#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/verify-celeste-tvos-stage6.sh [options]

Verify locked Stage 6 generation, dual-slot policy, prior-stage isolation,
privacy manifest, and optionally a built simulator/device app.

Options:
  --runtime-root DIR          Stage 6 ignored runtime root
                              (default: .build/celeste-runtime/stage6-current)
  --artifact-dir DIR          Stage 6 ignored manifests
                              (default: artifacts/celeste-runtime/stage6-current)
  --compare-artifact-dir DIR  Independent clean generation manifests
  --app DIR                   Validate a built app bundle
  --platform NAME             simulator or device (required with --app)
  --skip-toolchain            Skip SDK/workload checks
  -h, --help                  Show help
USAGE
}

readonly BASELINE_COMMIT=2e17b013e7af46c993858ee6ef3ff1a13e4d6784
readonly STAGE1_SHA=6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
RUNTIME_ROOT="$REPO_ROOT/.build/celeste-runtime/stage6-current"
ARTIFACT_DIR="$REPO_ROOT/artifacts/celeste-runtime/stage6-current"
COMPARE_ARTIFACT_DIR=""
APP_DIR=""
PLATFORM=""
SKIP_TOOLCHAIN=0
repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --runtime-root) RUNTIME_ROOT="$(repo_path "$2")"; shift 2 ;;
    --artifact-dir) ARTIFACT_DIR="$(repo_path "$2")"; shift 2 ;;
    --compare-artifact-dir) COMPARE_ARTIFACT_DIR="$(repo_path "$2")"; shift 2 ;;
    --app) APP_DIR="$(repo_path "$2")"; shift 2 ;;
    --platform) PLATFORM="$2"; shift 2 ;;
    --skip-toolchain) SKIP_TOOLCHAIN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done
for command in dotnet git python3 plutil shasum; do command -v "$command" >/dev/null || { echo "error: missing tool: $command" >&2; exit 1; }; done
if [[ -n "$APP_DIR" ]]; then
  for command in xcrun codesign nm; do command -v "$command" >/dev/null || { echo "error: missing app-verification tool: $command" >&2; exit 1; }; done
fi
if [[ "$SKIP_TOOLCHAIN" -eq 0 ]]; then
  [[ "$(dotnet --version)" == 10.0.302 ]] || { echo "error: dotnet 10.0.302 required" >&2; exit 1; }
  workloads="$(dotnet workload list)"
  grep -Fq 'Workload version: 10.0.302.0' <<<"$workloads" || { echo "error: workload set changed" >&2; exit 1; }
fi
[[ -f "$RUNTIME_ROOT/.stage6-runtime-preparation" && -f "$ARTIFACT_DIR/.stage6-runtime-preparation" ]] || { echo "error: Stage 6 outputs missing" >&2; exit 1; }

python3 - "$REPO_ROOT" "$RUNTIME_ROOT" "$ARTIFACT_DIR" "$STAGE1_SHA" <<'PY'
import hashlib, json, pathlib, sys
repo, runtime, artifacts = map(pathlib.Path, sys.argv[1:4])
stage1 = sys.argv[4]
policy = json.loads((repo / "managed/celeste-stage6-policy.json").read_text())
storage = policy["storage"]
if (storage["hardTotalBudgetBytes"] != 262144 or storage["hardEnvelopeBudgetBytes"] != 126976 or
    storage["hardCompressedEntryBudgetBytes"] != 98304 or storage["formatVersion"] != 2 or
    storage["previousFormatVersion"] != 1 or storage["legacyMigrationVersion"] != 0 or
    storage["compression"] != {"algorithmId":1,"name":"zlib","level":9,"scope":"independent-logical-entry"}):
    raise SystemExit("error: Stage 6 format or budget lock changed")
files = policy["writableFiles"]
if [item["logicalName"] for item in files] != ["settings", "0", "1", "2"] or not all(item["durable"] for item in files):
    raise SystemExit("error: durable writable-file allow-list changed")
if [item["maximumPayloadBytes"] for item in files] != [65536, 262144, 262144, 262144]:
    raise SystemExit("error: v2 uncompressed serializer safety limits changed")
for tree, mode in (("noaudio", "noAudio"), ("audio", "realAudio")):
    manifest = json.loads((artifacts / f"{tree}-generated-manifest.json").read_text())
    if manifest["mode"] != mode or manifest["allowListedLogicalNames"] != ["settings", "0", "1", "2"]:
        raise SystemExit(f"error: {tree} generation manifest changed")
    if {key: manifest["output"][key] for key in ("fileCount", "logicalSha256")} != policy["generatedOutputs"][mode]:
        raise SystemExit(f"error: {tree} generated output does not match the Stage 6 lock")
    root = runtime / tree / "managed"
    aggregate, count = hashlib.sha256(), 0
    for path in sorted(root.rglob("*"), key=lambda item: item.relative_to(root).as_posix()):
        if not path.is_file() or any(part in {"bin", "obj"} for part in path.relative_to(root).parts): continue
        relative = path.relative_to(root).as_posix(); digest = hashlib.sha256(path.read_bytes()).hexdigest(); count += 1
        aggregate.update(relative.encode() + b"\0" + digest.encode() + b"\n")
    if count != manifest["output"]["fileCount"] or aggregate.hexdigest() != manifest["output"]["logicalSha256"]:
        raise SystemExit(f"error: {tree} generated source changed after manifest")
    project = (root / "Celeste.Modern.csproj").read_text()
    userio = (root / "Celeste/UserIO.cs").read_text()
    if "TVOS_STAGE6" not in project or "TvOSStage6PersistenceHooks" not in userio:
        raise SystemExit(f"error: {tree} lacks Stage 6 hooks")
stage1_manifest = json.loads((repo / ".build/tvos-host/normalized-manifest.json").read_text())
if stage1_manifest.get("logicalSetSha256") != stage1:
    raise SystemExit("error: Stage 1 logical hash changed")
print("PASS: Stage 6 locked generated trees, allow-list, and Stage 1 hash")
PY

if [[ -n "$COMPARE_ARTIFACT_DIR" ]]; then
  for name in noaudio-generated-manifest.json audio-generated-manifest.json preparation-result.json; do
    cmp -s "$ARTIFACT_DIR/$name" "$COMPARE_ARTIFACT_DIR/$name" || { echo "error: independent Stage 6 $name differs" >&2; exit 1; }
  done
  echo "PASS: independent Stage 6 generations are logically equivalent"
fi

plutil -lint "$REPO_ROOT/tvos/CelesteTvOSRuntimeHost/PrivacyInfo.xcprivacy" >/dev/null
python3 - "$REPO_ROOT/tvos/CelesteTvOSRuntimeHost/PrivacyInfo.xcprivacy" <<'PY'
import plistlib, pathlib, sys
value=plistlib.loads(pathlib.Path(sys.argv[1]).read_bytes())
entries=value.get("NSPrivacyAccessedAPITypes", [])
expected={"NSPrivacyAccessedAPIType":"NSPrivacyAccessedAPICategoryUserDefaults","NSPrivacyAccessedAPITypeReasons":["CA92.1"]}
if expected not in entries: raise SystemExit("error: UserDefaults CA92.1 privacy reason missing")
PY
if git -C "$REPO_ROOT" grep -n -E 'com\.apple\.developer\.user-management|com\.apple\.developer\.ubiquity|iCloud' -- tvos/CelesteTvOSRuntimeHost >/dev/null; then
  echo "error: Stage 6 introduced User Management or iCloud" >&2; exit 1
fi
git -C "$REPO_ROOT" diff --quiet "$BASELINE_COMMIT" -- build.sh celestemeow fnalibs-ios-builder-celeste FNA native/tvos-dependencies.lock.json native/patches tvos/CelesteTvOSHost tvos/FNA.TvOS tvos/stage2-ios-native-baseline.sha256 || {
  echo "error: iOS or locked prior native/host dependency foundation changed" >&2; exit 1;
}
python3 "$REPO_ROOT/scripts/verify-celeste-tvos-stage16b.py" --repo-root "$REPO_ROOT" >/dev/null
(cd "$REPO_ROOT" && shasum -a 256 -c tvos/stage2-ios-native-baseline.sha256 >/dev/null)
echo "PASS: privacy reason, entitlement isolation, and iOS archive hashes"

if [[ -n "$APP_DIR" ]]; then
  [[ -d "$APP_DIR" ]] || { echo "error: app not found" >&2; exit 2; }
  case "$PLATFORM" in simulator|device) ;; *) echo "error: --platform required with --app" >&2; exit 2 ;; esac
  [[ -f "$APP_DIR/PrivacyInfo.xcprivacy" ]] || { echo "error: bundled privacy manifest missing" >&2; exit 1; }
  cmp -s "$APP_DIR/PrivacyInfo.xcprivacy" "$REPO_ROOT/tvos/CelesteTvOSRuntimeHost/PrivacyInfo.xcprivacy" || { echo "error: bundled privacy manifest differs" >&2; exit 1; }
  if find "$APP_DIR" -type f \( -iname '*.celeste' -o -iname '*.cs' -o -iname '*.h' -o -iname '*.a' \) -print -quit | grep -q .; then
    echo "error: app contains source, archive, Settings, or SaveData material" >&2; exit 1
  fi
  executable="$APP_DIR/$(plutil -extract CFBundleExecutable raw "$APP_DIR/Info.plist")"
  build_info="$(xcrun vtool -show-build "$executable")"
  if [[ "$PLATFORM" == device ]]; then
    grep -Eq 'platform[[:space:]]+TVOS$' <<<"$build_info" || { echo "error: device app platform mismatch" >&2; exit 1; }
    codesign --verify --deep --strict "$APP_DIR"
  else
    grep -Eq 'platform[[:space:]]+TVOSSIMULATOR$' <<<"$build_info" || { echo "error: simulator app platform mismatch" >&2; exit 1; }
    if find "$APP_DIR/Content/FMOD" -type f 2>/dev/null | grep -q .; then echo "error: simulator app contains FMOD banks" >&2; exit 1; fi
    if nm -gjU "$executable" | grep -E -q 'FMOD_(System|Studio)_'; then echo "error: simulator app defines FMOD" >&2; exit 1; fi
  fi
  echo "PASS: $PLATFORM package, privacy manifest, and proprietary writable-state isolation"
fi
