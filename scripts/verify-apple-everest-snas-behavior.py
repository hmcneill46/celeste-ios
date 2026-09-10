#!/usr/bin/env python3
"""K-N host-only differential execution against exact pinned helper sources.

All extracted references, canonical game source and compilation outputs remain in
a new owned work root. The result explicitly records its boundary-adapter scope;
it is not a device product or physical gameplay receipt.
"""
from __future__ import annotations
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
CANONICAL = {
    "Monocle/Coroutine.cs": "d4cd81b29b8535146931606aeafac396ce739df00a24763a1b51c337325fae33",
    "Monocle/Component.cs": "7c16c7fc976557595c0e30ac1013705261f1637f9b585d4d53533960ed01f464",
    "Monocle/ComponentList.cs": "8ce914cfba10d41711598668e01f43015e53314fcda2b9c2c8d7a11548fb50a3",
    "Monocle/EntityList.cs": "4ae4205ff0826a0fa53354148a1271317b0e0af1e38fa8e3661f2139e2bf9fac",
    "Celeste/EntityData.cs": "7e722e9d9fa35c3d5c026264d35f694f450f93300a4cb3967750dbc944d1043d",
    "Celeste/EntityID.cs": "6e0fad7ea2b4015f23bfd092fb4654c03900766cdba3d0ebc3c987983f05ff87",
    "Celeste/Trigger.cs": "1daf32b2c7418553b4e3897b16eeab0a985f2ac22a25edda03c7d502fb364949",
    "Celeste/PlayerCollider.cs": "d4709c9a080fcacf8b5beee20c5a2557d1e39ea497042d0eeb7c06ca9befd37f"
}
PACKAGES = {
    "CommunalHelper": ("44f4fb0b277a4900fd2a555e1a73e661140aa7b2455d3c7776cf420193349e3c",
        "src/bin/Debug/net8.0/CommunalHelper.dll", "4011b959ed4e9cc4cb98bf43f6884ae81361fecc994787640553205029c94a8a",
        {"PlayerBubbleRegion": "Celeste.Mod.CommunalHelper.Entities.PlayerBubbleRegion"}),
    "ContortHelper": ("d8b42128a808e68d30329baa9299fdb41bf7d24743635dec3137c36bcc87956a", "ContortHelper.dll", "c3983e67e1b535fbb1e0f0a541e8c78ad8f4cd150f66636c783a83dc5ffb4488",
        {"RandomSoundTrigger": "ContortHelper.RandomSoundTrigger", "AbstractTrigger": "ContortHelper.AbstractTrigger",
         "EntityDatas": "ContortHelper.EntityDatas"})
}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--communal", type=Path, required=True)
    parser.add_argument("--contort", type=Path, required=True)
    parser.add_argument("--canonical", type=Path, required=True)
    parser.add_argument("--references", type=Path, required=True)
    parser.add_argument("--work-root", type=Path, required=True)
    args = parser.parse_args()
    work = args.work_root.resolve()
    if work.exists():
        raise ValueError("differential output root already exists")
    # Never write into tracked paths, or through an existing symlink ancestor.
    if ROOT == work or ROOT in work.parents:
        relative = work.relative_to(ROOT)
        subprocess.run(["git", "check-ignore", "--no-index", "-q", "--", str(relative / ".stage25kn-behavior")], cwd=ROOT, check=True)
    for parent in (args.work_root.absolute(), *args.work_root.absolute().parents):
        if parent.is_symlink():
            raise ValueError("symlink output ancestor")
    work.mkdir(parents=True)
    (work / ".stage25kn-behavior").touch()
    log = (work / "commands.private.log").open("w")
    def run(cmd):
        subprocess.run(cmd, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
    version = subprocess.check_output(["dotnet", "--version"], cwd=ROOT, text=True).strip()
    if version != "10.0.302":
        raise ValueError("requires pinned SDK 10.0.302")
    tool_lock = json.loads((ROOT / ".config/dotnet-tools.json").read_text())
    if tool_lock["tools"]["ilspycmd"]["version"] != "8.0.0.7246-preview3":
        raise ValueError("pinned decompiler differs")
    # Reuse the exact K-M package authority; its contents are historical and unchanged.
    ledger = json.loads((ROOT / "apple-everest/sj-beginner-expansion-inputs-stage25km.json").read_text())
    receipts = {"canonicalSourceSha256": CANONICAL, "packages": {}, "referenceSources": {}, "productionSources": {}}
    # Resolving the actual bases is mandatory. Without Celeste/FNA references,
    # ILSpy can render a nonvirtual base call as a recursive virtual self-call.
    references = work / "references"
    references.mkdir()
    for name, expected in {
        "Celeste.exe": "fd73f8a2311fa5737ded550cbad4b75c85b7686b36432f59185e940fcb65fcfe",
        "FNA.dll": "00349b572636c0ed4c97d4e4f74344b3450160c42d4a6b52bcf5e0f2d5193a31"
    }.items():
        data = (args.references / name).read_bytes()
        if digest(data) != expected:
            raise ValueError("pinned decompiler reference differs: " + name)
        (references / name).write_bytes(data)
    for name, argument in [("CommunalHelper", args.communal), ("ContortHelper", args.contort)]:
        expected, member, dll_sha, classes = PACKAGES[name]
        package_bytes = argument.read_bytes()
        actual = digest(package_bytes)
        # A complete byte identity must occur as this package's recorded root ZIP.
        rows = ledger["acceptedPackages"] + ledger["candidateOnlyPackages"]
        authority = [row for row in rows if row["name"] == name]
        if len(authority) != 1:
            raise ValueError("missing or ambiguous exact package authority: " + name)
        identity = authority[0]
        version_pin = {"CommunalHelper": "1.25.5", "ContortHelper": "1.5.5"}[name]
        if actual != identity["zipSha256"] or actual != expected or identity["version"] != version_pin:
            raise ValueError("exact package hash differs: " + name)
        with zipfile.ZipFile(argument) as archive:
            if len([i for i in archive.infolist() if i.filename == member]) != 1:
                raise ValueError("missing or duplicate pinned DLL member")
            dll_bytes = archive.read(member)
        if digest(dll_bytes) != dll_sha:
            raise ValueError("exact DLL hash differs: " + name)
        dll = work / (name + ".dll")
        dll.write_bytes(dll_bytes)
        receipts["packages"][name] = {"archiveSha256": actual, "assemblySha256": dll_sha}
        for short, typename in classes.items():
            target = work / (short + ".cs")
            with target.open("w") as output:
                subprocess.run(["dotnet", "tool", "run", "ilspycmd", "-r", str(references), "-t", typename, str(dll)],
                    cwd=ROOT, stdout=output, stderr=log, check=True)
            if "Unknown result type" in target.read_text() or "Expected O, but got Unknown" in target.read_text():
                raise ValueError("unresolved reference in pinned reconstruction: " + typename)
            receipts["referenceSources"][typename] = digest(target.read_bytes())
    # Compile the two exact parsing methods; unrelated EntityDatas helpers are
    # not called by this finite trigger and are not reconstructed as substitutes.
    spec = importlib.util.spec_from_file_location("method_reader", ROOT / "scripts/verify-apple-everest-autotiler-stage25kl.py")
    reader = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(reader)
    reference = (work / "EntityDatas.cs").read_text()
    bodies = [reader.method(reference, signature) for signature in (
        "public static IEnumerable<T> Enumerable<T>(",
        "public static IEnumerable<string> EnumerableParse(")]
    (work / "EntityDatas.cs").write_text("using Celeste;\nnamespace ContortHelper;\npublic static class EntityDatas {\n" +
        "\n".join(bodies) + "\n}\n")
    for relative, expected in CANONICAL.items():
        source = args.canonical / relative
        data = source.read_bytes()
        if digest(data) != expected:
            raise ValueError("canonical implementation differs: " + relative)
        (work / Path(relative).name).write_bytes(data)
    for name in ("AppleEverestPlayerBubbleRegion", "AppleEverestRandomSoundTrigger"):
        source = ROOT / "apple-everest/runtime/semantics" / (name + ".cs")
        receipts["productionSources"][name] = digest(source.read_bytes())
        shutil.copyfile(source, work / (name + ".cs"))
    for name, target in [("SnasSemanticsStubs.cs.txt", "Stubs.cs"), ("SnasRandomBubbleProgram.cs.txt", "Program.cs")]:
        shutil.copyfile(ROOT / "tools/AppleEverestBuilder/tests" / name, work / target)
    project = work / "Conformance.csproj"
    project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>' +
        '<TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>' +
        '<Nullable>disable</Nullable><Deterministic>true</Deterministic>' +
        '<UseSharedCompilation>false</UseSharedCompilation><BuildInParallel>false</BuildInParallel>' +
        '</PropertyGroup></Project>\n')
    run(["dotnet", "build", str(project), "-c", "Release", "-m:1", "-p:BuildInParallel=false",
        "-p:UseSharedCompilation=false", "--nologo"])
    run(["dotnet", str(work / "bin/Release/net10.0/Conformance.dll"), str(work / "result.json")])
    result = json.loads((work / "result.json").read_text())
    if result["status"] != "PASS":
        raise ValueError("behavior result did not pass")
    receipts["result"] = result
    (work / "source-bound-result.json").write_text(json.dumps(receipts, sort_keys=True, indent=2) + "\n")
    log.close()
    print("PASS: pinned random/bubble differential execution; " + str(result["assertions"]) + " assertions")


if __name__ == "__main__":
    main()
