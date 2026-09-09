# Host-independent Apple native construction

The HOST-B tooling candidate starts from accepted build 46,
`be8546d4ae411cb491a0c3bbc4e5343ebf9c9651`. It changes native archive
construction and verification. Dependency revisions, native source patches,
compiler flags, architecture sets, deployment targets, game/closure authorities,
and accepted normalization and hashes stay unchanged. A build using this
candidate has a new tooling revision; it is not an unmodified build-46 checkout.

## Symbol policy

Both native verifiers pass `-arch arm64` explicitly for the existing logical
export fingerprint. Only stdout supplies symbols; stderr remains visible and
nonzero `nm` exits remain failures. There is no warning-line repair or filtering.

Required symbols are checked independently for every actual packaged slice.
iOS packages arm64 device and arm64 simulator. tvOS packages arm64 device and
arm64/x86_64 simulator for every component, plus the existing MoltenVK device
arm64e slice. XCFramework metadata must agree with actual archive architectures.
MoltenVK arm64e is checked against MoltenVK requirements; no arm64e slice is
required of other components. SDL may use only its same-architecture tvStubs
exports. Stub overlap is checked against real exports of the same architecture.
A union across architectures cannot satisfy a missing symbol.

Per-architecture inventories are separate verification JSON files. The accepted
normalized representation continues to contain the arm64 export fingerprint and
all archive members, including metadata and every architecture. The existing
terminal 32-hex source-path suffix rule is unchanged.

The iOS verifier also checks stub overlap and explicitly constructs unsigned
force-load probes, then verifies their architecture, platform, minimum OS and
lack of signature. This closes an unsigned-probe evidence gap without changing
native library compilation or the logical manifest. tvOS retains its existing
force-load, unsigned-probe and license checks.

## Deterministic index construction

`scripts/finalize-apple-archive.py` is a standard-library-only build producer.
It runs after the archive tool and before publishing the staged library consumed
by `xcodebuild -create-xcframework`. The HUD-augmented tvStubs archive is finalized
again after its last libtool/lipo operation. No global tool override, allocator
setting or new host dependency is needed.

The supported format is deliberately bounded: BSD `ar` with a single first
`__.SYMDEF` or `__.SYMDEF SORTED` index using little-endian 32-bit ranlib fields,
and little-endian 64-bit MH_OBJECT members for arm64, arm64e or x86_64. A
big-endian FAT_MAGIC wrapper containing these thin archives is supported.
GNU/thin archives, 64-bit index formats, other CPUs, malformed bounds, duplicate
architectures, overlapping slices and ambiguous string layouts fail closed.
This is not a general archive converter.

The field layouts come from the Apple SDK headers `ar.h`, `mach-o/ranlib.h`,
`mach-o/fat.h`, `mach-o/loader.h` and `mach-o/nlist.h`. For this supported Apple
64-bit producer layout, index string allocation is rounded to eight bytes.
The implementation validates the actual declared allocation against that rule;
it does not infer padding from byte values, component names or expected digests.

For each thin archive it validates member boundaries/names, load-command and
symbol-table bounds, ranlib table size, each referenced object offset, each
terminated string and the complete contiguous referenced string prefix. The
multiset of index `(name, member)` pairs must equal the objects' defined external
symbols, including common symbols. Valid empty assembly objects contribute no
symbols. This binding prevents a shortened or deleted symbol from converting
meaningful string bytes into apparent padding. Sorted indexes must retain
sorted entries.

Only the derived terminal alignment region, zero to seven bytes, is initialized
to zero. No header, member name/order, ranlib entry, referenced string, fat layout,
or Mach-O payload is reconstructed. The result is reparsed and all non-padding
bytes and complete object hashes are compared before publication. No expected
hash is used by the producer. Acceptance still hashes the actual index bytes.

Input and output must be distinct regular files below the explicitly supplied
native build root's work/stage/logs directories. The root must have the existing
source-state marker. Symlinks, hardlinked files, foreign owners, escapes and
missing parent directories are rejected. A same-directory temporary file is
flushed and atomically replaced into the owned destination. Originals and failed
inputs are never edited. Build roots are private, single-writer directories;
this is not a service for hostile concurrent filesystem writers.

Construction reports retain the input/output digest, initialized byte count,
derived ranges and pre-finalization member fingerprints. Existing verifiers
compare staged and packaged archive bytes, inspect all members, run symbol/link
checks and require the unchanged complete logical identity. Reports and native
binaries belong in ignored private output directories.

## Portable tests

Run from the feature checkout:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests -p 'test_native_*.py' -v
bash -n scripts/build-ios-native.sh scripts/build-tvos-native.sh
```

Fixtures are generated from project-owned bytes; no third-party native binary
is tracked. Tests cover stdout/stderr interleaving, nonzero exits, the actual
per-slice required-symbol and stub gates, all 256 padding-byte patterns,
idempotence, preservation, malformed lengths/offsets/references/strings,
unsupported formats, thin/fat paths and safe output handling. Mutated referenced
strings fail validation; a legitimate changed object remains changed and cannot
acquire the original object's fingerprint through finalization.

## M1 native-only regression

Review the feature diff first. Use a separate checkout of the exact delivered
feature SHA and initialize the pinned recursive submodules. Do not modify the
accepted checkout or manually fast-forward `tvos-port` before review/regression.
For a new checkout, use the delivered SHA explicitly:

```sh
git clone --no-checkout --single-branch --branch feature/apple-native-host-determinism \
  https://github.com/hmcneill46/celeste-ios.git celeste-native-hostb-m1
cd celeste-native-hostb-m1
git checkout --detach "$HOST_B_FEATURE_SHA"
git -c url.https://github.com/.insteadOf=git://github.com/ submodule update --init --recursive
git submodule status --recursive
git status --porcelain=v1 --untracked-files=all
```

Use the retained Xcode 26.6 / 17F113 and SDKs 26.5 through process-local
`DEVELOPER_DIR`; leave global selection untouched. No signing or device is needed.
The .NET 10.0.302 / workload 10.0.302.0 pins and existing host tools stay unchanged.

Validate the M1 reference inventory and both complete normalized manifests first.
The expected identities remain:

| Authority | SHA-256 |
| --- | --- |
| iOS native set | `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2` |
| iOS Theorafile | `f24cdd931a5ae337d681075d6f093afc7ca303c56652d79bc1510276772d8d21` |
| tvOS native set | `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc` |

Set `IOS_ACCEPTED_MANIFEST` and `TVOS_ACCEPTED_MANIFEST` to those validated private
reference files. Set `NATIVE_CACHE` to an owned public Git object cache. For each
of three independent runs, set `RUN` to a new unused name and execute serially:

```sh
ib=".build/ios-native/host-b-$RUN"
io="artifacts/ios-native/host-b-$RUN"
tb=".build/tvos-native/host-b-$RUN"
to="artifacts/tvos-native/host-b-$RUN"
test ! -e "$ib" && test ! -e "$io" && test ! -e "$tb" && test ! -e "$to"
git check-ignore "$ib" "$io" "$tb" "$to"
bash scripts/fetch-ios-native-deps.sh --build-dir "$ib" --cache-dir "$NATIVE_CACHE"
/usr/bin/time -lp bash scripts/build-ios-native.sh --build-dir "$ib" --output-dir "$io"
/usr/bin/time -lp bash scripts/verify-ios-native.sh --build-dir "$ib" --output-dir "$io" \
  --compare-manifest "$IOS_ACCEPTED_MANIFEST"
bash scripts/fetch-tvos-deps.sh --build-dir "$tb" --cache-dir "$NATIVE_CACHE"
/usr/bin/time -lp bash scripts/build-tvos-native.sh --build-dir "$tb" --output-dir "$to"
/usr/bin/time -lp bash scripts/verify-tvos-native.sh --build-dir "$tb" --output-dir "$to" \
  --compare-manifest "$TVOS_ACCEPTED_MANIFEST"
```

Use `set -euo pipefail` in the calling shell and keep all logs private. The three
build environments should be recorded separately: `MallocNanoZone=0` with
MallocScribble absent; `MallocNanoZone=0 MallocScribble=1`; and
`MallocNanoZone=1 MallocScribble=1`. Supply these only to each build's process,
not as system configuration. Clear other inherited experimental Malloc controls.
These stress controls prove construction is independent of allocator padding;
they are not required production settings or performance tuning.

Require all three complete accepted manifests, all per-slice checks and exact
ordered member fingerprints against the validated reference. Independently
compare every Mach-O payload and confirm staged/package equality and construction
preservation reports. Never reuse compiled output as a fresh run. Record code
revision, raw logs, source state, wall/user/system/RSS, disk and swap, plus any
failure. Reference inventory provenance does not replace fresh M1 execution.

After a successful fresh run, exercise the full identity gate with temporary
copies containing a deliberately changed Mach-O byte:

```sh
python3 tests/check_native_payload_rejection.py --platform ios --build-dir "$ib" --output-dir "$io"
python3 tests/check_native_payload_rejection.py --platform tvos --build-dir "$tb" --output-dir "$to"
```

The test requires a failure at the final complete identity comparison after
structural, symbol and link checks. Supplied accepted products are rehashed and
left intact; private temporary copies are removed.

Stop after reporting native regression. Any integration is the user's separate
manual fast-forward. App AOT, installation and physical qualification are later
stages: revalidate the original four game/closure identities and gates A–D then.
HOST-A's historical HOST_YELLOW result remains unchanged.
