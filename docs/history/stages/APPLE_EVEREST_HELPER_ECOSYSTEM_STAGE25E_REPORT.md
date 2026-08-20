# Stage 25E — real Apple Everest helper ecosystem

Status: **PASS — GREEN**

Date: 2026-08-20

Stage 25E extends the shared iOS/iPadOS/tvOS static-AOT Everest experiment
from isolated mods to one ordinary real map-to-helper dependency graph. The
Mac builder consumes the normal public release ZIPs for Cpop Helper 1.3.0 and
QuizSample 0.0.1, resolves their declared dependency before AOT, discovers
gameplay registrations and settings from precompiled metadata, and emits one
closed, typed Apple product. Neither source tree is needed to build the final
closure, and no third-party bytes are tracked or redistributed.

This remains an internal compatibility lab. It is not general Everest or
Strawberry Jam support, and it does not enable runtime mod loading, arbitrary
reflection, `IL.*`, `ILHook`, NativeDetour, Lua, custom mod audio, or durable
`EverestModuleSaveData`.

## Git and immutable boundaries

| Item | Accepted value |
| --- | --- |
| Starting commit | `3a11b16c04ac77a73328c5cd9b4f8874f854f1e9` |
| Feature branch | `feature/apple-everest-helper-ecosystem` |
| Final acceptance commit | The commit containing this report; the final response records its exact full SHA |
| Everest stable source | `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00` |
| MonoMod source | `dfc30a1506d37fb88a2c2be004f525205f46a24c` |
| iOS recovery tag | `ios-v0.1.1-rc.1^{}` -> `27e16b4724d94d3991b99c4795f680fcb0e5830c` |
| tvOS RC1 | `v1.0.0-rc.1^{}` -> `ee52b0868df091746f134d95d4f020f94f23d4fb` |
| tvOS RC2 | `v1.0.0-rc.2^{}` -> `641e86e4ed164cdf93f602ce2f11436449654d6e` |
| Deferred RC3 branch | `origin/release/v1.0.0-rc.3` -> `c8134c8ca7924cf12f48527e714b5242c6024927` |
| RC3 tag | Absent |

No integration/release branch, recovery tag, GitHub workflow, or cloud-builder
template changed. No GitHub Actions workflow was run.

The production and evidence commits preceding this self-referential acceptance
record are:

- `01c10626ba0106073aa4a528d6698734ff473cbd` — static helper registries;
- `7d3cc82fa64ca79ab41e378181f7f9dc75fabd90` — module-settings persistence;
- `eeb3ffe15c5c5397e3953782304235b7e8d6f74b` — real helper ecosystem tests and documentation.

## Candidate audit and selection

Eleven helper candidates and thirteen dependent-map candidates were audited
from Everest's public update index, dependency graph, and custom-entity
catalog. The complete classifications and exact hashes are recorded in
[`helper-ecosystem-compatibility-stage25e.json`](../../../apple-everest/helper-ecosystem-compatibility-stage25e.json).
Candidates requiring a broader HookGen/direct-Hook/dynamic-target surface or
exceeding the bounded archive budget were deferred rather than partially
accepted.

The selected pair was intentionally small, source-auditable, and rich enough
to prove the missing architecture:

| Fixture | Exact provenance | License finding | Role |
| --- | --- | --- | --- |
| Cpop Helper 1.3.0 | Release ZIP `7a807a8f9ce6ccb4d6ad0c664bb7791a60734533fb33cfcd6beccb202d846b63`; DLL `235d767a0f82f45aef7b01e4ef662934af449ee416deb6073a1eaca3263c75c9`; frozen DLL `9242842076d0992e3498a7b6efb3d48ab8e9fc09ea9ba07819bbc9f716e64845`; source audit `f8b70384d195a180b5c3e6e3a41f9d65075abcb2` | No SPDX file; upstream README grants reuse with credit and at own risk. No bytes are tracked/redistributed. | Real precompiled helper with entities, triggers, content, module/session state, and no detours |
| QuizSample 0.0.1 | Release ZIP `5cb8351bb263aa316831d2b683cb8b04acd270587edfe7c9e3df3bb8e6b83b3e` | No source repository or explicit redistribution license located. No bytes are tracked/redistributed. | Four-room content-only map declaring Cpop Helper >= 1.0.0 |

The resolved graph is:

`Everest -> CpopHelper -> QuizSample`

The full six-package regression closure is ordered deterministically as Cpop
Helper, Feather Maddy, I Accidentally Four Cassette Blocks, Lag Pauser,
Particle Palette Helper, then QuizSample. Omitting Cpop Helper, using a wrong
version, mixing owners, or creating an ambiguous/duplicate registration fails
on the Mac before AOT. Required helpers cannot be disabled while a dependent
map is present. Final generation was repeated from freshly downloaded,
hash-verified public release ZIPs and produced byte-identical manifests and
logical hashes without either helper/map source tree.

## Static gameplay registration

The host reads pinned Everest `CustomEntity` and `CustomBackdrop` annotations
from ordinary DLL metadata using Mono.Cecil. It validates each ID, owner,
declaration, constructor/factory shape, and map reference, then emits a direct
typed switch plus explicit trimmer/AOT roots. Supported shapes include the
normal entity/trigger `(EntityData, Vector2)` constructor and reviewed
`(EntityData, Vector2, EntityID)`/static-factory variants. Device code does not
call `Assembly.GetTypes`, `Activator.CreateInstance`, `DynamicInvoke`, or a
runtime assembly loader.

Cpop Helper contributes these exact IDs:

- entities: `HDGraphic`, `TheoJelly`, `cpopBlock`, `quizController`;
- triggers: `checkSubpixelTrigger`, `quizAnswerTrigger`,
  `setSubpixelTrigger`.

QuizSample independently resolves `quizController` and
`quizAnswerTrigger`. Its pinned `everest/coreMessage` reference uses one
separate reviewed Everest-core factory. The accepted registry contains eight
factories: seven from Cpop plus that core factory. Custom triggers are also
registered under the canonical `Trigger` tracker bucket through generic
inheritance-aware tracker generation; this fixed the first prototype where
the entity existed but its collision callback was never found.

The selected pair contains no custom backdrop. The pinned Everest
`ID[=factory]` convention, duplicate handling, typed factory emission, and
runtime dispatch are covered by a deterministic generated backdrop canary.
This is reported honestly as static coverage, not a physically observed real
helper backdrop.

Thirty-eight Gameplay and one Gui atlas entries from the real helper are
mounted into the normal Celeste atlases before module content callbacks. The
stable entity/update/render path is ordinary compiled code and performs no
per-frame reflection or registry scan.

## Real map behavior and fixture limitations

The four QuizSample rooms loaded through normal Celeste/Everest map and
entity construction. Wrong-answer trigger collision kills Madeline and uses
normal death/respawn; the correct-answer trigger produces confetti. Text,
Image, and HighResImage answer modes intentionally have different visual
styles, and the quiz seed includes the session death count, so values reroll
after a death.

The distributed `Dialog/English.txt` defines answer digits but omits
`PickTheEvenNumber`, `IsThisNumberEven`, `Yes`, and `No`, although the map's
`everest/coreMessage` entities reference them. Celeste consequently displays
its normal `XXX` missing-dialog fallback. Logs and screenshots proved that the
dialog fragment and core entity were loaded; the builder deliberately does
not invent missing copyrighted fixture text. This is an upstream sample-map
limitation, not a static-AOT loading failure.

## Mod Options and settings persistence

The same Cecil pass discovers public `EverestModuleSettings` properties and
emits typed descriptors and ordinary Celeste `TextMenu` entries under **Mod
Options**. Supported bounded shapes are:

- `bool`;
- enum with exact declared names/values;
- `int` carrying a small explicit `SettingRange`.

Unsupported or unbounded properties are recorded as omissions; no reflection
or guessed editor is used on device. The real closure exposes four booleans:
Feather Maddy's Dark Rooms, Feather Fly, and Player Effects, plus Lag Pauser's
Enable. Cpop's settings class is intentionally empty.

One platform-neutral versioned text codec has a strict 16 KiB maximum,
invariant formatting, exact module/property identity, duplicate rejection,
and type/range validation. Missing data uses module defaults. Malformed,
truncated, wrong-type, duplicate, unknown, or oversized data is discarded in
favor of defaults without touching vanilla state.

The complete four-setting physical document is 154 UTF-8 bytes. The 16 KiB
whole-document ceiling, bounded module/property tokens, and closed generated
descriptor set provide a stricter total budget than separate per-mod files
would for this static closure.

Storage is deliberately platform-specific only at the authority boundary:

- iOS/iPadOS atomically stores the logical document in private Application
  Support at `AppleEverest/ModuleSettings.v1`;
- tvOS stores the same bounded logical document in the separate app-private
  `CelesteAppleEverest.Settings.v1` UserDefaults key.

Neither authority is `settings.celeste`, SaveData, the tvOS compressed A/B
generation, Save Manager, or live Documents. Changing module options does not
advance or replace vanilla persistence. Disabling an optional module removes
its owned hooks/registrations while retaining its settings; Cpop cannot be
disabled in this closure because QuizSample requires it.

The first physical tvOS candidate exposed a compile-boundary defect: generated
Celeste defines `TVOS`, while the new shared file had tested the host-only
`TVOS_CELESTE_RUNTIME_HOST` symbol and therefore selected the iOS file branch.
The accepted implementation tests the actual generated `TVOS` symbol and
explicitly synchronizes the app-private default, matching the established
Controller Prompts and Performance HUD preference policy. A source test locks
that exact distinction.

Physical cold launches proved real option persistence on iPhone, iPadOS, and
tvOS. In particular, independent Feather Maddy and Lag Pauser module/setting
values survived app-switcher termination and replacement installation.

## SaveData and Session boundary

Durable `EverestModuleSaveData` remains deferred and is not claimed. Cpop's
module has no module SaveData. Its `EverestModuleSession` is created normally
and lasts for the current process/session only.

Static-map launches use a bounded non-persistent debug session with isolated
temporary `SaveData`; vanilla file writes and Save and Quit are suppressed for
that session. Return to Map disposes the old Level, restores the prior
SaveData/area authority, and enters the normal main menu through a null-safe
high-level boundary. It never writes a debug slot over the user's vanilla
data. This fixed the first prototype's AreaQuit/null-save failure without
claiming custom-map progression durability.

## Reviewed API and mechanism result

Cpop requires four members that pinned desktop Everest publicizes:

- `Actor.movementCounter`;
- `Glider.destroyed`;
- `Glider.sprite`;
- `Glider.DestroyAnimationRoutine`.

They extend the existing three-member Stage 25D surface to seven exact
members, hash
`f69855941b60298ce090315c81c716a01d74dd8359672dbc1e5d7e461a8f232e`.
`TagsExt.SubHUD` is a narrow shared facade backed by Celeste's existing HUD
tag. There is no broad publicizer.

The selected helper adds zero `On.*` targets and zero direct Hook plans. The
Stage 25D 18-target catalog and Lag Pauser direct-Hook proof remain unchanged.
Cpop/QuizSample require no `IL.*`, `ILHook`, ModInterop, Lua, native library,
custom audio, or runtime content watcher.

## Reproducible shared closure

| Boundary | Count / SHA-256 |
| --- | --- |
| Managed closure | 19 files; `4e3344307ede719033c56771d00f4ea1f07301bf2c70aa28bd74b5423ee421e5` |
| Content closure | 47 files; `1ebc6de7e1bb2c5de5bfe8a64dd5aedf761b9711b3a148d423408a37381fa31d` |
| Module registry | `ec59b5d7d384b4b0f9fa9ab9813fa8dd575ec8958995c233b558105215bf834f` |
| Gameplay registry | `680564bcfb179281a75f2b3fd5e805653b357985e9d00d1d49ea6282eb3dfb0e` |
| Complete shared closure | `d685d6277588b822831d911ff4f674036068d215dd7027bf6cfca5f8f43cbe82` |

Exactly one closure feeds iOS/iPadOS and tvOS. There is no platform- or
mod-name-specific source patch after this boundary.

## Package, AOT, and performance evidence

The final clean unsigned sequential iOS/tvOS build completed in 531.15
seconds. The final development-signed sequential build completed in 515.66
seconds. Both builds regenerated the complete product from the same locked
closure rather than reusing a packaged app.

| Product | IPA bytes | SHA-256 | Contract |
| --- | ---: | --- | --- |
| iOS/iPadOS unsigned | 882,501,625 | `b9d62d8dd220aad64905f0f050b19249827dbf3d74655360e095b7bffae17f20` | arm64; iOS 15.0; Release; full trim/AOT; no interpreter/JIT |
| tvOS unsigned | 896,416,116 | `a4eafaee05983ac5a8c8f235a46bc1c28a02d01d68650ca412d7e2e849be679a` | arm64; tvOS 16.0; Release; full trim/AOT; no interpreter/JIT |
| iOS/iPadOS signed physical | 882,673,720 | `8ac3f0cd16e6026c2849811610529113b11a4cdb050b36bb73584608954e6eb9` | same closure and AOT contract |
| tvOS signed physical | 896,813,667 | `1e29395e7f61aa22a37df599af920cdb4b2d7c7c83e8b1bb9b9c760cab9a13c5` | same closure and AOT contract |

All four external managed modules are linked, explicitly rooted, and carry
matching arm64 AOT data. The verifier checks every external method against its
LLVM AOT and companion Mono object. The final package run passed 190 Stage 25E
checks. Post-link/package scans found no device HookGen backend, RuntimeDetour
implementation, native/IL detour, Reflection.Emit, dynamic method, assembly
loader, Lua runtime, process launcher, or file watcher.

No material frame, input, audio, memory, or load-time regression was observed.
Factories and settings descriptors are static arrays/switches; settings write
only when changed; gameplay does no new registry scan or reflection. The
visible Lag Pauser threshold behavior remains the accepted third-party mod
policy rather than a renderer stall.

One distributed Feather Maddy DLL was compiled with its debug logging request
and attempted to emit a player position every render. The accepted production
log policy now clamps third-party requests to an `Info` minimum while retaining
Everest's longest-prefix rules. A 48th semantics check locks the policy, and
the final physical console contains no per-frame verbose stream.

## Physical acceptance

### iPhone 12 Pro Max

The final signed same-identity build restored existing canary state and
reached the main menu with four modules, 47 content mounts, and all expected
atlases/dialog fragments. Touch, physical controller, audio, module options,
QuizSample wrong-answer death/correct-answer confetti, pause, Return to Map,
and lifecycle passed. Independent Feather Maddy/Lag Pauser states and the
four supported settings survived app-switcher termination. Return to Map
restored a functional main menu with normal controls/audio and no managed
fatal. The user reported the complete final pass succeeded.

### iPadOS 15.8.8

The final universal signed build was installed as a same-identity replacement
through Xcode's legacy-device path. The old resident process was closed before
relaunch. Landscape, touch, controller, audio, persisted Mod Options,
QuizSample wrong-answer death/correct-answer confetti, Return to Map, rotation,
and background/foreground were physically exercised. The user reported the
complete final pass succeeded.

### Apple TV

The final signed tvOS build was installed as a same-identity replacement.
Controller/audio, the Cpop/QuizSample graph, trigger behavior, Return to Map,
Home/reopen, and one-runtime lifecycle passed. Feather Maddy Dark Rooms was
changed, the process was closed through the app switcher, and the final build
restored the changed value after a cold launch through the corrected bounded
tvOS UserDefaults authority. The user reported the complete final pass
succeeded.

## Regression, privacy, and locks

Deterministic acceptance includes 144 builder checks, 48 shared hook-semantics
checks, 148 source/product checks (190 with both final packages), the pinned
real MonoMod IL-freeze reference, and desktop HookGen/direct Hook references.
Current iOS Stage 24C2 (84) and 24D2 (153) behavior suites,
tvOS Stage 9B through 16B behavior suites, and current Stage 22B continuity
(85 checks, including 57 browser/protocol cases) pass. Stage 25C/25D
applicable source/closure regressions remain represented in the Stage 25E
verifier.

Historical verifiers which deliberately lock an older product-source hash,
generated byte layout, or pre-Stage-21 README placement are preserved rather
than weakened. Current-tree Stage 24D3/E1/E2 wrappers correctly reject later
tvOS/native source additions, so their unmodified accepted commits were also
verified directly: D3 passed 161 checks, E1 passed 90, and E2 passed 89. Stage
17C's current-tree run reaches all behavior suites and then stops only because
its historical README grep expects the old Steam command location. The current
Stage 25E/current-product verifiers own the new invariant.

Accepted locks remain:

| Boundary | SHA-256 |
| --- | --- |
| Canonical Content | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw source | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched source | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Stage 6 real-audio source | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| Vanilla iOS generated tree | `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357` |
| iOS native | `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2` |
| tvOS native | `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc` |

Repository/privacy verification found no tracked game/mod/FMOD bytes,
generated game source, saves, IPA/app, signing material, device identity,
private path, LAN secret, or credential. No analytics, telemetry, downloader,
cloud behavior, permission, native dependency, or GitHub workflow was added.

## Limitations and exact next stage

The supported helper surface is deliberately bounded: exact dependency
metadata, static entity/trigger/backdrop factories, the four settings shapes
represented above, process-lifetime module session state, and the existing
Stage 25D detour catalog. Custom-map progression and
`EverestModuleSaveData` are not durable. The public QuizSample has missing
dialog keys. General ModInterop, broader content APIs, IL manipulation,
custom audio, Lua/native helpers, and large multi-helper graphs remain out of
scope.

The highest-payoff next class is a Stage 25F-A durability rung: productionise
bounded `EverestModuleSaveData` and module-session save/restore semantics with
one small real fixture, without weakening vanilla persistence. No manual
download should be necessary if that stage selects and pins a publicly
fetchable ordinary release ZIP as Stage 25E did. After that, common ModInterop
and one modest multi-helper graph are rational before attempting an IL-heavy
helper. At least three major compatibility classes remain before an IL-heavy
fixture is sensible, roughly four before a modest collaboration is a useful
product test, and many more—including deterministic `IL.*`, broader helpers,
custom audio/content, Lua/native policy, and large dependency graphs—before
Strawberry Jam is a rational experiment.
