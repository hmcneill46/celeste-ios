#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/verify-celeste-tvos-stage13b.sh [options]

Verify Stage 13B soft-reload state, prior Stage 9B/10B/11/12B gates, and
optional generated or packaged product output.

Options:
  --generated-root DIR  Verify a generated Stage 13B managed tree
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

dotnet run --project "$REPO_ROOT/tvos/SoftReloadTests/SoftReloadTests.csproj" -c Release

prior_args=()
[[ -z "$GENERATED_ROOT" ]] || prior_args+=(--generated-root "$GENERATED_ROOT")
[[ -z "$APP" ]] || prior_args+=(--app "$APP" --platform "$PLATFORM")
[[ "$SIGNED" -eq 0 ]] || prior_args+=(--signed)
[[ -z "$IPA" ]] || prior_args+=(--ipa "$IPA")
if ((${#prior_args[@]})); then
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage12b.sh" "${prior_args[@]}"
else
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage12b.sh"
fi

python_args=(--repo-root "$REPO_ROOT")
[[ -z "$GENERATED_ROOT" ]] || python_args+=(--generated-root "$GENERATED_ROOT")
[[ -z "$APP" ]] || python_args+=(--app "$APP")
[[ -z "$IPA" ]] || python_args+=(--ipa "$IPA")
[[ -z "$OUTPUT" ]] || python_args+=(--output "$OUTPUT")
python3 "$REPO_ROOT/scripts/verify-celeste-tvos-stage13b.py" "${python_args[@]}"
echo "PASS: Stage 13B verification complete"
