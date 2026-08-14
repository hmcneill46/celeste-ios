# Build Celeste for Apple TV in the cloud

The private GitHub cloud builder compiles the same accepted Celeste tvOS port
without requiring a Mac for compilation. You provide files you already own in
your own **private** repository, run one manual workflow, and download a
verified unsigned IPA.

> [!IMPORTANT]
> Your generated builder repository must be **Private** before you upload
> Celeste or FMOD. Never upload those files to the public template repository.

The cloud build stops at an unsigned, signing-ready IPA. Provisioning, signing,
and installation are separate and may still require a compatible external
signing path.

## What you need

- A GitHub account.
- One ZIP containing an [exact supported Celeste 1.4.0.0 FNA
  input](CELESTE_INPUTS.md).
- The original official **FMOD Engine iOS/tvOS 1.10.09 build 97915 DMG**.

You must own Celeste and obtain FMOD through your own account. The template
does not provide either input and never asks for Steam, Epic, FMOD, or Apple
credentials.

## Quick start

### 1. Create your private builder repository

Open the public
[`hmcneill46/celeste-tvos-cloud-builder`](https://github.com/hmcneill46/celeste-tvos-cloud-builder)
template and click **Use this template → Create a new repository**.

On GitHub's creation page:

1. Choose your account as the owner.
2. Enter a repository name.
3. Under **Visibility**, choose **Private**.
4. Click **Create repository from template**.

Do not upload files to the public template itself. The Build workflow also
checks repository visibility through GitHub's API and refuses to run unless
both `private` and `visibility=private` are reported.

### 2. Prepare exactly two files

You need:

1. One `.zip` containing a supported Celeste installation.
2. The original FMOD iOS/tvOS 1.10.09 build 97915 `.dmg`.

If your supported Celeste download is already a ZIP, use it directly. If it is
an extracted folder or macOS app, compress that one folder/app to ZIP first:

- **Windows:** right-click the folder in File Explorer and choose **Compress to
  ZIP file** (or **Send to → Compressed (zipped) folder**).
- **macOS:** Control-click the folder or `.app` in Finder and choose
  **Compress**.
- **Linux:** use the file manager's **Compress** action and select ZIP; an
  optional terminal equivalent from the parent directory is
  `zip -r Celeste.zip Celeste-folder`.

Do not put an existing ZIP inside another ZIP. Upload the FMOD DMG unchanged;
Windows and Linux users do not need to open it. The [game-file acquisition
guide](../README.md#getting-a-clean-supported-celeste-copy) covers itch.io,
Steam, and Epic inputs.

### 3. Upload the inputs privately

In the private builder repository:

1. Open **Releases** and choose **Draft a new release**.
2. For **Choose a tag**, enter `celeste-tvos-inputs` and create that tag.
3. Use `celeste-tvos-inputs` as the release title.
4. Add exactly the Celeste ZIP and FMOD DMG.
5. Wait for both uploads to finish, then click **Publish release**.

Filenames do not matter. The workflow records each asset's size and SHA-256,
checks GitHub's digest when available, safely extracts the ZIP, then uses the
project's exact Celeste and FMOD validators. It rejects unknown, modified, or
wrong-version inputs.

### 4. Run the build

1. Open the repository's **Actions** tab.
2. Select **Build Celeste for Apple TV**.
3. Click **Run workflow**, then the green **Run workflow** button.

The optional bundle identifier has a safe default for an unsigned build; most
users should leave it unchanged. The workflow runs only when manually started,
serializes concurrent build/cleanup work, and has a two-hour timeout.

The builder reports eight timed phases. Long native or full-AOT operations
print a heartbeat every 60 seconds with elapsed time and free disk space.
GitHub groups each phase; seeing another heartbeat means the process is still
alive. Initial acceptance measured roughly 30–35 minutes without cache and
about 15 minutes with the verified safe cache, but current runner load and
GitHub images can change those times.

### 5. Download the unsigned IPA

After success, the workflow summary links to the private
`celeste-tvos-output` Release. Download:

`Celeste-tvOS-unsigned.ipa`

The Release also contains `Celeste-tvOS-build.txt`, recording the exact public
source commit, detected game profile, IPA size, and SHA-256. The product is
Release `tvos-arm64`, fully trimmed, full AOT, and has
`UseInterpreter=false`.

The IPA is **unsigned and cannot be installed as-is**. Continue with the
[unsigned IPA/signing guidance](../README.md#create-a-signing-ready-ipa).

### 6. Remove the private build files

Once the IPA is safely downloaded:

1. Return to **Actions**.
2. Select **Clean private build files**.
3. Click **Run workflow**.
4. Leave safe-cache deletion off for faster repeat builds, or enable it to
   remove that small open-source cache too.

The cleanup deletes only the `celeste-tvos-inputs` and
`celeste-tvos-output` Releases/tags. It can be run twice safely. The project
does not use normal GitHub Actions artifacts for the inputs or IPA.

## Privacy and ownership

The cloud route temporarily uploads your Celeste ZIP and FMOD DMG as private
Release assets in your private GitHub repository. A GitHub-hosted runner
downloads and processes them. Private does **not** mean those files stay on
your own computer. If you do not want to upload them to GitHub, use the
[local Mac builder](BUILDING.md).

The runner removes transferred, extracted, generated, app, and IPA work files
before the job ends. The separate cleanup workflow removes the two private
Releases from GitHub. Only two redistributable/open-source native output paths
may enter the optional cache; Celeste, FMOD, generated game source, app bundles,
and IPAs are excluded.

The built IPA contains user-supplied Celeste content and should remain private
and personal. This project supplies no game/FMOD files and grants no
redistribution rights.

## Cost and limits

Private-repository workflows use the repository owner's GitHub Actions
allowance and billing settings. Check GitHub's current [Actions billing
documentation](https://docs.github.com/en/billing/concepts/product-billing/github-actions)
before running a build; the project cannot know an account's remaining quota.

The workflow requires at least 25 GiB free on the runner before expensive work.
Each input asset and the output IPA must be below GitHub Releases' current
2 GiB per-file limit. It never splits inputs or products automatically.

## Troubleshooting

### My repository is public

Create a new **Private** repository from the template. The Build workflow
deliberately fails before checkout or input download in a public repository.

### The input Release is missing or has the wrong files

Publish a Release/tag named exactly `celeste-tvos-inputs` with exactly one
`.zip` and one `.dmg`. Remove drafts and extra assets.

### Celeste or FMOD is rejected

Use one exact profile from the [supported-input matrix](CELESTE_INPUTS.md) and
the original FMOD Engine iOS/tvOS 1.10.09 build 97915 DMG. Renaming or mixing
another version will not satisfy validation.

### The output Release already exists

Download it if needed, run **Clean private build files**, and start Build again.
The workflow refuses to overwrite an IPA that may not have been downloaded.

### The runner reports too little disk

Retry later or use the local Mac builder. The workflow does not remove system
Xcodes or weaken the 25 GiB safety threshold.

### GitHub says Actions minutes or billing are unavailable

Review the account/repository's Actions allowance and billing settings. The
workflow cannot bypass GitHub account limits and never starts automatically.

### The build appears slow or fails in native/full AOT

Expand the current GitHub log group. A heartbeat every 60 seconds during a
long operation is expected. On failure, the summary names the failed phase,
shows a bounded redacted diagnostic tail where available, and keeps the input
Release for a corrected retry.

### The IPA will not install

That is expected until it is signed. The workflow intentionally has no Apple
credentials, certificate, or provisioning profile. Follow the separate
[signing guidance](../README.md#create-a-signing-ready-ipa).

For more focused remedies, see [Troubleshooting](TROUBLESHOOTING.md).

## Security and reproducibility design

<details>
<summary>Technical details</summary>

- The public template contains only workflow/helper source. Users create a
  separate private repository from it.
- Build uses manual `workflow_dispatch`, least-privilege `contents: write`, one
  repository concurrency group, and a 120-minute timeout.
- Official `actions/checkout` and `actions/cache` revisions are pinned to full
  immutable commit SHAs.
- The workflow builds exact public source commit
  `90ebb023f3043222bc67e72922ae4d68223f009c`, verifies that checkout and its
  recursive submodules, and never follows a floating branch.
- The runner is the standard ARM64 `macos-26` image and must match Xcode 26.6,
  tvOS SDK 26.5, .NET SDK 10.0.302, and workload set 10.0.302.0.
- ZIP paths, duplicate/case-colliding entries, links, special files, expanded
  size, and extraction containment are checked before the existing exact
  validators run.
- The only cached paths are `artifacts/tvos-native/self-build` and
  `.build/tvos-host`. Their native logical hash is independently verified after
  restoration.
- The output is a private Release, not an Actions artifact. Runner cleanup is
  unconditional, while remote Releases are removed only by the user's explicit
  cleanup workflow.
- Maintainers update the pinned source only after testing a new accepted
  commit, running the template verifier, performing a representative private
  cloud build, and deliberately publishing the synchronized template.

</details>
