#!/usr/bin/env python3
"""Verify current Stage 24D2 production iOS touch-control invariants."""

from __future__ import annotations

import hashlib
import json
import pathlib
import plistlib
import subprocess
import sys


BASE = "802819d1c30a49cb31ec0b9c419be47c378952ce"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
DEFERRED_RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
GENERATED = "5ca5e70d1fc163aa75db780d1cb7fc35b9328eea42369b91a4a2776dd8651036"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
JUMP = "fc917b4d9f473fad297bc2e1afa25a4492ae73fbba0cafe8fa7d908455f0ec25"
DASH = "d10e42cc843f7cbe0d5134ea22de3a61b4d02f304b9fa8fe36e76cf38bf786e2"
GRAB_UNGRABBED = "8983a6379c5bf0c87b61fef32d1ae3007a73f7a7d03b73507d072f61bc8cc747"
GRAB_GRABBED = "34b09bfeaf7c24be8833228ba555acb225d3d31dc063525075d001de87983185"
TOUCH = "b503745e3130036c756e74301b5ef70643202b2c7e18fd6a2e8b6f88f4de786c"
PAUSE = "57a8e8fd8cb8a935cf02acf96de8fdacd1e115a7fe7debeea7e0cdb06d2bd77f"
JOURNAL = "d6e3c7b8b1dde4371cef7bd4d16060d7eb9312425d6f6fb490d2cde3ce860ada"
JUMP_SOURCE = "750c4a4887bb408e2b676eb406727a82d8df68ec6fddd1c79ea4a17903ffd4a8"
DASH_SOURCE = "dac71354f15a90cb1efc7fef7f65ecc8632787e70fd02239f7747307efb7a94c"
GRAB_UNGRABBED_SOURCE = "7b0e66d8ec3a5fcaa0cda5f91d6876ec32497433b9e15deb9f6eaf353fb60674"
GRAB_GRABBED_SOURCE = "cd73b8c3f0d525272653ab5eb68701c8ead0b2047e4363a4675da333816c4ed8"
TOUCH_SOURCE = "579f27975833686c0e2d820a355967f3136bb0d2b79adb2edc4d1f6bf418f330"
PAUSE_SOURCE = "63ea2c81138abfe21f6d6453547262690065e950b3019457f36c5008e4929d91"
JOURNAL_SOURCE = "7341cece5f9e1f23e4b75951e40630dd116f985293c7869f5c0b46af2af424cd"


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


def sha256(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    root = pathlib.Path(__file__).resolve().parents[1]
    c = Checks()
    policy = read(root, "modern-ios/CelesteIOSFoundation/TouchControlsPolicy.cs")
    touch = read(root, "managed/templates/IOSTouchControls.cs")
    touch_ui = read(root, "managed/templates/IOSTouchControlsUI.cs")
    presentation = read(root, "managed/templates/IOSPresentationCoordinator.cs")
    transform = read(root, "scripts/celeste-ios-stage24d2.py")
    prepare_foundation = read(root, "scripts/prepare-ios-foundation.sh")
    prepare_runtime = read(root, "scripts/prepare-celeste-ios-runtime.sh")
    build = read(root, "scripts/build-ios-celeste.sh")
    project = read(root, "modern-ios/CelesteIOSRuntimeHost/CelesteIOSRuntimeHost.csproj")
    extension = read(root, "modern-ios/CelesteIOSRuntimeHost/IOSFnaExtension.targets")
    lifecycle = read(root, "modern-ios/CelesteIOSRuntimeHost/IOSCelesteLifecycle.cs")
    stable = read(root, "modern-ios/FNA.iOS/StableTouchSlotPolicy.cs")
    fna_patch = read(root, "modern-ios/patches/FNA/0001-preserve-stable-touch-finger-ids.patch")
    fna_project = read(root, "modern-ios/FNA.iOS/FNA.iOS.csproj")
    package = read(root, "scripts/verify-ios-package.py")
    tests = read(root, "modern-ios/CelesteIOSFoundationTests/Program.cs")
    shared = read(root, "shared/CelesteAppleInput/ControllerPromptPolicy.cs")
    tvos_policy = read(root, "tvos/CelesteTvOSRuntimeHost/ControllerPromptPolicy.cs")
    notice = read(root, "modern-ios/Assets/TouchControls/NOTICE.md")
    report = read(root, "docs/history/stages/IOS_TOUCH_CONTROLS_STAGE24D2_REPORT.md")
    history = read(root, "docs/history/README.md")
    readme = read(root, "README.md")
    ios_docs = read(root, "docs/IOS_FOUNDATION.md")

    c.require(git(root, "rev-parse", f"{BASE}^{{commit}}") == BASE, "Stage 24D2 baseline exists")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "RC1 target preserved")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "RC2 target preserved")
    c.require(git(root, "rev-parse", "origin/release/v1.0.0-rc.3^{commit}") == DEFERRED_RC3,
              "deferred RC3 target preserved")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 tag remains absent")
    c.require(git(root, "diff", "--name-only", BASE, "--", "native", "build-tvos.sh") == "",
              "accepted native sources and tvOS builder remain unchanged")
    c.require(git(root, "status", "--short", "FNA") == "", "FNA submodule remains clean")

    profiles = read(root, "managed/celeste-input-profiles.json")
    c.require(CONTENT in profiles, "canonical Content lock retained")
    c.require(RAW in profiles, "canonical raw-source lock retained")
    c.require(PATCHED in profiles, "canonical patched-source lock retained")
    c.require(STAGE6 in profiles, "canonical Stage 6 lock retained")
    c.require(IOS_NATIVE in read(root, "native/ios-native-output.lock.json"), "iOS native lock retained")
    c.require(TVOS_NATIVE in read(root, "build-tvos.sh"), "tvOS native lock retained")

    c.require("StableTouchSlotPolicy.Map" in fna_patch and "SetFingerSnapshot" in fna_patch,
              "iOS FNA patch preserves stable SDL identities")
    c.require("SDL_GetTouchFinger" in fna_patch and "finger->id" in fna_patch,
              "stable identity originates in public SDL finger IDs")
    c.require("Vacated" in stable and "must expose its Released" in stable,
              "vacated slots preserve one release edge")
    c.require("MAX_TOUCHES" in fna_patch and "MaximumTouches = 8" in policy,
              "FNA and policy support at least eight physical touches")
    c.require("0001-preserve-stable-touch-finger-ids.patch" in prepare_foundation,
              "tracked iOS-only patch is applied reproducibly")
    c.require("$(IOSHostStageRoot)\\fna" in fna_project and "StableTouchSlotPolicy.cs" in fna_project,
              "FNA build consumes only staged patched source plus pure mapping policy")
    c.require("TitleContainer.cs" in extension, "generated game does not duplicate FNA host-owned sources")

    for token in ("Automatic", "Always", "Off", "Fixed", "Floating", "Toggle", "HoldButton",
                  "ShoulderHold", "JumpDash"):
        c.require(token in policy, f"touch policy includes {token}")
    c.require("DeadzoneRatio = 0.18" in policy, "movement deadzone is exactly 0.18")
    c.require("HysteresisDegrees = 8.0" in policy, "direction hysteresis is exactly eight degrees")
    c.require("MaximumTouches = 8" in policy, "eight-owner touch policy")
    c.require("HasNonOverlappingActionHitAreas" in policy and "IsPad" in policy,
              "adaptive phone/iPad geometry validates non-overlap")
    c.require("1.18" in policy and "0.70" in policy and "1.30" in policy,
              "accepted iPad and size scaling constants retained")
    c.require("MovementAcquisition" in policy, "Floating movement uses a bounded activation region")
    c.require("owners.Any" in policy and "RecomputeHeld" in policy,
              "held state is recomputed across independent finger owners")
    c.require("ReleaseMissingOwners" in policy, "missing physical fingers fail safely to release")
    c.require("TouchSlideMode.JumpDash" in policy and "owner.Control is TouchOwnedControl.Jump or TouchOwnedControl.Dash" in policy,
              "optional Jump/Dash sliding is bounded to the two action buttons")
    c.require("GrabStyle == TouchGrabStyle.Toggle ? grabToggle : grabHold" in policy,
              "Toggle and hold Grab have distinct final-state semantics")
    c.require("touchControlsVisible && !physicalControllerConnected" in policy and
              "TouchGrabPolicy.Resolve" in transform and "ControllerGrabCheck" in transform,
              "touch-only Grab remains independent of controller Grab Mode")
    c.require("GrabActionPressed" in policy and "GrabActionPressed" in touch and
              "IOSTouchControls.GrabActionPressed" in transform,
              "touch Grab exposes a separate one-frame edge for vanilla hidden-input listeners")
    c.require("UnlockEverythingThingy.cs" in transform and "Input.Grab.Pressed || IOSTouchControls.GrabActionPressed" in transform,
              "vanilla unlock cheat accepts touch Grab without changing its sequence")
    c.require("Enum.IsDefined" not in policy + touch and "System.Reflection" not in policy + touch,
              "touch policy/application adds no reflection dependency")

    for key in ("Visibility", "Movement", "Grab", "Sliding", "Opacity", "Size", "Haptics"):
        c.require(f"CelesteIOS.TouchControls.{key}.v1" in policy, f"versioned {key} preference key")
    c.require("TouchControlVisibility.Automatic" in policy and "TouchMovementMode.Fixed" in policy and
              "TouchGrabStyle.Toggle" in policy and "70" in policy and "100" in policy,
              "accepted production defaults retained")
    c.require("if (next == preferences) return" in touch, "unchanged touch setting is a no-op")
    c.require("NSUserDefaults.StandardUserDefaults" in touch and "settings.celeste" not in touch and
              "SaveData" not in touch, "touch preferences are host-only and outside game persistence")
    c.require("ResetPreferences" in touch and "TouchPreferences.Default" in touch,
              "Reset Touch Controls restores only touch defaults")
    c.require("Math.Max(configured, 0.25f)" in touch,
              "zero-opacity editing has a temporary visible preview floor")
    c.require("ENABLE TOUCH CONTROLS" in touch and "NeedsRecovery" in touch,
              "Off mode retains a controller-free recovery affordance")

    c.require(all(token in transform for token in
                  ("IOSTouchControls.Update();", "MInput.Update();", "IOSTouchControls.MarkGameUpdate();")),
              "touch state reaches logical input immediately before MInput.Update")
    c.require("AdditionalCheck" in transform and "AdditionalPressed" in transform and
              "AdditionalReleased" in transform, "VirtualButton receives held/pressed/released touch delegates")
    c.require("AdditionalValue" in transform and "VirtualIntegerAxis" in transform and
              "VirtualJoystick" in transform, "axes and joysticks receive logical touch delegates")
    c.require("IOSTouchControls.LogicalVisible" in transform and
              "IOSTouchControls.PhysicalControllerConnected" in transform,
              "touch Grab arbitration receives visibility and controller state")
    c.require("IOSTouchControls.Render();" in transform, "overlay renders after Celeste's game surface")
    c.require("SamplerState.LinearClamp" in touch and "TextureSize = 128" in touch,
              "bounded FNA overlay assets use the existing Metal render path")
    c.require("PromptSize = 80" in touch and "VirtualContent.CreateTexture(name + \" Prompt\"" in touch,
              "inline touch prompts match Celeste's ordinary 80 x 80 glyph frame")
    c.require("UIImpactFeedbackStyle.Light" in touch and "TouchHapticAction" in touch,
              "light action-only touch haptics")
    c.require("latencySamples % 100" in touch and "buffered-frames=0" in touch,
              "privacy-safe bounded latency diagnostics")
    c.require("touch.Position.X / presentation.NativeScale" in touch,
              "native pixel touches are normalized to UIKit point geometry")

    for label in ("Touch Controls", "Movement Pad", "Grab Style", "Slide Jump / Dash", "Opacity",
                  "Control Size", "Touch Haptics", "Reset Touch Controls"):
        c.require(label in transform + touch_ui, f"Options contains {label}")
    c.require("new Slider" in touch_ui and "new OnOff" in touch_ui and
              "Pressed(OpenTouchControls)" in transform,
              "Options uses a dedicated existing Celeste TextMenu configuration surface")
    c.require("ONE HELD FINGER MAY SLIDE BETWEEN JUMP AND DASH" in touch_ui,
              "Jump/Dash sliding has an in-product explanation")
    c.require("new Oui" not in transform + touch_ui and "drag-to-position" not in transform + touch_ui,
              "D2 adds no new Oui or layout editor")

    c.require("SDL_GetWindowWMInfo" in presentation and "SDL_Metal_GetDrawableSize" in presentation,
              "presentation derives from the real SDL UIKit Metal window")
    c.require("window.Bounds.Width * nativeScale" in presentation and "SafeAreaInsets" in presentation,
              "drawable, UIKit bounds, native scale, and safe area are cross-validated")
    c.require("GraphicsDevice.Reset(corrected)" in presentation and "before" not in presentation.lower(),
              "initial backbuffer mismatch is corrected through FNA presentation parameters")
    c.require("dirty" in presentation and "Invalidate" in presentation,
              "presentation revalidates only at focused lifecycle boundaries")
    c.require("OrientationDidChangeNotification" in lifecycle and "DidBecomeActiveNotification" in lifecycle,
              "rotation and foreground invalidate presentation state")
    c.require("IOSTouchControls.Reset" in lifecycle and "IOSTouchControls.Dispose" in lifecycle,
              "background/disposal clear touch ownership")

    c.require("CelesteAppleInput.csproj" in transform and "AppleControllerPromptPolicy" in touch,
              "iOS product references one shared prompt policy")
    c.require("CelesteAppleInput" in tvos_policy and "AppleControllerPromptPolicy" in tvos_policy,
              "tvOS delegates the same pure family policy")
    c.require("ControllerPromptMode" in shared and "SelectAppleFamily" in shared and "ResolvePrefix" in shared,
              "shared policy owns family ordering and glyph resolution")
    c.require("GCController.DidConnectNotification" not in touch and "AddObserver" not in touch,
              "iOS does not create a second controller observer stack")
    c.require("DualSense" in touch and 'return "keyboard"' in touch,
              "DualSense classification and touch-neutral prompt policy are present")
    c.require("TouchPrompt(button)" in transform and "!IOSTouchControls.TouchPromptActive" in transform,
              "touch Confirm/Cancel does not impersonate an Xbox controller")
    c.require("BindButton(Input.MenuJournal, TouchAction.Journal)" in touch and
              "TouchAction.Journal => state.JournalPressed" in touch,
              "Journal/Special has a direct touch press route")
    c.require("ReferenceEquals(button, Input.MenuJournal)" in touch and "journalPrompt" in touch,
              "Journal/Special exposes a matching touch prompt")
    c.require(all(token in touch for token in (
                  "Input.Jump", "Input.Dash", "Input.Talk", "Input.Pause", "Input.MenuConfirm",
                  "Input.MenuCancel", "Input.MenuLeft", "Input.MenuRight", "Input.MenuUp",
                  "Input.MenuDown", "Input.MenuJournal", "Input.MoveX", "Input.MoveY",
                  "Input.GliderMoveY", "Input.Aim", "Input.Feather", "Input.MountainAim")),
              "all required Celeste logical input families have touch routes")

    jump_svg = root / "modern-ios/Assets/TouchControls/Source/jump.svg"
    dash_svg = root / "modern-ios/Assets/TouchControls/Source/dash.svg"
    grab_ungrabbed_svg = root / "modern-ios/Assets/TouchControls/Source/grab-ungrabbed.svg"
    grab_grabbed_svg = root / "modern-ios/Assets/TouchControls/Source/grab-grabbed.svg"
    touch_svg = root / "modern-ios/Assets/TouchControls/Source/touch.svg"
    pause_svg = root / "modern-ios/Assets/TouchControls/Source/pause.svg"
    journal_svg = root / "modern-ios/Assets/TouchControls/Source/journal.svg"
    c.require(sha256(jump_svg) == JUMP_SOURCE, "reviewed Jump Material Symbol source hash")
    c.require(sha256(dash_svg) == DASH_SOURCE, "reviewed Dash Material Symbol source hash")
    c.require(sha256(grab_ungrabbed_svg) == GRAB_UNGRABBED_SOURCE,
              "reviewed attributed ungrabbed source hash")
    c.require(sha256(grab_grabbed_svg) == GRAB_GRABBED_SOURCE,
              "reviewed attributed grabbed source hash")
    c.require(sha256(touch_svg) == TOUCH_SOURCE, "reviewed neutral Touch Material Symbol source hash")
    c.require(sha256(pause_svg) == PAUSE_SOURCE, "reviewed project Pause source hash")
    c.require(sha256(journal_svg) == JOURNAL_SOURCE, "reviewed Journal Material Symbol source hash")
    c.require("Apache License 2.0" in notice and "Material Symbols" in notice and "LICENSE" in notice,
              "Material Symbols provenance and licence are discoverable")
    c.require("Daniel Tacho" in notice and "5229550" in notice and
              "Creative Commons Attribution 3.0" in notice and "Copyright Daniel Tacho" in notice,
              "Noun Project Grab artwork attribution and modification notice are discoverable")
    generator = read(root, "scripts/generate-ios-touch-assets.py")
    source_locks = (JUMP_SOURCE, DASH_SOURCE, GRAB_UNGRABBED_SOURCE, GRAB_GRABBED_SOURCE,
                    TOUCH_SOURCE, PAUSE_SOURCE, JOURNAL_SOURCE)
    c.require(all(value in generator for value in source_locks) and "sips" in generator and ".a8" in generator,
              "tracked SVGs reproducibly generate runtime A8 assets")
    c.require("grabGrabbedTexture" in touch and "state.Grab ? grabGrabbedTexture : grabUngrabbedTexture" in touch,
              "Grab control uses distinct ungrabbed and active visual states")
    generated_assets = root / ".build/celeste-ios/current/touch-assets"
    if generated_assets.is_dir():
        c.require(sha256(generated_assets / "jump.a8") == JUMP, "generated Jump A8 lock")
        c.require(sha256(generated_assets / "dash.a8") == DASH, "generated Dash A8 lock")
        c.require(sha256(generated_assets / "grab-ungrabbed.a8") == GRAB_UNGRABBED,
                  "generated ungrabbed A8 lock")
        c.require(sha256(generated_assets / "grab-grabbed.a8") == GRAB_GRABBED,
                  "generated grabbed A8 lock")
        c.require(sha256(generated_assets / "touch.a8") == TOUCH, "generated neutral Touch A8 lock")
        c.require(sha256(generated_assets / "pause.a8") == PAUSE, "generated Pause A8 lock")
        c.require(sha256(generated_assets / "journal.a8") == JOURNAL, "generated Journal A8 lock")

    privacy = plistlib.loads((root / "modern-ios/CelesteIOSRuntimeHost/PrivacyInfo.xcprivacy").read_bytes())
    reasons = privacy["NSPrivacyAccessedAPITypes"]
    c.require(privacy["NSPrivacyTracking"] is False and privacy["NSPrivacyCollectedDataTypes"] == [],
              "touch feature adds no tracking or collected data")
    c.require(any(item["NSPrivacyAccessedAPIType"] == "NSPrivacyAccessedAPICategoryUserDefaults" and
                  "CA92.1" in item["NSPrivacyAccessedAPITypeReasons"] for item in reasons),
              "app-private touch preference has the accepted UserDefaults privacy reason")
    forbidden = touch + policy + presentation
    c.require(all(token not in forbidden for token in ("NWListener", "CoreMotion", "CMMotion", "CLLocation",
                                                        "Process.Start", "Environment.Exit")),
              "touch adds no network/sensor/process service")
    c.require(not git(root, "ls-files", ".build/celeste-ios", "artifacts/ios-celeste"),
              "generated/proprietary iOS outputs remain ignored")

    c.require("Status: **PASS**" in report and BASE in report and GENERATED in report,
              "tracked D2 acceptance report locks status, baseline, and generated tree")
    c.require("IOS_TOUCH_CONTROLS_STAGE24D2_REPORT.md" in history,
              "history index links the D2 acceptance record")
    readme_flat = " ".join(readme.lower().split())
    if (root / "modern-ios/IOSPortVersion.props").is_file():
        # E2 deliberately supersedes D2's then-correct public-builder boundary.
        # Keep the original assertion for historical D2 checkouts while requiring
        # the production iOS entry point and current maturity on E2 and later.
        c.require("./build-ios.sh" in readme and "release-candidate acceptance" in readme_flat,
                  "root README exposes the accepted production iOS workflow")
    else:
        c.require("adaptive touch controls" in readme_flat and
                  "beginner/public builder remains tvos-only" in readme_flat,
                  "root README accurately bounds experimental touch support")
    c.require("Options > Touch Controls" in ios_docs and "0.18" in ios_docs and "8-degree" in ios_docs,
              "current iOS guide documents the production touch controls")
    c.require("custom-layout editor" in report and "hold-to-release" in report,
              "D3 layout and fourth Grab-mode ideas remain explicitly deferred")

    c.require("celeste-ios-stage24d2.py" in prepare_runtime and "ios-managed-stage24d2.json" in prepare_runtime,
              "one canonical pipeline layers the narrow iOS D2 transform")
    c.require("ios-managed-stage24d2.json" in build, "D2 manifest drives the product build record")
    c.require(all(token in build for token in ("RunAOTCompilation=true", "MtouchLink=Full", "TrimMode=full",
                                               "UseInterpreter=false", "MtouchUseLlvm=true")),
              "product builder locks full AOT/trim/LLVM and no interpreter")
    c.require("_mono_jit_init" in package and "libmono-component-interpreter" in package,
              "package verifier rejects JIT/interpreter")
    c.require("touchUiIncluded\": args.product == \"celeste\"" in package and
              "CelesteIOS.TouchControls.Visibility.v1" in package,
              "package verifier requires production touch UI")
    c.require("MetalForceHudEnabled" in package and "_celeste-save._tcp" in package,
              "package verifier keeps tvOS-only HUD and Save Manager out of iOS")

    for label in ("array compaction", "crossed release", "eight-way direction", "four-finger multitouch",
                  "Jump held duration", "Dash one pressed edge", "Toggle Grab", "Hold Grab",
                  "Shoulder Hold", "touch-only Grab ignores controller Invert", "button sliding Off", "button sliding transfers", "lifecycle reset",
                  "Automatic hides", "Always coexists", "Off recovery", "iPad geometry",
                  "Journal/Special down edge", "Journal/Special release preserves",
                  "Grab action edge lasts one frame", "second tap clears and remains an action",
                  "DualSense Automatic PlayStation"):
        c.require(label in tests, f"deterministic touch coverage: {label}")

    generated = root / "artifacts/ios-celeste/current/ios-managed-stage24d2.json"
    if generated.exists():
        value = json.loads(generated.read_text())
        c.require(value["output"]["fileCount"] == 938, "D2 generated file count")
        c.require(value["output"]["logicalSha256"] == GENERATED, "D2 generated source lock")
        c.require(value["input"]["fileCount"] == 928, "D2 consumes exact C2 tree")
        c.require(value["touchDefaults"] == {
            "deadzone": 0.18, "grab": "Toggle", "haptics": True,
            "hysteresisDegrees": 8, "movement": "Fixed", "opacityPercent": 70,
            "sizePercent": 100, "sliding": "Off", "visibility": "Automatic",
        }, "D2 manifest locks accepted defaults")
        unlock = read(root, ".build/celeste-ios/current/managed/Celeste/UnlockEverythingThingy.cs")
        c.require("Input.Grab.Pressed || IOSTouchControls.GrabActionPressed" in unlock,
                  "generated vanilla unlock listener receives touch Grab edge")
        c.require('AddCheat("lrLRuudlRA", EnteredCheat)' in unlock,
                  "generated touch adaptation preserves the vanilla cheat sequence")

    app_candidates = sorted((root / "artifacts/ios-celeste/device/publish").glob("*.app"))
    build_manifest = root / "artifacts/ios-celeste/device/build-manifest.json"
    product_is_d2 = False
    if build_manifest.exists():
        product_value = json.loads(build_manifest.read_text())
        product_is_d2 = product_value.get("generated", {}).get("logicalSha256") == GENERATED
    if app_candidates and product_is_d2:
        app = app_candidates[0]
        c.require((app / "Celeste.dll").is_file(), "canonical Celeste ships in the physical product")
        c.require((app / "CelesteAppleInput.dll").is_file(), "shared prompt policy ships")
        c.require((app / "CelesteAppleInput.aotdata.arm64").is_file(), "shared prompt policy is AOT compiled")
        c.require(not (app / "libmono-component-interpreter.dylib").exists(), "interpreter absent from product")

    print(f"PASS: Stage 24D2 production iOS touch verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
