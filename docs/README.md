# Documentation

The root [Celeste for Apple platforms README](../README.md) is the best starting point.
Use this page to jump directly to the task or level of detail you need.

## Getting started

- [Build locally on a Mac](BUILDING.md) — Apple TV prerequisites, builder
  choices, signing, and generated outputs.
- [Build and install on iPhone/iPad](IOS_BUILDING.md) — the beginner modern-iOS
  doctor, unsigned/signed builds, Personal Team setup, and paired-device install.
- [Build in the cloud](CLOUD_BUILDING.md) — compile an unsigned IPA in your own
  private GitHub repository when no suitable Mac is available.
- [Choose supported Celeste files](CELESTE_INPUTS.md) — beginner store guidance
  followed by the exact nine-profile compatibility matrix.
- [Troubleshooting](TROUBLESHOOTING.md) — a common-question index and detailed
  remedies for inputs, FMOD, cloud builds, signing, saves, and runtime issues.

## Features

- [Save Manager](../README.md#save-manager) — private same-network backup,
  validated restore, one-time QR pairing, and verified soft reload.
- [Controller Prompts](../README.md#apple-tv-integration) — select the artwork
  family without remapping physical controls.
- [Performance HUD](../README.md#apple-tv-integration) — toggle Apple's native
  Metal diagnostics from Celeste Options.
- [Current capabilities and limitations](STATUS.md) — what has been physically
  tested, what is expected to work, and what is unsupported.

## Developer documentation

- [Modern iOS/Celeste architecture](IOS_FOUNDATION.md) — the .NET 10,
  full-AOT iPhone/iPad developer build with editable Phone/Tablet touch
  layouts, full-display placement, per-control opacity, optional duplicate
  controls, controller coexistence, seven-bank audio, durable saves, and
  native Files-based save/layout transfer.
- [Experimental Apple Everest static-AOT architecture](APPLE_EVEREST_STATIC_AOT.md)
  — contributor documentation for the shared build-time canary and closed
  real-ZIP/precompiled-DLL foundation; general mod compatibility is not yet
  supported.
- [Apple Everest tested-mod compatibility](APPLE_EVEREST_COMPATIBILITY.md)
  — exact real ZIPs audited by the experimental static-AOT ladder, including
  supported and deferred mechanism classes.
- [Apple Everest host-tool third-party notices](APPLE_EVEREST_THIRD_PARTY.md)
  — exact upstream provenance and licensing scope.
- [Project architecture](STATUS.md#architecture) — host, native, generated
  managed code, audio, persistence, Save Manager, and lifecycle design.
- [Advanced build and reproducibility](BUILDING.md) — pinned toolchain,
  noninteractive modes, transformations, verification, and output locations.
- [Native dependency pipeline](../native/README.md) — XCFramework production,
  platform checks, native locks, and link closure.
- [Contributing](../CONTRIBUTING.md) — source boundaries, verification, and
  privacy expectations.
- [`scripts/`](../scripts/) — tracked validators and deterministic verification
  entry points.

## Release and history

- [v1.0.0-rc.2 release notes](releases/v1.0.0-rc.2.md)
- [iOS Port v0.1.1 RC1 draft release notes](releases/ios-v0.1.1-rc.1.md)
- [v1.0.0-rc.2 manifest](../tvos/release-candidates/v1.0.0-rc.2.json)
- [Development and acceptance history](history/README.md) — the original port
  plan and chronological stage reports retained for reproducibility and
  debugging.

The history explains how decisions were reached. It is not the current user
workflow; use [Status](STATUS.md) for the present implementation.
