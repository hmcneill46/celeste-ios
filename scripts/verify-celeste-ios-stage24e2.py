#!/usr/bin/env python3
"""Verify Stage 24E2 beginner-builder and iOS release-candidate invariants."""

from __future__ import annotations

import argparse
import json
import pathlib
import plistlib
import re
import subprocess
import sys
import tempfile


BASE = "b669f3766c7fc569031d1f511a5be50cb32dd75a"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
DEFERRED_RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
E1_TREE = "2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357"


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: bool, message: str) -> None:
        if not condition:
            raise SystemExit(f"FAIL: {message}")
        self.count += 1


def read(root: pathlib.Path, relative: str) -> str:
    return (root / relative).read_text()


def git(root: pathlib.Path, *args: str) -> str:
    return subprocess.check_output(["git", "-C", root, *args], text=True).strip()


def origin_branch(root: pathlib.Path, branch: str) -> str:
    found = subprocess.run(["git", "-C", root, "rev-parse", "--verify", "--quiet",
                            f"origin/{branch}^{{commit}}"], capture_output=True, text=True)
    if found.returncode == 0:
        return found.stdout.strip()
    line = git(root, "ls-remote", "--heads", "origin", f"refs/heads/{branch}")
    return line.split()[0] if len(line.split()) == 2 else ""


def prop(text: str, name: str) -> str:
    match = re.search(rf"<{name}>([^<]+)</{name}>", text)
    return match.group(1).strip() if match else ""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app", type=pathlib.Path)
    parser.add_argument("--signing", choices=("unsigned", "development"), default="development")
    args = parser.parse_args()
    root = pathlib.Path(__file__).resolve().parents[1]
    c = Checks()

    builder = read(root, "build-ios.sh")
    low_builder = read(root, "scripts/build-ios-celeste.sh")
    doctor = read(root, "scripts/check-ios-host.sh")
    fmod_validator = read(root, "scripts/validate-fmod-ios-sdk.py")
    device_parser = read(root, "scripts/list-ios-devices.py")
    package = read(root, "scripts/verify-ios-package.py")
    version = read(root, "modern-ios/IOSPortVersion.props")
    runtime_project = read(root, "modern-ios/CelesteIOSRuntimeHost/CelesteIOSRuntimeHost.csproj")
    foundation_project = read(root, "modern-ios/CelesteIOSFoundation/CelesteIOSFoundation.csproj")
    identity_generator = read(root, "scripts/generate-ios-port-build-identity.py")
    readme = read(root, "README.md")
    guide = read(root, "docs/IOS_BUILDING.md")
    status = read(root, "docs/STATUS.md")
    troubleshooting = read(root, "docs/TROUBLESHOOTING.md")
    release = read(root, "docs/releases/ios-v0.1.1-rc.1.md")

    c.require(git(root, "rev-parse", f"{BASE}^{{commit}}") == BASE, "E2 baseline exists")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "RC1 preserved")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "RC2 preserved")
    c.require(origin_branch(root, "release/v1.0.0-rc.3") == DEFERRED_RC3, "deferred RC3 preserved")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 remains untagged")
    c.require(git(root, "diff", "--name-only", BASE, "--", "tvos", "native", "build-tvos.sh") == "",
              "tvOS/native product sources unchanged")
    c.require(git(root, "diff", "--name-only", BASE, "--", ".github") == "",
              "GitHub Actions untouched")

    c.require(prop(version, "IOSPortSemanticVersion") == "0.1.1", "semantic port version")
    c.require(prop(version, "IOSPortBuildNumber") == "5", "monotonic bundle build 5")
    c.require("$(IOSPortSemanticVersion)" in runtime_project and "$(IOSPortBuildNumber)" in runtime_project,
              "bundle metadata consumes one version source")
    c.require("IOSPortVersion.props" in foundation_project and "GenerateIOSPortBuildIdentity" in foundation_project,
              "visible identity is generated from one version source")
    c.require("iOS PORT v{version}" in identity_generator and "BUILD {build_text}" in identity_generator,
              "visible label generator includes semantic version and build")
    c.require(not (root / "modern-ios/CelesteIOSFoundation/IOSPortBuildIdentity.cs").exists(),
              "no independently maintained identity source remains")

    for token in ("--doctor", "--unsigned", "--signed", "--install", "--game-root", "--fmod-root",
                  "--non-interactive", "--clean", "--verbose", "--reset-config"):
        c.require(token in builder, f"beginner builder exposes {token}")
    c.require("--device-id ID" not in builder and "--device NAME" in builder,
              "beginner install never asks for a pasted device identifier")
    c.require("list-ios-devices.py" in builder and "Select an iPhone or iPad" in builder,
              "single/multiple device selection is semantic")
    c.require("configure-ios-personal-team.sh" in builder and "Apple Development" in low_builder,
              "Personal Team development signing path")
    c.require("Xcode has no Personal Team" in builder and "Developer Mode" in builder and
              "Provisioning" in guide and "bundle identifier" in guide,
              "common signing/device failures are actionable")
    c.require(builder.index("validate-celeste") < builder.index("Preparing verified native libraries"),
              "Celeste validation precedes expensive work")
    c.require(builder.index("validate-fmod-ios") < builder.index("Preparing verified native libraries"),
              "FMOD validation precedes expensive work")
    c.require("1.10.09 build 97915" in builder + fmod_validator and
              "deviceArchives" in fmod_validator and "fingerprint mismatch" in fmod_validator,
              "exact FMOD release and archive hashes fail early")
    c.require("Still working:" in read(root, "scripts/tvos-builder-ui.sh") and
              "CELESTE_IOS_HEARTBEAT_SECONDS" in builder,
              "long operations have elapsed/free-disk heartbeat")
    c.require("ui_diagnostic_tail" in builder and "last-error.txt" in builder and "--verbose" in builder,
              "concise default and bounded failure output")
    c.require("ui_cancel_active_command" in builder and "Partial output was not promoted" in builder,
              "interruption cancels descendants without fake success")
    c.require(".incomplete" in builder and builder.index("verify-ios-final") < builder.index("mv \"$TEMP_IPA\""),
              "package is promoted only after verification")
    c.require("Celeste-iOS-v$VERSION-build$BUILD_NUMBER-$suffix.ipa" in builder,
              "semantic privacy-safe IPA names")
    c.require("shasum -a 256" in builder and "IPA_BYTES" in builder,
              "successful output prints SHA-256 and size")
    c.require("SIGNING=development" in builder and "SIGNING=unsigned" in builder and
              "EnableCodeSigning=false" in low_builder,
              "independent unsigned and development-signed product modes")
    c.require("--signing \"$SIGNING\"" in low_builder and "embedded.mobileprovision" in package,
              "package verifier distinguishes unsigned/development outputs")
    c.require("UseInterpreter=false" in low_builder and "RunAOTCompilation=true" in low_builder and
              "MtouchLink=Full" in low_builder and "TrimMode=full" in low_builder and "MtouchUseLlvm=true" in low_builder,
              "full-AOT/full-trim/LLVM/no-interpreter build")
    c.require("10.0.302" in doctor and "10.0.302.0" in doctor and
              r"26\.5\.10301" in doctor,
              "exact toolchain checks remain locked")
    # The expected Xcode values are represented as variables in the doctor.
    c.require('expected_xcode="26.6"' in doctor and 'expected_sdk="26.5"' in doctor,
              "exact Xcode and SDK checks")
    c.require("15 GiB is recommended" in builder and "about 11 GiB" in guide,
              "disk warning is based on observed intermediates")
    c.require(IOS_NATIVE in builder and IOS_NATIVE in read(root, "native/ios-native-output.lock.json"),
              "exact reusable iOS native cache lock")
    c.require(TVOS_NATIVE in read(root, "build-tvos.sh"), "tvOS native lock")

    profiles = json.loads(read(root, "managed/celeste-input-profiles.json"))
    profile_values = profiles.get("profiles", profiles if isinstance(profiles, list) else [])
    c.require(len(profile_values) == 9, "exact nine supported input profiles")
    c.require("validate-celeste-input.sh" in builder and "profileId" in builder,
              "automatic semantic input detection")
    locks = read(root, "managed/celeste-input-profiles.json") + read(root, "scripts/celeste-managed.py") + low_builder
    for value, label in ((CONTENT, "Content"), (RAW, "raw"), (PATCHED, "patched"), (STAGE6, "Stage 6")):
        c.require(value in locks, f"canonical {label} lock")
    c.require(E1_TREE in read(root, "scripts/verify-celeste-ios-stage24e1.py"), "E1 944-file generated lock")

    with tempfile.TemporaryDirectory() as temp_text:
        temp = pathlib.Path(temp_text)
        source = temp / "devices.json"
        output = temp / "devices.tsv"
        source.write_text(json.dumps([
            {"name": "Phone", "identifier": "phone-private", "platform": "com.apple.platform.iphoneos",
             "simulator": False, "available": True, "operatingSystemVersion": "26.5"},
            {"name": "Pad", "identifier": "pad-private", "platform": "com.apple.platform.iphoneos",
             "simulator": False, "available": True, "operatingSystemVersion": "15.8.8"},
            {"name": "Simulator", "identifier": "sim", "platform": "com.apple.platform.iphoneos",
             "simulator": True, "available": True},
            {"name": "TV", "identifier": "tv", "platform": "com.apple.platform.appletvos",
             "simulator": False, "available": True},
        ]))
        subprocess.check_call([sys.executable, str(root / "scripts/list-ios-devices.py"),
                               "--input", str(source), "--output", str(output)])
        rows = output.read_text().splitlines()
        c.require(len(rows) == 2 and rows[0].split("\t")[1] == "Pad" and rows[1].split("\t")[1] == "Phone",
                  "device parser selects only available physical iPhone/iPad")
        c.require("15.8.8" in rows[0] and "26.5" in rows[1], "device selection preserves visible OS versions")
    c.require("platform" in device_parser and "com.apple.platform.iphoneos" in device_parser,
              "device parser has a narrow platform boundary")

    c.require("Choose a platform" in readme and "./build-ios.sh" in readme and "docs/IOS_BUILDING.md" in readme,
              "repository root exposes the iPhone/iPad path")
    for heading in ("What you need", "Get the repository", "Get clean Celeste files", "Get the exact FMOD SDK",
                    "Prepare Xcode", "Run the doctor", "Build", "Launch and update later", "Saves, Files",
                    "Common problems", "Current limitations"):
        c.require(heading in guide, f"beginner guide covers {heading}")
    c.require("FMOD Engine iOS/tvOS 1.10.09 build 97915" in guide and
              "download?version=1.10.09#fmodengine" in guide,
              "exact beginner FMOD guidance")
    c.require("Personal Team" in guide and "about seven days" in guide and "Do not uninstall" in guide,
              "accurate free-provisioning/update guidance")
    c.require("Options → Data & Files" in guide and ".celeste" in guide and ".celestetouch" in guide,
              "Files and layout portability documented")
    c.require("physical" in guide and "device" in guide and "no arm64 iOS Simulator audio slice" in guide,
              "simulator limitation is explicit")
    c.require("release candidate" in status.lower() and "iPadOS 15.8.8" in status,
              "current status records modern iOS acceptance")
    c.require("iPhone/iPad builder problems" in troubleshooting and "artifacts/ios/logs/last-error.txt" in troubleshooting,
              "current troubleshooting routes builder failures")
    c.require("Celeste iOS Port v0.1.1 RC1" in release and "bundle build **5**" in release and
              "Everest/mod support" in release,
              "timeless iOS RC1 release-notes draft")
    c.require("Google Material Symbols" in read(root, "modern-ios/Assets/TouchControls/NOTICE.md") and
              "Creative Commons Attribution 3.0" in read(root, "modern-ios/Assets/TouchControls/NOTICE.md"),
              "touch artwork notices remain discoverable")
    privacy = plistlib.loads((root / "modern-ios/CelesteIOSRuntimeHost/PrivacyInfo.xcprivacy").read_bytes())
    c.require(privacy["NSPrivacyTracking"] is False and privacy["NSPrivacyCollectedDataTypes"] == [],
              "privacy manifest still declares no tracking/collection")
    info = read(root, "modern-ios/CelesteIOSRuntimeHost/Info.plist")
    c.require(all(token not in info for token in ("NSCameraUsageDescription", "NSLocation", "NSMotionUsageDescription")),
              "no camera/location/motion permission added")
    c.require(all(token not in runtime_project for token in
                  ("com.apple.developer.icloud", "application-groups", "dynamic-codesigning")),
              "no paid/cloud/JIT capability added")
    c.require(not git(root, "ls-files", ".build/ios-self-build", "artifacts/ios", ".build/celeste-ios"),
              "private/generated/package roots stay ignored")

    help_text = subprocess.check_output([str(root / "build-ios.sh"), "--help"], text=True)
    c.require("Guided build" in help_text and "unsigned IPA" in help_text and "no UDID is required" in help_text,
              "root help is beginner-facing")
    subprocess.check_call(["bash", "-n", str(root / "build-ios.sh"), str(root / "scripts/build-ios-celeste.sh")])
    c.require(True, "builder shell syntax")

    if args.app:
        subprocess.check_call([sys.executable, str(root / "scripts/verify-ios-package.py"),
                               "--app", str(args.app.resolve()), "--lane", "device", "--product", "celeste",
                               "--signing", args.signing])
        info_value = plistlib.loads((args.app.resolve() / "Info.plist").read_bytes())
        c.require(info_value.get("CFBundleShortVersionString") == "0.1.1" and
                  str(info_value.get("CFBundleVersion")) == "5",
                  "built product is iOS port v0.1.1 Build 5")

    report = root / "docs/history/stages/IOS_RELEASE_ACCEPTANCE_STAGE24E2_REPORT.md"
    if report.exists():
        report_text = report.read_text()
        c.require("Stage 24E2" in report_text and "GREEN" in report_text and BASE in report_text,
                  "final E2 report records accepted baseline/status")
        c.require(report.name in read(root, "docs/history/README.md"), "history index links E2 report")

    print(f"PASS: Stage 24E2 iOS release workflow verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
