#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"

bash -n \
  "$REPO_ROOT/cloud-builder-template/scripts/cloud-common.sh" \
  "$REPO_ROOT/scripts/export-cloud-builder-template.sh" \
  "$0"
python3 -m py_compile \
  "$REPO_ROOT/cloud-builder-template/scripts/prepare-inputs.py" \
  "$REPO_ROOT/scripts/test-cloud-builder-template.py" \
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage18c.py"
ruby -e 'require "yaml"; ARGV.each { |path| YAML.load_file(path) }' \
  "$REPO_ROOT/cloud-builder-template/.github/workflows/build.yml" \
  "$REPO_ROOT/cloud-builder-template/.github/workflows/cleanup.yml"
python3 "$REPO_ROOT/scripts/verify-celeste-tvos-stage18c.py"
"$REPO_ROOT/scripts/verify-celeste-tvos-stage18b.sh"
echo 'PASS: Stage 18C production private cloud-builder verification complete'
