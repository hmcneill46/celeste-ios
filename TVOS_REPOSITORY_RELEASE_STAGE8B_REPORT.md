# Stage 8B: public self-build repository release

Status: **PASS — documentation, clean-clone packaging, direct installation,
runtime, and repository-isolation gates passed.**

## Scope and baseline

- Branch: `tvos-port`
- Starting commit: `5163729f7423d2694cdc2f7eb7bf2d77bef89321`
- Candidate/final commit: the commit containing this report
- Intended commit subject: `docs: prepare Celeste tvOS repository for self-builds`
- User fork: `hmcneill46/celeste-ios`
- Upstream: `RoootTheFox/celeste-ios` (read-only and untouched)

The Stage 8A worktree began clean at the exact expected commit, tracked
`origin/tvos-port`, and had the expected pinned submodule revisions. Existing
signed and unsigned Stage 8A outputs were reverified without rebuilding and
both passed. The fork's default branch was `main` at the start of Stage 8B.

## Public documentation

The root README is now a tvOS-first landing page. It leads with the unofficial
self-build/legal boundary and documents status, requirements, Quick Start,
game and FMOD acquisition, Xcode/free signing, Apple TV pairing, direct install,
unsigned IPA packaging, saves, controllers, limitations, credits, and legal
scope.

New public guides:

- `docs/BUILDING.md`: automation, inputs, modes, preparation architecture,
  outputs, and verification.
- `docs/TROUBLESHOOTING.md`: symptom/cause/fix guidance derived from real
  builder failures and accepted runtime boundaries.
- `docs/STATUS.md`: current support matrix, architecture, known limitations,
  isolation, and links to historical reports.
- `CONTRIBUTING.md` and focused GitHub issue/PR templates: contributor checks
  plus strong proprietary/private-data warnings.

The public repository verifier mechanically checks required claims, relative
Markdown links, builder help/options, executable bits, generated-output ignores,
forbidden candidates, and private absolute paths.

## Supported external inputs

The tested and supported game input is explicitly:

```text
Celeste 1.4.0.0 — itch.io Linux download
```

Steam Linux/macOS/Windows and all other store/platform builds are untested. A
different distribution may work only if its unchanged files pass the exact
validator; it is not claimed as supported.

The exact audio SDK input is:

```text
FMOD Engine for iOS/tvOS 1.10.09, build 97915
```

Documentation directs users only to the official FMOD account/download flow.
No Celeste or FMOD acquisition, proprietary material, or unofficial mirror is
automated or redistributed.

## Builder correction

Clean-clone review found that downstream scripts require `gmake`, `monodis`,
`nmedit`, `file`, and `rg`, but the top-level preflight did not name them. The
preflight now checks those tools before any partial preparation. It also rejects
noninteractive use without an explicit `--mode`, and its Celeste prompts name
the tested itch.io Linux input rather than implying wider support. No runtime,
controller, persistence, audio, native dependency, or iOS path changed.

The first literal recursive submodule rehearsal also showed that pinned FNA
nested URLs still use GitHub's retired `git://` transport. The public command
now applies a command-scoped HTTPS rewrite; no global Git setting or upstream
URL is changed. The first full native build under `/tmp` then exposed that
Xcode and clang can independently spell the same temporary root as `/tmp` and
`/private/tmp`, preventing SDL2's intended `-ffile-prefix-map` match. Stage 1
now resolves generated build/output roots and maps both equivalent APFS
spellings when needed. This restores the already accepted SDL object content
and logical hash without changing source, target, dependency revisions, or
runtime behavior.

Once that hash passed, the builder exposed a second empty-clone assumption:
FMOD preparation defaulted to the development checkout's historical
`rebuild-e` directory even after the builder had created and verified
`self-build`. The builder now reads the exact Stage 1 artifact path from the
host-staging manifest, confines it to `artifacts/tvos-native/`, and passes it to
FMOD preparation. Repeat development builds still reuse their accepted set.

## Clean-clone unsigned IPA

An independent clone began without repository-local `.build`, `artifacts`,
`dist`, generated source, generated artwork, an app, an IPA, or signing
configuration. It used only the lawful external Celeste/FMOD inputs and the
machine's installed toolchains/caches. The documented submodule command and
builder help worked before preparation.

The clone fetched and built the locked Stage 1 dependency graph, restored the
accepted native logical SHA-256
`61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39`,
regenerated managed source and artwork, staged FMOD and the exact seven banks,
published full-AOT/full-trimmed `TVOS arm64`, and created a real unsigned IPA.
The clean-clone rehearsal, including the focused corrective retries described
above, took approximately 30 minutes from clone to accepted package on the
test Mac. There is no speed acceptance threshold.

Accepted output facts:

| Property | Result |
|---|---|
| Display name | `Celeste` |
| Payload | `Payload/Celeste.app` |
| Architecture / platform | `arm64` / `TVOS` |
| Minimum OS | `16.0` |
| App bytes | `1,215,233,341` |
| IPA bytes | `895,100,472` |
| IPA SHA-256 | `653837c6665a5acef2bcf24821c8b4dfcc02f45e25fe95fce56614ff07ce87c0` |
| FMOD banks | exactly 7 |
| Signature / profile | absent |
| Full AOT / interpreter | yes / no |
| Stage 6 standard-defaults bridge | present |
| User Management / paid entitlement | absent |

The package is accurately labelled **signing-ready unsigned IPA**. It is not
installable until a compatible tvOS sideloading tool supplies a signature and
profile. atvloadly/Sideloadly compatibility is structurally verified only; no
Stage 8B third-party-tool installation is claimed.

## Clean-clone Personal Team installation

A second independent clone also began without repository-generated state or
local signing props and performed its own native, managed, content, FMOD, and
artwork preparation. It used the already configured primary Personal Team and
stable private app identity from ignored machine-local configuration; no value
was copied into a tracked file or public report. The clone did not reuse the
unsigned clone's outputs.

The full Release `tvos-arm64`, full-AOT, full-trimmed,
`UseInterpreter=false` build was automatically provisioned, signed, installed
as a replacement (not uninstalled), and launched on the paired Apple TV. The
rehearsal took approximately 22 minutes from clone to accepted device result.
Independent static and runtime evidence showed:

- strict code-sign verification passed;
- one arm64 tvOS executable with minimum tvOS 16.0;
- display name `Celeste`;
- all seven FMOD banks became ready (`922` events, `118` buses, `3` VCAs);
- the real game reached its first Celeste-owned draw at 1920x1080;
- no User Management entitlement was present; and
- the installed app remained on Stage 6 shared standard-UserDefaults storage.

The existing stable app identity was retained, so this replacement did not
intentionally create another defaults domain or delete the user's saves.
Stage 8A's physical branding acceptance remains valid: the black-backed,
centred strawberry icon is unclipped, parallax works, the static Top Shelf
splash is correctly framed, and launch/gameplay continued to work.

## Negative-input tests

A third disposable clone exercised the noninteractive preflight without
altering the real Celeste or FMOD inputs. Every case exited nonzero before a
misleading partial product was produced:

| Case | Accepted boundary |
|---|---|
| Missing Celeste path | stated that the Celeste installation was not provided |
| Invalid Celeste directory | stated that the installation was incomplete |
| Missing `Celeste.png` | named the missing artwork file |
| Missing FMOD | stated that the FMOD SDK was not found |
| Invalid FMOD | stopped at the exact FMOD validator |
| Noninteractive mode without `--mode` | required an explicit build choice |
| Invalid bundle identifier | named the invalid identifier |
| Direct install without a selectable Personal Team | explained the missing free Personal Team |

The final case used an isolated command wrapper to simulate the absence of an
Xcode Personal Team; it did not remove or modify any real Xcode account.

## Documentation and repository audit

The repository-owned verifier passed. It checks public-document claims and
local Markdown links, all public script help, executable bits, ignore rules,
tracked extensions, proprietary-name/artwork candidates, private local props,
absolute home paths, and root Finder metadata. Shell syntax checks, Python
compilation, `git diff --check`, submodule state, and manual candidate review
also passed.

External documentation links were checked against Apple, Microsoft, FMOD,
itch.io, and GitHub. The itch.io endpoint briefly rate-limited a repeated
automated request after previously returning successfully; that was treated as
a remote rate limit, not a broken-link correction. All repository-relative
links resolve.

After both full builds, `git status --short` contained only the expected
candidate documentation/scripts. Generated source, native artifacts, artwork,
apps, IPA, content, FMOD files, signing configuration, provisioning data,
device evidence, and raw logs stayed under ignored roots. No existing iOS
application/project/build file changed.

## GitHub publication

The fork was confirmed as `hmcneill46/celeste-ios`; upstream remained
`RoootTheFox/celeste-ios` and was never written. The fork defaulted to legacy
`main` when this commit was prepared. All local gates passed before publication
was authorised. Publication is limited to `origin/tvos-port`; changing the
fork default to `tvos-port` and the final remote-clone smoke test intentionally
follow the successful push of this self-referential report and are recorded in
the final task handoff. No pull request, release, IPA upload, force push,
history rewrite, or upstream mutation is permitted.

## Exact public Quick Start

The preferred final workflow, conditional on the user fork defaulting to
`tvos-port`, is:

```bash
git clone https://github.com/hmcneill46/celeste-ios.git
cd celeste-ios
git -c url.https://github.com/.insteadOf=git://github.com/ \
  submodule update --init --recursive
./build-tvos.sh
```

## Known limitations

- Personal Team provisioning expires after about seven days.
- Saves are durable but shared between Apple TV users; there is no User
  Management entitlement, iCloud, cloud sync, cross-device sync, or uninstall
  survival guarantee.
- Steam and all non-itch.io-Linux game sources are untested.
- DualSense is the physically tested controller; no universal controller
  certification is claimed and the Siri Remote is not a gameplay controller.
- The arm64 simulator is no-audio; real FMOD audio is the physical-device lane.
- The app is roughly 1.1 GiB before IPA compression.
- atvloadly compatibility is structural/static only unless separately recorded;
  no physical atvloadly installation is claimed.
- This is not an App Store or paid-entitlement distribution.

## Isolation

The candidate contains only source, scripts, documentation, templates, locks,
and existing open-source notices. User-owned game files and artwork,
generated/decompiled game source, FMOD SDK files and banks, apps/IPAs, local
configuration, signing/provisioning material, identifiers, raw evidence, and
private paths remain ignored and uncommitted.
