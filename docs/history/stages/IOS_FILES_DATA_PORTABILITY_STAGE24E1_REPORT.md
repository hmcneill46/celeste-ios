# Stage 24E1 — Files-native data portability and touch-layout sharing

Status: **PASS**

Stage 24E1 adds normal-user iPhone/iPad backup, transfer, import, recovery, and
touch-layout sharing through Apple's standard Files and Share interfaces. The
authoritative live state remains private, atomic Celeste state; no iOS LAN
server, raw Documents exposure, new game format, or automatic cloud sync was
introduced.

## Baseline and repository policy

- Starting commit: `7c8580c4064268a3a4dccec6b000af1c46b963ac`
- Feature branch: `feature/modern-ios-files-data-portability`
- Production implementation commit:
  `2a5cb1a255891774ab898d45ecdfd4183da7d371`
- Final feature commit: the accepted branch tip containing this report
- RC1 remains `ee52b0868df091746f134d95d4f020f94f23d4fb`
- RC2 remains `641e86e4ed164cdf93f602ce2f11436449654d6e`
- Deferred RC3 remains `c8134c8ca7924cf12f48527e714b5242c6024927`
  on `origin/release/v1.0.0-rc.3`; no RC3 tag exists
- No merge, release tag, upstream push, or GitHub Actions job was performed

Generated/decompiled game source, game Content, FMOD inputs, signed products,
external documents, saves, device evidence, and signing details remain ignored.

## Product architecture

`Options > Data & Files` is an iOS-only Celeste menu in the Apple-specific
Options block below Speedrun Clock, alongside Controller Prompts. It exposes
logical Save Slots 1–3 and Settings, not implementation directories. Per-file
menus provide Export to Files, Share, Import, and Restore Previous. The root
also provides Export All Saves and destination-independent Import Save.

The UIKit host owns one process-lifetime `IOSFilePortabilityCoordinator` and
presents `UIDocumentPickerViewController` and `UIActivityViewController` above
the existing FNA view. Generated Celeste code sees only bounded bytes,
semantic documents, and completion callbacks. It never receives an external
URL. Destination, overwrite, and recovery choices use ordinary Celeste menu
buttons so touch and physical-controller confirmation share the proven game
input loop.

Live files remain beneath `Library/Application Support/Celeste/Saves` and
`Backups`. `UIFileSharingEnabled`, in-place Documents access, persistent
security-scoped bookmarks, iCloud containers, App Groups, custom networking,
and background synchronization remain absent.

## External document boundary

Imports use picker copy semantics, security-scoped access where supplied by the
provider, an `NSFileCoordinator` read, regular-file and size checks, and a
private byte copy. Access is always released. No external provider becomes
the save authority and no external path is retained.

Exports create exact logical bytes in a unique app-private temporary directory.
Files or the system Share sheet receives those copies. Completion and
cancellation remove the temporary tree; startup also removes an abandoned
export tree left by termination. iPad Share uses a valid centred popover
anchor. UIKit presentation clears transient touch ownership, and return does
not synthesize a held gameplay input.

Physical timing evidence isolated an iOS 26 UIKit alert interaction defect,
not slow persistence: the picker and slot alert detached in 58 ms and 42 ms,
respectively, while an alert action remained highlighted for 109 seconds until
additional taps caused its handler to fire. The following durable write took
about 22 ms. The accepted boundary therefore keeps only document/share UI in
UIKit, waits for that exact controller to detach, and performs every semantic
choice in Celeste's input loop. The bounded public-UIKit dismissal fallback
remains for a defective provider controller. Privacy-safe phase timings contain
only a sequence, phase, elapsed milliseconds, and controller class—never an
external filename or path.

The same diagnostic pass exposed a separate provider-boundary crash: an
unreadable selected item caused a managed `IOException` to escape an
`NSFileCoordinator` native callback, which Objective-C treated as uncaught.
The final coordinator catches expected read/validation failures inside that
callback, returns a bounded semantic failure to managed code, and never lets
an external-file error cross the native block. Build 4 physically repeated
valid import, replacement, recovery, cancellation, and invalid-item handling
without a crash.

The modern Apple port now has a product identity separate from Celeste's game
version. The accepted build displays `iOS PORT v0.1.1 • BUILD 4` at the bottom
of Options and carries matching `CFBundleShortVersionString`/`CFBundleVersion`
values. Semantic major/minor/patch identifies the port release; the monotonic
bundle build distinguishes installed revisions without changing Celeste
1.4.0.0.

## `.celeste` import/export and recovery

`.celeste` is declared as an **imported** existing XML/content type rather than
a project-owned type. The picker filter is only UX; canonical Celeste
deserialization remains authoritative.

- Slot exports are exact ordinary `Celeste-Slot-1.celeste` through
  `Celeste-Slot-3.celeste` bytes.
- Settings export is exact ordinary `Celeste-Settings.celeste` bytes.
- Export All presents every existing Settings/slot document together; it does
  not invent an archive or wrapper.
- Root save import asks for Slot 1, 2, or 3 independently of the filename.
- Import from a slot submenu has an explicit destination.
- Existing destinations require an explicit Replace confirmation and explain
  that a prior copy is retained.
- SaveData and Settings use their distinct canonical validators. Supplying one
  to the other path produces a specific rejection and no mutation.
- Accepted bytes commit through `CelesteFileDurabilityStore`, including its
  single-writer boundary, atomic replacement, previous-good rotation,
  validation, and exact post-write readback.

Save imports invalidate Celeste's normal file-select inventory. Safe Settings
imports use `Settings.Reload`, `Input.Initialize`, and `Input.ResetGrab` rather
than constructing another runtime. Imports and restores are blocked while a
`Level` is active or `UserIO` is saving; export and Share remain harmless copy
operations.

`Restore Previous` first validates the recovery copy, atomically installs and
verifies it as primary, and only then rotates a valid former primary into the
previous-good position. A successful full rotation is therefore naturally
reversible by choosing Restore Previous again. A failure in the final undo
rotation cannot damage the already verified restored primary.

## Touch-layout documents

Touch Controls provides Export Layout, Share Layout, and Import Layout. The
project-owned `.celestetouch` format is deterministic bounded JSON:

- document format version: 1;
- maximum size: 32 KiB;
- optional Phone and Tablet Stage 24D3 schema-v2 profile strings;
- strict property, version, depth, codec, and structural validation.

The current device class selects its matching profile. Import opens that data
as the existing D3 editor's transactional working copy: Done commits and Cancel
preserves the local layout. A document that omits the other form factor does
not overwrite it. Grab geometry and per-control opacity travel with the
layout; Grab input modes, controller preferences, visibility, haptics, saves,
Settings, paths, and device information do not.

Malformed/truncated/oversized JSON, unsupported versions, duplicate or unknown
properties, missing essentials, invalid actions/splits, non-finite geometry,
overlap, off-display controls, and excessive inventories fail before preview
or preference mutation. Save corruption and transaction fault coverage likewise
proves that invalid XML, type confusion, bounds failures, cancellation, commit
failure, and readback failure cannot silently replace valid live state.

## Physical acceptance

The feature evolved through same-identity full-AOT replacement builds, without
uninstalling either device.

On the physical iPhone 15 Pro Max, final Build 4's Files picker, slot and Settings export,
Share and cancellation, slot choice, explicit overwrite, desktop-valid save
import, visible imported progression, previous-good restore and undo, Settings
import/restore, touch-layout export, validated editor preview with Done/Cancel,
active-Level mutation block, cold persistence, landscape restoration, touch,
controller, audio, and lifecycle paths all passed. Slot selection, replacement,
and recovery were immediate after the UIKit-alert removal; repeated use also
confirmed that an unreadable external selection no longer crashes the process.
The final package was delivered as a same-identity replacement without relying
on an uninstall.

On the physical iPad mini 4 running iPadOS 15.8.8, the same final Build 4 package
preserved existing state and passed native picker/popover presentation,
SaveData and Settings import/recovery, fast chained confirmations, the
Apple-specific menu location, touch-layout Files flows, gameplay after return,
save persistence, 180-degree rotation, touch/controller input, and audio. This
proves the implementation does not depend on an iOS-16-only API despite being
built with the current SDK.

An ordinary desktop-valid `.celeste` save imported through Files, appeared in
the chosen iOS slot, loaded and saved normally, survived a cold launch, and
remained ordinary canonical state when exported. Files-based copies were also
used between the owned iPhone/iPad test environment without any custom
transfer service; the receiving device displayed and played the expected
state. No external test document is tracked.

## Locked output

| Boundary | Files | Logical SHA-256 |
| --- | ---: | --- |
| Canonical Content | 1,216 | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw source | 920 | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched source | 922 | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Shared Stage-6 real-audio tree | 934 | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| Stage 24D3 iOS tree | 942 | `08dcf0f324254dc31235b27d63b4144bad2a74079eb523d2c173117df1299e47` |
| Final Stage 24E1 iOS tree | 944 | `2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357` |

- iOS native logical hash:
  `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`
- tvOS native logical hash:
  `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`
- Build: Release `ios-arm64`, LLVM full AOT, full trim,
  `UseInterpreter=false`, no JIT
- Clean build duration: 250 seconds
- Signed IPA size: 881,452,904 bytes
- Signed IPA SHA-256:
  `74d2df493b448d030b90aac4314e6c0ae9881fffacdef880590c8cc61c1ea6a6`
- IPA ZIP integrity and device package verifier: PASS

## Verification

- Stage 24E1 source/product/package verifier: 95 checks
- Modern iOS foundation and portability policy: 243 tests
- Modern iOS durability/fault suite: 87 tests
- Stage 24C2 verifier: 87 checks
- Stage 24D2 verifier: 153 checks
- Stage 24D3 verifier: 164 checks
- Exact game-input profiles: 56 tests
- Save Manager protocol/security: 66 tests
- Controller Prompts: 38 tests
- graceful Quit: 16 tests
- soft reload: 21 tests
- QR pairing: 31 tests
- Performance HUD: 39 tests
- Save Manager continuity: 57 tests plus 85 current product checks
- iOS and tvOS native hashes, host doctor, plist/shell/Python syntax, package
  inspection, documentation links, repository/privacy, and `git diff --check`:
  PASS

The three iOS-only native-use patches are explicitly excluded from the
historical tvOS Stage-6 source-isolation comparison; they remain reviewed iOS
adaptations and do not change tvOS source, native artifacts, or behavior.

## Privacy and limitations

The product adds no analytics, LAN listener, iCloud entitlement, background
sync, persistent external authority, or new native dependency. System Files,
iCloud Drive, AirDrop, and Share destinations are chosen explicitly by the
user. External filenames and paths are not tracked or placed in acceptance
evidence.

Open-In from Files is intentionally deferred. Clean cold/warm document handoff
would require a new safe-state request queue and must never silently import a
document or start a second runtime. A single backup archive is also deferred:
the four canonical documents already export together, while another package
format would add AOT/parser/versioning surface without improving the core E1
portability goal.

## Recommended Stage 24E2

Stage 24E2 should productionise the **normal-user iOS build and release
acceptance** around this now-complete game/input/data product: a concise public
iOS builder entry point, prerequisite/signing/install guidance, unsigned and
same-identity package verification, clean-clone reproduction, beginner-facing
iPhone/iPad docs, and an integrated physical release-candidate matrix across
touch, controller, audio, rotation, persistence, Files transfer, and layout
sharing. It should not change save/layout formats. Open-In and a versioned
single-file backup package should remain separate optional follow-ups unless a
specific user need justifies their additional lifecycle and format surface.
