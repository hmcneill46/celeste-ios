#!/usr/bin/env python3
"""Inventory the locked Celeste 1.4.0.0 controller-prompt atlas metadata."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import pathlib
import struct
import sys
from typing import Any


class Reader:
    def __init__(self, data: bytes) -> None:
        self.data = data
        self.position = 0

    def int16(self) -> int:
        value = struct.unpack_from("<h", self.data, self.position)[0]
        self.position += 2
        return value

    def int32(self) -> int:
        value = struct.unpack_from("<i", self.data, self.position)[0]
        self.position += 4
        return value

    def string(self) -> str:
        length = 0
        shift = 0
        while True:
            if self.position >= len(self.data) or shift >= 35:
                raise ValueError("invalid BinaryReader string length")
            value = self.data[self.position]
            self.position += 1
            length |= (value & 0x7F) << shift
            if value & 0x80 == 0:
                break
            shift += 7
        end = self.position + length
        if end > len(self.data):
            raise ValueError("truncated BinaryReader string")
        value = self.data[self.position:end].decode("utf-8")
        self.position = end
        return value


def parse_paths(data: bytes) -> list[str]:
    reader = Reader(data)
    if reader.int32() != 5:
        raise ValueError("unsupported Gui atlas metadata version")
    _ = reader.string()
    _ = reader.int32()
    paths: list[str] = []
    for _ in range(reader.int16()):
        _ = reader.string()
        for _ in range(reader.int16()):
            paths.append(reader.string().replace("\\", "/"))
            for _ in range(8):
                reader.int16()
    if reader.position != len(data):
        marker = reader.string()
        if marker != "LINKS":
            raise ValueError("unexpected Gui atlas trailer")
        for _ in range(reader.int16()):
            _ = reader.string()
            _ = reader.string()
    if reader.position != len(data):
        raise ValueError("trailing Gui atlas metadata")
    return paths


def inventory(game_root: pathlib.Path, lock: dict[str, Any]) -> dict[str, Any]:
    atlas_path = game_root / lock["atlas"]["relativePath"]
    if not atlas_path.is_file():
        raise ValueError("locked Gui atlas metadata is missing")
    data = atlas_path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    paths = parse_paths(data)
    lowered = {path.lower(): path for path in paths}
    required = list(lock["requiredButtons"])
    fallback_prefix = "controls/fallback/"
    families: list[dict[str, Any]] = []
    for expected in lock["families"]:
        prefix = expected["prefix"]
        direct_assets = sorted(path for path in paths if path.lower().startswith(f"controls/{prefix}/".lower()))
        resolved: list[dict[str, str]] = []
        missing: list[str] = []
        for button in required:
            direct_key = f"controls/{prefix}/{button}".lower()
            fallback_key = f"{fallback_prefix}{button}".lower()
            if direct_key in lowered:
                resolved.append({"button": button, "source": "family", "path": lowered[direct_key]})
            elif fallback_key in lowered:
                resolved.append({"button": button, "source": "fallback", "path": lowered[fallback_key]})
            else:
                missing.append(button)
        family = {
            "displayName": expected["displayName"],
            "prefix": prefix,
            "atlasAssetCount": len(direct_assets),
            "requiredInputCount": len(required),
            "directResolutionCount": sum(item["source"] == "family" for item in resolved),
            "fallbackResolutionCount": sum(item["source"] == "fallback" for item in resolved),
            "complete": not missing,
            "selectable": not missing,
            "missingButtons": missing,
            "resolved": resolved,
        }
        families.append(family)
    return {
        "schemaVersion": 1,
        "gameVersion": lock["gameVersion"],
        "source": "$CELESTE_GAME_ROOT/" + lock["atlas"]["relativePath"],
        "atlasSha256": digest,
        "atlasTextureCount": len(paths),
        "requiredInputCount": len(required),
        "families": families,
        "proprietaryArtworkIncluded": False,
    }


def verify(result: dict[str, Any], lock: dict[str, Any]) -> None:
    if result["atlasSha256"] != lock["atlas"]["sha256"]:
        raise ValueError("Gui atlas metadata hash is not the locked Celeste 1.4.0.0 value")
    if result["atlasTextureCount"] != lock["atlas"]["textureCount"]:
        raise ValueError("Gui atlas texture count changed")
    if len(result["families"]) != len(lock["families"]):
        raise ValueError("controller prompt family count changed")
    for actual, expected in zip(result["families"], lock["families"]):
        for key in ("displayName", "prefix", "atlasAssetCount", "directResolutionCount", "fallbackResolutionCount", "complete", "selectable"):
            if actual[key] != expected[key]:
                raise ValueError(f"{expected['displayName']} prompt inventory changed at {key}")
        if actual["missingButtons"]:
            raise ValueError(f"{expected['displayName']} is missing required prompts: {', '.join(actual['missingButtons'])}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-root", default=None, help="validated Celeste root (default: CELESTE_GAME_ROOT)")
    parser.add_argument("--lock", type=pathlib.Path, default=None)
    parser.add_argument("--output", type=pathlib.Path, default=None)
    args = parser.parse_args()
    repository = pathlib.Path(__file__).resolve().parent.parent
    game_value = args.game_root or os.environ.get("CELESTE_GAME_ROOT")
    if not game_value:
        parser.error("--game-root or CELESTE_GAME_ROOT is required")
    lock_path = args.lock or repository / "managed" / "celeste-controller-prompts.lock.json"
    output = args.output or repository / ".build" / "celeste-controller-prompts" / "inventory.json"
    try:
        lock = json.loads(lock_path.read_text(encoding="utf-8"))
        result = inventory(pathlib.Path(game_value).expanduser().resolve(), lock)
        verify(result, lock)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as exception:
        print(f"error: controller-prompt inventory failed: {exception}", file=sys.stderr)
        return 1
    print("PASS: locked Celeste controller prompt families are complete")
    for family in result["families"]:
        print(f"  {family['displayName']}: {family['requiredInputCount']}/24 required inputs resolve")
    print("manifest: .build/celeste-controller-prompts/inventory.json")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
