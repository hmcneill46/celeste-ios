#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
DOTNET8="$REPO_ROOT/.build/apple-everest/toolchain/dotnet8/dotnet"

"$SCRIPT_DIR/bootstrap-apple-everest-host.sh"
# The repository root intentionally pins the product SDK (.NET 10). Run the
# pinned host-only .NET 8 toolchain from outside that global.json hierarchy so
# its own SDK is selected deterministically.
(cd /private/tmp && "$DOTNET8" restore "$REPO_ROOT/tools/AppleEverestBuilder/AppleEverestBuilder.csproj" --locked-mode >/dev/null)
(cd /private/tmp && "$DOTNET8" restore "$REPO_ROOT/tools/AppleEverestBuilder/tests/AppleEverestBuilder.Tests.csproj" --locked-mode >/dev/null)
(cd /private/tmp && "$DOTNET8" run --project "$REPO_ROOT/tools/AppleEverestBuilder/tests/AppleEverestBuilder.Tests.csproj" --no-restore -- "$REPO_ROOT")
(cd /private/tmp && "$DOTNET8" run --project "$REPO_ROOT/tools/AppleEverestBuilder/tests/HookSemantics/AppleEverestHookSemantics.Tests.csproj")
"$SCRIPT_DIR/test-apple-everest-il-freeze.sh"
"$SCRIPT_DIR/test-apple-everest-desktop-hookgen.sh"
exec python3 "$SCRIPT_DIR/verify-apple-everest-stage25d.py" "$@"
