# Stage 24D2 — production touch controls and iOS input UX

Status: **PASS**

Stage 24D2 turns the experimental modern iOS lane into a touch-playable Celeste
product without forking the canonical game pipeline or weakening controller,
save, audio, AOT, or tvOS behavior. The exact Release product passed touch-only
acceptance on a physical iPhone and an iPad mini running iPadOS 15.8.8.

## Baseline and repository state

- Starting commit: `802819d1c30a49cb31ec0b9c419be47c378952ce`
- Feature branch: `feature/modern-ios-touch-controls`
- Final feature commit: the accepted branch tip containing this report
- RC1 remained `ee52b0868df091746f134d95d4f020f94f23d4fb`
- RC2 remained `641e86e4ed164cdf93f602ce2f11436449654d6e`
- Deferred `origin/release/v1.0.0-rc.3` remained
  `c8134c8ca7924cf12f48527e714b5242c6024927`
- No branch was merged, no tag was created, and zero GitHub Actions minutes were
  used

Generated game source, signed products, device evidence, saves, and proprietary
Celeste/FMOD inputs remained ignored. The tracked work consists only of host and
policy source, deterministic transforms, open-licensed artwork source/notices,
tests, and documentation.

## D1 findings adopted

The Stage 24D1 physical interaction lab established the design that D2 retains:

- landscape safe-area-aware overlay on the real UIKit/FNA presentation;
- Fixed eight-way movement and a bounded Floating acquisition region;
- independent Opacity and Control Size rather than preset layout profiles;
- Toggle, Hold Button, and Shoulder Hold Grab styles;
- optional Jump/Dash finger sliding;
- Automatic controller takeover, Always coexistence, and lockout-safe Off;
- immediate logical input with no intentional frame buffer;
- a focused first-presentation correction for the portrait/backbuffer race.

The lab also exposed two real multi-touch bugs. SDL returns a compact active
finger array, so a release can move another held finger to a different array
index. Treating that index as identity caused both stuck controls and crossed
release behavior. A temporary broad reset avoided sticking but incorrectly
released unrelated held actions. D2 fixes the identity boundary rather than
carrying either workaround into production.

## Stable FNA touch identity

The iOS FNA project stages its pinned source and applies one exact zero-fuzz
managed patch. `StableTouchSlotPolicy` maps active SDL finger IDs back to stable
FNA/XNA slots. A vacated slot emits its Released edge for one update and cannot
be reused until the next snapshot. New fingers use only genuinely empty slots.
`TouchPanel.SetFingerSnapshot` therefore preserves every unrelated held finger
through SDL array compaction.

The policy uses bounded stack spans and fixed arrays; it adds no native change,
observer, reflection, or allocation-heavy per-frame identity table. Deterministic
tests cover array compaction, crossed releases, reordered snapshots, vacated-slot
edges, and multi-finger ownership. Physical crossed-release and four-finger
tests passed on the final iPhone and iPad product with no stuck or spuriously
released action.

## Touch architecture and movement

`TouchControlsPolicy` is platform-safe pure logic. The generated
`IOSTouchControls` bridge reads FNA `TouchPanel`, owns a maximum of eight stable
finger/action records, exposes ordinary Celeste virtual buttons/axes, and draws
one overlay after the game frame. There is no direct arbitrary menu tapping:
touch drives Celeste's normal Confirm, Cancel, and directional logical inputs.

Fixed movement is the production default and uses an invariant safe-area-relative
centre. Floating accepts the first movement finger only inside a bounded left
acquisition region and places its centre beneath that thumb. Both classify the
same eight directions with a radial deadzone of `0.18` and an 8-degree sector
hysteresis. Only one finger may own movement; action owners remain independent.
Physical tests confirmed diagonals, muscle-memory Fixed input, comfortable
Floating acquisition, variable-height movement combinations, and clean release.

Jump is a held logical input so variable-height jumps work. Dash supplies one
normal pressed edge per intended press. Pause supplies one edge. Optional
**Slide Jump / Dash** allows one still-held finger to transfer only between
those two action regions; with it Off, action ownership remains fixed until
release. A small safe-area-aware book control at the top-left supplies
Celeste's Journal/Special logical input without crowding the primary movement
and action regions. The Touch Controls screen explains sliding behavior
directly.

The final logical-input audit covered every input declared by Celeste rather
than only the common platforming path. Movement, aim, Jump, Dash, Grab, Talk,
Pause, Confirm, Cancel, menu directions, and Journal/Special all have direct
touch routes. `QuickRestart` is an optional shortcut with no default controller
binding; touch users retain the same Retry/Restart operations through Pause.
The optional `CrouchDash`/Demo Dash binding is likewise empty by default and
is an advanced shortcut rather than a gate to game content. `ESC` merely
duplicates existing Pause/Cancel behavior. Journal was the one genuinely
unique missing content/menu capability discovered by this audit, so D2 added
it directly and locked its press/hold/release behavior and prompt in
deterministic tests. A follow-up physical audit also found that Celeste's
vanilla unlock-cheat listener reads the raw one-frame Grab press rather than
the final held-Grab state used by gameplay. Touch now supplies that separate
edge to the existing listener without changing the vanilla sequence or
allowing controller Grab Mode to affect touch Grab.

## Grab, haptics, and preferences

Three production Grab styles passed real climbing:

- **Toggle** (default): a press toggles Grab and the fist artwork changes state;
- **Hold Button**: Grab remains active only while its action finger is held;
- **Shoulder Hold**: a broad safe upper-right region supplies held Grab while
  leaving Jump and Dash reachable below.

Light impact feedback is optional for Jump, Dash, and Grab-toggle actions.
Movement never pulses. Haptics Off produced no touch feedback, the older iPad
handled unsupported feedback without a crash, and DualSense rumble remained an
independent FNA controller behavior.

Validated app-private preferences are:

| Preference | Range | Default |
| --- | --- | --- |
| Visibility | Automatic / Always / Off | Automatic |
| Movement | Fixed / Floating | Fixed |
| Grab | Toggle / Hold Button / Shoulder Hold | Toggle |
| Slide Jump / Dash | Off / On | Off |
| Opacity | 0–100% in 10% steps | 70% |
| Control Size | 70–130% in 10% steps | 100% |
| Touch Haptics | Off / On | On |

Opacity never changes hit testing; 0% remained active and receives a full
preview while editing. All required opacity and size values passed physically
without overlap or safe-area escape. **Reset Touch Controls** restores only
these defaults. Same-identity install, background/foreground, orientation, and
cold relaunch preserved the chosen values independently of Celeste Settings,
SaveData, backups, and controller prompt preference.

## Options, visibility, and lockout safety

The root Options menu contains a single **Touch Controls** row beside the normal
Keyboard/Controller configuration area. It opens one standalone `TextMenu`, not
a new reflected `Oui`, containing Visibility, Movement, Opacity, Control Size,
Grab Style, Slide Jump / Dash, Haptics, and Reset.

Automatic clears every transient touch owner and fades the overlay out when a
physical controller connects; disconnect restores touch immediately. Always
allows both inputs without duplicate edges. Off deliberately hides touch, but
when no controller is available a recovery control remains, including after a
cold launch, so the user cannot permanently strand the app. Lifecycle,
orientation, cancellation, geometry reset, controller takeover, and coordinator
disposal all clear movement, held actions, toggle Grab, Pause edges, and pending
slide ownership.

## Controller prompts and touch prompts

The pure Apple controller-family selection policy now lives once in
`shared/CelesteAppleInput`. tvOS delegates to it without changing its preference
or menu implementation. iOS uses the same Automatic/manual family policy, while
retaining FNA/SDL as the sole controller input stack. Physical DualSense
Automatic now displays PlayStation artwork and preserves movement, actions,
rumble, reconnect, and chapter light color.

When touch is active, generated `GuiButton`, `GuiSingleButton`, and interaction
prompt paths use tracked neutral/action touch glyphs rather than falsely showing
Xbox. This includes the Chapter 3 hotel service bell. Prompt textures are 80 ×
80, matching Celeste's ordinary prompt atlas scale, while the overlay retains
crisp 128 × 128 alpha masks. The final sizes were physically accepted in menus,
dialog/interactions, and gameplay.

## Artwork and licensing

The deterministic asset generator converts seven reviewed SVG sources into
monochrome A8 resources with locked hashes. Jump, Dash, the neutral touch
prompt, and Journal use Google Material Symbols under Apache License 2.0. Grab state artwork
uses the free-attribution Noun Project download by Daniel Tacho, including icon
5229550, under Creative Commons Attribution 3.0; the downloaded attribution
footer is represented in the tracked notice rather than rendered in-game.
Pause and remaining simple geometry are project-owned. Provenance, modifications,
links, and license locations are recorded in the tracked
[touch-control artwork notice](../../../modern-ios/Assets/TouchControls/NOTICE.md).

## Presentation and physical acceptance

`IOSPresentationCoordinator` obtains the actual SDL UIKit window, validates
UIKit bounds/native scale/safe area against `SDL_Metal_GetDrawableSize`, and
corrects stale initial FNA `PresentationParameters` to the real Metal drawable
before the first game draw. It revalidates only on legitimate lifecycle and
orientation boundaries. The final iPhone first draw was 2796 × 1290 and was
correctly proportioned without a manual rotate. Cold launch in both landscape
orientations, background/foreground, repeated rotations, and the physical iPad
all passed without a second graphics device or FNA runtime.

The same-identity iPhone install preserved existing Settings, all three save
slots, and backups. Touch-only acceptance covered startup/title, file selection,
menus, Prologue through the hotel/Chapter 3 test area, eight-way movement,
variable Jump, diagonal Dash, climbing/Grab combinations, deaths and respawns,
room transitions, pause/resume, Settings, saves, cold relaunch, orientation,
audio, all preference matrices, controller takeover, and lockout recovery. A
20-minute touch-only endurance pass found no gameplay or lifecycle regression.
The exact final package then passed Celeste's vanilla touch-only unlock sequence:
Left, Right, Journal, Grab, Up, Up, Down, Left, Grab, Jump. Journal and all Grab
styles remained functional afterward.

The production D2 lane completed its full touch-only iPad matrix on a physical
iPad mini running iPadOS 15.8.8: title/menu, Fixed, Floating, Jump, Dash, all
three Grab styles, pause/resume, death/respawn, Settings, save, cold relaunch,
both landscape orientations, and audio passed. After the final narrow Grab
arbitration correction, the exact final signed app was installed as a
same-identity replacement. Its final focused smoke passed correct menu/aspect
presentation, Fixed movement, Jump, Dash, Journal, the selected Grab behavior,
180-degree rotation and return without presentation distortion, and continuous
audio; its three Grab styles plus crossed-release/no-stick boundary had already
passed the full matrix. The iPad's unavailable touch haptic capability caused
no error. Deterministic larger-iPad geometry also keeps hit regions bounded
without model-name special cases.

Touch latency remains effectively immediate. The accepted D1 instrumentation
over 450 physical samples measured touch-to-logical mean 0.144 ms (maximum
0.896 ms) and touch-to-update mean 0.187 ms (maximum 0.961 ms), with zero
buffered frames. The exact final D2 package independently measured its first
100 production samples at 0.237 ms logical mean (0.990 ms maximum) and 0.281 ms
update mean (1.038 ms maximum), again with zero buffered frames. The difference
is operational noise rather than a frame of buffering. Sustained physical
gameplay showed no obvious frame-rate regression, per-frame texture generation,
accumulating observers, duplicate audio, or unbounded object growth.

## Saves, audio, build, and locked outputs

Normal Settings and all three save slots remained serializer-valid across
replacement, gameplay saves, backgrounding, and cold launch. Reset Touch
Controls did not mutate a save or game setting. iOS still uses ordinary atomic
Application Support files with one previous-good backup; no format changed.

One FMOD runtime loaded the same seven banks once. Title/gameplay music,
ambience, UI/action/death effects, volume, pause/resume, and the public iOS audio
session lifecycle remained green. Touch haptics did not disturb FMOD.

| Boundary | Files | Logical SHA-256 |
| --- | ---: | --- |
| Canonical Content | 1,216 | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw source | 920 | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched source | 922 | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Shared Stage-6 real-audio tree | 934 | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| Final Stage 24D2 iOS tree | 938 | `5ca5e70d1fc163aa75db780d1cb7fc35b9328eea42369b91a4a2776dd8651036` |

- iOS transform version: Stage 24D2 after the exact 928-file C2 tree
- iOS native logical hash:
  `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`
- tvOS native logical hash:
  `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`
- Target: `net10.0-ios26.5`, iOS/iPadOS 15.0 minimum, `ios-arm64`
- Configuration: Release, LLVM full AOT, full trimming,
  `UseInterpreter=false`, no JIT entitlement or interpreter library
- Clean final build duration: 218 seconds
- IPA size: 886,411,936 bytes
- IPA SHA-256:
  `b51a7d9bea0941f2be8111ae5da8eb510ec0fb23c41602f6b606839414c946d0`
- Package verifier: PASS

## Deterministic and inherited regressions

- Modern iOS foundation/touch policy: 128 tests
- Modern iOS durability/fault suite: 76 tests
- Stage 24C2 current source/product verifier: 87 checks
- Stage 24D2 source/product/package verifier: 157 checks
- Exact input profiles: 56 tests
- Stage 10 Save Manager protocol/security: 66 tests
- Stage 22B Save Manager continuity: 57 tests plus 85 checks
- Stage 11 Controller Prompts: 38 tests
- Stage 12B Quit: 16 tests
- Stage 13B soft reload: 21 tests
- Stage 15 QR pairing: 31 tests
- Stage 16B Performance HUD: 39 tests
- Stage 9B persistence/repository isolation: PASS
- iOS native verifier, full product package verifier, host doctor, Stage 14
  feature/docs chain, documentation links, and Stage 8B repository/privacy:
  PASS

Sharing the pure prompt policy changed no tvOS behavior: its full 38-test family,
preference, artwork-only, and isolation suite remains green. No Apple TV physical
install was needed because neither the tvOS runtime bridge nor generated tvOS
product changed.

## Privacy, limitations, and next stage

The privacy manifest still declares no tracking or collected data. App-private
touch preferences use the existing appropriate UserDefaults reason. Touch adds
no network, camera, motion, location, analytics, cloud, Save Manager, Bluetooth
protocol, or new permission. The app remains full-AOT and interpreter/JIT-free.

Current limitations:

- the modern iOS lane is still developer-built and is not yet the root beginner
  builder or an App Store product;
- the exact FMOD 1.10.09 package has no arm64 Simulator audio slice;
- controls are safe-area adaptive but are not yet freely positioned or remapped;
- the accepted Grab modes do not yet include the proposed grab-by-default,
  hold-to-release option;
- only the available physical iPhone and older iPad received hardware acceptance;
  larger iPad layout is deterministic geometry evidence.

A focused Stage 24D3 is the best next step before broad packaging: add a bounded
custom-layout editor with reset/undo, overlap validation, separate phone/iPad
layouts, configurable fixed/floating activation region, draggable/resizable
Pause and Grab regions, and the proposed diagonal split Jump/Dash rectangle.
It should retain this proven button layout as a preset and prototype a cleaner
separation between **Grab placement** (lower-right button or upper-right
shoulder region) and **Grab behaviour**. The first design to test is making
every touch Grab placement honour Celeste's existing Hold, Toggle, or Invert
Grab Mode directly—including grab-by-default/hold-to-release—while keeping the
icon and colour synchronized to the true final state. That may eliminate a
redundant parallel touch-behaviour setting; if an independent override still
proves useful, it should be explicit and include **Follow Celeste**. D2 retains
its explicitly required and physically accepted independent touch policy rather
than invalidating the completed settings/controller matrices at the final gate.
Broader normal-user iOS packaging/import-export should follow that polish. The
no-JIT Everest feasibility lane should remain independent so it cannot destabilize
the accepted unmodified-game touch product.
