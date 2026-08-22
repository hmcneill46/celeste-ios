# Stage 25H-D — bounded static direct ILHook freeze

Status: **PASS — GREEN**

Date: 2026-08-22

Stage 25H-D productionises one deliberately bounded direct
`MonoMod.RuntimeDetour.ILHook` class for the shared Apple static-AOT product.
The real distributed CaeruleaHelper 1.11.1 manipulator is executed on the Mac
against an exact target fingerprint, then its output is frozen into ordinary
managed IL before iOS/iPadOS/tvOS compilation. The device product contains no
runtime `ILHook` backend, Cecil, `MonoMod.Cil`, dynamic method, JIT, interpreter,
or executable patcher.

This is not general direct-ILHook support. The accepted class is precisely:

`STATIC_DIRECT_ILHOOK_FREEZE`

- exact statically resolvable target;
- exact statically resolvable manipulator;
- no `DetourConfig` or ambient configured ordering;
- immediate apply;
- one immutable-active installed-build lifetime;
- no gameplay `Apply`, `Undo`, or `Dispose`;
- no second direct hook on the same target.

## Baseline and immutable references

| Item | Value/result |
| --- | --- |
| Starting integration commit | `6b5445f862d94da49df5aca8a9ecebc6e253cb20` |
| Feature branch | `feature/apple-everest-static-direct-ilhook` |
| Final commit | The commit containing this report; the handoff records its exact SHA |
| Everest | stable-1.6458.0 / `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00` |
| MonoMod | `dfc30a1506d37fb88a2c2be004f525205f46a24c` |
| iOS recovery tag | `27e16b4724d94d3991b99c4795f680fcb0e5830c` |
| tvOS RC1 / RC2 | `ee52b0868df091746f134d95d4f020f94f23d4fb` / `641e86e4ed164cdf93f602ce2f11436449654d6e` |
| RC3 release branch | `c8134c8ca7924cf12f48527e714b5242c6024927` |
| RC3 tag | Absent |
| GitHub Actions | Not run; zero Actions minutes used |

Historical Stage 25G/H-A/H-B/H-C reports and the H-C graph audit remain
byte-identical. No historical feature ref was rewritten.

## Complete 53-site classification

All 53 direct constructions in the Stage 25H-C high-priority graph union were
reacquired and classified before implementation. The exact packages were
CaeruleaHelper (1), CrystallineHelper (2), LuckyHelper (8), MaxHelpingHand
(34), and TeraHelper (8). The mandatory broader release audit also reacquired
ExtendedVariantMode, GravityHelper, and SorbetHelper. Every ZIP and distributed
DLL was hash checked and safely extracted; detailed third-party evidence
remains ignored.

Each construction records the owner, assembly, containing method, IL offset,
constructor, target and manipulator origins, configuration, apply behavior,
storage, later lifecycle calls, conditionality, and same-target participation
in [`apple-everest/direct-ilhook-audit-stage25hd.json`](../../../apple-everest/direct-ilhook-audit-stage25hd.json).

| Primary class | Sites |
| --- | ---: |
| A — static, unconfigured, immutable | 12 |
| B — static, unconfigured, dynamic lifetime | 2 |
| C — static, configured | 32 |
| D — dynamic target | 1 |
| E — dynamic manipulator | 0 |
| F — multiple direct hooks on same target | 6 |
| G — conditional runtime creation | 0 |
| H — other | 0 |
| **Total** | **53** |

Secondary properties intentionally overlap primary classes: three sites have
gameplay-dynamic lifetime, 12 have dynamic targets, zero have dynamic
manipulators, and 13 sites form five multiple-direct-same-target groups.
Configured classification takes precedence, so MaxHelpingHand's 32 ambient
configured sites can never be misreported as plain eligible hooks.

CrystallineHelper's two direct hooks share `Player.orig_Update`; this stage
does not accept them because exact same-target direct ordering was not required
by the selected product. Configured, gameplay-dynamic, dynamically resolved,
and ambiguous sites fail before AOT.

## Selected real helper and graph payoff

| Field | Exact accepted value |
| --- | --- |
| Helper | CaeruleaHelper 1.11.1 |
| Public release | `https://gamebanana.com/mmdl/1784884` |
| ZIP SHA-256 | `6a0649518d49cd0d17b84da3be53929cdd602d89d922e2d3ab87c524345e3807` |
| Distributed DLL SHA-256 | `3c5b79a57ce03b6c98e8ae12d781ec6baddce944995b2b4928f067a5ad7973ae` |
| Source logical SHA-256 | `036bc9adbc5471931ca6cfb1aa574d0bbcfe3a6dce9025a09b56a0c522054d07` |
| Source commit | `ce2ad0694feb28cd3dff0a5d7501f6e60d620fd5` |
| License | MIT |
| Production authority | Distributed release DLL; source not required |
| Exact Stage 25G graph advanced | QLetterAurora |

QLetterAurora uses CpopHelper, CaeruleaHelper, MaxHelpingHand, and LuckyHelper.
Freezing CaeruleaHelper removes one of its 43 direct-ILHook sites, leaving 42.
Configured MaxHelpingHand hooks, other direct hooks, ordinary IL/API/content
breadth, and other graph work remain. Exactly one graph is materially advanced;
zero graphs have no remaining direct-ILHook blocker, no remaining IL blocker,
or zero overall blockers. Therefore no actual Stage 25G multi-helper map was
packaged or claimed.

LittleEpic's Precision Challenge remains closest overall. It is a more valuable
next graph-completion target than implementing configured ILHook merely because
configured sites are numerous.

## Exact direct construction and lifetime

The authoritative distributed binary constructs:

```text
new ILHook(
  typeof(Celeste.Player)
    .GetMethod("DashCoroutine", NonPublic | Instance)
    .GetStateMachineTarget(),
  DashSpeedHook.ModifyDashCoroutineIL)
```

The exact constructor is:

`System.Void MonoMod.RuntimeDetour.ILHook::.ctor(System.Reflection.MethodBase,MonoMod.Cil.ILContext/Manipulator)`

Pinned MonoMod applies that overload immediately (`applyByDefault=true`). The
resolved target is the unique compiler-generated
`Celeste.Player.<DashCoroutine>d__*::MoveNext()` method. The manipulator is the
static `Celeste.Mod.CaeruleaHelper.Hooks.DashSpeedHook.ModifyDashCoroutineIL`.
No config is supplied. The object is stored in the one static
`DashCoroutineHook` field, receives no separate `Apply`, is never read through
`IsApplied` or `IsValid`, and is disposed only by module unload. No gameplay
path calls `Undo` or `Dispose`, reconstructs it, or conditionally changes it.

The installed Apple profile is itself immutable. Consequently applying the
transform for the whole process/module lifetime is observably equivalent.
Module unload only performs teardown and needs no live unfreeze. If the helper
is omitted from a later build, no plan is emitted and the exact canonical
target body returns; no residual mutation survives.

## Source-free frozen transformation

The production path is:

```text
fresh exact public ZIP
  → hash and safe-extraction checks
  → distributed DLL binary census
  → exact constructor/target/manipulator/lifetime resolution
  → canonical target fingerprint
  → real distributed manipulator in pinned Mac worker
  → intermediate/final IL validation and hash locks
  → constructor/field/unload-Dispose removal
  → host-only IL types/manipulators removed
  → introduced calls publicised/rooted
  → shared Apple closure
  → full trim and AOT
```

| Boundary | SHA-256 |
| --- | --- |
| Target baseline | `65ad66657afe6ca8d9d440be58b258cb48159b966b515aafe7b3b3ae85325416` |
| Target after real manipulator | `ebafbfe93b806ace3b0e49324702ff7a164adbd0379f142af2c864a6ae697abc` |
| Normalized semantic diff | `a941d338396c91447d9c0f331137cbd0d04016da5b2eaf32ab0f684a71a93f30` |
| Frozen plan | `b3f1f7ee1b14028759bf29156b14d5145218144638d11f03df29728c80a63ca7` |
| Final frozen helper DLL | `a4debb7153959317b9ce2b7366b55834091d26b28056a5f2b30d9131a41ade28` |

Plan schema 3 adds the mechanism, constructor, target/manipulator expressions,
configuration, apply behavior, storage, and lifetime to the plan hash. The
same `AppleEverestIlWorker` used by H-A/H-B/H-C executes the real distributed
manipulator. Three independent executions produce identical target,
manipulator, before/after/diff locks, delegate lowering, device rewrite, plan,
and frozen DLL. Normalized output matches the pinned desktop reference.

The manipulator introduces two calls to the ordinary static
`DashSpeedHook.PositiveINF` method. The method and its owner are publicised and
explicitly covered by the AOT closure. The constructor, static field, teardown
`Dispose`, manipulator bodies, Cecil/MonoMod.Cil types, and residual host
references are removed. No tiny device `ILHook` facade was necessary.

## Direct/event/On conformance

A project-owned target makes composition order numerically observable. It
executes the actual pinned desktop `HookEndpointManager.Modify`, direct
`ILHook(MethodBase, Manipulator)`, and `HookEndpointManager.Add` entry points,
then compares the results with the schema-3 Apple worker:

| Registration scenario | Desktop result | Apple frozen underlying result |
| --- | ---: | ---: |
| HookGen event A, then direct B | 55 | 55 |
| Direct B, then HookGen event A | 43 | 43 |
| Direct B, then On-style wrapper adding 100 | 140 | 40 |
| H-B event A, event B, direct C, then On-style wrapper adding 100 | 148 | 48 |

This proves registration order rather than assuming it. It also proves that a
normal On-style `orig` sees the fully direct-IL-frozen underlying method. The
Apple product keeps the ordinary static On wrapper and needs no runtime IL
backend. Multiple direct hooks on one target remain deferred despite this
project-owned semantic fixture.

## Shared closure and products

One target-neutral closure feeds iOS, iPadOS and tvOS:

| Boundary | SHA-256 |
| --- | --- |
| Shared closure | `769741d8cde76ffe7e7497263b326f434f889d416c94ea7b6f05422ebb7ff05e` |
| Managed logical tree | `1c30df8984dca04801ed11f23a33b9ad758c93dd376abd5262141466793ed5aa` |
| Content logical tree | `bdc8dd0805f2f14bc59a400b3b0e8add8809c443619e0b53a3c6d6c6dca133a1` |
| Hook transform | `6c8b00738fd06f3e091aaced109261cf35ab0d147b09615393056c1eafdb08b7` |
| ModInterop plan | `84f0267040cda9cc51d4dc08da367a4b934aa0e9f43cf3d553815e594f25899d` |
| Reviewed API surface | `d594dea86e944b468819818773f2177312445bbebe022bcbc7ea1ff779c8652c` |

The closure contains 13 frozen transforms: nine from CaeruleaHelper, one from
Dash Toggle Helper, and three from DisposableTheo. Exactly one uses the direct
mechanism. The other 12 retain the H-A/H-B event semantics.

| Product | Bytes | SHA-256 |
| --- | ---: | --- |
| Unsigned iOS | 882,016,940 | `e59554b51e5b750266b59468d294111e94e352c8d8f5e8dda29ab7d834398450` |
| Unsigned tvOS | 896,194,867 | `2e0780ca797144d259bb56c6b41102123acc63aa5270498e7d3b18b2f9a44224` |
| Signed iOS | 882,189,558 | `667358e7879812ff5649798853bbcace2ce0ae58221e78b12ffa93fd3dae995f` |
| Signed tvOS | 896,602,166 | `782c864c8b8071abe34a59020a377977a4e2a61006d544c28b2a7b085b6578ed` |

The combined signed build completed in 1,487.2 seconds. Both targets are
Release arm64, fully trimmed and fully AOT compiled with
`UseInterpreter=false`; there is no JIT. Package, preserved-assembly,
static-reference, runtime-closure, and AOT-object scanners pass. The products
contain all seven FMOD banks and no forbidden device IL-patching surface.

## Physical acceptance

The signed separate-identity canary products were installed without removing
user data. The project-owned room uses CaeruleaHelper's real
`NoDashSpeedResetTrigger`: the left half preserves speed after horizontal and
up-diagonal dashes, while the right half shows canonical reset behavior.
This is the exact **No Dash Speed Reset** behavior under physical test.

- **iPhone 12 Pro Max:** direct behavior, touch, controller, seven-bank audio,
  death/respawn, pause/resume, rotation, background/reopen, Dash Toggle H-A,
  and DisposableTheo H-B all passed.
- **iPadOS 15.8.8:** the same universal iOS product passed launch, direct
  behavior, touch/controller, audio, death/respawn, pause/resume, rotation,
  background/reopen, Dash Toggle and DisposableTheo through the legacy device
  route.
- **Apple TV:** the same shared closure passed FNA3D Metal at 1920×1080,
  controller, all seven FMOD banks, direct behavior, death/respawn, pause,
  Home/reopen, cold relaunch, Dash Toggle and DisposableTheo.

No actual multi-helper Stage 25G map was run because none reached zero overall
blockers. This distinction is intentional.

## Regression locks

| Boundary | Unchanged SHA-256 |
| --- | --- |
| Canonical Content | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw source | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched source | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Stage-6 real-audio tree | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| iOS native | `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2` |
| tvOS native | `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc` |
| Vanilla iOS generated tree | `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357` |
| H-A standalone frozen DLL | `79d06fd9428e33613b81f089693651f03c6445e0a6f22368902bb3fcb49e904f` |
| H-C Vortex plan | `96726af5fa21c129d1b1bd39746a1f6f903be38e2b53b20b79275eb05d5a3261` |
| F-B2 ModInterop plan | `9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318` |

The current deterministic builder suite passes 285 checks and the typed
HookGen/direct managed-detour semantic suite passes 48. H-A real freeze,
H-B A/B/AB/ABC/neither plus direct/event/On composition, and all H-D negative
target/config/lifetime cases pass. Historical H-A and H-B verifiers pass at
their accepted commits with 190 and 144 checks respectively; they are not
weakened to accept later schema breadth. Current H-C graph/lock verification,
B2 ModInterop, F-A durability, 25E entities/settings, and 25D managed detours
remain covered by the current suite and their immutable historical evidence.
The final Stage 25H-D invariant verifier passes **168 checks**.

Normal `build-ios.sh` and `build-tvos.sh` remain mod-free. The exact fixture
fetcher works from an empty ignored directory. No GitHub Actions workflow was
run. Repository/privacy verification finds no tracked third-party ZIP/DLL,
Celeste/FMOD content, IPA, save, signing value, device identifier, credential,
or private path.

## Limitations and recommendation

Stage 25H-D is integration-ready as a bounded GREEN mechanism and real-helper
increment. It does not make arbitrary direct ILHook, configured ordering,
dynamic target/manipulator selection, gameplay-scoped lifetime, or multiple
direct hooks on one target compatible.

Configured ILHook is now the largest measured remaining direct-mechanism class
(32 primary sites), but count alone is not the correct next-stage criterion.
LittleEpic's Precision Challenge remains the closest overall graph and its
ordinary DJMapHelper IL/API/entity/trigger/content closure is the higher-value
next focused target. DynamicData has not become the closest graph's dominant
wall, and custom audio has not overtaken IL for that graph.

The first true multi-helper map is approximately one focused graph-completion
stage away if LittleEpic's exact ordinary breadth closes and passes physical
testing. A medium standalone map is similarly close when it avoids configured
or dynamic helper mechanisms. A small collab still needs progression/LevelSet
durability plus broader helper/content coverage. A genuinely IL-heavy helper
still needs configured ordering, dynamic lifetime/target and same-target
classes, additional captures, API/content breadth, and physical proof.
Strawberry Jam remains premature until configured/dynamic IL, DynamicData,
custom audio, progression, broader content/helper ecosystems, and its Lua or
native boundaries are explicitly solved. No manual fixture download is needed
for the recommended next audit; exact public fetchers should continue to own
reproducible inputs.
