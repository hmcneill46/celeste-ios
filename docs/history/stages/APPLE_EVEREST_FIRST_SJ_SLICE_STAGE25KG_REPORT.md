# Stage 25K-G — first unchanged Strawberry Jam Beginner slice

**Status: YELLOW — new compatibility mechanism required.**

Stage 25K-G stopped at the composition boundary required by section 48 of its
specification. The unchanged Beginner lobby and Bing binaries were selected
byte for byte, the bounded file plan contained exactly two of the 128
Strawberry Jam map BINs, and the existing K-F verifier still reported
920/920/0/0. The first real iOS product compile then proved that this census was
not a sufficient integration gate.

The lobby contains `MaxHelpingHand/CustomTutorialWithNoBird` at `(1128,787)`
and `MaxHelpingHand/MoreCustomNPC` at `(3168,1904)`. Their public constructors
cannot be treated as standalone static factories. The first derives from
Everest's `Celeste.Mod.Entities.CustomBirdTutorial` and its authored
`direction=Right` behavior relies on helper hook/IL behavior for the bird and
tutorial pointer. The second derives from Everest's
`Celeste.Mod.Entities.CustomNPC`; the distributed helper initializes private
field reflection and provides a `CustomNPC.Talk` IL hook. The static Apple
runtime contains neither Everest base entity, and the selected-factory path
does not close those hook/reflection semantics. Compilation failed with
`CS7068` for both absent base types before AOT.

Adding type stubs would only hide the compiler evidence and would not reproduce
the authored behavior. Implementing the base entities and lowering the exact
MaxHelpingHand behavior needs a new bounded compatibility stage. K-G therefore
did not produce, install, or physically accept an iOS or tvOS product. The
rejected implementation was removed from the branch; only the content plan,
machine-readable YELLOW evidence, generator, verifier, and this report remain.

The [slice result](../../../apple-everest/sj-beginner-slice-stage25kg.json)
records the stop and exact map boundaries. The [content plan](../../../apple-everest/sj-beginner-content-stage25kg.json)
lists all 1,212 selected package files and their hashes. The [physical result](../../../apple-everest/sj-beginner-physical-stage25kg.json)
contains explicit not-run states. The [verifier](../../../scripts/verify-apple-everest-stage25kg.py)
locks those claims and chains the immutable K-F verifier.

The selected content plan has 14 packages and 1,212 files: 1,178 from the SJ
root, 24 from SJ assets, two from AudioA, one from AudioB, five JungleHelper
assets, and two YetAnotherHelper assets. It selects only
`Maps/StrawberryJam2021/0-Lobbies/1-Beginner.bin` and
`Maps/StrawberryJam2021/1-Beginner/Bing_Over_Google.bin`, excluding the other
126 map BINs. A preliminary closure mounted 7,970 files including retained
regressions, preserved both selected map files byte for byte, and had identity
`bc20bca22b14b064b0aef26a4b04855ba03cb3fb7b2846b089f8ceaacbca92d6`.
It is recorded only as rejected diagnostic evidence; no three-run or product
claim is made from it.

The exact lobby file is 674,648 bytes, SHA-256
`a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2`.
Its BinaryPacker root is 524,807 bytes and its 149,841-byte appendix hashes to
`d76dba1bc5834d999f4d622afd008a702ffe6663b17fe7b0be3c5cc69afabe0d`.
The exact Bing file is 135,363 bytes, SHA-256
`e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347`.
Its root is 70,024 bytes and its 65,339-byte appendix hashes to
`963d5f086279311c18561df8fb135e445ca1cf0bcc2d3f1ba0d8b21f8376da6d`.

Disk was 18.19 GiB free before the first cleanup and 20.12 GiB afterward. It
fell during fixture and closure preparation; safe removal of reproducible
runtime and failed-product intermediates left approximately 10 GiB free. No
authoritative input, signing material, or tracked evidence was deleted.

## Answers to the 100 closeout questions

1. Did K-F readiness remain 920/920/0/0? The immutable verifier still reports 920/920/0/0, but K-G proved that result insufficient for real integration, so its readiness conclusion did not remain valid.
2. Were the ORIGINAL distributed lobby bytes packaged? They were byte-identical in the rejected preliminary closure; no accepted product was packaged.
3. Original Bing bytes? The same: byte-identical in the rejected closure, with no accepted product.
4. Were either map modified? No.
5. Exact lobby SHA? `a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2`.
6. Exact Bing SHA? `e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347`.
7. Lobby appendix size? 149,841 bytes.
8. Bing appendix size? 65,339 bytes.
9. Lobby SID? `StrawberryJam2021/0-Lobbies/1-Beginner`.
10. Bing SID? `StrawberryJam2021/1-Beginner/Bing_Over_Google`.
11. How many SJ maps are in product? No accepted product exists; the rejected closure selected exactly two.
12. Any unrelated SJ map BINs? None in the rejected closure; 126 were excluded.
13. Exact SJ asset/content file count included? The plan selects 1,212 package files, 1,178 from the SJ root; the rejected closure mounted 7,970 files after retained regressions.
14. Did real lobby launch? No; compilation stopped before a product existed.
15. Correct real spawn? Not physically tested; the lobby contains 52 authored player markers.
16. Real lobby music? Not physically tested.
17. BegLobby event? Retained deterministically from K-F, not exercised in real K-G content.
18. Jam-jar event? Retained deterministically from K-F, not exercised in real K-G content.
19. Real AttachedJumpThrus worked? Not physically tested in the real lobby.
20. Real bloom triggers? Not physically tested in the real lobby.
21. Real depth edits? Not physically tested in the real lobby.
22. Real trigger-trigger? Not physically tested in the real lobby.
23. Real Contort spotlight? Not physically tested in the real lobby.
24. Real EVM brightness? Not physically tested in the real lobby.
25. Real Jungle MossyWall? Not physically tested in the real lobby.
26. Real YetAnother BubbleFields? Not physically tested in the real lobby.
27. Root lifecycle/state? Deterministic K-E evidence remains, but real K-G behavior was not exercised.
28. Real panel opened? No.
29. Correct Bing metadata? Deterministic manifest preparation succeeded, but there is no physical panel result.
30. Panel launched Bing? No.
31. Correct Bing start room/spawn? Deterministic expectation is room `00- intro` at `(264,152)`; not physically tested.
32. Bing event played? Not in real K-G content.
33. How many real rooms traversed? Zero.
34. Death/respawn? Not tested in K-G.
35. StrawberryWithReturn physically exercised? No.
36. Save and Quit worked? Not tested in K-G.
37. Cold resume? Not tested in K-G.
38. Exact resumed room/state? None.
39. Return to Lobby? Not tested in K-G.
40. Correct lobby return location? None observed.
41. Re-enter Bing? No.
42. Continue state? Not tested.
43. Journal correct? Not tested physically.
44. Bing physically completed? No.
45. Completion flag if completed? Not applicable.
46. Jam-jar state if completed? Not applicable.
47. Existing LittleEpic state intact? No K-G installation occurred; existing data was not touched.
48. Fear? No K-G installation occurred; existing data was not touched.
49. Torremolinos? No K-G installation occurred; existing data was not touched.
50. K-A? No K-G installation occurred; existing data was not touched.
51. K-B? No K-G installation occurred; existing data was not touched.
52. AEVPSV1 still v1? Yes in the retained baseline; K-G made no schema change.
53. SJ root typed SaveData working? It remains deterministically covered by K-E, not physically exercised with real K-G content.
54. SJ Session working? It remains deterministically covered by K-E, not physically exercised with real K-G content.
55. Slot isolation? Retained baseline tests only; no K-G progression was created.
56. Delete/recreate? Retained baseline tests only; no K-G progression was created.
57. Imported-save isolation? Retained baseline tests only; no K-G progression was created.
58. Corruption fallback? Retained baseline tests only; no K-G progression was created.
59. 12-bank order retained? Yes in the rejected closure: vanilla 1–7, Chrono 8, Bing 9, shared 10, Beginner lobby 11, jam jars 12.
60. One FMOD system? The retained design uses one system; no K-G product ran.
61. Duplicate bank loads? Not physically tested in K-G.
62. Background/reopen? Not physically tested in K-G.
63. Cold process bank load exactly once? Not physically tested in K-G.
64. Soft reload? Not physically tested in K-G.
65. Deferred texture decoding still active? Retained in the baseline; no K-G device observation exists.
66. Peak/memory observations? None, because no accepted product launched.
67. Three closure runs identical? No; mandatory STOP occurred after the first product compile exposed the new mechanism.
68. Shared closure SHA? Rejected preliminary diagnostic closure: `bc20bca22b14b064b0aef26a4b04855ba03cb3fb7b2846b089f8ceaacbca92d6`.
69. iPhone exact final physical PASS? No final product exists, so no pass is claimed.
70. iPad physical? `IPADOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE`.
71. Apple TV physical? Not run because the mandatory YELLOW stop preceded a tvOS product.
72. Full trim? No accepted K-G product exists.
73. Full AOT? No; iOS failed at managed compilation before AOT, and tvOS was not run.
74. UseInterpreter=false? It was requested by the rejected build, but no accepted product exists.
75. JIT absent? No product claim is available.
76. Forbidden surfaces absent? No product claim is available; the blocker itself includes helper runtime reflection/IL-hook assumptions forbidden from being silently shipped.
77. K-F regression? PASS through the unchanged K-F verifier.
78. K-E? PASS through the historical verifier chain.
79. K-D? PASS through the historical verifier chain; its exact eight-provider audit did not cover this MaxHelpingHand factory surface.
80. K-B/K-A? PASS through the historical verifier chain.
81. LittleEpic/Chrono? PASS in retained historical verification; no K-G physical regression run occurred.
82. DJ IL? PASS in retained historical verification.
83. H-D/H-C/H-B/H-A? PASS in retained historical verification.
84. F-B2/F-A/E/D? PASS in retained historical verification.
85. Vanilla? Retained historical verification; no K-G product build completed.
86. Canonical/native locks? Retained historical verification; K-G did not modify them.
87. Protected refs? Unchanged at the required SHAs; `v1.0.0-rc.3` remains absent.
88. Actions zero? Yes; no workflow changed and no Action was run.
89. Clean clone? Not run after the mandatory new-mechanism stop; no product or reproducibility claim depends on one.
90. Privacy? PASS; no inputs, products, device IDs, signing data, or private paths are tracked.
91. Disk outcome? 18.19 GiB before cleanup, 20.12 GiB after initial cleanup, and approximately 10 GiB after removal of failed-product intermediates; no accepted products or disposable clean clone existed.
92. Development integration-ready? No.
93. All-platform release-ready? No.
94. Exact fast-forward SHA? None; this YELLOW branch must not be fast-forwarded as an integration result.
95. Is this the first physically running unchanged Strawberry Jam map on Apple? No.
96. Is the whole Strawberry Jam package supported? No.
97. What is the next best expansion after K-G? A bounded compatibility stage for Everest `CustomBirdTutorial`/`CustomNPC` and the exact selected MaxHelpingHand load, hook, and reflection semantics, followed by a fresh K-G attempt.
98. How many additional Beginner maps appear zero/near-zero incremental blocker? Not audited after the mandatory stop; no number is claimed.
99. Is a full Beginner lobby now rational? The same lobby is the target, but physical integration is not rational until the exact MaxHelpingHand blocker is closed.
100. Is full 128-map Strawberry Jam rational yet? No.
