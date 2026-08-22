#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="${1:-$REPO_ROOT/.build/apple-everest/stage25hd/fixtures}"
NAME="CaeruleaHelper-v1.11.1.zip"
EXPECTED="6a0649518d49cd0d17b84da3be53929cdd602d89d922e2d3ab87c524345e3807"
URL="https://gamebanana.com/mmdl/1784884"
DESTINATION="$OUTPUT/$NAME"
TEMPORARY="$OUTPUT/.$NAME.download"

# The ordinary public release DLL inside this exact ZIP is the authoritative
# production input. It remains ignored; no third-party bytes enter Git.
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

echo "PASS: Stage 25H-D source-free direct-ILHook fixture is exact and ignored"
