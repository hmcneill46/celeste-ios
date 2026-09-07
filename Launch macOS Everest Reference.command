#!/bin/zsh
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"
exec "$PROJECT_ROOT/scripts/run-apple-everest-stage25kj-reference.sh" "$@"
