#!/bin/zsh
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"
PREPARE_REFERENCE="$PROJECT_ROOT/scripts/prepare-apple-everest-macos-reference.sh"
RUN_REFERENCE="$PROJECT_ROOT/scripts/run-apple-everest-macos-reference.sh"

"$PREPARE_REFERENCE"

exec "$RUN_REFERENCE"
