#!/usr/bin/env bash
set -euo pipefail

readonly EXPECTED_LOGICAL_SHA256="61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39"
readonly EXPECTED_DEPLOYMENT_TARGET="16.0"
readonly COMPONENTS=(SDL2 FNA3D FAudio Theorafile tvStubs MoltenVK)

usage() {
  cat <<'USAGE'
Usage: scripts/prepare-tvos-host-native.sh [options]

Validate and stage the accepted Stage 1 XCFrameworks and pinned FNA managed
bindings for the modern Stage 2 tvOS host. The script installs nothing and
writes only to the selected ignored staging directory.

Options:
  --artifact-dir DIR    Stage 1 acceptance artifact directory. If omitted,
                        rebuild-e is selected only when rebuild-e and rebuild-f
                        exist and carry the same accepted logical checksum.
  --stage1-build-dir DIR
                        Matching Stage 1 build directory containing clean
                        source checkouts. Defaults by artifact directory name.
  --stage-dir DIR       Destination (default: .build/tvos-host).
  --clean               Replace an existing staging directory. Refuses to
                        remove a directory without this script's marker.
  -h, --help            Show this help.

Relative paths are resolved from the repository root, not the caller's current
directory. The accepted deployment target is tvOS 16.0.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
ARTIFACT_DIR=""
STAGE1_BUILD_DIR=""
STAGE_DIR="$REPO_ROOT/.build/tvos-host"
CLEAN=0

repo_path() {
  case "$1" in
    /*) printf '%s\n' "$1" ;;
    *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;;
  esac
}

while (($#)); do
  case "$1" in
    --artifact-dir)
      [[ $# -ge 2 ]] || { echo "error: --artifact-dir requires a value" >&2; exit 2; }
      ARTIFACT_DIR="$(repo_path "$2")"
      shift 2
      ;;
    --stage1-build-dir)
      [[ $# -ge 2 ]] || { echo "error: --stage1-build-dir requires a value" >&2; exit 2; }
      STAGE1_BUILD_DIR="$(repo_path "$2")"
      shift 2
      ;;
    --stage-dir)
      [[ $# -ge 2 ]] || { echo "error: --stage-dir requires a value" >&2; exit 2; }
      STAGE_DIR="$(repo_path "$2")"
      shift 2
      ;;
    --clean)
      CLEAN=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "error: unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "$ARTIFACT_DIR" ]]; then
  REBUILD_E="$REPO_ROOT/artifacts/tvos-native/rebuild-e"
  REBUILD_F="$REPO_ROOT/artifacts/tvos-native/rebuild-f"
  for candidate in "$REBUILD_E" "$REBUILD_F"; do
    [[ -f "$candidate/normalized-manifest.json" ]] || {
      echo "error: default Stage 1 acceptance set is incomplete; pass --artifact-dir" >&2
      exit 1
    }
  done
  python3 - "$REBUILD_E/normalized-manifest.json" "$REBUILD_F/normalized-manifest.json" "$EXPECTED_LOGICAL_SHA256" <<'PY'
import json, pathlib, sys
a, b, expected = (pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), sys.argv[3])
ma, mb = json.loads(a.read_text()), json.loads(b.read_text())
if ma.get("logicalSetSha256") != expected or mb.get("logicalSetSha256") != expected:
    raise SystemExit("error: default Stage 1 directories do not carry the accepted logical checksum")
if ma != mb:
    raise SystemExit("error: rebuild-e and rebuild-f manifests differ; pass --artifact-dir explicitly")
PY
  ARTIFACT_DIR="$REBUILD_E"
fi

[[ -f "$ARTIFACT_DIR/normalized-manifest.json" ]] || {
  echo "error: normalized-manifest.json is missing from $ARTIFACT_DIR" >&2
  exit 1
}

if [[ -z "$STAGE1_BUILD_DIR" ]]; then
  STAGE1_BUILD_DIR="$REPO_ROOT/.build/tvos-native/$(basename -- "$ARTIFACT_DIR")"
fi

[[ -d "$STAGE1_BUILD_DIR/sources" ]] || {
  echo "error: matching Stage 1 source directory is missing: $STAGE1_BUILD_DIR/sources" >&2
  echo "       pass --stage1-build-dir for the acceptance artifact's verified build tree" >&2
  exit 1
}

python3 - "$ARTIFACT_DIR" "$EXPECTED_LOGICAL_SHA256" "$EXPECTED_DEPLOYMENT_TARGET" "${COMPONENTS[@]}" <<'PY'
import json, pathlib, plistlib, sys
root = pathlib.Path(sys.argv[1])
expected_hash, expected_target = sys.argv[2], sys.argv[3]
components = sys.argv[4:]
manifest = json.loads((root / "normalized-manifest.json").read_text())
if manifest.get("logicalSetSha256") != expected_hash:
    raise SystemExit(f"error: rejected logical checksum {manifest.get('logicalSetSha256')!r}")
if manifest.get("deploymentTarget") != expected_target:
    raise SystemExit(f"error: rejected deployment target {manifest.get('deploymentTarget')!r}")
if set(manifest.get("components", {})) != set(components):
    raise SystemExit("error: normalized manifest does not contain exactly the six Stage 1 components")
for name in components:
    xcframework = root / f"{name}.xcframework"
    info = xcframework / "Info.plist"
    if not info.is_file():
        raise SystemExit(f"error: missing {info}")
    with info.open("rb") as stream:
        plist = plistlib.load(stream)
    libraries = plist.get("AvailableLibraries", [])
    devices = [x for x in libraries if x.get("SupportedPlatform") == "tvos" and "SupportedPlatformVariant" not in x]
    simulators = [x for x in libraries if x.get("SupportedPlatform") == "tvos" and x.get("SupportedPlatformVariant") == "simulator"]
    if len(devices) != 1 or len(simulators) != 1 or len(libraries) != 2:
        raise SystemExit(f"error: {name} must contain exactly tvOS device and tvOS simulator variants")
    if "arm64" not in devices[0].get("SupportedArchitectures", []):
        raise SystemExit(f"error: {name} has no tvOS device arm64 slice")
    if "arm64" not in simulators[0].get("SupportedArchitectures", []):
        raise SystemExit(f"error: {name} has no tvOS Simulator arm64 slice")
print(f"accepted Stage 1 logical SHA-256: {expected_hash}")
PY

FNA_REVISION="$(git -C "$REPO_ROOT/FNA" rev-parse HEAD)"
[[ "$FNA_REVISION" == "d52b4ce61e4086b785c51a96d331dbf106975a58" ]] || {
  echo "error: FNA submodule revision is $FNA_REVISION, expected d52b4ce61e4086b785c51a96d331dbf106975a58" >&2
  exit 1
}

if [[ -e "$STAGE_DIR" ]]; then
  [[ "$CLEAN" -eq 1 ]] || {
    echo "error: staging directory exists; rerun with --clean: $STAGE_DIR" >&2
    exit 1
  }
  [[ -f "$STAGE_DIR/.stage2-host-staging" ]] || {
    echo "error: refusing to replace unmarked directory: $STAGE_DIR" >&2
    exit 1
  }
fi

mkdir -p "$(dirname -- "$STAGE_DIR")"
TEMP_STAGE="$(mktemp -d "$(dirname -- "$STAGE_DIR")/.tvos-host.prepare.XXXXXX")"
cleanup() {
  rm -rf -- "$TEMP_STAGE"
}
trap cleanup EXIT
mkdir -p "$TEMP_STAGE/native" "$TEMP_STAGE/managed"
touch "$TEMP_STAGE/.stage2-host-staging"

for component in "${COMPONENTS[@]}"; do
  echo "+ copy $component.xcframework"
  cp -R "$ARTIFACT_DIR/$component.xcframework" "$TEMP_STAGE/native/$component.xcframework"
done

cp "$STAGE1_BUILD_DIR/sources/SDL2-CS-bindings/src/SDL2.cs" "$TEMP_STAGE/managed/SDL2.cs"
cp "$STAGE1_BUILD_DIR/sources/FAudio/csharp/FAudio.cs" "$TEMP_STAGE/managed/FAudio.cs"
cp "$STAGE1_BUILD_DIR/sources/Theorafile/csharp/Theorafile.cs" "$TEMP_STAGE/managed/Theorafile.cs"
cp "$REPO_ROOT/FNA/src/Graphics/FNA3D.cs" "$TEMP_STAGE/managed/FNA3D.cs"
cp "$REPO_ROOT/FNA/src/FrameworkDispatcher.cs" "$TEMP_STAGE/managed/FrameworkDispatcher.cs"

python3 - "$REPO_ROOT/tvos/fna-managed-sources.lock.json" "$TEMP_STAGE/managed" <<'PY'
import hashlib, json, pathlib, sys
lock = json.loads(pathlib.Path(sys.argv[1]).read_text())
root = pathlib.Path(sys.argv[2])
for name, entry in lock["sources"].items():
    actual = hashlib.sha256((root / name).read_bytes()).hexdigest()
    if actual != entry["sha256"]:
        raise SystemExit(f"error: {name} SHA-256 {actual} does not match the managed-source lock")
    print(f"managed source {name}: {actual}")
PY

echo "+ patch --batch --forward -p1 -d <stage>/managed"
patch --batch --forward -p1 -d "$TEMP_STAGE/managed" \
  < "$REPO_ROOT/tvos/patches/FNA/0001-map-static-native-imports-to-internal.patch"
patch --batch --forward -p1 -d "$TEMP_STAGE/managed" \
  < "$REPO_ROOT/tvos/patches/FNA/0002-disambiguate-mediaplayer-alias.patch"

python3 - "$TEMP_STAGE/managed" <<'PY'
import pathlib, sys
root = pathlib.Path(sys.argv[1])
for name in ("SDL2.cs", "FNA3D.cs", "FAudio.cs", "Theorafile.cs"):
    text = (root / name).read_text()
    if 'nativeLibName = "__Internal"' not in text:
        raise SystemExit(f"error: static import mapping was not applied to {name}")
framework_dispatcher = (root / "FrameworkDispatcher.cs").read_text()
if "FNAMediaPlayer" not in framework_dispatcher or "using MediaPlayer =" in framework_dispatcher:
    raise SystemExit("error: modern tvOS MediaPlayer alias disambiguation was not applied")
PY

cp "$ARTIFACT_DIR/normalized-manifest.json" "$TEMP_STAGE/normalized-manifest.json"
python3 - "$TEMP_STAGE/staging-manifest.json" "$ARTIFACT_DIR" "$STAGE1_BUILD_DIR" "$FNA_REVISION" <<'PY'
import json, pathlib, sys
path = pathlib.Path(sys.argv[1])
path.write_text(json.dumps({
    "schemaVersion": 1,
    "stage1ArtifactDirectory": sys.argv[2],
    "stage1BuildDirectory": sys.argv[3],
    "fnaRevision": sys.argv[4],
    "nativeImportMapping": "__Internal",
    "tvStubsPolicy": "linked only to satisfy pinned cross-platform bindings; no Stage 2 call sites",
}, indent=2, sort_keys=True) + "\n")
PY

if [[ -e "$STAGE_DIR" ]]; then
  rm -rf -- "$STAGE_DIR"
fi
mv "$TEMP_STAGE" "$STAGE_DIR"
trap - EXIT

echo "prepared Stage 2 native inputs: $STAGE_DIR"
echo "FNA revision: $FNA_REVISION"
echo "deployment target: tvOS $EXPECTED_DEPLOYMENT_TARGET"
