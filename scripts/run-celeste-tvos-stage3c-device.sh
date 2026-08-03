#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/run-celeste-tvos-stage3c-device.sh --app DIR --scenario NAME [options]

Install and exercise a signed Stage 3C app on the unique paired physical
AppleTV14,1. For normal/skip it verifies 60 post-transition seconds and a full
second launch. Raw identifiers remain only in the ignored evidence directory.

Options:
  --app DIR             Signed tvOS arm64 .app (required).
  --scenario NAME       preflight, normal, or skip (required).
  --device-id ID        Ignored local device ID; otherwise discover it.
  --evidence-dir DIR    Ignored evidence directory
                        (default: artifacts/celeste-runtime/stage3c-device-NAME).
  --lifecycle           Background/foreground first normal/skip run.
  --no-second-launch    Skip full second run (not acceptance).
  --second-only         Capture only a clean second run after a separately
                        accepted first run in the same evidence directory.
  -h, --help            Show this help.

The script installs no tools and does not print signing or device identifiers.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP_DIR=""
SCENARIO=""
DEVICE_ID=""
EVIDENCE_DIR=""
RUN_LIFECYCLE=0
RUN_SECOND=1
SECOND_ONLY=0
BUNDLE_ID=""
EXECUTABLE_NAME=""
LAUNCH_PID=""

repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --app) [[ $# -ge 2 ]] || exit 2; APP_DIR="$(repo_path "$2")"; shift 2 ;;
    --scenario) [[ $# -ge 2 ]] || exit 2; SCENARIO="$2"; shift 2 ;;
    --device-id) [[ $# -ge 2 ]] || exit 2; DEVICE_ID="$2"; shift 2 ;;
    --evidence-dir) [[ $# -ge 2 ]] || exit 2; EVIDENCE_DIR="$(repo_path "$2")"; shift 2 ;;
    --lifecycle) RUN_LIFECYCLE=1; shift ;;
    --no-second-launch) RUN_SECOND=0; shift ;;
    --second-only) SECOND_ONLY=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done
[[ -d "$APP_DIR" ]] || { echo "error: --app must name an app bundle" >&2; exit 2; }
case "$SCENARIO" in preflight|normal|skip) ;; *) echo "error: --scenario must be preflight, normal, or skip" >&2; exit 2 ;; esac
[[ -n "$EVIDENCE_DIR" ]] || EVIDENCE_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage3c-device-$SCENARIO"
for command in xcrun python3 plutil codesign git; do command -v "$command" >/dev/null || { echo "error: missing tool: $command" >&2; exit 1; }; done
case "$EVIDENCE_DIR" in "$REPO_ROOT"/*) relative="${EVIDENCE_DIR#$REPO_ROOT/}" ;; *) echo "error: evidence must be below repository" >&2; exit 1 ;; esac
git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative/console.log" || { echo "error: evidence directory is not ignored" >&2; exit 1; }

"$REPO_ROOT/scripts/verify-celeste-tvos-stage3c.sh" --app "$APP_DIR" --platform tvos >/dev/null
codesign --verify --deep --strict "$APP_DIR"
mkdir -p "$EVIDENCE_DIR"
if [[ "$SECOND_ONLY" -eq 1 ]]; then
  rm -f -- "$EVIDENCE_DIR"/second-launch*.log "$EVIDENCE_DIR"/second-launch*.json
else
  rm -f -- "$EVIDENCE_DIR"/*.log "$EVIDENCE_DIR"/*.json
fi
devices_json="$EVIDENCE_DIR/devices-private.json"
xcrun devicectl list devices --json-output "$devices_json" > "$EVIDENCE_DIR/device-list-private.log" 2>&1
if [[ -z "$DEVICE_ID" ]]; then
  DEVICE_ID="$(python3 - "$devices_json" <<'PY'
import json, pathlib, sys
devices=json.loads(pathlib.Path(sys.argv[1]).read_text()).get("result",{}).get("devices",[])
matches=[d["identifier"] for d in devices if d.get("hardwareProperties",{}).get("productType")=="AppleTV14,1" and d.get("hardwareProperties",{}).get("reality")=="physical" and d.get("connectionProperties",{}).get("pairingState")=="paired"]
if len(matches)!=1: raise SystemExit(f"error: expected one paired AppleTV14,1, found {len(matches)}")
print(matches[0])
PY
)"
fi
BUNDLE_ID="$(plutil -extract CFBundleIdentifier raw "$APP_DIR/Info.plist")"
EXECUTABLE_NAME="$(plutil -extract CFBundleExecutable raw "$APP_DIR/Info.plist")"

terminate_device_app() {
  local label="$1"
  local processes="$EVIDENCE_DIR/$label-processes-private.json"
  local pid=""
  xcrun devicectl device info processes --quiet --device "$DEVICE_ID" --json-output "$processes" \
    --log-output "$EVIDENCE_DIR/$label-processes-private.log" >/dev/null 2>&1 || return 0
  pid="$(python3 - "$processes" "$EXECUTABLE_NAME" <<'PY'
import json, pathlib, sys, urllib.parse
processes=json.loads(pathlib.Path(sys.argv[1]).read_text()).get("result",{}).get("runningProcesses",[])
matches=[]
for process in processes:
    executable=urllib.parse.unquote(process.get("executable", "")).rstrip("/").rsplit("/", 1)[-1]
    if executable == sys.argv[2]: matches.append(str(process["processIdentifier"]))
if len(matches)>1: raise SystemExit("error: multiple host processes are running")
print(matches[0] if matches else "")
PY
)"
  if [[ -n "$pid" ]]; then
    xcrun devicectl device process terminate --quiet --device "$DEVICE_ID" --pid "$pid" \
      --json-output "$EVIDENCE_DIR/$label-terminate-private.json" \
      --log-output "$EVIDENCE_DIR/$label-terminate-private.log" >/dev/null 2>&1 || true
  fi
}

finish_console_capture() {
  for _ in {1..5}; do
    kill -0 "$LAUNCH_PID" 2>/dev/null || break
    sleep 1
  done
  # A backgrounded devicectl inherits an ignored SIGINT from non-interactive
  # bash.  The remote process has already been terminated above, so TERM is
  # used only to detach the local console client after its final log flush.
  if kill -0 "$LAUNCH_PID" 2>/dev/null; then
    kill -TERM "$LAUNCH_PID" 2>/dev/null || true
    for _ in {1..3}; do
      kill -0 "$LAUNCH_PID" 2>/dev/null || break
      sleep 1
    done
    # CoreDevice 26.6 can ignore TERM after the remote app has exited. Only
    # the exact local console PID created above is force-ended here.
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
trap cleanup EXIT

xcrun devicectl device install app --quiet --device "$DEVICE_ID" "$APP_DIR" \
  --json-output "$EVIDENCE_DIR/install-private.json" --log-output "$EVIDENCE_DIR/install-private.log" >/dev/null 2>&1

wait_for_log() {
  local log="$1" token="$2" attempts="$3"
  for ((i=0; i<attempts; i++)); do
    grep -Fq "$token" "$log" 2>/dev/null && return 0
    [[ -z "$LAUNCH_PID" ]] || kill -0 "$LAUNCH_PID" 2>/dev/null || return 1
    sleep 2
  done
  return 1
}

run_once() {
  local label="$1"
  local log="$EVIDENCE_DIR/$label.log"
  local launch_status=0
  xcrun devicectl device process launch --console --terminate-existing --timeout 240 \
    --device "$DEVICE_ID" "$BUNDLE_ID" \
    --json-output "$EVIDENCE_DIR/$label-private.json" --log-output "$EVIDENCE_DIR/$label-tool-private.log" \
    > "$log" 2>&1 &
  LAUNCH_PID=$!
  wait_for_log "$log" "lifecycle launch: SDL main callback" 15 || { echo "error: device host did not enter SDL" >&2; return 1; }
  # A prior lifecycle test can leave another tvOS application in front even
  # though the console-attached host process started successfully.  Activating
  # the already-running bundle makes each capture independent of that UI state.
  xcrun devicectl device process launch --quiet --device "$DEVICE_ID" "$BUNDLE_ID" \
    --json-output "$EVIDENCE_DIR/$label-activate-private.json" \
    --log-output "$EVIDENCE_DIR/$label-activate-private.log" >/dev/null 2>&1
  if [[ "$SCENARIO" == "preflight" ]]; then
    wait_for_log "$log" "CelestePreflight: run loop returned cleanly" 90 || { echo "error: device preflight did not finish" >&2; return 1; }
    terminate_device_app "$label"
    finish_console_capture
    "$REPO_ROOT/scripts/verify-celeste-tvos-stage3c-evidence.py" --log "$log" --scenario preflight --platform device --output "$EVIDENCE_DIR/$label-summary.json"
    return
  fi
  wait_for_log "$log" "name=next-scene-first-draw; scene=Celeste.Overworld" 105 || { echo "error: device next scene did not draw" >&2; return 1; }
  if [[ "$RUN_LIFECYCLE" -eq 1 && "$label" == "first-launch" ]]; then
    xcrun devicectl device process launch --device "$DEVICE_ID" com.apple.TVSettings \
      --json-output "$EVIDENCE_DIR/background-private.json" --log-output "$EVIDENCE_DIR/background-private.log" >/dev/null 2>&1
    sleep 5
    xcrun devicectl device process launch --device "$DEVICE_ID" "$BUNDLE_ID" \
      --json-output "$EVIDENCE_DIR/foreground-private.json" --log-output "$EVIDENCE_DIR/foreground-private.log" >/dev/null 2>&1
  fi
  wait_for_log "$log" "name=diagnostic-clean-exit-requested" 90 || { echo "error: device diagnostic did not sustain and exit" >&2; return 1; }
  wait_for_log "$log" "Celeste run loop returned" 15 || { echo "error: device Celeste run loop did not return" >&2; return 1; }
  terminate_device_app "$label"
  finish_console_capture
  evidence_args=(--log "$log" --scenario "$SCENARIO" --platform device --output "$EVIDENCE_DIR/$label-summary.json")
  if [[ "$RUN_LIFECYCLE" -eq 1 && "$label" == "first-launch" ]]; then evidence_args+=(--require-lifecycle); fi
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage3c-evidence.py" "${evidence_args[@]}"
}

if [[ "$SCENARIO" == "preflight" ]]; then run_once preflight; echo "PASS: Stage 3C device preflight"; exit 0; fi
if [[ "$SECOND_ONLY" -eq 1 ]]; then
  run_once second-launch
else
  run_once first-launch
  if [[ "$RUN_SECOND" -eq 1 ]]; then run_once second-launch; fi
fi
echo "PASS: Stage 3C physical $SCENARIO scenario; private IDs remain ignored"
