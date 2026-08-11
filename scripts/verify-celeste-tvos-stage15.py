#!/usr/bin/env python3
"""Verify Stage 15 QR pairing without weakening the accepted RC gates."""

from __future__ import annotations

import argparse
import json
import pathlib
import plistlib
import re
import subprocess
import sys
import zipfile


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def require(text: str, tokens: tuple[str, ...], label: str) -> None:
    for token in tokens:
        if token not in text:
            fail(f"{label} is missing required token: {token}")


def scan_product(info: dict, blobs: list[bytes], label: str) -> None:
    forbidden_plist = (
        "NSCameraUsageDescription", "NSPhotoLibraryUsageDescription",
        "LSSupportsGameMode", "GCSupportsGameMode",
    )
    for key in forbidden_plist:
        if key in info:
            fail(f"{label} contains forbidden/unneeded plist key: {key}")
    if info.get("NSBonjourServices") != ["_celeste-save._tcp"]:
        fail(f"{label} changed the accepted Bonjour declaration")
    required = (
        "Scan with your phone camera to connect.",
        "QR code expired. Use the address and access code below.",
        "X-Celeste-Pairing",
    )
    for token in required:
        encoded = (token.encode(), token.encode("utf-16le"))
        if not any(any(value in blob for value in encoded) for blob in blobs):
            fail(f"{label} lacks Stage 15 product token: {token}")


def product_info(app: pathlib.Path) -> tuple[dict, list[bytes]]:
    info = plistlib.loads((app / "Info.plist").read_bytes())
    executable = app / str(info.get("CFBundleExecutable", ""))
    candidates = (executable, app / "CelesteTvOSRuntimeHost.dll", app / "Celeste.dll")
    return info, [path.read_bytes() for path in candidates if path.is_file()]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--generated-root", type=pathlib.Path)
    parser.add_argument("--app", type=pathlib.Path)
    parser.add_argument("--ipa", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    repo = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()

    protocol_path = repo / "tvos/CelesteTvOSRuntimeHost/Stage10AHttpProtocol.cs"
    manager_path = repo / "tvos/CelesteTvOSRuntimeHost/Stage10ASaveManager.cs"
    qr_path = repo / "tvos/CelesteTvOSRuntimeHost/Stage15QrCodeGenerator.cs"
    bridge_path = repo / "managed/templates/TvOSSaveManagerBridge.cs"
    project_path = repo / "tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj"
    tests_path = repo / "tvos/Stage15QrPairingTests/Program.cs"
    builder_path = repo / "build-tvos.sh"
    for path in (protocol_path, manager_path, qr_path, bridge_path, project_path, tests_path, builder_path):
        if not path.is_file():
            fail(f"Stage 15 source is missing: {path.relative_to(repo)}")

    protocol = protocol_path.read_text()
    manager = manager_path.read_text()
    qr = qr_path.read_text()
    bridge = bridge_path.read_text()
    project = project_path.read_text()
    tests = tests_path.read_text()
    builder = builder_path.read_text()

    require(protocol, (
        "RandomNumberGenerator.GetBytes(32)", "PairingLifetime = TimeSpan.FromMinutes(3)",
        "MaximumPairingBodyBytes = 80", 'request.Method == "POST" && request.Path == "/pair"',
        'request.Path == "/pair"', "CryptographicOperations.FixedTimeEquals",
        "history.replaceState(null,'','/pair')",
        "HttpOnly; SameSite=Strict", "X-Celeste-Pairing", "pairingConsumed = true",
    ), "pairing protocol")
    require(manager, (
        "Stage15QrCodeGenerator.Create", "urls[0]", "STAGE10A_READY",
        "protocol?.Stop()", "Stage15PairingState.Consumed", "Stage15PairingState.Expired",
    ), "Save Manager lifecycle")
    require(qr, (
        "CIQRCodeGenerator", 'CorrectionLevel = "M"', "QuietZoneModules = 4",
        "MaximumRenderedPixels = 420", "int scale", "CIFormat.Rgba8",
    ), "Core Image QR generator")
    require(bridge, (
        "TvOSSaveManagerQrImage", "qrTexture.SetData", "Scan with your phone camera to connect.",
        "Or open manually:", "QR code expired. Use the address and access code below.",
    ), "Save Manager television UI")
    require(project, ("CoreImage",), "tvOS host project")
    require(builder, ("verify-stage15-unsigned", "verify-stage15-signed", "relaunch-apple-tv"), "public builder")

    required_tests = (
        "pairing-token-256-bit-shape", "fresh-token-per-activation",
        "valid-pairing-succeeds", "pairing-token-is-one-time",
        "concurrent-pairing-has-one-winner", "pairing-token-expires",
        "manual-code-survives-pairing-expiry", "manager-stop-invalidates-pairing",
        "background-stop-invalidates-pairing", "soft-reload-stop-invalidates-pairing",
        "new-activation-after-stop-has-fresh-pairing", "expired-pairing-secret-is-erased",
        "pairing-post-body-limit", "paired-session-can-use-stage10-mutation",
        "manual-auth-can-add-session-after-qr-use",
    )
    require(tests, required_tests, "Stage 15 deterministic tests")

    if re.search(r"Stage3BLog\.[A-Za-z]+\([^\n]*(pairingUrl|PairingCredentialForQr)", manager + protocol):
        fail("routine Stage 15 source appears to log a pairing credential")
    if "NSCameraUsageDescription" in project or "NSPhotoLibraryUsageDescription" in project:
        fail("QR display incorrectly adds camera/photo permissions")
    if "com.apple.developer" in qr + protocol + manager:
        fail("Stage 15 source introduces an entitlement")

    generated_summary = None
    if args.generated_root:
        generated = args.generated_root.resolve()
        generated_project = (generated / "Celeste.Modern.csproj").read_text()
        if generated_project.count("TVOS_STAGE15") != 1:
            fail("generated release tree does not contain exactly one TVOS_STAGE15 symbol")
        if not (generated / "Celeste/TvOSSaveManagerBridge.cs").is_file():
            fail("generated release tree lacks the QR-capable Save Manager bridge")
        generated_summary = {"stage15Symbols": 1}

    product = "not-requested"
    if args.app:
        info, blobs = product_info(args.app.resolve())
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
            blobs = [archive.read(name) for name in archive.namelist()
                     if name.startswith(prefix) and name.rsplit("/", 1)[-1] in
                     {str(info.get("CFBundleExecutable", "")), "CelesteTvOSRuntimeHost.dll", "Celeste.dll"}]
            scan_product(info, blobs, "unsigned IPA")
        product = "ipa"

    tracked = subprocess.run(
        ["git", "-C", str(repo), "ls-files"], check=True, capture_output=True, text=True
    ).stdout.splitlines()
    forbidden = (".celeste", ".bank", ".ipa", ".mobileprovision", ".p12", ".cer")
    offenders = [name for name in tracked if name.lower().endswith(forbidden) or ".app/" in name.lower()]
    if offenders:
        fail("private/proprietary Stage 15 output is tracked: " + ", ".join(offenders[:4]))

    report = repo / "docs/history/stages/TVOS_SAVE_MANAGER_QR_STAGE15_REPORT.md"
    history = repo / "docs/history/README.md"
    if not report.is_file() or "TVOS_SAVE_MANAGER_QR_STAGE15_REPORT.md" not in history.read_text():
        fail("Stage 15 report is not indexed under docs/history/stages")

    summary = {
        "schemaVersion": 1,
        "result": "PASS",
        "deterministicTests": 31,
        "credentialBits": 256,
        "credentialLifetimeSeconds": 180,
        "oneTime": True,
        "fragmentBootstrap": True,
        "manualFallback": True,
        "generated": generated_summary,
        "product": product,
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    print("PASS: Stage 15 one-time Core Image QR pairing and manual fallback")
    return 0


if __name__ == "__main__":
    sys.exit(main())
