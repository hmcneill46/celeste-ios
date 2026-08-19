#!/usr/bin/env python3
"""Verify the Stage 25D-C signature-driven Apple static managed-detour backend."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess
import tempfile
import zipfile

BASE = "4a4f376a4a34ba880bcd508ac04fc7bbdd5399f0"
EVEREST = "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00"
MONOMOD = "dfc30a1506d37fb88a2c2be004f525205f46a24c"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
VANILLA_IOS = "2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357"
CLOSURE = "4e7abe3f76fa18c55213241f6fb11e5670610eb440cc923b870fec63cbb44e6f"
FEATHER_ZIP = "a8f1104710aac5807be3b24cd8c3870d94aa117d1146b30a4de0983a10f3e40e"
FEATHER_DLL = "a4ff1c89733cb450af1fc7199ed777fd23f849fe4f6a3e0a42618e1177144509"
FEATHER_FROZEN = "f88aecc4d5cadeb9b46c4a34bf035d89a8c09593cb726c2826a2b2e4eeda5166"
LAG_ZIP = "32dac84d2c5b60458a701cb61e8601bc89d937e25bc7fdcf52c80d9128e99d10"
LAG_DLL = "6ed3515105056ff2f4be84ef5542ce0a7b90dca9f5bd305703c506f462ffcdfa"
LAG_FROZEN = "bbe3c964e9dd1664ed79cba5f1b48f62b358bbd472615df912377e396e99e452"
PARTICLE_ZIP = "f9cf8874acbfaff87af22098caea36429c97941d0b13e1114d8749be61afbe19"
PARTICLE_FROZEN = "3c8f459ec016c233f35a9ae723cd03035b745855867747dde53d5c26ed3157f0"
CONTENT_MOD_ZIP = "37eaa16b6b2d458a8ce27e08ea315b4cc3c2938d6b3c2292c61203c641ad7c95"


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: bool, message: str) -> None:
        if not condition:
            raise SystemExit("FAIL: " + message)
        self.count += 1


def read(root: pathlib.Path, relative: str) -> str:
    return (root / relative).read_text(errors="strict")


def git(root: pathlib.Path, *arguments: str) -> str:
    return subprocess.check_output(["git", "-C", str(root), *arguments], text=True).strip()


def sha(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def verify_package(ipa: pathlib.Path, platform: str, c: Checks) -> None:
    with tempfile.TemporaryDirectory() as temporary:
        with zipfile.ZipFile(ipa) as archive:
            c.require(archive.testzip() is None, f"{platform} ZIP integrity")
            archive.extractall(temporary)
        apps = list((pathlib.Path(temporary) / "Payload").glob("*.app"))
        c.require(len(apps) == 1, f"{platform} one app")
        app = apps[0]
        info = plistlib.loads((app / "Info.plist").read_bytes())
        c.require("everestcanary" in str(info.get("CFBundleIdentifier", "")).lower(), f"{platform} isolated identity")
        executable = app / str(info.get("CFBundleExecutable", ""))
        c.require(executable.is_file() and "arm64" in subprocess.check_output(["file", str(executable)], text=True), f"{platform} arm64")
        for assembly in ("FeatherMaddy", "LagPauser", "ParticlePaletteHelper"):
            c.require((app / f"{assembly}.dll").is_file(), f"{platform} {assembly} linked")
            c.require((app / f"{assembly}.aotdata.arm64").is_file(), f"{platform} {assembly} AOT data")
        c.require((app / "Celeste.aotdata.arm64").is_file(), f"{platform} Celeste AOT data")
        names = [path.name.lower() for path in app.rglob("*") if path.is_file()]
        forbidden = ("mmhook", "monomod.runtimedetour.dll", "nativedetour", "nlua", "keralua")
        c.require(not any(any(token in name for token in forbidden) for name in names), f"{platform} no transformation/runtime-patching payload")
        manifest = json.loads((ipa.parent / "build-manifest.json").read_text())
        c.require(manifest["sharedClosureSha256"] == CLOSURE, f"{platform} exact closure")
        c.require(manifest["fullAOT"] and manifest["fullTrim"] and not manifest["useInterpreter"] and not manifest["jit"], f"{platform} full-AOT contract")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--closure", type=pathlib.Path)
    parser.add_argument("--fixtures", type=pathlib.Path)
    parser.add_argument("--ios-ipa", type=pathlib.Path)
    parser.add_argument("--tvos-ipa", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    required = [
        "apple-everest/managed-detour-compatibility-stage25d.json",
        "apple-everest/managed-detour-targets-v1.json",
        "apple-everest/apple-api-surface-v1.json",
        "apple-everest/runtime/AppleEverestHookList.cs",
        "apple-everest/runtime/AppleEverestLogPolicy.cs",
        "apple-everest/runtime/MonoModRuntimeDetourStaticFacade.cs",
        "tools/AppleEverestBuilder/ManagedDetourCatalog.cs",
        "tools/AppleEverestBuilder/ManagedDetourGenerator.cs",
        "tools/AppleEverestBuilder/AssemblyFreezer.cs",
        "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs",
        "scripts/fetch-apple-everest-stage25d-fixtures.sh",
        "scripts/build-apple-everest-real-mods.sh",
        "docs/APPLE_EVEREST_STATIC_AOT.md",
        "docs/APPLE_EVEREST_COMPATIBILITY.md",
        "docs/history/stages/APPLE_EVEREST_MANAGED_DETOURS_STAGE25D_REPORT.md",
    ]
    for relative in required:
        c.require((root / relative).is_file(), f"required {relative}")

    targets = json.loads(read(root, "apple-everest/managed-detour-targets-v1.json"))
    c.require(targets["schemaVersion"] == 1 and len(targets["targets"]) == 18, "18-target signature catalog")
    ids = [item["id"] for item in targets["targets"]]
    c.require(len(ids) == len(set(ids)), "unique target IDs")
    c.require("celeste-player-die" in ids and "monocle-engine-update" in ids, "direct and HookGen targets catalogued")
    c.require(sum(len(item.get("directAliases", [])) for item in targets["targets"]) == 2, "bounded direct aliases")
    for item in targets["targets"]:
        c.require(bool(item["sourceFile"] and item["sourceDeclaration"] and item["returnType"]), f"complete signature {item['id']}")

    registry = json.loads(read(root, "apple-everest/managed-detour-compatibility-stage25d.json"))
    c.require(registry["schemaVersion"] == 1 and registry["auditedCandidates"] == 14, "Stage 25D candidate registry")
    c.require(registry["everestProfile"] == "apple-everest-stable-1.6458.0-v1" and registry["transformerVersion"] == "apple-everest-static-v3", "pinned profile/transformer")
    c.require(registry["selectedClosure"] == ["IAccidentallyFourCassetteBlocks@1.0.0", "ParticlePaletteHelper@1.0.0", "FeatherMaddy@1.3", "LagPauser@1.3.0"], "exact selected closure")
    by_name = {item["name"]: item for item in registry["fixtures"]}
    c.require(len(by_name) == 14, "fourteen audited candidates")
    c.require(by_name["FeatherMaddy"]["zipSha256"] == FEATHER_ZIP and by_name["FeatherMaddy"]["dllSha256"] == FEATHER_DLL and by_name["FeatherMaddy"]["frozenDllSha256"] == FEATHER_FROZEN, "Feather Maddy pins")
    c.require(by_name["LagPauser"]["zipSha256"] == LAG_ZIP and by_name["LagPauser"]["dllSha256"] == LAG_DLL and by_name["LagPauser"]["frozenDllSha256"] == LAG_FROZEN, "Lag Pauser pins")
    c.require("direct Hook" in " ".join(by_name["LagPauser"]["mechanisms"]) and "BINARY_COMPATIBLE" in by_name["LagPauser"]["result"], "real direct-Hook selection")
    c.require(all(item.get("license") and item.get("mechanisms") and item.get("result") for item in registry["fixtures"]), "license/mechanism/result findings")

    models = read(root, "tools/AppleEverestBuilder/Models.cs")
    for token in ("apple-everest-static-v3", "DIRECT_HOOK_SUPPORTED", "DYNAMIC_TARGET_DEFERRED", "DYNAMIC_DETOUR_DEFERRED", "DETOUR_CONFIG_DEFERRED", "DirectManagedHookPlan"):
        c.require(token in models, f"model {token}")
    catalog = read(root, "tools/AppleEverestBuilder/ManagedDetourCatalog.cs")
    c.require("managed-detour-targets-v1.json" in catalog and "duplicate managed-detour target" in catalog, "catalog is embedded and fail-closed")
    generator = read(root, "tools/AppleEverestBuilder/ManagedDetourGenerator.cs")
    for token in ("GeneratedAppleEverestManagedDetourRegistry", "GeneratedAppleEverestDirectHookRegistry", "CreateByPlan", "RegisterDirect_", "RewriteTargets", "ValidateDirectShape"):
        c.require(token in generator, f"signature generator {token}")
    for token in ("DynamicInvoke", "object[]", "Reflection.Emit", "DynamicMethod"):
        c.require(token not in generator, f"generator excludes {token}")

    analyzer = read(root, "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs")
    for token in ("ResolveDirectHookPlan", "direct-managed-hook", "DYNAMIC_TARGET_DEFERRED", "DYNAMIC_DETOUR_DEFERRED", "DETOUR_CONFIG_DEFERRED", "unsupported direct Hook member"):
        c.require(token in analyzer, f"analyzer {token}")
    c.require("GetMethod" in analyzer and "BindingFlags" in analyzer and "MonoMod.RuntimeDetour.Hook" in analyzer, "host resolves bounded reflection pattern")
    freezer = read(root, "tools/AppleEverestBuilder/AssemblyFreezer.cs")
    for token in ("IReadOnlyList<DirectManagedHookPlan> directHooks", "foreach (DirectManagedHookPlan plan in directHooks)",
                  "PlanId", "OpCodes.Ldstr", "MonoMod.RuntimeDetour.Hook", "MethodReference staticConstructor = new(\".ctor\""):
        c.require(token in freezer, f"freezer {token}")
    c.require("DynamicMethod" not in freezer and "Assembly.Load" not in freezer, "freezer has no dynamic execution")

    hook_list = read(root, "apple-everest/runtime/AppleEverestHookList.cs")
    for token in ("AppleEverestManagedHook<T>", "Apply()", "Undo()", "Dispose()", "Priority", "Before", "After", "cyclic Apple static managed-detour ordering constraints"):
        c.require(token in hook_list, f"shared chain semantics {token}")
    facade = read(root, "apple-everest/runtime/MonoModRuntimeDetourStaticFacade.cs")
    c.require("public Hook(string staticPlanId)" in facade and "CreateByPlan" in facade, "device facade is plan-only")
    c.require("using System.Reflection" not in facade and "global::System.Reflection" not in facade,
              "device facade has no target reflection")
    c.require("public void Apply()" in facade and "public void Undo()" in facade and "public void Dispose()" in facade, "direct-hook lifetime surface")
    runtime = "\n".join(path.read_text() for path in (root / "apple-everest/runtime").glob("*.cs"))
    for token in ("DynamicInvoke", "Reflection.Emit", "DynamicMethod", "Assembly.Load", "AssemblyLoadContext", "NativeDetour", "ILHook", "Process.Start", "FileSystemWatcher", "NLua", "KeraLua"):
        c.require(token not in runtime, f"device runtime excludes {token}")
    for removed in ("OnDialogStaticDispatch.cs", "OnParticleStaticDispatch.cs", "OnTrailStaticDispatch.cs"):
        c.require(not (root / "apple-everest/runtime" / removed).exists(), f"bespoke dispatcher removed: {removed}")

    closure_generator = read(root, "tools/AppleEverestBuilder/ClosureGenerator.cs")
    for token in ("ManagedDetourGenerator.DispatcherSource", "ManagedDetourGenerator.DirectRegistrySource", "ManagedDetourGenerator.RewriteTargets", "directManagedHookCount", "static-data-only"):
        c.require(token in closure_generator, f"closure generator {token}")
    c.require("FeatherMaddy" not in closure_generator and "LagPauser" not in closure_generator and "ParticlePaletteHelper" not in closure_generator, "no selected-mod special cases")
    api_surface = json.loads(read(root, "apple-everest/apple-api-surface-v1.json"))
    c.require(api_surface["schemaVersion"] == 1 and len(api_surface["members"]) == 3,
              "exact three-member reviewed Apple API surface")
    c.require({member["id"] for member in api_surface["members"]} == {
        "celeste-level-unpause-timer", "celeste-level-start-pause-effects",
        "celeste-level-end-pause-effects"}, "exact Lag Pauser API compatibility members")
    surface_source = read(root, "tools/AppleEverestBuilder/AppleApiSurface.cs")
    c.require("must occur exactly once" in surface_source and "AppleApiSurface.json" in surface_source,
              "API surface is exact and fail-closed")
    scanner = read(root, "tools/AppleEverestBuilder/RuntimeClosureScanner.cs")
    c.require("inaccessible-method:" in scanner and "inaccessible-field:" in scanner,
              "external API scanner rejects inaccessible members")
    runtime_api = read(root, "apple-everest/runtime/AppleEverestStaticRuntime.cs")
    c.require("GeneratedAppleEverestManagedDetourRegistry.RemoveOwner" in runtime_api, "owner cleanup spans all generated targets")
    c.require("AppleEverestDirectHooks" not in runtime_api, "no separate direct-hook lifecycle lane")
    c.require("RecordDirectHookInvocation" in runtime_api and "AppleEverestLogPolicy" in runtime,
              "bounded invocation evidence and Everest log-level policy")

    fetch = read(root, "scripts/fetch-apple-everest-stage25d-fixtures.sh")
    for value in (FEATHER_ZIP, LAG_ZIP, "https://gamebanana.com/dl/1066794", "https://gamebanana.com/dl/1458113"):
        c.require(value in fetch, f"fixture acquisition pin {value}")
    build = read(root, "scripts/build-apple-everest-canary.sh")
    c.require("UseInterpreter=false" in build and "RunAOTCompilation=true" in build and "TrimMode=full" in build, "Apple full-AOT build")
    c.require("verify-referenced-api" in build and "verify-aot-object" in build and ".dll.llvm.o" in build, "external assemblies get linked/AOT proof")
    c.require("--mods" not in read(root, "build-ios.sh") and "--mods" not in read(root, "build-tvos.sh"), "public vanilla builders remain mod-free")

    docs = read(root, "docs/APPLE_EVEREST_STATIC_AOT.md") + read(root, "docs/APPLE_EVEREST_COMPATIBILITY.md")
    for token in ("signature-driven", "HookGen", "direct Hook", "Feather Maddy", "Lag Pauser", "not general Everest support"):
        c.require(token.lower() in docs.lower(), f"documentation {token}")
    report = read(root, "docs/history/stages/APPLE_EVEREST_MANAGED_DETOURS_STAGE25D_REPORT.md")
    c.require("PASS — GREEN" in report and BASE in report and CLOSURE in report, "Stage 25D report status/baseline/closure")
    c.require(FEATHER_DLL in report and LAG_DLL in report and LAG_FROZEN in report, "report binary provenance")
    c.require("APPLE_EVEREST_MANAGED_DETOURS_STAGE25D_REPORT.md" in read(root, "docs/history/README.md"), "history index")

    lock_text = "\n".join([
        read(root, "managed/celeste-input-profiles.json"), read(root, "scripts/celeste-managed.py"),
        read(root, "scripts/verify-celeste-ios-stage24e1.py"), read(root, "native/ios-native-output.lock.json"),
        read(root, "build-tvos.sh"),
    ])
    for value, label in ((CONTENT, "content"), (RAW, "raw"), (PATCHED, "patched"), (STAGE6, "stage6"),
                         (VANILLA_IOS, "vanilla iOS"), (IOS_NATIVE, "iOS native"), (TVOS_NATIVE, "tvOS native")):
        c.require(value in lock_text, f"accepted {label} lock")
    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC, "iOS recovery tag")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "tvOS RC1")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "tvOS RC2")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 tag absent")
    c.require(git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3, "deferred RC3 preserved")
    c.require(git(root, "rev-parse", "tvos-port") == BASE and git(root, "rev-parse", "origin/tvos-port") == BASE, "integration branch preserved")
    c.require(git(root, "diff", "--name-only", BASE, "--", ".github") == "", "GitHub Actions untouched")

    closure = args.closure or root / ".build/apple-everest/production-canary/shared-closure"
    if closure.is_dir():
        manifest = json.loads((closure / "compatibility-manifest.json").read_text())
        c.require(manifest["sharedClosureSha256"] == CLOSURE, "exact reproducible closure")
        c.require(manifest["transformerVersion"] == "apple-everest-static-v3" and manifest["managedDetourTargetCount"] == 18 and manifest["directManagedHookCount"] == 1, "v3 managed-detour closure")
        c.require(manifest["runtimeDllLoading"] is False and manifest["runtimeDetour"] == "static-data-only" and manifest["interpreter"] is False, "closed runtime policy")
        c.require(manifest["resolvedOrder"] == ["FeatherMaddy", "IAccidentallyFourCassetteBlocks", "LagPauser", "ParticlePaletteHelper"], "deterministic mod order")
        classifications = {item["name"]: item["classification"] for item in manifest["selectedMods"]}
        c.require(classifications == {"FeatherMaddy": "ON_HOOK_SUPPORTED", "IAccidentallyFourCassetteBlocks": "CONTENT_ONLY", "LagPauser": "MIXED_MANAGED_DETOURS_SUPPORTED", "ParticlePaletteHelper": "ON_HOOK_SUPPORTED"}, "exact classifications")
        frozen = {item["assemblyName"]: item for item in manifest["frozenAssemblies"]}
        c.require(frozen["FeatherMaddy"]["frozenSha256"] == FEATHER_FROZEN and frozen["LagPauser"]["frozenSha256"] == LAG_FROZEN and frozen["ParticlePaletteHelper"]["frozenSha256"] == PARTICLE_FROZEN, "frozen assembly identities")
        for assembly, expected in (("FeatherMaddy", FEATHER_FROZEN), ("LagPauser", LAG_FROZEN), ("ParticlePaletteHelper", PARTICLE_FROZEN)):
            c.require(sha(closure / "assemblies" / f"{assembly}.dll") == expected, f"frozen bytes {assembly}")
        roots = read(closure, "managed/AppleEverestExternalAssemblyRoots.props")
        c.require(all(f'<TrimmerRootAssembly Include="{name}" />' in roots for name in ("FeatherMaddy", "LagPauser", "ParticlePaletteHelper")), "all external assemblies rooted")
        direct = read(closure, "managed/GeneratedAppleEverestDirectHooks.cs")
        c.require("LagPauser:celeste-player-die" in direct and "OnPlayerDie" in direct and "CreateByPlan" in direct, "real direct plan generated")
        c.require("MethodBase" not in direct and "System.Reflection" not in direct, "generated direct registry reflection-free")

    if args.fixtures:
        expected = {
            "FeatherMaddy-v1.3.zip": FEATHER_ZIP,
            "LagPauser-v1.3.0.zip": LAG_ZIP,
            "ParticlePaletteHelper-v1.0.0.zip": PARTICLE_ZIP,
            "IAccidentallyFourCassetteBlocks-v1.0.0.zip": CONTENT_MOD_ZIP,
        }
        for name, expected_sha in expected.items():
            c.require(sha(args.fixtures / name) == expected_sha, f"fixture SHA {name}")
    if args.ios_ipa:
        verify_package(args.ios_ipa.resolve(), "iOS", c)
    if args.tvos_ipa:
        verify_package(args.tvos_ipa.resolve(), "tvOS", c)

    output = {"schemaVersion": 1, "stage": "25D-C", "status": "PASS", "checks": c.count,
              "sharedClosureSha256": CLOSURE, "managedDetourTargets": 18, "directManagedHooks": 1}
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(output, indent=2, sort_keys=True) + "\n")
    print(f"PASS: Stage 25D-C verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
