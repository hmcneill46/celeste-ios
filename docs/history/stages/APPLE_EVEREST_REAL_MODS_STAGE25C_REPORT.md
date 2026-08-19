# Stage 25C — real Everest ZIP compatibility ladder I

Status: **PASS — GREEN**

Date: 2026-08-19

Stage 25C advances the shared Apple static-AOT Everest foundation from
project-owned canaries to ordinary public Everest release ZIPs. It proves a
real content map and a real precompiled code mod on iPhone, iPadOS 15.8.8, and
Apple TV without runtime DLL loading, JIT, an interpreter, or separate
platform mod forks. It is still a closed compatibility ladder, not general
Everest support.

## Git and release boundary

| Item | Accepted value |
| --- | --- |
| Starting commit | `b033c76d73b93e4f9b6b37c4f10458561b44f83e` |
| Feature branch | `feature/apple-everest-real-mod-compat-1` |
| Final feature commit | The commit containing this report; the final response records its full SHA without creating a self-referential hash |
| Vanilla iOS recovery tag | `ios-v0.1.1-rc.1^{}` → `27e16b4724d94d3991b99c4795f680fcb0e5830c` |
| tvOS RC1 | `v1.0.0-rc.1^{}` → `ee52b0868df091746f134d95d4f020f94f23d4fb` |
| tvOS RC2 | `v1.0.0-rc.2^{}` → `641e86e4ed164cdf93f602ce2f11436449654d6e` |
| Deferred RC3 branch | `origin/release/v1.0.0-rc.3` → `c8134c8ca7924cf12f48527e714b5242c6024927` |
| RC3 tag | Absent |

`tvos-port` and `origin/tvos-port` remained at the starting commit. No merge,
tag, upstream push, GitHub Actions run, or cloud-template change occurred.

## Candidate audit

Fourteen real candidates spanning content, HookGen, direct detours, IL, Lua,
native code, platform storage, and helper dependencies were audited. The
tracked exact registry is
[`apple-everest/real-mod-compatibility-stage25c.json`](../../../apple-everest/real-mod-compatibility-stage25c.json);
the ignored machine report records the complete CLI inventory.

| Candidate | Exact release/source pin | License finding | Result and reason |
| --- | --- | --- | --- |
| I Accidentally Four Cassette Blocks | 1.0.0; ZIP `37eaa16b6b2d458a8ce27e08ea315b4cc3c2938d6b3c2292c61203c641ad7c95` | No explicit redistribution license located | **SUPPORTED** content-only map/dialog/GUI asset; selected Fixture A |
| Particle Palette Helper | 1.0.0; ZIP `f9cf8874acbfaff87af22098caea36429c97941d0b13e1114d8749be61afbe19`; source `9a791bcb38f64a4c49ea3e496003899416f0bb82` | MIT | **SUPPORTED_WITH_STATIC_TRANSFORM**; selected binary module and bounded `On.*` fixture |
| Dashless Dream Blocks | 2.0.0; ZIP `ef5071ad28ed27ee73749623f4484a511f4793388f53417a4c0ee36c3d6e4a7b`; source `7c5aca66c322866acfde47c250bb9e692f8d3be6` | MIT | **DEFERRED_DIRECT_HOOK**; direct `Hook`, IL references, and unsupported HookGen targets |
| GoldenTrainer | 1.5.4; ZIP `2a39b5bb9524aeac510ff1e0835022a294081784982aca0bf3970fb8e867fc5b`; source `96439220c8d5598ace6bb2f06b75dd78b71a6897` | No explicit redistribution license located | **DEFERRED_IL**; real `IL.*`, direct `ILHook`, captured state, and unsupported HookGen targets |
| BetterSaves | 0.1.0; ZIP `9e9013f029c0ce765d10917dccc4b1eebddce130fb6ec48c4fefeace78c7310c`; source `6bc11db03adbb2226875d4fd8e004a2cb9ef6756` | MIT | **DEFERRED_PLATFORM**; filesystem semantics and unsupported menu hooks |
| ExtendedIdle | 1.0.0; ZIP `cc128b768dae5df77a413fa031dcd87618050613269f1190984a8f6f89c7e299`; source `fb65c482ea31f41ea51c3fef573a76b3c679e4c3` | MIT | **DEFERRED_DEPENDENCY**; SkinModHelperPlus and unsupported `Player` hooks |
| Small Spaces | GameBanana package 590825; ZIP `78f71b3c2223afb226fb047dfe60c711c4c4d6ca7773a8276e93b7676a910dab` | Not established | **REJECTED_PACKAGE**; no unique root Everest metadata |
| Spikeless | 1.0.0; ZIP `4880be5146aae628ea3e1381881b52e877dd589f9137c617f96f21bff3746a13` | Not established | **DEFERRED_DEPENDENCY**; FrostHelper and MaxHelpingHand unresolved |
| No Gondola | GameBanana package 150520; ZIP `84debe674a8cd22dc988c867985cf940db26c8c1f098e112a644645fc88ab4cf` | Not established | **REJECTED_PACKAGE**; no unique root Everest metadata |
| Snowy Assorted Items | Metadata 0.2.0; ZIP `ec1f595a1fe1b9578aa9c1a03bdc7ee42ac95c02ee778222394a80302b011e23`; source `2d1a961de1ba43beefbc7e1eb6d8da0d46ad1fbb` | MIT | **DEFERRED_ON_HOOK**; `CassetteBlockManager` and custom-entity work |
| Double Languages | 0.3.0; ZIP `280966901439dcf11340add42fb9efc99b397d302ebb312de2ecde0fe62d03f1`; source `af97c135af7aca98069fea7af98415af52c52d17` | No explicit license located | **DEFERRED_ON_HOOK**; `GameLoader` and `Textbox` targets |
| Trailine | 1.1.0; ZIP `c6cd04644b8cb98c2c2718ce1afaf185023c13cb5dd9650bb86f8a2d4ba5711b`; source `4f12442b4b2ffd375fbd50c99d8385d4cc0f63fb` | MIT | **DEFERRED_DIRECT_HOOK**; RuntimeDetour behavior and five unsupported HookGen surfaces |
| Extended Variant Mode | Source `fa9a25c356120c31196e21387e7b709f0cb30cc3` | MIT | **UNSUPPORTED_LUA**; real NLua dependency |
| SmoothCeleste | Source `b41697526e6bbcc9a6393dd00b0d09ec29616f5a` | MIT | **UNSUPPORTED_NATIVE**; dynamic native-library loading/PInvoke |

The two selected release ZIPs were chosen because together they cross the
first meaningful real-world boundary with a very small graph: one playable
content package and one precompiled code assembly whose six normal HookGen
subscriptions fit a bounded mechanism catalog. No third-party bytes are
tracked or redistributed.

## Binary-first architecture

The production contract is the ordinary release ZIP, not a source checkout:

```text
Everest ZIP / declared DLL
  → safe bounded ingest + exact graph
  → Mono.Cecil CLI/reference/mechanism audit
  → generic MMHOOK scope rebinding to the static Celeste facade
  → identity-preserving frozen DLL
  → direct generated module factory + complete assembly trimmer root
  → iOS/tvOS full native AOT
```

The selected final input directory contained only the two release ZIPs. The
Particle Palette Helper source checkout was removed from the closure input;
two independent final generations still succeeded and produced identical
manifests. The module retains assembly name `ParticlePaletteHelper`, its
namespace/type identity, and its normal parameterless module construction.

| Artifact | SHA-256 |
| --- | --- |
| Distributed `ParticlePaletteHelper.dll` | `eac2cb52112dafcb31f86cdad0f3373bb781a7f00e705394fb811c0f7ef08c17` |
| Frozen Apple-compatible DLL | `3c8f459ec016c233f35a9ae723cd03035b745855867747dde53d5c26ed3157f0` |

The compatibility manifest preserves the release ZIP hash, original DLL hash,
frozen DLL hash, profile and transformer identity. The generated registry
directly constructs the concrete module type. The project references the
frozen assembly at build time and generates
`TrimmerRootAssembly Include="ParticlePaletteHelper"`; no device code searches
for or loads it.

The package verifier compares every executable method body between the frozen
and linked DLL, resolves its full Celeste/Everest API surface against the linked
product, and verifies native method bodies across the paired LLVM and Mono AOT
objects. This final gate was added after the first physical build exposed an
AOT-only JIT fallback in `PaletteProfileRegistry.Reload()`: the trimmer had
preserved the module entry point but not an indirectly reached helper. The
corrected complete assembly root and AOT-object scan fail that defect on the
Mac before installation.

The facade was extended only for concrete binary API requirements: Everest's
nested content fields and typed interpolated logger contract. There is no
runtime reflection, dynamic dispatch generator, desktop Everest service, or
mod-name special case.

## Static HookGen compatibility

The ordinary distributed DLL's `MMHOOK_Celeste` type references are rebound to
the statically linked compatibility facade. The original event add/remove
instructions remain normal CLI calls; no per-mod source edit is performed.
The newly supported exact targets are:

- `On.Monocle.ParticleSystem.Emit(ParticleType, Vector2)`;
- `On.Monocle.ParticleSystem.Emit(ParticleType, Vector2, float)`;
- `On.Monocle.ParticleSystem.Emit(ParticleType, Vector2, Color)`;
- `On.Monocle.ParticleSystem.Emit(ParticleType, Vector2, Color, float)`;
- `On.Monocle.ParticleSystem.Emit(ParticleType, Entity, int, Vector2, Vector2, float)`;
- `On.Celeste.TrailManager.Add(Entity, Color, float, bool, bool)`.

Dispatch is strongly typed. `orig` receives the exact receiver/arguments and
preserves return/no-return semantics; handlers nest in HookGen order and the
original executes once. Chains are cached until subscription or owner state
changes, so hot particle calls do not allocate a new chain per frame. The
19-test semantics suite covers multi-handler ordering, `orig`, remove,
duplicate subscription, disable, and re-enable.

The real module's `Load`, `Initialize`, `LoadContent`, `Unload`, and reload path
ran on all three device classes. Turning it Off removed its owned subscriptions;
turning it On rebuilt the same AOT-compiled state without restarting. Its
optional palette YAML/content enumeration is not implemented, and it has no
selected persistent settings UI. Physical proof therefore covers binary module
lifecycle, logger/content ABI, hooks, pass-through behavior, and enable/disable,
not custom palette deserialization.

No selected fixture adds a custom entity or trigger, so that mechanism remains
the Stage 25B project-owned-canary proof rather than a new real-mod claim.
General `EverestModuleSettings`, `EverestModuleSaveData`, and session durability
remain deferred.

## Real content result

I Accidentally Four Cassette Blocks contributed four closure members: one map,
one map metadata file, English dialog, and one GUI asset. The precompiled map
body is valid and contains three rooms and eleven cassette-block entities.

The initial Apple launch reached vanilla `MapData.Load` but rejected the map's
conventional editor package label `Contribution` as corrupted. Desktop Everest
normally relaxes that check at runtime. The accepted static solution instead
validates the `CELESTE MAP` container and rewrites only its two length-prefixed
header strings so the package equals the exact mounted logical path. The
string table and complete element body stay byte-identical, malformed input
fails closed, and vanilla Celeste's runtime integrity check remains enabled.
This mechanism is generic to valid precompiled maps and contains no fixture
name check.

The final normalized map SHA-256 is
`b851f095df94b916c7e080260db47375a5e72f6d1835f9af169d76ab0464c75f`.
It physically loaded and played on iPhone, iPad, and Apple TV; dialog was loaded
and the content manifest mounted all four members.

The diagnostic launcher deliberately uses Celeste's non-persistent debug save
context to avoid touching user slots. Mod-map Save and Quit is therefore not a
supported persistence path in this rung. An attempted iPhone Save and Quit was
rejected by the iOS filename allow-list rather than writing a new unbounded
file, but that rejection surfaced as a background `UserIO` fatal in the canary
instead of a polished disabled action. This is an explicit experimental-canary
limitation, not a successful persistence claim. General mod-session persistence
and its UI/failure policy remain future work; ordinary numbered vanilla saves
are unaffected.

## IL and negative fixtures

GoldenTrainer proves the analyzer recognizes a real distributed
`IL.Celeste.SummitCheckpoint` event reference and direct
`MonoMod.RuntimeDetour.ILHook` construction from CLI metadata. Its manipulators
capture live module settings/state and the same DLL needs five unsupported
HookGen targets. The complete distributed DLL is therefore rejected before
AOT; it is not partially frozen or called production-ready. The pinned host
IL-freeze regression separately proves deterministic manipulator execution and
removal of MonoMod/RuntimeDetour from a closed target, but applying that to
GoldenTrainer needs a production IL target catalog and capture policy.

Dashless Dream Blocks is the direct managed `Hook` negative. Extended Variant
Mode is the Lua negative. SmoothCeleste is the native/PInvoke negative.
Runtime assembly loading, Reflection.Emit, DynamicMethod, NativeDetour,
FileSystemWatcher, process spawning, and arbitrary native loading remain
rejected before AOT. Unsupported real packages never enter the selected
closure.

## Exact shared closure and reproducibility

The selected order is:

1. `IAccidentallyFourCassetteBlocks` 1.0.0;
2. `ParticlePaletteHelper` 1.0.0.

| Boundary | Count / SHA-256 |
| --- | --- |
| Managed closure | 12 files; `7c1ddb85fbacbf375c8c3b3ea126da0daeef8b6e052eb3b6340602988dab68d8` |
| Content closure | 4 files; `935d40a790b792ebef77201b7169af9f40179e8d648215ece66d19f0fdfe1fd5` |
| Generated registry | `b07914264f102e9ffdc804cb400bb7999f4679aaca4b5dc4d8afdc4d3ec3e8c9` |
| Hook transformation contract | `3ae2bdb4f0400555bcbee13d3f583e891f67ef92c6e17f641012e542a4d72336` |
| Complete shared closure | `9c971fe1b84e80092a8d9178bbae9cf0f9588e185f85c6c4c61996775f4ec532` |

Two independent post-fix generations produced byte-identical compatibility
manifests and all five hashes above. The iOS and tvOS build manifests both
record the same complete closure hash. No platform-specific patch is applied
to either real mod after the shared boundary.

## Full-AOT products and physical acceptance

| Product | IPA bytes | SHA-256 | Build observation |
| --- | ---: | --- | --- |
| Universal iOS/iPadOS canary | 882,178,408 | `1b9e713886db0f4d36e8ed2c1005359162eb009f3d662fa4145feefd38e3724a` | Representative clean signed build: 264.33 seconds; final map-normalized rebuild repeated the same package gates |
| tvOS canary | 896,379,076 | `3a8425adb5337fb8763dc8d271ddca4c20497f9e1d97afb2ff9e4afac8248924` | Approximately 4 minutes 45 seconds from clean build directory creation to final IPA |

Both packages are Release arm64, full trim, full AOT,
`UseInterpreter=false`, and JIT=false. Both contain the transformed external
DLL and its AOT data as statically linked product members. Runtime/package scans
found no HookGen backend, RuntimeDetour, NativeDetour, NLua/KeraLua,
Reflection.Emit, DynamicMethod, dynamic assembly-loading path, arbitrary native
mod loader, process spawning, or file watcher.

### iPhone

The separate-identity canary launched to a functional menu with direct Metal,
touch, controller takeover, and seven-bank FMOD. The real DLL loaded without
source; Particle Palette Helper toggled Off/On; the real cassette-block map
loaded; movement, jump/dash, pause/resume, death/respawn, 180-degree rotation,
background/foreground, and cold relaunch passed. Live logs confirmed sustained
Level frame progression and normal audio/haptic events. The user reported all
requested checks passed.

### iPadOS 15.8.8

The same universal IPA launched on the older physical iPad. Landscape/touch,
audio, the real code DLL and toggle, the corrected real map, movement,
death/respawn, lifecycle, and 180-degree rotation passed. Live logs showed the
real map Level with continued frames and cassette-block gameplay/haptics. The
user reported the requested matrix passed.

### Apple TV

The tvOS product used direct FNA3D Metal and loaded all seven FMOD banks once.
DualSense was recognized, the real DLL and four content mounts loaded, the
module toggled Off/On, and the corrected real map ran with controller input,
pause/resume, death/respawn, haptics, Home/reopen, and cold launch. Live logs
showed one runtime and uninterrupted Level progression. The user reported all
requested checks passed.

The canary identities are separate from vanilla products, and no vanilla user
save was used as the mod-map session.

## Desktop reference

The stock desktop Everest renderer was not treated as a physical acceptance
source on this host because the previously documented host/native architecture
mismatch remains. Source and distributed-binary audit established Particle
Palette Helper's intended lifecycle/subscriptions; the real Apple runs proved
those calls. The real desktop HookGen reference still passed exact nested
ordering and removal semantics (`B-before,A-before,original,A-after,B-after`).
This is not overstated as full desktop gameplay parity.

## Locks, regressions, and privacy

Accepted native locks remain:

- iOS: `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`;
- tvOS: `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`.

Accepted canonical vanilla locks remain:

- Content: `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46`;
- raw source: `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273`;
- patched source: `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5`;
- Stage 6: `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9`;
- vanilla modern-iOS tree: 944 files,
  `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357`.

The normal `build-ios.sh` and `build-tvos.sh` have no mod argument. The optional
closure is copied only into ignored, marked derived roots. Stage 25C's 135
source/product/package checks, Stage 25B's 173 source/product checks, 85 builder
tests, 19 HookGen semantics tests, host IL freeze, and desktop HookGen reference
all passed. Current iOS foundation 243, durability 87, C2 84, D2 153, and input
profiles 56 passed. The complete current tvOS chain passed: Save Manager 66,
continuity 57 + 85 source checks, prompts 38, Quit 16, soft reload 21, QR 31,
and HUD 39.

The E1/D3/E2 historical verifiers intentionally freeze a no-later-tvOS-change
predicate at their old iOS baselines. They were not weakened: they passed at
the immutable iOS RC1 boundary (E1 90, D3 161, E2 89), while Stage 25C owns the
current shared-Apple/tvOS conditional-reference invariant.

Repository verification found no tracked Celeste game/mod ZIP, external DLL,
map, texture, generated game source, FMOD input, IPA, save, signing material,
device identity, or credential. There is no runtime downloader, telemetry,
auto-update, or new network permission. Fixture acquisition is host-only and
all downloaded bytes stay ignored.

## Known limits and next stage

The supported real-mod set is exactly the two pinned packages above. General
virtual content/palette deserialization, Mod Options/settings durability,
mod SaveData/session persistence, custom real-mod entities, helper graphs,
unknown HookGen targets, IL/direct detours, Lua, native mods, and custom mod
audio remain unsupported or deferred. The diagnostic map's Save and Quit does
not persist its `debug` context; ordinary vanilla saves are unchanged.

The next most valuable rung is **25D-C: broader bounded `On.*` plus direct
managed Hook compatibility**, followed by a small helper dependency graph with
real custom-entity and settings/content API evidence. A modest helper-dependent
map is plausibly two to three focused compatibility stages away. Strawberry Jam
still spans a large helper/content/IL/native/Lua surface and should not be
attempted until at least roughly six to ten additional measured classes are
green. No manual user download is needed to begin the next audit; public
fixtures can be acquired by the ignored host scripts.

Stage 25C is ready to integrate after final clean-clone verification. It does
not itself merge, tag, or publish a general mod feature.
