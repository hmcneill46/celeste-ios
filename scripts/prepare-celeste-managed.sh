#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/prepare-celeste-managed.sh --game-root DIR [options]

Validate, locally decompile and deterministically patch the supported user-owned
Celeste input for Stage 3A. Generated/game material stays below ignored paths.
The exact ilspycmd package is restored repository-locally; no global tool or
host software is installed.

Options:
  --game-root DIR    User-owned Celeste installation (required).
  --build-dir DIR    Generated tree (default: .build/celeste-managed/current).
  --artifact-dir DIR Privacy-safe manifests
                     (default: artifacts/celeste-managed/current).
  --clean            Replace an existing directory created by this script.
  -h, --help         Show this help.

Relative paths are resolved from the repository root. Raw generated source and
logs are ignored. The game Content directory is validated but never copied.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
GAME_ROOT=""
BUILD_DIR="$REPO_ROOT/.build/celeste-managed/current"
ARTIFACT_DIR="$REPO_ROOT/artifacts/celeste-managed/current"
CLEAN=0
LOCK="$REPO_ROOT/managed/celeste-generation.lock.json"
MARKER=".stage3a-managed-generation"

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

verify_locked_tree() {
  local manifest="$1"
  local kind="$2"
  python3 - "$LOCK" "$manifest" "$kind" <<'PY'
import json, pathlib, sys
lock = json.loads(pathlib.Path(sys.argv[1]).read_text())
manifest = json.loads(pathlib.Path(sys.argv[2]).read_text())
expected = lock["expectedGeneration"][sys.argv[3]]
actual = {key: manifest[key] for key in ("fileCount", "logicalSha256")}
if actual != expected:
    raise SystemExit(f"error: {sys.argv[3]} differs from the locked logical output: {actual}")
PY
}

while (($#)); do
  case "$1" in
    --game-root)
      [[ $# -ge 2 ]] || { echo "error: --game-root requires a value" >&2; exit 2; }
      GAME_ROOT="$2"
      shift 2
      ;;
    --build-dir)
      [[ $# -ge 2 ]] || { echo "error: --build-dir requires a value" >&2; exit 2; }
      BUILD_DIR="$(repo_path "$2")"
      shift 2
      ;;
    --artifact-dir)
      [[ $# -ge 2 ]] || { echo "error: --artifact-dir requires a value" >&2; exit 2; }
      ARTIFACT_DIR="$(repo_path "$2")"
      shift 2
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

[[ -n "$GAME_ROOT" ]] || { echo "error: --game-root is required" >&2; exit 2; }
[[ -d "$GAME_ROOT" ]] || { echo "error: --game-root is not an existing directory" >&2; exit 1; }
[[ "$BUILD_DIR" != "$REPO_ROOT" && "$ARTIFACT_DIR" != "$REPO_ROOT" ]] || {
  echo "error: repository root may not be used as a generated output" >&2
  exit 1
}
for command in awk dotnet python3 patch git shasum; do
  command -v "$command" >/dev/null || { echo "error: required tool is missing: $command" >&2; exit 1; }
done
[[ "$BUILD_DIR" != "$ARTIFACT_DIR" ]] || {
  echo "error: --build-dir and --artifact-dir must be distinct" >&2
  exit 1
}
for generated_root in "$BUILD_DIR" "$ARTIFACT_DIR"; do
  case "$generated_root" in
    "$REPO_ROOT"/*) generated_relative="${generated_root#$REPO_ROOT/}" ;;
    *) echo "error: generated directories must be inside ignored repository paths" >&2; exit 1 ;;
  esac
  git -C "$REPO_ROOT" check-ignore --no-index -q -- "$generated_relative/$MARKER" || {
    echo "error: generated directory is not ignored by Git: \$REPO_ROOT/$generated_relative" >&2
    exit 1
  }
done
[[ "$(dotnet --version)" == "10.0.302" ]] || { echo "error: dotnet 10.0.302 is required" >&2; exit 1; }
[[ "$(git -C "$REPO_ROOT/FNA" rev-parse HEAD)" == "d52b4ce61e4086b785c51a96d331dbf106975a58" ]] || {
  echo "error: pinned FNA submodule revision changed" >&2
  exit 1
}

safe_replace "$BUILD_DIR"
safe_replace "$ARTIFACT_DIR"
mkdir -p "$BUILD_DIR" "$ARTIFACT_DIR"
touch "$BUILD_DIR/$MARKER" "$ARTIFACT_DIR/$MARKER"
mkdir -p "$BUILD_DIR/input" "$BUILD_DIR/decompiled" "$BUILD_DIR/patched" \
  "$BUILD_DIR/logs" "$BUILD_DIR/tool-home" "$BUILD_DIR/packages" "$ARTIFACT_DIR"

"$REPO_ROOT/scripts/validate-celeste-input.sh" \
  --game-root "$GAME_ROOT" \
  --output "$BUILD_DIR/input-manifest.json"
cp "$BUILD_DIR/input-manifest.json" "$ARTIFACT_DIR/input-manifest.json"

# Stage only managed assemblies. Content is deliberately never copied.
for name in Celeste.exe Celeste.Content.dll FNA.dll; do
  cp "$GAME_ROOT/$name" "$BUILD_DIR/input/$name"
done

python3 - "$REPO_ROOT" "$LOCK" <<'PY'
import hashlib, json, pathlib, sys
root, lock_path = map(pathlib.Path, sys.argv[1:3])
lock = json.loads(lock_path.read_text())
for entry in lock["patchOrder"]:
    path_text = entry["path"]
    if "#" in path_text:
        path_text = path_text.split("#", 1)[0]
    path = root / path_text
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    if actual != entry["sha256"]:
        raise SystemExit(f"error: locked patch/transform changed: {path.relative_to(root)}")
for entry in lock["templates"]:
    path = root / entry["path"]
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    if actual != entry["sha256"]:
        raise SystemExit(f"error: locked template changed: {path.relative_to(root)}")
PY

export DOTNET_CLI_HOME="$BUILD_DIR/tool-home"
export NUGET_PACKAGES="$BUILD_DIR/packages"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

echo "+ dotnet tool restore (locked repository-local manifest)"
if ! (cd "$REPO_ROOT" && dotnet tool restore --tool-manifest "$REPO_ROOT/.config/dotnet-tools.json" --disable-parallel) \
  >"$BUILD_DIR/logs/tool-restore.log" 2>&1; then
  echo "error: locked repository-local ilspycmd restore failed" >&2
  echo "       inspect <BUILD_DIR>/logs/tool-restore.log" >&2
  exit 1
fi
ILSPY_PACKAGE="$NUGET_PACKAGES/ilspycmd/8.0.0.7246-preview3/ilspycmd.8.0.0.7246-preview3.nupkg"
[[ -f "$ILSPY_PACKAGE" ]] || { echo "error: restored locked ilspycmd package is missing" >&2; exit 1; }
[[ "$(shasum -a 256 "$ILSPY_PACKAGE" | awk '{print $1}')" == "9f2fe20986177e444b4bd9126a37514b073d49646a2df71b750d4a839cb97424" ]] || {
  echo "error: restored ilspycmd package SHA-256 does not match the generation lock" >&2
  exit 1
}
TOOL_VERSION="$(cd "$REPO_ROOT" && dotnet tool run ilspycmd -- --version)"
grep -Fq 'ilspycmd: 8.0.0.7246' <<<"$TOOL_VERSION" || {
  echo "error: restored ilspycmd did not report locked version 8.0.0.7246" >&2
  exit 1
}

echo "+ ilspycmd -p --nested-directories -lv CSharp10_0"
if ! (cd "$REPO_ROOT" && dotnet tool run ilspycmd -- \
  -p --nested-directories -lv CSharp10_0 \
  -o "$BUILD_DIR/decompiled" \
  -r "$BUILD_DIR/input" \
  "$BUILD_DIR/input/Celeste.exe") >"$BUILD_DIR/logs/decompile.log" 2>&1; then
  echo "error: locked Celeste decompilation failed" >&2
  echo "       inspect <BUILD_DIR>/logs/decompile.log" >&2
  exit 1
fi

grep -Fq 'Version = new Version(1, 4, 0, 0);' "$BUILD_DIR/decompiled/Celeste/Celeste.cs" || {
  echo "error: decompiled constructor does not prove Celeste game version 1.4.0.0" >&2
  exit 1
}
python3 "$REPO_ROOT/scripts/celeste-managed.py" tree-manifest \
  --root "$BUILD_DIR/decompiled" \
  --kind decompiled-source \
  --placeholder DECOMPILED_ROOT \
  --output "$ARTIFACT_DIR/decompiled-source-manifest.json"
verify_locked_tree "$ARTIFACT_DIR/decompiled-source-manifest.json" decompiledSource

cp -R "$BUILD_DIR/decompiled/." "$BUILD_DIR/patched/"
for patch_file in \
  "$REPO_ROOT/patches/crash-fixes.patch" \
  "$REPO_ROOT/managed/patches/game/0001-modern-library-boundary-and-bcl.patch" \
  "$REPO_ROOT/managed/patches/game/0002-use-aot-safe-generic-runtime-apis.patch"; do
  echo "+ patch --batch --forward -F 0 -p1 < ${patch_file/#$REPO_ROOT/\$REPO_ROOT}"
  if ! patch --batch --forward -F 0 -p1 -d "$BUILD_DIR/patched" < "$patch_file" \
    >>"$BUILD_DIR/logs/patch.log" 2>&1; then
    echo "error: deterministic patch failed: ${patch_file#$REPO_ROOT/}" >&2
    echo "       inspect <BUILD_DIR>/logs/patch.log" >&2
    exit 1
  fi
done

python3 "$REPO_ROOT/scripts/celeste-managed.py" patch-fmod \
  --root "$BUILD_DIR/patched" \
  --expected-count 490 \
  --output "$ARTIFACT_DIR/fmod-compile-boundary.json"

find "$BUILD_DIR/patched" -maxdepth 1 -type f \( -name '*.csproj' -o -name 'app.config' \) -delete
cp "$REPO_ROOT/managed/templates/Celeste.Modern.csproj" "$BUILD_DIR/patched/Celeste.Modern.csproj"
cp "$REPO_ROOT/managed/templates/Celeste.Content.Modern.csproj" "$BUILD_DIR/patched/Celeste.Content.Modern.csproj"
cp "$REPO_ROOT/managed/templates/Stage3AContentIdentity.cs" "$BUILD_DIR/patched/Stage3AContentIdentity.cs"

grep -Fq '<ProjectReference Include="$(CelesteTvOSRepoRoot)/tvos/FNA.TvOS/FNA.TvOS.csproj"' \
  "$BUILD_DIR/patched/Celeste.Modern.csproj" || {
  echo "error: generated project does not reference FNA.TvOS" >&2
  exit 1
}
if rg -n 'net452|mscorlib\.dll|FNA\.dll|Steamworks' "$BUILD_DIR/patched" --glob '*.csproj' >/dev/null; then
  echo "error: generated modern project retained a legacy framework/game reference" >&2
  exit 1
fi

python3 "$REPO_ROOT/scripts/celeste-managed.py" inventory \
  --root "$BUILD_DIR/patched" \
  --output-dir "$ARTIFACT_DIR" \
  --tvstubs "$REPO_ROOT/native/tvos-symbol-expectations.json" \
  --fna-root "$REPO_ROOT/FNA/src" \
  --stage2-managed-root "$REPO_ROOT/.build/tvos-host/managed"
python3 "$REPO_ROOT/scripts/celeste-managed.py" content-readers \
  --content-root "$GAME_ROOT/Content" \
  --output "$ARTIFACT_DIR/content-reader-inventory.json"
python3 "$REPO_ROOT/scripts/celeste-managed.py" tree-manifest \
  --root "$BUILD_DIR/patched" \
  --kind patched-source \
  --placeholder PATCHED_ROOT \
  --output "$ARTIFACT_DIR/patched-source-manifest.json"
verify_locked_tree "$ARTIFACT_DIR/patched-source-manifest.json" patchedSource

python3 - "$ARTIFACT_DIR/generation-manifest.json" "$TOOL_VERSION" <<'PY'
import json, pathlib, sys
version_lines = [line.strip() for line in sys.argv[2].splitlines() if line.strip()]
pathlib.Path(sys.argv[1]).write_text(json.dumps({
    "schemaVersion": 1,
    "gameRoot": "$CELESTE_GAME_ROOT",
    "gameContentCopied": False,
    "generatedSourceTracked": False,
    "tool": {
        "packageVersion": "8.0.0.7246-preview3",
        "reportedVersion": version_lines,
    },
    "patchOrder": [
        "patches/crash-fixes.patch",
        "managed/patches/game/0001-modern-library-boundary-and-bcl.patch",
        "managed/patches/game/0002-use-aot-safe-generic-runtime-apis.patch",
        "scripts/celeste-managed.py#patch-fmod",
    ],
    "targetFramework": "net10.0-tvos26.5",
    "modernFnaProject": "tvos/FNA.TvOS/FNA.TvOS.csproj",
}, indent=2, sort_keys=True) + "\n")
PY

echo "prepared deterministic Stage 3A managed source"
echo "generated root: <BUILD_DIR> (selected ignored local path)"
echo "artifact root: <ARTIFACT_DIR> (selected ignored local path)"
