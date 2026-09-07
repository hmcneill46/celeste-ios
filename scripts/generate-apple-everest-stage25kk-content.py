#!/usr/bin/env python3
"""Regenerate the bounded two-map file plan directly from the pinned public ZIPs."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
KI = "c28c38901fef2caafc18f2d35e17996a82e93966"
MAPS = (
    "Maps/StrawberryJam2021/0-Lobbies/1-Beginner.bin",
    "Maps/StrawberryJam2021/1-Beginner/Bing_Over_Google.bin",
)
PACKAGES = sorted((
    "StrawberryJam2021", "StrawberryJam2021Assets", "StrawberryJam2021AudioA",
    "StrawberryJam2021AudioB", "BrokemiaHelper", "CherryHelper", "FemtoHelper",
    "FlaglinesAndSuch", "FrostHelper", "HonlyHelper", "PandorasBox", "VivHelper",
    "JungleHelper", "YetAnotherHelper",
))


def digest_stream(stream):
    digest = hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
        digest.update(chunk)
    return digest.hexdigest()


def select(name, archive, inventory):
    if name == "StrawberryJam2021":
        chosen = {
            *MAPS, "Maps/StrawberryJam2021/0-Lobbies/1-Beginner.meta.yaml",
            "Dialog/English.txt", "Graphics/StrawberryJam2021/CustomEntitySprites.xml",
            *["Graphics/SJ2021xmls/BeginnerLobby/" + file for file in
              ("AnimatedTiles.xml", "ForegroundTiles.xml", "BackgroundTiles.xml", "Sprites.xml")],
            "Graphics/Atlases/Gui/areas/StrawberryJam2021/0-Lobbies/1-Beginner_hover.png",
            "Graphics/Atlases/Gui/areas/SJ2021/lobby/1-Beginner.png",
            "Graphics/Atlases/Gui/areas/SJ2021/lobby/1-Beginner_back.png",
            "Graphics/Atlases/Gui/areas/SJ2021/gym/1-Beginner.png",
            "Audio/sj21_jamjars.bank",
        }
        folded = {}
        for path in inventory:
            if path.casefold() in folded:
                raise ValueError("ambiguous archive path: " + path)
            folded[path.casefold()] = path
        for map_path in MAPS:
            cache = archive.read(map_path[:-4] + ".texturecache.txt").decode("utf-8-sig")
            for line in cache.splitlines():
                reference = line.strip()
                if not reference:
                    continue
                matches = {folded[candidate.casefold()] for candidate in
                           (reference, reference + ".png") if candidate.casefold() in folded}
                if len(matches) != 1:
                    raise ValueError("ambiguous or missing texture-cache entry: " + reference)
                chosen.update(matches)
        chosen.update(path for path in inventory if path.startswith(
            "Graphics/Atlases/Gameplay/objects/StrawberryJam2021/jamJar/beginner/")
            and path.endswith(".png"))
        return chosen
    if name == "StrawberryJam2021Assets":
        prefixes = ("Graphics/Atlases/Stickers/SJ2021/1-Beginner/",
                    "Graphics/Atlases/Mountain/StrawberryJam2021/0-Lobbies/1-Beginner/")
        return {path for path in inventory if any(path.startswith(prefix) and
                "/" not in path[len(prefix):] and path.endswith(".png") for prefix in prefixes)}
    return {
        "StrawberryJam2021AudioA": {"Audio/sj21_bingovergoogle.bank", "Audio/sj21_shared.bank"},
        "StrawberryJam2021AudioB": {"Audio/sj21_BegLobby.bank"},
        "JungleHelper": {"Graphics/Atlases/Gameplay/JungleHelper/Moss/" + part + ".png"
                         for part in ("moss_top", "moss_mid1", "moss_mid2", "moss_mid3", "moss_bottom")},
        "YetAnotherHelper": {"Graphics/Atlases/Gameplay/particles/YetAnotherHelper/bubble_a.png",
                             "Graphics/Atlases/Gameplay/particles/YetAnotherHelper/bubble_b.png"},
    }.get(name, set())


def generate(packages_root):
    graph = json.loads((ROOT / "apple-everest/strawberry-jam-dependency-graph-stage25kc.json").read_text())
    pins = {node["name"]: node for node in graph["nodes"]}
    records = []
    excluded = None
    for name in PACKAGES:
        pin = pins[name]
        path = packages_root / (name + ".zip")
        with path.open("rb") as stream:
            archive_sha = digest_stream(stream)
        if archive_sha != pin["zipSha256"] or path.stat().st_size != pin["zipBytes"]:
            raise ValueError("pinned public archive mismatch: " + name)
        with zipfile.ZipFile(path) as archive:
            entries = sorted((entry for entry in archive.infolist() if not entry.is_dir()),
                             key=lambda entry: entry.filename)
            inventory = {}
            manifest = bytearray()
            for entry in entries:
                relative = entry.filename
                if relative in inventory or relative.startswith("/") or ".." in relative.split("/"):
                    raise ValueError("invalid archive path: " + relative)
                with archive.open(entry) as stream:
                    entry_sha = digest_stream(stream)
                inventory[relative] = entry_sha
                manifest.extend(f"{relative}\0{entry.file_size}\0{entry_sha}\n".encode())
            selected = select(name, archive, inventory)
            included = [{"path": relative, "sha256": inventory[relative]} for relative in sorted(selected)]
            if name == "StrawberryJam2021":
                maps = {path for path in inventory if path.startswith("Maps/") and path.endswith(".bin")}
                if {path for path in selected if path.endswith(".bin")} != set(MAPS):
                    raise ValueError("unexpected SJ map inclusion")
                excluded = sorted(maps - set(MAPS))
            records.append({"name": name, "version": pin["resolvedVersion"],
                            "sourceLogicalSha256": hashlib.sha256(manifest).hexdigest(),
                            "archiveSha256": archive_sha, "includedFiles": included})
    count = sum(len(row["includedFiles"]) for row in records)
    if count != 1212 or len(excluded) != 126:
        raise ValueError("selected/excluded census changed; classify before product generation")
    return {"schemaVersion": 1, "id": "stage25kk-sj-beginner-slice-v1", "packages": records}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--compare-ki", action="store_true")
    args = parser.parse_args()
    value = generate(args.packages_root)
    if args.compare_ki:
        historical = json.loads(subprocess.check_output(
            ["git", "-C", str(ROOT), "show", KI + ":apple-everest/sj-beginner-content-stage25ki.json"], text=True))
        if value["packages"] != historical["packages"]:
            raise ValueError("regenerated plan differs from immutable K-I evidence")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n")
    print("PASS: independently regenerated 14-package / 1,212-file plan; exactly 2 SJ map BINs, 126 excluded")
    if args.compare_ki:
        print("PASS: all selected paths, archive/logical identities and file hashes match immutable K-I")


if __name__ == "__main__":
    main()
