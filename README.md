# Celeste for Apple TV

An unofficial community tvOS port that runs Celeste natively on Apple TV from
your own game files. Gameplay, extended-controller input, FMOD audio, durable
saves, a layered home-screen icon, and Top Shelf artwork are working on the
tested hardware.

This repository contains no Celeste game content and no proprietary FMOD SDK
material. You must own Celeste and obtain FMOD from FMOD's official website.
The recommended entry point is [`./build-tvos.sh`](build-tvos.sh), and a free
Apple Account is sufficient for direct installation.

## Status

The personal-use tvOS build is playable on physical Apple TV hardware. It uses
.NET 10, FNA, Metal, full AOT, full trimming, real FMOD audio, and a
compressed, corruption-recoverable UserDefaults save bridge.

The tested game input is **Celeste 1.4.0.0 from the itch.io Linux download**.
Steam and other platform/store builds have not been tested. See
[`docs/STATUS.md`](docs/STATUS.md) for the precise support matrix and known
limitations.

## What works

- Native arm64 tvOS gameplay and Metal rendering
- Music, ambience, UI audio, gameplay effects, and cutscene audio
- Extended game controllers; Sony DualSense was physically tested
- Movement, Jump, Dash, Grab, Confirm, Cancel, Pause, cutscene skip, and rumble
- Durable Settings and all three normal save slots
- Transparent compressed storage that fixes the later-game 32 KiB save limit
- Recovery from a corrupt newest save generation
- Local generation of the layered strawberry icon and static Top Shelf artwork
- Direct Personal Team installation or a signing-ready unsigned IPA

## What you need

- An Apple silicon Mac. The proven host used macOS 26.3.
- Full Xcode 26.6 with the tvOS 26.5 SDK and command-line tools selected.
- A paired, developer-ready arm64 Apple TV running tvOS 16 or later. The tested
  device is an Apple TV 4K (3rd generation).
- An extended Bluetooth game controller. DualSense is tested.
- Your lawful, unmodified **itch.io Linux Celeste 1.4.0.0** installation.
- The official **FMOD Engine for iOS/tvOS 1.10.09, build 97915** SDK.
- Roughly 8 GiB of free disk space for a clean build.

Exact tested and potentially compatible configurations are separated in
[`docs/STATUS.md`](docs/STATUS.md).

### Comes with macOS and full Xcode

The supported full-Xcode setup supplies Git, Python 3, Swift, the tvOS SDK and
Apple command-line utilities used for compilation, archives, assets, signing,
inspection, and packaging. Command Line Tools by themselves are not a
substitute for full Xcode.

### Install separately

- [**.NET SDK 10.0.302**](https://dotnet.microsoft.com/download/dotnet/10.0),
  followed by tvOS workload set **10.0.302.0**.
- [**GNU Make**](https://www.gnu.org/software/make/), which provides the
  `gmake` command used by pinned MoltenVK.
- [**Mono**](https://www.mono-project.com/download/stable/), which provides
  `monodis` for validating Celeste's managed assembly identity.

If you already use Homebrew, the last two can be installed together with
`brew install make mono`. Homebrew is optional, and the builder never invokes
it. CMake, Ninja, and ripgrep are **not** required by the public self-builder.

### Builder validates automatically

Before locating Celeste or FMOD, check only the Mac and tools:

```bash
./build-tvos.sh --check-host
```

It reports every missing command in one run, explains why it is needed, and
writes a privacy-safe failure summary to `dist/logs/last-error.txt`. It installs
nothing.

## Quick start

```bash
git clone https://github.com/hmcneill46/celeste-ios.git
cd celeste-ios
git -c url.https://github.com/.insteadOf=git://github.com/ \
  submodule update --init --recursive
./build-tvos.sh --check-host
./build-tvos.sh
```

The one-command URL rewrite is local to that Git invocation. It is needed
because the pinned historical FNA revision names its nested repositories with
GitHub's retired `git://` transport; the repositories are still fetched from
their official HTTPS URLs.

The builder checks the Mac, finds and validates Celeste and FMOD, checks Apple
tooling, generates local artwork, prepares the locked dependencies, builds the
game, and then installs it or creates an IPA. It installs no development tools,
accepts no licence for you, and downloads neither Celeste nor FMOD.

Choose one of these when prompted:

1. Build, sign, and install on an Apple TV.
2. Create a signing-ready unsigned IPA.
3. Build both.
4. Validate prerequisites only.

Run `./build-tvos.sh --help` for automation options. Advanced and
noninteractive examples are in [`docs/BUILDING.md`](docs/BUILDING.md).

## Celeste game files

You must legally own Celeste. The only tested and supported source is the
[itch.io Linux release](https://maddymakesgamesinc.itch.io/celeste), version
1.4.0.0:

1. Download the Linux build using your itch.io purchase or existing
   entitlement.
2. Extract it somewhere outside this repository.
3. Give the extracted folder to the builder by pasting or dragging it into
   Terminal, or set `CELESTE_GAME_ROOT`.

The folder must include `Celeste.exe`, `Celeste.Content.dll`, `FNA.dll`,
`Content/`, `Celeste.png`, and `Content/Graphics/SplashScreen.png`. The builder's
hash and assembly validator is authoritative and rejects modified, Everest, or
unsupported inputs.

Steam builds have not currently been tested by this project. Another Celeste
distribution may work if it contains the exact files accepted by the
validator, but it is not currently a supported/tested configuration.

Never copy game files, generated source, or generated artwork into Git.

## FMOD SDK

The exact accepted SDK is **FMOD Engine for iOS/tvOS 1.10.09, build 97915**.
Obtain it using your own account from [FMOD's official download
site](https://www.fmod.com/download); the older release may be listed in the
archived/previous versions available to your account. Download the iOS SDK,
mount its DMG, and rerun the builder. The builder detects one unambiguous
mounted SDK or asks for its root; `FMOD_SDK_ROOT` is also supported.

FMOD 2.x and other releases are rejected. This project does not redistribute
FMOD, automate an FMOD login, or use unofficial mirrors.

## Apple and Xcode setup

Install full [Xcode](https://apps.apple.com/app/xcode/id497799835), launch it
once, accept its licence, and let first-run components finish. If more than one
Xcode is installed, select the intended copy, for example:

```bash
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
```

Install [.NET 10 SDK 10.0.302](https://dotnet.microsoft.com/download/dotnet/10.0),
then, from this clone, install the exact workload set if it is not already
present:

```bash
dotnet workload install tvos --version 10.0.302.0
```

The builder itself never runs these installation commands.

### Pairing an Apple TV

1. Put the Mac and Apple TV on the same network and wake the Apple TV.
2. On Apple TV, open **Settings > Remotes and Devices > Remote App and
   Devices**.
3. In Xcode, open **Window > Devices and Simulators**, select the discovered
   Apple TV, and enter the on-screen pairing code if requested.
4. Enable **Developer Mode** on the Apple TV under **Settings > Privacy &
   Security** when tvOS exposes that option, restart if requested, and confirm
   it in Xcode.
5. Keep the Apple TV awake while running the builder.

See Apple's [current Xcode device-pairing
help](https://help.apple.com/xcode/mac/current/#/dev23aab79b4) if the device does
not appear.

## Build options

### Install directly on Apple TV

In **Xcode > Settings > Accounts**, add your Apple Account. Xcode creates a
free Personal Team; paid Apple Developer Program membership is not required.
The builder selects a Personal Team and paired Apple TV, configures automatic
development provisioning, publishes Release `tvos-arm64` with full AOT and
trimming, signs, installs, launches, and waits for the seven FMOD banks and the
first real Celeste draw.

The locally chosen bundle identifier is the app's identity. Keep it unchanged
when re-signing. Changing it creates a separate app/defaults domain, so existing
saves can appear missing because tvOS sees a different app.

Personal Team provisioning expires after about seven days. Run
`./build-tvos.sh` again to re-sign and reinstall, retaining the same bundle
identifier.

### Create a signing-ready IPA

The IPA mode writes:

```text
dist/Celeste-tvOS-unsigned.ipa
└── Payload/
    └── Celeste.app/
```

This is a **signing-ready unsigned IPA**, not an installable app. It contains
no usable development signature or provisioning profile. A tvOS-capable
external signer must supply those before installation.

The conventional payload structure has been statically verified for tools
such as atvloadly and Sideloadly, but an atvloadly physical installation has
not been part of project acceptance. This repository does not depend on a
third-party signing service and never handles credentials for one. If an
external signer changes the bundle identifier, tvOS gives the app a different
save domain.

## Updating and re-signing

Pull the same `tvos-port` branch, retain the ignored local configuration and
bundle identifier, then run the builder again. `./build-tvos.sh --clean` clears
only Stage 8 build/output caches. `./build-tvos.sh --reset-config` forgets saved
local paths and choices without touching installed Apple TV saves.

## Saves

Settings and save slots 0, 1, and 2 are durable. The storage bridge maintains
two complete checksummed generations, compresses each logical file separately,
and falls back to the older valid one if the newest is corrupt. Celeste still
reads and writes ordinary uncompressed save files while it runs. This fixes the
old 32 KiB per-slot limit that could reject normal progress in Chapter 5.

Existing v1 installs require no manual migration. They load unchanged and move
to v2 on the next successful changed save while retaining the older recovery
generation. Accepted testing covered the original Chapter 5 failure,
termination, Apple TV restart, and replacement installation with the same app
identity.

There is no iCloud, cloud backup, cross-device sync, or uninstall-survival
guarantee. Saves are currently **shared between Apple TV users**. Personal
Teams cannot provision the Apple User Management entitlement used for
automatic per-user app storage, so this project intentionally uses one normal
app-private defaults domain.

## Controllers

An extended physical controller is the intended gameplay input. DualSense was
physically tested for movement, Jump, Dash, Grab, Confirm, Cancel, Pause,
cutscene skip, and rumble/haptic cleanup. Other Xbox, DualShock, and MFi-style
extended controllers may work through normal SDL/FNA mappings but have not been
certified by this project. The Siri Remote is not an intended Celeste gameplay
controller.

## Tested hardware and software

| Item | Proven configuration |
| --- | --- |
| Mac | Apple M1 MacBook Air, macOS 26.3, arm64 |
| Xcode / tvOS SDK | Xcode 26.6 / tvOS SDK 26.5 |
| .NET | SDK 10.0.302 / workload set 10.0.302.0 |
| Deployment target | tvOS 16.0 |
| Apple TV | Apple TV 4K (3rd generation), `AppleTV14,1` |
| Controller | Sony DualSense |
| Celeste | 1.4.0.0, itch.io Linux download |
| FMOD | Engine iOS/tvOS 1.10.09, build 97915 |

Other arm64 Apple TV models and newer compatible tvOS versions may work, but
have not been physically tested here.

## Limitations

- Personal-use self-build only; no App Store package or support.
- Personal Team installs need re-signing about every seven days.
- Saves are shared between Apple TV users and do not sync to the cloud.
- Steam and non-itch.io-Linux game inputs are untested.
- DualSense is the only physically accepted controller; the Siri Remote is not
  a gameplay controller.
- The arm64 simulator remains deliberately no-audio because the required FMOD
  1.10.09 simulator archives are x86_64-only.
- The app is large (roughly 1.1 GiB before IPA compression) because it packages
  the user's full content and seven FMOD banks.
- External IPA re-signing/install tools remain outside this project's control.

## Troubleshooting

Start with [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md). Complete logs
are written below ignored `dist/logs/`. Every stopped builder invocation names
`dist/logs/last-error.txt`; a failed build phase also names its complete command
log. `dist/build-summary.txt` contains a privacy-safe success summary. Do not
post game files, FMOD files, signed apps/IPAs, provisioning profiles,
certificates, account details, Team IDs, or device IDs in an issue.

## Advanced/manual build

[`docs/BUILDING.md`](docs/BUILDING.md) documents noninteractive builder flags,
ignored outputs, and internal verification. [`docs/STATUS.md`](docs/STATUS.md)
summarizes the architecture and links the historical audit reports. The
original legacy Xamarin.iOS project remains in the repository but is not the
recommended tvOS workflow.

## Credits

This work builds on [RoootTheFox/celeste-ios](https://github.com/RoootTheFox/celeste-ios)
and its contributors, including the original FNA/iOS port work and native build
foundation. Celeste is by Extremely OK Games / Maddy Makes Games. Runtime and
native work uses [FNA](https://github.com/FNA-XNA/FNA),
[SDL](https://github.com/libsdl-org/SDL),
[FNA3D](https://github.com/FNA-XNA/FNA3D),
[FAudio](https://github.com/FNA-XNA/FAudio),
[Theorafile](https://github.com/FNA-XNA/Theorafile),
[MoltenVK](https://github.com/KhronosGroup/MoltenVK),
[FMOD](https://www.fmod.com/), and the
[FMOD-SDL bridge](https://github.com/flibitijibibo/FMOD_SDL). Exact revisions
and notices are recorded by the native locks and generated licence bundles.

See the repository history for all contributors. In particular, the original
README credited TheSpydog for demonstrating the approach and developing the
native-library builder, r58Playz for touch/controller and newer-Xcode work, and
the original project author's collaborators.

## Legal

This is an unofficial community project and is not endorsed by or affiliated
with Extremely OK Games, Maddy Makes Games, FMOD, or Apple. Celeste names,
software, and artwork remain the property of their respective rights holders.
FMOD is governed by its own licence. The repository's [`LICENSE`](LICENSE)
applies to the covered repository source only; it does not grant rights to
Celeste game content, generated/decompiled Celeste source, FMOD SDK material,
or user-generated branded packages.
