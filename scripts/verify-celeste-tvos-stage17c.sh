#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"

"$REPO_ROOT/scripts/verify-celeste-tvos-stage17b.sh" "$@"
grep -Fq 'steam-windows-fna-1.4.0.0-manifest-1981411158533599226' \
  "$REPO_ROOT/managed/celeste-input-profiles.json"
grep -Fq 'c83b01cc6ba64af1681ed05e0b8ae8144980eae4195d1691acefbac606ee2068' \
  "$REPO_ROOT/managed/celeste-input-profiles.json"
grep -Fq 'download_depot 504230 504233 5880027853585448535' "$REPO_ROOT/README.md"
if grep -Eqi 'DepotDownloader|opengl branch' "$REPO_ROOT/README.md" "$REPO_ROOT/docs/BUILDING.md"; then
  echo "error: ordinary acquisition guidance must not recommend the diagnostic Steam Windows tooling" >&2
  exit 1
fi
echo "PASS: Stage 17C exact Steam Windows profile, acquisition guidance, and accepted regression chain"
