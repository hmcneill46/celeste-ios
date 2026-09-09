#!/usr/bin/env python3
"""Integration negative: copied native products with one changed Mach-O byte.

Requires fresh verified products and Apple tools. Never edits the supplied
products. Temporary archives/logs are private; no vendor fixture is tracked.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("producer", ROOT / "scripts/finalize-apple-archive.py")
producer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(producer)


def changed_object_byte(data):
    """Change unused object string-table space, never archive-index padding.

    The linker may ignore this object byte, but the accepted whole-member
    fingerprint must still reject it. Derive the location from nlist references.
    """
    for part in producer.layout(data):
        for member in part["members"]:
            if member["type"] != "mach-o":
                continue
            start = part["offset"] + member["payloadOffset"]
            body = data[start:start + member["size"]]
            commands, = struct.unpack_from("<I", body, 16)
            position = 32
            for _ in range(commands):
                cmd, size = struct.unpack_from("<II", body, position)
                if cmd == 2:
                    symoff, count, stroff, strsize = struct.unpack_from("<4I", body, position + 8)
                    strings = body[stroff:stroff + strsize]
                    used = 1
                    for strx, kind, section, desc, value in struct.iter_unpack("<IBBHQ", body[symoff:symoff + count * 16]):
                        used = max(used, strx + len(producer.string_at(strings, strx)) + 1)
                        if not kind & 0xe0 and kind & 0x0e == 0x0a:
                            used = max(used, value + len(producer.string_at(strings, value)) + 1)
                    if used < strsize:
                        offset = start + stroff + strsize - 1
                        result = bytearray(data)
                        result[offset] ^= 0x5a
                        result = bytes(result)
                        finalized, _ = producer.finalize(result)
                        if finalized != result:
                            raise AssertionError("producer unexpectedly changed the copied object")
                        return result
                position += size
    raise AssertionError("no suitable unreferenced object string-table byte found")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--platform", choices=("ios", "tvos"), required=True)
    parser.add_argument("--build-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    original_build, original_output = args.build_dir.resolve(), args.output_dir.resolve()
    expected_path = original_output / "normalized-manifest.json"
    expected = json.loads(expected_path.read_text())
    before = {p: hashlib.sha256(p.read_bytes()).hexdigest()
              for root in (original_build / "stage", original_output)
              for p in root.rglob("*") if p.is_file()}
    with tempfile.TemporaryDirectory(prefix="native-payload-rejection-") as temporary:
        work = Path(temporary)
        build, output = work / "build", work / "output"
        shutil.copytree(original_build / "stage", build / "stage")
        output.mkdir()
        for path in original_output.glob("*.xcframework"):
            shutil.copytree(path, output / path.name)
        if args.platform == "tvos":
            for name in ("source-state.json", "generated-symbol-expectations.json"):
                shutil.copyfile(original_build / name, build / name)
            lock = json.loads((ROOT / "native/tvos-dependencies.lock.json").read_text())
            for dep in lock["dependencies"]:
                for name in dep.get("licensePaths", []):
                    relative = Path("sources") / dep["path"] / name
                    (build / relative).parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(original_build / relative, build / relative)
        xcf = output / "Theorafile.xcframework"
        import plistlib
        libraries = plistlib.loads((xcf / "Info.plist").read_bytes())["AvailableLibraries"]
        device = next(x for x in libraries if "SupportedPlatformVariant" not in x)
        packaged = xcf / device["LibraryIdentifier"] / device["LibraryPath"]
        mutated = changed_object_byte(packaged.read_bytes())
        packaged.write_bytes(mutated)
        (build / "stage/Theorafile/device/libTheorafile.a").write_bytes(mutated)
        # Both copies agree, so the negative must reach the unchanged hash gate.
        command = ["bash", str(ROOT / f"scripts/verify-{args.platform}-native.sh"),
                   "--build-dir", str(build), "--output-dir", str(output),
                   "--compare-manifest", str(expected_path)]
        result = subprocess.run(command, capture_output=True, text=True)
        message = ("normalized iOS native output differs from the accepted modern foundation lock"
                   if args.platform == "ios" else "normalized reproducibility mismatch:")
        if result.returncode == 0 or message not in result.stdout + result.stderr:
            raise AssertionError(f"expected final identity rejection; exit {result.returncode}: "
                                 + (result.stdout + result.stderr)[-3000:])
        actual = json.loads((output / "normalized-manifest.json").read_text())
        assert actual["logicalSetSha256"] != expected["logicalSetSha256"]
        assert actual != expected
    after = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in before}
    assert after == before, "supplied native products changed"
    print(f"PASS: {args.platform} changed Mach-O payload survives construction and fails final accepted identity; originals preserved")


if __name__ == "__main__":
    main()
