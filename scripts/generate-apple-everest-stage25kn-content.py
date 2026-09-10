#!/usr/bin/env python3
"""Extend the unchanged K-L resource selection by the exact snas composition.

The historical generator must still reproduce its original two-map authority.
This separate producer resolves all added references from exact pinned packages;
canonical references remain explicit obligations for the composition verifier.
"""
from __future__ import annotations
import argparse
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SID = "StrawberryJam2021/1-Beginner/snas"
MAP = "Maps/" + SID + ".bin"
MAP_SHA = "6ad3172d496e8b5b4ce71f1128fe231b162d534419dc823d2af2cea27fc241d9"
BANK = "Audio/sj21_snas.bank"
BANK_SHA = "08c980621201485026849c90e41b275c442448527d3c98096adeb73d562a289a"
GUIDS = "Audio/sj21_snas.guids.txt"
GUIDS_SHA = "4b8dfb05ba4d790b6acf0a86904cd0e28b7fefd3e9cdcd8a53050ac29cd74039"


def generator():
    spec = importlib.util.spec_from_file_location("kn_baseline_content", ROOT / "scripts/generate-apple-everest-stage25kl-content.py")
    value = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = value
    spec.loader.exec_module(value)
    return value


def generate(packages):
    prior = generator()
    # K-L's unchanged generator accepts only its historical source catalog.
    # K-N also supplies these two exact runtime inputs. Validate them, then
    # present the old producer with an owned, read-only view of its own set.
    runtime_inputs = {
        "CommunalHelper": "44f4fb0b277a4900fd2a555e1a73e661140aa7b2455d3c7776cf420193349e3c",
        "ChronoHelper": "af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18",
    }
    with tempfile.TemporaryDirectory(prefix="stage25kn-baseline-content-") as temporary:
        for path in sorted(packages.glob("*.zip")):
            if path.stem in runtime_inputs:
                if prior.sha(path.read_bytes()) != runtime_inputs[path.stem]:
                    raise ValueError("additional exact runtime package differs: " + path.stem)
            else:
                os.symlink(path.resolve(), Path(temporary) / path.name)
        result, maps = prior.generate(Path(temporary))
    if result != json.loads((ROOT / "apple-everest/sj-beginner-content-stage25kl.json").read_text()):
        raise ValueError("unchanged K-L content control differs")
    pins = {row["name"]: row for row in json.loads(prior.GRAPH.read_text())["nodes"]}
    archives = {p.stem: zipfile.ZipFile(p) for p in sorted(packages.glob("*.zip"))}
    try:
        entries = {name: {item.filename: item for item in archive.infolist() if not item.is_dir()}
                   for name, archive in archives.items()}
        # The original producer has already checked complete package identities,
        # path safety, duplicate names and case ambiguity above.
        records = {row["name"]: row for row in result["packages"]}
        selected = {name: {row["path"]: row for row in record["includedFiles"]} for name, record in records.items()}
        reasons = {key: set(values) for key, values in result["resourceReasons"].items()}
        canonical = set(result["canonicalTextureReferences"])
        index = {}
        for name, inventory in entries.items():
            for path in inventory:
                if path.startswith("Graphics/"):
                    index.setdefault(path.casefold(), []).append((name, path))

        def add(name, path, reason, preserve=False):
            if name not in records:
                raise ValueError("unreviewed additional content provider: " + name)
            data = archives[name].read(path)
            selected[name][path] = {"path": path, "sha256": prior.sha(data),
                **({"preserveSourceBytes": True} if preserve else {})}
            reasons.setdefault(name + "/" + path, set()).add(reason)
            return data

        def image(path, reason, allow_canonical=False):
            found = index.get(path.casefold(), [])
            if len(found) > 1:
                raise ValueError("ambiguous added graphics provider: " + path)
            if found:
                add(*found[0], reason)
            elif allow_canonical:
                if not path.startswith("Graphics/Atlases/Gameplay/") or not path.endswith(".png"):
                    raise ValueError("unsupported canonical graphics reference: " + path)
                canonical.add(path[len("Graphics/Atlases/Gameplay/"):-4])
            else:
                raise ValueError("missing added resource: " + path)

        def frames(key, reason):
            prefix = "Graphics/Atlases/Gameplay/" + key
            count = 0
            while True:
                candidates = ([prefix + ".png"] if count == 0 else []) + [prefix + str(count).zfill(w) + ".png" for w in range(len(str(count)), len(str(count)) + 6)]
                found = [p for p in candidates if p.casefold() in index]
                if not found:
                    break
                if len(found) != 1:
                    raise ValueError("ambiguous animation frame numbering: " + key)
                image(found[0], reason)
                count += 1
            if count == 0:
                canonical.add(key)

        data = add("StrawberryJam2021", MAP, "unchanged snas map", True)
        if len(data) != 67718 or prior.sha(data) != MAP_SHA:
            raise ValueError("snas BIN identity differs")
        parsed = maps[MAP] = prior.read_map(data)
        if parsed["label"] != "The Squeeze" or len(data)-parsed["rootBytes"] != 14677 or parsed["appendixSha256"] != "f13ab43805a004226d5c7eb82a42a028884be222f988198ce7bcf14708b28a04":
            raise ValueError("snas source label or appendix differs")
        for line in archives["StrawberryJam2021"].read(MAP[:-4] + ".texturecache.txt").decode("utf-8-sig").splitlines():
            if line.strip():
                image(line.strip() if line.strip().endswith(".png") else line.strip()+".png", "snas original texture cache")
        metadata = [node for _, node in prior.walk(parsed["tree"]) if node["name"] == "meta"]
        if len(metadata) != 1:
            raise ValueError("missing/ambiguous original snas metadata")
        meta = metadata[0]["attributes"]
        if meta.get("ForegroundTiles") != "Graphics/SJ2021xmls/snas/ForegroundTiles.xml":
            raise ValueError("snas foreground authority differs")
        xml_bytes = add("StrawberryJam2021", meta["ForegroundTiles"], "complete original snas foreground XML", True)
        for tileset in ET.fromstring(xml_bytes):
            image("Graphics/Atlases/Gameplay/tilesets/" + tileset.attrib["path"] + ".png",
                  "complete snas terrain constructor", True)
            if "debris" in tileset.attrib:
                frames("debris/" + tileset.attrib["debris"], "snas terrain debris")
        for parent, node in prior.walk(parsed["tree"]):
            attrs = node["attributes"]
            if parent in ("fgdecals", "bgdecals"):
                texture = attrs["texture"].replace("\\", "/")
                if texture.endswith(".png"):
                    texture = texture[:-4]
                frames("decals/" + re.sub(r"\d+$", "", texture), "all authored snas decal frames")
            if node["name"].lower() == "parallax":
                image("Graphics/Atlases/Gameplay/" + attrs["texture"] + ".png", "snas authored parallax", True)
        image("Graphics/Atlases/Gui/" + meta["Icon"] + ".png", "snas authored chapter icon")
        # Canonical color grades live outside Gameplay.meta and receive their
        # own exact packaged-content check; do not mistake them for atlas keys.
        result["additionalCanonicalContentReferences"] = ["Graphics/ColorGrading/" + meta["ColorGrade"] + ".png"]
        bank = add("StrawberryJam2021AudioB", BANK, "snas registered original event bank")
        guids = archives["StrawberryJam2021AudioB"].read(GUIDS)
        if len(bank) != 3320960 or prior.sha(bank) != BANK_SHA or len(guids) != 82696 or prior.sha(guids) != GUIDS_SHA:
            raise ValueError("snas bank/GUID companion identity differs")
        result["additionalAudioAuthority"] = {"owner": "StrawberryJam2021AudioB", "version": "1.0.0",
            "bank": BANK, "bankSha256": BANK_SHA, "bankBytes": len(bank), "guids": GUIDS,
            "guidsSha256": GUIDS_SHA, "guidsBytes": len(guids),
            "events": ["event:/sj21_snas", "event:/sj21_snas_flourish", "event:/sj21_snas_special"]}
        if result["excludedMaps"].count(MAP) != 1:
            raise ValueError("baseline excluded destination authority differs")
        result["excludedMaps"].remove(MAP)
        result["excludedMapCount"] = len(result["excludedMaps"])
        if result["excludedMapCount"] != 125:
            raise ValueError("K-N excluded census differs")
        for name, record in records.items():
            record["includedFiles"] = [selected[name][path] for path in sorted(selected[name])]
        result["id"] = "stage25kn-sj-snas-expansion-v1"
        result["canonicalTextureReferences"] = sorted(canonical)
        result["resourceReasons"] = {key: sorted(values) for key, values in sorted(reasons.items())}
        return result, maps
    finally:
        for archive in archives.values():
            archive.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--map-evidence", required=True, type=Path)
    args = parser.parse_args()
    result, maps = generate(args.packages_root)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False)+"\n")
    args.map_evidence.parent.mkdir(parents=True, exist_ok=True)
    args.map_evidence.write_text(json.dumps(maps, ensure_ascii=False)+"\n")
    print("PASS: exact three-map resource selection; 125 other SJ maps excluded")


if __name__ == "__main__":
    main()
