#!/usr/bin/env python3
"""Verify a modern iOS foundation application bundle."""

from __future__ import annotations

import argparse
import hashlib
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
    parser.add_argument("--product", choices=("foundation", "celeste"), default="foundation")
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
    require(not any(app.rglob("*Celeste.exe*")), "the original Celeste executable must never ship")
    require(not any(app.rglob("*Xamarin*")), "legacy Xamarin surface must not ship")
    require(not any(app.rglob("*TopShelf*")), "tvOS Top Shelf material leaked into iOS")
    managed = sorted(app.glob("*.dll"))
    require(managed and all((app / f"{assembly.stem}.aotdata.arm64").is_file() for assembly in managed),
            "not every bundled managed assembly has arm64 AOT data")
    require(not (app / "libmono-component-interpreter.dylib").exists(), "interpreter component is forbidden")
    binary_surfaces = [executable, *managed]

    def bundle_contains(token: str) -> bool:
        ascii_token = token.encode()
        utf16_token = token.encode("utf-16le")
        return any(ascii_token in path.read_bytes() or utf16_token in path.read_bytes()
                   for path in binary_surfaces)

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
    drawable_symbol = "_FNA3D_GetDrawableSize" if args.product == "celeste" else "_SDL_Metal_GetDrawableSize"
    require("_OBJC_CLASS_$_CAMetalLayer" in all_symbols and drawable_symbol in symbols,
            "real CAMetalLayer presentation closure is absent")
    require("_mono_jit_init" not in symbol_text and "_mono_jit_exec" not in symbol_text,
            "JIT symbols are forbidden")
    strings = run("strings", str(executable))
    require("FNA3D Driver: Metal" in strings, "direct FNA3D Metal driver evidence is absent")
    require("_vkCreateInstance" not in all_symbols, "a Vulkan driver is linked")
    require(not bundle_contains("Save Manager") and not bundle_contains("_celeste-save._tcp"),
            "tvOS Save Manager leaked into modern iOS")
    require(not bundle_contains("CelesteTvOS.PerformanceHUD.v1") and
            not bundle_contains("MetalForceHudEnabled"),
            "tvOS Performance HUD bootstrap leaked into the iOS foundation")
    if args.lane == "device":
        if args.product == "celeste":
            require("_FMOD_Studio_System_Create" in symbols and
                    "_FMOD_Studio_System_GetLowLevelSystem" in symbols and
                    "_FMOD_System_GetVersion" in symbols,
                    "Celeste FMOD Studio/low-level runtime closure is not linked")
        else:
            require("_FMOD_System_Create" in symbols and "_FMOD_Studio_System_Create" in symbols,
                    "foundation-probe FMOD low-level/Studio systems are not linked")
    else:
        require("_FMOD_System_Create" not in symbols and "_FMOD_Studio_System_Create" not in symbols,
                "simulator must not contain FMOD")

    if args.product == "celeste":
        require(args.lane == "device", "Stage 24C1 Celeste product is physical-device-only")
        require((app / "Celeste.dll").is_file() and (app / "Celeste.Content.dll").is_file(),
                "canonical Celeste managed assemblies are absent")
        require((app / "CelesteIOSFoundation.dll").is_file() and
                (app / "CelesteIOSFoundation.aotdata.arm64").is_file(),
                "shared iOS durability policy or its AOT image is absent")
        content = app / "Content"
        require(content.is_dir(), "canonical Content directory is absent")
        records = []
        logical = hashlib.sha256()
        total = 0
        for path in sorted((item for item in content.rglob("*") if item.is_file()),
                           key=lambda item: item.relative_to(content).as_posix()):
            relative = path.relative_to(content).as_posix()
            data = path.read_bytes()
            sha = hashlib.sha256(data).hexdigest()
            total += len(data)
            logical.update(relative.encode() + b"\0" + str(len(data)).encode() + b"\0" + sha.encode() + b"\n")
            records.append(relative)
        require(len(records) == 1216 and total == 1158665183 and
                logical.hexdigest() == "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46",
                "packaged Celeste Content does not match canonical class A")
        bank_root = content / "FMOD" / "Desktop"
        expected_banks = {"Master Bank.bank", "Master Bank.strings.bank", "music.bank", "sfx.bank",
                          "ui.bank", "dlc_music.bank", "dlc_sfx.bank"}
        require({path.name for path in bank_root.glob("*.bank")} == expected_banks,
                "the exact seven-bank inventory is absent")
        require(bundle_contains("celeste-product-context") and bundle_contains("celeste-run-loop-enter"),
                "modern iOS Celeste host markers are absent")
        require(bundle_contains("IOS_STORAGE recovery=") and bundle_contains("source=previous-good"),
                "bounded previous-good recovery path is absent")
        require(not bundle_contains("CelesteTvOS.Persistence") and
                not bundle_contains("CelesteTvOS.ControllerPrompts"),
                "tvOS host persistence/preferences leaked into iOS")
        require(not bundle_contains("menu_exit"), "desktop application Quit route remains in iOS")
    else:
        require(not (app / "Celeste.dll").exists() and not (app / "Content").exists(),
                "Celeste product material leaked into the foundation probe")

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
        "product": args.product,
        "celesteIncluded": args.product == "celeste",
        "canonicalContentSha256": "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46" if args.product == "celeste" else None,
        "fmodBankCount": 7 if args.product == "celeste" else 0,
        "touchUiIncluded": False,
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    print(f"PASS: modern iOS {args.lane} {args.product} package")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
