#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="${1:-$REPO_ROOT/.build/apple-everest/stage25kb/public-packages}"

"$SCRIPT_DIR/fetch-apple-everest-stage25ka-fixtures.sh" "$OUTPUT"

fetch_exact() {
  local name="$1" expected="$2" url="$3" destination temporary actual
  destination="$OUTPUT/$name"
  temporary="$OUTPUT/.$name.download"
  if [[ -f "$destination" ]] &&
     [[ "$(shasum -a 256 "$destination" | awk '{print $1}')" == "$expected" ]]; then
    printf 'verified: %s\n' "$name"
    return
  fi
  curl --fail --location --retry 3 --output "$temporary" "$url"
  actual="$(shasum -a 256 "$temporary" | awk '{print $1}')"
  if [[ "$actual" != "$expected" ]]; then
    echo "error: SHA-256 mismatch for $name; rejected download retained at $temporary" >&2
    exit 1
  fi
  mv "$temporary" "$destination"
  printf 'downloaded and verified: %s\n' "$name"
}

fetch_exact "EeveeHelper.zip" "de8eb463083e7298827d6c1ba4f5a4b69cc6cdd9b4be31fec78e272fce3a3839" "https://gamebanana.com/mmdl/1657000"
fetch_exact "Kayonara Collection.zip" "a6c8a1d001926a9167c08dde3d221d72d5deea1c577fd329fb386ae74cd9d7fc" "https://gamebanana.com/mmdl/1086296"
fetch_exact "XaphanHelper.zip" "ca868d06eb05f0c5de55126090e5019210e9d83c3dae9394cbcd155acf8eb16f" "https://gamebanana.com/mmdl/1716933"

echo "PASS: Stage 25K-B complete public ordinary package graph is exact and ignored"
