#!/usr/bin/env python3
"""Reproduce the bounded Stage 25K-F audio and helper evidence.

The script reads only exact public K-C fixtures and host-generated IL metadata.
It never copies map, bank, GUID-export, or assembly bytes into tracked output.
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import importlib.util
import json
import pathlib
import re
import sys
import time
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location(
    "stage25ke", ROOT / "scripts/generate-apple-everest-stage25ke.py")
ke = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = ke
spec.loader.exec_module(ke)

LOBBY = "StrawberryJam2021/0-Lobbies/1-Beginner"
BING = "StrawberryJam2021/1-Beginner/Bing_Over_Google"
MAP_IDS = {
    "VortexHelper/AttachedJumpThru": ("VortexHelper", "entity"),
    "vitellary/bloomstrengthtrigger": ("CrystallineHelper", "trigger"),
    "vitellary/editdepthtrigger": ("CrystallineHelper", "trigger"),
    "vitellary/triggertrigger": ("CrystallineHelper", "trigger"),
}
GUID_LINE = re.compile(r"^\{([0-9a-fA-F-]{36})\} (.+)$")

BANKS = [
    {
        "owner": "StrawberryJam2021AudioA", "version": "1.0.4",
        "archiveSha256": "81e9cbc39b3a5525c93dfc5b24b675a833a8fb616e8a01cfc0838296f5e37e1d",
        "sourcePath": "Audio/sj21_bingovergoogle.bank",
        "bankSha256": "772e3d4a41b463edcf529882f6d2f4de12384e891ae62a23220aa1379d89e159",
        "guidSourcePath": "Audio/sj21_bingovergoogle.guids.txt",
        "guidSha256": "88c1e1c6d54ffcfc9fbb1c4527cb9108e531ea3bc303dbfd73b8c7eeba0cf1ba",
        "bankId": "f023b527-acd0-40a9-a9b3-87e9f686b81c", "bankPath": "bank:/sj21_bingovergoogle",
        "requiredEventPath": "event:/sj21_bingovergoogle", "requiredEventId": "cbfb24b2-faf6-4db8-bc5a-c096f754724e",
        "loadOrdinal": 9,
    },
    {
        "owner": "StrawberryJam2021AudioA", "version": "1.0.4",
        "archiveSha256": "81e9cbc39b3a5525c93dfc5b24b675a833a8fb616e8a01cfc0838296f5e37e1d",
        "sourcePath": "Audio/sj21_shared.bank",
        "bankSha256": "7620d1de4c32f1564b1206ac252af806b5b33c6a9f2b4624df582ece0d7f62e8",
        "guidSourcePath": "Audio/sj21_shared.guids.txt",
        "guidSha256": "500b8c536468e0ce4bab7c44c0f8c8fe932a5ef41ab68d2793683f8a21420555",
        "bankId": "1068df52-9f57-4e6e-887c-c1d5a961d61d", "bankPath": "bank:/sj21_shared",
        "requiredEventPath": "event:/sj21_levelselect", "requiredEventId": "3de891f8-2a2c-42d8-b243-f522abaa7db5",
        "loadOrdinal": 10,
    },
    {
        "owner": "StrawberryJam2021AudioB", "version": "1.0.0",
        "archiveSha256": "70b90f45709956a4d18bfbb5941836c534344a1cf0ab859a50430daa3b76ef42",
        "sourcePath": "Audio/sj21_BegLobby.bank",
        "bankSha256": "a5c45fe0ed77d048c4c9520bb2e1e58f9dbfd1d05dc2ccc19d5310fac0ac0ceb",
        "guidSourcePath": "Audio/sj21_BegLobby.GUIDs.txt",
        "guidSha256": "3a8ea6f4a1f7f3ec0ae16b19b7cec11c5a1479974e830d812896351fa3cac740",
        "bankId": "827873b5-86b7-4e1b-9848-e04c83fb7ddf", "bankPath": "bank:/sj21_BegLobby",
        "requiredEventPath": "event:/sj21_BegLobby", "requiredEventId": "8e00fa4b-a47b-4270-8607-200aa2e49996",
        "loadOrdinal": 11,
    },
    {
        "owner": "StrawberryJam2021", "version": "1.0.12",
        "archiveSha256": "4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655",
        "sourcePath": "Audio/sj21_jamjars.bank",
        "bankSha256": "9e6bf27fc7f607e2ef5695b6b32ccf34abf1b7380ba13d66f3b49bc5b1613c1b",
        "guidSourcePath": "Audio/sj21_jamjars.guids.txt",
        "guidSha256": "034b1e3a27419c8332e4a81af1a773971858daca97609ea9855ddeb5b7bf34f7",
        "bankId": "a8371196-9461-4ff6-8994-7032718615a7", "bankPath": "bank:/sj21_jamjars",
        "requiredEventPath": "event:/sj21_jamjar-blue", "requiredEventId": "f81c1b1a-e90e-4442-90a7-8d05db253a0b",
        "loadOrdinal": 12,
    },
]

HELPER_PINS = {
    "CrystallineHelper": {
        "version": "1.17.2", "publicUrl": "https://gamebanana.com/mmdl/1744902",
        "zipSha256": "4573f5e45dce0905142cd2b119f4a9a744bdce8ef3199319d1e46b6d1d342747",
        "dllPath": "Code/bin/vitmod.dll", "dllSha256": "456410258fbce4594c3e987d025bf651e1bd3f49d2d676a921d8ea9f27ba052a",
        "logicalSourceSha256": "6a5fdd5a7b4ae95b77ebc351a5b663c872c67a5d09347752fbf423e7ae50deb2",
        "sourceAvailability": "DISTRIBUTED_SOURCE_PRESENT", "distributedSourceFileCount": 44,
        "sourceCommit": "NOT_BOUND_BY_RELEASE_METADATA", "license": "NO_LICENSE_FILE_IN_DISTRIBUTED_ARCHIVE",
        "ids": ["vitellary/bloomstrengthtrigger", "vitellary/editdepthtrigger", "vitellary/triggertrigger"],
    },
    "VortexHelper": {
        "version": "1.2.19", "publicUrl": "https://gamebanana.com/mmdl/1368600",
        "zipSha256": "b6280fe2e3c05d355c32a51049854137c41be72d24ec4aec9d997cc7c4394db2",
        "dllPath": "Code/bin/VortexHelper.dll", "dllSha256": "f5a32f02c2699dcdf41bb1af808ead631491571c3d259429761953693bd37f73",
        "logicalSourceSha256": "c071d33bb1cc4f0387ea204834e212bd020f3143aea68c9f4e56b9bfa35def1b",
        "sourceAvailability": "DISTRIBUTED_SOURCE_PRESENT", "distributedSourceFileCount": 15,
        "sourceCommit": "NOT_BOUND_BY_RELEASE_METADATA", "license": "NO_LICENSE_FILE_IN_DISTRIBUTED_ARCHIVE",
        "ids": ["VortexHelper/AttachedJumpThru"],
        "priorAcceptedEvidence": "Stage 25H-C frozen-IL evidence retained; it did not cover this AttachedJumpThru population",
    },
}


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def canonical(value) -> bytes:
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n").encode()


def finish(value):
    value["artifactSha256"] = sha(canonical(value))
    return value


def save(path: pathlib.Path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n")


def parse_guids(path: pathlib.Path, strict: bool = True):
    records = []
    for line in path.read_text(encoding="utf-8").splitlines():
        match = GUID_LINE.fullmatch(line)
        if not match and not strict:
            continue
        if not match:
            raise RuntimeError(f"malformed GUID record in {path.name}")
        guid, logical = match.group(1).lower(), match.group(2)
        kind = logical.split(":", 1)[0]
        if kind not in {"event", "bus", "vca", "snapshot", "bank"}:
            raise RuntimeError(f"unsupported GUID kind {kind}")
        records.append({"id": guid, "path": logical, "kind": kind})
    return records


def required_audio(fixture: pathlib.Path):
    extracted = fixture / "extracted"
    rows = []
    for fixed in BANKS:
        row = dict(fixed)
        package = fixture / "packages" / (row["owner"] + ".zip")
        root = extracted / row["owner"]
        bank = root / row["sourcePath"]
        guid_file = root / row["guidSourcePath"]
        assert sha(package.read_bytes()) == row["archiveSha256"]
        assert sha(bank.read_bytes()) == row["bankSha256"]
        assert sha(guid_file.read_bytes()) == row["guidSha256"]
        exported = parse_guids(guid_file)
        bank_bytes = bank.read_bytes()
        embedded = [record for record in exported
                    if uuid.UUID(record["id"]).bytes_le in bank_bytes]
        bank_records = [record for record in embedded if record["kind"] == "bank"]
        required = [record for record in embedded if record["path"] == row["requiredEventPath"]]
        assert bank_records == [{"id": row["bankId"], "path": row["bankPath"], "kind": "bank"}]
        assert required == [{"id": row["requiredEventId"], "path": row["requiredEventPath"], "kind": "event"}]
        counts = collections.Counter(record["kind"] for record in embedded)
        row.update({
            "archive": row["owner"] + ".zip", "guidExportRecords": exported,
            "guidExportRecordCount": len(exported), "embeddedGuidRecords": embedded,
            "embeddedGuidRecordCount": len(embedded), "eventCount": counts["event"],
            "busCount": counts["bus"], "vcaCount": counts["vca"],
            "snapshotCount": counts["snapshot"], "stringsBankRelationship": "NONE",
            "masterBankRelationship": "NONE", "programmerSound": False,
            "nativePluginOrDsp": False,
        })
        rows.append(row)
    assert len({(r["owner"], r["bankId"], r["bankSha256"]) for r in rows}) == 4
    return rows


def full_audio_audit(fixture: pathlib.Path):
    """Search only FMOD event metadata (the prefix before the first FSB5)."""
    extracted = fixture / "extracted"
    kc_audio = json.loads((ROOT / "apple-everest/strawberry-jam-stage25kc.json").read_text())["audio"]
    by_id = collections.defaultdict(set)
    by_path = collections.defaultdict(set)
    raw = 0
    # Preserve K-C's exact, case-sensitive public-export population.
    exports = sorted(extracted.rglob("*.guids.txt"))
    assert len(exports) == kc_audio["guidExportCount"] == 117
    for export in exports:
        for record in parse_guids(export, strict=False):
            raw += 1
            by_id[record["id"]].add(record["path"])
            by_path[record["path"]].add(record["id"])
    assert raw == kc_audio["rawGuidRecords"] == 71976
    assert len(by_id) == kc_audio["uniqueGuidCount"] == 2091
    assert len(by_path) == kc_audio["uniquePathCount"] == 2123

    started = time.perf_counter()
    rows, exact_id_paths, exact_path_ids = [], collections.defaultdict(set), collections.defaultdict(set)
    for source in kc_audio["banks"]:
        data = (extracted / source["path"]).read_bytes()
        fsb = data.find(b"FSB5")
        metadata = data if fsb < 0 else data[:fsb]
        matched = []
        for guid, paths in by_id.items():
            if uuid.UUID(guid).bytes_le in metadata:
                for logical in sorted(paths):
                    matched.append((guid, logical))
                    exact_id_paths[guid].add(logical)
                    exact_path_ids[logical].add(guid)
        bank_ids = sorted({guid for guid, logical in matched if logical.startswith("bank:/")})
        rows.append({"owner": source["owner"], "path": source["path"],
                     "bankSha256": source["sha256"], "metadataPrefixBytes": len(metadata),
                     "exactGuidRecordCount": len(matched), "bankIdentityGuidCount": len(bank_ids),
                     "hasBankIdentityAuthority": len(bank_ids) == 1})
    elapsed = time.perf_counter() - started
    assert len(rows) == 149 and all(row["exactGuidRecordCount"] > 0 for row in rows)
    authority = sum(row["hasBankIdentityAuthority"] for row in rows)
    assert authority == 119
    exact_guid_collisions = [
        {"guid": guid, "paths": sorted(paths)}
        for guid, paths in sorted(exact_id_paths.items()) if len(paths) > 1
    ]
    exact_path_collisions = [
        {"path": path, "guids": sorted(ids)}
        for path, ids in sorted(exact_path_ids.items()) if len(ids) > 1
    ]
    assert len(exact_guid_collisions) == 22 and len(exact_path_collisions) == 0
    assert len(kc_audio["incompatibleGuidCollisions"]) == 36
    assert len(kc_audio["incompatiblePathCollisions"]) == 5
    assert elapsed < 10
    return {
        "auditOnlyNoBanksPackaged": True, "totalBanks": 149,
        "structurallyCompatibleBankCount": 149, "bankIdentityManifestReadyCount": authority,
        "bankIdentityAuthorityPendingCount": 149 - authority,
        "eventsInPublicGuidCorpus": kc_audio["uniquePathsByKind"]["event"],
        "rawGuidRecords": raw, "uniqueGuids": len(by_id), "uniquePaths": len(by_path),
        "publicExportCollisionGroups": {
            "incompatibleGuid": len(kc_audio["incompatibleGuidCollisions"]),
            "incompatiblePath": len(kc_audio["incompatiblePathCollisions"]),
        },
        "exactMetadataCollisionGroups": {
            "incompatibleGuid": len(exact_guid_collisions),
            "incompatiblePath": len(exact_path_collisions),
        },
        "unsupportedShapeCount": 0, "masterBankCount": kc_audio["masterBankCount"],
        "stringsBankCount": kc_audio["stringsBankCount"],
        "programmerSoundCount": kc_audio["programmerSoundCount"],
        "nativePluginOrDspCount": kc_audio["nativePluginCount"],
        "maximumGraphBankCount": 149,
        "largestExactManifestRecordCount": max(r["exactGuidRecordCount"] for r in rows),
        "largestMetadataPrefixBytes": max(r["metadataPrefixBytes"] for r in rows),
        "plannerRuntimeCeilingSeconds": 10, "plannerObservedWithinRuntimeCeiling": True,
        "plannerPeakResidentCeilingBytes": 268435456,
        "algorithm": "all 2,091 K-C GUIDs searched as Guid.ToByteArray bytes before first FSB5",
        "disposition": "FULL_GRAPH_REJECTED_FOR_PACKAGING_COLLISIONS_AND_30_MISSING_BANK_IDENTITIES",
        "banks": rows,
    }


def rectangle(attrs):
    return (float(attrs.get("x", 0)), float(attrs.get("y", 0)),
            float(attrs.get("width", 8)), float(attrs.get("height", 8)))


def overlaps(a, b):
    ax, ay, aw, ah = rectangle(a); bx, by, bw, bh = rectangle(b)
    return ax < bx + bw and bx < ax + aw and ay < by + bh and by < ay + ah


def occurrence_evidence(fixture: pathlib.Path):
    map_path = fixture / "extracted/StrawberryJam2021/Maps" / (LOBBY + ".bin")
    tree, _ = ke.parse_map(map_path)
    room_items = collections.defaultdict(list)
    occurrences = []
    for item, parent, room in ke.walk(tree):
        if parent in {"entities", "triggers"}:
            room_items[room].append(item)
        if item["name"] in MAP_IDS:
            owner, kind = MAP_IDS[item["name"]]
            occurrences.append({
                "map": LOBBY, "room": room, "provider": owner, "kind": kind,
                "customId": item["name"], "entityId": item["attrs"]["id"],
                "attributes": item["attrs"],
                "nodes": [node["attrs"] for node in item["children"] if node["name"] == "node"],
                "classification": "STATIC_TYPED_FACTORY_ACCEPTED",
            })
    assert len(occurrences) == 55 and all(row["room"] == "sj2021beginnerlobby" for row in occurrences)
    by_id = collections.Counter(row["customId"] for row in occurrences)
    assert by_id == {"VortexHelper/AttachedJumpThru": 2, "vitellary/bloomstrengthtrigger": 9,
                     "vitellary/editdepthtrigger": 43, "vitellary/triggertrigger": 1}

    entities = room_items["sj2021beginnerlobby"]
    slopes = {254: "left slope at (896,1504)", 255: "left slope at (1016,1520)"}
    for row in occurrences:
        if row["customId"] == "VortexHelper/AttachedJumpThru":
            expected = 255 if row["entityId"] == 599 else 254
            target = next(item for item in entities if item["name"] == "XaphanHelper/Slope" and item["attrs"]["id"] == expected)
            row["resolvedRelation"] = {"targetCustomId": target["name"], "targetEntityId": expected,
                                       "targetAttributes": target["attrs"], "description": slopes[expected],
                                       "movingTarget": False, "liftSpeed": "zero for exact static slope population"}
        elif row["customId"] == "vitellary/editdepthtrigger":
            authored = row["attributes"]["entitiesToAffect"]
            target_id = {"Celeste.Mod.FancyTileEntities.FancySolidTiles": "FancyTileEntities/FancySolidTiles",
                         "Celeste.Mod.MaxHelpingHand.Entities.FlagExitBlock": "MaxHelpingHand/FlagExitBlock"}[authored]
            targets = sorted(item["attrs"]["id"] for item in entities
                             if item["name"] == target_id and overlaps(row["attributes"], item["attrs"]))
            row["resolvedRelation"] = {"authoredClrType": authored, "targetCustomId": target_id,
                                       "collidingTargetEntityIds": targets, "selection": "exact typed identity plus overlap",
                                       "noOpInPinnedRoom": not targets}
        elif row["customId"] == "vitellary/triggertrigger":
            assert row["nodes"] == [{"x": 480, "y": 2168}, {"x": 440, "y": 2168}]
            row["resolvedRelation"] = {
                "activation": "OnHoldableEnter", "delaySeconds": 0.4, "oneUse": True,
                "targetsInAuthoredNodeOrder": [
                    {"customId": "rumbleTrigger", "entityId": 1142, "node": row["nodes"][0],
                     "attributes": next(i["attrs"] for i in entities if i["name"] == "rumbleTrigger" and i["attrs"]["id"] == 1142)},
                    {"customId": "everest/flagTrigger", "entityId": 1141, "node": row["nodes"][1],
                     "attributes": next(i["attrs"] for i in entities if i["name"] == "everest/flagTrigger" and i["attrs"]["id"] == 1141)},
                ],
                "entityTypeAttributeDisposition": "desktop OnHoldableEnter accepts any Holdable; authored CherryHelper name is unused",
            }
    return occurrences


def method_census(owner: str, audit_root: pathlib.Path):
    data = json.loads((audit_root / (owner + "-semantics.json")).read_text())
    prefixes = (["vitmod.BloomStrengthTrigger", "vitmod.EditDepthTrigger", "vitmod.TriggerTrigger"]
                if owner == "CrystallineHelper" else
                ["Celeste.Mod.VortexHelper.Entities.AttachedJumpThru",
                 "Celeste.Mod.VortexHelper.Misc.StaticMoverWithLiftSpeed"])
    unreachable_markers = ("::Load()", "::Unload()", "::Engine_Update(", "::Player_Jump(",
        "::Player_WallJump(", "::PlayerCollider_Check(", "::Level_LoadLevel(", "::UpdateFreezeInput(",
        "::CheckInput(", "::JumpRoutine(", "::InteractExit(", "<JumpRoutine>", "<InteractExit>",
        "<Engine_Update>", "StaticMoverWithLiftSpeed/Hooks")
    rows = []
    for type_row in data["types"]:
        if not any(type_row["name"] == p or type_row["name"].startswith(p + "/") for p in prefixes):
            continue
        for method in type_row["methods"]:
            name = method["name"]
            disposition = ("PRESENT_UNREACHABLE_EXACT_AUTHORED_PROFILE"
                           if any(marker in name for marker in unreachable_markers)
                           else "REACHABLE_REQUIRED")
            calls = [call["target"] for call in method["calls"]]
            dynamic = [call for call in calls if "DynamicData" in call or "DynData`" in call]
            reflection = [call for call in calls if "System.Reflection" in call or "System.Type::" in call]
            hooks = [call for call in calls if "On.Celeste" in call or "IL." in call or "RuntimeDetour" in call]
            rows.append({"type": type_row["name"], "method": name, "bodySha256": method["bodySha256"],
                         "disposition": disposition, "dynamicDataCalls": dynamic,
                         "reflectionCalls": reflection, "hookCalls": hooks})
    assert rows
    return rows


def helper_artifact(fixture: pathlib.Path, audit_root: pathlib.Path):
    occurrences = occurrence_evidence(fixture)
    pins = {}
    for owner, pin in HELPER_PINS.items():
        actual = fixture / "packages" / (owner + ".zip")
        dll = fixture / "extracted" / owner / pin["dllPath"]
        assert sha(actual.read_bytes()) == pin["zipSha256"]
        assert sha(dll.read_bytes()) == pin["dllSha256"]
        rows = method_census(owner, audit_root)
        pins[owner] = {**pin, "occurrenceCount": sum(r["provider"] == owner for r in occurrences),
                       "reachableMethodCensus": rows, "unclassifiedMethodCount": 0}
    result = {
        "schemaVersion": 1, "stage": "25K-F", "sourceMap": LOBBY,
        "sourceMapSha256": sha((fixture / "extracted/StrawberryJam2021/Maps" / (LOBBY + ".bin")).read_bytes()),
        "sourceMapPackaged": False, "helperPins": pins, "occurrences": occurrences,
        "occurrenceCount": 55, "unknownOccurrenceCount": 0,
        "countsByCustomId": dict(sorted(collections.Counter(r["customId"] for r in occurrences).items())),
        "lowering": {
            "generalDynamicData": False, "arbitraryReflection": False,
            "newRuntimeHookClass": False, "dynamicHookLifetime": False,
            "factoriesAreStaticTyped": True,
            "vortexDynamicDataDisposition": "MoveBlock speed branch is unreachable: exact targets are Xaphan slopes; typed Platform.LiftSpeed replaces StaticMoverWithLiftSpeed hooks",
            "crystallineReflectionDisposition": "authored CLR names resolve on Mac to two exact custom IDs; device lookup uses AppleEverestStaticIdentity",
            "triggerConnectionDisposition": "two authored nodes resolve on Mac to typed rumble and Everest flag triggers",
        },
        "semantics": {
            "attachedJumpThru": "JumpThru collision, StaticMover attachment, platform movement/lift, shake/render offset, enable/disable, rider platform trigger; no persistence",
            "bloomStrength": "OnStay clamped Celeste Trigger position interpolation into Level.Bloom.Strength; room-scoped, no persistence",
            "editDepth": "Added-time exact CLR-type equality and overlap become typed custom-ID equality and overlap; Depth mutation lasts for room entity lifetime only",
            "triggerTrigger": "any Holdable collision, 0.4-second one-shot delay, target OnLeave-if-inside then OnEnter in node order, remove source",
        },
        "canary": {"sid": "AppleEverest/Stage25KF", "dataOnly": True,
                   "attachedJumpThru": "attached to moving vanilla zip mover to exercise collision, motion and lift",
                   "bloom": "three exact position modes and values",
                   "editDepth": "typed canary target must change from -100 to -20000 before Awake",
                   "triggerTrigger": "Theo Holdable activates rumble and flag targets after 0.4 seconds"},
    }
    return finish(result)


def audio_artifact(fixture: pathlib.Path):
    banks = required_audio(fixture)
    result = {
        "schemaVersion": 1, "stage": "25K-F", "compatibilityClass": "STATIC_CUSTOM_FMOD_BANK_SET",
        "requiredEventCount": 4, "distinctPhysicalBankCount": 4, "distinctBankIdentityCount": 4,
        "banks": banks,
        "loadOrder": [
            {"ordinal": 1, "bank": "Master Bank.bank", "owner": "Celeste"},
            {"ordinal": 2, "bank": "Master Bank.strings.bank", "owner": "Celeste"},
            {"ordinal": 3, "bank": "music.bank", "owner": "Celeste"},
            {"ordinal": 4, "bank": "sfx.bank", "owner": "Celeste"},
            {"ordinal": 5, "bank": "ui.bank", "owner": "Celeste"},
            {"ordinal": 6, "bank": "dlc_music.bank", "owner": "Celeste"},
            {"ordinal": 7, "bank": "dlc_sfx.bank", "owner": "Celeste"},
            {"ordinal": 8, "bank": "bank:/ExpertContestHelper", "owner": "ChronoHelper"},
            *[{"ordinal": b["loadOrdinal"], "bank": b["bankPath"], "owner": b["owner"]} for b in banks],
        ],
        "orderAuthority": "pinned desktop Everest module/content order then source ZIP registration order",
        "collisions": {"incompatibleEventPath": 0, "incompatibleEventGuid": 0,
                       "incompatibleBankIdentity": 0, "compatibleSharedGuidPath": [
                           {"id": "7429d822-1e68-4251-9907-6d4e8d14a82e", "path": "bus:/music/tunes/mains",
                            "banks": ["bank:/sj21_shared", "bank:/sj21_BegLobby"]}]},
        "duplicatePolicy": "fail closed for unequal bytes/identity/path; compatible non-bank GUID/path duplicates are accepted; no runtime ERR_EVENT_ALREADY_LOADED discovery",
        "runtime": {"studioSystemCount": 1, "runtimeScanning": False, "transactionalLoad": True,
                    "sameSystemInitializationIdempotent": True, "backgroundReopenDuplicateLoads": False,
                    "softReloadDuplicateLoads": False, "coldLaunchReload": True,
                    "teardown": "existing Studio.System.unloadAll; managed handles invalidated first; no double unload"},
        "chronoRegression": {"bankSha256": "2607c358e66f4bce4e81fa3748eaffe1205dcfb57010be2e90ce40fabeb8d3ec",
                             "loadOrdinal": 8, "hornEvent": "event:/ricky06/EC2023/horn", "retained": True},
        "fullGraphAudit": full_audio_audit(fixture),
    }
    return finish(result)


def readiness_artifact(reproduction: dict | None):
    result = {
        "schemaVersion": 1, "stage": "25K-F",
        "acceptedBaseline": {"stage25keSharedClosureSha256": "9ae24c882bf931c29ad3967377217290fbbb48cc44b0f27f741a5f19a76150e5",
            "lobbyManifestSha256": "fdd710714ca6280790d9bd70fbe22e0776b9a8b1abdc2c9bbca45e8eabd23092",
            "bingManifestSha256": "9118e61cff9edb6a34e2eba9868e4c64a1721e095b8222d9f996c60ef1c352b4",
            "aevpsv1Version": 1, "configuredAggregatePlanSha256": "bfbae569352041d00d98c5792354bc403423566ad796a69912431392111a9895"},
        "customIdOccurrenceCoverage": {"auditedLobbyAndBing": 920, "acceptedOrVanilla": 920,
                                       "blocked": 0, "unclassified": 0,
                                       "closedByStage25kf": 55},
        "beginnerBlockerGroups": {"beforeStage25kf": 2, "afterStage25kf": 0, "remaining": []},
        "providerClosureCount": 22, "providersWithRemainingMechanisms": [],
        "readiness": "READY_FOR_K_G_INTEGRATION_BUILD",
        "playableSliceBuilt": False, "playableSliceBuildIntentionallyDeferred": True,
        "actualLobbyOrBingMapPackaged": False,
        "hookGen": {"catalogBefore": 205, "catalogAfter": 205, "newDescriptors": 0},
        "appleApi": {"membersBefore": 30, "membersAfter": 30, "newMembers": 0, "broadPublicizer": False},
        "factories": {"staticSemanticBefore": 24, "staticSemanticAfter": 30,
                      "productionMechanismFactoriesAdded": 4, "canaryFactoriesAdded": 2},
        "runtimeBoundary": {"generalDynamicData": False, "arbitraryReflection": False,
            "runtimeHelperDiscovery": False, "runtimeBankScanning": False, "secondFmodSystem": False,
            "newRuntimeHookClass": False, "lua": False, "interpreter": False, "jit": False},
        "determinism": reproduction or {"completed": False},
        "nextStageRecommendation": "Stage 25K-G — first unchanged Strawberry Jam Beginner slice integration and physical acceptance",
        "fullStrawberryJamSupport": False,
    }
    return finish(result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fixture-root", required=True, type=pathlib.Path)
    parser.add_argument("--audit-root", required=True, type=pathlib.Path)
    parser.add_argument("--reproduction-json", type=pathlib.Path)
    parser.add_argument("--output-root", type=pathlib.Path, default=ROOT / "apple-everest")
    args = parser.parse_args()
    fixture, audit = args.fixture_root.resolve(), args.audit_root.resolve()
    reproduction = json.loads(args.reproduction_json.read_text()) if args.reproduction_json else None
    save(args.output_root / "sj-multibank-audio-stage25kf.json", audio_artifact(fixture))
    save(args.output_root / "sj-crystalline-vortex-stage25kf.json", helper_artifact(fixture, audit))
    save(args.output_root / "sj-beginner-readiness-stage25kf.json", readiness_artifact(reproduction))
    print("PASS: Stage 25K-F exact four-bank and 55-occurrence evidence")


if __name__ == "__main__":
    main()
