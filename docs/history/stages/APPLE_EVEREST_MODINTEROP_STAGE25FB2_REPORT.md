# Stage 25F-B2 — real ModInterop pair and bounded HookGen completion

Status: **PASS — GREEN**

Date: 2026-08-21

Stage 25F-B2 completes the bounded YELLOW rung recorded by Stage 25F-B. The
ordinary ConditionHelper 1.0.0 and AchievementHelper 1.0.5 release DLLs now
form one source-free, full-AOT Apple closure. Their existing static ModInterop
plan is unchanged; the follow-up adds only the complete reviewed HookGen target
set and three narrow compatibility surfaces discovered by full-product and
physical testing.

## Baseline and immutable references

| Item | Accepted value |
| --- | --- |
| Integration/start commit | `9fd2a809cf20521be7a1403b78fc1d15bbcc1782` |
| Historical Stage 25F-B YELLOW parent | `6cde1ad9ba93bbd2f57f2867baf3b189b2824b1b` |
| Feature branch | `feature/apple-everest-static-modinterop-b2` |
| Final feature commit | The commit containing this report; the final response records its exact full SHA |
| Everest stable source | `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00` |
| MonoMod semantic reference | `dfc30a1506d37fb88a2c2be004f525205f46a24c` |
| iOS recovery tag | `27e16b4724d94d3991b99c4795f680fcb0e5830c` |
| tvOS RC1 | `ee52b0868df091746f134d95d4f020f94f23d4fb` |
| tvOS RC2 | `641e86e4ed164cdf93f602ce2f11436449654d6e` |

The original B1 branch and YELLOW report remain historical evidence. The
integration branch, release refs, vanilla builders, native inputs and
canonical Celeste inputs were not modified. No GitHub Actions workflow ran.

## Complete binary blocker census

The Mac-side Cecil census reads the ordinary distributed DLL event accessors,
including add and remove sites, rather than estimating from source or names.

| Boundary | Result |
| --- | ---: |
| ConditionHelper HookGen events | 29 |
| AchievementHelper HookGen events | 5 |
| Distinct pair events | 34 |
| Pair events already catalogued | 1 (`Level.LoadLevel`) |
| New target descriptors | 33 |
| Catalog before / after | 18 / 51 |
| Unresolved pair blockers after bounded fixes | 0 |

ConditionHelper's exact 29-event inventory is:

- `On.Celeste.AreaModeStats::Clone`
- `On.Celeste.AreaStats::Clone`
- `On.Celeste.Commands::CmdHeartGem`
- `On.Celeste.Commands::CmdHearts_int`
- `On.Celeste.Commands::CmdHearts_int_string`
- `On.Celeste.Commands::CmdLevelFlag`
- `On.Celeste.Commands::CmdOWComplete`
- `On.Celeste.HeartGem::RegisterAsCollected`
- `On.Celeste.Level::LoadLevel`
- `On.Celeste.Level::Reload`
- `On.Celeste.Level::Update`
- `On.Celeste.Level::UpdateTime`
- `On.Celeste.MapData::Load`
- `On.Celeste.Player::Added`
- `On.Celeste.Player::CallDashEvents`
- `On.Celeste.Player::Removed`
- `On.Celeste.Player::SceneEnd`
- `On.Celeste.SaveData::AddDeath`
- `On.Celeste.SaveData::AddStrawberry_AreaKey_EntityID_bool`
- `On.Celeste.SaveData::RegisterCassette`
- `On.Celeste.SaveData::RegisterCompletion`
- `On.Celeste.SaveData::RegisterHeartGem`
- `On.Celeste.SaveData::StartSession`
- `On.Celeste.Session::SetFlag`
- `On.Celeste.Session::UpdateLevelStartDashes`
- `On.Monocle.Entity::Added`
- `On.Monocle.Entity::Removed`
- `On.Monocle.Entity::SceneEnd`
- `On.Monocle.Scene::Begin`

AchievementHelper's exact five-event inventory is:

- `On.Celeste.OuiChapterSelect::Enter`
- `On.Celeste.OuiChapterSelect::Leave`
- `On.Celeste.OuiChapterSelect::Render`
- `On.Celeste.OuiChapterSelect::Update`
- `On.Celeste.SaveData::InitializeDebugMode`

## Exact target expansion

`Level.LoadLevel` was the only pair event already in the 18-target v1
catalog. That v1 file remains tracked solely so the Stage 25D historical report
and verifier keep their exact evidence; only v2 is embedded in the current
builder. The complete new set is therefore:

1. `On.Celeste.AreaModeStats::Clone`
2. `On.Celeste.AreaStats::Clone`
3. `On.Celeste.Commands::CmdHeartGem`
4. `On.Celeste.Commands::CmdHearts_int`
5. `On.Celeste.Commands::CmdHearts_int_string`
6. `On.Celeste.Commands::CmdLevelFlag`
7. `On.Celeste.Commands::CmdOWComplete`
8. `On.Celeste.HeartGem::RegisterAsCollected`
9. `On.Celeste.Level::Reload`
10. `On.Celeste.Level::Update`
11. `On.Celeste.Level::UpdateTime`
12. `On.Celeste.MapData::Load`
13. `On.Celeste.OuiChapterSelect::Enter`
14. `On.Celeste.OuiChapterSelect::Leave`
15. `On.Celeste.OuiChapterSelect::Render`
16. `On.Celeste.OuiChapterSelect::Update`
17. `On.Celeste.Player::Added`
18. `On.Celeste.Player::CallDashEvents`
19. `On.Celeste.Player::Removed`
20. `On.Celeste.Player::SceneEnd`
21. `On.Celeste.SaveData::AddDeath`
22. `On.Celeste.SaveData::AddStrawberry_AreaKey_EntityID_bool`
23. `On.Celeste.SaveData::InitializeDebugMode`
24. `On.Celeste.SaveData::RegisterCassette`
25. `On.Celeste.SaveData::RegisterCompletion`
26. `On.Celeste.SaveData::RegisterHeartGem`
27. `On.Celeste.SaveData::StartSession`
28. `On.Celeste.Session::SetFlag`
29. `On.Celeste.Session::UpdateLevelStartDashes`
30. `On.Monocle.Entity::Added`
31. `On.Monocle.Entity::Removed`
32. `On.Monocle.Entity::SceneEnd`
33. `On.Monocle.Scene::Begin`

No unrelated Celeste method was added “just in case”. Catalog v2 continues to
describe target signatures rather than mod names. The generator emits the
typed `orig` and hook delegates, owner-aware add/remove accessors, cached chain,
direct no-hook path, AOT roots and one target-body rewrite from each descriptor.
ConditionHelper and AchievementHelper do not appear in the generic detour
generator.

The new signature matrix covers:

- static and instance targets;
- zero through eight explicit arguments (`CmdOWComplete` is the maximum);
- value, enum, string and reference parameters;
- `void`, two `IEnumerator`, and two clone/reference returns;
- exact overloaded `CmdHearts_int` and `CmdHearts_int_string` events;
- inherited `Entity` and `Player` `Added`/`Removed`/`SceneEnd` events as
  distinct target contracts.

Generated MMHOOK ABI tests compile the exact delegates against a desktop
fixture, exercise argument and return propagation, prove add/remove/reload,
and assert each target is rewritten once. ConditionWatcher unload removes only
its own 29 handlers. AchievementHelper removes only its five handlers. Reload
does not duplicate callbacks or reorder independent owners.

## Static ModInterop preservation

The exact B1 plan is unchanged:

`9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318`

It contains three registrations, seven exports, four imports and four resolved
imports. The real imports are:

- `ConditionHelper.ConditionChanged`
- `ConditionHelper.WatchConditions`
- `ConditionHelper.RemoveCallback`
- `ConditionHelper.EvaluateConditionExpression`

Both provider-first and consumer-first load orders, late provider refresh,
idempotent registration, missing optional providers, overload/signature
selection, and typed delegate assignment remain covered by the pinned desktop
conformance suite. The device still uses generated direct delegate
assignments—no `MethodInfo` registry, `Delegate.CreateDelegate`, reflection
scan or runtime DLL loading exists.

Physical logs on iPhone and Apple TV recorded successful binding of all four
imports and real invocation of `WatchConditions` and
`EvaluateConditionExpression`. The remaining imports are bound and AOT-covered
but were not falsely reported as observed calls in this acceptance scenario.

## Bounded adjacent compatibility findings

Three second-order blockers appeared only after the complete pair advanced to
full-product and physical testing:

1. AchievementHelper references Everest-publicized `TextMenu.Items` and
   `TextMenu.SubHeader(string)`. The derived Apple tree exposes exactly that
   bounded ABI.
2. Retained regression helpers reference passive Everest settings attributes.
   Their exact metadata constructors are supplied without runtime reflection.
3. AchievementHelper constructs an Everest `ButtonBinding` setting before
   Celeste input exists. The first physical iPhone launch exposed a null
   gamepad dereference. Pinned Everest creates the logical binding first and
   attaches its `VirtualButton` after input initialization. B2 now generates
   that same typed two-phase lifecycle.

The ButtonBinding fix is generic for declared module binding properties and
does not special-case AchievementHelper. `Everest.Content` support remains a
bounded generated asset API: the helper's YAML achievement list is parsed on
the Mac into typed factories and retained data. There is no general device
filesystem/content scan.

## Real end-to-end acceptance

The tracked `AppleEverestModInteropAcceptance` fixture is project-owned data
only. It contains no DLL and no substitute provider/consumer code. Its one
achievement is:

- condition: `totalDeaths() > 0`
- visible title: **First Apple Death**

The physical call path is:

```text
normal Celeste death
  -> SaveData.AddDeath
  -> generated On.Celeste.SaveData hook chain
  -> ordinary ConditionHelper ConditionWatcher
  -> watched expression is reevaluated
  -> ordinary AchievementHelper callback
  -> visible First Apple Death presentation
```

This proves that a newly enabled real provider HookGen event—not a test-only
callback—drives the real consumer through the unchanged ModInterop plan.

AchievementHelper's ordinary `EverestModuleSaveData` is serialized by the
generic Stage 25F-A typed durability path. Apple TV logs recorded a 102-byte
AchievementHelper SaveData payload during the physical save. On all three
platforms, a normal Save and Quit followed by process termination/relaunch
restored the award and suppressed a second popup. On iPhone, deliberately
terminating externally before saving allowed the unsaved award to appear
again; this is the expected save boundary, not corruption or resurrection of
committed state.

## Source-free closure and products

The final closure was generated from exact ordinary release ZIPs plus tracked
project data. No provider/consumer source tree was supplied to the final build.

| Boundary | SHA-256 |
| --- | --- |
| Shared iOS/tvOS closure | `d55f262c376c944a81cf5d253f84dc97592da41ea6e67115ccb33ad6e6b5fabe` |
| Managed logical tree | `2fd67cb7f4cf817dd7098f82484b497b17f232947626b409ea96641afe91a49e` |
| Content logical tree | `26c10db8e2d358a45db18e3ef63147069f123054a74b25728ccb5ffbdf60419e` |
| Hook transform | `8c84c0c895f8c2959e4c6fbf70f684cc03ac44e538c518fb495e26266152472f` |
| ModInterop plan | `9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318` |

An independent source-free regeneration produced a byte-equivalent logical
closure and the same shared hash.

| Assembly | Ordinary DLL SHA-256 | Frozen DLL SHA-256 |
| --- | --- | --- |
| ConditionHelper | `f4471853b8e6c2abdd107609eeee2852076cde4c7e70667367b71a38d72a62b9` | `255efd0d49ef55398d2794ca9bc812d2053e8fed44dc57ac9fa95ea330dbf560` |
| ExpressionParser | `9042e55e89b2ef51ad7e9ab4b7ec310f4a22f12ea7429f4207b67ba9bcc607a6` | `74ba9492295554d2b53738821cdbcc05e69eba4ae5a9735c40ae6190751ff078` |
| AchievementHelper | `5dd3ceadbd5c085c5b1c4ffebef9f7560c80c8a94f9dc35d4001f2b4443ba5b0` | `d13dfb945147eb20e9563e340449deec7cc5bc9b5087eddca7af2adb803f04d6` |
| CpopHelper | `235d767a0f82f45aef7b01e4ef662934af449ee416deb6073a1eaca3263c75c9` | `9242842076d0992e3498a7b6efb3d48ab8e9fc09ea9ba07819bbc9f716e64845` |
| DeathMarkers | `620e5b639b057a46890e7f0ed8a828fadb422a7b4adcd6b1c53bbb6568acdff8` | `4ede9f544bc18f703daa7dfaf997dc4b84ff127083f02ffe2731736ff2390e2b` |
| LagPauser | `6ed3515105056ff2f4be84ef5542ce0a7b90dca9f5bd305703c506f462ffcdfa` | `c92a88759faec662eb2dc98852b33973521d969582a20b7c50d572b95079f121` |

| Product | Bytes | SHA-256 | Build observation |
| --- | ---: | --- | --- |
| iOS Release IPA | 883,044,077 | `4bed659bfab047cfe585d2e02939f8916506fd95e8c06adf5926829cbdaa17c7` | Part of one clean all-platform build |
| tvOS Release IPA | 897,422,666 | `d4c8049b6fe240d5b7e0ed5b95755a93083dd5dcea4cbf7a96fdb95644e461b1` | Part of one clean all-platform build |

The combined signed all-platform build took 548.92 seconds. The build script
does not separately time iOS and tvOS, so no invented per-platform durations
are reported. Both products are Release, fully trimmed and full AOT with
`UseInterpreter=false`. All executable methods in all six frozen assemblies
passed linked-body, referenced-API, LLVM AOT-object and companion Mono AOT-
object verification. Runtime scans reject JIT, runtime assembly loading,
runtime ModInterop reflection, RuntimeDetour patching, ILHook, NativeDetour,
dynamic code, Lua and process/file-watcher behavior.

## Physical acceptance

### iPhone

The same-identity Release build reached a functional menu, bound all imports,
invoked the real exports and displayed the achievement after the first death.
Gameplay, death/respawn, pause, audio, touch and controller input remained
functional. Save and Quit persisted the award; a cold relaunch did not award
it again.

### iPadOS 15.8.8

The same universal iOS product was installed through the legacy deployment
path and passed launch, audio, touch/controller input, visible award, Save and
Quit, cold restore, rotation and background/foreground. The restored award did
not reappear after another death.

### Apple TV

The same shared closure in the tvOS product reached FNA3D Metal, loaded all
seven FMOD banks, bound all imports and invoked the real provider. Controller,
audio, gameplay, death/respawn, pause, Home/reopen and visible achievement all
passed. Save and Quit persisted the award and prevented a duplicate after an
app-switcher cold relaunch.

The Level/Update hot-hook closure remained responsive at the normal observed
frame cadence on all three devices. Chains are cached and rebuilt only when
subscriptions or owner state change; the no-hook path is direct and no
per-frame object array, reflection scan or dynamic dispatch was introduced.
This is a focused practical observation, not a claim of mathematically zero
diagnostic cost.

## Deterministic verification

- Pinned MonoMod ModInterop behavior: **14 pass**.
- Desktop HookGen/direct-Hook ordering and lifecycle: **pass**.
- Current signature-driven builder suite: **226 pass**.
- Typed HookGen semantic matrix: **48 pass**.
- Pinned MonoMod IL-freeze references: **2 pass**.
- Stage 25F-B2 source, exact fixture, census, closure, IPA and physical
  verifier: **181 checks**.
- Historical Stage 25D verifier at its accepted commit: **144 checks**.
- Historical Stage 25E verifier at its accepted commit: **130 checks**.
- Historical Stage 25F-A verifier at its accepted commit: **143 checks**.
- Historical vanilla iOS Stage 24E2 verifier at its accepted commit: **89
  checks**.
- Current tvOS Stage 14 product/privacy and documentation-link inventory:
  **pass**, including 179 documentation links.
- Current repository privacy, builder-help and Git-isolation verifier:
  **pass**.

## Regressions, locks and privacy

Stage 25F-A module durability remains the shared implementation used for the
real award. Stage 25E's Cpop/QuizSample registry and Stage 25D's HookGen/direct
Hook helpers remain in the regression closure and their deterministic source
contracts pass. Historical verifiers that intentionally lock earlier
transformer/closure versions remain unchanged and are evaluated at their
accepted commits where necessary; B2 owns transformer v7 and catalog v2.

Vanilla iOS and tvOS builders remain mod-free. Canonical locks remain:

- Content `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46`
- Raw `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273`
- Patched `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5`
- Stage 6 `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9`
- vanilla iOS 944-file tree
  `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357`

Native locks remain:

- iOS `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`
- tvOS `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`

No third-party ZIP, DLL, source tree, game data, save, signing value, private
path, device identifier or physical access credential is tracked. The public
repository contains only detection, deterministic transformation, generated-
API source, tests and project-owned data. No GitHub Actions minutes were used.

## Recommendation

Stage 25F-B2 is **GREEN** and integration-ready. Fast-forward `tvos-port`
directly from `9fd2a809cf20521be7a1403b78fc1d15bbcc1782` to the final B2 branch tip.
Do not merge the historical `6cde1ad9ba93bbd2f57f2867baf3b189b2824b1b`
checkpoint separately; it remains the useful YELLOW record inside the B2
history.

The next high-value experiment is a modest real map graph with at least two
actively used code helpers. Prefer a graph composing already-green custom
entities/triggers, HookGen/direct Hook, ModInterop, settings and module
durability while avoiding active `IL.*` if a meaningful candidate exists.
Deterministic IL becomes the rational next compatibility investment only if
good real multi-helper candidates consistently require it. No manual download
is required merely to complete B2; future graph selection may require the user
to provide one exact lawful public release if automated public download is not
available.

For ordinary medium-complexity maps, the largest remaining gaps are multi-
helper breadth, uncatalogued Everest/publicized APIs, custom content/audio and
active IL/ILHook. Strawberry Jam additionally remains blocked by its very
large dependency composition, Lua/native policy and content-virtualization
breadth. It was not downloaded, built or tested in this stage.
