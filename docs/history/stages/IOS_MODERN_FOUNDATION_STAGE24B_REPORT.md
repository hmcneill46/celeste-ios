# Stage 24B — modern iOS native/FNA foundation

Status: **PASS**

Stage 24B converted the Stage 24A feasibility result into a tracked,
reproducible modern iOS/iPadOS foundation. This is infrastructure, not a
playable Celeste build: no generated Celeste code, proprietary content, touch
controls, game saves, Save Manager, or game-audio banks are present.

## Baseline and scope

- Starting commit: `98b3d14459e124301919bec3b054c5348d4ddfb2`
- Feature branch: `feature/modern-ios-foundation`
- Accepted implementation commit: `9b0402d4508366a7c13512bf978a577eb9a1b69d`
- Stage 24A decisions retained: iOS 15.0 minimum, iOS 26.5 compile SDK, one
  iPhone/iPad target, direct Metal, arm64 device and Apple-silicon Simulator,
  device-only FMOD, full AOT, no interpreter, and Application Support storage
- Existing tvOS product sources and legacy Xamarin sources are unchanged
- No GitHub Actions workflow was run or modified

The new code lives under `modern-ios/`, avoiding any collision with the legacy
Xamarin project. Native locks and the narrow iOS-only compatibility source live
under `native/`; reproducible orchestration and verification live under
`scripts/`.

## Toolchain

- macOS 26.3 arm64
- Xcode 26.6 (17F113)
- iPhoneOS and iPhoneSimulator SDK 26.5
- .NET SDK 10.0.302; workload set 10.0.302.0
- iOS workload 26.5.10301
- Target framework `net10.0-ios26.5`; minimum iOS 15.0

The host doctor rejects material drift from this accepted stack.

## Reproducible native foundation

Two independent clean builds produced byte-identical normalized manifests and
the same logical set SHA-256:

`9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`

The post-push clean-room build also reproduced this lock from a disposable
macOS `/private/tmp` clone. That pass exposed Xcode's canonical `/tmp` spelling
inside one SDL `__FILE__` value; the builder now maps both names to the same
stable logical source root. No compiled instruction, export, or runtime source
changed, and the accepted native hash remains the value above.

| Component | Pin/source | Device | Simulator | Role |
|---|---|---:|---:|---|
| SDL2 | `257cacab183b312bbe60bd7967eee44a3ad7be85` | arm64 IOS | arm64 IOSSIMULATOR | UIKit, public `SDL_UIKitRunApp`, controller/touch/CoreMotion support |
| FNA3D | `dba98a71514cc30f3c19dc19fc0a479be7c90d52` | arm64 IOS | arm64 IOSSIMULATOR | direct Metal renderer |
| FAudio | `0e39055a3fb3c27de8dc02d650fe6a3da6c4de2c` | arm64 IOS | arm64 IOSSIMULATOR | complete FNA audio closure for later integration |
| Theorafile | `0c5504658a3108919e53b625287786a87529de42` | arm64 IOS | arm64 IOSSIMULATOR | video-decoding closure |
| ApplePlatformStubs | tracked repository source | arm64 IOS | arm64 IOSSIMULATOR | exactly 25 reviewed foreign-platform static-link exports |

SDL is built without its standalone UIKit `main`; the modern .NET entry point
owns the process and hands off through the public `SDL_UIKitRunApp` callback.
The pinned pregenerated SDL Metal shader header is used, so the optional Xcode
Metal Toolchain component is not required. The iOS product contains no
MoltenVK. The iOS compatibility library does not contain the tvOS Performance
HUD bootstrap.

The accepted tvOS native logical hash remains exactly:

`6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`

## Modern host and presentation

`CelesteIOSRuntimeHost` is a direct .NET for iOS application rather than MAUI.
It owns one process entry, one SDL UIKit handoff, one FNA `Game`, and one Metal
presentation layer. The SDL patch supplies a true `UIWindowScene` delegate and
creates its window from the active `UIWindowScene`; the obsolete window warning
from Stage 24A is absent.

Both iPhone and iPad device families advertise only landscape left and
landscape right. Runtime verification resolves the real SDL window to its
UIKit `UIWindow`, requires a `UIWindowScene` and `CAMetalLayer`, checks that the
Metal drawable equals point bounds multiplied by `NativeScale`, validates the
safe area, and rejects a portrait framebuffer. Safe-area values are exposed to
the host but do not crop the game presentation.

The probe renders to a 1280×720 logical target, then uses centered aspect-fit
presentation with point sampling. Modern wide iPhones use pillarboxing where
needed; the iPad test proves 4:3 letterboxing. No physical resolution is
hardcoded.

## Simulator acceptance

Release `iossimulator-arm64` was built with full trimming, LLVM AOT, and no
interpreter. Both dedicated iOS 26.5 runs passed:

- modern iPhone Simulator: Retina direct-Metal first draw, animated/sustained
  frames, safe-area and wide-aspect verification, background/foreground on the
  original FNA runtime;
- modern iPad Simulator: Retina direct-Metal first draw, 4:3 aspect-fit
  letterboxing, sustained frames, and same-runtime background/foreground.

The simulator lane deliberately defines `IOS_SIMULATOR_NO_FMOD`, disables FNA
sound, and logs the policy. FMOD 1.10.09 has no arm64 Apple-silicon Simulator
slice; the no-audio lane is explicit rather than a native-load failure or a
substitution of a different SDK.

## Physical iPhone acceptance

Automatic development signing created a normal local development profile for
a distinct foundation bundle. A same-identity signed Release build installed
and launched on the paired physical iPhone without overwriting tvOS or legacy
iOS software.

The accepted run proved:

- one managed host startup and one FNA game loop;
- direct FNA3D Metal on the physical Apple GPU;
- true landscape `UIWindowScene`/`CAMetalLayer` presentation;
- 3× native Retina drawable sizing, centered 1280×720 aspect fit, and valid
  safe-area metrics;
- animated first draw, sustained 60 fps, both landscape orientations, and
  repeated Home/foreground recovery without a second runtime;
- no unhandled exception, AOT/JIT failure, missing native symbol, Metal error,
  scene duplication, or interpreter fallback.

The exact external FMOD Engine iOS/tvOS 1.10.09 build 97915 low-level and
Studio archives were linked after the same narrow six-symbol localization used
by the accepted Apple TV architecture. The physical run created, versioned,
initialized, updated, suspended/resumed, and retained the Studio/low-level
foundation successfully. Celeste banks were intentionally not loaded.

An acceptance-harness termination exposed and fixed a narrow late-callback
race: once FMOD disposal begins, subsequent update/suspend/resume callbacks now
fail closed against the disposed foundation. The corrected build cleanly owns
and releases the native handles without allowing a late activation event to
touch them.

The final signed IPA is 5,096,945 bytes with SHA-256:

`fe7d1b90db6cb96558fd82d31492a328306f5c68d7486e3de8a0410c12ae9fb8`

Package inspection reports arm64 IOS, minimum iOS 15.0, SDK 26.5, complete AOT
data for every retained managed assembly, full trim, `UseInterpreter=false`,
and no JIT entitlement. The only trim diagnostics are reviewed upstream FNA
attribute/vertex-constructor annotations; the exercised foundation paths are
AOT-complete.

## Controller and storage boundaries

The controller foundation uses FNA's existing SDL `GamePad` path and feeds a
small movement state into the probe. It does not add a parallel Apple
GameController observer system and no controller is required for startup.
`TouchControllerState` is only a bounded state contract for later work; no
touch UI or virtual controls exist.

The storage root is obtained through public Foundation APIs and resolves under
the app sandbox at `Library/Application Support/Celeste/`. Runtime startup
creates only the directory, never the four Celeste files. `AtomicFileStore`
accepts only `settings.celeste` and slots `0.celeste`–`2.celeste`, writes a
sibling temporary file with write-through and disk flush, then atomically
replaces the destination. Fault-injection tests prove old-file preservation,
temporary cleanup, first write, replacement, idempotent directory creation,
and traversal/unknown-name rejection.

The privacy manifest declares no tracking or collected data. It declares only
the file-timestamp API category with reason `C617.1`, limited to files inside
the app container. There is no Documents sharing, listener, Bonjour service,
Save Manager, cloud service, analytics, or unrelated entitlement.

## Verification and regressions

- Stage 24B source/policy verifier: 54 checks
- Foundation deterministic tests: 31 tests
- Native verification: all five components, both Apple platforms, both arm64
  slices, exact export/platform/minimum-SDK contracts, and link probes
- Package verification: device and Simulator lanes pass
- Current tvOS chain passes: Stage 10 protocol 66, Controller Prompts 38,
  graceful Quit 16, soft reload 21, QR 31, Performance HUD 39, and Save
  Manager continuity 57 policy tests plus 85 source/product checks
- Stage 9B persistence, Stage 8B repository/docs, and Stage 14 product
  verifiers pass
- Repository link/privacy checks pass; no proprietary FMOD/Celeste input,
  generated product, signing identity, device identifier, or private path is
  tracked

RC1 remains `ee52b0868df091746f134d95d4f020f94f23d4fb`, RC2 remains
`641e86e4ed164cdf93f602ce2f11436449654d6e`, and deferred
`origin/release/v1.0.0-rc.3` remains
`c8134c8ca7924cf12f48527e714b5242c6024927`. No RC3 tag was created.

## Known limitations and Stage 24C

This target still does not run Celeste. It has no proprietary game input,
content pipeline, Celeste scenes, FMOD banks/game audio, save serialization,
touch controls, document import, Save Manager, Everest, App Store workflow, or
polished iPad UX. Simulator audio remains intentionally unavailable with the
accepted historical FMOD SDK.

Stage 24C should integrate the existing canonical Celeste 1.4.0.0 generated
tree and content into this one-runtime host, load all seven user-supplied FMOD
banks on physical iPhone, adapt Celeste `UserIO` to the approved Application
Support atomic boundary, and prove controller-first cold launch/gameplay/save
restore on iPhone and iPad. It should preserve the simulator no-FMOD policy and
continue to defer production touch UX to Stage 24D.
