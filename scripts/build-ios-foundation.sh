#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
PROJECT="$REPO_ROOT/modern-ios/CelesteIOSRuntimeHost/CelesteIOSRuntimeHost.csproj"
LANE=""
OUTPUT_DIR=""
CLEAN=0

usage() {
  cat <<'EOF'
Usage: scripts/build-ios-foundation.sh (--simulator | --device) [options]

Build the modern iOS FNA foundation. This is intentionally not a
playable Celeste build: it proves the production-native/runtime boundary only.

Options:
  --simulator       arm64 Apple-silicon simulator; explicit no-FMOD lane
  --device          arm64 physical iPhone; requires prepared FMOD and signing
  --output-dir DIR  ignored publish destination
  --clean           replace only a marked prior destination
  -h, --help        show this help
EOF
}

while (($#)); do
  case "$1" in
    --simulator|--device)
      [[ -z "$LANE" ]] || { echo "error: choose one lane" >&2; exit 2; }
      LANE="${1#--}"; shift ;;
    --output-dir) [[ $# -ge 2 ]] || exit 2; OUTPUT_DIR="$2"; shift 2 ;;
    --clean) CLEAN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done
[[ -n "$LANE" ]] || { usage >&2; exit 2; }
RID=iossimulator-arm64
[[ "$LANE" == device ]] && RID=ios-arm64
[[ -n "$OUTPUT_DIR" ]] || OUTPUT_DIR="$REPO_ROOT/artifacts/ios-host/$LANE"
[[ "$OUTPUT_DIR" == /* ]] || OUTPUT_DIR="$REPO_ROOT/$OUTPUT_DIR"

"$REPO_ROOT/scripts/check-ios-host.sh"
[[ -f "$REPO_ROOT/.build/ios-host/native-manifest.json" ]] || {
  echo "error: run scripts/prepare-ios-foundation.sh first" >&2; exit 1;
}
if [[ "$LANE" == device ]]; then
  [[ -f "$REPO_ROOT/.build/ios-host/fmod/fmod-ios-manifest.json" ]] || {
    echo "error: physical device FMOD stage is missing; run scripts/prepare-fmod-ios.sh" >&2; exit 1;
  }
fi
if [[ -e "$OUTPUT_DIR" ]]; then
  ((CLEAN)) || { echo "error: output exists; use --clean" >&2; exit 1; }
  [[ -f "$OUTPUT_DIR/.ios-foundation-output" ]] || { echo "error: refusing unmarked output" >&2; exit 1; }
  find "$OUTPUT_DIR" -depth -delete
fi
mkdir -p "$OUTPUT_DIR"
touch "$OUTPUT_DIR/.ios-foundation-output"

verb=publish
[[ "$LANE" == simulator ]] && verb=build
arguments=("$verb" "$PROJECT" -c Release -r "$RID" --self-contained true
  -p:ArchiveOnBuild=false -p:UseInterpreter=false -p:RunAOTCompilation=true
  -p:MtouchLink=Full -p:TrimMode=full -p:MtouchUseLlvm=true -p:PublishTrimmed=true)
if [[ "$LANE" == device ]]; then
  arguments+=(-p:EnableFmodDeviceFoundation=true -p:PublishDir="$OUTPUT_DIR/publish/")
fi
start="$(date +%s)"
dotnet "${arguments[@]}"
elapsed="$(( $(date +%s) - start ))"
built_app="$(find "$REPO_ROOT/modern-ios/CelesteIOSRuntimeHost/bin/Release/net10.0-ios26.5/$RID" \
  -maxdepth 2 -type d -name '*.app' -print -quit)"
[[ -n "$built_app" ]] || { echo "error: $LANE build produced no .app" >&2; exit 1; }
mkdir -p "$OUTPUT_DIR/publish"
rm_target="$OUTPUT_DIR/publish/$(basename "$built_app")"
[[ ! -e "$rm_target" ]] || find "$rm_target" -depth -delete
cp -R "$built_app" "$OUTPUT_DIR/publish/"
app="$(find "$OUTPUT_DIR/publish" -maxdepth 2 -type d -name '*.app' -print -quit)"
[[ -n "$app" ]] || { echo "error: publish produced no .app" >&2; exit 1; }
python3 - "$OUTPUT_DIR/build-manifest.json" "$LANE" "$RID" "$elapsed" "$app" <<'PY'
import hashlib, json, pathlib, sys
output, lane, rid, elapsed, app = pathlib.Path(sys.argv[1]), sys.argv[2], sys.argv[3], int(sys.argv[4]), pathlib.Path(sys.argv[5])
records = []
for path in sorted(x for x in app.rglob("*") if x.is_file() and not x.is_symlink()):
    records.append({"path": path.relative_to(app).as_posix(), "size": path.stat().st_size,
                    "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
output.write_text(json.dumps({"schemaVersion": 1, "lane": lane, "rid": rid, "configuration": "Release",
                              "minimumIOS": "15.0", "fullAOT": True, "fullTrim": True,
                              "useInterpreter": False, "elapsedSeconds": elapsed,
                              "appFileCount": len(records), "appFiles": records}, indent=2, sort_keys=True) + "\n")
PY
echo "PASS: modern iOS foundation $LANE build ($elapsed seconds)"
