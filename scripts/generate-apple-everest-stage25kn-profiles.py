#!/usr/bin/env python3
"""Extract K-N's unchanged three-map authored authority and exact extra guards.

The complete extraction is private build evidence. Only the typed guard and a
hash/count authority belong in Git. K-J/K-L/K-M inputs and reports are unchanged.
"""
from __future__ import annotations
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SID = "StrawberryJam2021/1-Beginner/snas"
MAP_SHA = "6ad3172d496e8b5b4ce71f1128fe231b162d534419dc823d2af2cea27fc241d9"
APPENDIX_SHA = "f13ab43805a004226d5c7eb82a42a028884be222f988198ce7bcf14708b28a04"
EXTRA = {
    ("entity", "CommunalHelper/PlayerBubbleRegion"): "CommunalHelper",
    ("trigger", "ContortHelper/RandomSoundTrigger"): "ContortHelper",
    ("entity", "MaxHelpingHand/FlagTouchSwitch"): "MaxHelpingHand",
    ("entity", "MaxHelpingHand/FlagSwitchGate"): "MaxHelpingHand",
}


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / filename)
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode()


def serialized(value):
    return (json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n").encode()


def extract(package):
    previous = module("kn_previous_profiles", "generate-apple-everest-stage25kj-profiles.py")
    reader = module("kn_binary_tree", "generate-apple-everest-stage25kl-content.py")
    baseline = previous.extract(package)  # Rehashes ZIP and both baseline maps.
    if serialized(baseline) != (ROOT / "apple-everest/sj-factory-authored-profiles-stage25kj.json").read_bytes():
        raise ValueError("baseline profile control differs")
    known = {(f["kind"], f["customId"]): f["provider"] for f in baseline["factories"]} | EXTRA
    km = json.loads((ROOT / "apple-everest/sj-beginner-expansion-inputs-stage25km.json").read_text())
    canonical_ids = {(row["kind"], row["id"]) for row in km["canonicalIds"]}
    with zipfile.ZipFile(package) as archive:
        member = "Maps/" + SID + ".bin"
        if len([entry for entry in archive.infolist() if entry.filename == member]) != 1:
            raise ValueError("missing or duplicate selected map")
        data = archive.read(member)
    if len(data) != 67718 or sha(data) != MAP_SHA:
        raise ValueError("snas original BIN identity differs")
    parsed = reader.read_map(data)
    boundary = parsed["rootBytes"]
    if len(data) - boundary != 14677 or sha(data[boundary:]) != APPENDIX_SHA:
        raise ValueError("snas original appendix differs")
    rows = []
    def walk(node, parent="", room=""):
        if node["name"] == "level":
            room = node["attributes"]["name"]
        kind = {"entities": "entity", "triggers": "trigger", "Backgrounds": "backdrop", "Foregrounds": "backdrop"}.get(parent)
        key = kind, node["name"]
        if kind is not None:
            if key not in known:
                if key not in canonical_ids:
                    raise ValueError("unclassified authored requirement: " + str(key))
            else:
                attrs = node["attributes"]
                if any(child["name"] != "node" for child in node["children"]):
                    raise ValueError("unsupported authored factory child")
                nodes = [child["attributes"] for child in node["children"]]
                if any(set(n) != {"x", "y"} for n in nodes):
                    raise ValueError("unsupported authored node attributes")
                profile = {k: v for k, v in attrs.items() if k not in ("x", "y", "id", "originX", "originY")}
                relative = [{**n, "x": n["x"] - attrs.get("x", 0), "y": n["y"] - attrs.get("y", 0)} for n in nodes]
                rows.append({"kind": kind, "customId": node["name"], "provider": known[key], "map": SID,
                    "room": room, "layer": parent if kind == "backdrop" else None,
                    "entityId": attrs.get("id"), "position": {k: attrs[k] for k in ("x", "y") if k in attrs},
                    "attributes": attrs, "nodes": nodes,
                    "profileSha256": sha(canonical({"attributes": profile, "relativeNodes": relative}))})
        for child in node["children"]:
            walk(child, node["name"], room)
    walk(parsed["tree"])
    if len(rows) != 53 or len({(r["kind"], r["customId"]) for r in rows}) != 12:
        raise ValueError("exact selected snas occurrence/ID census differs")
    occurrences = baseline["occurrences"] + rows
    counts = Counter((r["kind"], r["customId"]) for r in occurrences)
    if sum(counts.values()) != 973 or set(counts) != set(known):
        raise ValueError("three-map selected authority omitted a factory")
    maps = baseline["maps"] + [{"sid": SID, "member": member, "bytes": len(data), "sha256": MAP_SHA,
        "rootBytes": boundary, "appendixBytes": len(data)-boundary, "appendixSha256": APPENDIX_SHA,
        "hostAnalysisOnly": True}]
    return {"schemaVersion": 1, "sourceArchiveSha256": baseline["sourceArchiveSha256"], "maps": maps,
        "census": {"selectedFactories": len(counts), "selectedOccurrences": len(occurrences)},
        "factories": [{"kind": kind, "customId": name, "provider": known[kind, name], "occurrences": count,
            "distinctProfiles": len({r["profileSha256"] for r in occurrences if (r["kind"], r["customId"]) == (kind, name)})}
            for (kind, name), count in sorted(counts.items())], "occurrences": occurrences}


def guard(document):
    emit = module("kn_guard_literals", "generate-apple-everest-stage25kj-guards.py")
    rows = [r for r in document["occurrences"] if r["map"] == SID]
    grouped = {}
    for row in rows:
        if row["kind"] not in ("entity", "trigger"):
            raise ValueError("unreviewed snas backdrop extension")
        grouped.setdefault(row["customId"], {})[row["profileSha256"]] = row
    lines = ["// Generated from the exact unchanged snas BIN by generate-apple-everest-stage25kn-profiles.py.",
        "#nullable disable", "using System;", "using System.Collections.Generic;", "using System.Globalization;",
        "using Microsoft.Xna.Framework;", "namespace Celeste.Mod;", "internal static class AppleEverestSnasProfileGuard", "{",
        "    private sealed class Profile", "    {", "        internal readonly int Width, Height;",
        "        internal readonly Dictionary<string, object> Values;", "        internal readonly Vector2[] Nodes;",
        "        internal Profile(int width, int height, Dictionary<string, object> values, Vector2[] nodes)",
        "        { Width = width; Height = height; Values = values; Nodes = nodes; }", "    }",
        "    private static readonly Dictionary<string, Profile[]> Profiles = new(StringComparer.Ordinal)", "    {"]
    for name, profiles in sorted(grouped.items()):
        lines += ["        [" + emit.cs(name) + "] = new Profile[]", "        {"]
        for _, row in sorted(profiles.items()):
            a = row["attributes"]
            values = ", ".join("["+emit.cs(k)+"] = "+emit.cs(v) for k, v in sorted(a.items())
                if k not in ("x", "y", "id", "originX", "originY", "width", "height"))
            nodes = ", ".join("new Vector2("+emit.cs(float(n["x"]-a.get("x", 0)))+", "+emit.cs(float(n["y"]-a.get("y", 0)))+")" for n in row["nodes"])
            lines.append(f"            new({a.get('width', 0)}, {a.get('height', 0)}, new(StringComparer.Ordinal) {{ {values} }}, new Vector2[] {{ {nodes} }}),")
        lines += ["        },"]
    lines += ["    };", """
    internal static bool Accepts(string id, EntityData data)
    {
        if (data.Name != id || data.Origin != Vector2.Zero || !Profiles.TryGetValue(id, out Profile[] profiles)) return false;
        foreach (Profile profile in profiles)
        {
            if (data.Width != profile.Width || data.Height != profile.Height ||
                (data.Values?.Count ?? 0) != profile.Values.Count || (data.Nodes?.Length ?? 0) != profile.Nodes.Length) continue;
            bool match = true;
            foreach (var pair in profile.Values)
            {
                if (!data.Values.TryGetValue(pair.Key, out object value)) { match = false; break; }
                if (pair.Value is string || pair.Value is bool)
                { if (!pair.Value.Equals(value)) { match = false; break; } }
                else if (value is not (byte or short or int or float or double) ||
                    Convert.ToSingle(pair.Value, CultureInfo.InvariantCulture) != Convert.ToSingle(value, CultureInfo.InvariantCulture))
                { match = false; break; }
            }
            for (int i = 0; match && i < profile.Nodes.Length; i++)
                if (data.Nodes[i] - data.Position != profile.Nodes[i]) match = false;
            if (match) return true;
        }
        return false;
    }
}"""]
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--guard-output", type=Path, required=True)
    args = parser.parse_args()
    result = extract(args.package)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(serialized(result))
    args.guard_output.write_text(guard(result))
    print("PASS: unchanged three-map authored extraction", result["census"], "sha256="+sha(serialized(result)))


if __name__ == "__main__":
    main()
