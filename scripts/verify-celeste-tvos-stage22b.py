#!/usr/bin/env python3
"""Verify the Stage 22B Save Manager continuity and frozen product boundaries."""

from __future__ import annotations

import argparse
import json
import pathlib
import plistlib
import re
import subprocess
import sys
import zipfile


START_COMMIT = "4c07843d8b8069be00aba3626b58f6053299bdaa"
RC1_COMMIT = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2_COMMIT = "641e86e4ed164cdf93f602ce2f11436449654d6e"
CLOUD_PRODUCT_SOURCE = "b5f2ec2fdd5c65c533d86aae750ee60dcaf5009e"
NATIVE_HASH = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
REPORT = "docs/history/stages/TVOS_SAVE_MANAGER_CONTINUITY_STAGE22B_REPORT.md"


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: bool, label: str) -> None:
        self.count += 1
        if not condition:
            raise SystemExit(f"error: Stage 22B verification failed: {label}")

    def equal(self, actual: object, expected: object, label: str) -> None:
        self.require(actual == expected, f"{label}: expected {expected!r}, got {actual!r}")


def git(repo: pathlib.Path, *args: str) -> str:
    return subprocess.run(
        ["git", "-C", str(repo), *args], check=True, capture_output=True, text=True
    ).stdout.strip()


def require_tokens(checks: Checks, text: str, tokens: tuple[str, ...], label: str) -> None:
    for token in tokens:
        checks.require(token in text, f"{label}: {token}")


def scan_product(checks: Checks, info: dict, blobs: list[bytes], label: str) -> None:
    required = (
        "celeste-save-manager",
        "/status",
        "Save Manager is available again.",
        "Your Save Manager session has expired.",
        "Temporary network address in use:",
    )
    for token in required:
        encodings = (token.encode(), token.encode("utf-16le"))
        checks.require(any(any(value in blob for value in encodings) for blob in blobs),
                       f"{label} contains Stage 22B token: {token}")
    checks.require("LSSupportsGameMode" not in info and "GCSupportsGameMode" not in info,
                   f"{label} retains no Game Mode declaration")


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
    checks = Checks()

    checks.equal(git(repo, "rev-parse", "v1.0.0-rc.1^{}"), RC1_COMMIT, "immutable RC1")
    checks.equal(git(repo, "rev-parse", "v1.0.0-rc.2^{}"), RC2_COMMIT, "immutable RC2")
    checks.require(subprocess.run(
        ["git", "-C", str(repo), "merge-base", "--is-ancestor", START_COMMIT, "HEAD"],
        check=False, capture_output=True
    ).returncode == 0, "Stage 22B starts from the accepted integration commit")

    paths = {
        "policy": repo / "tvos/CelesteTvOSRuntimeHost/SaveManagerContinuityPolicy.cs",
        "protocol": repo / "tvos/CelesteTvOSRuntimeHost/SaveManagerHttpProtocol.cs",
        "service": repo / "tvos/CelesteTvOSRuntimeHost/SaveManagerService.cs",
        "bridge": repo / "managed/templates/TvOSSaveManagerBridge.cs",
        "tests": repo / "tvos/SaveManagerContinuityTests/Program.cs",
        "test_project": repo / "tvos/SaveManagerContinuityTests/SaveManagerContinuityTests.csproj",
        "builder": repo / "build-tvos.sh",
        "live10a": repo / "scripts/verify-celeste-tvos-stage10a-live.py",
        "live10b": repo / "scripts/verify-celeste-tvos-stage10b-live.py",
        "readme": repo / "README.md",
        "status": repo / "docs/STATUS.md",
        "building": repo / "docs/BUILDING.md",
        "troubleshooting": repo / "docs/TROUBLESHOOTING.md",
        "history": repo / "docs/history/README.md",
        "report": repo / REPORT,
    }
    for label, path in paths.items():
        checks.require(path.is_file(), f"required {label} exists")
    text = {label: path.read_text(encoding="utf-8", errors="replace") for label, path in paths.items()}

    require_tokens(checks, text["policy"], (
        "PreferredPort = 49728", 'ServiceIdentifier = "celeste-save-manager"',
        "ProtocolVersion = 1", "InstanceBytes = 16", "PollIntervalMilliseconds = 2000",
        "PollTimeoutMilliseconds = 1500", "DisconnectFailureThreshold = 3",
        "DarwinAddressInUse = 48", "ShouldUseEphemeralFallback",
    ), "continuity policy")
    require_tokens(checks, text["service"], (
        "NWListener.Create(SaveManagerContinuityPolicy.PreferredPort.ToString(CultureInfo.InvariantCulture), parameters)",
        "parameters.ReuseLocalAddress = true", "NWListener.Create(parameters)",
        "error?.ErrorDomain == NWErrorDomain.Posix", "error?.ErrorCode ?? 0",
        "fallbackAttempted = true", "usingTemporaryPort = true",
        "ReplaceFailedPreferredListenerWithFallback", "if (source != listener) return",
        "SaveManagerProtocolResult result = current.HandleWithActivity(buffered)",
        "if (result.CountsAsManagerActivity) TouchInactivityTimer()",
        'BonjourServiceType = "_celeste-save._tcp"',
    ), "fixed listener/service policy")
    checks.equal(text["service"].count("NWListener.ConnectionLimit"), 1,
                 "native listener limit appears only in the prohibiting comment")
    accept_block = text["service"].split("private void AcceptConnection", 1)[1].split("private void Receive", 1)[0]
    checks.require("TouchInactivityTimer" not in accept_block,
                   "connection acceptance no longer refreshes inactivity before routing")

    require_tokens(checks, text["protocol"], (
        "HandleWithActivity", 'request.Path != "/status"', 'request.Path == "/status"',
        'BaseHeaders("application/json; charset=utf-8"', "slide: false", "slide: true",
        "instanceId = RandomHex(SaveManagerContinuityPolicy.InstanceBytes)",
        "SaveManagerContinuityPolicy.IsValidInstanceId(suppliedInstance)",
        'name=instance value=', "ParseAuthForm", "CountsAsActivityForRejectedRequest",
        "active,authenticated,instance,protocol,service", "fetch('/status'",
        "continuityFailures<", "continuityReconnect.addEventListener",
        "continuityReconnect.disabled=kind==='connected'||kind==='checking'",
        "location.assign('/')", "if(continuityPolling)return", "AbortController",
        "data-continuity-control", "if(!continuityConnected)return",
    ), "status/auth/browser protocol")
    checks.require("<32 lowercase" not in text["protocol"], "status output is not a placeholder")
    checks.require(" onclick=" not in text["protocol"].lower(), "no inline reconnect handler")
    checks.require("script-src 'unsafe-inline'" not in text["protocol"], "script CSP remains nonce-only")
    checks.require(all(token not in text["protocol"] for token in
                       ("WebSocket", "EventSource", "Access-Control-Allow-Origin")),
                   "no WebSocket, SSE, or CORS")
    require_tokens(checks, text["service"], (
        "DisplayUrls(urls, usingTemporaryPort)", '"Temporary network address in use:"',
    ), "Apple TV fallback UI without changing canonical generated Celeste source")
    require_tokens(checks, text["live10a"] + text["live10b"], (
        'name=instance value=', '"instance"',
    ), "physical automation instance-bound authentication")

    checks.equal(len(re.findall(r"^Test\(", text["tests"], re.MULTILINE)), 57,
                 "Stage 22B deterministic test count")
    checks.equal(len(re.findall(r"^Test\(", (repo / "tvos/SaveManagerProtocolTests/Program.cs").read_text(), re.MULTILINE)),
                 66, "inherited protocol test count")
    checks.equal(len(re.findall(r"^Test\(", (repo / "tvos/SaveManagerPairingTests/Program.cs").read_text(), re.MULTILINE)),
                 31, "inherited QR test count")

    isolation_text = "\n".join((repo / relative).read_text(encoding="utf-8") for relative in (
        "tvos/CelesteTvOSRuntimeHost/PersistenceStore.cs",
        "managed/templates/TvOSSettingsSerializer.cs",
        "managed/templates/TvOSSaveDataSerializer.cs",
        "managed/celeste-stage6-policy.json",
    ))
    checks.require("49728" not in isolation_text and "celeste-save-manager" not in isolation_text,
                   "continuity state does not enter persistence or Celeste serializers")

    cloud_workflow = (repo / "cloud-builder-template/.github/workflows/build.yml").read_text(encoding="utf-8")
    cloud_common = (repo / "cloud-builder-template/scripts/cloud-common.sh").read_text(encoding="utf-8")
    checks.equal(cloud_workflow.count(f"CLOUD_PUBLIC_SOURCE_SHA: {CLOUD_PRODUCT_SOURCE}"), 1,
                 "cloud workflow remains pinned to RC2 product source")
    checks.equal(cloud_common.count(f'CLOUD_PUBLIC_SOURCE_SHA="{CLOUD_PRODUCT_SOURCE}"'), 1,
                 "cloud helper remains pinned to RC2 product source")

    require_tokens(checks, text["builder"], (
        "verify-save-manager-continuity-source", "verify-stage22b-unsigned", "verify-stage22b-signed",
    ), "public builder Stage 22B gates")
    for label in ("readme", "status", "building", "troubleshooting"):
        checks.require("Save Manager" in text[label], f"{label} documents Save Manager")
    checks.require(pathlib.Path(REPORT).name in text["history"], "Stage 22B history index entry")

    if args.generated_root:
        generated = args.generated_root.resolve()
        bridge = (generated / "Celeste/TvOSSaveManagerBridge.cs").read_text(encoding="utf-8")
        checks.require("TemporaryAddress" not in bridge,
                       "canonical generated bridge remains unchanged by Stage 22B")

    if args.native_manifest:
        manifest = json.loads(args.native_manifest.resolve().read_text(encoding="utf-8"))
        checks.equal(manifest.get("logicalSetSha256"), NATIVE_HASH, "native logical hash")

    product = "not-requested"
    if args.app:
        info, blobs = app_product(args.app.resolve())
        scan_product(checks, info, blobs, "built app")
        product = "app"
    if args.ipa:
        with zipfile.ZipFile(args.ipa.resolve()) as archive:
            plists = [name for name in archive.namelist()
                      if re.fullmatch(r"Payload/[^/]+\.app/Info\.plist", name)]
            checks.equal(len(plists), 1, "IPA has one Payload app")
            prefix = plists[0].removesuffix("Info.plist")
            info = plistlib.loads(archive.read(plists[0]))
            names = {str(info.get("CFBundleExecutable", "")), "CelesteTvOSRuntimeHost.dll", "Celeste.dll"}
            blobs = [archive.read(name) for name in archive.namelist()
                     if name.startswith(prefix) and name.rsplit("/", 1)[-1] in names]
            scan_product(checks, info, blobs, "unsigned IPA")
        product = "ipa"

    tracked = git(repo, "ls-files").splitlines()
    forbidden_suffixes = (".celeste", ".bank", ".ipa", ".mobileprovision", ".p12", ".cer")
    offenders = [name for name in tracked
                 if name.lower().endswith(forbidden_suffixes) or ".app/" in name.lower()]
    checks.require(not offenders, f"no private/proprietary product tracked: {offenders[:4]}")

    result = {
        "schemaVersion": 1,
        "stage": "22B",
        "result": "PASS",
        "checks": checks.count,
        "deterministicTests": 57,
        "preferredPort": 49728,
        "statusProtocol": 1,
        "product": product,
        "nativeLogicalSha256": NATIVE_HASH,
        "cloudProductSource": CLOUD_PRODUCT_SOURCE,
    }
    if args.output:
        output = args.output.resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"PASS: {checks.count} Stage 22B Save Manager continuity checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
