# Stage 25F-A — real Apple Everest module durability

Status: **PASS — GREEN**

Date: 2026-08-21

Stage 25F-A adds a bounded, statically generated implementation of pinned
Everest's default-YAML `EverestModuleSaveData` and `EverestModuleSession`
contracts to the shared Apple full-AOT experiment. One ordinary public
DeathMarkers 2.0.0 release DLL is the positive real fixture. Its nested state
is saved beside, but never inside, the numbered vanilla save; the same logical
transaction and typed restore path serves iOS, iPadOS, and tvOS.

This remains a closed compatibility lab, not arbitrary Everest persistence.
Binary SaveData, custom serializers, custom IO, legacy synchronous persistence,
dynamic property graphs, and runtime module loading remain fail-closed.

## Git and immutable boundaries

| Item | Accepted value |
| --- | --- |
| Starting commit | `c4de9040cf83a2132f993b62d3416fdc8b8c0a32` |
| Feature branch | `feature/apple-everest-module-durability` |
| Production commit | `f0482296d649b46e25d091bedafa26b40d3a08e2` |
| Acceptance-record commit | The commit containing this report |
| Final feature commit | The final response records its exact full SHA |
| Everest stable source | `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00` |
| MonoMod source | `dfc30a1506d37fb88a2c2be004f525205f46a24c` |
| iOS recovery tag | `ios-v0.1.1-rc.1^{}` -> `27e16b4724d94d3991b99c4795f680fcb0e5830c` |
| tvOS RC1 | `v1.0.0-rc.1^{}` -> `ee52b0868df091746f134d95d4f020f94f23d4fb` |
| tvOS RC2 | `v1.0.0-rc.2^{}` -> `641e86e4ed164cdf93f602ce2f11436449654d6e` |
| Deferred RC3 branch | `origin/release/v1.0.0-rc.3` -> `c8134c8ca7924cf12f48527e714b5242c6024927` |
| RC3 tag | Absent |

No integration/release branch, recovery tag, GitHub workflow, cloud-builder
template, or native dependency changed. No GitHub Actions workflow was run.

## Candidate audit and real fixture

Ten public, source-auditable releases were classified. The audited set covers
default SaveData and Session, empty and nested graphs, custom serializers,
runtime IL/detour dependencies, and wider helper-graph blockers. The complete
machine-readable record is
[`module-durability-audit-stage25f.json`](../../../apple-everest/module-durability-audit-stage25f.json).

DeathMarkers was selected because it gives directly observable persistent and
continuation state with a nontrivial, bounded graph while already using the
accepted `Player.Die` HookGen target.

| Property | Exact result |
| --- | --- |
| Release | DeathMarkers 2.0.0 |
| Release page | <https://gamebanana.com/mods/53649> |
| ZIP SHA-256 | `94ad7d14fec6fb500f811ef09f46f008f444b45aafcdd8c2e8b86ce5d3ee6fc7` |
| Original DLL SHA-256 | `620e5b639b057a46890e7f0ed8a828fadb422a7b4adcd6b1c53bbb6568acdff8` |
| Frozen DLL SHA-256 | `e78087ee336ae8007e0df65616d7a7f1d9493d24163351eb24d2550e96d2dc47` |
| Audited source | `24c9b8214c69dd10ce3a3efc1d08b4bb042d1965` |
| Source/license | <https://github.com/oatmealine/DeathMarkers>; MIT |
| SaveData | `Dictionary<string,List<DeathMarkersSession.Death>> Deaths` |
| Session | `List<DeathMarkersSession.Death> Deaths` |
| Nested record | `Room: string`, `Position: Vector2`, `Amount: int` |
| Serializer | Pinned Everest default YAML for both objects |
| Async policy | Default `SaveDataAsync=true` |
| Overrides | No binary, custom serializer, custom IO, or legacy sync override |

The ordinary precompiled DLL is authoritative. Source was used only to audit
behavior and licensing and is not required to regenerate the final closure.
The host walks the frozen assembly's declared graph with Mono.Cecil and emits
strongly typed factories, readers, writers, trimmer roots, and AOT roots.

The physical iPad test found one real binary assumption: changing Mode in a
live Level before the first death indexed a missing per-area SaveData bucket.
The final generated adapter establishes that empty bucket for the exact pinned
DeathMarkers Mode property, then calls the unchanged release setter. This
narrow compatibility guard fixed the crash without source-compiling or
forking the mod and is locked by deterministic source tests.

## Serializer and desktop interoperability

The supported graph is closed before AOT. It includes primitive and enum
values, nullable values, arrays, `List<T>`, string-keyed
`Dictionary<string,T>`, nested classes/records, and FNA `Vector2`. The graph
has bounded type/depth/node/scalar limits. Unknown external types, YAML runtime
tags, anchors, aliases, directives, duplicate keys, invalid UTF-8, and
oversized input fail closed.

Generated Apple logical YAML round-tripped through pinned desktop YamlDotNet,
and representative pinned desktop YAML restored through the Apple typed reader
with equivalent nested state. No runtime reflection, dynamic activation,
source generator, or device-side assembly scan participates.

Current compatibility classification is:

- default async YAML SaveData and Session: supported only after exact graph
  analysis and typed-code generation;
- binary SaveData: deferred;
- custom serializer: deferred;
- custom IO: deferred;
- legacy synchronous SaveData: deferred.

## Aggregate and storage authority

`AEVMSV1` is one deterministic per-slot aggregate. It records the numbered
slot, monotonic module generation, exact raw vanilla base-save SHA-256, static
closure identity, and ordered module name/version/schema entries. SaveData and
Session are separate nullable payloads with individual hashes; the complete
aggregate has its own SHA-256. A structurally valid aggregate can isolate one
bad module payload, while an invalid aggregate fails as a whole.

The common authority validates A and B independently, selects the highest
valid generation matching both base-save hash and closure, writes the older
replica, reads it back, and compares the exact encoded logical state before
advancing the in-memory selection. A failed, truncated, or unverifiable write
leaves previous-good state selectable. If neither snapshot matches the current
base save, every affected module receives its ordinary default; stale sidecar
state cannot attach to an imported, restored, deleted, or recreated slot.

Storage is platform-specific only below that shared authority:

- iOS/iPadOS: private Application Support at
  `Celeste/Everest/Slots/<N>/module-state-v1.{a,b}.snapshot`, using atomic
  Foundation writes;
- tvOS: app-private, separate compressed keys
  `CelesteAppleEverest.Slot<N>.State.{A,B}.v1`.

iOS admits a 2 MiB aggregate and 512 KiB per module object. tvOS admits 512 KiB
expanded per aggregate and 126,976 bytes per compressed replica. Six A/B
replicas for slots 0–2 therefore have an explicit 761,856-byte total ceiling.
Managed Deflate plus the `AEVMZV1` length/hash envelope is used on tvOS. These
limits leave headroom inside the previously accepted app-private defaults
budget while rejecting pathological histories before storage.

The largest observed real acceptance snapshot contained 4,273 bytes of
DeathMarkers SaveData, 19 bytes of Session, and 5,053 bytes in the complete
four-module aggregate. The explicit continuation proof recorded 3,621-byte
SaveData and a 478-byte Session within a 4,860-byte aggregate. Initial real
state was 448/19/1,228 bytes respectively. All are far below either platform
budget; compression and round-trip integrity were independently exercised by
deterministic boundary and corruption tests.

## Lifecycle integration

Module objects are serialized immediately beside the immutable already-
serialized vanilla bytes during the normal `UserIO.SaveRoutine`. Only after
the vanilla file write succeeds does the prepared module replica commit. A
failed vanilla or module write discards the candidate. Repeated save requests
while `UserIO` is active are coalesced into one bounded latest-state follow-up,
not an unbounded queue.

File select preloads all numbered sidecars on its existing worker path.
`SaveData.Start` applies persistent SaveData and `StartSession` applies the
matching continuation Session. Starting a genuinely new vanilla Session
creates fresh module Session objects while retaining module SaveData. Slot
delete removes A and B. An imported/replaced/restored base save selects module
state only when its exact raw hash matches; otherwise modules default until a
new coherent transaction is saved.

Global Mod Settings remain in `AppleEverest/ModuleSettings.v1` on iOS and
`CelesteAppleEverest.Settings.v1` on tvOS. They are not slot state. The
Cpop/QuizSample debug-map lane remains slot `-1`, deliberately nonpersistent,
and Save and Quit remains disabled there.

## Deterministic durability evidence

The host tests cover typed YAML round trips and desktop interoperability;
invalid UTF-8, unknown types, duplicate keys, tags, aliases, depth, node,
scalar, per-module and aggregate limits; A/B ordering; base/closure mismatch;
single-module corruption; full-aggregate corruption; compression boundaries;
two-slot isolation; delete/recreate; replacement mismatch; and injected throws
before/after write plus truncation. All injected failures preserve the last
matching previous-good generation and never mutate vanilla save bytes.

The final builder suite contains 203 checks, including the exact live-Level
DeathMarkers Mode guard. Stage 25F's product verifier additionally locks the
fixture identities, v5 analyser policy, serializer bounds, aggregate schema,
storage namespaces, lifecycle transforms, closure hashes, package AOT members,
forbidden runtime surface, release refs, and unchanged native/canonical locks.

## Shared closure and product evidence

| Boundary | Accepted result |
| --- | --- |
| Managed closure | 26 files; `e0ce8566a19bbdf8015fe3b4788cff7224471e234668f8d3446f5427537df410` |
| Content closure | 49 files; `3532ed2c76718e12340c9e119b69c2ed17e5066371f9b2bef3c29c2282260edf` |
| Durability closure | 4 typed adapters; `62868c5fa8a2c2167e45b9eb56b3069624377bb1cf8c800412f862a93bad7048` |
| Complete shared closure | `b85472c06b68757bc02de8a33171890df15015924178024c72da48556d8a3a01` |

Exactly this closure feeds both Apple targets. The signed products are:

| Product | IPA bytes | SHA-256 | Build time | Contract |
| --- | ---: | --- | ---: | --- |
| iOS/iPadOS | 882,962,993 | `bd877e3bbd5ff3d307d5b40dea9b7f3ab8dd73ca469388ec836b0d9709a13c7c` | 273 s | arm64; iOS 15; Release; full trim/AOT; no interpreter/JIT |
| tvOS | 897,337,616 | `0a840df42b52c0896a9256f8fb5b54d7fadb2af15ceac74f9dbda2286f582066` | 353 s | arm64; tvOS 16; Release; full trim/AOT; no interpreter/JIT |

All five external managed DLLs are rooted and have matching arm64 AOT data.
Post-link verification resolves their referenced APIs and native bodies. The
runtime/package scan excludes `Assembly.Load`, `AssemblyLoadContext`,
Reflection.Emit, `DynamicMethod`, `DynamicInvoke`, the device HookGen and
RuntimeDetour backends, NativeDetour, ILHook, Lua, process launch, and file
watchers.

## Physical acceptance

### iPhone 12 Pro Max

The same-identity signed canary restored the existing slot, recorded visible
DeathMarkers across deaths and respawns, saved and quit, cold-restored both
persistent history and continuation state, reset Session for a genuinely new
vanilla Session while retaining SaveData, isolated a second slot, deleted and
recreated it, and recovered previous-good state after controlled corruption.
Touch/controller, seven-bank audio, Stage 25E modules/map, Home/reopen, and
normal vanilla progression stayed functional. The user reported all final
steps passed.

### iPadOS 15.8.8

The universal signed product was installed as a same-identity replacement.
The initial run exposed the live-Level Mode setter's missing-bucket assumption
described above. The corrected final build then passed Mode
Session -> Save Data -> Session in a live Level, markers, Save and Quit,
app-switcher cold restore, landscape/touch/controller/audio, and responsive
foreground behavior. The user reported the corrected pass succeeded.

### Apple TV

The signed tvOS product was installed as the separate Everest canary identity.
Controller, seven-bank audio, numbered-slot load, DeathMarkers persistent and
continuation behavior, save/cold restore, new-session reset, second-slot
isolation, delete/recreate, Home/reopen, the Stage 25E helper ecosystem, and
normal tvOS lifecycle all passed. Storage remained in the separate bounded
module namespace and did not alter the accepted vanilla persistence keys. The
user reported the complete focused pass succeeded.

No material gameplay, frame, audio, input, load-time, or memory regression was
observed at the small real payload sizes. Serialization occurs at save
boundaries; normal gameplay uses the restored typed objects directly.

## Regression, privacy, and immutable locks

The Stage 25E helper/map/options graph and Stage 25D typed HookGen/direct-Hook
chain remain in the identical closure. Vanilla iOS and tvOS builders remain
mod-free and use their existing storage authorities. Accepted hashes remain:

| Boundary | SHA-256 |
| --- | --- |
| Canonical Content | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw source | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched source | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Stage 6 real-audio source | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| Vanilla iOS generated tree | `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357` |
| iOS native | `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2` |
| tvOS native | `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc` |

Repository/privacy/license checks found no tracked Celeste, FMOD, mod ZIP/DLL,
generated game source, save, IPA/app, signing material, device identity,
private path, or credential. All third-party fixture bytes remain ignored and
are fetched by exact public URL/hash for validation. No analytics, network,
permission, native library, or cloud workflow was added.

## Limitations and next stage

This stage does not provide general Everest persistence, custom-map/LevelSet
progression, binary module state, custom serializer/IO, legacy synchronous
SaveData, ModInterop, arbitrary helper graphs, IL manipulation, custom audio,
Lua, or native mods. If no snapshot matches an imported base save, modules
safely use defaults; module state cannot resurrect by itself.

The highest-value next major compatibility class is **Stage 25F-B: bounded
static/common MonoMod ModInterop**, because shared helper APIs unlock more real
mods without taking on IL rewriting. A modest multi-helper graph should follow
that. Deterministic `IL.*` lowering remains later and must stay exact-target and
host-generated. A public fixture can be pinned and fetched automatically, so
no manual download should be necessary unless its upstream host prevents
reliable lawful acquisition.

This materially advances ordinary medium-complexity maps: real helper code,
custom entities/triggers, options, SaveData, and Session now share one Apple
closure. The largest remaining Strawberry Jam blockers are broad ModInterop,
large multi-helper dependency graphs, deterministic IL/ILHook lowering,
broader Everest APIs/content virtualization, custom audio, and the Lua/native
policy. Strawberry Jam is still not a rational product target yet.
