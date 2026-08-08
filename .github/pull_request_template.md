## Summary

Describe the focused change and why it is needed.

## Verification

- [ ] `bash -n build-tvos.sh scripts/*.sh`
- [ ] `python3 -m py_compile scripts/*.py`
- [ ] `scripts/verify-repository-stage8b.py`
- [ ] `git diff --check`
- [ ] Relevant builder mode(s) tested and named below
- [ ] Existing iOS lane was not unintentionally changed

Tested modes/hardware:

## Isolation

- [ ] No Celeste files, generated/decompiled game source, content, or artwork
- [ ] No FMOD SDK files, archives, headers, or banks
- [ ] No app/IPA or private logs
- [ ] No credentials, certificates, profiles, Team/device/account IDs, bundle IDs, or local paths
- [ ] No unrelated native dependency/submodule update

## Notes

List remaining risks and anything not actually tested. Do not present
source-only or simulator checks as physical Apple TV acceptance.
