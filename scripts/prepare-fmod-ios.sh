#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
LOCK_FILE="$REPO_ROOT/native/fmod-ios-dependencies.lock.json"
SDK_ROOT=""
STAGE_DIR="$REPO_ROOT/.build/ios-host/fmod"
NATIVE_DIR="$REPO_ROOT/artifacts/ios-native"
CLEAN=0

usage() {
  cat <<'EOF'
Usage: scripts/prepare-fmod-ios.sh --sdk-root DIR [options]

Validate and stage the user-supplied FMOD Engine iOS/tvOS 1.10.09 build 97915
arm64 iPhone libraries for the modern iOS foundation host. The original SDK
is read-only and no proprietary file enters Git.

Options:
  --sdk-root DIR     SDK root containing doc/ and api/ (required)
  --stage-dir DIR    ignored output (default: .build/ios-host/fmod)
  --native-dir DIR   verified iOS native artifacts (default: artifacts/ios-native)
  --clean            replace only a marked prior stage
  -h, --help         show this help

The old FMOD package has no arm64 iOS Simulator slice. Simulator builds use an
explicit no-FMOD policy; this script stages physical-device libraries only.
EOF
}

while (($#)); do
  case "$1" in
    --sdk-root) [[ $# -ge 2 ]] || exit 2; SDK_ROOT="$2"; shift 2 ;;
    --stage-dir) [[ $# -ge 2 ]] || exit 2; STAGE_DIR="$2"; shift 2 ;;
    --native-dir) [[ $# -ge 2 ]] || exit 2; NATIVE_DIR="$2"; shift 2 ;;
    --clean) CLEAN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done

[[ -n "$SDK_ROOT" ]] || { echo "error: --sdk-root is required" >&2; exit 2; }
for variable in SDK_ROOT STAGE_DIR NATIVE_DIR; do
  value="${!variable}"
  [[ "$value" == /* ]] || printf -v "$variable" '%s/%s' "$REPO_ROOT" "$value"
done
for tool in python3 shasum xcrun xcodebuild nm nmedit; do
  command -v "$tool" >/dev/null || { echo "error: missing tool: $tool" >&2; exit 1; }
done

revision="$SDK_ROOT/doc/revision.txt"
common_header="$SDK_ROOT/api/lowlevel/inc/fmod_common.h"
low_header="$SDK_ROOT/api/lowlevel/inc"
studio_header="$SDK_ROOT/api/studio/inc"
low_archive="$SDK_ROOT/api/lowlevel/lib/libfmod_iphoneos.a"
studio_archive="$SDK_ROOT/api/studio/lib/libfmodstudio_iphoneos.a"
for file in "$revision" "$common_header" "$low_archive" "$studio_archive"; do
  [[ -f "$file" && ! -L "$file" ]] || { echo "error: required FMOD SDK input is missing" >&2; exit 1; }
done
grep -Eq '1\.10\.09.*build 97915' "$revision" || { echo "error: FMOD release/build mismatch" >&2; exit 1; }
grep -Eq '^#define[[:space:]]+FMOD_VERSION[[:space:]]+0x00011009' "$common_header" || {
  echo "error: FMOD header version mismatch" >&2; exit 1;
}

python3 - "$LOCK_FILE" "$low_archive" "$studio_archive" <<'PY'
import hashlib, json, pathlib, sys
lock = json.loads(pathlib.Path(sys.argv[1]).read_text())
for label, path in (("lowLevel", pathlib.Path(sys.argv[2])), ("studio", pathlib.Path(sys.argv[3]))):
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    if actual != lock["deviceArchives"][label]["sha256"]:
        raise SystemExit(f"error: exact FMOD {label} archive fingerprint mismatch")
PY

[[ "$(xcrun lipo -archs "$low_archive")" == *arm64* ]] || { echo "error: FMOD low-level lacks arm64" >&2; exit 1; }
[[ "$(xcrun lipo -archs "$studio_archive")" == *arm64* ]] || { echo "error: FMOD Studio lacks arm64" >&2; exit 1; }

theorafile_archive="$(find "$NATIVE_DIR/Theorafile.xcframework" -type f -name 'libTheorafile.a' -path '*ios-arm64*' -print -quit)"
[[ -n "$theorafile_archive" && -f "$theorafile_archive" ]] || {
  echo "error: verified iOS Theorafile device archive is missing" >&2; exit 1;
}

if [[ -e "$STAGE_DIR" ]]; then
  ((CLEAN)) || { echo "error: stage exists; use --clean" >&2; exit 1; }
  [[ -f "$STAGE_DIR/.ios-fmod-stage" ]] || { echo "error: refusing unmarked stage" >&2; exit 1; }
fi
mkdir -p "$(dirname "$STAGE_DIR")"
temporary="$(mktemp -d "$(dirname "$STAGE_DIR")/.ios-fmod.prepare.XXXXXX")"
cleanup() { [[ ! -e "$temporary" ]] || find "$temporary" -depth -delete; }
trap cleanup EXIT
touch "$temporary/.ios-fmod-stage"
mkdir -p "$temporary/build/low-extract" "$temporary/headers/low" "$temporary/headers/studio"

xcrun lipo "$low_archive" -thin arm64 -output "$temporary/build/libfmod_iphoneos-arm64.a"
xcrun lipo "$studio_archive" -thin arm64 -output "$temporary/build/libfmodstudio_iphoneos-arm64.a"

LC_ALL=C comm -12 \
  <(nm -gjU "$temporary/build/libfmod_iphoneos-arm64.a" | sed '/:$/d; /^$/d' | LC_ALL=C sort -u) \
  <(nm -gjU "$theorafile_archive" | sed '/:$/d; /^$/d' | LC_ALL=C sort -u) \
  > "$temporary/build/duplicate-symbols.txt"
grep -v '^#' "$REPO_ROOT/native/fmod-tvos/localized-symbols.txt" | sed '/^[[:space:]]*$/d' \
  > "$temporary/build/expected-duplicate-symbols.txt"
cmp -s "$temporary/build/duplicate-symbols.txt" "$temporary/build/expected-duplicate-symbols.txt" || {
  echo "error: iPhone FMOD/Theorafile duplicate-symbol set differs from reviewed policy" >&2; exit 1;
}

(cd "$temporary/build/low-extract" && xcrun ar -x "$temporary/build/libfmod_iphoneos-arm64.a")
master_object="$(find "$temporary/build/low-extract" -maxdepth 1 -type f ! -name '__.SYMDEF*' -print -quit)"
[[ -n "$master_object" ]] || { echo "error: FMOD device archive has no object" >&2; exit 1; }
xcrun nmedit -R "$temporary/build/expected-duplicate-symbols.txt" \
  -o "$temporary/build/FMOD-localized.o" "$master_object"
ZERO_AR_DATE=1 xcrun ar rcs "$temporary/build/libfmod_iphoneos-localized.a" "$temporary/build/FMOD-localized.o"

remaining="$(LC_ALL=C comm -12 \
  <(nm -gjU "$temporary/build/libfmod_iphoneos-localized.a" | sed '/:$/d; /^$/d' | LC_ALL=C sort -u) \
  <(nm -gjU "$theorafile_archive" | sed '/:$/d; /^$/d' | LC_ALL=C sort -u))"
[[ -z "$remaining" ]] || { echo "error: localized FMOD still shadows Theorafile" >&2; exit 1; }
localized_exports="$(nm -gjU "$temporary/build/libfmod_iphoneos-localized.a")"
for symbol in _FMOD_System_Create _FMOD_System_GetVersion _FMOD_System_Init _FMOD_System_Release; do
  grep -Fxq "$symbol" <<<"$localized_exports" || {
    echo "error: localized FMOD lost $symbol" >&2; exit 1;
  }
done

cp -R "$low_header"/. "$temporary/headers/low/"
cp -R "$studio_header"/. "$temporary/headers/studio/"
xcodebuild -create-xcframework \
  -library "$temporary/build/libfmod_iphoneos-localized.a" -headers "$temporary/headers/low" \
  -output "$temporary/FMOD.xcframework" >/dev/null
xcodebuild -create-xcframework \
  -library "$temporary/build/libfmodstudio_iphoneos-arm64.a" -headers "$temporary/headers/studio" \
  -output "$temporary/FMODStudio.xcframework" >/dev/null

python3 - "$temporary" "$LOCK_FILE" <<'PY'
import hashlib, json, pathlib, plistlib, sys
root, lock_path = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
lock = json.loads(lock_path.read_text())
result = {
    "schemaVersion": 1,
    "source": "$FMOD_IOS_SDK_ROOT",
    "release": lock["version"],
    "build": lock["build"],
    "headerVersion": lock["headerVersion"],
    "platform": "iOS device",
    "architectures": ["arm64"],
    "simulatorPolicy": lock["simulatorPolicy"],
    "xcframeworks": {},
}
for name in ("FMOD", "FMODStudio"):
    xcf = root / f"{name}.xcframework"
    with (xcf / "Info.plist").open("rb") as stream:
        info = plistlib.load(stream)
    libraries = info["AvailableLibraries"]
    if len(libraries) != 1 or libraries[0].get("SupportedPlatform") != "ios" or libraries[0].get("SupportedPlatformVariant"):
        raise SystemExit(f"error: {name} must be device iOS only")
    if libraries[0].get("SupportedArchitectures") != ["arm64"]:
        raise SystemExit(f"error: {name} must be arm64 only")
    binary = xcf / libraries[0]["LibraryIdentifier"] / libraries[0]["LibraryPath"]
    result["xcframeworks"][name] = {
        "binarySha256": hashlib.sha256(binary.read_bytes()).hexdigest(),
        "libraryIdentifier": libraries[0]["LibraryIdentifier"],
    }
(root / "fmod-ios-manifest.json").write_text(json.dumps(result, indent=2, sort_keys=True) + "\n")
PY

[[ ! -e "$STAGE_DIR" ]] || find "$STAGE_DIR" -depth -delete
mv "$temporary" "$STAGE_DIR"
trap - EXIT
echo "prepared physical-device FMOD iOS foundation: $STAGE_DIR"
