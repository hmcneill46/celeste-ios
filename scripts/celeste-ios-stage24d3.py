#!/usr/bin/env python3
"""Apply the locked Stage 24D3 custom-layout and source-aware Grab transform."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import shutil


EXPECTED_INPUT_FILES = 938
EXPECTED_INPUT_HASH = "5ca5e70d1fc163aa75db780d1cb7fc35b9328eea42369b91a4a2776dd8651036"


def digest(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def manifest(root: pathlib.Path) -> dict[str, object]:
    records: list[dict[str, object]] = []
    logical = hashlib.sha256()
    for path in sorted(root.rglob("*"), key=lambda item: item.relative_to(root).as_posix()):
        if not path.is_file() or any(part in {"bin", "obj"} for part in path.relative_to(root).parts):
            continue
        relative = path.relative_to(root).as_posix()
        sha = digest(path)
        records.append({"path": relative, "size": path.stat().st_size, "sha256": sha})
        logical.update(relative.encode() + b"\0" + sha.encode() + b"\n")
    return {"fileCount": len(records), "logicalSha256": logical.hexdigest(), "files": records}


def replace_exact(path: pathlib.Path, old: str, new: str, label: str) -> None:
    text = path.read_text()
    if text.count(old) != 1:
        raise SystemExit(f"error: {label} source lock changed (matches={text.count(old)})")
    path.write_text(text.replace(old, new))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=pathlib.Path, required=True)
    parser.add_argument("--templates", type=pathlib.Path, required=True)
    parser.add_argument("--asset-dir", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve()
    before = manifest(root)
    if (before["fileCount"], before["logicalSha256"]) != (EXPECTED_INPUT_FILES, EXPECTED_INPUT_HASH):
        raise SystemExit(f"error: Stage 24D3 input tree changed: {before['fileCount']} {before['logicalSha256']}")

    touch = root / "Celeste" / "IOSTouchControls.cs"
    replace_exact(touch, "public static class IOSTouchControls", "public static partial class IOSTouchControls", "partial touch coordinator")
    replace_exact(touch, "private static TouchInteractionState state;\n    private static TouchControlLayout layout;",
                  "private static CustomTouchInteractionState state;\n    private static TouchRuntimeLayout layout;", "D3 runtime state types")
    replace_exact(touch,
        "    internal static bool Grab => state != null && logicalVisible && state.Grab;",
        "    internal static bool Grab => initialized && D3GameplayGrab;",
        "source-aware effective Grab")
    replace_exact(touch,
        "            controllerConnected = connected;\n            Reset(connected ? \"controller-connected\" : \"controller-disconnected\");",
        "            controllerConnected = connected;\n            Reset(connected ? \"controller-connected\" : \"controller-disconnected\");\n            D3ControllerConnectionChanged(connected);",
        "controller source boundary")
    replace_exact(touch,
        "        state.BeginFrame();\n        TouchCollection touches = TouchPanel.GetState();",
        "        if (D3UpdateEditorIfActive()) return;\n\n        state.BeginFrame();\n        TouchCollection touches = TouchPanel.GetState();",
        "layout editor input ownership")
    replace_exact(touch,
        "            TouchHapticAction haptic = state.Apply(touch.Id, phase, point, preferences.Haptics);",
        "            TouchHapticAction haptic = D3ApplyTouch(touch.Id, phase, point);",
        "directional haptic edge")
    replace_exact(touch,
        "        recoveryOwner = -1;\n        pendingTouchTick = 0;",
        "        recoveryOwner = -1;\n        pendingTouchTick = 0;\n        ResetGrabArbiter();",
        "source-aware Grab lifecycle reset")
    replace_exact(touch,
'''        if (NeedsRecovery)
            DrawRecovery();
        if (visualVisibility > 0f)
        {
            float configured = preferences.OpacityPercent / 100f;
            if (editing) configured = Math.Max(configured, 0.25f);
            float alpha = configured * visualVisibility;
            DrawMovement(alpha);
            DrawAction(layout.Jump, jumpTexture, state.Jump, new Color(124, 219, 255), alpha);
            DrawAction(layout.Dash, dashTexture, state.Dash, new Color(255, 104, 146), alpha);
            if (state.GrabStyle == TouchGrabStyle.ShoulderHold) DrawShoulder(alpha);
            else DrawGrab(alpha);
            DrawPause(alpha);
            DrawJournal(alpha);
        }''',
'''        if (D3EditorActive)
        {
            D3RenderEditor();
        }
        else
        {
            if (NeedsRecovery) DrawRecovery();
            if (visualVisibility > 0f)
            {
                float configured = preferences.OpacityPercent / 100f;
                if (editing) configured = Math.Max(configured, 0.25f);
                float alpha = configured * visualVisibility;
                TouchLayoutControl movementVisual = layout.MovementMode == TouchMovementMode.Fixed
                    ? TouchLayoutControl.Movement : TouchLayoutControl.FloatingRegion;
                DrawMovement(D3ControlAlpha(movementVisual, alpha));
                if (layout.ActionLayout == TouchActionLayout.SplitRegion)
                    D3DrawSplit(D3ControlAlpha(TouchLayoutControl.SplitRegion, alpha));
                else
                {
                    DrawAction(layout.Jump, jumpTexture, state.Jump, new Color(124, 219, 255),
                        D3ControlAlpha(TouchLayoutControl.Jump, alpha));
                    DrawAction(layout.Dash, dashTexture, state.Dash, new Color(255, 104, 146),
                        D3ControlAlpha(TouchLayoutControl.Dash, alpha));
                }
                D3DrawGrab(D3ControlAlpha(TouchLayoutControl.Grab, alpha));
                DrawPause(D3ControlAlpha(TouchLayoutControl.Pause, alpha));
                if (layout.Profile.JournalEnabled)
                    DrawJournal(D3ControlAlpha(TouchLayoutControl.Journal, alpha));
                D3DrawExtras(alpha);
            }
        }''',
        "D3 overlay rendering")
    replace_exact(touch,
        "        BindButton(Input.Dash, TouchAction.Dash);",
        "        BindButton(Input.Dash, TouchAction.Dash);\n"
        "        BindButton(Input.CrouchDash, TouchAction.CrouchDash);\n"
        "        BindButton(Input.QuickRestart, TouchAction.QuickRestart);",
        "optional Crouch Dash and Quick Restart bindings")
    replace_exact(touch,
        "            TouchAction.Dash => state.Dash,",
        "            TouchAction.Dash => state.Dash,\n"
        "            TouchAction.CrouchDash => state.CrouchDash,\n"
        "            TouchAction.QuickRestart => state.QuickRestart,",
        "optional action held state")
    replace_exact(touch,
        "            TouchAction.Dash => state.DashPressed,",
        "            TouchAction.Dash => state.DashPressed,\n"
        "            TouchAction.CrouchDash => state.CrouchDashPressed,\n"
        "            TouchAction.QuickRestart => state.QuickRestartPressed,",
        "optional action pressed state")
    replace_exact(touch,
        "            TouchAction.Dash => state.DashReleased,",
        "            TouchAction.Dash => state.DashReleased,\n"
        "            TouchAction.CrouchDash => state.CrouchDashReleased,\n"
        "            TouchAction.QuickRestart => state.QuickRestartReleased,",
        "optional action released state")
    replace_exact(touch,
        "    private enum TouchAction { Jump, Dash, Pause, Journal, Left, Right, Up, Down }",
        "    private enum TouchAction { Jump, Dash, CrouchDash, QuickRestart, Pause, Journal, Left, Right, Up, Down }",
        "optional touch action kinds")

    replace_exact(touch,
        "    private static Texture2D journalTexture;",
        "    private static Texture2D journalTexture;\n    private static Texture2D restartTexture;\n    private static Texture2D crouchDashTexture;",
        "Quick Restart texture field")
    replace_exact(touch,
        '        (journalTexture, journalPrompt) = LoadAlphaTexture("Celeste.IOSTouchControls.journal.a8", "iOS Touch Journal");',
        '        (journalTexture, journalPrompt) = LoadAlphaTexture("Celeste.IOSTouchControls.journal.a8", "iOS Touch Journal");\n'
        '        (restartTexture, _) = LoadAlphaTexture("Celeste.IOSTouchControls.restart.a8", "iOS Touch Quick Restart");\n'
        '        (crouchDashTexture, _) = LoadAlphaTexture("Celeste.IOSTouchControls.crouch-dash.a8", "iOS Touch Crouch Dash");',
        "Quick Restart texture load")
    replace_exact(touch,
        "    public static int MovementIndex { get { EnsureInitialized(); return (int)preferences.Movement; } }",
        "    public static int MovementIndex { get { EnsureInitialized(); return (int)layoutProfile.MovementMode; } }",
        "D3 movement option")
    replace_exact(touch,
        "    public static bool SlidingEnabled { get { EnsureInitialized(); return preferences.Sliding == TouchSlideMode.JumpDash; } }",
        "    public static bool SlidingEnabled { get { EnsureInitialized(); return layoutProfile.Sliding == TouchSlideMode.JumpDash; } }",
        "D3 sliding option")
    replace_exact(touch,
'''    public static void SetMovement(int value)
    {
        TouchMovementMode mode = value is >= 0 and <= 1
            ? (TouchMovementMode)value : TouchPreferences.Default.Movement;
        UpdatePreferences(preferences with { Movement = mode }, TouchPreferencePolicy.MovementKey, TouchPreferencePolicy.Store(mode));
    }''',
'''    public static void SetMovement(int value)
    {
        EnsureInitialized();
        D3SetMovement(value);
    }''',
        "D3 movement persistence")
    replace_exact(touch,
'''    public static void SetSliding(bool enabled) => UpdatePreferences(
        preferences with { Sliding = enabled ? TouchSlideMode.JumpDash : TouchSlideMode.Off },
        TouchPreferencePolicy.SlidingKey,
        TouchPreferencePolicy.Store(enabled ? TouchSlideMode.JumpDash : TouchSlideMode.Off));''',
'''    public static void SetSliding(bool enabled)
    {
        EnsureInitialized();
        D3SetSliding(enabled);
    }''',
        "D3 sliding persistence")
    replace_exact(touch,
        "    public static void ResetPreferences()\n    {",
        "    // Retained only as a migration-compatible D2 API. The D3 UI calls ResetD3Preferences.\n    public static void ResetPreferences()\n    {",
        "legacy reset marker")
    replace_exact(touch,
'''        presentation = IOSPresentationCoordinator.Current;
        preferences = LoadPreferences();
        promptMode = AppleControllerPromptPolicy.ParseStored(Defaults.StringForKey(PromptPreferenceKey));
        layout = TouchControlLayout.Create(
            presentation.PointWidth, presentation.PointHeight, presentation.SafeArea,
            presentation.IsPad, preferences.SizePercent / 100.0);
        state = new TouchInteractionState(layout);
        ApplyPreferences();
        controllerConnected = GamePad.GetState(PlayerIndex.One).IsConnected;''',
'''        presentation = IOSPresentationCoordinator.Current;
        preferences = LoadPreferences();
        promptMode = AppleControllerPromptPolicy.ParseStored(Defaults.StringForKey(PromptPreferenceKey));
        controllerConnected = GamePad.GetState(PlayerIndex.One).IsConnected;
        D3LoadAndMigrate();
        layout = D3CreateRuntimeLayout();
        state = new CustomTouchInteractionState(layout);
        ApplyPreferences();''',
        "D3 startup migration")
    replace_exact(touch,
'''    private static void ApplyPreferences()
    {
        if (state == null) return;
        state.MovementMode = preferences.Movement;
        state.GrabStyle = preferences.Grab;
        state.SlideMode = preferences.Sliding;
        if (presentation.IsValid) ApplyLayout();
        else state.Reset();
    }

    private static void ApplyLayout()
    {
        layout = TouchControlLayout.Create(
            presentation.PointWidth, presentation.PointHeight, presentation.SafeArea,
            presentation.IsPad, preferences.SizePercent / 100.0);
        state?.UpdateLayout(layout);
        if (state != null)
        {
            state.MovementMode = preferences.Movement;
            state.GrabStyle = preferences.Grab;
            state.SlideMode = preferences.Sliding;
        }
    }''',
'''    private static void ApplyPreferences()
    {
        if (state == null) return;
        if (presentation.IsValid) D3ApplyLayout();
        else state.Reset();
    }

    private static void ApplyLayout() => D3ApplyLayout();''',
        "D3 normalized layout application")
    replace_exact(touch,
        "        TouchPoint center = state.MovementMode == TouchMovementMode.Floating && state.Direction != TouchDirection.Neutral",
        "        TouchPoint center = layout.MovementMode == TouchMovementMode.Floating && state.Direction != TouchDirection.Neutral",
        "D3 floating visual")
    replace_exact(touch,
'''    private static void DrawGrab(float alpha)
    {
        float radius = Pixels(layout.Grab.Radius);
        Draw.SpriteBatch.Draw(circleTexture, CircleDestination(layout.Grab.Center, radius),
            (state.Grab ? new Color(255, 188, 91) : new Color(18, 24, 42)) * ((state.Grab ? 0.82f : 0.58f) * alpha));
        Texture2D icon = state.Grab ? grabGrabbedTexture : grabUngrabbedTexture;
        Draw.SpriteBatch.Draw(icon, CircleDestination(layout.Grab.Center, radius * 0.58f), Color.White * (0.90f * alpha));
    }

    private static void DrawShoulder(float alpha)
    {
        Rectangle rect = PixelRect(layout.Shoulder);
        Draw.Rect(rect, (state.Grab ? new Color(255, 188, 91) : new Color(18, 24, 42)) * (0.52f * alpha));
        Draw.TextCentered(Draw.DefaultFont, "GRAB", new Vector2(rect.Center.X, rect.Center.Y),
            Color.White * (0.80f * alpha), (float)(0.45 * presentation.NativeScale));
    }

''', "", "D3 Grab renderer")

    shutil.copyfile(args.templates / "IOSTouchControlsD3.cs", root / "Celeste" / "IOSTouchControlsD3.cs")
    shutil.copyfile(args.templates / "IOSTouchControlsUID3.cs", root / "Celeste" / "IOSTouchControlsUI.cs")
    shutil.copyfile(args.templates / "IOSSourceGrabModeOption.cs", root / "Celeste" / "IOSSourceGrabModeOption.cs")
    for name in ("restart", "crouch-dash"):
        asset = args.asset_dir / f"{name}.a8"
        if asset.stat().st_size != 128 * 128:
            raise SystemExit(f"error: generated touch asset has wrong size: {name}.a8")
        shutil.copyfile(asset, root / "Celeste" / "TouchAssets" / f"{name}.a8")
    project = root / "Celeste.Modern.csproj"
    replace_exact(project,
        '    <EmbeddedResource Include="Celeste/TouchAssets/journal.a8" LogicalName="Celeste.IOSTouchControls.journal.a8" />',
        '    <EmbeddedResource Include="Celeste/TouchAssets/journal.a8" LogicalName="Celeste.IOSTouchControls.journal.a8" />\n'
        '    <EmbeddedResource Include="Celeste/TouchAssets/restart.a8" LogicalName="Celeste.IOSTouchControls.restart.a8" />\n'
        '    <EmbeddedResource Include="Celeste/TouchAssets/crouch-dash.a8" LogicalName="Celeste.IOSTouchControls.crouch-dash.a8" />',
        "Quick Restart and Crouch Dash embedded resources")

    engine = root / "Monocle" / "Engine.cs"
    replace_exact(engine,
        "\t\tIOSTouchControls.Update();\n\t\tMInput.Update();\n\t\tIOSTouchControls.MarkGameUpdate();",
        "\t\tIOSTouchControls.Update();\n\t\tMInput.Update();\n\t\tIOSTouchControls.UpdateHardwareSources();\n\t\tIOSTouchControls.MarkGameUpdate();",
        "source arbitration frame boundary")

    input_path = root / "Celeste" / "Input.cs"
    replace_exact(input_path,
'''\tprivate static bool ControllerGrabCheck => Settings.Instance.GrabMode switch
\t{
\t\tGrabModes.Invert => !Grab.Check,\x20
\t\tGrabModes.Toggle => grabToggle,\x20
\t\t_ => Grab.Check,\x20
\t};

\tpublic static bool GrabCheck => CelesteIOSFoundation.TouchGrabPolicy.Resolve(
\t\tIOSTouchControls.Grab,
\t\tIOSTouchControls.LogicalVisible,
\t\tIOSTouchControls.PhysicalControllerConnected,
\t\tControllerGrabCheck);''',
'''\tpublic static bool GrabCheck => IOSTouchControls.Grab;''',
        "single active-source Grab final state")
    replace_exact(input_path,
'''\tpublic static void UpdateGrab()
\t{
\t\tif (Settings.Instance.GrabMode == GrabModes.Toggle && Grab.Pressed)
\t\t{
\t\t\tgrabToggle = !grabToggle;
\t\t}
\t}

\tpublic static void ResetGrab()
\t{
\t\tgrabToggle = false;
\t}''',
'''\tpublic static void UpdateGrab()
\t{
\t\t// iOS source-aware Grab is updated once after MInput.Update.
\t}

\tpublic static void ResetGrab()
\t{
\t\tgrabToggle = false;
\t\tIOSTouchControls.ResetGrabArbiter();
\t}''',
        "source-aware Grab lifecycle")

    replace_exact(root / "Celeste" / "GrabbyIcon.cs",
        "Settings.Instance.GrabMode == GrabModes.Toggle && Input.GrabCheck",
        "IOSTouchControls.ActiveGrabMode == CelesteIOSFoundation.AppleGrabMode.Toggle && Input.GrabCheck",
        "source-aware Grabby icon")

    menu = root / "Celeste" / "MenuOptions.cs"
    replace_exact(menu,
'''\tprivate static void CreateGrabMode(TextMenu menu)
\t{
\t\tmenu.Add(new TextMenu.Slider(Dialog.Clean("OPTIONS_GRAB_MODE"), (int i) => i switch
\t\t{
\t\t\t0 => Dialog.Clean("OPTIONS_BUTTON_HOLD"),\x20
\t\t\t1 => Dialog.Clean("OPTIONS_BUTTON_INVERT"),\x20
\t\t\t_ => Dialog.Clean("OPTIONS_BUTTON_TOGGLE"),\x20
\t\t}, 0, 2, (int)Settings.Instance.GrabMode).Change(delegate(int i)
\t\t{
\t\t\tSettings.Instance.GrabMode = (GrabModes)i;
\t\t\tInput.ResetGrab();
\t\t}));
\t}''',
'''\tprivate static void CreateGrabMode(TextMenu menu)
\t{
\t\tmenu.Add(new IOSSourceGrabModeOption(Dialog.Clean("OPTIONS_GRAB_MODE")));
\t}''',
        "source-aware vanilla Grab Mode row")

    after = manifest(root)
    report = {
        "schemaVersion": 2,
        "platformTransform": "modern-ios-stage24d3-v2",
        "input": before,
        "output": after,
        "layoutSchema": 2,
        "profiles": ["Phone", "Tablet"],
        "grabProfiles": ["Touch", "Controller", "Keyboard"],
        "directionalHapticsDefault": False,
        "generatedSourceTracked": False,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    print(f"PASS: Stage 24D3 iOS tree {after['fileCount']} files {after['logicalSha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
