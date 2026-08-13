# Advanced build guide

The supported entry point is [`../build-tvos.sh`](../build-tvos.sh). This guide
explains its automation surface and generated files; normal users should begin
with the root [README](../README.md).

## Clone and submodules

The public fork's default branch is `tvos-port`:

```bash
git clone https://github.com/hmcneill46/celeste-ios.git
cd celeste-ios
git -c url.https://github.com/.insteadOf=git://github.com/ \
  submodule update --init --recursive
./build-tvos.sh --help
```

The command-scoped rewrite is required because the pinned FNA revision still
uses GitHub's retired `git://` transport for nested submodules. It changes no
repository URL or global Git setting; Git fetches those official repositories
over HTTPS for this invocation.

If the GitHub default branch has not yet been updated, the equivalent explicit
clone is:

```bash
git clone --branch tvos-port --recurse-submodules \
  https://github.com/hmcneill46/celeste-ios.git
cd celeste-ios
```

Do not substitute the original upstream `main` branch; that is the legacy iOS
project and does not contain the completed self-builder.

## Pinned toolchain and command audit

The builder requires macOS and a complete Xcode installation. The accepted
toolchain is:

| Component | Accepted value |
| --- | --- |
| Host architecture | arm64 Apple silicon |
| .NET SDK | 10.0.302, selected by `global.json` |
| .NET workload set | 10.0.302.0 with `tvos` installed |
| Xcode | 26.6 |
| tvOS SDK | 26.5 |
| Deployment target | 16.0 |

The public builder's command surface was audited transitively from
`build-tvos.sh` through native, managed, FMOD, artwork, verification, signing,
and packaging scripts. These are the authoritative command groups:

| Command(s) | Supplied by | Builder modes | Why required | Separate install |
| --- | --- | --- | --- | --- |
| `git`, `python3`, `patch`, `file` | macOS/Xcode Command Line Tools selected with full Xcode | all | locked source state, validation, deterministic transforms | no additional package on the supported Xcode host |
| `xcodebuild`, `xcrun`, `swift` | full Xcode | all | tvOS projects/SDK tools and local artwork generation | install full Xcode |
| `plutil`, `codesign`, `security`, `shasum`, `ditto`, `lipo`, `nm`, `nmedit` | macOS/Xcode | applicable validation, FMOD, signing, and packaging phases | plist/signature/profile/hash/archive/symbol work | no additional package on the supported Xcode host |
| `dotnet` | Microsoft .NET SDK 10.0.302 | all | managed tooling, full AOT, trimming, tvOS publish | **yes**; official .NET installer, then workload set 10.0.302.0 |
| `gmake` | [GNU Make](https://www.gnu.org/software/make/) | builds | pinned MoltenVK `tvos` and `tvossim` targets | **yes**; official GNU Make or optional `brew install make` |
| `monodis` | [Mono](https://www.mono-project.com/download/stable/) | input validation/builds | managed Celeste assembly identities/references | **yes**; official Mono or optional `brew install mono` |
| POSIX/macOS basics: `awk`, `grep`, `sed`, `find`, `sort`, `xargs`, `cp`, `mkdir`, `rm`, `stat`, `df`, `du`, `tail`, `head`, `cut`, `tr`, `wc`, `nl`, `kill`, `sleep`, `defaults`, `sw_vers`, `xcode-select` | macOS | as applicable | orchestration and bounded diagnostics | no |
| `clang`, `ar`, `otool`, `vtool` | selected through `xcrun` | native/FMOD/package verification | compile bridge and inspect Mach-O/archive members | no separate command check; covered by Xcode/SDK validation |

CMake and Ninja are historical/manual-lane tools but are not invoked by the
supported self-builder. Ripgrep was an avoidable verifier dependency and has
been replaced with macOS `grep`; it is not required. The builder verifies at
least 8 GiB free. No script installs tools or accepts licences.

Check the host without game files, FMOD, signing, or a build:

```bash
./build-tvos.sh --check-host
```

Missing commands are collected and reported together. Wrong .NET/workload,
Xcode-selection, SDK, first-launch, submodule, disk, and entitlement states use
separate focused diagnostics.

To inspect the host without changing it:

```bash
scripts/diagnose-tvos-host.sh --redact
```

## External inputs

Two user-owned inputs are mandatory:

- `CELESTE_GAME_ROOT`: an extracted, unmodified supported Celeste 1.4.0.0 FNA
  distribution from the [exact input matrix](CELESTE_INPUTS.md).
- `FMOD_SDK_ROOT`: mounted FMOD Engine iOS/tvOS 1.10.09 build 97915 SDK.

The exact validators are:

```bash
scripts/validate-celeste-input.sh --game-root "$CELESTE_GAME_ROOT"
scripts/validate-fmod-tvos-sdk.sh --sdk-root "$FMOD_SDK_ROOT"
```

Their manifests are privacy-safe and written only below ignored `.build/`
roots. The Celeste validator detects one explicit store/platform profile and
checks exact managed identities, references, hashes, content, layout markers,
required/forbidden files, and Everest/MonoMod markers. Supported Steam source
is normalized with an exact zero-fuzz adapter before the one shared tvOS
transformation pipeline. The FMOD validator checks the revision, build, headers,
archive members, tvOS platform, arm64 architecture, deployment minimum, and
symbols. Neither input is modified.

The builder accepts the game root, a supported macOS `.app`, or the one wrapper
directory from a supported archive. It does not require a store flag. A
successful detection prints the version, distribution, source platform,
runtime family, profile, and canonical class. Unknown and mixed payloads fail
closed rather than being tried optimistically.

Command-line options override environment variables, which override saved
local configuration:

```text
--game-root DIR
--fmod-root DIR
```

## Interactive modes

Run:

```bash
./build-tvos.sh
```

The eight numbered phases are:

1. Check this Mac.
2. Find and validate Celeste.
3. Find and validate FMOD.
4. Check Apple tooling and, for install modes, select signing/device values.
5. Generate artwork.
6. Prepare native, managed, content, and FMOD inputs.
7. Publish the application.
8. Package or install it.

The menu modes map to these command values:

| Menu choice | `--mode` | Result |
| --- | --- | --- |
| Direct installation | `install` | Signed app, install, launch, startup verification |
| Unsigned IPA | `ipa` | `dist/Celeste-tvOS-unsigned.ipa` |
| Both | `both` | Independent unsigned and signed products |
| Prerequisites only | `validate` | Inputs/tooling/artwork validation; no game build |

## Noninteractive use

Unsigned IPA example:

```bash
export CELESTE_GAME_ROOT=/path/to/extracted/celeste
export FMOD_SDK_ROOT=/Volumes/mounted-fmod-sdk

./build-tvos.sh --non-interactive \
  --mode ipa \
  --bundle-id com.example.celeste-tvos
```

Direct install additionally requires the intended locally available Personal
Team and paired Apple TV when selection would otherwise be ambiguous:

```bash
./build-tvos.sh --non-interactive \
  --mode install \
  --bundle-id com.example.celeste-tvos \
  --team-id "$TVOS_TEAM_ID" \
  --device-id "$TVOS_DEVICE_ID"
```

Keep team and device values in the shell or ignored configuration. Never put
them in a script intended for Git.

Validation-only automation:

```bash
./build-tvos.sh --non-interactive \
  --mode validate \
  --game-root "$CELESTE_GAME_ROOT" \
  --fmod-root "$FMOD_SDK_ROOT" \
  --bundle-id com.example.celeste-tvos
```

## Local configuration

The builder saves reusable local choices in:

```text
.build/tvos-self-build/config.json
```

For direct signing it generates the already ignored:

```text
tvos/Local.Build.props
```

These may contain local paths, bundle identity, team selection, or device
selection and must remain untracked. The builder validates stored paths and
inputs on every run. Environment variables and explicit options win over saved
values.

Reset the choices without touching installed app data:

```bash
./build-tvos.sh --reset-config
```

Clean only Stage 8 build/output caches:

```bash
./build-tvos.sh --clean
```

The accepted native and managed preparation roots are separately keyed and
reused only when their locks and logical manifests agree.

## Preparation pipeline

The builder composes existing focused scripts instead of maintaining a second
build implementation:

1. [`fetch-tvos-deps.sh`](../scripts/fetch-tvos-deps.sh) obtains immutable
   open-source native revisions below `.build/tvos-native/`.
2. [`build-tvos-native.sh`](../scripts/build-tvos-native.sh) builds device and
   simulator variants when accepted artifacts are absent.
3. [`verify-tvos-native.sh`](../scripts/verify-tvos-native.sh) validates each
   archive member, XCFramework, architecture, platform, deployment target,
   symbols, link probes, and licences.
4. [`prepare-tvos-host-native.sh`](../scripts/prepare-tvos-host-native.sh)
   validates and stages the accepted six-component set for the host.
5. [`prepare-fmod-tvos.sh`](../scripts/prepare-fmod-tvos.sh) validates and stages
   external FMOD archives, the locked open-source FMOD-SDL bridge, and seven
   user-owned banks.
6. [`prepare-celeste-tvos-stage6.sh`](../scripts/prepare-celeste-tvos-stage6.sh)
   regenerates/patches modern Celeste source and stages content under ignored
   roots with the durable-storage, Save Manager, controller-prompt, and Metal
   Performance HUD integrations.
   [`inventory-celeste-controller-prompts.py`](../scripts/inventory-celeste-controller-prompts.py)
   validates the exact locked GUI atlas metadata without copying artwork.
7. [`generate-celeste-tvos-artwork.sh`](../scripts/generate-celeste-tvos-artwork.sh)
   creates the layered icon and static Top Shelf catalog from the user's game.

The Stage 6 transformation also installs the Stage 10A/10B Options entry and its
host-modal bridge, plus the Stage 11 Controller Prompts slider, narrow input
prefix hook, and Stage 16B Performance HUD `OnOff` bridge into ignored generated
source. The host stores the prompt choice under the fixed
`CelesteTvOS.ControllerPrompts.v1` standard-UserDefaults key; the HUD uses the
separate fixed `CelesteTvOS.PerformanceHUD.v1` key. Both are
intentionally outside Settings XML and the Stage 9B A/B envelope. The
product links Apple's Network and GameController frameworks; no third-party
HTTP server, networking, or controller-identification package is added.

The source locks and tracked transforms are reviewable; downloaded repositories,
decompiled/generated Celeste source, binaries, content, banks, and artwork are
not.

## Publish properties

The release graph targets `net10.0-tvos`, RID `tvos-arm64`, and launch mode
`CelesteAudio`. It requires:

```text
RunAOTCompilation=true
UseInterpreter=false
PublishTrimmed=true
TrimMode=full
MtouchLink=Full
Stage6PersistenceEnabled=true
```

Direct mode adds automatic development signing. IPA mode publishes without
code signing, removes residual signature/profile material, and archives exactly
`Payload/Celeste.app`.

## Outputs and logs

User-facing ignored output is:

```text
dist/
├── Celeste.app
├── Celeste-tvOS-unsigned.ipa
├── SHA256SUMS
├── build-summary.txt
└── logs/
```

Depending on the selected mode, only the relevant product is present. The
summary omits private signing/device values and source paths. Detailed logs are
local and may contain private local values; redact them before sharing.

The builder announces `Logs: dist/logs/` before preflight. Every public failure
writes `dist/logs/last-error.txt` with its phase, problem, detected/required
state, remedy, and the relevant command-log name when one exists. A failed
`run_logged` phase also identifies `dist/logs/<phase>.log` in the terminal.
Successful runs remove stale `last-error.txt` and write `build-summary.txt`.

Other generated roots include:

```text
.build/tvos-native/              artifacts/tvos-native/
.build/tvos-host/                artifacts/tvos-host/
.build/celeste-managed/          artifacts/celeste-managed/
.build/celeste-runtime/          artifacts/celeste-runtime/
.build/fmod-tvos/                artifacts/fmod-tvos/
.build/tvos-self-build/          artifacts/tvos-self-build/
```

All are ignored.

## Verification

Verify a signing-ready IPA:

```bash
scripts/verify-celeste-tvos-stage8a.py \
  --ipa dist/Celeste-tvOS-unsigned.ipa \
  --repo-root .
```

Verify a signed app:

```bash
scripts/verify-celeste-tvos-stage8a.py \
  --app dist/Celeste.app \
  --signed \
  --repo-root .
```

Verify public repository documentation and isolation:

```bash
scripts/verify-repository-stage8b.py
```

The Stage 8 verifier checks the display name, tvOS/arm64/minimum OS, AOT
evidence, real FMOD exports, exact bank set, compiled branding, privacy
manifest, forbidden entitlements, signature/profile state, persistence code,
and Git isolation.

Persistence format v2 is verified independently with:

```bash
scripts/verify-celeste-tvos-stage9b.sh
```

The product keeps the existing standard UserDefaults A/B keys. It reads legacy
v0 and production v1 envelopes, materializes ordinary uncompressed Celeste
files, and independently zlib-compresses each allow-listed entry only in the
durable v2 representation. A v1 installation migrates on its next changed save,
not on launch. The stored limits remain 124 KiB per generation and 256 KiB for
A+B; the separate decompression safety ceilings are 64 KiB for Settings and
256 KiB for each save slot.

Verify the writable Save Manager and preserved Stage 10A boundary:

```bash
scripts/verify-celeste-tvos-stage10b.sh
```

An optional built app can be checked with `--app ... --platform device
--signed`; an unsigned package can be passed with `--ipa`. The verifier runs
the deterministic HTTP/authentication/mutation suite, Stage 9B checks,
Info.plist and entitlement isolation, the exact four-name persistence boundary,
stale-write safety, and built-product checks. The interactive read/browser
physical runner remains:

```bash
scripts/run-celeste-tvos-save-manager-acceptance.sh --app dist/Celeste.app
```

It stores device, console, Bonjour, and browser-download evidence only below an
ignored output root. It never prints or persists the on-screen access code.
The accepted web page offers a deterministic all-files ZIP, four fixed download
routes, fixed replace/delete actions, and Settings reset. Uploads use a bounded
`application/octet-stream` body; they never expose or construct the compressed
UserDefaults envelope. Successful mutations pass through the Stage 9B A/B
authority and enter a blocked reload-ready state before gameplay can continue.
Return to the Apple TV and press Confirm. The Stage 13B high-level soft reload
keeps the original FNA/FMOD runtime, verifies a generation/hash ticket before
and after re-materialisation, rebuilds Settings/Input and normal main-menu state,
then clears the stale-write guard. The Apple TV app switcher is only the fallback
if reload verification fails.

Verify the Stage 11 prompt inventory, artwork-only policy, generated Settings
schema isolation, prior persistence/Save Manager gates, and optional product:

```bash
scripts/verify-celeste-tvos-stage11.sh \
  --game-root "$CELESTE_GAME_ROOT" \
  --generated-root .build/celeste-runtime/stage6-current/audio/managed
```

Pass `--app ... --platform device --signed` or `--ipa ...` to inspect a built
product. The public builder runs the focused inventory/source checks before
publish and checks Stage 11 product tokens in both signed and unsigned modes.

Verify the Stage 12B main-menu Quit interception, host state machine, preserved
Stage 9B/10B/11 foundations, and optional product:

```bash
scripts/verify-celeste-tvos-stage12b.sh \
  --generated-root .build/celeste-runtime/stage6-current/audio/managed
```

Pass `--app ... --platform device --signed` or `--ipa ...` for product checks.
The generated tvOS main-menu Quit call site opens the Celeste-rendered Leave
screen before `Engine.Exit`; Pause-menu Save and Quit remains unchanged. Home
backgrounds the retained runtime, and a resident-process foreground return is
reset to the main menu. Neither `LSSupportsGameMode` nor the deprecated
`GCSupportsGameMode` is declared for tvOS.

Verify the Stage 13B production soft reload, all prior gates, generated hook,
and optional package:

```bash
scripts/verify-celeste-tvos-stage13b.sh \
  --generated-root .build/celeste-runtime/stage6-current/audio/managed
```

Pass `--app ... --platform device --signed` or `--ipa ...` for product checks.
The verifier requires one FNA game/runtime, one generated main-thread update
hook, ticketed persistence preparation/completion, blocked failure fallback,
and the 20 deterministic reload-state tests. If the soft reload fails on a
device, fully close Celeste from the Apple TV app switcher and reopen it.

Verify Stage 15 one-time QR pairing, the complete accepted Stage 9B–14 chain,
the generated bridge, and an optional product with:

```bash
scripts/verify-celeste-tvos-stage15.sh \
  --generated-root .build/celeste-runtime/stage6-current/audio/managed
```

Pass `--app ... --platform device --signed` or `--ipa ...` for product checks.
The Stage 15 layer adds 31 deterministic tests for 256-bit token generation,
expiry, atomic one-time consumption, session/CSRF integration, parser limits,
shutdown invalidation, and the unchanged manual six-digit fallback. The QR is
generated locally with Core Image and requires no extra build dependency,
entitlement, camera permission, or external service.

Verify Stage 16B Performance HUD policy, the generated startup/Options hooks,
post-Stage16 native set, and the complete prior chain with:

```bash
scripts/verify-celeste-tvos-stage16b.sh \
  --generated-root .build/celeste-runtime/stage6-current/audio/managed \
  --native-manifest .build/tvos-host/normalized-manifest.json
```

Pass `--app ... --platform device --signed` or `--ipa ...` for product checks.
The Stage 16B layer adds 39 deterministic preference/layer/application/isolation
tests. It locks the public-Foundation constructor and both official bootstrap
spellings, the exact SDL/UIWindow/CAMetalLayer acquisition path, default Off,
logging disabled, no HUD plist/environment dependency, and full-AOT product
tokens. Apple's device-global Developer Graphics HUD setting is not required.

For isolated automation, build only an explicitly local acceptance app with
`Stage10AAutomation=true` and `Stage6StorageNamespace=acceptance`, then use:

```bash
scripts/run-celeste-tvos-save-manager-write-automated.sh \
  --app /path/to/signed-acceptance.app \
  --settings /ignored/path/settings.celeste \
  --save /ignored/path/0.celeste
```

This runner rejects production namespaces and requires ignored serializer-valid
fixtures. It is acceptance tooling, not part of the public interactive build.

## Incremental build keys

Safe reuse is keyed by repository revision and relevant source diff, exact
Celeste/FMOD validation manifests, Stage 1 logical hash, generated Stage 6
manifests, artwork hashes, bundle metadata, RID, launch/audio mode, AOT,
trimming, and signing mode. A mismatch forces the applicable lane to rebuild.
`--clean` is the first remedy for a suspected stale Stage 8 product.

## Manual historical lanes

The [historical engineering records](history/README.md) document individual
audit gates and commands. They are useful when modifying the port but are not
the public build workflow. The root `build.sh` and `celestemeow/` project are
the original legacy iOS lane; the tvOS self-builder does not replace them.
