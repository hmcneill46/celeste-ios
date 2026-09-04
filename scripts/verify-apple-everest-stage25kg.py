#!/usr/bin/env python3
"""Fail-closed verification for the Stage 25K-G YELLOW integration audit."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import subprocess
import zipfile


START = "e38a886f54a719ad973f21f4ef84587d52b49718"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
ROOT_ZIP = "4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655"
MAPS = {
    "Maps/StrawberryJam2021/0-Lobbies/1-Beginner.bin":
        "a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2",
    "Maps/StrawberryJam2021/1-Beginner/Bing_Over_Google.bin":
        "e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347",
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
def file_hash(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def git(root, *args):
    return subprocess.run(["git", "-C", str(root), *args], check=True,
                          capture_output=True, text=True).stdout.strip()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--stage-root", type=pathlib.Path)
    parser.add_argument("--require-clean", action="store_true")
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    ae = root / "apple-everest"
    paths = {
        "slice": ae / "sj-beginner-slice-stage25kg.json",
        "content": ae / "sj-beginner-content-stage25kg.json",
        "physical": ae / "sj-beginner-physical-stage25kg.json",
        "report": root / "docs/history/stages/APPLE_EVEREST_FIRST_SJ_SLICE_STAGE25KG_REPORT.md",
    }
    required = [*paths.values(), root / "scripts/verify-apple-everest-stage25kg.sh",
                root / "scripts/generate-apple-everest-stage25kg-content.py"]
    c = Checks()
    for path in required: c.require(path.is_file(), "required file " + str(path.relative_to(root)))

    result, content, physical = (load(paths[name]) for name in ("slice", "content", "physical"))
    c.require(result["schemaVersion"] == physical["schemaVersion"] == content["schemaVersion"] == 1,
              "artifact schemas")
    c.require(artifact_hash(result) and artifact_hash(physical), "self-authenticating result artifacts")
    c.require(result["status"] == "YELLOW_NEW_COMPATIBILITY_MECHANISM_REQUIRED" and
              physical["status"] == "NOT_RUN_NEW_COMPATIBILITY_MECHANISM_REQUIRED",
              "honest YELLOW and physical-not-run statuses")
    readiness = result["kFReadiness"]
    c.require((readiness["auditedLobbyAndBing"], readiness["acceptedOrVanilla"],
               readiness["blocked"], readiness["unclassified"]) == (920, 920, 0, 0) and
              readiness["verifierResult"] == "PASS" and not readiness["remainedValidForIntegration"],
              "K-F result retained and K-G insufficiency recorded")

    maps = result["maps"]
    c.require(maps["lobby"]["sourceSha256"] == MAPS[maps["lobby"]["sourcePath"]] and
              maps["lobby"]["binaryPackerRootBytes"] == 524807 and
              maps["lobby"]["appendixBytes"] == 149841 and
              maps["lobby"]["appendixSha256"] ==
              "d76dba1bc5834d999f4d622afd008a702ffe6663b17fe7b0be3c5cc69afabe0d",
              "exact unchanged lobby identity")
    c.require(maps["bing"]["sourceSha256"] == MAPS[maps["bing"]["sourcePath"]] and
              maps["bing"]["binaryPackerRootBytes"] == 70024 and
              maps["bing"]["appendixBytes"] == 65339 and
              maps["bing"]["appendixSha256"] ==
              "963d5f086279311c18561df8fb135e445ca1cf0bcc2d3f1ba0d8b21f8376da6d",
              "exact unchanged Bing identity")

    packages = content["packages"]
    included = [(package["name"], row["path"], row["sha256"])
                for package in packages for row in package["includedFiles"]]
    map_rows = [(owner, path, digest) for owner, path, digest in included if path in MAPS]
    c.require(len(packages) == 14 and len(included) == 1212 and
              sum(len(package["includedFiles"]) for package in packages
                  if package["name"] == "StrawberryJam2021") == 1178,
              "bounded 14-package 1,212-file selection")
    c.require(map_rows == [("StrawberryJam2021", path, digest) for path, digest in MAPS.items()],
              "only the exact two map BINs selected")
    selection = result["content"]
    c.require(selection["selectionArtifactSha256"] == file_hash(paths["content"]) and
              selection["selectedMapBinCount"] == 2 and selection["excludedMapBinCount"] == 126 and
              selection["unrelatedStrawberryJamMapBins"] == 0 and
              not selection["preliminaryClosureAccepted"] and not selection["threeRunDeterminismCompleted"],
              "content identity and rejected preliminary closure boundary")

    blocker = result["blocker"]
    occurrences = {row["id"]: row for row in blocker["authoredOccurrences"]}
    c.require(blocker["classification"] == "NEW_COMPATIBILITY_MECHANISM" and
              not blocker["allowedKGBugFix"] and set(occurrences) == {
                  "MaxHelpingHand/CustomTutorialWithNoBird", "MaxHelpingHand/MoreCustomNPC"},
              "exact new-mechanism blocker group")
    c.require(any("CustomBirdTutorial" in row for row in blocker["compileErrors"]) and
              any("CustomNPC" in row for row in blocker["compileErrors"]) and
              occurrences["MaxHelpingHand/CustomTutorialWithNoBird"]["x"] == 1128 and
              occurrences["MaxHelpingHand/MoreCustomNPC"]["x"] == 3168,
              "exact compiler and authored occurrence evidence")
    c.require(result["product"] == {"iosBuild": "FAILED_BEFORE_AOT",
              "tvosBuild": "NOT_RUN_MANDATORY_STOP", "ipa": None, "app": None,
              "physicalAcceptance": "NOT_RUN_NO_ACCEPTED_PRODUCT"},
              "no product or physical claim")
    c.require(not result["claims"]["developmentIntegrationReady"] and
              not result["claims"]["allPlatformReleaseReady"] and
              not result["claims"]["wholeStrawberryJamSupported"] and
              not result["claims"]["firstPhysicallyRunningUnchangedStrawberryJamMapOnApple"],
              "no unsupported success claim")

    if args.stage_root:
        archive = args.stage_root.resolve() / "packages/StrawberryJam2021.zip"
        c.require(archive.is_file() and file_hash(archive) == ROOT_ZIP, "exact root archive fixture")
        with zipfile.ZipFile(archive) as source:
            bins = [name for name in source.namelist() if name.startswith("Maps/") and name.endswith(".bin")]
            c.require(len(bins) == 128 and all(hashlib.sha256(source.read(name)).hexdigest() == digest
                                               for name, digest in MAPS.items()),
                      "128-map archive and selected source bytes")

    report = paths["report"].read_text()
    c.require(set(map(str, range(1, 101))).issubset(
              set(re.findall(r"^\s*(\d+)\.\s", report, re.MULTILINE))),
              "all 100 closeout questions answered")
    privacy = re.compile(r"/Users/|/private/|mobileprovision|BEGIN (?:RSA |EC )?PRIVATE KEY", re.I)
    c.require(not any(privacy.search(path.read_text(errors="replace")) for path in required), "privacy scan")
    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".zip", ".dll", ".bank", ".ipa", ".bin", ".guids.txt"))
                      for path in tracked if "stage25kg" in path.lower()), "no tracked binary inputs or products")
    c.require(git(root, "diff", "--name-only", START, "--", ".github/workflows") == "", "zero Actions changes")
    c.require(subprocess.run(["git", "-C", str(root), "merge-base", "--is-ancestor", START, "HEAD"]).returncode == 0,
              "feature descends from K-F tip")
    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC and
              git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1 and
              git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2 and
              git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3,
              "protected refs unchanged")
    c.require(subprocess.run(["git", "-C", str(root), "rev-parse", "v1.0.0-rc.3^{}"],
                             capture_output=True).returncode != 0, "rc3 tag remains absent")
    if args.require_clean: c.require(git(root, "status", "--short") == "", "clean worktree")
    print(f"PASS: Stage 25K-G YELLOW audit verifier ({c.count} checks)")


if __name__ == "__main__": main()
