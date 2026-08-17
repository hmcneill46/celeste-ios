#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
PROJECT="$REPO_ROOT/modern-ios/CelesteIOSRuntimeHost/CelesteIOSRuntimeHost.csproj"
OUTPUT_DIR="$REPO_ROOT/artifacts/ios-celeste/device"
CLEAN=0
VERBOSE=0
MARKER=.ios-celeste-product-output

usage() {
  cat <<'EOF'
Usage: scripts/build-ios-celeste.sh [options]

Build the experimental modern-iOS Celeste product for a physical arm64 iPhone or iPad.
Run prepare-celeste-ios-runtime.sh and prepare-fmod-ios.sh first.

Options:
  --output-dir DIR  ignored output (default: artifacts/ios-celeste/device)
  --clean           replace only a marked prior output
  --verbose         stream full dotnet output
  -h, --help        show this help
EOF
}

while (($#)); do
  case "$1" in
    --output-dir) [[ $# -ge 2 ]] || exit 2; OUTPUT_DIR="$2"; shift 2 ;;
    --clean) CLEAN=1; shift ;;
    --verbose) VERBOSE=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done
[[ "$OUTPUT_DIR" == /* ]] || OUTPUT_DIR="$REPO_ROOT/$OUTPUT_DIR"
"$REPO_ROOT/scripts/check-ios-host.sh"
for required in \
  "$REPO_ROOT/.build/ios-host/native-manifest.json" \
  "$REPO_ROOT/.build/ios-host/fmod/fmod-ios-manifest.json" \
  "$REPO_ROOT/.build/celeste-ios/current/managed/Celeste.Modern.csproj" \
  "$REPO_ROOT/artifacts/ios-celeste/current/ios-managed.json" \
  "$REPO_ROOT/artifacts/ios-celeste/current/ios-managed-stage24c2.json" \
  "$REPO_ROOT/artifacts/ios-celeste/current/ios-managed-stage24d2.json"; do
  [[ -f "$required" ]] || { echo "error: prepared iOS Celeste input is incomplete" >&2; exit 1; }
done
if [[ -e "$OUTPUT_DIR" ]]; then
  ((CLEAN)) || { echo "error: output exists; use --clean" >&2; exit 1; }
  [[ -f "$OUTPUT_DIR/$MARKER" ]] || { echo "error: refusing unmarked output" >&2; exit 1; }
  find "$OUTPUT_DIR" -depth -delete
fi
mkdir -p "$OUTPUT_DIR/logs"
touch "$OUTPUT_DIR/$MARKER"

start="$(date +%s)"
phase() { printf '[%s] %s (elapsed %ss; free %s)\n' "$(date '+%H:%M:%S')" "$1" "$(( $(date +%s)-start ))" "$(df -h "$REPO_ROOT" | awk 'NR==2 {print $4}')"; }
phase "Publishing canonical Celeste for ios-arm64 (full AOT/trim/LLVM)"
log="$OUTPUT_DIR/logs/publish.log"
args=(publish "$PROJECT" -c Release -r ios-arm64 --self-contained true
  -p:IOSProductMode=Celeste -p:EnableFmodDeviceFoundation=true
  -p:CelesteAppleRepoRoot="$REPO_ROOT"
  -p:ArchiveOnBuild=false -p:UseInterpreter=false -p:RunAOTCompilation=true
  -p:MtouchLink=Full -p:TrimMode=full -p:MtouchUseLlvm=true -p:PublishTrimmed=true)
if ((VERBOSE)); then
  dotnet "${args[@]}" 2>&1 | tee "$log"
else
  dotnet "${args[@]}" >"$log" 2>&1 &
  child=$!
  while kill -0 "$child" 2>/dev/null; do
    for _ in {1..60}; do kill -0 "$child" 2>/dev/null || break; sleep 1; done
    kill -0 "$child" 2>/dev/null && phase "Still publishing Celeste"
  done
  if ! wait "$child"; then tail -80 "$log" >&2; exit 1; fi
fi

app="$(find "$REPO_ROOT/modern-ios/CelesteIOSRuntimeHost/bin/Release/net10.0-ios26.5/ios-arm64" -maxdepth 2 -type d -name '*.app' -print -quit)"
[[ -n "$app" ]] || { echo "error: publish produced no app" >&2; exit 1; }
mkdir -p "$OUTPUT_DIR/publish" "$OUTPUT_DIR/ipa/Payload"
cp -R "$app" "$OUTPUT_DIR/publish/"
published="$OUTPUT_DIR/publish/$(basename "$app")"
cp -R "$published" "$OUTPUT_DIR/ipa/Payload/"
(cd "$OUTPUT_DIR/ipa" && /usr/bin/zip -qry "$OUTPUT_DIR/Celeste-modern-iOS.ipa" Payload)
phase "Verifying package"
python3 "$REPO_ROOT/scripts/verify-ios-package.py" --app "$published" --lane device --product celeste \
  --output "$OUTPUT_DIR/package-verification.json"

elapsed="$(( $(date +%s)-start ))"
python3 - "$OUTPUT_DIR/build-manifest.json" "$OUTPUT_DIR/Celeste-modern-iOS.ipa" "$elapsed" \
  "$REPO_ROOT/artifacts/ios-celeste/current/preparation-result.json" \
  "$REPO_ROOT/artifacts/ios-celeste/current/ios-managed-stage24d2.json" <<'PY'
import hashlib,json,pathlib,sys
out,ipa,elapsed,prep,managed=pathlib.Path(sys.argv[1]),pathlib.Path(sys.argv[2]),int(sys.argv[3]),pathlib.Path(sys.argv[4]),pathlib.Path(sys.argv[5])
p=json.loads(prep.read_text()); m=json.loads(managed.read_text())
out.write_text(json.dumps({"schemaVersion":1,"product":"Celeste","rid":"ios-arm64","configuration":"Release",
 "profileId":p["profileId"],"canonicalClass":p["canonicalClass"],"fullAOT":True,"fullTrim":True,
 "useInterpreter":False,"jit":False,"elapsedSeconds":elapsed,"ipaBytes":ipa.stat().st_size,
 "ipaSha256":hashlib.sha256(ipa.read_bytes()).hexdigest(),"generated":m["output"]},indent=2,sort_keys=True)+"\n")
PY
phase "PASS: experimental modern-iOS Celeste product"
