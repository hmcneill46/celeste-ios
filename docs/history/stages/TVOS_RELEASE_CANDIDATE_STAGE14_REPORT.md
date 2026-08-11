# Stage 14 — integrated tvOS release-candidate validation

Stage 14 started from feature-freeze commit
`a7a2baf1a386fbdec4f44fb33a36f151fbe2e92f`. The final accepted commit is the
commit containing this report and `tvos/stage14-release-candidate.json`; its
exact SHA is recorded in the post-push handoff because a tracked file cannot
cryptographically contain the SHA of its own commit.

## Verdict

**RC READY.** No release-blocking defect remains within the tested scope. No
runtime, generated-Celeste, persistence, networking, audio, controller, native,
signing, or package-format source changed during Stage 14. The only tracked
changes are this report, the frozen machine-readable feature checklist, its
verifier, the history index, and the current-status note.

## Toolchain and public clean build

| Item | Accepted result |
| --- | --- |
| Host | Apple silicon, macOS 26.3 |
| Xcode / tvOS SDK | Xcode 26.6 / tvOS SDK 26.5 |
| .NET | SDK 10.0.302, workload set 10.0.302.0 |
| Device output | Release `tvos-arm64`, full AOT, full trim, `UseInterpreter=false` |
| Minimum OS | tvOS 16.0 |
| Fresh Stage 1 logical SHA-256 | `61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39` |
| Unsigned IPA | 895,567,234 bytes; SHA-256 `f0fd0b9db29bf12128bd00e1153b0b7ea9c4cc633d282271e8331b93bec8b871` |
| Signed app | 1,219,915,776 bytes; same-identity Personal Team install |

A genuinely empty clone of the public fork checked out the feature-freeze
commit as the default `tvos-port` branch, initialized every recursive submodule
with the documented HTTPS rewrite, passed builder help/host doctor and exact
Celeste/FMOD validation, and rebuilt all Stage 1 native inputs without copying
ignored output. It reproduced the accepted Stage 1 hash, generated the complete
managed tree, and produced the unsigned IPA above.

Two independent managed generations were identical. The real-audio tree had
933 files with logical SHA-256
`72a3beb58a9513d028cbce86dff0fa41fe3e372079b25f5cde9f5775948cc3eb`;
the no-audio tree had 932 files with logical SHA-256
`0b2fa1fb5997832feb951adf28eac2b46df32f49cf9c2525626e8c118f494925`.
Generated source, game content, FMOD inputs, artwork, apps, IPAs, and build logs
remained ignored.

The unsigned package had one conventional `Payload/Celeste.app`, arm64 TVOS
metadata, tvOS 16.0 minimum, compiled icon/Top Shelf assets, exact seven-bank
FMOD content, no interpreter, no profile, and no usable/stale signature. It had
no User Management entitlement and no Game Mode plist key.

## Deterministic and static verification

The frozen Stage 14 checklist covers the native runtime, Metal, FMOD, Stage 9B
persistence, Stage 10B Save Manager, Stage 11 prompts, Stage 12B Quit, Stage 13B
soft reload, branding, Personal Team installation, and unsigned IPA. The final
verifier composes the existing acceptance chain rather than replacing it:

- Save Manager bounded HTTP/auth/mutation tests: 66/66 PASS;
- Controller Prompt preference/detection/isolation tests: 38/38 PASS;
- graceful Quit state tests: 16/16 PASS;
- high-level soft-reload state/ticket/failure tests: 20/20 PASS;
- Stage 9B v0/v1/v2 compression, corruption, limits, and allow-list gates: PASS;
- repository/privacy, documentation-link, iOS archive-hash, generated-source,
  signed-app, and unsigned-IPA gates: PASS.

The first no-option invocations of the new Stage 14 wrapper in fresh clones
found two verifier-only assumptions: macOS Bash 3.2 treated an empty argument
array differently under `set -u`, and the no-product-evidence path inherited a
historical Stage 6 requirement for ignored generated output. The wrapper now
runs tracked-source gates in a pristine clone and the unchanged full chain when
generated/package evidence is supplied. The corrected clean-clone gates passed;
product source and acceptance semantics were not affected.

The safe soft-reload failure path was exercised deterministically: preparation,
materialization, Settings/Input, main-menu timeout, completion-ticket, and
background-during-reload failures all preserve the stale-write guard and require
the app-switcher cold-start fallback. The accepted durable generation is never
rolled back or damaged.

## Physical Apple TV integration

The exact signed feature-freeze product installed over the existing app without
uninstalling or changing identity. Startup selected production persistence v2,
restored Settings and three slots, reached the first real Celeste draw, initialized
one FMOD runtime and all seven banks, and produced no managed fatal, `tvStubs`
call, FMOD guard, or UserDefaults size warning.

### Content and gameplay coverage

Physically exercised on the final signed candidate:

- Prologue through its normal map/Chapter 1 transition;
- Chapters 1, 2, 3, 4, 5, 6, 7, and 8;
- Farewell;
- one representative B-side and one representative C-side;
- movement, Jump, Dash, Grab, Pause, Confirm/Cancel, file/map navigation,
  dialog/menu interaction, death/respawn, save/quit, audio, and rumble.

The late-game source was a lawful user-owned imported save with all A/B/C sides
and Farewell available and most progression complete. Each requested chapter
and representative side loaded, rendered, accepted input, and produced normal
audio. Existing completed strawberries, hearts, cassettes, checkpoints, Summit,
and Farewell state were visible through normal game UI; every collectible was
not re-earned during this audit.

The unchanged Stage 9B physical acceptance already includes real 33,183- and
33,455-byte Chapter 5/Theo saves above the former 32 KiB limit. Stage 14
revalidated that exact persistence implementation, its 181,096-byte
serializer-valid near-complete fixture, and three large-slot envelope tests.
The largest raw slot in the final user-owned three-slot cold snapshot was 25,498
bytes; no claim is made that the current user saves themselves crossed 32 KiB
during this audit.

### Persistence, termination, restart, and replacement

All three production slots were present after the final restoration. The final
cold snapshot selected generation 142, format v2, with privacy-safe raw sizes:

| Logical entry | Raw bytes |
| --- | ---: |
| Settings | 4,565 |
| Slot 0 | 20,678 |
| Slot 1 | 15,184 |
| Slot 2 | 25,498 |

The combined raw logical payload was 65,925 bytes and the two stored v2
generations occupied 15,110 bytes. Both A and B decoded as valid. Normal game
and Settings activity advanced the generation without save-failure UI or size
warning.

External termination followed by a cold launch restored the exact selected
generation and reached first draw/seven-bank readiness. A second same-identity
replacement install retained the same generation and logical hash. An actual
Apple TV restart retained generation 134 and its exact logical hash plus a
distinctive Stadia host preference; first draw and all seven banks returned.
Post-import gameplay and Settings saves then advanced through generation 137,
and the final temporary-Prologue-slot restoration reached generation 142.

### Save Manager and soft reload

The physical final-candidate workflow verified:

- explicit Options activation and same-network numeric URL;
- wrong access code rejection and correct-code authentication from another LAN
  device;
- all-files ZIP and individual ordinary `.celeste` download;
- old URL becoming unreachable after Back/Stop;
- controlled secondary-slot deletion and exact recreation;
- Settings reset and exact Settings restoration;
- browser/TV Confirm-driven reload guidance;
- six successful Confirm-driven high-level reload cycles in the original
  process, with a functional main menu after each;
- normal gameplay save and normal Settings save after imported state;
- cold restoration without stale slot resurrection.

No normal mutation required the Apple TV app switcher. The listener remained
explicit-only, local-LAN-only, authenticated, CSRF/revision protected, and
stopped for reload/background. The generated runtime enforces unchanged FNA
game/graphics identity and single FMOD readiness before clearing the stale-write
guard. The prior accepted ten-cycle physical stress remains applicable to this
unchanged runtime source; Stage 14 repeated several cycles as an integrated
smoke rather than replacing that stress record.

### Controller prompts, Quit, audio, haptics, and branding

All four manual artwork families—Xbox, PlayStation, Nintendo Switch, and
Stadia—were visually exercised, with all 24 required mappings still covered by
the deterministic asset gate. Automatic on the connected DualSense resolved to
PlayStation. The host-only preference survived relaunch, background/foreground,
real Apple TV restart, and replacement installation, then was returned to
Automatic. Gameplay mappings did not change.

Main-menu Quit showed Leave Celeste, Back returned to a functional menu, and
three Quit → Home → same-process reopen cycles returned to the main menu without
`Engine.Exit`, FNA disposal, a second runtime, or the former blank/frosted shell.
Pause-menu Save and Quit remained distinct and returned directly to the main
menu. Ordinary Home during gameplay resumed the existing gameplay scene.

Title/UI/gameplay/death/respawn/pause/chapter-transition audio, volume changes,
background pause/foreground resume, and soft reload were exercised without
duplicate audio. Rumble worked; background, reload, and Leave produced no stuck
haptics. Available logs showed continued frames and no obvious stall. The more
precise accepted Stage 13B ten-cycle measurement observed only a bounded
456,336-byte managed-memory increase and no accumulating runtime/listener/audio
objects; Stage 14 did not add a new performance subsystem.

The physical Home Screen still displayed `Celeste`, the centered unclipped
black-backed layered strawberry icon retained parallax, and the static Top Shelf
splash remained correctly cropped. Launching from the icon worked.

## iOS, privacy, and repository boundary

The original `celestemeow` iOS project graph remains present. The locked five
iOS native archive hashes pass, generated tvOS transformations remain gated to
ignored tvOS trees, and the tvOS host preference is absent from the iOS project.
This was a static/non-proprietary regression audit, not a fresh physical iOS
build or device acceptance.

The app declares the existing UserDefaults required reason `CA92.1` and the
focused Bonjour service used only by the explicitly opened Save Manager. There
is no tracking, analytics, cloud service, identity collection, iCloud, App
Group, User Management, or Game Mode claim. Raw device logs, LAN addresses,
access codes, saves, signing values, and device identifiers remain only in
ignored evidence.

Tracked Git content contains no Celeste/FMOD payloads, generated/decompiled
source, saves, apps, IPAs, profiles, certificates, private paths, device IDs, or
credentials. The current README, Building, Status, and Troubleshooting guides
accurately describe the final flow; historical records remain under
`docs/history/`.

## Known limitations and untested scope

- Only exact accepted itch.io Linux Celeste 1.4.0.0 and FMOD 1.10.09 build
  97915 inputs are supported; Steam/other versions and Everest/mods are not.
- DualSense is the only physically accepted controller. Nintendo/Stadia
  automatic identification is limited to Celeste's locked known identities;
  manual artwork selection works.
- Saves are shared between Apple TV users. There is no Apple User Management,
  cloud sync, cross-device sync, or uninstall-survival guarantee.
- Personal Team installations expire; changing the bundle identifier changes
  the defaults domain.
- The app does not claim Apple Game Mode or App Store distribution.
- atvloadly/Sideloadly compatibility is structurally verified, but this project
  did not repeat an external atvloadly physical install in Stage 14.
- A rare soft-reload verification failure deliberately keeps gameplay blocked
  and requires full app-switcher termination before cold restore.
- The current iOS lane received static integrity checks only in Stage 14.

Within those limits, the exact final commit is suitable to tag later as the
first public release candidate.
