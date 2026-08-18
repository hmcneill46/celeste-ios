#!/usr/bin/env python3
"""List available physical iPhone/iPad devices from Xcode's xcdevice JSON."""

from __future__ import annotations

import argparse
import json
import pathlib


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    values = json.loads(args.input.read_text())
    rows: list[tuple[str, str, str]] = []
    for value in values:
        if value.get("simulator"):
            continue
        if value.get("platform") != "com.apple.platform.iphoneos":
            continue
        if value.get("available") is not True:
            continue
        identifier = str(value.get("identifier", "")).strip()
        name = str(value.get("name", "iPhone or iPad")).replace("\t", " ").replace("\n", " ")
        version = str(value.get("operatingSystemVersion", "unknown iOS")).replace("\t", " ").replace("\n", " ")
        if identifier:
            rows.append((identifier, name, version))
    rows.sort(key=lambda row: (row[1].casefold(), row[2], row[0]))
    text = "".join("\t".join(row) + "\n" for row in rows)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(text)
    else:
        print(text, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
