# Project status and architecture

## Current result

The `tvos-port` branch is a working personal-use Apple TV port built locally
from user-owned inputs. The accepted device build runs Celeste 1.4.0.0 with
Metal graphics, real FMOD audio, extended-controller gameplay, and durable
Settings/save slots. It is not an App Store distribution.

## Proven support matrix

| Area | Proven configuration |
| --- | --- |
| Host | Apple silicon; M1 MacBook Air on macOS 26.3 |
| Apple tools | Xcode 26.6, tvOS SDK 26.5 |
| .NET | SDK 10.0.302, workload set 10.0.302.0 |
| Target | tvOS arm64, minimum tvOS 16.0 |
| Device | Apple TV 4K (3rd generation), `AppleTV14,1` |
| Controller | Sony DualSense |
| Game input | Unmodified Celeste 1.4.0.0, itch.io Linux download |
| FMOD input | FMOD Engine iOS/tvOS 1.10.09, build 97915 |
| Device build | Release, full AOT, full trimming, no interpreter |

Steam and other Celeste distributions are untested. Another distribution may
work only if the exact validator accepts byte-identical required files. Other
arm64 Apple TV models, newer compatible system versions, and extended SDL/FNA
controllers may work but have not received this project's physical acceptance.

## Architecture

```mermaid
flowchart LR
    U["User-owned itch.io Linux Celeste 1.4.0.0"] --> G["Locked validation and managed regeneration"]
    G --> C["Modern generated Celeste assembly"]
    F["Pinned FNA source"] --> H[".NET 10 tvOS host"]
    C --> H
    N["Six locked native XCFrameworks"] --> H
    M["External FMOD 1.10.09 + FMOD-SDL"] --> H
    A["Locally generated icon and Top Shelf assets"] --> H
    H --> P["Full-AOT, fully trimmed tvOS arm64 app"]
    P --> D["Personal Team install"]
    P --> I["Signing-ready unsigned IPA"]
    P --> S["Dual-generation standard UserDefaults storage"]
    S --> W["Explicit authenticated LAN Save Manager"]
    H --> B["Host-only controller prompt preference"]
    H --> Q["Foreground/background-aware Leave Celeste flow"]
```

### Modern sibling host

The tvOS app lives in `tvos/CelesteTvOSRuntimeHost/` and targets
`net10.0-tvos`. It is isolated from the retained Xamarin.iOS project in
`celestemeow/`. The tvOS lifecycle enters SDL and FNA through the modern .NET
Apple application host and renders through FNA3D Metal.

### Native dependencies

Stage 1 locks and builds SDL2, FNA3D, FAudio, Theorafile, tvStubs, and MoltenVK
for tvOS device and simulator. Each archive member's Apple platform and minimum
OS is checked, variants meet only in XCFrameworks, and native link probes close
without Celeste or FMOD. The accepted logical set SHA-256 is recorded in the
native report and builder.

### Managed regeneration

The repository never carries Celeste source or binaries. A repository-local,
version-locked decompiler reconstructs the user's exact executable under
ignored `.build/` directories. Ordered tracked transforms retarget the result
to modern .NET/FNA and encode focused AOT, serializer, platform, audio, and
persistence adaptations. Input identities, transform counts, and normalized
logical hashes make stale or changed generated output fail closed.

### Audio

Physical-device gameplay uses the generated Celeste FMOD 1.10.20 managed API
against the user-supplied native FMOD 1.10.09 SDK. The accepted path has 489
native exports for the 490 generated declarations; the sole absent API is
unused telemetry and is trimmed. All seven banks are bundled from the user's
game. The arm64 simulator deliberately retains the no-audio path because the
available FMOD 1.10.09 simulator archives are x86_64-only.

### Persistence

Celeste sees normal materialized Settings/save files during execution. The
bridge stores only four allow-listed logical entries—Settings and save slots
0, 1, and 2—in two complete checksummed envelopes under standard app-private
UserDefaults keys. Persistence format v2 independently compresses each present
entry with deterministic zlib level 9, retains compressed and uncompressed
SHA-256 values plus an outer envelope hash, and strictly bounds decompression.
Commits rotate slots, read back and validate, retain the older generation,
enforce a 256 KiB stored total budget, and recover from a corrupt newest slot.

Legacy v0 and production v1 generations remain readable. A v1 install is not
rewritten merely by launching; its next intentional changed save writes v2 to
the older slot and preserves the selected v1 generation until verification is
complete. The physical Chapter 5 save that exceeded the old 32 KiB port limit
now commits and restores successfully.

There is no User Management entitlement. The same standard app domain is
shared between Apple TV users. There is no iCloud, cloud sync, cross-device
sync, or uninstall-survival guarantee.

### Save Manager

The tvOS-only Options entry opens a Celeste-rendered host modal; it does not add
another `Oui` subtype or reflection root. Only that explicit action flushes and
read-back verifies the current Stage 9B generation, then starts a bounded
Network-framework `NWListener` on a system-selected port. The screen presents
the current numeric LAN URL and a new cryptographically random access code.

The listener advertises `_celeste-save._tcp` over Bonjour only while open. Its
fixed-purpose HTTP boundary authenticates into an in-memory ten-minute session
and permits one all-files ZIP plus fixed download, validated replace, save-slot
delete, and Settings-reset actions for only `settings`, `0`, `1`, and `2`.
Imported and exported data is the exact ordinary uncompressed `.celeste`
payload validated by the persistence authority; the server cannot see internal
A/B keys or compressed v2 envelopes. A successful mutation blocks stale
gameplay until Celeste is restarted. Background, screen exit, listener failure,
shutdown, or twelve minutes of inactivity stop the listener and erase all
credentials. Current Apple platform documentation does not apply the Local
Network privacy authorization prompt to tvOS; the app still declares its
focused Bonjour service and usage description.

### Controller prompts

The generated tvOS Options menu contains a normal Celeste slider for
Automatic, Xbox, PlayStation, Nintendo Switch, and Stadia prompt artwork. It
wraps the existing `Input.GuiInputPrefix` family choice and does not touch
bindings, FNA button values, Settings XML, SaveData, or the Stage 9B envelope.
All four manual families resolve the locked game's complete 24-input matrix;
family-specific assets fall back through Celeste's own `controls/fallback`
paths where designed.

The selected mode is one validated scalar under the standard app-private
`CelesteTvOS.ControllerPrompts.v1` UserDefaults key. Automatic prefers
Celeste/FNA's exact PlayStation, Nintendo, and Stadia GUID knowledge, then the
current Apple Game Controller product category for DualSense, DualShock 4, and
Xbox. Siri Remote categories are excluded, multiple controllers prefer the
current controller and then stable connection order, and unknown controllers
retain Celeste's existing fallback. Nintendo/Stadia automatic selection is not
claimed beyond Celeste's locked known identifiers; their manual modes remain
fully available.

### Graceful main-menu Quit

The tvOS generated main-menu Quit route is intercepted at its single locked
user-facing call site before `Engine.Exit`. The host waits for an active
`UserIO` save, read-back verifies the Stage 9B durable state, stops any Save
Manager listener, and clears haptics before showing a Celeste-rendered **Leave
Celeste** screen. It deliberately keeps the valid FNA/Celeste runtime alive.

Back dismisses the screen to the existing main menu. If the app genuinely
enters the background, the in-memory Leave state records completion; on a
same-process foreground return the screen is removed and the main menu regains
focus. Merely resigning active is not treated as Home/background completion.
This replaces the former desktop exit path that disposed FNA while leaving an
empty UIKit/SDL application surface.

Save Manager mutations remain a separate safety state. Because the current
runtime holds stale Settings/SaveData after an import, Home alone does not
restart it; the user must fully close Celeste from the Apple TV app switcher.
That restart-required state always takes precedence over normal Leave recovery.

### Branding and packaging

`build-tvos.sh` generates a black-backed layered strawberry icon from
`Celeste.png` and static Top Shelf artwork from
`Content/Graphics/SplashScreen.png`, all below ignored roots. Xcode compiles the
asset catalog. The same builder emits either a Personal Team development app
or an unsigned conventional tvOS IPA containing `Payload/Celeste.app`.

## Accepted behavior

- Real Celeste title/gameplay scenes and sustained frames on simulator/device
- Metal on the physical Apple TV GPU
- Title, UI, gameplay, Prologue, cutscene, death/respawn, and lifecycle audio
- DualSense gameplay controls and standard whole-controller rumble
- Artwork-only controller prompt selection, including physically verified
  DualSense Automatic → PlayStation
- Prologue normal and skip transitions
- Settings and three save slots across termination, restart, and replacement
  install with the same app identity
- Transparent v2 compression and the formerly failing Chapter 5 save boundary
- Newest-generation corruption fallback
- Layered icon/parallax and static Top Shelf artwork on physical Apple TV
- Free Personal Team signed installation and verified unsigned IPA structure
- Explicit Save Manager with temporary same-LAN authentication, ordinary-file
  backup, exact validated replacement, slot deletion, and Settings reset
- Main-menu Quit verifies durable state and provides a safe Home/background
  flow without destroying the FNA runtime or leaving a blank surface

## Known limitations

- Personal Team development provisioning expires after about seven days.
- Saves are shared between Apple TV users.
- Changing the bundle identifier changes the app/defaults domain.
- Uninstall survival, cloud backup, and cross-device sync are not provided.
- DualSense is the only physically accepted controller; Siri Remote gameplay is
  unsupported.
- Automatic Nintendo Switch/Stadia classification is limited to Celeste's
  locked known controller identities; use the manual prompt choice otherwise.
- Steam and other Celeste distributions are untested.
- The app is roughly 1.1 GiB before IPA compression.
- Actual atvloadly physical installation has not been project-tested; only the
  conventional unsigned IPA structure is statically verified.
- Save Manager operations are deliberate and local only; there is no unattended
  sync, cloud service, or in-browser XML editor. After a successful mutation,
  Celeste must be fully closed from the app switcher and relaunched; Home alone
  normally suspends the stale process.
- Apple Game Mode is not declared: the current public Apple keys do not document
  tvOS availability, and this project makes no Apple TV Game Mode claim.
- No App Store, distribution-profile, paid entitlement, or universal hardware
  claim is made.

## Audit history

These reports preserve the evidence and stage-by-stage decisions. Users do not
need them for normal builds.

- [Initial feasibility and port plan](../TVOS_PORT_PLAN.md)
- [Native dependency pipeline](../TVOS_NATIVE_BUILD_REPORT.md)
- [Modern FNA host](../TVOS_HOST_STAGE2_REPORT.md)
- [Managed retarget](../TVOS_CELESTE_MANAGED_STAGE3A_REPORT.md)
- [First real Celeste frame](../TVOS_CELESTE_RUNTIME_STAGE3B_REPORT.md)
- [Prologue/save/haptic correction](../TVOS_CELESTE_PROLOGUE_STAGE3C_REPORT.md)
- [FMOD diagnostic](../TVOS_FMOD_DIAGNOSTIC_STAGE5A_REPORT.md)
- [Normal gameplay audio](../TVOS_CELESTE_AUDIO_STAGE5B_REPORT.md)
- [Durable persistence](../TVOS_CELESTE_PERSISTENCE_STAGE6_REPORT.md)
- [Compressed persistence migration](../TVOS_COMPRESSED_PERSISTENCE_STAGE9B_REPORT.md)
- [Friendly self-build and branding](../TVOS_SELF_BUILD_STAGE8A_REPORT.md)
- [Public repository release](../TVOS_REPOSITORY_RELEASE_STAGE8B_REPORT.md)
- [Public prerequisite and failure-UX hardening](../TVOS_PUBLIC_PREREQUISITES_STAGE8C_REPORT.md)
- [Locale-independent FMOD/Theorafile symbol validation](../TVOS_LOCALE_REPRODUCIBILITY_STAGE8D_REPORT.md)
- [Read-only local-network Save Manager](../TVOS_READONLY_SAVE_MANAGER_STAGE10A_REPORT.md)
- [Writable local-network Save Manager](../TVOS_WRITABLE_SAVE_MANAGER_STAGE10B_REPORT.md)
- [Controller prompt selector](../TVOS_CONTROLLER_PROMPTS_STAGE11_REPORT.md)
- [Graceful main-menu Quit](../TVOS_GRACEFUL_QUIT_STAGE12B_REPORT.md)

## Isolation and licensing

Tracked files are host/adapter source, reproducibility locks, focused transforms,
scripts, documentation, and open-source notices. Celeste binaries/content,
generated/decompiled source, FMOD SDK files/banks, generated artwork, app/IPA
products, signing data, and device evidence stay ignored.

The repository root [MIT licence](../LICENSE) covers the source to which it
applies. FNA and each locked native dependency retain their own upstream terms;
exact paths are recorded in `native/tvos-dependencies.lock.json` and generated
licence bundles. FMOD and Celeste remain subject to their respective external
licences and are not relicensed by this project.
