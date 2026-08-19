#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
DOTNET8="$REPO_ROOT/.build/apple-everest/toolchain/dotnet8/dotnet"

"$SCRIPT_DIR/bootstrap-apple-everest-host.sh"
(cd "$REPO_ROOT/tools/AppleEverestBuilder" && "$DOTNET8" restore AppleEverestBuilder.csproj --locked-mode >/dev/null)
(cd "$REPO_ROOT/tools/AppleEverestBuilder/tests" && "$DOTNET8" restore AppleEverestBuilder.Tests.csproj --locked-mode >/dev/null)
(cd "$REPO_ROOT/tools/AppleEverestBuilder/tests" && "$DOTNET8" run --project AppleEverestBuilder.Tests.csproj --no-restore -- "$REPO_ROOT")
(cd "$REPO_ROOT/tools/AppleEverestBuilder/tests/HookSemantics" && "$DOTNET8" run --project AppleEverestHookSemantics.Tests.csproj)
"$SCRIPT_DIR/test-apple-everest-il-freeze.sh"
"$SCRIPT_DIR/test-apple-everest-desktop-hookgen.sh"
exec python3 "$SCRIPT_DIR/verify-apple-everest-stage25c.py" "$@"
