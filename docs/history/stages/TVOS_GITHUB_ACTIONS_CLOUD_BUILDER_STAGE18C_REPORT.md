# Private GitHub Actions cloud builder — Stage 18C

Status: **production acceptance in progress**

This record covers the production private cloud-builder template. It changes no
Celeste runtime or product behavior; the cloud workflow invokes the already
accepted public self-builder and produces an unsigned IPA.

## Baseline and repositories

- Starting main-project commit:
  `90ebb023f3043222bc67e72922ae4d68223f009c`.
- Main-project feature branch: `feature/github-actions-cloud-builder`.
- Main feature commit: the final commit containing this report, recorded in the
  Stage 18C completion response and branch history.
- Immutable `v1.0.0-rc.1^{}` remained
  `ee52b0868df091746f134d95d4f020f94f23d4fb`.
- Public template repository:
  `https://github.com/hmcneill46/celeste-tvos-cloud-builder`.
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

The Stage 18C suite uses synthetic inputs to cover privacy interpretation,
asset classification, digest verification, bounded ZIP extraction, traversal,
links/special files/case collision, disk policy, source identity, output
overwrite protection, cleanup allow-list/idempotency, summary generation,
cache allow-list, artifact exclusion, and manual-only triggering. The static
verifier also checks workflow permissions, immutable Action pins, source/native
pins, documentation, export inventory, and Stage 18B integration.

## Live production acceptance

This section is completed from the fresh private-template acceptance runs after
the feature/template commits are published. It records the exact private status
gate, selected supported game profile, FMOD validation, fresh/cached durations,
cache size, IPA size/hash, verifier result, authenticated Release round trip,
runner cleanup, public-template rejection, and final explicit cleanup without
including credentials, input hashes, local paths, or proprietary contents.

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
