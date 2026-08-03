#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/build-celeste-managed.sh [options]

Build the ignored modern Celeste library in Debug and Release, run trim/AOT
analysis, and publish an unsigned tvOS-arm64 full-AOT closure harness without
calling Celeste. Installs nothing and never links FMOD native code.

Options:
  --build-dir DIR    Prepared generated tree
                     (default: .build/celeste-managed/current).
  --artifact-dir DIR Privacy-safe manifests
                     (default: artifacts/celeste-managed/current).
  -h, --help         Show this help.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
BUILD_DIR="$REPO_ROOT/.build/celeste-managed/current"
ARTIFACT_DIR="$REPO_ROOT/artifacts/celeste-managed/current"
MARKER=".stage3a-managed-generation"

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

for generated_root in "$BUILD_DIR" "$ARTIFACT_DIR"; do
  case "$generated_root" in
    "$REPO_ROOT"/*) generated_relative="${generated_root#$REPO_ROOT/}" ;;
    *) echo "error: managed build directories must be inside ignored repository paths" >&2; exit 1 ;;
  esac
  git -C "$REPO_ROOT" check-ignore --no-index -q -- "$generated_relative/$MARKER" || {
    echo "error: managed build directory is not ignored by Git: \$REPO_ROOT/$generated_relative" >&2
    exit 1
  }
done
[[ -f "$BUILD_DIR/$MARKER" && -f "$ARTIFACT_DIR/$MARKER" ]] || {
  echo "error: prepared Stage 3A directories are missing; run prepare-celeste-managed.sh" >&2
  exit 1
}
[[ "$(dotnet --version)" == "10.0.302" ]] || { echo "error: dotnet 10.0.302 is required" >&2; exit 1; }
workloads="$(dotnet workload list)"
grep -Fq 'Workload version: 10.0.302.0' <<<"$workloads" || { echo "error: workload set 10.0.302.0 is required" >&2; exit 1; }
grep -Eq '^tvos[[:space:]]' <<<"$workloads" || { echo "error: tvOS workload is required" >&2; exit 1; }

"$REPO_ROOT/scripts/verify-tvos-host.sh" >/dev/null
mkdir -p "$BUILD_DIR/logs" "$BUILD_DIR/output" "$ARTIFACT_DIR"
export DOTNET_CLI_HOME="$BUILD_DIR/tool-home"
export NUGET_PACKAGES="$BUILD_DIR/packages"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

PROJECT="$BUILD_DIR/patched/Celeste.Modern.csproj"
CONTENT_PROJECT="$BUILD_DIR/patched/Celeste.Content.Modern.csproj"
COMMON=(
  -p:CelesteTvOSRepoRoot="$REPO_ROOT"
  -p:CelesteManagedGeneratedRoot="$BUILD_DIR/patched"
  -m:1
  -nr:false
)

echo "+ dotnet build Celeste.Modern.csproj -c Debug"
dotnet build "$PROJECT" -c Debug "${COMMON[@]}" \
  2>&1 | tee "$BUILD_DIR/logs/debug-build.log"

echo "+ dotnet build Celeste.Modern.csproj -c Release"
dotnet build "$PROJECT" -c Release "${COMMON[@]}" \
  2>&1 | tee "$BUILD_DIR/logs/release-build.log"

CLOSURE="$REPO_ROOT/tvos/CelesteManagedAotClosure/CelesteManagedAotClosure.csproj"
CLOSURE_COMMON=(
  -r tvos-arm64
  -p:CelesteManagedProject="$PROJECT"
  -p:CelesteContentProject="$CONTENT_PROJECT"
  -p:CelesteTvOSRepoRoot="$REPO_ROOT"
  -p:CelesteManagedGeneratedRoot="$BUILD_DIR/patched"
  -p:EnableCodeSigning=false
  -p:UseInterpreter=false
  -p:MtouchLink=Full
  -m:1
  -nr:false
)

echo "+ dotnet clean CelesteManagedAotClosure.csproj -c Release -r tvos-arm64"
if ! dotnet clean "$CLOSURE" -c Release "${CLOSURE_COMMON[@]}" \
  >"$BUILD_DIR/logs/closure-clean.log" 2>&1; then
  echo "error: closure clean failed; inspect <BUILD_DIR>/logs/closure-clean.log" >&2
  exit 1
fi

echo "+ dotnet restore CelesteManagedAotClosure.csproj -r tvos-arm64"
dotnet restore "$CLOSURE" "${CLOSURE_COMMON[@]}" \
  2>&1 | tee "$BUILD_DIR/logs/closure-restore.log"

echo "+ dotnet publish CelesteManagedAotClosure.csproj -c Release -r tvos-arm64 --no-restore"
dotnet publish "$CLOSURE" -c Release --no-restore "${CLOSURE_COMMON[@]}" \
  2>&1 | tee "$BUILD_DIR/logs/closure-publish.log"

APP_DIR="$(find "$REPO_ROOT/tvos/CelesteManagedAotClosure/bin/Release" -type d -name 'CelesteManagedAotClosure.app' -print -quit)"
[[ -n "$APP_DIR" ]] || { echo "error: full-AOT closure app was not produced" >&2; exit 1; }
python3 "$REPO_ROOT/scripts/celeste-managed.py" verify-app \
  --app "$APP_DIR" \
  --output "$ARTIFACT_DIR/aot-closure-manifest.json"
python3 "$REPO_ROOT/scripts/celeste-managed.py" diagnostics \
  --generated-root "$BUILD_DIR/patched" \
  --repo-root "$REPO_ROOT" \
  --log "$BUILD_DIR/logs/debug-build.log" \
  --log "$BUILD_DIR/logs/release-build.log" \
  --log "$BUILD_DIR/logs/closure-publish.log" \
  --output "$ARTIFACT_DIR/diagnostic-manifest.json"
python3 "$REPO_ROOT/scripts/celeste-managed.py" check-diagnostics \
  --diagnostics "$ARTIFACT_DIR/diagnostic-manifest.json" \
  --policy "$REPO_ROOT/managed/celeste-analysis-policy.json" \
  --output "$ARTIFACT_DIR/diagnostic-policy-result.json"

MODERN_DLL="$(find "$BUILD_DIR/patched/bin/Release" -type f -name Celeste.dll -print -quit)"
[[ -n "$MODERN_DLL" ]] || { echo "error: modern Celeste.dll was not produced" >&2; exit 1; }
references="$(monodis --assemblyref "$MODERN_DLL")"
if grep -Eq 'Name=mscorlib$' <<<"$references"; then
  echo "error: modern Celeste output retained a legacy mscorlib assembly reference" >&2
  exit 1
fi
grep -Fq 'Name=FNA' <<<"$references" || {
  echo "error: modern output does not reference the project-built FNA adapter identity" >&2
  exit 1
}

python3 - "$ARTIFACT_DIR/build-result.json" <<'PY'
import json, pathlib, sys
pathlib.Path(sys.argv[1]).write_text(json.dumps({
    "schemaVersion": 1,
    "debugBuild": "passed",
    "releaseBuildWithTrimAndAotAnalyzers": "passed",
    "deviceAotClosure": "passed",
    "deviceAotClosureInvokedCeleste": False,
    "deviceAotClosureSigned": False,
    "deviceAotClosureLaunched": False,
    "interpreter": False,
    "trimMode": "full",
    "fmodNativeLinked": False,
}, indent=2, sort_keys=True) + "\n")
PY

echo "Stage 3A managed builds and unsigned full-AOT closure completed."
