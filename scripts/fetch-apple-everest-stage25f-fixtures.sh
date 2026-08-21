#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="${1:-$REPO_ROOT/.build/apple-everest/module-durability/selected-fixtures}"

# Retain the exact accepted Stage 25E helper ecosystem and add the one real
# ordinary release ZIP selected for Stage 25F-A module durability.  The
# archives remain user-fetched ignored inputs; no game or mod bytes enter Git.
"$SCRIPT_DIR/fetch-apple-everest-stage25e-fixtures.sh" "$OUTPUT"

name="DeathMarkers-v2.0.0.zip"
expected="94ad7d14fec6fb500f811ef09f46f008f444b45aafcdd8c2e8b86ce5d3ee6fc7"
destination="$OUTPUT/$name"
temporary="$OUTPUT/.$name.download"

if [[ -f "$destination" ]] &&
   [[ "$(shasum -a 256 "$destination" | awk '{print $1}')" == "$expected" ]]; then
  printf 'verified: %s\n' "$name"
else
  mkdir -p "$OUTPUT"
  curl --fail --location --retry 3 --output "$temporary" \
    https://gamebanana.com/mmdl/1543349
  actual="$(shasum -a 256 "$temporary" | awk '{print $1}')"
  if [[ "$actual" != "$expected" ]]; then
    echo "error: SHA-256 mismatch for $name; rejected download retained at $temporary" >&2
    exit 1
  fi
  mv "$temporary" "$destination"
  printf 'downloaded and verified: %s\n' "$name"
fi

echo "PASS: Stage 25F-A source-free module-durability fixtures are exact and ignored"
