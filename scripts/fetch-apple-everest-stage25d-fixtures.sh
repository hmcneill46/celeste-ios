#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="${1:-$REPO_ROOT/.build/apple-everest/real-mods/downloads}"

"$SCRIPT_DIR/fetch-apple-everest-stage25c-fixtures.sh" "$OUTPUT"

fetch() {
  local name="$1" sha="$2" url="$3"
  local destination="$OUTPUT/$name" temporary="$OUTPUT/.$name.download"
  if [[ -f "$destination" ]] && [[ "$(shasum -a 256 "$destination" | awk '{print $1}')" == "$sha" ]]; then
    printf 'verified: %s\n' "$name"
    return
  fi
  curl --fail --location --retry 3 --output "$temporary" "$url"
  [[ "$(shasum -a 256 "$temporary" | awk '{print $1}')" == "$sha" ]] || {
    rm -f "$temporary"; echo "error: SHA-256 mismatch for $name" >&2; exit 1; }
  mv "$temporary" "$destination"
  printf 'downloaded and verified: %s\n' "$name"
}

fetch FeatherMaddy-v1.3.zip \
  a8f1104710aac5807be3b24cd8c3870d94aa117d1146b30a4de0983a10f3e40e \
  https://gamebanana.com/dl/1066794
fetch LagPauser-v1.3.0.zip \
  32dac84d2c5b60458a701cb61e8601bc89d937e25bc7fdcf52c80d9128e99d10 \
  https://gamebanana.com/dl/1458113

echo "PASS: Stage 25D public fixtures are exact and remain ignored"
