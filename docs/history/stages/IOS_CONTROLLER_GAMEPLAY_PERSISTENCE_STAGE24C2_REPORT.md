# Stage 24C2 — controller-first iOS gameplay, persistence, and lifecycle

Status: **PASS**

Stage 24C2 proves that the experimental modern iOS product is robustly playable
with a physical controller and that ordinary Celeste Settings and all three save
slots survive gameplay, replacement installs, backgrounding, and cold process
launches. The product remains experimental and controller-first: touch controls
and their normal-user UX are deliberately assigned to Stage 24D.

## Baseline and commits

- Starting commit: `212b0c97b5f58f624a8de6b116b9b36130f5cc6c`
- Feature branch: `feature/modern-ios-controller-gameplay`
- Save-durability commit: `4a537d1` (`fix: harden modern iOS Celeste save durability`)
- Gameplay/lifecycle commit: `b287d29` (`feat: complete controller-first iOS gameplay lifecycle`)
- Acceptance-record commit: the branch-tip commit containing this report
- RC1 remained `ee52b0868df091746f134d95d4f020f94f23d4fb`
- RC2 remained `641e86e4ed164cdf93f602ce2f11436449654d6e`
- Deferred `origin/release/v1.0.0-rc.3` remained
  `c8134c8ca7924cf12f48527e714b5242c6024927`
- No RC3 tag was created, no branch was merged, and no GitHub Actions minutes
  were used

All private device, provisioning, container, save, and diagnostic evidence
remained below ignored build directories. No proprietary game or FMOD payload
is tracked.

## Canonical and native locks

The project still has one canonical Celeste pipeline serving iOS, iPadOS, and
tvOS. Stage 24C2 is a second narrow iOS transform layered after Stage 24C1; it
does not contain another decompiler, game-source fork, or Content pipeline.

| Boundary | Files | Logical SHA-256 |
| --- | ---: | --- |
| Canonical Content | 1,216 | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw source | 920 | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched source | 922 | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Shared Stage-6 real-audio tree | 934 | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| Final Stage 24C2 iOS tree | 928 | `f51187556979e6970b00ac99c21e3a09c7e33d674138bd534c46a308406f1d03` |

- Canonical class: `celeste-1.4.0.0-a`
- iOS native logical hash:
  `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`
- tvOS native logical hash:
  `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`

All nine exact user-owned input profiles remain registered and passed the
56-test closed-profile suite. The accepted tvOS/native product paths did not
change.

## Durable ordinary-file architecture

iOS owns ordinary files beneath its private Application Support container:

```text
Celeste/
  Saves/       settings.celeste, 0.celeste, 1.celeste, 2.celeste
  Backups/     settings.celeste, 0.celeste, 1.celeste, 2.celeste
```

The platform-neutral `CelesteFileDurabilityStore` owns the single-writer,
validation, rotation, recovery, and bounded-copy policy. Its iOS adapter owns
path confinement and Foundation I/O. A commit:

1. accepts only `settings`, `0`, `1`, or `2`;
2. rejects an empty, oversized, or serializer-invalid candidate before any
   mutation;
3. reads and validates the existing primary and previous-good copy;
4. atomically rotates a valid changed primary to `Backups`;
5. atomically writes the new primary using public Foundation `NSData`;
6. re-reads and verifies the exact candidate bytes;
7. leaves at most one primary and one previous-good copy.

The 64 MiB per-logical-file bound is only an iOS corruption sanity limit. It is
not a tvOS UserDefaults budget and is not a persistence envelope. Foundation's
private atomic temporary file is not assigned a project-visible filename and
no accumulating generation or temp directory is created.

On load, a valid primary wins. A missing or malformed primary is repaired
byte-for-byte from a serializer-valid previous-good copy. If neither validates,
the game receives its ordinary missing-file semantics; malformed bytes are
never treated as authoritative. Application Support files retain normal device
container-backup eligibility and are not described as cloud synchronization.

## Deterministic fault and recovery results

The 76-test durability harness covers first write, no-op write, replacement,
Settings and all three slots, raw-byte verification, bounded backup count, 400
successive saves, parallel writers, path traversal, symlinks, malformed input,
both-invalid behavior, and faults at these boundaries:

- before temporary creation;
- after a temporary candidate is prepared;
- backup write failure;
- primary write failure;
- primary read failure;
- backup read failure;
- post-commit cleanup failure.

Every injected pre-commit failure preserved a valid authoritative state. A
reported native failure after an already successful atomic replacement was
accepted only when an independent read proved the exact valid candidate.
Unreadable mutation state aborts before rotation. Both-invalid state safely
returns missing; it does not fabricate or deserialize partial state.

A physical isolated-container test deliberately corrupted only the Settings
and slot-2 primaries while preserving their valid previous-good copies. Cold
launch repaired both primaries exactly, Settings loaded, and the third file was
visible through normal file select. A read-back confirmed equality with each
backup. The diagnostic process was terminated through test tooling and the
pre-test eight-file snapshot was then restored exactly.

## Settings, slots, and logical compatibility

Settings and slots 0, 1, and 2 were created and exercised through Celeste's
normal UI. All persisted across same-identity replacement and normal
app-switcher cold launches. Privacy-safe final observed sizes were:

| Logical state | Primary bytes | Previous-good bytes |
| --- | ---: | ---: |
| Settings | 4,538 | 4,536 |
| Slot 0 | 15,128 | 15,136 |
| Slot 1 | 12,015 | 14,690 |
| Slot 2 | 12,015 | 14,690 |

All eight files passed XML parsing and the same canonical Settings/SaveData
serializer validation used by the product. The final snapshot total was 92,748
bytes and contained no extra logical file or orphan project temp file.

For the cross-platform round trip, a user-owned 26,747-byte canonical desktop
SaveData file temporarily replaced isolated slot 2 through developer tooling.
iOS loaded it through ordinary file-select/`SaveData.Start`, ran gameplay,
death/respawn, and Pause → Save and Quit. The next primary remained canonical
and differed from the imported bytes, while the one previous-good copy was
exactly the imported payload. This proves the normal save advanced from the
desktop state rather than resurrecting stale iOS data. The user's original
slot and backup were then restored exactly before final acceptance.

No tvOS compressed UserDefaults envelope was copied to iOS. The shared unit is
the canonical logical `.celeste` serialization.

## Physical gameplay and lifecycle

The exact final app was installed with the same identity; existing C1 state
survived. Physical controller-first coverage included Prologue, Chapters 1 and
2, checkpoints, room and map transitions, collectibles/hazards, chapter
completion, retry, death/respawn, pause/resume, Save and Quit, and cold restore.

The accepted traces record 49 scene transitions. Across the accepted C1/C2
modern-iOS physical evidence, 21 death-routine haptic events were captured,
including eight in the Stage 24C2 processes. Every tested death completed its
audio/animation, respawned at a coherent checkpoint, and restored input.

The final active-Level trace captured 11 real background entries and 12
same-process foreground activations (plus initial activation). Each foreground
reactivated the public iOS playback audio session before requesting resume of
the existing FMOD root bus. The trace retained exactly one managed host startup,
one Celeste/FNA runtime, one FMOD initialization, and one seven-bank load.
Background/foreground was exercised during ordinary play, pause, after deaths,
and around checkpoint/save activity.

One instrumented C2 process remained alive for 37.4 minutes and included the
longest sustained gameplay session; the player reported responsive gameplay
without growing duplication or stability symptoms. Additional focused final
and recovery processes exercised lifecycle and storage independently.

Normal app-switcher termination followed by icon launch restored Settings, all
three slots, selected progression, first draw, audio, and controller input.
The intended final user test state is serializer-valid and contains no injected
corruption or temporary desktop-save fixture.

## Controller, haptics, orientation, and audio

DualSense D-pad/stick, Jump, Dash, Grab, Pause, Confirm, Cancel, and menu input
worked without double input. A real gameplay disconnect and reconnect produced
one disconnect and one reconnect event; input and haptics returned without a
runtime restart. Chapter-dependent light color and ordinary rumble, including
death, dash, climb, and dream-dash effects, were observed.

Stage 24C2 deliberately keeps the single existing FNA/SDL controller stack. It
does not add a UIKit GameController observer. DualSense currently uses Xbox
glyphs on iOS because the tvOS host preference/UI has not been copied. That is
an accepted cosmetic controller-first baseline; Stage 24D should share the pure
family-detection policy and add an iOS-appropriate Automatic/manual prompt UI
without copying tvOS presentation state.

Both landscape orientations remained aspect-correct with no portrait,
stretching, or unsafe crop.

The first sustained lifecycle candidate exposed a real iOS defect: FMOD and
all banks remained alive after foregrounding, but the OS audio session was not
explicitly reactivated, so output could become silent. The final narrow fix:

- locks SDL's public iOS audio category to playback;
- applies public `AVAudioSessionCategory.Playback` before runtime startup;
- observes interruption and route changes once;
- activates the same OS session on the main thread before resuming FMOD;
- suppresses duplicate activation work;
- never creates another FMOD system.

Physical Silent-switch On/Off testing, four initial lifecycle cycles, the final
active-Level matrix, cold relaunch, volume/mute, title music, gameplay music,
ambience, UI SFX, jump/dash/death SFX, pause/resume, and chapter transitions all
passed. Seven banks loaded once, exposing the accepted 922 events, 118 buses,
and 3 VCAs.

## Physical iPadOS 15 acceptance

The exact 886 MB signed full-AOT app was also installed on an older physical
iPad running iPadOS 15.8.8. The first local development install required the
ordinary one-time trust of its development profile; no OS security setting was
weakened. Physical acceptance then passed:

- launch and direct Metal title/main menu;
- seven-bank music and effects;
- controller menu/gameplay input;
- movement, jump, dash, death/respawn, and pause/resume;
- both landscape orientations;
- Home/foreground recovery of gameplay, audio, and controller;
- Settings/save write and normal app-switcher cold restore.

This is strong evidence for the declared iOS/iPadOS 15.0 minimum on real
hardware. It is not a claim of exhaustive performance coverage or polished
iPad touch UI.

## Build and package

- Toolchain: macOS 26.3 arm64; Xcode 26.6 (17F113), iOS SDK 26.5;
  .NET SDK 10.0.302/workload 26.5.10301
- Target: `net10.0-ios26.5`, minimum iOS/iPadOS 15.0, arm64 IOS
- Configuration: Release, LLVM, full trimming, full AOT,
  `UseInterpreter=false`, no JIT entitlement or interpreter library
- Build duration: 323 seconds
- IPA size: 886,253,516 bytes
- IPA SHA-256:
  `7baa1b21ecb876ad9b5eb595aed0dac9aef923e6bae1e8109e7c4e69bc52bccf`
- Input profile: `itch-linux-fna-1.4.0.0`
- Renderer: direct FNA3D Metal; no MoltenVK
- Device families: iPhone and iPad; landscape left/right only
- Package verifier: PASS

The product contains every managed assembly's arm64 AOT data, the shared
durability policy and its AOT image, exact Content, and seven banks. It contains
no interpreter, JIT/runtime-codegen entitlement, Celeste executable, generated
source, save, private fixture, Xamarin host, or tvOS-only service.

## Deterministic and tvOS regressions

- Modern iOS foundation tests: 31
- Modern iOS durability/fault tests: 76
- Stage 24C2 source/product verifier: 86 checks
- Stage 10 protocol/security tests: 66
- Stage 22B continuity tests: 57 plus 85 source/product checks
- Stage 11 Controller Prompts: 38
- Stage 12B Quit: 16
- Stage 13B soft reload: 21
- Stage 15 QR pairing: 31
- Stage 16B Performance HUD: 39
- Exact input profiles: 56
- Stage 9B v2 persistence policy/repository isolation: PASS
- Stage 14 frozen inventory and documentation links: PASS, 124 links
- Stage 8B repository/docs/privacy, iOS native, package, host doctor, shell
  syntax, Python compilation, and `git diff --check`: PASS

The frozen Stage 6 wrapper's historical final guard rejects any modern iOS
foundation change after its old baseline. It was preserved unchanged and its
current generated/native assertions passed before that intentional cross-era
guard; Stage 9B was therefore run with only `--skip-stage6-foundation` for the
current branch. Stage 24C2 independently verifies that current tvOS/native
product paths and both accepted native hashes remain unchanged. No regression
test was weakened.

## Privacy and boundaries

The iOS privacy manifest still declares no tracking and no collected data.
Stage 24C2 adds no networking, analytics, cloud service, Save Manager, QR,
Bonjour, Performance HUD bootstrap, Top Shelf, touch UI, or application-exit
path. Only public Foundation/AVFoundation/UIKit APIs are used at iOS platform
boundaries. Generated/proprietary files, device data, signing material, and
physical evidence remain ignored.

## Known limitations and Stage 24D recommendation

- A physical controller is required; there are no touch controls.
- DualSense is functionally correct but currently shows Xbox glyphs on iOS.
- Real-Celeste Simulator audio/execution remains unavailable because FMOD
  1.10.09 has no arm64 Simulator slice.
- No iOS Save Manager, QR, Performance HUD, document import, cloud sync,
  graceful-Leave UI, App Store workflow, or Everest/mod support is claimed.
- iPadOS 15 passed physically, but polished iPad/touch layout remains future
  work.

Stage 24C2 is ready to integrate. Stage 24D should implement one shared
controller-family policy with narrow iOS presentation, a safe-area-aware
landscape touch overlay, user-configurable touch size/opacity/layout and
remapping, seamless controller/touch transitions, iPhone/iPad layout testing,
and accessibility/persistence for those host-only touch preferences. It should
not fork generated Celeste, add another controller observer, weaken full AOT,
or copy tvOS-only Save Manager/Leave/HUD services.
