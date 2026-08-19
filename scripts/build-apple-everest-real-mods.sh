#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
MODS_DIR=""
PLATFORM="all"
FORWARD=()

usage() {
  cat <<'EOF'
Usage: scripts/build-apple-everest-real-mods.sh --mods DIRECTORY [options]

Internal experimental builder for an explicit directory of ordinary Everest
ZIPs. Every ZIP is still subjected to the closed Apple static-AOT analyser.

Options:
  --mods DIRECTORY         required, non-recursive directory of .zip files
  --platform ios|tvos|all  default all
  --signing MODE           unsigned or development
  --team-id ID
  --ios-bundle-id ID
  --tvos-bundle-id ID
  --ios-device-id ID
  --tvos-device-id ID
  --prepare-only
  --clean
  -h, --help
EOF
}

while (($#)); do
  case "$1" in
    --mods) MODS_DIR="$2"; shift 2 ;;
    --platform) PLATFORM="$2"; shift 2 ;;
    --signing|--team-id|--ios-bundle-id|--tvos-bundle-id|--ios-device-id|--tvos-device-id)
      FORWARD+=("$1" "$2"); shift 2 ;;
    --prepare-only|--clean) FORWARD+=("$1"); shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done

[[ -n "$MODS_DIR" && -d "$MODS_DIR" ]] || { echo "error: --mods must name a directory" >&2; exit 2; }
[[ "$PLATFORM" == ios || "$PLATFORM" == tvos || "$PLATFORM" == all ]] || {
  echo "error: invalid --platform" >&2; exit 2; }

MOD_ARGS=()
while IFS= read -r -d '' archive; do MOD_ARGS+=(--mod "$archive"); done < <(
  python3 - "$MODS_DIR" <<'PY'
import pathlib, sys
root = pathlib.Path(sys.argv[1]).resolve()
for path in sorted((item.resolve() for item in root.iterdir() if item.is_file() and item.suffix.lower() == ".zip"), key=str):
    print(path, end="\0")
PY
)
((${#MOD_ARGS[@]} > 0)) || { echo "error: no .zip files found in --mods directory" >&2; exit 2; }

exec "$SCRIPT_DIR/build-apple-everest-canary.sh" --platform "$PLATFORM" "${FORWARD[@]}" "${MOD_ARGS[@]}"
