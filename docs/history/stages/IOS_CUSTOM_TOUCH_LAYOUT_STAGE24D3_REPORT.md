# Stage 24D3 — custom touch layout editor and per-input Grab behaviour

Status: **PASS**

Stage 24D3 extends the controller-complete Stage 24D2 iPhone/iPad product with
a transactional custom touch-layout editor. It retains one canonical generated
Celeste pipeline, direct FNA3D Metal, real seven-bank FMOD, ordinary atomic iOS
save files, full AOT, and the accepted tvOS product boundaries.

## Baseline and repository policy

- Starting commit: `f00896a344ef03347d13a03977a7609961022aee`
- Feature branch: `feature/modern-ios-custom-touch-layout`
- Final feature commit: the accepted branch tip containing this report
- No branch was merged, no release tag was created, and no GitHub Actions job
  was run
- Deferred RC3 and all existing release-candidate tags remained untouched

Generated/decompiled game source, Celeste Content, FMOD inputs, device evidence,
saves, signed products, and signing details remain ignored. The tracked change
contains only repository-owned policies and transforms, tests, documentation,
and this report.

## Migration and schema

The layout codec is version 2. Stage 24D2 preferences migrate once into a
validated layout with the same effective default controls, opacity, movement
mode, sliding choice, and Grab intent. The old Toggle, Hold Button, and Shoulder
Hold values map to source-aware Celeste Grab modes and appropriate geometry;
they are not silently discarded.

Phone and Tablet use independent app-private keys. Profiles contain normalized
full-display coordinates rather than pixels or device-model names. Rotation,
different drawable sizes, iPhone pillarboxing, and iPad letterboxing therefore
materialize the same relative design against the current physical display.
The safe area is retained as a faint editor guide, not a placement limit.

Missing, malformed, wrong-version, out-of-range, undersized, overlapping, or
otherwise invalid data fails closed to the current device class's factory
layout. Touch-layout preferences remain separate from Celeste Settings,
SaveData, backups, and tvOS Stage 9B persistence.

## Editor UX and geometry

**Options > Touch Controls > Edit Layout** opens a transactional overlay on the
real game presentation. Selection, move, and resize use direct touch. A compact
tool palette can be hidden and moved between screen edges so it does not block
top controls or the area being edited. Text and touch targets were enlarged
after physical review.

The editor provides:

- **Done**, enabled only for a valid layout;
- **Cancel**, which restores the complete pre-edit working state;
- **Undo**, covering geometry and property changes;
- **Reset Selected** and **Factory**, scoped to the current Phone/Tablet class;
- **Mirror**, as a useful left-handed starting point;
- split orientation, Jump/Dash assignment, shape, opacity, add, duplicate, and
  delete tools as applicable to the selected control.

Rectangles resize freely in width and height. Circles preserve a physical
circle by deriving normalized width/height from the current display aspect.
The editor's solid movement/action disk uses the same centralized visual-to-hit
ratio as gameplay; a faint larger circle represents the intentional hit target.
This fixes the misleading D3 prototype in which the editor movement disk looked
larger than the runtime disk. Only the visible disk must remain on the display:
its larger invisible hit circle may clip at an edge, so visible circular
controls can sit truly flush in any corner. The resize handle follows the
visible disk and therefore remains reachable at the right and bottom edges.

All controls are bounded to the complete display, including rendered black-bar
regions. Pairwise shape-aware overlap checks cover circle/circle,
rectangle/rectangle, and circle/rectangle combinations. Invalid overlap,
off-screen geometry, minimum-hit-size failure, or an incomplete essential
inventory disables Done and shows a specific correction message.

## Editable controls and optional controls

The primary inventory is:

- Fixed movement disk or Floating movement activation region;
- separate Jump and Dash, or one Split Action Region;
- Grab as a circle or arbitrary rectangle;
- Pause;
- Journal/Special.

Movement, the selected Jump/Dash scheme, one Grab, and Pause are essential.
Journal is present in the factory layout but may be deleted. Up to four optional
controls may be added from Jump, Dash, Grab, Pause, Journal, Crouch Dash, and
Quick Restart. An audit of Celeste's complete logical-input inventory found no
other missing distinct action: Talk/Menu Confirm shares Jump, Menu Cancel
shares Dash, directions/aim share Movement, and ESC behavior is already covered
by Pause/Cancel. The editor therefore exposes all useful unique actions without
cluttering the palette with aliases.
Optional controls can be moved, resized, reshaped where meaningful, assigned
individual opacity, duplicated, and deleted. Essential primary controls cannot
be deleted into a controller-required state.

Editor selection names are semantic rather than storage-oriented. A lone
optional action is simply `Crouch Dash` or `Quick Restart`; only real duplicates
of the same action receive an ordinal. A late physical pass caught the former
`Extra0`…`Extra3` slot index leaking as misleading labels such as `Crouch Dash
2` and `Quick Restart 4`; the final generated tree removes that implementation
detail.

Quick Restart uses Google's `autorenew` Material Symbol and Crouch Dash uses
`motion_blur`; both are normalized through the same deterministic 128 x 128 A8
pipeline as the accepted touch artwork. A proposed automatic Jump/Dash to
Confirm/Cancel glyph swap was deliberately not added: Celeste has separate
virtual inputs but no single authoritative global menu-mode signal. Scene or
pause allowlists would mislabel special interactions such as postcards,
cutscenes, deaths, and naming screens. Input behavior remains correct in every
context without introducing that visual heuristic.

Every primary and optional control has 0–100% opacity in 10% steps, multiplied
by the existing global opacity master. A zero-opacity control remains visible
with an editor outline and remains interactive in gameplay by design.

Duplicated controls aggregate by logical action. A held action stays active
until its final owning finger releases. A second simultaneous Grab owner does
not create a second Toggle transition or duplicate haptic; a new press after
all owners release is a new logical press. This supports two compact upper
corner Grab regions without forcing one large region through Pause/Journal.
No macro, timed action, or arbitrary logical remapping was introduced.

## Split Action Region

Jump and Dash can remain ordinary independent controls or share one resizable
rectangle. Four splits are supported:

- top-left to bottom-right diagonal;
- top-right to bottom-left diagonal;
- vertical;
- horizontal.

The two halves display their actual action glyphs in both editor and gameplay.
Each half independently changes to its Jump-blue or Dash-pink pressed colour
while held.
**Swap Jump / Dash** reverses the assignments without changing geometry.
Deterministic boundary ownership avoids a dead seam. Existing Button Sliding
Off/On behavior is preserved for separate buttons and Split Region halves.

## Source-aware Grab

Grab geometry no longer chooses Grab behavior. Celeste's existing **Grab Mode**
row is source-aware on iOS and shows the active source being configured. Touch,
Controller, and Keyboard each persist an independent Hold, Invert, or Toggle
profile. Touch=Toggle and Controller=Hold can therefore coexist and are restored
when the meaningful active input source changes.

The arbiter owns one effective logical Grab state. Hold follows the aggregated
owners; Toggle changes only on a new aggregate press; Invert is active by
default and releases while physically held. Artwork and tint use that same
effective state, so Toggle and Invert never display a value different from
gameplay. Source changes clear transient owners/edges before restoring the new
source profile, preventing stuck or flickering Grab.

A final regression found that the generated gameplay bridge had accidentally
gated all Grab sources behind touch-overlay visibility. Automatic correctly
hides touch when a controller connects, but that also made physical-controller
Hold, Invert, and Toggle read false. `GrabSourceVisibilityPolicy` now suppresses
only a hidden **Touch** source; Controller and Keyboard remain valid while the
touch overlay is hidden. Deterministic coverage locks all four combinations.

## Physical acceptance

The evolving D3 product received repeated same-identity iPhone review during
implementation. Physical evidence established the editor crash fix, readable
tools, direct move/resize, free rectangle aspect ratios, circular geometry,
four Split Region modes, glyph assignment, overlap rejection, Undo/Cancel,
full-display placement, per-control opacity, duplicated controls, rotation,
**Directional Haptics**, touch Grab profiles, controller coexistence, gameplay,
audio, and save continuity.

The exact final binary was installed as a same-identity replacement on both the
physical iPad mini running iPadOS 15.8.8 and the iPhone 15 Pro Max. The iPad
retained launch, audio, saves, rotation, gameplay, editor behavior, icon-only
Quick Restart/Crouch Dash art, and the corrected semantic selection labels. A
final iPhone replacement then verified normal launch/gameplay and confirmed
that a lone optional action has no number while true same-action duplicates are
numbered semantically. No uninstall or data reset was used on either device.

## Saves, audio, latency, and lifecycle

No save serializer or format changed. Same-identity installation is required to
preserve Settings, slots 0–2, prior-good backups, controller prompt preference,
and migrated touch preferences. Resetting or editing touch controls cannot
mutate Celeste Settings or SaveData.

One FMOD runtime still loads the same seven banks. Editor entry/exit does not
reinitialize audio or graphics. Orientation, background/foreground, controller
takeover, touch cancellation, and coordinator disposal clear transient owners.

The layout path adds no intentional frame queue: physical touch is classified
during the same update path accepted by D2. D2's final measured touch-to-logical
mean was 0.237 ms (0.990 ms maximum) and touch-to-update mean was 0.281 ms
(1.038 ms maximum), with zero buffered frames. D3 changes geometry lookup and
ownership aggregation, not the update cadence; physical gameplay remained
effectively immediate.

## Locked output and package

| Boundary | Files | Logical SHA-256 |
| --- | ---: | --- |
| Canonical Content | 1,216 | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw source | 920 | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched source | 922 | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Shared Stage-6 real-audio tree | 934 | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| Stage 24D2 iOS input tree | 938 | `5ca5e70d1fc163aa75db780d1cb7fc35b9328eea42369b91a4a2776dd8651036` |
| Final Stage 24D3 iOS tree | 942 | `99c9df179036e70c1e9a01dbcde129d3a9e5eef9495d3962f256c3cfa4d1b329` |

- iOS native logical hash:
  `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`
- tvOS native logical hash:
  `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`
- Target: `net10.0-ios26.5`, minimum iOS/iPadOS 15.0, `ios-arm64`
- Configuration: Release, LLVM full AOT, full trim, `UseInterpreter=false`,
  no JIT
- Final signed build duration: 291 seconds
- Signed IPA size: 881,050,378 bytes
- Signed IPA SHA-256:
  `386908f4515e67ce7cc7199f28131713661142466af053a112e7e45f0173c851`
- Package verifier: PASS

The direct .NET package build and the public build wrapper both consumed the
same locked 942-file tree. Neither native dependency set changed.

A final icon-only rebuild exposed a subtle build hygiene defect: `--clean`
removed the packaged output but retained host/generated-project Mono AOT
intermediates. The managed assembly contained the new resources while the
native executable still ran the previous draw method. A genuinely clean rebuild
changed the native executable and produced the intended icon-only controls.
`build-ios-celeste.sh --clean` now removes only the four exact ignored host and
generated-project `bin`/`obj` roots before publishing, preventing stale AOT
method bodies without touching inputs, saves, or native dependency caches.

The subsequent selection-label correction exposed the separate generated-tree
boundary: a clean AOT build can still compile an old ignored generated source
tree if tracked templates were changed without rerunning preparation. The
device builder now compares all three directly copied D3 templates to their
prepared counterparts and fails with an explicit regeneration instruction on
any mismatch. The accepted final build was regenerated from the exact supported
itch.io Linux profile before its clean AOT publish.

## Deterministic and inherited regressions

- Modern iOS foundation/touch policy: 226 tests
- Stage 24D3 source/product/package verifier: 164 checks
- Modern iOS durability/fault suite: 76 tests
- Stage 24C2 verifier: 87 checks
- Stage 24D2 inherited verifier: 153 checks
- Exact input profiles: 56 tests
- Save Manager protocol/security: 66 tests
- Save Manager continuity: 57 tests and 85 current checks
- Controller Prompts: 38 tests
- graceful Quit: 16 tests
- soft reload: 21 tests
- QR pairing: 31 tests
- Metal Performance HUD: 39 tests
- iOS native verifier, signed package verifier, host doctor, documentation
  links, repository/privacy, shell syntax, Python compilation, and
  `git diff --check`: PASS

The historical Stage 6 wrapper intentionally rejects later shared-iOS/FNA
foundation changes relative to its old baseline. It remains byte-for-byte
unchanged. Its ordinary persistence policy and all current tvOS behavior suites
were verified directly; this is historical-verifier treatment rather than a
suppressed product failure. No Apple TV product/runtime file changed, so no new
physical Apple TV installation was warranted.

## Privacy, licensing, limitations, and next stage

Stage 24D3 adds no networking, analytics, cloud behavior, permissions, sensor
input, or arbitrary user macros. Its app-private preferences remain within the
existing UserDefaults privacy declaration. Quick Restart uses Google's
`autorenew` Material Symbol and Crouch Dash uses `motion_blur`, both under the
same Apache 2.0 terms as the accepted D2 Google artwork. All other
editor/duplicate artwork retains the already reviewed attribution.

Current limitations:

- the modern iOS lane remains a developer-built experimental product rather
  than the root beginner builder or an App Store release;
- layouts support a bounded four optional controls, not an unbounded generic
  macro/remapping system;
- phone and tablet layouts are separate, but there is not yet a user-facing
  import/export format for layout sharing;
- real-Celeste Simulator audio remains unavailable because the accepted FMOD
  1.10.09 input lacks an arm64 Simulator slice.

After physical final acceptance, the appropriate next stage is focused
normal-user iOS packaging/import-export and release acceptance. Any build-time
static-AOT Everest/mod experiment should remain isolated from this accepted
unmodified-game product.
