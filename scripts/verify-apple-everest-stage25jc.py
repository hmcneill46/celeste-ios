#!/usr/bin/env python3
"""Verify Stage 25J-C real same-LevelSet and travel-mode invariants."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess
import zipfile


START = "972daa03e5ca77d38b3d5071b43e474c0bf3084a"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
TORREMOLINOS_ZIP = "90e4918cfe24a2075b52853d02614cf97c9ecd8c552c3da6f1360d8c5e3fcbec"
MAP_A = "900b60bb3471299bde8f5419dfae9ca74c04f4a8bf556a2c4d4da5ecb2c81ba6"
MAP_B = "a3fc404c03276b8296f10d83ef9d4d9216237c711ab1002b2006bfd7e0f69aad"
COMPAT_A = "bb7febe30e78a2aad0f2b85709d624f37d63e9793dbbba89225d7193bc03c392"
COMPAT_B = "1166435f8b43fbd13843a84670733dc6f4bfb73e0095f8356b5457f87a6e3550"
LEVELSET_ID = "0a3373f734d66d247e1b30c46826d41e48faf72e201bd10a875463bb15d4d68d"
LITTLE_MAP = "6de18b4280df15493a9f1b0eac2b547118757cf276795146bdbf299e9c371475"
LITTLE_COMPAT = "a341e6cbc2ff916c81b2717f24560b50bbb65b0a4326fb0ea31ed285841dc259"
FEAR_MAP = "5c29701520776fa03a3e2d8af798c23d80e188a91d6b3f65cac97ca81239de86"
FEAR_COMPAT = "a7a63c0a036cda9dcbc16166afb06b1010ba714ef2b0f97b4054319dbf90ff5d"
SHARED = "ac8bfd9f69a2ba09154dc372c64701162e72575a18ba9a57190865faf7afb337"
MANAGED = "23713c56359684f621db6a6172bec3b91bf5c6a499b378ab18c740df43de64f0"
CONTENT_TREE = "31a1401602b00f347668e676105763f9e61b13bd326d467f7b6e176090782d04"
REGISTRY = "d7ff46c8ff66d0e5928723494ac65f4b08bfb23c110b621caeb30c2261b65ad1"
HOOKS = "80b1b94b86e0e331405fe58b61a2e85064a972c2fe778a022d026dadca785244"
API = "d594dea86e944b468819818773f2177312445bbebe022bcbc7ea1ff779c8652c"
PROGRESSION = "85fd6303a88e49e87135c8349e55af66ea86185e30a977799c93eab6a37822f4"
LEVELSET = "2e96e9d22ba47e2e744cab1cfadcf093319d458d84c9636f4d7fed06fa068733"
AUDIO = "0a7ca20a24239f4829ab5af7002004309d38d89ac5bffbfefff6a49b278c8841"
BANK = "2607c358e66f4bce4e81fa3748eaffe1205dcfb57010be2e90ce40fabeb8d3ec"
IL_PLAN = "5791ac3b27f500d8ae9f2cc72669b09aa7e490d17b33b992a2b55d0a8728b457"
CANONICAL_CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
CANONICAL_RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
CANONICAL_PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
VANILLA_IOS = "2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
TVOS_PENDING = "TVOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE"


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, value: bool, message: str) -> None:
        if not value:
            raise SystemExit("FAIL: " + message)
        self.count += 1


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def git(root: pathlib.Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(root), *args], check=True,
                          capture_output=True, text=True).stdout.strip()


def verify_ipa(c: Checks, ipa: pathlib.Path, platform: str, audit: dict) -> None:
    product = audit["products"][platform]
    c.require(ipa.is_file(), f"{platform} IPA exists")
    c.require(ipa.stat().st_size == product["ipaBytes"] and sha(ipa.read_bytes()) == product["ipaSha256"],
              f"{platform} exact accepted package")
    with zipfile.ZipFile(ipa) as archive:
        names = archive.namelist()
        apps = sorted({name.split("/")[1] for name in names
                       if name.startswith("Payload/") and name.count("/") >= 2})
        c.require(len(apps) == 1 and apps[0].endswith(".app"), f"{platform} exactly one app")
        prefix = f"Payload/{apps[0]}/"
        info = plistlib.loads(archive.read(prefix + "Info.plist"))
        c.require(info["CFBundleSupportedPlatforms"] ==
                  (["iPhoneOS"] if platform == "ios" else ["AppleTVOS"]), f"{platform} platform")
        c.require(info.get("UIDeviceFamily") == ([1, 2] if platform == "ios" else [3]),
                  f"{platform} exact device family")
        required_maps = (
            "Maps/LittleEpic/precisionchallenge/precisionchallenge.bin",
            "Maps/BevWeb/FearoftheDark/FearoftheDark.bin",
            "Maps/Xoa/Torremolinos Speedbuild/1.bin",
            "Maps/Xoa/Torremolinos Speedbuild/2.bin",
        )
        c.require(all(any(name.endswith("/" + map_path) for name in names) for map_path in required_maps),
                  f"{platform} contains all four real map roots")
        banks = [name for name in names if name.endswith(
            "/AppleEverest/Mods/ChronoHelper/Audio/ExpertContestHelper.bank")]
        c.require(len(banks) == 1 and sha(archive.read(banks[0])) == BANK,
                  f"{platform} exact custom bank once")
        c.require(not any(any(token in name for token in (
            "Mono.Cecil", "MonoMod.Cil", "MonoMod.RuntimeDetour", "MonoMod.Utils",
            "AppleEverestIlWorker")) for name in names), f"{platform} excludes host transformation material")
        if platform == "tvos":
            c.require(prefix + "embedded.mobileprovision" not in names and
                      prefix + "_CodeSignature/CodeResources" not in names,
                      "tvOS package is signing-ready unsigned")
        else:
            c.require(prefix + "embedded.mobileprovision" in names and
                      prefix + "_CodeSignature/CodeResources" in names,
                      "iOS package is development signed")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--closure", type=pathlib.Path)
    parser.add_argument("--ios-ipa", type=pathlib.Path)
    parser.add_argument("--tvos-ipa", type=pathlib.Path)
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    required = {
        "candidate audit": root / "apple-everest/real-levelset-candidates-stage25jc.json",
        "acceptance audit": root / "apple-everest/real-levelset-stage25jc.json",
        "LevelSet authority": root / "tools/AppleEverestBuilder/LevelSetProgressionManifest.cs",
        "MapData compatibility": root / "tools/AppleEverestBuilder/MapDataCompatibilityPatch.cs",
        "runtime": root / "apple-everest/runtime/AppleEverestProgressionRuntime.cs",
        "selector": root / "apple-everest/runtime/AppleEverestStaticRuntime.cs",
        "tests": root / "tools/AppleEverestBuilder/tests/LevelSetProgressionTests.cs",
        "fixture fetch": root / "scripts/fetch-apple-everest-stage25jc-fixtures.sh",
        "report": root / "docs/history/stages/APPLE_EVEREST_REAL_LEVELSET_STAGE25JC_REPORT.md",
    }
    for label, path in required.items():
        c.require(path.is_file(), f"required {label}")

    candidate = json.loads(required["candidate audit"].read_text())
    audit = json.loads(required["acceptance audit"].read_text())
    c.require(candidate["schemaVersion"] == 1 and candidate["stage"] == "25J-C", "candidate audit schema")
    c.require(candidate["screening"]["metadataCandidatesRecorded"] >= 30 and
              candidate["deepAudit"]["count"] >= 8 and
              len(candidate["deepAudit"]["candidates"]) >= 8, "candidate breadth")
    selected = candidate["selected"]
    c.require(selected["name"] == "Torremolinos Speedbuild" and selected["version"] == "1.0.0" and
              selected["zipSha256"] == TORREMOLINOS_ZIP, "selected exact ordinary release")
    c.require(selected["maps"] == ["Xoa/Torremolinos Speedbuild/1", "Xoa/Torremolinos Speedbuild/2"] and
              selected["levelSet"] == "Xoa/Torremolinos Speedbuild", "selected true same-LevelSet package")

    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25J-C", "acceptance schema")
    c.require(audit["status"] == "PASS_GREEN_IOS_IPADOS_TVOS_PHYSICAL_PENDING" and
              audit["classification"] == "REAL_MULTI_MAP_LEVELSET_STATIC_AOT", "exact travel status")
    baseline = audit["baseline"]
    c.require(baseline["startingCommit"] == START and
              baseline["featureBranch"] == "feature/apple-everest-real-levelset", "baseline")
    c.require(baseline["iosRecoveryCommit"] == IOS_RC and baseline["rc1"] == RC1 and
              baseline["rc2"] == RC2 and baseline["rc3ReleaseBranch"] == RC3 and
              baseline["rc3TagAbsent"], "protected refs recorded")

    package = audit["package"]
    c.require(package["zipSha256"] == TORREMOLINOS_ZIP and package["ordinaryUnmodifiedPublicRelease"] and
              package["mapCount"] == 2 and package["levelSet"] == "Xoa/Torremolinos Speedbuild",
              "accepted package identity")
    maps = package["maps"]
    c.require([value["mapSha256"] for value in maps] == [MAP_A, MAP_B] and
              [value["compatibilityId"] for value in maps] == [COMPAT_A, COMPAT_B], "exact new map identities")
    c.require([value["sid"] for value in maps] == ["Xoa/Torremolinos Speedbuild/1",
              "Xoa/Torremolinos Speedbuild/2"] and [len(value["rooms"]) for value in maps] == [5, 5],
              "both real maps and room census")
    c.require(package["levelSetIdentity"] == LEVELSET_ID and package["aggregateMaxima"] == {
        "strawberries": 4, "hearts": 2, "cassettes": 0, "completions": 2}, "LevelSet identity/maxima")

    retained = audit["retainedMaps"]
    c.require(retained["littleEpic"]["mapSha256"] == LITTLE_MAP and
              retained["littleEpic"]["compatibilityIdBefore"] == LITTLE_COMPAT ==
              retained["littleEpic"]["compatibilityIdAfter"], "LittleEpic exact compatibility retained")
    c.require(retained["fear"]["mapSha256"] == FEAR_MAP and
              retained["fear"]["compatibilityIdBefore"] == FEAR_COMPAT ==
              retained["fear"]["compatibilityIdAfter"], "Fear exact compatibility retained")

    graph = audit["combinedGraph"]
    c.require(graph["ordinaryUnmodifiedPublicPackages"] and len(graph["stableOrder"]) == 7 and
              graph["realMapCount"] == 4 and graph["generatedMapCountIncludingCanaries"] == 6,
              "one combined deterministic graph")
    c.require(graph["newPackageHelpers"] == [] and graph["selectedPackageActualHelperUses"] == [] and
              graph["sharedHelpers"] == ["ChronoHelper 1.3.3", "DJMapHelper 1.13.4"],
              "new package requires no helper expansion")

    census = audit["compatibilityCensus"]
    c.require(census["zeroUnclassifiedBlockers"] and census["zeroUnsupportedBlockers"] and
              not census["newMajorArchitecture"], "zero blockers/no major mechanism")
    c.require(census["hookGenTargetCountBefore"] == census["hookGenTargetCountAfter"] == 102 and
              census["newHookGenDescriptors"] == [] and census["frozenIlPlanSha256"] == IL_PLAN and
              census["newFrozenIl"] == [] and census["directIlHookCount"] == 0,
              "unchanged HookGen/IL boundary")
    c.require(not census["configuredIl"] and not census["generalDynamicData"] and
              census["modInteropRegistrationCount"] == 0 and not census["lua"] and
              not census["nativePayload"] and census["newApiMembers"] == [], "no dynamic/runtime expansion")
    c.require(census["mapDataCompatibility"] == "pinned-everest-normalize-and-grow-strawberry-tracker-v2" and
              census["mapDataCompatibilityReason"] ==
              "distributed negative strawberry checkpoint/order metadata is normalized deterministically before bounded tracker growth",
              "exact physically diagnosed pinned-Everest compatibility patch")

    progression = audit["progression"]
    c.require(progression["schema"] == "AEVPSV1" and progression["schemaVersion"] == 1 and
              not progression["migrationRequired"] and progression["areaIndexIndependent"], "AEVPSV1 unchanged")
    c.require(progression["perMapIsolation"] and progression["mapBChangeDoesNotInvalidateMapA"] and
              progression["mapRemovalQuarantinesAndExactReadditionRestores"] and progression["slotIsolation"] and
              progression["deleteRecreateRemovesAllMaps"], "same-LevelSet persistence policies")
    c.require(progression["fourRealMapRawBytes"] == 1508 and
              progression["fourRealMapTvosCompressedBytes"] == 598 and
              progression["maximumTvosReplicaBytes"] == 126976 and
              progression["maximumThreeSlotAbBytes"] == 761856, "four-map storage evidence")

    deterministic = audit["determinism"]
    expected = {"sharedClosureSha256": SHARED, "managedLogicalSha256": MANAGED,
                "contentLogicalSha256": CONTENT_TREE, "registrySha256": REGISTRY,
                "hookTransformSha256": HOOKS, "appleApiSurfaceSha256": API,
                "progressionManifestSha256": PROGRESSION, "levelSetManifestSha256": LEVELSET,
                "customAudioManifestSha256": AUDIO, "frozenIlPlanSha256": IL_PLAN}
    c.require(deterministic["independentRuns"] == 3 and deterministic["allIdentical"] and
              deterministic["transformerVersion"] == "apple-everest-static-v13", "three-run determinism")
    c.require(all(deterministic[key] == value for key, value in expected.items()), "complete closure locks")

    products = audit["products"]
    c.require(products["release"] and products["fullTrim"] and products["fullAot"] and
              not products["useInterpreter"] and not products["jit"], "Release full-AOT products")
    c.require(products["ios"]["ipaBytes"] > 800_000_000 and len(products["ios"]["ipaSha256"]) == 64 and
              products["tvos"]["ipaBytes"] > 800_000_000 and len(products["tvos"]["ipaSha256"]) == 64,
              "iOS/tvOS package evidence")
    physical = audit["physical"]
    c.require(physical["iphone"].startswith("PASS_") and physical["ipadOs15"].startswith("PASS_"),
              "required iPhone/iPad physical acceptance")
    c.require(physical["appleTv"] == TVOS_PENDING and len(audit["futureAppleTvCatchUp"]) >= 16,
              "honest Apple TV physical pending state")
    c.require(physical["littleEpicStateSurvived"] and physical["fearStateSurvived"] and
              physical["mapASaveAndQuitColdResume"] and physical["mapBSaveAndQuitColdResume"] and
              physical["sameLevelSetIsolation"], "physical progression acceptance")
    c.require(physical["littleEpicHorn"] and physical["djFrozenIl"] and physical["chronoGate"],
              "helper regressions")

    levelset_authority = required["LevelSet authority"].read_text()
    mapdata = required["MapData compatibility"].read_text()
    runtime = required["runtime"].read_text()
    selector = required["selector"].read_text()
    tests = required["tests"].read_text()
    report = required["report"].read_text()
    c.require("ordered-member-map-compatibility-identities-v1" in
              (root / "tools/AppleEverestBuilder/ClosureGenerator.cs").read_text() and
              "map.Sid + \"\\t\" + map.CompatibilityId" in levelset_authority,
              "semantic LevelSet identity authority")
    c.require("AppleEverestNormalizeAndGet" in mapdata and "if (y < 0)" in mapdata and
              "if (map[y, x] != null)" in mapdata and "strawberry.Values[\\\"checkpointID\\\"] = y" in mapdata and
              "Array.Copy" in mapdata and "ReplaceExactlyOnce" in mapdata and
              "pinned-everest-normalize-and-grow-strawberry-tracker:v2" in
              (root / "tools/AppleEverestBuilder/ClosureGenerator.cs").read_text(),
              "bounded fail-closed pinned-Everest MapData compatibility")
    c.require("TryLevelSet" in runtime and "TotalCompletions" in runtime and
              "MaximumCompletions" in runtime, "runtime LevelSet aggregate API")
    c.require("LEVELSET: " in selector and "Play Map: " in selector and
              "Play Static Mod Map (Debug):" in selector, "grouped static selector")
    required_tests = ("same-LevelSet members are explicit", "runtime Area ordering cannot change LevelSet identity",
                      "adding an unrelated LevelSet cannot invalidate existing identity",
                      "changing Map B changes its LevelSet aggregate but not Map A identity",
                      "exact Map B readdition restores", "large valid tracker coordinates grow without losing existing entries",
                      "LittleEpic Fear and both same-LevelSet maps share one bounded snapshot")
    c.require(all(token in tests for token in required_tests), "deterministic same-LevelSet and compatibility tests")
    c.require("PASS — GREEN_IOS_IPADOS / TVOS_PHYSICAL_PENDING" in report and TVOS_PENDING in report,
              "report exact travel wording")
    forbidden_physical_claims = ("Apple TV physical: PASS", "Apple TV: PASS", "APPLE_TV_PHYSICAL_PASS",
                                 "ALL_PLATFORMS_PHYSICAL_GREEN")
    c.require(not any(token in report for token in forbidden_physical_claims),
              "report explicitly cannot claim Apple TV physical PASS")

    locks = audit["locks"]
    c.require(locks == {"canonicalContent": CANONICAL_CONTENT, "canonicalRaw": CANONICAL_RAW,
              "canonicalPatched": CANONICAL_PATCHED, "stage6AudioTree": STAGE6,
              "vanillaIosGeneratedSource": VANILLA_IOS, "iosNative": IOS_NATIVE,
              "tvosNative": TVOS_NATIVE, "customBank": BANK, "djIlPlan": IL_PLAN}, "all protected locks")
    c.require(audit["githubActionsMinutesUsed"] == 0 and audit["developmentIntegrationReady"] and
              not audit["allPlatformReleaseReady"], "travel integration/release boundary")

    c.require(git(root, "rev-parse", "tvos-port") == START and git(root, "rev-parse", "origin/tvos-port") == START,
              "integration branch untouched")
    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC and
              git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1 and
              git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2 and
              git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3,
              "protected refs untouched")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 remains untagged")
    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".bank", ".zip", ".dll", ".ipa", ".snapshot"))
                      for path in tracked), "no proprietary/product bytes tracked")

    closure = (args.closure or root / ".build/apple-everest/production-canary/shared-closure").resolve()
    if closure.is_dir():
        manifest = json.loads((closure / "compatibility-manifest.json").read_text())
        c.require(manifest["sharedClosureSha256"] == SHARED and
                  manifest["managedLogicalSha256"] == MANAGED and
                  manifest["contentLogicalSha256"] == CONTENT_TREE, "supplied closure hashes")
        c.require(manifest["transformerVersion"] == "apple-everest-static-v13" and
                  manifest["levelSetProgressionMapCount"] == 6 and manifest["levelSetCount"] == 4 and
                  manifest["levelSetManifestSha256"] == LEVELSET, "supplied LevelSet closure")
        progression_text = (closure / "levelset-progression-manifest.txt").read_text()
        levelset_text = (closure / "levelset-manifest.txt").read_text()
        c.require(all(token in progression_text for token in
                      (MAP_A, MAP_B, COMPAT_A, COMPAT_B, LITTLE_COMPAT, FEAR_COMPAT)), "all map identities linked")
        c.require("Xoa/Torremolinos Speedbuild\t" + LEVELSET_ID in levelset_text, "exact LevelSet identity linked")

    if args.ios_ipa:
        verify_ipa(c, args.ios_ipa.resolve(), "ios", audit)
    if args.tvos_ipa:
        verify_ipa(c, args.tvos_ipa.resolve(), "tvos", audit)

    print(f"PASS: Stage 25J-C real same-LevelSet verification ({c.count} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
