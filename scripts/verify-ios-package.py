#!/usr/bin/env python3
"""Verify a modern iOS foundation application bundle."""

from __future__ import annotations

import argparse
import json
import pathlib
import plistlib
import re
import subprocess


def run(*arguments: str) -> str:
    return subprocess.check_output(arguments, text=True, stderr=subprocess.STDOUT).strip()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"error: {message}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app", type=pathlib.Path, required=True)
    parser.add_argument("--lane", choices=("device", "simulator"), required=True)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    app = args.app.resolve()
    require(app.is_dir(), "application bundle is missing")
    with (app / "Info.plist").open("rb") as stream:
        info = plistlib.load(stream)
    executable = app / info["CFBundleExecutable"]
    require(executable.is_file(), "application executable is missing")
    require(run("xcrun", "lipo", "-archs", str(executable)).split() == ["arm64"], "app must be arm64 only")
    build = run("xcrun", "vtool", "-show-build", str(executable))
    expected_platform = "IOS" if args.lane == "device" else "IOSSIMULATOR"
    require(re.search(rf"^\s*platform\s+{expected_platform}$", build, re.MULTILINE) is not None,
            f"app is not {expected_platform}")
    require(re.search(r"^\s*minos\s+15\.0$", build, re.MULTILINE) is not None, "minimum iOS is not 15.0")
    require(re.search(r"^\s*sdk\s+26\.5$", build, re.MULTILINE) is not None, "compile SDK is not 26.5")

    require(info.get("MinimumOSVersion") == "15.0", "bundle minimum iOS is not 15.0")
    require(info.get("UIDeviceFamily") == [1, 2], "bundle does not support exactly iPhone and iPad")
    orientations = set(info.get("UISupportedInterfaceOrientations", []))
    require(orientations == {"UIInterfaceOrientationLandscapeLeft", "UIInterfaceOrientationLandscapeRight"},
            "orientation policy is not landscape-only")
    require(set(info.get("UISupportedInterfaceOrientations~ipad", [])) == orientations,
            "iPad orientation policy differs")
    scene = info.get("UIApplicationSceneManifest", {})
    require(scene.get("UIApplicationSupportsMultipleScenes") is False, "multiple FNA scenes must be disabled")
    configurations = scene.get("UISceneConfigurations", {}).get("UIWindowSceneSessionRoleApplication", [])
    require(len(configurations) == 1 and configurations[0].get("UISceneDelegateClassName") == "SDLUIKitSceneDelegate",
            "modern UIScene delegate is absent")
    require((app / "PrivacyInfo.xcprivacy").is_file(), "privacy manifest is absent")
    require(not any(app.rglob("*MoltenVK*")), "MoltenVK must not ship")
    require(not any(app.rglob("*Celeste.exe*")), "Celeste proprietary input must not ship in the foundation")
    require(not any(app.rglob("*Xamarin*")), "legacy Xamarin surface must not ship")
    require(not any(app.rglob("*TopShelf*")), "tvOS Top Shelf material leaked into iOS")
    managed = sorted(app.glob("*.dll"))
    require(managed and all((app / f"{assembly.stem}.aotdata.arm64").is_file() for assembly in managed),
            "not every bundled managed assembly has arm64 AOT data")
    require(not (app / "libmono-component-interpreter.dylib").exists(), "interpreter component is forbidden")

    entitlement_output = subprocess.check_output(
        ["codesign", "-d", "--entitlements", ":-", str(app)],
        text=False, stderr=subprocess.STDOUT)
    xml_start = entitlement_output.find(b"<?xml")
    require(xml_start >= 0, "code-signing entitlements could not be inspected")
    entitlements = plistlib.loads(entitlement_output[xml_start:])
    forbidden_entitlements = {
        "com.apple.developer.driverkit",
        "com.apple.developer.kernel.increased-memory-limit",
        "com.apple.developer.kernel.extended-virtual-addressing",
        "com.apple.developer.user-management",
        "dynamic-codesigning",
    }
    require(not forbidden_entitlements.intersection(entitlements), "forbidden historical/JIT entitlement is present")

    symbols = run("xcrun", "nm", "-gjU", str(executable)).splitlines()
    all_symbols = run("xcrun", "nm", "-gj", str(executable)).splitlines()
    symbol_text = "\n".join(symbols)
    require("_SDL_UIKitRunApp" in symbols, "SDL UIKit entry point is not linked")
    require("_FNA3D_CreateDevice" in symbols and "_FNA3D_SwapBuffers" in symbols, "FNA3D is not linked")
    require("_OBJC_CLASS_$_CAMetalLayer" in all_symbols and "_SDL_Metal_GetDrawableSize" in symbols,
            "real CAMetalLayer presentation closure is absent")
    require("_mono_jit_init" not in symbol_text and "_mono_jit_exec" not in symbol_text,
            "JIT symbols are forbidden")
    strings = run("strings", str(executable))
    require("FNA3D Driver: Metal" in strings, "direct FNA3D Metal driver evidence is absent")
    require("_vkCreateInstance" not in all_symbols, "a Vulkan driver is linked")
    require("Save Manager" not in strings and "_celeste-save._tcp" not in strings,
            "tvOS Save Manager leaked into the iOS foundation")
    require("CelesteTvOS.PerformanceHUD.v1" not in strings and "MetalForceHudEnabled" not in strings,
            "tvOS Performance HUD bootstrap leaked into the iOS foundation")
    if args.lane == "device":
        require("_FMOD_System_Create" in symbols and "_FMOD_Studio_System_Create" in symbols,
                "physical-device FMOD low-level/Studio systems are not linked")
    else:
        require("_FMOD_System_Create" not in symbols and "_FMOD_Studio_System_Create" not in symbols,
                "simulator must not contain FMOD")

    report = {
        "schemaVersion": 1,
        "lane": args.lane,
        "architectures": ["arm64"],
        "platform": expected_platform,
        "minimumIOS": "15.0",
        "compileSdk": "26.5",
        "deviceFamilies": ["iPhone", "iPad"],
        "orientation": "landscape-left-and-right",
        "sceneDelegate": "SDLUIKitSceneDelegate",
        "renderer": "direct FNA3D Metal",
        "moltenVk": False,
        "fullAot": True,
        "fullTrim": True,
        "useInterpreter": False,
        "fmod": args.lane == "device",
        "celesteIncluded": False,
        "touchUiIncluded": False,
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    print(f"PASS: modern iOS {args.lane} package foundation")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
