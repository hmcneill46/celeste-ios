#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"

"$SCRIPT_DIR/verify-apple-everest-stage25kf.sh" --repo-root "$REPO_ROOT"
"$SCRIPT_DIR/bootstrap-apple-everest-host.sh"
(cd "${TMPDIR:-/tmp}" && "$REPO_ROOT/.build/apple-everest/toolchain/dotnet8/dotnet" restore \
  "$REPO_ROOT/tools/AppleEverestBuilder/tests/AppleEverestBuilder.Tests.csproj" --locked-mode >/dev/null)
(cd "${TMPDIR:-/tmp}" && "$REPO_ROOT/.build/apple-everest/toolchain/dotnet8/dotnet" run \
  --project "$REPO_ROOT/tools/AppleEverestBuilder/tests/AppleEverestBuilder.Tests.csproj" \
  -c Release --no-restore -- "$REPO_ROOT")
exec python3 "$SCRIPT_DIR/verify-apple-everest-stage25kh.py" "$@"
