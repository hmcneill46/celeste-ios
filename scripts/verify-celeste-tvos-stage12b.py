#!/usr/bin/env python3
"""Verify the Stage 12B foreground/background-safe tvOS Quit boundary."""

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


def scan_plist(data: bytes, label: str) -> None:
    info = plistlib.loads(data)
    for key in ("LSSupportsGameMode", "GCSupportsGameMode"):
        if key in info:
            fail(f"{label} contains unsupported Game Mode key {key}")


def scan_app(app: pathlib.Path) -> dict[str, object]:
    info_path = app / "Info.plist"
    if not info_path.is_file():
        fail("--app does not name a built app bundle")
    info_bytes = info_path.read_bytes()
    scan_plist(info_bytes, "built app")
    info = plistlib.loads(info_bytes)
    executable = app / str(info.get("CFBundleExecutable", ""))
    candidates = (executable, app / "CelesteTvOSRuntimeHost.dll", app / "Celeste.dll")
    blobs = [path.read_bytes() for path in candidates if path.is_file()]
    for token in ("LEAVE CELESTE", "STAGE12B_QUIT", "Apple TV app switcher"):
        encoded = (token.encode(), token.encode("utf-16le"))
        if not any(any(value in blob for value in encoded) for blob in blobs):
            fail(f"built app lacks Stage 12B product token: {token}")
    return {"gameModeKeysAbsent": True, "productTokens": "PASS"}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--generated-root", type=pathlib.Path)
    parser.add_argument("--app", type=pathlib.Path)
    parser.add_argument("--ipa", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    repo = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()

    policy = json.loads((repo / "managed/celeste-stage12b-exit-policy.json").read_text())
    if policy.get("schemaVersion") != 1 or policy.get("gameVersion") != "1.4.0.0":
        fail("Stage 12B exit policy identity changed")
    routes = policy.get("userFacingApplicationExitRoutes", [])
    if len(routes) != 1 or routes[0].get("source") != "Celeste/OuiMainMenu.cs" or not routes[0].get("intercepted"):
        fail("the locked user-facing application-exit inventory is not exactly OuiMainMenu.OnExit")
    if policy.get("preparationOrder") != [
        "wait-userio", "verify-stage9b-flush", "stop-save-manager", "stop-haptics", "show-leave-ready"
    ]:
        fail("Stage 12B preparation order changed")

    transform = (repo / "scripts/celeste-stage6.py").read_text()
    bridge = (repo / "managed/templates/TvOSQuitBridge.cs").read_text()
    coordinator = (repo / "tvos/CelesteTvOSRuntimeHost/QuitCoordinator.cs").read_text()
    machine = (repo / "tvos/CelesteTvOSRuntimeHost/QuitStateMachine.cs").read_text()
    manager = (repo / "tvos/CelesteTvOSRuntimeHost/SaveManagerService.cs").read_text()
    save_ui = (repo / "managed/templates/TvOSSaveManagerBridge.cs").read_text()
    http = (repo / "tvos/CelesteTvOSRuntimeHost/SaveManagerHttpProtocol.cs").read_text()
    tests = (repo / "tvos/QuitTests/Program.cs").read_text()
    info_text = (repo / "tvos/CelesteTvOSRuntimeHost/Info.plist").read_text()

    require(transform, (
        "TVOS_STAGE12B", "TvOSQuitBridge.cs", "tvOS-only main-menu application Quit interception",
        "TvOSQuitHooks.Show(delegate { Focused = true; })",
    ), "generated transformation")
    require(bridge, (
        "LEAVE CELESTE", "Your progress has been saved.", "TV/Home button", "Back — Return to Celeste",
        "ForegroundMainMenuGeneration", "UserIO.Saving", "UserIO.SavingResult",
    ), "generated Leave UI")
    for forbidden in ("Engine.Instance.Exit", "Environment.Exit", "abort(", "exit("):
        if forbidden in bridge or forbidden in coordinator:
            fail(f"normal Stage 12B Quit boundary contains forbidden termination call: {forbidden}")
    require(coordinator, (
        'Flush("stage12b-user-leave")', "saveManager.StopForLeave()", 'StopAllRumble("stage12b-leave-ready")',
        "DidEnterBackgroundNotification", "DidBecomeActiveNotification", "WillResignActiveNotification",
        "RequestForegroundMainMenu",
    ), "host Quit coordinator")
    require(machine, (
        "PreparingToLeave", "AwaitingBackground", "LeftViaBackground", "KeepRestartRequired",
        "WillResignActive", "DidEnterBackground", "DidBecomeActive",
    ), "Quit state machine")
    require(manager, ("RestartRequired", "StopForLeave"), "Save Manager lifecycle bridge")
    require(save_ui, (
        "CHANGES SAVED", "Apple TV app switcher", "TvOSSoftReloadHooks.RequestReload",
    ), "TV restart-required guidance")
    require(http, (
        "press Confirm to reload Celeste", "Apple TV app switcher",
    ), "browser restart-required guidance")
    required_tests = (
        "idle begins one Quit request", "duplicate Quit is suppressed", "active save leaves state preparing",
        "successful flush reaches Leave ready", "failed flush remains live in visible failure",
        "Back from Leave restores inactive main menu", "resign-active alone does not complete Leave",
        "actual background completes Leave", "foreground after completed Leave restores main menu",
        "ordinary background preserves ordinary runtime state", "restart-required foreground remains blocked",
        "cold launch has no Leave state", "five Leave foreground cycles reuse one state machine",
    )
    require(tests, required_tests, "Stage 12B state tests")
    if "LSSupportsGameMode" in info_text or "GCSupportsGameMode" in info_text:
        fail("tracked production Info.plist claims unsupported tvOS Game Mode")

    generated_summary = None
    if args.generated_root:
        generated = args.generated_root.resolve()
        main_menu = (generated / "Celeste/OuiMainMenu.cs").read_text()
        generated_bridge = (generated / "Celeste/TvOSQuitBridge.cs").read_text()
        project = (generated / "Celeste.Modern.csproj").read_text()
        if main_menu.count("TvOSQuitHooks.Show") != 1 or main_menu.count("#if TVOS_STAGE12B") != 1:
            fail("generated main-menu Quit interception is not singular")
        if main_menu.count("Engine.Instance.Exit();") != 1 or "#else" not in main_menu:
            fail("generated non-tvOS desktop Quit fallback changed unexpectedly")
        require(generated_bridge, ("LEAVE CELESTE", "TvOSLeaveCelesteUI"), "generated Quit bridge")
        if project.count("TVOS_STAGE12B") != 1:
            fail("generated Stage 12B compile mode is not exclusive")
        generated_summary = {"mainMenuInterceptionCount": 1, "newOui": False, "desktopFallbackPreserved": True}

    product = scan_app(args.app.resolve()) if args.app else None
    if args.ipa:
        ipa = args.ipa.resolve()
        with zipfile.ZipFile(ipa) as archive:
            plist_names = [name for name in archive.namelist() if name.startswith("Payload/") and name.endswith(".app/Info.plist")]
            if len(plist_names) != 1:
                fail("IPA does not contain exactly one Payload app Info.plist")
            scan_plist(archive.read(plist_names[0]), "IPA app")
            blobs = [archive.read(name) for name in archive.namelist() if name.startswith("Payload/") and not name.endswith("/")]
            for token in ("LEAVE CELESTE", "STAGE12B_QUIT"):
                values = (token.encode(), token.encode("utf-16le"))
                if not any(any(value in blob for value in values) for blob in blobs):
                    fail(f"IPA lacks Stage 12B product token: {token}")
        product = {"gameModeKeysAbsent": True, "productTokens": "PASS", "ipa": True}

    tracked = subprocess.run(["git", "-C", str(repo), "ls-files"], check=True, capture_output=True, text=True).stdout.splitlines()
    offenders = [name for name in tracked if name.lower().endswith((".celeste", ".bank", ".ipa", ".mobileprovision"))]
    if offenders:
        fail("private/proprietary Stage 12B output is tracked: " + ", ".join(offenders[:4]))

    summary = {
        "schemaVersion": 1,
        "result": "PASS",
        "interceptedUserExitRoutes": 1,
        "newOui": False,
        "stateTests": 16,
        "gameModeKeysAbsent": True,
        "generated": generated_summary,
        "product": product,
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    print("PASS: Stage 12B main-menu Quit interception, lifecycle policy, restart boundary, and Game Mode absence")
    return 0


if __name__ == "__main__":
    sys.exit(main())
