# Stage 16B — native Metal Performance HUD toggle

## Scope and basis

Stage 16B productionises the GREEN Stage 16A prototype on
`feature/metal-performance-hud`, starting from post-RC integration commit
`23dd9fbca854527c7e2fa3d304649e92c4c2114d`. The immutable
`v1.0.0-rc.1^{}` recovery point remains
`ee52b0868df091746f134d95d4f020f94f23d4fb`; neither it nor `tvos-port` is
changed by this feature branch.

## Architecture

- A repository-owned Objective-C constructor is compiled into the existing
  force-loaded tvStubs XCFramework. Before SDL/FNA creates the presentation
  layer, it uses public Foundation `NSUserDefaults` to set Apple's current
  `MetalHUDForceEnabled` force-default and the historical official Tech Talk
  `MetalForceHudEnabled` compatibility spelling. The latter was the spelling
  physically observed to activate the facility on the accepted tvOS release.
- These defaults provide capability only. No HUD Info.plist key, Xcode scheme
  environment, private selector, swizzle, native visibility bridge, or device
  Developer Graphics HUD setting is used.
- After `new Celeste()` creates the SDL/FNA window, one host coordinator
  follows the exact public path
  `GameWindow.Handle -> SDL_GetWindowWMInfo -> UIWindow -> root CAMetalLayer`.
  It requires a Metal device, an exact `SDL_Metal_GetDrawableSize` match, and
  an unambiguous direct presentation-layer policy. A generated, locked,
  one-shot hook immediately before Celeste's first `base.RenderCore()` call
  applies the stored preference before the first presented game frame. The
  coordinator revalidates only at focused lifecycle boundaries and fails
  closed without disrupting the game.
- .NET 10's direct `CAMetalLayer.DeveloperHudProperties` binding applies
  `mode=disabled, logging=disabled` for Off and
  `mode=default, logging=disabled` for On. Static dictionaries and one
  process-lifetime coordinator avoid per-toggle native-object accumulation.
- The generated tvOS-only Options menu uses the existing Celeste
  `TextMenu.OnOff` control. Its bridge exposes only requested state and the
  startup SDL handle; generated game code never sees UIKit or Metal details.
- On `WillResignActive`, the coordinator temporarily hides the HUD without
  changing the stored preference. This avoids a native Metal HUD failure while
  the drawable is inactive. On `DidBecomeActive`, it revalidates the real
  presentation layer and reapplies the requested state.

## Preference and isolation

The only user preference is the validated app-private key
`CelesteTvOS.PerformanceHUD.v1`, with stable `Off` and `On` values. Missing or
invalid data resolves to Off, and setting the current value is a no-op. The key
is outside Settings XML, SaveData, Stage 9B A/B, Save Manager import/export,
and the Controller Prompts preference. Existing UserDefaults privacy reason
`CA92.1` covers this host-only use.

The bootstrap force-defaults and the visibility preference have deliberately
separate roles. The pre-render hook applies the requested visibility to the
real layer before the first presented game frame, allowing a stored On state
while preventing an Off startup flash.

## Deterministic and product validation

- Stage 16B preference/layer/application/isolation tests: **39 passed**,
  including 60 alternating policy applications.
- The Objective-C source passed direct current-SDK compilation and its retained
  symbol was present in device arm64 and simulator arm64/x86_64 objects.
- The complete accepted Stage 9B–15 source suites remain unchanged in scope:
  Stage 10 protocol/security **66**, Stage 11 **38**, Stage 12B **16**, Stage
  13B **20**, and Stage 15 **31** tests.
- The combined verifier recorded **237 passes and zero failures**, including
  the prior Stage 9B verifier, Stage 10 protocol/security **66**, Stage 11
  **38**, Stage 12B **16**, Stage 13B **20**, Stage 15 **31**, and Stage 16B
  **39** test suites.

## Acceptance evidence

### Reproducibility and packaging

- Two independent native builds produced logical SHA-256
  `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`.
  SDL2, FNA3D, FAudio, Theorafile, and MoltenVK retained their accepted hashes;
  only the deliberately extended tvStubs member changed.
- Independent managed generation produced the same locked outputs: 933 files
  and logical SHA-256
  `19836963e14df335186e0de0ce460cd4c68769d2efb23e0ab7b73a69fe439e4b`
  without real-audio support, and 934 files with logical SHA-256
  `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9`
  for the accepted real-audio product.
- The final clean-feature-checkout signing-ready unsigned IPA is 895,772,699
  bytes with SHA-256
  `df4d874ad8d7224bb0e019326e8ef6bbdf9d9f6492656979201f6e8ac7b33aca`.
  It passed tvOS arm64, minimum tvOS 16.0, full trimming, full AOT,
  `UseInterpreter=false`, seven-bank FMOD, no usable signature/profile, and
  the complete product verifier chain.

### Physical Apple TV

- A same-identity Personal Team replacement retained production Stage 9B
  saves and host preferences. Startup reached a functional menu with one FNA
  game, one Metal presentation layer, one FMOD system, all seven banks, normal
  audio, and accepted controller input.
- Options exposed one native `Performance HUD` On/Off row. More than 60 live
  applications (about 30 complete Off/On cycles) succeeded without a restart,
  scene reload, logging, object accumulation, input change, or audio change.
- The Apple TV Developer Graphics HUD setting remained Off. No Xcode
  connection, HUD Info.plist key, or restart was required.
- A stored On preference displayed Apple's native HUD during cold startup. A
  240-fps recording of the final stored-Off build showed no visible HUD frame;
  live On still worked immediately afterward. The preference also survived a
  full Apple TV reboot; the rebooted app restored its saves and reached a
  functional menu with audio and controller input.
- App timing remained within normal measurement variation: median frame rate
  was 59.940 FPS while Off and 59.916 FPS while On. No measurable practical
  Off-state cost or meaningful visible-HUD regression was observed. The
  overlay itself remains diagnostic and may carry small overhead.
- Eight same-process Home/foreground cycles with requested On passed. The HUD
  hid before background and reappeared after foreground on the same validated
  layer; requested Off stayed hidden. Stage 12B Leave/Back and
  Leave/Home/reopen remained functional and never reached the old blank shell.
- A controlled Stage 15 QR Save Manager Settings mutation authenticated,
  completed, and performed the Stage 13B Confirm-driven soft reload. The main
  menu, controller, audio, prompt preference, single FNA/Metal/FMOD identities,
  and requested HUD state all remained valid.

### Defects found and closed during acceptance

1. Leaving the native HUD visible while tvOS deactivated the drawable caused
   Apple's HUD library to terminate in a native drawing callback on foreground.
   The production lifecycle now hides on `WillResignActive` and reapplies only
   after `DidBecomeActive`; eight consecutive physical cycles passed.
2. Applying stored Off only after ordinary host startup allowed an initial
   roughly quarter-second HUD appearance. A locked one-shot pre-render hook now
   applies Off immediately before Celeste's first render; a high-frame-rate
   physical recording verified no visible HUD frame.

Repository/privacy verification found no tracked credentials, signing values,
device identifiers, game assets, FMOD input, saves, or generated Celeste
source. The exact accepted feature SHA is recorded by the containing Git commit
and the final handoff; `tvos-port` and `v1.0.0-rc.1` remain unchanged.

## Known limitations

Apple controls the native HUD's layout and metric inventory, so either may
change with tvOS. The overlay is a diagnostic aid and can theoretically add
measurement overhead while visible. An unavailable or changed presentation
layer leaves the feature Off while normal Celeste gameplay continues.
