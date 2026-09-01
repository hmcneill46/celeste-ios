#!/usr/bin/env python3
"""Verify deterministic Stage 25K-C Strawberry Jam audit evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import subprocess
import sys
import tempfile


START = "d17f84796eaca56b2cd427f444492bb7a5bc2c0d"
TVOS = "311590334cf7180c2f9df0e8000578c14fa68fad"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
ROOT_ZIP = "4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655"
ROOT_DLL = "8d5b9184204e7e7728965bcf95af5f150f6220dafbfe52fdd6c23e50d47e5258"
ARTIFACTS = [
    "strawberry-jam-stage25kc.json",
    "strawberry-jam-dependency-graph-stage25kc.json",
    "strawberry-jam-content-usage-stage25kc.json",
    "strawberry-jam-mechanisms-stage25kc.json",
    "third-collab-control-stage25kc.json",
]


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: bool, message: str) -> None:
        if not condition:
            raise SystemExit("FAIL: " + message)
        self.count += 1


def canonical(value: object) -> bytes:
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n").encode()


def sha_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def git(root: pathlib.Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(root), *args], check=True,
                          capture_output=True, text=True).stdout.strip()


def load(path: pathlib.Path) -> dict:
    return json.loads(path.read_text())


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--fixture-root", type=pathlib.Path,
                        help="also regenerate and byte-compare all five artifacts")
    parser.add_argument("--require-clean", action="store_true")
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    artifact_root = root / "apple-everest"
    paths = {name: artifact_root / name for name in ARTIFACTS}
    required = [*paths.values(),
        root / "scripts/generate-apple-everest-stage25kc.py",
        root / "scripts/fetch-apple-everest-stage25kc-fixtures.py",
        root / "scripts/verify-apple-everest-stage25kc.sh",
        root / "tools/AppleEverestBuilder/AssemblyMechanismCensus.cs",
        root / "tools/AppleEverestBuilder/tests/ProgressionScaleAudit.cs",
        root / "docs/history/stages/APPLE_EVEREST_STRAWBERRY_JAM_AUDIT_STAGE25KC_REPORT.md",
    ]
    for path in required:
        c.require(path.is_file(), f"required file {path.relative_to(root)}")

    summary = load(paths[ARTIFACTS[0]])
    graph = load(paths[ARTIFACTS[1]])
    content = load(paths[ARTIFACTS[2]])
    mechanisms = load(paths[ARTIFACTS[3]])
    control = load(paths[ARTIFACTS[4]])
    c.require(all(value["schemaVersion"] == 1 and value["stage"] == "25K-C"
                  for value in (summary, graph, content, mechanisms, control)), "artifact schemas")
    c.require(summary["status"] == "PASS — GREEN_AUDIT" and
              summary["productSupportClaim"] == "STRAWBERRY_JAM_NOT_YET_SUPPORTED",
              "audit-only status boundary")
    acquisition = summary["acquisition"]
    c.require(acquisition["version"] == "1.0.12" and acquisition["zipSha256"] == ROOT_ZIP and
              acquisition["rootDllSha256"] == ROOT_DLL and acquisition["zipBytes"] == 95650820,
              "root release identity")

    material = {"nodes": graph["nodes"], "requiredEdges": graph["requiredEdges"],
                "missingRuntimeEdges": graph["runtimeDependenciesNotDownloaded"]}
    c.require(graph["graphSha256"] == sha_bytes(canonical(material)) == summary["pins"]["dependencyGraphSha256"],
              "dependency graph hash")
    c.require(graph["nodeCount"] == len(graph["nodes"]) == 52 and graph["zeroUnknownDependencies"],
              "complete 52-node required graph")
    c.require(len({row["name"] for row in graph["nodes"]}) == 52 and
              all(len(row["zipSha256"]) == 64 and row["classification"][:1] in "ABCDEFG"
                  for row in graph["nodes"]), "node identities and classifications")
    c.require(sum(row["zipBytes"] for row in graph["nodes"]) == 1237284560,
              "exact compressed closure size")

    scale = summary["scale"]
    c.require((scale["packages"], scale["codeHelperPackages"], scale["physicalDllFiles"]) == (52, 46, 47),
              "package and DLL scale")
    c.require((scale["maps"], scale["rooms"], scale["lobbies"]) == (128, 2585, 6),
              "map/lobby scale")
    c.require((scale["contentFiles"], scale["expandedBytes"], scale["pngFiles"],
               scale["yamlFiles"], scale["mapBins"], scale["luaFiles"], scale["bankFiles"]) ==
              (48475, 1343650014, 43463, 519, 132, 1303, 149), "content scale")
    c.require(scale["mapBinaryAppendixCount"] == 124 and
              "fails closed" in scale["mapBinaryAppendixConclusion"], "bounded map appendix blocker")
    c.require(summary["structure"]["ordinaryMapCount"] == 111 and
              summary["structure"]["heartSideCount"] == 5 and
              len(summary["structure"]["gyms"]) == 6, "map hierarchy")

    c.require(content["customIdCount"] == len(content["usage"]) == 722 and
              content["occurrenceCount"] == 274073 and content["unresolvedProviderCount"] == 0,
              "exact custom-ID ownership")
    c.require(len(content["maps"]) == 128 and len(content["top20Providers"]) == 20 and
              content["top20Providers"][0]["provider"] == "VivHelper" and
              content["top20Providers"][0]["occurrences"] == 97260, "content census summaries")
    c.require(all(len(row["providers"]) == 1 and row["providerResolution"] != "UNRESOLVED"
                  for row in content["usage"]), "zero ambiguous providers")

    expected_classes = {"A_ACCEPTED_UNCHANGED": 17,
        "B_ACCEPTED_MECHANISM_NEEDS_MORE_EXACT_DESCRIPTORS": 17,
        "C_STATIC_SEMANTIC_LOWERING_PLAUSIBLE": 6,
        "D_BOUNDED_NEW_MECHANISM_REQUIRED": 11,
        "E_MAJOR_UNSUPPORTED_RUNTIME_CLASS": 1,
        "F_NOT_ACTUALLY_USED_BY_SELECTED_CONTENT": 0, "G_UNKNOWN": 0}
    c.require(summary["classificationCounts"] == expected_classes and
              sum(expected_classes.values()) == 52 and summary["unknownMajorMechanisms"] == 0,
              "total zero-unknown helper taxonomy")

    counts = mechanisms["aggregateCounts"]
    c.require(mechanisms["dllCount"] == len(mechanisms["perDll"]) == 47, "per-DLL census")
    c.require((counts["onHookSubscriptions"], counts["ilEventSubscriptions"],
               counts["directHookConstructors"], counts["directIlHookConstructors"]) ==
              (1497, 606, 79, 231), "hook and IL census")
    c.require((counts["dynamicDataCalls"], counts["dynDataCalls"], counts["modInteropCalls"]) ==
              (1083, 639, 49), "DynamicData and ModInterop census")
    configured = mechanisms["configuredHooks"]
    c.require((configured["helperCount"], configured["siteCount"], configured["affectedMapCount"]) ==
              (13, 39, 128) and not configured["unknownProviders"], "configured hook census")
    c.require(mechanisms["dynamicData"]["totalCalls"] == 1722 and
              mechanisms["dynamicData"]["ownerClassCount"] == 296, "DynamicData classes")
    c.require(mechanisms["modInterop"]["resolvedRequiredGraphImports"] == 13 and
              mechanisms["modInterop"]["optionalAbsentImports"] == 5 and
              mechanisms["modInterop"]["unsupportedSignatures"] == 0, "static ModInterop closure")
    c.require(mechanisms["luaNative"]["luaRequired"] and
              mechanisms["luaNative"]["mapCount"] == 15 and
              mechanisms["luaNative"]["distributedNativeFileCount"] == 0 and
              mechanisms["luaNative"]["pinvokeMethodCount"] == 0 and
              mechanisms["luaNative"]["runtimeAssemblyLoadCount"] == 0 and
              mechanisms["luaNative"]["processCount"] == 0 and
              mechanisms["luaNative"]["fileSystemWatcherCount"] == 0, "Lua/native boundary")

    audio = summary["audio"]
    c.require((audio["bankCount"], audio["guidExportCount"], audio["rawGuidRecords"]) == (149, 117, 71976),
              "custom bank and GUID scale")
    c.require(audio["uniquePathsByKind"]["event"] == 1706 and
              len(audio["incompatibleGuidCollisions"]) == 36 and
              len(audio["incompatiblePathCollisions"]) == 5, "audio events and collisions")
    c.require(audio["masterBankCount"] == audio["stringsBankCount"] ==
              audio["programmerSoundCount"] == audio["nativePluginCount"] == 0 and
              all(row["classification"] == "BOUNDED_MULTI_BANK_EXTENSION" for row in audio["banks"]),
              "audio blocker classification")

    fixtures = summary["progressionScale"]["fixtures"]
    c.require([(row["rawBytes"], row["compressedBytes"], row["withinReplicaCap"]) for row in fixtures] ==
              [(38921, 7244, True), (41925, 10103, True), (48101, 14516, True)],
              "exact 128-map progression fixtures")
    c.require(summary["atlasStress"]["estimatedMetadataResidentBytes"] == 1390816 and
              summary["atlasStress"]["largestFirstUseDecodedRgbaBytes"] == 15269888 and
              not summary["atlasStress"]["eagerDecodeRegression"], "deferred atlas scale")
    coverage = summary["coverage"]
    c.require((coverage["package"]["accepted"], coverage["package"]["total"]) == (17, 52) and
              coverage["contentIdOccurrence"]["accepted"] == 43350 and
              coverage["map"]["zeroKnownBlocker"] == 0 and
              coverage["audio"]["acceptedBanks"] == 0 and
              coverage["progression"]["percent"] == 100.0, "honest separate coverage metrics")
    slice_value = summary["verticalSlice"]
    c.require(slice_value["lobby"] == "StrawberryJam2021/0-Lobbies/1-Beginner" and
              slice_value["maps"] == ["StrawberryJam2021/1-Beginner/Bing_Over_Google"] and
              slice_value["blockerCount"] == len(slice_value["blockersInOrder"]) == 7 and
              not slice_value["luaOrNativeBlocker"], "minimum vertical slice")

    c.require(control["candidateCount"] == len(control["candidates"]) == 10 and
              len(control["packagePlan"]) == 48, "third-collab audit breadth")
    selected = control["selected"]
    c.require(selected["name"] == "CrossoverCollabAprilDemo" and selected["mapCount"] == 2 and
              selected["closurePackageCount"] == 9 and selected["blockerCount"] == 7 and
              control["comparison"]["easierThanFirstStrawberryJamSlice"] and
              not control["comparison"]["moreArchitecturallyValuable"], "third-collab control result")
    c.require(summary["recommendedNextStage"]["name"].startswith("Stage 25K-D — configured detour/IL ordering"),
              "next-stage recommendation")
    c.require(not summary["runtimeProductionCodeChanged"], "audit-only production boundary")

    report = required[-1].read_text()
    answers = re.findall(r"^\s*(\d+)\.\s", report, re.MULTILINE)
    c.require(set(map(str, range(1, 99))).issubset(answers), "all 98 final questions answered")
    for match in re.finditer(r"\[[^]]+\]\(([^)]+)\)", report):
        link = match.group(1).split("#", 1)[0]
        if link and not re.match(r"^[a-z]+://", link):
            c.require((required[-1].parent / link).resolve().exists(), f"report link {link}")

    stage_paths = [path for path in required if path.is_file()] + list(paths.values())
    forbidden = re.compile(r"/Users/|/private/|device identifier|mobileprovision|BEGIN (?:RSA |EC )?PRIVATE KEY", re.I)
    c.require(not any(forbidden.search(path.read_text(errors="replace")) for path in stage_paths),
              "privacy scan")
    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".zip", ".dll", ".bank", ".ipa", ".bin"))
                      for path in tracked if "stage25kc" in path.lower()), "no tracked reviewed binaries")

    c.require(git(root, "rev-parse", "tvos-port") == TVOS and
              git(root, "rev-parse", "origin/tvos-port") == TVOS and
              git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC and
              git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1 and
              git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2 and
              git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3,
              "protected refs unchanged")
    c.require(subprocess.run(["git", "-C", str(root), "rev-parse", "v1.0.0-rc.3^{}"],
                             capture_output=True).returncode != 0, "rc3 tag remains absent")

    if args.fixture_root:
        fixture = args.fixture_root.resolve()
        with tempfile.TemporaryDirectory(prefix="stage25kc-verify-") as temp:
            generated = pathlib.Path(temp)
            subprocess.run([sys.executable, str(root / "scripts/generate-apple-everest-stage25kc.py"),
                            "--fixture-root", str(fixture), "--output-root", str(generated)],
                           cwd=root, check=True)
            for name, tracked_path in paths.items():
                c.require((generated / name).read_bytes() == tracked_path.read_bytes(),
                          f"deterministic regenerated {name}")
    if args.require_clean:
        c.require(git(root, "status", "--porcelain") == "", "clean worktree")
    c.require(git(root, "merge-base", "--is-ancestor", START, "HEAD") == "", "feature descends from K-B base")
    print(f"PASS: Stage 25K-C verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
