# Celeste tvOS Stage 6 persistence report

Status: **PASS**. The deterministic simulator suite, full-AOT device suite,
external process-kill recovery, real Apple TV restart, replacement install and
normal `CelesteAudio` gameplay persistence gates completed successfully.

## Scope and baseline

- Starting commit: `2e17b013e7af46c993858ee6ef3ff1a13e4d6784`
- Branch: `tvos-port`
- Final commit: the commit containing this report, identified in the final
  handoff after creation with message `feat: add durable tvOS save storage`.
- Toolchain: .NET SDK `10.0.302`, workload set `10.0.302.0`, Xcode `26.6`,
  tvOS SDK `26.5`, deployment target `16.0`.
- Accepted inputs: unmodified FNA Celeste `1.4.0.0`; external FMOD `1.10.09`
  build `97915` for the device-only real-audio graph.
- Stage 1 logical artifact hash remains
  `61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39`.
- No native library was rebuilt. No existing iOS project, script, archive or
  checksum changed.

## Durable architecture

The game still reads and writes its normal `.celeste` files through the
accepted reflection-free Settings and SaveData serializers. Those files live
in a fresh process-specific directory below the app temporary root. Before
`Celeste.Celeste.Run`, the host validates the durable slots and materializes
the selected complete snapshot into `Saves/` and `Backups/`. The temporary
materialization is authoritative only for the running process; standard
app-private `NSUserDefaults` is authoritative between launches.

Successful allow-listed `UserIO.Save` and `UserIO.Delete` operations request a
complete durable commit. Celeste's combined Settings/SaveData save worker is
batched so it produces one coherent snapshot. One in-process lock serializes
capture, slot selection, write, read-back verification and selection. An
unchanged logical snapshot is not rewritten.

Lifecycle observers flush on resign-active, background, termination where
observable, host disposal and normal game disposal. A failed durable commit is
reported back to `UserIO` rather than being silently treated as a successful
save.

## Writable-file inventory

The tracked machine-readable allow-list is
`managed/celeste-stage6-policy.json`. It has four logical entries and eight
materialized paths:

| Role | Primary | Validation backup | Serializer | Maximum payload |
|---|---|---|---|---:|
| Settings | `Saves/settings.celeste` | `Backups/settings.celeste` | `TvOSSettingsSerializer` | 24 KiB |
| Slot 0 | `Saves/0.celeste` | `Backups/0.celeste` | `TvOSSaveDataSerializer` | 32 KiB |
| Slot 1 | `Saves/1.celeste` | `Backups/1.celeste` | `TvOSSaveDataSerializer` | 32 KiB |
| Slot 2 | `Saves/2.celeste` | `Backups/2.celeste` | `TvOSSaveDataSerializer` | 32 KiB |

The backup path mirrors the primary payload at materialization time and is not
a separately divergent durable entry. Absence/deletion is encoded explicitly.
The debug save slot, logs, crashes, screenshots, map-editor output, content,
FMOD banks and arbitrary temporary files are rejected or excluded.

## UserDefaults keys and envelope

Production uses exactly two standard-domain keys:

- `CelesteTvOS.Persistence.v1.A`
- `CelesteTvOS.Persistence.v1.B`

The acceptance, restart and test lanes use fixed, compiled namespace categories
under the same app-private standard domain. They are not user identifiers and
cannot be supplied as arbitrary runtime keys. Stage 7 can rely on Apple's
system-provided current-user partitioning without changing the logical
envelope or manually suffixing user IDs.

Each key contains one complete binary `NSData` value. Format v1 contains:

- eight-byte magic and format version;
- commit-complete flag and monotonically increasing generation;
- the locked Celeste executable identity SHA-256;
- Settings and SaveData schema versions;
- four deterministically sorted entries;
- per-entry logical name, present/deleted state, serializer kind, bounded
  length, payload and SHA-256;
- completion marker and SHA-256 of the complete preceding envelope.

Slot A and B are independently decodable. Startup validates both and selects
the highest supported valid generation. Corrupt or truncated newest data falls
back to the older valid generation. Unsupported future formats are preserved.
If one slot is a valid supported recovery and the other is an unsupported
future format, writes fail closed because neither slot can be replaced while
preserving both values.

Format v0 is a synthetic migration fixture without explicit serializer-version
fields. It is read without rewriting; the next intentional game change writes
v1. Unknown future versions are retained and rejected.

## Commit protocol and size budget

For a changed snapshot the bridge:

1. reads and validates all four current logical entries;
2. rejects unknown names, symlinks, malformed XML and per-file oversize;
3. encodes generation `N+1` as one complete value;
4. chooses the invalid or older replaceable slot;
5. writes one `NSData` value through standard UserDefaults;
6. calls the platform synchronization boundary;
7. reads, decodes and verifies the just-written value;
8. keeps the other valid slot untouched.

There is no separate active-slot pointer and no normal commit erases the older
slot.

Hard limits are 126,976 bytes per envelope and 262,144 bytes for both bridge
keys combined. A candidate is rejected before writing if either limit would be
exceeded. The observer for
`NSUserDefaults.SizeLimitExceededNotification` makes any platform warning an
acceptance failure. Compression is not used.

The trimmed simulator diagnostic observed a maximum encoded slot of **4,013
bytes** and maximum combined bridge usage of **6,791 bytes** for synthetic
representative test state. The largest observed real-game physical envelope
was **34,281 bytes**, and the largest two-slot bridge usage was **68,560
bytes**. Both results are well below the enforced budget.

## Privacy manifest

The runtime host now bundles its own `PrivacyInfo.xcprivacy` while preserving
the prior system-boot-time declaration. It adds exactly:

```text
NSPrivacyAccessedAPICategoryUserDefaults
CA92.1
```

Both simulator and signed-device package verification confirmed the manifest
matches the tracked file. The save data is nonsensitive game preferences and
progress; this design does not claim encryption.

No User Management entitlement, `TVUserManager`, iCloud, CloudKit, iCloud KVS,
App Group, Keychain, custom defaults suite, device identifier or account
identifier is present.

## Deterministic generation

`scripts/prepare-celeste-tvos-stage6.sh` regenerates Stage 3C no-audio and
Stage 5B real-audio source, copies each locked tree into an ignored Stage 6
root, and applies the tracked `UserIO` transformation. Generated Celeste source
remains ignored.

Two independent clean preparations were logically identical:

| Mode | Files | Logical SHA-256 |
|---|---:|---|
| no-audio | 928 | `4f7caded99bb95e4ef562ecb091d5a3dd53cba4150ab539a44f5a747775cf4a4` |
| real audio | 929 | `1e40cae65e964e08f04f9ccc0767812cb794339dc5925a0ed6985141ad3bf55a` |

The generation fails if the upstream generated hashes, target transformation
sites, allow-list or exclusive Stage 6 symbol do not match the lock.

## Failure-injection matrix

The isolated `tests` namespace exercises the following without touching
production or restart data:

| Case | Result |
|---|---|
| Empty, one valid and two valid slots | PASS (simulator) |
| Empty lifecycle flush creates no generation | PASS (simulator) |
| Corrupt envelope and per-file checksums | PASS (simulator) |
| Truncation and malformed length | PASS (simulator) |
| Duplicate and unknown file entries | PASS (simulator) |
| Future format preserved | PASS (simulator) |
| Future format plus valid recovery fails closed | PASS (simulator) |
| Wrong Celeste identity and serializer version | PASS (simulator) |
| Stale/lower generation and missing slot | PASS (simulator) |
| Both invalid start new game without an immediate write | PASS (simulator) |
| Oversized payload retains an established prior valid generation | PASS (simulator and device AOT) |
| Interruption before and after one complete slot write | PASS (simulator) |
| Eight concurrent commit requests serialize | PASS (simulator) |
| Actual `UserIO` Settings and SaveData save/load/delete | PASS (simulator) |
| Failed materialization is fatal | PASS (simulator) |
| Malformed Settings and SaveData XML | PASS (simulator) |
| v0 read and intentional v0-to-v1 migration | PASS (simulator) |
| No UserDefaults size warning | PASS (simulator) |

The final trimmed arm64 Release simulator completed **34 tests**, including an
explicit byte-for-byte check that an oversized candidate leaves its established
prior generation untouched. The full-AOT physical device completed the same
focused suite. This includes the real Foundation-adapted `UserIO`
Settings/SaveData save, load and delete path.

## Build, packaging and prior-stage results

- Debug arm64 simulator build: PASS.
- Trimmed Release arm64 simulator build: PASS using the supported simulator
  interpreter/JIT runtime. Serializers remain reflection-free; device proves
  their AOT closure.
- An optional AOT-only simulator experiment was abandoned: the .NET Apple
  simulator runtime attempted to JIT an internal `NSString` construction before
  Stage 6 startup. It does not occur in the supported simulator lane or signed
  device build.
- Release arm64 device, full AOT, full trimming, `UseInterpreter=false`: PASS.
- Signed device package, Mach-O TVOS arm64, privacy and writable-state
  isolation: PASS.
- Signed real-audio package: PASS Stage 5B verification, exactly seven banks,
  real FMOD exports, no save/settings payloads.
- Approximate real-audio app allocation: 1,178,000 KiB; bank allocation:
  650,132 KiB; total Content allocation: 1,134,048 KiB.
- Incremental launch modes share app-bundle output. A focused target now removes
  stale `Content/FMOD` from no-audio bundles. Acceptance commands use clean
  per-RID builds when AOT or launch-mode properties change.
- Stage 1 hash, Stage 2 native host isolation, Stage 3C serializers and Stage
  5B package checks: PASS.
- The Stage 2 diagnostic scene ran on the arm64 simulator for 60 seconds with
  15 Metal frame heartbeats and a passing background/foreground cycle. Its
  legacy whole-`native/` privacy verifier reports the committed FMOD-SDL licence
  author's email as a false positive; Stage 6 package and candidate-file scans
  distinguish that third-party licence text from private repository data.
- The final trimmed simulator Prologue normal and skip paths each reached
  `Celeste.Overworld`, passed SaveData and bounded-rumble checks, survived a
  background/foreground cycle, sustained 73 seconds after transition and
  passed a clean second launch (69 seconds after transition).

The first final-device diagnostic attempt failed before store mutation when
the `System.IO.File` byte helpers caused .NET's generic `SafeFileHandle`
marshaller to try to emit invocation stubs in AOT-only mode. The bridge's
materialization writes and snapshot reads now use Foundation `NSData`, with
atomic writes, avoiding runtime code generation
without changing Celeste's normal `UserIO` file semantics. The actual `UserIO`
write/read/delete test remains in the full suite.

A subsequent clean normal-game build showed the same runtime-generated
constructor stub in unrelated `PlaybackData.Load`. Preserving and directly
constructing `SafeFileHandle` did **not** cause Mono's generic reflection invoke
wrapper to be emitted, so that abandoned approach is not retained. The focused
Stage 6 graph instead uses Foundation `NSData` for FNA title-container reads,
generated Celeste bundle reads, approved state files and excluded temporary
error logs. Each content helper rejects paths outside the signed app's Content
directory, and the pinned FNA submodule and prior-stage graphs remain unchanged.

The first real empty-store playthrough committed the completed Prologue state
successfully, then failed while returning to the Overworld because Mono tried
to emit `InvokeStub_OuiAssistMode..ctor` from `Activator.CreateInstance` in
full-AOT mode. Saving was therefore not causal. The generated Stage 6 source
now replaces that one discovery/activation site with the exact ten-type `Oui`
factory in original metadata order. This is a focused source-generated AOT
factory, not a preserve-all root; the locked type count makes a changed input
fail generation rather than silently omitting a new menu.

After the restart check, a normal Chapter 1 dash exposed a second independent
full-AOT boundary: generic `Pooler.Create<T>()` attempted to emit
`InvokeStub_Snapshot..ctor` for the player's trail. The pre-crash save had
already committed and the exception path stopped haptics and FMOD cleanly.
The generator now retains the accepted pooled-attribute discovery but replaces
generic construction with a locked static factory for all 12 exact pooled
Celeste 1.4.0.0 types. Four private nested pooled types become assembly-internal
only in generated output so the factory can call their real constructors. The
generator fails if any source site or pooled set changes. Two fresh generations
matched, the supported trimmed simulator suite passed, and the final full-AOT
replacement build completed Chapter 1, entered Chapter 2, saved, quit and
restored the same Chapter 2 screen without another runtime constructor stub.

The existing accepted linker/AOT warning baseline remains. The Stage 6
Foundation file adapters additionally produce `CA1416` analyzer warnings
because their shared generated projects are analysed as though callable on all
Apple platforms. Every cited Foundation API is available on tvOS 12.2 or later,
the project is locked to tvOS 16.0, and the same paths passed both simulator and
full-AOT device execution. These warnings do not represent an unsupported tvOS
call and are retained rather than broadly suppressed.

The first final diagnostic runner invocation printed a passing 34-test result
but its attached `devicectl` console process did not exit after a normal signal.
Cleanup now stops that exact capture process with a bounded grace period,
limits device inspection/termination commands to 15 seconds and escalates only
that exact launcher if needed. The corrected runner repeated the physical suite
and exited zero.

## Settings and save acceptance

Verified by the simulator and physical diagnostic source path:

- reflection-free Settings and SaveData validation before persistence and
  materialization;
- actual `UserIO.Save`, `UserIO.Load` and `UserIO.Delete` callbacks;
- independent entries for all three normal save slots;
- persistent deleted state;
- complete-snapshot batching for simultaneous Settings/SaveData writes;
- malformed or oversize writes do not replace the prior valid generation.

Normal physical `CelesteAudio` acceptance also passed:

- two populated save slots remained independent; the second slot was deleted,
  relaunched and recreated without changing the first;
- Prologue completion, Chapter 1 death/respawn, Chapter 1 completion and early
  Chapter 2 progression were committed through normal `UserIO` paths;
- Music 9, SFX 8 and an added Talk binding on logical Back/Share survived
  process relaunch and a real Apple TV restart;
- the restored FMOD VCA readbacks were music `0.90` and SFX/UI `0.80`;
- audio, controller input and bounded haptics remained correct after restore,
  background/foreground and replacement install.
- an isolated full-AOT physical Prologue-skip run used the non-production
  acceptance namespace, committed SaveData, executed the real FMOD
  `triggerCue`, reached `Celeste.Overworld`, kept haptics at zero after their
  bounded effect, advanced frames for 95 seconds and shut FMOD down cleanly;
- after that isolated test, the accepted normal gameplay package was installed
  back over the same bundle identifier without uninstalling the app.

The largest observed real-game envelope was **34,281 bytes** and the largest
two-slot bridge usage was **68,560 bytes**, below the 256 KiB hard total budget.
No UserDefaults size-limit notification occurred.

## Lifecycle, process-kill, restart and replacement status

The runner stores raw logs, device discovery, tokens and summaries only below
ignored `artifacts/celeste-runtime/stage6-device/`. It never prints or commits a
device ID, bundle ID, team, profile or signing value.

Implemented gates:

- `process-kill-prepare` watches for the next new read-back-verified commit and
  externally terminates the exact launched app immediately afterward;
- `process-kill-verify` requires the same generation and logical hash on the
  next launch;
- `prepare-before-restart` requires a successful background flush, saves only a
  generation/hash token and prints `READY FOR APPLE TV RESTART`;
- `verify-after-restart` compares the restored generation/hash after a real
  normal Apple TV restart;
- `replacement-install` installs the same signed bundle identifier over the
  existing app and verifies the same token.

All implemented gates passed. The process-kill runner externally terminated
the exact app after a read-back-verified commit; the next launch restored the
same generation/hash and Music 9. A later normal-game capture recorded the
background flush while FMOD paused and haptics stopped. After a real normal
Apple TV restart, the exact token restored and both slots, volumes, binding,
audio and progress matched. The final changed full-AOT build was then installed
over the same bundle identifier, restored generation 11, advanced through
Chapter 1 and into Chapter 2, and committed through generation 18. Eighty
bounded gameplay heartbeats ended in `Celeste.Overworld`; no fatal, tvStubs,
FMOD guard or defaults-size warning was present.

## Exact commands

Regenerate and compare two independent clean preparations:

```bash
scripts/prepare-celeste-tvos-stage6.sh \
  --game-root "$CELESTE_GAME_ROOT" --clean

scripts/prepare-celeste-tvos-stage6.sh \
  --game-root "$CELESTE_GAME_ROOT" --clean \
  --runtime-root .build/celeste-runtime/stage6-rebuild-b \
  --artifact-dir artifacts/celeste-runtime/stage6-rebuild-b

scripts/verify-celeste-tvos-stage6.sh \
  --compare-artifact-dir artifacts/celeste-runtime/stage6-rebuild-b
```

Publish the final physical acceptance graph:

```bash
dotnet publish tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj \
  -c Release -r tvos-arm64 -m:1 -p:BuildInParallel=false \
  -p:CelesteLaunchMode=CelesteAudio \
  -p:Stage5BAudioScenario=normal \
  -p:Stage6PersistenceEnabled=true \
  -p:Stage6StorageNamespace=restart \
  -p:UseInterpreter=false \
  -p:RunAOTCompilation=true \
  -p:PublishTrimmed=true
```

After waking the Apple TV, run the physical phases in order against the signed
ignored audio acceptance app:

```bash
STAGE6_APP=artifacts/celeste-runtime/stage6-device/audio-acceptance-app-v6/CelesteTvOSRuntimeHost.app

scripts/run-celeste-tvos-persistence-acceptance.sh \
  --phase process-kill-prepare --app "$STAGE6_APP"
scripts/run-celeste-tvos-persistence-acceptance.sh \
  --phase process-kill-verify --app "$STAGE6_APP"
scripts/run-celeste-tvos-persistence-acceptance.sh \
  --phase prepare-before-restart --app "$STAGE6_APP"
```

Restart the Apple TV normally after the runner prints its readiness marker,
then run:

```bash
scripts/run-celeste-tvos-persistence-acceptance.sh \
  --phase verify-after-restart --app "$STAGE6_APP"
scripts/run-celeste-tvos-persistence-acceptance.sh \
  --phase replacement-install --app "$STAGE6_APP"
```

## Known limits and Stage 7 boundary

- Stage 6 is one shared standard app-domain store. It does not separate Apple
  TV users.
- Uninstall/delete may remove the defaults domain. A different bundle ID uses a
  different domain. Neither is an acceptance failure.
- No cloud backup, iCloud sync, cross-device sync or desktop-save migration is
  provided.
- The external Celeste and FMOD inputs, generated source, banks, apps, evidence,
  UserDefaults data and materialized saves remain ignored and uncommitted.
- Stage 7 must request and provision
  `com.apple.developer.user-management` with the planned current-user behavior,
  then prove Apple partitions the standard domain by current tvOS profile. It
  must not change the v1 envelope or add manual user IDs to keys.

## Acceptance conclusion

Verified facts show that the compact bridge, corruption recovery, allow-list,
size enforcement, privacy declaration, deterministic generation, supported
simulator runtime, 34-case full-AOT device diagnostic, process-kill recovery,
normal-game Settings/save matrix, background flush, real Apple TV restart and
replacement-install persistence all pass. Stage 6 is **passed** for one shared
standard app-domain store. This conclusion does not claim per-user storage,
cloud backup, uninstall survival, cross-device sync or complete controller
certification.
