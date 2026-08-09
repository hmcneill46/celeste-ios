#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/run-celeste-tvos-save-manager-acceptance.sh --app DIR [options]

Install a signed same-identity Stage 10A app on the paired AppleTV14,1,
capture bounded console/Bonjour evidence, and guide one external-browser test.
Nothing is uninstalled and production saves are not cleared.

Options:
  --app DIR           Signed Release tvOS arm64 app (required)
  --device-id ID      Ignored device ID; otherwise discover the paired Apple TV
  --evidence-dir DIR  Ignored output root
  --duration SECONDS  Console timeout, 300-1200 (default 900)
  --skip-install      Test the already installed same-identity app
  -h, --help          Show help
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP=""; DEVICE_ID=""; EVIDENCE_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage10a/device"; DURATION=900; INSTALL=1
repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --app) APP="$(repo_path "$2")"; shift 2 ;;
    --device-id) DEVICE_ID="$2"; shift 2 ;;
    --evidence-dir) EVIDENCE_DIR="$(repo_path "$2")"; shift 2 ;;
    --duration) DURATION="$2"; shift 2 ;;
    --skip-install) INSTALL=0; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done
[[ -d "$APP" ]] || { echo "error: --app must name a signed app bundle" >&2; exit 2; }
[[ "$DURATION" =~ ^[0-9]+$ && "$DURATION" -ge 300 && "$DURATION" -le 1200 ]] || { echo "error: --duration must be 300-1200" >&2; exit 2; }
case "$EVIDENCE_DIR" in "$REPO_ROOT/artifacts/"*|"$REPO_ROOT/.build/"*) ;; *) echo "error: evidence must remain below an ignored repository output root" >&2; exit 2 ;; esac
mkdir -p "$EVIDENCE_DIR"
git -C "$REPO_ROOT" check-ignore --no-index -q -- "${EVIDENCE_DIR#$REPO_ROOT/}/console.log" || { echo "error: evidence root is not ignored" >&2; exit 1; }
for tool in xcrun plutil codesign python3 dns-sd git; do command -v "$tool" >/dev/null || { echo "error: missing tool: $tool" >&2; exit 1; }; done

"$REPO_ROOT/scripts/verify-celeste-tvos-stage10a.sh" --app "$APP" --platform device --signed --output "$EVIDENCE_DIR/package-summary.json" >/dev/null
codesign --verify --deep --strict "$APP"
BUNDLE_ID="$(plutil -extract CFBundleIdentifier raw "$APP/Info.plist")"
EXECUTABLE="$(plutil -extract CFBundleExecutable raw "$APP/Info.plist")"
xcrun devicectl list devices --json-output "$EVIDENCE_DIR/devices-private.json" >"$EVIDENCE_DIR/devices-private.log" 2>&1
if [[ -z "$DEVICE_ID" ]]; then
  DEVICE_ID="$(python3 - "$EVIDENCE_DIR/devices-private.json" <<'PY'
import json,pathlib,sys
items=json.loads(pathlib.Path(sys.argv[1]).read_text()).get("result",{}).get("devices",[])
matches=[x["identifier"] for x in items if x.get("hardwareProperties",{}).get("productType")=="AppleTV14,1" and x.get("hardwareProperties",{}).get("reality")=="physical" and x.get("connectionProperties",{}).get("pairingState")=="paired"]
if len(matches)!=1: raise SystemExit(f"error: expected one paired physical AppleTV14,1, found {len(matches)}")
print(matches[0])
PY
)"
fi

LAUNCH_PID=""; BONJOUR_PID=""
stop_capture_process() {
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
items=json.loads(pathlib.Path(sys.argv[1]).read_text()).get("result",{}).get("runningProcesses",[])
matches=[str(x["processIdentifier"]) for x in items if urllib.parse.unquote(x.get("executable","")).rstrip("/").rsplit("/",1)[-1]==sys.argv[2]]
print(matches[0] if len(matches)==1 else "")
PY
)"
  [[ -z "$pid" ]] || xcrun devicectl device process terminate --quiet --timeout 15 --device "$DEVICE_ID" --pid "$pid" --json-output "$EVIDENCE_DIR/terminate-private.json" --log-output "$EVIDENCE_DIR/terminate-private.log" >/dev/null 2>&1 || true
}
cleanup() {
  trap - EXIT INT TERM
  stop_capture_process "$BONJOUR_PID"
  stop_capture_process "$LAUNCH_PID"
  terminate_app
}
trap cleanup EXIT INT TERM

rm -f "$EVIDENCE_DIR/console.log" "$EVIDENCE_DIR/bonjour.log" "$EVIDENCE_DIR/second-launch.log"
if [[ "$INSTALL" -eq 1 ]]; then
  xcrun devicectl device install app --quiet --device "$DEVICE_ID" "$APP" --json-output "$EVIDENCE_DIR/install-private.json" --log-output "$EVIDENCE_DIR/install-private.log" >/dev/null 2>&1
fi
xcrun devicectl device process launch --console --terminate-existing --timeout "$DURATION" --device "$DEVICE_ID" "$BUNDLE_ID" --json-output "$EVIDENCE_DIR/launch-private.json" --log-output "$EVIDENCE_DIR/launch-private.log" >"$EVIDENCE_DIR/console.log" 2>&1 &
LAUNCH_PID=$!
dns-sd -B _celeste-save._tcp local. >"$EVIDENCE_DIR/bonjour.log" 2>&1 &
BONJOUR_PID=$!
for _ in {1..90}; do grep -Fq 'name=first-celeste-draw' "$EVIDENCE_DIR/console.log" 2>/dev/null && break; sleep 2; done
grep -Fq 'name=first-celeste-draw' "$EVIDENCE_DIR/console.log" || { echo "error: Celeste did not reach first draw" >&2; exit 1; }
grep -Fq 'STAGE10A_DORMANT listener=false; bonjour=false' "$EVIDENCE_DIR/console.log" || { echo "error: dormant checkpoint missing" >&2; exit 1; }
if grep -Fq 'STAGE10A_READY' "$EVIDENCE_DIR/console.log"; then echo "error: listener started before explicit Save Manager activation" >&2; exit 1; fi

echo "READY FOR EXTERNAL SAVE MANAGER TEST"
echo "1. On Apple TV, open Options and select Save Manager."
echo "2. On another phone or computer on the same LAN, open the numeric URL shown on the TV."
echo "3. Enter one wrong code (it must fail), then enter the displayed code."
echo "4. Choose Download all files (.zip) once. Confirm the archive contains every present .celeste file."
echo "5. Confirm the page labels absent slots as absent and keep the Save Manager screen visible until the download finishes."
echo "6. Then press Enter here."
read -r _
grep -Fq 'STAGE10A_READY' "$EVIDENCE_DIR/console.log" || { echo "error: listener never reached ready" >&2; exit 1; }
grep -Fq 'STAGE10A_IDLE_TIMER suppressed=true' "$EVIDENCE_DIR/console.log" || { echo "error: Save Manager did not suppress idle screensaver activation" >&2; exit 1; }
grep -Fq 'STAGE10A_HTTP status=401' "$EVIDENCE_DIR/console.log" || { echo "error: wrong-code rejection was not observed" >&2; exit 1; }
grep -Fq 'auth=accepted' "$EVIDENCE_DIR/console.log" || { echo "error: successful authentication was not observed" >&2; exit 1; }
if ! grep -Fq 'download-sha256=' "$EVIDENCE_DIR/console.log"; then
  if grep -Fq 'STAGE10A_STOP reason=resign-active' "$EVIDENCE_DIR/console.log"; then
    echo "error: Save Manager resigned active before a download reached it; reopen it and use the newly displayed URL" >&2
  else
    echo "error: no download was observed" >&2
  fi
  exit 1
fi
grep -Fq 'STAGE10A_BONJOUR change=add' "$EVIDENCE_DIR/console.log" || { echo "error: Network framework did not advertise Bonjour" >&2; exit 1; }

echo "READY TO STOP SAVE MANAGER"
echo "Press Back/Stop on Apple TV. Confirm the old browser URL no longer connects, then press Enter here."
read -r _
for _ in {1..20}; do grep -Fq 'STAGE10A_STOP reason=ui-back' "$EVIDENCE_DIR/console.log" && break; sleep 1; done
grep -Fq 'STAGE10A_STOP reason=ui-back' "$EVIDENCE_DIR/console.log" || { echo "error: UI stop checkpoint missing" >&2; exit 1; }
grep -Fq 'STAGE10A_IDLE_TIMER suppressed=false' "$EVIDENCE_DIR/console.log" || { echo "error: normal tvOS idle behaviour was not restored" >&2; exit 1; }

echo "READY FOR GAME REGRESSION"
echo "Resume gameplay, confirm controller/audio, trigger one normal save, then press Enter here."
read -r _
grep -Fq 'banks=7' "$EVIDENCE_DIR/console.log" || { echo "error: seven-bank FMOD startup evidence missing" >&2; exit 1; }
grep -Fq 'STAGE6_COMMIT' "$EVIDENCE_DIR/console.log" || { echo "error: Stage 9B save evidence missing" >&2; exit 1; }
if grep -E 'STAGE3B_FATAL|unhandled managed exception|tvStubs (invoked|call detected)|STAGE6_DEFAULTS_SIZE_LIMIT_WARNING' "$EVIDENCE_DIR/console.log"; then
  echo "error: runtime regression detected" >&2; exit 1
fi
terminate_app
stop_capture_process "$LAUNCH_PID"; LAUNCH_PID=""
xcrun devicectl device process launch --console --terminate-existing --timeout 90 --device "$DEVICE_ID" "$BUNDLE_ID" --json-output "$EVIDENCE_DIR/second-launch-private.json" --log-output "$EVIDENCE_DIR/second-launch-private.log" >"$EVIDENCE_DIR/second-launch.log" 2>&1 &
LAUNCH_PID=$!
for _ in {1..45}; do grep -Fq 'name=first-celeste-draw' "$EVIDENCE_DIR/second-launch.log" 2>/dev/null && break; sleep 2; done
grep -Fq 'name=first-celeste-draw' "$EVIDENCE_DIR/second-launch.log" || { echo "error: clean second launch did not reach first draw" >&2; exit 1; }
grep -Fq 'STAGE6_RESTORE namespace=production' "$EVIDENCE_DIR/second-launch.log" || { echo "error: production persistence did not restore" >&2; exit 1; }
echo "PASS: physical Save Manager, browser, stop, gameplay, persistence, and second-launch gates"
