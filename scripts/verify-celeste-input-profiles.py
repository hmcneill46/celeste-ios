#!/usr/bin/env python3
"""Deterministic Stage 17B profile/normalization policy tests (no game data)."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import pathlib
import re
import shutil
import tempfile
from argparse import Namespace


class Tests:
    def __init__(self) -> None:
        self.count = 0

    def check(self, condition: bool, label: str) -> None:
        if not condition:
            raise SystemExit(f"error: Stage 17B test failed: {label}")
        self.count += 1
        print(f"PASS {self.count:02d}: {label}")


def sha256(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_module(path: pathlib.Path):
    spec = importlib.util.spec_from_file_location("celeste_managed_stage17b", path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def evidence_for(profile: dict, registry: dict) -> dict:
    payload = registry["managedPayloads"][profile["managedPayload"]]
    files = {
        name: {"sha256": value["sha256"], "assemblyIdentity": value["assemblyIdentity"], "size": 1}
        for name, value in payload["files"].items()
    }
    if profile["celesteContentDll"] == "required":
        value = registry["sharedFiles"]["Celeste.Content.dll"]
        files["Celeste.Content.dll"] = {
            "sha256": value["sha256"], "assemblyIdentity": value["assemblyIdentity"], "size": 1
        }
    content_id = registry["canonicalClasses"][profile["canonicalClass"]]["contentClass"]
    return {
        "files": files,
        "celesteAssemblyReferences": payload["celesteAssemblyReferences"],
        "content": registry["contentClasses"][content_id],
    }


def materialize_patch_preimage(patch_path: pathlib.Path, root: pathlib.Path) -> None:
    """Build a non-proprietary synthetic tree from unified-diff old-side context."""
    lines = patch_path.read_text().splitlines(keepends=True)
    index = 0
    while index < len(lines):
        if not lines[index].startswith("--- a/"):
            index += 1
            continue
        relative = lines[index][6:].strip()
        index += 2
        old_lines: list[str] = []
        while index < len(lines) and not lines[index].startswith("--- a/"):
            header = re.match(r"@@ -(\d+)(?:,(\d+))? \+\d+(?:,\d+)? @@", lines[index])
            if not header:
                index += 1
                continue
            start = int(header.group(1))
            count = int(header.group(2) or "1")
            index += 1
            segment: list[str] = []
            while index < len(lines) and not lines[index].startswith(("@@ ", "--- a/")):
                line = lines[index]
                if line.startswith((" ", "-")):
                    segment.append(line[1:])
                index += 1
            if len(segment) != count:
                raise AssertionError(f"invalid old-side hunk in {relative}")
            while len(old_lines) < start - 1:
                old_lines.append("// synthetic unchanged line\n")
            old_lines[start - 1:start - 1 + count] = segment
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("".join(old_lines))

    (root / "Celeste.csproj").write_text(
        "<Project>\n"
        "    <Prefer32Bit>True</Prefer32Bit>\n"
        "    <Reference Include=\"Steamworks.NET\">\n"
        "      <HintPath>../input/Steamworks.NET.dll</HintPath>\n"
        "    </Reference>\n"
        "</Project>\n"
    )
    (root / "Unrelated.cs").write_text("namespace Synthetic; public sealed class Unrelated {}\n")


def logical_tree_hash(root: pathlib.Path) -> str:
    digest = hashlib.sha256()
    for path in sorted((p for p in root.rglob("*") if p.is_file()),
                       key=lambda p: p.relative_to(root).as_posix()):
        relative = path.relative_to(root).as_posix()
        digest.update(relative.encode() + b"\0" + path.read_bytes() + b"\n")
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", default=".")
    parser.add_argument("--output")
    args = parser.parse_args()
    repo = pathlib.Path(args.repo_root).resolve()
    registry_path = repo / "managed/celeste-input-profiles.json"
    registry = json.loads(registry_path.read_text())
    module = load_module(repo / "scripts/celeste-managed.py")
    tests = Tests()

    tests.check(registry["schemaVersion"] == 1, "registry schema")
    profiles = registry["profiles"]
    tests.check(len(profiles) == 8, "eight supplied profiles registered")
    ids = [profile["id"] for profile in profiles]
    tests.check(len(ids) == len(set(ids)), "profile IDs are unique")
    tests.check({p["store"] for p in profiles} == {"itch.io", "Steam", "Epic Games Store"}, "store metadata")
    tests.check({p["sourcePlatform"] for p in profiles} == {"Linux", "macOS", "Windows"}, "source-platform metadata")
    tests.check({p["runtimeFamily"] for p in profiles} == {"FNA"}, "FNA-only registry")
    tests.check({p["gameVersion"] for p in profiles} == {"1.4.0.0"}, "game-version metadata")
    tests.check({p["canonicalClass"] for p in profiles} == {"celeste-1.4.0.0-a"}, "one canonical game class")

    canonical = registry["canonicalClasses"]["celeste-1.4.0.0-a"]
    tests.check(canonical["decompiledSource"] == {"fileCount": 920, "logicalSha256": "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"}, "raw canonical lock")
    tests.check(canonical["patchedSource"] == {"fileCount": 922, "logicalSha256": "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"}, "patched canonical lock")
    tests.check(canonical["stage6RealAudio"] == {"fileCount": 934, "logicalSha256": "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"}, "Stage 6 canonical lock")
    content = registry["contentClasses"]["celeste-content-1.4.0.0-a"]
    tests.check(content["fileCount"] == 1216 and content["aggregateSha256"] == "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46", "content canonical lock")
    tests.check(all(len(value) == 64 for p in profiles for value in p["testedArchiveSha256"]), "archive fingerprints are SHA-256")

    original_sha = module.sha256_file
    module.sha256_file = lambda path: path.read_text().strip() if path.is_file() else original_sha(path)
    with tempfile.TemporaryDirectory(prefix="stage17b-profiles-") as temporary:
        temporary_root = pathlib.Path(temporary)
        profile_roots: dict[str, pathlib.Path] = {}
        for profile in profiles:
            root = temporary_root / profile["id"]
            root.mkdir()
            profile_roots[profile["id"]] = root
            for relative, digest in profile["requiredMarkers"].items():
                path = root.joinpath(*pathlib.PurePosixPath(relative).parts)
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(digest)
            evidence = evidence_for(profile, registry)
            matches = [candidate for candidate in profiles if module.profile_matches(candidate, registry, root, evidence)]
            tests.check([value["id"] for value in matches] == [profile["id"]], f"exact detection: {profile['id']}")

        unknown = evidence_for(profiles[0], registry)
        unknown["files"]["Celeste.exe"] = dict(unknown["files"]["Celeste.exe"], sha256="0" * 64)
        baseline_root = profile_roots[profiles[0]["id"]]
        tests.check(not any(module.profile_matches(p, registry, baseline_root, unknown) for p in profiles), "unknown executable rejected")
        xna = evidence_for(profiles[0], registry)
        xna["celesteAssemblyReferences"] = [{"name": "Microsoft.Xna.Framework", "version": "4.0.0.0"}]
        tests.check(not any(module.profile_matches(p, registry, baseline_root, xna) for p in profiles), "XNA evidence rejected")
        mixed = evidence_for(profiles[0], registry)
        mixed["files"]["FNA.dll"] = evidence_for(profiles[-1], registry)["files"]["FNA.dll"]
        tests.check(not any(module.profile_matches(p, registry, baseline_root, mixed) for p in profiles), "mixed managed installation rejected")
        duplicate_registry = json.loads(json.dumps(registry))
        duplicate_registry["profiles"].append(dict(profiles[0], id="synthetic-duplicate"))
        ambiguous = [p["id"] for p in duplicate_registry["profiles"]
                     if module.profile_matches(p, duplicate_registry, baseline_root,
                                               evidence_for(profiles[0], registry))]
        tests.check(ambiguous == [profiles[0]["id"], "synthetic-duplicate"],
                    "ambiguous duplicate profile is rejected by the one-match policy")
    module.sha256_file = original_sha

    with tempfile.TemporaryDirectory(prefix="stage17b-layout-") as temporary:
        layout = pathlib.Path(temporary)
        direct = layout / "direct"
        (direct / "Content").mkdir(parents=True)
        (direct / "Celeste.exe").touch()
        (direct / "FNA.dll").touch()
        tests.check(module.game_root_candidates(direct) == [direct.resolve()],
                    "direct supported layout is bounded and unambiguous")
        wrapper = layout / "wrapper"
        deep = wrapper / "one/two/game"
        (deep / "Content").mkdir(parents=True)
        (deep / "Celeste.exe").touch()
        (deep / "FNA.dll").touch()
        tests.check(module.game_root_candidates(wrapper) == [],
                    "arbitrary recursive executable discovery is rejected")
        outside = layout / "outside"
        shutil.copytree(direct, outside)
        linked = layout / "selected"
        linked.mkdir()
        (linked / "game").symlink_to(outside, target_is_directory=True)
        tests.check(module.game_root_candidates(linked) == [],
                    "wrapper symlink escaping selected root is rejected")

    adapters = registry["adapters"]
    tests.check(adapters["none"] == {"kind": "no-op", "changedFiles": []}, "itch/Epic no-op adapter")
    steam = adapters["steam-1.4.0.0-to-canonical-a"]
    patch_path = repo / steam["path"]
    tests.check(sha256(patch_path) == steam["sha256"], "Steam adapter hash")
    tests.check(set(steam["changedFiles"]) == {"Celeste/Achievements.cs", "Celeste/Celeste.cs", "Celeste/Commands.cs", "Celeste/Stats.cs", "Celeste.csproj"}, "Steam adapter target allow-list")
    patch_text = patch_path.read_text()
    tests.check("SteamAPI" in patch_text and "SteamUserStats" in patch_text and "global steam stats" in patch_text, "Steam integration removal coverage")
    tests.check("remove-steam.patch" not in json.dumps(registry) and "remove-steam2.patch" not in json.dumps(registry), "legacy Steam patches not used")
    prepare_text = (repo / "scripts/prepare-celeste-managed.sh").read_text()
    tests.check('patch --batch --forward -F 0 -p1' in prepare_text, "existing transforms remain zero-fuzz")
    managed_text = (repo / "scripts/celeste-managed.py").read_text()
    tests.check('["patch", "--batch", "--forward", "-F", "0", "-p1"' in managed_text, "Steam adapter is zero-fuzz")

    with tempfile.TemporaryDirectory(prefix="stage17b-normalize-") as temporary:
        temporary_root = pathlib.Path(temporary)
        source = temporary_root / "source"
        first = temporary_root / "first"
        second = temporary_root / "second"
        materialize_patch_preimage(patch_path, source)
        shutil.copytree(source, first)
        shutil.copytree(source, second)
        manifest = temporary_root / "input.json"
        manifest.write_text(json.dumps({
            "normalizationAdapter": "steam-1.4.0.0-to-canonical-a",
            "canonicalClass": "celeste-1.4.0.0-a",
        }))
        for number, candidate in enumerate((first, second), 1):
            module.cmd_normalize(Namespace(
                root=str(candidate), input_manifest=str(manifest), profiles=str(registry_path),
                repo_root=str(repo), output=str(temporary_root / f"normalization-{number}.json"),
            ))
        tests.check(logical_tree_hash(first) == logical_tree_hash(second), "Steam normalization is deterministic")
        tests.check((first / "Unrelated.cs").read_bytes() == (source / "Unrelated.cs").read_bytes(),
                    "Steam normalization leaves unrelated source unchanged")
        tests.check(not any("Steam" in path.read_text() for path in first.rglob("*") if path.is_file()),
                    "Steam normalization removes storefront references")

        unexpected = temporary_root / "unexpected"
        shutil.copytree(source, unexpected)
        achievement = unexpected / "Celeste/Achievements.cs"
        achievement.write_text(achievement.read_text().replace("using Steamworks;", "using ChangedSteamworks;", 1))
        rejected = False
        try:
            module.cmd_normalize(Namespace(
                root=str(unexpected), input_manifest=str(manifest), profiles=str(registry_path),
                repo_root=str(repo), output=str(temporary_root / "unexpected.json"),
            ))
        except SystemExit:
            rejected = True
        tests.check(rejected, "unexpected Steam source fails closed")

    tests.check("--force" not in (repo / "scripts/validate-celeste-input.sh").read_text(), "no force/accept-any mode")
    tests.check("Steamworks.NET.dll" not in (repo / "managed/templates/Celeste.Modern.csproj").read_text(), "Steam dependency absent downstream")
    tests.check("CelesteTvOS.PerformanceHUD.v1" not in json.dumps(registry), "host preferences absent from profile data")
    tests.check((repo / "docs/CELESTE_INPUTS.md").is_file(), "public input support matrix exists")
    tests.check("managed/celeste-input-profiles.json" in (repo / "scripts/validate-celeste-input.sh").read_text(), "validator uses closed registry")
    tests.check("resolvedRootRelative" in (repo / "build-tvos.sh").read_text(), "public builder consumes safe resolved layout")
    tests.check("6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc" in (repo / "managed/celeste-generation.lock.json").read_text(), "accepted native hash unchanged")

    result = {"schemaVersion": 1, "testCount": tests.count, "result": "pass",
              "profileCount": len(profiles), "canonicalClassCount": len(registry["canonicalClasses"])}
    if args.output:
        pathlib.Path(args.output).write_text(json.dumps(result, indent=2, sort_keys=True) + "\n")
    print(f"STAGE17B_INPUT_PROFILE_TESTS PASS count={tests.count}")


if __name__ == "__main__":
    main()
