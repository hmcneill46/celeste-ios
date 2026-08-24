#!/usr/bin/env python3
"""Verify Stage 25J-B second-real-map and multi-map progression invariants."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess
import zipfile


START = "55d5a3cdff88e707e38404be206a7046b5c149db"
LAST_ALL_THREE = "57d55c7b9e15c5fa84847d2879f774f30025a96a"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
FEAR_ZIP = "7071c67f93a29c0f25c87c366762437bbf60873e135c5ca34009c197f5b40e5b"
FEAR_MAP = "5c29701520776fa03a3e2d8af798c23d80e188a91d6b3f65cac97ca81239de86"
FEAR_COMPAT = "a7a63c0a036cda9dcbc16166afb06b1010ba714ef2b0f97b4054319dbf90ff5d"
LITTLE_ZIP = "ca57d295f446affdd8eb9b56ffe58f98cc652e9ec242ff07b224877d0067b384"
LITTLE_MAP = "6de18b4280df15493a9f1b0eac2b547118757cf276795146bdbf299e9c371475"
LITTLE_COMPAT = "a341e6cbc2ff916c81b2717f24560b50bbb65b0a4326fb0ea31ed285841dc259"
CHRONO = "af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18"
DJ = "95ab02d657213031b70c3079738b21be399e567ca8a32b1fe8e8effc0778eedb"
BANK = "2607c358e66f4bce4e81fa3748eaffe1205dcfb57010be2e90ce40fabeb8d3ec"
AUDIO = "0a7ca20a24239f4829ab5af7002004309d38d89ac5bffbfefff6a49b278c8841"
IL_PLAN = "5791ac3b27f500d8ae9f2cc72669b09aa7e490d17b33b992a2b55d0a8728b457"
CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
VANILLA_IOS = "2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
MANAGED = "4db0a0c085431e71281877128675a3a58dafeffc77a8eb06a6eb090fdc38cf40"
CONTENT_TREE = "3f62845066819fdf699a34d6207ed485dfc6f6062ca10660739aaadc3d328184"
REGISTRY = "d7ff46c8ff66d0e5928723494ac65f4b08bfb23c110b621caeb30c2261b65ad1"
HOOKS = "3ce99ab4365bf83a91e9b8c741a0a5d2b4a9af72582083f021869d80d53a17e4"
API = "d594dea86e944b468819818773f2177312445bbebe022bcbc7ea1ff779c8652c"
PROGRESSION = "80bf30085d2c840d98b949f743960f656afbefc442eb6053daa96a64bcf098fb"
SHARED = "0b4edf7adef5b678ab829a955ef015e9a3ec0259ff4caa0112b9fb55b7f333b1"
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


def git(root: pathlib.Path, *args: str, check: bool = True) -> str:
    return subprocess.run(["git", "-C", str(root), *args], check=check,
                          capture_output=True, text=True).stdout.strip()


def verify_ipa(c: Checks, ipa: pathlib.Path, platform: str, expected_hash: str) -> None:
    c.require(ipa.is_file(), f"{platform} IPA exists")
    c.require(sha(ipa.read_bytes()) == expected_hash, f"{platform} exact IPA hash")
    with zipfile.ZipFile(ipa) as archive:
        names = archive.namelist()
        apps = sorted({name.split("/")[1] for name in names
                       if name.startswith("Payload/") and name.count("/") >= 2})
        c.require(len(apps) == 1 and apps[0].endswith(".app"), f"{platform} exactly one app")
        prefix = f"Payload/{apps[0]}/"
        info = plistlib.loads(archive.read(prefix + "Info.plist"))
        expected_platform = ["iPhoneOS"] if platform == "ios" else ["AppleTVOS"]
        c.require(info["CFBundleSupportedPlatforms"] == expected_platform, f"{platform} platform tag")
        c.require(any(name.endswith("/Maps/BevWeb/FearoftheDark/FearoftheDark.bin") for name in names) and
                  any(name.endswith("/Maps/LittleEpic/precisionchallenge/precisionchallenge.bin") for name in names),
                  f"{platform} packages both real maps")
        banks = [name for name in names if name.endswith(
            "/AppleEverest/Mods/ChronoHelper/Audio/ExpertContestHelper.bank")]
        c.require(len(banks) == 1 and sha(archive.read(banks[0])) == BANK,
                  f"{platform} exact LittleEpic custom bank packaged once")
        forbidden = ("Mono.Cecil", "MonoMod.Cil", "MonoMod.RuntimeDetour",
                     "MonoMod.Utils", "AppleEverestIlWorker")
        c.require(not any(any(token in name for token in forbidden) for name in names),
                  f"{platform} excludes host-only transformation material")
        if platform == "tvos":
            c.require(prefix + "embedded.mobileprovision" not in names and
                      prefix + "_CodeSignature/CodeResources" not in names,
                      "tvOS diagnostic product is signing-ready unsigned")
        else:
            c.require(prefix + "embedded.mobileprovision" in names and
                      prefix + "_CodeSignature/CodeResources" in names,
                      "iOS product is development signed")


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
        "candidate audit": root / "apple-everest/second-real-map-candidates-stage25jb.json",
        "acceptance audit": root / "apple-everest/second-real-map-stage25jb.json",
        "authority": root / "apple-everest/runtime/AppleEverestProgressionReplicaAuthority.cs",
        "persistence": root / "apple-everest/runtime/AppleEverestProgressionPersistence.cs",
        "runtime": root / "apple-everest/runtime/AppleEverestProgressionRuntime.cs",
        "tests": root / "tools/AppleEverestBuilder/tests/LevelSetProgressionTests.cs",
        "graph tests": root / "tools/AppleEverestBuilder/tests/Program.cs",
        "report": root / "docs/history/stages/APPLE_EVEREST_SECOND_REAL_MAP_STAGE25JB_REPORT.md",
    }
    for label, path in required.items():
        c.require(path.is_file(), f"required {label}")

    candidate = json.loads(required["candidate audit"].read_text())
    audit = json.loads(required["acceptance audit"].read_text())
    c.require(candidate["schemaVersion"] == 1 and candidate["stage"] == "25J-B", "candidate audit schema")
    c.require(candidate["existingCohort"]["stage25gScreened"] == 53 and
              candidate["supplementalCohort"]["screened"] >= 30 and
              candidate["deepAudit"]["rootCount"] >= 8, "candidate breadth")
    c.require(len(candidate["deepAudit"]["candidates"]) == 8 and
              candidate["selected"]["name"] == "FearoftheDark", "reproducible selected candidate")
    selected = candidate["selected"]
    c.require(selected["zipSha256"] == FEAR_ZIP and selected["mapSha256"] == FEAR_MAP,
              "exact Fear package/map pins")
    c.require(selected["sid"] == "BevWeb/FearoftheDark/FearoftheDark" and
              selected["levelSet"] == "BevWeb" and len(selected["rooms"]) == 14,
              "Fear semantic identity")
    c.require(selected["actualMapUse"]["customEntities"] == ["ChronoHelper/PersistentFallingBlock"] and
              "checkpoint" in selected["actualMapUse"]["vanillaProgressionObjects"],
              "real helper and progression use")

    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25J-B", "acceptance schema")
    c.require(audit["status"] == "PASS_GREEN_IOS_IPADOS_TVOS_PHYSICAL_PENDING" and
              audit["classification"] == "BOUNDED_STATIC_MULTI_MAP_PROGRESSION", "exact successful status")
    baseline = audit["baseline"]
    c.require(baseline["startingCommit"] == START and baseline["lastAllThreePhysicalGreenSha"] == LAST_ALL_THREE,
              "baseline and last all-device GREEN")
    c.require(baseline["featureBranch"] == "feature/apple-everest-second-real-map" and
              baseline["iosRecoveryCommit"] == IOS_RC, "feature/recovery refs")
    c.require(baseline["rc1"] == RC1 and baseline["rc2"] == RC2 and
              baseline["rc3ReleaseBranch"] == RC3 and baseline["rc3TagAbsent"], "protected RC refs")

    second = audit["secondMap"]
    c.require(second["zipSha256"] == FEAR_ZIP and second["mapSha256"] == FEAR_MAP and
              second["compatibilityId"] == FEAR_COMPAT, "Fear acceptance pins")
    c.require(second["berries"] == 1 and second["heart"] and not second["cassette"] and
              second["checkpointRooms"] == ["02a"], "Fear progression census")
    little = audit["littleEpic"]
    c.require(little["zipSha256"] == LITTLE_ZIP and little["mapSha256"] == LITTLE_MAP,
              "exact LittleEpic regression pins")
    c.require(little["compatibilityIdBefore"] == LITTLE_COMPAT == little["compatibilityIdAfter"],
              "LittleEpic compatibility identity byte-identical")

    graph = audit["combinedGraph"]
    c.require(graph["sourceFree"] and graph["ordinaryUnmodifiedPublicPackages"] and
              len(graph["stableOrder"]) == 6, "one source-free combined graph")
    c.require(graph["realMaps"] == ["BevWeb/FearoftheDark/FearoftheDark",
              "LittleEpic/precisionchallenge/precisionchallenge"] and
              graph["generatedMapCountIncludingCanaries"] == 4, "both real maps in one closure")
    c.require(graph["sharedHelpers"] == ["ChronoHelper 1.3.3"] and
              set(graph["resolvedFactoriesUsedByRealMaps"]) == {
                  "ChronoHelper/PersistentFallingBlock", "ChronoHelper/CustomTimeSwitchGates",
                  "DJMapHelper/maxDashesTrigger"}, "actual helper use")

    census = audit["compatibilityCensus"]
    c.require(census["zeroUnclassifiedBlockers"] and census["zeroUnsupportedBlockers"] and
              not census["newMajorMechanism"] and census["newHelpers"] == [], "zero-overall mechanism census")
    c.require(census["hookGenTargetCountBefore"] == census["hookGenTargetCountAfter"] == 102 and
              census["newHookGenDescriptors"] == [], "unchanged HookGen catalog")
    c.require(census["frozenIlPlanSha256"] == IL_PLAN and census["frozenIlTransformCount"] == 5 and
              census["newFrozenIl"] == [] and census["directIlHookCount"] == 0 and
              not census["configuredIl"], "unchanged IL boundary")
    c.require(not census["generalDynamicData"] and census["modInteropRegistrationCount"] == 0 and
              census["newApiMembers"] == [] and not census["lua"] and not census["nativePayload"],
              "no DynamicData/ModInterop/API/Lua/native expansion")
    c.require(census["customAudioBankCount"] == 1 and census["customAudioEventCount"] == 2 and
              not census["runtimeDllLoading"] and census["runtimeDetour"] == "absent",
              "existing custom audio and static runtime boundary")

    progression = audit["progression"]
    c.require(progression["schema"] == "AEVPSV1" and progression["schemaVersion"] == 1 and
              progression["oldJaSidecarAccepted"] and progression["areaIndexIndependent"],
              "J-A-compatible semantic progression")
    c.require(progression["perRecordProjection"] and progression["absentMapRecordsQuarantined"] and
              progression["exactReadditionRestores"] and progression["changedSameSidRejected"],
              "per-map identity isolation")
    c.require(progression["slotIsolation"] and progression["deleteRecreateRemovesBothMaps"] and
              progression["replacementVanillaLineageRejectsOldProgression"], "slot/delete/import policies")
    c.require(progression["observedTwoMapRawBytes"] == 1330 and
              progression["observedTwoMapTvosCompressedBytes"] == 461 and
              progression["maximumThreeSlotAbBytes"] == 761856, "measured two-map storage budget")

    deterministic = audit["determinism"]
    c.require(deterministic["independentRuns"] == 3 and deterministic["allIdentical"] and
              deterministic["transformerVersion"] == "apple-everest-static-v11", "three-run determinism")
    expected_hashes = {"managedLogicalSha256": MANAGED, "contentLogicalSha256": CONTENT_TREE,
                       "registrySha256": REGISTRY, "hookTransformSha256": HOOKS,
                       "appleApiSurfaceSha256": API, "customAudioManifestSha256": AUDIO,
                       "progressionManifestSha256": PROGRESSION, "sharedClosureSha256": SHARED}
    c.require(all(deterministic[key] == value for key, value in expected_hashes.items()),
              "complete closure locks")

    products = audit["products"]
    c.require(products["release"] and products["fullTrim"] and products["fullAot"] and
              not products["useInterpreter"] and not products["jit"], "Release full-AOT products")
    c.require(products["ios"]["signedIpaBytes"] > 800_000_000 and
              len(products["ios"]["signedIpaSha256"]) == 64, "signed universal iOS evidence")
    c.require(products["tvos"]["unsignedIpaBytes"] > 800_000_000 and
              len(products["tvos"]["unsignedIpaSha256"]) == 64, "unsigned tvOS evidence")
    physical = audit["physical"]
    c.require(physical["iphone"].startswith("PASS_") and physical["ipadOs15"].startswith("PASS_"),
              "iPhone and iPad physical GREEN")
    c.require(physical["appleTv"] == TVOS_PENDING and len(audit["futureAppleTvCatchUp"]) >= 12,
              "honest Apple TV pending contract")
    c.require(physical["littleEpicJaProgressSurvived"] and physical["fearSaveAndQuit"] and
              physical["fearColdResume"] and physical["mapSwitchingIsolation"], "two-map physical persistence")
    c.require(physical["littleEpicCustomHorn"] and physical["littleEpicChronoGate"] and
              physical["littleEpicDjFrozenIl"], "LittleEpic helper regressions")
    c.require(audit["githubActionsMinutesUsed"] == 0 and audit["developmentIntegrationReady"] and
              not audit["allPlatformReleaseEligible"], "travel-stage integration boundary")

    locks = audit["locks"]
    c.require(locks["customBank"] == BANK and locks["customAudioManifest"] == AUDIO and
              locks["djIlPlan"] == IL_PLAN, "I-B audio/IL locks")
    c.require(locks["canonicalContent"] == CONTENT and locks["canonicalRaw"] == RAW and
              locks["canonicalPatched"] == PATCHED and locks["stage6AudioTree"] == STAGE6,
              "canonical locks")
    c.require(locks["vanillaIosGeneratedSource"] == VANILLA_IOS and locks["iosNative"] == IOS_NATIVE and
              locks["tvosNative"] == TVOS_NATIVE, "vanilla/native locks")

    authority = required["authority"].read_text()
    persistence = required["persistence"].read_text()
    runtime = required["runtime"].read_text()
    tests = required["tests"].read_text()
    graph_tests = required["graph tests"].read_text()
    report = required["report"].read_text()
    c.require("MergeInstalledAreas" in authority and "value.Areas.Any" in authority and
              "!compatibleMaps.ContainsKey(area.Sid)" in authority, "per-map quarantine authority")
    c.require("CaptureAreas(SaveData save, AppleEverestProgressionSnapshot selected)" in runtime and
              "value.Sid == descriptor.Sid && value.CompatibilityId == descriptor.CompatibilityId" in runtime,
              "exact per-map projection")
    c.require("state.Selected.Session.Sid" in persistence and "CaptureAreas(SaveData.Instance, state.Selected)" in persistence,
              "absent-map capture retention")
    required_tests = ("old single-map sidecar remains valid after second map installation",
                      "two installed maps select one shared snapshot", "removing second map does not hide first map state",
                      "absent second-map state stays quarantined", "exact second-map readdition restores",
                      "same SID with changed content replaces", "changed second map cannot hide independent exact first-map state",
                      "imported replacement base cannot inherit two-map progression",
                      "delete removes both map records before slot recreation")
    c.require(all(token in tests for token in required_tests), "multi-map identity deterministic tests")
    c.require("two real maps share one helper satisfying compatible minimums" in graph_tests and
              "two exact helper archives cannot win by input order" in graph_tests, "dependency version policy tests")
    c.require("PASS — GREEN_IOS_IPADOS / TVOS_PHYSICAL_PENDING" in report and TVOS_PENDING in report,
              "report exact successful travel wording")
    c.require("ALL_PLATFORMS_PHYSICAL_GREEN" not in report and "Apple TV physical: PASS" not in report,
              "report cannot claim new Apple TV acceptance")

    profiles = json.loads((root / "managed/celeste-input-profiles.json").read_text())
    canonical = profiles["canonicalClasses"]["celeste-1.4.0.0-a"]
    c.require(canonical["decompiledSource"]["logicalSha256"] == RAW and
              canonical["patchedSource"]["logicalSha256"] == PATCHED and
              canonical["stage6RealAudio"]["logicalSha256"] == STAGE6 and
              profiles["contentClasses"]["celeste-content-1.4.0.0-a"]["aggregateSha256"] == CONTENT,
              "tracked canonical input locks unchanged")
    c.require(json.loads((root / "native/ios-native-output.lock.json").read_text())["logicalSetSha256"] == IOS_NATIVE and
              json.loads((root / "managed/celeste-generation.lock.json").read_text())["stage1NativeLogicalSha256"] == TVOS_NATIVE,
              "tracked native locks unchanged")

    c.require(git(root, "rev-parse", "tvos-port") == START and git(root, "rev-parse", "origin/tvos-port") == START,
              "integration branch untouched")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1 and
              git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2 and
              git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3,
              "protected release refs untouched")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 tag remains absent")
    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".bank", ".zip", ".dll", ".ipa", ".snapshot"))
                      for path in tracked), "no proprietary/product/progression bytes tracked")

    closure = (args.closure or root / ".build/apple-everest/production-canary/shared-closure").resolve()
    if closure.is_dir():
        manifest = json.loads((closure / "compatibility-manifest.json").read_text())
        c.require(manifest["sharedClosureSha256"] == SHARED and manifest["managedLogicalSha256"] == MANAGED and
                  manifest["contentLogicalSha256"] == CONTENT_TREE, "supplied closure exact hashes")
        c.require(manifest["levelSetProgressionMapCount"] == 4 and manifest["managedDetourTargetCount"] == 102 and
                  manifest["customAudioManifestSha256"] == AUDIO, "supplied closure policies")
        progression_text = (closure / "levelset-progression-manifest.txt").read_text()
        c.require(FEAR_MAP in progression_text and FEAR_COMPAT in progression_text and
                  LITTLE_MAP in progression_text and LITTLE_COMPAT in progression_text,
                  "supplied closure contains both exact map identities")

    if args.ios_ipa and args.tvos_ipa:
        verify_ipa(c, args.ios_ipa.resolve(), "ios", products["ios"]["signedIpaSha256"])
        verify_ipa(c, args.tvos_ipa.resolve(), "tvos", products["tvos"]["unsignedIpaSha256"])

    print(f"PASS: Stage 25J-B verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
