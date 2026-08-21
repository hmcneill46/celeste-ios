#!/usr/bin/env python3
"""Verify Stage 25F-B2 real ModInterop and bounded HookGen acceptance."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess
import tempfile
import zipfile


PARENT = "6cde1ad9ba93bbd2f57f2867baf3b189b2824b1b"
INTEGRATION = "9fd2a809cf20521be7a1403b78fc1d15bbcc1782"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
PROVIDER_ZIP = "cefd1f8264d4eba9ccd8abb324f88764b99c1fda7812cb6cbbb84cef05c6e8ed"
PROVIDER_DLL = "f4471853b8e6c2abdd107609eeee2852076cde4c7e70667367b71a38d72a62b9"
CONSUMER_ZIP = "155b2ff92857e92f2c4510270e2c6d391b40510945fe3c6c31f9d8fa43a58177"
CONSUMER_DLL = "5dd3ceadbd5c085c5b1c4ffebef9f7560c80c8a94f9dc35d4001f2b4443ba5b0"
EXPRESSION_DLL = "9042e55e89b2ef51ad7e9ab4b7ec310f4a22f12ea7429f4207b67ba9bcc607a6"
PLAN = "9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318"
SHARED = "d55f262c376c944a81cf5d253f84dc97592da41ea6e67115ccb33ad6e6b5fabe"
MANAGED = "2fd67cb7f4cf817dd7098f82484b497b17f232947626b409ea96641afe91a49e"
CONTENT_CLOSURE = "26c10db8e2d358a45db18e3ef63147069f123054a74b25728ccb5ffbdf60419e"
HOOK_TRANSFORM = "8c84c0c895f8c2959e4c6fbf70f684cc03ac44e538c518fb495e26266152472f"
CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"

NEW_TARGETS = {
    "celeste-area-mode-stats-clone", "celeste-area-stats-clone",
    "celeste-commands-cmd-heart-gem", "celeste-commands-cmd-hearts-int",
    "celeste-commands-cmd-hearts-int-string", "celeste-commands-cmd-level-flag",
    "celeste-commands-cmd-ow-complete", "celeste-heart-gem-register-as-collected",
    "celeste-level-reload", "celeste-level-update", "celeste-level-update-time",
    "celeste-map-data-load", "celeste-oui-chapter-select-enter",
    "celeste-oui-chapter-select-leave", "celeste-oui-chapter-select-render",
    "celeste-oui-chapter-select-update", "celeste-player-added",
    "celeste-player-call-dash-events", "celeste-player-removed",
    "celeste-player-scene-end", "celeste-save-data-add-death",
    "celeste-save-data-add-strawberry-area-key-entity-id-bool",
    "celeste-save-data-initialize-debug-mode", "celeste-save-data-register-cassette",
    "celeste-save-data-register-completion", "celeste-save-data-register-heart-gem",
    "celeste-save-data-start-session", "celeste-session-set-flag",
    "celeste-session-update-level-start-dashes", "monocle-entity-added",
    "monocle-entity-removed", "monocle-entity-scene-end", "monocle-scene-begin",
}


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, value: bool, message: str) -> None:
        if not value:
            raise SystemExit("FAIL: " + message)
        self.count += 1


def read(root: pathlib.Path, relative: str) -> str:
    return (root / relative).read_text(errors="strict")


def sha(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def git(root: pathlib.Path, *args: str) -> str:
    return subprocess.check_output(["git", "-C", str(root), *args], text=True).strip()


def origin_branch(root: pathlib.Path, branch: str) -> str:
    local = subprocess.run(
        ["git", "-C", str(root), "rev-parse", "--verify", "--quiet", f"origin/{branch}^{{commit}}"],
        capture_output=True, text=True,
    )
    if local.returncode == 0:
        return local.stdout.strip()
    result = git(root, "ls-remote", "--heads", "origin", f"refs/heads/{branch}").split()
    return result[0] if len(result) == 2 else ""


def verify_fixture(path: pathlib.Path, expected_zip: str, members: dict[str, str], c: Checks) -> None:
    c.require(path.is_file(), f"fixture exists: {path.name}")
    c.require(sha(path) == expected_zip, f"fixture ZIP pin: {path.name}")
    with zipfile.ZipFile(path) as archive:
        c.require(archive.testzip() is None, f"fixture ZIP integrity: {path.name}")
        for member, expected in members.items():
            c.require(hashlib.sha256(archive.read(member)).hexdigest() == expected,
                      f"fixture member pin: {path.name}/{member}")


def verify_census(path: pathlib.Path, audit: dict, c: Checks) -> None:
    census = json.loads(path.read_text())
    c.require(census["schemaVersion"] == 1, "census schema")
    actual = {}
    for assembly in census["assemblies"]:
        events = sorted({f'{hook["hookType"]}::{hook["eventName"]}'
                         for hook in assembly["hooks"] if hook["operation"] == "add"})
        removes = sorted({f'{hook["hookType"]}::{hook["eventName"]}'
                          for hook in assembly["hooks"] if hook["operation"] == "remove"})
        c.require(events == removes, f'{assembly["file"]} symmetric add/remove census')
        actual[assembly["file"]] = events
    c.require(actual["ConditionHelper.dll"] == sorted(audit["provider"]["hookEvents"]),
              "ConditionHelper complete binary HookGen inventory")
    c.require(actual["AchievementHelper.dll"] == sorted(audit["consumer"]["hookEvents"]),
              "AchievementHelper complete binary HookGen inventory")


def verify_package(path: pathlib.Path, platform: str, c: Checks) -> None:
    with tempfile.TemporaryDirectory() as temporary:
        with zipfile.ZipFile(path) as archive:
            c.require(archive.testzip() is None, f"{platform} IPA ZIP integrity")
            archive.extractall(temporary)
        apps = list((pathlib.Path(temporary) / "Payload").glob("*.app"))
        c.require(len(apps) == 1, f"{platform} one application")
        app = apps[0]
        info = plistlib.loads((app / "Info.plist").read_bytes())
        c.require("everestcanary" in str(info.get("CFBundleIdentifier", "")).lower(),
                  f"{platform} isolated canary identity")
        executable = app / str(info.get("CFBundleExecutable", ""))
        c.require(executable.is_file() and "arm64" in subprocess.check_output(
            ["file", str(executable)], text=True), f"{platform} arm64 executable")
        for assembly in ("Celeste", "ConditionHelper", "ExpressionParser", "AchievementHelper"):
            c.require((app / f"{assembly}.dll").is_file(), f"{platform} {assembly} DLL")
            c.require((app / f"{assembly}.aotdata.arm64").is_file(), f"{platform} {assembly} AOT data")
        lowered = [item.name.lower() for item in app.rglob("*") if item.is_file()]
        for forbidden in ("monomod.utils.dll", "mmhook", "monomod.runtimedetour.dll", "ilhook",
                          "nativedetour", "nlua", "keralua", "yamldotnet.dll"):
            c.require(not any(forbidden in name for name in lowered), f"{platform} excludes {forbidden}")
        manifest_path = path.parent / "build-manifest.json"
        c.require(manifest_path.is_file(), f"{platform} build manifest")
        manifest = json.loads(manifest_path.read_text())
        c.require(manifest["sharedClosureSha256"] == SHARED, f"{platform} exact shared closure")
        c.require(manifest["fullAOT"] and manifest["fullTrim"], f"{platform} full AOT/trim")
        c.require(not manifest["useInterpreter"] and not manifest["jit"],
                  f"{platform} no interpreter/JIT")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--fixtures", type=pathlib.Path)
    parser.add_argument("--closure", type=pathlib.Path)
    parser.add_argument("--census", type=pathlib.Path)
    parser.add_argument("--ios-ipa", type=pathlib.Path)
    parser.add_argument("--tvos-ipa", type=pathlib.Path)
    parser.add_argument("--evidence", type=pathlib.Path)
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    required = [
        "apple-everest/modinterop-audit-stage25fb.json",
        "apple-everest/modinterop-acceptance-stage25fb2.json",
        "apple-everest/managed-detour-targets-v1.json",
        "apple-everest/managed-detour-targets-v2.json",
        "apple-everest/canaries/modinterop-acceptance/everest.yaml",
        "apple-everest/canaries/modinterop-acceptance/AchievementHelperAchievements.yaml",
        "apple-everest/runtime/YamlDotNetStaticApi.cs",
        "tools/AppleEverestBuilder/StaticAssetGenerator.cs",
        "scripts/verify-apple-everest-stage25fb2.sh",
    ]
    for relative in required:
        c.require((root / relative).is_file(), f"required file {relative}")

    parent = json.loads(read(root, "apple-everest/modinterop-audit-stage25fb.json"))
    c.require(parent["stage"] == "25F-B" and parent["classification"] == "YELLOW",
              "historical B1 remains YELLOW")
    c.require(parent["selected"]["planSha256"] == PLAN and
              not parent["selected"]["physicalInvocation"], "B1 exact plan/blocker retained")
    audit = json.loads(read(root, "apple-everest/modinterop-acceptance-stage25fb2.json"))
    c.require(audit["stage"] == "25F-B2" and audit["classification"] == "GREEN" and
              audit["parentYellowCommit"] == PARENT,
              "B2 audit parent")
    c.require(audit["provider"]["zipSha256"] == PROVIDER_ZIP and
              audit["provider"]["dllSha256"] == PROVIDER_DLL, "provider exact pins")
    c.require(audit["consumer"]["zipSha256"] == CONSUMER_ZIP and
              audit["consumer"]["dllSha256"] == CONSUMER_DLL, "consumer exact pins")
    census = audit["blockerCensus"]
    c.require((census["conditionHelperHookEvents"], census["achievementHelperHookEvents"],
               census["totalDistinctPairEvents"]) == (29, 5, 34), "complete event census counts")
    c.require((census["previouslyCatalogued"], census["newTargetDescriptors"],
               census["catalogBefore"], census["catalogAfter"]) == (1, 33, 18, 51),
              "exact old/new catalog counts")
    c.require(census["nonHookGenBlockersAfterFixes"] == [], "zero unresolved second-order blockers")
    c.require(len(census["resolvedSecondOrderBlockers"]) == 3 and
              "TextMenu.Items" in census["resolvedSecondOrderBlockers"][0] and
              "two-phase" in census["resolvedSecondOrderBlockers"][2],
              "bounded second-order blockers recorded")
    c.require(audit["modInterop"]["planSha256"] == PLAN and
              [audit["modInterop"][key] for key in ("registrations", "exports", "imports", "resolvedImports")]
              == [3, 7, 4, 4], "unchanged fully resolved ModInterop plan")
    c.require(audit["modInterop"]["importsByName"] ==
              ["ConditionChanged", "WatchConditions", "RemoveCallback", "EvaluateConditionExpression"],
              "four exact real imports")
    acceptance = audit["acceptance"]
    c.require(acceptance["dataOnlyMod"] == "AppleEverestModInteropAcceptance" and
              acceptance["condition"] == "totalDeaths() > 0" and
              acceptance["watcherTarget"] == "celeste-save-data-add-death", "real visible acceptance contract")
    c.require(not acceptance["replacementProviderCode"] and not acceptance["replacementConsumerCode"],
              "data-only acceptance fixture")

    catalog = json.loads(read(root, "apple-everest/managed-detour-targets-v2.json"))
    targets = catalog["targets"]
    ids = [target["id"] for target in targets]
    c.require(catalog["schemaVersion"] == 2 and len(targets) == 51, "catalog v2 exact breadth")
    c.require(len(ids) == len(set(ids)), "catalog descriptor IDs unique")
    c.require(NEW_TARGETS.issubset(set(ids)) and len(NEW_TARGETS) == 33, "exact 33 new descriptors")
    c.require(len({(target["hookNamespace"], target["hookType"], target["eventName"])
                   for target in targets}) == 51, "HookGen event descriptors unique")
    c.require(any(len(target["parameters"]) == 8 for target in targets), "eight-argument target")
    c.require(sum(target["returnType"] == "global::System.Collections.IEnumerator" for target in targets) == 2,
              "two IEnumerator targets")
    c.require(sum(target["returnType"] in ("global::Celeste.AreaModeStats", "global::Celeste.AreaStats")
                  for target in targets) == 2, "clone reference returns")
    c.require({target["eventName"] for target in targets if target["hookType"] == "Commands" and
               target["eventName"].startswith("CmdHearts")} == {"CmdHearts_int", "CmdHearts_int_string"},
              "exact overload descriptors")
    c.require({target["hookType"] for target in targets if target["eventName"] in
               ("Added", "Removed", "SceneEnd")} == {"Player", "Entity"},
              "inherited Player/Entity lifecycle targets remain distinct")
    historical_catalog = json.loads(read(root, "apple-everest/managed-detour-targets-v1.json"))
    c.require(historical_catalog["schemaVersion"] == 1 and
              len(historical_catalog["targets"]) == 18,
              "historical v1 catalog remains available to Stage 25D evidence")

    models = read(root, "tools/AppleEverestBuilder/Models.cs")
    c.require('TransformerVersion = "apple-everest-static-v7"' in models, "transformer v7")
    generator = read(root, "tools/AppleEverestBuilder/ClosureGenerator.cs")
    for token in ("PreparePinnedEverestManagedTargets", "PatchPinnedEverestCompatibility",
                  "GeneratedAppleEverestStaticAssets.cs", "TargetPatchContract", "RewriteTargets"):
        c.require(token.lower() in generator.lower(), f"closure generator contract {token}")
    detour_generator = read(root, "tools/AppleEverestBuilder/ManagedDetourGenerator.cs")
    c.require("ConditionHelper" not in detour_generator and "AchievementHelper" not in detour_generator,
              "no mod-name-specific HookGen transformations")
    managed_catalog = read(root, "tools/AppleEverestBuilder/ManagedDetourCatalog.cs")
    c.require("managed-detour-targets-v2.json" in read(root, "tools/AppleEverestBuilder/AppleEverestBuilder.csproj"),
              "catalog v2 embedded")
    c.require("managed-detour-targets-v1.json" not in
              read(root, "tools/AppleEverestBuilder/AppleEverestBuilder.csproj"),
              "historical v1 catalog is not embedded in the current product")
    c.require("SchemaVersion != 2" in managed_catalog and "parameters" in managed_catalog.lower(),
              "signature-driven catalog parser")
    freezer = read(root, "tools/AppleEverestBuilder/AssemblyFreezer.cs")
    c.require("InstrumentModInteropExports" in freezer and "RecordModInteropExportInvocation" in freezer,
              "generic real export invocation instrumentation")
    c.require("YamlIgnoreAttribute" in freezer and "YamlDotNet" in freezer,
              "bounded precompiled YAML marker erasure")
    static_assets = read(root, "tools/AppleEverestBuilder/StaticAssetGenerator.cs")
    for token in ("List<", "YamlIgnore", "GeneratedAppleEverestStaticAssets", "TryDeserialize"):
        c.require(token in static_assets, f"static content generator {token}")
    c.require("Dictionary<object" not in static_assets, "static content generator does not broaden to arbitrary objects")
    runtime = "\n".join(path.read_text() for path in (root / "apple-everest/runtime").glob("*.cs"))
    for token in ("RecordModInteropRegistration", "RecordModInteropBinding",
                  "RecordModInteropExportInvocation", "ReportStatus"):
        c.require(token in runtime or token in read(root, "tools/AppleEverestBuilder/ModInteropPlanner.cs"),
                  f"runtime acceptance diagnostic {token}")
    for forbidden in ("Assembly.GetTypes", "Delegate.CreateDelegate", "DynamicInvoke", "Reflection.Emit",
                      "DynamicMethod", "Assembly.Load", "AssemblyLoadContext", "NativeDetour", "ILHook",
                      "Process.Start", "FileSystemWatcher", "NLua", "KeraLua"):
        c.require(forbidden not in runtime, f"device runtime excludes {forbidden}")
    runtime_api = read(root, "apple-everest/runtime/EverestStaticApi.cs")
    static_runtime = read(root, "apple-everest/runtime/AppleEverestStaticRuntime.cs")
    c.require("internal void InitializeCurrentInput()" in runtime_api and
              "Celeste.Input.Gamepad == null" in runtime_api,
              "ButtonBinding post-input attachment is explicit and fail-closed")
    c.require("InputBindingInitializer?.Invoke" in static_runtime and
              "InputBindingInitializer(declaration)" in generator,
              "generated typed ButtonBinding lifecycle replaces Everest reflection")
    constructor = runtime_api.split("public ButtonBinding(Buttons buttons, params Keys[] keys)", 1)[1].split(
        "internal void InitializeCurrentInput()", 1)[0]
    c.require("new VirtualButton" not in constructor,
              "module settings construction cannot dereference pre-input Gamepad")
    fixture_yaml = read(root, "apple-everest/canaries/modinterop-acceptance/AchievementHelperAchievements.yaml")
    c.require("totalDeaths() > 0" in fixture_yaml and "AppleEverestModInteropAcceptance" in fixture_yaml,
              "data-only achievement condition")
    fixture_meta = read(root, "apple-everest/canaries/modinterop-acceptance/everest.yaml")
    c.require("AchievementHelper" in fixture_meta and ".dll" not in fixture_meta,
              "acceptance fixture has real helper dependency and no code")

    if args.fixtures:
        verify_fixture(args.fixtures / "ConditionHelper-v1.0.0.zip", PROVIDER_ZIP,
                       {"bin/ConditionHelper.dll": PROVIDER_DLL,
                        "bin/ExpressionParser.dll": EXPRESSION_DLL}, c)
        verify_fixture(args.fixtures / "AchievementHelper-v1.0.5.zip", CONSUMER_ZIP,
                       {"bin/AchievementHelper.dll": CONSUMER_DLL}, c)
    if args.census:
        verify_census(args.census, audit, c)
    if args.closure:
        manifest = json.loads((args.closure / "compatibility-manifest.json").read_text())
        for key, expected in (("sharedClosureSha256", SHARED), ("managedLogicalSha256", MANAGED),
                              ("contentLogicalSha256", CONTENT_CLOSURE),
                              ("hookTransformSha256", HOOK_TRANSFORM), ("modInteropPlanSha256", PLAN)):
            c.require(manifest[key] == expected, f"closure exact {key}")
        c.require(manifest["transformerVersion"] == "apple-everest-static-v7" and
                  manifest["managedDetourCatalogSchema"] == 2 and
                  manifest["managedDetourTargetCount"] == 51, "closure v7/catalog v2")
        c.require([manifest[key] for key in ("modInteropRegistrationCount", "modInteropExportCount",
                                             "modInteropImportCount", "modInteropResolvedImportCount")]
                  == [3, 7, 4, 4], "closure real ModInterop counts")
        c.require(manifest["staticAssetDeserializerTypeCount"] == 1 and
                  manifest["staticAssetFactoryCount"] == 2, "closure static Achievement assets")
        frozen = {entry["assemblyName"]: entry for entry in manifest["frozenAssemblies"]}
        c.require(frozen["ConditionHelper"]["originalSha256"] == PROVIDER_DLL and
                  frozen["AchievementHelper"]["originalSha256"] == CONSUMER_DLL and
                  frozen["ExpressionParser"]["originalSha256"] == EXPRESSION_DLL,
                  "closure consumes exact ordinary release DLLs")
        generated = read(args.closure, "managed/GeneratedAppleEverestModInterop.cs")
        for token in ("ConditionChanged", "WatchConditions", "RemoveCallback",
                      "EvaluateConditionExpression", "ReportStatus"):
            c.require(token in generated, f"generated real binding {token}")
        c.require("First_Apple_Death" in read(args.closure, "managed/GeneratedAppleEverestStaticAssets.cs"),
                  "generated project achievement asset")
    if args.ios_ipa:
        verify_package(args.ios_ipa, "iOS", c)
    if args.tvos_ipa:
        verify_package(args.tvos_ipa, "tvOS", c)
    if args.evidence:
        evidence = json.loads(args.evidence.read_text())
        c.require(evidence["stage"] == "25F-B2" and evidence["classification"] == "GREEN",
                  "physical evidence classification")
        for platform in ("iPhone", "iPadOS15", "AppleTV"):
            c.require(evidence["physical"][platform]["passed"], f"{platform} physical acceptance")
            c.require(evidence["physical"][platform]["visibleAchievement"],
                      f"{platform} visible real achievement")
            c.require(evidence["physical"][platform]["normalSavePersistedAchievement"],
                      f"{platform} module SaveData cold-restore acceptance")

    tests = read(root, "tools/AppleEverestBuilder/tests/Program.cs")
    for token in ("51", "IEnumerator", "reference return", "high arity",
                  "celeste-save-data-add-death", "StaticAssetGenerator"):
        c.require(token.lower() in tests.lower(), f"deterministic test coverage {token}")
    modinterop_tests = read(root, "tools/AppleEverestBuilder/tests/ModInteropTests.cs")
    c.require("RecordModInteropBinding" in modinterop_tests and "four" in modinterop_tests.lower(),
              "ModInterop binding diagnostics test host")

    locks = "\n".join((read(root, "managed/celeste-input-profiles.json"),
                        read(root, "scripts/celeste-managed.py"),
                        read(root, "native/ios-native-output.lock.json"), read(root, "build-tvos.sh")))
    for value in (CONTENT, RAW, PATCHED, STAGE6, IOS_NATIVE, TVOS_NATIVE):
        c.require(value in locks, f"canonical/native lock retained {value}")
    c.require("--mods" not in read(root, "build-ios.sh") and "--mods" not in read(root, "build-tvos.sh"),
              "vanilla builders remain mod-free")
    build = read(root, "scripts/build-apple-everest-canary.sh")
    c.require("UseInterpreter=false" in build and "RunAOTCompilation=true" in build and
              "TrimMode=full" in build, "full-AOT product policy")

    report_path = root / "docs/history/stages/APPLE_EVEREST_MODINTEROP_STAGE25FB2_REPORT.md"
    if report_path.is_file():
        report = report_path.read_text()
        c.require("Stage 25F-B2" in report and PARENT in report and PLAN in report and SHARED in report,
                  "B2 report identity and locks")
        c.require("APPLE_EVEREST_MODINTEROP_STAGE25FB2_REPORT.md" in read(root, "docs/history/README.md"),
                  "history index")

    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC, "immutable iOS RC")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "immutable tvOS RC1")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "immutable tvOS RC2")
    c.require(origin_branch(root, "release/v1.0.0-rc.3") == RC3, "deferred RC3 branch")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 remains untagged")
    c.require(git(root, "rev-parse", "tvos-port") == INTEGRATION and
              origin_branch(root, "tvos-port") == INTEGRATION, "integration branch untouched")

    print(f"PASS: Stage 25F-B2 real ModInterop verifier ({c.count})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
