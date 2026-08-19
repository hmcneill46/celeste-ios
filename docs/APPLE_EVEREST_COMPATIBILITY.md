# Apple Everest real-mod compatibility

This is a technical test matrix for the experimental shared Apple static-AOT
builder. It lists only exact public inputs inspected during Stage 25C. It is not
a promise that similarly named, newer, older, or dependent mods work. The
normal iOS and tvOS products do not contain these mods.

Status vocabulary:

- **SUPPORTED** — content enters the static closure without managed code.
- **SUPPORTED_WITH_STATIC_TRANSFORM** — the distributed DLL is transformed on
  the Mac and linked/AOT-compiled; no source is needed.
- **DEFERRED_…** — understood but needs another bounded compatibility class.
- **UNSUPPORTED_…** — conflicts with the current Apple runtime policy.
- **REJECTED_PACKAGE** — not a complete root Everest package as supplied.

## Selected production evidence

| Mod | Exact input | Class | Result | Platforms | Requirement | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| [I Accidentally Four Cassette Blocks](https://gamebanana.com/mods/150469) | 1.0.0; ZIP `37eaa16b…7c95` | Content-only | **SUPPORTED** | iOS, iPadOS, tvOS | Everest ≥ 1.519.0 | Ordinary map, dialog, and GUI asset; no DLL. No redistribution license was located, so the ZIP remains ignored and is never rebundled in Git. |
| [Particle Palette Helper](https://github.com/KnowHT1515/ParticlePaletteHelper/releases/tag/v1.0.0) | 1.0.0; ZIP `f9cf8874…be19`; source `9a791bcb…bb82` | Precompiled module + six `On.*` targets | **SUPPORTED_WITH_STATIC_TRANSFORM** | iOS, iPadOS, tvOS | Everest ≥ 1.6418.0 | MIT. Ordinary DLL, no source required. Assembly identity is retained. Current proof covers lifecycle, hook add/remove, cached typed `orig` pass-through, trim and AOT. Palette YAML deserialization/content enumeration remains deferred. |

The selected closure uses Everest stable 1.6458.0 and its one shared hash is
`9c971fe1b84e80092a8d9178bbae9cf0f9588e185f85c6c4c61996775f4ec532`.

Precompiled Everest map binaries are validated as Celeste map containers and
their package header is normalized to the exact static mount path. This is the
build-time equivalent of Everest accepting the conventional `Contribution`
package label: the string table and element body remain the pinned mod bytes,
while vanilla Celeste's path-integrity check stays enabled at runtime.
Accepted external assemblies are fully rooted by generated managed identity;
the product gate rejects a linked DLL if trimming removed any executable method
body before full AOT. It also resolves every reference from that DLL against
the linked Celeste/Everest API and verifies that every executable external
method has a native body across the paired LLVM and Mono AOT objects. This
prevents an unresolved compatibility method from becoming an on-device JIT
fallback.

## Audited compatibility ladder

| Mod | Exact input | Result | Principal boundary |
| --- | --- | --- | --- |
| [Dashless Dream Blocks](https://github.com/coloursofnoise/DashlessDreamBlocks/releases/tag/v2.0.0) | 2.0.0; ZIP `ef5071ad…a7b`; source `7c5aca66…6e6` | **DEFERRED_DIRECT_HOOK** | Direct `MonoMod.RuntimeDetour.Hook`, plus four unsupported HookGen targets. It is a direct-hook negative fixture, not an `IL.*` event fixture. |
| [GoldenTrainer](https://gamebanana.com/mods/364819) | 1.5.4; ZIP `2a39b5bb…fc5b`; source tag `96439220…a6897` | **DEFERRED_IL** | Real `IL.Celeste.SummitCheckpoint.Update` manipulator plus a direct `ILHook` on `Player.orig_Die`. Both delegates depend on live module settings/state. The binary analyzer now identifies the `IL.*` type from CLI metadata; freezing is deferred until the mixed direct-hook and unsupported `On.*` closure can be lowered safely. No explicit redistribution license was located. |
| [BetterSaves](https://github.com/Microck/bettersaves/releases/tag/v0.1.0) | 0.1.0; ZIP `9e9013f0…10c`; source `6bc11db0…756` | **DEFERRED_PLATFORM** | Save-filesystem semantics plus unsupported `OuiMainMenu`/`MainMenuClimb` hooks. |
| [ExtendedIdle](https://github.com/KnowHT1515/ExtendedIdle/releases/tag/v1.0.0) | 1.0.0; ZIP `cc128b76…299`; source `fb65c482…4c3` | **DEFERRED_DEPENDENCY** | Requires SkinModHelperPlus and unsupported `Player` hooks. |
| [Small Spaces](https://gamebanana.com/mods/590825) | GameBanana package 590825; ZIP `78f71b3c…dab` | **REJECTED_PACKAGE** | Supplied archive has no unique root `everest.yaml`; wrapper/partial packages are never guessed. |
| [Spikeless](https://gamebanana.com/mods/382011) | 1.0.0; ZIP `4880be51…a13` | **DEFERRED_DEPENDENCY** | Content itself is static, but required FrostHelper and MaxHelpingHand are unresolved. |
| No Gondola | GameBanana package 150520; ZIP `84debe67…cf` | **REJECTED_PACKAGE** | No unique root Everest metadata. |
| [Snowy Assorted Items](https://github.com/Snowy063/SnowyAssortedItems) | metadata 0.2.0; ZIP `ec1f595a…e23`; source `2d1a961d…fbb` | **DEFERRED_ON_HOOK** | Unsupported `CassetteBlockManager` HookGen target; custom-entity work remains outside this rung. |
| [Double Languages](https://github.com/Logabe/DoubleLanguages/releases/tag/0.3) | 0.3.0; ZIP `28096690…3f1`; source `af97c135…d17` | **DEFERRED_ON_HOOK** | Unsupported `GameLoader` and `Textbox` targets. No explicit redistribution license was located. |
| [Trailine](https://gamebanana.com/mods/349341) | 1.1.0; ZIP `c6cd0464…711`; source `4f12442b…3fb` | **DEFERRED_DIRECT_HOOK** | RuntimeDetour reference and direct behavior plus five unsupported HookGen surfaces. |
| [Extended Variant Mode](https://github.com/maddie480/ExtendedVariantMode) | source audit `fa9a25c3…cc3` | **UNSUPPORTED_LUA** | Real NLua dependency; Lua is not present in the device product. |
| [SmoothCeleste](https://github.com/bybrooklyn/SmoothCeleste) | source audit `b4169752…f5a` | **UNSUPPORTED_NATIVE** | Runtime native-library loading and platform P/Invoke are outside the fixed native closure. |

The full exact SHA-256 values, source pins, and license findings are in
[`apple-everest/real-mod-compatibility-stage25c.json`](../apple-everest/real-mod-compatibility-stage25c.json).
No third-party ZIP, DLL, map, texture, or source file is tracked.

## What is not supported yet

Unknown `On.*`, any unresolved `IL.*`, direct Hook/ILHook/RuntimeDetour,
NativeDetour, native/P/Invoke additions, Lua, dynamic assembly loading,
Reflection.Emit, desktop process/file-watcher behavior, helper ecosystems,
general ModInterop, general settings/save persistence, and custom mod audio all
fail closed or remain explicitly deferred. Strawberry Jam has not been
downloaded, built, or tested.
