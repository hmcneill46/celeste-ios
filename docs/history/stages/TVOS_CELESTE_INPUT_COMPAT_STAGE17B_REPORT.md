# Stage 17B — production multi-distribution Celeste FNA input support

## Result and baseline

Stage 17B productionises strict multi-distribution input support on
`feature/celeste-input-compat`, starting from integration commit
`87dcc54316115a3fac695343761e782383a0032e`. The immutable
`v1.0.0-rc.1` tag continued to dereference to
`ee52b0868df091746f134d95d4f020f94f23d4fb`.

All eight supplied archives were safely extracted into ignored storage and
classified as Celeste 1.4.0.0 using FNA 21.3.5. None was XNA. Every compatible
profile normalizes to the one locked canonical class
`celeste-1.4.0.0-a`; unknown, mixed, modified, ambiguous, XNA, and other-version
inputs remain rejected.

## Input profiles and evidence

| Archive label | Archive SHA-256 | Profile | Adapter | Result |
| --- | --- | --- | --- | --- |
| itch.io Linux | `a613430411dbef3c0c45a0474f90f4845122964c7d177d69e88965bcb84741bf` | `itch-linux-fna-1.4.0.0` | none | Supported — physically validated |
| itch.io macOS | `924e91d2e9367ef8c2c458ff4042e6b67e8bbd17fd27f13a314fd1c48c804181` | `itch-macos-fna-1.4.0.0` | none | Supported — canonical-equivalent |
| itch.io Windows FNA | `d1072fe39c086ed1cde0b0887f68ee8032b101822ddf55e6ee806b5e736896c3` | `itch-windows-fna-1.4.0.0` | none | Supported — canonical-equivalent |
| Epic Windows FNA | `c31b3c833c1a6993f73cb82355d9b088ccafb7a9f6919b42e6922a70f85aaab5` | `epic-windows-fna-1.4.0.0` | none | Supported — physically validated |
| Epic macOS FNA | `94bc7f260ed45feb09398df3bfbdc34f2e6029fbe47cb8bcddc271dc342f2262` | `epic-macos-fna-1.4.0.0` | none | Supported — canonical-equivalent |
| Steam Linux pinned manifest `1505052356460012099` | `4406ffe632bab487fc55bce14b138af5c046655f6ffd779611367f501bf443d0` | `steam-linux-fna-1.4.0.0-manifest-1505052356460012099` | Steam exact adapter | Supported — physically validated in Stage 17A |
| Steam Linux public 2025 fixture | `3088a42d6dce73e6e057013baf27a4a8e013a10bad569580b26b6200628f0087` | `steam-linux-public-2025-fna-1.4.0.0` | Steam exact adapter | Supported — canonical-equivalent |
| Steam macOS FNA | `27467f9a2ca847d361d76a51bd8a13ccb60f9780d3b0ade3b4105b2d28c2ff9a` | `steam-macos-fna-1.4.0.0` | Steam exact adapter | Supported — canonical-equivalent |

The pinned Steam archive is a recompressed wrapper of the Stage 17A payload;
the registry also retains Stage 17A's original-container fingerprint. Archive
names were not trusted for detection.

## Equivalence and adapters

All profiles have the same **1,216-file, 1,158,665,183-byte** Content tree:

`30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46`

This includes byte-identical copies of all seven FMOD banks. The itch.io and
Epic managed executable is byte-identical (`fd73f8…`) and requires no source
adapter. Epic contains no EOS/Epic managed dependency; the Windows package's
absence of the empty legacy `Celeste.Content.dll` is an exact supported layout
property, and the existing pipeline reconstructs the identity-only modern
assembly without borrowing game files.

All three Steam profiles have the same Steam-aware `Celeste.exe`, FNA, and
`Steamworks.NET.dll`. The 2025 Linux candidate differs from the pinned 2021
candidate only in irrelevant packaged desktop FMOD native libraries; game code,
FNA, Steamworks, Content, and banks are identical. Its depot manifest ID was
not available from trusted local evidence and was not invented.

The Steam adapter is a repository-owned exact zero-fuzz source transform. It
removes Steam initialization/restart checks, callback processing, language
selection, achievements, statistics/global-stat command, and the managed
Steamworks project reference. It targets only four generated source files and
the generated project boundary. The older root `remove-steam.patch` and
`remove-steam2.patch` remain legacy iOS-lane artifacts and are not used by the
modern profile system.

## Canonical locks and build deduplication

Every archive was independently detected, normalized, regenerated, and passed
all locked transformations through Stage 16B. The results were identical:

| Boundary | Files | Logical SHA-256 |
| --- | ---: | --- |
| Decompiled canonical source | 920 | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Patched canonical source | 922 | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Stage 6 real-audio generated tree | 934 | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |

The build matrix therefore has one class. Epic Windows was selected as the
single Stage 17B expensive representative because it exercises the new Windows
wrapper discovery, missing legacy Content assembly, Epic classification, and
ICO-derived local artwork. itch Linux and Steam Linux 2021 already had physical
acceptance; the remaining profiles' byte-identical canonical outputs made
additional 854-MiB publications and device installs knowingly redundant.

The accepted native dependency graph was unchanged at
`6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`.

## Production architecture

`managed/celeste-input-profiles.json` is a closed, versioned registry. The
validator uses multiple independent signals: managed hashes and identities,
the complete Celeste reference closure, FNA identity, Content aggregate,
required package markers, absent-file constraints, and canonical source locks.
It recognizes bounded Linux/Windows roots and macOS app Resources layouts; it
does not recursively hunt for arbitrary executables.

The public builder requires no store/profile flag. It reports game version,
store, source platform, runtime, profile, and canonical class, then passes the
resolved contained root into one shared downstream pipeline. An unknown future
update prints a diagnostic fingerprint and stops; there is no force mode.

## Validation

- Eight independent safe archive extractions rejected absolute paths, parent
  traversal, unsafe links, and metadata-only archive noise.
- All eight exact validators passed.
- Linux, Windows, and macOS layouts each reached the exact Stage 6 canonical
  result; normalized tree comparisons had no differences.
- Stage 17B deterministic profile/adapter/layout policy: **46 passed**.
- Existing regressions: Stage 10 protocol **66**, Stage 11 **38**, Stage 12B
  **16**, Stage 13B **21**, Stage 15 **31**, and Stage 16B **39** passed, plus
  Stage 9B and Stage 14 repository/product verification.
- A focused Stage 16B verifier correction reads each native component's
  `logicalSha256` field from the current full manifest object; all expected
  native hashes remain unchanged and are still compared exactly.
- Epic Windows produced a Release `tvos-arm64`, fully trimmed, fully AOT,
  `UseInterpreter=false` unsigned IPA: **895,767,408 bytes**, SHA-256
  `3c78650a0a4ff7aa21e531bb99d557febe57d39b213f07fb67eae1aaba50d101`.
- The same-identity Epic Windows physical acceptance restored existing saves
  and host preferences and passed first draw, seven-bank audio, controller,
  gameplay/death/respawn, Controller Prompts, Performance HUD, QR Save Manager,
  verified soft reload, and graceful Quit.

The first focused device pass found one release-blocking integration defect
unrelated to storefront input: after Save Manager was opened over an active
`Level`, the Stage 13B update hook cleared `SaveData.Instance` before that
Level's final render. The final render dereferenced the cleared state while a
Metal command encoder was active, producing an Apple Metal assertion and
`SIGABRT`; the accepted Stage 9B mutation itself remained durable and restored
correctly after cold launch. The narrow correction schedules an inert
`Monocle.Scene`, retains all old high-level state through the old Level's final
draw, waits for the engine to detach/end that Level on the next update, and
only then reloads Settings/Input, clears stale SaveData, and schedules the
normal main menu. Deterministic ordering coverage was added. A full-AOT
same-identity retest from active gameplay recorded
`Level -> Scene -> OverworldLoader -> Overworld/OuiMainMenu`, exact generation
and logical-hash completion, one FNA/FMOD runtime, seven banks, and a cleared
stale-write guard.

Settings/SaveData serializers and Stage 9B persistence hooks are byte-identical
for the one canonical class, so saves are shared transparently across all eight
input profiles. No game file, generated source, store library, bank, or save is
tracked.

## Scope and known untested distributions

Steam Windows, Microsoft Store/Xbox App, Humble, other storefront builds, and
other Steam depots were not supplied and are not claimed supported. XNA builds
are deliberately outside this FNA pipeline. The tested public 2025 Steam Linux
fixture is supported by its exact cryptographic profile, not as a claim that
every current or future Steam build will match.
