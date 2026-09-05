#!/usr/bin/env python3
"""Verify K-I's reproducible YELLOW stop; this cannot certify an Apple product."""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
START = "fe46b3c97cfc7dd74a907da8871ff9ba2c57d6e3"
KG = "e0d7c1988a9e5a896001735e5fe21d2251446bd0"
ARTIFACTS = ["sj-beginner-" + name + "-stage25ki.json" for name in
             ("slice", "content", "factory-closure", "physical")]
MAP_PINS = {
    "lobby": ("Maps/StrawberryJam2021/0-Lobbies/1-Beginner.bin",
        "a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2", 674648, 524807, 149841,
        "d76dba1bc5834d999f4d622afd008a702ffe6663b17fe7b0be3c5cc69afabe0d"),
    "bing": ("Maps/StrawberryJam2021/1-Beginner/Bing_Over_Google.bin",
        "e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347", 135363, 70024, 65339,
        "963d5f086279311c18561df8fb135e445ca1cf0bcc2d3f1ba0d8b21f8376da6d"),
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def git(*args):
    return subprocess.check_output(["git", "-C", str(ROOT), *args], text=True).strip()


def file_sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify_plan(plan):
    rows = plan["packages"]
    require(plan["id"] == "stage25ki-sj-beginner-slice-v1", "exact content plan identity")
    require(len(rows) == 14 and len({row["name"] for row in rows}) == 14, "exact package set")
    sj = next(row for row in rows if row["name"] == "StrawberryJam2021")
    maps = [row["path"] for row in sj["includedFiles"] if row["path"].lower().endswith(".bin")]
    require(sorted(maps) == sorted(pin[0] for pin in MAP_PINS.values()), "unrelated SJ map BIN rejected")
    require(sum(len(row["includedFiles"]) for row in rows) == 1212, "exact selected file count")
    require(len(sj["includedFiles"]) == 1178, "exact SJ-root file count")
    for row in rows:
        paths = [entry["path"] for entry in row["includedFiles"]]
        require(paths == sorted(set(paths)), "duplicate or unordered selected path")
        require(all(re.fullmatch(r"[0-9a-f]{64}", entry["sha256"]) for entry in row["includedFiles"]),
                "invalid selected file hash")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--regenerated-root", type=Path)
    parser.add_argument("--require-clean", action="store_true")
    args = parser.parse_args()
    ae = ROOT / "apple-everest"
    values = {name: json.loads((ae / name).read_text()) for name in ARTIFACTS}
    for name, value in values.items():
        if "artifactSha256" in value:
            canonical = dict(value)
            claimed = canonical.pop("artifactSha256")
            actual = hashlib.sha256((json.dumps(canonical, sort_keys=True, separators=(",", ":"),
                                               ensure_ascii=False) + "\n").encode()).hexdigest()
            require(actual == claimed, "artifact digest: " + name)
        if args.regenerated_root:
            require((ae / name).read_bytes() == (args.regenerated_root / name).read_bytes(),
                    "independent reproduction differs: " + name)
    plan = values[ARTIFACTS[1]]
    verify_plan(plan)
    kg_plan = json.loads(git("show", KG + ":apple-everest/sj-beginner-content-stage25kg.json"))
    require(plan["packages"] == kg_plan["packages"], "plan differs from immutable K-G")
    negative = copy.deepcopy(plan)
    next(row for row in negative["packages"] if row["name"] == "StrawberryJam2021")["includedFiles"].append(
        {"path": "Maps/StrawberryJam2021/1-Beginner/Unrelated.bin", "sha256": "0" * 64})
    try:
        verify_plan(negative)
    except ValueError as exception:
        require(str(exception) == "unrelated SJ map BIN rejected", "negative map guard failed for wrong reason")
    else:
        raise ValueError("unrelated map negative control was accepted")
    result, factories, physical = values[ARTIFACTS[0]], values[ARTIFACTS[2]], values[ARTIFACTS[3]]
    require(result["status"] == "YELLOW_READINESS_NOT_ESTABLISHED", "must retain YELLOW")
    require(result["startSha"] == START and not result["kgMerged"], "exact accepted start, K-G unmerged")
    require(result["contentPlan"]["sha256"] == file_sha(ae / ARTIFACTS[1]), "selected content plan binding")
    require(result["contentPlan"]["shippedSjMapBins"] == 0 and result["contentPlan"]["excludedSjMapBins"] == 126,
            "two-map plan is not a shipped product")
    for label, pin in MAP_PINS.items():
        row = result["maps"][label]
        require(tuple(row[key] for key in ("path", "sha256", "bytes", "binaryPackerRootBytes", "appendixBytes",
                                          "appendixSha256")) == pin, "original map/boundary lock: " + label)
        require(row["physicalSpawn"] is None and row["compatibilityId"] is None and not row["packagedInProduct"],
                "no invented product or physical map evidence")
    require(result["maps"]["bing"]["expectedInitialSpawn"] == {"room": "00- intro", "x": 264, "y": 152},
            "authored Bing spawn")
    require(result["freshContentIdRegistrationCensus"] == {"selected": 920, "acceptedRegistration": 511,
        "blockedRegistration": 409, "unclassifiedIds": 0}, "fresh occurrence availability census")
    require(factories["registrationCensus"] == {"selected": 73, "available": 30, "unavailable": 43},
            "factory availability census")
    require(factories["fullyClosedFactoryCount"] is None and result["fullyClosedFactoryCount"] is None and
            not result["bothReadinessGatesPassed"], "no invented semantic-closure proof")
    require(factories["historicalGraphSha256"] == file_sha(ae / "selected-factory-type-closure-stage25kh.json"),
            "historical graph identity")
    rows = factories["factories"]
    require(len(rows) == 73 and len({(row["kind"], row["customId"]) for row in rows}) == 73, "all selected factories")
    require(sum(row["occurrences"] for row in rows) == 920, "all selected occurrences")
    missing = [row for row in rows if row["status"] != "AVAILABLE_ACCEPTED_REGISTRATION"]
    require(len(missing) == 43 and sum(row["occurrences"] for row in missing) == 409, "all unavailable occurrences")
    require(len(factories["providers"]) == 21, "all exact provider inputs")
    require(sum(row["status"] == "REJECTED_PROVIDER" for row in factories["providers"]) == 8, "rejected providers")
    for custom_id in ("MaxHelpingHand/CustomTutorialWithNoBird", "MaxHelpingHand/MoreCustomNPC"):
        require(next(row for row in rows if row["customId"] == custom_id)["status"] == "AVAILABLE_ACCEPTED_REGISTRATION",
                "K-H exact lowerings retained")
    require(physical["status"] == "NOT_RUN_PREFLIGHT_BLOCKED" and physical["iosIpaSha256"] is None and
            physical["tvosIpaSha256"] is None and not physical["bingRoomsTraversed"], "no physical pass fabrication")
    require(all(value is False or value is None for value in result["claims"].values()), "no integration/release claim")
    require(result["product"]["sharedClosureSha256"] is None and not result["product"]["versionIncremented"],
            "no K-I product/version authority")
    require(result["aevpsv1Version"] == 1 and not result["runtimeSourceChanged"], "unchanged device runtime and schema")
    require(git("branch", "--show-current") == "feature/apple-everest-first-sj-slice-retry", "exact K-I branch")
    require(git("merge-base", START, "HEAD") == START, "descends from exact K-H")
    git("cat-file", "-e", KG + "^{commit}")
    require(subprocess.run(["git", "-C", str(ROOT), "merge-base", "--is-ancestor", KG, "HEAD"],
                           capture_output=True).returncode == 1, "K-G remains unmerged")
    for ref, expected in {
        "tvos-port": START, "origin/tvos-port": START,
        "ios-v0.1.1-rc.1^{}": "27e16b4724d94d3991b99c4795f680fcb0e5830c",
        "v1.0.0-rc.1^{}": "ee52b0868df091746f134d95d4f020f94f23d4fb",
        "v1.0.0-rc.2^{}": "641e86e4ed164cdf93f602ce2f11436449654d6e",
        "origin/release/v1.0.0-rc.3": "c8134c8ca7924cf12f48527e714b5242c6024927",
    }.items():
        require(git("rev-parse", ref) == expected, "protected ref: " + ref)
    require(subprocess.run(["git", "-C", str(ROOT), "show-ref", "--verify", "--quiet", "refs/tags/v1.0.0-rc.3"]).returncode == 1,
            "rc3 tag absent")
    require(not git("diff", START, "--", ".github/workflows", "apple-everest/runtime", "modern-ios/IOSPortVersion.props"),
            "no workflow, runtime or canonical version changes")
    historical_verifiers = git("ls-tree", "-r", "--name-only", START, "scripts").splitlines()
    for path in historical_verifiers:
        if Path(path).name.startswith("verify-"):
            require(not git("diff", START, "--", path), "immutable historical verifier: " + path)
    require(not git("diff", START, "--", "FNA", "fnalibs-ios-builder-celeste"), "recursive native locks unchanged")
    submodules = git("submodule", "status", "--recursive")
    require(not any(line.startswith(("+", "-", "U")) for line in submodules.splitlines()), "recursive submodule pins")
    nested_dirty = git("submodule", "foreach", "--quiet", "--recursive", "git status --porcelain")
    require(not nested_dirty, "recursive submodules clean")
    changed = set(git("diff", "--name-only", START).splitlines()) | set(git("ls-files", "--others", "--exclude-standard").splitlines())
    private = re.compile(r"/Users/|/private/|BEGIN (?:RSA |EC )?PRIVATE KEY|[A-F0-9]{8}-[A-F0-9]{16}")
    for relative in changed:
        path = ROOT / relative
        require(path.suffix.lower() in {".py", ".sh", ".json", ".md", ".cs"}, "unexpected added file type")
        # Do not scan this check's own pattern as an alleged private path.
        if path.is_file() and path.resolve() != Path(__file__).resolve():
            require(private.search(path.read_text()) is None, "private data in " + relative)
    report = ROOT / "docs/history/stages/APPLE_EVEREST_FIRST_SJ_SLICE_RETRY_STAGE25KI_REPORT.md"
    require(report.is_file(), "stage report present")
    require(set(range(1, 101)) <= {int(value) for value in re.findall(r"^(\d+)\. ", report.read_text(), re.M)},
            "all 100 closeout questions answered")
    if args.require_clean:
        require(not git("status", "--porcelain"), "clean final worktree")
    print("PASS: K-I diagnostic verification — YELLOW, 43 unavailable factories; no product or physical acceptance")


if __name__ == "__main__":
    main()
