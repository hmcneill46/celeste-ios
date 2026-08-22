#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="${1:-$REPO_ROOT/.build/apple-everest/stage25hb/fixtures}"

fetch_exact() {
  local name="$1" expected="$2" url="$3"
  local destination="$OUTPUT/$name" temporary="$OUTPUT/.$name.download"
  mkdir -p "$OUTPUT"
  if [[ -f "$destination" ]] && [[ "$(shasum -a 256 "$destination" | awk '{print $1}')" == "$expected" ]]; then
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

fetch_exact "DashToggleHelper-v1.1.0.zip" \
  "677e8fbd067340d7b3133cc908e4ecafc0f5deab2c38b7eeb79a62eb5f61d523" \
  "https://gamebanana.com/mmdl/1460721"
fetch_exact "DisposableTheo-v1.0.6.zip" \
  "df291c0175df46682791fb6373c47eb557c47483eca3db96895eba9b5bbe85b5" \
  "https://gamebanana.com/mmdl/929736"

echo "PASS: Stage 25H-B source-free frozen-IL fixtures are exact and ignored"
