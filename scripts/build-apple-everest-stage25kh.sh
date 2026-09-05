#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
FIXTURE="$ROOT/.build/apple-everest/stage25kc/packages"
MAX="$ROOT/.build/apple-everest/stage25kh/reacquired/MaxHelpingHand-1.40.9.zip"
CHRONO="$ROOT/.build/apple-everest/stage25kg/prior-packages/ChronoHelper.zip"

[[ -f "$MAX" ]] || { echo "error: reacquire exact MaxHelpingHand 1.40.9 first" >&2; exit 1; }
[[ "$(shasum -a 256 "$MAX" | awk '{print $1}')" == "abfc5d167936410e9b6665018335ee654455e30710caffe01540b232da468fee" ]] || {
  echo "error: MaxHelpingHand archive identity mismatch" >&2; exit 1; }

args=(--factory-closure "$ROOT/apple-everest/canaries/stage25kh/factory-closure.json")
for package in ContortHelper ExtendedVariantMode JungleHelper YetAnotherHelper StrawberryJam2021 \
  CollabUtils2 LunaticHelper CrystallineHelper VortexHelper StrawberryJam2021AudioA StrawberryJam2021AudioB; do
  args+=(--mod "$FIXTURE/$package.zip")
done
args+=(--mod "$CHRONO" --mod "$MAX"
  --mod "$ROOT/apple-everest/canaries/stage25ke"
  --mod "$ROOT/apple-everest/canaries/stage25kf"
  --mod "$ROOT/apple-everest/canaries/stage25kh")

exec "$SCRIPT_DIR/build-apple-everest-canary.sh" "${args[@]}" "$@"
