#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/verify-celeste-tvos-stage11.sh [options]

Verify the Stage 11 controller-prompt inventory, deterministic policy,
generated-source isolation, prior Stage 9B/10B foundations, and optional
built app/unsigned IPA.

Options:
  --game-root DIR       Validate the locked GUI atlas inventory
  --generated-root DIR  Verify generated Settings and Stage 11 hooks
  --app DIR             Verify a built app bundle
  --platform NAME       simulator or device (required with --app)
  --signed              Treat --app as a signed device app
  --ipa FILE            Verify a signing-ready unsigned IPA
  --output FILE         Write a privacy-safe Stage 11 JSON summary
  -h, --help            Show help
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
GAME_ROOT=""; GENERATED_ROOT=""; APP=""; PLATFORM=""; IPA=""; OUTPUT=""; SIGNED=0
repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --game-root) GAME_ROOT="$(repo_path "$2")"; shift 2 ;;
    --generated-root) GENERATED_ROOT="$(repo_path "$2")"; shift 2 ;;
    --app) APP="$(repo_path "$2")"; shift 2 ;;
    --platform) PLATFORM="$2"; shift 2 ;;
    --signed) SIGNED=1; shift ;;
    --ipa) IPA="$(repo_path "$2")"; shift 2 ;;
    --output) OUTPUT="$(repo_path "$2")"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done

for tool in dotnet python3 git; do
  command -v "$tool" >/dev/null || { echo "error: missing tool: $tool" >&2; exit 1; }
done

if [[ -n "$GAME_ROOT" ]]; then
  "$REPO_ROOT/scripts/validate-celeste-input.sh" --game-root "$GAME_ROOT" >/dev/null
  "$REPO_ROOT/scripts/inventory-celeste-controller-prompts.py" --game-root "$GAME_ROOT"
fi

dotnet run --project "$REPO_ROOT/tvos/ControllerPromptTests/ControllerPromptTests.csproj" -c Release

stage10_args=()
[[ -z "$APP" ]] || stage10_args+=(--app "$APP" --platform "$PLATFORM")
[[ "$SIGNED" -eq 0 ]] || stage10_args+=(--signed)
[[ -z "$IPA" ]] || stage10_args+=(--ipa "$IPA")
if ((${#stage10_args[@]})); then
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage10b.sh" "${stage10_args[@]}"
else
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage10b.sh"
fi

python_args=(--repo-root "$REPO_ROOT")
[[ -z "$GENERATED_ROOT" ]] || python_args+=(--generated-root "$GENERATED_ROOT")
[[ -z "$APP" ]] || python_args+=(--app "$APP")
[[ -z "$IPA" ]] || python_args+=(--ipa "$IPA")
[[ -z "$OUTPUT" ]] || python_args+=(--output "$OUTPUT")
"$REPO_ROOT/scripts/verify-celeste-tvos-stage11.py" "${python_args[@]}"

echo "PASS: Stage 11 verification complete"
