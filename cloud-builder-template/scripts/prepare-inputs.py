#!/usr/bin/env python3
"""Bounded input and metadata helpers for the private tvOS cloud builder."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import pathlib
import re
import shutil
import stat
import subprocess
import sys
import zipfile


RELEASE_ASSET_LIMIT = 2 * 1024**3
MAX_ZIP_ENTRIES = 200_000
MAX_ZIP_MEMBER_BYTES = 2 * 1024**3
MAX_ZIP_TOTAL_BYTES = 4 * 1024**3
SHA256_DIGEST = re.compile(r"sha256:([0-9a-f]{64})")


class InputError(RuntimeError):
    pass


def sha256_file(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def repository_is_private(metadata: dict) -> bool:
    return metadata.get("private") is True and metadata.get("visibility") == "private"


def _safe_asset_name(name: object) -> str:
    if not isinstance(name, str) or not name:
        raise InputError("a Release asset has no usable filename")
    if pathlib.PurePosixPath(name).name != name or "\\" in name:
        raise InputError("Release asset filenames must not contain directories")
    if any(ord(character) < 32 or ord(character) == 127 for character in name):
        raise InputError("Release asset filenames must not contain control characters")
    return name


def classify_release_assets(release: dict) -> dict:
    if release.get("isDraft") is True:
        raise InputError("the celeste-tvos-inputs Release is still a draft; publish it first")
    assets = release.get("assets")
    if not isinstance(assets, list):
        raise InputError("the input Release asset list is unavailable")
    if len(assets) != 2:
        raise InputError(
            "celeste-tvos-inputs must contain exactly two assets: one Celeste ZIP and one FMOD DMG"
        )

    classified: dict[str, dict] = {}
    seen: set[str] = set()
    for raw in assets:
        if not isinstance(raw, dict):
            raise InputError("the input Release contains malformed asset metadata")
        name = _safe_asset_name(raw.get("name"))
        folded = name.casefold()
        if folded in seen:
            raise InputError("the input Release contains duplicate asset filenames")
        seen.add(folded)
        suffix = pathlib.PurePosixPath(name).suffix.lower()
        kind = {".zip": "game", ".dmg": "fmod"}.get(suffix)
        if kind is None:
            raise InputError("only one .zip and one .dmg asset are allowed")
        if kind in classified:
            raise InputError(f"the input Release contains more than one {suffix} asset")
        size = raw.get("size")
        if not isinstance(size, int) or isinstance(size, bool) or size <= 0:
            raise InputError(f"the {suffix} asset has an invalid byte size")
        if size >= RELEASE_ASSET_LIMIT:
            raise InputError(f"the {suffix} asset must be smaller than GitHub's 2 GiB per-file limit")
        digest = raw.get("digest") or ""
        if digest and not SHA256_DIGEST.fullmatch(digest):
            raise InputError(f"the {suffix} asset has an unsupported server digest")
        classified[kind] = {
            "name": name,
            "size": size,
            "serverDigest": digest,
        }

    if set(classified) != {"game", "fmod"}:
        raise InputError("celeste-tvos-inputs must contain one .zip and one .dmg")
    return {
        "schemaVersion": 1,
        "releaseTag": "celeste-tvos-inputs",
        "game": classified["game"],
        "fmod": classified["fmod"],
    }


def verify_downloaded_assets(manifest: dict, directory: pathlib.Path) -> dict:
    expected_names = {manifest[kind]["name"] for kind in ("game", "fmod")}
    actual = {path.name for path in directory.iterdir() if path.is_file()}
    if actual != expected_names:
        raise InputError("downloaded input assets do not exactly match the validated Release")
    result = json.loads(json.dumps(manifest))
    for kind in ("game", "fmod"):
        item = result[kind]
        path = directory / item["name"]
        size = path.stat().st_size
        if size != item["size"]:
            raise InputError(f"downloaded {kind} asset size differs from GitHub metadata")
        digest = sha256_file(path)
        server_digest = item.get("serverDigest", "")
        if server_digest and digest != SHA256_DIGEST.fullmatch(server_digest).group(1):
            raise InputError(f"downloaded {kind} asset SHA-256 differs from GitHub metadata")
        item["sha256"] = digest
        item["path"] = str(path.resolve())
    return result


def _zip_parts(name: str) -> tuple[str, ...]:
    if not name or "\\" in name or "\x00" in name:
        raise InputError("ZIP contains an unsafe or ambiguous path")
    if any(ord(character) < 32 or ord(character) == 127 for character in name):
        raise InputError("ZIP paths must not contain control characters")
    pure = pathlib.PurePosixPath(name)
    if pure.is_absolute() or not pure.parts or any(part in ("", ".", "..") for part in pure.parts):
        raise InputError("ZIP contains an absolute or parent-traversal path")
    return pure.parts


def _zip_entry_kind(info: zipfile.ZipInfo) -> str:
    mode = info.external_attr >> 16
    file_type = stat.S_IFMT(mode)
    if info.is_dir() or file_type == stat.S_IFDIR:
        return "directory"
    if file_type in (0, stat.S_IFREG):
        return "file"
    if file_type == stat.S_IFLNK:
        raise InputError("ZIP symbolic links are forbidden")
    raise InputError("ZIP special files are forbidden")


def inspect_zip(archive: pathlib.Path) -> list[tuple[zipfile.ZipInfo, tuple[str, ...], str]]:
    entries: list[tuple[zipfile.ZipInfo, tuple[str, ...], str]] = []
    normalized: set[str] = set()
    total = 0
    try:
        bundle = zipfile.ZipFile(archive)
    except (OSError, zipfile.BadZipFile) as error:
        raise InputError("the Celeste ZIP is not a readable ZIP archive") from error
    with bundle:
        infos = bundle.infolist()
        if not infos or len(infos) > MAX_ZIP_ENTRIES:
            raise InputError("the Celeste ZIP has an invalid or excessive entry count")
        for info in infos:
            parts = _zip_parts(info.filename)
            folded = "/".join(parts).casefold().rstrip("/")
            if folded in normalized:
                raise InputError("the Celeste ZIP has duplicate or case-colliding paths")
            normalized.add(folded)
            kind = _zip_entry_kind(info)
            if info.flag_bits & 0x1:
                raise InputError("encrypted ZIP entries are not supported")
            if info.file_size < 0 or info.file_size > MAX_ZIP_MEMBER_BYTES:
                raise InputError("the Celeste ZIP contains an oversized member")
            total += info.file_size
            if total > MAX_ZIP_TOTAL_BYTES:
                raise InputError("the Celeste ZIP expands beyond the 4 GiB safety limit")
            entries.append((info, parts, kind))
    return entries


def safe_extract_zip(archive: pathlib.Path, destination: pathlib.Path) -> dict:
    entries = inspect_zip(archive)
    destination.mkdir(parents=True, exist_ok=False)
    destination_resolved = destination.resolve()
    total = 0
    with zipfile.ZipFile(archive) as bundle:
        for info, parts, kind in entries:
            target = destination.joinpath(*parts)
            resolved_parent = target.parent.resolve()
            try:
                resolved_parent.relative_to(destination_resolved)
            except ValueError as error:
                raise InputError("ZIP extraction would escape the temporary root") from error
            if kind == "directory":
                target.mkdir(parents=True, exist_ok=True)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            with bundle.open(info) as source, target.open("xb") as output:
                written = 0
                while True:
                    block = source.read(1024 * 1024)
                    if not block:
                        break
                    written += len(block)
                    if written > info.file_size:
                        raise InputError("ZIP member expanded beyond its declared size")
                    output.write(block)
            if written != info.file_size:
                raise InputError("ZIP member size differs after extraction")
            total += written
    return {"entries": len(entries), "uncompressedBytes": total, "root": str(destination_resolved)}


def find_fmod_root(mount: pathlib.Path) -> pathlib.Path:
    mount = mount.resolve()
    candidates: list[pathlib.Path] = []
    queue: list[tuple[pathlib.Path, int]] = [(mount, 0)]
    while queue:
        current, depth = queue.pop(0)
        if current.is_symlink():
            continue
        if (current / "doc/revision.txt").is_file() and (
            current / "api/lowlevel/lib/libfmod_appletvos.a"
        ).is_file():
            candidates.append(current)
            continue
        if depth >= 3:
            continue
        for child in sorted(current.iterdir(), key=lambda path: path.name):
            if child.is_dir() and not child.is_symlink():
                queue.append((child, depth + 1))
    if len(candidates) != 1:
        raise InputError(f"expected one FMOD SDK root in the mounted DMG, found {len(candidates)}")
    return candidates[0]


def free_bytes(path: pathlib.Path) -> int:
    stats = os.statvfs(path)
    return stats.f_bavail * stats.f_frsize


def disk_is_sufficient(available: int, minimum: int) -> bool:
    return available >= minimum


def verify_source(root: pathlib.Path, expected_sha: str) -> None:
    actual = subprocess.check_output(
        ["git", "-C", str(root), "rev-parse", "HEAD"], text=True
    ).strip()
    if actual != expected_sha:
        raise InputError(f"public source checkout is {actual}, expected {expected_sha}")
    status = subprocess.check_output(
        ["git", "-C", str(root), "status", "--short"], text=True
    )
    if status:
        raise InputError("public source checkout is not clean")


def write_json(path: pathlib.Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    privacy = subparsers.add_parser("privacy-check")
    privacy.add_argument("--metadata", required=True, type=pathlib.Path)

    classify = subparsers.add_parser("classify-assets")
    classify.add_argument("--release-json", required=True, type=pathlib.Path)
    classify.add_argument("--output", required=True, type=pathlib.Path)

    verify = subparsers.add_parser("verify-downloads")
    verify.add_argument("--manifest", required=True, type=pathlib.Path)
    verify.add_argument("--directory", required=True, type=pathlib.Path)
    verify.add_argument("--output", required=True, type=pathlib.Path)

    extract = subparsers.add_parser("extract-zip")
    extract.add_argument("--archive", required=True, type=pathlib.Path)
    extract.add_argument("--destination", required=True, type=pathlib.Path)
    extract.add_argument("--output", type=pathlib.Path)

    fmod = subparsers.add_parser("find-fmod-root")
    fmod.add_argument("--mount", required=True, type=pathlib.Path)

    disk = subparsers.add_parser("check-disk")
    disk.add_argument("--path", required=True, type=pathlib.Path)
    disk.add_argument("--minimum-gib", required=True, type=int)

    source = subparsers.add_parser("verify-source")
    source.add_argument("--root", required=True, type=pathlib.Path)
    source.add_argument("--sha", required=True)

    args = parser.parse_args()
    try:
        if args.command == "privacy-check":
            if not repository_is_private(json.loads(args.metadata.read_text(encoding="utf-8"))):
                raise InputError("repository is not private")
        elif args.command == "classify-assets":
            write_json(args.output, classify_release_assets(json.loads(args.release_json.read_text())))
        elif args.command == "verify-downloads":
            manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
            write_json(args.output, verify_downloaded_assets(manifest, args.directory.resolve()))
        elif args.command == "extract-zip":
            result = safe_extract_zip(args.archive.resolve(), args.destination)
            if args.output:
                write_json(args.output, result)
            print(f"Safe ZIP extraction: {result['entries']} entries, {result['uncompressedBytes']} bytes")
        elif args.command == "find-fmod-root":
            print(find_fmod_root(args.mount))
        elif args.command == "check-disk":
            minimum = args.minimum_gib * 1024**3
            available = free_bytes(args.path)
            print(f"Runner disk: {available // 1024**3} GiB available; {args.minimum_gib} GiB required")
            if not disk_is_sufficient(available, minimum):
                raise InputError(
                    "the current GitHub runner image does not provide enough free space for this build"
                )
        elif args.command == "verify-source":
            if not re.fullmatch(r"[0-9a-f]{40}", args.sha):
                raise InputError("expected source SHA is malformed")
            verify_source(args.root.resolve(), args.sha)
        return 0
    except (InputError, OSError, ValueError, zipfile.BadZipFile, json.JSONDecodeError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
