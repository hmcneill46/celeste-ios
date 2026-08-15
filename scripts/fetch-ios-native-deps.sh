#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
LOCK_FILE="$REPO_ROOT/native/ios-dependencies.lock.json"
BUILD_DIR="$REPO_ROOT/.build/ios-native"
CACHE_DIR=""
CLEAN=0

usage() {
  cat <<'EOF'
Usage: scripts/fetch-ios-native-deps.sh [options]

Fetch the immutable open-source native inputs for the modern iOS foundation.

Options:
  --build-dir DIR  Generated work root (default: .build/ios-native)
  --cache-dir DIR  Optional reusable bare Git-object cache
  --clean          Replace only the generated sources directory
  -h, --help       Show this help
EOF
}

while (($#)); do
  case "$1" in
    --build-dir) [[ $# -ge 2 ]] || exit 2; BUILD_DIR="$2"; shift 2 ;;
    --cache-dir) [[ $# -ge 2 ]] || exit 2; CACHE_DIR="$2"; shift 2 ;;
    --clean) CLEAN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

for tool in git python3; do
  command -v "$tool" >/dev/null || { echo "error: missing tool: $tool" >&2; exit 1; }
done

BUILD_DIR="$(python3 -c 'import os,sys; print(os.path.realpath(sys.argv[1]))' "$BUILD_DIR")"
SOURCES_DIR="$BUILD_DIR/sources"
[[ -n "$CACHE_DIR" ]] || CACHE_DIR="$BUILD_DIR/git-cache"
CACHE_DIR="$(python3 -c 'import os,sys; print(os.path.realpath(sys.argv[1]))' "$CACHE_DIR")"

if ((CLEAN)) && [[ -e "$SOURCES_DIR" ]]; then
  [[ -f "$BUILD_DIR/.ios-native-work" ]] || { echo "error: refusing unmarked build root" >&2; exit 1; }
  find "$SOURCES_DIR" -depth -delete
fi
mkdir -p "$BUILD_DIR" "$SOURCES_DIR" "$CACHE_DIR" "$BUILD_DIR/logs"
touch "$BUILD_DIR/.ios-native-work"
export GIT_TERMINAL_PROMPT=0

python3 - "$LOCK_FILE" "$SOURCES_DIR" "$CACHE_DIR" "$BUILD_DIR/source-state.json" <<'PY'
import hashlib, json, pathlib, re, shutil, subprocess, sys

lock_path, sources, cache_root, output = map(pathlib.Path, sys.argv[1:])
lock = json.loads(lock_path.read_text())
state = {"schemaVersion": 1, "dependencies": []}

def call(args):
    print("+", " ".join(map(str, args)))
    subprocess.check_call([str(x) for x in args])

for dep in lock["dependencies"]:
    revision = dep["revision"]
    if not re.fullmatch(r"[0-9a-f]{40}", revision):
        raise SystemExit(f"non-immutable revision: {dep['name']}")
    destination = sources / dep["path"]
    cache = cache_root / (hashlib.sha256(dep["url"].encode()).hexdigest() + ".git")
    if not cache.is_dir():
        call(["git", "clone", "--mirror", dep["url"], cache])
    if subprocess.call(["git", "-C", cache, "cat-file", "-e", revision + "^{commit}"],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL):
        call(["git", "-C", cache, "fetch", "--no-tags", "origin", revision])
    if destination.exists() and not (destination / ".git").is_dir():
        if destination.is_dir() and not any(destination.iterdir()):
            destination.rmdir()
        else:
            raise SystemExit(f"non-Git path blocks {dep['name']}: {destination}")
    if not (destination / ".git").is_dir():
        destination.parent.mkdir(parents=True, exist_ok=True)
        call(["git", "clone", "--no-checkout", "--reference-if-able", cache,
              dep["url"], destination])
        call(["git", "-C", destination, "checkout", "--detach", revision])
    origin = subprocess.check_output(
        ["git", "-C", destination, "remote", "get-url", "origin"], text=True).strip()
    head = subprocess.check_output(
        ["git", "-C", destination, "rev-parse", "HEAD"], text=True).strip()
    dirty = subprocess.check_output(
        ["git", "-C", destination, "status", "--porcelain", "--untracked-files=all"],
        text=True).strip()
    if (origin, head, dirty) != (dep["url"], revision, ""):
        raise SystemExit(f"checkout does not exactly match lock: {dep['name']}")
    for license_path in dep.get("licensePaths", []):
        if not (destination / license_path).is_file():
            raise SystemExit(f"missing license: {dep['name']}/{license_path}")
    state["dependencies"].append({"name": dep["name"], "origin": origin, "revision": head})

output.write_text(json.dumps(state, indent=2, sort_keys=True) + "\n")
print(f"Fetched and verified {len(state['dependencies'])} immutable iOS inputs.")
PY
