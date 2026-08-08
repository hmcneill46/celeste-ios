#!/usr/bin/env python3
"""Verify public Stage 8B documentation, commands, links, and Git isolation."""

from __future__ import annotations

import os
import pathlib
import re
import stat
import subprocess
import sys


REQUIRED_DOCS = (
    "README.md",
    "docs/BUILDING.md",
    "docs/TROUBLESHOOTING.md",
    "docs/STATUS.md",
    "CONTRIBUTING.md",
    "TVOS_PUBLIC_PREREQUISITES_STAGE8C_REPORT.md",
    "TVOS_LOCALE_REPRODUCIBILITY_STAGE8D_REPORT.md",
)
PUBLIC_TEXT = REQUIRED_DOCS + (
    ".github/ISSUE_TEMPLATE/bug_report.yml",
    ".github/ISSUE_TEMPLATE/build_problem.yml",
    ".github/pull_request_template.md",
)
FORBIDDEN_TRACKED_SUFFIXES = (
    ".ipa",
    ".mobileprovision",
    ".p12",
    ".cer",
    ".bank",
)


def run(*args: str, check: bool = True) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        args,
        check=check,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def main() -> None:
    repo = pathlib.Path(__file__).resolve().parent.parent
    if not (repo / ".git").exists():
        fail("repository root is not a Git checkout")

    for relative in REQUIRED_DOCS:
        if not (repo / relative).is_file():
            fail(f"required public document is missing: {relative}")

    readme = (repo / "README.md").read_text(encoding="utf-8")
    required_readme = (
        "# Celeste for Apple TV",
        "./build-tvos.sh",
        "itch.io Linux",
        "Steam builds have not currently been tested",
        "1.10.09, build 97915",
        "Personal Team",
        "seven days",
        "signing-ready unsigned IPA",
        "shared between Apple TV users",
        "DualSense",
        "Siri Remote",
        "./build-tvos.sh --check-host",
        "dist/logs/last-error.txt",
        "CMake, Ninja, and ripgrep are **not** required",
    )
    for value in required_readme:
        if value not in readme:
            fail(f"README omits required public fact: {value}")

    public = "\n".join((repo / item).read_text(encoding="utf-8") for item in PUBLIC_TEXT)
    forbidden_text = {
        "developer home path": r"/Users/[A-Za-z0-9._-]+/",
        "User Management entitlement": r"com\.apple\.developer\.user-management",
        "iCloud entitlement": r"com\.apple\.developer\.icloud",
        "App Group entitlement": r"com\.apple\.security\.application-groups",
    }
    # Public documentation may name a forbidden entitlement only as an explicit
    # absence. Reject executable entitlement snippets, while allowing prose.
    public_without_status_prose = "\n".join(
        line for line in public.splitlines()
        if not any(word in line for word in ("User Management", "iCloud", "App Groups"))
    )
    if re.search(forbidden_text["developer home path"], public):
        fail("public documentation contains an absolute user home path")
    for label in ("User Management entitlement", "iCloud entitlement", "App Group entitlement"):
        if re.search(forbidden_text[label], public_without_status_prose):
            fail(f"public material contains a forbidden capability declaration: {label}")

    # Resolve local Markdown links and code links without a network dependency.
    markdown_link = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
    for relative in REQUIRED_DOCS:
        source = repo / relative
        for target in markdown_link.findall(source.read_text(encoding="utf-8")):
            target = target.strip().split("#", 1)[0]
            if not target or re.match(r"https?://", target):
                continue
            if target.startswith("mailto:"):
                continue
            resolved = (source.parent / target).resolve()
            try:
                resolved.relative_to(repo.resolve())
            except ValueError:
                fail(f"link escapes repository: {relative} -> {target}")
            if not resolved.exists():
                fail(f"dead relative link: {relative} -> {target}")

    builder = repo / "build-tvos.sh"
    if not builder.stat().st_mode & stat.S_IXUSR:
        fail("build-tvos.sh is not executable")
    help_result = run(str(builder), "--help")
    for option in (
        "--non-interactive",
        "--mode MODE",
        "--game-root DIR",
        "--fmod-root DIR",
        "--bundle-id ID",
        "--check-host",
        "--clean",
        "--reset-config",
    ):
        if option not in help_result.stdout:
            fail(f"builder help omits documented option: {option}")

    builder_text = builder.read_text(encoding="utf-8")
    for avoidable in (" rg", " cmake", " ninja"):
        if re.search(rf"(^|[ (]){avoidable.strip()}([ )]|$)", builder_text, re.MULTILINE):
            fail(f"builder retained avoidable public prerequisite: {avoidable.strip()}")
    for required_error_fact in (
        "Some required tools are missing:",
        "dist/logs/last-error.txt",
        "Full command log:",
    ):
        if required_error_fact not in builder_text:
            fail(f"builder omits Stage 8C failure UX: {required_error_fact}")

    fmod_prepare = (repo / "scripts/prepare-fmod-tvos.sh").read_text(encoding="utf-8")
    comm_lines = [line.strip() for line in fmod_prepare.splitlines() if re.search(r"\bcomm\b", line)]
    if len(comm_lines) != 4 or any("LC_ALL=C comm" not in line for line in comm_lines):
        fail("FMOD sorted-set comparisons are not all pinned to C collation")

    tracked = run("git", "-C", str(repo), "ls-files", "-z").stdout.split("\0")
    tracked = [item for item in tracked if item]
    for relative in tracked:
        lower = relative.lower()
        if lower.endswith(FORBIDDEN_TRACKED_SUFFIXES):
            fail(f"proprietary/signing package is tracked: {relative}")
        if ".app/" in lower or lower.endswith(".app"):
            fail(f"application bundle is tracked: {relative}")
        if pathlib.PurePosixPath(relative).name in ("Celeste.png", "SplashScreen.png"):
            fail(f"user-owned artwork is tracked: {relative}")
        if relative == "tvos/Local.Build.props":
            fail("private signing configuration is tracked")

    status = run("git", "-C", str(repo), "status", "--porcelain", "-z").stdout.split("\0")
    for record in status:
        if not record:
            continue
        relative = record[3:] if len(record) > 3 else ""
        lower = relative.lower()
        if lower.endswith(FORBIDDEN_TRACKED_SUFFIXES + (".png",)) or ".app/" in lower:
            fail(f"generated/proprietary Git candidate is visible: {relative}")

    for ignored_root in (
        ".build/tvos-self-build/",
        "artifacts/tvos-self-build/",
        "dist/",
    ):
        if ignored_root not in (repo / ".gitignore").read_text(encoding="utf-8"):
            fail(f"generated root is not ignored: {ignored_root}")

    if (repo / ".DS_Store").exists():
        fail("repository root contains .DS_Store")

    print("PASS: Stage 8B public docs, links, builder help, permissions, and Git isolation")


if __name__ == "__main__":
    main()
