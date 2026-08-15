# Modern iOS foundation (experimental)

This repository contains a tracked modern iOS/iPadOS engineering foundation.
It is **not a playable Celeste iOS port**: it does not include the game,
generated Celeste code, touch controls, save integration, or game audio banks.
Its purpose is to prove the production-native boundary needed by later work.

The foundation uses one `net10.0-ios26.5` iPhone/iPad target with a minimum of
iOS 15.0. It enters SDL through the public iOS host, runs one FNA `Game`, and
presents through direct FNA3D Metal. It uses true `UIWindowScene` lifecycle,
landscape-only presentation, a 1280×720 logical surface with aspect-fit
letterboxing, native Retina drawable scaling, safe-area metrics for future
platform UI, and FNA's SDL controller path. It does not use MoltenVK, JIT, or
the legacy Xamarin application host.

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

## Physical iPhone lane

Mount the user-supplied FMOD Engine iOS/tvOS 1.10.09 build 97915 installer and
prepare its arm64 device libraries. The SDK path must contain `doc/` and `api/`:

```bash
scripts/prepare-fmod-ios.sh \
  --sdk-root "/Volumes/FMOD Programmers API iOS/FMOD Programmers API" \
  --clean
```

Sign into an Apple account in Xcode, connect and trust one Developer Mode
iPhone, then create local Personal Team provisioning. Team, bundle, and device
identifiers are private local values and must not be committed:

```bash
scripts/configure-ios-personal-team.sh \
  --team-id YOUR_TEAM_ID \
  --bundle-id your.unique.celeste.ios.foundation \
  --device-id YOUR_PAIRED_IPHONE_ID
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

## Storage boundary

The future save root is obtained through public Foundation APIs and resolves
inside the app sandbox at `Library/Application Support/Celeste/`. Stage 24B
creates only the directory. It does not create or migrate save files.

`CelesteIOSFoundation.AtomicFileStore` restricts writes to `settings.celeste`
and slots `0.celeste`–`2.celeste`, writes a sibling temporary file, flushes it,
and atomically replaces the destination. Deterministic fault tests prove that
a failure before commit preserves the previous file and removes temporary
files. Full Celeste persistence integration remains later work.

## Deliberately deferred

- running Celeste or loading proprietary content
- touch controls and touch UI
- save migration or Stage 9B persistence integration
- Save Manager, QR pairing, or graceful Quit on iOS
- Celeste FMOD banks/game audio
- Everest/mod support
- App Store distribution and polished iPad support

The retained Xamarin-era project is untouched and is not part of this modern
architecture. Current Apple TV behavior and its native reproducibility lock are
also unchanged.
