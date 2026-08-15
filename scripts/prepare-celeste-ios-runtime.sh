#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
GAME_ROOT="${CELESTE_GAME_ROOT:-}"
RUNTIME_ROOT="$REPO_ROOT/.build/celeste-ios/current"
ARTIFACT_DIR="$REPO_ROOT/artifacts/ios-celeste/current"
CLEAN=0
MARKER=.ios-celeste-runtime

usage() {
  cat <<'EOF'
Usage: scripts/prepare-celeste-ios-runtime.sh [options]

Generate the shared canonical Celeste 1.4.0.0 real-audio tree and derive the
narrow modern-iOS product tree. All game source, Content, and FMOD banks stay
under ignored local directories.

Options:
  --game-root DIR      supported user-owned Celeste installation
  --runtime-root DIR   ignored output (default: .build/celeste-ios/current)
  --artifact-dir DIR   ignored manifests (default: artifacts/ios-celeste/current)
  --clean              replace only marked prior outputs
  -h, --help           show this help
EOF
}

repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
safe_replace() {
  local target="$1"
  if [[ -e "$target" ]]; then
    [[ "$CLEAN" -eq 1 && -f "$target/$MARKER" ]] || {
      echo "error: refusing to replace unmarked/non-clean output: ${target/#$REPO_ROOT/\$REPO_ROOT}" >&2
      exit 1
    }
    find "$target" -depth -delete
  fi
}

while (($#)); do
  case "$1" in
    --game-root) [[ $# -ge 2 ]] || exit 2; GAME_ROOT="$2"; shift 2 ;;
    --runtime-root) [[ $# -ge 2 ]] || exit 2; RUNTIME_ROOT="$(repo_path "$2")"; shift 2 ;;
    --artifact-dir) [[ $# -ge 2 ]] || exit 2; ARTIFACT_DIR="$(repo_path "$2")"; shift 2 ;;
    --clean) CLEAN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ -n "$GAME_ROOT" && -d "$GAME_ROOT" ]] || {
  echo "error: --game-root or CELESTE_GAME_ROOT must name a supported installation" >&2; exit 1;
}
for command in dotnet git python3 cp shasum; do
  command -v "$command" >/dev/null || { echo "error: missing tool: $command" >&2; exit 1; }
done
[[ "$(dotnet --version)" == "10.0.302" ]] || { echo "error: dotnet 10.0.302 is required" >&2; exit 1; }
for root in "$RUNTIME_ROOT" "$ARTIFACT_DIR"; do
  case "$root" in "$REPO_ROOT"/*) relative="${root#$REPO_ROOT/}" ;; *) echo "error: output must be below repository" >&2; exit 1 ;; esac
  git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative/$MARKER" || {
    echo "error: output is not ignored: \$REPO_ROOT/$relative" >&2; exit 1;
  }
done
[[ "$RUNTIME_ROOT" != "$ARTIFACT_DIR" ]] || { echo "error: output roots must differ" >&2; exit 1; }

safe_replace "$RUNTIME_ROOT"
safe_replace "$ARTIFACT_DIR"
mkdir -p "$RUNTIME_ROOT" "$ARTIFACT_DIR"
touch "$RUNTIME_ROOT/$MARKER" "$ARTIFACT_DIR/$MARKER"

# Reuse the accepted generator and transforms exactly through Stage 3C.
"$REPO_ROOT/scripts/prepare-celeste-tvos-stage3c.sh" \
  --game-root "$GAME_ROOT" \
  --runtime-root "$RUNTIME_ROOT/shared-stage3c" \
  --artifact-dir "$ARTIFACT_DIR/shared-stage3c"

mkdir -p "$RUNTIME_ROOT/shared-stage5b/managed" "$RUNTIME_ROOT/shared-stage6/managed"
cp -R "$RUNTIME_ROOT/shared-stage3c/managed/." "$RUNTIME_ROOT/shared-stage5b/managed/"
original="$RUNTIME_ROOT/shared-stage3c/stage3b/stage3a-build/decompiled"
python3 "$REPO_ROOT/scripts/celeste-stage5b.py" \
  --root "$RUNTIME_ROOT/shared-stage5b/managed" \
  --original-root "$original" \
  --templates "$REPO_ROOT/managed/templates" \
  --policy "$REPO_ROOT/managed/celeste-stage5b-policy.json" \
  --output "$ARTIFACT_DIR/shared-stage5b.json"

cp -R "$RUNTIME_ROOT/shared-stage5b/managed/." "$RUNTIME_ROOT/shared-stage6/managed/"
python3 "$REPO_ROOT/scripts/celeste-stage6.py" \
  --root "$RUNTIME_ROOT/shared-stage6/managed" \
  --templates "$REPO_ROOT/managed/templates" \
  --policy "$REPO_ROOT/managed/celeste-stage6-policy.json" \
  --mode realAudio \
  --output "$ARTIFACT_DIR/shared-stage6.json"

mkdir -p "$RUNTIME_ROOT/managed" "$RUNTIME_ROOT/content/Content" "$RUNTIME_ROOT/banks/Content/FMOD/Desktop"
cp -R "$RUNTIME_ROOT/shared-stage6/managed/." "$RUNTIME_ROOT/managed/"
cp -cR "$RUNTIME_ROOT/shared-stage3c/content/Content/." "$RUNTIME_ROOT/content/Content/"

# Resolve the validated package root; wrapper layouts are never searched
# recursively or inferred from an executable name.
input_manifest="$ARTIFACT_DIR/shared-stage3c/stage3b/input-manifest.json"
resolved="$(python3 - "$input_manifest" <<'PY'
import json,pathlib,sys
value=json.loads(pathlib.Path(sys.argv[1]).read_text())["resolvedRootRelative"]
path=pathlib.PurePosixPath(value)
if value == ".": print("")
elif path.is_absolute() or any(x in ("", ".", "..") for x in path.parts): raise SystemExit("unsafe resolved root")
else: print(value)
PY
)"
resolved_game="$GAME_ROOT"
[[ -z "$resolved" ]] || resolved_game="$GAME_ROOT/$resolved"
for bank in "Master Bank.bank" "Master Bank.strings.bank" music.bank sfx.bank ui.bank dlc_music.bank dlc_sfx.bank; do
  source_bank="$resolved_game/Content/FMOD/Desktop/$bank"
  [[ -f "$source_bank" && ! -L "$source_bank" ]] || { echo "error: required Celeste FMOD bank is missing" >&2; exit 1; }
  cp -c "$source_bank" "$RUNTIME_ROOT/banks/Content/FMOD/Desktop/$bank"
done

python3 "$REPO_ROOT/scripts/celeste-ios-stage24c1.py" \
  --root "$RUNTIME_ROOT/managed" \
  --templates "$REPO_ROOT/managed/templates" \
  --output "$ARTIFACT_DIR/ios-managed.json"

python3 "$REPO_ROOT/scripts/celeste-ios-stage24c2.py" \
  --root "$RUNTIME_ROOT/managed" \
  --templates "$REPO_ROOT/managed/templates" \
  --output "$ARTIFACT_DIR/ios-managed-stage24c2.json"

python3 - "$resolved_game/Content" "$RUNTIME_ROOT/content/Content" "$RUNTIME_ROOT/banks/Content/FMOD/Desktop" "$ARTIFACT_DIR/content.json" <<'PY'
import hashlib,json,pathlib,sys
source, staged, banks, output = map(pathlib.Path, sys.argv[1:])
def aggregate(entries):
    h=hashlib.sha256(); total=0; records=[]
    for rel,path in entries:
        data=path.read_bytes(); sha=hashlib.sha256(data).hexdigest(); total+=len(data)
        h.update(rel.encode()+b"\0"+str(len(data)).encode()+b"\0"+sha.encode()+b"\n")
        records.append((rel,len(data),sha))
    return len(records),total,h.hexdigest()
source_entries=sorted((p.relative_to(source).as_posix(),p) for p in source.rglob('*') if p.is_file())
staged_entries=[(p.relative_to(staged).as_posix(),p) for p in staged.rglob('*') if p.is_file()]
staged_entries += [("FMOD/Desktop/"+p.name,p) for p in banks.iterdir() if p.is_file()]
staged_entries.sort()
s=aggregate(source_entries); d=aggregate(staged_entries)
expected=(1216,1158665183,"30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46")
if s != expected or d != expected: raise SystemExit(f"error: canonical Content mismatch: source={s}, staged={d}")
output.write_text(json.dumps({"schemaVersion":1,"fileCount":d[0],"totalBytes":d[1],"logicalSha256":d[2],"banks":7},indent=2,sort_keys=True)+"\n")
PY

python3 - "$input_manifest" "$ARTIFACT_DIR/preparation-result.json" <<'PY'
import json,pathlib,sys
source=json.loads(pathlib.Path(sys.argv[1]).read_text())
pathlib.Path(sys.argv[2]).write_text(json.dumps({
 "schemaVersion":1,"profileId":source["profileId"],"canonicalClass":source["canonicalClass"],
 "sharedGenerator":True,"separateIOSGenerator":False,"generatedSourceTracked":False,
 "contentTracked":False,"banksTracked":False
},indent=2,sort_keys=True)+"\n")
PY

echo "PASS: prepared ignored canonical Celeste runtime for modern iOS"
echo "profile: $(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["profileId"])' "$input_manifest")"
