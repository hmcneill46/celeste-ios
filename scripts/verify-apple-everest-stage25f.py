#!/usr/bin/env python3
"""Verify Stage 25F-A bounded Apple Everest module SaveData and Session durability."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess
import tempfile
import zipfile


BASE = "c4de9040cf83a2132f993b62d3416fdc8b8c0a32"
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
DEATH_ZIP = "94ad7d14fec6fb500f811ef09f46f008f444b45aafcdd8c2e8b86ce5d3ee6fc7"
DEATH_DLL = "620e5b639b057a46890e7f0ed8a828fadb422a7b4adcd6b1c53bbb6568acdff8"
DEATH_FROZEN = "e78087ee336ae8007e0df65616d7a7f1d9493d24163351eb24d2550e96d2dc47"
DEATH_SOURCE = "24c9b8214c69dd10ce3a3efc1d08b4bb042d1965"
CLOSURE = "b85472c06b68757bc02de8a33171890df15015924178024c72da48556d8a3a01"
DURABILITY_CLOSURE = "62868c5fa8a2c2167e45b9eb56b3069624377bb1cf8c800412f862a93bad7048"
MANAGED = "e0ce8566a19bbdf8015fe3b4788cff7224471e234668f8d3446f5427537df410"
CLOSURE_CONTENT = "3532ed2c76718e12340c9e119b69c2ed17e5066371f9b2bef3c29c2282260edf"


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


def package(ipa: pathlib.Path, platform: str, c: Checks) -> dict:
    with tempfile.TemporaryDirectory() as temporary:
        with zipfile.ZipFile(ipa) as archive:
            c.require(archive.testzip() is None, f"{platform} ZIP integrity")
            archive.extractall(temporary)
        apps = list((pathlib.Path(temporary) / "Payload").glob("*.app"))
        c.require(len(apps) == 1, f"{platform} contains one app")
        app = apps[0]
        info = plistlib.loads((app / "Info.plist").read_bytes())
        c.require("everestcanary" in str(info.get("CFBundleIdentifier", "")).lower(),
                  f"{platform} isolated experimental identity")
        executable = app / str(info.get("CFBundleExecutable", ""))
        c.require(executable.is_file() and "arm64" in subprocess.check_output(["file", str(executable)], text=True),
                  f"{platform} arm64 executable")
        for assembly in ("CpopHelper", "DeathMarkers", "FeatherMaddy", "LagPauser", "ParticlePaletteHelper"):
            c.require((app / f"{assembly}.dll").is_file(), f"{platform} {assembly} linked")
            c.require((app / f"{assembly}.aotdata.arm64").is_file(), f"{platform} {assembly} AOT data")
        c.require((app / "Celeste.aotdata.arm64").is_file(), f"{platform} Celeste AOT data")
        names = [path.name.lower() for path in app.rglob("*") if path.is_file()]
        forbidden = ("mmhook", "monomod.runtimedetour.dll", "nativedetour", "ilhook", "nlua", "keralua")
        c.require(not any(any(token in name for token in forbidden) for name in names),
                  f"{platform} excludes desktop patching/Lua payloads")
        sibling_manifest = ipa.parent / "build-manifest.json"
        c.require(sibling_manifest.is_file(), f"{platform} build manifest")
        manifest = json.loads(sibling_manifest.read_text())
        c.require(manifest["sharedClosureSha256"] == CLOSURE, f"{platform} exact shared closure")
        c.require(manifest["fullAOT"] and manifest["fullTrim"] and
                  not manifest["useInterpreter"] and not manifest["jit"],
                  f"{platform} full-AOT/full-trim/no-interpreter/no-JIT")
        return info


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
        "apple-everest/module-durability-audit-stage25f.json",
        "apple-everest/runtime/AppleEverestBoundedYaml.cs",
        "apple-everest/runtime/AppleEverestModuleYaml.cs",
        "apple-everest/runtime/AppleEverestModuleSnapshotCodec.cs",
        "apple-everest/runtime/AppleEverestModuleCompression.cs",
        "apple-everest/runtime/AppleEverestModuleReplicaAuthority.cs",
        "apple-everest/runtime/AppleEverestModulePersistence.cs",
        "tools/AppleEverestBuilder/DurabilityAdapterGenerator.cs",
        "tools/AppleEverestBuilder/tests/ModuleDurabilityTests.cs",
        "scripts/fetch-apple-everest-stage25f-fixtures.sh",
        "docs/APPLE_EVEREST_STATIC_AOT.md",
        "docs/APPLE_EVEREST_COMPATIBILITY.md",
        "docs/history/stages/APPLE_EVEREST_MODULE_DURABILITY_STAGE25F_REPORT.md",
    ]
    for relative in required:
        c.require((root / relative).is_file(), f"required {relative}")

    audit = json.loads(read(root, "apple-everest/module-durability-audit-stage25f.json"))
    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25F-A", "audit schema/stage")
    c.require(audit["everestCommit"] == EVEREST, "exact pinned Everest")
    c.require(len(audit["candidates"]) >= 10, "at least ten real source-available releases audited")
    selected = audit["selected"]
    c.require(selected["name"] == "DeathMarkers" and selected["version"] == "2.0.0", "selected fixture/version")
    c.require(selected["zipSha256"] == DEATH_ZIP and selected["dllSha256"] == DEATH_DLL,
              "selected release ZIP and DLL pins")
    c.require(selected["sourceCommit"] == DEATH_SOURCE and selected["license"] == "MIT",
              "selected audited source/license")
    c.require(selected["saveDataClass"] == "DEFAULT_YAML_SAVEDATA_SUPPORTED" and
              selected["sessionClass"] == "DEFAULT_YAML_SESSION_SUPPORTED" and
              selected["saveDataAsync"] == "default-true", "selected default-YAML async class")
    c.require("Dictionary<string,List<DeathMarkersSession.Death>>" in selected["propertyGraph"] and
              "Vector2" in selected["propertyGraph"], "selected nested typed graph")
    c.require(not audit["policy"]["sourceRequiredForProduction"] and
              not audit["policy"]["runtimeReflection"], "source-free/reflection-free production policy")
    for compatibility in ("BINARY_SAVEDATA_DEFERRED", "CUSTOM_SERIALIZER_DEFERRED",
                          "CUSTOM_IO_DEFERRED", "LEGACY_SYNC_SAVEDATA_DEFERRED"):
        c.require(compatibility in audit["policy"]["deferred"], f"explicit deferred class {compatibility}")

    models = read(root, "tools/AppleEverestBuilder/Models.cs")
    c.require('TransformerVersion = "apple-everest-static-v5"' in models and
              "AppleModuleDurabilityCompatibility" in models, "v5 durability declarations")
    freezer = read(root, "tools/AppleEverestBuilder/AssemblyFreezer.cs")
    for token in ("DEFAULT_YAML_SAVEDATA_SUPPORTED", "DEFAULT_YAML_SESSION_SUPPORTED",
                  "BINARY_SAVEDATA_DEFERRED", "CUSTOM_SERIALIZER_DEFERRED",
                  "CUSTOM_IO_DEFERRED", "LEGACY_SYNC_SAVEDATA_DEFERRED",
                  "ModuleHierarchy", "SaveDataAsync"):
        c.require(token in freezer, f"closed analyzer policy {token}")
    c.require("Assembly.GetTypes" not in freezer and "Activator.CreateInstance" not in freezer,
              "host analyzer does not create runtime reflection lane")

    generator = read(root, "tools/AppleEverestBuilder/DurabilityAdapterGenerator.cs")
    for token in ("MaximumGraphTypes", "MaximumGraphDepth", "Dictionary`2", "List`1",
                  "Microsoft.Xna.Framework.Vector2", "YamlIgnoreAttribute", "StringComparer.Ordinal"):
        c.require(token in generator, f"typed serializer generator {token}")
    c.require("dynamic" not in generator.lower() and "reflection" not in generator.lower(),
              "generated codec uses no dynamic/reflection fallback")

    api = read(root, "apple-everest/runtime/EverestStaticApi.cs")
    for token in ("public virtual EverestModuleSaveData _SaveData { get; set; }",
                  "public virtual EverestModuleSession _Session { get; set; }",
                  "public virtual bool SaveDataAsync { get; set; } = true;",
                  "public virtual void CreateModMenuSection",
                  "public int Index { get; set; }"):
        c.require(token in api, f"pinned module ABI {token}")

    yaml = read(root, "apple-everest/runtime/AppleEverestModuleYaml.cs") + read(
        root, "apple-everest/runtime/AppleEverestBoundedYaml.cs")
    for token in ("512 * 1024", "MaximumDepth", "MaximumNodes", "MaximumScalarCharacters",
                  "dynamic features are unsupported", "duplicate key", "valid UTF-8"):
        c.require(token in yaml, f"bounded YAML rule {token}")
    c.require("Type.GetType" not in yaml and "Activator.CreateInstance" not in yaml,
              "YAML cannot instantiate arbitrary types")

    codec = read(root, "apple-everest/runtime/AppleEverestModuleSnapshotCodec.cs")
    for token in ("AEVMSV1", "BaseSaveSha256", "StaticClosureSha256", "MaximumModules",
                  "duplicate Apple Everest module snapshot identity", "FixedTimeEquals"):
        c.require(token in codec, f"aggregate contract {token}")
    authority = read(root, "apple-everest/runtime/AppleEverestModuleReplicaAuthority.cs")
    c.require("SelectMatching" in authority and "OrderByDescending(item => item.Generation)" in authority and
              "AppleEverestModuleSnapshotCodec.Encode(decoded)" in authority,
              "primary/previous-good matching and exact readback")
    compression = read(root, "apple-everest/runtime/AppleEverestModuleCompression.cs")
    for token in ("AEVMZV1", "512 * 1024", "126976", "MaximumReplicaBytes * 6", "DeflateStream"):
        c.require(token in compression, f"bounded tvOS compression {token}")
    persistence = read(root, "apple-everest/runtime/AppleEverestModulePersistence.cs")
    for token in ("Everest/Slots", "ApplicationSupportDirectory", 'Path.Combine(root, "Celeste")', "module-state-v1.",
                  "CelesteAppleEverest.Slot", ".State.", "#if TVOS", "CaptureSave",
                  "CommitCapturedSave", "DeleteSlot", "ResetSessionForNewVanillaSession"):
        c.require(token in persistence, f"slot persistence/lifecycle {token}")
    c.require("AppleEverest/ModuleSettings.v1" not in persistence and "settings.celeste" not in persistence,
              "module slot state remains separate from settings and vanilla save")

    runtime = "\n".join(path.read_text() for path in (root / "apple-everest/runtime").glob("*.cs"))
    for token in ("DynamicInvoke", "Reflection.Emit", "DynamicMethod", "Assembly.Load",
                  "AssemblyLoadContext", "NativeDetour", "ILHook", "Process.Start",
                  "FileSystemWatcher", "NLua", "KeraLua", "Activator.CreateInstance", "Assembly.GetTypes"):
        c.require(token not in runtime, f"device runtime excludes {token}")
    static_runtime = read(root, "apple-everest/runtime/AppleEverestStaticRuntime.cs")
    c.require("CaptureModuleSnapshotEntries" in static_runtime and "preserve-previous" in static_runtime and
              "ResetModuleSessions" in static_runtime and "module-session=reset" in static_runtime,
              "immutable snapshot/failure isolation/session reset runtime")
    c.require("NonPersistentModSession" in static_runtime and "FilterVanillaFileSave" in static_runtime and
              "CompleteNonPersistentModSession" in static_runtime,
              "debug maps remain isolated and nonpersistent")

    closure_source = read(root, "tools/AppleEverestBuilder/ClosureGenerator.cs")
    for token in ("CaptureSave(SaveData.Instance.FileSlot, savingFileData)", "CommitCapturedSave()",
                  "DiscardCapturedSave()", "appleEverestSaveQueued", "appleEverestQueuedFile |= file",
                  "PreloadSlot(i, UserIO.Serialize(saveData))", "ActivateSlot(slot, UserIO.Serialize(Instance))",
                  "ResetSessionForNewVanillaSession", "DeleteSlot(slot)"):
        c.require(token in closure_source, f"vanilla lifecycle transform {token}")
    c.require("Queue<Tuple<bool, bool>>" not in closure_source, "save follow-up queue is bounded/coalesced")
    for token in ("PatchPinnedEverestCompatibility", "public Vector2 bounce", "public bool finished",
                  "public string SID", "public Scene scene"):
        c.require(token in closure_source, f"selected ordinary DLL ABI {token}")
    for token in ('module == "DeathMarkers" && property.Name == "Mode"',
                  "if (!deaths.ContainsKey(sid)) deaths.Add(sid",
                  "settings.Mode = next"):
        c.require(token in closure_source, f"DeathMarkers live Mode guard {token}")

    settings = read(root, "apple-everest/runtime/AppleEverestSettingsPersistence.cs")
    c.require("CelesteAppleEverest.Settings.v1" in settings and "AppleEverest/ModuleSettings.v1" in settings,
              "global module settings remain separate")
    build = read(root, "scripts/build-apple-everest-canary.sh")
    c.require("UseInterpreter=false" in build and "RunAOTCompilation=true" in build and "TrimMode=full" in build,
              "Apple full-AOT build policy")
    c.require("verify-referenced-api" in build and "verify-aot-object" in build and ".dll.llvm.o" in build,
              "external assembly method AOT verification retained")
    c.require("--mods" not in read(root, "build-ios.sh") and "--mods" not in read(root, "build-tvos.sh"),
              "ordinary vanilla public builders remain mod-free")
    fetch = read(root, "scripts/fetch-apple-everest-stage25f-fixtures.sh")
    c.require(DEATH_ZIP in fetch and "https://gamebanana.com/mmdl/1543349" in fetch and
              "fetch-apple-everest-stage25e-fixtures.sh" in fetch,
              "exact source-free Stage 25F fixture acquisition")

    docs = read(root, "docs/APPLE_EVEREST_STATIC_AOT.md") + read(root, "docs/APPLE_EVEREST_COMPATIBILITY.md")
    for token in ("DeathMarkers", "EverestModuleSaveData", "EverestModuleSession", "Application Support",
                  "CelesteAppleEverest.Slot", "base-save", "previous-good", "nonpersistent"):
        c.require(token.lower() in docs.lower(), f"documentation {token}")
    report = read(root, "docs/history/stages/APPLE_EVEREST_MODULE_DURABILITY_STAGE25F_REPORT.md")
    c.require("Stage 25F-A" in report and BASE in report and DEATH_ZIP in report and CLOSURE in report,
              "Stage report records baseline/fixture/closure")
    c.require("APPLE_EVEREST_MODULE_DURABILITY_STAGE25F_REPORT.md" in read(root, "docs/history/README.md"),
              "history index contains Stage 25F-A")

    lock_text = "\n".join([
        read(root, "managed/celeste-input-profiles.json"), read(root, "scripts/celeste-managed.py"),
        read(root, "scripts/verify-celeste-ios-stage24e1.py"), read(root, "native/ios-native-output.lock.json"),
        read(root, "build-tvos.sh"),
    ])
    for value, label in ((CONTENT, "Content"), (RAW, "raw"), (PATCHED, "patched"),
                         (STAGE6, "Stage 6"), (VANILLA_IOS, "vanilla iOS"),
                         (IOS_NATIVE, "iOS native"), (TVOS_NATIVE, "tvOS native")):
        c.require(value in lock_text, f"accepted {label} lock")
    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC, "iOS recovery tag")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "tvOS RC1")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "tvOS RC2")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 tag absent")
    c.require(git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3, "deferred RC3 preserved")
    c.require(git(root, "rev-parse", "tvos-port") == BASE and
              git(root, "rev-parse", "origin/tvos-port") == BASE, "integration branch preserved")
    c.require(git(root, "diff", "--name-only", BASE, "--", ".github") == "", "GitHub Actions untouched")

    closure = args.closure or root / ".build/apple-everest/production-canary/shared-closure"
    if closure.is_dir():
        manifest = json.loads((closure / "compatibility-manifest.json").read_text())
        c.require(manifest["transformerVersion"] == "apple-everest-static-v5", "v5 transformer")
        c.require(manifest["sharedClosureSha256"] == CLOSURE and
                  manifest["moduleDurabilityClosureSha256"] == DURABILITY_CLOSURE,
                  "exact shared and durability closures")
        c.require(manifest["managedLogicalSha256"] == MANAGED and
                  manifest["contentLogicalSha256"] == CLOSURE_CONTENT,
                  "exact managed/content trees")
        c.require(manifest["moduleDurabilityAdapterCount"] == 4 and
                  manifest["moduleDurabilityFormat"] == "per-slot-aggregate-ab-v1",
                  "four typed durability adapters and v1 aggregate")
        c.require(manifest["moduleDurabilityLogicalMaximumBytes"] == 2 * 1024 * 1024 and
                  manifest["moduleDurabilityTvOSLogicalMaximumBytes"] == 512 * 1024 and
                  manifest["moduleDurabilityTvOSReplicaMaximumBytes"] == 126976 and
                  manifest["moduleDurabilityTvOSTotalMaximumBytes"] == 761856,
                  "manifest records exact iOS/tvOS bounds")
        mods = {item["name"]: item for item in manifest["selectedMods"]}
        death = mods["DeathMarkers"]
        c.require(death["durability"]["SaveDataClass"] == "DEFAULT_YAML_SAVEDATA_SUPPORTED" and
                  death["durability"]["SessionClass"] == "DEFAULT_YAML_SESSION_SUPPORTED" and
                  death["durability"]["schemaSha256"] ==
                    "ca82473abce7558f7fd6c51e9387cb3c332b39ce6aed536af40fc625ebaf4695",
                  "DeathMarkers exact generated durability descriptor")
        frozen = {item["assemblyName"]: item for item in manifest["frozenAssemblies"]}
        c.require(frozen["DeathMarkers"]["originalSha256"] == DEATH_DLL and
                  frozen["DeathMarkers"]["frozenSha256"] == DEATH_FROZEN,
                  "DeathMarkers original/frozen identities")
        c.require(sha(closure / "assemblies/DeathMarkers.dll") == DEATH_FROZEN,
                  "DeathMarkers frozen bytes")
        c.require(not any("DeathMarkers" in path.name for path in (closure / "managed").glob("Mod*.cs")),
                  "DeathMarkers source absent from generated closure")
        roots = read(closure, "managed/AppleEverestExternalAssemblyRoots.props")
        c.require('<TrimmerRootAssembly Include="DeathMarkers" />' in roots,
                  "DeathMarkers trimmer root")
        aot_roots = read(closure, "managed/GeneratedAppleEverestAotRoots.cs")
        durability_adapters = read(closure, "managed/GeneratedAppleEverestModuleDurabilityAdapters.cs")
        c.require("DeathMarkersSaveData" in aot_roots and "DeathMarkersSession" in aot_roots and
                  "DeathMarkersSession.Death" in durability_adapters,
                  "complete selected typed serializer and AOT roots")
        c.require(manifest["runtimeDllLoading"] is False and manifest["interpreter"] is False,
                  "closed runtime policy")

    if args.fixtures:
        c.require(sha(args.fixtures / "DeathMarkers-v2.0.0.zip") == DEATH_ZIP,
                  "ordinary selected fixture ZIP")
    if args.ios_ipa:
        info = package(args.ios_ipa.resolve(), "iOS", c)
        c.require(str(info.get("MinimumOSVersion", "")).startswith("15"), "iOS 15 minimum")
    if args.tvos_ipa:
        info = package(args.tvos_ipa.resolve(), "tvOS", c)
        c.require(str(info.get("MinimumOSVersion", "")).startswith("16"), "tvOS 16 minimum")

    output = {
        "schemaVersion": 1,
        "stage": "25F-A",
        "status": "PASS",
        "checks": c.count,
        "sharedClosureSha256": CLOSURE,
        "durabilityClosureSha256": DURABILITY_CLOSURE,
        "selectedFixture": "DeathMarkers@2.0.0",
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(output, indent=2, sort_keys=True) + "\n")
    print(f"PASS: Stage 25F-A verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
