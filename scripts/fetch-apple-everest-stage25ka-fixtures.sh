#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="${1:-$REPO_ROOT/.build/apple-everest/stage25ka/public-packages}"

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

fetch_exact "ChronoHelper.zip" "af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18" "https://gamebanana.com/mmdl/1778580"
fetch_exact "DJMapHelper.zip" "95ab02d657213031b70c3079738b21be399e567ca8a32b1fe8e8effc0778eedb" "https://gamebanana.com/mmdl/1036311"
fetch_exact "LittleEpic.zip" "ca57d295f446affdd8eb9b56ffe58f98cc652e9ec242ff07b224877d0067b384" "https://gamebanana.com/mmdl/1243114"
fetch_exact "FearoftheDark.zip" "7071c67f93a29c0f25c87c366762437bbf60873e135c5ca34009c197f5b40e5b" "https://gamebanana.com/mmdl/1057184"
fetch_exact "Torremolinos-Speedbuild.zip" "90e4918cfe24a2075b52853d02614cf97c9ecd8c552c3da6f1360d8c5e3fcbec" "https://gamebanana.com/mmdl/542952"
fetch_exact "Hennyburgr-Comp-1.1.0.zip" "638ad7beac7a24600c7c0733acf12a645795bd8878fd5bc7c88e6e24b17dce39" "https://gamebanana.com/mmdl/1144148"
fetch_exact "CollabUtils2-1.13.4.zip" "4bcea8a9011edb8b7d27b433c47f1f4a871b8f7a4dd7f2bffac67bb99c7f6ad5" "https://gamebanana.com/mmdl/1721882"
fetch_exact "CommunalHelper.zip" "44f4fb0b277a4900fd2a555e1a73e661140aa7b2455d3c7776cf420193349e3c" "https://gamebanana.com/mmdl/1775162"
fetch_exact "MaxHelpingHand.zip" "abfc5d167936410e9b6665018335ee654455e30710caffe01540b232da468fee" "https://gamebanana.com/mmdl/1778093"
fetch_exact "LunaticHelper.zip" "b10e044b1dfa412605bee3ba6bfdd4263591099d331d6c1aab1e9ba65bf6a3e5" "https://gamebanana.com/mmdl/466257"
fetch_exact "ShroomHelper.zip" "6a2c3eacc68353c0f8bb69bb9b59ca62128e6d3f54ff38a8ff1f282f0cdd97d7" "https://gamebanana.com/mmdl/1361856"
fetch_exact "FrostHelper.zip" "4dc648dcdb3a86936b83af6f30aa80ee1f4740fd9e7808aee01cb7045a683434" "https://gamebanana.com/mmdl/1774878"
fetch_exact "FancyTileEntities.zip" "c37806dc7e7db4a392c8ab79030701f4cf2046109cc19435b607fca7fe41c8d8" "https://gamebanana.com/mmdl/1569312"

echo "PASS: Stage 25K-A complete public ordinary package graph is exact and ignored"
