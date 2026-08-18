# Stage 24E2 — beginner iOS self-builder and integrated release acceptance

Status: **PASS — GREEN**

The modern iPhone/iPad vanilla Celeste port is release-candidate-ready for
personal self-build. Stage 24E2 added one beginner-facing local builder,
current public documentation, explicit port/build identity, independent
unsigned and Personal Team lanes, and integrated physical acceptance on a
current iPhone and the minimum-version iPad. No GitHub Actions job was used.

## Baseline and repository policy

- Starting commit: `b669f3766c7fc569031d1f511a5be50cb32dd75a`
- Release branch: `release/ios-v0.1.1-rc.1`
- Final accepted branch: the commit containing this report; its exact SHA is
  recorded in the post-push acceptance handoff
- Immutable tvOS RC1: `ee52b0868df091746f134d95d4f020f94f23d4fb`
- Immutable tvOS RC2: `641e86e4ed164cdf93f602ce2f11436449654d6e`
- Deferred RC3 branch: `c8134c8ca7924cf12f48527e714b5242c6024927`
- No RC3 tag exists
- No merge, release tag, upstream push, GitHub Release, or GitHub Actions run
  occurred

Stage commits before this acceptance record:

- `493c870` — `build: add beginner-facing modern iOS builder`
- `65b92fb` — `docs: add modern iOS self-build and install guide`
- `ffb8cd3` — `test: make iOS verifiers clean-clone safe`
- `bd6bf07` — `fix: handle duplicate iOS device names`
- `10f77c7` — `fix: explain first-device iOS trust`
- `11c2764` — `fix: use opaque black iOS app icon`

## Product identity and public workflow

One authoritative `modern-ios/IOSPortVersion.props` supplies semantic port
version **0.1.1** and monotonic bundle build **5** to the bundle and to the
generated Options label `iOS PORT v0.1.1 · BUILD 5`. Celeste's game/content
version remains 1.4.0.0.

The obvious root entry point is `./build-ios.sh`:

- `--doctor` checks the exact host before expensive work;
- `--unsigned` produces and verifies a signing-ready unsigned IPA;
- `--signed` produces a device-provisioned Apple-development-signed IPA;
- `--install` signs, installs, and launches on a selected paired device.

The guided route remembers only ignored local choices. One device is selected
automatically; multiple devices are displayed as numbered names and OS
versions. The user never pastes a UDID. A release test with two phones having
the identical friendly name exposed a real ambiguity in `mlaunch`; the final
builder uses the selected private Xcode identifier internally, redacts it from
command logs, and does not retain an ambiguous preferred name. Curly-apostrophe
and non-UTF-8 diagnostic bytes can no longer break the bounded error summary.

The first install to a new phone also proved the distinction between trusting
the Mac/enabling Developer Mode and trusting a Personal Team app profile. The
builder and public guides now give the exact
Settings > General > VPN & Device Management remedy when iOS installs but
denies first launch. Missing accounts/certificates, unavailable devices,
provision expiry, bundle conflicts, unsupported game input, wrong FMOD,
insufficient disk, interrupted commands, and stale/partial output likewise
fail with a bounded next action. Incomplete products are never promoted.

The eight phases are semantic rather than fake percentages and long work has a
one-minute elapsed-time/free-disk heartbeat. A clean build used approximately
11 GiB beyond its final package; 8 GiB is the hard preflight floor and 15 GiB
is the early recommendation. Private full logs stay below ignored
`artifacts/ios/logs`.

## Inputs, FMOD, and fresh-clone journey

The builder retains automatic closed-registry support for all nine exact FNA
profiles: itch.io Linux/macOS/Windows, Epic macOS/Windows, and Steam Linux
2021, Linux public 2025, macOS, and the accepted Windows manifest. Modified,
mixed, ambiguous, unknown, and XNA inputs remain rejected. Every accepted
profile belongs to canonical class `celeste-1.4.0.0-a`.

Before native generation or AOT, the builder requires FMOD Engine iOS/tvOS
**1.10.09 build 97915** and validates its revision, headers, device archives,
and exact fingerprints. It does not download Celeste or FMOD.

A genuinely empty clone with recursive public submodules followed only the
public iOS guide. The documented HTTPS rewrite was added after that exercise
found the pinned FNA tree's historical nested `git://` URL could hang a modern
public clone. Host doctor, exact itch Linux input, exact FMOD, a from-scratch
native build, canonical generation, full AOT, package verification, and an
unsigned IPA completed in **22m28s** with a clean Git status. No generated or
native output was copied from the working checkout. The later duplicate-name,
trust-guidance, and opaque-icon corrections are bounded host/packaging changes;
the exact final source was then rebuilt through both complete unsigned and
signed product lanes, and its clean-clone-safe generator/verifiers are included
in the post-push clone gate.

## Exact final packages

The final committed product source produced:

| Lane | Build time | Bytes | SHA-256 |
| --- | ---: | ---: | --- |
| Development signed + installed | 9m14s | 881,452,645 | `523777f4be558de4730958238c2af93e7146a2e3b861ef66c613536607cf2a44` |
| Unsigned | 5m07s | 881,287,394 | `43ad5523a8f14ae35e280caa63e16645342a47148d6bf749227ebab674d02b1c` |

Both are Release `ios-arm64`, minimum iOS 15.0, direct FNA3D Metal, LLVM full
AOT, full trim, `UseInterpreter=false`, and JIT-free. Both contain the exact
seven FMOD banks and current game/features. ZIP integrity and the product
verifier passed. The unsigned product has neither a usable code signature nor
embedded profile. The signed product has only ordinary Personal Team
development signing; no iCloud, App Group, dynamic-code-signing/JIT, paid,
cloud, or unrelated entitlement was introduced.

The upstream icon artwork had transparent pixels. iPadOS 15 composited them
against black while iOS 26 displayed white, despite identical source. The
final build deterministically composites that artwork over an alpha-free black
sRGB master before Asset Catalog compilation. The product verifier decodes the
compiled 120-pixel iPhone and 152-pixel iPad icons and requires every pixel to
be opaque with black corners. Physical iOS 26.1 and iPadOS 15.8.8 Home Screens
now both show the intended black background.

## Canonical and native locks

| Boundary | Files | Logical SHA-256 |
| --- | ---: | --- |
| Canonical Content | 1,216 | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw source | 920 | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched source | 922 | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Shared Stage-6 real-audio tree | 934 | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| Final modern-iOS generated tree | 944 | `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357` |

- iOS native logical hash:
  `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`
- tvOS native logical hash:
  `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`

No native dependency or canonical game transformation changed in E2.

## Physical acceptance

The exact universal Build 5 app was installed as a same-identity replacement,
never by uninstalling an accepted app.

On the iPad mini 4 running iPadOS 15.8.8, Build 4 -> Build 5 preserved Settings,
all slots, previous-good backups, touch preferences, the custom Tablet layout,
Grab profiles, and Controller Prompts. Touch gameplay, death/respawn,
pause/resume, both landscape orientations, controller/prompt/audio handoff,
Data & Files export/import/restore, touch-layout export/import,
background/foreground, and cold relaunch passed. A later exact final-icon
replacement again preserved the existing state and launched normally.

An additional iPhone 12 Pro Max running iOS 26.1 provided the safe fresh-install
lane without touching the accepted primary phone app. Build 5 showed the
correct visible label and fresh defaults, with no unexpected saves/backups.
Touch-only menus/gameplay, movement, Jump, Dash, Grab, death/respawn,
pause/resume, rotation, editor move/resize, split Jump/Dash, duplicated
controls, custom-layout persistence, and cold relaunch passed. DualSense
handoff, PlayStation prompts, rumble, chapter-colour light behavior, return to
touch, title/gameplay audio, volume, background/foreground, Data & Files
save/Settings export/import, and `.celestetouch` sharing all passed. The same
final bundle was then installed over both devices; both black icons, launches,
audio/touch, and retained iPad state passed.

This provides both required paths: an existing-data Build 4 -> Build 5 upgrade
and a truly separate-device fresh install. No production save was deleted or
committed.

## Verification and inherited regressions

- Stage 24E2 workflow/source/package verifier: 90 checks with the final app
- Stage 24E1 Files portability verifier: 93 checks
- Stage 24D3 custom-layout verifier: 164 checks
- Stage 24D2 touch verifier: 153 checks
- Stage 24C2 controller/gameplay verifier: 87 checks
- Modern iOS foundation policy: 243 tests
- Modern iOS durability/fault policy: 87 tests
- Exact input profiles: 56 tests
- Shared builder UI: 21 tests, including non-UTF-8/device-ID redaction
- tvOS Save Manager protocol/security: 66 tests
- Controller Prompts: 38 tests
- graceful Quit: 16 tests
- soft reload: 21 tests
- QR pairing: 31 tests
- Performance HUD: 39 tests
- Save Manager continuity: 57 tests plus 85 current-product checks
- tvOS Stage 22B/current Stage 14/repository chain: PASS
- Shell/Python/Swift syntax, host doctor, native/package checks,
  documentation links, privacy/repository checks, and `git diff --check`: PASS

The shared UI change only hardens local diagnostic redaction. tvOS runtime,
native lock, cloud template, and product behavior are unchanged and the full
current tvOS deterministic chain remains green. Deferred RC3 is unchanged.
GitHub Actions usage for Stage 24E2 is zero.

## Documentation, privacy, and licensing

The root README now makes the Apple-platform choice clear without demoting
tvOS. `docs/IOS_BUILDING.md` gives the complete beginner journey: ownership,
nine exact profiles, exact FMOD, recursive clone, Xcode/Personal Team,
doctor, build modes, first-device trust, update-without-uninstall, Files,
touch layouts, and limitations. STATUS, BUILDING, TROUBLESHOOTING, and the docs
index point to the current route. `docs/releases/ios-v0.1.1-rc.1.md` is the
timeless release-note draft.

Tracked content contains no Celeste/FMOD payload, generated game source, IPA,
save, signing value, provisioning profile, account, device identifier, or
private acceptance path. The app still declares no tracking or collected data,
adds no camera/location/motion permission, analytics, networking, or cloud
service, and retains private Application Support authority plus user-directed
Files/Share operations.

Google Material Symbols remain attributed under Apache License 2.0. Daniel
Tacho's Grab artwork remains attributed under CC BY 3.0. Project/upstream
notices remain discoverable. No endorsement is implied.

## Current limitations and recommendation

Users must own and supply an exact supported Celeste 1.4.0.0 FNA input and
FMOD Engine iOS/tvOS 1.10.09 build 97915. The full product targets physical
arm64 iPhone/iPad; Personal Team provisioning is temporary and self-build
signing is required. There is no App Store distribution claim, automatic cloud
save, or Everest/mod support.

No further vanilla-iOS release blocker remains within the tested scope. After
manual review/integration, the recommended immutable annotated tag is:

`ios-v0.1.1-rc.1`

Everest/mod work should begin only as a separate isolated feasibility stage
that preserves full static AOT/no JIT and cannot weaken this known-good
vanilla recovery point.
