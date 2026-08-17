#!/usr/bin/env python3
"""Apply the locked modern-iOS touch/input transform after Stage 24C2."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import shutil


EXPECTED_INPUT_FILES = 928
EXPECTED_INPUT_HASH = "f51187556979e6970b00ac99c21e3a09c7e33d674138bd534c46a308406f1d03"


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
        raise SystemExit(f"error: Stage 24D2 input tree changed: {before['fileCount']} {before['logicalSha256']}")

    shutil.copyfile(args.templates / "IOSTouchControls.cs", root / "Celeste" / "IOSTouchControls.cs")
    shutil.copyfile(args.templates / "IOSTouchControlsUI.cs", root / "Celeste" / "IOSTouchControlsUI.cs")
    shutil.copyfile(args.templates / "IOSPresentationCoordinator.cs", root / "Celeste" / "IOSPresentationCoordinator.cs")
    touch_assets = root / "Celeste" / "TouchAssets"
    touch_assets.mkdir()
    for name in ("jump.a8", "dash.a8", "grab-ungrabbed.a8", "grab-grabbed.a8", "touch.a8", "pause.a8", "journal.a8"):
        source = args.asset_dir / name
        if source.stat().st_size != 128 * 128:
            raise SystemExit(f"error: generated touch asset has wrong size: {name}")
        shutil.copyfile(source, touch_assets / name)

    replace_exact(
        root / "Celeste" / "Celeste.cs",
        "\t\tbase.RenderCore();\n\t\tAppleRuntimeBridge.RecordDraw(Engine.Scene, base.GraphicsDevice);",
        "\t\tbase.RenderCore();\n\t\tIOSTouchControls.Render();\n\t\tAppleRuntimeBridge.RecordDraw(Engine.Scene, base.GraphicsDevice);",
        "Celeste touch render boundary")
    replace_exact(
        root / "Monocle" / "Engine.cs",
        "\t\tFrameCounter++;\n\t\tMInput.Update();",
        "\t\tFrameCounter++;\n\t\tIOSTouchControls.Update();\n\t\tMInput.Update();\n\t\tIOSTouchControls.MarkGameUpdate();",
        "touch input update boundary")

    input_path = root / "Celeste" / "Input.cs"
    replace_exact(
        input_path,
        "\tpublic static bool GrabCheck => Settings.Instance.GrabMode switch\n\t{\n\t\tGrabModes.Invert => !Grab.Check, \n\t\tGrabModes.Toggle => grabToggle, \n\t\t_ => Grab.Check, \n\t};",
        "\tprivate static bool ControllerGrabCheck => Settings.Instance.GrabMode switch\n\t{\n\t\tGrabModes.Invert => !Grab.Check, \n\t\tGrabModes.Toggle => grabToggle, \n\t\t_ => Grab.Check, \n\t};\n\n\tpublic static bool GrabCheck => CelesteIOSFoundation.TouchGrabPolicy.Resolve(\n\t\tIOSTouchControls.Grab,\n\t\tIOSTouchControls.LogicalVisible,\n\t\tIOSTouchControls.PhysicalControllerConnected,\n\t\tControllerGrabCheck);",
        "touch Grab final-state bridge")
    replace_exact(
        input_path,
        "\t\tMenuConfirm = new VirtualButton(Settings.Instance.Confirm, Gamepad, 0f, 0.2f);\n\t\tMenuCancel = new VirtualButton(Settings.Instance.Cancel, Gamepad, 0f, 0.2f);",
        "\t\tMenuConfirm = new VirtualButton(Settings.Instance.Confirm, Gamepad, 0f, 0.2f);\n\t\tMenuCancel = new VirtualButton(Settings.Instance.Cancel, Gamepad, 0f, 0.2f);\n\t\tIOSTouchControls.Bind();",
        "touch logical binding")
    replace_exact(
        input_path,
        "\t\treturn automaticPrefix;\n\t\t#endif\n\t}\n\n\tprivate static string GuiInputPrefixAutomatic",
        "\t\treturn IOSTouchControls.ResolvePromptPrefix(automaticPrefix);\n\t\t#endif\n\t}\n\n\tprivate static string GuiInputPrefixAutomatic",
        "iOS controller prompt-family bridge")
    replace_exact(
        input_path,
        "\tpublic static bool GuiInputController(PrefixMode mode = PrefixMode.Latest)\n\t{\n\t\treturn !GuiInputPrefix(mode).Equals(\"keyboard\");\n\t}",
        "\tpublic static bool GuiInputController(PrefixMode mode = PrefixMode.Latest)\n\t{\n\t\treturn !IOSTouchControls.TouchPromptActive && !GuiInputPrefix(mode).Equals(\"keyboard\");\n\t}",
        "touch prompt controller identity")
    replace_exact(
        input_path,
        "\tpublic static MTexture GuiButton(VirtualButton button, PrefixMode mode = PrefixMode.Latest, string fallback = \"controls/keyboard/oemquestion\")\n\t{\n\t\tstring prefix = GuiInputPrefix(mode);",
        "\tpublic static MTexture GuiButton(VirtualButton button, PrefixMode mode = PrefixMode.Latest, string fallback = \"controls/keyboard/oemquestion\")\n\t{\n\t\tMTexture touchPrompt = IOSTouchControls.TouchPrompt(button);\n\t\tif (touchPrompt != null) return touchPrompt;\n\t\tstring prefix = GuiInputPrefix(mode);",
        "touch Confirm/Cancel prompts")
    replace_exact(
        input_path,
        "\tpublic static MTexture GuiSingleButton(Buttons button, PrefixMode mode = PrefixMode.Latest, string fallback = \"controls/keyboard/oemquestion\")\n\t{\n\t\tstring prefix = ((!GuiInputController(mode)) ? \"xb1\" : GuiInputPrefix(mode));",
        "\tpublic static MTexture GuiSingleButton(Buttons button, PrefixMode mode = PrefixMode.Latest, string fallback = \"controls/keyboard/oemquestion\")\n\t{\n\t\tMTexture touchPrompt = IOSTouchControls.TouchPrompt(button);\n\t\tif (touchPrompt != null) return touchPrompt;\n\t\tstring prefix = ((!GuiInputController(mode)) ? \"xb1\" : GuiInputPrefix(mode));",
        "touch-neutral fixed-button prompts")

    replace_exact(
        root / "Celeste" / "TalkComponent.cs",
        "\t\t\t\tif (Input.GuiInputController())\n\t\t\t\t{\n\t\t\t\t\tInput.GuiButton(Input.Talk).DrawJustified(position, new Vector2(0.5f), Color.White * num2, num);",
        "\t\t\t\tif (Input.GuiInputController() || IOSTouchControls.TouchPromptActive)\n\t\t\t\t{\n\t\t\t\t\tInput.GuiButton(Input.Talk).DrawJustified(position, new Vector2(0.5f), Color.White * num2, num);",
        "touch Talk interaction prompt")

    replace_exact(
        root / "Celeste" / "UnlockEverythingThingy.cs",
        "\t\tAddInput('R', () => Input.Grab.Pressed && !Input.MenuJournal.Pressed);",
        "\t\tAddInput('R', () => (Input.Grab.Pressed || IOSTouchControls.GrabActionPressed) && !Input.MenuJournal.Pressed);",
        "touch Grab edge for vanilla unlock cheat")

    virtual_button = root / "Monocle" / "VirtualButton.cs"
    replace_exact(virtual_button, "using Microsoft.Xna.Framework.Input;", "using System;\nusing Microsoft.Xna.Framework.Input;", "VirtualButton Func import")
    replace_exact(virtual_button, "public class VirtualButton : VirtualInput\n{\n\tpublic Binding Binding;",
                  "public class VirtualButton : VirtualInput\n{\n\tpublic Func<bool> AdditionalCheck;\n\n\tpublic Func<bool> AdditionalPressed;\n\n\tpublic Func<bool> AdditionalReleased;\n\n\tpublic Binding Binding;", "VirtualButton logical delegates")
    replace_exact(virtual_button, "return Binding.Check(GamepadIndex, Threshold);", "return Binding.Check(GamepadIndex, Threshold) || (AdditionalCheck?.Invoke() ?? false);", "VirtualButton Check")
    replace_exact(virtual_button, "return Binding.Pressed(GamepadIndex, Threshold);", "return Binding.Pressed(GamepadIndex, Threshold) || (AdditionalPressed?.Invoke() ?? false);", "VirtualButton Pressed")
    replace_exact(virtual_button, "return Binding.Released(GamepadIndex, Threshold);", "return Binding.Released(GamepadIndex, Threshold) || (AdditionalReleased?.Invoke() ?? false);", "VirtualButton Released")
    replace_exact(virtual_button, "if (Binding.Pressed(GamepadIndex, Threshold))", "if (Binding.Pressed(GamepadIndex, Threshold) || (AdditionalPressed?.Invoke() ?? false))", "VirtualButton update Pressed")
    replace_exact(virtual_button, "else if (Binding.Check(GamepadIndex, Threshold))", "else if (Binding.Check(GamepadIndex, Threshold) || (AdditionalCheck?.Invoke() ?? false))", "VirtualButton update Check")

    integer_axis = root / "Monocle" / "VirtualIntegerAxis.cs"
    replace_exact(integer_axis, "namespace Monocle;", "using System;\n\nnamespace Monocle;", "VirtualIntegerAxis Func import")
    replace_exact(integer_axis, "public class VirtualIntegerAxis : VirtualInput\n{\n\tpublic Binding Positive;",
                  "public class VirtualIntegerAxis : VirtualInput\n{\n\tpublic Func<int> AdditionalValue;\n\n\tpublic Binding Positive;", "VirtualIntegerAxis logical delegate")
    replace_exact(integer_axis, "\t\tif (Inverted)\n\t\t{",
                  "\t\tint additional = AdditionalValue?.Invoke() ?? 0;\n\t\tif (additional != 0)\n\t\t{\n\t\t\tValue = Math.Sign(additional);\n\t\t}\n\t\tif (Inverted)\n\t\t{", "VirtualIntegerAxis touch value")

    joystick = root / "Monocle" / "VirtualJoystick.cs"
    replace_exact(joystick, "using Microsoft.Xna.Framework;", "using System;\nusing Microsoft.Xna.Framework;", "VirtualJoystick Func import")
    replace_exact(joystick, "public class VirtualJoystick : VirtualInput\n{\n\tpublic Binding Up;",
                  "public class VirtualJoystick : VirtualInput\n{\n\tpublic Func<Vector2> AdditionalValue;\n\n\tpublic Binding Up;", "VirtualJoystick logical delegate")
    replace_exact(joystick, "\t\tValue = new Vector2(InvertedX ? (value.X * -1f) : value.X, InvertedY ? (value.Y * -1f) : value.Y);",
                  "\t\tVector2 additional = AdditionalValue?.Invoke() ?? Vector2.Zero;\n\t\tif (additional != Vector2.Zero)\n\t\t{\n\t\t\tvalue = additional;\n\t\t}\n\t\tValue = new Vector2(InvertedX ? (value.X * -1f) : value.X, InvertedY ? (value.Y * -1f) : value.Y);", "VirtualJoystick touch value")

    menu = root / "Celeste" / "MenuOptions.cs"
    replace_exact(menu, "\t\tmenu = new TextMenu();\n\t\tmenu.Add(new TextMenu.Header(Dialog.Clean(\"options_title\")));",
                  "\t\tmenu = new TextMenu();\n\t\tmenu.Add(new TextMenu.Header(Dialog.Clean(\"options_title\")));", "touch options root remains vanilla")
    replace_exact(
        menu,
        '\t\tmenu.Add(new TextMenu.Button(Dialog.Clean("options_keyconfig")).Pressed(OpenKeyboardConfig));\n\t\tmenu.Add(new TextMenu.Button(Dialog.Clean("options_btnconfig")).Pressed(OpenButtonConfig));',
        '\t\tmenu.Add(new TextMenu.Button(Dialog.Clean("options_keyconfig")).Pressed(OpenKeyboardConfig));\n\t\tmenu.Add(new TextMenu.Button(Dialog.Clean("options_btnconfig")).Pressed(OpenButtonConfig));\n\t\tmenu.Add(new TextMenu.Button("Touch Controls").Pressed(OpenTouchControls));',
        "touch config entry beside keyboard/controller config")
    options = '''\t\tmenu.Add(new TextMenu.SubHeader("APPLE INPUT"));
\t\tTextMenu.Option<int> iosPromptMode = new TextMenu.Slider("Controller Prompts", IOSTouchControls.PromptModeName, 0, 4, IOSTouchControls.PromptModeIndex).Change(IOSTouchControls.SetPromptMode);
\t\tmenu.Add(iosPromptMode);
'''
    replace_exact(menu, "\t\tviewport.Visible = Settings.Instance.Fullscreen;", options + "\t\tviewport.Visible = Settings.Instance.Fullscreen;", "iOS touch options section")
    replace_exact(
        menu,
        "\tprivate static void OpenButtonConfig()\n\t{",
        '''\tprivate static void OpenTouchControls()
\t{
\t\tmenu.Focused = false;
\t\tIOSTouchControlsUI touchControlsUI = new IOSTouchControlsUI();
\t\ttouchControlsUI.OnClose = delegate
\t\t{
\t\t\tIOSTouchControls.SetOptionsEditing(false);
\t\t\tmenu.Focused = true;
\t\t};
\t\tEngine.Scene.Add(touchControlsUI);
\t\tEngine.Scene.OnEndOfFrame += delegate
\t\t{
\t\t\tEngine.Scene.Entities.UpdateLists();
\t\t};
\t}

\tprivate static void OpenButtonConfig()
\t{''',
        "standalone touch config TextMenu")

    project = root / "Celeste.Modern.csproj"
    project_anchor = '    <ProjectReference Include="$(CelesteAppleRepoRoot)/modern-ios/CelesteIOSFoundation/CelesteIOSFoundation.csproj" />'
    project_additions = project_anchor + '''
    <ProjectReference Include="$(CelesteAppleRepoRoot)/shared/CelesteAppleInput/CelesteAppleInput.csproj" />
    <EmbeddedResource Include="Celeste/TouchAssets/jump.a8" LogicalName="Celeste.IOSTouchControls.jump.a8" />
    <EmbeddedResource Include="Celeste/TouchAssets/dash.a8" LogicalName="Celeste.IOSTouchControls.dash.a8" />
    <EmbeddedResource Include="Celeste/TouchAssets/grab-ungrabbed.a8" LogicalName="Celeste.IOSTouchControls.grab-ungrabbed.a8" />
    <EmbeddedResource Include="Celeste/TouchAssets/grab-grabbed.a8" LogicalName="Celeste.IOSTouchControls.grab-grabbed.a8" />
    <EmbeddedResource Include="Celeste/TouchAssets/touch.a8" LogicalName="Celeste.IOSTouchControls.touch.a8" />
    <EmbeddedResource Include="Celeste/TouchAssets/pause.a8" LogicalName="Celeste.IOSTouchControls.pause.a8" />
    <EmbeddedResource Include="Celeste/TouchAssets/journal.a8" LogicalName="Celeste.IOSTouchControls.journal.a8" />'''
    replace_exact(project, project_anchor, project_additions, "iOS touch project resources")

    after = manifest(root)
    report = {
        "schemaVersion": 1,
        "platformTransform": "modern-ios-stage24d2-v1",
        "input": before,
        "output": after,
        "touchDefaults": {
            "visibility": "Automatic", "movement": "Fixed", "grab": "Toggle",
            "sliding": "Off", "opacityPercent": 70, "sizePercent": 100,
            "haptics": True, "deadzone": 0.18, "hysteresisDegrees": 8,
        },
        "stableFingerIdentity": "tracked iOS-only FNA source patch",
        "generatedSourceTracked": False,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    print(f"PASS: Stage 24D2 iOS tree {after['fileCount']} files {after['logicalSha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
