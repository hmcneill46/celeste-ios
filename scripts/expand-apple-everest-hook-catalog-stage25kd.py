#!/usr/bin/env python3
"""Generate the exact source-backed Stage 25K-D HookGen catalog delta."""

from __future__ import annotations

import argparse
import json
import pathlib
import re


PATCHED_ONLY = {
    ("On.Celeste.Level", "LoadCustomEntity"),
    ("On.Celeste.PlayerHair", "GetHairColor"),
    ("On.Celeste.Mod.Entities.DialogCutscene", "OnBegin"),
    ("On.Celeste.Mod.Entities.DialogCutscene", "OnEnd"),
    ("On.Celeste.Mod.Meta.MapMeta", "ApplyTo"),
}

PATCHED_ENTRIES = [
    {
        "id": "celeste-level-load-custom-entity",
        "sourceFile": "Celeste/Level.cs",
        "sourceDeclaration": "public static bool LoadCustomEntity(EntityData entityData, Level level)",
        "originalDeclaration": "private static bool AppleEverestOriginal_LoadCustomEntity(EntityData entityData, Level level)",
        "originalAlias": "AppleEverestOriginal_LoadCustomEntity",
        "hookNamespace": "On.Celeste",
        "hookType": "Level",
        "eventName": "LoadCustomEntity",
        "origDelegate": "orig_LoadCustomEntity",
        "hookDelegate": "hook_LoadCustomEntity",
        "isStatic": True,
        "returnType": "bool",
        "parameters": [
            {"type": "global::Celeste.EntityData", "name": "entityData"},
            {"type": "global::Celeste.Level", "name": "level"},
        ],
    },
    {
        "id": "celeste-player-hair-get-hair-color",
        "sourceFile": "Celeste/PlayerHair.cs",
        "sourceDeclaration": "public Color GetHairColor(int index)",
        "originalDeclaration": "private Color AppleEverestOriginal_GetHairColor(int index)",
        "originalAlias": "AppleEverestOriginal_GetHairColor",
        "hookNamespace": "On.Celeste",
        "hookType": "PlayerHair",
        "eventName": "GetHairColor",
        "origDelegate": "orig_GetHairColor",
        "hookDelegate": "hook_GetHairColor",
        "isStatic": False,
        "receiverType": "global::Celeste.PlayerHair",
        "returnType": "global::Microsoft.Xna.Framework.Color",
        "parameters": [{"type": "int", "name": "index"}],
    },
    {
        "id": "celeste-mod-entities-dialog-cutscene-on-begin",
        "sourceFile": "Celeste/Mod/AppleEverestStatic/EverestDialogCutsceneStaticApi.cs",
        "sourceDeclaration": "public override void OnBegin(Level level)",
        "originalDeclaration": "private void AppleEverestOriginal_OnBegin(global::Celeste.Level level)",
        "originalAlias": "AppleEverestOriginal_OnBegin",
        "hookNamespace": "On.Celeste.Mod.Entities",
        "hookType": "DialogCutscene",
        "eventName": "OnBegin",
        "origDelegate": "orig_OnBegin",
        "hookDelegate": "hook_OnBegin",
        "isStatic": False,
        "receiverType": "global::Celeste.Mod.Entities.DialogCutscene",
        "returnType": "void",
        "parameters": [{"type": "global::Celeste.Level", "name": "level"}],
    },
    {
        "id": "celeste-mod-entities-dialog-cutscene-on-end",
        "sourceFile": "Celeste/Mod/AppleEverestStatic/EverestDialogCutsceneStaticApi.cs",
        "sourceDeclaration": "public override void OnEnd(Level level)",
        "originalDeclaration": "private void AppleEverestOriginal_OnEnd(global::Celeste.Level level)",
        "originalAlias": "AppleEverestOriginal_OnEnd",
        "hookNamespace": "On.Celeste.Mod.Entities",
        "hookType": "DialogCutscene",
        "eventName": "OnEnd",
        "origDelegate": "orig_OnEnd",
        "hookDelegate": "hook_OnEnd",
        "isStatic": False,
        "receiverType": "global::Celeste.Mod.Entities.DialogCutscene",
        "returnType": "void",
        "parameters": [{"type": "global::Celeste.Level", "name": "level"}],
    },
    {
        "id": "celeste-mod-meta-map-meta-apply-to",
        "sourceFile": "Celeste/Mod/AppleEverestStatic/EverestMapMetaStaticApi.cs",
        "sourceDeclaration": "public void ApplyTo(AreaData area)",
        "originalDeclaration": "private void AppleEverestOriginal_ApplyTo(global::Celeste.AreaData area)",
        "originalAlias": "AppleEverestOriginal_ApplyTo",
        "hookNamespace": "On.Celeste.Mod.Meta",
        "hookType": "MapMeta",
        "eventName": "ApplyTo",
        "origDelegate": "orig_ApplyTo",
        "hookDelegate": "hook_ApplyTo",
        "isStatic": False,
        "receiverType": "global::Celeste.Mod.Meta.MapMeta",
        "returnType": "void",
        "parameters": [{"type": "global::Celeste.AreaData", "name": "area"}],
    },
]


def split_top(value: str, separator: str = ",") -> list[str]:
    result: list[str] = []
    start = 0
    depth = 0
    for index, char in enumerate(value):
        if char in "<[(":
            depth += 1
        elif char in ">])":
            depth -= 1
        elif char == separator and depth == 0:
            result.append(value[start:index].strip())
            start = index + 1
    tail = value[start:].strip()
    if tail:
        result.append(tail)
    return result


def without_default(value: str) -> str:
    depth = 0
    for index, char in enumerate(value):
        if char in "<[(":
            depth += 1
        elif char in ">])":
            depth -= 1
        elif char == "=" and depth == 0:
            return value[:index].strip()
    return value.strip()


def source_parameter(value: str) -> tuple[str, str]:
    value = without_default(value)
    value = re.sub(r"^(?:params|ref|out|in|this)\s+", "", value)
    match = re.search(r"([A-Za-z_][A-Za-z0-9_]*)$", value)
    if not match:
        raise ValueError(f"cannot parse source parameter: {value}")
    return value[: match.start()].strip(), match.group(1)


def simple_type(value: str) -> str:
    if value.endswith("[]"):
        return simple_type(value[:-2]) + "[]"
    if value.startswith("System.Nullable`1<") and value.endswith(">"):
        return simple_type(value[len("System.Nullable`1<") : -1]) + "?"
    generic = value.find("<")
    if generic >= 0 and value.endswith(">"):
        base = value[:generic].split("`")[0].split(".")[-1].replace("/", ".")
        return base + "<" + ",".join(simple_type(part) for part in split_top(value[generic + 1 : -1])) + ">"
    aliases = {
        "System.Boolean": "bool",
        "System.Int32": "int",
        "System.Single": "float",
        "System.String": "string",
        "System.Void": "void",
    }
    return aliases.get(value, value.split(".")[-1].replace("/", "."))


def normalized_source_type(value: str) -> str:
    value = value.replace("global::", "").replace(" ", "")
    for prefix in (
        "System.Collections.Generic.",
        "System.Collections.",
        "Microsoft.Xna.Framework.",
        "FMOD.Studio.",
        "Celeste.Editor.",
        "Celeste.Mod.Entities.",
        "Celeste.Mod.Meta.",
        "Celeste.",
        "Monocle.",
        "System.",
    ):
        value = value.replace(prefix, "")
    return value


def csharp_type(value: str) -> str:
    aliases = {
        "System.Boolean": "bool",
        "System.Int32": "int",
        "System.Single": "float",
        "System.String": "string",
        "System.Void": "void",
    }
    if value in aliases:
        return aliases[value]
    if value.endswith("[]"):
        return csharp_type(value[:-2]) + "[]"
    if value.startswith("System.Nullable`1<") and value.endswith(">"):
        return csharp_type(value[len("System.Nullable`1<") : -1]) + "?"
    generic = value.find("<")
    if generic >= 0 and value.endswith(">"):
        base = value[:generic].split("`")[0].replace("/", ".")
        args = ", ".join(csharp_type(part) for part in split_top(value[generic + 1 : -1]))
        return f"global::{base}<{args}>"
    return "global::" + value.replace("/", ".")


def kebab(value: str) -> str:
    value = value.replace("_", "-").replace(".", "-")
    value = re.sub(r"([a-z0-9])([A-Z])", r"\1-\2", value)
    value = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1-\2", value)
    return re.sub(r"-+", "-", value).strip("-").lower()


def find_declaration(source: pathlib.Path, requirement: dict) -> tuple[str, list[tuple[str, str]]]:
    text = source.read_text(encoding="utf-8")
    target_type = requirement["targetType"]
    method = requirement["targetMethod"].split("::", 1)[1].split("(", 1)[0]
    outer = target_type.split("/")[0].split(".")[-1]
    name = outer if method == ".ctor" else method.removeprefix("get_")
    expected = [simple_type(parameter["type"]) for parameter in requirement["parameters"]]
    indentation = 1 + target_type.count("/")
    modifiers = r"(?:public|private|protected|internal)(?:\s+(?:static|virtual|override|abstract|sealed|extern|unsafe|new|partial|async))*"
    pattern = re.compile(rf"(?m)^\t{{{indentation}}}{modifiers}\s+[^\n]*\b{re.escape(name)}\s*\(")
    candidates: list[tuple[str, list[tuple[str, str]]]] = []
    for match in pattern.finditer(text):
        opening = text.find("(", match.start(), match.end())
        depth = 0
        closing = -1
        for index in range(opening, len(text)):
            if text[index] == "(":
                depth += 1
            elif text[index] == ")":
                depth -= 1
                if depth == 0:
                    closing = index
                    break
        if closing < 0:
            continue
        source_parameters = [source_parameter(value) for value in split_top(text[opening + 1 : closing])]
        if len(source_parameters) != len(expected):
            continue
        if any(normalized_source_type(actual[0]) != normalized_source_type(wanted)
               for actual, wanted in zip(source_parameters, expected)):
            continue
        end = closing + 1
        cursor = end
        while cursor < len(text) and text[cursor] in " \t\r\n":
            cursor += 1
        if cursor < len(text) and text[cursor] == ":":
            brace = text.find("{", cursor)
            if brace < 0:
                raise ValueError(f"constructor initializer has no body: {source}")
            end = brace
        declaration = text[match.start() + 1 : end].rstrip()
        candidates.append((declaration, source_parameters))
    if len(candidates) != 1:
        raise ValueError(
            f"source declaration match count {len(candidates)} for "
            f"{requirement['hookType']}.{requirement['eventName']} in {source}"
        )
    return candidates[0]


def entry(requirement: dict, source_root: pathlib.Path) -> dict:
    target_type = requirement["targetType"]
    outer = target_type.split("/")[0]
    parts = outer.split(".")
    source_file = pathlib.Path(*parts[:-1], parts[-1] + ".cs")
    declaration, source_parameters = find_declaration(source_root / source_file, requirement)
    # Stage 3C deliberately exposes Session's parameterless constructor to the
    # static runtime before managed-detour target rewriting runs.  Keep the
    # generated declaration aligned with that accepted source transformation.
    if target_type == "Celeste.Session" and requirement["targetMethod"].startswith(".ctor()"):
        declaration = "internal Session()"
    hook_parts = requirement["hookType"].split(".")
    event = requirement["eventName"]
    alias = "AppleEverestOriginal_" + event
    parameters = [
        {"type": csharp_type(metadata["type"]), "name": source_value[1]}
        for metadata, source_value in zip(requirement["parameters"], source_parameters)
    ]
    parameter_text = ", ".join(value["type"] + " " + value["name"] for value in parameters)
    prefix = "private static " if requirement["targetIsStatic"] else "private "
    if requirement["targetMethod"].split("::", 1)[1].startswith(".ctor("):
        original = f"{prefix}void {alias}({parameter_text})"
    else:
        original = f"{prefix}{csharp_type(requirement['returnType'])} {alias}({parameter_text})"
    result = {
        "id": kebab(requirement["hookType"].removeprefix("On.") + "." + event),
        "sourceFile": source_file.as_posix(),
        "sourceDeclaration": declaration,
        "originalDeclaration": original,
        "originalAlias": alias,
        "hookNamespace": ".".join(hook_parts[:-1]),
        "hookType": hook_parts[-1],
        "eventName": event,
        "origDelegate": requirement["origDelegate"],
        "hookDelegate": requirement["hookDelegate"],
        "isStatic": requirement["targetIsStatic"],
        "returnType": csharp_type(requirement["returnType"]),
        "parameters": parameters,
    }
    if not requirement["targetIsStatic"]:
        result["receiverType"] = csharp_type(target_type)
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--discovery", required=True, type=pathlib.Path)
    parser.add_argument("--source-root", required=True, type=pathlib.Path)
    parser.add_argument("--catalog", required=True, type=pathlib.Path)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    args = parser.parse_args()
    discovery = json.loads(args.discovery.read_text(encoding="utf-8"))
    catalog = json.loads(args.catalog.read_text(encoding="utf-8"))
    existing = {(".".join((value["hookNamespace"], value["hookType"])), value["eventName"])
                for value in catalog["targets"]}
    unique: dict[tuple[str, str], dict] = {}
    for requirement in discovery["requirements"]:
        if requirement["status"] != "resolved":
            raise ValueError(f"unresolved discovery requirement: {requirement}")
        unique[(requirement["hookType"], requirement["eventName"])] = requirement
    added = []
    for key in sorted(unique):
        if key not in existing and key not in PATCHED_ONLY:
            added.append(entry(unique[key], args.source_root))
    patched_added = [value for value in PATCHED_ENTRIES
                     if (value["hookNamespace"] + "." + value["hookType"], value["eventName"]) not in existing]
    targets = sorted(catalog["targets"] + added + patched_added, key=lambda value: value["id"])
    if len({value["id"] for value in targets}) != len(targets):
        raise ValueError("generated target IDs are not unique")
    final_keys = {(value["hookNamespace"] + "." + value["hookType"], value["eventName"])
                  for value in targets}
    missing = set(unique) - final_keys
    if missing or len(targets) != 205:
        raise ValueError(f"expected exact 205-target closure; missing={sorted(missing)} total={len(targets)}")
    output = {"schemaVersion": 2, "targets": targets}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, indent=2, sort_keys=False) + "\n", encoding="utf-8")
    print(f"PASS: Stage 25K-D HookGen catalog +{len(added)} source-backed "
          f"+{len(patched_added)} pinned APIs ({len(targets)} total)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
