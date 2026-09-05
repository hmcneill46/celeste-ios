#!/usr/bin/env python3
"""Reproduce the K-I pre-product YELLOW finding from exact public inputs."""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
START = "fe46b3c97cfc7dd74a907da8871ff9ba2c57d6e3"
KG = "e0d7c1988a9e5a896001735e5fe21d2251446bd0"


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value


def sha(path):
    with path.open("rb") as stream:
        digest = hashlib.sha256()
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
        return digest.hexdigest()


def save(path, value):
    value["artifactSha256"] = hashlib.sha256((json.dumps(value, sort_keys=True,
        separators=(",", ":"), ensure_ascii=False) + "\n").encode()).hexdigest()
    path.write_text(json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages-root", type=Path, required=True)
    parser.add_argument("--work-root", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    args = parser.parse_args()
    if args.work_root.exists():
        raise ValueError("use a fresh work root")
    args.work_root.mkdir(parents=True)
    args.output_root.mkdir(parents=True, exist_ok=True)
    content = module("ki_content", ROOT / "scripts/generate-apple-everest-stage25ki-content.py")
    kc = module("ki_kc_parser", ROOT / "scripts/generate-apple-everest-stage25kc.py")
    plan = content.generate(args.packages_root)
    historical = json.loads(subprocess.check_output(["git", "-C", str(ROOT), "show",
        KG + ":apple-everest/sj-beginner-content-stage25kg.json"], text=True))
    if plan["packages"] != historical["packages"]:
        raise ValueError("content plan differs from immutable K-G")
    plan_path = args.output_root / "sj-beginner-content-stage25ki.json"
    plan_path.write_text(json.dumps(plan, indent=2, ensure_ascii=False) + "\n")

    graph_path = ROOT / "apple-everest/selected-factory-type-closure-stage25kh.json"
    graph = json.loads(graph_path.read_text())
    selected = {(item["kind"], item["customId"]): item for item in graph["factories"]}
    pins = {row["name"]: row for row in json.loads((ROOT /
        "apple-everest/strawberry-jam-dependency-graph-stage25kc.json").read_text())["nodes"]}
    runner = [str(ROOT / ".build/apple-everest/toolchain/dotnet8/dotnet"),
              str(ROOT / "tools/AppleEverestBuilder/bin/Release/net8.0/AppleEverestBuilder.dll")]
    result_path = args.work_root / "factory-preflight.json"
    command = [*runner, "preflight-factory-closure", "--manifest", str(graph_path),
               "--output", str(result_path.resolve())]
    for name in sorted({row["provider"] for row in selected.values()} - {"EverestCore"}):
        path = args.packages_root / (name + ".zip")
        if sha(path) != pins[name]["zipSha256"] or path.stat().st_size != pins[name]["zipBytes"]:
            raise ValueError("selected provider pin differs: " + name)
        command += ["--mod", str(path.resolve())]
    result = subprocess.run(command, capture_output=True, text=True)
    (args.work_root / "factory-preflight.log").write_text(result.stdout + result.stderr)
    preflight = json.loads(result_path.read_text())
    if result.returncode != 1 or preflight["registrationCensus"] != {
            "selected": 73, "available": 30, "unavailable": 43}:
        raise ValueError("readiness changed; reclassify this diagnostic stage")
    missing = {(row["kind"], row["customId"]) for row in preflight["factories"]
               if row["status"] != "AVAILABLE_ACCEPTED_REGISTRATION"}

    maps = {}
    counts = Counter()
    per_map = {}
    with zipfile.ZipFile(args.packages_root / "StrawberryJam2021.zip") as archive:
        dll_pin = pins["StrawberryJam2021"]["distributedDlls"]
        root_dll = dict(next(row for row in dll_pin if Path(row["path"]).name == "StrawberryJam2021.dll"))
        dll_paths = [path for path in archive.namelist() if Path(path).name == "StrawberryJam2021.dll"]
        if len(dll_paths) != 1:
            raise ValueError("ambiguous root DLL archive path")
        root_dll["path"] = dll_paths[0]
        if hashlib.sha256(archive.read(root_dll["path"])).hexdigest() != root_dll["sha256"]:
            raise ValueError("root DLL pin differs")
        for label, path in zip(("lobby", "bing"), content.MAPS):
            local = args.work_root / (label + ".bin")
            original = archive.read(path)
            local.write_bytes(original)
            parsed = kc.parse_map(local)
            boundary_path = args.work_root / (label + "-boundary.json")
            elements_path = args.work_root / (label + "-elements.json")
            for verb, output in (("inspect-map-boundary", boundary_path), ("inspect-map", elements_path)):
                subprocess.run([*runner, verb, "--map", str(local.resolve()), "--output", str(output.resolve())],
                               check=True, stdout=subprocess.DEVNULL)
            boundary = json.loads(boundary_path.read_text())
            if boundary["AppendixBytes"] != parsed["trailingBytes"]:
                raise ValueError("independent map boundary readers disagree")
            census = Counter((row.kind, row.id) for row in parsed["occurrences"] if (row.kind, row.id) in selected)
            counts.update(census)
            per_map[label] = census
            details = json.loads(elements_path.read_text())["elements"]
            players = [row for row in details if row["Kind"] == "entity" and row["Id"] == "player"]
            maps[label] = {"path": path, "sid": path[5:-4], "sha256": sha(local),
                "bytes": len(original), "binaryPackerRootBytes": boundary["ConsumedRootBytes"],
                "appendixBytes": boundary["AppendixBytes"], "appendixSha256": boundary["AppendixSha256"],
                "sourcePackage": parsed["package"], "rooms": [room.removeprefix("lvl_") for room in parsed["rooms"]],
                "authoredPlayerSpawnCount": len(players), "physicalSpawn": None,
                "compatibilityId": None, "packagedInProduct": False,
                "audioEventsInSource": parsed["audioEvents"]}
            if label == "bing":
                expected = {"room": "00- intro", "x": 264, "y": 152}
                if not any(row["Room"] == expected["room"] and row["X"] == 264 and row["Y"] == 152 for row in players):
                    raise ValueError("Bing initial authored marker differs")
                maps[label]["expectedInitialSpawn"] = expected
    if set(counts) != set(selected) or sum(counts.values()) != 920:
        raise ValueError("fresh two-map census differs from the selected graph")
    blocked_occurrences = sum(count for key, count in counts.items() if key in missing)
    if blocked_occurrences != 409:
        raise ValueError("blocked occurrence census differs")
    factory_evidence = {"schemaVersion": 1, "stage": "25K-I", "status": "FAIL_CLOSED_BEFORE_PRODUCT",
        "historicalClaim": preflight["claimedGraphCensus"], "historicalGraphSha256": sha(graph_path),
        "registrationCensus": preflight["registrationCensus"],
        "fullyClosedFactoryCount": None,
        "closureDisposition": "NOT_ESTABLISHED_REGISTRATION_NECESSARY_CONDITION_FAILED",
        "availableRegistrationIsNotNewSemanticClosureProof": True,
        "providers": preflight["providers"], "factories": [{**row,
            "occurrences": counts[(row["kind"], row["customId"])],
            "occurrencesByMap": {label: census[(row["kind"], row["customId"])] for label, census in per_map.items()}}
            for row in preflight["factories"]],
        "noAppleCompilationAttempted": True}
    save(args.output_root / "sj-beginner-factory-closure-stage25ki.json", factory_evidence)
    save(args.output_root / "sj-beginner-physical-stage25ki.json", {
        "schemaVersion": 1, "stage": "25K-I", "status": "NOT_RUN_PREFLIGHT_BLOCKED",
        "iosIpaSha256": None, "tvosIpaSha256": None,
        "iphone": "NOT_RUN_NO_K_I_PRODUCT", "appleTv": "NOT_RUN_NO_K_I_PRODUCT",
        "ipad": "IPADOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE",
        "hardwareAvailabilityRechecked": False,
        "lastAllThreePhysicalGreen": "Stage 25K-B build 35",
        "lobby": "NOT_RUN", "tutorial": "NOT_RUN", "npc": "NOT_RUN",
        "chapterPanel": "NOT_RUN", "bingRoomsTraversed": [], "saveQuit": "NOT_RUN",
        "coldResume": "NOT_RUN", "returnToLobby": "NOT_RUN", "reentry": "NOT_RUN",
        "journal": "NOT_RUN", "completion": "NOT_RUN", "strawberryWithReturn": "NOT_RUN",
        "priorDeviceProgressionTouched": False,
        "priorKhEntityPhysicalAcceptanceRetainedAsHistoricalOnly": True})
    save(args.output_root / "sj-beginner-slice-stage25ki.json", {
        "schemaVersion": 1, "stage": "25K-I", "status": "YELLOW_READINESS_NOT_ESTABLISHED",
        "startSha": START, "branch": "feature/apple-everest-first-sj-slice-retry",
        "historicalKgSha": KG, "kgMerged": False,
        "stopRule": "Brief sections 6 and 52: changed readiness or unresolved selected closure requires STOP before product construction.",
        "classification": "MISSING_ACCEPTED_SELECTED_IMPLEMENTATIONS_AND_OVERSTATED_K_H_READINESS",
        "newRuntimeMechanismCount": None,
        "strawberryJam": {"version": "1.0.12", "zipSha256": pins["StrawberryJam2021"]["zipSha256"],
                          "dllPath": root_dll["path"], "dllSha256": root_dll["sha256"]},
        "maps": maps,
        "contentPlan": {"packages": 14, "files": 1212, "sjRootFiles": 1178, "sjAssetsFiles": 24,
            "audioBankFiles": 4, "helperAssetFiles": 7, "sjMapBins": 2, "excludedSjMapBins": 126,
            "totalSjMapBinsInSource": 128, "matchesKg": True, "sha256": sha(plan_path),
            "widerNonMapAssetsAdded": False, "shippedSjMapBins": 0},
        "historicalContentIdClaim": {"selected": 920, "acceptedOrVanilla": 920, "blocked": 0, "unclassified": 0},
        "freshContentIdRegistrationCensus": {"selected": 920, "acceptedRegistration": 511,
            "blockedRegistration": 409, "unclassifiedIds": 0},
        "factoryRegistrationCensus": preflight["registrationCensus"],
        "fullyClosedFactoryCount": None, "bothReadinessGatesPassed": False,
        "product": {"ios": "NOT_BUILT_PREFLIGHT_BLOCKED", "tvos": "NOT_BUILT_PREFLIGHT_BLOCKED",
            "sharedClosureSha256": None, "threeClosureDeterminism": "NOT_RUN_PREFLIGHT_BLOCKED",
            "signing": "NOT_RUN", "installation": "NOT_RUN", "versionIncremented": False},
        "aevpsv1Version": 1, "runtimeSourceChanged": False,
        "deviceAudioTextureOrSaveClaims": "NO_K_I_PRODUCT_OR_PHYSICAL_EVIDENCE",
        "claims": {"originalMapsModified": False, "developmentIntegrationReady": False,
            "allPlatformReleaseReady": False, "firstPhysicallyRunningUnchangedSjSlice": False,
            "fullStrawberryJamSupported": False, "recommendedFastForwardSha": None},
        "nextWork": "Close and substantiate the exact 43 missing selected factories, then rerun both gates on actual pinned production inputs before retrying this same unchanged two-map slice. K-J expansion remains premature."})
    print("REPRODUCED YELLOW: 73 selected factories, 30 available registrations, 43 unavailable; 409/920 affected occurrences")
    print("Content plan matches K-G; original map hashes and appendices verified; no product generated")


if __name__ == "__main__":
    main()
