#!/usr/bin/env python3
"""Verify the beginner-facing Stage 21 documentation and frozen product boundary."""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import subprocess
from typing import Any


START_COMMIT = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC1_COMMIT = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2_COMMIT = "641e86e4ed164cdf93f602ce2f11436449654d6e"
PRODUCT_SOURCE = "b5f2ec2fdd5c65c533d86aae750ee60dcaf5009e"
FMOD_URL = "https://www.fmod.com/download?version=1.10.09#fmodengine"
EXPECTED_ORIGIN = "https://github.com/hmcneill46/celeste-ios.git"
REPORT = "docs/history/stages/TVOS_BEGINNER_PROJECT_EXPERIENCE_STAGE21_REPORT.md"

CURRENT_DOCS = (
    "README.md",
    "CONTRIBUTING.md",
    "docs/README.md",
    "docs/BUILDING.md",
    "docs/CLOUD_BUILDING.md",
    "docs/CELESTE_INPUTS.md",
    "docs/STATUS.md",
    "docs/TROUBLESHOOTING.md",
    "docs/releases/v1.0.0-rc.2.md",
    "docs/history/README.md",
    REPORT,
)

ALLOWED_CHANGES = {
    "README.md",
    "docs/README.md",
    "docs/BUILDING.md",
    "docs/CLOUD_BUILDING.md",
    "docs/CELESTE_INPUTS.md",
    "docs/STATUS.md",
    "docs/TROUBLESHOOTING.md",
    "docs/history/README.md",
    REPORT,
    "scripts/verify-celeste-tvos-stage21.py",
}


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: bool, label: str) -> None:
        self.count += 1
        if not condition:
            raise SystemExit(f"error: Stage 21 verification failed: {label}")

    def equal(self, actual: Any, expected: Any, label: str) -> None:
        self.require(actual == expected, f"{label}: expected {expected!r}, got {actual!r}")


def git(repo: pathlib.Path, *args: str, check: bool = True) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run(
        ["git", "-C", str(repo), *args],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=check,
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=pathlib.Path,
                        default=pathlib.Path(__file__).resolve().parents[1])
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    repo = args.repo_root.resolve()
    checks = Checks()

    checks.equal(git(repo, "rev-parse", "v1.0.0-rc.1^{}").stdout.decode().strip(),
                 RC1_COMMIT, "immutable RC1 target")
    checks.equal(git(repo, "rev-parse", "v1.0.0-rc.2^{}").stdout.decode().strip(),
                 RC2_COMMIT, "immutable RC2 target")
    checks.require(git(repo, "merge-base", "--is-ancestor", START_COMMIT, "HEAD",
                       check=False).returncode == 0, "Stage 21 baseline is an ancestor")

    origin = git(repo, "remote", "get-url", "origin").stdout.decode().strip()
    checks.equal(origin.removesuffix("/"), EXPECTED_ORIGIN, "repository slug and origin")

    for relative in CURRENT_DOCS:
        checks.require((repo / relative).is_file(), f"current document exists: {relative}")

    readme = (repo / "README.md").read_text(encoding="utf-8")
    building = (repo / "docs/BUILDING.md").read_text(encoding="utf-8")
    cloud = (repo / "docs/CLOUD_BUILDING.md").read_text(encoding="utf-8")
    inputs = (repo / "docs/CELESTE_INPUTS.md").read_text(encoding="utf-8")
    status = (repo / "docs/STATUS.md").read_text(encoding="utf-8")
    trouble = (repo / "docs/TROUBLESHOOTING.md").read_text(encoding="utf-8")
    readme_flat = " ".join(readme.split())

    for heading in (
        "# Celeste for Apple TV", "## Start here", "## Choose how you want to build",
        "## Step 1 — Get your Celeste files", "## Step 2 — Get FMOD",
        "## Step 3 — Build", "## Step 4 — Sign and install", "## What works?",
        "## Save Manager", "## Troubleshooting", "## For developers",
    ):
        checks.require(heading in readme, f"README beginner section: {heading}")

    for target in (
        "docs/BUILDING.md", "docs/CLOUD_BUILDING.md", "docs/CELESTE_INPUTS.md",
        "docs/TROUBLESHOOTING.md", "docs/STATUS.md", "docs/history/README.md",
    ):
        checks.require(target in readme, f"README navigation: {target}")

    checks.require("Apple TV 4K (3rd generation, 128 GB)" in readme_flat,
                   "bounded physically tested hardware wording")
    checks.require("tvOS 16.0 or later" in readme_flat, "minimum tvOS is visible")
    checks.require("nine exact **Celeste 1.4.0.0 FNA** input profiles" in readme_flat,
                   "exact input scope is visible")
    checks.require("Everest/mod support and XNA inputs are not currently supported" in readme_flat,
                   "Everest and XNA are not advertised")
    checks.require("modern self-builder targets tvOS" in readme_flat
                   and "not a current modern-iOS build path" in readme_flat,
                   "modern iOS is not claimed")
    checks.require("private GitHub" in readme and "unsigned IPA" in readme,
                   "private cloud and unsigned boundary are visible")
    checks.require("Apple silicon Mac" in readme and "./build-tvos.sh" in readme,
                   "local Mac route is visible")
    checks.require("```mermaid" in readme and "Apple silicon Mac available?" in readme,
                   "accessible build-choice diagram")

    for document, text in (("README", readme), ("BUILDING", building),
                           ("CLOUD_BUILDING", cloud), ("CELESTE_INPUTS", inputs),
                           ("TROUBLESHOOTING", trouble)):
        checks.require(FMOD_URL in text, f"{document} uses exact FMOD link")
    checks.require("FMOD Engine" in readme and "not FMOD Studio" in readme
                   and "Choose the **iOS** package" in readme,
                   "FMOD product/package distinction")
    checks.require("## Which download should I use?" in inputs,
                   "input guide begins with a store choice")
    checks.require("download_depot 504230 504233 5880027853585448535" in inputs,
                   "recommended Steam acquisition retained")
    checks.require("legendary auth" in inputs and "App Name" in inputs,
                   "Epic acquisition and App Name discovery retained")
    checks.require("There are six steps" in cloud and "**Private**" in cloud,
                   "cloud workflow is a clear private six-step path")
    checks.require("## Before you start" in building
                   and "### What the builder finds and what you provide" in building,
                   "local prerequisite and automatic/provided distinction")
    checks.require("## Common questions" in trouble and "Where are my saves?" in trouble,
                   "beginner troubleshooting index")
    checks.require("| Capability | Current status |" in status,
                   "status has a stage-independent scan table")

    public_text = "\n".join((repo / relative).read_text(encoding="utf-8")
                            for relative in CURRENT_DOCS)
    checks.require(not re.search(r"/Users/[A-Za-z0-9._-]+/", public_text),
                   "current public docs contain no personal home path")
    checks.require("192.168." not in public_text, "current public docs contain no LAN address")

    markdown_link = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
    link_count = 0
    for relative in CURRENT_DOCS:
        source = repo / relative
        for raw_target in markdown_link.findall(source.read_text(encoding="utf-8")):
            target = raw_target.strip().split("#", 1)[0]
            if not target or re.match(r"(?:https?|mailto):", target):
                continue
            link_count += 1
            resolved = (source.parent / target).resolve()
            try:
                resolved.relative_to(repo)
            except ValueError:
                checks.require(False, f"link escapes repository: {relative} -> {target}")
            checks.require(resolved.exists(), f"relative link resolves: {relative} -> {target}")

    baseline_reports = git(
        repo, "ls-tree", "-r", "--name-only", START_COMMIT, "docs/history/stages"
    ).stdout.decode().splitlines()
    checks.equal(len(baseline_reports), 28, "historical report baseline count")
    for relative in baseline_reports:
        expected = git(repo, "show", f"{START_COMMIT}:{relative}").stdout
        checks.equal((repo / relative).read_bytes(), expected,
                     f"historical report remains byte-identical: {pathlib.Path(relative).name}")

    changed = set(git(repo, "diff", "--name-only", START_COMMIT, "HEAD").stdout.decode().splitlines())
    changed.update(git(repo, "diff", "--name-only").stdout.decode().splitlines())
    checks.require(changed <= ALLOWED_CHANGES,
                   f"documentation-only file boundary: unexpected {sorted(changed - ALLOWED_CHANGES)}")

    workflow = (repo / "cloud-builder-template/.github/workflows/build.yml").read_text(encoding="utf-8")
    common = (repo / "cloud-builder-template/scripts/cloud-common.sh").read_text(encoding="utf-8")
    checks.equal(workflow.count(f"CLOUD_PUBLIC_SOURCE_SHA: {PRODUCT_SOURCE}"), 1,
                 "production cloud workflow source pin")
    checks.equal(common.count(f'CLOUD_PUBLIC_SOURCE_SHA="{PRODUCT_SOURCE}"'), 1,
                 "production cloud helper source pin")

    internal_slug_mentions: list[str] = []
    for relative in CURRENT_DOCS:
        for number, line in enumerate((repo / relative).read_text(encoding="utf-8").splitlines(), 1):
            if "github.com/hmcneill46/celeste-ios" in line:
                internal_slug_mentions.append(f"{relative}:{number}")
    checks.require(all(item.startswith("docs/BUILDING.md:") for item in internal_slug_mentions),
                   f"hardcoded repository URL limited to copyable clone commands: {internal_slug_mentions}")
    checks.equal(len(internal_slug_mentions), 2, "two technically required absolute clone URLs")

    result = {
        "schemaVersion": 1,
        "stage": 21,
        "status": "PASS",
        "tests": checks.count,
        "documentationLinks": link_count,
        "historicalReportsVerified": len(baseline_reports),
        "productSourceCommit": PRODUCT_SOURCE,
    }
    if args.output:
        output = args.output.resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"PASS: {checks.count} Stage 21 documentation tests; {link_count} relative links")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
