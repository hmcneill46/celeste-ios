# tvOS native library Stage 1 report

## Outcome and scope

**Acceptance status: passed in full.** Two independent clean generated
directories passed archive-member, XCFramework, symbol, licence, and native-link
verification and produced equivalent normalized manifests. The normalized
logical SHA-256 for both acceptance runs is
`61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39`.

Stage 1 is limited to the six open-source native components SDL2, FNA3D,
FAudio, Theorafile, tvStubs, and MoltenVK. It does not create a tvOS
application, consume Celeste executable/content/decompiled code, use FMOD,
sign or install anything, or implement controller, save, or user-management
behavior. A successful result here says nothing about whether an SDL/FNA host
or Celeste builds, launches, or renders.

## Baseline and host

The work began on branch `tvos-port` at commit
`2cf0d591f6508f6d3d1704514cecdc7f615b3802`, with an empty
`git status --short`. The relevant checked-out submodule revisions were FNA
`d52b4ce61e4086b785c51a96d331dbf106975a58` and the native builder
`41e34f66f8e74181d13e3a6e372d5085e3e1a344`; FNA's uninitialised native
submodules were recorded by `git submodule status --recursive` and were not
used as build directories.

| Host item | Verified value |
| --- | --- |
| macOS | 26.3 (`25D5087f`), arm64, Apple M1 |
| Xcode | 26.6 (`17F113`) |
| selected developer directory | `/Applications/Xcode.app/Contents/Developer` |
| tvOS SDKs | `appletvos` 26.5; `appletvsimulator` 26.5 |
| Apple clang | 21.0.0 |
| CMake / Ninja | 4.4.2 / 1.13.2 |
| GNU Make | 4.4.1 |
| Python | 3.9.6 |
| Git | 2.50.1 |
| Bash used by scripts | 3.2.57 |

The pipeline installed nothing. It uses Xcode, Python, Git, GNU Make, and the
standard Apple binary tools already present. The Stage 0 redacted host command
was rerun before work began.

Before any tvOS build, the five checked-in iOS native archives were inventoried
into a temporary, untracked file. Their SHA-256 values were:

| Existing iOS archive | Starting SHA-256 | Starting platform |
| --- | --- | --- |
| `libFAudio.a` | `3d704f74bc8987fa90c53889520a4cee1e32e6b9b15b1f94c66fd5f206cb3bee` | iPhoneOS, arm64 |
| `libFNA3D.a` | `7190c0776e4e394511bddadef5864a16758ab39f4db25cad8de7b9b3086a3a67` | iPhoneOS, arm64 |
| `libMoltenVK.a` | `e080304e9cf481468f656ea15421abcbe84ddcbe20c6b0ba4eed1d21263e5d03` | iPhoneOS, arm64 |
| `libSDL2.a` | `6cd680d7eda8fc0aecd744bf440bba5958435a89750267336a4e37dc806a9d70` | iPhoneOS, arm64 |
| `libtheorafile.a` | `b14d59dd093abc3c89dbe900e6886d1e853ce661262f9b5dfb7669eeafc70d48` | iPhoneOS, arm64 |

The final isolation check reproduced these values exactly. No checked-in iOS
archive is an input to the Stage 1 scripts.

## Immutable dependency graph

The complete machine-readable graph is
`native/tvos-dependencies.lock.json`. Every operational revision is a full
40-character commit; tags are documentation only after resolution.

| Component | Locked upstream | Revision | Required build entry | Patch |
| --- | --- | --- | --- | --- |
| SDL2 | `libsdl-org/SDL` | `257cacab183b312bbe60bd7967eee44a3ad7be85` | `Static Library-tvOS` | pre-generated Metal shaders |
| FNA3D | `FNA-XNA/FNA3D` | `dba98a71514cc30f3c19dc19fc0a479be7c90d52` | `FNA3D-tv` | Xcode header reference |
| FAudio | `FNA-XNA/FAudio` | `0e39055a3fb3c27de8dc02d650fe6a3da6c4de2c` | `FAudio-tv` | static-curve alignment |
| Theorafile | `FNA-XNA/Theorafile` | `0c5504658a3108919e53b625287786a87529de42` | `theorafile-tv` | none |
| tvStubs | `RoootTheFox/fnalibs-ios-builder-celeste` | `41e34f66f8e74181d13e3a6e372d5085e3e1a344` | `tvStubs` | none |
| MoltenVK | `KhronosGroup/MoltenVK`, tag `v1.2.9` | `bf097edc74ec3b6dfafdcd5a38d3ce14b11952d6` | `tvos`, `tvossim` | locked nested graph |

Nested and verification-only inputs are also locked:

| Parent | Dependency | Revision |
| --- | --- | --- |
| FNA3D | MojoShader | `c9037d90fa2f59b6be65d1391ca11d345356bad1` |
| FNA3D | Vulkan-Headers | `85470b32ad5d0d7d67fdf411b6e7b502c09c9c52` |
| MoltenVK | cereal | `51cbda5f30e56c801c07fe3d3aba5d7fb9e6cca4` |
| MoltenVK | Vulkan-Headers | `eaa319dade959cb61ed2229c8ea42e307cc8f8b3` |
| MoltenVK | SPIRV-Cross | `84cdc3b68e5ef5a15ecfacda77c61f24a9080cf9` |
| MoltenVK | glslang | `e8dd0b6903b34f1879520b444634c75ea2deedf5` |
| glslang | SPIRV-Tools | `dd4b663e13c07fea4fbb3f70c1c91c86731099f7` |
| SPIRV-Tools | SPIRV-Headers | `4f7b471f1a66b6d06462cd4ba57628cc0cd087d7` |
| glslang | googletest tag `v1.14.0` | `f8d7d77c06936315286eb55f8de22cd23c188571` |
| MoltenVK | Vulkan-Tools | `09f5cc6b0758a05ccd6bcde1342256c15c76670e` |
| MoltenVK | Volk | `3a8068a57417940cf2bf9d837a7bb60d015ca2f1` |
| symbol verification | FNA managed bindings | `d52b4ce61e4086b785c51a96d331dbf106975a58` |
| symbol verification | SDL2-CS | `2b8d237fd4585d14ea837764ac247d4cd200158f` |

The fetcher clones into an ignored directory, validates both origin and HEAD,
rejects a dirty checkout unless its changes exactly equal the locked patch
set, and records `source-state.json`. It never checks out a moving branch.

## Source targets and compatibility work

Before compiling, the build runs `xcodebuild -list -json` and fails unless each
historical target exists. MoltenVK is separately gated on the
`MoltenVK Package (tvOS only)` and `ExternalDependencies-tvOS` schemes, then
built through its pinned `tvos` and `tvossim` GNU Make lanes.

Four minimal patches were required by Xcode 26.6:

1. **SDL2 Metal tool:** the original tvOS target failed with
   `cannot execute tool 'metal' due to missing Metal Toolchain`. Installing the
   optional Xcode component was outside scope. The pinned source already
   contains and selects tvOS and tvOS-simulator metallib byte arrays, so the
   patch removes only the redundant `.metal` build-phase entry.
2. **FNA3D header:** the historical project omitted
   `FNA3D_SysRenderer.h`, causing a clean source-layout include failure. The
   patch adds only that header's Xcode file reference, matching the audited
   native-builder correction.
3. **FAudio linker alignment:** exhaustive device force-loading failed first
   with `ld: pointer not aligned in '_F3DAudioCalculate.lpfReverbDefault'`
   (and an atom-alignment diagnostic). Four pointer-bearing static distance
   curve structures now request eight-byte alignment. Their values, lifetime,
   and calculations are unchanged.
4. **MoltenVK nested fetch and DerivedData:** v1.2.9's glslang helper otherwise creates/fetches
   a `known-good` remote. The patch skips only that second mutation because the
   Stage 1 fetcher has already populated and verified the same exact nested
   commits. Its Makefile also defaults to the user's global Xcode DerivedData;
   the patch accepts `MVK_DERIVED_DATA_PATH`, which the Stage 1 script sets to
   separate device and simulator directories beneath the explicit ignored
   build root. No Xcode product setting or source is changed.

No dependency was upgraded and no broad warning suppression was added.

## Native results

The common deployment target is tvOS 16.0 and the installed object SDK is
26.5. The optional x86_64 simulator slice is available for all six components.
MoltenVK's upstream tvOS device lane also emits arm64e; it remains a tvOS device
slice and is packaged beside the mandatory arm64 slice.

| Component | Device archive | Simulator archive | Mach-O members inspected (device / simulator) | Exported symbols |
| --- | --- | --- | ---: | ---: |
| SDL2 | arm64, `TVOS` | arm64 + x86_64, `TVOSSIMULATOR` | 189 / 378 | 1,449 |
| FNA3D | arm64, `TVOS` | arm64 + x86_64, `TVOSSIMULATOR` | 13 / 26 | 366 |
| FAudio | arm64, `TVOS` | arm64 + x86_64, `TVOSSIMULATOR` | 18 / 36 | 353 |
| Theorafile | arm64, `TVOS` | arm64 + x86_64, `TVOSSIMULATOR` | 37 / 74 | 293 |
| tvStubs | arm64, `TVOS` | arm64 + x86_64, `TVOSSIMULATOR` | 1 / 2 | 25 |
| MoltenVK | arm64 + arm64e, `TVOS` | arm64 + x86_64, `TVOSSIMULATOR` | 2 / 2 | 508 |

Each thin slice contains one archive symbol-table member, reported as archive
metadata rather than silently skipped. Every other member is Mach-O. The
verifier rejects non-Mach-O payloads, unknown platforms, iPhoneOS, iOS
Simulator, macOS, wrong architectures, and a minimum OS other than the common
16.0 baseline. It separately validates the two platform variants in each
XCFramework `Info.plist` and compares packaged archive hashes to the staged
archives.

The generated products are ignored and rebuildable:

```text
artifacts/tvos-native/<run>/SDL2.xcframework
artifacts/tvos-native/<run>/FNA3D.xcframework
artifacts/tvos-native/<run>/FAudio.xcframework
artifacts/tvos-native/<run>/Theorafile.xcframework
artifacts/tvos-native/<run>/tvStubs.xcframework
artifacts/tvos-native/<run>/MoltenVK.xcframework
```

Acceptance run E recorded these raw archive hashes:

| Component | Device SHA-256 | Simulator SHA-256 |
| --- | --- | --- |
| SDL2 | `519be8a0ab399e2ab5125a4b8728b15411c5b1e1d94f4cce5581a5d09e9d6ef9` | `a8932e363d170d259225e108bc927d68033c6fa3b4fc0b69d7c3acd043f17bce` |
| FNA3D | `900afe89f07c6b5392490f7455ab2090826911f2d8cb245439513d6320df2d7f` | `3cc0e468f28694fc8ebfa36623a15ad53b0217d13b912a5eb1222d3bae49d9af` |
| FAudio | `1ca7a074abbb998a1e6ccfa4064afe68db7e1b6a554193d2280b6071de3eba7d` | `87e0a9cc19dc29a07147f579bcc97f5b0adb79b5ffc49ed050cf9fd96fcb547e` |
| Theorafile | `50099fd678241f969abe9e83f38644ab751d0618b46e61e67e3b34b865dbaf68` | `c60554de520751d7bac473345f3b932cc57c18febccda93088bc610fd4ce4bdc` |
| tvStubs | `65c366b7773db3734da4e96a8e8d76f45518fa07939187d24afe6d262c515206` | `f8760bc5ebdd30562bd8da57166f457d48690ce8ef5b8de028953d04ee69c824` |
| MoltenVK | `dbc90769a0535c5bd3665e1be5085cf919509cabb8c26e9db2b4048b7d6fd926` | `107492e66040e7f73d32293ef81e035b1b1d4e1d9adf4ca30e047a742619a826` |

## Managed-symbol expectations and tvStubs

`native/tvos-symbol-expectations.json` is regenerated from exact locked FNA,
SDL2-CS, FAudio-CS, and Theorafile-CS sources, then byte-for-byte compared with
the tracked manifest. FMOD is deliberately absent.

| Native API | Managed imports expected | Expected imports found |
| --- | ---: | ---: |
| SDL2 | 565 | 565 (551 in SDL2, 14 platform-only imports in tvStubs) |
| FNA3D | 78 | 78 |
| FAudio | 151 | 151 |
| Theorafile | 11 | 11 |
| MoltenVK loader | 1 | 1 |
| tvStubs source contract | 25 | 25 |

For both device and simulator, the intersection between all real-library
exports and tvStubs exports is empty. Consequently no real implementation is
shadowed. The 25 intentional compatibility exports, all of which must remain
unreachable on tvOS, are:

- Windows/D3D: `SDL_DXGIGetOutputInfo`,
  `SDL_Direct3D9GetAdapterIndex`, `SDL_RenderGetD3D11Device`,
  `SDL_RenderGetD3D9Device`, and `SDL_SetWindowsMessageHook`; these satisfy
  Windows renderer/window declarations retained by the cross-platform binding.
- WinRT: `SDL_WinRTGetDeviceFamily`, `SDL_WinRTGetFSPathUNICODE`,
  `SDL_WinRTGetFSPathUTF8`, and `SDL_WinRTRunApp`; these satisfy WinRT
  filesystem and application declarations.
- Android: `INTERNAL_SDL_AndroidGetExternalStoragePath`,
  `SDL_AndroidBackButton`, `SDL_AndroidGetActivity`,
  `SDL_AndroidGetExternalStoragePath`, `SDL_AndroidGetExternalStorageState`,
  `SDL_AndroidGetInternalStoragePath`, `SDL_AndroidGetJNIEnv`,
  `SDL_AndroidRequestPermission`, `SDL_AndroidShowToast`,
  `SDL_GetAndroidSDKVersion`, `SDL_IsAndroidTV`, `SDL_IsChromebook`, and
  `SDL_IsDeXMode`; these satisfy Android activity, storage, permission,
  notification, and device-mode declarations.
- Linux: `SDL_LinuxSetThreadPriority`, retained for the Linux scheduler API.
- Emscripten: `emscripten_cancel_main_loop` and
  `emscripten_set_main_loop`, retained for browser main-loop declarations.

These functions are link compatibility only, not tvOS implementations.

## Native link probes

Both probes target tvOS 16.0 arm64, force-load every object from all six
platform-correct archives, emit a link map, use `-no_adhoc_codesign`, and are
neither signed nor installed. Device output verifies as `TVOS`; simulator
output verifies as `TVOSSIMULATOR`. Undefined or duplicate symbols are fatal.
The final device and simulator probe SHA-256 values are respectively
`d32161d677be3c49c9c0c23c0ac5824e4c5413b49c89fdffa588c99329edd0c9`
and
`2d0db8248c85f8b80cc7de55a365adb688169f0a5f1bb94d213b822ec7dea380`;
both values reproduced byte-for-byte in run F.

The empirically minimized link set is:

```text
Foundation UIKit AVFoundation AudioToolbox CoreBluetooth CoreGraphics
CoreHaptics GameController IOSurface Metal OpenGLES QuartzCore libc++
```

Each retained item was removed individually from the otherwise successful
exhaustive link to confirm an unresolved reference. Foundation/UIKit provide
SDL application, window, and event integration; AVFoundation/AudioToolbox are
used by the SDL and FAudio audio paths; CoreBluetooth, CoreHaptics, and
GameController are SDL input dependencies; CoreGraphics is used by display and
MoltenVK color-space code; Metal, QuartzCore, and IOSurface support SDL/FNA3D/
MoltenVK rendering and surfaces; OpenGLES is required because the pinned SDL
archive still contains UIKit OpenGL ES objects; and `libc++` is required by
MoltenVK. Redundant CoreAudio, CoreFoundation, CoreVideo, and `libobjc` flags
were removed. CoreMotion is not present in the installed tvOS SDK.

## Reproducibility

`ZERO_AR_DATE=1` is exported for deterministic archive tables. Debug-symbol
generation is disabled. The normalized manifest includes dependency commits,
patch paths and hashes, architecture/platform/minimum-OS/SDK evidence, every
archive member payload hash, exported-symbol hashes, and canonical
XCFramework metadata. It intentionally excludes link-executable UUIDs, link
maps, absolute generated paths, and logs. Those volatile outputs remain in the
full per-run `manifest.json` with raw SHA-256 values; the normalized comparison
input is the adjacent `normalized-manifest.json`.

| Run | Clean generated directory | Verification | Normalized logical SHA-256 |
| --- | --- | --- | --- |
| A (development validation) | `rebuild-a` | passed | `87feaf3fa2b6fea968049342615a8f45a35bc15d31cf653d6454b33a868080ac` |
| B (isolation diagnostic) | `rebuild-b` | disqualified: upstream global DerivedData | not compared |
| C (isolation diagnostic) | `rebuild-c` | disqualified: shared `Package` replacement exposed | not compared |
| D (reproducibility diagnostic) | `rebuild-d` | disqualified: SDL2 absolute path evidence | not compared |
| E (acceptance) | `rebuild-e` | passed | `61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39` |
| F (independent comparison) | `rebuild-f` | passed; equivalent to E | `61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39` |

An early development orchestration was invalidated after the shell script was
edited while Bash was still reading it; the native compilations had completed,
but the invocation did not qualify as an acceptance run. Acceptance runs use
frozen scripts and entirely clean generated `work`, `stage`, logs, and output
directories. A later diagnostic run exposed MoltenVK's global DerivedData
default and was also excluded; the final runs redirect those intermediates
under their respective build roots. That isolation also exposed the upstream
packaging scheme's replacement of the shared `Package` directory when device
and simulator use separate DerivedData. A diagnostic run compiled both
archives but failed collection after the simulator package replaced the device
package. Final runs collect the two unambiguous `Build/Products` archives from
their respective DerivedData roots. Comparing that fresh run with the prior
diagnostic exposed SDL2's absolute checkout path in one `__FILE__` string and
Xcode's path-derived 32-hex suffixes on colliding object basenames. Final runs
compile SDL2 with a source-prefix map. Raw inspection retains all original
member names and archive hashes, while the logical comparison normalizes only
those terminal Xcode path-hash suffixes. Between E and F, SDL2 had ten such
renamed members per architecture; every corresponding object payload hash was
identical. All other component archive hashes were byte-for-byte identical.
The unsigned device and simulator probe executables were also byte-for-byte
identical; their link-map hashes differed only because link maps record
generated absolute paths. String inspection found no local home or run-root
path in any of the twelve final staged archives. There are therefore no
unexplained binary differences hidden by the normalization.

## Licences

Verification generates `licenses.json` plus 25 exact notice files for the 19
locked checkouts. The inventory includes SDL2 and its embedded HIDAPI/yuv
notices; FNA3D, MojoShader, and both Vulkan-Headers revisions; FAudio;
Theorafile; MoltenVK; cereal and RapidXML; SPIRV-Cross; glslang; SPIRV-Tools and
SPIRV-Headers; googletest; Vulkan-Tools; Volk; and the two managed binding
sources used for verification.

The locked native-builder checkout contains no standalone licence file for
tvStubs. That absence is recorded as an explicit redistribution-review risk;
it is not silently treated as licensed. FMOD, Celeste, game content, signing
files, profiles, and private device data are not in the bundle.

## Remaining warnings and Stage 2 risks

Verified non-fatal diagnostics are confined to pinned upstream code: SDL's
obsolete Carbon-resource phase warning; FAudio's unused internal validation
helpers; glslang enum-arithmetic warnings and a symbol-empty SPIR-V object; and
MoltenVK v1.2.9's deprecated Metal feature-set APIs, C++ variable-length-array
extensions, and newer-enum switch cases. These were not broadly suppressed or
patched because the archives and exhaustive probes verify correctly and such
changes would enlarge the compatibility patch unnecessarily.

Inferred Stage 2 risks, not Stage 1 claims:

- A future host must select the correct XCFramework variant and preserve all
  required Objective-C/C++ linker behavior.
- MoltenVK v1.2.9 compiles against SDK 26.5 but its deprecated Metal paths need
  rendering validation on the actual Stage 2 host and hardware.
- OpenGLES is currently required by exhaustive SDL force-loading even if a
  future host uses Metal/Vulkan; dead-strip behavior must not be assumed here.
- Theorafile packages its pinned Ogg, Theora, and Vorbis public headers; a
  future consumer must avoid mixing them with incompatible versions.
- tvStubs calls must remain unreachable and its licence status must be resolved
  before any redistribution.

Unverified at this stage: application-host startup, managed P/Invoke loading,
rendering, controller behavior, audio playback, FMOD, saves, signing,
installation, and physical-device behavior.

## Rebuild and verification commands

From the repository root, with no installation or signing side effects:

```bash
bash -n scripts/fetch-tvos-deps.sh
bash -n scripts/build-tvos-native.sh
bash -n scripts/verify-tvos-native.sh

scripts/fetch-tvos-deps.sh --clean \
  --build-dir .build/tvos-native/rebuild-1 \
  --cache-dir .build/tvos-native/git-cache
scripts/build-tvos-native.sh --clean \
  --build-dir .build/tvos-native/rebuild-1 \
  --output-dir artifacts/tvos-native/rebuild-1
scripts/verify-tvos-native.sh \
  --build-dir .build/tvos-native/rebuild-1 \
  --output-dir artifacts/tvos-native/rebuild-1

scripts/fetch-tvos-deps.sh --clean \
  --build-dir .build/tvos-native/rebuild-2 \
  --cache-dir .build/tvos-native/git-cache
scripts/build-tvos-native.sh --clean \
  --build-dir .build/tvos-native/rebuild-2 \
  --output-dir artifacts/tvos-native/rebuild-2
scripts/verify-tvos-native.sh \
  --build-dir .build/tvos-native/rebuild-2 \
  --output-dir artifacts/tvos-native/rebuild-2 \
  --compare-manifest \
  artifacts/tvos-native/rebuild-1/normalized-manifest.json
```

Use `TVOS_DEPLOYMENT_TARGET=<version>` as the single documented override, with
the same value for every command in both artifact sets. The supported default
for this port is `16.0`.
