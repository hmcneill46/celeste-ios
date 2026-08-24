# Stage 25J-B — second real map and multi-map progression

Status: **PASS — GREEN_IOS_IPADOS / TVOS_PHYSICAL_PENDING**

Apple TV physical status: `TVOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE`

Stage 25J-B started from `55d5a3cdff88e707e38404be206a7046b5c149db`
on `feature/apple-everest-second-real-map`. The last all-three-device physical
GREEN checkpoint remains `57d55c7b9e15c5fa84847d2879f774f30025a96a`.
The feature is development-integration-ready, but it is not an all-platform
release candidate until the cumulative Apple TV catch-up run is completed.

## Result

One source-free static-AOT product now installs two independent ordinary real
maps in the same numbered Celeste save:

- LittleEpic's Precision Challenge 1.0.0 (`LittleEpic` LevelSet); and
- Fear of the Dark 1.0.0 (`BevWeb` LevelSet).

Each map can Save and Quit and cold-resume independently. Adding Fear of the
Dark preserved the pre-existing Stage 25J-A LittleEpic progression and did not
change LittleEpic's compatibility identity. Switching repeatedly between the
two maps preserved both records. The serialized format remains the byte-stable
`AEVPSV1` schema version 1.

## Candidate selection

The audit re-evaluated the existing 53-map Stage 25G cohort, screened 34
supplemental candidates, and deeply audited eight roots plus their required
helpers. Selection used a declared distance vector that penalized unsupported
major mechanisms, new helpers, large graphs, new content factories, new audio
classes, and physical complexity.

Fear of the Dark was selected over the content-only runner-up SpaceJam because
it is still zero-blocker while genuinely exercising an accepted helper factory,
an independent 14-room LevelSet, checkpoints, a golden strawberry and a heart.
It therefore supplies stronger composition and persistence evidence without
introducing a mechanism merely to make the stage pass.

| Property | Accepted value |
|---|---|
| Public ordinary release | [GameBanana download](https://gamebanana.com/mmdl/1057184), [mod page](https://gamebanana.com/mods/423072) |
| Version | Fear of the Dark 1.0.0 |
| ZIP SHA-256 | `7071c67f93a29c0f25c87c366762437bbf60873e135c5ca34009c197f5b40e5b` |
| License finding | GameBanana metadata: CC BY-NC-ND 4.0; ordinary unchanged download/install use |
| Map | `Maps/BevWeb/FearoftheDark/FearoftheDark.bin` |
| Map SHA-256 | `5c29701520776fa03a3e2d8af798c23d80e188a91d6b3f65cac97ca81239de86` |
| SID | `BevWeb/FearoftheDark/FearoftheDark` |
| LevelSet | `BevWeb` |
| Rooms | 14: `01a`–`01e`, `02a`–`02i` |
| Compatibility identity | `a7a63c0a036cda9dcbc16166afb06b1010ba714ef2b0f97b4054319dbf90ff5d` |
| Progression content | one golden strawberry, heart, `02a` checkpoint, change-respawn triggers and normal completion statistics |

The public ZIP is used unchanged and is never committed. The exact dependency
graph is Fear of the Dark → ChronoHelper >=1.0.1 → resolved ChronoHelper 1.3.3,
plus Everest >=1.2330.0. The map actually places
`ChronoHelper/PersistentFallingBlock`; the helper is not merely declared.

## Combined static closure

The stable build order is:

1. ChronoHelper 1.3.3;
2. the existing custom-audio canary;
3. DJMapHelper 1.13.4;
4. the existing DJ frozen-IL content canary;
5. Fear of the Dark 1.0.0;
6. LittleEpic's Precision Challenge 1.0.0.

The two real maps and the two retained canary maps produce four registered
progression maps in one closure. Real-map factory use comprises
`ChronoHelper/PersistentFallingBlock`,
`ChronoHelper/CustomTimeSwitchGates`, and `DJMapHelper/maxDashesTrigger`.
The static entity registry contains 59 factories and zero custom backdrop
factories. All factories remain statically generated; device execution performs
no `GetTypes`/`Activator` discovery.

No new helper, HookGen descriptor, API publicizer member, frozen IL transform,
direct ILHook, configured IL, general DynamicData, ModInterop, module setting,
SaveData/Session adapter, audio class, Lua, native payload, P/Invoke, runtime
DLL loading, or runtime detour was needed. The HookGen catalogue remains 102
targets, the accepted DJ frozen-IL plan remains five transforms at
`5791ac3b27f500d8ae9f2cc72669b09aa7e490d17b33b992a2b55d0a8728b457`,
and the existing custom-audio class remains one bank/two events.

Three independent generations were identical:

| Boundary | SHA-256 / count |
|---|---|
| Shared closure | `0b4edf7adef5b678ab829a955ef015e9a3ec0259ff4caa0112b9fb55b7f333b1` |
| Managed tree | 42 files; `4db0a0c085431e71281877128675a3a58dafeffc77a8eb06a6eb090fdc38cf40` |
| Content tree | 650 files; `3f62845066819fdf699a34d6207ed485dfc6f6062ca10660739aaadc3d328184` |
| Static registry | `d7ff46c8ff66d0e5928723494ac65f4b08bfb23c110b621caeb30c2261b65ad1` |
| Hook transform | `3ce99ab4365bf83a91e9b8c741a0a5d2b4a9af72582083f021869d80d53a17e4` |
| Apple API surface | 30 members; `d594dea86e944b468819818773f2177312445bbebe022bcbc7ea1ff779c8652c` |
| Progression manifest | `80bf30085d2c840d98b949f743960f656afbefc442eb6053daa96a64bcf098fb` |
| Custom-audio manifest | `0a7ca20a24239f4829ab5af7002004309d38d89ac5bffbfefff6a49b278c8841` |

LittleEpic's map hash remains
`6de18b4280df15493a9f1b0eac2b547118757cf276795146bdbf299e9c371475`
and its compatibility identity remains byte-identical at
`a341e6cbc2ff916c81b2717f24560b50bbb65b0a4326fb0ea31ed285841dc259`.
Its custom bank remains
`2607c358e66f4bce4e81fa3748eaffe1205dcfb57010be2e90ce40fabeb8d3ec`.

## Multi-map progression authority

Stage 25J-A originally selected only a snapshot for which every map was
currently installed. Stage 25J-B generalizes selection and projection without
weakening identity checks:

- a snapshot is selectable only when at least one installed SID has the exact
  compatibility identity;
- only exact installed records project into live `SaveData`;
- absent-map records remain quarantined in the independently validated A/B
  snapshot when another exact map is saved;
- exact re-addition restores the absent record;
- changed content at the same SID replaces its record and cannot consume the
  old identity;
- runtime Area index changes are irrelevant because ownership is SID plus
  original map SHA-256;
- an absent custom Session is retained only while its map is absent; and
- a changed or imported vanilla base, slot lineage change, delete/recreate, or
  identity mismatch still rejects stale progression.

The A/B corruption unit remains one complete progression snapshot, so a corrupt
newest replica falls back to the previous valid generation. Slot 0–2 isolation,
delete/recreate removal of both maps, and imported-save lineage protection were
covered deterministically. The measured two-map fixture is 1,330 raw bytes and
461 bytes in the tvOS compressed representation, inside the unchanged 761,856
byte complete three-slot A/B budget.

Persisted objects remain the J-A typed `AreaStats`/`AreaModeStats` state and one
resumable custom Session: collectibles/checkpoints/completion, deaths/time/best
statistics, SID/room/respawn/inventory/environment/audio/flags/collections, and
the exact old-stat baseline required for delta accounting. Module SaveData and
module Session remain the separate Stage 25F lane.

## Full-AOT products

Both products use the same pre-platform closure and are Release, fully trimmed,
fully AOT compiled, `UseInterpreter=false`, and JIT-free.

| Product | Bytes | SHA-256 | Result |
|---|---:|---|---|
| Signed universal iOS/iPadOS IPA | 884,404,441 | `fb88917a2aa8ac70842e1e5070df09f6fdb8556467022291e2ee78eb476d7c85` | same-identity development-signed physical product |
| Signing-ready unsigned tvOS IPA | 898,348,533 | `9171cdca98daa6515eb708eb2116c21660026b145c2f97cf1ff52e003289cd19` | full static/package/AOT acceptance; physical pending |

The accepted native locks are unchanged: iOS
`9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`
and tvOS
`6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`.
Canonical Content/raw/patched/Stage-6 locks remain respectively
`30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46`,
`db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273`,
`0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5`,
and `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9`.
The vanilla iOS generated-tree lock remains
`2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357`.

## Physical acceptance

The same signed universal IPA was installed without uninstalling on an iPhone
12 Pro Max running iOS 26.1 and an iPad mini 4 running iPadOS 15.8.8.
Both devices passed:

- existing numbered save and Stage 25J-A LittleEpic progression restoration;
- LittleEpic Save and Quit and helper/audio behavior, including the horn,
  Chrono gate and DJ frozen-IL behavior;
- Fear of the Dark room traversal and real persistent-falling-block behavior;
- death/respawn, pause/resume, and Fear Save and Quit;
- app-switcher termination and Fear cold resume;
- LittleEpic → Fear → LittleEpic/Fear switching with independent state;
- touch, controller, audio, rotation, and background/foreground.

The persisted Fear action was room progression plus ChronoHelper persistent
falling-block interaction followed by Save and Quit; cold launch restored the
Fear Session while the older LittleEpic record remained intact. Startup, map
selection/load, save, cold resume, and map switching showed no practical stall
or regression during focused acceptance. No formal benchmark was introduced.

Apple TV was deliberately not physically tested because the hardware was
unavailable. The tvOS product and deterministic UserDefaults A/B two-map tests
passed, but this is not a substitute for physical evidence.

## Regression and historical-verifier treatment

The current tree passed 382 AppleEverestBuilder deterministic tests, Stage
25J-A (81), Stage 25I-B (75), Stage 25H-D (163), Stage 25H-C (120; its accepted
YELLOW/fail-closed scope), Stage 22B Save Manager continuity (85), 56 input
profile checks, and the repository/privacy/link verifier. Current functional
HookGen, direct-hook, ModInterop, IL-freeze and IL-composition probes also pass.

Stages 25D, 25E, 25F-A, 25F-B, 25F-B2, 25H-A and 25H-B intentionally lock their
historical transformer/catalogue breadth. Their current-tree wrappers therefore
fail closed when they see the later v11/102-target closure. They were not
weakened: their immutable verifiers passed at their accepted commits with 144,
130, 143, 142, 92, 190 and 144 checks respectively. The Stage 24E2 vanilla iOS
release verifier likewise passed 89 checks at its accepted RC commit, while the
current J-B verifier and product scans own the modern static closure. No GitHub
Actions workflow was run; Actions usage is zero.

## Future Apple TV catch-up

The cumulative run must install this exact J-B product as a same-identity
replacement, verify prior J-A LittleEpic state, launch Fear of the Dark,
exercise the persistent falling block, Save and Quit, cold-resume Fear, switch
between both maps, test slot delete/recreate, verify the existing custom bank
loads once, then check Save Manager/soft reload and Home/reopen/graceful Quit.

## Recommendation

Stage 25J-B proves that medium standalone maps are now close when they fit the
closed mechanism census: two ordinary maps, different LevelSets, shared helpers
and independent progression coexist without a parallel pipeline. A small real
multi-map same-LevelSet package is the highest-value next target because it
would test LevelSet aggregation and intra-package map selection rather than
repeating independent-root evidence.

A small collab is closer than before but still needs its lobby/UI/content graph
audited. Configured-IL-heavy maps still need deterministic configured ordering;
broader custom audio still needs more bank/event shapes; general DynamicData,
Lua and native payloads remain unsupported. Strawberry Jam is not yet a
rational target until those classes and collab-scale content are bounded.
