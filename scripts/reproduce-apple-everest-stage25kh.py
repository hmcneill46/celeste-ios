#!/usr/bin/env python3
"""Reproduce three K-H canary closures and fail-closed omission controls."""
from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
PACKAGES = ["ContortHelper", "ExtendedVariantMode", "JungleHelper", "YetAnotherHelper",
            "StrawberryJam2021", "CollabUtils2", "LunaticHelper", "CrystallineHelper",
            "VortexHelper", "StrawberryJam2021AudioA", "StrawberryJam2021AudioB"]
HASH_KEYS = ["sharedClosureSha256", "managedLogicalSha256", "contentLogicalSha256",
             "registrySha256", "hookTransformSha256", "appleApiSurfaceSha256",
             "moduleDurabilityClosureSha256", "staticSemanticRuntimePatchSha256",
             "customAudioManifestSha256", "customBankLogicalSetSha256"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fixture-root", required=True, type=pathlib.Path)
    parser.add_argument("--chrono-package", required=True, type=pathlib.Path)
    parser.add_argument("--max-package", required=True, type=pathlib.Path)
    parser.add_argument("--work-root", required=True, type=pathlib.Path)
    args = parser.parse_args()
    work = args.work_root.resolve()
    if work.exists():
        raise RuntimeError("use a fresh --work-root")
    work.mkdir(parents=True)
    fixture = args.fixture_root.resolve()
    dotnet8 = ROOT / ".build/apple-everest/toolchain/dotnet8/dotnet"
    builder = ROOT / "tools/AppleEverestBuilder/bin/Release/net8.0/AppleEverestBuilder.dll"
    subprocess.run(["dotnet", "build", str(ROOT / "tools/AppleEverestBuilder/AppleEverestBuilder.csproj"),
                    "-c", "Release", "--nologo"], check=True, stdout=subprocess.DEVNULL)
    runner = [str(dotnet8), str(builder)]

    assert hashlib.sha256(args.max_package.read_bytes()).hexdigest() == \
        "abfc5d167936410e9b6665018335ee654455e30710caffe01540b232da468fee"

    def run(name: str, include_max: bool, include_kh: bool):
        output = work / name
        command = [*runner, "build", "--profile", str(ROOT / "apple-everest/profiles/stable-1.6458.0.json"),
                   "--repo-root", str(ROOT), "--upstream", str(ROOT / ".build/apple-everest/upstream/Everest"),
                   "--output", str(output)]
        if include_kh:
            command += ["--factory-closure", str(ROOT / "apple-everest/canaries/stage25kh/factory-closure.json")]
        for canary in ["stage25ke", "stage25kf", "custom-audio-content"] + (["stage25kh"] if include_kh else []):
            command += ["--mod", str(ROOT / "apple-everest/canaries" / canary)]
        for package in PACKAGES:
            command += ["--mod", str(fixture / "packages" / (package + ".zip"))]
        command += ["--mod", str(args.chrono_package.resolve())]
        if include_max:
            command += ["--mod", str(args.max_package.resolve())]
        with (work / (name + ".log")).open("w") as log:
            subprocess.run(command, check=True, cwd=tempfile.gettempdir(), stdout=log,
                           stderr=subprocess.STDOUT)
        return json.loads((output / "compatibility-manifest.json").read_text())

    def inventory(path: pathlib.Path):
        return {item.relative_to(path).as_posix(): hashlib.sha256(item.read_bytes()).hexdigest()
                for item in path.rglob("*") if item.is_file()}

    runs = [run(f"run{index}", True, True) for index in range(1, 4)]
    inventories = [inventory(work / f"run{index}") for index in range(1, 4)]
    assert inventories[0] == inventories[1] == inventories[2]
    assert all({key: value[key] for key in HASH_KEYS} == {key: runs[0][key] for key in HASH_KEYS}
               for value in runs)
    print("PASS: three complete K-H closure trees identical", flush=True)

    control = run("max-control", True, False)
    omitted = run("omit-maxhelpinghand", False, False)
    assert control["sharedClosureSha256"] != omitted["sharedClosureSha256"]
    assert "MaxHelpingHand" in control["resolvedOrder"] and "MaxHelpingHand" not in omitted["resolvedOrder"]
    control_registry = (work / "max-control/managed/GeneratedAppleEverestGameplayRegistry.cs").read_text()
    omitted_registry = (work / "omit-maxhelpinghand/managed/GeneratedAppleEverestGameplayRegistry.cs").read_text()
    for factory in ["MaxHelpingHand/CustomTutorialWithNoBird", "MaxHelpingHand/MoreCustomNPC"]:
        assert factory in control_registry and factory not in omitted_registry
    assert (work / "max-control/managed/AppleEverestEverestBaseEntitySemantics.cs").exists()
    assert not (work / "omit-maxhelpinghand/managed/AppleEverestEverestBaseEntitySemantics.cs").exists()
    baseline = {row["name"]: row.get("staticSemanticLowering") for row in control["selectedMods"]
                if row["name"] != "MaxHelpingHand"}
    assert baseline == {row["name"]: row.get("staticSemanticLowering") for row in omitted["selectedMods"]}

    source_graph = json.loads((ROOT / "apple-everest/canaries/stage25kh/factory-closure.json").read_text())
    negatives = []
    for custom_id, base_name in [("MaxHelpingHand/CustomTutorialWithNoBird", "CustomBirdTutorial"),
                                 ("MaxHelpingHand/MoreCustomNPC", "CustomNPC")]:
        graph = json.loads(json.dumps(source_graph))
        node = next(row for row in graph["nodes"] if row["id"] == f"entity:{custom_id}:base_chain")
        node["classification"] = "UNKNOWN"
        node["evidence"] = "synthetic omitted " + base_name + " semantic profile"
        path = work / ("omit-" + base_name + ".json")
        path.write_text(json.dumps(graph, indent=2, sort_keys=True) + "\n")
        result = subprocess.run([*runner, "validate-factory-closure", "--manifest", str(path)],
                                capture_output=True, text=True)
        assert result.returncode != 0 and "BASE_CHAIN" in result.stderr and custom_id in result.stderr
        negatives.append({"omitted": base_name, "factory": custom_id, "failedBeforeProductGeneration": True})

    result = {"schemaVersion": 1, "stage": "25K-H", "runsIdentical": True,
              "completeTreeCompared": True, "fileCount": len(inventories[0]),
              "closure": {key: runs[0][key] for key in HASH_KEYS},
              "staticSemanticFactoryCount": runs[0]["staticSemanticFactoryCount"],
              "customAudioBankCount": runs[0]["customAudioBankCount"],
              "omitMaxHelpingHand": {"closureSha256": omitted["sharedClosureSha256"],
                "factoriesRemoved": ["MaxHelpingHand/CustomTutorialWithNoBird", "MaxHelpingHand/MoreCustomNPC"],
                "baseSemanticSourceRemoved": True, "unrelatedKfSemanticPlansIdentical": True,
                "closureIdentityDiffers": True},
              "omitBaseProfiles": negatives,
              "historical": {"contentIdCensus": "920/920/0/0",
                             "preFixTypeClosure": "73/71/2/0"}}
    (work / "reproduction.json").write_text(json.dumps(result, indent=2, sort_keys=True) + "\n")
    print("PASS: K-H MaxHelpingHand and base-profile omission controls", flush=True)


if __name__ == "__main__":
    main()
