#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
ARTIFACT_DIR="$REPO_ROOT/artifacts/ios-native"
NATIVE_BUILD_DIR="$REPO_ROOT/.build/ios-native"
STAGE_DIR="$REPO_ROOT/.build/ios-host"
CLEAN=0

usage() {
  cat <<'EOF'
Usage: scripts/prepare-ios-foundation.sh [options]

Validate and stage the accepted iOS native XCFrameworks plus pinned FNA
managed bindings for the modern iOS foundation host.

Options:
  --artifact-dir DIR     iOS native artifacts (default: artifacts/ios-native)
  --native-build-dir DIR matching fetched native source root
  --stage-dir DIR        destination (default: .build/ios-host)
  --clean                replace only a marked stage directory
  -h, --help             show this help
EOF
}

while (($#)); do
  case "$1" in
    --artifact-dir) [[ $# -ge 2 ]] || exit 2; ARTIFACT_DIR="$2"; shift 2 ;;
    --native-build-dir) [[ $# -ge 2 ]] || exit 2; NATIVE_BUILD_DIR="$2"; shift 2 ;;
    --stage-dir) [[ $# -ge 2 ]] || exit 2; STAGE_DIR="$2"; shift 2 ;;
    --clean) CLEAN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done

for path_var in ARTIFACT_DIR NATIVE_BUILD_DIR STAGE_DIR; do
  value="${!path_var}"
  [[ "$value" == /* ]] || printf -v "$path_var" '%s/%s' "$REPO_ROOT" "$value"
done
[[ -f "$ARTIFACT_DIR/normalized-manifest.json" ]] || { echo "error: verify iOS native artifacts first" >&2; exit 1; }
[[ -d "$NATIVE_BUILD_DIR/sources" ]] || { echo "error: fetched iOS native sources are missing" >&2; exit 1; }

if [[ -e "$STAGE_DIR" ]]; then
  ((CLEAN)) || { echo "error: stage exists; use --clean" >&2; exit 1; }
  [[ -f "$STAGE_DIR/.ios-host-stage" ]] || { echo "error: refusing unmarked stage" >&2; exit 1; }
fi

temp="$(mktemp -d "$(dirname "$STAGE_DIR")/.ios-host.prepare.XXXXXX")"
cleanup() { [[ ! -e "$temp" ]] || find "$temp" -depth -delete; }
trap cleanup EXIT
mkdir -p "$temp/native" "$temp/managed"
touch "$temp/.ios-host-stage"

for component in SDL2 FNA3D FAudio Theorafile ApplePlatformStubs; do
  cp -R "$ARTIFACT_DIR/$component.xcframework" "$temp/native/"
done
cp "$NATIVE_BUILD_DIR/sources/SDL2-CS-bindings/src/SDL2.cs" "$temp/managed/SDL2.cs"
cp "$NATIVE_BUILD_DIR/sources/FAudio/csharp/FAudio.cs" "$temp/managed/FAudio.cs"
cp "$NATIVE_BUILD_DIR/sources/Theorafile/csharp/Theorafile.cs" "$temp/managed/Theorafile.cs"
cp "$REPO_ROOT/FNA/src/Graphics/FNA3D.cs" "$temp/managed/FNA3D.cs"
cp "$REPO_ROOT/FNA/src/FrameworkDispatcher.cs" "$temp/managed/FrameworkDispatcher.cs"

python3 - "$REPO_ROOT/tvos/fna-managed-sources.lock.json" "$temp/managed" <<'PY'
import hashlib, json, pathlib, sys
lock, root = json.loads(pathlib.Path(sys.argv[1]).read_text()), pathlib.Path(sys.argv[2])
for name, entry in lock["sources"].items():
    actual = hashlib.sha256((root / name).read_bytes()).hexdigest()
    if actual != entry["sha256"]: raise SystemExit(f"managed source lock mismatch: {name}")
PY
patch --batch --forward -p1 -d "$temp/managed" < "$REPO_ROOT/native/patches/FNA/0001-map-apple-static-imports-to-internal.patch"
patch --batch --forward -p1 -d "$temp/managed" < "$REPO_ROOT/tvos/patches/FNA/0002-disambiguate-mediaplayer-alias.patch"

python3 - "$temp/managed" <<'PY'
import pathlib, sys
root = pathlib.Path(sys.argv[1])
for name in ("SDL2.cs", "FNA3D.cs", "FAudio.cs", "Theorafile.cs"):
    if 'nativeLibName = "__Internal"' not in (root / name).read_text():
        raise SystemExit(f"static import mapping missing: {name}")
PY

cp "$ARTIFACT_DIR/normalized-manifest.json" "$temp/native-manifest.json"
python3 - "$temp/staging-manifest.json" "$REPO_ROOT" <<'PY'
import json, pathlib, subprocess, sys
root = pathlib.Path(sys.argv[2])
pathlib.Path(sys.argv[1]).write_text(json.dumps({
    "schemaVersion": 1,
    "fnaRevision": subprocess.check_output(["git", "-C", root / "FNA", "rev-parse", "HEAD"], text=True).strip(),
    "nativeImportMapping": "__Internal",
    "simulatorAudioPolicy": "explicit-no-fmod",
    "deviceAudioPolicy": "external-fmod-1.10.09-foundation-smoke",
}, indent=2, sort_keys=True) + "\n")
PY

[[ ! -e "$STAGE_DIR" ]] || find "$STAGE_DIR" -depth -delete
mv "$temp" "$STAGE_DIR"
trap - EXIT
echo "prepared modern iOS foundation inputs: $STAGE_DIR"
