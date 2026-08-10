# Celeste tvOS Stage 10B writable Save Manager report

## Result and scope

Stage 10B started from
`8e661abbfeb620daf8eacb1d406b756c909950a3`. The accepted change is committed
with message `feat: add writable tvOS save manager`; its final commit is
recorded in Git rather than embedded here to avoid a self-referential file.

The Stage 10A same-LAN Save Manager now supports exact, authenticated
replacement of Settings and all three save slots, intentional save-slot
deletion, and safe Settings reset. It remains explicitly activated from
Options, uses fixed logical targets only, and never exposes an arbitrary path,
the standard-defaults A/B keys, compressed v2 data, or a general-purpose file
server. ZIP import and an inline XML editor remain out of scope.

The locked toolchain remained .NET SDK 10.0.302, workload set 10.0.302.0, Xcode
26.6, tvOS SDK 26.5, and deployment target tvOS 16.0. The Stage 1 logical hash
remained `61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39`;
no native dependency was rebuilt.

## Mutation architecture and routes

The web layer still receives one immutable snapshot from the Stage 9B
persistence authority. It may request exactly one of these fixed mutations:

- `POST /replace/settings`
- `POST /replace/0`
- `POST /replace/1`
- `POST /replace/2`
- `POST /reset/settings`
- `POST /delete/0`
- `POST /delete/1`
- `POST /delete/2`

The established `POST /auth`, fixed `GET`/`HEAD` downloads, and
`GET`/`HEAD /download/all` remain. No arbitrary route parameter is accepted.
Settings absence was physically and serializer-tested as the normal signal for
Celeste to recreate defaults, so the public action is labelled **Reset
Settings**, not Delete Settings.

Each replacement uses `application/octet-stream`; accepted bytes are retained
exactly rather than normalised. The request parser requires Content-Length and
rejects chunked transfer, duplicate length, premature EOF, extra bytes,
pipelining, unsupported media types, and over-limit length before buffering the
body. The raw limits remain 65,536 bytes for Settings and 262,144 bytes for each
SaveData. Stage 9B's 98,304-byte compressed-entry, 126,976-byte complete
generation, and 262,144-byte A+B budgets remain authoritative and unchanged.

The HTTP layer cannot encode or compress persistence data or access
NSUserDefaults. It passes a fixed logical name, optional ordinary `.celeste`
payload, and expected generation/hash to `Stage6PersistenceStore`. Under its
single writer lock, the store copies the four current entry states, applies one
mutation, runs the exact reflection-free Settings or SaveData serializer,
creates a complete v2 candidate, checks every raw/compressed/envelope/bridge
budget, writes only the older or invalid A/B slot, synchronizes, reads it back,
decodes and decompresses it, validates all hashes and serializers again, and
verifies the generation and logical hash before accepting it. The previous
valid generation remains untouched throughout.

Deleting an already absent slot and replacing a payload with identical bytes
are deterministic no-ops: they do not advance the generation or require a
restart. A genuine successful change creates exactly one complete verified
generation.

## Authentication, CSRF, and conflicts

Stage 10A's six-digit activation code, fixed-time comparison, random 256-bit
in-memory session token, `HttpOnly`, `SameSite=Strict`, ten-minute sliding
session, managed four-connection gate, dynamic `NWListener` port, Bonjour, and
all stop paths are preserved.

Each authenticated session also receives a separate random 256-bit in-memory
CSRF token. Every mutation must carry that token and an opaque revision in
dedicated headers. The revision is an activation-keyed digest of the selected
generation and logical snapshot; it reveals neither the A/B slot nor save
contents. It changes after every committed mutation. A stale request receives
HTTP 409, and two requests carrying the same revision can produce at most one
commit. Mutation execution is serialized by the persistence authority rather
than last-write-wins browser ordering.

Tokens, access codes, cookies, revisions, save contents, and player strings are
not logged. Audit records contain only operation, logical target, bounded byte
counts, result category, resulting generation, and durable size metrics.

## Browser and TV safety UX

The authenticated self-contained page retains all downloads and ZIP backup,
then adds a fixed file chooser and Replace action for each target, Delete only
for present save slots, and Reset Settings. JavaScript shows the target,
selected filename and byte count through the browser's confirmation prompt;
deletion/reset has a separate explicit confirmation. The server independently
enforces authentication, CSRF, revision, type, serializer, and size checks.

After a committed mutation, downloads and ZIP are regenerated from the new
verified snapshot and the page reports the successful target plus **Restart
Celeste before continuing**. The TV overlay enters the same restart-required
state. The listener may be stopped, but the bridge refuses to dismiss the
frozen modal back into the stale game. A normal future UserIO commit is also
suppressed defensively after external mutation, so in-memory pre-import state
cannot overwrite the accepted durable generation. Only terminating/backgrounding
and reopening Celeste restores the new durable state and resumes gameplay.

Two real-browser defects found during physical acceptance were corrected. The
Content Security Policy initially omitted `connect-src 'self'`, so the browser
blocked every mutation `fetch` after confirmation. Successful mutation then
used `location.reload()` from a page returned by `POST /auth`, which could
replay authentication and lose the success state. The final page explicitly
allows same-origin fetch and uses `location.replace('/')`, guaranteeing a fresh
GET. Focused protocol tests lock both behaviours.

## Serializer, replacement, deletion, and rollback results

The mutation test matrix passed with ordinary uncompressed files:

- exact Settings and SaveData replacements preserved every accepted input byte;
- Settings submitted to SaveData and SaveData submitted to Settings were
  rejected;
- empty, malformed, unknown/duplicate/invalid, oversized, and wrong-root input
  was rejected before durable selection;
- download after replacement matched the accepted payload hash;
- deletion used explicit absence and download immediately reported absent;
- Reset Settings produced absence while all three save entry states remained
  unchanged;
- stale and simultaneous same-revision requests produced one commit and one
  conflict;
- `/download/all` reflected every completed replace/delete/reset;
- simulated write, read-back, decode, decompression, hash, serializer, and
  synchronization failures retained the prior selected generation;
- v0/v1/v2 decode, v1-to-v2 compatibility, highest-valid selection, corruption
  fallback, decompression bounds, and UserDefaults budgets remained passing.

An isolated full-AOT physical automation namespace exercised authentication,
malformed and over-limit rejection, exact 4,566-byte Settings replacement,
exact 18,756-byte SaveData replacement, delete, Settings reset, stale and
concurrent conflict, ZIP refresh, and restart-required state. It never read or
modified the production persistence keys. The package and live evidence are
stored only below ignored artifact roots.

## Protocol, persistence, and AOT validation

The deterministic protocol harness passes 66 tests: all accepted Stage 10A
coverage plus authentication/CSRF/revision mutation controls, body framing and
bounds, type validation, exact replacement, deletion/reset, conflicts,
post-mutation downloads/ZIP, restart state, and reusable connection-gate
coverage. The isolated Stage 9B/10B persistence diagnostic passes 111 tests,
including A/B rollback and all mutation failure stages.

Release device packages use full trimming, full AOT, and
`UseInterpreter=false`. A clean-build project-graph defect was found while
validating this gate: the nested generated Celeste build could resolve FNA
without the Stage 6 AOT-safe TitleContainer target. The generated
ProjectReference now propagates the exact Stage 6 property and extension target
without modifying the pinned FNA submodule. The final full-AOT automation and
production packages compile and pass the Stage 10B product verifier.

## Physical Apple TV and real-browser result

The isolated same-identity diagnostic replacement reached real Celeste first
draw, loaded all seven FMOD banks, and kept the listener dormant until explicit
Save Manager activation. The complete automated physical HTTP matrix passed
before manual testing. A real external browser then authenticated, replaced
Settings, reset Settings, deleted a controlled secondary slot, restored that
slot from its exact prior download, observed the success and restart warning
immediately after each operation, and remained responsive across all
operations. No valuable production save was deleted.

The final production-namespace build was installed as a same-identity
replacement without uninstalling. It restored the existing Stage 9B production
generation, reached first Celeste draw, loaded seven banks, reported no fatal,
tvStubs, or UserDefaults-size warning, and confirmed that both listener and
Bonjour were dormant during normal gameplay. User-assisted physical validation
confirmed that the browser workflow, downloads, mutation feedback, restart,
save restoration, controller, audio, and subsequent normal gameplay behaved as
expected.

## Public builder, unsigned IPA, and isolation

`./build-tvos.sh` remains the public entry point. Its host doctor, exact Celeste
and FMOD validators, ignored artwork/runtime staging, accepted Stage 1 reuse,
full-AOT unsigned publish, and Stage 8A/10A/10B verifiers were rerun after the
final changes. The final signing-ready IPA is 895,467,738 bytes with SHA-256
`6ee4832531d1a510fdc3ee76c25c958688f33c2693809235cdf7947107a80624`;
the result and full details remain in the privacy-safe public builder summary
and ignored verification evidence.

A new candidate clone with no generated outputs initialized all recursive
submodules and completed the documented public unsigned-IPA flow. The IPA has
the conventional `Payload/Celeste.app` layout, arm64 TVOS executable, tvOS 16.0
minimum, full AOT/trimming, seven banks, writable Save Manager product tokens,
no provisioning profile or usable signature, and no private/proprietary Git
candidate. That independent native-rebuild path produced an 895,467,880-byte
IPA with SHA-256
`49d9a34ceb0b3048e00a9a6d096c03e949468675ccb135777e3c6a263cc33ab9`.
As in earlier stages, the accepted reproducibility contract is the normalized
native/source logical manifests rather than byte-identical final ZIP metadata;
both builds reproduced the locked Stage 1 logical hash and passed the same
package verifier.

Generated Celeste source, game content, FMOD archives/headers/banks, upload
fixtures, downloaded files, app bundles, IPAs, LAN addresses, access codes,
tokens, signing values, profiles, device identifiers, and raw console evidence
remain under ignored `.build/`, `artifacts/`, or `dist/` roots. Stage 10B adds
explicit ignore rules for its AOT build roots and regeneration logs.

## Failures and focused corrections

1. Browser mutations were blocked by CSP; same-origin `connect-src` was added.
2. Reloading a successful POST-auth document could replay authentication; the
   success path now performs an explicit GET-root replacement.
3. An early live verifier posted credentials to the wrong route; its fixed
   protocol now mirrors the product's `/auth` contract.
4. Concurrent-test helper naming shadowed a Python module; the local verifier
   was corrected without changing the product.
5. A stale-conflict fixture did not establish deterministic revisions; it now
   captures and reuses the exact initial revision.
6. A broad tvStubs log pattern matched a harmless verifier string; the runtime
   matcher now detects only actual invocation evidence.
7. An incremental package lacked the compiled asset catalog; a clean publish
   produced the correctly branded app.
8. Clean full-AOT project evaluation lost the Stage 6 FNA extension property;
   focused ProjectReference property propagation fixed the graph.
9. CoreDevice console capture did not terminate promptly on SIGTERM; the
   automation runner now uses a bounded wait, then kills only its exact child
   and terminates only the exact remote app it launched.

## Remaining scope

There is no cloud sync, unattended server, arbitrary file browser, general ZIP
import, or inline XML editor. Management requires explicit foreground
activation and same-LAN access. A successful mutation intentionally requires a
full app restart; Stage 10B does not attempt a fragile in-process Celeste reset
or private Home-button API. These boundaries are deliberate safety properties,
not missing persistence validation.
