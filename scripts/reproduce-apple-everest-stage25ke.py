#!/usr/bin/env python3
"""Generate three K-E closures and four independent helper-omission profiles."""
import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
HELPERS = ["ContortHelper", "ExtendedVariantMode", "JungleHelper", "YetAnotherHelper"]
PACKAGES = [*HELPERS, "StrawberryJam2021", "CollabUtils2", "LunaticHelper"]
KEYS = ["sharedClosureSha256", "managedLogicalSha256", "contentLogicalSha256", "registrySha256",
        "hookTransformSha256", "appleApiSurfaceSha256", "moduleDurabilityClosureSha256",
        "staticSemanticRuntimePatchSha256"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fixture-root", required=True, type=pathlib.Path)
    parser.add_argument("--work-root", required=True, type=pathlib.Path)
    parser.add_argument("--compile-omissions", action="store_true")
    args = parser.parse_args()
    work = args.work_root.resolve()
    work.mkdir(parents=True, exist_ok=True)
    runner = [str(ROOT / ".build/apple-everest/toolchain/dotnet8/dotnet"),
              str(ROOT / "tools/AppleEverestBuilder/bin/Debug/net8.0/AppleEverestBuilder.dll")]
    subprocess.run(["dotnet", "build", str(ROOT / "tools/AppleEverestBuilder/AppleEverestBuilder.csproj"), "--no-restore"],
                   check=True, stdout=subprocess.DEVNULL)

    def run(name, packages, canary):
        output = work / name
        if output.exists():
            raise RuntimeError(f"use a fresh work root; existing run {name}")
        command = [*runner, "build", "--profile", str(ROOT / "apple-everest/profiles/stable-1.6458.0.json"),
            "--repo-root", str(ROOT), "--upstream", str(ROOT / ".build/apple-everest/upstream/Everest"),
            "--output", str(output), "--mod", str(ROOT / "apple-everest/canaries" / canary)]
        for package in packages:
            command += ["--mod", str(args.fixture_root.resolve() / "packages" / (package + ".zip"))]
        with (work / (name + ".log")).open("w") as log:
            subprocess.run(command, check=True, cwd="/tmp", stdout=log, stderr=subprocess.STDOUT)
        return json.loads((output / "compatibility-manifest.json").read_text())

    runs = [run("run" + str(i), PACKAGES, "stage25ke") for i in range(1, 4)]
    assert all({key: r[key] for key in KEYS} == {key: runs[0][key] for key in KEYS} for r in runs)
    def files(path):
        return {p.relative_to(path).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                for p in path.rglob("*") if p.is_file()}
    inventories = [files(work / ("run" + str(i))) for i in range(1, 4)]
    assert inventories[0] == inventories[1] == inventories[2]
    print("PASS: three complete closure trees identical", flush=True)
    base = run("omission-control", PACKAGES, "content")
    baseline = {m["name"]: m for m in base["selectedMods"]}
    records = []
    for omitted in HELPERS:
        name = "omit-" + omitted
        manifest = run(name, [p for p in PACKAGES if p != omitted], "content")
        assert manifest["sharedClosureSha256"] != base["sharedClosureSha256"]
        assert omitted not in manifest["resolvedOrder"]
        removed = baseline[omitted]["staticSemanticLowering"]
        for file in removed["RuntimeFiles"]:
            assert not (work / name / "managed" / file).exists()
        for factory in removed["Factories"]:
            assert factory["Id"] not in (work / name / "managed/GeneratedAppleEverestModuleRegistry.cs").read_text()
        for mod in manifest["selectedMods"]:
            assert mod["staticSemanticLowering"] == baseline[mod["name"]]["staticSemanticLowering"]
        old_maps = {m["logicalPath"]: m["sha256"] for m in base["contentMounts"] if m["logicalPath"].startswith("Maps/")}
        new_maps = {m["logicalPath"]: m["sha256"] for m in manifest["contentMounts"] if m["logicalPath"].startswith("Maps/")}
        assert old_maps == new_maps and old_maps
        compiled = False
        if args.compile_omissions:
            managed = work / (name + "-target")
            shutil.copytree(ROOT / ".build/celeste-ios/current/managed", managed,
                            ignore=shutil.ignore_patterns("bin", "obj"))
            with (work / (name + "-compile.log")).open("w") as log:
                subprocess.run([*runner, "apply", "--closure", str(work / name), "--managed-root", str(managed)],
                               check=True, stdout=log, stderr=subprocess.STDOUT)
                subprocess.run(["dotnet", "build", str(managed / "Celeste.Modern.csproj"), "-c", "Release", "-r", "ios-arm64",
                    "-p:CelesteAppleRepoRoot=" + str(ROOT), "-p:CelesteManagedGeneratedRoot=" + str(managed),
                    "-p:EnableCodeSigning=false"], check=True, stdout=log, stderr=subprocess.STDOUT)
            compiled = True
            shutil.rmtree(managed)
        records.append({"omitted": omitted, "closureSha256": manifest["sharedClosureSha256"],
            "factoriesRemoved": [f["Id"] for f in removed["Factories"]], "sourcesRemoved": removed["RuntimeFiles"],
            "unrelatedPlansIdentical": True, "controlMapsIdentical": True, "managedCompile": compiled})
        print("PASS: omission " + omitted, flush=True)
    result = {"schemaVersion": 1, "stage": "25K-E", "runsIdentical": True, "completeTreeCompared": True,
        "fileCount": len(inventories[0]), "closure": {key: runs[0][key] for key in KEYS},
        "omissionControlClosureSha256": base["sharedClosureSha256"], "omissions": records}
    (work / "reproduction.json").write_text(json.dumps(result, indent=2, sort_keys=True) + "\n")


if __name__ == "__main__": main()
