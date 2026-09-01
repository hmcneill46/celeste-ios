# Apple Everest Stage 25K-C — Strawberry Jam dependency-gap audit

Status: **PASS — GREEN_AUDIT**

This is an audit result, not a product-support claim. In particular, it does **not** claim `STRAWBERRY_JAM_SUPPORTED`, and this stage did not build or run Strawberry Jam on an Apple device.

## Scope and reproducibility

The audit pins the distributed Strawberry Jam 2021 1.0.12 package and every required ordinary public release in its dependency closure. It inventories the packages without loading reviewed assemblies: the added Mono.Cecil census reads metadata and IL only. Maps are parsed with the Apple builder's host parser, while an independent root parser records the bounded trailing appendix accepted by desktop Celeste but currently rejected by the Apple builder. Audio classification is derived from every distributed bank plus the adjacent public GUID exports.

The canonical evidence is split so that graph identity, content ownership, executable mechanisms, overall decisions, and the independent third-collab control can each be checked without ambiguity:

- [overall audit](../../../apple-everest/strawberry-jam-stage25kc.json)
- [dependency graph](../../../apple-everest/strawberry-jam-dependency-graph-stage25kc.json)
- [content ownership](../../../apple-everest/strawberry-jam-content-usage-stage25kc.json)
- [mechanism census](../../../apple-everest/strawberry-jam-mechanisms-stage25kc.json)
- [third-collab control](../../../apple-everest/third-collab-control-stage25kc.json)
- [audit generator](../../../scripts/generate-apple-everest-stage25kc.py)
- [clean acquisition driver](../../../scripts/fetch-apple-everest-stage25kc-fixtures.py)
- [verifier](../../../scripts/verify-apple-everest-stage25kc.py)

The public sources pinned on 2026-09-01 are the [Everest update database](https://maddie480.ovh/celeste/everest_update.yaml), [Everest dependency graph](https://maddie480.ovh/celeste/mod_dependency_graph.yaml), [Everest search database](https://maddie480.ovh/celeste/mod_search_database.yaml), [distributed package](https://gamebanana.com/mmdl/1414214), [GameBanana page](https://gamebanana.com/mods/424541), and [official source repository](https://github.com/StrawberryJam2021/StrawberryJam2021). The distributed archive has no cryptographic source-commit binding or release tag; `890526ff026be31e3f728ee0ea14bbf13631d2dc` is only the closest public commit by timestamp, not a claim of source equivalence. The three whole-catalogue hashes are acquisition snapshots: those catalogues can change in unrelated records, so fresh acquisition semantically verifies the exact 52 pinned release records and independently reconstructs their required graph from every distributed `everest.yaml`. GameBanana package URLs are fetched through the [official Everest updater mirror](https://github.com/maddie480/EverestUpdateCheckerServer), while the recorded upstream URLs and every expected archive SHA-256 remain authoritative.

The clean acquisition driver starts from an empty ignored fixture directory, reacquires only the exact public releases recorded by the artifacts, checks all SHA-256 pins before extraction, rejects unsafe archive paths, performs the 47-DLL metadata census, parses all maps, creates all three progression fixtures, and regenerates all five JSON files. Two empty-fixture acquisitions and a clean recursive clone reproduce byte-identical deterministic JSON. Acquisition timestamps are deliberately fixed in the tracked evidence rather than injected at run time.

## Quantitative result

The required graph is 52 packages (46 code-helper packages, 47 DLLs), 1,237,284,560 compressed bytes, 1,343,650,014 expanded bytes, and 48,475 content files—6.4333 times K-B's 7,535-file closure. It contains 43,463 PNGs, 519 YAML files, 132 map BINs, 1,303 Lua files, and 149 FMOD banks. The root contains 128 maps, 2,585 rooms, six difficulty/prologue lobbies, six gyms/prologues, 111 ordinary maps, and five heart sides.

All 722 namespaced custom IDs and all 274,073 occurrences resolve to exactly one provider. There are no unknown package dependencies, provider owners, or major mechanism classes. This completeness is why the status is GREEN_AUDIT even though runtime coverage is intentionally low.

One newly bounded parser gap is important: 124 of 128 maps contain a valid Celeste root followed by 19,240,291 total appendix bytes. Desktop Celeste's `BinaryPacker` stops after the root and ignores those bytes; the Apple builder currently rejects them with its strict trailing-data check. K-C records this as the first slice blocker instead of weakening the parser during an audit stage.

### Helper taxonomy

Every required package is assigned exactly once:

- A, accepted unchanged (17): Batteries, CanyonHelper, CollabUtils2, ColoredLights, CommunalHelper, DJMapHelper, DisposableTheo, EeveeHelper, FancyTileEntities, FurryHelper, LunaticHelper, MaxHelpingHand, ShroomHelper, StrawberryJam2021Assets, TwigHelper, XaphanHelper, memorialHelper.
- B, accepted mechanism but more exact descriptors/API breadth needed (17): Anonhelper, BounceHelper, BrokemiaHelper, CavernHelper, CherryHelper, EmHelper, FemtoHelper, FlaglinesAndSuch, FrostHelper, HonlyHelper, JackalHelper, MoreDasheline, PandorasBox, SafeRespawnCrumble, Sardine7, SpirialisHelper, VivHelper.
- C, plausible immutable semantic lowering (6): AdventureHelper, ContortHelper, ExtendedVariantMode, JungleHelper, OutbackHelper, YetAnotherHelper.
- D, bounded new mechanism (11): BGswitch, CrystallineHelper, FactoryHelper, GravityHelper, IsaGrabBag, SorbetHelper, StrawberryJam2021, StrawberryJam2021AudioA, StrawberryJam2021AudioB, StrawberryJam2021AudioC, VortexHelper.
- E, major unsupported runtime class (1): LuaCutscenes. The required replacement is audited per-script state machines, not a general Lua VM.
- F and G: zero.

### Executable-mechanism census

Across the 47 DLLs, the exact metadata census found 1,497 `On.*` subscriptions, 606 ordinary `IL.*` subscriptions, 79 direct `Hook` constructors, 231 direct `ILHook` constructors, 67 `DetourConfig` references, 38 `DetourContext` references, 740 `EmitDelegate` calls, 1,083 `DynamicData` calls, 639 `DynData` calls, 53 `FastReflection` calls, 49 ModInterop calls, 5,540 other reflection calls, 35 `DynamicMethod` calls, 189 `Reflection.Emit` calls, and 163 FMOD calls. Runtime `Assembly.Load`, `Process`, filesystem watcher, and P/Invoke counts are all zero.

Configured detour/IL ordering has 39 exact construction sites in 13 helpers. Twelve providers are statically freezeable; ExtendedVariantMode retains dynamic lifecycle behavior requiring semantic lowering. Wildcard/default/BeforeAll/AfterAll identities create same-target composition requirements. Those providers or their dependency-global behavior affect all 128 maps and 1,797 rooms, so immutable ordering is the highest-value next mechanism.

DynamicData/DynData totals 1,722 calls in 597 methods across 296 owner classes. Uses divide into exact field accessors, extra-data dictionary semantics, runtime type lookup, optional-helper integration, mutable object attachment, and general reflection. Existing bounded lowering covers exact accessors and frozen optional integration only; general mutable attachment and runtime type lookup remain a new class.

ModInterop has 12 registration calls, 46 export declarations, 18 import declarations, and 49 metadata call references. Static dependency ordering resolves all 13 required-graph imports; five missing imports are optional and zero delegate signatures are unsupported. Accepted F-B2 static ModInterop is sufficient.

The IL inventory separates nine already accepted H-A frozen sites from H-B ordered composition, H-C broader ordinary IL, and H-D immutable direct ILHook candidates. Dynamic targets/lifetimes, same-target direct composition, configured IL, and unsupported captures stay explicitly unsupported until lowered.

### Root DLL, content, audio, and state

`StrawberryJam2021.dll` is not treated as a small content shim. Its bounded D-class audit covers module lifecycle; Settings, SaveData, and Session; hooks and IL; 111 root-provided custom IDs; special berries; lobby/map/journal behavior; graphics, cutscenes, toggles, and audio calls. It requires root-specific schemas for its state but exposes no unbounded persistence mechanism.

K-B semantics remain sufficient for MiniHeartDoor, SilverBerry, RainbowBerry, SpeedBerry, and ordinary GoldenBerry use. Whole-SJ still needs exact behavior for `SJ2021/ExplodingStrawberry`, `LunaticHelper/StrawberryWithReturn`, `SorbetHelper/ReturnBerry`, `BrokemiaHelper/trollStrawberry`, and MaxHelpingHand multi-room strawberry state.

The closure has 149 bank files and 117 adjacent GUID exports. There are 71,976 raw GUID records, 2,091 unique GUIDs and 2,123 unique paths, including 1,706 unique event paths, 119 buses, three VCAs, and 58 snapshots. Thirty-six GUID collisions and five path collisions require a graph-wide deterministic policy. There are no distributed master/strings banks, programmer sounds, native plugins, or DSP payloads. Every bank is therefore `BOUNDED_MULTI_BANK_EXTENSION`, not fundamentally unsupported.

LuaCutscenes entities are actually reachable in 15 maps and 70 rooms, so Lua cannot be dismissed as unused. There are no native files, P/Invoke methods, runtime assembly loads, processes, or watchers. The minimum Beginner slice does not use Lua.

Deferred dimension-only atlas mounting remains O(metadata): its measured lower bound is 1,390,816 bytes (32 bytes per PNG, excluding path strings). The largest single decoded RGBA surface is 15,269,888 bytes for `Necro0.png`. No test eagerly decodes all 43,463 PNGs. The static manifest is 52 package records, one LevelSet record, 128 progression records, 722 factory IDs, 116 journal rows, 25 chapter-panel trigger records, and ten MiniHeartDoor instances; sorted/hash indexing is O(N log N), with no required O(map-count²) pass.

AEVPSV1 remains comfortably within its 126,976-byte replica cap. For 128 maps, incomplete/partial/complete fixtures are respectively 38,921/41,925/48,101 raw bytes and 7,244/10,103/14,516 compressed bytes. Complete state consumes 11.4321% of one replica and 87,096 bytes across all three A/B slots.

## Smallest coherent slice and full blockers

The minimum meaningful unchanged-content slice is the Beginner lobby `StrawberryJam2021/0-Lobbies/1-Beginner` plus `StrawberryJam2021/1-Beginner/Bing_Over_Google`, including lobby UI, Return to Lobby, completion, and journal. Its exact 22-provider closure is BrokemiaHelper, CherryHelper, CollabUtils2, ContortHelper, CrystallineHelper, DJMapHelper, EverestCore, ExtendedVariantMode, FancyTileEntities, FemtoHelper, FlaglinesAndSuch, FrostHelper, HonlyHelper, JungleHelper, LunaticHelper, MaxHelpingHand, PandorasBox, StrawberryJam2021, VivHelper, VortexHelper, XaphanHelper, and YetAnotherHelper.

Its seven ordered blockers are:

1. accept the bounded 124-map binary appendix format without accepting malformed root data;
2. freeze exact configured detour/IL order and same-target composition;
3. add only the evidenced current descriptors/APIs for Brokemia, Cherry, Femto, Flaglines, Frost, Honly, Pandora, and Viv;
4. add immutable lowerings for Contort, ExtendedVariantMode, Jungle, and YetAnotherHelper;
5. lower the StrawberryJam2021 root lobby/journal/settings/save/session surface;
6. add bounded multi-bank FMOD registration plus collision policy;
7. add the Crystalline/Vortex bounded mechanisms used by the lobby.

For full SJ, the ranked mechanisms and exact reach are: configured ordering (13 helpers, 128 maps, 1,797 rooms); root DLL lowering (one helper, 75 maps, 701 rooms); multi-bank FMOD (11 helpers, 128 maps, 2,585 rooms); descriptor breadth (17 helpers, 126 maps, 1,702 rooms); general DynamicData (28 helpers, 128 maps, 1,793 rooms); six static helper lowerings (102 maps, 1,018 rooms); and LuaCutscenes replacement (15 maps, 70 rooms). Zero maps presently have zero known blockers.

Coverage is deliberately reported as separate denominators: packages 17/52 (32.6923%); accepted custom-ID occurrences 43,350/274,073 (15.817%); zero-known-blocker maps 0/128 (0%); accepted helper-mechanism sites 3,818/11,009 (34.6807%); lobby/UI 0/6; audio 0/149; and progression fixtures 3/3 (100%).

Ten credible third-collab candidates were evaluated with the same taxonomy. CrossoverCollabAprilDemo 1.0.5 is selected: nine closure packages, two maps, and seven blocked packages (two B, three C, two D). It is easier than the first SJ slice, but repeats already known classes across only two maps, so it is less architecturally valuable than the measured Strawberry Jam work.

The value model selects **Stage 25K-D — configured detour/IL ordering and current-helper descriptor breadth**. Its scope is to freeze exact pinned Before/After/priority composition, reject dynamic lifecycle cases, expand only the exact descriptors/APIs evidenced by the slice, and keep Strawberry Jam content unchanged. Multi-bank FMOD and bounded helper/root lowerings follow; a runtime SJ product build is not yet rational.

## Answers to the 98 closeout questions

1. Exact Strawberry Jam release/version? `StrawberryJam2021` 1.0.12, released 2025-04-03T18:13:56Z (GameBanana mod 424541, file 1414214).
2. ZIP SHA? `4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655` (95,650,820 bytes).
3. Public URL? `https://gamebanana.com/mmdl/1414214`, corroborated by the three pinned Everest databases and the official repository linked above.
4. Root DLL SHA? `8d5b9184204e7e7728965bcf95af5f150f6220dafbfe52fdd6c23e50d47e5258`.
5. Direct dependencies? The distributed release has 51 ordinary direct dependencies—every other graph package—and Everest as a runtime dependency; SpeedrunTool 3.20.4 and CelesteTAS 3.25.8 are optional. Exact names and minimum versions are in the dependency-graph artifact; this independently disproves the stale ten-helper source-list assumption.
6. Total transitive required packages? 52 downloaded ordinary packages, excluding the provided Everest/Celeste runtime.
7. Code-helper count? 46 packages; 47 physical DLLs because JackalHelper distributes two.
8. Map count? 128.
9. Room count? 2,585.
10. Lobby count? Six difficulty/prologue lobbies (plus six gyms/prologues, 111 ordinary maps, and five heart sides).
11. Total content files? 48,475.
12. Total expanded bytes? 1,343,650,014; exact compressed closure is 1,237,284,560 bytes.
13. Custom IDs? 722 distinct namespaced entity/trigger/backdrop IDs.
14. How many custom-ID occurrences? 274,073, all uniquely owned.
15. Top 20 provider helpers? VivHelper 97,260; FrostHelper 86,809; MaxHelpingHand 24,962; StrawberryJam2021 9,095; EverestCore 7,874; HonlyHelper 5,984; ContortHelper 5,902; PandorasBox 5,096; FemtoHelper 5,037; LunaticHelper 3,202; ExtendedVariantMode 2,618; CommunalHelper 2,539; CrystallineHelper 2,468; IsaGrabBag 1,566; FlaglinesAndSuch 1,438; FactoryHelper 1,416; AdventureHelper 909; XaphanHelper 880; FancyTileEntities 868; ShroomHelper 855.
16. How many required helpers already accepted unchanged? 17 of 52.
17. How many need descriptor/API breadth only? 17.
18. How many fit static semantic lowering? Six.
19. How many require a genuinely new mechanism? Eleven bounded D-class packages plus one major E-class package; D is the direct answer, while E is reported separately to avoid hiding it.
20. Exact configured-hook site count? 39 sites in 13 helpers, with 67 DetourConfig and 38 DetourContext metadata references.
21. Maps affected by configured hooks? All 128 dependency-closed maps; exact content-feature reach is 1,797 rooms.
22. Exact direct ILHook count? 231 constructor sites.
23. Exact ordinary IL count? 606 `IL.*` event subscriptions; nine sites are already accepted H-A frozen transforms.
24. DynamicData count/classes? 1,722 total calls (1,083 DynamicData + 639 DynData), 296 owner classes and 597 methods; six semantic classes are listed in the mechanism section above.
25. ModInterop import/export count? 18 imports, 46 exports, 12 registration calls, and 49 metadata call references.
26. Can accepted static ModInterop handle them? Yes: 13 required imports resolve in frozen graph order, five missing ones are optional, and zero signatures are unsupported.
27. Custom bank count? 149 bank files across the full closure.
28. Event count? 1,706 unique event paths (58,789 raw event records).
29. Any master/strings banks? No; zero of each in the distributed closure.
30. Programmer sounds? None found in exports or managed callback evidence.
31. FMOD plugins? None: zero native/plugin/DSP payloads and zero P/Invoke.
32. Lua required? Yes, in 15 maps and 70 rooms for `luaTalker`/`luaCutsceneTrigger`; not in the minimum slice.
33. Native/PInvoke required? No; zero native files and zero P/Invoke methods.
34. Runtime Assembly.Load required? No; zero sites.
35. Process/FileSystemWatcher relevant? No; both counts are zero.
36. SJ root DLL Settings? Yes; one bounded module settings surface.
37. SaveData? Yes; one bounded module save-data surface.
38. Session? Yes; one bounded module session surface.
39. New persistence semantics? Yes, bounded root-specific fields and schemas; no unbounded/general persistence mechanism.
40. MiniHeartDoor coverage sufficient? Yes for the ten observed instances under accepted K-B semantics.
41. SilverBerry sufficient? Yes.
42. RainbowBerry sufficient? Yes.
43. SpeedBerry sufficient? Yes.
44. Other special berries? Ordinary GoldenBerry is sufficient; ExplodingStrawberry, StrawberryWithReturn, ReturnBerry, trollStrawberry, and multi-room strawberry state need exact bounded semantics.
45. K-B deferred atlas architecture sufficient at SJ scale? Yes; the host stress audit finds no eager-decode regression and preserves O(metadata) mounting.
46. Estimated metadata resident memory? 1,390,816-byte measured floor, excluding path strings.
47. Biggest first-use texture burst? 15,269,888 decoded RGBA bytes for the 1864×2048 `Necro0.png` surface.
48. AEVPSV1 all-map raw fixture size? 48,101 bytes for all-complete; incomplete is 38,921 and representative partial is 41,925.
49. tvOS compressed size? 14,516 bytes for all-complete; incomplete is 7,244 and partial is 10,103.
50. Replica percentage? Worst measured is 11.4321% of the 126,976-byte replica cap.
51. Would current cap hold? Yes; even all three A/B slots total 87,096 bytes for all-complete state.
52. Smallest coherent SJ vertical slice? Beginner lobby, one unchanged ordinary map, Return to Lobby, completion, and journal.
53. Exact lobby? `StrawberryJam2021/0-Lobbies/1-Beginner`.
54. Exact maps? `StrawberryJam2021/1-Beginner/Bing_Over_Google`.
55. Required helpers for that slice? The exact 22-provider closure is listed in the slice section above and machine-readable in the overall artifact.
56. Number of remaining slice blockers? Seven.
57. What are they in order? Map appendix acceptance; configured ordering; exact descriptor/API breadth; four immutable semantic lowerings; SJ root lifecycle/UI/persistence; multi-bank audio/collision policy; Crystalline/Vortex bounded mechanisms.
58. Is configured ordering one? Yes, blocker 2 and the highest-value reusable mechanism.
59. Is custom audio one? Yes, blocker 6; the blocker is multi-bank registration and collision policy, not an unsupported FMOD plugin.
60. Is DynamicData one? Yes, within descriptor/lowering/root work; the slice's relevant usages must be classified and frozen rather than receiving a general runtime implementation.
61. Is Lua/native one? No for the minimum slice; Lua is a full-SJ blocker and native code is absent.
62. How many FULL SJ maps are already zero-known-blocker? Zero of 128.
63. Package coverage? 17/52 = 32.6923% wholly accepted.
64. Content-ID coverage? 43,350/274,073 = 15.817% of occurrences have already accepted exact provider semantics.
65. Map coverage? 0/128 = 0% zero-known-blocker.
66. Helper-mechanism coverage? 3,818/11,009 = 34.6807% exact census sites owned by wholly accepted packages.
67. Lobby/UI coverage? 0/6 = 0%; K-B supplies primitives, but SJ root-specific lobby/UI behavior is not lowered.
68. Audio coverage? 0/149 accepted as a complete graph; all 149 are bounded multi-bank extensions pending collision policy.
69. Progression coverage? 3/3 deterministic SJ-scale fixtures = 100%.
70. Which unsupported mechanism unlocks the most SJ maps? Configured detour/IL ordering reaches all 128 and is the best first unlock; multi-bank audio and DynamicData also reach all 128 but carry higher risk.
71. Which unlocks most ecosystem maps overall? Configured ordering, with 128 SJ and 48 audited control-candidate maps in the value model, ahead of multi-bank audio's 128 + 41.
72. Are those the same mechanism? Yes: configured detour/IL ordering.
73. Third-collab control selected? CrossoverCollabAprilDemo 1.0.5 (`9e2c09ca1110c771274d3706ff477ca11bd1d752ec901f54070db88df2f31ed8`).
74. Its blocker count? Seven blocked packages in a nine-package, two-map closure: two B, three C, and two D.
75. Is a third collab easier than first SJ slice? Yes.
76. Is it more architecturally valuable? No; it repeats known classes at much smaller measured scale.
77. Should next stage implement configured ordering? Yes, with immutable target-local composition and dynamic-lifecycle rejection.
78. DynamicData? Not as an unbounded general runtime next; freeze exact slice cases after ordering/descriptor breadth.
79. Multi-bank audio? Required, but after configured ordering/descriptor breadth; its collision policy needs its own bounded work.
80. Helper semantic lowering? Required after the exact descriptors: six full-graph C packages, with Contort, ExtendedVariantMode, Jungle, and YetAnother in the slice.
81. First SJ vertical slice? Not yet; K-C audits it but deliberately does not build it.
82. Is an actual full Strawberry Jam build rational after K-C? No.
83. If not, what exact prerequisites remain? Bounded map-appendix ingest; configured composition; exact descriptors; root DLL UI/state/lifecycle; multi-bank audio; bounded helper mechanisms; general DynamicData class separation; six static helper lowerings; and per-script LuaCutscenes replacement for full coverage.
84. Estimate how many bounded stages to a first SJ slice. Three: K-D ordering/descriptors, one bounded root/map/helper-lowering stage, then multi-bank audio plus a host/physical slice-validation stage. This is an estimate, not a schedule promise.
85. Estimate how many compatibility classes to full SJ. Four non-accepted taxonomy classes remain (B–E), spanning 35 packages, plus the separately bounded map-appendix input rule.
86. Did K-C change runtime production code? No.
87. If yes, why? Not applicable; changes are audit tooling, a metadata-only builder command, deterministic tests, evidence, and documentation.
88. Builder tests? PASS, including the deterministic SJ-scale AEVPSV1 fixtures and existing builder suite.
89. K-C verifier? PASS, 64 checks.
90. Clean clone reproduced? Yes: an empty-fixture independent reacquisition regenerates all five artifacts byte-for-byte before closeout.
91. Protected refs untouched? Yes: `tvos-port`/remote remain `311590334cf7180c2f9df0e8000578c14fa68fad`; iOS RC is `27e16b4724d94d3991b99c4795f680fcb0e5830c`; RC1/RC2/release-RC3 remain pinned, and the RC3 tag remains absent.
92. Actions zero? Yes; no workflow or Actions run was triggered.
93. Privacy pass? Yes: no reviewed archives, assemblies, maps, banks, game data, signing/saves, private host paths, or hardware-specific values are tracked.
94. Disk before/after? About 51.7 GiB free at the final audit run start and at least 20 GiB throughout; the exact post-clean-clone value is reported in the final handoff because disposable fixture cleanup occurs after this document is committed.
95. Final SHA? The immutable feature tip containing this report; resolve with `git rev-parse HEAD`. Its exact SHA is necessarily reported in the final handoff because a commit cannot contain its own SHA.
96. Integration-ready? Yes, for audit integration only, after the clean-clone proof; not a Strawberry Jam runtime-support claim.
97. Exact fast-forward SHA? The same feature-tip SHA reported in the final handoff; it fast-forwards from `d17f84796eaca56b2cd427f444492bb7a5bc2c0d`.
98. Recommended next stage name and scope? **Stage 25K-D — configured detour/IL ordering and current-helper descriptor breadth**: freeze exact pinned Before/After/priority composition, reject dynamic lifetimes, widen only evidenced descriptors/APIs, and keep SJ content unchanged.

## Final boundary

The dependency and mechanism gap is completely classified, so Stage 25K-C is **PASS — GREEN_AUDIT**. Strawberry Jam remains **not yet supported**. No merge, tag, pull request, protected-ref mutation, product build, or hosted workflow run was performed.
