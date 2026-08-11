#!/usr/bin/env bash
set -euo pipefail

readonly EXPECTED_STAGE1_HASH="6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
readonly FMOD_SDL_URL="https://github.com/flibitijibibo/FMOD_SDL.git"
readonly FMOD_SDL_COMMIT="947df759501d9f5a7df702101c453efc3e06ba22"
readonly DEPLOYMENT_TARGET="${TVOS_DEPLOYMENT_TARGET:-16.0}"

usage() {
  cat <<'USAGE'
Usage: scripts/prepare-fmod-tvos.sh --sdk-root DIR --game-root DIR [options]

Validate and stage the external FMOD Engine 1.10.09 tvOS SDK and user-owned
Celeste FMOD banks, fetch the immutable zlib-licensed FMOD_SDL bridge source,
and build arm64 tvOS static XCFramework inputs for the Stage 5A diagnostic.
The script installs nothing and never modifies the SDK or game installation.

Options:
  --sdk-root DIR          FMOD SDK root containing doc/ and api/ (required).
  --game-root DIR         Exact supported Celeste 1.4.0.0 root (required).
  --stage-dir DIR         Ignored output (default: .build/fmod-tvos/current).
  --source-dir DIR        Ignored immutable bridge checkout
                          (default: .build/fmod-tvos/sources/FMOD_SDL).
  --stage1-artifact-dir DIR
                          Accepted Stage 1 artifact set
                          (default: artifacts/tvos-native/rebuild-e).
  --clean                 Replace a previously marked stage directory.
  -h, --help              Show this help.

TVOS_DEPLOYMENT_TARGET may override the default 16.0, but Stage 5A verification
accepts only 16.0 so all diagnostic components share the established baseline.
All generated sources, proprietary inputs, binaries, manifests, and logs stay
below ignored .build/fmod-tvos or artifacts/fmod-tvos directories.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
SDK_ROOT=""
GAME_ROOT=""
STAGE_DIR="$REPO_ROOT/.build/fmod-tvos/current"
SOURCE_DIR="$REPO_ROOT/.build/fmod-tvos/sources/FMOD_SDL"
STAGE1_ARTIFACT_DIR="$REPO_ROOT/artifacts/tvos-native/rebuild-e"
DIAGNOSTIC_DIR="$REPO_ROOT/.build/fmod-tvos/diagnostics"
CLEAN=0

repo_path() {
  case "$1" in
    /*) printf '%s\n' "$1" ;;
    *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;;
  esac
}

while (($#)); do
  case "$1" in
    --sdk-root) [[ $# -ge 2 ]] || exit 2; SDK_ROOT="$2"; shift 2 ;;
    --game-root) [[ $# -ge 2 ]] || exit 2; GAME_ROOT="$2"; shift 2 ;;
    --stage-dir) [[ $# -ge 2 ]] || exit 2; STAGE_DIR="$(repo_path "$2")"; shift 2 ;;
    --source-dir) [[ $# -ge 2 ]] || exit 2; SOURCE_DIR="$(repo_path "$2")"; shift 2 ;;
    --stage1-artifact-dir) [[ $# -ge 2 ]] || exit 2; STAGE1_ARTIFACT_DIR="$(repo_path "$2")"; shift 2 ;;
    --clean) CLEAN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ -n "$SDK_ROOT" ]] || { echo "error: --sdk-root is required" >&2; exit 2; }
[[ -n "$GAME_ROOT" ]] || { echo "error: --game-root is required" >&2; exit 2; }
[[ "$DEPLOYMENT_TARGET" == "16.0" ]] || {
  echo "error: Stage 5A accepts only the established tvOS 16.0 deployment target" >&2
  exit 1
}
for required_tool in git python3 xcrun xcodebuild nm shasum nmedit; do
  command -v "$required_tool" >/dev/null || {
    echo "error: required existing tool is unavailable: $required_tool" >&2
    exit 1
  }
done

for selected_dir in "$STAGE_DIR" "$SOURCE_DIR"; do
  case "$selected_dir" in
    "$REPO_ROOT/.build/fmod-tvos/"*|"$REPO_ROOT/artifacts/fmod-tvos/"*) ;;
    *) echo "error: Stage 5 output and source directories must remain in ignored Stage 5 roots" >&2; exit 2 ;;
  esac
done

[[ -f "$STAGE1_ARTIFACT_DIR/normalized-manifest.json" ]] || {
  echo "error: accepted Stage 1 normalized manifest is missing" >&2
  exit 1
}
python3 - "$STAGE1_ARTIFACT_DIR/normalized-manifest.json" "$EXPECTED_STAGE1_HASH" <<'PY'
import json, pathlib, sys
manifest = json.loads(pathlib.Path(sys.argv[1]).read_text())
if manifest.get("logicalSetSha256") != sys.argv[2]:
    raise SystemExit("error: Stage 1 artifact set does not carry the accepted logical hash")
if manifest.get("deploymentTarget") != "16.0":
    raise SystemExit("error: Stage 1 artifact deployment target is not tvOS 16.0")
PY
SDL_HEADERS="$STAGE1_ARTIFACT_DIR/SDL2.xcframework/tvos-arm64/Headers"
[[ -f "$SDL_HEADERS/SDL.h" && -f "$SDL_HEADERS/SDL_audio.h" ]] || {
  echo "error: accepted Stage 1 SDL2 device headers are missing" >&2
  exit 1
}

if [[ -e "$STAGE_DIR" ]]; then
  [[ "$CLEAN" -eq 1 ]] || {
    echo "error: staging directory exists; rerun with --clean" >&2
    exit 1
  }
  [[ -f "$STAGE_DIR/.stage5a-fmod-staging" ]] || {
    echo "error: refusing to replace an unmarked staging directory" >&2
    exit 1
  }
fi

mkdir -p "$(dirname -- "$SOURCE_DIR")"
if [[ ! -d "$SOURCE_DIR/.git" ]]; then
  [[ ! -e "$SOURCE_DIR" ]] || { echo "error: bridge source path exists but is not a Git checkout" >&2; exit 1; }
  echo "+ git clone --no-checkout $FMOD_SDL_URL \$SOURCE_DIR"
  git clone --no-checkout "$FMOD_SDL_URL" "$SOURCE_DIR"
  git -C "$SOURCE_DIR" checkout --detach "$FMOD_SDL_COMMIT"
fi
[[ "$(git -C "$SOURCE_DIR" remote get-url origin)" == "$FMOD_SDL_URL" ]] || {
  echo "error: existing FMOD_SDL checkout has the wrong origin" >&2
  exit 1
}
[[ "$(git -C "$SOURCE_DIR" rev-parse HEAD)" == "$FMOD_SDL_COMMIT" ]] || {
  echo "error: existing FMOD_SDL checkout is not at the locked commit" >&2
  exit 1
}
[[ -z "$(git -C "$SOURCE_DIR" status --short)" ]] || {
  echo "error: existing FMOD_SDL checkout is dirty" >&2
  exit 1
}
[[ "$(shasum -a 256 "$SOURCE_DIR/FMOD_SDL.c" | awk '{print $1}')" == "0f5a4c25298e0c223ef7a42c3a479ee26fbd2f79509cfd814afadcbe9472d392" ]] || {
  echo "error: locked FMOD_SDL source hash mismatch" >&2
  exit 1
}
[[ "$(shasum -a 256 "$SOURCE_DIR/LICENSE" | awk '{print $1}')" == "ab25f1e6bc56f1531d6f3c955a1c76d446d23fbbf52a858e2ae7cedf56788212" ]] || {
  echo "error: locked FMOD_SDL licence hash mismatch" >&2
  exit 1
}
grep -Fq '#define FMOD_SDL_VERSION 190916' "$SOURCE_DIR/FMOD_SDL.c" || {
  echo "error: locked FMOD_SDL plugin version mismatch" >&2
  exit 1
}

mkdir -p "$(dirname -- "$STAGE_DIR")"
mkdir -p "$DIAGNOSTIC_DIR"
rm -f -- "$DIAGNOSTIC_DIR/duplicate-symbol-mismatch.txt" \
  "$DIAGNOSTIC_DIR/post-localization-duplicate-symbols.txt"
TEMP_STAGE="$(mktemp -d "$(dirname -- "$STAGE_DIR")/.fmod-stage5a.prepare.XXXXXX")"
cleanup() {
  rm -rf -- "$TEMP_STAGE"
}
trap cleanup EXIT
touch "$TEMP_STAGE/.stage5a-fmod-staging"
mkdir -p "$TEMP_STAGE/sdk/include/lowlevel" "$TEMP_STAGE/sdk/include/studio" \
  "$TEMP_STAGE/sdk/lib" "$TEMP_STAGE/build" "$TEMP_STAGE/xcframeworks" \
  "$TEMP_STAGE/banks" "$TEMP_STAGE/licenses"

"$REPO_ROOT/scripts/validate-fmod-tvos-sdk.sh" \
  --sdk-root "$SDK_ROOT" \
  --output "$TEMP_STAGE/sdk-manifest.json"
"$REPO_ROOT/scripts/validate-celeste-input.sh" \
  --game-root "$GAME_ROOT" \
  --output "$TEMP_STAGE/celeste-input-manifest.json"
python3 "$REPO_ROOT/scripts/fmod-tvos.py" validate-banks \
  --game-root "$GAME_ROOT" \
  --output "$TEMP_STAGE/bank-manifest.json"

cp -R "$SDK_ROOT/api/lowlevel/inc/." "$TEMP_STAGE/sdk/include/lowlevel/"
cp -R "$SDK_ROOT/api/studio/inc/." "$TEMP_STAGE/sdk/include/studio/"
cp "$SDK_ROOT/api/lowlevel/lib/libfmod_appletvos.a" "$TEMP_STAGE/sdk/lib/libfmod_appletvos.a"
cp "$SDK_ROOT/api/studio/lib/libfmodstudio_appletvos.a" "$TEMP_STAGE/sdk/lib/libfmodstudio_appletvos.a"
cp "$SOURCE_DIR/LICENSE" "$TEMP_STAGE/licenses/FMOD_SDL-LICENSE.txt"

python3 - "$TEMP_STAGE/bank-manifest.json" "$GAME_ROOT" "$TEMP_STAGE/banks" <<'PY'
import json, pathlib, shutil, sys
manifest = json.loads(pathlib.Path(sys.argv[1]).read_text())
game_root = pathlib.Path(sys.argv[2])
destination = pathlib.Path(sys.argv[3])
for record in manifest["banks"]:
    relative = pathlib.PurePosixPath(record["path"])
    source = game_root.joinpath(*relative.parts)
    target = destination.joinpath(*relative.parts)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)
PY

SDK_PATH="$(xcrun --sdk appletvos --show-sdk-path)"
CLANG="$(xcrun --sdk appletvos --find clang)"
AR="$(xcrun --sdk appletvos --find ar)"
{
  echo 'FMOD_SDL revision: 947df759501d9f5a7df702101c453efc3e06ba22'
  echo 'FMOD SDK: 1.10.09 build 97915 ($FMOD_SDK_ROOT)'
  echo 'deployment target: tvOS 16.0'
  echo '+ xcrun --sdk appletvos clang -arch arm64 -mtvos-version-min=16.0 -O2 -fno-common -I$STAGE1_SDL_HEADERS -I$FMOD_SDK_ROOT/api/lowlevel/inc -c FMOD_SDL.c'
  echo '+ ZERO_AR_DATE=1 xcrun --sdk appletvos ar rcs libfmod_SDL.a FMOD_SDL.o'
} > "$TEMP_STAGE/build/commands.log"

"$CLANG" -arch arm64 -isysroot "$SDK_PATH" -mtvos-version-min="$DEPLOYMENT_TARGET" \
  -O2 -fno-common -I "$SDL_HEADERS" -I "$TEMP_STAGE/sdk/include/lowlevel" \
  -c "$SOURCE_DIR/FMOD_SDL.c" -o "$TEMP_STAGE/build/FMOD_SDL.o" \
  >> "$TEMP_STAGE/build/build.log" 2>&1
ZERO_AR_DATE=1 "$AR" rcs "$TEMP_STAGE/build/libfmod_SDL.a" "$TEMP_STAGE/build/FMOD_SDL.o" \
  >> "$TEMP_STAGE/build/build.log" 2>&1

# FMOD and Theorafile each embed six private Ogg/Vorbis helpers. Keep the
# original proprietary archive intact and verified, then make only FMOD's copy
# local in an ignored derived object. This preserves all public FMOD exports,
# avoids a broad duplicate-symbol linker override, and does not touch Stage 1.
mkdir -p "$TEMP_STAGE/build/fmod-extract"
(cd "$TEMP_STAGE/build/fmod-extract" && "$AR" -x "$TEMP_STAGE/sdk/lib/libfmod_appletvos.a")
FMOD_MASTER_OBJECT="$(find "$TEMP_STAGE/build/fmod-extract" -maxdepth 1 -type f ! -name '__.SYMDEF*' -print -quit)"
[[ -n "$FMOD_MASTER_OBJECT" ]] || { echo "error: FMOD device archive has no extractable object" >&2; exit 1; }
THEORAFILE_ARCHIVE="$STAGE1_ARTIFACT_DIR/Theorafile.xcframework/tvos-arm64/libTheorafile.a"
LC_ALL=C comm -12 \
  <(nm -gjU "$TEMP_STAGE/sdk/lib/libfmod_appletvos.a" | sed '/:$/d; /^$/d' | LC_ALL=C sort -u) \
  <(nm -gjU "$THEORAFILE_ARCHIVE" | sed '/:$/d; /^$/d' | LC_ALL=C sort -u) \
  > "$TEMP_STAGE/build/duplicate-symbols.txt"
grep -v '^#' "$REPO_ROOT/native/fmod-tvos/localized-symbols.txt" | sed '/^[[:space:]]*$/d' \
  > "$TEMP_STAGE/build/expected-duplicate-symbols.txt"
LC_ALL=C sort -cu "$TEMP_STAGE/build/expected-duplicate-symbols.txt" || {
  echo "error: reviewed FMOD/Theorafile duplicate-symbol policy is not C-locale sorted and unique" >&2
  exit 1
}
cmp -s "$TEMP_STAGE/build/duplicate-symbols.txt" "$TEMP_STAGE/build/expected-duplicate-symbols.txt" || {
  mismatch_report="$DIAGNOSTIC_DIR/duplicate-symbol-mismatch.txt"
  missing_symbols="$TEMP_STAGE/build/missing-duplicate-symbols.txt"
  unexpected_symbols="$TEMP_STAGE/build/unexpected-duplicate-symbols.txt"
  LC_ALL=C comm -23 \
    "$TEMP_STAGE/build/expected-duplicate-symbols.txt" \
    "$TEMP_STAGE/build/duplicate-symbols.txt" > "$missing_symbols"
  LC_ALL=C comm -13 \
    "$TEMP_STAGE/build/expected-duplicate-symbols.txt" \
    "$TEMP_STAGE/build/duplicate-symbols.txt" > "$unexpected_symbols"
  expected_count="$(wc -l < "$TEMP_STAGE/build/expected-duplicate-symbols.txt" | tr -d ' ')"
  actual_count="$(wc -l < "$TEMP_STAGE/build/duplicate-symbols.txt" | tr -d ' ')"
  missing_count="$(wc -l < "$missing_symbols" | tr -d ' ')"
  unexpected_count="$(wc -l < "$unexpected_symbols" | tr -d ' ')"
  {
    printf 'FMOD/Theorafile duplicate-symbol mismatch\n'
    printf 'expected-count: %s\n' "$expected_count"
    printf 'actual-count: %s\n' "$actual_count"
    printf 'missing-count: %s\n' "$missing_count"
    printf 'unexpected-count: %s\n' "$unexpected_count"
    printf '\nMissing from actual intersection:\n'
    if [[ "$missing_count" -eq 0 ]]; then printf '<none>\n'; else cat "$missing_symbols"; fi
    printf '\nUnexpected duplicate symbols:\n'
    if [[ "$unexpected_count" -eq 0 ]]; then printf '<none>\n'; else cat "$unexpected_symbols"; fi
  } > "$mismatch_report"
  echo "error: FMOD/Theorafile duplicate-symbol set differs from the reviewed six-symbol policy" >&2
  echo "error: expected $expected_count symbols; found $actual_count; missing $missing_count; unexpected $unexpected_count" >&2
  echo "error: complete symbol differences: .build/fmod-tvos/diagnostics/duplicate-symbol-mismatch.txt" >&2
  exit 1
}
xcrun nmedit -R "$TEMP_STAGE/build/expected-duplicate-symbols.txt" \
  -o "$TEMP_STAGE/build/FMOD-localized.o" "$FMOD_MASTER_OBJECT"
ZERO_AR_DATE=1 "$AR" rcs "$TEMP_STAGE/build/libfmod_appletvos-localized.a" "$TEMP_STAGE/build/FMOD-localized.o"
remaining_duplicates="$(LC_ALL=C comm -12 \
  <(nm -gjU "$TEMP_STAGE/build/libfmod_appletvos-localized.a" | sed '/:$/d; /^$/d' | LC_ALL=C sort -u) \
  <(nm -gjU "$THEORAFILE_ARCHIVE" | sed '/:$/d; /^$/d' | LC_ALL=C sort -u))"
if [[ -n "$remaining_duplicates" ]]; then
  post_localization_report="$DIAGNOSTIC_DIR/post-localization-duplicate-symbols.txt"
  printf '%s\n' "$remaining_duplicates" > "$post_localization_report"
  remaining_count="$(wc -l < "$post_localization_report" | tr -d ' ')"
  echo "error: derived FMOD archive still shadows $remaining_count Theorafile symbols" >&2
  echo "error: complete symbol list: .build/fmod-tvos/diagnostics/post-localization-duplicate-symbols.txt" >&2
  exit 1
fi
localized_fmod_exports="$(nm -gjU "$TEMP_STAGE/build/libfmod_appletvos-localized.a" | sed '/:$/d; /^$/d' | LC_ALL=C sort -u)"
for required_export in _FMOD_System_Create _FMOD_System_GetVersion _FMOD_System_Init _FMOD_System_Release; do
  grep -Fxq "$required_export" <<<"$localized_fmod_exports" || {
    echo "error: derived FMOD archive lost required public export $required_export" >&2
    exit 1
  }
done

bridge_build="$(xcrun vtool -show-build "$TEMP_STAGE/build/FMOD_SDL.o")"
grep -Eq 'platform[[:space:]]+TVOS$' <<<"$bridge_build" || { echo "error: FMOD_SDL object is not TVOS" >&2; exit 1; }
grep -Eq 'minos[[:space:]]+16\.0$' <<<"$bridge_build" || { echo "error: FMOD_SDL object minimum is not tvOS 16.0" >&2; exit 1; }
[[ "$(xcrun lipo -archs "$TEMP_STAGE/build/libfmod_SDL.a")" == "arm64" ]] || { echo "error: FMOD_SDL archive is not arm64" >&2; exit 1; }
bridge_exports="$(nm -gjU "$TEMP_STAGE/build/libfmod_SDL.a" | sed '/:$/d; /^$/d' | LC_ALL=C sort -u)"
[[ "$bridge_exports" == "_FMOD_SDL_Register" ]] || { echo "error: FMOD_SDL bridge exports are not the locked single-symbol API" >&2; exit 1; }

xcodebuild -create-xcframework \
  -library "$TEMP_STAGE/build/libfmod_appletvos-localized.a" \
  -headers "$TEMP_STAGE/sdk/include/lowlevel" \
  -output "$TEMP_STAGE/xcframeworks/FMOD.xcframework" \
  >> "$TEMP_STAGE/build/build.log" 2>&1
xcodebuild -create-xcframework \
  -library "$TEMP_STAGE/sdk/lib/libfmodstudio_appletvos.a" \
  -headers "$TEMP_STAGE/sdk/include/studio" \
  -output "$TEMP_STAGE/xcframeworks/FMODStudio.xcframework" \
  >> "$TEMP_STAGE/build/build.log" 2>&1
xcodebuild -create-xcframework \
  -library "$TEMP_STAGE/build/libfmod_SDL.a" \
  -output "$TEMP_STAGE/xcframeworks/FMODSDL.xcframework" \
  >> "$TEMP_STAGE/build/build.log" 2>&1

python3 - "$TEMP_STAGE" "$REPO_ROOT/native/fmod-tvos-dependencies.lock.json" <<'PY'
import hashlib, json, pathlib, plistlib, subprocess, sys
root = pathlib.Path(sys.argv[1])
lock = json.loads(pathlib.Path(sys.argv[2]).read_text())

def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
xcframeworks = {}
for name in ("FMOD", "FMODStudio", "FMODSDL"):
    xc = root / "xcframeworks" / (name + ".xcframework")
    with (xc / "Info.plist").open("rb") as stream:
        info = plistlib.load(stream)
    available = info.get("AvailableLibraries", [])
    if len(available) != 1:
        raise SystemExit(f"error: {name} must contain exactly one device variant")
    variant = available[0]
    if variant.get("SupportedPlatform") != "tvos" or variant.get("SupportedPlatformVariant"):
        raise SystemExit(f"error: {name} contains a non-device-tvOS variant")
    if variant.get("SupportedArchitectures") != ["arm64"]:
        raise SystemExit(f"error: {name} is not arm64-only")
    binary = xc / variant["LibraryIdentifier"] / variant["LibraryPath"]
    xcframeworks[name] = {
        "libraryIdentifier": variant["LibraryIdentifier"],
        "architectures": variant["SupportedArchitectures"],
        "platform": variant["SupportedPlatform"],
        "binarySha256": digest(binary),
    }

sdk = json.loads((root / "sdk-manifest.json").read_text())
banks = json.loads((root / "bank-manifest.json").read_text())
bridge_object = root / "build" / "FMOD_SDL.o"
bridge_archive = root / "build" / "libfmod_SDL.a"
localized_fmod_object = root / "build" / "FMOD-localized.o"
localized_fmod_archive = root / "build" / "libfmod_appletvos-localized.a"
manifest = {
    "schemaVersion": 1,
    "deploymentTarget": "16.0",
    "stage1LogicalSha256": "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc",
    "fmodSdk": sdk,
    "banks": banks,
    "fmodSdl": {
        "sourceUrl": lock["fmodSdl"]["sourceUrl"],
        "commit": lock["fmodSdl"]["commit"],
        "sourceSha256": lock["fmodSdl"]["sourceSha256"],
        "licenseSha256": lock["fmodSdl"]["licenseSha256"],
        "pluginVersion": lock["fmodSdl"]["pluginVersion"],
        "architectures": subprocess.check_output(["xcrun", "lipo", "-archs", str(bridge_archive)], text=True).split(),
        "platform": "TVOS",
        "minimumOS": "16.0",
        "objectSha256": digest(bridge_object),
        "archiveSha256": digest(bridge_archive),
        "exports": ["FMOD_SDL_Register"],
    },
    "linkCompatibility": {
        "method": "nmedit -R on ignored FMOD object copy",
        "localizedSymbols": [
            line for line in (root / "build" / "expected-duplicate-symbols.txt").read_text().splitlines() if line
        ],
        "originalArchiveSha256": sdk["archives"]["lowLevelArchive"]["sha256"],
        "derivedObjectSha256": digest(localized_fmod_object),
        "derivedArchiveSha256": digest(localized_fmod_archive),
        "remainingStage1DuplicateSymbols": [],
        "publicFmodExportsPreserved": True,
    },
    "xcframeworks": xcframeworks,
    "normalizedCommands": [
        "xcrun --sdk appletvos clang -arch arm64 -mtvos-version-min=16.0 -O2 -fno-common -I$STAGE1_SDL_HEADERS -I$FMOD_SDK_ROOT/api/lowlevel/inc -c FMOD_SDL.c",
        "ZERO_AR_DATE=1 xcrun --sdk appletvos ar rcs libfmod_SDL.a FMOD_SDL.o",
        "xcodebuild -create-xcframework (device arm64 only)",
    ],
}
logical = json.dumps(manifest, sort_keys=True, separators=(",", ":")).encode()
manifest["logicalSha256"] = hashlib.sha256(logical).hexdigest()
(root / "logical-manifest.json").write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n")
PY

if [[ -e "$STAGE_DIR" ]]; then
  rm -rf -- "$STAGE_DIR"
fi
mv "$TEMP_STAGE" "$STAGE_DIR"
trap - EXIT

echo "PASS: deterministic FMOD tvOS device inputs staged"
echo "FMOD_SDL commit: $FMOD_SDL_COMMIT"
echo 'output: $REPO_ROOT/.build/fmod-tvos/ (selected stage directory)'
