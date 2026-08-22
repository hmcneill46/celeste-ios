#!/usr/bin/env python3
"""Verify Stage 25H-D bounded static direct-ILHook production evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import subprocess


BASELINE = "6b5445f862d94da49df5aca8a9ecebc6e253cb20"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
EVEREST = "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00"
MONOMOD = "dfc30a1506d37fb88a2c2be004f525205f46a24c"
ZIP = "6a0649518d49cd0d17b84da3be53929cdd602d89d922e2d3ab87c524345e3807"
DLL = "3c5b79a57ce03b6c98e8ae12d781ec6baddce944995b2b4928f067a5ad7973ae"
SOURCE = "036bc9adbc5471931ca6cfb1aa574d0bbcfe3a6dce9025a09b56a0c522054d07"
BEFORE = "65ad66657afe6ca8d9d440be58b258cb48159b966b515aafe7b3b3ae85325416"
AFTER = "ebafbfe93b806ace3b0e49324702ff7a164adbd0379f142af2c864a6ae697abc"
DIFF = "a941d338396c91447d9c0f331137cbd0d04016da5b2eaf32ab0f684a71a93f30"
PLAN = "b3f1f7ee1b14028759bf29156b14d5145218144638d11f03df29728c80a63ca7"
DEVICE = "a4debb7153959317b9ce2b7366b55834091d26b28056a5f2b30d9131a41ade28"
CLOSURE = "769741d8cde76ffe7e7497263b326f434f889d416c94ea7b6f05422ebb7ff05e"


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


def sha(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--evidence-root", type=pathlib.Path,
                        help="optional ignored Stage 25H-D product evidence root")
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    paths = {
        "audit": root / "apple-everest/direct-ilhook-audit-stage25hd.json",
        "registry": root / "tools/AppleEverestBuilder/StaticIlFreeze.cs",
        "freezer": root / "tools/AppleEverestBuilder/AssemblyFreezer.cs",
        "worker": root / "tools/AppleEverestIlWorker/Program.cs",
        "analyzer": root / "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs",
        "models": root / "tools/AppleEverestBuilder/Models.cs",
        "tests": root / "tools/AppleEverestBuilder/tests/Program.cs",
        "builder": root / "scripts/build-apple-everest-canary.sh",
        "fetch": root / "scripts/fetch-apple-everest-stage25hd-fixture.sh",
        "compose": root / "scripts/test-apple-everest-il-compose.py",
        "runtime_source": root / "apple-everest/tests/il-compose/RuntimeReference/Program.cs",
        "runtime_project": root / "apple-everest/tests/il-compose/RuntimeReference/RuntimeReference.csproj",
        "runtime_lock": root / "apple-everest/tests/il-compose/RuntimeReference/packages.lock.json",
        "canary": root / "apple-everest/canaries/static-direct-ilhook-content/Content/Maps/AppleEverest/StaticDirectIlHook.xml",
        "compatibility": root / "docs/APPLE_EVEREST_COMPATIBILITY.md",
        "architecture": root / "docs/APPLE_EVEREST_STATIC_AOT.md",
        "report": root / "docs/history/stages/APPLE_EVEREST_DIRECT_ILHOOK_STAGE25HD_REPORT.md",
        "history": root / "docs/history/README.md",
    }
    for label, path in paths.items():
        c.require(path.is_file(), f"required {label} file")

    audit = json.loads(paths["audit"].read_text())
    c.require(audit["schemaVersion"] == 1 and audit["stage"] == "25H-D", "audit schema/stage")
    c.require(audit["status"] == "GREEN", "honest GREEN status")
    source = audit["highPrioritySource"]
    c.require(source["constructionSites"] == 53, "53-site source union")
    c.require(source["packages"] == {"CaeruleaHelper": 1, "CrystallineHelper": 2,
                                      "LuckyHelper": 8, "MaxHelpingHand": 34,
                                      "TeraHelper": 8}, "exact package census")

    policy = audit["classificationPolicy"]
    classes = [key for key in policy if key[:2] in {"A_", "B_", "C_", "D_", "E_", "F_", "G_", "H_"}]
    c.require(len(classes) == 8 and len(policy["primaryOrder"]) == 8, "eight exact primary classes")
    c.require(policy["primaryOrder"][0] == "C_STATIC_CONFIGURED", "configured classification precedence")
    c.require(policy["secondaryFlagsOverlap"], "secondary risks retained")

    summary = audit["summary"]
    expected_primary = {
        "A_STATIC_UNCONFIGURED_IMMUTABLE": 12,
        "B_STATIC_UNCONFIGURED_DYNAMIC_LIFETIME": 2,
        "C_STATIC_CONFIGURED": 32,
        "D_DYNAMIC_TARGET": 1,
        "E_DYNAMIC_MANIPULATOR": 0,
        "F_MULTIPLE_DIRECT_HOOKS_SAME_TARGET": 6,
        "G_CONDITIONAL_RUNTIME_CREATION": 0,
        "H_OTHER": 0,
    }
    c.require(summary["primaryCounts"] == expected_primary, "primary classification counts")
    c.require(sum(summary["primaryCounts"].values()) == 53, "primary classes close")
    c.require(summary["eligibleStaticUnconfiguredImmutable"] == 12, "12 eligible sites")
    c.require(summary["configuredSites"] == 32, "32 configured sites")
    c.require(summary["dynamicGameplayLifetimeSites"] == 3, "three dynamic-lifetime sites")
    c.require(summary["dynamicTargetSites"] == 12, "12 dynamic-target sites")
    c.require(summary["dynamicManipulatorSites"] == 0, "zero dynamic manipulators")
    c.require(summary["multipleDirectSameTargetSites"] == 13 and
              summary["multipleDirectSameTargetGroups"] == 5, "same-target risk counts")
    c.require(summary["allSitesExactlyClassified"], "no ambiguous site")

    sites = audit["sites"]
    c.require(len(sites) == 53 and len({site["id"] for site in sites}) == 53, "53 unique sites")
    c.require(all(site["primary"] in expected_primary for site in sites), "all primary labels known")
    c.require(all(all(key in site for key in ("package", "assembly", "method", "offset", "target",
                                               "manipulator", "storage", "config", "separateApply",
                                               "dispose", "dynamicGameplayLifetime", "dynamicTarget",
                                               "dynamicManipulator", "multipleSameTarget", "conditionalCreation"))
                  for site in sites), "complete per-site semantic records")
    actual_primary = {name: sum(site["primary"] == name for site in sites) for name in expected_primary}
    c.require(actual_primary == expected_primary, "site records reproduce primary counts")
    c.require(sum(site["dynamicGameplayLifetime"] for site in sites) == 3, "secondary lifetime count reproduces")
    c.require(sum(site["dynamicTarget"] for site in sites) == 12, "secondary target count reproduces")
    c.require(sum(site["dynamicManipulator"] for site in sites) == 0, "secondary manipulator count reproduces")
    c.require(sum(site["multipleSameTarget"] for site in sites) == 13, "secondary same-target count reproduces")
    selected_site = next(site for site in sites if site["id"] == "caerulea-dash-coroutine")
    c.require(selected_site["primary"] == "A_STATIC_UNCONFIGURED_IMMUTABLE", "selected site eligible")
    c.require(selected_site["config"] == "absent" and not selected_site["separateApply"], "selected config/apply")
    c.require(selected_site["storage"] == "static field DashCoroutineHook", "selected field storage")
    c.require(selected_site["dispose"] == "module Unload only", "selected teardown-only disposal")

    semantics = audit["commonSemantics"]
    c.require(semantics["applyByDefault"] == "implicit-true", "pinned immediate constructor apply")
    c.require(semantics["undoCalls"] == 0 and semantics["isAppliedReads"] == 0 and
              semantics["isValidReads"] == 0, "no mutable/read facade semantics")

    reacquired = audit["exactFixtureReacquisition"]
    c.require(len(reacquired) == 8 and len({item["package"] for item in reacquired}) == 8,
              "eight exact release families reacquired")
    c.require(all(len(item["zipSha256"]) == 64 and len(item["dllSha256"]) == 64
                  for item in reacquired), "release SHA shapes")
    c.require({item["package"] for item in reacquired} == {"CaeruleaHelper", "CrystallineHelper",
              "ExtendedVariantMode", "GravityHelper", "LuckyHelper", "MaxHelpingHand",
              "SorbetHelper", "TeraHelper"}, "mandatory helper audit set")

    fixture = audit["selectedFixture"]
    c.require(fixture["package"] == "CaeruleaHelper" and fixture["version"] == "1.11.1",
              "selected fixture identity")
    c.require(fixture["zipUrl"] == "https://gamebanana.com/mmdl/1784884", "exact public release URL")
    c.require(fixture["zipSha256"] == ZIP and fixture["dllSha256"] == DLL and
              fixture["sourceLogicalSha256"] == SOURCE, "fixture SHA locks")
    c.require(fixture["sourceCommit"] == "ce2ad0694feb28cd3dff0a5d7501f6e60d620fd5" and
              fixture["license"] == "MIT", "source/license provenance")
    c.require(fixture["planClass"] == "STATIC_DIRECT_ILHOOK_FREEZE", "bounded class name")
    c.require(not fixture["sourceRequiredForProduction"], "distributed DLL authoritative")
    c.require("no facade remains" in fixture["runtimeRewrite"], "no device facade")

    transform = audit["productionTransform"]
    c.require(transform["schemaVersion"] == 3 and
              transform["worker"] == "apple-everest-static-il-worker-v3", "plan schema/worker v3")
    c.require(transform["mechanism"] == "DIRECT_ILHOOK", "mechanism field")
    c.require(transform["constructor"] == semantics["constructor"], "exact constructor overload")
    c.require("GetMethod(\"DashCoroutine\"" in transform["targetExpression"] and
              transform["resolvedTarget"].endswith("::MoveNext()"), "static iterator target")
    c.require(transform["resolvedManipulator"] ==
              "Celeste.Mod.CaeruleaHelper.Hooks.DashSpeedHook.ModifyDashCoroutineIL",
              "exact static manipulator")
    c.require(transform["config"] is None and transform["applyByDefault"], "null config and immediate apply")
    c.require(transform["storage"] == "static field DashCoroutineHook", "exact storage")
    c.require(transform["separateApplyCalls"] == 0 and transform["gameplayUndoCalls"] == 0 and
              transform["gameplayDisposeCalls"] == 0 and transform["moduleUnloadDisposeCalls"] == 1,
              "immutable lifetime")
    c.require(transform["baselineSha256"] == BEFORE and transform["afterSha256"] == AFTER and
              transform["diffSha256"] == DIFF, "target before/after/diff locks")
    c.require(transform["frozenPlanSha256"] == PLAN and transform["frozenDllSha256"] == DEVICE,
              "plan/device locks")
    c.require(transform["originalDllSha256"] == DLL, "original DLL lock repeated")
    c.require(transform["threeRunDeterminism"] == {"independentRuns": 3, "allIdentical": True,
              "planSha256": PLAN}, "three-run determinism")
    c.require(transform["desktopReference"] == "MATCH", "pinned desktop reference")
    c.require(transform["remainingFacade"] is None and
              transform["deviceForbiddenRuntimeReferences"] == [], "no device direct-hook backend")
    c.require("preserves speed" in transform["behavior"], "observable real behavior")

    composition = audit["compositionConformance"]
    c.require(composition["pinnedMonoModCommit"] == MONOMOD, "composition uses pinned MonoMod")
    c.require(composition["eventThenDirect"] == {"desktopResult": 55, "appleFrozenResult": 55},
              "event then direct order matches")
    c.require(composition["directThenEvent"] == {"desktopResult": 43, "appleFrozenResult": 43},
              "direct then event order matches")
    c.require(composition["directThenOn"] == {"desktopResult": 140,
              "appleFrozenUnderlyingResult": 40, "onWrapperDelta": 100},
              "On orig sees direct-frozen target")
    c.require(composition["eventEventDirectThenOn"] == {"desktopResult": 148,
              "appleFrozenUnderlyingResult": 48, "onWrapperDelta": 100},
              "H-B sequence plus direct plus On matches")
    c.require(composition["desktopEntryPoints"] == ["HookEndpointManager.Modify",
              "ILHook(MethodBase,ILContext.Manipulator)", "HookEndpointManager.Add"] and
              not composition["deviceRuntimeIlHookBackend"], "real desktop entry points and no device backend")

    product = audit["productEvidence"]
    c.require(product["sharedClosureSha256"] == CLOSURE, "shared closure lock")
    for key in ("managedLogicalSha256", "contentLogicalSha256", "hookTransformSha256",
                "modInteropPlanSha256", "appleApiSurfaceSha256"):
        c.require(len(product[key]) == 64, f"product {key} shape")
    c.require(product["fullTrim"] and product["fullAot"] and not product["useInterpreter"] and
              not product["jit"], "full trim/AOT/no interpreter/JIT")
    c.require(product["unsignedIos"] == {"bytes": 882016940,
              "sha256": "e59554b51e5b750266b59468d294111e94e352c8d8f5e8dda29ab7d834398450"},
              "unsigned iOS product evidence")
    c.require(product["unsignedTvos"] == {"bytes": 896194867,
              "sha256": "2e0780ca797144d259bb56c6b41102123acc63aa5270498e7d3b18b2f9a44224"},
              "unsigned tvOS product evidence")
    c.require(product["signedBuildSeconds"] == 1487.2 and
              product["signedIos"] == {"bytes": 882189558,
              "sha256": "667358e7879812ff5649798853bbcace2ce0ae58221e78b12ffa93fd3dae995f"} and
              product["signedTvos"] == {"bytes": 896602166,
              "sha256": "782c864c8b8071abe34a59020a377977a4e2a61006d544c28b2a7b085b6578ed"},
              "signed full-AOT product evidence")
    physical = product["physical"]
    c.require(all(value["status"] == "PASS" for value in physical.values()),
              "iPhone/iPad/tvOS physical matrix")
    c.require(physical["ipadOs15_8_8"]["sameUniversalIosProduct"] and
              physical["appleTv"]["sameSharedClosure"], "shared iOS/tvOS closure physical evidence")

    payoff = audit["graphPayoff"]
    c.require(payoff["selectedGraph"] == "QLetterAurora", "exact selected Stage 25G graph")
    c.require(payoff["selectedGraphHelpers"] == ["CpopHelper", "CaeruleaHelper",
              "MaxHelpingHand", "LuckyHelper"], "selected exact graph helpers")
    c.require(payoff["directIlHookSitesBefore"] == 43 and
              payoff["directIlHookSitesAfterSelectedFixture"] == 42, "graph direct-hook distance")
    c.require(payoff["materiallyAdvancedGraphs"] == ["QLetterAurora"], "one materially advanced graph")
    c.require(payoff["zeroDirectIlHookGraphs"] == [] and payoff["zeroIlBlockerGraphs"] == [] and
              payoff["zeroOverallBlockerGraphs"] == [], "no false graph unlock")
    c.require(payoff["closestOverallAfterStage"] == "LittleEpic's Precision Challenge" and
              not payoff["readyForStage25G2"], "closest graph and pre-AOT decision")

    registry = paths["registry"].read_text()
    for token in (ZIP, DLL, SOURCE, BEFORE, AFTER, DIFF, "DIRECT_ILHOOK",
                  "CaeruleaHelper direct ILHook constructor contract drifted",
                  "CaeruleaHelper direct ILHook lifetime contract drifted",
                  "CaeruleaHelper direct ILHook storage/apply contract drifted",
                  "RewriteCaerulea", "DashCoroutineHook", "dashSpeed.Fields.Remove"):
        c.require(token in registry, f"registry contract {token}")
    c.require('"absent", "implicit-true", "DashCoroutineHook", "MODULE_IMMUTABLE_ACTIVE"' in registry,
              "exact absent-config/immediate-apply/storage/lifetime record")
    worker = paths["worker"].read_text()
    for token in ("exact DashCoroutine iterator target is missing or ambiguous",
                  "target baseline mismatch", "transformed IL lock mismatch",
                  "EmitDelegate lowering count/targets drifted", "forbidden final IL reference",
                  "DynamicReferenceManager", "Reflection.Emit"):
        c.require(token in worker, f"worker fail-closed token {token}")
    c.require("context.Invoke(manipulator)" in worker, "real distributed manipulator executes")
    c.require("DIRECT_ILHOOK" in paths["models"].read_text() and
              "STATIC_DIRECT_ILHOOK_FREEZE" in paths["models"].read_text(), "typed production class")
    c.require("STATIC_DIRECT_ILHOOK_FREEZE" in paths["analyzer"].read_text(), "analyzer classifies direct freeze")
    c.require("StaticIlFreeze.RewriteDeviceAssembly" in paths["freezer"].read_text() and
              "RewriteCaerulea" in registry, "generic freezer dispatches exact Caerulea rewrite")
    tests = paths["tests"].read_text()
    for token in ("all 53 high-priority direct construction sites", "direct audit exact eligibility",
                  "Caerulea runtime constructor, field and unload disposal are removed source-free",
                  "exact direct constructor, target, manipulator, storage and lifetime fail closed"):
        c.require(token in tests, f"deterministic test contract {token}")
    c.require("CaeruleaHelper/NoDashSpeedResetTrigger" in paths["canary"].read_text(),
              "real behavior canary trigger")
    compose = paths["compose"].read_text()
    runtime_source = paths["runtime_source"].read_text()
    c.require(all(token in compose for token in ("event-direct", "direct-event", "direct-on",
              "event-event-direct-on", "apple-everest-static-il-worker-v3")),
              "direct/event/On frozen composition scenarios")
    c.require(all(token in runtime_source for token in ("HookEndpointManager", "new(target, directManipulator)",
              "AddHundred", "ComposeTarget.Compose(4)")), "actual pinned desktop composition fixture")
    compose_summary_path = root / ".build/apple-everest/il-compose/summary.json"
    c.require(compose_summary_path.is_file(), "ignored direct composition evidence")
    compose_summary = json.loads(compose_summary_path.read_text())["directIlHookComposition"]
    c.require(compose_summary["desktopRegistrationResults"] == {
              "eventThenDirect": 55, "directThenEvent": 43, "directThenOn": 140,
              "eventEventDirectThenOn": 148}, "executed desktop composition results")
    c.require(compose_summary["eventThenDirectAppleResult"] == 55 and
              compose_summary["directThenEventAppleResult"] == 43 and
              compose_summary["directThenOnUnderlyingAppleResult"] == 40 and
              compose_summary["eventEventDirectThenOnUnderlyingAppleResult"] == 48 and
              not compose_summary["appleRuntimeBackend"], "executed Apple frozen composition results")
    fetch = paths["fetch"].read_text()
    c.require(ZIP in fetch and "https://gamebanana.com/mmdl/1784884" in fetch,
              "clean-clone exact fixture fetch")
    c.require(not any(path.suffix.lower() in {".zip", ".dll"} for path in
                      (root / "apple-everest/canaries/static-direct-ilhook-content").rglob("*")),
              "tracked canary has no external bytes")

    docs = {key: paths[key].read_text() for key in ("compatibility", "architecture", "report", "history")}
    for token in ("STATIC_DIRECT_ILHOOK_FREEZE", "CaeruleaHelper", "immutable-active"):
        c.require(token in docs["architecture"], f"architecture token {token}")
    for token in ("STATIC_DIRECT_ILHOOK_FREEZE", "DEFERRED_CONFIGURED_ILHOOK",
                  "DEFERRED_DYNAMIC_DIRECT_ILHOOK_LIFETIME"):
        c.require(token in docs["compatibility"], f"compatibility token {token}")
    for token in ("Stage 25H-D", "PASS — GREEN", "QLetterAurora", "LittleEpic",
                  "No Dash Speed Reset", CLOSURE):
        c.require(token in docs["report"], f"report token {token}")
    c.require("APPLE_EVEREST_DIRECT_ILHOOK_STAGE25HD_REPORT.md" in docs["history"], "history index")

    for path in (
        "apple-everest/graph-il-closure-audit-stage25hc.json",
        "docs/history/stages/APPLE_EVEREST_MULTI_HELPER_STAGE25G_REPORT.md",
        "docs/history/stages/APPLE_EVEREST_STATIC_IL_STAGE25H_REPORT.md",
        "docs/history/stages/APPLE_EVEREST_STATIC_IL_COMPOSITION_STAGE25HB_REPORT.md",
        "docs/history/stages/APPLE_EVEREST_GRAPH_IL_CLOSURE_STAGE25HC_REPORT.md",
    ):
        c.require((root / path).read_bytes() == historical(root, path),
                  f"historical bytes unchanged: {path}")

    locks = audit["unchangedLocks"]
    expected_locks = {
        "canonicalContent": "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46",
        "canonicalRaw": "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273",
        "canonicalPatched": "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5",
        "stage6RealAudio": "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9",
        "iosNative": "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2",
        "tvosNative": "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc",
        "vanillaIosTree": "2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357",
        "stage25haFrozenDll": "79d06fd9428e33613b81f089693651f03c6445e0a6f22368902bb3fcb49e904f",
        "stage25hcVortexPlan": "96726af5fa21c129d1b1bd39746a1f6f903be38e2b53b20b79275eb05d5a3261",
        "stage25fb2ModInteropPlan": "9e755781f2d107d37bec45bd9fe4b551a28e9ddf8919f91f11d878c1dd041318",
    }
    c.require(locks == expected_locks, "canonical/native/vanilla/regression locks")
    profile = json.loads((root / "apple-everest/profiles/stable-1.6458.0.json").read_text())
    c.require(profile["everest"]["sha256Commit"] == EVEREST, "Everest pin")
    c.require(profile["dependencies"]["monoModCommit"] == MONOMOD, "MonoMod pin")
    c.require(resolve(root, "ios-v0.1.1-rc.1") == IOS_RC, "iOS RC immutable")
    c.require(resolve(root, "v1.0.0-rc.1") == RC1, "tvOS RC1 immutable")
    c.require(resolve(root, "v1.0.0-rc.2") == RC2, "tvOS RC2 immutable")
    c.require(resolve(root, "origin/release/v1.0.0-rc.3") == RC3, "RC3 branch immutable")
    c.require(git(root, "tag", "-l", "v1.0.0-rc.3") == "", "RC3 tag absent")
    c.require(resolve(root, BASELINE) == BASELINE, "baseline commit exists")

    if args.evidence_root:
        evidence = args.evidence_root.resolve()
        closure = evidence / "shared-closure/compatibility-manifest.json"
        c.require(closure.is_file(), "ignored closure manifest")
        manifest = json.loads(closure.read_text())
        c.require(manifest["sharedClosureSha256"] == CLOSURE, "ignored exact shared closure")
        c.require(manifest["frozenIlPlanSha256"] == PLAN and
                  manifest["frozenIlSchema"] == 3, "ignored exact plan/schema")
        c.require(manifest["frozenIlTransformCount"] == 13 and
                  sum(item["EventType"] == "DIRECT_ILHOOK" for item in manifest["frozenIlTransforms"]) == 1,
                  "ignored one direct transform among 13")
        frozen = next(item for item in manifest["frozenAssemblies"] if item["owner"] == "CaeruleaHelper")
        c.require(frozen["originalSha256"] == DLL and frozen["frozenSha256"] == DEVICE,
                  "ignored original/frozen DLL")

    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".zip", ".dll", ".bank", ".ipa", ".app"))
                      for path in tracked), "no external/product bytes tracked")
    public_text = "\n".join((root / path).read_text(errors="ignore") for path in tracked
                              if (root / path).is_file() and
                              path.startswith(("apple-everest/", "docs/", "scripts/", "tools/")))
    c.require("/" + "Users" + "/" + "harrymcneill" + "/" not in public_text,
              "no private absolute path")

    print(f"PASS: Stage 25H-D verifier ({c.count} checks; GREEN, bounded static direct ILHook)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
