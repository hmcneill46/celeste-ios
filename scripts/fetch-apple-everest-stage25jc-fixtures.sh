#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="${1:-$REPO_ROOT/.build/apple-everest/stage25jc/public-packages}"

mkdir -p "$OUTPUT"

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

fetch_exact "ChronoHelper.zip" \
  "af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18" \
  "https://gamebanana.com/mmdl/1778580"
fetch_exact "DJMapHelper.zip" \
  "95ab02d657213031b70c3079738b21be399e567ca8a32b1fe8e8effc0778eedb" \
  "https://gamebanana.com/mmdl/1036311"
fetch_exact "LittleEpic.zip" \
  "ca57d295f446affdd8eb9b56ffe58f98cc652e9ec242ff07b224877d0067b384" \
  "https://gamebanana.com/mmdl/1243114"
fetch_exact "FearoftheDark.zip" \
  "7071c67f93a29c0f25c87c366762437bbf60873e135c5ca34009c197f5b40e5b" \
  "https://gamebanana.com/mmdl/1057184"
fetch_exact "Torremolinos-Speedbuild.zip" \
  "90e4918cfe24a2075b52853d02614cf97c9ecd8c552c3da6f1360d8c5e3fcbec" \
  "https://gamebanana.com/mmdl/542952"

echo "PASS: Stage 25J-C public ordinary package graph is exact and ignored"
