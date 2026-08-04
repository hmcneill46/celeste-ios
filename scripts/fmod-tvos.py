#!/usr/bin/env python3
"""Privacy-safe validation helpers for the Stage 5A FMOD tvOS lane."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import pathlib
import re
import struct
import subprocess
import tempfile
from typing import Dict, Iterable, List, Tuple


EXPECTED_VERSION = "1.10.09"
EXPECTED_BUILD = 97915
EXPECTED_HEADER = "0x00011009"
EXPECTED_ARCHITECTURES = ["arm64"]

SDK_FILES = {
    "revision": "doc/revision.txt",
    "lowLevelHeader": "api/lowlevel/inc/fmod.h",
    "lowLevelCommonHeader": "api/lowlevel/inc/fmod_common.h",
    "lowLevelOutputHeader": "api/lowlevel/inc/fmod_output.h",
    "studioHeader": "api/studio/inc/fmod_studio.h",
    "studioCommonHeader": "api/studio/inc/fmod_studio_common.h",
    "lowLevelArchive": "api/lowlevel/lib/libfmod_appletvos.a",
    "studioArchive": "api/studio/lib/libfmodstudio_appletvos.a",
}

REQUIRED_SYMBOLS = {
    "lowLevelArchive": [
        "FMOD_System_Close",
        "FMOD_System_Create",
        "FMOD_System_GetVersion",
        "FMOD_System_Init",
        "FMOD_System_RegisterOutput",
        "FMOD_System_Release",
        "FMOD_System_SetOutputByPlugin",
        "FMOD_System_Update",
    ],
    "studioArchive": [
        "FMOD_Studio_Bank_GetEventCount",
        "FMOD_Studio_Bank_GetEventList",
        "FMOD_Studio_Bank_GetPath",
        "FMOD_Studio_Bank_Unload",
        "FMOD_Studio_Bus_GetMute",
        "FMOD_Studio_Bus_GetPaused",
        "FMOD_Studio_Bus_GetVolume",
        "FMOD_Studio_Bus_SetMute",
        "FMOD_Studio_Bus_SetPaused",
        "FMOD_Studio_Bus_SetVolume",
        "FMOD_Studio_EventDescription_CreateInstance",
        "FMOD_Studio_EventDescription_GetLength",
        "FMOD_Studio_EventDescription_GetParameterByIndex",
        "FMOD_Studio_EventDescription_GetParameterCount",
        "FMOD_Studio_EventDescription_GetPath",
        "FMOD_Studio_EventInstance_GetPlaybackState",
        "FMOD_Studio_EventInstance_Release",
        "FMOD_Studio_EventInstance_SetParameterValue",
        "FMOD_Studio_EventInstance_Start",
        "FMOD_Studio_EventInstance_Stop",
        "FMOD_Studio_System_Create",
        "FMOD_Studio_System_FlushCommands",
        "FMOD_Studio_System_GetBankCount",
        "FMOD_Studio_System_GetBankList",
        "FMOD_Studio_System_GetBus",
        "FMOD_Studio_System_GetEvent",
        "FMOD_Studio_System_GetLowLevelSystem",
        "FMOD_Studio_System_Initialize",
        "FMOD_Studio_System_LoadBankFile",
        "FMOD_Studio_System_Release",
        "FMOD_Studio_System_UnloadAll",
        "FMOD_Studio_System_Update",
    ],
}

MACHO_MAGICS = {
    b"\xfe\xed\xfa\xce",
    b"\xce\xfa\xed\xfe",
    b"\xfe\xed\xfa\xcf",
    b"\xcf\xfa\xed\xfe",
}


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def run(*args: str) -> str:
    return subprocess.check_output(args, text=True, stderr=subprocess.STDOUT).strip()


def archive_members(data: bytes) -> Iterable[Tuple[str, bytes]]:
    if not data.startswith(b"!<arch>\n"):
        raise ValueError("input is not a thin ar archive")
    offset = 8
    while offset < len(data):
        if offset + 60 > len(data):
            raise ValueError("truncated ar header")
        header = data[offset : offset + 60]
        if header[58:60] != b"`\n":
            raise ValueError("invalid ar header")
        size = int(header[48:58].decode("ascii").strip())
        raw_name = header[:16].decode("utf-8", "replace").rstrip()
        payload = data[offset + 60 : offset + 60 + size]
        if len(payload) != size:
            raise ValueError("truncated ar member")
        if raw_name.startswith("#1/"):
            name_length = int(raw_name[3:])
            name = payload[:name_length].rstrip(b"\0").decode("utf-8", "replace")
            payload = payload[name_length:]
        else:
            name = raw_name.rstrip("/")
        yield name, payload
        offset += 60 + size + (size & 1)


def parse_build_evidence(vtool: str, otool: str) -> Tuple[str, str, str]:
    platform_match = re.search(r"^\s*platform\s+(\S+)", vtool, re.MULTILINE)
    minos_match = re.search(r"^\s*minos\s+(\S+)", vtool, re.MULTILINE)
    sdk_match = re.search(r"^\s*sdk\s+(\S+)", vtool, re.MULTILINE)
    if platform_match:
        return (
            platform_match.group(1),
            minos_match.group(1) if minos_match else "UNKNOWN",
            sdk_match.group(1) if sdk_match else "UNKNOWN",
        )
    legacy = re.search(r"cmd LC_VERSION_MIN_(TVOS|IPHONEOS|MACOSX).*?\n\s*version\s+(\S+).*?\n\s*sdk\s+(\S+)", otool, re.DOTALL)
    if legacy:
        mapped = {"TVOS": "TVOS", "IPHONEOS": "IPHONEOS", "MACOSX": "MACOS"}[legacy.group(1)]
        return mapped, legacy.group(2), legacy.group(3)
    return "UNKNOWN", "UNKNOWN", "UNKNOWN"


def inspect_archive(path: pathlib.Path, required_symbols: List[str]) -> Dict[str, object]:
    architectures = run("xcrun", "lipo", "-archs", str(path)).split()
    if architectures != EXPECTED_ARCHITECTURES:
        raise ValueError(f"expected device architecture arm64, found {architectures!r}")
    defined = {item.removeprefix("_") for item in run("nm", "-gjU", str(path)).splitlines()}
    missing = sorted(set(required_symbols) - defined)
    if missing:
        raise ValueError("missing required symbols: " + ", ".join(missing))

    members: List[Dict[str, object]] = []
    macho_count = 0
    with tempfile.TemporaryDirectory(prefix="fmod-tvos-inspect-") as temporary:
        temporary_root = pathlib.Path(temporary)
        for index, (name, payload) in enumerate(archive_members(path.read_bytes()), start=1):
            if name.startswith("__.SYMDEF") or name in ("/", "//", "SYM64"):
                members.append({"name": name, "type": "archive-metadata", "size": len(payload)})
                continue
            if payload[:4] not in MACHO_MAGICS:
                raise ValueError(f"unexpected non-Mach-O member: {name}")
            macho_count += 1
            member = temporary_root / f"member-{index}.o"
            member.write_bytes(payload)
            member_architectures = run("xcrun", "lipo", "-archs", str(member)).split()
            vtool = run("xcrun", "vtool", "-show-build", str(member))
            otool = run("xcrun", "otool", "-l", str(member))
            platform, minimum_os, sdk = parse_build_evidence(vtool, otool)
            if member_architectures != ["arm64"]:
                raise ValueError(f"archive member {name} is not arm64")
            if platform != "TVOS":
                raise ValueError(f"archive member {name} is {platform}, not TVOS")
            if minimum_os == "UNKNOWN" or tuple(map(int, minimum_os.split("."))) > (16, 0):
                raise ValueError(f"archive member {name} requires incompatible tvOS {minimum_os}")
            if re.search(r"LC_(LOAD|LOAD_WEAK|REEXPORT)_DYLIB", otool):
                raise ValueError(f"archive member {name} contains a dynamic-library load command")
            members.append(
                {
                    "name": name,
                    "type": "mach-o",
                    "size": len(payload),
                    "sha256": hashlib.sha256(payload).hexdigest(),
                    "architectures": member_architectures,
                    "platform": platform,
                    "minimumOS": minimum_os,
                    "sdk": sdk,
                }
            )
    if macho_count == 0:
        raise ValueError("archive contains no Mach-O members")
    return {
        "size": path.stat().st_size,
        "sha256": sha256(path),
        "architectures": architectures,
        "platform": "TVOS",
        "static": True,
        "dynamicLoadCommands": False,
        "requiredSymbols": required_symbols,
        "memberCount": len(members),
        "members": members,
    }


def validate_sdk(sdk_root: pathlib.Path, output: pathlib.Path) -> None:
    if not sdk_root.is_dir():
        raise SystemExit("error: FMOD SDK root is not a readable directory")
    resolved: Dict[str, pathlib.Path] = {}
    for label, relative in SDK_FILES.items():
        candidate = sdk_root / relative
        if not candidate.is_file() or not os.access(candidate, os.R_OK):
            raise SystemExit(f"error: required FMOD SDK file is missing or unreadable: $FMOD_SDK_ROOT/{relative}")
        resolved[label] = candidate

    revision = resolved["revision"].read_text(encoding="utf-8", errors="replace")
    release_match = re.search(r"\b1\.10\.09\b.*?\bbuild\s+(\d+)\b", revision, re.IGNORECASE)
    if not release_match or int(release_match.group(1)) != EXPECTED_BUILD:
        raise SystemExit("error: FMOD SDK must report release 1.10.09 build 97915")
    common = resolved["lowLevelCommonHeader"].read_text(encoding="utf-8", errors="replace")
    header_match = re.search(r"^#define\s+FMOD_VERSION\s+(0x[0-9A-Fa-f]+)\b", common, re.MULTILINE)
    if not header_match or header_match.group(1).lower() != EXPECTED_HEADER:
        raise SystemExit("error: FMOD_VERSION must equal 0x00011009")

    archives: Dict[str, object] = {}
    try:
        for archive_label in ("lowLevelArchive", "studioArchive"):
            archives[archive_label] = inspect_archive(resolved[archive_label], REQUIRED_SYMBOLS[archive_label])
    except (subprocess.CalledProcessError, ValueError) as error:
        raise SystemExit(f"error: FMOD archive validation failed: {error}") from error

    files = {}
    for label, relative in SDK_FILES.items():
        candidate = resolved[label]
        files[label] = {
            "path": "$FMOD_SDK_ROOT/" + relative,
            "size": candidate.stat().st_size,
            "sha256": sha256(candidate),
        }
    manifest = {
        "schemaVersion": 1,
        "source": "$FMOD_SDK_ROOT",
        "release": EXPECTED_VERSION,
        "build": EXPECTED_BUILD,
        "headerVersion": EXPECTED_HEADER,
        "deploymentCompatibility": "archive minimum tvOS <= 16.0",
        "dynamicFrameworkRequired": False,
        "files": files,
        "archives": archives,
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def validate_banks(game_root: pathlib.Path, output: pathlib.Path) -> None:
    bank_root = game_root / "Content" / "FMOD"
    if not bank_root.is_dir():
        raise SystemExit("error: $CELESTE_GAME_ROOT/Content/FMOD is missing")
    records = []
    for current, directories, filenames in os.walk(bank_root, followlinks=False):
        current_path = pathlib.Path(current)
        for name in list(directories) + list(filenames):
            candidate = current_path / name
            if candidate.is_symlink():
                raise SystemExit("error: Content/FMOD contains a symbolic link")
        for name in filenames:
            candidate = current_path / name
            if not candidate.is_file() or not os.access(candidate, os.R_OK):
                raise SystemExit("error: Content/FMOD contains an unreadable non-file entry")
            relative = candidate.relative_to(game_root).as_posix()
            if candidate.suffix.lower() != ".bank":
                raise SystemExit(f"error: unexpected non-bank FMOD input: {relative}")
            records.append({"path": relative, "size": candidate.stat().st_size, "sha256": sha256(candidate)})
    records.sort(key=lambda item: item["path"])
    if not records:
        raise SystemExit("error: no FMOD banks were found")
    names = [pathlib.PurePosixPath(item["path"]).name for item in records]
    master = [name for name in names if name.lower().endswith("master bank.bank")]
    strings = [name for name in names if name.lower().endswith("master bank.strings.bank")]
    if len(master) != 1 or len(strings) != 1:
        raise SystemExit("error: FMOD bank tree must contain exactly one master and one strings bank")
    manifest = {
        "schemaVersion": 1,
        "source": "$CELESTE_GAME_ROOT/Content/FMOD",
        "count": len(records),
        "totalBytes": sum(int(item["size"]) for item in records),
        "masterBank": next(item["path"] for item in records if item["path"].endswith(master[0])),
        "stringsBank": next(item["path"] for item in records if item["path"].endswith(strings[0])),
        "banks": records,
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    sdk = subparsers.add_parser("validate-sdk", help="validate an external FMOD 1.10.09 SDK")
    sdk.add_argument("--sdk-root", required=True, type=pathlib.Path)
    sdk.add_argument("--output", required=True, type=pathlib.Path)
    banks = subparsers.add_parser("validate-banks", help="validate user-owned Celeste FMOD banks")
    banks.add_argument("--game-root", required=True, type=pathlib.Path)
    banks.add_argument("--output", required=True, type=pathlib.Path)
    args = parser.parse_args()
    if args.command == "validate-sdk":
        validate_sdk(args.sdk_root.resolve(), args.output.resolve())
    else:
        validate_banks(args.game_root.resolve(), args.output.resolve())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
