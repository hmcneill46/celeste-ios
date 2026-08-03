#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/run-celeste-tvos-simulator.sh --app DIR --mode MODE [options]

Install and exercise a built Stage 3B app on an available arm64 Apple TV 4K
(3rd generation) tvOS 26.5 simulator. Installs no tools, does not erase a
simulator, and writes all logs/screenshots below an ignored directory.

Options:
  --app DIR             Built .app bundle (required).
  --mode MODE           CelestePreflight or Celeste (required); must match the
                        launch mode compiled into the app.
  --simulator-id UUID   Use this simulator; otherwise select the one available
                        matching the locked model/runtime.
  --duration SECONDS    Celeste first-launch duration (default/minimum: 90).
  --evidence-dir DIR    Ignored output directory
                        (default: artifacts/celeste-runtime/simulator-run).
  -h, --help            Show this help.

Use scripts/verify-celeste-tvos-evidence.py to compare the resulting logs with
the Debug reference, trimmed Release and physical-device evidence.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP_DIR=""
MODE=""
SIMULATOR_ID=""
DURATION=90
EVIDENCE_DIR="$REPO_ROOT/artifacts/celeste-runtime/simulator-run"
CONSOLE_PID=""
BUNDLE_ID=""

repo_path() {
  case "$1" in
    /*) printf '%s\n' "$1" ;;
    *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;;
  esac
}

while (($#)); do
  case "$1" in
    --app)
      [[ $# -ge 2 ]] || { echo "error: --app requires a value" >&2; exit 2; }
      APP_DIR="$(repo_path "$2")"; shift 2 ;;
    --mode)
      [[ $# -ge 2 ]] || { echo "error: --mode requires a value" >&2; exit 2; }
      MODE="$2"; shift 2 ;;
    --simulator-id)
      [[ $# -ge 2 ]] || { echo "error: --simulator-id requires a value" >&2; exit 2; }
      SIMULATOR_ID="$2"; shift 2 ;;
    --duration)
      [[ $# -ge 2 ]] || { echo "error: --duration requires a value" >&2; exit 2; }
      DURATION="$2"; shift 2 ;;
    --evidence-dir)
      [[ $# -ge 2 ]] || { echo "error: --evidence-dir requires a value" >&2; exit 2; }
      EVIDENCE_DIR="$(repo_path "$2")"; shift 2 ;;
    -h|--help)
      usage; exit 0 ;;
    *)
      echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ -d "$APP_DIR" ]] || { echo "error: --app must name a built app bundle" >&2; exit 2; }
case "$MODE" in
  CelestePreflight|Celeste) ;;
  *) echo "error: --mode must be CelestePreflight or Celeste" >&2; exit 2 ;;
esac
[[ "$DURATION" =~ ^[0-9]+$ ]] || { echo "error: --duration must be an integer" >&2; exit 2; }
if [[ "$MODE" == "Celeste" && "$DURATION" -lt 90 ]]; then
  echo "error: Celeste --duration must be at least 90 seconds" >&2
  exit 2
fi
for command in xcrun python3 plutil git; do
  command -v "$command" >/dev/null || { echo "error: required tool is missing: $command" >&2; exit 1; }
done
case "$EVIDENCE_DIR" in
  "$REPO_ROOT"/*) evidence_relative="${EVIDENCE_DIR#$REPO_ROOT/}" ;;
  *) echo "error: evidence directory must be below the repository" >&2; exit 1 ;;
esac
git -C "$REPO_ROOT" check-ignore --no-index -q -- "$evidence_relative/console.log" || {
  echo "error: evidence directory is not ignored" >&2; exit 1;
}

"$REPO_ROOT/scripts/verify-celeste-tvos-runtime.sh" --app "$APP_DIR" --platform tvossimulator
BUNDLE_ID="$(plutil -extract CFBundleIdentifier raw "$APP_DIR/Info.plist")"
devices_json="$(mktemp -t stage3b-simulators).json"

stop_console() {
  if [[ -n "$SIMULATOR_ID" && -n "$BUNDLE_ID" ]]; then
    xcrun simctl terminate "$SIMULATOR_ID" "$BUNDLE_ID" 2>/dev/null || true
  fi
  if [[ -n "$CONSOLE_PID" ]]; then
    kill "$CONSOLE_PID" 2>/dev/null || true
    wait "$CONSOLE_PID" 2>/dev/null || true
    CONSOLE_PID=""
  fi
}
cleanup() {
  stop_console
  rm -f -- "$devices_json"
}
trap cleanup EXIT

xcrun simctl list --json devices available > "$devices_json"
if [[ -z "$SIMULATOR_ID" ]]; then
  SIMULATOR_ID="$(python3 - "$devices_json" <<'PY'
import json, pathlib, sys
data = json.loads(pathlib.Path(sys.argv[1]).read_text())
matches = [device["udid"]
           for runtime, devices in data.get("devices", {}).items()
           if "tvOS-26-5" in runtime
           for device in devices
           if device.get("isAvailable") and device.get("name") == "Apple TV 4K (3rd generation)"]
if len(matches) != 1:
    raise SystemExit(f"error: expected one available matching simulator, found {len(matches)}")
print(matches[0])
PY
)"
fi

mkdir -p "$EVIDENCE_DIR"
rm -f -- "$EVIDENCE_DIR/console.log" "$EVIDENCE_DIR/second-launch.log" \
  "$EVIDENCE_DIR/frame-before-background.png" "$EVIDENCE_DIR/frame-after-foreground.png"
xcrun simctl boot "$SIMULATOR_ID" 2>/dev/null || true
xcrun simctl bootstatus "$SIMULATOR_ID" -b
xcrun simctl install "$SIMULATOR_ID" "$APP_DIR"
stop_console

xcrun simctl launch --console-pty "$SIMULATOR_ID" "$BUNDLE_ID" > "$EVIDENCE_DIR/console.log" 2>&1 &
CONSOLE_PID=$!
if [[ "$MODE" == "CelestePreflight" ]]; then
  for _ in {1..12}; do
    grep -Fq "CelestePreflight: run loop returned cleanly" "$EVIDENCE_DIR/console.log" && break
    kill -0 "$CONSOLE_PID" 2>/dev/null || break
    sleep 5
  done
  wait "$CONSOLE_PID" || true
  CONSOLE_PID=""
  grep -Fq "mode=CelestePreflight" "$EVIDENCE_DIR/console.log" || { echo "error: app was not built in CelestePreflight mode" >&2; exit 1; }
  grep -Fq "name=preflight-complete; settings=PASS;" "$EVIDENCE_DIR/console.log" &&
    grep -Fq "reflection=PASS; xnb-readers=7/7; fmod-low-level=0" "$EVIDENCE_DIR/console.log" || {
    echo "error: preflight did not complete" >&2; exit 1;
  }
  echo "PASS: simulator CelestePreflight completed"
  exit 0
fi

for ((elapsed=0; elapsed<45; elapsed+=5)); do sleep 5; done
xcrun simctl io "$SIMULATOR_ID" screenshot "$EVIDENCE_DIR/frame-before-background.png"
xcrun simctl launch "$SIMULATOR_ID" com.apple.TVSettings >/dev/null
sleep 5
xcrun simctl launch "$SIMULATOR_ID" "$BUNDLE_ID" >/dev/null
for ((elapsed=50; elapsed<DURATION; elapsed+=5)); do sleep 5; done
xcrun simctl io "$SIMULATOR_ID" screenshot "$EVIDENCE_DIR/frame-after-foreground.png"
stop_console

xcrun simctl launch --console-pty "$SIMULATOR_ID" "$BUNDLE_ID" > "$EVIDENCE_DIR/second-launch.log" 2>&1 &
CONSOLE_PID=$!
for _ in {1..6}; do sleep 5; done
stop_console

grep -Fq "mode=Celeste" "$EVIDENCE_DIR/console.log" || { echo "error: app was not built in Celeste mode" >&2; exit 1; }
grep -Fq "name=first-celeste-draw" "$EVIDENCE_DIR/console.log" || { echo "error: first Celeste draw was not observed" >&2; exit 1; }
grep -Fq "lifecycle foreground" "$EVIDENCE_DIR/console.log" || { echo "error: foreground lifecycle was not observed" >&2; exit 1; }
grep -Fq "scene=Celeste.Overworld" "$EVIDENCE_DIR/console.log" || { echo "error: Celeste.Overworld was not observed" >&2; exit 1; }
grep -Fq "name=first-celeste-draw" "$EVIDENCE_DIR/second-launch.log" || { echo "error: second launch did not draw" >&2; exit 1; }
if rg -n 'unhandled managed exception|STAGE3B_FATAL|FMOD LOW-LEVEL|fmod-low-level=[1-9]|tvStubs (invoked|call detected)|ContentLoadException' \
  "$EVIDENCE_DIR/console.log" "$EVIDENCE_DIR/second-launch.log"; then
  echo "error: forbidden Stage 3B runtime failure detected" >&2
  exit 1
fi
echo "PASS: simulator Celeste run, lifecycle cycle, and second launch completed"
