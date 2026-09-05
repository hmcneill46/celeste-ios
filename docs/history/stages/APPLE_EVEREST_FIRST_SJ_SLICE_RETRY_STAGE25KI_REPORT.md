# Stage 25K-I — unchanged Strawberry Jam Beginner integration retry

**Status: YELLOW — production readiness is not established. No K-I Apple product was built.**

The retry started from exact accepted K-H commit
`fe46b3c97cfc7dd74a907da8871ff9ba2c57d6e3`, with local and origin `tvos-port`
matching and a clean worktree. Work is confined to
`feature/apple-everest-first-sj-slice-retry`. K-G
`e0d7c1988a9e5a896001735e5fe21d2251446bd0` remains unmerged diagnostic evidence.
No merge, tag, PR, force-push, upstream push, or Action was performed.

The package-backed preflight finds **73 selected factories, 30 available accepted
registrations, and 43 unavailable registrations**. The missing entries affect
**409 of 920 selected occurrences**: 351 in the lobby and 58 in Bing. The other
511 occurrences have accepted registrations. This is a necessary-condition
census, not a newly established full behavioral closure of the 30 available
entries. A defensible fresh fully-closed count is therefore left null.

This changes the required readiness result. Brief sections 6 and 52 require
STOP before product construction. The missing implementation set cannot be
treated as one small registration-order fix. It also does not establish that
there are 43 distinct new runtime mechanisms: that architectural classification
requires a subsequent bounded implementation/evidence stage.

## Why the historical green graph was insufficient

The immutable K-H graph still says 73/73/0/0. Its generator assigns accepted
classifications by provider/ID membership and fills several closure dimensions
with fixed assertions, including constructor call graphs, lifecycle and module
behavior. The structural validator accepts those assertions without proving
that the selected implementation exists in the production profile. The
rejected K-G implementation was removed from K-G; its ignored preliminary
manifest does not supply implementations to accepted K-H.

For example, the accepted MaxHelpingHand semantic plan supplies four of the
sixteen selected Max IDs. Its entire desktop DLL is omitted, so the other
twelve cannot be obtained from that DLL on-device. The selected FrostHelper
release is 1.80.1, but the accepted semantic registry recognizes only 1.79.1.
The exact 1.80.1 package correctly fails the identity check. Eight selected
provider packages fail production analysis; several accepted semantic packages
also lack selected factories.

| Provider | Unavailable selected factories |
| --- | ---: |
| MaxHelpingHand | 12 |
| FrostHelper | 7 |
| PandorasBox | 4 |
| CherryHelper | 3 |
| CollabUtils2 | 3 |
| FemtoHelper | 3 |
| FlaglinesAndSuch | 2 |
| HonlyHelper | 2 |
| LunaticHelper | 2 |
| VivHelper | 2 |
| BrokemiaHelper | 1 |
| FancyTileEntities | 1 |
| XaphanHelper | 1 |

The [factory evidence](../../../apple-everest/sj-beginner-factory-closure-stage25ki.json)
lists all 73 exact IDs, 21 provider archive identities, production rejection
reasons, and per-map occurrence counts. The existing tutorial and NPC lowerings
are present and their exact authored profiles match the original lobby. Their
prior K-H physical acceptance remains historical evidence for those two
profiles; no real-lobby physical acceptance is inferred from it.

## Changes and reproduction

The host-only `preflight-factory-closure` command now evaluates actual package
inputs and compares the selected graph with accepted provider registrations.
It emits its diagnostic report and fails before content/product generation.
The shared availability check now includes the actual built-in Everest factory
list and checks provider ownership. Three focused regression tests cover a
valid built-in factory, a wrong provider claiming the same ID, and an accepted
graph asserting a factory that is absent. No device runtime source changed.

The [content generator](../../../scripts/generate-apple-everest-stage25ki-content.py)
reads original ZIP entries directly, verifies complete archive identities and
logical trees, resolves the authored texture-cache references, and reproduces
the immutable K-G plan exactly: **14 content-plan packages, 1,212 files, 1,178
SJ-root files, 24 SJ assets, four audio bank files including the root's jam-jar
bank, and seven helper assets**. Exactly two SJ map BINs are selected; 126 are
excluded. No wider non-map asset allowance was needed. The 14 is the content
allow-list package count, not a claim that only 14 runtime dependencies suffice.

The original lobby is 674,648 bytes, SHA
`a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2`.
Its root is 524,807 bytes and its 149,841-byte appendix has SHA
`d76dba1bc5834d999f4d622afd008a702ffe6663b17fe7b0be3c5cc69afabe0d`.
The original Bing map is 135,363 bytes, SHA
`e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347`.
Its root is 70,024 bytes and its 65,339-byte appendix has SHA
`963d5f086279311c18561df8fb135e445ca1cf0bcc2d3f1ba0d8b21f8376da6d`.
Both independent map readers agree on the boundaries. Bing's authored initial
marker remains `00- intro`, `(264,152)`. The lobby has 52 player markers;
its physical entry spawn was not observed. No K-I compatibility IDs were
generated or borrowed from the rejected K-G closure.

The [reproducer](../../../scripts/generate-apple-everest-stage25ki.py) regenerates
all four artifacts from public package inputs and actual production analysis.
It uses the unchanged K-C parser to count backdrops as well as entities and
triggers. A successful reproduction reports **REPRODUCED YELLOW**. The
[verifier](../../../scripts/verify-apple-everest-stage25ki.py) checks the exact
stop, original hashes, census, privacy, protected refs, immutable historical
verifiers and submodules. Its negative control explicitly rejects an unrelated
third SJ map BIN. A verifier PASS means the diagnostic evidence is consistent,
not that integration passed.

For a disposable recursive clone at the final feature SHA, acquire fresh
inputs with `scripts/fetch-apple-everest-stage25ki-inputs.py --output INPUTS`,
bootstrap the pinned host SDK, and build AppleEverestBuilder in Release. Then
run `scripts/generate-apple-everest-stage25ki.py --packages-root INPUTS
--work-root FRESH_WORK --output-root REGENERATED` and
`scripts/verify-apple-everest-stage25ki.sh --regenerated-root REGENERATED
--require-clean`. The public-input fetcher selects only the union of required
content-plan packages and selected providers, validates exact ZIP hashes, and
never copies ignored fixtures. Final-SHA execution and clone removal are
reported in the handoff; they are not pre-claimed here. This diagnostic
reproduction is not the brief's GREEN three-product-closure clean-clone gate,
which cannot run after the readiness stop.

## Validation and boundaries

Current passes: AppleEverestBuilder **535**, typed HookGen **50**, K-D **46**,
K-C **59**, K-E **62**, K-F **66**, I-B **75**, and H-C **120** with its expected
historical YELLOW status. Input profiles pass **56**, Save Manager pairing
**31**, protocol **66**, continuity **57**, and soft reload **21**.

Other immutable verifiers were attempted on the current checkout and did not
pass: K-H requires its exact feature branch; K-B requires its original generated
closure; K-A requires its historical version; H-D lacks ignored composition
evidence; H-B/H-A/F-B2 require historical catalog sizes; F-A/E/D require their
historical transformer/schema versions. They were not modified or their counts
weakened. Their old PASS claims are not represented as fresh cumulative K-I
passes. No attempt was made to rebuild older signed products after the stop.
The K-H graph validator itself still accepts 73/73/0/0, illustrating precisely
why the independent package-backed check is required.

Disk started at 30,588,588 KiB free (about 29.17 GiB); after diagnostic work it
was 30,227,684 KiB (about 28.83 GiB). No cleanup or Apple AOT build was needed.
Before-iOS, before-tvOS and after-product measurements are not applicable.
The final measurement after disposable-clone removal belongs in the handoff.
No Celeste/FMOD authoritative inputs, signing material or accepted products
were deleted. Canonical version remains 0.1.1 build 35 because no product was
constructed; no second version authority was introduced.

One read-only subagent independently audited the readiness/registration
mismatch and the two exact K-H entity profiles. It changed no files or refs and
spawned no agents. The root alone changed source/evidence and Git. The requested
configuration was Astra/Max; model, effort and speed settings are not exposed
by repository tooling and are not independently certified by this report.

## Answers to the 100 handoff questions

1. K-G remained unmerged? Yes, its diagnostic commit is not an ancestor.
2. K-I started from exact K-H? Yes, `fe46b3c97cfc7dd74a907da8871ff9ba2c57d6e3`.
3. Both gates passed before product build? No; actual production preflight failed and no product build was attempted.
4. Content total/accepted/blocked/unknown? 920 selected; 511 have accepted registrations, 409 lack them, zero IDs unclassified. The historical 920/920/0/0 is not current production readiness.
5. Factories total/closed/blocked/unknown? 73 selected; 30 registrations available, 43 unavailable. Fresh fully-closed/semantic-unknown counts were not established and are not invented.
6. Known ID with unresolved closure impossible? Missing/wrong-provider registrations now fail the actual preflight; a blanket guarantee about all semantic assertions is not established by the structural K-H graph.
7. Lobby SHA? `a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2`.
8. Bing SHA? `e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347`.
9. Maps modified? No.
10. SJ map BINs shipped? Zero; the unbuilt content plan selects exactly two.
11. Excluded from the selected plan? 126.
12. Selected file count? 1,212 across 14 content-plan packages.
13. Real lobby launched? Not tested; no K-I product.
14. Actual lobby spawn? Unobserved; 52 authored markers counted.
15. Real lobby audio? Unobserved; original source references `event:/sj21_BegLobby`.
16. Real tutorial? Not physically tested in K-I; exact K-H lowering remains registered.
17. Right pointer? K-H historical canary acceptance only, no K-I real-lobby claim.
18. Birdless behavior? Same scope as answer 17.
19. Real MoreCustomNPC? Not physically tested in K-I; exact K-H lowering remains registered.
20. Animated portrait? Prior K-H acceptance retained; real K-I dialogue untested.
21. Ornate textbox? Same scope as answer 20.
22. Cleaned dialog? Parser regression retained; real K-I dialogue untested.
23. Repeat interaction? Prior K-H acceptance only.
24. Real AttachedJumpThru? Not tested in K-I.
25. Real bloom trigger? Not tested in K-I.
26. Real edit-depth? Not tested in K-I.
27. Real trigger-trigger? Not tested in K-I.
28. Other K-E semantics? Not physically exercised in unchanged SJ content.
29. Real chapter panel? Not tested.
30. Correct Bing metadata? No product manifest/panel generated; original source preserved.
31. Panel launched Bing? No.
32. Actual Bing start? Unobserved; authored expected marker is `00- intro`, `(264,152)`.
33. Bing audio? Unobserved; original source references `event:/sj21_bingovergoogle`.
34. Rooms physically traversed? None. Source inspection found twelve gameplay rooms plus a filler room.
35. Death/respawn? Not tested in K-I.
36. StrawberryWithReturn physical? Not tested; no difficulty claim was used to excuse it.
37. Save and Quit? Not tested in K-I.
38. Cold resume? Not tested in K-I.
39. Exact resumed state? None observed.
40. Return to Lobby? Not tested.
41. Return position/state? None observed.
42. Re-enter Bing? Not tested.
43. Continue state? Not tested.
44. Journal? Not tested.
45. Bing physically completed? No.
46. Completion state? No K-I completion state exists.
47. LittleEpic intact? No installation or device progression modification occurred; fresh physical spot-check not performed.
48. Fear intact? Same scope as answer 47.
49. Torremolinos intact? Same scope as answer 47 for both maps.
50. K-A progression intact? Same scope as answer 47.
51. K-B progression intact? Same scope as answer 47.
52. AEVPSV1 v1? Yes, unchanged.
53. SJ root SaveData/Session? Existing typed runtime unchanged; real SJ persistence not exercised.
54. Slot isolation? Existing deterministic builder coverage passed; no new real-SJ state test is claimed.
55. Delete/recreate? Same scope as answer 54.
56. Imported-save isolation? Same scope as answer 54.
57. Corruption fallback? Same scope as answer 54.
58. 12-bank order? Retained K-F design: vanilla 1–7, Chrono 8, Bing 9, shared 10, Beginner lobby 11, jam jars 12. No K-I product order evidence.
59. One FMOD Studio system? Device runtime unchanged; not observed in K-I.
60. Duplicate loads absent? No K-I physical evidence.
61. Background/Home reopen? Not tested in K-I.
62. Cold initialization once? Not tested in K-I.
63. Soft reload? 21 deterministic host tests pass; real SJ device reload untested.
64. Deferred textures? Existing implementation unchanged; no new decoded-texture/working-set measurements.
65. Three closures identical? No closures generated after the mandatory stop.
66. Shared closure SHA? None for K-I.
67. iPhone exact-final PASS? No K-I IPA exists.
68. iPad? Last known hardware unavailable; not rechecked because no product exists. No pass inferred.
69. Apple TV exact-final PASS? No K-I IPA exists; available hardware does not alter the preflight stop.
70. Last all-three physical GREEN? Stage 25K-B build 35.
71. Full trim? Not evaluated on a K-I product.
72. Full AOT? No Apple compile/AOT attempted.
73. UseInterpreter=false? Existing build policy unchanged, no K-I product assertion.
74. JIT absent? No K-I product to scan.
75. Forbidden surfaces absent? Device sources unchanged; no K-I final product scan exists.
76. K-H regression? Its graph check still accepts historical assertions; full immutable verifier requires its own branch. Two exact lowerings retained, current parser/builder tests pass.
77. K-F? 66 checks pass.
78. K-E/K-D/K-C? 62/46/59 checks pass.
79. K-B/K-A? Attempted but fail their historical closure/version requirements on this checkout; no fresh PASS claim.
80. I-B? 75 checks pass.
81. H-D/H-C/H-B/H-A? H-C passes 120 with expected YELLOW; H-D lacks historical evidence; H-B/H-A fail historical catalog-size assertions.
82. F-B2/F-A/E/D? Attempted; historical catalog/transformer/schema assertions fail on current sources. No verifier changed.
83. Vanilla/input? Input profiles pass 56; vanilla signed products were not rebuilt. Save Manager pairing/protocol/continuity pass 31/66/57.
84. Canonical/native locks? Unchanged; recursive submodule pins and cleanliness are verifier gates.
85. Protected refs? Required RC refs and both tvos-port refs retained; rc3 tag absent.
86. Zero Actions? Yes; no Action dispatched and workflows unchanged.
87. Clean clone? Final-SHA diagnostic reproduction is recorded in the handoff. The GREEN product/three-closure clean-clone gate is not applicable to this stopped attempt.
88. Privacy? Only source, scripts, documentation and metadata/hash evidence added; inputs/products/identifiers/signing data remain untracked.
89. Disk outcome? About 29.17 GiB at start and 28.83 GiB after diagnostic preparation; final after-clone value in handoff. No AOT or cleanup needed for this stop.
90. Development integration-ready? No.
91. All-platform release-ready? No.
92. Exact fast-forward SHA? None recommended. The feature commit is diagnostic and must not be merged as a GREEN integration result.
93. First accepted physically running unchanged SJ slice? No.
94. Full SJ package supported? No.
95. Must answer 94 remain NO? Yes.
96. K-J next audit? Defer expansion; first close the exact missing selected implementations and establish both actual production gates for this same two-map slice.
97. Additional Beginner maps at zero/near-zero distance? Not established; no number is claimed.
98. Expand to several Beginner maps now? No; the current selected graph is not closed.
99. Whole Beginner map set rational yet? No.
100. All 128 SJ maps rational yet? No.
