#!/usr/bin/env python3
"""Freeze the shared Apple-port identity into a generated platform menu."""

from __future__ import annotations

import argparse
import pathlib
import re


def value(source: str, name: str) -> str:
    match = re.search(rf"<{name}>([^<]+)</{name}>", source)
    if not match:
        raise SystemExit(f"error: {name} is absent from the Apple port version source")
    return match.group(1).strip()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version-source", type=pathlib.Path, required=True)
    parser.add_argument("--managed-root", type=pathlib.Path, required=True)
    parser.add_argument("--platform", choices=("tvos",), required=True)
    args = parser.parse_args()

    source = args.version_source.read_text()
    version = value(source, "IOSPortSemanticVersion")
    build = value(source, "IOSPortBuildNumber")
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version):
        raise SystemExit("error: invalid Apple port semantic version")
    if not re.fullmatch(r"[1-9][0-9]*", build):
        raise SystemExit("error: invalid Apple bundle build number")

    menu = args.managed_root / "Celeste" / "MenuOptions.cs"
    text = menu.read_text()
    label = f"tvOS PORT v{version}  •  BUILD {build}"
    generated = f'\t\tmenu.Add(new TextMenu.SubHeader("{label}", false));'
    if generated in text:
        if text.count(generated) != 1:
            raise SystemExit("error: generated tvOS build identity is duplicated")
        return 0
    if "tvOS PORT v" in text:
        raise SystemExit("error: stale tvOS build identity is already present")

    anchor = '\t\tmenu.Add(new TextMenu.SubHeader("APPLE TV"));'
    if text.count(anchor) != 1:
        raise SystemExit("error: expected one tvOS Options identity anchor")
    menu.write_text(text.replace(anchor, anchor + "\n" + generated, 1))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
