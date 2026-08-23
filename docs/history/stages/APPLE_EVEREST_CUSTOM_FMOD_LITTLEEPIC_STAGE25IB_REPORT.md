# Stage 25I-B — bounded custom FMOD and first real multi-helper map

Status: **PASS — GREEN**

Date: 2026-08-23

Stage 25I-B closes the single classified boundary left by Stage 25I-A. The
exact ChronoHelper 1.3.3 ordinary FMOD bank is now selected from its public
release ZIP on the Mac, hash checked, represented by a generated typed
manifest, staged once, and loaded through Celeste's existing FMOD Studio
system. The unchanged LittleEpic's Precision Challenge 1.0.0 map and its two
real code helpers then passed full-AOT physical acceptance on iPhone, iPadOS
15.8.8, and Apple TV.

This is the first real multi-code-helper map accepted by the shared Apple
static-AOT product. It is not general desktop Everest or arbitrary custom-audio
support.

## Git and immutable references

| Item | Accepted value/result |
| --- | --- |
| Integration baseline | `236d5fc957fb02d283a29ae4f55ef9753be042c2` |
| Stage 25I-A parent | `b1a20bf3fe7b4d20a50ef406e54e0f6d6445e73c` |
| Feature branch | `feature/apple-everest-custom-fmod-littleepic` |
| Final feature commit | The commit containing this report; the final handoff records its exact SHA |
| Everest | stable-1.6458.0 / `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00` |
| MonoMod | `dfc30a1506d37fb88a2c2be004f525205f46a24c` |
| iOS recovery / tvOS RC1 / tvOS RC2 | `27e16b4724d94d3991b99c4795f680fcb0e5830c` / `ee52b0868df091746f134d95d4f020f94f23d4fb` / `641e86e4ed164cdf93f602ce2f11436449654d6e` |
| RC3 release ref | `c8134c8ca7924cf12f48527e714b5242c6024927`; tag absent |
| GitHub Actions | Not run; zero Actions minutes used |

The branch was created directly from the exact I-A YELLOW tip. `tvos-port`,
the protected release references, prior reports, upstream, and all tags were
left untouched.

## Exact public graph

| Package | Version and ZIP SHA-256 | Production payload | Licence/provenance recorded by I-A |
| --- | --- | --- | --- |
| LittleEpic's Precision Challenge | 1.0.0; `ca57d295f446affdd8eb9b56ffe58f98cc652e9ec242ff07b224877d0067b384` | Content-only root | LittleEpic; GameBanana metadata says CC BY-NC-ND 4.0 |
| ChronoHelper | 1.3.3; `af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18` | `ChronoHelper.dll` `214b26a5d7e3f17e93d3c388a3a6066dd137f9ce09b6203a4e1a482ba342a2dc` | ricky06; GameBanana metadata says CC BY-NC-ND 4.0; no authoritative public source repository was located |
| DJMapHelper | 1.13.4; `95ab02d657213031b70c3079738b21be399e567ca8a32b1fe8e8effc0778eedb` | `DJMapHelper.dll` `0d73202b5e16f26a4c8a286dd6ad600c1601d1904d8f72d909f8acb8469ee446` | DemoJameson; GameBanana metadata says CC BY-NC-ND 4.0; audit source tag `v1.13.4` is `693fbea3405090517ad66217b809673ff2725206`; that repository has no separate LICENSE file |

All packages are safely ingested from exact source-free public ZIPs outside
Git. No helper source checkout, copied helper code, packaged ZIP, DLL, map,
bank, GUID file, or other third-party byte is tracked. Package licensing and
provenance remain exactly as recorded by Stage 25I-A.

The original map remains:

- path `Maps/LittleEpic/precisionchallenge/precisionchallenge.bin`;
- SHA-256 `6de18b4280df15493a9f1b0eac2b547118757cf276795146bdbf299e9c371475`;
- SID `LittleEpic/precisionchallenge/precisionchallenge`;
- LevelSet `LittleEpic`;
- rooms `1`–`7` and `heart`;
- start room `1`, spawn `(160,176)`, entity ID `0`.

No acceptance entity or test data was injected into the real map. The
established content compiler's package-header normalization remains separate
from, and does not change, the source-package map lock.

## Pinned custom-audio behavior

ChronoHelper's exact package contributes:

| Record | Accepted value |
| --- | --- |
| Bank | `Audio/ExpertContestHelper.bank` |
| Bank SHA-256 | `2607c358e66f4bce4e81fa3748eaffe1205dcfb57010be2e90ce40fabeb8d3ec` |
| GUID file | `Audio/ExpertContestHelper.guids.txt` |
| GUID SHA-256 | `db7f44d7ee1d79eb5efe734e595a6fb99937fd78e5cf9af91b0db6cabc4272a6` |
| Generated bundle path | `AppleEverest/Mods/ChronoHelper/Audio/ExpertContestHelper.bank` |
| Bank identity | `{f12a5c05-a79b-4ed0-bea9-81a1d2ecb986}` / `bank:/ExpertContestHelper` |
| Load ordinal | 0 after all seven Celeste banks |
| Manifest SHA-256 | `0a7ca20a24239f4829ab5af7002004309d38d89ac5bffbfefff6a49b278c8841` |
| Bank logical-set SHA-256 | `c927c0779c1dbbb5b43bd1a5daa5c2eb5e1c9d00b7e81eab87e70aa0a46eb8d7` |

The complete 200-byte UTF-8 GUID export has three records, all retained in
closure identity:

| Class | GUID | Path |
| --- | --- | --- |
| event | `33eab85e-7e13-417e-ab3b-a0b7c8caa6b2` | `event:/ricky06/EC2023/horn` |
| event | `22f6b410-423f-4c15-a6b5-0523b16ab5fd` | `event:/ricky06/zip_mover 2` |
| bank | `f12a5c05-a79b-4ed0-bea9-81a1d2ecb986` | `bank:/ExpertContestHelper` |

There are two event records, one bank record, and no bus, VCA, snapshot, or
other record class. Missing, malformed, oversized, invalid UTF-8, duplicate,
colliding, wrong-owner, wrong-archive, wrong-bank, and wrong-GUID inputs fail on
the Mac before AOT.

### Desktop versus static Apple

Pinned desktop Everest discovers banks and GUID exports from a live mod
archive/content graph and registers them dynamically. The Apple product instead
performs that census at build time:

```text
exact ZIPs
  -> bounded Mac-side bank/GUID census and collision validation
  -> canonical typed custom-audio manifest
  -> exact bank staged once in the shared closure
  -> existing Celeste Studio System.loadBankFile
  -> GUID-validated bank and event descriptions
```

The selected API is `FMOD.Studio.System.loadBankFile`. It matches both Celeste's
existing bank-loading API and the immutable read-only app-resource lifetime,
requires no retained pinned byte buffer, and lets FMOD own normal bank data.
`loadBankMemory` would add unnecessary duplicate memory and pointer-lifetime
ownership. No current-working-directory lookup is used.

The accepted bank has no companion strings bank, so `Bank.getPath` cannot
reverse-resolve its human path. Runtime identity therefore combines the exact
build-time bank/GUID/hash manifest with `Bank.getID`, `System.getEventByID`, and
`EventDescription.getID` against the bytes actually loaded. Both event paths
then enter normal `Celeste.Audio` lookup through their exact generated GUIDs.

## One Studio system and lifecycle

The only Studio-system creation remains
`Celeste.Audio.Initialize -> FMOD.Studio.System.create`. The seven ordinary
Celeste banks continue to load through `Celeste.Audio` first. The generated
custom load hook runs only after `Stage5BAllBanksLoaded`, on the same system,
before helper gameplay can request an event.

The production state is bounded to `NotLoaded`, `Loading`, `Loaded`, and
`Unloaded`, keyed to the live Studio-system identity. Repeated initialization
against the same system is a no-op. Soft Celeste reload retains the one FMOD
system and bank; background/foreground and tvOS Home/reopen retain the same
validated handle. Cold process launch creates the normal single system and
loads the custom bank once again from the bundle.

At true audio teardown the generated registry invalidates its managed handles
before Celeste calls `Studio.System.unloadAll`. FMOD owns bank destruction;
there is no double unload. A failed custom load unloads any bank loaded in that
attempt and aborts helper-product initialization rather than continuing with a
partial module.

No second FMOD system, native audio binary, framework, plugin, scanner,
download path, mod folder, persisted bank marker, or custom audio thread was
introduced. Native FMOD remains 1.10.09 build 97915 and the managed binding
remains 1.10.20 (`0x00011014`).

## Real helper surfaces and necessary compatibility fixes

ChronoHelper's complete accepted surface is retained, including its
bank-dependent entities. Binary census identifies:

- `ExplodingPinata.BreakRoutine`: the fake-out path calls normal
  `Audio.Play("event:/ricky06/EC2023/horn", position)`;
- `EntityConveyor.moveEntityOnConveyorRoutine`: attachment calls normal
  `SoundSource.Play("event:/ricky06/zip_mover 2", ...)`.

The project-owned data-only custom-audio room instantiates the real distributed
`ChronoHelper/ExplodingPinata`. Dashing into it invoked real ChronoHelper code,
created the horn event through the ordinary audio path, audibly played it, and
continued through real pooled debris. The zip-mover event was GUID-resolved and
AOT-linked on every product but was not physically triggered; physical proof of
both events is optional Strong GREEN, not overall GREEN.

Two previously hidden compatibility details were fixed narrowly and locked:

- public `[Pooled]` helper entity types with parameterless constructors are
  discovered on the Mac and receive generated typed registration/factories;
- old Everest maps may omit the optional pre-1.2.5 `lvl_` room-name prefix, so
  the canonical generated `LevelData` accepts either exact form rather than
  unconditionally slicing four characters.

Both fixes are general static-closure behavior required by the exact binaries;
neither edits the third-party map or performs runtime type discovery.

## Real LittleEpic and frozen-IL proof

The graph moved from `{unclassified: 0, unsupported-required: 1}` to
`{unclassified: 0, unsupported-required: 0}`. The complete helper/module/content
surface is source-free and zero-overall before platform compilation.

Physical acceptance used three distinct proofs:

1. **Custom FMOD:** the real ChronoHelper exploding piñata played the horn and
   spawned its debris through the accepted bank and ordinary audio path.
2. **Unchanged real map:** room `1` launched at its real spawn. Crossing
   `DJMapHelper/maxDashesTrigger` with `dashes=Zero` removed dash availability.
   Real room `5` loaded from the original map, and
   `ChronoHelper/CustomTimeSwitchGates` performed its configured approximately
   17-second transition while gameplay continued.
3. **Frozen DJ IL:** a data-only acceptance room used the real distributed
   `DJMapHelper/colorfulFlyFeather` and `DJMapHelper/featherBarrier`; the blue
   feather passed the matching blue barrier, proving the real frozen collision
   transformation rather than only factory construction.

DJMapHelper retains five exact IL registrations across three fingerprinted
target bodies and 51 static `EmitDelegate` sites. The plan remains
`5791ac3b27f500d8ae9f2cc72669b09aa7e490d17b33b992a2b55d0a8728b457`.
All five transformations are deterministic; omitting DJMapHelper leaves the
canonical bodies untouched. The ordinary HookGen catalog remains
signature-driven at 102 targets. Static entity/trigger factories, 27 reviewed
API accesses, and bounded DynData/FastReflection lowering remain active; there
is no device `GetTypes`, `Activator`, general DynamicData backend, runtime IL
worker, or reflection-driven factory selection.

## Shared closure and reproducibility

Three fresh post-fix closure generations are identical:

| Boundary | SHA-256 |
| --- | --- |
| Compatibility manifest | `99d3d8c085d8f360ea00067427296d45a8d30c6d5ea2387c2c614c8cce3ee313` |
| Managed logical tree (36 files) | `efae50ad9bd2fa2840d0d7626a747c820a4f9734cd849dc1aeccaf8137ab90ca` |
| Content logical tree (621 files) | `f2b3d04ab37d4ce7a260777544939f5835307c9d92e127dab137535fadccd9e3` |
| Frozen assembly logical set | `b8b923321856f4684bd80743b1a1aa9eb24fb51ef1696321974c230e6e6139d3` |
| Gameplay/module registry | `87f6ab1991354c68be7a1c9743e43dcd4b0b806e0aa7e49e9d59841b5ae0cfea` |
| Hook transform | `5710b53c8b78bf178b5a5514083caf4bf4db930286fbc92120b4e7c7db959fd3` |
| Reviewed API surface (30 members) | `d594dea86e944b468819818773f2177312445bbebe022bcbc7ea1ff779c8652c` |
| ModInterop plan (empty for this graph) | `84f0267040cda9cc51d4dc08da367a4b934aa0e9f43cf3d553815e594f25899d` |
| Custom-audio manifest | `0a7ca20a24239f4829ab5af7002004309d38d89ac5bffbfefff6a49b278c8841` |
| Custom-bank logical set | `c927c0779c1dbbb5b43bd1a5daa5c2eb5e1c9d00b7e81eab87e70aa0a46eb8d7` |
| Complete target-neutral closure | `f2eaa7c5304f84f82af2326e3e6182fa925c6b62747e2e59e3f0e31d6a6ed3b3` |

The bank bytes, GUID mapping, owner/version, order, lifecycle policies,
registries, frozen IL, and reviewed APIs all transitively affect the shared
identity. The same closure feeds iPhone, iPad, and tvOS.

## Products and package boundary

| Product | Bytes | SHA-256 |
| --- | ---: | --- |
| Signed iOS universal IPA | 884,050,184 | `34e5a082c0e3ec4502235db792c415bfd446c65bc0dc8e83eb9edd7e46734fca` |
| Signing-ready unsigned iOS IPA | 883,848,765 | `06a61fc832554b40137262e7cf5b11a96244f73c15db89baf0d1fdfe420f073f` |
| Signed tvOS IPA | 898,487,863 | `82762b97428dded5b8f69697c2063eff8c260a980edf2b9aac1176e4f49afef4` |
| Signing-ready unsigned tvOS IPA | 898,044,330 | `6eb4f837a52a2aeec898b18906c4d5eb4a64f218207ed115dc72b07c8d17ac87` |

Both platforms are arm64 Release, fully trimmed and full AOT, with
`UseInterpreter=false` and no JIT. Minimum versions remain iOS 15.0 and tvOS
16.0. Each product contains the bank exactly once at the generated path with
the exact source SHA. Neither contains source ZIPs, helper source, host IL
workers, Cecil, MonoMod.Cil, RuntimeDetour backends, or a new native component.
The unsigned packages were derived from the accepted app bundles by removing
only their app signature/provisioning boundary; their executable/content
closure is identical. Full compilation succeeded, but a trustworthy aggregate
wall-clock duration was not separately captured, so this report does not
invent one.

The normal vanilla iOS and tvOS generation paths remain outside the canary
builder and have no ChronoHelper, DJMapHelper, LittleEpic, generated custom
audio manifest, or custom bank. Canonical and native locks remain:

- Content `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46`;
- raw `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273`;
- patched `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5`;
- Stage-6 audio `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9`;
- iOS native `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`;
- tvOS native `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`;
- vanilla iOS generated source `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357`.

## Physical acceptance

The same signed universal iOS product was installed as a replacement on an
iPhone 12 Pro Max and iPad mini 4 running iPadOS 15.8.8. The corresponding
tvOS product was installed as the existing canary identity on an Apple TV 4K
(3rd generation). Existing application data was not uninstalled.

All three devices passed:

- normal launch, title/menu, base audio, seven banks, and the custom bank;
- real horn playback and continued pooled-debris behavior;
- actual LittleEpic room 1, zero-dash trigger, death/respawn, pause/resume, and
  Return to Map;
- actual room 5 Chrono time-switch gate;
- real DJ frozen-IL feather/barrier behavior;
- controller and audio; touch plus rotation on iPhone/iPad;
- background/foreground or Home/reopen;
- cold relaunch with one fresh custom-bank load.

Apple TV additionally passed a safe Save Manager Settings mutation/reset and
Confirm-driven soft reload. The listener stopped, the existing FNA/FMOD
runtime recovered to a functional menu, and the custom bank/event remained
correct without a duplicate system or bank. The user's final iPad and Apple TV
responses were both all-pass.

## Tests, regressions, and privacy

The current AppleEverestBuilder deterministic suite passes 323 tests. It
includes exact bank selection, full GUID parsing, archive/bank/GUID locks,
collision negatives, missing/malformed inputs, manifest/closure identity,
no-bank controls, lifecycle transitions, package boundaries, optional old room
prefixes, public pooled entities, and retained I-A frozen IL/API contracts.

The exact Stage 25I-B verifier passes 75 repository checks without optional
artifacts and 117 checks with the exact packages, generated closure, and
byte-locked signed and unsigned products. The retained predecessor and semantic
regression evidence is:

- Stage 25I-A: 115 checks; H-D: 163 checks; H-C: 120 checks;
- production typed HookGen semantics: 48 checks;
- pinned MonoMod ModInterop reference: 14 checks;
- desktop HookGen and direct-Hook chain/order/lifecycle: pass;
- real pinned MonoMod IL freeze without device RuntimeDetour: pass;
- H-B/H-D composed IL sequence, order, locks, rejection, and isolation: pass;
- all nine canonical Celeste input profiles and rejection policy: 56 checks;
- public documentation, link, repository, and proprietary-candidate gate: pass.

Older per-stage verifier programs deliberately freeze their then-current model
version, catalog count, and pre-semantic source filenames. They remain
unchanged and accepted at their historical commits rather than being weakened
to accept v10. Their behavior contracts—H-D Caerulea, H-C Vortex, H-B
DisposableTheo, H-A Dash Toggle, Stage 25F durability, Stage 25F-B2 ModInterop,
Stage 25E helpers, and Stage 25D detours—are carried by the current 323-test
builder suite and the focused semantic probes above.

Runtime scans reject Cecil, MonoMod.Cil, runtime ILHook, DynamicMethod,
Reflection.Emit, runtime Assembly.Load, helper assembly scans, arbitrary bank
scans, Lua, native mod payloads, and process or file-watcher mod behavior.

The repository/privacy verifier finds no game data, mod packages, FMOD banks,
saves, signing material, device identifiers, LAN credentials, or generated
third-party source in Git. Detailed physical logs and packages remain ignored.
Final repository-volume free space before commit was 48 GiB; no accepted source,
input, product, or compact evidence was removed.

## Compatibility boundary and next stage

`STATIC_CUSTOM_FMOD_BANK` means one exact, statically selected, ordinary
hash-pinned event bank with a complete parsed GUID export, deterministic graph
load order, the existing Celeste Studio system, and immutable installed
lifecycle. Dynamic/downloaded banks, arbitrary runtime discovery, custom
master/strings replacement, native DSP/plugins, programmer-sound callbacks,
and arbitrary hot bank mutation/unload remain deferred.

LittleEpic custom-map progression also remains intentionally nonpersistent;
Save and Quit is suppressed in that lane. With a real map, two real code
helpers, real helper content, frozen IL, and custom FMOD now physically green
on all three Apple targets, LevelSet/custom-map progression durability is the
highest-value next stage. Ordinary medium standalone maps built from the
already accepted mechanisms are substantially closer; configured-IL-heavy
helpers, broader custom-audio shapes, Lua/native payloads, collab-wide content
and progression, and extensive graph scale still stand between this bounded
result and a rational Strawberry Jam experiment.
