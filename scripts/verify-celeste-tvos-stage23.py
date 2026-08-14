#!/usr/bin/env python3
"""Verify the current v1.0.0-rc.3 candidate contract."""

from __future__ import annotations

import argparse
import json
import pathlib
import plistlib
import re
import subprocess
import sys
import zipfile
from typing import Any


PRODUCT_SOURCE = "98b3d14459e124301919bec3b054c5348d4ddfb2"
RC1_COMMIT = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2_COMMIT = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3_TAG = "v1.0.0-rc.3"
NATIVE_HASH = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
FMOD_URL = "https://www.fmod.com/download?version=1.10.09#fmodengine"
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
HISTORICAL_FILES = (
    "tvos/stage14-release-candidate.json",
    "tvos/release-candidates/v1.0.0-rc.2.json",
    "docs/releases/v1.0.0-rc.2.md",
    "docs/history/stages/TVOS_RELEASE_CANDIDATE_STAGE14_REPORT.md",
    "docs/history/stages/TVOS_RC2_INTEGRATED_ACCEPTANCE_STAGE20_REPORT.md",
    "docs/history/stages/TVOS_BEGINNER_PROJECT_EXPERIENCE_STAGE21_REPORT.md",
    "docs/history/stages/TVOS_SAVE_MANAGER_CONTINUITY_STAGE22B_REPORT.md",
)


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: bool, label: str) -> None:
        self.count += 1
        if not condition:
            raise SystemExit(f"error: Stage 23 verification failed: {label}")

    def equal(self, actual: Any, expected: Any, label: str) -> None:
        self.require(actual == expected, f"{label}: expected {expected!r}, got {actual!r}")


def git(repo: pathlib.Path, *args: str, check: bool = True) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run(
        ["git", "-C", str(repo), *args],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=check,
    )


def require_tokens(checks: Checks, text: str, tokens: tuple[str, ...], label: str) -> None:
    for token in tokens:
        checks.require(token in text, f"{label}: {token}")


def read_json(path: pathlib.Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def verify_ipa(checks: Checks, path: pathlib.Path) -> dict[str, Any]:
    with zipfile.ZipFile(path) as archive:
        checks.require(archive.testzip() is None, "IPA ZIP integrity")
        plists = [name for name in archive.namelist()
                  if re.fullmatch(r"Payload/[^/]+\.app/Info\.plist", name)]
        checks.equal(len(plists), 1, "IPA contains one Payload app")
        info = plistlib.loads(archive.read(plists[0]))
        checks.equal(info.get("MinimumOSVersion"), "16.0", "IPA minimum tvOS")
        checks.require("LSSupportsGameMode" not in info and "GCSupportsGameMode" not in info,
                       "IPA makes no Game Mode claim")
        names = archive.namelist()
        checks.equal(len([name for name in names if name.endswith(".bank")]), 7,
                     "IPA contains seven FMOD banks")
        checks.require(not any(name.endswith("embedded.mobileprovision") for name in names),
                       "unsigned IPA has no provisioning profile")
    return {"bytes": path.stat().st_size}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path,
                        default=pathlib.Path(__file__).resolve().parents[1])
    parser.add_argument("--template-root", type=pathlib.Path)
    parser.add_argument("--ipa", type=pathlib.Path)
    parser.add_argument("--final", action="store_true")
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    repo = args.repo_root.resolve()
    checks = Checks()

    checks.equal(git(repo, "rev-parse", "v1.0.0-rc.1^{}").stdout.decode().strip(),
                 RC1_COMMIT, "immutable RC1 target")
    checks.equal(git(repo, "rev-parse", "v1.0.0-rc.2^{}").stdout.decode().strip(),
                 RC2_COMMIT, "immutable RC2 target")
    checks.require(git(repo, "show-ref", "--verify", f"refs/tags/{RC3_TAG}", check=False).returncode != 0,
                   "Stage 23 has not created the RC3 tag")
    checks.require(git(repo, "cat-file", "-e", f"{PRODUCT_SOURCE}^{{commit}}", check=False).returncode == 0,
                   "product-source commit exists")
    checks.require(git(repo, "merge-base", "--is-ancestor", PRODUCT_SOURCE, "HEAD", check=False).returncode == 0,
                   "product source is an ancestor of candidate metadata")

    manifest_path = repo / "tvos/release-candidates/v1.0.0-rc.3.json"
    notes_path = repo / "docs/releases/v1.0.0-rc.3.md"
    checks.require(manifest_path.is_file(), "RC3 manifest exists")
    checks.require(notes_path.is_file(), "RC3 release notes exist")
    manifest = read_json(manifest_path)
    checks.equal(manifest["schemaVersion"], 1, "manifest schema")
    checks.equal(manifest["stage"], 23, "manifest stage")
    checks.equal(manifest["intendedTag"], RC3_TAG, "intended tag")
    checks.equal(manifest["releaseStatus"], "accepted-release-candidate", "timeless status")
    checks.equal(manifest["rc1"], {"tag": "v1.0.0-rc.1", "commit": RC1_COMMIT}, "RC1 identity")
    checks.equal(manifest["rc2"], {"tag": "v1.0.0-rc.2", "commit": RC2_COMMIT}, "RC2 identity")
    checks.equal(manifest["productSourceCommit"], PRODUCT_SOURCE, "product source")
    checks.require("finalCandidateCommit" not in json.dumps(manifest), "no self-referential final SHA")

    build = manifest["build"]
    for key, value in {
        "configuration": "Release", "runtimeIdentifier": "tvos-arm64",
        "minimumTvOS": "16.0", "renderer": "Metal", "trimMode": "full",
        "fullAot": True, "useInterpreter": False,
    }.items():
        checks.equal(build[key], value, f"build contract {key}")
    checks.equal(manifest["native"]["logicalSha256"], NATIVE_HASH, "native lock")
    checks.equal(set(manifest["native"]["components"]),
                 {"SDL2", "FNA3D", "FAudio", "Theorafile", "tvStubs", "MoltenVK"},
                 "native component set")

    game = manifest["celesteInput"]
    checks.equal(game["gameVersion"], "1.4.0.0", "Celeste version")
    checks.equal(game["runtimeFamily"], "FNA", "runtime family")
    checks.equal(game["canonicalClass"], "celeste-1.4.0.0-a", "canonical class")
    checks.equal(set(game["supportedProfiles"]), PROFILES, "nine manifest profiles")
    checks.equal(len(game["supportedProfiles"]), 9, "manifest profile count")
    for name, (files, sha256) in CANONICAL_LOCKS.items():
        checks.equal(game["canonicalLocks"][name], {"files": files, "sha256": sha256},
                     f"canonical lock {name}")
    checks.equal(manifest["audio"], {"engine": "FMOD 1.10.09 build 97915", "bankCount": 7},
                 "FMOD contract")
    checks.equal(manifest["persistence"]["currentWriteFormat"], 2, "persistence format")
    checks.equal(manifest["persistence"]["readableFormats"], [0, 1, 2], "readable formats")
    checks.equal(manifest["persistence"]["logicalFiles"], ["settings", "0", "1", "2"],
                 "logical file allowlist")

    manager = manifest["saveManager"]
    checks.equal(manager["preferredPort"], 49728, "preferred Save Manager port")
    checks.equal(manager["temporaryPortFallback"], "exact-posix-48-once", "fallback policy")
    checks.equal(manager["continuityService"], "celeste-save-manager", "continuity service")
    checks.equal(manager["continuityProtocolVersion"], 1, "continuity protocol")
    for name in ("writable", "oneTimeQrPairing", "confirmDrivenSoftReload", "freshAuthenticationEveryActivation"):
        checks.require(manager[name] is True, f"Save Manager feature: {name}")
    checks.equal(manifest["features"]["controllerPromptModes"],
                 ["Automatic", "Xbox", "PlayStation", "Nintendo Switch", "Stadia"],
                 "controller prompt modes")
    checks.require(manifest["features"]["performanceHud"] is True, "Performance HUD enabled")
    checks.require(manifest["features"]["gracefulMainMenuQuit"] is True, "graceful Quit enabled")
    checks.equal(manifest["builders"]["cloudProductSourceCommit"], PRODUCT_SOURCE, "cloud manifest pin")
    checks.require(manifest["builders"]["cloudOutputUnsigned"] is True, "cloud output unsigned")
    checks.require(manifest["builders"]["cloudSigning"] is False, "cloud signing disabled")

    profiles = read_json(repo / "managed/celeste-input-profiles.json")["profiles"]
    checks.equal(len(profiles), 9, "profile registry count")
    checks.equal({item["id"] for item in profiles}, PROFILES, "profile registry identities")
    checks.require(all(item["canonicalClass"] == "celeste-1.4.0.0-a" for item in profiles),
                   "all profiles share canonical class")
    checks.require(all(item["runtimeFamily"] == "FNA" for item in profiles), "all profiles are FNA")

    runtime = repo / "tvos/CelesteTvOSRuntimeHost"
    persistence = (runtime / "PersistenceStore.cs").read_text(encoding="utf-8")
    protocol = (runtime / "SaveManagerHttpProtocol.cs").read_text(encoding="utf-8")
    service = (runtime / "SaveManagerService.cs").read_text(encoding="utf-8")
    continuity = (runtime / "SaveManagerContinuityPolicy.cs").read_text(encoding="utf-8")
    require_tokens(checks, persistence, (
        '"production" => "CelesteTvOS.Persistence.v1"', "internal const ushort FormatVersion = 2",
        "PreviousFormatVersion = 1", "LegacyFormatVersion = 0", "CompressionZlibLevel9 = 1",
    ), "persistence source")
    require_tokens(checks, continuity, (
        "PreferredPort = 49728", 'ServiceIdentifier = "celeste-save-manager"', "ProtocolVersion = 1",
        "InstanceBytes = 16", "PollIntervalMilliseconds = 2000", "PollTimeoutMilliseconds = 1500",
        "DisconnectFailureThreshold = 3", "DarwinAddressInUse = 48", "ShouldUseEphemeralFallback",
    ), "continuity source")
    require_tokens(checks, service, (
        "parameters.ReuseLocalAddress = true", "NWListener.Create(parameters)",
        "error?.ErrorDomain == NWErrorDomain.Posix", "fallbackAttempted = true",
        'BonjourServiceType = "_celeste-save._tcp"', "if (result.CountsAsManagerActivity) TouchInactivityTimer()",
    ), "Save Manager service")
    require_tokens(checks, protocol, (
        'request.Path == "/status"', 'fetch(\'/status\'', "slide: false", "slide: true",
        "Save Manager is available again.", "Your Save Manager session has expired.",
        "continuityReconnect.disabled=kind==='connected'||kind==='checking'", "name=instance value=",
        "pairingCredential = RandomNumberGenerator.GetBytes(32)", 'SessionCookieName = "CelesteSaveSession"',
    ), "Save Manager protocol")
    checks.require("script-src 'unsafe-inline'" not in protocol, "nonce-only script CSP")
    checks.require(all(token not in protocol for token in ("WebSocket", "EventSource", "Access-Control-Allow-Origin")),
                   "no WebSocket, SSE, or CORS")

    project = (runtime / "CelesteTvOSRuntimeHost.csproj").read_text(encoding="utf-8")
    builder = (repo / "build-tvos.sh").read_text(encoding="utf-8")
    builder_ui = (repo / "scripts/tvos-builder-ui.sh").read_text(encoding="utf-8")
    checks.require("<UseInterpreter>false</UseInterpreter>" in project, "interpreter disabled")
    checks.require("<TrimMode Condition=\"'$(Configuration)' == 'Release'\">full</TrimMode>" in project,
                   "full trim configured")
    require_tokens(checks, builder, (
        "-p:UseInterpreter=false -p:RunAOTCompilation=true",
        "-p:PublishTrimmed=true -p:TrimMode=full -p:MtouchLink=Full", NATIVE_HASH, "Elapsed:",
    ), "public builder")
    require_tokens(checks, builder_ui, (
        "CELESTE_TVOS_HEARTBEAT_SECONDS:-60", "Still working:", "elapsed", "GiB free",
    ), "builder progress UI")
    checks.require('PreferenceKey = "CelesteTvOS.ControllerPrompts.v1"' in
                   (runtime / "ControllerPromptPolicy.cs").read_text(), "prompt preference")
    checks.require('PreferenceKey = "CelesteTvOS.PerformanceHUD.v1"' in
                   (runtime / "PerformanceHudPolicy.cs").read_text(), "HUD preference")

    current_docs = (
        "README.md", "docs/README.md", "docs/BUILDING.md", "docs/CLOUD_BUILDING.md",
        "docs/CELESTE_INPUTS.md", "docs/STATUS.md", "docs/TROUBLESHOOTING.md",
        "docs/releases/v1.0.0-rc.3.md",
    )
    docs_text = {name: (repo / name).read_text(encoding="utf-8") for name in current_docs}
    for name in current_docs:
        checks.require(bool(docs_text[name].strip()), f"current document is nonempty: {name}")
    for name in ("README.md", "docs/BUILDING.md", "docs/CLOUD_BUILDING.md", "docs/TROUBLESHOOTING.md"):
        checks.require(FMOD_URL in docs_text[name], f"exact FMOD URL: {name}")
    require_tokens(checks, docs_text["README.md"], (
        "## Start here", "## Choose how you want to build", "## Step 1 — Get your Celeste files",
        "## Step 2 — Get FMOD", "## Step 3 — Build", "## Step 4 — Sign and install",
    ), "beginner README")
    notes = notes_path.read_text(encoding="utf-8")
    checks.require("accepted third release candidate" in notes, "timeless release wording")
    checks.require("tag has not" not in notes.lower() and "tag does not" not in notes.lower(),
                   "release notes do not assert tag absence")
    require_tokens(checks, notes, (
        "What changed since RC2", "49728", "Checking", "Disconnected", "Reopened", "Session expired",
        "fresh authentication", "1.10.09 build 97915", "Known limitations",
    ), "RC3 release notes")

    for relative in HISTORICAL_FILES:
        expected = git(repo, "show", f"{PRODUCT_SOURCE}:{relative}").stdout
        checks.equal((repo / relative).read_bytes(), expected,
                     f"historical evidence unchanged: {pathlib.Path(relative).name}")

    cloud = repo / "cloud-builder-template"
    workflow = (cloud / ".github/workflows/build.yml").read_text(encoding="utf-8")
    cleanup = (cloud / ".github/workflows/cleanup.yml").read_text(encoding="utf-8")
    common = (cloud / "scripts/cloud-common.sh").read_text(encoding="utf-8")
    cloud_readme = (cloud / "README.md").read_text(encoding="utf-8")
    cloud_all = "\n".join((workflow, cleanup, common, cloud_readme))
    checks.equal(workflow.count(f"CLOUD_PUBLIC_SOURCE_SHA: {PRODUCT_SOURCE}"), 1, "workflow cloud pin")
    checks.equal(common.count(f'CLOUD_PUBLIC_SOURCE_SHA="{PRODUCT_SOURCE}"'), 1, "helper cloud pin")
    checks.require(workflow.index("Checking private repository") < workflow.index("actions/checkout@"),
                   "privacy gate precedes checkout")
    checks.require(workflow.index("Checking private repository") < workflow.index("Inspect private input Release"),
                   "privacy gate precedes input access")
    checks.require("workflow_dispatch:" in workflow and not any(x in workflow for x in
                   ("pull_request:", "schedule:", "push:")), "cloud build remains manual-only")
    checks.require("upload-artifact" not in cloud_all and "download-artifact" not in cloud_all,
                   "no Actions artifact transport")
    checks.equal(set(re.findall(r"actions/[A-Za-z0-9_./-]+@[0-9a-f]{40}", cloud_all)), ACTION_PINS,
                 "external Action pins unchanged")
    checks.require("artifacts/tvos-native/self-build" in workflow and ".build/tvos-host" in workflow,
                   "safe cache allowlist")
    checks.require("cloud_delete_safe_caches" in common and "CLOUD_CACHE_PREFIX" in common,
                   "bounded cache cleanup")
    checks.require("actions: write" in cleanup, "cleanup may delete caches")
    checks.require(NATIVE_HASH in cloud_all, "cloud native lock")
    expected_template_files = sorted((
        ".github/workflows/build.yml", ".github/workflows/cleanup.yml", "README.md",
        "scripts/cloud-common.sh", "scripts/prepare-inputs.py",
    ))
    actual_template_files = sorted(path.relative_to(cloud).as_posix()
                                   for path in cloud.rglob("*") if path.is_file())
    checks.equal(actual_template_files, expected_template_files, "canonical template file allowlist")
    if args.template_root:
        external = args.template_root.resolve()
        external_files = sorted(path.relative_to(external).as_posix()
                                for path in external.rglob("*") if path.is_file() and ".git" not in path.parts)
        checks.equal(external_files, expected_template_files, "exported template file allowlist")
        for relative in expected_template_files:
            checks.equal((external / relative).read_bytes(), (cloud / relative).read_bytes(),
                         f"exported template matches: {relative}")

    tracked = git(repo, "ls-files").stdout.decode().splitlines()
    forbidden_suffixes = (".celeste", ".bank", ".ipa", ".mobileprovision", ".p12", ".cer")
    offenders = [name for name in tracked if name.lower().endswith(forbidden_suffixes)
                 or ".app/" in name.lower()]
    checks.require(not offenders, f"no proprietary/private product tracked: {offenders[:4]}")
    public_text = "\n".join(docs_text.values())
    owner_path = "/Users/" + "harrymcneill/"
    checks.require(owner_path not in public_text, "no project-owner home path")
    private_lan_prefix = "192." + "168."
    checks.require(private_lan_prefix not in public_text, "no private LAN address")
    checks.require("LSSupportsGameMode" not in (runtime / "Info.plist").read_text(encoding="utf-8"),
                   "no Game Mode plist claim")

    product = "not-requested"
    ipa_result: dict[str, Any] = {}
    if args.ipa:
        ipa_result = verify_ipa(checks, args.ipa.resolve())
        product = "ipa"

    report = repo / "docs/history/stages/TVOS_RC3_INTEGRATED_ACCEPTANCE_STAGE23_REPORT.md"
    history = repo / "docs/history/README.md"
    if args.final:
        checks.require(report.is_file(), "final Stage 23 report exists")
        checks.require(report.name in history.read_text(encoding="utf-8"), "history indexes Stage 23")
        report_text = report.read_text(encoding="utf-8")
        checks.require("RC3 tag was intentionally absent during acceptance" in report_text,
                       "historical report records tag absence during acceptance")

    result = {
        "schemaVersion": 1,
        "stage": 23,
        "status": "PASS",
        "checks": checks.count,
        "productSourceCommit": PRODUCT_SOURCE,
        "intendedTag": RC3_TAG,
        "tagCreationByStage23": False,
        "supportedProfiles": 9,
        "preferredPort": 49728,
        "continuityProtocol": 1,
        "product": product,
        **ipa_result,
    }
    if args.output:
        output = args.output.resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"PASS: {checks.count} Stage 23 RC3 candidate contract checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
