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


def exports(archive: pathlib.Path, architecture: str = "arm64") -> set[str]:
    result = set()
    # Keep nm diagnostics out of the symbol stream; inherit stderr for logging.
    output = subprocess.check_output(["xcrun", "nm", "-arch", architecture, "-gjU", str(archive)], text=True)
    for line in output.splitlines():
        if line.startswith("_"):
            result.add(line[1:])
    return result


def collect_exports(archive: pathlib.Path, architectures: list[str]) -> dict[str, set[str]]:
    if architectures != ["arm64"]:
        raise SystemExit("iOS native archives must contain exactly arm64")
    return {arch: exports(archive, arch) for arch in architectures}


def validate_component_symbols(component: str, by_arch: dict, mach_names: list[str]) -> None:
    if set(by_arch) != {"arm64"}:
        raise SystemExit("iOS native archives must contain exactly arm64")
    for symbol_set in by_arch.values():
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


def validate_stub_overlap(exports_by_component: dict) -> None:
    stubs = exports_by_component["ApplePlatformStubs"]
    for arch, symbols in stubs.items():
        real = set().union(*(exports_by_component[c][arch] for c in COMPONENTS if c != "ApplePlatformStubs"))
        if real & symbols:
            raise SystemExit(f"ApplePlatformStubs shadows real {arch} symbols: {sorted(real & symbols)}")


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
    exports_by_variant = {x: {} for x in PLATFORMS}
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
            if variant in variants:
                raise SystemExit(f"{component}: duplicate platform variant")
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
            by_arch = collect_exports(archive, inspection["architectures"])
            symbol_set = by_arch["arm64"]
            validate_component_symbols(component, by_arch, mach_names)
            exports_by_variant[variant][component] = by_arch
            (reports / f"{component}-{variant}-exports.txt").write_text(
                "\n".join(sorted(symbol_set)) + "\n")
            (reports / f"{component}-{variant}-exports-by-architecture.json").write_text(
                json.dumps({a: sorted(v) for a, v in by_arch.items()}, indent=2, sort_keys=True) + "\n")
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

    for by_component in exports_by_variant.values():
        validate_stub_overlap(by_component)

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
        executable = reports / f"link-probe-{variant}"
        command.extend(["-lc++", "-lz", "-Wl,-no_adhoc_codesign", "-o", str(executable)])
        subprocess.check_call(command, stdout=subprocess.DEVNULL)
        vtool = run(["xcrun", "vtool", "-show-build", str(executable)])
        if not re.search(rf"^\s*platform\s+{PLATFORMS[variant]}$", vtool, re.MULTILINE):
            raise SystemExit(f"wrong platform in {variant} link probe")
        if not re.search(rf"^\s*minos\s+{re.escape(args.deployment_target)}$", vtool, re.MULTILINE):
            raise SystemExit(f"wrong minimum OS in {variant} link probe")
        if run(["xcrun", "lipo", "-archs", str(executable)]) != "arm64":
            raise SystemExit(f"wrong architecture in {variant} link probe")
        signature = subprocess.run(["codesign", "--display", str(executable)],
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if signature.returncode == 0:
            raise SystemExit(f"{variant} link probe was unexpectedly signed")

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
