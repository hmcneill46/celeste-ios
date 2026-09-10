#!/usr/bin/env python3
"""Re-census all 18 unchanged regression maps and inspect the current assembly.

330 occurrences use the selected guard proof. Six older controls deliberately
retain separate proof scope; this tool is registration evidence, not Gate C/D.
"""
from __future__ import annotations
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode()


def check(value, reason):
    if not value:
        raise ValueError(reason)


def extract(closure):
    authority = ROOT / "apple-everest/sj-beginner-expansion-inputs-stage25km.json"
    check(sha(authority.read_bytes()) == "29f2dd452bbf10e5e6d03dd19bb17a0719c3f3520ef97164e44a1e4fcadda72d", "immutable K-M regression authority differs")
    config = json.loads(authority.read_text())
    spec = importlib.util.spec_from_file_location("kn_regression_reader", ROOT / "scripts/generate-apple-everest-stage25kl-content.py")
    reader = importlib.util.module_from_spec(spec);sys.modules[spec.name] = reader;spec.loader.exec_module(reader)
    selected = {(r["kind"], r["customId"]): r["provider"] for r in json.loads((ROOT / "apple-everest/sj-factory-authored-profiles-stage25kj.json").read_text())["factories"]}
    legacy = {("entity", "ChronoHelper/ExplodingPinata"): "ChronoHelper",
              ("entity", "DJMapHelper/colorfulFlyFeather"): "DJMapHelper", ("entity", "DJMapHelper/featherBarrier"): "DJMapHelper"}
    legacy.update({("entity", key): owner for key, owner in config["ownedRegressionProviders"].items()})
    canonical_ids = {(r["kind"], r["id"]) for r in config["canonicalIds"]}
    profiles = []
    for path, expected in config["regressionMaps"].items():
        data = (closure / path).read_bytes()
        check(sha(data) == expected, "original regression map differs: " + path)
        sid = path.removeprefix("content/Content/Maps/")[:-4]
        def walk(node, parent="", room=""):
            attrs = node["attributes"]
            if node["name"] == "level": room = attrs["name"].removeprefix("lvl_")
            kind = {"entities": "entity", "triggers": "trigger", "Foregrounds": "backdrop", "Backgrounds": "backdrop"}.get(parent)
            key = kind, node["name"]
            if kind and key not in canonical_ids:
                check(key in selected or key in legacy, "unclassified regression requirement")
                check(all(c["name"] == "node" for c in node["children"]), "unsupported regression child")
                nodes = [c["attributes"] for c in node["children"]]
                profile = {k: v for k, v in attrs.items() if k not in ("x", "y", "id", "originX", "originY")}
                relative = [{**n, "x": n["x"]-attrs.get("x", 0), "y": n["y"]-attrs.get("y", 0)} for n in nodes]
                profiles.append({"kind": kind, "customId": node["name"], "provider": (selected | legacy)[key], "map": sid,
                    "room": room, "attributes": attrs, "nodes": nodes,
                    "profileSha256": sha(canonical({"attributes": profile, "relativeNodes": relative})),
                    "scope": "SELECTED_PROFILE" if key in selected else "SEPARATE_LEGACY"})
            for child in node["children"]: walk(child, node["name"], room)
        walk(reader.read_map(data)["tree"])
    check(len(profiles) == 336 and len({p["map"] for p in profiles}) == 18, "complete regression census differs")
    check(Counter(p["scope"] for p in profiles) == {"SELECTED_PROFILE": 330, "SEPARATE_LEGACY": 6}, "regression proof scope changed")
    check(Counter((p["kind"], p["customId"]) for p in profiles if p["scope"] == "SEPARATE_LEGACY") == Counter({k: 1 for k in legacy}), "legacy control omitted or duplicated")
    return {"occurrences": profiles}, config["regressionMaps"]


def verify_legacy(rows):
    authority = ROOT / "apple-everest/sj-snas-legacy-reference-stage25kn.json"
    check(sha(authority.read_bytes()) == "83ed99658075ec1f3da44706529b4c9008aea3759dcd393e711d2637c26c0ca5", "legacy source authority differs")
    expected = json.loads(authority.read_text())["entries"]
    actual = []
    for row in rows:
        check(row["actualSelectorInvoked"] and not row["selectedProfileGuardProof"] and
              not row["constructorLifecycleExecution"], "legacy proof scope differs")
        actual.append({"kind": row["kind"], "customId": row["customId"], "provider": row["provider"],
            "entrySha256": row["entrySha256"], "linkedTypeClosureSha256": sha(canonical(row["linkedTypeClosure"])),
            "productionCallers": row["actualProductionCallers"]})
    check(actual == expected, "legacy compiled entry/type closure differs from accepted build-46 authority")
    return sha(authority.read_bytes())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("closure", "assembly", "production-preflight", "work-root"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args();work = args.work_root.resolve()
    check(not work.exists(), "regression output exists")
    for parent in (args.work_root.absolute(), *args.work_root.absolute().parents): check(not parent.is_symlink(), "symlink output ancestor")
    if ROOT == work or ROOT in work.parents:
        subprocess.run(["git", "check-ignore", "--no-index", "-q", "--", str((work / ".stage25kn-regression").relative_to(ROOT))], cwd=ROOT, check=True)
    preflight = json.loads(args.production_preflight.read_text())
    manifest = json.loads((args.closure / "compatibility-manifest.json").read_text())
    check(preflight["compiledAssemblySha256"] == sha(args.assembly.read_bytes()) and
          preflight["sharedClosureSha256"] == manifest["sharedClosureSha256"] and
          preflight["actualProductionRegenerated"] and preflight["compilation"]["FreshProductionCompilation"], "current fresh compilation evidence missing or mismatched")
    profiles, maps = extract(args.closure)
    work.mkdir(parents=True);(work / ".stage25kn-regression").touch()
    (work / "profiles.private.json").write_bytes(canonical(profiles)+b"\n")
    dotnet = ROOT / ".build/apple-everest/toolchain/dotnet8/dotnet"
    env = dict(os.environ, MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_USE_MSBUILD_SERVER="0")
    env["DOTNET_ROOT"] = str(dotnet.parent)
    with (work / "commands.private.log").open("w") as log:
        # The repository global.json selects SDK 10 for device tools; the
        # builder itself remains compiled by pinned SDK 8 outside that lookup.
        subprocess.run([str(dotnet), "build", str(ROOT / "tools/AppleEverestBuilder/AppleEverestBuilder.csproj"), "-c", "Release", "-m:1", "-p:BuildInParallel=false", "-p:UseSharedCompilation=false", "--nologo"], cwd="/private/tmp", env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
        host = ROOT / ".build/apple-everest/toolchain/dotnet10/dotnet"
        env["DOTNET_ROOT"] = str(host.parent)
        env["DOTNET_ROLL_FORWARD"] = "LatestMajor"
        command = [str(host), str(ROOT / "tools/AppleEverestBuilder/bin/Release/net8.0/AppleEverestBuilder.dll")]
        for mode, extra, output in [
            ("inspect-compiled-factories", ["--manifest", str(ROOT / "apple-everest/selected-factory-type-closure-stage25kh.json"), "--authored-profiles", str(work / "profiles.private.json")], "selected.json"),
            ("inspect-legacy-regression-entries", [], "legacy.json")]:
            subprocess.run(command+[mode, "--assembly", str(args.assembly.resolve()), "--output", str(work / output)]+extra, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
    selected = json.loads((work / "selected.json").read_text());legacy = json.loads((work / "legacy.json").read_text())
    check(len(selected) == 73 and sum(r["AcceptedOccurrences"] for r in selected) == 330, "actual selected regression guard census differs")
    check(len(legacy) == 6, "legacy linked entry census differs")
    legacy_authority = verify_legacy(legacy)
    report = {"status": "PASS_REGISTRATION_SCOPE_ONLY", "sharedClosureSha256": manifest["sharedClosureSha256"],
        "compiledAssemblySha256": sha(args.assembly.read_bytes()), "regressionMaps": maps,
        "census": {"maps": 18, "occurrences": 336, "selectedGuardOccurrences": 330, "separateLegacyOccurrences": 6},
        "profileSha256": sha((work / "profiles.private.json").read_bytes()), "selected": selected, "legacy": legacy,
        "legacyEntryAndTypeClosureAuthoritySha256": legacy_authority,
        "unchangedLegacyImplementationsVerified": True,
        "gateC": "SEPARATE_BEHAVIOR_FROZEN_IL_LIFECYCLE_PROOFS_REQUIRED", "gateD": "SEPARATE_COMPOSITION_AND_PHYSICAL_PROOFS_REQUIRED"}
    (work / "source-bound-result.json").write_text(json.dumps(report, indent=2, sort_keys=True)+"\n")
    print("PASS: all 336 regression occurrences; 330 actual selected guard checks and six separately scoped linked legacy entries")


if __name__ == "__main__": main()
