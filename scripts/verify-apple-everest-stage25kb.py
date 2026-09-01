#!/usr/bin/env python3
"""Verify Stage 25K-B's second real static-AOT CollabUtils2 collab."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess
import zipfile


START = "311590334cf7180c2f9df0e8000578c14fa68fad"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
TVOS_PASS = "PASS"
PORT_VERSION = "0.1.1"
PORT_BUILD = "35"

COLLAB_ZIP = "a6c8a1d001926a9167c08dde3d221d72d5deea1c577fd329fb386ae74cd9d7fc"
COLLAB_SOURCE = "20ae5ad7342562c19f725feb20a2587db8253a73a1de0adadfbbbffebd528fc0"
LOBBY_SOURCE = "98a4257d09d52c7861227899e33a7c57295fc8731b60622a9bbb0853bf2e2f7c"
LOBBY_STAGED = "9da4c14b0ef2076002706ea3b940353303053ba218748ced2d82782e9492dfa8"
LOBBY_COMPAT = "6eb259f725e0019e598de9834f06c32500ed44b5b6336e8491dd3d52b5a4a000"

MAPS = {
    "KayonaraCollection/1-Collection/1-Kayonara": (
        "0a48b7d87b24cbec7e841148c9be024cd80bca164017218663b75b0e3b841675",
        "91148d2cb4d6ee609380112a8ee21e82e9b3aba2b031a395e4c4b5a875d61e92",
        "de750691136a599199cc7a3c719dc035ec39feedd25a126f7e059b08b65f2a18", 42),
    "KayonaraCollection/1-Collection/2-KayonaraBSide": (
        "afd672e97dc70af765f461a2d79c4965452ff10742c3e7b52f6bd7058fdc854f",
        "70a38f3b43f3cb279a90b1d370b8ccb3d87b32f6fe7d9283532ed918827d52aa",
        "521ed182e849a0d2dfbdb78ca2486d1c5f94c71f0e116ab0272573963c64e678", 16),
    "KayonaraCollection/1-Collection/3-KayonaraCSide": (
        "50289523809847139b04163db9b824424543ecf09379c7f198f8e18879d1c4d8",
        "93bf30f310cc2f2f4adad3ba7604f440e67ad6806c43a40e3dea7429764ae1d2",
        "272796a8086125eb4daa451bd0901f32822d573ff21bae74183d157dbd2733d5", 4),
    "KayonaraCollection/1-Collection/ZZ-HeartSide": (
        "b474a33f9e0a20dd669d33f6d9b12773ae1cc4bf4772770160753707b7ad0b40",
        "b8ec39efd17b6f263977383b55d69b9ff1da8a60d375d031bfa3bcb87fdbd822",
        "b4450cf9da6f1a9d7543e7cd3bc408ca1c86fbfc4f2a9fe4631301acf2d86317", 9),
}

SHARED = "224cb6c223f3d4521c1c9f2499ed42a6c1671a88fa2886eef003f58698a4a50b"
MANAGED = "21ed46579db76aa3ca2d078a8b8f1eb640db37c59f7c4aa914f4709fe46086eb"
CONTENT = "1617ce24472c7755e7368ca8026210b4e86930ed6a0a6a86033d825410051344"
REGISTRY = "d7ff46c8ff66d0e5928723494ac65f4b08bfb23c110b621caeb30c2261b65ad1"
HOOKS = "0c4039cd649a816ec44668e4f8d7498ac69deba17fd344a7275e6278ffa39f99"
API = "d594dea86e944b468819818773f2177312445bbebe022bcbc7ea1ff779c8652c"
COLLAB = "affef9e8276830ee75779341cf5fd2ab9f3a1dbc4ab4818491438855282e59b2"
LEVELSET = "36edf253fd01ad964a6c779335a3f60b1f2875f028cf510fe96c8b1a6627167b"
PROGRESSION = "4995de5514c6b6928399bb51b0dffbd3082719768f7223be6a390a1f871a540c"
AUDIO = "0a7ca20a24239f4829ab5af7002004309d38d89ac5bffbfefff6a49b278c8841"
BANK_SET = "c927c0779c1dbbb5b43bd1a5daa5c2eb5e1c9d00b7e81eab87e70aa0a46eb8d7"
IL_PLAN = "5791ac3b27f500d8ae9f2cc72669b09aa7e490d17b33b992a2b55d0a8728b457"
MODINTEROP = "84f0267040cda9cc51d4dc08da367a4b934aa0e9f43cf3d553815e594f25899d"


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, value: bool, message: str) -> None:
        if not value:
            raise SystemExit("FAIL: " + message)
        self.count += 1


def sha(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def git(root: pathlib.Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(root), *args], check=True,
                          capture_output=True, text=True).stdout.strip()


def verify_ipa(c: Checks, ipa: pathlib.Path, platform: str, audit: dict) -> None:
    expected = audit["products"][platform]
    c.require(ipa.is_file(), f"{platform} IPA exists")
    c.require(ipa.stat().st_size == expected["ipaBytes"] and sha(ipa) == expected["ipaSha256"],
              f"{platform} exact accepted IPA")
    manifest = json.loads((ipa.parent / "build-manifest.json").read_text())
    c.require(manifest["sharedClosureSha256"] == SHARED and manifest["fullAOT"] and
              manifest["fullTrim"] and not manifest["useInterpreter"] and not manifest["jit"],
              f"{platform} build manifest full-AOT boundary")
    with zipfile.ZipFile(ipa) as archive:
        names = archive.namelist()
        apps = sorted({name.split("/")[1] for name in names
                       if name.startswith("Payload/") and name.count("/") >= 2})
        c.require(len(apps) == 1 and apps[0].endswith(".app"), f"{platform} one app")
        prefix = f"Payload/{apps[0]}/"
        info = plistlib.loads(archive.read(prefix + "Info.plist"))
        c.require(info.get("CFBundleShortVersionString") == PORT_VERSION and
                  str(info.get("CFBundleVersion")) == PORT_BUILD, f"{platform} version/build")
        c.require(info.get("MinimumOSVersion") == ("15.0" if platform == "ios" else "16.0"),
                  f"{platform} minimum OS")
        c.require(info.get("CFBundleSupportedPlatforms") ==
                  (["iPhoneOS"] if platform == "ios" else ["AppleTVOS"]), f"{platform} platform")
        c.require(info.get("UIDeviceFamily") == ([1, 2] if platform == "ios" else [3]),
                  f"{platform} device family")
        expected_maps = [
            "Maps/HennyburgrCompEntries/0-Lobbies/lobby.bin",
            "Maps/HennyburgrCompEntries/1-Lobby/redboostercomp.bin",
            "Maps/HennyburgrCompEntries/1-Lobby/stationmovers.bin",
            "Maps/KayonaraCollection/0-Lobbies/1-Collection.bin",
            *["Maps/" + sid + ".bin" for sid in MAPS],
        ]
        c.require(all(sum(name.endswith("/" + item) for name in names) == 1 for item in expected_maps),
                  f"{platform} contains both exact collabs")
        c.require(not any(name.lower().endswith(("collabutils2.dll", "eeveehelper.dll",
                                                "xaphanhelper.dll", "maxhelpinghand.dll",
                                                "lunatichelper.dll")) for name in names),
                  f"{platform} excludes statically lowered helper DLLs")
        c.require(not any(any(token in name for token in ("Mono.Cecil", "MonoMod.Cil",
                  "MonoMod.RuntimeDetour", "MonoMod.Utils", "AppleEverestIlWorker")) for name in names),
                  f"{platform} excludes host/runtime transformation machinery")
        celeste_dlls = [name for name in names if name.endswith("/Celeste.dll")]
        visible = f'{"iOS" if platform == "ios" else "tvOS"} PORT v{PORT_VERSION}  •  BUILD {PORT_BUILD}'
        c.require(len(celeste_dlls) == 1 and visible.encode("utf-16le") in archive.read(celeste_dlls[0]),
                  f"{platform} visible build identity")
        if platform == "ios":
            c.require(prefix + "embedded.mobileprovision" in names and
                      prefix + "_CodeSignature/CodeResources" in names,
                      "iOS development-signed universal package")
            c.require(info.get("UIRequiresFullScreen") is True and
                      info.get("UISupportedInterfaceOrientations~ipad") == [
                          "UIInterfaceOrientationLandscapeLeft", "UIInterfaceOrientationLandscapeRight"],
                      "iOS native iPad landscape presentation")
        else:
            c.require(prefix + "embedded.mobileprovision" in names and
                      prefix + "_CodeSignature/CodeResources" in names,
                      "tvOS development-signed package")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--closure", type=pathlib.Path)
    parser.add_argument("--determinism-root", type=pathlib.Path)
    parser.add_argument("--ios-ipa", type=pathlib.Path)
    parser.add_argument("--tvos-ipa", type=pathlib.Path)
    parser.add_argument("--skip-products", action="store_true")
    parser.add_argument("--clean-clone", action="store_true",
                        help="verify a freshly generated closure without local signed products")
    parser.add_argument("--allow-pending-devices", action="store_true")
    parser.add_argument("--require-clean", action="store_true")
    args = parser.parse_args()
    if args.clean_clone:
        args.skip_products = True
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    closure = (args.closure or root / ".build/apple-everest/production-canary/shared-closure").resolve()
    determinism_root = (args.determinism_root or
                        root / ".build/apple-everest/stage25kb/determinism-build35").resolve()
    c = Checks()

    required = {
        "candidate audit": root / "apple-everest/second-real-collab-candidates-stage25kb.json",
        "acceptance audit": root / "apple-everest/second-real-collab-stage25kb.json",
        "collab generator": root / "tools/AppleEverestBuilder/CollabManifestGenerator.cs",
        "content compiler": root / "tools/AppleEverestBuilder/ContentCompiler.cs",
        "semantic lowering": root / "tools/AppleEverestBuilder/StaticSemanticLowering.cs",
        "semantic factories": root / "apple-everest/runtime/AppleEverestSemanticFactories.cs",
        "second collab runtime": root / "apple-everest/runtime/AppleEverestSecondCollabRuntime.cs",
        "collab runtime": root / "apple-everest/runtime/AppleEverestCollabRuntime.cs",
        "progression runtime": root / "apple-everest/runtime/AppleEverestProgressionRuntime.cs",
        "static runtime": root / "apple-everest/runtime/AppleEverestStaticRuntime.cs",
        "tests": root / "tools/AppleEverestBuilder/tests/CollabStaticTests.cs",
        "progression tests": root / "tools/AppleEverestBuilder/tests/LevelSetProgressionTests.cs",
        "reference launcher": root / "Launch macOS Everest Reference.command",
        "reference preparation": root / "scripts/prepare-apple-everest-macos-reference.sh",
        "reference runner": root / "scripts/run-apple-everest-macos-reference.sh",
        "public graph fetcher": root / "scripts/fetch-apple-everest-stage25kb-fixtures.sh",
        "report": root / "docs/history/stages/APPLE_EVEREST_SECOND_REAL_COLLAB_STAGE25KB_REPORT.md",
    }
    for label, path in required.items():
        c.require(path.is_file(), f"required {label}")

    candidate = json.loads(required["candidate audit"].read_text())
    audit = json.loads(required["acceptance audit"].read_text())
    c.require(candidate["schemaVersion"] == 1 and candidate["stage"] == "25K-B", "candidate schema")
    screening = candidate["screening"]
    c.require(screening["directCollabUtils2Dependents"] == 1317 and
              screening["broadCandidatesUnder150MiB"] == 279 and
              screening["strongCandidates"] == 151 and len(screening["reviewedCandidates"]) >= 50,
              "candidate screening breadth")
    c.require(candidate["deepAudit"]["count"] == 20 and
              len(candidate["deepAudit"]["candidates"]) == 20, "deep audit breadth")
    selected = candidate["selected"]
    c.require(selected["zipSha256"] == COLLAB_ZIP and
              selected["sourceLogicalSha256"] == COLLAB_SOURCE and
              selected["ordinaryUnmodifiedPublicRelease"], "selected ordinary release identity")
    c.require(selected["collabId"] == "KayonaraCollection" and
              selected["lobbySid"] == "KayonaraCollection/0-Lobbies/1-Collection" and
              selected["subordinateMapSids"] == list(MAPS), "selected one-lobby/four-map identity")
    c.require(selected["managedPayloads"] == selected["customFmodBanks"] ==
              selected["luaFiles"] == selected["nativePayloads"] == 0, "selected content-only boundary")
    fetcher = required["public graph fetcher"].read_text()
    c.require(all(token in fetcher for token in (
        "de8eb463083e7298827d6c1ba4f5a4b69cc6cdd9b4be31fec78e272fce3a3839",
        "https://gamebanana.com/mmdl/1657000", COLLAB_ZIP,
        "https://gamebanana.com/mmdl/1086296",
        "ca868d06eb05f0c5de55126090e5019210e9d83c3dae9394cbcd155acf8eb16f",
        "https://gamebanana.com/mmdl/1716933")), "exact public K-B graph fetcher")

    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25K-B" and
              audit["classification"] == "SECOND_REAL_COLLAB_STATIC_AOT", "acceptance schema")
    if args.allow_pending_devices:
        c.require(audit["status"] in {"PENDING_EXACT_FINAL_DEVICE_SMOKE",
                                     "PASS_GREEN_IOS_IPADOS_TVOS"},
                  "honest pending-or-final stage status")
    else:
        c.require(audit["status"] == "PASS_GREEN_IOS_IPADOS_TVOS",
                  "final travel status")
        physical = audit["physical"]
        c.require(physical["iphone"]["exactFinalBuildInstalled"] and
                  physical["iphone"]["fullExactFinalMatrix"] and
                  physical["iphone"]["result"] == "PASS", "exact final iPhone physical PASS")
        c.require(physical["ipad"]["exactFinalBuildInstalled"] and
                  physical["ipad"]["fullExactFinalMatrix"] and
                  physical["ipad"]["result"] == "PASS", "exact final iPad physical PASS")
    apple_tv = audit["physical"]["appleTV"]
    if args.allow_pending_devices:
        c.require(apple_tv["exactFinalBuildInstalled"] and
                  apple_tv["result"] in {TVOS_PASS, "PASS_EXACT_FINAL_LAUNCH"},
                  "exact final Apple TV launch or physical PASS")
    else:
        c.require(apple_tv["exactFinalBuildInstalled"] and apple_tv["fullExactFinalMatrix"] and
                  apple_tv["result"] == TVOS_PASS, "exact final Apple TV physical PASS")

    baseline = audit["baseline"]
    c.require(baseline["startingCommit"] == START and
              baseline["featureBranch"] == "feature/apple-everest-second-real-collab", "baseline")
    c.require(baseline["iosRecoveryCommit"] == IOS_RC and baseline["rc1"] == RC1 and
              baseline["rc2"] == RC2 and baseline["rc3ReleaseBranch"] == RC3 and
              baseline["rc3TagAbsent"], "protected refs recorded")

    collab = audit["collab"]
    c.require(collab["name"] == "Kayonara Collection" and collab["version"] == "1.0.1" and
              collab["zipSha256"] == COLLAB_ZIP and collab["sourceLogicalSha256"] == COLLAB_SOURCE and
              collab["ordinaryUnmodifiedPublicRelease"], "accepted collab exact identity")
    lobby = collab["lobby"]
    c.require(lobby["sid"] == "KayonaraCollection/0-Lobbies/1-Collection" and
              lobby["sourceMapSha256"] == LOBBY_SOURCE and lobby["stagedMapSha256"] == LOBBY_STAGED and
              lobby["compatibilityId"] == LOBBY_COMPAT and lobby["rooms"] == ["1"], "exact lobby")
    maps = {value["sid"]: value for value in collab["maps"]}
    c.require(set(maps) == set(MAPS) and len(maps) == 4, "four exact subordinate maps")
    for sid, (source, staged, compatibility, rooms) in MAPS.items():
        value = maps[sid]
        c.require(value["sourceMapSha256"] == source and value["stagedMapSha256"] == staged and
                  value["compatibilityId"] == compatibility and value["roomCount"] == rooms,
                  f"exact map identity {sid}")
        c.require(value["levelSet"] == "KayonaraCollection/1-Collection" and
                  value["returnToLobbyMode"] == "SetReturnToHere", f"exact map lobby relation {sid}")
    c.require(maps["KayonaraCollection/1-Collection/1-Kayonara"]["effectiveIntro"] == "WakeUp",
              "embedded map metadata overrides sidecar intro")

    door = audit["miniHeartDoor"]
    c.require(door["present"] and door["requiredByGraph"] and door["heartThreshold"] == 3 and
              door["levelSet"] == "KayonaraCollection/1-Collection" and
              len(door["contributingMaps"]) == 3 and door["deterministicThresholdModelPassed"],
              "real three-heart MiniHeartDoor")
    c.require(door["physicalLockedProof"], "physical locked-door proof")
    berries = audit["specialBerries"]
    classes = {value["type"]: value for value in berries["presentClasses"]}
    c.require(classes["CollabUtils2/SilverBerry"]["count"] == 3 and
              classes["CollabUtils2/SilverBerry"]["classification"] == "REQUIRED_BY_GRAPH" and
              classes["CollabUtils2/RainbowBerry"]["count"] == 1 and
              classes["CollabUtils2/RainbowBerry"]["classification"] == "REQUIRED_BY_GRAPH",
              "required silver/rainbow completion classes")
    c.require(not berries["newDurableSchemaRequired"], "special durability derives from existing state")

    census = audit["compatibilityCensus"]
    c.require(census["zeroUnclassifiedBlockers"] and census["zeroUnsupportedRequiredBlockers"] and
              not census["newMajorMechanism"], "zero blockers/no major mechanism")
    c.require(census["hookGenTargetCountBefore"] == census["hookGenTargetCountAfter"] == 102 and
              census["hookGenTargetsAdded"] == 0 and census["reviewedApiMembersBefore"] ==
              census["reviewedApiMembersAfter"] == 30 and census["reviewedApiMembersAdded"] == 0,
              "unchanged HookGen and API surfaces")
    c.require(census["semanticFactoriesBefore"] == 16 and census["semanticFactoriesAdded"] == 11 and
              census["semanticFactoriesAfter"] == 27 and census["registryFactoriesAfter"] == 59 and
              census["coreGameplayFactoriesAfter"] == 6, "bounded factory expansion")
    c.require(census["frozenIlPlanSha256"] == IL_PLAN and census["frozenIlTransformCount"] == 5 and
              census["newFrozenIl"] == [] and census["directIlHookCount"] == 0 and
              not census["configuredHooks"], "unchanged frozen/direct/configured IL")
    c.require(not census["generalDynamicData"] and census["modInteropRegistrationCount"] == 0 and
              census["customFmodBanksAdded"] == 0 and not census["lua"] and
              not census["nativeOrPInvoke"], "no dynamic/native/audio expansion")
    c.require(census["staticFactoriesOnly"] and not census["runtimeDllLoading"] and
              census["runtimeDetour"] == "absent" and not census["deviceModScanning"] and
              not census["reflectionFactoryDiscovery"], "static-only device boundary")

    progression = audit["progression"]
    c.require(progression["schema"] == "AEVPSV1" and progression["schemaVersion"] == 1 and
              not progression["migrationRequired"] and not progression["newCollabSpecificDurableSchema"],
              "AEVPSV1 remains schema 1")
    c.require(progression["cumulativeAreaCount"] == 12 and progression["rawBytes"] == 4987 and
              progression["tvosCompressedBytes"] == 1278 and progression["tvosReplicaCapPercent"] == 1.01 and
              progression["maximumThreeSlotAbBytes"] == 761856, "bounded cumulative progression size")
    c.require(all(progression[key] for key in ("slotIsolation", "deleteRecreate",
              "importReplacementIsolation", "corruptionFallback", "absentMapQuarantine",
              "crossCollabIsolation")), "progression isolation and recovery")

    manifest = json.loads((closure / "compatibility-manifest.json").read_text())
    expected = audit["determinism"]
    keys = {
        "sharedClosureSha256": SHARED, "managedLogicalSha256": MANAGED,
        "contentLogicalSha256": CONTENT, "registrySha256": REGISTRY,
        "hookTransformSha256": HOOKS, "appleApiSurfaceSha256": API,
        "collabManifestSha256": COLLAB, "levelSetManifestSha256": LEVELSET,
        "levelSetProgressionManifestSha256": PROGRESSION,
        "customAudioManifestSha256": AUDIO, "customBankLogicalSetSha256": BANK_SET,
        "frozenIlPlanSha256": IL_PLAN, "modInteropPlanSha256": MODINTEROP,
    }
    audit_keys = {
        "levelSetProgressionManifestSha256": "progressionManifestSha256",
    }
    c.require(all(manifest[key] == value and
                  expected[audit_keys.get(key, key)] == value
                  for key, value in keys.items()),
              "exact accepted closure identities")
    c.require(manifest["managedFileCount"] == 47 and manifest["contentFileCount"] == 7535 and
              manifest["collabCount"] == 2 and manifest["collabMapCount"] == 6 and
              manifest["levelSetProgressionMapCount"] == 14 and manifest["levelSetCount"] == 8,
              "exact closure counts")
    c.require(manifest["managedDetourTargetCount"] == 102 and manifest["appleApiSurfaceMemberCount"] == 30 and
              manifest["directManagedHookCount"] == 0 and manifest["frozenIlTransformCount"] == 5,
              "exact static hook/API counts")
    c.require(manifest["customEntityFactoryCount"] == 59 and manifest["customBackdropFactoryCount"] == 0 and
              len(manifest["coreGameplayFactories"]) == 6, "exact factory counts")
    c.require(manifest["modInteropRegistrationCount"] == manifest["modInteropImportCount"] ==
              manifest["modInteropExportCount"] == 0, "empty device ModInterop plan")
    c.require(not manifest["runtimeDllLoading"] and manifest["runtimeDetour"] == "absent" and
              manifest["interpreter"] is False, "no runtime loader/detour/interpreter")
    c.require(sha(closure / "collab-manifest.txt") == COLLAB and
              sha(closure / "levelset-manifest.txt") == LEVELSET and
              sha(closure / "levelset-progression-manifest.txt") == PROGRESSION and
              sha(closure / "custom-audio-manifest.txt") == AUDIO, "exact generated manifests")
    collab_text = (closure / "collab-manifest.txt").read_text()
    c.require("mini-heart-door\tKayonaraCollection\t12\t1\t464\t88\t40\t56\t3" in collab_text and
              "CollabUtils2/RainbowBerry\tREQUIRED_BY_GRAPH\tsilver-completion-derived" in collab_text and
              collab_text.count("CollabUtils2/SilverBerry\tREQUIRED_BY_GRAPH") == 3,
              "generated door/silver/rainbow records")

    run1 = determinism_root / ("run1-compatibility-manifest.json" if args.clean_clone
                               else "run1-ios-build-manifest.json")
    c.require(run1.is_file() and json.loads(run1.read_text())["sharedClosureSha256"] == SHARED,
              "determinism run 1 exact closure" if args.clean_clone
              else "determinism run 1 product consumed exact closure")
    determinism = [tuple(manifest[key] for key in keys)]
    for index in range(2, 4):
        path = determinism_root / f"run{index}-compatibility-manifest.json"
        c.require(path.is_file(), f"determinism run {index}")
        value = json.loads(path.read_text())
        determinism.append(tuple(value[key] for key in keys))
    c.require(len(set(determinism)) == 1 and determinism[0] == tuple(keys.values()),
              "three independent complete closures identical")

    semantic = required["semantic lowering"].read_text()
    runtime = required["second collab runtime"].read_text()
    collab_runtime = required["collab runtime"].read_text()
    static_runtime = required["static runtime"].read_text()
    compiler = required["content compiler"].read_text()
    c.require("CollabUtils2/MiniHeartDoor" in semantic and "CollabUtils2/RainbowBerry" in semantic and
              "CollabUtils2/SilverBerry" in semantic and "EeveeHelper/FlagToggleModifier" in semantic and
              "MaxHelpingHand/SecretBerry" in semantic, "exact semantic registry")
    c.require("class AppleEverestMiniHeartDoor : HeartGemDoor" in runtime and
              "class AppleEverestRainbowBerry : Strawberry" in runtime and
              "class AppleEverestSpeedBerry : Strawberry" in runtime, "typed completion runtime")
    c.require("[Tracked(false)]" in runtime and
              "class AppleEverestSpeedBerryCollectTrigger : Trigger" in runtime,
              "speed-berry trigger avoids duplicate inherited tracker registration")
    c.require('TextMenu.Button("EVEREST / PORT OPTIONS")' in static_runtime and
              'TextMenu.Header("EVEREST / PORT OPTIONS")' in static_runtime,
              "dedicated Everest and port options submenu")
    c.require("Assembly.Load" not in runtime and "System.Reflection" not in runtime and
              "Everest.Content.Mods" not in collab_runtime, "no device discovery in collab runtime")
    c.require("presentation.ApplySidecar" in compiler and
              compiler.index("presentation.ApplySidecar") < compiler.index("ReadProgressionElement"),
              "desktop-compatible sidecar-then-embedded metadata precedence")

    if not args.skip_products:
        ios = (args.ios_ipa or root / "artifacts/apple-everest/canary/ios/Celeste-Everest-Canary-iOS.ipa").resolve()
        tvos = (args.tvos_ipa or root / "artifacts/apple-everest/canary/tvos/Celeste-Everest-Canary-tvOS.ipa").resolve()
        verify_ipa(c, ios, "ios", audit)
        verify_ipa(c, tvos, "tvos", audit)

    c.require(git(root, "merge-base", START, "HEAD") == START, "feature branch descends from exact start")
    c.require(git(root, "rev-parse", "origin/tvos-port") == START and
              git(root, "rev-parse", "tvos-port") == START, "protected tvos-port unchanged")
    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC and
              git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1 and
              git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2 and
              git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3,
              "immutable release references unchanged")
    c.require(subprocess.run(["git", "-C", str(root), "show-ref", "--verify", "--quiet",
                              "refs/tags/v1.0.0-rc.3"]).returncode != 0, "v1.0.0-rc.3 tag remains absent")
    c.require(audit["regressions"]["deterministicBuilderTests"] == 476 and
              all(audit["regressions"][key] for key in ("kACollabRetained", "littleEpicRetained",
                  "fearRetained", "torremolinosMap1Retained", "torremolinosMap2Retained",
                  "chronoCustomAudioRetained", "djFrozenIlRetained")), "cumulative regressions recorded")
    c.require(audit["closeout"]["githubActionsRuns"] == 0 and
              not audit["closeout"]["allPlatformReleaseReady"], "no Actions/all-platform claim")
    if args.require_clean:
        c.require(git(root, "status", "--short") == "", "clean Git status")

    print(f"PASS: Stage 25K-B second real collab verification ({c.count} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
