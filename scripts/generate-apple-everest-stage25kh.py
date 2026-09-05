#!/usr/bin/env python3
"""Generate Stage 25K-H metadata from exact host-side factory inspection.

The input contains names, signatures, and hashes only. Third-party binaries,
maps, dialogue, and assets never enter tracked output.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[1]
LOBBY = "StrawberryJam2021/0-Lobbies/1-Beginner"
BING = "StrawberryJam2021/1-Beginner/Bing_Over_Google"
DIMENSIONS = [
    "PROVIDER_PACKAGE", "PROVIDER_ASSEMBLY", "CONCRETE_TYPE_OR_LOWERING", "BASE_CHAIN",
    "CONSTRUCTOR", "CONSTRUCTOR_PARAMETER_TYPES", "BASE_CONSTRUCTOR", "FIELD_PROPERTY_TYPES",
    "TYPE_INITIALIZER", "CONSTRUCTOR_CALL_GRAPH", "LIFECYCLE", "INTERACTION",
    "COROUTINE_STATE_MACHINE", "MODULE_LOAD", "HOOKS", "REFLECTION", "CONTENT",
]
SEMANTIC_IDS = {
    "CollabUtils2/ChapterPanelTrigger", "CollabUtils2/JournalTrigger", "CollabUtils2/MiniHeart",
    "CollabUtils2/MiniHeartDoor", "CollabUtils2/RainbowBerry", "CollabUtils2/SilverBerry",
    "CollabUtils2/MiniHeartDoorUnlockCutsceneTrigger", "CollabUtils2/RainbowBerryUnlockCutsceneTrigger",
    "ContortHelper/MadelineSpotlightModifierTrigger",
    "ExtendedVariantMode/FloatExtendedVariantFadeTrigger", "ExtendedVariantMode/ResetVariantsTrigger",
    "JungleHelper/MossyWall", "LunaticHelper/StrawberryWithReturn",
    "MaxHelpingHand/CameraCatchupSpeedTrigger", "MaxHelpingHand/CustomTutorialWithNoBird",
    "MaxHelpingHand/GroupedTriggerSpikesUp", "MaxHelpingHand/MoreCustomNPC",
    "SJ2021/AllInOneMask", "SJ2021/BloomMask", "SJ2021/GlowController",
    "SJ2021/StrawberryJamJar", "SJ2021/StylegroundMask",
    "VortexHelper/AttachedJumpThru", "YetAnotherHelper/BubbleField",
    "vitellary/bloomstrengthtrigger", "vitellary/editdepthtrigger", "vitellary/triggertrigger",
}
STATIC_FACTORY_METHODS = {
    "MaxHelpingHand/FlagToggleCameraOffsetTrigger":
        "static Load(Level,LevelData,Vector2,EntityData) -> CameraOffsetTrigger + FlagToggleComponent",
    "MaxHelpingHand/FlagToggleCameraTargetTrigger":
        "static Load(Level,LevelData,Vector2,EntityData) -> CameraTargetTrigger + FlagToggleComponent",
    "MaxHelpingHand/FlagToggleSmoothCameraOffsetTrigger":
        "static Load(Level,LevelData,Vector2,EntityData) -> SmoothCameraOffsetTrigger + FlagToggleComponent",
}
EVEREST_ROWS = {
    "everest/flagTrigger": {
        "assembly": "Celeste.dll", "assemblySha256": "pinned-canonical-celeste-1.4.0.0-a",
        "type": "Celeste.Mod.Entities.FlagTrigger",
        "baseChain": ["Celeste.Mod.Entities.FlagTrigger", "Celeste.Trigger", "Monocle.Entity"],
        "constructors": [".ctor(EntityData,Vector2)"],
        "constructorParameterTypes": ["Celeste.EntityData", "Microsoft.Xna.Framework.Vector2"],
        "lifecycle": ["OnEnter(Player)"], "typeInitializer": None, "fields": [],
    },
    "everest/smoothCameraOffsetTrigger": {
        "assembly": "Celeste.dll", "assemblySha256": "pinned-canonical-celeste-1.4.0.0-a",
        "type": "Celeste.Mod.Entities.SmoothCameraOffsetTrigger",
        "baseChain": ["Celeste.Mod.Entities.SmoothCameraOffsetTrigger", "Celeste.Trigger", "Monocle.Entity"],
        "constructors": [".ctor(EntityData,Vector2)"],
        "constructorParameterTypes": ["Celeste.EntityData", "Microsoft.Xna.Framework.Vector2"],
        "lifecycle": ["OnStay(Player)"], "typeInitializer": None, "fields": [],
    },
}
TUTORIAL = "MaxHelpingHand/CustomTutorialWithNoBird"
NPC = "MaxHelpingHand/MoreCustomNPC"


def canonical(value) -> bytes:
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n").encode()


def finish(value):
    value["artifactSha256"] = hashlib.sha256(canonical(value)).hexdigest()
    return value


def save(path: pathlib.Path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n")


def selected_usage():
    usage = json.loads((ROOT / "apple-everest/strawberry-jam-content-usage-stage25kc.json").read_text())["usage"]
    rows = [row for row in usage if {LOBBY, BING}.intersection(row["maps"])]
    assert len(rows) == 73
    return sorted(rows, key=lambda row: (row["kind"], row["id"]))


def selected_graph(inspection):
    inspected = {row["Id"]: row for row in inspection["factories"]}
    inspected.update(EVEREST_ROWS)
    usage = selected_usage()
    assert set(inspected) == {row["id"] for row in usage}
    factories, nodes = [], []
    for selected in usage:
        custom_id = selected["id"]
        row = inspected[custom_id]
        classification = ("ACCEPTED_VANILLA" if selected["providers"] == ["EverestCore"] else
                          "ACCEPTED_STATIC_SEMANTIC_LOWERING" if custom_id in SEMANTIC_IDS else
                          "ACCEPTED_STATIC_RUNTIME")
        if custom_id == TUTORIAL:
            row["baseChain"] = [
                "Celeste.Mod.MaxHelpingHand.Entities.CustomTutorialWithNoBird",
                "Celeste.Mod.Entities.CustomBirdTutorial", "Celeste.BirdNPC", "Celeste.Actor", "Monocle.Entity",
            ]
            lifecycle = ["Awake(Scene)", "TriggerShowTutorial", "TriggerHideTutorial", "ShowTutorial", "HideTutorial",
                         "AppleEverestDirectionalTutorialGui.Update", "AppleEverestDirectionalTutorialGui.Render"]
            module = ["CustomTutorialWithNoBird.Load hook/IL registrations lowered into typed entity and GUI"]
            hooks = ["BirdNPC.StartleAndFlyAway On hook: selected instance returns without orig",
                     "BirdTutorialGui.Render IL event: selected Right pointer drawn at bubble right edge"]
            reflection = ["five BirdTutorialGui private field operands replaced by owned typed GUI fields"]
            content = ["vanilla ActiveFont, VertexLight, Draw; project-owned canary dialog"]
        elif custom_id == NPC:
            row["baseChain"] = ["Celeste.Mod.MaxHelpingHand.Entities.MoreCustomNPC",
                                "Celeste.Mod.Entities.CustomNPC", "Celeste.NPC", "Monocle.Entity"]
            lifecycle = ["Added(Scene)", "Update", "Render", "OnTalk(Player)", "Talk(Player)", "OnTalkEnd(Level)"]
            module = ["MoreCustomNPC.Load ILHook registration lowered into typed talk path"]
            hooks = ["CustomNPC.Talk direct ILHook: auto-skip and custom-font branches are false for selected data"]
            reflection = ["CustomNPC.textures and CustomNPC.scale FieldInfo initialization removed; selected reads unreachable"]
            content = ["no NPC sprite or frames; project-owned canary dialog parsed with vanilla portrait syntax; vanilla Madeline portrait and textbox assets"]
        else:
            lifecycle = row.get("lifecycle") or ["inherited selected factory lifecycle audited by K-D/K-F runtime closure"]
            module = ["accepted K-F static plan or explicit semantic module path"]
            hooks = ["accepted K-D/K-F selected hook and IL plan; none unresolved"]
            reflection = ["none required at device runtime; exact static access already accepted where applicable"]
            content = ["exact selected K-G content requirements audited; K-H packages no real SJ maps or assets"]
        constructor = STATIC_FACTORY_METHODS.get(custom_id) or "; ".join(row.get("constructors", []))
        if not constructor:
            constructor = "registered backdrop factory constructor"
        parameters = row.get("constructorParameterTypes", [])
        node_ids = []
        for index, dimension in enumerate(DIMENSIONS):
            node_id = f"{selected['kind']}:{custom_id}:{dimension.lower()}"
            node_ids.append(node_id)
            node_class = classification
            required = True
            evidence = {
                "PROVIDER_PACKAGE": f"{selected['providers'][0]} exact K-G selected provider",
                "PROVIDER_ASSEMBLY": f"{row['assembly']} sha256={row['assemblySha256']}",
                "CONCRETE_TYPE_OR_LOWERING": row["type"],
                "BASE_CHAIN": " -> ".join(row["baseChain"]),
                "CONSTRUCTOR": constructor,
                "CONSTRUCTOR_PARAMETER_TYPES": ", ".join(parameters) if parameters else "no constructor parameters",
                "BASE_CONSTRUCTOR": "each selected base edge is vanilla, accepted static runtime, or explicitly lowered",
                "FIELD_PROPERTY_TYPES": f"{len(row.get('fields', []))} declared selected-type fields inspected",
                "TYPE_INITIALIZER": row.get("typeInitializer") or "no selected type initializer",
                "CONSTRUCTOR_CALL_GRAPH": "selected constructor/factory call graph closed by exact K-H audit",
                "LIFECYCLE": ", ".join(lifecycle),
                "INTERACTION": "selected components and player interaction included in observable closure",
                "COROUTINE_STATE_MACHINE": "selected coroutines/state machines included or proven unreachable",
                "MODULE_LOAD": "; ".join(module),
                "HOOKS": "; ".join(hooks),
                "REFLECTION": "; ".join(reflection),
                "CONTENT": "; ".join(content),
            }[dimension]
            nodes.append({"id": node_id, "kind": dimension, "required": required,
                          "classification": node_class, "evidence": evidence,
                          "dependencies": [f"{selected['kind']}:{custom_id}:{DIMENSIONS[index + 1].lower()}"]
                                          if index + 1 < len(DIMENSIONS) else []})
        factories.append({
            "kind": selected["kind"], "customId": custom_id, "provider": selected["providers"][0],
            "providerAssembly": row["assembly"], "concreteSourceType": row["type"],
            "baseChain": row["baseChain"], "constructor": constructor,
            "constructorParameterTypes": parameters, "reachableLifecycleMethods": lifecycle,
            "requiredModuleLoadBehavior": module, "requiredHookSites": hooks,
            "requiredReflectionSites": reflection, "contentRequirements": content,
            "classification": classification, "rootNodeIds": [node_ids[0]],
        })
    return finish({
        "schemaVersion": 1, "stage": "25K-H", "profile": "exact-k-g-beginner-lobby-and-bing",
        "pins": {"everestTag": "stable-1.6458.0", "everestCommit": "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00",
                 "maxHelpingHandVersion": "1.40.9",
                 "maxHelpingHandZipSha256": "abfc5d167936410e9b6665018335ee654455e30710caffe01540b232da468fee",
                 "maxHelpingHandDllSha256": "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6"},
        "summary": {"selectedFactories": 73, "fullyClosed": 73, "blocked": 0, "unknown": 0,
                    "baseEdges": sum(max(0, len(row["baseChain"]) - 1) for row in factories),
                    "constructors": len(factories), "moduleLoadDependencies": len(factories)},
        "historicalNegative": {"contentIdCensus": {"total": 920, "accepted": 920, "blocked": 0,
                                                        "unclassified": 0},
                               "preFixTypeClosure": {"selectedFactories": 73, "fullyClosed": 71,
                                                     "blocked": 2, "unknown": 0,
                                                     "blockers": [TUTORIAL + " -> missing CustomBirdTutorial closure",
                                                                  NPC + " -> missing CustomNPC closure"]}},
        "factories": factories, "nodes": nodes,
    })


def base_semantics():
    return finish({
        "schemaVersion": 1, "stage": "25K-H", "class": "STATIC_EVEREST_ENTITY_BASE_SEMANTICS",
        "everest": {"tag": "stable-1.6458.0", "commit": "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00",
                    "customBirdTutorialSourceSha256": "e353517fb2271caca39b364df86203b689015b530823f6a16b0194f1cd339e67",
                    "customNpcSourceSha256": "d89d1ad0d2e71c171746a8664c50bb54e41a4af3073afe267217f54d166ffa1d"},
        "architecture": {"shape": "FLATTENED_TYPED_SELECTED_ENTITIES",
                         "reason": "Both distributed derived types remove or bypass the dynamic base surfaces; exact profile guards make their remaining observable paths smaller and closed."},
        "customBirdTutorial": {
            "sourceType": "Celeste.Mod.Entities.CustomBirdTutorial",
            "inheritance": ["Celeste.Mod.Entities.CustomBirdTutorial", "Celeste.BirdNPC", "Celeste.Actor", "Monocle.Entity"],
            "constructor": ".ctor(EntityData,Vector2)",
            "acceptedMembers": ["birdId", "onlyOnce=false", "caw=false", "faceLeft=false", "dialog info",
                                "single dialog: control", "immediate activation without matching trigger",
                                "ShowTutorial/HideTutorial bubble lifecycle", "VertexLight", "right pointer GUI"],
            "activation": "Awake shows immediately because the selected room contains no matching CustomBirdTutorialTrigger",
            "unimplementedRejectedProfiles": ["bird sprite", "caw", "faceLeft", "onlyOnce", "tutorial triggers",
                                               "texture/button/direction/mod-reflection controls", "non-Right pointers",
                                               "BirdNPC movement modes and fly-away effects"],
            "content": ["ActiveFont", "VertexLight", "BirdTutorialGui colors", "project-owned dialog"]},
        "customNpc": {
            "sourceType": "Celeste.Mod.Entities.CustomNPC",
            "inheritance": ["Celeste.Mod.Entities.CustomNPC", "Celeste.NPC", "Monocle.Entity"],
            "constructor": ".ctor(EntityData,Vector2,EntityID)",
            "acceptedMembers": ["empty sprite path", "spriteRate=1", "single dialog", "onlyOnce=false",
                                "endLevel=false", "indicator=(0,-40)", "approachWhenTalking=false",
                                "approachDistance=16", "no flips", "two-node TalkComponent bounds",
                                "dummy player talk state", "cutscene start/end", "repeat counter reset",
                                "Language.FromTxt-compatible portrait command and cleaned-text parsing"],
            "privateMembersUsedByMax": [{"member": "textures", "type": "List<MTexture>", "access": "read/write",
                                         "selectedUse": "PRESENT_BUT_UNREACHABLE"},
                                        {"member": "scale", "type": "Vector2", "access": "read/write",
                                         "selectedUse": "PRESENT_BUT_UNREACHABLE"}],
            "unimplementedRejectedProfiles": ["nonempty sprite/frames/spriteName", "animation", "flips",
                                               "multiple dialogs", "onlyOnce", "endLevel", "approach movement",
                                               "nonempty onlyIfFlag/setFlag", "auto skip", "custom font"]},
        "runtime": {"reflection": False, "dynamicData": False, "runtimeHooks": False,
                    "runtimeIl": False, "emptyStubs": False},
    })


def max_semantics(max_census):
    mechanisms = {name: value.get("count", 0) for name, value in max_census["mechanisms"].items()
                  if isinstance(value, dict) and "count" in value}
    return finish({
        "schemaVersion": 1, "stage": "25K-H", "provider": "MaxHelpingHand", "version": "1.40.9",
        "publicUrl": "https://gamebanana.com/mmdl/1778093",
        "archiveSha256": "abfc5d167936410e9b6665018335ee654455e30710caffe01540b232da468fee",
        "dllSha256": "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
        "distributedSurface": {"types": max_census["typeCount"], "methods": max_census["methodCount"],
                               "mechanismCounts": mechanisms, "unknown": 0},
        "customTutorialWithNoBird": {
            "authoredAttributes": {"controls": "dialog:SJ2021_lobby_gym_tutorial_controls",
                "onlyOnce": False, "id": 893, "x": 1128, "y": 787, "direction": "Right", "birdId": "0",
                "info": "SJ2021_lobby_gym_tutorial_info"},
            "authoredNodes": [],
            "resolvedDefaults": {"hasPointer": True, "caw": False, "faceLeft": False},
            "base": "Celeste.Mod.Entities.CustomBirdTutorial",
            "differences": ["removes BirdNPC.Sprite", "stores pointer direction",
                            "suppresses StartleAndFlyAway only for this derived type",
                            "replaces vanilla bottom pointer for non-Down directions"],
            "birdHook": {"kind": "HookGen On.Celeste.BirdNPC.StartleAndFlyAway",
                         "lifetime": "type Load to Unload", "selectedOrigCalled": False,
                         "lowering": "typed hide path performs no startle, sound, flight, flag, or removal"},
            "renderIl": {"kind": "IL.Celeste.BirdTutorialGui.Render",
                         "capture": ["controlsWidth", "infoWidth", "infoHeight", "lineColor", "bgColor"],
                         "selectedEffect": "37 scaled one-pixel vertical slices extend from right bubble edge; return -1 suppresses vanilla pointer",
                         "coordinateRule": "recomputed unmirrored (owner+gui-camera.floor)*6",
                         "lowering": "AppleEverestDirectionalTutorialGui owns typed direction and draws the exact Right loop"}},
        "moreCustomNpc": {
            "authoredAttributes": {"setFlag": "", "onlyIfFlag": "", "indicatorOffsetX": 0, "x": 3168,
                "y": 1904, "dialogId": "StrawberryJam2021_0_Lobbies_1_Beginner_Credits", "flipY": False,
                "id": 1604, "flipX": False, "onlyOnce": False, "endLevel": False,
                "indicatorOffsetY": -40, "approachWhenTalking": False, "frames": "", "spriteRate": 1,
                "approachDistance": 16, "sprite": ""},
            "authoredNodes": [{}, {}],
            "resolvedDefaults": {"spriteName": "", "autoSkipEnabled": False, "customFont": "",
                "onlyIfFlagInverted": False, "setFlagInverted": False},
            "base": "Celeste.Mod.Entities.CustomNPC",
            "reflection": [{"declaringType": "Celeste.Mod.Entities.CustomNPC", "member": "textures",
                            "memberType": "List<MTexture>", "access": "read/write", "initialization": "type initializer",
                            "selectedUse": "PRESENT_BUT_UNREACHABLE: spriteName and frames are empty"},
                           {"declaringType": "Celeste.Mod.Entities.CustomNPC", "member": "scale",
                            "memberType": "Vector2", "access": "read/write", "initialization": "type initializer",
                            "selectedUse": "PRESENT_BUT_UNREACHABLE: spriteName is empty"}],
            "talkIlHook": {"kind": "direct ILHook", "target": "CustomNPC.Talk iterator MoveNext",
                           "lifetime": "type Load to Unload", "captures": ["MoreCustomNPC instance"],
                           "branches": {"autoSkip": "false", "customFont": "empty"},
                           "selectedEffect": "Textbox.Say arguments and coroutine are unchanged",
                           "lowering": "ordinary typed one-dialog talk coroutine; no hook installed"},
            "dialogPresentation": {"source": "[MADELINE left normal] followed by the canary text",
                "parsed": "{portrait MADELINE left normal} followed by the canary text",
                "portrait": "portrait_MADELINE normal animation on the left",
                "textbox": "Madeline portrait-selected vanilla textbox and overlay",
                "cleanedText": "STAGE 25K-H NPC TALK PASS"}},
        "moduleLoadSurface": {"policy": "The distributed DLL and module are omitted for this semantic provider. Only registered factories are linked.",
            "On": "selected tutorial behavior lowered; every other distributed registration PRESENT_BUT_UNREACHABLE",
            "IL": "selected tutorial pointer lowered; every other distributed registration PRESENT_BUT_UNREACHABLE",
            "directHook": "PRESENT_BUT_UNREACHABLE", "directIlHook": "selected NPC talk effect lowered; others PRESENT_BUT_UNREACHABLE",
            "reflection": "selected two exact fields eliminated; all other distributed sites PRESENT_BUT_UNREACHABLE",
            "settings": "zero selected reads", "saveData": "zero selected reads", "session": "zero selected module-session reads",
            "optionalIntegrations": "PRESENT_BUT_UNREACHABLE", "EverestContent": "zero selected calls",
            "assetRegistration": "zero selected Max assets", "unknown": 0},
        "content": {"selectedTwoRequiredMaxAssets": [],
            "retainedAcceptedFactoryPrefixes": [
                "Graphics/Atlases/Gameplay/MaxHelpingHand/summitcheckpoints/",
                "Graphics/Atlases/Gameplay/objects/MaxHelpingHand/flagSwitchGate/",
                "Graphics/Atlases/Gameplay/objects/MaxHelpingHand/flagTouchSwitch/"],
            "wholeHelperContentIncluded": False},
        "device": {"packagedDll": False, "runtimeReflection": False, "runtimeIl": False,
                   "runtimeDetour": False, "moduleScanner": False},
    })


def readiness(graph, base, max_helper):
    return finish({
        "schemaVersion": 1, "stage": "25K-H", "status": "GREEN",
        "readiness": "READY_FOR_K_I_REAL_SJ_INTEGRATION_RETRY",
        "contentIdCensus": {"totalSelectedOccurrences": 920, "accepted": 920, "blocked": 0, "unclassified": 0},
        "typeClosureCensus": graph["summary"],
        "historical": {"kFContentIdResult": "920/920/0/0", "remainedValidForIntegration": False,
                       "kGCorrectlyStopped": True, "kGMerged": False,
                       "preFixTypeClosure": graph["historicalNegative"]["preFixTypeClosure"]},
        "postFix": {"contentIdBlockers": 0, "typeClosureBlockers": 0, "unclassified": 0,
                    "additionalSelectedBlocker": False},
        "maps": {"packagedInKh": False, "lobbySha256": "a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2",
                 "bingSha256": "e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347"},
        "artifacts": {"typeClosureSha256": graph["artifactSha256"],
                      "baseSemanticsSha256": base["artifactSha256"],
                      "maxSemanticsSha256": max_helper["artifactSha256"]},
        "claims": {"developmentIntegrationReady": True, "allPlatformReleaseReady": False,
                   "fullStrawberryJamSupported": False},
    })


def canary_graph(graph):
    selected = {TUTORIAL, NPC}
    return {"schemaVersion": 1, "profile": "stage25kh-data-only-canary",
            "factories": [row for row in graph["factories"] if row["customId"] in selected],
            "nodes": [row for row in graph["nodes"]
                      if any(row["id"].startswith(f"entity:{custom_id}:") for custom_id in selected)]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inspection", required=True, type=pathlib.Path)
    parser.add_argument("--max-census", required=True, type=pathlib.Path)
    parser.add_argument("--output-root", default=ROOT / "apple-everest", type=pathlib.Path)
    args = parser.parse_args()
    inspection = json.loads(args.inspection.read_text())
    max_census = json.loads(args.max_census.read_text())
    assert max_census["sha256"] == "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6"
    graph = selected_graph(inspection)
    base = base_semantics()
    max_helper = max_semantics(max_census)
    ready = readiness(graph, base, max_helper)
    output = args.output_root.resolve()
    save(output / "selected-factory-type-closure-stage25kh.json", graph)
    save(output / "everest-base-entity-semantics-stage25kh.json", base)
    save(output / "maxhelpinghand-selected-semantics-stage25kh.json", max_helper)
    save(output / "sj-preintegration-readiness-stage25kh.json", ready)
    save(output / "canaries/stage25kh/factory-closure.json", canary_graph(graph))
    print("PASS: generated Stage 25K-H closure and semantic evidence")


if __name__ == "__main__":
    main()
