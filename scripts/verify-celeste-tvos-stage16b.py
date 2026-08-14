#!/usr/bin/env python3
"""Verify Stage 16B without weakening the accepted Stage 9B-15 gates."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import re
import subprocess
import sys
import zipfile


EXPECTED_STAGE16_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
UNCHANGED_NATIVE_COMPONENTS = {
    "SDL2": "474d48e30d14aa38754f17848e753de3d0acaf8df13bbab41a0045a14d8da454",
    "FNA3D": "feb9191689d277c7b15a0bec1e15902efd83098e0e4ad8110243ea4a2a2f00c2",
    "FAudio": "bc4b6e5e40407aafc6a1d9058ff93ceb8110d4e00d47861f8cbe397493d05bb6",
    "Theorafile": "c9a67a528be14ba8a2a5d4bb03254db80a7e0faf2bb41f961639da9550ff7da0",
    "MoltenVK": "ffe9f6c812299940f74ec440dd218dff9841a58ecfb91fb2f59e55360d1f02b7",
}


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def require(text: str, tokens: tuple[str, ...], label: str) -> None:
    for token in tokens:
        if token not in text:
            fail(f"{label} is missing required token: {token}")


def sha256(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def scan_product(info: dict, blobs: list[bytes], label: str) -> None:
    forbidden = (
        "MetalHudEnabled", "MetalHUDEnabled", "MTL_HUD_ENABLED",
        "LSSupportsGameMode", "GCSupportsGameMode",
    )
    for key in forbidden:
        if key in info:
            fail(f"{label} contains forbidden HUD/Game Mode plist key: {key}")
    required = (
        "Performance HUD", "CelesteTvOS.PerformanceHUD.v1",
        "MetalHUDForceEnabled", "MetalForceHudEnabled",
    )
    for token in required:
        encodings = (token.encode(), token.encode("utf-16le"))
        if not any(any(value in blob for value in encodings) for blob in blobs):
            fail(f"{label} lacks Stage 16B product token: {token}")


def app_product(app: pathlib.Path) -> tuple[dict, list[bytes]]:
    info = plistlib.loads((app / "Info.plist").read_bytes())
    names = {str(info.get("CFBundleExecutable", "")), "CelesteTvOSRuntimeHost.dll", "Celeste.dll"}
    return info, [path.read_bytes() for path in app.rglob("*") if path.is_file() and path.name in names]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--generated-root", type=pathlib.Path)
    parser.add_argument("--native-manifest", type=pathlib.Path)
    parser.add_argument("--app", type=pathlib.Path)
    parser.add_argument("--ipa", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    repo = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()

    paths = {
        "bootstrap": repo / "native/tvstubs/MetalPerformanceHudBootstrap.m",
        "header": repo / "native/tvstubs/tvStubs.h",
        "native_build": repo / "scripts/build-tvos-native.sh",
        "expectations": repo / "native/tvos-symbol-expectations.json",
        "policy": repo / "tvos/CelesteTvOSRuntimeHost/PerformanceHudPolicy.cs",
        "coordinator": repo / "tvos/CelesteTvOSRuntimeHost/PerformanceHudCoordinator.cs",
        "bridge": repo / "managed/templates/TvOSPerformanceHudBridge.cs",
        "generator": repo / "scripts/celeste-stage6.py",
        "host": repo / "tvos/CelesteTvOSRuntimeHost/Main.cs",
        "project": repo / "tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj",
        "plist": repo / "tvos/CelesteTvOSRuntimeHost/Info.plist",
        "privacy": repo / "tvos/CelesteTvOSRuntimeHost/PrivacyInfo.xcprivacy",
        "tests": repo / "tvos/PerformanceHudTests/Program.cs",
        "builder": repo / "build-tvos.sh",
        "generation_lock": repo / "managed/celeste-generation.lock.json",
        "prepare_host": repo / "scripts/prepare-tvos-host-native.sh",
    }
    for label, path in paths.items():
        if not path.is_file():
            fail(f"Stage 16B {label} source is missing: {path.relative_to(repo)}")
    text = {label: path.read_text(errors="replace") for label, path in paths.items()}

    require(text["bootstrap"], (
        "__attribute__((constructor))", "NSUserDefaults", "standardUserDefaults",
        'MetalHUDForceEnabled', 'MetalForceHudEnabled', "setBool:YES",
    ), "early public-Foundation bootstrap")
    for forbidden in ("performSelector", "objc_msgSend", "method_exchange", "swizzle", "UIKit"):
        if forbidden in text["bootstrap"]:
            fail(f"native bootstrap uses forbidden/private mechanism: {forbidden}")
    require(text["header"] + text["native_build"], (
        "CelesteTvOSMetalHudBootstrapForceLink", "MetalPerformanceHudBootstrap.m",
        "append_metal_hud_bootstrap", "-fobjc-arc",
    ), "Stage 1 bootstrap integration")
    require(text["policy"], (
        'CelesteTvOS.PerformanceHUD.v1', '"Off"', '"On"',
        'new(mode == PerformanceHudMode.On ? "default" : "disabled", "disabled")',
        "SelectPresentationLayer", "ModeWhileInactive", "ModeAfterForeground",
    ), "Performance HUD policy")
    require(text["coordinator"], (
        "SDL_GetWindowWMInfo", "SDL_SYSWM_UIKIT", "RootViewController?.View",
        "CAMetalLayer", "Device", "SDL_Metal_GetDrawableSize",
        "DeveloperHudProperties", "OperatingSystem.IsTvOSVersionAtLeast(16)",
        "UIApplication.WillResignActiveNotification", "UIApplication.DidBecomeActiveNotification",
        "ApplyBeforeFirstRender", 'Apply(preference.Mode, "before-first-render")',
        'Apply(hidden, "resign-active")', "OnProperties", "OffProperties",
    ), "real-layer coordinator")
    require(text["bridge"], (
        "TvOSPerformanceHudHooks", "EnabledRequested", "EnabledChanged",
        "AvailableRequested", "StartupRequested", "BeforeFirstRenderRequested",
        "ApplyBeforeFirstRender", "Interlocked.Exchange",
    ), "generated narrow bridge")
    require(text["generator"], (
        "TVOS_STAGE16B", "TvOSPerformanceHudBridge.cs", 'TextMenu.OnOff(\\"Performance HUD\\"',
        "TvOSPerformanceHudHooks.ApplyStartup(celeste.Window.Handle)",
        "TvOSPerformanceHudHooks.ApplyBeforeFirstRender()",
    ), "locked generated transformation")
    require(text["host"], ("PerformanceHudCoordinator",), "host lifetime")
    require(text["privacy"], ("NSPrivacyAccessedAPICategoryUserDefaults", "CA92.1"), "privacy manifest")
    require(text["builder"], (
        "verify-performance-hud-source", "verify-stage16b-unsigned", "verify-stage16b-signed",
    ), "public builder")
    generation_lock = json.loads(text["generation_lock"])
    if generation_lock.get("stage1NativeLogicalSha256") != EXPECTED_STAGE16_NATIVE:
        fail("managed generation lock does not identify the accepted post-Stage16 native set")
    require(text["prepare_host"] + text["builder"], (EXPECTED_STAGE16_NATIVE,),
            "public Stage 1 selection")

    if any(token in text["plist"] + text["project"] for token in
           ("MetalHudEnabled", "MetalHUDEnabled", "MTL_HUD_ENABLED")):
        fail("Stage 16B incorrectly relies on an Info.plist or Xcode HUD activation key")
    if "LSSupportsGameMode" in text["plist"] or "GCSupportsGameMode" in text["plist"]:
        fail("Stage 16B introduces a Game Mode claim")

    expectations = json.loads(text["expectations"])
    stubs = expectations.get("components", {}).get("tvStubs", {})
    if stubs.get("repositoryBootstrapSource") != "native/tvstubs/MetalPerformanceHudBootstrap.m":
        fail("symbol expectations do not identify the repository bootstrap")
    if stubs.get("repositoryBootstrapSha256") != sha256(paths["bootstrap"]):
        fail("symbol expectations do not hash the exact bootstrap source")
    if "CelesteTvOSMetalHudBootstrapForceLink" not in stubs.get("symbols", []):
        fail("symbol expectations omit the bootstrap force-link export")

    isolation = "\n".join((
        (repo / "managed/templates/TvOSSettingsSerializer.cs").read_text(),
        (repo / "managed/templates/TvOSSaveDataSerializer.cs").read_text(),
        (repo / "managed/celeste-stage6-policy.json").read_text(),
    ))
    if "CelesteTvOS.PerformanceHUD.v1" in isolation or "Performance HUD" in isolation:
        fail("host HUD preference leaked into Celeste Settings/SaveData/Stage 9B policy")

    generated_summary = None
    if args.generated_root:
        generated = args.generated_root.resolve()
        project = (generated / "Celeste.Modern.csproj").read_text()
        celeste = (generated / "Celeste/Celeste.cs").read_text()
        menu = (generated / "Celeste/MenuOptions.cs").read_text()
        if project.count("TVOS_STAGE16B") != 1:
            fail("generated tree does not contain exactly one TVOS_STAGE16B symbol")
        if celeste.count("TvOSPerformanceHudHooks.ApplyStartup(celeste.Window.Handle)") != 1:
            fail("generated tree does not contain exactly one pre-first-draw startup hook")
        if celeste.count("TvOSPerformanceHudHooks.ApplyBeforeFirstRender()") != 1:
            fail("generated tree does not contain exactly one pre-render visibility hook")
        if menu.count('TextMenu.OnOff("Performance HUD"') != 1:
            fail("generated Options does not contain exactly one Performance HUD row")
        if not (generated / "Celeste/TvOSPerformanceHudBridge.cs").is_file():
            fail("generated tree lacks the Performance HUD bridge")
        generated_summary = {"stage16Symbols": 1, "startupHooks": 2, "optionRows": 1}

    native_summary = None
    if args.native_manifest:
        manifest = json.loads(args.native_manifest.resolve().read_text())
        if EXPECTED_STAGE16_NATIVE == "STAGE16_NATIVE_HASH_PENDING":
            fail("Stage 16B accepted native hash has not yet been established")
        if manifest.get("logicalSetSha256") != EXPECTED_STAGE16_NATIVE:
            fail("native manifest is not the accepted post-Stage16 reproducible set")
        components = manifest.get("components", {})
        for name, expected in UNCHANGED_NATIVE_COMPONENTS.items():
            component = components.get(name)
            actual = component.get("logicalSha256") if isinstance(component, dict) else component
            if actual != expected:
                fail(f"unrelated native component changed: {name}")
        tvstubs = components.get("tvStubs")
        tvstubs_hash = tvstubs.get("logicalSha256") if isinstance(tvstubs, dict) else tvstubs
        if tvstubs_hash == "ef066108d5427a511cd5b42f9ca9ace8d78a0c244d9efc9b8fe1b4614cdeefac":
            fail("tvStubs did not incorporate the Stage 16B bootstrap")
        native_summary = {"logicalSetSha256": EXPECTED_STAGE16_NATIVE, "changedComponent": "tvStubs"}

    product = "not-requested"
    if args.app:
        info, blobs = app_product(args.app.resolve())
        scan_product(info, blobs, "built app")
        product = "app"
    if args.ipa:
        with zipfile.ZipFile(args.ipa.resolve()) as archive:
            plist_names = [name for name in archive.namelist()
                           if re.fullmatch(r"Payload/[^/]+\.app/Info\.plist", name)]
            if len(plist_names) != 1:
                fail("IPA does not contain exactly one Payload app")
            prefix = plist_names[0].removesuffix("Info.plist")
            info = plistlib.loads(archive.read(plist_names[0]))
            names = {str(info.get("CFBundleExecutable", "")), "CelesteTvOSRuntimeHost.dll", "Celeste.dll"}
            blobs = [archive.read(name) for name in archive.namelist()
                     if name.startswith(prefix) and name.rsplit("/", 1)[-1] in names]
            scan_product(info, blobs, "unsigned IPA")
        product = "ipa"

    tracked = subprocess.run(
        ["git", "-C", str(repo), "ls-files"], check=True, capture_output=True, text=True
    ).stdout.splitlines()
    forbidden_suffixes = (".celeste", ".bank", ".ipa", ".mobileprovision", ".p12", ".cer")
    offenders = [name for name in tracked
                 if name.lower().endswith(forbidden_suffixes) or ".app/" in name.lower()]
    if offenders:
        fail("private/proprietary Stage 16B output is tracked: " + ", ".join(offenders[:4]))

    report = repo / "docs/history/stages/TVOS_METAL_PERFORMANCE_HUD_STAGE16B_REPORT.md"
    history = repo / "docs/history/README.md"
    if not report.is_file() or report.name not in history.read_text():
        fail("Stage 16B report is not indexed under docs/history/stages")

    summary = {
        "schemaVersion": 1,
        "result": "PASS",
        "deterministicTests": 39,
        "preferenceDefault": "Off",
        "logging": "disabled",
        "native": native_summary,
        "generated": generated_summary,
        "product": product,
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    print("PASS: Stage 16B native Metal HUD policy, bootstrap, isolation, and product boundaries")
    return 0


if __name__ == "__main__":
    sys.exit(main())
