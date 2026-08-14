#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
SOURCE_ROOT="$REPO_ROOT/cloud-builder-template"
MODE="${1:-}"
TARGET="${2:-}"
readonly TEMPLATE_FILES=(
  README.md
  .github/workflows/build.yml
  .github/workflows/cleanup.yml
  scripts/cloud-common.sh
  scripts/prepare-inputs.py
)

usage() {
  cat <<'EOF'
Usage: scripts/export-cloud-builder-template.sh --export TARGET
       scripts/export-cloud-builder-template.sh --check TARGET

Export or compare the tracked canonical cloud-builder template. TARGET may be
an empty directory or an existing clone dedicated to the template. The export
overwrites only the five allow-listed template files; it does not initialise a
Git repository, push, copy build inputs, or remove unrelated paths.
EOF
}

[[ "$MODE" == --export || "$MODE" == --check ]] || { usage >&2; exit 2; }
[[ -n "$TARGET" ]] || { usage >&2; exit 2; }
TARGET="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$TARGET")"
case "$TARGET" in
  /|"$HOME"|"$REPO_ROOT"|"$SOURCE_ROOT")
    echo "error: unsafe template export target: $TARGET" >&2
    exit 1
    ;;
esac

for relative in "${TEMPLATE_FILES[@]}"; do
  [[ -f "$SOURCE_ROOT/$relative" ]] || {
    echo "error: canonical template file is missing: $relative" >&2
    exit 1
  }
done

if [[ "$MODE" == --export ]]; then
  mkdir -p "$TARGET"
  for relative in "${TEMPLATE_FILES[@]}"; do
    mkdir -p "$TARGET/$(dirname -- "$relative")"
    cp "$SOURCE_ROOT/$relative" "$TARGET/$relative"
  done
  chmod +x "$TARGET/scripts/cloud-common.sh" "$TARGET/scripts/prepare-inputs.py"
  printf 'Exported %d canonical cloud-builder files to %s\n' "${#TEMPLATE_FILES[@]}" "$TARGET"
  exit 0
fi

for relative in "${TEMPLATE_FILES[@]}"; do
  cmp -s "$SOURCE_ROOT/$relative" "$TARGET/$relative" || {
    echo "error: template copy differs: $relative" >&2
    exit 1
  }
done

if [[ -d "$TARGET/.git" ]]; then
  actual="$(git -C "$TARGET" ls-files | LC_ALL=C sort)"
else
  actual="$(cd "$TARGET" && find . -type f ! -path './.git/*' -print | sed 's#^./##' | LC_ALL=C sort)"
fi
expected="$(printf '%s\n' "${TEMPLATE_FILES[@]}" | LC_ALL=C sort)"
[[ "$actual" == "$expected" ]] || {
  echo "error: target repository has missing or unexpected tracked template files" >&2
  diff -u <(printf '%s\n' "$expected") <(printf '%s\n' "$actual") || true
  exit 1
}
echo "PASS: public template copy exactly matches cloud-builder-template/"
