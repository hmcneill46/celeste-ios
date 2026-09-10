#!/usr/bin/env python3
"""Compare complete original three-map terrain with the pinned Everest producer.

Keeps K-L's historical single-room test unchanged. This lane uses all original
rooms in source order and exact canonical/mounted texture geometry.
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
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
MAPS = {
    "StrawberryJam2021/0-Lobbies/1-Beginner": "a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2",
    "StrawberryJam2021/1-Beginner/Bing_Over_Google": "e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347",
    "StrawberryJam2021/1-Beginner/snas": "6ad3172d496e8b5b4ce71f1128fe231b162d534419dc823d2af2cea27fc241d9",
}
CANONICAL_XML = {
    "Graphics/AnimatedTiles.xml": "eedb00fc3eb3186cf92b14bc6ac1f080fc0409fa00975d62e84bc400df29e189",
    "Graphics/BackgroundTiles.xml": "fa2c03e2b41f72778bacbe2498927a5bfce81112e9645748d371606bd4ed016b",
    "Graphics/ForegroundTiles.xml": "1d31280a6ef0a81909186954648aa3e47e30de036404e3117916dbcdd0dfee65",
}


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / filename)
    result = importlib.util.module_from_spec(spec)
    sys.modules[name] = result
    spec.loader.exec_module(result)
    return result


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def check(value, reason):
    if not value:
        raise ValueError(reason)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("runtime", "closure", "canonical-content", "work-root"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    work = args.work_root.resolve()
    check(not work.exists(), "terrain work root exists")
    for parent in (args.work_root.absolute(), *args.work_root.absolute().parents):
        check(not parent.is_symlink(), "symlink output ancestor")
    if ROOT == work or ROOT in work.parents:
        subprocess.run(["git", "check-ignore", "--no-index", "-q", "--",
                        str((work / ".stage25kn-terrain").relative_to(ROOT))], cwd=ROOT, check=True)
    old = module("kn_pinned_terrain", "verify-apple-everest-autotiler-stage25kl.py")
    reader = module("kn_terrain_map_reader", "generate-apple-everest-stage25kl-content.py")
    atlas = module("kn_terrain_atlas", "inventory-celeste-controller-prompts.py")
    canonical = ROOT / ".build/celeste-ios/current/managed"
    upstream = ROOT / ".build/apple-everest/upstream/Everest/Celeste.Mod.mm/Patches"
    original = canonical / "Celeste/Autotiler.cs"
    pinned = upstream / "Autotiler.cs"
    check(sha(original) == old.CANONICAL_SHA and sha(pinned) == old.PINNED_SHA, "autotiler source pins differ")
    partial = args.runtime / "Celeste/Mod/AppleEverestStatic/AppleEverestAutotiler.cs"
    check(sha(partial) == sha(ROOT / "apple-everest/runtime/semantics/AppleEverestAutotiler.cs"), "applied terrain partial differs")
    mask = args.runtime / "Celeste/Mod/AppleEverestStatic/AppleEverestTileMaskRules.cs"
    check(sha(mask) == sha(ROOT / "apple-everest/runtime/AppleEverestTileMaskRules.cs"), "applied terrain guard differs")
    work.mkdir(parents=True)
    (work / ".stage25kn-terrain").touch()
    (work / "Pinned.cs").write_text(old.reference(original.read_text(), pinned.read_text()))
    for namespace, name in (("Celeste", "Autotiler"), ("Pinned", "ReferenceAutotiler")):
        (work / (name + "Bridge.cs")).write_text(old.BRIDGE.replace("NAMESPACE", namespace).replace("CLASS", name))
    copies = {
        args.runtime / "Celeste/Autotiler.cs": "Apple.cs", partial: "ApplePartial.cs", mask: "MaskRules.cs",
        canonical / "Monocle/VirtualMap.cs": "VirtualMap.cs",
        ROOT / "tools/AppleEverestBuilder/tests/SnasTerrainConformanceStubs.cs.txt": "Stubs.cs",
        ROOT / "tools/AppleEverestBuilder/tests/SnasTerrainConformanceProgram.cs.txt": "Program.cs",
    }
    for source, target in copies.items():
        shutil.copyfile(source, work / target)
    tileset = args.runtime / "Monocle/Tileset.cs"
    expected = (canonical / "Monocle/Tileset.cs").read_text().replace("tiles[x, y]", "tiles[x % tiles.GetLength(0), y % tiles.GetLength(1)]").replace(
        "tiles[index % tiles.GetLength(0), index / tiles.GetLength(0)]", "tiles[index % tiles.GetLength(0), (index / tiles.GetLength(0)) % tiles.GetLength(1)]")
    check(tileset.read_text() == expected, "applied tileset differs from pinned indexers")
    for expression in ("tiles[x % tiles.GetLength(0), y % tiles.GetLength(1)]", "tiles[index % tiles.GetLength(0), (index / tiles.GetLength(0)) % tiles.GetLength(1)]"):
        check(expression in (upstream / "Monocle/Tileset.cs").read_text(), "pinned modulo contract differs")
    shutil.copyfile(tileset, work / "Tileset.cs")
    # Extract canonical bounds and combined-map preparation mechanically. The
    # production segments must still contain these exact accepted operations.
    loader = (canonical / "Celeste/LevelLoader.cs").read_text()
    start = loader.index("Rectangle tileBounds = mapData.TileBounds;")
    end = loader.index("Vector2 position = new Vector2(tileBounds.X, tileBounds.Y) * 8f;", start)
    preparation = loader[start:end]
    check(preparation in (args.runtime / "Celeste/LevelLoader.cs").read_text(), "actual terrain preparation changed")
    level_source = (canonical / "Celeste/LevelData.cs").read_text()
    map_source = (canonical / "Celeste/MapData.cs").read_text()
    height = old.method(level_source, "if (Bounds.Height == 184)")
    tile_bounds = next(line.strip() for line in level_source.splitlines() if "public Rectangle TileBounds =>" in line)
    check(tile_bounds in map_source and tile_bounds in (args.runtime / "Celeste/LevelData.cs").read_text() and
          tile_bounds in (args.runtime / "Celeste/MapData.cs").read_text(), "tile bound operations differ")
    bounds = map_source[map_source.index("int num = int.MaxValue;"):map_source.index("ModeData.TotalStrawberries = 0;")]
    check(bounds in (args.runtime / "Celeste/MapData.cs").read_text() and height in (args.runtime / "Celeste/LevelData.cs").read_text(), "actual map bounds changed")
    (work / "LoaderTerrain.cs").write_text("using Monocle;\nusing Microsoft.Xna.Framework;\nusing System.Text.RegularExpressions;\nnamespace Conformance;\n" +
        "internal sealed class TerrainLevel { public Rectangle Bounds; public string Bg,Solids;\n" + tile_bounds + "\n" +
        "public TerrainLevel(Rectangle bounds,string solids,string bg) { Bounds=bounds; Solids=solids; Bg=bg;\n" + height + "} }\n" +
        "internal sealed class TerrainMap { public Rectangle Bounds; public TerrainLevel[] Levels; public Rectangle[] Filler;\n" + tile_bounds +
        "\ninternal void ComputeBounds() {\n" + bounds.replace("LevelData", "TerrainLevel") + "} }\n" +
        "internal static class LoaderTerrain { internal static (VirtualMap<char> Background,VirtualMap<char> Foreground,List<Rectangle> Bounds) Prepare(TerrainMap mapData) {\nList<Rectangle> bounds=new();\n" +
        preparation.replace("GFX.FGAutotiler.LevelBounds", "bounds").replace("LevelData", "TerrainLevel") + "return (virtualMap,virtualMap2,bounds); } }\n")
    manifest = json.loads((args.closure / "compatibility-manifest.json").read_text())
    mounts = {m["sourcePath"]: args.closure / "content/Content" / m["logicalPath"] for m in manifest["contentMounts"]}
    for row in manifest["contentMounts"]:
        check(sha(args.closure / "content/Content" / row["logicalPath"]) == row["sha256"], "staged content bytes differ")
    metadata = args.canonical_content / "Graphics/Atlases/Gameplay.meta"
    check(sha(metadata) == "1f23b1f35544a3751ffec170b0230c7c9513000ab1549f78ebb9cb9f4203c8d9", "canonical Gameplay geometry identity differs")
    ar = atlas.Reader(metadata.read_bytes());check(ar.int32() == 5, "unsupported canonical atlas")
    ar.string();ar.int32();dimensions = {}
    for _ in range(ar.int16()):
        ar.string()
        for _ in range(ar.int16()):
            key = ar.string().replace("\\", "/")
            frame = [ar.int16() for _ in range(8)]
            check(key.lower() not in {k.lower() for k in dimensions}, "ambiguous canonical texture key")
            dimensions[key] = frame[6:8]
    check(ar.position == len(ar.data), "unsupported atlas trailer")
    for path, file in mounts.items():
        prefix = "Graphics/Atlases/Gameplay/"
        if path.startswith(prefix) and path.endswith(".png"):
            data = file.read_bytes()[:24]
            check(data[:8] == b"\x89PNG\r\n\x1a\n" and data[12:16] == b"IHDR", "invalid texture header")
            key = path[len(prefix):-4]
            previous = next((k for k in dimensions if k.lower() == key.lower()), key)
            dimensions.pop(previous, None)
            dimensions[key] = list(struct.unpack(">II", data[16:24]))
    keys = {k.lower() for k in dimensions}
    check(all(w > 0 and h > 0 for w, h in dimensions.values()), "empty atlas dimensions")
    bindings = {row["Sid"]: row for row in json.loads((args.closure / "map-bindings.json").read_text())}
    cases = []
    for path, expected in CANONICAL_XML.items():
        check(sha(args.canonical_content / path) == expected, "canonical terrain XML authority differs: " + path)
    xml_evidence = {}
    for sid, digest in MAPS.items():
        source = mounts["Maps/" + sid + ".bin"]
        check(sha(source) == digest, "original selected map changed")
        parsed = reader.read_map(source.read_bytes());tree = parsed["tree"]
        binding = bindings[sid]
        check(binding["TerrainSeed"] == sum(map(ord, sid)), "raw SID seed changed")
        files = {}
        for field in ("ForegroundTiles", "BackgroundTiles", "AnimatedTiles"):
            files[field] = args.closure / "content/Content" / binding[field] if binding[field] else args.canonical_content / ("Graphics/" + field + ".xml")
        xml_evidence[sid] = {field: sha(path) for field, path in files.items()}
        animations = {}
        for row in ET.parse(files["AnimatedTiles"]).getroot():
            key = row.attrib["path"].lower();count = 0
            while (count == 0 and key in keys) or any(key + str(count).zfill(width) in keys for width in range(len(str(count)), len(str(count))+6)):
                count += 1
            check(count > 0 and row.attrib["name"] not in animations, "missing or ambiguous animation")
            animations[row.attrib["name"]] = count
        rooms = tree["children"][[n["name"] for n in tree["children"]].index("levels")]["children"]
        levels = [];entities = []
        for room in rooms:
            a = room["attributes"];children = {c["name"]: c for c in room["children"]}
            for layer in ("fgtiles", "bgtiles"):
                check(not children.get(layer, {}).get("attributes", {}).get("innerText", ""), "authored CSV scenery requires a separate closure")
            levels.append({"bounds": [a[k] for k in ("x", "y", "width", "height")],
                "solids": children["solids"]["attributes"].get("innerText", ""), "bg": children["bg"]["attributes"].get("innerText", "")})
            if sid.endswith("/snas"):
                for entity in children.get("entities", {}).get("children", []):
                    if entity["name"] == "dashBlock":
                        ea = entity["attributes"]
                        check(not ea.get("blendin", False), "unproved dash-block overlay branch")
                        entities.append({"kind": "dashBlock", "room": a["name"], "id": ea["id"], "tile": ea.get("tiletype", "3"),
                                         "width": ea["width"], "height": ea["height"]})
        filler = [n for parent, n in reader.walk(tree) if n["name"] == "Filler"]
        check(all(not n["children"] for n in filler), "unreviewed authored filler")
        cases.append({"sid": sid, "seed": binding["TerrainSeed"], "levels": levels, "filler": [], "terrainEntities": entities,
            "foreground": str(files["ForegroundTiles"].resolve()), "background": str(files["BackgroundTiles"].resolve()), "animations": animations})
    request = {"textureDimensions": dimensions, "missingTextures": ["tilesets/subfolder/betterTemplate"], "terrainCases": cases}
    (work / "request.private.json").write_text(json.dumps(request) + "\n")
    (work / "Conformance.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable><UseSharedCompilation>false</UseSharedCompilation><BuildInParallel>false</BuildInParallel></PropertyGroup></Project>\n')
    env = dict(os.environ, MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_USE_MSBUILD_SERVER="0")
    with (work / "commands.private.log").open("w") as log:
        subprocess.run(["dotnet", "build", str(work / "Conformance.csproj"), "-c", "Release", "-m:1", "-p:BuildInParallel=false", "-p:UseSharedCompilation=false", "--nologo"], cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
        subprocess.run(["dotnet", str(work / "bin/Release/net10.0/Conformance.dll"), str(work / "request.private.json"), str(work / "result.json")], cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
    report = json.loads((work / "result.json").read_text())
    report.update({"sharedClosureSha256": manifest["sharedClosureSha256"], "mapSha256": MAPS,
        "canonicalAtlasSha256": sha(metadata), "resolvedXmlSha256": xml_evidence,
        "sourceEvidence": {key: sha(work / key) for key in ("Pinned.cs", "Apple.cs", "ApplePartial.cs", "MaskRules.cs", "Tileset.cs", "LoaderTerrain.cs", "Program.cs", "Stubs.cs")},
        "hostBoundaries": "Exact atlas geometry without GPU pixels; XML helpers and AnimatedTiles.Set initial-frame RNG adapter. No render/physical PASS."})
    (work / "source-bound-result.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    print("PASS: complete three-map terrain, original room ordering and seeded RNG")


if __name__ == "__main__":
    main()
