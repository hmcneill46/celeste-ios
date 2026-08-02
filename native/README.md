# Reproducible tvOS native dependencies

This directory is the tracked input to Stage 1. Downloaded repositories,
objects, archives, XCFrameworks, logs, link maps, and license bundles are
generated under ignored directories. None of these scripts installs software,
signs code, uses FMOD, or consumes Celeste code or content.

## Inputs

- `tvos-dependencies.lock.json` records every source URL and exact 40-character
  commit. Immutable release tags are recorded alongside their resolved commit.
- `patches/` contains every source edit applied to a clean locked checkout.
- `tvos-symbol-expectations.json` is generated from the pinned FNA, SDL2-CS,
  FAudio-CS, and Theorafile-CS bindings. It deliberately contains no FMOD API.
- `tvos-link-probe/main.c` references one public entry point from every real
  library and tvStubs. Verification force-loads all archives, so unused object
  files also participate in duplicate- and undefined-symbol checks.

The single supported deployment-target override is
`TVOS_DEPLOYMENT_TARGET`; its default is `16.0`. Do not combine artifact sets
built with different values.

SDL2's OpenGL ES renderer expands a macro containing `__FILE__`, so its compile
uses `-ffile-prefix-map` to replace only the staged checkout prefix with
`/STAGE1_SOURCES/SDL2`. Xcode also gives colliding object basenames a suffix
derived from their absolute source path. Raw reports preserve those member
names and archive hashes; the normalized reproducibility manifest replaces
only a terminal 32-hex object-name suffix with `<PATH_HASH>`.

## Compatibility patches

### SDL2: use the pinned pre-generated tvOS Metal shaders

The unpatched `Static Library-tvOS` target asks Xcode to compile
`SDL_shaders_metal.metal`. Xcode 26.6 fails when the optional downloadable Metal
Toolchain is absent:

```text
error: cannot execute tool 'metal' due to missing Metal Toolchain
```

The same pinned SDL source already checks in
`SDL_shaders_metal_tvos.h` and `SDL_shaders_metal_tvsimulator.h` and
`SDL_render_metal.m` embeds those precompiled metallib byte arrays. The patch
removes only the redundant `.metal` entry from the static tvOS target's Sources
phase. It does not change renderer source or suppress diagnostics.

### FNA3D: expose the system-renderer header to Xcode

The historical Xcode project omits `include/FNA3D_SysRenderer.h` from its file
references, causing the pinned source's renderer include to fail in a clean
builder layout. This is the same minimal project-file correction audited in the
pinned native-builder repository. It adds a header reference only; no source or
runtime behavior changes.

### MoltenVK: do not run a second dependency fetcher

MoltenVK v1.2.9 locks glslang, but glslang's
`update_glslang_sources.py` adds/fetches a `known-good` remote before checking
out its nested revisions. The Stage 1 fetcher already materializes and verifies
those exact revisions from the top-level lock. The patch skips only that network
mutation; MoltenVK's normal external-library and tvOS packaging builds remain
unchanged. The same patch adds an opt-in Makefile argument used by the Stage 1
lane to keep Xcode DerivedData under the caller-selected ignored build root;
upstream otherwise writes these intermediates to the user's global Xcode
DerivedData directory. Device and simulator are collected from their separate
`Build/Products` archives because running the two isolated upstream packaging
schemes successively replaces, rather than merges, the shared `Package`
directory.

### FAudio: align pointer-bearing static distance curves

With every FAudio object force-loaded, Xcode 26.6's arm64 linker rejects the
pinned function-local distance-curve structs because atoms containing pointer
relocations are emitted at four-byte alignment:

```text
ld: pointer not aligned in '_F3DAudioCalculate.lpfReverbDefault'
```

The patch requests eight-byte alignment on only the four static
`F3DAUDIO_DISTANCE_CURVE` instances that contain pointers. It does not alter
their fields, values, lifetime, or audio calculations. The patch is applied
only to the clean Stage 1 tvOS checkout.

## Commands

```bash
bash -n scripts/fetch-tvos-deps.sh
bash -n scripts/build-tvos-native.sh
bash -n scripts/verify-tvos-native.sh

scripts/fetch-tvos-deps.sh --clean --build-dir .build/tvos-native
scripts/build-tvos-native.sh --clean \
  --build-dir .build/tvos-native \
  --output-dir artifacts/tvos-native
scripts/verify-tvos-native.sh \
  --build-dir .build/tvos-native \
  --output-dir artifacts/tvos-native
```

For a second clean build, preserve the first
`normalized-manifest.json`, repeat all three commands with clean sources and
products, and pass the preserved file to `--compare-manifest`.

## Link-probe dependencies

Verification uses `clang` with the tvOS SDK, `-force_load` for all six static
archives, and a generated link map. The framework set is explicit and is not
copied from the iOS application's `MtouchExtraArgs`:

- Foundation and UIKit: SDL's tvOS application/window/event implementation.
- AVFoundation and AudioToolbox: SDL and FAudio audio backends.
- CoreGraphics: SDL display integration and MoltenVK color spaces.
- CoreBluetooth: SDL's controller discovery backend references
  `CBCentralManager`.
- CoreHaptics and GameController: SDL's tvOS input backends. CoreMotion is not
  present in the audited tvOS SDK and is intentionally excluded.
- Metal and QuartzCore: SDL, FNA3D, and MoltenVK rendering surfaces.
- IOSurface: MoltenVK's image/surface implementation directly imports the
  IOSurface C API.
- OpenGLES: the pinned SDL tvOS archive still contains its UIKit OpenGL ES
  objects; the exhaustive force-load probe therefore resolves them even though
  the planned FNA3D path is Metal/Vulkan.
- `libc++`: MoltenVK C++ objects. Objective-C runtime symbols are already
  resolved by the required Apple frameworks, so a redundant `libobjc` flag is
  not added.

The Stage 1 report records the final empirically minimized set and any item
whose removal makes a probe fail.

## tvStubs rule

tvStubs supplies 25 Windows, Android, WinRT, Linux, and Emscripten SDL symbols
retained by the pinned cross-platform managed bindings. A tvOS call to any of
them would be a bug. Verification requires all 25 exports and fails if any
symbol is also supplied by SDL2, FNA3D, FAudio, Theorafile, or MoltenVK.

The locked native-builder checkout contains no standalone license file. The
generated license inventory records that absence explicitly; redistribution of
tvStubs therefore remains a licensing-review item even though this port is for
personal use.
