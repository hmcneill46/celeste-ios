#!/usr/bin/env python3
"""Verify Stage 25I-B bounded custom FMOD and LittleEpic closure invariants."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess
import zipfile


HD = "236d5fc957fb02d283a29ae4f55ef9753be042c2"
IA = "b1a20bf3fe7b4d20a50ef406e54e0f6d6445e73c"
EVEREST = "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00"
MONOMOD = "dfc30a1506d37fb88a2c2be004f525205f46a24c"
LITTLE = "ca57d295f446affdd8eb9b56ffe58f98cc652e9ec242ff07b224877d0067b384"
MAP = "6de18b4280df15493a9f1b0eac2b547118757cf276795146bdbf299e9c371475"
CHRONO = "af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18"
DJ = "95ab02d657213031b70c3079738b21be399e567ca8a32b1fe8e8effc0778eedb"
BANK = "2607c358e66f4bce4e81fa3748eaffe1205dcfb57010be2e90ce40fabeb8d3ec"
GUIDS = "db7f44d7ee1d79eb5efe734e595a6fb99937fd78e5cf9af91b0db6cabc4272a6"
AUDIO_MANIFEST = "0a7ca20a24239f4829ab5af7002004309d38d89ac5bffbfefff6a49b278c8841"
BANK_SET = "c927c0779c1dbbb5b43bd1a5daa5c2eb5e1c9d00b7e81eab87e70aa0a46eb8d7"
IL_PLAN = "5791ac3b27f500d8ae9f2cc72669b09aa7e490d17b33b992a2b55d0a8728b457"
SHARED = "f2eaa7c5304f84f82af2326e3e6182fa925c6b62747e2e59e3f0e31d6a6ed3b3"
CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
VANILLA_IOS = "2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357"
IOS_SIGNED = "34e5a082c0e3ec4502235db792c415bfd446c65bc0dc8e83eb9edd7e46734fca"
IOS_UNSIGNED = "06a61fc832554b40137262e7cf5b11a96244f73c15db89baf0d1fdfe420f073f"
TVOS_SIGNED = "82762b97428dded5b8f69697c2063eff8c260a980edf2b9aac1176e4f49afef4"
TVOS_UNSIGNED = "6eb4f837a52a2aeec898b18906c4d5eb4a64f218207ed115dc72b07c8d17ac87"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"


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


def verify_ipa(c: Checks, ipa: pathlib.Path, platform: str, unsigned: bool = False) -> None:
    c.require(ipa.is_file(), f"{platform} IPA exists")
    with zipfile.ZipFile(ipa) as archive:
        names = archive.namelist()
        bank_names = [name for name in names if name.endswith(
            "/AppleEverest/Mods/ChronoHelper/Audio/ExpertContestHelper.bank")]
        c.require(len(bank_names) == 1, f"{platform} exact bank packaged once")
        c.require(sha(archive.read(bank_names[0])) == BANK, f"{platform} packaged bank hash")
        apps = sorted({name.split("/")[1] for name in names
                       if name.startswith("Payload/") and name.count("/") >= 2})
        c.require(len(apps) == 1 and apps[0].endswith(".app"), f"{platform} one app")
        info_name = f"Payload/{apps[0]}/Info.plist"
        info = plistlib.loads(archive.read(info_name))
        c.require(info["CFBundleSupportedPlatforms"] ==
                  (["iPhoneOS"] if platform == "ios" else ["AppleTVOS"]),
                  f"{platform} platform tag")
        forbidden = ("Mono.Cecil", "MonoMod.Cil", "MonoMod.Utils",
                     "MonoMod.RuntimeDetour", "AppleEverestIlWorker")
        c.require(not any(any(token in name for token in forbidden) for name in names),
                  f"{platform} excludes host-only transformation material")
        if unsigned:
            prefix = f"Payload/{apps[0]}/"
            c.require(prefix + "embedded.mobileprovision" not in names,
                      f"{platform} unsigned product excludes provisioning profile")
            c.require(prefix + "_CodeSignature/CodeResources" not in names,
                      f"{platform} unsigned product excludes app signature resources")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--closure", type=pathlib.Path)
    parser.add_argument("--ios-ipa", type=pathlib.Path)
    parser.add_argument("--tvos-ipa", type=pathlib.Path)
    parser.add_argument("--ios-unsigned-ipa", type=pathlib.Path)
    parser.add_argument("--tvos-unsigned-ipa", type=pathlib.Path)
    parser.add_argument("--packages", type=pathlib.Path)
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    required = {
        "audit": root / "apple-everest/custom-fmod-littleepic-stage25ib.json",
        "models": root / "tools/AppleEverestBuilder/Models.cs",
        "audio host": root / "tools/AppleEverestBuilder/CustomAudioManifest.cs",
        "audio runtime": root / "apple-everest/runtime/AppleEverestCustomAudioRuntime.cs",
        "audio lifecycle": root / "apple-everest/runtime/AppleEverestCustomAudioLifecycle.cs",
        "closure": root / "tools/AppleEverestBuilder/ClosureGenerator.cs",
        "analyzer": root / "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs",
        "freezer": root / "tools/AppleEverestBuilder/StaticIlFreeze.cs",
        "tests": root / "tools/AppleEverestBuilder/tests/Program.cs",
        "audio canary": root / "apple-everest/canaries/custom-audio-content/Content/Maps/AppleEverest/CustomAudio.xml",
        "DJ canary": root / "apple-everest/canaries/dj-frozen-il-content/Content/Maps/AppleEverest/DJFrozenIl.xml",
        "architecture": root / "docs/APPLE_EVEREST_STATIC_AOT.md",
        "compatibility": root / "docs/APPLE_EVEREST_COMPATIBILITY.md",
        "report": root / "docs/history/stages/APPLE_EVEREST_CUSTOM_FMOD_LITTLEEPIC_STAGE25IB_REPORT.md",
    }
    for label, path in required.items():
        c.require(path.is_file(), f"required {label}")

    audit = json.loads(required["audit"].read_text())
    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25I-B", "audit schema")
    c.require(audit["status"] == "PASS_GREEN" and audit["integrationReady"] and
              audit["firstRealMultiCodeHelperMapGreen"], "final GREEN acceptance state")
    c.require(audit["classification"] == "STATIC_CUSTOM_FMOD_BANK", "bounded compatibility class")
    baseline = audit["baseline"]
    c.require(baseline["integrationCommit"] == HD and baseline["parentStage25IACommit"] == IA,
              "H-D/I-A ancestry locks")
    c.require(baseline["featureBranch"] == "feature/apple-everest-custom-fmod-littleepic",
              "feature branch identity")
    c.require(baseline["everestCommit"] == EVEREST and baseline["monoModCommit"] == MONOMOD,
              "Everest/MonoMod pins")

    inputs = audit["inputs"]
    c.require(inputs["littleEpic"]["zipSha256"] == LITTLE and
              inputs["littleEpic"]["mapSha256"] == MAP, "exact root/map")
    c.require(inputs["littleEpic"]["startingRoom"] == "1" and
              inputs["littleEpic"]["startingSpawn"] == {"x": 160, "y": 176, "entityId": 0},
              "real room-one spawn")
    c.require(inputs["chronoHelper"]["version"] == "1.3.3" and
              inputs["chronoHelper"]["zipSha256"] == CHRONO, "exact Chrono")
    c.require(inputs["djMapHelper"]["version"] == "1.13.4" and
              inputs["djMapHelper"]["zipSha256"] == DJ, "exact DJ")

    audio = audit["audio"]
    c.require(audio["nativeVersion"] == "1.10.09 build 97915" and
              audio["managedBindingVersion"] == "1.10.20 (0x00011014)" and
              not audio["versionsChanged"], "unchanged native/managed FMOD versions")
    c.require(audio["studioSystemCreationPaths"] == 1 and not audio["secondFmodSystem"],
              "one existing Studio system")
    c.require(audio["loadApi"] == "FMOD.Studio.System.loadBankFile" and
              audio["sampleDataPolicy"] == "event-description-lazy", "file load and sample policy")
    c.require(audio["bankSourcePath"] == "Audio/ExpertContestHelper.bank" and
              audio["bankStagedPath"] ==
              "AppleEverest/Mods/ChronoHelper/Audio/ExpertContestHelper.bank", "fixed bank paths")
    c.require(audio["bankSha256"] == BANK and audio["guidSha256"] == GUIDS,
              "bank/GUID byte locks")
    records = audio["records"]
    c.require(len(records) == 3 and [record["kind"] for record in records] ==
              ["event", "event", "bank"], "complete three-record GUID table")
    expected = {
        "event:/ricky06/EC2023/horn": "33eab85e-7e13-417e-ab3b-a0b7c8caa6b2",
        "event:/ricky06/zip_mover 2": "22f6b410-423f-4c15-a6b5-0523b16ab5fd",
        "bank:/ExpertContestHelper": "f12a5c05-a79b-4ed0-bea9-81a1d2ecb986",
    }
    c.require({record["path"]: record["guid"] for record in records} == expected,
              "exact event/bank GUID mappings")
    c.require(audio["manifestSha256"] == AUDIO_MANIFEST and
              audio["bankLogicalSetSha256"] == BANK_SET, "audio closure hashes")
    c.require(audio["collisions"] == 0 and not audio["runtimeBankScan"] and
              not audio["runtimeModScan"], "zero collisions/scans")
    c.require(not audio["newNativeLibrary"] and
              audio["duplicateLoadPolicy"] == "idempotent-per-system-identity", "native/idempotence policy")

    graph = audit["graph"]
    c.require(graph["blockersBefore"] == {"unclassified": 0, "unsupportedRequired": 1} and
              graph["blockersAfter"] == {"unclassified": 0, "unsupportedRequired": 0},
              "single blocker closed without new blockers")
    c.require(graph["room1Mechanic"] == "DJMapHelper/maxDashesTrigger dashes=Zero" and
              graph["room5Mechanic"] == "ChronoHelper/CustomTimeSwitchGates moveTime=17",
              "real helper mechanics")
    c.require(graph["customMapProgression"] == "NONPERSISTENT_DEBUG_LANE" and
              graph["saveAndQuit"] == "SUPPRESSED_IN_CUSTOM_MAP_DEBUG_LANE",
              "honest custom-map progression boundary")
    il = audit["staticIl"]
    c.require(il["registrationCount"] == 5 and il["targetBodyCount"] == 3 and
              il["emitDelegateCount"] == 51 and il["planSha256"] == IL_PLAN, "frozen IL locks")
    c.require(il["planIds"] == ["DJMapHelper:player-h-feather", "DJMapHelper:player-h-theo",
              "DJMapHelper:player-v-feather", "DJMapHelper:player-v-theo",
              "DJMapHelper:fling-awake"], "five exact IL registrations")

    closure = audit["closure"]
    c.require(closure["independentRuns"] == 3 and closure["allIdentical"], "three-run determinism")
    c.require(closure["sharedSha256"] == SHARED and closure["sourceFree"] and
              closure["onePrePlatformClosure"], "shared source-free closure")
    c.require(closure["managedDetourCatalogCount"] == 102 and
              closure["reviewedApiMemberCount"] == 30, "catalog/API counts")
    c.require(len({closure[key] for key in ("manifestSha256", "managedSha256", "contentSha256",
              "frozenAssemblySha256", "registrySha256", "hookTransformSha256",
              "reviewedApiSha256", "modInteropPlanSha256")}) == 8, "distinct complete closure locks")

    products = audit["products"]
    c.require(products["ios"]["signedIpaSha256"] == IOS_SIGNED and
              products["ios"]["unsignedIpaSha256"] == IOS_UNSIGNED,
              "iOS signed/unsigned product locks")
    c.require(products["tvos"]["signedIpaSha256"] == TVOS_SIGNED and
              products["tvos"]["unsignedIpaSha256"] == TVOS_UNSIGNED,
              "tvOS signed/unsigned product locks")
    c.require(products["fullTrim"] and products["fullAot"] and
              not products["useInterpreter"] and not products["jit"],
              "full trim/AOT without interpreter or JIT")
    physical = audit["physical"]
    c.require(all(str(physical[key]).startswith("PASS_") for key in
              ("iphone", "ipadOs15", "appleTv", "horn", "room1", "room5", "djFrozenIl", "lifecycle")),
              "three-device and mechanic physical acceptance")
    regressions = audit["regressions"]
    c.require(regressions["appleEverestBuilder"] == "PASS_323" and
              regressions["stage25ibVerifier"] == "PASS_117_WITH_SIGNED_AND_UNSIGNED_PRODUCTS",
              "current deterministic/product counts")
    c.require(regressions["stage25iaVerifier"] == "PASS_115_YELLOW_GATE_RETAINED" and
              regressions["stage25hdVerifier"] == "PASS_163" and
              regressions["stage25hcVerifier"] == "PASS_120", "current predecessor verifiers")
    c.require(regressions["typedHookSemantics"] == "PASS_48" and
              regressions["pinnedModInteropReference"] == "PASS_14" and
              regressions["desktopHookGenAndDirectHook"] == "PASS" and
              regressions["realPinnedIlFreeze"] == "PASS", "runtime-semantics regressions")
    c.require(regressions["inputProfiles"] == "PASS_56" and
              regressions["repositoryDocsPrivacy"] == "PASS" and
              regressions["historicalVerifierPolicy"].startswith("OLDER_MODEL_VERSION"),
              "input/privacy and historical-verifier policy")
    locks = audit["locks"]
    c.require(locks == {
        "canonicalContent": CONTENT,
        "canonicalRaw": RAW,
        "canonicalPatched": PATCHED,
        "stage6AudioTree": STAGE6,
        "iosNative": IOS_NATIVE,
        "tvosNative": TVOS_NATIVE,
        "vanillaIosGeneratedSource": VANILLA_IOS,
    }, "canonical/native/vanilla lock set")
    profiles = json.loads((root / "managed/celeste-input-profiles.json").read_text())
    canonical = profiles["canonicalClasses"]["celeste-1.4.0.0-a"]
    c.require(canonical["decompiledSource"]["logicalSha256"] == RAW and
              canonical["patchedSource"]["logicalSha256"] == PATCHED and
              canonical["stage6RealAudio"]["logicalSha256"] == STAGE6 and
              profiles["contentClasses"]["celeste-content-1.4.0.0-a"]["aggregateSha256"] == CONTENT,
              "tracked canonical locks unchanged")
    ios_native = json.loads((root / "native/ios-native-output.lock.json").read_text())
    generation = json.loads((root / "managed/celeste-generation.lock.json").read_text())
    c.require(ios_native["logicalSetSha256"] == IOS_NATIVE and
              generation["stage1NativeLogicalSha256"] == TVOS_NATIVE,
              "tracked native locks unchanged")
    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".bank", ".zip", ".dll", ".ipa")) for path in tracked),
              "no third-party/product binary tracked")

    host = required["audio host"].read_text()
    runtime = required["audio runtime"].read_text()
    lifecycle = required["audio lifecycle"].read_text()
    generator = required["closure"].read_text()
    analyzer = required["analyzer"].read_text()
    tests = required["tests"].read_text()
    c.require(all(token in host for token in (CHRONO, BANK, GUIDS, "ExpectedBankPath",
              "ValidateGraph", "custom FMOD GUID collision")), "host exact validation/collision policy")
    c.require("ParseGuidTable" in host and "Directory.EnumerateFiles" not in host,
              "Mac parser without runtime discovery")
    c.require("system.loadBankFile" in runtime and "System.create" not in runtime and
              "Directory.EnumerateFiles" not in runtime, "existing system file-load implementation")
    c.require("identity=guid-manifest" in runtime and "getEventByID" in runtime and
              "bank.getPath" not in runtime, "stringless bank uses exact GUID identity")
    c.require("TryGetEventDescription" in runtime and "RecordEventRequest" in runtime,
              "normal event lookup integration")
    c.require("BeginLoad" in lifecycle and "SoftReload" in lifecycle and
              "BeforeSystemUnload" in lifecycle, "deterministic lifecycle state machine")
    c.require("custom-audio-manifest.txt" in generator and "customAudioManifestSha256" in generator and
              "customBankLogicalSetSha256" in generator, "generated audio manifest/closure identity")
    c.require("STATIC_CUSTOM_FMOD_BANK" in analyzer and "CUSTOM_AUDIO_UNSUPPORTED" in analyzer,
              "recognized exact class and unknown-bank rejection")
    c.require("djmaphelper_littleepic_fixture_sha" in tests and
              "malformed custom FMOD GUID rejected" in tests and
              "duplicate custom FMOD event path rejected" in tests and
              "cross-module custom FMOD GUID collision rejected" in tests,
              "deterministic DJ/audio negative tests")
    c.require("System.create" not in required["audio lifecycle"].read_text(), "lifecycle creates no FMOD system")
    c.require("ChronoHelper/ExplodingPinata" in required["audio canary"].read_text(),
              "real Chrono custom-audio canary")
    dj_canary = required["DJ canary"].read_text()
    c.require("DJMapHelper/colorfulFlyFeather" in dj_canary and
              "DJMapHelper/featherBarrier" in dj_canary, "real DJ frozen-IL canary")

    c.require(git(root, "merge-base", "--is-ancestor", HD, "HEAD") == "", "H-D ancestor")
    c.require(git(root, "merge-base", "--is-ancestor", IA, "HEAD") == "", "I-A ancestor")
    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC, "iOS RC unchanged")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "RC1 unchanged")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "RC2 unchanged")
    c.require(git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3, "RC3 release ref unchanged")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 tag absent")
    historical = "docs/history/stages/APPLE_EVEREST_LITTLEEPIC_STAGE25IA_REPORT.md"
    c.require(subprocess.run(["git", "-C", str(root), "diff", "--quiet", IA, "--", historical]).returncode == 0,
              "Stage 25I-A report byte-identical")

    if args.packages:
        package_root = args.packages.resolve()
        for name, expected_hash in (("LittleEpic.zip", LITTLE), ("ChronoHelper.zip", CHRONO),
                                    ("DJMapHelper.zip", DJ)):
            path = package_root / name
            c.require(path.is_file() and sha(path.read_bytes()) == expected_hash, f"exact external {name}")
        with zipfile.ZipFile(package_root / "ChronoHelper.zip") as archive:
            bank_names = [name for name in archive.namelist() if name == "Audio/ExpertContestHelper.bank"]
            guid_names = [name for name in archive.namelist() if name == "Audio/ExpertContestHelper.guids.txt"]
            c.require(len(bank_names) == len(guid_names) == 1, "one bank/GUID file in Chrono ZIP")
            c.require(sha(archive.read(bank_names[0])) == BANK and sha(archive.read(guid_names[0])) == GUIDS,
                      "bank/GUID bytes sourced directly from Chrono ZIP")

    if args.closure:
        manifest_path = args.closure.resolve() / "compatibility-manifest.json"
        c.require(manifest_path.is_file(), "generated compatibility manifest")
        manifest = json.loads(manifest_path.read_text())
        c.require(manifest["sharedClosureSha256"] == SHARED, "generated shared closure lock")
        c.require(manifest["customAudioManifestSha256"] == AUDIO_MANIFEST and
                  manifest["customBankLogicalSetSha256"] == BANK_SET, "generated audio locks")
        c.require(manifest["customAudioBankCount"] == 1 and manifest["customAudioEventCount"] == 2,
                  "one bank/two events")
        c.require(manifest["frozenIlPlanSha256"] == IL_PLAN and
                  manifest["managedDetourTargetCount"] == 102, "generated IL/catalog locks")

    if args.ios_ipa:
        c.require(sha(args.ios_ipa.read_bytes()) == IOS_SIGNED, "iOS signed IPA hash")
        verify_ipa(c, args.ios_ipa.resolve(), "ios")
    if args.tvos_ipa:
        c.require(sha(args.tvos_ipa.read_bytes()) == TVOS_SIGNED, "tvOS signed IPA hash")
        verify_ipa(c, args.tvos_ipa.resolve(), "tvos")
    if args.ios_unsigned_ipa:
        c.require(sha(args.ios_unsigned_ipa.read_bytes()) == IOS_UNSIGNED, "iOS unsigned IPA hash")
        verify_ipa(c, args.ios_unsigned_ipa.resolve(), "ios", unsigned=True)
    if args.tvos_unsigned_ipa:
        c.require(sha(args.tvos_unsigned_ipa.read_bytes()) == TVOS_UNSIGNED, "tvOS unsigned IPA hash")
        verify_ipa(c, args.tvos_unsigned_ipa.resolve(), "tvos", unsigned=True)

    print(f"PASS: Stage 25I-B custom FMOD/LittleEpic verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
