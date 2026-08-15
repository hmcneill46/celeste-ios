#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
DEVICE_KIND=""
APP_DIR="$REPO_ROOT/artifacts/ios-host/simulator/publish/CelesteIOSRuntimeHost.app"
EVIDENCE_DIR=""
RUNTIME="com.apple.CoreSimulator.SimRuntime.iOS-26-5"

usage() {
  cat <<'EOF'
Usage: scripts/run-ios-foundation-simulator.sh (--iphone | --ipad) [options]

Install and run the modern foundation on a dedicated iOS 26.5 simulator.
Privacy-sensitive simulator identifiers remain only in ignored evidence.

Options:
  --iphone          iPhone 17 Pro Max simulator
  --ipad            iPad Pro 13-inch (M5) simulator
  --app-dir DIR     built simulator .app
  --evidence-dir DIR ignored evidence destination
  -h, --help        show this help
EOF
}

while (($#)); do
  case "$1" in
    --iphone|--ipad) [[ -z "$DEVICE_KIND" ]] || exit 2; DEVICE_KIND="${1#--}"; shift ;;
    --app-dir) [[ $# -ge 2 ]] || exit 2; APP_DIR="$2"; shift 2 ;;
    --evidence-dir) [[ $# -ge 2 ]] || exit 2; EVIDENCE_DIR="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done
[[ -n "$DEVICE_KIND" ]] || { usage >&2; exit 2; }
[[ "$APP_DIR" == /* ]] || APP_DIR="$REPO_ROOT/$APP_DIR"
[[ -d "$APP_DIR" ]] || { echo "error: simulator app not found" >&2; exit 1; }
[[ -n "$EVIDENCE_DIR" ]] || EVIDENCE_DIR="$REPO_ROOT/.build/ios-host/simulator-$DEVICE_KIND"
[[ "$EVIDENCE_DIR" == /* ]] || EVIDENCE_DIR="$REPO_ROOT/$EVIDENCE_DIR"
mkdir -p "$EVIDENCE_DIR"

name="Celeste iOS Foundation iPhone"
type="com.apple.CoreSimulator.SimDeviceType.iPhone-17-Pro-Max"
[[ "$DEVICE_KIND" == ipad ]] && {
  name="Celeste iOS Foundation iPad"
  type="com.apple.CoreSimulator.SimDeviceType.iPad-Pro-13-inch-M5-12GB"
}
uuid="$(xcrun simctl list devices -j | python3 -c 'import json,sys; n=sys.argv[1]; d=json.load(sys.stdin)["devices"]; print(next((x["udid"] for xs in d.values() for x in xs if x["name"]==n), ""))' "$name")"
if [[ -z "$uuid" ]]; then uuid="$(xcrun simctl create "$name" "$type" "$RUNTIME")"; fi
printf '%s\n' "$uuid" > "$EVIDENCE_DIR/simulator-id.txt"
xcrun simctl boot "$uuid" 2>/dev/null || true
xcrun simctl bootstatus "$uuid" -b >/dev/null &
boot_pid=$!
cleanup_boot() {
  kill "$boot_pid" >/dev/null 2>&1 || true
  wait "$boot_pid" 2>/dev/null || true
}
trap cleanup_boot EXIT
for _ in $(seq 1 240); do
  kill -0 "$boot_pid" 2>/dev/null || break
  sleep 1
done
if kill -0 "$boot_pid" 2>/dev/null; then
  echo "error: simulator did not finish booting within 240 seconds" >&2
  exit 1
fi
wait "$boot_pid"
trap - EXIT
xcrun simctl install "$uuid" "$APP_DIR"

bundle_id="$(plutil -extract CFBundleIdentifier raw "$APP_DIR/Info.plist")"
log_file="$EVIDENCE_DIR/runtime.log"
xcrun simctl launch --terminate-running-process --console-pty \
  "$uuid" "$bundle_id" >"$log_file" 2>&1 &
console_pid=$!
cleanup() {
  xcrun simctl terminate "$uuid" "$bundle_id" >/dev/null 2>&1 || true
  wait "$console_pid" 2>/dev/null || true
  xcrun simctl shutdown "$uuid" >/dev/null 2>&1 || true
}
trap cleanup EXIT
for _ in $(seq 1 45); do
  grep -Fq 'frame-heartbeat' "$log_file" 2>/dev/null && break
  kill -0 "$console_pid" 2>/dev/null || break
  sleep 1
done
xcrun simctl io "$uuid" screenshot "$EVIDENCE_DIR/first-draw.png" >/dev/null
xcrun simctl launch "$uuid" com.apple.mobilesafari >/dev/null
sleep 2
xcrun simctl launch "$uuid" "$bundle_id" >/dev/null
for _ in $(seq 1 20); do
  active_count="$(grep -Fc 'lifecycle=active; existing-runtime-resumed=true' "$log_file" 2>/dev/null || true)"
  [[ "$active_count" -ge 2 ]] && break
  sleep 1
done
cleanup
trap - EXIT

grep -Fq 'first-draw-ready renderer=direct-FNA3D-Metal' "$log_file" || {
  echo "error: simulator did not prove direct Metal first draw" >&2; exit 1;
}
grep -Fq 'audio-lane=simulator-no-fmod' "$log_file" || {
  echo "error: simulator no-FMOD policy was not active" >&2; exit 1;
}
grep -Fq 'scene=UIWindowScene; layer=CAMetalLayer' "$log_file" || {
  echo "error: simulator did not prove UIScene/CAMetalLayer presentation" >&2; exit 1;
}
grep -Fq 'frame-heartbeat' "$log_file" || { echo "error: sustained frame heartbeat absent" >&2; exit 1; }
grep -Fq 'lifecycle=inactive; runtime-disposed=false' "$log_file" || {
  echo "error: simulator background did not preserve the runtime" >&2; exit 1;
}
active_count="$(grep -Fc 'lifecycle=active; existing-runtime-resumed=true' "$log_file" || true)"
[[ "$active_count" -ge 2 ]] || { echo "error: simulator foreground did not resume the runtime" >&2; exit 1; }
[[ "$(grep -Fc 'runtime-foundation scene-count=1; fna-game-count=1' "$log_file")" -eq 1 ]] || {
  echo "error: simulator lifecycle created a second runtime" >&2; exit 1;
}
echo "PASS: $DEVICE_KIND simulator direct-Metal first draw, sustained frames, and same-runtime lifecycle"
