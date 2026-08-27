#!/bin/zsh
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"
REFERENCE_LAUNCHER="$PROJECT_ROOT/.build/apple-everest/stage25ka/desktop-reference-sdk9/Launch Celeste Everest Reference.command"

if [[ ! -x "$REFERENCE_LAUNCHER" ]]; then
  print -u2 "The staged macOS Everest reference is not currently available."
  print -u2 "Expected launcher: $REFERENCE_LAUNCHER"
  print -u2 "The reference may need to be regenerated if the .build folder was cleaned."
  print -u2 ""
  read -k 1 "?Press any key to close."
  print
  exit 1
fi

exec "$REFERENCE_LAUNCHER"
