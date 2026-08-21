#!/usr/bin/env python3
"""Verify the bounded Stage 25G multi-helper compatibility diagnosis."""

from __future__ import annotations

import argparse
import json
import pathlib
import subprocess


BASELINE = "8ed17c42ee3bd6f00fde8516d1ac8da833cf3f3e"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
EVEREST = "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00"
MONOMOD = "dfc30a1506d37fb88a2c2be004f525205f46a24c"

ALLOWED_CHANGES = {
    "apple-everest/multi-helper-candidate-audit-stage25g.json",
    "docs/APPLE_EVEREST_COMPATIBILITY.md",
    "docs/APPLE_EVEREST_STATIC_AOT.md",
    "docs/history/README.md",
    "docs/history/stages/APPLE_EVEREST_MULTI_HELPER_STAGE25G_REPORT.md",
    "scripts/verify-apple-everest-stage25g.py",
    "scripts/verify-apple-everest-stage25g.sh",
}


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, value: bool, message: str) -> None:
        if not value:
            raise SystemExit("FAIL: " + message)
        self.count += 1


def git(root: pathlib.Path, *args: str, check: bool = True) -> str:
    result = subprocess.run(
        ["git", "-C", str(root), *args],
        check=check,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def resolve(root: pathlib.Path, ref: str) -> str:
    return git(root, "rev-parse", f"{ref}^{{commit}}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    audit_path = root / "apple-everest/multi-helper-candidate-audit-stage25g.json"
    report_path = root / "docs/history/stages/APPLE_EVEREST_MULTI_HELPER_STAGE25G_REPORT.md"
    compatibility_path = root / "docs/APPLE_EVEREST_COMPATIBILITY.md"
    architecture_path = root / "docs/APPLE_EVEREST_STATIC_AOT.md"
    history_path = root / "docs/history/README.md"
    for path in (audit_path, report_path, compatibility_path, architecture_path, history_path):
        c.require(path.is_file(), f"required Stage 25G record: {path.relative_to(root)}")

    audit = json.loads(audit_path.read_text())
    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25G", "audit schema/stage")
    c.require(audit["status"] == "YELLOW", "honest YELLOW classification")
    c.require(audit["classification"] == "DEFERRED_ACTIVE_LUA", "bounded blocker class")
    c.require(audit["retrievedAt"] == "2026-08-21", "retrieval date")
    c.require(audit["baseline"]["integrationCommit"] == BASELINE, "integration baseline")
    c.require(audit["baseline"]["everestCommit"] == EVEREST, "Everest pin")
    c.require(audit["baseline"]["monoModCommit"] == MONOMOD, "MonoMod pin")
    c.require(audit["baseline"]["managedDetourTargetCount"] == 51, "51-target baseline")

    metadata = audit["publicMetadata"]
    c.require(len(metadata) == 4, "four public metadata snapshots")
    c.require(metadata[0]["entryCount"] == 6056, "dependency graph entry count")
    c.require(metadata[1]["entryCount"] == 6031, "search database entry count")
    c.require(
        [item["sha256"] for item in metadata] == [
            "ecec2aef46d38525e9c7064129b98ad19ea02743425cec01ed34d6feb3cbf13e",
            "eaf55346084b60bb97e76cc07ae8f874a6434117e1197ca4662d87532cd109d9",
            "b16438e92cbb33eb3e6f2697a09dd4123530afee5759309f05267ffbaea5b602",
            "efa254e5a869838647d8dd9d3f9ed8aaba1df151bf1e9acb4b0509b32f9b0989",
        ],
        "metadata SHA-256 pins",
    )

    discovery = audit["discovery"]
    c.require(discovery["realMapPackagesScreened"] >= 30, "at least 30 real maps screened")
    c.require(discovery["realMapPackagesScreened"] == 53, "complete 53-map screen")
    c.require(discovery["helperPackagesDownloadedAndScanned"] == 99, "helper package breadth")
    c.require(discovery["deepGraphsAudited"] >= 12, "at least 12 deep graphs")
    c.require(discovery["deepGraphsAudited"] == 24, "complete 24-graph audit")
    cohort = discovery["requiredThirtyCohort"]
    c.require(len(cohort) == 30 and len(set(cohort)) == 30, "unique required 30-map cohort")
    c.require({"NightClimb", "obby", "GLACEIR", "The Fall"}.issubset(cohort), "cohort sentinels")
    blockers = discovery["deepGraphPrimaryBlockers"]
    c.require(sum(blockers[key] for key in ("activeIL", "customAudio", "luaOrNative", "apiContentOrUsageBreadth")) == 24,
              "exclusive primary blocker census totals 24")
    c.require(blockers["activeIL"] == 18, "18 active-IL primary blockers")
    c.require(blockers["customAudio"] == 3, "three custom-audio primary blockers")
    c.require(blockers["luaOrNative"] == 1, "one Lua/native primary blocker")
    c.require(blockers["apiContentOrUsageBreadth"] == 2, "two breadth primary blockers")
    c.require(blockers["compatibleWithCurrentNonILProfile"] == 0, "no zero-blocker current-profile graph")

    top = audit["scoring"]["topCandidates"]
    c.require([item["rank"] for item in top] == [1, 2, 3, 4], "reproducible top-candidate order")
    c.require([item["name"] for item in top] == [
        "Noctambule", "UnfortunateWaveCave", "NightClimb", "FUNNIEST MAP NAME EVER"
    ], "top-candidate identities")

    selected = audit["strongestCandidate"]
    c.require(selected["name"] == "Noctambule" and selected["version"] == "0.0.1", "selected root identity")
    c.require(selected["downloadUrl"] == "https://gamebanana.com/mmdl/1541978", "selected root URL")
    c.require(selected["zipSha256"] == "cc48ce4800bea0663ebe80f2a694fc1f9a1fdf503d62fc906f5fef98b3b1a4d6",
              "selected root ZIP pin")
    c.require(selected["map"]["sha256"] == "97d0937ee630bb563d54119a8a9b4bc97a832b9a7200adebb06dc14e977022e1",
              "real map pin")
    c.require(selected["script"]["sha256"] == "6a0fd0ec87d1b41088d140c946257eb5387d4d3c0635fbe7732cb27db61f6fc8",
              "reachable Lua script pin")
    c.require(selected["codeHelperCount"] == 2, "exactly two code helpers")
    c.require(selected["actualUsageIds"] == [
        "ShroomHelper/GradualChangeColorGradeTrigger", "luaCutscenes/luaCutsceneTrigger"
    ], "actual map helper-use IDs")
    c.require(selected["unusedDeclaredHelpers"] == [], "no unused declared helper")
    c.require(selected["resolvedLoadOrder"] == [
        "Everest stable-1.6458.0", "LuaCutscenes 0.2.13", "ShroomHelper 1.2.10", "Noctambule 0.0.1"
    ], "deterministic resolved load order")

    helpers = {helper["name"]: helper for helper in selected["helpers"]}
    c.require(set(helpers) == {"LuaCutscenes", "ShroomHelper"}, "two exact direct helpers")
    c.require(all(helper["relationship"] == "direct" for helper in helpers.values()), "direct dependency relationships")
    c.require(helpers["LuaCutscenes"]["zipSha256"] == "c57913d16596b129275a5dc288bc44fc31a9a67d6f47d7e855efe479dacaaac8",
              "LuaCutscenes ZIP pin")
    c.require(helpers["LuaCutscenes"]["dllSha256"] == "dc697f1adfaa18bdb219df0a3ee569e1a785a452f17c46cd7626a8730440eb57",
              "LuaCutscenes DLL pin")
    c.require(helpers["LuaCutscenes"]["sourceCommit"] == "356dcfb7b0280c1381ed6d47b09a77e44d87bcc3",
              "LuaCutscenes source pin")
    c.require(helpers["ShroomHelper"]["zipSha256"] == "6a2c3eacc68353c0f8bb69bb9b59ca62128e6d3f54ff38a8ff1f282f0cdd97d7",
              "ShroomHelper ZIP pin")
    c.require(helpers["ShroomHelper"]["dllSha256"] == "2428be4659522324b4b426452b17a095a78b857048d6340a0c4720151c08fa1d",
              "ShroomHelper DLL pin")
    c.require(helpers["ShroomHelper"]["sourceCommit"] == "771918ac8e04cca0db56f4cc1b3f96b235678d68",
              "ShroomHelper source pin")
    c.require(all(helper["license"] == "MIT" for helper in helpers.values()), "helper license records")

    mechanisms = selected["mechanismCensus"]
    c.require(not mechanisms["activeIL"] and not mechanisms["activeILHook"], "selected graph avoids active IL/ILHook")
    c.require(mechanisms["lua"], "selected graph contains reachable Lua")
    c.require(not mechanisms["nativeOrPInvoke"], "selected graph has no native/PInvoke")
    c.require(not mechanisms["customFmodBank"], "selected graph has no custom FMOD bank")
    c.require(not mechanisms["directHook"] and not mechanisms["modInterop"], "no selected direct Hook/ModInterop")
    c.require(mechanisms["customTriggerRegistrations"] == 2, "two helper-owned trigger usages")
    c.require(selected["preAotDecision"] == {
        "result": "BLOCKED",
        "diagnostic": "LUA_UNSUPPORTED: reachable Lua cutscene requires NLua/KeraLua, which are intentionally absent from the source-free full-AOT Apple runtime.",
        "unclassifiedBlockers": 0,
        "fullBuildAllowed": False,
    }, "complete classified pre-AOT stop")

    production = audit["productionResult"]
    c.require(not production["runtimeOrBuilderChanges"], "no production/runtime implementation")
    c.require(production["newHookGenTargets"] == 0 and production["targetCatalogCount"] == 51,
              "no target-catalog expansion")
    c.require(production["newReviewedPublicizedMembers"] == 0, "no publicized API expansion")
    c.require(not production["sourceFreeClosureGenerated"] and production["sharedClosureSha256"] is None,
              "no false source-free closure claim")
    c.require(not production["iosProductBuilt"] and not production["tvosProductBuilt"], "no wasteful blocked product builds")
    c.require(all(production[key] == "NOT_RUN_PRE_AOT_BLOCKER" for key in (
        "physicalIphone", "physicalIpad", "physicalAppleTv", "fullAot", "fullTrim", "noInterpreterNoJit"
    )), "physical/AOT gates honestly not run")
    c.require(production["customMapProgression"] == "DEFERRED", "custom progression remains deferred")
    c.require(production["saveAndQuitInDebugMapLane"] == "REMAINS_SUPPRESSED", "debug map Save and Quit remains safe")

    profile = json.loads((root / "apple-everest/profiles/stable-1.6458.0.json").read_text())
    c.require(profile["everest"]["sha256Commit"] == EVEREST, "profile Everest unchanged")
    c.require(profile["dependencies"]["monoModCommit"] == MONOMOD, "profile MonoMod unchanged")
    c.require({"NLua", "KeraLua"}.issubset(profile["deviceRuntimeExcludes"]), "Lua engines remain device-excluded")
    catalog = json.loads((root / "apple-everest/managed-detour-targets-v2.json").read_text())
    c.require(catalog["schemaVersion"] == 2 and len(catalog["targets"]) == 51, "signature target catalog remains v2/51")

    locks = audit["unchangedLocks"]
    c.require(locks["canonicalContent"] == "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46",
              "canonical Content lock")
    c.require(locks["canonicalRaw"] == "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273",
              "canonical raw lock")
    c.require(locks["canonicalPatched"] == "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5",
              "canonical patched lock")
    c.require(locks["canonicalStage6"] == "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9",
              "canonical Stage-6 lock")
    c.require(locks["iosNative"] == "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2",
              "iOS native lock")
    c.require(locks["tvosNative"] == "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc",
              "tvOS native lock")
    c.require(locks["stage25fb2ModInteropPlan"] == "9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318",
              "B2 ModInterop plan lock")
    c.require(audit["nextStageRecommendation"]["highestValueClass"] == "DETERMINISTIC_IL_LOWERING_FEASIBILITY",
              "evidence-led next-stage recommendation")

    report = report_path.read_text()
    compatibility = compatibility_path.read_text()
    architecture = architecture_path.read_text()
    history = history_path.read_text()
    for token in ("PASS — YELLOW", "Noctambule", "LUA_UNSUPPORTED", "18 of 24", "No GitHub Actions"):
        c.require(token in report, f"report token: {token}")
    c.require("Noctambule" in compatibility and "DEFERRED_ACTIVE_LUA" in compatibility,
              "compatibility matrix records selected YELLOW graph")
    c.require("pre-AOT" in architecture and "composed" in architecture and "immutable" in architecture,
              "architecture documents composed-graph boundary")
    c.require("APPLE_EVEREST_MULTI_HELPER_STAGE25G_REPORT.md" in history, "history index entry")

    c.require(resolve(root, "ios-v0.1.1-rc.1") == IOS_RC, "iOS recovery tag immutable")
    c.require(resolve(root, "v1.0.0-rc.1") == RC1, "tvOS RC1 immutable")
    c.require(resolve(root, "v1.0.0-rc.2") == RC2, "tvOS RC2 immutable")
    c.require(resolve(root, "origin/release/v1.0.0-rc.3") == RC3, "RC3 release branch immutable")
    c.require(git(root, "tag", "-l", "v1.0.0-rc.3") == "", "RC3 tag remains absent")

    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".zip", ".dll", ".bank", ".ipa")) for path in tracked),
              "no proprietary/runtime fixture archives tracked")
    production_text = "\n".join(
        (root / path).read_text(errors="ignore")
        for path in tracked
        if (path.startswith("apple-everest/canaries/") or path.startswith("apple-everest/profiles/"))
        and (root / path).is_file()
    )
    c.require("Noctambule" not in production_text, "blocked map not accepted into a product profile")

    changed = set(git(root, "diff", "--name-only", BASELINE + "..HEAD").splitlines())
    changed.update(git(root, "diff", "--name-only").splitlines())
    changed.update(git(root, "ls-files", "--others", "--exclude-standard").splitlines())
    c.require(changed.issubset(ALLOWED_CHANGES), "Stage 25G changes remain diagnostic/docs/verifier only")

    print(f"PASS: Stage 25G verifier ({c.count} checks; YELLOW — active Lua pre-AOT blocker)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
