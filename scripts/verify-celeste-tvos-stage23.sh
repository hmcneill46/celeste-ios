#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="$REPO_ROOT/.build/celeste-runtime/stage23/verification.json"

args=(--repo-root "$REPO_ROOT")
while (($#)); do
  case "$1" in
    --output) OUTPUT="$2"; shift 2 ;;
    --template-root|--generated-root|--native-manifest|--app|--ipa) args+=("$1" "$2"); shift 2 ;;
    --final) args+=("$1"); shift ;;
    -h|--help)
      echo "Usage: scripts/verify-celeste-tvos-stage23.sh [--final] [--template-root DIR] [--generated-root DIR] [--native-manifest FILE] [--app DIR] [--ipa FILE] [--output FILE]"
      exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done

python3 "$REPO_ROOT/scripts/verify-celeste-tvos-stage23.py" \
  "${args[@]}" --output "$OUTPUT"

echo "PASS: Stage 23 current RC3 candidate verification complete"
