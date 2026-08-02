#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="$REPO_ROOT/.build/tvos-native"
OUTPUT_DIR="$REPO_ROOT/artifacts/tvos-native"
DEPLOYMENT_TARGET="${TVOS_DEPLOYMENT_TARGET:-16.0}"
COMPARE_MANIFEST=""

usage() {
  cat <<'EOF'
Usage: scripts/verify-tvos-native.sh [options]

Verify every archive member, XCFramework metadata, architecture, platform,
minimum OS, exported symbol, tvStubs overlap, license notice, and native link
probe in a completed Stage 1 artifact set.

Options:
  --build-dir DIR          Generated build directory
                           (default: .build/tvos-native)
  --output-dir DIR         XCFramework directory
                           (default: artifacts/tvos-native)
  --compare-manifest FILE  Require normalized output to equal FILE
  -h, --help               Show this help

Environment:
  TVOS_DEPLOYMENT_TARGET   Common minimum tvOS version (default: 16.0)

Verification is read-only with respect to sources and system configuration.
It writes reports, link probes, and license copies only below --output-dir.
EOF
}

while (($#)); do
  case "$1" in
    --build-dir)
      (($# >= 2)) || { echo "error: --build-dir requires a value" >&2; exit 2; }
      BUILD_DIR="$2"; shift 2 ;;
    --output-dir)
      (($# >= 2)) || { echo "error: --output-dir requires a value" >&2; exit 2; }
      OUTPUT_DIR="$2"; shift 2 ;;
    --compare-manifest)
      (($# >= 2)) || { echo "error: --compare-manifest requires a value" >&2; exit 2; }
      COMPARE_MANIFEST="$2"; shift 2 ;;
    -h|--help)
      usage; exit 0 ;;
    *)
      echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

for tool in python3 xcrun; do
  command -v "$tool" >/dev/null 2>&1 || { echo "error: required tool not found: $tool" >&2; exit 1; }
done

BUILD_DIR="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$BUILD_DIR")"
OUTPUT_DIR="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$OUTPUT_DIR")"

command=(
  python3 "$SCRIPT_DIR/verify-tvos-artifacts.py"
  --repo-root "$REPO_ROOT"
  --build-dir "$BUILD_DIR"
  --output-dir "$OUTPUT_DIR"
  --deployment-target "$DEPLOYMENT_TARGET"
)
if [[ -n "$COMPARE_MANIFEST" ]]; then
  command+=(--compare-manifest "$COMPARE_MANIFEST")
fi

printf 'Running:'
printf ' %q' "${command[@]}"
printf '\n'
"${command[@]}"
