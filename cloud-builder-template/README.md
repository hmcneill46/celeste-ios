# Celeste for Apple TV — private cloud builder

> [!IMPORTANT]
> **Your generated builder repository must be Private before you upload Celeste
> or FMOD. Never upload copyrighted game files to this public template.**

This template compiles an unsigned Celeste tvOS IPA on GitHub's Mac runner.
You provide game files you own and the official FMOD SDK in your own private
repository; the workflow validates them, runs the existing Celeste Apple TV
self-builder, and gives you a verified unsigned IPA.

You do not need a Mac, Git, or a terminal for compilation. Signing,
provisioning, and installation are separate: the resulting IPA is **not
installable until it is signed**.

## What you need

- A GitHub account.
- One ZIP containing an [exact supported Celeste 1.4.0.0 FNA
  input](https://github.com/hmcneill46/celeste-ios/blob/98b3d14459e124301919bec3b054c5348d4ddfb2/docs/CELESTE_INPUTS.md).
- The original official **FMOD Engine iOS/tvOS 1.10.09 build 97915 DMG**.

You must own Celeste and obtain FMOD through your own FMOD account. This
template provides neither file and does not request any store, FMOD, or Apple
credentials.

## Quick start

### 1. Create your private builder

At the top of this public repository, click **Use this template**, then
**Create a new repository**.

On the creation page:

1. Choose your account as the owner.
2. Give the repository any sensible name.
3. Under **Visibility**, select **Private**.
4. Click **Create repository from template**.

Do not upload files to `hmcneill46/celeste-tvos-cloud-builder` itself. All
remaining steps happen in the private repository you just created.

### 2. Get the two input files

You need exactly:

- one `.zip` containing a supported Celeste installation;
- one `.dmg`: FMOD Engine iOS/tvOS 1.10.09 build 97915.

The main project explains how to obtain clean files you already own through
[itch.io, Steam, or Epic Games Store](https://github.com/hmcneill46/celeste-ios/blob/98b3d14459e124301919bec3b054c5348d4ddfb2/README.md#step-1--get-your-celeste-files)
and where to obtain the [official FMOD
SDK](https://github.com/hmcneill46/celeste-ios/blob/98b3d14459e124301919bec3b054c5348d4ddfb2/README.md#step-2--get-fmod).

If your Celeste download is already a supported ZIP, upload it directly. If
you have an extracted game folder or app, compress that one folder/app into one
ZIP first. Do not put an existing ZIP inside another ZIP.

<details>
<summary>How to make a ZIP from an extracted game</summary>

- **Windows:** In File Explorer, right-click the Celeste folder, choose
  **Compress to ZIP file** (or **Send to → Compressed (zipped) folder** on
  older Windows versions).
- **macOS:** In Finder, Control-click the Celeste folder or `.app`, then choose
  **Compress**.
- **Linux:** In your file manager, right-click the folder and choose its ZIP or
  **Compress** action. As an optional terminal fallback, run
  `zip -r Celeste.zip Celeste-folder` from the folder's parent.

Upload the official FMOD DMG as-is. Windows and Linux users do not need to open
or mount it.

</details>

### 3. Upload them privately

In **your private builder repository**:

1. Open **Releases** on the repository page.
2. Choose **Draft a new release**.
3. In **Choose a tag**, type `celeste-tvos-inputs`, then create that tag.
4. Use `celeste-tvos-inputs` as the release title too.
5. Drag in exactly the one Celeste ZIP and one FMOD DMG.
6. Wait for both uploads to finish, then click **Publish release**.

The filenames do not matter. The workflow detects and validates the contents.
Do not add extra Release assets.

### 4. Build

1. Open the **Actions** tab.
2. Select **Build Celeste for Apple TV** in the left sidebar.
3. Click **Run workflow**, then the green **Run workflow** button.

The default bundle identifier is usable for an unsigned build, so most users
should leave it unchanged. A first build was measured at about 30–35 minutes;
a repeat using the safe cache was about 15 minutes. These are examples, not
guarantees. Long native and full-AOT phases print a heartbeat every 60 seconds,
including elapsed time and free disk, so a heartbeat means the build is still
working.

Private-repository Actions use the repository owner's GitHub Actions allowance
and may be billed according to their plan and settings. See GitHub's current
[Actions billing documentation](https://docs.github.com/en/billing/concepts/product-billing/github-actions).

### 5. Download

When the workflow finishes, its summary links to the private
`celeste-tvos-output` Release. Open it and download:

`Celeste-tvOS-unsigned.ipa`

The Release also contains a tiny text file with the source commit, detected
Celeste profile, size, and SHA-256.

The IPA is unsigned. Follow the main project's [unsigned IPA and signing
guidance](https://github.com/hmcneill46/celeste-ios/blob/98b3d14459e124301919bec3b054c5348d4ddfb2/README.md#step-4--sign-and-install)
for the separate signing/install step. Keep the final signed bundle identifier
stable between replacement installs if you want tvOS to keep using the same
app-data domain.

### 6. Clean up

After downloading the IPA:

1. Return to **Actions**.
2. Select **Clean private build files**.
3. Click **Run workflow**.
4. Leave safe-cache deletion off for faster repeat builds, or turn it on if you
   want to remove that open-source cache too.

The cleanup removes only the `celeste-tvos-inputs` and
`celeste-tvos-output` Releases/tags. It is safe to run twice. This project does
not use normal Actions artifacts for the game, FMOD, or IPA.

## Privacy and ownership

The cloud route temporarily uploads your Celeste ZIP and FMOD DMG to GitHub as
**private Release assets in your private repository**. A GitHub-hosted runner
downloads and processes them. The runner explicitly removes downloaded,
extracted, generated, app, and IPA files before it finishes; the separate
cleanup workflow removes the two private Releases from GitHub.

Private does not mean the files stay on your own computer. If you do not want
to upload them to GitHub, use the [local Mac
builder](https://github.com/hmcneill46/celeste-ios/blob/98b3d14459e124301919bec3b054c5348d4ddfb2/docs/BUILDING.md)
instead.

The built IPA contains user-supplied Celeste content and should remain private
and personal. This project does not grant redistribution rights. It asks for
no Steam/Epic login, FMOD login, Apple ID, certificate, provisioning profile,
or signing secret.

## Troubleshooting

### My repository is public

The Build workflow deliberately stops before checkout or input download. Make
a new **Private** repository from the template and upload your inputs there.

### The input Release is missing or rejected

The tag must be exactly `celeste-tvos-inputs`, the Release must be published,
and it must contain exactly one `.zip` plus one `.dmg`. Delete extra assets or
replace the Release with the correct two files.

### Celeste or FMOD is unsupported

Use one exact supported Celeste profile and the original FMOD Engine iOS/tvOS
1.10.09 build 97915 DMG. Renaming another version does not bypass validation.

### The output Release already exists

Download it if wanted, run **Clean private build files**, then run Build again.
The workflow refuses to overwrite an IPA you may not have downloaded yet.

### The build looks stuck

Expand **Building Celeste for Apple TV**. The existing builder shows eight
timed groups and prints a heartbeat every 60 seconds during long operations.
No heartbeat for a short step is normal. A failure displays a bounded useful
tail and points to the failed phase.

### The runner has too little disk

The workflow requires at least 25 GiB free before expensive work. If GitHub's
current `macos-26` image provides less, retry later or use the local Mac
builder. The workflow does not delete system Xcodes to force a build through.

### I downloaded the IPA but it will not install

That is expected until it is signed. The cloud workflow intentionally never
receives Apple credentials and produces an unsigned signing-ready IPA only.

### I need more help

See the main project's [cloud-building guide](https://github.com/hmcneill46/celeste-ios/blob/98b3d14459e124301919bec3b054c5348d4ddfb2/docs/CLOUD_BUILDING.md)
and [troubleshooting guide](https://github.com/hmcneill46/celeste-ios/blob/98b3d14459e124301919bec3b054c5348d4ddfb2/docs/TROUBLESHOOTING.md).

## Technical and security design

<details>
<summary>Show implementation details</summary>

- The workflow triggers only by manual `workflow_dispatch`, has a 120-minute
  timeout, and serializes build/cleanup operations.
- Its first step queries the GitHub API and requires both `private=true` and
  `visibility=private` before any input operation.
- External Actions are official GitHub Actions pinned to full immutable commit
  SHAs. The token grants only repository contents access for private Releases;
  cleanup additionally gets narrowly scoped cache deletion permission.
- The workflow checks out public Celeste-port commit
  `98b3d14459e124301919bec3b054c5348d4ddfb2` with reachable history and exact
  recursive submodules. It never follows a floating branch.
- ZIP paths, links, special files, duplicates, expanded size, and extraction
  root are bounded before extraction. Existing Stage 17 and FMOD validators
  remain authoritative.
- The only cached paths are `artifacts/tvos-native/self-build` and
  `.build/tvos-host`. They contain redistributable/open-source native outputs,
  not Celeste, FMOD, generated game source, content, an app, or an IPA.
- The output is a private GitHub Release asset, not a normal Actions artifact.
- The runner uses Release `tvos-arm64`, full AOT, full trimming, and
  `UseInterpreter=false`, then reruns the package/product verifier chain.

Maintainers deliberately update the pinned source only after validating a new
accepted commit, running the template verifier, and completing a representative
private cloud build.

</details>
