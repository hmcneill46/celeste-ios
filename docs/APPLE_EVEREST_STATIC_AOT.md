# Apple Everest static-AOT architecture

This repository contains an experimental foundation for building a deliberately
small, pre-analysed subset of Everest mods into separate iPhone, iPad, and
Apple TV canary products. It is contributor infrastructure, not general
Everest compatibility, and it is not part of the recommended vanilla builders.

## Why desktop Everest is not run on-device

Desktop Everest and MonoMod normally discover assemblies, generate hooks, and
rewrite or detour managed code while the game is running. Native Apple full-AOT
products cannot safely depend on runtime IL generation, JIT compilation,
runtime assembly loading, or desktop-native/Lua dependencies. Copying the
desktop distribution into an IPA would therefore be both incorrect and much
broader than the supported experiment.

The Apple design moves every supported dynamic operation to the Mac host. The
device receives ordinary statically compiled C# plus content:

```text
user-selected mod packages
  -> bounded extraction and Everest metadata graph
  -> compatibility analysis
  -> deterministic content compilation and C# closure generation
  -> normal iOS/tvOS full-AOT build
```

No Everest updater, downloader, LAN service, telemetry, or runtime mod loader is
present in the canary app.

## Pinned build-host profile

The initial profile is `apple-everest-stable-1.6458.0-v1` and locks:

- Everest `stable-1.6458.0` at commit
  `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00`;
- its MonoMod submodule at
  `dfc30a1506d37fb88a2c2be004f525205f46a24c`;
- NLua provenance at
  `b3524288712743fb2394dcf615d14d0dac3276e2` (provenance only; NLua is not
  placed in the device closure);
- YamlDotNet 16.1.3 and Mono.Cecil 0.11.6 for the repository-owned host tool;
- .NET SDK 8.0.424 for the builder and 9.0.317 for the pinned MonoMod host
  regression.

The complete machine-readable profile is in
[`apple-everest/profiles/stable-1.6458.0.json`](../apple-everest/profiles/stable-1.6458.0.json).
Unknown or changed upstream inputs fail closed.

## Ingest, graph, and compatibility policy

`AppleEverestBuilder` accepts a directory or ordinary Everest ZIP through a bounded archive
reader. Absolute paths, traversal, links, duplicate normalized paths, excessive
depth, excessive members, and oversized input are rejected. `everest.yaml` is
parsed with the pinned YamlDotNet package. Required dependencies, optional
dependencies, conflicts, duplicate names, and cycles are resolved into a stable
topological order.

The compatibility analyser classifies every package before generated code is
accepted. The profile supports project-owned content-only packages, static
module lifecycle/events, explicitly recognised typed `On.*` hooks, and the
bounded direct `MonoMod.RuntimeDetour.Hook` class described below. It rejects
dynamic code, runtime IL hooks, dynamic/ambiguous detour targets or delegates,
native payloads, Lua, unsupported platform APIs, and unknown mechanisms. A
rejection is a compatibility result, not an invitation to weaken full AOT.

Stage 25C adds a binary-first lane for explicitly compatible distributed code
mods. The Mac host inventories the declared DLL with Mono.Cecil, infers the
single concrete module and its static state types, checks its complete managed
reference/mechanism surface, and writes a transformed assembly into the shared
closure. The transformation preserves assembly/type identity, removes the
`MMHOOK_Celeste` runtime reference, and rebinds only registered HookGen facades
to the statically linked `Celeste` assembly. Both the original ZIP/DLL hashes
and transformed DLL hash are recorded. Source is audit evidence, not a build
input.

Precompiled mods sometimes call members which Everest deliberately exposes in
its desktop-patched Celeste assembly. Apple builds reproduce only the exact
reviewed members needed by the accepted closure through
`apple-everest/apple-api-surface-v1.json`. The transform is declaration-locked,
and the post-link scanner rejects any absent **or inaccessible** external API
before installation. This is intentionally not a broad publicizer.

### Composed map graphs and the pre-AOT gate

Multi-helper composition uses the same closed architecture rather than a
second runtime loader:

```text
public map/helper ZIP graph
          |
  dependency resolver
          |
per-assembly and map-content compatibility census
          |
  +-------+---------+---------+---------+
  |                 |         |         |
hooks           registries  interop   content
  |                 |         |         |
  +-----------------+---------+---------+
                    |
          static composed closure
                    |
             iOS/tvOS AOT hosts
```

The census maps actual entity, trigger, backdrop, processor, asset and audio
usage back to the owning helper; a declaration in `everest.yaml` alone is not
evidence that a helper participates. The complete transitive graph must have
zero unclassified blockers before an expensive product build starts. Missing
or wrong-version dependencies, cycles, conflicting IDs, unsupported hooks,
active IL, Lua/native code, custom audio, and unknown content fail before AOT.

Stage 25G applied that policy to 53 real maps and 24 deep graphs. Noctambule
was the strongest small graph, with two genuine helper triggers and no active
IL or custom bank, but one trigger executes a packaged Lua completion cutscene.
Because NLua/KeraLua are intentionally absent, the result is a classified
pre-AOT YELLOW rather than a stripped map or a misleading product build. The
runtime module set for any accepted installed build remains immutable: helper
ZIPs, DLLs, scripts, and registries cannot be added or replaced on-device.

## One shared Apple closure

The tool emits one deterministic closure, not separate iOS and tvOS mod forks.
Its manifest records the graph, package ownership/order, generated managed and
content hashes, compatibility decisions, and one shared-closure hash. That same
directory is applied unchanged to derived iOS and tvOS canonical Celeste trees.
Only the existing narrow Apple host, storage, lifecycle, and packaging adapters
remain platform-specific.

The generated managed closure contains:

- a static module registry and strongly typed factories;
- ordinary module lifecycle and event subscriptions;
- linker/AOT roots for every accepted module and generated dispatch target;
- statically compiled module source and/or transformed external assemblies;
- typed hook dispatchers for explicitly supported targets.

There is no runtime DLL discovery or loading. A transformed external assembly
is referenced by the generated project and rooted by a concrete module factory.
Because an ordinary third-party Everest DLL has no reliable .NET trimmer
annotations, the closure also generates a `TrimmerRootAssembly` entry from its
actual managed assembly identity. That preserves every executable method for
native AOT rather than guessing which indirect lifecycle/helper calls are
reachable. Its `.dll` and `.aotdata.arm64` are static product members, not
on-device mod-loader inputs. The package gate compares the frozen and linked
assemblies and rejects any lost executable method body. It then resolves the
external DLL's complete Celeste/Everest member-reference surface against the
linked product API and verifies native bodies in both AOT outputs (the LLVM
object and its companion Mono object). A missing API or method body therefore
fails the Mac build instead of falling back to JIT on an AOT-only device.

The bounded compatibility facade includes Everest's nested content fields and
typed interpolated logger contract using normal AOT-safe managed code. Its
tag-prefix minimum-level policy matches the relevant Everest behavior, so a
release mod that selects `Info` does not turn legacy `Logger.Log` (`Verbose`)
calls into per-frame device I/O. This is
binary API compatibility for the exact accepted external DLL, not a general
desktop Everest runtime implementation.

## Signature-driven managed detour lowering

Every supported target is declared once in a reviewed signature catalog. From
that catalog the Mac builder generates the public HookGen-compatible event,
typed `orig`/hook delegates, cached dispatcher, direct-hook registration entry,
owner cleanup, and the one-time canonical Celeste target-body rewrite. This
replaced the three historical hand-written Dialog, ParticleSystem, and
TrailManager dispatchers with one mechanism.

Each generated dispatcher caches its active chain, preserves pinned MonoMod
subscription/order semantics, passes a typed `orig` delegate, allows argument
and return-value changes, honours handlers that intentionally do not call
`orig`, and tracks the owning module for enable/disable and unload cleanup. The
no-hook path is a cached direct typed call with no per-call allocation.

The first canary proves two independent handlers around
`Celeste.Dialog.Clean`: module B enters, module A enters, the original runs,
then A and B return. It also exercises owner disable/re-enable and duplicate
subscription semantics. This is a bounded mechanism registry; arbitrary
MonoMod hook compatibility is not implied.

Stage 25C additionally registered the exact five distributed
`On.Monocle.ParticleSystem.Emit` overloads and exact
`On.Celeste.TrailManager.Add` overload used by Particle Palette Helper 1.0.0.
The hot chains are rebuilt only when subscription/module state changes; normal
particle emission does not allocate a new compatibility chain per call.

Stage 25D-C registers nine additional Celeste targets used by Feather Maddy
1.3, plus `On.Monocle.Engine.Update` and `Celeste.Player.Die`. Feather Maddy is
the former deferred HookGen fixture: its ordinary distributed DLL now enters
the same generated backend without a mod-specific transformer.

Stage 25F-B2 expands the same reviewed catalog from 18 to 51 targets for the
exact ConditionHelper 1.0.0 and AchievementHelper 1.0.5 binary closure. The
complete census contains 29 provider events and five consumer events; only
`Level.LoadLevel` was already present, so exactly 33 descriptors were added.
The signature-driven generator now proves an eight-explicit-argument static
target, two `IEnumerator` targets, two clone/reference return targets, exact
overload selection, and distinct inherited `Entity` and `Player` lifecycle
targets. Each canonical target body is still rewritten exactly once. The
expansion contains no helper-name checks and no speculative Celeste methods.

For a direct `Hook`, the Mac host recognises a narrow fixed-IL construction
pattern, resolves the exact target and detour signatures against the catalog,
validates their typed `orig` contract, records a static plan, and replaces the
reflection sequence with a plan identifier. The device facade changes only
typed registration data. `Apply`, `Undo`, `Dispose`, `IsApplied`, and `IsValid`
share the same chain and owner lifecycle as HookGen events; no executable code
is patched and no target reflection occurs on-device. Each generated direct
plan writes one bounded first-invocation diagnostic record so physical evidence
proves execution rather than merely module loading. Lag Pauser 1.3.0 is the
first real mixed fixture: HookGen wraps `Engine.Update`, while its direct static
detour wraps `Player.Die`.

Dynamic target selection, dynamically chosen detour delegates, property
targets not present in the catalog, unsupported `DetourConfig`/context use,
finalizer-dependent lifetime, and unregistered `ILHook`/`IL.*` remain explicit
fail-closed classes. The small data-only `DetourConfig` implementation is exercised by the
project's conformance canaries; third-party configuration surfaces remain
deferred until an exact distributed input is accepted.

## Build-time frozen HookGen IL

Desktop HookGen `IL.*` normally constructs an `ILHook` and rewrites a live
method. Stages 25H-A/B use a different, bounded Apple mechanism:

```text
exact release ZIP + precompiled DLL
  -> discover exact IL event add/remove registrations
  -> require exact normalized target-body SHA-256
  -> isolated Mac host invokes real pinned manipulator A
  -> validate/hash the intermediate body
  -> invoke B, then C, in pinned registration order where registered
  -> validate/hash every intermediate and the final semantic diff
  -> lower reviewed dynamic delegate cells to ordinary rooted calls
  -> remove device IL subscriptions and host-only manipulator infrastructure
  -> root injected ordinary methods
  -> full trim + full AOT for the shared iOS/tvOS closure
```

`STATIC_IL_EVENT_FREEZE` accepts one reviewed HookGen manipulator per target;
`STATIC_IL_EVENT_SEQUENCE` accepts two or more exact ordinary unconfigured
registrations. The dedicated host worker receives explicit
hash-pinned assemblies, target and manipulator identities, and output paths.
It uses pinned MonoMod commit `dfc30a1506d37fb88a2c2be004f525205f46a24c`
and fails before mutation if the target fingerprint differs. A throwing
manipulator, failed cursor match, malformed branch/exception region, changed
after/diff hash, unresolved member, or final reference to Cecil, `MonoMod.Cil`,
`ILHook`, Reflection.Emit, DynamicMethod, or assembly loading fails the build.
In particular, no `DynamicReferenceManager` cell survives.

The selected Dash Toggle Helper 1.1.0 release uses noncapturing
`EmitDelegate`. Pinned MonoMod resolves those delegates while building into
ordinary direct calls to statically rooted methods in the frozen mod DLL; no
dynamic-reference cell is present in final device IL. Stage 25H-B's real
Disposable Theo 1.0.6 DLL broadens that lowering to compiler-generated
noncapturing singleton lambdas. The worker preserves the delegate's typed
runtime parameters and return value using normal locals, the exact rooted
`<>c` singleton field, and an ordinary method call. It never serializes the Mac closure object.
Captured constants, module/runtime state, mutable or
transient captures, and dynamically selected delegates remain deferred.

For a same-target sequence, pinned MonoMod ordering is reproduced exactly:

```text
canonical baseline body
        ↓
manipulator A → validate/hash
        ↓
manipulator B → validate/hash
        ↓
manipulator C → validate/hash (when present)
        ↓
delegate/reference lowering and final validation
        ↓
normal typed On/direct-Hook wrapper boundary
        ↓
full trim + full AOT
```

Resolved dependency/module load order is primary; declaration order within a
module is secondary. The complete ordered sequence—not independently persisted
partial patches—owns the target. Duplicate registrations, ordinal gaps, a
no-op, an altered baseline, malformed intermediate branches, and changed
intermediate/final hashes fail closed. The project-owned order-sensitive
A→B/B→A/A→B→C conformance matches pinned desktop MonoMod. A real high-value
two-manipulator same-target fixture was not found, so that physical evidence is
still explicitly pending.

Transform order is shared and platform-neutral. The reviewed managed-detour
rewrite first creates the canonical typed `On.*` wrapper/original boundary;
the IL worker then freezes the manipulator into the underlying original body.
Consequently a same-target `On.*` hook's `orig` delegate observes the
IL-modified implementation, as pinned desktop behavior requires. iOS and tvOS
consume one identical pre-platform closure and do not apply platform-specific
IL patches.

The original `IL.* +=` and `IL.* -=` callsites do not create or remove device
hooks. The module is immutable-active for that installed build. Runtime unload
or live disable is unsupported; removing the mod means rebuilding and
reinstalling a closure which omits it, at which point the exact baseline
method remains untouched. Multiple manipulators on one target, direct
`new ILHook(...)`, configured IL ordering, dynamic targets, captured delegates,
and runtime unapply are later compatibility classes.

## Current real-ZIP evidence

The current internal command is:

```bash
scripts/build-apple-everest-real-mods.sh \
  --mods "/path/to/an-explicit-directory-of-zips" \
  --platform all \
  --signing unsigned \
  --clean
```

This remains contributor infrastructure and is deliberately not exposed by the
beginner `build-ios.sh` or `build-tvos.sh` entry points. It accepts only the
closed compatibility mechanisms above; it is not an arbitrary Mods-folder
implementation.

**BINARY-COMPATIBLE** means the ordinary release DLL is accepted and
transformed generically without a mod-specific source edit. **SOURCE-PORTED**
means a source change was required for Apple. No Stage 25C success fixture is
reported as binary-compatible if it needed that.

The Stage 25E selected closure contains six ordinary public ZIPs: the four
Stage 25C/25D fixtures above, *Cpop Helper* 1.3.0, and its real dependent map
*QuizSample* 0.0.1. All four managed mods are
`BINARY-COMPATIBLE_WITH_STATIC_TRANSFORM`: their ordinary DLLs are accepted
without source trees or mod-specific source edits. Particle Palette Helper's
optional palette-file content API is not yet implemented, so its evidence
covers module/hook execution and vanilla pass-through rather than custom
palette deserialization.

The exact tested-mod matrix is in
[Apple Everest compatibility](APPLE_EVEREST_COMPATIBILITY.md).

## Helper graphs, gameplay registries, and Mod Options

Stage 25E adds the first ordinary map-to-helper dependency graph. The pinned
`QuizSample` metadata requires Cpop Helper, so the graph resolver orders the
helper before the map and rejects a missing or too-old helper before AOT. The
helper cannot be disabled while the dependent map is present. This is a
generic dependency rule, not a Cpop-specific runtime patch.

For a precompiled helper DLL the Mac builder reads pinned Everest
`CustomEntity` and `CustomBackdrop` metadata with Mono.Cecil. It emits direct,
strongly typed entity, trigger, and backdrop factories plus AOT roots; map
binaries are independently inspected so the compatibility manifest records
which real IDs resolve to which owning module. Device code performs a switch
over those exact IDs and calls normal constructors or reviewed static
factories. It never calls `Assembly.GetTypes`, `Activator.CreateInstance`, or a
runtime mod loader. Duplicate IDs and unsupported constructor/factory shapes
fail on the Mac.

Everest's own `everest/coreMessage` entity is a separate, pinned MIT-licensed
core factory. It is included explicitly because real dependent maps can use
Everest core entities even when their helper DLL does not declare them. The
lab lists every staged map through one generated `MapPaths` inventory, so a
dependent map can be launched deliberately rather than relying on ZIP order.

The selected real map resolves `quizController` and `quizAnswerTrigger` from
Cpop Helper. Cpop declares four custom entities and three custom triggers in
total. The selected pair has no custom backdrop; the pinned Everest
`ID[=factory]` backdrop convention is covered by a generated deterministic
canary rather than adding an unrelated physical mod.

This is the first production proof in which a real **custom entity** and a
real **custom trigger** are both instantiated from ordinary dependent-map
data through the generated registry.

QuizSample contains four rooms which deliberately exercise Cpop's `Text`,
`Image`, and `HighResImage` answer modes. The distributed dialog fragment
defines the answer digits but omits the four question/choice keys referenced
by its `everest/coreMessage` entities (`PickTheEvenNumber`,
`IsThisNumberEven`, `Yes`, and `No`). Celeste therefore displays its normal
`XXX` missing-dialog fallback for those labels. Answer values also reroll
after death because Cpop includes the session death count in its quiz seed.
These are properties of the exact public sample fixture, not static-AOT
rendering or dialog-loader failures; the builder does not invent replacement
map text.

Cpop also consumes four members which pinned desktop Everest publicizes:
`Actor.movementCounter`, `Glider.sprite`, `Glider.destroyed`, and
`Glider.DestroyAnimationRoutine`. They are the only Stage 25E additions to the
reviewed Apple API surface. `TagsExt.SubHUD` is supplied as a small shared
Everest API facade and uses Celeste's existing high-resolution HUD pass; no
broad publicizer or separate desktop SubHUD event system is shipped.

Simple `EverestModuleSettings` properties are also discovered on the Mac and
become typed Celeste `TextMenu` entries under Mod Options. The current bounded
production shapes are `bool`, enum, and `int` with an explicit small
`SettingRange`. Unsupported or unbounded properties are listed in the
manifest rather than guessed. The current real closure exposes Feather Maddy's
three booleans and Lag Pauser's `Enable` boolean.

One versioned 16 KiB logical settings document is shared by both Apple
platforms. On iOS/iPadOS it is atomically stored in private Application
Support at `AppleEverest/ModuleSettings.v1`; it is not a live Documents file.
On tvOS the same logical data uses the separate bounded app-private
`CelesteAppleEverest.Settings.v1` `NSUserDefaults` key. It is outside vanilla
Settings, SaveData, compressed tvOS save generations, and Save Manager.
Malformed, truncated, wrong-type, duplicate, or oversized data falls back to
normal module defaults without changing vanilla state.

## Static MonoMod ModInterop plans

Pinned desktop MonoMod implements its common interop facility dynamically:

```text
Type.ModInterop()
  -> reflect public static methods and delegate fields
  -> store MethodInfo entries by logical name
  -> Delegate.CreateDelegate for compatible imports
```

Stage 25F-B preserves those observable semantics while moving discovery to the
Mac:

```text
ordinary distributed mod DLLs
  -> Mono.Cecil resolves literal typeof(T).ModInterop() calls
  -> generated export/import candidates and typed AOT roots
  -> device Type.ModInterop() marks one known plan registered
  -> direct typed delegate fields are refreshed
  -> subsequent calls are ordinary delegate calls
```

Registration remains per type, idempotent and process-local. Each public static
method is available under both its unqualified name and its assembly-name (or
`ModExportName`) prefix. Imports are public static delegate fields and honour
field-level and type-level `ModImportName`. The generated plan keeps pinned
registration order, skips incompatible same-name methods, selects the first
compatible registered provider, and refreshes already-registered importers
when a provider appears later. Missing optional providers leave fields null.

The device facade retains only the exact `MonoMod.ModInterop` ABI expected by
the precompiled DLLs. It has no `GetMethods`, `GetFields`, `MethodInfo` registry,
`Delegate.CreateDelegate`, assembly loading, executable mutation or general
MonoMod.Utils runtime. Dynamic registration types, open generic surfaces,
readonly imports and unsupported delegate shapes are rejected on the Mac.

The selected real ConditionHelper/AchievementHelper binaries prove all four
qualified imports resolve in one deterministic plan. Stage 25F-B2 completes
their bounded HookGen closure while preserving the exact plan SHA-256
`9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318`.
The project-owned acceptance package contains data only: its
`totalDeaths() > 0` definition makes the ordinary AchievementHelper call the
ordinary ConditionHelper exports. A real `SaveData.AddDeath` event drives
reevaluation and displays **First Apple Death** on iPhone, iPadOS 15.8.8, and
Apple TV.

AchievementHelper's `ButtonBinding` setting exposed one physical-only startup
ordering issue: pinned Everest constructs the logical binding before Celeste
input exists, then attaches the `VirtualButton` after input initialization.
The generated registry now follows that typed two-phase lifecycle. It neither
reflects settings nor dereferences the gamepad during module construction.
The exact Everest-publicized `TextMenu.Items` and `TextMenu.SubHeader(string)`
surface and the helper's bounded YAML achievement asset were likewise supplied
as narrow shared Apple APIs. No provider or consumer source patch was needed.

## Module settings, SaveData, and Session

Stage 25F-A adds a separate, deliberately bounded compatibility class for
pinned Everest's default YAML `EverestModuleSaveData` and
`EverestModuleSession`. These three concepts remain distinct:

- module **Settings** are global app preferences in the existing 16 KiB
  `ModuleSettings.v1` store;
- module **SaveData** belongs to one numbered Celeste slot;
- module **Session** belongs to that slot's current continuation and is reset
  when Celeste starts a genuinely new `Session`.

The Mac analyser finds the two declared root types in the precompiled mod DLL,
walks their closed property graph, validates constructors and writable YAML
properties, and emits direct typed factories plus serializer methods. The
current supported graph includes primitives, enums, nullable values, arrays,
`List<T>`, string-keyed `Dictionary<string,T>`, nested classes/records, and
FNA `Vector2`. It rejects external/dynamic object types, excessive depth or
type count, custom serializers, custom IO, legacy synchronous save methods,
and binary SaveData before AOT. The generated reader accepts ordinary bounded
block YAML from pinned desktop YamlDotNet and deterministic JSON flow YAML; it
rejects runtime type tags, anchors, aliases, directives, duplicate keys,
oversized values, and unknown construction paths.

During a real Celeste save request, module objects are serialized immediately
beside the already-serialized immutable base-save bytes. Repeated requests
during an active save coalesce into one bounded latest-state follow-up. The
shared logical transaction is:

```text
                    module objects
                         |
                   save snapshot
                         |
                shared serialized state
                         |
             base-save SHA-256 match
                    /          \
                  iOS          tvOS
       Application Support     bounded UserDefaults
          A/B snapshots        compressed A/B snapshots
                    \          /
                    load/recovery
                         |
                typed module restore
```

Each numbered-slot aggregate records its schema, slot, generation, exact base
save SHA-256, static durability-closure identity, module ID/version/type-graph
schema, and separate SaveData/Session payload hashes. The highest valid A/B
generation matching both the exact base save and closure is selected. This
provides coherent primary/previous-good recovery across the unavoidable
base-save/sidecar crash window. If neither replica matches, module objects use
fresh defaults; stale state is never attached to an imported or recreated
slot. A valid aggregate may isolate one bad module payload, but a broken
aggregate checksum or structure fails closed as a whole.

iOS/iPadOS stores private replicas below Application Support at
`Celeste/Everest/Slots/<N>/module-state-v1.{a,b}.snapshot` using atomic
Foundation writes. The logical aggregate is bounded to 2 MiB and every module
SaveData or Session payload to 512 KiB. tvOS stores the same logical model in
the separate keys `CelesteAppleEverest.Slot<N>.State.{A,B}.v1`, compressed with
the existing managed Deflate strategy. tvOS admits at most 512 KiB expanded
and 126,976 bytes per compressed replica, with six replicas (A/B for slots
0–2) capped at 761,856 bytes total. An oversized or unverifiable candidate is
rejected before it can replace previous-good data.

DeathMarkers 2.0.0 is the first real positive fixture. Its ordinary public
precompiled DLL uses default async YAML for both a persistent
`Dictionary<string,List<Death>>` and current-session `List<Death>`, with nested
`Room`, `Vector2 Position`, and `Amount` state. The logical YAML boundary
round-trips semantically with pinned desktop YamlDotNet without a source build
or runtime reflection.

This does not make arbitrary module persistence compatible. Binary module
data, custom serializer overrides, custom IO, legacy synchronous APIs, and
dynamic property graphs remain deferred. The Cpop/QuizSample map launcher also
remains an isolated slot `-1` debug lane: it is deliberately nonpersistent and
Save and Quit remains suppressed. General Everest LevelSet/custom-map
progression is separate future work.

## Graph-driven ordinary IL-event breadth

Stage 25H-C re-audited all 18 exact Stage 25G IL-primary graphs from freshly
downloaded, hash-verified public releases before choosing another mechanism.
The selected path is still the existing `STATIC_IL_EVENT_FREEZE` class—not a
new runtime architecture—because ordinary HookGen IL events have the broadest
safe graph payoff and 213 of the 226 relevant `EmitDelegate` sites are already
covered by the accepted static-method or compiler-singleton lowering classes.

VortexHelper 1.2.19, from the exact Space Trip graph, is the first registered
fixture whose two IL subscriptions live in entity-local `Hook`/`Unhook`
methods instead of an Everest module's `Load`/`Unload`. The Mac validates the
exact nested registration sites, runs the two distributed manipulators, and
freezes `Player.NormalUpdate` and `Player.WallJumpCheck`. The device rewrite
then reconstructs the neighbouring ordinary `On.*` registrations, removes
only the exact IL-event subscriptions and host-only manipulator code, and roots
the surviving static method/compiler singleton. Source remains audit evidence;
the public release DLL is authoritative and sufficient for the transform.

This is deliberately fail-closed. An extra IL subscription, changed metadata,
changed DLL, changed baseline/after/diff hash, direct `ILHook`, capture outside
the accepted classes, or forbidden final reference rejects the helper. The
installed profile remains immutable-active and does not claim live unpatching.
Space Trip is not yet product-compatible: LunaticHelper configured ordering,
VortexHelper `DynamicData`, uncatalogued ordinary hooks/API/content, and custom
audio remain separate blockers. No product was built merely to discover those
already classified failures.

The deferred IL rung uses the ordinary GoldenTrainer 1.5.4 release. Its DLL
contains `IL.Celeste.SummitCheckpoint.Update` plus a direct `ILHook` on
`Player.orig_Die`; the host analyzer now identifies both from CLI metadata.
The manipulators capture live module settings/state, and several accompanying
`On.*` targets remain outside the bounded catalog, so this release is correctly
rejected before AOT rather than partially frozen. Dashless Dream Blocks is
retained separately as a direct-`Hook` negative fixture.

## Bounded static direct ILHook freeze

Stage 25H-D adds one deliberately narrow direct-construction class:
`STATIC_DIRECT_ILHOOK_FREEZE`. Desktop Everest normally executes the following
at module load:

```text
new ILHook(exact MethodBase, exact manipulator)
  -> immediate live runtime mutation
  -> stored hook remains active
  -> Dispose only during module unload/process teardown
```

For an accepted Apple build, the Mac instead discovers that exact constructor
in the distributed DLL, proves the target, manipulator, absent configuration,
and immutable-effective lifetime, verifies the canonical target fingerprint,
runs the real distributed manipulator through the same pinned worker, and
freezes its output. The exact constructor, storage field, and teardown-only
`Dispose` are removed from the device assembly before ordinary full AOT:

```text
distributed binary constructor
  -> static target/manipulator/lifetime proof
  -> pinned real manipulator runs on Mac
  -> target body hash-locked and frozen
  -> runtime constructor/lifecycle removed
  -> ordinary linked and AOT-compiled code
```

CaeruleaHelper 1.11.1 is the first production fixture. Its static
`DashSpeedHook.Load` resolves `Player.DashCoroutine`'s compiler-generated
`MoveNext`, passes static `ModifyDashCoroutineIL`, supplies no `DetourConfig`,
and relies on the constructor's immediate apply. The hook is stored in one
static field and is disposed only by module unload. Its real No Dash Speed
Reset trigger therefore remains observable while the installed profile is
immutable-active. Omitting the helper from a later build restores the exact
canonical target; no runtime unfreeze is promised or needed.

The device contains no `ILHook` facade or patching backend. Configured hooks,
runtime-resolved targets or manipulators, gameplay/room-scoped construction or
disposal, `applyByDefault=false` with live `Apply`, and multiple direct hooks
on one target remain deferred. Those are different semantic classes and are
not silently made permanent. The complete 53-site classification is in
[`apple-everest/direct-ilhook-audit-stage25hd.json`](../apple-everest/direct-ilhook-audit-stage25hd.json).

Project-owned conformance executes the actual pinned desktop
`HookEndpointManager.Modify`, direct `ILHook`, and
`HookEndpointManager.Add` entry points. It proves both direct/event
registration orders, proves a normal On-style `orig` observes the frozen
direct-IL target, and proves an H-B A/B sequence followed by a direct
manipulator and On wrapper. Those exact results are compared with the schema-3
Apple worker; no runtime IL backend is added to the device.

## Content model

Accepted content is compiled into the normal Celeste content tree on the build
host. The canary includes a deterministic dialog entry, map, and generated
visual asset. Package precedence follows the resolved Everest graph: later
packages replace the same logical path, and the manifest records both the
winning bytes and their owner. The resulting content is bundled normally and
requires no on-device virtual filesystem or ZIP reader.

## Product isolation and full AOT

The experiment builds separate **Celeste Everest Canary** application
identities and containers. It never overwrites vanilla app data. The iOS/iPadOS
canary uses ordinary Application Support files; the tvOS canary uses a separate
test persistence namespace. The accepted vanilla builders remain unchanged.

Both canary products must remain Release, fully trimmed, full AOT, and
`UseInterpreter=false`. Device/package verification rejects
the desktop `MonoMod.RuntimeDetour` and HookGen backends, NLua/KeraLua,
dynamically discovered mod DLLs, native mod libraries, desktop
Everest services, and unexpected executable content.

## Current limits

This foundation does **not** promise arbitrary Everest mods, runtime mod
installation, runtime enable/disable of code outside the prebuilt registry,
unregistered IL hooks, any IL hook on-device, arbitrary direct detours, Lua, native mods, content hot reload,
Everest networking/updating, dependency downloading, general mod
settings shapes, module SaveData/Session outside the exact bounded default-YAML
class, the complete Everest virtual-content API, or
desktop parity. Only
exactly analysed packages and explicitly registered mechanisms can enter the
closure. Extending support requires a new deterministic transform plus desktop
reference, AOT, device, isolation, and performance evidence.

In short, this is not general Everest support.

For third-party provenance and licensing, see
[Apple Everest third-party notices](APPLE_EVEREST_THIRD_PARTY.md).
