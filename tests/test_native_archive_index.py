"""Project-owned byte fixtures; no compiler, Apple tools or vendor objects."""
import importlib.util
import os
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "scripts/finalize-apple-archive.py"
spec = importlib.util.spec_from_file_location("archive_finalizer", SCRIPT)
producer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(producer)


def object_bytes(arch="arm64", names=(b"_Alpha", b"_Beta")):
    cpu, subtype = {"arm64": (0x100000c, 0), "arm64e": (0x100000c, 2),
                    "x86_64": (0x1000007, 3)}[arch]
    strings, symbols = b"\0", b""
    for name in names:
        symbols += struct.pack("<IBBHQ", len(strings), 3, 0, 0, 1)  # N_ABS | N_EXT
        strings += name + b"\0"
    return (struct.pack("<8I", 0xfeedfacf, cpu, subtype, 1, 1, 24, 0, 0) +
            struct.pack("<6I", 2, 24, 56, len(names), 56 + len(symbols), len(strings)) +
            symbols + strings)


def member(name, body):
    extended = name + bytes((-len(name)) % 8)
    data = extended + body
    fields = [b"#1/" + str(len(extended)).encode(), b"0", b"0", b"0", b"100644", str(len(data)).encode()]
    header = b"".join(value.ljust(size, b" ") for value, size in zip(fields, (16, 12, 6, 6, 8, 10))) + b"`\n"
    return header + data + (b"\n" if len(data) % 2 else b"")


def archive(arch="arm64", padding=0, names=(b"_Alpha", b"_Beta"), index_names=None, sorted_index=True):
    index_names = names if index_names is None else index_names
    strings, offsets = b"", []
    for name in index_names:
        offsets.append(len(strings))
        strings += name + b"\0"
    strings += bytes([padding]) * ((-len(strings)) % 8)
    def index(member_offset):
        table = b"".join(struct.pack("<II", x, member_offset) for x in offsets)
        return struct.pack("<I", len(table)) + table + struct.pack("<I", len(strings)) + strings
    index_name = b"__.SYMDEF SORTED" if sorted_index else b"__.SYMDEF"
    object_offset = 8 + len(member(index_name, index(0)))
    return b"!<arch>\n" + member(index_name, index(object_offset)) + member(b"owned.o", object_bytes(arch, names))


def fat(archives):
    header = struct.pack(">II", 0xcafebabe, len(archives))
    position = (8 + 20 * len(archives) + 7) // 8 * 8
    contents = bytes(position - (8 + 20 * len(archives)))
    for arch, data in archives:
        cpu, subtype = {"arm64": (0x100000c, 0), "arm64e": (0x100000c, 2),
                        "x86_64": (0x1000007, 3)}[arch]
        header += struct.pack(">5I", cpu, subtype, position, len(data), 3)
        contents += data
        position += len(data)
        if (arch, data) != archives[-1]:
            contents += bytes((-position) % 8)
            position = (position + 7) // 8 * 8
    return header + contents


class ArchiveIndexTests(unittest.TestCase):
    def test_zero_padding_and_unsorted_supported(self):
        for sorted_index in (False, True):
            original = archive(sorted_index=sorted_index)
            output, report = producer.finalize(original)
            self.assertEqual(output, original)
            self.assertEqual(report["initializedNonzeroBytes"], 0)

    def test_all_byte_patterns_and_idempotence(self):
        expected = archive()
        for value in range(256):
            with self.subTest(padding=value):
                result, report = producer.finalize(archive(padding=value))
                self.assertEqual(result, expected)
                self.assertEqual(producer.finalize(result)[0], result)
                self.assertTrue(report["meaningfulBytesPreserved"])

    def test_mixed_padding_bytes(self):
        source = bytearray(archive())
        layout = producer.layout(bytes(source))[0]
        start, size = layout["paddingOffset"], layout["paddingBytes"]
        self.assertEqual(size, 3)
        source[start:start + size] = b"\xbf\0\xaa"
        self.assertEqual(producer.finalize(bytes(source))[0], archive())

    def test_preserves_every_nonpadding_byte(self):
        source = archive(padding=0xaa)
        result, report = producer.finalize(source)
        padding = set()
        for s in report["slices"]:
            padding.update(range(s["paddingOffset"], s["paddingOffset"] + s["paddingBytes"]))
            for m in s["members"]:
                if m["type"] == "mach-o":
                    a, n = m["payloadOffset"], m["size"]
                    self.assertEqual(source[a:a+n], result[a:a+n])
        self.assertEqual(len(source), len(result))
        self.assertTrue(all(a == b for i, (a, b) in enumerate(zip(source, result)) if i not in padding))

    def test_empty_assembly_object_without_symbol_table(self):
        header = struct.pack("<8I", 0xfeedfacf, 0x100000c, 0, 1, 0, 0, 0, 0)
        self.assertEqual(producer.object_symbols(header), ("arm64", []))
        empty_index = struct.pack("<II", 0, 0)
        original = b"!<arch>\n" + member(b"__.SYMDEF", empty_index) + member(b"empty.o", header)
        self.assertEqual(producer.finalize(original)[0], original)

    def test_thin_and_fat_paths_include_arm64e(self):
        for archs in (("arm64",), ("x86_64", "arm64"), ("arm64", "arm64e")):
            parts = [(a, archive(a, padding=0xbf)) for a in archs]
            source = parts[0][1] if len(parts) == 1 else fat(parts)
            expected = archive(archs[0]) if len(parts) == 1 else fat([(a, archive(a)) for a in archs])
            self.assertEqual(producer.finalize(source)[0], expected)

    def test_truncation_and_invalid_lengths(self):
        original = archive()
        for end in (0, 4, 7, 8, 30, 67, 68, 87, 92, 104, len(original)-1):
            with self.subTest(end=end), self.assertRaises(producer.ArchiveError):
                producer.finalize(original[:end])
        layout = producer.layout(original)[0]
        base = layout["members"][0]["payloadOffset"]
        for relative, value in ((0, 7), (0, 0xfffffff8), (20, 0xffffffff)):
            bad = bytearray(original)
            struct.pack_into("<I", bad, base + relative, value)
            with self.subTest(relative=relative, value=value), self.assertRaises(producer.ArchiveError):
                producer.finalize(bytes(bad))

    def test_invalid_offsets_references_and_unterminated_strings(self):
        original = archive()
        base = producer.layout(original)[0]["members"][0]["payloadOffset"]
        for field, value in ((4, 99999), (4, 1), (8, 8), (8, 129), (8, 0xffffffff)):
            bad = bytearray(original)
            struct.pack_into("<I", bad, base + field, value)
            with self.subTest(field=field, value=value), self.assertRaises(producer.ArchiveError):
                producer.finalize(bytes(bad))
        bad = bytearray(original)
        bad[base + 24:base + 40] = b"X" * 16
        with self.assertRaisesRegex(producer.ArchiveError, "unterminated"):
            producer.finalize(bytes(bad))

    def test_referenced_content_is_never_padding(self):
        original = archive()
        base = producer.layout(original)[0]["members"][0]["payloadOffset"]
        for value in (0, ord("Z")):
            bad = bytearray(original)
            bad[base + 24 + 10] = value
            with self.subTest(value=value), self.assertRaises(producer.ArchiveError):
                producer.finalize(bytes(bad))
        for index_names in ((b"_Alpha",), (b"_Alpha", b"_Bet"), (b"_Alpha", b"_Alpha")):
            with self.subTest(index_names=index_names), self.assertRaisesRegex(producer.ArchiveError, "object definitions"):
                producer.finalize(archive(index_names=index_names))

    def test_legitimate_changed_symbol_is_preserved_not_normalized(self):
        changed = archive(names=(b"_Alpha", b"_Zeta"), padding=0xaa)
        result, report = producer.finalize(changed)
        self.assertEqual(result, archive(names=(b"_Alpha", b"_Zeta")))
        self.assertNotEqual(producer.sha(result), producer.sha(archive()))
        self.assertNotEqual(report["slices"][0]["members"][1]["sha256"],
                            producer.layout(archive())[0]["members"][1]["sha256"])

    def test_unsupported_and_ambiguous_formats_fail(self):
        for magic in (b"!<thin>\n", b"\xca\xfe\xba\xbf", b"\xbe\xba\xfe\xca", b"ELF!"):
            with self.subTest(magic=magic), self.assertRaises(producer.ArchiveError):
                producer.finalize(magic + archive()[len(magic):])
        for bad in (archive().replace(b"__.SYMDEF SORTED", b"__.SYMDEF_64    ", 1),
                    fat([("arm64", archive()), ("arm64", archive())]),
                    fat([("x86_64", archive())])):
            with self.assertRaises(producer.ArchiveError):
                producer.finalize(bad)

    def test_fat_bounds_overlap_and_architecture_mismatch(self):
        original = fat([("x86_64", archive("x86_64")), ("arm64", archive())])
        for field, value in ((16, 8), (20, 0xffffffff), (24, 31), (36, 48), (12, 2)):
            bad = bytearray(original)
            struct.pack_into(">I", bad, field, value)
            with self.subTest(field=field), self.assertRaises(producer.ArchiveError):
                producer.finalize(bytes(bad))

    def test_safe_cli_and_failure_preserves_existing_output(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            (root / "source-state.json").write_text("{}")
            for name in ("work", "stage", "logs"):
                (root / name).mkdir()
            source, output, report = root / "work/input.a", root / "stage/output.a", root / "logs/report.json"
            source.write_bytes(archive(padding=0xaa))
            command = [sys.executable, str(SCRIPT), "--build-root", str(root), "--input", str(source),
                       "--output", str(output), "--report", str(report)]
            result = subprocess.run(command, capture_output=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(output.read_bytes(), archive())
            source.write_bytes(b"malformed")
            self.assertNotEqual(subprocess.run(command, capture_output=True).returncode, 0)
            self.assertEqual(output.read_bytes(), archive())
            source.unlink()
            source.symlink_to(output)
            self.assertNotEqual(subprocess.run(command, capture_output=True).returncode, 0)
            source.unlink()
            os.link(output, source)
            self.assertNotEqual(subprocess.run(command, capture_output=True).returncode, 0)
            with self.assertRaises(producer.ArchiveError):
                producer.owned_path(root, root.parent / "escape.a")
            with self.assertRaises(producer.ArchiveError):
                producer.owned_path(root, root / "sources/input.a")


if __name__ == "__main__":
    unittest.main()
