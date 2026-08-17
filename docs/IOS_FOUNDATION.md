# Modern iOS foundation (experimental)

This repository contains a tracked modern iOS/iPadOS engineering foundation
and an experimental Celeste product lane. The product uses a user-owned
supported Celeste 1.4.0.0 FNA installation to generate the same canonical game
as tvOS, packages the real Content and seven FMOD banks, and runs touch- or
controller-driven Celeste gameplay on physical iPhone and iPad hardware. It is
still a developer-facing experimental lane rather than a finished public iOS
release.

The foundation uses one `net10.0-ios26.5` iPhone/iPad target with a minimum of
iOS 15.0. It enters SDL through the public iOS host, runs one FNA `Game`, and
presents through direct FNA3D Metal. It uses true `UIWindowScene` lifecycle,
landscape-only presentation, a 1280×720 logical surface with aspect-fit
letterboxing, native Retina drawable scaling, safe-area metrics for future
platform UI, and FNA's SDL controller path. It does not use MoltenVK, JIT, or
the legacy Xamarin application host.

## Current experimental Celeste lane

Stages 24C1–24D3 keep one canonical input/decompilation/transformation pipeline for
iOS, iPadOS, and tvOS. It reuses the locked nine-profile validator, canonical
source and Content, shared Apple-safe transforms, serializers, AOT inventory,
legacy XNB registration, and bundle reader. A small final iOS transform removes
tvOS-only Save Manager, Performance HUD, soft-reload and Leave bridges; adapts
the platform storage and FMOD-output boundaries; hides Celeste's desktop
application-Quit row; and binds the narrow iOS touch/prompt bridge. Controller
family detection is shared with tvOS, so a physical DualSense resolves to
PlayStation artwork without adding a second GameController input stack.

The physical product has one SDL handoff, one FNA `Game`, one Celeste runtime,
and one Celeste-owned FMOD Studio/low-level runtime. It renders through direct
FNA3D Metal and supports normal gameplay/death/respawn/lifecycle/save flows by
touch alone or with a physical controller. It intentionally contains no Save
Manager/LAN listener, tvOS Performance HUD, JIT, interpreter, or Xamarin host.

## Touch controls

The production touch overlay is available from **Options > Touch Controls**.
Defaults are Automatic visibility, Fixed eight-way movement, separate Jump and
Dash buttons, Toggle Grab for touch, 70% opacity, Jump/Dash sliding Off, touch
haptics On, and directional haptics Off. Automatic hides and clears touch when
a physical controller connects, then restores it when the controller
disconnects; Always permits deliberate coexistence. Off retains a recovery
control when no controller is present.

Fixed movement provides a repeatable centre, while Floating places the movement
origin beneath the first thumb inside a bounded left-side acquisition region.
Movement keeps a 0.18 radial deadzone and eight-direction sector selection with
8-degree hysteresis. Optional **Directional Haptics** pulses once when movement
starts and once when the thumb crosses into a different direction; it does not
repeat merely because a direction remains held. Jump remains held for
variable-height jumps and Dash produces one press edge. Optional Jump/Dash
sliding lets one held finger transfer between those two buttons.
The small top-left book control supplies Celeste's Journal/Special input, so
chapter records and contextual alternate menu actions are available without a
controller. Pause, Confirm, Cancel, Talk, menu navigation, aiming, and every
ordinary gameplay action also use Celeste's normal logical input paths.
The Options-page opacity is a global master. Every control also has its own
0–100% opacity in the layout editor, in 10% steps; editor geometry remains
visible even when its gameplay opacity is zero. Opacity never disables hit
testing. Haptics remain an independent preference.

**Edit Layout** opens a transactional full-display editor with separate Phone
and Tablet profiles. The safe area is a faint guide rather than a placement
boundary, so controls may occupy the physical corners and the letterbox or
pillarbox regions around Celeste. Fixed movement, the Floating movement
acquisition region, Jump, Dash, Grab, Pause, and Journal can be moved and
resized independently. Rectangles resize freely in width and height; circular
controls remain physical circles. The editor's solid circular preview is the
exact gameplay visual size, with a faint outer ring showing the larger touch
target. A visible circular disk may sit flush against any physical display edge;
only its larger invisible hit margin is clipped there.
Jump and Dash may remain separate or share one resizable Split Region with
either diagonal, vertical, or horizontal division; assignments can be swapped
and Button Sliding still applies. Each held half receives its own Jump/Dash
pressed colour. Grab may be a circle or an arbitrary
rectangle, including the compact shoulder-style preset. Up to four optional
Jump, Dash, Grab, Pause, Journal, Crouch Dash, or Quick Restart controls can be added,
duplicated, moved, resized, reshaped, given individual opacity, and deleted.
Journal is present by default but may be deleted; Movement, the selected
Jump/Dash scheme, one Grab, and Pause remain essential. Duplicate physical
controls feed one logical action: it remains held until the final owner is
released, and a second simultaneous Grab does not double-toggle. The editor
provides Undo, Reset Selected, Reset Layout, Mirror Layout, Done, and Cancel.
Its tools and guidance use Celeste's normal menu font with bounded auto-fitting.
Invalid visibly off-screen, undersized, or overlapping layouts cannot be saved.

Celeste's ordinary **Grab Mode** row is source-aware on iOS. Touch, Controller,
and Keyboard remember independent Hold, Invert, or Toggle choices, and the row
labels the source currently being configured. Grab geometry does not determine
its behavior; the same mode applies to any circle or rectangle.

Touch preferences live in validated app-private host defaults. They are not
part of `settings.celeste`, SaveData, save backups, or the tvOS persistence
format. Resetting them cannot alter game progress or controller preferences.
The tracked glyph masks are generated deterministically from attributed source
artwork; see the [touch-control artwork notice](../modern-ios/Assets/TouchControls/NOTICE.md).

Physical iPhone/iPad audio uses Apple's public playback audio-session category, so
the Ring/Silent switch does not mute Celeste. On foreground and after audio
route/interruption changes, the iOS host reactivates that same OS audio session
before resuming Celeste's existing FMOD root bus. It does not create or
reinitialize a second FMOD system.

## Accepted toolchain

- Apple silicon Mac
- macOS 26.3
- Xcode 26.6 (17F113), iOS SDK 26.5
- .NET SDK 10.0.302, workload set 10.0.302.0
- `ios` workload 26.5.10301
- FMOD Engine iOS/tvOS 1.10.09 build 97915 for physical-device audio

The iOS native graph is pinned independently from tvOS and produces arm64
device plus arm64 Apple-silicon Simulator slices for SDL2, FNA3D, FAudio,
Theorafile, and a narrowly reviewed 25-symbol Apple static-link stub library.
The accepted normalized logical SHA-256 is:

```text
9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2
```

The Apple TV native lock remains independently unchanged. Platform-neutral
Celeste generation is shared; native packaging, lifecycle, storage and
presentation adapters stay platform-specific where Apple requires it.

## Build the native foundation

```bash
scripts/check-ios-host.sh
scripts/fetch-ios-native-deps.sh --clean
scripts/build-ios-native.sh --clean
scripts/verify-ios-native.sh
scripts/prepare-ios-foundation.sh --clean
```

Generated native sources and XCFrameworks stay under ignored `.build/` and
`artifacts/` directories. No Apple or third-party binary is committed.

## Simulator lane

FMOD 1.10.09 predates arm64 Apple-silicon iOS Simulator libraries. The simulator
lane therefore has an explicit no-FMOD-audio policy; it must not substitute a
newer FMOD or use Rosetta/x86 emulation to hide that limitation.

```bash
scripts/build-ios-foundation.sh --simulator --clean
scripts/verify-ios-package.sh \
  --app artifacts/ios-host/simulator/publish/CelesteIOSRuntimeHost.app \
  --lane simulator
scripts/run-ios-foundation-simulator.sh --iphone
scripts/run-ios-foundation-simulator.sh --ipad
```

The runners verify a real direct-Metal first draw, sustained frames, the
`UIWindowScene`/`CAMetalLayer` relationship, and same-runtime
background/foreground. The iPad run also exercises the 4:3 letterbox policy.

## Physical iPhone/iPad lane

Mount the user-supplied FMOD Engine iOS/tvOS 1.10.09 build 97915 installer and
prepare its arm64 device libraries. The SDK path must contain `doc/` and `api/`:

```bash
scripts/prepare-fmod-ios.sh \
  --sdk-root "/Volumes/FMOD Programmers API iOS/FMOD Programmers API" \
  --clean
```

Sign into an Apple account in Xcode, connect and trust one Developer Mode
iPhone or iPad, then create local Personal Team provisioning. Team, bundle, and
device identifiers are private local values and must not be committed:

```bash
scripts/configure-ios-personal-team.sh \
  --team-id YOUR_TEAM_ID \
  --bundle-id your.unique.celeste.ios.foundation \
  --device-id YOUR_PAIRED_DEVICE_ID
```

That helper writes ignored `modern-ios/Local.Build.props`. Then build, verify,
install, and launch:

```bash
scripts/build-ios-foundation.sh --device --clean
scripts/verify-ios-package.sh \
  --app artifacts/ios-host/device/publish/CelesteIOSRuntimeHost.app \
  --lane device
scripts/run-ios-foundation-device.sh
```

The physical lane is Release, arm64, fully trimmed, full AOT, and
`UseInterpreter=false`. It validates the accepted FMOD 1.10.09 low-level and
Studio runtimes without requiring Celeste banks.

### Build real Celeste (experimental)

First prepare the native foundation, FMOD device libraries, and ignored
Personal Team configuration as above. Then provide one of the exact supported
user-owned Celeste FNA installations listed in
[Celeste inputs](CELESTE_INPUTS.md):

```bash
scripts/prepare-celeste-ios-runtime.sh \
  --game-root "/path/to/supported/Celeste" \
  --clean
scripts/build-ios-celeste.sh --clean
scripts/run-ios-celeste-device.sh
```

The preparation command regenerates Celeste below ignored `.build/`, checks
the shared canonical hashes, stages the exact Content/seven banks, and applies
only the narrow iOS product transform. The builder emits the signed local app
and ignored IPA below `artifacts/ios-celeste/device/`; it reports timed phases,
free space, and a heartbeat during full AOT. This lane is developer-facing and
requires a physical iPhone or iPad and controller. It does not use GitHub
Actions.

## Storage boundary

The Celeste save root is obtained through public Foundation APIs and resolves
inside the app sandbox at `Library/Application Support/Celeste/`. The product
does not migrate legacy Xamarin data or use tvOS compressed UserDefaults.

The iOS storage adapter restricts writes to `settings.celeste` and slots
`0.celeste`–`2.celeste`. It validates a complete serialized candidate, rotates
the valid primary to one bounded previous-good copy under `Backups/`, then uses
Foundation's native atomic replacement API for the primary. A missing or
invalid primary is repaired from its validated backup; if both copies are
invalid, Celeste receives its normal missing/new-file semantics rather than
accepting malformed state. No unbounded generations or project-visible temp
files are created.
This is a real platform boundary: the common Settings/SaveData serializers are
shared with tvOS, while iOS uses ordinary Application Support files and tvOS
retains its compressed UserDefaults persistence authority. The native iOS
write avoids the JIT-only `SafeFileHandle` constructor path that is unavailable
under full AOT. The app does not mark this Application Support state as
excluded from normal device backups; this is container backup behavior, not an
iCloud synchronization feature.

## Deliberately deferred

- save migration and document import
- Save Manager, QR pairing, or graceful Quit on iOS
- real-Celeste Simulator execution (FMOD 1.10.09 has no arm64 Simulator slice)
- Everest/mod support
- App Store distribution and polished iPad support

The retained Xamarin-era project is untouched and is not part of this modern
architecture. Current Apple TV behavior and its native reproducibility lock are
also unchanged.
