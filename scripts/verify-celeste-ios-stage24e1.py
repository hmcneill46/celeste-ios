#!/usr/bin/env python3
"""Verify Stage 24E1 Files portability and touch-layout sharing invariants."""

from __future__ import annotations

import argparse
import json
import os
import pathlib
import plistlib
import subprocess
import sys


BASE = "7c8580c4064268a3a4dccec6b000af1c46b963ac"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
DEFERRED_RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
D3_GENERATED = "08dcf0f324254dc31235b27d63b4144bad2a74079eb523d2c173117df1299e47"
E1_GENERATED = "2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357"


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, value: bool, message: str) -> None:
        if not value:
            raise SystemExit(f"FAIL: {message}")
        self.count += 1


def read(root: pathlib.Path, relative: str) -> str:
    return (root / relative).read_text()


def git(root: pathlib.Path, *arguments: str) -> str:
    return subprocess.check_output(["git", "-C", root, *arguments], text=True).strip()


def resolve_origin_branch(root: pathlib.Path, branch: str) -> str:
    """Resolve a branch in both full and public single-branch clones."""
    local = subprocess.run(
        ["git", "-C", root, "rev-parse", "--verify", "--quiet", f"origin/{branch}^{{commit}}"],
        text=True,
        capture_output=True,
        check=False,
    )
    if local.returncode == 0:
        return local.stdout.strip()
    remote = git(root, "ls-remote", "--heads", "origin", f"refs/heads/{branch}")
    fields = remote.split()
    return fields[0] if len(fields) == 2 else ""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app", type=pathlib.Path,
                        help="optional built Celeste.app to run the full-AOT package verifier")
    args = parser.parse_args()
    root = pathlib.Path(__file__).resolve().parents[1]
    c = Checks()

    policy = read(root, "modern-ios/CelesteIOSFoundation/IOSDataPortabilityPolicy.cs")
    durability = read(root, "modern-ios/CelesteIOSFoundation/CelesteFileDurabilityStore.cs")
    coordinator = read(root, "modern-ios/CelesteIOSRuntimeHost/IOSFilePortabilityCoordinator.cs")
    context = read(root, "modern-ios/CelesteIOSRuntimeHost/IOSCelesteRuntimeContext.cs")
    program = read(root, "modern-ios/CelesteIOSRuntimeHost/Program.cs")
    data_ui = read(root, "managed/templates/IOSDataFilesUI.cs")
    storage = read(root, "managed/templates/IOSStorageHooksC2.cs")
    storage_e1 = read(root, "managed/templates/IOSStoragePortabilityE1.cs")
    touch = read(root, "managed/templates/IOSTouchControlsD3.cs")
    touch_ui = read(root, "managed/templates/IOSTouchControlsUID3.cs")
    transform = read(root, "scripts/celeste-ios-stage24e1.py")
    prepare = read(root, "scripts/prepare-celeste-ios-runtime.sh")
    build = read(root, "scripts/build-ios-celeste.sh")
    package = read(root, "scripts/verify-ios-package.py")
    runtime_project = read(root, "modern-ios/CelesteIOSRuntimeHost/CelesteIOSRuntimeHost.csproj")
    build_identity = read(root, "modern-ios/CelesteIOSFoundation/IOSPortBuildIdentity.cs")
    foundation_tests = read(root, "modern-ios/CelesteIOSFoundationTests/Program.cs")
    durability_tests = read(root, "modern-ios/CelesteIOSDurabilityTests/Program.cs")

    c.require(git(root, "rev-parse", f"{BASE}^{{commit}}") == BASE, "Stage 24E1 baseline exists")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "RC1 preserved")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "RC2 preserved")
    c.require(resolve_origin_branch(root, "release/v1.0.0-rc.3") == DEFERRED_RC3,
              "deferred RC3 preserved")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 tag remains absent")
    c.require(git(root, "diff", "--name-only", BASE, "--", "tvos", "native", "build-tvos.sh") == "",
              "tvOS and native product sources are unchanged")
    c.require(git(root, "diff", "--name-only", BASE, "--", ".github") == "",
              "GitHub Actions remain untouched")
    c.require(git(root, "status", "--short", "FNA") == "", "FNA submodule remains clean")

    c.require("ApplicationSupportDirectory" in context and 'Path.Combine(support, "Celeste")' in context and
              'CELESTE_IOS_STORAGE_ROOT' in context,
              "authoritative state remains in private Application Support")
    c.require('Path.Combine(root, copy == CelesteFileCopy.Primary ? "Saves" : "Backups")' in storage,
              "live primary and previous-good copies remain app-owned")
    c.require("UIFileSharingEnabled" not in read(root, "modern-ios/CelesteIOSRuntimeHost/Info.plist") and
              "LSSupportsOpeningDocumentsInPlace" not in read(root, "modern-ios/CelesteIOSRuntimeHost/Info.plist"),
              "Documents/file-sharing exposure remains disabled")

    for token in ("UIDocumentPickerViewController", "UIActivityViewController", "UIWindowScene",
                  "StartAccessingSecurityScopedResource", "StopAccessingSecurityScopedResource",
                  "NSFileCoordinator", "CoordinateRead", "AllowsMultipleSelection = false"):
        c.require(token in coordinator, f"native document boundary includes {token}")
    c.require("asCopy: true" in coordinator and "No external URL crosses this boundary" in policy,
              "external providers are explicit copy sources, never save authority")
    c.require("NSFileAttributes" in coordinator and "NSFileType.Regular" in coordinator and
              "attributes.Size == 0" in coordinator and "maximumBytes" in coordinator,
              "external bytes are bounded before materialization")
    c.require("Task.Run" in coordinator and "BeginInvokeOnMainThread" in coordinator,
              "provider reads are coordinated off the game thread and returned on main")
    c.require("string? callbackFailure" in coordinator and
              "Never let a managed exception cross this native block" in coordinator and
              'callbackFailure = "unreadable"' in coordinator and
              'callbackFailure = "bounds"' in coordinator,
              "coordinated-read native callback converts expected failures without throwing across Objective-C")
    c.require("FinishPresentation" in coordinator and
              "void Finish()" in coordinator and
              "continuation();" in coordinator and
              "controller.View?.Window is null" in coordinator and
              "Task.Delay(25)" in coordinator and
              "DismissViewController(false" in coordinator,
              "native Files/share UI waits for actual UIKit detachment")
    c.require("files-ui seq=" in coordinator and "Stopwatch.GetElapsedTime" in coordinator and
              all(token not in coordinator for token in ("url.Path", "AbsoluteString", "LastPathComponent")),
              "Files presentation timings are privacy-safe and do not log external paths")
    c.require("CelesteExports" in coordinator and "Guid.NewGuid" in coordinator and
              "CleanupTemporaryRoot" in coordinator and "CleanupAbandonedExports" in coordinator,
              "temporary exports are private, unique, and cleaned after completion/cancellation")
    c.require("PopoverPresentationController" in coordinator and "SourceView" in coordinator and
              "SourceRect" in coordinator,
              "share sheet has a valid iPad popover anchor")
    c.require("IOSTouchControls.Reset" in coordinator and "IOSPresentationCoordinator.Invalidate" in coordinator,
              "system UI clears touch ownership and presentation state")
    c.require("using IOSFilePortabilityCoordinator filePortability = new();" in program,
              "one process-owned UIKit portability coordinator is registered")
    c.require(all(token not in coordinator + policy for token in
                  ("NWListener", "Bonjour", "_celeste-save._tcp", "HttpListener", "Process.Start")),
              "iOS Files support introduces no Save Manager/network/process path")
    c.require(all(token not in coordinator for token in ("securityScopedBookmark", "StartDownloadingUbiquitousItem")),
              "no persistent external bookmark or bespoke iCloud synchronization exists")

    for token in ("DATA & FILES", "Save Slot 1", "Save Slot 2", "Save Slot 3", "Settings",
                  "Export All Saves...", "Import Save...", "Export to Files...", "Share...",
                  "Restore Previous Save...", "Restore Previous Settings..."):
        c.require(token in data_ui, f"Data & Files UI includes {token}")
    c.require("Engine.Scene is not Level && !UserIO.Saving" in data_ui and
              "Return to the main menu before" in data_ui,
              "destructive import/restore is blocked behind active-Level and UserIO safety")
    c.require("Export(" in data_ui and "ReadLogicalFile" in data_ui and
              "Celeste-Slot-1.celeste" in data_ui and "Celeste-Settings.celeste" in data_ui,
              "exports use exact logical bytes and user-friendly filenames")
    c.require('new[] { "settings", "0", "1", "2" }' in data_ui and
              "RequestExport(documents, false" in data_ui,
              "Export All supplies every existing logical file to native Files UI")
    c.require("MaximumLogicalFileBytes" in data_ui and "ValidateLogicalFile" in data_ui + storage_e1 and
              "CanonicalCelesteValidator" in storage,
              "imports retain the 64 MiB bound and exact canonical serializer validation")
    c.require("SaveData, not Celeste Settings" in data_ui and "Celeste Settings, not SaveData" in data_ui,
              "SaveData and Settings type confusion is rejected explicitly")
    c.require("Choose Save Slot" in data_ui and "choice.Value.ToString()" in data_ui,
              "root import asks for an explicit destination independent of filename")
    c.require("Replace Existing File" in data_ui and "A previous copy will be retained" in data_ui,
              "existing destination overwrite requires explicit confirmation")
    c.require("private void PresentChoice" in data_ui and "new Button(choices[index])" in data_ui and
              "UIAlertController" not in coordinator and "ChoiceRequested" not in policy,
              "slot and overwrite choices use the reliable Celeste input loop, not UIKit alerts")
    c.require("SaveLogicalFile(logicalName, data)" in data_ui and "ReadLogicalFile(logicalName)" in data_ui and
              "SequenceEqual(data)" in data_ui,
              "import uses the accepted durability store with exact post-write readback")
    c.require("Settings.Reload()" in data_ui and "Input.Initialize()" in data_ui and "Input.ResetGrab()" in data_ui,
              "safe Settings import uses the native Celeste reload path")
    c.require("OuiFileSelect.Loaded = false" in data_ui,
              "SaveData import invalidates the normal file-select inventory")

    restore_method = durability[durability.index("public CelesteFileRestoreResult RestorePreviousGood"):
                                durability.index("public bool Delete")]
    c.require("RestorePreviousGood" in durability + data_ui + storage_e1 and
              "backend.WriteAtomic(logicalName, CelesteFileCopy.Primary, previousGood!)" in durability,
              "Restore Previous installs the validated recovery copy atomically")
    c.require(restore_method.index("backend.WriteAtomic(logicalName, CelesteFileCopy.Primary, previousGood!)") <
              restore_method.index("WriteAndVerify(logicalName, CelesteFileCopy.PreviousGood, primary!)"),
              "restore verifies primary before rotating the undo copy")
    c.require("repeating Restore Previous naturally performs an undo" in durability and
              "restore again to undo" in data_ui,
              "successful previous-good swap exposes its one-generation undo semantics")
    c.require("LoadPreviousGood(logicalName) is not null" in storage_e1 and "Disabled = !IOSDataFiles.HasPrevious" in data_ui,
              "Restore Previous is disabled when no valid copy exists")

    c.require("TouchLayoutShareDocument" in policy and "FormatVersion = 1" in policy and
              'Extension = "celestetouch"' in policy and "MaximumBytes = 32 * 1024" in policy,
              "touch layout has a bounded distinct versioned document format")
    c.require("phoneProfile" in policy and "tabletProfile" in policy and
              "TouchLayoutCodec.TryDecode" in policy and "TouchLayoutPolicy.IsStructurallyValid" in policy,
              "layout import reuses the exact D3 codec and structural validator")
    c.require("AllowTrailingCommas = false" in policy and "CommentHandling.Disallow" in policy and
              "MaxDepth = 8" in policy and "unknown touch share property rejected" in foundation_tests,
              "layout JSON parser rejects extension ambiguity and unbounded structure")
    c.require("Export Layout..." in touch_ui and "Share Layout..." in touch_ui and "Import Layout..." in touch_ui,
              "Touch Controls exposes native layout export/share/import")
    c.require("BeginImportedLayoutPreview" in touch + touch_ui and
              "private static void BeginLayoutEditor(TouchLayoutProfile working" in touch and
              "BeginLayoutEditor(imported, closed)" in touch,
              "imported layout enters the transactional D3 editor before persistence")
    c.require("presentation.IsPad ? document.TabletProfile : document.PhoneProfile" in touch and
              "PhoneProfile" in touch and "TabletProfile" in touch,
              "the current form factor is previewed without fabricating the other profile")
    c.require(all(token not in policy for token in
                  ("GrabMode.Touch", "GrabMode.Controller", "GrabMode.Keyboard", "DirectionalHaptics", "Visibility.v1")),
              "shared layout excludes Grab modes, haptics, and visibility preferences")

    info = plistlib.loads((root / "modern-ios/CelesteIOSRuntimeHost/Info.plist").read_bytes())
    imported = info.get("UTImportedTypeDeclarations", [])
    exported = info.get("UTExportedTypeDeclarations", [])
    c.require(any(item.get("UTTypeIdentifier") == "io.github.roootthefox.celeste.save-data" and
                  item.get("UTTypeTagSpecification", {}).get("public.filename-extension") == ["celeste"]
                  for item in imported),
              ".celeste is declared as an imported existing format")
    c.require(any(item.get("UTTypeIdentifier") == "io.github.roootthefox.celeste.touch-layout" and
                  item.get("UTTypeTagSpecification", {}).get("public.filename-extension") == ["celestetouch"]
                  for item in exported),
              ".celestetouch is declared as a project-owned exported format")
    c.require("CFBundleDocumentTypes" not in info,
              "Open-In ownership remains intentionally deferred instead of importing silently")
    privacy = plistlib.loads((root / "modern-ios/CelesteIOSRuntimeHost/PrivacyInfo.xcprivacy").read_bytes())
    c.require(privacy["NSPrivacyTracking"] is False and privacy["NSPrivacyCollectedDataTypes"] == [],
              "Files workflows add no tracking or collected data")
    c.require("icloud" not in json.dumps(info).lower(), "no iCloud container/capability is declared")

    c.require("celeste-ios-stage24e1.py" in prepare and "ios-managed-stage24e1.json" in prepare + build,
              "one canonical pipeline layers the narrow iOS E1 transform")
    c.require("Apple-specific Data & Files and iOS port identity rows" in transform and
              'menu.Add(new TextMenu.Button("Data & Files").Pressed(OpenDataFiles))' in transform and
              "IOSPortBuildIdentity.DisplayLabel" in transform,
              "Data & Files and visible build identity are in the Apple-specific Options block")
    c.require('Version = "0.1.1"' in build_identity and "Build = 4" in build_identity and
              "iOS PORT v0.1.1" in build_identity and
              "<ApplicationDisplayVersion>0.1.1</ApplicationDisplayVersion>" in runtime_project and
              "<ApplicationVersion>4</ApplicationVersion>" in runtime_project,
              "iOS port semantic version/build identity is consistent and separate from Celeste 1.4.0.0")
    c.require("IOSDataFilesUI.cs:Celeste/IOSDataFilesUI.cs" in build and
              "IOSStoragePortabilityE1.cs:Celeste/IOSStoragePortabilityE1.cs" in build,
              "device build rejects stale generated E1 bridge sources")
    c.require("RunAOTCompilation=true" in build and "MtouchLink=Full" in build and
              "TrimMode=full" in build and "UseInterpreter=false" in build and "MtouchUseLlvm=true" in build,
              "full-AOT/full-trim/LLVM/no-interpreter product gate retained")
    c.require("DATA & FILES" in package and "UIFileSharingEnabled" in package and
              "com.apple.developer.icloud" in package and "NSFileCoordinator" in package,
              "package verifier owns E1 UI/storage/entitlement/AOT invariants")
    c.require(IOS_NATIVE in read(root, "native/ios-native-output.lock.json"), "iOS native lock preserved")
    c.require(TVOS_NATIVE in read(root, "build-tvos.sh"), "tvOS native lock preserved")
    c.require(all(value in read(root, "managed/celeste-input-profiles.json") +
                  read(root, "scripts/celeste-managed.py") + read(root, "scripts/build-ios-celeste.sh")
                  for value in (CONTENT, RAW, PATCHED, STAGE6)),
              "canonical Content/raw/patched/Stage6 locks remain represented")
    c.require(not git(root, "ls-files", ".build/celeste-ios", "artifacts/ios-celeste"),
              "generated/proprietary iOS output remains ignored")

    generated = root / "artifacts/ios-celeste/current/ios-managed-stage24e1.json"
    if generated.exists():
        value = json.loads(generated.read_text())
        c.require(value["input"]["fileCount"] == 942 and
                  value["input"]["logicalSha256"] == D3_GENERATED,
                  "E1 consumes the exact current D3 tree")
        c.require(value["output"]["fileCount"] == 944 and
                  value["output"]["logicalSha256"] == E1_GENERATED,
                  "E1 generated tree lock")
        c.require(value["liveStorage"] == "Library/Application Support/Celeste" and
                  value["externalSemantics"] == "validated-copy",
                  "E1 manifest locks private-live/copy semantics")

    environment = dict(os.environ, MSBuildEnableWorkloadResolver="false")
    foundation_project = root / "modern-ios/CelesteIOSFoundationTests/CelesteIOSFoundationTests.csproj"
    durability_project = root / "modern-ios/CelesteIOSDurabilityTests/CelesteIOSDurabilityTests.csproj"
    for project, expected in ((foundation_project, "PASS: modern iOS foundation deterministic tests 243"),
                              (durability_project, "PASS: modern iOS durability deterministic tests 87")):
        subprocess.check_call(["dotnet", "build", str(project), "-c", "Release", "--no-restore", "-maxcpucount:1"],
                              env=environment)
        output = subprocess.check_output(["dotnet", "exec", str(project.parent / "bin/Release/net10.0" /
                                                                  f"{project.stem}.dll")],
                                         text=True, env=environment)
        c.require(expected in output, f"deterministic suite passes: {project.stem}")
    for label in ("previous-good restore is reversible", "restore primary failure preserves previous-good",
                  "restore remains successful when only undo rotation fails", "touch share document decodes",
                  "unsupported touch share version rejected", "unknown touch share property rejected",
                  "Files bridge clears every process-owned delegate"):
        c.require(label in foundation_tests + durability_tests, f"E1 semantic test coverage: {label}")

    if args.app:
        subprocess.check_call([sys.executable, str(root / "scripts/verify-ios-package.py"),
                               "--app", str(args.app.resolve()), "--lane", "device", "--product", "celeste"])
        c.require(True, "full-AOT product package passes E1 verifier")
        built_info = plistlib.loads((args.app.resolve() / "Info.plist").read_bytes())
        c.require(built_info.get("CFBundleShortVersionString") == "0.1.1" and
                  str(built_info.get("CFBundleVersion")) == "4",
                  "built app carries the visible iOS port version/build identity")

    report_path = root / "docs/history/stages/IOS_FILES_DATA_PORTABILITY_STAGE24E1_REPORT.md"
    if report_path.exists():
        report = report_path.read_text()
        c.require("Status: **PASS**" in report and BASE in report and E1_GENERATED in report,
                  "E1 report records accepted status, baseline, and generated tree")
        c.require("IOS_FILES_DATA_PORTABILITY_STAGE24E1_REPORT.md" in read(root, "docs/history/README.md"),
                  "history index links the E1 report")

    print(f"PASS: Stage 24E1 iOS Files portability verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
