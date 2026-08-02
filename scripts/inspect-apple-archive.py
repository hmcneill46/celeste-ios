#!/usr/bin/env python3
"""Inspect every member of an Apple static archive, including every fat slice."""

from __future__ import annotations

import argparse
import concurrent.futures
import hashlib
import json
import os
import pathlib
import re
import subprocess
import tempfile


MACHO_MAGICS = {
    b"\xfe\xed\xfa\xce",
    b"\xce\xfa\xed\xfe",
    b"\xfe\xed\xfa\xcf",
    b"\xcf\xfa\xed\xfe",
}


def command(*args: str) -> str:
    return subprocess.check_output(args, text=True, stderr=subprocess.STDOUT).strip()


def archive_members(data: bytes):
    if not data.startswith(b"!<arch>\n"):
        raise ValueError("not a thin ar archive")
    offset = 8
    index = 0
    while offset < len(data):
        if offset + 60 > len(data):
            raise ValueError(f"truncated ar header at byte {offset}")
        header = data[offset : offset + 60]
        if header[58:60] != b"`\n":
            raise ValueError(f"bad ar header at byte {offset}")
        try:
            size = int(header[48:58].decode("ascii").strip())
        except ValueError as exc:
            raise ValueError(f"bad ar member size at byte {offset}") from exc
        raw_name = header[:16].decode("utf-8", "replace").rstrip()
        payload = data[offset + 60 : offset + 60 + size]
        if len(payload) != size:
            raise ValueError(f"truncated ar member at byte {offset}")
        if raw_name.startswith("#1/"):
            name_length = int(raw_name[3:])
            name = payload[:name_length].rstrip(b"\0").decode("utf-8", "replace")
            payload = payload[name_length:]
        else:
            name = raw_name.rstrip("/")
        index += 1
        yield index, name, payload
        offset += 60 + size + (size & 1)


def inspect_member(
    item: tuple[int, str, bytes], architecture: str, work: pathlib.Path
) -> dict[str, object]:
    index, name, payload = item
    record: dict[str, object] = {
        "index": index,
        "name": name,
        "sha256": hashlib.sha256(payload).hexdigest(),
        "size": len(payload),
    }
    if name.startswith("__.SYMDEF") or name in ("/", "//", "SYM64"):
        record["type"] = "archive-metadata"
    elif payload[:4] in MACHO_MAGICS:
        member = work / f"{architecture}-{index}.o"
        member.write_bytes(payload)
        vtool = command("xcrun", "vtool", "-show-build", str(member))
        platform = re.search(r"^\s*platform\s+(\S+)", vtool, re.MULTILINE)
        minos = re.search(r"^\s*minos\s+(\S+)", vtool, re.MULTILINE)
        sdk = re.search(r"^\s*sdk\s+(\S+)", vtool, re.MULTILINE)
        member_archs = command("xcrun", "lipo", "-archs", str(member)).split()
        record.update(
            {
                "type": "mach-o",
                "architectures": member_archs,
                "platform": platform.group(1) if platform else "UNKNOWN",
                "minos": minos.group(1) if minos else "UNKNOWN",
                "sdk": sdk.group(1) if sdk else "UNKNOWN",
            }
        )
    else:
        record["type"] = "non-mach-o"
    return record


def inspect_thin_archive(
    path: pathlib.Path, architecture: str, work: pathlib.Path
) -> list[dict[str, object]]:
    items = list(archive_members(path.read_bytes()))
    with concurrent.futures.ThreadPoolExecutor(max_workers=min(8, os.cpu_count() or 1)) as executor:
        result = list(
            executor.map(lambda item: inspect_member(item, architecture, work), items)
        )
    return sorted(result, key=lambda record: int(record["index"]))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=pathlib.Path)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    args = parser.parse_args()

    archive = args.archive.resolve()
    architectures = command("xcrun", "lipo", "-archs", str(archive)).split()
    if not architectures:
        raise SystemExit(f"no architectures reported for {archive}")

    slices: list[dict[str, object]] = []
    with tempfile.TemporaryDirectory(prefix="tvos-archive-inspect-") as temporary:
        work = pathlib.Path(temporary)
        for architecture in architectures:
            thin = work / f"{architecture}.a"
            if len(architectures) == 1:
                thin.write_bytes(archive.read_bytes())
            else:
                subprocess.check_call(
                    ["xcrun", "lipo", str(archive), "-thin", architecture, "-output", str(thin)]
                )
            slices.append(
                {
                    "architecture": architecture,
                    "members": inspect_thin_archive(thin, architecture, work),
                }
            )

    result = {
        "schemaVersion": 1,
        "archiveSha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
        "architectures": architectures,
        "slices": slices,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
