#!/usr/bin/env python3
"""Verify the tracked Stage 24C1 modern-iOS Celeste product architecture."""

from __future__ import annotations

import json
import pathlib
import plistlib
import subprocess
import sys

BASE = "e8802098b9ffcd772a70aed5d5e29e7cf9e32e01"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
DEFERRED_RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
IOS_GENERATED = "cbd740fef7f312ab961fc098f113046122c7babc86e70f78f68d88078c25b361"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, value: bool, message: str) -> None:
        if not value:
            raise SystemExit(f"FAIL: {message}")
        self.count += 1


def read(root: pathlib.Path, relative: str) -> str:
    return (root / relative).read_text()


def git(root: pathlib.Path, *args: str) -> str:
    return subprocess.check_output(["git", "-C", root, *args], text=True).strip()


def main() -> int:
    root = pathlib.Path(__file__).resolve().parents[1]
    c = Checks()
    prepare = read(root, "scripts/prepare-celeste-ios-runtime.sh")
    transform = read(root, "scripts/celeste-ios-stage24c1.py")
    build = read(root, "scripts/build-ios-celeste.sh")
    project = read(root, "modern-ios/CelesteIOSRuntimeHost/CelesteIOSRuntimeHost.csproj")
    program = read(root, "modern-ios/CelesteIOSRuntimeHost/Program.cs")
    context = read(root, "modern-ios/CelesteIOSRuntimeHost/IOSCelesteRuntimeContext.cs")
    lifecycle = read(root, "modern-ios/CelesteIOSRuntimeHost/IOSCelesteLifecycle.cs")
    fna_extension = read(root, "modern-ios/CelesteIOSRuntimeHost/IOSFnaExtension.targets")
    storage = read(root, "managed/templates/IOSStorageHooks.cs")
    package = read(root, "scripts/verify-ios-package.py")
    profiles = read(root, "managed/celeste-input-profiles.json")

    c.require(git(root, "rev-parse", f"{BASE}^{{commit}}") == BASE, "Stage 24C1 baseline exists")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "RC1 target preserved")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "RC2 target preserved")
    c.require(git(root, "rev-parse", "origin/release/v1.0.0-rc.3^{commit}") == DEFERRED_RC3,
              "deferred RC3 branch preserved")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 tag remains absent")
    c.require(git(root, "diff", "--name-only", BASE, "--", "tvos", "build-tvos.sh", "native") == "",
              "accepted tvOS product/native inputs unchanged")

    for value, label in ((CONTENT, "canonical Content"), (RAW, "canonical raw source"),
                         (PATCHED, "canonical patched source"), (STAGE6, "shared Stage 6 source")):
        c.require(value in prepare or value in transform or value in profiles, f"{label} lock present")
    c.require("celeste-1.4.0.0-a" in profiles and "canonicalClass" in prepare,
              "canonical class flows from exact input profile")
    c.require(all(name in prepare for name in ("prepare-celeste-tvos-stage3c.sh", "celeste-stage5b.py",
                                                "celeste-stage6.py", "celeste-ios-stage24c1.py")),
              "one shared generator with a narrow iOS derivation")
    c.require("ilspycmd" not in transform and "decompile" not in transform.lower(),
              "iOS transform is not a second decompiler/generator")
    c.require("SHARED_COUNT = 934" in transform and STAGE6 in transform,
              "iOS transform fails closed on shared Stage 6 boundary")
    c.require("platformTransform\": \"modern-ios-stage24c1-v1" in transform,
              "versioned iOS-only transform metadata")
    c.require(all(name in transform for name in ("TvOSSaveManagerBridge.cs", "TvOSSoftReloadBridge.cs",
                                                  "TvOSPerformanceHudBridge.cs", "TvOSQuitBridge.cs")),
              "tvOS-only generated services explicitly excluded")
    c.require('"<DefineConstants>$(DefineConstants);CELESTE_RUNTIME;IOS_CELESTE_RUNTIME_HOST</DefineConstants>"'
              in transform,
              "semantic iOS symbols replace Stage/tvOS product symbols")

    c.require("AppleShared/LegacyXnbReaders.cs" in fna_extension and
              "tvos\\CelesteTvOSRuntimeHost\\LegacyXnbReaders.cs" in fna_extension,
              "iOS reuses the single Apple-shared legacy XNB reader source")
    c.require("AppleShared/TitleContainer.cs" in fna_extension and
              "tvos\\CelesteTvOSRuntimeHost\\TvOSTitleContainer.cs" in fna_extension,
              "iOS reuses the single Apple-shared bundle TitleContainer source")
    c.require("CelesteLinker.xml" in project and "tvos" in project,
              "accepted Apple AOT linker closure is shared without duplication")
    c.require(not (root / "modern-ios/CelesteIOSRuntimeHost/IOSLegacyXnbReaders.cs").exists() and
              not (root / "modern-ios/CelesteIOSRuntimeHost/IOSTitleContainer.cs").exists(),
              "no duplicate iOS copies of shared FNA services")

    c.require("IOSProductMode" in project and "Foundation" in project and "Celeste" in project,
              "explicit probe versus Celeste product modes")
    c.require("net10.0-ios26.5" in project and "SupportedOSPlatformVersion>15.0" in project,
              "modern .NET iOS target with minimum iOS 15")
    c.require("UIDeviceFamily" in read(root, "modern-ios/CelesteIOSRuntimeHost/Info.plist"),
              "universal iPhone/iPad product metadata")
    with (root / "modern-ios/CelesteIOSRuntimeHost/Info.plist").open("rb") as stream:
        info = plistlib.load(stream)
    c.require(info["UIDeviceFamily"] == [1, 2], "exact iPhone/iPad device families")
    c.require(info["CFBundleDisplayName"] == "Celeste", "real product display name")
    c.require("SDL_UIKitRunApp" in program and "Celeste.Run(Array.Empty<string>())" in program,
              "one public SDL handoff and one direct Celeste entry")
    c.require("FoundationGame game = new()" in program and "#if IOS_CELESTE_PRODUCT" in program,
              "probe remains separate from product execution")
    c.require("new FmodFoundation" not in program and "fmod-owner=Celeste" in program,
              "Celeste product never initializes the FMOD probe")
    c.require('FNA3D_FORCE_DRIVER", "Metal"' in program, "direct FNA3D Metal selection")

    c.require("ApplicationSupportDirectory" in context and 'Path.Combine(support, "Celeste")' in context,
              "iOS Application Support/Celeste storage root")
    c.require(all(name in storage for name in ('"settings"', '"0"', '"1"', '"2"')),
              "four logical Celeste names only")
    c.require("NSData.FromArray" in storage and "atomically: true" in storage and
              "SafeFileHandle" in storage and "AtomicFileStore" not in storage,
              "AOT-safe native Foundation atomic state writes")
    c.require("NSData.FromFile" in storage and "NSBundle" not in storage,
              "AOT-safe Foundation file reads")
    c.require("CELESTE_IOS_INCIDENTAL_ROOT" in transform and "iOS incidental error-log root" in transform,
              "diagnostic output separated from durable state")
    c.require("CelesteIOSFoundation.csproj" not in transform,
              "generated Celeste does not depend on the desktop System.IO atomic probe")
    c.require("desktop-main-menu-exit-hidden" in transform and "menu_exit" in transform,
              "desktop application Quit row removed deterministically")

    c.require("FMOD_SDL_Register" in transform and "ios-native-output" in transform,
              "narrow iOS/CoreAudio FMOD output adaptation")
    c.require("FMOD_DSP_GetCPUUsage" in transform and "ERR_UNSUPPORTED" in transform,
              "exact FMOD 1.10.09 diagnostic-symbol compatibility")
    c.require(context.count("Master Bank.bank") == 1 and context.count("dlc_sfx.bank") == 1 and
              "banks=7" in context, "exact seven-bank preflight")
    c.require("AppleAudioDiagnostics.LifecyclePause" in lifecycle and
              "AppleAudioDiagnostics.LifecycleResume" in lifecycle,
              "real Celeste FMOD lifecycle adapter")
    c.require("AppleRuntimeDiagnostics.StopAllRumble" in lifecycle,
              "background haptics stop")
    c.require("runtime-disposed=false" in lifecycle and "existing-runtime-resumed=true" in lifecycle,
              "one-runtime lifecycle policy")
    c.require("IOS_SIMULATOR_NO_FMOD" in project and
              "'$(RuntimeIdentifier)' == 'iossimulator-arm64'" in project,
              "simulator-only no-FMOD symbol")
    c.require("Stage 24C1 real Celeste is physical-device-only" in project,
              "real Celeste simulator limitation explicit")
    c.require("FMOD.xcframework" in project and "FMODStudio.xcframework" in project,
              "real FMOD device libraries")

    c.require("UseInterpreter>false" in project and "MtouchLink" in project and "TrimMode" in project,
              "interpreter-free full-trim policy")
    c.require(all(token in build for token in ("RunAOTCompilation=true", "MtouchLink=Full",
                                               "TrimMode=full", "MtouchUseLlvm=true",
                                               "UseInterpreter=false")),
              "device builder locks LLVM/full AOT/full trim")
    c.require("CelesteAppleRepoRoot" in build, "clean restore receives explicit shared repository root")
    c.require("Still publishing Celeste" in build and "free %s" in build,
              "timed phase and heartbeat builder UX")
    c.require("--product celeste" in build, "Celeste package verifier is mandatory")
    c.require("1216" in package and CONTENT in package and "fmodBankCount\": 7" in package,
              "package locks canonical Content and seven banks")
    c.require("_mono_jit_init" in package and "libmono-component-interpreter" in package,
              "package rejects JIT/interpreter")
    c.require("Save Manager" in package and "PerformanceHUD" in package,
              "package rejects tvOS-only product services")
    c.require("UIDeviceFamily" in package and "landscape-only" in package,
              "package verifies universal landscape presentation")

    privacy = plistlib.loads((root / "modern-ios/CelesteIOSRuntimeHost/PrivacyInfo.xcprivacy").read_bytes())
    c.require(privacy["NSPrivacyTracking"] is False and privacy["NSPrivacyCollectedDataTypes"] == [],
              "no tracking or collected data")
    c.require(privacy["NSPrivacyAccessedAPITypes"] == [{
        "NSPrivacyAccessedAPIType": "NSPrivacyAccessedAPICategoryFileTimestamp",
        "NSPrivacyAccessedAPITypeReasons": ["C617.1"],
    }], "minimal app-container file-timestamp privacy reason")
    c.require("49728" not in context + lifecycle + program and "NWListener" not in context + lifecycle + program,
              "no iOS Save Manager/network listener")
    c.require("PerformanceHUD" not in context + lifecycle + program and "MetalForceHudEnabled" not in
              context + lifecycle + program, "no iOS Performance HUD")
    c.require("Xamarin" not in project + program + context + lifecycle, "no Xamarin integration")
    c.require("TouchController" not in program + context + lifecycle, "no fake touch GamePad")

    tracked_generated = git(root, "ls-files", ".build/celeste-ios", "artifacts/ios-celeste")
    c.require(not tracked_generated, "generated/proprietary iOS product outputs remain untracked")
    c.require(".build/celeste-ios/" in read(root, ".gitignore") and
              "artifacts/ios-celeste" in read(root, ".gitignore"),
              "iOS proprietary/generated outputs ignored")
    c.require(IOS_NATIVE in json.dumps(json.loads(read(root, "native/ios-native-output.lock.json"))),
              "accepted iOS native hash preserved")
    c.require(TVOS_NATIVE in read(root, "build-tvos.sh"), "accepted tvOS native hash preserved")

    generated = root / "artifacts/ios-celeste/current/ios-managed.json"
    if generated.exists():
        value = json.loads(generated.read_text())
        c.require(value["output"]["fileCount"] == 928, "generated iOS source file count")
        c.require(value["output"]["logicalSha256"] == IOS_GENERATED, "generated iOS source lock")
        c.require(value["input"]["logicalSha256"] == STAGE6, "generated input is exact shared Stage 6")
        generated_root = root / ".build/celeste-ios/current/managed"
        c.require("CELESTE_IOS_INCIDENTAL_ROOT" in
                  (generated_root / "Monocle/ErrorLog.cs").read_text(),
                  "generated ErrorLog uses incidental root")
        c.require("CelesteIOSFoundation.csproj" not in
                  (generated_root / "Celeste.Modern.csproj").read_text(),
                  "generated Celeste uses its narrow iOS storage adapter")

    print(f"PASS: Stage 24C1 modern iOS Celeste verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
