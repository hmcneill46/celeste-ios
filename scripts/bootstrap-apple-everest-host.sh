#!/usr/bin/env bash
set -euo pipefail

readonly DOTNET8_VERSION="8.0.424"
readonly DOTNET9_VERSION="9.0.317"
readonly INSTALL_SCRIPT_SHA256="082f7685e156738a1b2e2ed8381a621870d4ce8e8c59278034556f05c186eb2e"

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
TOOL_ROOT="$REPO_ROOT/.build/apple-everest/toolchain"
INSTALL_SCRIPT="$TOOL_ROOT/dotnet-install.sh"

verify_sdk() {
  local root="$1" version="$2" probe
  [[ -x "$root/dotnet" ]] || return 1
  probe="$(CDPATH= cd -- "$root" && ./dotnet --list-sdks 2>/dev/null | awk '{print $1}' | grep -Fx "$version" || true)"
  [[ "$probe" == "$version" ]]
}

install_sdk() {
  local version="$1" root="$2"
  if verify_sdk "$root" "$version"; then
    printf 'Apple Everest host SDK %s: ready\n' "$version"
    return
  fi
  mkdir -p "$TOOL_ROOT"
  if [[ ! -f "$INSTALL_SCRIPT" ]] || [[ "$(shasum -a 256 "$INSTALL_SCRIPT" | awk '{print $1}')" != "$INSTALL_SCRIPT_SHA256" ]]; then
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$INSTALL_SCRIPT.download"
    actual="$(shasum -a 256 "$INSTALL_SCRIPT.download" | awk '{print $1}')"
    [[ "$actual" == "$INSTALL_SCRIPT_SHA256" ]] || { echo "error: dotnet-install.sh digest mismatch" >&2; exit 1; }
    mv "$INSTALL_SCRIPT.download" "$INSTALL_SCRIPT"
    chmod 755 "$INSTALL_SCRIPT"
  fi
  if [[ -e "$root" ]]; then
    [[ "$root" == "$TOOL_ROOT"/* ]] || { echo "error: unsafe SDK cleanup target" >&2; exit 1; }
    find "$root" -depth -delete
  fi
  bash "$INSTALL_SCRIPT" --version "$version" --install-dir "$root" --no-path
  verify_sdk "$root" "$version" || { echo "error: .NET SDK $version verification failed" >&2; exit 1; }
  printf 'Apple Everest host SDK %s: installed and verified\n' "$version"
}

install_sdk "$DOTNET8_VERSION" "$TOOL_ROOT/dotnet8"
install_sdk "$DOTNET9_VERSION" "$TOOL_ROOT/dotnet9"
printf 'PASS: bounded Apple Everest host toolchain\n'
