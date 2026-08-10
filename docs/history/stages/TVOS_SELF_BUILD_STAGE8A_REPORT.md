# Stage 8A: tvOS self-build and branding report

Status: **PASS**. All code, clean-build, signing, installation, launch,
package, privacy, repository-isolation and manual physical-branding gates have
passed. The locally generated icon focus/parallax and Top Shelf presentation
were confirmed on the physical Apple TV.

Starting commit: `e6f1be27a94befd19f41e1ce17cdd239ce5c0923`

Final commit: the commit containing this report, created with message
`build: add friendly tvOS self-build pipeline`. Its SHA is reported by Git
after creation and cannot be embedded self-referentially in the commit.

## Scope and result

Stage 8A adds a separate, friendly self-build path without rewriting the public
README or changing the legacy iOS lane. It keeps the existing modern tvOS
runtime, real FMOD audio, controller behaviour and Stage 6 standard-UserDefaults
persistence. It does not add User Management, iCloud, App Groups, a paid
entitlement, controller changes or App Store distribution.

The product bundle now reports the display name **Celeste**. A clean Release
device build was provisioned with the existing primary free Personal Team,
signed, installed and launched on the paired Apple TV. Device logs reached the
real Celeste first update and first draw at 1920×1080, loaded all seven FMOD
banks and reported 922 events, 118 buses and 3 VCAs. The app used FMOD native
1.10.09 through the accepted managed 1.10.20 surface.

A separate clean Release device build produced a signing-ready unsigned tvOS
IPA. It has a correct `Payload/Celeste.app` layout, contains no embedded profile
or usable signature, and is accurately described as requiring an external tvOS
signing tool.

## Artwork architecture

The tracked generator reads only these user-owned inputs:

- `$CELESTE_GAME_ROOT/Celeste.png`
- `$CELESTE_GAME_ROOT/Content/Graphics/SplashScreen.png`

It validates that both inputs are readable PNGs and that the strawberry input
contains both visible and transparent pixels. It never changes either source.
Swift, AppKit, Core Graphics and ImageIO are used, so no Pillow, Homebrew package
or downloaded image tool is needed.

Generated artwork is written below ignored `.build/tvos-self-build/artwork/`.
The generated asset catalog contains:

- a 400×240/800×480 layered small tvOS icon;
- a required 1280×768 App Store stack used only to satisfy the asset-catalog
  format, without making an App Store distribution claim;
- an opaque solid-black back layer;
- a transparent, aspect-fit strawberry front layer, centred within 46% of the
  landscape canvas to leave focus/parallax safe area;
- standard static Top Shelf images at 1920×720 and 3840×1440;
- wide static Top Shelf images at 2320×720 and 4640×1440.

The Top Shelf images use centred aspect-fill/crop and are never stretched.
`actool` compiles the catalog, and the final `Info.plist`/`Assets.car` identify
`App Icon - Small`, `Top Shelf Image` and `Top Shelf Image Wide`. Two independent
clean artwork generations produced the same normalized logical hashes. No
source or derived Celeste artwork is visible to Git.

## Builder architecture

The entry point is:

```bash
./build-tvos.sh
```

Its numbered interactive flow is:

1. check the Mac;
2. find and validate Celeste;
3. find and validate FMOD;
4. check Apple tooling/signing needs;
5. generate artwork;
6. prepare native, managed, content and FMOD inputs;
7. build;
8. package and/or install.

It delegates to the accepted Stage 1, Stage 3/6 and Stage 5 preparation and
verification scripts. Generated sources, content, native artifacts, local
configuration, signing information, apps, IPAs and logs remain ignored. It
supports:

```text
./build-tvos.sh
./build-tvos.sh --help
./build-tvos.sh --non-interactive --mode validate ...
./build-tvos.sh --clean
./build-tvos.sh --reset-config
```

Build choices are `install`, `ipa`, `both` and `validate`. The `--clean` option
removes only ignored Stage 8 caches and outputs. The reset option removes only
the ignored local Stage 8 JSON configuration; it does not touch installed-app
saves.

Complete command output is kept under ignored `dist/logs/`. Terminal output is
concise, and a failure ends with a plain-language Problem/Detected/Required/Fix
block and the rerun command. The builder installs no software, runs no `sudo`,
accepts no licence and downloads neither Celeste nor FMOD.

## Prerequisite matrix

| Check | Required | Accepted host result |
|---|---|---|
| Operating system | macOS | macOS 26.3 |
| Supported host | Apple silicon preferred | arm64 |
| Xcode | selected full Xcode, first launch complete | Xcode 26.6 |
| tvOS SDK | installed and discoverable | 26.5 |
| .NET SDK | exact feature band | 10.0.302 |
| Workload set | exact | 10.0.302.0 |
| tvOS workload | installed | present |
| Native tools | Git, Python, Swift, CMake, Ninja and Apple tools | present |
| Submodules | recorded revisions | pass; deliberately uninitialised nested FNA dependencies are allowed |
| Disk space | at least 8 GiB free | pass |
| Repository | writable, no wrong submodule revisions | pass |
| Entitlements | no paid capability | pass |

Missing prerequisites are diagnosed before preparation or building. The script
does not silently select a different .NET SDK/workload or accept a missing tvOS
SDK.

## Celeste and FMOD discovery

Celeste selection precedence is command line, `CELESTE_GAME_ROOT`, ignored saved
configuration, then an interactive pasted/dragged path. The existing exact
validator requires the unmodified FNA Celeste 1.4.0.0 installation and rejects
missing assemblies/content, wrong versions and common Everest modifications.
Stage 8 additionally requires both local artwork files.

FMOD selection precedence is command line, `FMOD_SDK_ROOT`, ignored saved
configuration, an unambiguous mounted official SDK, then an interactive path.
The existing validator requires FMOD Engine iOS/tvOS 1.10.09 build 97915. The
builder explains that the user must obtain and mount it through their own FMOD
account; it neither logs in nor downloads or redistributes the SDK.

## Local configuration and app identity

Ignored `.build/tvos-self-build/config.json` stores validated local paths,
preferred mode, a local bundle identifier and, when needed, the selected team
and paired device. Environment variables and command-line arguments override
saved values. All saved paths are revalidated on every run.

The tracked project fallback is the non-private example
`com.example.celeste-tvos`; product builds use a configurable local reverse-DNS
identifier. Signing data remains in ignored `tvos/Local.Build.props`. No Apple
password, two-factor code, key, certificate, profile or credential is stored.

Changing the bundle identifier creates a different installed app identity and
standard-UserDefaults domain, so existing saves will appear unavailable. The
builder warns before building and repeats the identity rule after installation.

## Direct Personal Team installation

Result: **PASS (machine-observed)**.

The builder found the existing primary free Personal Team, refreshed automatic
development provisioning for the local explicit identifier, reused the paired
Apple TV and produced a Release `tvos-arm64` app with full trimming, full AOT and
`UseInterpreter=false`. It requested no paid capability.

Independent checks proved:

- the executable is one `arm64` `TVOS` Mach-O with minimum tvOS 16.0;
- strict code-sign verification passes;
- the embedded profile is a current device-development profile;
- the profile application identifier and team match the signed executable;
- no User Management, iCloud or App Group entitlement is present;
- installation and launch succeed;
- the app reaches the first real Celeste draw and real FMOD bank-ready state.

The builder prints the Personal Team seven-day expiry warning and instructs the
user to rerun it to re-sign/reinstall while keeping the same bundle identifier.
No signing or device value appears in tracked files or this report.

## Signing-ready unsigned IPA

Result: **PASS**.

| Property | Verified value |
|---|---|
| Path | `dist/Celeste-tvOS-unsigned.ipa` |
| Archive size | 895,100,467 bytes (about 853.6 MiB) |
| SHA-256 | `0dedb983f1af51a42768640dde916077a4f53f40a17c2364d9782fe51103f19d` |
| Payload | exactly `Payload/Celeste.app` |
| Display name | `Celeste` |
| Platform/architecture | `TVOS` / `arm64` |
| Minimum OS | tvOS 16.0 |
| AOT/interpreter | full AOT / no interpreter |
| Signing | no usable signature |
| Provisioning | no embedded profile |
| Banks | exact accepted seven-bank set |

The unsigned app has a logical size of 1,215,233,362 bytes. The final signed app
has a logical size of 1,215,911,005 bytes. Its executable is 35,474,288 bytes,
the seven banks total 665,721,576 bytes, non-audio Content totals 492,943,607
bytes, and compiled `Assets.car` is 12,391,656 bytes. No bank duplication was
found.

The package contains no loose `.a`, `.so`, `.dylib`, header, SDK, generated
source, profile or local builder path. Trimmed managed metadata DLLs required by the
.NET Apple bundle are distinguished from forbidden native/desktop DLLs.

## Sideload-tool compatibility

Static compatibility with tvOS-capable IPA re-signers such as atvloadly and
Sideloadly: **PASS**. The package has the conventional Payload layout, a valid
tvOS plist, no extension, no paid entitlement, no User Management, no iCloud,
no App Group, statically linked native dependencies and bundle-contained Content
and banks. Stage 6 refers only to standard app-private UserDefaults and does not
hardcode a bundle identifier.

A user-assisted atvloadly upload/install was not performed in this run and
remains an explicitly open optional external-tool check. The IPA is not labelled
or presented as directly installable. If an external signer rewrites the bundle
identifier, it creates a different defaults domain; refreshes must retain that
same external app identity to see the same saves.

## Incremental behaviour

Build reuse is keyed by the repository revision and relevant diff, locked input
manifests, validated Celeste/FMOD identity, Stage 1 logical set, generated
managed manifests, artwork file hashes, local bundle metadata, RID, launch mode,
configuration, AOT, trim and signing mode. Signed and unsigned products use
separate roots. A mismatch invalidates reuse. The accepted Stage 1 artifacts are
reused; a clean clone can invoke the locked Stage 1 fetch/build/verify pipeline
when they are absent.

## Prior-stage regression and isolation

- Stage 1 accepted logical hash remains
  `61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39`.
- No Stage 1 library was rebuilt during acceptance.
- Exact FMOD 1.10.09 staging verification passes.
- Stage 6 device package, privacy manifest and writable-state isolation pass.
- A fresh trimmed Release `tvossimulator-arm64` no-audio build succeeds and
  contains no FMOD banks. The first attempt incorrectly requested
  `PublishTrimmed=false`; the current .NET tvOS SDK correctly rejected it, and
  the supported trimmed command passed without changing source.
- The simulator and device builds retain the accepted, individually documented
  generated-code/legacy FNA trim warning baseline. Stage 8 introduced no new
  unresolved AOT or native warning.
- The existing iOS project, build script and checked-in archives are unchanged.
- Real-audio device startup and Stage 6 bridge initialization are observed.
- Current controller and haptic code is unchanged; exhaustive Stage 4
  certification was deliberately not reintroduced.
- No Stage 7 entitlement, iCloud capability or paid-account requirement exists.

## Failures and focused corrections

1. An early asset catalog included an obsolete/unassigned legacy large icon
   stack. The catalog was reduced to the roles accepted by Xcode 26.6; a clean
   compile then completed without asset warnings.
2. The simulator regression was first invoked with `PublishTrimmed=false`.
   Modern .NET tvOS requires trimmed publishing, so the SDK stopped before a
   product was produced. The accepted trimmed Release lane then built normally.
3. Device-console attachment can outlive the bounded startup proof. The builder
   now terminates only its exact attached process after the required checkpoints
   or timeout, with a short bounded escalation if it does not exit.
4. Release assemblies initially retained local PDB path strings even though no
   PDB was packaged. Disabling compiler debug output removed the host record,
   while `TrimmerRemoveSymbols=true` still left CodeView records in linked
   dependencies; the verifier rejected both candidates. A narrow deterministic
   post-publish tool now edits only CodeView PDB-path fields in managed PE files.
   The unsigned app is sanitized before packaging. The signed app's existing
   entitlements are captured privately, it is sanitized, then it is re-signed
   with the exact selected Personal Team identity and re-verified against its
   embedded profile. The package verifier rejects the exact local repository
   path in every bundled file. Paths compiled into the external proprietary
   FMOD archive by its vendor are upstream metadata, not builder identity data.

No failure produced a misleading IPA. Failed/partial products stayed below
ignored build roots.

## Proprietary and private isolation

The candidate verifier rejects tracked or Git-visible app bundles, IPAs, PNGs,
banks, profiles and signing material. The final tracked changes contain only
generator source, build/verification scripts, neutral project metadata, ignore
rules and this report. Celeste content, generated managed source, generated
artwork, FMOD inputs, app bundles, IPAs, local configuration and evidence remain
ignored.

Privacy-safe user-facing outputs are under `dist/`; the repository ignores that
directory. The summary omits real bundle, team, device, profile, certificate,
account and absolute input paths.

## Manual physical branding acceptance

Result: **PASS (user-confirmed on the physical Apple TV)**.

The user confirmed that:

- the installed home-screen name is `Celeste`;
- the strawberry is centred and unclipped on a black background;
- the two-layer focus/parallax effect is visible;
- when the app is placed in the top row, the static splash Top Shelf artwork
  appears and is not visibly stretched;
- launching from the branded icon still provides working menus, controller
  input and real FMOD audio.

No Stage 8A acceptance gate remains open. The optional physical atvloadly
installation was not performed and remains explicitly non-blocking.

## Stage 8B work remaining

After Stage 8A is committed, Stage 8B should rewrite the public README around
the proven `./build-tvos.sh` workflow. It should cover lawful Celeste and FMOD
inputs, exact prerequisites, direct Personal Team installation, the seven-day
expiry, unsigned-IPA external signing, bundle-identifier/save-domain stability,
shared (not per-user) saves, simulator no-audio behaviour, troubleshooting,
licensing and the absence of App Store support. The optional atvloadly physical
test can be added if separately completed.
