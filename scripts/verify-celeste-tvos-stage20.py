#!/usr/bin/env python3
"""Verify the current v1.0.0-rc.2 candidate contract."""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import subprocess
import sys
from typing import Any


PRODUCT_SOURCE = "b5f2ec2fdd5c65c533d86aae750ee60dcaf5009e"
RC1_COMMIT = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2_TAG = "v1.0.0-rc.2"
NATIVE_HASH = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
CANONICAL_LOCKS = {
    "content": (1216, "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"),
    "rawSource": (920, "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"),
    "patchedSource": (922, "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"),
    "stage6RealAudio": (934, "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"),
}
PROFILES = {
    "itch-linux-fna-1.4.0.0",
    "itch-macos-fna-1.4.0.0",
    "itch-windows-fna-1.4.0.0",
    "epic-windows-fna-1.4.0.0",
    "epic-macos-fna-1.4.0.0",
    "steam-linux-fna-1.4.0.0-manifest-1505052356460012099",
    "steam-linux-public-2025-fna-1.4.0.0",
    "steam-macos-fna-1.4.0.0",
    "steam-windows-fna-1.4.0.0-manifest-1981411158533599226",
}
ACTION_PINS = {
    "actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1",
    "actions/cache/restore@55cc8345863c7cc4c66a329aec7e433d2d1c52a9",
    "actions/cache/save@55cc8345863c7cc4c66a329aec7e433d2d1c52a9",
}


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: bool, label: str) -> None:
        self.count += 1
        if not condition:
            raise SystemExit(f"error: Stage 20 verification failed: {label}")

    def equal(self, actual: Any, expected: Any, label: str) -> None:
        self.require(actual == expected, f"{label}: expected {expected!r}, got {actual!r}")


def git(repo: pathlib.Path, *args: str, check: bool = True) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run(
        ["git", "-C", str(repo), *args],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=check,
    )


def baseline_file(repo: pathlib.Path, relative: str) -> bytes:
    return git(repo, "show", f"{PRODUCT_SOURCE}:{relative}").stdout


def read_json(path: pathlib.Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[1])
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    repo = args.repo_root.resolve()
    checks = Checks()

    checks.equal(git(repo, "rev-parse", "v1.0.0-rc.1^{}").stdout.decode().strip(), RC1_COMMIT,
                 "immutable RC1 dereference")
    checks.require(git(repo, "cat-file", "-e", f"{PRODUCT_SOURCE}^{{commit}}", check=False).returncode == 0,
                   "product source commit exists")
    checks.require(git(repo, "merge-base", "--is-ancestor", PRODUCT_SOURCE, "HEAD", check=False).returncode == 0,
                   "product source is an ancestor of the candidate")
    checks.require(git(repo, "show-ref", "--verify", "--quiet", f"refs/tags/{RC2_TAG}", check=False).returncode != 0,
                   "v1.0.0-rc.2 tag remains absent")

    manifest_path = repo / "tvos/release-candidates/v1.0.0-rc.2.json"
    notes_path = repo / "docs/releases/v1.0.0-rc.2.md"
    checks.require(manifest_path.is_file(), "RC2 manifest exists")
    checks.require(notes_path.is_file(), "RC2 release notes exist")
    manifest = read_json(manifest_path)
    checks.equal(manifest["schemaVersion"], 1, "manifest schema")
    checks.equal(manifest["stage"], 20, "manifest stage")
    checks.equal(manifest["intendedTag"], RC2_TAG, "intended tag")
    checks.equal(manifest["releaseStatus"], "candidate-not-tagged", "candidate status")
    checks.equal(manifest["rc1"], {"tag": "v1.0.0-rc.1", "commit": RC1_COMMIT}, "RC1 manifest identity")
    checks.equal(manifest["productSourceCommit"], PRODUCT_SOURCE, "product source pin")
    checks.require("finalCandidateCommit" not in json.dumps(manifest), "manifest avoids self-referential final commit")

    build = manifest["build"]
    for key, expected in {
        "configuration": "Release", "runtimeIdentifier": "tvos-arm64",
        "minimumTvOS": "16.0", "renderer": "Metal", "trimMode": "full",
        "fullAot": True, "useInterpreter": False,
    }.items():
        checks.equal(build[key], expected, f"build manifest {key}")
    checks.equal(manifest["native"]["logicalSha256"], NATIVE_HASH, "native logical hash")
    checks.equal(set(manifest["native"]["components"]), {"SDL2", "FNA3D", "FAudio", "Theorafile", "tvStubs", "MoltenVK"},
                 "native component set")

    game = manifest["celesteInput"]
    checks.equal(game["gameVersion"], "1.4.0.0", "Celeste version")
    checks.equal(game["runtimeFamily"], "FNA", "runtime family")
    checks.equal(game["canonicalClass"], "celeste-1.4.0.0-a", "canonical class")
    checks.equal(set(game["supportedProfiles"]), PROFILES, "nine manifest profiles")
    checks.equal(len(game["supportedProfiles"]), 9, "profile count")
    for name, (files, sha256) in CANONICAL_LOCKS.items():
        checks.equal(game["canonicalLocks"][name], {"files": files, "sha256": sha256}, f"canonical lock {name}")

    checks.equal(manifest["audio"], {"engine": "FMOD 1.10.09 build 97915", "bankCount": 7}, "FMOD manifest")
    persistence = manifest["persistence"]
    checks.equal(persistence["currentWriteFormat"], 2, "persistence write format")
    checks.equal(persistence["readableFormats"], [0, 1, 2], "persistence readable formats")
    checks.require(persistence["dualGeneration"] is True, "A/B generations enabled")
    checks.equal(persistence["compression"], "deterministic-zlib-level-9", "persistence compression")
    checks.equal(persistence["logicalFiles"], ["settings", "0", "1", "2"], "persistence file allowlist")
    checks.equal(manifest["features"]["controllerPromptModes"],
                 ["Automatic", "Xbox", "PlayStation", "Nintendo Switch", "Stadia"], "prompt modes")
    for key in ("writableSaveManager", "oneTimeQrPairing", "confirmDrivenSoftReload", "gracefulMainMenuQuit", "performanceHud"):
        checks.require(manifest["features"][key] is True, f"feature enabled: {key}")
    checks.require(manifest["builders"]["localMac"] is True, "local builder declared")
    checks.require(manifest["builders"]["privateGitHubActions"] is True, "private cloud builder declared")
    checks.require(manifest["builders"]["cloudOutputUnsigned"] is True, "cloud output unsigned")
    checks.require(manifest["builders"]["cloudSigning"] is False, "cloud signing disabled")

    profiles = read_json(repo / "managed/celeste-input-profiles.json")["profiles"]
    checks.equal(len(profiles), 9, "profile registry count")
    checks.equal({item["id"] for item in profiles}, PROFILES, "profile registry identities")
    checks.require(all(item["canonicalClass"] == "celeste-1.4.0.0-a" for item in profiles),
                   "all profiles share canonical class")
    checks.require(all(item["runtimeFamily"] == "FNA" for item in profiles), "all profiles are FNA")

    project = (repo / "tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj").read_text(encoding="utf-8")
    builder = (repo / "build-tvos.sh").read_text(encoding="utf-8")
    checks.require("<TrimMode Condition=\"'$(Configuration)' == 'Release'\">full</TrimMode>" in project,
                   "full trim project default")
    checks.require("<UseInterpreter>false</UseInterpreter>" in project, "interpreter disabled in project")
    checks.require("-p:UseInterpreter=false -p:RunAOTCompilation=true" in builder, "builder full AOT arguments")
    checks.require("-p:PublishTrimmed=true -p:TrimMode=full -p:MtouchLink=Full" in builder,
                   "builder full trimming arguments")
    checks.require(NATIVE_HASH in builder, "builder native lock")

    runtime = repo / "tvos/CelesteTvOSRuntimeHost"
    persistence_source = (runtime / "PersistenceStore.cs").read_text(encoding="utf-8")
    controller_source = (runtime / "ControllerPromptPolicy.cs").read_text(encoding="utf-8")
    hud_source = (runtime / "PerformanceHudPolicy.cs").read_text(encoding="utf-8")
    protocol_source = (runtime / "SaveManagerHttpProtocol.cs").read_text(encoding="utf-8")
    service_source = (runtime / "SaveManagerService.cs").read_text(encoding="utf-8")
    for token in (
        '"production" => "CelesteTvOS.Persistence.v1"',
        '"acceptance" => "CelesteTvOS.Persistence.Acceptance.v1"',
        '"restart" => "CelesteTvOS.Persistence.Restart.v1"',
        '"tests" => "CelesteTvOS.Persistence.Tests.v1"',
        "internal const ushort FormatVersion = 2", "PreviousFormatVersion = 1", "LegacyFormatVersion = 0",
        "CompressionZlibLevel9 = 1",
    ):
        checks.require(token in persistence_source, f"persistence contract: {token}")
    checks.require('PreferenceKey = "CelesteTvOS.ControllerPrompts.v1"' in controller_source,
                   "controller prompt preference key")
    checks.require('PreferenceKey = "CelesteTvOS.PerformanceHUD.v1"' in hud_source,
                   "Performance HUD preference key")
    checks.require('BonjourServiceType = "_celeste-save._tcp"' in service_source, "Bonjour service contract")
    for token in (
        "MaximumPairingBodyBytes = 80", "PairingLifetime = TimeSpan.FromMinutes(3)",
        "pairingCredential = RandomNumberGenerator.GetBytes(32)", '"/pair"', '"/auth"',
        '"/download/all"', '"/replace/settings"', '"/delete/2"', '"/reset/settings"',
        'SessionCookieName = "CelesteSaveSession"', 'CsrfHeaderName = "x-celeste-csrf"',
        'RevisionHeaderName = "x-celeste-revision"', "MaximumConcurrentConnections = 4",
    ):
        checks.require(token in protocol_source, f"Save Manager contract: {token}")

    active_names = "\n".join(
        path.read_text(encoding="utf-8", errors="replace")
        for path in runtime.iterdir() if path.suffix in {".cs", ".csproj", ".targets", ".xml"}
    )
    checks.require(not re.search(r"\b(?:class|struct|enum|record|interface)\s+(?:I)?(?:TvOS)?Stage\d+[A-Za-z0-9_]*", active_names),
                   "active runtime uses semantic names")
    for name in ("PersistenceStore", "SaveManagerService", "SoftReloadCoordinator", "PerformanceHudCoordinator"):
        checks.require(name in active_names, f"semantic runtime name present: {name}")

    stage14_path = "tvos/stage14-release-candidate.json"
    checks.require((repo / stage14_path).read_bytes() == baseline_file(repo, stage14_path),
                   "Stage 14 manifest remains byte-identical")
    historical = git(repo, "ls-tree", "-r", "--name-only", PRODUCT_SOURCE, "docs/history/stages").stdout.decode().splitlines()
    checks.require(len(historical) >= 27, "historical report baseline enumerated")
    for relative in historical:
        checks.require((repo / relative).read_bytes() == baseline_file(repo, relative),
                       f"historical report unchanged: {pathlib.Path(relative).name}")

    cloud = repo / "cloud-builder-template"
    build_workflow = (cloud / ".github/workflows/build.yml").read_text(encoding="utf-8")
    cleanup_workflow = (cloud / ".github/workflows/cleanup.yml").read_text(encoding="utf-8")
    common = (cloud / "scripts/cloud-common.sh").read_text(encoding="utf-8")
    cloud_readme = (cloud / "README.md").read_text(encoding="utf-8")
    cloud_all = "\n".join((build_workflow, cleanup_workflow, common, cloud_readme))
    checks.require(build_workflow.count(f"CLOUD_PUBLIC_SOURCE_SHA: {PRODUCT_SOURCE}") == 1,
                   "workflow pins exact product source")
    checks.require(common.count(f'CLOUD_PUBLIC_SOURCE_SHA="{PRODUCT_SOURCE}"') == 1,
                   "orchestration pins exact product source")
    checks.require("90ebb023f3043222bc67e72922ae4d68223f009c" not in cloud_all,
                   "current template no longer uses superseded source pin")
    checks.require("workflow_dispatch:" in build_workflow and not any(x in build_workflow for x in ("pull_request:", "schedule:", "push:")),
                   "cloud build remains manual-only")
    checks.require(build_workflow.index("Checking private repository") < build_workflow.index("actions/checkout@"),
                   "privacy gate precedes checkout")
    checks.require(build_workflow.index("Checking private repository") < build_workflow.index("Inspect private input Release"),
                   "privacy gate precedes private input access")
    checks.require("runs-on: macos-26" in build_workflow, "standard ARM64 macOS runner")
    checks.require("permissions:\n  contents: write" in build_workflow, "bounded build workflow permission")
    checks.require("celeste-tvos-inputs" in common and "celeste-tvos-output" in common,
                   "fixed Release input/output tags")
    checks.require("upload-artifact" not in cloud_all and "download-artifact" not in cloud_all,
                   "no Actions artifact transport")
    checks.require("artifacts/tvos-native/self-build" in build_workflow and ".build/tvos-host" in build_workflow,
                   "exact safe cache paths")
    checks.require("Content/FMOD" not in build_workflow.split("path: |", 1)[1].split("key:", 1)[0],
                   "proprietary content absent from cache paths")
    checks.equal(set(re.findall(r"(?:actions/[A-Za-z0-9_./-]+@[0-9a-f]{40})", cloud_all)), ACTION_PINS,
                 "external Action pins")
    checks.require('case "$1" in' in common and '"$CLOUD_INPUT_TAG"|"$CLOUD_OUTPUT_TAG"' in common,
                   "cleanup Release allowlist")
    checks.require("cloud_delete_safe_caches" in common and "CLOUD_CACHE_PREFIX" in common,
                   "cleanup cache allowlist")
    checks.require("actions: write" in cleanup_workflow, "cleanup cache permission")
    checks.require(NATIVE_HASH in cloud_all, "cloud native lock")

    template_files = sorted(
        path.relative_to(cloud).as_posix() for path in cloud.rglob("*") if path.is_file()
    )
    checks.equal(template_files, sorted([
        ".github/workflows/build.yml", ".github/workflows/cleanup.yml", "README.md",
        "scripts/cloud-common.sh", "scripts/prepare-inputs.py",
    ]), "canonical cloud-template file allowlist")
    export_script = (repo / "scripts/export-cloud-builder-template.sh").read_text(encoding="utf-8")
    for relative in template_files:
        checks.require(relative in export_script, f"template exporter includes {relative}")

    notes = notes_path.read_text(encoding="utf-8")
    for heading in ("Highlights", "Build options", "Supported Celeste inputs", "Save Manager",
                    "Controller and UI", "Performance HUD", "Cloud compilation", "Fixes since RC1",
                    "Upgrade and persistence compatibility", "Known limitations"):
        checks.require(f"## {heading}" in notes, f"release-note section: {heading}")
    checks.require("has not been created or published" in notes, "release notes do not claim a tag")
    checks.require("Game Mode" not in json.dumps(manifest["features"]), "no Game Mode feature claim")
    checks.require("LSSupportsGameMode" not in (repo / "tvos/CelesteTvOSRuntimeHost/Info.plist").read_text(encoding="utf-8"),
                   "no Game Mode plist key")

    result = {
        "schemaVersion": 1,
        "stage": 20,
        "status": "PASS",
        "tests": checks.count,
        "productSourceCommit": PRODUCT_SOURCE,
        "intendedTag": RC2_TAG,
        "tagPresent": False,
    }
    if args.output:
        output = args.output.resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"PASS: {checks.count} Stage 20 RC2 candidate contract tests")
    return 0


if __name__ == "__main__":
    sys.exit(main())
