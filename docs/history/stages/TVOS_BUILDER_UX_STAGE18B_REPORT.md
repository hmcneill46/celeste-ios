# tvOS builder progress and diagnostics — Stage 18B

Status: **PASS**

This report records a presentation/diagnostics-only improvement to the public
tvOS self-builder. No Celeste runtime, generated game transformation, native
dependency, persistence, controller, audio, signing, or package semantics were
changed.

## Baseline and scope

- Starting commit: `cc77e51ae2e770a075c081cd5569ba218e6491fd`.
- Feature branch: `feature/builder-progress-ui`.
- Final feature commit: the final commit containing this report (recorded in
  the Stage 18B completion response and branch history).
- Immutable `v1.0.0-rc.1^{}` remained
  `ee52b0868df091746f134d95d4f020f94f23d4fb`.
- `tvos-port` remained at the starting commit throughout this feature stage.

Stage 18A measured an approximately 18m51s fresh native build, a 7–8 minute
full-AOT publish, a 33-minute uncached cloud job, and a 15-minute cached cloud
job. Those healthy operations previously wrote only to logs and could appear
stalled. Stage 18B retains the existing eight phases and adds honest elapsed
status without a synthetic percentage.

## UI architecture

`build-tvos.sh` sources a focused Bash 3.2-compatible helper at
`scripts/tvos-builder-ui.sh`. It owns:

- eight phase begin/success/failure markers;
- phase and whole-builder elapsed time using Bash's process-scoped `SECONDS`;
- compact duration rendering (`4s`, `1m 08s`, `1h 01m 11s`);
- restrained interactive colour and Unicode/ASCII marker selection;
- optional GitHub Actions groups;
- operation status, heartbeat, log teeing, diagnostic tails, and redaction.

Every normal phase prints an active line followed by a completion line with
elapsed time. Reused and skipped work is still reported only where the builder
already has authoritative evidence. There is no 0–100% progress calculation.

## Heartbeat and process handling

Logged commands default to one privacy-safe heartbeat every **60 seconds**.
Each heartbeat contains the operation name, elapsed time, and rounded free
disk. Tests can set an internal positive interval without changing the public
default. Fast commands finish without a heartbeat.

A tiny Python launcher calls public `os.setsid()` and then `os.execvp()` so the
actual child and its descendants occupy one private process group. The builder
retains the direct child status. On INT, TERM, or HUP it stops and reaps the
heartbeat, forwards the signal to the complete child group, drains/stops any
verbose tee, removes its FIFO, and exits with the signal-appropriate status.
The deterministic interrupt test proved the synthetic long child no longer
existed after SIGINT and no false completion line was printed.

The heartbeat sleeps for the full interval rather than polling. Its termination
trap also terminates and reaps the current sleep process, avoiding an orphan
helper after fast completion, failure, or interruption.

## Default and verbose logging

Default mode remains concise. A command line and all child stdout/stderr go to
the existing `dist/logs/<operation>.log`; the terminal shows operation start,
heartbeat when needed, and completion.

`--verbose` streams the same child output through a bounded FIFO/`tee` path
while retaining the complete log. The child is not placed in a status-changing
shell pipeline, so its exact exit code remains authoritative. Synthetic
success, status 23, status 17 under verbose, and command-not-found status 127
all passed.

## Failure diagnostics

Failed logged commands immediately print:

- an unmistakable failed-operation line with elapsed time;
- the final 40 privacy-redacted diagnostic lines;
- the complete phase-log path;
- the existing structured stop block.

`dist/logs/last-error.txt` remains deliberately small and now adds the active
operation and elapsed time. It continues to record the phase, problem,
detected/required state, remedy, and full-log reference. Representative home
and FMOD paths, device UUID, Apple-account email, Team ID, and bundle ID all
redacted in deterministic and integrated failure tests.

## Colour and CI policy

- Interactive stdout/stderr uses restrained cyan active, green success,
  yellow warning, and red failure status.
- `--no-color` disables builder ANSI output.
- Presence of `NO_COLOR`, including an empty value, disables colour.
- Redirected and non-TTY output emits ordinary newline-delimited plain text.
- No spinner, backspace animation, or carriage-return progress is used.
- `GITHUB_ACTIONS=true` wraps each existing phase in exactly one balanced
  `::group::` / `::endgroup::`; failure also closes the group.

The local TTY, explicit no-colour TTY, `NO_COLOR`, redirected output, and eight
successfully balanced GitHub Actions phase groups all passed.

## Broken-pipe and terminal-noise correction

The former `xcodebuild -version | head -1` calls could close Xcode 26.6's output
early and cause a harmless but alarming broken-pipe exception. The builder now
captures the complete version output once, derives its first line in Bash, and
reuses it in terminal and summary output. No Xcode stderr is suppressed.

The host check also captures `dotnet workload list` once and validates that
complete text. Its short version/workload information queries use `TERM=dumb`,
preventing the .NET CLI from writing cursor-mode controls during an explicit
no-colour TTY run. Validation semantics are unchanged.

## Deterministic and fast-path results

- Stage 18B deterministic UI tests: **21 PASS**.
- macOS system Bash: **3.2.57 PASS**.
- `bash -n` across modified shell scripts: **PASS**.
- `./build-tvos.sh --help`: **PASS**; both new options documented.
- `./build-tvos.sh --check-host`: **PASS**.
- Real validation-only mode: **PASS**.
- Detected input: Steam / Windows / FNA 1.4.0.0, exact manifest profile.
- Canonical class: `celeste-1.4.0.0-a`.
- FMOD 1.10.09 build 97915 validation: **PASS**.
- Verbose live output plus retained log: **PASS**.
- Redirected/no-colour output: **PASS** with no ANSI controls.
- GitHub Actions local simulation: **PASS**, eight balanced groups.
- Synthetic interrupt/process-group cleanup: **PASS**.

## Real unsigned build

One normal, non-verbose production IPA build used valid accepted caches and
completed successfully:

- total builder time: **4m44s**;
- full-AOT publish operation: **3m07s**;
- maximum observed quiet interval: **60.035s**;
- heartbeats observed at 1m00s, 2m00s, and 3m00s during AOT;
- IPA bytes: **895,768,323**;
- IPA SHA-256:
  `3fcc1d80199c7eab2108b6a121609fa609184f0f7935ce2321db0746e4813360`;
- Release `tvos-arm64`: **PASS**;
- full trimming: **PASS**;
- full AOT: **PASS**;
- `UseInterpreter=false`: **PASS**;
- semantic package/product verifiers: **PASS**.

The accepted native logical hash remained exactly:

`6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`

## Documentation and privacy

README, BUILDING, and TROUBLESHOOTING now explain timed phases, the 60-second
heartbeat, `--verbose`, `--no-color`, `NO_COLOR`, full logs, bounded failure
tails, and CI/non-TTY behavior. The history index links this report.

No game files, FMOD input, generated Celeste source, IPA, logs, saves, local
configuration, credentials, signing values, or device data are tracked.
Repository/privacy and documentation-link verification passed.

## Known limitations

- Phase and heartbeat timing is intentionally whole-second and descriptive,
  not a build-progress prediction.
- Free disk is rounded and sampled only at heartbeat intervals.
- Heartbeats cover commands owned by the builder's logged-command mechanism;
  future cloud input download, cache restore, output upload, and cleanup remain
  Stage 18C workflow responsibilities.
- `--verbose` may naturally expose local input paths or signing-tool output;
  full logs should be reviewed and redacted before sharing.
- The builder does not implement the Stage 18C cloud workflow in this stage.
