#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
DOTNET8="$REPO_ROOT/.build/apple-everest/toolchain/dotnet8/dotnet"

"$SCRIPT_DIR/bootstrap-apple-everest-host.sh"
(cd /tmp && dotnet restore \
  "$REPO_ROOT/tools/AppleEverestBuilder/tests/AppleEverestBuilder.Tests.csproj" --locked-mode >/dev/null)
(cd /tmp && dotnet build \
  "$REPO_ROOT/tools/AppleEverestBuilder/tests/AppleEverestBuilder.Tests.csproj" \
  --no-restore >/dev/null)
(cd /tmp && "$DOTNET8" \
  "$REPO_ROOT/tools/AppleEverestBuilder/tests/bin/Debug/net8.0/AppleEverestBuilder.Tests.dll" \
  "$REPO_ROOT")
(cd /tmp && dotnet build \
  "$REPO_ROOT/tools/AppleEverestBuilder/tests/HookSemantics/AppleEverestHookSemantics.Tests.csproj" \
  --no-restore >/dev/null)
(cd /tmp && "$DOTNET8" \
  "$REPO_ROOT/tools/AppleEverestBuilder/tests/HookSemantics/bin/Debug/net8.0/AppleEverestHookSemantics.Tests.dll")
exec python3 "$SCRIPT_DIR/verify-apple-everest-stage25kd.py" "$@"
