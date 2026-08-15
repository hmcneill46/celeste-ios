#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"

dotnet run --project "$REPO_ROOT/modern-ios/CelesteIOSFoundationTests/CelesteIOSFoundationTests.csproj" -c Release
dotnet run --project "$REPO_ROOT/modern-ios/CelesteIOSDurabilityTests/CelesteIOSDurabilityTests.csproj" -c Release
python3 "$REPO_ROOT/scripts/verify-celeste-ios-stage24c2.py"
