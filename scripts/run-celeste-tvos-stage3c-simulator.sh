#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/run-celeste-tvos-stage3c-simulator.sh --app DIR --scenario NAME [options]

Install and run a built Stage 3C app on the locked arm64 Apple TV simulator.
For normal and skip diagnostics it verifies the transition, temporary save,
rumble stop, 60 post-transition seconds, lifecycle cycle and a clean second run.

Options:
  --app DIR             Built .app bundle (required).
  --scenario NAME       fault, preflight, normal, or skip (required).
  --simulator-id UUID   Explicit ignored local simulator ID.
  --evidence-dir DIR    Ignored evidence directory
                        (default: artifacts/celeste-runtime/stage3c-simulator-NAME).
  --no-lifecycle        Skip background/foreground (not an acceptance run).
  --no-second-launch    Skip full second run (not an acceptance run).
  --second-only         Capture only the clean second run after an already
                        accepted first run in the same evidence directory.
  -h, --help            Show this help.

The app must already be compiled for the selected launch mode/scenario. This
script installs no tools and does not erase simulator state.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP_DIR=""
SCENARIO=""
SIMULATOR_ID=""
EVIDENCE_DIR=""
RUN_LIFECYCLE=1
RUN_SECOND=1
SECOND_ONLY=0
BUNDLE_ID=""
CONSOLE_PID=""

repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }

while (($#)); do
  case "$1" in
    --app) [[ $# -ge 2 ]] || exit 2; APP_DIR="$(repo_path "$2")"; shift 2 ;;
    --scenario) [[ $# -ge 2 ]] || exit 2; SCENARIO="$2"; shift 2 ;;
    --simulator-id) [[ $# -ge 2 ]] || exit 2; SIMULATOR_ID="$2"; shift 2 ;;
    --evidence-dir) [[ $# -ge 2 ]] || exit 2; EVIDENCE_DIR="$(repo_path "$2")"; shift 2 ;;
    --no-lifecycle) RUN_LIFECYCLE=0; shift ;;
    --no-second-launch) RUN_SECOND=0; shift ;;
    --second-only) SECOND_ONLY=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ -d "$APP_DIR" ]] || { echo "error: --app must name an app bundle" >&2; exit 2; }
case "$SCENARIO" in fault|preflight|normal|skip) ;; *) echo "error: --scenario must be fault, preflight, normal, or skip" >&2; exit 2 ;; esac
[[ -n "$EVIDENCE_DIR" ]] || EVIDENCE_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage3c-simulator-$SCENARIO"
for command in xcrun python3 plutil git; do command -v "$command" >/dev/null || { echo "error: missing tool: $command" >&2; exit 1; }; done
case "$EVIDENCE_DIR" in "$REPO_ROOT"/*) relative="${EVIDENCE_DIR#$REPO_ROOT/}" ;; *) echo "error: evidence must be below repository" >&2; exit 1 ;; esac
git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative/console.log" || { echo "error: evidence directory is not ignored" >&2; exit 1; }

"$REPO_ROOT/scripts/verify-celeste-tvos-stage3c.sh" --app "$APP_DIR" --platform tvossimulator >/dev/null
BUNDLE_ID="$(plutil -extract CFBundleIdentifier raw "$APP_DIR/Info.plist")"
devices_json="$(mktemp -t stage3c-simulators).json"

stop_app() {
  if [[ -n "$SIMULATOR_ID" && -n "$BUNDLE_ID" ]]; then xcrun simctl terminate "$SIMULATOR_ID" "$BUNDLE_ID" 2>/dev/null || true; fi
  if [[ -n "$CONSOLE_PID" ]]; then kill "$CONSOLE_PID" 2>/dev/null || true; wait "$CONSOLE_PID" 2>/dev/null || true; CONSOLE_PID=""; fi
}
cleanup() { stop_app; rm -f -- "$devices_json"; }
trap cleanup EXIT

xcrun simctl list --json devices available > "$devices_json"
if [[ -z "$SIMULATOR_ID" ]]; then
  SIMULATOR_ID="$(python3 - "$devices_json" <<'PY'
import json, pathlib, sys
data=json.loads(pathlib.Path(sys.argv[1]).read_text())
matches=[d["udid"] for runtime, devices in data.get("devices",{}).items() if "tvOS-26-5" in runtime for d in devices if d.get("isAvailable") and d.get("name")=="Apple TV 4K (3rd generation)"]
if len(matches)!=1: raise SystemExit(f"error: expected one matching simulator, found {len(matches)}")
print(matches[0])
PY
)"
fi

mkdir -p "$EVIDENCE_DIR"
if [[ "$SECOND_ONLY" -eq 1 ]]; then
  rm -f -- "$EVIDENCE_DIR"/second-launch.log "$EVIDENCE_DIR"/second-launch-summary.json "$EVIDENCE_DIR"/second-launch-next-scene.png
else
  rm -f -- "$EVIDENCE_DIR"/*.log "$EVIDENCE_DIR"/*.json "$EVIDENCE_DIR"/*.png
fi
xcrun simctl boot "$SIMULATOR_ID" 2>/dev/null || true
xcrun simctl bootstatus "$SIMULATOR_ID" -b
xcrun simctl install "$SIMULATOR_ID" "$APP_DIR"

wait_for_log() {
  local log="$1" token="$2" attempts="$3"
  for ((i=0; i<attempts; i++)); do
    grep -Fq "$token" "$log" 2>/dev/null && return 0
    [[ -z "$CONSOLE_PID" ]] || kill -0 "$CONSOLE_PID" 2>/dev/null || return 1
    sleep 2
  done
  return 1
}

run_once() {
  local label="$1"
  local log="$EVIDENCE_DIR/$label.log"
  stop_app
  xcrun simctl launch --console-pty "$SIMULATOR_ID" "$BUNDLE_ID" > "$log" 2>&1 &
  CONSOLE_PID=$!
  if [[ "$SCENARIO" == "preflight" ]]; then
    wait_for_log "$log" "CelestePreflight: run loop returned cleanly" 60 || { echo "error: preflight did not finish" >&2; return 1; }
    stop_app
    "$REPO_ROOT/scripts/verify-celeste-tvos-stage3c-evidence.py" --log "$log" --scenario preflight --platform simulator --output "$EVIDENCE_DIR/$label-summary.json"
    return
  fi
  if [[ "$SCENARIO" == "fault" ]]; then
    wait_for_log "$log" "name=fatal" 60 || { echo "error: preserved Prologue fault was not observed" >&2; return 1; }
    stop_app
    "$REPO_ROOT/scripts/verify-celeste-tvos-stage3c-evidence.py" --log "$log" --scenario fault --platform simulator --output "$EVIDENCE_DIR/$label-summary.json"
    return
  fi

  wait_for_log "$log" "name=next-scene-first-draw; scene=Celeste.Overworld" 90 || { echo "error: next scene did not draw" >&2; return 1; }
  xcrun simctl io "$SIMULATOR_ID" screenshot "$EVIDENCE_DIR/$label-next-scene.png" >/dev/null
  if [[ "$RUN_LIFECYCLE" -eq 1 && "$label" == "first-launch" ]]; then
    xcrun simctl launch "$SIMULATOR_ID" com.apple.TVSettings >/dev/null
    sleep 5
    xcrun simctl launch "$SIMULATOR_ID" "$BUNDLE_ID" >/dev/null
  fi
  wait_for_log "$log" "name=diagnostic-clean-exit-requested" 75 || { echo "error: diagnostic did not sustain and request clean exit" >&2; return 1; }
  wait_for_log "$log" "Celeste run loop returned" 15 || { echo "error: Celeste run loop did not return after exit request" >&2; return 1; }
  stop_app
  evidence_args=(--log "$log" --scenario "$SCENARIO" --platform simulator --output "$EVIDENCE_DIR/$label-summary.json")
  if [[ "$RUN_LIFECYCLE" -eq 1 && "$label" == "first-launch" ]]; then evidence_args+=(--require-lifecycle); fi
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage3c-evidence.py" "${evidence_args[@]}"
}

if [[ "$SCENARIO" == "preflight" || "$SCENARIO" == "fault" ]]; then
  run_once preflight
  echo "PASS: Stage 3C simulator $SCENARIO evidence"
  exit 0
fi

if [[ "$SECOND_ONLY" -eq 1 ]]; then
  run_once second-launch
else
  run_once first-launch
  if [[ "$RUN_SECOND" -eq 1 ]]; then run_once second-launch; fi
fi
echo "PASS: Stage 3C simulator $SCENARIO scenario"
