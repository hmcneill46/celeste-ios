#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/validate-fmod-tvos-sdk.sh --sdk-root DIR [options]

Validate the exact user-supplied FMOD Engine 1.10.09 iOS/tvOS SDK without
modifying it. The script verifies release/build/header identity, every member
of both arm64 tvOS static archives, deployment compatibility, and the public
symbols required by Stage 5A. It installs nothing.

Options:
  --sdk-root DIR    FMOD SDK root containing doc/ and api/ (required).
  --output FILE     Privacy-safe ignored manifest
                    (default: .build/fmod-tvos/sdk-manifest.json).
  -h, --help        Show this help.

The manifest represents the source as $FMOD_SDK_ROOT and never records the
mounted volume's absolute path. Validation affects output only; it does not
modify SDK files, system audio configuration, signing, or devices.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
SDK_ROOT=""
OUTPUT="$REPO_ROOT/.build/fmod-tvos/sdk-manifest.json"

repo_path() {
  case "$1" in
    /*) printf '%s\n' "$1" ;;
    *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;;
  esac
}

while (($#)); do
  case "$1" in
    --sdk-root)
      [[ $# -ge 2 ]] || { echo "error: --sdk-root requires a value" >&2; exit 2; }
      SDK_ROOT="$2"
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

[[ -n "$SDK_ROOT" ]] || { echo "error: --sdk-root is required" >&2; exit 2; }
for required_tool in python3 xcrun nm; do
  command -v "$required_tool" >/dev/null || {
    echo "error: required existing tool is unavailable: $required_tool" >&2
    exit 1
  }
done

case "$OUTPUT" in
  "$REPO_ROOT/.build/fmod-tvos/"*|"$REPO_ROOT/artifacts/fmod-tvos/"*) ;;
  *) echo "error: --output must remain in an ignored Stage 5 directory" >&2; exit 2 ;;
esac

python3 "$REPO_ROOT/scripts/fmod-tvos.py" validate-sdk \
  --sdk-root "$SDK_ROOT" \
  --output "$OUTPUT"

echo "PASS: FMOD Engine 1.10.09 build 97915; arm64 TVOS static archives; minimum tvOS compatible with 16.0"
echo 'privacy-safe manifest: $REPO_ROOT/.build/fmod-tvos/ (selected output)'
