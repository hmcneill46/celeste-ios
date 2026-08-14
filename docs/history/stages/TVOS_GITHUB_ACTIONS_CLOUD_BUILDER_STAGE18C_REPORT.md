# Private GitHub Actions cloud builder — Stage 18C

Status: **PASS**

This record covers the production private cloud-builder template. It changes no
Celeste runtime or product behavior; the cloud workflow invokes the already
accepted public self-builder and produces an unsigned IPA.

## Baseline and repositories

- Starting main-project commit:
  `90ebb023f3043222bc67e72922ae4d68223f009c`.
- Main-project feature branch: `feature/github-actions-cloud-builder`.
- Implementation commits were `2529e75`, `33b8fe0`, and `75053f5`; the final
  report commit is recorded in the Stage 18C completion response and branch
  history.
- Immutable `v1.0.0-rc.1^{}` remained
  `ee52b0868df091746f134d95d4f020f94f23d4fb`.
- Public template repository:
  `https://github.com/hmcneill46/celeste-tvos-cloud-builder`.
- Final public-template commit:
  `825ea17bf9f4cd7d3be410fd2d477dbd9a445c1a`.
- GitHub's API reported the template repository as public, with
  `is_template=true` and default branch `main`.
- The template pins the accepted public build source to
  `90ebb023f3043222bc67e72922ae4d68223f009c`, not a floating branch.

The tracked `cloud-builder-template/` directory is the canonical template
source. A five-file allow-listed export/check helper prevents the separate
public repository from drifting or receiving build inputs.

## Workflow and security architecture

The Build workflow is manual-only `workflow_dispatch`, runs one serialized job
on standard ARM64 `macos-26`, and times out after 120 minutes. It requests only
`contents: write`. Cleanup additionally requests `actions: write` solely for
its optional namespaced cache deletion.

Official external Actions are pinned to immutable commits:

- `actions/checkout` v7.0.1:
  `3d3c42e5aac5ba805825da76410c181273ba90b1`.
- `actions/cache` v6.1.0:
  `55cc8345863c7cc4c66a329aec7e433d2d1c52a9`.

The first workflow step queries the repository through GitHub's API and
requires both `private=true` and `visibility=private`. Public-template runs
stop before checkout or any input operation. No Steam, Epic, FMOD, or Apple
credential is accepted.

The fixed private input Release/tag is `celeste-tvos-inputs` and must contain
exactly one Celeste ZIP and one official FMOD DMG. GitHub asset size/digest,
archive paths/types/counts/expanded size, and bounded extraction are checked
before the existing exact Stage 17/FMOD validators. FMOD is mounted read-only.

The workflow checks ARM64, Xcode 26.6, tvOS SDK 26.5, .NET SDK 10.0.302,
workload set 10.0.302.0, and a 25 GiB free-disk minimum. Its narrowly pinned
.NET installer adds runtime 6.0.36 only for locked ILSpy. The normal
`build-tvos.sh --non-interactive --mode ipa --no-color` path owns compilation,
eight timed groups, 60-second heartbeats, failure tails, full trimming, full
AOT, and interpreter-disabled verification.

Only these redistributable/open-source paths may be cached:

- `artifacts/tvos-native/self-build`
- `.build/tvos-host`

The exact cache key includes source, runner, image, toolchain, accepted native
logical hash, and relevant tracked native-input hashes. A hit is independently
verified against native logical SHA-256
`6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`.
Celeste, FMOD, generated source/content, app bundles, saves, and IPAs are not
cached.

The verified private output Release/tag is `celeste-tvos-output`, containing
`Celeste-tvOS-unsigned.ipa` and privacy-safe build metadata. It uses no normal
Actions artifact. Existing output blocks overwrite. An unconditional final
step detaches FMOD and removes runner-side input, generated, app, and IPA work.
The explicit cleanup workflow deletes only the two fixed Releases/tags and can
optionally delete only the cloud-builder cache namespace.

## Deterministic validation

The **32-test** Stage 18C suite uses synthetic inputs to cover privacy interpretation,
asset classification, digest verification, bounded ZIP extraction, traversal,
links/special files/case collision, disk policy, source identity, output
overwrite protection, cleanup allow-list/idempotency, summary generation,
cache allow-list, tracked-native-input cache identity, artifact exclusion, and
manual-only triggering. The static verifier also checks workflow permissions,
immutable Action pins, source/native pins, documentation, export inventory,
and Stage 18B integration.

Final inherited regression results were Stage 18B **21**, Stage 17 **56**,
Stage 16B **39**, Stage 15 **31**, Stage 13B **21**, Stage 12B **16**, Stage 11
**38**, and Stage 10 **66**, with Stage 9B persistence, Stage 14 product/docs,
repository/privacy, builder help, and host doctor also passing. No Celeste
runtime/product source changed, so a redundant physical Apple TV installation
was not required for this orchestration-only stage.

## Live production acceptance

### Template and privacy gates

A new repository was created through GitHub's actual template mechanism at
`hmcneill46/celeste-tvos-cloud-builder-acceptance`. Before any private upload,
GitHub's API reported `private=true`, `visibility=private`, and template source
`hmcneill46/celeste-tvos-cloud-builder`. Its final acceptance-template commit
was `13fcbb22ba1a21a2b3db37abbf8478d7f0eaeab7`.

The final public template was manually dispatched as run `31789385771`. It
failed at its first privacy check in about two seconds, skipped checkout,
input, cache, build, verification, and publication, then ran its cleanup and
failure summary. No input was required or present.

The private Release `celeste-tvos-inputs` contained exactly one lawful Celeste
ZIP and the original official FMOD DMG. Detection selected profile
`steam-windows-fna-1.4.0.0-manifest-1981411158533599226`, canonical class
`celeste-1.4.0.0-a`. FMOD Engine 1.10.09 build 97915, arm64 TVOS archives, and
tvOS 16.0 compatibility all passed. The runner mounted FMOD read-only and
removed the transferred Celeste ZIP after validation.

The 25 GiB disk preflight passed. The accepted fresh log retained all eight
balanced builder groups and 17 one-minute heartbeats. The lowest heartbeat
reported 87 GiB free. The default log was concise/no-colour; a privacy scan
found no personal Mac path, credential header, private key, or unmasked secret.

### Fresh production build

Corrected cache-miss run `31789383776` passed on standard ARM64 `macos-26`:

- total workflow: **24m48s**;
- existing eight-phase builder: **21m02s**;
- independent IPA verifier: **1m02s**;
- cache save: **3s**;
- private Release upload: **43s**;
- final runner cleanup: **14s**.

The run performed a genuine cache miss, built from the exact public source,
and passed full trimming, full AOT, `UseInterpreter=false`, seven-bank FMOD,
and Stage 14/15/16 package verification. The verified unsigned IPA was
895,768,180 bytes with SHA-256
`b7c23fca3d8c1566f1cf6454b89da4ef8563a4b631dd6ffbe081e7ffe4e16800`.

An earlier live run exposed that the tracked-native-input cache component was
being hashed relative to the template checkout, producing the empty digest.
Product correctness was still protected by the immutable source SHA, accepted
native logical hash, and independent hit verification, but the key did not
meet the stronger Stage 18C identity policy. Commit `75053f5` corrected the
working directory and added a deterministic regression. The accepted cache
key now contains non-empty tracked-input digest
`a0043958c233be1ca6b84a92a7c7c814b45104f4b5cdd5df1f7e43b7dc63278e`.

### Safe-cache repeat

The saved cache was **37,866,061 bytes** and contained only:

- `artifacts/tvos-native/self-build`;
- `.build/tvos-host`.

Cached run `31791369757` passed in **15m34s**. Exact-key restore took four
seconds; independent cache verification passed before the builder ran. The
builder then took 11m05s, the unchanged full verifier took 1m27s, publication
passed, and final runner cleanup passed. Cache save was correctly skipped.

The repeat IPA was 895,768,190 bytes with SHA-256
`5879388d34f95fd8efef3b17b463a64849fa9cfeb368adaad7aec564cc3a872b`.
Its small byte-level difference from the fresh IPA was ZIP/package metadata;
both independently passed the same semantic verifier chain.

### Output round trip and cleanup

For both accepted runs, the private `celeste-tvos-output` Release contained
only `Celeste-tvOS-unsigned.ipa` and a 380-byte privacy-safe metadata file.
Authenticated re-downloads matched workflow metadata and GitHub's server
digest, ZIP integrity passed, and the complete local Stage 14/15/16 verifier
chain passed again. No normal Actions artifact was created.

The unconditional runner cleanup passed after the successful fresh and cached
runs. It also passed after focused failure run `31786321533`, where validation
failed early before any build/cache/output operation and the failure summary
remained actionable.

Actual cleanup workflow run `31792808343` removed exactly the private input and
output Releases and tags while retaining the safe cache. Run `31792857937`
then removed only the namespaced cache. Idempotency run `31792904389` passed
with everything already absent. Final acceptance-repository state was zero
Releases, zero tags, zero Actions caches, and zero Actions artifacts.

The public Celeste repository, public template repository, and private
acceptance Git histories contain no Celeste/FMOD input, generated proprietary
source, app bundle, IPA, save, credential, or signing material.

## Documentation and ownership boundary

The root README now presents local Mac and private cloud compilation as two
clear choices. `docs/CLOUD_BUILDING.md` and the template README put the private
choice, exact two-file upload, click path, progress behavior, download, cleanup,
unsigned boundary, billing caveat, and privacy consequences before technical
detail.

The public project and template contain no Celeste/FMOD assets or credentials.
Private does not mean inputs stay on the user's computer: the guide explicitly
states that GitHub stores the user's private Release assets and a hosted runner
processes them. Users who reject that boundary are directed to the local Mac
builder. The generated IPA contains user-owned game content and is described as
private/personal, not redistributable.

## Known limitations

- Compilation can run without a Mac, but signing/provisioning/installation are
  separate and are not performed by GitHub.
- GitHub Actions time and private storage use the user's own allowance/billing
  settings.
- The template is pinned deliberately and does not automatically follow future
  `tvos-port` changes.
- Only exact supported Celeste profiles and FMOD iOS/tvOS 1.10.09 build 97915
  are accepted.
- GitHub-hosted runner availability, image contents, and build time can change;
  incompatible toolchain or disk state fails closed.
