#!/usr/bin/env python3
"""Verify Stage 25J-A durable static LevelSet progression invariants."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess
import zipfile


START = "57d55c7b9e15c5fa84847d2879f774f30025a96a"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
EVEREST = "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00"
MONOMOD = "dfc30a1506d37fb88a2c2be004f525205f46a24c"
LITTLE = "ca57d295f446affdd8eb9b56ffe58f98cc652e9ec242ff07b224877d0067b384"
MAP = "6de18b4280df15493a9f1b0eac2b547118757cf276795146bdbf299e9c371475"
CHRONO = "af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18"
DJ = "95ab02d657213031b70c3079738b21be399e567ca8a32b1fe8e8effc0778eedb"
BANK = "2607c358e66f4bce4e81fa3748eaffe1205dcfb57010be2e90ce40fabeb8d3ec"
AUDIO = "0a7ca20a24239f4829ab5af7002004309d38d89ac5bffbfefff6a49b278c8841"
IL_PLAN = "5791ac3b27f500d8ae9f2cc72669b09aa7e490d17b33b992a2b55d0a8728b457"
CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
VANILLA_IOS = "2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357"
COMPAT = "48dc3df7886f1a32fe49d4cdecde2c692a95c7ec4aac345de522466408cdb7b0"
PROGRESSION_MANIFEST = "a8cddafb90f3698b821c34ecc8d3bb8d6674b03860436bfec27d902429f7ebac"
SCHEMA_SOURCE = "460c46df4863a16e8f8c7e88bb0f7413e415da46731c158bc09624af2d65d9b2"
MANAGED = "31bd37ee46719b5bd8c9913184a184d66971b889bcc9e0bcd3a55b20febdae04"
CONTENT_TREE = "f2b3d04ab37d4ce7a260777544939f5835307c9d92e127dab137535fadccd9e3"
SHARED = "f1345b601246a67e1d6bae9fdfc1c0d789540c945fc3308d720dab7fbb3f2d11"
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


def verify_ipa(c: Checks, ipa: pathlib.Path, platform: str, expected_hash: str) -> None:
    c.require(ipa.is_file(), f"{platform} IPA exists")
    c.require(sha(ipa.read_bytes()) == expected_hash, f"{platform} exact IPA hash")
    with zipfile.ZipFile(ipa) as archive:
        names = archive.namelist()
        apps = sorted({name.split("/")[1] for name in names
                       if name.startswith("Payload/") and name.count("/") >= 2})
        c.require(len(apps) == 1 and apps[0].endswith(".app"), f"{platform} exactly one app")
        info = plistlib.loads(archive.read(f"Payload/{apps[0]}/Info.plist"))
        expected_platform = ["iPhoneOS"] if platform == "ios" else ["AppleTVOS"]
        c.require(info["CFBundleSupportedPlatforms"] == expected_platform, f"{platform} platform tag")
        banks = [name for name in names if name.endswith(
            "/AppleEverest/Mods/ChronoHelper/Audio/ExpertContestHelper.bank")]
        c.require(len(banks) == 1 and sha(archive.read(banks[0])) == BANK,
                  f"{platform} exact custom bank packaged once")
        forbidden = ("Mono.Cecil", "MonoMod.Cil", "MonoMod.RuntimeDetour",
                     "MonoMod.Utils", "AppleEverestIlWorker")
        c.require(not any(any(token in name for token in forbidden) for name in names),
                  f"{platform} excludes host-only dynamic transformation material")
        if platform == "tvos":
            prefix = f"Payload/{apps[0]}/"
            c.require(prefix + "embedded.mobileprovision" not in names and
                      prefix + "_CodeSignature/CodeResources" not in names,
                      "tvOS diagnostic product is signing-ready unsigned")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--closure", type=pathlib.Path)
    parser.add_argument("--ios-ipa", type=pathlib.Path)
    parser.add_argument("--tvos-ipa", type=pathlib.Path)
    parser.add_argument("--packages", type=pathlib.Path)
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    required = {
        "audit": root / "apple-everest/levelset-progression-stage25ja.json",
        "codec": root / "apple-everest/runtime/AppleEverestProgressionSnapshotCodec.cs",
        "compression": root / "apple-everest/runtime/AppleEverestProgressionCompression.cs",
        "authority": root / "apple-everest/runtime/AppleEverestProgressionReplicaAuthority.cs",
        "persistence": root / "apple-everest/runtime/AppleEverestProgressionPersistence.cs",
        "runtime": root / "apple-everest/runtime/AppleEverestProgressionRuntime.cs",
        "api": root / "apple-everest/runtime/EverestStaticApi.cs",
        "area-key": root / "apple-everest/runtime/EverestAreaKeyStaticApi.cs",
        "static runtime": root / "apple-everest/runtime/AppleEverestStaticRuntime.cs",
        "compiler": root / "tools/AppleEverestBuilder/ContentCompiler.cs",
        "generator": root / "tools/AppleEverestBuilder/ClosureGenerator.cs",
        "tests": root / "tools/AppleEverestBuilder/tests/LevelSetProgressionTests.cs",
        "report": root / "docs/history/stages/APPLE_EVEREST_LEVELSET_PROGRESSION_STAGE25JA_REPORT.md",
    }
    for label, path in required.items():
        c.require(path.is_file(), f"required {label}")

    audit = json.loads(required["audit"].read_text())
    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25J-A", "audit schema")
    c.require(audit["status"] == "PASS_GREEN_IOS_IPADOS_TVOS_PHYSICAL_PENDING",
              "exact successful travel-stage status")
    c.require(audit["classification"] == "BOUNDED_STATIC_LEVELSET_PROGRESSION",
              "bounded compatibility classification")
    baseline = audit["baseline"]
    c.require(baseline["startingCommit"] == START and
              baseline["lastAllThreePhysicalGreenSha"] == START, "baseline and last all-device GREEN")
    c.require(baseline["featureBranch"] == "feature/apple-everest-levelset-progression",
              "feature branch identity")
    c.require(baseline["everestCommit"] == EVEREST and baseline["monoModCommit"] == MONOMOD,
              "pinned Everest/MonoMod")
    travel = audit["travelHardware"]
    c.require(travel["iphone"].startswith("AVAILABLE_") and travel["ipad"].startswith("AVAILABLE_"),
              "available iPhone/iPad are explicit")
    c.require(travel["appleTv"] == TVOS_PENDING, "exact Apple TV unavailable marker")

    semantics = audit["pinnedEverestSemantics"]
    c.require(len(semantics["persistentAreaProgression"]) == 14, "complete persistent AreaStats census")
    c.require("OldStats baseline" in semantics["resumableSession"] and
              semantics["separateModuleState"] == ["EverestModuleSaveData", "EverestModuleSession"],
              "Session baseline and module-state separation")
    little = audit["littleEpic"]
    c.require(little["zipSha256"] == LITTLE and little["mapSha256"] == MAP,
              "exact LittleEpic package/map")
    c.require(little["sid"] == "LittleEpic/precisionchallenge/precisionchallenge" and
              little["levelSet"] == "LittleEpic", "stable SID and LevelSet")
    c.require(little["rooms"] == ["1", "2", "3", "4", "5", "6", "7", "heart"],
              "exact room inventory")
    c.require(little["berries"] == 0 and little["heartGems"] == 1 and
              little["cassettes"] == 0 and little["persistentCheckpointObjects"] == 0,
              "real progression object census")

    architecture = audit["architecture"]
    c.require(architecture["selected"] == "HYBRID_VANILLA_BYTES_PLUS_TYPED_PROGRESSION_SIDECAR" and
              not architecture["vanillaSaveXmlModified"], "hybrid vanilla-safe architecture")
    c.require(not architecture["runtimeDiscovery"] and not architecture["reflectionSerializer"] and
              architecture["multipleMaps"] and architecture["multipleLevelSets"],
              "closed typed scalable schema")
    c.require("Debug" in architecture["debugLane"] and "numbered slot" in architecture["persistentLane"],
              "persistent and debug lanes separate")
    lineage = audit["identityAndLineage"]
    c.require("SID, LevelSet, source map SHA-256" in lineage["progressionCompatibilityIdentity"],
              "stable bounded map identity")
    c.require("256-bit random lineage" in lineage["numberedSaveOwner"] and
              lineage["slotRecreate"].startswith("fresh lineage"), "numbered-save lineage")
    c.require(not lineage["staleCrossSlotAttachment"] and
              "cannot select old progression" in lineage["importOrReplacement"],
              "slot/import stale-state protection")
    c.require(lineage["sameMapRebuildOrResign"] == "retained" and
              lineage["sameSidChangedMap"].startswith("quarantined"), "rebuild and map-change policy")

    ordering = audit["ordering"]
    c.require(ordering["save"][1:4] == ["commit vanilla save bytes",
              "commit and read-back progression generation", "commit Stage 25F module generation"],
              "vanilla/progression/module commit order")
    c.require(ordering["load"][2:4] == ["project custom AreaStats and custom map Session",
              "activate Stage 25F module SaveData and module Session"], "restore order")
    storage = audit["storage"]
    c.require(storage["codec"] == "AEVPSV1 typed little-endian binary plus SHA-256" and
              storage["codecSchemaVersion"] == 1, "typed schema v1")
    c.require(storage["ios"]["directory"] ==
              "Library/Application Support/Celeste/Everest/Progression/<slot>" and
              not storage["ios"]["documentsVisible"], "private iOS Application Support storage")
    c.require(storage["tvos"]["keyPattern"] ==
              "CelesteAppleEverest.Slot<0-2>.Progression.<A|B>.v1" and
              storage["tvos"]["replicasPerSlot"] == 2, "bounded tvOS keys/A-B")
    bounds = storage["bounds"]
    c.require(bounds == {"maximumLogicalBytes": 1048576, "maximumAreas": 128,
              "maximumModesPerArea": 3, "maximumCollectionItems": 16384,
              "maximumUtf8StringBytes": 4096, "unknownSchema": "reject",
              "malformedOrOversized": "isolate custom progression and continue with vanilla save"},
              "exact serializer bounds")
    observed = storage["observed"]
    c.require(observed == {"fixtureRawBytes": 1029, "fixtureTvosCompressedBytes": 437,
              "stressRawBytes": 164147, "stressTvosCompressedBytes": 5324,
              "stressMaps": 64, "stressLevelSets": 2}, "measured fixture/stress payloads")
    c.require(storage["tvos"]["maximumAllSlotsAbBytes"] == 761856, "complete tvOS A/B budget")
    recovery = audit["recovery"]
    c.require(recovery["tornNewest"].startswith("falls back") and
              recovery["bothCorrupt"].startswith("custom progression defaults empty") and
              "rehydrates" in recovery["softReload"], "A/B, corruption, and reload behavior")

    closure = audit["closure"]
    c.require(closure["independentRuns"] == 3 and closure["allIdentical"] and
              closure["transformerVersion"] == "apple-everest-static-v11", "three-run v11 determinism")
    c.require(closure["compatibilityManifestSha256"] == COMPAT and
              closure["progressionManifestSha256"] == PROGRESSION_MANIFEST and
              closure["generatedSchemaSourceSha256"] == SCHEMA_SOURCE, "generated progression locks")
    c.require(closure["managedLogicalSha256"] == MANAGED and
              closure["contentLogicalSha256"] == CONTENT_TREE and
              closure["sharedSha256"] == SHARED, "target-neutral closure locks")
    c.require(closure["sourceFree"] and closure["onePrePlatformClosure"], "one source-free closure")
    products = audit["products"]
    c.require(products["release"] and products["fullTrim"] and products["fullAot"] and
              not products["useInterpreter"] and not products["jit"], "Release full-AOT product policy")
    c.require(products["ios"]["signedIpaBytes"] > 800_000_000 and
              len(products["ios"]["signedIpaSha256"]) == 64, "signed iOS evidence")
    c.require(products["tvos"]["unsignedIpaBytes"] > 800_000_000 and
              len(products["tvos"]["unsignedIpaSha256"]) == 64, "unsigned tvOS evidence")
    physical = audit["physical"]
    c.require(physical["iphone"].startswith("PASS_") and physical["ipadOs15"].startswith("PASS_"),
              "iPhone and iPad physical GREEN")
    c.require(physical["appleTv"] == TVOS_PENDING, "Apple TV remains honestly pending")
    c.require(len(physical["futureAppleTvCatchUp"]) >= 10, "complete Apple TV catch-up contract")
    c.require(audit["githubActionsMinutesUsed"] == 0 and audit["developmentIntegrationReady"] and
              not audit["allPlatformReleaseEligible"], "travel-stage release boundary")

    locks = audit["locks"]
    c.require(locks["customBank"] == BANK and locks["customAudioManifest"] == AUDIO and
              locks["djIlPlan"] == IL_PLAN, "custom FMOD and frozen-IL locks")
    c.require(locks["canonicalContent"] == CONTENT and locks["canonicalRaw"] == RAW and
              locks["canonicalPatched"] == PATCHED and locks["stage6AudioTree"] == STAGE6,
              "canonical Celeste locks")
    c.require(locks["iosNative"] == IOS_NATIVE and locks["tvosNative"] == TVOS_NATIVE and
              locks["vanillaIosGeneratedSource"] == VANILLA_IOS, "native/vanilla locks")

    codec = required["codec"].read_text()
    compression = required["compression"].read_text()
    authority = required["authority"].read_text()
    persistence = required["persistence"].read_text()
    runtime = required["runtime"].read_text()
    compiler = required["compiler"].read_text()
    generator = required["generator"].read_text()
    tests = required["tests"].read_text()
    report = required["report"].read_text()
    c.require(all(token in codec for token in ("AEVPSV1", "MaximumBytes = 1024 * 1024",
              "OldStatsModes", "SHA256.HashData", "CryptographicOperations.FixedTimeEquals")),
              "typed deterministic authenticated codec")
    c.require(all(token in compression for token in ("AEVPZV1", "MaximumReplicaBytes = 126976",
              "DeflateStream", "MaximumTotalReplicaBytes")), "bounded tvOS compression")
    c.require(all(token in authority for token in ("SelectMatching", "OrderByDescending",
              "FixedTimeEquals", "GenerationA", "GenerationB")), "independent newest-valid authority")
    c.require("ApplicationSupportDirectory" in persistence and "Everest/Progression" in persistence and
              "#if TVOS" in persistence and "NSUserDefaults" in persistence and "Documents" not in persistence,
              "shared semantic persistence with narrow platform adapters")
    c.require("RandomNumberGenerator.GetBytes(32)" in persistence and
              "CommitCapturedSave" in persistence and "DeleteSlot" in persistence,
              "256-bit lineage, commit, and delete")
    c.require("SerializeVanillaBase" in runtime and "RemoveRange" in runtime and
              "OldStats = RestoreArea" in runtime, "vanilla boundary and exact Session baseline restore")
    c.require("TotalStrawberries" in runtime and "TotalHearts" in runtime and
              "TotalCassettes" in runtime and "TotalTime" in runtime and "TotalDeaths" in runtime,
              "static LevelSet aggregates")
    c.require("MapProgressionRecord InspectProgression" in compiler and
              "apple-everest-progression-map-v1" in compiler and
              "string compatibility = Hashing.BytesSha256" in compiler,
              "Mac-side stable map census")
    c.require("GeneratedAppleEverestProgressionManifest" in generator and
              "levelset-progression-manifest.txt" in generator and
              "AppleEverestProgressionPersistence.CaptureSave" in generator,
              "generated closed progression manifest and save bridge")
    c.require(generator.index("Save<SaveData>") < generator.index("CommitCapturedSave"),
              "progression commit follows vanilla save")
    c.require("Mode.SaveAndQuit" in generator and "Mode.Completed" in generator and
              "AppleEverestProgressionPersistence.DeleteSlot" in generator,
              "custom completion/save-and-quit/delete wiring")
    c.require("two-LevelSet/multi-map generic identity model" in tests and
              "corrupt both replicas isolates progression" in tests and
              "replacement base cannot inherit progression" in tests and
              "full AreaStats and Session baseline round trip" in tests,
              "deterministic identity/corruption/session tests")
    c.require(TVOS_PENDING in report and "PASS — GREEN_IOS_IPADOS / TVOS_PHYSICAL_PENDING" in report and
              "Apple TV" in report, "report states exact partial-physical status")
    c.require("PASS — GREEN\n" not in report and "ALL_PLATFORMS_PHYSICAL_GREEN" not in report,
              "report does not falsely claim all-platform physical GREEN")

    profiles = json.loads((root / "managed/celeste-input-profiles.json").read_text())
    canonical = profiles["canonicalClasses"]["celeste-1.4.0.0-a"]
    c.require(canonical["decompiledSource"]["logicalSha256"] == RAW and
              canonical["patchedSource"]["logicalSha256"] == PATCHED and
              canonical["stage6RealAudio"]["logicalSha256"] == STAGE6 and
              profiles["contentClasses"]["celeste-content-1.4.0.0-a"]["aggregateSha256"] == CONTENT,
              "tracked canonical locks unchanged")
    c.require(json.loads((root / "native/ios-native-output.lock.json").read_text())["logicalSetSha256"] == IOS_NATIVE and
              json.loads((root / "managed/celeste-generation.lock.json").read_text())["stage1NativeLogicalSha256"] == TVOS_NATIVE,
              "tracked native locks unchanged")
    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".bank", ".zip", ".dll", ".ipa", ".snapshot"))
                      for path in tracked), "no proprietary/product/progression bytes tracked")

    c.require(git(root, "merge-base", "--is-ancestor", START, "HEAD") == "", "starting commit ancestor")
    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC, "iOS recovery unchanged")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "RC1 unchanged")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "RC2 unchanged")
    c.require(git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3, "RC3 release ref unchanged")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 tag remains absent")
    historical = "docs/history/stages/APPLE_EVEREST_CUSTOM_FMOD_LITTLEEPIC_STAGE25IB_REPORT.md"
    c.require(subprocess.run(["git", "-C", str(root), "diff", "--quiet", START, "--", historical]).returncode == 0,
              "Stage 25I-B report byte-identical")

    if args.packages:
        package_root = args.packages.resolve()
        for name, expected in (("LittleEpic.zip", LITTLE), ("ChronoHelper.zip", CHRONO),
                               ("DJMapHelper.zip", DJ)):
            path = package_root / name
            c.require(path.is_file() and sha(path.read_bytes()) == expected, f"exact external {name}")

    if args.closure:
        closure_root = args.closure.resolve()
        manifest_path = closure_root / "compatibility-manifest.json"
        progression_path = closure_root / "levelset-progression-manifest.txt"
        schema_path = closure_root / "managed/GeneratedAppleEverestProgressionManifest.cs"
        c.require(manifest_path.is_file() and sha(manifest_path.read_bytes()) == COMPAT,
                  "exact generated compatibility manifest")
        manifest = json.loads(manifest_path.read_text())
        c.require(manifest["sharedClosureSha256"] == SHARED and
                  manifest["levelSetProgressionSchema"] == 1 and
                  manifest["levelSetProgressionManifestSha256"] == PROGRESSION_MANIFEST,
                  "generated closure/progression identity")
        c.require(progression_path.is_file() and sha(progression_path.read_bytes()) == PROGRESSION_MANIFEST,
                  "exact progression manifest")
        c.require(schema_path.is_file() and sha(schema_path.read_bytes()) == SCHEMA_SOURCE,
                  "exact generated typed map schema")

    if args.ios_ipa:
        verify_ipa(c, args.ios_ipa.resolve(), "ios", products["ios"]["signedIpaSha256"])
    if args.tvos_ipa:
        verify_ipa(c, args.tvos_ipa.resolve(), "tvos", products["tvos"]["unsignedIpaSha256"])

    print(f"PASS: Stage 25J-A LevelSet progression verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
