#!/usr/bin/env bash
set -euo pipefail

# Beginner-facing modern iPhone/iPad self-builder. Keep this compatible with
# the macOS system Bash 3.2; the game itself remains full-AOT and device-only.

readonly REQUIRED_DOTNET="10.0.302"
readonly REQUIRED_WORKLOAD_SET="10.0.302.0"
readonly REQUIRED_NATIVE_HASH="9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$SCRIPT_DIR"
CONFIG_ROOT="$REPO_ROOT/.build/ios-self-build"
CONFIG_FILE="$CONFIG_ROOT/config.json"
LOG_ROOT="$REPO_ROOT/artifacts/ios/logs"
LAST_ERROR="$LOG_ROOT/last-error.txt"
PRODUCT_ROOT="$CONFIG_ROOT/products"
OUTPUT_ROOT="$REPO_ROOT/artifacts/ios"
PROJECT="$REPO_ROOT/modern-ios/CelesteIOSRuntimeHost/CelesteIOSRuntimeHost.csproj"
VERSION_SOURCE="$REPO_ROOT/modern-ios/IOSPortVersion.props"
UI_HELPER="$REPO_ROOT/scripts/tvos-builder-ui.sh"
COMMAND_LAUNCHER="$REPO_ROOT/scripts/tvos-builder-command.py"

MODE=""
NON_INTERACTIVE=0
DOCTOR=0
CLEAN=0
VERBOSE=0
NO_COLOR_OPTION=0
RESET_CONFIG=0
GAME_ROOT_ARG=""
FMOD_ROOT_ARG=""
BUNDLE_ID_ARG=""
TEAM_ID_ARG=""
DEVICE_NAME_ARG=""
CURRENT_PHASE="Reading options"
STOPPING=0

usage() {
  cat <<'EOF'
Usage: ./build-ios.sh [options]

Friendly self-build assistant for Celeste on a personal iPhone or iPad.
Without a mode it guides you through the required choices.

Main commands:
  ./build-ios.sh             Guided build
  ./build-ios.sh --doctor    Check this Mac and report optional input/device state
  ./build-ios.sh --unsigned  Build a verified unsigned IPA (not directly installable)
  ./build-ios.sh --signed    Build a device-provisioned development-signed IPA
  ./build-ios.sh --install   Build, sign, install, and launch on a paired device

Options:
  --game-root DIR       Supported extracted Celeste 1.4.0.0 FNA folder/app
  --fmod-root DIR       FMOD Engine iOS/tvOS 1.10.09 build 97915 SDK root
  --bundle-id ID        Local reverse-DNS app identity (advanced)
  --team-id ID          Local Personal Team identifier (advanced; never tracked)
  --device NAME         Paired iPhone/iPad name (advanced; no UDID is required)
  --non-interactive     Never prompt; require missing choices as options/environment
  --clean               Clear only managed/AOT/product intermediates
  --verbose             Stream child output while retaining full ignored logs
  --no-color            Disable ANSI colour
  --reset-config        Forget ignored builder choices; preserve apps and saves
  -h, --help            Show this help

CELESTE_GAME_ROOT and FMOD_SDK_ROOT may supply the two user-owned inputs.
The builder downloads neither Celeste nor FMOD and uploads nothing.

The full game targets physical arm64 iPhone/iPad devices. The accepted FMOD
1.10.09 package has no arm64 iOS Simulator audio slice.
EOF
}

while (($#)); do
  case "$1" in
    --doctor) DOCTOR=1; shift ;;
    --unsigned) [[ -z "$MODE" ]] || { echo "error: choose only one build mode" >&2; exit 2; }; MODE=unsigned; shift ;;
    --signed) [[ -z "$MODE" ]] || { echo "error: choose only one build mode" >&2; exit 2; }; MODE=development; shift ;;
    --install) [[ -z "$MODE" ]] || { echo "error: choose only one build mode" >&2; exit 2; }; MODE=install; shift ;;
    --game-root) [[ $# -ge 2 ]] || { echo "error: --game-root needs a directory" >&2; exit 2; }; GAME_ROOT_ARG="$2"; shift 2 ;;
    --fmod-root) [[ $# -ge 2 ]] || { echo "error: --fmod-root needs a directory" >&2; exit 2; }; FMOD_ROOT_ARG="$2"; shift 2 ;;
    --bundle-id) [[ $# -ge 2 ]] || { echo "error: --bundle-id needs a value" >&2; exit 2; }; BUNDLE_ID_ARG="$2"; shift 2 ;;
    --team-id) [[ $# -ge 2 ]] || { echo "error: --team-id needs a value" >&2; exit 2; }; TEAM_ID_ARG="$2"; shift 2 ;;
    --device) [[ $# -ge 2 ]] || { echo "error: --device needs the displayed device name" >&2; exit 2; }; DEVICE_NAME_ARG="$2"; shift 2 ;;
    --non-interactive) NON_INTERACTIVE=1; shift ;;
    --clean) CLEAN=1; shift ;;
    --verbose) VERBOSE=1; shift ;;
    --no-color) NO_COLOR_OPTION=1; shift ;;
    --reset-config) RESET_CONFIG=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ -r "$UI_HELPER" && -x "$COMMAND_LAUNCHER" ]] || {
  echo "error: builder presentation helpers are missing" >&2; exit 1;
}
# The mature UI helper is implementation-neutral despite its historical name.
# Preserve the tvOS environment spelling internally while exposing an iOS name.
export CELESTE_TVOS_HEARTBEAT_SECONDS="${CELESTE_IOS_HEARTBEAT_SECONDS:-60}"
# shellcheck source=scripts/tvos-builder-ui.sh
source "$UI_HELPER"
ui_initialize "$NO_COLOR_OPTION" "$VERBOSE" "$REPO_ROOT" "$LOG_ROOT" "$COMMAND_LAUNCHER"

begin_phase() { CURRENT_PHASE="$2"; ui_phase_begin "$1" "$2"; }
config_get() {
  [[ -f "$CONFIG_FILE" ]] || return 0
  python3 - "$CONFIG_FILE" "$1" <<'PY'
import json,pathlib,sys
try: value=json.loads(pathlib.Path(sys.argv[1]).read_text()).get(sys.argv[2],"")
except Exception: value=""
print(value if isinstance(value,str) else "")
PY
}
local_prop() {
  local path="$REPO_ROOT/modern-ios/Local.Build.props"
  [[ -f "$path" ]] || return 0
  python3 - "$path" "$1" <<'PY'
import pathlib,re,sys
text=pathlib.Path(sys.argv[1]).read_text()
m=re.search(rf"<{re.escape(sys.argv[2])}>([^<]+)</{re.escape(sys.argv[2])}>",text)
print(m.group(1).strip() if m else "")
PY
}
normalize_pasted_path() {
  python3 - "$1" <<'PY'
import shlex,sys
value=sys.argv[1].strip()
try: parts=shlex.split(value)
except ValueError: parts=[value]
print(parts[0] if len(parts)==1 else value)
PY
}
resolve_fmod_root() {
  python3 - "$1" <<'PY'
import pathlib,sys
root=pathlib.Path(sys.argv[1]).expanduser()
candidates=[root,root/'FMOD Programmers API']
matches=[p for p in candidates if (p/'doc/revision.txt').is_file() and (p/'api/lowlevel/lib/libfmod_iphoneos.a').is_file()]
print(str(matches[0]) if len(matches)==1 else str(root))
PY
}
write_last_error() {
  local problem="$1" fix="$2" log="${3:-}"
  mkdir -p "$LOG_ROOT" 2>/dev/null || return 0
  {
    printf 'Celeste for iPhone/iPad builder failure\n\nPhase:\n%s\n\nProblem:\n%s\n\nFix:\n%s\n' "$CURRENT_PHASE" "$problem" "$fix"
    [[ -z "$log" ]] || printf '\nFull local log:\n%s\n' "$log"
  } | ui_redact > "$LAST_ERROR" || true
}
stop_build() {
  local problem="$1" fix="$2" log="${3:-}"
  STOPPING=1; write_last_error "$problem" "$fix" "$log"; ui_phase_failure "$problem"
  printf '\nBuild stopped: %s\n\nWhat to do:\n%s\n\nDetails: artifacts/ios/logs/last-error.txt\n' "$problem" "$fix" >&2
  [[ -z "$log" ]] || printf 'Full local log: %s\n' "$log" >&2
  exit 1
}
signal_handler() {
  local signal="$1" code="$2"
  STOPPING=1; trap - ERR INT TERM HUP; ui_cancel_active_command "$signal"
  write_last_error "The builder was interrupted." "Rerun ./build-ios.sh when ready. Partial output was not promoted."
  ui_phase_failure "Interrupted"
  exit "$code"
}
unexpected_error() {
  local code=$?
  [[ "$STOPPING" -eq 0 ]] || exit "$code"
  stop_build "An unexpected command failed near shell line ${BASH_LINENO[0]:-unknown}." \
    "Read the bounded diagnostic above or the phase log, correct the first error, and rerun."
}
trap unexpected_error ERR
trap 'signal_handler INT 130' INT
trap 'signal_handler TERM 143' TERM
trap 'signal_handler HUP 129' HUP

run_logged() {
  local label="$1" operation started finished elapsed status; shift
  operation="$(ui_operation_name "$label")"; started="$(ui_now_seconds)"; ui_operation_begin "$operation"
  if ui_run_command "$LOG_ROOT/$label.log" "$operation" "$@"; then status=0; else status=$?; fi
  finished="$(ui_now_seconds)"; elapsed=$((finished-started))
  if [[ "$status" -ne 0 ]]; then
    ui_operation_failure "$operation" "$elapsed"
    printf '  Last diagnostic lines (privacy-redacted):\n' >&2
    ui_diagnostic_tail "$LOG_ROOT/$label.log" 45 >&2
    stop_build "$operation failed." "Correct the first diagnostic shown above, then rerun." "artifacts/ios/logs/$label.log"
  fi
  ui_operation_success "$operation" "$elapsed"
}

if [[ "$RESET_CONFIG" -eq 1 ]]; then
  [[ ! -f "$CONFIG_FILE" ]] || rm -f -- "$CONFIG_FILE"
  echo "Forgot ignored iOS builder choices. Builds, installed apps, and device saves were not changed."
  exit 0
fi
if [[ "$CLEAN" -eq 1 ]]; then
  for root in \
    "$PRODUCT_ROOT" \
    "$REPO_ROOT/modern-ios/CelesteIOSRuntimeHost/bin" \
    "$REPO_ROOT/modern-ios/CelesteIOSRuntimeHost/obj" \
    "$REPO_ROOT/.build/celeste-ios/current/managed/bin" \
    "$REPO_ROOT/.build/celeste-ios/current/managed/obj"; do
    [[ ! -e "$root" ]] || find "$root" -depth -delete
  done
fi
mkdir -p "$CONFIG_ROOT" "$LOG_ROOT" "$OUTPUT_ROOT"
rm -f -- "$LAST_ERROR"

VERSION="$(python3 - "$VERSION_SOURCE" <<'PY'
import pathlib,re,sys
t=pathlib.Path(sys.argv[1]).read_text(); m=re.search(r'<IOSPortSemanticVersion>([^<]+)</IOSPortSemanticVersion>',t)
if not m: raise SystemExit('missing iOS version')
print(m.group(1).strip())
PY
)"
BUILD_NUMBER="$(python3 - "$VERSION_SOURCE" <<'PY'
import pathlib,re,sys
t=pathlib.Path(sys.argv[1]).read_text(); m=re.search(r'<IOSPortBuildNumber>([^<]+)</IOSPortBuildNumber>',t)
if not m: raise SystemExit('missing iOS build')
print(m.group(1).strip())
PY
)"

printf '\nCeleste for iPhone and iPad Builder\nPort v%s · Build %s\n\n' "$VERSION" "$BUILD_NUMBER"
printf 'Private local logs: artifacts/ios/logs/\n\n'

begin_phase 1 "Checking this Mac"
[[ "$(uname -s)" == Darwin ]] || stop_build "This builder requires macOS." "Use an Apple-silicon Mac with the documented Xcode and .NET toolchain."
tools=(git python3 dotnet xcodebuild xcrun plutil codesign security shasum zip unzip patch nm nmedit)
missing=""
for tool in "${tools[@]}"; do command -v "$tool" >/dev/null 2>&1 || missing="$missing $tool"; done
[[ -z "$missing" ]] || stop_build "Required build tools are missing:$missing" "Install full Xcode, .NET SDK 10.0.302 with the iOS workload, and the documented command-line prerequisites."
run_logged host-doctor "$REPO_ROOT/scripts/check-ios-host.sh"
[[ -z "$(git -C "$REPO_ROOT" submodule status --recursive | awk '/^[+U-]/')" ]] || stop_build "Git submodules are missing or at the wrong revisions." "Run git submodule update --init --recursive, then rerun."
FREE_KIB="$(df -Pk "$REPO_ROOT" | awk 'NR==2 {print $4}')"
FREE_GIB=$((FREE_KIB/1024/1024))
[[ "$FREE_KIB" -ge 8388608 ]] || stop_build "Only $FREE_GIB GiB is free; a full-AOT build cannot safely complete." "Free at least 8 GiB (15 GiB recommended), then rerun."
[[ "$FREE_KIB" -ge 15728640 ]] || ui_warning "Only $FREE_GIB GiB is free. A clean full build has used about 11 GiB; 15 GiB is recommended."
echo "  PASS  Exact .NET/Xcode/iOS toolchain"
echo "  PASS  Recursive submodules"
echo "  PASS  $FREE_GIB GiB free"
ui_phase_success

if [[ "$DOCTOR" -eq 1 ]]; then
  begin_phase 2 "Checking optional inputs and devices"
  GAME_DOCTOR="${GAME_ROOT_ARG:-${CELESTE_GAME_ROOT:-}}"
  FMOD_DOCTOR="${FMOD_ROOT_ARG:-${FMOD_SDK_ROOT:-}}"
  doctor_fail=0
  if [[ -n "$GAME_DOCTOR" ]]; then
    if "$REPO_ROOT/scripts/validate-celeste-input.sh" --game-root "$GAME_DOCTOR" --output "$CONFIG_ROOT/doctor-game.json" >"$LOG_ROOT/doctor-game.log" 2>&1; then
      echo "  PASS  Supported Celeste input"
    else echo "  FAIL  Supplied Celeste input is not one of the nine exact profiles"; doctor_fail=1; fi
  else echo "  WARNING  No Celeste path supplied; set CELESTE_GAME_ROOT or use --game-root"; fi
  if [[ -n "$FMOD_DOCTOR" ]]; then
    FMOD_DOCTOR="$(resolve_fmod_root "$FMOD_DOCTOR")"
    if python3 "$REPO_ROOT/scripts/validate-fmod-ios-sdk.py" --sdk-root "$FMOD_DOCTOR" --lock "$REPO_ROOT/native/fmod-ios-dependencies.lock.json" >"$LOG_ROOT/doctor-fmod.log" 2>&1; then
      echo "  PASS  Exact FMOD 1.10.09 build 97915 input"
    else echo "  FAIL  Supplied FMOD path is not the exact accepted SDK"; doctor_fail=1; fi
  else echo "  WARNING  No FMOD path supplied; set FMOD_SDK_ROOT or use --fmod-root"; fi
  if xcrun xcdevice list --timeout 5 >"$CONFIG_ROOT/doctor-devices-private.json" 2>"$LOG_ROOT/doctor-devices.log"; then
    python3 "$REPO_ROOT/scripts/list-ios-devices.py" --input "$CONFIG_ROOT/doctor-devices-private.json" --output "$CONFIG_ROOT/doctor-devices-private.tsv"
    count="$(wc -l < "$CONFIG_ROOT/doctor-devices-private.tsv" | tr -d ' ')"
    [[ "$count" -gt 0 ]] && echo "  PASS  $count paired physical iPhone/iPad device(s) available" || echo "  WARNING  No paired physical iPhone/iPad is currently available"
  else echo "  WARNING  Xcode could not enumerate physical devices"; fi
  ui_phase_success
  ui_phase_skip 3 "Validating FMOD" "doctor-only mode"
  ui_phase_skip 4 "Preparing native libraries" "doctor-only mode"
  ui_phase_skip 5 "Generating Celeste" "doctor-only mode"
  ui_phase_skip 6 "Building full-AOT app" "doctor-only mode"
  ui_phase_skip 7 "Verifying package" "doctor-only mode"
  ui_phase_skip 8 "Signing or installing" "doctor-only mode"
  [[ "$doctor_fail" -eq 0 ]] || stop_build "One or more supplied optional inputs failed validation." "Choose one of the nine exact Celeste profiles and FMOD 1.10.09 build 97915, then rerun --doctor."
  ui_build_success "Doctor completed (warnings describe optional next steps)"
  exit 0
fi

if [[ -z "$MODE" ]]; then
  if [[ "$NON_INTERACTIVE" -eq 1 || ! -t 0 ]]; then
    stop_build "No build mode was selected." "Use --unsigned, --signed, or --install."
  fi
  cat <<'EOF'
  What would you like to do?
    1. Build, sign, install, and launch on an iPhone/iPad
    2. Create a verified unsigned IPA for later signing
    3. Create a local development-signed IPA without installing
EOF
  read -r -p "  Choice [1-3]: " choice
  case "$choice" in 1) MODE=install ;; 2) MODE=unsigned ;; 3) MODE=development ;; *) stop_build "The build choice was not recognised." "Rerun and choose 1, 2, or 3." ;; esac
fi

begin_phase 2 "Validating your Celeste files"
GAME_ROOT="${GAME_ROOT_ARG:-${CELESTE_GAME_ROOT:-$(config_get celesteGameRoot)}}"
if [[ -z "$GAME_ROOT" && "$NON_INTERACTIVE" -eq 0 ]]; then
  read -r -p "  Drag your extracted supported Celeste folder/app here: " answer
  GAME_ROOT="$(normalize_pasted_path "$answer")"
fi
[[ -n "$GAME_ROOT" && -d "$GAME_ROOT" ]] || stop_build "A supported Celeste game folder was not provided." "Use --game-root, set CELESTE_GAME_ROOT, or follow docs/IOS_BUILDING.md to obtain your own supported files."
run_logged validate-celeste "$REPO_ROOT/scripts/validate-celeste-input.sh" --game-root "$GAME_ROOT" --output "$CONFIG_ROOT/celeste-input.json"
python3 - "$CONFIG_ROOT/celeste-input.json" <<'PY'
import json,pathlib,sys
d=json.loads(pathlib.Path(sys.argv[1]).read_text())
print(f"  PASS  Celeste {d['gameVersion']} — {d['store']} / {d['sourcePlatform']} / {d['runtimeFamily']}")
print(f"        Profile: {d['profileId']}")
PY
ui_phase_success

begin_phase 3 "Validating FMOD before expensive work"
FMOD_ROOT="${FMOD_ROOT_ARG:-${FMOD_SDK_ROOT:-$(config_get fmodSdkRoot)}}"
if [[ -z "$FMOD_ROOT" ]]; then
  FMOD_ROOT="$(python3 - <<'PY'
import glob,pathlib
m=[]
for item in glob.glob('/Volumes/*'):
 p=pathlib.Path(item)
 if (p/'doc/revision.txt').is_file() and (p/'api/lowlevel/lib/libfmod_iphoneos.a').is_file(): m.append(str(p))
print(m[0] if len(m)==1 else '')
PY
)"
fi
if [[ -z "$FMOD_ROOT" && "$NON_INTERACTIVE" -eq 0 ]]; then
  echo "  Mount the official FMOD Engine iOS/tvOS 1.10.09 build 97915 package."
  read -r -p "  Drag the mounted SDK root here: " answer
  FMOD_ROOT="$(normalize_pasted_path "$answer")"
fi
[[ -n "$FMOD_ROOT" && -d "$FMOD_ROOT" ]] || stop_build "The required FMOD SDK was not found." "Download FMOD Engine iOS/tvOS 1.10.09 build 97915 using your FMOD account, mount it, then use --fmod-root."
FMOD_ROOT="$(resolve_fmod_root "$FMOD_ROOT")"
run_logged validate-fmod-ios python3 "$REPO_ROOT/scripts/validate-fmod-ios-sdk.py" --sdk-root "$FMOD_ROOT" --lock "$REPO_ROOT/native/fmod-ios-dependencies.lock.json" --output "$CONFIG_ROOT/fmod-input.json"
echo "  PASS  Exact FMOD Engine iOS/tvOS 1.10.09 build 97915"
ui_phase_success

python3 - "$CONFIG_FILE" "$GAME_ROOT" "$FMOD_ROOT" "$MODE" <<'PY'
import json,pathlib,sys
p=pathlib.Path(sys.argv[1]); d={}
if p.is_file():
 try: d=json.loads(p.read_text())
 except Exception: d={}
d.update({'schemaVersion':1,'celesteGameRoot':sys.argv[2],'fmodSdkRoot':sys.argv[3],'preferredMode':sys.argv[4]})
p.write_text(json.dumps(d,indent=2,sort_keys=True)+'\n')
PY

TEAM_ID="${TEAM_ID_ARG:-$(config_get developmentTeam)}"
[[ -n "$TEAM_ID" ]] || TEAM_ID="$(local_prop DevelopmentTeam)"
BUNDLE_ID="${BUNDLE_ID_ARG:-$(config_get bundleIdentifier)}"
[[ -n "$BUNDLE_ID" ]] || BUNDLE_ID="$(local_prop ApplicationId)"
if [[ -z "$BUNDLE_ID" ]]; then
  local_name="$(id -un | LC_ALL=C tr '[:upper:]' '[:lower:]' | tr -cd 'a-z0-9-' | cut -c1-24)"
  [[ -n "$local_name" ]] || local_name=local
  BUNDLE_ID="com.$local_name.celeste-ios"
fi
[[ "$BUNDLE_ID" =~ ^[A-Za-z][A-Za-z0-9-]*(\.[A-Za-z0-9-]+)+$ ]] || stop_build "The local bundle identifier is invalid." "Use reverse-DNS form such as com.your-name.celeste-ios."

DEVICE_ID=""; DEVICE_NAME=""; DEVICE_VERSION=""
if [[ "$MODE" == development || "$MODE" == install ]]; then
  TEAMS="$CONFIG_ROOT/teams-private.tsv"
  defaults export com.apple.dt.Xcode - 2>/dev/null | python3 -c 'import plistlib,sys; p=plistlib.loads(sys.stdin.buffer.read()); rows=[]
for groups in p.get("IDEProvisioningTeamByIdentifier",{}).values():
  for x in groups:
    if x.get("isFreeProvisioningTeam"): rows.append((x.get("teamID",""),x.get("teamName","Personal Team")))
print("\n".join("\t".join(r) for r in rows))' > "$TEAMS" || true
  [[ -s "$TEAMS" ]] || stop_build "Xcode has no Personal Team available." "Open Xcode > Settings > Accounts, add your Apple Account, allow Xcode to create an Apple Development certificate, then rerun."
  if [[ -z "$TEAM_ID" ]]; then
    team_count="$(wc -l < "$TEAMS" | tr -d ' ')"
    if [[ "$team_count" -eq 1 ]]; then TEAM_ID="$(cut -f1 "$TEAMS")"
    elif [[ "$NON_INTERACTIVE" -eq 1 ]]; then stop_build "More than one Personal Team is available." "Use --team-id for this local run."
    else
      echo "  Select a Personal Team:"; nl -w2 -s'. ' "$TEAMS" | cut -f2-
      read -r -p "  Choice: " pick
      [[ "$pick" =~ ^[0-9]+$ && "$pick" -ge 1 && "$pick" -le "$team_count" ]] || stop_build "The Personal Team choice was invalid." "Rerun and choose a displayed number."
      TEAM_ID="$(sed -n "${pick}p" "$TEAMS" | cut -f1)"
    fi
  fi
  grep -q "^$TEAM_ID[[:space:]]" "$TEAMS" || stop_build "The selected Personal Team is unavailable." "Select a Personal Team currently shown in Xcode Accounts."

  # A Personal Team development profile is device-bound even when the caller
  # only wants a signed IPA and does not request installation. Use the same
  # friendly name-based selection and provisioning path for both signed modes.
  run_logged list-ios-devices xcrun xcdevice list --timeout 8
  # xcdevice writes JSON to stdout; run it again into private ignored state.
  xcrun xcdevice list --timeout 8 > "$CONFIG_ROOT/devices-private.json" 2> "$CONFIG_ROOT/devices-private.log" || stop_build "Xcode could not list devices." "Wake, unlock, trust, pair, and enable Developer Mode on the iPhone/iPad, then rerun."
  python3 "$REPO_ROOT/scripts/list-ios-devices.py" --input "$CONFIG_ROOT/devices-private.json" --output "$CONFIG_ROOT/devices-private.tsv"
  [[ -s "$CONFIG_ROOT/devices-private.tsv" ]] || stop_build "No paired developer-ready iPhone or iPad is available." "Connect or pair the device in Xcode > Window > Devices and Simulators, trust this Mac, and enable Developer Mode."
  wanted="${DEVICE_NAME_ARG:-$(config_get preferredDeviceName)}"
  if [[ -n "$wanted" ]]; then
    matches="$(awk -F '\t' -v wanted="$wanted" '$2==wanted {n++} END {print n+0}' "$CONFIG_ROOT/devices-private.tsv")"
    [[ "$matches" -eq 1 ]] || stop_build "The requested device name is not uniquely available." "Use one exact device name shown by the guided builder."
    row="$(awk -F '\t' -v wanted="$wanted" '$2==wanted {print; exit}' "$CONFIG_ROOT/devices-private.tsv")"
  else
    count="$(wc -l < "$CONFIG_ROOT/devices-private.tsv" | tr -d ' ')"
    if [[ "$count" -eq 1 ]]; then row="$(sed -n '1p' "$CONFIG_ROOT/devices-private.tsv")"
    elif [[ "$NON_INTERACTIVE" -eq 1 ]]; then stop_build "More than one iPhone/iPad is available." "Use --device with a displayed device name; no UDID is required."
    else
      echo "  Select an iPhone or iPad:"
      cut -f2-3 "$CONFIG_ROOT/devices-private.tsv" | nl -w2 -s'. '
      read -r -p "  Choice: " pick
      [[ "$pick" =~ ^[0-9]+$ && "$pick" -ge 1 && "$pick" -le "$count" ]] || stop_build "The device choice was invalid." "Rerun and choose a displayed number."
      row="$(sed -n "${pick}p" "$CONFIG_ROOT/devices-private.tsv")"
    fi
  fi
  DEVICE_ID="$(printf '%s\n' "$row" | cut -f1)"; DEVICE_NAME="$(printf '%s\n' "$row" | cut -f2)"; DEVICE_VERSION="$(printf '%s\n' "$row" | cut -f3)"
  run_logged provision-ios "$REPO_ROOT/scripts/configure-ios-personal-team.sh" --team-id "$TEAM_ID" --bundle-id "$BUNDLE_ID" --device-id "$DEVICE_ID"
  echo "  PASS  Personal Team and $DEVICE_NAME ($DEVICE_VERSION) are ready"
  device_name_count="$(awk -F '\t' -v wanted="$DEVICE_NAME" '$2==wanted {n++} END {print n+0}' "$CONFIG_ROOT/devices-private.tsv")"
  python3 - "$CONFIG_FILE" "$BUNDLE_ID" "$TEAM_ID" "$DEVICE_NAME" "$device_name_count" <<'PY'
import json,pathlib,sys
p=pathlib.Path(sys.argv[1]); d=json.loads(p.read_text()); d.update({'bundleIdentifier':sys.argv[2],'developmentTeam':sys.argv[3]})
if sys.argv[4] and sys.argv[5]=='1': d['preferredDeviceName']=sys.argv[4]
else: d.pop('preferredDeviceName',None)
p.write_text(json.dumps(d,indent=2,sort_keys=True)+'\n')
PY
fi

begin_phase 4 "Preparing verified native libraries"
native_ok=0
if [[ -f "$REPO_ROOT/artifacts/ios-native/normalized-manifest.json" && -f "$REPO_ROOT/.build/ios-native/source-state.json" ]]; then
  native_ok="$(python3 - "$REPO_ROOT/artifacts/ios-native/normalized-manifest.json" "$REQUIRED_NATIVE_HASH" <<'PY'
import json,pathlib,sys
try: ok=json.loads(pathlib.Path(sys.argv[1]).read_text()).get('logicalSetSha256')==sys.argv[2]
except Exception: ok=False
print(1 if ok else 0)
PY
)"
fi
if [[ "$native_ok" -eq 1 ]]; then
  run_logged verify-native-ios "$REPO_ROOT/scripts/verify-ios-native.sh"
  echo "  Reused exact verified iOS native cache: $REQUIRED_NATIVE_HASH"
else
  run_logged fetch-native-ios "$REPO_ROOT/scripts/fetch-ios-native-deps.sh" --clean
  run_logged build-native-ios "$REPO_ROOT/scripts/build-ios-native.sh" --clean
  run_logged verify-native-ios "$REPO_ROOT/scripts/verify-ios-native.sh"
fi
run_logged prepare-ios-host "$REPO_ROOT/scripts/prepare-ios-foundation.sh" --clean
ui_phase_success

begin_phase 5 "Generating Celeste and staging Content/audio"
run_logged prepare-fmod-ios "$REPO_ROOT/scripts/prepare-fmod-ios.sh" --sdk-root "$FMOD_ROOT" --clean
run_logged generate-celeste-ios "$REPO_ROOT/scripts/prepare-celeste-ios-runtime.sh" --game-root "$GAME_ROOT" --clean
ui_phase_success

begin_phase 6 "Building the full-AOT iPhone/iPad app"
SIGNING=development; [[ "$MODE" == unsigned ]] && SIGNING=unsigned
WORK_OUTPUT="$PRODUCT_ROOT/$SIGNING.incomplete"
if [[ -e "$WORK_OUTPUT" ]]; then
  [[ -f "$WORK_OUTPUT/.ios-celeste-product-output" ]] || stop_build "An unrecognised partial output blocks this build." "Remove only .build/ios-self-build after checking it contains no needed local evidence."
fi
build_args=(--output-dir "$WORK_OUTPUT" --signing "$SIGNING" --bundle-id "$BUNDLE_ID" --clean)
[[ "$SIGNING" == development ]] && build_args+=(--team-id "$TEAM_ID")
[[ "$VERBOSE" -eq 0 ]] || build_args+=(--verbose)
run_logged build-ios-product "$REPO_ROOT/scripts/build-ios-celeste.sh" "${build_args[@]}"
ui_phase_success

begin_phase 7 "Verifying and promoting the package"
APP="$(find "$WORK_OUTPUT/publish" -maxdepth 1 -type d -name '*.app' -print -quit)"
[[ -n "$APP" && -d "$APP" ]] || stop_build "The full-AOT build produced no app bundle." "Inspect artifacts/ios/logs/build-ios-product.log."
run_logged verify-ios-final python3 "$REPO_ROOT/scripts/verify-ios-package.py" --app "$APP" --lane device --product celeste --signing "$SIGNING" --output "$WORK_OUTPUT/final-package-verification.json"
run_logged verify-ipa-zip unzip -tq "$WORK_OUTPUT/Celeste-modern-iOS.ipa"
suffix=development; [[ "$SIGNING" == unsigned ]] && suffix=unsigned
FINAL_IPA="$OUTPUT_ROOT/Celeste-iOS-v$VERSION-build$BUILD_NUMBER-$suffix.ipa"
TEMP_IPA="$FINAL_IPA.incomplete"
rm -f -- "$TEMP_IPA"
cp "$WORK_OUTPUT/Celeste-modern-iOS.ipa" "$TEMP_IPA"
rm -f -- "$FINAL_IPA"
mv "$TEMP_IPA" "$FINAL_IPA"
FINAL_APP="$OUTPUT_ROOT/Celeste-iOS-v$VERSION-build$BUILD_NUMBER-$suffix.app"
if [[ -e "$FINAL_APP" ]]; then find "$FINAL_APP" -depth -delete; fi
cp -R "$APP" "$FINAL_APP"
IPA_SHA="$(shasum -a 256 "$FINAL_IPA" | awk '{print $1}')"; IPA_BYTES="$(stat -f %z "$FINAL_IPA")"
echo "  PASS  $suffix IPA: artifacts/ios/$(basename "$FINAL_IPA")"
echo "  SHA-256: $IPA_SHA"
echo "  Size: $IPA_BYTES bytes"
ui_phase_success

begin_phase 8 "Signing or installing"
if [[ "$MODE" == install ]]; then
  MLAUNCH="$(find /usr/local/share/dotnet/packs/Microsoft.iOS.Sdk.net10.0_26.5/26.5.10301/tools/bin -maxdepth 1 -type f -name mlaunch -print -quit)"
  [[ -x "$MLAUNCH" ]] || stop_build "The iOS device installer is missing from the accepted workload." "Repair the exact .NET iOS workload 26.5.10301, then rerun."
  # mlaunch accepts the Xcode device identifier through --devname. Use the
  # private parser result internally so two devices with the same friendly
  # name remain unambiguous; the user still selects by a concise number and
  # never has to paste an identifier.
  run_logged install-ios-device "$MLAUNCH" --installdev="$FINAL_APP" --devname="$DEVICE_ID" --install-progress --timeout=180
  run_logged launch-ios-device "$MLAUNCH" --launchdevbundleid="$BUNDLE_ID" --devname="$DEVICE_ID" --wait-for-exit=false --wait-for-unlock=true --timeout=60
  echo "  PASS  Installed and launched on $DEVICE_NAME"
elif [[ "$MODE" == unsigned ]]; then
  echo "  UNSIGNED: package verified, but it must be signed before installation."
  echo "  To build and install locally later: ./build-ios.sh --install"
else
  echo "  DEVELOPMENT SIGNED: ready for a provisioned personal device."
  echo "  Installation was not requested."
fi
ui_phase_success

python3 - "$OUTPUT_ROOT/build-manifest.json" "$VERSION" "$BUILD_NUMBER" "$SIGNING" "$FINAL_IPA" "$IPA_SHA" "$IPA_BYTES" "$CONFIG_ROOT/celeste-input.json" <<'PY'
import json,pathlib,sys
out=pathlib.Path(sys.argv[1]); detected=json.loads(pathlib.Path(sys.argv[8]).read_text())
out.write_text(json.dumps({'schemaVersion':1,'product':'Celeste iOS','portVersion':sys.argv[2],'bundleBuild':int(sys.argv[3]),'signing':sys.argv[4],
 'ipaName':pathlib.Path(sys.argv[5]).name,'ipaSha256':sys.argv[6],'ipaBytes':int(sys.argv[7]),'profileId':detected['profileId'],
 'canonicalClass':detected['canonicalClass'],'rid':'ios-arm64','configuration':'Release','fullAOT':True,'fullTrim':True,
 'useInterpreter':False,'jit':False},indent=2,sort_keys=True)+'\n')
PY
rm -f -- "$LAST_ERROR"
ui_build_success "Celeste iOS v$VERSION Build $BUILD_NUMBER completed"
printf '\nFinal package:\n  %s\nState: %s\nSHA-256: %s\n\n' "$FINAL_IPA" "$SIGNING" "$IPA_SHA"
