# Stage 25J-A — durable custom-map and LevelSet progression

## Result

**PASS — GREEN_IOS_IPADOS / TVOS_PHYSICAL_PENDING**

- iPhone/iPadOS: physical GREEN.
- tvOS: Release/full-AOT build and deterministic storage/recovery GREEN.
- Apple TV physical status: `TVOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE`.
- Last full three-device physical GREEN: `57d55c7b9e15c5fa84847d2879f774f30025a96a`.
- Development integration-ready: **yes**.
- All-platform release eligible: **no**; future RC/release tags remain blocked until the cumulative Apple TV catch-up contract passes.

Stage 25J-A started from `57d55c7b9e15c5fa84847d2879f774f30025a96a` on
`feature/apple-everest-levelset-progression`. It used no GitHub Actions minutes.

## Pinned Everest model and selected architecture

The pinned Everest `stable-1.6458.0` model persists two distinct categories:

1. Custom `AreaStats`/`AreaModeStats` records grouped by stable SID and
   LevelSet for durable area and LevelSet progression.
2. A resumable `Session` for the active custom map.

`EverestModuleSaveData` and `EverestModuleSession` remain in the independent
Stage 25F module-state authority. They are not folded into map progression.

Stage 25J-A uses a hybrid architecture: Celeste's ordinary save XML remains
byte-compatible and contains no custom-map records. A closed, typed sidecar
stores custom `AreaStats` and `Session`; after vanilla deserialization, those
objects are projected into the runtime before module state and the Level load.
This avoids changing the accepted vanilla serializer while still providing the
objects Celeste/Everest code expects during custom gameplay.

Serialization is reflection-free and uses the bounded `AEVPSV1` schema,
version 1, with deterministic little-endian fields and SHA-256 integrity. No
runtime mod discovery, map discovery, JIT, or interpreter path was added.

## Stable ownership and compatibility

Progression compatibility is identified by the canonical combination of:

- SID;
- LevelSet;
- original source-map SHA-256;
- ordered room set.

The runtime Area index is deliberately not persistent identity. Rebuilding or
re-signing the same exact map retains progression. A changed map at the same SID
is quarantined until an explicit compatible migration exists. Removing a map
makes its state inactive but retains it so an exact compatible re-addition can
restore it.

Each numbered slot (0–2) owns an independent random 256-bit lineage plus the
exact serialized vanilla-base SHA-256. A normal save advances the sidecar to the
new base while preserving lineage. A different imported/replaced vanilla base
cannot select stale progression. An exact previous vanilla base deliberately
selects its corresponding previous-good generation. Deleting a slot removes
both progression replicas and both Stage 25F module replicas after the vanilla
delete succeeds; recreating that slot creates a fresh lineage.

Consequently, slot 0 state cannot attach to slot 1, deleted data cannot
resurrect into a recreated slot, and an unrelated imported save cannot inherit
old LittleEpic progression.

## Persisted state

Persistent area/LevelSet state includes:

- `AreaStats.Cassette`;
- `AreaModeStats.TotalStrawberries`;
- completion, single-run completion and full-clear state;
- deaths and time played;
- best time, best full-clear time, best dashes and best deaths;
- heart gem;
- strawberry entity IDs;
- checkpoints.

The resumable map Session includes:

- SID, compatibility identity, mode, room, respawn and start checkpoint;
- time, deaths, dashes, in-area, first-level and started state;
- cassette, heart, dreaming, golden and checkpoint state;
- color grade, lighting, bloom, dark-room and core mode;
- inventory;
- music/ambience plus parameters;
- flags, level flags, strawberries, do-not-load IDs, keys and counters;
- summit gems, C-side unlock, furthest-seen level and best-time state;
- the exact `OldStats` baseline needed for correct delta accounting.

Generated LevelSet helpers aggregate hearts, cassettes, strawberries, deaths and
time across statically registered maps while excluding custom records from
vanilla global totals and achievements. Custom completion updates the custom
area record; after completion statistics, the persistent lane uses Save and
Quit safely.

## LittleEpic evidence

The exact package was LittleEpic's Precision Challenge 1.0.0, SHA-256
`ca57d295f446affdd8eb9b56ffe58f98cc652e9ec242ff07b224877d0067b384`.
The accepted map is:

- SID: `LittleEpic/precisionchallenge/precisionchallenge`;
- LevelSet: `LittleEpic`;
- map SHA-256: `6de18b4280df15493a9f1b0eac2b547118757cf276795146bdbf299e9c371475`;
- rooms: `1`, `2`, `3`, `4`, `5`, `6`, `7`, `heart`;
- zero berries, one heart, zero cassettes and no persistent checkpoint objects.

Because there is no early berry/cassette/checkpoint object, physical persistence
used room transition, death count, elapsed time and the resumable Session. Room
1 retained the DJ zero-dash mechanic and room 5 retained the Chrono 17-second
gate. Generic deterministic fixtures cover collectible, checkpoint, completion
and multi-LevelSet behavior.

On both physical devices, the tester entered the persistent LittleEpic map from
a numbered save, progressed, died, saved and quit, terminated the app, cold
reopened it, selected the same slot, and resumed the stored room/state. Pause,
resume, touch/controller input, rotation, background/reopen, audio and the
existing vanilla save also passed. The persistent lane now enables Save and
Quit; the separately named nonpersistent debug lane remains available.

## Ordering, recovery and storage

Load ordering is vanilla deserialization, newest valid matching progression A/B
selection, custom AreaStats/Session projection, Stage 25F module SaveData and
Session activation, then Level load.

Save ordering is a single consistent capture, vanilla save commit, progression
generation commit/readback, then Stage 25F module commit. A vanilla failure
discards pending custom/module writes. A progression failure reports the save
failure while leaving vanilla usable and previous custom replicas isolated. The
existing bounded UserIO one-follow-up coalescing remains unchanged.

On iOS/iPadOS, two replicas live privately under
`Library/Application Support/Celeste/Everest/Progression/<slot>` as
`levelset-state-v1.a.snapshot` and `levelset-state-v1.b.snapshot`, using atomic
Foundation replacement. They are not exposed through Files.

On tvOS, each slot uses two compressed UserDefaults values:
`CelesteAppleEverest.Slot<0-2>.Progression.<A|B>.v1`. The bounded `AEVPZV1`
deflate envelope authenticates both logical and compressed representations.
Each replica is capped at 126,976 bytes; the complete three-slot A/B budget is
761,856 bytes. The shared logical maximum is 1 MiB, with 128 maps, three modes
per map, 16,384 collection items and 4,096-byte UTF-8 strings.

A and B validate independently. Selection uses the newest valid generation
matching slot, lineage, exact vanilla base and installed compatible maps. A
torn newest replica falls back to the previous valid match. If both replicas
are malformed, oversized or corrupt, custom progression starts empty while the
vanilla save and independent module state remain usable. Cold launch and soft
reload reselect from durable replicas instead of process-global state.

Measured deterministic payloads were:

| Fixture | Raw | tvOS compressed |
|---|---:|---:|
| Representative LittleEpic fixture | 1,029 bytes | 437 bytes |
| 64-map/two-LevelSet stress fixture | 164,147 bytes | 5,324 bytes |

## Deterministic closure and products

Three independent generation runs were identical:

| Lock | SHA-256 |
|---|---|
| compatibility manifest | `48dc3df7886f1a32fe49d4cdecde2c692a95c7ec4aac345de522466408cdb7b0` |
| progression manifest | `a8cddafb90f3698b821c34ecc8d3bb8d6674b03860436bfec27d902429f7ebac` |
| generated schema source | `460c46df4863a16e8f8c7e88bb0f7413e415da46731c158bc09624af2d65d9b2` |
| managed tree | `31bd37ee46719b5bd8c9913184a184d66971b889bcc9e0bcd3a55b20febdae04` |
| content tree | `f2b3d04ab37d4ce7a260777544939f5835307c9d92e127dab137535fadccd9e3` |
| source-free shared closure | `f1345b601246a67e1d6bae9fdfc1c0d789540c945fc3308d720dab7fbb3f2d11` |

The same source-free pre-platform closure produced both Release products with
full trim, full AOT, `UseInterpreter=false`, and no JIT:

| Product | Bytes | SHA-256 |
|---|---:|---|
| signed universal iOS/iPadOS IPA | 884,176,257 | `33cee3f7e71c69ec64b0183042e17dbc79a2cb09ce5c68abea6d96504e9bb756` |
| unsigned tvOS IPA | 898,121,634 | `6f407f575b94e763cc9bcebf665d7959b8e921efadb612485c540c4ef7317cb5` |

The iPhone 12 Pro Max on iOS 26 and iPad mini 4 on iPadOS 15.8.8 both passed
with the same signed universal IPA. The tvOS product passed package/static and
deterministic persistence/recovery checks, but was not installed on Apple TV in
this travel stage.

## Regression and lock results

- AppleEverestBuilder: 370 deterministic tests passed.
- Stage 25I-B historical contract: 75 checks passed at its accepted commit.
- Pinned MonoMod IL conformance/freeze, composed H-B, graph H-C and direct H-D
  ordering/composition remained green.
- Real ModInterop/HookGen reference acceptance, Stage 25F module durability,
  Stage 25E helper ecosystem and Stage 25D managed detours remained green.
- LittleEpic custom horn, ExpertContestHelper bank, Chrono gate and real DJ
  frozen-IL trigger passed on iPhone and iPad.
- The 56 input-profile tests and unchanged public builder entry points passed.
- Repository documentation/link/privacy checks passed.
- No third-party ZIP/DLL/map/bank, Celeste/FMOD data, save/progression payload,
  signing material, private path or device identifier is tracked.

Canonical locks remained unchanged: Content `30a1c147…`, raw `db7b722…`,
patched `0c6515…`, Stage-6 audio `1a981bc…`; iOS native `9fb302…`, tvOS native
`6286e0…`, and vanilla iOS generated source `2f5d6f…`. The custom bank
`2607c3…`, audio manifest `0a7ca2…` and DJ IL plan `5791ac…` also remained
locked. Protected recovery/release refs were untouched.

## Required Apple TV catch-up

The future cumulative Apple TV stage must use the then-current integrated tip
and physically verify:

1. launch/FNA3D Metal;
2. seven base FMOD banks plus the ExpertContestHelper custom bank;
3. LittleEpic rooms 1 and 5;
4. the real DJ frozen-IL trigger;
5. progression and Save and Quit;
6. cold resume;
7. slot isolation and delete/recreate;
8. Save Manager and soft reload;
9. controller and pause;
10. Home/reopen and cold relaunch.

Until that passes, Stage 25J-A is development-integration-ready but not an
all-platform release/RC baseline.

## Forward assessment

Persistent real custom maps substantially shorten the path to another medium
standalone map and to a small multi-map LevelSet. A second medium map is the
closest high-value proof because it exercises this new identity/progression
model with a different real graph. Configured-IL ordering remains important but
is not yet higher-value than that independent map proof.

A small multi-map LevelSet is now close: it principally needs real multi-map
selection/transition and aggregate progression evidence. A small collab still
needs broader map composition/UI, helper graph breadth and stress of shared
LevelSet progression. Broader custom audio needs multi-bank/event collision and
lifecycle coverage. Configured-IL-heavy helpers need deterministic config
capture, ordering/conflict policy and more real helper graphs. Strawberry Jam
is not rational until those collab-scale, custom-audio, configured-IL, entity/
trigger/helper breadth and storage-stress boundaries are discharged.

## Direct acceptance answers

1. Pinned Everest persists custom AreaStats/AreaModeStats and a resumable map Session; module SaveData/Session remain separate.
2. Durable Area/LevelSet fields are the completion/collectible/checkpoint, death/time and best-stat fields listed above.
3. Resumable Session fields are identity/room/respawn, counters, inventory, environmental, audio, flags/collections and exact OldStats baseline listed above.
4. `EverestModuleSaveData` and `EverestModuleSession` remain Stage 25F state.
5. Vanilla save XML was not modified.
6. Not applicable; byte-compatible vanilla serialization is preserved.
7. A typed authenticated A/B sidecar is selected and projected after vanilla load.
8. LittleEpic is owned by SID + LevelSet + map SHA + ordered rooms, within a slot lineage/base hash.
9. Yes, the same exact map survives rebuild/re-sign.
10. Changed map bytes create an incompatible identity and quarantine old state.
11. A slot number, 256-bit random lineage and exact vanilla-base SHA identify ownership.
12. No, cross-slot selection is rejected.
13. Vanilla deletion is followed by progression and module A/B deletion.
14. Recreation receives a fresh lineage; deleted state cannot return.
15. A changed imported/replaced vanilla base cannot select the old sidecar.
16. No stale LittleEpic progression can attach to an unrelated replacement.
17. LittleEpic has eight rooms, one heart, no berries/cassettes/persistent checkpoints, and completion stats.
18. Physical proof used room transition, death/time changes and resumable Session.
19. Room/session and accumulated custom AreaStats persist after Save and Quit.
20. Yes, both physical devices cold-resumed the saved map state.
21. Yes, Save and Quit is enabled in the persistent numbered-slot lane.
22. Yes, the explicitly named debug lane remains separate and nonpersistent.
23. Yes, vanilla slots remain byte/semantically safe.
24. Yes, progression corruption is isolated from vanilla saves.
25. Yes, independent A/B validation and older-valid fallback pass.
26. Yes, serialization is reflection-free.
27. Schema is `AEVPSV1`, version 1.
28. The representative raw payload is 1,029 bytes.
29. The 64-map/two-LevelSet stress payload is 164,147 bytes raw.
30. That stress payload is 5,324 bytes in the tvOS envelope.
31. The complete tvOS three-slot A/B budget is 761,856 bytes.
32. Yes, Stage 25F module durability is unchanged and isolated.
33. Map state projects first, then module SaveData/Session activate, then the Level loads.
34. Yes, custom FMOD remains functional.
35. Yes, the horn passed on iPhone and iPad.
36. Yes, the real DJ trigger passed on both.
37. Yes, the real Chrono gate passed on both.
38. Yes, the locked DJ frozen IL plan is unchanged.
39. Yes, one source-free shared graph produces both products.
40. Yes, iPhone physical progression passed.
41. Yes, iPadOS 15.8.8 physical progression passed.
42. No, Apple TV was not physically tested for J-A.
43. Yes, its exact status is `TVOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE`.
44. Yes, a Release tvOS product built.
45. Yes, it is fully trimmed and full AOT.
46. Yes, `UseInterpreter=false`.
47. Yes, tvOS deterministic persistence and recovery tests are green.
48. The exact ten-item cumulative Apple TV checklist is recorded above.
49. The last all-three physical GREEN SHA is `57d55c7b9e15c5fa84847d2879f774f30025a96a`.
50. Yes, release/RC tags are blocked until Apple TV catch-up.
51. H-D remained green.
52. H-C remained green.
53. H-B remained green.
54. H-A remained green.
55. B2 remained green.
56. F-A remained green.
57. Stages 25E and 25D remained green.
58. Yes, vanilla builders are unchanged.
59. Yes, canonical/native locks are unchanged.
60. Yes, protected refs are untouched.
61. Yes, zero Actions minutes were used.
62. The final clean-clone reproduction result is recorded by the final verifier/acceptance handoff.
63. Yes, J-A is development-integration-ready.
64. No, J-A is not all-platform-release-ready.
65. The exact final feature commit reported at handoff is the development fast-forward target.
66. Run the ten-item cumulative Apple TV checklist above.
67. The closest next proof is a second, harder medium standalone real map selected by graph distance.
68. No; another independent real map currently provides more value than configured IL alone.
69. A small multi-map LevelSet is close; real selection/transition and aggregate evidence remain.
70. A small collab is farther: composition/UI, broader helper graphs and progression stress remain.
71. Broader audio needs multiple banks/events, collision policy and lifecycle evidence.
72. Configured-IL-heavy helpers need deterministic config capture, ordering/conflicts and real graph evidence.
73. Strawberry Jam requires collab scale, broader helpers/entities/triggers, multi-bank audio, configured IL and storage stress first.
