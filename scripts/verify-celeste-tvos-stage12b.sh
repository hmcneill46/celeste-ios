#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/verify-celeste-tvos-stage12b.sh [options]

Verify Stage 12B graceful Quit state, the exact generated interception,
preserved Stage 9B/10B/11 foundations, and optional built products.

Options:
  --generated-root DIR  Verify a generated Stage 12B managed tree
  --app DIR             Verify a built app bundle
  --platform NAME       simulator or device (required with --app)
  --signed              Treat --app as signed
  --ipa FILE            Verify a signing-ready unsigned IPA
  --output FILE         Write a privacy-safe JSON result
  -h, --help            Show help
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
GENERATED_ROOT=""; APP=""; PLATFORM=""; IPA=""; OUTPUT=""; SIGNED=0
repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --generated-root) GENERATED_ROOT="$(repo_path "$2")"; shift 2 ;;
    --app) APP="$(repo_path "$2")"; shift 2 ;;
    --platform) PLATFORM="$2"; shift 2 ;;
    --signed) SIGNED=1; shift ;;
    --ipa) IPA="$(repo_path "$2")"; shift 2 ;;
    --output) OUTPUT="$(repo_path "$2")"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

dotnet run --project "$REPO_ROOT/tvos/Stage12BQuitTests/Stage12BQuitTests.csproj" -c Release

stage11_args=()
[[ -z "$GENERATED_ROOT" ]] || stage11_args+=(--generated-root "$GENERATED_ROOT")
[[ -z "$APP" ]] || stage11_args+=(--app "$APP" --platform "$PLATFORM")
[[ "$SIGNED" -eq 0 ]] || stage11_args+=(--signed)
[[ -z "$IPA" ]] || stage11_args+=(--ipa "$IPA")
if ((${#stage11_args[@]})); then
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage11.sh" "${stage11_args[@]}"
else
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage11.sh"
fi

python_args=(--repo-root "$REPO_ROOT")
[[ -z "$GENERATED_ROOT" ]] || python_args+=(--generated-root "$GENERATED_ROOT")
[[ -z "$APP" ]] || python_args+=(--app "$APP")
[[ -z "$IPA" ]] || python_args+=(--ipa "$IPA")
[[ -z "$OUTPUT" ]] || python_args+=(--output "$OUTPUT")
python3 "$REPO_ROOT/scripts/verify-celeste-tvos-stage12b.py" "${python_args[@]}"
echo "PASS: Stage 12B verification complete"
