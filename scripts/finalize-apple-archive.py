#!/usr/bin/env python3
"""Construct deterministic BSD archive-index padding in owned native build roots.

Supports little-endian 64-bit Mach-O objects in BSD ar, optionally wrapped in
a big-endian FAT_MAGIC container. See docs/APPLE_NATIVE_REPRODUCIBILITY.md.
This producer never changes the acceptance verifier's byte interpretation.
"""

from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import stat
import struct
import tempfile


class ArchiveError(ValueError):
    """Unsupported or inconsistent input; no output may be published."""


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ArchiveError(message)


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def region(data: bytes, offset: int, size: int) -> bytes:
    require(0 <= offset <= len(data) and 0 <= size <= len(data) - offset,
            "truncated or out-of-bounds region")
    return data[offset:offset + size]


def string_at(data: bytes, offset: int) -> bytes:
    require(0 <= offset < len(data), "invalid string offset")
    end = data.find(b"\0", offset)
    require(end >= 0, "unterminated string")
    return data[offset:end]


def architecture(cpu: int, subtype: int) -> str:
    key = (cpu, subtype & 0x00ffffff)
    names = {(0x0100000c, 0): "arm64", (0x0100000c, 2): "arm64e",
             (0x01000007, 3): "x86_64"}
    require(key in names, "unsupported Mach-O architecture")
    return names[key]


def object_symbols(data: bytes) -> tuple[str, list[bytes]]:
    require(region(data, 0, 4) == b"\xcf\xfa\xed\xfe", "unsupported object format")
    _, cpu, subtype, filetype, ncmds, sizeofcmds, _, reserved = struct.unpack(
        "<8I", region(data, 0, 32))
    arch = architecture(cpu, subtype)
    require(filetype == 1 and reserved == 0, "expected MH_OBJECT")
    region(data, 32, sizeofcmds)
    require(ncmds <= sizeofcmds // 8, "invalid load-command count")
    end, position, symtab = 32 + sizeofcmds, 32, None
    for _ in range(ncmds):
        cmd, size = struct.unpack("<II", region(data, position, 8))
        require(size >= 8 and size % 8 == 0 and position + size <= end,
                "invalid load-command size")
        if cmd == 2:  # LC_SYMTAB
            require(size == 24 and symtab is None, "ambiguous symbol table")
            symtab = struct.unpack("<4I", region(data, position + 8, 16))
        position += size
    require(position == end, "malformed load-command table")
    # Architecture-guarded assembly can emit a valid empty object without an
    # LC_SYMTAB. It contributes no index entries and is still preserved whole.
    if symtab is None:
        return arch, []
    symoff, nsyms, stroff, strsize = symtab
    require(symoff >= end and stroff >= symoff + nsyms * 16,
            "overlapping object symbol/string tables")
    symbols = region(data, symoff, nsyms * 16)
    strings = region(data, stroff, strsize)
    definitions = []
    for strx, kind, section, desc, value in struct.iter_unpack("<IBBHQ", symbols):
        name = string_at(strings, strx)
        if kind & 0xe0 or not kind & 1:  # N_STAB or not N_EXT
            continue
        symbol_type = kind & 0x0e
        require(symbol_type in (0, 2, 0x0a, 0x0e), "unsupported external symbol type")
        if symbol_type == 0x0a:  # N_INDR target is a string-table offset
            require(bool(string_at(strings, value)), "empty indirect symbol target")
        if symbol_type != 0 or value != 0:  # defined or common, not undefined
            require(bool(name), "empty defined symbol")
            definitions.append(name)
    return arch, definitions


def thin_layout(data: bytes) -> tuple[str, list[dict], tuple[int, int], dict]:
    require(data.startswith(b"!<arch>\n"), "unsupported archive format")
    position, members, indexes, expected, archs = 8, [], [], [], set()
    while position < len(data):
        header = region(data, position, 60)
        require(header[58:] == b"`\n", "invalid ar header")
        size_text = header[48:58].strip()
        require(bool(size_text) and size_text.isdigit(), "invalid ar member size")
        size = int(size_text)
        payload_start = position + 60
        stored = region(data, payload_start, size)
        name = header[:16].rstrip(b" ")
        if name.startswith(b"#1/"):
            length_text = name[3:]
            require(bool(length_text) and length_text.isdigit(), "invalid extended name")
            length = int(length_text)
            require(0 < length <= size, "extended name outside member")
            stored_name = stored[:length]
            name = stored_name.rstrip(b"\0")
            require(b"\0" not in name, "ambiguous extended name")
            payload_start += length
            stored = stored[length:]
        else:
            require(name not in (b"/", b"//", b"/SYM64/") and b"/" not in name[:-1],
                    "unsupported GNU ar member name")
            name = name.removesuffix(b"/")
        require(bool(name), "empty ar member name")
        record = {"index": len(members) + 1, "name": name.decode("utf-8", "strict"),
                  "offset": position, "payloadOffset": payload_start,
                  "size": len(stored), "sha256": sha(stored)}
        if name in (b"__.SYMDEF", b"__.SYMDEF SORTED"):
            require(not members, "archive index must be the first member")
            indexes.append((stored, payload_start, name))
            record["type"] = "archive-metadata"
        else:
            require(not name.startswith(b"__.SYMDEF"), "unsupported archive index format")
            arch, definitions = object_symbols(stored)
            archs.add(arch)
            expected.extend((symbol, position) for symbol in definitions)
            record["type"] = "mach-o"
        members.append(record)
        position += 60 + size
        if size & 1:
            require(region(data, position, 1) == b"\n", "invalid ar alignment byte")
            position += 1
    require(position == len(data) and len(indexes) == 1 and len(archs) == 1,
            "missing index, missing objects, or mixed thin architectures")
    index, payload_start, index_name = indexes[0]
    table_size, = struct.unpack("<I", region(index, 0, 4))
    require(table_size % 8 == 0, "invalid ranlib table length")
    table = region(index, 4, table_size)
    string_size, = struct.unpack("<I", region(index, 4 + table_size, 4))
    strings = region(index, 8 + table_size, string_size)
    require(len(index) == 8 + table_size + string_size, "trailing index payload")
    entries, ranges = [], {}
    object_offsets = {m["offset"] for m in members if m["type"] == "mach-o"}
    for strx, member_offset in struct.iter_unpack("<II", table):
        require(member_offset in object_offsets, "ranlib references a non-object member")
        name = string_at(strings, strx)
        require(bool(name), "empty ranlib symbol")
        ranges[strx] = strx + len(name) + 1
        entries.append((name, member_offset))
    # Bind every index entry to the actual object's defined external symbols.
    # A shortened/removed/redirected string cannot become "padding" silently.
    require(Counter(entries) == Counter(expected), "index differs from object definitions")
    if index_name == b"__.SYMDEF SORTED":
        names = [name for name, _ in entries]
        require(names == sorted(names), "unsorted SORTED index")
    used = 0
    for start, end in sorted(ranges.items()):
        require(start == used, "overlapping or unreferenced index strings")
        used = end
    # Apple's 64-bit archive producer rounds the string allocation to 8 bytes.
    # All actual strings must form a contiguous prefix. Only this terminal
    # alignment region, at most seven bytes, is eligible for initialization.
    require(string_size == (used + 7) // 8 * 8, "unsupported string-table alignment")
    padding = (payload_start + 8 + table_size + used, string_size - used)
    return archs.pop(), members, padding, {"entries": len(entries), "stringBytes": used}


def layout(data: bytes) -> list[dict]:
    if data.startswith(b"!<arch>\n"):
        slices = [(None, 0, len(data))]
    else:
        require(region(data, 0, 4) == b"\xca\xfe\xba\xbe", "unsupported fat archive format")
        count, = struct.unpack(">I", region(data, 4, 4))
        require(1 <= count <= 3, "unsupported fat slice count")
        table = region(data, 8, count * 20)
        slices, spans, seen = [], [], set()
        for cpu, subtype, offset, size, align in struct.iter_unpack(">5I", table):
            arch = architecture(cpu, subtype)
            require(arch not in seen, "duplicate fat architecture")
            seen.add(arch)
            require(align <= 20 and offset % (1 << align) == 0 and offset >= 8 + count * 20,
                    "invalid fat slice alignment or offset")
            region(data, offset, size)
            require(size > 0, "empty fat slice")
            spans.append((offset, offset + size))
            slices.append((arch, offset, size))
        spans.sort()
        require(all(a[1] <= b[0] for a, b in zip(spans, spans[1:])), "overlapping fat slices")
        require(spans[-1][1] == len(data), "trailing fat archive data")
    records = []
    for expected_arch, offset, size in slices:
        arch, members, (pad_start, pad_size), index = thin_layout(region(data, offset, size))
        require(expected_arch in (None, arch), "fat header disagrees with objects")
        records.append({"architecture": arch, "offset": offset, "size": size,
                        "members": members, "paddingOffset": offset + pad_start,
                        "paddingBytes": pad_size, **index})
    return records


def finalize(data: bytes) -> tuple[bytes, dict]:
    before = layout(data)
    result = bytearray(data)
    for item in before:
        start, size = item["paddingOffset"], item["paddingBytes"]
        result[start:start + size] = bytes(size)
    result = bytes(result)
    after = layout(result)
    # Independently compare all non-padding regions, including headers, fat
    # layout, ranlib entries, referenced strings and complete object payloads.
    cursor = 0
    for start, size in sorted((r["paddingOffset"], r["paddingBytes"]) for r in before):
        require(data[cursor:start] == result[cursor:start], "meaningful bytes changed")
        cursor = start + size
    require(data[cursor:] == result[cursor:] and len(data) == len(result), "meaningful bytes changed")
    for old, new in zip(before, after):
        require([m for m in old["members"] if m["type"] == "mach-o"] ==
                [m for m in new["members"] if m["type"] == "mach-o"], "object payload changed")
    return result, {"schemaVersion": 1, "inputSha256": sha(data), "outputSha256": sha(result),
                    "meaningfulBytesPreserved": True, "slices": before,
                    "initializedNonzeroBytes": sum(a != b for a, b in zip(data, result))}


def owned_path(root: Path, path: Path) -> Path:
    path = Path(os.path.abspath(path))
    require(path.is_relative_to(root), "path escapes build root")
    relative = path.relative_to(root)
    require(len(relative.parts) >= 2 and relative.parts[0] in ("work", "stage", "logs"),
            "path is not a native build product")
    current = root
    for part in relative.parts:
        current /= part
        require(not current.is_symlink(), "symlink in build product path")
        if current.exists():
            info = current.stat()
            require(info.st_uid == os.getuid(), "build path has a different owner")
            require(stat.S_ISDIR(info.st_mode) or (stat.S_ISREG(info.st_mode) and info.st_nlink == 1),
                    "nonregular or hardlinked build product")
    require(path.parent.is_dir(), "output parent must already exist")
    return path


def atomic_write(path: Path, content: bytes) -> None:
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(dir=path.parent, prefix=".native-index-", delete=False) as stream:
            temporary = Path(stream.name)
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary is not None and temporary.exists():
            temporary.unlink()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-root", required=True, type=Path)
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    args = parser.parse_args()
    root = args.build_root.resolve(strict=True)
    require(root != Path(root.anchor) and root.stat().st_uid == os.getuid(), "unsafe build root")
    state = root / "source-state.json"
    require(state.is_file() and not state.is_symlink(), "unmarked native build root")
    source, output, report = [owned_path(root, p) for p in (args.input, args.output, args.report)]
    require(len({source, output, report}) == 3, "input/output/report must be distinct")
    original = source.read_bytes()
    result, evidence = finalize(original)
    require(source.read_bytes() == original, "input changed during finalization")
    atomic_write(output, result)
    require(output.read_bytes() == result, "published archive differs from verified bytes")
    atomic_write(report, (json.dumps(evidence, indent=2, sort_keys=True) + "\n").encode())
    print(f"Finalized archive: {evidence['initializedNonzeroBytes']} padding bytes initialized; all meaningful bytes preserved")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ArchiveError, UnicodeError, OSError) as error:
        raise SystemExit(f"archive finalization failed: {error}")
