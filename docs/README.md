# Documentation

The root [Celeste for Apple TV README](../README.md) is the best starting point.
Use this page to jump directly to the task or level of detail you need.

## Getting started

- [Build locally on a Mac](BUILDING.md) — prerequisites, copyable setup,
  builder choices, signing, and generated outputs.
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

- [Experimental modern iOS/Celeste lane](IOS_FOUNDATION.md) — the .NET 10,
  full-AOT, controller-first iPhone/iPad foundation and current gameplay/save
  developer build; touch controls remain deliberately deferred.
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
- [v1.0.0-rc.2 manifest](../tvos/release-candidates/v1.0.0-rc.2.json)
- [Development and acceptance history](history/README.md) — the original port
  plan and chronological stage reports retained for reproducibility and
  debugging.

The history explains how decisions were reached. It is not the current user
workflow; use [Status](STATUS.md) for the present implementation.
