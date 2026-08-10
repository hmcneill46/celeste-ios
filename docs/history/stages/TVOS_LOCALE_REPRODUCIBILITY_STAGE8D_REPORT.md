# Stage 8D: locale-independent FMOD/Theorafile symbol validation

Status: **PASS — duplicate-symbol validation, FMOD localization, clean public
packaging, and signed device runtime gates pass independently of the user's
collation locale.**

## Scope and baseline

- Branch: `tvos-port`
- Starting commit: `b77ea6983553c5b4e1716772280308bf146315c4`
- Candidate/final commit: the commit containing this report
- Intended commit subject: `fix: make tvOS symbol validation locale-independent`
- Publication target: `origin/tvos-port` on the user fork only

The worktree began clean at the exact required commit, tracked
`origin/tvos-port`, and retained the accepted submodule revisions. The patch is
limited to FMOD/Theorafile sorted-set validation, its focused diagnostics, a
repository regression check, and this report. Gameplay, controller behavior,
audio architecture, persistence, branding, inputs, entitlements, native
revisions, and packaging are unchanged.

## Real public failure and root cause

A real public fresh clone had already validated the exact Celeste and FMOD
inputs, built Stage 1, reproduced its accepted logical hash, and produced the
accepted Theorafile archive. It then rejected the reviewed FMOD/Theorafile
six-symbol policy.

The user's normal shell used `en_GB.UTF-8` language and collation with
`LC_ALL` unset. Both `nm` streams were sorted with `LC_ALL=C sort -u`, but the
consumer was plain `comm -12`. BSD `comm` therefore interpreted C-sorted input
under en_GB collation. In that mismatched configuration it reported only:

```text
_vorbis_lpc_from_data
_vorbis_lpc_predict
```

Applying `LC_ALL=C` to `comm` itself immediately returned the complete reviewed
intersection:

```text
_drft_backward
_drft_clear
_drft_forward
_drft_init
_vorbis_lpc_from_data
_vorbis_lpc_predict
```

This proves a locale mismatch in the comparison, not stale policy or changed
archives.

## Archive and policy evidence

Independent `nm` checks confirmed that the exact FMOD 1.10.09 build 97915
device archive contains all six symbols and the accepted Theorafile device
archive contains all six symbols.

Theorafile SHA-256 is:

```text
50099fd678241f969abe9e83f38644ab751d0618b46e61e67e3b34b865dbaf68
```

That is byte-for-byte identical to accepted rebuilds A through F and to the
new Stage 8D public-clone build. The fresh build also reproduced Stage 1
logical SHA-256:

```text
61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39
```

`native/fmod-tvos/localized-symbols.txt` and its exact six-symbol set were not
changed or weakened.

## Exact fix and locale audit

Two pre-existing locale-sensitive comparisons were corrected in
`scripts/prepare-fmod-tvos.sh`:

1. the original FMOD/Theorafile duplicate-symbol intersection; and
2. the post-`nmedit` intersection which must be empty.

Both now invoke `LC_ALL=C comm -12`, matching the `LC_ALL=C sort -u` inputs.
The new missing/unexpected diagnostic comparisons also use `LC_ALL=C` from
their introduction. The expected policy is explicitly checked as C-locale
sorted and unique.

The complete supported public graph was searched for `comm`, shell `sort`, and
sorted-text comparisons. There were no other pre-existing `comm` calls. Every
other deterministic shell sort was already pinned to `LC_ALL=C`; Python
`sorted` operations are code-point ordered and do not use the process locale.
No additional deterministic locale bug was found.

The public repository verifier now requires all four FMOD `comm` lines—the two
production intersections and two failure-diff comparisons—to contain
`LC_ALL=C comm`. This prevents the locale pin from silently regressing.

## Focused failure diagnostics

If the reviewed intersection ever differs legitimately, preparation now
reports bounded terminal counts for:

- expected symbols;
- actual duplicate symbols;
- missing symbols; and
- unexpected duplicate symbols.

The complete missing/unexpected lists are written beneath the ignored path:

```text
.build/fmod-tvos/diagnostics/duplicate-symbol-mismatch.txt
```

A post-localization collision similarly records its complete bounded list in:

```text
.build/fmod-tvos/diagnostics/post-localization-duplicate-symbols.txt
```

The terminal names these paths without dumping complete symbol tables. Stale
diagnostics are removed at the start of a new preparation. The top-level
builder continues to preserve its full ignored phase log as well.

An ignored test-only command shim removed exactly one symbol from the first
real intersection without modifying either archive or the policy. Preparation
failed as intended and reported expected `6`, actual `5`, missing `1`, and
unexpected `0`; the persistent diagnostic identified `_drft_backward` as
missing. A following normal successful preparation cleared the stale mismatch
file.

## Locale regression matrix

The same accepted FMOD and Theorafile archives were compared with `LC_ALL`
unset where applicable and with the locale applied to the surrounding process.
Every corrected comparison returned the exact ordered six-symbol policy:

| Environment | Intersection count | Exact policy match |
| --- | ---: | --- |
| `LC_ALL=C` | 6 | yes |
| `LANG=en_GB.UTF-8`, `LC_COLLATE=en_GB.UTF-8`, `LC_ALL` unset | 6 | yes |
| `LANG=de_DE.UTF-8`, `LC_COLLATE=de_DE.UTF-8`, `LC_ALL` unset | 6 | yes |

The uncorrected historical command was also rerun under en_GB and reproduced
the erroneous two-symbol result before the patch.

## FMOD localization

Fresh preparation under en_GB validated Celeste 1.4.0.0 and FMOD Engine
1.10.09 build 97915, found exactly the six expected duplicates, localized only
FMOD's ignored derived object with the existing `nmedit -R` policy, and left
zero duplicates with Theorafile afterward.

Required public exports remained present, including:

```text
_FMOD_System_Create
_FMOD_System_GetVersion
_FMOD_System_Init
_FMOD_System_Release
```

The normalized FMOD staging logical SHA-256 remained:

```text
b32fc89dbefdda69ab7bf20ea9ece37826dce51787a4f70446493e292b12603d
```

No FMOD archive, header, bank, or derived binary is tracked.

## Fresh-clone unsigned IPA

A disposable public clone began without native artifacts, managed output,
content staging, FMOD staging, artwork, apps, or IPAs. It contained only the
two focused candidate source edits and initialized the documented submodules.
The documented builder ran from that clone with en_GB language/collation and
`LC_ALL` unset.

It rebuilt all six Stage 1 components, reproduced the accepted Stage 1 and
Theorafile hashes, passed the formerly failing FMOD preparation, generated the
managed/content/artwork inputs below ignored roots, and published a full-AOT,
fully trimmed signing-ready IPA in approximately 34 minutes.

| Property | Accepted result |
| --- | --- |
| Display name | `Celeste` |
| Payload | `Payload/Celeste.app` |
| Architecture / platform | `arm64` / `TVOS` |
| Minimum OS | `16.0` |
| App bytes | `1,215,232,371` |
| IPA bytes | `895,099,539` |
| IPA SHA-256 | `aa0baf94b332103a94c1c1decc695026017ff3e15ac08b7d198ccdd057c2c167` |
| FMOD banks | exactly 7 |
| Signature / profile | absent |
| User Management | absent |

The independent Stage 8A IPA verifier and public repository verifier passed.

## Signed Apple TV regression

The same candidate clone reused its newly verified Stage 1 set, reran FMOD
preparation under en_GB, and used the existing ignored primary Personal Team
configuration. Automatic provisioning, signed Release full-AOT/full-trimmed
publish, sanitization/resigning, signed-app verification, replacement
installation, and launch passed in approximately nine minutes.

The signed verifier reported one arm64 TVOS executable, tvOS 16.0 minimum,
display name `Celeste`, seven banks, and `1,215,911,005` app bytes. Device logs
provided machine evidence for:

- native FMOD `0x00011009` with generated managed API `0x00011014`;
- FMOD low-level and Studio initialization success;
- FMOD-SDL registration;
- all seven banks ready (`922` events, `118` buses, `3` VCAs);
- a real world-map ambience event created and started;
- the first real Celeste draw at 1920x1080; and
- zero FMOD load/result, missing-entry-point, or unhandled-runtime errors.

This is a regression of the already accepted real-audio architecture, not a
new audio behavior claim.

## Isolation

Shell syntax, the public repository verifier, `git diff --check`, candidate
suffix/private-value scans, exact archive hashes, IPA/app verification, and
candidate Git status pass. In the disposable public clone, Git saw only the
two focused candidate source edits; all generated/proprietary/private outputs
remained ignored.

The candidate contains no Celeste binary/content/artwork, generated Celeste
source, FMOD SDK file/bank/archive, app/IPA, signing configuration, profile,
account/team/device identifier, private path, or raw device log. No README or
troubleshooting workaround is needed because the builder now behaves
deterministically for the user without locale configuration.
