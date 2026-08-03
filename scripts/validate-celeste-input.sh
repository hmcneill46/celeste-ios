#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/validate-celeste-input.sh --game-root DIR [options]

Validate the exact supported, unmodified FNA-based Celeste 1.4.0.0 input and
write a privacy-safe manifest. The script reads game files but never modifies
them, never reads saves, and prints no absolute game-library path.

Options:
  --game-root DIR   User-owned Celeste installation (required).
  --output FILE     Manifest destination
                    (default: .build/celeste-managed/input-manifest.json).
  -h, --help        Show this help.

Relative output paths are resolved from the repository root. The source root is
represented as $CELESTE_GAME_ROOT in the manifest.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
GAME_ROOT=""
OUTPUT="$REPO_ROOT/.build/celeste-managed/input-manifest.json"

repo_path() {
  case "$1" in
    /*) printf '%s\n' "$1" ;;
    *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;;
  esac
}

while (($#)); do
  case "$1" in
    --game-root)
      [[ $# -ge 2 ]] || { echo "error: --game-root requires a value" >&2; exit 2; }
      GAME_ROOT="$2"
      shift 2
      ;;
    --output)
      [[ $# -ge 2 ]] || { echo "error: --output requires a value" >&2; exit 2; }
      OUTPUT="$(repo_path "$2")"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "error: unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

[[ -n "$GAME_ROOT" ]] || { echo "error: --game-root is required" >&2; exit 2; }
command -v python3 >/dev/null || { echo "error: python3 is required" >&2; exit 1; }
command -v monodis >/dev/null || { echo "error: monodis is required to validate managed identities" >&2; exit 1; }
command -v file >/dev/null || { echo "error: file is required" >&2; exit 1; }
command -v git >/dev/null || { echo "error: git is required" >&2; exit 1; }

case "$OUTPUT" in
  "$REPO_ROOT"/*) OUTPUT_RELATIVE="${OUTPUT#$REPO_ROOT/}" ;;
  *) echo "error: --output must be inside an ignored repository directory" >&2; exit 1 ;;
esac
git -C "$REPO_ROOT" check-ignore --no-index -q -- "$OUTPUT_RELATIVE" || {
  echo "error: --output must be ignored by Git to protect local game evidence" >&2
  exit 1
}

python3 "$REPO_ROOT/scripts/celeste-managed.py" validate \
  --game-root "$GAME_ROOT" \
  --lock "$REPO_ROOT/managed/celeste-generation.lock.json" \
  --output "$OUTPUT"
