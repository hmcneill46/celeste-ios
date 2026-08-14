#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
OUTPUT="$REPO_ROOT/.build/celeste-runtime/stage19/verification.json"

while (($#)); do
  case "$1" in
    --output) OUTPUT="$2"; shift 2 ;;
    -h|--help)
      echo "Usage: scripts/verify-celeste-tvos-stage19.sh [--output FILE]"
      exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done

python3 "$REPO_ROOT/scripts/verify-celeste-tvos-stage19.py" \
  --repo-root "$REPO_ROOT" --output "$OUTPUT"

for project in \
  SaveManagerProtocolTests \
  ControllerPromptTests \
  QuitTests \
  SoftReloadTests \
  SaveManagerPairingTests \
  PerformanceHudTests; do
  dotnet build "$REPO_ROOT/tvos/$project/$project.csproj" -c Release --nologo >/dev/null
done

echo "PASS: Stage 19 semantic production naming verification complete"
