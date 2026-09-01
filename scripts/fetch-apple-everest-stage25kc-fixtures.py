#!/usr/bin/env python3
"""Reacquire and regenerate ignored Stage 25K-C public audit fixtures."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import subprocess
import sys
import zipfile


METADATA = {
    "everest_update.yaml": "https://maddie480.ovh/celeste/everest_update.yaml",
    "mod_dependency_graph.yaml": "https://maddie480.ovh/celeste/mod_dependency_graph.yaml",
    "mod_search_database.yaml": "https://maddie480.ovh/celeste/mod_search_database.yaml",
}


def canonical(value: object) -> bytes:
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n").encode()


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def download(url: str, output: pathlib.Path, expected: str | None = None) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    if expected is not None and output.is_file() and sha256(output) == expected:
        return
    source_url = url
    mirror = re.fullmatch(r"https://gamebanana\.com/mmdl/(\d+)", url)
    if mirror:
        source_url = f"https://celestemodupdater.0x0a.de/banana-mirror/{mirror.group(1)}.zip"
    command = ["curl", "--fail", "--location", "--retry", "10", "--retry-all-errors",
               "--retry-delay", "2", "--silent", "--show-error", "--output", str(output)]
    if expected is not None and output.is_file() and output.stat().st_size:
        command.extend(["--continue-at", "-"])
    command.append(source_url)
    subprocess.run(command, check=True)
    actual = sha256(output)
    if expected is not None and actual != expected:
        raise SystemExit(f"FAIL: public download drift for {url}: {actual}")


def extract(archive: pathlib.Path, output: pathlib.Path) -> None:
    output.mkdir(parents=True, exist_ok=True)
    root = output.resolve()
    with zipfile.ZipFile(archive) as value:
        for member in value.infolist():
            target = (output / member.filename).resolve()
            if target != root and root not in target.parents:
                raise SystemExit(f"FAIL: ZIP path escaped fixture root: {member.filename}")
        value.extractall(output)


def run(*args: str, cwd: pathlib.Path) -> None:
    subprocess.run(list(args), cwd=cwd, check=True)


def required_dependencies(path: pathlib.Path) -> list[dict[str, str]]:
    """Read the deliberately small Everest dependency subset without a YAML runtime."""
    result: list[dict[str, str]] = []
    in_dependencies = False
    section_indent = 0
    current: dict[str, str] | None = None
    for raw in path.read_text(errors="replace").splitlines():
        line = raw.split("#", 1)[0].rstrip()
        stripped = line.strip()
        if not stripped:
            continue
        indent = len(line) - len(line.lstrip())
        if stripped == "Dependencies:":
            in_dependencies = True
            section_indent = indent
            current = None
            continue
        if in_dependencies and indent <= section_indent:
            in_dependencies = False
            current = None
        if not in_dependencies:
            continue
        match = re.fullmatch(r"-?\s*Name:\s*[\"']?([^\"']+?)[\"']?\s*", stripped)
        if match:
            current = {"Name": match.group(1).strip()}
            result.append(current)
            continue
        match = re.fullmatch(r"Version:\s*[\"']?([^\"']+?)[\"']?\s*", stripped)
        if match and current is not None:
            current["Version"] = match.group(1).strip()
    if any(set(row) != {"Name", "Version"} for row in result):
        raise SystemExit(f"FAIL: incomplete distributed dependency metadata in {path.name}")
    return result


def validate_distributed_graph(graph: dict, extracted: pathlib.Path) -> None:
    """Independently prove tracked edges match every downloaded everest.yaml."""
    runtime_by_owner: dict[str, list[dict[str, str]]] = {}
    for edge in graph["runtimeEdges"]:
        runtime_by_owner.setdefault(edge["owner"], []).append(
            {"Name": edge["name"], "Version": edge["requiredVersion"]})
    key = lambda row: (row["Name"], row["Version"])
    for node in graph["nodes"]:
        metadata = sorted((extracted / node["name"]).glob("everest.y*ml"))
        if len(metadata) != 1:
            raise SystemExit(f"FAIL: expected one root everest.yaml for {node['name']}")
        actual = sorted(required_dependencies(metadata[0]), key=key)
        expected = sorted([
            {"Name": row["name"], "Version": row["requiredVersion"]}
            for row in node["dependencies"]
        ] + runtime_by_owner.get(node["name"], []), key=key)
        if actual != expected:
            raise SystemExit(f"FAIL: distributed dependency graph drift for {node['name']}")


def update_database_entry(path: pathlib.Path, name: str) -> dict[str, object]:
    """Extract one updater entry while ignoring unrelated mutable catalogue fields."""
    lines = path.read_text(errors="replace").splitlines()
    start = next((index for index, line in enumerate(lines) if line == f"{name}:"), None)
    if start is None:
        raise SystemExit(f"FAIL: {name} is absent from the live Everest update database")
    result: dict[str, object] = {"xxHash": []}
    in_xxhash = False
    for line in lines[start + 1:]:
        if line and not line[0].isspace():
            break
        stripped = line.strip()
        match = re.fullmatch(r"(Version|LastUpdate|GameBananaFileId|URL|Size):\s*(.*?)\s*", stripped)
        if match:
            value = match.group(2).strip("'\"")
            result[match.group(1)] = int(value) if match.group(1) in {"LastUpdate", "Size"} else value
            in_xxhash = False
        elif stripped == "xxHash:":
            in_xxhash = True
        elif in_xxhash and stripped.startswith("- "):
            result["xxHash"].append(stripped[2:].strip("'\""))
    return result


def validate_live_release_records(graph: dict, update_database: pathlib.Path) -> None:
    """Allow catalogue churn while pinning all 52 semantically relevant records."""
    for node in graph["nodes"]:
        live = update_database_entry(update_database, node["name"])
        expected = {
            "Version": node["resolvedVersion"], "LastUpdate": node["lastUpdateUnix"],
            "GameBananaFileId": node["gameBananaFileId"], "URL": node["publicUrl"],
            "Size": node["zipBytes"], "xxHash": node["updaterXxHash"],
        }
        if live != expected:
            raise SystemExit(f"FAIL: live release identity drift for {node['name']}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    parser.add_argument("--artifact-output", type=pathlib.Path,
                        help="optional regenerated tracked-artifact directory")
    parser.add_argument("--resume", action="store_true",
                        help="resume a fixture directory that began empty in an earlier attempt")
    args = parser.parse_args()
    repo = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    output = args.output.resolve()
    if output.exists() and any(output.iterdir()) and not args.resume:
        raise SystemExit("FAIL: fixture output must be absent or empty")
    output.mkdir(parents=True, exist_ok=True)

    graph = json.loads((repo / "apple-everest/strawberry-jam-dependency-graph-stage25kc.json").read_text())
    control = json.loads((repo / "apple-everest/third-collab-control-stage25kc.json").read_text())

    metadata_root = output / "public-metadata"
    for name, url in METADATA.items():
        download(url, metadata_root / name)
    validate_live_release_records(graph, metadata_root / "everest_update.yaml")

    plan_rows = []
    for node in graph["nodes"]:
        archive = output / "packages" / f"{node['name']}.zip"
        download(node["publicUrl"], archive, node["zipSha256"])
        extract(archive, output / "extracted" / node["name"])
        plan_rows.append({"name": node["name"], "version": node["resolvedVersion"],
                          "bytes": node["zipBytes"], "url": node["publicUrl"],
                          "fileId": node["gameBananaFileId"],
                          "lastUpdate": node["lastUpdateUnix"],
                          "xxHash": node["updaterXxHash"]})
    validate_distributed_graph(graph, output / "extracted")
    (output / "package-plan.json").write_bytes(canonical({
        "count": len(plan_rows), "totalBytes": sum(row["bytes"] for row in plan_rows),
        "rows": sorted(plan_rows, key=lambda row: row["name"]),
    }))
    (output / "root-closure.json").write_bytes(canonical({
        "count": graph["nodeCount"], "names": sorted(row["name"] for row in graph["nodes"]),
        "edges": graph["requiredEdges"], "missing": graph["runtimeDependenciesNotDownloaded"],
    }))

    dotnet_path = repo / ".build/apple-everest/toolchain/dotnet8/dotnet"
    if not dotnet_path.is_file():
        run(str(repo / "scripts/bootstrap-apple-everest-host.sh"), cwd=repo)
    dotnet = str(dotnet_path)
    builder_project = repo / "tools/AppleEverestBuilder/AppleEverestBuilder.csproj"
    tests_project = repo / "tools/AppleEverestBuilder/tests/AppleEverestBuilder.Tests.csproj"
    neutral_cwd = pathlib.Path("/tmp")
    run(dotnet, "build", str(builder_project), "-c", "Release", "--nologo", cwd=neutral_cwd)
    run(dotnet, "build", str(tests_project), "-c", "Release", "--nologo", cwd=neutral_cwd)
    builder = repo / "tools/AppleEverestBuilder/bin/Release/net8.0/AppleEverestBuilder.dll"
    tests = repo / "tools/AppleEverestBuilder/tests/bin/Release/net8.0/AppleEverestBuilder.Tests.dll"
    census_root = output / "dll-census"
    census_root.mkdir(exist_ok=True)
    for owner in sorted(item for item in (output / "extracted").iterdir() if item.is_dir()):
        dlls = sorted(path for path in owner.rglob("*.dll") if "/obj/" not in path.as_posix())
        for dll in dlls:
            target = census_root / f"{owner.name}--{dll.name}.json"
            run(dotnet, str(builder), "census-dll", "--dll", str(dll), "--output", str(target), cwd=repo)

    third = output / "third-collab"
    (third / "packages").mkdir(parents=True, exist_ok=True)
    (third / "extracted").mkdir(exist_ok=True)
    full_plan_lines = []
    full_hash_lines = []
    for package in control["packagePlan"]:
        archive = third / "packages" / f"{package['name']}.zip"
        download(package["url"], archive, package["zipSha256"])
        extract(archive, third / "extracted" / package["name"])
        full_plan_lines.append("\t".join(map(str, [package["name"], package["version"],
            package["fileId"], package["bytes"], package["lastUpdateUnix"], package["url"]])))
        full_hash_lines.append("\t".join([package["name"], package["version"], package["zipSha256"]]))
    (third / "full-plan.tsv").write_text("\n".join(full_plan_lines) + "\n")
    (third / "full-hashes.tsv").write_text("\n".join(full_hash_lines) + "\n")
    root_hash_lines = ["\t".join([row["name"], row["version"], row["zipSha256"]])
                       for row in control["candidates"]]
    (third / "hashes.tsv").write_text("\n".join(root_hash_lines) + "\n")
    (third / "closures.json").write_bytes(canonical({row["name"]: {
        "names": row["closurePackages"], "edges": row["requiredEdges"],
        "optional": row["optionalEdges"]} for row in control["candidates"]}))
    audit_args = [dotnet, str(builder), "audit"]
    for archive in sorted((third / "packages").glob("*.zip")):
        audit_args.extend(["--mod", str(archive)])
    audit_args.extend(["--output", str(third / "builder-audit.json")])
    run(*audit_args, cwd=repo)

    generator = repo / "scripts/generate-apple-everest-stage25kc.py"
    run(sys.executable, str(generator), "--fixture-root", str(output), cwd=repo)
    raw = json.loads((output / "raw-analysis.json").read_text())
    progression_input = [{"sid": row["sid"], "rooms": row["rooms"],
                          "berries": row["berries"], "checkpoints": row["checkpoints"],
                          "heart": row["heart"], "cassette": row["cassette"]}
                         for row in raw["mapCensus"]["maps"]]
    (output / "progression-input.json").write_bytes(canonical(progression_input))
    run(dotnet, str(tests), "--stage25kc-progression", str(output / "progression-input.json"),
        str(output / "progression-scale.json"), cwd=repo)
    artifact_output = (args.artifact_output or output / "generated-artifacts").resolve()
    run(sys.executable, str(generator), "--fixture-root", str(output),
        "--output-root", str(artifact_output), cwd=repo)
    print(f"PASS: reacquired Stage 25K-C fixtures at {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
