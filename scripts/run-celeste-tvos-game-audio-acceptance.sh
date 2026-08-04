#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/run-celeste-tvos-game-audio-acceptance.sh --app DIR [options]

Install and run a signed Stage 5B CelesteAudio app on the unique paired
physical AppleTV14,1 while retaining raw console/device data only in an
ignored evidence directory.

Options:
  --app DIR           Signed arm64 CelesteAudio app (required).
  --test NAME         normal, lifecycle, prologue-normal, prologue-skip, or second-launch
                      (default: normal).
  --device-id ID      Ignored local device ID; otherwise discover the unique
                      paired AppleTV14,1 dynamically.
  --duration SECONDS  Console timeout (default: 600; range: 180-900).
  --evidence-dir DIR  Ignored directory (default is selected from --test).
  -h, --help          Show this help.

The two Prologue modes are automated Stage 3C routes and require an app built
with matching Stage5BAudioScenario. The normal route pauses at a concise manual
gameplay/listening checklist, then automatically checks background/foreground
and records yes/no/uncertain answers. No private identifier is printed.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP_DIR=""
TEST="normal"
DEVICE_ID=""
DURATION=600
EVIDENCE_DIR=""
LAUNCH_PID=""
EXECUTABLE_NAME=""
BUNDLE_ID=""

repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --app) [[ $# -ge 2 ]] || exit 2; APP_DIR="$(repo_path "$2")"; shift 2 ;;
    --test) [[ $# -ge 2 ]] || exit 2; TEST="$2"; shift 2 ;;
    --device-id) [[ $# -ge 2 ]] || exit 2; DEVICE_ID="$2"; shift 2 ;;
    --duration) [[ $# -ge 2 ]] || exit 2; DURATION="$2"; shift 2 ;;
    --evidence-dir) [[ $# -ge 2 ]] || exit 2; EVIDENCE_DIR="$(repo_path "$2")"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ -d "$APP_DIR" ]] || { echo "error: --app must name a signed app bundle" >&2; exit 2; }
case "$TEST" in normal|lifecycle|prologue-normal|prologue-skip|second-launch) ;; *) echo "error: unsupported --test" >&2; exit 2 ;; esac
[[ "$DURATION" =~ ^[0-9]+$ && "$DURATION" -ge 180 && "$DURATION" -le 900 ]] || { echo "error: --duration must be 180-900 seconds" >&2; exit 2; }
[[ -n "$EVIDENCE_DIR" ]] || EVIDENCE_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage5b-device-$TEST"
for command in xcrun python3 plutil codesign git; do command -v "$command" >/dev/null || { echo "error: missing existing tool: $command" >&2; exit 1; }; done
case "$EVIDENCE_DIR" in "$REPO_ROOT/artifacts/celeste-runtime/"*|"$REPO_ROOT/.build/celeste-runtime/"*) ;; *)
  echo "error: evidence must remain in an ignored Celeste runtime directory" >&2; exit 2 ;;
esac
relative_evidence="${EVIDENCE_DIR#$REPO_ROOT/}"
git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative_evidence/console.log" || { echo "error: evidence directory is not ignored" >&2; exit 1; }

"$REPO_ROOT/scripts/verify-celeste-tvos-stage5b.sh" --app "$APP_DIR" >/dev/null
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
items = json.loads(pathlib.Path(sys.argv[1]).read_text()).get("result", {}).get("runningProcesses", [])
matches = [str(item["processIdentifier"]) for item in items
           if urllib.parse.unquote(item.get("executable", "")).rstrip("/").rsplit("/", 1)[-1] == sys.argv[2]]
if len(matches) > 1: raise SystemExit("error: multiple host processes are running")
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
  for _ in {1..5}; do kill -0 "$LAUNCH_PID" 2>/dev/null || break; sleep 1; done
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
  local token="$1" attempts="$2"
  for ((attempt=0; attempt<attempts; attempt++)); do
    grep -Fq "$token" "$EVIDENCE_DIR/console.log" 2>/dev/null && return 0
    kill -0 "$LAUNCH_PID" 2>/dev/null || return 1
    sleep 2
  done
  return 1
}

xcrun devicectl device install app --quiet --device "$DEVICE_ID" "$APP_DIR" \
  --json-output "$EVIDENCE_DIR/install-private.json" --log-output "$EVIDENCE_DIR/install-private.log" >/dev/null 2>&1
xcrun devicectl device process launch --console --terminate-existing --timeout "$DURATION" \
  --device "$DEVICE_ID" "$BUNDLE_ID" \
  --json-output "$EVIDENCE_DIR/launch-private.json" --log-output "$EVIDENCE_DIR/launch-tool-private.log" \
  > "$EVIDENCE_DIR/console.log" 2>&1 &
LAUNCH_PID=$!
wait_for_log "name=banks-ready; banks=7;" 90 || { echo "error: real Celeste audio did not load all banks" >&2; exit 1; }
xcrun devicectl device process launch --quiet --device "$DEVICE_ID" "$BUNDLE_ID" \
  --json-output "$EVIDENCE_DIR/activate-private.json" --log-output "$EVIDENCE_DIR/activate-private.log" >/dev/null 2>&1

if [[ "$TEST" == prologue-* ]]; then
  wait_for_log "name=diagnostic-clean-exit-requested" 180 || { echo "error: automated Prologue route did not finish" >&2; exit 1; }
  wait_for_log "name=audio-shutdown-completed" 20 || { echo "error: automated route did not shut down Studio cleanly" >&2; exit 1; }
  terminate_device_app "$TEST"
  finish_console_capture
  stage3c_scenario="${TEST#prologue-}"
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage3c-evidence.py" \
    --log "$EVIDENCE_DIR/console.log" --scenario "$stage3c_scenario" --platform device \
    --output "$EVIDENCE_DIR/stage3c-summary.json" >/dev/null
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage5b-evidence.py" \
    --log "$EVIDENCE_DIR/console.log" --scenario "$TEST" --output "$EVIDENCE_DIR/audio-summary.json"
  echo "PASS: physical $TEST progression, real triggerCue, sustained frames, haptics, and clean FMOD shutdown"
  exit 0
fi

if [[ "$TEST" == "second-launch" ]]; then
  echo "SECOND CLEAN LAUNCH READY; observing real title music and FMOD heartbeats for 70 seconds"
  sleep 70
  terminate_device_app "$TEST"
  finish_console_capture
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage5b-evidence.py" \
    --log "$EVIDENCE_DIR/console.log" --scenario second-launch --output "$EVIDENCE_DIR/audio-summary.json"
  echo "PASS: clean second physical launch retained real music and update heartbeats"
  exit 0
fi

if [[ "$TEST" == "lifecycle" ]]; then
  echo "NORMAL-GAME AUDIO LIFECYCLE TEST; observing title audio before backgrounding"
  sleep 35
  xcrun devicectl device process launch --quiet --device "$DEVICE_ID" com.apple.TVSettings \
    --json-output "$EVIDENCE_DIR/background-private.json" --log-output "$EVIDENCE_DIR/background-private.log" >/dev/null 2>&1
  wait_for_log "name=lifecycle-audio-paused; reason=resign-active; readback=true" 20 || {
    echo "error: normal-game audio did not report paused root bus on resign-active" >&2; exit 1;
  }
  sleep 5
  xcrun devicectl device process launch --quiet --device "$DEVICE_ID" "$BUNDLE_ID" \
    --json-output "$EVIDENCE_DIR/foreground-private.json" --log-output "$EVIDENCE_DIR/foreground-private.log" >/dev/null 2>&1
  wait_for_log "name=lifecycle-audio-resumed; reason=active; readback=false" 20 || {
    echo "error: normal-game audio did not report resumed root bus on active" >&2; exit 1;
  }
  sleep 15
  terminate_device_app lifecycle
  finish_console_capture
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage5b-evidence.py" \
    --log "$EVIDENCE_DIR/console.log" --scenario lifecycle --require-lifecycle \
    --output "$EVIDENCE_DIR/audio-summary.json"
  echo "PASS: normal-game real audio paused and resumed across physical background/foreground"
  exit 0
fi

echo "READY FOR MANUAL CELESTE GAME AUDIO TEST"
echo "1. Confirm title/menu music and UI navigation sounds."
echo "2. In Settings, move Music and SFX through 0, an intermediate value, and 10; restore both to 10."
echo "3. Start normal gameplay; exercise movement, Jump, Dash, Grab, Pause/resume, and one rumble."
echo "4. Trigger one death/respawn and confirm music does not duplicate."
echo "5. If available, exercise cutscene skip. Do not test long platforming solely for this runner."
read -r -p "When complete, press Enter; background/foreground will then run automatically. " _

xcrun devicectl device process launch --quiet --device "$DEVICE_ID" com.apple.TVSettings \
  --json-output "$EVIDENCE_DIR/background-private.json" --log-output "$EVIDENCE_DIR/background-private.log" >/dev/null 2>&1
sleep 5
xcrun devicectl device process launch --quiet --device "$DEVICE_ID" "$BUNDLE_ID" \
  --json-output "$EVIDENCE_DIR/foreground-private.json" --log-output "$EVIDENCE_DIR/foreground-private.log" >/dev/null 2>&1
sleep 15
terminate_device_app normal
finish_console_capture

"$REPO_ROOT/scripts/verify-celeste-tvos-stage5b-evidence.py" \
  --log "$EVIDENCE_DIR/console.log" --scenario normal --require-lifecycle \
  --output "$EVIDENCE_DIR/audio-summary.json"

questions=(
  titleMusic uiSfx gameplaySfx prologueAudio deathRespawn pauseBehaviour resumeBehaviour
  backgroundSuspended foregroundRestored distortion crackling duplicateMusic audioAfterTermination
  controllerHapticSmoke
)
prompts=(
  "Title/menu music audible" "UI navigation sounds audible" "Jump/dash/gameplay sounds audible"
  "Prologue music or ambience audible in the separate route" "Death/respawn sound audible"
  "Pause behaved as expected" "Resume restored expected audio" "Background suspended/stopped audio"
  "Foreground restored expected audio" "Any distortion" "Any severe crackling"
  "Any duplicate/overlapping persistent music" "Any audio after termination"
  "Movement/Jump/Dash/Grab/Confirm/Cancel/Pause/skip plus rumble smoke passed"
)
responses=()
for index in "${!questions[@]}"; do
  while true; do
    read -r -p "${prompts[$index]}? Enter yes, no, or uncertain: " response
    case "$response" in yes|no|uncertain) responses+=("$response"); break ;; *) echo "Please enter yes, no, or uncertain." ;; esac
  done
done
python3 - "$EVIDENCE_DIR/audible-confirmation.json" "${responses[@]}" <<'PY'
import json, pathlib, sys
keys = ["titleMusic", "uiSfx", "gameplaySfx", "prologueAudio", "deathRespawn", "pauseBehaviour",
        "resumeBehaviour", "backgroundSuspended", "foregroundRestored", "distortion", "crackling",
        "duplicateMusic", "audioAfterTermination", "controllerHapticSmoke"]
pathlib.Path(sys.argv[1]).write_text(json.dumps({"schemaVersion": 1, "scope": "user-observed Stage 5B physical gameplay", **dict(zip(keys, sys.argv[2:]))}, indent=2, sort_keys=True) + "\n")
PY
for index in 0 1 2 3 4 5 6 7 8 13; do
  [[ "${responses[$index]}" == "yes" ]] || { echo "error: required positive confirmation is ${responses[$index]} for ${questions[$index]}" >&2; exit 1; }
done
for index in 9 10 11 12; do
  [[ "${responses[$index]}" == "no" ]] || { echo "error: required negative confirmation is ${responses[$index]} for ${questions[$index]}" >&2; exit 1; }
done
echo "PASS: physical normal-game machine evidence, lifecycle, audible matrix, and controller/haptic smoke"
echo "all raw identifiers and responses remain in ignored evidence"
