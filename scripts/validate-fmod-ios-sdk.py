#!/usr/bin/env python3
"""Validate the exact user-supplied FMOD iPhone SDK before expensive work."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sdk-root", type=pathlib.Path, required=True)
    parser.add_argument("--lock", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    root = args.sdk_root.resolve()
    lock = json.loads(args.lock.read_text())

    revision = root / "doc/revision.txt"
    header = root / "api/lowlevel/inc/fmod_common.h"
    if not revision.is_file() or revision.is_symlink():
        raise SystemExit("error: FMOD doc/revision.txt is missing")
    if not header.is_file() or header.is_symlink():
        raise SystemExit("error: FMOD api/lowlevel/inc/fmod_common.h is missing")
    if not re.search(r"1\.10\.09.*build 97915", revision.read_text(errors="replace")):
        raise SystemExit("error: FMOD must be Engine iOS/tvOS 1.10.09 build 97915")
    if not re.search(r"^#define\s+FMOD_VERSION\s+0x00011009", header.read_text(), re.MULTILINE):
        raise SystemExit("error: FMOD header version is not 1.10.09")

    result = {
        "schemaVersion": 1,
        "release": lock["version"],
        "build": lock["build"],
        "headerVersion": lock["headerVersion"],
        "deviceArchives": {},
    }
    for label, expected in lock["deviceArchives"].items():
        path = root / expected["path"]
        if not path.is_file() or path.is_symlink():
            raise SystemExit(f"error: required FMOD {label} iPhone archive is missing")
        actual = sha256(path)
        if actual != expected["sha256"]:
            raise SystemExit(f"error: exact FMOD {label} archive fingerprint mismatch")
        result["deviceArchives"][label] = {"sha256": actual}

    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n")
    print("PASS: FMOD Engine iOS/tvOS 1.10.09 build 97915")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
