#!/usr/bin/env python3
"""Verify the frozen Stage 14 tvOS release-candidate feature inventory."""

from __future__ import annotations

import argparse
import json
import pathlib
import plistlib
import re
import subprocess
import sys
import urllib.parse
import zipfile


EXPECTED_STAGE1 = "61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39"
EXPECTED_FEATURES = {
    "Automatic", "Xbox", "PlayStation", "Nintendo Switch", "Stadia"
}


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def require(text: str, tokens: tuple[str, ...], label: str) -> None:
    for token in tokens:
        if token not in text:
            fail(f"{label} is missing required token: {token}")


def verify_links(repo: pathlib.Path) -> int:
    sources = [repo / "README.md", repo / "CONTRIBUTING.md"]
    sources.extend(sorted((repo / "docs").rglob("*.md")))
    pattern = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
    checked = 0
    for source in sources:
        for raw in pattern.findall(source.read_text(encoding="utf-8")):
            target = raw.strip().strip("<>").split("#", 1)[0]
            if not target or target.startswith(("https://", "http://", "mailto:")):
                continue
            target = urllib.parse.unquote(target)
            resolved = (source.parent / target).resolve()
            try:
                resolved.relative_to(repo)
            except ValueError:
                fail(f"documentation link escapes repository: {source.relative_to(repo)} -> {target}")
            if not resolved.exists():
                fail(f"dead documentation link: {source.relative_to(repo)} -> {target}")
            checked += 1
    return checked


def product_info(app: pathlib.Path) -> tuple[dict, list[bytes]]:
    info = plistlib.loads((app / "Info.plist").read_bytes())
    executable = app / str(info.get("CFBundleExecutable", ""))
    blobs = [path.read_bytes() for path in (executable, app / "CelesteTvOSRuntimeHost.dll", app / "Celeste.dll") if path.is_file()]
    return info, blobs


def scan_product(info: dict, blobs: list[bytes], label: str) -> None:
    if info.get("CFBundleDisplayName") != "Celeste" and info.get("CFBundleName") != "Celeste":
        fail(f"{label} does not display as Celeste")
    if info.get("MinimumOSVersion") != "16.0":
        fail(f"{label} minimum tvOS is not 16.0")
    if "LSSupportsGameMode" in info or "GCSupportsGameMode" in info:
        fail(f"{label} contains an unsupported Game Mode declaration")
    required = (
        "STAGE9B_CAPTURE", "Celeste Save Manager", "STAGE10B_MUTATION",
        "Controller Prompts", "LEAVE CELESTE", "STAGE13B_RELOAD",
    )
    for token in required:
        encoded = (token.encode(), token.encode("utf-16le"))
        if not any(any(value in blob for value in encoded) for blob in blobs):
            fail(f"{label} lacks release-candidate product token: {token}")


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

    policy = json.loads((repo / "tvos/stage14-release-candidate.json").read_text())
    if policy.get("schemaVersion") != 1 or policy.get("stage") != "14":
        fail("Stage 14 checklist schema changed")
    if policy.get("stage1LogicalSha256") != EXPECTED_STAGE1:
        fail("Stage 1 logical hash changed in the release checklist")
    if set(policy.get("controllerPrompts", [])) != EXPECTED_FEATURES:
        fail("the five accepted Controller Prompt choices changed")
    build = policy.get("build", {})
    expected_build = {
        "configuration": "Release", "runtimeIdentifier": "tvos-arm64",
        "minimumTvOS": "16.0", "trimMode": "full", "fullAot": True,
        "useInterpreter": False, "renderer": "Metal",
    }
    if build != expected_build:
        fail("release build policy changed")
    persistence = policy.get("persistence", {})
    if persistence.get("logicalFiles") != ["settings", "0", "1", "2"]:
        fail("persistence logical-file inventory changed")
    if persistence.get("readableFormats") != [0, 1, 2] or persistence.get("currentWriteFormat") != 2:
        fail("persistence compatibility policy changed")
    if policy.get("deterministicTests") != {
        "saveManagerProtocol": 66, "controllerPrompts": 38,
        "gracefulQuit": 16, "softReload": 20,
    }:
        fail("accepted deterministic-test inventory changed")

    project = (repo / "tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj").read_text()
    host = (repo / "tvos/CelesteTvOSRuntimeHost/Main.cs").read_text()
    store = (repo / "tvos/CelesteTvOSRuntimeHost/Stage6PersistenceStore.cs").read_text()
    protocol = (repo / "tvos/CelesteTvOSRuntimeHost/Stage10AHttpProtocol.cs").read_text()
    prompts = "\n".join((repo / path).read_text() for path in (
        "tvos/CelesteTvOSRuntimeHost/Stage11ControllerPromptPreferences.cs",
        "tvos/CelesteTvOSRuntimeHost/Stage11ControllerPromptPolicy.cs",
    ))
    quitter = "\n".join((repo / path).read_text() for path in (
        "tvos/CelesteTvOSRuntimeHost/Stage12BQuitCoordinator.cs",
        "tvos/CelesteTvOSRuntimeHost/Stage12BQuitStateMachine.cs",
    ))
    reload = (repo / "tvos/CelesteTvOSRuntimeHost/Stage13BSoftReloadCoordinator.cs").read_text()
    plist = plistlib.loads((repo / "tvos/CelesteTvOSRuntimeHost/Info.plist").read_bytes())
    privacy = plistlib.loads((repo / "tvos/CelesteTvOSRuntimeHost/PrivacyInfo.xcprivacy").read_bytes())
    builder = (repo / "build-tvos.sh").read_text()

    require(project, ("net10.0-tvos", "TargetPlatformMinVersion>16.0", "TrimMode", "UseInterpreter>false", "CelesteAudio"), "tvOS host project")
    require(host, ("FNA3D_FORCE_DRIVER", "Metal", "Celeste.Celeste.Run", "Stage13BSoftReloadCoordinator"), "runtime host")
    require(store, ("FormatVersion = 2", "CompressionLevel.SmallestSize", "ReadCandidate(\"A\")", "ReadCandidate(\"B\")"), "persistence store")
    require(protocol, ("X-Celeste-CSRF", "X-Celeste-Revision", "press Confirm to reload Celeste"), "Save Manager protocol")
    require(prompts, ("CelesteTvOS.ControllerPrompts.v1", "DualSense", "PlayStation"), "Controller Prompt preferences")
    require(quitter, ("PreparingToLeave", "AwaitingBackground", "LeftViaBackground"), "graceful Quit coordinator")
    require(reload, ("PrepareExternalMutationReload", "CompleteExternalMutationReload", "game-disposed=false"), "soft-reload coordinator")
    require(builder, ("Celeste for Apple TV Builder", "Signing-ready unsigned IPA", "Personal Team"), "public self-builder")
    if "LSSupportsGameMode" in plist or "GCSupportsGameMode" in plist:
        fail("tracked Info.plist declares unsupported Game Mode")
    if plist.get("NSBonjourServices") != ["_celeste-save._tcp"]:
        fail("tracked Bonjour service inventory changed")
    reasons = privacy.get("NSPrivacyAccessedAPITypes", [])
    if not any(item.get("NSPrivacyAccessedAPIType") == "NSPrivacyAccessedAPICategoryUserDefaults" and
               item.get("NSPrivacyAccessedAPITypeReasons") == ["CA92.1"] for item in reasons):
        fail("UserDefaults privacy reason CA92.1 is missing")

    history = (repo / "docs/history/README.md").read_text()
    report = repo / "docs/history/stages/TVOS_RELEASE_CANDIDATE_STAGE14_REPORT.md"
    if not report.is_file() or "TVOS_RELEASE_CANDIDATE_STAGE14_REPORT.md" not in history:
        fail("Stage 14 report is not indexed under docs/history/stages")
    links = verify_links(repo)

    if args.native_manifest:
        native = json.loads(args.native_manifest.resolve().read_text())
        if native.get("logicalSetSha256") != EXPECTED_STAGE1:
            fail("fresh Stage 1 normalized manifest differs from the accepted logical hash")
    if args.generated_root:
        generated = args.generated_root.resolve()
        generated_project = (generated / "Celeste.Modern.csproj").read_text()
        for symbol in ("TVOS_STAGE10A", "TVOS_STAGE11", "TVOS_STAGE12B", "TVOS_STAGE13B"):
            if generated_project.count(symbol) != 1:
                fail(f"generated release tree does not contain exactly one {symbol} symbol")

    product = "not-requested"
    if args.app:
        info, blobs = product_info(args.app.resolve())
        scan_product(info, blobs, "built app")
        if len(list((args.app.resolve() / "Content/FMOD").rglob("*.bank"))) != 7:
            fail("built app does not contain exactly seven FMOD banks")
        if not (args.app.resolve() / "Assets.car").is_file():
            fail("built app lacks compiled branding/Top Shelf assets")
        product = "app"
    if args.ipa:
        with zipfile.ZipFile(args.ipa.resolve()) as archive:
            plist_names = [name for name in archive.namelist() if re.fullmatch(r"Payload/[^/]+\.app/Info\.plist", name)]
            if len(plist_names) != 1:
                fail("IPA does not contain exactly one Payload app")
            prefix = plist_names[0].removesuffix("Info.plist")
            info = plistlib.loads(archive.read(plist_names[0]))
            blobs = [archive.read(name) for name in archive.namelist()
                     if name.startswith(prefix) and name.rsplit("/", 1)[-1] in
                     {str(info.get("CFBundleExecutable", "")), "CelesteTvOSRuntimeHost.dll", "Celeste.dll"}]
            scan_product(info, blobs, "unsigned IPA")
            if f"{prefix}embedded.mobileprovision" in archive.namelist() or f"{prefix}_CodeSignature/CodeResources" in archive.namelist():
                fail("signing-ready IPA contains a profile or stale usable signature")
        product = "ipa"

    tracked = subprocess.run(
        ["git", "-C", str(repo), "ls-files"], check=True, capture_output=True, text=True
    ).stdout.splitlines()
    forbidden = (".celeste", ".bank", ".ipa", ".mobileprovision", ".p12", ".cer")
    offenders = [name for name in tracked if name.lower().endswith(forbidden) or ".app/" in name.lower()]
    if offenders:
        fail("private/proprietary release output is tracked: " + ", ".join(offenders[:4]))
    if not (repo / "celestemeow/celestemeow.csproj").is_file():
        fail("retained iOS project graph is missing")
    if "CelesteTvOS.ControllerPrompts" in (repo / "celestemeow/celestemeow.csproj").read_text(errors="replace"):
        fail("tvOS host preference leaked into the iOS project")

    summary = {
        "schemaVersion": 1,
        "result": "PASS",
        "stage1LogicalSha256": EXPECTED_STAGE1,
        "featureCount": 24,
        "documentationLinks": links,
        "product": product,
        "runtimeChanges": False,
    }
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    print(f"PASS: Stage 14 frozen feature inventory, product boundaries, and {links} documentation links")
    return 0


if __name__ == "__main__":
    sys.exit(main())
