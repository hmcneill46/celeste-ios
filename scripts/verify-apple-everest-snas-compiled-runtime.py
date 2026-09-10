#!/usr/bin/env python3
"""Execute finite snas mechanisms in the unchanged compiled managed closure.

This host probe uses metadata-only graphics and an absent native audio backend.
It is neither a product verifier nor a physical-play receipt.
"""
from __future__ import annotations
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import xml.sax.saxutils as xml

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("assembly", "closure", "canonical-content", "work-root"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    work = args.work_root.resolve()
    if work.exists():
        raise ValueError("host runtime probe output already exists")
    for parent in (args.work_root.absolute(), *args.work_root.absolute().parents):
        if parent.is_symlink():
            raise ValueError("symlink output ancestor")
    if ROOT == work or ROOT in work.parents:
        subprocess.run(["git", "check-ignore", "--no-index", "-q", "--",
                        str((work / ".stage25kn-runtime-probe").relative_to(ROOT))], cwd=ROOT, check=True)
    source = args.closure / "content/Content/Maps/StrawberryJam2021/1-Beginner/snas.bin"
    if source.stat().st_size != 67718 or sha(source) != "6ad3172d496e8b5b4ce71f1128fe231b162d534419dc823d2af2cea27fc241d9":
        raise ValueError("original snas bytes differ")
    bing = args.closure / "content/Content/Maps/StrawberryJam2021/1-Beginner/Bing_Over_Google.bin"
    if sha(bing) != "e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347":
        raise ValueError("accepted Bing control differs")
    assembly = args.assembly.resolve()
    if assembly.name != "Celeste.dll":
        raise ValueError("actual untrimmed production Celeste assembly is required")
    before = {p.name: sha(p) for p in sorted(assembly.parent.glob("*.dll"))}
    if "FNA.dll" not in before:
        raise ValueError("original sibling assembly set missing")
    work.mkdir(parents=True)
    (work / ".stage25kn-runtime-probe").touch()
    binaries = work / "production"
    binaries.mkdir()
    for name in before:
        shutil.copyfile(assembly.parent / name, binaries / name)
    # Consume the exact canonical Packer frame geometry, never invented sprite
    # sizes. Pixels are deliberately not loaded by this graphics-free probe.
    spec = importlib.util.spec_from_file_location("atlas_reader", ROOT / "scripts/inventory-celeste-controller-prompts.py")
    atlas = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(atlas)
    metadata = args.canonical_content / "Graphics/Atlases/Gameplay.meta"
    if sha(metadata) != "1f23b1f35544a3751ffec170b0230c7c9513000ab1549f78ebb9cb9f4203c8d9":
        raise ValueError("canonical Gameplay geometry authority differs")
    reader = atlas.Reader(metadata.read_bytes())
    if reader.int32() != 5:
        raise ValueError("unsupported canonical atlas metadata")
    reader.string(); reader.int32()
    sheets = []
    for _ in range(reader.int16()):
        sheet = reader.string()
        image = metadata.parent / (sheet + ".data")
        with image.open("rb") as stream:
            width, height = struct.unpack("<ii", stream.read(8))
        frames = []
        for _ in range(reader.int16()):
            key = reader.string().replace("\\", "/")
            frames.append({"key": key, "rect": [reader.int16() for _ in range(8)]})
        sheets.append({"name": sheet, "width": width, "height": height, "frames": frames})
    if reader.position != len(reader.data):
        raise ValueError("unreviewed Gameplay atlas trailer")
    gui_metadata = args.canonical_content / "Graphics/Atlases/Gui.meta"
    if sha(gui_metadata) != "884ea39e604e34e3a45b005d164a5249de9a985e58fde7ef730193c47363525b":
        raise ValueError("canonical GUI geometry authority differs")
    reader = atlas.Reader(gui_metadata.read_bytes())
    if reader.int32() != 5:
        raise ValueError("unsupported GUI atlas metadata")
    reader.string(); reader.int32(); gui_sheets = []
    for _ in range(reader.int16()):
        sheet = reader.string()
        with (gui_metadata.parent / (sheet + ".data")).open("rb") as stream:
            width, height = struct.unpack("<ii", stream.read(8))
        frames = []
        for _ in range(reader.int16()):
            key = reader.string().replace("\\", "/")
            frames.append({"key": key, "rect": [reader.int16() for _ in range(8)]})
        gui_sheets.append({"name": sheet, "width": width, "height": height, "frames": frames})
    if reader.position != len(reader.data):
        raise ValueError("unreviewed GUI atlas trailer")
    manifest = json.loads((args.closure / "compatibility-manifest.json").read_text())
    for row in manifest["contentMounts"]:
        if row["sourcePath"].endswith(".xml") and sha(args.closure / "content/Content" / row["logicalPath"]) != row["sha256"]:
            raise ValueError("mounted XML bytes differ")
    foreground = [row for row in manifest["contentMounts"]
                  if row["sourcePath"] == "Graphics/SJ2021xmls/snas/ForegroundTiles.xml"]
    if len(foreground) != 1:
        raise ValueError("exact snas foreground binding missing or ambiguous")
    foreground_path = args.closure / "content/Content" / foreground[0]["logicalPath"]
    if sha(foreground_path) != foreground[0]["sha256"]:
        raise ValueError("snas foreground bytes differ")
    mounted = []
    for row in manifest["contentMounts"]:
        prefix = "Graphics/Atlases/Gameplay/"
        if not row["sourcePath"].startswith(prefix) or not row["sourcePath"].endswith(".png"):
            continue
        path = args.closure / "content/Content" / row["logicalPath"]
        if sha(path) != row["sha256"]:
            raise ValueError("mounted atlas bytes differ")
        with path.open("rb") as stream:
            header = stream.read(24)
        if header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
            raise ValueError("mounted texture is not a PNG")
        width, height = struct.unpack(">II", header[16:24])
        if width < 1 or height < 1:
            raise ValueError("mounted texture has invalid geometry")
        mounted.append({"key": row["sourcePath"][len(prefix):-4], "width": width,
                        "height": height, "sha256": row["sha256"]})
    sprites = args.canonical_content / "Graphics/Sprites.xml"
    if sha(sprites) != "baec20ec28911be5ef8acc6b181c6818095aa867c692c14d57d26e42c00fc04a":
        raise ValueError("canonical sprite bank authority differs")
    request = {"map": str(source.resolve()), "bingMap": str(bing.resolve()), "sheets": sheets, "spritesXml": str(sprites.resolve()),
               "guiSheets": gui_sheets,
               "foregroundXml": str(foreground_path.resolve()),
               "contentRoot": str((args.closure / "content/Content").resolve()),
               "mountedTextures": mounted, "factories": manifest["resolvedMapFactories"]}
    (work / "request.private.json").write_text(json.dumps(request, sort_keys=True) + "\n")
    shutil.copyfile(ROOT / "tools/AppleEverestBuilder/tests/SnasCompiledRuntimeProgram.cs.txt", work / "Program.cs")
    refs = "".join('<Reference Include="' + xml.escape(Path(name).stem) + '"><HintPath>' +
                   xml.escape(str(binaries / name)) + '</HintPath></Reference>' for name in before)
    (work / "Probe.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>' +
        '<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>' +
        '<Nullable>disable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems>' +
        '<UseSharedCompilation>false</UseSharedCompilation><BuildInParallel>false</BuildInParallel>' +
        '</PropertyGroup><ItemGroup><Compile Include="Program.cs"/>' + refs + '</ItemGroup></Project>\n')
    env = dict(os.environ, CELESTE_IOS_STORAGE_ROOT=str(work / "private-fixture-storage"))
    with (work / "commands.private.log").open("w") as log:
        subprocess.run(["dotnet", "build", str(work / "Probe.csproj"), "-c", "Release", "-m:1",
                        "-p:BuildInParallel=false", "-p:UseSharedCompilation=false", "--nologo"],
                       cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
        subprocess.run(["dotnet", str(work / "bin/Release/net10.0/Probe.dll"),
                        str(work / "request.private.json"), str(work / "result.json")],
                       cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
    for name, expected in before.items():
        if sha(assembly.parent / name) != expected or sha(binaries / name) != expected or sha(work / "bin/Release/net10.0" / name) != expected:
            raise ValueError("actual production assembly bytes changed during probe: " + name)
    result = json.loads((work / "result.json").read_text())
    if result["status"] != "PASS" or result["assemblySha256"] != before["Celeste.dll"]:
        raise ValueError("compiled runtime result is not bound to the actual assembly")
    (work / "source-bound-result.json").write_text(json.dumps({
        "result": result, "productionDlls": before, "originalAssemblyBytesUnchanged": True,
        "gameplayAtlasMetadataSha256": sha(metadata), "sourceMapSha256": sha(source),
        "guiAtlasMetadataSha256": sha(gui_metadata),
        "baselineBingMapSha256": sha(bing),
        "canonicalSpritesXmlSha256": sha(sprites),
        "snasForegroundXmlSha256": sha(foreground_path),
        "mountedGameplayTextureGeometry": mounted,
        "probeSourceSha256": sha(work / "Program.cs")}, indent=2, sort_keys=True) + "\n")
    print("PASS: " + str(result["checks"]) + " actual compiled snas runtime checks; physical/audio/rendering not claimed")


if __name__ == "__main__":
    main()
