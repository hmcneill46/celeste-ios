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
exec "$RESOURCES_ROOT/Celeste" "$@"
