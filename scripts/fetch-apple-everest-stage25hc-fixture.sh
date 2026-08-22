#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="${1:-$REPO_ROOT/.build/apple-everest/stage25hc/fixtures}"
NAME="VortexHelper-v1.2.19.zip"
EXPECTED="b6280fe2e3c05d355c32a51049854137c41be72d24ec4aec9d997cc7c4394db2"
URL="https://gamebanana.com/mmdl/1368600"
DESTINATION="$OUTPUT/$NAME"
TEMPORARY="$OUTPUT/.$NAME.download"

mkdir -p "$OUTPUT"
if [[ -f "$DESTINATION" ]] &&
   [[ "$(shasum -a 256 "$DESTINATION" | awk '{print $1}')" == "$EXPECTED" ]]; then
  printf 'verified: %s\n' "$NAME"
else
  curl --fail --location --retry 3 --output "$TEMPORARY" "$URL"
  actual="$(shasum -a 256 "$TEMPORARY" | awk '{print $1}')"
  if [[ "$actual" != "$EXPECTED" ]]; then
    echo "error: SHA-256 mismatch for $NAME; rejected download retained at $TEMPORARY" >&2
    exit 1
  fi
  mv "$TEMPORARY" "$DESTINATION"
  printf 'downloaded and verified: %s\n' "$NAME"
fi

echo "PASS: Stage 25H-C source-free VortexHelper fixture is exact and ignored"
