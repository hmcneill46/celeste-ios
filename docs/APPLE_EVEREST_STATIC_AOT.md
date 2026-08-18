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

`AppleEverestBuilder` accepts a directory or ZIP through a bounded archive
reader. Absolute paths, traversal, links, duplicate normalized paths, excessive
depth, excessive members, and oversized input are rejected. `everest.yaml` is
parsed with the pinned YamlDotNet package. Required dependencies, optional
dependencies, conflicts, duplicate names, and cycles are resolved into a stable
topological order.

The compatibility analyser classifies every package before generated code is
accepted. The first profile supports project-owned content-only packages, static
module lifecycle/events, and explicitly recognised typed `On.*` hooks. It
rejects dynamic code, runtime IL hooks, direct runtime detours, native payloads,
Lua, unsupported platform APIs, ambiguous targets, and unknown mechanisms. A
rejection is a compatibility result, not an invitation to weaken full AOT.

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
- statically compiled module source;
- typed hook dispatchers for explicitly supported targets.

There is no runtime DLL discovery or loading.

## Typed `On.*` lowering

Supported `On.*` targets are rewritten at build time into ordinary typed
dispatchers. Each dispatcher caches its active chain, preserves Everest-style
subscription order and LIFO invocation, passes a typed `orig` delegate, allows
argument and return-value changes, honours handlers that intentionally do not
call `orig`, and tracks the owning module for enable/disable. The no-hook path
is a cached direct typed call with no per-call allocation.

The first canary proves two independent handlers around
`Celeste.Dialog.Clean`: module B enters, module A enters, the original runs,
then A and B return. It also exercises owner disable/re-enable and duplicate
subscription semantics. This is a bounded mechanism registry; arbitrary
MonoMod hook compatibility is not implied.

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
`MonoMod.RuntimeDetour`, HookGen, NLua/KeraLua, runtime mod DLLs, native mod
libraries, desktop
Everest services, and unexpected executable content.

## Current limits

This foundation does **not** promise arbitrary Everest mods, runtime mod
installation, runtime enable/disable of code outside the prebuilt registry,
IL hooks on-device, direct detours, Lua, native mods, content hot reload,
Everest networking/updating, dependency downloading, or desktop parity. Only
exactly analysed packages and explicitly registered mechanisms can enter the
closure. Extending support requires a new deterministic transform plus desktop
reference, AOT, device, isolation, and performance evidence.

In short, this is not general Everest support.

For third-party provenance and licensing, see
[Apple Everest third-party notices](APPLE_EVEREST_THIRD_PARTY.md).
