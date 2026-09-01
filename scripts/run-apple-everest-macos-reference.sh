#!/bin/zsh
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
REFERENCE_ROOT="$PROJECT_ROOT/.build/apple-everest/stage25kb/macos-reference"
APP_ROOT="$REFERENCE_ROOT/game/Celeste.app"
RESOURCES_ROOT="$APP_ROOT/Contents/Resources"
GAME_EXECUTABLE="$RESOURCES_ROOT/Celeste"
STATE_ROOT="$PROJECT_ROOT/.build/apple-everest/macos-reference-state"
TMP_ROOT="$PROJECT_ROOT/.build/apple-everest/macos-reference-tmp"

if [[ ! -x "$GAME_EXECUTABLE" ]]; then
  print -u2 "The staged Everest executable is missing: $GAME_EXECUTABLE"
  print -u2 "Run the repository-root 'Launch macOS Everest Reference.command' to rebuild it."
  exit 1
fi

mkdir -p "$STATE_ROOT" "$TMP_ROOT"
cd "$RESOURCES_ROOT"

export EVEREST_SAVEPATH="$STATE_ROOT"
export EVEREST_TMPDIR="$TMP_ROOT"
export EVEREST_LOG_FILENAME="stage25kb-reference.log"
export DYLD_LIBRARY_PATH="$RESOURCES_ROOT/lib64-osx${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}"

exec "$GAME_EXECUTABLE"
