# Beginner-friendly project experience — Stage 21

Status: **PASS**

Stage 21 reorganizes the current public documentation around a newcomer's
tasks while preserving the technical reference and historical evidence. It is
a documentation-only change: no runtime, build, native, generated-Celeste,
cloud-workflow, persistence, networking, controller, audio, signing, or
packaging behaviour changed.

## Git identity and scope

- Starting commit: `641e86e4ed164cdf93f602ce2f11436449654d6e`.
- Feature branch: `feature/beginner-project-experience`.
- Final feature commit: the commit containing this report, recorded by the
  feature-branch history and completion response.
- Immutable `v1.0.0-rc.1^{}` remained
  `ee52b0868df091746f134d95d4f020f94f23d4fb`.
- Immutable `v1.0.0-rc.2^{}` remained
  `641e86e4ed164cdf93f602ce2f11436449654d6e`.
- `tvos-port`, both release-candidate tags, and upstream were not modified.

The GitHub repository was intentionally not renamed. The visible product name
is now consistently **Celeste for Apple TV**, while the existing fork identity,
default branch, clone commands, links, and frozen cloud source pin remain
stable. A remote rename is deferred until the feasibility and intended scope
of a future modern iOS lane are known, so the project does not choose a new
long-term platform identity prematurely or break existing automation.

## Files changed

- `README.md`
- `docs/README.md`
- `docs/BUILDING.md`
- `docs/CLOUD_BUILDING.md`
- `docs/CELESTE_INPUTS.md`
- `docs/STATUS.md`
- `docs/TROUBLESHOOTING.md`
- `docs/history/README.md`
- this report
- `scripts/verify-celeste-tvos-stage21.py`

No release notes or earlier report was rewritten.

## Documentation architecture

Before Stage 21, the root README contained accurate but intimidating detail:
the full dependency/tool table, three storefront acquisition tutorials,
signing, persistence internals, feature internals, tested matrices, and
developer architecture all competed with the first build decision.

The root now explains what the project does, what the user must supply, and the
four practical steps: obtain Celeste, obtain FMOD, choose local or private-cloud
compilation, then sign/install. One small Mermaid decision diagram reinforces
the Mac-versus-cloud choice. Store commands live in `CELESTE_INPUTS.md`, local
toolchain and reproducibility detail in `BUILDING.md`, cloud procedure and
security detail in `CLOUD_BUILDING.md`, and implementation detail in
`STATUS.md`. `docs/README.md` is now a task/audience navigation hub.

The root retains a compact capabilities summary, Save Manager explanation,
short limitations, credits, legal boundary, and direct paths to
troubleshooting, development documents, releases, and historical evidence.

## Beginner guidance

The FMOD instruction now consistently names **FMOD Engine iOS/tvOS 1.10.09
build 97915**, links directly to
`https://www.fmod.com/download?version=1.10.09#fmodengine`, distinguishes Engine
from Studio, tells users to choose the iOS package containing the tvOS
libraries, and notes that an FMOD login may be required.

The build choice now states explicitly:

- with an Apple silicon Mac, the local `build-tvos.sh` route can compile and
  can sign/install through the accepted Xcode/Personal Team path;
- without a suitable Mac, compilation can run in the user's own private GitHub
  repository, but the resulting IPA is unsigned and still needs a separate
  signing/install path.

The cloud summary explains the private template repository, fixed private input
and output Releases, public-repository refusal, cleanup workflow, and the fact
that GitHub never needs store, FMOD-account, or Apple-signing credentials.

`CELESTE_INPUTS.md` now begins with “Which download should I use?” and retains
the exact nine-profile matrix and reproducible itch.io, Steam-console, and
optional Epic/Legendary acquisition detail. The root links there rather than
duplicating depot commands.

## User-journey review

### User A — Windows user, no Mac, owns Steam Celeste

**PASS.** The first README screen explains the Apple TV product and lawful
input boundary. The build-choice section directs this user to private GitHub
compilation, clearly labels its output unsigned, and links the six-step cloud
guide. The input guide recommends Steam's built-in console Linux depot. FMOD's
exact Engine/iOS/version requirement and separate signing boundary are visible
before any developer architecture.

### User B — Mac user, owns itch.io Celeste

**PASS.** The build-choice section points directly to the local route. The
README supplies the host-doctor command, while the local guide opens with a
prerequisite checklist and clearly separates automatically detected values,
values the user supplies, and advanced overrides. The input guide gives itch.io
as the simplest direct-library route.

### User C — developer interested in architecture

**PASS.** The README's developer section and task-based documentation hub lead
to the current architecture, native pipeline, advanced/reproducible build,
tracked verifiers, contribution rules, releases, and chronological history.
Those details remain fully available without blocking the two beginner paths.

## Visual and privacy audit

The only added visual is repository-native Mermaid text for the build-choice
flow. It repeats its essential information in adjacent prose, works without
colour-only meaning, adds no binary asset or metadata, and carries no game
artwork, personal UI, network address, access code, device identifier, or
licensing ambiguity. No screenshots or external images were necessary.

Current user-facing documents were checked for personal home paths and LAN
addresses. The two remaining absolute URLs for this repository are copyable Git
clone commands in `BUILDING.md`, where a relative link cannot perform the
operation. Other internal navigation is repository-relative. The separate
cloud-template repository link and upstream/third-party project links remain
external by design.

## Validation

- Stage 21 focused verifier: PASS, 201 checks and 112 scoped relative links.
- Repository-wide documentation links: PASS, 116 links through the Stage 14
  documentation/product verifier.
- Stage 20 deterministic release-candidate verifier: PASS, 150 checks.
- Stage 19's historical standalone verifier is intentionally pinned to the
  superseded pre-RC2 cloud source. It is not directly applicable after Stage
  20; the passing Stage 20 verifier rechecks current semantic runtime names and
  the accepted current cloud pin.
- Cloud-template deterministic tests: PASS, 32 checks. The historical Stage
  18C wrapper is likewise pinned to its then-current source; Stage 20 verifies
  the accepted replacement pin.
- Repository/privacy verifier: PASS.
- Python syntax for the new verifier: PASS.
- `git diff --check`: PASS.

The Stage 21 verifier locks both RC tag targets, the production cloud source
pin, the documentation-only file boundary, the FMOD link and beginner paths,
current-document privacy, relative links, and byte identity for all 28 earlier
tracked stage reports. No full IPA build, live cloud workflow, or physical
Apple TV run was performed because no product semantics changed.
