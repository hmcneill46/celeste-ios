# Stage 25K-N — unchanged The Squeeze

This candidate adds only `StrawberryJam2021/1-Beginner/snas` to the accepted
Beginner lobby and Bing selection. It starts at K-M revision
`b65bedd20016dc3482d7702d7f0a9707bc2b1479`, retains HOST-B/HOST-C tooling, and
advances the canonical Apple product identity to 0.1.1 (47). The accepted
build-46 game baseline remains `be8546d4ae411cb491a0c3bbc4e5343ebf9c9651`.
This is a new implementation/tooling revision, not an unchanged build-46 app.

This tracked document describes the candidate and its reproducible checks.
It does not certify a signed product or physical gameplay. Exact final source,
product hashes, signing/install outcomes and user observations belong in the
private outside-tree `STAGE25KN_FINAL_REPORT.md` and product receipts. A clean
commit is required before final AOT; both final products must use that same
revision. Subsequent source changes require fresh products and acceptance.

## Inputs and selection

| Input | Exact authority |
| --- | --- |
| StrawberryJam2021 | 1.0.12; ZIP `4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655` |
| Root DLL | `8d5b9184204e7e7728965bcf95af5f150f6220dafbfe52fdd6c23e50d47e5258` |
| Snas BIN | 67,718 bytes; `6ad3172d496e8b5b4ce71f1128fe231b162d534419dc823d2af2cea27fc241d9` |
| Snas appendix | 14,677 bytes; `f13ab43805a004226d5c7eb82a42a028884be222f988198ce7bcf14708b28a04` |
| CommunalHelper | 1.25.5; ZIP `44f4fb0b277a4900fd2a555e1a73e661140aa7b2455d3c7776cf420193349e3c` |
| CommunalHelper DLL | `4011b959ed4e9cc4cb98bf43f6884ae81361fecc994787640553205029c94a8a` |
| Added AudioB bank | `Audio/sj21_snas.bank`; 3,320,960 bytes; `08c980621201485026849c90e41b275c442448527d3c98096adeb73d562a289a` |
| Bank GUID companion | 82,696 bytes; `4b8dfb05ba4d790b6acf0a86904cd0e28b7fefd3e9cdcd8a53050ac29cd74039` |

The immutable K-M input ledger supplies every other exact provider/DLL pin.
CommunalHelper's selected finite bubble closure is new compatibility; its whole
helper backend is not imported. EeveeHelper is not an implementation input.
No live/latest dependency resolver is used.

The source label is **The Squeeze**, initial room **1**, original default spawn
**(32, 104)** and terrain seed **2989**. The spawn is checked against canonical
selection of the original three spawns, not the first parsed spawn. Lobby and
Bing BIN hashes remain unchanged. The complete product contains three original
SJ maps and 18 existing regression maps. The other 125 original SJ maps stay
excluded; only Bing and snas are available among 23 lobby destinations. Gym,
heartside and the authored 21-heart threshold are unchanged.

Custom occurrences are regenerated from the original source: **973** across
the three SJ maps plus **336** regression occurrences, **1,309** total, **83**
distinct IDs and **604** raw authored profiles. Snas contributes 53 occurrences,
12 IDs and 32 raw profiles. Four IDs need new finite implementations, seven
extend exact existing profiles, and SilverBerry retains its accepted profile.
The implementation registration scope is 77 selected factories and six separate
legacy controls; the six do not acquire a selected73-profile lifecycle claim.

## Implementation and proof boundaries

The [issue ledger](../../../apple-everest/sj-snas-issues-stage25kn.json)
records the bounded behavior/composition work. The
[semantic obligations](../../../apple-everest/sj-snas-semantics-stage25kn.json)
bind all 83 factories to their exact profiles, source files and required fresh
proofs. They are review authorities, not copied PASS receipts.

- PlayerBubbleRegion invokes canonical cassette flight with exactly two nodes,
  dead/state-21 guards and original cleanup. It does not replace player states.
- RandomSound retains empty parsing/flags, `Next(1)`, the zero-delay coroutine
  yield, 12 oneUse and 11 repeat occurrences, event scheduling and queued removal.
- Typed flag switch/gate behavior replaces the old canonical fallback for this
  exact group. Switch 422 is persistent; gate 423 is nonpersistent. Main and
  per-switch flags, ordinal grouping, gate restoration and original tween timing
  are tested. Creation guards operate at enqueue and before Added, including
  delayed scene binding, without disabling unrelated canonical maps.
- Camera/platform/core/MiniHeart extensions execute actual constructors and
  lifecycle callbacks. A passing profile guard alone is not execution evidence.
- Full original terrain XML, atlas geometry, decals, implicit sprites, source
  labels, chapter metadata, default spawns and cross-map graphics restoration
  receive separate composition checks. Deferred texture loading is retained.

The exact snas bank is loaded at ordinal 11: vanilla 1–7, Chrono 8, Bing 9,
shared 10, snas 11, lobby 12, jamjars 13, Collab collectibles 14, Honly 15.
The GUID authority binds `event:/sj21_snas`, `event:/sj21_snas_flourish` and
`event:/sj21_snas_special`; duplicate IDs/paths are rejected. There remains one
FMOD Studio system. Host event traces and bank-byte checks do not prove audible
playback; that remains part of physical acceptance.

All 20 existing progression descriptors remain byte-identical and snas adds
one compatibility identity. Vanilla, module and AEVPSV1 persistence authorities
remain separate. Investigation found that the physically accepted build-46
AEVPSV1 codec already writes envelope version 2 and accepts versions 1/2; its
source bytes are unchanged. K-N introduces no codec migration or downgrade.
The [progression reference](../../../apple-everest/sj-snas-progression-reference-stage25kn.json)
and [legacy reference](../../../apple-everest/sj-snas-legacy-reference-stage25kn.json)
identify exactly what is preserved, without transferring physical observations.

## Mandatory pre-AOT path

Use process-local `DEVELOPER_DIR=/Applications/Xcode-26.6.app/Contents/Developer`
with Xcode 26.6 / 17F113, SDKs 26.5, .NET 10.0.302, workload set 10.0.302.0 and
runtime/compiler packs 10.0.10. Keep the pinned .NET 8/9 host tools. The accepted
iOS/tvOS native identities and HOST-B normalization rules are unchanged.

Prepare canonical lawful inputs and validated native staging through the
existing supported scripts. Keep package/native/proprietary caches outside Git.
From a clean pinned recursive checkout of the exact candidate revision, run:

```sh
python3 scripts/build-apple-everest-stage25kn.py \
  --package-root "$PINNED_PACKAGES" --chrono-package "$PINNED_CHRONO_ZIP" \
  --work-root .build/apple-everest/stage25kn/reproduction-1 \
  --output artifacts/apple-everest/stage25kn/reproduction-1 \
  --platform ios --prepare-only
```

Use new `reproduction-2` and `reproduction-3` roots for independent repetitions.
Do not copy closures or receipts. Compare all identities in each generated
`real-composition/readiness.json`: managed, content, actual gameplay registry,
semantic, composition, progression, audio, collab and shared closure. The old
build-46 hashes remain historical controls; expanded K-N identities are frozen
separately after review and reproducibility.

Three independent fresh normal-wrapper preparations passed for this candidate.
They compiled new production assemblies and executed all four proofs separately;
their complete logical identities agreed. The
[K-N identity authority](../../../apple-everest/sj-snas-identities-stage25kn.json)
freezes these results and is required by preparation and final product checks:

| Identity | SHA-256 |
| --- | --- |
| Shared closure | `b6cd47670050c6d7be5bcab40c6a052d3113faa5c10e386d1693f5966753caad` |
| Managed logical | `e0dcff40a9b44b58a554cb061b0298cc00b76b73f6eaaf11383002318a6b0566` |
| Content logical | `92e9ce053b4957230e760933336f2de8910f2762c834aca6a95c35aa95418e80` |
| Gameplay factory registry | `9d7fa8c198cea379e9e330b81741c02b2e747e97050ab2ec078c5432ca13ff04` |
| Semantic logical | `3befc207260380688b6e8973c6545e5df1062ce08d50cb2fdd9284310d415efc` |
| Composition logical | `fda85caac4fe645cafdf850836e6432e0c55aa3dca848994dfa88485c4142cad` |

The authority also binds the progression, collab, audio-manifest and bank-set
identities. The gameplay registry above is distinct from the generic registry
field retained in the existing closure manifest. Both are verified. Complete
semantic and composition proof bodies are rehashed, so unchanged identity labels
cannot hide altered evidence. Missing manifest fields fail closed.

These three runs precede the final source commit. A separate recursive clean
checkout must regenerate and pass the same frozen identities at that commit;
neither these receipts nor earlier physical acceptance can replace that check.
The first preparation attempt is retained as a failure: it sent the unchanged
312-occurrence K-J canary subset to the exact 973-occurrence K-N contract, which
correctly rejected it. The caller now checks that subset with its historical
selected73 contract, while the full K-N contract remains mandatory and strict.

The common builder selects the K-N lane whenever snas, the K-N contract or its
package-root argument is present. It validates all 21 closure map bytes before
registration, so omitting the lobby cannot skip composition. Each execution
recreates package/profile bindings and the production closure, compiles fresh
managed/frozen IL, compares the complete implementation bytes using the existing
timestamp/MVID-only rule, and binds its receipt to current owned sources.

The expanded preflight creates fresh metadata/IL reference bindings, differential
random/bubble tests, actual compiled runtime probes, all regression checks,
source-bound composition/terrain probes and compiled omission/mutation controls.
No prior execution receipt can substitute for these runs. Its marker is
`READY_FOR_STAGE25KN_SNAS_PRODUCT_BUILD`, distinct from K-L's marker.

Host probes use original atlas geometry without GPU pixels, explicit window/
player fixtures and recorded/null audio boundaries. They separately identify
SilverBerry/DashBlock lifecycle evidence inherited from unchanged implementations,
terrain construction and native operations deferred to physical products.
They do not certify rendering, audible playback, a completed chapter or live
device save/relaunch.

Final linked checks require the 77 selected factories, each actual authored
guard, the six separate linked legacy entries/type closures, and their exact
native constructor/lifecycle definitions. The new typed implementations and
generated group guards require AOT definitions as well. Exact compiler receipts,
full/static LLVM arm64 AOT, actual device platforms, trimming, forbidden surfaces,
packaged assemblies/content and explicit signing semantics remain mandatory.

## Regression and physical acceptance

Portable host controls contain project-owned synthetic data only:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 scripts/test-apple-everest-stage25kn-controls.py
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests -p 'test_native_*.py' -v
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests -p 'test_apple_aot_host_verification.py' -v
PYTHONDONTWRITEBYTECODE=1 python3 scripts/regress-apple-everest-stage25kn.py \
  --work-root .build/apple-everest/stage25kn/host-regressions --require-clean
```

Retain builder, frozen-IL/hook, SaveManager pairing/protocol/continuity, soft
reload and input-profile regressions. Historical literal-count verifiers keep
their original scopes and authorities; they are not rewritten to describe K-N.
The precommit run passed all current suites, including 706 builder checks,
50 typed-hook checks, 83 K-N contract controls, 29 portable native-tooling tests
and 21 historical K-M audit tests. Each complete expanded preparation also ran
3,842 actual compiled runtime checks, 4,793 random/bubble differential assertions,
221,309 terrain checks, provider omissions and changed-implementation controls.
Counts describe their named host proof scopes, not physical gameplay coverage.

After review/commit and clean-checkout reproduction, build iOS then tvOS in new
roots using the same wrapper and explicit development signing configuration.
Keep `-m:1`, `BuildInParallel=false`, node reuse/server/shared compilation off,
full trimming, full/static LLVM device AOT and `UseInterpreter=false`. Do not
carry allocator stress settings or reuse compiled app objects.

Both exact installed products must pass real lobby navigation, full unchanged
snas completion, berries/MiniHeart, bubble/platform/death behavior, switch/gate
restore, sound/music, numbered save and full cold resume, Return to Lobby,
journal/sticker state, prior progression, snas→lobby→Bing regression, excluded
destinations and platform lifecycle/input checks. Disposable fixtures cover
slot isolation, A/B recovery, quarantine/re-addition and lineage. Human play
results must be explicitly identified as user observations of those products.

`READY_FOR_PHYSICAL_ACCEPTANCE` is not gameplay GREEN or integration approval.
GREEN requires both exact final products and the complete physical matrix:
`PASS — GREEN_IPHONE_TVOS / IPADOS_PHYSICAL_DEFERRED`.
The iPad policy remains `IPADOS_PHYSICAL_DEFERRED_LEGACY_COMPATIBILITY_POLICY`.
No quality reduction, additional map, worker tuning, Actions, release or automatic
integration is part of K-N. The user performs any eventual manual fast-forward.
