#!/usr/bin/env python3
"""Generate the modern-iOS visible build identity from its one MSBuild source."""

from __future__ import annotations

import argparse
import pathlib
import re


def value(source: str, name: str) -> str:
    match = re.search(rf"<{name}>([^<]+)</{name}>", source)
    if not match:
        raise SystemExit(f"error: {name} is absent from the iOS version source")
    return match.group(1).strip()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version-source", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()

    source = args.version_source.read_text()
    version = value(source, "IOSPortSemanticVersion")
    build_text = value(source, "IOSPortBuildNumber")
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version):
        raise SystemExit("error: invalid iOS semantic port version")
    if not re.fullmatch(r"[1-9][0-9]*", build_text):
        raise SystemExit("error: invalid iOS bundle build number")

    generated = f'''// Generated from modern-ios/IOSPortVersion.props; do not edit.
namespace CelesteIOSFoundation;

/// <summary>
/// Product identity for the modern iOS/iPadOS port. This is deliberately
/// separate from Celeste's own 1.4.0.0 game/content version.
/// </summary>
public static class IOSPortBuildIdentity
{{
    public const string Version = "{version}";
    public const int Build = {build_text};
    public const string DisplayLabel = "iOS PORT v{version}  •  BUILD {build_text}";
}}
'''
    args.output.parent.mkdir(parents=True, exist_ok=True)
    if not args.output.exists() or args.output.read_text() != generated:
        args.output.write_text(generated)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
