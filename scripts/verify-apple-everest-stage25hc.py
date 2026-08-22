#!/usr/bin/env python3
"""Verify Stage 25H-C graph-driven ordinary-IL closure evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import subprocess


BASELINE = "8f9fbf64aadc4ff8853132cc35bf9e2e77378df8"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
EVEREST = "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00"
MONOMOD = "dfc30a1506d37fb88a2c2be004f525205f46a24c"
VORTEX_ZIP = "b6280fe2e3c05d355c32a51049854137c41be72d24ec4aec9d997cc7c4394db2"
VORTEX_DLL = "f5a32f02c2699dcdf41bb1af808ead631491571c3d259429761953693bd37f73"
VORTEX_SOURCE = "c071d33bb1cc4f0387ea204834e212bd020f3143aea68c9f4e56b9bfa35def1b"
VORTEX_PLAN = "96726af5fa21c129d1b1bd39746a1f6f903be38e2b53b20b79275eb05d5a3261"
VORTEX_DEVICE = "641532a1d3d32a4c412ef816eb4541072d28bf942bd7bdd610fa300ac493cd17"


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: bool, message: str) -> None:
        if not condition:
            raise SystemExit("FAIL: " + message)
        self.count += 1


def git(root: pathlib.Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(root), *args], check=True,
                          capture_output=True, text=True).stdout.strip()


def resolve(root: pathlib.Path, ref: str) -> str:
    return git(root, "rev-parse", f"{ref}^{{commit}}")


def historical(root: pathlib.Path, path: str) -> bytes:
    return subprocess.run(["git", "-C", str(root), "show", f"{BASELINE}:{path}"],
                          check=True, capture_output=True).stdout


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--evidence-root", type=pathlib.Path,
                        help="optional ignored Stage 25H-C evidence directory")
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    paths = {
        "audit": root / "apple-everest/graph-il-closure-audit-stage25hc.json",
        "registry": root / "tools/AppleEverestBuilder/StaticIlFreeze.cs",
        "resolver": root / "tools/AppleEverestBuilder/ModInteropPlanner.cs",
        "worker": root / "tools/AppleEverestIlWorker/Program.cs",
        "tests": root / "tools/AppleEverestBuilder/tests/Program.cs",
        "fetch": root / "scripts/fetch-apple-everest-stage25hc-fixture.sh",
        "compatibility": root / "docs/APPLE_EVEREST_COMPATIBILITY.md",
        "architecture": root / "docs/APPLE_EVEREST_STATIC_AOT.md",
        "report": root / "docs/history/stages/APPLE_EVEREST_GRAPH_IL_CLOSURE_STAGE25HC_REPORT.md",
        "history": root / "docs/history/README.md",
    }
    for label, path in paths.items():
        c.require(path.is_file(), f"required {label} file")

    audit = json.loads(paths["audit"].read_text())
    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25H-C", "audit schema/stage")
    c.require(audit["status"] == "YELLOW", "honest YELLOW status")
    c.require(audit["baseline"]["integrationCommit"] == BASELINE, "exact baseline")
    c.require(audit["baseline"]["everestCommit"] == EVEREST, "Everest pin")
    c.require(audit["baseline"]["monoModCommit"] == MONOMOD, "MonoMod pin")
    c.require(audit["baseline"]["managedDetourTargetCount"] == 57, "57 HookGen targets")
    c.require(audit["baseline"]["frozenPlanSchema"] == 2, "frozen plan schema remains 2")
    c.require(audit["baseline"]["stage25hbSharedClosure"] ==
              "51bc59ca40c9b0ea2962958ab53bca92b0aa43fa39aee7c3c15512305938e98f",
              "H-B closure baseline")
    c.require(audit["baseline"]["stage25hbModInteropPlan"] ==
              "9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318",
              "B2 ModInterop baseline")

    acquisition = audit["reacquisition"]
    c.require(acquisition["requestedExactGraphs"] == 18 and
              acquisition["reacquiredExactGraphs"] == 18, "all 18 exact graphs reacquired")
    c.require(acquisition["verifiedPackages"] == 44, "44 package hashes verified")
    c.require(acquisition["inaccessibleHistoricalReleases"] == [], "no missing historical release")
    c.require(acquisition["archiveTraversalRejected"] and not acquisition["originalPackagesModified"],
              "safe immutable acquisition")

    inventory = audit["binaryInventory"]
    for key, value in {
        "helperDllsInspected": 26,
        "hookGenIlRegistrationOperations": 720,
        "hookGenIlAdditions": 360,
        "directIlHookConstructionSites": 126,
        "configuredHookRelatedSites": 91,
        "emitDelegateSites": 471,
    }.items():
        c.require(inventory[key] == value, f"binary inventory {key}")
    priority = inventory["highPriorityGraphUnion"]
    c.require(priority["hookGenIlRegistrationOperations"] == 270, "priority IL registrations")
    c.require(priority["directIlHookConstructionSites"] == 53, "priority direct ILHook sites")
    c.require(priority["configuredHookRelatedSites"] == 50, "priority configured sites")
    c.require(priority["emitDelegateSites"] == 226, "priority EmitDelegate sites")
    taxonomy = priority["emitDelegateTaxonomy"]
    c.require(sum(taxonomy.values()) == 226, "priority taxonomy closes")
    c.require(taxonomy["A_STATIC_METHOD"] == 161 and
              taxonomy["B_COMPILER_STATELESS_SINGLETON"] == 52, "dominant accepted delegate classes")
    c.require(taxonomy["E_MODULE_SINGLETON_CAPTURE"] == 4 and
              taxonomy["H_TRANSIENT_MANIPULATOR_LOCAL_CAPTURE"] == 2 and
              taxonomy["J_OTHER_OR_UNKNOWN"] == 7, "deferred captures kept explicit")

    selection = audit["selection"]
    c.require(selection["path"] == "A" and
              selection["majorCompatibilityClass"] == "STATIC_IL_EVENT_FREEZE",
              "ordinary event breadth selected")
    c.require(selection["newArchitectureClass"] is False, "no false new-class claim")
    c.require(selection["selectedGraph"] == "Space Trip", "selected graph")
    c.require(selection["closestOverallGraph"] == "LittleEpic's Precision Challenge",
              "closest graph kept distinct from selected proof graph")
    c.require(selection["exactGraphsContainingOrdinaryIlEventBreadth"] == 15,
              "ordinary event graph payoff")
    c.require(selection["exactGraphsMateriallyAdvanced"] == ["Space Trip"],
              "one exact graph materially advanced")
    c.require(selection["exactGraphsWithNoRemainingIlBlocker"] == [] and
              selection["exactGraphsWithZeroOverallBlockers"] == [], "no false map unlock")

    fixture = audit["selectedFixture"]
    c.require(fixture["name"] == "VortexHelper" and fixture["version"] == "1.2.19",
              "fixture identity")
    c.require(fixture["zipSha256"] == VORTEX_ZIP and fixture["dllSha256"] == VORTEX_DLL and
              fixture["logicalSourceSha256"] == VORTEX_SOURCE, "fixture exact locks")
    c.require(fixture["license"] == "MIT" and len(fixture["licenseSha256"]) == 64,
              "fixture license provenance")
    c.require(fixture["distributedDllAuthoritative"] and not fixture["sourceRequiredAtBuildTime"],
              "source-free authority")
    c.require(fixture["selectedEmitDelegateTaxonomy"] ==
              {"A_STATIC_METHOD": 1, "B_COMPILER_STATELESS_SINGLETON": 2, "unknown": 0},
              "selected site taxonomy")
    c.require(fixture["config"] is None and not fixture["directIlHook"],
              "selected fixture is ordinary unconfigured IL")

    plan = audit["productionPlan"]
    c.require(plan["schemaVersion"] == 2 and plan["worker"] == "apple-everest-static-il-worker-v2",
              "plan v2 retained")
    c.require(plan["planSha256"] == VORTEX_PLAN, "exact Vortex plan")
    c.require(len(plan["transforms"]) == 2, "two exact transforms")
    for transform in plan["transforms"]:
        c.require(transform["mechanism"] == "HookGen IL event", "ordinary mechanism")
        c.require(transform["registrationOrdinal"] == 0, "single-target ordinal")
        c.require(all(len(transform[key]) == 64 for key in
                      ("beforeSha256", "afterSha256", "diffSha256")), "transform hashes")
        c.require(transform["finalForbiddenReferences"] == [], "no final forbidden references")
    determinism = plan["threeRunDeterminism"]
    c.require(determinism["independentRuns"] == 3 and determinism["allIdentical"],
              "three-run determinism")
    c.require(all(len(value) == 64 for key, value in determinism.items()
                  if key.endswith("Sha256")), "determinism SHA shapes")
    device = plan["deviceRewriteEvidence"]
    c.require(device["independentRuns"] == 2 and device["outputSha256"] == VORTEX_DEVICE,
              "real device rewrite determinism")
    c.require(device["monoCecilReferenceRemoved"] and
              device["retainedOrdinaryOnRegistrations"] == 8,
              "device rewrite removes host IL and retains ordinary hooks")
    c.require("MonoMod.Utils" in device["remainingAssemblyReferences"] and
              "DynamicData" in device["monoModUtilsDisposition"],
              "device rewrite preserves honest unrelated blocker")
    c.require("No Cecil" in plan["deviceRuntime"] and "ILHook backend" in plan["deviceRuntime"],
              "forbidden device runtime excluded")

    graphs = audit["graphs"]
    expected = {
        "NightClimb", "obby", "MountBaker", "void", "Astraeus",
        "LittleEpic's Precision Challenge", "Wholesome Theo Gameplay", "QLetterAurora",
        "TeraBlast", "GLACEIR", "The Fall", "Lightning Strike",
        "Coffee Pot Crystal Heaven", "StupidLava", "Blue Ice", "sillymap1",
        "Space Trip", "8bb1b",
    }
    c.require(len(graphs) == 18 and {item["graph"] for item in graphs} == expected,
              "all 18 graph records exact")
    c.require(sum(bool(item["selected"]) for item in graphs) == 1, "one selected graph")
    c.require(all(item["packageGraph"] and item["helpers"] and item["nonIlBlockers"]
                  for item in graphs), "package/helper/non-IL census")
    high_priority = {
        "LittleEpic's Precision Challenge", "NightClimb", "QLetterAurora", "TeraBlast",
        "The Fall", "Coffee Pot Crystal Heaven", "StupidLava", "Blue Ice",
        "Space Trip", "8bb1b",
    }
    c.require(high_priority.issubset({item["graph"] for item in graphs}),
              "mandatory high-priority graph set")
    space = next(item for item in graphs if item["graph"] == "Space Trip")
    c.require(space["distanceBefore"]["majorIlClasses"] == 2 and
              space["distanceAfter"]["majorIlClasses"] == 1, "selected graph IL distance reduced")
    c.require("VortexHelper DynamicData/DynData" in space["nonIlBlockers"] and
              "custom FMOD bank" in space["nonIlBlockers"], "unrelated Space Trip blockers explicit")
    little = next(item for item in graphs if item["graph"] == "LittleEpic's Precision Challenge")
    c.require(little["distance"]["majorIlClasses"] == 1 and
              little["emitDelegate"].endswith("unknown=0"), "closest graph exact distance")

    gate = audit["productGate"]
    c.require(gate["selectedHelperUnclassifiedBlockers"] == 0 and
              gate["selectedGraphUnclassifiedBlockers"] == 0, "zero unclassified")
    c.require(gate["selectedGraphOverallBlockers"] > 0 and not gate["fullMapBuildAllowed"],
              "pre-AOT gate honestly blocks map")
    c.require(gate["sharedClosureSha256"] is None and
              gate["physicalIphone"] == "NOT_RUN_PRE_AOT_GATE" and
              gate["physicalAppleTv"] == "NOT_RUN_PRE_AOT_GATE", "no invented product evidence")

    registry = paths["registry"].read_text()
    for token in (VORTEX_ZIP, VORTEX_DLL, VORTEX_SOURCE,
                  "VortexHelper:Player.NormalUpdate:Player_FrictionNormalUpdate",
                  "VortexHelper:Player.WallJumpCheck:Player_WallJumpCheck",
                  "RewriteVortexHelper", "FloorBooster/Hooks", "PurpleBooster/Hooks"):
        c.require(token in registry, f"registry contract {token}")
    c.require("registered VortexHelper metadata drifted" in registry, "metadata fail-closed")
    c.require("registered fixture contains an unreviewed IL event subscription" in registry,
              "unexpected event rejected")
    c.require("registered fixture unexpectedly uses direct ILHook" in registry,
              "direct ILHook not smuggled into Path A")
    c.require("MonoMod.Utils intentionally remains blocked" in registry,
              "DynamicData blocker not hidden")
    c.require("AssemblyResolutionException" in paths["resolver"].read_text(),
              "bounded metadata resolver accepts absent unrelated dependency")
    worker = paths["worker"].read_text()
    for token in ("target baseline mismatch", "intermediate chain mismatch",
                  "transformed IL lock mismatch", "forbidden final IL reference",
                  "capturing EmitDelegate closure is deferred"):
        c.require(token in worker, f"worker fail-closed token {token}")
    tests = paths["tests"].read_text()
    c.require("Stage 25H-C exact VortexHelper identity" in tests and
              "Stage 25H-C removes the exact nested runtime IL registrations" in tests,
              "Stage 25H-C deterministic tests")
    fetch = paths["fetch"].read_text()
    c.require(VORTEX_ZIP in fetch and "https://gamebanana.com/mmdl/1368600" in fetch,
              "clean-clone exact fixture fetch")

    docs = {key: path.read_text() for key, path in paths.items()
            if path.suffix in {".md"}}
    for token in ("Stage 25H-C", "VortexHelper", "Space Trip", "YELLOW"):
        c.require(token in docs["report"], f"report token {token}")
    c.require("APPLE_EVEREST_GRAPH_IL_CLOSURE_STAGE25HC_REPORT.md" in docs["history"],
              "history index")
    c.require("STATIC_IL_EVENT_FREEZE" in docs["architecture"] and
              "entity-local" in docs["architecture"], "architecture docs")
    c.require("VortexHelper" in docs["compatibility"] and
              "configured ordering" in docs["compatibility"], "compatibility docs")

    for path in (
        "apple-everest/multi-helper-candidate-audit-stage25g.json",
        "apple-everest/il-compatibility-audit-stage25h.json",
        "apple-everest/il-compatibility-audit-stage25hb.json",
        "docs/history/stages/APPLE_EVEREST_MULTI_HELPER_STAGE25G_REPORT.md",
        "docs/history/stages/APPLE_EVEREST_STATIC_IL_STAGE25H_REPORT.md",
        "docs/history/stages/APPLE_EVEREST_STATIC_IL_COMPOSITION_STAGE25HB_REPORT.md",
    ):
        c.require((root / path).read_bytes() == historical(root, path),
                  f"historical bytes unchanged: {path}")

    locks = audit["unchangedLocks"]
    expected_locks = {
        "dashToggleFrozenDll": "79d06fd9428e33613b81f089693651f03c6445e0a6f22368902bb3fcb49e904f",
        "vanillaIosTree": "2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357",
        "canonicalContent": "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46",
        "canonicalRaw": "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273",
        "canonicalPatched": "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5",
        "canonicalStage6": "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9",
        "iosNative": "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2",
        "tvosNative": "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc",
    }
    c.require(locks == expected_locks, "canonical/native/H-A locks unchanged")

    profile = json.loads((root / "apple-everest/profiles/stable-1.6458.0.json").read_text())
    c.require(profile["everest"]["sha256Commit"] == EVEREST, "profile Everest pin")
    c.require(profile["dependencies"]["monoModCommit"] == MONOMOD, "profile MonoMod pin")
    c.require(resolve(root, "ios-v0.1.1-rc.1") == IOS_RC, "iOS RC immutable")
    c.require(resolve(root, "v1.0.0-rc.1") == RC1, "tvOS RC1 immutable")
    c.require(resolve(root, "v1.0.0-rc.2") == RC2, "tvOS RC2 immutable")
    c.require(resolve(root, "origin/release/v1.0.0-rc.3") == RC3, "RC3 branch immutable")
    c.require(git(root, "tag", "-l", "v1.0.0-rc.3") == "", "RC3 tag absent")
    c.require(resolve(root, BASELINE) == BASELINE, "baseline commit exists")

    if args.evidence_root:
        evidence = args.evidence_root.resolve()
        reacquisition = json.loads((evidence / "fresh-reacquisition.json").read_text())
        c.require(reacquisition["count"] == 44, "ignored evidence package count")
        c.require(all(len(item["sha256"]) == 64 for item in reacquisition["packages"]),
                  "ignored evidence package hashes")
        probe = evidence / "vortex-probe"
        for stem, expected in {
            "normal-run1.dll": "1c649a54c053100cc0ddd12888ffdf042b69ffbf53932ab67d66f8bde17d2ca6",
            "normal-run2.dll": "1c649a54c053100cc0ddd12888ffdf042b69ffbf53932ab67d66f8bde17d2ca6",
            "normal-run3.dll": "1c649a54c053100cc0ddd12888ffdf042b69ffbf53932ab67d66f8bde17d2ca6",
            "wall-run1.dll": "3a387526cdec914c5bfcb4f4c84e50fade6895c4f01c3b149c7d7c595efa39e0",
            "wall-run2.dll": "3a387526cdec914c5bfcb4f4c84e50fade6895c4f01c3b149c7d7c595efa39e0",
            "wall-run3.dll": "3a387526cdec914c5bfcb4f4c84e50fade6895c4f01c3b149c7d7c595efa39e0",
        }.items():
            c.require(hashlib.sha256((probe / stem).read_bytes()).hexdigest() == expected,
                      f"three-run evidence {stem}")
        rewritten = evidence / "reflection-probe/VortexHelper.device-rewritten.dll"
        c.require(hashlib.sha256(rewritten.read_bytes()).hexdigest() == VORTEX_DEVICE,
                  "real source-free device rewrite evidence")

    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".zip", ".dll", ".bank", ".ipa", ".app"))
                      for path in tracked), "no third-party/product bytes tracked")
    public_text = "\n".join((root / path).read_text(errors="ignore") for path in tracked
                              if (root / path).is_file() and
                              path.startswith(("apple-everest/", "docs/", "scripts/")))
    private_home = "/" + "Users" + "/" + "harrymcneill"
    c.require(private_home + "/" not in public_text, "no private absolute path")

    print(f"PASS: Stage 25H-C verifier ({c.count} checks; YELLOW, graph IL closure fail-closed)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
