#!/usr/bin/env python3
"""Verify the bounded Stage 25H-A build-time HookGen IL freeze."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import subprocess


INTEGRATION = "8ed17c42ee3bd6f00fde8516d1ac8da833cf3f3e"
PARENT = "9ffc1460d15bfe69074f55f6f3187d3cb2156365"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
EVEREST = "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00"
MONOMOD = "dfc30a1506d37fb88a2c2be004f525205f46a24c"
ZIP = "677e8fbd067340d7b3133cc908e4ecafc0f5deab2c38b7eeb79a62eb5f61d523"
DLL = "531eaa8a719cb81cc84adf2b9e930dcb3abae73c60406b8f44b823c9c4b4a083"
FROZEN_DLL = "79d06fd9428e33613b81f089693651f03c6445e0a6f22368902bb3fcb49e904f"
SOURCE = "9b140684c2ee80ddae3c9ef032de0c767a67530c"
SOURCE_LOGICAL = "a26ac163b4184cc0daccfd99f4ef11aeeeef7a2858b7d84938beb0dc6afd5d09"
PLAN = "ce86b2eaeb0abfeab48254ffa202daa6e23d154b5b623c07dd5742c2fd465f87"
SHARED = "cbf1fbd55aeb90495ceb99901926df056c16ead5d94e41daa3af9a11a9c1ac8f"
MANAGED = "bdc2fb054bf23469f34148c85e3f787ef5c3b1203bc7caa088e11c5e0f7fa8f3"
CONTENT = "ee432aeff74a65e77b5fb131afcbd51d71b02e311d739faf9c01f82bf8939135"
CREATE_BEFORE = "60e4d178d19f1e70f21e9e88243354830db71155aa33de6b7b1f9b1e6abf6a94"
CREATE_AFTER = "f03103f1f63b71351054d68b8fc6ed52a06dc1e690b616cf993885c93b3bd0d8"
CREATE_DIFF = "da499a5a57b7ecb09f1d15ec20b61caa445252b8f631789d7723cbc02dbc81a9"
ADD_BEFORE = "0a10b7404b2394238548b3e32d89c8f15298c32f293a2bc23153e0c1a8ebd051"
ADD_AFTER = "91f955361cc5df3c643c8bd09b9cd211cf7ee09d6817ae2175d6569e2f9ec0aa"
ADD_DIFF = "17bf0d8a9050ef5f8372e08dc41367a800344a877a009096e20787dd64bb79e7"
CANONICAL = {
    "Content": "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46",
    "Raw": "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273",
    "Patched": "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5",
    "Stage6": "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9",
    "iOSNative": "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2",
    "tvOSNative": "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc",
    "VanillaIOS": "2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357",
    "ModInterop": "9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318",
}


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, value: bool, message: str) -> None:
        if not value:
            raise SystemExit("FAIL: " + message)
        self.count += 1


def git(root: pathlib.Path, *args: str, check: bool = True) -> str:
    result = subprocess.run(["git", "-C", str(root), *args], check=check,
                            capture_output=True, text=True)
    return result.stdout.strip()


def resolve(root: pathlib.Path, ref: str) -> str:
    return git(root, "rev-parse", f"{ref}^{{commit}}")


def historical_bytes(root: pathlib.Path, path: str) -> bytes:
    return subprocess.run(["git", "-C", str(root), "show", f"{PARENT}:{path}"],
                          check=True, capture_output=True).stdout


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    files = {
        "audit": root / "apple-everest/il-compatibility-audit-stage25h.json",
        "matrix": root / "apple-everest/il-conformance-stage25h.json",
        "registry": root / "tools/AppleEverestBuilder/StaticIlFreeze.cs",
        "worker": root / "tools/AppleEverestIlWorker/Program.cs",
        "workerProject": root / "tools/AppleEverestIlWorker/AppleEverestIlWorker.csproj",
        "models": root / "tools/AppleEverestBuilder/Models.cs",
        "closure": root / "tools/AppleEverestBuilder/ClosureGenerator.cs",
        "freezer": root / "tools/AppleEverestBuilder/AssemblyFreezer.cs",
        "build": root / "scripts/build-apple-everest-canary.sh",
        "fetch": root / "scripts/fetch-apple-everest-stage25h-fixtures.sh",
        "iltest": root / "scripts/test-apple-everest-il-freeze.sh",
        "map": root / "apple-everest/canaries/static-il-content/Content/Maps/AppleEverest/StaticIl.xml",
        "compat": root / "docs/APPLE_EVEREST_COMPATIBILITY.md",
        "architecture": root / "docs/APPLE_EVEREST_STATIC_AOT.md",
        "report": root / "docs/history/stages/APPLE_EVEREST_STATIC_IL_STAGE25H_REPORT.md",
        "history": root / "docs/history/README.md",
    }
    for name, path in files.items():
        c.require(path.is_file(), f"required {name} file")

    audit = json.loads(files["audit"].read_text())
    matrix = json.loads(files["matrix"].read_text())
    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25H-A", "audit schema/stage")
    c.require(audit["status"] == "GREEN", "GREEN classification")
    c.require(audit["classification"] == "STATIC_IL_EVENT_FREEZE", "bounded class")
    c.require(audit["baseline"]["stage25gParent"] == PARENT, "Stage 25G parent")
    c.require(audit["baseline"]["acceptedIntegration"] == INTEGRATION, "B2 integration")
    c.require(audit["baseline"]["everest"] == EVEREST, "Everest pin")
    c.require(audit["baseline"]["monoMod"] == MONOMOD, "MonoMod pin")

    methodology = audit["methodology"]
    c.require(methodology["sourceAvailableHelpers"] >= 20, "at least twenty real helpers")
    c.require(methodology["sourceAvailableHelpers"] == 27, "exact helper audit count")
    c.require(methodology["hookGenIlAddSites"] == 138, "HookGen add-site count")
    c.require(methodology["hookGenIlRemoveSites"] == 132, "HookGen remove-site count")
    c.require(methodology["distinctHookGenSubscriptions"] == 135, "distinct subscription count")
    c.require(methodology["distinctTargetIdentities"] == 115, "distinct target count")
    c.require(methodology["targetIdentitiesUsedByMultipleSubscriptions"] == 23,
              "multiple-subscription target count")
    c.require(methodology["directIlHookConstructions"] == 45, "direct ILHook count")
    c.require(methodology["detourConfigSourceOccurrences"] == 4, "DetourConfig count")
    c.require(methodology["detourContextSourceOccurrences"] == 4, "DetourContext count")
    c.require(len(audit["candidates"]) == 27, "27 pinned candidate commits")
    c.require(len({item[0] for item in audit["candidates"]}) == 27, "unique candidate names")
    c.require(all(len(item[1]) == 40 for item in audit["candidates"]), "candidate commit pins")
    c.require(sum(item[2] for item in audit["candidates"]) == 138, "candidate IL adds total")
    c.require(sum(item[3] for item in audit["candidates"]) == 45, "candidate direct ILHooks total")

    shapes = audit["shapeSourceOccurrences"]
    expected_shapes = {
        "cursorPatternOrMatch": 699, "replaceOperand": 5, "replaceOpcode": 30,
        "replaceConstant": 52, "insertOrdinaryInstruction": 321, "insertCall": 27,
        "removeOneInstruction": 72, "removeRange": 3, "branchOrLabel": 90,
        "localVariableOrAccess": 137, "exceptionHandler": 6, "switchTable": 0,
        "emitDelegate": 201, "lambdaNearEmitDelegateCandidates": 38,
        "dynamicData": 73, "genericConstruction": 8,
        "reflectionSelectedMember": 267, "dynamicReferenceManager": 0,
    }
    for key, value in expected_shapes.items():
        c.require(shapes[key] == value, f"shape count {key}")

    link = audit["stage25gLinkage"]
    c.require(link["deepGraphs"] == 24 and link["ilPrimaryGraphs"] == 18, "Stage 25G cohort retained")
    c.require(link["directlyUnlockedByExactDashToggleRegistry"] == 0, "no false graph relabel")
    c.require("No historical graph is relabelled" in link["assessment"], "honest ecosystem payoff")

    fixture = audit["selectedFixture"]
    c.require(fixture["name"] == "DashToggleHelper" and fixture["version"] == "1.1.0", "fixture identity")
    c.require(fixture["downloadUrl"] == "https://gamebanana.com/mmdl/1460721", "fixture URL")
    c.require(fixture["zipSha256"] == ZIP, "fixture ZIP pin")
    c.require(fixture["dllSha256"] == DLL, "fixture DLL pin")
    c.require(fixture["frozenDllSha256"] == FROZEN_DLL, "frozen DLL pin")
    c.require(fixture["sourceCommit"] == SOURCE, "source commit")
    c.require(fixture["sourceLogicalSha256"] == SOURCE_LOGICAL, "source logical pin")
    c.require(fixture["license"] == "MIT", "fixture license")
    c.require(fixture["authoritativeInput"] == "ordinary public release ZIP and precompiled DLL",
              "binary authority")
    c.require(not fixture["sourceRequiredForProduction"], "source-free production")
    c.require(fixture["ordinaryOnHookCount"] == 6, "six accompanying On hooks")
    c.require(not fixture["directIlHook"], "fixture avoids direct ILHook")
    c.require(fixture["emitDelegate"] and not fixture["capturedDelegate"], "noncapturing EmitDelegate")
    c.require(not fixture["dynamicReferenceInFinalIl"], "no dynamic final IL")

    transforms = audit["acceptedTransforms"]
    c.require(len(transforms) == 2, "two exact real transformations")
    first, second = transforms
    c.require(first["event"] == "IL.Celeste.CrystalStaticSpinner.CreateSprites", "CreateSprites event")
    c.require(first["beforeSha256"] == CREATE_BEFORE, "CreateSprites baseline")
    c.require(first["afterSha256"] == CREATE_AFTER, "CreateSprites after")
    c.require(first["normalizedDiffSha256"] == CREATE_DIFF, "CreateSprites diff")
    c.require(first["manipulator"].endswith("::CreateSpritesOverride"), "CreateSprites manipulator")
    c.require(first["injectedMethods"] == ["DTSpinnerImage", "DTSpinnerColor", "isDTSpinner"],
              "CreateSprites injected methods")
    c.require(second["event"] == "IL.Celeste.CrystalStaticSpinner.AddSprite", "AddSprite event")
    c.require(second["beforeSha256"] == ADD_BEFORE, "AddSprite baseline")
    c.require(second["afterSha256"] == ADD_AFTER, "AddSprite after")
    c.require(second["normalizedDiffSha256"] == ADD_DIFF, "AddSprite diff")
    c.require(second["manipulator"].endswith("::AddSpriteOverride"), "AddSprite manipulator")
    c.require(second["injectedMethods"] == ["DTSpinnerImage", "tintIfDTSpinner"],
              "AddSprite injected methods")
    c.require(audit["frozenIlPlanSha256"] == PLAN, "frozen plan hash")

    boundary = audit["productionBoundary"]
    c.require(boundary["oneManipulatorPerTarget"], "one manipulator per target")
    c.require(boundary["liveDisable"] is False, "no fake live disable")
    c.require(boundary["directIlHook"] == "deferred", "direct ILHook deferred")
    c.require(boundary["configuredIlHook"] == "deferred", "configured ILHook deferred")
    c.require(boundary["multipleManipulatorsPerTarget"] == "deferred", "multiple manipulators deferred")
    c.require(boundary["capturedOrDynamicEmitDelegate"] == "deferred", "dynamic delegates deferred")
    for key in ("deviceMonoModCil", "deviceMonoCecil", "deviceIlHook", "deviceDynamicCode"):
        c.require(boundary[key] is False, f"device exclusion {key}")

    c.require(matrix["schemaVersion"] == 1 and matrix["pinnedMonoMod"] == MONOMOD, "matrix pin")
    c.require(len(matrix["hostCanaries"]) >= 6, "host conformance breadth")
    for token in ("replace constant", "insert direct static call", "insert branch and label",
                  "alter return value", "create and round-trip local", "remove instruction range"):
        c.require(token in matrix["hostCanaries"], f"host conformance {token}")
    c.require("same-target HookGen On.* wrapper whose orig calls frozen IL body" in
              matrix["productionRealFixture"], "same-target composition")
    for token in ("target fingerprint mismatch", "second manipulator on a target", "direct ILHook",
                  "throwing or cursor-miss manipulator", "forbidden dynamic-reference output"):
        c.require(token in matrix["failClosedCases"], f"fail-closed case {token}")

    registry = files["registry"].read_text()
    worker = files["worker"].read_text()
    closure = files["closure"].read_text()
    freezer = files["freezer"].read_text()
    build = files["build"].read_text()
    registry_contract = registry + files["models"].read_text() + files["closure"].read_text() + \
        (root / "tools/AppleEverestBuilder/Program.cs").read_text()
    for token in (ZIP, DLL, SOURCE_LOGICAL, SOURCE, "PlanSha256", "ValidateRegistrations",
                  "registered fixture unexpectedly uses direct ILHook", "runtimeUnload"):
        c.require(token in registry_contract, f"registry token {token}")
    c.require("context.Invoke(manipulator)" in worker, "real manipulator invocation")
    c.require("target baseline mismatch" in worker and "transformed IL lock mismatch" in worker,
              "body lock failures")
    c.require("dangling branch target" in worker and "dangling exception-handler endpoint" in worker,
              "IL structure validation")
    for token in ("MonoMod.Cil", "Mono.Cecil", "ILHook", "DynamicMethod", "Reflection.Emit",
                  "DynamicReferenceManager", "Assembly::Load"):
        c.require(token in worker, f"forbidden final reference {token}")
    c.require("APPLE_EVEREST_STATIC_IL_ALREADY_FROZEN" in worker, "exact incremental idempotence")
    c.require("AppleEverestStaticIl.targets" in closure and ".AppleEverestStaticIlHost" in closure,
              "host transform closure")
    c.require("StaticIlFreeze.RewriteDeviceAssembly" in freezer, "device subscription rewrite")
    c.require("AppleEverestIlWorker" in build and "host-only static-IL transformation material" in build,
              "product host-library exclusion")
    c.require("MonoMod.Utils.csproj" in build, "pinned MonoMod host build")
    c.require(ZIP in build and "apple-everest/canaries/static-il-content" in build,
              "exact physical Canary mounts project-owned behavior room")

    catalog = json.loads((root / "apple-everest/managed-detour-targets-v2.json").read_text())
    c.require(catalog["schemaVersion"] == 2 and len(catalog["targets"]) == 56, "catalog v2/56")
    ids = {target["id"] for target in catalog["targets"]}
    for target in ("celeste-cassette-block-find-in-group", "celeste-cassette-block-check-for-same",
                   "celeste-cassette-block-set-image", "celeste-cassette-block-shift-size",
                   "celeste-crystal-static-spinner-create-sprites"):
        c.require(target in ids, f"reviewed companion On target {target}")

    map_text = files["map"].read_text()
    c.require("DashToggleHelper/DashToggleStaticSpinner" in map_text, "real spinner canary")
    c.require("DashToggleHelper/DashToggleBlock" in map_text, "real block canary")
    c.require(not any(path.suffix.lower() in {".dll", ".zip"} for path in
                      (root / "apple-everest/canaries/static-il-content").rglob("*")),
              "no tracked fixture binary in content canary")
    fetch = files["fetch"].read_text()
    c.require("https://gamebanana.com/mmdl/1460721" in fetch and ZIP in fetch, "exact fixture fetcher")

    compatibility = files["compat"].read_text()
    architecture = files["architecture"].read_text()
    report = files["report"].read_text()
    history = files["history"].read_text()
    for token in ("STATIC_IL_EVENT_FREEZE", "Dash Toggle Helper", "immutable-active",
                  "Multiple manipulators", "direct or configured `ILHook`"):
        c.require(token in compatibility, f"compatibility documentation {token}")
    for token in ("Build-time frozen HookGen IL", "isolated Mac host", "full trim + full AOT",
                  "same-target `On.*`", "no `DynamicReferenceManager`"):
        c.require(token in architecture, f"architecture documentation {token}")
    for token in ("PASS — GREEN", "DashToggleHelper", PLAN, SHARED, "No GitHub Actions",
                  "iPadOS 15.8.8", "immutable-active", "Stage 25G2"):
        c.require(token in report, f"report token {token}")
    c.require("APPLE_EVEREST_STATIC_IL_STAGE25H_REPORT.md" in history, "history index")

    old_audit = "apple-everest/multi-helper-candidate-audit-stage25g.json"
    old_report = "docs/history/stages/APPLE_EVEREST_MULTI_HELPER_STAGE25G_REPORT.md"
    c.require((root / old_audit).read_bytes() == historical_bytes(root, old_audit), "Stage 25G audit byte-identical")
    c.require((root / old_report).read_bytes() == historical_bytes(root, old_report), "Stage 25G report byte-identical")

    for name, value in CANONICAL.items():
        c.require(value in report, f"unchanged lock {name}")
    profile = json.loads((root / "apple-everest/profiles/stable-1.6458.0.json").read_text())
    c.require(profile["everest"]["sha256Commit"] == EVEREST, "profile Everest unchanged")
    c.require(profile["dependencies"]["monoModCommit"] == MONOMOD, "profile MonoMod unchanged")
    excludes = set(profile["deviceRuntimeExcludes"])
    for name in ("MonoMod.RuntimeDetour", "MonoMod.Utils.HookGen", "NLua", "KeraLua"):
        c.require(name in excludes, f"runtime exclude {name}")

    c.require(resolve(root, "ios-v0.1.1-rc.1") == IOS_RC, "iOS RC immutable")
    c.require(resolve(root, "v1.0.0-rc.1") == RC1, "tvOS RC1 immutable")
    c.require(resolve(root, "v1.0.0-rc.2") == RC2, "tvOS RC2 immutable")
    c.require(resolve(root, "origin/release/v1.0.0-rc.3") == RC3, "RC3 branch immutable")
    c.require(git(root, "tag", "-l", "v1.0.0-rc.3") == "", "RC3 tag absent")
    c.require(resolve(root, PARENT) == PARENT, "exact Stage 25G parent exists")
    ancestor = subprocess.run(["git", "-C", str(root), "merge-base", "--is-ancestor", PARENT, "HEAD"])
    c.require(ancestor.returncode == 0, "Stage 25H descends from Stage 25G")

    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".zip", ".dll", ".bank", ".ipa")) for path in tracked),
              "no proprietary/runtime fixture archives tracked")
    tracked_text = "\n".join((root / path).read_text(errors="ignore") for path in tracked
                               if (root / path).is_file() and path.startswith(("apple-everest/", "docs/")))
    c.require("/Users/harrymcneill" not in tracked_text, "no private absolute path")
    c.require("DynamicMethodDefinition.dll" not in tracked, "no dynamic method runtime payload")

    print(f"PASS: Stage 25H-A verifier ({c.count} checks; GREEN — bounded static IL freeze)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
