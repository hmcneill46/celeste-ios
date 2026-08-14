#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/verify-celeste-tvos-stage10b.sh [options]

Verify the Stage 10B writable Save Manager protocol, Stage 9B persistence
authority, preserved Stage 10A network boundary, and optional app/IPA.

Options:
  --app DIR       Verify a built app bundle
  --platform NAME simulator or device (required with --app)
  --ipa FILE      Verify a signing-ready unsigned IPA through Stage 8A
  --signed        Treat --app as a signed device app
  --output FILE   Write a privacy-safe Stage 10B JSON summary
  -h, --help      Show help
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP=""; PLATFORM=""; IPA=""; OUTPUT=""; SIGNED=0
repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --app) APP="$(repo_path "$2")"; shift 2 ;;
    --platform) PLATFORM="$2"; shift 2 ;;
    --ipa) IPA="$(repo_path "$2")"; shift 2 ;;
    --signed) SIGNED=1; shift ;;
    --output) OUTPUT="$(repo_path "$2")"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done

for tool in dotnet python3 git; do command -v "$tool" >/dev/null || { echo "error: missing tool: $tool" >&2; exit 1; }; done
python_args=(--repo-root "$REPO_ROOT")
[[ -z "$APP" ]] || python_args+=(--app "$APP")
[[ -z "$OUTPUT" ]] || python_args+=(--output "$OUTPUT")

dotnet run --project "$REPO_ROOT/tvos/SaveManagerProtocolTests/SaveManagerProtocolTests.csproj" -c Release
"$REPO_ROOT/scripts/verify-celeste-tvos-stage9b.sh"
"$REPO_ROOT/scripts/verify-celeste-tvos-stage10a.py" --repo-root "$REPO_ROOT"
"$REPO_ROOT/scripts/verify-celeste-tvos-stage10b.py" "${python_args[@]}"

if [[ -n "$APP" ]]; then
  [[ "$PLATFORM" == "simulator" || "$PLATFORM" == "device" ]] || { echo "error: --platform must be simulator or device with --app" >&2; exit 2; }
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage9b.sh" --skip-stage6-foundation --app "$APP" --platform "$PLATFORM"
  if [[ "$PLATFORM" == "device" ]]; then
    stage8_args=(--repo-root "$REPO_ROOT" --app "$APP")
    [[ "$SIGNED" -eq 0 ]] || stage8_args+=(--signed)
    "$REPO_ROOT/scripts/verify-celeste-tvos-stage8a.py" "${stage8_args[@]}"
  fi
fi
if [[ -n "$IPA" ]]; then
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage8a.py" --repo-root "$REPO_ROOT" --ipa "$IPA"
fi
echo "PASS: Stage 10B verification complete"
