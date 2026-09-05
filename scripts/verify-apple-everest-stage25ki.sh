#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
"$SCRIPT_DIR/verify-apple-everest-stage25kf.sh" --repo-root "$ROOT"
exec python3 "$SCRIPT_DIR/verify-apple-everest-stage25ki.py" "$@"
