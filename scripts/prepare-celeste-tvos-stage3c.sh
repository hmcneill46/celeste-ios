#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/prepare-celeste-tvos-stage3c.sh [options]

Regenerate the accepted Stage 3B ignored runtime tree, then apply the locked
Stage 3C Prologue diagnostics, temporary SaveData serializer and haptic fixes.
Installs nothing and never stages FMOD content.

Options:
  --game-root DIR      Supported Celeste 1.4.0.0 installation. Defaults to
                       CELESTE_GAME_ROOT.
  --runtime-root DIR   Ignored Stage 3C runtime root
                       (default: .build/celeste-runtime/stage3c-current).
  --artifact-dir DIR   Ignored privacy-safe evidence/manifests
                       (default: artifacts/celeste-runtime/stage3c-current).
  --fault-baseline     Retain the Stage 3B null-audio/save guards while adding
                       diagnostic routing; for reproducing the original fault.
  --managed-only       Do not copy Content; compile-only iteration.
  --clean              Replace existing marked output directories.
  -h, --help           Show this help.

Generated source, content, logs and binaries remain below ignored directories.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
GAME_ROOT="${CELESTE_GAME_ROOT:-}"
RUNTIME_ROOT="$REPO_ROOT/.build/celeste-runtime/stage3c-current"
ARTIFACT_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage3c-current"
FAULT_BASELINE=0
MANAGED_ONLY=0
CLEAN=0
MARKER=".stage3c-runtime-preparation"

repo_path() {
  case "$1" in
    /*) printf '%s\n' "$1" ;;
    *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;;
  esac
}

safe_replace() {
  local target="$1"
  if [[ -e "$target" ]]; then
    [[ "$CLEAN" -eq 1 && -f "$target/$MARKER" ]] || {
      echo "error: refusing to replace unmarked or non-clean output: ${target/#$REPO_ROOT/\$REPO_ROOT}" >&2
      exit 1
    }
    rm -rf -- "$target"
  fi
}

while (($#)); do
  case "$1" in
    --game-root) [[ $# -ge 2 ]] || { echo "error: --game-root requires a value" >&2; exit 2; }; GAME_ROOT="$2"; shift 2 ;;
    --runtime-root) [[ $# -ge 2 ]] || { echo "error: --runtime-root requires a value" >&2; exit 2; }; RUNTIME_ROOT="$(repo_path "$2")"; shift 2 ;;
    --artifact-dir) [[ $# -ge 2 ]] || { echo "error: --artifact-dir requires a value" >&2; exit 2; }; ARTIFACT_DIR="$(repo_path "$2")"; shift 2 ;;
    --fault-baseline) FAULT_BASELINE=1; shift ;;
    --managed-only) MANAGED_ONLY=1; shift ;;
    --clean) CLEAN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ -n "$GAME_ROOT" && -d "$GAME_ROOT" ]] || { echo "error: --game-root or CELESTE_GAME_ROOT must name an existing directory" >&2; exit 1; }
[[ "$RUNTIME_ROOT" != "$ARTIFACT_DIR" && "$RUNTIME_ROOT" != "$REPO_ROOT" && "$ARTIFACT_DIR" != "$REPO_ROOT" ]] || {
  echo "error: generated roots must be distinct and below the repository" >&2; exit 1;
}
for command in dotnet git python3 cp; do command -v "$command" >/dev/null || { echo "error: required tool is missing: $command" >&2; exit 1; }; done
for root in "$RUNTIME_ROOT" "$ARTIFACT_DIR"; do
  case "$root" in "$REPO_ROOT"/*) relative="${root#$REPO_ROOT/}" ;; *) echo "error: generated root must be below the repository" >&2; exit 1 ;; esac
  git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative/$MARKER" || {
    echo "error: generated root is not ignored: \$REPO_ROOT/$relative" >&2; exit 1;
  }
done

safe_replace "$RUNTIME_ROOT"
safe_replace "$ARTIFACT_DIR"
mkdir -p "$RUNTIME_ROOT" "$ARTIFACT_DIR"
touch "$RUNTIME_ROOT/$MARKER" "$ARTIFACT_DIR/$MARKER"

stage3b_args=(
  --game-root "$GAME_ROOT"
  --runtime-root "$RUNTIME_ROOT/stage3b"
  --artifact-dir "$ARTIFACT_DIR/stage3b"
)
if [[ "$MANAGED_ONLY" -eq 1 ]]; then stage3b_args+=(--managed-only); fi
"$REPO_ROOT/scripts/prepare-celeste-tvos-runtime.sh" "${stage3b_args[@]}"

mkdir -p "$RUNTIME_ROOT/managed"
cp -R "$RUNTIME_ROOT/stage3b/managed/." "$RUNTIME_ROOT/managed/"
if [[ "$MANAGED_ONLY" -eq 0 ]]; then
  mkdir -p "$RUNTIME_ROOT/content"
  cp -cR "$RUNTIME_ROOT/stage3b/content/Content" "$RUNTIME_ROOT/content/"
fi

transform_args=(
  --root "$RUNTIME_ROOT/managed"
  --templates "$REPO_ROOT/managed/templates"
  --policy "$REPO_ROOT/managed/celeste-stage3c-policy.json"
  --output "$ARTIFACT_DIR/managed-stage3c-manifest.json"
)
if [[ "$FAULT_BASELINE" -eq 1 ]]; then transform_args+=(--fault-baseline); fi
python3 "$REPO_ROOT/scripts/celeste-stage3c.py" "${transform_args[@]}"

python3 - "$ARTIFACT_DIR/preparation-result.json" "$FAULT_BASELINE" "$MANAGED_ONLY" <<'PY'
import json, pathlib, sys
pathlib.Path(sys.argv[1]).write_text(json.dumps({
    "schemaVersion": 1,
    "gameRoot": "$CELESTE_GAME_ROOT",
    "stage3BRegenerated": True,
    "stage3CFaultBaseline": sys.argv[2] == "1",
    "contentStaged": sys.argv[3] == "0",
    "generatedSourceTracked": False,
    "gameContentTracked": False,
    "fmodContentStaged": False,
    "fmodNativeLinked": False
}, indent=2, sort_keys=True) + "\n")
PY

echo "prepared ignored Stage 3C runtime inputs"
echo "runtime root: <RUNTIME_ROOT>"
echo "artifact root: <ARTIFACT_DIR>"
