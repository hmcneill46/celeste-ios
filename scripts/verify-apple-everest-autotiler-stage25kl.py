#!/usr/bin/env python3
"""Compile actual Apple terrain against mechanically extracted pinned Everest methods."""
from __future__ import annotations
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PINNED_SHA = "cb331f7b766976c306675d8928e46632d1b6a6b17df5d0ab8eded515ab4fa748"
CANONICAL_SHA = "b0db319cab002cecbd8155c19b4462af0f1c6a75df38e6348a25a09e8d450a0b"


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def method(source, signature):
    start = source.index(signature)
    begin = source.index("{", start)
    depth, pos, state = 1, begin + 1, None
    while depth:
        c, pair = source[pos], source[pos:pos+2]
        if state == "line":
            if c == "\n": state = None
        elif state == "block":
            if pair == "*/": state = None; pos += 1
        elif state in ('"', "'"):
            if c == "\\": pos += 1
            elif c == state: state = None
        elif pair == "//": state = "line"; pos += 1
        elif pair == "/*": state = "block"; pos += 1
        elif c in ('"', "'"): state = c
        elif c == "{": depth += 1
        elif c == "}": depth -= 1
        pos += 1
    return source[start:pos]


def replace_once(text, old, new):
    if text.count(old) != 1: raise ValueError("source target differs: " + old)
    return text.replace(old, new)


def reference(canonical, pinned):
    result = canonical.replace("namespace Celeste;", "using Celeste;\nusing Celeste.Mod;\nnamespace Pinned;")
    result = result.replace("public class Autotiler", "public partial class ReferenceAutotiler").replace("public Autotiler(", "public ReferenceAutotiler(")
    result = result.replace("private void ReadInto(", "private void orig_ReadInto(").replace("private Tiles TileHandler(", "private Tiles orig_TileHandler(")
    result = result.replace("public Generated GenerateOverlay(", "public Generated orig_GenerateOverlay(")
    fields = '''public int ScanWidth, ScanHeight;
        public string Debris, DebrisImpactSfx;
        public List<Tiles> CustomFills;
        public HashSet<char> IgnoreExceptions=new();
        public Dictionary<byte,string> whitelists=new(), blacklists=new();'''
    result = replace_once(result, "public char ID;", "public char ID;\n" + fields)
    original_ignore = method(result, "public bool Ignore(char c)")
    result = replace_once(result, original_ignore, method(pinned, "public bool Ignore(char c)"))
    signatures = ["private void ReadInto(patch_TerrainType", "private void ReadIntoCustomTemplate(",
                  "private byte GetByteLookup(", "private patch_Tiles TileHandler(", "private bool TryGetTile(",
                  "private int GetDepth(", "private bool CheckCross(", "public new Generated GenerateOverlay("]
    methods = []
    for signature in signatures:
        body = method(pinned, signature).replace("patch_", "").replace("public new Generated", "public Generated")
        methods.append(body)
    return result[:result.rfind("}")] + "\n" + "\n".join(methods) + "\n}\n"


BRIDGE = r'''
using System.Linq;
using System.Text.Json;
using Conformance;
using Monocle;
using Microsoft.Xna.Framework;
namespace NAMESPACE;
public partial class CLASS
{
    private static Rule Describe(Tiles tiles,string mask) => tiles==null ? null :
        new(mask,tiles.Textures.Select(t=>t.Key).ToArray(),tiles.OverlapSprites.ToArray(),tiles.HasOverlays);
    public Rule Probe(VirtualMap<char> map,int x,int y,Rectangle fill,char id,bool extend,bool edges,bool padding)
    {
        var behavior=new Behaviour { EdgesExtend=extend,EdgesIgnoreOutOfLevel=edges,PaddingIgnoreOutOfLevel=padding };
        var tiles=TileHandler(map,x,y,fill,id,behavior);
        char tile=GetTile(map,x,y,fill,id,behavior);
        if (tiles==null) return null;
        var terrain=lookup[tile];
        return Describe(tiles,tiles==terrain.Center ? "center" : tiles==terrain.Padded ? "padding" :
            string.Concat(terrain.Masked.First(m=>m.Tiles==tiles).Mask.Select(v=>v==2 ? 'x' : (char)('0'+v))));
    }
    public Definition[] Snapshot() => lookup.Values.OrderBy(t=>t.ID).Select(t=>new Definition(t.ID,t.ScanWidth,t.ScanHeight,
        string.Concat(t.Ignores.Order()),t.Debris,t.Masked.Select(m=>Describe(m.Tiles,string.Concat(m.Mask.Select(v=>v==2?'x':(char)('0'+v))))).ToArray(),
        Describe(t.Center,"center"),Describe(t.Padded,"padding"))).ToArray();
    public static string View(Generated generated) => JsonSerializer.Serialize(new {
        tiles=Enumerable.Range(0,generated.TileGrid.Tiles.Columns).SelectMany(x=>Enumerable.Range(0,generated.TileGrid.Tiles.Rows)
            .Select(y=>x+","+y+":"+generated.TileGrid.Tiles[x,y]?.Key)).ToArray(), overlays=generated.SpriteOverlay.Trace });
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runtime", required=True, type=Path)
    parser.add_argument("--closure", required=True, type=Path)
    parser.add_argument("--map-evidence", required=True, type=Path)
    parser.add_argument("--work-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    canonical_path = ROOT / ".build/celeste-ios/current/managed/Celeste/Autotiler.cs"
    pinned_path = ROOT / ".build/apple-everest/upstream/Everest/Celeste.Mod.mm/Patches/Autotiler.cs"
    if sha(canonical_path) != CANONICAL_SHA or sha(pinned_path) != PINNED_SHA:
        raise ValueError("autotiler source pin mismatch")
    production_path = args.runtime / "Celeste/Autotiler.cs"
    partial_path = args.runtime / "Celeste/Mod/AppleEverestStatic/AppleEverestAutotiler.cs"
    mask_rules_path=args.runtime/"Celeste/Mod/AppleEverestStatic/AppleEverestTileMaskRules.cs"
    if sha(mask_rules_path)!=sha(ROOT/"apple-everest/runtime/AppleEverestTileMaskRules.cs"):
        raise ValueError("actual production mask validator differs; regenerate closure")
    if sha(partial_path) != sha(ROOT / "apple-everest/runtime/semantics/AppleEverestAutotiler.cs"):
        raise ValueError("production partial differs from current source; regenerate closure")
    work = args.work_root.resolve()
    if work.exists(): raise ValueError("conformance work root already exists")
    work.mkdir(parents=True)
    (work / ".stage25kl-conformance").touch()
    (work / "Pinned.cs").write_text(reference(canonical_path.read_text(), pinned_path.read_text()))
    loader_path=ROOT/".build/celeste-ios/current/managed/Celeste/LevelLoader.cs"
    loader=loader_path.read_text()
    start=loader.index("Rectangle tileBounds = mapData.TileBounds;")
    end=loader.index("Vector2 position = new Vector2(tileBounds.X, tileBounds.Y) * 8f;",start)
    preparation=loader[start:end].replace("GFX.FGAutotiler.LevelBounds","bounds").replace("LevelData", "TerrainLevel")
    (work/"LoaderTerrain.cs").write_text('''using Monocle;
using Microsoft.Xna.Framework;
using System.Text.RegularExpressions;
namespace Conformance;
internal sealed class TerrainLevel { public Rectangle TileBounds; public string Bg,Solids; }
internal sealed class TerrainMap { public Rectangle TileBounds; public TerrainLevel[] Levels; public Rectangle[] Filler; }
internal static class LoaderTerrain {
    internal static (VirtualMap<char> Background,VirtualMap<char> Foreground,List<Rectangle> Bounds) Prepare(TerrainMap mapData) {
        List<Rectangle> bounds=new();
''' + preparation + "return (virtualMap,virtualMap2,bounds);\n}\n}\n")
    for source, target in [(production_path,"Apple.cs"), (partial_path,"ApplePartial.cs"),
            (mask_rules_path,"MaskRules.cs"),
            (ROOT / ".build/celeste-ios/current/managed/Monocle/VirtualMap.cs","VirtualMap.cs"),
            (ROOT / "tools/AppleEverestBuilder/tests/AutotilerConformanceStubs.cs.txt","Stubs.cs"),
            (ROOT / "tools/AppleEverestBuilder/tests/AutotilerConformanceProgram.cs.txt","Program.cs")]:
        shutil.copyfile(source,work/target)
    tileset_path=args.runtime/"Monocle/Tileset.cs"
    canonical_tileset=(ROOT/".build/celeste-ios/current/managed/Monocle/Tileset.cs").read_text()
    pinned_tileset_path=ROOT/".build/apple-everest/upstream/Everest/Celeste.Mod.mm/Patches/Monocle/Tileset.cs"
    pinned_tileset=pinned_tileset_path.read_text()
    for expression in ["tiles[x % tiles.GetLength(0), y % tiles.GetLength(1)]",
                       "tiles[index % tiles.GetLength(0), (index / tiles.GetLength(0)) % tiles.GetLength(1)]"]:
        if expression not in pinned_tileset:raise ValueError("pinned tileset indexer contract differs")
    expected_tileset=canonical_tileset.replace("tiles[x, y]","tiles[x % tiles.GetLength(0), y % tiles.GetLength(1)]").replace(
        "tiles[index % tiles.GetLength(0), index / tiles.GetLength(0)]","tiles[index % tiles.GetLength(0), (index / tiles.GetLength(0)) % tiles.GetLength(1)]")
    if tileset_path.read_text()!=expected_tileset:raise ValueError("actual applied Tileset differs from exact pinned indexers")
    shutil.copyfile(tileset_path,work/"Tileset.cs")
    for namespace, name in [("Celeste","Autotiler"),("Pinned","ReferenceAutotiler")]:
        (work / (name + "Bridge.cs")).write_text(BRIDGE.replace("NAMESPACE",namespace).replace("CLASS",name))
    (work / "Conformance.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable><Deterministic>true</Deterministic></PropertyGroup></Project>\n')
    manifest = json.loads((args.closure / "compatibility-manifest.json").read_text())
    mounts = {m["sourcePath"]: args.closure / "content/Content" / m["logicalPath"] for m in manifest["contentMounts"]}
    graphics = "Graphics/SJ2021xmls/BeginnerLobby/"
    content = ROOT / ".build/celeste-ios/current/content/Content"
    spec=importlib.util.spec_from_file_location("atlas",ROOT/"scripts/inventory-celeste-controller-prompts.py")
    atlas=importlib.util.module_from_spec(spec);spec.loader.exec_module(atlas)
    keys = {p.lower() for p in atlas.parse_paths((content / "Graphics/Atlases/Gameplay.meta").read_bytes())}
    for path in mounts:
        prefix="Graphics/Atlases/Gameplay/"
        if path.startswith(prefix) and path.endswith(".png"): keys.add(path[len(prefix):-4].lower())
    frame_counts={}
    for row in ET.parse(mounts[graphics + "AnimatedTiles.xml"]).getroot():
        key=row.attrib["path"].lower();count=0
        while (count==0 and key in keys) or any(key+str(count).zfill(w) in keys for w in range(len(str(count)),len(str(count))+6)):
            count+=1
        if count == 0: raise ValueError("missing animation frames: "+key)
        frame_counts[row.attrib["name"]]=count
    trees=json.loads(args.map_evidence.read_text())
    lobby=trees["Maps/StrawberryJam2021/0-Lobbies/1-Beginner.bin"]
    def find(n,name):
        if n["name"]==name: yield n
        for c in n["children"]: yield from find(c,name)
    level=next(find(lobby["tree"],"level"))
    request={"foreground":str(mounts[graphics+"ForegroundTiles.xml"].resolve()),
             "background":str(mounts[graphics+"BackgroundTiles.xml"].resolve()),
             "vanillaForeground":str(content/"Graphics/ForegroundTiles.xml"),
             "vanillaBackground":str(content/"Graphics/BackgroundTiles.xml"),
             "width":int(level["attributes"]["width"])/8,"height":int(level["attributes"]["height"])/8,
             "solids":next(find(level,"solids"))["attributes"]["innerText"],
             "bg":next(find(level,"bg"))["attributes"]["innerText"],"animations":frame_counts,
             "seed":sum(map(ord,"StrawberryJam2021/0-Lobbies/1-Beginner"))}
    binding=next(row for row in json.loads((args.closure/"map-bindings.json").read_text()) if row["Sid"]=="StrawberryJam2021/0-Lobbies/1-Beginner")
    if binding["TerrainSeed"] != request["seed"] or request["seed"]!=3349:
        raise ValueError("production terrain seed differs from pinned raw-SID AreaData.Name")
    request["width"]=int(request["width"]);request["height"]=int(request["height"])
    request["textureDimensions"]={path[len("Graphics/Atlases/Gameplay/"):-4]:struct.unpack(">II",file.read_bytes()[16:24]) for path,file in mounts.items()
        if path.startswith("Graphics/Atlases/Gameplay/") and path.endswith(".png")}
    request["missingTextures"]=["tilesets/subfolder/betterTemplate"]
    fillers=list(find(lobby["tree"],"Filler"))
    if any(n["children"] for n in fillers):raise ValueError("selected single-room reference needs authored filler input")
    request["roomX"]=int(level["attributes"]["x"])
    request["roomY"]=int(level["attributes"]["y"])
    (work/"request.json").write_text(json.dumps(request))
    args.output.parent.mkdir(parents=True,exist_ok=True)
    with (work/"run.log").open("w") as log:
        result=subprocess.run(["dotnet","run","--project",str(work/"Conformance.csproj"),"-c","Release","--",
                               str(work/"request.json"),str(args.output.resolve())],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT)
    if result.returncode:
        print((work/"run.log").read_text()[-12000:]);raise SystemExit(result.returncode)
    report=json.loads(args.output.read_text())
    report.update({"canonicalAutotilerSha256":CANONICAL_SHA,"pinnedEverestAutotilerSha256":PINNED_SHA,
                   "actualAppliedAutotilerSha256":sha(production_path),"actualProductionPartialSha256":sha(partial_path),
                   "actualProductionMaskRulesSha256":sha(mask_rules_path),
                   "actualAppliedTilesetSha256":sha(tileset_path),"pinnedTilesetSha256":sha(pinned_tileset_path),
                   "sharedClosureSha256":manifest["sharedClosureSha256"],
                   "canonicalTerrainPreparationSha256":sha(loader_path),
                   "referenceConstruction":"MECHANICAL_PINNED_METHODS_WITH_CANONICAL_ORIGINALS_AND_ACTUAL_VIRTUAL_MAP",
                   "hostBoundaries":"XML helpers and coordinate textures; AnimatedTiles.Set preserves exact initial-frame RNG consumption"})
    args.output.write_text(json.dumps(report,indent=2,ensure_ascii=False)+"\n")
    print((work/"run.log").read_text().strip())


if __name__=="__main__":main()
