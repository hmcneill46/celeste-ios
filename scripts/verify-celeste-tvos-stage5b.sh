#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/verify-celeste-tvos-stage5b.sh [options]

Verify the locked Stage 5B real-audio generated source, its exact Stage 5A
native/bank inputs, prior-stage isolation, and optionally a signed device app.
This command installs nothing and does not launch the app.

Options:
  --runtime-root DIR          Ignored Stage 5B runtime root
                              (default: .build/celeste-runtime/stage5b-current).
  --artifact-dir DIR          Ignored Stage 5B manifests
                              (default: artifacts/celeste-runtime/stage5b-current).
  --compare-runtime-root DIR  Independent clean generation to compare.
  --compare-artifact-dir DIR  Its manifests; required with compare runtime.
  --fmod-stage DIR            Accepted Stage 5A staging
                              (default: .build/fmod-tvos/current).
  --app DIR                   Validate a signed CelesteAudio device app.
  --skip-toolchain            Skip SDK/workload checks.
  -h, --help                  Show this help.

The sole generated 1.10.20 import absent from native 1.10.09 must remain the
unreferenced FMOD_DSP_GetCPUUsage telemetry method. It may not be fabricated,
and it must not survive trimming into the device executable.
USAGE
}

readonly BASELINE_COMMIT="e9134d8129c5d3bf7b97c42c598f85df2e3cdc23"
readonly EXPECTED_STAGE1_SHA="61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39"
readonly EXPECTED_STAGE5A_SHA="b32fc89dbefdda69ab7bf20ea9ece37826dce51787a4f70446493e292b12603d"
readonly EXPECTED_STAGE5A_VERIFIER_SHA="dc33936e72d361a3e3399f228cb4f33c85eeba25cd9f4199ec23ba3afb623058"
readonly EXPECTED_STAGE5B_SHA="4e4e96f3d15815430a2f8ad063a4c4cdff004273b7513cf972d56ecd0b04460d"
readonly EXPECTED_STAGE5B_FILES=928
readonly MARKER=".stage5b-runtime-preparation"

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
RUNTIME_ROOT="$REPO_ROOT/.build/celeste-runtime/stage5b-current"
ARTIFACT_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage5b-current"
FMOD_STAGE="$REPO_ROOT/.build/fmod-tvos/current"
COMPARE_RUNTIME_ROOT=""
COMPARE_ARTIFACT_DIR=""
APP_DIR=""
SKIP_TOOLCHAIN=0

repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }

while (($#)); do
  case "$1" in
    --runtime-root) [[ $# -ge 2 ]] || exit 2; RUNTIME_ROOT="$(repo_path "$2")"; shift 2 ;;
    --artifact-dir) [[ $# -ge 2 ]] || exit 2; ARTIFACT_DIR="$(repo_path "$2")"; shift 2 ;;
    --compare-runtime-root) [[ $# -ge 2 ]] || exit 2; COMPARE_RUNTIME_ROOT="$(repo_path "$2")"; shift 2 ;;
    --compare-artifact-dir) [[ $# -ge 2 ]] || exit 2; COMPARE_ARTIFACT_DIR="$(repo_path "$2")"; shift 2 ;;
    --fmod-stage) [[ $# -ge 2 ]] || exit 2; FMOD_STAGE="$(repo_path "$2")"; shift 2 ;;
    --app) [[ $# -ge 2 ]] || exit 2; APP_DIR="$(repo_path "$2")"; shift 2 ;;
    --skip-toolchain) SKIP_TOOLCHAIN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ -n "$COMPARE_RUNTIME_ROOT" || -n "$COMPARE_ARTIFACT_DIR" ]]; then
  [[ -n "$COMPARE_RUNTIME_ROOT" && -n "$COMPARE_ARTIFACT_DIR" ]] || {
    echo "error: both comparison roots are required" >&2; exit 2;
  }
fi
for command in dotnet git python3 xcrun nm shasum; do
  command -v "$command" >/dev/null || { echo "error: missing existing tool: $command" >&2; exit 1; }
done
if [[ "$SKIP_TOOLCHAIN" -eq 0 ]]; then
  [[ "$(dotnet --version)" == "10.0.302" ]] || { echo "error: dotnet 10.0.302 is required" >&2; exit 1; }
  workload_output="$(dotnet workload list)"
  grep -Fq "Workload version: 10.0.302.0" <<<"$workload_output" || { echo "error: workload set 10.0.302.0 is required" >&2; exit 1; }
  grep -Eq '^tvos[[:space:]]' <<<"$workload_output" || { echo "error: tvOS workload is missing" >&2; exit 1; }
fi

for root in "$RUNTIME_ROOT" "$ARTIFACT_DIR" ${COMPARE_RUNTIME_ROOT:+"$COMPARE_RUNTIME_ROOT"} ${COMPARE_ARTIFACT_DIR:+"$COMPARE_ARTIFACT_DIR"}; do
  case "$root" in "$REPO_ROOT"/*) relative="${root#$REPO_ROOT/}" ;; *) echo "error: generated roots must be below the repository" >&2; exit 1 ;; esac
  git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative/$MARKER" || {
    echo "error: generated root is not ignored: \$REPO_ROOT/$relative" >&2; exit 1;
  }
done
[[ -f "$RUNTIME_ROOT/$MARKER" && -f "$ARTIFACT_DIR/$MARKER" ]] || { echo "error: Stage 5B output markers are missing" >&2; exit 1; }
[[ -f "$ARTIFACT_DIR/managed-stage5b-manifest.json" ]] || { echo "error: Stage 5B managed manifest is missing" >&2; exit 1; }
[[ -f "$FMOD_STAGE/logical-manifest.json" ]] || { echo "error: accepted Stage 5A staging is missing" >&2; exit 1; }
[[ "$(shasum -a 256 "$REPO_ROOT/scripts/verify-fmod-tvos.sh" | awk '{print $1}')" == "$EXPECTED_STAGE5A_VERIFIER_SHA" ]] || {
  echo "error: the Stage 5A verifier differs from the Stage 5B-compatible lock" >&2; exit 1;
}

"$REPO_ROOT/scripts/verify-fmod-tvos.sh" --stage-dir "$FMOD_STAGE" >/dev/null

python3 - "$REPO_ROOT" "$RUNTIME_ROOT" "$ARTIFACT_DIR" "$FMOD_STAGE" \
  "$EXPECTED_STAGE1_SHA" "$EXPECTED_STAGE5A_SHA" "$EXPECTED_STAGE5B_SHA" "$EXPECTED_STAGE5B_FILES" <<'PY'
import collections, hashlib, json, pathlib, re, sys
repo, runtime, artifacts, fmod = map(pathlib.Path, sys.argv[1:5])
stage1_sha, stage5a_sha, stage5b_sha, stage5b_files = sys.argv[5:]
stage5b_files = int(stage5b_files)
policy = json.loads((repo / "managed/celeste-stage5b-policy.json").read_text())
manifest = json.loads((artifacts / "managed-stage5b-manifest.json").read_text())
native = json.loads((fmod / "logical-manifest.json").read_text())
if native.get("stage1LogicalSha256") != stage1_sha or native.get("logicalSha256") != stage5a_sha:
    raise SystemExit("error: Stage 1 or Stage 5A logical input hash changed")
if policy.get("stage5BOutput") != {"fileCount": stage5b_files, "logicalSha256": stage5b_sha}:
    raise SystemExit("error: Stage 5B output policy is not the accepted lock")
if manifest.get("fileCount") != stage5b_files or manifest.get("logicalSha256") != stage5b_sha:
    raise SystemExit("error: generated Stage 5B output differs from the lock")
if manifest.get("restoredImportCount") != 490 or manifest.get("restoredImportsByOriginalLibrary") != {"fmod": 320, "fmod_SDL": 1, "fmodstudio": 169}:
    raise SystemExit("error: restored generated FMOD API count changed")
if manifest.get("audioMode") != "TVOS_REAL_AUDIO" or manifest.get("deviceImportName") != "__Internal":
    raise SystemExit("error: real-audio compile mode/import mapping changed")

root = runtime / "managed"
entries, aggregate = [], hashlib.sha256()
for path in sorted(root.rglob("*"), key=lambda p: p.relative_to(root).as_posix()):
    if not path.is_file() or any(part in {"bin", "obj"} for part in path.relative_to(root).parts):
        continue
    relative = path.relative_to(root).as_posix()
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    entries.append((relative, digest, path.stat().st_size))
    aggregate.update(relative.encode() + b"\0" + digest.encode() + b"\n")
if len(entries) != stage5b_files or aggregate.hexdigest() != stage5b_sha:
    raise SystemExit("error: generated Stage 5B tree changed after manifest creation")
if entries != [(item["path"], item["sha256"], item["size"]) for item in manifest["files"]]:
    raise SystemExit("error: generated Stage 5B file inventory differs from its manifest")

sources = "\n".join(path.read_text(errors="replace") for path in root.rglob("*.cs"))
project = (root / "Celeste.Modern.csproj").read_text()
required = (
    "TVOS_REAL_AUDIO", "TvOSStage5BAudioBridge.NativeRuntimeVersion", "FMOD_SDL_Register(system.getRaw())",
    "TvOSStage5BAudioBridge.AllBanksLoaded(system)", "Audio.TriggerCueStage5B", "TvOSStage5BAudioBridge.BusState",
    "public static void ShutdownSafely", "TvOSStage5BAudioBridge.EventOperation",
)
for token in required:
    if token not in sources + project:
        raise SystemExit(f"error: real-audio generated route is missing: {token}")
if "TVOS_AUDIO_DISABLED" in project or ".FmodLowLevelReached(" in sources:
    raise SystemExit("error: real-audio output contains the no-audio implementation or low-level guards")
imports = re.findall(r'\[DllImport\("([^"\n]+)"', sources)
if len(imports) != 490 or set(imports) != {"__Internal"}:
    raise SystemExit(f"error: expected 490 __Internal FMOD imports, found {len(imports)} and {sorted(set(imports))}")
symbols = [item["symbol"] for item in manifest["restoredImports"]]
if len(symbols) != 490 or collections.Counter(symbols)["FMOD_DSP_GetCPUUsage"] != 1:
    raise SystemExit("error: generated import inventory changed")
callers = []
needle = "FMOD_DSP_GetCPUUsage("
for path in root.rglob("*.cs"):
    text = path.read_text(errors="replace")
    if needle in text:
        callers.append(path.relative_to(root).as_posix())
if callers != ["FMOD/DSP.cs"]:
    raise SystemExit(f"error: absent 1.10.20 telemetry import gained a Celeste caller: {callers}")
print("PASS: locked real-audio source, 490 __Internal imports, and honest 1.10.20/1.10.09 boundary")
PY

temporary_dir="$(mktemp -d "${TMPDIR:-/tmp}/celeste-stage5b-verify.XXXXXX")"
trap 'rm -rf -- "$temporary_dir"' EXIT
for archive in \
  "$FMOD_STAGE/xcframeworks/FMOD.xcframework/tvos-arm64/libfmod_appletvos-localized.a" \
  "$FMOD_STAGE/xcframeworks/FMODStudio.xcframework/tvos-arm64/libfmodstudio_appletvos.a" \
  "$FMOD_STAGE/xcframeworks/FMODSDL.xcframework/tvos-arm64/libfmod_SDL.a"; do
  [[ -f "$archive" ]] || { echo "error: prepared archive is missing: ${archive#$REPO_ROOT/}" >&2; exit 1; }
  nm -gjU "$archive" >> "$temporary_dir/native-symbols.txt"
done
python3 - "$ARTIFACT_DIR/managed-stage5b-manifest.json" "$temporary_dir/native-symbols.txt" <<'PY'
import json, pathlib, sys
manifest = json.loads(pathlib.Path(sys.argv[1]).read_text())
expected = {item["symbol"] for item in manifest["restoredImports"]}
actual = {line.strip().lstrip("_") for line in pathlib.Path(sys.argv[2]).read_text().splitlines() if line.strip()}
missing = sorted(expected - actual)
if missing != ["FMOD_DSP_GetCPUUsage"]:
    raise SystemExit(f"error: unexpected managed/native FMOD symbol gap: {missing}")
for required in ("FMOD_System_GetVersion", "FMOD_Studio_System_Initialize", "FMOD_Studio_EventInstance_TriggerCue", "FMOD_SDL_Register"):
    if required not in actual:
        raise SystemExit(f"error: required native symbol is absent: {required}")
print("PASS: native 1.10.09 satisfies every reachable generated import; only unused 1.10.20 telemetry is absent")
PY

if [[ -n "$COMPARE_RUNTIME_ROOT" ]]; then
  [[ -f "$COMPARE_RUNTIME_ROOT/$MARKER" && -f "$COMPARE_ARTIFACT_DIR/$MARKER" ]] || { echo "error: comparison markers are missing" >&2; exit 1; }
  cmp -s "$ARTIFACT_DIR/managed-stage5b-manifest.json" "$COMPARE_ARTIFACT_DIR/managed-stage5b-manifest.json" || {
    echo "error: independent Stage 5B managed manifests differ" >&2; exit 1;
  }
  cmp -s "$ARTIFACT_DIR/preparation-result.json" "$COMPARE_ARTIFACT_DIR/preparation-result.json" || {
    echo "error: independent Stage 5B preparation manifests differ" >&2; exit 1;
  }
  echo "PASS: two independent clean Stage 5B generations are logically identical"
fi

git -C "$REPO_ROOT" diff --quiet "$BASELINE_COMMIT" -- \
  build.sh celestemeow fnalibs-ios-builder-celeste FNA native \
  TVOS_PORT_PLAN.md TVOS_NATIVE_BUILD_REPORT.md TVOS_HOST_STAGE2_REPORT.md \
  TVOS_CELESTE_MANAGED_STAGE3A_REPORT.md TVOS_CELESTE_RUNTIME_STAGE3B_REPORT.md \
  TVOS_CELESTE_PROLOGUE_STAGE3C_REPORT.md TVOS_FMOD_DIAGNOSTIC_STAGE5A_REPORT.md \
  global.json managed/celeste-generation.lock.json managed/celeste-stage3b-policy.json \
  managed/celeste-stage3c-policy.json managed/patches \
  scripts/fetch-tvos-deps.sh scripts/build-tvos-native.sh scripts/verify-tvos-native.sh \
  scripts/validate-celeste-input.sh scripts/prepare-celeste-managed.sh scripts/celeste-managed.py \
  scripts/prepare-celeste-tvos-runtime.sh scripts/celeste-stage3b.py \
  scripts/prepare-celeste-tvos-stage3c.sh scripts/celeste-stage3c.py \
  scripts/validate-fmod-tvos-sdk.sh scripts/prepare-fmod-tvos.sh \
  native/fmod-tvos tvos/CelesteTvOSHost tvos/FNA.TvOS tvos/CelesteManagedAotClosure \
  tvos/stage2-ios-native-baseline.sha256 || {
    echo "error: existing iOS or accepted Stage 1/2/3A/3B/3C/5A foundation changed" >&2; exit 1;
  }
(cd "$REPO_ROOT" && shasum -a 256 -c tvos/stage2-ios-native-baseline.sha256 >/dev/null)
if grep -R -n -E 'com\.apple\.developer\.user-management' "$REPO_ROOT/tvos" >/dev/null; then
  echo "error: Stage 5B must not add User Management" >&2; exit 1
fi
echo "PASS: accepted prior stages, iOS archives, and storage/entitlement boundaries remain isolated"

if [[ -n "$APP_DIR" ]]; then
  [[ -d "$APP_DIR" ]] || { echo "error: --app must name a built app" >&2; exit 2; }
  codesign --verify --deep --strict "$APP_DIR"
  executable_name="$(plutil -extract CFBundleExecutable raw "$APP_DIR/Info.plist")"
  executable="$APP_DIR/$executable_name"
  [[ -f "$executable" ]] || { echo "error: app executable is missing" >&2; exit 1; }
  file "$executable" | grep -Fq 'arm64' || { echo "error: app executable is not arm64" >&2; exit 1; }
  build_info="$(xcrun vtool -show-build "$executable")"
  grep -Eq 'platform[[:space:]]+TVOS$' <<<"$build_info" || { echo "error: app executable is not TVOS" >&2; exit 1; }
  grep -Eq 'minos[[:space:]]+16\.0$' <<<"$build_info" || { echo "error: app minimum tvOS is not 16.0" >&2; exit 1; }
  grep -Eq 'sdk[[:space:]]+26\.5$' <<<"$build_info" || { echo "error: app SDK is not 26.5" >&2; exit 1; }

  nm -gjU "$executable" > "$temporary_dir/app-defined.txt"
  nm -gu "$executable" > "$temporary_dir/app-undefined.txt"
  for symbol in FMOD_System_GetVersion FMOD_Studio_System_Create FMOD_Studio_System_Initialize FMOD_Studio_System_LoadBankFile FMOD_Studio_EventInstance_TriggerCue FMOD_SDL_Register; do
    grep -Fxq "_$symbol" "$temporary_dir/app-defined.txt" || { echo "error: signed app lacks real export $symbol" >&2; exit 1; }
  done
  if grep -E -q 'FMOD_DSP_GetCPUUsage' "$temporary_dir/app-defined.txt" "$temporary_dir/app-undefined.txt"; then
    echo "error: unreachable 1.10.20 telemetry import survived trimming" >&2; exit 1
  fi

  python3 - "$FMOD_STAGE/bank-manifest.json" "$APP_DIR" <<'PY'
import hashlib, json, pathlib, sys
manifest = json.loads(pathlib.Path(sys.argv[1]).read_text())
app = pathlib.Path(sys.argv[2])
expected = {item["path"]: (item["size"], item["sha256"]) for item in manifest["banks"]}
actual = {}
for path in (app / "Content" / "FMOD").rglob("*.bank"):
    relative = path.relative_to(app).as_posix()
    actual[relative] = (path.stat().st_size, hashlib.sha256(path.read_bytes()).hexdigest())
if actual != expected:
    raise SystemExit("error: audio-enabled app does not contain exactly the seven validated banks")
print(f"PASS: exactly seven validated banks; bytes={sum(size for size, _ in actual.values())}")
PY
  if find "$APP_DIR" -type f \( -iname '*.h' -o -iname '*.cs' -o -iname '*.a' -o -iname '*.so' -o -iname '*.dylib' -o -iname '*.celeste' -o -iname 'settings.celeste' \) -print -quit | grep -q .; then
    echo "error: app contains SDK/source/loose native/save/settings material" >&2; exit 1
  fi
  profile_count="$(find "$APP_DIR" -type f -iname '*.mobileprovision' | wc -l | tr -d ' ')"
  [[ "$profile_count" == "1" && -f "$APP_DIR/embedded.mobileprovision" ]] || {
    echo "error: signed development app must contain exactly its one embedded profile" >&2; exit 1;
  }
  if grep -a -R -n -E 'platform[[:space:]]+(IOS|IOSSIMULATOR|TVOSSIMULATOR|MACOS)' "$APP_DIR" >/dev/null; then
    echo "error: app contains evidence of a non-device-tvOS native slice" >&2; exit 1
  fi
  echo "PASS: signed full-AOT arm64 TVOS app, real FMOD exports, exact banks, and clean package boundary"
fi

python3 - "$REPO_ROOT" "$BASELINE_COMMIT" <<'PY'
import pathlib, re, subprocess, sys
root, baseline = pathlib.Path(sys.argv[1]), sys.argv[2]
changed = subprocess.check_output(["git", "-C", str(root), "diff", "--name-only", baseline, "--"], text=True).splitlines()
untracked = subprocess.check_output(["git", "-C", str(root), "ls-files", "--others", "--exclude-standard"], text=True).splitlines()
patterns = {
    "absolute home path": re.compile(b"/" + b"Users/" + rb"[^/$\s]+/"),
    "email": re.compile(rb"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"),
    "UUID": re.compile(rb"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b"),
    "certificate fingerprint": re.compile(rb"\b[A-F0-9]{40}\b"),
    "Team ID": re.compile(rb"<DevelopmentTeam>\s*[A-Z0-9]{10}\s*</DevelopmentTeam>"),
}
binary_suffixes = {".a", ".bank", ".xnb", ".exe", ".dll", ".dylib", ".so", ".mobileprovision", ".p12", ".cer", ".celeste"}
for relative in sorted(set(changed + untracked)):
    path = root / relative
    if not path.is_file():
        continue
    if path.suffix.lower() in binary_suffixes:
        raise SystemExit(f"error: proprietary/generated/signing binary is a candidate tracked file: {relative}")
    data = path.read_bytes()
    for label, pattern in patterns.items():
        if pattern.search(data):
            raise SystemExit(f"error: {label} found in candidate tracked file: {relative}")
print("PASS: candidate tracked files contain no proprietary binaries or private identity/path data")
PY

echo "Stage 5B static, generated-input, native-input, and package verification passed."
