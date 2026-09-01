# Stage 25K-B — second real collab and broader CollabUtils2 completion

Status: **GREEN — iOS, iPadOS AND tvOS**

Stage start: `311590334cf7180c2f9df0e8000578c14fa68fad`

Branch: `feature/apple-everest-second-real-collab`

Development integration-ready: **yes**

All-platform release-ready: **no**

## Outcome

The shared static-AOT Apple Everest product now includes its second ordinary
real CollabUtils2 collab, Kayonara Collection 1.0.1. Its real lobby, four
subordinate maps, package-authored journal, full-height checkpoint panels,
three-heart MiniHeartDoor, SilverBerries and RainbowBerry are represented by
deterministic build-time records and typed runtime implementations.

Build 35 also fixes an iPad mini 4 launch failure that was exposed only after
the full two-collab closure reached 7,075 loose mod PNGs. Build 34 decoded all
of those textures during atlas mounting, retaining about 127.2 MiB of RGBA
data before normal game allocation and crossing iPadOS's per-process memory
limit. Build 35 reads PNG dimensions without decoding and loads each texture
on first use. This is a general static-mod-atlas lifecycle fix, not a Kayonara
or map-specific workaround. The exact build has stayed alive for 120 seconds
on iPhone, iPad mini 4 and Apple TV; iPhone and Apple TV reached the normal
Overworld with rendering and audio heartbeats.

The app never scans mod archives or helper folders on device. It does not load
CollabUtils2, EeveeHelper, XaphanHelper or other lowered helper DLLs at runtime,
and it adds no runtime detours, IL rewriting, Lua, native payload or custom
FMOD bank.

## Selection and provenance

The selection pass screened 60 releases from 1,317 direct CollabUtils2
dependents, including 279 candidates below 150 MiB and 151 strong candidates.
Twenty candidates received deep compatibility audits: seven were content-only
and thirteen required unsupported custom-audio expansion. Kayonara was the
smallest reviewed release with a real multi-map lobby and meaningful bounded
completion graph.

| Property | Accepted value |
|---|---|
| Package | Kayonara Collection 1.0.1 |
| Collab ID | `KayonaraCollection` |
| Author | Kayonara team |
| Public release | [GameBanana page](https://gamebanana.com/mods/150587), [release ZIP](https://gamebanana.com/mmdl/1086296) |
| ZIP SHA-256 | `a6c8a1d001926a9167c08dde3d221d72d5deea1c577fd329fb386ae74cd9d7fc` |
| Extracted logical SHA-256 | `20ae5ad7342562c19f725feb20a2587db8253a73a1de0adadfbbbffebd528fc0` |
| License/provenance | No explicit license was present in the archive. This is the team's unchanged public release; the repository redistributes none of its bytes. |

The package has no managed DLL, custom FMOD bank, Lua file or native payload.
The resolved helper graph grows from 15 to 18 reviewed packages, adding exact
EeveeHelper 1.12.5 and XaphanHelper 1.0.79 releases while retaining
CollabUtils2 1.13.4 (`ce7d646252a2fc51f1f513071d97eaa1acedf7ddb0871276c688914e37319f60`).

## Exact collab graph

| Role | SID | Rooms | Source / staged / compatibility SHA-256 |
|---|---|---:|---|
| Lobby | `KayonaraCollection/0-Lobbies/1-Collection` | 1 | `98a4257d09d52c7861227899e33a7c57295fc8731b60622a9bbb0853bf2e2f7c` / `9da4c14b0ef2076002706ea3b940353303053ba218748ced2d82782e9492dfa8` / `6eb259f725e0019e598de9834f06c32500ed44b5b6336e8491dd3d52b5a4a000` |
| Kayonara | `KayonaraCollection/1-Collection/1-Kayonara` | 42 | `0a48b7d87b24cbec7e841148c9be024cd80bca164017218663b75b0e3b841675` / `91148d2cb4d6ee609380112a8ee21e82e9b3aba2b031a395e4c4b5a875d61e92` / `de750691136a599199cc7a3c719dc035ec39feedd25a126f7e059b08b65f2a18` |
| Kayonara B Side | `KayonaraCollection/1-Collection/2-KayonaraBSide` | 16 | `afd672e97dc70af765f461a2d79c4965452ff10742c3e7b52f6bd7058fdc854f` / `70a38f3b43f3cb279a90b1d370b8ccb3d87b32f6fe7d9283532ed918827d52aa` / `521ed182e849a0d2dfbdb78ca2486d1c5f94c71f0e116ab0272573963c64e678` |
| Kayonara C Side | `KayonaraCollection/1-Collection/3-KayonaraCSide` | 4 | `50289523809847139b04163db9b824424543ecf09379c7f198f8e18879d1c4d8` / `93bf30f310cc2f2f4adad3ba7604f440e67ad6806c43a40e3dea7429764ae1d2` / `272796a8086125eb4daa451bd0901f32822d573ff21bae74183d157dbd2733d5` |
| Gated Heart Side | `KayonaraCollection/1-Collection/ZZ-HeartSide` | 9 | `b474a33f9e0a20dd669d33f6d9b12773ae1cc4bf4772770160753707b7ad0b40` / `b8ec39efd17b6f263977383b55d69b9ff1da8a60d375d031bfa3bcb87fdbd822` / `b4450cf9da6f1a9d7543e7cd3bc408ca1c86fbfc4f2a9fe4631301acf2d86317` |

The lobby journal contains exactly Kayonara, B Side and C Side, in that order;
Heart Side remains gated separately. The chapter panel uses the collab's
postcards and authored checkpoints. Kayonara's embedded `WakeUp` intro wins
over the outer sidecar metadata, matching native Everest's black circular
reveal, seated Madeline and stand-up animation. Every subordinate map uses its
generated `SetReturnToHere` return authority, and the collab's no-save-prompt
behavior is preserved.

## Door, berries and progression

The lobby contains one required vertical `CollabUtils2/MiniHeartDoor`, entity
12 at `(464,88)`, size `40x56`, threshold three. Only the main, B Side and C
Side hearts contribute. Its locked state is physically proven; full count
transition/opening evidence is not claimed until explicitly observed.

The graph contains three required SilverBerries and one required RainbowBerry.
It also contains four supported SpeedBerries, five MaxHelpingHand SecretBerries
and one vanilla golden berry. Silver/rainbow state derives from existing map
completion, while speed/silver/golden behavior remains run-local until its
authored completion point. No new durable schema is required.

`AEVPSV1` remains version 1. The cumulative 12-area fixture is 4,987 bytes raw
and 1,278 bytes in the tvOS representation, 1.01% of the 126,976-byte replica
cap. Slot isolation, delete/recreate, import replacement, corruption fallback,
absent-map quarantine and cross-collab isolation pass deterministic tests.

## Static compatibility boundary

HookGen remains 102 targets and the reviewed API remains 30 members. The
selected semantic-factory count grows from 16 to 27, registry factories remain
59, and core gameplay factories grow from four to six. The frozen IL plan
remains five transforms. Direct IL hooks, configured hooks, general
DynamicData, ModInterop registrations, new custom audio, Lua, native/P/Invoke,
runtime assembly loading, runtime content scanning and reflection-based factory
discovery remain absent.

## Determinism and products

Three independent complete generations agree on these identities:

| Boundary | Accepted value |
|---|---|
| Shared closure | `224cb6c223f3d4521c1c9f2499ed42a6c1671a88fa2886eef003f58698a4a50b` |
| Managed tree | 47 files; `21ed46579db76aa3ca2d078a8b8f1eb640db37c59f7c4aa914f4709fe46086eb` |
| Content tree | 7,535 files; `1617ce24472c7755e7368ca8026210b4e86930ed6a0a6a86033d825410051344` |
| Registry / hook transform | `d7ff46c8ff66d0e5928723494ac65f4b08bfb23c110b621caeb30c2261b65ad1` / `0c4039cd649a816ec44668e4f8d7498ac69deba17fd344a7275e6278ffa39f99` |
| API surface | `d594dea86e944b468819818773f2177312445bbebe022bcbc7ea1ff779c8652c` |

Both products use one closure, full trimming, full AOT, `UseInterpreter=false`
and no JIT.

| Product | Bytes | SHA-256 | Package facts |
|---|---:|---|---|
| Universal iOS/iPadOS IPA | 897,617,876 | `35e36e3b1a300ecacb1f2d9e3679a08ee91fdc0067e0e90e22cd06d6878a6656` | 0.1.1 build 35; iOS 15; `UIDeviceFamily [1,2]`; native full-screen iPad landscape; development signed |
| tvOS IPA | 912,070,227 | `f50380e77b7c3a9801c5338eaaea4f9a7b5e74d68328a18bf142136f66826767` | 0.1.1 build 35; tvOS 16; `UIDeviceFamily [3]`; development signed |

The builder suite passes 476 deterministic tests and the K-B verifier passes
107 checks in final mode. Package signatures verify deeply. The exact
build is installed on iPhone 12 Pro Max, iPad mini 4 / iPadOS 15.8.8 and Apple
TV 4K (3rd generation). Apple TV build 34 received the user's all-level pass;
build 35 received a 120-second exact-final launch/render/audio smoke. The user
also passed the build-35 iPhone, iPad and Apple TV physical matrices, including
the final deferred-texture gameplay delta.

## Native reference

Double-click `Launch macOS Everest Reference.command`, or run:

```sh
./scripts/run-apple-everest-macos-reference.sh
```

If its ignored staging has been cleaned, run the preparation command once:

```sh
./scripts/prepare-apple-everest-macos-reference.sh
./scripts/run-apple-everest-macos-reference.sh
```

The preparation script installs the exact reviewed dependency graph into an
isolated native Everest reference. The tracked launcher contains no private
absolute path.

## Closeout state

No GitHub Actions run was triggered. Protected refs and release tags remain
unchanged. About 37.0 GiB of reproducible ignored build data was removed over
the stage, leaving 22 GiB after closeout cleanup. The final commit passed the
privacy scan, documentation-link checks and a disposable clean-clone
reproduction using freshly downloaded, SHA-256-pinned public packages.

## Required 104 answers

1. Kayonara Collection 1.0.1.
2. It was the smallest deeply reviewed content-only release with a real lobby, four maps and a bounded required completion graph.
3. 60 releases were screened from the larger 1,317-record dependency population.
4. 20 were deeply audited.
5. Yes, it is the ordinary unchanged public release.
6. `a6c8a1d001926a9167c08dde3d221d72d5deea1c577fd329fb386ae74cd9d7fc`.
7. CollabUtils2 1.13.4.
8. `ce7d646252a2fc51f1f513071d97eaa1acedf7ddb0871276c688914e37319f60`.
9. `KayonaraCollection`.
10. One lobby.
11. `KayonaraCollection/0-Lobbies/1-Collection`.
12. Four subordinate maps.
13. Main: `KayonaraCollection/1-Collection/1-Kayonara`; source hash `0a48b7d87b24cbec7e841148c9be024cd80bca164017218663b75b0e3b841675`.
14. B Side: `KayonaraCollection/1-Collection/2-KayonaraBSide`; source hash `afd672e97dc70af765f461a2d79c4965452ff10742c3e7b52f6bd7058fdc854f`.
15. Yes.
16. Yes, it gates Heart Side.
17. Three hearts.
18. Yes.
19. Not explicitly claimed yet.
20. Not explicitly claimed yet.
21. Normal GREEN is claimed from deterministic threshold proof, the locked physical state and exact-final matrices; full opening still belongs to Strong GREEN.
22. SilverBerry, RainbowBerry, SpeedBerry, SecretBerry and vanilla golden berry.
23. Three SilverBerries and one derived RainbowBerry are required by the graph.
24. Required-berry physical completion is not explicitly claimed yet.
25. Map completion persists; silver/rainbow derive from it; speed/silver/golden run state remains local until authored completion.
26. Yes; the exact build-35 device matrix passed.
27. Yes; the exact build-35 device matrix passed.
28. Main-map Return to Lobby passed.
29. B-Side Return to Lobby passed.
30. The collab disables the K-A-style save prompt; its authored return flow passed.
31. Yes; automatic progression survived the exact build-35 matrix and cold relaunch.
32. The same authored no-prompt return flow applies to B Side and passed.
33. Yes; build-35 cold relaunch passed.
34. Yes; deterministic A/B isolation and the exact-final physical matrix pass.
35. Completion flags are statically modelled and retained.
36. Completion aggregates are statically modelled and previously matched the reference.
37. Yes, K-A progression is retained.
38. Yes, K-A completion and journal behavior are retained.
39. Yes, LittleEpic is retained.
40. Yes, Fear of the Dark is retained.
41. Yes, Torremolinos map 1 is retained.
42. Yes, Torremolinos map 2 is retained.
43. Yes, zero unclassified required blockers.
44. Yes, zero unsupported required blockers.
45. No new major compatibility mechanism.
46. Zero new HookGen targets.
47. 102 targets.
48. No new frozen IL; the plan remains five transforms.
49. No direct IL hooks.
50. No configured hooks.
51. No general DynamicData.
52. No ModInterop registrations/imports/exports.
53. No new custom audio; the prior Chrono bank remains.
54. No Lua.
55. No native/P/Invoke payload.
56. Zero new reviewed API members.
57. Yes, static factories only.
58. Runtime `GetTypes`/`Activator` factory discovery is absent.
59. Runtime mod/content scanning is absent.
60. Yes, AEVPSV1 remains version 1.
61. No migration.
62. No new collab-specific durable schema.
63. Door and berry state derives from existing per-map completion and run-local semantics.
64. 4,987 bytes.
65. 1,278 bytes.
66. 1.01% of the tvOS replica cap.
67. Pass.
68. Pass.
69. Pass.
70. Pass.
71. Yes, three generations are identical.
72. `224cb6c223f3d4521c1c9f2499ed42a6c1671a88fa2886eef003f58698a4a50b`.
73. Pass; build 35 is installed and passed the user matrix.
74. Pass.
75. Pass; build 35 survived beyond the old crash boundary and passed the user matrix.
76. Pass.
77. Yes, iPhone and iPad use the same universal IPA.
78. Yes.
79. Yes, native full-screen iPad landscape presentation is packaged.
80. Yes, tvOS build 35 is signed and installed.
81. Yes, full AOT.
82. Yes, static collab/progression tests pass.
83. Yes: all-level physical pass on build 34 plus exact-final launch/render/audio and gameplay-delta passes on build 35.
84. The original brief marker was `TVOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE`; hardware became available, so the evidence now records the stronger real-device result.
85. Pass, Chrono custom audio is retained.
86. Pass, the DJ five-transform frozen-IL path is retained.
87. Pass, the K-A collab is retained.
88. Retained through the cumulative closure and locks.
89. Retained through the cumulative closure and locks.
90. Vanilla builders remain covered by their existing locks and regression path.
91. Canonical content/raw/patched, Stage 6 and native iOS/tvOS locks remain unchanged.
92. Protected refs are unchanged and the rc.3 tag remains absent.
93. Yes, Actions runs remain zero.
94. Pass; the final commit reproduced in a disposable recursive clean clone from freshly downloaded, SHA-256-pinned public packages.
95. 13 GiB reported at handoff, 16 GiB measured before cleanup, 27 GiB after cleanup, 23 GiB before tvOS build, 16 GiB after final products and 22 GiB after closeout cleanup; about 37.0 GiB of reproducible data was removed.
96. Yes, development integration-ready; all-platform release-ready remains no.
97. No; no release merge or tag is authorized.
98. Reported after the final feature-branch commit.
99. Kayonara's lobby, four maps, journal/checkpoints, MiniHeartDoor, completion berries, return flow and general deferred static-atlas loading.
100. No. The current selected graph has no configured IL; candidate audit data, not assumption, must determine the next dominant blocker.
101. A third/larger collab audit is rational after this stage is GREEN.
102. Yes, a Strawberry Jam dependency audit becomes rational after GREEN.
103. No, attempting a Strawberry Jam build before that audit is not rational.
104. Run a bounded Stage 25K-C dependency audit comparing a third ordinary collab with Strawberry Jam's smallest required unsupported mechanisms.
