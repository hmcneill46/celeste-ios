#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/verify-celeste-tvos-stage14.sh [options]

Verify the Stage 14 release-candidate checklist and all accepted Stage 9B–13B
gates, with optional fresh native/generated/package evidence.

With no product-evidence options, this runs the complete tracked-source suite
and does not require ignored generated build output.

Options:
  --generated-root DIR   Verify a generated managed release tree
  --native-manifest FILE Verify a fresh Stage 1 normalized manifest
  --app DIR              Verify a built app bundle
  --platform NAME        simulator or device (required with --app)
  --signed               Treat --app as signed
  --ipa FILE             Verify a signing-ready unsigned IPA
  --output FILE          Write a privacy-safe JSON result
  -h, --help             Show this help
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
GENERATED_ROOT=""; NATIVE_MANIFEST=""; APP=""; PLATFORM=""; IPA=""; OUTPUT=""; SIGNED=0
repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --generated-root) GENERATED_ROOT="$(repo_path "$2")"; shift 2 ;;
    --native-manifest) NATIVE_MANIFEST="$(repo_path "$2")"; shift 2 ;;
    --app) APP="$(repo_path "$2")"; shift 2 ;;
    --platform) PLATFORM="$2"; shift 2 ;;
    --signed) SIGNED=1; shift ;;
    --ipa) IPA="$(repo_path "$2")"; shift 2 ;;
    --output) OUTPUT="$(repo_path "$2")"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

prior_args=()
[[ -z "$GENERATED_ROOT" ]] || prior_args+=(--generated-root "$GENERATED_ROOT")
[[ -z "$APP" ]] || prior_args+=(--app "$APP" --platform "$PLATFORM")
[[ "$SIGNED" -eq 0 ]] || prior_args+=(--signed)
[[ -z "$IPA" ]] || prior_args+=(--ipa "$IPA")
if ((${#prior_args[@]})); then
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage13b.sh" "${prior_args[@]}"
else
  dotnet run --project "$REPO_ROOT/tvos/Stage13BSoftReloadTests/Stage13BSoftReloadTests.csproj" -c Release
  dotnet run --project "$REPO_ROOT/tvos/Stage12BQuitTests/Stage12BQuitTests.csproj" -c Release
  dotnet run --project "$REPO_ROOT/tvos/Stage11ControllerPromptTests/Stage11ControllerPromptTests.csproj" -c Release
  dotnet run --project "$REPO_ROOT/tvos/Stage10AProtocolTests/Stage10AProtocolTests.csproj" -c Release
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage9b.sh" --skip-stage6-foundation
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage10a.py" --repo-root "$REPO_ROOT"
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage10b.py" --repo-root "$REPO_ROOT"
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage11.py" --repo-root "$REPO_ROOT"
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage12b.py" --repo-root "$REPO_ROOT"
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage13b.py" --repo-root "$REPO_ROOT"
fi
python3 "$REPO_ROOT/scripts/verify-repository-stage8b.py"
(cd "$REPO_ROOT" && shasum -a 256 -c tvos/stage2-ios-native-baseline.sha256 >/dev/null)

args=(--repo-root "$REPO_ROOT")
[[ -z "$GENERATED_ROOT" ]] || args+=(--generated-root "$GENERATED_ROOT")
[[ -z "$NATIVE_MANIFEST" ]] || args+=(--native-manifest "$NATIVE_MANIFEST")
[[ -z "$APP" ]] || args+=(--app "$APP")
[[ -z "$IPA" ]] || args+=(--ipa "$IPA")
[[ -z "$OUTPUT" ]] || args+=(--output "$OUTPUT")
python3 "$REPO_ROOT/scripts/verify-celeste-tvos-stage14.py" "${args[@]}"
echo "PASS: Stage 14 release-candidate verification complete"
