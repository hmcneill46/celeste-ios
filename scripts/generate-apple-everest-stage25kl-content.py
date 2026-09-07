#!/usr/bin/env python3
"""Enumerate the unchanged selected maps' resources from exact public archives."""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MAPS = ("Maps/StrawberryJam2021/0-Lobbies/1-Beginner.bin",
        "Maps/StrawberryJam2021/1-Beginner/Bing_Over_Google.bin")
GRAPH = ROOT / "apple-everest/strawberry-jam-dependency-graph-stage25kc.json"
FALLBACK_PATH = "Graphics/Atlases/Gameplay/__fallback.png"
FALLBACK_SHA = "f28617c37d0760b999caed3cde18935841017c29d7dab622fb668c224fca2ca8"
TITLE_PATH = "Graphics/Atlases/Gui/areaselect/title.png"
TITLE_SHA = "0839135f2baafbf652d652177e69456c07bc85343a6c93491e4e2a906034518d"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def file_sha(path):
    with path.open("rb") as stream:
        return stream_sha(stream)


def stream_sha(stream):
    digest = hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
        digest.update(chunk)
    return digest.hexdigest()


def read_map(data):
    # Reuse the accepted bounded binary primitives; retain every node rather
    # than just the earlier stage's custom-ID projection.
    spec = importlib.util.spec_from_file_location("stage25kc_reader", ROOT / "scripts/generate-apple-everest-stage25kc.py")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    reader = module.Reader(data)
    if reader.string() != "CELESTE MAP":
        raise ValueError("invalid map header")
    label = reader.string()
    count = reader.i16()
    if not 0 < count <= 8192:
        raise ValueError("invalid string table")
    table = [reader.string() for _ in range(count)]
    visited = 0

    def element(depth=0):
        nonlocal visited
        visited += 1
        if depth > 128 or visited > 100000:
            raise ValueError("map tree exceeds bound")
        name = table[reader.i16()]
        attributes = {}
        for _ in range(reader.u8()):
            key, kind = table[reader.i16()], reader.u8()
            if kind == 0: value = bool(reader.u8())
            elif kind == 1: value = reader.u8()
            elif kind == 2: value = reader.i16()
            elif kind == 3: value = reader.i32()
            elif kind == 4: value = reader.f32()
            elif kind == 5: value = table[reader.i16()]
            elif kind == 6: value = reader.string()
            elif kind == 7:
                encoded = reader.take(reader.i16())
                if len(encoded) % 2: raise ValueError("invalid RLE")
                value = "".join(chr(encoded[i+1]) * encoded[i] for i in range(0, len(encoded), 2))
            else: raise ValueError("unknown map value")
            if key in attributes: raise ValueError("duplicate map attribute")
            attributes[key] = value
        children = reader.i16()
        if children < 0: raise ValueError("invalid child count")
        return {"name": name, "attributes": attributes, "children": [element(depth+1) for _ in range(children)]}
    tree = element()
    if len(data) - reader.pos > 2 * 1024 * 1024: raise ValueError("appendix exceeds bound")
    return {"label": label, "rootBytes": reader.pos, "appendixSha256": sha(data[reader.pos:]), "tree": tree}


def walk(node, parent=None):
    yield parent, node
    for child in node["children"]:
        yield from walk(child, node["name"])


def generate(package_root):
    pins = {row["name"]: row for row in json.loads(GRAPH.read_text())["nodes"]}
    archives, inventories, logical, chosen, reasons = {}, {}, {}, {}, {}
    for path in sorted(package_root.glob("*.zip")):
        name = path.stem
        if name not in pins: raise ValueError("unpinned archive: " + name)
        pin = pins[name]
        if file_sha(path) != pin["zipSha256"] or path.stat().st_size != pin["zipBytes"]:
            raise ValueError("public archive mismatch: " + name)
        archive = archives[name] = zipfile.ZipFile(path)
        inventory, manifest = {}, bytearray()
        for entry in sorted(archive.infolist(), key=lambda e: e.filename):
            if entry.is_dir(): continue
            relative = entry.filename
            if relative in inventory or relative.startswith("/") or ".." in relative.split("/"):
                raise ValueError("invalid archive path")
            with archive.open(entry) as stream: digest = stream_sha(stream)
            inventory[relative] = digest
            manifest.extend(f"{relative}\0{entry.file_size}\0{digest}\n".encode())
        if len({p.casefold() for p in inventory}) != len(inventory):
            raise ValueError("case-ambiguous archive: " + name)
        inventories[name], logical[name], chosen[name] = inventory, sha(manifest), set()
    index = {}
    for name, inventory in inventories.items():
        for path in inventory:
            if path.startswith("Graphics/"):
                index.setdefault(path.casefold(), []).append((name, path))

    def select(name, path, reason):
        if path not in inventories[name]: raise ValueError("missing source: " + name + "/" + path)
        chosen[name].add(path)
        reasons.setdefault(name + "/" + path, set()).add(reason)

    def resource(path, reason, required=True):
        rows = index.get(path.casefold(), [])
        if not rows:
            if required: raise ValueError("missing graphics: " + path)
            return False
        if len(rows) != 1: raise ValueError("ambiguous graphics providers: " + path)
        select(*rows[0], reason)
        return True

    canonical = set()
    def frames(key, reason):
        path = "Graphics/Atlases/Gameplay/" + key
        found = 0
        while True:
            candidates = ([path + ".png"] if found == 0 else []) + [path + str(found).zfill(width) + ".png" for width in range(len(str(found)), len(str(found)) + 6)]
            match = next((p for p in candidates if p.casefold() in index), None)
            if match is None: break
            resource(match, reason)
            found += 1
        if found == 0:
            if "sj2021" in key.lower() or "helper" in key.lower() or "strawberryjam" in key.lower():
                raise ValueError("missing mod animation: " + key)
            canonical.add(key)
        return found

    root = "StrawberryJam2021"
    definitions = "Graphics/SJ2021xmls/BeginnerLobby/"
    for path in [*MAPS, MAPS[0][:-4] + ".meta.yaml", "Dialog/English.txt",
                 "Graphics/StrawberryJam2021/CustomEntitySprites.xml", "Audio/sj21_jamjars.bank",
                 *[definitions + name for name in ("ForegroundTiles.xml", "BackgroundTiles.xml", "AnimatedTiles.xml", "Sprites.xml")]]:
        select(root, path, "selected map/root metadata")
    parsed = {}
    for map_path in MAPS:
        parsed[map_path] = read_map(archives[root].read(map_path))
        for line in archives[root].read(map_path[:-4] + ".texturecache.txt").decode("utf-8-sig").splitlines():
            if not line.strip(): continue
            key = line.strip()
            resource(key if key.endswith(".png") else key + ".png", "original map texture cache")
        for parent, node in walk(parsed[map_path]["tree"]):
            attributes = node["attributes"]
            for attribute, value in attributes.items():
                if attribute.lower() in ("colorgrade", "colorgradea", "colorgradeb", "colorgradefrom", "colorgradeto") and isinstance(value, str) and value not in ("", "none", "(current)"):
                    resource("Graphics/ColorGrading/" + value + ".png", "authored color-grade consumer", required="/" in value)
            if parent in ("fgdecals", "bgdecals"):
                texture = attributes["texture"].replace("\\", "/")
                texture = texture.replace(Path(texture).suffix, "") if Path(texture).suffix else texture
                frames("decals/" + re.sub(r"\d+$", "", texture), "authored decal")
            if node["name"].lower() == "parallax":
                texture = attributes.get("texture", "")
                if texture.startswith("bgs/MaxHelpingHand/animatedParallax/"):
                    frames(re.sub(r"\d+$", "", texture), "authored animated parallax")
            if node["name"] == "meta":
                if attributes.get("Icon"): resource("Graphics/Atlases/Gui/" + attributes["Icon"] + ".png", "map icon")
                if attributes.get("ColorGrade") not in (None, "", "none"):
                    resource("Graphics/ColorGrading/" + attributes["ColorGrade"] + ".png", "map color grade")
    for filename, prefix in [("ForegroundTiles.xml", "tilesets/"), ("BackgroundTiles.xml", "tilesets/")]:
        for definition in ET.fromstring(archives[root].read(definitions + filename)):
            key = prefix + definition.attrib["path"]
            if not resource("Graphics/Atlases/Gameplay/" + key + ".png", "full terrain constructor", required=False):
                canonical.add(key)
            if "debris" in definition.attrib:
                frames("debris/" + definition.attrib["debris"], "custom terrain debris")
    for animation in ET.fromstring(archives[root].read(definitions + "AnimatedTiles.xml")):
        frames(animation.attrib["path"], "full animated tile bank")
    for path in inventories[root]:
        if path.startswith("Graphics/Atlases/Gameplay/objects/StrawberryJam2021/jamJar/beginner/") and path.endswith(".png"):
            select(root, path, "selected jar sprite bank")
    for path in ["Graphics/Atlases/Gui/areas/StrawberryJam2021/0-Lobbies/1-Beginner_hover.png",
                 *["Graphics/Atlases/Gui/areas/SJ2021/" + p + ".png" for p in ("lobby/1-Beginner", "lobby/1-Beginner_back", "gym/1-Beginner", "meters/2-med")]]:
        select(root, path, "selected chapter presentation")
    for path in inventories["StrawberryJam2021Assets"]:
        for prefix in ("Graphics/Atlases/Stickers/SJ2021/1-Beginner/", "Graphics/Atlases/Mountain/StrawberryJam2021/0-Lobbies/1-Beginner/"):
            if path.startswith(prefix) and "/" not in path[len(prefix):] and path.endswith(".png"):
                select("StrawberryJam2021Assets", path, "selected lobby presentation")
    for name, paths in {"StrawberryJam2021AudioA": ["sj21_bingovergoogle", "sj21_shared"], "StrawberryJam2021AudioB": ["sj21_BegLobby"]}.items():
        for path in paths: select(name, "Audio/" + path + ".bank", "registered selected event bank")
    records = []
    for name in sorted(chosen):
        if not chosen[name]: continue
        records.append({"name": name, "version": pins[name]["resolvedVersion"], "sourceLogicalSha256": logical[name],
                        "archiveSha256": pins[name]["zipSha256"], "includedFiles": [
                            {"path": p, "sha256": inventories[name][p], **({"preserveSourceBytes": True} if p in MAPS or p.startswith(definitions) else {})}
                            for p in sorted(chosen[name])]})
    excluded = sorted(p for p in inventories[root] if p.startswith("Maps/") and p.endswith(".bin") and p not in MAPS)
    if len(excluded) != 126 or sorted(p for p in chosen[root] if p.endswith(".bin")) != sorted(MAPS):
        raise ValueError("selected map closure widened")
    core = ROOT / ".build/apple-everest/upstream/Everest/Celeste.Mod.mm/Content"
    fallback = core / FALLBACK_PATH
    if file_sha(fallback) != FALLBACK_SHA: raise ValueError("pinned Everest fallback asset mismatch")
    title = core / TITLE_PATH
    if file_sha(title) != TITLE_SHA: raise ValueError("pinned Everest chapter title asset mismatch")
    # The original XML contains one unused template path that is absent from
    # every pinned provider. Everest constructs it with its own missing-image
    # sheet. Keep that behavior and the source bytes, not a renamed mod sheet.
    original_missing = "tilesets/subfolder/betterTemplate"
    if original_missing not in canonical: raise ValueError("original missing template profile changed")
    canonical.remove(original_missing)
    result = {"schemaVersion": 1, "id": "stage25kl-sj-beginner-slice-v1", "selectionMode": "UNION_WITH_ACCEPTED_PROVIDER_REGRESSION_CONTENT", "packages": records,
              "everestContent": [{"path": FALLBACK_PATH, "sha256": FALLBACK_SHA}, {"path": TITLE_PATH, "sha256": TITLE_SHA}],
              "originalMissingTerrainTextures": [{"key": original_missing, "tileId": "y", "sources": [definitions + f for f in ("ForegroundTiles.xml", "BackgroundTiles.xml")],
                                                  "resolution": "PINNED_EVEREST_ATLAS_FALLBACK_AND_TILESET_MODULO", "usedCellCount": 0}],
              "excludedMapCount": len(excluded), "excludedMaps": excluded, "canonicalTextureReferences": sorted(canonical),
              "resourceReasons": {k: sorted(v) for k, v in sorted(reasons.items())}}
    for archive in archives.values(): archive.close()
    return result, parsed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--map-evidence", type=Path)
    args = parser.parse_args()
    result, maps = generate(args.packages_root)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n")
    if args.map_evidence:
        args.map_evidence.parent.mkdir(parents=True, exist_ok=True)
        args.map_evidence.write_text(json.dumps(maps, ensure_ascii=False) + "\n")
    print(f"PASS: {sum(len(p['includedFiles']) for p in result['packages'])} original package files + {len(result['everestContent'])} pinned core asset; exactly two SJ maps, 126 excluded")


if __name__ == "__main__":
    main()
