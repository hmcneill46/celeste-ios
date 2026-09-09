# Stage 25K-M — Strawberry Jam Beginner expansion audit

**AUDIT_GREEN**: complete reproducible census, conservative four-dimensional screening, and an evidence-backed next decision. This is not new gameplay acceptance. Recommend one additional unchanged map, **The Squeeze**, SID `StrawberryJam2021/1-Beginner/snas`. Whole-Beginner support should not be the next implementation target.

Starting revision: `b4997fa49f4f8b82f6f2984fe9047396e401ea48`, independently verified as origin/tvos-port before creating `feature/apple-everest-sj-beginner-expansion-audit`. This report belongs to the new audit commit; its final SHA, clean-checkout reproduction and remote delivery are recorded in the outside-tree final handoff. No integration is performed here. Reviewed HOST-C and M1 evidence remain historical external evidence, not local reruns.

The physically accepted game/content/semantic baseline remains `be8546d4ae411cb491a0c3bbc4e5343ebf9c9651`; HOST-B tooling is `632a451da2f62928404a2408826747aa6fc55b20`. The audit adds tooling, a host-only probe, synthetic tests and sanitized evidence. The only edit to an existing file excludes the new probe subdirectory from the parent test project compile glob. Existing production code, selected maps, profiles, semantic lowerings, native locks, compiler settings, dependency pins and version authority are unchanged.

## Input and baseline authorities

StrawberryJam2021 1.0.12 ZIP SHA-256: `4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655`. Root DLL SHA-256: `8d5b9184204e7e7728965bcf95af5f150f6220dafbfe52fdd6c23e50d47e5258`. Every regeneration rehashes the 24 accepted public package ZIPs and distributed DLLs, Chrono 1.3.3, six retained production DLLs, their exact HOST-C production-preflight receipt, 18 regression BINs and 162 source/acceptance authorities. No live/latest resolution is used.

Two additional exact historical packages were independently acquired only for candidate source/provider audit: CommunalHelper 1.25.5 and EeveeHelper 1.12.5. They are **not accepted product dependencies**. Their ZIP/DLL identities are bound separately in the input ledger. Other candidate-only providers without independently validated inputs remain unknown. A historical provider alias or a DLL string is not production registration or semantic proof.

| Package | Version | ZIP SHA-256 |
|---|---|---|
| BrokemiaHelper | 1.8.5 | `c80d7d71cdccef7c9b418e05d53debd197b59717d3cdeade72910ace33d4fbeb` |
| CherryHelper | 1.8.2 | `8ca640f9e14f844507e0bbedc2afcb99ef61bec6957a7558cee90e9fabd2fba7` |
| CollabUtils2 | 1.13.4 | `4bcea8a9011edb8b7d27b433c47f1f4a871b8f7a4dd7f2bffac67bb99c7f6ad5` |
| ContortHelper | 1.5.5 | `d8b42128a808e68d30329baa9299fdb41bf7d24743635dec3137c36bcc87956a` |
| CrystallineHelper | 1.17.2 | `4573f5e45dce0905142cd2b119f4a9a744bdce8ef3199319d1e46b6d1d342747` |
| DJMapHelper | 1.13.4 | `95ab02d657213031b70c3079738b21be399e567ca8a32b1fe8e8effc0778eedb` |
| ExtendedVariantMode | 0.50.5 | `4019b362d9ad1b2d3a6a670f833ef6324d735d5791a0bf21b62cf5cfbc0d639d` |
| FancyTileEntities | 1.6.2 | `c37806dc7e7db4a392c8ab79030701f4cf2046109cc19435b607fca7fe41c8d8` |
| FemtoHelper | 1.15.22 | `5a845805bae30490ed7c9da84410fbe672628b8fe927a830f5f2eb148eb88419` |
| FlaglinesAndSuch | 1.6.80 | `fb0fd300f95d77539eb9079aed445c81f263557c1a1715187bd509d39c1eb794` |
| FrostHelper | 1.80.1 | `ba6aee8f596eff0f595fd90cb7f136ea13b1e9c2d664fc38f389608b0f9e2c81` |
| HonlyHelper | 1.7.5 | `6a2d0f04a5be3a9c9c3bf66ec7e93701398a64d5a0e72add5df682e860e7d08d` |
| JungleHelper | 1.4.10 | `a140e21cbb5fd2dcaac70d4d5e36d49476e414861455ae25dedc0678164406cc` |
| LunaticHelper | 1.1.1 | `b10e044b1dfa412605bee3ba6bfdd4263591099d331d6c1aab1e9ba65bf6a3e5` |
| MaxHelpingHand | 1.40.9 | `abfc5d167936410e9b6665018335ee654455e30710caffe01540b232da468fee` |
| PandorasBox | 1.0.49 | `25f9c7d6792983ac697e3780667fb0963233c0c08f6e528f5c6d9fe732cded05` |
| StrawberryJam2021 | 1.0.12 | `4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655` |
| StrawberryJam2021Assets | 1.0.1 | `26fab85f20fff89d1adcba2ef926c3447d9996057c7dfc78e5d97f9d96ed7e45` |
| StrawberryJam2021AudioA | 1.0.4 | `81e9cbc39b3a5525c93dfc5b24b675a833a8fb616e8a01cfc0838296f5e37e1d` |
| StrawberryJam2021AudioB | 1.0.0 | `70b90f45709956a4d18bfbb5941836c534344a1cf0ab859a50430daa3b76ef42` |
| VivHelper | 1.14.10 | `9a95f51659ccfcb78404f9610307918d6114fc824298fbb8493651ec7c80215a` |
| VortexHelper | 1.2.19 | `b6280fe2e3c05d355c32a51049854137c41be72d24ec4aec9d997cc7c4394db2` |
| XaphanHelper | 1.0.79 | `ca868d06eb05f0c5de55126090e5019210e9d83c3dae9394cbcd155acf8eb16f` |
| YetAnotherHelper | 1.2.5 | `73d64e1b3457e2461d3368a01de9f5f31a58bf24e130f3e35ec7280b24814c2d` |
| ChronoHelper | 1.3.3 | `af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18` |
| CommunalHelper | 1.25.5 | `44f4fb0b277a4900fd2a555e1a73e661140aa7b2455d3c7776cf420193349e3c` |
| EeveeHelper | 1.12.5 | `de8eb463083e7298827d6c1ba4f5a4b69cc6cdd9b4be31fec78e272fce3a3839` |

The exact distributed DLL paths, byte sizes and hashes are in `apple-everest/sj-beginner-expansion-inputs-stage25km.json`. The fresh Mono.Cecil provider census covers 26 DLL entries, including all three pinned Chrono distributed variants. Owned regression IDs bind to the unchanged static producer source. Every authored provider proof remains separate from the actual compiled selector/profile observation.

| Accepted logical authority | Unchanged SHA-256 |
|---|---|
| content | `3ee3129bf48f841786482f9bc88311bb58b3c8cbef3f4550d7a8fe2f1064b8ca` |
| managed | `906787e52b5d8bc84ab68195532678019beb77947cb7713486af7e1388cffb1f` |
| registry | `6e5b89f7d952aa98e72640abce0c75522567fb9d48f8b027e3db54f5cf9da72f` |
| shared | `11e5006c72dde385ca3b41773e3d924c29e7b19979aef96e11a5ad44f211d6b9` |

Baseline A remains 920/920; B and C remain 73/73; D remains accepted real composition. The current host probe reruns the actual production parser, selectors and guards for those 920 occurrences and the existing 73-factory compiled inspection. It does not re-execute gameplay or transfer old physical PASS to candidate maps.

## Complete discovered census

The exact package contains 128 SJ maps. The Beginner folder, 21 distinct authored lobby jars, original sticker sidecar and two special chapter-panel destinations agree: **21 ordinary Beginner maps including Bing**, hence **20 remaining ordinary maps**, plus **one heartside and one auxiliary gym**. There are 22 candidates and two baseline controls. Full source map inventory hashes are checked against the existing locked census; no duplicate or omitted SID is accepted.

All ordinary/heartside SIDs below use the exact prefix `StrawberryJam2021/1-Beginner/`; Lobby is `StrawberryJam2021/0-Lobbies/1-Beginner`, Gym is `StrawberryJam2021/0-Gyms/1-Beginner`. Source members are exactly `Maps/<SID>.bin`. The ledger stores full SIDs, byte counts, root/appendix lengths and hashes, source labels and metadata evidence. Original complete BINs and appendices remain private.

| Map suffix / special role | Bytes | Source SHA-256 |
|---|---:|---|
| Gym (auxiliary) | 71637 | `ac098e22996253308432f6de306aa26ff2522c881752522d2247cd0b89146304` |
| Lobby (control) | 674648 | `a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2` |
| Bing_Over_Google (control) | 135363 | `e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347` |
| Ceph | 134859 | `39be3444bca7b5d247d87ace69953081891b7860ac306ad70150879d92b27e8c` |
| Circumplex | 290474 | `106115ab0d696c404a2edcc78962c4ad184c2a095a28d29167a11dd192a9e987` |
| CoupCritik | 141392 | `9267d7495273ada8017c7fc9469c3cb3afc8dfd1995663f0504056a77253bf08` |
| Eclipse | 588914 | `aa7089d133c6df267ed8702a70402b3e8521bd62eefe4b373dd0fd144feb8591` |
| Flagpole1up | 290330 | `b1b6ab411e05a16eb4abf3b128297be445ef45fcfb4a8b2aebbfe21c7b580581` |
| HankyMueller | 135308 | `2a01683faa53203064a2912a81bb931171175ba8b418fe67856fa57ff8cf6965` |
| Jadeturtle | 141375 | `5487797ad5ea9742b0c61bba6ed127e1000c459da70fec76c030a9ec2ed774a4` |
| NotYourBadeline | 141736 | `d0e0928aa1e3a471a1e1c4ebf8d2c778d1ba528a768acddce39f82e768dae486` |
| Owen-Shirrell | 141627 | `45051295883dd37fe1c45ecb94c9819c869e1c3a8121e9fd2072b5a88d3ae1d0` |
| Quinnigan | 290478 | `130f297479f4b97a46f6398834ce127b3f0ef1374c5cca391209483ce0371398` |
| ZZ-HeartSide (heartside) | 1177812 | `8ed2f31c2de34d1942ea5c0ce0a4de2486706e695caf4bb66c2ffcead2f19e3f` |
| asteriskblue | 141452 | `8ddfe671414f42fd0e997c5b25718cd4c7227b6678ba4c403b3d5d0ebc97618b` |
| cellularAutomaton | 62100 | `2392d1e1068d68eebd6179bc0d046539868e13806a238cee00f780a766b83b18` |
| coffe | 289478 | `e52e08cd9c53f07e71fd4a07997cda7778b351cb8d3b9c5e7c529206270ce649` |
| frozenflygone | 141726 | `5881ba93d5867bde52f523769d39d32ecf94e052f32c92dfb61d8fc264db43a1` |
| hyperlife | 141638 | `cd1eb1fc18d396c0fd4fc445b526ca24c9e58fd502855ff2f51dd28cc2b8bb9a` |
| joltik | 140786 | `c285c6d6df22566ce5872088c36468f0c561c287a6552da98fdfc085b6ad9f66` |
| mosscairn | 1186233 | `725326a39186b82f98cdde398f26da52d57bce9a3fb004000d5133ef6fb39b9f` |
| skeleton | 135325 | `2deea6850e773debf81b13357d2808bb0760937957d0eb45f532ed7e51c6dfb1` |
| snas | 67718 | `6ad3172d496e8b5b4ce71f1128fe231b162d534419dc823d2af2cea27fc241d9` |
| voliver9 | 67564 | `e6485fc35bd6484d3fbea246a6a2e1403f3376695a79674067b6044d39581b6a` |

## Per-map four-dimensional screening

Counts below describe each map alone, not copied baseline counts. For each candidate the ledger also computes the full lobby+Bing+candidate+18-regression union and deduplicated ID/profile counts. A columns are accepted-provider / new validated-provider / blocked-provider / unknown-provider occurrences. B columns are exact guard accepted / authored profile rejected / actual selector missing / registered but outside the selected proof. C columns are existing exact-profile closure / unimplemented / unknown. Every candidate has blocked B and D; no candidate has zero incremental readiness.

| Rank | Candidate | Occurrences / IDs / profiles | A accepted/new/blocked/unknown | B accepted/rejected/missing/unreviewed | C existing/blocked/unknown | D blocked / unknown requirements |
|---:|---|---|---|---|---|---|
| 1 | snas | 53/12/32 | 52/1/0/0 | 1/26/24/2 | 1/24/28 | 1/5 |
| 2 | cellularAutomaton | 516/11/44 | 516/0/0/0 | 443/50/13/10 | 443/13/60 | 1/6 |
| 3 | voliver9 | 28/16/27 | 27/1/0/0 | 2/16/4/6 | 2/4/22 | 1/5 |
| 4 | coffe | 1716/17/188 | 1716/0/0/0 | 11/1625/79/1 | 11/79/1626 | 2/6 |
| 5 | joltik | 746/15/85 | 746/0/0/0 | 2/536/207/1 | 2/207/537 | 1/6 |
| 6 | NotYourBadeline | 518/11/101 | 518/0/0/0 | 342/62/114/0 | 342/114/62 | 1/7 |
| 7 | hyperlife | 83/16/67 | 83/0/0/0 | 3/44/36/0 | 3/36/44 | 2/6 |
| 8 | asteriskblue | 1254/11/42 | 1229/25/0/0 | 1/1227/26/0 | 1/26/1227 | 1/5 |
| 9 | CoupCritik | 200/14/142 | 200/0/0/0 | 1/24/65/110 | 1/65/134 | 1/6 |
| 10 | Ceph | 946/19/120 | 914/32/0/0 | 5/873/67/1 | 5/67/874 | 2/8 |
| 11 | Circumplex | 3159/27/157 | 3159/0/0/0 | 3/2937/159/60 | 3/159/2997 | 1/5 |
| 12 | Quinnigan | 2126/18/61 | 2126/0/0/0 | 806/1252/67/1 | 806/67/1253 | 1/6 |
| 13 | Flagpole1up | 292/13/184 | 292/0/0/0 | 1/48/242/1 | 1/242/49 | 1/5 |
| 14 | Jadeturtle | 1113/21/138 | 1112/0/0/1 | 16/849/235/13 | 16/235/862 | 1/5 |
| 15 | skeleton | 933/33/317 | 883/2/0/48 | 1/802/128/2 | 1/128/804 | 1/7 |
| 16 | HankyMueller | 366/33/125 | 349/13/0/4 | 1/67/296/2 | 1/296/69 | 1/4 |
| 17 | Owen-Shirrell | 1071/25/208 | 1066/4/0/1 | 1/897/99/74 | 1/98/972 | 2/7 |
| 18 | Eclipse | 5269/24/109 | 5218/43/0/8 | 2/5200/67/0 | 2/67/5200 | 1/4 |
| 19 | frozenflygone | 209/22/133 | 129/0/0/80 | 2/6/190/11 | 2/190/17 | 2/9 |
| 20 | Gym | 612/28/145 | 570/42/0/0 | 0/411/172/29 | 0/172/440 | 3/6 |
| 21 | ZZ-HeartSide | 5057/134/922 | 4998/34/0/25 | 261/4086/629/81 | 261/627/4169 | 1/12 |
| 22 | mosscairn | 3051/87/662 | 2976/32/8/35 | 3/2171/830/47 | 3/830/2218 | 1/11 |

The controls independently yield lobby 856/856 and Bing 64/64 exact-profile acceptance. Across 24 SJ maps and all 18 regressions, 30,574 custom occurrences receive actual compiled observations: 3,158 exact profiles, 23,209 rejected profiles, 3,749 missing registrations and 458 registered/unreviewed. All 3,158 accepted occurrences reject an injected unsupported attribute. No entity constructor, game lifecycle, draw path or gameplay is executed by this probe. C reuses only existing exact-profile closure evidence; all new lifecycle/hooks/reflection/content/constructor dependencies remain blocked or unknown.

The older K-J inspector covers 312 occurrences in 13 regression maps. This audit independently parses **all 18** retained BINs: 336 occurrences, including 24 legacy occurrences. Of these, 330 pass selected guards; six actual registrations are outside the selected 73-profile proof: Chrono pinata, two DJ frozen-IL controls and three owned KE/KF canaries. These retain their historical acceptance evidence but are explicitly unreviewed within this audit proof scope. This is not a newly observed baseline regression. The union includes all 336, rather than silently omitting the older five maps.

Unknown provider evidence includes unvalidated Gravity/Adventure/Memorial/SafeRespawn/Shroom/Sorbet/DisposableTheo/Sardine/Lua requirements. Four historical ID aliases have no fresh exact provider binding in the supplied pinned evidence: SJ cassette-pellet left/up, SJ time-freeze music controller and Max ambience-volume trigger. Unprefixed `ForceVariantTrigger` appears once in Owen-Shirrell and twice in heartside; it is not silently classified as vanilla. All unknowns remain explicit.

## Incremental ranking and shared work

The ranking is a screening order with explicit unknowns, not a complete cost estimate. The top decision has stronger source review than lower-ranked closures. No 3–5-map cluster is sufficiently bounded by the current evidence; recommend snas alone. The complete per-ID shared ledger deduplicates profiles and affected maps, without treating a shared fix as a whole-map unlock.

| Candidate | Incremental finding |
|---|---|
| snas | AUTO_IMPLEMENT_BOUNDED_COMPATIBILITY: Finite bubble return and random-sound trigger; genuine flag-switch/gate semantics and static SID/mode metadata; exact camera/platform/MiniHeart profiles; resolved AudioB events. Recommended one-map scope. |
| cellularAutomaton | AUTO_IMPLEMENT_BOUNDED_COMPATIBILITY: Two absent factories: DashZipMover and drowning controller. Frozen DJ killBox exists but needs selected proof. Attached Frost spinners; Player.Render drowning hook; unresolved authored music/custom mover SFX; 443 currently accepted occurrences do not remove these blockers. |
| voliver9 | UNKNOWN: Few occurrences conceal Eevee FlagGateContainer container lifecycle, upside-down platforms, CustomNote UI and flag persistence; source-reviewed dependencies exceed snas. Core mode and triggertrigger profiles need proof. |
| coffe | UNKNOWN: 74 LoopBlocks plus golden-berry respawn, flag killbox, custom moon creatures; custom spiral wipe, stale start a-01 vs c-01, sprite/core-mode lifecycle. |
| joltik | UNKNOWN: 200 FloatyBgTiles, multicolor spinner controller, northern lights, upside-down platform and golden respawn; tile motion/graphics and exact profiles remain unproved. |
| NotYourBadeline | UNKNOWN: Blackout/wall/controller, lamp flags and82 conditional textboxes; portraits metadata, dialog and upside-down platforms. Same-ID reuse is insufficient. |
| hyperlife | UNKNOWN: Intro-crusher families, multiroom berry/seed lifecycle, NPC/dialog, CustomNote, spinner controller, unsupported Rocks wipe and unresolved authored music. |
| asteriskblue | UNKNOWN: DreamRefill is DreamTunnelRefill: new player dash state, base-field access, particles and Communal custom audio; 1155 attached spinners. Not an ordinary refill substitution. |
| CoupCritik | UNKNOWN: Extra jumps, flag gates, inventory changes, conditional blocks/dialog; background G is not foreground sound-table proof. |
| Ceph | UNKNOWN: Cassette zip movers, tempo/variant/music state; CassetteModifier BeatsMax576, custom cassette song and custom wipe. |
| Circumplex | UNKNOWN: Blue booster, flag gates, no-refill springs, upside-down platforms, fake-wall state and large new profile set. |
| Quinnigan | UNKNOWN: Triple-boost flower, custom springs, holdable barriers, entity muting and flag gates; large exact-profile delta. |
| Flagpole1up | UNKNOWN: Color switches/blocks, clear pipes, door fields and custom bird tutorials; interacting movement/transport composition. |
| Jadeturtle | UNKNOWN: Disposable/respawning Theo, killboxes/barriers, clear pipes, entity muting and flag gates; lifecycle interactions. |
| skeleton | UNKNOWN: Linked zip movers, connected/custom dream blocks, dream-dash controller, swap-block toggles and return berries. |
| HankyMueller | UNKNOWN: Time crystals/freeze, refill/hint mechanics, connected blocks/containers, lighting and book/dialog UI; many unproved provider closures. |
| Owen-Shirrell | UNKNOWN: Dash/variant/hint systems, bubble, flag gates, Kevin barrier/UI and custom wipe. One unprefixed ForceVariantTrigger has no proven provider; kept unknown. |
| Eclipse | UNKNOWN: Connected dream blocks, time modulation, safe-respawn crumble, spinner/barrier controllers and star-climb presentation; unaccepted providers. |
| frozenflygone | UNKNOWN: Gravity/spawn inversion, upside-down watchtower, room wrapping/teleport, variants and custom Spirialis wipe. Selected static hook closure unproved; gravity is not declared impossible. |
| Gym | UNKNOWN: Auxiliary gym, not an ordinary chapter: teaching UI, Theo/dream barriers, teleports/global flags, custom wipe, Interlude=true unsupported by current static area construction; prologue destination remains excluded. |
| ZZ-HeartSide | UNKNOWN: Special heartside is a broad Beginner union plus gravity/teleport/cassette/checkpoint progression, layered complete screen and postcard audio. Two unprefixed ForceVariantTrigger occurrences remain unknown. |
| mosscairn | STOP_MAJOR_ARCHITECTURE: Eight Lua-trigger occurrences/seven script paths require forbidden generic Lua/import behavior or a separately proved static per-script lowering. Six scripts found; baddyfollow unresolved. Gravity, paint/cassette and inventory/skip cleanup compound the work. |

Shared flag-switch/gate semantics affect eight candidates. Sideways one-way platforms also affect eight; exact camera/profile families and MiniHeart recur widely. Frost attached-spinner behavior is a bounded shared requirement, but 1,155 attached spinners in asteriskblue and 28 in cellularAutomaton do not share acceptance with the current unattached profile. The production numeric parser proves a small number of profiles that raw textual hashes alone would miss; actual guard results, rather than constructor names or hash-only matching, determine B.

Mosscairn is STOP_MAJOR_ARCHITECTURE for the generic Lua/import path: eight trigger occurrences reference seven script paths, six found and `baddyfollow` unresolved. Finite per-script static lowerings are unproved, not declared impossible. Gravity alone is not declared impossible; its selected hook/type/lifecycle closure is unknown. Neither finding blocks auditing other maps.

## Recommended combined union and next implementation boundary

Proposed SJ selection: unchanged Beginner lobby + Bing + **snas**, plus all 18 existing regression maps: 21 total maps, 1,309 custom occurrences, 83 distinct IDs and 604 raw authored profiles. A: 1,308 accepted-provider occurrences and one exact new Communal provider requirement. B: 1,251 exact-profile observations, 26 rejected profiles, 24 missing registrations and eight unreviewed registrations (two snas plus six legacy controls). C: 1,251 existing-profile occurrences, 24 unimplemented and 34 unknown within this proof scope. D remains blocked with explicit real-map composition unknowns. None of these are implementation PASS counts.

Snas has eight rooms including filler, six rooms with player spawns, 53 custom occurrences, 12 IDs and 32 raw authored profiles. Its 67,718-byte original BIN contains a 14,677-byte appendix, SHA-256 `f13ab43805a004226d5c7eb82a42a028884be222f988198ce7bcf14708b28a04`. Preserve its source label “The Squeeze”, initial room `1`, original spawn and terrain seed 2989. It has no custom wipe or authored outbound destination. The existing texture cache selects no additional explicit PNGs for its six cached textures; this is only a lower bound and does not prove implicit assets, decals or rendered terrain.

- Original lobby+Bing+snas BINs and all18 accepted regression maps; unchanged appendices/source labels and map hashes
- Finite snas RandomSound/bubble implementations; real flag switch/gate behavior and generated SID/mode/group metadata; exact rejected profile closures
- Close lifecycle and type/reflection branches against all selected/regression entity types; preserve ordinal grouping and saved session flags
- Add original snas FG graphics binding; prove existing cached textures, implicit assets, decals, core terrain and restoration; no cache-list-as-closure shortcut
- Add one AudioB snas bank and exact GUID authority through existing single-system pipeline; preserve existing bank relative order and deterministic fresh GUID/event duplicate checks
- Publish exactly snas availability; preserve original21-heart threshold; all other currently excluded ordinary/special/auxiliary destinations remain excluded (counts computed from actual census).
- Observe panel, fresh start, resume, Return to Lobby, completion/journal/sticker/silver state across selected maps and separate saves
- Pinned processor defaults legacy=true/group persistent=true; switch422 persistent=true, gate423 false; main session flag restores gate after reload. No moving/dream switch or seeker/puffer authored in21-map union; future closed-creation guards required.
- Contort exact list parser:12 oneUse and11 repeat sounds, empty flags, persistent=false; preserve Next(1), zero-delay yield and removal/re-entry timing.

Source review establishes the following bounded work, without implementing it: Communal PlayerBubbleRegion uses canonical cassette flight with two nodes and dead/state-21 guards; Contort RandomSound requires exact empty-list parsing, Next(1), zero-delay coroutine yield, one-use/re-entry timing and persistent=false semantics. The current flag-switch/gate selectors exist but use canonical fallbacks that do not prove the authored flag behavior. Real typed semantics and static map-processor group data are required.

The exact snas flag group is room `3`, flag `flag_snasberry_switch`, switch ID422 persistent=true and gate ID423 persistent=false. Pinned processor defaults are legacyMode=true and groupPersistence=true. Preserve main/per-switch session-flag behavior and gate restoration after reload. The 21-map union contains no authored moving/dream switches/gates, seekers or puffers, and the relevant compiled companion selectors are absent. Future closed-creation guards must prove runtime exclusion; authored absence alone is insufficient. Retain one FMOD Studio system and separate vanilla/module/AEVPSV1 persistence authorities.

Composition work includes original FG XML/atlas/decal/implicit-asset binding, source label and SID, chapter title/credits/input, correct initial/default spawn, graphics/terrain restoration, availability, save/resume/Return to Lobby, completion/journal/sticker/silver state, bank order and regression traversal. Candidate-wide findings include unsupported custom wipes, gym Interlude=true, Ceph cassette BeatsMax576, heartside completion/postcard metadata, portraits and stale coffe StartLevel. These remain independent D blockers/unknowns even with complete factory coverage.

Exactly one expected bank addition: AudioB 1.0.0 `Audio/sj21_snas.bank`, 3,320,960 bytes, SHA-256 `08c980621201485026849c90e41b275c442448527d3c98096adeb73d562a289a`. Companion GUID file: 82,696 bytes, SHA-256 `4b8dfb05ba4d790b6acf0a86904cd0e28b7fefd3e9cdcd8a53050ac29cd74039`. Three events (`event:/sj21_snas`, `_flourish`, `_special`) have exact GUID bytes embedded in the bank. Global GUID text alone cannot supply this evidence. No playback is performed. Expected order: vanilla1–7, Chrono8, Bing9, shared10, snas11, lobby12, jamjars13, Collab14, Honly15; preserve the old relative order and reverify the actual future registry/payload.

With snas added, 125 SJ maps remain excluded. Two of 23 lobby destinations become available (Bing and snas), 21 remain excluded, and the original 21-heart threshold remains unchanged. Gym/heartside stay excluded. Existing sticker metadata for a map is not evidence of playable availability.

Required subsequent implementation/physical observations:

- iPhone and Apple TV: lobby jar interaction, original title/credits, correct first spawn, complete unchanged snas route and ordinary/silver berries
- Repeat death/retry, one-way platform collision, two-node bubble flight, oneUse/repeat audio triggers and exact random scheduling
- Flag switch/gate activate, leave/reenter, die/retry, suspend/resume, save/relaunch; confirm persistent switch and restored nonpersistent gate behavior
- Lobby→snas→lobby→Bing→regression traversal: graphics/audio/controller/session restoration and no cross-map flag contamination
- Return to captured jar route; chapter completion, MiniHeart/journal/sticker updates; two numbered saves, A/B fallback and absent-map quarantine
- All unselected jars/gym/heartside stay excluded; original21-heart threshold unchanged; no old PASS transferred
- iPad physical remains deferred under IPADOS_PHYSICAL_DEFERRED_LEGACY_COMPATIBILITY_POLICY; modern device quality retained

## Reproduction, regression and privacy

The deterministic sanitized result is `apple-everest/sj-beginner-expansion-stage25km.json`, SHA-256 `e6f94afeb56eae73a7e8971996b95cc44b0cbf16b56f0bae17abc0dca7483e35`. Three independent new output roots regenerate identical bytes, with fresh exact-input validation, DLL census and actual compiled guard observations. Observation times and private paths are absent from deterministic data. The reviewed decision file is a source-review authority, not generated gameplay proof; its hash and exact source/package bindings are rechecked. A separate clean checkout at the final audit commit must regenerate and compare the complete result, rather than copy an old PASS.

Regression results: **63 Python tests PASS**, including retained HOST-B/HOST-C controls and 21 audit controls; **622 existing builder deterministic tests PASS**, including the 73-factory runtime profile fixtures. The real compiled wrong-provider negative fails with no output. Synthetic controls cover omitted/duplicate maps, wrong package/DLL/map/provider hashes, unsupported profiles, unknown or missing evidence, unsafe archive paths, unprefixed unknown IDs, GUID text without bank evidence, missing candidate assessment and inconsistent candidate/union/recommendation readiness. Signed/AOT strings in the old Python synthetic test logs are mocked tests, not signing or AOT operations.

Reproduce using exact retained inputs and fresh owned SDK/cache/output locations. The host-only probe uses pinned SDK10.0.302 with a locked net10 project reference to the existing net8 builder, Mono.Cecil0.11.6 and YamlDotNet16.1.3. The old test project uses pinned SDK8.0.424. Use process-local DEVELOPER_DIR for Xcode26.6/17F113 and the existing pinned host tools; no toolchain installation or global selection change is needed. Prevent SDK first-use certificate generation with DOTNET_GENERATE_ASPNET_CERTIFICATE=false. An early unsuccessful draft emitted the SDK HTTPS-development-certificate first-use message; no Apple signing, credential access or trust command was performed, and later runs suppress generation.

```sh
export DEVELOPER_DIR=/Applications/Xcode-26.6.app/Contents/Developer
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_CLI_TELEMETRY_OPTOUT=1
export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0
export UseSharedCompilation=false PYTHONDONTWRITEBYTECODE=1
# Set task-owned DOTNET_ROOT, DOTNET_CLI_HOME and NUGET_PACKAGES.
python3 scripts/audit-apple-everest-stage25km.py \
  --package-root "$PUBLIC_PACKAGES" --candidate-package-root "$AUDIT_ONLY_PACKAGES" \
  --chrono-package "$CHRONO_ZIP" --production-runtime "$VALIDATED_PRODUCTION_RUNTIME" \
  --production-receipt "$VALIDATED_HOST_C_PREFLIGHT_RECEIPT" \
  --accepted-closure "$VALIDATED_HOST_C_CLOSURE" \
  --dotnet "$DOTNET_ROOT/dotnet" --output "$NEW_PRIVATE_OUTPUT"
python3 scripts/verify-apple-everest-stage25km.py --result "$NEW_PRIVATE_OUTPUT/result.json"
python3 -m unittest discover -s tests -v
```

Use three different new output roots. For clean reproduction, check out the final feature SHA with pinned recursive submodules, retain the tracked ledger only as expected comparison, and freshly run the audit to a fourth new root. Validated public inputs, retained exact production control DLLs and source/download caches may be reused; generated candidate profiles, DLL census or previous PASS receipts may not substitute for regeneration. No production content expansion is generated.

All original HOST-A/B/C and vanilla checkouts, native/app outputs and reports are preserved. Raw maps, DLLs, decompiled source, bank data, host probes, receipts and logs stay ignored or outside Git. Only owned code/tests and sanitized public identifiers/hashes/counts/classifications are tracked. The outside-tree handoff records the before/after preservation and remote checks.

Protected refs remain: ios-v0.1.1-rc.1 peeled `27e16b4724d94d3991b99c4795f680fcb0e5830c`; v1.0.0-rc.1 peeled `ee52b0868df091746f134d95d4f020f94f23d4fb`; v1.0.0-rc.2 peeled `641e86e4ed164cdf93f602ce2f11436449654d6e`; release/v1.0.0-rc.3 `c8134c8ca7924cf12f48527e714b5242c6024927`; v1.0.0-rc.3 tag absent. tvos-port stays at b4997fa4. Only the audit feature is eligible for push after privacy/workflow checks; no merge, upstream push, PR, tag or release.

No app/device AOT, native rebuild/stress, signing, installation, physical/iPad testing, simulator management, worker tuning or GitHub Actions work. HOST-A historical HOST_YELLOW stays unchanged; HOST-C qualification is not repeated. Retain IPADOS_PHYSICAL_DEFERRED_LEGACY_COMPATIBILITY_POLICY and modern-device quality. The M1 remains signing/install fallback.

**Next action:** review this K-M audit feature, then authorize a separate implementation stage for the unchanged snas-only union and its finite compatibility/composition requirements. Do not merge or implement automatically.
