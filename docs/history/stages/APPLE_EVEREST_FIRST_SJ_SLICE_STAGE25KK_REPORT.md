# Stage 25K-K — unchanged Strawberry Jam Beginner slice integration retry

Status: **YELLOW_NEW_AUTOTILER_COMPATIBILITY_MECHANISM**. Stopped before Apple product generation under brief sections 46 and 53.

The unchanged Beginner lobby uses 26 foreground cells from an Everest 5×5 tile definition. The accepted K-J runtime has fixed 3×3 matching and nine-cell masks. Loading the complete definition would write beyond that array. Two actual lobby cells with identical 3×3 neighborhoods require different 5×5 results, so projecting the definition into the old mechanism cannot preserve the rendering. This requires a bounded new compatibility mechanism. No new mechanism was implemented in K-K.

The selected K-J gates remain accepted: 920/920 content occurrences, 73/73 actual production registrations, and 73/73 closed selected semantic profiles. These gates establish the selected factory scope; they do not establish complete real-map composition. No K-K Apple product, AOT build, signing, installation, gameplay, macOS reference launch or save access occurred. The user-accepted K-J build 41 remains the latest primary-device product. K-K is neither development integration-ready nor release-ready.

## Source and execution boundary

Start: `e69bfd6eeba3d36a5d745c24b3bc0f2f3f4ee92f`, verified as both local and origin `tvos-port`. Branch: `feature/apple-everest-first-sj-slice-retry2`. K-G and K-I were inspected only as immutable history and remain non-ancestors. Only the K-K feature branch may be pushed; no merge, tag, PR, upstream push, force push or GitHub Actions dispatch.

Requested configuration: GPT-6 Astra · Max · Standard. Runtime model/effort/speed identifiers are not independently exposed, so the request is recorded without claiming verification. Exactly two read-only subagents audited composition and the terrain stop. Both independently agreed with the finding. Root was the sole writer, test runner and Git/cleanup owner. No further agents were used.

Only host diagnostics, portable evidence and documentation changed. Production source, helper semantics, canaries, codecs, canonical/native inputs and version authority remain unchanged. Initial uncommitted content-plan implementation drafts were removed when the stop was established; no draft reaches this branch's final diff.

This tracked report freezes the diagnostic facts. The separate ignored `stage25kk/FINAL_REPORT.md` and `completion-index.json` record the final feature commit, push/readback, exact verifier, final-SHA disposable clone and final disk measurements. They do not make a later revision or the old build 41 into a K-K physical PASS. No integration fast-forward SHA is recommended.

## Exact packages and map identities

StrawberryJam2021 **1.0.12**, ZIP SHA256 `4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655`, root DLL `Code/StrawberryJam2021.dll` SHA256 `8d5b9184204e7e7728965bcf95af5f150f6220dafbfe52fdd6c23e50d47e5258`. All 24 required selected-provider/presentation ZIPs were independently reacquired at pinned public release identities. Distributed DLLs were verified inside those archives; no live updater version resolution. The separately retained Chrono regression package was hash-verified for the baseline compile.

| Field | Beginner lobby | Bing_Over_Google |
|---|---|---|
| Original member | `Maps/StrawberryJam2021/0-Lobbies/1-Beginner.bin` | `Maps/StrawberryJam2021/1-Beginner/Bing_Over_Google.bin` |
| SID | `StrawberryJam2021/0-Lobbies/1-Beginner` | `StrawberryJam2021/1-Beginner/Bing_Over_Google` |
| Complete SHA256 | `a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2` | `e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347` |
| File / root / appendix bytes | 674648 / 524807 / 149841 | 135363 / 70024 / 65339 |
| Appendix SHA256 | `d76dba1bc5834d999f4d622afd008a702ffe6663b17fe7b0be3c5cc69afabe0d` | `963d5f086279311c18561df8fb135e445ca1cf0bcc2d3f1ba0d8b21f8376da6d` |
| Source package label | `3Plain` | `Bing_Over_Google` |
| Source room / first local player marker | `sj2021beginnerlobby` / (1012,680) | `00- intro` / (264,152) |
| First source world marker | (588,40) | (-56,152) |
| Room count / player-marker count | 1 / 52 | 13 / 20 |
| Source-derived compatibility ID | `e9e4d8c99bdecded8a85516e723096abfa51340c7a1d00524c27686de3c9c9e6` | `4024d36a33c33e411381e26ca2482ba09f828bc1c7f86306a98a915e904e39e1` |
| Actual entry / spawn / product registration | None | None |

Bing source rooms: `00- intro`, `01- Crusher`, `02- Bait N' Switch`, `02B- a stwawbewwy??`, `03- Uberjump`, `04- Head Trauma`, `05- Boing`, `06- Bubbles`, `07- Falling Cannon`, `07B- OwO whats this??`, `08- U Turn`, `09- Fin`, `filler`. The source `StartLevel=01-intro` is stale; the existing accepted first-real-room fallback already applies. No Apple or desktop observation is inferred from these source markers.

The exact per-provider versions, ZIP/DLL hashes, authored factory summaries, every spawn and every required asset delta are in the linked JSON evidence. Original BIN bytes and appendices were never rewritten or distributed in Git.

## Terrain finding and scope

Original `Graphics/SJ2021xmls/BeginnerLobby/ForegroundTiles.xml`: 53,620 bytes, SHA256 `4d13bd8fc7164ddd7b20015e17dde1b7aaa035d292ac59b058e40d22c6876f8f`. Tileset `J` begins at line 1070, uses `scanWidth=5`, `scanHeight=5`, texture path `SJ2021/mosscairn/grayExtended`, and ignores tile `7`. It has 62 ordinary 25-cell masks plus center/padding records. The first mask is `xxxxx-xx0xx-x111x-xx1xx-xx1xx`.

All 26 actual uses are in `sj2021beginnerlobby`: row 263 at x446–448, 453–461, 465–467; row 264 at x448, 454–461, 466–467. The first tile is world (3144,1464). Bing uses zero `J` cells; it cannot substitute for the required lobby-entry route.

Accepted generated Autotiler source SHA256 is `c0a174b10272fb67226a71833b357ee443d4e007ef3e1814b6cbccde8a790168`, identical in accepted preflight, iOS and tvOS managed trees. `Masked.Mask` and adjacency storage are nine bytes; parsing writes every 0/1/x without a 25-cell bounds accommodation, sorting and matching inspect nine cells, and neighborhood construction is fixed 3×3. Actual compiled `Celeste.Autotiler/Masked::.ctor()` confirms `ldc.i4.s 9`, `newarr System.Byte`, and the Mask field assignment. This is compiled-contract and source evidence, not a physical crash claim.

| Actual tile cell | Existing 3×3 neighborhood / distance-two cross | Required Everest result |
|---|---|---|
| (446,263) | `111111111` / `1111` | center, texture (5,14) |
| (448,263) | `111111111` / `1111` | `xx10x-x1111-11111-x111x-xx1xx`, texture (10,3) |

The second cell's 5×5 neighborhood is `1110011111111111111111111`. The reproduced counterexample distinguishes the rules even when all information available to the existing 3×3 matcher and its padding check agrees. Neither dropping `J`, truncating the masks, changing map cells nor using canary terrain would satisfy unchanged-content acceptance.

The accepted K-J XML projection in `StaticSemanticLowering.cs` includes only its reviewed canary tile IDs and excludes `J`. Existing runtime patches address debris ABI and Fancy overlays; they do not parse or match variable scan dimensions. Pinned Everest's `Patches/Autotiler.cs` explicitly parses scan dimensions, allocates width×height masks and implements the larger neighborhoods and fill/padding rules. Supporting that behavior is the new bounded work, outside K-K's composition allowance.

## Content plan and deferred composition findings

The historical selection was regenerated directly from public archives and compared with immutable K-I tracked metadata: **14 content-plan packages, 1,212 files, 1,178 SJ-root files, 24 SJ assets, four selected SJ bank files, seven helper assets, exactly two selected SJ BINs and 126 excluded**. The algorithm was independently run in K-K; no ignored K-I output was copied. This plan is explicitly **not product-ready**.

The audit identifies **117 additional required root files**, with individual original paths, hashes and reasons: 101 accepted animation frames (28 coral, 57 spider, 16 rabbit), the Bing `areas/SJ2021/meters/2-med` icon, the `SJ2021/Hanky/ForestNight` color grade, and 14 original textures referenced by the complete foreground/background constructors (11 foreground, three background). No revised 1,329-file plan is asserted sufficient; all future additions still need complete path/case/reference validation and the terrain mechanism remains blocked. No unrelated map was added.

Further audited composition work is recorded without implementation: preserve both BIN header labels while binding static SIDs; apply real map graphics metadata rather than the owned-canary SID whitelist; supply exact bounded collab/progression/presentation metadata; and guard destinations outside the selected two-map set. Existing chapter-panel area resolution can otherwise fall back to vanilla Area 0 for excluded jars. The real lobby contains other map jars, gym and heartside references, so availability must be explicit in a future bounded product. No map or authored attribute should be changed to solve these paths.

The actual NPC credits dialogue has no Madeline portrait command. The ornate portrait/PASS line belongs to K-H's owned diagnostic dialogue. The later real-map acceptance should follow original package/desktop behavior, preserving plain credits where authored, rather than injecting the K-H fixture text or portrait. K-J's accepted isolation fix remains unchanged.

## Three gates and compiled baseline proof

| Gate | Retained result | Evidence scope |
|---|---|---|
| Content IDs | 920 selected / 920 accepted-or-vanilla / 0 blocked / 0 unclassified | Regenerated exact original map/profile census |
| Production registrations | 73 selected / 73 available / 0 missing | Fresh accepted K-J profile regeneration and six actual compiled DLL comparisons |
| Selected semantics | 73 selected / 73 closed / 0 blocked / 0 unknown | Exact final K-J accepted ledger, unchanged source and original authored guards |
| Real-map composition | Blocked | New 5×5 terrain mechanism; no ready K-K closure |

Complete accepted registry SHA256 `6e5b89f7d952aa98e72640abce0c75522567fb9d48f8b027e3db54f5cf9da72f`; gameplay registry source `8ed2794ec88b7ba140ace3763da73eb7eb37023c703a797030ace99e41587a59`; accepted semantic ledger `0097f41ed546502765849d86f951925596b8ba0d61671a98ddd446cbe0f6f652`; accepted K-J shared closure `21c6480c3a852322b7a4efc54e58c447561f455abb54f2e67c1f8d885869247f`. No K-K shared closure exists.

The fresh baseline preflight verifies the exact package set, actual generated selectors, every linked implementation and all six compiled DLLs. Its inspected Celeste DLL SHA256 is `92d8b4fc49a25f624a751480b5d3701870233c527eee7fb94a3275c60f1d90c2`. The portable receipt binds the full local preflight and actual IL inspection by hash. This preserves the stronger K-J proof without calling a canary product real-content acceptance. HookGen catalogue 205, reviewed API members 30, and 17 frozen IL transforms (five DJ, 12 Sideways) are unchanged.

## Products, physical testing and persistence

K-K iOS/tvOS version, IPA bytes/hash, full trim/AOT result and physical matrices: **NOT_BUILT_PREFLIGHT_STOP / NOT_RUN_NO_KK_PRODUCT**. No static collab selection, lobby/panel/warp route, Bing gameplay, helper interaction, audio, berry collection, Save/Quit, cold resume, Return to Lobby, journal, completion, lifecycle or soft reload was tested on a K-K product. Source-derived identities are not installed progression descriptors. Existing saves were neither read nor altered. AEVPSV1 remains schema v1; existing slot/lineage/recovery tests passed, while real-SJ descriptor integration remains unrun.

Accepted build 41 archives and signed apps were preserved. Their identities are historical K-J acceptance, not K-K products:

| Platform | Version | IPA bytes | IPA SHA256 | Retained package metadata |
|---|---|---:|---|---|
| iOS | 0.1.1 (41) | 911281301 | `8460e90214f903879ae64ca4d94f04313d45f71860b109103040c502f6442e20` | arm64, family [1,2], minimum iOS 15, native iPad landscape metadata |
| tvOS | 0.1.1 (41) | 925724444 | `537016394df71b232a424f2a0d2c077e3f47b4584e4ba9be02ce1b30fb86d519` | arm64, family [3], minimum tvOS 16 |

Both retained plist identities were rechecked in K-K. Their historical accepted full-trim, full-AOT, UseInterpreter=false, JIT-free, 302-root native proofs and 615-check final K-J verifier are preserved; no new native proof is claimed after the reproducible intermediate cleanup.

Exact K-K iPad status: **IPADOS_PHYSICAL_DEFERRED_LEGACY_COMPATIBILITY_POLICY**. It is neither PASS nor FAIL, and no new simulator or physical iPad result is claimed. Modern texture/graphics quality and architecture were not reduced. Last historical all-three-device physical GREEN remains **Stage 25K-B build 35**; latest primary-device acceptance remains K-J build 41 on iPhone 12 and Apple TV.

No final K-K bank manifest exists. Historical accepted K-J order remains vanilla banks 1–7, Chrono at 8, `sj21_bingovergoogle` at 9, `sj21_shared` at 10, `sj21_BegLobby` at 11, `sj21_jamjars` at 12, followed by its retained Collab and Honly regression banks at 13–14. A future real closure must justify its final exact order and retained regression-only banks separately. No K-K event resolution, audible result, duplicate-load absence, texture decode count, working set, load time or frame rate is claimed. One-system FMOD and deferred-texture code remain unchanged.

## Reproduction, regression and disk evidence

Three fresh host processes independently regenerated all six K-K evidence documents from the pinned public inputs, with identical file hashes. These are host selection/profile/terrain analyses, **not three complete real-content closure builds**. Complete K-K collab/progression/audio/managed/content/shared-closure reproduction, AOT and device gates stopped with the unsupported mechanism. Final-SHA recursive-clone acquisition, repeated host analysis, current host tests, verifier, clean status and clone deletion are recorded separately in the final handoff.

Current regression results: builder **597 PASS**, typed HookGen **50 PASS**, desktop HookGen/direct-hook **PASS**, Save Manager pairing **31**, protocol **66**, continuity **57**, soft reload **21**, input profiles **56**, K-D **46**, K-E **62** (including K-C audit **59** through its existing isolated historical-ref view) and I-B **75**, all PASS. The builder covers current K-H dialogue/profile lowerings, selected K-F/K-E behaviors, configured ordering and progression/durability contracts. Real-SJ descriptor tests and physical behavior remain unrun. Historical immutable product verifiers and literal-count contracts remain unchanged; the accepted K-J 615-product checks are historical, while its package-backed compiled gate was freshly reproduced. They are not relabeled fresh K-K product PASS.

The K-K verifier independently regenerates original input evidence, binds the accepted source/semantic/compiled receipts, checks the stop and protected refs, rejects nine false-positive mutations, checks privacy and documentation links, and forbids product/AOT trees. `--require-ready` must exit nonzero. The final handoff records exact final check counts and hashes. No new production/runtime/native surface was added.

Recorded disk: start **8.770 GiB**, after owned cleanup **15.172 GiB**. Cleanup removed only marked K-J platform `obj`, `bin` and `publish` intermediates after recording inventories and verifying preservation of both accepted build 41 archives, signed apps, source and native evidence. Authoritative Celeste/FMOD inputs, signing setup, tracked evidence and accepted artifacts remain intact. The preferred 25/30 GiB AOT threshold was never needed because no AOT was started. Before-iOS-AOT, before-tvOS-AOT and after-new-products checkpoints are not applicable. Final and after-clone-removal disk values are in the final handoff.

Protected locks: `tvos-port` and `origin/tvos-port` at the start SHA; `ios-v0.1.1-rc.1^{}` at `27e16b4724d94d3991b99c4795f680fcb0e5830c`; `v1.0.0-rc.1^{}` at `ee52b0868df091746f134d95d4f020f94f23d4fb`; `v1.0.0-rc.2^{}` at `641e86e4ed164cdf93f602ce2f11436449654d6e`; `origin/release/v1.0.0-rc.3` at `c8134c8ca7924cf12f48527e714b5242c6024927`; `v1.0.0-rc.3` tag absent. Final remote readback and zero branch Actions are captured after the sole authorized feature push.

## Reproduction commands

Run from the checkout root; inputs and machine outputs stay ignored:

```sh
python3 scripts/fetch-apple-everest-stage25kk-inputs.py --output .build/apple-everest/stage25kk/packages
python3 scripts/generate-apple-everest-stage25kk-content.py --packages-root .build/apple-everest/stage25kk/packages --output .build/apple-everest/stage25kk/reproduced-plan.json --compare-ki
python3 scripts/generate-apple-everest-stage25kk.py --packages-root .build/apple-everest/stage25kk/packages --output-root .build/apple-everest/stage25kk/reproduced-evidence
scripts/verify-apple-everest-stage25kk.sh --packages-root .build/apple-everest/stage25kk/packages --require-clean --output .build/apple-everest/stage25kk/verification.json
```

The last command verifies an expected YELLOW diagnostic. Adding `--require-ready` intentionally rejects readiness; it cannot authorize AOT. Optional baseline-preflight, compiled-mask-proof and accepted-autotiler-source arguments bind the preserved local compiler evidence. A fresh clone can verify portable accepted receipts without claiming to have rebuilt the unavailable real product.

## Tracked evidence

- [Slice, maps and asset deltas](../../../apple-everest/sj-beginner-slice-stage25kk.json)
- [Reproduced historical content plan](../../../apple-everest/sj-beginner-content-stage25kk.json)
- [Production readiness stop](../../../apple-everest/sj-beginner-production-readiness-stage25kk.json)
- [Selected factory scope and provider identities](../../../apple-everest/sj-beginner-factory-closure-stage25kk.json)
- [Physical status](../../../apple-everest/sj-beginner-physical-stage25kk.json)
- [Terrain census and counterexample](../../../apple-everest/sj-beginner-terrain-stage25kk.json)
- [Accepted runtime/IL contract](../../../apple-everest/sj-autotiler-contract-stage25kk.json)
- [Exact accepted K-J semantic ledger](../../../apple-everest/sj-kj-accepted-semantic-stage25kk.json)
- [Fresh compiled-baseline receipt](../../../apple-everest/sj-kj-compiled-baseline-stage25kk.json)
- [Permanent verifier](../../../scripts/verify-apple-everest-stage25kk.py)

## Explicit answers to all 120 questions

“Not run” below means the required pre-product YELLOW stop, not an inferred PASS or a device failure. Historical K-J evidence is labeled separately.

| # | Question | Answer |
|---:|---|---|
| 1 | Exact K-K start SHA? | `e69bfd6eeba3d36a5d745c24b3bc0f2f3f4ee92f`. |
| 2 | Final SHA? | The final handoff report and completion index bind the committed diagnostic revision; this tracked file cannot contain its own commit hash. |
| 3 | Branch? | `feature/apple-everest-first-sj-slice-retry2`. |
| 4 | K-G merged? | No; rejected K-G is not an ancestor. |
| 5 | K-I merged? | No; rejected K-I is not an ancestor. |
| 6 | Did content-ID preflight pass 920/920/0/0? | Yes, retained selected factory scope: 920 / 920 / 0 / 0 from the exact unchanged maps. |
| 7 | Did production registration pass 73/73/0? | Yes, retained K-J actual compiled profile: 73 / 73 / 0. |
| 8 | Did semantic closure pass 73/73/0/0? | Yes, accepted K-J selected semantic scope: 73 / 73 / 0 / 0. Real-map composition is blocked separately. |
| 9 | Was readiness based on the actual compiled registry? | Yes for the retained K-J profile: fresh package-backed regeneration and six compiled DLL comparisons. No K-K real-slice registry was produced. |
| 10 | Exact final registry SHA? | Retained complete registry `6e5b89f7d952aa98e72640abce0c75522567fb9d48f8b027e3db54f5cf9da72f`; no new K-K product registry. |
| 11 | Exact semantic-ledger SHA? | Accepted final K-J ledger `0097f41ed546502765849d86f951925596b8ba0d61671a98ddd446cbe0f6f652`. |
| 12 | Strawberry Jam exact version? | StrawberryJam2021 1.0.12. |
| 13 | Root ZIP SHA? | `4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655`. |
| 14 | Root DLL SHA? | `8d5b9184204e7e7728965bcf95af5f150f6220dafbfe52fdd6c23e50d47e5258`. |
| 15 | Lobby source SHA? | `a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2`. |
| 16 | Bing source SHA? | `e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347`. |
| 17 | Were either map modified? | No. Both complete original byte streams and appendices were independently verified. |
| 18 | How many SJ gameplay maps packaged? | Zero packaged; exactly two selected in the host-only plan. |
| 19 | How many excluded? | 126 excluded from the selected plan. |
| 20 | Exact selected file count? | 1,212 in the reproduced historical plan; 117 additional root files identified before any future product. No revised ready plan or product generated. |
| 21 | Did real Beginner lobby launch? | No K-K launch was attempted; stopped before AOT. |
| 22 | Actual lobby entry room? | No actual entry. Source room: `sj2021beginnerlobby`. |
| 23 | Actual spawn? | No actual spawn. First source marker is local (1012,680), world (588,40); 52 authored lobby player markers are recorded. |
| 24 | Were all assets present? | Not physically tested. The historical plan omits the 117 documented required root files. |
| 25 | Any unknown factory error? | No K-K runtime was launched; absence of an observed error is not a runtime PASS. |
| 26 | Any constructor error? | Source and compiled-IL analysis predict an out-of-range write when the full 25-cell tile mask reaches the accepted nine-cell array. No device exception was claimed. |
| 27 | Any profile-guard failure? | No K-K physical guard result. Exact authored profiles still match K-J; current compiled canary guard tests pass. |
| 28 | Real BegLobby audio? | Not run: K-K stopped before product generation. |
| 29 | Real CustomTutorialWithNoBird? | Not run: K-K stopped before product generation. |
| 30 | Right pointer? | Not run: K-K stopped before product generation. |
| 31 | Birdless behavior? | Not run: K-K stopped before product generation. |
| 32 | Real MoreCustomNPC? | Not run: K-K stopped before product generation. |
| 33 | Animated portrait? | Not tested. The real authored credits entry contains no Madeline portrait command; the K-H portrait belongs to its owned diagnostic dialogue. |
| 34 | Ornate textbox? | Not tested. Real source credits use the portrait-free default presentation; do not replace them with the ornate K-H fixture. |
| 35 | Clean dialog? | Not physically tested. Current dialogue isolation/parser regressions pass; original dialogue bytes unchanged. |
| 36 | Repeat interaction? | Not run: K-K stopped before product generation. |
| 37 | Real LobbyMapController? | Not run: K-K stopped before product generation. |
| 38 | Real LobbyMapMarker? | Not run: K-K stopped before product generation. |
| 39 | Real LobbyMapWarp? | Not run: K-K stopped before product generation. |
| 40 | Did actual Collab navigation work? | Not run: K-K stopped before product generation. |
| 41 | Real SidewaysJumpThru? | Not run: K-K stopped before product generation. |
| 42 | Sideways static IL plan unchanged? | Yes: unchanged accepted 12 Sideways transforms after the retained five DJ transforms, 17 total. |
| 43 | Real Frost 1.80.1 semantics? | Real content not run; exact FrostHelper 1.80.1 identity and seven accepted selected profiles retained. |
| 44 | Real Cherry/Fancy/Femto examples? | Not run: K-K stopped before product generation. |
| 45 | Real Pandoras examples? | Not run: K-K stopped before product generation. |
| 46 | Real Viv/Xaphan examples? | Not run: K-K stopped before product generation. |
| 47 | Real Crystalline bloom? | Not run: K-K stopped before product generation. |
| 48 | Real Crystalline edit-depth? | Not run: K-K stopped before product generation. |
| 49 | Real Crystalline trigger-trigger? | Not run: K-K stopped before product generation. |
| 50 | Real Vortex AttachedJumpThru? | Not run: K-K stopped before product generation. |
| 51 | Real K-E helper semantics? | Not run: K-K stopped before product generation. |
| 52 | Real chapter panel? | Not run: K-K stopped before product generation. |
| 53 | Correct Bing title/author/icon? | Not physically tested; required Bing icon omitted by historical selection is identified in the asset delta. |
| 54 | Correct strawberry capacity? | Not run: K-K stopped before product generation. |
| 55 | Start/Continue state? | Not run: K-K stopped before product generation. |
| 56 | Did panel/warp launch Bing? | Not run: K-K stopped before product generation. |
| 57 | Actual Bing starting room? | No physical start. First real source room: `00- intro`. |
| 58 | Actual Bing spawn? | No physical spawn. Source local (264,152), world (-56,152). |
| 59 | Bing audio? | Not run: K-K stopped before product generation. |
| 60 | Rooms physically traversed? | None. |
| 61 | Death/respawn? | Not run: K-K stopped before product generation. |
| 62 | StrawberryWithReturn physically tested? | No. Deterministic profile and K-J canary proof retained; no object was moved. |
| 63 | Save and Quit? | Not run: K-K stopped before product generation. |
| 64 | Cold resume? | Not run: K-K stopped before product generation. |
| 65 | Exact resumed room/state? | No resumed K-K room or state exists. |
| 66 | Return to Lobby? | Not run: K-K stopped before product generation. |
| 67 | Correct return state? | Not run: K-K stopped before product generation. |
| 68 | Re-enter Bing? | Not run: K-K stopped before product generation. |
| 69 | Continue state preserved? | Not run: K-K stopped before product generation. |
| 70 | Journal correct? | Not run: K-K stopped before product generation. |
| 71 | Bing physically completed? | No. |
| 72 | Completion state if completed? | Not applicable; no physical completion. |
| 73 | Jam-jar/completion UI if completed? | Not applicable; no physical completion. |
| 74 | LittleEpic state intact? | No installation, deletion or save access; previous data untouched. Not physically spot-checked in K-K. |
| 75 | Fear state intact? | No installation, deletion or save access; previous data untouched. Not physically spot-checked in K-K. |
| 76 | Torremolinos states intact? | Both maps: no installation, deletion or save access; previous data untouched. No K-K spot-check. |
| 77 | K-A state intact? | No installation, deletion or save access; previous data untouched. No K-K spot-check. |
| 78 | K-B state intact? | No installation, deletion or save access; previous data untouched. No K-K spot-check. |
| 79 | AEVPSV1 remains v1? | Yes, AEVPSV1 schema v1 and codecs unchanged. |
| 80 | Slot isolation? | Current existing progression/durability regression suite passes; real SJ descriptors were not registered or tested in a product. |
| 81 | Delete/recreate? | Current existing deletion/lineage regressions pass; no real-SJ product descriptor exercise. |
| 82 | Imported-save isolation? | Current existing replacement/import isolation regressions pass; no real-SJ product descriptor exercise. |
| 83 | Corruption fallback? | Current existing A/B and corruption isolation regressions pass; no real-SJ product descriptor exercise. |
| 84 | One FMOD Studio system? | One-system implementation retained unchanged. No K-K runtime observation. |
| 85 | Exact final bank order? | No final K-K bank manifest. Historical accepted K-J order is documented below; it is not asserted as a real-slice order. |
| 86 | Duplicate custom bank loads absent? | No K-K runtime observation. Existing one-system/dedup logic and current custom-audio regressions retained. |
| 87 | Background/reopen? | Not run: K-K stopped before product generation. |
| 88 | Home/reopen? | Not run: K-K stopped before product generation. |
| 89 | Cold initialization once? | Not run: K-K stopped before product generation. |
| 90 | Soft reload? | Not run: K-K stopped before product generation. |
| 91 | Deferred texture loading retained? | Yes, unchanged deferred-loading implementation; no real-SJ texture counts or decoding measurements. |
| 92 | Any major performance/memory issue on modern devices? | Not measured: no K-K modern-device run. |
| 93 | Did you avoid degrading modern quality for the deferred iPad? | Yes; no runtime, graphics, texture quality or architecture changes. |
| 94 | iPhone exact-final physical PASS? | No: NOT_RUN_NO_KK_PRODUCT. |
| 95 | Apple TV exact-final physical PASS? | No: NOT_RUN_NO_KK_PRODUCT. |
| 96 | iPad status exactly deferred? | `IPADOS_PHYSICAL_DEFERRED_LEGACY_COMPATIBILITY_POLICY`. |
| 97 | Full trim? | No K-K product. Accepted build 41 full-trim evidence retained. |
| 98 | Full AOT? | No K-K product. Accepted build 41 full-AOT evidence retained. |
| 99 | UseInterpreter=false? | No K-K product. Accepted build 41 UseInterpreter=false evidence retained. |
| 100 | JIT absent? | No K-K product. Accepted build 41 JIT-free evidence retained. |
| 101 | Forbidden surfaces absent? | No K-K device surface introduced; runtime and native code unchanged. Full new-product scan is not applicable. |
| 102 | Three closures identical? | No complete K-K closures were generated. Three independent host analyses produced identical six-file evidence; this is a narrower diagnostic check. |
| 103 | Shared closure SHA? | K-K: none. Retained K-J closure `21c6480c3a852322b7a4efc54e58c447561f455abb54f2e67c1f8d885869247f`. |
| 104 | Clean clone? | Final-SHA recursive-clone result, independently reacquired input receipts, host reproductions and removal are bound in the separate final handoff. Complete real-slice closure/product reproduction is blocked. |
| 105 | Privacy? | Only source scripts, public metadata/hashes and documentation tracked. No map, DLL, ZIP, bank, save, product, device/signing ID or private path tracked. |
| 106 | Protected refs? | All specified refs verified unchanged; final remote readback is recorded in the completion index. K-K feature push only. |
| 107 | Zero Actions? | No Actions dispatch; branch run listing is empty. Final readback is recorded in the completion index. |
| 108 | Disk outcome? | 8.770 GiB recorded at start; 15.172 GiB after bounded cleanup. No AOT checkpoints apply. Final/clone-removal measurements are recorded in the handoff. |
| 109 | Development integration-ready? | No: YELLOW_NEW_AUTOTILER_COMPATIBILITY_MECHANISM. |
| 110 | All-platform release-ready? | No. |
| 111 | Exact fast-forward SHA? | None recommended for integration. The diagnostic feature commit is not a release/development GREEN fast-forward candidate. |
| 112 | Is this the first accepted physically running unchanged Strawberry Jam slice on Apple? | No; no unchanged real SJ slice is physically accepted by K-K. |
| 113 | Is full Beginner Strawberry Jam support established? | No. |
| 114 | Is full Strawberry Jam support established? | No. |
| 115 | Must 113/114 remain NO unless separately proven? | Yes, both remain NO until separately proven. |
| 116 | Is K-L expansion audit the correct next stage? | Not yet. First close and accept the bounded Everest custom-autotiler mechanism, then retry unchanged lobby/Bing. Expansion follows a successful first slice. |
| 117 | Should K-L use Astra Ultra? | Astra Ultra may suit a later explicitly requested parallel expansion audit; no expansion stage is started here. |
| 118 | How many remaining Beginner maps appear zero/near-zero incremental distance? | Not audited; no count claimed while the first slice is blocked. |
| 119 | Is a 3–5 map expansion rational? | Premature until the first unchanged slice passes and a bounded expansion audit ranks candidates. |
| 120 | Is full 128-map Strawberry Jam rational yet? | No evidence supports a 128-map expansion. |
