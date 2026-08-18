#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
PROFILE="$REPO_ROOT/apple-everest/profiles/stable-1.6458.0.json"
UPSTREAM="$REPO_ROOT/.build/apple-everest/upstream/Everest"
TEST_ROOT="$REPO_ROOT/apple-everest/tests/il-freeze"
OUTPUT="$REPO_ROOT/.build/apple-everest/il-freeze"
DOTNET8="$REPO_ROOT/.build/apple-everest/toolchain/dotnet8/dotnet"
DOTNET9="$REPO_ROOT/.build/apple-everest/toolchain/dotnet9/dotnet"
MONOMOD_SHA="dfc30a1506d37fb88a2c2be004f525205f46a24c"

"$SCRIPT_DIR/bootstrap-apple-everest-host.sh"
(cd "$REPO_ROOT/tools/AppleEverestBuilder" && "$DOTNET8" run --project AppleEverestBuilder.csproj -- acquire --profile "$PROFILE" --output "$UPSTREAM")
[[ "$(git -C "$UPSTREAM/external/MonoMod" rev-parse HEAD)" == "$MONOMOD_SHA" ]] || { echo "error: MonoMod pin mismatch" >&2; exit 1; }

(cd "$UPSTREAM/external/MonoMod" && "$DOTNET9" build src/MonoMod.Utils/MonoMod.Utils.csproj \
  -c Release -f net8.0 -p:RestoreLockedMode=false >/dev/null)
MONOMOD_UTILS="$UPSTREAM/external/MonoMod/artifacts/bin/MonoMod.Utils/release_net8.0/MonoMod.Utils.dll"
[[ -f "$MONOMOD_UTILS" ]] || { echo "error: pinned MonoMod.Utils output missing" >&2; exit 1; }

mkdir -p "$OUTPUT/frozen"
(cd "$TEST_ROOT" && "$DOTNET8" build Runner/Runner.csproj -c Release --nologo >/dev/null)
RUNNER="$TEST_ROOT/Runner/bin/Release/net8.0/Runner.dll"
TARGET_IN_RUNNER="$TEST_ROOT/Runner/bin/Release/net8.0/AppleEverestIlFreezeTarget.dll"
EXPECTED=10 "$DOTNET8" "$RUNNER"

(cd "$TEST_ROOT" && "$DOTNET8" restore Freezer/Freezer.csproj -p:MonoModUtilsPath="$MONOMOD_UTILS" --locked-mode >/dev/null)
(cd "$TEST_ROOT" && "$DOTNET8" build Freezer/Freezer.csproj -c Release --no-restore -p:MonoModUtilsPath="$MONOMOD_UTILS" --nologo >/dev/null)
"$DOTNET8" "$TEST_ROOT/Freezer/bin/Release/net8.0/Freezer.dll" \
  "$TARGET_IN_RUNNER" "$OUTPUT/frozen/AppleEverestIlFreezeTarget.dll"
cp "$OUTPUT/frozen/AppleEverestIlFreezeTarget.dll" "$TARGET_IN_RUNNER"
EXPECTED=15 "$DOTNET8" "$RUNNER"

if find "$TEST_ROOT/Runner/bin/Release/net8.0" -maxdepth 1 -type f \
    \( -name 'MonoMod*.dll' -o -name 'Mono.Cecil*.dll' -o -name '*RuntimeDetour*.dll' \) | grep -q .; then
  echo "error: frozen runner retained a host transformation dependency" >&2
  exit 1
fi
shasum -a 256 "$OUTPUT/frozen/AppleEverestIlFreezeTarget.dll" > "$OUTPUT/frozen-target.sha256"
printf 'PASS: real pinned MonoMod IL freeze; frozen runner has no MonoMod/RuntimeDetour dependency\n'
