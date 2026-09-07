#!/bin/zsh
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
REFERENCE_ROOT="$PROJECT_ROOT/.build/apple-everest/stage25kl/macos-reference"
RESOURCES_ROOT="$REFERENCE_ROOT/game/Celeste.app/Contents/Resources"
[[ -f "$REFERENCE_ROOT/.stage25kl-owned-reference" && -f "$REFERENCE_ROOT/reference-evidence.json" && -x "$RESOURCES_ROOT/Celeste" ]] || {
  print -u2 'Prepare the Stage 25K-L original reference first.'; exit 1;
}
mkdir -p "$REFERENCE_ROOT/state" "$REFERENCE_ROOT/tmp"
export EVEREST_SAVEPATH="$REFERENCE_ROOT/state"
export EVEREST_TMPDIR="$REFERENCE_ROOT/tmp"
export EVEREST_LOG_FILENAME="stage25kl-reference.log"
export DYLD_LIBRARY_PATH="$RESOURCES_ROOT/lib64-osx${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}"
cd "$RESOURCES_ROOT"
exec python3 - "$RESOURCES_ROOT/Celeste" "$@" <<'PY'
import fcntl,os,subprocess,sys
lock=open(os.path.join(os.environ['EVEREST_TMPDIR'],'launcher.lock'),'w')
try:fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
except BlockingIOError:
    print('The Stage 25K-L reference is already running.');raise SystemExit(0)
raise SystemExit(subprocess.call([sys.argv[1],*(sys.argv[2:] or ['--debug'])]))
PY
