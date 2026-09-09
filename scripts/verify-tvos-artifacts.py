#!/usr/bin/env python3
"""Verify Stage 1 tvOS XCFrameworks, symbols, licenses, and link probes."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import pathlib
import plistlib
import re
import shutil
import subprocess
import sys
from typing import Optional


COMPONENTS = ("SDL2", "FNA3D", "FAudio", "Theorafile", "tvStubs", "MoltenVK")
FRAMEWORKS = (
    "Foundation",
    "UIKit",
    "AVFoundation",
    "AudioToolbox",
    "CoreBluetooth",
    "CoreGraphics",
    "CoreHaptics",
    "GameController",
    "IOSurface",
    "Metal",
    "OpenGLES",
    "QuartzCore",
)


def run(args: list[str], *, output: Optional[pathlib.Path] = None) -> str:
    if output is None:
        return subprocess.check_output(args, text=True, stderr=subprocess.STDOUT)
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8") as stream:
        subprocess.check_call(args, stdout=stream, stderr=subprocess.STDOUT)
    return ""


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def canonical_sha(value: object) -> str:
    data = json.dumps(value, sort_keys=True, separators=(",", ":")).encode()
    return hashlib.sha256(data).hexdigest()


def exported_symbols(archive: pathlib.Path, architecture: str = "arm64") -> list[str]:
    # Keep nm diagnostics out of the symbol stream; inherit stderr for logging.
    text = subprocess.check_output(["xcrun", "nm", "-arch", architecture, "-gjU", str(archive)], text=True)
    # nm prints archive/member headings as well as Mach-O symbols.
    return sorted({line[1:] for line in text.splitlines() if line.startswith("_")})


def collect_exports(archive: pathlib.Path, architectures: list[str]) -> dict[str, set[str]]:
    if "arm64" not in architectures or len(set(architectures)) != len(architectures):
        raise SystemExit("required arm64 slice missing or duplicate architecture")
    return {arch: set(exported_symbols(archive, arch)) for arch in architectures}


def validate_required_symbols(variant: str, exports: dict, expectations: dict) -> dict:
    """Check each real slice independently; only same-architecture SDL stubs apply."""
    for component in COMPONENTS:
        expected_archs = {"arm64", "x86_64"} if variant == "simulator" else {"arm64"}
        if component == "MoltenVK" and variant == "device":
            expected_archs.add("arm64e")
        if set(exports[component]) != expected_archs:
            raise SystemExit(f"{component}/{variant}: unexpected architecture set")
        for arch, symbols in exports[component].items():
            expected = set(expectations["components"][component]["symbols"])
            available = symbols
            if component == "SDL2":
                available = available | exports["tvStubs"][arch]
            missing = sorted(expected - available)
            if missing:
                raise SystemExit(f"missing {component}/{variant}/{arch} expected symbols: {missing[:20]}")
    duplicates = {}
    for arch, stubs in exports["tvStubs"].items():
        real = set().union(*(exports[c].get(arch, set()) for c in COMPONENTS if c != "tvStubs"))
        duplicates[arch] = sorted(real & stubs)
        if duplicates[arch]:
            raise SystemExit(f"tvStubs shadows real {variant}/{arch} symbols: {duplicates[arch]}")
    return duplicates


def validate_inspection(path: pathlib.Path, expected_platform: str, deployment: str) -> dict:
    data = json.load(path.open(encoding="utf-8"))
    if "arm64" not in data["architectures"]:
        raise SystemExit(f"required arm64 slice missing: {path}")
    for slice_data in data["slices"]:
        architecture = slice_data["architecture"]
        for member in slice_data["members"]:
            member_type = member["type"]
            if member_type == "archive-metadata":
                continue
            if member_type != "mach-o":
                raise SystemExit(
                    f"unexpected non-Mach-O member: {path}: {architecture}/{member['name']}"
                )
            if member["platform"] != expected_platform:
                raise SystemExit(
                    f"wrong platform {member['platform']} in {path}: {architecture}/{member['name']}"
                )
            if member["minos"] != deployment:
                raise SystemExit(
                    f"wrong minimum OS {member['minos']} in {path}: expected {deployment}"
                )
            if member["architectures"] != [architecture]:
                raise SystemExit(
                    f"member architecture mismatch in {path}: {architecture}/{member['name']}"
                )
    return data


def dependency_for_component(lock: dict, component: str) -> dict:
    for dependency in lock["dependencies"]:
        if dependency.get("component") == component and dependency["name"] in COMPONENTS + ("tvStubs-source",):
            return dependency
    raise KeyError(component)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", required=True, type=pathlib.Path)
    parser.add_argument("--build-dir", required=True, type=pathlib.Path)
    parser.add_argument("--output-dir", required=True, type=pathlib.Path)
    parser.add_argument("--deployment-target", required=True)
    parser.add_argument("--compare-manifest", type=pathlib.Path)
    args = parser.parse_args()

    repo = args.repo_root.resolve()
    build = args.build_dir.resolve()
    output = args.output_dir.resolve()
    reports = output / "verification"
    reports.mkdir(parents=True, exist_ok=True)

    lock = json.load((repo / "native/tvos-dependencies.lock.json").open(encoding="utf-8"))
    locked_expectations = json.load((repo / "native/tvos-symbol-expectations.json").open(encoding="utf-8"))
    generated_expectations = json.load((build / "generated-symbol-expectations.json").open(encoding="utf-8"))
    if locked_expectations != generated_expectations:
        raise SystemExit("generated symbol expectations differ from the tracked pinned-binding manifest")

    sdk_versions = {
        "device": run(["xcrun", "--sdk", "appletvos", "--show-sdk-version"]).strip(),
        "simulator": run(["xcrun", "--sdk", "appletvsimulator", "--show-sdk-version"]).strip(),
    }
    source_state = json.load((build / "source-state.json").open(encoding="utf-8"))
    state_revisions = {d["name"]: d["revision"] for d in source_state["dependencies"]}

    artifacts: dict[str, dict] = {}
    exports: dict[str, dict[str, dict[str, set[str]]]] = {"device": {}, "simulator": {}}
    probe_archives: dict[str, dict[str, pathlib.Path]] = {"device": {}, "simulator": {}}

    for component in COMPONENTS:
        xcframework = output / f"{component}.xcframework"
        info_path = xcframework / "Info.plist"
        if not info_path.is_file():
            raise SystemExit(f"missing XCFramework Info.plist: {info_path}")
        with info_path.open("rb") as stream:
            info = plistlib.load(stream)
        libraries = info.get("AvailableLibraries", [])
        if len(libraries) != 2:
            raise SystemExit(f"{component} XCFramework must contain exactly two platform variants")

        variants: dict[str, dict] = {}
        for library in libraries:
            if library.get("SupportedPlatform") != "tvos":
                raise SystemExit(f"non-tvOS slice in {component}: {library}")
            variant = "simulator" if library.get("SupportedPlatformVariant") == "simulator" else "device"
            if variant in variants:
                raise SystemExit(f"duplicate {variant} slice in {component}")
            identifier = library["LibraryIdentifier"]
            archive = xcframework / identifier / library["LibraryPath"]
            if not archive.is_file():
                raise SystemExit(f"missing packaged archive: {archive}")
            if not (xcframework / identifier / library["HeadersPath"]).is_dir():
                raise SystemExit(f"missing packaged headers: {component}/{identifier}")

            staged = build / "stage" / component / variant / f"lib{component}.a"
            if not staged.is_file() or sha256(staged) != sha256(archive):
                raise SystemExit(f"packaged archive differs from staged build: {component}/{variant}")

            inspection_path = reports / f"{component}-{variant}-archive.json"
            run(
                [
                    sys.executable,
                    str(repo / "scripts/inspect-apple-archive.py"),
                    str(archive),
                    "--output",
                    str(inspection_path),
                ]
            )
            expected_platform = "TVOSSIMULATOR" if variant == "simulator" else "TVOS"
            inspection = validate_inspection(inspection_path, expected_platform, args.deployment_target)
            expected_archs = ["arm64", "x86_64"] if variant == "simulator" else ["arm64"]
            if component == "MoltenVK" and variant == "device":
                expected_archs.append("arm64e")
            if sorted(inspection["architectures"]) != sorted(expected_archs):
                raise SystemExit(f"{component}/{variant}: packaged architecture set differs from recipe")
            if sorted(library["SupportedArchitectures"]) != sorted(inspection["architectures"]):
                raise SystemExit(f"{component}/{variant}: XCFramework architecture metadata differs")
            by_arch = collect_exports(archive, inspection["architectures"])
            # The accepted logical fingerprint is arm64 on every build host.
            # Additional per-slice evidence stays outside the normalized format.
            symbols = sorted(by_arch["arm64"])
            (reports / f"{component}-{variant}-exports.txt").write_text(
                "\n".join(symbols) + "\n", encoding="utf-8"
            )
            (reports / f"{component}-{variant}-exports-by-architecture.json").write_text(
                json.dumps({a: sorted(v) for a, v in by_arch.items()}, indent=2, sort_keys=True) + "\n"
            )
            exports[variant][component] = by_arch
            probe_archives[variant][component] = archive

            member_fingerprint = []
            metadata_members = 0
            platforms: set[str] = set()
            minimums: set[str] = set()
            sdks: set[str] = set()
            for slice_data in inspection["slices"]:
                for member in slice_data["members"]:
                    if member["type"] == "archive-metadata":
                        metadata_members += 1
                    elif member["type"] == "mach-o":
                        platforms.add(member["platform"])
                        minimums.add(member["minos"])
                        sdks.add(member["sdk"])
                    member_fingerprint.append(
                        [slice_data["architecture"], member["index"], member["name"], member["type"], member["sha256"]]
                    )
            variants[variant] = {
                "architectures": inspection["architectures"],
                "applePlatforms": sorted(platforms),
                "minimumDeploymentTargets": sorted(minimums),
                "objectSDKs": sorted(sdks),
                "installedSDK": sdk_versions[variant],
                "archiveSha256": sha256(archive),
                "archiveMetadataMembers": metadata_members,
                "memberFingerprint": member_fingerprint,
                "exportedSymbolCount": len(symbols),
                "exportedSymbolsSha256": canonical_sha(symbols),
                "xcframeworkLibraryIdentifier": identifier,
            }

        if set(variants) != {"device", "simulator"}:
            raise SystemExit(f"device/simulator variant set incomplete for {component}")
        dependency = dependency_for_component(lock, component)
        patches = dependency.get("patches", [])
        patch_hashes = {path: sha256(repo / path) for path in patches}
        logical_variants = {}
        for variant, values in variants.items():
            logical_values = {
                key: value
                for key, value in values.items()
                if key not in ("archiveSha256", "xcframeworkLibraryIdentifier")
            }
            # Xcode hashes the absolute source path into some archive member
            # names when identical basenames collide. Preserve the raw names
            # in manifest.json and the inspection reports, but remove only
            # that path-derived 32-hex suffix from the logical comparison.
            logical_values["memberFingerprint"] = [
                [
                    architecture,
                    index,
                    re.sub(r"-[0-9a-f]{32}(?=\.o$)", "-<PATH_HASH>", name),
                    member_type,
                    member_sha,
                ]
                for architecture, index, name, member_type, member_sha
                in values["memberFingerprint"]
            ]
            logical_variants[variant] = logical_values

        logical = {
            "component": component,
            "revision": dependency["revision"],
            "patches": patch_hashes,
            "deploymentTarget": args.deployment_target,
            "variants": logical_variants,
            "xcframework": {
                "formatVersion": info.get("XCFrameworkFormatVersion"),
                "libraries": sorted(
                    [
                        {
                            "architectures": item["SupportedArchitectures"],
                            "platform": item["SupportedPlatform"],
                            "variant": item.get("SupportedPlatformVariant", "device"),
                        }
                        for item in libraries
                    ],
                    key=lambda item: item["variant"],
                ),
            },
        }
        artifacts[component] = {
            "revision": dependency["revision"],
            "appliedPatches": patches,
            "patchSha256": patch_hashes,
            "xcframeworkPath": f"{component}.xcframework",
            "buildLogs": {
                "device": f"logs/build/{component}-device.log",
                "simulator": f"logs/build/{component}-simulator.log",
            },
            "variants": variants,
            "logicalSha256": canonical_sha(logical),
        }

    for variant in ("device", "simulator"):
        duplicates = validate_required_symbols(variant, exports[variant], locked_expectations)
        (reports / f"tvStubs-{variant}-duplicates.json").write_text(
            json.dumps(duplicates, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )

    # Force-load all six archives so link probes detect their complete undefined
    # and duplicate symbol sets instead of only the functions called by main.c.
    link_results: dict[str, dict] = {}
    for variant, sdk, triple in (
        ("device", "appletvos", f"arm64-apple-tvos{args.deployment_target}"),
        ("simulator", "appletvsimulator", f"arm64-apple-tvos{args.deployment_target}-simulator"),
    ):
        probe_dir = output / "link-probes" / variant
        probe_dir.mkdir(parents=True, exist_ok=True)
        executable = probe_dir / "tvos-native-link-probe"
        link_map = probe_dir / "tvos-native-link-probe.map"
        log = probe_dir / "link.log"
        sdk_path = run(["xcrun", "--sdk", sdk, "--show-sdk-path"]).strip()
        command = [
            "xcrun", "--sdk", sdk, "clang", "-target", triple, "-isysroot", sdk_path,
            str(repo / "native/tvos-link-probe/main.c"), "-o", str(executable),
            f"-Wl,-map,{link_map}", "-Wl,-no_adhoc_codesign",
        ]
        for component in COMPONENTS:
            command.extend(["-I", str(build / "stage" / component / "headers")])
            command.append(f"-Wl,-force_load,{probe_archives[variant][component]}")
        for framework in FRAMEWORKS:
            command.extend(["-framework", framework])
        command.append("-lc++")
        run(command, output=log)
        if not executable.is_file() or not link_map.is_file():
            raise SystemExit(f"{variant} link probe did not produce an executable and link map")
        vtool = run(["xcrun", "vtool", "-show-build", str(executable)])
        expected_platform = "TVOSSIMULATOR" if variant == "simulator" else "TVOS"
        if not re.search(rf"^\s*platform\s+{expected_platform}$", vtool, re.MULTILINE):
            raise SystemExit(f"wrong Mach-O platform in {variant} link probe")
        if not re.search(
            rf"^\s*minos\s+{re.escape(args.deployment_target)}$", vtool, re.MULTILINE
        ):
            raise SystemExit(f"wrong minimum OS in {variant} link probe")
        signature = subprocess.run(
            ["codesign", "--display", str(executable)],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            check=False,
        )
        if signature.returncode == 0:
            raise SystemExit(f"{variant} link probe was unexpectedly signed")
        link_results[variant] = {
            "architecture": run(["xcrun", "lipo", "-archs", str(executable)]).strip().split(),
            "platform": expected_platform,
            "sha256": sha256(executable),
            "linkMapSha256": sha256(link_map),
            "frameworks": list(FRAMEWORKS),
            "libraries": ["c++"],
            "signed": False,
        }

    licenses = output / "licenses"
    if licenses.exists():
        shutil.rmtree(licenses)
    licenses.mkdir(parents=True)
    license_inventory: list[dict[str, str]] = []
    for dependency in lock["dependencies"]:
        source = build / "sources" / dependency["path"]
        paths = dependency.get("licensePaths", [])
        if not paths:
            license_inventory.append(
                {
                    "dependency": dependency["name"],
                    "status": "No license file present in the locked upstream checkout; redistribution requires review.",
                }
            )
            continue
        for relative in paths:
            source_license = source / relative
            if not source_license.is_file():
                raise SystemExit(f"locked license disappeared: {dependency['name']}/{relative}")
            destination_name = re.sub(r"[^A-Za-z0-9_.-]", "_", f"{dependency['name']}--{relative}")
            destination = licenses / destination_name
            shutil.copyfile(source_license, destination)
            license_inventory.append(
                {
                    "dependency": dependency["name"],
                    "source": relative,
                    "bundlePath": f"licenses/{destination_name}",
                    "sha256": sha256(destination),
                    "status": "collected",
                }
            )
    (output / "licenses.json").write_text(
        json.dumps(license_inventory, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )

    normalized = {
        "schemaVersion": 1,
        "deploymentTarget": args.deployment_target,
        "dependencyRevisions": state_revisions,
        "components": {component: artifacts[component]["logicalSha256"] for component in COMPONENTS},
        "symbolExpectationsSha256": canonical_sha(locked_expectations),
        "linkProbeInterfaces": {
            variant: {
                "architecture": result["architecture"],
                "platform": result["platform"],
                "frameworks": result["frameworks"],
                "libraries": result["libraries"],
                "signed": result["signed"],
            }
            for variant, result in link_results.items()
        },
        "licenseInventorySha256": canonical_sha(license_inventory),
    }
    normalized["logicalSetSha256"] = canonical_sha(normalized)
    normalized_path = output / "normalized-manifest.json"
    normalized_path.write_text(json.dumps(normalized, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    manifest = {
        "schemaVersion": 1,
        "deploymentTarget": args.deployment_target,
        "dependencies": source_state["dependencies"],
        "components": artifacts,
        "symbolExpectationManifest": "native/tvos-symbol-expectations.json",
        "linkProbes": link_results,
        "licenseInventory": "licenses.json",
        "normalizedManifest": "normalized-manifest.json",
        "logicalSetSha256": normalized["logicalSetSha256"],
    }
    (output / "manifest.json").write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )

    if args.compare_manifest:
        previous = json.load(args.compare_manifest.open(encoding="utf-8"))
        if previous != normalized:
            raise SystemExit(
                f"normalized reproducibility mismatch: {args.compare_manifest} != {normalized_path}"
            )
        print(f"Normalized reproducibility match: {normalized['logicalSetSha256']}")

    print(f"Verified six tvOS device and simulator XCFrameworks: {output}")
    print(f"Normalized logical checksum: {normalized['logicalSetSha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
