# Stage 25I-A — LittleEpic graph completion and custom-audio boundary

Status: **PASS — YELLOW**

Date: 2026-08-22

Stage 25I-A reacquired and completely audited the exact historical
LittleEpic's Precision Challenge 1.0.0 graph. Both declared code helpers are
genuinely used by the map. DJMapHelper's previously blocking IL and ordinary
HookGen breadth can be transformed deterministically and source-free, but
ChronoHelper 1.3.3 unexpectedly contains a custom FMOD bank. The accepted
shared Apple runtime has no bounded mod-bank registration/lifecycle service.

The pre-AOT result is therefore deliberately YELLOW:

```text
CUSTOM_AUDIO_UNSUPPORTED
```

No complete graph closure, IPA, or physical product was accepted. The bank was
not stripped, ignored, or staged inertly, and ChronoHelper was not reduced to
only the entity used by this map. That would violate the no-partial-helper
contract and make a misleading first multi-helper claim.

## Baseline and immutable references

| Item | Value/result |
| --- | --- |
| Starting integration commit | `236d5fc957fb02d283a29ae4f55ef9753be042c2` |
| Feature branch | `feature/apple-everest-littleepic-map` |
| Final feature commit | The commit containing this report; the handoff records its exact SHA |
| Everest | stable-1.6458.0 / `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00` |
| MonoMod | `dfc30a1506d37fb88a2c2be004f525205f46a24c` |
| iOS recovery tag | `27e16b4724d94d3991b99c4795f680fcb0e5830c` |
| tvOS RC1 / RC2 | `ee52b0868df091746f134d95d4f020f94f23d4fb` / `641e86e4ed164cdf93f602ce2f11436449654d6e` |
| RC3 release branch | `c8134c8ca7924cf12f48527e714b5242c6024927` |
| RC3 tag | Absent |
| GitHub Actions | Not run; zero Actions minutes used |

The Stage 25G/H-A/H-B/H-C/H-D audit and report files remain byte-identical.
The feature branch was not merged, tagged, or pushed upstream.

## Exact public graph

The build inputs remain ordinary public release ZIPs outside Git. No helper
source tree is required by the production transform.

| Package | Exact release | ZIP SHA-256 | Distributed DLL | Licence/provenance |
| --- | --- | --- | --- | --- |
| LittleEpic's Precision Challenge | 1.0.0; [GameBanana download](https://gamebanana.com/mmdl/1243114) | `ca57d295f446affdd8eb9b56ffe58f98cc652e9ec242ff07b224877d0067b384` | Content-only | LittleEpic; GameBanana package metadata says CC BY-NC-ND 4.0 |
| ChronoHelper | 1.3.3; [GameBanana download](https://gamebanana.com/mmdl/1778580) | `af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18` | `bin/Debug/net452/ChronoHelper.dll`; `214b26a5d7e3f17e93d3c388a3a6066dd137f9ce09b6203a4e1a482ba342a2dc` | ricky06; GameBanana package metadata says CC BY-NC-ND 4.0; no authoritative public source repository was located |
| DJMapHelper | 1.13.4; [GameBanana download](https://gamebanana.com/mmdl/1036311) | `95ab02d657213031b70c3079738b21be399e567ca8a32b1fe8e8effc0778eedb` | `DJMapHelper.dll`; `0d73202b5e16f26a4c8a286dd6ad600c1601d1904d8f72d909f8acb8469ee446` | DemoJameson; GameBanana package metadata says CC BY-NC-ND 4.0. Audit source tag `v1.13.4` is commit `693fbea3405090517ad66217b809673ff2725206`; that repository has no separate LICENSE file |

The root ZIP contains three files and expands to 13,708 bytes. ChronoHelper
contains 331 files / 1,273,757 bytes. DJMapHelper contains 398 files / 556,701
bytes. Extraction rejected absolute paths, traversal, links, duplicate
normalized paths, and excessive archive structure through the existing safe
ingestor.

The resolved graph is:

```text
Everest stable-1.6458.0
  ├─ ChronoHelper 1.3.3
  ├─ DJMapHelper 1.13.4
  └─ LittleEpic's Precision Challenge 1.0.0
       ├─ ChronoHelper >= 1.1.9 (direct)
       └─ DJMapHelper >= 1.11.1 (direct)
```

There are exactly two direct code helpers and zero transitive code helpers.
The declared distributed DLL in each helper remains authoritative. Incidental
Chrono source and alternate Debug/Release assembly copies are recorded but not
treated as competing production modules.

## Map identity and real helper use

| Field | Value |
| --- | --- |
| Map path | `Maps/LittleEpic/precisionchallenge/precisionchallenge.bin` |
| Original map SHA-256 | `6de18b4280df15493a9f1b0eac2b547118757cf276795146bdbf299e9c371475` |
| Original bytes | 13,352 |
| SID | `LittleEpic/precisionchallenge/precisionchallenge` |
| LevelSet | `LittleEpic` |
| Rooms | `1`, `2`, `3`, `4`, `5`, `6`, `7`, `heart` |
| Room count | 8 |
| Start | Room `1` |
| Starting spawn | Room `1`, `(160, 176)`, entity ID `0` |

Both helper relationships are real rather than metadata-only:

- room `1` contains one `DJMapHelper/maxDashesTrigger`, 48×32, configured
  `dashes=Zero`. Its static factory is
  `Celeste.Mod.DJMapHelper.Triggers.MaxDashesTrigger(EntityData, Vector2)`;
- room `5` contains one `ChronoHelper/CustomTimeSwitchGates`, configured
  `moveTime=17`. Its static factory is
  `Celeste.Mod.ChronoHelper.Entities.CustomTimeSwitchGates(EntityData, Vector2)`.

There are no custom backdrop IDs in the actual map. The map's other entities,
triggers, and progression are ordinary Celeste objects. Custom LevelSet
progression remains the existing nonpersistent debug lane, and Save and Quit
remains suppressed there.

## Complete helper census

ChronoHelper's authoritative binary registers 30 exact entity/trigger factory
IDs and 15 ordinary typed HookGen targets. DJMapHelper registers 28 exact
factory IDs and 26 ordinary typed targets. The Mac generator emits direct type
and constructor references; the device does not use `GetTypes`, `Activator`,
or a runtime assembly scan. The actual LittleEpic map needs one factory from
each helper, listed above.

ChronoHelper's 15 required target descriptors cover:

```text
Actor.MoveHExact, Actor.MoveVExact
DashBlock.Break(Vector2,Vector2,bool,bool)
Level.EnforceBounds, Level.LoadLevel
LevelLoader.LoadingThread
MapData.Reload
Player.CallDashEvents, Player.DashBegin, Player.DashEnd, Player.Die
Solid.MoveHExact, Solid.MoveVExact
Spikes(EntityData,Vector2,Directions)
Spring(EntityData,Vector2,Orientations)
```

DJMapHelper's 26 required target descriptors cover:

```text
CrystalStaticSpinner constructor and Removed
FinalBoss.OnPlayer and TriggerFallingBlocks
FlyFeather.OnPlayer and Respawn
HeartGem.Collect
Level.End
LevelLoader constructor
Player.Added, ClimbBoundsCheck, ClimbJump, constructor
Player.FinishFlingBird, IntroJump coroutine, Pickup
Player.RefillDash, RefillStamina, ReflectionFall coroutine
Player.StarFlyReturnToNormalHitbox, Update, UseRefill, WindMove
Refill.OnPlayer
Spring.OnCollide
TheoCrystal.Update
```

The shared exact signature catalog grows from 69 to 102 targets: 33 new unique
descriptors because eight required targets already existed. The descriptor IDs
are locked in
[`apple-everest/littleepic-graph-stage25ia.json`](../../../apple-everest/littleepic-graph-stage25ia.json).
They are signature-driven and fail on declaration/overload drift; no
helper-specific hand-written dispatcher was added.

Neither helper requires a direct `ILHook` or configured IL ordering. Neither
uses MonoMod ModInterop. Neither exposes module Settings or module SaveData.
Both expose a small default-YAML Session graph which fits the already accepted
Stage 25F-A typed path. There is no reachable Lua and no native/P/Invoke mod
payload.

## DJMapHelper ordinary IL freeze

The exact 1.13.4 binary registers five ordinary HookGen `IL.*` manipulators
over three target bodies:

| Plan | Target | Manipulator | EmitDelegate sites | Before → after | Diff |
| --- | --- | --- | ---: | --- | --- |
| `player-h-feather` | `Player.OnCollideH` | `FeatherBarrier.AddCollideCheck` | 10 | `71468bce…f2b9` → `f0659cb0…6ee4` | `4d9b7864…92e6` |
| `player-h-theo` | `Player.OnCollideH` after the first registration | `TheoCrystalBarrier.AddCollideCheck` | 10 | `f0659cb0…6ee4` → `a86a9eff…48a3` | `17dc28cf…4327` |
| `player-v-feather` | `Player.OnCollideV` | `FeatherBarrier.AddCollideCheck` | 15 | `40e68470…6271` → `d07b75bd…fb50` | `cc5f9e0b…d6f` |
| `player-v-theo` | `Player.OnCollideV` after the first registration | `TheoCrystalBarrier.AddCollideCheck` | 15 | `d07b75bd…fb50` → `0dd3e5e5…7e1` | `b44772e4…b1f` |
| `fling-awake` | `FlingBird.Awake` | `FlingBirdReversed.ModFlingBirdAwake` | 1 | `1e0337d0…c55c` → `1423bb5b…d92` | `a8c1cc2d…bb10` |

All 51 `EmitDelegate` sites are exact static/noncapturing method groups. The
actual distributed manipulators run in the pinned Mac worker. Each canonical,
intermediate, and final target fingerprint is locked. The exact combined plan
SHA-256 is
`5791ac3b27f500d8ae9f2cc72669b09aa7e490d17b33b992a2b55d0a8728b457`.
Omitting DJMapHelper emits no plan and leaves all canonical target bodies at
their exact baselines.

Three clean DJ-only source-free generations are byte-identical:

| Evidence | SHA-256 |
| --- | --- |
| DJ-only shared closure | `82418da0c90fe543ee4da4c9c7af58b372f96a2a11b4dda0e0c5dbb0bdf77639` |
| Byte-identical manifest | `879983c9b2b09056748b6a5e925d05600ad13c690072ad9d6d4e42b8e8f2ff6c` |

These are scoped DJMapHelper compatibility evidence, not a complete LittleEpic
closure.

## Bounded static compatibility surface

The two exact distributed DLL identities select closed compatibility plans.
Unknown versions/hashes retain the normal rejection. DJMapHelper's
`FastReflection` DynamicMethod implementation is replaced on the Mac by typed
field getters. The exact live `DynData<T>` uses become generated ordinary
field access plus bounded extra-data storage; this does not add a general
reflection/DynamicData implementation.

The existing reviewed Apple API catalog remains 30 members with SHA-256
`d594dea86e944b468819818773f2177312445bbebe022bcbc7ea1ff779c8652c`.
Stage 25I-A adds 27 exact declaration-locked internal source accesses needed by
these helpers:

```text
Strawberry: rotateWiggler, flyingAway, flapSpeed, Winged, collected
LightningRenderer: list
DashBlock: canDash
Player: dashCooldownTimer, boostTarget, starFlyColor, starFlyTimer,
        beforeDashSpeed, varJumpSpeed, flingBird, forceMoveX, boostRed
FlyFeather: shielded
FinalBoss: normalHair, nodes, patternIndex, attackCoroutine
Refill: sprite, flash
CrystalStaticSpinner: color
Spring: sprite, BounceAnimate
Solid: riders
```

No broad publicizer or device reflection search was added. Custom entity types
are exposed only as required for their exact static factory references.

## Content and precedence

The diagnostic content census is:

| Owner | Files | Types |
| --- | ---: | --- |
| ChronoHelper | 280 | 276 PNG, one ASE, one XML, one text GUID file, one FMOD bank |
| DJMapHelper | 337 | 173 PNG, 162 YAML sidecars, one XML, one text file |
| LittleEpic root | 2 | one map BIN, one dialog text file |
| **Total** | **619** | — |

The deterministic precedence is ChronoHelper, then DJMapHelper, then the root.
There are zero logical-path collisions in this graph. The existing static
content compiler/merger handles the ordinary graphics, sprite metadata,
dialog, and map inputs. The 619-file hash from an early diagnostic closure is
not accepted because that run mounted the unsupported bank without a loader.

## Exact custom-audio blocker

ChronoHelper contains:

| File/identity | SHA-256 / value |
| --- | --- |
| `Audio/ExpertContestHelper.bank` | `2607c358e66f4bce4e81fa3748eaffe1205dcfb57010be2e90ce40fabeb8d3ec` |
| `Audio/ExpertContestHelper.guids.txt` | `db7f44d7ee1d79eb5efe734e595a6fb99937fd78e5cf9af91b0db6cabc4272a6` |
| Bank identity | `bank:/ExpertContestHelper` |
| Events | `event:/ricky06/EC2023/horn`; `event:/ricky06/zip_mover 2` |
| Referencing helper types | `ExplodingPinata`; `EntityConveyor` |

The real `CustomTimeSwitchGates` instance used by LittleEpic uses ordinary
base-game touch-switch gate events, so the map's immediate room is not itself
asking for either custom event. ChronoHelper remains one installed module,
however, and its other registered/reachable helper features use the bank.
Shipping all factories but leaving those features silently without their audio
would be a partial-helper product. Removing the unused features would be a
source-edited special case rather than compatibility with the exact helper.

The analyser now records every `.bank` as `custom-fmod-bank:<path>` and rejects
the package as `CUSTOM_AUDIO_UNSUPPORTED` before closure/AOT. This is the one
required blocker and there are zero unclassified blockers.

The smallest follow-up is a bounded custom-FMOD class which:

1. inventories exact selected banks/guids on the Mac;
2. stages only graph-selected bank bytes;
3. registers and loads them on the existing single FMOD system before helper
   content can instantiate;
4. validates event identities and bank readiness;
5. defines background/reload/process lifecycle without a second FMOD system;
6. fails closed on collision, missing event, version drift, or load failure;
7. proves complete graph behavior physically on iPhone, iPadOS 15, and tvOS.

No custom-FMOD implementation was attempted in 25I-A because the stage
explicitly separates that architecture class.

## Product and physical gates

An early ignored diagnostic run generated a 619-content-file closure and both
iOS/tvOS managed trees compiled with zero C# errors. That run predated the bank
gate and is retained only as diagnostic evidence. It is not a source-free
accepted graph closure and none of its hashes are product locks.

| Gate | Result |
| --- | --- |
| Complete source-free shared closure | `NOT_RUN_PRE_AOT_CUSTOM_AUDIO_GATE` |
| Full trim / full AOT | `NOT_RUN_PRE_AOT_CUSTOM_AUDIO_GATE` |
| `UseInterpreter=false` / no JIT | `NOT_RUN_PRE_AOT_CUSTOM_AUDIO_GATE` |
| Forbidden final runtime scan | `NOT_RUN_PRE_AOT_CUSTOM_AUDIO_GATE` |
| iOS IPA | Not built |
| tvOS IPA | Not built |
| iPhone physical map/helper/IL behavior | Not run |
| iPadOS 15.8.8 physical map/helper/IL behavior | Not run |
| Apple TV physical map/helper/IL behavior | Not run |

Consequently Stage 25I-A is not the first real multi-code-helper map GREEN and
is not integration-ready. There is no SHA to fast-forward into `tvos-port`.
The feature branch preserves the reusable builder work and explicit gate for a
follow-up.

## Deterministic and regression verification

Current source verification completed:

- AppleEverestBuilder: 287 deterministic tests PASS;
- Stage 25H-D: 163 checks PASS at current source;
- Stage 25H-C: 120 checks PASS at current source;
- Stage 25H-B: 143 checks PASS at its accepted commit;
- Stage 25H-A: 190 checks PASS at its accepted commit;
- current IL freeze reference, IL composition, desktop HookGen/direct Hook,
  and desktop ModInterop reference tests PASS;
- Stage 25I-A: exact package/graph/freeze/custom-audio/verifier checks PASS;
- malformed/absolute/traversal/link/duplicate ZIP paths, missing and wrong
  helper versions, duplicate factory IDs, unknown hooks, custom banks, dynamic
  code, native code, Lua, and other forbidden mechanisms fail closed.

The older H-B/H-A suites are intentionally executed at their accepted commits
because their historical target-count invariants are 57 and earlier. They are
not weakened to accept the current 102-target catalog. B2/F-A/25E/25D behavior
remains exercised in the 287-test current builder suite; their tracked reports
and audit records remain unchanged.

The accepted vanilla and native locks remain:

| Boundary | SHA-256 |
| --- | --- |
| Canonical Content | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw source | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched source | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Stage-6 real-audio tree | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| iOS native | `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2` |
| tvOS native | `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc` |

No native dependency or ordinary Celeste product code changed.

## Privacy, licensing, storage, and recommendation

The three ZIPs, helper DLLs, map/content, bank, generated closure, decompiled
audit source, game data, FMOD data, IPAs, saves, signing values, device data,
and private paths remain ignored. Only public provenance, hashes, bounded
transform code, and aggregate evidence are tracked. No third-party code or
content is redistributed. Approximately 54 GiB was free before final cleanup;
the compact Stage 25I-A ignored evidence occupied about 20 MiB. No GitHub
Actions job ran.

Recommendation: implement the single custom-FMOD boundary next, then rebuild
this exact graph from fresh public ZIPs. Only after one identical shared
closure, full trim/AOT, and real Chrono/DJ/IL behavior pass on iPhone, iPadOS
15, and Apple TV should LittleEpic become GREEN. Configured-IL-heavy helpers,
large custom-audio ecosystems, durable custom LevelSet progression, and
Strawberry Jam remain later work; Strawberry Jam is still not a rational
experiment until these bounded classes and at least one real multi-helper map
are accepted.
