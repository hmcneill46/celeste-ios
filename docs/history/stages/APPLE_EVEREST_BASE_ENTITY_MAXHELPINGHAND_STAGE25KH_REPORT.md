# Stage 25K-H — Everest base-entity closure and selected MaxHelpingHand semantics

Stage 25K-H closes the constructor and observable runtime dependencies which
correctly stopped the first unchanged Strawberry Jam Beginner attempt. It adds
a general recursive selected-factory closure gate, audits all 73 factories in
the exact lobby/Bing selection, and implements two guarded typed entities for
the exact selected MaxHelpingHand 1.40.9 profiles. The result is
`READY_FOR_K_I_REAL_SJ_INTEGRATION_RETRY`; this stage does not package either
real Strawberry Jam map and does not claim general MaxHelpingHand or full
Strawberry Jam support.

The branch starts at accepted K-F commit
`e38a886f54a719ad973f21f4ef84587d52b49718`. K-G commit
`e0d7c1988a9e5a896001735e5fe21d2251446bd0` was read as immutable diagnostic
evidence and was not merged or cherry-picked. The K-H branch is
`feature/apple-everest-everest-base-entities`; its exact final commit is the
commit containing this report and is recorded in the stage handoff.

## Factory closure and historical negative

The builder now accepts a selected-factory closure manifest before generating
a product. It recursively follows evidence nodes, rejects missing and cyclic
edges, requires an accepted required node for all 17 closure dimensions, and
cross-checks each selected ID against the resolved factory registry. The
dimensions cover package/assembly/type, base and constructor chains,
construction fields and type initializers, lifecycle and interaction,
coroutines, module load, hooks, reflection, and content. The implementation is
provider- and map-neutral.

The original K-F model still reproduces 920 selected occurrences, all 920
accepted or vanilla, zero blocked, and zero unclassified. The stronger model
applied to the same pre-fix selection reports 73 factories, 71 closed, two
blocked, and zero unknown: `CustomTutorialWithNoBird` lacked its
`CustomBirdTutorial` closure and `MoreCustomNPC` lacked its `CustomNPC`
closure. After lowering, the separate type census is 73 selected, 73 closed,
zero blocked, and zero unknown, across 191 base edges, 73 constructors, and 73
module-load dispositions. Ten synthetic cases lock the fail-closed behavior,
including both direct and transitive missing bases.

The machine-readable evidence is the [selected-factory graph](../../../apple-everest/selected-factory-type-closure-stage25kh.json),
[Everest base semantics](../../../apple-everest/everest-base-entity-semantics-stage25kh.json),
[selected MaxHelpingHand semantics](../../../apple-everest/maxhelpinghand-selected-semantics-stage25kh.json),
and [pre-integration readiness](../../../apple-everest/sj-preintegration-readiness-stage25kh.json).

## Exact source and binary authority

MaxHelpingHand is the independently reacquired public 1.40.9 release. Its ZIP
SHA-256 is
`abfc5d167936410e9b6665018335ee654455e30710caffe01540b232da468fee`
and distributed DLL SHA-256 is
`6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6`.
The DLL is production authority. The audited Everest source is tag
`stable-1.6458.0`, commit
`4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00`. The exact source hashes are
`e353517fb2271caca39b364df86203b689015b530823f6a16b0194f1cd339e67`
for `CustomBirdTutorial.cs` and
`d89d1ad0d2e71c171746a8664c50bb54e41a4af3073afe267217f54d166ffa1d`
for `CustomNPC.cs`.

The complete Max DLL census contains 447 types and 2,178 methods. Its mechanism
counts are 76 `On.*` subscriptions, 82 `IL.*` subscriptions, 21 direct Hook
constructors, 34 direct ILHook constructors, 407 reflection calls, 30
`DetourConfig` references, 11 `DetourContext` references, 105 `EmitDelegate`
calls, five ModInterop calls, and two FMOD calls. Assembly load, DynamicData,
DynData, DynamicMethod, Reflection.Emit, FastReflection, Everest.Content,
FileSystemWatcher, and process calls are zero in the distributed census. For
this linked semantic profile, the two relevant On/IL/direct-ILHook/reflection
results are lowered; every other global registration is
`PRESENT_BUT_UNREACHABLE` because the DLL/module is omitted. Selected Settings,
SaveData, and module Session reads are zero. Optional integrations and direct
hooks are unreachable. There are zero unknown load sites.

## CustomTutorialWithNoBird

The exact authored attributes are `controls=dialog:SJ2021_lobby_gym_tutorial_controls`,
`onlyOnce=false`, `id=893`, `x=1128`, `y=787`, `direction=Right`, `birdId=0`,
and `info=SJ2021_lobby_gym_tutorial_info`, with no nodes. Absent attributes
resolve to `hasPointer=true`, `caw=false`, and `faceLeft=false`.

Pinned Everest `CustomBirdTutorial` derives through `BirdNPC`, `Actor`, and
`Entity`. It parses the info/dialog and control sequence, creates the tutorial
GUI, retains the bird ID and only-once/caw/facing state, and shows immediately
in `Awake` when no matching show trigger exists. `BirdNPC` supplies the light,
bubble open/close coroutines, and fly-away path. In this exact profile the bird
mode is `None`, no trigger owns activation, no distance/state coroutine runs,
and no sprite, caw, facing animation, only-once removal, or fly-away effect is
observable.

Max removes the bird Sprite and stores a pointer direction. Its
`On.Celeste.BirdNPC.StartleAndFlyAway` hook returns without `orig` only for this
derived type. Its `IL.Celeste.BirdTutorialGui.Render` manipulator reads five
private GUI fields and, for `Right`, draws 37 scaled one-pixel vertical slices
from the bubble's right edge, with inner background slices when size exceeds
12; it returns `-1` to suppress the vanilla bottom pointer. Its injected
coordinate recomputation is intentionally unmirrored.

The Apple implementation is a flattened typed entity plus a typed directional
tutorial GUI. It keeps the exact light, immediate activation, string dialog
control, scale/open lifecycle, Right-pointer loop and unmirrored injected
coordinate rule. The hide path directly represents the hook result: no
startle, sound, flight, flag, or removal. Constructor guards reject every
other profile. This shape carries the observable base behavior without
shipping an otherwise unused desktop Everest hierarchy or installing a hook.

## MoreCustomNPC

The exact authored attributes are `setFlag=""`, `onlyIfFlag=""`,
`indicatorOffsetX=0`, `x=3168`, `y=1904`,
`dialogId=StrawberryJam2021_0_Lobbies_1_Beginner_Credits`, `flipY=false`,
`id=1604`, `flipX=false`, `onlyOnce=false`, `endLevel=false`,
`indicatorOffsetY=-40`, `approachWhenTalking=false`, `frames=""`,
`spriteRate=1`, `approachDistance=16`, and `sprite=""`, with two authored
nodes. Absent Max attributes resolve to empty `spriteName`/`customFont`,
`autoSkipEnabled=false`, and both inversion flags false.

Pinned Everest `CustomNPC` derives through `NPC` and `Entity`. The selected
path creates no textures or sprite, installs a TalkComponent, enters the dummy
player state and cutscene, runs one `Textbox.Say`, restores normal state,
increments then resets the dialog counter, and remains repeatable. Max changes
the TalkComponent bounds to the rectangle defined by its two nodes.

The Max type initializer reflects private `CustomNPC.textures : List<MTexture>`
and `CustomNPC.scale : Vector2` for read/write access. Both selected uses are
unreachable because `spriteName` and `frames` are empty. Its module-lifetime
direct ILHook targets the `CustomNPC.Talk` iterator `MoveNext`; it adds
auto-skip triggers and a custom-font wrapper only when their authored branches
are active. Both are false here, so the selected `Textbox.Say` arguments and
coroutine are unchanged.

The flattened typed Apple NPC owns the exact two-node TalkComponent, no-sprite
render result, one-dialog cutscene, dummy/normal state transition, and
repeatability. It contains no reflected members or ILHook. Constructor guards
reject nonempty sprite/frames/spriteName, multiple dialogs, flags, only-once,
end-level, approach, auto-skip, custom-font, flip, and other profiles.

The first device build exposed a separate presentation defect during comparison
with the pinned macOS reference: the static fragment loader copied the authored
`[MADELINE left normal]` token and trailing `&#x20;` as printable text. Consequently,
`FancyText` never received a portrait node, so the iOS/tvOS dialogue used centered
text in its plain fallback box instead of Madeline's animated portrait and the
ornate purple/brown character textbox. That product was rejected and superseded.
The loader now performs the bounded `Language.FromTxt` transformations needed by
static fragments, including portrait commands, cleaned text, multiline breaks,
escaped hashes, and bounded insert resolution. The exact canary line becomes
`{portrait MADELINE left normal}STAGE 25K-H NPC TALK PASS`, while its cleaned
form is `STAGE 25K-H NPC TALK PASS`. Parser regression coverage raises the current
builder suite to 532 checks.

## Product boundary and determinism

The target-neutral closure includes only project-owned K-H map/dialog data for
the two real factory IDs. The selected pair needs no Max asset. Three exact
atlas prefixes remain allow-listed for older accepted Max summit-checkpoint and
flag-switch factories; whole-helper Max content is excluded. Neither real
Strawberry Jam map, any real SJ asset, the Max DLL, nor the desktop Everest
runtime enters the product.

Three complete generations are recorded by the ignored reproduction evidence.
The final values are filled after regeneration of the narrowed content closure:

- shared closure: `9a067cc582f8a53a95f9150e32917f96891f91752c6582cae4b834b291374cca`;
- managed tree: `7a6bb8938d4e5497ec16d243eb7ba12f1332e88e76f0778d77f6f08c139ca89b`;
- content tree: `1b64d0616a2cfa9e8c4a0141be09d15287bf3fc6e95ee3c7f70452869485d116`;
- complete tree files: `1,245`;
- HookGen catalog: 205 before and 205 after, zero new descriptors;
- reviewed Apple API catalog: 30 before and 30 after, zero new members;
- new frozen IL plans, direct ILHook plans, configured-order plans, and managed
  hook plans: zero.

Omitting Max removes both factories, removes the now-unreferenced base semantic
source and hook/reflection dispositions, keeps unrelated K-F semantic plans
identical, compiles, and changes closure identity. Independently omitting each
base profile fails at the closure gate before product generation.

The final development-signed products and physical/reference results are
recorded after acceptance:

| Gate | Result |
| --- | --- |
| iOS universal IPA | `PASS` — `artifacts/apple-everest/canary/ios/Celeste-Everest-Canary-iOS.ipa`; SHA-256 `e6d4874680003586fd8f13bf2f5726ab6de5c70848860c53baf5422e1aa49768` |
| tvOS IPA | `PASS` — `artifacts/apple-everest/canary/tvos/Celeste-Everest-Canary-tvOS.ipa`; SHA-256 `1ee4191e94613aea4a2a2d158be33e7da57790e156fa0343204e37cd5e5bb747` |
| macOS Everest reference | `PASS` — tutorial behavior and exact NPC dialogue observed; dialogue has Madeline's animated portrait and ornate purple/brown textbox |
| iPhone physical | `PASS` — exact replacement IPA; complete checklist and portrait/textbox reference parity accepted manually |
| iPad physical | `IPADOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE` |
| Apple TV physical | `PASS` — exact replacement IPA; complete checklist and portrait/textbox reference parity accepted manually |

Both products are Release arm64, full trim, full AOT,
`UseInterpreter=false`, and contain no JIT fallback. The iOS product retains
`UIDeviceFamily [1,2]`, native iPad presentation metadata, and iOS 15 minimum;
the tvOS product retains family `[3]` and tvOS 16 minimum. The last
all-three-device physical GREEN remains Stage 25K-B build 35.

Disk free space was approximately 9.7 GiB before cleanup and 25.80 GiB after the
first cleanup. It was 17.32 GiB before the dialogue-fix cleanup, 40.26 GiB after
that cleanup and before the replacement iOS AOT, 34.78 GiB before replacement
tvOS AOT, and 27.99 GiB after both products. Only ignored, reproducible build outputs,
old device-support/cache data, and stale marked products were removed;
authoritative Celeste/FMOD inputs and signing configuration were preserved.

Historical builder/HookGen, K-F through K-A, I-B, H-D through H-A, F-B2,
F-A, E, D, vanilla/input, Save Manager, privacy, docs/link, cleanliness, and
submodule gates are `PASS: current builder 532, HookGen 50, K-F 66, K-E 62, K-D 46, K-C 59, I-B 75, and input profiles 56; immutable K-B 80, K-A 82, H-D 163, H-C 120 (expected YELLOW), H-B 144, H-A 190, F-B2 94, F-A 143, E 130, and D 144; Save Manager Stage 10A/10B/22B also passed`. The K-H verifier passes 1,466 checks with exact products and physical evidence. A fresh recursive clone at the exact final feature SHA independently reacquired the pinned public inputs, regenerated the K-H evidence and three complete closures, and passed the builder, HookGen, K-H, historical, docs/link, privacy, cleanliness, and recursive-submodule gates without copying ignored fixtures. Protected refs remained fixed, K-G remained unmerged, and no GitHub Action ran or changed.

## Answers to the 97 closeout questions

1. Did K-G correctly stop? Yes; its compile failure exposed a real missing semantic closure.
2. Was K-G merged? No; it was read only as immutable diagnostic evidence.
3. Exact K-H starting SHA? `e38a886f54a719ad973f21f4ef84587d52b49718`.
4. MaxHelpingHand version? 1.40.9.
5. ZIP SHA? `abfc5d167936410e9b6665018335ee654455e30710caffe01540b232da468fee`.
6. DLL SHA? `6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6`.
7. Exact Everest CustomBirdTutorial implementation pinned? Yes; stable-1.6458.0 at the pinned commit and source hash above.
8. Exact CustomNPC implementation pinned? Yes; the same Everest commit and the source hash above.
9. What is CustomBirdTutorial's required selected behavior? Info and one dialog-string control, light, immediate show, bubble lifecycle, and a Right pointer; no active bird movement mode.
10. What does CustomTutorialWithNoBird change? It removes the Sprite, suppresses fly-away effects for its type, stores direction, and replaces the vanilla pointer when direction is not Down.
11. How is direction=Right represented? An immutable typed direction accepted by the constructor guard and an exact 37-slice right-edge draw loop.
12. BirdNPC.StartleAndFlyAway hook required? Its selected observable result is required.
13. How lowered? The typed hide path directly performs no startle, sound, flight, flag, or removal.
14. BirdTutorialGui.Render IL required? Its exact selected Right-pointer result is required.
15. How lowered/frozen? It is semantic lowering into the owned typed GUI; no target IL is changed.
16. Any runtime IL? No.
17. What is CustomNPC's required selected behavior? No sprite, two-node talk bounds, one dialog, dummy/cutscene state, cleanup, and repeatability.
18. Which private members did MaxHelpingHand reflect? `textures : List<MTexture>` and `scale : Vector2`, both read/write.
19. How were they replaced? Their exact selected branches are proven unreachable and the flattened entity owns only the needed typed state.
20. CustomNPC.Talk ILHook required? Its exact selected outcome is required; both injected feature branches are false.
21. How lowered/frozen? The ordinary typed one-dialog coroutine directly represents the unchanged selected path.
22. Any runtime reflection? No.
23. Empty base-type stubs used? No.
24. Must be NO. No empty stubs were used.
25. Reusable typed base semantics or flattened entities? Flattened typed selected entities.
26. Why? The exact observable paths are smaller than the desktop bases and reject all wider profiles, while retaining the base behavior that is actually visible.
27. MaxHelpingHand global/load behavior fully audited? Yes for the exact selected profile; 447 types, 2,178 methods, all mechanisms classified, zero unknown.
28. Any additional selected blocker discovered? No unrelated blocker.
29. How many selected factories were type-closure audited? 73.
30. How many fully closed? 73 post-fix.
31. Blocked? Zero post-fix.
32. Unknown? Zero.
33. Does analyzer inspect transitive base types? Yes, through required recursive base-chain nodes.
34. Constructors? Yes, including parameter types, base constructors, fields, and call graph.
35. Static constructors? Yes, through required type-initializer evidence.
36. Lifecycle methods? Yes: Added, Awake, Update, Render, Removed, SceneEnd, and exact reachable equivalents.
37. Module-load hooks? Yes.
38. Reflection? Yes; every selected site needs an accepted static result or an exact unreachable proof.
39. IL? Yes; ordinary, direct, and configured hook dispositions are part of the HOOKS closure dimension.
40. Can an ID-known/base-missing factory still report supported? No.
41. Must be NO. No; both graph validation and registry cross-check fail first.
42. K-F 920/920/0/0 reproduced historically? Yes.
43. Did stronger pre-fix gate catch the two K-G entities? Yes; the result was 73/71/2/0.
44. Post-fix content-ID blockers? Zero.
45. Post-fix type-closure blockers? Zero.
46. Unclassified? Zero in both censuses.
47. Exact readiness marker? `READY_FOR_K_I_REAL_SJ_INTEGRATION_RETRY`.
48. Is it READY_FOR_K_I_REAL_SJ_INTEGRATION_RETRY? Yes.
49. Real SJ maps packaged in K-H? No.
50. Must be NO. No real SJ map or asset is packaged.
51. Tutorial canary physical PASS? `PASS on iPhone and Apple TV` using the exact replacement products.
52. Correct pointer direction? `PASS; the tutorial pointer points Right on both available devices`.
53. Correct birdless behavior? `PASS; no bird or fly-away effects on either available device`.
54. NPC canary physical PASS? `PASS on iPhone and Apple TV; both exactly match the pinned macOS reference`.
55. Talk/dialog? `PASS; Madeline's animated portrait and ornate purple/brown textbox appear, the literal markup is absent, and the exact text is presented on both devices`.
56. Repeated interaction? `PASS; the dialog can be closed and started again on both devices`.
57. New HookGen descriptors? Zero.
58. Final count? 205.
59. New reviewed APIs? Zero.
60. Final count? 30.
61. New frozen IL? Zero; the pointer result is typed semantic lowering.
62. New direct ILHook? Zero; the selected Talk result is typed semantic lowering.
63. New configured ordering? Zero.
64. General DynamicData? No.
65. Runtime Assembly.Load? No.
66. Runtime reflection discovery? No.
67. Runtime RuntimeDetour? No.
68. Live ILHook? No.
69. Three closures identical? `Yes; all three complete 1,245-file trees are identical`.
70. Shared closure SHA? `9a067cc582f8a53a95f9150e32917f96891f91752c6582cae4b834b291374cca`.
71. iPhone physical? `PASS on replacement IPA e6d4874680003586fd8f13bf2f5726ab6de5c70848860c53baf5422e1aa49768, including the full checklist and exact macOS dialogue parity; controller unavailable during acceptance`.
72. iPad? `IPADOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE`.
73. Apple TV? `PASS on replacement IPA 1ee4191e94613aea4a2a2d158be33e7da57790e156fa0343204e37cd5e5bb747, including the full checklist and exact macOS dialogue parity`.
74. Pending markers? iPad retains `IPADOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE`; no iPhone or Apple TV pending marker remains.
75. Last all-three physical GREEN? Stage 25K-B build 35.
76. Full trim? Yes on both exact products.
77. Full AOT? Yes on both exact products.
78. UseInterpreter=false? Yes.
79. JIT absent? Yes.
80. K-F regression? `PASS, 66 checks; four banks, 53 Crystalline and two Vortex occurrences, 920-occurrence content model, 205 HookGen descriptors, 30 APIs, multi-bank lifecycle, Chrono horn, ordering, and AOT boundaries retained`.
81. K-E/K-D? `PASS: K-E 62, K-D 46, and K-C 59 checks`.
82. K-B/K-A? `PASS at their immutable accepted commits: K-B 80 and K-A 82 checks`.
83. LittleEpic/Chrono? `PASS: Stage I-B custom audio and LittleEpic/Chrono, 75 checks`.
84. H-D/H-C/H-B/H-A? `PASS at their immutable accepted commits: H-D 163, H-C expected-YELLOW 120, H-B 144, and H-A 190 checks`.
85. F-B2/F-A/E/D? `PASS at their immutable accepted commits: F-B2 94, F-A 143, E 130, and D 144 checks`.
86. canonical/native locks? `PASS; 56 input-profile checks and exact product metadata retain the canonical/native locks`.
87. protected refs? Unchanged.
88. Actions zero? Yes; no workflow changed and no Action ran.
89. clean clone? `PASS at the exact final feature SHA; public inputs were independently reacquired, all K-H evidence and three closure trees were regenerated, and no ignored fixture was copied`.
90. privacy? `PASS for the final tracked Stage 25K-H surface and disposable clean clone`.
91. disk before/after? About 9.7 GiB before initial cleanup, 40.26 GiB before replacement iOS AOT, 34.78 GiB before replacement tvOS AOT, and 27.99 GiB after both products.
92. development integration-ready? Yes for the exact K-I retry under both gates.
93. exact fast-forward SHA? The final K-H commit recorded in the stage handoff; it fast-forwards from the exact K-F start.
94. Is K-I now the correct next stage? Yes.
95. Is the first unchanged SJ slice ready to retry? Yes, from the accepted K-H tip with both gates.
96. Is full Strawberry Jam supported? No.
97. Must be NO. No; this result is restricted to the exact audited Beginner lobby/Bing selection and these bounded mechanisms.
