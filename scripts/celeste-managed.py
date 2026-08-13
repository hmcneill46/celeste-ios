#!/usr/bin/env python3
"""Deterministic, privacy-safe helpers for the Stage 3A managed pipeline."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import re
import shutil
import subprocess
import sys
from collections import Counter
from typing import Any, Iterable


def sha256_file(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_json(path: pathlib.Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(value, indent=2, sort_keys=True) + "\n"
    if "/" + "Users/" in text or re.search(r"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", text):
        raise SystemExit(f"error: refusing to write private path or email to {path.name}")
    path.write_text(text, encoding="utf-8")


def load_json(path: pathlib.Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def run_text(command: list[str]) -> str:
    return subprocess.run(command, check=True, text=True, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE).stdout


def monodis_identity(path: pathlib.Path) -> dict[str, str]:
    output = run_text(["monodis", "--assembly", str(path)])
    name = re.search(r"^Name:\s+(.+)$", output, re.MULTILINE)
    version = re.search(r"^Version:\s+(.+)$", output, re.MULTILINE)
    if not name or not version:
        raise SystemExit(f"error: could not read managed identity for {path.name}")
    return {"name": name.group(1).strip(), "version": version.group(1).strip()}


def monodis_references(path: pathlib.Path) -> list[dict[str, str]]:
    output = run_text(["monodis", "--assemblyref", str(path)])
    references: list[dict[str, str]] = []
    current_version: str | None = None
    for line in output.splitlines():
        version = re.match(r"^\d+: Version=(.+)$", line)
        if version:
            current_version = version.group(1).strip()
            continue
        name = re.match(r"^\s*Name=(.+)$", line)
        if name and current_version:
            references.append({"name": name.group(1).strip(), "version": current_version})
            current_version = None
    return sorted(references, key=lambda item: item["name"].lower())


def content_manifest(content_root: pathlib.Path) -> dict[str, Any]:
    aggregate = hashlib.sha256()
    count = 0
    total_bytes = 0
    for path in sorted((p for p in content_root.rglob("*") if p.is_file()),
                       key=lambda p: p.relative_to(content_root).as_posix()):
        relative = path.relative_to(content_root).as_posix()
        size = path.stat().st_size
        digest = sha256_file(path)
        aggregate.update(relative.encode("utf-8"))
        aggregate.update(b"\0")
        aggregate.update(str(size).encode("ascii"))
        aggregate.update(b"\0")
        aggregate.update(digest.encode("ascii"))
        aggregate.update(b"\n")
        count += 1
        total_bytes += size
    return {
        "aggregateAlgorithm": "sha256(relative-path NUL size NUL file-sha256 newline)",
        "aggregateSha256": aggregate.hexdigest(),
        "fileCount": count,
        "totalBytes": total_bytes,
    }


def safe_registry_relative(value: str) -> pathlib.PurePosixPath:
    path = pathlib.PurePosixPath(value)
    if path.is_absolute() or not path.parts or any(part in ("", ".", "..") for part in path.parts):
        raise SystemExit(f"error: unsafe relative path in Celeste input profile registry: {value!r}")
    return path


def game_root_candidates(supplied: pathlib.Path) -> list[pathlib.Path]:
    """Return only documented direct, app-bundle, and one-wrapper layouts."""
    values: list[pathlib.Path] = []
    supplied = supplied.resolve()

    def add(candidate: pathlib.Path) -> None:
        candidate = candidate.resolve()
        if not candidate.is_relative_to(supplied):
            return
        if candidate in values:
            return
        if ((candidate / "Celeste.exe").is_file() and
                (candidate / "FNA.dll").is_file() and
                (candidate / "Content").is_dir()):
            values.append(candidate)

    add(supplied)
    add(supplied / "Contents/Resources")
    add(supplied / "Celeste.app/Contents/Resources")
    for child in sorted((path for path in supplied.iterdir() if path.is_dir()), key=lambda path: path.name):
        add(child)
        add(child / "Contents/Resources")
        add(child / "Celeste.app/Contents/Resources")
    return values


def exact_file_evidence(path: pathlib.Path) -> dict[str, Any]:
    identity = monodis_identity(path)
    file_output = run_text(["file", "-b", str(path)]).strip()
    if "PE32" not in file_output or "Mono/.Net assembly" not in file_output:
        raise SystemExit(f"error: detected {path.name} is not a managed PE32 assembly")
    return {
        "assemblyIdentity": identity,
        "managedArchitectureEvidence": "PE32 Mono/.Net assembly (legacy AnyCPU-compatible input)",
        "sha256": sha256_file(path),
        "size": path.stat().st_size,
    }


def profile_matches(profile: dict[str, Any], registry: dict[str, Any], root: pathlib.Path,
                    evidence: dict[str, Any]) -> bool:
    payload = registry["managedPayloads"][profile["managedPayload"]]
    for name, expected in payload["files"].items():
        actual = evidence["files"].get(name)
        if actual is None or actual["sha256"] != expected["sha256"] or actual["assemblyIdentity"] != expected["assemblyIdentity"]:
            return False
    content_policy = profile["celesteContentDll"]
    actual_content_dll = evidence["files"].get("Celeste.Content.dll")
    if content_policy == "required":
        expected_content_dll = registry["sharedFiles"]["Celeste.Content.dll"]
        if (actual_content_dll is None or actual_content_dll["sha256"] != expected_content_dll["sha256"] or
                actual_content_dll["assemblyIdentity"] != expected_content_dll["assemblyIdentity"]):
            return False
    elif content_policy == "absent":
        if actual_content_dll is not None:
            return False
    else:
        raise SystemExit(f"error: invalid Celeste.Content.dll policy in profile {profile['id']}")
    if evidence["celesteAssemblyReferences"] != payload["celesteAssemblyReferences"]:
        return False
    content_class = registry["contentClasses"][registry["canonicalClasses"][profile["canonicalClass"]]["contentClass"]]
    if any(evidence["content"][key] != content_class[key]
           for key in ("fileCount", "totalBytes", "aggregateSha256")):
        return False
    for relative, expected_hash in profile["requiredMarkers"].items():
        path = root.joinpath(*safe_registry_relative(relative).parts)
        if not path.is_file() or sha256_file(path) != expected_hash:
            return False
    for relative in profile["absentFiles"]:
        if root.joinpath(*safe_registry_relative(relative).parts).exists():
            return False
    return True


def cmd_validate(args: argparse.Namespace) -> None:
    supplied = pathlib.Path(args.game_root).expanduser().resolve()
    registry = load_json(pathlib.Path(args.profiles))
    if not supplied.is_dir():
        raise SystemExit("error: --game-root is not an existing directory")
    candidates = game_root_candidates(supplied)
    if not candidates:
        celeste_executable = supplied / "Celeste.exe"
        if celeste_executable.is_file():
            references = monodis_references(celeste_executable)
            if not any(item["name"] == "FNA" for item in references):
                raise SystemExit("error: unsupported Celeste input: this package is not an FNA build")
        raise SystemExit(
            "error: no supported Celeste/FNA game root was found in the selected folder; "
            "select the extracted game folder or Celeste.app"
        )
    if len(candidates) != 1:
        raise SystemExit("error: the selected folder contains multiple possible Celeste/FNA game roots")
    root = candidates[0]

    everest_patterns = (
        "everest-lib", "everest-settings", "everest-update", "miniinstaller",
        "monomod", "mono.cecil", "celeste.mod.mm", "olympus", "modsettings",
    )
    markers = sorted(
        path.name for path in root.iterdir()
        if any(pattern in path.name.lower() for pattern in everest_patterns)
    )
    if markers:
        raise SystemExit("error: Everest/MonoMod marker files were found: " + ", ".join(markers))

    files: dict[str, Any] = {}
    for name in ("Celeste.exe", "FNA.dll", "Celeste.Content.dll", "Steamworks.NET.dll"):
        path = root / name
        if path.is_file():
            files[name] = exact_file_evidence(path)
    references = monodis_references(root / "Celeste.exe")
    fna_references = [item for item in references if item["name"] == "FNA"]
    if len(fna_references) != 1:
        raise SystemExit("error: unsupported Celeste input: the managed executable is not an unambiguous FNA build")
    content = content_manifest(root / "Content")
    evidence = {"files": files, "celesteAssemblyReferences": references, "content": content}
    matches = [profile for profile in registry["profiles"] if profile_matches(profile, registry, root, evidence)]
    if not matches:
        raise SystemExit(
            "error: Celeste was found, but this exact build is not currently supported.\n"
            f"  Managed assembly: {files['Celeste.exe']['assemblyIdentity']['version']}\n"
            f"  Runtime: FNA {files['FNA.dll']['assemblyIdentity']['version']}\n"
            f"  Executable fingerprint: {files['Celeste.exe']['sha256']}\n"
            "  See docs/CELESTE_INPUTS.md for tested builds."
        )
    if len(matches) != 1:
        raise SystemExit("error: Celeste input profile detection is ambiguous; refusing the mixed installation")
    profile = matches[0]
    payload = registry["managedPayloads"][profile["managedPayload"]]

    content_dll = root / "Celeste.Content.dll"
    if content_dll.is_file():
        content_types = run_text(["monodis", "--typedef", str(content_dll)])
        content_resources = run_text(["monodis", "--manifest", str(content_dll)])
        type_rows = re.findall(r"^\d+:.*$", content_types, re.MULTILINE)
        if len(type_rows) != 1 or "(null)" not in type_rows[0] or "Manifestresource Table (1..0)" not in content_resources:
            raise SystemExit("error: Celeste.Content.dll is not the locked empty identity assembly")

    relative = root.relative_to(supplied).as_posix()
    source_root = "$CELESTE_GAME_ROOT" if relative == "." else f"$CELESTE_GAME_ROOT/{relative}"
    result = {
        "schemaVersion": 2,
        "sourceRoot": source_root,
        "resolvedRootRelative": relative,
        "validation": "supported-explicit-fna-input-profile",
        "profileId": profile["id"],
        "store": profile["store"],
        "sourcePlatform": profile["sourcePlatform"],
        "packageLayout": profile["packageLayout"],
        "runtimeFamily": profile["runtimeFamily"],
        "canonicalClass": profile["canonicalClass"],
        "normalizationAdapter": payload["normalizationAdapter"],
        "decompilerReferences": payload["decompilerReferences"],
        "gameVersion": profile["gameVersion"],
        "gameVersionEvidence": {
            "kind": "explicit-profile-fingerprints-and-decompiled-constructor",
            "note": "Generation independently verifies that the canonical source constructs Version(1, 4, 0, 0).",
        },
        "everestMarkerCount": 0,
        "files": files,
        "celesteAssemblyReferences": references,
        "celesteContentAssembly": {
            "sourcePresent": content_dll.is_file(),
            "definedTypeCount": 0,
            "embeddedResourceCount": 0,
            "handling": "reconstruct identity-only modern library; no proprietary content is embedded",
        },
        "content": content,
    }
    write_json(pathlib.Path(args.output), result)
    print("Celeste input detected:")
    print(f"  Game: Celeste {profile['gameVersion']}")
    print(f"  Store: {profile['store']}")
    print(f"  Source platform: {profile['sourcePlatform']}")
    print(f"  Runtime: {profile['runtimeFamily']}")
    print(f"  Profile: {profile['id']}")
    print(f"  Canonical game: {profile['canonicalClass']}")
    print("Input validation: PASS")


def cmd_normalize(args: argparse.Namespace) -> None:
    root = pathlib.Path(args.root).resolve()
    manifest = load_json(pathlib.Path(args.input_manifest))
    registry = load_json(pathlib.Path(args.profiles))
    adapter_id = manifest["normalizationAdapter"]
    adapter = registry["adapters"].get(adapter_id)
    if adapter is None:
        raise SystemExit(f"error: unknown input normalization adapter: {adapter_id}")
    changed_files = adapter["changedFiles"]
    if adapter["kind"] == "exact-patch":
        repository = pathlib.Path(args.repo_root).resolve()
        patch_path = repository.joinpath(*safe_registry_relative(adapter["path"]).parts)
        if sha256_file(patch_path) != adapter["sha256"]:
            raise SystemExit(f"error: locked input adapter changed: {adapter_id}")
        process = subprocess.run(
            ["patch", "--batch", "--forward", "-F", "0", "-p1", "-d", str(root)],
            stdin=patch_path.open("rb"), stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
        )
        if process.returncode != 0:
            raise SystemExit(f"error: exact input normalization failed for {adapter_id}")
        if adapter_id == "steam-1.4.0.0-to-canonical-a":
            project = root / "Celeste.csproj"
            text = project.read_text(encoding="utf-8")
            replacements = (
                ("    <Prefer32Bit>True</Prefer32Bit>\n", "    <PlatformTarget>x86</PlatformTarget>\n"),
                ("    <Reference Include=\"Steamworks.NET\">\n"
                 "      <HintPath>../input/Steamworks.NET.dll</HintPath>\n"
                 "    </Reference>\n", ""),
            )
            for old, new in replacements:
                if text.count(old) != 1:
                    raise SystemExit("error: exact Steam project normalization site changed")
                text = text.replace(old, new, 1)
            project.write_text(text, encoding="utf-8")
    elif adapter["kind"] != "no-op":
        raise SystemExit(f"error: unsupported input adapter kind: {adapter['kind']}")
    remaining = []
    for path in root.rglob("*"):
        if path.is_file() and path.suffix in (".cs", ".csproj"):
            text = path.read_text(encoding="utf-8")
            if re.search(r"Steamworks|SteamAPI|SteamApps|SteamUserStats|global steam stats", text):
                remaining.append(path.relative_to(root).as_posix())
    if remaining:
        raise SystemExit("error: storefront integration remains after normalization: " + ", ".join(remaining))
    write_json(pathlib.Path(args.output), {
        "schemaVersion": 1,
        "adapter": adapter_id,
        "canonicalClass": manifest["canonicalClass"],
        "changedFiles": changed_files,
        "fuzzyPatchAllowed": False,
    })
    print(f"applied exact input adapter: {adapter_id}")


def iter_logical_files(root: pathlib.Path) -> Iterable[pathlib.Path]:
    ignored_dirs = {"bin", "obj", ".git"}
    for path in sorted(root.rglob("*"), key=lambda p: p.relative_to(root).as_posix()):
        if not path.is_file():
            continue
        if any(part in ignored_dirs for part in path.relative_to(root).parts):
            continue
        yield path


def cmd_tree_manifest(args: argparse.Namespace) -> None:
    root = pathlib.Path(args.root).resolve()
    aggregate = hashlib.sha256()
    entries: list[dict[str, Any]] = []
    normalised_files: list[str] = []
    for path in iter_logical_files(root):
        relative = path.relative_to(root).as_posix()
        data = path.read_bytes()
        if args.kind == "decompiled-source" and path.suffix == ".csproj":
            text = data.decode("utf-8")

            def normalise_hint(match: re.Match[str]) -> str:
                name = pathlib.PurePosixPath(match.group(1).replace("\\", "/")).name
                root_name = "$INPUT_ROOT" if name == "FNA.dll" else "$DECOMPILER_REFERENCE_ROOT"
                return f"<HintPath>{root_name}/{name}</HintPath>"

            text, count = re.subn(r"<HintPath>([^<]+)</HintPath>", normalise_hint, text)
            if count:
                data = text.encode("utf-8")
                normalised_files.append(relative)
        digest = hashlib.sha256(data).hexdigest()
        size = len(data)
        entries.append({"path": relative, "sha256": digest, "size": size})
        aggregate.update(relative.encode("utf-8") + b"\0" + digest.encode("ascii") + b"\n")
    write_json(pathlib.Path(args.output), {
        "schemaVersion": 1,
        "kind": args.kind,
        "root": f"${args.placeholder}",
        "fileCount": len(entries),
        "logicalSha256": aggregate.hexdigest(),
        "normalisation": {
            "decompilerReferenceHintPaths": normalised_files,
        },
        "files": entries,
    })
    print(f"{args.kind} logical SHA-256: {aggregate.hexdigest()}")


FMOD_IMPORT = re.compile(
    r'(?m)^(?P<indent>[ \t]*)\[DllImport\("(?P<library>fmod(?:studio|_SDL)?)"'
    r'(?P<attribute_tail>[^\n]*)\)\]\n'
    r'(?P=indent)(?P<prefix>(?:private|internal|public)\s+static\s+)extern\s+'
    r'(?P<signature>[^;\n]+);$'
)


def cmd_patch_fmod(args: argparse.Namespace) -> None:
    root = pathlib.Path(args.root).resolve()
    expected = args.expected_count
    entries: list[dict[str, Any]] = []
    for path in sorted(root.rglob("*.cs"), key=lambda p: p.relative_to(root).as_posix()):
        original = path.read_text(encoding="utf-8")

        def replace(match: re.Match[str]) -> str:
            signature = match.group("signature")
            symbol_match = re.search(r"([A-Za-z_][A-Za-z0-9_]*)\s*\(", signature)
            if not symbol_match:
                raise SystemExit(f"error: cannot identify FMOD import in {path.relative_to(root)}")
            symbol = symbol_match.group(1)
            line = original.count("\n", 0, match.start()) + 1
            entries.append({
                "file": path.relative_to(root).as_posix(),
                "library": match.group("library"),
                "line": line,
                "symbol": symbol,
            })
            indent = match.group("indent")
            prefix = match.group("prefix")
            return (
                f'{indent}// Stage 3A compile-only FMOD boundary; native FMOD is intentionally absent.\n'
                f'{indent}{prefix}{signature} => throw new global::System.PlatformNotSupportedException('
                f'"FMOD native call {symbol} is unavailable in the Stage 3A closure harness.");'
            )

        patched, count = FMOD_IMPORT.subn(replace, original)
        if count:
            path.write_text(patched, encoding="utf-8")

    remaining = []
    for path in root.rglob("*.cs"):
        text = path.read_text(encoding="utf-8")
        if re.search(r'\[DllImport\("fmod(?:studio|_SDL)?"', text):
            remaining.append(path.relative_to(root).as_posix())
    if len(entries) != expected or remaining:
        raise SystemExit(
            f"error: FMOD source-boundary transformed {len(entries)} imports, expected {expected}; "
            f"remaining files: {remaining}"
        )
    counts = Counter(item["library"] for item in entries)
    write_json(pathlib.Path(args.output), {
        "schemaVersion": 1,
        "policy": "compile-only-managed-throw; no native FMOD library or symbol provider",
        "transformedImportCount": len(entries),
        "byLibrary": dict(sorted(counts.items())),
        "imports": entries,
    })
    print(f"replaced {len(entries)} FMOD native imports with explicit compile-only throws")


DYNAMIC_PATTERNS: tuple[tuple[str, str], ...] = (
    ("assembly-get-types", r"Assembly\.(?:GetExecutingAssembly\(\)|GetEntryAssembly\(\)|GetAssembly\([^)]*\))\.GetTypes\(\)|\.GetTypes\(\)"),
    ("type-get-type", r"Type\.GetType\s*\("),
    ("activator", r"Activator\.CreateInstance\s*\("),
    ("xml-serializer", r"XmlSerializer\s*\("),
    ("binary-serializer", r"BinaryFormatter\s*\("),
    ("expression-compile", r"\.Compile\s*\("),
    ("reflection-emit", r"Reflection\.Emit|DynamicMethod\s*\("),
    ("delegate-reflection", r"Delegate\.CreateDelegate\s*\(|GetMethod\s*\(|GetMethods\s*\(|GetFields\s*\("),
    ("native-import", r"\[(?:DllImport|LibraryImport)\s*\("),
    ("unmanaged-callback", r"UnmanagedCallersOnly|MonoPInvokeCallback"),
)
PLATFORM_PATTERNS: tuple[tuple[str, str, str], ...] = (
    ("steam", r"Steamworks|SteamAPI|SteamUser|SteamApps|SteamID", "must-be-absent"),
    ("windows", r"kernel32|user32|OSPlatform\.Windows|Equals\(\"Windows\"\)", "unreachable-on-target"),
    ("linux-bsd", r"OSPlatform\.Linux|Equals\(\"(?:Linux|FreeBSD|OpenBSD|NetBSD)\"\)|XDG_DATA_HOME", "unreachable-on-target"),
    ("macos-desktop", r"Mac OS X|OSPlatform\.OSX|Application Support", "unreachable-on-target"),
    ("process-shell", r"Process\.(?:Start|GetCurrentProcess)|ProcessStartInfo|UseShellExecute", "requires-focused-tvos-behaviour"),
    ("file-picker", r"OpenFileDialog|SaveFileDialog|FolderBrowser|NSOpenPanel", "unreachable-on-target"),
    ("desktop-window", r"SDL_SetWindow(?:Position|Size|Fullscreen|Bordered)|SetWindowPos", "requires-focused-tvos-behaviour"),
    ("rich-presence-store", r"Discord|RichPresence|StatsForStadia|GOG|EpicGames", "unreachable-on-target"),
    ("keyboard", r"MInput\.Keyboard|Keyboard\.GetState|\bKeys\.", "deferred-to-stage4"),
    ("mouse", r"MInput\.Mouse|Mouse\.GetState|\bMouse\.", "deferred-to-stage4"),
    ("opengl", r"OpenGL|GraphicsBackend\.OpenGL|\bGL_[A-Za-z0-9_]+", "renderer-selection-risk"),
    ("save-path", r"SDL_GetPrefPath|XDG_DATA_HOME|Application Support|AppDomain\.CurrentDomain\.BaseDirectory", "deferred-to-stage6"),
    ("virtual-controller", r"VirtualController|VirtualGamepad|GCControllerDirectionPad", "must-not-enable-on-tvos"),
)


def classify_site(kind: str, relative: str) -> tuple[str, str]:
    if kind == "xml-serializer" and relative == "Celeste/UserIO.cs":
        return "requires-runtime-rewrite", "Settings and SaveData require an AOT-safe serializer path before durable-save work."
    if kind == "xml-serializer" and relative == "Celeste/Commands.cs":
        return "unreachable-on-target", "Debug command-only Session export; Stage 3B must verify retail command disablement."
    if kind == "xml-serializer":
        return "deferred-until-stage3b-evidence", "Generic helper requires a concrete call-site/runtime test before preservation is chosen."
    if kind in {"assembly-get-types", "activator", "delegate-reflection"}:
        return "requires-focused-preservation", "Attribute/type discovery requires exact generated roots and Stage 3B behavioural validation."
    if kind in {"reflection-emit", "expression-compile"}:
        return "requires-runtime-rewrite", "Runtime code generation is not assumed to work under device AOT."
    if kind == "native-import":
        return "requires-native-resolution", "Native import must resolve through an accepted Stage 1 package or an explicit deferred boundary."
    return "deferred-until-stage3b-evidence", "Static analysis cannot prove the dynamic target."


def dynamic_sites(root: pathlib.Path, prefix: str = "") -> list[dict[str, Any]]:
    sites: list[dict[str, Any]] = []
    for path in sorted(root.rglob("*.cs"), key=lambda p: p.relative_to(root).as_posix()):
        relative = prefix + path.relative_to(root).as_posix()
        for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            for kind, pattern in DYNAMIC_PATTERNS:
                if re.search(pattern, line):
                    classification, rationale = classify_site(kind, relative)
                    sites.append({
                        "classification": classification,
                        "file": relative,
                        "kind": kind,
                        "line": line_number,
                        "rationale": rationale,
                    })
    return sites


def cmd_inventory(args: argparse.Namespace) -> None:
    root = pathlib.Path(args.root).resolve()
    sites = dynamic_sites(root)
    if args.fna_root:
        sites.extend(dynamic_sites(pathlib.Path(args.fna_root).resolve(), "FNA/"))
    counts = Counter(item["kind"] for item in sites)
    classifications = Counter(item["classification"] for item in sites)
    write_json(pathlib.Path(args.output_dir) / "reflection-inventory.json", {
        "schemaVersion": 1,
        "countsByKind": dict(sorted(counts.items())),
        "countsByClassification": dict(sorted(classifications.items())),
        "sites": sites,
    })

    serializer_roots = [
        {
            "type": "Celeste.Settings",
            "phase": "before-first-frame",
            "status": "requires-runtime-rewrite",
            "stage3BTest": "round-trip default settings in an ignored temporary directory under device AOT",
        },
        {
            "type": "Celeste.SaveData",
            "phase": "save-system",
            "status": "requires-runtime-rewrite",
            "stage3BTest": "round-trip representative save data under device AOT before Stage 6 storage integration",
        },
        {
            "type": "Celeste.Session",
            "phase": "debug-command-only",
            "status": "unreachable-on-target",
            "stage3BTest": "prove debug command console is disabled in the tvOS runtime lane",
        },
    ]
    write_json(pathlib.Path(args.output_dir) / "serializer-inventory.json", {
        "schemaVersion": 1,
        "blanketPreservation": False,
        "concreteRoots": serializer_roots,
        "note": "XmlSerializer emits IL2026/IL3050 under full AOT; Stage 3A inventories roots but does not claim runtime serializer support.",
    })

    expectations = load_json(pathlib.Path(args.tvstubs))
    symbols = expectations["components"]["tvStubs"]["symbols"]
    generated_text = {
        path.relative_to(root).as_posix(): path.read_text(encoding="utf-8")
        for path in root.rglob("*.cs")
    }
    fna_root = pathlib.Path(args.fna_root).resolve() if args.fna_root else None
    fna_text = ({path.relative_to(fna_root).as_posix(): path.read_text(encoding="utf-8")
                 for path in fna_root.rglob("*.cs")} if fna_root else {})
    binding_root = pathlib.Path(args.stage2_managed_root).resolve() if args.stage2_managed_root else None
    binding_text = ({path.relative_to(binding_root).as_posix(): path.read_text(encoding="utf-8")
                     for path in binding_root.rglob("*.cs")} if binding_root else {})
    generated_hits = []
    fna_hits = []
    binding_hits = []
    for symbol in symbols:
        token = re.compile(rf"\b{re.escape(symbol)}\b")
        for relative, text in generated_text.items():
            for line_number, line in enumerate(text.splitlines(), 1):
                if token.search(line):
                    generated_hits.append({"file": relative, "line": line_number, "symbol": symbol})
        for relative, text in fna_text.items():
            for line_number, line in enumerate(text.splitlines(), 1):
                if token.search(line):
                    fna_hits.append({"file": f"FNA/{relative}", "line": line_number, "symbol": symbol})
        for relative, text in binding_text.items():
            for line_number, line in enumerate(text.splitlines(), 1):
                if token.search(line):
                    binding_hits.append({"file": f"Stage2Managed/{relative}", "line": line_number, "symbol": symbol})
    write_json(pathlib.Path(args.output_dir) / "tvstubs-reachability.json", {
        "schemaVersion": 1,
        "expectedStubExportCount": len(symbols),
        "generatedCelesteCallSites": generated_hits,
        "pinnedFnaOccurrences": fna_hits,
        "stage2BindingOccurrences": binding_hits,
        "classification": {
            "pinnedFnaOccurrences": "WEB-only calls/declarations excluded by the tvOS compile-time symbols",
            "stage2BindingOccurrences": "cross-platform SDL binding declarations/wrappers; no generated Celeste or normal tvOS host call site",
            "unmentionedExports": "native link-closure stubs with no managed occurrence in the pinned Celeste/FNA graph",
        },
        "result": "no-intended-tvos-call-path" if not generated_hits else "generated-call-sites-found",
    })
    if generated_hits:
        raise SystemExit("error: generated Celeste source directly references a tvStubs export")

    imports = [site for site in sites if site["kind"] == "native-import"]
    write_json(pathlib.Path(args.output_dir) / "native-import-inventory.json", {
        "schemaVersion": 1,
        "remainingGeneratedCelesteImports": [site for site in imports if not site["file"].startswith("FNA/")],
        "pinnedFnaImports": [site for site in imports if site["file"].startswith("FNA/")],
        "fmodBoundaryManifest": "$ARTIFACT_ROOT/fmod-compile-boundary.json",
        "stage1NativeImports": ["SDL2", "FNA3D", "FAudio", "Theorafile", "MoltenVK/Vulkan loader"],
    })

    platform_sites: list[dict[str, Any]] = []
    for path in sorted(root.rglob("*.cs"), key=lambda p: p.relative_to(root).as_posix()):
        relative = path.relative_to(root).as_posix()
        for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            for kind, pattern, classification in PLATFORM_PATTERNS:
                if re.search(pattern, line):
                    platform_sites.append({
                        "classification": classification,
                        "file": relative,
                        "kind": kind,
                        "line": line_number,
                    })
    platform_counts = Counter(site["kind"] for site in platform_sites)
    if platform_counts.get("steam", 0):
        raise SystemExit("error: supported input unexpectedly contains Steam API call sites")
    if platform_counts.get("virtual-controller", 0):
        raise SystemExit("error: generated Celeste unexpectedly contains a virtual-controller patch")
    write_json(pathlib.Path(args.output_dir) / "platform-api-inventory.json", {
        "schemaVersion": 1,
        "countsByKind": dict(sorted(platform_counts.items())),
        "steamCallSiteCount": 0,
        "virtualControllerCallSiteCount": 0,
        "sites": platform_sites,
    })
    print(f"dynamic inventory: {len(sites)} sites; no generated Celeste tvStubs call sites")


def cmd_content_readers(args: argparse.Namespace) -> None:
    root = pathlib.Path(args.content_root).resolve()
    xnb_files = sorted(root.rglob("*.xnb"), key=lambda p: p.relative_to(root).as_posix())
    readers: set[str] = set()
    token = re.compile(rb"[ -~]{4,}")
    reader = re.compile(r"(?:Microsoft\.Xna\.Framework\.Content\.)?[A-Za-z0-9_.+]+Reader(?:`\d+)?")
    for path in xnb_files:
        for match in token.finditer(path.read_bytes()):
            text = match.group().decode("ascii", errors="ignore")
            readers.update(reader.findall(text))
    write_json(pathlib.Path(args.output), {
        "schemaVersion": 1,
        "xnbFileCount": len(xnb_files),
        "readerTypes": sorted(readers),
        "classification": "requires-focused-preservation",
        "stage3BTest": "load representative XNB assets for every reader type under device AOT before first-frame acceptance",
        "contentCopied": False,
    })
    print(f"content reader inventory: {len(xnb_files)} XNB files, {len(readers)} reader types")


DIAGNOSTIC = re.compile(
    r"(?P<file>[^:\n]+\.cs)(?:\((?P<line>\d+),(?P<column>\d+)\)|:(?P<line2>\d+)(?::\d+)?)?"
    r"[^\n]*?\b(?P<severity>warning|error)\s+(?P<code>[A-Z]{2,}\d+):\s*(?P<message>.+?)(?:\s+\[[^\]]+\])?$"
)
GLOBAL_DIAGNOSTIC = re.compile(
    r"^(?P<file>[A-Za-z0-9_.-]+)\s*:\s*(?:Trim analysis\s+)?"
    r"(?P<severity>warning|error)\s+(?P<code>[A-Z]{2,}\d+):\s*(?P<message>.+?)(?:\s+\[[^\]]+\])?$"
)


def cmd_diagnostics(args: argparse.Namespace) -> None:
    root = pathlib.Path(args.generated_root).resolve()
    repo = pathlib.Path(args.repo_root).resolve()
    records: dict[tuple[str, int, str, str], dict[str, Any]] = {}
    for log_name in args.log:
        for line in pathlib.Path(log_name).read_text(encoding="utf-8", errors="replace").splitlines():
            normalized = line.replace(str(root), "$GENERATED_ROOT").replace(str(repo), "$REPO_ROOT")
            normalized = re.sub(r"/_/\.build/celeste-managed/[^/]+/patched", "$GENERATED_ROOT", normalized)
            match = DIAGNOSTIC.search(normalized)
            if not match:
                match = GLOBAL_DIAGNOSTIC.search(normalized)
            if not match:
                continue
            file_name = match.group("file")
            file_name = file_name.replace("$GENERATED_ROOT/", "").replace("$REPO_ROOT/", "")
            line_number = int(match.groupdict().get("line") or match.groupdict().get("line2") or 0)
            message = match.group("message")
            key = (file_name, line_number, match.group("code"), message)
            records[key] = {
                "code": match.group("code"),
                "file": file_name,
                "line": line_number,
                "message": message,
                "severity": match.group("severity"),
            }
    values = sorted(records.values(), key=lambda item: (item["severity"], item["code"], item["file"], item["line"], item["message"]))
    counts = Counter(item["code"] for item in values)
    write_json(pathlib.Path(args.output), {
        "schemaVersion": 1,
        "countsByCode": dict(sorted(counts.items())),
        "errorCount": sum(1 for item in values if item["severity"] == "error"),
        "uniqueDiagnosticCount": len(values),
        "warningCount": sum(1 for item in values if item["severity"] == "warning"),
        "diagnostics": values,
    })
    print(f"diagnostics: captured {len(values)} unique records")


def cmd_check_diagnostics(args: argparse.Namespace) -> None:
    diagnostics = load_json(pathlib.Path(args.diagnostics))
    policy = load_json(pathlib.Path(args.policy))
    if diagnostics["errorCount"]:
        raise SystemExit("error: diagnostic baseline contains compiler/linker errors")
    coverage = Counter()
    unexplained = []
    for item in diagnostics["diagnostics"]:
        matches = []
        for rule in policy["rules"]:
            if not re.fullmatch(rule["codeRegex"], item["code"]):
                continue
            if not re.search(rule["fileRegex"], item["file"]):
                continue
            if not re.search(rule.get("messageRegex", ".*"), item["message"]):
                continue
            matches.append(rule["id"])
        if not matches:
            unexplained.append(item)
        else:
            for rule_id in matches:
                coverage[rule_id] += 1
    result = {
        "schemaVersion": 1,
        "diagnosticCount": len(diagnostics["diagnostics"]),
        "explainedCount": len(diagnostics["diagnostics"]) - len(unexplained),
        "unexplainedCount": len(unexplained),
        "coverageByRule": dict(sorted(coverage.items())),
        "unexplained": unexplained,
    }
    write_json(pathlib.Path(args.output), result)
    if unexplained:
        summary = ", ".join(sorted({f"{item['code']} {item['file']}" for item in unexplained}))
        raise SystemExit("error: diagnostic policy does not explain: " + summary)
    print(f"diagnostic policy explains all {result['diagnosticCount']} unique records")


def cmd_compare(args: argparse.Namespace) -> None:
    left = pathlib.Path(args.left)
    right = pathlib.Path(args.right)
    names = args.manifest
    differences = []
    checks: dict[str, str] = {}
    for name in names:
        a = load_json(left / name)
        b = load_json(right / name)
        if a != b:
            differences.append(name)
        checks[name] = "equivalent" if a == b else "different"
    result = {
        "schemaVersion": 1,
        "result": "equivalent" if not differences else "different",
        "manifests": checks,
    }
    write_json(pathlib.Path(args.output), result)
    if differences:
        raise SystemExit("error: clean generation differs: " + ", ".join(differences))
    print(f"reproducibility: {len(names)} normalized manifests are equivalent")


def cmd_verify_app(args: argparse.Namespace) -> None:
    app = pathlib.Path(args.app).resolve()
    info_path = app / "Info.plist"
    if not info_path.is_file():
        raise SystemExit("error: closure app Info.plist is missing")
    with info_path.open("rb") as stream:
        info = plistlib.load(stream)
    executable_name = info.get("CFBundleExecutable")
    executable = app / str(executable_name)
    if not executable.is_file():
        raise SystemExit("error: closure executable is missing")
    file_output = run_text(["file", "-b", str(executable)])
    if "Mach-O 64-bit executable arm64" not in file_output:
        raise SystemExit("error: closure executable is not arm64 Mach-O")
    build = run_text(["xcrun", "vtool", "-show-build", str(executable)])
    for pattern, label in ((r"platform\s+TVOS$", "tvOS platform"),
                           (r"minos\s+16\.0$", "tvOS 16.0 minimum"),
                           (r"sdk\s+26\.5$", "tvOS 26.5 SDK")):
        if not re.search(pattern, build, re.MULTILINE):
            raise SystemExit(f"error: closure executable lacks {label}")
    macho_files = []
    desktop_native = []
    for path in (p for p in app.rglob("*") if p.is_file()):
        kind = run_text(["file", "-b", str(path)]).strip()
        relative = path.relative_to(app).as_posix()
        if "Mach-O" in kind:
            macho_files.append(relative)
            member_build = run_text(["xcrun", "vtool", "-show-build", str(path)])
            if not re.search(r"platform\s+TVOS$", member_build, re.MULTILINE):
                raise SystemExit(f"error: non-tvOS Mach-O in closure: {relative}")
        if "ELF " in kind or path.suffix.lower() in {".so", ".dylib", ".a"}:
            desktop_native.append(relative)
    if macho_files != [str(executable.relative_to(app))]:
        raise SystemExit(f"error: unexpected Mach-O set in closure: {macho_files}")
    if desktop_native:
        raise SystemExit("error: desktop/loose native binaries in closure: " + ", ".join(desktop_native))
    forbidden_names = re.compile(r"(?i)(fmod|Celeste\.exe|Content[/\\]|\.celeste$|Steamworks|Everest)")
    forbidden = [p.relative_to(app).as_posix() for p in app.rglob("*") if forbidden_names.search(p.relative_to(app).as_posix())]
    if forbidden:
        raise SystemExit("error: forbidden proprietary/runtime material in closure app: " + ", ".join(forbidden))
    if (app / "embedded.mobileprovision").exists() or (app / "_CodeSignature").exists():
        raise SystemExit("error: unsigned closure unexpectedly contains signing/provisioning material")
    modern_fna = app / "FNA.dll"
    if not modern_fna.is_file() or sha256_file(modern_fna) == "00349b572636c0ed4c97d4e4f74344b3450160c42d4a6b52bcf5e0f2d5193a31":
        raise SystemExit("error: closure lacks the project-built FNA adapter or contains the game-supplied legacy FNA binary")
    write_json(pathlib.Path(args.output), {
        "schemaVersion": 1,
        "architecture": "arm64",
        "platform": "TVOS",
        "minimumOS": "16.0",
        "sdk": "26.5",
        "signed": False,
        "launched": False,
        "fmodNativeBinaryCount": 0,
        "celesteContentFileCount": 0,
        "legacyGameFnaBinaryCount": 0,
        "machOBinaries": macho_files,
        "managedPeAssembliesAreAotInputs": True,
        "result": "full-aot-link-closure-verified",
    })
    print("verified unsigned tvOS arm64 full-AOT closure bundle")


def parser() -> argparse.ArgumentParser:
    value = argparse.ArgumentParser(description=__doc__)
    sub = value.add_subparsers(dest="command", required=True)

    validate = sub.add_parser("validate")
    validate.add_argument("--game-root", required=True)
    validate.add_argument("--profiles", required=True)
    validate.add_argument("--output", required=True)
    validate.set_defaults(func=cmd_validate)

    normalize = sub.add_parser("normalize-input")
    normalize.add_argument("--root", required=True)
    normalize.add_argument("--input-manifest", required=True)
    normalize.add_argument("--profiles", required=True)
    normalize.add_argument("--repo-root", required=True)
    normalize.add_argument("--output", required=True)
    normalize.set_defaults(func=cmd_normalize)

    tree = sub.add_parser("tree-manifest")
    tree.add_argument("--root", required=True)
    tree.add_argument("--kind", required=True)
    tree.add_argument("--placeholder", required=True)
    tree.add_argument("--output", required=True)
    tree.set_defaults(func=cmd_tree_manifest)

    boundary = sub.add_parser("patch-fmod")
    boundary.add_argument("--root", required=True)
    boundary.add_argument("--expected-count", required=True, type=int)
    boundary.add_argument("--output", required=True)
    boundary.set_defaults(func=cmd_patch_fmod)

    inventory = sub.add_parser("inventory")
    inventory.add_argument("--root", required=True)
    inventory.add_argument("--output-dir", required=True)
    inventory.add_argument("--tvstubs", required=True)
    inventory.add_argument("--fna-root")
    inventory.add_argument("--stage2-managed-root")
    inventory.set_defaults(func=cmd_inventory)

    readers = sub.add_parser("content-readers")
    readers.add_argument("--content-root", required=True)
    readers.add_argument("--output", required=True)
    readers.set_defaults(func=cmd_content_readers)

    diagnostics = sub.add_parser("diagnostics")
    diagnostics.add_argument("--generated-root", required=True)
    diagnostics.add_argument("--repo-root", required=True)
    diagnostics.add_argument("--log", required=True, action="append")
    diagnostics.add_argument("--output", required=True)
    diagnostics.set_defaults(func=cmd_diagnostics)

    check = sub.add_parser("check-diagnostics")
    check.add_argument("--diagnostics", required=True)
    check.add_argument("--policy", required=True)
    check.add_argument("--output", required=True)
    check.set_defaults(func=cmd_check_diagnostics)

    compare = sub.add_parser("compare")
    compare.add_argument("--left", required=True)
    compare.add_argument("--right", required=True)
    compare.add_argument("--manifest", required=True, action="append")
    compare.add_argument("--output", required=True)
    compare.set_defaults(func=cmd_compare)

    app = sub.add_parser("verify-app")
    app.add_argument("--app", required=True)
    app.add_argument("--output", required=True)
    app.set_defaults(func=cmd_verify_app)
    return value


def main() -> int:
    args = parser().parse_args()
    args.func(args)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
