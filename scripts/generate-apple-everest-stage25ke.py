#!/usr/bin/env python3
"""Reproduce bounded SJ Beginner metadata from K-C's exact distributed inputs.

No input assembly is loaded. Full map trees and DLL census intermediates stay
in the ignored work directory; tracked outputs contain metadata and hashes.
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import importlib.util
import json
import pathlib
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("stage25kc", ROOT / "scripts/generate-apple-everest-stage25kc.py")
kc = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = kc
spec.loader.exec_module(kc)
LOBBY = "StrawberryJam2021/0-Lobbies/1-Beginner"
BING = "StrawberryJam2021/1-Beginner/Bing_Over_Google"
OWNERS = ["ContortHelper", "ExtendedVariantMode", "JungleHelper", "YetAnotherHelper", "StrawberryJam2021"]
CLASSIFICATIONS = ["REQUIRED_BY_SLICE", "DEPENDENCY_GLOBAL_REQUIRED", "PRESENT_BUT_UNREACHABLE",
                   "OPTIONAL", "UNSUPPORTED_NOT_REQUIRED"]
HELPERS = OWNERS[:4]
EVM_DYNAMIC_SITES = [
    "System.Void ExtendedVariants.Module.ExtendedVariantsModule::hookStuffRightNow()",
    "System.Void ExtendedVariants.Module.ExtendedVariantsModule::initializeStuff()",
    "System.Void ExtendedVariants.UI.VanillaVariantOptions::Load()",
    "System.Void ExtendedVariants.Variants.DisableWallJumping::Load()",
    "System.Void ExtendedVariants.Variants.Gravity::Load()",
    "System.Void ExtendedVariants.Variants.InvertHorizontalControls::Load()",
    "System.Void ExtendedVariants.Variants.InvertVerticalControls::Load()",
    "System.Void ExtendedVariants.Variants.NoFreezeFrames::Load()",
    "System.Void ExtendedVariants.Variants.Stamina::Load()",
]


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def load(path):
    return json.loads(path.read_text())


def save(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, sort_keys=True, ensure_ascii=False) + "\n")


def digest(data):
    return sha(kc.canonical(data))


def parse_map(path):
    reader = kc.Reader(path.read_bytes())
    assert reader.string() == "CELESTE MAP"
    reader.string()
    size = reader.i16()
    assert 0 < size <= 8192
    table = [reader.string() for _ in range(size)]
    count = 0

    def element(depth=0):
        nonlocal count
        count += 1
        assert count <= 2_000_000 and depth <= 64
        name = table[reader.i16()]
        attrs = {}
        for _ in range(reader.u8()):
            key, tag = table[reader.i16()], reader.u8()
            if tag == 0: value = bool(reader.u8())
            elif tag == 1: value = reader.u8()
            elif tag == 2: value = reader.i16()
            elif tag == 3: value = reader.i32()
            elif tag == 4: value = reader.f32()
            elif tag == 5: value = table[reader.i16()]
            elif tag == 6: value = reader.string()
            elif tag == 7:
                # Compressed tile/decal data is not needed for this manifest.
                reader.take(reader.i16())
                value = None
            else: raise ValueError(f"unsupported map value tag {tag}")
            attrs[key] = value
        children = reader.i16()
        assert children >= 0
        return {"name": name, "attrs": attrs,
                "children": [element(depth + 1) for _ in range(children)]}

    tree = element()
    assert len(reader.data) - reader.pos <= 2_097_152
    return tree, reader.pos


def walk(tree, parent="", room=""):
    if tree["name"] == "level": room = tree["attrs"]["name"]
    yield tree, parent, room
    for child in tree["children"]:
        yield from walk(child, tree["name"], room)


def manifest(sid, fixture, usage):
    path = fixture / "extracted/StrawberryJam2021/Maps" / (sid + ".bin")
    tree, boundary = parse_map(path)
    data = path.read_bytes()
    audited = next(row for row in usage["maps"] if row["sid"] == sid)
    assert sha(data) == audited["sha256"]
    rooms, counts, feature_attrs, panels, journals, backdrops = [], collections.Counter(), {}, [], [], []
    vanilla_counts = collections.Counter()
    decals = []
    providers = {(row["kind"], row["id"]): row["providers"] for row in usage["usage"]}
    for item, parent, room in walk(tree):
        name, attrs = item["name"], item["attrs"]
        if name == "level": rooms.append(attrs["name"])
        if parent in ("fgdecals", "bgdecals"): decals.append(attrs.get("texture", ""))
        kind = {"entities": "entity", "triggers": "trigger",
                "Backgrounds": "backdrop", "Foregrounds": "backdrop"}.get(parent)
        if not kind: continue
        if parent in ("Backgrounds", "Foregrounds"):
            backdrops.append({"id": name, "layer": parent, "attributes": attrs})
        if (kind, name) in providers: counts[(kind, name)] += 1
        else: vanilla_counts[name] += 1
        owner = providers.get((kind, name), [])
        if any(o in OWNERS for o in owner):
            feature_attrs.setdefault((kind, name), []).append({k: v for k, v in attrs.items()
                if k not in {"x", "y", "width", "height", "id"}})
        if name in {"SJ2021/StrawberryJamJar", "CollabUtils2/ChapterPanelTrigger"}:
            panels.append({"sourceId": name, "entityId": attrs["id"], "room": room,
                "map": attrs.get("map", ""), "returnMode": attrs.get("returnToLobbyMode", ""),
                "allowSaving": attrs.get("allowSaving", False), "sprite": attrs.get("sprite"),
                "boundsDerivedFromJar": name == "SJ2021/StrawberryJamJar"})
        if name == "CollabUtils2/JournalTrigger":
            journals.append({"room": room, "entityId": attrs["id"],
                "attributes": {k: v for k, v in attrs.items() if k not in {"x", "y", "width", "height", "id"}}})
    custom = [{"kind": k, "id": n, "occurrences": count, "providers": providers[(k, n)]}
              for (k, n), count in sorted(counts.items())]
    profile = [{"kind": k, "id": n, "occurrences": len(rows),
                "distinctAttributes": sorted({kc.canonical(r).decode().strip(): r for r in rows}.values(), key=digest),
                "attributeSequenceSha256": digest(rows)}
               for (k, n), rows in sorted(feature_attrs.items())]
    result = {"schemaVersion": 1, "stage": "25K-E", "sid": sid,
        "sourceMapSha256": sha(data), "fileBytes": len(data), "consumedRootBytes": boundary,
        "appendixBytes": len(data) - boundary, "appendixSha256": sha(data[boundary:]),
        "sourceModified": False, "packagedAsPlayableSlice": False,
        "rooms": sorted(rooms), "roomCount": len(rooms),
        "progressionCompatibilityId": sha(("apple-everest-progression-map-v1\n" + sid + "\n" + sha(data) + "\n" + "\n".join(sorted(rooms))).encode()),
        "customIds": custom, "providers": sorted({p for row in custom for p in row["providers"]}),
        "helperRootFeatures": profile, "chapterPanels": sorted(panels, key=lambda row: row["entityId"]),
        "journalTriggers": sorted(journals, key=lambda row: row["entityId"]),
        "audioEventsFromMap": audited["audioEvents"], "berryCountFromKc": audited["berryCount"],
        "vanillaEntityAndBackdropCounts": dict(sorted(vanilla_counts.items())),
        "guardEvidence": {"decalCount": len(decals), "groupedParallaxDecalCount": sum("sjgroupedparallaxdecals" in d.lower() for d in decals),
            "taggedVanillaHeatWaveCount": sum(b["id"] == "heatwave" and b["attributes"].get("tag", "").startswith("sjstylemask_") for b in backdrops),
            "stylegroundTagSets": sorted({b["attributes"].get("tag", "") for b in backdrops if b["attributes"].get("tag", "").startswith("sjstylemask_")})}}
    if sid == LOBBY:
        meta_path = path.with_suffix(".meta.yaml")
        meta_text = meta_path.read_text()
        music = re.search(r"^\s*BackgroundMusic:\s*(\S+)\s*$", meta_text, re.MULTILINE)
        assert music
        associated = sorted({row["map"] for row in panels if row["sourceId"] == "SJ2021/StrawberryJamJar"})
        result["collab"] = {"id": (fixture / "extracted/StrawberryJam2021/CollabUtils2CollabID.txt").read_text().strip(),
            "difficulty": "Beginner", "journalLevelSet": "StrawberryJam2021/1-Beginner",
            "jarAssociatedMaps": associated, "jarCount": len(associated), "installedSubordinateMaps": [BING],
            "completionAuthority": "vanilla AreaStats.Modes[0].Completed through existing static progression descriptor",
            "returnAuthority": "CollabUtils2 SetReturnToHere: generated jar panel at jar Position-(24,32), node Position-(0,32)",
            "journalPrimitive": "AppleEverestCollabJournalProgress",
            "journalRowsWhenOnlySliceInstalled": [BING]}
        result["lobbyMetadata"] = {"sourceSha256": sha(meta_path.read_bytes()),
            "backgroundMusic": music.group(1), "difficulty": "Beginner"}
    result["manifestSha256"] = digest(result)
    return result


def pin_for(graph, owner):
    node = next(row for row in graph["nodes"] if row["name"] == owner)
    return {"name": owner, "version": node["resolvedVersion"], "zipSha256": node["zipSha256"],
        "zipBytes": node["zipBytes"], "publicUrl": node["publicUrl"],
        "distributedDlls": node["distributedDlls"], "productionAuthority": "PINNED_DISTRIBUTED_BINARY"}


def method_class(owner, type_name, method):
    name = method["name"]
    calls = [row["target"] for row in method["calls"]]
    dangerous = any(any(marker in target for marker in ("MonoMod.Utils.DynamicData", "MonoMod.Utils.DynData`",
        "MonoMod.RuntimeDetour", "MonoMod.Cil", "Mono.Cecil", "System.Reflection", "Assembly::Load")) for target in calls)
    required = {
        "ContortHelper": ("ContortHelper.AbstractTrigger", "ContortHelper.MadelineSpotlightModifierTrigger",
            "ContortHelper.EaseUtilities"),
        "ExtendedVariantMode": ("ExtendedVariants.Entities.ForMappers.AbstractExtendedVariantTrigger",
            "ExtendedVariants.Entities.ForMappers.FloatExtendedVariantFadeTrigger",
            "ExtendedVariants.Entities.ForMappers.ResetVariantsTrigger", "ExtendedVariants.Variants.BackgroundBrightness"),
        "JungleHelper": ("Celeste.Mod.JungleHelper.Entities.MossyWall",),
        "YetAnotherHelper": ("Celeste.Mod.YetAnotherHelper.Entities.BubblePushField",
            "Celeste.Mod.YetAnotherHelper.Entities.BubbleParticle"),
        "StrawberryJam2021": ("Celeste.Mod.StrawberryJam2021.Entities.GlowController",
            "Celeste.Mod.StrawberryJam2021.Entities.StrawberryJamJar",
            "Celeste.Mod.StrawberryJam2021.StylegroundMasks.AllInOneMask",
            "Celeste.Mod.StrawberryJam2021.StylegroundMasks.BloomMask",
            "Celeste.Mod.StrawberryJam2021.StylegroundMasks.Mask",
            "Celeste.Mod.StrawberryJam2021.StylegroundMasks.MaskHooks",
            "Celeste.Mod.StrawberryJam2021.StylegroundMasks.StylegroundMask"),
    }[owner]
    global_required = {
        "ExtendedVariantMode": ("ExtendedVariants.Module.ExtendedVariantsModule",),
        "StrawberryJam2021": ("Celeste.Mod.StrawberryJam2021.StrawberryJam2021Module",
            "Celeste.Mod.StrawberryJam2021.StrawberryJam2021Settings",
            "Celeste.Mod.StrawberryJam2021.StrawberryJam2021SaveData",
            "Celeste.Mod.StrawberryJam2021.StrawberryJam2021Session",
            "Celeste.Mod.StrawberryJam2021.TogglePlaybackHandler"),
    }.get(owner, ())
    if owner == "ExtendedVariantMode" and name in EVM_DYNAMIC_SITES:
        return "UNSUPPORTED_NOT_REQUIRED"
    if type_name.startswith(required):
        return "REQUIRED_BY_SLICE"
    if type_name.startswith(global_required):
        return "DEPENDENCY_GLOBAL_REQUIRED"
    if (owner == "ContortHelper" and type_name.startswith("ContortHelper.EntityDatas") and
            ("::Color(" in name or "::Easer(" in name or "<Color>" in name or "<Easer>" in name)):
        return "REQUIRED_BY_SLICE"
    if owner == "StrawberryJam2021" and ("DashSequenceDisplay" in type_name or "DashSequenceController" in type_name):
        return "OPTIONAL"
    return "UNSUPPORTED_NOT_REQUIRED" if dangerous else "PRESENT_BUT_UNREACHABLE"


def classified_census(owner, audit_root):
    audit = load(audit_root / (owner + "-semantics.json"))
    records, type_records = [], []
    dynamic_calls_all = dynamic_calls_reachable = reflection_calls_all = reflection_calls_reachable = 0
    for type_row in audit["types"]:
        counts = collections.Counter()
        methods = []
        for method in type_row["methods"]:
            classification = method_class(owner, type_row["name"], method)
            assert classification in CLASSIFICATIONS
            counts[classification] += 1
            call_targets = [row["target"] for row in method["calls"]]
            dynamic = sum("MonoMod.Utils.DynamicData" in target or "MonoMod.Utils.DynData`" in target for target in call_targets)
            reflection = sum("System.Reflection" in target or "Assembly::Load" in target for target in call_targets)
            dynamic_calls_all += dynamic
            reflection_calls_all += reflection
            if classification in ("REQUIRED_BY_SLICE", "DEPENDENCY_GLOBAL_REQUIRED"):
                dynamic_calls_reachable += dynamic
                reflection_calls_reachable += reflection
            row = {"name": method["name"], "bodySha256": method["bodySha256"], "classification": classification}
            records.append({"type": type_row["name"], **row})
            methods.append(row)
        type_records.append({"name": type_row["name"], "baseType": type_row["baseType"],
            "fieldCount": len(type_row["fields"]), "propertyCount": len(type_row["properties"]),
            "methodClassCounts": dict(sorted(counts.items())), "methods": methods})
    summary = collections.Counter(row["classification"] for row in records)
    assert sum(summary.values()) == len(records)
    return {"assembly": audit["assembly"], "dllSha256": audit["dllSha256"],
        "typeCount": len(type_records), "methodCount": len(records), "unclassifiedMethodCount": 0,
        "classificationCounts": {name: summary[name] for name in CLASSIFICATIONS},
        "methodSurfaceSha256": digest(records), "dynamicData": {"distributedCallCount": dynamic_calls_all,
            "sliceReachableBeforeLowering": dynamic_calls_reachable, "acceptedRuntimeAfterLowering": 0},
        "reflection": {"distributedCallCount": reflection_calls_all,
            "sliceReachableBeforeLowering": reflection_calls_reachable,
            "acceptedRuntimeModuleOrHelperDiscoveryAfterLowering": 0}, "types": type_records}


def attr_profiles(fixture, usage, sid, wanted_providers):
    path = fixture / "extracted/StrawberryJam2021/Maps" / (sid + ".bin")
    tree, _ = parse_map(path)
    providers = {(row["kind"], row["id"]): row["providers"] for row in usage["usage"]}
    rows = collections.defaultdict(list)
    for item, parent, _ in walk(tree):
        kind = {"entities": "entity", "triggers": "trigger", "Backgrounds": "backdrop",
                "Foregrounds": "backdrop"}.get(parent)
        if not kind: continue
        item_providers = providers.get((kind, item["name"]), [])
        if not any(provider in wanted_providers for provider in item_providers): continue
        attrs = {k: v for k, v in item["attrs"].items() if k not in {"x", "y", "id"}}
        rows[(kind, item["name"], tuple(item_providers))].append(attrs)
    return [{"kind": key[0], "id": key[1], "providers": list(key[2]), "occurrences": len(values),
        "distinctAttributes": sorted({kc.canonical(row).decode().strip(): row for row in values}.values(), key=digest),
        "attributeSequenceSha256": digest(values)} for key, values in sorted(rows.items())]


def bank_record(fixture, owner, event, event_guid, bank, bank_guid, relative_bank, relative_guids, reachability):
    bank_path = fixture / "extracted" / owner / relative_bank
    guid_path = fixture / "extracted" / owner / relative_guids
    assert bank_path.is_file() and guid_path.is_file()
    guid_text = guid_path.read_text(errors="strict").lower()
    assert event.lower() in guid_text and event_guid.lower() in guid_text and bank.lower() in guid_text and bank_guid.lower() in guid_text
    return {"provider": owner, "eventPath": event, "eventGuid": event_guid, "owningBank": bank,
        "bankGuid": bank_guid, "bankSourcePath": relative_bank, "bankSha256": sha(bank_path.read_bytes()),
        "guidSourcePath": relative_guids, "guidSourceSha256": sha(guid_path.read_bytes()),
        "reachability": reachability, "stage25keDisposition": "HANDOFF_TO_25K_F_NO_BANK_SHIPPED"}


def build_semantic_artifacts(fixture, audit_root, usage, graph, lobby, bing, reproduction):
    censuses = {owner: classified_census(owner, audit_root) for owner in OWNERS}
    helper_features = attr_profiles(fixture, usage, LOBBY, set(HELPERS))
    assert sum(row["occurrences"] for row in helper_features) == 220  # 209 moss + 4 bubbles + 3 Contort + 4 EVM
    helper = {"schemaVersion": 1, "stage": "25K-E", "status": "CLOSED_BOUNDED_SEMANTICS",
        "classificationVocabulary": CLASSIFICATIONS, "packages": [pin_for(graph, owner) for owner in HELPERS],
        "mapReachability": {LOBBY: helper_features, BING: []},
        "lowerings": [
            {"owner": "ContortHelper", "factory": "ContortHelper/MadelineSpotlightModifierTrigger",
             "behavior": "typed entry tween over Player.Light color/alpha/start/end radii using authored CubeInOut timing"},
            {"owner": "ExtendedVariantMode", "factories": ["ExtendedVariantMode/FloatExtendedVariantFadeTrigger", "ExtendedVariantMode/ResetVariantsTrigger"],
             "behavior": "typed per-session BackgroundBrightness plus fixed render branch after Level.Background; no live hook lifetime",
             "rejectedDynamicLifetimeSites": EVM_DYNAMIC_SITES},
            {"owner": "JungleHelper", "factory": "JungleHelper/MossyWall",
             "behavior": "typed static mover, hitbox, authored moss images and side attachment"},
            {"owner": "YetAnotherHelper", "factory": "YetAnotherHelper/BubbleField",
             "behavior": "typed horizontal Always bubble field, lift and wind state with bounded particle state"}],
        "productionSourceRequired": False, "generalDynamicDataBackend": False, "runtimeDetourContext": False,
        "runtimeHelperAssemblyScanning": False, "surfaceCensuses": [censuses[owner] for owner in HELPERS],
        "omissionControls": reproduction.get("omissions", []) if reproduction else [],
        "unclassifiedMethodCount": sum(censuses[o]["unclassifiedMethodCount"] for o in HELPERS)}
    helper["artifactSha256"] = digest(helper)

    audio = [
        bank_record(fixture, "StrawberryJam2021", "event:/sj21_jamjar-blue", "f81c1b1a-e90e-4442-90a7-8d05db253a0b",
            "bank:/sj21_jamjars", "a8371196-9461-4ff6-8994-7032718615a7", "Audio/sj21_jamjars.bank",
            "Audio/sj21_jamjars.guids.txt", "Beginner lobby jar first-fill transition after subordinate map completion"),
        bank_record(fixture, "StrawberryJam2021AudioB", "event:/sj21_BegLobby", "8e00fa4b-a47b-4270-8607-200aa2e49996",
            "bank:/sj21_BegLobby", "827873b5-86b7-4e1b-9848-e04c83fb7ddf", "Audio/sj21_BegLobby.bank",
            "Audio/sj21_BegLobby.GUIDs.txt", "Beginner lobby map music"),
        bank_record(fixture, "StrawberryJam2021AudioA", "event:/sj21_bingovergoogle", "cbfb24b2-faf6-4db8-bc5a-c096f754724e",
            "bank:/sj21_bingovergoogle", "f023b527-acd0-40a9-a9b3-87e9f686b81c", "Audio/sj21_bingovergoogle.bank",
            "Audio/sj21_bingovergoogle.guids.txt", "Bing_Over_Google map music"),
        bank_record(fixture, "StrawberryJam2021AudioA", "event:/sj21_levelselect", "3de891f8-2a2c-42d8-b243-f522abaa7db5",
            "bank:/sj21_shared", "1068df52-9f57-4e6e-887c-c1d5a961d61d", "Audio/sj21_shared.bank",
            "Audio/sj21_shared.guids.txt", "Beginner lobby chapter-select metadata background music")]
    root_features = attr_profiles(fixture, usage, LOBBY, {"StrawberryJam2021"})
    root = {"schemaVersion": 1, "stage": "25K-E", "status": "BEGINNER_ROOT_CLOSED_ONLY",
        "package": pin_for(graph, "StrawberryJam2021"), "classificationVocabulary": CLASSIFICATIONS,
        "lifecycle": ["constructor", "Load", "Initialize", "LoadContent", "Unload"],
        "moduleActivation": "generated static descriptor and typed constructor; no reflection or runtime assembly activation",
        "settings": {"schema": "typed", "fields": [
            {"name": "DisplayDashSequence", "type": "Boolean", "default": False, "sliceUse": "recorded; dash sequence UI absent from exact maps"},
            {"name": "TogglePlaybacks", "type": "ButtonBinding", "defaultButtons": ["Back"], "defaultKeys": ["Tab"],
             "sliceUse": "ten vanilla playbackTutorial entities in the Beginner lobby"}], "reflectionSerializer": False},
        "saveData": {"durability": "Stage 25F per-module typed YAML aggregate A/B", "schemaVersion": 1,
            "fields": [{"name": "ModifiedThemeMaps", "type": "ordered serialized HashSet<String>", "default": []},
                       {"name": "FilledJamJarSIDs", "type": "ordered serialized HashSet<String>", "default": []}],
            "schemaSha256": "58f135298191f68e6cf6ec27204d82b50f93e0642b5305a3415fe48b898d6d70",
            "unknownFields": "ignored", "unknownVersions": "rejected", "mapProgressionFailureDomain": "separate AEVPSV1 unchanged"},
        "session": {"durability": "Stage 25F typed module session", "schemaVersion": 1,
            "fields": ["MusicWonkyBeatIndex:Int32=0", "CassetteWonkyBeatIndex:Int32=0", "MusicBeatTimer:Single=0",
                "CassetteBeatTimer:Single=0", "CassetteBlocksDisabled:Boolean=true", "CassetteBlocksLastParameter:String=empty",
                "OshiroBSideMode:Boolean=false", "SkateboardEnabled:Boolean=false", "ZeroG:Boolean=false",
                "ExpiringDashRemainingTime:Double=0", "ExpiringDashFlashThreshold:Single=0",
                "RainDensityData:{Density=1,StartDensity=1,EndDensity=1,Duration=0}"],
            "ephemeralUnserialized": ["DashSequenceDisplay: live entity reference; absent from exact maps"]},
        "restoreOrder": ["vanilla numbered-slot save", "AEVPSV1 map progression", "root module SaveData", "root ModuleSession", "lobby/Level load"],
        "mapReachability": {LOBBY: root_features, BING: []},
        "uiJournalCompletion": {"lobbyId": lobby["collab"]["id"], "difficulty": "Beginner",
            "chapterPanels": lobby["chapterPanels"], "journalRowsWhenOnlySliceInstalled": [BING],
            "journalPrimitive": "existing AppleEverestCollabJournalProgress",
            "completionAuthority": "vanilla AreaStats.Modes[0].Completed",
            "returnToLobby": "existing CollabUtils2 SetReturnToHere panel generated at jar bounds and node"},
        "specialBerry": {"provider": "LunaticHelper", "id": "LunaticHelper/StrawberryWithReturn", "occurrences": 1,
            "map": BING, "semantics": "collect, wait 0.3 seconds, then vanilla cassette-fly to current respawn; ignore squish in state 21",
            "audio": {"eventPath": "event:/game/general/cassette_bubblereturn", "eventGuid": "9db695ce-ce56-4025-9528-030d6b599c86",
                "owningBank": "bank:/sfx", "bankGuid": "98263171-3ea2-482b-abb7-9af01e4c002a", "availability": "existing vanilla bank"}},
        "audioHandoff": audio, "customAudioCallsSilentlyDiscarded": 0, "generalDynamicDataBackend": False,
        "runtimeAssemblyScanning": False, "surfaceCensus": censuses["StrawberryJam2021"], "unclassifiedMethodCount": 0}
    root["artifactSha256"] = digest(root)

    remaining = attr_profiles(fixture, usage, LOBBY, {"CrystallineHelper", "VortexHelper"}) + \
                attr_profiles(fixture, usage, BING, {"CrystallineHelper", "VortexHelper"})
    assert sum(row["occurrences"] for row in remaining) == 55
    all_occurrences = sum(row["occurrences"] for row in lobby["customIds"] + bing["customIds"])
    readiness = {"schemaVersion": 1, "stage": "25K-E", "status": "DEVELOPMENT_INTEGRATION_READY",
        "playableSliceBuilt": False, "playableSliceBuildIntentionallyDeferred": True,
        "beginnerBlockerGroups": {"stage25kc": 7, "stage25kd": 4, "stage25ke": 2,
            "remaining": ["bounded multi-bank FMOD", "CrystallineHelper/VortexHelper exact bounded mechanisms"]},
        "unclassified": 0, "mapsStillBlocked": [LOBBY, BING], "remainingMechanismSurface": remaining,
        "providerClosureCount": 22, "providerClosure": ["BrokemiaHelper", "CherryHelper", "CollabUtils2", "ContortHelper",
            "CrystallineHelper", "DJMapHelper", "EverestCore", "ExtendedVariantMode", "FancyTileEntities", "FemtoHelper",
            "FlaglinesAndSuch", "FrostHelper", "HonlyHelper", "JungleHelper", "LunaticHelper", "MaxHelpingHand",
            "PandorasBox", "StrawberryJam2021", "VivHelper", "VortexHelper", "XaphanHelper", "YetAnotherHelper"],
        "providersWithRemainingMechanisms": ["CrystallineHelper", "VortexHelper"],
        "newlyClosedProviders": HELPERS + ["StrawberryJam2021"],
        "customIdOccurrenceCoverage": {"auditedLobbyAndBing": all_occurrences,
            "closedByStage25keHelpers": sum(row["occurrences"] for row in helper_features),
            "closedByStage25keRoot": sum(row["occurrences"] for row in root_features),
            "closedByStage25keSpecialBerry": 1,
            "previouslyClosedOrVanilla": all_occurrences - sum(row["occurrences"] for row in helper_features) -
                sum(row["occurrences"] for row in root_features) - 1 - 55,
            "remainingMechanismOccurrences": 55},
        "manifestHashes": {"lobby": lobby["manifestSha256"], "bing": bing["manifestSha256"]},
        "runtimeBoundary": {"generalDynamicData": False, "runtimeDetourContext": False, "runtimeAssemblyLoad": False,
            "runtimeHelperDiscovery": False, "lua": False, "interpreter": False, "jit": False},
        "hookGen": {"catalogBefore": 205, "catalogAfter": 205, "newDescriptors": 0,
            "appleApiMembersBefore": 30, "appleApiMembersAfter": 30, "newPublicMembers": 0,
            "exactInternalSourceAccesses": ["Celeste.Player.noWindTimer", "Celeste.Player.windDirection",
                "Celeste.Player.windTimeout", "Celeste.SoundSource.instance"], "broadPublicizer": False},
        "configuredOrdering": {"class": "STATIC_CONFIGURED_DETOUR_SEQUENCE", "evmNineSitesRemainRejected": True,
            "lunaticFixturePlanSha256": "bfbae569352041d00d98c5792354bc403423566ad796a69912431392111a9895"},
        "mapAppendix": {"mapsWithAppendix": 124, "totalBytes": 19240291, "maxBytes": 1260932,
            "hardPerMapBound": 2097152, "originalHashesAndProgressionIdentitiesUnchanged": True},
        "determinism": reproduction or {}, "audioHandoffCount": len(audio),
        "nextStageRecommendation": "one Stage 25K-F is bounded if four custom banks and the 55 authored Crystalline/Vortex occurrences remain within their audited exact mechanisms; split F1/F2 if either mechanism audit broadens",
        "boundedStagesBeforeFirstSlice": 1, "plannedFirstSliceStage": "25K-G"}
    readiness["artifactSha256"] = digest(readiness)
    return helper, root, readiness


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fixture-root", type=pathlib.Path, required=True)
    parser.add_argument("--audit-root", type=pathlib.Path, required=True,
                        help="output from AppleEverestBuilder census-semantics for the five exact DLLs")
    parser.add_argument("--reproduction-json", type=pathlib.Path,
                        help="optional completed three-run/omission result")
    parser.add_argument("--output-root", type=pathlib.Path, default=ROOT / "apple-everest")
    args = parser.parse_args()
    usage = load(ROOT / "apple-everest/strawberry-jam-content-usage-stage25kc.json")
    graph = load(ROOT / "apple-everest/strawberry-jam-dependency-graph-stage25kc.json")
    for owner in OWNERS:
        pin = next(n for n in graph["nodes"] if n["name"] == owner)
        assert sha((args.fixture_root / "packages" / (owner + ".zip")).read_bytes()) == pin["zipSha256"], owner
        for dll in pin["distributedDlls"]:
            # K-C's census path is assembly-relative for some nested releases.
            candidates = list((args.fixture_root / "extracted" / owner).rglob(pathlib.Path(dll["path"]).name))
            assert sum(sha(path.read_bytes()) == dll["sha256"] for path in candidates) == 1, owner
    manifests = {name: manifest(sid, args.fixture_root, usage)
                 for name, sid in [("lobby", LOBBY), ("bing", BING)]}
    for name, value in manifests.items():
        save(args.output_root / f"sj-beginner-{name}-manifest-stage25ke.json", value)
    reproduction = load(args.reproduction_json) if args.reproduction_json else None
    helper, root, readiness = build_semantic_artifacts(args.fixture_root, args.audit_root, usage, graph,
        manifests["lobby"], manifests["bing"], reproduction)
    save(args.output_root / "sj-helper-semantics-stage25ke.json", helper)
    save(args.output_root / "sj-root-semantics-stage25ke.json", root)
    save(args.output_root / "sj-beginner-readiness-stage25ke.json", readiness)
    print("PASS: exact bounded Beginner manifests and zero-unknown semantic artifacts; playable slice remains unbuilt")


if __name__ == "__main__":
    main()
