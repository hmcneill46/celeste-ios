# Celeste tvOS Stage 13B soft-reload report

## Result and scope

Stage 13B starts from `be0765f514d8ccfd1b54e076ffe1329ace4197f2`.
The accepted change is committed by the commit containing this report after the
physical and public-release gates pass.

This productionises the GREEN Stage 13A boundary. A successful Save Manager
mutation no longer normally asks the player to terminate Celeste. The TV shows
**CHANGES SAVED**, one local Confirm press starts a high-level soft reload, and
the existing FNA runtime returns to a normal Celeste main menu. The Apple TV app
switcher remains only the conservative recovery path after a failed reload.

## Runtime and state-machine architecture

The process retains one UIKit host, SDL runtime, FNA `Game`, FNA3D graphics
device and FMOD system. The generated `Celeste.Update` has one exact tvOS-only
hook that advances a main-thread-owned state machine:

`Inactive → RestartRequired → PreparingReload → ReloadingSettings →`
`ReloadingGameState → WaitingForMainMenu → Verifying → Complete`

Any error enters `Failure`; duplicate Confirm presses are suppressed and a
background transition during the critical reload conservatively fails. Failure
never clears the stale-write guard or resumes gameplay.

The reload boundary is intentionally high-level:

1. wait for active `UserIO` work and stop Save Manager networking, Bonjour,
   connections, access code and browser sessions;
2. obtain a persistence preparation ticket;
3. use `Settings.Reload`, `Input.Initialize` and `Input.ResetGrab`;
4. clear `SaveData.Instance`, the old Level/Session through scene replacement,
   and `OuiFileSelect.Loaded`;
5. stop stale music/ambience and haptics;
6. reset only `Engine.TimeRate`, `TimeRateB`, `FreezeTimer` and
   `DashAssistFreeze`, as proven necessary by Stage 13A;
7. schedule `OverworldLoader(MainMenu)` on the existing run loop;
8. wait for `Overworld` with active `OuiMainMenu`, then perform completion
   verification.

No reload path calls `Engine.Exit`, `Game.Exit`, `Game.Dispose`, another
`Celeste.Run`, process spawning, process termination or a private UIKit API.

## Persistence preparation and completion tickets

Preparation runs under the Stage 9B authority. It independently decodes A and
B, normally selects the highest valid generation, requires that generation and
logical hash to equal Stage 10B's committed selection, clears and re-materialises
only `settings`, `0`, `1` and `2`, then recaptures their exact logical hash. It
does not advance a generation or expose the compressed v2 envelope.

Completion again reads A/B and recaptures materialised files. Before it is
called, the host verifies the current Settings graph, current input-binding
references, save-slot presence and serializers, normal main-menu scene, FMOD
readiness, Controller Prompts independence, and stable game/graphics identity.
Only a matching completion ticket clears the external-mutation stale guard and
Save Manager reload-required state.

Raw imported Settings bytes remain exact at the persistence boundary. Live
Settings validation compares canonical deserialised graphs, so harmless XML
formatting canonicalisation cannot create a false failure.

## User experience and failure handling

The browser now directs the player back to the Apple TV to press Confirm. The
TV displays **RELOADING CELESTE…** during the noninteractive critical section.
Back cannot resume the stale scene. Home before Confirm preserves the blocked
reload-ready state. No-op and failed mutations do not request a reload.

If any preparation, Settings/Input, scene, timeout, runtime-identity or
completion check fails, the imported generation stays durable and the TV shows
**RELOAD FAILED** with the safe app-switcher fallback. Partial high-level reloads
are not retried blindly.

## Deterministic and build evidence

- Stage 13B reload-state tests: 20/20 PASS.
- Stage 10A/10B bounded HTTP/auth/mutation tests: 66/66 PASS.
- Stage 11 controller-prompt tests: 38/38 PASS.
- Stage 12B graceful-Quit tests: 16/16 PASS.
- Stage 9B v2 compression, limits, allow-list and repository isolation: PASS.
- Locked generated output: 932 no-audio files and 933 real-audio files, with
  one Stage 13B bridge and one main-thread update hook.
- Simulator compile: PASS with the accepted historical generated/FNA trim
  warnings and no Stage 13B-specific warning or interpreter fallback.
- Clean public-builder `both` run: PASS. The independently published unsigned
  and signed device graphs used Release, full trimming, full AOT and
  `UseInterpreter=false`; all Stage 8A/11/12B/13B package verifiers passed.
- Signing-ready unsigned IPA: 854 MiB; SHA-256
  `22b5864ed75ef89d99573cfaa32e62ae9b727cc3818e624688da3fe3a36d15df`.
- Same-identity signed startup: PASS; the first Celeste draw and seven-bank
  FMOD readiness were independently observed.

The first signed candidate exposed one focused startup-order defect: the host
coordinator attempted to capture `Engine.Instance` before `Celeste.Run` had
constructed the FNA game. It failed before first draw without touching durable
state. Runtime identity capture was moved to the exact first generated game-loop
update, the verifier now forbids constructor-time FNA identity access, and the
corrected signed app reached first draw and all seven banks.

The remaining acceptance section is completed from privacy-safe physical
evidence before this report is committed. Raw saves, player names,
private paths, signing values, LAN addresses and browser credentials are never
tracked.

## Physical acceptance

Physical acceptance uses a same-identity Release `tvos-arm64` replacement with
full trimming, full AOT and `UseInterpreter=false`. It covers the complete
mutation matrix, ten Confirm-driven reload cycles in one runtime, normal save
after import, Settings save after import, deletion non-resurrection, language,
Controller Prompts, Stage 12B Quit and cold-launch fallback. Privacy-safe final
results are recorded here only after those gates complete.

An isolated acceptance namespace completed ten genuine HTTP mutations followed
by ten production-coordinator reloads in one process: replacements for slots 0,
1 and 2; deletion/recreation for slots 1 and 2; Settings replacement/reset; and
English/French Settings transitions. Every preparation/completion ticket
passed. Host startup, FMOD initialization, and seven-bank readiness each
occurred once; there was no reload failure, fatal exception, content failure,
or UserDefaults warning. Managed memory changed from 117,900,808 bytes at cycle
1 to 118,357,144 bytes at cycle 10 (456,336-byte increase; observed maximum
118,566,496), with no accumulating listeners, FMOD systems, or FNA runtimes.

The user then exercised the real DualSense/menu path on the physical Apple TV.
French UI, audio and controller input were functional; loading imported slot 0,
making progress, Save and Quit, and changing a normal Settings value succeeded.
The ordinary slot-0 payload changed from 18,756 to 19,076 bytes, and the next
normal Settings save committed that current slot plus Settings as generation
28; a second Settings change committed generation 29. A cold launch selected
generation 29 and reproduced the exact privacy-safe hashes for all four logical
files, proving the post-import normal save and Settings save did not resurrect
stale state.

After those reloads, Stage 12B main-menu Quit was exercised twice. Back from
the first Leave screen restored the functional main menu. The second Leave
entered real background after Home, and foregrounding the same process cleared
the Leave state and restored `OuiMainMenu`; no FNA disposal, second runtime, or
blank/frosted shell occurred. The signed production build was then reinstalled
with the same identity and selected the untouched production namespace at
generation 107, followed by first draw and seven-bank readiness. The user
confirmed that the real saves and Settings were present and that audio,
DualSense input, and Automatic PlayStation prompts behaved normally.

## Public build and repository isolation

The public builder runs the Stage 13B source verifier before publish and the
product verifier for signed apps and unsigned IPAs. A genuinely empty candidate
clone initialized every recursive submodule using the documented HTTPS rewrite,
passed host doctor and exact Celeste/FMOD validation, freshly rebuilt all Stage
1 inputs, reproduced accepted logical hash
`61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39`,
and produced a verified 854 MiB unsigned IPA (SHA-256
`5f13ede5182232dbb32c960e862a0a55b715b332b3609148c84dec62385721fa`).
Generated Celeste source,
game content, FMOD material, app bundles, IPAs, fixtures and device/browser logs
remain ignored and are not committed.
