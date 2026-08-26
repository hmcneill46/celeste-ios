#!/usr/bin/env python3
"""Verify Stage 25K-A's first real static-AOT CollabUtils2 collab."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess
import zipfile


START = "9f3dcaa75ef14ff02ed09cdad4b0b669c5d0c198"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
TVOS_PENDING = "TVOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE"
PORT_VERSION = "0.1.1"
PORT_BUILD = "17"

COLLAB_ZIP = "638ad7beac7a24600c7c0733acf12a645795bd8878fd5bc7c88e6e24b17dce39"
COLLAB_SOURCE = "73785a6c562fcb16fc1738840483da0c35526c80344adee9fc0180baa49a4ed2"
LOBBY_SOURCE = "6bffc2c2cc5d077527051322cfccab8916b33cefc253fc77cb1c7614377e8d2c"
LOBBY_STAGED = "e3b7b2e817424ab7835d422104a1957cfee4069aa70454ec74cb9ebd81f0ce01"
LOBBY_COMPAT = "293cc93cdb340dd32eabcbaac4e96fb4504c57dac0b12334113f95e9712d012c"
MAP_A_SOURCE = "072634ac7290cb765db958c9fca345c7a14d877cf57154dd16ad7322a222c8aa"
MAP_A_STAGED = "02674e201dd7cb1b20067eedd88c8241e275de6e9f2b96dd1d970870088502f8"
MAP_A_COMPAT = "0a0a36f63a4294380c50ea94baee48191fddec96963f33f92685c80cd5cfb095"
MAP_B_SOURCE = "f9afb1908a4d7e47ba29b778fab4e924147c2bae3a84686c0e81b76b5105ebc3"
MAP_B_STAGED = "ab358aa423948180698d6d5c10166031f36f94062335ed7f9a8d9f8385488dbc"
MAP_B_COMPAT = "419a5bae9425df172b32515079add4f89f0b1e81290d03f55f76cd6474bb077d"

SHARED = "ccf4f6db802c935071d019b46fbb77d3be49445f4bb590d4e9a1872c9a4b0660"
MANAGED = "35d9ccb691ce1a0134bdf7df963661ce43ed31e8f6a7a0c8eedcacc7fe1607e4"
CONTENT_TREE = "0947c2e4156c02ba2bbdf37681b202378ad52432b9dabcd9dadb71a945977947"
REGISTRY = "d7ff46c8ff66d0e5928723494ac65f4b08bfb23c110b621caeb30c2261b65ad1"
HOOKS = "590af232c6c7209c9295f3f166478ad44c5bf559da22c72c01e45585fabf66f7"
API = "d594dea86e944b468819818773f2177312445bbebe022bcbc7ea1ff779c8652c"
PROGRESSION = "55796d3e95a64959f8ddd58e2cf001c06f0b8207f4d37118b60a96aa9d2d7072"
LEVELSET = "c9ecdcc99221ff03926d282dada049505bd416f66bc1464fa2847d24c36771ba"
COLLAB = "6925d8780b6427a67ef5438bef8315b80d4707506b5ebe7e61f9fe3c81f928a0"
AUDIO = "0a7ca20a24239f4829ab5af7002004309d38d89ac5bffbfefff6a49b278c8841"
BANK_SET = "c927c0779c1dbbb5b43bd1a5daa5c2eb5e1c9d00b7e81eab87e70aa0a46eb8d7"
IL_PLAN = "5791ac3b27f500d8ae9f2cc72669b09aa7e490d17b33b992a2b55d0a8728b457"

CANONICAL_CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
CANONICAL_RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
CANONICAL_PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
VANILLA_IOS = "2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
CHRONO_BANK = "2607c358e66f4bce4e81fa3748eaffe1205dcfb57010be2e90ce40fabeb8d3ec"


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
    expected = audit["products"][platform]
    c.require(ipa.is_file(), f"{platform} IPA exists")
    c.require(ipa.stat().st_size == expected["ipaBytes"] and sha(ipa.read_bytes()) == expected["ipaSha256"],
              f"{platform} exact accepted IPA")
    with zipfile.ZipFile(ipa) as archive:
        names = archive.namelist()
        apps = sorted({name.split("/")[1] for name in names
                       if name.startswith("Payload/") and name.count("/") >= 2})
        c.require(len(apps) == 1 and apps[0].endswith(".app"), f"{platform} one app")
        prefix = f"Payload/{apps[0]}/"
        info = plistlib.loads(archive.read(prefix + "Info.plist"))
        c.require(info.get("CFBundleShortVersionString") == expected["portVersion"] == PORT_VERSION and
                  str(info.get("CFBundleVersion")) == str(expected["buildNumber"]) == PORT_BUILD,
                  f"{platform} shared Apple port version/build identity")
        c.require(info["CFBundleSupportedPlatforms"] ==
                  (["iPhoneOS"] if platform == "ios" else ["AppleTVOS"]), f"{platform} platform")
        c.require(info.get("UIDeviceFamily") == ([1, 2] if platform == "ios" else [3]),
                  f"{platform} device family")
        maps = (
            "Maps/HennyburgrCompEntries/0-Lobbies/lobby.bin",
            "Maps/HennyburgrCompEntries/1-Lobby/redboostercomp.bin",
            "Maps/HennyburgrCompEntries/1-Lobby/stationmovers.bin",
        )
        c.require(all(sum(name.endswith("/" + item) for name in names) == 1 for item in maps),
                  f"{platform} contains exact lobby/map payload once")
        c.require(not any(name.lower().endswith(("collabutils2.dll", "communalhelper.dll",
                                                "maxhelpinghand.dll", "lunatichelper.dll",
                                                "shroomhelper.dll", "frosttemplehelper.dll")) for name in names),
                  f"{platform} excludes statically lowered helper DLLs")
        c.require(not any(any(token in name for token in ("Mono.Cecil", "MonoMod.Cil",
                  "MonoMod.RuntimeDetour", "MonoMod.Utils", "AppleEverestIlWorker")) for name in names),
                  f"{platform} excludes host/runtime transformation machinery")
        celeste_dlls = [name for name in names if name.endswith("/Celeste.dll")]
        visible_identity = f'{"iOS" if platform == "ios" else "tvOS"} PORT v{PORT_VERSION}  •  BUILD {PORT_BUILD}'
        c.require(len(celeste_dlls) == 1 and
                  visible_identity.encode("utf-16le") in archive.read(celeste_dlls[0]),
                  f"{platform} visible Options build identity")
        if platform == "ios":
            c.require(prefix + "embedded.mobileprovision" in names and
                      prefix + "_CodeSignature/CodeResources" in names,
                      "iOS development-signed universal package")
        else:
            c.require(prefix + "embedded.mobileprovision" not in names and
                      prefix + "_CodeSignature/CodeResources" not in names,
                      "tvOS signing-ready unsigned package")


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
        "candidate audit": root / "apple-everest/first-real-collab-candidates-stage25ka.json",
        "acceptance audit": root / "apple-everest/first-real-collab-stage25ka.json",
        "collab generator": root / "tools/AppleEverestBuilder/CollabManifestGenerator.cs",
        "semantic lowering": root / "tools/AppleEverestBuilder/StaticSemanticLowering.cs",
        "semantic factories": root / "apple-everest/runtime/AppleEverestSemanticFactories.cs",
        "collab runtime": root / "apple-everest/runtime/AppleEverestCollabRuntime.cs",
        "progression runtime": root / "apple-everest/runtime/AppleEverestProgressionRuntime.cs",
        "tests": root / "tools/AppleEverestBuilder/tests/CollabStaticTests.cs",
        "fixture fetch": root / "scripts/fetch-apple-everest-stage25ka-fixtures.sh",
        "canary builder": root / "scripts/build-apple-everest-canary.sh",
        "port identity patch": root / "scripts/apply-apple-port-build-identity.py",
        "port version source": root / "modern-ios/IOSPortVersion.props",
        "tvOS runtime project": root / "tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj",
        "tvOS runtime plist": root / "tvos/CelesteTvOSRuntimeHost/Info.plist",
        "report": root / "docs/history/stages/APPLE_EVEREST_FIRST_REAL_COLLAB_STAGE25KA_REPORT.md",
    }
    for label, path in required.items():
        c.require(path.is_file(), f"required {label}")

    candidate = json.loads(required["candidate audit"].read_text())
    audit = json.loads(required["acceptance audit"].read_text())
    c.require(candidate["schemaVersion"] == 1 and candidate["stage"] == "25K-A", "candidate schema")
    c.require(candidate["screening"]["metadataRecords"] == 429 and
              candidate["screening"]["credibleSmallCollabReleases"] == 47 and
              len(candidate["screening"]["candidates"]) == 47, "candidate screening breadth")
    c.require(candidate["deepAudit"]["count"] == 18 and
              len(candidate["deepAudit"]["candidates"]) == 18, "deep audit breadth")
    selected = candidate["selected"]
    c.require(selected["zipSha256"] == COLLAB_ZIP and selected["sourceLogicalSha256"] == COLLAB_SOURCE and
              selected["ordinaryUnmodifiedPublicRelease"], "selected ordinary release identity")
    c.require(selected["collabId"] == "HennyburgrCompEntries" and
              selected["lobbySid"] == "HennyburgrCompEntries/0-Lobbies/lobby" and
              selected["subordinateMapSids"] == ["HennyburgrCompEntries/1-Lobby/redboostercomp",
                                                  "HennyburgrCompEntries/1-Lobby/stationmovers"],
              "selected one-lobby/two-map identity")
    c.require(selected["managedPayloads"] == selected["customFmodBanks"] == selected["luaFiles"] ==
              selected["nativePayloads"] == 0, "selected content-only boundary")

    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25K-A", "acceptance schema")
    c.require(audit["status"] == "PASS_GREEN_IOS_IPADOS_TVOS_PHYSICAL_PENDING" and
              audit["classification"] == "FIRST_REAL_COLLAB_STATIC_AOT", "exact travel status")
    baseline = audit["baseline"]
    c.require(baseline["startingCommit"] == START and
              baseline["featureBranch"] == "feature/apple-everest-first-real-collab", "baseline")
    c.require(baseline["iosRecoveryCommit"] == IOS_RC and baseline["rc1"] == RC1 and
              baseline["rc2"] == RC2 and baseline["rc3ReleaseBranch"] == RC3 and
              baseline["rc3TagAbsent"], "protected refs recorded")

    collab = audit["collab"]
    c.require(collab["name"] == "hennyburgr's Mapping Competition Entries" and
              collab["version"] == "1.1.0" and collab["zipSha256"] == COLLAB_ZIP and
              collab["sourceLogicalSha256"] == COLLAB_SOURCE, "accepted collab exact identity")
    c.require(collab["collabId"] == "HennyburgrCompEntries" and collab["lobbyCount"] == 1 and
              collab["subordinateMapCount"] == 2, "one real collab/lobby/two maps")
    lobby = collab["lobby"]
    c.require(lobby["sid"] == "HennyburgrCompEntries/0-Lobbies/lobby" and
              lobby["sourceMapSha256"] == LOBBY_SOURCE and lobby["stagedMapSha256"] == LOBBY_STAGED and
              lobby["compatibilityId"] == LOBBY_COMPAT and lobby["rooms"] == ["a-01"], "exact lobby")
    maps = collab["maps"]
    c.require([m["sourceMapSha256"] for m in maps] == [MAP_A_SOURCE, MAP_B_SOURCE] and
              [m["stagedMapSha256"] for m in maps] == [MAP_A_STAGED, MAP_B_STAGED] and
              [m["compatibilityId"] for m in maps] == [MAP_A_COMPAT, MAP_B_COMPAT], "exact subordinate maps")
    c.require([m["sid"] for m in maps] == ["HennyburgrCompEntries/1-Lobby/redboostercomp",
              "HennyburgrCompEntries/1-Lobby/stationmovers"] and
              [m["rooms"] for m in maps] == [["hennyburgr_1"], ["hennyburgr_01"]], "map SIDs/rooms")
    c.require(all(m["levelSet"] == "HennyburgrCompEntries/1-Lobby" and
                  m["allowSaving"] and m["returnToLobbyMode"] == "SetReturnToHere" for m in maps),
              "both maps genuinely wired to the lobby")

    helpers = audit["helperGraph"]
    c.require(helpers["resolvedOrder"] == ["ChronoHelper", "AppleEverestCustomAudioCanary", "DJMapHelper",
              "AppleEverestDJFrozenIlContentCanary", "CollabUtils2", "CommunalHelper", "FancyTileEntities",
              "FearoftheDark", "FrostHelper", "LunaticHelper", "MaxHelpingHand", "ShroomHelper",
              "HennyburgrCompEntries", "LittleEpic's Precision Challenge", "Torremolinos Speedbuild"],
              "stable complete helper/load order")
    exact_helpers = {value["name"]: value for value in helpers["staticallyLoweredHelpers"]}
    c.require(exact_helpers["CollabUtils2"]["version"] == "1.13.4" and
              exact_helpers["CollabUtils2"]["sourceSha256"] ==
              "b987d25608874623453e2c75c441661b906d6fd907948699983c2f12ba14c88e" and
              exact_helpers["CollabUtils2"]["dllSha256"] ==
              "ce7d646252a2fc51f1f513071d97eaa1acedf7ddb0871276c688914e37319f60",
              "exact CollabUtils2 source/DLL")
    c.require(len(exact_helpers) == 7 and all(value["hashLocked"] for value in exact_helpers.values()),
              "seven exact static semantic helper profiles")

    binary = audit["collabUtils2BinaryCensus"]
    c.require(binary["releaseVersion"] == "1.13.4" and
              binary["dllSha256"] == "ce7d646252a2fc51f1f513071d97eaa1acedf7ddb0871276c688914e37319f60" and
              binary["typeDefinitions"] == 194 and binary["methodDefinitions"] == 1062,
              "complete pinned CollabUtils2 binary identity/census")
    c.require(binary["onCelesteMethodReferenceInstructions"] == 347 and
              binary["ilCelesteMethodReferenceInstructions"] == 22 and
              binary["directHookConstructions"] == 9 and binary["directIlHookConstructions"] == 19 and
              binary["emitDelegateMethodReferences"] == 43,
              "CollabUtils2 hook/IL/detour census")
    c.require(binary["dynamicDataMethodReferences"] == 0 and
              binary["processMethodReferences"] == 0 and binary["fileSystemWatcherMethodReferences"] == 0 and
              binary["pinvokeMethods"] == 0 and binary["customEntityAttributes"] == 20 and
              binary["customBackdropAttributes"] == 0,
              "CollabUtils2 dynamic/native/factory census")

    census = audit["compatibilityCensus"]
    c.require(census["zeroUnclassifiedBlockers"] and census["zeroUnsupportedRequiredBlockers"] and
              not census["newMajorMechanism"], "zero blockers/no new major mechanism")
    c.require(census["hookGenTargetCountBefore"] == census["hookGenTargetCountAfter"] == 102 and
              census["newHookGenDescriptors"] == [] and census["frozenIlPlanSha256"] == IL_PLAN and
              census["frozenIlTransformCount"] == 5 and census["newFrozenIl"] == [] and
              census["directIlHookCount"] == 0 and not census["configuredIl"], "unchanged HookGen/IL")
    c.require(not census["generalDynamicData"] and census["modInteropRegistrationCount"] == 0 and
              not census["lua"] and not census["nativeOrPInvoke"] and
              census["customFmodBanksAdded"] == 0, "no dynamic/native/audio expansion")
    c.require(census["staticSemanticFactoryCount"] == 16 and census["legacyFactoryCount"] == 59 and
              census["coreGameplayFactoryCount"] == 4 and census["staticFactoriesOnly"],
              "bounded static factory surface")
    c.require(census["runtimeDllLoading"] is False and census["runtimeDetour"] == "absent" and
              not census["deviceModScanning"] and not census["reflectionFactoryDiscovery"],
              "no device-side dynamic loading/discovery")

    features = audit["collabUtils2Features"]
    c.require("CollabUtils2CollabID.txt build-time discovery" in features["required"] and
              "ChapterPanelTrigger" in features["required"] and "JournalTrigger" in features["required"] and
              "MiniHeart" in features["required"] and "lobby completion flags" in features["required"] and
              "Return to Lobby and exact lobby respawn" in features["required"] and
              features["supportedUnused"] == [] and "HeartDoor" in features["deferred"] and "SilverBerry" in features["deferred"] and
              "RainbowBerry" in features["deferred"] and "SpeedBerry" in features["deferred"],
              "explicit CollabUtils2 feature boundary")

    progression = audit["progression"]
    c.require(progression["schema"] == "AEVPSV1" and progression["schemaVersion"] == 1 and
              not progression["migrationRequired"] and not progression["newCollabSpecificDurableSchema"],
              "AEVPSV1 unchanged")
    c.require(progression["perMapIsolation"] and progression["slotIsolation"] and
              progression["deleteRecreate"] and progression["importedSaveIsolation"] and
              progression["corruptionFallback"], "durability isolation/recovery")
    c.require(progression["rawBytes"] == 3189 and progression["tvosCompressedBytes"] == 984 and
              progression["tvosReplicaCapPercent"] == 0.77 and
              progression["maximumThreeSlotAbBytes"] == 761856, "bounded progression size")

    deterministic = audit["determinism"]
    expected = {"sharedClosureSha256": SHARED, "managedLogicalSha256": MANAGED,
                "contentLogicalSha256": CONTENT_TREE, "registrySha256": REGISTRY,
                "hookTransformSha256": HOOKS, "appleApiSurfaceSha256": API,
                "progressionManifestSha256": PROGRESSION, "levelSetManifestSha256": LEVELSET,
                "collabManifestSha256": COLLAB, "customAudioManifestSha256": AUDIO,
                "customBankLogicalSetSha256": BANK_SET, "frozenIlPlanSha256": IL_PLAN}
    c.require(deterministic["independentRuns"] == 3 and deterministic["allIdentical"] and
              deterministic["transformerVersion"] == "apple-everest-static-v14", "three-run determinism")
    c.require(all(deterministic[key] == value for key, value in expected.items()), "complete closure locks")
    c.require(deterministic["managedFileCount"] == 46 and deterministic["contentFileCount"] == 4461 and
              deterministic["collabCount"] == 1 and deterministic["collabMapCount"] == 2 and
              deterministic["levelSetProgressionMapCount"] == 9 and deterministic["levelSetCount"] == 6,
              "complete closure census")

    products = audit["products"]
    c.require(products["release"] and products["fullTrim"] and products["fullAot"] and
              not products["useInterpreter"] and not products["jit"] and products["sharedPrePlatformClosure"],
              "Release full-AOT products")
    c.require(products["ios"]["universalIphoneIpad"] and products["ios"]["nativeIpadPresentation"] and
              products["ios"]["developmentSigned"] and products["tvos"]["signingReadyUnsigned"],
              "universal signed iOS and unsigned tvOS products")
    c.require(all(product["portVersion"] == PORT_VERSION and str(product["buildNumber"]) == PORT_BUILD
                  for product in (products["ios"], products["tvos"])),
              "one audited Apple port semantic version/build")

    version_source = required["port version source"].read_text()
    tvos_project = required["tvOS runtime project"].read_text()
    tvos_plist = required["tvOS runtime plist"].read_text()
    identity_patch = required["port identity patch"].read_text()
    canary_builder = required["canary builder"].read_text()
    c.require(f"<IOSPortSemanticVersion>{PORT_VERSION}</IOSPortSemanticVersion>" in version_source and
              f"<IOSPortBuildNumber>{PORT_BUILD}</IOSPortBuildNumber>" in version_source,
              "tracked shared Apple version source")
    c.require("modern-ios\\IOSPortVersion.props" in tvos_project and
              "<ApplicationDisplayVersion>$(IOSPortSemanticVersion)</ApplicationDisplayVersion>" in tvos_project and
              "<ApplicationVersion>$(IOSPortBuildNumber)</ApplicationVersion>" in tvos_project,
              "tvOS package consumes shared Apple version source")
    c.require("CFBundleShortVersionString" not in tvos_plist and "CFBundleVersion" not in tvos_plist,
              "tvOS plist cannot override shared version identity")
    c.require("tvOS PORT v{version}  •  BUILD {build}" in identity_patch and
              "apply-apple-port-build-identity.py" in canary_builder and
              "--managed-root \"$destination/managed\" --platform tvos" in canary_builder,
              "tvOS Options label freezes shared identity at build time")

    physical = audit["physical"]
    c.require(physical["iphone"].startswith("PASS_") and physical["ipadOs15"].startswith("PASS_"),
              "required iPhone/iPad physical acceptance")
    c.require(physical["appleTv"] == TVOS_PENDING, "exact honest Apple TV pending marker")
    forbidden_tv_pass = ("APPLE_TV_PHYSICAL_PASS", "PASS_APPLE_TV", "Apple TV physical: PASS",
                         "Apple TV: PASS", "ALL_PLATFORMS_PHYSICAL_GREEN")
    c.require(not any(token in required["report"].read_text() for token in forbidden_tv_pass),
              "verifier rejects Apple TV physical PASS while unavailable")
    c.require(physical["lobbyLoaded"] and physical["lobbyLaunchMapA"] and physical["returnFromMapA"] and
              physical["lobbyLaunchMapB"] and physical["returnFromMapB"], "physical lobby routing")
    c.require(physical["mapASaveAndQuitColdResume"] and physical["mapBSaveAndQuitColdResume"] and
              physical["mapStateIsolated"], "physical map durability")
    c.require(physical["littleEpicStateSurvived"] and physical["fearStateSurvived"] and
              physical["torremolinosMap1StateSurvived"] and physical["torremolinosMap2StateSurvived"],
              "previous four-map state survived")
    c.require(physical["touchControllerAudioRotationBackground"], "focused platform regressions")

    collab_generator = required["collab generator"].read_text()
    lowering = required["semantic lowering"].read_text()
    factories = required["semantic factories"].read_text()
    runtime = required["collab runtime"].read_text()
    progression_runtime = required["progression runtime"].read_text()
    tests = required["tests"].read_text()
    progression_tests = (root / "tools/AppleEverestBuilder/tests/LevelSetProgressionTests.cs").read_text()
    c.require("CollabUtils2CollabID.txt" in collab_generator and "must expose exactly one bounded CollabUtils2 lobby" in collab_generator and
              "must expose exactly one bounded CollabUtils2 journal" in collab_generator,
              "build-time collab identity/lobby contract")
    c.require("SetReturnToHere" in collab_generator and "GeneratedAppleEverestCollabManifest" in collab_generator,
              "frozen panel/return manifest")
    c.require("StaticSemanticLowering" in lowering and "hash-locked-static-semantic-lowering" in
              (root / "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs").read_text() and
              "No helper DLL is" in lowering, "hash-locked semantic lowering boundary")
    c.require("AppleEverestSemanticFactories" in factories and "unregistered static semantic entity" in factories and
              "unregistered static semantic trigger" in factories, "fail-closed static semantic factories")
    c.require("collabutils2_returntolobby" in runtime and "UserIO.Saving" in runtime and
              "CollabUtils2_MapCompleted_" in runtime and "CompleteMapAndReturn" in runtime,
              "runtime lobby/completion/save boundary")
    c.require("LaunchPersistentAt" in progression_runtime and "Play Real Collab Lobby:" in
              (root / "apple-everest/runtime/AppleEverestStaticRuntime.cs").read_text(),
              "persistent lobby launch path")
    c.require("GetTypes(" not in runtime + factories and "Activator.CreateInstance" not in runtime + factories and
              "Assembly.Load" not in runtime + factories, "runtime has no reflection/dynamic assembly path")
    required_test_tokens = ("exact journal semantics frozen", "malformed collab ID fails closed",
                            "chapter-panel X order deterministically orders maps",
                            "one lobby owns exactly two subordinate maps",
                            "static semantic lowering excludes executable/audio/metadata payloads",
                            "device collab runtime performs no archive/assembly/content discovery")
    c.require(all(token in tests for token in required_test_tokens) and
              "four prior maps plus real collab lobby and both subordinate maps share one bounded snapshot" in progression_tests and
              "real collab cumulative tvOS snapshot remains within one replica" in progression_tests,
              "deterministic collab/fail-closed/progression tests")

    locks = audit["locks"]
    c.require(locks == {"canonicalContent": CANONICAL_CONTENT, "canonicalRaw": CANONICAL_RAW,
              "canonicalPatched": CANONICAL_PATCHED, "stage6AudioTree": STAGE6,
              "vanillaIosGeneratedSource": VANILLA_IOS, "iosNative": IOS_NATIVE,
              "tvosNative": TVOS_NATIVE, "chronoBank": CHRONO_BANK, "djIlPlan": IL_PLAN},
              "all canonical/native/audio/IL locks")
    clean = audit["cleanCloneReproduction"]
    c.require(clean == {"recursive": True, "ignoredFixturesCopied": False,
              "publicReleasesReacquired": 13, "exactClosureReproduced": True,
              "builderDeterministicTests": 443, "stageVerifierChecks": 108,
              "privacyPassed": True, "cleanStatus": True},
              "fresh recursive clean-clone reproduction recorded")
    c.require(audit["githubActionsMinutesUsed"] == 0 and audit["developmentIntegrationReady"] and
              not audit["allPlatformReleaseReady"], "travel integration/release boundary")
    c.require(len(audit["futureAppleTvCatchUp"]) >= 20, "cumulative Apple TV catch-up retained")

    report = required["report"].read_text()
    c.require("PASS — GREEN_IOS_IPADOS / TVOS_PHYSICAL_PENDING" in report and TVOS_PENDING in report,
              "report exact travel status")
    c.require("Development integration-ready: **YES**" in report and
              "All-platform release-ready: **NO**" in report, "report release boundary")

    c.require(git(root, "rev-parse", "tvos-port") == START and git(root, "rev-parse", "origin/tvos-port") == START,
              "integration branch untouched")
    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC and
              git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1 and
              git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2 and
              git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3,
              "protected refs untouched")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 remains untagged")
    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".bank", ".zip", ".dll", ".ipa", ".snapshot", ".bin"))
                      for path in tracked), "no proprietary/product bytes tracked")

    closure = (args.closure or root / ".build/apple-everest/production-canary/shared-closure").resolve()
    if closure.is_dir():
        manifest = json.loads((closure / "compatibility-manifest.json").read_text())
        c.require(manifest["sharedClosureSha256"] == SHARED and manifest["managedLogicalSha256"] == MANAGED and
                  manifest["contentLogicalSha256"] == CONTENT_TREE, "supplied closure hashes")
        c.require(manifest["collabSchema"] == 1 and manifest["collabCount"] == 1 and
                  manifest["collabMapCount"] == 2 and manifest["collabManifestSha256"] == COLLAB,
                  "supplied collab closure")
        c.require(manifest["levelSetProgressionMapCount"] == 9 and manifest["levelSetCount"] == 6 and
                  manifest["levelSetProgressionManifestSha256"] == PROGRESSION and
                  manifest["levelSetManifestSha256"] == LEVELSET, "supplied progression closure")
        collab_text = (closure / "collab-manifest.txt").read_text()
        c.require(all(token in collab_text for token in (LOBBY_SOURCE, LOBBY_STAGED, LOBBY_COMPAT,
                  MAP_A_SOURCE, MAP_A_STAGED, MAP_A_COMPAT, MAP_B_SOURCE, MAP_B_STAGED, MAP_B_COMPAT)),
                  "supplied exact collab/map identities")
        c.require("saving\tSetReturnToHere" in collab_text and collab_text.count("\nmap\t") == 2,
                  "supplied two bounded map routes")

    if args.ios_ipa:
        verify_ipa(c, args.ios_ipa.resolve(), "ios", audit)
    if args.tvos_ipa:
        verify_ipa(c, args.tvos_ipa.resolve(), "tvos", audit)

    print(f"PASS: Stage 25K-A first real collab verification ({c.count} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
