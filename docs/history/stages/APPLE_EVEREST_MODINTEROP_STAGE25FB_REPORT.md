# Stage 25F-B — bounded static MonoMod ModInterop compatibility

Status: **PASS — YELLOW**

Stage 25F-B proves that the common pinned `MonoMod.ModInterop` contract can be
analysed from ordinary precompiled mod DLLs and lowered into deterministic,
typed static-AOT plans. It does **not** claim the selected real pair is ready
for device use: their ModInterop graph is complete, but unrelated HookGen
targets in the same distributed DLLs remain outside the reviewed target
catalog. A bounded 25F-B2 is required before physical provider-to-consumer
invocation and integration.

## Baseline and immutable references

- Branch: `feature/apple-everest-static-modinterop`
- Starting commit: `9fd2a809cf20521be7a1403b78fc1d15bbcc1782`
- Feature implementation commit:
  `2238706c811ee6608aa4611bdf30451d4506d49c`
- Final acceptance commit: the branch-tip commit containing this report
- Everest: stable 1.6458.0 at
  `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00`
- MonoMod semantic reference:
  `dfc30a1506d37fb88a2c2be004f525205f46a24c`
- iOS recovery tag: `27e16b4724d94d3991b99c4795f680fcb0e5830c`
- tvOS RC1: `ee52b0868df091746f134d95d4f020f94f23d4fb`
- tvOS RC2: `641e86e4ed164cdf93f602ce2f11436449654d6e`
- Deferred RC3 branch:
  `c8134c8ca7924cf12f48527e714b5242c6024927`; RC3 remains untagged.

No release reference, integration branch or historical report was changed.
No GitHub Actions workflow was run.

The branch uses two primary coherent commits: `feat: add static MonoMod
ModInterop plans`, followed by `test: record Apple Everest ModInterop
acceptance`. A focused third correction adds the missing locked builder
restore before the desktop conformance runner's `--no-restore` invocation.

## Pinned behavior and desktop reference

The exact pinned implementation was treated as the semantic reference. Its
observable contract is:

- registration is idempotent per `Type` and process-local;
- export prefix is the assembly name unless the last observed type-level
  `ModExportName` replaces it;
- each public static method is exported under its unqualified and qualified
  names;
- imports are public static delegate fields;
- field `ModImportName` supplies the exact lookup name, otherwise type
  `ModImportName` prefixes the field name, otherwise the field name is used;
- candidates are tried in registration/method order until delegate-compatible;
- no match assigns null;
- every new type registration refreshes registered importers, so consumer-first
  and provider-first orders both work;
- duplicate registration does not change ordering or duplicate exports;
- registration has no normal unregister operation.

The tracked desktop reference runs the pinned manager and records 14 passing
cases covering providers A/B, importers C/D, qualified and unqualified names,
both load orders, late refresh, missing/signature-mismatched providers,
multiple providers and duplicate calls.

## Production architecture

`AppleEverestBuilder` transformer v6 uses Mono.Cecil on the Mac to:

1. locate calls to the exact `MonoMod.ModInterop.ModInteropManager.ModInterop`
   ABI;
2. resolve the literal `typeof(T)` registration argument;
3. inventory public static methods, delegate fields, naming attributes and
   exact signatures from binary metadata;
4. calculate compatible candidates in stable registration/method order;
5. emit one typed `GeneratedAppleEverestModInterop` source plan and explicit
   AOT roots;
6. hash the normalized plan into the shared closure identity.

On-device, the tiny `MonoMod.ModInterop` facade accepts a known `Type`, marks
its generated registration ordinal and refreshes direct delegate assignments.
Calls after binding are ordinary delegate calls. The original reflection-heavy
manager, runtime method/field enumeration and MethodInfo-based delegate
construction are not shipped. The broader MonoMod.Utils assembly is not kept
merely for this feature.

Unsupported runtime-selected types, external/dynamic registration types, open
generic registrations/delegates, readonly import fields and unsupported
delegate shapes fail before AOT under explicit `DEFERRED_…` diagnostics. Closed
generic delegates, ordinary `Action`/`Func`, custom delegates, value/reference
types and bounded `ref`/`out` shapes are generated statically. Required Everest
dependencies remain required; ModInterop does not satisfy dependency metadata.

## Real binary audit

Eighteen public release archives were inventoried. The machine-readable audit
is `apple-everest/modinterop-audit-stage25fb.json`; it records archive/DLL pins
and the first independent blocker. StaminaBar 1.1.2 is the cleanest standalone
positive: its optional ExtendedVariants import is statically supported and
correctly remains null without that provider. GameHelper and Picoline also
produce valid static plans, but retain IL/detour/broader MonoMod blockers.

The selected separate provider/consumer pair is:

### Provider — ConditionHelper 1.0.0

- Release: `https://gamebanana.com/mmdl/1081578`
- ZIP SHA-256:
  `cefd1f8264d4eba9ccd8abb324f88764b99c1fda7812cb6cbbb84cef05c6e8ed`
- DLL SHA-256:
  `f4471853b8e6c2abdd107609eeee2852076cde4c7e70667367b71a38d72a62b9`
- Source: `https://github.com/Brokemia/ConditionHelper`
- Source commit: `1d42c6a756b41ea7165a11891b91869252ed59dd`
- License: MIT
- Registration: `Celeste.Mod.ConditionHelper.ConditionHelperExports`
- Exports: `ConditionChanged(Action<string>)`,
  `WatchConditions(Func<string,Action,int>)`,
  `RemoveCallback(Action<int>)`, and
  `EvaluateConditionExpression(Func<string,bool>)`.

Every method is available under its unqualified name and the
`ConditionHelper.` assembly prefix.

### Consumer — AchievementHelper 1.0.5

- Release: `https://gamebanana.com/mmdl/1081965`
- ZIP SHA-256:
  `155b2ff92857e92f2c4510270e2c6d391b40510945fe3c6c31f9d8fa43a58177`
- DLL SHA-256:
  `5dd3ceadbd5c085c5b1c4ffebef9f7560c80c8a94f9dc35d4001f2b4443ba5b0`
- Source: `https://github.com/Brokemia/AchievementHelper`
- Source commit: `6f8bb7807b077195c149f2336be2fb166a0e483b`
- License: MIT
- Registrations:
  `Celeste.Mod.AchievementHelper.AchievementHelperExports` and
  `Celeste.Mod.AchievementHelper.ConditionHelperImports`.
- Its required metadata dependency is ConditionHelper 1.0.0.

All four explicit qualified imports bind to the four provider exports.
AchievementHelper also contributes three exports of its own. The complete pair
has three registrations, seven exports, four imports and four resolved imports.
Its deterministic plan SHA-256 is:

`9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318`

The ordinary release DLLs are authoritative and neither source tree is needed
to generate the plan. No mod-name-specific transformer was added.

## Why the result is YELLOW

ConditionHelper also uses roughly 28 uncatalogued `On.*` targets;
AchievementHelper uses five uncatalogued `OuiChapterSelect`/SaveData targets.
Accepting only their ModInterop portion would create a misleading partial
product. The builder therefore correctly reports `ON_HOOK_DEFERRED` before AOT.
There was no source edit, fuzzy bypass or false physical claim.

An explicit source-free production-build attempt using only the two pinned
release ZIPs stopped on that `ON_HOOK_DEFERRED` result and did not promote a
closure. This distinguishes the independent real-world HookGen blocker from a
ModInterop discovery or binding failure.

Consequently:

- real binary discovery and all four real bindings are proven;
- real consumer-to-provider invocation on iPhone/iPad/tvOS is **not tested**;
- no package containing this pair was installed;
- Stage 25F-B is not ready to fast-forward into `tvos-port` as a completed
  compatibility rung.

The exact next step is Stage 25F-B2: review and add the smallest closed HookGen
target catalog needed by this pair, rerun the same source-free plan, and then
perform full-AOT physical iPhone, iPadOS 15.8.8 and Apple TV acceptance. It does
not require a manual download; the exact public ZIPs are fetched and verified
by `scripts/fetch-apple-everest-stage25fb-fixtures.sh`.

## Deterministic and product regression evidence

- Pinned desktop ModInterop reference: **14 pass**.
- AppleEverestBuilder deterministic suite: **221 pass**.
- Complete Stage 25F-B verifier with fixtures, pair audit, shared closure and
  both packages: **214 pass**.
- Real pair binary analysis: **3 registrations / 7 exports / 4 imports / 4
  bindings**, exact plan hash above.
- Existing accepted shared closure: transformer v6, empty ModInterop plan
  `84f0267040cda9cc51d4dc08da367a4b934aa0e9f43cf3d553815e594f25899d`,
  shared closure SHA-256
  `d447f31a17d582f41537a14c44f0940f960e8cd40c4420b74711101976aa7537`.
  A second independent closure generation reproduced both hashes exactly.
- Existing-regression iOS Release IPA: 882,789,420 bytes, SHA-256
  `be3c0f8da3cadb323c9ce0ebbeada9785eb15f17f7aa2a8434e564901b8ca667`.
- Existing-regression tvOS Release IPA: 896,931,734 bytes, SHA-256
  `4d8e8253294f3224cfab6cbbcebe82d4d6f03fcac8a5a5da572a6806a527259f`.
- Both packages passed ZIP integrity, full trim, full AOT, LLVM object and
  referenced-API verification with `UseInterpreter=false`. The combined clean
  unsigned build took approximately 8 minutes 20 seconds. No real-pair package
  was built because its independent HookGen blockers stop before product
  generation.
- Stage 25F-A durability, Stage 25E helper ecosystem and Stage 25D detour
  closures remain the selected regression graph. The generated ModInterop plan
  is shared before platform derivation.
- Runtime scans reject the desktop ModInterop manager, general MonoMod.Utils,
  assembly loading, reflection emit, dynamic methods, desktop detour/ILHook,
  native mod loading, Lua, process spawning and file watching.
- The current tvOS Stage 22B regression chain also passed: Stage 13B 21,
  Stage 12B 16, Stage 11 38, Stage 10 protocol 66, Stage 15 31, Stage 16B 39,
  and Stage 22B continuity 57, plus the repository/Stage 14 product chain.

The older Stage 25F-A verifier remains byte-for-byte historical and expects
transformer v5 plus its accepted historical closure. It is verified at its
accepted commit; Stage 25F-B's current verifier owns the new v6 invariant.

## Locks and privacy

Canonical game locks remain:

- Content `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46`
- Raw `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273`
- Patched `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5`
- Stage 6 `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9`
- Vanilla iOS 944-file tree
  `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357`

Native locks remain:

- iOS `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`
- tvOS `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`

All third-party ZIPs, DLLs and source trees remain ignored. No game data, mod
bytes, saves, signing data, private paths or device identifiers are tracked.

## Recommendation

The static/generated architecture is sound and should be retained. Stage
25F-B is **YELLOW**, not integration-ready: run bounded Stage 25F-B2 before the
larger two-helper composition stage. Deterministic IL work is not yet the next
move because the selected real ModInterop pair is blocked first by ordinary,
reviewable HookGen catalog breadth. For medium maps, that catalog breadth,
multi-helper graphs, broader Everest APIs and custom content/audio are the main
barriers. Strawberry Jam additionally retains large dependency breadth,
active IL/ILHook, Lua/native policy and content virtualization blockers.
