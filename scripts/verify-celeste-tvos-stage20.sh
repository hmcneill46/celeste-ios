#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="$REPO_ROOT/.build/celeste-runtime/stage20/verification.json"

while (($#)); do
  case "$1" in
    --output) OUTPUT="$2"; shift 2 ;;
    -h|--help)
      echo "Usage: scripts/verify-celeste-tvos-stage20.sh [--output FILE]"
      exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done

python3 "$REPO_ROOT/scripts/verify-celeste-tvos-stage20.py" \
  --repo-root "$REPO_ROOT" --output "$OUTPUT"

echo "PASS: Stage 20 current RC2 candidate verification complete"
