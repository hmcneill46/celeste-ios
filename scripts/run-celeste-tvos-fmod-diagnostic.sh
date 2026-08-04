#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/run-celeste-tvos-fmod-diagnostic.sh --app DIR [options]

Install and run the signed Stage 5A FmodDiagnostic app on the unique paired
physical AppleTV14,1. The first launch performs real FMOD music/SFX,
volume/mute/pause, and an automated background/foreground cycle. A complete
second launch follows. The user then records audible confirmation.

Options:
  --app DIR           Signed arm64 tvOS FmodDiagnostic app (required).
  --device-id ID      Ignored local device ID; otherwise discover the unique
                      paired AppleTV14,1 dynamically.
  --duration SECONDS  Per-launch console timeout (default: 240, range: 120-600).
  --evidence-dir DIR  Ignored evidence directory
                      (default: artifacts/fmod-tvos/device-acceptance).
  -h, --help          Show this help.

The script installs no tools and never prints device, bundle, account, Team,
certificate, or provisioning identifiers. Raw CoreDevice output remains only
inside the ignored evidence directory.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP_DIR=""
DEVICE_ID=""
DURATION=240
EVIDENCE_DIR="$REPO_ROOT/artifacts/fmod-tvos/device-acceptance"
LAUNCH_PID=""
EXECUTABLE_NAME=""
BUNDLE_ID=""

repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --app) [[ $# -ge 2 ]] || exit 2; APP_DIR="$(repo_path "$2")"; shift 2 ;;
    --device-id) [[ $# -ge 2 ]] || exit 2; DEVICE_ID="$2"; shift 2 ;;
    --duration) [[ $# -ge 2 ]] || exit 2; DURATION="$2"; shift 2 ;;
    --evidence-dir) [[ $# -ge 2 ]] || exit 2; EVIDENCE_DIR="$(repo_path "$2")"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ -d "$APP_DIR" ]] || { echo "error: --app must name a signed app bundle" >&2; exit 2; }
[[ "$DURATION" =~ ^[0-9]+$ && "$DURATION" -ge 120 && "$DURATION" -le 600 ]] || {
  echo "error: --duration must be 120-600 seconds" >&2
  exit 2
}
for required_tool in xcrun python3 plutil codesign git; do
  command -v "$required_tool" >/dev/null || { echo "error: missing existing tool: $required_tool" >&2; exit 1; }
done
case "$EVIDENCE_DIR" in "$REPO_ROOT/artifacts/fmod-tvos/"*|"$REPO_ROOT/.build/fmod-tvos/"*) ;; *)
  echo "error: evidence directory must remain inside an ignored Stage 5 root" >&2; exit 2 ;;
esac
relative_evidence="${EVIDENCE_DIR#$REPO_ROOT/}"
git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative_evidence/console.log" || {
  echo "error: selected evidence directory is not ignored" >&2
  exit 1
}

"$REPO_ROOT/scripts/verify-fmod-tvos.sh" --app "$APP_DIR" --platform tvos >/dev/null
codesign --verify --deep --strict "$APP_DIR"
mkdir -p "$EVIDENCE_DIR"
rm -f -- "$EVIDENCE_DIR"/*.log "$EVIDENCE_DIR"/*.json "$EVIDENCE_DIR"/*.plist

devices_json="$EVIDENCE_DIR/devices-private.json"
xcrun devicectl list devices --json-output "$devices_json" > "$EVIDENCE_DIR/device-list-private.log" 2>&1
if [[ -z "$DEVICE_ID" ]]; then
  DEVICE_ID="$(python3 - "$devices_json" <<'PY'
import json, pathlib, sys
devices = json.loads(pathlib.Path(sys.argv[1]).read_text()).get("result", {}).get("devices", [])
matches = [
    item["identifier"] for item in devices
    if item.get("hardwareProperties", {}).get("productType") == "AppleTV14,1"
    and item.get("hardwareProperties", {}).get("reality") == "physical"
    and item.get("connectionProperties", {}).get("pairingState") == "paired"
]
if len(matches) != 1:
    raise SystemExit(f"error: expected one paired physical AppleTV14,1, found {len(matches)}")
print(matches[0])
PY
)"
fi
BUNDLE_ID="$(plutil -extract CFBundleIdentifier raw "$APP_DIR/Info.plist")"
EXECUTABLE_NAME="$(plutil -extract CFBundleExecutable raw "$APP_DIR/Info.plist")"

terminate_device_app() {
  local label="$1" processes="$EVIDENCE_DIR/$1-processes-private.json" remote_pid=""
  xcrun devicectl device info processes --quiet --device "$DEVICE_ID" --json-output "$processes" \
    --log-output "$EVIDENCE_DIR/$label-processes-private.log" >/dev/null 2>&1 || return 0
  remote_pid="$(python3 - "$processes" "$EXECUTABLE_NAME" <<'PY'
import json, pathlib, sys, urllib.parse
processes = json.loads(pathlib.Path(sys.argv[1]).read_text()).get("result", {}).get("runningProcesses", [])
matches = []
for process in processes:
    executable = urllib.parse.unquote(process.get("executable", "")).rstrip("/").rsplit("/", 1)[-1]
    if executable == sys.argv[2]:
        matches.append(str(process["processIdentifier"]))
if len(matches) > 1:
    raise SystemExit("error: multiple diagnostic processes are running")
print(matches[0] if matches else "")
PY
)"
  if [[ -n "$remote_pid" ]]; then
    xcrun devicectl device process terminate --quiet --device "$DEVICE_ID" --pid "$remote_pid" \
      --json-output "$EVIDENCE_DIR/$label-terminate-private.json" \
      --log-output "$EVIDENCE_DIR/$label-terminate-private.log" >/dev/null 2>&1 || true
  fi
}

finish_console_capture() {
  for _ in {1..5}; do
    kill -0 "$LAUNCH_PID" 2>/dev/null || break
    sleep 1
  done
  if kill -0 "$LAUNCH_PID" 2>/dev/null; then
    kill -TERM "$LAUNCH_PID" 2>/dev/null || true
    for _ in {1..3}; do kill -0 "$LAUNCH_PID" 2>/dev/null || break; sleep 1; done
    if kill -0 "$LAUNCH_PID" 2>/dev/null; then kill -KILL "$LAUNCH_PID" 2>/dev/null || true; fi
  fi
  wait "$LAUNCH_PID" 2>/dev/null || true
  LAUNCH_PID=""
}

cleanup() {
  if [[ -n "$LAUNCH_PID" ]]; then
    terminate_device_app cleanup
    kill -TERM "$LAUNCH_PID" 2>/dev/null || true
    sleep 1
    if kill -0 "$LAUNCH_PID" 2>/dev/null; then kill -KILL "$LAUNCH_PID" 2>/dev/null || true; fi
    wait "$LAUNCH_PID" 2>/dev/null || true
    LAUNCH_PID=""
  fi
}
trap cleanup EXIT INT TERM

wait_for_log() {
  local log_file="$1" token="$2" attempts="$3"
  for ((attempt=0; attempt<attempts; attempt++)); do
    grep -Fq "$token" "$log_file" 2>/dev/null && return 0
    [[ -z "$LAUNCH_PID" ]] || kill -0 "$LAUNCH_PID" 2>/dev/null || return 1
    sleep 2
  done
  return 1
}

verify_launch_log() {
  local log_file="$1" require_lifecycle="$2" label="$3"
  python3 - "$log_file" "$require_lifecycle" "$label" <<'PY'
import pathlib, sys
text = pathlib.Path(sys.argv[1]).read_text(errors="replace")
required = [
    "native=0x00011009; managed=0x00011014",
    "name=low-level-initialized result=OK",
    "name=studio-initialized result=OK",
    "name=fmod-sdl-registered target=standalone-low-level",
    "name=fmod-sdl-registered target=studio-low-level",
    "name=bank-loaded file=Master Bank.bank",
    "name=bank-loaded file=Master Bank.strings.bank",
    "name=events-selected music=event:/music/",
    "name=playback-state category=music; state=PLAYING",
    "name=playback-state category=sfx; state=PLAYING",
    "name=master-volume-lowered",
    "name=master-muted",
    "name=master-unmuted",
    "name=volume category=sfx; requested=0.50; readback=0.50",
    "name=volume category=sfx; requested=1.00; readback=1.00",
    "name=master-paused",
    "FMOD_DIAGNOSTIC_SUMMARY low-level=PASS studio=PASS banks=PASS music-playing=PASS sfx-playing=PASS volume=PASS sfx-volume=PASS",
    "name=studio-shutdown reason=normal-completion; unload=OK; release=OK",
    "name=clean-process-exit",
]
for token in required:
    if token not in text:
        raise SystemExit(f"error: {sys.argv[3]} lacks checkpoint: {token}")
if sys.argv[2] == "yes":
    for token in ("name=lifecycle-background playback-stopped=true", "name=lifecycle-active playback-restarted=true", "background=True; foreground=True"):
        if token not in text:
            raise SystemExit(f"error: first launch lacks lifecycle checkpoint: {token}")
for forbidden in ("name=fatal", "unhandled managed exception", "ERR_VERSION", "ERR_HEADER_MISMATCH", "ERR_OUTPUT_INIT"):
    if forbidden in text:
        raise SystemExit(f"error: {sys.argv[3]} contains failure marker: {forbidden}")
PY
}

run_once() {
  local label="$1" run_lifecycle="$2" console="$EVIDENCE_DIR/$1-console.log"
  xcrun devicectl device process launch --console --terminate-existing --timeout "$DURATION" \
    --device "$DEVICE_ID" "$BUNDLE_ID" \
    --json-output "$EVIDENCE_DIR/$label-launch-private.json" \
    --log-output "$EVIDENCE_DIR/$label-launch-tool-private.log" > "$console" 2>&1 &
  LAUNCH_PID=$!
  wait_for_log "$console" "lifecycle launch: SDL main callback" 20 || {
    echo "error: $label did not enter SDL; inspect ignored console" >&2
    return 1
  }
  xcrun devicectl device process launch --quiet --device "$DEVICE_ID" "$BUNDLE_ID" \
    --json-output "$EVIDENCE_DIR/$label-activate-private.json" \
    --log-output "$EVIDENCE_DIR/$label-activate-private.log" >/dev/null 2>&1
  wait_for_log "$console" "name=lifecycle-ready" 75 || {
    echo "error: $label did not finish music/SFX/control setup; inspect ignored console" >&2
    return 1
  }
  if [[ "$run_lifecycle" == "yes" ]]; then
    xcrun devicectl device process launch --quiet --device "$DEVICE_ID" com.apple.TVSettings \
      --json-output "$EVIDENCE_DIR/$label-background-private.json" \
      --log-output "$EVIDENCE_DIR/$label-background-private.log" >/dev/null 2>&1
    sleep 5
    xcrun devicectl device process launch --quiet --device "$DEVICE_ID" "$BUNDLE_ID" \
      --json-output "$EVIDENCE_DIR/$label-foreground-private.json" \
      --log-output "$EVIDENCE_DIR/$label-foreground-private.log" >/dev/null 2>&1
  fi
  wait_for_log "$console" "name=clean-process-exit" 45 || {
    echo "error: $label did not shut down cleanly; inspect ignored console" >&2
    return 1
  }
  terminate_device_app "$label"
  finish_console_capture
  verify_launch_log "$console" "$run_lifecycle" "$label"
  echo "PASS: $label machine-observed FMOD sequence"
}

echo "FMOD LISTENING TEST"
echo "First: a music event should play, become quieter, mute briefly, then return."
echo "Next: a short SFX/UI event should play at half bus volume. The app will background and return automatically."
echo "Keep the television/receiver at a safe audible volume; no platforming is required."

xcrun devicectl device install app --quiet --device "$DEVICE_ID" "$APP_DIR" \
  --json-output "$EVIDENCE_DIR/install-private.json" \
  --log-output "$EVIDENCE_DIR/install-private.log" >/dev/null 2>&1
run_once first-launch yes
run_once second-launch no

read -r -p "Was the music audible? Enter yes or no: " music_response
read -r -p "Was the SFX/UI event audible? Enter yes or no: " sfx_response
read -r -p "Was either sound distorted? Enter yes or no: " distorted_response
for response_name in music_response sfx_response distorted_response; do
  response_value="${!response_name}"
  case "$response_value" in yes|no) ;; *) echo "error: audible responses must be yes or no" >&2; exit 1 ;; esac
done

python3 - "$EVIDENCE_DIR/audible-confirmation.json" "$music_response" "$sfx_response" "$distorted_response" <<'PY'
import json, pathlib, sys
pathlib.Path(sys.argv[1]).write_text(json.dumps({
    "schemaVersion": 1,
    "musicAudible": sys.argv[2] == "yes",
    "sfxAudible": sys.argv[3] == "yes",
    "distorted": sys.argv[4] == "yes",
    "scope": "user-observed Stage 5A physical diagnostic only",
}, indent=2, sort_keys=True) + "\n")
PY

[[ "$music_response" == "yes" ]] || { echo "error: user did not confirm audible music" >&2; exit 1; }
[[ "$sfx_response" == "yes" ]] || { echo "error: user did not confirm audible SFX" >&2; exit 1; }
[[ "$distorted_response" == "no" ]] || { echo "error: user reported distorted output" >&2; exit 1; }

echo "PASS: physical FMOD diagnostic, lifecycle, clean second launch, and audible confirmation"
echo "private device/signing data and raw evidence remain ignored"
