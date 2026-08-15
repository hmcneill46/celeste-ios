#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
BUILD_DIR="$REPO_ROOT/.build/ios-native"
OUTPUT_DIR="$REPO_ROOT/artifacts/ios-native"
COMPARE=""

while (($#)); do
  case "$1" in
    --build-dir) [[ $# -ge 2 ]] || exit 2; BUILD_DIR="$2"; shift 2 ;;
    --output-dir) [[ $# -ge 2 ]] || exit 2; OUTPUT_DIR="$2"; shift 2 ;;
    --compare-manifest) [[ $# -ge 2 ]] || exit 2; COMPARE="$2"; shift 2 ;;
    -h|--help) echo "Usage: scripts/verify-ios-native.sh [--build-dir DIR] [--output-dir DIR] [--compare-manifest FILE]"; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done

args=(--repo-root "$REPO_ROOT" --build-dir "$BUILD_DIR" --output-dir "$OUTPUT_DIR" --deployment-target 15.0)
[[ -z "$COMPARE" ]] || args+=(--compare-manifest "$COMPARE")
python3 "$SCRIPT_DIR/verify-ios-native.py" "${args[@]}"
