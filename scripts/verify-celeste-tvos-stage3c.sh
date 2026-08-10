#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/verify-celeste-tvos-stage3c.sh [options]

Verify the locked Stage 3C generated-source transformation, temporary SaveData
serializer, haptic lifecycle, prior-stage isolation and an optional built app.
Installs nothing and never launches an application.

Options:
  --runtime-root DIR          Stage 3C generated runtime root
                              (default: .build/celeste-runtime/stage3c-current).
  --artifact-dir DIR          Stage 3C ignored manifests
                              (default: artifacts/celeste-runtime/stage3c-current).
  --compare-runtime-root DIR  Independent Stage 3C generation to compare.
  --compare-artifact-dir DIR  Its manifests; required with compare runtime.
  --app DIR                   Validate a built app.
  --platform NAME             Required with --app: tvos or tvossimulator.
  --skip-toolchain            Skip SDK/workload checks.
  -h, --help                  Show this help.
USAGE
}

readonly BASELINE_COMMIT="e5bc7bae395cc97832d7f60f140c157ae6302f2f"
readonly MARKER=".stage3c-runtime-preparation"
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
RUNTIME_ROOT="$REPO_ROOT/.build/celeste-runtime/stage3c-current"
ARTIFACT_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage3c-current"
COMPARE_RUNTIME_ROOT=""
COMPARE_ARTIFACT_DIR=""
APP_DIR=""
PLATFORM=""
SKIP_TOOLCHAIN=0

repo_path() {
  case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac
}

while (($#)); do
  case "$1" in
    --runtime-root) [[ $# -ge 2 ]] || exit 2; RUNTIME_ROOT="$(repo_path "$2")"; shift 2 ;;
    --artifact-dir) [[ $# -ge 2 ]] || exit 2; ARTIFACT_DIR="$(repo_path "$2")"; shift 2 ;;
    --compare-runtime-root) [[ $# -ge 2 ]] || exit 2; COMPARE_RUNTIME_ROOT="$(repo_path "$2")"; shift 2 ;;
    --compare-artifact-dir) [[ $# -ge 2 ]] || exit 2; COMPARE_ARTIFACT_DIR="$(repo_path "$2")"; shift 2 ;;
    --app) [[ $# -ge 2 ]] || exit 2; APP_DIR="$(repo_path "$2")"; shift 2 ;;
    --platform) [[ $# -ge 2 ]] || exit 2; PLATFORM="$2"; shift 2 ;;
    --skip-toolchain) SKIP_TOOLCHAIN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ -n "$COMPARE_RUNTIME_ROOT" || -n "$COMPARE_ARTIFACT_DIR" ]]; then
  [[ -n "$COMPARE_RUNTIME_ROOT" && -n "$COMPARE_ARTIFACT_DIR" ]] || { echo "error: both comparison roots are required" >&2; exit 2; }
fi
if [[ -n "$APP_DIR" || -n "$PLATFORM" ]]; then
  [[ -n "$APP_DIR" && "$PLATFORM" =~ ^(tvos|tvossimulator)$ ]] || { echo "error: --app requires --platform tvos or tvossimulator" >&2; exit 2; }
fi
for command in dotnet git python3; do command -v "$command" >/dev/null || { echo "error: missing tool: $command" >&2; exit 1; }; done
for root in "$RUNTIME_ROOT" "$ARTIFACT_DIR" ${COMPARE_RUNTIME_ROOT:+"$COMPARE_RUNTIME_ROOT"} ${COMPARE_ARTIFACT_DIR:+"$COMPARE_ARTIFACT_DIR"}; do
  case "$root" in "$REPO_ROOT"/*) relative="${root#$REPO_ROOT/}" ;; *) echo "error: generated root must be below repository" >&2; exit 1 ;; esac
  git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative/$MARKER" || { echo "error: generated root is not ignored: \$REPO_ROOT/$relative" >&2; exit 1; }
done
[[ -f "$RUNTIME_ROOT/$MARKER" && -f "$ARTIFACT_DIR/$MARKER" ]] || { echo "error: Stage 3C markers are missing" >&2; exit 1; }
[[ -f "$ARTIFACT_DIR/managed-stage3c-manifest.json" ]] || { echo "error: Stage 3C manifest is missing" >&2; exit 1; }

stage3b_args=(--runtime-root "$RUNTIME_ROOT/stage3b" --artifact-dir "$ARTIFACT_DIR/stage3b")
if [[ "$SKIP_TOOLCHAIN" -eq 1 ]]; then stage3b_args+=(--skip-toolchain); fi
"$REPO_ROOT/scripts/verify-celeste-tvos-runtime.sh" "${stage3b_args[@]}" >/dev/null

python3 - "$REPO_ROOT" "$RUNTIME_ROOT" "$ARTIFACT_DIR" <<'PY'
import hashlib, json, pathlib, re, sys
repo, runtime, artifacts = map(pathlib.Path, sys.argv[1:])
policy = json.loads((repo / "managed/celeste-stage3c-policy.json").read_text())
manifest = json.loads((artifacts / "managed-stage3c-manifest.json").read_text())
if manifest.get("faultBaseline"):
    raise SystemExit("error: corrected Stage 3C verifier received the fault-baseline output")
if manifest.get("stage3BInputLogicalSha256") != policy["stage3BInput"]["logicalSha256"]:
    raise SystemExit("error: Stage 3B logical input differs from the Stage 3C lock")
expected = policy.get("stage3COutput")
if not expected:
    raise SystemExit("error: policy does not lock the accepted Stage 3C output")
for key in ("fileCount", "logicalSha256"):
    if manifest.get(key) != expected.get(key):
        raise SystemExit(f"error: generated Stage 3C {key} differs from policy")

root = runtime / "managed"
entries = []
aggregate = hashlib.sha256()
for path in sorted(root.rglob("*"), key=lambda p: p.relative_to(root).as_posix()):
    if not path.is_file() or any(part in {"bin", "obj"} for part in path.relative_to(root).parts):
        continue
    relative = path.relative_to(root).as_posix()
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    entries.append((relative, digest, path.stat().st_size))
    aggregate.update(relative.encode() + b"\0" + digest.encode() + b"\n")
if len(entries) != manifest["fileCount"] or aggregate.hexdigest() != manifest["logicalSha256"]:
    raise SystemExit("error: Stage 3C generated tree changed after manifest creation")
if entries != [(e["path"], e["sha256"], e["size"]) for e in manifest["files"]]:
    raise SystemExit("error: Stage 3C generated file inventory differs from manifest")

sources = "\n".join(path.read_text(errors="replace") for path in root.rglob("*.cs"))
requirements = (
    "TvOSSaveDataSerializer.Deserialize(stream)",
    "TvOSSaveDataSerializer.SerializeToBytes((SaveData)(object)instance)",
    "Audio.TriggerCueNoAudio(Audio.CurrentMusicEventInstance)",
    "ControllerConnectionChanged((int)PlayerIndex, connected: false)",
    "StopAllRumble(WasSkipped ? \"cutscene-skip\" : \"cutscene-complete\")",
    "StopAllRumble(\"unhandled-exception\")",
    "StopAllRumble(\"resign-active\")",
)
for token in requirements:
    if token not in sources:
        raise SystemExit(f"error: required Stage 3C generated route is missing: {token}")
if sources.count("TvOSStage3Bridge.FmodLowLevelReached(") != 490:
    raise SystemExit("error: all 490 FMOD low-level guards were not retained")
if re.search(r'\[DllImport\("fmod(?:studio|_SDL)?"', sources, re.I):
    raise SystemExit("error: generated source imports native FMOD")
if "Stage 3B does not serialize save data" in sources or "Stage 3B does not deserialize save data" in sources:
    raise SystemExit("error: normal SaveData still reaches the Stage 3B runtime guard")
print("PASS: locked Stage 3C source, explicit SaveData route, 490 FMOD guards, and haptic lifecycle")
PY

if [[ -n "$COMPARE_RUNTIME_ROOT" ]]; then
  [[ -f "$COMPARE_RUNTIME_ROOT/$MARKER" && -f "$COMPARE_ARTIFACT_DIR/$MARKER" ]] || { echo "error: comparison Stage 3C markers are missing" >&2; exit 1; }
  cmp -s "$ARTIFACT_DIR/managed-stage3c-manifest.json" "$COMPARE_ARTIFACT_DIR/managed-stage3c-manifest.json" || {
    echo "error: independent Stage 3C manifests differ" >&2; exit 1;
  }
  cmp -s "$ARTIFACT_DIR/preparation-result.json" "$COMPARE_ARTIFACT_DIR/preparation-result.json" || {
    echo "error: independent Stage 3C preparation results differ" >&2; exit 1;
  }
  echo "PASS: independent clean Stage 3C generations are logically identical"
fi

git -C "$REPO_ROOT" diff --quiet "$BASELINE_COMMIT" -- \
  build.sh celestemeow fnalibs-ios-builder-celeste FNA native \
  global.json managed/celeste-analysis-policy.json managed/celeste-compatibility-ledger.json \
  managed/celeste-generation.lock.json managed/celeste-stage3b-policy.json managed/patches \
  scripts/fetch-tvos-deps.sh scripts/build-tvos-native.sh scripts/verify-tvos-native.sh \
  scripts/prepare-tvos-host-native.sh scripts/verify-tvos-host.sh \
  scripts/validate-celeste-input.sh scripts/prepare-celeste-managed.sh \
  scripts/build-celeste-managed.sh scripts/verify-celeste-managed.sh scripts/celeste-managed.py \
  scripts/prepare-celeste-tvos-runtime.sh scripts/celeste-stage3b.py \
  tvos/CelesteTvOSHost tvos/FNA.TvOS tvos/CelesteManagedAotClosure \
  tvos/stage2-ios-native-baseline.sha256 || {
    echo "error: existing iOS or Stage 1/2/3A/3B foundation changed outside the Stage 3C host extension" >&2; exit 1;
  }

if [[ -n "$APP_DIR" ]]; then
  app_args=(--runtime-root "$RUNTIME_ROOT/stage3b" --artifact-dir "$ARTIFACT_DIR/stage3b" --app "$APP_DIR" --platform "$PLATFORM")
  if [[ "$SKIP_TOOLCHAIN" -eq 1 ]]; then app_args+=(--skip-toolchain); fi
  "$REPO_ROOT/scripts/verify-celeste-tvos-runtime.sh" "${app_args[@]}" >/dev/null
  if find "$APP_DIR" -type f \( -iname 'save*.celeste' -o -iname 'settings.celeste' -o -iname '*.bank' -o -iname 'libfmod*' \) -print -quit | grep -q .; then
    echo "error: app contains save/settings/FM0D material" >&2; exit 1
  fi
  echo "PASS: $PLATFORM app isolation and native platform checks"
fi

python3 - "$REPO_ROOT" <<'PY'
import pathlib, re, subprocess, sys
root = pathlib.Path(sys.argv[1])
changed = subprocess.check_output(["git", "-C", str(root), "diff", "--name-only", "--diff-filter=ACMR", "HEAD", "--"], text=True).splitlines()
untracked = subprocess.check_output(["git", "-C", str(root), "ls-files", "--others", "--exclude-standard"], text=True).splitlines()
patterns = {
    "absolute home path": re.compile(rb"/" + rb"Users/[^/$\s]+/"),
    "email": re.compile(rb"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"),
    "UUID": re.compile(rb"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b"),
    "certificate fingerprint": re.compile(rb"\b[A-F0-9]{40}\b"),
    "team id": re.compile(rb"<DevelopmentTeam>\s*[A-Z0-9]{10}\s*</DevelopmentTeam>"),
}
for relative in sorted(set(changed + untracked)):
    path = root / relative
    if not path.is_file(): continue
    data = path.read_bytes()
    for label, pattern in patterns.items():
        if pattern.search(data): raise SystemExit(f"error: {label} in candidate tracked file: {relative}")
    if path.suffix.lower() in {".dll", ".exe", ".a", ".dylib", ".mobileprovision", ".xnb", ".celeste"}:
        raise SystemExit(f"error: generated/proprietary/signing file is a candidate: {relative}")
print("PASS: candidate tracked files contain no private paths, signing identifiers, or proprietary binaries")
PY

echo "Stage 3C static and generated-input verification passed."
