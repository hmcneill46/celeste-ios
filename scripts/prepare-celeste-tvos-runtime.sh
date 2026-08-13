#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/prepare-celeste-tvos-runtime.sh [options]

Regenerate the accepted Stage 3A source, apply the deterministic Stage 3B
runtime transformations, and stage user-owned non-audio Content below ignored
directories. Installs nothing and never stages the FMOD content tree.

Options:
  --game-root DIR     Supported Celeste 1.4.0.0 installation. Defaults to
                      CELESTE_GAME_ROOT.
  --runtime-root DIR  Ignored generated runtime root
                      (default: .build/celeste-runtime/current).
  --artifact-dir DIR  Ignored privacy-safe manifests
                      (default: artifacts/celeste-runtime/current).
  --managed-only      Regenerate/transform managed source without copying
                      Content; intended only for implementation iteration.
  --clean             Replace an existing marked runtime/artifact directory.
  -h, --help          Show this help.

The default invocation stages every validated Content file except Content/FMOD.
Source paths are represented as $CELESTE_GAME_ROOT in generated manifests.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
GAME_ROOT="${CELESTE_GAME_ROOT:-}"
RUNTIME_ROOT="$REPO_ROOT/.build/celeste-runtime/current"
ARTIFACT_DIR="$REPO_ROOT/artifacts/celeste-runtime/current"
CLEAN=0
MANAGED_ONLY=0
MARKER=".stage3b-runtime-preparation"

repo_path() {
  case "$1" in
    /*) printf '%s\n' "$1" ;;
    *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;;
  esac
}

safe_replace() {
  local target="$1"
  if [[ -e "$target" ]]; then
    [[ "$CLEAN" -eq 1 ]] || {
      echo "error: output exists; rerun with --clean: ${target/#$REPO_ROOT/\$REPO_ROOT}" >&2
      exit 1
    }
    [[ -f "$target/$MARKER" ]] || {
      echo "error: refusing to replace unmarked directory: ${target/#$REPO_ROOT/\$REPO_ROOT}" >&2
      exit 1
    }
    rm -rf -- "$target"
  fi
}

while (($#)); do
  case "$1" in
    --game-root)
      [[ $# -ge 2 ]] || { echo "error: --game-root requires a value" >&2; exit 2; }
      GAME_ROOT="$2"
      shift 2
      ;;
    --runtime-root)
      [[ $# -ge 2 ]] || { echo "error: --runtime-root requires a value" >&2; exit 2; }
      RUNTIME_ROOT="$(repo_path "$2")"
      shift 2
      ;;
    --artifact-dir)
      [[ $# -ge 2 ]] || { echo "error: --artifact-dir requires a value" >&2; exit 2; }
      ARTIFACT_DIR="$(repo_path "$2")"
      shift 2
      ;;
    --managed-only)
      MANAGED_ONLY=1
      shift
      ;;
    --clean)
      CLEAN=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "error: unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

[[ -n "$GAME_ROOT" && -d "$GAME_ROOT" ]] || {
  echo "error: --game-root or CELESTE_GAME_ROOT must name an existing directory" >&2
  exit 1
}
[[ "$RUNTIME_ROOT" != "$REPO_ROOT" && "$ARTIFACT_DIR" != "$REPO_ROOT" && "$RUNTIME_ROOT" != "$ARTIFACT_DIR" ]] || {
  echo "error: generated roots must be distinct and may not be the repository root" >&2
  exit 1
}
for command in dotnet git python3 cp shasum; do
  command -v "$command" >/dev/null || { echo "error: required tool is missing: $command" >&2; exit 1; }
done
for root in "$RUNTIME_ROOT" "$ARTIFACT_DIR"; do
  case "$root" in
    "$REPO_ROOT"/*) relative="${root#$REPO_ROOT/}" ;;
    *) echo "error: generated roots must be below the repository" >&2; exit 1 ;;
  esac
  git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative/$MARKER" || {
    echo "error: generated root is not ignored: \$REPO_ROOT/$relative" >&2
    exit 1
  }
done
[[ "$(dotnet --version)" == "10.0.302" ]] || { echo "error: dotnet 10.0.302 is required" >&2; exit 1; }

safe_replace "$RUNTIME_ROOT"
safe_replace "$ARTIFACT_DIR"
mkdir -p "$RUNTIME_ROOT" "$ARTIFACT_DIR" "$RUNTIME_ROOT/logs"
touch "$RUNTIME_ROOT/$MARKER" "$ARTIFACT_DIR/$MARKER"

"$REPO_ROOT/scripts/validate-celeste-input.sh" \
  --game-root "$GAME_ROOT" \
  --output "$ARTIFACT_DIR/input-manifest.json"
RESOLVED_RELATIVE="$(python3 - "$ARTIFACT_DIR/input-manifest.json" <<'PY'
import json,pathlib,sys
value=json.loads(pathlib.Path(sys.argv[1]).read_text())["resolvedRootRelative"]
path=pathlib.PurePosixPath(value)
if value == ".": print("")
elif path.is_absolute() or any(part in ("", ".", "..") for part in path.parts):
    raise SystemExit("error: validator returned an unsafe resolved game root")
else: print(value)
PY
)"
[[ -z "$RESOLVED_RELATIVE" ]] || GAME_ROOT="$GAME_ROOT/$RESOLVED_RELATIVE"

"$REPO_ROOT/scripts/prepare-celeste-managed.sh" \
  --game-root "$GAME_ROOT" \
  --build-dir "$RUNTIME_ROOT/stage3a-build" \
  --artifact-dir "$ARTIFACT_DIR/stage3a" \
  --clean

mkdir -p "$RUNTIME_ROOT/managed"
cp -R "$RUNTIME_ROOT/stage3a-build/patched/." "$RUNTIME_ROOT/managed/"
python3 "$REPO_ROOT/scripts/celeste-stage3b.py" transform \
  --root "$RUNTIME_ROOT/managed" \
  --templates "$REPO_ROOT/managed/templates" \
  --policy "$REPO_ROOT/managed/celeste-stage3b-policy.json" \
  --stage3a-manifest "$ARTIFACT_DIR/stage3a/patched-source-manifest.json" \
  --output "$ARTIFACT_DIR/managed-runtime-manifest.json"

if [[ "$MANAGED_ONLY" -eq 0 ]]; then
  mkdir -p "$RUNTIME_ROOT/content/Content"
  python3 "$REPO_ROOT/scripts/celeste-stage3b.py" stage-content \
    --game-root "$GAME_ROOT" \
    --destination "$RUNTIME_ROOT/content/Content" \
    --policy "$REPO_ROOT/managed/celeste-stage3b-policy.json" \
    --input-manifest "$ARTIFACT_DIR/input-manifest.json" \
    --output "$ARTIFACT_DIR/content-staging-manifest.json"
fi

python3 - "$ARTIFACT_DIR/preparation-result.json" "$MANAGED_ONLY" <<'PY'
import json, pathlib, sys
pathlib.Path(sys.argv[1]).write_text(json.dumps({
    "schemaVersion": 1,
    "gameRoot": "$CELESTE_GAME_ROOT",
    "managedSourceRegeneratedFromStage3A": True,
    "managedSourceTracked": False,
    "contentStaged": sys.argv[2] == "0",
    "contentTracked": False,
    "fmodContentStaged": False,
    "fmodNativeLinked": False,
}, indent=2, sort_keys=True) + "\n")
PY

echo "prepared ignored Stage 3B runtime inputs"
echo "runtime root: <RUNTIME_ROOT>"
echo "artifact root: <ARTIFACT_DIR>"
