#!/bin/zsh
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
REFERENCE_ROOT="$PROJECT_ROOT/.build/apple-everest/stage25kj/macos-reference"
RESOURCES_ROOT="$REFERENCE_ROOT/game/Celeste.app/Contents/Resources"
[[ -f "$REFERENCE_ROOT/.stage25kj-owned-reference" && -f "$REFERENCE_ROOT/reference-evidence.json" && -x "$RESOURCES_ROOT/Celeste" ]] || {
  print -u2 'Prepare the authored Stage 25K-J reference first.'; exit 1;
}
export EVEREST_SAVEPATH="$REFERENCE_ROOT/state"
export EVEREST_TMPDIR="$REFERENCE_ROOT/tmp"
export EVEREST_LOG_FILENAME="stage25kj-reference.log"
export DYLD_LIBRARY_PATH="$RESOURCES_ROOT/lib64-osx${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}"
cd "$RESOURCES_ROOT"
exec python3 - "$RESOURCES_ROOT/Celeste" "$@" <<'PY'
import fcntl
import os
import subprocess
import sys

executable = sys.argv[1]
lock = open(os.path.join(os.environ['EVEREST_TMPDIR'], 'launcher.lock'), 'w')
try:
    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
except BlockingIOError:
    print('The Stage 25K-J reference is already running.')
    raise SystemExit(0)
for line in subprocess.check_output(['ps', '-axo', 'stat=,args='], text=True).splitlines():
    parts = line.strip().split(None, 1)
    if len(parts) == 2 and 'E' not in parts[0] and parts[1].split(' --', 1)[0] == executable:
        print('The Stage 25K-J reference is already running.')
        raise SystemExit(0)
raise SystemExit(subprocess.call([executable, *(sys.argv[2:] or ['--debug'])]))
PY
