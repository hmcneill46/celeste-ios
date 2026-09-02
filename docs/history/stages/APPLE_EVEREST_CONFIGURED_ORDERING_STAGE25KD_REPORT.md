# Apple Everest Stage 25K-D — configured ordering and bounded map appendices

Status: **IN PROGRESS — automated and clean-clone gates pass; reachable-iPhone physical exercise pending**

Stage 25K-D closes three prerequisites identified by the Strawberry Jam audit without building or claiming support for Strawberry Jam: bounded one-root map appendices, immutable configured detour ordering, and the exact managed-hook/API breadth reached by the current Beginner slice. Dynamic configuration, Strawberry Jam root behavior, multi-bank audio, and the remaining helper mechanisms stay fail-closed.

## Evidence and implementation

The authoritative machine-readable records are the [configured-site census and ordering proof](../../../apple-everest/configured-ordering-stage25kd.json), [Beginner descriptor/API breadth and payoff recount](../../../apple-everest/sj-beginner-breadth-stage25kd.json), and [128-map appendix census](../../../apple-everest/map-appendix-stage25kd.json). The production policy is enforced by the [Stage D verifier](../../../scripts/verify-apple-everest-stage25kd.py), the [managed-detour catalog](../../../apple-everest/managed-detour-targets-v2.json), and the [configured-order resolver](../../../tools/AppleEverestBuilder/ConfiguredDetourOrdering.cs).

Map ingestion now parses exactly one valid BinaryPacker root, records the consumed boundary, and permits at most 2 MiB of opaque trailing bytes. It neither reparses the appendix nor edits the input map. All compatibility and progression identities remain SHA-256 hashes of the complete original file. The exact Strawberry Jam 2021 1.0.12 census is 128 maps, of which 124 have 19,240,291 total appendix bytes; the largest observed appendix is 1,260,932 bytes.

Configured ordering is deliberately restricted to `STATIC_CONFIGURED_DETOUR_SEQUENCE`: exact target, exact hook/manipulator, immutable host-known configuration, and immutable-active lifetime. Repository-pinned MonoMod is the behavioral authority. The host resolves a target-local graph, hashes the resulting plan, composes IL sequentially in the pinned order, and emits only typed ordinals/chains or already-frozen bodies. Cycles, incompatible duplicate identities, drift, dynamic targets/manipulators/configuration, and gameplay-scoped lifetimes reject before AOT. The Apple runtime receives no detour backend or serialized graph.

All 39 audited construction sites in 13 helpers are fully classified: 30 are statically eligible and nine ExtendedVariantMode sites retain dynamic lifetime and reject. The real production fixture is ordinary LunaticHelper 1.1.1. Its distributed DLL is the authority; its legacy `After("*")` Player-constructor HookGen registration is normalized by the pinned rules and emitted as a static typed registration. Three independent closures reproduce the same configured plan, output file set, and shared closure hash.

A fresh recursive clone independently reacquired the 52 exact public Strawberry Jam dependency archives, then reproduced all 128 map boundaries, all 39 configured construction methods, the 67/38 configuration-reference census, the 176 resolved Beginner descriptor requirements across eight providers, and three byte-identical LunaticHelper closures. The mutable live Everest updater entry for FrostHelper has drifted since K-C, so clean reproduction bypassed only that live-record comparison; every authoritative archived release URL and SHA-256 check remained enforced. The clean clone then passed the full 496/50/46 test boundary. No ignored fixture was copied into it.

The configured canary additionally preserves only the two exact public desktop-Everest ABI members reached by that pinned fixture: `BinaryPacker.Element.AttrInt` with upstream conversion semantics and the legacy two-argument `Input.Rumble` overload forwarding to the existing Apple-aware implementation. The linked-product contract scanner verifies these references; they are separate from, and do not alter, the Beginner-slice API-breadth count below.

The native-body verifier consumes both Mono and LLVM object files and matches Mono's exact Mach-O `_Assembly__Type_Method` symbol form. The final unsigned iOS and tvOS products both pass two runtime scans plus preservation, referenced-API, and native-body scans with no allowlist added for missing AOT code. Their manifests record the same target-neutral closure hash: iOS is 882,674,369 bytes with SHA-256 `1cf993f94c22fc954757c78dcc6fb65292b48ecb8e889b26fa41c51852e22200`; tvOS is 896,592,813 bytes with SHA-256 `426ed621dd35389a134296c2760a2134108062049ea37eb3cbe07a84082d154a`.

The Beginner reachability audit adds 103 declaration- and overload-locked descriptors—98 from canonical source and five pinned Apple API targets—and 29 exact reviewed API members across BrokemiaHelper, CherryHelper, FemtoHelper, FlaglinesAndSuch, FrostHelper, HonlyHelper, PandorasBox, and VivHelper. It uses no broad publicizer. The Beginner blocker count falls from seven to four, but the slice remains intentionally unbuilt.

## Verification boundary

The current 496-test builder suite, 50-test typed HookGen suite, 46-check Stage D verifier, pinned MonoMod runner, focused historical regressions, full-trim/full-AOT canary, privacy/reference locks, and clean-clone reproduction are the acceptance boundary. The canary uses one shared target-neutral LunaticHelper closure for iOS/iPadOS/tvOS with `UseInterpreter=false`; product scans reject RuntimeDetour, a live ILHook backend, Mono.Cecil/MonoMod.Cil, `DynamicMethod`, `Reflection.Emit`, a host worker, runtime loading, or a serialized configuration graph.

Hardware availability is reported exactly: no unavailable device receives an inferred pass. Strawberry Jam remains unsupported.

## Answers to the 92 closeout questions

1. Exact starting SHA? `d6bdaa4cf6d4c8ccf451d6ac1bfb66cde5d0dc10`.
2. Final SHA? The immutable feature tip containing this report; its exact value is reported in the final handoff because a commit cannot contain its own SHA.
3. Branch? `feature/apple-everest-configured-ordering`.
4. Map appendix maps accepted? 124 of the 128 exact Strawberry Jam maps; the other four have no appendix and remain semantically unchanged.
5. Exact appendix bytes? 19,240,291 total; maximum observed 1,260,932; the accepted hard bound is 2,097,152 bytes per map.
6. How is root-vs-appendix boundary detected? The existing bounded BinaryPacker reader consumes exactly one valid root; its stream position is the root length and the remaining bytes are the opaque appendix.
7. Can malformed root data slip through? No. Header, string-table, structure, truncation, archive/path, and bound failures still reject; appendix acceptance starts only after a complete valid root.
8. Does original map hash remain unchanged? Yes, it remains SHA-256 over the complete unedited source file.
9. Progression identity unchanged? Yes, it remains SID plus the complete original-map SHA-256.
10. All 39 configured sites revalidated? Yes, with no `UNKNOWN` fields.
11. Number statically eligible? 30 construction sites.
12. Number dynamic-lifetime? Nine, all in ExtendedVariantMode and all rejected.
13. Number dynamic-target? Zero in the exact 39-site census.
14. Number dynamic-manipulator? Zero in the exact census.
15. Configured IL sites? 13 exact configured IL sites; direct evidence also records eight HookGen IL and seven direct ILHook construction sites where site categories overlap.
16. Configured managed Hook sites? Four direct managed-Hook sites.
17. Configured On.* sites? 22 HookGen `On.*` sites.
18. Helpers affected? 13: BrokemiaHelper, CollabUtils2, CommunalHelper, CrystallineHelper, ExtendedVariantMode, FemtoHelper, GravityHelper, JungleHelper, LunaticHelper, MaxHelpingHand, StrawberryJam2021, VivHelper, and XaphanHelper.
19. Exact priority semantics? Pinned DepGraph higher priority is outer and first in the managed invocation chain; configured execution, managed wrapping, and IL composition are recorded separately.
20. Before semantics? `Before(X)` creates an edge from the configured node to config ID `X`.
21. After semantics? `After(X)` creates an edge from config ID `X` to the configured node.
22. Wildcard semantics? The legacy adapter swaps legacy Before/After into reorganized config; legacy `Before("*")` normalizes to `int.MinValue`, legacy `After("*")` to `int.MaxValue`, with the pinned IL reversal applied separately.
23. BeforeAll semantics? The pinned legacy normalization is `int.MinValue` priority, with exact subpriority/registration behavior preserved.
24. AfterAll semantics? The pinned legacy normalization is `int.MaxValue` priority, with exact subpriority/registration behavior preserved.
25. How are ties resolved? Pinned subpriority then global registration index; later equal registration is the outer managed wrapper.
26. How are cycles handled? They fail plan construction with `STATIC_CONFIG_ORDER_CYCLE`; there is no fallback.
27. Real selected helper? LunaticHelper.
28. Version? 1.1.1.
29. ZIP SHA? `b10e044b1dfa412605bee3ba6bfdd4263591099d331d6c1aab1e9ba65bf6a3e5`.
30. DLL SHA? `fc08f00296551a6025c5e31422c6edd5e8136e6861459f44ffea909129c6a925`.
31. Source required? No. The ordinary distributed DLL is production authority; source is audit-only (`e7cef501937fc1bc07d1ff13e753fe920b4ccbbd4e4db4c0b2c4312de89fdd78` logical hash).
32. Exact target? `System.Void Celeste.Player::.ctor(Microsoft.Xna.Framework.Vector2,Celeste.PlayerSpriteMode)`.
33. Exact config? Legacy DetourContext ID `LunaticHelper` with `After=["*"]`, normalized to ID `LunaticHelper`, priority `int.MaxValue`, subpriority `int.MinValue`, empty explicit Before/After arrays.
34. Exact manipulator/hook? HookGen `On.Celeste.Player.ctor`, handler `LunaticHelper.BubbleReturnBerry::onPlayerConstructor`, installed by `BubbleReturnBerry::Load()`.
35. Lifetime? Module/process immutable-active; no gameplay-time Apply, Undo, Dispose, or mutation.
36. Baseline target hash? Canonical `2079f53ceba4e9143c75e98b138d710a5e4b95271bfe5ae10ab94f7db5980b4c`; desktop-patched baseline `78ae30f07c73e7a47cb05471ce5b871edc15d0933c4c5450d851cab3d0e28ec8`.
37. Final target hash? `19e211c4b655b88ad4f1130982d109671452c96fa6cee4bf88dea06431b4c885`.
38. Semantic diff hash? `576c6e57bd149341343abb9e23712d4fb026b002376775d42e4d4138be142214`.
39. Plan hash? Selected fixture `016bb7899f8622318936ef96933c69486fb183e97f35234bae481b630f77a384`; aggregate configured closure `bfbae569352041d00d98c5792354bc403423566ad796a69912431392111a9895`.
40. Three-run deterministic? Yes: three independent closures have identical plans, typed chains, frozen output, descriptor/API catalogs, and product file set.
41. Desktop reference match? Yes, against real pinned MonoMod commit `dfc30a1506d37fb88a2c2be004f525205f46a24c` / runtime 25.3.1.0.
42. Same-target composition proven? Yes; two configured non-commutative participants yield 3 for A→B and 2 for B→A and Apple matches the reference.
43. Configured+unconfigured composition? Yes, including ordinary default participants and registration/dependency tie-breaks.
44. Configured IL + On wrapper? Yes. Frozen underlying IL order and typed managed wrapper order are distinct and composed; no claim is made for arbitrary unaccepted targets.
45. Runtime RuntimeDetour present? No.
46. Runtime ILHook backend present? No.
47. Runtime config graph present? No.
48. New HookGen descriptor count? 103: 98 canonical-source-backed plus five pinned patched Apple API targets.
49. New exact API-member count? 29, from 326 reachable reference sites, with zero missing or nonpublic after review.
50. Which Beginner providers expanded? BrokemiaHelper, CherryHelper, FemtoHelper, FlaglinesAndSuch, FrostHelper, HonlyHelper, PandorasBox, and VivHelper.
51. Broad publicizer used? No.
52. Beginner blocker groups before? Seven.
53. After? Four.
54. Which are closed? Bounded map appendix; static-eligible configured ordering; exact evidenced Beginner descriptor/API breadth.
55. Which remain? Contort/EVM/Jungle/YetAnother semantic lowerings; StrawberryJam2021 root lifecycle/UI/state lowering; bounded multi-bank FMOD; Crystalline/Vortex bounded mechanisms.
56. Maps still blocked by configured ordering? All 128 remain graph-wide blocked because ExtendedVariantMode’s dynamic-lifetime configuration is still required.
57. Maps unlocked from configured ordering? Zero; the stage closes 30 static sites but honestly does not erase EVM’s nine dynamic sites.
58. Package coverage after? 17/52 = 32.6923%; slice-bounded evidence does not silently upgrade whole packages.
59. Content-ID coverage after? 43,350/274,073 = 15.817%.
60. Helper-mechanism coverage after? 3,818/11,009 = 34.6807%.
61. Zero-blocker SJ maps after? 0/128.
62. iPhone physical? PENDING functional exercise: the final development-signed configured canary installed and launched successfully on the reachable iPhone 12 Pro Max, but the phone remained unlocked/in use and iPhone Mirroring could not reconnect for the checklist. No functional pass is inferred.
63. iPadOS 15.8.8 physical? PENDING exactly: the paired iPad mini 4 was booted but its developer tunnel was unavailable during Stage D closeout; no pass is inferred.
64. Apple TV physical? PENDING exactly: the paired Apple TV 4K 3rd generation was disconnected during Stage D closeout; no pass is inferred.
65. Shared closure hash? `fe9e0a4b7a19fa4a9ea4d607aa2424617d8567636c97248f39dfed19961ccdf6`; all three independent runs also have the product file-set hash `394fe8ffc1fc3ec3dd2b94429c99af405caa9b0c0eacf370b12ee694103ef190`.
66. Full trim? Yes, on the iOS/iPadOS and tvOS canary products.
67. Full AOT? Yes, including product scans and required native AOT objects.
68. UseInterpreter=false? Yes.
69. JIT absent? Yes; product boundary scans pass and no runtime assembly loader or dynamic-code backend is present.
70. K-B collab regression? PASS.
71. K-A regression? PASS.
72. Custom FMOD regression? PASS (LittleEpic/Chrono bounded custom audio).
73. DJ frozen-IL regression? PASS.
74. H-D regression? PASS (immutable direct ILHook).
75. H-B regression? PASS (ordered composed IL).
76. ModInterop regression? PASS (F-B2 static reference and binding behavior).
77. Module durability regression? PASS.
78. Canonical/native locks unchanged? Yes: Content `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46`, raw `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273`, patched `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5`, Stage 6 `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9`, iOS native `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`, tvOS native `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`, vanilla iOS generated `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357`.
79. Protected refs untouched? Yes; no merge, tag, force-push, upstream push, or protected-ref update occurred.
80. Actions zero? Yes; no workflow file changed and no hosted Actions run was invoked.
81. Clean clone reproduced? Yes. A fresh recursive clone independently reacquired the exact public releases; reproduced 128 map boundaries, 39 configured sites, the selected real fixture, 176 descriptor requirements, and three byte-identical closures; and passed the full 496/50/46 verifier boundary. The exact final-tip clean check is repeated after the closeout commit.
82. Privacy pass? Yes: no third-party ZIP/DLL/map/bank/game bytes, Apple product, save, signing material, device identifier, or private absolute path is tracked.
83. Disk before/after? Approximately 47 GiB free at Stage D start and 27 GiB while the disposable clean-clone reproduction remains present; the final post-cleanup value is reported in the handoff.
84. Integration-ready? The bounded architecture and all automated gates are ready; final integration status remains pending only the reachable-iPhone functional checklist. Strawberry Jam itself is not ready or supported.
85. Exact fast-forward SHA? The same immutable final feature-tip SHA reported in the handoff; it descends directly from `d6bdaa4cf6d4c8ccf451d6ac1bfb66cde5d0dc10`.
86. Is Beginner slice now ready to build? No; Stage D deliberately does not package or run it.
87. If no, exact remaining blockers? The four groups in answer 55: four helper semantic lowerings, SJ root lifecycle/UI/state, bounded multi-bank FMOD, and Crystalline/Vortex mechanisms.
88. Recommended K-E scope? One bounded root/map/helper semantic-lowering stage: close Contort, EVM, Jungle, YetAnother and the exact SJ Beginner root/lobby/state surface without adding a general runtime architecture.
89. Is multi-bank FMOD next? No.
90. Or are helper/root semantic lowerings now higher value? Yes; they remove the largest exact remaining Beginner control/lifecycle blocker before audio integration.
91. How many bounded stages remain before first SJ slice? Approximately three: helper/root lowering, bounded audio/mechanisms, then slice integration and physical validation.
92. Is a full Strawberry Jam build rational yet? No.

## Final boundary

Stage 25K-D has completed its implementation, deterministic-product, regression, privacy, and clean-clone boundaries. Final GREEN/integration-ready status awaits only the functional checklist on the already-installed reachable-iPhone canary. It does not merge, tag, push, run Actions, build the Beginner slice, or claim Strawberry Jam support.
