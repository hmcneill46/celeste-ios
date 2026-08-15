#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP_DIR="$REPO_ROOT/artifacts/ios-host/device/publish/CelesteIOSRuntimeHost.app"
EVIDENCE_DIR="$REPO_ROOT/.build/ios-host/device-evidence"

usage() {
  cat <<'EOF'
Usage: scripts/run-ios-foundation-device.sh [options]

Install and launch the signed modern foundation on the single paired iPhone.
Device identifiers and runtime logs remain only in ignored evidence.

Options:
  --app-dir DIR       signed physical-device .app
  --evidence-dir DIR  ignored evidence destination
  -h, --help          show this help
EOF
}

while (($#)); do
  case "$1" in
    --app-dir) [[ $# -ge 2 ]] || exit 2; APP_DIR="$2"; shift 2 ;;
    --evidence-dir) [[ $# -ge 2 ]] || exit 2; EVIDENCE_DIR="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done
[[ "$APP_DIR" == /* ]] || APP_DIR="$REPO_ROOT/$APP_DIR"
[[ "$EVIDENCE_DIR" == /* ]] || EVIDENCE_DIR="$REPO_ROOT/$EVIDENCE_DIR"
[[ -d "$APP_DIR" ]] || { echo "error: signed iPhone app not found" >&2; exit 1; }
mkdir -p "$EVIDENCE_DIR"

devices_json="$EVIDENCE_DIR/devices-private.json"
xcrun devicectl list devices --json-output "$devices_json" --quiet
device_id="$(python3 - "$devices_json" <<'PY'
import json, pathlib, sys
devices = json.loads(pathlib.Path(sys.argv[1]).read_text())["result"]["devices"]
matches = [x for x in devices
           if x.get("hardwareProperties", {}).get("platform") == "iOS"
           and x.get("hardwareProperties", {}).get("deviceType") == "iPhone"
           and x.get("connectionProperties", {}).get("pairingState") == "paired"
           and x.get("deviceProperties", {}).get("developerModeStatus") == "enabled"]
if len(matches) != 1:
    raise SystemExit(f"error: expected exactly one paired Developer Mode iPhone, found {len(matches)}")
print(matches[0]["identifier"])
PY
)"
printf '%s\n' "$device_id" > "$EVIDENCE_DIR/device-id-private.txt"

install_json="$EVIDENCE_DIR/install-private.json"
xcrun devicectl device install app --device "$device_id" "$APP_DIR" \
  --json-output "$install_json" --timeout 120 --quiet
bundle_id="$(plutil -extract CFBundleIdentifier raw "$APP_DIR/Info.plist")"
runtime_log="$EVIDENCE_DIR/runtime-private.log"
echo "Launching the iPhone foundation. Press Control-C after the physical checks are complete."
xcrun devicectl device process launch --device "$device_id" --terminate-existing \
  --console "$bundle_id" 2>&1 | tee "$runtime_log"
