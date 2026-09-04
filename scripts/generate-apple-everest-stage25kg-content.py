#!/usr/bin/env python3
"""Generate the exact, hash-locked Stage 25K-G Strawberry Jam content slice."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


PACKAGES = {
    "StrawberryJam2021": ("1.0.12", "4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655", "d5e68237d8371fa26ed5d804578ce5b1841673ba380d7438fb4fafcd73899063"),
    "StrawberryJam2021Assets": ("1.0.1", "26fab85f20fff89d1adcba2ef926c3447d9996057c7dfc78e5d97f9d96ed7e45", "a50e1da4e2a35c1e61b48ff81ee5600870398bcfd74d69ad03fa577080d73625"),
    "StrawberryJam2021AudioA": ("1.0.4", "81e9cbc39b3a5525c93dfc5b24b675a833a8fb616e8a01cfc0838296f5e37e1d", "d4fc6c79afdb1a986317af52f331a7d1a404eedee79c2f2fa1a32664cc4d7330"),
    "StrawberryJam2021AudioB": ("1.0.0", "70b90f45709956a4d18bfbb5941836c534344a1cf0ab859a50430daa3b76ef42", "cc73019ea246b2742ac0cbfa639e6cda5a566fe05b91631bb70ff71669425751"),
    "BrokemiaHelper": ("1.8.5", "c80d7d71cdccef7c9b418e05d53debd197b59717d3cdeade72910ace33d4fbeb", "8ca14d6791d481178e2383c965f8f810f6cd442a211ddafbb9cc7186db86602c"),
    "CherryHelper": ("1.8.2", "8ca640f9e14f844507e0bbedc2afcb99ef61bec6957a7558cee90e9fabd2fba7", "3a157baabcc7b9a0157fdcb8404e0044f0fbf3926df7b7788b29923149d711dc"),
    "FemtoHelper": ("1.15.22", "5a845805bae30490ed7c9da84410fbe672628b8fe927a830f5f2eb148eb88419", "19ff3a3b968c02082344ea11cb7b6df780a47d9118e1cae882d2e880d67257d2"),
    "FlaglinesAndSuch": ("1.6.80", "fb0fd300f95d77539eb9079aed445c81f263557c1a1715187bd509d39c1eb794", "897ade835828e742c64fefc2f85cc458ab6cbe3ae00c5a509000588b9aeccce2"),
    "FrostHelper": ("1.80.1", "ba6aee8f596eff0f595fd90cb7f136ea13b1e9c2d664fc38f389608b0f9e2c81", "9ff388dd81ac09d033c45d6e93a6c194f4a7714711e1cc5032bb30b5e2b89d69"),
    "HonlyHelper": ("1.7.5", "6a2d0f04a5be3a9c9c3bf66ec7e93701398a64d5a0e72add5df682e860e7d08d", "cb8524306f28c0d04081dd63b5a3ccad4d7fecd5bc71970e06376606439298e3"),
    "PandorasBox": ("1.0.49", "25f9c7d6792983ac697e3780667fb0963233c0c08f6e528f5c6d9fe732cded05", "7113d897271264cd6bd8089e06112c536970fb016138350a4eeaf98576651e04"),
    "VivHelper": ("1.14.10", "9a95f51659ccfcb78404f9610307918d6114fc824298fbb8493651ec7c80215a", "81468fa30222a69bb6c648ef6d8ddcace0d51a60172a1a0930bb823b3d3ff199"),
    "JungleHelper": ("1.4.10", "a140e21cbb5fd2dcaac70d4d5e36d49476e414861455ae25dedc0678164406cc", "911457d12e30d0912eb2420a39dbde7dc892560cd8a512284dccb89d93ff6e49"),
    "YetAnotherHelper": ("1.2.5", "73d64e1b3457e2461d3368a01de9f5f31a58bf24e130f3e35ec7280b24814c2d", "e48a8c6b1941ebef23853fdb65b4db487aa9aa3162935b975b5468675de93da6"),
}


def sha(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def logical_hash(root: Path) -> str:
    lines = bytearray()
    for path in sorted((p for p in root.rglob("*") if p.is_file()), key=lambda p: p.relative_to(root).as_posix()):
        relative = path.relative_to(root).as_posix()
        lines.extend(relative.encode())
        lines.extend(b"\0")
        lines.extend(str(path.stat().st_size).encode())
        lines.extend(b"\0")
        lines.extend(sha(path).encode())
        lines.extend(b"\n")
    return hashlib.sha256(lines).hexdigest()


def root_files(root: Path) -> set[str]:
    chosen = {
        "Maps/StrawberryJam2021/0-Lobbies/1-Beginner.bin",
        "Maps/StrawberryJam2021/0-Lobbies/1-Beginner.meta.yaml",
        "Maps/StrawberryJam2021/1-Beginner/Bing_Over_Google.bin",
        "Dialog/English.txt",
        "Graphics/StrawberryJam2021/CustomEntitySprites.xml",
        "Graphics/SJ2021xmls/BeginnerLobby/AnimatedTiles.xml",
        "Graphics/SJ2021xmls/BeginnerLobby/ForegroundTiles.xml",
        "Graphics/SJ2021xmls/BeginnerLobby/BackgroundTiles.xml",
        "Graphics/SJ2021xmls/BeginnerLobby/Sprites.xml",
        "Graphics/Atlases/Gui/areas/StrawberryJam2021/0-Lobbies/1-Beginner_hover.png",
        "Graphics/Atlases/Gui/areas/SJ2021/lobby/1-Beginner.png",
        "Graphics/Atlases/Gui/areas/SJ2021/lobby/1-Beginner_back.png",
        "Graphics/Atlases/Gui/areas/SJ2021/gym/1-Beginner.png",
        "Audio/sj21_jamjars.bank",
    }
    caches = (
        root / "Maps/StrawberryJam2021/0-Lobbies/1-Beginner.texturecache.txt",
        root / "Maps/StrawberryJam2021/1-Beginner/Bing_Over_Google.texturecache.txt",
    )
    inventory = {p.relative_to(root).as_posix(): p for p in root.rglob("*") if p.is_file()}
    folded = {name.casefold(): name for name in inventory}
    for cache in caches:
        for raw in cache.read_text(encoding="utf-8-sig").splitlines():
            reference = raw.strip()
            if not reference:
                continue
            matches = []
            for candidate in (reference, reference + ".png"):
                if candidate in inventory:
                    matches.append(candidate)
                elif candidate.casefold() in folded:
                    matches.append(folded[candidate.casefold()])
            matches = sorted(set(matches))
            if len(matches) != 1:
                raise SystemExit(f"texture-cache reference resolved {len(matches)} files: {reference}")
            chosen.add(matches[0])
    chosen.update(name for name in inventory
                  if name.startswith("Graphics/Atlases/Gameplay/objects/StrawberryJam2021/jamJar/beginner/")
                  and name.endswith(".png"))
    return chosen


def selections(extracted: Path) -> dict[str, set[str]]:
    return {
        "StrawberryJam2021": root_files(extracted / "StrawberryJam2021"),
        "StrawberryJam2021Assets": {
            *[p.relative_to(extracted / "StrawberryJam2021Assets").as_posix()
              for p in (extracted / "StrawberryJam2021Assets/Graphics/Atlases/Stickers/SJ2021/1-Beginner").glob("*.png")],
            *[p.relative_to(extracted / "StrawberryJam2021Assets").as_posix()
              for p in (extracted / "StrawberryJam2021Assets/Graphics/Atlases/Mountain/StrawberryJam2021/0-Lobbies/1-Beginner").glob("*.png")],
        },
        "StrawberryJam2021AudioA": {
            "Audio/sj21_bingovergoogle.bank", "Audio/sj21_shared.bank",
        },
        "StrawberryJam2021AudioB": {"Audio/sj21_BegLobby.bank"},
        "JungleHelper": {
            f"Graphics/Atlases/Gameplay/JungleHelper/Moss/{name}.png"
            for name in ("moss_top", "moss_mid1", "moss_mid2", "moss_mid3", "moss_bottom")
        },
        "YetAnotherHelper": {
            "Graphics/Atlases/Gameplay/particles/YetAnotherHelper/bubble_a.png",
            "Graphics/Atlases/Gameplay/particles/YetAnotherHelper/bubble_b.png",
        },
        "BrokemiaHelper": set(), "CherryHelper": set(), "FemtoHelper": set(),
        "FlaglinesAndSuch": set(), "FrostHelper": set(), "HonlyHelper": set(),
        "PandorasBox": set(), "VivHelper": set(),
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage-root", type=Path, default=Path(".build/apple-everest/stage25kc"))
    parser.add_argument("--output", type=Path, default=Path("apple-everest/sj-beginner-content-stage25kg.json"))
    args = parser.parse_args()
    extracted = args.stage_root / "extracted"
    packages = args.stage_root / "packages"
    selected = selections(extracted)
    records = []
    for name in sorted(PACKAGES):
        version, archive_expected, logical_expected = PACKAGES[name]
        archive = packages / f"{name}.zip"
        source = extracted / name
        if sha(archive) != archive_expected:
            raise SystemExit(f"archive identity drifted: {name}")
        if logical_hash(source) != logical_expected:
            raise SystemExit(f"source logical identity drifted: {name}")
        included = []
        for relative in sorted(selected[name]):
            path = source / relative
            if not path.is_file():
                raise SystemExit(f"selected file is missing: {name}/{relative}")
            included.append({"path": relative, "sha256": sha(path)})
        records.append({
            "name": name, "version": version, "sourceLogicalSha256": logical_expected,
            "archiveSha256": archive_expected, "includedFiles": included,
        })
    value = {"schemaVersion": 1, "id": "stage25kg-sj-beginner-slice-v1", "packages": records}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"PASS: Stage 25K-G content slice packages={len(records)} files={sum(len(p['includedFiles']) for p in records)}")


if __name__ == "__main__":
    main()
