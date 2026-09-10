#!/usr/bin/env python3
"""Recreate source metadata/IL bindings for the finite K-N review.

The exact DLL and deterministic metadata/IL census are the source authority.
Historical decompiled-text hashes identify the reviewed private notes; text
rendering depends on the decompiler's reference context and is not an execution
or reproducibility gate. No distributed code is activated by this tool.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
TYPES = {
    "MaxHelpingHand": {
        "Entities.FlagSwitchGate": "c7ab870c8fa16ab93acf68cc42e65ca5164c27538abec39cb2d24befdaa0cdf2",
        "Entities.FlagTouchSwitch": "91602ca853757b825ce8d0216ed8f5dfe01a48b4719d6e9d832b2c41a86c5dcb",
        "Module.MaxHelpingHandMapDataProcessor": "ecd436b27a02a39f2cd4936fff7fc3aa3d8d5cdd60c01a070d458c94845e7953",
        "Triggers.CameraOffsetBorder": "e55e9464637f99bfd8d753f6fe8dd0af77a609dd650b50d51bc81b7efd7a19e9",
        "Triggers.FlagToggleComponent": "54cdafc8f8e0941d4a6c590c7c0a101fe91fc4f333f39f8ca551a58e68631f4f",
        "Triggers.FlagToggleSmoothCameraOffsetTrigger": "6fbdb8da5e0fd1e190f3c882111a56b239aef6432d484d0a9633da44922f5021",
        "Entities.SetFlagOnSpawnController": "06e755179a4be38fabdca56296c500fe307d4e37cf2f316e379cadadf02112ca",
        "Entities.SidewaysJumpThru": "c99447e709447c2950086e6374834b356da13f1a5cb5886300313201c0d3cbdc",
        "Module.MaxHelpingHandModule": "5e91b254ec2af758b05bfbd5b19b4ade6b2f2572489a766417ed397354303a77",
    },
    "CollabUtils2": {
        "Entities.MiniHeart": "915638f11b83298b0cec07c80dafd9313b3d44dca03c7c99b16dea3e1a33c97b",
        "Entities.AbstractMiniHeart": "4a303f908b91066ac29b830cea75c18e378ae4934d21e5399a9bf0c9453ecc03",
    },
    "CommunalHelper": {
        "Entities.PlayerBubbleRegion": "cdfdbfb79daa7715487b0218e42952e6130340069a58fc1eeeb4ff92bd6c121d",
    },
    "ContortHelper": {
        "RandomSoundTrigger": "267cfe03185864335641de3a2c9ff39ee0f059fa8eb37fe63537e7fc0b15da81",
        "AbstractTrigger": "eb45ae84fc5efb34312d19d3eafa93c1de9e7ad774c5b93589e2f44336f60d9a",
        "EntityDatas": "5f3f0872ab5440ab95439745703891ff66f3a77ff431a86144482fa73df176b1",
    },
}
CENSUS = {
    "MaxHelpingHand": "04f4ef3abfb942f7fe7107eda35fc07aa2fc46b4c72677973017f8019ae494e1",
    "CollabUtils2": "75d456bf3fd9ccc43340872096a52c2f605b8effb9325cfa5c6c1c05c905fa41",
    "CommunalHelper": "7b920ca144c5a3dc650d12669329ed01fd46157054268e31cc52c4a7edb46d48",
    "ContortHelper": "cee3fade9e8c2df013209e77e772f89053ea0d6813be2e971e3ce3976be1257f",
}


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package-root", type=Path, required=True)
    parser.add_argument("--work-root", type=Path, required=True)
    parser.add_argument("--builder", type=Path, default=ROOT / "tools/AppleEverestBuilder/bin/Debug/net8.0/AppleEverestBuilder.dll")
    args = parser.parse_args();work = args.work_root.resolve()
    require(not work.exists(), "source-binding output already exists")
    for parent in (args.work_root.absolute(), *args.work_root.absolute().parents):
        require(not parent.is_symlink(), "symlink source-binding output")
    if work == ROOT or ROOT in work.parents:
        subprocess.run(["git", "check-ignore", "--no-index", "-q", "--",
                        str((work / ".stage25kn-sources").relative_to(ROOT))], cwd=ROOT, check=True)
    authority = ROOT / "apple-everest/sj-beginner-expansion-inputs-stage25km.json"
    require(sha(authority) == "29f2dd452bbf10e5e6d03dd19bb17a0719c3f3520ef97164e44a1e4fcadda72d", "immutable K-M input ledger differs")
    km = json.loads(authority.read_text())
    pins = {row["name"]: row for row in km["acceptedPackages"] + km["candidateOnlyPackages"]}
    census_source = ROOT / "tools/AppleEverestBuilder/SemanticReachabilityCensus.cs"
    require(sha(census_source) == "0a938a06f16699853d4649c6b33656f71a01e8f02d56daed3d75285e89ef5ffa", "unchanged metadata/IL census writer differs")
    work.mkdir(parents=True);(work / ".stage25kn-sources").touch()
    records = [];packages = []
    with (work / "commands.private.log").open("w") as log:
        for provider, types in TYPES.items():
            package = args.package_root / (provider + ".zip");pin = pins[provider]
            require(sha(package) == pin["zipSha256"] and package.stat().st_size == pin["zipBytes"], "exact source package differs: " + provider)
            member = "src/bin/Debug/net8.0/CommunalHelper.dll" if provider == "CommunalHelper" else pin["distributedDlls"][0]["archivePath"]
            with zipfile.ZipFile(package) as archive:
                require(sum(row.filename == member for row in archive.infolist()) == 1, "missing or duplicate source DLL")
                data = archive.read(member)
            expected = pin["distributedDlls"][0]
            require(hashlib.sha256(data).hexdigest() == expected["sha256"] and len(data) == expected["bytes"], "exact source DLL differs: " + provider)
            dll = work / (provider + ".dll");dll.write_bytes(data)
            packages.append({"name": provider, "version": pin["version"], "archiveSha256": pin["zipSha256"], "dllSha256": expected["sha256"]})
            census_path = work / (provider + ".private.json")
            subprocess.run([str(ROOT / ".build/apple-everest/toolchain/dotnet8/dotnet"), str(args.builder.resolve()),
                            "census-semantics", "--dll", str(dll), "--output", str(census_path)],
                           cwd="/private/tmp", stdout=log, stderr=log, check=True)
            require(sha(census_path) == CENSUS[provider], "exact source metadata/IL census differs: " + provider)
            census = json.loads(census_path.read_text())
            for suffix, expected_source in types.items():
                name = ("ContortHelper." if provider == "ContortHelper" else "Celeste.Mod." + provider + ".") + suffix
                selected = [row for row in census["types"] if row["name"] == name or row["name"].startswith(name + "/")]
                require(sum(row["name"] == name for row in selected) == 1, "missing or ambiguous source type")
                methods = [{"method": method["name"], "bodySha256": method["bodySha256"]}
                           for row in selected for method in row["methods"] if method["bodySha256"] is not None]
                require(methods, "source implementation body missing")
                records.append({"type": name, "metadataIlCensusSha256": CENSUS[provider],
                    "reviewedHistoricalDecompiledTextSha256": expected_source,
                    "methods": methods, "scope": "EXACT_DISTRIBUTED_METADATA_AND_IL_NOT_EXECUTION"})
    report = {"schemaVersion": 1, "status": "PASS_SOURCE_BINDINGS", "packages": packages,
              "sourceTypes": records, "executionEvidence": False,
              "censusWriterSha256": sha(census_source), "inputAuthoritySha256": sha(authority),
              "historicalDecompilerVersion": "8.0.0.7246-preview3",
              "historicalDecompiledTextReproductionClaimed": False}
    (work / "source-bound-result.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    print("PASS: 15 exact pinned source-review bindings; execution remains separately proven")


if __name__ == "__main__":
    main()
