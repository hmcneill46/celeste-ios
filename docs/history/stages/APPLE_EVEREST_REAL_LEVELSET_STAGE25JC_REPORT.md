# Stage 25J-C — first real multi-map LevelSet

Status: **PASS — GREEN_IOS_IPADOS / TVOS_PHYSICAL_PENDING**

Apple TV physical status: `TVOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE`

Stage 25J-C started from `972daa03e5ca77d38b3d5071b43e474c0bf3084a`
on `feature/apple-everest-real-levelset`. The last all-three-device physical
GREEN checkpoint remains `57d55c7b9e15c5fa84847d2879f774f30025a96a`.
This stage is development-integration-ready, but is not an all-platform release
candidate until the cumulative Apple TV catch-up run is completed.

## Result

One source-free shared static-AOT closure now contains four ordinary real map
SIDs. LittleEpic's Precision Challenge and Fear of the Dark retain their exact
compatibility identities, while the unchanged public Torremolinos Speedbuild
package contributes two genuine five-room maps in one distributed LevelSet:

- `Xoa/Torremolinos Speedbuild/1`; and
- `Xoa/Torremolinos Speedbuild/2`.

The two new maps have independent SID-plus-map identities, AreaStats and
resumable Sessions. They share a deterministic LevelSet identity and aggregate
maximums without sharing progression records. Map 1's blue heart and Map 2's
red strawberry survived Save and Quit, process termination, cold launch and
repeated A → B → A switching on the physical iPhone. The prior LittleEpic and
Fear records survived the same-identity installation.

## Candidate selection and provenance

The audit screened a 512-package same-root corpus, retained metadata for 30
plausible small multi-map releases, and deeply audited eight leading packages
and their dependency closures. The deterministic ranking preferred a small
ordinary versioned package, two to five maps, bounded physical scope, and no
new unrelated compatibility mechanism.

Torremolinos Speedbuild was selected because it is a compact exact two-map
release with two meaningful A-sides, no managed code, no helper dependency
beyond Everest, and useful heart/berry/completion evidence.

| Property | Accepted value |
|---|---|
| Public release | [GameBanana download](https://gamebanana.com/mmdl/542952), [mod page](https://gamebanana.com/mods/150598) |
| Package | Torremolinos Speedbuild 1.0.0 by XoaOfficial |
| ZIP SHA-256 | `90e4918cfe24a2075b52853d02614cf97c9ecd8c552c3da6f1360d8c5e3fcbec` |
| License finding | GameBanana metadata: CC BY-NC-ND 4.0; ordinary unchanged download/install use |
| LevelSet | `Xoa/Torremolinos Speedbuild` |
| LevelSet identity | `0a3373f734d66d247e1b30c46826d41e48faf72e201bd10a875463bb15d4d68d` |

The public ZIP is consumed unchanged and is not tracked. No package bytes,
generated game source, saves, signing identifiers or device identifiers appear
in Git.

## Map and aggregate census

| SID | Rooms | Persistent content | Map SHA-256 | Compatibility identity |
|---|---:|---|---|---|
| `Xoa/Torremolinos Speedbuild/1` | 5 | one golden strawberry, heart, normal completion | `900b60bb3471299bde8f5419dfae9ca74c04f4a8bf556a2c4d4da5ecb2c81ba6` | `bb7febe30e78a2aad0f2b85709d624f37d63e9793dbbba89225d7193bc03c392` |
| `Xoa/Torremolinos Speedbuild/2` | 5 | two red berries, one golden strawberry, heart, normal completion | `a3fc404c03276b8296f10d83ef9d4d9216237c711ab1002b2006bfd7e0f69aad` | `1166435f8b43fbd13843a84670733dc6f4bfb73e0095f8356b5457f87a6e3550` |

The LevelSet maximum is four strawberries, two hearts, zero cassettes and two
completions. Runtime aggregates sum only the installed exact maps in this
LevelSet: strawberries, hearts, cassettes, deaths, time and completion count.
LittleEpic and BevWeb data cannot contaminate those totals. Neither new map
declares a checkpoint, so checkpoint persistence was not physically claimed;
the existing deterministic checkpoint fixtures remain green. Physical map
completion was also not claimed, while completion fields, maxima and aggregate
policy passed deterministic verification.

## Static LevelSet authority

The builder now emits a target-neutral, explicitly ordered LevelSet manifest.
Every record contains the LevelSet name, semantic identity, ordered member SIDs
and aggregate maximums. The identity is derived from each ordered member SID
plus its independently authoritative compatibility identity. Consequently:

- runtime Area-number reordering cannot change identity;
- adding an unrelated LevelSet cannot change identity;
- changing Map B quarantines only Map B's old record while Map A remains exact;
- removing Map B leaves its record quarantined while Map A remains usable; and
- exact Map B re-addition restores the original record and LevelSet identity.

The generated Options selector groups persistent maps under their LevelSet and
keeps the explicit static debug-map lane separate. All entries remain known at
build time; there is no device-side Mods scan, reflection discovery, network
resolution or runtime DLL loading.

## Pinned-Everest map compatibility finding

The ordinary second map distributes strawberry metadata with Everest's
automatic negative checkpoint/order sentinel values. The base 1.4.0.0 loader
indexes those values directly into a fixed tracker and therefore throws before
the menu on a clean AOT launch. The failure was reproduced from passive device
logs and traced to `MapData.Load`, not to iPadOS 15.

The exact generated-source transform now implements the bounded pinned-Everest
semantics required by this real map: negative coordinates normalize
deterministically, duplicate order positions take the next free position, and
the tracker grows while retaining existing entries. The transform is locked to
one exact source shape and fails closed if that shape changes. It is shared by
iOS/iPadOS and tvOS and introduces no platform-specific game behavior.

## Combined static closure

The stable package order is:

1. ChronoHelper 1.3.3;
2. the retained custom-audio canary;
3. DJMapHelper 1.13.4;
4. the retained DJ frozen-IL content canary;
5. Fear of the Dark 1.0.0;
6. LittleEpic's Precision Challenge 1.0.0;
7. Torremolinos Speedbuild 1.0.0.

The new package requires no helper or mechanism expansion. The closure remains
102 HookGen targets, five frozen IL transforms, zero direct ILHooks, zero
configured IL, zero general DynamicData, zero ModInterop registration, zero
Lua/native payloads, and the existing one-bank/two-event custom-audio class.
The static entity registry remains 59 factories and zero custom backdrops.

Three independent generations were identical:

| Boundary | SHA-256 / count |
|---|---|
| Shared closure | `ac8bfd9f69a2ba09154dc372c64701162e72575a18ba9a57190865faf7afb337` |
| Managed tree | 42 files; `23713c56359684f621db6a6172bec3b91bf5c6a499b378ab18c740df43de64f0` |
| Content tree | 653 files; `31a1401602b00f347668e676105763f9e61b13bd326d467f7b6e176090782d04` |
| Static registry | `d7ff46c8ff66d0e5928723494ac65f4b08bfb23c110b621caeb30c2261b65ad1` |
| Hook transform | `80b1b94b86e0e331405fe58b61a2e85064a972c2fe778a022d026dadca785244` |
| Apple API surface | 30 members; `d594dea86e944b468819818773f2177312445bbebe022bcbc7ea1ff779c8652c` |
| Progression manifest | `85fd6303a88e49e87135c8349e55af66ea86185e30a977799c93eab6a37822f4` |
| LevelSet manifest | `2e96e9d22ba47e2e744cab1cfadcf093319d458d84c9636f4d7fed06fa068733` |
| Custom-audio manifest | `0a7ca20a24239f4829ab5af7002004309d38d89ac5bffbfefff6a49b278c8841` |
| Frozen IL plan | `5791ac3b27f500d8ae9f2cc72669b09aa7e490d17b33b992a2b55d0a8728b457` |

## Persistence and storage

The byte-stable `AEVPSV1` schema remains version 1. No migration or parallel
store was introduced. The four-real-map fixture is 1,508 raw bytes and 598
bytes after the tvOS representation's compression. The accepted physical
snapshot reached generation 9, 2,448 raw bytes and 886 compressed bytes,
0.69777% of the unchanged 126,976-byte replica cap. The complete theoretical
three-slot A/B allocation remains 761,856 bytes.

The A/B corruption unit is still one complete independently validated
progression snapshot. Newest-replica corruption falls back to the previous
valid generation without affecting the vanilla numbered save. Slot isolation,
delete/recreate, vanilla replacement lineage, map update, removal and exact
re-addition all passed.

## Products and physical acceptance

Both products consume the same pre-platform closure and are Release, fully
trimmed, fully AOT compiled, `UseInterpreter=false`, and JIT-free.

| Product | Bytes | SHA-256 | Result |
|---|---:|---|---|
| Signed universal iOS/iPadOS IPA | 883,837,122 | `1261bf8ac82196a4022270b77ed4928f9efc5acd7d22d2b8c54223990a0b7e2f` | development-signed; exact iPhone/iPad device-family manifest |
| Signing-ready unsigned tvOS IPA | 897,773,913 | `8d1cc5d3b67b97d1579a34b213ce20d9e15c8bed596e21cfe8039d35aac5dd18` | full static/package/AOT acceptance; physical pending |

The iPhone 12 Pro Max physical run passed both new maps, independent cold
progression, A → B → A switching, retained LittleEpic/Fear state, LittleEpic
horn, DJ frozen IL, Chrono gate, death/respawn, pause/resume, touch, controller,
rotation and background/reopen.

The iPad mini 4 diagnostic initially exposed a stale locally compiled product:
passive logs identified the unpatched negative-strawberry tracker access. A
fresh build with the already tracked compatibility transform loaded all vanilla
and all six static maps. A second local packaging check found that one direct
diagnostic publish had reused a phone-only intermediate manifest even though
the source manifest and normal product declared iPhone and iPad. The exact
accepted package was rebuilt with device families `[1, 2]`, iPad landscape
orientations and a valid signature; the Stage 25J-C package verifier now locks
that universal requirement. This was a stale build-product issue, not an
iPad-specific runtime workaround.

The final universal package then passed the equivalent iPadOS 15.8.8 matrix:
native full-screen iPad presentation, both Torremolinos maps, independent
progression and cold restore, LittleEpic/Fear retention, helper/audio behavior,
death/respawn, pause/resume, touch/controller, rotation and background/reopen.

Apple TV was deliberately not physically tested because the hardware was
unavailable. The exact tvOS product, shared closure, package contents,
UserDefaults A/B policies and forbidden-reference scans passed. This evidence
does not substitute for the future physical catch-up run.

## Locks, regression and privacy

The canonical Content/raw/patched/Stage-6 locks remain respectively
`30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46`,
`db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273`,
`0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5`
and `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9`.
The vanilla iOS tree is still `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357`.
Native locks remain iOS `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`
and tvOS `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`.

The current builder passed 398 deterministic tests. Current functional
progression, HookGen, direct-hook, ModInterop, IL-freeze, IL-composition,
custom-FMOD, repository/privacy, documentation-link and package checks passed.
Older stage verifiers that intentionally lock smaller historical transformer
catalogues were preserved rather than weakened and remain accepted at their
historical commits. No GitHub Actions workflow was run; Actions usage is zero.

## Future Apple TV catch-up and recommendation

The cumulative Apple TV run must install the then-current integrated product,
verify prior LittleEpic and Fear state, enter both Torremolinos maps, exercise
and cold-restore progression in each, switch A ↔ B, verify LevelSet aggregates,
test slot delete/recreate, confirm the custom FMOD bank loads once, then cover
Save Manager/soft reload, controller/pause, Home/reopen and graceful Quit.

Stage 25J-C proves the first ordinary real multi-map LevelSet in the shared
static-AOT product. It is ready for development integration on iOS/iPadOS with
tvOS static acceptance, while all-platform release readiness remains blocked
only on the explicitly deferred cumulative Apple TV physical gate.
