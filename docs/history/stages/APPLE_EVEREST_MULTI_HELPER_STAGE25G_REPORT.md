# Stage 25G — first real multi-helper Everest map composition audit

Status: **PASS — YELLOW**

Date: 2026-08-21

Stage 25G establishes a clear next compatibility wall without weakening the
Apple static-AOT policy. Fifty-three real maps were screened, 24 plausible
multi-helper graphs were deeply audited, and 99 helper packages were inspected.
No graph reached the zero-blocker pre-AOT gate. The strongest candidate,
Noctambule, genuinely uses two code helpers and avoids active IL, native code,
and custom audio, but its real completion mechanic executes Lua. The installed
Apple profile intentionally contains neither NLua nor KeraLua.

The correct result is therefore YELLOW. No helper was source-edited, no map
mechanic was removed, no interpreter/JIT was introduced, and no blocked product
was built merely to obtain a first frame.

## Baseline and immutable references

| Item | Value/result |
| --- | --- |
| Start commit | `8ed17c42ee3bd6f00fde8516d1ac8da833cf3f3e` |
| Feature branch | `feature/apple-everest-multi-helper-graph` |
| Final commit | The commit containing this report; the final response records the exact full SHA |
| Everest | stable-1.6458.0 / `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00` |
| MonoMod | `dfc30a1506d37fb88a2c2be004f525205f46a24c` |
| Historical Stage 25F-B YELLOW | `6cde1ad9ba93bbd2f57f2867baf3b189b2824b1b` (untouched) |
| iOS recovery tag | `27e16b4724d94d3991b99c4795f680fcb0e5830c` |
| tvOS RC1 | `ee52b0868df091746f134d95d4f020f94f23d4fb` |
| tvOS RC2 | `641e86e4ed164cdf93f602ce2f11436449654d6e` |
| RC3 release branch | `c8134c8ca7924cf12f48527e714b5242c6024927` |
| RC3 tag | Absent |
| GitHub Actions | Not run; zero Actions minutes used |

The feature branch contains only the candidate audit, verifier, current
architecture/compatibility documentation, and this historical report. There is
no product, builder, runtime, generated-game, native, or profile change.
No GitHub Actions workflow ran.

## Public metadata snapshot

All external databases and downloaded packages were retained only below ignored
local build storage. No full public database or third-party package is tracked.

| Source | Retrieval date | SHA-256 | Entries |
| --- | --- | --- | ---: |
| `https://maddie480.ovh/celeste/mod_dependency_graph.yaml` | 2026-08-21 | `ecec2aef46d38525e9c7064129b98ad19ea02743425cec01ed34d6feb3cbf13e` | 6,056 |
| `https://maddie480.ovh/celeste/mod_search_database.yaml` | 2026-08-21 | `eaf55346084b60bb97e76cc07ae8f874a6434117e1197ca4662d87532cd109d9` | 6,031 |
| `https://maddie480.ovh/celeste/everest_update.yaml` | 2026-08-21 | `b16438e92cbb33eb3e6f2697a09dd4123530afee5759309f05267ffbaea5b602` | — |
| `https://maddie480.ovh/celeste/mod_files_database.zip` | 2026-08-21 | `efa254e5a869838647d8dd9d3f9ed8aaba1df151bf1e9acb4b0509b32f9b0989` | — |

Joining dependency and search metadata produced 4,739 real map entries. Of
those, 865 had two to four non-Everest dependencies before actual-use and
compatibility filtering.

## Candidate audit

The required initial 30-map cohort was:

1. NightClimb
2. HappyNationalAviationDay
3. 433
4. obby
5. TheSpire
6. MountBaker
7. void
8. Astraeus
9. LittleEpic's Precision Challenge
10. CaveofEntropyShowcase
11. Wholesome Theo Gameplay
12. IntroCar
13. fountain
14. Celess
15. madeline's adventure for a blue slushie
16. QLetterAurora
17. TeraBlast
18. SnowRidge
19. Partnership
20. Mineclimber
21. FrogelineSummit
22. UncycledMadness
23. 7X1D
24. Etna
25. Ricochet
26. idoremember
27. RockClimbing
28. Sunset
29. GLACEIR
30. The Fall

A supplemental screen brought the real-map total to 53. The 24 deep graph
roots were NightClimb, obby, MountBaker, void, Astraeus, LittleEpic's Precision
Challenge, Wholesome Theo Gameplay, QLetterAurora, TeraBlast, Mineclimber,
GLACEIR, The Fall, rainbowtest, Lightning Strike, Coffee Pot Crystal Heaven,
StupidLava, FUNNIEST MAP NAME EVER, Blue Ice, Sciorda, sillymap1, Space Trip,
8bb1b, Noctambule, and UnfortunateWaveCave.

Each real map binary was parsed rather than trusting `everest.yaml`. Entity,
trigger, backdrop, map metadata, assets, and audio use were matched to helper
registrations. This found several declared-but-unused helpers—for example Cpop
Helper in HappyNationalAviationDay, 433, Celess, and QLetterAurora, and
StyleMaskHelper in rainbowtest. Those packages could not qualify by declaration
alone.

### Scoring

The reproducible score rewarded two demonstrably active helpers, a small graph,
ordinary public ZIPs, modest map size, source-auditable/permissive helpers,
custom entity/trigger/backdrop evidence, and reuse of existing static classes.
It penalized active IL/ILHook, Lua/native code, custom banks, unused declared
dependencies, broad hook/public API surfaces, and progression-heavy content.

| Rank | Candidate | Strength | Decisive blocker |
| ---: | --- | --- | --- |
| 1 | **Noctambule** | Exactly two map-used helpers; no active IL, ILHook, native code, or custom bank | Real completion trigger executes Lua through NLua |
| 2 | UnfortunateWaveCave | Actual AurorasHelper and ShroomHelper behavior | Root ships a 7,312,640-byte custom FMOD bank; broader helper gaps remain |
| 3 | NightClimb | Real MaxHelpingHand custom backdrop plus ContortHelper trigger | Current MaxHelpingHand uses active IL/RuntimeDetour and a large API surface; Contort has uncatalogued hooks |
| 4 | FUNNIEST MAP NAME EVER | Two actual helpers | HonlyHelper and SorbetHelper bring custom banks and broad IL/dynamic behavior |

SteamAchievementPack resolved entirely to the already-accepted
AchievementHelper/ConditionHelper pair but is an achievement data pack, not a
real map; it therefore did not satisfy the root-map contract. Candidate graphs
using only already-green helpers otherwise appeared in large collabs rather
than a suitable modest standalone map.

## Measured compatibility-wall distribution

The following are exclusive **primary** classifications across the 24 deep
graphs. Secondary blockers overlap (for example, some IL graphs also have
custom audio or broad APIs).

| Primary wall | Graphs | Share |
| --- | ---: | ---: |
| Active `IL.*` / IL-centered helper | 18 | 75% |
| Mod-supplied custom audio | 3 | 12.5% |
| Lua/native | 1 Lua / 0 native | 4.2% |
| API/content/actual-usage breadth | 2 | 8.3% |
| Zero-blocker current non-IL profile | 0 | 0% |

This makes deterministic IL feasibility the highest-payoff next investigation.
It does **not** justify adding an interpreter or stripping manipulators.
Custom-audio work ranks second; broad Lua support is not the rational immediate
investment from this sample.

## Strongest graph: Noctambule

### Root package

| Field | Exact value |
| --- | --- |
| Name/version | Noctambule 0.0.1 |
| Creator / GameBanana | exopta / mod 561341 |
| Public ZIP | `https://gamebanana.com/mmdl/1541978` |
| ZIP SHA-256 | `cc48ce4800bea0663ebe80f2a694fc1f9a1fdf503d62fc906f5fef98b3b1a4d6` |
| Map | `Maps/exopta/noctambule.bin`, 9,688 bytes |
| Map SHA-256 | `97d0937ee630bb563d54119a8a9b4bc97a832b9a7200adebb06dc14e977022e1` |
| Reachable script | `Assets/exopta/Noctambule/end_point.lua`, 401 bytes |
| Script SHA-256 | `6a0fd0ec87d1b41088d140c946257eb5387d4d3c0635fbe7732cb27db61f6fc8` |
| License | No explicit redistribution license located; bytes remain external/ignored |

The one real map contains both required helper IDs. The Lua script is a real
completion cutscene: it controls player movement/animation and camera timing,
then completes the area. Its mechanics cannot be removed while calling the map
compatible.

### Complete dependency graph and load order

Noctambule directly requires Everest ≥ 1.5105.0, LuaCutscenes ≥ 0.2.12, and
ShroomHelper ≥ 1.2.8. The pinned database resolves these against the fixed host
profile to:

```text
Everest stable-1.6458.0
├── LuaCutscenes 0.2.13
├── ShroomHelper 1.2.10
└── Noctambule 0.0.1
```

Both helpers are direct; there is no non-Everest transitive helper. Stable
topological order is Everest, LuaCutscenes, ShroomHelper, Noctambule. The
production resolver's existing missing dependency, wrong-version, cycle, and
conflict checks remain unchanged and fail before AOT. No hand ordering or
optional-provider promotion was introduced.

### Authoritative helper inputs

| Helper | ZIP / DLL | Source audit | License |
| --- | --- | --- | --- |
| LuaCutscenes 0.2.13 | `https://gamebanana.com/mmdl/1305773`; ZIP `c57913d16596b129275a5dc288bc44fc31a9a67d6f47d7e855efe479dacaaac8`; DLL `dc697f1adfaa18bdb219df0a3ee569e1a785a452f17c46cd7626a8730440eb57` | `https://github.com/Cruor/LuaCutscenes`, commit `356dcfb7b0280c1381ed6d47b09a77e44d87bcc3` | MIT |
| ShroomHelper 1.2.10 | `https://gamebanana.com/mmdl/1361856`; ZIP `6a2c3eacc68353c0f8bb69bb9b59ca62128e6d3f54ff38a8ff1f282f0cdd97d7`; DLL `2428be4659522324b4b426452b17a095a78b857048d6340a0c4720151c08fa1d` | `https://github.com/CommunalHelper/ShroomHelper`, tag/commit `v1.2.10` / `771918ac8e04cca0db56f4cc1b3f96b235678d68` | MIT |

The ordinary release ZIPs were authoritative. Source was audit evidence only.
Because the graph failed before freezing, there are intentionally no transformed
DLL/frozen hashes and no source-free closure hash. Reporting fabricated hashes
would imply an acceptance that did not occur.

## Actual helper-use contract

| Map object | Provider | Real behavior |
| --- | --- | --- |
| `ShroomHelper/GradualChangeColorGradeTrigger` | ShroomHelper | Gradually changes the selected map color grade |
| `luaCutscenes/luaCutsceneTrigger` | LuaCutscenes | Resolves and runs `end_point.lua`, the map's completion cutscene |

No helper in this selected root is declaration-only. Both are new to the
accepted Apple code-helper closure, but neither is accepted by this YELLOW
stage.

## Complete selected-graph blocker census

### LuaCutscenes

- managed reference to NLua 1.4.25.0;
- real `luaCutscenes/luaCutsceneTrigger` registration;
- map-level script lookup and execution;
- no custom FMOD bank, native library, or P/Invoke found;
- no active `IL.*`/`ILHook` found in the selected package.

NLua/KeraLua are provenance-only in the pinned profile and are explicitly
excluded from the device closure. Supporting this behavior would require a
separate bounded static-Lua design or an interpreter, and the latter is
forbidden by the full-AOT product contract.

### ShroomHelper

- `MonoMod.Utils.DynamicData` use;
- one already-catalogued `Player.RefillDash` HookGen target;
- five uncatalogued targets: `DashSwitch.OnDashed`, `Player.RedBoost`,
  `Player.StartDash`, `Player.UseRefill`, and `Session.Restart`;
- custom trigger registration for the map-used color-grade trigger;
- no active `IL.*`, `ILHook`, direct Hook, native/P/Invoke, or custom bank.

Those HookGen/DynamicData gaps look manageable but nontrivial. They were not
implemented because closing them cannot make this Lua-dependent map GREEN.
No helper-name-specific production bypass was added.

### Other mechanism classes

| Class | Selected graph result |
| --- | --- |
| Active `IL.*` / `ILHook` | Absent |
| Direct managed Hook | Absent |
| ModInterop | Absent; no cross-helper calls |
| Settings / ButtonBinding | No required settings graph |
| Module SaveData / Session | None |
| Custom entity registry | No helper-owned entity used |
| Custom trigger registry | Two map-used IDs inventoried; no production registry emitted because pre-AOT blocked |
| Custom backdrop | None selected; no real-backdrop claim |
| Publicized API additions | Zero |
| Custom FMOD/audio | No custom bank or custom event requirement |
| Native/PInvoke | Absent |
| Content | Map, dialog, GUI/end-screen/color-grade assets, and one Lua script; Lua is unsupported |
| Content collision/precedence | No selected collision found; no closure emitted |
| Runtime update/hot reload | Not requested and remains unsupported |

Every blocker is classified. The pre-AOT diagnostic is:

```text
LUA_UNSUPPORTED: reachable Lua cutscene requires NLua/KeraLua, which are
intentionally absent from the source-free full-AOT Apple runtime.
```

## Build, closure, and physical gates

The zero-blocker gate failed before the expensive work. Therefore:

- source-free closure generation: **not run**;
- managed/content/registry/shared hashes: **not produced**;
- Release iOS/tvOS product builds and durations: **not run**;
- IPA sizes/hashes: **not produced**;
- full-AOT/trim/LLVM package coverage: **not run for this graph**;
- iPhone, iPadOS 15.8.8, and Apple TV map acceptance: **not run**;
- helper-A/helper-B physical behavior: **not claimed**;
- performance/memory observation: **not applicable**;
- clean-clone external graph regeneration: **not applicable to a blocked product**.

This is the intended cost-control behavior of the complete blocker census, not
an omitted release gate. Existing accepted AppleEverest products and their
physical evidence remain historical; this branch does not alter them.

## Regression and lock preservation

The Stage 25G verifier locks the current profile, 51-target signature-driven
catalog, Stage 25F-B2 ModInterop plan, release refs, documentation, and the
absence of third-party archives. The applicable existing fast suites were run
without product inputs:

| Deterministic gate | Result |
| --- | --- |
| Stage 25G audit/verifier | PASS — 93 checks |
| AppleEverestBuilder | PASS — 226 tests |
| Production typed HookGen semantics | PASS — 48 tests |
| Pinned desktop ModInterop reference | PASS — 14 checks |
| Desktop HookGen/direct Hook chains | PASS — both-owner, one-owner, priority and lifecycle probes |
| Pinned/real MonoMod IL-freeze regression | PASS — result 10 and result 15 probes |
| Current Stage 25F-B2 invariants | PASS — 94 checks |
| Current tvOS Stage 9B–14 chain | PASS — soft reload 21, Quit 16, prompts 38, Save Manager 66, plus source verifiers |
| Documentation links | PASS — 181 links |
| Repository documentation/privacy/isolation | PASS |

The tracked Stage 25F-B2 verifier intentionally pins the historical
pre-integration `tvos-port` SHA. Its substantive checks passed, then its old
branch assertion rejected today's later integration as designed. A read-only
in-memory evaluation changed only that expected integration SHA and passed all
94 checks; the historical verifier itself remains byte-identical. Likewise,
the older Stage 25D/E/F/F-B scripts deliberately pin transformer versions
v3/v4/v5/v6 and are not rewritten to accept current v7. The current B2 and 25G
verifiers own the present-state invariants.

The Stage 24E2 historical verifier similarly asserts that no tvOS/native source
changed after its own old baseline; later accepted tvOS/Everest work makes that
historical assertion intentionally inapplicable. Stage 25G independently locks
today's iOS, tvOS, canonical, and native values. No product code changed, so no
physical matrix or full product build was repeated.

| Lock | Value |
| --- | --- |
| Vanilla iOS tree | `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357` |
| Canonical Content | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Stage-6 real-audio tree | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| iOS native | `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2` |
| tvOS native | `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc` |
| Stage 25F-B2 ModInterop plan | `9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318` |
| HookGen catalog | v2, 51 targets; zero additions |

B2 ModInterop, Stage 25F-A durability, Stage 25E helper functionality, and
Stage 25D detours retain their accepted implementation and tests. Normal iOS
and tvOS builders remain mod-free. Custom-map progression remains deliberately
deferred, and Save and Quit remains suppressed in the nonpersistent static-map
lane.

## Storage, licensing, and privacy

External metadata, all 53 map packages, 99 helper packages, extracted maps,
DLLs, source checkouts, and detailed local manifests remain under ignored build
storage. The tracked audit records only privacy-safe names, public URLs,
versions, hashes, compatibility decisions, and license findings. It contains no
Celeste/FMOD bytes, third-party ZIP/DLL/map bytes, saves, device IDs, signing
values, credentials, private paths, analytics, or telemetry. No runtime network
service was introduced.

No explicit redistribution license was found for Noctambule, so even a future
accepted build must continue to require the user-provided/public package rather
than redistributing it. Both helper source repositories are MIT, but ordinary
release ZIPs remain the authoritative build inputs and are not tracked.

## Decision and next stage

Stage 25G is **not integration-ready as multi-helper product support**. It is
integration-ready only as a truthful compatibility audit/verifier record on its
feature branch. The strongest rational follow-up is a diagnostic stage for
deterministic build-time lowering of a narrowly selected real `IL.*` shape,
because active IL was the primary rejection in 18 of 24 deep graphs. It should
not begin by promising generic IL compatibility.

No manual download is needed before designing that diagnostic stage; candidate
and helper selection should again use public metadata and ordinary releases.
Noctambule is a useful future Lua fixture. Custom FMOD is the second-ranked
compatibility class by measured rejection count. A medium standalone map now
looks one focused compatibility class away when a clean graph can be found; a
small collab still needs broader helper/API/content and progression coverage.
Strawberry Jam remains premature because it compounds large graph breadth,
active IL/ILHook, publicized APIs, custom audio/content virtualization,
progression, and Lua/native policies.

## Explicit acceptance answers

1. **Can the builder package a qualifying real multi-code-helper map now?** No; no audited graph cleared the pre-AOT gate.
2. **Selected map?** Noctambule 0.0.1, as the strongest YELLOW candidate.
3. **Code helpers?** Two.
4. **Direct/transitive?** LuaCutscenes and ShroomHelper are both direct; no non-Everest transitive helper.
5. **Are both actually used?** Yes, proven from real map IDs.
6. **Helper A proof?** `luaCutscenes/luaCutsceneTrigger` executes the packaged completion script.
7. **Helper B proof?** `ShroomHelper/GradualChangeColorGradeTrigger` changes color grading.
8. **Unused declared helpers in selected graph?** None; other screened maps did have unused declarations.
9. **New accepted helper?** Both helpers are new candidates, but neither is accepted because the stage is YELLOW.
10. **Authoritative inputs?** Ordinary public release ZIPs.
11. **Helper source needed for final generation?** No final generation occurred; source was audit-only.
12. **Missing dependency fails before AOT?** Existing resolver policy remains yes.
13. **Wrong version fails before AOT?** Existing resolver policy remains yes.
14. **Complete census before build?** Yes; it stopped the build.
15. **Active IL?** No.
16. **ILHook?** No.
17. **Lua?** Yes, reachable and decisive.
18. **Native/PInvoke?** No.
19. **Custom FMOD/audio?** No.
20. **IL-primary rejections?** 18 of 24.
21. **Audio-primary rejections?** 3 of 24.
22. **Lua/native-primary rejections?** 1 Lua, 0 native.
23. **API/content/usage-primary rejections?** 2 of 24.
24. **New HookGen targets?** Zero; Shroom's five gaps were inventoried but deliberately not added.
25. **Still signature-driven?** Yes; the unchanged catalog remains signature-driven.
26. **Direct Hook?** No selected helper used one.
27. **ModInterop?** No selected helper used it.
28. **Physical cross-helper call?** Not applicable.
29. **Entities merged?** No production registry was emitted; the selected usage is trigger-only.
30. **Triggers merged?** Two IDs were inventoried, but production merge was correctly blocked.
31. **Real custom backdrop?** None selected; no claim.
32. **Duplicate IDs rejected?** Existing deterministic production rule remains unchanged; none occurred here.
33. **Load order?** Deterministic topological order was resolved.
34. **Content precedence?** No collision occurred; existing deterministic policy remains unchanged.
35. **New publicized members?** Zero.
36. **Module Settings?** None required.
37. **Module SaveData?** None required.
38. **Session?** None required.
39. **Custom-map progression?** Still deliberately deferred.
40. **Save and Quit safety?** It remains suppressed in the nonpersistent lane.
41. **Normal real-map load?** Actual map bytes were parsed for usage, but not launched because pre-AOT failed.
42. **Helper A on iPhone?** Not run and not claimed.
43. **Helper B on iPhone?** Not run and not claimed.
44. **Both on iPadOS 15.8.8?** Not run.
45. **Both on Apple TV?** Not run.
46. **Touch/controller/audio/lifecycle?** Unchanged product; no new physical claim.
47. **One shared closure?** Architecture remains one shared closure, but none was produced for this blocked graph.
48. **Shared closure SHA-256?** None.
49. **New helper AOT coverage?** Not reached.
50. **Full trim/AOT/no interpreter/no JIT?** Required policy unchanged; not run for this graph.
51. **Forbidden runtime mechanisms absent?** Existing products remain unchanged; NLua/KeraLua were not added.
52. **B2 ModInterop?** Implementation/plan unchanged and deterministic regression retained.
53. **Stage 25F-A durability?** Unchanged and regression retained.
54. **Stage 25E functionality?** Unchanged and regression retained.
55. **Stage 25D detours?** Unchanged and regression retained.
56. **Vanilla iOS?** Unchanged.
57. **Vanilla tvOS?** Unchanged.
58. **Canonical/native locks?** Unchanged as listed above.
59. **Release refs?** Untouched.
60. **Actions minutes?** Zero.
61. **Stage 25G integration-ready?** Not as product compatibility; only as YELLOW evidence.
62. **Fast-forward SHA?** None should be fast-forwarded for product support; the final feature SHA identifies the audit branch only.
63. **Next class?** Narrow deterministic IL-lowering feasibility.
64. **Is IL highest-value?** Yes, from 18/24 primary rejections.
65. **Greater payoff if not IL?** Not applicable; custom audio is second.
66. **Manual download before next stage?** No.
67. **Medium standalone map distance?** Likely one focused class for a carefully selected graph, not a compatibility promise.
68. **Small collab distance?** Further: graph breadth, APIs/content, audio, and progression remain.
69. **Before an IL-heavy helper?** Prove one deterministic, bounded IL shape and its complete hook/API closure without runtime IL.
70. **Before Strawberry Jam?** Multiple IL shapes, larger helper graphs, custom audio/content, broader APIs, progression, and Lua/native policy need evidence first.

No merge, tag, force-push, upstream push, or GitHub Actions run is part of this
stage.
