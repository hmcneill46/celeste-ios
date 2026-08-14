#!/usr/bin/env bash
# Shared Stage 18B builder presentation and logged-command helpers.
# Keep this file compatible with the macOS system Bash 3.2.

UI_VERBOSE=0
UI_NO_COLOR_OPTION=0
UI_STDOUT_COLOR=0
UI_STDERR_COLOR=0
UI_HEARTBEAT_SECONDS=60
UI_REPO_ROOT=""
UI_LOG_ROOT=""
UI_COMMAND_LAUNCHER=""
UI_INITIALIZED=0
UI_BUILDER_STARTED_AT=0
UI_PHASE_STARTED_AT=0
UI_PHASE_NUMBER=""
UI_PHASE_TITLE=""
UI_PHASE_OPEN=0
UI_GROUP_OPEN=0
UI_CURRENT_OPERATION=""
UI_ACTIVE_CHILD_PID=""
UI_ACTIVE_HEARTBEAT_PID=""
UI_ACTIVE_TEE_PID=""
UI_ACTIVE_FIFO=""
UI_CYAN='\033[36m'
UI_GREEN='\033[32m'
UI_YELLOW='\033[33m'
UI_RED='\033[31m'
UI_RESET='\033[0m'
UI_ACTIVE_MARKER='*'
UI_SUCCESS_MARKER='OK'
UI_FAILURE_MARKER='X'
UI_WARNING_MARKER='!'

ui_now_seconds() {
  # Bash maintains SECONDS for the lifetime of this builder process.  Using it
  # avoids wall-clock changes and works in the macOS system Bash 3.2 without
  # starting a timing subprocess for every status update.
  printf '%s\n' "$SECONDS"
}

ui_format_duration() {
  local seconds="$1" hours minutes remainder
  [[ "$seconds" =~ ^[0-9]+$ ]] || seconds=0
  if [[ "$seconds" -lt 60 ]]; then
    printf '%ss\n' "$seconds"
  elif [[ "$seconds" -lt 3600 ]]; then
    minutes=$((seconds / 60))
    remainder=$((seconds % 60))
    printf '%dm %02ds\n' "$minutes" "$remainder"
  else
    hours=$((seconds / 3600))
    minutes=$(((seconds % 3600) / 60))
    remainder=$((seconds % 60))
    printf '%dh %02dm %02ds\n' "$hours" "$minutes" "$remainder"
  fi
}

ui_valid_heartbeat_interval() {
  local value="$1"
  case "$value" in
    ''|*[!0-9]*|0) printf '60\n' ;;
    *) printf '%s\n' "$value" ;;
  esac
}

ui_initialize() {
  local no_color_option="$1" verbose="$2" repo_root="$3" log_root="$4" launcher="$5"
  local requested_interval="${CELESTE_TVOS_HEARTBEAT_SECONDS:-60}" charmap=""
  UI_NO_COLOR_OPTION="$no_color_option"
  UI_VERBOSE="$verbose"
  UI_REPO_ROOT="$repo_root"
  UI_LOG_ROOT="$log_root"
  UI_COMMAND_LAUNCHER="$launcher"
  UI_HEARTBEAT_SECONDS="$(ui_valid_heartbeat_interval "$requested_interval")"
  UI_BUILDER_STARTED_AT="$(ui_now_seconds)"
  UI_INITIALIZED=1
  UI_STDOUT_COLOR=0
  UI_STDERR_COLOR=0
  if [[ "$UI_NO_COLOR_OPTION" -eq 0 && "${NO_COLOR+x}" != x ]]; then
    [[ -t 1 ]] && UI_STDOUT_COLOR=1
    [[ -t 2 ]] && UI_STDERR_COLOR=1
  fi
  charmap="$(locale charmap 2>/dev/null || true)"
  case "$charmap" in
    *UTF-8*|*utf8*)
      UI_ACTIVE_MARKER='●'
      UI_SUCCESS_MARKER='✓'
      UI_FAILURE_MARKER='✗'
      UI_WARNING_MARKER='!'
      ;;
    *)
      UI_ACTIVE_MARKER='*'
      UI_SUCCESS_MARKER='OK'
      UI_FAILURE_MARKER='X'
      UI_WARNING_MARKER='!'
      ;;
  esac
}

ui_stdout_line() {
  local colour="$1" text="$2"
  if [[ "$UI_STDOUT_COLOR" -eq 1 ]]; then
    printf '%b%s%b\n' "$colour" "$text" "$UI_RESET"
  else
    printf '%s\n' "$text"
  fi
}

ui_stderr_line() {
  local colour="$1" text="$2"
  if [[ "$UI_STDERR_COLOR" -eq 1 ]]; then
    printf '%b%s%b\n' "$colour" "$text" "$UI_RESET" >&2
  else
    printf '%s\n' "$text" >&2
  fi
}

ui_group_begin() {
  local title="$1"
  if [[ "${GITHUB_ACTIONS:-}" == true && "$UI_GROUP_OPEN" -eq 0 ]]; then
    printf '::group::%s\n' "$title"
    UI_GROUP_OPEN=1
  fi
}

ui_group_end() {
  if [[ "$UI_GROUP_OPEN" -eq 1 ]]; then
    printf '::endgroup::\n'
    UI_GROUP_OPEN=0
  fi
}

ui_phase_begin() {
  UI_PHASE_NUMBER="$1"
  UI_PHASE_TITLE="$2"
  UI_PHASE_STARTED_AT="$(ui_now_seconds)"
  UI_PHASE_OPEN=1
  ui_group_begin "[$UI_PHASE_NUMBER/8] $UI_PHASE_TITLE"
  ui_stdout_line "$UI_CYAN" "$UI_ACTIVE_MARKER [$UI_PHASE_NUMBER/8] $UI_PHASE_TITLE"
}

ui_phase_success() {
  local now elapsed duration
  [[ "$UI_PHASE_OPEN" -eq 1 ]] || return 0
  now="$(ui_now_seconds)"
  elapsed=$((now - UI_PHASE_STARTED_AT))
  duration="$(ui_format_duration "$elapsed")"
  ui_stdout_line "$UI_GREEN" "$UI_SUCCESS_MARKER [$UI_PHASE_NUMBER/8] $UI_PHASE_TITLE — $duration"
  UI_PHASE_OPEN=0
  ui_group_end
}

ui_phase_skip() {
  local number="$1" title="$2" reason="$3"
  ui_phase_begin "$number" "$title"
  ui_stdout_line "$UI_YELLOW" "  skipped — $reason"
  ui_phase_success
}

ui_phase_failure() {
  local problem="$1" now elapsed duration
  [[ "$UI_PHASE_OPEN" -eq 1 ]] || { ui_group_end; return 0; }
  now="$(ui_now_seconds)"
  elapsed=$((now - UI_PHASE_STARTED_AT))
  duration="$(ui_format_duration "$elapsed")"
  ui_stderr_line "$UI_RED" "$UI_FAILURE_MARKER [$UI_PHASE_NUMBER/8] $UI_PHASE_TITLE failed after $duration"
  [[ -z "$problem" ]] || printf '  %s\n' "$problem" >&2
  UI_PHASE_OPEN=0
  ui_group_end
}

ui_total_elapsed() {
  local now
  now="$(ui_now_seconds)"
  ui_format_duration "$((now - UI_BUILDER_STARTED_AT))"
}

ui_build_success() {
  local message="$1" elapsed
  elapsed="$(ui_total_elapsed)"
  ui_stdout_line "$UI_GREEN" "$UI_SUCCESS_MARKER $message in $elapsed."
}

ui_warning() {
  ui_stdout_line "$UI_YELLOW" "$UI_WARNING_MARKER $1"
}

ui_operation_name() {
  case "$1" in
    fetch-native) printf 'Fetching native dependency sources\n' ;;
    build-native) printf 'Building native tvOS dependencies\n' ;;
    verify-native) printf 'Verifying native tvOS dependencies\n' ;;
    prepare-host-native) printf 'Preparing native host libraries\n' ;;
    validate-celeste) printf 'Validating Celeste input\n' ;;
    validate-fmod) printf 'Validating FMOD input\n' ;;
    prepare-fmod) printf 'Preparing FMOD for tvOS\n' ;;
    prepare-game) printf 'Generating canonical Celeste sources and content\n' ;;
    verify-game) printf 'Verifying canonical Celeste sources and content\n' ;;
    generate-artwork) printf 'Generating Apple TV artwork\n' ;;
    publish-unsigned) printf 'Publishing the unsigned full-AOT app\n' ;;
    publish-signed) printf 'Publishing the signed full-AOT app\n' ;;
    install-apple-tv) printf 'Installing Celeste on Apple TV\n' ;;
    relaunch-apple-tv) printf 'Launching Celeste on Apple TV\n' ;;
    verify-*) printf 'Running %s\n' "${1#verify-}" ;;
    sanitize-*) printf 'Sanitising %s\n' "${1#sanitize-}" ;;
    provision-*) printf 'Preparing %s\n' "${1#provision-}" ;;
    *) printf 'Running %s\n' "${1//-/ }" ;;
  esac
}

ui_operation_begin() {
  UI_CURRENT_OPERATION="$1"
  ui_stdout_line "$UI_CYAN" "  $UI_ACTIVE_MARKER $UI_CURRENT_OPERATION"
}

ui_operation_success() {
  local operation="$1" seconds="$2" duration
  duration="$(ui_format_duration "$seconds")"
  ui_stdout_line "$UI_GREEN" "  $UI_SUCCESS_MARKER $operation — $duration"
  UI_CURRENT_OPERATION=""
}

ui_operation_failure() {
  local operation="$1" seconds="$2" duration
  duration="$(ui_format_duration "$seconds")"
  ui_stderr_line "$UI_RED" "  $UI_FAILURE_MARKER $operation failed after $duration"
}

ui_redact() {
  /usr/bin/sed -E \
    -e 's#/Users/[^/[:space:]]+#$HOME#g' \
    -e 's#/Volumes/[^/[:space:]]+#$FMOD_SDK_ROOT#g' \
    -e 's#[A-Fa-f0-9]{8}-[A-Fa-f0-9-]{27,}#<DEVICE_ID>#g' \
    -e 's#[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}#<APPLE_ID>#g' \
    -e 's#(^|[^A-Z0-9])[A-Z0-9]{10}([^A-Z0-9]|$)#\1<TEAM_ID>\2#g' \
    -e 's#(com|org|net)\.[A-Za-z0-9.-]+#<BUNDLE_ID>#g'
}

ui_diagnostic_tail() {
  local log="$1" lines="${2:-40}"
  [[ -f "$log" ]] || return 0
  tail -n "$lines" "$log" | ui_redact
}

ui_write_command_line() {
  local log="$1"; shift
  {
    printf '+ '
    printf '%q ' "$@"
    printf '\n'
  } >> "$log"
}

ui_free_disk_gib() {
  local free_kib
  free_kib="$(df -Pk "$UI_REPO_ROOT" 2>/dev/null | awk 'NR==2 {print $4}')"
  case "$free_kib" in
    ''|*[!0-9]*) return 1 ;;
    *) printf '%s\n' "$((free_kib / 1024 / 1024))" ;;
  esac
}

ui_heartbeat_loop() {
  local child_pid="$1" operation="$2" started_at="$3" sleep_pid="" now elapsed duration free_gib
  trap 'if [[ -n "$sleep_pid" ]]; then kill -TERM "$sleep_pid" 2>/dev/null || true; wait "$sleep_pid" 2>/dev/null || true; fi; exit 0' TERM INT HUP
  while kill -0 "$child_pid" 2>/dev/null; do
    sleep "$UI_HEARTBEAT_SECONDS" &
    sleep_pid=$!
    wait "$sleep_pid" 2>/dev/null || exit 0
    sleep_pid=""
    kill -0 "$child_pid" 2>/dev/null || exit 0
    now="$(ui_now_seconds)"
    elapsed=$((now - started_at))
    duration="$(ui_format_duration "$elapsed")"
    free_gib="$(ui_free_disk_gib 2>/dev/null || true)"
    if [[ -n "$free_gib" ]]; then
      printf '    Still working: %s — %s elapsed · %s GiB free\n' "$operation" "$duration" "$free_gib"
    else
      printf '    Still working: %s — %s elapsed\n' "$operation" "$duration"
    fi
  done
}

ui_stop_heartbeat() {
  local had_errexit=0
  [[ -n "$UI_ACTIVE_HEARTBEAT_PID" ]] || return 0
  case $- in *e*) had_errexit=1 ;; esac
  kill -TERM "$UI_ACTIVE_HEARTBEAT_PID" 2>/dev/null || true
  set +e
  wait "$UI_ACTIVE_HEARTBEAT_PID" 2>/dev/null
  [[ "$had_errexit" -eq 0 ]] || set -e
  UI_ACTIVE_HEARTBEAT_PID=""
}

ui_run_command() {
  local log="$1" operation="$2"; shift 2
  local started_at child_status tee_status=0 had_errexit=0 fifo=""
  mkdir -p "$(dirname "$log")"
  : > "$log"
  ui_write_command_line "$log" "$@"
  if [[ "$UI_VERBOSE" -eq 1 ]]; then
    cat "$log"
    fifo="${log}.pipe.$$.$RANDOM"
    mkfifo "$fifo"
    UI_ACTIVE_FIFO="$fifo"
    tee -a "$log" < "$fifo" &
    UI_ACTIVE_TEE_PID=$!
    "$UI_COMMAND_LAUNCHER" "$@" > "$fifo" 2>&1 &
  else
    "$UI_COMMAND_LAUNCHER" "$@" >> "$log" 2>&1 &
  fi
  UI_ACTIVE_CHILD_PID=$!
  started_at="$(ui_now_seconds)"
  ui_heartbeat_loop "$UI_ACTIVE_CHILD_PID" "$operation" "$started_at" &
  UI_ACTIVE_HEARTBEAT_PID=$!

  case $- in *e*) had_errexit=1 ;; esac
  set +e
  wait "$UI_ACTIVE_CHILD_PID"
  child_status=$?
  [[ "$had_errexit" -eq 0 ]] || set -e
  UI_ACTIVE_CHILD_PID=""
  ui_stop_heartbeat

  if [[ -n "$UI_ACTIVE_TEE_PID" ]]; then
    had_errexit=0
    case $- in *e*) had_errexit=1 ;; esac
    set +e
    wait "$UI_ACTIVE_TEE_PID"
    tee_status=$?
    [[ "$had_errexit" -eq 0 ]] || set -e
    UI_ACTIVE_TEE_PID=""
  fi
  if [[ -n "$fifo" ]]; then
    rm -f -- "$fifo"
    UI_ACTIVE_FIFO=""
  fi
  if [[ "$child_status" -eq 0 && "$tee_status" -ne 0 ]]; then
    return "$tee_status"
  fi
  return "$child_status"
}

ui_cancel_active_command() {
  local signal_name="${1:-TERM}" pid="$UI_ACTIVE_CHILD_PID" count
  ui_stop_heartbeat
  if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
    kill -s "$signal_name" -- "-$pid" 2>/dev/null || kill -s "$signal_name" "$pid" 2>/dev/null || true
    for count in 1 2 3; do
      kill -0 "$pid" 2>/dev/null || break
      sleep 1
    done
    if kill -0 "$pid" 2>/dev/null; then
      kill -KILL -- "-$pid" 2>/dev/null || kill -KILL "$pid" 2>/dev/null || true
    fi
    wait "$pid" 2>/dev/null || true
  fi
  UI_ACTIVE_CHILD_PID=""
  if [[ -n "$UI_ACTIVE_TEE_PID" ]]; then
    kill -TERM "$UI_ACTIVE_TEE_PID" 2>/dev/null || true
    wait "$UI_ACTIVE_TEE_PID" 2>/dev/null || true
    UI_ACTIVE_TEE_PID=""
  fi
  if [[ -n "$UI_ACTIVE_FIFO" ]]; then
    rm -f -- "$UI_ACTIVE_FIFO"
    UI_ACTIVE_FIFO=""
  fi
}
