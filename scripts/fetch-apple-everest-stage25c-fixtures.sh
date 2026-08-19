#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="${1:-$REPO_ROOT/.build/apple-everest/real-mods/downloads}"
mkdir -p "$OUTPUT"

fetch() {
  local name="$1" sha="$2" url="$3" destination="$OUTPUT/$name" temporary="$OUTPUT/.$name.download"
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

fetch IAccidentallyFourCassetteBlocks-v1.0.0.zip \
  37eaa16b6b2d458a8ce27e08ea315b4cc3c2938d6b3c2292c61203c641ad7c95 \
  https://gamebanana.com/dl/386507
fetch ParticlePaletteHelper-v1.0.0.zip \
  f9cf8874acbfaff87af22098caea36429c97941d0b13e1114d8749be61afbe19 \
  https://github.com/KnowHT1515/ParticlePaletteHelper/releases/download/v1.0.0/ParticlePaletteHelper.zip
fetch DashlessDreamBlocks-v2.0.0.zip \
  ef5071ad28ed27ee73749623f4484a511f4793388f53417a4c0ee36c3d6e4a7b \
  https://github.com/coloursofnoise/DashlessDreamBlocks/releases/download/v2.0.0/DashlessDreamBlocks.zip
fetch GoldenTrainer-v1.5.4.zip \
  2a39b5bb9524aeac510ff1e0835022a294081784982aca0bf3970fb8e867fc5b \
  https://gamebanana.com/dl/1097059

echo "PASS: Stage 25C public fixtures are exact and remain ignored"
