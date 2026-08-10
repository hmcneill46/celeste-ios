#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/verify-celeste-managed.sh [options]

Verify a prepared and built Stage 3A managed artifact set, legal isolation,
native-input isolation, full-AOT closure metadata and previous-stage integrity.
Installs nothing and never launches an application.

Options:
  --build-dir DIR    Prepared generated tree
                     (default: .build/celeste-managed/current).
  --artifact-dir DIR Privacy-safe manifests
                     (default: artifacts/celeste-managed/current).
  --compare-dir DIR  Compare normalized manifests with another artifact set.
  -h, --help         Show this help.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
BUILD_DIR="$REPO_ROOT/.build/celeste-managed/current"
ARTIFACT_DIR="$REPO_ROOT/artifacts/celeste-managed/current"
COMPARE_DIR=""
MARKER=".stage3a-managed-generation"
BASELINE_COMMIT="3b1d516709b4bd7d45a1e86aa84780fbba817fba"

repo_path() {
  case "$1" in
    /*) printf '%s\n' "$1" ;;
    *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;;
  esac
}

while (($#)); do
  case "$1" in
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
    --compare-dir)
      [[ $# -ge 2 ]] || { echo "error: --compare-dir requires a value" >&2; exit 2; }
      COMPARE_DIR="$(repo_path "$2")"
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

for generated_root in "$BUILD_DIR" "$ARTIFACT_DIR" ${COMPARE_DIR:+"$COMPARE_DIR"}; do
  case "$generated_root" in
    "$REPO_ROOT"/*) generated_relative="${generated_root#$REPO_ROOT/}" ;;
    *) echo "error: managed verification directories must be inside ignored repository paths" >&2; exit 1 ;;
  esac
  git -C "$REPO_ROOT" check-ignore --no-index -q -- "$generated_relative/$MARKER" || {
    echo "error: managed verification directory is not ignored by Git: \$REPO_ROOT/$generated_relative" >&2
    exit 1
  }
done
[[ -f "$BUILD_DIR/$MARKER" && -f "$ARTIFACT_DIR/$MARKER" ]] || {
  echo "error: Stage 3A generated/artifact marker is missing" >&2
  exit 1
}
for name in input-manifest.json decompiled-source-manifest.json patched-source-manifest.json \
  generation-manifest.json fmod-compile-boundary.json reflection-inventory.json \
  serializer-inventory.json content-reader-inventory.json native-import-inventory.json platform-api-inventory.json tvstubs-reachability.json \
  diagnostic-manifest.json diagnostic-policy-result.json aot-closure-manifest.json build-result.json; do
  [[ -f "$ARTIFACT_DIR/$name" ]] || { echo "error: missing artifact manifest: $name" >&2; exit 1; }
done

TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/celeste-stage3a-verify.XXXXXX")"
cleanup() { rm -rf -- "$TMP_DIR"; }
trap cleanup EXIT
python3 "$REPO_ROOT/scripts/celeste-managed.py" tree-manifest \
  --root "$BUILD_DIR/decompiled" --kind decompiled-source --placeholder DECOMPILED_ROOT \
  --output "$TMP_DIR/decompiled-source-manifest.json" >/dev/null
python3 "$REPO_ROOT/scripts/celeste-managed.py" tree-manifest \
  --root "$BUILD_DIR/patched" --kind patched-source --placeholder PATCHED_ROOT \
  --output "$TMP_DIR/patched-source-manifest.json" >/dev/null
cmp -s "$TMP_DIR/decompiled-source-manifest.json" "$ARTIFACT_DIR/decompiled-source-manifest.json" || {
  echo "error: decompiled source changed after manifest generation" >&2; exit 1;
}
cmp -s "$TMP_DIR/patched-source-manifest.json" "$ARTIFACT_DIR/patched-source-manifest.json" || {
  echo "error: patched source changed after manifest generation" >&2; exit 1;
}

python3 - "$ARTIFACT_DIR" "$BUILD_DIR/patched/Celeste.Modern.csproj" <<'PY'
import json, pathlib, re, sys
artifacts, project = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
input_manifest = json.loads((artifacts / "input-manifest.json").read_text())
if input_manifest["validation"] != "supported-unmodified-fna-release" or input_manifest["gameVersion"] != "1.4.0.0":
    raise SystemExit("error: input validation manifest is not the locked Celeste 1.4.0.0 release")
boundary = json.loads((artifacts / "fmod-compile-boundary.json").read_text())
if boundary["transformedImportCount"] != 490:
    raise SystemExit("error: FMOD compile boundary does not cover the locked 490 imports")
if boundary["policy"] != "compile-only-managed-throw; no native FMOD library or symbol provider":
    raise SystemExit("error: FMOD boundary policy changed")
stubs = json.loads((artifacts / "tvstubs-reachability.json").read_text())
if stubs["generatedCelesteCallSites"]:
    raise SystemExit("error: a generated Celeste call site reaches tvStubs")
diagnostics = json.loads((artifacts / "diagnostic-manifest.json").read_text())
if diagnostics["errorCount"]:
    raise SystemExit("error: compiler/trim/AOT diagnostics contain an error")
policy = json.loads((artifacts / "diagnostic-policy-result.json").read_text())
if policy["unexplainedCount"] != 0 or policy["explainedCount"] != diagnostics["uniqueDiagnosticCount"]:
    raise SystemExit("error: compiler/trim/AOT diagnostics are not fully explained by focused policy")
closure = json.loads((artifacts / "aot-closure-manifest.json").read_text())
if closure["result"] != "full-aot-link-closure-verified" or closure["fmodNativeBinaryCount"] != 0 or closure["celesteContentFileCount"] != 0:
    raise SystemExit("error: closure manifest does not prove isolated full-AOT closure")
text = project.read_text()
if "net10.0-tvos26.5" not in text or "tvos/FNA.TvOS/FNA.TvOS.csproj" not in text:
    raise SystemExit("error: generated project is not retargeted to the pinned modern FNA adapter")
if re.search(r"net452|mscorlib\.dll|FNA\.dll|TrimmerRootAssembly|PublishTrimmed>false|UseInterpreter>true", text):
    raise SystemExit("error: generated project contains a forbidden legacy or blanket AOT workaround")
PY

APP_DIR="$(find "$REPO_ROOT/tvos/CelesteManagedAotClosure/bin/Release" -type d -name 'CelesteManagedAotClosure.app' -print -quit)"
[[ -n "$APP_DIR" ]] || { echo "error: closure app is missing" >&2; exit 1; }
python3 "$REPO_ROOT/scripts/celeste-managed.py" verify-app \
  --app "$APP_DIR" --output "$TMP_DIR/aot-closure-manifest.json" >/dev/null
cmp -s "$TMP_DIR/aot-closure-manifest.json" "$ARTIFACT_DIR/aot-closure-manifest.json" || {
  echo "error: closure bundle no longer matches its verification manifest" >&2; exit 1;
}

if find "$APP_DIR" -type f \( -iname '*fmod*' -o -iname 'Celeste.exe' -o -iname '*.celeste' \) -print -quit | grep -q .; then
  echo "error: closure contains FMOD native, legacy executable, or save material" >&2
  exit 1
fi
if find "$APP_DIR" -type d -name Content -print -quit | grep -q .; then
  echo "error: closure contains a Celeste Content directory" >&2
  exit 1
fi

"$REPO_ROOT/scripts/verify-tvos-host.sh" >/dev/null
git -C "$REPO_ROOT" diff --quiet "$BASELINE_COMMIT" -- \
  build.sh celestemeow fnalibs-ios-builder-celeste FNA \
  native scripts/fetch-tvos-deps.sh scripts/build-tvos-native.sh \
  scripts/verify-tvos-native.sh scripts/verify-tvos-artifacts.py \
  scripts/prepare-tvos-host-native.sh scripts/verify-tvos-host.sh \
  scripts/run-tvos-host-simulator.sh tvos/CelesteTvOSHost tvos/FNA.TvOS || {
  echo "error: existing iOS, Stage 1, or Stage 2 tracked foundation changed" >&2
  exit 1
}

python3 - "$REPO_ROOT" <<'PY'
import pathlib, re, subprocess, sys
root = pathlib.Path(sys.argv[1])
output = subprocess.check_output([
    "git", "-C", str(root), "ls-files", "--cached", "--others", "--exclude-standard", "--",
    ".gitignore", ".config/dotnet-tools.json", "managed",
    "scripts/validate-celeste-input.sh", "scripts/prepare-celeste-managed.sh",
    "scripts/build-celeste-managed.sh", "scripts/verify-celeste-managed.sh",
    "scripts/celeste-managed.py", "tvos/CelesteManagedAotClosure",
], text=True)
for relative in output.splitlines():
    path = root / relative
    if not path.is_file():
        continue
    data = path.read_bytes()
    forbidden = {
        "private home path": re.compile(b"/" + b"Users/" + rb"[^/$\s]+/"),
        "email": re.compile(rb"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"),
        "provision/device UUID": re.compile(rb"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b"),
        "signing fingerprint": re.compile(rb"\b[A-F0-9]{40}\b"),
        "literal development team": re.compile(rb"<DevelopmentTeam>\s*[A-Z0-9]{10}\s*</DevelopmentTeam>"),
    }
    for label, pattern in forbidden.items():
        if pattern.search(data):
            raise SystemExit(f"error: {label} found in candidate tracked file {relative}")
    if relative.startswith((".build/celeste-managed/", "artifacts/celeste-managed/")):
        raise SystemExit(f"error: generated Celeste material is a candidate tracked file: {relative}")
print("PASS: no generated/proprietary/private material is trackable")
PY

if [[ -n "$COMPARE_DIR" ]]; then
  python3 "$REPO_ROOT/scripts/celeste-managed.py" compare \
    --left "$ARTIFACT_DIR" --right "$COMPARE_DIR" \
    --manifest input-manifest.json \
    --manifest decompiled-source-manifest.json \
    --manifest patched-source-manifest.json \
    --manifest generation-manifest.json \
    --manifest fmod-compile-boundary.json \
    --manifest reflection-inventory.json \
    --manifest serializer-inventory.json \
    --manifest content-reader-inventory.json \
    --manifest native-import-inventory.json \
    --manifest platform-api-inventory.json \
    --manifest tvstubs-reachability.json \
    --manifest diagnostic-manifest.json \
    --manifest diagnostic-policy-result.json \
    --manifest aot-closure-manifest.json \
    --manifest build-result.json \
    --output "$ARTIFACT_DIR/reproducibility-comparison.json"
fi

echo "Stage 3A managed verification passed."
