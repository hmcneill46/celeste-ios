#!/usr/bin/env python3
"""Verify the tracked modern iOS foundation without requiring proprietary inputs."""

from __future__ import annotations

import json
import pathlib
import plistlib
import re
import subprocess
import sys

BASE = "98b3d14459e124301919bec3b054c5348d4ddfb2"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def check(self, value: bool, message: str) -> None:
        if not value:
            raise SystemExit(f"FAIL: {message}")
        self.count += 1


def read(root: pathlib.Path, relative: str) -> str:
    return (root / relative).read_text()


def git(root: pathlib.Path, *args: str) -> str:
    return subprocess.check_output(["git", "-C", root, *args], text=True).strip()


def main() -> int:
    root = pathlib.Path(__file__).resolve().parents[1]
    t = Checks()
    lock = json.loads(read(root, "native/ios-dependencies.lock.json"))
    output_lock = json.loads(read(root, "native/ios-native-output.lock.json"))
    fmod_lock = json.loads(read(root, "native/fmod-ios-dependencies.lock.json"))
    project = read(root, "modern-ios/CelesteIOSRuntimeHost/CelesteIOSRuntimeHost.csproj")
    program = read(root, "modern-ios/CelesteIOSRuntimeHost/Program.cs")
    game = read(root, "modern-ios/CelesteIOSRuntimeHost/FoundationGame.cs")
    storage = read(root, "modern-ios/CelesteIOSFoundation/AtomicFileStore.cs")
    fmod = read(root, "modern-ios/CelesteIOSRuntimeHost/FmodFoundation.cs")
    package_verifier = read(root, "scripts/verify-ios-package.py")
    host_doctor = read(root, "scripts/check-ios-host.sh")
    all_modern = "\n".join(path.read_text(errors="replace") for path in
                           (root / "modern-ios").rglob("*") if path.is_file()
                           and "bin" not in path.parts and "obj" not in path.parts)

    t.check(git(root, "rev-parse", f"{BASE}^{{commit}}") == BASE, "Stage 24B base exists")
    t.check(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "RC1 target unchanged")
    t.check(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "RC2 target unchanged")
    t.check(git(root, "diff", "--name-only", BASE, "--", "tvos", "build-tvos.sh") == "",
            "accepted tvOS product files unchanged")
    t.check(git(root, "diff", "--name-only", BASE, "--", "celestemeow", "fnalibs-ios-builder-celeste") == "",
            "legacy Xamarin-era sources untouched")

    t.check(lock["deploymentTarget"] == "15.0" and lock["compileSdk"] == "26.5",
            "iOS platform lock")
    t.check(lock["excludedComponents"] == ["MoltenVK", "tvOS Performance HUD bootstrap"],
            "iOS exclusions locked")
    t.check({d["component"] for d in lock["dependencies"] if "component" in d} >=
            {"SDL2", "FNA3D", "FAudio", "Theorafile", "managed-bindings"},
            "native and binding dependency closure")
    t.check(output_lock["logicalSetSha256"] == IOS_NATIVE, "accepted iOS native logical hash")
    t.check(len(output_lock["componentLogicalSha256"]) == 5, "five native components locked")
    t.check(fmod_lock["version"] == "1.10.09" and fmod_lock["build"] == 97915,
            "FMOD release/build exact")
    t.check(fmod_lock["simulatorPolicy"] == "explicit-no-fmod-because-sdk-has-no-arm64-simulator-slice",
            "FMOD simulator limitation explicit")
    t.check(TVOS_NATIVE in read(root, "build-tvos.sh"), "accepted tvOS native hash unchanged")
    t.check(all(token in host_doctor for token in
                ('expected_macos="26.3"', 'expected_dotnet="10.0.302"',
                 'expected_workload_set="10.0.302.0"', 'expected_xcode="26.6"',
                 'expected_xcode_build="17F113"', 'expected_sdk="26.5"')),
            "exact modern iOS host toolchain lock")

    t.check("net10.0-ios26.5" in project, "modern .NET iOS target")
    t.check("SupportedOSPlatformVersion>15.0" in project and "TargetPlatformMinVersion>15.0" in project,
            "minimum iOS 15")
    t.check("UseInterpreter>false" in project and "MtouchUseLlvm" in project and "TrimMode" in project,
            "AOT/LLVM/full-trim policy")
    t.check("ios-arm64" in project and "iossimulator-arm64" in project, "bounded device/simulator RIDs")
    t.check("IOS_SIMULATOR_NO_FMOD" in project and "IOS_DEVICE_FMOD" in project,
            "separate audio lanes")
    t.check("FMOD.xcframework" in project and "FMODStudio.xcframework" in project,
            "device FMOD native references")
    t.check("ApplePlatformStubs.xcframework" in project, "reviewed Apple static stub library linked")

    with (root / "modern-ios/CelesteIOSRuntimeHost/Info.plist").open("rb") as stream:
        info = plistlib.load(stream)
    t.check(info["UIDeviceFamily"] == [1, 2], "one iPhone/iPad target")
    expected_orientations = ["UIInterfaceOrientationLandscapeLeft", "UIInterfaceOrientationLandscapeRight"]
    t.check(info["UISupportedInterfaceOrientations"] == expected_orientations and
            info["UISupportedInterfaceOrientations~ipad"] == expected_orientations,
            "landscape-only declarations")
    scene = info["UIApplicationSceneManifest"]
    t.check(scene["UIApplicationSupportsMultipleScenes"] is False, "single-scene policy")
    t.check(scene["UISceneConfigurations"]["UIWindowSceneSessionRoleApplication"][0]["UISceneDelegateClassName"] ==
            "SDLUIKitSceneDelegate", "true SDL UIScene delegate")
    t.check(info["UIRequiresFullScreen"] is True and info["UIStatusBarHidden"] is True,
            "full-screen presentation")

    t.check("SDL_UIKitRunApp" in program and "FNA3D_FORCE_DRIVER" in program and '"Metal"' in program,
            "public SDL entry and direct Metal selection")
    t.check('FNA_GRAPHICS_ENABLE_HIGHDPI", "1"' in program and "expectedDrawableWidth" in
            read(root, "modern-ios/CelesteIOSRuntimeHost/SceneMetricsCoordinator.cs"),
            "native Retina drawable scale")
    t.check("SDL_HINT_IOS_HIDE_HOME_INDICATOR" in program, "supported home-indicator policy")
    t.check("new SceneMetricsCoordinator(Window)" in game and "AspectFit" in game,
            "real scene metrics and aspect fit")
    t.check("fna-game-count" in game and "LifecyclePolicy" in game, "one-runtime lifecycle policy")
    t.check("GamePad.GetState" in read(root, "modern-ios/CelesteIOSRuntimeHost/ControllerFoundation.cs") and
            "input-path=FNA-SDL" in read(root, "modern-ios/CelesteIOSRuntimeHost/ControllerFoundation.cs"),
            "FNA SDL controller foundation")

    t.check(all(name in storage for name in ("settings.celeste", "0.celeste", "1.celeste", "2.celeste")),
            "approved future Celeste logical files")
    t.check("FileOptions.WriteThrough" in storage and "Flush(flushToDisk: true)" in storage and
            "File.Move(temporary, destination, overwrite: true)" in storage,
            "atomic storage primitive")
    t.check("ApplicationSupportDirectory" in read(root, "modern-ios/CelesteIOSRuntimeHost/ApplicationSupportStorage.cs"),
            "public Application Support lookup")
    t.check("save-files-created=false" in read(root, "modern-ios/CelesteIOSRuntimeHost/ApplicationSupportStorage.cs"),
            "runtime creates no save files")

    t.check("ExpectedVersion = 0x00011009" in fmod, "runtime FMOD version lock")
    t.check("FMOD_System_Init" in fmod and "FMOD_Studio_System_Initialize" in fmod,
            "low-level and Studio FMOD smoke")
    t.check("FMOD_System_MixerSuspend" in fmod and "FMOD_System_MixerResume" in fmod,
            "FMOD lifecycle foundation")
    t.check("if (disposed || suspended" in fmod and "if (disposed || !suspended" in fmod and
            "disposed = true;" in fmod and "suspended = false;" in fmod,
            "FMOD late lifecycle callbacks fail closed after disposal")
    t.check("fmod-foundation unavailable=expected-simulator-policy" in fmod,
            "simulator no-FMOD runtime diagnostic")
    privacy = plistlib.loads((root / "modern-ios/CelesteIOSRuntimeHost/PrivacyInfo.xcprivacy").read_bytes())
    t.check(privacy["NSPrivacyTracking"] is False and privacy["NSPrivacyCollectedDataTypes"] == [],
            "no tracking or collected data")
    t.check(privacy["NSPrivacyAccessedAPITypes"] == [{
        "NSPrivacyAccessedAPIType": "NSPrivacyAccessedAPICategoryFileTimestamp",
        "NSPrivacyAccessedAPITypeReasons": ["C617.1"],
    }], "minimal app-container file-metadata privacy reason")

    stub_source = read(root, "native/apple-platform-stubs/ApplePlatformStubs.c")
    t.check(len(set(re.findall(r"\b(SDL_[A-Za-z0-9_]+|INTERNAL_SDL_[A-Za-z0-9_]+|emscripten_[a-z0-9_]+)\s*\(",
                               stub_source))) == 25, "exact 25-symbol Apple stub implementation inventory")
    t.check("SDLUIKitSceneDelegate" in read(root, "native/patches/SDL2/0002-ios-modern-scene-host.patch"),
            "tracked SDL UIScene patch")
    t.check("SDL_shaders_metal_ios.h" in read(root, "native/patches/SDL2/0003-ios-use-pregenerated-metal-shaders.patch") and
            "SDL_shaders_metal.metal in Sources" in read(root, "native/patches/SDL2/0003-ios-use-pregenerated-metal-shaders.patch"),
            "tracked pregenerated SDL Metal shader policy")
    t.check("__Internal" in read(root, "native/patches/FNA/0001-map-apple-static-imports-to-internal.patch"),
            "AOT-safe static managed import patch")

    forbidden = ("CelesteTvOS.SaveManager", "NWListener", "CelesteTvOS.PerformanceHUD.v1",
                 "MTL_HUD_ENABLED", "MoltenVK", "UserManagement")
    t.check(not any(token in all_modern for token in forbidden), "no tvOS/product-only feature leakage")
    t.check("Celeste.exe" not in all_modern and "Celeste.Content" not in all_modern,
            "no proprietary Celeste runtime integration")
    t.check("TouchControllerState" in all_modern and "touch-controls=not-implemented" in all_modern,
            "touch state boundary only; no virtual controls")
    t.check("_mono_jit_init" in package_verifier and '"fullAot": True' in package_verifier and
            '"useInterpreter": False' in package_verifier,
            "iOS package verifier owns runtime policy")
    t.check("Save Manager" in package_verifier and "_celeste-save._tcp" in package_verifier,
            "package verifier rejects Save Manager leakage")
    t.check("modern iOS/iPadOS engineering foundation" in read(root, "docs/IOS_FOUNDATION.md") and
            "not a playable Celeste iOS port" in read(root, "docs/IOS_FOUNDATION.md"),
            "documentation is explicitly non-playable")

    print(f"PASS: Stage 24B modern iOS foundation verifier ({t.count} checks)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
