#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/run-tvos-host-simulator.sh --app DIR [options]

Install and exercise a built arm64 Stage 2 host on an Apple TV 4K
(3rd generation) tvOS 26.5 simulator. Installs no tools and never erases a
simulator. Generated logs and screenshots are written below an ignored path.

Options:
  --app DIR             Built .app bundle (required).
  --simulator-id UUID   Use this simulator. Otherwise select an available
                        tvOS 26.5 Apple TV 4K (3rd generation) simulator.
  --duration SECONDS    Pre-background run time (default and minimum: 60).
  --evidence-dir DIR    Output directory
                        (default: artifacts/tvos-host/simulator-acceptance).
  -h, --help            Show this help.

Relative paths are resolved from the repository root.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP_DIR=""
SIMULATOR_ID=""
DURATION=60
EVIDENCE_DIR="$REPO_ROOT/artifacts/tvos-host/simulator-acceptance"

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
      APP_DIR="$(repo_path "$2")"
      shift 2
      ;;
    --simulator-id)
      [[ $# -ge 2 ]] || { echo "error: --simulator-id requires a value" >&2; exit 2; }
      SIMULATOR_ID="$2"
      shift 2
      ;;
    --duration)
      [[ $# -ge 2 ]] || { echo "error: --duration requires a value" >&2; exit 2; }
      DURATION="$2"
      shift 2
      ;;
    --evidence-dir)
      [[ $# -ge 2 ]] || { echo "error: --evidence-dir requires a value" >&2; exit 2; }
      EVIDENCE_DIR="$(repo_path "$2")"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "error: unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

[[ -n "$APP_DIR" ]] || { echo "error: --app is required" >&2; exit 2; }
[[ "$DURATION" =~ ^[0-9]+$ && "$DURATION" -ge 60 ]] || {
  echo "error: --duration must be an integer of at least 60 seconds" >&2
  exit 2
}

"$REPO_ROOT/scripts/verify-tvos-host.sh" --app "$APP_DIR" --platform tvossimulator

devices_json="$(mktemp -t stage2-simulators).json"
cleanup() {
  rm -f -- "$devices_json"
}
trap cleanup EXIT
xcrun simctl list --json devices available > "$devices_json"

if [[ -z "$SIMULATOR_ID" ]]; then
  SIMULATOR_ID="$(python3 - "$devices_json" <<'PY'
import json, pathlib, sys
data = json.loads(pathlib.Path(sys.argv[1]).read_text())
matches = []
for runtime, devices in data.get("devices", {}).items():
    if "tvOS-26-5" not in runtime:
        continue
    for device in devices:
        if device.get("isAvailable") and device.get("name") == "Apple TV 4K (3rd generation)":
            matches.append(device["udid"])
if len(matches) != 1:
    raise SystemExit(f"error: expected one available tvOS 26.5 Apple TV 4K (3rd generation) 4K simulator, found {len(matches)}")
print(matches[0])
PY
)"
fi

mkdir -p "$EVIDENCE_DIR"
RAW_LOG="$EVIDENCE_DIR/console.log"
BEFORE_SCREENSHOT="$EVIDENCE_DIR/fna-scene-before-background.png"
AFTER_SCREENSHOT="$EVIDENCE_DIR/fna-scene-after-foreground.png"
BUNDLE_ID="$(plutil -extract CFBundleIdentifier raw "$APP_DIR/Info.plist")"

xcrun simctl boot "$SIMULATOR_ID" 2>/dev/null || true
xcrun simctl bootstatus "$SIMULATOR_ID" -b
xcrun simctl install "$SIMULATOR_ID" "$APP_DIR"
xcrun simctl terminate "$SIMULATOR_ID" "$BUNDLE_ID" 2>/dev/null || true

xcrun simctl launch --console-pty "$SIMULATOR_ID" "$BUNDLE_ID" > "$RAW_LOG" 2>&1 &
CONSOLE_PID=$!
stop_console() {
  xcrun simctl terminate "$SIMULATOR_ID" "$BUNDLE_ID" 2>/dev/null || true
  kill "$CONSOLE_PID" 2>/dev/null || true
  wait "$CONSOLE_PID" 2>/dev/null || true
}
trap 'stop_console; cleanup' EXIT

sleep "$DURATION"
xcrun simctl io "$SIMULATOR_ID" screenshot "$BEFORE_SCREENSHOT"
xcrun simctl launch "$SIMULATOR_ID" com.apple.TVSettings >/dev/null
sleep 5
xcrun simctl launch "$SIMULATOR_ID" "$BUNDLE_ID" >/dev/null
sleep 15
xcrun simctl io "$SIMULATOR_ID" screenshot "$AFTER_SCREENSHOT"

grep -Fq "native interoperability self-test PASS" "$RAW_LOG" || { echo "error: native self-test did not pass" >&2; exit 1; }
grep -Fq "graphics initialized:" "$RAW_LOG" || { echo "error: FNA graphics did not initialize" >&2; exit 1; }
grep -Fq "renderer selected: Metal" "$RAW_LOG" || { echo "error: Metal renderer was not confirmed" >&2; exit 1; }
grep -Fq "lifecycle resign-active" "$RAW_LOG" || { echo "error: resign-active was not observed" >&2; exit 1; }
grep -Fq "lifecycle background" "$RAW_LOG" || { echo "error: background was not observed" >&2; exit 1; }
grep -Fq "lifecycle foreground" "$RAW_LOG" || { echo "error: foreground was not observed" >&2; exit 1; }
grep -Fq "lifecycle active" "$RAW_LOG" || { echo "error: active was not observed" >&2; exit 1; }
if grep -n -E 'EntryPointNotFoundException|DllNotFoundException|unhandled managed exception|graphics device.*fail|tvStubs (invoked|call detected)' "$RAW_LOG"; then
  echo "error: a forbidden runtime failure was detected" >&2
  exit 1
fi
heartbeat_count="$(grep -c 'frame-heartbeat' "$RAW_LOG" || true)"
minimum_heartbeats=$((DURATION / 5))
[[ "$heartbeat_count" -ge "$minimum_heartbeats" ]] || {
  echo "error: only $heartbeat_count frame heartbeats; expected at least $minimum_heartbeats" >&2
  exit 1
}

stop_console
trap cleanup EXIT
echo "PASS: simulator acceptance with $heartbeat_count heartbeats"
echo "console: $RAW_LOG"
echo "screenshots: $BEFORE_SCREENSHOT, $AFTER_SCREENSHOT"
