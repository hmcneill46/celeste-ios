#!/usr/bin/env python3
"""Verify Stage 19's semantic-only production naming refactor."""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import subprocess
import sys


BASELINE = "bbad967974fada41f7884b62b29330063e616899"
NATIVE_SHA = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
CLOUD_SOURCE_SHA = "90ebb023f3043222bc67e72922ae4d68223f009c"

RUNTIME_FILES = {
    "Stage3BLegacyXnbReaders.cs": "LegacyXnbReaders.cs",
    "Stage3BLinker.xml": "CelesteLinker.xml",
    "Stage3BLog.cs": "RuntimeLog.cs",
    "Stage3CHapticLifecycle.cs": "HapticLifecycle.cs",
    "Stage5BAudioLifecycle.cs": "AudioLifecycle.cs",
    "Stage6PersistenceDiagnostic.cs": "PersistenceDiagnostic.cs",
    "Stage6PersistenceLifecycle.cs": "PersistenceLifecycle.cs",
    "Stage6PersistenceStore.cs": "PersistenceStore.cs",
    "Stage6TitleContainer.cs": "TvOSTitleContainer.cs",
    "Stage10AHttpProtocol.cs": "SaveManagerHttpProtocol.cs",
    "Stage10ALanAddressPolicy.cs": "SaveManagerLanAddressPolicy.cs",
    "Stage10ASaveManager.cs": "SaveManagerService.cs",
    "Stage11ControllerPromptPolicy.cs": "ControllerPromptPolicy.cs",
    "Stage11ControllerPromptPreferences.cs": "ControllerPromptPreferences.cs",
    "Stage12BQuitCoordinator.cs": "QuitCoordinator.cs",
    "Stage12BQuitStateMachine.cs": "QuitStateMachine.cs",
    "Stage13BSoftReloadCoordinator.cs": "SoftReloadCoordinator.cs",
    "Stage13BSoftReloadStateMachine.cs": "SoftReloadStateMachine.cs",
    "Stage15QrCodeGenerator.cs": "SaveManagerQrCodeGenerator.cs",
    "Stage16PerformanceHudCoordinator.cs": "PerformanceHudCoordinator.cs",
    "Stage16PerformanceHudPolicy.cs": "PerformanceHudPolicy.cs",
}

TYPE_NAMES = {
    "Stage3BLegacyXnbReaders": "LegacyXnbReaders",
    "Stage3BLog": "RuntimeLog",
    "Stage3CHapticLifecycle": "HapticLifecycle",
    "Stage5BAudioLifecycle": "AudioLifecycle",
    "Stage6PersistenceDiagnostic": "PersistenceDiagnostic",
    "Stage6PersistenceLifecycle": "PersistenceLifecycle",
    "Stage6PersistenceStore": "PersistenceStore",
    "Stage10AExportSnapshot": "SaveExportSnapshot",
    "Stage10BMutationCommand": "SaveMutationCommand",
    "Stage10BMutationResult": "SaveMutationResult",
    "Stage10AHttpResponse": "SaveManagerHttpResponse",
    "Stage10ARequestProgress": "SaveManagerRequestProgress",
    "Stage15PairingState": "SaveManagerPairingState",
    "Stage10AHttpProtocol": "SaveManagerHttpProtocol",
    "Stage10AConnectionGate": "SaveManagerConnectionGate",
    "Stage10AConnectionPolicy": "SaveManagerConnectionPolicy",
    "Stage10ALanAddressPolicy": "SaveManagerLanAddressPolicy",
    "Stage10ASaveManager": "SaveManagerService",
    "Stage11PromptMode": "ControllerPromptMode",
    "Stage11AppleControllerFamily": "AppleControllerFamily",
    "Stage11ControllerCandidate": "ControllerCandidate",
    "IStage11PromptPreferenceStore": "IControllerPromptPreferenceStore",
    "Stage11PromptPreferenceState": "ControllerPromptPreferenceState",
    "Stage11ControllerPromptPolicy": "ControllerPromptPolicy",
    "Stage11ControllerPromptPreferences": "ControllerPromptPreferences",
    "Stage12BQuitCoordinator": "QuitCoordinator",
    "Stage12BQuitState": "QuitState",
    "Stage12BForegroundAction": "QuitForegroundAction",
    "Stage12BQuitStateMachine": "QuitStateMachine",
    "Stage13BSoftReloadCoordinator": "SoftReloadCoordinator",
    "Stage13BSoftReloadStateMachine": "SoftReloadStateMachine",
    "Stage15QrCodeGenerator": "SaveManagerQrCodeGenerator",
    "Stage16PerformanceHudCoordinator": "PerformanceHudCoordinator",
    "Stage16PerformanceHudMode": "PerformanceHudMode",
    "Stage16PerformanceHudPreferenceState": "PerformanceHudPreferenceState",
    "IStage16PerformanceHudPreferenceStore": "IPerformanceHudPreferenceStore",
    "Stage16MetalLayerCandidate": "MetalLayerCandidate",
    "Stage16HudProperties": "PerformanceHudProperties",
    "Stage16PerformanceHudPolicy": "PerformanceHudPolicy",
}

TEST_PROJECTS = {
    "Stage10AProtocolTests": "SaveManagerProtocolTests",
    "Stage11ControllerPromptTests": "ControllerPromptTests",
    "Stage12BQuitTests": "QuitTests",
    "Stage13BSoftReloadTests": "SoftReloadTests",
    "Stage15QrPairingTests": "SaveManagerPairingTests",
    "Stage16PerformanceHudTests": "PerformanceHudTests",
}


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: bool, name: str) -> None:
        self.count += 1
        if not condition:
            raise AssertionError(name)


def git(repo: pathlib.Path, *args: str) -> bytes:
    return subprocess.check_output(["git", "-C", str(repo), *args])


def baseline_file(repo: pathlib.Path, relative: str) -> bytes:
    return git(repo, "show", f"{BASELINE}:{relative}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[1])
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    repo = args.repo_root.resolve()
    checks = Checks()

    runtime = repo / "tvos/CelesteTvOSRuntimeHost"
    checks.require(runtime.is_dir(), "runtime source exists")

    for old, new in RUNTIME_FILES.items():
        checks.require(not (runtime / old).exists(), f"obsolete runtime file removed: {old}")
        checks.require((runtime / new).is_file(), f"semantic runtime file exists: {new}")

    permitted_runtime_stage_files = {"Stage3BFnaExtension.targets"}
    remaining_runtime_stage_files = {
        path.name for path in runtime.iterdir()
        if path.is_file() and re.search(r"Stage\d", path.name)
    }
    checks.require(
        remaining_runtime_stage_files == permitted_runtime_stage_files,
        "runtime Stage filename allowlist is exact",
    )

    runtime_text = "\n".join(
        path.read_text(encoding="utf-8", errors="replace")
        for path in runtime.iterdir() if path.suffix in {".cs", ".csproj", ".targets", ".xml"}
    )
    declaration = re.compile(
        r"\b(?:class|struct|enum|record|interface)\s+((?:I)?(?:TvOS)?Stage\d+[A-Za-z0-9_]*)"
    )
    checks.require(not declaration.search(runtime_text), "no Stage-prefixed active runtime declarations")
    for old, new in TYPE_NAMES.items():
        checks.require(not re.search(rf"\b{re.escape(old)}\b", runtime_text), f"obsolete type absent: {old}")
        checks.require(bool(re.search(rf"\b{re.escape(new)}\b", runtime_text)), f"semantic type present: {new}")

    test_texts: list[str] = []
    for old, new in TEST_PROJECTS.items():
        checks.require(not (repo / "tvos" / old).exists(), f"obsolete test project removed: {old}")
        project = repo / "tvos" / new / f"{new}.csproj"
        checks.require(project.is_file(), f"semantic test project exists: {new}")
        test_texts.extend(path.read_text(encoding="utf-8") for path in project.parent.glob("*.*") if path.is_file())
    checks.require(not declaration.search("\n".join(test_texts)), "no Stage-prefixed active test declarations")

    verifier_text = "\n".join(
        path.read_text(encoding="utf-8", errors="replace")
        for path in (repo / "scripts").glob("verify-celeste-tvos-stage*.*")
        if path.name not in {"verify-celeste-tvos-stage19.py", "verify-celeste-tvos-stage19.sh"}
    )
    for old, new in TEST_PROJECTS.items():
        checks.require(old not in verifier_text, f"no stale verifier project reference: {old}")
        checks.require(new in verifier_text, f"semantic verifier project reference: {new}")

    project = (runtime / "CelesteTvOSRuntimeHost.csproj").read_text(encoding="utf-8")
    main_source = (runtime / "Main.cs").read_text(encoding="utf-8")
    target = (runtime / "Stage3BFnaExtension.targets").read_text(encoding="utf-8")
    build = (repo / "build-tvos.sh").read_text(encoding="utf-8")
    checks.require("<PersistenceEnabled" in project and "<PersistenceStorageNamespace" in project,
                   "semantic persistence build properties")
    checks.require("TVOS_CELESTE_RUNTIME_HOST" in project and "TVOS_CELESTE_RUNTIME_HOST" in main_source,
                   "semantic runtime-host compile symbol")
    checks.require("TVOS_SAVE_MANAGER_AUTOMATION" in project and "TVOS_SAVE_MANAGER_AUTOMATION" in (runtime / "SaveManagerService.cs").read_text(),
                   "semantic Save Manager automation symbol")
    checks.require("-p:PersistenceEnabled=true -p:PersistenceStorageNamespace=production" in build,
                   "public builder uses semantic properties")
    checks.require("Stage6StorageNamespace" not in project and "Stage10AAutomation" not in project,
                   "obsolete active MSBuild properties absent")
    checks.require("Stage6PersistenceEnabled=$(PersistenceEnabled)" in project,
                   "canonical generated-project property boundary retained")
    checks.require("Stage6PersistenceEnabled" in target and "PersistenceEnabled" in target,
                   "canonical property translated at FNA target boundary")
    checks.require("LegacyXnbReaders.cs" in target and "TvOSTitleContainer.cs" in target,
                   "FNA target references semantic source files")
    checks.require("<TrimmerRootDescriptor Include=\"CelesteLinker.xml\"" in project,
                   "semantic linker descriptor referenced")
    checks.require("using Stage" not in runtime_text, "no obsolete compatibility type aliases")

    # Generated Celeste templates are a locked canonical product input. They
    # must stay byte-identical in this host-only naming refactor.
    template_paths = git(repo, "ls-tree", "-r", "--name-only", BASELINE, "managed/templates").decode().splitlines()
    checks.require(bool(template_paths), "baseline generated templates enumerated")
    checks.require(all((repo / path).read_bytes() == baseline_file(repo, path) for path in template_paths),
                   "generated templates remain byte-identical")

    persistence = (runtime / "PersistenceStore.cs").read_text(encoding="utf-8")
    controller = (runtime / "ControllerPromptPolicy.cs").read_text(encoding="utf-8")
    hud = (runtime / "PerformanceHudPolicy.cs").read_text(encoding="utf-8")
    protocol = (runtime / "SaveManagerHttpProtocol.cs").read_text(encoding="utf-8")
    service = (runtime / "SaveManagerService.cs").read_text(encoding="utf-8")

    for token in (
        '"production" => "CelesteTvOS.Persistence.v1"',
        '"acceptance" => "CelesteTvOS.Persistence.Acceptance.v1"',
        '"restart" => "CelesteTvOS.Persistence.Restart.v1"',
        '"tests" => "CelesteTvOS.Persistence.Tests.v1"',
        'private string Key(string slot) => $"{keyPrefix}.{slot}"',
    ):
        checks.require(token in persistence, f"persistence key contract: {token}")
    checks.require('PreferenceKey = "CelesteTvOS.ControllerPrompts.v1"' in controller,
                   "controller prompt preference key frozen")
    checks.require('PreferenceKey = "CelesteTvOS.PerformanceHUD.v1"' in hud,
                   "Performance HUD preference key frozen")
    checks.require('BonjourServiceType = "_celeste-save._tcp"' in service,
                   "Bonjour service contract frozen")

    for token in (
        "MaximumRequestLineBytes = 2048", "MaximumHeaderBytes = 16 * 1024",
        "MaximumHeaderCount = 48", "MaximumAuthBodyBytes = 64",
        "MaximumPairingBodyBytes = 80", "MaximumSettingsUploadBytes = 64 * 1024",
        "MaximumSaveUploadBytes = 256 * 1024", "MaximumConcurrentConnections = 4",
        'SessionCookieName = "CelesteSaveSession"', 'CsrfHeaderName = "x-celeste-csrf"',
        'RevisionHeaderName = "x-celeste-revision"', "TimeSpan.FromSeconds(10)",
        "TimeSpan.FromMinutes(10)", "TimeSpan.FromMinutes(3)",
    ):
        checks.require(token in protocol, f"Save Manager protocol constant frozen: {token}")
    for route in (
        '"/pair"', '"/auth"', '"/download/all"', '"/download/settings"',
        '"/replace/settings"', '"/replace/0"', '"/replace/1"', '"/replace/2"',
        '"/reset/settings"', '"/delete/0"', '"/delete/1"', '"/delete/2"',
    ):
        checks.require(route in protocol, f"Save Manager route frozen: {route}")

    for token in (
        "internal const ushort FormatVersion = 2", "PreviousFormatVersion = 1",
        "LegacyFormatVersion = 0", 'new("settings", "Saves/settings.celeste"',
        'new("0", "Saves/0.celeste"', 'new("1", "Saves/1.celeste"',
        'new("2", "Saves/2.celeste"', "CompressionZlibLevel9 = 1",
    ):
        checks.require(token in persistence, f"persistence format contract frozen: {token}")

    soft_reload = (runtime / "SoftReloadCoordinator.cs").read_text(encoding="utf-8")
    ordered = [
        "detachmentScene = new Scene();", "Engine.Scene = detachmentScene;",
        "Settings.Reload();", "Input.Initialize();", "Input.ResetGrab();",
        "SaveData.Instance = null;", "OuiFileSelect.Loaded = false;",
        "Engine.Scene = new OverworldLoader(Overworld.StartMode.MainMenu);",
        "persistence.CompleteExternalMutationReload(current);",
    ]
    positions = [soft_reload.find(token) for token in ordered]
    checks.require(all(position >= 0 for position in positions) and positions == sorted(positions),
                   "soft reload detach/reload/verify order frozen")
    quit = (runtime / "QuitCoordinator.cs").read_text(encoding="utf-8")
    checks.require("Engine.Exit" not in quit and "Game.Exit" not in quit,
                   "graceful Quit remains runtime-retaining")

    profiles = json.loads((repo / "managed/celeste-input-profiles.json").read_text(encoding="utf-8"))
    canonical = profiles["canonicalClasses"]["celeste-1.4.0.0-a"]
    content = profiles["contentClasses"][canonical["contentClass"]]
    checks.require(content["fileCount"] == 1216 and content["aggregateSha256"] == "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46",
                   "canonical Content lock frozen")
    checks.require(canonical["decompiledSource"] == {"fileCount": 920, "logicalSha256": "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"},
                   "canonical raw-source lock frozen")
    checks.require(canonical["patchedSource"] == {"fileCount": 922, "logicalSha256": "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"},
                   "canonical patched-source lock frozen")
    checks.require(canonical["stage6RealAudio"] == {"fileCount": 934, "logicalSha256": "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"},
                   "canonical Stage-6 tree lock frozen")
    checks.require(NATIVE_SHA in (repo / "managed/celeste-generation.lock.json").read_text(),
                   "native logical hash frozen")
    checks.require(CLOUD_SOURCE_SHA in (repo / "cloud-builder-template/scripts/cloud-common.sh").read_text(),
                   "cloud template source pin frozen")

    cloud_paths = git(repo, "ls-tree", "-r", "--name-only", BASELINE, "cloud-builder-template").decode().splitlines()
    checks.require(all((repo / path).read_bytes() == baseline_file(repo, path) for path in cloud_paths),
                   "cloud template remains byte-identical")

    historical_paths = git(repo, "ls-tree", "-r", "--name-only", BASELINE, "docs/history/stages").decode().splitlines()
    checks.require(bool(historical_paths), "baseline historical reports enumerated")
    checks.require(all((repo / path).read_bytes() == baseline_file(repo, path) for path in historical_paths),
                   "pre-existing historical reports remain byte-identical")

    building = (repo / "docs/BUILDING.md").read_text(encoding="utf-8")
    checks.require("PersistenceEnabled=true" in building and "SaveManagerAutomation=true" in building,
                   "current BUILDING guide uses semantic properties")
    checks.require("Stage6PersistenceEnabled=true" not in building and "Stage10AAutomation=true" not in building,
                   "current BUILDING guide has no obsolete properties")

    result = {
        "schemaVersion": 1,
        "stage": "19",
        "result": "PASS",
        "tests": checks.count,
        "runtimeFilesRenamed": len(RUNTIME_FILES),
        "productionTypesRenamed": len(TYPE_NAMES),
        "testProjectsRenamed": len(TEST_PROJECTS),
        "nativeLogicalSha256": NATIVE_SHA,
        "cloudSourcePin": CLOUD_SOURCE_SHA,
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"STAGE19_SEMANTIC_NAMING_TESTS result=PASS; tests={checks.count}; runtime-files={len(RUNTIME_FILES)}; types={len(TYPE_NAMES)}; test-projects={len(TEST_PROJECTS)}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (AssertionError, KeyError, OSError, subprocess.CalledProcessError) as error:
        print(f"error: Stage 19 verification failed: {error}", file=sys.stderr)
        raise SystemExit(1)
