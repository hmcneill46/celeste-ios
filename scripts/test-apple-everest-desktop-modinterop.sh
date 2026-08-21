#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
DOTNET8="$REPO_ROOT/.build/apple-everest/toolchain/dotnet8/dotnet"
DOTNET9="$REPO_ROOT/.build/apple-everest/toolchain/dotnet9/dotnet"
UPSTREAM="$REPO_ROOT/.build/apple-everest/upstream/Everest"
MONOMOD="$UPSTREAM/external/MonoMod"
UTILS_PROJECT="$MONOMOD/src/MonoMod.Utils/MonoMod.Utils.csproj"
UTILS_OUT="$MONOMOD/artifacts/bin/MonoMod.Utils/release_net8.0"
TEST_ROOT="$REPO_ROOT/apple-everest/tests/desktop-modinterop"

"$SCRIPT_DIR/bootstrap-apple-everest-host.sh"
(cd /private/tmp && "$DOTNET8" run --project "$REPO_ROOT/tools/AppleEverestBuilder/AppleEverestBuilder.csproj" --no-restore -- acquire \
  --profile "$REPO_ROOT/apple-everest/profiles/stable-1.6458.0.json" --output "$UPSTREAM")

(cd "$MONOMOD" && \
  "$DOTNET9" restore src/MonoMod.SourceGen.Internal/MonoMod.SourceGen.Internal.csproj >/dev/null && \
  "$DOTNET9" restore "$UTILS_PROJECT" -p:TargetFrameworks=net8.0 -p:RestoreRecursive=false >/dev/null && \
  "$DOTNET9" build "$UTILS_PROJECT" -c Release -f net8.0 --no-restore -p:TargetFrameworks=net8.0 >/dev/null)

[[ -f "$UTILS_OUT/MonoMod.Utils.dll" ]] || { echo "error: pinned MonoMod.Utils build missing" >&2; exit 1; }
(cd /private/tmp && "$DOTNET8" run --project "$TEST_ROOT/Reference/Reference.csproj" -c Release \
  -p:AppleEverestMonoModUtilsRoot="$UTILS_OUT")
