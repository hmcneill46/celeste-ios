#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/prepare-celeste-tvos-stage5b.sh [options]

Regenerate the accepted Stage 3C ignored runtime tree, restore the locked
generated FMOD API to static __Internal imports, and reuse the accepted Stage
5A external FMOD staging for a device-only Celeste audio build. Installs
nothing and writes no proprietary input to a tracked directory.

Options:
  --game-root DIR     Supported Celeste 1.4.0.0 installation. Defaults to
                      CELESTE_GAME_ROOT.
  --fmod-stage DIR    Accepted Stage 5A staging root
                      (default: .build/fmod-tvos/current).
  --runtime-root DIR  Ignored Stage 5B runtime root
                      (default: .build/celeste-runtime/stage5b-current).
  --artifact-dir DIR  Ignored privacy-safe manifests
                      (default: artifacts/celeste-runtime/stage5b-current).
  --managed-only      Do not stage non-audio Content; compile iteration only.
  --clean             Replace existing marked outputs.
  -h, --help          Show this help.

FMOD SDK archives and the seven Celeste banks remain in the separate ignored
Stage 5A root. This script never reads or modifies the mounted FMOD SDK.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
GAME_ROOT="${CELESTE_GAME_ROOT:-}"
FMOD_STAGE="$REPO_ROOT/.build/fmod-tvos/current"
RUNTIME_ROOT="$REPO_ROOT/.build/celeste-runtime/stage5b-current"
ARTIFACT_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage5b-current"
MANAGED_ONLY=0
CLEAN=0
MARKER=".stage5b-runtime-preparation"
EXPECTED_FMOD_LOGICAL_SHA="b32fc89dbefdda69ab7bf20ea9ece37826dce51787a4f70446493e292b12603d"

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
      echo "error: refusing to replace existing unmarked/non-clean output: ${target/#$REPO_ROOT/\$REPO_ROOT}" >&2
      exit 1
    }
    rm -rf -- "$target"
  fi
}

while (($#)); do
  case "$1" in
    --game-root) [[ $# -ge 2 ]] || { echo "error: --game-root requires a value" >&2; exit 2; }; GAME_ROOT="$2"; shift 2 ;;
    --fmod-stage) [[ $# -ge 2 ]] || { echo "error: --fmod-stage requires a value" >&2; exit 2; }; FMOD_STAGE="$(repo_path "$2")"; shift 2 ;;
    --runtime-root) [[ $# -ge 2 ]] || { echo "error: --runtime-root requires a value" >&2; exit 2; }; RUNTIME_ROOT="$(repo_path "$2")"; shift 2 ;;
    --artifact-dir) [[ $# -ge 2 ]] || { echo "error: --artifact-dir requires a value" >&2; exit 2; }; ARTIFACT_DIR="$(repo_path "$2")"; shift 2 ;;
    --managed-only) MANAGED_ONLY=1; shift ;;
    --clean) CLEAN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ -n "$GAME_ROOT" && -d "$GAME_ROOT" ]] || { echo "error: --game-root or CELESTE_GAME_ROOT must name a directory" >&2; exit 1; }
[[ -f "$FMOD_STAGE/logical-manifest.json" ]] || { echo "error: accepted Stage 5A staging is missing" >&2; exit 1; }
for command in dotnet git python3 cp; do command -v "$command" >/dev/null || { echo "error: required tool is missing: $command" >&2; exit 1; }; done
[[ "$(dotnet --version)" == "10.0.302" ]] || { echo "error: dotnet 10.0.302 is required" >&2; exit 1; }
for root in "$RUNTIME_ROOT" "$ARTIFACT_DIR"; do
  case "$root" in "$REPO_ROOT"/*) relative="${root#$REPO_ROOT/}" ;; *) echo "error: generated roots must be below the repository" >&2; exit 1 ;; esac
  git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative/$MARKER" || { echo "error: generated root is not ignored: \$REPO_ROOT/$relative" >&2; exit 1; }
done
[[ "$RUNTIME_ROOT" != "$ARTIFACT_DIR" && "$RUNTIME_ROOT" != "$REPO_ROOT" && "$ARTIFACT_DIR" != "$REPO_ROOT" ]] || {
  echo "error: generated roots must be distinct and may not be the repository root" >&2; exit 1;
}

python3 - "$FMOD_STAGE/logical-manifest.json" "$EXPECTED_FMOD_LOGICAL_SHA" <<'PY'
import json, pathlib, sys
manifest = json.loads(pathlib.Path(sys.argv[1]).read_text())
if manifest.get("logicalSha256") != sys.argv[2]:
    raise SystemExit("error: Stage 5A logical staging hash is not accepted")
if manifest.get("banks", {}).get("count") != 7:
    raise SystemExit("error: Stage 5A staging does not contain exactly seven banks")
PY

safe_replace "$RUNTIME_ROOT"
safe_replace "$ARTIFACT_DIR"
mkdir -p "$RUNTIME_ROOT" "$ARTIFACT_DIR"
touch "$RUNTIME_ROOT/$MARKER" "$ARTIFACT_DIR/$MARKER"

stage3c_args=(
  --game-root "$GAME_ROOT"
  --runtime-root "$RUNTIME_ROOT/stage3c"
  --artifact-dir "$ARTIFACT_DIR/stage3c"
)
if [[ "$MANAGED_ONLY" -eq 1 ]]; then stage3c_args+=(--managed-only); fi
"$REPO_ROOT/scripts/prepare-celeste-tvos-stage3c.sh" "${stage3c_args[@]}"

mkdir -p "$RUNTIME_ROOT/managed"
cp -R "$RUNTIME_ROOT/stage3c/managed/." "$RUNTIME_ROOT/managed/"
if [[ "$MANAGED_ONLY" -eq 0 ]]; then
  mkdir -p "$RUNTIME_ROOT/content"
  cp -cR "$RUNTIME_ROOT/stage3c/content/Content" "$RUNTIME_ROOT/content/"
fi

ORIGINAL_ROOT="$RUNTIME_ROOT/stage3c/stage3b/stage3a-build/decompiled"
[[ -f "$ORIGINAL_ROOT/Celeste/Audio.cs" && -f "$ORIGINAL_ROOT/FMOD/Studio/System.cs" ]] || {
  echo "error: fresh locked decompiler output is unavailable for FMOD restoration" >&2; exit 1;
}
python3 "$REPO_ROOT/scripts/celeste-stage5b.py" \
  --root "$RUNTIME_ROOT/managed" \
  --original-root "$ORIGINAL_ROOT" \
  --templates "$REPO_ROOT/managed/templates" \
  --policy "$REPO_ROOT/managed/celeste-stage5b-policy.json" \
  --output "$ARTIFACT_DIR/managed-stage5b-manifest.json"

python3 - "$ARTIFACT_DIR/preparation-result.json" "$MANAGED_ONLY" <<'PY'
import json, pathlib, sys
pathlib.Path(sys.argv[1]).write_text(json.dumps({
    "schemaVersion": 1,
    "gameRoot": "$CELESTE_GAME_ROOT",
    "stage3CRegenerated": True,
    "stage5AStagingLogicalSha256": "b32fc89dbefdda69ab7bf20ea9ece37826dce51787a4f70446493e292b12603d",
    "contentStaged": sys.argv[2] == "0",
    "fmodBanksRemainInStage5AStaging": True,
    "fmodSdkRead": False,
    "generatedSourceTracked": False,
    "proprietaryContentTracked": False
}, indent=2, sort_keys=True) + "\n")
PY

echo "prepared ignored Stage 5B Celeste real-audio inputs"
echo "runtime root: <RUNTIME_ROOT>"
echo "artifact root: <ARTIFACT_DIR>"
echo "FMOD stage: <FMOD_STAGE>"
