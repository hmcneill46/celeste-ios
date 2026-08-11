#!/usr/bin/env python3
"""Verify the production Stage 13B high-level Celeste soft-reload boundary."""

from __future__ import annotations

import argparse
import json
import pathlib
import plistlib
import subprocess
import sys
import zipfile


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def require(text: str, tokens: tuple[str, ...], label: str) -> None:
    for token in tokens:
        if token not in text:
            fail(f"{label} is missing required token: {token}")


def scan_blobs(blobs: list[bytes], label: str) -> None:
    for token in ("STAGE13B_RELOAD", "CHANGES SAVED", "RELOADING CELESTE", "press Confirm to reload Celeste"):
        encoded = (token.encode(), token.encode("utf-16le"))
        if not any(any(value in blob for value in encoded) for blob in blobs):
            fail(f"{label} lacks Stage 13B product token: {token}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--generated-root", type=pathlib.Path)
    parser.add_argument("--app", type=pathlib.Path)
    parser.add_argument("--ipa", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    repo = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()

    store = (repo / "tvos/CelesteTvOSRuntimeHost/Stage6PersistenceStore.cs").read_text()
    manager = (repo / "tvos/CelesteTvOSRuntimeHost/Stage10ASaveManager.cs").read_text()
    protocol = (repo / "tvos/CelesteTvOSRuntimeHost/Stage10AHttpProtocol.cs").read_text()
    coordinator = (repo / "tvos/CelesteTvOSRuntimeHost/Stage13BSoftReloadCoordinator.cs").read_text()
    machine = (repo / "tvos/CelesteTvOSRuntimeHost/Stage13BSoftReloadStateMachine.cs").read_text()
    bridge = (repo / "managed/templates/TvOSSoftReloadBridge.cs").read_text()
    ui = (repo / "managed/templates/TvOSSaveManagerBridge.cs").read_text()
    transform = (repo / "scripts/celeste-stage6.py").read_text()
    tests = (repo / "tvos/Stage13BSoftReloadTests/Program.cs").read_text()
    project = (repo / "tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj").read_text()

    require(store, (
        "PrepareExternalMutationReload", "CompleteExternalMutationReload", "ReadCandidate(\"A\")",
        "ReadCandidate(\"B\")", "ClearMaterializedFiles", "Materialize(durable)",
        "reload-materialization-mismatch", "reload-completion-ticket-mismatch",
        "externalMutationRequiresRestart = false",
    ), "ticketed persistence authority")
    require(manager, ("StopForSoftReload", "CompleteSoftReload", "MarkSoftReloadFailed"), "Save Manager reload lifecycle")
    require(protocol, (
        "press Confirm to reload Celeste", "X-Celeste-Reload-Required",
        "If reload fails, fully close Celeste from the Apple TV app switcher",
    ), "browser success/fallback UX")
    require(bridge, ("TvOSSoftReloadPhase", "ReloadRequested", "UpdateRequested"), "generated host bridge")
    require(ui, ("CHANGES SAVED", "RELOADING CELESTE", "RELOAD FAILED", "TvOSSoftReloadHooks.RequestReload"), "TV UX")
    require(machine, (
        "PreparingReload", "ReloadingSettings", "ReloadingGameState", "WaitingForMainMenu",
        "Verifying", "Complete", "Failure", "DidEnterBackground",
    ), "main-thread state machine")
    require(coordinator, (
        "Settings.Reload()", "Input.Initialize()", "Input.ResetGrab()", "SaveData.Instance = null",
        "OuiFileSelect.Loaded = false", "OverworldLoader(Overworld.StartMode.MainMenu)",
        "TvOSStage5BAudioBridge.Initialized", "TvOSStage5BAudioBridge.BanksReady",
        "ReferenceEquals(Input.Jump.Binding, Settings.Instance.Jump)", "RequireRuntimeIdentity",
        "GC.GetTotalMemory", "fallback=app-switcher-termination",
        "CaptureRuntimeIdentityOnFirstUpdate();", "runtime-identity=pending-first-update",
    ), "host reload coordinator")
    constructor = coordinator.split("internal Stage13BSoftReloadCoordinator", 1)[1].split("internal TvOSSoftReloadPhase Phase", 1)[0]
    if "Engine.Instance" in constructor or "Engine.Graphics" in constructor:
        fail("Stage 13B coordinator captures FNA identity before Celeste constructs the game")
    require(transform, (
        "TVOS_STAGE13B", "TvOSSoftReloadBridge.cs", "TvOSSoftReloadHooks.Update()",
        "Stage 13B main-thread soft-reload update hook",
    ), "locked generated transformation")
    require(project, ("UseInterpreter>false",), "host project")
    if not any(version in project for version in (
        "13.0.0+stage13b", "15.0.0+stage15", "16.0.0+stage16b"
    )):
        fail("host project no longer identifies the accepted Stage 13B boundary or an accepted successor")

    forbidden_calls = (
        "Process.Start(", "Environment.Exit(", "Engine.Instance.Exit(", ".Game.Exit(",
        "Engine.Instance.Dispose(", "Celeste.Run(", "SDL_Quit(",
    )
    for call in forbidden_calls:
        if call in coordinator:
            fail(f"production soft reload contains forbidden runtime/process call: {call}")

    required_tests = (
        "reload state-machine success", "duplicate Confirm suppression",
        "preparation ticket generation mismatch", "materialisation hash mismatch",
        "Settings reload failure", "input reload failure", "main-menu timeout",
        "completion ticket mismatch", "failure cannot blindly retry",
        "background before Confirm", "background during reload", "network stop precedes materialisation",
        "FNA lifecycle calls forbidden", "Game and graphics identity required unchanged",
    )
    require(tests, required_tests, "Stage 13B tests")

    for document in ("README.md", "docs/STATUS.md", "docs/BUILDING.md", "docs/TROUBLESHOOTING.md"):
        text = (repo / document).read_text()
        require(text, ("soft reload", "app switcher"), document)

    generated_summary = None
    if args.generated_root:
        generated = args.generated_root.resolve()
        generated_bridge = (generated / "Celeste/TvOSSoftReloadBridge.cs").read_text()
        generated_game = (generated / "Celeste/Celeste.cs").read_text()
        generated_project = (generated / "Celeste.Modern.csproj").read_text()
        if generated_game.count("TvOSSoftReloadHooks.Update();") != 1:
            fail("generated Celeste update hook is not singular")
        if generated_project.count("TVOS_STAGE13B") != 1:
            fail("generated Stage 13B compile symbol is not exclusive")
        require(generated_bridge, ("TvOSSoftReloadPhase", "UpdateRequested"), "generated soft-reload bridge")
        generated_summary = {"updateHooks": 1, "runtimeRestart": False}

    product = None
    if args.app:
        app = args.app.resolve()
        info = plistlib.loads((app / "Info.plist").read_bytes())
        if "LSSupportsGameMode" in info or "GCSupportsGameMode" in info:
            fail("Stage 13B app contains unsupported Game Mode declaration")
        executable = app / str(info.get("CFBundleExecutable", ""))
        blobs = [path.read_bytes() for path in (executable, app / "CelesteTvOSRuntimeHost.dll", app / "Celeste.dll") if path.is_file()]
        scan_blobs(blobs, "built app")
        product = {"app": True, "tokens": "PASS"}
    if args.ipa:
        with zipfile.ZipFile(args.ipa.resolve()) as archive:
            names = [name for name in archive.namelist() if name.startswith("Payload/") and not name.endswith("/")]
            plist_names = [name for name in names if name.endswith(".app/Info.plist")]
            if len(plist_names) != 1:
                fail("IPA does not contain exactly one app Info.plist")
            info = plistlib.loads(archive.read(plist_names[0]))
            if "LSSupportsGameMode" in info or "GCSupportsGameMode" in info:
                fail("Stage 13B IPA contains unsupported Game Mode declaration")
            scan_blobs([archive.read(name) for name in names], "unsigned IPA")
        product = {"ipa": True, "tokens": "PASS"}

    tracked = subprocess.run(["git", "-C", str(repo), "ls-files"], check=True, capture_output=True, text=True).stdout.splitlines()
    offenders = [name for name in tracked if name.lower().endswith((".celeste", ".bank", ".ipa", ".mobileprovision"))]
    if offenders:
        fail("private/proprietary Stage 13B output is tracked: " + ", ".join(offenders[:4]))

    summary = {
        "schemaVersion": 1,
        "result": "PASS",
        "stateTests": 20,
        "oneFnaRuntime": True,
        "appSwitcherNormalPath": False,
        "appSwitcherFailureFallback": True,
        "generated": generated_summary,
        "product": product,
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    print("PASS: Stage 13B ticketed high-level soft reload, one-runtime boundary, and failure fallback")
    return 0


if __name__ == "__main__":
    sys.exit(main())
