#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
UI_HELPER="$SCRIPT_DIR/tvos-builder-ui.sh"
COMMAND_LAUNCHER="$SCRIPT_DIR/tvos-builder-command.py"
TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/celeste-stage18b.XXXXXX")"
PASS_COUNT=0

cleanup() {
  rm -rf -- "$TEST_ROOT"
}
trap cleanup EXIT

# shellcheck source=scripts/tvos-builder-ui.sh
source "$UI_HELPER"

fail() {
  printf 'FAIL: %s\n' "$1" >&2
  return 1
}

assert_equal() {
  [[ "$1" == "$2" ]] || fail "$3 (expected '$2', got '$1')"
}

assert_contains() {
  grep -Fq -- "$2" "$1" || fail "$3"
}

assert_not_contains() {
  if grep -Fq -- "$2" "$1"; then fail "$3"; fi
}

run_test() {
  local name="$1"; shift
  if "$@"; then
    PASS_COUNT=$((PASS_COUNT + 1))
  else
    printf 'Stage 18B test failed: %s\n' "$name" >&2
    exit 1
  fi
}

reset_ui() {
  local no_color="${1:-1}" verbose="${2:-0}"
  unset NO_COLOR || true
  unset GITHUB_ACTIONS || true
  unset CELESTE_TVOS_HEARTBEAT_SECONDS || true
  ui_initialize "$no_color" "$verbose" "$REPO_ROOT" "$TEST_ROOT/logs" "$COMMAND_LAUNCHER"
}

test_duration_formatting() {
  assert_equal "$(ui_format_duration 4)" "4s" "seconds formatting" &&
  assert_equal "$(ui_format_duration 68)" "1m 08s" "minute formatting" &&
  assert_equal "$(ui_format_duration 3671)" "1h 01m 11s" "hour formatting"
}

test_heartbeat_default_and_validation() {
  assert_equal "$(ui_valid_heartbeat_interval '')" "60" "empty heartbeat default" &&
  assert_equal "$(ui_valid_heartbeat_interval nope)" "60" "invalid heartbeat default" &&
  assert_equal "$(ui_valid_heartbeat_interval 0)" "60" "zero heartbeat default" &&
  assert_equal "$(ui_valid_heartbeat_interval 7)" "7" "valid heartbeat interval"
}

test_no_color_option() {
  ui_initialize 1 0 "$REPO_ROOT" "$TEST_ROOT/logs" "$COMMAND_LAUNCHER"
  [[ "$UI_STDOUT_COLOR" -eq 0 && "$UI_STDERR_COLOR" -eq 0 ]]
}

test_no_color_environment_presence() {
  NO_COLOR= ui_initialize 0 0 "$REPO_ROOT" "$TEST_ROOT/logs" "$COMMAND_LAUNCHER"
  local result=0
  [[ "$UI_STDOUT_COLOR" -eq 0 && "$UI_STDERR_COLOR" -eq 0 ]] || result=1
  unset NO_COLOR
  return "$result"
}

test_non_tty_has_no_ansi() {
  local output="$TEST_ROOT/non-tty.txt"
  /bin/bash -c 'source "$1"; ui_initialize 0 0 "$2" "$3" "$4"; ui_phase_begin 1 test; ui_phase_success' \
    bash "$UI_HELPER" "$REPO_ROOT" "$TEST_ROOT/logs" "$COMMAND_LAUNCHER" > "$output" 2>&1
  if LC_ALL=C grep -q $'\033' "$output"; then fail "non-TTY output contains ANSI"; fi
}

test_help_options() {
  local output="$TEST_ROOT/help.txt"
  "$REPO_ROOT/build-tvos.sh" --verbose --no-color --help > "$output"
  assert_contains "$output" "--verbose" "help omits --verbose" &&
  assert_contains "$output" "--no-color" "help omits --no-color"
}

test_default_logging_is_concise() {
  local terminal="$TEST_ROOT/default-terminal.txt" log="$TEST_ROOT/logs/default.log"
  reset_ui 1 0
  ui_run_command "$log" "Synthetic default command" sh -c 'printf "child-default-output\\n"' > "$terminal"
  assert_contains "$log" "child-default-output" "default log missed child output" &&
  assert_not_contains "$terminal" "child-default-output" "default terminal leaked child output"
}

test_verbose_tees_and_logs() {
  local terminal="$TEST_ROOT/verbose-terminal.txt" log="$TEST_ROOT/logs/verbose.log"
  reset_ui 1 1
  ui_run_command "$log" "Synthetic verbose command" sh -c 'printf "child-verbose-output\\n"' > "$terminal"
  assert_contains "$terminal" "child-verbose-output" "verbose terminal missed child output" &&
  assert_contains "$log" "child-verbose-output" "verbose log missed child output"
}

test_success_exit_status() {
  reset_ui 1 0
  ui_run_command "$TEST_ROOT/logs/success.log" "Synthetic success" sh -c 'exit 0'
}

test_failure_exit_status() {
  local status
  reset_ui 1 0
  if ui_run_command "$TEST_ROOT/logs/failure.log" "Synthetic failure" sh -c 'exit 23'; then
    fail "failed child returned success"
  else
    status=$?
  fi
  assert_equal "$status" "23" "child failure status"
}

test_heartbeat_appears() {
  local terminal="$TEST_ROOT/heartbeat.txt"
  CELESTE_TVOS_HEARTBEAT_SECONDS=1 ui_initialize 1 0 "$REPO_ROOT" "$TEST_ROOT/logs" "$COMMAND_LAUNCHER"
  ui_run_command "$TEST_ROOT/logs/heartbeat.log" "Synthetic long command" sh -c 'sleep 2' > "$terminal"
  assert_contains "$terminal" "Still working: Synthetic long command" "heartbeat did not appear"
}

test_fast_command_has_no_heartbeat() {
  local terminal="$TEST_ROOT/no-heartbeat.txt"
  CELESTE_TVOS_HEARTBEAT_SECONDS=1 ui_initialize 1 0 "$REPO_ROOT" "$TEST_ROOT/logs" "$COMMAND_LAUNCHER"
  ui_run_command "$TEST_ROOT/logs/no-heartbeat.log" "Synthetic fast command" sh -c 'exit 0' > "$terminal"
  assert_not_contains "$terminal" "Still working:" "fast command emitted a heartbeat"
}

test_heartbeat_state_clears() {
  CELESTE_TVOS_HEARTBEAT_SECONDS=1 ui_initialize 1 0 "$REPO_ROOT" "$TEST_ROOT/logs" "$COMMAND_LAUNCHER"
  ui_run_command "$TEST_ROOT/logs/clear.log" "Synthetic clear" sh -c 'exit 0'
  [[ -z "$UI_ACTIVE_CHILD_PID" && -z "$UI_ACTIVE_HEARTBEAT_PID" && -z "$UI_ACTIVE_TEE_PID" && -z "$UI_ACTIVE_FIFO" ]]
}

test_github_groups_balance_success() {
  local output="$TEST_ROOT/groups-success.txt" begins ends
  reset_ui 1 0
  GITHUB_ACTIONS=true
  { ui_phase_begin 1 "Synthetic group"; ui_phase_success; } > "$output"
  unset GITHUB_ACTIONS
  begins="$(grep -c '^::group::' "$output")"
  ends="$(grep -c '^::endgroup::$' "$output")"
  [[ "$begins" -eq 1 && "$ends" -eq 1 ]]
}

test_github_groups_balance_failure() {
  local output="$TEST_ROOT/groups-failure.txt" begins ends
  reset_ui 1 0
  GITHUB_ACTIONS=true
  { ui_phase_begin 1 "Synthetic group"; ui_phase_failure "expected"; } > "$output" 2>&1
  unset GITHUB_ACTIONS
  begins="$(grep -c '^::group::' "$output")"
  ends="$(grep -c '^::endgroup::$' "$output")"
  [[ "$begins" -eq 1 && "$ends" -eq 1 ]]
}

test_diagnostic_tail_is_bounded() {
  local log="$TEST_ROOT/tail.log" output="$TEST_ROOT/tail-output.txt" count
  awk 'BEGIN {for (i=1;i<=60;i++) print "diagnostic line " i}' > "$log"
  ui_diagnostic_tail "$log" 7 > "$output"
  count="$(wc -l < "$output" | tr -d ' ')"
  assert_equal "$count" "7" "diagnostic tail line count" &&
  assert_contains "$output" "diagnostic line 60" "diagnostic tail omitted final line"
}

test_redaction() {
  local output="$TEST_ROOT/redacted.txt"
  printf '%s\n' '/Users/alice/private /Volumes/FMODSDK/secret 12345678-1234-1234-1234-123456789ABC person@example.com ABCDE12345 com.alice.celeste' | ui_redact > "$output"
  assert_contains "$output" '$HOME/private' "home path not redacted" &&
  assert_contains "$output" '$FMOD_SDK_ROOT/secret' "FMOD path not redacted" &&
  assert_contains "$output" '<DEVICE_ID>' "device ID not redacted" &&
  assert_contains "$output" '<APPLE_ID>' "email not redacted" &&
  assert_contains "$output" '<TEAM_ID>' "team ID not redacted" &&
  assert_contains "$output" '<BUNDLE_ID>' "bundle ID not redacted"
}

test_verbose_failure_status() {
  local status terminal="$TEST_ROOT/verbose-failure.txt"
  reset_ui 1 1
  if ui_run_command "$TEST_ROOT/logs/verbose-failure.log" "Synthetic verbose failure" sh -c 'printf "visible-before-failure\\n"; exit 17' > "$terminal"; then
    fail "verbose failed child returned success"
  else
    status=$?
  fi
  assert_equal "$status" "17" "verbose child failure status" &&
  assert_contains "$terminal" "visible-before-failure" "verbose failure output not streamed"
}

test_no_heartbeat_after_failure() {
  local terminal="$TEST_ROOT/failure-heartbeat.txt"
  CELESTE_TVOS_HEARTBEAT_SECONDS=1 ui_initialize 1 0 "$REPO_ROOT" "$TEST_ROOT/logs" "$COMMAND_LAUNCHER"
  ui_run_command "$TEST_ROOT/logs/quick-failure.log" "Synthetic quick failure" sh -c 'exit 9' > "$terminal" || true
  sleep 2
  assert_not_contains "$terminal" "Still working:" "heartbeat continued after failure" &&
  [[ -z "$UI_ACTIVE_HEARTBEAT_PID" ]]
}

test_interrupt_kills_process_group() {
  local harness="$TEST_ROOT/interrupt-harness.sh" child_file="$TEST_ROOT/interrupt-child.pid"
  printf '%s\n' \
    '#!/bin/bash' \
    'set -euo pipefail' \
    'source "$1"' \
    'ui_initialize 1 0 "$2" "$3" "$4"' \
    'interrupted() { trap - INT TERM HUP; ui_cancel_active_command INT; exit 130; }' \
    "trap 'interrupted' INT" \
    'ui_run_command "$3/interrupt.log" "Synthetic interrupt" sh -c '\''echo $$ > "$1"; exec sleep 30'\'' sh "$5"' \
    > "$harness"
  chmod +x "$harness"
  python3 - "$harness" "$UI_HELPER" "$REPO_ROOT" "$TEST_ROOT/logs" "$COMMAND_LAUNCHER" "$child_file" <<'PY'
import os
import pathlib
import signal
import subprocess
import sys
import time

harness, helper, repo, logs, launcher, child_file = sys.argv[1:]
proc = subprocess.Popen(
    ["/bin/bash", harness, helper, repo, logs, launcher, child_file],
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
    text=True,
)
path = pathlib.Path(child_file)
for _ in range(100):
    if path.is_file() and path.read_text().strip():
        break
    time.sleep(0.02)
else:
    proc.kill()
    raise SystemExit("interrupt child did not start")
child_pid = int(path.read_text().strip())
os.kill(proc.pid, signal.SIGINT)
stdout, stderr = proc.communicate(timeout=8)
if proc.returncode != 130:
    raise SystemExit(f"interrupt status {proc.returncode}; stdout={stdout!r}; stderr={stderr!r}")
try:
    os.kill(child_pid, 0)
except ProcessLookupError:
    pass
else:
    raise SystemExit(f"interrupted child {child_pid} remains alive")
PY
}

test_launcher_exec_failure_status() {
  local status
  reset_ui 1 0
  if ui_run_command "$TEST_ROOT/logs/no-command.log" "Missing command" "$TEST_ROOT/does-not-exist"; then
    fail "missing command returned success"
  else
    status=$?
  fi
  assert_equal "$status" "127" "missing command status"
}

run_test "duration formatting" test_duration_formatting
run_test "heartbeat default and validation" test_heartbeat_default_and_validation
run_test "--no-color policy" test_no_color_option
run_test "NO_COLOR policy" test_no_color_environment_presence
run_test "non-TTY ANSI policy" test_non_tty_has_no_ansi
run_test "help options" test_help_options
run_test "default command logging" test_default_logging_is_concise
run_test "verbose tee and logging" test_verbose_tees_and_logs
run_test "success exit status" test_success_exit_status
run_test "failure exit status" test_failure_exit_status
run_test "heartbeat appears" test_heartbeat_appears
run_test "fast command heartbeat suppression" test_fast_command_has_no_heartbeat
run_test "heartbeat state cleanup" test_heartbeat_state_clears
run_test "GitHub success groups" test_github_groups_balance_success
run_test "GitHub failure groups" test_github_groups_balance_failure
run_test "bounded diagnostic tail" test_diagnostic_tail_is_bounded
run_test "privacy redaction" test_redaction
run_test "verbose failure status" test_verbose_failure_status
run_test "failure heartbeat cleanup" test_no_heartbeat_after_failure
run_test "interrupt process-group cleanup" test_interrupt_kills_process_group
run_test "launcher exec failure status" test_launcher_exec_failure_status

printf 'PASS: Stage 18B builder UI (%d deterministic tests)\n' "$PASS_COUNT"
