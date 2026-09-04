#!/usr/bin/env python3
"""Generate three complete K-F closures and compile both helper omissions."""
from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
PACKAGES = ["ContortHelper", "ExtendedVariantMode", "JungleHelper", "YetAnotherHelper",
            "StrawberryJam2021", "CollabUtils2", "LunaticHelper", "CrystallineHelper",
            "VortexHelper", "StrawberryJam2021AudioA", "StrawberryJam2021AudioB"]
OMISSIONS = ["CrystallineHelper", "VortexHelper"]
HASH_KEYS = ["sharedClosureSha256", "managedLogicalSha256", "contentLogicalSha256",
             "registrySha256", "hookTransformSha256", "appleApiSurfaceSha256",
             "moduleDurabilityClosureSha256", "staticSemanticRuntimePatchSha256",
             "customAudioManifestSha256", "customBankLogicalSetSha256"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fixture-root", required=True, type=pathlib.Path)
    parser.add_argument("--chrono-package", required=True, type=pathlib.Path)
    parser.add_argument("--work-root", required=True, type=pathlib.Path)
    args = parser.parse_args()
    work = args.work_root.resolve()
    if work.exists():
        raise RuntimeError("use a fresh --work-root")
    work.mkdir(parents=True)
    fixture = args.fixture_root.resolve()
    dotnet8 = ROOT / ".build/apple-everest/toolchain/dotnet8/dotnet"
    builder = ROOT / "tools/AppleEverestBuilder/bin/Debug/net8.0/AppleEverestBuilder.dll"
    runner = [str(dotnet8), str(builder)]
    subprocess.run(["dotnet", "build", str(ROOT / "tools/AppleEverestBuilder/AppleEverestBuilder.csproj"),
                    "--no-restore"], check=True, stdout=subprocess.DEVNULL)

    def run(name: str, packages: list[str], canaries: list[str]):
        output = work / name
        command = [*runner, "build", "--profile", str(ROOT / "apple-everest/profiles/stable-1.6458.0.json"),
                   "--repo-root", str(ROOT), "--upstream", str(ROOT / ".build/apple-everest/upstream/Everest"),
                   "--output", str(output)]
        for canary in canaries:
            command += ["--mod", str(ROOT / "apple-everest/canaries" / canary)]
        for package in packages:
            command += ["--mod", str(fixture / "packages" / (package + ".zip"))]
        command += ["--mod", str(args.chrono_package.resolve())]
        with (work / (name + ".log")).open("w") as log:
            subprocess.run(command, check=True, cwd="/tmp", stdout=log, stderr=subprocess.STDOUT)
        return json.loads((output / "compatibility-manifest.json").read_text())

    def inventory(path: pathlib.Path):
        return {p.relative_to(path).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                for p in path.rglob("*") if p.is_file()}

    full = [run(f"run{index}", PACKAGES, ["stage25ke", "stage25kf", "custom-audio-content"])
            for index in range(1, 4)]
    assert all({key: item[key] for key in HASH_KEYS} == {key: full[0][key] for key in HASH_KEYS}
               for item in full)
    inventories = [inventory(work / f"run{index}") for index in range(1, 4)]
    assert inventories[0] == inventories[1] == inventories[2]
    expected_banks = [(8, "ChronoHelper", "bank:/ExpertContestHelper"),
        (9, "StrawberryJam2021AudioA", "bank:/sj21_bingovergoogle"),
        (10, "StrawberryJam2021AudioA", "bank:/sj21_shared"),
        (11, "StrawberryJam2021AudioB", "bank:/sj21_BegLobby"),
        (12, "StrawberryJam2021", "bank:/sj21_jamjars")]
    assert [(b["loadOrdinal"], b["owner"], b["bankPath"]) for b in full[0]["customAudioBanks"]] == expected_banks
    assert full[0]["staticSemanticFactoryCount"] == 30
    print("PASS: three complete Stage 25K-F closure trees identical", flush=True)

    base = run("omission-control", PACKAGES, ["content"])
    baseline = {mod["name"]: mod for mod in base["selectedMods"]}
    base_audio = [(b["loadOrdinal"], b["bankPath"], b["bankSha256"]) for b in base["customAudioBanks"]]
    omission_rows = []
    for omitted in OMISSIONS:
        name = "omit-" + omitted
        manifest = run(name, [item for item in PACKAGES if item != omitted], ["content"])
        assert manifest["sharedClosureSha256"] != base["sharedClosureSha256"]
        assert omitted not in manifest["resolvedOrder"]
        removed = baseline[omitted]["staticSemanticLowering"]
        for source in removed["RuntimeFiles"]:
            assert not (work / name / "managed" / source).exists()
        registry = (work / name / "managed/GeneratedAppleEverestGameplayRegistry.cs").read_text()
        for factory in removed["Factories"]:
            assert factory["Id"] not in registry
        for mod in manifest["selectedMods"]:
            assert mod["staticSemanticLowering"] == baseline[mod["name"]]["staticSemanticLowering"]
        audio = [(b["loadOrdinal"], b["bankPath"], b["bankSha256"]) for b in manifest["customAudioBanks"]]
        assert audio == base_audio

        managed = work / (name + "-managed")
        shutil.copytree(ROOT / ".build/celeste-ios/current/managed", managed,
                        ignore=shutil.ignore_patterns("bin", "obj"))
        with (work / (name + "-compile.log")).open("w") as log:
            subprocess.run([*runner, "apply", "--closure", str(work / name), "--managed-root", str(managed)],
                           check=True, stdout=log, stderr=subprocess.STDOUT)
            subprocess.run(["dotnet", "build", str(managed / "Celeste.Modern.csproj"), "-c", "Release",
                "-r", "ios-arm64", "-p:CelesteAppleRepoRoot=" + str(ROOT),
                "-p:CelesteManagedGeneratedRoot=" + str(managed), "-p:EnableCodeSigning=false"],
                check=True, stdout=log, stderr=subprocess.STDOUT)
        shutil.rmtree(managed)
        omission_rows.append({"omitted": omitted, "closureSha256": manifest["sharedClosureSha256"],
            "factoriesRemoved": [factory["Id"] for factory in removed["Factories"]],
            "runtimeSourcesRemoved": removed["RuntimeFiles"], "managedCompile": True,
            "unrelatedSemanticPlansIdentical": True, "audioManifestIdentical": True,
            "acceptedBeginnerManifestHashesRetained": True})
        print("PASS: compiled omission " + omitted, flush=True)

    result = {"schemaVersion": 1, "stage": "25K-F", "runsIdentical": True,
        "completeTreeCompared": True, "fileCount": len(inventories[0]),
        "closure": {key: full[0][key] for key in HASH_KEYS},
        "staticSemanticFactoryCount": full[0]["staticSemanticFactoryCount"],
        "customAudioBankCount": full[0]["customAudioBankCount"],
        "loadOrder": expected_banks, "omissionControlClosureSha256": base["sharedClosureSha256"],
        "omissions": omission_rows}
    (work / "reproduction.json").write_text(json.dumps(result, indent=2, sort_keys=True) + "\n")
    print("PASS: Stage 25K-F reproduction and omission controls")


if __name__ == "__main__":
    main()
