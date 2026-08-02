#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)"
LOCK_FILE="$REPO_ROOT/native/tvos-dependencies.lock.json"
BUILD_DIR="$REPO_ROOT/.build/tvos-native"
CACHE_DIR=""
CLEAN=0

usage() {
  cat <<'EOF'
Usage: scripts/fetch-tvos-deps.sh [options]

Fetch every Stage 1 native source at the immutable revision in
native/tvos-dependencies.lock.json. The command installs nothing and never
updates a moving branch.

Options:
  --build-dir DIR  Generated-work directory (default: .build/tvos-native)
  --cache-dir DIR  Optional reusable bare Git-object cache
                   (default: DIR/git-cache)
  --clean          Remove only DIR/sources before fetching
  -h, --help       Show this help

The command rejects a pre-existing checkout if its origin or HEAD differs from
the lock, or if its changes are not exactly the locked patch set.
GIT_TERMINAL_PROMPT is disabled.
EOF
}

while (($#)); do
  case "$1" in
    --build-dir)
      (($# >= 2)) || { echo "error: --build-dir requires a value" >&2; exit 2; }
      BUILD_DIR="$2"
      shift 2
      ;;
    --clean)
      CLEAN=1
      shift
      ;;
    --cache-dir)
      (($# >= 2)) || { echo "error: --cache-dir requires a value" >&2; exit 2; }
      CACHE_DIR="$2"
      shift 2
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

for tool in awk git python3 shasum; do
  command -v "$tool" >/dev/null 2>&1 || { echo "error: required tool not found: $tool" >&2; exit 1; }
done

BUILD_DIR="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$BUILD_DIR")"
SOURCES_DIR="$BUILD_DIR/sources"
LOG_DIR="$BUILD_DIR/logs"
if [[ -z "$CACHE_DIR" ]]; then
  CACHE_DIR="$BUILD_DIR/git-cache"
fi
CACHE_DIR="$(python3 -c 'import os,sys; print(os.path.abspath(sys.argv[1]))' "$CACHE_DIR")"

if ((CLEAN)); then
  case "$SOURCES_DIR" in
    /|/sources|"")
      echo "error: refusing unsafe sources path: $SOURCES_DIR" >&2
      exit 1
      ;;
  esac
  rm -rf -- "$SOURCES_DIR"
fi
mkdir -p -- "$SOURCES_DIR" "$LOG_DIR" "$CACHE_DIR"
LOG_FILE="$LOG_DIR/fetch.log"
: >"$LOG_FILE"
exec > >(tee -a "$LOG_FILE") 2>&1

export GIT_TERMINAL_PROMPT=0
export GIT_CONFIG_NOSYSTEM=1

echo "Stage 1 dependency fetch"
echo "lock: $LOCK_FILE"
echo "sources: $SOURCES_DIR"
echo "Git object cache: $CACHE_DIR"
echo "git: $(git --version)"

python3 - "$LOCK_FILE" <<'PY' >"$BUILD_DIR/dependencies.tsv"
import json, re, sys
data = json.load(open(sys.argv[1], encoding="utf-8"))
for dep in data["dependencies"]:
    rev = dep["revision"]
    if not re.fullmatch(r"[0-9a-f]{40}", rev):
        raise SystemExit(f"non-immutable revision for {dep['name']}: {rev}")
    print("\t".join((dep["name"], dep["path"], dep["url"], rev, "|".join(dep.get("patches", [])))))
PY

while IFS=$'\t' read -r name relative_path url revision patch_list; do
  destination="$SOURCES_DIR/$relative_path"
  cache_key="$(printf '%s' "$url" | shasum -a 256 | awk '{print $1}')"
  cache="$CACHE_DIR/$cache_key.git"
  new_checkout=0
  echo
  echo "[$name] $url @ $revision"

  if [[ -e "$destination" && ! -d "$destination/.git" ]]; then
    if [[ -d "$destination" && -z "$(find "$destination" -mindepth 1 -maxdepth 1 -print -quit)" ]]; then
      rmdir -- "$destination"
    else
      echo "error: $destination exists but is not a Git checkout" >&2
      exit 1
    fi
  fi

  if [[ ! -d "$cache" ]]; then
    set -x
    git clone --mirror "$url" "$cache"
    set +x
  fi
  cache_origin="$(git -C "$cache" remote get-url origin)"
  [[ "$cache_origin" == "$url" ]] || {
    echo "error: cached origin mismatch for $name: $cache_origin" >&2
    exit 1
  }
  if ! git -C "$cache" cat-file -e "$revision^{commit}" 2>/dev/null; then
    set -x
    git -C "$cache" fetch --no-tags origin "$revision"
    set +x
  fi

  if [[ ! -d "$destination/.git" ]]; then
    mkdir -p -- "$(dirname -- "$destination")"
    set -x
    git clone --no-checkout --origin origin --reference-if-able "$cache" "$url" "$destination"
    set +x
    new_checkout=1
  fi

  actual_origin="$(git -C "$destination" remote get-url origin)"
  if [[ "$actual_origin" != "$url" ]]; then
    echo "error: origin mismatch for $name" >&2
    echo "  locked: $url" >&2
    echo "  actual: $actual_origin" >&2
    exit 1
  fi

  if ! git -C "$destination" cat-file -e "$revision^{commit}" 2>/dev/null; then
    set -x
    git -C "$destination" fetch --no-tags origin "$revision"
    set +x
  fi
  resolved="$(git -C "$destination" rev-parse "$revision^{commit}")"
  [[ "$resolved" == "$revision" ]] || { echo "error: revision did not resolve exactly for $name" >&2; exit 1; }

  if ((new_checkout)); then
    set -x
    git -C "$destination" checkout --detach "$revision"
    set +x
  elif git -C "$destination" rev-parse --verify HEAD >/dev/null 2>&1; then
    head_revision="$(git -C "$destination" rev-parse HEAD)"
    [[ "$head_revision" == "$revision" ]] || {
      echo "error: existing checkout HEAD mismatch for $name: $head_revision" >&2
      echo "rerun with --clean to create clean locked sources" >&2
      exit 1
    }
    dirty="$(git -C "$destination" status --porcelain --untracked-files=all)"
    if [[ -n "$dirty" ]]; then
      [[ -n "$patch_list" ]] || {
        echo "error: existing checkout is dirty: $destination" >&2
        echo "rerun with --clean to create clean locked sources" >&2
        exit 1
      }
      python3 - "$destination" "$REPO_ROOT" "$patch_list" <<'PY'
import pathlib, subprocess, sys
checkout, repo = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
patches = [repo / path for path in sys.argv[3].split("|") if path]
reversed_patches = []
try:
    for patch in reversed(patches):
        subprocess.check_call(["git", "-C", checkout, "apply", "--reverse", patch])
        reversed_patches.append(patch)
    remaining = subprocess.check_output(
        ["git", "-C", checkout, "status", "--porcelain", "--untracked-files=all"], text=True
    ).strip()
finally:
    for patch in reversed(reversed_patches):
        subprocess.check_call(["git", "-C", checkout, "apply", patch])
if remaining:
    raise SystemExit(f"changes remain after reversing locked patches: {remaining}")
PY
      echo "Existing checkout has exactly the locked patch set."
    fi
  else
    echo "error: existing checkout has no HEAD: $destination" >&2
    exit 1
  fi
done <"$BUILD_DIR/dependencies.tsv"

python3 - "$LOCK_FILE" "$SOURCES_DIR" "$BUILD_DIR/source-state.json" <<'PY'
import json, os, subprocess, sys
lock_path, sources, output = sys.argv[1:]
lock = json.load(open(lock_path, encoding="utf-8"))
state = {"schemaVersion": 1, "dependencies": []}
for dep in lock["dependencies"]:
    root = os.path.join(sources, dep["path"])
    head = subprocess.check_output(["git", "-C", root, "rev-parse", "HEAD"], text=True).strip()
    origin = subprocess.check_output(["git", "-C", root, "remote", "get-url", "origin"], text=True).strip()
    if head != dep["revision"] or origin != dep["url"]:
        raise SystemExit(f"post-fetch verification failed for {dep['name']}")
    for license_path in dep.get("licensePaths", []):
        if not os.path.isfile(os.path.join(root, license_path)):
            raise SystemExit(f"missing locked license for {dep['name']}: {license_path}")
    state["dependencies"].append({"name": dep["name"], "origin": origin, "revision": head})
with open(output, "w", encoding="utf-8") as f:
    json.dump(state, f, indent=2, sort_keys=True)
    f.write("\n")
PY

echo
echo "Fetched and verified $(wc -l <"$BUILD_DIR/dependencies.tsv" | tr -d ' ') immutable checkouts."
echo "State: $BUILD_DIR/source-state.json"
