#!/usr/bin/env python3
"""Verify Stage 25F-B bounded static MonoMod ModInterop compatibility."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess
import tempfile
import zipfile


START = "9fd2a809cf20521be7a1403b78fc1d15bbcc1782"
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
PROVIDER_ZIP = "cefd1f8264d4eba9ccd8abb324f88764b99c1fda7812cb6cbbb84cef05c6e8ed"
PROVIDER_DLL = "f4471853b8e6c2abdd107609eeee2852076cde4c7e70667367b71a38d72a62b9"
CONSUMER_ZIP = "155b2ff92857e92f2c4510270e2c6d391b40510945fe3c6c31f9d8fa43a58177"
CONSUMER_DLL = "5dd3ceadbd5c085c5b1c4ffebef9f7560c80c8a94f9dc35d4001f2b4443ba5b0"
PAIR_PLAN = "9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318"
SHARED_CLOSURE = "d447f31a17d582f41537a14c44f0940f960e8cd40c4420b74711101976aa7537"
EMPTY_PLAN = "84f0267040cda9cc51d4dc08da367a4b934aa0e9f43cf3d553815e594f25899d"


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


def verify_fixture(path: pathlib.Path, expected_zip: str, member: str, expected_dll: str, c: Checks) -> None:
    c.require(path.is_file(), f"fixture exists: {path.name}")
    c.require(sha(path) == expected_zip, f"fixture ZIP hash: {path.name}")
    with zipfile.ZipFile(path) as archive:
        c.require(archive.testzip() is None, f"fixture ZIP integrity: {path.name}")
        c.require(hashlib.sha256(archive.read(member)).hexdigest() == expected_dll,
                  f"fixture DLL hash: {path.name}")


def verify_package(path: pathlib.Path, platform: str, c: Checks) -> None:
    with tempfile.TemporaryDirectory() as temporary:
        with zipfile.ZipFile(path) as archive:
            c.require(archive.testzip() is None, f"{platform} IPA ZIP integrity")
            archive.extractall(temporary)
        apps = list((pathlib.Path(temporary) / "Payload").glob("*.app"))
        c.require(len(apps) == 1, f"{platform} one app")
        app = apps[0]
        info = plistlib.loads((app / "Info.plist").read_bytes())
        c.require("everestcanary" in str(info.get("CFBundleIdentifier", "")).lower(),
                  f"{platform} isolated canary identity")
        executable = app / str(info.get("CFBundleExecutable", ""))
        c.require(executable.is_file(), f"{platform} executable")
        c.require("arm64" in subprocess.check_output(["file", str(executable)], text=True),
                  f"{platform} arm64")
        for assembly in ("Celeste", "CpopHelper", "DeathMarkers", "FeatherMaddy", "LagPauser",
                         "ParticlePaletteHelper"):
            c.require((app / f"{assembly}.dll").is_file(), f"{platform} {assembly} assembly")
            c.require((app / f"{assembly}.aotdata.arm64").is_file(), f"{platform} {assembly} AOT data")
        lowered = [candidate.name.lower() for candidate in app.rglob("*") if candidate.is_file()]
        for forbidden in ("monomod.utils.dll", "mmhook", "monomod.runtimedetour.dll", "ilhook",
                          "nativedetour", "nlua", "keralua"):
            c.require(not any(forbidden in name for name in lowered), f"{platform} excludes {forbidden}")
        manifest_path = path.parent / "build-manifest.json"
        c.require(manifest_path.is_file(), f"{platform} build manifest")
        manifest = json.loads(manifest_path.read_text())
        c.require(manifest["sharedClosureSha256"] == SHARED_CLOSURE, f"{platform} shared closure")
        c.require(manifest["fullAOT"] and manifest["fullTrim"], f"{platform} AOT/trim")
        c.require(not manifest["useInterpreter"] and not manifest["jit"], f"{platform} no interpreter/JIT")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--fixtures", type=pathlib.Path)
    parser.add_argument("--closure", type=pathlib.Path)
    parser.add_argument("--pair-audit", type=pathlib.Path)
    parser.add_argument("--ios-ipa", type=pathlib.Path)
    parser.add_argument("--tvos-ipa", type=pathlib.Path)
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    required = [
        "apple-everest/modinterop-audit-stage25fb.json",
        "apple-everest/runtime/MonoModModInteropStaticFacade.cs",
        "apple-everest/tests/desktop-modinterop/Reference/Program.cs",
        "tools/AppleEverestBuilder/ModInteropPlanner.cs",
        "tools/AppleEverestBuilder/tests/ModInteropTests.cs",
        "scripts/fetch-apple-everest-stage25fb-fixtures.sh",
        "scripts/test-apple-everest-desktop-modinterop.sh",
        "docs/APPLE_EVEREST_STATIC_AOT.md",
        "docs/APPLE_EVEREST_COMPATIBILITY.md",
        "docs/history/stages/APPLE_EVEREST_MODINTEROP_STAGE25FB_REPORT.md",
    ]
    for relative in required:
        c.require((root / relative).is_file(), f"required file {relative}")

    audit = json.loads(read(root, "apple-everest/modinterop-audit-stage25fb.json"))
    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25F-B", "audit schema/stage")
    c.require(audit["classification"] == "YELLOW", "honest bounded classification")
    c.require(audit["everestCommit"] == EVEREST and audit["monoModCommit"] == MONOMOD,
              "exact Everest/MonoMod pins")
    c.require(audit["transformerVersion"] == "apple-everest-static-v6", "transformer v6")
    c.require(len(audit["candidateAudit"]) >= 15, "at least fifteen real releases audited")
    provider, consumer = audit["selected"]["provider"], audit["selected"]["consumer"]
    c.require(provider["name"] == "ConditionHelper" and provider["version"] == "1.0.0",
              "selected provider identity")
    c.require(provider["zipSha256"] == PROVIDER_ZIP and provider["dllSha256"] == PROVIDER_DLL,
              "selected provider hashes")
    c.require(provider["sourceCommit"] == "1d42c6a756b41ea7165a11891b91869252ed59dd" and
              provider["license"] == "MIT", "selected provider source/license")
    c.require(consumer["name"] == "AchievementHelper" and consumer["version"] == "1.0.5",
              "selected consumer identity")
    c.require(consumer["zipSha256"] == CONSUMER_ZIP and consumer["dllSha256"] == CONSUMER_DLL,
              "selected consumer hashes")
    c.require(consumer["sourceCommit"] == "6f8bb7807b077195c149f2336be2fb166a0e483b" and
              consumer["license"] == "MIT", "selected consumer source/license")
    c.require(audit["selected"]["planSha256"] == PAIR_PLAN, "exact pair plan hash")
    c.require(audit["selected"]["registrations"] == 3 and audit["selected"]["exports"] == 7 and
              audit["selected"]["imports"] == audit["selected"]["resolvedImports"] == 4,
              "pair plan counts/all imports resolved")
    c.require(not audit["selected"]["physicalInvocation"] and
              "On.*" in audit["selected"]["physicalBlocker"], "physical blocker recorded")
    c.require(not audit["policy"]["sourceRequiredForProduction"] and
              not audit["policy"]["runtimeReflection"] and
              not audit["policy"]["runtimeDelegateCreationFromMethodInfo"],
              "source-free/static device policy")

    models = read(root, "tools/AppleEverestBuilder/Models.cs")
    c.require('TransformerVersion = "apple-everest-static-v6"' in models, "v6 transformer")
    for token in ("MODINTEROP_STATIC_SUPPORTED", "MODINTEROP_DEFERRED", "ModInteropRegistrations"):
        c.require(token in models, f"model token {token}")
    planner = read(root, "tools/AppleEverestBuilder/ModInteropPlanner.cs")
    for token in ("MonoMod.ModInterop.ModInteropManager", "ModExportNameAttribute", "ModImportNameAttribute",
                  "DEFERRED_DYNAMIC_MODINTEROP_TYPE", "DEFERRED_OPEN_GENERIC_MODINTEROP_TYPE",
                  "DEFERRED_READONLY_MODINTEROP_IMPORT", "OPTIONAL_INTEROP_PROVIDER_ABSENT",
                  "REQUIRED_INTEROP_PROVIDER_ABSENT", "assembly.Name.Name", "Refresh()",
                  "nextOrdinal", "bestOrdinal", "registration", "new global::"):
        c.require(token in planner, f"planner contract {token}")
    c.require("Assembly.GetTypes" not in planner and "Delegate.CreateDelegate" not in planner and
              "DynamicInvoke" not in planner and "MakeGenericType" not in planner,
              "planner generates no device reflection/dynamic lane")
    c.require("method.IsPublic && method.IsStatic" in planner and
              "field.IsPublic && field.IsStatic" in planner, "pinned public-static export/import envelope")
    c.require("[method.Name, prefix + \".\" + method.Name]" in planner,
              "qualified and unqualified exports")
    c.require("Compatible(item.Import.Signature, export.Export.Signature)" in planner,
              "signature compatibility before binding")
    c.require("OrderBy(value => value.Registration).ThenBy(value => value.Export.MethodOrder)" in planner,
              "deterministic first-compatible order")

    facade = read(root, "apple-everest/runtime/MonoModModInteropStaticFacade.cs")
    for token in ("namespace MonoMod.ModInterop", "public static class ModInteropManager",
                  "public static void ModInterop(this Type type)", "GeneratedAppleEverestModInterop.Register(type)",
                  "ModExportNameAttribute", "ModImportNameAttribute"):
        c.require(token in facade, f"static ABI facade {token}")
    for forbidden in ("GetMethods", "GetFields", "Delegate.CreateDelegate", "MethodInfo", "Assembly.Load",
                      "Reflection.Emit", "DynamicMethod"):
        c.require(forbidden not in facade, f"facade excludes {forbidden}")

    freezer = read(root, "tools/AppleEverestBuilder/AssemblyFreezer.cs")
    for token in ("ModInteropPlanner.RequiredPublicTypes", 'type.Namespace == "MonoMod.ModInterop"',
                  "IgnoresAccessChecksToAttribute"):
        c.require(token in freezer, f"frozen ABI handling {token}")
    scanner = read(root, "tools/AppleEverestBuilder/RuntimeClosureScanner.cs")
    c.require('type.Namespace == "MonoMod.ModInterop"' in scanner and
              '"ModInteropManager" => method.Name == "ModInterop"' in scanner,
              "runtime scanner permits only bounded facade")
    generator = read(root, "tools/AppleEverestBuilder/ClosureGenerator.cs")
    for token in ("GeneratedAppleEverestModInterop.cs", "modInteropPlanSha256",
                  "modInteropRegistrationCount", "modInteropExportCount", "modInteropImportCount",
                  "MonoMod.ModInterop:host-cecil-static-typed-plan:v1"):
        c.require(token in generator, f"closure manifest/hash token {token}")

    tests = read(root, "tools/AppleEverestBuilder/tests/ModInteropTests.cs")
    for token in ("consumer-first", "provider-first", "late refresh", "duplicate typeof",
                  "qualified, explicit, unqualified", "wrong overload skipped", "custom generic delegate",
                  "closed generic", "open generic", "dynamic Type", "optional null", "required provider",
                  "contravariant", "covariant", "readonly delegate", "deterministic SHA-256"):
        c.require(token.lower() in tests.lower(), f"deterministic coverage {token}")
    desktop = read(root, "apple-everest/tests/desktop-modinterop/Reference/Program.cs")
    for token in ("ProviderA", "ProviderB", "ImportsBefore", "ImportsAfter", "ModInterop", "PASS"):
        c.require(token in desktop, f"desktop conformance {token}")

    fetch = read(root, "scripts/fetch-apple-everest-stage25fb-fixtures.sh")
    for token in (PROVIDER_ZIP, CONSUMER_ZIP, "https://gamebanana.com/mmdl/1081578",
                  "https://gamebanana.com/mmdl/1081965"):
        c.require(token in fetch, f"fixture fetch pin {token}")
    if args.fixtures:
        verify_fixture(args.fixtures / "ConditionHelper-v1.0.0.zip", PROVIDER_ZIP,
                       "bin/ConditionHelper.dll", PROVIDER_DLL, c)
        verify_fixture(args.fixtures / "AchievementHelper-v1.0.5.zip", CONSUMER_ZIP,
                       "bin/AchievementHelper.dll", CONSUMER_DLL, c)
    if args.pair_audit:
        pair = json.loads(args.pair_audit.read_text())
        c.require(pair["transformerVersion"] == "apple-everest-static-v6", "pair audit transformer")
        c.require(pair["modInterop"]["PlanSha256"] == PAIR_PLAN, "pair audit plan hash")
        c.require(pair["modInterop"]["RegistrationCount"] == 3 and
                  pair["modInterop"]["ExportCount"] == 7 and
                  pair["modInterop"]["ImportCount"] == pair["modInterop"]["ResolvedImportCount"] == 4,
                  "pair audit exact counts")
    if args.closure:
        manifest = json.loads((args.closure / "compatibility-manifest.json").read_text())
        c.require(manifest["transformerVersion"] == "apple-everest-static-v6", "closure transformer")
        c.require(manifest["sharedClosureSha256"] == SHARED_CLOSURE, "exact shared closure hash")
        c.require(manifest["modInteropPlanSha256"] == EMPTY_PLAN, "existing regression closure empty plan")
        c.require(manifest["modInteropRegistrationCount"] == 0 and manifest["modInteropExportCount"] == 0 and
                  manifest["modInteropImportCount"] == 0, "existing closure has no accidental registrations")
        c.require((args.closure / "managed/GeneratedAppleEverestModInterop.cs").is_file(),
                  "generated static plan source")
    if args.ios_ipa:
        verify_package(args.ios_ipa, "iOS", c)
    if args.tvos_ipa:
        verify_package(args.tvos_ipa, "tvOS", c)

    runtime = "\n".join(path.read_text() for path in (root / "apple-everest/runtime").glob("*.cs"))
    for forbidden in ("Assembly.GetTypes", "Delegate.CreateDelegate", "DynamicInvoke", "Reflection.Emit",
                      "DynamicMethod", "Assembly.Load", "AssemblyLoadContext", "NativeDetour", "ILHook",
                      "Process.Start", "FileSystemWatcher", "NLua", "KeraLua"):
        c.require(forbidden not in runtime, f"device runtime excludes {forbidden}")
    durability = read(root, "apple-everest/runtime/AppleEverestModulePersistence.cs") + read(
        root, "apple-everest/runtime/AppleEverestModuleSnapshotCodec.cs")
    for token in ("Everest/Slots", "ApplicationSupportDirectory", "CelesteAppleEverest.Slot", "AEVMSV1",
                  "BaseSaveSha256", "StaticClosureSha256", "FixedTimeEquals", "DeleteSlot"):
        c.require(token in durability, f"Stage 25F durability retained {token}")
    docs = read(root, "docs/APPLE_EVEREST_STATIC_AOT.md") + read(root, "docs/APPLE_EVEREST_COMPATIBILITY.md")
    for token in ("ModInterop", "ConditionHelper", "AchievementHelper", "YELLOW", "Delegate.CreateDelegate",
                  "OPTIONAL_INTEROP_PROVIDER_ABSENT", "DEFERRED_DYNAMIC_MODINTEROP_TYPE",
                  "DEFERRED_OPEN_GENERIC_MODINTEROP"):
        c.require(token.lower() in docs.lower(), f"documentation {token}")
    report = read(root, "docs/history/stages/APPLE_EVEREST_MODINTEROP_STAGE25FB_REPORT.md")
    c.require("Stage 25F-B" in report and START in report and PAIR_PLAN in report and SHARED_CLOSURE in report,
              "report baseline/plan/closure")
    c.require("APPLE_EVEREST_MODINTEROP_STAGE25FB_REPORT.md" in read(root, "docs/history/README.md"),
              "history index")

    locks = "\n".join([
        read(root, "managed/celeste-input-profiles.json"),
        read(root, "scripts/celeste-managed.py"),
        read(root, "scripts/verify-celeste-ios-stage24e1.py"),
        read(root, "native/ios-native-output.lock.json"),
        read(root, "build-tvos.sh"),
    ])
    for value in (CONTENT, RAW, PATCHED, STAGE6, VANILLA_IOS, IOS_NATIVE, TVOS_NATIVE):
        c.require(value in locks, f"canonical/native lock retained {value}")
    c.require("--mods" not in read(root, "build-ios.sh") and "--mods" not in read(root, "build-tvos.sh"),
              "vanilla builders remain mod-free")
    build = read(root, "scripts/build-apple-everest-canary.sh")
    c.require("UseInterpreter=false" in build and "RunAOTCompilation=true" in build and
              "TrimMode=full" in build and "verify-aot-object" in build, "full-AOT build policy")

    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC, "immutable iOS RC")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "immutable tvOS RC1")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "immutable tvOS RC2")
    c.require(git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3, "deferred RC3 branch")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 remains untagged")

    print(f"PASS: Stage 25F-B static ModInterop verifier ({c.count})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
