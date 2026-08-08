#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/verify-celeste-tvos-runtime.sh [options]

Verify Stage 3B regenerated source, staged user-owned Content, no-audio and
privacy isolation, prior-stage integrity, and optionally a built tvOS app.
Installs nothing and never launches an application.

Options:
  --runtime-root DIR          Generated runtime root
                              (default: .build/celeste-runtime/current).
  --artifact-dir DIR          Privacy-safe runtime manifests
                              (default: artifacts/celeste-runtime/current).
  --compare-runtime-root DIR  Independently generated runtime tree to compare.
  --compare-artifact-dir DIR  Its manifests; required with --compare-runtime-root.
  --app DIR                   Validate a built Stage 3B .app bundle.
  --platform NAME             Required with --app: tvos or tvossimulator.
  --skip-toolchain            Skip local SDK/workload version checks.
  -h, --help                  Show this help.

Relative paths are resolved from the repository root. Generated inputs and
outputs must live in ignored repository paths.
USAGE
}

readonly BASELINE_COMMIT="7a761643904ad2b4bcbc1fae85c19a21d3fc93cc"
readonly STAGE1_LOGICAL_SHA256="61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39"
readonly MARKER=".stage3b-runtime-preparation"

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
RUNTIME_ROOT="$REPO_ROOT/.build/celeste-runtime/current"
ARTIFACT_DIR="$REPO_ROOT/artifacts/celeste-runtime/current"
COMPARE_RUNTIME_ROOT=""
COMPARE_ARTIFACT_DIR=""
APP_DIR=""
EXPECTED_PLATFORM=""
CHECK_TOOLCHAIN=1

repo_path() {
  case "$1" in
    /*) printf '%s\n' "$1" ;;
    *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;;
  esac
}

require_ignored_root() {
  local root="$1"
  local relative
  case "$root" in
    "$REPO_ROOT"/*) relative="${root#$REPO_ROOT/}" ;;
    *) echo "error: generated root must be below the repository" >&2; exit 1 ;;
  esac
  git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative/$MARKER" || {
    echo "error: generated root is not ignored: \$REPO_ROOT/$relative" >&2
    exit 1
  }
}

while (($#)); do
  case "$1" in
    --runtime-root)
      [[ $# -ge 2 ]] || { echo "error: --runtime-root requires a value" >&2; exit 2; }
      RUNTIME_ROOT="$(repo_path "$2")"; shift 2 ;;
    --artifact-dir)
      [[ $# -ge 2 ]] || { echo "error: --artifact-dir requires a value" >&2; exit 2; }
      ARTIFACT_DIR="$(repo_path "$2")"; shift 2 ;;
    --compare-runtime-root)
      [[ $# -ge 2 ]] || { echo "error: --compare-runtime-root requires a value" >&2; exit 2; }
      COMPARE_RUNTIME_ROOT="$(repo_path "$2")"; shift 2 ;;
    --compare-artifact-dir)
      [[ $# -ge 2 ]] || { echo "error: --compare-artifact-dir requires a value" >&2; exit 2; }
      COMPARE_ARTIFACT_DIR="$(repo_path "$2")"; shift 2 ;;
    --app)
      [[ $# -ge 2 ]] || { echo "error: --app requires a value" >&2; exit 2; }
      APP_DIR="$(repo_path "$2")"; shift 2 ;;
    --platform)
      [[ $# -ge 2 ]] || { echo "error: --platform requires a value" >&2; exit 2; }
      EXPECTED_PLATFORM="$2"; shift 2 ;;
    --skip-toolchain)
      CHECK_TOOLCHAIN=0; shift ;;
    -h|--help)
      usage; exit 0 ;;
    *)
      echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ -n "$COMPARE_RUNTIME_ROOT" || -n "$COMPARE_ARTIFACT_DIR" ]]; then
  [[ -n "$COMPARE_RUNTIME_ROOT" && -n "$COMPARE_ARTIFACT_DIR" ]] || {
    echo "error: both comparison directories are required" >&2; exit 2;
  }
fi
if [[ -n "$APP_DIR" || -n "$EXPECTED_PLATFORM" ]]; then
  [[ -n "$APP_DIR" && -n "$EXPECTED_PLATFORM" ]] || {
    echo "error: --app and --platform must be supplied together" >&2; exit 2;
  }
fi
case "$EXPECTED_PLATFORM" in
  ""|tvos|tvossimulator) ;;
  *) echo "error: --platform must be tvos or tvossimulator" >&2; exit 2 ;;
esac

for command in git python3 dotnet shasum; do
  command -v "$command" >/dev/null || { echo "error: required tool is missing: $command" >&2; exit 1; }
done
if [[ "$CHECK_TOOLCHAIN" -eq 1 ]]; then
  [[ "$(dotnet --version)" == "10.0.302" ]] || { echo "error: dotnet 10.0.302 is required" >&2; exit 1; }
  workload_output="$(dotnet workload list)"
  grep -Fq "Workload version: 10.0.302.0" <<<"$workload_output" || {
    echo "error: workload set 10.0.302.0 is required" >&2; exit 1;
  }
  grep -Eq '^tvos[[:space:]]' <<<"$workload_output" || {
    echo "error: the tvOS workload is not installed" >&2; exit 1;
  }
fi

for root in "$RUNTIME_ROOT" "$ARTIFACT_DIR" ${COMPARE_RUNTIME_ROOT:+"$COMPARE_RUNTIME_ROOT"} ${COMPARE_ARTIFACT_DIR:+"$COMPARE_ARTIFACT_DIR"}; do
  require_ignored_root "$root"
done
[[ -f "$RUNTIME_ROOT/$MARKER" && -f "$ARTIFACT_DIR/$MARKER" ]] || {
  echo "error: Stage 3B preparation marker is missing" >&2; exit 1;
}
for name in input-manifest.json managed-runtime-manifest.json content-staging-manifest.json preparation-result.json; do
  [[ -f "$ARTIFACT_DIR/$name" ]] || { echo "error: missing Stage 3B manifest: $name" >&2; exit 1; }
done

python3 - "$REPO_ROOT" "$RUNTIME_ROOT" "$ARTIFACT_DIR" <<'PY'
import hashlib, json, pathlib, re, sys
repo, runtime, artifacts = map(pathlib.Path, sys.argv[1:])
policy = json.loads((repo / "managed/celeste-stage3b-policy.json").read_text())
input_manifest = json.loads((artifacts / "input-manifest.json").read_text())
managed = json.loads((artifacts / "managed-runtime-manifest.json").read_text())
content = json.loads((artifacts / "content-staging-manifest.json").read_text())
result = json.loads((artifacts / "preparation-result.json").read_text())

if input_manifest.get("validation") != "supported-unmodified-fna-release" or input_manifest.get("gameVersion") != "1.4.0.0":
    raise SystemExit("error: runtime input is not the supported unmodified Celeste 1.4.0.0 release")
expected_managed = policy["stage3BOutput"]
for key in ("fileCount", "logicalSha256"):
    if managed.get(key) != expected_managed[key]:
        raise SystemExit(f"error: managed manifest {key} differs from the Stage 3B lock")
if managed.get("fmodLowLevelGuardCount") != 490 or managed.get("fmodLowLevelUniqueSymbolCount") != 490:
    raise SystemExit("error: managed output does not retain all 490 FMOD fail-fast guards")
if managed.get("stage3AInputLogicalSha256") != policy["stage3ABaseline"]["patchedSourceLogicalSha256"]:
    raise SystemExit("error: Stage 3B is not based on the accepted Stage 3A logical source")

managed_root = runtime / "managed"
entries = []
aggregate = hashlib.sha256()
for path in sorted(managed_root.rglob("*"), key=lambda item: item.relative_to(managed_root).as_posix()):
    if not path.is_file() or any(part in {"bin", "obj"} for part in path.relative_to(managed_root).parts):
        continue
    relative = path.relative_to(managed_root).as_posix()
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    entries.append((relative, digest, path.stat().st_size))
    aggregate.update(relative.encode() + b"\0" + digest.encode() + b"\n")
if len(entries) != managed["fileCount"] or aggregate.hexdigest() != managed["logicalSha256"]:
    raise SystemExit("error: generated managed tree changed after its manifest was written")
recorded = [(entry["path"], entry["sha256"], entry["size"]) for entry in managed["files"]]
if entries != recorded:
    raise SystemExit("error: generated managed file inventory differs from its manifest")

content_policy = policy["content"]
expected_content = {
    "stagedFileCount": content_policy["expectedStagedFileCount"],
    "stagedTotalBytes": content_policy["expectedStagedTotalBytes"],
    "stagedAggregateSha256": content_policy["expectedStagedAggregateSha256"],
    "excludedFileCount": content_policy["expectedExcludedFileCount"],
    "excludedTotalBytes": content_policy["expectedExcludedTotalBytes"],
}
for key, expected in expected_content.items():
    if content.get(key) != expected:
        raise SystemExit(f"error: content manifest {key} differs from the Stage 3B lock")
content_root = runtime / "content/Content"
actual_paths = []
aggregate = hashlib.sha256()
for entry in content["files"]:
    relative = entry["path"]
    if relative.startswith("FMOD/") or pathlib.PurePosixPath(relative).is_absolute() or ".." in pathlib.PurePosixPath(relative).parts:
        raise SystemExit(f"error: invalid staged Content path: {relative}")
    path = content_root / pathlib.PurePosixPath(relative)
    if path.is_symlink() or not path.is_file():
        raise SystemExit(f"error: staged Content file is missing or a symlink: {relative}")
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if path.stat().st_size != entry["size"] or digest != entry["sha256"]:
        raise SystemExit(f"error: staged Content file differs from its manifest: {relative}")
    actual_paths.append(relative)
    aggregate.update(relative.encode() + b"\0" + str(entry["size"]).encode() + b"\0" + digest.encode() + b"\n")
disk_paths = sorted(path.relative_to(content_root).as_posix() for path in content_root.rglob("*") if path.is_file())
if actual_paths != sorted(actual_paths) or actual_paths != disk_paths:
    raise SystemExit("error: staged Content file set differs from its deterministic manifest")
if aggregate.hexdigest() != content["stagedAggregateSha256"]:
    raise SystemExit("error: staged Content aggregate differs from its manifest")
if (content_root / "FMOD").exists():
    raise SystemExit("error: staged runtime contains the FMOD Content tree")
if result != {
    "schemaVersion": 1,
    "gameRoot": "$CELESTE_GAME_ROOT",
    "managedSourceRegeneratedFromStage3A": True,
    "managedSourceTracked": False,
    "contentStaged": True,
    "contentTracked": False,
    "fmodContentStaged": False,
    "fmodNativeLinked": False,
}:
    raise SystemExit("error: Stage 3B preparation result does not assert the expected isolation")

sources = "\n".join(path.read_text(errors="replace") for path in managed_root.rglob("*.cs"))
if re.search(r'\[DllImport\("fmod(?:studio|_SDL)?"', sources):
    raise SystemExit("error: generated source contains a native FMOD import")
if sources.count("TvOSStage3Bridge.FmodLowLevelReached(") != 490:
    raise SystemExit("error: generated source does not route exactly 490 FMOD guards centrally")
print("PASS: locked generated source, 490 FMOD guards, and staged non-audio Content")
PY

if [[ -n "$COMPARE_RUNTIME_ROOT" ]]; then
  [[ -f "$COMPARE_RUNTIME_ROOT/$MARKER" && -f "$COMPARE_ARTIFACT_DIR/$MARKER" ]] || {
    echo "error: comparison preparation marker is missing" >&2; exit 1;
  }
  for name in input-manifest.json managed-runtime-manifest.json content-staging-manifest.json preparation-result.json; do
    cmp -s "$ARTIFACT_DIR/$name" "$COMPARE_ARTIFACT_DIR/$name" || {
      echo "error: independent generation differs: $name" >&2; exit 1;
    }
  done
  python3 - "$RUNTIME_ROOT/managed" "$COMPARE_RUNTIME_ROOT/managed" <<'PY'
import hashlib, pathlib, sys
def manifest(root):
    root = pathlib.Path(root)
    return [(p.relative_to(root).as_posix(), hashlib.sha256(p.read_bytes()).hexdigest())
            for p in sorted(root.rglob("*"), key=lambda x: x.relative_to(root).as_posix())
            if p.is_file() and not any(part in {"bin", "obj"} for part in p.relative_to(root).parts)]
if manifest(sys.argv[1]) != manifest(sys.argv[2]):
    raise SystemExit("error: independent generated managed trees are not logically identical")
print("PASS: independent Stage 3B generations are logically identical")
PY
fi

python3 - "$REPO_ROOT" <<'PY'
import json, pathlib, plistlib, re, subprocess, sys, xml.etree.ElementTree as ET
root = pathlib.Path(sys.argv[1])
project_path = root / "tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj"
project_text = project_path.read_text()
project = ET.parse(project_path).getroot()
if project.findtext(".//TargetFramework") != "net10.0-tvos":
    raise SystemExit("error: Stage 3B host does not target net10.0-tvos")
if project.findtext(".//UseInterpreter") != "false" or project.findtext(".//SupportedOSPlatformVersion") != "16.0":
    raise SystemExit("error: Stage 3B host weakened AOT or deployment settings")
for mode in ("Stage2Diagnostic", "CelestePreflight", "Celeste"):
    if mode not in project_text:
        raise SystemExit(f"error: Stage 3B host lacks launch mode {mode}")
if "Stage3BLinker.xml" not in project_text or "Celeste.Modern.csproj" not in project_text:
    raise SystemExit("error: Stage 3B host lacks focused roots or generated Celeste reference")
if re.search(r"TrimmerRootAssembly|PublishTrimmed>false|UseInterpreter>true", project_text):
    raise SystemExit("error: Stage 3B host contains a blanket trim/AOT workaround")
with (root / "tvos/CelesteTvOSRuntimeHost/Entitlements.plist").open("rb") as stream:
    if plistlib.load(stream):
        raise SystemExit("error: Stage 3B entitlements are not empty")
tracked_tvos_text = [
    path for path in (root / "tvos").rglob("*")
    if path.is_file()
    and not any(part in {"bin", "obj"} for part in path.relative_to(root / "tvos").parts)
    and path.suffix.lower() in {".cs", ".csproj", ".plist", ".props", ".targets", ".xml"}
]
if "com.apple.developer.user-management" in "\n".join(path.read_text(errors="ignore") for path in tracked_tvos_text):
    raise SystemExit("error: a tvOS project requests the deferred User Management entitlement")

symbols = json.loads((root / "native/tvos-symbol-expectations.json").read_text())["components"]["tvStubs"]["symbols"]
source_roots = [root / "tvos/CelesteTvOSRuntimeHost", root / "managed/templates"]
source_paths = [
    path for source_root in source_roots for path in source_root.rglob("*.cs")
    if not any(part in {"bin", "obj"} for part in path.relative_to(source_root).parts)
]
sources = "\n".join(path.read_text(errors="replace") for path in source_paths)
used = sorted(symbol for symbol in symbols if re.search(rf"\b{re.escape(symbol)}\s*\(", sources))
if used:
    raise SystemExit("error: intended Stage 3B source path calls tvStubs symbols: " + ", ".join(used))

changed = subprocess.check_output([
    "git", "-C", str(root), "diff", "--name-only", "--diff-filter=ACMR", "7a761643904ad2b4bcbc1fae85c19a21d3fc93cc", "--"
], text=True).splitlines()
untracked = subprocess.check_output([
    "git", "-C", str(root), "ls-files", "--others", "--exclude-standard"
], text=True).splitlines()
paths = [root / line for line in sorted(set(changed + untracked)) if line]
patterns = {
    "absolute user path": re.compile(b"/" + rb"Users/[^/$\s]+/"),
    "email address": re.compile(rb"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"),
    "device/profile UUID": re.compile(rb"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b"),
    "identity fingerprint": re.compile(rb"\b[A-F0-9]{40}\b"),
    "literal development team": re.compile(rb"<DevelopmentTeam>\s*[A-Z0-9]{10}\s*</DevelopmentTeam>"),
}
for path in paths:
    if not path.is_file():
        continue
    data = path.read_bytes()
    if b"MZ\x90\x00" in data[:8] or data[:4] in {b"\xca\xfe\xba\xbe", b"\xcf\xfa\xed\xfe"}:
        raise SystemExit(f"error: candidate tracked file is a generated/proprietary binary: {path.relative_to(root)}")
    for label, pattern in patterns.items():
        if pattern.search(data):
            raise SystemExit(f"error: {label} found in candidate tracked file {path.relative_to(root)}")
for forbidden in ("Celeste.exe", "Celeste.dll", "Celeste.Content.dll"):
    if any(path.name == forbidden for path in paths):
        raise SystemExit(f"error: proprietary/generated assembly is a candidate tracked file: {forbidden}")
print("PASS: focused roots, empty entitlements, no tvStubs calls, and tracked-file privacy isolation")
PY

stage1_hash="$(python3 - "$REPO_ROOT/artifacts/tvos-native/rebuild-e/normalized-manifest.json" <<'PY'
import json, pathlib, sys
print(json.loads(pathlib.Path(sys.argv[1]).read_text())["logicalSetSha256"])
PY
)"
[[ "$stage1_hash" == "$STAGE1_LOGICAL_SHA256" ]] || {
  echo "error: Stage 1 accepted logical hash changed" >&2; exit 1;
}
if [[ "$CHECK_TOOLCHAIN" -eq 1 ]]; then
  "$REPO_ROOT/scripts/verify-tvos-host.sh" >/dev/null
else
  "$REPO_ROOT/scripts/verify-tvos-host.sh" --skip-toolchain >/dev/null
fi

git -C "$REPO_ROOT" diff --quiet "$BASELINE_COMMIT" -- \
  build.sh celestemeow fnalibs-ios-builder-celeste FNA \
  TVOS_PORT_PLAN.md TVOS_NATIVE_BUILD_REPORT.md TVOS_HOST_STAGE2_REPORT.md \
  TVOS_CELESTE_MANAGED_STAGE3A_REPORT.md global.json native \
  managed/celeste-analysis-policy.json managed/celeste-compatibility-ledger.json \
  managed/celeste-generation.lock.json managed/patches \
  managed/templates/Celeste.Content.Modern.csproj managed/templates/Celeste.Modern.csproj \
  managed/templates/Stage3AContentIdentity.cs \
  scripts/fetch-tvos-deps.sh scripts/build-tvos-native.sh scripts/verify-tvos-native.sh \
  scripts/verify-tvos-artifacts.py scripts/prepare-tvos-host-native.sh \
  scripts/verify-tvos-host.sh scripts/run-tvos-host-simulator.sh \
  scripts/validate-celeste-input.sh scripts/prepare-celeste-managed.sh \
  scripts/build-celeste-managed.sh scripts/verify-celeste-managed.sh \
  scripts/celeste-managed.py tvos/CelesteTvOSHost tvos/FNA.TvOS \
  tvos/CelesteManagedAotClosure tvos/stage2-ios-native-baseline.sha256 || {
  echo "error: existing iOS or Stage 1/2/3A tracked foundation changed" >&2; exit 1;
}
echo "PASS: Stage 1 hash and existing iOS/Stage 1/2/3A tracked foundations are unchanged"

if [[ -n "$APP_DIR" ]]; then
  [[ -d "$APP_DIR" ]] || { echo "error: app bundle not found" >&2; exit 1; }
  if [[ "$CHECK_TOOLCHAIN" -eq 1 ]]; then
    "$REPO_ROOT/scripts/verify-tvos-host.sh" --app "$APP_DIR" --platform "$EXPECTED_PLATFORM" >/dev/null
  else
    "$REPO_ROOT/scripts/verify-tvos-host.sh" --app "$APP_DIR" --platform "$EXPECTED_PLATFORM" --skip-toolchain >/dev/null
  fi
  if find "$APP_DIR" -type f \( -iname 'libfmod*' -o -iname 'fmod*.a' -o -iname 'fmod*.dylib' \
      -o -iname 'FMOD.dll' -o -iname '*.bank' -o -iname 'Celeste.exe' -o -iname '*.celeste' \) \
      -print -quit | grep -q .; then
    echo "error: app bundle contains FMOD, legacy executable, or save material" >&2; exit 1
  fi
  [[ ! -d "$APP_DIR/Content/FMOD" ]] || { echo "error: app bundle contains FMOD Content" >&2; exit 1; }
  [[ -f "$APP_DIR/Content/Effects/Border.xnb" && -f "$APP_DIR/Content/Monocle/MonocleDefault.xnb" ]] || {
    echo "error: app bundle lacks representative validated Content" >&2; exit 1;
  }
  executable_name="$(plutil -extract CFBundleExecutable raw "$APP_DIR/Info.plist")"
  executable="$APP_DIR/$executable_name"
  if nm -u "$executable" 2>/dev/null | grep -E '_FMOD_' >/dev/null; then
    echo "error: app executable has an unresolved native FMOD symbol" >&2; exit 1
  fi
  if otool -L "$executable" | grep -E -i 'iPhoneOS|iOSSimulator|MacOSX|fmod' >/dev/null; then
    echo "error: app executable references a forbidden platform or FMOD binary" >&2; exit 1
  fi
  echo "PASS: $EXPECTED_PLATFORM app is arm64, platform-correct, and contains no FMOD/save material"
fi

echo "Stage 3B static/runtime-input verification passed."
