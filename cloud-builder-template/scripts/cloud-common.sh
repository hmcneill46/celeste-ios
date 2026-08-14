#!/usr/bin/env bash
# Shared, bounded orchestration for the private Celeste tvOS cloud builder.
set -euo pipefail

readonly CLOUD_INPUT_TAG="celeste-tvos-inputs"
readonly CLOUD_OUTPUT_TAG="celeste-tvos-output"
readonly CLOUD_IPA_NAME="Celeste-tvOS-unsigned.ipa"
readonly CLOUD_METADATA_NAME="Celeste-tvOS-build.txt"
readonly CLOUD_CACHE_PREFIX="celeste-tvos-safe-native-v1-"
readonly CLOUD_PUBLIC_REPOSITORY="hmcneill46/celeste-ios"
readonly CLOUD_PUBLIC_SOURCE_SHA="90ebb023f3043222bc67e72922ae4d68223f009c"
readonly CLOUD_NATIVE_LOGICAL_SHA256="6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
readonly CLOUD_MINIMUM_FREE_GIB=25

cloud_state_root() {
  printf '%s\n' "${RUNNER_TEMP:-/tmp}/celeste-tvos-cloud-state"
}

cloud_set_stage() {
  local title="$1" remedy="$2" root
  root="$(cloud_state_root)"
  mkdir -p "$root"
  printf '%s\n' "$title" > "$root/stage.txt"
  printf '%s\n' "$remedy" > "$root/remedy.txt"
}

cloud_fail() {
  local title="$1" remedy="$2"
  cloud_set_stage "$title" "$remedy"
  printf 'error: %s\n' "$title" >&2
  printf '       %s\n' "$remedy" >&2
  return 1
}

cloud_require_private_repository() {
  local metadata
  metadata="$(gh api "repos/$GITHUB_REPOSITORY")" || \
    cloud_fail "GitHub could not verify repository privacy." \
      "Retry after GitHub API access is available. No private input was downloaded."
  if ! REPOSITORY_METADATA="$metadata" python3 - <<'PY'
import json, os
value = json.loads(os.environ["REPOSITORY_METADATA"])
if value.get("private") is not True or value.get("visibility") != "private":
    raise SystemExit(1)
PY
  then
    cloud_fail \
      "This workflow only runs in a private repository." \
      "Create a private repository from the template before uploading Celeste or FMOD."
  fi
}

cloud_release_or_tag_exists() {
  local tag="$1"
  if gh release view "$tag" --repo "$GITHUB_REPOSITORY" >/dev/null 2>&1; then
    return 0
  fi
  gh api "repos/$GITHUB_REPOSITORY/git/ref/tags/$tag" >/dev/null 2>&1
}

cloud_ensure_output_absent() {
  if cloud_release_or_tag_exists "$CLOUD_OUTPUT_TAG"; then
    cloud_fail \
      "The celeste-tvos-output Release already exists." \
      "Download it if wanted, run Clean private build files, then run Build again."
  fi
}

cloud_validate_cleanup_tag() {
  case "$1" in
    "$CLOUD_INPUT_TAG"|"$CLOUD_OUTPUT_TAG") return 0 ;;
    *) printf 'error: cleanup tag is not allow-listed: %s\n' "$1" >&2; return 1 ;;
  esac
}

cloud_delete_release_and_tag() {
  local tag="$1"
  cloud_validate_cleanup_tag "$tag"
  if gh release view "$tag" --repo "$GITHUB_REPOSITORY" >/dev/null 2>&1; then
    gh release delete "$tag" --repo "$GITHUB_REPOSITORY" --cleanup-tag --yes >/dev/null
    printf 'removed\n'
    return 0
  fi
  if gh api "repos/$GITHUB_REPOSITORY/git/ref/tags/$tag" >/dev/null 2>&1; then
    gh api --method DELETE "repos/$GITHUB_REPOSITORY/git/refs/tags/$tag" >/dev/null
    printf 'removed-tag-only\n'
    return 0
  fi
  printf 'not-present\n'
}

cloud_delete_safe_caches() {
  local state ids_file cache_id count=0
  state="$(cloud_state_root)"
  mkdir -p "$state"
  ids_file="$state/cache-ids.txt"
  gh api --paginate --slurp "repos/$GITHUB_REPOSITORY/actions/caches?per_page=100" > "$state/caches.json"
  python3 - "$state/caches.json" "$CLOUD_CACHE_PREFIX" > "$ids_file" <<'PY'
import json, pathlib, sys
pages = json.loads(pathlib.Path(sys.argv[1]).read_text())
prefix = sys.argv[2]
for page in pages:
    for item in page.get("actions_caches", []):
        if str(item.get("key", "")).startswith(prefix):
            print(int(item["id"]))
PY
  while IFS= read -r cache_id; do
    [[ -n "$cache_id" ]] || continue
    gh api --method DELETE "repos/$GITHUB_REPOSITORY/actions/caches/$cache_id" >/dev/null
    count=$((count + 1))
  done < "$ids_file"
  printf '%s\n' "$count"
}

cloud_compute_cache_key() {
  local source_root="$1" image_version native_inputs_hash
  image_version="${ImageVersion:-unknown-image}"
  native_inputs_hash="$({
    git -C "$source_root" ls-files -z -- \
      native/tvos-dependencies.lock.json native/tvos-symbol-expectations.json \
      native/patches scripts/fetch-tvos-deps.sh scripts/build-tvos-native.sh \
      scripts/verify-tvos-native.sh scripts/verify-tvos-artifacts.py \
      scripts/prepare-tvos-host-native.sh tvos/fna-managed-sources.lock.json \
      tvos/patches/FNA | (
        cd "$source_root"
        LC_ALL=C sort -z | xargs -0 shasum -a 256
      )
  } | shasum -a 256 | awk '{print $1}')"
  printf '%s%s-%s-%s-%s-xcode26.6-tvos26.5-dotnet10.0.302-%s-%s\n' \
    "$CLOUD_CACHE_PREFIX" "$CLOUD_PUBLIC_SOURCE_SHA" "${RUNNER_OS:-macOS}" \
    "${RUNNER_ARCH:-ARM64}" "$image_version" "$CLOUD_NATIVE_LOGICAL_SHA256" \
    "$native_inputs_hash"
}

cloud_verify_safe_cache_paths() {
  local source_root="$1"
  python3 - "$source_root" "$CLOUD_NATIVE_LOGICAL_SHA256" <<'PY'
import json, pathlib, sys
root = pathlib.Path(sys.argv[1])
expected = sys.argv[2]
paths = [
    root / "artifacts/tvos-native/self-build/normalized-manifest.json",
    root / ".build/tvos-host/normalized-manifest.json",
]
for path in paths:
    if not path.is_file():
        raise SystemExit(f"error: safe cache is missing {path.relative_to(root)}")
    if json.loads(path.read_text()).get("logicalSetSha256") != expected:
        raise SystemExit(f"error: safe cache has the wrong native logical hash: {path.relative_to(root)}")
PY
  "$source_root/scripts/verify-tvos-host.sh" --stage-dir "$source_root/.build/tvos-host"
}

cloud_capture_builder_failure() {
  local source_root="${1:-}" state
  state="$(cloud_state_root)"
  mkdir -p "$state"
  if [[ -n "$source_root" && -f "$source_root/dist/logs/last-error.txt" ]]; then
    tail -n 40 "$source_root/dist/logs/last-error.txt" > "$state/builder-failure.txt" || true
  fi
}

cloud_safe_remove() {
  local target="$1" runner_temp_resolved
  [[ -n "${RUNNER_TEMP:-}" && -n "$target" ]] || return 0
  runner_temp_resolved="$(cd "$RUNNER_TEMP" && pwd -P)"
  case "$target" in
    "$runner_temp_resolved"/celeste-tvos-cloud-*) ;;
    *) printf 'warning: refusing cloud cleanup outside the bounded runner root: %s\n' "$target" >&2; return 1 ;;
  esac
  rm -rf -- "$target"
}

cloud_runner_cleanup() {
  local mount="${CLOUD_FMOD_MOUNT:-${RUNNER_TEMP:-/tmp}/celeste-tvos-cloud-fmod}"
  set +e
  if [[ -d "$mount" ]]; then
    hdiutil detach "$mount" -force >/dev/null 2>&1
  fi
  cloud_capture_builder_failure "${CELESTE_SOURCE_ROOT:-}"
  cloud_safe_remove "${RUNNER_TEMP}/celeste-tvos-cloud-inputs"
  cloud_safe_remove "${RUNNER_TEMP}/celeste-tvos-cloud-game"
  cloud_safe_remove "${RUNNER_TEMP}/celeste-tvos-cloud-fmod"
  cloud_safe_remove "${RUNNER_TEMP}/celeste-tvos-cloud-source"
  cloud_safe_remove "${RUNNER_TEMP}/celeste-tvos-cloud-output"
  rm -f -- "${RUNNER_TEMP}/celeste-tvos-dotnet-install.sh"
  set -e
}

cloud_write_failure_summary() {
  local summary="${GITHUB_STEP_SUMMARY:-/dev/stdout}" state stage remedy
  state="$(cloud_state_root)"
  stage="$(cat "$state/stage.txt" 2>/dev/null || printf 'The cloud build did not complete.')"
  remedy="$(cat "$state/remedy.txt" 2>/dev/null || printf 'Open the failed step for its first meaningful error, correct it, and run the workflow again.')"
  {
    printf '# Celeste for Apple TV — Build stopped\n\n'
    printf '**Problem:** %s\n\n' "$stage"
    printf '**What to do:** %s\n\n' "$remedy"
    printf 'Your private `%s` input Release was not deleted automatically, so it remains available for a retry.\n' "$CLOUD_INPUT_TAG"
    if [[ -s "$state/builder-failure.txt" ]]; then
      printf '\n<details><summary>Builder failure summary</summary>\n\n```text\n'
      cat "$state/builder-failure.txt"
      printf '```\n</details>\n'
    fi
  } >> "$summary"
}

cloud_write_success_summary() {
  local metadata="$1" summary="${GITHUB_STEP_SUMMARY:-/dev/stdout}" release_url
  release_url="https://github.com/$GITHUB_REPOSITORY/releases/tag/$CLOUD_OUTPUT_TAG"
  {
    printf '# Celeste for Apple TV — Build complete\n\n'
    printf '✓ Celeste input detected and validated\n\n'
    printf '✓ Full AOT\n\n✓ Full trimming\n\n✓ Interpreter disabled\n\n✓ IPA verified\n\n'
    printf '## Download\n\n[%s](%s)\n\n' "$CLOUD_OUTPUT_TAG" "$release_url"
    printf 'Download `%s`.\n\n' "$CLOUD_IPA_NAME"
    sed -n '/^Celeste profile:/p;/^IPA bytes:/p;/^IPA SHA-256:/p;/^Public source commit:/p' "$metadata"
    printf '\n## Next\n\n1. Download the unsigned IPA.\n'
    printf '2. Run **Clean private build files**.\n'
    printf '3. Sign and install the IPA using your preferred tvOS signing method.\n\n'
    printf 'The IPA is unsigned and cannot be installed as-is.\n'
  } >> "$summary"
}

cloud_write_cleanup_summary() {
  local input_result="$1" output_result="$2" cache_result="$3" summary="${GITHUB_STEP_SUMMARY:-/dev/stdout}"
  {
    printf '# Private build-file cleanup complete\n\n'
    printf -- '- Input Release/tag: `%s`\n' "$input_result"
    printf -- '- Output Release/tag: `%s`\n' "$output_result"
    printf -- '- Safe build cache: `%s`\n' "$cache_result"
    printf -- '- Actions artifacts: not used by this project\n'
  } >> "$summary"
}

cloud_usage() {
  cat <<'EOF'
Usage: scripts/cloud-common.sh COMMAND [arguments]

Commands:
  require-private
  ensure-output-absent
  cache-key SOURCE_ROOT
  verify-safe-cache SOURCE_ROOT
  delete-release TAG
  delete-safe-caches
  runner-cleanup
  failure-summary
  success-summary METADATA_FILE
EOF
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  command="${1:-}"
  case "$command" in
    require-private) cloud_require_private_repository ;;
    ensure-output-absent) cloud_ensure_output_absent ;;
    cache-key) [[ $# -eq 2 ]] || { cloud_usage >&2; exit 2; }; cloud_compute_cache_key "$2" ;;
    verify-safe-cache) [[ $# -eq 2 ]] || { cloud_usage >&2; exit 2; }; cloud_verify_safe_cache_paths "$2" ;;
    delete-release) [[ $# -eq 2 ]] || { cloud_usage >&2; exit 2; }; cloud_delete_release_and_tag "$2" ;;
    delete-safe-caches) cloud_delete_safe_caches ;;
    runner-cleanup) cloud_runner_cleanup ;;
    failure-summary) cloud_write_failure_summary ;;
    success-summary) [[ $# -eq 2 ]] || { cloud_usage >&2; exit 2; }; cloud_write_success_summary "$2" ;;
    *) cloud_usage >&2; exit 2 ;;
  esac
fi
