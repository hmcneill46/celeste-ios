# Supported Celeste game inputs

The tvOS builder accepts a small, explicit set of unmodified Celeste 1.4.0.0
FNA distributions. You must supply your own lawfully obtained game files. This
repository does not contain, download, or redistribute Celeste.

The builder identifies the actual payload rather than trusting a folder or
archive name. It checks the managed assembly identities and hashes, FNA runtime,
content manifest, package-layout markers, required/forbidden files, and expected
assembly references. Unknown, modified, mixed, modded, XNA, and newer/older
builds fail closed.

## Accepted profiles

| Store | Source OS | Game | Runtime | Profile | Canonical class | Status | Physical evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| itch.io | Linux | 1.4.0.0 | FNA 21.3.5 | `itch-linux-fna-1.4.0.0` | `celeste-1.4.0.0-a` | Supported — physically validated | Integrated release-candidate acceptance |
| itch.io | macOS | 1.4.0.0 | FNA 21.3.5 | `itch-macos-fna-1.4.0.0` | `celeste-1.4.0.0-a` | Supported — canonical-equivalent | Redundant device run omitted |
| itch.io | Windows | 1.4.0.0 | FNA 21.3.5 | `itch-windows-fna-1.4.0.0` | `celeste-1.4.0.0-a` | Supported — canonical-equivalent | Redundant device run omitted |
| Epic Games Store | Windows | 1.4.0.0 | FNA 21.3.5 | `epic-windows-fna-1.4.0.0` | `celeste-1.4.0.0-a` | Supported — physically validated | Stage 17B representative |
| Epic Games Store | macOS | 1.4.0.0 | FNA 21.3.5 | `epic-macos-fna-1.4.0.0` | `celeste-1.4.0.0-a` | Supported — canonical-equivalent | Redundant device run omitted |
| Steam | Linux, pinned manifest `1505052356460012099` | 1.4.0.0 | FNA 21.3.5 | `steam-linux-fna-1.4.0.0-manifest-1505052356460012099` | `celeste-1.4.0.0-a` | Supported — physically validated | Stage 17A representative |
| Steam | Linux, tested public 2025 payload | 1.4.0.0 | FNA 21.3.5 | `steam-linux-public-2025-fna-1.4.0.0` | `celeste-1.4.0.0-a` | Supported — canonical-equivalent | Redundant device run omitted |
| Steam | macOS | 1.4.0.0 | FNA 21.3.5 | `steam-macos-fna-1.4.0.0` | `celeste-1.4.0.0-a` | Supported — canonical-equivalent | Redundant device run omitted |
| Steam | Windows, depot `504231`, manifest `1981411158533599226` | 1.4.0.0 | FNA 21.3.5 | `steam-windows-fna-1.4.0.0-manifest-1981411158533599226` | `celeste-1.4.0.0-a` | Supported — canonical-equivalent | Windows ingestion physically covered by Epic; Steam normalization physically covered by Linux; exact canonical output matched |

“Exact canonical equivalence” means the accepted input independently produced
the same normalized Celeste source and complete Stage 6 generated tree as the
physically tested representatives. It does not mean every release ever
published by that store is supported.

All nine profiles contain the same locked 1.4.0.0 game content and normalize
to one downstream tvOS product class. Storefront-specific desktop libraries are
never copied into the Apple TV app. Steam inputs use one exact, zero-fuzz source
adapter that removes Steam startup, callbacks, stats, achievements, and the
managed Steamworks reference before the shared transformations run.

## Selecting the folder

Extract the game outside this repository and pass the extracted folder to the
builder interactively, with `--game-root`, or through `CELESTE_GAME_ROOT`:

```bash
export CELESTE_GAME_ROOT=/path/to/extracted/celeste
./build-tvos.sh
```

You may select the game root itself, a macOS `.app` bundle, or the single
wrapper folder produced by one of the supported archives. The builder resolves
only the known bounded layouts and prints the detected version, store, source
platform, runtime, and profile. No store flag is required.

Validate without building:

```bash
scripts/validate-celeste-input.sh --game-root "$CELESTE_GAME_ROOT"
```

The root README has [concise, store-specific
instructions](../README.md#getting-a-clean-supported-celeste-copy) for
obtaining a clean copy through an account that owns Celeste. Steam users should
normally use the tested Linux depot through Steam's built-in console; there is
no product benefit
to obtaining the unusual Windows `opengl` branch. Epic owners may optionally
use Legendary to download one supported Mac or Windows payload, but Legendary
is not a builder dependency.

The selected game files are read-only inputs. Extraction, decompilation,
normalization, and generated content remain in ignored local build directories.

## Unsupported

- Everest, MonoMod, or any modified installation
- XNA builds
- other Celeste versions, even if their directory layout looks similar

## Not tested

- Steam payloads other than the exact profiles above
- Microsoft Store/Xbox App
- Humble and other storefront packages
- console packages

An unsupported but recognizable installation reports that the exact build is
not yet supported. Do not rename, patch, or combine files from different
distributions to imitate a supported profile.

The separate required native FMOD input remains the official FMOD Engine
iOS/tvOS 1.10.09 build 97915 SDK. The `.bank` files bundled with Celeste are
game content and are not a substitute for that SDK.
