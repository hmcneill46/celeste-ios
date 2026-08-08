# Troubleshooting

Start with a host-only check:

```bash
./build-tvos.sh --check-host
```

It needs neither Celeste nor FMOD. If the builder stops, read the terminal
block and `dist/logs/last-error.txt`. A failed build phase also names its full
`dist/logs/<phase>.log`. Redact local paths and signing/device details before
sharing excerpts.

Do not attach Celeste files, generated Celeste source, FMOD SDK files or banks,
an app/IPA containing game content, Apple credentials or two-factor codes,
certificates, provisioning profiles, Team IDs, device identifiers, or
`tvos/Local.Build.props` to an issue.

## The script is not executable

**Symptom**

```text
./build-tvos.sh: Permission denied
```

**Cause**

The executable bit was lost while downloading or copying the repository.

**Fix**

Prefer a real Git clone. In an existing clone:

```bash
chmod +x build-tvos.sh scripts/*.sh scripts/*.py
./build-tvos.sh --help
```

## Unsupported host or architecture

**Symptom**

The builder says macOS is required or warns that the architecture is untested.

**Cause**

The Apple tvOS toolchain requires macOS. This project was accepted on an arm64
Apple silicon Mac; Intel and virtualized hosts were not validated.

**Fix**

Use an Apple silicon Mac with full Xcode. Do not try to bypass the platform
check with a cross-compile container.

## Wrong .NET SDK

**Symptom**

```text
Problem:
The .NET SDK version is unsupported
```

**Cause**

SDK 10.0.302 is absent or `global.json` cannot select it.

**Fix**

Install .NET SDK 10.0.302 from Microsoft's official .NET 10 download page,
then verify from the clone:

```bash
dotnet --version
```

It must print `10.0.302`.

## Missing or wrong tvOS workload

**Symptom**

The builder reports workload set `not installed`, a value other than
`10.0.302.0`, or no `tvos` workload.

**Cause**

The exact Apple workload family selected by the repository is unavailable.

**Fix**

From the clone, install the pinned workload set deliberately:

```bash
dotnet workload install tvos --version 10.0.302.0
dotnet workload list
```

The list must show workload version `10.0.302.0` and `tvos`. The builder never
installs or updates workloads automatically.

## Missing build tools

**Symptom**

```text
Problem:
3 required tools are missing.
```

**Cause**

The preflight could not find one or more commands. It reports the complete set
in one run rather than stopping at the first missing tool. CMake, Ninja, and
ripgrep are not required by the supported public builder.

**Fix**

Use the guidance printed for the named group:

- Xcode/Apple commands: install full Xcode, complete first launch, and select
  its developer directory. The supported setup also supplies Git, Python 3,
  Swift, `patch`, `file`, and the Apple inspection/packaging utilities.
- `dotnet`: install Microsoft's .NET SDK 10.0.302, then workload set
  10.0.302.0 as described below.
- `gmake`: install [GNU Make](https://www.gnu.org/software/make/) from its
  official distribution. With optional Homebrew: `brew install make`.
- `monodis`: install [Mono](https://www.mono-project.com/download/stable/) from
  its official distribution. With optional Homebrew: `brew install mono`.

For both independently installed native tools, an optional combined command is:

```bash
brew install make mono
```

Homebrew is not mandatory. The project never runs Homebrew, `sudo`, licence
acceptance, or a package installer for you. After installing/selecting tools:

```bash
./build-tvos.sh --check-host
```

## Incorrect Xcode selection

**Symptom**

The selected developer directory is invalid, an SDK is missing even though
Xcode is installed, or the command line selects a different Xcode.

**Cause**

`xcode-select` points at CommandLineTools or another Xcode installation.

**Fix**

Select the full supported Xcode explicitly:

```bash
xcode-select -p
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
xcodebuild -version
xcrun --sdk appletvos --show-sdk-version
```

Adjust the path if your Xcode has another name.

## Xcode licence or first launch is incomplete

**Symptom**

```text
Problem:
Xcode first-launch tasks or licence acceptance are incomplete
```

**Cause**

Xcode has not completed its first launch, licence, or component setup.

**Fix**

Open Xcode normally, review its licence, let components finish, and close it.
Confirm:

```bash
xcodebuild -checkFirstLaunchStatus
```

Then rerun the builder.

## tvOS SDK is missing

**Symptom**

The builder reports no `appletvos` SDK.

**Cause**

The selected Xcode lacks tvOS platform support.

**Fix**

Open **Xcode > Settings > Components**, install the tvOS platform/runtime needed
by that Xcode, then verify:

```bash
xcrun --sdk appletvos --show-sdk-version
```

## Celeste path is missing or not found

**Symptom**

The builder reports that the Celeste installation was not provided or the
selected directory does not exist.

**Cause**

No path was supplied, a drag-and-drop path was incomplete, or the game folder
moved.

**Fix**

Extract your own itch.io Linux Celeste 1.4.0.0 download and either paste/drag
the folder into the interactive prompt or run:

```bash
export CELESTE_GAME_ROOT=/path/to/extracted/celeste
./build-tvos.sh
```

Do not point at this Git repository.

## Celeste version/distribution is rejected

**Symptom**

Validation reports an assembly identity, version, executable hash, FNA hash, or
content-tree mismatch.

**Cause**

The project accepts the exact unmodified Celeste 1.4.0.0 itch.io Linux input.
Steam, Windows, macOS, Epic, console, older/newer, and modified files are
untested even when they look similar.

**Fix**

Use a clean extraction of the tested itch.io Linux download. Do not patch files
to imitate expected hashes. Another distribution is usable only if the
unchanged validator genuinely accepts its exact files.

## Everest or modded installation is rejected

**Symptom**

The validator reports Everest/MonoMod markers or unexpected managed files.

**Cause**

The selected directory is a modded installation.

**Fix**

Keep your modded installation separate and extract a fresh, unmodified itch.io
Linux Celeste 1.4.0.0 copy for this build.

## `Celeste.png` is missing

**Symptom**

```text
Problem:
The Celeste installation is incomplete

Detected:
missing Celeste.png
```

**Cause**

The selected game directory is incomplete or is not the tested distribution.

**Fix**

Re-extract the complete tested download. Do not download substitute artwork or
copy a derived icon into the repository.

## `SplashScreen.png` is missing

**Symptom**

The builder reports missing `Content/Graphics/SplashScreen.png`.

**Cause**

The game content extraction is incomplete.

**Fix**

Re-extract the complete game. The file is needed to generate the local static
Top Shelf asset.

## FMOD SDK is not mounted

**Symptom**

```text
Problem:
The FMOD SDK was not found
```

**Cause**

No valid mounted SDK was found and no path was provided.

**Fix**

Using your own FMOD account, obtain official FMOD Engine for iOS/tvOS 1.10.09,
mount the DMG, then rerun. If discovery is ambiguous:

```bash
export FMOD_SDK_ROOT=/Volumes/the-mounted-fmod-sdk
./build-tvos.sh
```

Do not copy FMOD into the repository.

## Wrong FMOD version

**Symptom**

Validation rejects the revision, build, `FMOD_VERSION`, archive hash/platform,
or symbols.

**Cause**

The accepted native SDK is exactly FMOD Engine iOS/tvOS 1.10.09 build 97915.
FMOD 2.x and other 1.x releases are not interchangeable.

**Fix**

Use FMOD's official archived/previous releases available through your account,
mount the correct iOS SDK DMG, and rerun. Do not rename another SDK to bypass
validation.

## Not enough disk space

**Symptom**

The builder reports less than 8 GiB free or a later tool reports no space.

**Cause**

A clean native/AOT/content build needs substantial intermediates; the finished
app is roughly 1.1 GiB before IPA compression.

**Fix**

Free space on the volume containing the clone. You can remove only generated
Stage 8 outputs with:

```bash
./build-tvos.sh --clean
```

Do not delete user game/FMOD inputs through the project.

## No Apple Account or Personal Team

**Symptom**

```text
Problem:
Xcode has no free Personal Team
```

**Cause**

No Apple Account with a free Personal Team is configured in Xcode.

**Fix**

Open **Xcode > Settings > Accounts**, add your Apple Account, and confirm its
Personal Team appears. No paid Apple Developer Program membership is required.
Then rerun direct-install mode.

## Apple TV is not paired or developer-ready

**Symptom**

The builder finds zero available paired Apple TVs.

**Cause**

The Apple TV is asleep, off-network, unpaired, or Developer Mode is disabled.

**Fix**

Wake it, put it on the same network, use **Settings > Remotes and Devices >
Remote App and Devices**, and pair in **Xcode > Window > Devices and
Simulators**. Enable Developer Mode in Apple TV **Settings > Privacy &
Security** when offered, complete any restart/confirmation, then rerun.

## Invalid bundle identifier

**Symptom**

```text
Problem:
The bundle identifier is invalid
```

**Cause**

The value is not reverse-DNS form or contains unsupported characters.

**Fix**

Choose a unique identifier such as `com.local-name.celeste-tvos`, using letters,
digits, hyphens, and dots. Keep the chosen value private/local and stable across
re-signing.

## Personal Team provisioning or app limit fails

**Symptom**

Automatic provisioning reports that an App ID cannot be registered, a profile
cannot be created, or the free-app/device limit has been reached.

**Cause**

Personal Teams have Apple-managed development limits, or the local bundle ID is
already owned by another team.

**Fix**

Use a unique bundle identifier. In Xcode, verify the intended Personal Team and
device, and remove/let expire only your own unrelated temporary Personal Team
apps when appropriate. Do not revoke certificates or delete profiles merely to
retry. Rerun after the account/device state is healthy.

## Provisioning expired after about seven days

**Symptom**

Celeste no longer launches even though it previously worked.

**Cause**

Free Personal Team development provisioning is short-lived.

**Fix**

Wake/pair the Apple TV and rerun `./build-tvos.sh` in install mode. Keep the same
bundle identifier so the replacement keeps the same app identity and save
domain.

## Direct install succeeds but launch verification fails

**Symptom**

Installation completes, but the builder does not observe seven banks and the
first Celeste draw.

**Cause**

The Apple TV may have lost connectivity/focus, a controller/startup dependency
may be unavailable, or the process may have crashed.

**Fix**

Wake the Apple TV, confirm it remains paired and developer-ready, connect the
controller, and launch the installed app manually once. Inspect the end of the
ignored `dist/logs/` files and `.build/tvos-self-build/launch-console-private.log`.
Share only a redacted excerpt if opening an issue.

## No sound

**Symptom**

The device game runs but music/effects are absent.

**Cause**

The arm64 simulator is intentionally no-audio. On physical hardware, the wrong
mode/input, muted in-game volume, muted output, or a bank/native failure may be
responsible.

**Fix**

Confirm this is the physical `CelesteAudio` build, all seven bank checks passed,
Apple TV output is audible, and Celeste Music/SFX values are above zero. Rerun
the builder if the app was produced before FMOD mounted successfully. Do not add
loose FMOD libraries manually.

## No controller input

**Symptom**

Celeste starts but cannot be controlled.

**Cause**

The Siri Remote is not a gameplay controller, or the extended controller is not
connected/claimed.

**Fix**

Pair/connect an extended Bluetooth controller in tvOS Settings before launch.
DualSense is the physically tested controller. Relaunch after reconnecting if
necessary.

## Saves appear missing after changing the bundle ID

**Symptom**

Settings and slots are empty after a new build, while the old app had saves.

**Cause**

Standard UserDefaults is scoped to the app identity. A changed bundle
identifier creates a separate defaults domain.

**Fix**

Rebuild/re-sign with the exact previous local bundle identifier and install it
as a replacement. The project does not migrate data between bundle IDs.

## Unsigned IPA will not install directly

**Symptom**

Apple TV or a tool rejects `Celeste-tvOS-unsigned.ipa` as unsigned.

**Cause**

This output is intentionally signing-ready, not directly installable. It has no
usable signature or provisioning profile.

**Fix**

Use builder direct-install mode, or use a tvOS-capable external signer to sign
the IPA for your device/account. Do not expect `devicectl` to install the
unsigned archive.

## A third-party signer changes the bundle identifier

**Symptom**

The signed app installs beside an earlier one or existing saves appear absent.

**Cause**

The signing service rewrote the app identity.

**Fix**

Configure the external signer to reuse the same identity when it safely
supports that. Save-domain behavior is controlled by the final signed bundle
identifier, not the unsigned IPA filename. External tools are outside project
acceptance; never give Apple credentials to this repository.

## Build/package is very large

**Symptom**

The app is about 1.1 GiB and the IPA is hundreds of MiB.

**Cause**

The faithful package includes the full user-owned content tree and seven FMOD
banks. Stage 8 does not recompress or remove content.

**Fix**

This is expected. Ensure enough disk/network space for your local signing path.
Do not delete banks or arbitrary content to shrink the app.

## Stale cached output

**Symptom**

A build appears to use old branding, metadata, mode, or local input despite
changes.

**Cause**

An interrupted earlier build may have left a cache that does not pass current
reuse checks.

**Fix**

```bash
./build-tvos.sh --clean
./build-tvos.sh
```

The clean option removes Stage 8 build/output caches, not your external game,
FMOD SDK, or installed saves.

## Reporting a build problem safely

Run:

```bash
scripts/diagnose-tvos-host.sh --redact
scripts/verify-repository-stage8b.py
```

Include the detected versions, builder mode, whether each input validator
passed, and the first meaningful redacted error. Use the repository's build
problem issue form; follow its private/proprietary attachment warnings.
