#!/usr/bin/env bash
set -euo pipefail

readonly BASELINE_COMMIT="ce65a3896241ad33b0685a37e17278d0d5398e23"
readonly EXPECTED_STAGE1_HASH="6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
readonly EXPECTED_FMOD_LOGICAL_HASH="40e9fb6ce63d1551611e2ed365934a4dfa56c616be1393e249a4a550fdfdeccc"
readonly REQUIRED_EXPORTS=(
  SDL_GetVersion FNA3D_LinkedVersion FAudioLinkedVersion tf_fopen vkGetInstanceProcAddr
  FMOD_System_Create FMOD_System_GetVersion FMOD_System_Init FMOD_System_Release
  FMOD_Studio_System_Create FMOD_Studio_System_Initialize FMOD_Studio_System_LoadBankFile
  FMOD_Studio_System_FlushCommands
  FMOD_Studio_EventInstance_Start FMOD_Studio_EventInstance_Stop FMOD_SDL_Register
)

usage() {
  cat <<'USAGE'
Usage: scripts/verify-fmod-tvos.sh [options]

Verify the Stage 5A lock, ignored deterministic FMOD staging, prior-stage
isolation, privacy/proprietary boundaries, and optionally a built app bundle.
The script installs nothing.

Options:
  --stage-dir DIR          Prepared FMOD input (default: .build/fmod-tvos/current).
  --compare-stage-dir DIR  Require an independently prepared logical manifest
                           to be identical.
  --app DIR                Validate a built app bundle.
  --platform NAME          Required with --app: tvos or tvossimulator.
  -h, --help               Show this help.

Device apps must be arm64 TVOS, signed, contain the real FMOD/Studio/bridge
exports and exactly the validated ignored banks, and contain no Celeste
managed assembly. Simulator apps must be arm64 TVOSSIMULATOR and contain no
FMOD native exports or banks.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
STAGE_DIR="$REPO_ROOT/.build/fmod-tvos/current"
COMPARE_STAGE_DIR=""
APP_DIR=""
PLATFORM=""

repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --stage-dir) [[ $# -ge 2 ]] || exit 2; STAGE_DIR="$(repo_path "$2")"; shift 2 ;;
    --compare-stage-dir) [[ $# -ge 2 ]] || exit 2; COMPARE_STAGE_DIR="$(repo_path "$2")"; shift 2 ;;
    --app) [[ $# -ge 2 ]] || exit 2; APP_DIR="$(repo_path "$2")"; shift 2 ;;
    --platform) [[ $# -ge 2 ]] || exit 2; PLATFORM="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done
if [[ -n "$APP_DIR" && -z "$PLATFORM" ]] || [[ -z "$APP_DIR" && -n "$PLATFORM" ]]; then
  echo "error: --app and --platform must be supplied together" >&2
  exit 2
fi
case "$PLATFORM" in ""|tvos|tvossimulator) ;; *) echo "error: --platform must be tvos or tvossimulator" >&2; exit 2 ;; esac

[[ "$(dotnet --version)" == "10.0.302" ]] || { echo "error: .NET SDK 10.0.302 is required" >&2; exit 1; }
workloads="$(dotnet workload list)"
grep -Fq "Workload version: 10.0.302.0" <<<"$workloads" || { echo "error: workload set 10.0.302.0 is required" >&2; exit 1; }
grep -Eq '^tvos[[:space:]]' <<<"$workloads" || { echo "error: tvOS workload is absent" >&2; exit 1; }

python3 - "$REPO_ROOT/native/fmod-tvos-dependencies.lock.json" "$STAGE_DIR/logical-manifest.json" \
  "$EXPECTED_STAGE1_HASH" "$EXPECTED_FMOD_LOGICAL_HASH" <<'PY'
import json, pathlib, sys
lock_path, manifest_path = map(pathlib.Path, sys.argv[1:3])
stage1, expected_logical = sys.argv[3:5]
lock = json.loads(lock_path.read_text())
manifest = json.loads(manifest_path.read_text())
if lock["fmodSdk"]["version"] != "1.10.09" or lock["fmodSdk"]["build"] != 97915:
    raise SystemExit("error: FMOD SDK lock changed")
if lock["managedApi"]["version"] != "1.10.20":
    raise SystemExit("error: managed FMOD API discrepancy is not preserved")
if lock["fmodSdl"]["commit"] != "947df759501d9f5a7df702101c453efc3e06ba22":
    raise SystemExit("error: FMOD_SDL revision changed")
if manifest.get("logicalSha256") != expected_logical:
    raise SystemExit("error: prepared FMOD logical hash is not accepted")
if manifest.get("stage1LogicalSha256") != stage1 or manifest.get("deploymentTarget") != "16.0":
    raise SystemExit("error: prepared FMOD set changed Stage 1 or deployment baseline")
if manifest["fmodSdk"]["release"] != "1.10.09" or manifest["fmodSdk"]["headerVersion"] != "0x00011009":
    raise SystemExit("error: prepared SDK identity mismatch")
if manifest["banks"]["count"] != 7 or manifest["banks"]["totalBytes"] != 665721576:
    raise SystemExit("error: prepared Celeste bank set differs from the exact input")
compat = manifest["linkCompatibility"]
if compat["remainingStage1DuplicateSymbols"] or not compat["publicFmodExportsPreserved"]:
    raise SystemExit("error: FMOD/Theorafile link compatibility is not closed")
if len(compat["localizedSymbols"]) != 6:
    raise SystemExit("error: unexpected localized-symbol policy")
for name, item in manifest["xcframeworks"].items():
    if item["architectures"] != ["arm64"] or item["platform"] != "tvos":
        raise SystemExit(f"error: {name} is not device-only arm64 tvOS")
print("PASS: exact FMOD SDK, FMOD_SDL lock, banks, and logical staging")
PY

if [[ -n "$COMPARE_STAGE_DIR" ]]; then
  cmp -s "$STAGE_DIR/logical-manifest.json" "$COMPARE_STAGE_DIR/logical-manifest.json" || {
    echo "error: independent FMOD preparations differ" >&2
    exit 1
  }
  echo "PASS: independent FMOD preparations are logically identical"
fi

grep -Fq 'TVOS_AUDIO_DISABLED' "$REPO_ROOT/managed/templates/Celeste.Modern.csproj" || {
  echo "error: normal Celeste no-audio compile boundary was removed" >&2
  exit 1
}
grep -Fq '#if TVOS_AUDIO_DISABLED' "$REPO_ROOT/managed/templates/Audio.TvOSDisabled.cs" || {
  echo "error: normal Celeste no-audio implementation was removed" >&2
  exit 1
}
grep -Fq "'\$(CelesteLaunchMode)' == 'FmodDiagnostic' and '\$(RuntimeIdentifier)' == 'tvos-arm64'" \
  "$REPO_ROOT/tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj" || {
  echo "error: FMOD native references are not isolated to the device diagnostic" >&2
  exit 1
}
if git -C "$REPO_ROOT" grep -n -E 'com\.apple\.developer\.user-management' -- tvos >/dev/null; then
  echo "error: User Management is out of scope" >&2
  exit 1
fi

git -C "$REPO_ROOT" diff --quiet "$BASELINE_COMMIT" -- \
  build.sh celestemeow fnalibs-ios-builder-celeste \
  native/tvos-dependencies.lock.json native/patches \
  scripts/fetch-tvos-deps.sh scripts/verify-tvos-native.sh || {
  echo "error: existing iOS or locked Stage 1 dependency lane changed" >&2
  exit 1
}
python3 "$REPO_ROOT/scripts/verify-celeste-tvos-stage16b.py" --repo-root "$REPO_ROOT" >/dev/null
python3 "$REPO_ROOT/scripts/verify-celeste-tvos-stage14.py" --repo-root "$REPO_ROOT" >/dev/null
(cd "$REPO_ROOT" && shasum -a 256 -c tvos/stage2-ios-native-baseline.sha256 >/dev/null)
echo "PASS: accepted Stage 14 inventory, Stage 16 native evolution, and iOS lane"

python3 "$REPO_ROOT/scripts/verify-repository-stage8b.py" >/dev/null
echo "PASS: current repository verifier found no proprietary binary or private identity data"

if [[ -n "$APP_DIR" ]]; then
  [[ -d "$APP_DIR" ]] || { echo "error: app bundle does not exist" >&2; exit 1; }
  EXECUTABLE_NAME="$(plutil -extract CFBundleExecutable raw "$APP_DIR/Info.plist")"
  EXECUTABLE="$APP_DIR/$EXECUTABLE_NAME"
  file "$EXECUTABLE" | grep -Fq 'Mach-O 64-bit executable arm64' || { echo "error: app executable is not arm64 Mach-O" >&2; exit 1; }
  build_info="$(xcrun vtool -show-build "$EXECUTABLE")"
  expected_marker="TVOS"
  [[ "$PLATFORM" == "tvossimulator" ]] && expected_marker="TVOSSIMULATOR"
  grep -Eq "platform[[:space:]]+$expected_marker$" <<<"$build_info" || { echo "error: app has the wrong Apple platform" >&2; exit 1; }
  grep -Eq 'minos[[:space:]]+16\.0$' <<<"$build_info" || { echo "error: app minimum is not tvOS 16.0" >&2; exit 1; }
  grep -Eq 'sdk[[:space:]]+26\.5$' <<<"$build_info" || { echo "error: app SDK is not 26.5" >&2; exit 1; }
  exports="$(nm -gjU "$EXECUTABLE" | sed '/:$/d; /^$/d' | LC_ALL=C sort -u)"
  for stage1_export in SDL_GetVersion FNA3D_LinkedVersion FAudioLinkedVersion tf_fopen vkGetInstanceProcAddr; do
    grep -Fxq "_$stage1_export" <<<"$exports" || { echo "error: app lacks Stage 1 export _$stage1_export" >&2; exit 1; }
  done

  if [[ "$PLATFORM" == "tvos" ]]; then
    for required_export in "${REQUIRED_EXPORTS[@]}"; do
      grep -Fxq "_$required_export" <<<"$exports" || { echo "error: device app lacks _$required_export" >&2; exit 1; }
    done
    [[ ! -f "$APP_DIR/Celeste.dll" && ! -f "$APP_DIR/Celeste.Content.dll" ]] || {
      echo "error: FMOD diagnostic app contains a Celeste managed assembly" >&2
      exit 1
    }
    python3 - "$STAGE_DIR/bank-manifest.json" "$APP_DIR" <<'PY'
import hashlib, json, pathlib, sys
manifest = json.loads(pathlib.Path(sys.argv[1]).read_text())
app = pathlib.Path(sys.argv[2])
expected = {}
for item in manifest["banks"]:
    relative = pathlib.PurePosixPath(item["path"])
    expected[relative.as_posix()] = item["sha256"]
actual = {}
root = app / "Content" / "FMOD"
for path in root.rglob("*.bank"):
    relative = path.relative_to(app).as_posix()
    actual[relative] = hashlib.sha256(path.read_bytes()).hexdigest()
if actual != expected:
    raise SystemExit("error: app FMOD banks differ from the validated ignored bank manifest")
print("PASS: device app contains exactly the seven validated ignored banks")
PY
    if find "$APP_DIR" -type f \( -name '*.h' -o -name '*.so' -o -name '*.dylib' \) -print -quit | grep -q .; then
      echo "error: app contains an SDK header or loose desktop/dynamic native library" >&2
      exit 1
    fi
    if find "$APP_DIR" -type f \( -iname '*SaveData*' -o -iname 'Settings.xml' -o -iname '*.celeste' \) -print -quit | grep -q .; then
      echo "error: app contains save/settings data" >&2
      exit 1
    fi
    codesign --verify --deep --strict "$APP_DIR"
    codesign -d --entitlements :- "$APP_DIR" > "$STAGE_DIR/app-entitlements.plist" 2>/dev/null
    if plutil -p "$STAGE_DIR/app-entitlements.plist" | grep -Fq 'com.apple.developer.user-management'; then
      echo "error: diagnostic app contains User Management" >&2
      exit 1
    fi
    [[ -f "$APP_DIR/embedded.mobileprovision" ]] || { echo "error: signed device app has no embedded profile" >&2; exit 1; }
    security cms -D -i "$APP_DIR/embedded.mobileprovision" > "$STAGE_DIR/profile-private.plist"
    python3 - "$STAGE_DIR/profile-private.plist" <<'PY'
import plistlib, pathlib, sys
profile = plistlib.loads(pathlib.Path(sys.argv[1]).read_bytes())
if not {"AppleTVOS", "tvOS"}.intersection(profile.get("Platform", [])):
    raise SystemExit("error: provisioning profile is not for tvOS")
PY
    echo "PASS: signed arm64 TVOS diagnostic app, real FMOD exports, no headers/saves/Celeste assembly"
  else
    if grep -Eq '^_FMOD_(System|Studio)|^_FMOD_SDL_Register$' <<<"$exports"; then
      echo "error: arm64 simulator app contains device-only FMOD exports" >&2
      exit 1
    fi
    [[ ! -d "$APP_DIR/Content/FMOD" ]] || { echo "error: simulator app contains FMOD banks" >&2; exit 1; }
    echo "PASS: arm64 TVOSSIMULATOR app remains explicitly no-FMOD"
  fi
fi

echo "Stage 5A static verification passed."
