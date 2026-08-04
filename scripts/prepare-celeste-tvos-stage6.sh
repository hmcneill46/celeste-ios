#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/prepare-celeste-tvos-stage6.sh [options]

Regenerate the locked Stage 5B tree once, derive separate no-audio and
real-audio Stage 6 managed trees, and apply the tracked durable-storage hooks.
All generated source and user-owned Content remain ignored.

Options:
  --game-root DIR      Celeste 1.4.0.0 installation (default: CELESTE_GAME_ROOT)
  --fmod-stage DIR     Accepted Stage 5A staging root (default: .build/fmod-tvos/current)
  --runtime-root DIR   Ignored output root (default: .build/celeste-runtime/stage6-current)
  --artifact-dir DIR   Ignored manifests (default: artifacts/celeste-runtime/stage6-current)
  --managed-only       Omit non-audio game Content for compile iteration
  --clean              Replace existing marked outputs
  -h, --help           Show this help

This script installs nothing and never reads Settings, SaveData, or the
contents of the external FMOD SDK.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
GAME_ROOT="${CELESTE_GAME_ROOT:-}"
FMOD_STAGE="$REPO_ROOT/.build/fmod-tvos/current"
RUNTIME_ROOT="$REPO_ROOT/.build/celeste-runtime/stage6-current"
ARTIFACT_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage6-current"
MANAGED_ONLY=0
CLEAN=0
MARKER=.stage6-runtime-preparation

repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
safe_replace() {
  local target="$1"
  if [[ -e "$target" ]]; then
    [[ "$CLEAN" -eq 1 && -f "$target/$MARKER" ]] || { echo "error: refusing to replace unmarked output: ${target/#$REPO_ROOT/\$REPO_ROOT}" >&2; exit 1; }
    rm -rf -- "$target"
  fi
}

while (($#)); do
  case "$1" in
    --game-root) [[ $# -ge 2 ]] || exit 2; GAME_ROOT="$2"; shift 2 ;;
    --fmod-stage) [[ $# -ge 2 ]] || exit 2; FMOD_STAGE="$(repo_path "$2")"; shift 2 ;;
    --runtime-root) [[ $# -ge 2 ]] || exit 2; RUNTIME_ROOT="$(repo_path "$2")"; shift 2 ;;
    --artifact-dir) [[ $# -ge 2 ]] || exit 2; ARTIFACT_DIR="$(repo_path "$2")"; shift 2 ;;
    --managed-only) MANAGED_ONLY=1; shift ;;
    --clean) CLEAN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ -n "$GAME_ROOT" && -d "$GAME_ROOT" ]] || { echo "error: --game-root or CELESTE_GAME_ROOT is required" >&2; exit 1; }
[[ -f "$FMOD_STAGE/logical-manifest.json" ]] || { echo "error: accepted Stage 5A staging is missing" >&2; exit 1; }
for command in dotnet git python3 cp; do command -v "$command" >/dev/null || { echo "error: missing required tool: $command" >&2; exit 1; }; done
[[ "$(dotnet --version)" == 10.0.302 ]] || { echo "error: dotnet 10.0.302 is required" >&2; exit 1; }
for root in "$RUNTIME_ROOT" "$ARTIFACT_DIR"; do
  case "$root" in "$REPO_ROOT"/*) relative="${root#$REPO_ROOT/}" ;; *) echo "error: output must be below repository" >&2; exit 1 ;; esac
  git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative/$MARKER" || { echo "error: output is not ignored: \$REPO_ROOT/$relative" >&2; exit 1; }
done
[[ "$RUNTIME_ROOT" != "$ARTIFACT_DIR" ]] || { echo "error: runtime and artifact roots must differ" >&2; exit 1; }

safe_replace "$RUNTIME_ROOT"
safe_replace "$ARTIFACT_DIR"
mkdir -p "$RUNTIME_ROOT" "$ARTIFACT_DIR"
touch "$RUNTIME_ROOT/$MARKER" "$ARTIFACT_DIR/$MARKER"

stage5b_args=(--game-root "$GAME_ROOT" --fmod-stage "$FMOD_STAGE" --runtime-root "$RUNTIME_ROOT/base-stage5b" --artifact-dir "$ARTIFACT_DIR/base-stage5b")
if [[ "$MANAGED_ONLY" -eq 1 ]]; then stage5b_args+=(--managed-only); fi
"$REPO_ROOT/scripts/prepare-celeste-tvos-stage5b.sh" "${stage5b_args[@]}"

mkdir -p "$RUNTIME_ROOT/noaudio/managed" "$RUNTIME_ROOT/audio/managed"
cp -R "$RUNTIME_ROOT/base-stage5b/stage3c/managed/." "$RUNTIME_ROOT/noaudio/managed/"
cp -R "$RUNTIME_ROOT/base-stage5b/managed/." "$RUNTIME_ROOT/audio/managed/"
if [[ "$MANAGED_ONLY" -eq 0 ]]; then
  mkdir -p "$RUNTIME_ROOT/noaudio/content" "$RUNTIME_ROOT/audio/content"
  cp -cR "$RUNTIME_ROOT/base-stage5b/content/Content" "$RUNTIME_ROOT/noaudio/content/"
  cp -cR "$RUNTIME_ROOT/base-stage5b/content/Content" "$RUNTIME_ROOT/audio/content/"
fi

for mode in noAudio realAudio; do
  case "$mode" in noAudio) tree=noaudio ;; realAudio) tree=audio ;; esac
  python3 "$REPO_ROOT/scripts/celeste-stage6.py" \
    --root "$RUNTIME_ROOT/$tree/managed" \
    --templates "$REPO_ROOT/managed/templates" \
    --policy "$REPO_ROOT/managed/celeste-stage6-policy.json" \
    --mode "$mode" \
    --output "$ARTIFACT_DIR/$tree-generated-manifest.json"
done

python3 - "$ARTIFACT_DIR/preparation-result.json" "$MANAGED_ONLY" <<'PY'
import json, pathlib, sys
pathlib.Path(sys.argv[1]).write_text(json.dumps({
    "schemaVersion": 1,
    "gameRoot": "$CELESTE_GAME_ROOT",
    "fmodSdkRoot": "$FMOD_SDK_ROOT",
    "stage5BRegeneratedOnce": True,
    "noAudioAndRealAudioTrees": True,
    "contentStaged": sys.argv[2] == "0",
    "generatedSourceTracked": False,
    "settingsOrSaveDataRead": False
}, indent=2, sort_keys=True) + "\n")
PY

echo "prepared ignored Stage 6 no-audio and real-audio inputs"
echo "runtime root: <RUNTIME_ROOT>"
echo "artifact root: <ARTIFACT_DIR>"
