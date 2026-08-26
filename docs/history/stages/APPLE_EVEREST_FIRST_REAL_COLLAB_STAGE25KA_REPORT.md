# Stage 25K-A — first real CollabUtils2 lobby/collab

Status: **PASS — GREEN_IOS_IPADOS / TVOS_PHYSICAL_PENDING**

Apple TV physical status: `TVOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE`

Stage 25K-A started at `9f3dcaa75ef14ff02ed09cdad4b0b669c5d0c198`
on `feature/apple-everest-first-real-collab`. The last all-three-device
physical GREEN checkpoint remains
`57d55c7b9e15c5fa84847d2879f774f30025a96a`. The immutable iOS recovery
commit remains `27e16b4724d94d3991b99c4795f680fcb0e5830c`.

Development integration-ready: **YES**

All-platform release-ready: **NO**

## Result

The shared static-AOT Apple Everest product now represents one ordinary real
CollabUtils2 collab as build-time typed data: one distributed lobby, two
distributed subordinate maps, real chapter-panel launch, journal state,
package-defined return locations, per-map completion flags and mini hearts.
The device performs no mod-folder, archive, assembly or `Everest.Content.Mods`
discovery. It loads no CollabUtils2 or helper DLL and performs no runtime
detouring or IL rewriting.

This stage intentionally does not claim general CollabUtils2 compatibility.
Only the behavior required by this exact reviewed graph is supported.

## Selection and provenance

The discovery pass examined 429 public metadata records, retained 47 credible
small collab releases and deeply audited the best 18 using a deterministic
distance vector. The selected release was the smallest graph that proved the
architecture without forcing another major compatibility mechanism.

| Property | Accepted value |
|---|---|
| Package | hennyburgr's Mapping Competition Entries 1.1.0 |
| Collab display name / ID | `burgr king` / `HennyburgrCompEntries` |
| Author | hennyburgr |
| Public release | [GameBanana page](https://gamebanana.com/mods/150465), [ordinary release ZIP](https://gamebanana.com/mmdl/1144148) |
| ZIP SHA-256 | `638ad7beac7a24600c7c0733acf12a645795bd8878fd5bc7c88e6e24b17dce39` |
| Extracted logical SHA-256 | `73785a6c562fcb16fc1738840483da0c35526c80344adee9fc0180baa49a4ed2` |
| License/provenance finding | No explicit software/content license was declared inside the reviewed archive; this is the author's public release and no third-party bytes are redistributed by the repository. |

The package is an unchanged public ZIP with no managed DLL, custom FMOD bank,
Lua file or native payload. It contains exactly one one-room lobby and two
one-room maps, making its physical acceptance bounded while still proving a
real collab relationship.

## Static collab manifest

| Role | SID / room | Source map SHA-256 | Staged SHA-256 | Compatibility ID |
|---|---|---|---|---|
| Lobby | `HennyburgrCompEntries/0-Lobbies/lobby` / `a-01` | `6bffc2c2cc5d077527051322cfccab8916b33cefc253fc77cb1c7614377e8d2c` | `e3b7b2e817424ab7835d422104a1957cfee4069aa70454ec74cb9ebd81f0ce01` | `293cc93cdb340dd32eabcbaac4e96fb4504c57dac0b12334113f95e9712d012c` |
| Map A — Through the Walls | `HennyburgrCompEntries/1-Lobby/redboostercomp` / `hennyburgr_1` | `072634ac7290cb765db958c9fca345c7a14d877cf57154dd16ad7322a222c8aa` | `02674e201dd7cb1b20067eedd88c8241e275de6e9f2b96dd1d970870088502f8` | `0a0a36f63a4294380c50ea94baee48191fddec96963f33f92685c80cd5cfb095` |
| Map B — Station Stratosphere | `HennyburgrCompEntries/1-Lobby/stationmovers` / `hennyburgr_01` | `f9afb1908a4d7e47ba29b778fab4e924147c2bae3a84686c0e81b76b5105ebc3` | `ab358aa423948180698d6d5c10166031f36f94062335ed7f9a8d9f8385488dbc` | `419a5bae9425df172b32515079add4f89f0b1e81290d03f55f76cd6474bb077d` |

The lobby LevelSet is `HennyburgrCompEntries/0-Lobbies`; both subordinate
maps belong to `HennyburgrCompEntries/1-Lobby`. Two real
`ChapterPanelTrigger` records, ordered by their package X coordinates, target
the two SIDs. Both declare saving and `SetReturnToHere`; the generated return
authority is lobby room `a-01`, spawn `(160,160)`. The package journal targets
the subordinate LevelSet. Dialog metadata supplies the displayed map names and
authors.

The versioned collab-manifest SHA-256 is
`6925d8780b6427a67ef5438bef8315b80d4707506b5ebe7e61f9fe3c81f928a0`.
`CollabUtils2CollabID.txt` and all folder associations are consumed on the Mac
and frozen into typed generated records. There is no device discovery.

## CollabUtils2 audit and bounded lowering

The exact helper is CollabUtils2 1.13.4. Its authoritative distributed DLL is
`ce7d646252a2fc51f1f513071d97eaa1acedf7ddb0871276c688914e37319f60`;
the reviewed source tree is
`b987d25608874623453e2c75c441661b906d6fd907948699983c2f12ba14c88e`.
The DLL identifies as `CollabUtils2, Version=1.0.0.0` and contains 194 type and
1,062 method definitions.

The pre-AOT Cecil census found 347 On.Celeste method-reference instructions
across 108 On type references, 22 IL.Celeste method-reference instructions
across eight IL type references, nine direct `Hook` constructions, 19 direct
`ILHook` constructions, six `DetourConfig` and two `DetourContext` method
references, and 43 `EmitDelegate` references. It also found three ModInterop
method references plus five ModInterop attributes, 29 reflection method
references, 62 Everest module/content-discovery references, 32 FMOD and 61
Celeste Audio references, 20 custom-entity attributes, and the module's
Settings, SaveData and Session types. General DynamicData, Process,
FileSystemWatcher, P/Invoke and custom-backdrop counts were zero.

Those counts describe the desktop DLL, not the device closure. The broad DLL
is excluded. Exact source and DLL hashes select a repository-owned static
semantic lowering; mismatched versions, source trees, declared DLL paths or
DLL bytes fail closed. The selected Apple product has zero new HookGen targets,
zero new frozen IL, zero direct ILHooks, zero configured hooks, zero DynamicData,
zero ModInterop registrations, zero Lua/native payloads and zero added custom
FMOD banks.

Required CollabUtils2 behavior is:

- build-time collab ID and folder association;
- one lobby and its two subordinate-map relationships;
- chapter panels and author metadata;
- journal rows;
- mini hearts;
- exact Return to Lobby authority;
- durable per-map completion flags; and
- normal map/lobby interaction with the existing save projection.

No supported-but-unused surface was added. Heart doors, silver/rainbow/speed/
golden berries, lobby-unlock progression, multiple lobbies, prologue, gym and
dynamic lobby discovery remain deferred because this graph does not require
them.

## Helper graph and gameplay factories

The deterministic load order is:

1. ChronoHelper;
2. AppleEverestCustomAudioCanary;
3. DJMapHelper;
4. AppleEverestDJFrozenIlContentCanary;
5. CollabUtils2;
6. CommunalHelper;
7. FancyTileEntities;
8. FearoftheDark;
9. FrostHelper;
10. LunaticHelper;
11. MaxHelpingHand;
12. ShroomHelper;
13. HennyburgrCompEntries;
14. LittleEpic's Precision Challenge; and
15. Torremolinos Speedbuild.

Seven exact helper releases are statically lowered: CollabUtils2 1.13.4,
CommunalHelper 1.25.5, FancyTileEntities 1.6.2, FrostHelper 1.79.1,
LunaticHelper 1.1.1, MaxHelpingHand 1.40.9 and ShroomHelper 1.2.10. Dependency
resolution remains topological and rejects missing, cyclic, duplicate or
conflicting identities.

The selected map graph uses these custom IDs:

- lobby triggers: `CollabUtils2/ChapterPanelTrigger`,
  `CollabUtils2/JournalTrigger`;
- Map A: `CollabUtils2/MiniHeart`,
  `CommunalHelper/DreamMoveBlock`,
  `LunaticHelper/StrawberryWithReturn`,
  `MaxHelpingHand/GroupedTriggerSpikesUp`;
- Map B: `CollabUtils2/MiniHeart`, `CommunalHelper/StationBlock`,
  `CommunalHelper/StationBlockTrack`, `FancyTileEntities/FancyFakeWall`,
  `FrostHelper/NoDashArea`, `MaxHelpingHand/CustomSummitCheckpoint`,
  `MaxHelpingHand/FlagSwitchGate`, `MaxHelpingHand/FlagTouchSwitch`,
  `MaxHelpingHand/GroupedTriggerSpikesUp`, `ShroomHelper/AttachedIceWall`,
  `ShroomHelper/CrumbleBlockOnTouch`;
- Map B triggers: `MaxHelpingHand/CameraCatchupSpeedTrigger`,
  `everest/changeInventoryTrigger`, `everest/flagTrigger`,
  `everest/smoothCameraOffsetTrigger`; and
- backdrops: `blackhole`, `parallax`, `planets`, `stardust`, `starfield`.

The product adds 16 bounded semantic factories; the retained pre-existing
catalog is 59 factories and the core registry is four factories. Every
namespaced map ID must resolve to one exact static owner. Unknown or ambiguous
IDs fail the build. `Assembly.GetTypes`, `Activator.CreateInstance`, runtime
DLL loading and reflection factory discovery remain absent.

## Persistence and completion

`AEVPSV1` remains byte-stable schema version 1. A new collab-specific durable
record is unnecessary: the existing SID plus original-map-SHA compatibility
identity already models the lobby and each map independently. Lobby
`CollabUtils2_MapCompleted_<binname>` flags derive from each subordinate map's
durable heart state. No state is double-counted or shared with LittleEpic,
Fear of the Dark or Torremolinos.

The cumulative deterministic fixture contains the four prior maps, lobby and
two subordinate maps. It is 3,189 bytes raw and 984 bytes in the tvOS
representation, 0.77% of the unchanged 126,976-byte replica cap. Three slots
with complete A/B replicas remain bounded at 761,856 bytes. Slot isolation,
delete/recreate, unrelated vanilla import/replacement, update lineage and
newest-replica corruption fallback all passed.

Mini-heart collection registers the map heart, saves and returns through the
generated lobby authority. Completion flags and journal aggregation passed
deterministic tests. Full physical completion/mini-heart collection was not a
mandatory gate and is not claimed unless separately recorded below. No heart
door or special berry behavior is required by this release.

## Determinism and products

Three independently generated complete closures were identical:

| Boundary | Accepted value |
|---|---|
| Shared closure | `ccf4f6db802c935071d019b46fbb77d3be49445f4bb590d4e9a1872c9a4b0660` |
| Managed tree | 46 files; `35d9ccb691ce1a0134bdf7df963661ce43ed31e8f6a7a0c8eedcacc7fe1607e4` |
| Content tree | 4,461 files; `0947c2e4156c02ba2bbdf37681b202378ad52432b9dabcd9dadb71a945977947` |
| Registry | `d7ff46c8ff66d0e5928723494ac65f4b08bfb23c110b621caeb30c2261b65ad1` |
| Hook transform | `590af232c6c7209c9295f3f166478ad44c5bf559da22c72c01e45585fabf66f7` |
| API surface | 30 members; `d594dea86e944b468819818773f2177312445bbebe022bcbc7ea1ff779c8652c` |
| Progression manifest | `55796d3e95a64959f8ddd58e2cf001c06f0b8207f4d37118b60a96aa9d2d7072` |
| LevelSet manifest | `c9ecdcc99221ff03926d282dada049505bd416f66bc1464fa2847d24c36771ba` |
| Collab manifest | `6925d8780b6427a67ef5438bef8315b80d4707506b5ebe7e61f9fe3c81f928a0` |
| Custom audio / bank set | `0a7ca20a24239f4829ab5af7002004309d38d89ac5bffbfefff6a49b278c8841` / `c927c0779c1dbbb5b43bd1a5daa5c2eb5e1c9d00b7e81eab87e70aa0a46eb8d7` |
| Frozen IL plan | five transforms; `5791ac3b27f500d8ae9f2cc72669b09aa7e490d17b33b992a2b55d0a8728b457` |

One shared pre-platform closure feeds both products. Both are Release, fully
trimmed, fully AOT, `UseInterpreter=false` and JIT-free.

The tracked `modern-ios/IOSPortVersion.props` file is the single Apple-port
identity source for both package families. The tvOS host no longer overrides
it with stale plist/project values, and its generated Options menu now shows
the same semantic version and build-number form as iPhone/iPad.

| Product | Bytes | SHA-256 | Result |
|---|---:|---|---|
| Signed universal iOS/iPadOS IPA | 892,308,308 | `ff96e06ca1f50e1e014d99baea2a2051a272c0776c2fbeabd3d89a6a7a7a412b` | version 0.1.1 (build 17); iOS 15 minimum; `UIDeviceFamily [1,2]`; native iPad presentation; development signed |
| Signing-ready unsigned tvOS IPA | 906,029,218 | `ebdad25cdbab2ff44d010645b489c44bece69f46ec9a6fb4fc21d37642f1f1e1` | version 0.1.1 (build 17); tvOS 16 minimum; package/static acceptance; physical pending |

Package verification found the exact lobby and two maps once in each product,
excluded all statically lowered helper DLLs and excluded Mono.Cecil, MonoMod
runtime transformation machinery and the host IL worker.

## Physical acceptance

- iPhone 12 Pro Max: **PASS** on the exact signed build-17 IPA.
- iPad mini 4 / iPadOS 15.8.8, exact same universal IPA: **PASS**.
- Apple TV: `TVOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE`.

Both physical devices passed lobby load, journal and chapter panels, launching
and returning from both subordinate maps, per-map Save and Quit/cold resume,
map-state isolation, survival of the four prior custom-map states, and the
focused touch/controller/audio/pause/rotation/background/reopen matrix.

The Apple TV Release product passed static collab lookup, package, FMOD,
progression A/B, slot/delete/import/corruption, full-AOT and forbidden-surface
checks. That evidence does not substitute for hardware acceptance.

## Regression, locks and privacy

The builder suite passed 443 deterministic tests, including the current shared
contracts for H-D/H-C/H-B/H-A, F-B2/F-A, 25E, 25D and vanilla paths. Earlier
verifiers that intentionally lock smaller historical closures remain unchanged
and accepted at their historical commits. The Chrono bank identity and DJ
five-transform IL plan are unchanged.

Canonical locks remain Content
`30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46`,
raw `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273`,
patched `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5`
and Stage 6 `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9`.
The vanilla iOS source remains
`2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357`;
native iOS/tvOS locks remain
`9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`
and `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`.

No GitHub Actions workflow was run. Protected integration/release refs remain
unchanged. Git tracks no game data, mod ZIP/DLL/map/FMOD bytes, generated game
source, saves, snapshots, IPAs, signing values, device IDs or credentials.

A fresh recursive clone of the exact feature commit copied no ignored fixture,
independently reacquired all 13 public releases, regenerated the same
`ccf4f6db…` closure, passed all 443 builder tests and 108 stage checks, passed
the privacy scan, and finished with clean Git/submodule status.

## Apple TV catch-up and next stage

The cumulative Apple TV list now adds the real lobby, both chapter-panel
launches, Return to Lobby from both maps, independent Save and Quit/cold resume,
completion/journal state and slot/delete/recreate coverage. Earlier vanilla,
Chrono, LittleEpic, Fear, Torremolinos, Save Manager, soft reload, controller,
Home/reopen and graceful Quit requirements remain.

A somewhat harder second collab or broader CollabUtils2 feature slice is now
rational. The screening data shows that doors and special berries plus broader
helper graphs block the nearest candidates more consistently than one proven
configured-IL ordering case. Configured IL is therefore not automatically the
highest-value next mechanism; it should be added only when a specifically
selected graph makes it the dominant blocker. Strawberry Jam is materially
closer because the shared product now has real lobby routing, map ownership,
return and completion semantics, but its much larger map/helper/berry/door/UI
surface remains far beyond this bounded proof.

The evidence recommends **Stage 25K-B — second real collab and broader
CollabUtils2 completion features**, selecting the smallest graph that exercises
a mini-heart door and special-berry/completion aggregation without introducing
another unrelated major runtime mechanism.
