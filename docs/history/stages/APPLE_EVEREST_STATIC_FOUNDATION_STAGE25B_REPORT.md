# Stage 25B — production shared Apple Everest static foundation

Status: **PASS — GREEN**

Date: 2026-08-18

Stage 25B turns the Stage 25A feasibility result into tracked production
infrastructure. It does **not** claim arbitrary Everest mod support, stock
desktop Everest on Apple devices, or Strawberry Jam compatibility. The normal
vanilla iOS/iPadOS and tvOS products remain the recommended products.

## Git and release boundary

| Item | Accepted value |
| --- | --- |
| Starting commit | `27e16b4724d94d3991b99c4795f680fcb0e5830c` |
| Feature branch | `feature/apple-everest-static-foundation` |
| Foundation commit | `cf527cc` (`feat: add shared Apple Everest static builder`) |
| Verification/documentation commit | `b168535` (`test: verify Apple Everest static-AOT foundation`) |
| Final feature commit | The commit containing this report; the final response records its full SHA without creating a self-referential commit hash |
| Modern-iOS recovery tag | `ios-v0.1.1-rc.1^{}` → starting commit |
| tvOS RC1 | `v1.0.0-rc.1^{}` → `ee52b0868df091746f134d95d4f020f94f23d4fb` |
| tvOS RC2 | `v1.0.0-rc.2^{}` → `641e86e4ed164cdf93f602ce2f11436449654d6e` |
| Deferred tvOS RC3 branch | `origin/release/v1.0.0-rc.3` → `c8134c8ca7924cf12f48527e714b5242c6024927` |
| tvOS RC3 tag | Absent |

No integration branch, release branch, tag, GitHub workflow, or cloud-builder
template was modified. No GitHub Actions workflow was run.

## Exact profile and host tools

The tracked profile is
[`apple-everest/profiles/stable-1.6458.0.json`](../../../apple-everest/profiles/stable-1.6458.0.json).
It locks:

- Everest `stable-1.6458.0` at
  `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00`;
- Everest's MonoMod submodule at
  `dfc30a1506d37fb88a2c2be004f525205f46a24c`;
- NLua provenance at
  `b3524288712743fb2394dcf615d14d0dac3276e2`, while excluding NLua from the
  device closure;
- YamlDotNet 16.1.3 and Mono.Cecil 0.11.6;
- repository-local .NET SDK 8.0.424 for `AppleEverestBuilder` and .NET SDK
  9.0.317 for the pinned MonoMod reference tests.

The Apple product build retained macOS 26.3, Xcode 26.6, Apple SDK 26.5, and
.NET 10.0.302. The extra desktop SDKs are installed under ignored build state;
they do not change the accepted global Apple toolchain.

[`AppleEverestBuilder`](../../../tools/AppleEverestBuilder/) is shared host
tooling. [`scripts/build-apple-everest-canary.sh`](../../../scripts/build-apple-everest-canary.sh)
is the deliberately internal entry point. The normal root builders have no
`--mods` mode and never enter this pipeline.

## Acquisition, ingestion, and graph

The builder verifies the exact Everest and MonoMod Git commits after checkout.
Its production ingest path accepts a directory or ZIP and rejects absolute or
escaping paths, links, case-insensitive duplicate paths, excessive depth,
excessive file count, oversized members, oversized expanded input, malformed
archives, and missing or invalid root metadata.

Everest metadata is parsed with pinned YamlDotNet, including multi-entry
metadata, `Name`, `Version`, `DLL`, required dependencies, optional
dependencies, and conflicts. The resolver implements the pinned Everest
version rule, rejects missing/incompatible dependencies, duplicate identities,
conflicts, and cycles, and produces a deterministic dependency-first order.
Filesystem and ZIP enumeration order do not decide the output.

The deterministic test suite exercised safe directory/ZIP ingestion and all
of those graph cases. Two independent final closure generations were
byte-for-byte equal across all 16 emitted files.

## Compatibility analysis

The analyzer inventories C# and CLI assemblies with source inspection and
Mono.Cecil. It accepts only the initial closed classes:

- `CONTENT_ONLY`;
- `STATIC_MODULE`;
- `NORMAL_EVENT`;
- the exact registered `ON_HOOK_SUPPORTED` target.

It rejects before Apple AOT:

- unknown `On.*`, production `IL.*`, direct `Hook`, and `ILHook` mechanisms;
- `Assembly.Load*`, custom runtime `AssemblyLoadContext`, Reflection.Emit, and
  `DynamicMethod`;
- RuntimeDetour and NativeDetour;
- unapproved native/P/Invoke payloads;
- NLua/KeraLua;
- process spawning, file watchers, and other unsupported desktop behavior.

Diagnostics identify the package, source/assembly construct, and compatibility
class. There is no accept-any or force mode.

## Closed manifest and shared boundary

Every build emits ignored compatibility-manifest schema 1. It records the
profile/transformer, canonical game class, exact upstream pins, each mod's
identity, hash, dependencies, classification, mechanisms and inventory,
resolved order, content ownership, registry and transform hashes, and final
logical hashes. It contains no absolute path.

The accepted order is:

1. `AppleEverestContentCanary`;
2. `AppleEverestCanaryCore`;
3. `AppleEverestCanaryHookA`;
4. `AppleEverestCanaryHookB`.

The one target-neutral closure contains 10 managed source files and four final
content files. Its accepted logical SHA-256 is:

`c678e7fc77a213e58861e10ae8498a4902e02848b10f515a0715085f647ea7bb`

Two independent regenerations also had the identical 16-file physical
manifest SHA-256:

`fb64ff1eabd4752c329525b0fd4a51aafaf49b5d17671b5856c80dc9e030c9f9`

Both iOS and tvOS package manifests record the same shared-closure hash. No
source-level Everest difference is introduced after that boundary.

### Component accounting

| Boundary | iOS/iPadOS | tvOS |
| --- | ---: | ---: |
| Accepted vanilla managed files copied | 987 | 973 |
| Shared Everest managed files added | 10 | 10 |
| Final managed files before publish | 997 | 983 |
| Accepted vanilla content files | 1,209 | 1,209 |
| Shared Everest content files added | 4 | 4 |
| Final content files before publish | 1,213 | 1,213 |
| Platform-specific Everest source changes after closure | 0 | 0 |

The derived iOS runtime contained 2,217 files (excluding its marker), logical
SHA-256 `97547c22b2f1969481fa7d66bc1412d9e009be8741d4e230f63328b4a298d385`.
The derived tvOS runtime contained 2,196 files, logical SHA-256
`3be6d35711785385862ba6a4b8662fae85aefc6a27252b9fa89b2e6521a195da`.
Those whole-tree hashes are diagnostic definitions over path, size, and file
SHA, not new vanilla locks.

The remaining differences are existing host responsibilities: iOS lifecycle,
touch, Application Support and packaging versus tvOS lifecycle, controller UI,
test persistence namespace and packaging.

## Static runtime, registries, and content

The generated registry constructs each selected `EverestModule` directly and
contains direct factories for its `EverestModuleSettings`,
`EverestModuleSaveData`, and `EverestModuleSession` shapes. Generated AOT roots
reference every closed type. There is no device `Assembly.GetTypes` discovery
for mod types.

The gameplay registry explicitly roots and registers the canary entity in
Monocle's tracker. This was necessary for the real canary room and replaced an
initial physical `KeyNotFoundException`; the final generator patches Tracker
initialization exactly once and rejects duplicate registrations.

The ordinary-event canary uses actual `Everest.Events.Level.OnLoadLevel`
source semantics. Physical logs proved subscription and execution on the
canary room. Logical module disable calls the module's ordinary cleanup and
removes only that module's events/hooks; re-enable reconstructs only already
AOT-compiled state.

Content is validated and compiled on the Mac, then copied into the ordinary
Celeste content tree. The project-owned canary provides Everest metadata,
dialog, a generated visual, and a playable map. Later resolved packages win a
logical-path collision. The accepted winner was
`AppleEverestCanaryHookB`, and the manifest records every prior owner and hash.

Pinned desktop Everest registered `AppleEverestContentCanary 1.0.0` and the
reference loader reached five registered modules. That disposable desktop run
ended with the already documented x64 native signal 139 after registration and
before rendering, with no managed exception. Therefore it proves desktop
registration/content identity, while actual content rendering and precedence
were proved on all three Apple device classes rather than overstated as a
desktop gameplay pass.

## Typed `On.*` lowering

The canary modules use genuine source-facing syntax:

`On.Celeste.Dialog.Clean += Handler`

The analyzer permits that one exact type/method/signature. The generator first
requires a single locked target, rewrites `Dialog.Clean` once on the Mac, and
routes it through a strongly typed generated dispatcher. It never patches once
per mod.

The dispatcher caches its active typed chain when subscriptions or owner state
change. There is no `DynamicInvoke`, `object[]`, per-call reflection, per-call
list creation, runtime method construction, executable-memory mutation, or
device RuntimeDetour backend.

Production semantics tests proved:

- zero hooks call the original once;
- A may change the argument and return value;
- B may wrap A's return;
- a handler may omit `orig`, suppressing the inner chain;
- duplicate subscriptions are retained and `-=` removes the newest matching
  subscription;
- owner cleanup removes only its own handlers;
- disabling/re-enabling changes data only and needs no restart.

With A subscribed first and B second, the exact desktop HookGen and Apple
trace is:

`B-before → A-before → original → A-after → B-after`

The physical canary also proved A+B, A-only, and original-only states, followed
by successful re-enable, without process restart.

## Host MonoMod versus device runtime

MonoMod, Cecil, and real HookGen are valid Mac build/reference tools. The
host-only IL-freeze regression ran a pinned MonoMod `ILContext`/`ILCursor`
manipulator: baseline result 10 became frozen result 15, and the frozen target
ran without MonoMod or RuntimeDetour dependencies.

This does not enable production `IL.*`. `IL.*`, direct `Hook`/`ILHook`,
NativeDetour, Lua, arbitrary native code, and dynamic loading remain explicit
pre-AOT rejections.

The linked device scanner inspects CLI assembly references, types, and method
calls. Both final `Celeste.dll` files passed. The packages contain no
MonoMod.RuntimeDetour, HookGen backend, NLua, KeraLua, DiscordGameSDK,
MiniInstaller, NETCoreifier, arbitrary assembly loader, Reflection.Emit,
NativeLibrary mod loader, process launcher, or file watcher.

## Package inventory and build evidence

Selected Everest runtime/module source is compiled into `Celeste.dll`; no
standalone Everest, mod DLL, MonoMod, HookGen, or Lua assembly remains.

| Evidence | iOS/iPadOS | tvOS |
| --- | --- | --- |
| RID | `ios-arm64` | `tvos-arm64` |
| Build | Release, development signed, separate canary identity | Release, development signed, separate canary identity |
| Full trim / AOT | yes / yes, LLVM enabled | yes / yes |
| Interpreter / JIT | false / false | false / false |
| Managed assemblies in app | 39 | 33 |
| FMOD banks | 7 | 7 |
| Shared closure | `c678e7f…a7bb` | `c678e7f…a7bb` |
| Build duration | 4m58s | 4m38s |
| IPA bytes | 882,053,633 | 896,261,668 |
| IPA SHA-256 | `2788d0f26b362b6c978db634f0d7ad4ec08bc8588db5fe64ecb2dd6c1a307423` | `177a6eeeec3c93f3effe3e0d879f7aff8df4eab2eab38b5df236eef9f256ac8f` |

The iOS app contains the normal platform/framework closure plus
`Celeste.dll`, `Celeste.Content.dll`, `FNA.dll`, `CelesteAppleInput.dll`,
`CelesteIOSFoundation.dll`, and `CelesteIOSRuntimeHost.dll`. The tvOS app
contains the corresponding normal platform/framework closure plus
`Celeste.dll`, `Celeste.Content.dll`, `FNA.dll`, `CelesteAppleInput.dll`, and
`CelesteTvOSRuntimeHost.dll`. The differing totals are existing platform host
and framework assemblies, not differing Everest closures.

The build logs retain known vanilla Celeste/FNA trim warnings. No IL2026,
IL3050, `RequiresDynamicCode`, or `RequiresUnreferencedCode` warning was
attributed to an Apple-Everest source. The new Foundation private-log calls
produce availability analyzer warnings only; both deployment floors are above
the declared API availability and physical iPadOS 15.8.8 passed.

## Physical acceptance

### iPhone

The same full-AOT iOS product launched on iPhone 12 Pro Max. Title/menu,
touch, audio, hook panel, A+B/A-only/original-only traces, re-enable, content
room, ordinary event, pause/resume, background/foreground, and 180-degree
rotation passed. The final room has a bounded flat floor, so death/respawn was
not physically reachable and is not claimed. No stuck input occurred.

A bounded private app log independently recorded startup, profile, three code
modules, four content mounts, content precedence B, exact hook traces, content
room launch, and ordinary-event execution. It remains ignored and contains no
analytics or upload path.

### iPad

The exact same universal iOS IPA installed on iPad mini 4 running iPadOS
15.8.8. Launch, landscape, menu/audio/touch, A+B hook trace, content canary,
ordinary module/event behavior, pause, background/reopen, 180-degree rotation,
and stuck-input checks all passed. A legacy Apple/Xamarin deployment transport
was needed because the current CoreDevice CLI did not enumerate this old
device; the product bytes were unchanged.

### Apple TV

The same logical closure in the tvOS full-AOT product launched on Apple TV 4K
(3rd generation). Direct FNA3D Metal, controller menus, seven-bank audio,
module controls, A+B/A-only/original-only traces, content room, movement,
pause, Home/reopen, and continued audio/controller function all passed.

Live diagnostics showed one A15 Metal runtime, seven banks, 922 FMOD events,
118 buses, three VCAs, startup/content/precedence passes, and no runtime DLL
load or detour. The canary identity intentionally does not duplicate every
vanilla Save Manager/HUD product feature.

Across all devices the user physically confirmed the requested matrices as
passing. All used the same pre-platform closure and ordinary platform hosts.

## Performance and lifecycle observations

The no-hook path is a cached direct typed delegate; the active path rebuilds
only when subscription/owner state changes. There is no per-frame source
analysis and no observed hitch, audio interruption, stuck input, or lifecycle
failure during repeated physical toggles and gameplay. No claim of zero-cost
instrumentation or a broad performance benchmark is made.

## Vanilla and inherited regression

The optional builder copies accepted generated roots into marked ignored
directories before applying the closure. Direct scans found no Apple-Everest
tokens in either accepted vanilla generated root or the accepted vanilla iOS
IPA. The vanilla iOS tree lock remains 944 files / SHA-256
`2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357`.

Current iOS gates passed:

- C2: 84 source checks plus Foundation 243 and durability 87;
- D2: 153;
- D3: 164;
- E1: 93;
- E2: 89;
- input profiles: 56.

The historical Stage 24B and C1 verifiers still intentionally assert that no
tvOS file changed after their own old baselines. They now fail that historical
predicate because accepted tvOS work occurred later; they were not edited or
weakened. The current C2-through-E2 chain is green.

The complete current tvOS chain passed:

- Save Manager protocol: 66;
- Controller Prompts: 38;
- graceful Quit: 16;
- soft reload: 21;
- QR pairing: 31;
- Performance HUD: 39;
- continuity protocol: 57 plus 85 current source checks;
- builder UX: 21;
- Stage 14/documentation and repository gates.

Stage 25B itself passed 53 deterministic builder tests, 19 production typed
hook tests, the real desktop HookGen reference, the real host IL-freeze test,
two independent closure reproductions, and 234 source/product contract checks.

## Frozen locks, privacy, and licensing

All accepted locks remain unchanged:

| Boundary | SHA-256 |
| --- | --- |
| Canonical Content | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw source | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched source | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Stage 6 real-audio tree | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| iOS native | `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2` |
| tvOS native | `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc` |

The canary adds no telemetry, analytics, updater, mod downloader, listener,
network permission, or dynamic-code entitlement. Host Git/NuGet acquisition is
build tooling only. The apps use separate experimental identities/containers
and do not read or mutate vanilla saves.

Tracked content contains only project-owned canary sources/assets. Everest,
MonoMod, YamlDotNet, and Mono.Cecil licensing/provenance is documented in
[`APPLE_EVEREST_THIRD_PARTY.md`](../../APPLE_EVEREST_THIRD_PARTY.md). No Celeste,
FMOD, generated game source, signed app, IPA, save, device identifier, signing
value, or private log is tracked.

## Known limits and Stage 25C

This is production foundation code, not a user-facing general mod loader.
Only pre-analysed source/content enters a build. General Mod Options parity,
persistent mod settings/save/session storage, arbitrary entities/triggers/
backdrops, runtime installation, hot reload, IL hooks, direct detours, native
extensions, and Lua remain out of scope. The canary map does not offer a death
hazard. The desktop reference established registration rather than rendering.

Stage 25C should be a compatibility-evaluation stage, not a large content-pack
attempt. Before it starts, select small source-available, permissively licensed
Everest mods compatible with the pinned profile and preserve their exact ZIPs
as ignored user inputs. The useful fixture classes are:

1. one content-only map with dialog/texture and no code/native/Lua;
2. one small normal-event/static-module mod with settings and explicit
   entity/trigger registrations;
3. one small source-available `On.*` mod whose limited targets can be reviewed
   and either added to the typed registry or rejected precisely;
4. one deterministic `IL.*` mod for host-freeze feasibility only, still
   rejected from the 25B production profile;
5. negative fixtures using direct Hook/ILHook, native/P/Invoke, Lua, and
   runtime loading to prove early rejection.

Do not start with Strawberry Jam or a helper ecosystem. Specific public mod
names should be chosen only after their current source, license, pinned-Everest
metadata, dependency closure, native/Lua requirements, and analyzer output have
been reviewed. Real ZIPs are needed for the Stage 25C end-to-end archive and
content tests, but need not be downloaded before that selection audit.

## Recommendation

Stage 25B is **GREEN** and ready to integrate by fast-forwarding the exact
final feature-branch commit after review. Integration, a general mod CLI,
release tagging, and Stage 25C compatibility claims remain separate actions.
