#!/usr/bin/env python3
"""Render the locked, attributed touch SVG sources to deterministic 128x128 A8."""

from __future__ import annotations

import argparse
import hashlib
import pathlib
import shutil
import struct
import subprocess
import tempfile
import zlib


SOURCE_HASHES = {
    "jump": "750c4a4887bb408e2b676eb406727a82d8df68ec6fddd1c79ea4a17903ffd4a8",
    "dash": "dac71354f15a90cb1efc7fef7f65ecc8632787e70fd02239f7747307efb7a94c",
    "grab-ungrabbed": "7b0e66d8ec3a5fcaa0cda5f91d6876ec32497433b9e15deb9f6eaf353fb60674",
    "grab-grabbed": "cd73b8c3f0d525272653ab5eb68701c8ead0b2047e4363a4675da333816c4ed8",
    "touch": "579f27975833686c0e2d820a355967f3136bb0d2b79adb2edc4d1f6bf418f330",
    "pause": "63ea2c81138abfe21f6d6453547262690065e950b3019457f36c5008e4929d91",
    "journal": "7341cece5f9e1f23e4b75951e40630dd116f985293c7869f5c0b46af2af424cd",
}


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def png_alpha(path: pathlib.Path) -> bytes:
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        fail("sips did not produce PNG")
    position = 8
    compressed = bytearray()
    width = height = bit_depth = color_type = interlace = 0
    while position < len(data):
        length = int.from_bytes(data[position : position + 4], "big")
        kind = data[position + 4 : position + 8]
        payload = data[position + 8 : position + 8 + length]
        position += 12 + length
        if kind == b"IHDR":
            width, height, bit_depth, color_type, _, _, interlace = struct.unpack(">IIBBBBB", payload)
        elif kind == b"IDAT":
            compressed.extend(payload)
    if (width, height, bit_depth, color_type, interlace) != (128, 128, 8, 6, 0):
        fail(f"unexpected PNG format: {width}x{height}, depth={bit_depth}, color={color_type}, interlace={interlace}")

    source = zlib.decompress(bytes(compressed))
    bytes_per_pixel = 4
    stride = width * bytes_per_pixel
    previous = bytearray(stride)
    alpha = bytearray()
    offset = 0
    for _ in range(height):
        filter_kind = source[offset]
        offset += 1
        row = bytearray(source[offset : offset + stride])
        offset += stride
        for index in range(stride):
            left = row[index - bytes_per_pixel] if index >= bytes_per_pixel else 0
            above = previous[index]
            upper_left = previous[index - bytes_per_pixel] if index >= bytes_per_pixel else 0
            if filter_kind == 1:
                predictor = left
            elif filter_kind == 2:
                predictor = above
            elif filter_kind == 3:
                predictor = (left + above) // 2
            elif filter_kind == 4:
                candidate = left + above - upper_left
                distances = (abs(candidate - left), abs(candidate - above), abs(candidate - upper_left))
                predictor = (left, above, upper_left)[distances.index(min(distances))]
            elif filter_kind == 0:
                predictor = 0
            else:
                fail(f"unsupported PNG filter: {filter_kind}")
            row[index] = (row[index] + predictor) & 0xFF
        alpha.extend(row[3::4])
        previous = row
    if len(alpha) != 128 * 128:
        fail("generated A8 payload has the wrong size")
    return bytes(alpha)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=pathlib.Path, required=True)
    parser.add_argument("--output-dir", type=pathlib.Path, required=True)
    args = parser.parse_args()
    if shutil.which("sips") is None:
        fail("Apple sips is required to rasterize the tracked SVG sources")
    args.output_dir.mkdir(parents=True, exist_ok=True)
    records: list[str] = []
    with tempfile.TemporaryDirectory(prefix="celeste-ios-touch-assets.") as temporary:
        temporary_root = pathlib.Path(temporary)
        for name, expected_source_hash in SOURCE_HASHES.items():
            source = args.source_dir / f"{name}.svg"
            actual_source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
            if actual_source_hash != expected_source_hash:
                fail(f"locked touch SVG changed: {name}")
            png = temporary_root / f"{name}.png"
            subprocess.run(
                ["sips", "-s", "format", "png", "-z", "128", "128", str(source), "--out", str(png)],
                check=True,
                stdout=subprocess.DEVNULL,
            )
            payload = png_alpha(png)
            output = args.output_dir / f"{name}.a8"
            output.write_bytes(payload)
            records.append(f"{name}={hashlib.sha256(payload).hexdigest()}")
    print("PASS: generated iOS touch A8 assets; " + "; ".join(records))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
