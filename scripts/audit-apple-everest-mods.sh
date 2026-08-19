#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
DOTNET8="$REPO_ROOT/.build/apple-everest/toolchain/dotnet8/dotnet"
output=""
mods=()

while (($#)); do
  case "$1" in
    --output) output="$2"; shift 2 ;;
    --mod) mods+=("$(python3 -c 'import pathlib,sys; print(pathlib.Path(sys.argv[1]).resolve())' "$2")"); shift 2 ;;
    -h|--help)
      echo "Usage: scripts/audit-apple-everest-mods.sh --output REPORT.json --mod MOD.zip [--mod MOD_DIR ...]"
      exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done

[[ -n "$output" && ${#mods[@]} -gt 0 ]] || { echo "error: --output and at least one --mod are required" >&2; exit 2; }
output="$(python3 -c 'import pathlib,sys; print(pathlib.Path(sys.argv[1]).resolve())' "$output")"
"$SCRIPT_DIR/bootstrap-apple-everest-host.sh" >/dev/null
args=()
for mod in "${mods[@]}"; do args+=(--mod "$mod"); done
(cd /private/tmp && "$DOTNET8" run --project "$REPO_ROOT/tools/AppleEverestBuilder/AppleEverestBuilder.csproj" --no-restore -- \
  audit "${args[@]}" --output "$output")
