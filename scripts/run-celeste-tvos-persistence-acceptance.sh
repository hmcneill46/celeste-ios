#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/run-celeste-tvos-persistence-acceptance.sh --app DIR --phase NAME [options]

Install and capture a signed Stage 6 app on the unique paired AppleTV14,1.
Raw device data remains below an ignored evidence directory.

Phases:
  diagnostic              Run the isolated 34-test persistence suite
  process-kill-prepare     Let the user save state, then externally terminate
  process-kill-verify      Relaunch and verify the prior generation/hash
  prepare-before-restart   Save a restart token and print READY FOR APPLE TV RESTART
  verify-after-restart     Relaunch after a real Apple TV restart and compare
  replacement-install     Install this app over the existing same-bundle app and verify

Options:
  --app DIR             Signed tvOS arm64 app
  --phase NAME          One phase above
  --device-id ID        Ignored device ID; otherwise discover paired AppleTV14,1
  --evidence-dir DIR    Ignored root (default: artifacts/celeste-runtime/stage6-device)
  --duration SECONDS    Capture timeout (default 600)
  -h, --help            Show help

The restart phases never erase, reset, unpair, or uninstall the Apple TV app.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP_DIR=""; PHASE=""; DEVICE_ID=""; EVIDENCE_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage6-device"; DURATION=600
repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --app) APP_DIR="$(repo_path "$2")"; shift 2 ;;
    --phase) PHASE="$2"; shift 2 ;;
    --device-id) DEVICE_ID="$2"; shift 2 ;;
    --evidence-dir) EVIDENCE_DIR="$(repo_path "$2")"; shift 2 ;;
    --duration) DURATION="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done
[[ -d "$APP_DIR" ]] || { echo "error: --app is required" >&2; exit 2; }
case "$PHASE" in diagnostic|process-kill-prepare|process-kill-verify|prepare-before-restart|verify-after-restart|replacement-install) ;; *) echo "error: unsupported --phase" >&2; exit 2 ;; esac
[[ "$DURATION" =~ ^[0-9]+$ && "$DURATION" -ge 60 && "$DURATION" -le 900 ]] || { echo "error: duration must be 60-900" >&2; exit 2; }
case "$EVIDENCE_DIR" in "$REPO_ROOT/artifacts/celeste-runtime/"*|"$REPO_ROOT/.build/celeste-runtime/"*) ;; *) echo "error: evidence root is not an ignored runtime path" >&2; exit 2 ;; esac
mkdir -p "$EVIDENCE_DIR/$PHASE"
OUT="$EVIDENCE_DIR/$PHASE"
git -C "$REPO_ROOT" check-ignore --no-index -q -- "${OUT#$REPO_ROOT/}/console.log" || { echo "error: evidence root is not ignored" >&2; exit 1; }
for command in xcrun python3 plutil codesign git; do command -v "$command" >/dev/null || { echo "error: missing tool: $command" >&2; exit 1; }; done
"$REPO_ROOT/scripts/verify-celeste-tvos-stage6.sh" --app "$APP_DIR" --platform device >/dev/null
codesign --verify --deep --strict "$APP_DIR"
xcrun devicectl list devices --json-output "$OUT/devices-private.json" >"$OUT/devices-private.log" 2>&1
if [[ -z "$DEVICE_ID" ]]; then
  DEVICE_ID="$(python3 - "$OUT/devices-private.json" <<'PY'
import json,pathlib,sys
items=json.loads(pathlib.Path(sys.argv[1]).read_text()).get("result",{}).get("devices",[])
matches=[x["identifier"] for x in items if x.get("hardwareProperties",{}).get("productType")=="AppleTV14,1" and x.get("hardwareProperties",{}).get("reality")=="physical" and x.get("connectionProperties",{}).get("pairingState")=="paired"]
if len(matches)!=1: raise SystemExit(f"error: expected one paired physical AppleTV14,1, found {len(matches)}")
print(matches[0])
PY
)"
fi
BUNDLE_ID="$(plutil -extract CFBundleIdentifier raw "$APP_DIR/Info.plist")"
EXECUTABLE="$(plutil -extract CFBundleExecutable raw "$APP_DIR/Info.plist")"

terminate_app() {
  xcrun devicectl device info processes --quiet --timeout 15 --device "$DEVICE_ID" --json-output "$OUT/processes-private.json" --log-output "$OUT/processes-private.log" >/dev/null 2>&1 || return 0
  local remote_pid
  remote_pid="$(python3 - "$OUT/processes-private.json" "$EXECUTABLE" <<'PY'
import json,pathlib,sys,urllib.parse
items=json.loads(pathlib.Path(sys.argv[1]).read_text()).get("result",{}).get("runningProcesses",[])
matches=[str(x["processIdentifier"]) for x in items if urllib.parse.unquote(x.get("executable","")).rstrip("/").rsplit("/",1)[-1]==sys.argv[2]]
print(matches[0] if len(matches)==1 else "")
PY
)"
  [[ -z "$remote_pid" ]] || xcrun devicectl device process terminate --quiet --timeout 15 --device "$DEVICE_ID" --pid "$remote_pid" --json-output "$OUT/terminate-private.json" --log-output "$OUT/terminate-private.log" >/dev/null 2>&1 || true
}

stop_console_capture() {
  [[ -n "${LAUNCH_PID:-}" ]] || return 0
  kill -TERM "$LAUNCH_PID" 2>/dev/null || true
  for _ in {1..30}; do
    kill -0 "$LAUNCH_PID" 2>/dev/null || break
    sleep 0.1
  done
  kill -KILL "$LAUNCH_PID" 2>/dev/null || true
  wait "$LAUNCH_PID" 2>/dev/null || true
  LAUNCH_PID=""
}

rm -f -- "$OUT/console.log" "$OUT/summary.json"
xcrun devicectl device install app --quiet --device "$DEVICE_ID" "$APP_DIR" --json-output "$OUT/install-private.json" --log-output "$OUT/install-private.log" >/dev/null 2>&1
xcrun devicectl device process launch --console --terminate-existing --timeout "$DURATION" --device "$DEVICE_ID" "$BUNDLE_ID" --json-output "$OUT/launch-private.json" --log-output "$OUT/launch-private.log" >"$OUT/console.log" 2>&1 &
LAUNCH_PID=$!
cleanup() {
  trap - EXIT INT TERM
  stop_console_capture
  terminate_app
}
trap cleanup EXIT INT TERM
for _ in {1..90}; do grep -Fq 'STAGE6_READY' "$OUT/console.log" 2>/dev/null && break; kill -0 "$LAUNCH_PID" 2>/dev/null || break; sleep 2; done
grep -Fq 'STAGE6_READY' "$OUT/console.log" || { echo "error: Stage 6 did not become ready" >&2; exit 1; }
xcrun devicectl device process launch --quiet --device "$DEVICE_ID" "$BUNDLE_ID" --json-output "$OUT/activate-private.json" --log-output "$OUT/activate-private.log" >/dev/null 2>&1

case "$PHASE" in
  diagnostic)
    for _ in {1..90}; do grep -Fq 'STAGE6_DIAGNOSTIC_PASS' "$OUT/console.log" 2>/dev/null && break; sleep 2; done
    "$REPO_ROOT/scripts/verify-celeste-tvos-stage6-evidence.py" --log "$OUT/console.log" --scenario diagnostic --output "$OUT/summary.json"
    ;;
  process-kill-prepare)
    echo "READY FOR EXTERNAL PROCESS-KILL TEST"
    echo "1. Navigate to a Settings control or a point that triggers a normal Celeste save."
    echo "2. Press Enter here, then make exactly one deliberate change."
    read -r _
    initial_commits="$(grep -Fc 'result=committed' "$OUT/console.log" 2>/dev/null || true)"
    echo "WAITING FOR THE NEXT VERIFIED DURABLE COMMIT"
    observed=0
    for _ in $(seq 1 1200); do
      current_commits="$(grep -Fc 'result=committed' "$OUT/console.log" 2>/dev/null || true)"
      if [[ "$current_commits" -gt "$initial_commits" ]]; then
        observed=1
        break
      fi
      sleep 0.05
    done
    [[ "$observed" -eq 1 ]] || { echo "error: no new complete durable commit observed" >&2; exit 1; }
    terminate_app
    python3 - "$OUT/console.log" "$EVIDENCE_DIR/restart-token.json" <<'PY'
import json,pathlib,re,sys
text=pathlib.Path(sys.argv[1]).read_text(errors="replace")
matches=re.findall(r"STAGE6_COMMIT .*result=committed; .*generation=(\d+); logical=([0-9a-f]{64});",text)
if not matches: raise SystemExit("error: no complete generation")
generation,digest=matches[-1]
pathlib.Path(sys.argv[2]).write_text(json.dumps({"schemaVersion":1,"generation":int(generation),"logicalSha256":digest},indent=2,sort_keys=True)+"\n")
PY
    "$REPO_ROOT/scripts/verify-celeste-tvos-stage6-evidence.py" --log "$OUT/console.log" --scenario process-kill --output "$OUT/summary.json"
    echo "PASS: app was externally terminated immediately after read-back verification of the new slot"
    ;;
  prepare-before-restart)
    echo "READY FOR STAGE 6 SAVE TEST"
    echo "1. Create or update slots 0 and 1; delete/recreate a slot if this is the full acceptance run."
    echo "2. Set distinctive Music/SFX volumes and one non-default controller binding."
    echo "3. Background and foreground once; confirm audio and haptics resume safely."
    echo "4. Return to gameplay, wait for the save icon to finish, then press Enter here."
    read -r _
    grep -Fq 'result=committed' "$OUT/console.log" || { echo "error: no durable commit observed" >&2; exit 1; }
    grep -Fq 'STAGE6_LIFECYCLE_FLUSH reason=background; result=completed-or-unchanged' "$OUT/console.log" || { echo "error: no successful background flush observed" >&2; exit 1; }
    terminate_app
    python3 - "$OUT/console.log" "$EVIDENCE_DIR/restart-token.json" <<'PY'
import json,pathlib,re,sys
text=pathlib.Path(sys.argv[1]).read_text(errors="replace")
matches=re.findall(r"STAGE6_COMMIT .*result=committed; .*generation=(\d+); logical=([0-9a-f]{64});",text)
if not matches: raise SystemExit("error: no complete generation")
generation,digest=matches[-1]
pathlib.Path(sys.argv[2]).write_text(json.dumps({"schemaVersion":1,"generation":int(generation),"logicalSha256":digest},indent=2,sort_keys=True)+"\n")
PY
    echo "READY FOR APPLE TV RESTART"
    ;;
  process-kill-verify|verify-after-restart|replacement-install)
    [[ -f "$EVIDENCE_DIR/restart-token.json" ]] || { echo "error: prepare phase token missing" >&2; exit 1; }
    for _ in {1..30}; do grep -Fq 'STAGE6_RESTORE namespace=' "$OUT/console.log" 2>/dev/null && break; sleep 2; done
    "$REPO_ROOT/scripts/verify-celeste-tvos-stage6-evidence.py" --log "$OUT/console.log" --scenario restart-verify --expected-token "$EVIDENCE_DIR/restart-token.json" --output "$OUT/summary.json"
    echo "Verify in-game Settings, slots, audio volumes, and controller binding now; press Enter when confirmed."
    read -r _
    ;;
esac
echo "PASS: $PHASE machine evidence; private device data remains ignored"
