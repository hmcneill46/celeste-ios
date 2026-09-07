#!/bin/zsh
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"
if [[ -f "$PROJECT_ROOT/.build/apple-everest/stage25kl/macos-reference/reference-evidence.json" ]]; then
  exec "$PROJECT_ROOT/scripts/run-apple-everest-stage25kl-reference.sh" "$@"
fi
exec "$PROJECT_ROOT/scripts/run-apple-everest-stage25kj-reference.sh" "$@"
