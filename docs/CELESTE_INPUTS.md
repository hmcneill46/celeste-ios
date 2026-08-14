# Supported Celeste game inputs

The tvOS builder accepts a small, explicit set of unmodified Celeste 1.4.0.0
FNA distributions. You must supply your own lawfully obtained game files. This
repository does not contain, download, or redistribute Celeste.

The builder identifies the actual payload rather than trusting a folder or
archive name. It checks the managed assembly identities and hashes, FNA runtime,
content manifest, package-layout markers, required/forbidden files, and expected
assembly references. Unknown, modified, mixed, modded, XNA, and newer/older
builds fail closed.

## Which download should I use?

Choose the store where you already own Celeste. You need only **one** supported
game folder; the builder does not need a store client after that folder has been
obtained.

| Store | Simplest supported choice |
| --- | --- |
| itch.io | Download an unmodified Celeste 1.4.0.0 FNA package from your itch.io library. |
| Steam | Use Steam's built-in console to download the tested public-2025 Linux depot. This is the recommended Steam route on a Mac. |
| Epic Games Store | Use one clean Mac or Windows FNA package from your account. The optional Legendary commands below can obtain either. |

The filenames and folder names are not proof of compatibility. After extracting
the download outside this repository, let the validator identify it:

```bash
scripts/validate-celeste-input.sh --game-root "/path/to/Celeste"
```

### itch.io

1. Sign in to the itch.io account that owns Celeste and open your library.
2. Download an unmodified Celeste 1.4.0.0 FNA package represented in the matrix
   below.
3. Extract it outside this repository.
4. Give the resulting folder or macOS app to `build-tvos.sh`.

An arbitrary itch.io download is not accepted based on its filename alone; the
validator checks the actual game, runtime, and content.

### Steam — recommended Linux depot

This route uses Steam's built-in console. The Linux files are lawful
Celeste/FNA/content input for the builder; you do not run the Linux game on your
Mac.

1. Install Steam and sign in to an account that owns Celeste.
2. Enter `steam://open/console` in a browser address bar and allow it to open
   Steam.
3. Open Steam's **Console** tab and enter:

   ```text
   download_depot 504230 504233 5880027853585448535
   ```

4. Steam normally shows no useful progress bar for this operation. Wait for the
   completion message, for example:

   ```text
   Depot download complete: ".../steamapps/content/app_504230/depot_504233"
   ```

5. Use the exact path Steam prints. Copy or move the completed `depot_504233`
   folder somewhere convenient outside Steam's content-depot area, such as
   `~/Downloads/Celeste-Linux`.
6. Give that folder to `./build-tvos.sh`.

A complete supported Linux depot contains at least `Celeste.exe`,
`Celeste.Content.dll`, `FNA.dll`, `Steamworks.NET.dll`, `Content/`, `lib/`, and
`lib64/`.

Other exact Steam inputs tested by this project are:

```text
# Linux 1.4.0.0 pinned 2021
download_depot 504230 504233 1505052356460012099

# Linux public 2025 (recommended)
download_depot 504230 504233 5880027853585448535

# macOS FNA 1.4.0.0
download_depot 504230 504232 3271492884622616896
```

The Steam Windows FNA profile in the matrix is accepted if you already have
that exact payload, but obtaining its unusual `opengl` branch offers no product
benefit over the recommended Linux depot. The project does not recommend
DepotDownloader to ordinary users.

### Epic Games Store — optional Legendary route

The project does not download Epic files. One optional way to obtain files you
own on macOS is the open-source [Legendary CLI](https://github.com/derrod/legendary).
Its current supported setup requires 64-bit Python 3.10 or newer. Check first:

```bash
python3 --version
python3 -m pip install --user legendary-gl
legendary auth
legendary list
```

`legendary auth` signs in to your Epic account. Find Celeste in `legendary
list` and note its **App Name**. A project test account reported `Salt`, but do
not assume it is universal: use the App Name shown for your own account.

Set that value and download **one** supported platform package:

```bash
EPIC_APP="<App Name shown by legendary list>"
mkdir -p "$HOME/Celeste-Clean-Builds/Epic"

# Natural choice on a Mac
legendary install "$EPIC_APP" \
  --platform Mac \
  --base-path "$HOME/Celeste-Clean-Builds/Epic" \
  --game-folder "Celeste-macOS-FNA-1.4.0.0" \
  --download-only

# Supported alternative
legendary install "$EPIC_APP" \
  --platform Windows \
  --base-path "$HOME/Celeste-Clean-Builds/Epic" \
  --game-folder "Celeste-Windows-FNA-1.4.0.0" \
  --download-only
```

Legendary is an optional acquisition tool, not a dependency of this project.
It is not needed after the game folder has been downloaded, and the builder
never receives your Epic credentials.

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

The store-specific instructions above describe clean acquisition through an
account that owns Celeste. They are conveniences, not builder dependencies:
once the folder exists, the builder only reads it.

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

The separate required native FMOD input remains the official
[FMOD Engine iOS/tvOS 1.10.09 build 97915](https://www.fmod.com/download?version=1.10.09#fmodengine)
SDK. Choose FMOD Engine's iOS package, not FMOD Studio; the iOS package contains
the required tvOS libraries. The `.bank` files bundled with Celeste are game
content and are not a substitute for that SDK.
