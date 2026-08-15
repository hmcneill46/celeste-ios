#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"

python3 "$SCRIPT_DIR/verify-celeste-ios-stage24c1.py"
dotnet run --project "$REPO_ROOT/modern-ios/CelesteIOSFoundationTests/CelesteIOSFoundationTests.csproj" -c Release
