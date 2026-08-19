#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
DOTNET8="$REPO_ROOT/.build/apple-everest/toolchain/dotnet8/dotnet"
DOTNET9="$REPO_ROOT/.build/apple-everest/toolchain/dotnet9/dotnet"
UPSTREAM="$REPO_ROOT/.build/apple-everest/upstream/Everest"
MONOMOD="$UPSTREAM/external/MonoMod"
HOOKGEN_PROJECT="$MONOMOD/src/MonoMod.RuntimeDetour.HookGen/MonoMod.RuntimeDetour.HookGen.csproj"
SOURCEGEN_PROJECT="$MONOMOD/src/MonoMod.SourceGen.Internal/MonoMod.SourceGen.Internal.csproj"
ICED_PROJECT="$MONOMOD/external/iced.csproj"
HOOKGEN_OUT="$MONOMOD/artifacts/bin/MonoMod.RuntimeDetour.HookGen/release_net8.0"
TEST_ROOT="$REPO_ROOT/apple-everest/tests/desktop-hookgen"
WORK="$REPO_ROOT/.build/apple-everest/desktop-hookgen"

"$SCRIPT_DIR/bootstrap-apple-everest-host.sh"
(cd /private/tmp && "$DOTNET8" run --project "$REPO_ROOT/tools/AppleEverestBuilder/AppleEverestBuilder.csproj" --no-restore -- acquire \
  --profile "$REPO_ROOT/apple-everest/profiles/stable-1.6458.0.json" --output "$UPSTREAM")

# The historical MonoMod project is multi-targeted. Restore only the supported
# host target while retaining its netstandard source generator dependency.
(cd "$MONOMOD" && \
  "$DOTNET9" restore "$SOURCEGEN_PROJECT" >/dev/null && \
  "$DOTNET9" restore "$HOOKGEN_PROJECT" -p:TargetFrameworks=net8.0 -p:RestoreRecursive=false >/dev/null && \
  "$DOTNET9" restore src/MonoMod.Utils/MonoMod.Utils.csproj -p:TargetFrameworks=net8.0 -p:RestoreRecursive=false >/dev/null && \
  "$DOTNET9" restore src/MonoMod.Core/MonoMod.Core.csproj -p:TargetFrameworks=net8.0 -p:RestoreRecursive=false >/dev/null && \
  "$DOTNET9" restore src/MonoMod.Patcher/MonoMod.Patcher.csproj -p:TargetFrameworks=net8.0 -p:RestoreRecursive=false >/dev/null && \
  "$DOTNET9" restore src/MonoMod.RuntimeDetour/MonoMod.RuntimeDetour.csproj -p:TargetFrameworks=net8.0 -p:RestoreRecursive=false >/dev/null && \
  "$DOTNET9" restore "$ICED_PROJECT" -p:TargetFrameworks=net8.0 >/dev/null && \
  "$DOTNET9" restore "$SOURCEGEN_PROJECT" >/dev/null && \
  "$DOTNET9" build "$HOOKGEN_PROJECT" -c Release -f net8.0 --no-restore -p:TargetFrameworks=net8.0 >/dev/null)

[[ -f "$HOOKGEN_OUT/MonoMod.RuntimeDetour.HookGen.dll" ]] || { echo "error: pinned HookGen build missing" >&2; exit 1; }
if [[ -d "$WORK" ]]; then find "$WORK" -depth -delete; fi
mkdir -p "$WORK"
(cd /private/tmp && "$DOTNET8" build "$TEST_ROOT/Target/Target.csproj" -c Release --artifacts-path "$WORK/target-artifacts" >/dev/null)
target="$(find "$WORK/target-artifacts" -type f -name AppleEverestDesktopHookGenTarget.dll -path '*/bin/*' | head -1)"
[[ -f "$target" ]] || { echo "error: desktop target missing" >&2; exit 1; }
cp "$target" "$WORK/AppleEverestDesktopHookGenTarget.dll"
(cd "$WORK" && "$DOTNET8" "$HOOKGEN_OUT/MonoMod.RuntimeDetour.HookGen.dll" AppleEverestDesktopHookGenTarget.dll >/dev/null)
[[ -f "$WORK/MMHOOK_AppleEverestDesktopHookGenTarget.dll" ]] || { echo "error: HookGen output missing" >&2; exit 1; }
(cd /private/tmp && "$DOTNET8" run --project "$TEST_ROOT/Runner/Runner.csproj" -c Release \
  -p:AppleEverestDesktopReferenceRoot="$WORK" -p:AppleEverestHookGenToolRoot="$HOOKGEN_OUT")
