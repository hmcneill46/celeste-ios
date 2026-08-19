# Stage 25D-C — scalable Apple static managed detours

Status: **PASS — GREEN**

Date: 2026-08-19

Stage 25D-C extends the shared Apple static-AOT Everest experiment from a
small hand-written HookGen surface to one signature-driven managed-detour
backend. Both HookGen `On.*` subscriptions and one bounded form of direct
`MonoMod.RuntimeDetour.Hook` are lowered on the Mac into ordinary typed code
and data. No iPhone, iPad, or Apple TV executable code is modified at runtime.

This remains an internal compatibility experiment. It is not general Everest
support, does not enable `IL.*`, `ILHook`, `NativeDetour`, Lua, runtime DLL
loading, or Strawberry Jam, and does not add a public `--mods` option to either
vanilla builder.

## Git, release, and upstream boundary

| Item | Accepted value |
| --- | --- |
| Starting commit | `4a4f376a4a34ba880bcd508ac04fc7bbdd5399f0` |
| Feature branch | `feature/apple-everest-managed-detours` |
| Final acceptance commit | The commit containing this report; the final response records its exact full SHA |
| Everest stable source | `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00` |
| MonoMod source | `dfc30a1506d37fb88a2c2be004f525205f46a24c` |
| iOS recovery tag | `ios-v0.1.1-rc.1^{}` → `27e16b4724d94d3991b99c4795f680fcb0e5830c` |
| tvOS RC1 | `v1.0.0-rc.1^{}` → `ee52b0868df091746f134d95d4f020f94f23d4fb` |
| tvOS RC2 | `v1.0.0-rc.2^{}` → `641e86e4ed164cdf93f602ce2f11436449654d6e` |
| Deferred RC3 branch | `origin/release/v1.0.0-rc.3` → `c8134c8ca7924cf12f48527e714b5242c6024927` |
| RC3 tag | Absent |

No integration branch, release branch, tag, GitHub workflow, or cloud-builder
template was changed. No GitHub Actions workflow was run.

## Candidate audit and selected fixtures

Fourteen real or source-audit candidates were classified. The exact findings,
release URLs, hashes, source pins, mechanisms, and license status are recorded
in
[`managed-detour-compatibility-stage25d.json`](../../../apple-everest/managed-detour-compatibility-stage25d.json).
The selected four-package closure is:

| Fixture | Purpose | Exact provenance | Result |
| --- | --- | --- | --- |
| I Accidentally Four Cassette Blocks 1.0.0 | Stage 25C content/map regression | ZIP `37eaa16b6b2d458a8ce27e08ea315b4cc3c2938d6b3c2292c61203c641ad7c95`; no explicit redistribution license located | Content-only supported |
| Particle Palette Helper 1.0.0 | Stage 25C real HookGen regression | ZIP `f9cf8874acbfaff87af22098caea36429c97941d0b13e1114d8749be61afbe19`; DLL `eac2cb52112dafcb31f86cdad0f3373bb781a7f00e705394fb811c0f7ef08c17`; source `9a791bcb38f64a4c49ea3e496003899416f0bb82`; MIT | Binary-compatible static transform |
| Feather Maddy 1.3 | Broader real HookGen fixture | ZIP `a8f1104710aac5807be3b24cd8c3870d94aa117d1146b30a4de0983a10f3e40e`; DLL `a4ff1c89733cb450af1fc7199ed777fd23f849fe4f6a3e0a42618e1177144509`; frozen DLL `f88aecc4d5cadeb9b46c4a34bf035d89a8c09593cb726c2826a2b2e4eeda5166`; source `d9d339c00ca4a4d109183d66fac09303bc7ffd64`; no explicit redistribution license located | Binary-compatible static transform |
| Lag Pauser 1.3.0 | Real mixed HookGen/direct-Hook fixture | ZIP `32dac84d2c5b60458a701cb61e8601bc89d937e25bc7fdcf52c80d9128e99d10`; DLL `6ed3515105056ff2f4be84ef5542ce0a7b90dca9f5bd305703c506f462ffcdfa`; frozen DLL `bbe3c964e9dd1664ed79cba5f1b48f62b358bbd472615df912377e396e99e452`; source `ec217fd959daa91fa1efcb6cd62a9808178805e7`; no explicit redistribution license located | Binary-compatible shared static managed detours |

No fixture bytes or source trees are tracked. The two fixtures without an
explicit redistribution license remain ignored external inputs, as do all
other third-party ZIPs, DLLs, maps, and assets.

## Signature-driven target generator

[`managed-detour-targets-v1.json`](../../../apple-everest/managed-detour-targets-v1.json)
contains 18 reviewed target descriptors. For each descriptor the host
generates:

- exact typed `orig` and hook delegates;
- the HookGen-compatible event facade;
- one shared registration list and cached chain;
- direct-Hook registration where an exact direct alias is authorised;
- owner cleanup and a no-hook direct path;
- one exact rewrite of the canonical target body.

The old bespoke Dialog, ParticleSystem, and TrailManager dispatch files were
removed. A new target now normally requires one reviewed descriptor and its
tests, rather than another hand-written runtime subsystem.

The accepted signature envelope is deliberately concrete: instance targets
with zero through six explicit parameters, value/reference parameters, and
the exact `void`, `bool`, `string`, and `PlayerDeadBody` return shapes in the
catalog. Typed `orig` can mutate arguments/returns or be deliberately omitted.
Unsupported signatures fail generation; there is no `DynamicInvoke`,
`object[]` trampoline, reflection emit, or runtime method construction.

Feather Maddy added nine real HookGen targets:

- `Level.LoadLevel`;
- `Level.TransitionTo`;
- `Player.DashBegin`;
- `Player.RefillDash`;
- `Player.StarFlyEnd`;
- `Player.Update`;
- `Player.IntroRespawnBegin`;
- `Player.Render`;
- `Refill.OnPlayer`.

`On.Monocle.Engine.Update` and `Player.Die` complete the 18-target catalog;
the six Particle Palette Helper targets and `Dialog.Clean` remain regression
members.

## Direct `Hook` static model

The production analyzer accepts one exact precompiled constructor surface:

`Hook(MethodBase target, MethodInfo detour)`

It recognises the compiler-emitted fixed expression formed from
`typeof(Target).GetMethod("name")` and
`typeof(DetourOwner).GetMethod("name")`. The target name must resolve through
an authorised catalog alias, the detour must resolve uniquely in the same
input assembly, and its typed `orig`/argument/return shape must match the
target. A static detour is accepted; the host model also represents a uniquely
resolved module-instance detour. Runtime-chosen types/names, ambiguous methods,
captured arbitrary instances, property targets outside the catalog, other
constructors, and unsupported configuration are rejected before AOT.

The host replaces the resolved construction with a constant static plan ID.
The small device-side `MonoMod.RuntimeDetour.Hook` facade contains only the
plan-backed handle surface. It exposes `Apply`, `Undo`, `Dispose`, `IsApplied`,
and `IsValid`; these operations add/remove/enable data in the same typed chain
used by HookGen. They never alter executable memory. Disposed handles become
invalid, duplicate operations are harmless where MonoMod semantics require,
and owner unload removes both HookGen and direct registrations.

`DetourConfig` has a bounded data model for ID, nullable priority,
sub-priority, `Before`, and `After`, including `With*`/`Add*` helpers. The
project-owned conformance tests cover configured ordering and reject cycles.
The real accepted two-argument `Hook` does not carry configuration; third-party
configured constructors and `DetourContext` remain deferred rather than being
silently ignored. The Trailine version-gated `After = *` path is known dead at
the pinned Everest version, but is still rejected because production does not
erase unproved third-party branches.

## Semantics and lifetime

The 47-check typed semantics suite and pinned desktop reference prove:

- zero hooks call the original once;
- `orig` receives and returns the exact typed values;
- a detour may suppress `orig`;
- `Apply`, `Undo`, `Dispose`, `IsApplied`, and `IsValid` match the supported
  lifecycle;
- two direct hooks chain correctly;
- HookGen and direct Hook coexist on one target;
- owner cleanup is isolated;
- configured priority/sub-priority/Before/After ordering is deterministic;
- cyclic constraints fail rather than degrade to registration order.

With A registered before B, both the pinned desktop HookGen reference and the
direct-Hook reference produced:

`B-before → A-before → original → A-after → B-after`

The generated target is rewritten exactly once no matter how many modules
subscribe. Hot-path chains are rebuilt only when registration or module state
changes; the zero-hook and stable-hook paths allocate no per-call chain.

Lag Pauser is the real mixed proof. Its distributed DLL subscribes to
`On.Monocle.Engine.Update` and constructs a direct Hook around
`Player.orig_Die`. The final closure builds without the mod's source tree and
without any mod-name-specific transformer. A bounded first-invocation record
exists for direct plans, but normal verbose logging is filtered according to
Everest's tag-level policy. This corrected an early diagnostic build which
treated legacy `Logger.Log` as unconditional and caused per-frame Feather
Maddy file output.

The external-reference scanner also found that Lag Pauser legally expects
Everest-patched access to exactly `Level.StartPauseEffects`,
`Level.EndPauseEffects`, and `Level.unpauseTimer`. The production solution is a
three-member, version-locked reviewed Apple API surface, not a broad publicizer.
The scanner rejects missing *or inaccessible* members before AOT. The API
surface contributes hash
`52df8fb1350a92c0720d5e9601c26f8dd558caa2449c3ab6395bf43879d26034`
to the complete closure.

## Shared closure and reproducibility

The exact resolved order is:

1. Feather Maddy;
2. I Accidentally Four Cassette Blocks;
3. Lag Pauser;
4. Particle Palette Helper.

| Boundary | Count / SHA-256 |
| --- | --- |
| Managed closure | 13 files; `b37473122bd03761e7dea21e22235a6c4a5511a4a19c81b0ecadf157271afced` |
| Content closure | 5 files; `3f87563b10dd592db1e0fe5f0f472987af9c0ec55838e79addb80466bb2a614c` |
| Hook transformation | `6dd288a1a0393e70c93fd3bcf1bff3a8c9d38fc5fd7e69f937b369affe287d57` |
| Reviewed Apple API surface | 3 members; `52df8fb1350a92c0720d5e9601c26f8dd558caa2449c3ab6395bf43879d26034` |
| Complete shared closure | `4e7abe3f76fa18c55213241f6fb11e5670610eb440cc923b870fec63cbb44e6f` |

Two independent clean generations produced the same complete manifest and
hash. Exactly one closure feeds iOS/iPadOS and tvOS; there is no platform- or
mod-specific source patch after this boundary.

## Package, AOT, and forbidden-runtime proof

The final clean unsigned sequential build completed in 542.66 seconds. iOS
completed at 269.34 seconds and tvOS completed 273.31 seconds later.

| Product | IPA bytes | SHA-256 | Contract |
| --- | ---: | --- | --- |
| iOS/iPadOS unsigned | 882,148,692 | `898825b236f3b472c9857a0bcde3f3d6ac3ba502104836273cd0c93eccbc26d2` | arm64; iOS 15.0; Release; full trim/AOT; no interpreter/JIT |
| tvOS unsigned | 896,107,466 | `308ca2d813923e353fcdd02cc9f7b32e9d82a00c91a382226c74ef7b845f8d96` | arm64; tvOS 16.0; Release; full trim/AOT; no interpreter/JIT |
| iOS/iPadOS signed physical | 882,316,347 | `46be41d1a971a536385afc8b930d1ba452c63daf16560922b172309fdfb2f9b0` | same closure and AOT contract |
| tvOS signed physical | 896,500,098 | `e044a24a7e7fabeeaccd2852c2ef6badf21a91d7f1917ad6c5ca79b149dbdd33` | same closure and AOT contract |

All three external assemblies are retained as linked DLLs and have matching
`.aotdata.arm64`. The build verifies every executable external method across
both its LLVM AOT object and companion Mono object. It also compares the
pre-link and linked method bodies and resolves the complete external
Celeste/Everest reference surface with accessibility checks.

Post-link and package scans found no device HookGen backend,
`MonoMod.RuntimeDetour.dll`, `NativeDetour`, `ILHook`, NLua/KeraLua,
Reflection.Emit, `DynamicMethod`, assembly loader, arbitrary native mod
loader, process launcher, or file watcher. The facade type name exists only as
ordinary statically compiled API compatibility; no desktop RuntimeDetour
implementation is shipped.

## Physical acceptance

### iPhone

The same-identity development build launched on the physical iPhone 12 Pro
Max. Feather Maddy loaded and changed gameplay as expected; death/respawn
executed Lag Pauser's real direct hook; touch, controller, audio, haptics,
pause/resume, rotation, background/foreground, and the static mod map passed.
Turning Feather Maddy Off/On during gameplay correctly removed/restored its
owned handlers.

An initial build froze after pause because Lag Pauser's three Everest-patched
`Level` members were present but inaccessible. The exact API surface and
accessibility scanner above fixed that defect. In the accepted build Lag
Pauser's default 100 ms policy may intentionally show its responsive
“Lag Paused” menu after the ordinary resume-frame spike; repeated Resume may
trigger it again if the next frame also exceeds the threshold. With Lag Pauser
disabled, normal pause/resume remains uninterrupted. This is visible mod
policy, not a frozen renderer or touch input.

### iPadOS 15.8.8

The universal iOS build installed through the legacy Apple-device path and
launched on the older physical iPad. Menu, touch, controller, audio, Feather
Maddy, Lag Pauser's responsive pause policy, death/respawn, the static map,
rotation, and background/reopen all passed. The user reported every requested
check worked perfectly.

### Apple TV

The tvOS build launched on the physical Apple TV 4K (3rd generation) with one
FNA/FMOD runtime and normal controller/audio behavior. Feather Maddy, Lag
Pauser, death/respawn, pause/resume, the static map, Home/reopen, and lifecycle
all passed. The user reported every requested check worked perfectly.

The mod-map launcher uses a non-persistent debug session. Stage 25D disables
Save and Quit for that session at the normal menu-policy boundary, avoiding the
Stage 25C background `UserIO` fatal while leaving ordinary numbered vanilla
saves unchanged.

No measurable gameplay regression was observed on any device. The visible Lag
Pauser menu reflects its threshold by design. The logging correction removes
the only observed uncontrolled hot-path I/O; stable generated dispatch chains
do not allocate per call.

## Regression, privacy, and locks

Deterministic acceptance includes 112 builder tests, 47 shared hook-semantics
tests, the pinned real MonoMod IL-freeze reference, and both desktop HookGen
and direct-Hook reference chains. Stage 9B through Stage 13B tvOS suites and
the current iOS Stage 24D2/C2 behavior suites pass. Some older iOS/Stage 25C
historical verifiers intentionally assert a byte layout that predates the
accepted Stage 25B shared foundation; those files were not weakened. Stage
25D's current verifier owns the post-Stage-25C invariants and proves no new
vanilla/native or GitHub workflow change from its `4a4f376…` baseline.

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

No tracked proprietary game/mod/FMOD bytes, generated Celeste source, saves,
signing values, device identifiers, LAN data, or private paths were added. No
analytics, telemetry, downloader, updater, listener, or permission was added.

## Known limitations and next stage

Supported compatibility is exactly the tracked 18-target catalog and one
fixed direct-Hook construction pattern. `IL.*`, `ILHook`, NativeDetour,
dynamic/ambiguous target or detour selection, unsupported configuration,
arbitrary captured detours, helper ecosystems, custom entities/triggers from
real helpers, Mod Options/settings persistence, module SaveData/session,
general ModInterop, Lua, and custom mod audio remain deferred or rejected.

The next useful rung is Stage 25E: select one small, well-licensed helper with
a shallow deterministic dependency graph and one small map that depends on it.
It should exercise generated custom entity/trigger/backdrop registration and a
small Mod Options/settings persistence surface, avoid Lua/native/large IL
graphs, and remain source-free in the final closure. No manual download is
needed if the next stage pins publicly downloadable release ZIPs through the
existing ignored acquisition tooling.

Measured work still has several distinct classes before an IL-heavy helper is
rational: helper dependency closure, custom entity/trigger/backdrop
registration, settings/Mod Options, module SaveData/session, and common
ModInterop/content edges. Strawberry Jam additionally needs a production
deterministic `IL.*` lane, broader helper ecosystems, Lua/native decisions,
and custom audio/content coverage. It remains several bounded stages away and
was neither downloaded nor attempted here.
