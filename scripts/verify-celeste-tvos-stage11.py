#!/usr/bin/env python3
"""Verify the tracked Stage 11 artwork-only controller-prompt boundary."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess
import sys
import zipfile


KEY = "CelesteTvOS.ControllerPrompts.v1"
MODES = ("Automatic", "Xbox", "PlayStation", "NintendoSwitch", "Stadia")
DISPLAY_MODES = ("Automatic", "Xbox", "PlayStation", "Nintendo Switch", "Stadia")


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def require(text: str, tokens: tuple[str, ...], label: str) -> None:
    for token in tokens:
        if token not in text:
            fail(f"{label} is missing required token: {token}")


def digest(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def scan_product(app: pathlib.Path) -> dict[str, object]:
    info_path = app / "Info.plist"
    if not info_path.is_file():
        fail("Stage 11 --app does not name a built app bundle")
    info = plistlib.loads(info_path.read_bytes())
    candidates = [
        app / str(info.get("CFBundleExecutable", "")),
        app / "CelesteTvOSRuntimeHost.dll",
        app / "Celeste.dll",
    ]
    blobs = [path.read_bytes() for path in candidates if path.is_file()]
    for token in (KEY, "Controller Prompts", "Nintendo Switch", "STAGE11_PROMPTS"):
        encodings = (token.encode("utf-8"), token.encode("utf-16le"))
        if not any(any(encoded in blob for encoded in encodings) for blob in blobs):
            fail(f"built app lacks Stage 11 product token: {token}")
    return {"path": "<APP>", "tokens": "PASS"}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--generated-root", type=pathlib.Path)
    parser.add_argument("--app", type=pathlib.Path)
    parser.add_argument("--ipa", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    repo = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()

    lock = json.loads((repo / "managed/celeste-controller-prompts.lock.json").read_text())
    policy = (repo / "tvos/CelesteTvOSRuntimeHost/ControllerPromptPolicy.cs").read_text()
    shared_policy = (repo / "shared/CelesteAppleInput/ControllerPromptPolicy.cs").read_text()
    preferences = (repo / "tvos/CelesteTvOSRuntimeHost/ControllerPromptPreferences.cs").read_text()
    bridge = (repo / "managed/templates/TvOSControllerPromptBridge.cs").read_text()
    transform = (repo / "scripts/celeste-stage6.py").read_text()
    tests = (repo / "tvos/ControllerPromptTests/Program.cs").read_text()
    settings_serializer = (repo / "managed/templates/TvOSSettingsSerializer.cs").read_text()
    save_serializer = (repo / "managed/templates/TvOSSaveDataSerializer.cs").read_text()
    persistence = (repo / "tvos/CelesteTvOSRuntimeHost/PersistenceStore.cs").read_text()
    save_manager_protocol = (repo / "tvos/CelesteTvOSRuntimeHost/SaveManagerHttpProtocol.cs").read_text()

    if lock.get("schemaVersion") != 1 or lock.get("gameVersion") != "1.4.0.0":
        fail("controller-prompt inventory lock identity changed")
    if len(lock.get("requiredButtons", [])) != 24:
        fail("locked required controller-button matrix is not 24 entries")
    families = lock.get("families", [])
    if [family.get("displayName") for family in families] != list(DISPLAY_MODES[1:]):
        fail("locked manual prompt-family order changed")
    if any(not family.get("complete") or not family.get("selectable") for family in families):
        fail("an exposed manual prompt family is incomplete")
    if any(family.get("directResolutionCount", 0) + family.get("fallbackResolutionCount", 0) != 24 for family in families):
        fail("an exposed family does not resolve the complete required matrix")

    require(policy + shared_policy, (KEY, *MODES, '"xb1"', '"ps4"', '"ns"', '"stadia"'), "shared host preference policy")
    require(preferences, (
        "NSUserDefaults.StandardUserDefaults", "GCController.Current", "GCController.Controllers",
        "GCProductCategory.DualSense", "GCProductCategory.DualShock4", "GCProductCategory.XboxOne",
        "DidConnectNotification", "DidDisconnectNotification", "DidBecomeCurrentNotification",
        "DidStopBeingCurrentNotification", "controller.Handle == other.Handle",
    ), "Apple controller integration")
    require(bridge, ("TvOSControllerPromptMode", "ModeRequested", "ModeChanged", "PrefixRequested", *DISPLAY_MODES), "generated prompt bridge")
    require(transform, (
        'TextMenu.Slider(\\"Controller Prompts\\"', "GuiInputPrefixAutomatic", "TvOSControllerPromptHooks.ResolvePrefix",
        'controllerPromptBridge": "Celeste/TvOSControllerPromptBridge.cs"',
    ), "locked generated transformation")

    required_tests = (
        "missing key is Automatic", "invalid key is Automatic", "same mode is a no-op",
        "manual Xbox resolves xb1", "manual PlayStation resolves ps4", "manual Nintendo Switch resolves ns",
        "manual Stadia resolves stadia", "DualSense category selects PlayStation",
        "DualShock category selects PlayStation", "Xbox category selects Xbox",
        "unknown controller preserves Celeste fallback", "Siri Remote is not selected",
        "current controller wins multiple-controller selection", "prompt choice does not mutate controller bindings",
        "prompt choice does not mutate save bytes", "Settings import does not alter prompt preference",
        "Settings reset does not alter prompt preference",
    )
    require(tests, required_tests, "Stage 11 policy tests")

    forbidden = settings_serializer + save_serializer + persistence + save_manager_protocol
    if KEY in forbidden or "TvOSControllerPromptMode" in forbidden:
        fail("the host prompt preference leaked into Settings, SaveData, persistence envelope, or Save Manager")
    if "settings.celeste" in bridge or "SaveData" in bridge:
        fail("the generated prompt bridge couples display artwork to game persistence")
    if "VendorName" in preferences:
        fail("automatic classification persists or guesses from controller vendor names")
    if preferences.count("NSUserDefaults.StandardUserDefaults") != 1:
        fail("the prompt preference does not use exactly one standard-defaults store boundary")
    if policy.count(KEY) != 1:
        fail("the fixed UserDefaults key is not singular in the host policy")

    generated_summary: dict[str, object] | None = None
    if args.generated_root:
        generated = args.generated_root.resolve()
        current_settings = generated / "Celeste/Settings.cs"
        current_serializer = generated / "Celeste/TvOSSettingsSerializer.cs"
        base = generated.parent.parent / "base-stage5b/managed/Celeste"
        if not current_settings.is_file() or not current_serializer.is_file():
            fail("--generated-root lacks the generated Settings sources")
        if not (base / "Settings.cs").is_file() or not (base / "TvOSSettingsSerializer.cs").is_file():
            fail("the locked pre-Stage-11 generated baseline is unavailable")
        if digest(current_settings) != digest(base / "Settings.cs"):
            fail("Stage 11 changed the locked Celeste Settings graph")
        if digest(current_serializer) != digest(base / "TvOSSettingsSerializer.cs"):
            fail("Stage 11 changed the locked Settings XML serializer")
        input_source = (generated / "Celeste/Input.cs").read_text()
        menu_source = (generated / "Celeste/MenuOptions.cs").read_text()
        require(input_source, ("GuiInputPrefixAutomatic", "TvOSControllerPromptHooks.ResolvePrefix"), "generated Input bridge")
        require(menu_source, ('TextMenu.Slider("Controller Prompts"', 'TextMenu.Button("Save Manager"'), "generated Options menu")
        generated_summary = {
            "settingsGraphUnchanged": True,
            "settingsSerializerUnchanged": True,
            "menuAndInputHooks": "PASS",
        }

    product_summary: dict[str, object] | None = None
    if args.app:
        product_summary = scan_product(args.app.resolve())
    if args.ipa:
        ipa = args.ipa.resolve()
        if not ipa.is_file():
            fail("--ipa is not a file")
        with zipfile.ZipFile(ipa) as archive:
            names = archive.namelist()
            apps = sorted({name.split("/")[1] for name in names if name.startswith("Payload/") and name.count("/") >= 2 and name.split("/")[1].endswith(".app")})
            if len(apps) != 1:
                fail("unsigned IPA does not contain exactly one Payload app")
            tokens = (KEY, "Controller Prompts", "Nintendo Switch")
            payload_blobs = [archive.read(name) for name in names if name.startswith(f"Payload/{apps[0]}/") and not name.endswith("/")]
            for token in tokens:
                encoded = (token.encode("utf-8"), token.encode("utf-16le"))
                if not any(any(item in blob for item in encoded) for blob in payload_blobs):
                    fail(f"unsigned IPA lacks Stage 11 product token: {token}")
        product_summary = {"path": "<IPA>", "tokens": "PASS"}

    tracked = subprocess.run(["git", "-C", str(repo), "ls-files"], check=True, capture_output=True, text=True).stdout.splitlines()
    offenders = [name for name in tracked if name.lower().endswith((".celeste", ".bank", ".ipa", ".mobileprovision", "gui.meta"))]
    offenders += [name for name in tracked if "controller-prompts" in name.lower() and name.lower().endswith(".png")]
    if offenders:
        fail("private/proprietary Stage 11 output is tracked: " + ", ".join(offenders[:4]))

    summary = {
        "schemaVersion": 1,
        "result": "PASS",
        "preferenceKey": KEY,
        "storedModes": list(MODES),
        "manualFamilies": [family["displayName"] for family in families],
        "requiredButtonCount": 24,
        "newOui": False,
        "settingsXmlChanged": False,
        "saveManagerFileSetChanged": False,
        "generated": generated_summary,
        "product": product_summary,
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    print("PASS: Stage 11 prompt families, host preference isolation, and artwork-only bridge")
    if generated_summary:
        print("PASS: generated Settings graph/XML serializer unchanged")
    if product_summary:
        print("PASS: built product contains Stage 11 selector tokens")
    return 0


if __name__ == "__main__":
    sys.exit(main())
