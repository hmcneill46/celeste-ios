# Celeste iOS Port v0.1.1 RC1

This release candidate is the first beginner-facing personal self-build of the
modern Celeste iPhone/iPad port.

## Highlights

- Complete touch-only menus and gameplay, with optional physical controllers.
- Custom Phone and Tablet touch layouts, per-control editing, Grab behaviours,
  haptics where supported, and layout sharing through Files.
- Direct Metal rendering and the real seven-bank FMOD soundtrack/audio path.
- Durable Settings and all three save slots with a previous-good recovery copy.
- Native **Data & Files** export, Share, validated import, and restore for
  ordinary `.celeste` files.
- One universal landscape app for iPhone and iPad, targeting iOS/iPadOS 15.0
  or later.
- One root `./build-ios.sh` workflow for doctor, unsigned package,
  development-signed package, and paired-device installation.

The iOS port version is **0.1.1**, bundle build **5**. Celeste game/content
remains the locked 1.4.0.0 FNA release.

## Requirements and limitations

This is an unofficial personal self-build, not an App Store release. You must
provide your own supported Celeste files and FMOD Engine iOS/tvOS 1.10.09 build
97915. A physical iPhone/iPad is the supported full-product route because that
FMOD package lacks an arm64 Simulator audio slice. A free Personal Team works,
but its development provisioning normally needs periodic renewal.

Everest/mod support, automatic cloud saves, iOS LAN Save Manager, and system
Open-In ownership are not included. See the [iPhone/iPad build and install
guide](../IOS_BUILDING.md) for the complete workflow and current boundaries.
