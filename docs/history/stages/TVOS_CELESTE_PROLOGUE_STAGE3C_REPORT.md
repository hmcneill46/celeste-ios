# Celeste tvOS Stage 3C — Prologue, temporary SaveData, and haptics

## Status and scope

Stage 3C started from commit `e5bc7bae395cc97832d7f60f140c157ae6302f2f` on branch `tvos-port`. The final commit is the commit containing this report, created with subject `fix: unblock Celeste prologue transition on tvOS`; its SHA is reported by Git after creation and cannot be embedded self-referentially. The supported input is the exact, unmodified FNA Celeste `1.4.0.0` installation already locked by Stage 3A. Generated source, game assemblies, Content, temporary saves, signing data, device identifiers, console logs, screenshots, and built applications remain below ignored `.build/` or `artifacts/` directories.

This stage does not provide durable saves or per-user saves. Its serializer is an AOT-safe compatibility bridge writing to the per-process temporary root. FMOD remains deliberately disabled; no FMOD SDK input, archive, header, bank, or native symbol provider is used.

Final acceptance status: **PASS**. Automated simulator and physical-device results, including the user-assisted DualSense disconnect/reconnect gate, are recorded below.

## Toolchain and protected baseline

| Item | Verified value |
| --- | --- |
| macOS / architecture | 26.3 / arm64 |
| .NET SDK | 10.0.302 |
| workload set | 10.0.302.0 |
| tvOS workload manifest | 26.5.10301/10.0.100 |
| Xcode | 26.6 (17F113) |
| device / simulator SDK | tvOS 26.5 / tvOS Simulator 26.5 |
| deployment baseline | tvOS 16.0 |
| physical model | AppleTV14,1 |
| native artifact logical SHA-256 | `61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39` |

The Stage 1 native dependency lock and accepted hash are unchanged. No native library was rebuilt. The existing iOS project, build script, prebuilt archives, and Stage 1–3B tracked foundations are protected by `scripts/verify-celeste-tvos-stage3c.sh`, which compares them with the starting commit.

Focused regressions also pass: the trimmed simulator `Stage2Diagnostic` scene ran for 60 seconds, survived background/foreground, and produced 16 heartbeats; normal Stage 3B `Celeste` mode reached `Celeste.Overworld`, survived its lifecycle cycle, and completed a clean second simulator launch. Stage 3C preflight retained the accepted reflection manifests, Settings tests, all seven XNB readers, native self-tests, zero FMOD low-level calls, and zero tvStubs calls.

## Original failure and first causal boundary

The user observed the real Prologue bird tutorial on the physical Apple TV: gameplay input and vibration worked, but the instructed dash left the controller vibrating, stopped cutscene progress, and a subsequent skip showed black.

The locked diagnostic transform adds ordered checkpoints to the real `CS00_Ending`, `CutsceneEntity`, `Level`, `UserIO`, scene-transition, and haptic paths. A preserved-fault build took the normal Celeste path into room `3` of Area 0, constructed the real `CS00_Ending`, and reproduced the earliest equivalent failure:

1. `bird-tutorial-entered`
2. dash input consumed and `dash-initiated`
3. dash vibration started
4. `music-cue-requested; source=post-dash`
5. `NullReferenceException` in `CS00_Ending.Cutscene`
6. fatal handler stopped vibration with reason `unhandled-exception`

The complete managed stack begins at `Celeste.CS00_Ending.Cutscene(...)+MoveNext()` (generated line 109), then passes through `Monocle.Coroutine`, `CutsceneEntity`, `Level.Update`, `Celeste.Update`, and the FNA game loop. `save-data-serialization-entered` was absent. The fault therefore occurred before progression update, area completion, `LevelExit`, or `UserIO.SaveHandler`. The new fatal boundary stopped the active 0.25-second effect after 0.029 seconds with reason `unhandled-exception`.

### Causal conclusion

The primary failure was the Stage 3B no-audio boundary returning `null` from `Audio.CurrentMusicEventInstance`; `CS00_Ending` unconditionally called `triggerCue()` on that value after the tutorial dash. SaveData did **not** cause the original black screen.

The endless vibration was a secondary consequence: the exception froze the normal update-driven vibration timer after the native controller motor had been enabled. Saving neither caused nor directly extended vibration. It was nevertheless unsafe for an exception to leave a motor active, so Stage 3C independently hardens every observable stop boundary.

There was a second, later defect which the original crash masked: Stage 3B intentionally threw if generic `SaveData` serialization was reached. Once the cue fault was corrected, normal Prologue completion reached that guard. Stage 3C replaces only the `SaveData` route with an explicit serializer.

## Deterministic reproduction architecture

`CelestePrologueDiagnostic` is a separate build mode. It creates a normal `Session` for Area 0, mode `Normal`, room `3`, and enters through the real `LevelLoader`. It does not edit maps or invoke isolated completion methods. The actual room entities, player, bird, `CS00_Ending`, fade, `CompleteArea`, save handler, `LevelExit`, and `Overworld` transition run normally.

Three compile-time scenarios are supported:

- `normal`: waits until the real dash prompt and issues one edge-triggered up-right dash through the tutorial condition.
- `skip`: issues one cutscene-skip edge after the real cutscene has begun.
- `manual`: stops at the real prompt for physical-controller testing; after transition it exposes a bounded haptic disconnect probe.

The transform is locked by `managed/celeste-stage3c-policy.json`. It requires the accepted 925-file Stage 3B source hash, checks hashes of all SaveData-graph source files, requires exact patch match counts, and produces a 927-file Stage 3C logical manifest. Direct edits to ignored generated source are neither required nor accepted.

Two independent clean, full runtime preparations produced identical input manifests, Stage 3B logical hash `0dcc35b9a2b55fd69174a450db9fb21d7ebab9a7e092ccae75810d9041374dee`, Stage 3C file inventory, and Stage 3C logical hash `a5ffcde2252c527d71bae43418751ff8dbf100bf2aa344c7400e7b722f2341c7`. Each staged 1,209 non-audio Content files (492,943,607 bytes) and excluded the seven FMOD files (665,721,576 bytes). This comparison was rerun after the final tracked haptic-probe adjustment.

## Correction

### Explicit no-audio cue

Only the two Prologue high-level cue sites are changed. They call `Audio.TriggerCueNoAudio`, which records a bounded high-level no-audio event and returns without dereferencing a null FMOD handle. All 490 generated low-level FMOD methods still throw and are counted. Static verification rejects native FMOD imports and packaging checks reject FMOD libraries, banks, and the Content `FMOD` tree.

This is a removable Stage 3C/Stage 5 boundary, not audio emulation. Audio remains nonfunctional.

### Temporary AOT-safe SaveData serializer

`TvOSSaveDataSerializer` is explicit, reflection-free XML code for the locked Celeste 1.4.0.0 graph:

- `SaveData` and `Assists`
- `AreaKey`, `AreaStats`, and `AreaModeStats`
- `Session` and `Session.Counter`
- `AudioState`, `AudioTrackState`, and `MEP`
- `PlayerInventory`, `EntityID`, and `Vector2`
- nested arrays, lists, sets, enums, nullable objects, timestamps, scalar progress, and area statistics

Generation fails if any locked graph source changes. XML writing uses fixed element order and invariant scalar formatting. Reading rejects DTDs, malformed scalar values, duplicate singleton elements, and unknown elements with `InvalidDataException`. Repeated list entries remain meaningful where the original type is ordered (for example, poem lines); set-backed collections retain their natural uniqueness semantics.

`UserIO.Serialize<T>` and `Deserialize<T>` route only `Settings` to the Stage 3B settings serializer and `SaveData` to the Stage 3C serializer. Every other unapproved generic type fails loudly. No runtime `XmlSerializer` generation, reflection, interpreter, blanket trimmer root, or trim disable is introduced.

Storage remains under `CELESTE_TVOS_SESSION_ROOT`, which the host creates below `Path.GetTempPath()` with a process-specific name. It is intentionally unsuitable for final persistence: there is no reinstall guarantee, profile separation, migration contract, iCloud synchronization, or crash-safe durable replacement.

### SaveData test matrix

The following tests pass in trimmed arm64 simulator Release and signed full-AOT `tvos-arm64` (`UseInterpreter=false`) on physical hardware:

| Test | Result |
| --- | --- |
| Missing save / new-game defaults | PASS |
| Default object round trip | PASS |
| Representative non-default graph | PASS |
| Prologue completion and dash-unlock values | PASS |
| Area stats, checkpoints, strawberries, and session collections | PASS |
| Assists, inventory, audio-state metadata, enums, and nullable state | PASS |
| Malformed scalar/XML rejection | PASS |
| Unknown element rejection | PASS |
| Duplicate singleton rejection | PASS |
| Real `UserIO.Save` and `UserIO.Load` route | PASS |
| Repeated temporary-file replacement | PASS |

Controller bindings are settings data rather than members of the locked SaveData graph. The existing AOT-safe Settings preflight remains passing and covers that separate file.

## Haptic path and lifecycle

The call path is `Celeste.Input.Rumble` / `RumbleSpecific` → `Monocle.MInput.GamePadData.Rumble` → FNA `GamePad.SetVibration` → SDL GameController haptics. Stage 3C adds caller-category forwarding and bounded sequence events containing motor values, intended duration, monotonic start/stop time, non-private controller index, scene category, and stop reason. It does not log per frame.

Duration countdown uses `Engine.RawDeltaTime`, so pause/time-rate changes cannot indefinitely suspend a finite motor effect. A watchdog is a last-resort bound for every duration-bearing effect at `max(3.0 seconds, intended duration + 0.5 seconds)`; accepted automated runs contain no watchdog intervention.

Vibration is forced to zero on:

- duration expiry and explicit zero requests;
- cutscene completion or skip;
- scene transition;
- UIKit resign-active, background, and termination notifications;
- controller disconnect;
- unhandled exception on the main or Celeste worker path;
- game disposal and normal shutdown.

The manual scenario provides a 12-second post-transition haptic probe, long enough for the DualSense's approximately ten-second PS-button power-off gesture but still bounded to 12.5 seconds by the watchdog. Its acceptance parser requires disconnect while the probe is active, the stop reason `controller-disconnect`, reconnect, and a subsequent logical Confirm edge. This is the remaining hardware-only gate.

## Ordered runtime checkpoints

The generated bridge records: bird sequence entry, tutorial prompt entry, controller input, dash, vibration start/stop, cue request, cutscene completion/skip request, progression update, SaveData serialization entry/completion, fade entry, scene construction, next-scene update/draw, and post-transition heartbeats. Fatal records include exception type/message, source, last checkpoint, and managed stack through the existing host logger.

## Results

### Preserved-fault simulator

Verified: the normal diagnostic reproduced the null audio event-instance dereference immediately after `music-cue-requested`. Save serialization was not reached. Exception-safe haptic cancellation recorded `reason=unhandled-exception`.

### Corrected trimmed simulator Release

| Scenario | Result |
| --- | --- |
| Preflight | Settings, SaveData, reflection, 7 XNB readers, and native imports PASS |
| Normal completion | Real bird/dash sequence, save, fade, and `Celeste.Overworld` PASS |
| Normal second launch | PASS |
| Skip completion | Edge-triggered skip, save, fade, and `Celeste.Overworld` PASS |
| Lifecycle | Background/foreground and resumed advancing frames PASS |
| Sustained frames | 73.462 s normal first run; 69.980 s normal second run; 73.290 s skip |
| FMOD low-level / tvStubs / unhandled exception | 0 / 0 / 0 |

The diagnostic dash vibration requested 0.25 seconds and stopped by duration expiry after 0.249 seconds in the representative simulator run. Later bird pulses also stopped by duration or explicit zero. Every post-transition heartbeat reported zero active rumble.

### Signed physical Apple TV

The app is development-signed with the already configured primary personal development team; all identity, Team ID, account, provisioning, device, and private bundle values remain ignored.

| Scenario | Result |
| --- | --- |
| Full-AOT preflight | PASS on Apple A15 GPU; Settings, SaveData, reflection, 7 XNB readers, and native imports PASS |
| Normal completion | PASS: real tutorial, temporary save (14,590-byte XML), fade, and `Celeste.Overworld` |
| Sustained frames | PASS: 73.368 s after transition |
| Background/foreground | PASS; vibration remained zero and frames resumed |
| Clean second launch | PASS: same sequence and 69.981 s after transition |
| Skip completion | PASS: edge-triggered skip, temporary save (14,538-byte XML), fade, `Celeste.Overworld`, and 69.980 s after transition |
| Disconnect/reconnect during active haptic | PASS: stopped after 10.343 s with `controller-disconnect`; reconnect and subsequent Confirm observed |

The renderer remains FNA3D Metal on the physical Apple A15 GPU. The accepted normal run recorded next-scene update/draw and advancing heartbeats with zero active rumble. Physical visual evidence is the device-only Metal renderer, real scene identity, next-scene draw, and advancing draw counters; no physical screenshot is claimed.

On hardware, the representative tutorial dash effect requested 0.25 seconds and stopped by normal duration expiry after 0.238 seconds. The skip diagnostic's bounded vibration returned to zero before transition. No accepted simulator or device run required the watchdog.

The final user-assisted run also opened and returned from Pause/Settings at the bird prompt, then completed the dash and transition, demonstrating that the surrounding settings interaction did not freeze the tutorial. The 12-second probe began only after `Overworld` first update/draw; controller power-off stopped it after 10.343 seconds with `haptic-active-before-stop=True`, reconnect was detected 6.479 seconds later, Confirm was detected another 1.308 seconds later, and 69.980 seconds of post-transition frames completed with rumble zero after disconnect.

## Controller-focused status

Previously observed physical input remains: directional movement, Jump, Grab, Dash, Confirm, Cancel, settings navigation, and rumble. Stage 3C validates the logical Dash condition on the real tutorial path and implements a single edge for diagnostic skip. Pause/Menu and settings-around-cutscene remain manual Stage 4 coverage rather than newly claimed automated coverage. Final remapping UI and Siri Remote support are out of scope.

## Failed approaches and corrections

- Treating the first save as the likely crash was rejected by ordering evidence: the null cue call fails before save entry.
- Returning a null inert FMOD event was adequate for earlier startup but not this unconditional cue call. The fix is at the high-level no-audio boundary; no fake native FMOD symbols were added.
- The old generic SaveData guard correctly prevented unsafe device-AOT serializer generation, but could not support progression. It was replaced only for the hash-locked graph.
- Background `devicectl --console` processes inherit an ignored SIGINT under noninteractive Bash, and CoreDevice 26.6 can retain the client after the remote process exits. Capture scripts now send SIGTERM and, after a bounded grace period, SIGKILL only to the exact local console PID they created; the app is terminated remotely first. This changes evidence collection only.
- The first combined physical-normal capture command was edited while Bash was still reading it and therefore returned a parser error after it had already emitted accepted first- and second-launch summaries. Runtime evidence is complete; the corrected capture cleanup subsequently completed end-to-end during the physical skip run. Scripts are syntax-checked after the correction.
- A lifecycle run can leave another tvOS application foregrounded. The device runners explicitly reactivate the already-running captured bundle, making a second launch independent of prior UI state.
- The first dedicated disconnect probe lasted 2.5 seconds and was stopped by its 3-second watchdog before a human could complete the DualSense power-off gesture. That diagnostic run was rejected. The probe is now explicitly 12 seconds and the duration-aware watchdog bounds it at 12.5 seconds; normal Celeste effects retain their existing 0.1–2.0-second durations and 3-second upper bound.
- One parallel full-AOT republish failed before app creation with `MSB3491` when two project-graph nodes raced to write the same ignored FNA file-list intermediate. No source or tracked file was involved. The accepted republish uses `-m:1 -p:BuildInParallel=false` and a fresh ignored artifacts path.

## Rebuild, verification, and manual capture

Raw evidence is intentionally ignored. The local evidence roots are:

- preserved fault: `$REPO_ROOT/artifacts/celeste-runtime/stage3c-fault-final/evidence/`
- simulator preflight/normal/skip: `$REPO_ROOT/artifacts/celeste-runtime/stage3c-final-simulator-*`
- physical preflight/normal/skip: `$REPO_ROOT/artifacts/celeste-runtime/stage3c-final-device-*`
- clean generation comparison: `$REPO_ROOT/artifacts/celeste-runtime/stage3c-repro-a/` and `stage3c-repro-b/`
- Stage 2 and normal Stage 3B regressions: `$REPO_ROOT/artifacts/celeste-runtime/stage3c-regression-*`

From `$REPO_ROOT`, with `CELESTE_GAME_ROOT` set:

```bash
scripts/prepare-celeste-tvos-stage3c.sh --game-root "$CELESTE_GAME_ROOT" --clean
scripts/verify-celeste-tvos-stage3c.sh

dotnet build tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj \
  -c Release -r tvossimulator-arm64 \
  -p:CelesteLaunchMode=CelestePrologueDiagnostic \
  -p:Stage3CPrologueScenario=normal

dotnet publish tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj \
  -c Release -r tvos-arm64 \
  -m:1 -p:BuildInParallel=false \
  -p:CelesteLaunchMode=CelestePrologueDiagnostic \
  -p:Stage3CPrologueScenario=manual \
  -p:UseInterpreter=false
```

The exact user-assisted command uses the ignored signed manual build:

```bash
scripts/run-celeste-tvos-manual-diagnostic.sh \
  --app "$STAGE3C_MANUAL_APP" \
  --test disconnect \
  --duration 600
```

At `READY FOR MANUAL PROLOGUE TEST`, perform one up-right logical Dash at the bird prompt. After `Overworld` appears and the diagnostic announces the probe, press logical Dash once, immediately hold the DualSense PS button for about ten seconds until it disconnects, reconnect it with PS, and press logical Confirm once. No identifier needs to be supplied or printed.

## Remaining work

### Known warnings

The full-trim build retains the Stage 3A/3B diagnostic baseline for desktop-only generic XML helpers and reflected discovery (`IL2026`, `IL207x`, `IL3050`, plus FNA content-reader analysis). The runtime SaveData and Settings paths do not call those generic helpers; the explicit serializers and focused reflection roots pass both trimmed simulator and full-AOT device preflight. `System.Private.Xml` therefore still reports aggregate `IL2104`, but Stage 3C introduced no unexplained serializer or haptic AOT diagnostic and did not broaden roots or suppress warnings.

### Stage 4

- Complete controller mapping, held-button/debounce, pause/Menu, remapping UI, and connection-state coverage across supported controllers.
- Decide and test the intended Siri Remote policy.

### Stage 6

- Replace the process-temporary root with the planned materialized save session and durable bridge.
- Add atomic/multi-generation backup, recovery, migration, lifecycle flush, quota, and reinstall semantics.
- Stage 7 must separately add provisioned Apple TV current-user isolation and document the entitlement fallback.

FMOD remains disabled and belongs to Stage 5.

## Evidence classification

Verified facts are the locked-source reproduction, exception order, serializer/preflight results, simulator normal/skip paths, full-AOT physical preflight, physical automated normal and skip paths, and the user-assisted active-haptic disconnect/reconnect/input sequence. The inference is that the user’s black screen was the visible consequence of the captured null cue exception; its checkpoint and stack match the reported dash timing, while the exact original user session was not console-attached. No Stage 3C acceptance gate remains open.
