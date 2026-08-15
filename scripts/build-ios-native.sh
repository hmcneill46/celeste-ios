#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
LOCK_FILE="$REPO_ROOT/native/ios-dependencies.lock.json"
BUILD_DIR="$REPO_ROOT/.build/ios-native"
OUTPUT_DIR="$REPO_ROOT/artifacts/ios-native"
DEPLOYMENT_TARGET="${IOS_DEPLOYMENT_TARGET:-15.0}"
CLEAN=0

usage() {
  cat <<'EOF'
Usage: scripts/build-ios-native.sh [options]

Build SDL2, FNA3D, FAudio, Theorafile, and ApplePlatformStubs for arm64
iPhone and arm64 Apple-silicon iOS Simulator. No MoltenVK is built.

Options:
  --build-dir DIR   Generated source/work root (default: .build/ios-native)
  --output-dir DIR  Generated XCFramework root (default: artifacts/ios-native)
  --clean           Replace only marked generated work and output
  -h, --help        Show this help
EOF
}

while (($#)); do
  case "$1" in
    --build-dir) [[ $# -ge 2 ]] || exit 2; BUILD_DIR="$2"; shift 2 ;;
    --output-dir) [[ $# -ge 2 ]] || exit 2; OUTPUT_DIR="$2"; shift 2 ;;
    --clean) CLEAN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ "$DEPLOYMENT_TARGET" == "15.0" ]] || { echo "error: the modern iOS foundation locks iOS 15.0" >&2; exit 1; }
for tool in git python3 xcodebuild xcrun; do
  command -v "$tool" >/dev/null || { echo "error: missing tool: $tool" >&2; exit 1; }
done
xcrun --sdk iphoneos --show-sdk-path >/dev/null
xcrun --sdk iphonesimulator --show-sdk-path >/dev/null

BUILD_DIR="$(python3 -c 'import os,sys; print(os.path.realpath(sys.argv[1]))' "$BUILD_DIR")"
OUTPUT_DIR="$(python3 -c 'import os,sys; print(os.path.realpath(sys.argv[1]))' "$OUTPUT_DIR")"
SOURCES_DIR="$BUILD_DIR/sources"
WORK_DIR="$BUILD_DIR/work"
STAGE_DIR="$BUILD_DIR/stage"
LOG_DIR="$BUILD_DIR/logs/build"
[[ -f "$BUILD_DIR/.ios-native-work" && -f "$BUILD_DIR/source-state.json" ]] || {
  echo "error: run scripts/fetch-ios-native-deps.sh first" >&2; exit 1;
}

if ((CLEAN)); then
  for path in "$WORK_DIR" "$STAGE_DIR" "$LOG_DIR" "$OUTPUT_DIR"; do
    [[ -z "$path" || "$path" == / ]] && { echo "error: unsafe clean path" >&2; exit 1; }
    [[ ! -e "$path" ]] || find "$path" -depth -delete
  done
fi
mkdir -p "$WORK_DIR/sources" "$STAGE_DIR" "$LOG_DIR" "$OUTPUT_DIR"
touch "$OUTPUT_DIR/.ios-native-output"
export ZERO_AR_DATE=1 NSUnbufferedIO=YES

python3 - "$LOCK_FILE" "$BUILD_DIR/source-state.json" "$SOURCES_DIR" <<'PY'
import json, pathlib, subprocess, sys
lock, state, sources = json.loads(pathlib.Path(sys.argv[1]).read_text()), json.loads(pathlib.Path(sys.argv[2]).read_text()), pathlib.Path(sys.argv[3])
expected = {x["name"]: (x["url"], x["revision"]) for x in lock["dependencies"]}
actual = {x["name"]: (x["origin"], x["revision"]) for x in state["dependencies"]}
if expected != actual: raise SystemExit("source state does not match iOS lock")
for dep in lock["dependencies"]:
    root = sources / dep["path"]
    if subprocess.check_output(["git", "-C", root, "rev-parse", "HEAD"], text=True).strip() != dep["revision"]:
        raise SystemExit(f"source revision drift: {dep['name']}")
    if subprocess.check_output(["git", "-C", root, "status", "--porcelain", "--untracked-files=all"], text=True).strip():
        raise SystemExit(f"source checkout dirty: {dep['name']}")
PY

# Copy immutable checkouts into disposable build work so locked patches never
# dirty the fetch cache. Nested pinned dependencies are already present.
if [[ ! -f "$WORK_DIR/.sources-ready" ]]; then
  find "$WORK_DIR/sources" -depth -delete 2>/dev/null || true
  mkdir -p "$WORK_DIR/sources"
  cp -R "$SOURCES_DIR"/. "$WORK_DIR/sources"/
  touch "$WORK_DIR/.sources-ready"
fi
PATCHED_SOURCES="$WORK_DIR/sources"

python3 - "$LOCK_FILE" "$REPO_ROOT" "$PATCHED_SOURCES" <<'PY'
import json, pathlib, subprocess, sys
lock, repo, sources = json.loads(pathlib.Path(sys.argv[1]).read_text()), pathlib.Path(sys.argv[2]), pathlib.Path(sys.argv[3])
for dep in lock["dependencies"]:
    root = sources / dep["path"]
    for relative in dep.get("patches", []):
        patch = repo / relative
        if subprocess.call(["git", "-C", root, "apply", "--check", patch], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) == 0:
            subprocess.check_call(["git", "-C", root, "apply", patch])
        elif subprocess.call(["git", "-C", root, "apply", "--reverse", "--check", patch], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) != 0:
            raise SystemExit(f"locked patch does not apply: {relative}")
PY

build_xcode() {
  local component="$1" project="$2" target="$3" product="$4" variant="$5" sdk="$6"
  local derived="$WORK_DIR/derived/$component/$variant"
  local products="$WORK_DIR/products/$component/$variant"
  local log="$LOG_DIR/$component-$variant.log"
  local definitions='$(inherited)'
  local source_map='$(inherited)'
  if [[ "$component" == SDL2 ]]; then
    definitions='$(inherited) SDL_MAIN_HANDLED=1'
    source_map="\$(inherited) -ffile-prefix-map=$PATCHED_SOURCES/SDL2=/IOS_NATIVE_SOURCES/SDL2"
  fi
  mkdir -p "$derived" "$products"
  echo "[$component/$variant] $target ($sdk, arm64, iOS $DEPLOYMENT_TARGET)"
  xcodebuild build -project "$project" -target "$target" -configuration Release -sdk "$sdk" \
    ARCHS=arm64 ONLY_ACTIVE_ARCH=NO IPHONEOS_DEPLOYMENT_TARGET="$DEPLOYMENT_TARGET" \
    CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO GCC_GENERATE_DEBUGGING_SYMBOLS=NO \
    DEBUG_INFORMATION_FORMAT= OBJROOT="$derived/obj" SYMROOT="$derived/sym" \
    CONFIGURATION_BUILD_DIR="$products" GCC_PREPROCESSOR_DEFINITIONS="$definitions" \
    OTHER_CFLAGS="$source_map" >"$log" 2>&1
  [[ -f "$products/$product" ]] || { echo "error: missing $component product" >&2; exit 1; }
  mkdir -p "$STAGE_DIR/$component/$variant"
  cp -p "$products/$product" "$STAGE_DIR/$component/$variant/lib$component.a"
}

build_pair() {
  local component="$1" project="$2" target="$3" product="$4"
  build_xcode "$component" "$project" "$target" "$product" device iphoneos
  build_xcode "$component" "$project" "$target" "$product" simulator iphonesimulator
}

build_pair SDL2 "$PATCHED_SOURCES/SDL2/Xcode/SDL/SDL.xcodeproj" "Static Library-iOS" libSDL2.a
build_pair FNA3D "$PATCHED_SOURCES/FNA3D/Xcode-iOS/FNA3D.xcodeproj" FNA3D libFNA3D.a
build_pair FAudio "$PATCHED_SOURCES/FAudio/Xcode-iOS/FAudio.xcodeproj" FAudio libFAudio.a
build_pair Theorafile "$PATCHED_SOURCES/Theorafile/Xcode-iOS/theorafile.xcodeproj" theorafile libtheorafile.a

for variant in device simulator; do
  sdk=iphoneos; target=arm64-apple-ios${DEPLOYMENT_TARGET}
  [[ "$variant" == simulator ]] && { sdk=iphonesimulator; target=arm64-apple-ios${DEPLOYMENT_TARGET}-simulator; }
  sdk_path="$(xcrun --sdk "$sdk" --show-sdk-path)"
  mkdir -p "$STAGE_DIR/ApplePlatformStubs/$variant"
  xcrun --sdk "$sdk" clang -target "$target" -isysroot "$sdk_path" -Os -fno-ident \
    -fvisibility=hidden -c "$REPO_ROOT/native/apple-platform-stubs/ApplePlatformStubs.c" \
    -o "$STAGE_DIR/ApplePlatformStubs/$variant/ApplePlatformStubs.o"
  ZERO_AR_DATE=1 xcrun --sdk "$sdk" ar rcs \
    "$STAGE_DIR/ApplePlatformStubs/$variant/libApplePlatformStubs.a" \
    "$STAGE_DIR/ApplePlatformStubs/$variant/ApplePlatformStubs.o"
done

copy_headers() {
  local component="$1"; shift
  local destination="$STAGE_DIR/$component/headers"
  mkdir -p "$destination"
  for source in "$@"; do
    [[ -d "$source" ]] && cp -R "$source"/. "$destination"/ || cp -p "$source" "$destination"/
  done
}
copy_headers SDL2 "$PATCHED_SOURCES/SDL2/include"
copy_headers FNA3D "$PATCHED_SOURCES/FNA3D/include"
copy_headers FAudio "$PATCHED_SOURCES/FAudio/include"
copy_headers Theorafile "$PATCHED_SOURCES/Theorafile/theorafile.h"
mkdir -p "$STAGE_DIR/Theorafile/headers/ogg" "$STAGE_DIR/Theorafile/headers/theora" "$STAGE_DIR/Theorafile/headers/vorbis"
cp -p "$PATCHED_SOURCES/Theorafile/lib/ogg"/*.h "$STAGE_DIR/Theorafile/headers/ogg/"
cp -p "$PATCHED_SOURCES/Theorafile/lib/theora/codec.h" "$PATCHED_SOURCES/Theorafile/lib/theora/theoradec.h" "$STAGE_DIR/Theorafile/headers/theora/"
cp -p "$PATCHED_SOURCES/Theorafile/lib/vorbis/codec.h" "$STAGE_DIR/Theorafile/headers/vorbis/"
copy_headers ApplePlatformStubs "$REPO_ROOT/native/apple-platform-stubs/ApplePlatformStubs.h"

for component in SDL2 FNA3D FAudio Theorafile ApplePlatformStubs; do
  destination="$OUTPUT_DIR/$component.xcframework"
  [[ ! -e "$destination" ]] || find "$destination" -depth -delete
  xcodebuild -create-xcframework \
    -library "$STAGE_DIR/$component/device/lib$component.a" -headers "$STAGE_DIR/$component/headers" \
    -library "$STAGE_DIR/$component/simulator/lib$component.a" -headers "$STAGE_DIR/$component/headers" \
    -output "$destination" >"$LOG_DIR/$component-xcframework.log" 2>&1
done

echo "iOS native build complete; run scripts/verify-ios-native.sh"
