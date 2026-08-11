#!/usr/bin/env bash
set -euo pipefail

readonly REQUIRED_DOTNET="10.0.302"
readonly REQUIRED_WORKLOAD_SET="10.0.302.0"
readonly EXPECTED_STAGE1_HASH="6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"

usage() {
  cat <<'USAGE'
Usage: ./build-tvos.sh [options]

Friendly self-build assistant for the personal-use Celeste Apple TV port.
Without --non-interactive it prompts for missing paths and build choices.

Options:
  --non-interactive       Never prompt; require all needed choices
  --mode MODE             install, ipa, both, or validate
  --game-root DIR         Tested itch.io Linux Celeste 1.4.0.0 root
  --fmod-root DIR         Mounted FMOD Engine iOS/tvOS 1.10.09 SDK root
  --bundle-id ID          Unique local bundle identifier
  --team-id ID            Personal Team identifier for direct installation
  --device-id ID          Paired Apple TV identifier for direct installation
  --check-host            Check only Mac/Xcode/.NET/tool prerequisites
  --clean                 Clean only Stage 8 build/output caches
  --reset-config          Remove the ignored saved local configuration and exit
  -h, --help              Show this help

Environment variables CELESTE_GAME_ROOT and FMOD_SDK_ROOT override saved paths.
Command-line values override both environment variables and saved values.

Modes:
  install  Build, sign, install, launch, and verify on a paired Apple TV
  ipa      Create dist/Celeste-tvOS-unsigned.ipa for an external signing tool
  both     Produce both outputs independently
  validate Validate the Mac, game, FMOD, and artwork inputs without building

This command installs no software, accepts no licence, downloads neither
Celeste nor FMOD, and never stores Apple credentials.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$SCRIPT_DIR"
CONFIG_ROOT="$REPO_ROOT/.build/tvos-self-build"
CONFIG_FILE="$CONFIG_ROOT/config.json"
VALIDATION_ROOT="$CONFIG_ROOT/validation"
BUILD_ROOT="$CONFIG_ROOT/build"
LOG_ROOT="$REPO_ROOT/dist/logs"
LAST_ERROR="$LOG_ROOT/last-error.txt"
DIST_ROOT="$REPO_ROOT/dist"
ARTWORK_ROOT="$CONFIG_ROOT/artwork/Assets.xcassets"
PROJECT="$REPO_ROOT/tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj"
NON_INTERACTIVE=0
CLEAN=0
RESET_CONFIG=0
MODE=""
GAME_ROOT_ARG=""
FMOD_ROOT_ARG=""
BUNDLE_ID_ARG=""
TEAM_ID_ARG=""
DEVICE_ID_ARG=""
CHECK_HOST=0
CURRENT_PHASE="Reading options"
STOPPING=0

redact_error_log() {
  /usr/bin/sed -E \
    -e 's#/Users/[^/[:space:]]+#$HOME#g' \
    -e 's#/Volumes/[^/[:space:]]+#$FMOD_SDK_ROOT#g' \
    -e 's#[A-Fa-f0-9]{8}-[A-Fa-f0-9-]{27,}#<DEVICE_ID>#g' \
    -e 's#[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}#<APPLE_ID>#g' \
    -e 's#(^|[^A-Z0-9])[A-Z0-9]{10}([^A-Z0-9]|$)#\1<TEAM_ID>\2#g' \
    -e 's#(com|org|net)\.[A-Za-z0-9.-]+#<BUNDLE_ID>#g'
}

write_last_error() {
  local problem="$1" detected="$2" required="$3" fix="$4" command_log="${5:-}"
  mkdir -p "$LOG_ROOT" 2>/dev/null || return 0
  {
    printf 'Celeste for Apple TV builder failure\n\n'
    printf 'Phase:\n%s\n\n' "$CURRENT_PHASE"
    printf 'Problem:\n%s\n\n' "$problem"
    printf 'Detected:\n%s\n\n' "$detected"
    printf 'Required:\n%s\n\n' "$required"
    printf 'Fix:\n%s\n' "$fix"
    if [[ -n "$command_log" ]]; then
      printf '\nFull command log:\n%s\n' "$command_log"
    fi
  } | redact_error_log > "$LAST_ERROR" || true
}

stop_build() {
  local problem="$1" detected="$2" required="$3" fix="$4" command_log="${5:-}"
  STOPPING=1
  write_last_error "$problem" "$detected" "$required" "$fix" "$command_log"
  cat >&2 <<EOF

Build stopped during:
$CURRENT_PHASE

Problem:
$problem

Detected:
$detected

Required:
$required

Fix:
$fix

Details saved to:
  dist/logs/last-error.txt
EOF
  if [[ -n "$command_log" ]]; then
    cat >&2 <<EOF

Full command log:
  $command_log
EOF
  fi
  cat >&2 <<'EOF'

Then run:
./build-tvos.sh
EOF
  exit 1
}

unexpected_error() {
  local exit_code=$?
  [[ "$STOPPING" -eq 0 ]] || exit "$exit_code"
  stop_build "An unexpected builder command failed" \
    "exit status $exit_code near shell line ${BASH_LINENO[0]:-unknown}" \
    "the current builder phase to complete" \
    "Read the terminal error and last-error.txt; if a phase log is named there, inspect its first meaningful error."
}
trap unexpected_error ERR

repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
config_get() {
  local key="$1"
  [[ -f "$CONFIG_FILE" ]] || return 0
  python3 - "$CONFIG_FILE" "$key" <<'PY'
import json,pathlib,sys
try: value=json.loads(pathlib.Path(sys.argv[1]).read_text()).get(sys.argv[2],"")
except Exception: value=""
print(value if isinstance(value,str) else "")
PY
}
local_prop() {
  local key="$1" path="$REPO_ROOT/tvos/Local.Build.props"
  [[ -f "$path" ]] || return 0
  python3 - "$path" "$key" <<'PY'
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
run_logged() {
  local label="$1"; shift
  mkdir -p "$LOG_ROOT"
  printf '  %s...\n' "$label"
  {
    printf '+ '
    printf '%q ' "$@"
    printf '\n'
    "$@"
  } > "$LOG_ROOT/$label.log" 2>&1 || {
    echo "  failed; the last diagnostic lines are:" >&2
    tail -40 "$LOG_ROOT/$label.log" >&2
    stop_build "$label failed" "the command exited unsuccessfully" "A successful $label" \
      "Correct the first error shown above." "dist/logs/$label.log"
  }
  printf '  done\n'
}
locate_app() {
  python3 - "$1" "$2" <<'PY'
import pathlib,plistlib,sys
root,bundle=pathlib.Path(sys.argv[1]),sys.argv[2]
matches=[]
for app in root.rglob("*.app"):
    if "bin" not in app.parts: continue
    try: info=plistlib.loads((app/"Info.plist").read_bytes())
    except Exception: continue
    if info.get("CFBundleIdentifier")==bundle and info.get("CFBundleExecutable")=="CelesteTvOSRuntimeHost": matches.append(app)
if len(matches)!=1: raise SystemExit(f"expected one product app, found {len(matches)}")
print(matches[0])
PY
}

while (($#)); do
  case "$1" in
    --non-interactive) NON_INTERACTIVE=1; shift ;;
    --mode) [[ $# -ge 2 ]] || stop_build "An option value is missing" "--mode has no value" "--mode install, ipa, both, or validate" "Add the intended value and rerun."; MODE="$2"; shift 2 ;;
    --game-root) [[ $# -ge 2 ]] || stop_build "An option value is missing" "--game-root has no value" "a Celeste directory after --game-root" "Add the path and rerun."; GAME_ROOT_ARG="$2"; shift 2 ;;
    --fmod-root) [[ $# -ge 2 ]] || stop_build "An option value is missing" "--fmod-root has no value" "an FMOD SDK directory after --fmod-root" "Add the path and rerun."; FMOD_ROOT_ARG="$2"; shift 2 ;;
    --bundle-id) [[ $# -ge 2 ]] || stop_build "An option value is missing" "--bundle-id has no value" "a reverse-DNS identifier after --bundle-id" "Add the identifier and rerun."; BUNDLE_ID_ARG="$2"; shift 2 ;;
    --team-id) [[ $# -ge 2 ]] || stop_build "An option value is missing" "--team-id has no value" "a Personal Team identifier after --team-id" "Add the ignored local value and rerun."; TEAM_ID_ARG="$2"; shift 2 ;;
    --device-id) [[ $# -ge 2 ]] || stop_build "An option value is missing" "--device-id has no value" "a paired device identifier after --device-id" "Add the ignored local value and rerun."; DEVICE_ID_ARG="$2"; shift 2 ;;
    --check-host) CHECK_HOST=1; shift ;;
    --clean) CLEAN=1; shift ;;
    --reset-config) RESET_CONFIG=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) stop_build "The option is not recognised" "$1" "an option shown by ./build-tvos.sh --help" "Correct or remove the option, then rerun." ;;
  esac
done

if [[ "$RESET_CONFIG" -eq 1 ]]; then
  rm -f -- "$CONFIG_FILE"
  echo "Local Stage 8 configuration reset. Generated builds and durable Apple TV saves were not changed."
  exit 0
fi

if [[ "$CLEAN" -eq 1 ]]; then
  for root in "$BUILD_ROOT" "$REPO_ROOT/artifacts/tvos-self-build" "$DIST_ROOT"; do
    case "$root" in "$REPO_ROOT/.build/tvos-self-build/"*|"$REPO_ROOT/artifacts/tvos-self-build"|"$REPO_ROOT/dist") rm -rf -- "$root" ;; esac
  done
fi
mkdir -p "$CONFIG_ROOT" "$VALIDATION_ROOT" "$DIST_ROOT" "$LOG_ROOT"
rm -f -- "$LAST_ERROR"

printf '\nCeleste for Apple TV Builder\n\n'
printf 'Logs: dist/logs/\n\n'
CURRENT_PHASE="Checking this Mac"
printf '[1/8] Checking this Mac\n'
[[ "$(uname -s)" == Darwin ]] || stop_build "This builder requires macOS" "$(uname -s)" "macOS" "Run it on a Mac with Xcode installed."
HOST_ARCH="$(uname -m)"
[[ "$HOST_ARCH" == arm64 ]] || echo "  Warning: $HOST_ARCH is untested; Apple silicon arm64 is the supported host."
[[ -w "$REPO_ROOT" ]] || stop_build "The repository is not writable" "read-only path" "writable clone" "Move or change permissions on the clone."
tool_names=(git python3 dotnet xcodebuild xcrun swift gmake patch plutil codesign security shasum ditto lipo nm nmedit monodis file)
tool_reasons=(
  "source and submodule revision checks"
  "deterministic validation and source generation"
  "the modern tvOS build and full AOT"
  "native Xcode projects, assets, and signing"
  "tvOS SDK, archive, inspection, and device tools"
  "local icon and Top Shelf artwork generation"
  "the pinned MoltenVK tvOS targets"
  "tracked generated-source compatibility patches"
  "property-list validation"
  "development signature verification"
  "certificate and provisioning inspection"
  "deterministic SHA-256 manifests"
  "app copying and IPA archive creation"
  "archive architecture validation"
  "native export/import validation"
  "the exact FMOD duplicate-symbol localisation step"
  "managed Celeste assembly identity validation"
  "managed/native input architecture checks"
)
missing_tools=()
missing_details=""
missing_xcode=0
missing_dotnet=0
missing_gmake=0
missing_monodis=0
for ((tool_index=0; tool_index<${#tool_names[@]}; tool_index++)); do
  tool="${tool_names[$tool_index]}"
  reason="${tool_reasons[$tool_index]}"
  if ! command -v "$tool" >/dev/null 2>&1; then
    missing_tools+=("$tool")
    missing_details+="  - $tool: $reason"$'\n'
    case "$tool" in
      dotnet) missing_dotnet=1 ;;
      gmake) missing_gmake=1 ;;
      monodis) missing_monodis=1 ;;
      *) missing_xcode=1 ;;
    esac
  fi
done
if ((${#missing_tools[@]})); then
  printf '\nSome required tools are missing:\n\n' >&2
  for ((tool_index=0; tool_index<${#tool_names[@]}; tool_index++)); do
    tool="${tool_names[$tool_index]}"
    reason="${tool_reasons[$tool_index]}"
    command -v "$tool" >/dev/null 2>&1 || printf '  ✗ %-10s %s\n' "$tool" "$reason" >&2
  done
  printf '\nAlready available: %d of %d required commands.\n' \
    "$((${#tool_names[@]}-${#missing_tools[@]}))" "${#tool_names[@]}" >&2
  printf '\nInstallation guidance:\n' >&2
  if [[ "$missing_xcode" -eq 1 ]]; then
    printf '  - Install/open full Xcode, finish first launch, and select its developer directory.\n' >&2
  fi
  if [[ "$missing_dotnet" -eq 1 ]]; then
    printf '  - Install .NET SDK 10.0.302 from Microsoft, then install workload set 10.0.302.0.\n' >&2
  fi
  homebrew_formulas=()
  [[ "$missing_gmake" -eq 0 ]] || homebrew_formulas+=(make)
  [[ "$missing_monodis" -eq 0 ]] || homebrew_formulas+=(mono)
  if ((${#homebrew_formulas[@]})); then
    printf '  - Install GNU Make and/or Mono from their official distributions.\n' >&2
    printf '    If you use Homebrew (optional): brew install' >&2
    printf ' %s' "${homebrew_formulas[@]}" >&2
    printf '\n' >&2
  fi
  missing_csv="$(IFS=', '; echo "${missing_tools[*]}")"
  stop_build "${#missing_tools[@]} required tools are missing." \
    "${missing_details%$'\n'}" "all required host commands on PATH" \
    "Follow the installation guidance above or docs/TROUBLESHOOTING.md#missing-build-tools. This builder installs nothing."
fi
echo "  Host commands: all ${#tool_names[@]} required commands are available."
PYTHON_VERSION="$(python3 -c 'import platform; print(platform.python_version())')"
python3 -c 'import sys; raise SystemExit(0 if sys.version_info >= (3, 9) else 1)' || \
  stop_build "The Python version is unsupported" "$PYTHON_VERSION" "Python 3.9 or newer" "Use the Python 3 supplied with the supported Xcode tools or install a current Python 3 release."
DEVELOPER_DIR_DETECTED="$(xcode-select -p 2>/dev/null || true)"
[[ -d "$DEVELOPER_DIR_DETECTED" && -x "$DEVELOPER_DIR_DETECTED/usr/bin/xcodebuild" ]] || stop_build "The selected Xcode developer directory is invalid" "${DEVELOPER_DIR_DETECTED:-none}" "A complete Xcode installation" "Run sudo xcode-select -s /Applications/Xcode.app/Contents/Developer after installing Xcode."
xcodebuild -checkFirstLaunchStatus >/dev/null 2>&1 || stop_build "Xcode first-launch tasks or licence acceptance are incomplete" "xcodebuild check failed" "Xcode ready for command-line builds" "Open Xcode once, review its licence, and let it finish installing components."
TVOS_SDK="$(xcrun --sdk appletvos --show-sdk-version 2>/dev/null || true)"
[[ -n "$TVOS_SDK" ]] || stop_build "The tvOS SDK is unavailable" "no appletvos SDK" "an installed tvOS SDK" "Open Xcode Settings > Components and install the tvOS platform support."
DOTNET_VERSION="$(dotnet --version)"
[[ "$DOTNET_VERSION" == "$REQUIRED_DOTNET" ]] || stop_build "The .NET SDK version is unsupported" "$DOTNET_VERSION" "$REQUIRED_DOTNET" "Install the required SDK without removing other SDKs; global.json will select it."
WORKLOAD_SET="$(dotnet workload list 2>/dev/null | awk '/Workload version:/ {print $3; exit}')"
[[ "$WORKLOAD_SET" == "$REQUIRED_WORKLOAD_SET" ]] || stop_build "The .NET tvOS workload set is unsupported" "${WORKLOAD_SET:-not installed}" "$REQUIRED_WORKLOAD_SET" "Install the exact tvOS workload set using the documented .NET workload command, then rerun."
dotnet workload list | grep -Eq '^[[:space:]]*tvos[[:space:]]' || stop_build "The .NET tvOS workload is missing" "tvos not listed" "tvos workload installed" "Install the exact tvOS workload for SDK 10.0.302, then rerun."
[[ -z "$(git -C "$REPO_ROOT" submodule status --recursive | awk '/^[+U]/')" ]] || stop_build "Git submodules are not at the recorded revisions" "modified submodule revision" "recorded submodule commits" "Restore the recorded submodule commits and rerun. Uninitialised nested FNA sources are acceptable because Stage 1 fetches its locked inputs separately."
FREE_KIB="$(df -Pk "$REPO_ROOT" | awk 'NR==2 {print $4}')"
[[ "$FREE_KIB" -ge 8388608 ]] || stop_build "There is not enough free disk space" "$((FREE_KIB/1024/1024)) GiB free" "at least 8 GiB free" "Free disk space without deleting repository inputs, then rerun."
if grep -q 'com.apple.developer.user-management\|com.apple.developer.icloud\|com.apple.security.application-groups' "$REPO_ROOT/tvos/CelesteTvOSRuntimeHost/Entitlements.plist"; then
  stop_build "A paid or out-of-scope entitlement is present" "forbidden entitlement in Entitlements.plist" "empty free-Personal-Team entitlements" "Remove the unrelated capability before using the self-builder."
fi
echo "  macOS $(sw_vers -productVersion), $HOST_ARCH; Xcode $(xcodebuild -version | head -1 | awk '{print $2}'); tvOS SDK $TVOS_SDK"
echo "  .NET $DOTNET_VERSION; workload set $WORKLOAD_SET; $((FREE_KIB/1024/1024)) GiB free"

if [[ "$CHECK_HOST" -eq 1 ]]; then
  echo
  echo "Host prerequisite check passed. Celeste, FMOD, signing, and building were not requested."
  rm -f -- "$LAST_ERROR"
  exit 0
fi

CURRENT_PHASE="Finding Celeste"
printf '[2/8] Finding Celeste\n'
GAME_ROOT="${GAME_ROOT_ARG:-${CELESTE_GAME_ROOT:-$(config_get celesteGameRoot)}}"
if [[ -z "$GAME_ROOT" && "$NON_INTERACTIVE" -eq 0 ]]; then
  read -r -p "  Drag the Celeste folder here, or paste its path: " answer
  GAME_ROOT="$(normalize_pasted_path "$answer")"
fi
[[ -n "$GAME_ROOT" ]] || stop_build "The Celeste installation was not provided" "no path" "your lawful unmodified itch.io Linux Celeste 1.4.0.0 folder" "Set CELESTE_GAME_ROOT or rerun and paste the extracted Linux folder path."
[[ -d "$GAME_ROOT" ]] || stop_build "The Celeste directory was not found" "the selected path does not exist" "a folder containing Celeste.exe, Celeste.Content.dll, FNA.dll, and Content" "Mount or locate your own game installation and rerun."
for item in Celeste.exe Celeste.Content.dll FNA.dll Content Celeste.png Content/Graphics/SplashScreen.png; do
  [[ -e "$GAME_ROOT/$item" ]] || stop_build "The Celeste installation is incomplete" "missing $item" "the tested itch.io Linux Celeste 1.4.0.0 files plus local artwork" "Select the extracted, unmodified itch.io Linux game installation, not Everest or a partial copy. Steam and other distributions are untested."
done
run_logged validate-celeste "$REPO_ROOT/scripts/validate-celeste-input.sh" --game-root "$GAME_ROOT" --output "$VALIDATION_ROOT/celeste-input.json"

CURRENT_PHASE="Finding FMOD"
printf '[3/8] Finding FMOD\n'
FMOD_ROOT="${FMOD_ROOT_ARG:-${FMOD_SDK_ROOT:-$(config_get fmodSdkRoot)}}"
if [[ -z "$FMOD_ROOT" ]]; then
  FMOD_ROOT="$(python3 - <<'PY'
import glob,pathlib
matches=[]
for candidate in glob.glob('/Volumes/*'):
    root=pathlib.Path(candidate)
    if (root/'doc/revision.txt').is_file() and (root/'api/lowlevel/lib/libfmod_appletvos.a').is_file(): matches.append(str(root))
print(matches[0] if len(matches)==1 else '')
PY
)"
fi
if [[ -z "$FMOD_ROOT" && "$NON_INTERACTIVE" -eq 0 ]]; then
  echo "  FMOD requires its own account. Mount the official FMOD Engine iOS 1.10.09 DMG."
  read -r -p "  Drag the mounted FMOD SDK root here, or paste its path: " answer
  FMOD_ROOT="$(normalize_pasted_path "$answer")"
fi
[[ -n "$FMOD_ROOT" && -d "$FMOD_ROOT" ]] || stop_build "The FMOD SDK was not found" "no valid mounted SDK path" "FMOD Engine iOS/tvOS 1.10.09 build 97915" "Download it from your FMOD account, mount the DMG, and rerun; copy nothing into Git."
run_logged validate-fmod "$REPO_ROOT/scripts/validate-fmod-tvos-sdk.sh" --sdk-root "$FMOD_ROOT" --output "$REPO_ROOT/.build/fmod-tvos/stage8a-validation/sdk-manifest.json"

MODE="${MODE:-$(config_get preferredMode)}"
if [[ -z "$MODE" && "$NON_INTERACTIVE" -eq 0 ]]; then
  cat <<'CHOICES'
  What would you like to do?
    1. Build, sign and install on an Apple TV
    2. Create a signing-ready unsigned IPA
    3. Build both
    4. Validate prerequisites only
CHOICES
  read -r -p "  Choice [1-4]: " choice
  case "$choice" in 1) MODE=install ;; 2) MODE=ipa ;; 3) MODE=both ;; 4) MODE=validate ;; *) stop_build "The build choice was not recognised" "$choice" "1, 2, 3, or 4" "Rerun and select one numbered choice." ;; esac
fi
if [[ -z "$MODE" && "$NON_INTERACTIVE" -eq 1 ]]; then
  stop_build "Non-interactive mode needs an explicit build choice" "no --mode value" "--mode install, ipa, both, or validate" "Add the intended --mode option and rerun."
fi
case "$MODE" in install|ipa|both|validate) ;; *) stop_build "The build mode is invalid" "$MODE" "install, ipa, both, or validate" "Use --mode with a supported value." ;; esac

BUNDLE_ID="${BUNDLE_ID_ARG:-$(config_get bundleIdentifier)}"
[[ -n "$BUNDLE_ID" ]] || BUNDLE_ID="$(local_prop ApplicationId)"
if [[ -z "$BUNDLE_ID" ]]; then
  local_name="$(id -un | LC_ALL=C tr '[:upper:]' '[:lower:]' | tr -cd 'a-z0-9-' | cut -c1-30)"
  [[ -n "$local_name" ]] || local_name=local
  BUNDLE_ID="com.$local_name.celeste-tvos"
fi
if [[ "$NON_INTERACTIVE" -eq 0 ]]; then
  echo "  Local bundle identifier: $BUNDLE_ID"
  read -r -p "  Press Enter to keep it, or type a replacement: " answer
  [[ -z "$answer" ]] || BUNDLE_ID="$answer"
fi
[[ "$BUNDLE_ID" =~ ^[A-Za-z][A-Za-z0-9-]*(\.[A-Za-z0-9-]+)+$ ]] || stop_build "The bundle identifier is invalid" "$BUNDLE_ID" "reverse-DNS form such as com.local-name.celeste-tvos" "Choose a unique identifier containing letters, digits, hyphens, and dots."
echo "  Warning: changing this identifier later creates another app identity and makes prior saves appear unavailable."

python3 - "$CONFIG_FILE" "$GAME_ROOT" "$FMOD_ROOT" "$BUNDLE_ID" "$MODE" <<'PY'
import json,pathlib,sys
path=pathlib.Path(sys.argv[1]); path.parent.mkdir(parents=True,exist_ok=True)
current={}
if path.is_file():
    try: current=json.loads(path.read_text())
    except Exception: current={}
current.update({"schemaVersion":1,"celesteGameRoot":sys.argv[2],"fmodSdkRoot":sys.argv[3],"bundleIdentifier":sys.argv[4],"preferredMode":sys.argv[5]})
path.write_text(json.dumps(current,indent=2,sort_keys=True)+"\n")
PY

CURRENT_PHASE="Checking Apple tooling"
printf '[4/8] Checking Apple tooling\n'
TEAM_ID="${TEAM_ID_ARG:-$(config_get developmentTeam)}"
[[ -n "$TEAM_ID" ]] || TEAM_ID="$(local_prop DevelopmentTeam)"
DEVICE_ID="${DEVICE_ID_ARG:-$(config_get preferredDevice)}"
if [[ "$MODE" == install || "$MODE" == both ]]; then
  TEAMS_FILE="$CONFIG_ROOT/teams-private.tsv"
  defaults export com.apple.dt.Xcode - 2>/dev/null | python3 -c 'import plistlib,sys; p=plistlib.loads(sys.stdin.buffer.read()); rows=[]
for groups in p.get("IDEProvisioningTeamByIdentifier",{}).values():
  for x in groups:
    if x.get("isFreeProvisioningTeam"): rows.append((x.get("teamID",""),x.get("teamName","Personal Team")))
print("\n".join("\t".join(r) for r in rows))' > "$TEAMS_FILE" || true
  [[ -s "$TEAMS_FILE" ]] || stop_build "Xcode has no free Personal Team" "no Personal Team account" "an Apple Account added to Xcode" "Open Xcode > Settings > Accounts, add your Apple Account, select its Personal Team, and rerun."
  if [[ -z "$TEAM_ID" ]]; then
    team_count="$(wc -l < "$TEAMS_FILE" | tr -d ' ')"
    if [[ "$team_count" -eq 1 ]]; then TEAM_ID="$(cut -f1 "$TEAMS_FILE")";
    elif [[ "$NON_INTERACTIVE" -eq 1 ]]; then stop_build "More than one Personal Team is available" "$team_count teams" "one explicit selection" "Rerun with --team-id for the intended Personal Team.";
    else
      echo "  Select a Personal Team:"
      nl -w2 -s'. ' "$TEAMS_FILE" | cut -f2-
      read -r -p "  Choice: " team_choice
      [[ "$team_choice" =~ ^[0-9]+$ && "$team_choice" -ge 1 && "$team_choice" -le "$team_count" ]] || stop_build "The Personal Team choice was not recognised" "$team_choice" "a number from 1 to $team_count" "Rerun and choose one of the displayed Personal Teams."
      TEAM_ID="$(sed -n "${team_choice}p" "$TEAMS_FILE" | cut -f1)"
    fi
  fi
  grep -q "^$TEAM_ID[[:space:]]" "$TEAMS_FILE" || stop_build "The selected team is not an available free Personal Team" "unavailable team selection" "a Personal Team shown by Xcode" "Select the intended Personal Team and rerun."
  DEVICES_JSON="$CONFIG_ROOT/devices-private.json"
  xcrun devicectl list devices --json-output "$DEVICES_JSON" > "$CONFIG_ROOT/devices-private.log" 2>&1
  DEVICES_FILE="$CONFIG_ROOT/apple-tvs-private.tsv"
  python3 - "$DEVICES_JSON" > "$DEVICES_FILE" <<'PY'
import json,pathlib,sys
items=json.loads(pathlib.Path(sys.argv[1]).read_text()).get("result",{}).get("devices",[])
for x in items:
    h=x.get("hardwareProperties",{}); c=x.get("connectionProperties",{})
    if h.get("reality")=="physical" and h.get("productType","").startswith("AppleTV") and c.get("pairingState")=="paired" and x.get("deviceProperties",{}).get("developerModeStatus")=="enabled":
        print(f"{x.get('identifier','')}\t{x.get('deviceProperties',{}).get('name','Apple TV')}\t{h.get('marketingName',h.get('productType','Apple TV'))}")
PY
  [[ -s "$DEVICES_FILE" ]] || stop_build "No paired developer-ready Apple TV was found" "zero available paired Apple TVs" "an Apple TV paired to Xcode with Developer Mode enabled" "On Apple TV enable Developer Mode, then in Xcode open Window > Devices and Simulators and pair it; rerun afterward."
  if [[ -z "$DEVICE_ID" ]]; then
    device_count="$(wc -l < "$DEVICES_FILE" | tr -d ' ')"
    if [[ "$device_count" -eq 1 ]]; then DEVICE_ID="$(cut -f1 "$DEVICES_FILE")";
    elif [[ "$NON_INTERACTIVE" -eq 1 ]]; then stop_build "More than one paired Apple TV is available" "$device_count devices" "one explicit device" "Rerun with --device-id for the intended Apple TV.";
    else
      echo "  Select an Apple TV:"
      nl -w2 -s'. ' "$DEVICES_FILE" | cut -f2-
      read -r -p "  Choice: " device_choice
      [[ "$device_choice" =~ ^[0-9]+$ && "$device_choice" -ge 1 && "$device_choice" -le "$device_count" ]] || stop_build "The Apple TV choice was not recognised" "$device_choice" "a number from 1 to $device_count" "Rerun and choose one of the displayed Apple TVs."
      DEVICE_ID="$(sed -n "${device_choice}p" "$DEVICES_FILE" | cut -f1)"
    fi
  fi
  grep -q "^$DEVICE_ID[[:space:]]" "$DEVICES_FILE" || stop_build "The selected Apple TV is unavailable" "unpaired or unavailable device" "an available paired Apple TV" "Wake and pair the Apple TV, then select it again."
  python3 - "$CONFIG_FILE" "$TEAM_ID" "$DEVICE_ID" <<'PY'
import json,pathlib,sys
p=pathlib.Path(sys.argv[1]); d=json.loads(p.read_text()); d.update({"developmentTeam":sys.argv[2],"preferredDevice":sys.argv[3]}); p.write_text(json.dumps(d,indent=2,sort_keys=True)+"\n")
PY
  echo "  Free Personal Team and paired Apple TV selected (private values remain ignored)."
else
  echo "  Signing is not required for mode: $MODE"
fi

CURRENT_PHASE="Generating artwork"
printf '[5/8] Generating artwork\n'
run_logged generate-artwork "$REPO_ROOT/scripts/generate-celeste-tvos-artwork.sh" --game-root "$GAME_ROOT" --output "$ARTWORK_ROOT" --clean

if [[ "$MODE" == validate ]]; then
  echo '[6/8] Preparing the game'
  echo '  skipped by validate-only mode'
  echo '[7/8] Building'
  echo '  skipped by validate-only mode'
  echo '[8/8] Packaging or installing'
  echo '  Validation passed. Choose install, ipa, or both on the next run.'
  exit 0
fi

CURRENT_PHASE="Preparing the game"
printf '[6/8] Preparing the game\n'
if [[ ! -f "$REPO_ROOT/.build/tvos-host/normalized-manifest.json" ]] || ! python3 - "$REPO_ROOT/.build/tvos-host/normalized-manifest.json" "$EXPECTED_STAGE1_HASH" <<'PY'
import json,pathlib,sys
raise SystemExit(0 if json.loads(pathlib.Path(sys.argv[1]).read_text()).get("logicalSetSha256")==sys.argv[2] else 1)
PY
then
  if [[ ! -f "$REPO_ROOT/artifacts/tvos-native/rebuild-e/normalized-manifest.json" ]] || ! python3 - "$REPO_ROOT/artifacts/tvos-native/rebuild-e/normalized-manifest.json" "$EXPECTED_STAGE1_HASH" <<'PY'
import json,pathlib,sys
raise SystemExit(0 if json.loads(pathlib.Path(sys.argv[1]).read_text()).get("logicalSetSha256")==sys.argv[2] else 1)
PY
  then
    run_logged fetch-native "$REPO_ROOT/scripts/fetch-tvos-deps.sh" --build-dir .build/tvos-native/self-build
    run_logged build-native "$REPO_ROOT/scripts/build-tvos-native.sh" --build-dir .build/tvos-native/self-build --output-dir artifacts/tvos-native/self-build --clean
    run_logged verify-native "$REPO_ROOT/scripts/verify-tvos-native.sh" --build-dir .build/tvos-native/self-build --output-dir artifacts/tvos-native/self-build
    stage1_artifacts=artifacts/tvos-native/self-build
    stage1_build=.build/tvos-native/self-build
  else
    stage1_artifacts=artifacts/tvos-native/rebuild-e
    stage1_build=.build/tvos-native/rebuild-e
  fi
  native_args=(--artifact-dir "$stage1_artifacts" --stage1-build-dir "$stage1_build")
  [[ -d "$REPO_ROOT/.build/tvos-host" ]] && native_args+=(--clean)
  run_logged prepare-host-native "$REPO_ROOT/scripts/prepare-tvos-host-native.sh" "${native_args[@]}"
else
  echo "  reusing accepted Stage 1 native set ($EXPECTED_STAGE1_HASH)"
fi

STAGE1_ARTIFACT_FOR_FMOD="$(python3 - "$REPO_ROOT/.build/tvos-host/staging-manifest.json" <<'PY'
import json,pathlib,sys
path=pathlib.Path(sys.argv[1])
if not path.is_file(): raise SystemExit("error: Stage 2 host staging manifest is missing")
value=json.loads(path.read_text()).get("stage1ArtifactDirectory","")
if not value: raise SystemExit("error: Stage 2 host staging does not identify its Stage 1 artifact set")
print(pathlib.Path(value).resolve())
PY
)"
case "$STAGE1_ARTIFACT_FOR_FMOD" in
  "$REPO_ROOT/artifacts/tvos-native/"*) ;;
  *) stop_build "The staged native artifact path is unsafe" "path outside artifacts/tvos-native" "the verified repository-local Stage 1 set" "Clean the Stage 8 cache and rerun." ;;
esac
[[ -f "$STAGE1_ARTIFACT_FOR_FMOD/normalized-manifest.json" ]] || stop_build "The staged native artifact set is unavailable" "missing normalized manifest" "the verified Stage 1 set used by host staging" "Clean the Stage 8 cache and rerun."

if ! "$REPO_ROOT/scripts/verify-fmod-tvos.sh" --stage-dir .build/fmod-tvos/current > "$LOG_ROOT/verify-fmod-stage.log" 2>&1; then
  fmod_args=(--sdk-root "$FMOD_ROOT" --game-root "$GAME_ROOT" --stage1-artifact-dir "$STAGE1_ARTIFACT_FOR_FMOD")
  [[ -d "$REPO_ROOT/.build/fmod-tvos/current" ]] && fmod_args+=(--clean)
  run_logged prepare-fmod "$REPO_ROOT/scripts/prepare-fmod-tvos.sh" "${fmod_args[@]}"
else
  echo "  reusing verified FMOD 1.10.09 device staging"
fi
if ! "$REPO_ROOT/scripts/verify-celeste-tvos-stage6.sh" > "$LOG_ROOT/verify-stage6-inputs.log" 2>&1; then
  stage6_args=(--game-root "$GAME_ROOT")
  [[ -d "$REPO_ROOT/.build/celeste-runtime/stage6-current" ]] && stage6_args+=(--clean)
  run_logged prepare-game "$REPO_ROOT/scripts/prepare-celeste-tvos-stage6.sh" "${stage6_args[@]}"
  run_logged verify-game "$REPO_ROOT/scripts/verify-celeste-tvos-stage6.sh"
else
  echo "  reusing verified Stage 6 generated managed/content inputs"
fi
run_logged verify-controller-prompt-assets "$REPO_ROOT/scripts/inventory-celeste-controller-prompts.py" --game-root "$GAME_ROOT"
run_logged verify-controller-prompt-source "$REPO_ROOT/scripts/verify-celeste-tvos-stage11.py" \
  --repo-root "$REPO_ROOT" \
  --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" \
  --output "$REPO_ROOT/artifacts/celeste-runtime/stage6-current/stage11-verification.json"
run_logged verify-graceful-quit-source "$REPO_ROOT/scripts/verify-celeste-tvos-stage12b.py" \
  --repo-root "$REPO_ROOT" \
  --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" \
  --output "$REPO_ROOT/artifacts/celeste-runtime/stage6-current/stage12b-verification.json"
run_logged verify-soft-reload-source "$REPO_ROOT/scripts/verify-celeste-tvos-stage13b.py" \
  --repo-root "$REPO_ROOT" \
  --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" \
  --output "$REPO_ROOT/artifacts/celeste-runtime/stage6-current/stage13b-verification.json"
run_logged verify-qr-pairing-source "$REPO_ROOT/scripts/verify-celeste-tvos-stage15.py" \
  --repo-root "$REPO_ROOT" \
  --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" \
  --output "$REPO_ROOT/artifacts/celeste-runtime/stage6-current/stage15-verification.json"
run_logged verify-performance-hud-source "$REPO_ROOT/scripts/verify-celeste-tvos-stage16b.py" \
  --repo-root "$REPO_ROOT" \
  --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" \
  --native-manifest "$REPO_ROOT/.build/tvos-host/normalized-manifest.json" \
  --output "$REPO_ROOT/artifacts/celeste-runtime/stage6-current/stage16b-verification.json"

COMMON_PUBLISH=(dotnet publish "$PROJECT" -c Release -r tvos-arm64 -m:1 -p:BuildInParallel=false
  -p:CelesteLaunchMode=CelesteAudio -p:Stage5BAudioScenario=normal
  -p:Stage6PersistenceEnabled=true -p:Stage6StorageNamespace=production
  -p:CelesteBrandingEnabled=true -p:CelesteBrandAssetsRoot="$ARTWORK_ROOT"
  -p:ApplicationId="$BUNDLE_ID" -p:UseInterpreter=false -p:RunAOTCompilation=true
  -p:PublishTrimmed=true -p:TrimMode=full -p:MtouchLink=Full)

# This privacy-safe logical key makes repeat builds reusable only when all
# locally supplied inputs, relevant repository sources, artwork, metadata, and
# accepted native/generated manifests still agree.
INPUT_KEY="$({
  git -C "$REPO_ROOT" rev-parse HEAD
  git -C "$REPO_ROOT" diff --no-ext-diff -- tvos scripts managed native global.json build-tvos.sh | shasum -a 256 | awk '{print $1}'
  shasum -a 256 "$REPO_ROOT/build-tvos.sh" "$REPO_ROOT/tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj" "$REPO_ROOT/tvos/CelesteTvOSRuntimeHost/Info.plist" | awk '{print $1}'
  shasum -a 256 "$VALIDATION_ROOT/celeste-input.json" "$REPO_ROOT/.build/fmod-tvos/stage8a-validation/sdk-manifest.json" "$REPO_ROOT/.build/tvos-host/normalized-manifest.json" "$REPO_ROOT/.build/fmod-tvos/current/logical-manifest.json" | awk '{print $1}'
  find "$REPO_ROOT/artifacts/celeste-runtime/stage6-current" -type f -name '*.json' -print0 | LC_ALL=C sort -z | xargs -0 shasum -a 256 | awk '{print $1}'
  find "$ARTWORK_ROOT" -type f -print0 | LC_ALL=C sort -z | xargs -0 shasum -a 256 | awk '{print $1}'
  printf '%s\n' "$BUNDLE_ID" 'Release' 'tvos-arm64' 'CelesteAudio' 'full-AOT' 'full-trim' 'no-interpreter'
} | shasum -a 256 | awk '{print $1}')"

CURRENT_PHASE="Building"
printf '[7/8] Building\n'
SIGNED_APP=""
UNSIGNED_APP=""
if [[ "$MODE" == ipa || "$MODE" == both ]]; then
  unsigned_key="$BUILD_ROOT/unsigned/self-build-input-key.txt"
  if [[ -f "$unsigned_key" && "$(<"$unsigned_key")" == "$INPUT_KEY:unsigned" ]]; then
    UNSIGNED_APP="$(locate_app "$BUILD_ROOT/unsigned" "$BUNDLE_ID" 2>/dev/null || true)"
    if [[ -n "$UNSIGNED_APP" ]] && "$REPO_ROOT/scripts/verify-celeste-tvos-stage8a.py" --app "$UNSIGNED_APP" --repo-root "$REPO_ROOT" > "$LOG_ROOT/reuse-unsigned.log" 2>&1; then
      echo "  reusing matching verified unsigned full-AOT build"
    else
      UNSIGNED_APP=""
    fi
  fi
  if [[ -z "$UNSIGNED_APP" ]]; then
    rm -rf -- "$BUILD_ROOT/unsigned"
    run_logged publish-unsigned "${COMMON_PUBLISH[@]}" --artifacts-path "$BUILD_ROOT/unsigned" -p:EnableCodeSigning=false
    UNSIGNED_APP="$(locate_app "$BUILD_ROOT/unsigned" "$BUNDLE_ID")"
    run_logged sanitize-unsigned "$REPO_ROOT/scripts/sanitize-dotnet-debug-paths.py" --app "$UNSIGNED_APP" --private-root "$REPO_ROOT"
    printf '%s\n' "$INPUT_KEY:unsigned" > "$BUILD_ROOT/unsigned/self-build-input-key.txt"
  fi
fi
if [[ "$MODE" == install || "$MODE" == both ]]; then
  run_logged provision-personal-team "$REPO_ROOT/scripts/configure-tvos-personal-team.sh" --team-id "$TEAM_ID" --bundle-id "$BUNDLE_ID" --device-id "$DEVICE_ID"
  signed_key="$BUILD_ROOT/signed/self-build-input-key.txt"
  if [[ -f "$signed_key" && "$(<"$signed_key")" == "$INPUT_KEY:signed" ]]; then
    SIGNED_APP="$(locate_app "$BUILD_ROOT/signed" "$BUNDLE_ID" 2>/dev/null || true)"
    if [[ -n "$SIGNED_APP" ]] && "$REPO_ROOT/scripts/verify-celeste-tvos-stage8a.py" --app "$SIGNED_APP" --signed --repo-root "$REPO_ROOT" > "$LOG_ROOT/reuse-signed.log" 2>&1; then
      echo "  reusing matching verified signed full-AOT build"
    else
      SIGNED_APP=""
    fi
  fi
  if [[ -z "$SIGNED_APP" ]]; then
    rm -rf -- "$BUILD_ROOT/signed"
    run_logged publish-signed "${COMMON_PUBLISH[@]}" --artifacts-path "$BUILD_ROOT/signed" -p:EnableCodeSigning=true -p:DevelopmentTeam="$TEAM_ID" -p:CodesignKey="Apple Development" -p:CodesignProvision=Automatic -p:ProvisioningType=automatic
    SIGNED_APP="$(locate_app "$BUILD_ROOT/signed" "$BUNDLE_ID")"
    run_logged sanitize-resign-signed "$REPO_ROOT/scripts/sanitize-dotnet-debug-paths.py" --app "$SIGNED_APP" --private-root "$REPO_ROOT" --resign-team "$TEAM_ID"
    printf '%s\n' "$INPUT_KEY:signed" > "$BUILD_ROOT/signed/self-build-input-key.txt"
  fi
fi

CURRENT_PHASE="Packaging or installing"
printf '[8/8] Packaging or installing\n'
rm -f -- "$DIST_ROOT/SHA256SUMS" "$DIST_ROOT/build-summary.txt"
if [[ -n "$UNSIGNED_APP" ]]; then
  rm -rf -- "$DIST_ROOT/Celeste.app" "$CONFIG_ROOT/ipa-root"
  mkdir -p "$CONFIG_ROOT/ipa-root/Payload"
  ditto --norsrc "$UNSIGNED_APP" "$DIST_ROOT/Celeste.app"
  ditto --norsrc "$UNSIGNED_APP" "$CONFIG_ROOT/ipa-root/Payload/Celeste.app"
  rm -rf -- "$DIST_ROOT/Celeste.app/_CodeSignature"
  rm -f -- "$DIST_ROOT/Celeste.app/embedded.mobileprovision"
  rm -rf -- "$CONFIG_ROOT/ipa-root/Payload/Celeste.app/_CodeSignature"
  rm -f -- "$CONFIG_ROOT/ipa-root/Payload/Celeste.app/embedded.mobileprovision"
  IPA="$DIST_ROOT/Celeste-tvOS-unsigned.ipa"
  rm -f -- "$IPA"
  (cd "$CONFIG_ROOT/ipa-root" && ditto -c -k --norsrc --keepParent Payload "$IPA")
  run_logged verify-unsigned-ipa "$REPO_ROOT/scripts/verify-celeste-tvos-stage8a.py" --ipa "$IPA" --repo-root "$REPO_ROOT" --output "$REPO_ROOT/artifacts/tvos-self-build/unsigned-verification.json"
  run_logged verify-stage11-unsigned "$REPO_ROOT/scripts/verify-celeste-tvos-stage11.py" --repo-root "$REPO_ROOT" --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" --ipa "$IPA"
  run_logged verify-stage12b-unsigned "$REPO_ROOT/scripts/verify-celeste-tvos-stage12b.py" --repo-root "$REPO_ROOT" --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" --ipa "$IPA"
  run_logged verify-stage13b-unsigned "$REPO_ROOT/scripts/verify-celeste-tvos-stage13b.py" --repo-root "$REPO_ROOT" --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" --ipa "$IPA"
  run_logged verify-stage15-unsigned "$REPO_ROOT/scripts/verify-celeste-tvos-stage15.py" --repo-root "$REPO_ROOT" --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" --ipa "$IPA"
  run_logged verify-stage16b-unsigned "$REPO_ROOT/scripts/verify-celeste-tvos-stage16b.py" --repo-root "$REPO_ROOT" --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" --native-manifest "$REPO_ROOT/.build/tvos-host/normalized-manifest.json" --ipa "$IPA"
  IPA_HASH="$(shasum -a 256 "$IPA" | awk '{print $1}')"
  IPA_BYTES="$(stat -f %z "$IPA")"
  printf '%s  %s\n' "$IPA_HASH" "Celeste-tvOS-unsigned.ipa" >> "$DIST_ROOT/SHA256SUMS"
  echo "  Signing-ready unsigned IPA: $IPA"
  echo "  Size: $((IPA_BYTES/1024/1024)) MiB"
  echo "  SHA-256: $IPA_HASH"
  echo "  Bundle identifier: $BUNDLE_ID"
  echo "  Payload: Celeste.app, arm64 TVOS, minimum tvOS 16.0"
  echo "  It contains no usable signature or profile and is not installable as-is."
  echo "  A tvOS-capable sideloading tool such as atvloadly or Sideloadly must sign it."
fi

INSTALL_RESULT=not-requested
if [[ -n "$SIGNED_APP" ]]; then
  rm -rf -- "$DIST_ROOT/Celeste.app"
  ditto --norsrc "$SIGNED_APP" "$DIST_ROOT/Celeste.app"
  run_logged verify-signed-app "$REPO_ROOT/scripts/verify-celeste-tvos-stage8a.py" --app "$DIST_ROOT/Celeste.app" --signed --repo-root "$REPO_ROOT" --output "$REPO_ROOT/artifacts/tvos-self-build/signed-verification.json"
  run_logged verify-stage11-signed "$REPO_ROOT/scripts/verify-celeste-tvos-stage11.py" --repo-root "$REPO_ROOT" --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" --app "$DIST_ROOT/Celeste.app"
  run_logged verify-stage12b-signed "$REPO_ROOT/scripts/verify-celeste-tvos-stage12b.py" --repo-root "$REPO_ROOT" --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" --app "$DIST_ROOT/Celeste.app"
  run_logged verify-stage13b-signed "$REPO_ROOT/scripts/verify-celeste-tvos-stage13b.py" --repo-root "$REPO_ROOT" --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" --app "$DIST_ROOT/Celeste.app"
  run_logged verify-stage15-signed "$REPO_ROOT/scripts/verify-celeste-tvos-stage15.py" --repo-root "$REPO_ROOT" --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" --app "$DIST_ROOT/Celeste.app"
  run_logged verify-stage16b-signed "$REPO_ROOT/scripts/verify-celeste-tvos-stage16b.py" --repo-root "$REPO_ROOT" --generated-root "$REPO_ROOT/.build/celeste-runtime/stage6-current/audio/managed" --native-manifest "$REPO_ROOT/.build/tvos-host/normalized-manifest.json" --app "$DIST_ROOT/Celeste.app"
  run_logged install-apple-tv xcrun devicectl device install app --quiet --device "$DEVICE_ID" "$DIST_ROOT/Celeste.app" --json-output "$CONFIG_ROOT/install-private.json" --log-output "$CONFIG_ROOT/install-private.log"
  CONSOLE="$CONFIG_ROOT/launch-console-private.log"
  xcrun devicectl device process launch --console --terminate-existing --timeout 120 --device "$DEVICE_ID" "$BUNDLE_ID" --json-output "$CONFIG_ROOT/launch-private.json" --log-output "$CONFIG_ROOT/launch-tool-private.log" > "$CONSOLE" 2>&1 &
  launch_pid=$!
  launch_ok=0
  for _ in $(seq 1 60); do
    if grep -Fq 'name=banks-ready; banks=7;' "$CONSOLE" 2>/dev/null && grep -Fq 'name=first-celeste-draw' "$CONSOLE" 2>/dev/null; then launch_ok=1; break; fi
    kill -0 "$launch_pid" 2>/dev/null || break
    sleep 2
  done
  kill -TERM "$launch_pid" 2>/dev/null || true
  for _ in 1 2 3 4 5; do
    kill -0 "$launch_pid" 2>/dev/null || break
    sleep 1
  done
  kill -KILL "$launch_pid" 2>/dev/null || true
  wait "$launch_pid" 2>/dev/null || true
  [[ "$launch_ok" -eq 1 ]] || stop_build "The installed app did not reach the real Celeste/audio startup checkpoints" "see ignored launch evidence" "seven FMOD banks and the first Celeste draw" "Wake the Apple TV, confirm the controller is available, and inspect dist/logs plus the ignored launch log."
  # devicectl forwards termination signals from its attached --console process
  # to the app. Start one clean, detached foreground instance after collecting
  # the bounded checkpoints so the builder does not hand the user an empty app
  # presentation after a successful installation.
  run_logged relaunch-apple-tv xcrun devicectl device process launch --quiet --terminate-existing --device "$DEVICE_ID" "$BUNDLE_ID"
  INSTALL_RESULT=passed
  echo "  Installed and launched Celeste; real FMOD banks and first Celeste draw confirmed."
  cat <<'NOTICE'

Personal Team installations expire after 7 days.
Run ./build-tvos.sh again to re-sign and reinstall.
Keep the same bundle identifier to retain the same app identity and saves.
NOTICE
fi

APP_BYTES=0
[[ -d "$DIST_ROOT/Celeste.app" ]] && APP_BYTES="$(du -sk "$DIST_ROOT/Celeste.app" | awk '{print $1*1024}')"
cat > "$DIST_ROOT/build-summary.txt" <<EOF
Celeste for Apple TV self-build summary
commit: $(git -C "$REPO_ROOT" rev-parse HEAD)
macOS: $(sw_vers -productVersion)
host architecture: $HOST_ARCH
Xcode: $(xcodebuild -version | head -1)
tvOS SDK: $TVOS_SDK
.NET SDK: $DOTNET_VERSION
workload set: $WORKLOAD_SET
Celeste input: 1.4.0.0 (validated; source path omitted)
FMOD input: 1.10.09 build 97915 (validated; source path omitted)
output type: $MODE
display name: Celeste
bundle identifier: <LOCAL_BUNDLE_ID>
platform: TVOS arm64
minimum deployment target: 16.0
full AOT: true
interpreter: false
trimming: full
app bytes: $APP_BYTES
installation result: $INSTALL_RESULT
unsigned IPA: $([[ -n "${IPA:-}" ]] && echo present || echo not-requested)
unsigned IPA SHA-256: ${IPA_HASH:-not-requested}
private signing/device values: omitted
EOF
echo "  Privacy-safe summary: $DIST_ROOT/build-summary.txt"
echo "  Complete logs: $DIST_ROOT/logs/"
rm -f -- "$LAST_ERROR"
