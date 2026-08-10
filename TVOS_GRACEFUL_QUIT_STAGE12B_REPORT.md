# Celeste tvOS graceful Quit (Stage 12B)

## Result and scope

Starting commit: `6118e5e2f6fd13657e55ebeeab52dee313d5306d`.

Stage 12B replaces only the intentional, controller-accessible main-menu
application Quit path. It does not change gameplay, input bindings, controller
prompt semantics, Settings XML, SaveData, the Stage 9B v2 envelope, FMOD event
selection, or Save Manager protocol routes. Apple Game Mode remains absent.

Final commit: the Stage 12B commit containing this report.

## Stage 12A cause

The locked Celeste 1.4.0.0 main-menu path was:

`OuiMainMenu.OnExit` → fade callback → empty `Scene` → `Engine.Exit` → FNA
`Game.Exit` → game-loop return → Celeste/FNA disposal.

The .NET/SDL/UIKit application remained active after that return. It had no
replacement content, so tvOS displayed a blank/frosted application surface and
subsequent icon selection addressed the same empty process. Saving and FMOD
were successful and were not the cause. Stage 12A also proved that constructing
a second Celeste runtime in that process is unsafe: FNA disposal clears global
AOT content-reader registration which a module initializer installs only once.

No supported public API was found for a tvOS application to send itself to the
Home Screen. Forced exit, abort, exception, signal, and private UIKit selector
approaches remain rejected.

## Locked user-facing exit inventory

The tracked machine-readable policy is
`managed/celeste-stage12b-exit-policy.json`.

| Route | Production meaning | Stage 12B treatment |
| --- | --- | --- |
| `OuiMainMenu.OnExit` | Intentional application Quit | Intercept exactly once before `Engine.Exit` |
| Pause-menu Save and Quit | Save and return to main menu | Unchanged; not an application exit |
| Run-thread/fatal/diagnostic/developer exits | Compromised or diagnostic runtime | Unchanged; never disguised as a healthy Leave screen |

The exact source transform fails closed if the locked `OnExit` body changes.
The tvOS branch calls `TvOSQuitHooks.Show`; the non-tvOS branch retains the
original fade, empty scene, and `Engine.Exit` fallback. No new `Oui` subtype or
reflection root was required.

## Leave architecture and state machine

The generated bridge adds one normal Celeste `Entity` over the existing main
menu. The host owns the deterministic in-memory state:

`Inactive` → `PreparingToLeave` → `AwaitingBackground` →
`LeftViaBackground` → `Inactive`.

A preparation failure enters `Failure`, where Confirm retries and Back restores
the live main menu. Duplicate Quit requests are rejected unless the state is
`Inactive` or `Failure`.

`WillResignActive` never completes Leave. Only
`UIApplication.DidEnterBackgroundNotification` changes
`AwaitingBackground` to `LeftViaBackground`. On a subsequent
`DidBecomeActive`, a completed ordinary Leave increments an in-memory UI
generation; the existing overlay removes itself and returns focus to the same
main-menu runtime. Ordinary backgrounding outside Leave is unchanged.

The state is never persisted. If tvOS purges the process, the next launch is a
normal cold launch restored by Stage 9B.

## Save and cleanup ordering

The generated UI remains in its preparing state while `UserIO.Saving` is true.
If that observed save finishes unsuccessfully, it displays a visible failure
and retains the runtime. Otherwise the host performs, in order:

1. Stage 9B `Flush("stage12b-user-leave")` and read-back verification;
2. Save Manager listener/Bonjour/session shutdown;
3. haptic stop;
4. transition to the ready-to-leave screen.

An unchanged, already verified snapshot is a successful flush and does not
create a new persistence generation. Failure or exception never calls
`Engine.Exit` and never claims that progress was saved.

FMOD and FNA are deliberately retained while the guidance is foregrounded, so
Back is safe. The established background lifecycle pauses audio and stops
haptics only when tvOS actually backgrounds the application; foreground
reactivation resumes those established systems without a second runtime.

## Leave and restart-required UI

The ready screen tells the player that progress is safe, asks them to use the
TV/Home button, explains that reopening returns to the main menu, and offers
Back. Confirm cannot fall through to the original Quit path.

Stage 10B mutations remain categorically different. The running Settings and
SaveData objects are stale after an external replace, delete, or Settings
reset. Both the TV screen and authenticated browser result now say that Home is
not enough and instruct the player to fully close Celeste from the Apple TV app
switcher before reopening it. On foreground, restart-required state has
priority over ordinary Leave restoration and continues to block gameplay.

## Deterministic and generated verification

- Stage 12B state-machine tests: 16/16 pass.
- Stage 10A/10B protocol tests: 66/66 pass.
- Stage 11 prompt tests: 38/38 pass.
- Stage 9B persistence/recovery verification: pass.
- Two independent full managed regenerations produced identical manifests.
- No-audio generated tree: 931 files, logical SHA-256
  `bd9412aa5a7a7d0ac9662b81ac803158a5c6151064ec2107b57a88fa96756e84`.
- Real-audio generated tree: 932 files, logical SHA-256
  `091bee7a7859cfcf0a0ae3ebeb22b80db64f9e337960ed92b6ddfa12765c8ec9`.
- The generated main-menu interception occurs once; the desktop fallback occurs
  once; Pause Save and Quit is untouched.
- Debug arm64 simulator compile: pass.
- Static Stage 12B verifier: pass.

The test policy covers one Quit event, duplicate suppression, active-save
deferral, flush success/failure, Back, resign-only behavior, actual background,
foreground restoration, ordinary background preservation, restart-required
precedence, cold state, failure retry, and a five-cycle state-machine sequence.
Static gates also lock cleanup ordering, intentional-route inventory, fatal and
diagnostic non-interception, controller-binding isolation through Stage 11,
and Game Mode absence.

## Product and physical acceptance

The signed product used Release `tvos-arm64`, full trimming, full AOT, and
`UseInterpreter=false`. It was installed as a same-identity replacement without
uninstalling. The production v2 store restored, the Save Manager was dormant,
the DualSense changed Automatic prompts to PlayStation, all seven FMOD banks
loaded, and the app advanced through thousands of draws into `Celeste.Overworld`.

The normal main-menu Quit path was exercised eight times. Every request reached
the verified ready state; three were cancelled with Back and five proceeded
through real background/foreground cycles. For the five completed cycles:

- one host/runtime start and one FMOD bank initialization were observed;
- the same process and runtime resumed each time;
- five actual background completions and five foreground main-menu restores
  were recorded;
- the game loop never returned and FNA was never disposed;
- no second Celeste runtime, duplicate audio/input, `ContentLoadException`,
  blank surface, or unhandled exception occurred.

After the stress run, gameplay input, menu navigation, audio, and normal saving
were exercised. Pause-menu Save and Quit committed a new verified v2 generation
and returned to the normal main menu without opening the Leave screen. A
test-tool-only external termination then proved a clean cold launch, production
save restoration, seven-bank FMOD initialization, and normal Overworld draw.

The Stage 10B lifecycle distinction was tested with an isolated compiled
persistence namespace. The complete 66-test live writable protocol passed and
entered restart-required state. Pressing Home and reopening the resident process
kept gameplay blocked; lifecycle logs recorded `keeprestartrequired` and
suppressed every stale materialized-runtime write. Fully closing Celeste from
the Apple TV app switcher and reopening it created a clean process, removed the
restart screen, and restored the imported state. The normal production build
was then reinstalled with the same identity and its untouched production v2
store restored successfully.

The clean-clone signing-ready unsigned IPA also passed the Stage 8A and Stage
12B product verifiers:

- IPA bytes: `895538724`;
- SHA-256: `6373f63f79e218a2b6d0d91e3d78d44d12a23d7495e5eed2cdba0cf0bf284dae`;
- one arm64 TVOS executable, minimum tvOS 16.0;
- full AOT, no interpreter, seven banks, no signature/profile;
- Stage 12B product tokens present and Game Mode keys absent.

## Game Mode

Neither `LSSupportsGameMode` nor deprecated `GCSupportsGameMode` is present in
tracked Info.plist, the signed app, or the unsigned IPA. Current public Apple
documentation does not list tvOS availability for those keys, so Stage 12B
makes no Game Mode or performance claim.

## Repository and private-data isolation

Generated/decompiled Celeste source, Celeste content, FMOD inputs/banks,
signing material, app/IPA outputs, device logs, identifiers, persistence
payloads, and access codes remain ignored. Tracked additions contain only host
and bridge source, deterministic policy/tests/verifiers, documentation, and
this privacy-safe report.
