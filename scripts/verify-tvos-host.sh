#!/usr/bin/env bash
set -euo pipefail

readonly BASELINE_COMMIT="5c59ba1b2cb353d241f6026caa8da1d8e1822e24"
readonly EXPECTED_LOGICAL_SHA256="6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
readonly COMPONENTS=(SDL2 FNA3D FAudio Theorafile tvStubs MoltenVK)
readonly REQUIRED_EXPORTS=(SDL_GetVersion FNA3D_LinkedVersion FAudioLinkedVersion tf_fopen vkGetInstanceProcAddr)

usage() {
  cat <<'USAGE'
Usage: scripts/verify-tvos-host.sh [options]

Verify the repository-owned Stage 2 project, accepted Stage 1 inputs, iOS-lane
isolation, and optionally a built tvOS application bundle. Installs nothing.

Options:
  --stage-dir DIR       Prepared native input directory
                        (default: .build/tvos-host).
  --app DIR             Validate this built .app bundle.
  --platform NAME       Required with --app: tvos or tvossimulator.
  --skip-toolchain      Skip local SDK/workload version checks.
  -h, --help            Show this help.

Relative paths are resolved from the repository root. The application check
requires arm64, minimum tvOS 16.0, SDK 26.5, and all representative exports.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
STAGE_DIR="$REPO_ROOT/.build/tvos-host"
APP_DIR=""
EXPECTED_PLATFORM=""
CHECK_TOOLCHAIN=1

repo_path() {
  case "$1" in
    /*) printf '%s\n' "$1" ;;
    *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;;
  esac
}

while (($#)); do
  case "$1" in
    --stage-dir)
      [[ $# -ge 2 ]] || { echo "error: --stage-dir requires a value" >&2; exit 2; }
      STAGE_DIR="$(repo_path "$2")"
      shift 2
      ;;
    --app)
      [[ $# -ge 2 ]] || { echo "error: --app requires a value" >&2; exit 2; }
      APP_DIR="$(repo_path "$2")"
      shift 2
      ;;
    --platform)
      [[ $# -ge 2 ]] || { echo "error: --platform requires a value" >&2; exit 2; }
      EXPECTED_PLATFORM="$2"
      shift 2
      ;;
    --skip-toolchain)
      CHECK_TOOLCHAIN=0
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

if [[ -n "$APP_DIR" && -z "$EXPECTED_PLATFORM" ]]; then
  echo "error: --platform is required with --app" >&2
  exit 2
fi
if [[ -z "$APP_DIR" && -n "$EXPECTED_PLATFORM" ]]; then
  echo "error: --platform requires --app" >&2
  exit 2
fi
case "$EXPECTED_PLATFORM" in
  ""|tvos|tvossimulator) ;;
  *) echo "error: --platform must be tvos or tvossimulator" >&2; exit 2 ;;
esac

if [[ "$CHECK_TOOLCHAIN" -eq 1 ]]; then
  [[ "$(dotnet --version)" == "10.0.302" ]] || {
    echo "error: dotnet 10.0.302 is required" >&2
    exit 1
  }
  workload_output="$(dotnet workload list)"
  grep -Fq "Workload version: 10.0.302.0" <<<"$workload_output" || {
    echo "error: workload set 10.0.302.0 is required" >&2
    exit 1
  }
  grep -Eq '^tvos[[:space:]]' <<<"$workload_output" || {
    echo "error: the tvOS workload is not installed" >&2
    exit 1
  }
fi

[[ -f "$STAGE_DIR/normalized-manifest.json" ]] || {
  echo "error: prepared normalized manifest is missing: $STAGE_DIR" >&2
  exit 1
}

python3 - "$STAGE_DIR" "$EXPECTED_LOGICAL_SHA256" "${COMPONENTS[@]}" <<'PY'
import json, pathlib, plistlib, sys
root = pathlib.Path(sys.argv[1])
expected_hash = sys.argv[2]
components = sys.argv[3:]
manifest = json.loads((root / "normalized-manifest.json").read_text())
if manifest.get("logicalSetSha256") != expected_hash:
    raise SystemExit("error: prepared native logical checksum is not the Stage 1 acceptance checksum")
if manifest.get("deploymentTarget") != "16.0":
    raise SystemExit("error: prepared native deployment target is not tvOS 16.0")
if set(manifest.get("components", {})) != set(components):
    raise SystemExit("error: prepared manifest does not contain exactly six components")
for component in components:
    info_path = root / "native" / f"{component}.xcframework" / "Info.plist"
    if not info_path.is_file():
        raise SystemExit(f"error: missing {component}.xcframework")
    with info_path.open("rb") as stream:
        info = plistlib.load(stream)
    variants = info.get("AvailableLibraries", [])
    if len(variants) != 2:
        raise SystemExit(f"error: {component} contains a non-tvOS or extra platform variant")
    device = [v for v in variants if v.get("SupportedPlatform") == "tvos" and not v.get("SupportedPlatformVariant")]
    simulator = [v for v in variants if v.get("SupportedPlatform") == "tvos" and v.get("SupportedPlatformVariant") == "simulator"]
    if len(device) != 1 or len(simulator) != 1:
        raise SystemExit(f"error: {component} lacks exactly one tvOS device and simulator variant")
    if "arm64" not in device[0].get("SupportedArchitectures", []) or "arm64" not in simulator[0].get("SupportedArchitectures", []):
        raise SystemExit(f"error: {component} lacks a required arm64 variant")
print("PASS: six accepted Stage 1 XCFrameworks")
PY

for binding in SDL2.cs FNA3D.cs FAudio.cs Theorafile.cs; do
  grep -Fq 'nativeLibName = "__Internal"' "$STAGE_DIR/managed/$binding" || {
    echo "error: $binding is not mapped to __Internal" >&2
    exit 1
  }
done
echo "PASS: static native imports map to __Internal"

python3 - "$REPO_ROOT/tvos/CelesteTvOSHost/CelesteTvOSHost.csproj" \
  "$REPO_ROOT/tvos/CelesteTvOSHost/Info.plist" \
  "$REPO_ROOT/tvos/CelesteTvOSHost/Entitlements.plist" "${COMPONENTS[@]}" <<'PY'
import pathlib, plistlib, sys, xml.etree.ElementTree as ET
project_path, info_path, entitlements_path = map(pathlib.Path, sys.argv[1:4])
components = sys.argv[4:]
project = ET.parse(project_path).getroot()
tfm = project.findtext(".//TargetFramework")
if tfm != "net10.0-tvos":
    raise SystemExit(f"error: unexpected host TFM {tfm!r}")
if project.find(".//RuntimeIdentifiers") is not None:
    raise SystemExit("error: a multi-RID RuntimeIdentifiers property breaks device/simulator classification")
native_names = {pathlib.PureWindowsPath(item.attrib["Include"]).name for item in project.findall(".//NativeReference")}
expected_names = {f"{name}.xcframework" for name in components}
if native_names != expected_names:
    raise SystemExit("error: the project does not reference exactly the six Stage 1 XCFrameworks")
with info_path.open("rb") as stream:
    info = plistlib.load(stream)
if info.get("UIDeviceFamily") != [3] or info.get("MinimumOSVersion") != "16.0":
    raise SystemExit("error: Info.plist is not isolated to tvOS family 3 with minimum 16.0")
if info.get("UIRequiredDeviceCapabilities") != ["arm64"]:
    raise SystemExit("error: Info.plist does not require arm64")
with entitlements_path.open("rb") as stream:
    entitlements = plistlib.load(stream)
if entitlements:
    raise SystemExit("error: Stage 2 entitlements must remain empty")
print("PASS: project, Info.plist, and entitlement isolation")
PY

if grep -R -n -E -i 'fmod|Celeste\.(exe|dll)|decompil|/Content/' \
  "$REPO_ROOT/tvos/CelesteTvOSHost" "$REPO_ROOT/tvos/FNA.TvOS" >/dev/null; then
  echo "error: host project contains a Celeste executable/content/decompilation or FMOD reference" >&2
  exit 1
fi
if git -C "$REPO_ROOT" grep -n -E 'com\.apple\.developer\.user-management' -- tvos >/dev/null; then
  echo "error: Stage 2 must not request Apple TV User Management" >&2
  exit 1
fi
echo "PASS: no Celeste material, FMOD, or User Management entitlement"

python3 - "$REPO_ROOT/native/tvos-symbol-expectations.json" "$REPO_ROOT/tvos/CelesteTvOSHost" <<'PY'
import json, pathlib, sys
symbols = json.loads(pathlib.Path(sys.argv[1]).read_text())["components"]["tvStubs"]["symbols"]
source_root = pathlib.Path(sys.argv[2])
sources = "\n".join(path.read_text() for path in source_root.glob("*.cs"))
used = sorted(symbol for symbol in symbols if symbol in sources)
if used:
    raise SystemExit("error: Stage 2 source calls tvStubs symbols: " + ", ".join(used))
print(f"PASS: no call sites for {len(symbols)} tvStubs exports")
PY

(cd "$REPO_ROOT" && shasum -a 256 -c tvos/stage2-ios-native-baseline.sha256)
git -C "$REPO_ROOT" diff --quiet "$BASELINE_COMMIT" -- \
  build.sh celestemeow fnalibs-ios-builder-celeste || {
  echo "error: existing iOS application/build/submodule lane changed from the Stage 2 baseline" >&2
  exit 1
}
git -C "$REPO_ROOT" diff --quiet "$BASELINE_COMMIT" -- \
  native/tvos-dependencies.lock.json native/patches \
  scripts/fetch-tvos-deps.sh scripts/verify-tvos-native.sh \
  scripts/verify-tvos-artifacts.py || {
  echo "error: locked Stage 1 dependencies, patches, or base verifier changed" >&2
  exit 1
}
python3 "$REPO_ROOT/scripts/verify-celeste-tvos-stage16b.py" --repo-root "$REPO_ROOT" >/dev/null
echo "PASS: existing iOS lane and accepted Stage 16 native evolution are isolated"

python3 - "$REPO_ROOT" <<'PY'
import pathlib, re, subprocess, sys
root = pathlib.Path(sys.argv[1])
output = subprocess.check_output(
    ["git", "-C", str(root), "ls-files", "--cached", "--others", "--exclude-standard", "--", ".gitignore", "global.json", "scripts/prepare-tvos-host-native.sh", "scripts/verify-tvos-host.sh", "scripts/run-tvos-host-simulator.sh", "tvos"],
    text=True,
)
paths = [root / line for line in output.splitlines() if line]
patterns = {
    "user home path": re.compile(b"/" + b"Users/" + rb"[^/$\s]+/"),
    "email address": re.compile(rb"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"),
    "device/profile UUID": re.compile(rb"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b"),
    "identity fingerprint": re.compile(rb"\b[A-F0-9]{40}\b"),
    "literal development team": re.compile(rb"<DevelopmentTeam>\s*[A-Z0-9]{10}\s*</DevelopmentTeam>"),
}
for path in paths:
    if not path.is_file():
        continue
    data = path.read_bytes()
    for label, pattern in patterns.items():
        if pattern.search(data):
            raise SystemExit(f"error: {label} found in candidate tracked file {path.relative_to(root)}")
print("PASS: no private signing or device values in candidate tracked files")
PY

if [[ -n "$APP_DIR" ]]; then
  [[ -d "$APP_DIR" ]] || { echo "error: app bundle not found: $APP_DIR" >&2; exit 1; }
  EXECUTABLE_NAME="$(plutil -extract CFBundleExecutable raw "$APP_DIR/Info.plist")"
  EXECUTABLE="$APP_DIR/$EXECUTABLE_NAME"
  file "$EXECUTABLE" | grep -Fq "Mach-O 64-bit executable arm64" || {
    echo "error: application executable is not arm64 Mach-O" >&2
    exit 1
  }
  build_info="$(xcrun vtool -show-build "$EXECUTABLE")"
  if [[ "$EXPECTED_PLATFORM" == "tvos" ]]; then
    grep -Eq 'platform[[:space:]]+TVOS$' <<<"$build_info" || {
      echo "error: application executable is not TVOS" >&2
      exit 1
    }
  else
    grep -Eq 'platform[[:space:]]+TVOSSIMULATOR$' <<<"$build_info" || {
      echo "error: application executable is not TVOSSIMULATOR" >&2
      exit 1
    }
  fi
  grep -Eq 'minos[[:space:]]+16\.0$' <<<"$build_info" || {
    echo "error: application minimum OS is not 16.0" >&2
    exit 1
  }
  grep -Eq 'sdk[[:space:]]+26\.5$' <<<"$build_info" || {
    echo "error: application SDK is not 26.5" >&2
    exit 1
  }
  exports="$(nm -gjU "$EXECUTABLE")"
  for symbol in "${REQUIRED_EXPORTS[@]}"; do
    grep -Fxq "_$symbol" <<<"$exports" || {
      echo "error: application executable lacks _$symbol" >&2
      exit 1
    }
  done
  echo "PASS: $EXPECTED_PLATFORM arm64 app bundle and representative exports"
fi

echo "Stage 2 host verification passed."
