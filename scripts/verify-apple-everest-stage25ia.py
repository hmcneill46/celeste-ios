#!/usr/bin/env python3
"""Verify Stage 25I-A LittleEpic graph evidence and the bounded YELLOW gate."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import subprocess


BASELINE = "236d5fc957fb02d283a29ae4f55ef9753be042c2"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
EVEREST = "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00"
MONOMOD = "dfc30a1506d37fb88a2c2be004f525205f46a24c"
ROOT_ZIP = "ca57d295f446affdd8eb9b56ffe58f98cc652e9ec242ff07b224877d0067b384"
CHRONO_ZIP = "af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18"
CHRONO_DLL = "214b26a5d7e3f17e93d3c388a3a6066dd137f9ce09b6203a4e1a482ba342a2dc"
DJ_ZIP = "95ab02d657213031b70c3079738b21be399e567ca8a32b1fe8e8effc0778eedb"
DJ_DLL = "0d73202b5e16f26a4c8a286dd6ad600c1601d1904d8f72d909f8acb8469ee446"
DJ_PLAN = "5791ac3b27f500d8ae9f2cc72669b09aa7e490d17b33b992a2b55d0a8728b457"
DJ_CLOSURE = "82418da0c90fe543ee4da4c9c7af58b372f96a2a11b4dda0e0c5dbb0bdf77639"
DJ_MANIFEST = "879983c9b2b09056748b6a5e925d05600ad13c690072ad9d6d4e42b8e8f2ff6c"


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


def sha(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def historical(root: pathlib.Path, path: str) -> bytes:
    return subprocess.run(["git", "-C", str(root), "show", f"{BASELINE}:{path}"],
                          check=True, capture_output=True).stdout


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--evidence-root", type=pathlib.Path,
                        help="optional ignored stage25ia root containing packages and determinism runs")
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    paths = {
        "audit": root / "apple-everest/littleepic-graph-stage25ia.json",
        "compat": root / "tools/AppleEverestBuilder/StaticAotCompatibility.cs",
        "analyzer": root / "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs",
        "freezer": root / "tools/AppleEverestBuilder/AssemblyFreezer.cs",
        "closure": root / "tools/AppleEverestBuilder/ClosureGenerator.cs",
        "static_il": root / "tools/AppleEverestBuilder/StaticIlFreeze.cs",
        "models": root / "tools/AppleEverestBuilder/Models.cs",
        "targets": root / "apple-everest/managed-detour-targets-v2.json",
        "tests": root / "tools/AppleEverestBuilder/tests/Program.cs",
        "architecture": root / "docs/APPLE_EVEREST_STATIC_AOT.md",
        "compatibility": root / "docs/APPLE_EVEREST_COMPATIBILITY.md",
        "report": root / "docs/history/stages/APPLE_EVEREST_LITTLEEPIC_STAGE25IA_REPORT.md",
        "history": root / "docs/history/README.md",
    }
    for label, path in paths.items():
        c.require(path.is_file(), f"required {label} file")

    audit = json.loads(paths["audit"].read_text())
    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25I-A", "audit schema/stage")
    c.require(audit["status"] == "YELLOW" and audit["classification"] == "CUSTOM_AUDIO_UNSUPPORTED",
              "honest YELLOW custom-audio classification")
    c.require(audit["baseline"]["integrationCommit"] == BASELINE and
              audit["baseline"]["featureBranch"] == "feature/apple-everest-littleepic-map",
              "exact baseline and branch")
    c.require(audit["baseline"]["everestCommit"] == EVEREST and
              audit["baseline"]["monoModCommit"] == MONOMOD, "Everest/MonoMod pins")

    package = audit["root"]
    c.require(package["name"] == "LittleEpic's Precision Challenge" and package["version"] == "1.0.0",
              "root identity")
    c.require(package["downloadUrl"] == "https://gamebanana.com/mmdl/1243114" and
              package["zipSha256"] == ROOT_ZIP, "root public release pin")
    c.require(package["sourceLogicalSha256"] ==
              "a691ed075d73eab6085d7526ac5ffea52c6f1db8daf64a9da095cb51f8ed6053",
              "root logical pin")
    c.require(package["license"].startswith("CC BY-NC-ND 4.0"), "root licence metadata")
    map_info = package["map"]
    c.require(map_info["path"] == "Maps/LittleEpic/precisionchallenge/precisionchallenge.bin" and
              map_info["originalSha256"] ==
              "6de18b4280df15493a9f1b0eac2b547118757cf276795146bdbf299e9c371475",
              "exact map file")
    c.require(map_info["sid"] == "LittleEpic/precisionchallenge/precisionchallenge" and
              map_info["levelSet"] == "LittleEpic", "SID/LevelSet")
    c.require(map_info["roomCount"] == 8 and map_info["rooms"] ==
              ["1", "2", "3", "4", "5", "6", "7", "heart"], "eight exact rooms")
    c.require(map_info["startingRoom"] == "1" and map_info["startingSpawn"] ==
              {"room": "1", "x": 160, "y": 176, "entityId": 0}, "exact starting spawn")

    graph = audit["dependencyGraph"]
    c.require(graph["directCodeHelpers"] == ["ChronoHelper", "DJMapHelper"] and
              graph["transitiveCodeHelpers"] == [], "two direct helpers and no transitive code helper")
    c.require(graph["resolvedLoadOrder"] == ["Everest stable-1.6458.0", "ChronoHelper 1.3.3",
              "DJMapHelper 1.13.4", "LittleEpic's Precision Challenge 1.0.0"], "exact load order")

    helpers = {helper["name"]: helper for helper in audit["helpers"]}
    c.require(set(helpers) == {"ChronoHelper", "DJMapHelper"}, "two exact helper records")
    chrono = helpers["ChronoHelper"]
    c.require(chrono["version"] == "1.3.3" and chrono["downloadUrl"] ==
              "https://gamebanana.com/mmdl/1778580", "Chrono identity/URL")
    c.require(chrono["zipSha256"] == CHRONO_ZIP and chrono["authoritativeDllSha256"] == CHRONO_DLL,
              "Chrono ZIP/DLL pins")
    c.require(chrono["authoritativeDllPath"] == "bin/Debug/net452/ChronoHelper.dll" and
              not chrono["sourceRequiredForProduction"], "Chrono authoritative distributed DLL")
    c.require(chrono["actualMapUsage"] == {
        "id": "ChronoHelper/CustomTimeSwitchGates", "kind": "entity", "room": "5", "count": 1,
        "attributes": {"moveTime": 17},
        "factoryType": "Celeste.Mod.ChronoHelper.Entities.CustomTimeSwitchGates",
        "factorySignature": "EntityData,Vector2"}, "real Chrono map usage")
    dj = helpers["DJMapHelper"]
    c.require(dj["version"] == "1.13.4" and dj["downloadUrl"] ==
              "https://gamebanana.com/mmdl/1036311", "DJ identity/URL")
    c.require(dj["zipSha256"] == DJ_ZIP and dj["authoritativeDllSha256"] == DJ_DLL,
              "DJ ZIP/DLL pins")
    c.require(dj["publicSource"]["commit"] == "693fbea3405090517ad66217b809673ff2725206" and
              not dj["sourceRequiredForProduction"], "DJ public audit source and distributed authority")
    c.require(dj["actualMapUsage"]["id"] == "DJMapHelper/maxDashesTrigger" and
              dj["actualMapUsage"]["room"] == "1" and
              dj["actualMapUsage"]["attributes"]["dashes"] == "Zero", "real DJ map usage")

    il = audit["djMapHelperIlFreeze"]
    c.require(il["registrationCount"] == 5 and il["targetBodyCount"] == 3, "five registrations/three bodies")
    c.require(il["directIlHookCount"] == 0 and il["configuredIlHookCount"] == 0, "no direct/configured ILHook")
    c.require(il["emitDelegateSites"] == 51 and il["allEmitDelegatesStaticNonCapturing"],
              "51 static noncapturing delegates")
    c.require(il["realDistributedManipulatorsExecutedOnMac"] and il["allTargetsFingerprintProtected"],
              "real Mac manipulators and fingerprints")
    c.require(il["planSha256"] == DJ_PLAN and len(il["transforms"]) == 5, "exact frozen plan")
    c.require([item["planId"] for item in il["transforms"]] == ["DJMapHelper:player-h-feather",
              "DJMapHelper:player-h-theo", "DJMapHelper:player-v-feather",
              "DJMapHelper:player-v-theo", "DJMapHelper:fling-awake"], "exact transform order")
    c.require(sum(item["emitDelegateSites"] for item in il["transforms"]) == 51, "delegate site sum")
    c.require(all(len(item[key]) == 64 for item in il["transforms"]
                  for key in ("beforeSha256", "afterSha256", "diffSha256")), "IL hash shapes")
    runs = il["threeRunDeterminism"]
    c.require(runs == {"independentRuns": 3, "allIdentical": True,
              "sharedClosureSha256": DJ_CLOSURE, "manifestSha256": DJ_MANIFEST,
              "scope": "DJMapHelper-only; not the blocked complete LittleEpic graph"},
              "three-run DJ-only determinism")
    c.require(il["omissionRestoresCanonicalTargets"], "helper omission restores canonical targets")

    detours = audit["ordinaryManagedDetours"]
    c.require(detours["catalogBefore"] == 69 and detours["catalogAfter"] == 102 and
              detours["newDescriptorCount"] == 33, "69-to-102 target catalog")
    c.require(detours["chronoHelperTargetCount"] == 15 and detours["djMapHelperTargetCount"] == 26,
              "helper target counts")
    c.require(len(detours["newDescriptorIds"]) == 33 and len(set(detours["newDescriptorIds"])) == 33 and
              detours["descriptorsAreExactSignatureDriven"], "33 unique exact descriptors")
    target_catalog = json.loads(paths["targets"].read_text())
    target_ids = {target["id"] for target in target_catalog["targets"]}
    c.require(target_catalog["schemaVersion"] == 2 and len(target_ids) == 102, "target catalog schema/count")
    c.require(set(detours["newDescriptorIds"]).issubset(target_ids), "new descriptors present")

    api = audit["reviewedApi"]
    c.require(api["existingAppleApiSurfaceBefore"] == api["existingAppleApiSurfaceAfter"] == 30,
              "existing reviewed API catalog unchanged")
    c.require(api["stageSpecificExactMemberPromotions"] == 27 and
              len(api["stageSpecificMembers"]) == 27 and not api["broadPublicize"],
              "27 bounded source members")
    registries = audit["registries"]
    c.require(registries["chronoHelperFactoryCount"] == 30 and registries["djMapHelperFactoryCount"] == 28,
              "helper static factory census")
    c.require(registries["allFactoriesStatic"] and not registries["deviceUsesGetTypes"] and
              not registries["deviceUsesActivator"], "reflection-free factory policy")
    c.require(len(registries["actualMapEntities"]) == 1 and
              registries["actualMapEntities"][0]["id"] == "ChronoHelper/CustomTimeSwitchGates",
              "one actual custom entity")
    c.require(len(registries["actualMapTriggers"]) == 1 and
              registries["actualMapTriggers"][0]["id"] == "DJMapHelper/maxDashesTrigger",
              "one actual custom trigger")
    c.require(registries["actualMapBackdrops"] == [], "zero custom backdrops")

    content = audit["content"]
    c.require(content["deterministicOrder"] == ["ChronoHelper", "DJMapHelper",
              "LittleEpic's Precision Challenge"] and content["collisions"] == [], "content order/no collisions")
    c.require(content["chronoHelper"]["files"] == 280 and content["djMapHelper"]["files"] == 337 and
              content["root"]["files"] == 2 and content["diagnosticMergedFileCount"] == 619,
              "content file census")
    c.require(content["acceptedCompleteGraphContentHash"] is None, "no false accepted content hash")

    mechanism = audit["mechanismCensus"]
    c.require(mechanism["directIlHookCount"] == 0 and mechanism["configuredIlHookCount"] == 0,
              "complete graph no direct/configured ILHook")
    c.require(mechanism["dynamicData"] == "LIVE_EXACT_BOUNDED_LOWERING_PROTOTYPED",
              "honest bounded DynamicData status")
    c.require(not mechanism["modInteropRequired"] and not mechanism["moduleSettingsRequired"] and
              not mechanism["moduleSaveDataRequired"], "no ModInterop/settings/SaveData")
    c.require(not mechanism["reachableLua"] and not mechanism["nativeOrPInvoke"], "no Lua/native")
    audio = mechanism["customAudio"]
    c.require(audio["bankPath"] == "Audio/ExpertContestHelper.bank" and
              audio["bankSha256"] == "2607c358e66f4bce4e81fa3748eaffe1205dcfb57010be2e90ce40fabeb8d3ec",
              "exact custom bank")
    c.require(audio["events"] == ["event:/ricky06/EC2023/horn", "event:/ricky06/zip_mover 2"] and
              audio["bank"] == "bank:/ExpertContestHelper", "exact custom event inventory")
    c.require(audio["actualMapEntityUsesBaseGameEventsOnly"] and
              not audio["acceptedAppleCustomBankLoaderExists"], "map nuance and absent loader")

    gate = audit["preAotDecision"]
    c.require(gate["unclassifiedBlockers"] == 0 and gate["unsupportedRequiredBlockers"] == 1,
              "one classified blocker")
    c.require(gate["result"] == "BLOCKED" and not gate["fullBuildAllowed"] and
              gate["diagnostic"].startswith("CUSTOM_AUDIO_UNSUPPORTED:"), "pre-AOT fail-closed decision")
    result = audit["productionResult"]
    c.require(not result["sourceFreeCompleteGraph"] and result["acceptedSharedClosureSha256"] is None,
              "no false complete source-free closure")
    for key in ("iosIpa", "tvosIpa"):
        c.require(result[key] is None, f"no false {key}")
    c.require(all(result[key] == "NOT_RUN_PRE_AOT_CUSTOM_AUDIO_GATE" for key in
                  ("fullTrim", "fullAot", "useInterpreter", "jit", "iphone", "ipadOs15", "appleTv")),
              "product/physical gates honestly skipped")
    c.require(result["customMapProgression"] == "NONPERSISTENT_DEBUG_LANE" and
              result["saveAndQuit"] == "SUPPRESSED_IN_CUSTOM_MAP_DEBUG_LANE", "progression policy unchanged")
    c.require(not audit["integrationReady"] and not audit["firstRealMultiCodeHelperMapGreen"] and
              audit["githubActionsMinutesUsed"] == 0, "not integration-ready and zero Actions")

    compat = paths["compat"].read_text()
    for token in (CHRONO_ZIP.replace("af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18",
                                    "3ada281adf3affd433bcd02eb9966abab662bf5ab23714095460f4de7eaff5f6"),
                  CHRONO_DLL, DJ_DLL, "chronohelper-1.3.3-static-aot-v1",
                  "djmaphelper-1.13.4-static-aot-v1", "RewriteDJFastReflection",
                  "RootReviewedReflectionMembers", "AllowsDynamicMethod"):
        c.require(token in compat, f"static-AOT compatibility contract {token}")
    analyzer = paths["analyzer"].read_text()
    c.require("custom-fmod-bank:" in analyzer and "CUSTOM_AUDIO_UNSUPPORTED" in analyzer and
              "otherwise compatible helper must not silently ship a partial feature" in analyzer,
              "explicit custom-audio fail-closed analyzer")
    c.require("ignored-nonauthoritative-managed-assembly" in analyzer and
              "ignored-incidental-managed-source" in analyzer, "authoritative declared binary policy")
    static_il = paths["static_il"].read_text()
    for item in il["transforms"]:
        c.require(item["planId"] in static_il and item["beforeSha256"] in static_il and
                  item["afterSha256"] in static_il and item["diffSha256"] in static_il,
                  f"frozen transform contract {item['planId']}")
    c.require("DJMapHelperPlans" in static_il and "RewriteDJMapHelper" in static_il,
              "DJ plan registry and host rewrite")
    c.require("CUSTOM_AUDIO_UNSUPPORTED" in paths["models"].read_text(), "custom-audio model class")
    tests = paths["tests"].read_text()
    c.require("custom FMOD bank fails closed before AOT" in tests and
              "custom FMOD bank audit is explicit" in tests, "custom-audio deterministic tests")

    c.require("CUSTOM_AUDIO_UNSUPPORTED" in paths["architecture"].read_text(), "architecture gate documented")
    compatibility_doc = paths["compatibility"].read_text()
    c.require("custom FMOD" in compatibility_doc or "custom-FMOD" in compatibility_doc,
              "compatibility gate documented")
    c.require("Stage 25I-A" in paths["report"].read_text() and "YELLOW" in paths["report"].read_text(),
              "YELLOW report")
    c.require("APPLE_EVEREST_LITTLEEPIC_STAGE25IA_REPORT.md" in paths["history"].read_text(), "history index")

    for old_path in (
        "apple-everest/multi-helper-candidate-audit-stage25g.json",
        "apple-everest/il-compatibility-audit-stage25h.json",
        "apple-everest/il-compatibility-audit-stage25hb.json",
        "apple-everest/graph-il-closure-audit-stage25hc.json",
        "apple-everest/direct-ilhook-audit-stage25hd.json",
        "docs/history/stages/APPLE_EVEREST_MULTI_HELPER_STAGE25G_REPORT.md",
        "docs/history/stages/APPLE_EVEREST_STATIC_IL_STAGE25H_REPORT.md",
        "docs/history/stages/APPLE_EVEREST_STATIC_IL_COMPOSITION_STAGE25HB_REPORT.md",
        "docs/history/stages/APPLE_EVEREST_GRAPH_IL_CLOSURE_STAGE25HC_REPORT.md",
        "docs/history/stages/APPLE_EVEREST_DIRECT_ILHOOK_STAGE25HD_REPORT.md",
    ):
        c.require((root / old_path).read_bytes() == historical(root, old_path),
                  f"historical file unchanged: {old_path}")

    c.require(resolve(root, "ios-v0.1.1-rc.1") == IOS_RC, "iOS RC immutable")
    c.require(resolve(root, "v1.0.0-rc.1") == RC1, "tvOS RC1 immutable")
    c.require(resolve(root, "v1.0.0-rc.2") == RC2, "tvOS RC2 immutable")
    c.require(resolve(root, "origin/release/v1.0.0-rc.3") == RC3, "RC3 release branch immutable")
    c.require(git(root, "tag", "-l", "v1.0.0-rc.3") == "", "RC3 tag absent")
    c.require(resolve(root, BASELINE) == BASELINE, "baseline commit exists")

    tracked = git(root, "ls-files").splitlines()
    forbidden_names = ("LittleEpic.zip", "ChronoHelper.zip", "DJMapHelper.zip",
                       "ChronoHelper.dll", "DJMapHelper.dll", "ExpertContestHelper.bank")
    c.require(not any(any(name in path for name in forbidden_names) for path in tracked),
              "no proprietary fixture/archive/bank tracked")

    if args.evidence_root:
        evidence = args.evidence_root.resolve()
        packages = evidence / "packages"
        c.require(sha(packages / "LittleEpic.zip") == ROOT_ZIP, "ignored root ZIP pin")
        c.require(sha(packages / "ChronoHelper.zip") == CHRONO_ZIP, "ignored Chrono ZIP pin")
        c.require(sha(packages / "DJMapHelper.zip") == DJ_ZIP, "ignored DJ ZIP pin")
        manifests = [evidence / f"determinism-dj-{index}/compatibility-manifest.json"
                     for index in (1, 2, 3)]
        c.require(all(path.is_file() for path in manifests), "three ignored determinism manifests")
        c.require(len({path.read_bytes() for path in manifests}) == 1 and sha(manifests[0]) == DJ_MANIFEST,
                  "ignored manifests byte-identical")
        c.require(json.loads(manifests[0].read_text())["sharedClosureSha256"] == DJ_CLOSURE,
                  "ignored DJ closure hash")

    print(f"PASS: Stage 25I-A verifier ({c.count} checks, YELLOW custom-audio gate)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
