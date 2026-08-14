#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/run-celeste-tvos-save-manager-automated.sh --app DIR [options]

Install an explicitly automation-enabled, signed Stage 10A acceptance app and
stress its real physical Apple TV listener from this Mac. The temporary URL and
code remain only in ignored evidence. Production builds do not contain this lane.

Options:
  --app DIR           Signed Release tvOS arm64 automation app (required)
  --device-id ID      Ignored device ID; otherwise discover the paired Apple TV
  --evidence-dir DIR  Ignored output root
  -h, --help          Show help
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP=""; DEVICE_ID=""; EVIDENCE_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage10a/automated-device"
repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --app) APP="$(repo_path "$2")"; shift 2 ;;
    --device-id) DEVICE_ID="$2"; shift 2 ;;
    --evidence-dir) EVIDENCE_DIR="$(repo_path "$2")"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done
[[ -d "$APP" ]] || { echo "error: --app must name a signed app bundle" >&2; exit 2; }
case "$EVIDENCE_DIR" in "$REPO_ROOT/artifacts/"*|"$REPO_ROOT/.build/"*) ;; *) echo "error: evidence must remain below an ignored repository output root" >&2; exit 2 ;; esac
mkdir -p "$EVIDENCE_DIR"
git -C "$REPO_ROOT" check-ignore --no-index -q -- "${EVIDENCE_DIR#$REPO_ROOT/}/console-private.log" || { echo "error: evidence root is not ignored" >&2; exit 1; }
for tool in xcrun plutil codesign python3 git; do command -v "$tool" >/dev/null || { echo "error: missing tool: $tool" >&2; exit 1; }; done

codesign --verify --deep --strict "$APP"
BUNDLE_ID="$(plutil -extract CFBundleIdentifier raw "$APP/Info.plist")"
EXECUTABLE="$(plutil -extract CFBundleExecutable raw "$APP/Info.plist")"
python3 - "$APP" "$EXECUTABLE" <<'PY'
import pathlib,sys
app=pathlib.Path(sys.argv[1])
files=[p for p in (app/sys.argv[2], app/'CelesteTvOSRuntimeHost.dll') if p.is_file()]
token=b'STAGE10A_AUTOMATION_READY'
utf16='STAGE10A_AUTOMATION_READY'.encode('utf-16le')
if not any(token in p.read_bytes() or utf16 in p.read_bytes() for p in files):
    raise SystemExit('error: app was not built with SaveManagerAutomation=true')
PY

xcrun devicectl list devices --json-output "$EVIDENCE_DIR/devices-private.json" >"$EVIDENCE_DIR/devices-private.log" 2>&1
if [[ -z "$DEVICE_ID" ]]; then
  DEVICE_ID="$(python3 - "$EVIDENCE_DIR/devices-private.json" <<'PY'
import json,pathlib,sys
items=json.loads(pathlib.Path(sys.argv[1]).read_text()).get('result',{}).get('devices',[])
matches=[x['identifier'] for x in items if x.get('hardwareProperties',{}).get('productType')=='AppleTV14,1' and x.get('hardwareProperties',{}).get('reality')=='physical' and x.get('connectionProperties',{}).get('pairingState')=='paired']
if len(matches)!=1: raise SystemExit(f'error: expected one paired physical AppleTV14,1, found {len(matches)}')
print(matches[0])
PY
)"
fi

LAUNCH_PID=""
stop_console_capture() {
  local pid="$1"
  [[ -z "$pid" ]] && return 0
  kill "$pid" 2>/dev/null || true
  for _ in {1..10}; do kill -0 "$pid" 2>/dev/null || break; sleep 0.2; done
  kill -9 "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true
}
terminate_app() {
  xcrun devicectl device info processes --quiet --timeout 15 --device "$DEVICE_ID" --json-output "$EVIDENCE_DIR/processes-private.json" --log-output "$EVIDENCE_DIR/processes-private.log" >/dev/null 2>&1 || return 0
  local pid
  pid="$(python3 - "$EVIDENCE_DIR/processes-private.json" "$EXECUTABLE" <<'PY'
import json,pathlib,sys,urllib.parse
items=json.loads(pathlib.Path(sys.argv[1]).read_text()).get('result',{}).get('runningProcesses',[])
matches=[str(x['processIdentifier']) for x in items if urllib.parse.unquote(x.get('executable','')).rstrip('/').rsplit('/',1)[-1]==sys.argv[2]]
print(matches[0] if len(matches)==1 else '')
PY
)"
  [[ -z "$pid" ]] || xcrun devicectl device process terminate --quiet --timeout 15 --device "$DEVICE_ID" --pid "$pid" --json-output "$EVIDENCE_DIR/terminate-private.json" --log-output "$EVIDENCE_DIR/terminate-private.log" >/dev/null 2>&1 || true
}
cleanup() {
  trap - EXIT INT TERM
  stop_console_capture "$LAUNCH_PID"
  terminate_app
}
trap cleanup EXIT INT TERM

credentials() {
  local log="$1" output="$2"
  python3 - "$log" "$output" <<'PY'
import os,pathlib,re,sys
text=pathlib.Path(sys.argv[1]).read_text(errors='replace')
matches=re.findall(r'STAGE10A_AUTOMATION_READY url=(http://[^; ]+/); access-code=([0-9]{6})',text)
if not matches: raise SystemExit('error: automation listener credentials were not captured')
path=pathlib.Path(sys.argv[2]); path.write_text(matches[-1][0]+'\n'+matches[-1][1]+'\n'); os.chmod(path,0o600)
PY
}

launch_and_wait() {
  local log="$1" json="$2" tool_log="$3"
  rm -f "$log"
  xcrun devicectl device process launch --console --terminate-existing --timeout 420 --device "$DEVICE_ID" "$BUNDLE_ID" --json-output "$json" --log-output "$tool_log" >"$log" 2>&1 &
  LAUNCH_PID=$!
  for _ in {1..120}; do grep -Fq 'STAGE10A_AUTOMATION_READY' "$log" 2>/dev/null && return 0; sleep 2; done
  echo "error: automation listener did not reach ready" >&2; return 1
}

rm -rf "$EVIDENCE_DIR/downloads-first" "$EVIDENCE_DIR/downloads-second"
xcrun devicectl device install app --quiet --device "$DEVICE_ID" "$APP" --json-output "$EVIDENCE_DIR/install-private.json" --log-output "$EVIDENCE_DIR/install-private.log" >/dev/null 2>&1
launch_and_wait "$EVIDENCE_DIR/console-private.log" "$EVIDENCE_DIR/launch-private.json" "$EVIDENCE_DIR/launch-tool-private.log"
credentials "$EVIDENCE_DIR/console-private.log" "$EVIDENCE_DIR/credentials-private.txt"
IFS= read -r URL < "$EVIDENCE_DIR/credentials-private.txt"
CODE="$(sed -n '2p' "$EVIDENCE_DIR/credentials-private.txt")"
"$REPO_ROOT/scripts/verify-celeste-tvos-stage10a-live.py" --url "$URL" --code "$CODE" --output "$EVIDENCE_DIR/live-first.json" --downloads "$EVIDENCE_DIR/downloads-first" >"$EVIDENCE_DIR/live-first-private.log" 2>&1 || {
  echo "error: first physical listener stress run failed; see ignored live-first-private.log" >&2
  exit 1
}
"$REPO_ROOT/scripts/verify-celeste-tvos-stage10a-downloads.py" --log "$EVIDENCE_DIR/console-private.log" --downloads "$EVIDENCE_DIR/downloads-first"
python3 - "$EVIDENCE_DIR/console-private.log" <<'PY'
import pathlib,re,sys
text=pathlib.Path(sys.argv[1]).read_text(errors='replace')
accepted=text.count('STAGE10A_CONNECTION result=accepted')
active=[int(x) for x in re.findall(r'STAGE10A_CONNECTION result=(?:accepted|closed);[^\n]* active=([0-9]+)',text)]
if accepted < 20: raise SystemExit(f'error: physical listener accepted only {accepted} lifetime connections')
if not active or max(active)>4 or active[-1]!=0: raise SystemExit('error: physical connection bound/retirement evidence failed')
PY
FIRST_CODE="$CODE"
terminate_app
stop_console_capture "$LAUNCH_PID"; LAUNCH_PID=""
python3 - "$URL" <<'PY'
import sys,urllib.request
try: urllib.request.urlopen(sys.argv[1],timeout=3)
except Exception: raise SystemExit(0)
raise SystemExit('error: old Save Manager URL remained reachable after process termination')
PY

launch_and_wait "$EVIDENCE_DIR/second-console-private.log" "$EVIDENCE_DIR/second-launch-private.json" "$EVIDENCE_DIR/second-launch-tool-private.log"
credentials "$EVIDENCE_DIR/second-console-private.log" "$EVIDENCE_DIR/second-credentials-private.txt"
IFS= read -r URL < "$EVIDENCE_DIR/second-credentials-private.txt"
CODE="$(sed -n '2p' "$EVIDENCE_DIR/second-credentials-private.txt")"
[[ "$CODE" != "$FIRST_CODE" ]] || { echo "error: second launch reused the prior access code" >&2; exit 1; }
"$REPO_ROOT/scripts/verify-celeste-tvos-stage10a-live.py" --url "$URL" --code "$CODE" --output "$EVIDENCE_DIR/live-second.json" --downloads "$EVIDENCE_DIR/downloads-second" >"$EVIDENCE_DIR/live-second-private.log" 2>&1 || {
  echo "error: second physical listener stress run failed; see ignored live-second-private.log" >&2
  exit 1
}
"$REPO_ROOT/scripts/verify-celeste-tvos-stage10a-downloads.py" --log "$EVIDENCE_DIR/second-console-private.log" --downloads "$EVIDENCE_DIR/downloads-second"
echo "PASS: automated physical Save Manager authentication, sequential/parallel downloads, archive, shutdown, and clean second launch"
