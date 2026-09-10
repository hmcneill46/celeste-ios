#!/usr/bin/env python3
"""Run fresh K-N source, compiled semantics and composition proofs before AOT.

Registration evidence is an input, never a substitute for the other gates.
This command creates every execution receipt itself in a new owned directory.
It cannot consume a previous probe PASS or claim physical gameplay acceptance.
"""
from __future__ import annotations
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
MARKER = "READY_FOR_STAGE25KN_SNAS_PRODUCT_BUILD"
SID = "StrawberryJam2021/1-Beginner/snas"
PROFILE_SHA = "1906bcec05cfb5ae5c16c471368716dd275aaf747f6b8e24eca8abd4741c6bfd"
SEMANTIC_BASELINE = "0097f41ed546502765849d86f951925596b8ba0d61671a98ddd446cbe0f6f652"
CONTRACT_SHA = "9552770401ef4d5d465c67dbbab5958f9eec07524b67d482fd5696ab751eeda2"
EXTENSIONS = {
    "CollabUtils2/MiniHeart", "MaxHelpingHand/CameraOffsetBorder",
    "MaxHelpingHand/FlagToggleSmoothCameraOffsetTrigger", "MaxHelpingHand/SetFlagOnSpawnController",
    "MaxHelpingHand/SidewaysJumpThru", "everest/flagTrigger", "everest/smoothCameraOffsetTrigger",
}
NEW = {
    "CommunalHelper/PlayerBubbleRegion", "ContortHelper/RandomSoundTrigger",
    "MaxHelpingHand/FlagTouchSwitch", "MaxHelpingHand/FlagSwitchGate",
}
GENERATION_SOURCES = {
    "tools/AppleEverestBuilder/" + name for name in (
        "ClosureGenerator.cs", "StaticSemanticLowering.cs", "StaticSemanticRuntimePatches.cs", "SelectedSidewaysIlPlans.cs",
        "SelectedFactoryProfiles.cs", "SnasFlagGroups.cs", "CollabManifestGenerator.cs", "CustomAudioManifest.cs")
} | {"tools/AppleEverestIlWorker/SelectedSidewaysIlLowering.cs",
     "apple-everest/runtime/semantics/AppleEverestSelectedProfileGuard.cs",
     "apple-everest/runtime/semantics/AppleEverestSnasProfileGuard.cs",
     "apple-everest/runtime/AppleEverestCustomAudioRuntime.cs",
     "apple-everest/runtime/AppleEverestCustomAudioLifecycle.cs"}


def check(value, reason):
    if not value:
        raise ValueError(reason)


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode()


def logical(value):
    return hashlib.sha256(canonical(value)).hexdigest()


def read(path):
    return json.loads(path.read_text())


def production_source_inventory(root=ROOT):
    paths = []
    for tree in ("tools/AppleEverestBuilder", "tools/AppleEverestIlWorker", "apple-everest/runtime", "apple-everest/profiles"):
        for path in (root / tree).rglob("*"):
            if not path.is_file() or set(path.relative_to(root / tree).parts) & {"bin", "obj", "tests"}: continue
            if path.suffix in {".cs", ".csproj", ".json", ".props", ".targets"}: paths.append(path)
    paths += list((root / "apple-everest").glob("*.json")) + [root / "modern-ios/IOSPortVersion.props"]
    return [{"Path": path.relative_to(root).as_posix(), "Bytes": path.stat().st_size, "Sha256": sha(path)}
            for path in sorted(paths, key=lambda path: path.relative_to(root).as_posix())]


def verify_compilation_source_binding(compiled, root=ROOT):
    check(compiled["productionSourceBindings"] == production_source_inventory(root),
          "compilation receipt lacks or differs from current owned source/authority bindings")


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / filename)
    value = importlib.util.module_from_spec(spec);sys.modules[name] = value;spec.loader.exec_module(value)
    return value


def verify_obligations(document, occurrences, root=ROOT):
    """No default disposition, missing row or unknown profile can become ready."""
    check(document["stage"] == "25K-N" and document["schemaVersion"] == 1 and
          document["authority"] == "REVIEWED_SOURCE_AND_REQUIRED_FRESH_EXECUTION; NOT_A_PASS_RECEIPT", "unsupported semantic authority")
    check(document["openRequirements"] == [], "unresolved semantic or composition requirement")
    check(document["issueLedger"] == "apple-everest/sj-snas-issues-stage25kn.json", "missing implementation issue ledger")
    issue_path = root / document["issueLedger"]
    check(sha(issue_path) == "e952fec264697982e33e4302484f0335e76c1e321fccdcdb873ba938d92887a6", "unreviewed implementation issue ledger")
    issue_ledger = read(issue_path)
    check(issue_ledger["openRequirements"] == [] and {row["id"] for row in issue_ledger["issues"]} ==
          {"KN" + str(i).zfill(2) for i in range(1, 10)} and all(row["resolution"] == "IMPLEMENTED_SOURCE_REVIEWED"
          for row in issue_ledger["issues"]), "unresolved implementation issue")
    check(document["census"] == {"customOccurrences": 1309, "factories": 83, "rawAuthoredProfiles": 604,
          "selectedFactories": 77, "separateLegacyFactories": 6, "regressionOccurrences": 336}, "wrong semantic census")
    baseline_path = root / "apple-everest/sj-kj-accepted-semantic-stage25kl.json"
    check(sha(baseline_path) == document["baselineSemanticAuthoritySha256"] == SEMANTIC_BASELINE,
          "accepted semantic baseline differs")
    baseline = read(baseline_path)
    check(baseline["census"] == {"selected": 73, "fullyClosed": 73, "blocked": 0, "unknown": 0},
          "historical semantic authority is incomplete")
    old = {(row["kind"], row["customId"]): row for row in baseline["factories"]}
    groups = {}
    for occurrence in occurrences:
        key = occurrence["kind"], occurrence["customId"]
        groups.setdefault(key, []).append(occurrence)
    rows = document["factories"]
    check(len(rows) == len(groups) == 83 and len(occurrences) == 1309, "omitted or duplicate factory obligation")
    check(len({(row["kind"], row["customId"]) for row in rows}) == len(rows), "duplicate factory obligation")
    check({(row["kind"], row["customId"]) for row in rows} == set(groups), "factory obligation set differs")
    check(document["historicalPhysicalResultsTransferred"] is False, "historical physical results cannot transfer")
    scope_counts = Counter()
    for row in rows:
        key = row["kind"], row["customId"];authored = groups[key];cid = key[1]
        check({item["provider"] for item in authored} == {row["provider"]}, "wrong authored provider")
        check(row["occurrences"] == len(authored) and row["authoredProfileSha256"] ==
              sorted({item["profileSha256"] for item in authored}), "unproved authored profile or occurrence")
        if cid in NEW:
            scope = "NEW_FINITE_IMPLEMENTATION"
            required = {"source-bindings", "compiled-runtime"}
            if cid in {"CommunalHelper/PlayerBubbleRegion", "ContortHelper/RandomSoundTrigger"}:
                required.add("random-bubble")
        elif key in old:
            scope = "EXACT_PROFILE_EXTENSION" if cid in EXTENSIONS else "UNCHANGED_ACCEPTED_AUTHORED_PROFILES"
            required = {"baseline-source"}
            if cid in EXTENSIONS: required |= {"source-bindings", "compiled-runtime"}
            if cid == "CollabUtils2/SilverBerry": required.add("compiled-runtime")
            expected_bindings = []
            for binding in old[key]["implementation"]["sourceBindings"]:
                for source in binding.get("sourceFiles", []):
                    current = sha(root / source["path"])
                    if current != source["sha256"]:
                        check(cid in {"CollabUtils2/ChapterPanelTrigger", "CollabUtils2/JournalTrigger"} and
                              source["path"] == "apple-everest/runtime/AppleEverestCollabRuntime.cs" and
                              current == "d1b440b2d05ece6515e081e223358f73acf63e7268201aca20001382f4ffe0ba",
                              "unreviewed change to accepted semantic implementation")
                    expected_bindings.append({"path": source["path"], "sha256": current})
            check(row["sourceBindings"] == expected_bindings, "accepted source binding omitted or substituted")
        else:
            scope = "SEPARATE_UNCHANGED_LEGACY_CONTROL";required = {"legacy-reference"}
        required |= {"registration", "regressions", "composition"}
        check(row["scope"] == scope and set(row["requiredProofs"]) == required,
              "missing or unsupported semantic proof obligation")
        issues = {"CommunalHelper/PlayerBubbleRegion": ["KN01"], "ContortHelper/RandomSoundTrigger": ["KN02"],
                  "MaxHelpingHand/FlagTouchSwitch": ["KN03"], "MaxHelpingHand/FlagSwitchGate": ["KN03"],
                  **{name: ["KN04"] for name in EXTENSIONS}, "CollabUtils2/SilverBerry": ["KN05"]}
        check(row["issues"] == issues.get(cid, []), "unknown or unbound semantic issue")
        check(row["physicalAcceptance"] == "SEPARATE_EXACT_PRODUCT_MATRIX", "host proof claims physical acceptance")
        if cid in NEW:
            filenames = {
                "CommunalHelper/PlayerBubbleRegion": ["AppleEverestPlayerBubbleRegion.cs"],
                "ContortHelper/RandomSoundTrigger": ["AppleEverestRandomSoundTrigger.cs"],
                "MaxHelpingHand/FlagTouchSwitch": ["AppleEverestFlagTouchSwitch.cs", "AppleEverestFlagGroup.cs"],
                "MaxHelpingHand/FlagSwitchGate": ["AppleEverestFlagSwitchGate.cs", "AppleEverestFlagGroup.cs"],
            }[cid]
            expected_paths = {"apple-everest/runtime/semantics/" + name for name in filenames}
            check(len(row["sourceBindings"]) == len(expected_paths) and
                  {b["path"] for b in row["sourceBindings"]} == expected_paths,
                  "new implementation lacks exact owned source bindings")
        for binding in row["sourceBindings"]:
            check(sha(root / binding["path"]) == binding["sha256"], "reviewed implementation source changed")
        scope_counts[scope] += 1
    check(scope_counts == {"NEW_FINITE_IMPLEMENTATION": 4, "EXACT_PROFILE_EXTENSION": 7,
          "UNCHANGED_ACCEPTED_AUTHORED_PROFILES": 66, "SEPARATE_UNCHANGED_LEGACY_CONTROL": 6}, "semantic scope census differs")
    check(sum(len(row["authoredProfileSha256"]) for row in rows) == 604, "raw profile census differs")
    check(len(document["generationSourceBindings"]) == len(GENERATION_SOURCES) and
          {b["path"] for b in document["generationSourceBindings"]} == GENERATION_SOURCES,
          "generation/frozen-IL source obligation missing or substituted")
    for binding in document["generationSourceBindings"]:
        check(sha(root / binding["path"]) == binding["sha256"], "reviewed generation or frozen-IL source changed")
    return {"factoryScopes": dict(sorted(scope_counts.items())), "authoredProfiles": 604,
            "baselineSemanticSha256": SEMANTIC_BASELINE, "reviewedObligationsSha256": logical(document)}


def progression(closure):
    authority = ROOT / "apple-everest/sj-snas-progression-reference-stage25kn.json"
    check(sha(authority) == "7b9e69de150e3361ead58455d2b0c1da9c0f639bcb091a08ee688d3697074ce5", "progression authority differs")
    reference = read(authority);rows = []
    for line in (closure / "managed/GeneratedAppleEverestProgressionManifest.cs").read_text().splitlines():
        if "new AppleEverestMapProgressionDescriptor(" not in line: continue
        strings = re.findall(r'"((?:[^"\\]|\\.)*)"', line)
        sid = strings[1] if "/" in strings[1] else strings[0]
        rows.append({"sid": sid, "descriptorLineSha256": hashlib.sha256(line.encode()).hexdigest()})
    expected = reference["baselineDescriptors"] + [reference["expandedDescriptor"]]
    check(sorted(rows, key=lambda r: r["sid"]) == sorted(expected, key=lambda r: r["sid"]),
          "existing map compatibility descriptor changed or new map differs")
    codec = ROOT / "apple-everest/runtime/AppleEverestProgressionSnapshotCodec.cs"
    check(sha(codec) == "4d34c448ea425f2de76892f4127ab73bd9c4530451abcb875cb47f6bfb6cb6f1", "accepted persistence codec changed")
    return {"preservedDescriptors": 20, "additionalDescriptors": 1, "authoritySha256": sha(authority),
            "codecSha256": sha(codec), "existingEnvelope": "AEVPSV1; accepted writer envelope 2, reader 1/2; unchanged"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("closure", "runtime", "production-preflight", "content-plan", "authored-profiles", "package-root", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args();out = args.output.resolve();closure = args.closure.resolve()
    check(not out.exists(), "K-N preflight requires a fresh output root")
    for parent in (args.output.absolute(), *args.output.absolute().parents): check(not parent.is_symlink(), "symlink output ancestor")
    if out == ROOT or ROOT in out.parents:
        subprocess.run(["git", "check-ignore", "--no-index", "-q", "--", str((out / ".stage25kn-preflight").relative_to(ROOT))], cwd=ROOT, check=True)
    manifest = read(closure / "compatibility-manifest.json");compiled = read(args.production_preflight)
    verify_compilation_source_binding(compiled)
    assembly = args.runtime / "bin/Release/net10.0-ios26.5/Celeste.dll"
    check(sha(assembly) == compiled["compiledAssemblySha256"] and compiled["sharedClosureSha256"] == manifest["sharedClosureSha256"] and
          compiled["actualProductionRegenerated"] and compiled["exactPackageIdentityVerified"] and compiled["compilation"]["FreshProductionCompilation"],
          "fresh actual production compilation belongs to another input or closure")
    check(compiled["contentIdCensus"] == {"selectedOccurrences": 973, "acceptedOrVanilla": 973, "blocked": 0, "unclassified": 0} and
          compiled["registrationCensus"] == {"selected": 77, "available": 77, "unavailable": 0, "providerRejected": 0}, "selected registration gate incomplete")
    check(compiled["semanticClosureEstablished"] is False, "registration evidence incorrectly claims semantic execution")
    check(sha(args.authored_profiles) == compiled["authoredProfilesSha256"] == PROFILE_SHA, "wrong source profiles")
    contract = ROOT / "apple-everest/sj-snas-factory-contract-stage25kn.json"
    check(sha(contract) == CONTRACT_SHA, "unreviewed product contract")
    extractor = module("kn_fresh_profiles", "generate-apple-everest-stage25kn-profiles.py")
    profiles = extractor.extract(args.package_root / "StrawberryJam2021.zip")
    check(extractor.serialized(profiles) == args.authored_profiles.read_bytes(), "fresh package profile extraction differs")
    check(extractor.guard(profiles) == (ROOT / "apple-everest/runtime/semantics/AppleEverestSnasProfileGuard.cs").read_text(), "actual guard differs from original profile extraction")
    regressions = module("kn_fresh_regressions", "verify-apple-everest-snas-regressions.py")
    regression_profiles, _ = regressions.extract(closure)
    all_profiles = profiles["occurrences"] + regression_profiles["occurrences"]
    obligations_path = ROOT / "apple-everest/sj-snas-semantics-stage25kn.json"
    semantics = verify_obligations(read(obligations_path), all_profiles)
    progression_proof = progression(closure)
    out.mkdir(parents=True);(out / ".stage25kn-preflight").touch()
    env = dict(os.environ, MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_USE_MSBUILD_SERVER="0", UseSharedCompilation="false", PYTHONDONTWRITEBYTECODE="1")
    canonical = ROOT / ".build/celeste-ios/current"
    builder = ROOT / "tools/AppleEverestBuilder/bin/Debug/net8.0/AppleEverestBuilder.dll"
    check(builder.is_file(), "normal builder compilation missing")
    def run(label, command):
        print("K-N fresh proof: " + label, flush=True)
        with (out / (label + ".private.log")).open("xb") as log:
            result = subprocess.run(list(map(str, command)), cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
        check(result.returncode == 0, "K-N " + label + " failed; see retained private phase log")
    run("source-bindings", [sys.executable, ROOT / "scripts/verify-apple-everest-snas-source-bindings.py",
        "--package-root", args.package_root, "--builder", builder, "--work-root", out / "source-bindings"])
    run("random-bubble", [sys.executable, ROOT / "scripts/verify-apple-everest-snas-behavior.py",
        "--communal", args.package_root / "CommunalHelper.zip", "--contort", args.package_root / "ContortHelper.zip",
        "--canonical", canonical / "managed", "--references", canonical / "shared-stage3c/stage3b/stage3a-build/input", "--work-root", out / "random-bubble"])
    run("compiled-runtime", [sys.executable, ROOT / "scripts/verify-apple-everest-snas-compiled-runtime.py",
        "--assembly", assembly, "--closure", closure, "--canonical-content", canonical / "content/Content", "--work-root", out / "compiled-runtime"])
    run("audio-names", [sys.executable, ROOT / "scripts/verify-apple-everest-audio-names.py",
        "--runtime", args.runtime, "--work-root", out / "audio-names"])
    run("regressions", [sys.executable, ROOT / "scripts/verify-apple-everest-snas-regressions.py",
        "--assembly", assembly, "--closure", closure, "--production-preflight", args.production_preflight, "--work-root", out / "regressions"])
    run("composition", [sys.executable, ROOT / "scripts/verify-apple-everest-snas-composition.py",
        "--closure", closure, "--runtime", args.runtime, "--production-preflight", args.production_preflight,
        "--content-plan", args.content_plan, "--authored-profiles", args.authored_profiles,
        "--sj-package", args.package_root / "StrawberryJam2021.zip", "--output", out / "composition"])
    run("compiled-negative-controls", ["dotnet", "exec", "--fx-version", "10.0.10", builder,
        "verify-compiled-factory-controls", "--assembly", assembly, "--manifest", contract,
        "--authored-profiles", args.authored_profiles, "--output", out / "compiled-negative-controls.json"])
    records = {name: read(out / path) for name, path in {
        "source-bindings": "source-bindings/source-bound-result.json", "random-bubble": "random-bubble/source-bound-result.json",
        "compiled-runtime": "compiled-runtime/source-bound-result.json", "regressions": "regressions/source-bound-result.json",
        "composition": "composition/source-composition.json", "terrain": "composition/terrain-host/source-bound-result.json",
        "audio-names": "audio-names/source-bound-result.json",
        "compiled-negative-controls": "compiled-negative-controls.json"}.items()}
    runtime = records["compiled-runtime"];regression = records["regressions"];composition = records["composition"]
    check(runtime["originalAssemblyBytesUnchanged"] and runtime["result"]["status"] == "PASS" and runtime["result"]["checks"] >= 3842,
          "actual compiled constructor/lifecycle checks incomplete")
    check(runtime["productionDlls"]["Celeste.dll"] == sha(assembly) == compiled["compiledAssemblySha256"], "compiled evidence byte binding differs")
    check(regression["census"] == {"maps": 18, "occurrences": 336, "selectedGuardOccurrences": 330, "separateLegacyOccurrences": 6} and
          regression["unchangedLegacyImplementationsVerified"], "separate legacy or selected regression evidence incomplete")
    check(records["source-bindings"]["status"] == "PASS_SOURCE_BINDINGS" and len(records["source-bindings"]["sourceTypes"]) == 15,
          "source/type/lifecycle review bindings incomplete")
    check(records["random-bubble"]["result"]["status"] == "PASS" and records["random-bubble"]["result"]["assertions"] >= 4793,
          "pinned reference differential failed")
    check(records["terrain"]["status"] == "PASS" and records["terrain"]["checks"] >= 221309 and
          composition["status"] == "PASS_SOURCE_COMPOSITION_CHECKS", "complete real-map composition proof incomplete")
    controls = records["compiled-negative-controls"]
    check(controls["positiveFactories"] == 77 and controls["positiveOccurrences"] == 973 and
          len(controls["omissions"]) == 10 and len(controls["rejectedCompiledControls"]) >= 5 and
          len(controls["rejectedImplementationControls"]) >= 4 and
          controls["rejectedLegacyControls"] == ["CHANGED_LEGACY_UPDATE_WITH_VALID_REGISTRATION"], "compiled rejection controls incomplete")
    proof_sources = {p.relative_to(ROOT).as_posix(): sha(p) for p in sorted((ROOT / "scripts").glob("*snas*.py"))}
    proof_sources.update({p.relative_to(ROOT).as_posix(): sha(p) for p in sorted((ROOT / "tools/AppleEverestBuilder/tests").glob("Snas*")) if p.is_file()})
    semantics.update({"freshSourceBindings": records["source-bindings"], "progressionPreservation": progression_proof,
                      "proofSources": proof_sources, "separateLegacyReferenceSha256": regression["legacyEntryAndTypeClosureAuthoritySha256"]})
    semantics["execution"] = {
        "actualCompiledRuntime": {key: value for key, value in runtime["result"].items() if key != "assemblySha256"},
        "pinnedRandomBubbleDifferential": records["random-bubble"],
        "actualCompiledSelectedFactories": compiled["factories"],
        "actualCompiledRegressionGuards": regression["selected"],
        "actualCompiledLegacyEntries": regression["legacy"],
        "actualCompiledNegativeControls": controls,
    }
    # New K-N logical proof identities describe source behavior and composition.
    # Raw invocation receipts (DLL bytes, private paths, compiler timings) remain
    # bound separately below. Existing managed/content/native hash rules do not change.
    composition_logical = {k: v for k, v in composition.items() if k not in
        {"compiledPreflightSha256", "runtimeProbeSha256", "terrainProbeSha256", "remainingSeparateProofs"}}
    composition_logical["terrainExecution"] = records["terrain"]
    composition_logical["runtimeExecution"] = read(out / "composition/runtime-composition.json")
    composition_logical["implicitAssetProbeSourceSha256"] = runtime["probeSourceSha256"]
    product = module("kn_product_contract", "verify-apple-everest-stage25kn-product-content.py")
    composition_logical["canonicalAssets"] = product.canonical_assets(canonical / "content/Content")
    composition_logical["canonicalAssetAuthoritySha256"] = product.CANONICAL_ASSET_AUTHORITY
    product.verify_audio_names(records["audio-names"])
    composition_logical["audioEventNameExecution"] = records["audio-names"]
    identities = {k: manifest[k] for k in ("sharedClosureSha256", "managedLogicalSha256", "contentLogicalSha256",
        "registrySha256", "customAudioManifestSha256", "customBankLogicalSetSha256", "levelSetProgressionManifestSha256", "collabManifestSha256")}
    identities.update({"factoryRegistrySha256": compiled["actualFactoryRegistrySha256"],
        "semanticLogicalSha256": logical(semantics), "compositionLogicalSha256": logical(composition_logical)})
    census = {"maps": len({r["map"] for r in all_profiles}), "customOccurrences": len(all_profiles),
              "distinctCustomIds": len({(r["kind"], r["customId"]) for r in all_profiles}),
              "rawAuthoredProfiles": len({(r["kind"], r["customId"], r["profileSha256"]) for r in all_profiles}), "regressionOccurrences": len(regression_profiles["occurrences"])}
    report = {"schemaVersion": 1, "stage": "25K-N", "status": "PASS", "marker": MARKER,
        "sharedClosureSha256": manifest["sharedClosureSha256"], "identities": identities, "census": census,
        "gateA": {"occurrences": len(all_profiles), "accepted": len(all_profiles), "blocked": 0, "unclassified": 0},
        "gateB": {"factories": 83, "available": 83, "missing": 0, "selectedProfileFactories": 77, "separateLegacyFactories": 6},
        "gateC": {"factories": 83, "closed": 83, "blocked": 0, "unknown": 0, "authority": semantics},
        "gateD": {**composition_logical, "status": "PASS_PRE_AOT_COMPOSITION", "blocked": 0, "unknown": 0,
                  "constructorAssets": "ACTUAL_COMPILED_CONSTRUCTORS_AND_EXACT_ATLAS_GEOMETRY",
                  "chapterTitleLayout": composition["chapterTitleLayout"]},
        "compiledAssemblySha256": sha(assembly), "compiledPreflightSha256": sha(args.production_preflight),
        "contentPlanSha256": sha(args.content_plan), "rawProofSha256": {k: logical(v) for k, v in records.items()},
        "physicalAcceptance": "PENDING_EXACT_PRODUCT_OBSERVATIONS",
        "limitations": runtime["result"]["fixtureBoundaries"] + ["Native audio playback, rendering, route completion and device save/relaunch require the exact signed products."]}
    # Assert the very same complete gate contract consumed by final products.
    product.verify_readiness(report, manifest)
    # A changed implementation needs review and a new immutable authority.
    # Retain its computed identities on mismatch, without a readiness receipt
    # or product marker; the frozen comparison below remains mandatory.
    (out / "candidate-identities.json").write_text(json.dumps({
        "status": "UNACCEPTED_UNTIL_FROZEN_IDENTITY_MATCH", "identities": identities,
        "census": census, "contentPlanSha256": sha(args.content_plan)}, indent=2, sort_keys=True) + "\n")
    product.verify_frozen_readiness(report, manifest)
    check(sha(assembly) == compiled["compiledAssemblySha256"], "assembly changed during expanded proof")
    verify_compilation_source_binding(compiled)
    (out / "readiness.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    (out / "semantic-proof.json").write_text(json.dumps(semantics, indent=2, sort_keys=True) + "\n")
    (out / "composition-proof.json").write_text(json.dumps(composition_logical, indent=2, sort_keys=True) + "\n")
    (out / MARKER).write_text(logical(report) + "\n")
    print("PASS: fresh K-N gates A1309/1309 B83/83 C83/83 D three unchanged SJ maps plus 18 regressions; physical acceptance remains pending")


if __name__ == "__main__":
    main()
