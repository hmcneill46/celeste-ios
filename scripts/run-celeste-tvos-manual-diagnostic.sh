#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/run-celeste-tvos-manual-diagnostic.sh --app DIR --test TEST [options]

Install a signed Stage 3C manual-scenario app on the paired AppleTV14,1,
attach its console, and capture bounded ignored evidence for a user-assisted
Prologue or haptic test.

Options:
  --app DIR             Signed tvOS arm64 .app (required).
  --test TEST           normal, skip, lifecycle, or disconnect (required).
  --device-id ID        Ignored local device identifier; otherwise discover
                        the unique paired AppleTV14,1 dynamically.
  --duration SECONDS    Capture timeout (default: 600, range: 120-900).
  --evidence-dir DIR    Ignored raw evidence directory
                        (default: artifacts/celeste-runtime/stage3c-device-manual-TEST).
  -h, --help            Show this help.

Build the app with CelesteLaunchMode=CelestePrologueDiagnostic and
Stage3CPrologueScenario=manual. The script never prints the device ID, bundle
ID, signing identity, team, certificate, or provisioning UUID. Raw devicectl
output remains ignored and must not be committed.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP_DIR=""
TEST=""
DEVICE_ID=""
DURATION=600
EVIDENCE_DIR=""
LAUNCH_PID=""
EXECUTABLE_NAME=""

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
case "$TEST" in normal|skip|lifecycle|disconnect) ;; *) echo "error: --test must be normal, skip, lifecycle, or disconnect" >&2; exit 2 ;; esac
[[ "$DURATION" =~ ^[0-9]+$ && "$DURATION" -ge 120 && "$DURATION" -le 900 ]] || { echo "error: --duration must be 120-900 seconds" >&2; exit 2; }
[[ -n "$EVIDENCE_DIR" ]] || EVIDENCE_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage3c-device-manual-$TEST"
for command in xcrun python3 plutil codesign git; do command -v "$command" >/dev/null || { echo "error: missing tool: $command" >&2; exit 1; }; done
case "$EVIDENCE_DIR" in "$REPO_ROOT"/*) relative="${EVIDENCE_DIR#$REPO_ROOT/}" ;; *) echo "error: evidence must be below repository" >&2; exit 1 ;; esac
git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative/console.log" || { echo "error: evidence directory is not ignored" >&2; exit 1; }

"$REPO_ROOT/scripts/verify-celeste-tvos-stage3c.sh" --app "$APP_DIR" --platform tvos >/dev/null
codesign --verify --deep --strict "$APP_DIR"
mkdir -p "$EVIDENCE_DIR"
rm -f -- "$EVIDENCE_DIR"/*.log "$EVIDENCE_DIR"/*.json
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
  local processes="$EVIDENCE_DIR/processes-private.json" pid=""
  xcrun devicectl device info processes --quiet --device "$DEVICE_ID" --json-output "$processes" \
    --log-output "$EVIDENCE_DIR/processes-private.log" >/dev/null 2>&1 || return 0
  pid="$(python3 - "$processes" "$EXECUTABLE_NAME" <<'PY'
import json, pathlib, sys, urllib.parse
processes=json.loads(pathlib.Path(sys.argv[1]).read_text()).get("result",{}).get("runningProcesses",[])
matches=[str(p["processIdentifier"]) for p in processes if urllib.parse.unquote(p.get("executable", "")).rstrip("/").rsplit("/",1)[-1] == sys.argv[2]]
if len(matches)>1: raise SystemExit("error: multiple host processes are running")
print(matches[0] if matches else "")
PY
)"
  if [[ -n "$pid" ]]; then
    xcrun devicectl device process terminate --quiet --device "$DEVICE_ID" --pid "$pid" \
      --json-output "$EVIDENCE_DIR/terminate-private.json" --log-output "$EVIDENCE_DIR/terminate-private.log" >/dev/null 2>&1 || true
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
    terminate_device_app
    kill -TERM "$LAUNCH_PID" 2>/dev/null || true
    sleep 1
    if kill -0 "$LAUNCH_PID" 2>/dev/null; then kill -KILL "$LAUNCH_PID" 2>/dev/null || true; fi
    wait "$LAUNCH_PID" 2>/dev/null || true
    LAUNCH_PID=""
  fi
}
trap cleanup EXIT INT TERM

xcrun devicectl device install app --quiet --device "$DEVICE_ID" "$APP_DIR" \
  --json-output "$EVIDENCE_DIR/install-private.json" --log-output "$EVIDENCE_DIR/install-private.log" >/dev/null 2>&1
xcrun devicectl device process launch --console --terminate-existing --timeout "$DURATION" \
  --device "$DEVICE_ID" "$BUNDLE_ID" \
  --json-output "$EVIDENCE_DIR/launch-private.json" --log-output "$EVIDENCE_DIR/launch-tool-private.log" \
  > "$EVIDENCE_DIR/console.log" 2>&1 &
LAUNCH_PID=$!

started=0
for ((i=0; i<30; i++)); do
  if grep -Fq "lifecycle launch: SDL main callback" "$EVIDENCE_DIR/console.log" 2>/dev/null; then started=1; break; fi
  kill -0 "$LAUNCH_PID" 2>/dev/null || break
  sleep 1
done
[[ "$started" -eq 1 ]] || { echo "error: device host did not enter SDL" >&2; exit 1; }
xcrun devicectl device process launch --quiet --device "$DEVICE_ID" "$BUNDLE_ID" \
  --json-output "$EVIDENCE_DIR/activate-private.json" \
  --log-output "$EVIDENCE_DIR/activate-private.log" >/dev/null 2>&1

ready=0
for ((i=0; i<90; i++)); do
  if grep -Fq "name=bird-tutorial-entered" "$EVIDENCE_DIR/console.log" 2>/dev/null; then ready=1; break; fi
  kill -0 "$LAUNCH_PID" 2>/dev/null || break
  sleep 2
done
[[ "$ready" -eq 1 ]] || { echo "error: app did not reach the manual Prologue boundary; inspect ignored console.log" >&2; exit 1; }

echo "READY FOR MANUAL PROLOGUE TEST"
case "$TEST" in
  normal)
    echo "Perform the instructed up-right logical Dash once; do not skip. Wait through the fade and leave the next scene visible until the app exits cleanly." ;;
  skip)
    echo "Invoke the normal edge-triggered cutscene Skip once. Wait through the fade and leave the next scene visible until the app exits cleanly." ;;
  lifecycle)
    echo "Complete the tutorial normally. After the next scene appears, background the app, wait five seconds, then foreground it and leave it running." ;;
  disconnect)
    echo "Complete the tutorial normally. In the next scene, press logical Dash, immediately hold PS for about ten seconds until the DualSense disconnects, reconnect with PS, then press logical Confirm once." ;;
esac

finished=0
for ((i=0; i<DURATION/2; i++)); do
  if grep -Fq "name=diagnostic-clean-exit-requested" "$EVIDENCE_DIR/console.log" 2>/dev/null; then
    finished=1
    break
  fi
  kill -0 "$LAUNCH_PID" 2>/dev/null || break
  sleep 2
done
if [[ "$finished" -eq 1 ]]; then
  for _ in {1..10}; do
    grep -Fq "Celeste run loop returned" "$EVIDENCE_DIR/console.log" && break
    sleep 1
  done
  terminate_device_app
  finish_console_capture
else
  wait "$LAUNCH_PID" || launch_status=$?
  LAUNCH_PID=""
fi
launch_status="${launch_status:-0}"
if [[ "$launch_status" -ne 0 ]] && ! grep -Fq "name=diagnostic-clean-exit-requested" "$EVIDENCE_DIR/console.log"; then
  echo "error: console capture ended with status $launch_status before clean diagnostic exit" >&2
  exit 1
fi

evidence_args=(--log "$EVIDENCE_DIR/console.log" --scenario manual --platform device --output "$EVIDENCE_DIR/summary.json")
case "$TEST" in
  normal)
    grep -Fq "name=dash-initiated" "$EVIDENCE_DIR/console.log" || { echo "error: normal dash was not observed" >&2; exit 1; }
    grep -Fq "name=cutscene-completion-requested" "$EVIDENCE_DIR/console.log" || { echo "error: normal completion was not observed" >&2; exit 1; } ;;
  skip)
    grep -Fq "name=cutscene-skip-completing" "$EVIDENCE_DIR/console.log" || { echo "error: skip completion was not observed" >&2; exit 1; } ;;
  lifecycle) evidence_args+=(--require-lifecycle) ;;
  disconnect) evidence_args+=(--require-controller-cycle) ;;
esac
"$REPO_ROOT/scripts/verify-celeste-tvos-stage3c-evidence.py" "${evidence_args[@]}"
echo "PASS: manual physical Stage 3C $TEST evidence accepted; raw identifiers remain only in ignored evidence"
