#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="${1:-$REPO_ROOT/.build/apple-everest/modinterop/selected-fixtures}"

# Exact ordinary release archives selected for the Stage 25F-B binary-first
# ModInterop audit. The archives stay under ignored storage; no third-party
# binary or source bytes enter Git.
fetch() {
  local name="$1"
  local expected="$2"
  local url="$3"
  local destination="$OUTPUT/$name"
  local temporary="$OUTPUT/.$name.download"

  if [[ -f "$destination" ]] &&
     [[ "$(shasum -a 256 "$destination" | awk '{print $1}')" == "$expected" ]]; then
    printf 'verified: %s\n' "$name"
    return
  fi

  mkdir -p "$OUTPUT"
  curl --fail --location --retry 3 --output "$temporary" "$url"
  local actual
  actual="$(shasum -a 256 "$temporary" | awk '{print $1}')"
  if [[ "$actual" != "$expected" ]]; then
    echo "error: SHA-256 mismatch for $name; rejected download retained at $temporary" >&2
    exit 1
  fi
  mv "$temporary" "$destination"
  printf 'downloaded and verified: %s\n' "$name"
}

fetch \
  "ConditionHelper-v1.0.0.zip" \
  "cefd1f8264d4eba9ccd8abb324f88764b99c1fda7812cb6cbbb84cef05c6e8ed" \
  "https://gamebanana.com/mmdl/1081578"

fetch \
  "AchievementHelper-v1.0.5.zip" \
  "155b2ff92857e92f2c4510270e2c6d391b40510945fe3c6c31f9d8fa43a58177" \
  "https://gamebanana.com/mmdl/1081965"

echo "PASS: Stage 25F-B source-free ModInterop fixtures are exact and ignored"
