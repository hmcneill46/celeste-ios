#!/usr/bin/env python3
"""Verify the tracked Stage 18C private cloud-builder template and docs."""

from __future__ import annotations

import pathlib
import re
import subprocess


SOURCE_SHA = "90ebb023f3043222bc67e72922ae4d68223f009c"
NATIVE_SHA = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
CHECKOUT_SHA = "3d3c42e5aac5ba805825da76410c181273ba90b1"
CACHE_SHA = "55cc8345863c7cc4c66a329aec7e433d2d1c52a9"


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def require(text: str, values: tuple[str, ...], label: str) -> None:
    for value in values:
        if value not in text:
            fail(f"{label} is missing: {value}")


def main() -> int:
    repo = pathlib.Path(__file__).resolve().parents[1]
    template = repo / "cloud-builder-template"
    expected = {
        "README.md",
        ".github/workflows/build.yml",
        ".github/workflows/cleanup.yml",
        "scripts/cloud-common.sh",
        "scripts/prepare-inputs.py",
    }
    actual = {
        path.relative_to(template).as_posix()
        for path in template.rglob("*")
        if path.is_file() and "__pycache__" not in path.parts
    }
    if actual != expected:
        fail(f"canonical template file inventory changed: {sorted(actual ^ expected)}")

    build = (template / ".github/workflows/build.yml").read_text()
    cleanup = (template / ".github/workflows/cleanup.yml").read_text()
    common = (template / "scripts/cloud-common.sh").read_text()
    prepare = (template / "scripts/prepare-inputs.py").read_text()
    readme = (template / "README.md").read_text()

    require(build, (
        "name: Build Celeste for Apple TV",
        "on:\n  workflow_dispatch:",
        "runs-on: macos-26",
        "timeout-minutes: 120",
        "contents: write",
        "cancel-in-progress: false",
        SOURCE_SHA,
        NATIVE_SHA,
        "--non-interactive",
        "--mode ipa",
        "--no-color",
        '--bundle-id "$CLOUD_BUNDLE_ID"',
        '$CELESTE_SOURCE_ROOT/.build/celeste-runtime/stage18c-cloud/celeste-input.json',
        '$CELESTE_SOURCE_ROOT/.build/fmod-tvos/stage18c-cloud/sdk-manifest.json',
        "celeste-tvos-inputs",
        "celeste-tvos-output",
        "Celeste-tvOS-unsigned.ipa",
        "minimum-gib 25",
        "UseInterpreter",
        "verify-celeste-tvos-stage14.sh",
        "verify-celeste-tvos-stage15.sh",
        "verify-celeste-tvos-stage16b.sh",
    ), "Build workflow")
    if build.index("Checking private repository") > build.index("actions/checkout@"):
        fail("privacy gate is not before the first external Action/input operation")
    if any(trigger in build for trigger in ("pull_request:", "schedule:", "push:", "release:")):
        fail("Build workflow has an automatic trigger")
    if '"${{ inputs.bundle_id }}"' in build:
        fail("user-controlled bundle ID is interpolated into shell source")
    if "upload-artifact" in build or "download-artifact" in build + cleanup:
        fail("cloud builder must not use Actions artifacts")
    if re.search(r"restore-keys:", build):
        fail("safe cache must not use a broad restore prefix")
    if build.count("${{ runner.temp }}/celeste-tvos-cloud-source/artifacts/tvos-native/self-build") != 2 or \
            build.count("${{ runner.temp }}/celeste-tvos-cloud-source/.build/tvos-host") != 2:
        fail("safe cache path allow-list changed")
    for forbidden in (
        "Steam password", "Apple password", "provisioning profile", "id-token: write",
        "packages: write", "pull-requests: write",
    ):
        if forbidden in build:
            fail(f"Build workflow contains a forbidden cache/permission/credential surface: {forbidden}")

    uses = re.findall(r"uses:\s*([^\s#]+)", build + "\n" + cleanup)
    allowed_actions = {
        f"actions/checkout@{CHECKOUT_SHA}",
        f"actions/cache/restore@{CACHE_SHA}",
        f"actions/cache/save@{CACHE_SHA}",
    }
    if not uses or set(uses) != allowed_actions:
        fail(f"external Action allow-list changed: {uses}")
    if any(not re.fullmatch(r"actions/[a-z-]+(?:/[a-z-]+)?@[0-9a-f]{40}", value) for value in uses):
        fail("an external Action is not pinned to a full immutable SHA")

    require(cleanup, (
        "name: Clean private build files",
        "on:\n  workflow_dispatch:",
        "contents: write",
        "actions: write",
        "also_delete_build_cache",
        "cloud_delete_release_and_tag",
        "cloud_delete_safe_caches",
    ), "cleanup workflow")
    if any(trigger in cleanup for trigger in ("pull_request:", "schedule:", "push:", "release:")):
        fail("cleanup workflow has an automatic trigger")

    require(common, (
        'CLOUD_INPUT_TAG="celeste-tvos-inputs"',
        'CLOUD_OUTPUT_TAG="celeste-tvos-output"',
        'CLOUD_CACHE_PREFIX="celeste-tvos-safe-native-v1-"',
        SOURCE_SHA,
        NATIVE_SHA,
        "cloud_validate_cleanup_tag",
        "cloud_require_private_repository",
        "cloud_runner_cleanup",
        "cloud_write_failure_summary",
        "cloud_write_success_summary",
    ), "cloud helper")
    if "eval " in common or "rm -rf -- \"$HOME\"" in common:
        fail("cloud helper contains an unsafe dynamic/destructive operation")

    require(prepare, (
        "RELEASE_ASSET_LIMIT = 2 * 1024**3",
        "MAX_ZIP_TOTAL_BYTES",
        "ZIP symbolic links are forbidden",
        "ZIP special files are forbidden",
        "repository_is_private",
        "classify_release_assets",
        "safe_extract_zip",
        "find_fmod_root",
        "verify_source",
    ), "input helper")

    require(readme, (
        "must be Private",
        "Never upload copyrighted game files to this public template",
        "## Quick start",
        "celeste-tvos-inputs",
        "celeste-tvos-output",
        "Celeste-tvOS-unsigned.ipa",
        "FMOD Engine iOS/tvOS 1.10.09 build 97915",
        "30–35 minutes",
        "about 15 minutes",
        "every 60 seconds",
        "unsigned",
        "does not mean the files stay on your own computer",
    ), "template README")

    main_readme = (repo / "README.md").read_text()
    cloud_guide = repo / "docs/CLOUD_BUILDING.md"
    docs_index = (repo / "docs/README.md").read_text()
    status = (repo / "docs/STATUS.md").read_text()
    troubleshooting = (repo / "docs/TROUBLESHOOTING.md").read_text()
    for path in (cloud_guide, repo / "docs/history/stages/TVOS_GITHUB_ACTIONS_CLOUD_BUILDER_STAGE18C_REPORT.md"):
        if not path.is_file():
            fail(f"Stage 18C document is missing: {path.relative_to(repo)}")
    require(main_readme, ("Build locally on a Mac", "Build in the cloud", "docs/CLOUD_BUILDING.md"), "root README")
    require(cloud_guide.read_text(), ("Visibility", "Private", "celeste-tvos-inputs", "Clean private build files"), "cloud guide")
    require(docs_index, ("Cloud building", "CLOUD_BUILDING.md"), "documentation index")
    require(status, ("private GitHub Actions", "unsigned IPA"), "status")
    require(troubleshooting, ("Cloud builder", "Output Release already exists", "GitHub Actions minutes"), "troubleshooting")

    history = (repo / "docs/history/README.md").read_text()
    if "TVOS_GITHUB_ACTIONS_CLOUD_BUILDER_STAGE18C_REPORT.md" not in history:
        fail("Stage 18C report is not indexed")

    subprocess.run([str(repo / "scripts/test-cloud-builder-template.py")], check=True)
    print("PASS: Stage 18C template security, orchestration, documentation, and deterministic policy")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
