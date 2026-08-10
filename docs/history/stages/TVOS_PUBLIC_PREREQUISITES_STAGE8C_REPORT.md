# Stage 8C: public prerequisite and failure-UX hardening

Status: **PASS — the public builder now derives its prerequisites from the
actual build path, reports all missing tools together, and records useful
privacy-safe failure details from startup onward.**

## Scope and baseline

- Branch: `tvos-port`
- Starting commit: `eeab3fd23988880b4f9cbed6cb83b0b352e3f4f5`
- Candidate/final commit: the commit containing this report
- Intended commit subject: `build: improve tvOS self-build prerequisite checks`
- Publication target: `origin/tvos-port` on the user fork only

The worktree began clean at the exact expected commit, tracked
`origin/tvos-port`, and retained the accepted recursive submodule revisions.
The upstream remote was inspected but not changed. This hotfix changes only
public prerequisite detection, simple verifier search commands, failure logs,
and onboarding documentation. Gameplay, native revisions, generated-game
transforms, FMOD behavior, persistence, branding, signing architecture, and IPA
format are unchanged.

## Original public failure and environment finding

An untouched post-Stage-8B public clone reproduced the reported failure. With
the Codex-app-specific command directory removed from a normal login shell's
search path, `rg` was unavailable and the old builder stopped on that one
command before reporting anything else. It printed `rg not found` and produced
no `dist/logs/last-error.txt`.

The current Codex process and the user's login shell could both see an `rg`
binary supplied inside the desktop app bundle. Removing only that app-specific
path entry reproduced the ordinary fresh-user environment. This explains why
Stage 8B's clean repository clones passed even though a normal Terminal user
encountered a prerequisite dead end. No complete private `PATH` value is
recorded here.

Repository-wide review found that ripgrep was used only for straightforward
recursive or binary/text regular-expression checks in repository verification
scripts. Those calls were converted to stock macOS `grep`; ripgrep is no longer
a build requirement.

## Complete external-command audit

The audit followed `build-tvos.sh` through all transitively invoked native,
managed-generation, runtime-content, FMOD, artwork, verification, signing, and
packaging scripts. The supported public path has this command surface:

| Command or group | Provider | Where used | Public requirement |
| --- | --- | --- | --- |
| `git` | macOS/Xcode command-line tools | revisions and submodules | supplied by supported Xcode host |
| `python3` (3.9+) | macOS/Xcode command-line tools | validators, deterministic generation, manifests | supplied by supported Xcode host |
| `xcodebuild`, `xcrun`, `swift` | full Xcode | SDK/Xcode builds, Apple tools, local artwork | full Xcode required |
| `patch`, `plutil`, `codesign`, `security`, `shasum`, `ditto`, `lipo`, `nm`, `nmedit`, `file` | macOS/Xcode | transforms, plist/signing/profile, hash/package, archive/symbol/input validation | supplied by supported Xcode host |
| `dotnet` | Microsoft .NET SDK | managed tools, trimming, full AOT, tvOS publish | separate install: SDK 10.0.302 and tvOS workload set 10.0.302.0 |
| `gmake` | GNU Make | pinned MoltenVK `tvos` and `tvossim` targets | separate install |
| `monodis` | Mono | exact Celeste assembly identity and reference validation | separate install |
| `clang`, `ar`, `otool`, `vtool` | selected by `xcrun` | FMOD bridge/native builds and Mach-O/archive inspection | covered by Xcode/SDK validation |
| POSIX/macOS basics (`awk`, `grep`, `sed`, `find`, `sort`, `xargs`, file operations, text utilities, process utilities, `defaults`, `sw_vers`, `xcode-select`) | macOS | orchestration and bounded diagnostics | supplied by macOS |

The builder's explicit all-mode command check is now exactly 18 commands:

```text
git python3 dotnet xcodebuild xcrun swift gmake patch plutil codesign security
shasum ditto lipo nm nmedit monodis file
```

The supported setup separately requires only:

1. .NET SDK 10.0.302 and tvOS workload set 10.0.302.0;
2. GNU Make, providing `gmake`; and
3. Mono, providing `monodis`.

GNU Make and Mono are available from their official projects. For builders who
already choose Homebrew, the verified formula names permit the optional single
command `brew install make mono`. The builder does not invoke Homebrew.

### Removed avoidable requirements

| Tool | Audit finding | Decision |
| --- | --- | --- |
| `rg` | simple verifier searches only | replaced with macOS `grep`; removed |
| CMake | preflight and historical/manual diagnostics only; not called by the supported public build graph | removed from public prerequisites |
| Ninja | preflight and historical/manual diagnostics only; not called by the supported public build graph | removed from public prerequisites |

`gmake`, `monodis`, and `nmedit` remain because the current supported path
really invokes them. The first two require separate installation; `nmedit` is
an Apple tool and does not need another package.

## Builder behavior

`./build-tvos.sh --check-host` now checks only the Mac, selected Xcode/tvOS SDK,
.NET SDK/workload, commands, submodules, licence/first-launch state, and disk
space. It does not require Celeste, FMOD, signing, or a build.

The first phase collects every missing command before stopping. A controlled
minimal-path test reported all three absent independently installed command
providers in one run:

```text
dotnet   modern tvOS build and full AOT
gmake   pinned MoltenVK tvOS targets
monodis exact managed-assembly validation
```

It also reported that 15 of 18 checked commands were already available,
explained the official installation route for each missing group, and offered
the optional combined Homebrew command without running it. Missing commands
are distinct from wrong Python, .NET, workload, Xcode, SDK, submodule, licence,
or disk-space states, which retain focused diagnostics.

The builder announces `Logs: dist/logs/` at startup. Every controlled failure,
including option and early host failures, writes:

```text
dist/logs/last-error.txt
```

That file contains the phase, problem, detected/required state, and fix with
private home/volume paths, account identifiers, team identifiers, device
identifiers, and bundle identifiers redacted. A failing logged command also
prints and records its exact relative `dist/logs/<phase>.log`. Terminal output
still contains the complete actionable failure; logs supplement it. A
successful invocation removes stale `last-error.txt`.

A controlled invalid-Celeste fixture confirmed that a later command failure
named both `dist/logs/last-error.txt` and `dist/logs/validate-celeste.log`.
Neither test altered or inspected the real user inputs.

## Public documentation

The root README now answers, before Quick Start:

- which tools come with macOS/full Xcode;
- which three components must be installed separately;
- the optional Homebrew command for GNU Make and Mono;
- that CMake, Ninja, and ripgrep are not required;
- how to check a host without Celeste or FMOD; and
- where every failure summary and full phase log is written.

`docs/BUILDING.md` contains the authoritative command/provider/mode/purpose
matrix. `docs/TROUBLESHOOTING.md` contains official and optional installation
routes plus focused version/Xcode/workload fixes. The build-problem issue
template now asks for host-check and redacted failure-log results, not private
inputs.

## Fresh-clone prerequisite tests

A disposable clone started without `.build`, `artifacts`, `dist`, local signing
configuration, generated source, artwork, apps, or IPAs. Recursive submodules
initialized with the documented command-scoped HTTPS rewrite.

Under a normal login shell with the desktop-app command directory removed:

- `rg` was confirmed absent;
- `./build-tvos.sh --help` succeeded;
- the repository verifier succeeded;
- `./build-tvos.sh --check-host` passed all 18 actual commands without asking
  for ripgrep, CMake, Ninja, game files, FMOD, or signing; and
- a minimal-path run reported `dotnet`, `gmake`, and `monodis` together and
  created the advertised privacy-safe last-error file.

This directly fixes the original one-command-at-a-time `rg not found` failure.

## Complete post-fix unsigned build

The same fresh candidate clone performed a clean noninteractive unsigned build
through a normal login shell with `rg` still unavailable. It built the locked
native graph from clean state, reproduced accepted Stage 1 logical SHA-256
`61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39`,
regenerated the managed/game inputs below ignored roots, staged the exact FMOD
inputs and seven banks, generated artwork, and published a full-AOT,
full-trimmed `TVOS arm64` signing-ready IPA.

| Property | Accepted result |
| --- | --- |
| Display name | `Celeste` |
| Payload | `Payload/Celeste.app` |
| Architecture / platform | `arm64` / `TVOS` |
| Minimum OS | `16.0` |
| App bytes | `1,215,232,381` |
| IPA bytes | `895,099,587` |
| IPA SHA-256 | `c2db6b321afd83002c43478b32843458012a84bb3080b392d1d79e87dc99fd59` |
| FMOD banks | exactly 7 |
| Signature / profile | absent |
| Stage 6 persistence | present |
| User Management entitlement | absent |

The independent Stage 8A package verifier passed. The complete clean build took
approximately 29 minutes on the accepted host; there is no speed gate.

## Direct-signing regression

The same candidate clone then reused only its own verified build products and
the existing ignored local signing choices. Automatic Personal Team
provisioning, Release full-AOT/full-trimmed publish, sanitization and resigning,
strict signed-app verification, replacement installation, and launch all
passed. Device runtime evidence confirmed all seven real FMOD banks and the
first Celeste-owned draw. The signed app verifier reported one `arm64` `TVOS`
executable, tvOS 16.0 minimum, display name `Celeste`, and an app size of
`1,215,911,005` bytes. This reuse smoke took approximately seven minutes.

No private bundle, team, certificate, profile, account, or device value entered
the candidate or this report. No uninstall occurred and no save-domain change
was requested.

## Isolation and remaining limitations

Source/repository verification, shell syntax, Python compilation, generated
output ignores, public Markdown links, executable bits, tracked-file suffixes,
and private-path/proprietary-candidate scans pass. User-owned Celeste and FMOD
inputs, generated source, content, banks, artwork, apps/IPAs, signing values,
profiles, device evidence, and raw logs remain ignored and uncommitted.

Known onboarding boundaries remain intentional:

- the user installs/selects Xcode, .NET, workload, GNU Make, and Mono;
- the user obtains lawful Celeste and FMOD inputs independently;
- initial Xcode licence/component setup and Apple account/device pairing remain
  user actions;
- only the accepted Apple-silicon/Xcode/.NET versions are proven; and
- a complete clean native/game package remains a large, approximately
  half-hour build on the accepted host.

The builder installs nothing, runs no `sudo`, accepts no licence, downloads no
Celeste or FMOD material, stores no Apple credential, requests no paid
entitlement, and preserves the Stage 8A/8B isolation boundaries.
