# v1.0.0-rc.2 integrated acceptance — Stage 20

Status: **PASS — RC2 READY**

Stage 20 treated the accepted product source as feature-frozen. No runtime,
persistence, networking, generated-Celeste, controller, audio, native, or
gameplay source was changed. The only pre-acceptance changes were the RC2
manifest/release documentation, an RC2 verifier, and the public cloud-template
source-pin update required to build the frozen product source.

## Git and release identity

- Starting and product-source commit:
  `b5f2ec2fdd5c65c533d86aae750ee60dcaf5009e`.
- Release branch: `release/v1.0.0-rc.2`.
- Preparation commit:
  `3a8d4bf39a0243eb0e7671e7e655a8561f52d67e`.
- Final RC2 candidate commit: the commit containing this report; its exact SHA
  is recorded by the release-branch history and Stage 20 completion response
  without creating a self-referential manifest.
- Immutable `v1.0.0-rc.1^{}` remained
  `ee52b0868df091746f134d95d4f020f94f23d4fb`.
- Intended later tag: `v1.0.0-rc.2`.
- No `v1.0.0-rc.2` tag or GitHub Release existed during acceptance.
- Local and origin `tvos-port` remained at the product-source commit. Upstream
  was not modified.

The machine-readable candidate description is
[`tvos/release-candidates/v1.0.0-rc.2.json`](../../../tvos/release-candidates/v1.0.0-rc.2.json),
and the user-facing candidate notes are
[`docs/releases/v1.0.0-rc.2.md`](../../releases/v1.0.0-rc.2.md).

## RC1 to RC2 product delta

RC2 adds the already accepted post-RC1 work as one integrated product:

- one-time Save Manager QR pairing while retaining manual address/code auth;
- live tvOS Metal Performance HUD control;
- nine exact itch.io, Epic Games Store, and Steam FNA input profiles;
- timed local-builder phases, heartbeat, bounded diagnostics, and CI output;
- the private GitHub Actions unsigned-IPA builder;
- the active-Level soft-reload final-render correction; and
- semantic names in active production/test code without changing stable
  storage or protocol contracts.

## Nine real Celeste inputs

The originals were never modified. Each archive was extracted through the
bounded traversal-safe path and automatically identified without a store or
profile override.

| Profile | Store / source OS | Result |
|---|---|---|
| `itch-linux-fna-1.4.0.0` | itch.io / Linux | PASS |
| `itch-macos-fna-1.4.0.0` | itch.io / macOS | PASS |
| `itch-windows-fna-1.4.0.0` | itch.io / Windows | PASS |
| `epic-windows-fna-1.4.0.0` | Epic / Windows | PASS |
| `epic-macos-fna-1.4.0.0` | Epic / macOS | PASS |
| `steam-linux-fna-1.4.0.0-manifest-1505052356460012099` | Steam / Linux | PASS |
| `steam-linux-public-2025-fna-1.4.0.0` | Steam / Linux | PASS |
| `steam-macos-fna-1.4.0.0` | Steam / macOS | PASS |
| `steam-windows-fna-1.4.0.0-manifest-1981411158533599226` | Steam / Windows | PASS |

All nine resolved to Celeste 1.4.0.0, FNA 21.3.5, complete expected
references, and canonical class `celeste-1.4.0.0-a`. Recalculated archive
fingerprints matched the accepted Stage 17 records. Representative modified
executable/FNA/Content, mixed-store, unknown Steam, XNA, and ambiguous-wrapper
cases remained fail-closed through the 56-test profile suite; there is no
force or accept-any mode.

| Canonical boundary | Files | SHA-256 |
|---|---:|---|
| Content | 1,216 | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Raw source | 920 | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Patched source | 922 | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Stage-6 real-audio tree | 934 | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |

The clean local and both cloud builds independently retained native logical
SHA-256
`6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`.

## Clean local build

A disposable clone of the actual release branch initialized every recursive
submodule and began with no ignored product/native output. It used the direct,
no-Steam itch.io Linux profile and the user-supplied FMOD 1.10.09 build 97915
SDK. No prebuilt Stage-1 or app output was copied into it.

The literal public journey passed builder help, host doctor, exact Celeste and
FMOD validation, repository isolation, and the 91-link documentation/product
audit. The accepted host was arm64 macOS 26.3, Xcode 26.6, tvOS SDK 26.5,
.NET SDK 10.0.302, and workload set 10.0.302.0.

`./build-tvos.sh --mode both` completed in **25m19s**:

| Phase | Time |
|---|---:|
| Checking this Mac | 1s |
| Finding Celeste | 3s |
| Finding FMOD | 1s |
| Checking Apple tooling | <1s |
| Generating artwork (deferred) | <1s |
| Preparing the game | 12m57s |
| Building two independent full-AOT products | 6m32s |
| Packaging/installing/verifying | 5m45s |

Long operations emitted 60-second elapsed-time/free-disk heartbeats, with no
fake percentage, broken-pipe noise, or ANSI output under `--no-color`.
Stage 1 rebuilt in 10m12s and verified its six device/simulator XCFrameworks
and logical hash independently.

The local signing-ready IPA was:

- bytes: **895,771,614**;
- SHA-256:
  `a46a5afd37a05cb92ec222493b3f22686c28551d2ff7281db0b0db05df6b0124`;
- uncompressed app bytes: **1,217,130,532**;
- `Payload/Celeste.app`, arm64 TVOS, minimum tvOS 16.0;
- Release `tvos-arm64`, full trimming, full AOT, `UseInterpreter=false`;
- seven FMOD banks, branding, icon/Top Shelf, privacy manifest, Save Manager,
  QR, Controller Prompts, Performance HUD, graceful Quit, and soft reload;
- no usable signature, embedded provisioning profile, User Management
  entitlement, Game Mode key, private input, or diagnostic acceptance file.

The separately published signed app was 1,217,819,547 bytes. It installed as a
same-identity replacement in 4m26s; the builder confirmed launch, real banks,
and first draw. No uninstall or bundle-identity change occurred.

## Physical Apple TV acceptance

The same-identity candidate ran on the accepted Apple TV 4K with a physical
DualSense. The prior app-private domain survived replacement: all three save
slots and Settings were readable, Controller Prompts remained independent,
and the Performance HUD preference remained independent. The initial
production snapshot was v2 generation 234 with 60,735 raw logical bytes,
6,974 compressed payload bytes, and 15,307 bridge bytes across 36 app-domain
keys; no UserDefaults size warning occurred.

Physical and console-correlated results:

- cold launch, first 1920×1080 draw, title/main menu, and saved data: PASS;
- seven banks loaded once; title/gameplay music, UI and gameplay SFX,
  ambience, death/respawn, pause/resume, volume/mute, and foreground resume:
  PASS;
- DualSense movement, Jump, Dash, Grab, navigation, pause/resume,
  death/respawn, and haptics: PASS;
- meaningful multi-room gameplay with repeated deaths/respawns and lifecycle
  transitions, including a final uninterrupted seven-minute smoke after the
  normal cold launch: PASS;
- Automatic prompts resolved DualSense to PlayStation; Xbox, PlayStation,
  Nintendo Switch, and Stadia artwork all displayed while bindings remained
  untouched: PASS;
- native HUD On/Off applied live with logging disabled; On and Off both
  survived focused Home/foreground tests and the final state was Off: PASS;
- repeated normal Home/background/foreground resumed the same runtime, audio,
  input, and HUD policy without duplication: PASS.

The console showed one host startup, one FNA runtime, one graphics device, one
FMOD runtime, advancing update/draw heartbeats, and no managed fatal,
SIGABRT, Metal assertion, ContentLoadException, persistence corruption, or
tvStubs invocation. Logged FMOD `ERR_EVENT_NOTFOUND` entries were the accepted
non-applicable-parameter probes; music/SFX continued and all banks remained
ready.

## Save Manager and active-Level soft reload

Save Manager was explicitly opened from an active `Level`; it was dormant
before activation. The overlay presented the real local URL, six-digit code,
and a crisp 408-pixel QR (43 modules, integer scale 8, correction M, four-module
quiet zone). A real iPhone Camera/Safari scan authenticated directly. Reusing
the QR in a fresh browser was rejected, two deliberately wrong manual code
attempts returned 401, and the correct manual code authenticated. The original
QR/session path and manual fallback therefore both passed without exposing a
credential in logs.

Before mutation, Download All, Settings, and present slots 0–2 were exported
as ordinary Celeste data. A private local/phone backup was retained only for
restoration; no save data entered Git.

Settings Reset while the old `Level` was active committed generation 235.
Confirm then performed the production ordering:

`Level` → inert `Scene` → Settings/Input/SaveData reload →
`OverworldLoader` → `OuiMainMenu`.

The listener, Bonjour state, access code, and sessions were cleared before
materialization. Preparation/completion tickets matched; final old-Level draw
completed, old SaveData/file-select state was cleared, and the stale guard
cleared only after the verified main menu. The run loop did not return, the
game was not disposed, and the same single FNA/FMOD runtime retained all seven
banks. This directly revalidates the Stage 17 active-Level correction.

The downloaded Settings backup was then imported through a newly activated
manager and Confirm-soft-reloaded a second time. Generation 236 restored the
exact pre-test logical fingerprint and all three slots; the second main-menu,
Settings graph, Input graph, materialized files, runtime identity, and bank
state all verified. Controller Prompts remained Automatic/PlayStation,
Performance HUD remained Off, and Save Manager remained stopped. Managed
memory moved from about 122.4 MB after reload one to 126.5 MB after reload two,
with no duplicate runtime/listener/audio/controller evidence.

Graceful main-menu Quit/Leave, Back-to-menu, and Leave/Home/reopen passed
without `Engine.Exit`, `Game.Exit`, a second runtime, or the historical
blank/frosted shell. The first resident process recorded 37,538 updates,
36,900 draws, and 558 haptic lifecycle events by about 10m35s; subsequent
normal gameplay advanced the durable snapshot through generation 239.

One deliberately abrupt diagnostic sequence killed the foreground process by
PID and immediately requested a console launch through `devicectl`. tvOS kept
an empty retained app surface and no Celeste startup log appeared, so that
unsupported raw process-control sequence was rejected as relaunch evidence;
the product never performs it. The user then fully closed the app through the
Apple TV app switcher and launched it normally from the icon. Exactly one host
process returned to the functional menu with current Settings/saves, audio,
controller input, Automatic/PlayStation prompts, HUD Off, and dormant Save
Manager. A further uninterrupted seven-minute gameplay pass, including
volume-low, mute, restore, death/respawn, pause/resume, audio, controller, and
haptics, passed. This normal app-switcher/icon cold relaunch is the accepted
relaunch gate.

## Private cloud-builder acceptance

The tracked cloud template source pin changed only from the earlier accepted
`90ebb023f3043222bc67e72922ae4d68223f009c` to frozen product source
`b5f2ec2fdd5c65c533d86aae750ee60dcaf5009e`. The separately published public
template commit was
`612e9a75e9ce618d3657fbc93c1ae1af77230f03`. It remained public, template
enabled, five-file-only, Release-free, tag-free, cache-free, artifact-free,
and contained no proprietary input or signing material.

A manual build dispatched in the public template repository failed at its
first privacy gate in about three seconds. Checkout, input inspection/download,
cache, build, and publish were all skipped.

A genuinely new private repository was then created through the public
template API. Its initial generated commit was
`7dbc1f736931a621c8f79a464448ddf313c0378a`. Exactly one Steam Windows
Celeste archive and the accepted FMOD DMG were uploaded to the fixed private
input Release; no signing material was uploaded.

### Cache-miss run

- result/time: **PASS, 35m38s**;
- safe native cache: confirmed miss, then one entry saved;
- IPA bytes: **895,768,394**;
- IPA SHA-256:
  `6d0feefe6544ad12d0bda6776062314483435ac2c3dc28151761a443d996c9a5`;
- uncompressed app bytes: **1,217,126,200**;
- authenticated post-download Stage-8A/product verification: PASS.

### Cache-hit run

- result/time: **PASS, 16m33s**;
- restored cache independently verified before use;
- full proprietary input validation, canonical generation, full-AOT publish,
  packaging, and product verifier all still ran;
- IPA bytes: **895,768,400**;
- IPA SHA-256:
  `6f2fcae0623fdc71a9bb9bbf342b8a0c2c66ab08eaa28c347507e2318e481bf4`;
- uncompressed app bytes: **1,217,126,200**;
- authenticated post-download Stage-8A/product verification: PASS.

The single safe cache was 37,868,396 bytes and was keyed by public source,
runner/toolchain, native manifest, and the accepted native logical hash. It
contained only open-source native/toolchain outputs; no Celeste, FMOD, app,
IPA, signing, save, or session data.

The two unsigned cloud IPAs were not required to be byte-identical. They had
the same 1,288 file paths; 1,255 files were byte-identical, and the 33
same-sized differing managed/AOT build products had identical managed assembly
contracts and native symbol/architecture contracts. Both passed every product
verifier. The local itch-derived and cloud Steam-derived IPAs likewise had the
same 1,288 paths, 1,246 byte-identical members, identical managed/native public
contracts, identical canonical game/native locks, and only expected bundle
identity, host-build metadata, asset-catalog, and nondeterministic AOT/link
differences.

No Actions artifact was used. After authenticated downloads and verification,
the real cleanup workflow removed the input Release, output Release, both
fixed tags, and the safe cache. Final counts were zero Releases, zero tags,
zero caches, and zero artifacts. A second cleanup run passed idempotently with
the same zero counts. The acceptance repository remained private.

## Deterministic, repository, and documentation gates

- Stage 9B persistence/repository chain: PASS.
- Save Manager protocol/security: 66 PASS.
- Controller Prompts: 38 PASS.
- graceful Quit: 16 PASS.
- soft reload: 21 PASS.
- QR pairing: 31 PASS.
- Performance HUD: 39 PASS.
- exact Celeste inputs: 56 PASS.
- builder UX: 21 PASS.
- cloud orchestration: 32 PASS.
- Stage 19 semantic production names at the frozen product source: 215 PASS.
- Stage 20 RC2 contract: 150 PASS.
- Stage 14 product/features, repository/privacy, shell syntax, documentation
  links, builder help, host doctor, and package chains: PASS.
- Historical Stage 14 manifest and all 27 pre-Stage-20 tracked reports:
  byte-identical to the product-source commit.

Current README/Building/Cloud Building/Celeste Inputs/Status/Troubleshooting
were followed as a new user. Local and cloud paths were self-contained; the
only external values were lawful Celeste/FMOD input paths and local signing
choices. Steam/Legendary remained optional acquisition tools rather than build
dependencies. Documentation did not claim distributed game content, automatic
downloads, cloud saves, Game Mode, or cloud signing.

The repository/privacy audit found no game/FMOD content, generated/decompiled
Celeste source, saves, IPA/app bundle, provisioning profile, certificate,
signing value, device identifier, LAN address, access code, pairing/session or
CSRF secret, or private absolute path in tracked content.

## Known limitations

- Only the nine registered Celeste 1.4.0.0 FNA profiles are accepted; unknown
  versions, XNA, and Everest/mod inputs remain unsupported.
- Users must provide their own lawful Celeste files and FMOD 1.10.09 SDK.
- There is no iCloud/cloud-save sync or uninstall-survival guarantee.
- Personal Team builds do not provide Apple User Management profiles and
  expire under Apple's normal Personal Team rules.
- Cloud output is unsigned, needs a separate signing/install step, consumes
  the user's Actions allowance, and never receives signing credentials.
- App Store distribution, Apple Game Mode support, and arbitrary controller
  Automatic-family detection are not claimed.
- App-switcher termination remains the safe fallback if a future soft reload
  fails its verification.

## Recommendation

**RC2 READY.** No release-blocking defect remains within the integrated tested
scope. After the release branch is explicitly fast-forwarded into `tvos-port`,
the exact resulting accepted commit should be tagged `v1.0.0-rc.2` in a
separate authorized action. This Stage 20 run intentionally did neither.
