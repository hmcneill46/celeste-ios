# Celeste tvOS Stage 9B compressed persistence report

## Result

Stage 9B **passed**. The starting commit was
`a29c88b9b016ae895adc43326346572cedea1022`. The accepted change is committed
with message `fix: add compressed durable tvOS save storage`; its final commit
is recorded in Git rather than embedded here to avoid a self-referential file.

The original physical Chapter 5 save bug is fixed. A normal save at the first
carry-Theo progression boundary produced a 33,183-byte slot, exceeded the old
32,768-byte policy, and committed successfully as persistence v2. A following
33,455-byte save also succeeded. No room, Theo, gameplay, serializer, or
UserIO special case was added.

## Root cause inherited from Stage 9A

Stage 6 rejected SaveData in `Stage6PersistenceStore.Capture` once the raw file
exceeded 32,768 bytes. The rejection occurred before envelope encoding and
before UserDefaults. The last accepted physical slot was 32,725 bytes; six
retained rejected candidates measured 32,839–33,985 bytes. The latest was
33,259 bytes. Theo introduced only ordinary growing session flags, not a new or
unsupported serializer state. The 124 KiB envelope limit, 256 KiB A+B limit,
Foundation I/O, AOT, and Apple's UserDefaults warning were not causal.

## v2 architecture

Celeste continues to use normal, uncompressed `settings.celeste` and numbered
`.celeste` files in its private materialized session directory. On a durable
commit the bridge validates the exact serializer graph, independently
compresses each present allow-listed entry, writes one complete generation to
the older A/B slot, synchronizes, reads it back, decompresses and validates it,
and only then selects it.

The format retains the existing `CTVOSPV1` magic and these exact standard
application UserDefaults keys:

- `CelesteTvOS.Persistence.v1.A`
- `CelesteTvOS.Persistence.v1.B`

Each v2 entry carries its fixed logical name, present/deleted state, serializer
kind, compression identifier, declared raw and stored lengths, stored bytes,
stored SHA-256, and raw SHA-256. The complete envelope retains generation,
Celeste identity, serializer versions, completion marker, and outer SHA-256.
Only `settings`, `0`, `1`, and `2` are accepted.

## Compression and bounds

The implementation uses `System.IO.Compression.ZLibStream` with
`CompressionLevel.SmallestSize`, which is zlib level 9 in the locked .NET 10
toolchain. Algorithm identifier 1 is committed for v2. Identical inputs gave
identical output on arm64 simulator and full-AOT physical tvOS. No reflection,
runtime code generation, interpreter, or new native dependency is used.

The distinct limits are:

| Limit | Bytes | Purpose |
| --- | ---: | --- |
| Settings raw | 65,536 | Bounded serializer/decompression input |
| Each SaveData raw | 262,144 | Bounded serializer/decompression input |
| Each compressed entry | 98,304 | Stored-entry safety |
| Complete envelope | 126,976 | Conservative UserDefaults generation budget |
| A+B bridge | 262,144 | Conservative total bridge budget |

The 256 KiB SaveData safety ceiling is evidence-derived rather than a doubling
of the failed limit. The largest source-valid near-complete fixture was 181,096
raw bytes and 13,240 compressed bytes, leaving 81,048 raw bytes (30.9%) of
allocation margin. The fixture covers later chapters, Areas/AreaModeStats,
current Session, flags, LevelFlags, DoNotLoad, IDs, checkpoints, A/B/C sides,
202-strawberry-capable state, hearts/cassettes/gems and golden-related fields.

Three separately progressed large slots plus Settings measured 487,964 raw
payload bytes and 36,554 compressed payload bytes. Its full v2 generation was
36,967 bytes (29.1% of the envelope budget); two generations were 73,934 bytes
(28.2% of the A+B budget). Ordinary complete-game progression therefore has
substantial measured headroom. There is no evidence that it can reach the new
limits, although deliberately malformed or unbounded future data still fails
closed.

Bounded decompression checks stored and declared raw lengths before allocation,
streams through an exact output ceiling, rejects short/long output, truncated
or malformed streams, trailing/non-canonical compressed data, unknown
algorithms, either per-entry hash mismatch, serializer-invalid payloads, and an
invalid outer hash.

## Compatibility and migration

Formats v0, v1 and v2 decode under the same highest-valid-generation selection
rule. A higher-generation v1 remains preferred over a lower-generation v2.
Unknown future formats are preserved and fail closed rather than being
overwritten.

The physical production inspection restored generation 74 from two valid v1
slots totaling 135,856 bytes. Launch and disposal performed no write. Installing
the candidate as a same-identity replacement preserved those values. The first
intentional Chapter 5 save wrote v2 generation 75 into older slot A, read it
back and selected it while untouched v1 generation 74 remained in B. The next
intentional Settings save wrote v2 generation 76 into B. A clean relaunch then
selected generation 76 from two valid v2 slots.

The deterministic suite also verifies v0/v1 launch without rewrite,
v0/v1-to-v2 intentional migration, mixed v1/v2 selection, preservation of the
old v1 during the first v2 write, interrupted migration before and after a
complete write, advancement to a second v2, and future-format preservation.

## Diagnostic results

The trimmed arm64 simulator executed 99 tests. The signed, full-trim,
full-AOT `tvos-arm64` diagnostic with `UseInterpreter=false` executed the 93
public/synthetic tests; the six additional simulator tests used ignored lawful
retained Stage 9A files and were deliberately absent from the device/product
bundle.

Coverage includes basic v2 states, compression determinism, exact limit
boundaries, incompressible valid input, every decompression/integrity failure,
v0/v1/v2 migration/recovery, actual `UserIO.Save`/load/delete, lifecycle flush,
eight concurrent commits, failure injection, and large profiles. Corrupt newest
v2 falls back both to an older v2 and to v1. A simulated interrupted write never
produces mixed state. UserDefaults warning injection retains the prior
generation.

Ignored retained candidates roundtripped byte-for-byte through v2:

| Candidate | Raw bytes | zlib bytes |
| --- | ---: | ---: |
| First rejected | 32,839 | 3,349 |
| Latest rejected | 33,259 | 3,403 |
| Largest rejected | 33,985 | 3,490 |

No save contents were logged or committed.

## Physical production evidence

The signed production candidate used the existing app identity and Personal
Team and was installed as a replacement, never uninstalled. It restored both
v1 generations without launch-time migration, reached a real Celeste first
draw, and loaded all seven real FMOD banks.

At the formerly failing Chapter 5 boundary:

| Generation | Trigger | Raw logical bytes | Compressed payload | Envelope | A+B after commit |
| ---: | --- | ---: | ---: | ---: | ---: |
| 75 | Save slot 0 | 68,121 | 7,288 | 7,701 | 75,629 |
| 76 | Settings following save | 68,393 | 7,341 | 7,754 | 15,455 |

The primary slot measured 33,183 then 33,455 raw bytes. Both saves completed,
read-back verification passed, Celeste showed no save-failure overlay, and no
UserDefaults size warning fired. The user confirmed the former failure no
longer occurs.

A clean second launch restored generation 76 from valid-v2 A and B and reached
first draw with seven banks. That also proves external process termination after
the verified commit did not damage state. Physical background/foreground
produced successful unchanged v2 flushes on resign-active and background,
stopped haptics, paused FMOD, and resumed normally. A real Apple TV restart
restored the exact pre-restart generation and logical hash from both v2 slots,
then reached first draw and loaded seven banks. Same-identity replacement
installation had already preserved and migrated the original production data.

## Product and repository isolation

The production build remains Release `tvos-arm64`, fully trimmed, full AOT and
`UseInterpreter=false`. Normal FMOD audio, controller input, haptic cleanup,
branding, privacy declaration and the existing iOS lane are unchanged. No
Stage 1 native library was rebuilt.

Raw saves, retained fixtures, console logs, generated Celeste source/content,
FMOD inputs, apps, IPAs, signing values and device identifiers remain below
ignored roots. The tracked changes contain only bridge/diagnostic source,
policy, verifiers, scripts and documentation.

The signing-ready unsigned IPA independently republished after the persistence
change and passed the Stage 8A verifier. It is 895,173,334 bytes with SHA-256
`11beb3a7d4290ec2437a84906593dcea86ff43f7a57c505f7f1cb478c9e609c6`.
Its payload is one unsigned `Celeste.app`, arm64 TVOS with tvOS 16.0 minimum,
full-AOT product code, all seven banks, branding and the v2 bridge; it contains
no usable signature, embedded provisioning profile, or User Management
entitlement.

A separate cache-free candidate clone used the documented HTTPS recursive
submodule command, passed builder help and host doctor, rebuilt the complete
locked Stage 1 graph, reproduced logical hash
`61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39`,
prepared FMOD and generated Celeste from the exact external inputs, and
completed another verified unsigned IPA. That independently built IPA used a
neutral test identity and SHA-256
`427e87360d1ca33354b0323d3d9bc02942a16ca65b7c4b08b1d96df95980686b`.
No ignored output was copied into the clone.

An abandoned first rehearsal invoked `git clone --recurse-submodules` without
the documented `git://` to HTTPS rewrite and was interrupted during the legacy
transport timeout. Reusing that partial directory left one submodule worktree
empty, which the unchanged prior-foundation verifier correctly rejected. The
disposable checkout was restored to its locked commit and the documented flow
then passed; no verifier or product gate was weakened.

## Known limitations and Stage 10 boundary

- Saves remain shared between Apple TV users; Personal Teams cannot provision
  Apple User Management.
- There is no iCloud, cloud backup, cross-device sync or uninstall-survival
  guarantee. Changing the bundle identifier selects another defaults domain.
- The stored format is internal. A future Save Manager can request one exact
  allow-listed uncompressed logical payload through the narrow bridge boundary;
  it does not need to parse v2. Import must use the same serializer and commit
  path. Stage 9B does not implement that user interface.
