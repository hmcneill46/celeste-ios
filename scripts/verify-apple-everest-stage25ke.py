#!/usr/bin/env python3
"""Fail-closed verification for Stage 25K-E Strawberry Jam semantic lowering."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import pathlib
import re
import subprocess
import sys
import tempfile


START = "ea5bfd7de0185a676d549d7e4b9f483ee159f3a8"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
STAGE25KC_TVOS = "311590334cf7180c2f9df0e8000578c14fa68fad"
ROOT_DLL = "8d5b9184204e7e7728965bcf95af5f150f6220dafbfe52fdd6c23e50d47e5258"
LOBBY = "StrawberryJam2021/0-Lobbies/1-Beginner"
BING = "StrawberryJam2021/1-Beginner/Bing_Over_Google"
CLASSES = {"REQUIRED_BY_SLICE", "DEPENDENCY_GLOBAL_REQUIRED", "PRESENT_BUT_UNREACHABLE",
           "OPTIONAL", "UNSUPPORTED_NOT_REQUIRED"}
PINS = {
    "ContortHelper": ("1.5.5", "d8b42128a808e68d30329baa9299fdb41bf7d24743635dec3137c36bcc87956a",
                      "c3983e67e1b535fbb1e0f0a541e8c78ad8f4cd150f66636c783a83dc5ffb4488"),
    "ExtendedVariantMode": ("0.50.5", "4019b362d9ad1b2d3a6a670f833ef6324d735d5791a0bf21b62cf5cfbc0d639d",
                            "cb28846f7f7348ddb996f63498c97694616436e97ef34f67880b697ba024fe81"),
    "JungleHelper": ("1.4.10", "a140e21cbb5fd2dcaac70d4d5e36d49476e414861455ae25dedc0678164406cc",
                     "fed840ade7250f05e38b70a81bcfe751ca87ff63274f4b568172868cacf56f8a"),
    "YetAnotherHelper": ("1.2.5", "73d64e1b3457e2461d3368a01de9f5f31a58bf24e130f3e35ec7280b24814c2d",
                         "f48e16a568edf43913beac0684bd26b7dc12d45be4011a8b214e05db86b78aea"),
}
EVM_SITES = {
    "System.Void ExtendedVariants.Module.ExtendedVariantsModule::hookStuffRightNow()",
    "System.Void ExtendedVariants.Module.ExtendedVariantsModule::initializeStuff()",
    "System.Void ExtendedVariants.UI.VanillaVariantOptions::Load()",
    "System.Void ExtendedVariants.Variants.DisableWallJumping::Load()",
    "System.Void ExtendedVariants.Variants.Gravity::Load()",
    "System.Void ExtendedVariants.Variants.InvertHorizontalControls::Load()",
    "System.Void ExtendedVariants.Variants.InvertVerticalControls::Load()",
    "System.Void ExtendedVariants.Variants.NoFreezeFrames::Load()",
    "System.Void ExtendedVariants.Variants.Stamina::Load()",
}


class Checks:
    def __init__(self): self.count = 0
    def require(self, value, message):
        if not value: raise SystemExit("FAIL: " + message)
        self.count += 1


def load(path): return json.loads(path.read_text())
def canonical(value): return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n").encode()
def digest(value): return hashlib.sha256(canonical(value)).hexdigest()
def git(root, *args):
    return subprocess.run(["git", "-C", str(root), *args], check=True, capture_output=True, text=True).stdout.strip()


def artifact_hash(value):
    copy = dict(value)
    claimed = copy.pop("artifactSha256")
    return claimed == digest(copy)


def verify_stage25kc_in_historical_ref_view(root: pathlib.Path) -> None:
    """Run the immutable K-C verifier without moving the real post-K-D refs."""
    with tempfile.TemporaryDirectory(prefix="stage25kc-historical-ref-") as temporary:
        git_dir = pathlib.Path(temporary) / "git"
        subprocess.run(["git", "init", "--bare", str(git_dir)], check=True,
                       capture_output=True, text=True)
        object_dir = pathlib.Path(git(root, "rev-parse", "--git-path", "objects"))
        if not object_dir.is_absolute():
            object_dir = (root / object_dir).resolve()
        (git_dir / "objects/info/alternates").write_text(str(object_dir) + "\n")
        current = git(root, "rev-parse", "HEAD")
        refs = {
            "refs/heads/current": current,
            "refs/heads/tvos-port": STAGE25KC_TVOS,
            "refs/remotes/origin/tvos-port": STAGE25KC_TVOS,
            "refs/tags/ios-v0.1.1-rc.1": IOS_RC,
            "refs/tags/v1.0.0-rc.1": RC1,
            "refs/tags/v1.0.0-rc.2": RC2,
            "refs/remotes/origin/release/v1.0.0-rc.3": RC3,
        }
        for name, value in refs.items():
            subprocess.run(["git", "--git-dir", str(git_dir), "update-ref", name, value],
                           check=True, capture_output=True, text=True)
        subprocess.run(["git", "--git-dir", str(git_dir), "symbolic-ref", "HEAD",
                        "refs/heads/current"], check=True, capture_output=True, text=True)
        environment = dict(os.environ, GIT_DIR=str(git_dir), GIT_WORK_TREE=str(root))
        subprocess.run(["git", "--git-dir", str(git_dir), "--work-tree", str(root),
                        "read-tree", current], check=True, env=environment,
                       capture_output=True, text=True)
        result = subprocess.run([sys.executable,
            str(root / "scripts/verify-apple-everest-stage25kc.py"),
            "--repo-root", str(root)], env=environment, capture_output=True, text=True)
        if result.returncode:
            raise SystemExit((result.stdout + result.stderr).strip())
        print(result.stdout.strip())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--fixture-root", type=pathlib.Path)
    parser.add_argument("--audit-root", type=pathlib.Path)
    parser.add_argument("--reproduction-json", type=pathlib.Path)
    parser.add_argument("--require-clean", action="store_true")
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()
    ae = root / "apple-everest"
    paths = {name: ae / name for name in ["sj-helper-semantics-stage25ke.json",
        "sj-root-semantics-stage25ke.json", "sj-beginner-readiness-stage25ke.json",
        "sj-beginner-lobby-manifest-stage25ke.json", "sj-beginner-bing-manifest-stage25ke.json"]}
    required = [*paths.values(), root / "scripts/generate-apple-everest-stage25ke.py",
        root / "scripts/reproduce-apple-everest-stage25ke.py", root / "scripts/verify-apple-everest-stage25ke.sh",
        root / "tools/AppleEverestBuilder/SemanticReachabilityCensus.cs",
        root / "tools/AppleEverestBuilder/StaticSemanticLowering.cs",
        root / "tools/AppleEverestBuilder/StaticSemanticRuntimePatches.cs",
        root / "apple-everest/canaries/stage25ke/Content/Maps/AppleEverest/Stage25KE.xml",
        root / "docs/history/stages/APPLE_EVEREST_SJ_ROOT_HELPER_SEMANTICS_STAGE25KE_REPORT.md"]
    for path in required: c.require(path.is_file(), "required file " + str(path.relative_to(root)))
    helper, sjroot, ready, lobby, bing = (load(paths[name]) for name in paths)
    c.require(all(row["schemaVersion"] == 1 and row["stage"] == "25K-E"
                  for row in (helper, sjroot, ready, lobby, bing)), "artifact schemas")
    c.require(all(artifact_hash(row) for row in (helper, sjroot, ready)), "semantic artifact hashes")
    c.require(set(helper["classificationVocabulary"]) == CLASSES == set(sjroot["classificationVocabulary"]),
              "closed classification vocabulary")

    packages = {row["name"]: row for row in helper["packages"]}
    c.require(set(packages) == set(PINS), "four helper pins")
    c.require(all((packages[name]["version"], packages[name]["zipSha256"],
                   packages[name]["distributedDlls"][0]["sha256"]) == expected
                  for name, expected in PINS.items()), "exact helper ZIP and DLL identities")
    c.require(not helper["productionSourceRequired"] and not helper["generalDynamicDataBackend"] and
              not helper["runtimeDetourContext"] and not helper["runtimeHelperAssemblyScanning"],
              "static helper runtime boundary")
    feature_counts = {(row["kind"], row["id"]): row["occurrences"]
                      for row in helper["mapReachability"][LOBBY]}
    c.require(feature_counts == {("entity", "JungleHelper/MossyWall"): 209,
        ("entity", "YetAnotherHelper/BubbleField"): 4,
        ("trigger", "ContortHelper/MadelineSpotlightModifierTrigger"): 3,
        ("trigger", "ExtendedVariantMode/FloatExtendedVariantFadeTrigger"): 3,
        ("trigger", "ExtendedVariantMode/ResetVariantsTrigger"): 1}, "exact helper map reachability")
    evm = next(row for row in helper["lowerings"] if row["owner"] == "ExtendedVariantMode")
    c.require(set(evm["rejectedDynamicLifetimeSites"]) == EVM_SITES and len(EVM_SITES) == 9,
              "nine EVM dynamic sites remain rejected")
    census_expected = {"ContortHelper": 721, "ExtendedVariantMode": 2374,
                       "JungleHelper": 695, "YetAnotherHelper": 116}
    censuses = {row["assembly"]: row for row in helper["surfaceCensuses"]}
    c.require({name: row["methodCount"] for name, row in censuses.items()} == census_expected,
              "complete helper method census")
    c.require(helper["unclassifiedMethodCount"] == 0 and all(row["unclassifiedMethodCount"] == 0 and
              sum(row["classificationCounts"].values()) == row["methodCount"] and
              set(row["classificationCounts"]) == CLASSES for row in censuses.values()),
              "zero unclassified helper methods")
    c.require(all(row["dynamicData"]["acceptedRuntimeAfterLowering"] == 0 and
                  row["reflection"]["acceptedRuntimeModuleOrHelperDiscoveryAfterLowering"] == 0
                  for row in censuses.values()), "helper dynamic/reflection lowering boundary")

    root_pin = sjroot["package"]
    c.require(root_pin["name"] == "StrawberryJam2021" and root_pin["version"] == "1.0.12" and
              root_pin["zipSha256"] == "4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655" and
              root_pin["distributedDlls"][0]["sha256"] == ROOT_DLL, "exact root release")
    c.require(sjroot["lifecycle"] == ["constructor", "Load", "Initialize", "LoadContent", "Unload"] and
              "no reflection" in sjroot["moduleActivation"], "typed static root lifecycle")
    settings = sjroot["settings"]["fields"]
    c.require(settings[0]["name"] == "DisplayDashSequence" and settings[0]["default"] is False and
              settings[1]["name"] == "TogglePlaybacks" and settings[1]["defaultButtons"] == ["Back"] and
              settings[1]["defaultKeys"] == ["Tab"], "root settings defaults")
    c.require([row["name"] for row in sjroot["saveData"]["fields"]] ==
              ["ModifiedThemeMaps", "FilledJamJarSIDs"] and
              sjroot["saveData"]["schemaSha256"] ==
              "58f135298191f68e6cf6ec27204d82b50f93e0642b5305a3415fe48b898d6d70" and
              sjroot["saveData"]["mapProgressionFailureDomain"] == "separate AEVPSV1 unchanged",
              "root save durability schema")
    c.require(len(sjroot["session"]["fields"]) == 12 and
              "RainDensityData:{Density=1,StartDensity=1,EndDensity=1,Duration=0}" in sjroot["session"]["fields"] and
              sjroot["session"]["ephemeralUnserialized"] ==
              ["DashSequenceDisplay: live entity reference; absent from exact maps"], "root session schema")
    root_features = {row["id"]: row["occurrences"] for row in sjroot["mapReachability"][LOBBY]}
    c.require(root_features == {"SJ2021/AllInOneMask": 8, "SJ2021/BloomMask": 3,
        "SJ2021/GlowController": 1, "SJ2021/StrawberryJamJar": 21,
        "SJ2021/StylegroundMask": 39}, "exact root entity reachability")
    c.require(sjroot["uiJournalCompletion"]["completionAuthority"] == "vanilla AreaStats.Modes[0].Completed" and
              sjroot["uiJournalCompletion"]["journalRowsWhenOnlySliceInstalled"] == [BING] and
              "SetReturnToHere" in sjroot["uiJournalCompletion"]["returnToLobby"], "root lobby/journal/completion")
    c.require(sjroot["specialBerry"]["id"] == "LunaticHelper/StrawberryWithReturn" and
              sjroot["specialBerry"]["occurrences"] == 1 and
              sjroot["specialBerry"]["audio"]["availability"] == "existing vanilla bank", "exact special berry")
    c.require(len(sjroot["audioHandoff"]) == 4 and {row["eventPath"] for row in sjroot["audioHandoff"]} ==
              {"event:/sj21_jamjar-blue", "event:/sj21_BegLobby", "event:/sj21_bingovergoogle",
               "event:/sj21_levelselect"} and sjroot["customAudioCallsSilentlyDiscarded"] == 0,
              "precise four-bank audio handoff")
    root_census = sjroot["surfaceCensus"]
    c.require(root_census["methodCount"] == 2192 and root_census["unclassifiedMethodCount"] == 0 and
              sum(root_census["classificationCounts"].values()) == 2192 and
              root_census["dynamicData"]["acceptedRuntimeAfterLowering"] == 0 and
              root_census["reflection"]["acceptedRuntimeModuleOrHelperDiscoveryAfterLowering"] == 0,
              "zero-unknown root method census")

    c.require((lobby["sourceMapSha256"], lobby["fileBytes"], lobby["consumedRootBytes"], lobby["appendixBytes"],
               lobby["roomCount"]) == ("a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2",
                                         674648, 524807, 149841, 1), "Beginner lobby map identity")
    c.require((bing["sourceMapSha256"], bing["fileBytes"], bing["consumedRootBytes"], bing["appendixBytes"],
               bing["roomCount"]) == ("e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347",
                                        135363, 70024, 65339, 13), "Bing map identity")
    c.require(lobby["lobbyMetadata"]["backgroundMusic"] == "event:/sj21_levelselect" and
              lobby["collab"]["jarCount"] == 21 and lobby["collab"]["installedSubordinateMaps"] == [BING] and
              len(lobby["chapterPanels"]) == 23, "lobby static UI metadata")
    c.require(lobby["guardEvidence"]["groupedParallaxDecalCount"] == 0 and
              lobby["guardEvidence"]["taggedVanillaHeatWaveCount"] == 0 and
              bing["guardEvidence"]["groupedParallaxDecalCount"] == 0, "exact root guard evidence")

    blockers = ready["beginnerBlockerGroups"]
    c.require((blockers["stage25kc"], blockers["stage25kd"], blockers["stage25ke"]) == (7, 4, 2) and
              blockers["remaining"] == ["bounded multi-bank FMOD",
              "CrystallineHelper/VortexHelper exact bounded mechanisms"], "honest two-group recount")
    c.require(ready["unclassified"] == 0 and not ready["playableSliceBuilt"] and
              ready["playableSliceBuildIntentionallyDeferred"] and ready["mapsStillBlocked"] == [LOBBY, BING],
              "pre-slice zero-unknown boundary")
    c.require(sum(row["occurrences"] for row in ready["remainingMechanismSurface"]) == 55 and
              {row["id"] for row in ready["remainingMechanismSurface"]} ==
              {"VortexHelper/AttachedJumpThru", "vitellary/bloomstrengthtrigger",
               "vitellary/editdepthtrigger", "vitellary/triggertrigger"}, "exact Crystalline/Vortex handoff")
    coverage = ready["customIdOccurrenceCoverage"]
    c.require(sum(coverage[key] for key in ("closedByStage25keHelpers", "closedByStage25keRoot",
              "closedByStage25keSpecialBerry", "previouslyClosedOrVanilla", "remainingMechanismOccurrences")) ==
              coverage["auditedLobbyAndBing"] == 920, "complete custom occurrence accounting")
    c.require(ready["providerClosureCount"] == len(ready["providerClosure"]) == 22 and
              ready["providersWithRemainingMechanisms"] == ["CrystallineHelper", "VortexHelper"],
              "exact provider closure")
    hookgen = ready["hookGen"]
    c.require((hookgen["catalogBefore"], hookgen["catalogAfter"], hookgen["newDescriptors"],
               hookgen["appleApiMembersBefore"], hookgen["appleApiMembersAfter"], hookgen["newPublicMembers"]) ==
              (205, 205, 0, 30, 30, 0) and not hookgen["broadPublicizer"] and
              len(hookgen["exactInternalSourceAccesses"]) == 4, "no descriptor/API expansion")
    c.require(ready["configuredOrdering"]["evmNineSitesRemainRejected"] and
              ready["configuredOrdering"]["lunaticFixturePlanSha256"] ==
              "bfbae569352041d00d98c5792354bc403423566ad796a69912431392111a9895",
              "configured ordering regression boundary")
    c.require(ready["mapAppendix"] == {"mapsWithAppendix": 124, "totalBytes": 19240291,
              "maxBytes": 1260932, "hardPerMapBound": 2097152,
              "originalHashesAndProgressionIdentitiesUnchanged": True}, "map appendix regression")

    deterministic = ready["determinism"]
    c.require(deterministic.get("runsIdentical") and deterministic.get("completeTreeCompared") and
              len(deterministic.get("omissions", [])) == 4 and
              all(row["managedCompile"] and row["unrelatedPlansIdentical"] and row["controlMapsIdentical"]
                  for row in deterministic["omissions"]), "three runs and four omission controls")
    c.require(len(deterministic["closure"]["sharedClosureSha256"]) == 64 and
              deterministic["closure"]["staticSemanticRuntimePatchSha256"] is not None,
              "shared semantic closure identity")
    canary = required[-2].read_text()
    c.require(all(name in canary for name in ["MadelineSpotlightModifierTrigger", "FloatExtendedVariantFadeTrigger",
              "MossyWall", "BubbleField", "stage25keRootState", "StrawberryWithReturn"]),
              "data-only semantic canary coverage")
    c.require('texture="bgs/01/bg0"' in canary, "canary references a real vanilla gameplay-atlas backdrop")
    static_plan = (root / "tools/AppleEverestBuilder/StaticSemanticLowering.cs").read_text()
    patches = (root / "tools/AppleEverestBuilder/StaticSemanticRuntimePatches.cs").read_text()
    c.require(all(token in static_plan for token in ["contorthelper-1.5.5-sj-beginner-v1",
              "extendedvariantmode-0.50.5-sj-beginner-v1", "junglehelper-1.4.10-sj-beginner-v1",
              "yetanotherhelper-1.2.5-sj-beginner-v1", "strawberryjam2021-1.0.12-beginner-root-v1"]),
              "hash-locked production plans")
    c.require("DynamicData" not in patches and "RuntimeDetour" not in patches and "Assembly.Load" not in patches,
              "static source patch runtime boundary")
    root_state = (root / "apple-everest/runtime/semantics/AppleEverestStrawberryJamState.cs").read_text()
    c.require("AppleEverest/Mods/StrawberryJam2021/Graphics/StrawberryJam2021/CustomEntitySprites.xml" in root_state,
              "root SpriteBank reads its mounted static content path")
    c.require('TransformerVersion = "apple-everest-static-v19"' in
              (root / "tools/AppleEverestBuilder/Models.cs").read_text(), "v19 transformer identity")

    report = required[-1].read_text()
    c.require(set(map(str, range(1, 96))).issubset(set(re.findall(r"^\s*(\d+)\.\s", report, re.MULTILINE))),
              "all 95 final questions answered")
    stage_paths = required + list(paths.values()) + [root / "docs/APPLE_EVEREST_STATIC_AOT.md",
                                                    root / "docs/APPLE_EVEREST_COMPATIBILITY.md"]
    forbidden = re.compile(r"/Users/|/private/|mobileprovision|device identifier|BEGIN (?:RSA |EC )?PRIVATE KEY", re.I)
    c.require(not any(forbidden.search(path.read_text(errors="replace")) for path in stage_paths), "privacy scan")
    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".zip", ".dll", ".bank", ".ipa", ".bin"))
                      for path in tracked if "stage25ke" in path.lower()), "no tracked binary inputs/products")
    c.require(git(root, "diff", "--name-only", START, "--", ".github/workflows") == "", "zero Actions changes")
    c.require(git(root, "merge-base", "--is-ancestor", START, "HEAD") == "", "feature descends from K-D tip")
    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC and
              git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1 and
              git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2 and
              git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3, "protected refs unchanged")
    c.require(subprocess.run(["git", "-C", str(root), "rev-parse", "v1.0.0-rc.3^{}"],
                             capture_output=True).returncode != 0, "rc3 tag remains absent")
    verify_stage25kc_in_historical_ref_view(root)
    c.require(True, "unchanged K-C audit verifier in isolated historical-ref view")

    if args.fixture_root or args.audit_root or args.reproduction_json:
        c.require(all((args.fixture_root, args.audit_root, args.reproduction_json)),
                  "fixture, audit, and reproduction inputs supplied together")
        with tempfile.TemporaryDirectory(prefix="stage25ke-verify-") as temp:
            generated = pathlib.Path(temp)
            subprocess.run([sys.executable, str(root / "scripts/generate-apple-everest-stage25ke.py"),
                "--fixture-root", str(args.fixture_root.resolve()), "--audit-root", str(args.audit_root.resolve()),
                "--reproduction-json", str(args.reproduction_json.resolve()), "--output-root", str(generated)],
                cwd=root, check=True)
            for name, tracked_path in paths.items():
                c.require((generated / name).read_bytes() == tracked_path.read_bytes(), "deterministic regenerated " + name)
    if args.require_clean: c.require(git(root, "status", "--porcelain") == "", "clean worktree")
    print(f"PASS: Stage 25K-E verifier ({c.count} checks)")
    return 0


if __name__ == "__main__": raise SystemExit(main())
