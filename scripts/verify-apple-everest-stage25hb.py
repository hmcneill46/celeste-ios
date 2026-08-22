#!/usr/bin/env python3
"""Verify Stage 25H-B composed frozen IL and singleton-delegate lowering."""

from __future__ import annotations

import argparse
import json
import pathlib
import subprocess


BASELINE = "9bc5d75fcaf42571c1575843f943cea39ce626dc"
STAGE25G = "9ffc1460d15bfe69074f55f6f3187d3cb2156365"
STAGE25FB = "6cde1ad9ba93bbd2f57f2867baf3b189b2824b1b"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
EVEREST = "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00"
MONOMOD = "dfc30a1506d37fb88a2c2be004f525205f46a24c"
DISPOSABLE_ZIP = "df291c0175df46682791fb6373c47eb557c47483eca3db96895eba9b5bbe85b5"
DISPOSABLE_DLL = "1d47c08238fd0dd29eaa5c6e53a36e7d72942fdb7b7abc2a3870f09fbc952dcc"
DISPOSABLE_SOURCE = "fc6aa15ee69311eac205af76e382d8a09dfb16ebe73eb05163ba90da7c21d597"
PLAN = "90759f477ea46d97aac981a1e8c61bbf8df97fce26bc4bc080c18aa30dd2473b"
CANONICAL = {
    "Content": "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46",
    "Raw": "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273",
    "Patched": "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5",
    "Stage6": "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9",
    "iOSNative": "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2",
    "tvOSNative": "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc",
}


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: bool, message: str) -> None:
        if not condition:
            raise SystemExit("FAIL: " + message)
        self.count += 1


def git(root: pathlib.Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(root), *args], check=True,
                          capture_output=True, text=True).stdout.strip()


def resolve(root: pathlib.Path, ref: str) -> str:
    return git(root, "rev-parse", f"{ref}^{{commit}}")


def historical(root: pathlib.Path, path: str) -> bytes:
    return subprocess.run(["git", "-C", str(root), "show", f"{BASELINE}:{path}"],
                          check=True, capture_output=True).stdout


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--closure", type=pathlib.Path)
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    paths = {
        "audit": root / "apple-everest/il-compatibility-audit-stage25hb.json",
        "catalog": root / "apple-everest/managed-detour-targets-v2.json",
        "registry": root / "tools/AppleEverestBuilder/StaticIlFreeze.cs",
        "models": root / "tools/AppleEverestBuilder/Models.cs",
        "closure": root / "tools/AppleEverestBuilder/ClosureGenerator.cs",
        "analyzer": root / "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs",
        "worker": root / "tools/AppleEverestIlWorker/Program.cs",
        "workerProject": root / "tools/AppleEverestIlWorker/AppleEverestIlWorker.csproj",
        "builderTests": root / "tools/AppleEverestBuilder/tests/Program.cs",
        "composeTest": root / "scripts/test-apple-everest-il-compose.py",
        "fetch": root / "scripts/fetch-apple-everest-stage25hb-fixtures.sh",
        "compatibility": root / "docs/APPLE_EVEREST_COMPATIBILITY.md",
        "architecture": root / "docs/APPLE_EVEREST_STATIC_AOT.md",
        "report": root / "docs/history/stages/APPLE_EVEREST_STATIC_IL_COMPOSITION_STAGE25HB_REPORT.md",
        "history": root / "docs/history/README.md",
    }
    for label, path in paths.items():
        c.require(path.is_file(), f"required {label} file")

    audit = json.loads(paths["audit"].read_text())
    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25H-B", "audit schema/stage")
    c.require(audit["status"] == "PASS_YELLOW", "honest final YELLOW classification")
    c.require(audit["baseline"]["integrationCommit"] == BASELINE, "exact baseline")
    c.require(audit["baseline"]["stage25gHistoricalCommit"] == STAGE25G, "Stage 25G history")
    c.require(audit["baseline"]["stage25fbHistoricalCommit"] == STAGE25FB, "Stage 25F-B history")
    c.require(audit["baseline"]["everestCommit"] == EVEREST, "Everest pin")
    c.require(audit["baseline"]["monoModCommit"] == MONOMOD, "MonoMod pin")
    c.require(audit["baseline"]["managedDetourTargetCountBefore"] == 56, "pre-HB target count")
    c.require(audit["baseline"]["managedDetourTargetCountAfter"] == 57, "post-HB target count")
    c.require(audit["baseline"]["newManagedDetourTargets"] == ["celeste-player-throw"], "one exact new target")

    cohort = audit["originalStage25HCohort"]
    expected_cohort = {"realModsOrHelpers": 27, "hookGenIlAddSites": 138,
                       "distinctSubscriptions": 135, "distinctTargets": 115,
                       "targetsWithMultipleSubscribers": 23,
                       "emitDelegateSourceOccurrences": 201,
                       "directIlHookConstructions": 45,
                       "configuredIlHookSourceOccurrences": 4}
    for key, value in expected_cohort.items():
        c.require(cohort[key] == value, f"retained cohort {key}")

    taxonomy = audit["emitDelegateTaxonomy"]
    original = taxonomy["originalCohort"]
    c.require(original["total"] == 201, "EmitDelegate total")
    c.require(original["staticMethodGroupNoCaptureProven"] == 6, "six H-A static sites")
    c.require(original["unknown"] == 195, "unresolved source cohort kept honest")
    c.require(original["lambdaAdjacentCandidatesWithinUnknown"] == 38, "lambda-adjacent candidates")
    c.require(original["staticMethodGroupNoCaptureProven"] + original["unknown"] == original["total"],
              "EmitDelegate taxonomy total closes")
    additional = taxonomy["additionalRealHBCandidate"]
    c.require(additional["compilerGeneratedNoncapturingSingletonLambdas"] == 2,
              "two real compiler-singleton lambdas")
    c.require(additional["runtimeParameterDelegates"] == 2 and additional["capturedHostObjects"] == 0,
              "typed runtime delegates without host capture")
    c.require(taxonomy["acceptedProductionFixturesCombined"]["totalExactlyLoweredSites"] == 8,
              "eight exact accepted delegate sites")

    semantics = audit["sequenceSemantics"]
    c.require(semantics["class"] == "STATIC_IL_EVENT_SEQUENCE", "sequence class")
    c.require(semantics["evolvingBody"] and semantics["intermediateValidation"] and
              semantics["intermediateHashing"], "evolving intermediate chain")
    c.require(semantics["duplicateRegistration"] == "rejected", "duplicate rejection")
    c.require(semantics["configuredSequences"] == "DEFERRED_CONFIGURED_IL_SEQUENCE",
              "configured ordering remains deferred")
    c.require(semantics["runtimeUnapply"] is False, "immutable runtime policy")
    project = audit["projectOwnedComposition"]
    c.require(project["maximumSequenceLength"] == 3, "three manipulator conformance")
    c.require(project["aThenBResult"] == 55 and project["bThenAResult"] == 43,
              "observable registration order")
    c.require(project["aThenBThenCResult"] == 48, "three-step result")
    c.require(project["desktopMonoModCompared"], "pinned desktop comparison")
    c.require("dangling intermediate branch" in project["negativeCases"], "intermediate invalid IL rejection")

    fixture = audit["selectedFixture"]
    c.require(fixture["name"] == "DisposableTheo" and fixture["version"] == "1.0.6", "fixture identity")
    c.require(fixture["downloadUrl"] == "https://gamebanana.com/mmdl/929736", "fixture URL")
    c.require(fixture["zipSha256"] == DISPOSABLE_ZIP, "fixture ZIP pin")
    c.require(fixture["dllSha256"] == DISPOSABLE_DLL, "fixture DLL pin")
    c.require(fixture["sourceLogicalSha256"] == DISPOSABLE_SOURCE, "fixture source audit pin")
    c.require(fixture["authoritativeInput"] == "ordinary public release ZIP and precompiled DLL",
              "source-free authority")
    c.require(fixture["sourceRequiredForProduction"] is False, "source absent from production")
    c.require(fixture["ordinaryIlRegistrations"] == 2 and fixture["ordinaryOnRegistrations"] == 2,
              "complete real registration census")
    c.require(not fixture["directIlHook"] and not fixture["configuredIlHook"], "ordinary unconfigured fixture")
    c.require(len(audit["acceptedRealTransforms"]) == 2, "two real accepted transforms")
    for transform in audit["acceptedRealTransforms"]:
        c.require(transform["ordinal"] == 0, "single real transform target ordinal")
        c.require(all(len(transform[key]) == 64 for key in ("beforeSha256", "afterSha256", "diffSha256")),
                  "real transform hashes")
        c.require("<>c" in transform["delegateTarget"], "real compiler singleton target")

    c.require(not audit["realMultipleManipulatorEvidence"]["sameTargetSequenceLocated"],
              "no false real same-target claim")
    c.require("YELLOW" in audit["realMultipleManipulatorEvidence"]["assessment"],
              "real composition evidence labelled")
    mapping = audit["stage25GGraphMapping"]
    c.require(len(mapping) == 18 and len({item["graph"] for item in mapping}) == 18,
              "all 18 Stage 25G IL-primary graphs mapped")
    c.require(all(item["hBResult"] == "not exact-registered" for item in mapping),
              "no false graph unlock")
    payoff = audit["stage25GPayoff"]
    c.require(payoff["materiallyAdvancedByExactHBCandidate"] == 0, "exact graph advancement count")
    c.require(payoff["graphsWithNoRemainingIlBlocker"] == 0 and payoff["graphsWithNoOverallBlocker"] == 0,
              "no false zero-blocker claim")
    c.require(len(payoff["broaderCandidateMapsDependingOnDisposableTheo"]) == 6,
              "broader candidate linkage")
    for deferred in ("DEFERRED_DIRECT_ILHOOK", "DEFERRED_CONFIGURED_ILHOOK",
                     "DEFERRED_CAPTURED_MODULE_INSTANCE", "DEFERRED_HOST_LOCAL_CAPTURE"):
        c.require(deferred in audit["deferredClasses"], f"deferred class {deferred}")

    catalog = json.loads(paths["catalog"].read_text())
    c.require(catalog["schemaVersion"] == 2 and len(catalog["targets"]) == 57, "catalog v2/57")
    c.require(sum(target["id"] == "celeste-player-throw" for target in catalog["targets"]) == 1,
              "Player.Throw target exactly once")
    text = {key: path.read_text() for key, path in paths.items() if path.suffix in {".cs", ".py", ".md"}}
    contract = text["registry"] + text["models"] + text["closure"] + text["analyzer"]
    for token in (DISPOSABLE_ZIP, DISPOSABLE_DLL, DISPOSABLE_SOURCE, "STATIC_IL_EVENT_SEQUENCE",
                  "ManipulatorIsStatic", "RegistrationOrdinal", "ExpectedDelegateTargets",
                  "ComposeFrozenIlTransforms", "apple-everest-static-v9"):
        c.require(token in contract, f"production contract {token}")
    worker = text["worker"]
    for token in ("same-target registration order is not closed and contiguous",
                  "same-target intermediate hash chain", "duplicate frozen-IL manipulator",
                  "intermediate chain mismatch before", "ValidateBody(target)",
                  "compiler-singleton-noncapturing", "capturing EmitDelegate closure is deferred",
                  "unreviewed EmitDelegate target", "forbidden final IL reference"):
        c.require(token in worker, f"worker contract {token}")
    c.require("document.SchemaVersion != 2" in worker and "apple-everest-static-il-worker-v2" in worker,
              "frozen plan schema/worker v2")
    c.require("Assembly.LoadFrom" in worker and "context.Invoke(manipulator)" in worker,
              "real distributed manipulator runs host-only")
    c.require("MonoMod.Backports" in paths["workerProject"].read_text() and
              "MonoMod.ILHelpers" in paths["workerProject"].read_text(), "pinned host delegate support")

    compose = text["composeTest"]
    for token in ("A,B,C", "reject-invalid-intermediate", "reject-duplicate",
                  "compiler-singleton-lambda", "threeRunDeterminism", "post-rejection-isolation",
                  "DOTNET_ARTIFACTS"):
        c.require(token in compose, f"composition conformance {token}")
    c.require("https://gamebanana.com/mmdl/929736" in paths["fetch"].read_text() and
              DISPOSABLE_ZIP in paths["fetch"].read_text(), "exact source-free fixture fetch")

    for token in ("STATIC_IL_EVENT_SEQUENCE", "Disposable Theo", "compiler-generated",
                  "real shared-target", "immutable-active"):
        c.require(token in text["compatibility"], f"compatibility docs {token}")
    for token in ("manipulator A → validate/hash", "manipulator B → validate/hash",
                  "A→B/B→A/A→B→C", "never serializes the Mac closure object"):
        c.require(token in text["architecture"], f"architecture docs {token}")
    for token in ("Stage 25H-B", "DisposableTheo", "STATIC_IL_EVENT_SEQUENCE",
                  "iPadOS 15.8.8", "No GitHub Actions"):
        c.require(token in text["report"], f"report token {token}")
    c.require("APPLE_EVEREST_STATIC_IL_COMPOSITION_STAGE25HB_REPORT.md" in text["history"], "history index")

    for label, value in CANONICAL.items():
        c.require(value in json.dumps(audit) and value in text["report"], f"unchanged lock {label}")
    profile = json.loads((root / "apple-everest/profiles/stable-1.6458.0.json").read_text())
    c.require(profile["everest"]["sha256Commit"] == EVEREST, "profile Everest pin")
    c.require(profile["dependencies"]["monoModCommit"] == MONOMOD, "profile MonoMod pin")
    c.require(resolve(root, "ios-v0.1.1-rc.1") == IOS_RC, "iOS RC immutable")
    c.require(resolve(root, "v1.0.0-rc.1") == RC1, "tvOS RC1 immutable")
    c.require(resolve(root, "v1.0.0-rc.2") == RC2, "tvOS RC2 immutable")
    c.require(resolve(root, "origin/release/v1.0.0-rc.3") == RC3, "RC3 branch immutable")
    c.require(git(root, "tag", "-l", "v1.0.0-rc.3") == "", "RC3 tag absent")
    c.require(resolve(root, BASELINE) == BASELINE, "baseline commit exists")

    for path in ("apple-everest/multi-helper-candidate-audit-stage25g.json",
                 "docs/history/stages/APPLE_EVEREST_MULTI_HELPER_STAGE25G_REPORT.md",
                 "docs/history/stages/APPLE_EVEREST_STATIC_IL_STAGE25H_REPORT.md"):
        c.require((root / path).read_bytes() == historical(root, path), f"historical bytes unchanged: {path}")

    if args.closure:
        closure = args.closure.resolve()
        manifest = json.loads((closure / "compatibility-manifest.json").read_text())
        c.require(manifest["transformerVersion"] == "apple-everest-static-v9", "closure transformer v9")
        c.require(manifest["managedDetourTargetCount"] == 57, "closure target count")
        c.require(manifest["frozenIlSchema"] == 2 and
                  manifest["frozenIlWorker"] == "apple-everest-static-il-worker-v2", "closure plan v2")
        c.require(manifest["frozenIlPlanSha256"] == PLAN, "closure plan hash")
        c.require(manifest["frozenIlTransformCount"] == 4, "four exact real transforms")
        c.require({item["Owner"] for item in manifest["frozenIlTransforms"]} ==
                  {"DashToggleHelper", "DisposableTheo"}, "two exact frozen owners")
        c.require(all(item["runtimeUnload"] == "unsupported-immutable-active"
                      for item in manifest["frozenIlTransforms"]), "closure immutable policy")
        c.require(manifest["precompiledAssembliesAotLinked"] == 2, "two rooted precompiled assemblies")

    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".zip", ".dll", ".bank", ".ipa", ".app"))
                      for path in tracked), "no proprietary product/fixture bytes tracked")
    public_text = "\n".join((root / path).read_text(errors="ignore") for path in tracked
                              if (root / path).is_file() and path.startswith(("apple-everest/", "docs/")))
    c.require("/Users/" not in public_text, "no private absolute path")

    print(f"PASS: Stage 25H-B verifier ({c.count} checks; composed frozen IL, fail-closed)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
