#!/usr/bin/env python3
"""Verify and normalize the modern iOS native foundation artifacts."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import re
import subprocess
import sys

COMPONENTS = ("SDL2", "FNA3D", "FAudio", "Theorafile", "ApplePlatformStubs")
PLATFORMS = {"device": "IOS", "simulator": "IOSSIMULATOR"}
SDKS = {"device": "iphoneos", "simulator": "iphonesimulator"}
FRAMEWORKS = (
    "Foundation", "UIKit", "AVFoundation", "AudioToolbox", "CoreBluetooth",
    "CoreGraphics", "CoreHaptics", "CoreMotion", "CoreVideo", "GameController",
    "Metal", "OpenGLES", "QuartzCore",
)
STUB_SYMBOLS = {
    "SDL_SetWindowsMessageHook", "SDL_Direct3D9GetAdapterIndex", "SDL_RenderGetD3D9Device",
    "SDL_DXGIGetOutputInfo", "SDL_LinuxSetThreadPriority", "SDL_AndroidGetJNIEnv",
    "SDL_AndroidGetActivity", "SDL_GetAndroidSDKVersion", "SDL_IsAndroidTV",
    "SDL_IsChromebook", "SDL_IsDeXMode", "SDL_AndroidBackButton",
    "SDL_AndroidGetInternalStoragePath", "SDL_AndroidGetExternalStorageState",
    "INTERNAL_SDL_AndroidGetExternalStoragePath", "SDL_AndroidGetExternalStoragePath",
    "SDL_WinRTGetFSPathUNICODE", "SDL_WinRTGetFSPathUTF8", "SDL_WinRTGetDeviceFamily",
    "SDL_WinRTRunApp", "SDL_AndroidRequestPermission", "SDL_RenderGetD3D11Device",
    "SDL_AndroidShowToast", "emscripten_set_main_loop", "emscripten_cancel_main_loop",
}


def run(args: list[str], *, text: bool = True) -> str:
    return subprocess.check_output(args, text=text, stderr=subprocess.STDOUT).strip()


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def canonical_sha(value: object) -> str:
    encoded = json.dumps(value, sort_keys=True, separators=(",", ":")).encode()
    return hashlib.sha256(encoded).hexdigest()


def exports(archive: pathlib.Path) -> set[str]:
    result = set()
    for line in run(["xcrun", "nm", "-gjU", str(archive)]).splitlines():
        if line.startswith("_"):
            result.add(line[1:])
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path, required=True)
    parser.add_argument("--build-dir", type=pathlib.Path, required=True)
    parser.add_argument("--output-dir", type=pathlib.Path, required=True)
    parser.add_argument("--deployment-target", default="15.0")
    parser.add_argument("--compare-manifest", type=pathlib.Path)
    args = parser.parse_args()
    repo, build, output = args.repo_root.resolve(), args.build_dir.resolve(), args.output_dir.resolve()
    reports = output / "verification"
    reports.mkdir(parents=True, exist_ok=True)
    lock = json.loads((repo / "native/ios-dependencies.lock.json").read_text())
    if lock["deploymentTarget"] != args.deployment_target:
        raise SystemExit("deployment target differs from lock")
    installed_sdks = {name: run(["xcrun", "--sdk", sdk, "--show-sdk-version"])
                      for name, sdk in SDKS.items()}
    if set(installed_sdks.values()) != {lock["compileSdk"]}:
        raise SystemExit(f"iOS SDK drift: {installed_sdks}")

    dependency_revisions = {d.get("component"): d["revision"] for d in lock["dependencies"]
                            if d.get("component") in COMPONENTS}
    artifacts: dict[str, object] = {}
    archives: dict[str, dict[str, pathlib.Path]] = {x: {} for x in PLATFORMS}

    for component in COMPONENTS:
        xcf = output / f"{component}.xcframework"
        with (xcf / "Info.plist").open("rb") as stream:
            info = plistlib.load(stream)
        libraries = info.get("AvailableLibraries", [])
        if len(libraries) != 2:
            raise SystemExit(f"{component}: expected exactly device and simulator")
        variants: dict[str, object] = {}
        for library in libraries:
            if library.get("SupportedPlatform") != "ios":
                raise SystemExit(f"{component}: non-iOS slice")
            variant = "simulator" if library.get("SupportedPlatformVariant") == "simulator" else "device"
            if library.get("SupportedArchitectures") != ["arm64"]:
                raise SystemExit(f"{component}/{variant}: must be arm64 only")
            identifier = library["LibraryIdentifier"]
            archive = xcf / identifier / library["LibraryPath"]
            staged = build / "stage" / component / variant / f"lib{component}.a"
            if not archive.is_file() or sha256(archive) != sha256(staged):
                raise SystemExit(f"{component}/{variant}: package differs from staged archive")
            inspection_path = reports / f"{component}-{variant}.json"
            subprocess.check_call([
                sys.executable, str(repo / "scripts/inspect-apple-archive.py"), str(archive),
                "--output", str(inspection_path),
            ])
            inspection = json.loads(inspection_path.read_text())
            if inspection["architectures"] != ["arm64"]:
                raise SystemExit(f"{component}/{variant}: wrong archive architectures")
            members = []
            mach_names = []
            for slice_data in inspection["slices"]:
                for member in slice_data["members"]:
                    if member["type"] == "mach-o":
                        if member["platform"] != PLATFORMS[variant]:
                            raise SystemExit(f"{component}/{variant}: platform leak {member['platform']}")
                        if member["minos"] != args.deployment_target:
                            raise SystemExit(f"{component}/{variant}: wrong minimum {member['minos']}")
                        if member["architectures"] != ["arm64"]:
                            raise SystemExit(f"{component}/{variant}: member is not arm64")
                        mach_names.append(member["name"])
                    name = re.sub(r"-[0-9a-f]{32}(?=\.o$)", "-<PATH_HASH>", member["name"])
                    members.append([name, member["type"], member["sha256"]])
            symbol_set = exports(archive)
            (reports / f"{component}-{variant}-exports.txt").write_text(
                "\n".join(sorted(symbol_set)) + "\n")
            if component == "SDL2":
                if "SDL_UIKitRunApp" not in symbol_set or "main" in symbol_set:
                    raise SystemExit("SDL must retain SDL_UIKitRunApp and omit standalone main")
                if any("SDL_uikit_main" in name for name in mach_names):
                    raise SystemExit("SDL standalone UIKit main object is present")
            elif component == "FNA3D":
                if not any("FNA3D_Driver_Metal" in name for name in mach_names):
                    raise SystemExit("FNA3D direct Metal driver is absent")
                if any("Vulkan" in name for name in mach_names):
                    raise SystemExit("FNA3D iOS foundation unexpectedly contains Vulkan")
            elif component == "ApplePlatformStubs" and symbol_set != STUB_SYMBOLS:
                raise SystemExit("ApplePlatformStubs export inventory is not exactly 25 reviewed symbols")
            archives[variant][component] = archive
            variants[variant] = {
                "architecture": "arm64",
                "applePlatform": PLATFORMS[variant],
                "minimumDeploymentTarget": args.deployment_target,
                "objectSdks": sorted({m.get("sdk") for s in inspection["slices"] for m in s["members"] if m["type"] == "mach-o"}),
                "installedSdk": installed_sdks[variant],
                "memberFingerprint": members,
                "exportCount": len(symbol_set),
                "exportsSha256": canonical_sha(sorted(symbol_set)),
            }
        artifacts[component] = {
            "revision": dependency_revisions.get(component, "tracked-repository-source"),
            "variants": variants,
            "logicalSha256": canonical_sha({"component": component, "variants": variants}),
        }

    # A real static link catches missing Apple frameworks and cross-component closure.
    probe_source = reports / "link-probe.c"
    probe_source.write_text("int main(void) { return 0; }\n")
    for variant, sdk in SDKS.items():
        target = f"arm64-apple-ios{args.deployment_target}" + ("-simulator" if variant == "simulator" else "")
        command = ["xcrun", "--sdk", sdk, "clang", "-target", target,
                   "-isysroot", run(["xcrun", "--sdk", sdk, "--show-sdk-path"]), str(probe_source)]
        for archive in archives[variant].values():
            command.extend(["-Wl,-force_load," + str(archive)])
        for framework in FRAMEWORKS:
            command.extend(["-framework", framework])
        command.extend(["-lc++", "-lz", "-o", str(reports / f"link-probe-{variant}")])
        subprocess.check_call(command, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)

    tracked_inputs = {
        str(path.relative_to(repo)): sha256(path)
        for path in sorted([
            repo / "native/ios-dependencies.lock.json",
            repo / "native/apple-platform-stubs/ApplePlatformStubs.c",
            repo / "native/apple-platform-stubs/ApplePlatformStubs.h",
            repo / "native/patches/SDL2/0001-tvos-use-pregenerated-metal-shaders.patch",
            repo / "native/patches/SDL2/0003-ios-use-pregenerated-metal-shaders.patch",
            repo / "native/patches/SDL2/0002-ios-modern-scene-host.patch",
            repo / "native/patches/FNA3D/0001-add-sysrenderer-header-to-xcode-project.patch",
            repo / "native/patches/FAudio/0001-align-static-distance-curves-for-ld64.patch",
        ])
    }
    normalized = {
        "schemaVersion": 1,
        "platform": "iOS",
        "deploymentTarget": args.deployment_target,
        "compileSdk": lock["compileSdk"],
        "architectures": {"device": ["arm64"], "simulator": ["arm64"]},
        "renderer": "direct FNA3D Metal",
        "moltenVk": False,
        "trackedInputs": tracked_inputs,
        "components": artifacts,
    }
    normalized["logicalSetSha256"] = canonical_sha(normalized)
    (output / "normalized-manifest.json").write_text(json.dumps(normalized, indent=2, sort_keys=True) + "\n")
    output_lock = json.loads((repo / "native/ios-native-output.lock.json").read_text())
    actual_components = {name: value["logicalSha256"] for name, value in artifacts.items()}
    if output_lock["logicalSetSha256"] != normalized["logicalSetSha256"]:
        raise SystemExit("normalized iOS native output differs from the accepted modern foundation lock")
    if output_lock["componentLogicalSha256"] != actual_components:
        raise SystemExit("iOS native component output differs from the accepted modern foundation lock")
    if args.compare_manifest:
        expected = json.loads(args.compare_manifest.read_text())
        if expected != normalized:
            raise SystemExit("normalized iOS native manifests differ")
    print(f"PASS: iOS native foundation {normalized['logicalSetSha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
