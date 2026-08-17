#!/usr/bin/env python3
"""Verify current Stage 24C2 controller-first iOS product invariants."""

from __future__ import annotations

import json
import pathlib
import plistlib
import subprocess
import sys

BASE = "212b0c97b5f58f624a8de6b116b9b36130f5cc6c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
DEFERRED_RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
IOS_GENERATED = "f51187556979e6970b00ac99c21e3a09c7e33d674138bd534c46a308406f1d03"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: bool, message: str) -> None:
        if not condition:
            raise SystemExit(f"FAIL: {message}")
        self.count += 1


def read(root: pathlib.Path, relative: str) -> str:
    return (root / relative).read_text()


def git(root: pathlib.Path, *arguments: str) -> str:
    return subprocess.check_output(["git", "-C", root, *arguments], text=True).strip()


def main() -> int:
    root = pathlib.Path(__file__).resolve().parents[1]
    c = Checks()
    core = read(root, "modern-ios/CelesteIOSFoundation/CelesteFileDurabilityStore.cs")
    c1_hook = read(root, "managed/templates/IOSStorageHooks.cs")
    hook = read(root, "managed/templates/IOSStorageHooksC2.cs")
    transform = read(root, "scripts/celeste-ios-stage24c2.py")
    prepare = read(root, "scripts/prepare-celeste-ios-runtime.sh")
    build = read(root, "scripts/build-ios-celeste.sh")
    tests = read(root, "modern-ios/CelesteIOSDurabilityTests/Program.cs")
    project = read(root, "modern-ios/CelesteIOSRuntimeHost/CelesteIOSRuntimeHost.csproj")
    program = read(root, "modern-ios/CelesteIOSRuntimeHost/Program.cs")
    lifecycle = read(root, "modern-ios/CelesteIOSRuntimeHost/IOSCelesteLifecycle.cs")
    audio_session = read(root, "modern-ios/CelesteIOSRuntimeHost/IOSAudioSessionCoordinator.cs")
    context = read(root, "modern-ios/CelesteIOSRuntimeHost/IOSCelesteRuntimeContext.cs")
    package = read(root, "scripts/verify-ios-package.py")
    profiles = read(root, "managed/celeste-input-profiles.json")

    c.require(git(root, "rev-parse", f"{BASE}^{{commit}}") == BASE, "Stage 24C2 baseline exists")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "RC1 target preserved")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "RC2 target preserved")
    c.require(git(root, "rev-parse", "origin/release/v1.0.0-rc.3^{commit}") == DEFERRED_RC3,
              "deferred RC3 branch preserved")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 tag remains absent")
    c.require(git(root, "diff", "--name-only", BASE, "--", "native", "build-tvos.sh") == "",
              "accepted native and tvOS builder source unchanged")
    tvos_delta = set(filter(None, git(root, "diff", "--name-only", BASE, "--", "tvos").splitlines()))
    c.require(tvos_delta.issubset({
        "tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj",
        "tvos/CelesteTvOSRuntimeHost/ControllerPromptPolicy.cs",
        "tvos/CelesteTvOSRuntimeHost/ControllerPromptPreferences.cs",
        "tvos/ControllerPromptTests/ControllerPromptTests.csproj",
        "tvos/ControllerPromptTests/Program.cs",
    }), "post-C2 tvOS changes are limited to the behavior-preserving shared prompt-policy extraction")

    for value, label in ((CONTENT, "Content"), (RAW, "raw source"),
                         (PATCHED, "patched source"), (STAGE6, "Stage 6")):
        c.require(value in profiles or value in read(root, "scripts/celeste-ios-stage24c1.py") or value in prepare,
                  f"canonical {label} lock retained")
    c.require("celeste-1.4.0.0-a" in profiles, "canonical game class retained")
    c.require(IOS_NATIVE in read(root, "native/ios-native-output.lock.json"), "iOS native lock retained")
    c.require(TVOS_NATIVE in read(root, "build-tvos.sh"), "tvOS native lock retained")
    c.require("celeste-ios-stage24c1.py" in prepare and "celeste-ios-stage24c2.py" in prepare,
              "one canonical pipeline with layered narrow iOS transforms")
    c.require("WriteApprovedFile" in c1_hook and "CelesteFileDurabilityStore" not in c1_hook,
              "accepted Stage 24C1 transform boundary remains reproducible")
    c.require("IOSStorageHooksC2.cs" in transform and "--templates" in transform,
              "C2 storage adapter is layered after the accepted C1 boundary")
    c.require("SHARED_COUNT = 934" in read(root, "scripts/celeste-ios-stage24c1.py"),
              "shared Stage 6 input remains fail-closed")
    c.require("EXPECTED_BLOCKS" in transform and all(value in transform for value in
              ("UserIO Save", "UserIO Load", "UserIO Exists/Delete")),
              "UserIO changes are exact source-locked transformations")
    c.require("platformTransform\": \"modern-ios-stage24c2-v1" in transform,
              "C2 transform version is explicit")
    c.require("CelesteIOSFoundation.csproj" in transform,
              "generated Celeste references the shared policy assembly")
    c.require("ios-managed-stage24c2.json" in prepare and "ios-managed-stage24c2.json" in build,
              "C2 generated manifest drives product build evidence")

    for name in ('"settings"', '"0"', '"1"', '"2"'):
        c.require(name in core and name in hook, f"logical file {name} allow-listed")
    c.require('"3"' not in core, "no fourth save slot admitted")
    c.require("CelesteFileCopy.Primary" in core and "CelesteFileCopy.PreviousGood" in core,
              "primary plus one previous-good model")
    c.require("HashSet<string> ApprovedNames" in core and "lock (gate)" in core,
              "closed registry and single-writer authority")
    c.require("validator.IsValid(logicalName, candidate)" in core and
              core.index("validator.IsValid(logicalName, candidate)") < core.index("backend.WriteAtomic"),
              "candidate validated before mutation")
    c.require("WriteAndVerify(logicalName, CelesteFileCopy.PreviousGood, primary" in core,
              "valid primary rotated before replacement")
    c.require("WriteAndVerify(logicalName, CelesteFileCopy.Primary, previousGood" in core,
              "valid backup repairs primary")
    c.require("new CelesteFileLoadResult(null" in core, "both-invalid state fails as missing")
    c.require("MaximumLogicalFileBytes = 64 * 1024 * 1024" in core,
              "iOS-only corruption sanity bound is not tvOS budget")
    c.require("256 * 1024" not in core and "NSUserDefaults" not in core + hook and
              "CelesteTvOS.Persistence" not in core + hook,
              "tvOS persistence limits/authority do not leak into iOS")
    c.require("SequenceEqual(candidate.Span)" in core, "committed raw bytes verified exactly")
    c.require("cleanupSucceeded" in core and "CleanupOwnedTemporaryFiles" in core,
              "cleanup outcome is explicit")
    c.require("IData" not in core and "System.Reflection" not in core,
              "durability policy uses no reflection/dynamic code")

    c.require("NSData.FromArray" in hook and "atomically: true" in hook,
              "production writes use Foundation atomic replacement")
    c.require("Microsoft.Win32.SafeHandles" not in hook and "new FileStream" not in hook,
              "accepted production write path avoids System.IO handles")
    c.require("NSFileType.SymbolicLink" in hook and "escaped Application Support" in hook,
              "Foundation adapter fails closed on symlinks/path escape")
    c.require("NSFileManager.DefaultManager" in hook and "GetAttributes" in hook,
              "public Foundation filesystem authority")
    c.require("AppleSettingsSerializer.Deserialize" in hook and "AppleSaveDataSerializer.Deserialize" in hook,
              "canonical AOT serializers validate Settings and SaveData")
    c.require("SaveLogicalFile" in hook and "ReadLogicalFile" in hook and
              "DeleteLogicalFile" in hook, "narrow logical-file host bridge")
    c.require("NSData's atomic save owns its private temporary name" in hook,
              "production temp ownership documented")
    c.require("IsExcludedFromBackup" not in hook + context and "SetResource" not in hook + context,
              "Application Support files retain normal device-backup semantics")

    for token in ("BeforeTemporaryCreation", "AfterTemporaryPrepared", "BackupWriteFailure",
                  "PrimaryWriteFailure", "PrimaryReadFailure", "BackupReadFailure",
                  "AfterCommitCleanupFailure"):
        c.require(token in tests, f"fault coverage: {token}")
    c.require("Parallel.For" in tests, "concurrent write serialization tested")
    c.require("400-save stress" in tests and "CountCopies" in tests,
              "bounded backup stress tested")
    c.require("Directory.CreateSymbolicLink" in tests and "traversal rejected" in tests,
              "path traversal/symlink tests")
    c.require(all(label in tests for label in ("slot 0", "slot 1", "slot 2", "Settings")),
              "all logical categories tested")
    c.require("both invalid returns missing semantics" in tests and "corrupt save primary recovers" in tests,
              "corruption and both-invalid semantics tested")
    c.require("invalid candidate rejected before mutation" in tests,
              "malformed candidate cannot replace good state")

    c.require("AppleRuntimeDiagnostics.StopAllRumble" in lifecycle,
              "background stops active controller effects")
    c.require("audioSession.Pause" in lifecycle and "audioSession.Resume" in lifecycle and
              "AppleAudioDiagnostics.LifecyclePause" in audio_session and
              "AppleAudioDiagnostics.LifecycleResume" in audio_session,
              "FMOD follows the reactivated iOS session without reinitialization")
    c.require('SDL.SDL_SetHint("SDL_AUDIO_CATEGORY", "playback")' in program and
              "AVAudioSessionCategory.Playback" in audio_session,
              "iOS audio uses the public playback policy that ignores Ring/Silent")
    c.require("SetActive(true" in audio_session and
              audio_session.index("SetActive(true") < audio_session.index("AppleAudioDiagnostics.LifecycleResume(reason)"),
              "OS audio session reactivates before the FMOD root bus")
    c.require("ObserveInterruption" in audio_session and "ObserveRouteChange" in audio_session,
              "interruption and route changes trigger bounded session revalidation")
    c.require("BeginInvokeOnMainThread" in audio_session and "activationScheduled" in audio_session,
              "audio restoration is main-thread-owned and duplicate-suppressed")
    c.require("runtime-disposed=false" in lifecycle and "existing-runtime-resumed=true" in lifecycle,
              "one-runtime lifecycle remains explicit")
    c.require("banks=7" in context and context.count("Master Bank.bank") == 1,
              "seven-bank product preflight retained")
    c.require("49728" not in context + lifecycle and "NWListener" not in context + lifecycle,
              "iOS Save Manager remains absent")
    c.require("PerformanceHUD" not in context + lifecycle and "MetalForceHudEnabled" not in context + lifecycle,
              "iOS Performance HUD remains absent")
    c.require("TouchController" not in context and "touch" not in transform.lower(),
              "accepted C2 transform itself remains touch-free and reproducible")
    c.require("menu_exit" in read(root, "scripts/celeste-ios-stage24c1.py"),
              "desktop Quit row remains deterministically removed")
    c.require("UseInterpreter>false" in project and "TrimMode" in project and "MtouchUseLlvm" in project,
              "full-AOT/full-trim/interpreter-free project policy")
    c.require(all(token in build for token in ("RunAOTCompilation=true", "MtouchLink=Full",
                                               "TrimMode=full", "UseInterpreter=false")),
              "device builder locks AOT/trim/no interpreter")
    c.require("_mono_jit_init" in package and "libmono-component-interpreter" in package,
              "package verifier rejects JIT/interpreter")

    privacy = plistlib.loads((root / "modern-ios/CelesteIOSRuntimeHost/PrivacyInfo.xcprivacy").read_bytes())
    c.require(privacy["NSPrivacyTracking"] is False and privacy["NSPrivacyCollectedDataTypes"] == [],
              "no tracking or collected data")
    c.require(not git(root, "ls-files", ".build/celeste-ios", "artifacts/ios-celeste"),
              "generated/proprietary iOS outputs remain ignored")

    generated = root / "artifacts/ios-celeste/current/ios-managed-stage24c2.json"
    if generated.exists():
        value = json.loads(generated.read_text())
        c.require(value["output"]["fileCount"] == 928, "C2 generated file count")
        c.require(value["output"]["logicalSha256"] == IOS_GENERATED, "C2 generated source lock")
        c.require(value["input"]["fileCount"] == 928, "C2 consumes exact-size C1 tree")
        generated_root = root / ".build/celeste-ios/current/managed"
        user_io = (generated_root / "Celeste/UserIO.cs").read_text()
        generated_hook = (generated_root / "Celeste/IOSStorageHooks.cs").read_text()
        generated_project = (generated_root / "Celeste.Modern.csproj").read_text()
        c.require("SaveLogicalFile(path, data)" in user_io and "ReadLogicalFile(path, backup)" in user_io,
                  "generated UserIO uses durability bridge")
        c.require("CopyApprovedFile" not in user_io and "WriteApprovedFile" not in user_io,
                  "old current-as-backup behavior removed")
        c.require("CelesteIOSFoundation.csproj" in generated_project,
                  "generated game carries shared policy reference")
        c.require("FoundationDurabilityBackend" in generated_hook,
                  "generated Foundation adapter included")
        c.require("CelesteTvOS.ControllerPrompts" not in generated_hook + generated_project,
                  "tvOS prompt preference does not leak into the controller-first iOS lane")

    app_candidates = sorted((root / "artifacts/ios-celeste/device/publish").glob("*.app"))
    if app_candidates:
        app = app_candidates[0]
        c.require((app / "CelesteIOSFoundation.dll").is_file(), "shared durability policy ships")
        c.require((app / "CelesteIOSFoundation.aotdata.arm64").is_file(), "durability policy is AOT compiled")
        c.require(not (app / "libmono-component-interpreter.dylib").exists(), "interpreter absent from product")

    print(f"PASS: Stage 24C2 modern iOS controller gameplay verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
