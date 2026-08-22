# Stage 25H-C — graph-driven IL closure

Status: **PASS — YELLOW**

Date: 2026-08-22

Stage 25H-C stops treating IL compatibility as an isolated mechanism ladder.
It reacquires and reclassifies all 18 exact Stage 25G IL-primary graphs, selects
one class from measured graph payoff, and productionises two modern ordinary
HookGen IL registrations from a real graph helper. The outcome is YELLOW:
Space Trip is materially closer because VortexHelper's ordinary IL events are
no longer blockers, but configured ordering, `DynamicData`, broad ordinary
hook/API/content work, and custom audio prevent a zero-overall product closure.

No full AOT build or physical install was started after that exact pre-AOT gate
failed. That is intentional: this stage does not implement a second unrelated
major mechanism or spend ten-minute build cycles to rediscover known blockers.

## Baseline and immutable references

| Item | Value/result |
| --- | --- |
| Starting commit | `8f9fbf64aadc4ff8853132cc35bf9e2e77378df8` |
| Feature branch | `feature/apple-everest-graph-il-closure` |
| Final commit | The commit containing this report; the handoff records its exact SHA |
| Historical Stage 25G | `9ffc1460d15bfe69074f55f6f3187d3cb2156365`, unchanged |
| Historical Stage 25F-B | `6cde1ad9ba93bbd2f57f2867baf3b189b2824b1b`, unchanged |
| Everest | stable-1.6458.0 / `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00` |
| MonoMod | `dfc30a1506d37fb88a2c2be004f525205f46a24c` |
| iOS recovery tag | `27e16b4724d94d3991b99c4795f680fcb0e5830c` |
| tvOS RC1 / RC2 | `ee52b0868df091746f134d95d4f020f94f23d4fb` / `641e86e4ed164cdf93f602ce2f11436449654d6e` |
| RC3 release branch | `c8134c8ca7924cf12f48527e714b5242c6024927` |
| RC3 tag | Absent |
| GitHub Actions | Not run; zero Actions minutes used |

The H-B baseline remains 57 managed HookGen descriptors, frozen-plan schema 2,
shared closure `51bc59ca…98f`, and ModInterop plan `9e755781…318`.

## Exact reacquisition and complete binary census

All 18 exact roots and their transitive helper releases were downloaded again
from Stage 25G URLs. Forty-four packages passed exact SHA-256 verification and
safe extraction; no historical fixture was inaccessible. The roots were:

`NightClimb`, `obby`, `MountBaker`, `void`, `Astraeus`, LittleEpic's Precision
Challenge, Wholesome Theo Gameplay, `QLetterAurora`, `TeraBlast`, `GLACEIR`,
The Fall, Lightning Strike, Coffee Pot Crystal Heaven, `StupidLava`, Blue Ice,
`sillymap1`, Space Trip, and `8bb1b`.

Twenty-six helper DLLs were inspected directly with Cecil. The compact tracked
audit records graph closure and classification; detailed instruction windows
remain ignored diagnostic evidence.

| Binary mechanism | Entire exact cohort | Mandatory high-priority union |
| --- | ---: | ---: |
| HookGen IL add/remove operations | 720 (360 additions) | 270 |
| Direct `ILHook` construction sites | 126 | 53 |
| Config-related hook sites | 91 | 50 |
| `EmitDelegate` sites | 471 | 226 |

Every direct construction was inventoried with constructor, target/manipulator
resolution, configuration and observed lifecycle calls. Every `DetourConfig`
or `DetourContext` site was kept separate. Stage 25H-C admits neither class.

The high-priority `EmitDelegate` taxonomy closes exactly:

| Class | Count |
| --- | ---: |
| A — static method | 161 |
| B — stateless compiler singleton | 52 |
| E — module singleton capture | 4 |
| H — transient manipulator-local capture | 2 |
| J — other/unknown | 7 |
| All other capture classes | 0 |
| **Total** | **226** |

Thus 213/226 sites are already in H-A/H-B's accepted lowering classes. The
four module captures, two host-local captures and seven unknown sites remain
explicitly deferred; no Mac closure object is serialized into a product.

## Graph distance

Distance is a vector, not an aesthetic score: major unsupported IL classes,
minor exact IL registrations, major unsupported non-IL classes, remaining
minor Hook/API/content breadth, and physical complexity.

| Graph | Major IL | Minor IL | Major non-IL | Other breadth | Physical |
| --- | ---: | ---: | ---: | --- | --- |
| LittleEpic's Precision Challenge | 1 | 5 | 0 | medium | medium |
| Lightning Strike | 1 | 0 | 1 | medium | medium |
| GLACEIR | 2 | 1 | 1 | medium | high |
| The Fall | 2 | 5 | 1 | medium | high |
| Space Trip, before → after | 2 → 1 | 2 → 0 | 2 | high | high |
| Coffee Pot Crystal Heaven | 3 | 6 | 0 | medium | medium |
| StupidLava | 3 | 8 | 0 | medium | medium |
| Blue Ice | 3 | 6 | 1 | medium | medium |
| 8bb1b | 3 | 8 | 1 | medium | high |
| NightClimb | 3 | 78 | 1 | high | high |
| obby | 3 | 90 | 1 | high | high |
| MountBaker | 3 | 83 | 1 | high | high |
| void | 3 | 78 | 1 | high | high |
| Astraeus | 3 | 87 | 1 | high | high |
| Wholesome Theo Gameplay | 3 | 148 | 1 | high | high |
| QLetterAurora | 3 | 107 | 1 | high | high |
| TeraBlast | 3 | 90 | 1 | high | high |
| sillymap1 | 3 | 61 | 1 | high | high |

LittleEpic remains the closest overall exact graph: DJMapHelper contributes five
ordinary event additions over `Player.OnCollideH`, `Player.OnCollideV`, and
`FlingBird.Awake`; its three emitted delegates are all static. Its remaining
non-IL work is ordinary `On.*`, API, entity, trigger and content breadth.

The graph-payoff decision nevertheless selected **Path A — ordinary HookGen IL
event breadth** and VortexHelper as the safest exact production proof. Path A
appears in 15/18 graphs and reuses the already accepted worker; Vortex's two
selected sites have zero unknown captures, zero direct `ILHook`, zero config,
and exact modern entity-local registrations. It therefore extends production
without inventing a second architecture. Space Trip is the exact graph it
materially advances. The machine audit preserves the distinction between
“closest graph” and “safest exact proof graph.”

## Real fixture and production transform

| Field | Exact value |
| --- | --- |
| Helper | VortexHelper 1.2.19 |
| Exact Stage 25G graph | Space Trip 1.0.0 |
| Public ZIP | `https://gamebanana.com/mmdl/1368600` |
| ZIP SHA-256 | `b6280fe2e3c05d355c32a51049854137c41be72d24ec4aec9d997cc7c4394db2` |
| Logical source SHA-256 | `c071d33bb1cc4f0387ea204834e212bd020f3143aea68c9f4e56b9bfa35def1b` |
| Distributed DLL | `Code/bin/VortexHelper.dll` / `f5a32f02c2699dcdf41bb1af808ead631491571c3d259429761953693bd37f73` |
| Source commit | `b37b67b9365d769ba0fd19a67a1327260988fd6e` |
| License | MIT / `051f92453f04ec0a8a9dff60882264ca949ea8e86de5f6aa96e04acdee90d359` |
| Build authority | Ordinary distributed DLL; source not required |
| Frozen plan | schema 2 / `96726af5fa21c129d1b1bd39746a1f6f903be38e2b53b20b79275eb05d5a3261` |

The exact entity-local registration contracts are:

1. `FloorBooster.Hooks.Hook/Unhook` adds/removes
   `IL.Celeste.Player.NormalUpdate` using static
   `Player_FrictionNormalUpdate`.
2. `PurpleBooster.Hooks.Hook/Unhook` adds/removes
   `IL.Celeste.Player.WallJumpCheck` using static
   `Player_WallJumpCheck`.

| Target | Before | After | Normalized diff |
| --- | --- | --- | --- |
| `System.Int32 Celeste.Player::NormalUpdate()` | `c3813704…e4b` | `9f34b6f8…991` | `c75c39af…62e` |
| `System.Boolean Celeste.Player::WallJumpCheck(System.Int32)` | `7c87ba1a…8cf0` | `6749ff27…162b` | `1cc8f904…05e` |

`NormalUpdate` lowers one ordinary static method call. `WallJumpCheck` contains
two compiler-singleton emit sites; one is copied as ordinary target IL and one
survives as the statically rooted `<>c.<Player_WallJumpCheck>b__3_1` call. The
final target bodies reference no Cecil, `MonoMod.Cil`, `ILContext`, `ILCursor`,
`ILHook`, DynamicReferenceManager, DynamicMethod or Reflection.Emit.

Three independent source-free executions produced identical output and
manifest bytes:

| Target | Output SHA-256 | Manifest SHA-256 |
| --- | --- | --- |
| `NormalUpdate` | `1c649a54c053100cc0ddd12888ffdf042b69ffbf53932ab67d66f8bde17d2ca6` | `55616d1bc22ee8d985a1f0fcc88f5ff9faee3eaf9116af88180ddee98b993d99` |
| `WallJumpCheck` | `3a387526cdec914c5bfcb4f4c84e50fade6895c4f01c3b149c7d7c595efa39e0` | `d852c44e6e5e48ce289ba7cb5659eb7dae0ebc815a25972eadfbc4c27e7d992f` |

The production device-assembly rewrite was also run twice against the exact
distributed DLL. Both runs produced
`641532a1d3d32a4c412ef816eb4541072d28bf942bd7bdd610fa300ac493cd17`,
removed the `Mono.Cecil` reference, and retained exactly four ordinary `On.*`
adds plus their four matching removes. `MonoMod.Utils` remains present solely
because VortexHelper has separate live `DynamicData` use; the product gate
therefore continues to reject the helper rather than hiding that blocker.

When VortexHelper is absent, no plan is emitted and both canonical target
bodies remain exact. When present, the device rewrite reconstructs its three
FloorBooster and one PurpleBooster ordinary `On.*` registrations, removes the
two exact IL subscriptions/manipulators, and roots the surviving targets.
Vortex's separate live `DynamicData` use remains an explicit blocker instead
of being disguised by removing the whole MonoMod.Utils assembly reference.

The only adjacent production fix broadens a metadata-resolution catch from
Cecil's `ResolutionException` to its exact `AssemblyResolutionException`
subclass. This permits exact ModInterop type-name matching when an unrelated
optional Celeste dependency is absent from the bounded host resolver; it does
not accept a new signature or compatibility class.

## Post-H-C graph status

| Result | Exact graph count |
| --- | ---: |
| Materially advanced | 1 — Space Trip |
| No remaining IL blocker | 0 |
| Zero overall blockers | 0 |
| Ready for physical Stage 25G2 | 0 |

Space Trip still requires LunaticHelper's configured `After={"*"}` ordering,
VortexHelper `DynamicData`/`DynData<T>`, 12 ordinary `On.*` targets, optional
GravityHelper ModInterop handling, custom helper/API/content work, and its
custom FMOD bank. Custom audio was not silently omitted. The graph is exactly
classified (zero unknown blockers), but it is not zero-overall.

## Product, AOT and physical result

No new shared closure, IPA, AOT object or physical installation exists for
H-C. iPhone, iPadOS 15.8.8 and Apple TV are **not run — pre-AOT gate**. This is
not a failed build; the specified zero-overall gate correctly prevented one.
Consequently no claim is made that VortexHelper or Space Trip currently runs
on an Apple product.

The H-A Dash Toggle frozen-DLL lock remains `79d06fd9…904f`; H-B sequence and
DisposableTheo, B2 ModInterop, F-A durability, 25E, and 25D retain their
deterministic/static regression contracts. The vanilla iOS generated-tree,
canonical and native locks remain:

| Boundary | SHA-256 |
| --- | --- |
| Vanilla iOS generated source | `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357` |
| Content | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Raw | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Patched | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Stage 6 | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| iOS native | `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2` |
| tvOS native | `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc` |

No third-party ZIP, DLL, map, source, game content or user data is tracked.
No Actions workflow was run. All historical Stage 25G/H-A/H-B reports and
audits remain byte-identical.

## Deterministic and regression verification

- Current AppleEverestBuilder suite: **267 pass**. This retains the current
  managed-detour, entity/content, module durability and ModInterop contracts.
- Typed HookGen semantic matrix: **48 pass**.
- H-A real pinned MonoMod freeze and H-B A/B/AB/ABC/neither composition:
  **pass**; current H-B verifier: **144 checks**.
- H-C exact graph, release, transform, fail-closed gate, lock and ignored
  evidence verifier: **129 checks**.
- Historical Stage 25D, 25E and 25F-A verifiers at their accepted commits:
  **144**, **130** and **143** checks; historical B2 source verifier: **94**
  checks. Those immutable verifiers deliberately lock earlier catalog and
  transformer versions, so they are evaluated in isolated historical clones
  rather than weakened to accept H-A/H-B/H-C breadth.
- Historical vanilla iOS Stage 24E2 verifier at its accepted commit: **89
  checks**. Current vanilla iOS and tvOS builder help remains functional and
  neither normal entry point enables Everest.
- Current tvOS Stage 14 source/product-boundary verification: **pass**, with
  **186** documentation links. Current repository documentation, privacy,
  permissions and generated/proprietary isolation verification: **pass**.

The exact public fixture fetcher was independently exercised from an empty
temporary directory and reproduced the registered ZIP hash. Shell/Python
syntax, JSON parsing, `git diff --check`, and the real source-free device
rewrite all pass. The latter is diagnostic transformation evidence only; it
does not override the zero-overall product gate.

## Recommendation

Stage 25H-C is integration-ready as a bounded YELLOW architecture increment;
it is not a claim of Space Trip support. Stage 25G2 is not next because no map
is zero-overall. The next audit should test one class only:
`STATIC_DIRECT_ILHOOK_FREEZE` eligibility. Direct `ILHook` now has the largest
measured distinct high-priority construction surface (53 sites), narrowly
ahead of 50 configured-hook related sites. Exact target, manipulator, config
and immutable-lifetime eligibility must decide the fixture; it must not be
selected by count alone. No manual download is currently required.

Medium standalone maps are now close only when their helper graph avoids broad
direct/configured hooks and custom audio. A small collab still needs general
LevelSet/progression plus wider helper/content closure. A genuinely IL-heavy
helper needs bounded direct/configured/capture classes chosen from exact graph
payoff. Strawberry Jam remains premature until these classes, custom audio,
progression and broader content/helper ecosystems are accepted; Lua/native
remain separate explicit boundaries.
