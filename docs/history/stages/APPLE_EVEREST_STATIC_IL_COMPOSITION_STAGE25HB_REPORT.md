# Stage 25H-B — composed frozen IL and common `EmitDelegate`

Status: **PASS — YELLOW**

Date: 2026-08-22

Stage 25H-B productionises deterministic composition of two or more ordinary,
unconfigured HookGen `IL.*` manipulators on one target, and extends the
build-time freeze worker to a real compiler-generated singleton lambda. The
sequence engine is production-ready and matches the pinned desktop MonoMod
semantics. The classification remains YELLOW because the exact public fixtures
available for this stage did not contain two real independent manipulators on
the same target, and the new real fixture is not one of Stage 25G's exact 18
IL-primary deep graphs. Project-owned A/B/C conformance therefore proves the
composition mechanism; it is not misrepresented as real shared-target evidence.

All transformation still occurs on the Mac. The Apple products contain frozen,
ordinary managed IL and statically rooted calls only—never Mono.Cecil,
`MonoMod.Cil`, `ILHook`, dynamic methods, runtime code mutation, or a serialized
host closure object.

## Baseline and immutable references

| Item | Value/result |
| --- | --- |
| Starting integration commit | `9bc5d75fcaf42571c1575843f943cea39ce626dc` |
| Feature branch | `feature/apple-everest-static-il-compose` |
| Final commit | The commit containing this report; the handoff records its exact SHA |
| Historical Stage 25G | `9ffc1460d15bfe69074f55f6f3187d3cb2156365`, unchanged |
| Historical Stage 25F-B | `6cde1ad9ba93bbd2f57f2867baf3b189b2824b1b`, unchanged |
| Everest | stable-1.6458.0 / `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00` |
| MonoMod | `dfc30a1506d37fb88a2c2be004f525205f46a24c` |
| iOS recovery tag | `27e16b4724d94d3991b99c4795f680fcb0e5830c` |
| tvOS RC1 | `ee52b0868df091746f134d95d4f020f94f23d4fb` |
| tvOS RC2 | `641e86e4ed164cdf93f602ce2f11436449654d6e` |
| RC3 release branch | `c8134c8ca7924cf12f48527e714b5242c6024927` |
| RC3 tag | Absent |
| GitHub Actions | Not run; zero Actions minutes used |

The managed-detour catalog advances from 56 to 57 exact targets. Its one new
entry is `celeste-player-throw`, required by DisposableTheo's ordinary `On.*`
closure. The frozen plan advances to schema 2 and the product transformer to
`apple-everest-static-v9`.

## Pinned sequence semantics

The new production class is `STATIC_IL_EVENT_SEQUENCE`. For ordinary
unconfigured hooks, pinned MonoMod appends registrations and rebuilds the target
from one original source clone, invoking every manipulator sequentially in
registration order. Stage 25H-B preserves that exact behavior:

```text
exact canonical baseline body
    → manipulator A → validate/hash
    → manipulator B → validate/hash
    → manipulator C → validate/hash
    → delegate/reference lowering
    → final validate/hash
    → ordinary On/direct-Hook wrappers
    → full Apple AOT
```

Manipulator B therefore sees A's modified body; C sees A+B. The order comes
from resolved dependency/module load order and registration order inside the
module—not alphabetical sorting. The plan owns a contiguous local ordinal for
each target and includes the exact order in its hash. Duplicate registration,
an ordinal gap, a semantic no-op, changed baseline, invalid intermediate branch,
or mismatched intermediate/final hash fails closed. Configured ordering remains
`DEFERRED_CONFIGURED_IL_SEQUENCE`.

Project-owned conformance deliberately makes order observable. On a baseline
that evaluates to 10, A adds 3, B multiplies by 5, and C subtracts 7:

| Build/sequence | Result |
| --- | ---: |
| Neither | 20 |
| A | 23 |
| B | 50 |
| A then B | 55 |
| B then A | 43 |
| A then B then C | 48 |

The project-owned reference runner executes the same registrations through
pinned desktop MonoMod and compares normalized final bodies with the Apple
worker. A→B and B→A differ exactly as expected; A/B/AB/neither contain no
residual state from another build. A third manipulator exercises sequence
length three. The same target is also covered with a normal `On.*` wrapper,
whose `orig` observes the fully transformed body, and a statically modelled
direct-Hook boundary. Three clean A→B runs are identical.

This proves sequence mechanics, not a claim that the selected real public
closure has two subscribers on one target. No suitable exact high-value public
pair was found during this stage.

## Real IL audit and delegate taxonomy

The accepted H-A cohort remains 27 real helpers/mods, 138 HookGen add sites,
135 distinct subscriptions, 115 targets, 23 targets with multiple subscribers,
201 source `EmitDelegate` occurrences, 45 direct `ILHook` constructions, and
four configured `ILHook`/`DetourConfig` occurrences. The source corpus could not
be safely broadened into a complete semantic delegate inventory during this
stage, so uncertainty is retained rather than guessed:

| H-A cohort `EmitDelegate` class | Count |
| --- | ---: |
| Proven static method group / no capture | 6 |
| Proven compiler singleton lambda | 0 |
| Proven immutable constant capture | 0 |
| Proven module/runtime-object capture | 0 |
| Proven manipulator-local capture | 0 |
| Proven reflection/dynamic target | 0 |
| Unknown | 195 |
| Lambda-adjacent candidates within unknown | 38 |

The newly selected real fixture contributes two additional exact
compiler-generated singleton noncapturing lambda sites. Both consume and return
typed runtime stack values, and neither captures a host object. Across the
accepted H-A and H-B production fixtures, eight sites are now exactly lowered:
six ordinary static sites and two compiler-singleton sites.

The generic worker resolves the actual delegate target and signature. For a
compiler's stateless `<>c` singleton it emits an ordinary typed local spill,
loads the rooted singleton field, reloads the runtime arguments, and calls the
known instance method. It never copies the Mac delegate or closure object. The
singleton type, field, method, and any newly referenced target are publicised
and rooted for AOT.

Primitive captured constants, module-instance captures, mutable runtime
captures, transient manipulator-local captures, reflection-selected delegates,
direct `ILHook`, and configured hooks remain explicitly deferred. No captured
constant class is claimed in this stage.

## Selected real H-B fixture

| Field | Exact accepted value |
| --- | --- |
| Mod | DisposableTheo 1.0.6 |
| Public release | `https://gamebanana.com/mmdl/929736` |
| ZIP SHA-256 | `df291c0175df46682791fb6373c47eb557c47483eca3db96895eba9b5bbe85b5` |
| Distributed DLL SHA-256 | `1d47c08238fd0dd29eaa5c6e53a36e7d72942fdb7b7abc2a3870f09fbc952dcc` |
| Final frozen DLL SHA-256 | `4620a69277ccaaac8f11cf5583107630c3db5763b70bdec3d0852529ea8363c9` |
| Audited source logical SHA-256 | `fc6aa15ee69311eac205af76e382d8a09dfb16ebe73eb05163ba90da7c21d597` |
| License | No declaration located; no third-party bytes are redistributed |
| Production authority | Ordinary public release ZIP and precompiled DLL |
| Source needed by production | No |

The complete reachable registration census is two ordinary `IL.*` and two
ordinary `On.*` registrations, with no direct/configured `ILHook`. The exact
frozen targets are:

| Target / manipulator | Before SHA-256 | After SHA-256 | Diff SHA-256 |
| --- | --- | --- | --- |
| `TheoCrystal.Die()` / `DisposableTheoModule.TheoCrystal_Die` | `ec6294022668396295da4d81b61192b01bcbc9e898399d94a8a74251d3c87911` | `c538112f327281bfd4fa0af488a3ee175ff8662a63bfd3fced969e1d8e52ba55` | `107ea6b57c066477eda086f303ca7331c86adbe40964fe30a266d27691c02987` |
| `Level.EnforceBounds(Player)` / `DisposableTheoModule.Level_EnforceBounds` | `90f8c7928bd3cf8fff7db66aaebc122dc4f8082a947e41b78d5bba7d0021c1ac` | `025cf84ebf83868acd00f5bab4bc7c028f6190159e89c4199ec08d03db5ecd78` | `480bd4cdc59a703a66658d048a57067eebc55bde531c52acf344b6a3d3694fc1` |

Each target has registration ordinal zero because the real fixture modifies two
different methods. The exact delegate targets are respectively
`<>c.<TheoCrystal_Die>b__9_0` and
`<>c.<Level_EnforceBounds>b__10_0`.

The distributed assembly also needed narrow desktop-Everest ABI normalization
that does not alter its gameplay semantics: exact virtual-axis conversions are
bound to the canonical public value, exact two-argument rumble calls receive a
null third argument, generated-setting-only metadata is removed, and one unused
desktop settings-menu helper is replaced by an exact no-op. Every count is
locked and fails closed. Incorrect nested-type visibility discovered during the
first AOT attempt was fixed with the atomic Cecil visibility mask; this is now a
builder regression test.

The project-owned physical room uses only a built-in Theo crystal and the real
`DisposableTheoTrigger`. Throwing Theo into its right-hand pit exercises the
new frozen death/out-of-bounds semantics: Theo disappears without killing or
freezing Celeste. No third-party map or asset is tracked.

## Stage 25G payoff

All 18 historical IL-primary graphs were mapped to their helper and IL-family
blockers in `apple-everest/il-compatibility-audit-stage25hb.json`. The exact
DisposableTheo fixture is used by six broader Stage 25G candidate maps, but not
by any of those exact 18 deep graphs. Therefore the honest exact counts are:

| Result | Count |
| --- | ---: |
| Exact 18 graphs materially advanced | 0 |
| Exact 18 graphs with no remaining IL blocker | 0 |
| Exact 18 graphs with zero overall blockers | 0 |
| Broader audited candidate maps advanced by DisposableTheo | 6 |

No Stage 25G historical classification is changed. No exact graph is ready for
Stage 25G2 yet. The sequence engine creates the needed infrastructure for the
23 audited shared-target families, but a captured/unknown delegate or direct
`ILHook` remains the next likely bounded compatibility class; it must be chosen
from exact graph payoff rather than inferred from source-occurrence totals.

## Determinism and shared closure

The final source-free closure was regenerated twice after the last ABI fix and
matched byte-for-byte, including both frozen distributed assemblies. The same
target-neutral closure feeds iOS/iPadOS and tvOS:

| Boundary | SHA-256 / count |
| --- | --- |
| Shared closure | `51bc59ca40c9b0ea2962958ab53bca92b0aa43fa39aee7c3c15512305938e98f` |
| Managed logical tree | `76467b6a5b56db6019b6d9ec3d5070c62df40863bcbb5a02c582e6372c99af83` / 31 files |
| Content logical tree | `e38e5f286fae6e7c34ce4f3d9bee324dd43d8a8e264732d3af78d48377caf4ef` / 9 files |
| Reviewed Apple API surface | `62c540c89097ba84d0880b7620be4f846c684fbb3a407b034fcf2958ce736e3d` / 21 members |
| Frozen IL plan | `90759f477ea46d97aac981a1e8c61bbf8df97fce26bc4bc080c18aa30dd2473b` / 4 transforms |
| Registry | `a7d154672f00c050410a7119c0abd24ea5f6dad36702f4bf5ae225455f40891a` |
| Hook transform | `d66632f47b01df8786910bf5ba815037f9728d1dbda52e1bbaabbb6751438e33` |
| DisposableTheo frozen DLL | `4620a69277ccaaac8f11cf5583107630c3db5763b70bdec3d0852529ea8363c9` |
| Dash Toggle Helper frozen DLL | `79d06fd9428e33613b81f089693651f03c6445e0a6f22368902bb3fcb49e904f` |

Dash Toggle remains byte-identical to Stage 25H-A. Its H-A-alone build retains
the accepted two method-body transformations and the exact
`79d06fd...` frozen binary.

## Full-AOT products and scans

| Product | Build time | IPA bytes | SHA-256 | Result |
| --- | ---: | ---: | --- | --- |
| iOS/iPadOS | 10:21.94 | 882,405,073 | `c35dbaed18f5cc30e76c5efb4f8f49061a3d27ea9823e85fcd135a4fd43a1a45` | Release arm64; full trim; LLVM/full AOT; interpreter false |
| tvOS | 10:39.51 | 896,582,045 | `7c19a118f9717443bb361f6337ba4795d0b28736191a2b9ac83db355c9811e17` | Release arm64; full trim/full AOT; interpreter false |

Both packages passed ZIP, architecture, minimum-OS, package, preserved-assembly,
static-reference, and LLVM/Mono AOT-object checks. The final target bodies and
reachable assemblies contain no `Mono.Cecil`, `MonoMod.Cil`, `ILHook`,
`DynamicReferenceManager`, `DynamicMethod`, `Reflection.Emit`, runtime assembly
loading, or native detour surface. The two new singleton methods and every
injected/called target are statically rooted and AOT-covered. No JIT or
interpreter fallback exists.

The canonical/native locks remain unchanged:

| Boundary | SHA-256 |
| --- | --- |
| Content | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw source | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched source | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Stage-6 real-audio source | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| iOS native | `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2` |
| tvOS native | `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc` |
| Vanilla iOS generated source | `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357` |
| Existing B2 ModInterop plan | `9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318` |

## Physical acceptance

The signed separate-identity products were installed as same-identity Canary
replacements without uninstalling. On iPhone 12 Pro Max and Apple TV, the exact
new room proved DisposableTheo's IL-dependent pit behavior while Celeste stayed
alive and responsive. Both platforms also passed menu/startup, real audio and
controller/touch input, movement/dash, death/respawn, pause/resume, the H-A Dash
Toggle room, and background/reopen. iPhone additionally passed rotation; tvOS
passed Home/reopen and the expected FNA3D Metal/seven-bank path.

The same universal iOS IPA was installed on iPadOS 15.8.8 through the legacy
device route. It passed menu, touch/controller, audio, the DisposableTheo pit
behavior, movement/dash, death/respawn, pause/resume, rotation, the H-A Dash
Toggle room, and background/reopen. Thus the exact new IL-driven behavior is
physically green on iPhone, iPadOS 15.8.8, and Apple TV.

The current deterministic regression chain covers B2 ModInterop,
Stage 25F-A durability, Stage 25E helper/settings/content behavior, Stage 25D
managed detours, H-A freeze, 48 typed-hook semantics cases, the pinned desktop
HookGen reference, and the current builder suite. Historical verifiers that
lock earlier target counts or transformer versions remain valid at their
accepted commits and were not weakened.

## Security, privacy, lifecycle, and limitations

Only hash-pinned, source-audited, registry-approved manipulators execute in the
host worker. The worker remains isolated from device products. Frozen modules
are immutable-active: `-=`, unload, or live disable cannot unpatch method IL;
changing the installed graph requires a rebuild/reinstall. Ordinary `On.*`
lifecycle remains static and supported independently.

No third-party ZIP/DLL, Celeste or FMOD content, generated proprietary source,
IPA, save, signing value, device identifier, private path, credential, or
telemetry is tracked. No GitHub Actions workflow ran.

This stage does **not** support arbitrary IL, captured constants, module/runtime
captures, mutable/local captures, direct `ILHook`, configured ordering, live IL
unapply, RuntimeDetour generally, Lua, custom FMOD, or Strawberry Jam. A medium
standalone map is closer when its graph uses already registered static hooks,
content APIs, and the new exact freeze shapes; a small collab still needs a
substantially wider helper graph. A genuinely IL-heavy helper still requires
measured support for its remaining captured-delegate/direct-hook/configuration
classes. Strawberry Jam remains irrational to attempt until helper breadth,
audio, content/progression, and Lua/native blockers are addressed.

## Recommendation

Stage 25H-B is integration-ready as a truthful **YELLOW** compatibility rung:
the sequence engine and compiler-singleton lowering are production code, full
AOT packages and real IL-driven behavior pass, and every unsupported class
fails closed. Stage 25G2 is not yet the highest-value next step because no exact
deep graph has reached zero blockers. The next investigation should select the
single dominant exact blocker from the closest Stage 25G graph—likely a bounded
captured `EmitDelegate` or direct-`ILHook` class—and return immediately to a
physical multi-helper graph once one reaches zero overall blockers. No manual
download is required merely to integrate this stage; the next-stage fixture
choice should be driven by the existing ignored audit evidence.
