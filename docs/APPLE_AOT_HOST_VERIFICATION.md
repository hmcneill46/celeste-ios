# Apple device AOT host verification

HOST-C starts from reviewed HOST-B tooling commit
`632a451da2f62928404a2408826747aa6fc55b20`. The game, content and semantic
authorities remain accepted build 46 (`be8546d4ae411cb491a0c3bbc4e5343ebf9c9651`).
A product built with these verification changes has a new tooling/source commit.
No game transformation, native compilation setting, dependency, version, hash or
normalization rule changes here.

## Compiler provenance

The existing `_AOTCompile` before/after and native-link receipts now distinguish
the macOS host architecture from the arm64 device target. Independent Python
host observation and the executing .NET host's SDK/RID/architecture must agree.
The SDK base directory must belong to that observed dotnet executable. The
receipt includes its hash, SDK 10.0.302, workload set 10.0.302.0, runtime 10.0.10,
Xcode 26.6 / 17F113 and device SDKs 26.5. No host-label command argument is used.

The captured compiler must be the exact installed
`Microsoft.NETCore.App.Runtime.AOT.osx-{x64|arm64}.Cross.{ios|tvos}-arm64`
pack's `10.0.10/tools/mono-aot-cross`. Its real Mach-O architecture must match
the observed host; its own version output must identify 10.0.10.0 and arm64.
The actual compiler item's LLVM directory must be that same pack's tools
directory. The existing compiler hash and new `llc`/`opt` hashes are verified
again after AOT, after native linking and during product verification.

The target platform is captured from the SDK's `TargetPlatformIdentifier` and
checked against the final Info.plist and actual native LC_BUILD_VERSION.
The SDK's `iOS`/`tvOS` spelling is converted to the canonical lowercase names;
the accepted device-platform choices remain exactly `ios` and `tvos`.
Both device OSes retain the reviewed `arm64-ios` Mono ABI triple. The target
family/minimum/SDK checks remain iOS [1,2]/15/26.5 and tvOS [3]/16/26.5.
The SDK supplies the actual item metadata and compiler path directly to its
AOT task. That task constructs the invocation from the captured `Arguments`
and `ProcessArguments`; HOST-C changes neither. See the pinned
[Apple SDK AOT task](https://github.com/dotnet/macios/blob/dotnet-10.0.1xx-xcode26.6-10301/msbuild/Xamarin.MacDev.Tasks/Tasks/AOTCompile.cs).

Existing source revision/worktree, fresh-output, full/static LLVM AOT,
input/item/output hashes, companion assembly and native-link checks remain.
Missing or altered host/compiler/LLVM evidence fails. Historical receipts
without this evidence are not silently promoted to the new verification lane.

## Signing semantics

`verify-apple-everest-aot-factory-product.py --signing development` remains the
strict default, including for existing signed callers. It requires successful
`codesign --verify --strict`. A failure never retries in another mode.
The canary builder explicitly propagates its selected `--signing` value.
For unsigned construction it also clears `CodesignEntitlements` and disables
default entitlement discovery. The pinned SDK otherwise compiles a project's
declared entitlement file even with `EnableCodeSigning=false`, placing an
`archived-expanded-entitlements.xcent` file in the app. This applies to the
existing empty tvOS entitlement declaration. See the pinned
[entitlement producer](https://github.com/dotnet/macios/blob/dotnet-10.0.1xx-xcode26.6-10301/msbuild/Xamarin.MacDev.Tasks/Tasks/CompileEntitlements.cs)
and its `_CompileEntitlements` target. Signed callers retain their entitlement
input. No packaged file is deleted to pass verification.

The explicit `unsigned` mode rejects bundle signature resources, provisioning
profiles, entitlement files and any Mach-O LC_CODE_SIGNATURE, including ad-hoc
signatures and nested native files. Malformed or unsupported native formats
fail closed. It additionally requires codesign's specific unsigned-object
result; arbitrary command failure does not count as unsigned evidence.
It neither signs nor removes signing material.

Both modes run the same actual linked factory/profile inspection, forbidden-call
scan, stripping/metadata comparison, AOT object and native code/data binding,
companion proof and content checks. The reported signing status names the mode
that actually passed. Packaging still records the chosen signing mode and exact
source and IPA identities. The historical aggregate K-L physical/signing gate
remains unchanged; it is not an unsigned HOST-C acceptance shortcut.

## Regression and qualification

Run the portable project-owned fixtures, including HOST-B regressions:

```sh
python3 -m unittest discover -s tests -p 'test_*.py' -v
bash -n scripts/build-apple-everest-canary.sh
git diff --check
```

The host verification fixtures cover both host variants and both device OSes,
the existing 15 platform controls, wrong packs/target/version/LLVM, tool and
receipt tampering, source changes, missing/stale evidence, strict-signature
failure without fallback, positive unsigned absence, nested/ad-hoc signatures,
provisioning material and malformed native headers. Both signing entry paths
must still call the linked-product proof. These synthetic fixtures are not a
physical signature or M1 app-build result.

Before final qualification, commit the reviewed tooling, confirm clean source
and pinned submodules, and freeze that revision throughout both fresh products.
Reuse byte-validated HOST-B native foundations through the existing staging
scripts. Generate each product's actual closure through the K-L wrapper and
require all four original logical identities and readiness gates. Build iOS
then tvOS with explicit unsigned mode and fresh isolated roots; preserve serial
MSBuild settings and omit allocator stress controls. Never use `--reuse-build`.
Run final linked/native/content checks, actual AOT negative controls and exact
IPA-to-app payload comparison, retaining real command exits and private logs.

Only both complete fresh product passes support HOST_GREEN_BUILD_ONLY.
Signing, installation and physical acceptance remain separate; the M1 is the
signing/install fallback. Feature integration remains the user's review/manual
fast-forward decision.
