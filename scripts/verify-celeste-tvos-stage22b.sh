#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/verify-celeste-tvos-stage22b.sh [options]

Verify Stage 22B Save Manager continuity and the complete accepted product chain.

Options:
  --generated-root DIR   Verify a generated managed release tree
  --native-manifest FILE Verify the accepted native set
  --app DIR              Verify a built app bundle
  --platform NAME        simulator or device (required with --app)
  --signed               Treat --app as signed
  --ipa FILE             Verify a signing-ready unsigned IPA
  --output FILE          Write a privacy-safe Stage 22B JSON result
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

stage16_args=()
[[ -z "$GENERATED_ROOT" ]] || stage16_args+=(--generated-root "$GENERATED_ROOT")
[[ -z "$NATIVE_MANIFEST" ]] || stage16_args+=(--native-manifest "$NATIVE_MANIFEST")
[[ -z "$APP" ]] || stage16_args+=(--app "$APP" --platform "$PLATFORM")
[[ "$SIGNED" -eq 0 ]] || stage16_args+=(--signed)
[[ -z "$IPA" ]] || stage16_args+=(--ipa "$IPA")
if ((${#stage16_args[@]})); then
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage16b.sh" "${stage16_args[@]}"
else
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage16b.sh"
fi

dotnet run --project "$REPO_ROOT/tvos/SaveManagerProtocolTests/SaveManagerProtocolTests.csproj" -c Release
dotnet run --project "$REPO_ROOT/tvos/SaveManagerContinuityTests/SaveManagerContinuityTests.csproj" -c Release

args=(--repo-root "$REPO_ROOT")
[[ -z "$GENERATED_ROOT" ]] || args+=(--generated-root "$GENERATED_ROOT")
[[ -z "$NATIVE_MANIFEST" ]] || args+=(--native-manifest "$NATIVE_MANIFEST")
[[ -z "$APP" ]] || args+=(--app "$APP")
[[ -z "$IPA" ]] || args+=(--ipa "$IPA")
[[ -z "$OUTPUT" ]] || args+=(--output "$OUTPUT")
python3 "$REPO_ROOT/scripts/verify-celeste-tvos-stage22b.py" "${args[@]}"
echo "PASS: Stage 22B Save Manager continuity verification complete"
