#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
DOTNET8="$REPO_ROOT/.build/apple-everest/toolchain/dotnet8/dotnet"

"$SCRIPT_DIR/bootstrap-apple-everest-host.sh"
(cd /private/tmp && "$DOTNET8" restore \
  "$REPO_ROOT/tools/AppleEverestBuilder/tests/AppleEverestBuilder.Tests.csproj" --locked-mode >/dev/null)
(cd /private/tmp && "$DOTNET8" run --project \
  "$REPO_ROOT/tools/AppleEverestBuilder/tests/AppleEverestBuilder.Tests.csproj" \
  --no-restore -- "$REPO_ROOT")
exec python3 "$SCRIPT_DIR/verify-apple-everest-stage25jb.py" "$@"
