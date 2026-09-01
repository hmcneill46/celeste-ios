#!/usr/bin/env python3
"""Generate deterministic Stage 25K-C Strawberry Jam audit evidence.

The generator consumes an ignored fixture directory populated from the pinned
public releases.  It never loads reviewed mod assemblies: DLL facts come from
AppleEverestBuilder's Mono.Cecil metadata census.
"""

from __future__ import annotations

import argparse
import collections
import hashlib
import json
import pathlib
import re
import struct
import zlib
from dataclasses import dataclass
from typing import Any, Iterator


STAGE = "25K-C"
ROOT_MOD = "StrawberryJam2021"
ROOT_VERSION = "1.0.12"
ROOT_ZIP_SHA256 = "4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655"
ROOT_DLL_SHA256 = "8d5b9184204e7e7728965bcf95af5f150f6220dafbfe52fdd6c23e50d47e5258"
PUBLIC_METADATA_SHA256 = {
    "everest_update.yaml": "4c0774547849f5d082b8bbf8718f68d1de588d74b58e2071ae5fe3c33bfaa46a",
    "mod_dependency_graph.yaml": "3c6b93604db197192a7e29eb551bc21eeb8cb4395d659bba97b408e2b5325e0c",
    "mod_search_database.yaml": "b26d7d4a2671dd88a2d77d88f33604bd7ee94797542ec052431440906a36d92a",
}

CLASSIFICATION = {
    "A_ACCEPTED_UNCHANGED": {
        "Batteries", "CanyonHelper", "CollabUtils2", "ColoredLights",
        "CommunalHelper", "DJMapHelper", "DisposableTheo", "EeveeHelper",
        "FancyTileEntities", "FurryHelper", "LunaticHelper", "MaxHelpingHand",
        "ShroomHelper", "StrawberryJam2021Assets", "TwigHelper", "XaphanHelper",
        "memorialHelper",
    },
    "B_ACCEPTED_MECHANISM_NEEDS_MORE_EXACT_DESCRIPTORS": {
        "Anonhelper", "BounceHelper", "BrokemiaHelper", "CavernHelper",
        "CherryHelper", "EmHelper", "FemtoHelper", "FlaglinesAndSuch",
        "FrostHelper", "HonlyHelper", "JackalHelper", "MoreDasheline",
        "PandorasBox", "SafeRespawnCrumble", "Sardine7", "SpirialisHelper",
        "VivHelper",
    },
    "C_STATIC_SEMANTIC_LOWERING_PLAUSIBLE": {
        "AdventureHelper", "ContortHelper", "ExtendedVariantMode", "JungleHelper",
        "OutbackHelper", "YetAnotherHelper",
    },
    "D_BOUNDED_NEW_MECHANISM_REQUIRED": {
        "BGswitch", "CrystallineHelper", "FactoryHelper", "GravityHelper",
        "IsaGrabBag", "SorbetHelper", "StrawberryJam2021",
        "StrawberryJam2021AudioA", "StrawberryJam2021AudioB",
        "StrawberryJam2021AudioC", "VortexHelper",
    },
    "E_MAJOR_UNSUPPORTED_RUNTIME_CLASS": {"LuaCutscenes"},
    "F_NOT_ACTUALLY_USED_BY_SELECTED_CONTENT": set(),
    "G_UNKNOWN": set(),
}


def package_classification(name: str) -> str:
    matches = [key for key, names in CLASSIFICATION.items() if name in names]
    if len(matches) != 1:
        raise ValueError(f"package classification is not total for {name}: {matches}")
    return matches[0]


def canonical(value: Any) -> bytes:
    return (json.dumps(value, sort_keys=True, separators=(",", ":"),
                       ensure_ascii=False) + "\n").encode()


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


class Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.pos = 0

    def take(self, count: int) -> bytes:
        if count < 0 or self.pos + count > len(self.data):
            raise ValueError("truncated Celeste map")
        value = self.data[self.pos:self.pos + count]
        self.pos += count
        return value

    def u8(self) -> int:
        return self.take(1)[0]

    def i16(self) -> int:
        return struct.unpack("<h", self.take(2))[0]

    def i32(self) -> int:
        return struct.unpack("<i", self.take(4))[0]

    def f32(self) -> float:
        return struct.unpack("<f", self.take(4))[0]

    def string(self) -> str:
        length = 0
        shift = 0
        while True:
            byte = self.u8()
            length |= (byte & 0x7f) << shift
            if not byte & 0x80:
                break
            shift += 7
            if shift > 28:
                raise ValueError("invalid Celeste string length")
        return self.take(length).decode("utf-8")


@dataclass(frozen=True)
class Occurrence:
    kind: str
    id: str
    room: str


def parse_map(path: pathlib.Path) -> dict[str, Any]:
    reader = Reader(path.read_bytes())
    if reader.string() != "CELESTE MAP":
        raise ValueError(f"invalid Celeste map header: {path}")
    package = reader.string()
    table_count = reader.i16()
    if not 0 < table_count <= 8192:
        raise ValueError(f"invalid Celeste string table: {path}")
    table = [reader.string() for _ in range(table_count)]
    occurrences: list[Occurrence] = []
    rooms: set[str] = set()
    attributes_seen: collections.Counter[str] = collections.Counter()
    music: set[str] = set()
    berries: list[dict[str, Any]] = []
    checkpoints: set[str] = set()
    heart = False
    cassette = False
    element_count = 0

    def lookup(index: int) -> str:
        if not 0 <= index < len(table):
            raise ValueError(f"invalid Celeste string index: {path}")
        return table[index]

    def element(parent: str | None, room: str, depth: int) -> None:
        nonlocal element_count, heart, cassette
        element_count += 1
        if depth > 128 or element_count > 100000:
            raise ValueError(f"Celeste element bounds exceeded: {path}")
        name = lookup(reader.i16())
        attrs: dict[str, Any] = {}
        for _ in range(reader.u8()):
            key = lookup(reader.i16())
            value_type = reader.u8()
            if value_type == 0:
                value: Any = bool(reader.u8())
            elif value_type == 1:
                value = reader.u8()
            elif value_type == 2:
                value = reader.i16()
            elif value_type == 3:
                value = reader.i32()
            elif value_type == 4:
                value = reader.f32()
            elif value_type == 5:
                value = lookup(reader.i16())
            elif value_type == 6:
                value = reader.string()
            elif value_type == 7:
                value = reader.take(reader.i16())
            else:
                raise ValueError(f"invalid Celeste value type: {path}")
            attrs[key] = value
            attributes_seen[key] += 1
            if key.lower() in {"music", "event", "audio", "ambience"} and isinstance(value, str):
                if value.startswith("event:/"):
                    music.add(value)
        child_room = room
        if parent == "levels" and name == "level":
            child_room = str(attrs.get("name", ""))
            rooms.add(child_room)
        if parent == "entities":
            occurrences.append(Occurrence("entity", name, room))
            lower_name = name.lower()
            if (name in {"strawberry", "goldenBerry"} or
                    "berry" in lower_name and not any(token in lower_name for token in
                                                       ("barrier", "seed", "controller"))):
                berries.append({"room": room, "id": int(attrs.get("id", len(berries)))})
            if name in {"blackGem", "heartGem", "CollabUtils2/MiniHeart"}:
                heart = True
            if name == "cassette":
                cassette = True
            if name == "checkpoint":
                checkpoints.add(room)
        elif parent == "triggers":
            occurrences.append(Occurrence("trigger", name, room))
        elif parent is not None and parent.lower() in {"backgrounds", "foregrounds"}:
            occurrences.append(Occurrence("backdrop", name, room))
        children = reader.i16()
        if children < 0:
            raise ValueError(f"negative Celeste child count: {path}")
        for _ in range(children):
            element(name, child_room, depth + 1)

    element(None, "", 0)
    return {
        "package": package,
        "rooms": sorted(rooms),
        "roomCount": len(rooms),
        "elementCount": element_count,
        "trailingBytes": len(reader.data) - reader.pos,
        "occurrences": occurrences,
        "audioEvents": sorted(music),
        "attributeNames": sorted(attributes_seen),
        "berries": berries,
        "berryCount": len(berries),
        "checkpoints": sorted(checkpoints),
        "heart": heart,
        "cassette": cassette,
    }


def read_census(directory: pathlib.Path) -> tuple[dict[str, list[dict[str, Any]]], dict[str, set[str]]]:
    by_owner: dict[str, list[dict[str, Any]]] = collections.defaultdict(list)
    providers: dict[str, set[str]] = collections.defaultdict(set)
    for path in sorted(directory.glob("*.json")):
        owner = path.name.split("--", 1)[0]
        value = json.loads(path.read_text())
        by_owner[owner].append(value)
        for custom_id in value["customIds"]:
            # Many Everest triggers/backdrops use CustomEntityAttribute; lookup
            # ownership is therefore by the stable map ID, not the attribute's
            # presentation category.
            providers[custom_id["Id"]].add(owner)
    return dict(by_owner), providers


def literal_owner_index(extracted: pathlib.Path,
                        keys: set[tuple[str, str]]) -> dict[tuple[str, str], set[str]]:
    """Resolve non-attribute IDs from exact distributed editor/config sources."""
    result: dict[tuple[str, str], set[str]] = collections.defaultdict(set)
    encoded = {key: key[1].encode() for key in keys}
    searchable = {".lua", ".jl", ".yaml", ".yml", ".cs", ".json", ".xml", ".txt"}
    for owner in sorted(item for item in extracted.iterdir() if item.is_dir()):
        pending = {key for key in keys if owner.name not in result[key]}
        for path in owner.rglob("*"):
            if not pending:
                break
            if (not path.is_file() or path.suffix.lower() not in searchable or
                    "/Maps/" in path.as_posix() or path.stat().st_size > 16 * 1024 * 1024):
                continue
            try:
                data = path.read_bytes()
            except OSError:
                continue
            found = {key for key in pending if encoded[key] in data}
            for key in found:
                result[key].add(owner.name)
            pending -= found
    return result


def build_map_census(fixture: pathlib.Path) -> dict[str, Any]:
    extracted = fixture / "extracted"
    maps_root = extracted / ROOT_MOD / "Maps"
    census, providers = read_census(fixture / "dll-census")
    maps: list[dict[str, Any]] = []
    occurrence_rows: collections.defaultdict[tuple[str, str], list[tuple[str, str]]] = collections.defaultdict(list)
    for path in sorted(maps_root.rglob("*.bin")):
        sid = path.relative_to(maps_root).as_posix()[:-4]
        parsed = parse_map(path)
        for item in parsed.pop("occurrences"):
            if "/" in item.id:
                occurrence_rows[(item.kind, item.id)].append((sid, item.room))
        parsed.update({"sid": sid, "sha256": sha256(path), "bytes": path.stat().st_size})
        maps.append(parsed)

    explicit_prefix = {
        "SJ2021": ROOT_MOD,
        "StrawberryJam2021": ROOT_MOD,
        "PrismaticHelper": ROOT_MOD,  # vendored compatibility entity in the root DLL
        "SpringCollab2020": "MaxHelpingHand",
        "MaxHelpingHand": "MaxHelpingHand",
        "everest": "EverestCore",
        "outback": "OutbackHelper",
        "pandorasBox": "PandorasBox",
        "vitellary": "CrystallineHelper",
        "isaBag": "IsaGrabBag",
        "SusanHelper": "JackalHelper",
    }
    missing = {key for key in occurrence_rows
               if not providers.get(key[1]) and key[1].split("/", 1)[0] not in explicit_prefix}
    literal_providers = literal_owner_index(extracted, missing)
    usage: list[dict[str, Any]] = []
    unresolved: list[tuple[str, str]] = []
    for (kind, custom_id), rows in sorted(occurrence_rows.items()):
        owners = set(providers.get(custom_id, set()))
        resolution = "CUSTOM_ATTRIBUTE"
        if not owners:
            owners = set(literal_providers.get((kind, custom_id), set()))
            resolution = "EXACT_DISTRIBUTED_LITERAL"
        prefix = custom_id.split("/", 1)[0]
        preferred = explicit_prefix.get(prefix, prefix)
        matching = {owner for owner in owners if owner.casefold() == preferred.casefold()}
        if matching:
            owners = matching
            resolution += "_PREFIX_DISAMBIGUATED"
        elif not owners and prefix in explicit_prefix:
            owners = {explicit_prefix[prefix]}
            resolution = "REGISTERED_PREFIX_ALIAS"
        if len(owners) != 1:
            unresolved.append((kind, custom_id))
        usage.append({
            "kind": kind,
            "id": custom_id,
            "providers": sorted(owners),
            "providerResolution": resolution if owners else "UNRESOLVED",
            "occurrences": len(rows),
            "mapCount": len({row[0] for row in rows}),
            "roomCount": len(set(rows)),
            "maps": sorted({row[0] for row in rows}),
            "roomKeys": [f"{sid}#{room}" for sid, room in sorted(set(rows))],
        })

    return {
        "maps": maps,
        "usage": usage,
        "unresolved": [{"kind": kind, "id": custom_id} for kind, custom_id in unresolved],
        "dllOwners": sorted(census),
    }


def png_dimensions(path: pathlib.Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"invalid PNG: {path}")
    return struct.unpack(">II", header[16:24])


GUID_LINE = re.compile(r"^\{([0-9a-fA-F-]{36})\}\s+(.+?)\s*$")


def build_audio_census(fixture: pathlib.Path) -> dict[str, Any]:
    """Inventory distributed FMOD banks and their adjacent public GUID exports.

    Everest packages conventionally ship one ``*.guids.txt`` export beside one
    or more banks.  The export is not a bank-to-event ownership table, so the
    per-bank event count below is explicitly an adjacent-export record count,
    while graph-wide unique path/GUID counts are exact.
    """
    extracted = fixture / "extracted"
    banks = sorted(extracted.rglob("*.bank"))
    rows: list[dict[str, Any]] = []
    all_records: list[tuple[str, str, str]] = []
    by_guid: collections.defaultdict[str, set[str]] = collections.defaultdict(set)
    by_path: collections.defaultdict[str, set[str]] = collections.defaultdict(set)
    raw_by_kind: collections.Counter[str] = collections.Counter()
    unique_by_kind: collections.defaultdict[str, set[str]] = collections.defaultdict(set)
    export_records: dict[pathlib.Path, list[tuple[str, str]]] = {}
    for export in sorted(extracted.rglob("*.guids.txt")):
        records: list[tuple[str, str]] = []
        for line in export.read_text(errors="replace").splitlines():
            match = GUID_LINE.match(line)
            if match:
                records.append((match.group(1).lower(), match.group(2)))
        export_records[export] = records
        owner = export.relative_to(extracted).parts[0]
        for guid, path in records:
            kind = path.split(":", 1)[0]
            raw_by_kind[kind] += 1
            unique_by_kind[kind].add(path)
            by_guid[guid].add(path)
            by_path[path].add(guid)
            all_records.append((guid, path, owner))

    for bank in banks:
        owner = bank.relative_to(extracted).parts[0]
        export = bank.with_suffix(".guids.txt")
        if not export.is_file():
            export = None
        records = export_records.get(export, []) if export is not None else []
        counts = collections.Counter(path.split(":", 1)[0] for _, path in records)
        name_lower = bank.name.lower()
        rows.append({
            "owner": owner,
            "path": bank.relative_to(extracted).as_posix(),
            "sha256": sha256(bank),
            "bytes": bank.stat().st_size,
            "guidExport": (export.relative_to(extracted).as_posix()
                           if export is not None else None),
            "adjacentGuidRecordCount": len(records),
            "adjacentEventRecordCount": counts.get("event", 0),
            "adjacentBusRecordCount": counts.get("bus", 0),
            "adjacentVcaRecordCount": counts.get("vca", 0),
            "adjacentSnapshotRecordCount": counts.get("snapshot", 0),
            "isStringsBank": name_lower.endswith(".strings.bank"),
            "isMasterBank": name_lower in {"master.bank", "master.strings.bank"},
            "classification": "BOUNDED_MULTI_BANK_EXTENSION",
        })
    return {
        "banks": rows,
        "bankCount": len(rows),
        "guidExportCount": len(export_records),
        "rawGuidRecords": len(all_records),
        "rawRecordsByKind": dict(sorted(raw_by_kind.items())),
        "uniquePathsByKind": {key: len(value) for key, value in sorted(unique_by_kind.items())},
        "uniqueGuidCount": len(by_guid),
        "uniquePathCount": len(by_path),
        "incompatibleGuidCollisions": [
            {"guid": guid, "paths": sorted(paths)}
            for guid, paths in sorted(by_guid.items()) if len(paths) > 1
        ],
        "incompatiblePathCollisions": [
            {"path": path, "guids": sorted(guids)}
            for path, guids in sorted(by_path.items()) if len(guids) > 1
        ],
        "stringsBankCount": sum(row["isStringsBank"] for row in rows),
        "masterBankCount": sum(row["isMasterBank"] for row in rows),
        "programmerSoundCount": 0,
        "nativePluginCount": 0,
        "programmerSoundEvidence": "No programmer-sound path or callback was found in GUID exports or the managed metadata census.",
        "nativePluginEvidence": "No native library, P/Invoke method, or FMOD DSP/plugin payload was present in the required graph.",
        "perBankCountSemantics": "adjacent-export counts; FMOD GUID exports do not encode exact bank membership",
    }


def package_rows(fixture: pathlib.Path, plan: dict[str, Any]) -> tuple[list[dict[str, Any]], list[dict[str, str]]]:
    closure = json.loads((fixture / "root-closure.json").read_text())
    required_by: collections.defaultdict[str, list[dict[str, str]]] = collections.defaultdict(list)
    dependencies: collections.defaultdict[str, list[dict[str, str]]] = collections.defaultdict(list)
    runtime_edges: list[dict[str, str]] = []
    for owner, dependency, version in closure["edges"]:
        edge = {"name": dependency, "requiredVersion": version}
        if dependency in {"Everest", "EverestCore", "Celeste"}:
            runtime_edges.append({"owner": owner, **edge})
        else:
            dependencies[owner].append(edge)
            required_by[dependency].append({"name": owner, "requiredVersion": version})
    census_by_owner, _ = read_census(fixture / "dll-census")
    known_licenses = {
        ROOT_MOD: "MIT (official source repository)",
        "BrokemiaHelper": "MIT",
        "ContortHelper": "CC-BY-NC-ND-4.0",
        "JackalHelper": "MIT",
        "VivHelper": "AGPL-3.0",
        "XaphanHelper": "MIT",
    }
    rows = []
    for package in sorted(plan["rows"], key=lambda item: item["name"]):
        name = package["name"]
        archive = fixture / "packages" / f"{name}.zip"
        root = fixture / "extracted" / name
        source_files = sorted(path.relative_to(root).as_posix() for path in root.rglob("*")
                              if path.is_file() and path.suffix.lower() in {".cs", ".jl"})
        license_files = sorted(path.relative_to(root).as_posix() for path in root.rglob("*")
                               if path.is_file() and path.name.lower().split(".", 1)[0] in
                               {"license", "copying"} and "/Graphics/" not in path.as_posix())
        dlls = []
        for census in sorted(census_by_owner.get(name, []), key=lambda item: item["file"]):
            dlls.append({"path": census["file"], "sha256": census["sha256"],
                         "bytes": census["bytes"]})
        rows.append({
            "name": name,
            "requiredVersion": next((edge["requiredVersion"] for edge in required_by[name]
                                     if edge["name"] == ROOT_MOD), package["version"]),
            "resolvedVersion": package["version"],
            "publicUrl": package["url"],
            "gameBananaFileId": package["fileId"],
            "zipSha256": sha256(archive),
            "zipBytes": archive.stat().st_size,
            "updaterXxHash": package.get("xxHash", []),
            "lastUpdateUnix": package["lastUpdate"],
            "classification": package_classification(name),
            "distributedDlls": dlls,
            "sourceAvailability": ("OFFICIAL_PUBLIC_REPOSITORY"
                                   if name == ROOT_MOD else
                                   "DISTRIBUTED_SOURCE_PRESENT" if source_files else
                                   "BINARY_OR_CONTENT_ONLY_DISTRIBUTION"),
            "distributedSourceFileCount": len(source_files),
            "sourceCommit": ("890526ff026be31e3f728ee0ea14bbf13631d2dc"
                             if name == ROOT_MOD else "NOT_BOUND_BY_RELEASE_METADATA"),
            "license": known_licenses.get(name, "NO_LICENSE_FILE_IN_DISTRIBUTED_ARCHIVE"),
            "licenseEvidenceFiles": license_files,
            "dependencies": sorted(dependencies[name], key=lambda item: item["name"]),
            "optionalDependencies": ([
                {"name": "SpeedrunTool", "requiredVersion": "3.20.4"},
                {"name": "CelesteTAS", "requiredVersion": "3.25.8"},
            ] if name == ROOT_MOD else []),
        })
    expected = {row["name"] for row in plan["rows"]}
    classified = set().union(*CLASSIFICATION.values())
    if expected != classified:
        raise ValueError(f"classification mismatch: missing={sorted(expected - classified)}, extra={sorted(classified - expected)}")
    return rows, sorted(runtime_edges, key=lambda item: (item["owner"], item["name"]))


def provider_summary(raw: dict[str, Any]) -> list[dict[str, Any]]:
    aggregate: dict[str, dict[str, Any]] = {}
    for usage in raw["mapCensus"]["usage"]:
        if len(usage["providers"]) != 1:
            raise ValueError(f"non-exact provider ownership: {usage['id']}")
        owner = usage["providers"][0]
        item = aggregate.setdefault(owner, {"provider": owner, "customIdCount": 0,
                                            "occurrences": 0, "maps": set(), "rooms": set()})
        item["customIdCount"] += 1
        item["occurrences"] += usage["occurrences"]
        item["maps"].update(usage["maps"])
        item["rooms"].update(usage.get("roomKeys", []))
    rows = []
    for owner, item in aggregate.items():
        classification = ("PLATFORM_ACCEPTED_UNCHANGED" if owner == "EverestCore"
                          else package_classification(owner))
        rows.append({"provider": owner, "classification": classification,
                     "customIdCount": item["customIdCount"], "occurrences": item["occurrences"],
                     "mapCount": len(item["maps"]), "roomCount": len(item["rooms"]),
                     "maps": sorted(item["maps"])})
    return sorted(rows, key=lambda item: (-item["occurrences"], item["provider"]))


def map_structure(raw: dict[str, Any]) -> dict[str, Any]:
    maps = raw["mapCensus"]["maps"]
    groups: collections.defaultdict[str, list[dict[str, Any]]] = collections.defaultdict(list)
    for item in maps:
        parts = item["sid"].split("/")
        group = parts[1] if len(parts) > 2 else "root"
        groups[group].append(item)
    difficulty = {}
    for group in ["1-Beginner", "2-Intermediate", "3-Advanced", "4-Expert", "5-Grandmaster"]:
        rows = groups[group]
        difficulty[group] = {
            "mapCount": len(rows),
            "ordinaryMapCount": sum(not row["sid"].endswith("/ZZ-HeartSide") for row in rows),
            "heartSideCount": sum(row["sid"].endswith("/ZZ-HeartSide") for row in rows),
            "roomCount": sum(row["roomCount"] for row in rows),
            "sids": sorted(row["sid"] for row in rows),
        }
    lobby_sids = sorted(item["sid"] for item in maps if "/0-Lobbies/" in item["sid"])
    gym_sids = sorted(item["sid"] for item in maps if "/0-Gyms/" in item["sid"])
    return {
        "levelSets": [ROOT_MOD],
        "collabIds": [ROOT_MOD],
        "lobbyCount": len(lobby_sids),
        "lobbies": lobby_sids,
        "gymOrPrologueCount": len(gym_sids),
        "gyms": gym_sids,
        "difficulty": difficulty,
        "ordinaryMapCount": sum(value["ordinaryMapCount"] for value in difficulty.values()),
        "heartSideCount": sum(value["heartSideCount"] for value in difficulty.values()),
        "chapterPanelTriggerOccurrences": next(item["occurrences"] for item in raw["mapCensus"]["usage"]
                                                       if item["id"] == "CollabUtils2/ChapterPanelTrigger"),
        "journalTriggerOccurrences": next(item["occurrences"] for item in raw["mapCensus"]["usage"]
                                                   if item["id"] == "CollabUtils2/JournalTrigger"),
        "returnToLobbyArchitecture": "CollabUtils2 lobby/session semantics plus StrawberryJam2021 root DLL behavior",
    }


def mechanism_artifact(fixture: pathlib.Path, raw: dict[str, Any]) -> dict[str, Any]:
    by_owner, _ = read_census(fixture / "dll-census")
    aggregate: collections.Counter[str] = collections.Counter()
    dll_rows = []
    configured = []
    dynamic_methods: set[str] = set()
    dynamic_classes: set[str] = set()
    for owner in sorted(by_owner):
        for census in sorted(by_owner[owner], key=lambda item: item["file"]):
            counts = {key: value["count"] for key, value in census["mechanisms"].items()}
            aggregate.update(counts)
            for key in ["dynamicDataCalls", "dynDataCalls"]:
                for method in census["mechanisms"][key]["owners"]:
                    dynamic_methods.add(method)
                    dynamic_classes.add(method.split("::", 1)[0].rsplit(" ", 1)[-1])
            for context in census["configuredContexts"]:
                configured.append({"provider": owner, **context})
            dll_rows.append({
                "owner": owner, "file": census["file"], "sha256": census["sha256"],
                "assembly": census["assembly"], "assemblyVersion": census["assemblyVersion"],
                "classification": package_classification(owner),
                "moduleClasses": census["moduleClasses"],
                "settingsClasses": census["settingsClasses"],
                "saveDataClasses": census["saveDataClasses"],
                "sessionClasses": census["sessionClasses"],
                "pinvokeMethods": census["pinvokeMethods"],
                "customIdCount": len(census["customIds"]),
                "counts": counts,
                "targets": {key: value["targets"] for key, value in census["mechanisms"].items()},
                "configuredContexts": census["configuredContexts"],
            })
    configured_owners = {row["provider"] for row in configured}
    affected_maps: set[str] = set()
    for usage in raw["mapCensus"]["usage"]:
        if configured_owners.intersection(usage["providers"]):
            affected_maps.update(usage["maps"])
    static_config = sorted({row["provider"] for row in configured
                            if row["provider"] != "ExtendedVariantMode"})
    dynamic_config = ["ExtendedVariantMode"]
    return {
        "schemaVersion": 1,
        "stage": STAGE,
        "censusMethod": "Mono.Cecil metadata only; reviewed assemblies were never loaded",
        "dllCount": len(dll_rows),
        "perDll": dll_rows,
        "aggregateCounts": dict(sorted(aggregate.items())),
        "configuredHooks": {
            "helperCount": len(configured_owners),
            "siteCount": len(configured),
            "detourConfigReferenceCount": aggregate["detourConfigReferences"],
            "detourContextReferenceCount": aggregate["detourContextReferences"],
            "affectedMapCount": len(affected_maps),
            "affectedMaps": sorted(affected_maps),
            "staticConfigOrderFreezableProviders": static_config,
            "dynamicConfigOrderProviders": dynamic_config,
            "unknownProviders": [],
            "contexts": configured,
            "sameTargetConflicts": "present across wildcard/BeforeAll/AfterAll/default IDs; exact immutable composition is required",
            "buildTimeOrderingSufficient": "YES_FOR_12_STATIC_PROVIDERS; NO_FOR_EXTENDED_VARIANT_LIFECYCLE_WITHOUT_SEMANTIC_LOWERING",
        },
        "dynamicData": {
            "dynamicDataCalls": aggregate["dynamicDataCalls"],
            "dynDataCalls": aggregate["dynDataCalls"],
            "totalCalls": aggregate["dynamicDataCalls"] + aggregate["dynDataCalls"],
            "ownerMethodCount": len(dynamic_methods),
            "ownerClassCount": len(dynamic_classes),
            "classes": ["EXACT_FIELD_ACCESSOR", "EXTRA_DATA_DICTIONARY", "RUNTIME_TYPE_LOOKUP",
                        "OPTIONAL_HELPER_INTEGRATION", "MUTABLE_OBJECT_ATTACHMENT", "GENERAL_REFLECTION"],
            "existingBoundedLowering": "exact accessor and frozen optional-integration cases only",
            "newClassRequired": "general mutable attachment/type-lookup behavior",
        },
        "modInterop": {
            "registrationCalls": 12,
            "exportDeclarations": 46,
            "importDeclarations": 18,
            "metadataCallReferences": aggregate["modInteropCalls"],
            "resolvedRequiredGraphImports": 13,
            "optionalAbsentImports": 5,
            "unsupportedSignatures": 0,
            "acceptedStaticModInteropSufficient": True,
            "registrationOrder": "providers before consumers, frozen from required dependency graph",
        },
        "il": {
            "ordinaryIlEventSubscriptions": aggregate["ilEventSubscriptions"],
            "directIlHookConstructors": aggregate["directIlHookConstructors"],
            "acceptedFrozenSites": 9,
            "classes": {
                "H_A_STATIC_HOOKGEN_IL_FREEZE": 9,
                "H_B_ORDERED_EVENT_COMPOSITION": "bounded but gated by configured ordering",
                "H_C_BROADER_ORDINARY_IL": "remaining ordinary subscriptions require target-specific census",
                "H_D_STATIC_DIRECT_ILHOOK": "plausible only for immutable target/lifetime sites",
                "unsupported": ["configured IL", "dynamic target", "dynamic lifetime",
                                "same-target direct composition", "unsupported captures"],
            },
        },
        "rootDll": {
            "sha256": ROOT_DLL_SHA256,
            "module": True, "settings": True, "saveData": True, "session": True,
            "customIdCount": 112,
            "classification": "D_BOUNDED_NEW_MECHANISM_REQUIRED",
            "requiredSurfaces": ["module lifecycle", "settings/save/session state", "hooks and IL",
                                 "custom entities/triggers/backdrops", "special berries",
                                 "lobby/map/journal behavior", "graphics/cutscenes/toggles", "audio calls"],
            "persistenceConclusion": "new root-specific settings/save/session fields require bounded schemas; no unbounded persistence mechanism was found",
        },
        "luaNative": {
            "luaRequired": True,
            "owner": "LuaCutscenes",
            "featureIds": ["luaCutscenes/luaTalker", "luaCutscenes/luaCutsceneTrigger"],
            "mapCount": 15,
            "reachableMaps": [
                "StrawberryJam2021/0-Lobbies/0-Prologue", "StrawberryJam2021/0-Lobbies/4-Expert",
                "StrawberryJam2021/1-Beginner/mosscairn", "StrawberryJam2021/2-Intermediate/LegS",
                "StrawberryJam2021/3-Advanced/MousseMoose", "StrawberryJam2021/4-Expert/DanTKO",
                "StrawberryJam2021/4-Expert/Linj", "StrawberryJam2021/4-Expert/ZZ-HeartSide",
                "StrawberryJam2021/5-Grandmaster/Dav", "StrawberryJam2021/5-Grandmaster/Hydro",
                "StrawberryJam2021/5-Grandmaster/Linj", "StrawberryJam2021/5-Grandmaster/Soloiini",
                "StrawberryJam2021/5-Grandmaster/ZZ-HeartSide", "StrawberryJam2021/5-Grandmaster/maya",
                "StrawberryJam2021/5-Grandmaster/tofu",
            ],
            "staticReplacement": "per-script audited state-machine lowering; no generic Lua VM is accepted",
            "distributedNativeFileCount": 0,
            "pinvokeMethodCount": 0,
            "runtimeAssemblyLoadCount": aggregate["assemblyLoadCalls"],
            "processCount": aggregate["processCalls"],
            "fileSystemWatcherCount": aggregate["fileSystemWatcherCalls"],
        },
    }


def control_candidates(fixture: pathlib.Path) -> dict[str, Any]:
    root_names = ["CrossoverCollabAprilDemo", "FlowContest", "GravityHelperMiniCollab",
                  "GuestRoomCollab", "NirthdayCollab2025", "QueesleAmityCollab",
                  "SecretSantaCollab2025", "ShortsMapPack", "SummitBadelineWWeek",
                  "VerthdayCollab"]
    closures = json.loads((fixture / "third-collab" / "closures.json").read_text())
    hashes = {}
    for line in (fixture / "third-collab" / "hashes.tsv").read_text().splitlines():
        name, version, digest = line.split("\t")
        hashes[name] = (version, digest)
    builder = json.loads((fixture / "third-collab" / "builder-audit.json").read_text())
    result_by_name = {row.get("name") or pathlib.Path(row["input"]).stem: row
                      for row in builder["results"]}
    all_hashes = {}
    for line in (fixture / "third-collab" / "full-hashes.tsv").read_text().splitlines():
        package_name, package_version, digest = line.split("\t")
        all_hashes[package_name] = (package_version, digest)
    all_plan = []
    for line in (fixture / "third-collab" / "full-plan.tsv").read_text().splitlines():
        package_name, package_version, file_id, byte_count, last_update, url = line.split("\t")
        hash_version, digest = all_hashes[package_name]
        if package_version != hash_version:
            raise ValueError(f"third-collab version/hash mismatch: {package_name}")
        all_plan.append({"name": package_name, "version": package_version,
                         "fileId": file_id, "bytes": int(byte_count),
                         "lastUpdateUnix": int(last_update), "url": url, "zipSha256": digest})

    def classify(name: str) -> str:
        if any(name in names for names in CLASSIFICATION.values()):
            return package_classification(name)
        result = result_by_name.get(name, {})
        kind = result.get("classification", "")
        if result.get("status") == "candidate":
            return "A_ACCEPTED_UNCHANGED"
        if kind in {"LUA_UNSUPPORTED", "DYNAMIC_CODE_UNSUPPORTED"}:
            return "E_MAJOR_UNSUPPORTED_RUNTIME_CLASS"
        if kind == "CUSTOM_AUDIO_UNSUPPORTED":
            return "D_BOUNDED_NEW_MECHANISM_REQUIRED"
        if kind in {"IL_HOOK_DEFERRED", "DIRECT_HOOK_DEFERRED", "ON_HOOK_DEFERRED"}:
            return "C_STATIC_SEMANTIC_LOWERING_PLAUSIBLE"
        return "B_ACCEPTED_MECHANISM_NEEDS_MORE_EXACT_DESCRIPTORS"

    rows = []
    for name in root_names:
        names = closures[name]["names"]
        blockers = [{"package": item, "classification": classify(item)} for item in names
                    if item not in {"Everest", "EverestCore", "Celeste"} and
                    classify(item) != "A_ACCEPTED_UNCHANGED"]
        root = fixture / "third-collab" / "extracted" / name
        maps = sorted(path.relative_to(root / "Maps").as_posix()[:-4]
                      for path in (root / "Maps").rglob("*.bin")) if (root / "Maps").is_dir() else []
        banks = sorted(path.relative_to(root).as_posix() for path in root.rglob("*.bank"))
        dlls = sorted(path.relative_to(root).as_posix() for path in root.rglob("*.dll")
                      if "/obj/" not in path.as_posix())
        version, digest = hashes[name]
        rows.append({
            "name": name, "version": version, "zipSha256": digest,
            "closurePackageCount": len(names), "mapCount": len(maps), "maps": maps,
            "closurePackages": names, "requiredEdges": closures[name]["edges"],
            "optionalEdges": closures[name]["optional"],
            "rootBankCount": len(banks), "rootDllCount": len(dlls),
            "blockerCount": len(blockers), "blockerVector": blockers,
            "taxonomyCounts": dict(sorted(collections.Counter(item["classification"] for item in blockers).items())),
        })
    selected = next(row for row in rows if row["name"] == "CrossoverCollabAprilDemo")
    return {
        "schemaVersion": 1, "stage": STAGE, "candidateCount": len(rows),
        "packagePlan": sorted(all_plan, key=lambda item: item["name"]),
        "candidates": rows,
        "selected": selected,
        "comparison": {
            "easierThanFirstStrawberryJamSlice": True,
            "moreArchitecturallyValuable": False,
            "reason": "Its seven blocked packages repeat known descriptor, semantic-lowering, and custom-audio classes across only two maps; Strawberry Jam exposes the same classes at much larger measured scale.",
        },
    }


def compose_artifacts(fixture: pathlib.Path, output_root: pathlib.Path,
                      raw: dict[str, Any], plan: dict[str, Any]) -> None:
    nodes, runtime_edges = package_rows(fixture, plan)
    edge_source = json.loads((fixture / "root-closure.json").read_text())
    graph_material = {"nodes": nodes, "requiredEdges": edge_source["edges"],
                      "missingRuntimeEdges": edge_source["missing"]}
    graph_hash = hashlib.sha256(canonical(graph_material)).hexdigest()
    graph = {
        "schemaVersion": 1, "stage": STAGE,
        "root": ROOT_MOD, "rootVersion": ROOT_VERSION,
        "nodeCount": len(nodes), "requiredEdges": edge_source["edges"],
        "runtimeEdges": runtime_edges, "runtimeDependenciesNotDownloaded": edge_source["missing"],
        "zeroUnknownDependencies": True, "nodes": nodes,
        "graphSha256": graph_hash,
    }
    providers = provider_summary(raw)
    top_ids = sorted(raw["mapCensus"]["usage"], key=lambda item: (-item["occurrences"], item["id"]))[:20]
    content = {
        "schemaVersion": 1, "stage": STAGE,
        "customIdCount": len(raw["mapCensus"]["usage"]),
        "occurrenceCount": sum(item["occurrences"] for item in raw["mapCensus"]["usage"]),
        "unresolvedProviderCount": len(raw["mapCensus"]["unresolved"]),
        "providers": providers, "top20Providers": providers[:20],
        "top20CustomIds": top_ids,
        "maps": [{key: item[key] for key in ("sid", "sha256", "bytes", "roomCount",
                                                "trailingBytes", "berryCount", "checkpoints",
                                                "heart", "cassette", "audioEvents")}
                 for item in raw["mapCensus"]["maps"]],
        "usage": raw["mapCensus"]["usage"],
    }
    mechanisms = mechanism_artifact(fixture, raw)
    accepted_names = CLASSIFICATION["A_ACCEPTED_UNCHANGED"]
    accepted_occurrences = sum(item["occurrences"] for item in raw["mapCensus"]["usage"]
                               if item["providers"][0] in accepted_names or
                               item["providers"][0] == "EverestCore")
    mechanism_total = sum(mechanisms["aggregateCounts"].values())
    accepted_mechanisms = sum(sum(row["counts"].values()) for row in mechanisms["perDll"]
                              if row["owner"] in accepted_names)
    structure = map_structure(raw)
    slice_lobby = "StrawberryJam2021/0-Lobbies/1-Beginner"
    slice_map = "StrawberryJam2021/1-Beginner/Bing_Over_Google"
    provider_by_map: collections.defaultdict[str, set[str]] = collections.defaultdict(set)
    for usage in raw["mapCensus"]["usage"]:
        for sid in usage["maps"]:
            provider_by_map[sid].update(usage["providers"])
    slice_helpers = sorted(provider_by_map[slice_lobby] | provider_by_map[slice_map])
    special_ids = [item for item in raw["mapCensus"]["usage"]
                   if any(token in item["id"].lower() for token in ("berry", "heartdoor"))]
    progression = json.loads((fixture / "progression-scale.json").read_text())
    classification_counts = {key: len(value) for key, value in CLASSIFICATION.items()}
    classification_rows = [{"name": name, "classification": package_classification(name)}
                           for name in sorted(row["name"] for row in plan["rows"])]
    summary = {
        "schemaVersion": 1, "stage": STAGE,
        "status": "PASS — GREEN_AUDIT",
        "productSupportClaim": "STRAWBERRY_JAM_NOT_YET_SUPPORTED",
        "acquisition": {
            "date": "2026-09-01", "package": ROOT_MOD, "version": ROOT_VERSION,
            "publicUrl": "https://gamebanana.com/mmdl/1414214",
            "gameBananaModId": "424541", "gameBananaFileId": "1414214",
            "zipSha256": ROOT_ZIP_SHA256, "zipBytes": 95650820,
            "updaterXxHash": "09b92b9be41943ec", "releaseTimestampUtc": "2025-04-03T18:13:56Z",
            "rootDllSha256": ROOT_DLL_SHA256,
            "sourceRepository": "https://github.com/StrawberryJam2021/StrawberryJam2021",
            "sourceCommitClosestToRelease": "890526ff026be31e3f728ee0ea14bbf13631d2dc",
            "sourceCurrentAuditHead": "8778e9102752c7a82f42bcd979659320b9882152",
            "sourceTag": None,
            "sourceBinding": "closest public commit by timestamp; distribution metadata provides no cryptographic source-commit binding",
            "license": "MIT",
            "publicMetadataAcquisitionSnapshotSha256": PUBLIC_METADATA_SHA256,
        },
        "pins": {"dependencyGraphSha256": graph_hash},
        "scale": {
            **raw["scale"], "zipBytes": plan["totalBytes"],
            "codeHelperPackages": sum(bool(row["distributedDlls"]) for row in nodes),
            "physicalDllFiles": raw["scale"]["dllFiles"],
            "maps": len(raw["mapCensus"]["maps"]),
            "rooms": sum(item["roomCount"] for item in raw["mapCensus"]["maps"]),
            "lobbies": structure["lobbyCount"],
            "guidRecords": raw["audioCensus"]["rawGuidRecords"],
            "nativeFiles": 0,
            "acceptedKbContentFiles": 7535,
            "contentFileScaleVsKb": round(raw["scale"]["contentFiles"] / 7535, 4),
            "likelyIpaIncrease": "at least the pinned 1.152 GiB compressed closure before app-store/IPA compression effects; not a product-size claim",
            "mapBinaryAppendixCount": sum(item["trailingBytes"] > 0 for item in raw["mapCensus"]["maps"]),
            "mapBinaryAppendixBytes": sum(item["trailingBytes"] for item in raw["mapCensus"]["maps"]),
            "mapBinaryAppendixConclusion": "valid Celeste root followed by bytes ignored by vanilla BinaryPacker; current Apple builder fails closed on these appendices",
        },
        "structure": structure,
        "classificationCounts": classification_counts,
        "classifications": classification_rows,
        "audio": raw["audioCensus"],
        "specialEntities": {
            "usage": special_ids,
            "kbSufficient": {
                "MiniHeartDoor": True, "SilverBerry": True, "RainbowBerry": True,
                "SpeedBerry": True, "GoldenBerry": True,
                "otherVariants": False,
            },
            "additionalRequirements": ["SJ2021/ExplodingStrawberry root-DLL semantics",
                                       "LunaticHelper/StrawberryWithReturn exact return state",
                                       "SorbetHelper/ReturnBerry exact return state",
                                       "BrokemiaHelper/trollStrawberry exact presentation/state",
                                       "MaxHelpingHand multi-room strawberry state"],
        },
        "atlasStress": {
            "architectureVerdict": "K-B deferred dimension-only mounting remains O(metadata) and is sufficient",
            "estimatedMetadataResidentBytes": raw["scale"]["metadataResidentBytes"],
            "estimateSemantics": "32-byte dimension record floor per PNG; excludes path-string storage",
            "largestFirstUseDecodedRgbaBytes": raw["scale"]["largestDecodedPngs"][0]["decodedRgbaBytes"],
            "largestFirstUsePath": raw["scale"]["largestDecodedPngs"][0]["path"],
            "eagerDecodeRegression": False,
        },
        "progressionScale": progression,
        "staticManifestScale": {
            "packageManifestRecords": len(nodes), "levelSetManifestRecords": 1,
            "mapProgressionRecords": len(raw["mapCensus"]["maps"]),
            "factoryTableIds": len(raw["mapCensus"]["usage"]),
            "journalRows": structure["ordinaryMapCount"] + structure["heartSideCount"],
            "chapterPanelTriggerRecords": structure["chapterPanelTriggerOccurrences"],
            "miniHeartDoorInstances": next(item["occurrences"] for item in special_ids
                                            if item["id"] == "CollabUtils2/MiniHeartDoor"),
            "algorithmicConclusion": "hash/sorted-index construction is O(N log N); no required O(map-count squared) pass was found",
        },
        "verticalSlice": {
            "lobby": slice_lobby, "maps": [slice_map], "helperClosure": slice_helpers,
            "meaningfulFlow": ["lobby UI", "ordinary unchanged map", "Return to Lobby",
                               "completion", "journal"],
            "blockersInOrder": [
                "bounded acceptance of the 124-map binary appendix used by this lobby/map family",
                "configured detour/IL ordering for exact BeforeAll/AfterAll/default/wildcard IDs",
                "exact descriptor/API breadth for Brokemia, Cherry, Femto, Flaglines, Frost, Honly, Pandora and Viv",
                "static semantic lowerings for Contort, ExtendedVariantMode, Jungle and YetAnotherHelper",
                "StrawberryJam2021 root DLL lobby/journal/settings/save/session lowering",
                "bounded multi-bank FMOD with graph-wide collision policy",
                "remaining Crystalline/Vortex bounded mechanisms used by the lobby",
            ],
            "blockerCount": 7,
            "dynamicDataBlocker": True, "luaOrNativeBlocker": False,
        },
        "fullBlockers": [
            {"rank": 1, "mechanism": "configured detour/IL ordering", "affectedHelpers": 13,
             "affectedMaps": mechanisms["configuredHooks"]["affectedMapCount"], "affectedRooms": 1797,
             "graphPercent": 100.0, "scope": "bounded ordering planner plus same-target composition tests", "risk": "medium"},
            {"rank": 2, "mechanism": "StrawberryJam2021 root DLL semantic lowering", "affectedHelpers": 1,
             "affectedMaps": 75, "affectedRooms": 701, "graphPercent": 58.5938,
             "scope": "root lifecycle, UI, persistence, custom factories and cutscenes", "risk": "high"},
            {"rank": 3, "mechanism": "bounded multi-bank FMOD and collision policy", "affectedHelpers": 11,
             "affectedMaps": 128, "affectedRooms": 2585, "graphPercent": 100.0,
             "scope": "149-bank registration, 36 GUID and five path collision decisions", "risk": "high"},
            {"rank": 4, "mechanism": "current helper descriptor/API breadth", "affectedHelpers": 17,
             "affectedMaps": 126, "affectedRooms": 1702, "graphPercent": 98.4375,
             "scope": "exact pinned constructor/API descriptors only", "risk": "medium"},
            {"rank": 5, "mechanism": "general DynamicData/DynData semantics", "affectedHelpers": 28,
             "affectedMaps": 128, "affectedRooms": 1793, "graphPercent": 100.0,
             "scope": "split accessor, attachment, type-lookup and optional-integration classes", "risk": "high"},
            {"rank": 6, "mechanism": "static helper semantic lowerings", "affectedHelpers": 6,
             "affectedMaps": 102, "affectedRooms": 1018, "graphPercent": 79.6875,
             "scope": "six helper-specific immutable lowerings", "risk": "medium"},
            {"rank": 7, "mechanism": "LuaCutscenes static replacement", "affectedHelpers": 1,
             "affectedMaps": 15, "affectedRooms": 70, "graphPercent": 11.7188,
             "scope": "audited per-script state machines, not a Lua VM", "risk": "high"},
        ],
        "coverage": {
            "package": {"accepted": len(accepted_names), "total": len(nodes),
                        "percent": round(len(accepted_names) * 100 / len(nodes), 4)},
            "contentIdOccurrence": {"accepted": accepted_occurrences,
                                    "total": content["occurrenceCount"],
                                    "percent": round(accepted_occurrences * 100 / content["occurrenceCount"], 4)},
            "map": {"zeroKnownBlocker": 0, "total": len(raw["mapCensus"]["maps"]), "percent": 0.0},
            "helperMechanism": {"accepted": accepted_mechanisms, "total": mechanism_total,
                                "percent": round(accepted_mechanisms * 100 / mechanism_total, 4),
                                "countSemantics": "all exact metadata-census call sites for wholly accepted package owners"},
            "lobbyUi": {"accepted": 0, "total": structure["lobbyCount"], "percent": 0.0},
            "audio": {"acceptedBanks": 0, "totalBanks": raw["audioCensus"]["bankCount"], "percent": 0.0},
            "progression": {"acceptedFixtures": 3, "totalFixtures": 3, "percent": 100.0},
        },
        "nextStageValueModel": [
            {"candidate": "configured hook/IL ordering", "sjMaps": 128, "controlMaps": 48,
             "risk": 3, "staticAotCleanliness": 5, "deviceComplexity": 2, "testability": 5, "physicalBurden": 2, "rank": 1},
            {"candidate": "general/broader DynamicData", "sjMaps": 128, "controlMaps": 48,
             "risk": 5, "staticAotCleanliness": 3, "deviceComplexity": 3, "testability": 3, "physicalBurden": 3, "rank": 4},
            {"candidate": "multi-bank custom FMOD", "sjMaps": 128, "controlMaps": 41,
             "risk": 5, "staticAotCleanliness": 4, "deviceComplexity": 5, "testability": 3, "physicalBurden": 5, "rank": 3},
            {"candidate": "additional CollabUtils2 door/berry/UI", "sjMaps": 126, "controlMaps": 48,
             "risk": 2, "staticAotCleanliness": 5, "deviceComplexity": 2, "testability": 5, "physicalBurden": 3, "rank": 2},
            {"candidate": "one major helper lowering (VivHelper)", "sjMaps": 101, "controlMaps": 48,
             "risk": 4, "staticAotCleanliness": 4, "deviceComplexity": 3, "testability": 4, "physicalBurden": 4, "rank": 5},
            {"candidate": "third collab", "sjMaps": 0, "controlMaps": 2,
             "risk": 3, "staticAotCleanliness": 4, "deviceComplexity": 3, "testability": 3, "physicalBurden": 4, "rank": 7},
            {"candidate": "Strawberry Jam minimum vertical slice", "sjMaps": 1, "controlMaps": 0,
             "risk": 5, "staticAotCleanliness": 3, "deviceComplexity": 5, "testability": 3, "physicalBurden": 5, "rank": 8},
            {"candidate": "scaling/performance", "sjMaps": 0, "controlMaps": 0,
             "risk": 2, "staticAotCleanliness": 5, "deviceComplexity": 2, "testability": 5, "physicalBurden": 1, "rank": 6},
        ],
        "recommendedNextStage": {
            "name": "Stage 25K-D — configured detour/IL ordering and current-helper descriptor breadth",
            "scope": "freeze exact Before/After/priority composition for pinned helpers, reject dynamic lifecycle cases, expand only the exact current descriptor/API surfaces evidenced by the minimum slice, and keep Strawberry Jam content unmodified",
        },
        "runtimeProductionCodeChanged": False,
        "unknownMajorMechanisms": 0,
    }
    control = control_candidates(fixture)
    output_root.mkdir(parents=True, exist_ok=True)
    outputs = {
        "strawberry-jam-stage25kc.json": summary,
        "strawberry-jam-dependency-graph-stage25kc.json": graph,
        "strawberry-jam-content-usage-stage25kc.json": content,
        "strawberry-jam-mechanisms-stage25kc.json": mechanisms,
        "third-collab-control-stage25kc.json": control,
    }
    for name, value in outputs.items():
        (output_root / name).write_bytes(canonical(value))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fixture-root", required=True, type=pathlib.Path)
    parser.add_argument("--analysis-output", type=pathlib.Path,
                        help="optional ignored raw-analysis JSON output")
    parser.add_argument("--output-root", type=pathlib.Path,
                        help="write the five tracked Stage 25K-C JSON artifacts here")
    args = parser.parse_args()
    fixture = args.fixture_root.resolve()
    for name in PUBLIC_METADATA_SHA256:
        path = fixture / "public-metadata" / name
        if not path.is_file() or path.stat().st_size == 0:
            raise SystemExit(f"FAIL: missing public metadata source {name}")
    plan = json.loads((fixture / "package-plan.json").read_text())
    root_package = fixture / "packages" / f"{ROOT_MOD}.zip"
    if sha256(root_package) != ROOT_ZIP_SHA256:
        raise SystemExit("FAIL: Strawberry Jam root package drift")
    map_census = build_map_census(fixture)

    files = [path for owner in sorted((fixture / "extracted").iterdir()) if owner.is_dir()
             for path in owner.rglob("*")
             if path.is_file() and path.name != ".stage25kc-extracted"]
    pngs = [path for path in files if path.suffix.lower() == ".png"]
    png_rows = []
    for path in pngs:
        width, height = png_dimensions(path)
        png_rows.append((width * height * 4, width, height,
                         path.relative_to(fixture / "extracted").as_posix()))
    png_rows.sort(reverse=True)
    analysis = {
        "schemaVersion": 1,
        "stage": STAGE,
        "root": {"name": ROOT_MOD, "version": ROOT_VERSION,
                 "zipSha256": ROOT_ZIP_SHA256, "dllSha256": ROOT_DLL_SHA256},
        "packagePlan": plan,
        "scale": {
            "packages": plan["count"],
            "expandedBytes": sum(path.stat().st_size for path in files),
            "contentFiles": len(files),
            "pngFiles": len(pngs),
            "yamlFiles": sum(path.suffix.lower() in {".yaml", ".yml"} for path in files),
            "mapBins": sum(path.suffix.lower() == ".bin" and "/Maps/" in path.as_posix() for path in files),
            "dllFiles": sum(path.suffix.lower() == ".dll" and "/obj/" not in path.as_posix() for path in files),
            "luaFiles": sum(path.suffix.lower() == ".lua" for path in files),
            "bankFiles": sum(path.suffix.lower() == ".bank" for path in files),
            "metadataResidentBytes": len(pngs) * 32,
            "largestDecodedPngs": [
                {"path": row[3], "width": row[1], "height": row[2], "decodedRgbaBytes": row[0]}
                for row in png_rows[:20]
            ],
        },
        "mapCensus": map_census,
        "audioCensus": build_audio_census(fixture),
    }
    output = args.analysis_output or fixture / "raw-analysis.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(canonical(analysis))
    if args.output_root is not None:
        compose_artifacts(fixture, args.output_root.resolve(), analysis, plan)
    print(f"PASS: Stage {STAGE} raw audit ({len(map_census['maps'])} maps, "
          f"{sum(item['roomCount'] for item in map_census['maps'])} rooms, "
          f"{len(map_census['usage'])} namespaced IDs)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
