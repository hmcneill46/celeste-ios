#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="$REPO_ROOT/.build/celeste-runtime/stage17b-verification.json"
STAGE16_ARGS=()

while (($#)); do
  case "$1" in
    --output) OUTPUT="$2"; shift 2 ;;
    --generated-root|--native-manifest|--app|--platform|--ipa)
      STAGE16_ARGS+=("$1" "$2"); shift 2 ;;
    --signed) STAGE16_ARGS+=("$1"); shift ;;
    -h|--help)
      echo "Usage: scripts/verify-celeste-tvos-stage17b.sh [Stage 16B product options] [--output FILE]"
      exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done

python3 "$REPO_ROOT/scripts/verify-celeste-input-profiles.py" \
  --repo-root "$REPO_ROOT" --output "$OUTPUT"
if ((${#STAGE16_ARGS[@]})); then
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage16b.sh" "${STAGE16_ARGS[@]}"
else
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage16b.sh"
fi
python3 "$REPO_ROOT/scripts/verify-repository-stage8b.py"
echo "PASS: Stage 17B explicit multi-distribution FNA profiles and accepted regression chain"
