# Stage 25K-E — Strawberry Jam Beginner helper/root semantic lowering

Stage 25K-E closes the two semantic blocker groups selected after K-D: the exact
Beginner-reachable behavior of ContortHelper, ExtendedVariantMode,
JungleHelper, and YetAnotherHelper, plus the StrawberryJam2021 root lifecycle,
typed state, lobby UI, journal, completion, and Return-to-Lobby behavior. The
accepted implementation is generated, typed, and static. It does not add a
general DynamicData backend, a runtime helper loader, live DetourContext
lifetimes, Lua, reflection-based module activation, or device-side source
analysis.

The scope remains deliberately smaller than a playable Strawberry Jam build.
The Beginner lobby and Bing_Over_Google are still blocked by four exact custom
FMOD banks and 55 authored CrystallineHelper/VortexHelper occurrences. Those
two bounded groups are the Stage 25K-F handoff. The first unchanged playable
slice remains planned for K-G.

## Evidence

The machine-readable results are the [helper semantic census](../../../apple-everest/sj-helper-semantics-stage25ke.json),
[root semantic census](../../../apple-everest/sj-root-semantics-stage25ke.json),
[readiness recount](../../../apple-everest/sj-beginner-readiness-stage25ke.json),
[Beginner lobby manifest](../../../apple-everest/sj-beginner-lobby-manifest-stage25ke.json),
and [Bing manifest](../../../apple-everest/sj-beginner-bing-manifest-stage25ke.json).
The generator, three-run reproducer, and fail-closed verifier are
[generate-apple-everest-stage25ke.py](../../../scripts/generate-apple-everest-stage25ke.py),
[reproduce-apple-everest-stage25ke.py](../../../scripts/reproduce-apple-everest-stage25ke.py),
and [verify-apple-everest-stage25ke.sh](../../../scripts/verify-apple-everest-stage25ke.sh).

Every method in the five distributed DLLs has one of the five required
classifications and none is unknown. The method totals are 721 Contort, 2,374
ExtendedVariantMode, 695 Jungle, 116 YetAnother, and 2,192 StrawberryJam2021.
The accepted slice replaces all 12 reachable DynamicData calls and 35 reachable
reflection sites with typed access, generated lookup, or fixed dispatch. The
distributed binaries contain 83 DynamicData calls and 393 reflection calls in
total; unreached behavior remains outside the accepted product instead of being
silently generalized.

The exact lobby uses 209 MossyWall entities, four horizontal Always-mode
BubbleFields, three Contort spotlight triggers, three BackgroundBrightness fade
triggers, one EVM reset trigger, and 72 root entity occurrences. Bing adds the
one required LunaticHelper StrawberryWithReturn. Across the two selected maps,
865 of 920 custom-ID occurrences are accepted or vanilla; the remaining 55 are
the exact Crystalline/Vortex handoff. Whole-package coverage remains honestly
17/52 and helper-mechanism coverage remains 3,818/11,009 because this stage does
not claim complete semantics for any of the newly lowered packages. Across all
128 SJ maps the eleven lowered IDs occur 4,753 times, but only the bounded
Beginner parameter/state surface is accepted here.

The root SaveData schema uses the existing Stage 25F per-module typed YAML A/B
aggregate. AEVPSV1 remains version 1 and stays a separate map-progression
failure domain. Settings and Session are closed typed models. Unknown SaveData
fields are ignored, unknown schema versions are rejected, and no reflection
serializer is used.

The exact four-bank K-F audio handoff covers `event:/sj21_jamjar-blue`,
`event:/sj21_BegLobby`, `event:/sj21_bingovergoogle`, and
`event:/sj21_levelselect`, with owning bank/event GUIDs and source hashes in the
root artifact. The required StrawberryWithReturn uses the already available
vanilla `event:/game/general/cassette_bubblereturn`. No reachable audio call was
dropped.

Three independent complete generations produced the same 743-file closure:
shared closure `9ae24c882bf931c29ad3967377217290fbbb48cc44b0f27f741a5f19a76150e5`,
managed tree `e719bb72ad71d83d0ece6503d106f1ad69bba6123bc306434d41083191c9df99`,
content tree `60091f1045188c19198813d9a047730d9c3c48c3a7d941d37395e986a5557425`,
registry `c26570a4b9ae76541624c85c9f425ccc92fabcba11577600a35477548c245b66`,
API surface `d594dea86e944b468819818773f2177312445bbebe022bcbc7ea1ff779c8652c`,
and semantic patch plan `b737d6820d6b54aa42240e320528d8d80d76e7e75516a9601cce3ef8eb25e22c`.
Each of the four omission profiles compiled, removed only that helper's
factories and generated source, retained identical unrelated plans and maps,
and changed the closure hash.

The bounded canary uses real distributed semantics as authority and contains a
Contort spotlight transition, EVM BackgroundBrightness fade/reset, Jungle
MossyWall, YetAnother horizontal bubble field, Lunatic return strawberry, and
an SJ root lifecycle/SaveData/Session marker. The marker is gold after static
root initialization, cyan when prior durable state exists, and lime after the
player crosses it and the typed transition completes.

The macOS Everest reference loaded the exact 52-package set successfully. Its
OpenGL gameplay window was unavailable to the automation surface after startup,
so no visual parity claim is made for the selected room. The two real SJ maps
remain unlaunchable as accepted Apple products until their audio and
Crystalline/Vortex blockers close.

The product proof built iOS/iPadOS and tvOS from the same closure. The final
development-signed iOS artifact is Release `ios-arm64`, 886,090,130 bytes,
SHA-256 `66b111622c2010ab251a66c3e066f0dbe863ee50f42a183a97263c19c7b2d09f`,
universal device family `[1,2]`, iOS
15 minimum, native landscape iPad metadata, full trim/AOT,
`UseInterpreter=false`, and no JIT. tvOS is Release `tvos-arm64`, 900,042,864
bytes, SHA-256 `35509a126e6c7e7885c6873db6e7ba4f856c1103141883973a97c97c3a696bac`,
tvOS 16 minimum, full trim/AOT, `UseInterpreter=false`, and no JIT. Both
passed the forbidden-runtime, preserved-assembly, referenced-API, and native
AOT-object scans.

The first signed device launch exposed one packaging defect before the menu:
the projected root SpriteBank XML was correctly mounted under the static mod
namespace, but the typed root loader used its original desktop-relative path.
That build was rejected. The loader now uses the mounted path, the verifier
locks it, and the replacement signed build reached `DONE LOADING` and the
Overworld menu. That second rejected run then exposed a data-only canary error
at map entry: the authored backdrop key `bgs/01/bg` does not exist in the
vanilla gameplay atlas. The canary now uses the real `bgs/01/bg0` key, the
verifier locks that asset name, and all closure, product, and physical evidence
below comes from the regenerated final build.

The regenerated final IPA was installed on the available iPhone 12 Pro Max and
passed the full available-device checklist: launch, menu, the K-E semantic and
root-state canaries, movement, jump, dash, death/respawn, pause/resume, touch,
audio regressions, rotation, background/reopen, and cold relaunch. A controller
was unavailable and was therefore not exercised. The device runtime log records
the exact `AppleEverest/Stage25KE` map launch, PASS factory creation for the root
marker and every bounded helper feature, and the root marker's typed-session
transition to its mutated lime state. Physical observation additionally
confirmed the spotlight, BackgroundBrightness fade/reset, MossyWall, horizontal
bubble field, and return-berry behavior.

The current 513-test AppleEverestBuilder suite and 50-test typed HookGen suite
pass. They cover the retained K-B/K-A collab behavior, LittleEpic custom audio,
DJ frozen IL, H-D direct ILHook lowering, H-B composition, H-A freeze, F-B2
ModInterop, F-A durability, 25E, 25D, map appendices, and the new root/helper
semantics. The K-D verifier passes 46 checks and the input-profile verifier
passes 56. The unchanged K-C verifier has a historical `tvos-port` assertion
for its pre-K-D SHA; its substantive audit checks are run unchanged in an
isolated historical-ref view during the final clean-clone proof, without moving
the real K-D protected refs.

## Answers to the 95 closeout questions

1. Start SHA? `ea5bfd7de0185a676d549d7e4b9f483ee159f3a8`.
2. Final SHA? The immutable feature tip containing this report; its exact value is reported in the final handoff because a commit cannot contain its own SHA.
3. Branch? `feature/apple-everest-sj-root-helper-lowering`.
4. Exact Contort version/ZIP/DLL? ContortHelper 1.5.5; ZIP `d8b42128a808e68d30329baa9299fdb41bf7d24743635dec3137c36bcc87956a`; DLL `c3983e67e1b535fbb1e0f0a541e8c78ad8f4cd150f66636c783a83dc5ffb4488`.
5. Exact Contort Beginner features? Three `ContortHelper/MadelineSpotlightModifierTrigger` occurrences in the lobby, with authored color, alpha, light radii, CubeInOut timing, and entry behavior; Bing uses none.
6. Were they all lowered? Yes, to one typed trigger factory and exact Player.Light tween behavior.
7. Any general DynamicData? No.
8. Exact EVM version/ZIP/DLL? ExtendedVariantMode 0.50.5; ZIP `4019b362d9ad1b2d3a6a670f833ef6324d735d5791a0bf21b62cf5cfbc0d639d`; DLL `cb28846f7f7348ddb996f63498c97694616436e97ef34f67880b697ba024fe81`.
9. Which EVM behavior does Beginner require? Typed per-session `BackgroundBrightness`, three float fade triggers, one reset trigger, interpolation by the authored position mode, and a fixed render-time brightness branch.
10. Which nine K-D dynamic sites remain rejected? `ExtendedVariantsModule.hookStuffRightNow`, `ExtendedVariantsModule.initializeStuff`, `VanillaVariantOptions.Load`, and `Load` for DisableWallJumping, Gravity, InvertHorizontalControls, InvertVerticalControls, NoFreezeFrames, and Stamina.
11. How was the required observable EVM behavior reproduced without live hooks? A statically registered pair of trigger factories writes typed per-session brightness state; a fixed compiled Level background branch reads it. No hook is installed or removed at runtime.
12. Any runtime DetourContext? No.
13. Exact Jungle version/ZIP/DLL? JungleHelper 1.4.10; ZIP `a140e21cbb5fd2dcaac70d4d5e36d49476e414861455ae25dedc0678164406cc`; DLL `fed840ade7250f05e38b70a81bcfe751ca87ff63274f4b568172868cacf56f8a`.
14. Required Jungle features? The lobby's 209 `JungleHelper/MossyWall` entities: exact hitbox, static-mover attachment, left/right orientation, and authored moss images.
15. Exact YetAnother version/ZIP/DLL? YetAnotherHelper 1.2.5; ZIP `73d64e1b3457e2461d3368a01de9f5f31a58bf24e130f3e35ec7280b24814c2d`; DLL `f48e16a568edf43913beac0684bd26b7dc12d45be4011a8b214e05db86b78aea`.
16. Required YetAnother features? Four horizontal, Always-mode `YetAnotherHelper/BubbleField` entities with strength 1.5, left/right direction, lift-off-ground, flag/wind behavior, and bounded particles.
17. Source required for production? No. The pinned distributed DLLs are production authority; source was audit/reference evidence only.
18. Does omission of each helper cleanly remove its generated semantics? Yes. All four omission controls compile, remove the relevant factories/source, keep unrelated plans/maps identical, and produce distinct closure hashes.
19. Exact SJ root DLL still 8d5b918...? Yes: StrawberryJam2021 1.0.12 root DLL `8d5b9184204e7e7728965bcf95af5f150f6220dafbfe52fdd6c23e50d47e5258` from ZIP `4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655`.
20. Root lifecycle surfaces required? Constructor, `Load`, `Initialize`, `LoadContent`, and `Unload`, lowered as generated identity, typed state construction, static registration, initialization, and collab/lobby use.
21. Root Settings fields/defaults? `DisplayDashSequence:Boolean=false`; `TogglePlaybacks:ButtonBinding` defaults to controller Back and keyboard Tab.
22. Root SaveData fields? Ordered `HashSet<String>` values `ModifiedThemeMaps` and `FilledJamJarSIDs`, both default empty.
23. Root Session fields? Music/Cassette wonky beat indexes and timers; cassette disabled/last parameter; Oshiro B-side, skateboard, and zero-G flags; expiring-dash time/threshold; RainDensity Density/Start/End/Duration; plus an ephemeral live `DashSequenceDisplay` reference that is absent from the exact maps and is not serialized.
24. Which root fields are durable? `ModifiedThemeMaps` and `FilledJamJarSIDs` in the typed module SaveData aggregate; settings retain their typed values through the existing settings path.
25. Which are session-only? All fields listed in answer 23, including RainDensity; the live DashSequenceDisplay reference remains ephemeral and unserialized.
26. Did Stage 25F typed module state suffice? Yes, for both the per-module SaveData A/B aggregate and typed ModuleSession restoration.
27. Any new persistence framework? No.
28. AEVPSV1 changed? No; it remains schema version 1.
29. Existing progression still compatible? Yes. LittleEpic, Fear of the Dark, both Torremolinos maps, the K-A collab, and the K-B collab retain separate compatible state.
30. Beginner lobby static manifest hash? `fdd710714ca6280790d9bd70fbe22e0776b9a8b1abdc2c9bbca45e8eabd23092`.
31. Bing manifest hash? `9118e61cff9edb6a34e2eba9868e4c64a1721e095b8222d9f996c60ef1c352b4`.
32. Return-to-Lobby semantics represented? Yes. Existing CollabUtils2 `SetReturnToHere` panels remain authoritative; the root-specific jam-jar bounds/node data feeds that primitive.
33. Journal semantics represented? Yes. The existing static Collab journal primitive receives the exact Beginner row and completion state.
34. Root completion semantics represented? Yes. Vanilla `AreaStats.Modes[0].Completed` remains authority, with typed jam-jar fill and panel state around it.
35. Any special berry semantics newly required? Yes, one Bing `LunaticHelper/StrawberryWithReturn`: collect, wait 0.3 seconds, vanilla cassette-fly to the current respawn, and ignore squish in Player state 21.
36. DynamicData calls before/after classification? The five DLLs contain 83 distributed calls; 12 are slice-reachable before lowering; zero remain in the accepted runtime.
37. Any arbitrary runtime attachment remaining in accepted slice? No.
38. Reflection calls lowered? Yes. Of 393 distributed reflection calls, 35 are slice-reachable; all accepted discovery/access behavior is generated or typed and zero runtime module/helper discovery remains.
39. Runtime module/helper scanning? No.
40. New HookGen descriptors? Zero.
41. New API members? Zero public members; four exact internal source accesses are compiled directly.
42. Starting/final catalog counts? HookGen catalog 205/205 and reviewed Apple API members 30/30.
43. Broad publicizer? No.
44. Configured-order regression passed? Yes: K-D passes 46 checks and the Lunatic aggregate plan remains `bfbae569352041d00d98c5792354bc403423566ad796a69912431392111a9895`.
45. K-D Lunatic fixture passed? Yes.
46. Map appendix regression passed? Yes: 124 maps, 19,240,291 total appendix bytes, 1,260,932 maximum, 2,097,152 bound, unchanged complete hashes and progression identities.
47. Beginner blocker groups before? Four at the K-D boundary.
48. After? Two.
49. Exact blockers remaining? Bounded multi-bank FMOD; CrystallineHelper/VortexHelper exact bounded mechanisms.
50. Multi-bank FMOD still blocked? Yes; four exact banks/events are handed to K-F and no bank was added in K-E.
51. Crystalline/Vortex still blocked? Yes: two AttachedJumpThrus, nine bloom-strength triggers, 43 edit-depth triggers, and one trigger-trigger.
52. Any other blocker? No known or unclassified blocker for the selected lobby+Bing slice.
53. Zero unclassified? Yes, across all 6,098 audited methods and the complete selected content occurrence census.
54. Slice built? No.
55. If not, confirm intentionally not built. Confirmed: the playable Beginner slice was intentionally deferred until the two K-F groups close.
56. Shared canary closure SHA? `9ae24c882bf931c29ad3967377217290fbbb48cc44b0f27f741a5f19a76150e5`.
57. Three runs identical? Yes, complete 743-file trees and every named manifest/schema/plan/hash are identical.
58. iPhone final build physical PASS? PASS. The exact final development-signed IPA, SHA-256 `66b111622c2010ab251a66c3e066f0dbe863ee50f42a183a97263c19c7b2d09f` from shared closure `9ae24c882bf931c29ad3967377217290fbbb48cc44b0f27f741a5f19a76150e5`, passed the available iPhone 12 Pro Max checklist. Controller input was unavailable and was not exercised.
59. Exact K-E-specific physical behavior exercised? The gold/cyan root marker turned lime after collision and its typed-session mutation was recorded; spotlight transition, BackgroundBrightness fade/reset, MossyWall collision/rendering, horizontal Always bubble-field behavior, and StrawberryWithReturn collection/return were also physically exercised.
60. iPad physical? No physical pass is inferred.
61. Exact pending marker if unavailable? `IPADOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE`.
62. Apple TV physical? No physical pass is inferred.
63. Exact pending marker if unavailable? `TVOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE`.
64. Last all-three physical GREEN? Stage 25K-B build 35, on lineage rooted at `d17f84796eaca56b2cd427f444492bb7a5bc2c0d`.
65. iOS/iPad package universal? Yes, `UIDeviceFamily=[1,2]`, arm64, iOS 15 minimum, and the shared closure hash above.
66. Native iPad presentation metadata? Yes: iPad landscape-left/right orientations and full-screen native presentation are present.
67. tvOS Release build? Yes, `tvos-arm64`, tvOS 16 minimum.
68. Full trim? Yes on both products.
69. Full AOT? Yes on both products, including the expected LLVM and companion Mono AOT objects.
70. UseInterpreter=false? Yes on both products.
71. JIT absent? Yes.
72. Forbidden runtime surfaces absent? Yes: product scans reject Mono.Cecil, MonoMod.Cil, RuntimeDetour/ILHook backends, DynamicMethod, Reflection.Emit, Assembly.Load, arbitrary discovery/DynamicData, Lua, Process, FileSystemWatcher, and native mod loading.
73. K-B regression? PASS in the current 513-test suite, including collab and large-static-content contracts.
74. K-A regression? PASS in the current suite, including CollabUtils2 lobby, panels, journal, and Return-to-Lobby contracts.
75. LittleEpic audio? PASS; the bounded Chrono/custom-FMOD path and locks are retained.
76. DJ frozen IL? PASS; the five-transform DJ plan and content canary are retained.
77. H-D? PASS; immutable direct-ILHook planning, composition evidence, and rejection boundaries are retained.
78. H-B/H-A? PASS; ordered composition and the original frozen-IL contracts are retained.
79. F-B2? PASS; static ModInterop reference/binding behavior is retained.
80. F-A? PASS; typed module durability, corruption fallback, and separation from AEVPSV1 are retained.
81. E/D? PASS; helper factory/runtime and managed-detour contracts are retained.
82. Canonical/native locks? Unchanged: Content `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46`, raw `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273`, patched `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5`, Stage 6 `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9`, iOS native `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`, tvOS native `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`, vanilla iOS `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357`.
83. Protected refs? Unchanged: `ios-v0.1.1-rc.1^{}` `27e16b4724d94d3991b99c4795f680fcb0e5830c`; `v1.0.0-rc.1^{}` `ee52b0868df091746f134d95d4f020f94f23d4fb`; `v1.0.0-rc.2^{}` `641e86e4ed164cdf93f602ce2f11436449654d6e`; `origin/release/v1.0.0-rc.3` `c8134c8ca7924cf12f48527e714b5242c6024927`; `v1.0.0-rc.3` remains absent.
84. Actions zero? Yes; no workflow file changed and no hosted Actions run was invoked.
85. Clean clone reproduced? PASS. A fresh recursive clone at the exact final feature SHA independently reacquired the required public K-C/K-D releases, reproduced all tracked K-E evidence and three closure runs, passed the full verifier, had empty `git status --short` and clean recursive submodules, and was deleted afterward.
86. Privacy? PASS: no ZIP, DLL, map BIN, bank, IPA, save, signing material, device ID, private path, or proprietary Celeste/FMOD bytes are tracked by K-E.
87. Disk before/after? Approximately 43 GiB free before K-E and 26 GiB free after removing the disposable public-package fixtures, generated closure trees, decompilation output, product build roots, and private device logs while retaining the final iOS and tvOS IPAs.
88. Development integration-ready? Yes. The final iPhone, universal iOS/iPad static/package, tvOS Release/full-AOT/static, deterministic clean-clone, privacy, and zero-Actions gates pass; the unavailable iPad and Apple TV physical gates retain their exact pending markers.
89. All-platform release-ready? No; iPad and Apple TV physical acceptance remain pending, and K-E is not an RC/release stage.
90. Exact fast-forward SHA? The immutable K-E feature tip reported in the final handoff; no merge, tag, or push is performed here.
91. Can audio + Crystalline/Vortex fit one K-F stage? Yes if the audited four-bank and 55-occurrence surfaces remain bounded; split F1/F2 if either audit expands.
92. How many blocker groups remain before first SJ slice? Two.
93. How many bounded stages remain before first slice? One implementation stage, K-F, followed by K-G for the first build/physical acceptance.
94. Is first SJ slice now close enough to schedule immediately after K-F? Yes, provided K-F closes both groups without broadening the runtime boundary.
95. Is full Strawberry Jam build rational yet? No. K-E supports only the bounded Beginner root/helper surface and makes no complete-package or full-128-map claim.
