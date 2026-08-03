#!/usr/bin/env python3
"""Deterministic Stage 3C transformations and logical manifests."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import shutil
from typing import Any


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def replace_once(path: pathlib.Path, old: str, new: str, label: str) -> None:
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"error: {label} expected one match in {path.name}, found {count}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


def replace_count(path: pathlib.Path, old: str, new: str, expected: int, label: str) -> None:
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != expected:
        raise SystemExit(f"error: {label} expected {expected} matches in {path.name}, found {count}")
    path.write_text(text.replace(old, new), encoding="utf-8")


def logical_manifest(root: pathlib.Path) -> dict[str, Any]:
    entries: list[dict[str, Any]] = []
    aggregate = hashlib.sha256()
    for path in sorted(root.rglob("*"), key=lambda item: item.relative_to(root).as_posix()):
        if not path.is_file() or any(part in {"bin", "obj"} for part in path.relative_to(root).parts):
            continue
        relative = path.relative_to(root).as_posix()
        digest = sha256(path)
        entries.append({"path": relative, "sha256": digest, "size": path.stat().st_size})
        aggregate.update(relative.encode() + b"\0" + digest.encode() + b"\n")
    return {"fileCount": len(entries), "logicalSha256": aggregate.hexdigest(), "files": entries}


def validate_input(root: pathlib.Path, policy: dict[str, Any]) -> None:
    actual = logical_manifest(root)
    expected = policy["stage3BInput"]
    if actual["fileCount"] != expected["fileCount"] or actual["logicalSha256"] != expected["logicalSha256"]:
        raise SystemExit("error: Stage 3C input is not the accepted Stage 3B generated source")
    for relative, expected_hash in policy["saveDataGraph"]["sourceFiles"].items():
        path = root / relative
        if not path.is_file() or sha256(path) != expected_hash:
            raise SystemExit(f"error: locked SaveData graph source drifted: {relative}")


def transform(root: pathlib.Path, templates: pathlib.Path, policy: dict[str, Any], fault_baseline: bool) -> dict[str, Any]:
    validate_input(root, policy)
    project = root / "Celeste.Modern.csproj"
    replace_once(
        project,
        "<DefineConstants>$(DefineConstants);TVOS;TVOS_AUDIO_DISABLED;TVOS_STAGE3B</DefineConstants>",
        "<DefineConstants>$(DefineConstants);TVOS;TVOS_AUDIO_DISABLED;TVOS_STAGE3B;TVOS_STAGE3C</DefineConstants>",
        "Stage 3C compile symbol",
    )
    for name in ("TvOSSaveDataSerializer.cs", "TvOSStage3CBridge.cs"):
        source = templates / name
        if not source.is_file():
            raise SystemExit(f"error: Stage 3C template is missing: {name}")
        shutil.copyfile(source, root / "Celeste" / name)

    session = root / "Celeste" / "Session.cs"
    replace_once(session, "\tprivate Session()", "\tinternal Session()", "AOT-safe Session construction")

    celeste = root / "Celeste" / "Celeste.cs"
    replace_once(
        celeste,
        "\t\tTvOSStage3Bridge.RecordUpdate(Engine.Scene);",
        "\t\tTvOSStage3Bridge.RecordUpdate(Engine.Scene);\n\t\tTvOSStage3CBridge.OnCelesteUpdate(Engine.Scene);",
        "Stage 3C update hook",
    )
    replace_once(
        celeste,
        "\t\tTvOSStage3Bridge.RecordDraw(Engine.Scene, base.GraphicsDevice);",
        "\t\tTvOSStage3Bridge.RecordDraw(Engine.Scene, base.GraphicsDevice);\n\t\tTvOSStage3CBridge.OnCelesteDraw(Engine.Scene);",
        "Stage 3C draw hook",
    )
    replace_once(
        celeste,
        "\tprotected override void OnSceneTransition(Scene last, Scene next)\n\t{",
        "\tprotected override void OnSceneTransition(Scene last, Scene next)\n\t{\n\t\tTvOSStage3CBridge.SceneTransition(last, next);",
        "scene transition haptic stop",
    )
    replace_once(
        celeste,
        "\t\tceleste.RunWithLogging();\n\t\tTvOSStage3Bridge.ThrowIfFatal();",
        "\t\tceleste.RunWithLogging();\n\t\tTvOSStage3Bridge.ThrowIfFatal();\n\t\tTvOSStage3CBridge.StopAllRumble(\"game-disposal\");",
        "normal disposal haptic stop",
    )
    replace_count(
        celeste,
        "\t\t\tTvOSStage3Bridge.RecordFatal(ex, \"Celeste.Run startup\");",
        "\t\t\tTvOSStage3CBridge.Fatal(ex, \"Celeste.Run startup\");\n\t\t\tTvOSStage3Bridge.RecordFatal(ex, \"Celeste.Run startup\");",
        1,
        "startup fatal haptic stop",
    )

    loader = root / "Celeste" / "GameLoader.cs"
    replace_once(
        loader,
        "\t\tEngine.Scene = new OverworldLoader(Overworld.StartMode.Titlescreen, Snow);",
        "#if TVOS_STAGE3C\n\t\tif (TvOSStage3CBridge.PrologueDiagnosticEnabled)\n\t\t{\n"
        "\t\t\tTvOSStage3CBridge.PreparePrologueDiagnostic(Snow);\n\t\t}\n\t\telse\n#endif\n\t\t{\n"
        "\t\t\tEngine.Scene = new OverworldLoader(Overworld.StartMode.Titlescreen, Snow);\n\t\t}",
        "Prologue diagnostic route",
    )

    ending = root / "Celeste" / "CS00_Ending.cs"
    replace_once(
        ending,
        "\tpublic override void OnBegin(Level level)\n\t{\n\t\tAdd(new Coroutine(Cutscene(level)));",
        "\tpublic override void OnBegin(Level level)\n\t{\n\t\tTvOSStage3CBridge.Checkpoint(\"bird-tutorial-entered-sequence\", \"phase=cutscene-begin\");\n\t\tAdd(new Coroutine(Cutscene(level)));",
        "Prologue entry checkpoint",
    )
    replace_once(
        ending,
        "\t\tyield return bird.ShowTutorial(new BirdTutorialGui(bird, new Vector2(0f, -16f), Dialog.Clean(\"tutorial_dash\"), new Vector2(1f, -1f), \"+\", BirdTutorialGui.ButtonPrompt.Dash), caw: true);",
        "\t\tTvOSStage3CBridge.Checkpoint(\"bird-tutorial-entered\");\n"
        "\t\tyield return bird.ShowTutorial(new BirdTutorialGui(bird, new Vector2(0f, -16f), Dialog.Clean(\"tutorial_dash\"), new Vector2(1f, -1f), \"+\", BirdTutorialGui.ButtonPrompt.Dash), caw: true);",
        "bird tutorial checkpoint",
    )
    replace_once(
        ending,
        "\t\t\tif (aimVector.X > 0f && aimVector.Y < 0f && Input.Dash.Pressed)",
        "\t\t\tif ((aimVector.X > 0f && aimVector.Y < 0f && Input.Dash.Pressed) || TvOSStage3CBridge.ConsumeDiagnosticDash())",
        "deterministic diagnostic dash edge",
    )
    replace_once(
        ending,
        "\t\tplayer.StateMachine.State = 16;",
        "\t\tTvOSStage3CBridge.Checkpoint(\"controller-input-received\", \"action=dash-up-right\");\n"
        "\t\tTvOSStage3CBridge.Checkpoint(\"dash-initiated\");\n\t\tplayer.StateMachine.State = 16;",
        "dash checkpoints",
    )
    replace_once(
        ending,
        "\t\tkeyOffed = true;\n\t\tAudio.CurrentMusicEventInstance.triggerCue();",
        "\t\tkeyOffed = true;\n\t\tTvOSStage3CBridge.Checkpoint(\"music-cue-requested\", \"source=post-dash\");\n"
        "\t\tAudio.CurrentMusicEventInstance.triggerCue();",
        "post-dash music cue checkpoint",
    )
    replace_once(
        ending,
        "\t\t\tif (!keyOffed)\n\t\t\t{\n\t\t\t\tAudio.CurrentMusicEventInstance.triggerCue();",
        "\t\t\tif (!keyOffed)\n\t\t\t{\n\t\t\t\tTvOSStage3CBridge.Checkpoint(\"music-cue-requested\", \"source=cutscene-skip\");\n"
        "\t\t\t\tAudio.CurrentMusicEventInstance.triggerCue();",
        "skip music cue checkpoint",
    )
    replace_once(
        ending,
        "\tpublic override void OnEnd(Level level)\n\t{",
        "\tpublic override void OnEnd(Level level)\n\t{\n"
        "\t\tTvOSStage3CBridge.Checkpoint(WasSkipped ? \"cutscene-skip-completing\" : \"cutscene-completion-requested\");\n"
        "\t\tTvOSStage3CBridge.StopAllRumble(WasSkipped ? \"cutscene-skip\" : \"cutscene-complete\");",
        "cutscene completion checkpoint and haptic stop",
    )

    cutscene = root / "Celeste" / "CutsceneEntity.cs"
    replace_once(
        cutscene,
        "\tprivate void SkipCutscene(Level level)\n\t{\n\t\tWasSkipped = true;",
        "\tprivate void SkipCutscene(Level level)\n\t{\n\t\tTvOSStage3CBridge.Checkpoint(\"cutscene-skip-requested\", \"source=game-edge\");\n\t\tWasSkipped = true;",
        "skip checkpoint",
    )

    level = root / "Celeste" / "Level.cs"
    replace_once(
        level,
        "\t\tCompleted = true;\n\t\tSaveData.Instance.RegisterCompletion(Session);",
        "\t\tCompleted = true;\n\t\tTvOSStage3CBridge.Checkpoint(\"progression-state-updating\");\n"
        "\t\tSaveData.Instance.RegisterCompletion(Session);\n\t\tTvOSStage3CBridge.Checkpoint(\"progression-state-updated\");",
        "progression checkpoint",
    )
    replace_once(
        level,
        "\tpublic ScreenWipe CompleteArea(bool spotlightWipe = true, bool skipScreenWipe = false, bool skipCompleteScreen = false)\n\t{\n\t\tRegisterAreaComplete();",
        "\tpublic ScreenWipe CompleteArea(bool spotlightWipe = true, bool skipScreenWipe = false, bool skipCompleteScreen = false)\n\t{\n"
        "\t\tTvOSStage3CBridge.Checkpoint(\"transition-fade-entered\", $\"skipping={SkippingCutscene}; spotlight={spotlightWipe}\");\n\t\tRegisterAreaComplete();",
        "transition checkpoint",
    )

    user_io = root / "Celeste" / "UserIO.cs"
    replace_once(
        user_io,
        "\t\t\t\tSaveData.Instance.BeforeSave();\n\t\t\t\tsavingFileData = Serialize(SaveData.Instance);",
        "\t\t\t\tTvOSStage3CBridge.Checkpoint(\"save-data-serialization-entered\");\n\t\t\t\tSaveData.Instance.BeforeSave();\n"
        "\t\t\t\tsavingFileData = Serialize(SaveData.Instance);\n\t\t\t\tTvOSStage3CBridge.Checkpoint(\"save-data-serialization-completed\", $\"bytes={savingFileData.Length}\");",
        "SaveData serialization checkpoints",
    )

    engine = root / "Monocle" / "Engine.cs"
    replace_once(
        engine,
        "\tprotected override void OnDeactivated(object sender, EventArgs args)\n\t{\n\t\tbase.OnDeactivated(sender, args);",
        "\tprotected override void OnDeactivated(object sender, EventArgs args)\n\t{\n\t\tTvOSStage3CBridge.StopAllRumble(\"resign-active\");\n\t\tbase.OnDeactivated(sender, args);",
        "deactivation haptic stop",
    )
    replace_once(
        engine,
        "\t\t\tglobal::Celeste.TvOSStage3Bridge.RecordFatal(ex, \"Engine.RunWithLogging\");",
        "\t\t\tglobal::Celeste.TvOSStage3CBridge.Fatal(ex, \"Engine.RunWithLogging\");\n"
        "\t\t\tglobal::Celeste.TvOSStage3Bridge.RecordFatal(ex, \"Engine.RunWithLogging\");",
        "engine fatal haptic stop",
    )

    run_thread = root / "Celeste" / "RunThread.cs"
    replace_once(
        run_thread,
        "\t\t\tTvOSStage3Bridge.RecordFatal(ex, $\"RunThread:{Thread.CurrentThread.Name}\");",
        "\t\t\tTvOSStage3CBridge.Fatal(ex, $\"RunThread:{Thread.CurrentThread.Name}\");\n"
        "\t\t\tTvOSStage3Bridge.RecordFatal(ex, $\"RunThread:{Thread.CurrentThread.Name}\");",
        "worker fatal haptic stop",
    )

    if not fault_baseline:
        replace_once(
            user_io,
            "\t\tif (typeof(T) == typeof(Settings))\n\t\t{\n\t\t\treturn (T)(object)TvOSSettingsSerializer.Deserialize(stream);\n\t\t}\n"
            "\t\tthrow new PlatformNotSupportedException(\"Stage 3B does not deserialize save data; durable saves are Stage 6.\");",
            "\t\tif (typeof(T) == typeof(Settings))\n\t\t{\n\t\t\treturn (T)(object)TvOSSettingsSerializer.Deserialize(stream);\n\t\t}\n"
            "\t\tif (typeof(T) == typeof(SaveData))\n\t\t{\n\t\t\treturn (T)(object)TvOSSaveDataSerializer.Deserialize(stream);\n\t\t}\n"
            "\t\tthrow new PlatformNotSupportedException($\"Stage 3C has no AOT-safe XML serializer for {typeof(T).FullName}.\");",
            "AOT-safe SaveData deserialize route",
        )
        replace_once(
            user_io,
            "\t\tif (typeof(T) == typeof(Settings))\n\t\t{\n\t\t\treturn TvOSSettingsSerializer.SerializeToBytes((Settings)(object)instance);\n\t\t}\n"
            "\t\tthrow new PlatformNotSupportedException(\"Stage 3B does not serialize save data; durable saves are Stage 6.\");",
            "\t\tif (typeof(T) == typeof(Settings))\n\t\t{\n\t\t\treturn TvOSSettingsSerializer.SerializeToBytes((Settings)(object)instance);\n\t\t}\n"
            "\t\tif (typeof(T) == typeof(SaveData))\n\t\t{\n\t\t\treturn TvOSSaveDataSerializer.SerializeToBytes((SaveData)(object)instance);\n\t\t}\n"
            "\t\tthrow new PlatformNotSupportedException($\"Stage 3C has no AOT-safe XML serializer for {typeof(T).FullName}.\");",
            "AOT-safe SaveData serialize route",
        )

        audio = root / "Celeste" / "Audio.TvOSDisabled.cs"
        replace_once(
            audio,
            "    private static EventInstance InertEvent(string category)",
            "    public static void TriggerCueNoAudio(EventInstance instance) => TvOSStage3Bridge.RecordNoAudioCall(\"music-cue\");\n\n"
            "    private static EventInstance InertEvent(string category)",
            "high-level inert music cue",
        )
        replace_count(
            ending,
            "Audio.CurrentMusicEventInstance.triggerCue();",
            "Audio.TriggerCueNoAudio(Audio.CurrentMusicEventInstance);",
            2,
            "Prologue no-audio cue calls",
        )

        input_path = root / "Celeste" / "Input.cs"
        replace_once(input_path, "using System.Collections.Generic;", "using System.Collections.Generic;\nusing System.Runtime.CompilerServices;", "CallerMemberName import")
        replace_once(
            input_path,
            "public static void Rumble(RumbleStrength strength, RumbleLength length)",
            "public static void Rumble(RumbleStrength strength, RumbleLength length, [CallerMemberName] string source = null)",
            "rumble source annotation",
        )
        replace_once(
            input_path,
            "MInput.GamePads[Gamepad].Rumble(rumbleStrengths[(int)strength] * num, rumbleLengths[(int)length]);",
            "MInput.GamePads[Gamepad].Rumble(rumbleStrengths[(int)strength] * num, rumbleLengths[(int)length], source);",
            "rumble source forwarding",
        )
        replace_once(
            input_path,
            "public static void RumbleSpecific(float strength, float time)",
            "public static void RumbleSpecific(float strength, float time, [CallerMemberName] string source = null)",
            "specific rumble source annotation",
        )
        replace_once(
            input_path,
            "MInput.GamePads[Gamepad].Rumble(strength * num, time);",
            "MInput.GamePads[Gamepad].Rumble(strength * num, time, source);",
            "specific rumble source forwarding",
        )

        minput = root / "Monocle" / "MInput.cs"
        replace_once(
            minput,
            "\t\t\tPreviousState = CurrentState;\n\t\t\tCurrentState = GamePad.GetState(PlayerIndex);\n"
            "\t\t\tif (!Attached && CurrentState.IsConnected)\n\t\t\t{\n"
            "\t\t\t\tIsControllerFocused = true;\n\t\t\t}\n\t\t\tAttached = CurrentState.IsConnected;",
            "\t\t\tPreviousState = CurrentState;\n\t\t\tbool wasAttached = Attached;\n\t\t\tCurrentState = GamePad.GetState(PlayerIndex);\n"
            "\t\t\tif (wasAttached && !CurrentState.IsConnected)\n\t\t\t{\n"
            "\t\t\t\tglobal::Celeste.TvOSStage3CBridge.ControllerConnectionChanged((int)PlayerIndex, connected: false);\n\t\t\t}\n"
            "\t\t\tif (!Attached && CurrentState.IsConnected)\n\t\t\t{\n"
            "\t\t\t\tglobal::Celeste.TvOSStage3CBridge.ControllerConnectionChanged((int)PlayerIndex, connected: true);\n"
            "\t\t\t\tIsControllerFocused = true;\n\t\t\t}\n"
            "\t\t\tAttached = CurrentState.IsConnected;",
            "controller disconnect haptic stop",
        )
        replace_count(minput, "rumbleTime -= Engine.DeltaTime;", "rumbleTime -= Engine.RawDeltaTime;", 2, "raw-clock rumble expiry")
        replace_once(
            minput,
            "\t\t\t\t\tGamePad.SetVibration(PlayerIndex, 0f, 0f);",
            "\t\t\t\t\tStopRumble(\"duration-expired\");",
            "duration expiry stop",
        )
        replace_once(
            minput,
            "\t\t\tGamePad.SetVibration(PlayerIndex, 0f, 0f);\n\t\t}\n\n\t\tpublic void Rumble(float strength, float time)",
            "\t\t\tStopRumble(\"explicit-zero\");\n\t\t}\n\n\t\tpublic void Rumble(float strength, float time, string source = \"MInput\")",
            "inactive update stop and source signature",
        )
        replace_once(
            minput,
            "\t\t\t\tGamePad.SetVibration(PlayerIndex, strength, strength);\n\t\t\t\trumbleStrength = strength;",
            "\t\t\t\tglobal::Celeste.TvOSStage3CBridge.Rumble((int)PlayerIndex, strength, strength, time, source);\n\t\t\t\trumbleStrength = strength;",
            "instrumented rumble start",
        )
        replace_once(
            minput,
            "\t\tpublic void StopRumble()\n\t\t{\n\t\t\tGamePad.SetVibration(PlayerIndex, 0f, 0f);\n\t\t\trumbleTime = 0f;\n\t\t}",
            "\t\tpublic void StopRumble(string reason = \"explicit-zero\")\n\t\t{\n"
            "\t\t\tglobal::Celeste.TvOSStage3CBridge.StopRumble((int)PlayerIndex, reason);\n\t\t\trumbleTime = 0f;\n\t\t\trumbleStrength = 0f;\n\t\t}",
            "instrumented rumble stop",
        )
        replace_once(minput, "GamePads[0].Rumble(strength, time);", "GamePads[0].Rumble(strength, time, \"MInput.RumbleFirst\");", "RumbleFirst source")
        replace_once(
            minput,
            "\t\t\tgamePads[i].StopRumble();",
            "\t\t\tgamePads[i].StopRumble(\"shutdown\");",
            "shutdown stop reason",
        )

    manifest = logical_manifest(root)
    manifest.update({
        "schemaVersion": 1,
        "root": "$GENERATED_STAGE3C_ROOT",
        "gameVersion": policy["gameVersion"],
        "stage3BInputLogicalSha256": policy["stage3BInput"]["logicalSha256"],
        "faultBaseline": fault_baseline,
        "saveDataGraphSourceFiles": policy["saveDataGraph"]["sourceFiles"],
        "saveDataSerializer": "guard-retained" if fault_baseline else "explicit reflection-free Celeste 1.4.0.0 XML",
        "audioCueFix": not fault_baseline,
        "rumbleLifecycleFix": not fault_baseline,
    })
    return manifest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True)
    parser.add_argument("--templates", required=True)
    parser.add_argument("--policy", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--fault-baseline", action="store_true", help="retain the Stage 3B audio/save failure while adding bounded diagnostics")
    args = parser.parse_args()
    root = pathlib.Path(args.root).resolve()
    templates = pathlib.Path(args.templates).resolve()
    policy = json.loads(pathlib.Path(args.policy).read_text(encoding="utf-8"))
    manifest = transform(root, templates, policy, args.fault_baseline)
    output = pathlib.Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"Stage 3C logical SHA-256: {manifest['logicalSha256']}")
    print("mode: fault-baseline" if args.fault_baseline else "mode: corrected")


if __name__ == "__main__":
    main()
