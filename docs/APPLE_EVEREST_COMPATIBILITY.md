# Apple Everest real-mod compatibility

This is a technical test matrix for the experimental shared Apple static-AOT
builder. It lists only exact public inputs inspected through Stage 25H-D. It is not
a promise that similarly named, newer, older, or dependent mods work. The
normal iOS and tvOS products do not contain these mods.

Status vocabulary:

- **SUPPORTED** — content enters the static closure without managed code.
- **SUPPORTED_WITH_STATIC_TRANSFORM** — the distributed DLL is transformed on
  the Mac and linked/AOT-compiled; no source is needed.
- **DEFERRED_…** — understood but needs another bounded compatibility class.
- **UNSUPPORTED_…** — conflicts with the current Apple runtime policy.
- **REJECTED_PACKAGE** — not a complete root Everest package as supplied.

## Selected production evidence

| Mod | Exact input | Class | Result | Platforms | Requirement | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| [I Accidentally Four Cassette Blocks](https://gamebanana.com/mods/150469) | 1.0.0; ZIP `37eaa16b…7c95` | Content-only | **SUPPORTED** | iOS, iPadOS, tvOS | Everest ≥ 1.519.0 | Ordinary map, dialog, and GUI asset; no DLL. No redistribution license was located, so the ZIP remains ignored and is never rebundled in Git. |
| [Particle Palette Helper](https://github.com/KnowHT1515/ParticlePaletteHelper/releases/tag/v1.0.0) | 1.0.0; ZIP `f9cf8874…be19`; source `9a791bcb…bb82` | Precompiled module + six `On.*` targets | **SUPPORTED_WITH_STATIC_TRANSFORM** | iOS, iPadOS, tvOS | Everest ≥ 1.6418.0 | MIT. Ordinary DLL, no source required. Assembly identity is retained. Current proof covers lifecycle, hook add/remove, cached typed `orig` pass-through, trim and AOT. Palette YAML deserialization/content enumeration remains deferred. |
| [Feather Maddy](https://gamebanana.com/mods/467551) | 1.3; ZIP `a8f11047…e40e`; source `d9d339c0…fd64` | Precompiled module + nine newly catalogued `On.*` targets | **SUPPORTED_WITH_STATIC_TRANSFORM** | iOS, iPadOS, tvOS | Everest ≥ 1.3471.0 | Ordinary DLL, no source required. This is the former deferred HookGen fixture and proves generated target expansion, lifecycle cleanup, argument/return typing, and gameplay-hook pass-through. No explicit redistribution license was located; bytes remain ignored. |
| [Lag Pauser](https://gamebanana.com/mods/591485) | 1.3.0; ZIP `32dac84d…9d10`; source `ec217fd9…e7` | `On.Monocle.Engine.Update` + direct static `Hook` on `Player.orig_Die` | **SUPPORTED_WITH_STATIC_TRANSFORM** | iOS, iPadOS, tvOS | EverestCore ≥ 1.4673.0 | Ordinary DLL, no source required. The Mac resolves its fixed reflection pattern into one static plan and exposes only its three reviewed Everest-patched `Level` API members. Device behavior uses the shared typed chain and explicit `Dispose` lifetime; there is no runtime target reflection, publicizer, or patching. No explicit redistribution license was located; bytes remain ignored. |
| [Cpop Helper](https://gamebanana.com/mods/434438) | 1.3.0; ZIP `7a807a8f…b63`; DLL `235d767a…5c9`; source `f8b70384…cb2` | Precompiled helper; four custom entities and three custom triggers | **SUPPORTED_WITH_STATIC_TRANSFORM** | iOS, iPadOS, tvOS | Everest ≥ 1.3471.0 | Ordinary DLL, no source required. Static metadata discovery emits seven direct factories. The README grants reuse with credit and at own risk but no SPDX license file was found; the ZIP, DLL, content, and source remain ignored and are not redistributed. |
| QuizSample | 0.0.1; ZIP `5cb8351b…b3e` | Content-only map depending on Cpop Helper ≥ 1.0.0 | **SUPPORTED** | iOS, iPadOS, tvOS | Everest ≥ 1.3761.0; Cpop Helper ≥ 1.0.0 | Real four-room quiz map. Normal map loading instantiates `quizController` and `quizAnswerTrigger`; wrong answers kill/respawn and the correct trigger produces the configured outcome. Its Text/Image/HighResImage number styles intentionally differ. The release omits four dialog keys used by its question labels, so those labels use Celeste's `XXX` missing-dialog fallback; answer values intentionally reroll after death. No source repository or explicit redistribution license was located, so its bytes remain ignored and are not redistributed. |
| [DeathMarkers](https://gamebanana.com/mods/53649) | 2.0.0; ZIP `94ad7d14…e6fc7`; DLL `620e5b63…ff8`; source `24c9b821…965` | Precompiled module + `Player.Die` HookGen target + default YAML SaveData/Session | **SUPPORTED_WITH_STATIC_TRANSFORM** | iOS, iPadOS, tvOS | EverestCore ≥ 1.5577.0 | MIT. Ordinary DLL is authoritative and source is not needed. Stage 25F-A proves per-slot persistent `Dictionary<string,List<Death>>`, continuation `List<Death>`, nested record/`Vector2` serialization, cold restore, new-session reset, deletion/replacement isolation, and bounded A/B recovery. |
| [Dash Toggle Helper](https://github.com/kyfex-uwu/DashToggleHelper) | 1.1.0; ZIP `677e8fbd…d523`; DLL `531eaa8a…a083`; source `9b140684…530c`; MIT | Two exact HookGen `IL.*` manipulators plus six ordinary `On.*` targets | **SUPPORTED_WITH_STATIC_TRANSFORM** | iOS, iPadOS, tvOS | EverestCore ≥ 1.5421.0 | The pinned MonoMod manipulator methods run in an isolated Mac build process against two exact Celeste target-body fingerprints. The deterministic final bodies are frozen before full AOT; the device contains no Cecil, `MonoMod.Cil`, `ILHook`, dynamic method, or runtime code-patching backend. The module is immutable-active for the installed build. |
| [Disposable Theo](https://gamebanana.com/mods/53752) | 1.0.6; ZIP `df291c01…b5b5`; DLL `1d47c082…52dcc` | Two instance-owned HookGen `IL.*` manipulators, two ordinary `On.*` hooks, and two compiler-singleton noncapturing delegates | **SUPPORTED_WITH_STATIC_TRANSFORM** | iOS, iPadOS, tvOS | Everest ≥ 1.0.0 | The source-free distributed DLL proves generic lowering of compiler-generated `<>c` singleton delegates which consume runtime stack values. The Mac freezes both exact target bodies and roots the ordinary singleton methods/fields; no host closure object or dynamic-reference cell enters the app. No license declaration was located, so external bytes are not redistributed. |
| [CaeruleaHelper](https://github.com/azure-bluet/CaeruleaHelper) | 1.11.1; ZIP `6a064951…3807`; DLL `3c5b79a5…973a`; source `036bc9ad…4d07`; MIT | One direct `ILHook` plus eight ordinary HookGen `IL.*` targets | **SUPPORTED_WITH_STATIC_TRANSFORM** | iOS, iPadOS, tvOS | EverestCore ≥ 1.5577.0 | The direct construction fits `STATIC_DIRECT_ILHOOK_FREEZE`: exact static `DashCoroutine.MoveNext` target, static manipulator, no `DetourConfig`, immediate apply, and module-lifetime storage with teardown-only disposal. The real No Dash Speed Reset behavior is frozen source-free; the constructor, field, disposal, Cecil, `MonoMod.Cil`, and runtime ILHook backend are absent on device. This exact profile is immutable-active. |

## Bounded build-time frozen IL

Stage 25H-A introduced one deliberately narrow class named
`STATIC_IL_EVENT_FREEZE`: one exact, statically registered HookGen `IL.*`
manipulator per exact target method. The ordinary hash-pinned public release
DLL remains authoritative. On the Mac, the builder verifies the original
target's normalized IL hash, invokes the real distributed manipulator with the
pinned MonoMod/Cecil implementation, validates the resulting branches,
exception regions and references, and locks the normalized after-body and diff
hashes. The result is compiled by the normal full-AOT pipeline.

Dash Toggle Helper 1.1.0 is the first production fixture. Its
`CreateSpritesOverride` and `AddSpriteOverride` manipulators freeze the custom
spinner image/colour behavior into `CrystalStaticSpinner.CreateSprites` and
`AddSprite`. Its noncapturing `EmitDelegate` calls resolve to ordinary direct
static mod-method calls in final IL; no `DynamicReferenceManager` cell survives.
The same-target `On.CrystalStaticSpinner.CreateSprites` wrapper calls the
already IL-modified original body, matching the pinned desktop ordering.

Stage 25H-B adds `STATIC_IL_EVENT_SEQUENCE`. For two or more exact ordinary,
unconfigured registrations on one target, the worker always starts with one
canonical baseline and applies manipulators in resolved module/load order and
then source registration order. Manipulator B sees A's output. Each
intermediate body is structurally validated and hash-locked; the final body is
then compiled normally. Project-owned A→B, B→A, and A→B→C cases match pinned
MonoMod and make ordering observable. No suitable public high-value fixture
with two real manipulators on the same target was located, so real shared-target
composition remains a clearly labelled evidence gap rather than a broad claim.

H-B also generically lowers compiler-generated noncapturing `<>c` singleton
delegates. It spills runtime arguments to typed locals, loads the exact static
singleton field, reloads the arguments, and calls the rooted instance method.
Disposable Theo 1.0.6 proves this with two real `Func`-style paths. Primitive
captured constants, module/runtime captures, mutable/transient closures, direct
or configured `ILHook`, runtime targets, runtime unapply, and live module
disable remain deferred. A frozen-IL module is immutable-active until process
termination; excluding it from a new build restores the exact baseline method.
Any target, registration, ordinal, intermediate/final hash, or reference
mismatch fails before linker/AOT. The exact H-B census is in
[`apple-everest/il-compatibility-audit-stage25hb.json`](../apple-everest/il-compatibility-audit-stage25hb.json).

Stage 25H-D adds `STATIC_DIRECT_ILHOOK_FREEZE`, but only for a construction
whose target and manipulator are exact and static, whose `DetourConfig` is
absent, and whose effective installed-build lifetime is immutable-active. The
same pinned worker executes the real distributed manipulator on the Mac; the
device receives only the frozen target and statically rooted introduced calls.
CaeruleaHelper 1.11.1 is the first accepted real release.

This is not general direct-`ILHook` support. The following remain explicit:

- `DEFERRED_CONFIGURED_ILHOOK` for priority/subpriority/ID/Before/After or
  ambient `DetourContext` semantics;
- `DEFERRED_DYNAMIC_DIRECT_ILHOOK_LIFETIME` for gameplay-scoped construction,
  `Apply`, `Undo`, or `Dispose`;
- deferred dynamic targets/manipulators and multiple direct hooks on one
  target unless their exact ordering is separately proven.

The exact Stage 25H-D census classified all 53 high-priority direct
constructions: 12 fit the static/unconfigured/immutable primary class, 32 were
configured, three had gameplay-dynamic lifetime as a secondary property, 12
had dynamic targets, zero had dynamic manipulators, and 13 participated in
five same-target groups. QLetterAurora is materially advanced by its one
CaeruleaHelper site, but still has 42 other direct sites plus configured and
ordinary breadth; no complete real map is newly unlocked.

Stage 25K-D adds the narrower `STATIC_CONFIGURED_DETOUR_SEQUENCE` class.
The Mac resolves immutable target-local `DetourConfig`/`DetourContext` graphs
with repository-pinned MonoMod, including priority, `Before`, `After`, legacy
wildcards, equal-order ties and the reversed IL-composition convention. The
resolved ordinals and plan hash enter the target-neutral closure; the Apple
runtime receives typed chains only, never RuntimeDetour, a live ILHook backend,
or a serialized configuration graph. The exact Strawberry Jam audit census is
30 statically eligible construction sites and nine rejected dynamic-lifetime
Extended Variant Mode sites. LunaticHelper 1.1.1 is the ordinary distributed
real fixture. This does not make Strawberry Jam or dynamic configuration
supported.

The same stage accepts one valid Celeste BinaryPacker root followed by at most
2 MiB of opaque map appendix bytes. The complete original map remains
unchanged and retains its full-file source/progression hash; malformed or
truncated primary roots still reject. It also adds 103 exact managed-detour
descriptors and 29 reviewed API members for the eight providers reachable from
the audited Beginner slice. That breadth is slice-bounded and is not a broad
helper publicizer or a whole-package compatibility claim.

## LittleEpic graph completion status

[LittleEpic's Precision Challenge](https://gamebanana.com/mods/530248) 1.0.0
really uses both of its direct code helpers: room 1 contains
`DJMapHelper/maxDashesTrigger`, and room 5 contains
`ChronoHelper/CustomTimeSwitchGates`. Stage 25I-B retains DJMapHelper 1.13.4's
five source-frozen ordinary `IL.*` registrations and exact HookGen/static
factory surface, then adds the bounded `STATIC_CUSTOM_FMOD_BANK` class required
by ChronoHelper 1.3.3. Its exact `ExpertContestHelper.bank` and GUID table are
validated on the Mac, staged once, and loaded through the existing Celeste
Studio system. The two custom events resolve through the normal audio path; no
runtime discovery, second audio system, new native library, or FMOD upgrade is
introduced.

The exact LittleEpic graph is therefore a supported static Apple Everest
closure, not a claim of arbitrary ChronoHelper, DJMapHelper, or custom FMOD bank
compatibility. Different package hashes, unknown GUID tables, other bank
classes, or different helper versions remain rejected. Custom-map LevelSet
progression is still the nonpersistent acceptance lane, so Save and Quit stays
disabled there until a later progression stage.

## Multi-helper map graph audit

Stage 25G screened 53 real map packages, deeply audited 24 plausible composed
graphs, and stopped at the pre-AOT gate. The strongest graph was
[Noctambule](https://gamebanana.com/mods/561341) 0.0.1 with two direct,
map-used code helpers:

| Root/helper | Exact input | Actual map use | Result |
| --- | --- | --- | --- |
| Noctambule | ZIP `cc48ce48…a4d6`; map `97d0937e…22e1` | Ordinary map data contains both helper-owned triggers below. | **DEFERRED_ACTIVE_LUA** |
| LuaCutscenes 0.2.13 | ZIP `c57913d1…aac8`; DLL `dc697f1a…b57`; MIT | `luaCutscenes/luaCutsceneTrigger` executes the packaged completion cutscene. | **UNSUPPORTED_LUA** — NLua/KeraLua are deliberately absent from the installed full-AOT closure. |
| ShroomHelper 1.2.10 | ZIP `6a2c3eac…97d7`; DLL `2428be46…fa1d`; MIT | `ShroomHelper/GradualChangeColorGradeTrigger` changes the map color grade. | **DEFERRED_ON_HOOK** — no active IL in this release, but DynamicData and five uncatalogued `On.*` targets remain. |

Noctambule has no active `IL.*`, `ILHook`, native/P/Invoke payload, or custom
FMOD bank, and neither declared helper is unused. Its Lua trigger is real
gameplay, however, so removing it or source-editing the helper would not be an
honest compatibility result. No source-free closure or Apple product was built.
This YELLOW audit adds no accepted map/helper profile and does not generalize
support to other packages using either helper. Exact evidence and rejection
statistics are in
[`apple-everest/multi-helper-candidate-audit-stage25g.json`](../apple-everest/multi-helper-candidate-audit-stage25g.json).

## Bounded static MonoMod ModInterop

Stage 25F-B adds a binary-first, generated implementation of the common pinned
MonoMod `typeof(T).ModInterop()` contract. The Mac host resolves each literal
registration type with Cecil, inventories public static exports and public
static delegate import fields, applies `ModExportName`/`ModImportName`, and
emits a typed plan. The device registers known type IDs and assigns ordinary
delegates; it does not discover methods/fields or call the reflection-heavy
desktop manager.

The exact selected proof pair is:

| Mod | Exact input | Static ModInterop result | Overall result |
| --- | --- | --- | --- |
| [ConditionHelper](https://github.com/Brokemia/ConditionHelper) | 1.0.0; ZIP `cefd1f82…e8ed`; DLL `f4471853…2b9`; source `1d42c6a7…9dd`; MIT | Four exports found: `ConditionChanged`, `WatchConditions`, `RemoveCallback`, and `EvaluateConditionExpression`, each under qualified and unqualified names; all 29 referenced HookGen events are in the reviewed static catalog. | **SUPPORTED_WITH_STATIC_TRANSFORM** — ordinary release DLL, no source tree or replacement provider code. |
| [AchievementHelper](https://github.com/Brokemia/AchievementHelper) | 1.0.5; ZIP `155b2ff9…8177`; DLL `5dd3cead…a5b0`; source `6f8bb780…483b`; MIT | Four qualified imports bind exactly to ConditionHelper; its own three exports and all five referenced HookGen events are generated statically. | **SUPPORTED_WITH_STATIC_TRANSFORM** — ordinary release DLL plus project-owned data-only achievement definition; no replacement consumer code. |

The ordinary distributed DLLs produce three registrations, seven exports and
four resolved imports with plan SHA-256
`9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318`.
Stage 25F-B2 closes the historical YELLOW rung without changing that plan. A
complete binary census found 29 ConditionHelper and five AchievementHelper
HookGen events: one was already catalogued and exactly 33 descriptors were
added, taking the reviewed catalog from 18 to 51 targets. The added signature
classes include an eight-argument static method, two `IEnumerator` returns,
two reference returns, exact overloads, and distinct inherited
`Entity`/`Player` lifecycle targets.

A project-owned data-only achievement watches the real condition
`totalDeaths() > 0`. A normal death runs the newly catalogued
`SaveData.AddDeath` hook, ConditionHelper reevaluates the watch, and the
ordinary AchievementHelper consumer displays **First Apple Death**. That path
was physically accepted on iPhone, iPadOS 15.8.8, and Apple TV. Its module
SaveData persists after normal Save and Quit and suppresses a duplicate award
after cold relaunch. As with ordinary Celeste state, externally terminating
the process before a requested save is complete does not promise to preserve
the newest unsaved award.

Supported plan semantics include provider-first and consumer-first load order,
late refresh, idempotent type registration, default assembly prefixes,
type/field naming attributes, qualified and unqualified names, overload and
signature selection, ordinary `Action`/`Func`, custom delegates, `ref`/`out`,
and closed generic delegate types. An absent optional provider is recorded as
`OPTIONAL_INTEROP_PROVIDER_ABSENT` and leaves its field null.

Runtime-selected registration types (`DEFERRED_DYNAMIC_MODINTEROP_TYPE`), open
generic surfaces (`DEFERRED_OPEN_GENERIC_MODINTEROP_…`), readonly import fields,
nonreproducible bindings, and unsupported delegate shapes fail before AOT as
explicit `DEFERRED_…` classes. Static ModInterop registration is
process-local and has no Apple-specific unregister or persisted state.

The complete 18-release audit and exact selected binary/source pins are in
[`apple-everest/modinterop-audit-stage25fb.json`](../apple-everest/modinterop-audit-stage25fb.json).

The Stage 25F-B2 selected regression closure uses Everest stable 1.6458.0 and
its one shared iOS/tvOS hash is
`d55f262c376c944a81cf5d253f84dc97592da41ea6e67115ccb33ad6e6b5fabe`.

The Cpop/QuizSample pair is the first supported real helper ecosystem. Its
declared dependency resolves before AOT, and omitting Cpop or supplying a
version below 1.0.0 fails early. Generated entity/trigger factories contain
direct type and constructor references; no device reflection scan or dynamic
activation is used. The same closure also exposes bounded Mod Options for real
Feather Maddy and Lag Pauser settings. Supported automatic shapes are boolean,
enum, and explicitly ranged integer. Their shared logical settings format is
stored privately in Application Support on iOS/iPadOS and under
`CelesteAppleEverest.Settings.v1` on tvOS. Module settings remain global and
separate from the numbered-slot durability described below.

## Bounded module SaveData and Session

Stage 25F-A supports only the exact default async YAML class selected by the
Mac analyser. SaveData and Session root types, constructors, property graphs,
typed codecs, trimmer roots, and AOT roots are generated before compilation;
there is no runtime assembly/type scan. Current supported property shapes are
primitive and enum values, nullable values, arrays, lists, string-keyed
dictionaries, nested classes/records, and `Microsoft.Xna.Framework.Vector2`.

DeathMarkers 2.0.0 is the positive real distributed fixture. Its SaveData is
`Dictionary<string,List<DeathMarkersSession.Death>> Deaths`; its Session is
`List<Death>`, and each nested record contains `Room`, `Position`, and `Amount`.
Both use pinned Everest's default YAML behavior with `SaveDataAsync=true` and
no custom/legacy override. Deterministic desktop interoperability verifies
pinned YamlDotNet can read Apple-emitted logical bytes and the bounded Apple
reader can read pinned desktop YAML with equivalent typed state.

The distributed Mode setter expects a per-area SaveData bucket to exist when it
is changed inside a live Level. The static adapter establishes that empty bucket
for the exact reviewed DeathMarkers 2.0.0 property before calling the ordinary
setter; the release DLL and its transfer semantics remain authoritative. This
is a pinned compatibility guard, not a general mod-specific runtime patch lane.

For each numbered slot, one versioned aggregate binds module payloads to the
exact logical Celeste base-save SHA-256 and static closure. iOS uses private
Application Support A/B snapshots. tvOS uses the separate bounded compressed
keys `CelesteAppleEverest.Slot<N>.State.{A,B}.v1`. Previous-good data is only
selected when it matches the exact base save; otherwise the affected module
returns to its normal default. Base-save replacement, deletion, malformed
data, and closure/schema changes therefore cannot silently inherit unrelated
module state.

The initial bounds are 512 KiB per module SaveData/Session payload and 2 MiB
per logical iOS slot aggregate. tvOS additionally limits each expanded replica
to 512 KiB, each compressed replica to 126,976 bytes, and all six numbered-slot
A/B replicas to 761,856 bytes. The selected fixture is tiny relative to these
bounds. Module settings keep their independent 16 KiB global document.

Binary SaveData/Session, custom `Serialize`/`Deserialize`, custom `Read`/`Write`,
legacy synchronous save APIs, and unsupported dynamic graphs are explicitly
deferred. The static map launcher still uses nonpersistent debug slot `-1`;
this work does not enable general custom-map Save and Quit or Everest LevelSet
progression.

Precompiled Everest map binaries are validated as Celeste map containers and
their package header is normalized to the exact static mount path. This is the
build-time equivalent of Everest accepting the conventional `Contribution`
package label: the string table and element body remain the pinned mod bytes,
while vanilla Celeste's path-integrity check stays enabled at runtime.
Accepted external assemblies are fully rooted by generated managed identity;
the product gate rejects a linked DLL if trimming removed any executable method
body before full AOT. It also resolves every reference from that DLL against
the linked Celeste/Everest API and verifies that every executable external
method has a native body across the paired LLVM and Mono AOT objects. This
includes rejecting members which exist but are not legally accessible from the
mod assembly. It prevents an unresolved or inaccessible compatibility method
from becoming an on-device failure or JIT fallback.

## Audited compatibility ladder

| Mod | Exact input | Result | Principal boundary |
| --- | --- | --- | --- |
| [Dashless Dream Blocks](https://github.com/coloursofnoise/DashlessDreamBlocks/releases/tag/v2.0.0) | 2.0.0; ZIP `ef5071ad…a7b`; source `7c5aca66…6e6` | **DEFERRED_DYNAMIC_TARGET_AND_IL** | Runtime-selected direct-Hook targets plus active `IL.*` and unsupported gameplay state. It cannot use the fixed signature-plan class safely. |
| [GoldenTrainer](https://gamebanana.com/mods/364819) | 1.5.4; ZIP `2a39b5bb…fc5b`; source tag `96439220…a6897` | **DEFERRED_IL** | Real `IL.Celeste.SummitCheckpoint.Update` manipulator plus a direct `ILHook` on `Player.orig_Die`. Both delegates depend on live module settings/state. The binary analyzer now identifies the `IL.*` type from CLI metadata; freezing is deferred until the mixed direct-hook and unsupported `On.*` closure can be lowered safely. No explicit redistribution license was located. |
| [BetterSaves](https://github.com/Microck/bettersaves/releases/tag/v0.1.0) | 0.1.0; ZIP `9e9013f0…10c`; source `6bc11db0…756` | **DEFERRED_PLATFORM** | Save-filesystem semantics plus unsupported `OuiMainMenu`/`MainMenuClimb` hooks. |
| [ExtendedIdle](https://github.com/KnowHT1515/ExtendedIdle/releases/tag/v1.0.0) | 1.0.0; ZIP `cc128b76…299`; source `fb65c482…4c3` | **DEFERRED_DEPENDENCY** | Requires SkinModHelperPlus and unsupported `Player` hooks. |
| [Small Spaces](https://gamebanana.com/mods/590825) | GameBanana package 590825; ZIP `78f71b3c…dab` | **REJECTED_PACKAGE** | Supplied archive has no unique root `everest.yaml`; wrapper/partial packages are never guessed. |
| [Spikeless](https://gamebanana.com/mods/382011) | 1.0.0; ZIP `4880be51…a13` | **DEFERRED_DEPENDENCY** | Content itself is static, but required FrostHelper and MaxHelpingHand are unresolved. |
| No Gondola | GameBanana package 150520; ZIP `84debe67…cf` | **REJECTED_PACKAGE** | No unique root Everest metadata. |
| [Snowy Assorted Items](https://github.com/Snowy063/SnowyAssortedItems) | metadata 0.2.0; ZIP `ec1f595a…e23`; source `2d1a961d…fbb` | **DEFERRED_ON_HOOK** | Unsupported `CassetteBlockManager` HookGen target; custom-entity work remains outside this rung. |
| [Double Languages](https://github.com/Logabe/DoubleLanguages/releases/tag/0.3) | 0.3.0; ZIP `28096690…3f1`; source `af97c135…d17` | **DEFERRED_ON_HOOK** | Unsupported `GameLoader` and `Textbox` targets. No explicit redistribution license was located. |
| [Trailine](https://gamebanana.com/mods/349341) | 1.1.0; ZIP `c6cd0464…711`; source `4f12442b…3fb` | **DEFERRED_DETOUR_CONFIG** | A version-gated historical block uses `DetourContext.After = *`. It is dead for the pinned Everest version but is not silently eliminated by the production transformer. |
| [Extended Variant Mode](https://github.com/maddie480/ExtendedVariantMode) | source audit `fa9a25c3…cc3` | **UNSUPPORTED_LUA** | Real NLua dependency; Lua is not present in the device product. |
| [SmoothCeleste](https://github.com/bybrooklyn/SmoothCeleste) | source audit `b4169752…f5a` | **UNSUPPORTED_NATIVE** | Runtime native-library loading and platform P/Invoke are outside the fixed native closure. |
| [VortexHelper](https://github.com/CommunalHelper/VortexHelper) | 1.2.19; ZIP `b6280fe2…4db2`; DLL `f5a32f02…f73` | **PARTIAL — STATIC_IL_EVENT_FREEZE** | Its two exact entity-local HookGen IL events are source-free, deterministic, and frozen using existing static/compiler-singleton lowering. The complete helper remains deferred for live `DynamicData`, uncatalogued `On.*`, API/content breadth, and optional integration surface. |
| Space Trip | 1.0.0; ZIP `b08d42f8…2fdc` | **DEFERRED_CONFIGURED_AND_NON_IL** | VortexHelper's two ordinary IL blockers are eliminated by Stage 25H-C, but LunaticHelper configured ordering, custom audio, `DynamicData`, and ordinary helper breadth remain. No multi-helper support is claimed. |

The full Stage 25F-A ten-mod durability audit is in
[`apple-everest/module-durability-audit-stage25f.json`](../apple-everest/module-durability-audit-stage25f.json).
The earlier Stage 25E candidate audit, exact selected SHA-256 values, source pins,
and license findings are in
[`apple-everest/helper-ecosystem-compatibility-stage25e.json`](../apple-everest/helper-ecosystem-compatibility-stage25e.json).
Earlier managed-detour findings remain in
[`apple-everest/managed-detour-compatibility-stage25d.json`](../apple-everest/managed-detour-compatibility-stage25d.json).
No third-party ZIP, DLL, map, texture, or source file is tracked.

## What is not supported yet

Unknown `On.*`, any unregistered `IL.*`, dynamic/ambiguous direct Hook,
direct `ILHook` outside the exact static/unconfigured/immutable class,
unsupported RuntimeDetour members/configuration,
NativeDetour, native/P/Invoke additions, Lua, dynamic assembly loading,
Reflection.Emit, desktop process/file-watcher behavior, unregistered helper
ecosystems, dynamic/open-generic or custom runtime ModInterop systems, settings
outside the bounded shapes, and module SaveData/Session outside the bounded
default-YAML class fail closed or remain explicitly deferred. Custom audio is
supported only through the exact `STATIC_CUSTOM_FMOD_BANK` class described
above; dynamic bank discovery, custom master/strings replacement, native FMOD
plugins, programmer-sound callbacks, and arbitrary hot unload/reload remain
deferred. The exact Strawberry Jam Beginner audit inputs have been inspected
and its four C-class helpers plus bounded root behavior now have hash-locked
static semantic plans. This is not complete package support and the playable
slice remains unbuilt while its custom-bank and Crystalline/Vortex mechanisms
are pending.

## Strawberry Jam Beginner semantic plans

Stage 25K-E accepts five narrow `STATIC_SEMANTIC_LOWERING` identities. Their
factories and runtime source are selected from the immutable plan rather than
from a mod-name dispatcher:

| Exact package | Accepted Beginner behavior | Remaining package boundary |
| --- | --- | --- |
| ContortHelper 1.5.5 | `MadelineSpotlightModifierTrigger` | Every other Contort entity, effect, setting and hook is unselected. |
| ExtendedVariantMode 0.50.5 | Typed `BackgroundBrightness` fade/reset and fixed render effect | General variant registry and nine dynamic-lifetime configured sites remain rejected. |
| JungleHelper 1.4.10 | `MossyWall` with exact authored sprite directory, collider and static-mover behavior | Lantern, skin enforcement, cassette and unrelated Jungle behavior remain unselected. |
| YetAnotherHelper 1.2.5 | Horizontal `BubbleField`, strength 1.5, Always mode, lift and wind | Vertical and conditional modes and all unrelated entities remain unselected. |
| StrawberryJam2021 1.0.12 root | Typed lifecycle/state, Beginner masks/glow/jars, playback toggle and collab-facing lobby data | Full root entity/effect/cutscene library, custom audio loading, and non-Beginner behavior remain unselected. |

Every method in the five pinned DLLs has one of five closed reachability
classifications. Required reflection and `DynamicData` observations are replaced
by generated exact access or typed state, leaving zero runtime module/helper
discovery and zero arbitrary attachment. The two exact maps retain their full
source hashes and BinaryPacker appendices in metadata only; they are not staged
into the K-E product.

The full 18-graph census and explicit distance table are tracked in
[`apple-everest/graph-il-closure-audit-stage25hc.json`](../apple-everest/graph-il-closure-audit-stage25hc.json).
