#!/bin/zsh
set -euo pipefail
setopt null_glob

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VANILLA_APP="${CELESTE_MACOS_APP:-$HOME/Downloads/Celeste.app}"
EVEREST_ROOT="$PROJECT_ROOT/.build/apple-everest/upstream/Everest"
MINIINSTALLER_OUTPUT="$EVEREST_ROOT/MiniInstaller/bin/Release/net8.0/publish"
EVEREST_OUTPUT="$EVEREST_ROOT/Celeste.Mod.mm/bin/Release/net8.0/publish"
DOTNET_ROOT="$PROJECT_ROOT/.build/apple-everest/toolchain/dotnet8"
PACKAGE_ROOT="$PROJECT_ROOT/.build/apple-everest/stage25kb/combined-public-packages"
REFERENCE_ROOT="$PROJECT_ROOT/.build/apple-everest/stage25kb/macos-reference"
PREPARING_ROOT="$PROJECT_ROOT/.build/apple-everest/stage25kb/macos-reference.preparing"
APP_ROOT="$REFERENCE_ROOT/game/Celeste.app"
STAMP_PATH="$REFERENCE_ROOT/reference-inputs.sha256"

require_file() {
  if [[ ! -f "$1" ]]; then
    print -u2 "Missing required reference input: $1"
    exit 1
  fi
}

require_dir() {
  if [[ ! -d "$1" ]]; then
    print -u2 "Missing required reference input directory: $1"
    exit 1
  fi
}

require_dir "$VANILLA_APP"
require_dir "$MINIINSTALLER_OUTPUT"
require_dir "$EVEREST_OUTPUT"
require_dir "$DOTNET_ROOT"
require_dir "$PACKAGE_ROOT"
require_file "$VANILLA_APP/Contents/Resources/Celeste.exe"
require_file "$MINIINSTALLER_OUTPUT/MiniInstaller.dll"
require_file "$EVEREST_OUTPUT/Celeste.Mod.mm.dll"
require_file "$DOTNET_ROOT/dotnet"

packages=("$PACKAGE_ROOT"/*.zip)
if [[ ${#packages[@]} -ne 16 ]]; then
  print -u2 "Expected the locked 16-package K-A + K-B reference set; found ${#packages[@]}."
  exit 1
fi

current_stamp="$({
  shasum -a 256 "$VANILLA_APP/Contents/Resources/Celeste.exe"
  shasum -a 256 "$MINIINSTALLER_OUTPUT/MiniInstaller.dll"
  shasum -a 256 "$EVEREST_OUTPUT/Celeste.Mod.mm.dll"
  for package in "${packages[@]}"; do
    shasum -a 256 "$package"
  done
} | shasum -a 256 | awk '{print $1}')"

if [[ -x "$APP_ROOT/Contents/Resources/Celeste" && -f "$STAMP_PATH" ]] &&
   [[ "$(<"$STAMP_PATH")" == "$current_stamp" ]]; then
  print "macOS Everest reference is current (K-A + K-B, 16 locked public packages)."
  exit 0
fi

print "Preparing the current macOS Everest reference..."
print "This one-time rebuild copies Celeste.app and installs the real K-A + K-B mod graph."

if [[ -e "$PREPARING_ROOT" ]]; then
  rm -rf -- "$PREPARING_ROOT"
fi
mkdir -p "$PREPARING_ROOT/game" "$PREPARING_ROOT/tmp"
ditto "$VANILLA_APP" "$PREPARING_ROOT/game/Celeste.app"

resources="$PREPARING_ROOT/game/Celeste.app/Contents/Resources"
ditto "$MINIINSTALLER_OUTPUT" "$resources"
ditto "$EVEREST_OUTPUT" "$resources"

(
  cd "$resources"
  env DOTNET_ROOT="$DOTNET_ROOT" PATH="$DOTNET_ROOT:$PATH" \
    "$DOTNET_ROOT/dotnet" MiniInstaller.dll
)

mkdir -p "$resources/Mods"
for package in "${packages[@]}"; do
  ditto "$package" "$resources/Mods/${package:t}"
done
xattr -dr com.apple.quarantine "$PREPARING_ROOT/game/Celeste.app"
print -r -- "$current_stamp" > "$PREPARING_ROOT/reference-inputs.sha256"

if [[ -e "$REFERENCE_ROOT" ]]; then
  rm -rf -- "$REFERENCE_ROOT"
fi
mv "$PREPARING_ROOT" "$REFERENCE_ROOT"

print "macOS Everest reference prepared successfully."
print "Installed mod ZIPs: ${#packages[@]}"
