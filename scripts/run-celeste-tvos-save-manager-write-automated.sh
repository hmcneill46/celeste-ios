#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/run-celeste-tvos-save-manager-write-automated.sh --app DIR --settings FILE --save FILE [options]

Install an explicitly automation-enabled, acceptance-namespace Stage 10B app
and stress the physical writable listener from this Mac. Fixtures and all
credentials remain in ignored evidence. Never use a production-namespace app.

Options:
  --app DIR           Signed Release tvOS arm64 automation app
  --settings FILE     Ignored serializer-valid settings.celeste fixture
  --save FILE         Ignored serializer-valid SaveData .celeste fixture
  --device-id ID      Ignored device ID; otherwise discover paired Apple TV
  --evidence-dir DIR  Ignored output root
  -h, --help          Show help
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP=""; SETTINGS=""; SAVE=""; DEVICE_ID=""; EVIDENCE_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage10b/automated-device"
repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --app) APP="$(repo_path "$2")"; shift 2 ;;
    --settings) SETTINGS="$(repo_path "$2")"; shift 2 ;;
    --save) SAVE="$(repo_path "$2")"; shift 2 ;;
    --device-id) DEVICE_ID="$2"; shift 2 ;;
    --evidence-dir) EVIDENCE_DIR="$(repo_path "$2")"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done
[[ -d "$APP" && -f "$SETTINGS" && -f "$SAVE" ]] || { echo "error: --app, --settings, and --save are required" >&2; exit 2; }
case "$EVIDENCE_DIR" in "$REPO_ROOT/artifacts/"*|"$REPO_ROOT/.build/"*) ;; *) echo "error: evidence must remain below an ignored output root" >&2; exit 2 ;; esac
mkdir -p "$EVIDENCE_DIR"
git -C "$REPO_ROOT" check-ignore --no-index -q -- "${EVIDENCE_DIR#$REPO_ROOT/}/console-private.log" || { echo "error: evidence root is not ignored" >&2; exit 1; }
git -C "$REPO_ROOT" check-ignore --no-index -q -- "${SETTINGS#$REPO_ROOT/}" || { echo "error: Settings fixture must be ignored" >&2; exit 1; }
git -C "$REPO_ROOT" check-ignore --no-index -q -- "${SAVE#$REPO_ROOT/}" || { echo "error: SaveData fixture must be ignored" >&2; exit 1; }
for tool in xcrun plutil codesign python3 git; do command -v "$tool" >/dev/null || { echo "error: missing tool: $tool" >&2; exit 1; }; done

codesign --verify --deep --strict "$APP"
BUNDLE_ID="$(plutil -extract CFBundleIdentifier raw "$APP/Info.plist")"
EXECUTABLE="$(plutil -extract CFBundleExecutable raw "$APP/Info.plist")"
python3 - "$APP" "$EXECUTABLE" <<'PY'
import pathlib,sys
app=pathlib.Path(sys.argv[1]); files=[p for p in (app/sys.argv[2],app/'CelesteTvOSRuntimeHost.dll') if p.is_file()]
def present(value):
    return any(value.encode() in p.read_bytes() or value.encode('utf-16le') in p.read_bytes() for p in files)
if not present('STAGE10A_AUTOMATION_READY'): raise SystemExit('error: app lacks SaveManagerAutomation=true')
if not present('Acceptance.v1'): raise SystemExit('error: app is not locked to the isolated acceptance namespace')
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
terminate_app() {
  xcrun devicectl device info processes --quiet --timeout 15 --device "$DEVICE_ID" --json-output "$EVIDENCE_DIR/processes-private.json" --log-output "$EVIDENCE_DIR/processes-private.log" >/dev/null 2>&1 || return 0
  local remote_pid
  remote_pid="$(python3 - "$EVIDENCE_DIR/processes-private.json" "$EXECUTABLE" <<'PY'
import json,pathlib,sys,urllib.parse
items=json.loads(pathlib.Path(sys.argv[1]).read_text()).get('result',{}).get('runningProcesses',[])
matches=[str(x['processIdentifier']) for x in items if urllib.parse.unquote(x.get('executable','')).rstrip('/').rsplit('/',1)[-1]==sys.argv[2]]
print(matches[0] if len(matches)==1 else '')
PY
)"
  [[ -z "$remote_pid" ]] || xcrun devicectl device process terminate --quiet --timeout 15 --device "$DEVICE_ID" --pid "$remote_pid" --json-output "$EVIDENCE_DIR/terminate-private.json" --log-output "$EVIDENCE_DIR/terminate-private.log" >/dev/null 2>&1 || true
}
cleanup() {
  trap - EXIT INT TERM
  if [[ -n "$LAUNCH_PID" ]]; then
    kill "$LAUNCH_PID" 2>/dev/null || true
    for _ in {1..20}; do
      kill -0 "$LAUNCH_PID" 2>/dev/null || break
      sleep 0.1
    done
    # CoreDevice's console process can ignore TERM while its remote stream is
    # attached. Kill only the exact child started above so cleanup stays
    # bounded; the separately resolved app PID is terminated immediately next.
    kill -KILL "$LAUNCH_PID" 2>/dev/null || true
    wait "$LAUNCH_PID" 2>/dev/null || true
  fi
  terminate_app
}
trap cleanup EXIT INT TERM

xcrun devicectl device install app --quiet --device "$DEVICE_ID" "$APP" --json-output "$EVIDENCE_DIR/install-private.json" --log-output "$EVIDENCE_DIR/install-private.log" >/dev/null 2>&1
rm -f "$EVIDENCE_DIR/console-private.log"
xcrun devicectl device process launch --console --terminate-existing --timeout 420 --device "$DEVICE_ID" "$BUNDLE_ID" --json-output "$EVIDENCE_DIR/launch-private.json" --log-output "$EVIDENCE_DIR/launch-tool-private.log" >"$EVIDENCE_DIR/console-private.log" 2>&1 &
LAUNCH_PID=$!
for _ in {1..120}; do grep -Fq 'STAGE10A_AUTOMATION_READY' "$EVIDENCE_DIR/console-private.log" 2>/dev/null && break; sleep 2; done
grep -Fq 'STAGE10A_AUTOMATION_READY' "$EVIDENCE_DIR/console-private.log" || { echo "error: automation listener did not reach ready" >&2; exit 1; }
python3 - "$EVIDENCE_DIR/console-private.log" "$EVIDENCE_DIR/credentials-private.txt" <<'PY'
import os,pathlib,re,sys
text=pathlib.Path(sys.argv[1]).read_text(errors='replace')
matches=re.findall(r'STAGE10A_AUTOMATION_READY url=(http://[^; ]+/); access-code=([0-9]{6})',text)
if not matches: raise SystemExit('error: ignored automation credentials were not captured')
path=pathlib.Path(sys.argv[2]); path.write_text(matches[-1][0]+'\n'+matches[-1][1]+'\n'); os.chmod(path,0o600)
PY
IFS= read -r URL < "$EVIDENCE_DIR/credentials-private.txt"
CODE="$(sed -n '2p' "$EVIDENCE_DIR/credentials-private.txt")"
"$REPO_ROOT/scripts/verify-celeste-tvos-stage10b-live.py" --url "$URL" --code "$CODE" --settings "$SETTINGS" --save "$SAVE" --output "$EVIDENCE_DIR/live-summary.json" >"$EVIDENCE_DIR/live-private.log" 2>&1
grep -Fq 'STAGE10B_MUTATION operation=replace' "$EVIDENCE_DIR/console-private.log" || { echo "error: device log lacks replacement evidence" >&2; exit 1; }
grep -Fq 'STAGE10B_MUTATION operation=delete' "$EVIDENCE_DIR/console-private.log" || { echo "error: device log lacks deletion evidence" >&2; exit 1; }
grep -Fq 'restart-required=true' "$EVIDENCE_DIR/console-private.log" || { echo "error: device log lacks restart-required evidence" >&2; exit 1; }
if grep -Eiq 'SizeLimitExceeded|tvStubs (invoked|call detected)|STAGE3B_TVSTUB|FMOD_GUARD_REACHED|Unhandled managed exception' "$EVIDENCE_DIR/console-private.log"; then
  echo "error: device log contains a persistence/native failure marker" >&2; exit 1
fi
echo "PASS: isolated physical writable Save Manager stress completed; production saves were not used as the mutation target"
