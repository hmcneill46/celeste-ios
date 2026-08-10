# tvOS FMOD Diagnostic — Stage 5A

## Result

Stage 5A passed in full. The separate `FmodDiagnostic` lane validates, stages,
links, signs, installs and exercises the user-supplied FMOD Engine 1.10.09
iOS/tvOS SDK on a physical Apple TV. Both a music event and a UI/SFX event
reached FMOD's real `PLAYING` state, and the user confirmed that both were
audible with no observed distortion.

This result is deliberately narrower than game-audio support. Normal Celeste
still compiles with `TVOS_AUDIO_DISABLED`; its 490 low-level FMOD guards remain
in place and were not called. Stage 5B must integrate audio into gameplay.

- Starting commit: `ce65a3896241ad33b0685a37e17278d0d5398e23`
- Final commit: the commit containing this report
- Branch: `tvos-port`
- Host: macOS 26.3, arm64
- .NET SDK/workload set: `10.0.302` / `10.0.302.0`
- tvOS workload pack: `26.5.10301/10.0.100`
- Xcode: 26.6 (`17F113`)
- Device and simulator SDK: tvOS 26.5
- Deployment target: tvOS 16.0

No native dependency, SDK, workload or host tool was installed or upgraded.

## Architecture and isolation

`FmodDiagnostic` is a compile-time launch mode in the existing modern tvOS
lifecycle. On `tvos-arm64` it links three device-only XCFrameworks prepared in
ignored storage: FMOD low level, FMOD Studio and the real FMOD-SDL bridge. It
uses the accepted six Stage 1 XCFrameworks and the same SDL/FNA lifecycle as
Stage 2, but it does not reference or invoke the generated Celeste assembly.

On `tvossimulator-arm64`, the mode contains no FMOD native reference and no
bank. It reports that this FMOD SDK is device-only, because its supplied tvOS
simulator archive is x86_64-only, then runs the Stage 2 FNA diagnostic scene.
This keeps the accepted arm64 simulator lane free of incompatible slices.

The project includes the external banks only when both conditions are true:

```text
CelesteLaunchMode == FmodDiagnostic
RuntimeIdentifier == tvos-arm64
```

Normal `Celeste`, `CelestePreflight`, `CelestePrologueDiagnostic` and
`Stage2Diagnostic` modes do not acquire the FMOD references or banks.

## FMOD SDK validation

Verified facts:

| Evidence | Result |
|---|---|
| `doc/revision.txt` | FMOD Engine 1.10.09, build 97915 |
| `FMOD_VERSION` | `0x00011009` |
| Low-level archive | `libfmod_appletvos.a`, static arm64 TVOS, minimum tvOS 9.0 |
| Studio archive | `libfmodstudio_appletvos.a`, static arm64 TVOS, minimum tvOS 9.0 |
| Dynamic FMOD dependency | None |
| Public exports | Required low-level and Studio C exports present |
| External source representation | `$FMOD_SDK_ROOT` only |

Every archive member was inspected, including archive metadata. Mach-O members
identify as `arm64` and `TVOS`; no iOS, simulator or desktop member was accepted.
The original archive SHA-256 values are stored only in the ignored logical
manifest. The validator rejects a different release, build, header version,
architecture or platform instead of adapting it silently.

## FMOD-SDL bridge

The repository previously did not contain FMOD-SDL source, while the
user-owned game installation contains only built Linux bridge objects. Those
objects export `FMOD_SDL_Register` and embed plugin version `190916`. This
identifies the matching upstream source as:

- component: FMOD_SDL
- immutable tag: `19.09.16`
- exact commit: `947df759501d9f5a7df702101c453efc3e06ba22`
- source URL: `https://github.com/flibitijibibo/FMOD_SDL.git`
- licence: zlib
- source SHA-256: `0f5a4c25298e0c223ef7a42c3a479ee26fbd2f79509cfd814afadcbe9472d392`
- exported symbol: `FMOD_SDL_Register`

The preparation script fetches only that immutable revision, rejects a wrong
origin, commit or dirty checkout, and compiles the real bridge for arm64 TVOS
16.0 against the accepted Stage 1 SDL headers and the external FMOD headers.
The bridge registers an SDL-backed FMOD output plugin; it is not a no-op or
symbol stub.

## Link compatibility correction

The first device link found six duplicate private Ogg/Vorbis helper exports in
FMOD 1.10.09 and the accepted Theorafile archive:

```text
_drft_backward
_drft_clear
_drft_forward
_drft_init
_vorbis_lpc_from_data
_vorbis_lpc_predict
```

The fix does not edit the mounted SDK or Stage 1. The preparation script first
requires the duplicate intersection to equal that tracked six-symbol list,
then runs Apple `nmedit -R` on an ignored copy of FMOD's single device object.
It rebuilds a derived archive in which only those bundled helpers are local.
All public `FMOD_*` exports remain global and the final executable has no
duplicate Stage 1 intersection. Broad duplicate-symbol suppression was not
used.

## Deterministic staging

Two independent clean preparations, `current` and `rebuild-b`, produced
identical normalized manifests. The accepted logical SHA-256 is:

```text
b32fc89dbefdda69ab7bf20ea9ece37826dce51787a4f70446493e292b12603d
```

The manifest locks the external SDK identity and hashes, FMOD-SDL source and
licence hashes, compiler commands, deployment target, derived-symbol policy,
architectures, platforms, exports, XCFramework metadata and bank inventory.
Generated sources, archives, XCFrameworks, headers, banks and logs remain under
ignored `.build/fmod-tvos/` and `artifacts/fmod-tvos/` roots.

The exact validated bank set contains seven files and totals 665,721,576 bytes:

```text
Content/FMOD/Desktop/Master Bank.bank
Content/FMOD/Desktop/Master Bank.strings.bank
Content/FMOD/Desktop/dlc_music.bank
Content/FMOD/Desktop/dlc_sfx.bank
Content/FMOD/Desktop/music.bank
Content/FMOD/Desktop/sfx.bank
Content/FMOD/Desktop/ui.bank
```

Paths, sizes and SHA-256 values are kept in ignored evidence. Bank bytes are
not modified, recompressed or tracked.

## Managed/native compatibility

Celeste 1.4.0.0 reconstructs an FMOD managed API with header version
`0x00011014` (1.10.20), while the required native SDK reports `0x00011009`
(1.10.09). Neither number was rewritten.

The diagnostic uses a focused exact C binding surface with `__Internal`
imports and passes the managed 1.10.20 header value to Studio creation. On both
physical launches:

- the real low-level version API returned exactly `0x00011009`;
- standalone low-level system creation, FMOD-SDL registration, initialization,
  update, close and release returned `OK`;
- Studio creation with the 1.10.20 header, low-level lookup, FMOD-SDL
  registration and Studio initialization returned `OK`;
- there was no `ERR_VERSION`, `ERR_HEADER_MISMATCH`, missing entry point or
  output-initialization error.

Therefore the exact managed 1.10.20 surface exercised by Stage 5A is compatible
with native 1.10.09 on this physical device. This does not prove every one of
the game's managed FMOD calls compatible; Stage 5B must retain precise failure
reporting for any newly reached symbol.

## Physical diagnostic evidence

The signed Release `tvos-arm64` publish used full AOT, full trimming and
`UseInterpreter=false`. Automatic development signing reused the ignored
primary-personal-team configuration. No alternate-store identity was used and
no App Store Connect record was created.

Machine-observed first-launch sequence:

1. SDL/FNA lifecycle and Stage 2 native interop self-tests passed.
2. Low-level FMOD and the real FMOD-SDL bridge initialized.
3. Studio initialized and all seven banks loaded.
4. Runtime enumeration found 868 event paths.
5. Music event `event:/music/menu/complete_area` reached `PLAYING`.
6. Master volume changed from 1.0 to 0.45 and back with matching readback.
7. Master mute/unmute completed with command flushing and matching readback.
8. Music-bus volume changed to 0.5 and back.
9. The event parameter `fade` was changed to 0.5.
10. The music instance stopped and released.
11. The SFX bus changed to 0.5 with matching readback; UI event
    `event:/ui/main/button_select` reached `PLAYING`; the SFX bus returned to
    1.0 with matching readback; then the event stopped and released.
12. Master pause/unpause completed.
13. Resign-active stopped playback; background was entered safely.
14. Foreground/active restarted playback and the post-foreground SFX reached
    `PLAYING`.
15. All active events stopped, banks unloaded, Studio released and the process
    exited cleanly.

The second clean launch repeated native-version validation, low-level and
Studio initialization, all bank loads, music and SFX `PLAYING`, volume, mute,
pause, parameter and clean-shutdown checks. The lifecycle cycle was intentionally
required only on the first launch.

User-observed audible result:

| Check | Result |
|---|---|
| Music audible | Yes |
| SFX/UI audible | Yes; the user noted it was difficult to distinguish from the music |
| Distortion observed | No |

This confirmation is limited to the Stage 5A diagnostic. It is distinct from
the FMOD state-machine evidence and does not claim normal Celeste audio works.

## Packaging and signing verification

The final device app was approximately 653 MB, dominated by the ignored bank
set. Verification proved:

- one arm64 Mach-O executable with platform `TVOS`, minimum 16.0, SDK 26.5;
- the accepted Stage 1 representative exports;
- real FMOD low-level, Studio and FMOD-SDL exports;
- exactly the seven hash-validated bank files;
- no iOS or simulator slice;
- no loose `.so` or `.dylib`, SDK header, save/settings file, Celeste assembly,
  User Management entitlement or alternate-store material;
- a valid tvOS development signature and embedded tvOS provisioning profile.

Private Team, account, certificate, profile, bundle and device values exist
only in ignored local configuration/evidence. They are not present in this
report or another candidate tracked file.

## Simulator and prior-stage regression

FMOD itself is intentionally unavailable in the arm64 simulator lane because
the supplied 1.10.09 simulator archives are x86_64. The arm64 Release
`FmodDiagnostic` fallback built, contained no FMOD symbols or banks, ran the
FNA scene, produced 15 heartbeats across 60 seconds and survived a lifecycle
cycle.

Focused regression evidence after the Stage 5A changes:

- `Stage2Diagnostic` Release arm64 simulator build passed with the previously
  documented four FNA trim warnings; its 60-second run produced 15 heartbeats,
  survived background/foreground and captured before/after screenshots.
- normal trimmed Release arm64-simulator Celeste reached
  `Celeste.Overworld`, recorded a real Celeste draw, survived
  background/foreground and completed a clean second launch.
- no normal-Celeste FMOD low-level guard or tvStubs call was observed.
- Stage 1 logical hash remains
  `61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39`.
- accepted Stage 3 managed generation, no-audio implementation, Settings,
  temporary SaveData, Prologue and haptic sources are byte-unchanged.
- accepted iOS native archive checksums still pass.

The full Stage 3C physical Prologue acceptance was not replayed because no
Stage 3 source or transformation changed; its committed physical evidence
remains the acceptance basis. The new verifier additionally checks that those
tracked foundations are unchanged from the Stage 5A baseline.

## Failed attempts and corrections

1. The first device link failed on the six Ogg/Vorbis duplicate exports listed
   above. Exact symbol localization on an ignored FMOD copy closed the link
   without changing Stage 1 or suppressing unrelated duplicates.
2. An incremental diagnostic package retained stale Celeste managed assemblies
   from a prior launch-mode build. The package verifier rejected it. A clean
   publish removed them, and final verification requires their absence.
3. The first physical run initialized and played music but strict immediate
   mute readback observed FMOD Studio's asynchronous command timing. The
   diagnostic now calls the real `FMOD_Studio_System_FlushCommands` API before
   validating readback. The failure path stopped playback and released Studio
   cleanly; the two final launches passed.
4. Final gate review found that the SFX bus was resolved but had not been
   changed independently. A focused 0.5/1.0 SFX-bus readback test was added,
   followed by another clean signed publish and two complete physical runs.
5. A regression-build invocation supplied `CelesteRuntimeRoot` as a path
   relative to the repository, while MSBuild evaluated the override relative
   to the host project. That invocation failed before compilation. Reusing the
   project's existing repository-root-derived default corrected the command;
   the trimmed no-audio Celeste build and runtime regression then passed.
6. After switching the same output tree from the simulator RID, the first
   device `dotnet clean` reported `NETSDK1047` because its current assets file
   did not contain `tvos-arm64`. An explicit `dotnet restore -r tvos-arm64`
   before clean corrected the RID state, and the final clean full-AOT publish
   passed. One SDK-validator invocation also used the wrong option name
   `--manifest`; rerunning with its documented `--output` option passed.

No failure was handled by a fake symbol, no-op native implementation,
interpreter mode, disabled AOT, weakened signing or normal-game audio enable.

## Rebuild and verification commands

All paths below are privacy-safe placeholders:

```bash
scripts/validate-fmod-tvos-sdk.sh --sdk-root "$FMOD_SDK_ROOT"
scripts/prepare-fmod-tvos.sh --sdk-root "$FMOD_SDK_ROOT" \
  --game-root "$CELESTE_GAME_ROOT" --stage-dir .build/fmod-tvos/current --clean
scripts/prepare-fmod-tvos.sh --sdk-root "$FMOD_SDK_ROOT" \
  --game-root "$CELESTE_GAME_ROOT" --stage-dir .build/fmod-tvos/rebuild-b --clean
scripts/verify-fmod-tvos.sh \
  --stage-dir .build/fmod-tvos/current \
  --compare-stage-dir .build/fmod-tvos/rebuild-b

dotnet publish tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj \
  -c Release -r tvos-arm64 \
  -p:CelesteLaunchMode=FmodDiagnostic \
  -p:UseInterpreter=false

scripts/verify-fmod-tvos.sh \
  --compare-stage-dir .build/fmod-tvos/rebuild-b \
  --app tvos/CelesteTvOSRuntimeHost/bin/Release/net10.0-tvos/tvos-arm64/CelesteTvOSRuntimeHost.app \
  --platform tvos

scripts/run-celeste-tvos-fmod-diagnostic.sh \
  --app tvos/CelesteTvOSRuntimeHost/bin/Release/net10.0-tvos/tvos-arm64/CelesteTvOSRuntimeHost.app
```

The user must continue to supply legally obtained Celeste and FMOD inputs via
`CELESTE_GAME_ROOT` and `FMOD_SDK_ROOT`, plus ignored local development-signing
configuration. The repository does not fetch or redistribute those inputs.

## Stage 5B boundary

Stage 5B still needs to:

1. add a separately selectable game-audio build while retaining the accepted
   no-audio diagnostic and fallback lanes;
2. replace only the high-level `TVOS_AUDIO_DISABLED` behaviour needed for real
   Celeste audio and prove newly reached managed/native calls;
3. integrate bank loading, event lifecycle, listeners, parameters, buses and
   Celeste volume/settings semantics into normal gameplay;
4. validate title, Prologue, level, death, pause, cutscene, background,
   foreground and shutdown audio on full-AOT hardware;
5. retain controller haptics, temporary SaveData and all Stage 3C progression
   behaviour without coupling them to audio success;
6. preserve external-SDK licensing and ignored-bank boundaries.

Durable saves, per-user storage and User Management remain later stages.

## Acceptance conclusion

Verified: exact FMOD 1.10.09 external inputs, real arm64 TVOS objects, real
FMOD-SDL, deterministic staging, signed full-AOT package, low-level and Studio
initialization, all banks, machine-observed music and SFX playback, volume,
mute, pause, parameter change, lifecycle safety, complete shutdown, clean
second launch and user-confirmed audible output.

Inferred risk: other Celeste FMOD 1.10.20 calls not exercised by this focused
surface could still expose patch-level incompatibilities during Stage 5B.

Unverified future requirement: normal Celeste gameplay audio. It remains
intentionally disabled and is not claimed by Stage 5A.
