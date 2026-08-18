#!/usr/bin/env python3
"""Verify the Stage 25B shared Apple Everest static-AOT foundation."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import re
import subprocess
import tempfile
import zipfile

BASE = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
EVEREST = "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00"
MONOMOD = "dfc30a1506d37fb88a2c2be004f525205f46a24c"
NLUA = "b3524288712743fb2394dcf615d14d0dac3276e2"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
IOS_RC = BASE
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
VANILLA_IOS = "2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357"


class Checks:
    def __init__(self) -> None: self.count = 0
    def require(self, condition: bool, message: str) -> None:
        if not condition: raise SystemExit("FAIL: " + message)
        self.count += 1


def read(root: pathlib.Path, relative: str) -> str:
    return (root / relative).read_text(errors="strict")


def git(root: pathlib.Path, *arguments: str) -> str:
    return subprocess.check_output(["git", "-C", str(root), *arguments], text=True).strip()


def package(root: pathlib.Path, ipa: pathlib.Path, platform: str, closure_hash: str, c: Checks) -> None:
    with tempfile.TemporaryDirectory() as temporary:
        with zipfile.ZipFile(ipa) as archive:
            c.require(archive.testzip() is None, f"{platform} IPA ZIP integrity")
            archive.extractall(temporary)
        apps = list((pathlib.Path(temporary) / "Payload").glob("*.app"))
        c.require(len(apps) == 1, f"{platform} conventional Payload app")
        app = apps[0]
        info = plistlib.loads((app / "Info.plist").read_bytes())
        c.require("everestcanary" in str(info.get("CFBundleIdentifier", "")).lower(), f"{platform} separate experimental identity")
        c.require("Everest Canary" in str(info.get("CFBundleDisplayName", info.get("CFBundleName", ""))) or
                  "Everest Canary" in str(info.get("CFBundleName", "")), f"{platform} experimental title")
        names = [path.name for path in app.rglob("*") if path.is_file()]
        forbidden_assemblies = ("MonoMod.RuntimeDetour", "NLua", "KeraLua", "DiscordGameSDK", "MiniInstaller", "NETCoreifier")
        c.require(not any(any(token in name for token in forbidden_assemblies) for name in names), f"{platform} forbidden runtime assemblies absent")
        c.require(not (app / "embedded.mobileprovision").exists() or (app / "embedded.mobileprovision").stat().st_size > 0,
                  f"{platform} provisioning state is structurally valid")
        executable = app / str(info.get("CFBundleExecutable", ""))
        c.require(executable.is_file(), f"{platform} executable exists")
        c.require(subprocess.check_output(["file", str(executable)], text=True).find("arm64") >= 0, f"{platform} arm64 executable")
        celeste = app / "Celeste.dll"
        c.require(celeste.is_file(), f"{platform} linked Celeste managed closure")
        managed = celeste.read_bytes()
        def managed_has(value: str) -> bool:
            return value.encode() in managed or value.encode("utf-16le") in managed
        c.require(managed_has("AppleEverestStaticRuntime"), f"{platform} static runtime linked")
        c.require(all(managed_has(value) for value in ("AppleEverestCanaryCore", "AppleEverestCanaryHookA",
                  "AppleEverestCanaryHookB")), f"{platform} canary modules linked")
        c.require(not managed_has("Assembly.LoadFrom") and not managed_has("MonoMod.RuntimeDetour"),
                  f"{platform} no loader/detour product token")
        c.require((app / "Celeste.aotdata.arm64").is_file(), f"{platform} Celeste AOT data")
        manifest = ipa.parent / "build-manifest.json"
        if manifest.is_file():
            build = json.loads(manifest.read_text())
            c.require(build["sharedClosureSha256"] == closure_hash, f"{platform} consumed shared closure")
            c.require(build["fullAOT"] and build["fullTrim"] and not build["useInterpreter"] and not build["jit"], f"{platform} AOT contract")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--closure", type=pathlib.Path)
    parser.add_argument("--ios-runtime", type=pathlib.Path)
    parser.add_argument("--tvos-runtime", type=pathlib.Path)
    parser.add_argument("--ios-ipa", type=pathlib.Path)
    parser.add_argument("--tvos-ipa", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    required = [
        "apple-everest/profiles/stable-1.6458.0.json", "tools/AppleEverestBuilder/AppleEverestBuilder.csproj",
        "tools/AppleEverestBuilder/packages.lock.json", "tools/AppleEverestBuilder/SafeModIngestor.cs",
        "tools/AppleEverestBuilder/EverestGraphResolver.cs", "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs",
        "tools/AppleEverestBuilder/ClosureGenerator.cs", "tools/AppleEverestBuilder/RuntimeClosureScanner.cs",
        "apple-everest/runtime/EverestStaticApi.cs",
        "apple-everest/runtime/OnDialogStaticDispatch.cs", "apple-everest/runtime/AppleEverestStaticRuntime.cs",
        "apple-everest/canaries/content/everest.yaml", "apple-everest/canaries/module-a/everest.yaml",
        "apple-everest/canaries/module-b/everest.yaml", "apple-everest/canaries/module-c/everest.yaml",
        "scripts/build-apple-everest-canary.sh",
        "scripts/bootstrap-apple-everest-host.sh", "scripts/test-apple-everest-il-freeze.sh",
        "scripts/test-apple-everest-desktop-hookgen.sh",
        "apple-everest/tests/desktop-hookgen/Runner/Program.cs",
        "docs/APPLE_EVEREST_STATIC_AOT.md",
    ]
    for relative in required: c.require((root / relative).is_file(), f"required source {relative}")

    profile = json.loads(read(root, required[0]))
    c.require(profile["schemaVersion"] == 1 and profile["appleTransformationVersion"] == 1, "profile schema")
    c.require(profile["everest"]["tag"] == "stable-1.6458.0" and profile["everest"]["sha256Commit"] == EVEREST, "exact Everest pin")
    c.require(profile["dependencies"]["monoModCommit"] == MONOMOD, "exact MonoMod pin")
    c.require(profile["dependencies"]["nLuaProvenanceOnlyCommit"] == NLUA, "NLua provenance pin")
    c.require(profile["dependencies"]["yamlDotNetVersion"] == "16.1.3", "YamlDotNet pin")
    c.require(profile["host"]["dotnetSdk"] == "8.0.424" and profile["host"]["monoModBuildSdk"] == "9.0.317", "bounded host SDKs")
    c.require(profile["celesteCanonicalClass"] == "celeste-1.4.0.0-a", "canonical class")
    c.require(set(profile["supportedMechanismClasses"]) == {"CONTENT_ONLY", "STATIC_MODULE", "NORMAL_EVENT", "ON_HOOK_SUPPORTED"}, "supported classes closed")
    for item in ("IL_HOOK_DEFERRED", "DIRECT_HOOK_DEFERRED", "NATIVE_UNSUPPORTED", "LUA_UNSUPPORTED", "DYNAMIC_CODE_UNSUPPORTED"):
        c.require(item in profile["unsupportedMechanismClasses"], f"{item} explicit")

    project = read(root, "tools/AppleEverestBuilder/AppleEverestBuilder.csproj")
    lock = read(root, "tools/AppleEverestBuilder/packages.lock.json")
    c.require('TargetFramework>net8.0' in project and 'YamlDotNet" Version="16.1.3' in project, "host project framework/YAML")
    c.require('Mono.Cecil" Version="0.11.6' in project and "YamlDotNet" in lock and "Mono.Cecil" in lock, "host packages locked")

    safe = read(root, "tools/AppleEverestBuilder/SafeModIngestor.cs")
    for token in ("MaxFiles", "MaxExpandedBytes", "MaxSingleFileBytes", "MaxPathDepth", "ExternalAttributes", "LinkTarget", "duplicate ZIP path", "Path.IsPathRooted"):
        c.require(token in safe, f"safe ingestion {token}")
    c.require("YamlDotNet" in safe and "DeserializerBuilder" in safe and "multi" not in safe.lower(), "production YAML library")

    graph = read(root, "tools/AppleEverestBuilder/EverestGraphResolver.cs")
    for token in ("Satisfies", "OptionalDependencies", "Conflicts", "dependency cycle", "duplicate Everest identity", "OrderBy"):
        c.require(token in graph, f"graph contract {token}")

    analyzer = read(root, "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs")
    for token in ("Assembly.Load", "AssemblyLoadContext", "DynamicMethod", "Reflection.Emit", "RuntimeDetour", "NativeDetour", "DllImport", "NLua", "KeraLua", "Process.Start", "FileSystemWatcher", "rejected before AOT"):
        c.require(token in analyzer, f"analyzer token {token}")
    c.require("On.Celeste.Dialog.Clean" in analyzer and "ON_HOOK_DEFERRED" in analyzer, "one exact supported On target")

    runtime_scanner = read(root, "tools/AppleEverestBuilder/RuntimeClosureScanner.cs")
    for token in ("MonoMod", "NLua", "KeraLua", "Assembly::Load", "AssemblyLoadContext::Load",
                  "NativeLibrary::Load", "Reflection.Emit", "Process::Start", "FileSystemWatcher::.ctor"):
        c.require(token in runtime_scanner, f"linked runtime scanner {token}")

    generator = read(root, "tools/AppleEverestBuilder/ClosureGenerator.cs")
    for token in ("GeneratedAppleEverestModuleRegistry", "GeneratedAppleEverestGameplayRegistry", "RegisterTrackerTypes", "TrackedEntityTypes", "StoredEntityTypes", "GeneratedAppleEverestAotRoots", "sharedClosureSha256", "contentMounts", "global::Celeste.Mod", "locked target must occur exactly once", "EVEREST_APPLE_STATIC_AOT"):
        c.require(token in generator, f"closure generator {token}")
    c.require(generator.count("PatchDialog(") == 2, "target method patched once by one transform function")
    c.require(generator.count("PatchTracker(") == 2, "Tracker initialization patched once by one transform function")
    c.require("typeof(global::" in generator, "generated type roots cannot collide with the Celeste game class")

    static_declaration = json.loads(read(root, "apple-everest/canaries/module-a/apple-static.json"))
    c.require(static_declaration["trackedEntityTypes"] == ["Celeste.Mod.AppleEverestCanaryBanner"],
              "canary entity is an explicit static gameplay-registry member")

    hook = read(root, "apple-everest/runtime/OnDialogStaticDispatch.cs")
    for token in ("delegate string orig_Clean", "delegate string hook_Clean", "orig_Clean chain", "RebuildActiveChain", "Handlers.Count - 1", "CurrentOwner"):
        c.require(token in hook, f"typed hook dispatcher {token}")
    c.require("DynamicInvoke" not in hook and "object[]" not in hook and "System.Reflection" not in hook, "no dynamic hook invocation")

    runtime = read(root, "apple-everest/runtime/AppleEverestStaticRuntime.cs")
    for token in ("GeneratedAppleEverestAotRoots.Root", "ModuleFactory", "SettingsFactory", "SaveDataFactory", "SessionFactory", "LoadContent", "Unload", "B-before", "A-before", "original", "A-after", "B-after", "content-precedence=PASS", "MaximumBytes = 64 * 1024"):
        c.require(token in runtime, f"runtime contract {token}")
    c.require("CELESTE_IOS_STORAGE_ROOT" in runtime and "AppleEverestStatic.log" in runtime, "bounded private log")
    c.require("SaveData.InitializeDebugMode(loadExisting: false)" in runtime,
              "content canary establishes an isolated pre-file-select save context")
    c.require("Input.MenuConfirm.ConsumePress()" in runtime and "Input.Jump.ConsumePress()" in runtime,
              "content canary consumes its launch input edge")
    c.require("TitleContainer.OpenStream" in runtime and "NSData.FromFile" in runtime and "NSFileManager" in runtime,
              "Apple-safe bundle and private-log IO")
    for forbidden_io in ("File.ReadAllLines", "File.ReadAllText", "File.AppendAllText", "File.WriteAllText"):
        c.require(forbidden_io not in runtime, f"AOT-unsafe canary IO absent: {forbidden_io}")

    all_device_source = "\n".join(read(root, str(path.relative_to(root))) for path in (root / "apple-everest/runtime").glob("*.cs"))
    for forbidden in ("Assembly.Load", "AssemblyLoadContext", "Reflection.Emit", "DynamicMethod", "RuntimeDetour", "NativeDetour", "Process.Start", "FileSystemWatcher", "NLua", "KeraLua", "DllImport"):
        c.require(forbidden not in all_device_source, f"device runtime excludes {forbidden}")

    module_core = read(root, "apple-everest/canaries/module-a/Code/CanaryModuleA.cs")
    module_a = read(root, "apple-everest/canaries/module-b/Code/CanaryModuleB.cs")
    module_b = read(root, "apple-everest/canaries/module-c/Code/CanaryModuleC.cs")
    c.require("class CanaryCoreModule : EverestModule" in module_core and "Everest.Events.Level.OnLoadLevel +=" in module_core, "real module/event source")
    c.require("On.Celeste." not in module_core, "ordinary-event module is hook-free")
    c.require("On.Celeste.Dialog.Clean +=" in module_a and "On.Celeste.Dialog.Clean +=" in module_b, "genuine independent HookGen source semantics")
    c.require("EverestModuleSettings" in module_core and "EverestModuleSaveData" in module_core and "EverestModuleSession" in module_core, "factory canary shapes")
    c.require((root / "apple-everest/canaries/module-b/Content/AppleEverest/Canary/precedence.txt").read_text().strip() == "AppleEverestCanaryA", "precedence A source")
    c.require((root / "apple-everest/canaries/module-c/Content/AppleEverest/Canary/precedence.txt").read_text().strip() == "AppleEverestCanaryB", "precedence B source")

    build = read(root, "scripts/build-apple-everest-canary.sh")
    for token in ("--platform ios|tvos|all", "--prepare-only", "CelesteIOSRuntimeRoot", "CelesteRuntimeRoot", "everestcanary", "UseInterpreter=false", "RunAOTCompilation=true", "TrimMode=full", "MtouchLink=Full", "PersistenceStorageNamespace=tests"):
        c.require(token in build, f"experimental builder {token}")
    c.require(build.count("scan-runtime --assembly") == 2, "both packaged Apple targets receive linked-runtime metadata inspection")
    c.require("build-ios.sh --mods" not in build and "build-tvos.sh --mods" not in build, "no vanilla mod CLI")
    c.require(".build/celeste-ios/current" in build and ".build/celeste-runtime/stage6-current/audio" in build, "accepted vanilla roots are derived not modified")

    bootstrap = read(root, "scripts/bootstrap-apple-everest-host.sh")
    c.require("8.0.424" in bootstrap and "9.0.317" in bootstrap and "INSTALL_SCRIPT_SHA256" in bootstrap, "host bootstrap exact")
    freeze = read(root, "scripts/test-apple-everest-il-freeze.sh") + read(root, "apple-everest/tests/il-freeze/Freezer/Program.cs")
    c.require("ILContext" in freeze and "ILCursor" in freeze and "MonoModUtilsPath" in freeze and "EXPECTED=15" in freeze, "real host IL freeze")
    c.require("RuntimeDetour dependency" in freeze and "MonoMod.Utils" in freeze, "frozen runtime scan")
    desktop = read(root, "scripts/test-apple-everest-desktop-hookgen.sh") + read(root, "apple-everest/tests/desktop-hookgen/Runner/Program.cs")
    c.require("MonoMod.RuntimeDetour.HookGen" in desktop and "TargetFrameworks=net8.0" in desktop, "pinned real HookGen reference")
    c.require("B-before,A-before,original,A-after,B-after" in desktop and "onlyA-trace" in desktop, "desktop reference trace")

    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC, "modern iOS recovery tag")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "tvOS RC1")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "tvOS RC2")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 absent")
    remote_rc3 = subprocess.check_output(["git", "-C", str(root), "rev-parse", "origin/release/v1.0.0-rc.3"], text=True).strip()
    c.require(remote_rc3 == RC3, "deferred RC3 preserved")
    c.require(git(root, "diff", "--name-only", BASE, "--", ".github") == "", "GitHub Actions untouched")

    locks = read(root, "managed/celeste-input-profiles.json") + read(root, "scripts/celeste-managed.py") + read(root, "scripts/verify-celeste-ios-stage24e1.py") + read(root, "native/ios-native-output.lock.json") + read(root, "build-tvos.sh")
    for value, label in ((CONTENT,"Content"),(RAW,"raw"),(PATCHED,"patched"),(STAGE6,"Stage6"),(VANILLA_IOS,"vanilla iOS"),(IOS_NATIVE,"iOS native"),(TVOS_NATIVE,"tvOS native")):
        c.require(value in locks, f"unchanged {label} lock")

    architecture = read(root, "docs/APPLE_EVEREST_STATIC_AOT.md")
    for token in ("stable-1.6458.0", "Mac host", "full AOT", "RuntimeDetour", "content-only", "iOS", "tvOS", "not general Everest"):
        c.require(token.lower() in architecture.lower(), f"architecture docs {token}")

    closure_hash = ""
    if args.closure:
        manifest = json.loads((args.closure / "compatibility-manifest.json").read_text())
        closure_hash = manifest["sharedClosureSha256"]
        c.require(manifest["schemaVersion"] == 1 and manifest["everestSha"] == EVEREST and manifest["monoModSha"] == MONOMOD, "closure pins")
        c.require(manifest["resolvedOrder"] == ["AppleEverestContentCanary", "AppleEverestCanaryCore", "AppleEverestCanaryHookA", "AppleEverestCanaryHookB"], "resolved lifecycle order")
        c.require(not manifest["runtimeDllLoading"] and not manifest["runtimeDetour"] and not manifest["interpreter"], "closed runtime flags")
        c.require(len(closure_hash) == 64, "shared closure hash")
        c.require(any(m["logicalPath"] == "AppleEverest/Canary/precedence.txt" and m["owner"] == "AppleEverestCanaryHookB" for m in manifest["contentMounts"]), "later content mount recorded")
        for runtime_root, label in ((args.ios_runtime, "iOS"), (args.tvos_runtime, "tvOS")):
            if runtime_root:
                generated = runtime_root / "managed/Celeste/Mod/AppleEverestStatic"
                c.require(generated.is_dir(), f"{label} closure applied")
                for source in (args.closure / "managed").glob("*.cs"):
                    c.require((generated / source.name).read_bytes() == source.read_bytes(), f"{label} exact shared source {source.name}")
                c.require((runtime_root / "content/Content/AppleEverest/Canary/precedence.txt").read_text().strip() == "AppleEverestCanaryB", f"{label} content precedence")
    if args.ios_ipa:
        c.require(bool(closure_hash), "closure required with iOS IPA")
        package(root, args.ios_ipa, "ios", closure_hash, c)
    if args.tvos_ipa:
        c.require(bool(closure_hash), "closure required with tvOS IPA")
        package(root, args.tvos_ipa, "tvos", closure_hash, c)

    result = {"schemaVersion": 1, "stage": "25B", "checks": c.count, "status": "PASS", "sharedClosureSha256": closure_hash or None}
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n")
    print(f"PASS: Stage 25B source/product contracts ({c.count} checks)")
    return 0


if __name__ == "__main__": raise SystemExit(main())
