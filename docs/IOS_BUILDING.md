# Build and install Celeste on iPhone or iPad

This guide is the normal modern-iOS self-build path. You do not need to read
the engineering stage reports. The result is one landscape iPhone/iPad app
with touch controls, optional controllers, real audio, local saves, and native
Files import/export.

This is an unofficial personal self-build. The repository contains no Celeste
game files or FMOD SDK files and does not download either one for you.

## What you need

- An Apple-silicon Mac running the exact toolchain checked by the builder:
  macOS 26.3, Xcode 26.6 with iOS SDK 26.5, .NET SDK 10.0.302, workload set
  10.0.302.0, and the iOS workload 26.5.10301.
- Your own unmodified Celeste 1.4.0.0 FNA files matching one of the [nine
  supported profiles](CELESTE_INPUTS.md).
- **FMOD Engine iOS/tvOS 1.10.09 build 97915** from the [official
  version-specific FMOD page](https://www.fmod.com/download?version=1.10.09#fmodengine).
  Choose FMOD Engine (not FMOD Studio) and the iOS package.
- A physical arm64 iPhone or iPad running iOS/iPadOS 15.0 or later for the full
  product. The same universal app supports both device families.
- For direct installation, an Apple Account added to Xcode. A free Personal
  Team is supported.

The accepted FMOD release has no arm64 iOS Simulator audio slice. A physical
device—not Simulator—is the supported full-game route.

## 1. Get the repository

```bash
git clone https://github.com/hmcneill46/celeste-ios.git
cd celeste-ios
git -c url.https://github.com/.insteadOf=git://github.com/ \
  submodule update --init --recursive
```

If you are testing a named release branch before integration, add
`--branch NAME` to the clone command. The command-scoped URL rewrite is needed
because the pinned FNA revision names GitHub's retired `git://` transport for
nested submodules; it changes no repository or global Git configuration. Do
not copy generated source or an app from another checkout.

## 2. Get clean Celeste files

Follow [Celeste input files](CELESTE_INPUTS.md) for lawful itch.io, Steam, and
Epic Games Store acquisition routes. Extract the package somewhere outside
the repository. Do not use a modded/Everest, XNA, mixed, or unknown-version
folder.

For example:

```bash
export CELESTE_GAME_ROOT="$HOME/Celeste-Clean-Builds/Celeste-Linux"
```

The builder fingerprints the contents and prints the detected store, source
platform, runtime family, and exact supported profile. Folder names are never
trusted as validation.

## 3. Get the exact FMOD SDK

Download and mount **FMOD Engine iOS/tvOS 1.10.09 build 97915**. Point the
builder either at the mounted volume or its `FMOD Programmers API` directory:

```bash
export FMOD_SDK_ROOT="/Volumes/FMOD Programmers API iOS"
```

The builder validates the revision, headers, iPhone archives, and exact
fingerprints before native or AOT work. Newer FMOD versions are not substitutes.

To retain these two variables for future Terminal sessions, add their `export`
lines to `~/.zprofile`, then open a new Terminal window.

## 4. Prepare Xcode for a personal device

For a direct install:

1. Open Xcode → Settings → Accounts and add your Apple Account.
2. Allow Xcode to create an Apple Development certificate for your Personal
   Team if needed.
3. Connect the iPhone/iPad by USB or pair it in Xcode → Window → Devices and
   Simulators.
4. Trust the Mac/device prompts and enable Developer Mode on the device when
   iOS asks for it.
5. Wake and unlock the device before installation.

A paid Apple Developer Program subscription is not required for basic
personal-device installation. Free Personal Team provisioning normally expires
after about seven days; rebuild and install the same bundle identity to renew
it without deleting the app data.

## 5. Run the doctor

```bash
./build-ios.sh --doctor
```

The doctor checks the exact host toolchain, submodules, disk space, optional
Celeste/FMOD inputs, and currently available devices. It labels optional
missing inputs/devices as warnings. A missing device does not block an unsigned
package build.

A clean full build has used about 11 GiB across native, generated, AOT, and
package intermediates; keep at least 15 GiB free if practical. The builder
stops below the safe minimum rather than failing near the end.

## 6. Build

For a guided choice:

```bash
./build-ios.sh
```

Or choose one explicit workflow:

```bash
# Verified unsigned IPA for a separate signing workflow; not directly installable
./build-ios.sh --unsigned

# Local Apple-development-signed IPA, without installing
./build-ios.sh --signed

# Sign, install, and launch on a paired iPhone/iPad
./build-ios.sh --install
```

If one compatible device is available, install selects it automatically. If
several are available, choose a numbered device name; you never have to paste a
UDID. Personal Team development profiles are device-bound, so `--signed` uses
the same friendly device selection/provisioning step but does not install.
Installation happens only with `--install` or the matching guided choice.

The eight real phases validate inputs early, reuse the exact verified iOS
native cache, regenerate Celeste deterministically, build Release `ios-arm64`
with LLVM full AOT/full trimming/`UseInterpreter=false`, verify the app, and
only then promote the semantic IPA filename. Long operations emit one-minute
elapsed-time/free-disk heartbeats. Use `--verbose` for full terminal output;
ignored logs are under `artifacts/ios/logs/`.

Successful outputs are under `artifacts/ios/`, for example:

```text
Celeste-iOS-v0.1.1-build5-unsigned.ipa
Celeste-iOS-v0.1.1-build5-development.ipa
```

The builder prints signing state, byte size, and SHA-256. An interrupted or
failed build never promotes an `.incomplete` file as a successful IPA.

## 7. Launch and update later

The app appears as **Celeste**. It supports landscape left and right on iPhone
and iPad. The first screen can be operated entirely by touch; a controller is
optional.

When updating, keep the same bundle identifier and install the newer build
over the existing app. Settings, slots 0–2, previous-good backups, touch
preferences, Phone/Tablet layouts, Grab profiles, and prompt preference remain
in the same app container. Do not uninstall first: deleting the app can delete
its private Application Support data. Export important saves to Files before
an intentional uninstall.

## Saves, Files, and touch layouts

Use **Options → Data & Files** to export, Share, import, or restore previous
Settings and save slots. Exported `.celeste` files are ordinary portable
logical Celeste files. Live saves remain private in Application Support; there
is no iOS LAN Save Manager or automatic cloud sync.

Touch works without a controller. Automatic mode hides the overlay while a
controller is active; the editor keeps separate Phone and Tablet layouts. Use
**Options → Touch Controls** to customize or export/import a validated
`.celestetouch` layout.

## Common problems

- **No Apple Account/Personal Team:** add the account in Xcode Settings →
  Accounts and let Xcode create an Apple Development certificate.
- **Device missing:** wake/unlock it, reconnect or pair it in Xcode, trust the
  prompts, and enable Developer Mode. `--unsigned` does not need a device.
- **Provisioning expired:** rerun `./build-ios.sh --install` with the same bundle
  identity. Do not uninstall.
- **Bundle identifier conflict:** choose a unique reverse-DNS identity once and
  keep it for later upgrades. Changing it creates a separate app/container.
- **Celeste rejected:** use the exact [supported input matrix](CELESTE_INPUTS.md),
  not a modified or mixed installation.
- **FMOD rejected:** use Engine iOS/tvOS 1.10.09 build 97915, not FMOD 2.x.
- **Stale generated/AOT output:** rerun with `./build-ios.sh --clean`.
- **Build stopped:** read the privacy-redacted summary at
  `artifacts/ios/logs/last-error.txt`, then the named ignored phase log.

More remedies are in [Troubleshooting](TROUBLESHOOTING.md). Never post game
files, FMOD files, signed apps/IPAs, Apple credentials, certificates,
provisioning profiles, Team IDs, or device identifiers in an issue.

## Current limitations

- Personal self-build only; no App Store/TestFlight distribution is claimed.
- The user must own/provide supported Celeste files and the exact FMOD SDK.
- Full product is physical-device-only; Simulator is not the main product path.
- Free Personal Team provisioning is temporary and must be renewed.
- No Everest/mod support, automatic cloud saves, or iOS Save Manager.
- Files import uses the explicit in-app picker; system Open-In ownership remains
  deferred.

For implementation and reproducibility details, see [Modern iOS
architecture](IOS_FOUNDATION.md) and the [engineering history](history/README.md).
