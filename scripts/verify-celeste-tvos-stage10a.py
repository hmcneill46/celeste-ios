#!/usr/bin/env python3
"""Verify the preserved Stage 10A network/security boundary and an optional app."""

from __future__ import annotations

import argparse
import json
import pathlib
import plistlib
import re
import subprocess
import sys


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def require(text: str, tokens: tuple[str, ...], label: str) -> None:
    for token in tokens:
        if token not in text:
            fail(f"{label} is missing required token: {token}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify the Stage 10A tvOS Save Manager foundation")
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--app", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    repo = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()

    protocol = (repo / "tvos/CelesteTvOSRuntimeHost/Stage10AHttpProtocol.cs").read_text()
    manager = (repo / "tvos/CelesteTvOSRuntimeHost/Stage10ASaveManager.cs").read_text()
    lan_policy = (repo / "tvos/CelesteTvOSRuntimeHost/Stage10ALanAddressPolicy.cs").read_text()
    store = (repo / "tvos/CelesteTvOSRuntimeHost/Stage6PersistenceStore.cs").read_text()
    bridge = (repo / "managed/templates/TvOSSaveManagerBridge.cs").read_text()
    transform = (repo / "scripts/celeste-stage6.py").read_text()
    project = (repo / "tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj").read_text()
    info = plistlib.loads((repo / "tvos/CelesteTvOSRuntimeHost/Info.plist").read_bytes())
    entitlements = plistlib.loads((repo / "tvos/CelesteTvOSRuntimeHost/Entitlements.plist").read_bytes())

    require(protocol, (
        "MaximumRequestLineBytes = 2048", "MaximumHeaderBytes = 16 * 1024",
        "MaximumHeaderCount = 48", "MaximumAuthBodyBytes = 64", "MaximumConcurrentConnections = 4",
        "RequestLifetime = TimeSpan.FromSeconds(10)",
        "SessionLifetime = TimeSpan.FromMinutes(10)", "RandomNumberGenerator.GetInt32",
        "CryptographicOperations.FixedTimeEquals", "HttpOnly; SameSite=Strict",
        '"/download/all"', '"/download/settings"', '"/download/0"', '"/download/1"', '"/download/2"',
        'request.Method == "POST" && request.Path == "/auth"',
        'headers.ContainsKey("transfer-encoding")', "X-Celeste-Logical-Name", "X-Celeste-Content-SHA256",
        '"X-Celeste-Authentication"] = "accepted"', "Download backup (.zip)",
    ), "HTTP protocol")
    require(manager, (
        'BonjourServiceType = "_celeste-save._tcp"', "Stage10AHttpProtocol.MaximumConcurrentConnections",
        "Stage10AHttpProtocol.RequestLifetime", "ManagerInactivityLifetime = TimeSpan.FromMinutes(12)",
        "Stage10AConnectionPolicy.ResponseCloseLifetime", "response-close-fallback",
        "NWListener.Create(parameters)", "case NWListenerState.Ready:", "ushort port = source.Port",
        "DiscoverLanAddresses", "GetIfAddrs(out head)", 'DllImport("__Internal", EntryPoint = "getifaddrs"',
        'Observe(UIApplication.WillResignActiveNotification, "resign-active")',
        'Observe(UIApplication.DidEnterBackgroundNotification, "background")',
        "UIApplication.SharedApplication.IdleTimerDisabled = suppressed",
        "SetIdleTimerSuppressed(false)",
        "STAGE10A_IDLE_TIMER suppressed=",
        'STAGE10A_DORMANT listener=false; bonjour=false; activation=explicit-options-only',
    ), "listener")
    if "nextListener.ConnectionLimit" in manager or ".ConnectionLimit =" in manager:
        fail("the physical tvOS lifetime NWListener connection limit was reintroduced")
    require(protocol, ("ResponseCloseLifetime = TimeSpan.FromSeconds(5)",), "connection lifecycle policy")
    require(lan_policy, ("ReadDarwinSockAddr", "IsExcludedInterface", "IsUsableIpv4", "FormatUrl"), "LAN address policy")
    if "NetworkInterface.GetAllNetworkInterfaces" in manager + lan_policy:
        fail("the physical tvOS-incompatible NetworkInformation enumeration was reintroduced")
    require(store, (
        "CreateReadOnlySaveManagerSnapshot", 'Commit("save-manager-read-snapshot")',
        "ExportLogicalPayloadForFutureSaveManager", 'new[] { "settings", "0", "1", "2" }',
    ), "persistence export boundary")
    require(bridge, (": Entity", "TvOSSaveManagerHooks.Start()", 'TvOSSaveManagerHooks.Stop("ui-back")'), "TV UI bridge")
    if ": Oui" in bridge or "class TvOSSaveManagerUI : Oui" in bridge:
        fail("Stage 10A unexpectedly added a new Oui subtype")
    require(transform, ('new TextMenu.Button(\\"Save Manager\\").Pressed(OpenSaveManager)', "TVOS_STAGE10A"), "managed transform")
    if "new Oui[11]" in transform:
        fail("the exact AOT-safe Oui factory was expanded unexpectedly")
    require(project, ("<Frameworks>Foundation UIKit", " Metal Network OpenGLES", "TvOSSaveManagerBridge.cs"), "tvOS project")

    expected_service = ["_celeste-save._tcp"]
    if info.get("NSBonjourServices") != expected_service:
        fail("Info.plist Bonjour service declaration differs from the locked service type")
    if not isinstance(info.get("NSLocalNetworkUsageDescription"), str) or "Save Manager" not in info["NSLocalNetworkUsageDescription"]:
        fail("Info.plist lacks the focused Save Manager local-network explanation")
    forbidden_entitlements = {
        "com.apple.developer.user-management", "com.apple.developer.networking.multicast",
        "com.apple.developer.icloud-container-identifiers", "com.apple.security.application-groups",
    }
    if forbidden_entitlements.intersection(entitlements):
        fail("a paid, user-management, iCloud, App Group, or multicast entitlement was introduced")

    save_manager_sources = protocol + manager
    for forbidden in (
        "CelesteTvOS.Persistence.v1.A", "CelesteTvOS.Persistence.v1.B",
        "DataForKey", "StandardUserDefaults", "TVUserManager", "TcpListener",
    ):
        if forbidden in save_manager_sources:
            fail(f"Save Manager bypasses its narrow boundary: {forbidden}")
    if re.search(r"https?://(?:192\.168|10\.|172\.(?:1[6-9]|2\d|3[01]))", manager):
        fail("a private LAN address was hard-coded")

    app_summary: dict[str, object] | None = None
    if args.app:
        app = args.app.resolve()
        if not (app / "Info.plist").is_file():
            fail("--app does not name a built .app bundle")
        built_info = plistlib.loads((app / "Info.plist").read_bytes())
        if built_info.get("NSBonjourServices") != expected_service:
            fail("built app is missing the Bonjour service declaration")
        if "Save Manager" not in str(built_info.get("NSLocalNetworkUsageDescription", "")):
            fail("built app is missing the local-network explanation")
        executable = app / str(built_info.get("CFBundleExecutable", ""))
        if not executable.is_file():
            fail("built app executable is missing")
        token_files = [executable]
        managed_host = app / "CelesteTvOSRuntimeHost.dll"
        if managed_host.is_file():
            token_files.append(managed_host)
        for token in ("STAGE10A_DORMANT", "STAGE10A_READY", "_celeste-save._tcp", "Celeste Save Manager"):
            encoded = (token.encode("ascii"), token.encode("utf-16le"))
            if not any(any(item in candidate.read_bytes() for item in encoded) for candidate in token_files):
                fail(f"built app executable lacks Stage 10A token: {token}")
        automation_token = b"STAGE10A_AUTOMATION_READY"
        automation_utf16 = "STAGE10A_AUTOMATION_READY".encode("utf-16le")
        if any(automation_token in candidate.read_bytes() or automation_utf16 in candidate.read_bytes() for candidate in token_files):
            fail("production app unexpectedly contains the local-only Stage 10A automation lane")
        app_summary = {"path": "<APP>", "bonjour": expected_service, "stage10aTokens": "PASS"}

    tracked = subprocess.run(["git", "-C", str(repo), "ls-files"], check=True, capture_output=True, text=True).stdout.splitlines()
    forbidden_suffixes = (".celeste", ".bank", ".ipa", ".mobileprovision")
    offenders = [name for name in tracked if name.lower().endswith(forbidden_suffixes)]
    if offenders:
        fail("proprietary/private output is tracked: " + ", ".join(offenders[:4]))

    summary = {
        "schemaVersion": 1,
        "result": "PASS",
        "newOui": False,
        "routes": ["/", "/auth", "/download/all", "/download/settings", "/download/0", "/download/1", "/download/2"],
        "limits": {"requestLine": 2048, "headers": 16384, "headerCount": 48, "connections": 4, "requestSeconds": 10},
        "bonjourType": "_celeste-save._tcp",
        "app": app_summary,
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    print("PASS: Stage 10A network, bounded HTTP, authentication, lifecycle, and narrow export boundary remain intact")
    if app_summary:
        print("PASS: built app contains the Stage 10A listener and required Info.plist declarations")
    return 0


if __name__ == "__main__":
    sys.exit(main())
