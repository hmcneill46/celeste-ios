# Stage 25H-A — deterministic build-time `IL.*` freeze

Status: **PASS — GREEN**

Date: 2026-08-22

Stage 25H-A proves one deliberately bounded production compatibility class for
ordinary precompiled Everest mods that subscribe to HookGen `IL.*` events. The
real distributed manipulator runs on the Mac against an exact target-body
fingerprint using the repository-pinned MonoMod implementation. Its validated,
deterministic result is then compiled by the normal full-trim/full-AOT Apple
pipeline. No Cecil, `MonoMod.Cil`, `ILHook`, runtime code generation, or device
method-patching backend enters the application.

The accepted class is named `STATIC_IL_EVENT_FREEZE`. It is not generic runtime
IL support: the first registry permits exactly one known manipulator on each of
two known methods. A frozen module is immutable-active for the installed build.

## Baseline and immutable references

| Item | Value/result |
| --- | --- |
| Accepted B2 integration | `8ed17c42ee3bd6f00fde8516d1ac8da833cf3f3e` |
| Stage 25G YELLOW parent / starting commit | `9ffc1460d15bfe69074f55f6f3187d3cb2156365` |
| Feature branch | `feature/apple-everest-static-il-freeze` |
| Final commit | The commit containing this report; the final response records its exact SHA |
| Everest | stable-1.6458.0 / `4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00` |
| MonoMod | `dfc30a1506d37fb88a2c2be004f525205f46a24c` |
| iOS recovery tag | `27e16b4724d94d3991b99c4795f680fcb0e5830c` |
| tvOS RC1 | `ee52b0868df091746f134d95d4f020f94f23d4fb` |
| tvOS RC2 | `641e86e4ed164cdf93f602ce2f11436449654d6e` |
| RC3 release branch | `c8134c8ca7924cf12f48527e714b5242c6024927` |
| RC3 tag | Absent |
| GitHub Actions | Not run; zero Actions minutes used |

The Stage 25G audit and report remain byte-identical to their parent versions.
No integration/release branch or immutable tag was changed during this work.

## Real-world IL cohort

The audit used public source at exact commits for 27 real helpers/mods—more than
the required 20—and classified 138 HookGen `IL.* +=` sites, 132 corresponding
remove sites, 135 distinct subscriptions, and 115 target identities. Twenty-
three targets had multiple subscriptions. The same cohort contained 45 direct
`ILHook` constructions, four `DetourConfig` occurrences, and four
`DetourContext` occurrences.

The principal source-occurrence signals were:

| Shape | Count |
| --- | ---: |
| Cursor/pattern matching | 699 |
| Ordinary emitted instructions | 321 |
| `EmitDelegate` | 201 |
| Locals | 137 |
| Branches/labels | 90 |
| Single-instruction removal | 72 |
| Constant replacement | 52 |
| Lambda-near-`EmitDelegate` candidates | 38 |
| Opcode replacement | 30 |
| Direct call insertion | 27 |
| Exception-handler interaction | 6 |
| Operand replacement | 5 |
| Range removal | 3 |
| Switch tables | 0 |
| Explicit `DynamicReferenceManager` source use | 0 |

These are reproducible static source-occurrence measures, not claims that every
occurrence is an independent or accepted manipulator. Captured-delegate risk
was classified through 38 lambda-near-`EmitDelegate` candidates; the selected
fixture itself uses two noncapturing delegates. The complete pinned cohort and
taxonomy are in `apple-everest/il-compatibility-audit-stage25h.json`.

Stage 25G's 24 deep graphs and 18 IL-primary graphs were revisited honestly.
The exact first registry does not directly unlock one of those graph closures,
so none was relabelled. The measured distribution nevertheless confirms that
bounded IL breadth is the highest-payoff compatibility direction.

## Selected ordinary release

| Field | Exact accepted value |
| --- | --- |
| Mod | Dash Toggle Helper 1.1.0 |
| Public release | `https://gamebanana.com/mmdl/1460721` |
| ZIP SHA-256 | `677e8fbd067340d7b3133cc908e4ecafc0f5deab2c38b7eeb79a62eb5f61d523` |
| Original DLL SHA-256 | `531eaa8a719cb81cc84adf2b9e930dcb3abae73c60406b8f44b823c9c4b4a083` |
| Frozen DLL SHA-256 | `79d06fd9428e33613b81f089693651f03c6445e0a6f22368902bb3fcb49e904f` |
| Source repository | `https://github.com/kyfex-uwu/DashToggleHelper` |
| Source commit | `9b140684c2ee80ddae3c9ef032de0c767a67530c` |
| Source logical SHA-256 | `a26ac163b4184cc0daccfd99f4ef11aeeeef7a2858b7d84938beb0dc6afd5d09` |
| License | MIT |
| Required dependency | EverestCore 1.5421.0 |
| Optional dependency | MoreDasheline 1.7.1; absent exact fallback frozen |

The ordinary public ZIP and precompiled DLL are authoritative. The source was
used for review and licensing evidence only; production acquisition, analysis,
freeze, closure generation, compilation, and AOT completed with no source tree.
External mod bytes remain ignored and are not redistributed.

The module contains two real HookGen IL registrations and six ordinary `On.*`
registrations. It does not construct a direct `ILHook` or use configuration or
ordering metadata.

## Exact transformations

| Event / target | Manipulator | Before SHA-256 | After SHA-256 | Normalized diff SHA-256 |
| --- | --- | --- | --- | --- |
| `IL.Celeste.CrystalStaticSpinner.CreateSprites` / `CrystalStaticSpinner.CreateSprites()` | `DashToggleHelperModule.CreateSpritesOverride` | `60e4d178d19f1e70f21e9e88243354830db71155aa33de6b7b1f9b1e6abf6a94` | `f03103f1f63b71351054d68b8fc6ed52a06dc1e690b616cf993885c93b3bd0d8` | `da499a5a57b7ecb09f1d15ec20b61caa445252b8f631789d7723cbc02dbc81a9` |
| `IL.Celeste.CrystalStaticSpinner.AddSprite` / `CrystalStaticSpinner.AddSprite(Vector2)` | `DashToggleHelperModule.AddSpriteOverride` | `0a10b7404b2394238548b3e32d89c8f15298c32f293a2bc23153e0c1a8ebd051` | `91f955361cc5df3c643c8bd09b9cd211cf7ee09d6817ae2175d6569e2f9ec0aa` | `17bf0d8a9050ef5f8372e08dc41367a800344a877a009096e20787dd64bb79e7` |

The normalized before/after evidence locks opcodes, operands, locals,
branches, and exception regions independently of PE/MVID noise. The frozen plan
SHA-256 is
`ce86b2eaeb0abfeab48254ffa202daa6e23d154b5b623c07dd5742c2fd465f87`.

Both manipulators use noncapturing `EmitDelegate`. Pinned MonoMod resolves those
delegates to ordinary direct calls to preserved static methods in the final
body. No `DynamicReferenceManager` cell or other host/runtime indirection
survives. The final reference inventory rejects Cecil, `MonoMod.Cil`,
RuntimeDetour/`ILHook`, dynamic-method, reflection-emit, and assembly-load
surfaces before the linker runs.

## Host execution and security boundary

`AppleEverestIlWorker` is a small host-only process built against the exact
pinned MonoMod source. For each registered plan it:

1. verifies fixture identity, the exact `IL.*` registration, and target body;
2. loads one target and one manipulator into an isolated Mac process;
3. invokes the real distributed manipulator through `ILContext`;
4. validates cursor success, branch targets, labels, locals, exception regions,
   allowed references, the after-body hash, and normalized diff hash;
5. writes only the validated target assembly back into the compile input.

The worker, MonoMod/Cecil host libraries, original fixture, plan, and MSBuild
targets are never device content. The product scanner fails if any enters an app
bundle or IPA. Unknown target/mod hashes, missing or duplicate registrations,
throwing/cursor-miss manipulators, partial prior transforms, a second
manipulator on a target, or forbidden final references fail closed.

The conformance matrix independently exercises constant replacement, an
ordinary static call, branches/labels, return alteration, a new local, and a
range removal through pinned MonoMod. Desktop reference execution proved the
same-target ordering: the ordinary `On.CrystalStaticSpinner.CreateSprites`
wrapper's `orig` calls the already IL-frozen original body.

## Device lifecycle and policy

Exact IL add/remove callsites are removed from the device module while all six
ordinary HookGen registrations retain their existing generated typed chain.
The frozen IL is therefore always active whenever this build contains the mod.
Runtime disable/unload is deliberately unsupported and never simulated. To
exclude the behavior, build without the mod; the no-mod control produced zero
frozen transforms and the exact original method-body hashes.

Multiple manipulators on one target, direct/configured `ILHook`, captured or
runtime-selected delegates, custom ordering/priority, runtime target selection,
and live unapply remain explicitly deferred.

## Determinism and shared closure

Three clean source-free closure builds were compared recursively and were
byte-identical:

| Boundary | SHA-256 / count |
| --- | --- |
| Shared closure | `cbf1fbd55aeb90495ceb99901926df056c16ead5d94e41daa3af9a11a9c1ac8f` |
| Managed logical tree | `bdc2fb054bf23469f34148c85e3f787ef5c3b1203bc7caa088e11c5e0f7fa8f3` / 31 files |
| Content logical tree | `ee432aeff74a65e77b5fb131afcbd51d71b02e311d739faf9c01f82bf8939135` / 8 files |
| Reviewed Apple API surface | `62c540c89097ba84d0880b7620be4f846c684fbb3a407b034fcf2958ce736e3d` / 21 exact members |
| Frozen transform plan | `ce86b2eaeb0abfeab48254ffa202daa6e23d154b5b623c07dd5742c2fd465f87` / 2 transforms |
| Frozen distributed assembly | `79d06fd9428e33613b81f089693651f03c6445e0a6f22368902bb3fcb49e904f` |

The eighth content file is the project-owned compiled physical-test map. The
exact fixture ZIP is the only condition under which the Canary builder mounts
that room; it contains no third-party binary, source, or proprietary asset.

The identical closure feeds both iOS/iPadOS and tvOS; there is no platform-
specific mod transformation fork. Reapplying the exact worker to already-frozen
input is an explicit no-op with the same hash. A partial or unknown state fails.

## Full-AOT products

One clean, signed, separate-identity build produced both Apple products from
the same closure in 508.32 seconds.

| Product | IPA bytes | SHA-256 | Minimum OS | Result |
| --- | ---: | --- | --- | --- |
| iOS/iPadOS | 882,384,991 | `5944e0fe881e6333e80903543a83635008b7bbc0fa0f0bd5618f18bed4aa9a1f` | iOS/iPadOS 15.0 | Release arm64; full trim; full AOT; interpreter false |
| tvOS | 896,564,255 | `e03ef670f3cdd77ed0066506a37e89d1570aba5aba97b3bfd70193fe6199e540` | tvOS 16.0 | Release arm64; full trim; full AOT; interpreter false |

ZIP integrity, platform architecture/tagging, package validation, preserved
assembly identity, every frozen assembly reference, and an LLVM/Mono AOT object
for the mod passed. No worker, plan, target, original fixture, MonoMod/Cecil,
RuntimeDetour, dynamic-method, or interpreter payload was found in either app.

The existing canonical/native locks remain unchanged:

| Boundary | SHA-256 |
| --- | --- |
| Content | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw source | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched source | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Stage-6 real-audio source | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| iOS native | `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2` |
| tvOS native | `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc` |
| Vanilla iOS generated source | `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357` |
| Existing ModInterop plan | `9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318` |

## Physical acceptance and regressions

The exact signed products were installed as same-identity Canary replacements,
without uninstalling, on iPhone, iPadOS 15.8.8, and Apple TV. All three reached
a functional menu and the tracked static-IL test room. Dash-dependent blocks
and spinners visibly followed the real mod behavior; movement, dash,
death/respawn, pause/resume, audio, touch/controller input, rotation where
applicable, and background/foreground remained functional. tvOS Home/reopen
also passed. The products used the same frozen closure and no runtime IL backend.

Focused inherited B2/F-A/E/D behavior remains covered by the current 252-case
builder suite, 48 typed-hook semantics cases, six-shape pinned MonoMod freeze
matrix, desktop HookGen lifecycle/ordering reference, and 14 pinned ModInterop
reference cases. Historical stage verifiers whose purpose is to lock an older
transformer version, 51-target catalog, or pre-semantic filename remain valid
at their accepted commits and were not weakened; Stage 25H owns the current v8,
56-target invariants.

No GitHub Actions workflow was run. Repository/privacy verification found no
tracked Celeste, FMOD, mod ZIP/DLL/content, generated proprietary source, IPA,
save, signing value, device identifier, credential, or private absolute path.

## Limitations and next recommendation

This proof supports only registered exact fixtures and exact method bodies. It
does not promise unrestricted `IL.*`, direct `ILHook`, arbitrary Celeste or
Everest builds, runtime mod loading, live module disable, JIT, Lua, custom FMOD,
or general desktop Everest compatibility.

Because this first exact registry directly unlocks none of the 18 Stage 25G
IL-primary graphs, the next rational work is **Stage 25H-B**: use the measured
audit to add common noncapturing/captured `EmitDelegate`, multiple-manipulator,
and additional exact target classes. Then return to **Stage 25G2** as soon as a
real composed map graph reaches zero blockers. Direct/configured `ILHook` and
custom FMOD/audio remain separate later rungs.
