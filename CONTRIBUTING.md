# Contributing

Thank you for helping improve the Apple TV port. Keep changes focused,
reproducible, and independent of proprietary inputs.

## Before opening an issue

- Use the `tvos-port` branch and read the [README](README.md) and
  [troubleshooting guide](docs/TROUBLESHOOTING.md).
- Run `./build-tvos.sh --help` and the relevant validation/build mode.
- Record the first meaningful error and tool versions.
- Redact local paths, accounts, bundle/signing values, Team IDs, device IDs,
  profile UUIDs, and certificate details.

Never attach Celeste files or generated source, FMOD SDK files/banks, generated
artwork, an app/IPA containing game content, saves/settings, Apple credentials
or two-factor codes, certificates, provisioning profiles, or private local
configuration.

## Pull requests

Keep the original Xamarin.iOS lane working and isolate tvOS changes to the
modern sibling path. Do not upgrade locked native, FNA, Celeste, or FMOD inputs
without a separately justified compatibility/reproducibility change.

Before submitting:

```bash
bash -n build-tvos.sh scripts/*.sh
python3 -m py_compile scripts/*.py
scripts/verify-repository-stage8b.py
git diff --check
git status --short
git submodule status --recursive
```

Describe which builder modes and hardware paths you actually tested. A build
that requires Celeste/FMOD/signing cannot be reproduced by public CI, so do not
claim physical or proprietary-input acceptance from source-only checks.

## Legal boundary

Contributions must contain only code, deterministic transforms, metadata,
tests, and documentation that the contributor can lawfully submit. Do not add
copyrighted game assets, decompiled/generated Celeste source, FMOD proprietary
material, or another user's signing/device data.
