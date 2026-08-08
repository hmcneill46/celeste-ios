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

## Pinned toolchain

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

The preflight also requires Git, Python 3, Swift, CMake, Ninja, GNU Make as
`gmake`, Mono's `monodis`, `patch`, `rg`, and Apple/Xcode command-line tools.
It verifies at least 8 GiB free. No script installs tools or accepts licences.

To inspect the host without changing it:

```bash
scripts/diagnose-tvos-host.sh --redact
```

## External inputs

Two user-owned inputs are mandatory:

- `CELESTE_GAME_ROOT`: extracted, unmodified itch.io Linux Celeste 1.4.0.0.
- `FMOD_SDK_ROOT`: mounted FMOD Engine iOS/tvOS 1.10.09 build 97915 SDK.

The exact validators are:

```bash
scripts/validate-celeste-input.sh --game-root "$CELESTE_GAME_ROOT"
scripts/validate-fmod-tvos-sdk.sh --sdk-root "$FMOD_SDK_ROOT"
```

Their manifests are privacy-safe and written only below ignored `.build/`
roots. The Celeste validator checks exact assembly/content hashes and rejects
Everest/MonoMod markers. The FMOD validator checks the revision, build, headers,
archive members, tvOS platform, arm64 architecture, deployment minimum, and
symbols. Neither input is modified.

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
   roots with the durable-storage integration.
7. [`generate-celeste-tvos-artwork.sh`](../scripts/generate-celeste-tvos-artwork.sh)
   creates the layered icon and static Top Shelf catalog from the user's game.

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

## Incremental build keys

Safe reuse is keyed by repository revision and relevant source diff, exact
Celeste/FMOD validation manifests, Stage 1 logical hash, generated Stage 6
manifests, artwork hashes, bundle metadata, RID, launch/audio mode, AOT,
trimming, and signing mode. A mismatch forces the applicable lane to rebuild.
`--clean` is the first remedy for a suspected stale Stage 8 product.

## Manual historical lanes

The historical Stage reports document individual audit gates and commands.
They are useful when modifying the port but are not the public build workflow.
The root `build.sh` and `celestemeow/` project are the original legacy iOS lane;
Stage 8 does not change them.
