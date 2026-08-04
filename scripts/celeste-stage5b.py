#!/usr/bin/env python3
"""Deterministically restore Celeste's generated FMOD API for Stage 5B."""

from __future__ import annotations

import argparse
import collections
import hashlib
import json
import pathlib
import re
import shutil
from typing import Any


FMOD_IMPORT = re.compile(
    r'(?m)^(?P<indent>[ \t]*)\[DllImport\("(?P<library>fmod(?:studio|_SDL)?)"'
    r'(?P<attribute_tail>[^\n]*)\)\]\n'
    r'(?P=indent)(?P<prefix>(?:private|internal|public)\s+static\s+)extern\s+'
    r'(?P<signature>[^;\n]+);$'
)


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


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


def collect_original_imports(root: pathlib.Path) -> list[dict[str, str]]:
    imports: list[dict[str, str]] = []
    for path in sorted(root.rglob("*.cs"), key=lambda item: item.relative_to(root).as_posix()):
        text = path.read_text(encoding="utf-8")
        for match in FMOD_IMPORT.finditer(text):
            signature = match.group("signature")
            symbol_match = re.search(r"([A-Za-z_][A-Za-z0-9_]*)\s*\(", signature)
            if not symbol_match:
                raise SystemExit(f"error: cannot identify original FMOD import in {path.relative_to(root)}")
            imports.append({
                "file": path.relative_to(root).as_posix(),
                "library": match.group("library"),
                "attributeTail": match.group("attribute_tail"),
                "indent": match.group("indent"),
                "prefix": match.group("prefix"),
                "signature": signature,
                "symbol": symbol_match.group(1),
            })
    return imports


def restore_imports(root: pathlib.Path, original_root: pathlib.Path, policy: dict[str, Any]) -> list[dict[str, str]]:
    imports = collect_original_imports(original_root)
    expected = policy["generatedFmodApi"]
    if len(imports) != expected["restoredImportCount"]:
        raise SystemExit(f"error: original decompilation contains {len(imports)} FMOD imports, expected 490")
    counts = collections.Counter(item["library"] for item in imports)
    if dict(sorted(counts.items())) != expected["importsByOriginalLibrary"]:
        raise SystemExit(f"error: original FMOD import library counts drifted: {dict(counts)}")

    restored: list[dict[str, str]] = []
    for item in imports:
        path = root / item["file"]
        if not path.is_file():
            raise SystemExit(f"error: transformed source is missing FMOD import owner: {item['file']}")
        indent = item["indent"]
        signature = item["signature"]
        symbol = item["symbol"]
        guard = (
            f'{indent}// Stage 3A compile-only FMOD boundary; native FMOD is intentionally absent.\n'
            f'{indent}{item["prefix"]}{signature} => throw global::Celeste.TvOSStage3Bridge.'
            f'FmodLowLevelReached("{symbol}");'
        )
        tail = item["attributeTail"]
        declaration = (
            f'{indent}[DllImport("__Internal"{tail}, ExactSpelling = true)]\n'
            f'{indent}{item["prefix"]}extern {signature};'
        )
        text = path.read_text(encoding="utf-8")
        count = text.count(guard)
        if count != 1:
            raise SystemExit(f"error: expected one Stage 3 FMOD guard for {item['file']}::{symbol}, found {count}")
        path.write_text(text.replace(guard, declaration, 1), encoding="utf-8")
        restored.append({"file": item["file"], "originalLibrary": item["library"], "symbol": symbol})

    generated_text = "\n".join(path.read_text(encoding="utf-8") for path in root.rglob("*.cs"))
    if ".FmodLowLevelReached(" in generated_text or "Stage 3A compile-only FMOD boundary" in generated_text:
        raise SystemExit("error: a Stage 3 FMOD guard remains in the real-audio output")
    remaining_names = sorted(set(re.findall(r'\[DllImport\("([^"\n]+)"', generated_text)))
    if remaining_names != ["__Internal"]:
        raise SystemExit(f"error: real-audio imports do not map exclusively to __Internal: {remaining_names}")
    return restored


def transform(root: pathlib.Path, original_root: pathlib.Path, templates: pathlib.Path, policy: dict[str, Any]) -> dict[str, Any]:
    actual = logical_manifest(root)
    expected_input = policy["stage3CInput"]
    if actual["fileCount"] != expected_input["fileCount"] or actual["logicalSha256"] != expected_input["logicalSha256"]:
        raise SystemExit("error: Stage 5B input is not the accepted Stage 3C generated source")

    project = root / "Celeste.Modern.csproj"
    replace_once(
        project,
        "<DefineConstants>$(DefineConstants);TVOS;TVOS_AUDIO_DISABLED;TVOS_STAGE3B;TVOS_STAGE3C</DefineConstants>",
        "<DefineConstants>$(DefineConstants);TVOS;TVOS_STAGE3B;TVOS_STAGE3C;TVOS_STAGE5B;TVOS_REAL_AUDIO</DefineConstants>",
        "exclusive real-audio compile symbols",
    )
    bridge = templates / "TvOSStage5BAudioBridge.cs"
    if not bridge.is_file():
        raise SystemExit("error: Stage 5B audio bridge template is missing")
    shutil.copyfile(bridge, root / "Celeste" / bridge.name)

    restored = restore_imports(root, original_root, policy)

    audio = root / "Celeste" / "Audio.cs"
    replace_once(
        audio,
        "\t\tCheckFmod(FMOD.Studio.System.create(out Audio.system));\n"
        "\t\tAudio.system.getLowLevelSystem(out var system);\n"
        "\t\tif (SDL.SDL_GetPlatform().Equals(\"Linux\"))\n"
        "\t\t{\n\t\t\tFMOD_SDL_Register(system.getRaw());\n\t\t}\n"
        "\t\tCheckFmod(Audio.system.initialize(1024, studioFlags, FMOD.INITFLAGS.NORMAL, IntPtr.Zero));",
        "\t\tTvOSStage5BAudioBridge.Checkpoint(\"audio-initialization-entered\", \"managed=0x00011014\");\n"
        "\t\tCheckFmod(FMOD.Studio.System.create(out Audio.system), \"FMOD_Studio_System_Create\");\n"
        "\t\tCheckFmod(Audio.system.getLowLevelSystem(out var system), \"FMOD_Studio_System_GetLowLevelSystem\");\n"
        "\t\tCheckFmod(system.getVersion(out uint nativeVersion), \"FMOD_System_GetVersion\");\n"
        "\t\tTvOSStage5BAudioBridge.NativeRuntimeVersion(nativeVersion);\n"
        "\t\tFMOD_SDL_Register(system.getRaw());\n"
        "\t\tTvOSStage5BAudioBridge.Checkpoint(\"fmod-sdl-registered\", \"target=studio-low-level\");\n"
        "\t\tCheckFmod(Audio.system.initialize(1024, studioFlags, FMOD.INITFLAGS.NORMAL, IntPtr.Zero), \"FMOD_Studio_System_Initialize\");\n"
        "\t\tTvOSStage5BAudioBridge.InitializedSystem();",
        "real FMOD Studio and FMOD-SDL initialization",
    )
    replace_once(
        audio,
        "\t\t\tCheckFmod(system.loadBankFile(text + \".bank\", LOAD_BANK_FLAGS.NORMAL, out var bank));\n"
        "\t\t\tbank.loadSampleData();\n"
        "\t\t\tif (loadStrings)\n\t\t\t{\n"
        "\t\t\t\tCheckFmod(system.loadBankFile(text + \".strings.bank\", LOAD_BANK_FLAGS.NORMAL, out var _));\n"
        "\t\t\t}\n\t\t\treturn bank;",
        "\t\t\tstring relativeName = name + \".bank\";\n"
        "\t\t\tCheckFmod(system.loadBankFile(text + \".bank\", LOAD_BANK_FLAGS.NORMAL, out var bank), $\"loadBankFile:{relativeName}\");\n"
        "\t\t\tCheckFmod(bank.loadSampleData(), $\"loadSampleData:{relativeName}\");\n"
        "\t\t\tTvOSStage5BAudioBridge.BankLoaded(relativeName, bank, stringsBank: false);\n"
        "\t\t\tif (loadStrings)\n\t\t\t{\n"
        "\t\t\t\tstring stringsName = name + \".strings.bank\";\n"
        "\t\t\t\tCheckFmod(system.loadBankFile(text + \".strings.bank\", LOAD_BANK_FLAGS.NORMAL, out var stringsBank), $\"loadBankFile:{stringsName}\");\n"
        "\t\t\t\tTvOSStage5BAudioBridge.BankLoaded(stringsName, stringsBank, stringsBank: true);\n"
        "\t\t\t}\n\t\t\treturn bank;",
        "checked and instrumented bank loading",
    )
    replace_once(
        audio,
        "\tinternal static void CheckFmod(RESULT result)\n\t{\n\t\tif (result != 0)\n\t\t{\n\t\t\tthrow new Exception(\"FMOD Failed: \" + result);\n\t\t}\n\t}",
        "\tinternal static void CheckFmod(RESULT result, string operation = \"unspecified\")\n\t{\n"
        "\t\tif (result != RESULT.OK)\n\t\t{\n"
        "\t\t\tTvOSStage5BAudioBridge.Checkpoint(\"fmod-error\", $\"managed={operation}; result={result}\");\n"
        "\t\t\tthrow new Exception($\"FMOD {operation} failed: {result}\");\n\t\t}\n\t}",
        "bounded FMOD error context",
    )
    replace_once(
        audio,
        "\t\t\tCheckFmod(system.update());",
        "\t\t\tCheckFmod(system.update(), \"FMOD_Studio_System_Update\");\n\t\t\tTvOSStage5BAudioBridge.AudioUpdated();",
        "audio update evidence",
    )
    replace_once(
        audio,
        "\t\tif (system != null)\n\t\t{\n"
        "\t\t\tCheckFmod(system.unloadAll());\n\t\t\tCheckFmod(system.release());\n\t\t\tsystem = null;\n\t\t}",
        "\t\tif (system != null)\n\t\t{\n"
        "\t\t\tTvOSStage5BAudioBridge.ShutdownEntered(\"Celeste.Audio.Unload\");\n"
        "\t\t\tCheckFmod(system.unloadAll(), \"FMOD_Studio_System_UnloadAll\");\n"
        "\t\t\tCheckFmod(system.release(), \"FMOD_Studio_System_Release\");\n"
        "\t\t\tsystem = null;\n\t\t\tready = false;\n"
        "\t\t\tTvOSStage5BAudioBridge.ShutdownCompleted(\"Celeste.Audio.Unload\");\n\t\t}",
        "checked audio shutdown",
    )
    replace_once(
        audio,
        "\t\tsystem.setListenerAttributes(0, attributes);",
        "\t\tCheckFmod(system.setListenerAttributes(0, attributes), \"FMOD_Studio_System_SetListenerAttributes\");\n"
        "\t\tTvOSStage5BAudioBridge.ListenerUpdated();",
        "listener evidence",
    )
    replace_once(
        audio,
        "\t\t\teventDescription.createInstance(out var instance);\n\t\t\teventDescription.is3D(out var is3D);",
        "\t\t\tCheckFmod(eventDescription.createInstance(out var instance), $\"createInstance:{path}\");\n"
        "\t\t\tCheckFmod(eventDescription.is3D(out var is3D), $\"is3D:{path}\");\n"
        "\t\t\tTvOSStage5BAudioBridge.RegisterEvent(instance, path, \"event\");",
        "event creation evidence",
    )
    replace_once(
        audio,
        "\t\t_event.createInstance(out var instance);\n\t\tif (start)",
        "\t\tCheckFmod(_event.createInstance(out var instance), $\"snapshot-create:{name}\");\n"
        "\t\tTvOSStage5BAudioBridge.RegisterEvent(instance, name, \"snapshot\");\n\t\tif (start)",
        "snapshot creation evidence",
    )
    replace_once(
        audio,
        "\t\tVCA vca;\n\t\tRESULT vCA = system.getVCA(path, out vca);",
        "\t\tVCA vca;\n\t\tRESULT vCA = system.getVCA(path, out vca);",
        "VCA method anchor",
    )
    replace_once(
        audio,
        "\tpublic static bool BusPaused(string path, bool? pause = null)\n\t{\n"
        "\t\tbool paused = false;\n"
        "\t\tif (system != null && system.getBus(path, out var bus) == RESULT.OK)\n\t\t{\n"
        "\t\t\tif (pause.HasValue)\n\t\t\t{\n\t\t\t\tbus.setPaused(pause.Value);\n\t\t\t}\n"
        "\t\t\tbus.getPaused(out paused);\n\t\t}\n\t\treturn paused;\n\t}",
        "\tpublic static bool BusPaused(string path, bool? pause = null)\n\t{\n"
        "\t\tif (system == null) return false;\n"
        "\t\tCheckFmod(system.getBus(path, out var bus), $\"getBus:{path}\");\n"
        "\t\tif (pause.HasValue)\n\t\t{\n"
        "\t\t\tCheckFmod(bus.setPaused(pause.Value), $\"Bus.setPaused:{path}\");\n"
        "\t\t\tCheckFmod(system.flushCommands(), \"FMOD_Studio_System_FlushCommands\");\n\t\t}\n"
        "\t\tCheckFmod(bus.getPaused(out bool paused), $\"Bus.getPaused:{path}\");\n"
        "\t\tTvOSStage5BAudioBridge.BusState(path, \"paused\", paused.ToString().ToLowerInvariant(), RESULT.OK);\n"
        "\t\treturn paused;\n\t}",
        "checked bus pause/readback",
    )
    replace_once(
        audio,
        "\tpublic static bool BusMuted(string path, bool? mute)\n\t{\n"
        "\t\tbool paused = false;\n"
        "\t\tif (system.getBus(path, out var bus) == RESULT.OK)\n\t\t{\n"
        "\t\t\tif (mute.HasValue)\n\t\t\t{\n\t\t\t\tbus.setMute(mute.Value);\n\t\t\t}\n"
        "\t\t\tbus.getPaused(out paused);\n\t\t}\n\t\treturn paused;\n\t}",
        "\tpublic static bool BusMuted(string path, bool? mute)\n\t{\n"
        "\t\tif (system == null) return false;\n"
        "\t\tCheckFmod(system.getBus(path, out var bus), $\"getBus:{path}\");\n"
        "\t\tif (mute.HasValue)\n\t\t{\n"
        "\t\t\tCheckFmod(bus.setMute(mute.Value), $\"Bus.setMute:{path}\");\n"
        "\t\t\tCheckFmod(system.flushCommands(), \"FMOD_Studio_System_FlushCommands\");\n\t\t}\n"
        "\t\tCheckFmod(bus.getMute(out bool muted), $\"Bus.getMute:{path}\");\n"
        "\t\tTvOSStage5BAudioBridge.BusState(path, \"muted\", muted.ToString().ToLowerInvariant(), RESULT.OK);\n"
        "\t\treturn muted;\n\t}",
        "checked bus mute/readback",
    )
    replace_once(
        audio,
        "\t\t\tif (volume.HasValue)\n\t\t\t{\n\t\t\t\tvca.setVolume(volume.Value);\n\t\t\t}\n"
        "\t\t\tvca.getVolume(out volume2, out finalvolume);",
        "\t\t\tif (volume.HasValue)\n\t\t\t{\n\t\t\t\tCheckFmod(vca.setVolume(volume.Value), $\"VCA.setVolume:{path}\");\n\t\t\t}\n"
        "\t\t\tCheckFmod(vca.getVolume(out volume2, out finalvolume), $\"VCA.getVolume:{path}\");\n"
        "\t\t\tif (volume.HasValue) TvOSStage5BAudioBridge.VcaVolume(path, volume.Value, volume2, finalvolume);",
        "VCA volume readback",
    )
    replace_once(
        audio,
        "\tprivate static void EndMainDownSnapshot()",
        "\tpublic static void Stage5BAllBanksLoaded() => TvOSStage5BAudioBridge.AllBanksLoaded(system);\n\n"
        "\tpublic static RESULT TriggerCueStage5B(EventInstance instance, string source)\n\t{\n"
        "\t\tif (instance == null) throw new InvalidOperationException($\"Music cue {source} has no current event instance.\");\n"
        "\t\tRESULT result = instance.triggerCue();\n\t\tCheckFmod(result, $\"triggerCue:{source}\");\n\t\treturn result;\n\t}\n\n"
        "\tprivate static void EndMainDownSnapshot()",
        "Stage 5B bank and cue boundaries",
    )

    ending = root / "Celeste" / "CS00_Ending.cs"
    replace_count(
        ending,
        "Audio.TriggerCueNoAudio(Audio.CurrentMusicEventInstance);",
        "Audio.TriggerCueStage5B(Audio.CurrentMusicEventInstance, \"prologue\");",
        2,
        "real Prologue music cues",
    )

    intro_vignette = root / "Celeste" / "IntroVignette.cs"
    replace_once(
        intro_vignette,
        "\tprivate void StopSfx()\n\t{\n\t\tAudio.Stop(sfx, allowFadeOut: false);\n\t}",
        "\tprivate void StopSfx()\n\t{\n"
        "\t\tif (sfx == null) return;\n"
        "\t\tAudio.Stop(sfx, allowFadeOut: false);\n"
        "\t\tsfx = null;\n\t}",
        "idempotent IntroVignette sound cleanup",
    )

    loader = root / "Celeste" / "GameLoader.cs"
    replace_once(
        loader,
        "\t\tAudio.Banks.DlcSfxs = Audio.Banks.Load(\"dlc_sfx\", loadStrings: false);\n\t\tSettings.Instance.ApplyVolumes();",
        "\t\tAudio.Banks.DlcSfxs = Audio.Banks.Load(\"dlc_sfx\", loadStrings: false);\n"
        "\t\tAudio.Stage5BAllBanksLoaded();\n\t\tSettings.Instance.ApplyVolumes();",
        "complete bank inventory",
    )

    event_instance = root / "FMOD" / "Studio" / "EventInstance.cs"
    method_replacements = {
        "\tpublic RESULT setPaused(bool paused)\n\t{\n\t\treturn FMOD_Studio_EventInstance_SetPaused(rawPtr, paused);\n\t}":
            "\tpublic RESULT setPaused(bool paused)\n\t{\n\t\tRESULT result = FMOD_Studio_EventInstance_SetPaused(rawPtr, paused);\n"
            "\t\treturn global::Celeste.TvOSStage5BAudioBridge.EventOperation(\"pause\", rawPtr, result, $\"value={paused.ToString().ToLowerInvariant()}\");\n\t}",
        "\tpublic RESULT start()\n\t{\n\t\treturn FMOD_Studio_EventInstance_Start(rawPtr);\n\t}":
            "\tpublic RESULT start()\n\t{\n\t\tRESULT result = FMOD_Studio_EventInstance_Start(rawPtr);\n"
            "\t\treturn global::Celeste.TvOSStage5BAudioBridge.EventOperation(\"start\", rawPtr, result);\n\t}",
        "\tpublic RESULT stop(STOP_MODE mode)\n\t{\n\t\treturn FMOD_Studio_EventInstance_Stop(rawPtr, mode);\n\t}":
            "\tpublic RESULT stop(STOP_MODE mode)\n\t{\n\t\tRESULT result = FMOD_Studio_EventInstance_Stop(rawPtr, mode);\n"
            "\t\treturn global::Celeste.TvOSStage5BAudioBridge.EventOperation(\"stop\", rawPtr, result, $\"mode={mode}\");\n\t}",
        "\tpublic RESULT release()\n\t{\n\t\treturn FMOD_Studio_EventInstance_Release(rawPtr);\n\t}":
            "\tpublic RESULT release()\n\t{\n\t\tRESULT result = FMOD_Studio_EventInstance_Release(rawPtr);\n"
            "\t\treturn global::Celeste.TvOSStage5BAudioBridge.EventOperation(\"release\", rawPtr, result);\n\t}",
        "\tpublic RESULT triggerCue()\n\t{\n\t\treturn FMOD_Studio_EventInstance_TriggerCue(rawPtr);\n\t}":
            "\tpublic RESULT triggerCue()\n\t{\n\t\tRESULT result = FMOD_Studio_EventInstance_TriggerCue(rawPtr);\n"
            "\t\treturn global::Celeste.TvOSStage5BAudioBridge.EventOperation(\"trigger-cue\", rawPtr, result);\n\t}",
    }
    for old, new in method_replacements.items():
        replace_once(event_instance, old, new, "FMOD EventInstance evidence hook")
    replace_once(
        event_instance,
        "\tpublic RESULT setParameterValue(string name, float value)\n\t{\n"
        "\t\treturn FMOD_Studio_EventInstance_SetParameterValue(rawPtr, Encoding.UTF8.GetBytes(name + \"\\0\"), value);\n\t}",
        "\tpublic RESULT setParameterValue(string name, float value)\n\t{\n"
        "\t\tRESULT result = FMOD_Studio_EventInstance_SetParameterValue(rawPtr, Encoding.UTF8.GetBytes(name + \"\\0\"), value);\n"
        "\t\treturn global::Celeste.TvOSStage5BAudioBridge.EventOperation(\"parameter\", rawPtr, result, $\"name={name}; value={value:0.###}\");\n\t}",
        "FMOD EventInstance named parameter evidence",
    )

    celeste = root / "Celeste" / "Celeste.cs"
    replace_once(
        celeste,
        "\t\tTvOSStage3CBridge.OnCelesteUpdate(Engine.Scene);",
        "\t\tTvOSStage3CBridge.OnCelesteUpdate(Engine.Scene);\n\t\tTvOSStage5BAudioBridge.SceneUpdated(Engine.Scene);",
        "scene audio evidence",
    )

    manifest = logical_manifest(root)
    output_lock = policy.get("stage5BOutput")
    if output_lock is not None and (
        manifest["fileCount"] != output_lock["fileCount"] or
        manifest["logicalSha256"] != output_lock["logicalSha256"]
    ):
        raise SystemExit("error: Stage 5B transformed output does not match the locked logical result")
    manifest.update({
        "schemaVersion": 1,
        "root": "$GENERATED_STAGE5B_ROOT",
        "gameVersion": policy["gameVersion"],
        "stage3CInputLogicalSha256": policy["stage3CInput"]["logicalSha256"],
        "audioMode": "TVOS_REAL_AUDIO",
        "restoredImportCount": len(restored),
        "restoredImportsByOriginalLibrary": dict(sorted(collections.Counter(item["originalLibrary"] for item in restored).items())),
        "deviceImportName": "__Internal",
        "fmodSdlRegistration": "unconditional before Studio initialization on tvOS",
        "prologueCueMode": "real EventInstance.triggerCue with checked RESULT",
        "restoredImports": restored,
    })
    return manifest


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True, help="copy of the accepted Stage 3C generated managed tree")
    parser.add_argument("--original-root", required=True, help="fresh locked decompiler output containing original FMOD externs")
    parser.add_argument("--templates", required=True)
    parser.add_argument("--policy", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    policy = json.loads(pathlib.Path(args.policy).read_text(encoding="utf-8"))
    manifest = transform(
        pathlib.Path(args.root).resolve(),
        pathlib.Path(args.original_root).resolve(),
        pathlib.Path(args.templates).resolve(),
        policy,
    )
    output = pathlib.Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"Stage 5B managed logical SHA-256: {manifest['logicalSha256']}")
    print("restored 490 generated FMOD imports to __Internal; selected real Celeste.Audio")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
