#!/usr/bin/env python3
"""Verify the Stage 25E Apple Everest helper ecosystem and settings boundary."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess
import tempfile
import zipfile


BASE = "3a11b16c04ac77a73328c5cd9b4f8874f854f1e9"
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

CPOP_ZIP = "7a807a8f9ce6ccb4d6ad0c664bb7791a60734533fb33cfcd6beccb202d846b63"
CPOP_DLL = "235d767a0f82f45aef7b01e4ef662934af449ee416deb6073a1eaca3263c75c9"
CPOP_FROZEN = "9242842076d0992e3498a7b6efb3d48ab8e9fc09ea9ba07819bbc9f716e64845"
CPOP_SOURCE = "f8b70384d195a180b5c3e6e3a41f9d65075abcb2"
QUIZ_ZIP = "5cb8351bb263aa316831d2b683cb8b04acd270587edfe7c9e3df3bb8e6b83b3e"
CLOSURE = "d685d6277588b822831d911ff4f674036068d215dd7027bf6cfca5f8f43cbe82"
MANAGED = "4e3344307ede719033c56771d00f4ea1f07301bf2c70aa28bd74b5423ee421e5"
MOD_REGISTRY = "ec59b5d7d384b4b0f9fa9ab9813fa8dd575ec8958995c233b558105215bf834f"
GAMEPLAY_REGISTRY = "680564bcfb179281a75f2b3fd5e805653b357985e9d00d1d49ea6282eb3dfb0e"
CLOSURE_CONTENT = "1ebc6de7e1bb2c5de5bfe8a64dd5aedf761b9711b3a148d423408a37381fa31d"


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


def verify_package(ipa: pathlib.Path, platform: str, c: Checks) -> str:
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
        c.require(executable.is_file() and "arm64" in subprocess.check_output(["file", str(executable)], text=True),
                  f"{platform} arm64")
        for assembly in ("CpopHelper", "FeatherMaddy", "LagPauser", "ParticlePaletteHelper"):
            c.require((app / f"{assembly}.dll").is_file(), f"{platform} {assembly} linked")
            c.require((app / f"{assembly}.aotdata.arm64").is_file(), f"{platform} {assembly} AOT data")
        c.require((app / "Celeste.aotdata.arm64").is_file(), f"{platform} Celeste AOT data")
        c.require((app / "Content/Maps/QuizTest/quiztest.bin").is_file(), f"{platform} dependent map bundled")
        names = [path.name.lower() for path in app.rglob("*") if path.is_file()]
        forbidden = ("mmhook", "monomod.runtimedetour.dll", "nativedetour", "ilhook", "nlua", "keralua")
        c.require(not any(any(token in name for token in forbidden) for name in names),
                  f"{platform} no desktop runtime-patching payload")
        manifest = json.loads((ipa.parent / "build-manifest.json").read_text())
        c.require(manifest["sharedClosureSha256"] == CLOSURE, f"{platform} exact shared closure")
        c.require(manifest["fullAOT"] and manifest["fullTrim"] and not manifest["useInterpreter"] and not manifest["jit"],
                  f"{platform} full-AOT contract")
        return str(info.get("MinimumOSVersion", ""))


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
        "apple-everest/helper-ecosystem-compatibility-stage25e.json",
        "apple-everest/runtime/AppleEverestSettingsCodec.cs",
        "apple-everest/runtime/AppleEverestSettingsPersistence.cs",
        "apple-everest/runtime/EverestEntitiesStaticApi.cs",
        "apple-everest/runtime/EverestBackdropsStaticApi.cs",
        "apple-everest/runtime/EverestCoreEntitiesStaticApi.cs",
        "apple-everest/runtime/EverestTagsStaticApi.cs",
        "tools/AppleEverestBuilder/AssemblyFreezer.cs",
        "tools/AppleEverestBuilder/ClosureGenerator.cs",
        "tools/AppleEverestBuilder/ContentCompiler.cs",
        "scripts/fetch-apple-everest-stage25e-fixtures.sh",
        "scripts/build-apple-everest-real-mods.sh",
        "docs/APPLE_EVEREST_STATIC_AOT.md",
        "docs/APPLE_EVEREST_COMPATIBILITY.md",
        "docs/APPLE_EVEREST_THIRD_PARTY.md",
        "docs/history/stages/APPLE_EVEREST_HELPER_ECOSYSTEM_STAGE25E_REPORT.md",
    ]
    for relative in required:
        c.require((root / relative).is_file(), f"required {relative}")

    matrix = json.loads(read(root, "apple-everest/helper-ecosystem-compatibility-stage25e.json"))
    c.require(matrix["schemaVersion"] == 1 and matrix["stage"] == "25E", "Stage 25E matrix schema")
    c.require(matrix["everestCommit"] == EVEREST and matrix["monoModCommit"] == MONOMOD, "exact Everest/MonoMod pins")
    c.require(len(matrix["helperCandidates"]) >= 8, "at least eight helper candidates audited")
    c.require(len(matrix["mapCandidates"]) >= 12, "at least twelve map candidates audited")
    helpers = {item["name"]: item for item in matrix["helperCandidates"]}
    c.require("SnowyAssortedItems" in helpers and helpers["SnowyAssortedItems"]["version"] == "0.2.0",
              "Snowy Assorted Items audited")
    selected = matrix["selected"]
    helper, map_mod = selected["helper"], selected["map"]
    c.require(helper["name"] == "CpopHelper" and helper["version"] == "1.3.0", "selected Cpop Helper")
    c.require(helper["zipSha256"] == CPOP_ZIP and helper["dllSha256"] == CPOP_DLL,
              "Cpop release ZIP/DLL pins")
    c.require(helper["sourceCommit"] == CPOP_SOURCE and "credit" in helper["license"].lower(), "Cpop source/license finding")
    c.require(map_mod["name"] == "QuizSample" and map_mod["version"] == "0.0.1" and
              map_mod["zipSha256"] == QUIZ_ZIP, "selected dependent map pin")
    c.require("CpopHelper >= 1.0.0" in map_mod["dependencies"], "real helper dependency declared")
    c.require(map_mod["roomCount"] == 4 and len(map_mod["knownFixtureLimitations"]) == 3 and
              "PickTheEvenNumber" in map_mod["knownFixtureLimitations"][0],
              "exact dependent-map behavior and missing-dialog limitation recorded")
    c.require(selected["dependencyGraph"] == ["Everest", "CpopHelper", "QuizSample"], "recorded dependency graph")
    c.require(not selected["sourceTreesRequiredForProduction"], "source trees not required")
    c.require(not selected["redistributedThirdPartyBytes"], "third-party bytes not redistributed")
    c.require(helper["customEntities"] == ["HDGraphic", "TheoJelly", "cpopBlock", "quizController"],
              "four exact helper entities")
    c.require(helper["customTriggers"] == ["checkSubpixelTrigger", "quizAnswerTrigger", "setSubpixelTrigger"],
              "three exact helper triggers")
    c.require(not helper["onHooks"] and not helper["directHooks"] and not helper["ilHooks"], "selected helper requires no detours")
    c.require(not helper["modInterop"] and not helper["nativeLuaCustomAudio"], "selected helper has no ModInterop/native/Lua/audio")

    models = read(root, "tools/AppleEverestBuilder/Models.cs")
    for token in ("apple-everest-static-v4", "AppleCustomEntityFactory", "AppleCustomBackdropFactory",
                  "AppleSettingProperty"):
        c.require(token in models, f"Stage 25E model {token}")
    freezer = read(root, "tools/AppleEverestBuilder/AssemblyFreezer.cs")
    for token in ("CustomEntityAttribute", "CustomBackdropAttribute", "EntityData,Microsoft.Xna.Framework.Vector2",
                  "Celeste.EntityID,Celeste.EntityData", "InspectSettings", "SettingRangeAttribute"):
        c.require(token in freezer, f"Cecil declaration discovery {token}")
    c.require("Activator.CreateInstance" not in freezer and "Assembly.GetTypes" not in freezer,
              "host emits typed factories without runtime activator scans")
    generator = read(root, "tools/AppleEverestBuilder/ClosureGenerator.cs")
    for token in ("GeneratedAppleEverestGameplayRegistry", "TryCreateEntity", "TryCreateTrigger", "TryCreateBackdrop",
                  "RegisterTrackerTypes", "customEntityFactoryCount", "customBackdropFactoryCount",
                  "resolvedMapFactories", "CoreGameplayFactories", "MapPaths",
                  "AppleEverestAtlasMountDescriptor", "Graphics/Atlases/Gameplay/", "Graphics/Atlases/Gui/",
                  "ModuleSettings:typed-menu-and-platform-storage:v1"):
        c.require(token in generator, f"closure generator {token}")
    c.require("CpopHelper" not in generator and "QuizSample" not in generator,
              "no selected-mod special case in generator")
    c.require("InheritedTrackedEntityTypes" in generator and
              "typeof(global::Celeste.Trigger)" in generator and
              "inheritedBase.IsAssignableFrom(type)" in generator,
              "custom triggers and other inherited tracked entities enter canonical base buckets")
    c.require("if (mod.DeclaredAssemblyPath == null)" in generator, "binary helper remains source-free")
    api_surface = json.loads(read(root, "apple-everest/apple-api-surface-v1.json"))
    c.require(len(api_surface["members"]) == 7 and
              {item["id"] for item in api_surface["members"]} >= {
                  "celeste-actor-movement-counter", "celeste-glider-destroyed",
                  "celeste-glider-sprite", "celeste-glider-destroy-animation-routine"},
              "Cpop exact pinned-Everest publicized API surface")
    content_compiler = read(root, "tools/AppleEverestBuilder/ContentCompiler.cs")
    c.require("InspectGameplayIds" in content_compiler and '("entity", name)' in content_compiler and
              '("trigger", name)' in content_compiler and '("backdrop", name)' in content_compiler,
              "map gameplay identifiers inspected generically")

    runtime = "\n".join(path.read_text() for path in (root / "apple-everest/runtime").glob("*.cs"))
    for token in ("DynamicInvoke", "Reflection.Emit", "DynamicMethod", "Assembly.Load", "AssemblyLoadContext",
                  "NativeDetour", "ILHook", "Process.Start", "FileSystemWatcher", "NLua", "KeraLua",
                  "Activator.CreateInstance", "Assembly.GetTypes"):
        c.require(token not in runtime, f"device runtime excludes {token}")
    static_runtime = read(root, "apple-everest/runtime/AppleEverestStaticRuntime.cs")
    c.require("RequiredBy.Length > 0" in static_runtime and "disable=blocked" in static_runtime,
              "required helper disable is blocked")
    c.require("AppleEverestSettingsPersistence.LoadAndApply" in static_runtime and
              "MOD OPTIONS" in static_runtime, "bounded generated Mod Options path")
    c.require("descriptor.Minimum + index * descriptor.Step" in static_runtime and
              "(descriptor.Maximum - descriptor.Minimum) / descriptor.Step" in static_runtime,
              "ranged integer UI honors the generated step")
    c.require("MountStaticAtlases();" in static_runtime and
              "VirtualContent.CreateTexture(descriptor.LogicalPath)" in static_runtime and
              "atlas[descriptor.Key] = mounted" in static_runtime and
              "content-atlas=PASS" in static_runtime,
              "release atlas PNGs enter the live Celeste atlas before module content callbacks")
    c.require("BeginNonPersistentModSession" in static_runtime and
              "FilterVanillaFileSave" in static_runtime and
              "CompleteNonPersistentModSession" in static_runtime and
              "saveDataBeforeModSession" in static_runtime and
              "return Overworld.StartMode.MainMenu" in static_runtime and
              "requested={requestedStartMode} applied={Overworld.StartMode.MainMenu}" in static_runtime,
              "static maps isolate temporary SaveData and restore it through the null-safe main-menu boundary")
    c.require("PatchNonPersistentSave" in generator and
              "FilterVanillaFileSave(file)" in generator and
              "PatchNonPersistentOverworldReturn" in generator and
              "CompleteNonPersistentModSession(StartMode)" in generator,
              "locked generated transforms suppress debug-slot persistence and normalize overworld return")
    c.require("GeneratedAppleEverestManagedDetourRegistry.RemoveOwner" in static_runtime,
              "existing owner cleanup retained")
    log_policy = read(root, "apple-everest/runtime/AppleEverestLogPolicy.cs")
    c.require("if (level < LogLevel.Info) level = LogLevel.Info" in log_policy,
              "production Info floor prevents third-party per-frame verbose I/O")
    c.require("readonly BitTag SubHUD = Tags.HUD" in read(root, "apple-everest/runtime/EverestTagsStaticApi.cs"),
              "bounded high-resolution SubHUD facade")
    codec = read(root, "apple-everest/runtime/AppleEverestSettingsCodec.cs")
    c.require("APPLE_EVEREST_SETTINGS_V1" in codec and "16 * 1024" in codec and
              "CultureInfo.InvariantCulture" in codec, "bounded shared settings codec")
    persistence = read(root, "apple-everest/runtime/AppleEverestSettingsPersistence.cs")
    for token in ("CelesteAppleEverest.Settings.v1", "AppleEverest/ModuleSettings.v1",
                  "ApplicationSupportDirectory", "#if TVOS", "data.Save(url, true)",
                  "defaults.Synchronize()"):
        c.require(token in persistence, f"settings persistence {token}")
    c.require("#if TVOS_CELESTE_RUNTIME_HOST" not in persistence,
              "generated tvOS source uses its actual TVOS compile symbol")
    c.require("SaveData" not in persistence and "settings.celeste" not in persistence,
              "module settings isolated from vanilla persistence")
    c.require("module-settings=corrupt action=defaults" in persistence and "descriptor.Accepts" in persistence,
              "corrupt/wrong-type settings fail to defaults")

    fetch = read(root, "scripts/fetch-apple-everest-stage25e-fixtures.sh")
    for token in (CPOP_ZIP, QUIZ_ZIP, "https://gamebanana.com/mmdl/1035472",
                  "https://gamebanana.com/mmdl/964770"):
        c.require(token in fetch, f"source-free fixture pin {token}")
    c.require("--mods" not in read(root, "build-ios.sh") and "--mods" not in read(root, "build-tvos.sh"),
              "public vanilla builders remain mod-free")
    build = read(root, "scripts/build-apple-everest-canary.sh")
    c.require("UseInterpreter=false" in build and "RunAOTCompilation=true" in build and "TrimMode=full" in build,
              "Apple full-AOT build contract")
    c.require("verify-referenced-api" in build and "verify-aot-object" in build and ".dll.llvm.o" in build,
              "external managed API/method AOT coverage")

    docs = read(root, "docs/APPLE_EVEREST_STATIC_AOT.md") + read(root, "docs/APPLE_EVEREST_COMPATIBILITY.md")
    for token in ("Cpop Helper", "QuizSample", "custom entity", "custom trigger", "Mod Options",
                  "Application Support", "CelesteAppleEverest.Settings.v1", "not general Everest support",
                  "PickTheEvenNumber", "missing-dialog fallback"):
        c.require(token.lower() in docs.lower(), f"documentation {token}")
    notices = read(root, "docs/APPLE_EVEREST_THIRD_PARTY.md")
    c.require("Cpop Helper" in notices and "QuizSample" in notices and "not tracked or redistributed" in notices,
              "third-party provenance/license boundary")
    report = read(root, "docs/history/stages/APPLE_EVEREST_HELPER_ECOSYSTEM_STAGE25E_REPORT.md")
    c.require("PASS — GREEN" in report and BASE in report and CLOSURE in report, "Stage 25E report status/baseline/closure")
    c.require("APPLE_EVEREST_HELPER_ECOSYSTEM_STAGE25E_REPORT.md" in read(root, "docs/history/README.md"),
              "Stage 25E history index")

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
    c.require(git(root, "rev-parse", "tvos-port") == BASE and git(root, "rev-parse", "origin/tvos-port") == BASE,
              "integration branch preserved")
    c.require(git(root, "diff", "--name-only", BASE, "--", ".github") == "", "GitHub Actions untouched")

    closure = args.closure or root / ".build/apple-everest/production-canary/shared-closure"
    if closure.is_dir():
        manifest = json.loads((closure / "compatibility-manifest.json").read_text())
        c.require(manifest["sharedClosureSha256"] == CLOSURE, "exact reproducible helper closure")
        c.require(manifest["transformerVersion"] == "apple-everest-static-v4", "v4 helper transformer")
        c.require(manifest["managedLogicalSha256"] == MANAGED and manifest["contentLogicalSha256"] == CLOSURE_CONTENT,
                  "exact managed/content closure hashes")
        c.require(manifest["registrySha256"] == MOD_REGISTRY and
                  sha(closure / "managed/GeneratedAppleEverestGameplayRegistry.cs") == GAMEPLAY_REGISTRY,
                  "exact module/gameplay registries")
        c.require(manifest["resolvedOrder"] == ["CpopHelper", "FeatherMaddy", "IAccidentallyFourCassetteBlocks",
                                                "LagPauser", "ParticlePaletteHelper", "QuizSample"],
                  "deterministic six-package order")
        c.require(manifest["customEntityFactoryCount"] == 8 and manifest["customBackdropFactoryCount"] == 0,
                  "seven helper factories plus pinned Everest core entity and no selected backdrop")
        c.require(manifest["moduleSettingCount"] == 4, "four real bounded settings")
        c.require(manifest["appleApiSurfaceMemberCount"] == 7 and
                  manifest["appleApiSurfaceSha256"] ==
                  "f69855941b60298ce090315c81c716a01d74dd8359672dbc1e5d7e461a8f232e",
                  "seven-member reviewed Apple API surface")
        resolved = {(item["kind"], item["id"], item["owner"]) for item in manifest["resolvedMapFactories"]}
        c.require(("entity", "quizController", "CpopHelper") in resolved and
                  ("trigger", "quizAnswerTrigger", "CpopHelper") in resolved and
                  ("entity", "everest/coreMessage", "EverestCore") in resolved,
                  "real map resolves helper entity, trigger and pinned Everest core entity")
        frozen = {item["assemblyName"]: item for item in manifest["frozenAssemblies"]}
        c.require(frozen["CpopHelper"]["originalSha256"] == CPOP_DLL and
                  frozen["CpopHelper"]["frozenSha256"] == CPOP_FROZEN, "Cpop frozen assembly identity")
        c.require(sha(closure / "assemblies/CpopHelper.dll") == CPOP_FROZEN, "Cpop frozen bytes")
        c.require(not any(path.name.startswith("Mod") and "Cpop" in path.name
                          for path in (closure / "managed").glob("*.cs")), "Cpop source absent from closure")
        roots = read(closure, "managed/AppleEverestExternalAssemblyRoots.props")
        c.require(all(f'<TrimmerRootAssembly Include="{name}" />' in roots
                      for name in ("CpopHelper", "FeatherMaddy", "LagPauser", "ParticlePaletteHelper")),
                  "every external assembly fully rooted")
        registry = read(closure, "managed/GeneratedAppleEverestModuleRegistry.cs")
        c.require('new[] { "QuizSample" }' in registry and "DarkRooms" in registry and "LagPauser" in registry,
                  "required-by and real setting descriptors generated")
        gameplay = read(closure, "managed/GeneratedAppleEverestGameplayRegistry.cs")
        c.require("new global::Celeste.Mod.CpopHelper.CpopHelperModule.quizController" in gameplay and
                  "new global::Celeste.Mod.CpopHelper.CpopHelperModule.quizAnswerTrigger" in gameplay and
                  "new global::Celeste.Mod.Entities.CustomCoreMessage" in gameplay,
                  "real typed helper and core factories generated")
        content_manifest = read(closure, "managed/GeneratedAppleEverestContentManifest.cs")
        c.require('"QuizTest/quiztest"' in content_manifest and
                  '"ncrecc/0/IAccidentallyFourCassetteBlocks"' in content_manifest,
                  "every staged map is generically launchable")
        c.require('"Gameplay", "quiz/test/incorrect/3x"' in content_manifest and
                  '"Gameplay", "quiz/test/correct/3x"' in content_manifest and
                  '"Gui", "areas/cassette"' in content_manifest,
                  "real helper-map and regression GUI assets have exact static atlas keys")
        c.require(manifest["runtimeDllLoading"] is False and manifest["runtimeDetour"] == "static-data-only" and
                  manifest["interpreter"] is False, "closed runtime policy")

    if args.fixtures:
        expected = {
            "CpopHelper-1.3.0.zip": CPOP_ZIP,
            "QuizSample-0.0.1.zip": QUIZ_ZIP,
            "FeatherMaddy-v1.3.zip": "a8f1104710aac5807be3b24cd8c3870d94aa117d1146b30a4de0983a10f3e40e",
            "LagPauser-v1.3.0.zip": "32dac84d2c5b60458a701cb61e8601bc89d937e25bc7fdcf52c80d9128e99d10",
            "ParticlePaletteHelper-v1.0.0.zip": "f9cf8874acbfaff87af22098caea36429c97941d0b13e1114d8749be61afbe19",
            "IAccidentallyFourCassetteBlocks-v1.0.0.zip": "37eaa16b6b2d458a8ce27e08ea315b4cc3c2938d6b3c2292c61203c641ad7c95",
        }
        for name, expected_sha in expected.items():
            c.require(sha(args.fixtures / name) == expected_sha, f"fixture SHA {name}")
    if args.ios_ipa:
        c.require(verify_package(args.ios_ipa.resolve(), "iOS", c).startswith("15"), "iOS 15 minimum")
    if args.tvos_ipa:
        c.require(verify_package(args.tvos_ipa.resolve(), "tvOS", c).startswith("16"), "tvOS 16 minimum")

    output = {
        "schemaVersion": 1,
        "stage": "25E",
        "status": "PASS",
        "checks": c.count,
        "sharedClosureSha256": CLOSURE,
        "helper": "CpopHelper@1.3.0",
        "dependentMap": "QuizSample@0.0.1",
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(output, indent=2, sort_keys=True) + "\n")
    print(f"PASS: Stage 25E verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
