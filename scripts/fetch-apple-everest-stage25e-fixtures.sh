#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="${1:-$REPO_ROOT/.build/apple-everest/helper-ecosystem/selected-fixtures}"
mkdir -p "$OUTPUT"

fetch() {
  local name="$1" sha="$2" url="$3"
  local destination="$OUTPUT/$name" temporary="$OUTPUT/.$name.download"
  if [[ -f "$destination" ]] && [[ "$(shasum -a 256 "$destination" | awk '{print $1}')" == "$sha" ]]; then
    printf 'verified: %s\n' "$name"
    return
  fi
  curl --fail --location --retry 3 --output "$temporary" "$url"
  if [[ "$(shasum -a 256 "$temporary" | awk '{print $1}')" != "$sha" ]]; then
    echo "error: SHA-256 mismatch for $name; rejected download retained at $temporary" >&2
    exit 1
  fi
  mv "$temporary" "$destination"
  printf 'downloaded and verified: %s\n' "$name"
}

# Exact ordinary release ZIP closure: four accepted Stage 25C/25D modules,
# the Stage 25E helper, and a real map that declares the helper dependency.
fetch IAccidentallyFourCassetteBlocks-v1.0.0.zip \
  37eaa16b6b2d458a8ce27e08ea315b4cc3c2938d6b3c2292c61203c641ad7c95 \
  https://gamebanana.com/dl/386507
fetch ParticlePaletteHelper-v1.0.0.zip \
  f9cf8874acbfaff87af22098caea36429c97941d0b13e1114d8749be61afbe19 \
  https://github.com/KnowHT1515/ParticlePaletteHelper/releases/download/v1.0.0/ParticlePaletteHelper.zip
fetch FeatherMaddy-v1.3.zip \
  a8f1104710aac5807be3b24cd8c3870d94aa117d1146b30a4de0983a10f3e40e \
  https://gamebanana.com/dl/1066794
fetch LagPauser-v1.3.0.zip \
  32dac84d2c5b60458a701cb61e8601bc89d937e25bc7fdcf52c80d9128e99d10 \
  https://gamebanana.com/dl/1458113
fetch CpopHelper-1.3.0.zip \
  7a807a8f9ce6ccb4d6ad0c664bb7791a60734533fb33cfcd6beccb202d846b63 \
  https://gamebanana.com/mmdl/1035472
fetch QuizSample-0.0.1.zip \
  5cb8351bb263aa316831d2b683cb8b04acd270587edfe7c9e3df3bb8e6b83b3e \
  https://gamebanana.com/mmdl/964770

echo "PASS: Stage 25E source-free helper ecosystem fixtures are exact and ignored"
