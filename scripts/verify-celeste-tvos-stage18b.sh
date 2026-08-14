#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
BUILDER="$REPO_ROOT/build-tvos.sh"
UI="$SCRIPT_DIR/tvos-builder-ui.sh"
LAUNCHER="$SCRIPT_DIR/tvos-builder-command.py"
TESTS="$SCRIPT_DIR/test-tvos-builder-ui.sh"
REPORT="$REPO_ROOT/docs/history/stages/TVOS_BUILDER_UX_STAGE18B_REPORT.md"

for path in "$BUILDER" "$UI" "$LAUNCHER" "$TESTS"; do
  [[ -f "$path" ]] || { echo "error: missing Stage 18B file: ${path#$REPO_ROOT/}" >&2; exit 1; }
done

bash -n "$BUILDER" "$UI" "$TESTS" "$0"
python3 -m py_compile "$LAUNCHER"

grep -Fq -- '--verbose' "$BUILDER"
grep -Fq -- '--no-color' "$BUILDER"
grep -Fq 'CELESTE_TVOS_HEARTBEAT_SECONDS:-60' "$UI"
grep -Fq 'Still working:' "$UI"
grep -Fq 'GITHUB_ACTIONS' "$UI"
grep -Fq 'ui_diagnostic_tail' "$BUILDER"
grep -Fq 'ui_build_success "Build completed successfully"' "$BUILDER"
grep -Fq 'os.setsid()' "$LAUNCHER"
grep -Fq 'os.execvp' "$LAUNCHER"

if grep -Eq '([0-9]+%|percentage progress|progress bar)' "$UI" "$BUILDER"; then
  echo 'error: Stage 18B must not implement fake percentage progress' >&2
  exit 1
fi
if grep -Eq 'xcodebuild[^\n]*(head|sed -n .1p)' "$BUILDER"; then
  echo 'error: xcodebuild output is still consumed through an early-closing pipeline' >&2
  exit 1
fi

"$TESTS"

if [[ -f "$REPORT" ]]; then
  grep -Fq '6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc' "$REPORT"
  grep -Fq '60 seconds' "$REPORT"
fi

echo 'PASS: Stage 18B progress, logging, failure, colour, and CI policy'
