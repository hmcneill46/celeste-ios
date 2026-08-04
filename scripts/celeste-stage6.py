#!/usr/bin/env python3
"""Apply deterministic Stage 6 durable-state hooks to generated Celeste source."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import shutil
from typing import Any


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def logical_manifest(root: pathlib.Path) -> dict[str, Any]:
    files: list[dict[str, Any]] = []
    aggregate = hashlib.sha256()
    for path in sorted(root.rglob("*"), key=lambda value: value.relative_to(root).as_posix()):
        if not path.is_file() or any(part in {"bin", "obj"} for part in path.relative_to(root).parts):
            continue
        relative = path.relative_to(root).as_posix()
        digest = sha256(path)
        files.append({"path": relative, "size": path.stat().st_size, "sha256": digest})
        aggregate.update(relative.encode() + b"\0" + digest.encode() + b"\n")
    return {"fileCount": len(files), "logicalSha256": aggregate.hexdigest(), "files": files}


def replace_once(path: pathlib.Path, old: str, new: str, label: str) -> None:
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"error: {label} expected one match in {path.name}, found {count}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


def transform(root: pathlib.Path, templates: pathlib.Path, policy: dict[str, Any], mode: str) -> dict[str, Any]:
    actual = logical_manifest(root)
    expected = policy["generatedInputs"][mode]
    if actual["fileCount"] != expected["fileCount"] or actual["logicalSha256"] != expected["logicalSha256"]:
        raise SystemExit(f"error: {mode} input is not the locked Stage 3C/5B generated source")

    project = root / "Celeste.Modern.csproj"
    if mode == "realAudio":
        old_constants = "<DefineConstants>$(DefineConstants);TVOS;TVOS_STAGE3B;TVOS_STAGE3C;TVOS_STAGE5B;TVOS_REAL_AUDIO</DefineConstants>"
        new_constants = "<DefineConstants>$(DefineConstants);TVOS;TVOS_STAGE3B;TVOS_STAGE3C;TVOS_STAGE5B;TVOS_STAGE6;TVOS_REAL_AUDIO</DefineConstants>"
    else:
        old_constants = "<DefineConstants>$(DefineConstants);TVOS;TVOS_AUDIO_DISABLED;TVOS_STAGE3B;TVOS_STAGE3C</DefineConstants>"
        new_constants = "<DefineConstants>$(DefineConstants);TVOS;TVOS_AUDIO_DISABLED;TVOS_STAGE3B;TVOS_STAGE3C;TVOS_STAGE6</DefineConstants>"
    replace_once(project, old_constants, new_constants, "exclusive Stage 6 compile symbol")

    hook = templates / "TvOSStage6PersistenceHooks.cs"
    if not hook.is_file():
        raise SystemExit("error: Stage 6 persistence hook template is missing")
    shutil.copyfile(hook, root / "Celeste" / hook.name)

    user_io = root / "Celeste" / "UserIO.cs"
    replace_once(
        user_io,
        "\t\tstring handle = GetHandle(path);\n\t\tbool flag = false;",
        "\t\tTvOSStage6PersistenceHooks.ValidateLogicalName(path);\n\t\tstring handle = GetHandle(path);\n\t\tbool flag = false;",
        "pre-write allow-list enforcement",
    )
    replace_once(
        user_io,
        "\t\tif (!flag)\n\t\t{\n\t\t\tConsole.WriteLine(\"Save Failed\");\n\t\t}\n\t\treturn flag;",
        "\t\tif (!flag)\n\t\t{\n\t\t\tConsole.WriteLine(\"Save Failed\");\n\t\t}\n\t\telse\n\t\t{\n\t\t\tTvOSStage6PersistenceHooks.FileChanged(path, \"write\");\n\t\t}\n\t\treturn flag;",
        "successful write notification",
    )
    replace_once(
        user_io,
        "\t\t\t\tFile.WriteAllBytes(handle, data);\n\t\t\t\tbyte[] array = File.ReadAllBytes(handle);",
        "\t\t\t\tTvOSStage6PersistenceHooks.WriteApprovedFile(handle, data);\n\t\t\t\tbyte[] array = TvOSStage6PersistenceHooks.ReadApprovedFile(handle);",
        "AOT-safe GGP file write and verification",
    )
    replace_once(
        user_io,
        "\t\t\tusing (FileStream fileStream = File.Open(backupHandle, FileMode.Create, FileAccess.Write))\n\t\t\t{\n\t\t\t\tfileStream.Write(data, 0, data.Length);\n\t\t\t}",
        "\t\t\tTvOSStage6PersistenceHooks.WriteApprovedFile(backupHandle, data);",
        "AOT-safe backup file write",
    )
    replace_once(
        user_io,
        "\t\t\t\tFile.Copy(backupHandle, handle, overwrite: true);",
        "\t\t\t\tTvOSStage6PersistenceHooks.CopyApprovedFile(backupHandle, handle);",
        "AOT-safe validated file promotion",
    )
    replace_once(
        user_io,
        "\t\t\t\tusing (FileStream stream = File.OpenRead(path2))",
        "\t\t\t\tusing (Stream stream = TvOSStage6PersistenceHooks.OpenApprovedFile(path2))",
        "AOT-safe approved file read",
    )
    replace_once(
        user_io,
        "\tpublic static bool Delete(string path)\n\t{\n\t\tstring handle = GetHandle(path);",
        "\tpublic static bool Delete(string path)\n\t{\n\t\tTvOSStage6PersistenceHooks.ValidateLogicalName(path);\n\t\tstring handle = GetHandle(path);",
        "pre-delete allow-list enforcement",
    )
    replace_once(
        user_io,
        "\t\t\tFile.Delete(handle);\n\t\t\treturn true;",
        "\t\t\tFile.Delete(handle);\n\t\t\tstring backup = GetBackupHandle(path);\n\t\t\tif (File.Exists(backup)) File.Delete(backup);\n\t\t\tTvOSStage6PersistenceHooks.FileChanged(path, \"delete\");\n\t\t\treturn true;",
        "persistent deletion notification",
    )
    replace_once(
        user_io,
        "\tprivate static void SaveThread()\n\t{\n\t\tSavingResult = false;",
        "\tprivate static void SaveThread()\n\t{\n\t\tTvOSStage6PersistenceHooks.BeginBatch();\n\t\tSavingResult = false;",
        "save batch begin",
    )
    replace_once(
        user_io,
        "\t\tsavingInternal = false;\n\t}\n}",
        "\t\tSavingResult &= TvOSStage6PersistenceHooks.EndBatch(SavingResult, \"UserIO.SaveThread\");\n\t\tsavingInternal = false;\n\t}\n}",
        "save batch completion",
    )

    content_reads = [
        ("Celeste/PlaybackData.cs", "File.ReadAllBytes(path)", "TvOSStage6PersistenceHooks.ReadBundleFile(path)", "tutorial playback bytes"),
        ("Celeste/PreviewRecording.cs", "File.ReadAllBytes(filename)", "TvOSStage6PersistenceHooks.ReadBundleFile(filename)", "preview playback bytes"),
        ("Celeste/ObjModel.cs", "new BinaryReader(File.OpenRead(filename + \".export\"))", "new BinaryReader(TvOSStage6PersistenceHooks.OpenBundleFile(filename + \".export\"))", "exported mountain model"),
        ("Celeste/ObjModel.cs", "new StreamReader(filename)", "new StreamReader(TvOSStage6PersistenceHooks.OpenBundleFile(filename))", "text mountain model fallback"),
        ("Celeste/BinaryPacker.cs", "using FileStream input = File.OpenRead(filename);", "using Stream input = TvOSStage6PersistenceHooks.OpenBundleFile(filename);", "packed map input"),
        ("Celeste/Fonts.cs", "XmlReader.Create(File.OpenRead(text), xmlReaderSettings)", "XmlReader.Create(TvOSStage6PersistenceHooks.OpenBundleFile(text), xmlReaderSettings)", "font metadata"),
        ("Celeste/Language.cs", "new BinaryReader(File.OpenRead(path))", "new BinaryReader(TvOSStage6PersistenceHooks.OpenBundleFile(path))", "exported language"),
        ("Celeste/Language.cs", "File.ReadLines(path, Encoding.UTF8)", "TvOSStage6PersistenceHooks.ReadBundleLines(path, Encoding.UTF8)", "text language fallback"),
        ("Celeste/Dialog.cs", "File.ReadLines(text, Encoding.UTF8)", "TvOSStage6PersistenceHooks.ReadBundleLines(text, Encoding.UTF8)", "dialog character inventory"),
        ("Monocle/Calc.cs", "using FileStream inStream = File.OpenRead(filename);", "using Stream inStream = global::Celeste.TvOSStage6PersistenceHooks.OpenBundleFile(filename);", "content XML"),
    ]
    for relative, old, new, label in content_reads:
        replace_once(root / relative, old, new, f"AOT-safe {label} read")

    for relative, old_name in (("Monocle/VirtualTexture.cs", "fileStream"), ("Monocle/VirtualTexture.cs", "stream2"), ("Monocle/VirtualTexture.cs", "stream")):
        path = root / relative
        old = f"using (FileStream {old_name} = File.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))"
        new = f"using (Stream {old_name} = global::Celeste.TvOSStage6PersistenceHooks.OpenBundleFile(System.IO.Path.Combine(Engine.ContentDirectory, Path)))"
        replace_once(path, old, new, f"AOT-safe virtual texture {old_name} read")

    atlas = root / "Monocle" / "Atlas.cs"
    for variable, suffix in (("input2", "path"), ("input", "path + \".bin\""), ("fileStream2", "path + \".meta\""), ("fileStream", "path + \".meta\"")):
        old = f"using FileStream {variable} = File.OpenRead(Path.Combine(Engine.ContentDirectory, {suffix}));"
        new = f"using Stream {variable} = global::Celeste.TvOSStage6PersistenceHooks.OpenBundleFile(Path.Combine(Engine.ContentDirectory, {suffix}));"
        replace_once(atlas, old, new, f"AOT-safe atlas {variable} read")

    error_log = root / "Monocle" / "ErrorLog.cs"
    replace_once(
        error_log,
        "\t\t\tStreamReader streamReader = new StreamReader(Filename);\n\t\t\ttext = streamReader.ReadToEnd();\n\t\t\tstreamReader.Close();",
        "\t\t\ttext = global::Celeste.TvOSStage6PersistenceHooks.ReadSessionText(Filename);",
        "AOT-safe excluded error-log read",
    )
    replace_once(
        error_log,
        "\t\tStreamWriter streamWriter = new StreamWriter(Filename, append: false);\n\t\tstreamWriter.Write(stringBuilder.ToString());\n\t\tstreamWriter.Close();",
        "\t\tglobal::Celeste.TvOSStage6PersistenceHooks.WriteSessionText(Filename, stringBuilder.ToString());",
        "AOT-safe excluded error-log write",
    )

    overworld = root / "Celeste" / "Overworld.cs"
    replace_once(
        overworld,
        "\t\tType[] types = Assembly.GetExecutingAssembly().GetTypes();\n"
        "\t\tforeach (Type type in types)\n"
        "\t\t{\n"
        "\t\t\tif (typeof(Oui).IsAssignableFrom(type) && !type.IsAbstract)\n"
        "\t\t\t{\n"
        "\t\t\t\tOui oui = (Oui)Activator.CreateInstance(type);\n"
        "\t\t\t\toui.Visible = false;\n"
        "\t\t\t\tAdd(oui);\n"
        "\t\t\t\tUIs.Add(oui);\n"
        "\t\t\t\tif (oui.IsStart(this, startMode))\n"
        "\t\t\t\t{\n"
        "\t\t\t\t\toui.Visible = true;\n"
        "\t\t\t\t\tLast = (Current = oui);\n"
        "\t\t\t\t}\n"
        "\t\t\t}\n"
        "\t\t}",
        "\t\tOui[] menus = new Oui[10]\n"
        "\t\t{\n"
        "\t\t\tnew OuiAssistMode(), new OuiChapterPanel(), new OuiChapterSelect(), new OuiCredits(),\n"
        "\t\t\tnew OuiFileNaming(), new OuiFileSelect(), new OuiJournal(), new OuiMainMenu(),\n"
        "\t\t\tnew OuiOptions(), new OuiTitleScreen()\n"
        "\t\t};\n"
        "\t\tforeach (Oui oui in menus)\n"
        "\t\t{\n"
        "\t\t\toui.Visible = false;\n"
        "\t\t\tAdd(oui);\n"
        "\t\t\tUIs.Add(oui);\n"
        "\t\t\tif (oui.IsStart(this, startMode))\n"
        "\t\t\t{\n"
        "\t\t\t\toui.Visible = true;\n"
        "\t\t\t\tLast = (Current = oui);\n"
        "\t\t\t}\n"
        "\t\t}",
        "AOT-safe exact Oui factory",
    )

    # Mono's full-AOT runtime implements a generic `new T()` through a
    # generated InvokeStub.  The first normal Chapter 1 dash reaches
    # Pooler.Create<TrailManager.Snapshot> and cannot emit that stub on tvOS.
    # Keep the accepted attribute discovery, but construct the exact locked
    # 1.4.0.0 pooled set through ordinary statically visible constructors.
    for relative, old, new, label in (
        ("Celeste/BounceBlock.cs", "\tprivate class RespawnDebris : Entity", "\tinternal class RespawnDebris : Entity", "BounceBlock.RespawnDebris visibility"),
        ("Celeste/BounceBlock.cs", "\tprivate class BreakDebris : Entity", "\tinternal class BreakDebris : Entity", "BounceBlock.BreakDebris visibility"),
        ("Celeste/MoveBlock.cs", "\tprivate class Debris : Actor", "\tinternal class Debris : Actor", "MoveBlock.Debris visibility"),
        ("Celeste/Seeker.cs", "\tprivate class RecoverBlast : Entity", "\tinternal class RecoverBlast : Entity", "Seeker.RecoverBlast visibility"),
    ):
        replace_once(root / relative, old, new, f"AOT pool factory {label}")

    pooler = root / "Monocle" / "Pooler.cs"
    replace_once(
        pooler,
        "\tpublic T Create<T>() where T : Entity, new()\n"
        "\t{\n"
        "\t\tif (!Pools.ContainsKey(typeof(T)))\n"
        "\t\t{\n"
        "\t\t\treturn new T();\n"
        "\t\t}\n"
        "\t\tQueue<Entity> queue = Pools[typeof(T)];\n"
        "\t\tif (queue.Count == 0)\n"
        "\t\t{\n"
        "\t\t\treturn new T();\n"
        "\t\t}\n"
        "\t\treturn queue.Dequeue() as T;\n"
        "\t}",
        "\tpublic T Create<T>() where T : Entity, new()\n"
        "\t{\n"
        "\t\tType type = typeof(T);\n"
        "\t\tif (!Pools.TryGetValue(type, out Queue<Entity> queue))\n"
        "\t\t{\n"
        "\t\t\tthrow new InvalidOperationException(\"Unregistered pooled type: \" + type.FullName);\n"
        "\t\t}\n"
        "\t\tif (queue.Count == 0)\n"
        "\t\t{\n"
        "\t\t\treturn (T)CreateKnown(type);\n"
        "\t\t}\n"
        "\t\treturn (T)queue.Dequeue();\n"
        "\t}\n\n"
        "\tprivate static Entity CreateKnown(Type type)\n"
        "\t{\n"
        "\t\tif (type == typeof(global::Celeste.BounceBlock.BreakDebris)) return new global::Celeste.BounceBlock.BreakDebris();\n"
        "\t\tif (type == typeof(global::Celeste.BounceBlock.RespawnDebris)) return new global::Celeste.BounceBlock.RespawnDebris();\n"
        "\t\tif (type == typeof(global::Celeste.CrystalDebris)) return new global::Celeste.CrystalDebris();\n"
        "\t\tif (type == typeof(global::Celeste.Debris)) return new global::Celeste.Debris();\n"
        "\t\tif (type == typeof(global::Celeste.FinalBossBeam)) return new global::Celeste.FinalBossBeam();\n"
        "\t\tif (type == typeof(global::Celeste.FinalBossShot)) return new global::Celeste.FinalBossShot();\n"
        "\t\tif (type == typeof(global::Celeste.MoveBlock.Debris)) return new global::Celeste.MoveBlock.Debris();\n"
        "\t\tif (type == typeof(global::Celeste.Seeker.RecoverBlast)) return new global::Celeste.Seeker.RecoverBlast();\n"
        "\t\tif (type == typeof(global::Celeste.SlashFx)) return new global::Celeste.SlashFx();\n"
        "\t\tif (type == typeof(global::Celeste.SpeedRing)) return new global::Celeste.SpeedRing();\n"
        "\t\tif (type == typeof(global::Celeste.TempleBigEyeballShockwave)) return new global::Celeste.TempleBigEyeballShockwave();\n"
        "\t\tif (type == typeof(global::Celeste.TrailManager.Snapshot)) return new global::Celeste.TrailManager.Snapshot();\n"
        "\t\tthrow new InvalidOperationException(\"Missing AOT pooled factory for: \" + type.FullName);\n"
        "\t}",
        "AOT-safe exact 12-type pooled factory",
    )

    output = logical_manifest(root)
    return {
        "schemaVersion": 1,
        "mode": mode,
        "input": {"fileCount": actual["fileCount"], "logicalSha256": actual["logicalSha256"]},
        "output": output,
        "hook": "Celeste/TvOSStage6PersistenceHooks.cs",
        "allowListedLogicalNames": [item["logicalName"] for item in policy["writableFiles"]],
        "generatedSourceTracked": False,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True, type=pathlib.Path)
    parser.add_argument("--templates", required=True, type=pathlib.Path)
    parser.add_argument("--policy", required=True, type=pathlib.Path)
    parser.add_argument("--mode", required=True, choices=("noAudio", "realAudio"))
    parser.add_argument("--output", required=True, type=pathlib.Path)
    args = parser.parse_args()
    policy = json.loads(args.policy.read_text(encoding="utf-8"))
    result = transform(args.root.resolve(), args.templates.resolve(), policy, args.mode)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"Stage 6 {args.mode} generated source: {result['output']['logicalSha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
