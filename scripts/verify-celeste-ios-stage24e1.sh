#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
python3 "$SCRIPT_DIR/verify-celeste-ios-stage24e1.py" "$@"
