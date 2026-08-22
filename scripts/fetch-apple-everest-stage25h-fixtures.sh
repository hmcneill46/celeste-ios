#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="${1:-$REPO_ROOT/.build/apple-everest/stage25h/fixtures/dashtoggle}"
NAME="DashToggleHelper-v1.1.0.zip"
EXPECTED="677e8fbd067340d7b3133cc908e4ecafc0f5deab2c38b7eeb79a62eb5f61d523"
URL="https://gamebanana.com/mmdl/1460721"
DESTINATION="$OUTPUT/$NAME"
TEMPORARY="$OUTPUT/.$NAME.download"

# This exact ordinary public release ZIP is the authoritative Stage 25H input.
# It remains under ignored storage; no third-party source or binary enters Git.
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

echo "PASS: Stage 25H source-free frozen-IL fixture is exact and ignored"
