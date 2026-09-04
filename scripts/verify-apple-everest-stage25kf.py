#!/usr/bin/env python3
"""Fail-closed verification for Stage 25K-F audio and helper lowering."""

from __future__ import annotations

import argparse
import collections
import hashlib
import json
import pathlib
import re
import subprocess
import sys
import tempfile


START = "a5455ced962dd55cf4e9f708f55d265df77182c9"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
LOBBY = "StrawberryJam2021/0-Lobbies/1-Beginner"
EVENTS = {
    "event:/sj21_bingovergoogle": ("StrawberryJam2021AudioA", "bank:/sj21_bingovergoogle",
        "772e3d4a41b463edcf529882f6d2f4de12384e891ae62a23220aa1379d89e159",
        "88c1e1c6d54ffcfc9fbb1c4527cb9108e531ea3bc303dbfd73b8c7eeba0cf1ba",
        "f023b527-acd0-40a9-a9b3-87e9f686b81c", "cbfb24b2-faf6-4db8-bc5a-c096f754724e", 9),
    "event:/sj21_levelselect": ("StrawberryJam2021AudioA", "bank:/sj21_shared",
        "7620d1de4c32f1564b1206ac252af806b5b33c6a9f2b4624df582ece0d7f62e8",
        "500b8c536468e0ce4bab7c44c0f8c8fe932a5ef41ab68d2793683f8a21420555",
        "1068df52-9f57-4e6e-887c-c1d5a961d61d", "3de891f8-2a2c-42d8-b243-f522abaa7db5", 10),
    "event:/sj21_BegLobby": ("StrawberryJam2021AudioB", "bank:/sj21_BegLobby",
        "a5c45fe0ed77d048c4c9520bb2e1e58f9dbfd1d05dc2ccc19d5310fac0ac0ceb",
        "3a8ea6f4a1f7f3ec0ae16b19b7cec11c5a1479974e830d812896351fa3cac740",
        "827873b5-86b7-4e1b-9848-e04c83fb7ddf", "8e00fa4b-a47b-4270-8607-200aa2e49996", 11),
    "event:/sj21_jamjar-blue": ("StrawberryJam2021", "bank:/sj21_jamjars",
        "9e6bf27fc7f607e2ef5695b6b32ccf34abf1b7380ba13d66f3b49bc5b1613c1b",
        "034b1e3a27419c8332e4a81af1a773971858daca97609ea9855ddeb5b7bf34f7",
        "a8371196-9461-4ff6-8994-7032718615a7", "f81c1b1a-e90e-4442-90a7-8d05db253a0b", 12),
}
PINS = {
    "CrystallineHelper": ("1.17.2",
        "4573f5e45dce0905142cd2b119f4a9a744bdce8ef3199319d1e46b6d1d342747",
        "Code/bin/vitmod.dll", "456410258fbce4594c3e987d025bf651e1bd3f49d2d676a921d8ea9f27ba052a", 53),
    "VortexHelper": ("1.2.19",
        "b6280fe2e3c05d355c32a51049854137c41be72d24ec4aec9d997cc7c4394db2",
        "Code/bin/VortexHelper.dll", "f5a32f02c2699dcdf41bb1af808ead631491571c3d259429761953693bd37f73", 2),
}


class Checks:
    def __init__(self): self.count = 0
    def require(self, value, message):
        if not value: raise SystemExit("FAIL: " + message)
        self.count += 1


def load(path): return json.loads(path.read_text())
def canonical(value): return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n").encode()
def artifact_hash(value):
    copy = dict(value)
    claimed = copy.pop("artifactSha256")
    return claimed == hashlib.sha256(canonical(copy)).hexdigest()
def git(root, *args):
    return subprocess.run(["git", "-C", str(root), *args], check=True,
                          capture_output=True, text=True).stdout.strip()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--fixture-root", type=pathlib.Path)
    parser.add_argument("--audit-root", type=pathlib.Path)
    parser.add_argument("--reproduction-json", type=pathlib.Path)
    parser.add_argument("--require-clean", action="store_true")
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    ae = root / "apple-everest"
    c = Checks()
    paths = {name: ae / name for name in ["sj-multibank-audio-stage25kf.json",
        "sj-crystalline-vortex-stage25kf.json", "sj-beginner-readiness-stage25kf.json"]}
    required = [*paths.values(), root / "scripts/generate-apple-everest-stage25kf.py",
        root / "scripts/reproduce-apple-everest-stage25kf.py",
        root / "scripts/verify-apple-everest-stage25kf.sh",
        root / "apple-everest/runtime/AppleEverestCustomAudioRuntime.cs",
        root / "apple-everest/runtime/AppleEverestStage25KFAudioCanary.cs",
        root / "apple-everest/runtime/AppleEverestStaticIdentity.cs",
        root / "apple-everest/runtime/semantics/AppleEverestCrystallineSemantics.cs",
        root / "apple-everest/runtime/semantics/AppleEverestVortexSemantics.cs",
        root / "apple-everest/canaries/stage25kf/Content/Maps/AppleEverest/Stage25KF.xml",
        root / "docs/history/stages/APPLE_EVEREST_SJ_AUDIO_CRYSTALLINE_VORTEX_STAGE25KF_REPORT.md"]
    for path in required: c.require(path.is_file(), "required file " + str(path.relative_to(root)))
    audio, helper, ready = (load(paths[name]) for name in paths)
    c.require(all(row["schemaVersion"] == 1 and row["stage"] == "25K-F"
                  for row in (audio, helper, ready)), "artifact schemas")
    c.require(all(artifact_hash(row) for row in (audio, helper, ready)), "artifact hashes")

    c.require(audio["compatibilityClass"] == "STATIC_CUSTOM_FMOD_BANK_SET" and
              audio["requiredEventCount"] == audio["distinctPhysicalBankCount"] ==
              audio["distinctBankIdentityCount"] == 4, "four distinct bounded SJ banks")
    observed = {}
    for bank in audio["banks"]:
        required_event = next(row for row in bank["embeddedGuidRecords"]
                              if row["path"] == bank["requiredEventPath"])
        observed[bank["requiredEventPath"]] = (bank["owner"], bank["bankPath"], bank["bankSha256"],
            bank["guidSha256"], bank["bankId"], required_event["id"], bank["loadOrdinal"])
        c.require(len(bank["guidExportRecords"]) == bank["guidExportRecordCount"] and
                  len(bank["embeddedGuidRecords"]) == bank["embeddedGuidRecordCount"],
                  "complete GUID records for " + bank["bankPath"])
        c.require(not bank["stringsBankRelationship"] == "REQUIRED" and
                  not bank["masterBankRelationship"] == "REQUIRED" and
                  not bank["programmerSound"] and not bank["nativePluginOrDsp"],
                  "supported bank shape for " + bank["bankPath"])
    c.require(observed == EVENTS, "exact bank, GUID export, identity, event, and ordinal mapping")
    expected_order = [(row["ordinal"], row["owner"], row["bank"]) for row in audio["loadOrder"]]
    c.require(expected_order[:7] == [(index, "Celeste", name) for index, name in enumerate([
        "Master Bank.bank", "Master Bank.strings.bank", "music.bank", "sfx.bank", "ui.bank",
        "dlc_music.bank", "dlc_sfx.bank"], 1)], "seven vanilla banks lead order")
    c.require(expected_order[7:] == [(8, "ChronoHelper", "bank:/ExpertContestHelper"),
        (9, "StrawberryJam2021AudioA", "bank:/sj21_bingovergoogle"),
        (10, "StrawberryJam2021AudioA", "bank:/sj21_shared"),
        (11, "StrawberryJam2021AudioB", "bank:/sj21_BegLobby"),
        (12, "StrawberryJam2021", "bank:/sj21_jamjars")], "desktop-derived custom bank order")
    c.require(audio["collisions"]["incompatibleEventPath"] == 0 and
              audio["collisions"]["incompatibleEventGuid"] == 0 and
              audio["collisions"]["incompatibleBankIdentity"] == 0 and
              len(audio["collisions"]["compatibleSharedGuidPath"]) == 1, "bounded collision result")
    runtime = audio["runtime"]
    c.require(runtime["studioSystemCount"] == 1 and runtime["transactionalLoad"] and
              runtime["sameSystemInitializationIdempotent"] and
              not runtime["backgroundReopenDuplicateLoads"] and
              not runtime["softReloadDuplicateLoads"] and runtime["coldLaunchReload"],
              "single-system transactional lifecycle")
    c.require(audio["chronoRegression"]["retained"] and audio["chronoRegression"]["loadOrdinal"] == 8 and
              audio["chronoRegression"]["bankSha256"] ==
              "2607c358e66f4bce4e81fa3748eaffe1205dcfb57010be2e90ce40fabeb8d3ec",
              "Chrono bank identity and order retained")
    full = audio["fullGraphAudit"]
    c.require((full["totalBanks"], full["structurallyCompatibleBankCount"],
               full["bankIdentityManifestReadyCount"], full["bankIdentityAuthorityPendingCount"]) ==
              (149, 149, 119, 30), "full 149-bank structural and identity audit")
    c.require(full["unsupportedShapeCount"] == full["masterBankCount"] == full["stringsBankCount"] ==
              full["programmerSoundCount"] == full["nativePluginOrDspCount"] == 0,
              "no full-audit unsupported bank shapes")
    c.require(full["exactMetadataCollisionGroups"] == {"incompatibleGuid": 22, "incompatiblePath": 0} and
              full["publicExportCollisionGroups"] == {"incompatibleGuid": 36, "incompatiblePath": 5},
              "full-audit collision groups")
    c.require(full["plannerObservedWithinRuntimeCeiling"] and full["maximumGraphBankCount"] == 149 and
              len(full["banks"]) == 149, "bounded full-audit planner")

    c.require(helper["sourceMap"] == LOBBY and not helper["sourceMapPackaged"] and
              helper["occurrenceCount"] == 55 and helper["unknownOccurrenceCount"] == 0,
              "55 exact occurrences and no real map packaging")
    c.require(helper["countsByCustomId"] == {"VortexHelper/AttachedJumpThru": 2,
        "vitellary/bloomstrengthtrigger": 9, "vitellary/editdepthtrigger": 43,
        "vitellary/triggertrigger": 1}, "exact custom-ID counts")
    pins = helper["helperPins"]
    c.require(set(pins) == set(PINS), "two exact helper pins")
    for name, expected in PINS.items():
        pin = pins[name]
        c.require((pin["version"], pin["zipSha256"], pin["dllPath"], pin["dllSha256"],
                   pin["occurrenceCount"]) == expected, "exact " + name + " release")
        c.require(pin["sourceAvailability"] == "DISTRIBUTED_SOURCE_PRESENT" and
                  pin["unclassifiedMethodCount"] == 0 and
                  all(row["disposition"] in {"REACHABLE_REQUIRED", "PRESENT_UNREACHABLE_EXACT_AUTHORED_PROFILE"}
                      for row in pin["reachableMethodCensus"]),
                  "closed " + name + " method census")
    c.require(collections.Counter(row["provider"] for row in helper["occurrences"]) ==
              {"CrystallineHelper": 53, "VortexHelper": 2} and
              all(row["room"] == "sj2021beginnerlobby" and
                  row["classification"] == "STATIC_TYPED_FACTORY_ACCEPTED"
                  for row in helper["occurrences"]), "all occurrences mapped and accepted")
    lowering = helper["lowering"]
    c.require(lowering["factoriesAreStaticTyped"] and not lowering["generalDynamicData"] and
              not lowering["arbitraryReflection"] and not lowering["newRuntimeHookClass"] and
              not lowering["dynamicHookLifetime"], "bounded static helper lowering")
    c.require(set(helper["semantics"]) == {"attachedJumpThru", "bloomStrength",
        "editDepth", "triggerTrigger"}, "four semantic records")
    c.require(helper["canary"]["dataOnly"] and helper["canary"]["sid"] == "AppleEverest/Stage25KF",
              "data-only K-F canary")

    coverage = ready["customIdOccurrenceCoverage"]
    c.require(coverage == {"auditedLobbyAndBing": 920, "acceptedOrVanilla": 920,
        "blocked": 0, "unclassified": 0, "closedByStage25kf": 55}, "zero-blocker occurrence recount")
    c.require(ready["beginnerBlockerGroups"] == {"beforeStage25kf": 2, "afterStage25kf": 0,
        "remaining": []} and ready["readiness"] == "READY_FOR_K_G_INTEGRATION_BUILD",
        "exact readiness gate")
    c.require(not ready["playableSliceBuilt"] and ready["playableSliceBuildIntentionallyDeferred"] and
              not ready["actualLobbyOrBingMapPackaged"] and not ready["fullStrawberryJamSupport"],
              "K-G integration boundary")
    c.require(ready["providerClosureCount"] == 22 and not ready["providersWithRemainingMechanisms"],
              "provider closure complete")
    c.require(ready["hookGen"] == {"catalogBefore": 205, "catalogAfter": 205, "newDescriptors": 0} and
              ready["appleApi"] == {"membersBefore": 30, "membersAfter": 30,
              "newMembers": 0, "broadPublicizer": False}, "no HookGen or Apple API expansion")
    c.require(ready["factories"] == {"staticSemanticBefore": 24, "staticSemanticAfter": 30,
        "productionMechanismFactoriesAdded": 4, "canaryFactoriesAdded": 2}, "static factory recount")
    boundary = ready["runtimeBoundary"]
    c.require(not any(boundary.values()), "closed runtime boundary")
    deterministic = ready["determinism"]
    c.require(deterministic["runsIdentical"] and deterministic["completeTreeCompared"] and
              deterministic["fileCount"] > 1000 and deterministic["customAudioBankCount"] == 5 and
              deterministic["staticSemanticFactoryCount"] == 30, "three full identical closure trees")
    c.require([row["omitted"] for row in deterministic["omissions"]] ==
              ["CrystallineHelper", "VortexHelper"] and
              all(row["managedCompile"] and row["unrelatedSemanticPlansIdentical"] and
                  row["audioManifestIdentical"] and row["acceptedBeginnerManifestHashesRetained"]
                  for row in deterministic["omissions"]), "compiled independent omission controls")

    canary = required[-2].read_text()
    c.require(all(token in canary for token in ["stage25kfAudio", "VortexHelper/AttachedJumpThru",
        "vitellary/bloomstrengthtrigger", "vitellary/editdepthtrigger", "vitellary/triggertrigger",
        "everest/flagTrigger", "rumbleTrigger", "theoCrystal"]), "canary mechanism coverage")
    c.require('rumbleTrigger id="44"' in canary and 'manualTrigger="false" persistent="false"' in canary,
              "repeatable rumble target does not inherit a stale room flag")
    crystalline = (root / "apple-everest/runtime/semantics/AppleEverestCrystallineSemantics.cs").read_text()
    c.require("TriggerEntityIdOffset = 10000000" in crystalline and "sourceEntityId = data.ID" in crystalline and
              "entityId.ID != TriggerEntityIdOffset + sourceEntityId" in crystalline,
              "authored and runtime trigger identities fail closed")
    custom_audio = (root / "tools/AppleEverestBuilder/CustomAudioManifest.cs").read_text()
    c.require(all(token in custom_audio for token in ["ValidateGraph", "CanonicalManifest", "LogicalSet",
        "ParseGuidTable", "STATIC_CUSTOM_FMOD_BANK_SET"]), "static bank-set planner implementation")
    runtime_source = (root / "apple-everest/runtime/AppleEverestCustomAudioRuntime.cs").read_text()
    c.require("system.loadBankFile" in runtime_source and
              "for (int index = Banks.Count - 1; index >= 0; index--)" in runtime_source and
              "Banks[index].Handle.unload()" in runtime_source and
              "do not double-unload" in runtime_source,
              "transactional runtime and teardown ownership")

    report = required[-1].read_text()
    c.require(set(map(str, range(1, 91))).issubset(set(re.findall(r"^\s*(\d+)\.\s", report, re.MULTILINE))),
              "all 90 final questions answered")
    stage_paths = required + [root / "docs/APPLE_EVEREST_STATIC_AOT.md",
                              root / "docs/APPLE_EVEREST_COMPATIBILITY.md"]
    forbidden = re.compile(r"/Users/|/private/|mobileprovision|device identifier|BEGIN (?:RSA |EC )?PRIVATE KEY", re.I)
    c.require(not any(forbidden.search(path.read_text(errors="replace")) for path in stage_paths), "privacy scan")
    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".zip", ".dll", ".bank", ".ipa", ".bin", ".guids.txt"))
                      for path in tracked if "stage25kf" in path.lower()), "no tracked binary inputs or products")
    c.require(git(root, "diff", "--name-only", START, "--", ".github/workflows") == "", "zero Actions changes")
    c.require(subprocess.run(["git", "-C", str(root), "merge-base", "--is-ancestor", START, "HEAD"]).returncode == 0,
              "feature descends from K-E tip")
    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC and
              git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1 and
              git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2 and
              git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3, "protected refs unchanged")
    c.require(subprocess.run(["git", "-C", str(root), "rev-parse", "v1.0.0-rc.3^{}"],
                             capture_output=True).returncode != 0, "rc3 tag remains absent")

    if args.fixture_root or args.audit_root or args.reproduction_json:
        c.require(all((args.fixture_root, args.audit_root, args.reproduction_json)),
                  "fixture, audit, and reproduction inputs supplied together")
        with tempfile.TemporaryDirectory(prefix="stage25kf-verify-") as temporary:
            generated = pathlib.Path(temporary)
            subprocess.run([sys.executable, str(root / "scripts/generate-apple-everest-stage25kf.py"),
                "--fixture-root", str(args.fixture_root.resolve()), "--audit-root", str(args.audit_root.resolve()),
                "--reproduction-json", str(args.reproduction_json.resolve()), "--output-root", str(generated)],
                cwd=root, check=True)
            for name, tracked_path in paths.items():
                c.require((generated / name).read_bytes() == tracked_path.read_bytes(),
                          "deterministic regenerated " + name)
    if args.require_clean: c.require(git(root, "status", "--porcelain") == "", "clean worktree")
    print(f"PASS: Stage 25K-F verifier ({c.count} checks)")
    return 0


if __name__ == "__main__": raise SystemExit(main())
