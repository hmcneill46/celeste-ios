#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)"
LOCK_FILE="$REPO_ROOT/native/tvos-dependencies.lock.json"
BUILD_DIR="$REPO_ROOT/.build/tvos-native"
OUTPUT_DIR="$REPO_ROOT/artifacts/tvos-native"
DEPLOYMENT_TARGET="${TVOS_DEPLOYMENT_TARGET:-16.0}"
CLEAN=0

usage() {
  cat <<'EOF'
Usage: scripts/build-tvos-native.sh [options]

Build and package the six locked open-source Stage 1 components for tvOS and
tvOS Simulator. Sources must first be fetched with fetch-tvos-deps.sh.

Options:
  --build-dir DIR   Generated sources/intermediates directory
                    (default: .build/tvos-native)
  --output-dir DIR  Generated XCFramework output directory
                    (default: artifacts/tvos-native)
  --clean           Remove only generated work, products, logs, and output
  -h, --help        Show this help

Environment:
  TVOS_DEPLOYMENT_TARGET  Common minimum tvOS version (default: 16.0)

The script is noninteractive, performs no signing, and installs nothing.
EOF
}

while (($#)); do
  case "$1" in
    --build-dir)
      (($# >= 2)) || { echo "error: --build-dir requires a value" >&2; exit 2; }
      BUILD_DIR="$2"; shift 2 ;;
    --output-dir)
      (($# >= 2)) || { echo "error: --output-dir requires a value" >&2; exit 2; }
      OUTPUT_DIR="$2"; shift 2 ;;
    --clean)
      CLEAN=1; shift ;;
    -h|--help)
      usage; exit 0 ;;
    *)
      echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

for tool in git python3 xcodebuild xcrun gmake; do
  command -v "$tool" >/dev/null 2>&1 || { echo "error: required tool not found: $tool" >&2; exit 1; }
done
xcrun --sdk appletvos --show-sdk-path >/dev/null
xcrun --sdk appletvsimulator --show-sdk-path >/dev/null

BUILD_DIR="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$BUILD_DIR")"
OUTPUT_DIR="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$OUTPUT_DIR")"
SOURCES_DIR="$BUILD_DIR/sources"
WORK_DIR="$BUILD_DIR/work"
STAGE_DIR="$BUILD_DIR/stage"
LOG_DIR="$BUILD_DIR/logs"

[[ -f "$BUILD_DIR/source-state.json" ]] || {
  echo "error: missing $BUILD_DIR/source-state.json; run fetch-tvos-deps.sh first" >&2
  exit 1
}

if ((CLEAN)); then
  for path in "$WORK_DIR" "$STAGE_DIR" "$LOG_DIR/build"; do
    case "$path" in /|"") echo "error: refusing unsafe clean path: $path" >&2; exit 1 ;; esac
    rm -rf -- "$path"
  done
  for path in \
    "$OUTPUT_DIR/SDL2.xcframework" \
    "$OUTPUT_DIR/FNA3D.xcframework" \
    "$OUTPUT_DIR/FAudio.xcframework" \
    "$OUTPUT_DIR/Theorafile.xcframework" \
    "$OUTPUT_DIR/tvStubs.xcframework" \
    "$OUTPUT_DIR/MoltenVK.xcframework" \
    "$OUTPUT_DIR/verification" \
    "$OUTPUT_DIR/link-probes" \
    "$OUTPUT_DIR/licenses" \
    "$OUTPUT_DIR/licenses.json" \
    "$OUTPUT_DIR/manifest.json" \
    "$OUTPUT_DIR/normalized-manifest.json"; do
    rm -rf -- "$path"
  done
fi
mkdir -p -- "$WORK_DIR" "$STAGE_DIR" "$LOG_DIR/build" "$OUTPUT_DIR"
LOG_FILE="$LOG_DIR/build.log"
: >"$LOG_FILE"
exec > >(tee -a "$LOG_FILE") 2>&1

export ZERO_AR_DATE=1
export NSUnbufferedIO=YES
export GIT_TERMINAL_PROMPT=0

echo "Stage 1 native build"
echo "lock: $LOCK_FILE"
echo "sources: $SOURCES_DIR"
echo "work: $WORK_DIR"
echo "output: $OUTPUT_DIR"
echo "deployment target: tvOS $DEPLOYMENT_TARGET"
echo "Xcode: $(xcodebuild -version | tr '\n' ' ')"
echo "appletvos SDK: $(xcrun --sdk appletvos --show-sdk-version)"
echo "appletvsimulator SDK: $(xcrun --sdk appletvsimulator --show-sdk-version)"

python3 - "$LOCK_FILE" "$BUILD_DIR/source-state.json" "$SOURCES_DIR" <<'PY'
import json, pathlib, subprocess, sys
lock = json.load(open(sys.argv[1], encoding="utf-8"))
state = json.load(open(sys.argv[2], encoding="utf-8"))
expected = {d["name"]: (d["url"], d["revision"]) for d in lock["dependencies"]}
actual = {d["name"]: (d["origin"], d["revision"]) for d in state["dependencies"]}
if expected != actual:
    raise SystemExit("source-state.json does not match the dependency lock")
sources = pathlib.Path(sys.argv[3])
for dependency in lock["dependencies"]:
    checkout = sources / dependency["path"]
    head = subprocess.check_output(["git", "-C", checkout, "rev-parse", "HEAD"], text=True).strip()
    origin = subprocess.check_output(["git", "-C", checkout, "remote", "get-url", "origin"], text=True).strip()
    if (origin, head) != (dependency["url"], dependency["revision"]):
        raise SystemExit(f"checkout no longer matches lock: {dependency['name']}")
PY

write_xcconfig() {
  local destination="$1"
  {
    echo "TVOS_DEPLOYMENT_TARGET = $DEPLOYMENT_TARGET"
    echo 'ONLY_ACTIVE_ARCH = NO'
    echo 'CODE_SIGNING_ALLOWED = NO'
    echo 'CODE_SIGNING_REQUIRED = NO'
    echo 'GCC_GENERATE_DEBUGGING_SYMBOLS = NO'
    echo 'DEBUG_INFORMATION_FORMAT ='
  } >"$destination"
}

XCCONFIG="$WORK_DIR/stage1.xcconfig"
write_xcconfig "$XCCONFIG"
export XCODE_XCCONFIG_FILE="$XCCONFIG"

apply_locked_patch() {
  local source_name="$1"
  local patch_relative="$2"
  local source="$SOURCES_DIR/$source_name"
  local patch="$REPO_ROOT/$patch_relative"
  if git -C "$source" apply --check "$patch" 2>/dev/null; then
    echo "Applying $patch_relative to $source_name"
    git -C "$source" apply "$patch"
  elif git -C "$source" apply --reverse --check "$patch" 2>/dev/null; then
    echo "Patch already applied: $patch_relative"
  else
    echo "error: patch does not apply cleanly: $patch_relative" >&2
    exit 1
  fi
}

apply_locked_patch SDL2 native/patches/SDL2/0001-tvos-use-pregenerated-metal-shaders.patch
apply_locked_patch FNA3D native/patches/FNA3D/0001-add-sysrenderer-header-to-xcode-project.patch
apply_locked_patch FAudio native/patches/FAudio/0001-align-static-distance-curves-for-ld64.patch
apply_locked_patch MoltenVK native/patches/MoltenVK/0001-do-not-refetch-locked-glslang-dependencies.patch

python3 - "$LOCK_FILE" "$SOURCES_DIR" "$REPO_ROOT" <<'PY'
import json, pathlib, subprocess, sys
lock = json.load(open(sys.argv[1], encoding="utf-8"))
sources, repo = pathlib.Path(sys.argv[2]), pathlib.Path(sys.argv[3])
for dependency in lock["dependencies"]:
    checkout = sources / dependency["path"]
    patches = [repo / patch for patch in dependency.get("patches", [])]
    reversed_patches = []
    try:
        for patch in reversed(patches):
            subprocess.check_call(["git", "-C", checkout, "apply", "--reverse", patch])
            reversed_patches.append(patch)
        remaining = subprocess.check_output(
            ["git", "-C", checkout, "status", "--porcelain", "--untracked-files=all"], text=True
        ).strip()
    finally:
        for patch in reversed(reversed_patches):
            subprocess.check_call(["git", "-C", checkout, "apply", patch])
    if remaining:
        raise SystemExit(
            f"changes remain after reversing locked patches in {dependency['name']}: {remaining}"
        )
PY

list_and_require_target() {
  local component="$1" project="$2" target="$3"
  local output="$LOG_DIR/build/${component}-xcodebuild-list.json"
  echo "[$component] xcodebuild -list; require target '$target'"
  xcodebuild -list -json -project "$project" >"$output"
  python3 - "$output" "$target" <<'PY'
import json, sys
data = json.load(open(sys.argv[1], encoding="utf-8"))
targets = data.get("project", {}).get("targets", [])
if sys.argv[2] not in targets:
    raise SystemExit(f"required Xcode target absent: {sys.argv[2]} (found: {targets})")
PY
}

list_and_require_target SDL2 "$SOURCES_DIR/SDL2/Xcode/SDL/SDL.xcodeproj" "Static Library-tvOS"
list_and_require_target FNA3D "$SOURCES_DIR/FNA3D/Xcode-iOS/FNA3D.xcodeproj" "FNA3D-tv"
list_and_require_target FAudio "$SOURCES_DIR/FAudio/Xcode-iOS/FAudio.xcodeproj" "FAudio-tv"
list_and_require_target Theorafile "$SOURCES_DIR/Theorafile/Xcode-iOS/theorafile.xcodeproj" "theorafile-tv"
list_and_require_target tvStubs "$SOURCES_DIR/NativeBuilder/tvStubs/tvStubs.xcodeproj" "tvStubs"

echo "[MoltenVK] xcodebuild -list; require pinned tvOS packaging and external schemes"
xcodebuild -list -json -project "$SOURCES_DIR/MoltenVK/MoltenVKPackaging.xcodeproj" \
  >"$LOG_DIR/build/MoltenVK-packaging-xcodebuild-list.json"
xcodebuild -list -json -project "$SOURCES_DIR/MoltenVK/ExternalDependencies.xcodeproj" \
  >"$LOG_DIR/build/MoltenVK-external-xcodebuild-list.json"
python3 - \
  "$LOG_DIR/build/MoltenVK-packaging-xcodebuild-list.json" \
  "$LOG_DIR/build/MoltenVK-external-xcodebuild-list.json" <<'PY'
import json, sys
checks = (
    (sys.argv[1], "MoltenVK Package (tvOS only)"),
    (sys.argv[2], "ExternalDependencies-tvOS"),
)
for path, expected in checks:
    schemes = json.load(open(path, encoding="utf-8")).get("project", {}).get("schemes", [])
    if expected not in schemes:
        raise SystemExit(f"required MoltenVK scheme absent: {expected} (found: {schemes})")
PY

build_xcode_archive() {
  local component="$1" project="$2" target="$3" product="$4" variant="$5" sdk="$6" archs="$7"
  local derived="$WORK_DIR/derived/$component/$variant"
  local products="$WORK_DIR/products/$component/$variant"
  local log="$LOG_DIR/build/${component}-${variant}.log"
  local module_cache="$derived/module-cache"
  local definitions='$(inherited)'
  local cflags='$(inherited)'
  if [[ "$component" == SDL2 ]]; then
    definitions='$(inherited) SDL_MAIN_HANDLED=1'
    cflags="\$(inherited) -ffile-prefix-map=$SOURCES_DIR/SDL2=/STAGE1_SOURCES/SDL2"
  fi
  mkdir -p -- "$derived" "$products" "$module_cache"
  echo "[$component/$variant] target=$target sdk=$sdk archs=$archs revision=$(git -C "$(dirname "$(dirname "$project")")" rev-parse HEAD 2>/dev/null || echo locked-nested-project)"
  set -x
  xcodebuild build \
    -project "$project" \
    -target "$target" \
    -configuration Release \
    -sdk "$sdk" \
    ARCHS="$archs" \
    ONLY_ACTIVE_ARCH=NO \
    TVOS_DEPLOYMENT_TARGET="$DEPLOYMENT_TARGET" \
    CODE_SIGNING_ALLOWED=NO \
    CODE_SIGNING_REQUIRED=NO \
    OBJROOT="$derived/obj" \
    SYMROOT="$derived/sym" \
    SHARED_PRECOMPS_DIR="$derived/precompiled" \
    CLANG_MODULE_CACHE_PATH="$module_cache" \
    MODULE_CACHE_DIR="$module_cache" \
    CONFIGURATION_BUILD_DIR="$products" \
    GCC_PREPROCESSOR_DEFINITIONS="$definitions" \
    OTHER_CFLAGS="$cflags" \
    >"$log" 2>&1
  set +x
  [[ -f "$products/$product" ]] || { echo "error: expected product missing: $products/$product" >&2; exit 1; }
  mkdir -p -- "$STAGE_DIR/$component/$variant"
  cp -p -- "$products/$product" "$STAGE_DIR/$component/$variant/lib$component.a"
}

build_pair() {
  local component="$1" project="$2" target="$3" product="$4"
  build_xcode_archive "$component" "$project" "$target" "$product" device appletvos arm64
  build_xcode_archive "$component" "$project" "$target" "$product" simulator appletvsimulator "arm64 x86_64"
}

build_pair SDL2 "$SOURCES_DIR/SDL2/Xcode/SDL/SDL.xcodeproj" "Static Library-tvOS" libSDL2.a
build_pair FNA3D "$SOURCES_DIR/FNA3D/Xcode-iOS/FNA3D.xcodeproj" "FNA3D-tv" libFNA3D.a
build_pair FAudio "$SOURCES_DIR/FAudio/Xcode-iOS/FAudio.xcodeproj" "FAudio-tv" libFAudio-tv.a
build_pair Theorafile "$SOURCES_DIR/Theorafile/Xcode-iOS/theorafile.xcodeproj" "theorafile-tv" libtheorafile-tv.a
build_pair tvStubs "$SOURCES_DIR/NativeBuilder/tvStubs/tvStubs.xcodeproj" "tvStubs" libtvStubs.a

echo "[MoltenVK] build exact locked external graph for tvOS and tvOS Simulator"
(
  cd "$SOURCES_DIR/MoltenVK"
  set -x
  ./fetchDependencies --tvos --tvossim --no-parallel-build --keep-cache \
    >"$LOG_DIR/build/MoltenVK-dependencies.log" 2>&1
  MVK_DERIVED_DATA_PATH="$WORK_DIR/derived/MoltenVK/device" \
    gmake tvos >"$LOG_DIR/build/MoltenVK-device.log" 2>&1
  MVK_DERIVED_DATA_PATH="$WORK_DIR/derived/MoltenVK/simulator" \
    gmake tvossim >"$LOG_DIR/build/MoltenVK-simulator.log" 2>&1
)

find_molten_archive() {
  local variant="$1" archive
  if [[ "$variant" == device ]]; then
    archive="$WORK_DIR/derived/MoltenVK/device/Build/Products/Release-appletvos/libMoltenVK.a"
  else
    archive="$WORK_DIR/derived/MoltenVK/simulator/Build/Products/Release-appletvsimulator/libMoltenVK.a"
  fi
  [[ -f "$archive" ]] || {
    echo "error: expected MoltenVK $variant archive missing: $archive" >&2
    exit 1
  }
  printf '%s\n' "$archive"
}

for variant in device simulator; do
  molten_archive="$(find_molten_archive "$variant")"
  mkdir -p -- "$STAGE_DIR/MoltenVK/$variant"
  cp -p -- "$molten_archive" "$STAGE_DIR/MoltenVK/$variant/libMoltenVK.a"
done

copy_headers() {
  local component="$1"
  local destination="$STAGE_DIR/$component/headers"
  mkdir -p -- "$destination"
  shift
  while (($#)); do
    local source="$1"
    shift
    if [[ -d "$source" ]]; then
      cp -R "$source"/. "$destination"/
    else
      cp -p "$source" "$destination"/
    fi
  done
}

copy_headers SDL2 "$SOURCES_DIR/SDL2/include"
copy_headers FNA3D "$SOURCES_DIR/FNA3D/include"
copy_headers FAudio "$SOURCES_DIR/FAudio/include"
copy_headers Theorafile "$SOURCES_DIR/Theorafile/theorafile.h"
mkdir -p -- \
  "$STAGE_DIR/Theorafile/headers/ogg" \
  "$STAGE_DIR/Theorafile/headers/theora" \
  "$STAGE_DIR/Theorafile/headers/vorbis"
cp -p -- "$SOURCES_DIR/Theorafile/lib/ogg"/*.h "$STAGE_DIR/Theorafile/headers/ogg/"
cp -p -- \
  "$SOURCES_DIR/Theorafile/lib/theora/codec.h" \
  "$SOURCES_DIR/Theorafile/lib/theora/theoradec.h" \
  "$STAGE_DIR/Theorafile/headers/theora/"
cp -p -- "$SOURCES_DIR/Theorafile/lib/vorbis/codec.h" "$STAGE_DIR/Theorafile/headers/vorbis/"
copy_headers tvStubs "$REPO_ROOT/native/tvstubs/tvStubs.h"
copy_headers MoltenVK "$SOURCES_DIR/MoltenVK/External/Vulkan-Headers/include"
mkdir -p -- "$STAGE_DIR/MoltenVK/headers/MoltenVK"
cp -p -- "$SOURCES_DIR/MoltenVK/MoltenVK/MoltenVK/API"/*.h "$STAGE_DIR/MoltenVK/headers/MoltenVK/"

for component in SDL2 FNA3D FAudio Theorafile tvStubs MoltenVK; do
  destination="$OUTPUT_DIR/$component.xcframework"
  rm -rf -- "$destination"
  echo "[$component] create tvOS-only XCFramework"
  set -x
  xcodebuild -create-xcframework \
    -library "$STAGE_DIR/$component/device/lib$component.a" -headers "$STAGE_DIR/$component/headers" \
    -library "$STAGE_DIR/$component/simulator/lib$component.a" -headers "$STAGE_DIR/$component/headers" \
    -output "$destination" \
    >"$LOG_DIR/build/${component}-xcframework.log" 2>&1
  set +x
done

python3 "$SCRIPT_DIR/generate-tvos-symbol-expectations.py" \
  --sources-dir "$SOURCES_DIR" \
  --output "$BUILD_DIR/generated-symbol-expectations.json"

echo "Build and packaging complete. Verification is a separate required gate:"
echo "  $SCRIPT_DIR/verify-tvos-native.sh --build-dir '$BUILD_DIR' --output-dir '$OUTPUT_DIR'"
