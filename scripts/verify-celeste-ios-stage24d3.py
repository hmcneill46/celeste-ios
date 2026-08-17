#!/usr/bin/env python3
"""Verify Stage 24D3 custom-layout and per-input Grab production invariants."""

from __future__ import annotations

import json
import os
import pathlib
import plistlib
import subprocess
import sys


BASE = "f00896a344ef03347d13a03977a7609961022aee"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
DEFERRED_RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
GENERATED = "99c9df179036e70c1e9a01dbcde129d3a9e5eef9495d3962f256c3cfa4d1b329"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"


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


def main() -> int:
    root = pathlib.Path(__file__).resolve().parents[1]
    c = Checks()
    policy = read(root, "modern-ios/CelesteIOSFoundation/CustomTouchLayoutPolicy.cs")
    legacy_policy = read(root, "modern-ios/CelesteIOSFoundation/TouchControlsPolicy.cs")
    touch = read(root, "managed/templates/IOSTouchControlsD3.cs")
    ui = read(root, "managed/templates/IOSTouchControlsUID3.cs")
    source_option = read(root, "managed/templates/IOSSourceGrabModeOption.cs")
    transform = read(root, "scripts/celeste-ios-stage24d3.py")
    prepare = read(root, "scripts/prepare-celeste-ios-runtime.sh")
    build = read(root, "scripts/build-ios-celeste.sh")
    package = read(root, "scripts/verify-ios-package.py")
    tests = read(root, "modern-ios/CelesteIOSFoundationTests/Program.cs")

    c.require(git(root, "rev-parse", f"{BASE}^{{commit}}") == BASE, "Stage 24D3 baseline exists")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "RC1 preserved")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "RC2 preserved")
    c.require(git(root, "rev-parse", "origin/release/v1.0.0-rc.3^{commit}") == DEFERRED_RC3,
              "deferred RC3 preserved")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 tag remains absent")
    c.require(git(root, "diff", "--name-only", BASE, "--", "native", "build-tvos.sh", "tvos") == "",
              "tvOS product/native sources are unchanged")
    c.require(git(root, "diff", "--name-only", BASE, "--", ".github") == "", "GitHub Actions untouched")
    c.require(git(root, "status", "--short", "FNA") == "", "FNA submodule remains clean")

    for token in ("TouchLayoutProfile", "CurrentSchemaVersion = 2", "Phone", "Tablet",
                  "Movement", "FloatingRegion", "Jump", "Dash", "SplitRegion", "Grab", "Pause", "Journal"):
        c.require(token in policy + touch, f"D3 layout surface includes {token}")
    c.require("Normalize(" in policy and "Denormalize(" in policy and "FullCanvas" in policy,
              "layout coordinates are normalized to full physical-screen geometry")
    c.require("TouchLayoutCodec" in policy and "CultureInfo.InvariantCulture" in policy,
              "layout codec is deterministic and culture independent")
    c.require("System.Text.Json" not in policy + touch and "System.Reflection" not in policy + touch,
              "layout persistence adds no reflection serializer")
    c.require("value.Length is < 20 or > 4096" in policy and "IsStructurallyValid" in policy,
              "persisted layout input is bounded and structurally validated")
    c.require("HitRegionsOverlap" in policy and "CircleGeometry" in policy and "RectsOverlap" in policy,
              "validation uses true circle/rectangle hit regions")
    c.require("must stay inside the screen" in policy and "is too small" in policy and "overlaps" in policy,
              "bounds, minimum size, and overlap failures are explicit")
    c.require("LegacyPrefix = \"D3|1|\"" in policy and "RebaseFromSafeArea" in policy and
              "Layout.Phone.v2" in touch and "LegacyPhoneLayoutKey" in touch,
              "v1 safe-area layouts migrate once into v2 full-screen layouts")

    for token in ("BeginGesture", "PreviewRect", "EndGesture", "MaximumUndo = 32", "ResetSelected",
                  "ResetLayout", "Mirror", "TryCommit"):
        c.require(token in policy, f"transactional editor policy includes {token}")
    c.require("Original" in policy and "Working" in policy and "value = validation.IsValid ? Working : Original" in policy,
              "Cancel/failed Done preserve the original profile")
    c.require("DRAG CONTROL TO MOVE" in touch and "DRAG WHITE CORNER TO RESIZE" in touch,
              "editor provides concise in-product guidance")
    c.require("•" not in touch,
              "editor copy avoids SpriteFont-unsupported typographic bullets")
    for token in ("DONE", "CANCEL", "UNDO", "FACTORY", "MIRROR", "RESET", "ADD CONTROL",
                  "DUPLICATE", "DELETE", "OPACITY -", "OPACITY +"):
        c.require(token in touch, f"editor command {token}")
    c.require("EditorControlIsCircle" in touch and "Draw.Circle" in touch and "ResizeHandle" in touch,
              "editor shows true hit shapes and a visible resize handle")
    c.require("TouchLayoutEditorGeometry.Resize" in touch and "if (!circle)" in policy and
              "dominantDelta" in policy,
              "rectangles resize freely while circles retain physical circular geometry")
    c.require("editorToolsVisible" in touch and "SHOW TOOLS" in touch and "MOVE DOWN" in touch and
              "EditorDockBounds" in touch,
              "readable editor tools can collapse and move between screen edges")
    c.require("DrawEditorSplitIcons" in touch and "jumpTexture" in touch and "dashTexture" in touch,
              "split editor labels both halves with the actual Jump and Dash glyphs")
    c.require("MovementMode = Working.MovementMode" in policy and
              "ActionLayout = Working.ActionLayout" in policy and "Sliding = Working.Sliding" in policy,
              "Factory restores geometry without contradicting the surrounding Options rows")
    editor_update = touch[touch.index("private static bool D3UpdateEditorIfActive"):
                          touch.index("private static void UpdateEditorTouch")]
    c.require(editor_update.index("UpdateEditorTouch(touch.Id, phase, point)") <
              editor_update.index("touch.Id == editorFinger"),
              "new editor touch ownership is observed after Pressed can claim it")
    c.require("PixelRect(full)" in touch and "PixelRect(safe)" in touch and "Safe area is guidance" in touch,
              "editor uses full screen while retaining a faint safe-area guide")
    c.require("!validation.IsValid ? Color.OrangeRed" in touch,
              "invalid geometry is highlighted as well as explained")
    c.require("EditorCommand.Done => validation.IsValid" in touch,
              "invalid layouts disable Done")
    c.require("TouchLayoutPolicy.VisualRatio(profile, selection)" in touch and "MovementVisualRatio" in policy and
              "The filled disk is the exact runtime visual size" in touch,
              "editor solid circles exactly match runtime visuals while exposing hit margins")
    c.require("ConstrainToScreen" in policy + touch and "visible disk is constrained" in policy and
              "larger hit margin is clipped" in tests,
              "circles can sit visually flush while their larger hit regions clip at display edges")
    c.require("TouchControlOpacityProfile" in policy and "OpacityPercent" in policy and
              "D3ControlAlpha" in touch and "editor changes one duplicate opacity" in tests,
              "each primary and optional control has independent runtime opacity")
    c.require("MaximumExtraControls = 4" in policy and "TryAddExtra" in policy and
              "AddCrouchDash" in touch and "CrouchDash" in legacy_policy and
              "crouch-dash.a8" in transform + package,
              "bounded optional controls include dedicated Crouch Dash input and artwork")
    c.require("AddQuickRestart" in touch and "TouchExtraControlKind.QuickRestart" in policy and
              "Input.QuickRestart" in transform and "restart.a8" in transform + package,
              "all distinct useful Celeste actions include optional Quick Restart with dedicated artwork")
    c.require("SameLogicalAction" in policy and "!logicalAlreadyHeld" in policy and
              "second simultaneous Grab owner does not double-toggle" in tests,
              "duplicate actions use aggregate last-owner-release semantics")
    c.require("selection.Primary != TouchLayoutControl.Journal" in policy and
              "cannot delete essential Movement or Pause" in tests,
              "only optional controls and Journal can be deleted")

    for token in ("TopLeftToBottomRight", "TopRightToBottomLeft", "Vertical", "Horizontal"):
        c.require(token in policy + touch, f"split orientation {token}")
    c.require("JumpOnFirstHalf" in policy and "EditorCommand.Swap" in touch,
              "split Jump/Dash assignment is swappable")
    c.require("DrawSplitHalf" in touch and "firstPressed" in touch and "secondPressed" in touch,
              "each split half receives its own pressed-state tint")
    c.require("signed >= 0" in policy and "SplitTransferHysteresisPoints = 4.0" in policy,
              "split-line ownership is deterministic with narrow sliding hysteresis")
    c.require("TouchSlideMode.JumpDash" in policy and "SplitControl" in policy,
              "Button Sliding applies to Split Region")
    c.require("TouchActionLayout.SeparateButtons" in policy and "TouchActionLayout.SplitRegion" in policy,
              "Separate and Split action layouts coexist")

    c.require("TouchGrabShape.Circle" in policy + touch and "TouchGrabShape.Rectangle" in policy + touch,
              "Grab supports circle and arbitrary rectangle geometry")
    c.require("count > 1 ? name + \" \" + ordinal : name" in touch and
              "selectedExtraIndex + 2" not in touch,
              "editor labels number only real same-action duplicates, never internal extra slots")
    build_script = read(root, "scripts/build-ios-celeste.sh")
    c.require("prepared iOS Celeste source is stale" in build_script and
              "cmp -s" in build_script and "IOSTouchControlsD3.cs:Celeste/IOSTouchControlsD3.cs" in build_script,
              "device build refuses a stale generated D3 bridge after template changes")
    c.require("D3DrawGrab" in touch and "grabGrabbedTexture" in touch and "grabUngrabbedTexture" in touch,
              "both Grab geometries retain effective-state icon art")
    c.require("ShoulderPreset" in touch and "legacy.Shoulder" in touch,
              "accepted shoulder rectangle remains an editor preset")
    c.require("Grab Style" not in ui and "Control Size" not in ui,
              "obsolete D2 Grab Style and global size rows are absent")
    c.require("GRAB BEHAVIOUR USES CELESTE'S SOURCE-AWARE GRAB MODE" in ui,
              "Touch settings explain the geometry/behavior boundary")

    for key in ("Layout.Phone.v2", "Layout.Tablet.v2", "Layout.Phone.v1", "Layout.Tablet.v1", "GrabMode.Touch.v1",
                "GrabMode.Controller.v1", "GrabMode.Keyboard.v1", "D3Migrated.v1"):
        c.require(key in touch, f"versioned host preference {key}")
    c.require("TouchD2MigrationPolicy.Migrate" in touch and "legacy.SizePercent / 100.0" in policy,
              "D2 size and Grab state migrate once into D3")
    c.require("TouchGrabStyle.ShoulderHold" in policy and "TouchGrabShape.Rectangle" in policy,
              "D2 shoulder migration produces Hold plus rectangle")
    c.require("ResetD3Preferences" in ui + touch and "SaveGrabProfiles" not in ui,
              "factory touch reset does not reset per-source Grab profiles")
    c.require("settings.celeste" not in touch and "SaveData" not in touch,
              "layout/Grab profiles remain outside Celeste persistence")

    for token in ("AppleInputSource.Touch", "AppleInputSource.Controller", "AppleInputSource.Keyboard",
                  "AppleGrabMode.Hold", "AppleGrabMode.Invert", "AppleGrabMode.Toggle"):
        c.require(token in policy + touch, f"source-aware Grab includes {token}")
    c.require("GrabSourceArbiter" in policy and "Array.Clear(toggle)" in policy,
              "source switches clear stale Toggle latches")
    c.require("D3GameplayGrab" in touch and "GrabSourceVisibilityPolicy.GameplayValue" in touch and
              "initialized && D3GameplayGrab" in transform,
              "controller/keyboard Grab is independent of touch-overlay visibility")
    c.require("meaningful && source != ActiveSource" in policy and "Crossed(" in touch,
              "only meaningful edges/threshold crossings switch hardware source")
    c.require("KeyboardHasNewPress" in touch and ".CurrentState.GetPressedKeys()" not in touch,
              "keyboard source arbitration is bounded and allocation-free per update")
    c.require("raw[(int)source]" in policy and "AppleGrabMode.Invert => !raw" in policy,
              "Invert uses the active source raw state")
    c.require("IOSSourceGrabModeOption" in source_option + transform and "ActiveGrabSourceName" in source_option,
              "vanilla Grab Mode row identifies and edits the current source")
    c.require("Settings.Instance.GrabMode =" not in source_option and "SetActiveGrabMode" in source_option,
              "source profile changes do not rewrite canonical Settings continuously")
    generated_input = root / ".build/celeste-ios/current/managed/Celeste/Input.cs"
    c.require("public static bool GrabCheck => IOSTouchControls.Grab" in transform and
              (not generated_input.exists() or "TouchGrabPolicy.Resolve" not in generated_input.read_text()),
              "final Grab state comes from exactly one active source, never an OR")

    c.require("Directional Haptics" in ui and "DirectionalHaptics.v1" in touch,
              "optional directional haptic setting is present")
    c.require("DirectionalHapticPolicy.ShouldPulse" in touch and "after != TouchDirection.Neutral && after != before" in policy,
              "direction haptics pulse only on initial/changed non-neutral sectors")
    c.require('directionalHapticsDefault": False' in transform,
              "directional haptics default Off preserves D2 behavior")
    c.require("ImpactOccurred" in touch and "UIImpactFeedbackStyle.Light" in read(root, "managed/templates/IOSTouchControls.cs"),
              "direction feedback reuses the accepted light public haptic path")

    for label in ("Touch Controls", "Movement Pad", "Action Layout", "Button Sliding", "Opacity",
                  "Touch Haptics", "Directional Haptics", "Edit Layout", "Reset Touch Controls"):
        c.require(label in ui, f"D3 Touch Controls page includes {label}")
    c.require("Reset Touch Controls" in ui and "Press Again to Restore Factory Touch Layout" in ui,
              "factory reset requires confirmation")
    c.require("new Oui" not in touch + ui + transform and "System.Reflection" not in touch + ui,
              "D3 adds no reflected Oui or dynamic runtime mechanism")
    c.require("ActiveFont.DrawOutline" in touch and "EditorTextScale" in touch and
              "ActiveFont.Measure" in touch,
              "editor text uses Celeste's readable menu font with bounded auto-fitting")

    c.require("celeste-ios-stage24d2.py" in prepare and "celeste-ios-stage24d3.py" in prepare,
              "one canonical pipeline retains D2 then layers D3")
    c.require("ios-managed-stage24d2.json" in build and "ios-managed-stage24d3.json" in build,
              "build preserves inherited D2 evidence and consumes D3 product manifest")
    c.require("IOSTouchControls.UpdateHardwareSources();" in transform and
              transform.index("MInput.Update();") < transform.index("IOSTouchControls.UpdateHardwareSources();"),
              "source arbiter runs immediately after hardware input update")
    c.require("RunAOTCompilation=true" in build and "MtouchLink=Full" in build and
              "UseInterpreter=false" in build and "MtouchUseLlvm=true" in build,
              "full-AOT/trim/LLVM/no-interpreter product gate retained")
    c.require("managed/bin" in build and "managed/obj" in build and
              'find "$build_root" -depth -delete' in build,
              "clean iOS product build invalidates external generated-project AOT intermediates")
    c.require(all(token in package for token in ("Layout.Phone.v2", "Layout.Tablet.v2",
                                                  "Layout.Phone.v1", "Layout.Tablet.v1",
                                                  "GrabMode.Touch.v1", "DirectionalHaptics.v1")),
              "package verifier requires D3 product tokens")
    c.require("_mono_jit_init" in package and "libmono-component-interpreter" in package,
              "package verifier still rejects JIT/interpreter")

    c.require(IOS_NATIVE in read(root, "native/ios-native-output.lock.json"), "iOS native hash unchanged")
    c.require(TVOS_NATIVE in read(root, "build-tvos.sh"), "tvOS native hash unchanged")
    privacy = plistlib.loads((root / "modern-ios/CelesteIOSRuntimeHost/PrivacyInfo.xcprivacy").read_bytes())
    reasons = privacy["NSPrivacyAccessedAPITypes"]
    c.require(any(item["NSPrivacyAccessedAPIType"] == "NSPrivacyAccessedAPICategoryUserDefaults" and
                  "CA92.1" in item["NSPrivacyAccessedAPITypeReasons"] for item in reasons),
              "host-only D3 preferences retain accepted UserDefaults reason")
    c.require(not git(root, "ls-files", ".build/celeste-ios", "artifacts/ios-celeste"),
              "generated/proprietary D3 output remains ignored")
    c.require("Apache License 2.0" in read(root, "modern-ios/Assets/TouchControls/NOTICE.md") and
              "autorenew" in read(root, "modern-ios/Assets/TouchControls/NOTICE.md") and
              "motion_blur" in read(root, "modern-ios/Assets/TouchControls/NOTICE.md") and
              "Daniel Tacho" in read(root, "modern-ios/Assets/TouchControls/NOTICE.md"),
              "accepted touch artwork licenses remain discoverable")

    foundation_project = root / "modern-ios/CelesteIOSFoundationTests/CelesteIOSFoundationTests.csproj"
    foundation_environment = dict(os.environ, MSBuildEnableWorkloadResolver="false")
    subprocess.check_call(
        ["dotnet", "build", str(foundation_project), "-c", "Release", "--no-restore", "-maxcpucount:1"],
        env=foundation_environment,
    )
    foundation = subprocess.check_output(
        ["dotnet", "exec", str(foundation_project.parent / "bin/Release/net10.0/CelesteIOSFoundationTests.dll")],
        text=True,
        env=foundation_environment,
    )
    c.require("PASS: modern iOS foundation deterministic tests 226" in foundation,
              "D2 inherited coverage plus D3 full-screen/duplicate semantics pass")
    for label in ("layout serialization round trip", "factory Split Region validates", "editor Undo",
                  "off-screen control rejected", "TL-BR exact split line", "split Jump/Dash assignment swaps",
                  "Factory geometry preserves Movement", "rectangle corner independently changes width and height",
                  "circle corner resize preserves a physical circle", "D2 Toggle migration", "Touch Toggle latches",
                  "Keyboard Invert", "directional haptic pulses", "physical screen outside",
                  "second simultaneous Grab owner", "fifth optional control", "exact phone movement visual ratio",
                  "exact tablet movement visual ratio", "controller Grab remains active",
                  "hidden touch cannot leave Invert Grab", "visible circular control can sit flush",
                  "optional Quick Restart emits"):
        c.require(label in tests, f"semantic D3 test coverage: {label}")

    generated = root / "artifacts/ios-celeste/current/ios-managed-stage24d3.json"
    if generated.exists():
        value = json.loads(generated.read_text())
        c.require(value["input"]["fileCount"] == 938 and
                  value["input"]["logicalSha256"] == "5ca5e70d1fc163aa75db780d1cb7fc35b9328eea42369b91a4a2776dd8651036",
                  "D3 consumes exact accepted D2 tree")
        c.require(value["output"]["fileCount"] == 942 and value["output"]["logicalSha256"] == GENERATED,
                  "D3 generated tree lock")
        c.require(value["layoutSchema"] == 2 and value["profiles"] == ["Phone", "Tablet"],
                  "D3 manifest locks layout schema/form factors")

    report_path = root / "docs/history/stages/IOS_CUSTOM_TOUCH_LAYOUT_STAGE24D3_REPORT.md"
    if report_path.exists():
        report = report_path.read_text()
        c.require("Status: **PASS**" in report and BASE in report and GENERATED in report,
                  "D3 report records accepted status and hashes")
        c.require("Directional Haptics" in report, "D3 report records user-requested direction feedback")
        c.require("IOS_CUSTOM_TOUCH_LAYOUT_STAGE24D3_REPORT.md" in read(root, "docs/history/README.md"),
                  "history index links D3 report")

    print(f"PASS: Stage 24D3 custom touch layout verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
