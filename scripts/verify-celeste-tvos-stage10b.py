#!/usr/bin/env python3
"""Verify the tracked writable Save Manager boundary and an optional built app."""

from __future__ import annotations

import argparse
import json
import pathlib
import plistlib
import subprocess
import sys


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def require(text: str, tokens: tuple[str, ...], label: str) -> None:
    for token in tokens:
        if token not in text:
            fail(f"{label} is missing required token: {token}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify the writable tvOS Save Manager")
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--app", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    repo = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()

    protocol = (repo / "tvos/CelesteTvOSRuntimeHost/Stage10AHttpProtocol.cs").read_text()
    manager = (repo / "tvos/CelesteTvOSRuntimeHost/Stage10ASaveManager.cs").read_text()
    store = (repo / "tvos/CelesteTvOSRuntimeHost/Stage6PersistenceStore.cs").read_text()
    diagnostic = (repo / "tvos/CelesteTvOSRuntimeHost/Stage6PersistenceDiagnostic.cs").read_text()
    bridge = (repo / "managed/templates/TvOSSaveManagerBridge.cs").read_text()
    tests = (repo / "tvos/Stage10AProtocolTests/Program.cs").read_text()
    policy = json.loads((repo / "managed/celeste-stage6-policy.json").read_text())
    info = plistlib.loads((repo / "tvos/CelesteTvOSRuntimeHost/Info.plist").read_bytes())
    entitlements = plistlib.loads((repo / "tvos/CelesteTvOSRuntimeHost/Entitlements.plist").read_bytes())

    replace_routes = [f"/replace/{name}" for name in ("settings", "0", "1", "2")]
    delete_routes = [f"/delete/{name}" for name in ("0", "1", "2")]
    for route in replace_routes + delete_routes + ["/reset/settings"]:
        if f'"{route}"' not in protocol:
            fail(f"fixed mutation route is missing: {route}")

    require(protocol, (
        "MaximumSettingsUploadBytes = 64 * 1024", "MaximumSaveUploadBytes = 256 * 1024",
        "CsrfHeaderName", "RevisionHeaderName", "HMACSHA256.HashData",
        "CryptographicOperations.FixedTimeEquals", "application/octet-stream",
        "Content-Length", "transfer-encoding", "connect-src 'self'", "location.replace('/')",
        "Restart Celeste before continuing", "mutator(new Stage10BMutationCommand",
        "The save changed since this page was opened", "The durable write could not be verified",
    ), "writable HTTP protocol")
    require(store, (
        "MutateFromSaveManager", "ValidatePayload(policy, acceptedPayload)",
        "Snapshot.Create", "Encode(candidate, FormatVersion)", "ChooseTarget(a, b)",
        "ValidateBridgeLength", "defaults.SetValueForKey", "defaults.Synchronize()",
        "ReadCandidate(target)", "readBack.Snapshot.LogicalHash != candidate.LogicalHash",
        "externalMutationRequiresRestart = true", "STAGE10B_STALE_COMMIT_SUPPRESSED",
    ), "persistence mutation boundary")
    require(manager, (
        "mutationHandler: persistence.MutateFromSaveManager", "current.RestartRequired",
        "NextReceiveMaximum", "InspectRequestProgress", "restart-required",
    ), "listener mutation integration")
    require(bridge, (
        "RestartRequired", "ui-restart-required", "This screen intentionally blocks the stale running game",
    ), "restart-required TV UI")
    if ": Oui" in bridge or "class TvOSSaveManagerUI : Oui" in bridge:
        fail("Stage 10B unexpectedly added a new Oui subtype")

    required_test_names = (
        "unauthenticated-replace-rejected", "invalid-csrf-rejected",
        "oversized-upload-rejected-from-headers", "exact-save-replacement-roundtrip",
        "exact-settings-replacement-roundtrip", "failed-replacement-preserves-previous",
        "delete-populated-slot", "settings-reset-semantics", "stale-revision-conflict",
        "two-stale-concurrent-mutations-conflict", "zip-download-reflects-replacement",
        "restart-required-only-after-success", "request-pipelining-rejected",
        "browser-mutation-csp-allows-same-origin-fetch", "browser-mutation-refresh-uses-get-root",
    )
    require(tests, required_test_names, "protocol tests")
    require(diagnostic, (
        "SaveManagerMutationSuite", "save-manager-replace-preserves-exact-bytes",
        "save-manager-stale-runtime-commit-suppressed", "save-manager-corrupt-newest-falls-back-to-prior-generation",
        "save-manager-concurrent-stale-mutation-conflict",
    ), "persistence mutation diagnostics")

    storage = policy.get("storage", {})
    writable = {entry["logicalName"]: entry for entry in policy.get("writableFiles", [])}
    locked = {
        "hardCompressedEntryBudgetBytes": 98304,
        "hardEnvelopeBudgetBytes": 126976,
        "hardTotalBudgetBytes": 262144,
    }
    for key, value in locked.items():
        if storage.get(key) != value:
            fail(f"Stage 9B limit changed unexpectedly: {key}")
    if writable.get("settings", {}).get("maximumPayloadBytes") != 65536:
        fail("Stage 9B Settings raw limit changed unexpectedly")
    if any(writable.get(name, {}).get("maximumPayloadBytes") != 262144 for name in ("0", "1", "2")):
        fail("Stage 9B SaveData raw limit changed unexpectedly")

    if any(token in protocol + manager for token in (
        "NSUserDefaults.StandardUserDefaults", "DataForKey", "SetValueForKey", "CelesteTvOS.Persistence.v1",
    )):
        fail("the network layer bypasses the Stage 9B persistence authority")
    if "ZipArchiveMode.Read" in protocol:
        fail("optional ZIP import was introduced before the mandatory individual operations were accepted")
    if "new Oui[11]" in (repo / "scripts/celeste-stage6.py").read_text():
        fail("the exact AOT-safe Oui factory was expanded unexpectedly")

    forbidden_entitlements = {
        "com.apple.developer.user-management", "com.apple.developer.icloud-container-identifiers",
        "com.apple.security.application-groups",
    }
    if forbidden_entitlements.intersection(entitlements):
        fail("a user-management, iCloud, or App Group entitlement was introduced")
    if info.get("NSBonjourServices") != ["_celeste-save._tcp"]:
        fail("the accepted Bonjour service changed")

    for document in ("README.md", "docs/STATUS.md", "docs/BUILDING.md", "docs/TROUBLESHOOTING.md"):
        text = (repo / document).read_text()
        require(text, ("Save Manager",), document)

    app_summary: dict[str, object] | None = None
    if args.app:
        app = args.app.resolve()
        built_info_path = app / "Info.plist"
        if not built_info_path.is_file():
            fail("--app does not name a built .app bundle")
        built_info = plistlib.loads(built_info_path.read_bytes())
        executable = app / str(built_info.get("CFBundleExecutable", ""))
        candidates = [path for path in (executable, app / "CelesteTvOSRuntimeHost.dll") if path.is_file()]
        for token in ("STAGE10B_MUTATION", "/replace/settings", "Restart Celeste before continuing"):
            encoded = (token.encode("ascii"), token.encode("utf-16le"))
            if not any(any(item in candidate.read_bytes() for item in encoded) for candidate in candidates):
                fail(f"built app lacks Stage 10B token: {token}")
        app_summary = {"path": "<APP>", "stage10bTokens": "PASS"}

    tracked = subprocess.run(
        ["git", "-C", str(repo), "ls-files"], check=True, capture_output=True, text=True
    ).stdout.splitlines()
    offenders = [name for name in tracked if name.lower().endswith((".celeste", ".bank", ".ipa", ".mobileprovision"))]
    if offenders:
        fail("proprietary/private output is tracked: " + ", ".join(offenders[:4]))

    summary = {
        "schemaVersion": 1,
        "result": "PASS",
        "newOui": False,
        "replaceRoutes": replace_routes,
        "deleteRoutes": delete_routes,
        "settingsReset": True,
        "uploadLimits": {"settings": 65536, "saveData": 262144},
        "csrf": "per-session-256-bit",
        "revision": "activation-keyed-HMAC",
        "restartRequired": True,
        "app": app_summary,
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    print("PASS: Stage 10B fixed mutations, validation, A/B authority, conflicts, and restart boundary")
    if app_summary:
        print("PASS: built app contains the writable Save Manager boundary")
    return 0


if __name__ == "__main__":
    sys.exit(main())
