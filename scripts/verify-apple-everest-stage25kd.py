#!/usr/bin/env python3
"""Fail-closed verification for Stage 25K-D tracked evidence and production policy."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import subprocess


START = "d6bdaa4cf6d4c8ccf451d6ac1bfb66cde5d0dc10"
ROOT_ZIP = "4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655"
ROOT_DLL = "8d5b9184204e7e7728965bcf95af5f150f6220dafbfe52fdd6c23e50d47e5258"
LUNATIC_ZIP = "b10e044b1dfa412605bee3ba6bfdd4263591099d331d6c1aab1e9ba65bf6a3e5"
LUNATIC_DLL = "fc08f00296551a6025c5e31422c6edd5e8136e6861459f44ffea909129c6a925"
MONOMOD_COMMIT = "dfc30a1506d37fb88a2c2be004f525205f46a24c"


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: bool, message: str) -> None:
        if not condition:
            raise SystemExit("FAIL: " + message)
        self.count += 1


def load(path: pathlib.Path) -> dict:
    return json.loads(path.read_text())


def sha(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def git(root: pathlib.Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(root), *args], check=True,
                          capture_output=True, text=True).stdout.strip()


def target_fingerprint(targets: list[dict]) -> str:
    rows = sorted(value["hookKind"] + "\0" + value["target"] + "\0" +
                  value["hookOrManipulator"] for value in targets)
    return sha("\n".join(rows).encode())


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--require-clean", action="store_true")
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    artifact_root = root / "apple-everest"
    configured_path = artifact_root / "configured-ordering-stage25kd.json"
    breadth_path = artifact_root / "sj-beginner-breadth-stage25kd.json"
    appendix_path = artifact_root / "map-appendix-stage25kd.json"
    report_path = root / "docs/history/stages/APPLE_EVEREST_CONFIGURED_ORDERING_STAGE25KD_REPORT.md"
    required = [configured_path, breadth_path, appendix_path, report_path,
                root / "scripts/verify-apple-everest-stage25kd.sh",
                root / "tools/AppleEverestBuilder/ConfiguredDetourOrdering.cs",
                root / "tools/AppleEverestBuilder/HookDescriptorDiscovery.cs",
                root / "tools/AppleEverestBuilder/StaticConfiguredDetourCompatibility.cs",
                root / "tools/AppleEverestBuilder/tests/PinnedOrdering/Program.cs"]
    for path in required:
        c.require(path.is_file(), f"required file {path.relative_to(root)}")

    configured = load(configured_path)
    breadth = load(breadth_path)
    appendix = load(appendix_path)
    c.require(all(value["schemaVersion"] == 1 and value["stage"] == "25K-D" and
                  value["status"] == "GREEN" for value in (configured, breadth, appendix)),
              "green artifact schemas")

    release = appendix["authoritativeRelease"]
    c.require(release["version"] == "1.0.12" and release["zipSha256"] == ROOT_ZIP and
              release["rootDllSha256"] == ROOT_DLL, "unchanged Strawberry Jam release pin")
    summary = appendix["summary"]
    maps = appendix["maps"]
    c.require((summary["mapCount"], summary["appendixMapCount"],
               summary["noAppendixMapCount"], summary["appendixBytes"],
               summary["maximumObservedAppendixBytes"]) ==
              (128, 124, 4, 19240291, 1260932), "exact map appendix census")
    c.require(len(maps) == 128 and len({row["logicalMap"] for row in maps}) == 128 and
              sum(row["appendixBytes"] for row in maps) == 19240291 and
              all(row["consumedRootBytes"] + row["appendixBytes"] == row["fileBytes"]
                  for row in maps), "complete root/appendix boundaries")
    c.require(all(len(row["sourceMapSha256"]) == 64 and len(row["appendixSha256"]) == 64 and
                  not row["appendixInterpreted"] for row in maps), "complete map hashes")
    bing = next(row for row in maps if row["logicalMap"] ==
                "StrawberryJam2021/1-Beginner/Bing_Over_Google")
    c.require((bing["fileBytes"], bing["consumedRootBytes"], bing["appendixBytes"],
               bing["appendixSha256"]) ==
              (135363, 70024, 65339,
               "963d5f086279311c18561df8fb135e445ca1cf0bcc2d3f1ba0d8b21f8376da6d"),
              "minimum-slice real map boundary")
    policy = appendix["policy"]
    c.require(policy["maximumAppendixBytes"] == 2 * 1024 * 1024 and
              not policy["malformedPrimaryRootAccepted"] and
              not policy["truncatedPrimaryRootAccepted"] and
              not policy["stagedMapMutation"] and
              policy["sourceMapIdentity"] == "SHA256_OVER_COMPLETE_ORIGINAL_FILE",
              "bounded non-permissive appendix policy")

    census = configured["census"]
    c.require((census["siteCount"], census["helperCount"],
               census["detourConfigReferenceCount"], census["detourContextReferenceCount"]) ==
              (39, 13, 67, 38), "revalidated K-C configured census")
    c.require((census["staticallyEligibleSiteCount"], census["dynamicLifetimeSiteCount"],
               census["dynamicTargetSiteCount"], census["dynamicManipulatorSiteCount"]) ==
              (30, 9, 0, 0) and census["zeroUnclassifiedSites"],
              "static eligibility split")
    sites = configured["sites"]
    c.require(len(sites) == 39 and [row["siteOrdinal"] for row in sites] == list(range(1, 40)) and
              len({row["package"] for row in sites}) == 13, "all 39 ordered site records")
    c.require("UNKNOWN" not in json.dumps(sites).upper(), "zero UNKNOWN configured fields")
    c.require(all(row["targetSetFingerprintSha256"] == target_fingerprint(row["targets"]) and
                  len(row["assemblySha256"]) == len(row["constructionSiteFingerprintSha256"]) == 64 and
                  row["configurationPhases"] for row in sites), "site target/config fingerprints")
    c.require(sum(row["eligibility"] == "STATIC_CONFIGURED_DETOUR_SEQUENCE" for row in sites) == 30 and
              sum(row["eligibility"] == "REJECT_DYNAMIC_LIFETIME" for row in sites) == 9 and
              {row["package"] for row in sites if row["eligibility"] == "REJECT_DYNAMIC_LIFETIME"} ==
              {"ExtendedVariantMode"}, "dynamic lifetime remains rejected")
    c.require((census["hookGenIlSiteCount"], census["directIlHookSiteCount"],
               census["configuredIlSiteCount"], census["directManagedHookSiteCount"],
               census["hookGenOnSiteCount"]) == (8, 7, 13, 4, 22),
              "exact directly evidenced configured hook-kind site counts")

    pinned = configured["pinnedReference"]
    cases = pinned["cases"]
    c.require(pinned["monoModCommit"] == MONOMOD_COMMIT and
              pinned["runtimeVersion"] == "25.3.1.0", "pinned MonoMod reference")
    expected_chains = {
        "priority": ["high", "low", "ordinary"],
        "before": ["before", "after"], "after": ["second", "first"],
        "beforeWildcard": ["before-wildcard", "peer", "ordinary"],
        "afterWildcard": ["after-wildcard", "peer", "ordinary"],
        "defaultAndTie": ["tie-b", "tie-a", "ordinary"],
        "beforeAllNormalized": ["peer", "before-all", "ordinary"],
        "afterAllNormalized": ["after-all", "peer", "ordinary"],
        "multipleIds": ["first", "middle", "last"],
    }
    c.require(all(cases[name]["chain"] == chain for name, chain in expected_chains.items()),
              "pinned ordering chains")
    c.require(cases["cycleRejected"]["rejected"] and
              cases["ilAThenB"]["value"] == 3 and
              cases["ilBThenA"]["value"] == 2,
              "cycle and non-commutative IL conformance")

    fixture = configured["selectedRealFixture"]
    c.require(fixture["package"] == "LunaticHelper" and fixture["version"] == "1.1.1" and
              fixture["zipSha256"] == LUNATIC_ZIP and fixture["dllSha256"] == LUNATIC_DLL and
              not fixture["sourceRequiredForProduction"] and
              fixture["planSha256"] ==
              "016bb7899f8622318936ef96933c69486fb183e97f35234bae481b630f77a384",
              "ordinary distributed real configured fixture")
    c.require(fixture["target"] ==
              "System.Void Celeste.Player::.ctor(Microsoft.Xna.Framework.Vector2,Celeste.PlayerSpriteMode)" and
              fixture["configSource"] == {"id": "LunaticHelper", "after": ["*"],
                                            "legacyContext": True} and
              fixture["hookKind"] == "HOOKGEN_ON" and
              all(len(fixture[key]) == 64 for key in
                  ("canonicalTargetInstructionSha256", "desktopPatchedBaselineTargetInstructionSha256",
                   "appleFinalTargetInstructionSha256", "semanticDiffSha256")),
              "real target/config/hook body evidence")
    deterministic = configured["determinism"]
    c.require(deterministic["independentClosureRunCount"] == 3 and deterministic["identical"] and
              len({row["sharedClosureSha256"] for row in deterministic["runs"]}) == 1 and
              len({row["configuredPlanSha256"] for row in deterministic["runs"]}) == 1 and
              len({row["productClosureFileSetSha256"] for row in deterministic["runs"]}) == 1,
              "three independent deterministic closures")
    boundary = configured["runtimeBoundary"]
    c.require(not any(boundary.values()), "no runtime detour/IL/config graph/interpreter boundary")

    descriptor = breadth["descriptorBreadth"]
    api = breadth["apiBreadth"]
    c.require((descriptor["catalogBefore"], descriptor["catalogAfter"],
               descriptor["newDescriptorCount"], descriptor["newSourceBackedCount"],
               descriptor["newPinnedPatchedApiCount"]) == (102, 205, 103, 98, 5) and
              descriptor["unresolvedCount"] == 0, "exact HookGen descriptor expansion")
    c.require(sum(row.get("provenance") == "PINNED_PATCHED_APPLE_API"
                  for row in descriptor["targets"]) == 5,
              "five exact pinned-patched Apple API descriptors")
    catalog = load(artifact_root / "managed-detour-targets-v2.json")
    c.require(len(catalog["targets"]) == 205 and
              {row["id"] for row in descriptor["targets"]}.issubset(
                  {row["id"] for row in catalog["targets"]}), "production descriptor catalog")
    c.require((api["referenceSiteCount"], api["newExactMemberCount"], api["missingAfter"],
               api["nonpublicAfter"]) == (326, 29, 0, 0) and
              len(api["newExactMembers"]) == 29, "reviewed exact API breadth")
    c.require(len(breadth["providers"]) == 8 and
              all(not row["unresolvedDescriptorCount"] and not row["missingAppleApiCount"] and
                  not row["nonpublicAppleApiCount"] for row in breadth["providers"]) and
              not breadth["scope"]["broadPublicizerUsed"], "eight-provider exact closure")
    recount = breadth["beginnerSliceRecount"]
    c.require((recount["blockerGroupsBefore"], recount["blockerGroupsAfter"],
               len(recount["closed"]), len(recount["remaining"])) == (7, 4, 3, 4) and
              not recount["sliceReadyToBuild"], "honest Beginner blocker recount")
    payoff = breadth["fullStrawberryJamPayoff"]
    c.require(payoff["configuredSitesStaticEligible"] == 30 and
              payoff["configuredSitesDynamicLifetime"] == 9 and
              payoff["configuredOrderingAffectedMapsAfter"] == 128 and
              payoff["mapsUnlockedByConfiguredOrdering"] == 0 and
              payoff["packageCoverage"]["accepted"] == 17 and
              payoff["zeroKnownBlockerMaps"]["accepted"] == 0,
              "honest full-SJ payoff boundary")

    ordering_source = (root / "tools/AppleEverestBuilder/ConfiguredDetourOrdering.cs").read_text()
    compatibility_source = (root / "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs").read_text()
    content_source = (root / "tools/AppleEverestBuilder/ContentCompiler.cs").read_text()
    c.require("STATIC_CONFIGURED_DETOUR_SEQUENCE" in compatibility_source and
              "DETOUR_CONFIG_DEFERRED" in compatibility_source and
              "RuntimeDetour" not in (root / "apple-everest/runtime/LegacyMonoModConfiguredStaticFacade.cs").read_text(),
              "production static-only configured policy")
    c.require("STATIC_CONFIG_ORDER_CYCLE" in ordering_source and
              "MaxMapAppendixBytes = 2L * 1024 * 1024" in content_source,
              "fail-closed order cycle and appendix bound")
    canary_source = (root / "scripts/build-apple-everest-canary.sh").read_text()
    c.require("--configured-fixture" in canary_source and "build-configured-fixture" in canary_source,
              "canary has an explicit configured-fixture lane")
    everest_api_source = (root / "apple-everest/runtime/EverestStaticApi.cs").read_text()
    static_compatibility_source = (
        root / "tools/AppleEverestBuilder/StaticAotCompatibility.cs").read_text()
    c.require(
        "public static int AttrInt(this global::Celeste.BinaryPacker.Element element," in
        everest_api_source and
        "CultureInfo.InvariantCulture" in everest_api_source and
        "public static void Rumble(RumbleStrength strength, RumbleLength length) =>" in
        static_compatibility_source and
        "Rumble(strength, length, null);" in static_compatibility_source,
        "exact configured-fixture public Everest ABI")

    report = report_path.read_text()
    answers = set(re.findall(r"^\s*(\d+)\.\s", report, re.MULTILINE))
    c.require(set(map(str, range(1, 93))).issubset(answers), "all 92 final questions answered")
    scan_paths = [*required, configured_path, breadth_path, appendix_path]
    forbidden = re.compile(r"/Users/|/private/|mobileprovision|BEGIN (?:RSA |EC )?PRIVATE KEY", re.I)
    c.require(not any(forbidden.search(path.read_text(errors="replace")) for path in scan_paths),
              "privacy scan")
    c.require(git(root, "diff", "--name-only", START, "--", ".github/workflows") == "",
              "zero Actions changes")
    c.require(not any(path.lower().endswith((".zip", ".dll", ".bank", ".ipa", ".bin"))
                      for path in git(root, "ls-files").splitlines()
                      if "stage25kd" in path.lower()), "no tracked third-party or product binaries")
    c.require(git(root, "merge-base", "--is-ancestor", START, "HEAD") == "",
              "feature descends from exact Stage D base")
    if args.require_clean:
        c.require(git(root, "status", "--porcelain") == "", "clean worktree")
    print(f"PASS: Stage 25K-D verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
