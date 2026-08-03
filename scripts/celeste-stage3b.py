#!/usr/bin/env python3
"""Deterministic Stage 3B generated-source and runtime-content helpers."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import pathlib
import re
import shutil
import subprocess
from typing import Any, Iterable


def sha256_file(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_json(path: pathlib.Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(value, indent=2, sort_keys=True) + "\n"
    if "/Users/" in text or re.search(r"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", text):
        raise SystemExit(f"error: refusing to write private path or email to {path.name}")
    path.write_text(text, encoding="utf-8")


def load_json(path: pathlib.Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def replace_once(path: pathlib.Path, old: str, new: str, label: str) -> None:
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"error: {label} expected one source match in {path.name}, found {count}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


def logical_files(root: pathlib.Path) -> Iterable[pathlib.Path]:
    for path in sorted(root.rglob("*"), key=lambda item: item.relative_to(root).as_posix()):
        if not path.is_file() or any(part in {"bin", "obj"} for part in path.relative_to(root).parts):
            continue
        yield path


def logical_manifest(root: pathlib.Path) -> dict[str, Any]:
    aggregate = hashlib.sha256()
    entries: list[dict[str, Any]] = []
    for path in logical_files(root):
        relative = path.relative_to(root).as_posix()
        digest = sha256_file(path)
        entries.append({"path": relative, "sha256": digest, "size": path.stat().st_size})
        aggregate.update(relative.encode("utf-8") + b"\0" + digest.encode("ascii") + b"\n")
    return {
        "fileCount": len(entries),
        "logicalSha256": aggregate.hexdigest(),
        "files": entries,
    }


def cmd_transform(args: argparse.Namespace) -> None:
    root = pathlib.Path(args.root).resolve()
    templates = pathlib.Path(args.templates).resolve()
    policy = load_json(pathlib.Path(args.policy))
    stage3a = load_json(pathlib.Path(args.stage3a_manifest))
    expected = policy["stage3ABaseline"]
    if stage3a.get("fileCount") != expected["patchedSourceFileCount"] or \
            stage3a.get("logicalSha256") != expected["patchedSourceLogicalSha256"]:
        raise SystemExit("error: Stage 3B input is not the accepted Stage 3A patched-source tree")
    if not (root / "Celeste" / "Celeste.cs").is_file():
        raise SystemExit("error: generated Celeste source root is incomplete")

    project = root / "Celeste.Modern.csproj"
    replace_once(
        project,
        "<DefineConstants>$(DefineConstants);TVOS;TVOS_AUDIO_DISABLED</DefineConstants>",
        "<DefineConstants>$(DefineConstants);TVOS;TVOS_AUDIO_DISABLED;TVOS_STAGE3B</DefineConstants>",
        "Stage 3B compile symbol",
    )

    audio = root / "Celeste" / "Audio.cs"
    audio_text = audio.read_text(encoding="utf-8")
    if "#if !TVOS_AUDIO_DISABLED" in audio_text:
        raise SystemExit("error: Stage 3B audio transform was already applied")
    audio.write_text("#if !TVOS_AUDIO_DISABLED\n" + audio_text + "\n#endif\n", encoding="utf-8")
    for name in ("Audio.TvOSDisabled.cs", "TvOSSettingsSerializer.cs", "TvOSStage3Bridge.cs"):
        source = templates / name
        if not source.is_file():
            raise SystemExit(f"error: Stage 3B template is missing: {name}")
        shutil.copyfile(source, root / "Celeste" / name)

    fmod_pattern = re.compile(
        r'=> throw new global::System\.PlatformNotSupportedException\('
        r'"FMOD native call ([A-Za-z0-9_]+) is unavailable in the Stage 3A closure harness\."\);'
    )
    fmod_count = 0
    fmod_symbols: list[str] = []
    for path in sorted(root.rglob("*.cs"), key=lambda item: item.relative_to(root).as_posix()):
        text = path.read_text(encoding="utf-8")

        def fmod_replace(match: re.Match[str]) -> str:
            nonlocal fmod_count
            fmod_count += 1
            fmod_symbols.append(match.group(1))
            return f'=> throw global::Celeste.TvOSStage3Bridge.FmodLowLevelReached("{match.group(1)}");'

        patched = fmod_pattern.sub(fmod_replace, text)
        if patched != text:
            path.write_text(patched, encoding="utf-8")
    if fmod_count != 490:
        raise SystemExit(f"error: Stage 3B centralized {fmod_count} FMOD guards, expected 490")

    user_io = root / "Celeste" / "UserIO.cs"
    replace_once(
        user_io,
        "\tprivate static string GetSavePath(string dir)\n\t{\n\t\tstring text = SDL.SDL_GetPlatform();",
        "\tprivate static string GetSavePath(string dir)\n\t{\n#if TVOS_STAGE3B\n"
        "\t\tstring sessionRoot = Environment.GetEnvironmentVariable(\"CELESTE_TVOS_SESSION_ROOT\");\n"
        "\t\tif (string.IsNullOrWhiteSpace(sessionRoot))\n\t\t{\n"
        "\t\t\tthrow new InvalidOperationException(\"Stage 3B requires an ephemeral settings session root.\");\n"
        "\t\t}\n\t\treturn Path.Combine(sessionRoot, dir);\n#else\n"
        "\t\tstring text = SDL.SDL_GetPlatform();",
        "ephemeral UserIO root",
    )
    replace_once(
        user_io,
        "\t\treturn Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dir);\n\t}\n\n\tprivate static string GetHandle",
        "\t\treturn Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dir);\n#endif\n\t}\n\n\tprivate static string GetHandle",
        "ephemeral UserIO root terminator",
    )
    replace_once(
        user_io,
        "\tprivate static T Deserialize<T>(Stream stream) where T : class\n\t{\n"
        "\t\treturn (T)new XmlSerializer(typeof(T)).Deserialize(stream);\n\t}",
        "\tprivate static T Deserialize<T>(Stream stream) where T : class\n\t{\n#if TVOS_STAGE3B\n"
        "\t\tif (typeof(T) == typeof(Settings))\n\t\t{\n"
        "\t\t\treturn (T)(object)TvOSSettingsSerializer.Deserialize(stream);\n\t\t}\n"
        "\t\tthrow new PlatformNotSupportedException(\"Stage 3B does not deserialize save data; durable saves are Stage 6.\");\n"
        "#else\n\t\treturn (T)new XmlSerializer(typeof(T)).Deserialize(stream);\n#endif\n\t}",
        "AOT-safe Settings deserialize route",
    )
    replace_once(
        user_io,
        "\tpublic static byte[] Serialize<T>(T instance)\n\t{\n"
        "\t\tusing MemoryStream memoryStream = new MemoryStream();\n"
        "\t\tnew XmlSerializer(typeof(T)).Serialize(memoryStream, instance);\n"
        "\t\treturn memoryStream.ToArray();\n\t}",
        "\tpublic static byte[] Serialize<T>(T instance)\n\t{\n#if TVOS_STAGE3B\n"
        "\t\tif (typeof(T) == typeof(Settings))\n\t\t{\n"
        "\t\t\treturn TvOSSettingsSerializer.SerializeToBytes((Settings)(object)instance);\n\t\t}\n"
        "\t\tthrow new PlatformNotSupportedException(\"Stage 3B does not serialize save data; durable saves are Stage 6.\");\n"
        "#else\n\t\tusing MemoryStream memoryStream = new MemoryStream();\n"
        "\t\tnew XmlSerializer(typeof(T)).Serialize(memoryStream, instance);\n"
        "\t\treturn memoryStream.ToArray();\n#endif\n\t}",
        "AOT-safe Settings serialize route",
    )

    celeste = root / "Celeste" / "Celeste.cs"
    replace_once(celeste, "\t\tInstance = this;", "\t\tInstance = this;\n\t\tTvOSStage3Bridge.Checkpoint(\"celeste-constructor\");", "constructor checkpoint")
    replace_once(celeste, "\t\tbase.Initialize();\n\t\tSettings.Instance.AfterLoad();", "\t\tbase.Initialize();\n\t\tTvOSStage3Bridge.Checkpoint(\"graphics-device-available\", $\"adapter={base.GraphicsDevice.Adapter.Description}\");\n\t\tSettings.Instance.AfterLoad();", "graphics checkpoint")
    replace_once(celeste, "\t\tbase.LoadContent();\n\t\tConsole.WriteLine(\"BEGIN LOAD\");", "\t\tbase.LoadContent();\n\t\tTvOSStage3Bridge.Checkpoint(\"content-manager-created\", \"root=Content\");\n\t\tConsole.WriteLine(\"BEGIN LOAD\");", "content checkpoint")
    replace_once(celeste, "\t\tbase.Update(gameTime);\n\t\tInput.UpdateGrab();", "\t\tbase.Update(gameTime);\n\t\tTvOSStage3Bridge.RecordUpdate(Engine.Scene);\n\t\tInput.UpdateGrab();", "first update instrumentation")
    replace_once(celeste, "\t\tbase.RenderCore();\n\t\tif (DisconnectUI != null)", "\t\tbase.RenderCore();\n\t\tTvOSStage3Bridge.RecordDraw(Engine.Scene, base.GraphicsDevice);\n\t\tif (DisconnectUI != null)", "first draw instrumentation")
    replace_once(celeste, "\t\t\tSettings.Initialize();\n\t\t\t_ = Settings.Existed;", "\t\t\tSettings.Initialize();\n\t\t\tTvOSStage3Bridge.Checkpoint(\"settings-load\", $\"existed={Settings.Existed}\");\n\t\t\t_ = Settings.Existed;", "settings checkpoint")
    replace_once(celeste, "\t\t\tceleste = new Celeste();", "\t\t\tTvOSStage3Bridge.Checkpoint(\"celeste-construction-start\");\n\t\t\tceleste = new Celeste();", "construction-start checkpoint")
    original_catch = """\t\tcatch (Exception ex)\n\t\t{\n\t\t\tConsole.WriteLine(ex.ToString());\n\t\t\tErrorLog.Write(ex);\n\t\t\ttry\n\t\t\t{\n\t\t\t\tErrorLog.Open();\n\t\t\t\treturn;\n\t\t\t}\n\t\t\tcatch\n\t\t\t{\n\t\t\t\tConsole.WriteLine(\"Failed to open the log!\");\n\t\t\t\treturn;\n\t\t\t}\n\t\t}\n\t\tceleste.RunWithLogging();"""
    replacement_catch = """\t\tcatch (Exception ex)\n\t\t{\n\t\t\tConsole.WriteLine(ex.ToString());\n\t\t\tErrorLog.Write(ex);\n#if TVOS_STAGE3B\n\t\t\tTvOSStage3Bridge.RecordFatal(ex, \"Celeste.Run startup\");\n\t\t\tthrow;\n#else\n\t\t\ttry\n\t\t\t{\n\t\t\t\tErrorLog.Open();\n\t\t\t\treturn;\n\t\t\t}\n\t\t\tcatch\n\t\t\t{\n\t\t\t\tConsole.WriteLine(\"Failed to open the log!\");\n\t\t\t\treturn;\n\t\t\t}\n#endif\n\t\t}\n\t\tceleste.RunWithLogging();\n\t\tTvOSStage3Bridge.ThrowIfFatal();"""
    replace_once(celeste, original_catch, replacement_catch, "startup exception propagation")
    original_process = """\tprivate static void CallProcess(string path, string args = \"\", bool createWindow = false)\n\t{\n\t\tProcess process = new Process();\n\t\tprocess.StartInfo = new ProcessStartInfo\n\t\t{\n\t\t\tFileName = path,\n\t\t\tWorkingDirectory = Path.GetDirectoryName(path),\n\t\t\tRedirectStandardOutput = false,\n\t\t\tCreateNoWindow = !createWindow,\n\t\t\tUseShellExecute = false,\n\t\t\tArguments = args\n\t\t};\n\t\tprocess.Start();\n\t\tprocess.WaitForExit();\n\t}"""
    replacement_process = """\tprivate static void CallProcess(string path, string args = \"\", bool createWindow = false)\n\t{\n#if TVOS_STAGE3B\n\t\tTvOSStage3Bridge.Checkpoint(\"desktop-process-launch-blocked\");\n\t\tthrow new PlatformNotSupportedException(\"Process launching is unavailable on tvOS.\");\n#else\n\t\tProcess process = new Process();\n\t\tprocess.StartInfo = new ProcessStartInfo\n\t\t{\n\t\t\tFileName = path,\n\t\t\tWorkingDirectory = Path.GetDirectoryName(path),\n\t\t\tRedirectStandardOutput = false,\n\t\t\tCreateNoWindow = !createWindow,\n\t\t\tUseShellExecute = false,\n\t\t\tArguments = args\n\t\t};\n\t\tprocess.Start();\n\t\tprocess.WaitForExit();\n#endif\n\t}"""
    replace_once(celeste, original_process, replacement_process, "desktop process isolation")

    engine = root / "Monocle" / "Engine.cs"
    replace_once(
        engine,
        "\t\tGCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;",
        "#if TVOS_STAGE3B\n"
        "\t\tglobal::Celeste.TvOSStage3Bridge.Checkpoint(\"gc-latency-mode-suppressed\", \"requested=SustainedLowLatency\");\n"
        "#else\n\t\tGCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;\n#endif",
        "unsupported tvOS GC latency-mode isolation",
    )
    replace_once(
        engine,
        "\t\tcatch (Exception ex)\n\t\t{\n\t\t\tConsole.WriteLine(ex.ToString());\n\t\t\tErrorLog.Write(ex);\n\t\t\tErrorLog.Open();\n\t\t}",
        "\t\tcatch (Exception ex)\n\t\t{\n\t\t\tConsole.WriteLine(ex.ToString());\n\t\t\tErrorLog.Write(ex);\n#if TVOS_STAGE3B\n"
        "\t\t\tglobal::Celeste.TvOSStage3Bridge.RecordFatal(ex, \"Engine.RunWithLogging\");\n\t\t\tthrow;\n#else\n"
        "\t\t\tErrorLog.Open();\n#endif\n\t\t}",
        "engine exception propagation",
    )

    error_log = root / "Monocle" / "ErrorLog.cs"
    replace_once(
        error_log,
        "\tpublic static void Open()\n\t{\n\t\tif (!global::Celeste.Celeste.IsGGP && File.Exists(Filename))\n\t\t{\n\t\t\tProcess.Start(Filename);\n\t\t}\n\t}",
        "\tpublic static void Open()\n\t{\n#if TVOS_STAGE3B\n"
        "\t\tglobal::Celeste.TvOSStage3Bridge.Checkpoint(\"desktop-error-log-open-suppressed\");\n#else\n"
        "\t\tif (!global::Celeste.Celeste.IsGGP && File.Exists(Filename))\n\t\t{\n\t\t\tProcess.Start(Filename);\n\t\t}\n#endif\n\t}",
        "desktop error-log isolation",
    )
    replace_once(
        error_log,
        "\tprivate static string GetLogPath()\n\t{\n\t\tstring text = SDL.SDL_GetPlatform();",
        "\tprivate static string GetLogPath()\n\t{\n#if TVOS_STAGE3B\n"
        "\t\tstring sessionRoot = Environment.GetEnvironmentVariable(\"CELESTE_TVOS_SESSION_ROOT\");\n"
        "\t\tif (!string.IsNullOrWhiteSpace(sessionRoot))\n\t\t{\n"
        "\t\t\treturn Path.Combine(sessionRoot, \"errorLog.txt\");\n\t\t}\n#endif\n"
        "\t\tstring text = SDL.SDL_GetPlatform();",
        "ephemeral error-log path",
    )

    loader = root / "Celeste" / "GameLoader.cs"
    replace_once(loader, "\t\tactiveThread.Priority = ThreadPriority.Lowest;", "#if TVOS_STAGE3B\n\t\tTvOSStage3Bridge.Checkpoint(\"thread-priority-suppressed\", \"thread=main; requested=Lowest\");\n#else\n\t\tactiveThread.Priority = ThreadPriority.Lowest;\n#endif", "main-thread priority isolation")
    replace_once(loader, "\t\tactiveThread.Priority = ThreadPriority.Normal;", "#if !TVOS_STAGE3B\n\t\tactiveThread.Priority = ThreadPriority.Normal;\n#endif", "main-thread priority restore isolation")

    overworld_loader = root / "Celeste" / "OverworldLoader.cs"
    replace_once(overworld_loader, "\t\tactiveThread.Priority = ThreadPriority.Lowest;", "#if TVOS_STAGE3B\n\t\tTvOSStage3Bridge.Checkpoint(\"thread-priority-suppressed\", \"thread=main; requested=Lowest; loader=Overworld\");\n#else\n\t\tactiveThread.Priority = ThreadPriority.Lowest;\n#endif", "overworld main-thread priority isolation")
    replace_once(overworld_loader, "\t\tactiveThread.Priority = ThreadPriority.Normal;", "#if !TVOS_STAGE3B\n\t\tactiveThread.Priority = ThreadPriority.Normal;\n#endif", "overworld main-thread priority restore isolation")

    run_thread = root / "Celeste" / "RunThread.cs"
    replace_once(run_thread, "\t\tif (highPriority)\n\t\t{\n\t\t\tthread.Priority = ThreadPriority.Highest;\n\t\t}", "#if TVOS_STAGE3B\n\t\tif (highPriority)\n\t\t{\n\t\t\tTvOSStage3Bridge.Checkpoint(\"thread-priority-suppressed\", $\"thread={name}; requested=Highest\");\n\t\t}\n#else\n\t\tif (highPriority)\n\t\t{\n\t\t\tthread.Priority = ThreadPriority.Highest;\n\t\t}\n#endif", "worker-thread priority isolation")
    replace_once(run_thread, "\t\t\tErrorLog.Open();\n\t\t\tEngine.Instance.Exit();", "\t\t\tErrorLog.Open();\n#if TVOS_STAGE3B\n\t\t\tTvOSStage3Bridge.RecordFatal(ex, $\"RunThread:{Thread.CurrentThread.Name}\");\n#endif\n\t\t\tEngine.Instance.Exit();", "background failure propagation")

    settings_text = (root / "Celeste" / "Settings.cs").read_text(encoding="utf-8")
    declared = set(re.findall(
        r"^\tpublic (?!static|const)(?:[A-Za-z0-9_.<>?]+) ([A-Za-z0-9_]+)(?:\s*=(?!>).*?)?;$",
        settings_text,
        re.MULTILINE,
    ))
    expected_fields = set(policy["settingsSerializer"]["scalarFields"] + policy["settingsSerializer"]["bindingFields"])
    if declared != expected_fields:
        raise SystemExit(
            "error: explicit Settings serializer field policy drifted; "
            f"missing={sorted(declared - expected_fields)}, extra={sorted(expected_fields - declared)}"
        )

    if any(re.search(r'\[DllImport\("fmod(?:studio|_SDL)?"', path.read_text(encoding="utf-8")) for path in root.rglob("*.cs")):
        raise SystemExit("error: a native FMOD import remains after Stage 3B transformation")
    manifest = logical_manifest(root)
    manifest.update({
        "schemaVersion": 1,
        "root": "$GENERATED_RUNTIME_ROOT",
        "stage3AInputLogicalSha256": expected["patchedSourceLogicalSha256"],
        "fmodLowLevelGuardCount": fmod_count,
        "fmodLowLevelUniqueSymbolCount": len(set(fmod_symbols)),
        "audioMode": "TVOS_AUDIO_DISABLED high-level inert implementation; no native FMOD",
        "settingsSerializer": "explicit reflection-free Celeste 1.4.0.0 Settings XML",
        "transformations": policy["transformations"],
    })
    locked_output = policy["stage3BOutput"]
    if manifest["fileCount"] != locked_output["fileCount"] or \
            manifest["logicalSha256"] != locked_output["logicalSha256"]:
        raise SystemExit("error: Stage 3B transformed output does not match the locked logical result")
    write_json(pathlib.Path(args.output), manifest)
    print(f"Stage 3B managed logical SHA-256: {manifest['logicalSha256']}")
    print("centralized 490 FMOD fail-fast guards; installed explicit no-audio and Settings paths")


def clone_or_copy(source: pathlib.Path, destination: pathlib.Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    result = subprocess.run(["cp", "-c", str(source), str(destination)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if result.returncode != 0:
        shutil.copyfile(source, destination)


def cmd_stage_content(args: argparse.Namespace) -> None:
    game_root = pathlib.Path(args.game_root).expanduser().resolve()
    source = game_root / "Content"
    destination = pathlib.Path(args.destination).resolve()
    policy = load_json(pathlib.Path(args.policy))["content"]
    input_manifest = load_json(pathlib.Path(args.input_manifest))
    if input_manifest.get("validation") != "supported-unmodified-fna-release":
        raise SystemExit("error: content staging requires the validated locked input manifest")
    if destination.exists() and any(destination.iterdir()):
        raise SystemExit("error: content staging destination must be empty")
    destination.mkdir(parents=True, exist_ok=True)

    excluded_prefixes = tuple(policy["excludedPrefixes"])
    aggregate = hashlib.sha256()
    entries: list[dict[str, Any]] = []
    excluded_count = 0
    excluded_bytes = 0
    for path in sorted(source.rglob("*"), key=lambda item: item.relative_to(source).as_posix()):
        relative = path.relative_to(source).as_posix()
        if path.is_symlink():
            raise SystemExit(f"error: content symlink is forbidden: $CELESTE_GAME_ROOT/Content/{relative}")
        if not path.is_file():
            continue
        size = path.stat().st_size
        if relative.startswith(excluded_prefixes):
            excluded_count += 1
            excluded_bytes += size
            continue
        lowered = relative.lower()
        if path.suffix.lower() in {".celeste", ".log"} or any(token in lowered for token in ("everest", "monomod")):
            raise SystemExit(f"error: forbidden non-runtime material in Content: {relative}")
        digest = sha256_file(path)
        clone_or_copy(path, destination / relative)
        entries.append({"path": relative, "sha256": digest, "size": size})
        aggregate.update(relative.encode("utf-8") + b"\0" + str(size).encode("ascii") + b"\0" + digest.encode("ascii") + b"\n")

    for asset in policy["representativeXnbAssets"]:
        staged = destination / asset["relativePath"]
        if not staged.is_file() or sha256_file(staged) != asset["sha256"]:
            raise SystemExit(f"error: representative XNB staging failed: {asset['relativePath']}")
    result = {
        "schemaVersion": 1,
        "sourceRoot": "$CELESTE_GAME_ROOT/Content",
        "destinationRoot": "$CELESTE_RUNTIME_ROOT/content/Content",
        "inputContentAggregateSha256": input_manifest["content"]["aggregateSha256"],
        "inputContentFileCount": input_manifest["content"]["fileCount"],
        "inputContentTotalBytes": input_manifest["content"]["totalBytes"],
        "stagedFileCount": len(entries),
        "stagedTotalBytes": sum(item["size"] for item in entries),
        "stagedAggregateSha256": aggregate.hexdigest(),
        "excludedPrefixes": list(excluded_prefixes),
        "excludedFileCount": excluded_count,
        "excludedTotalBytes": excluded_bytes,
        "copyMode": "APFS clone when supported, byte copy fallback",
        "caseSensitiveRelativePathsPreserved": True,
        "representativeXnbAssets": policy["representativeXnbAssets"],
        "files": entries,
    }
    expected_content = {
        "stagedFileCount": policy["expectedStagedFileCount"],
        "stagedTotalBytes": policy["expectedStagedTotalBytes"],
        "stagedAggregateSha256": policy["expectedStagedAggregateSha256"],
        "excludedFileCount": policy["expectedExcludedFileCount"],
        "excludedTotalBytes": policy["expectedExcludedTotalBytes"],
    }
    for key, expected_value in expected_content.items():
        if result[key] != expected_value:
            raise SystemExit(f"error: staged Content {key} does not match the locked value")
    write_json(pathlib.Path(args.output), result)
    print(f"staged {len(entries)} non-audio content files ({result['stagedTotalBytes']} bytes)")
    print(f"excluded {excluded_count} FMOD content files ({excluded_bytes} bytes)")


def parser() -> argparse.ArgumentParser:
    value = argparse.ArgumentParser(description=__doc__)
    sub = value.add_subparsers(dest="command", required=True)
    transform = sub.add_parser("transform")
    transform.add_argument("--root", required=True)
    transform.add_argument("--templates", required=True)
    transform.add_argument("--policy", required=True)
    transform.add_argument("--stage3a-manifest", required=True)
    transform.add_argument("--output", required=True)
    transform.set_defaults(func=cmd_transform)
    content = sub.add_parser("stage-content")
    content.add_argument("--game-root", required=True)
    content.add_argument("--destination", required=True)
    content.add_argument("--policy", required=True)
    content.add_argument("--input-manifest", required=True)
    content.add_argument("--output", required=True)
    content.set_defaults(func=cmd_stage_content)
    return value


def main() -> int:
    args = parser().parse_args()
    args.func(args)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
