#!/usr/bin/env python3
"""Stage 25H-B same-target frozen-IL conformance against pinned MonoMod."""

from __future__ import annotations

import hashlib
import json
import pathlib
import shutil
import subprocess
import sys


REPO = pathlib.Path(__file__).resolve().parent.parent
ROOT = REPO / ".build/apple-everest/il-compose"
DOTNET_ARTIFACTS = ROOT / "dotnet-artifacts"
TEST = REPO / "apple-everest/tests/il-compose"
DOTNET8 = REPO / ".build/apple-everest/toolchain/dotnet8/dotnet"
DOTNET9 = REPO / ".build/apple-everest/toolchain/dotnet9/dotnet"
DOTNET10 = pathlib.Path("/usr/local/share/dotnet/dotnet")
UPSTREAM = REPO / ".build/apple-everest/upstream/Everest"
MONOMOD_SHA = "dfc30a1506d37fb88a2c2be004f525205f46a24c"
WORKER_ID = "apple-everest-static-il-worker-v2"
TARGET_METHOD = "System.Int32 AppleEverest.IlCompose.ComposeTarget::Compose(System.Int32)"
MANIPULATOR_TYPE = "AppleEverest.IlCompose.SequenceManipulators"


def run(*args: object, cwd: pathlib.Path | None = None, expect: int = 0) -> subprocess.CompletedProcess[str]:
    process = subprocess.run([str(value) for value in args], cwd=cwd or REPO, text=True,
                             stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if process.returncode != expect:
        sys.stderr.write(process.stdout)
        raise RuntimeError(f"command returned {process.returncode}, expected {expect}: {args}")
    return process


def sha(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def text_sha(value: str) -> str:
    return hashlib.sha256(value.encode()).hexdigest()


def plan_sha(transforms: list[dict[str, object]]) -> str:
    lines: list[str] = []
    for item in transforms:
        values = [
            item["planId"], item["assemblySha256"], item["eventType"], item["eventName"],
            item["targetMethod"], item["canonicalTargetMethod"], item["manipulatorType"],
            item["manipulatorMethod"], "True" if item["manipulatorIsStatic"] else "False",
            str(item["registrationOrdinal"]), item["beforeSha256"], item["afterSha256"],
            item["diffSha256"], ",".join(item["expectedDelegateTargets"]),
        ]
        line = "\0".join(str(value) for value in values)
        if item.get("mechanism") == "DIRECT_ILHOOK":
            line += "\0" + "\0".join(str(item[key]) for key in (
                "mechanism", "constructorSignature", "targetExpression",
                "manipulatorExpression", "config", "applyByDefault", "storage", "lifetime"))
        lines.append(line)
    return text_sha("\n".join(lines) + "\n")


def reference(sequence: str, baseline: pathlib.Path, name: str) -> tuple[pathlib.Path, dict[str, object]]:
    destination = ROOT / "reference" / name
    destination.mkdir(parents=True)
    run(DOTNET8, DOTNET_ARTIFACTS / "bin/Reference/release/Reference.dll", "--sequence", sequence,
        baseline, destination)
    return destination / "AppleEverestIlComposeTarget.dll", json.loads(
        (destination / "reference.json").read_text())


def transform(owner: str, manipulator: str, ordinal: int, step: dict[str, object], *,
              mechanism: str = "HOOKGEN_IL_EVENT") -> dict[str, object]:
    result: dict[str, object] = {
        "planId": f"{owner}:Compose:{manipulator}",
        "owner": owner,
        "assemblySha256": MANIP_SHA,
        "eventType": "IL.AppleEverest.IlCompose.ComposeTarget",
        "eventName": "Compose",
        "targetMethod": TARGET_METHOD,
        "canonicalTargetMethod": TARGET_METHOD,
        "manipulatorType": MANIPULATOR_TYPE,
        "manipulatorMethod": manipulator,
        "manipulatorIsStatic": True,
        "registrationOrdinal": ordinal,
        "beforeSha256": step["beforeSha256"],
        "afterSha256": step["afterSha256"],
        "diffSha256": step["diffSha256"],
        "expectedDelegateTargets": [],
    }
    if mechanism == "DIRECT_ILHOOK":
        result.update({
            "mechanism": mechanism,
            "constructorSignature":
                "System.Void MonoMod.RuntimeDetour.ILHook::.ctor(System.Reflection.MethodBase,MonoMod.Cil.ILContext/Manipulator)",
            "targetExpression": "typeof(ComposeTarget).GetMethod(\"Compose\")",
            "manipulatorExpression": f"{MANIPULATOR_TYPE}.{manipulator}",
            "config": "absent",
            "applyByDefault": "implicit-true",
            "storage": "scoped fixture local",
            "lifetime": "MODULE_IMMUTABLE_ACTIVE",
        })
    return result


def worker(name: str, baseline: pathlib.Path, transforms: list[dict[str, object]], *,
           expected: int = 0, declared_sha: str | None = None) -> tuple[pathlib.Path, pathlib.Path, str]:
    destination = ROOT / "worker" / name
    destination.mkdir(parents=True)
    fixture_directory = destination / "fixtures"
    fixture_directory.mkdir()
    for owner in {str(item["owner"]) for item in transforms}:
        shutil.copy2(MANIP, fixture_directory / f"{owner}.original.dll")
    direct = any(item.get("mechanism") == "DIRECT_ILHOOK" for item in transforms)
    plan = {"schemaVersion": 3 if direct else 2,
            "worker": "apple-everest-static-il-worker-v3" if direct else WORKER_ID,
            "planSha256": declared_sha or plan_sha(transforms), "transforms": transforms}
    plan_path = destination / "plan.json"
    plan_path.write_text(json.dumps(plan, indent=2) + "\n")
    output = destination / "target.dll"
    manifest = destination / "manifest.json"
    process = run(DOTNET10, WORKER, "--target", baseline, "--output", output,
                  "--plan", plan_path, "--target-method", TARGET_METHOD, "--manifest", manifest,
                  "--runtime-dir", WORKER.parent, "--runtime-dir", MANIP.parent,
                  "--runtime-dir", baseline.parent, expect=expected)
    return output, manifest, process.stdout


def runtime_reference(scenario: str) -> int:
    executable = DOTNET_ARTIFACTS / "bin/RuntimeReference/release/RuntimeReference.dll"
    output = run(DOTNET8, executable, scenario).stdout
    marker = "APPLE_EVEREST_RUNTIME_RESULT="
    values = [line.removeprefix(marker) for line in output.splitlines() if line.startswith(marker)]
    if len(values) != 1:
        raise RuntimeError(f"pinned runtime reference did not emit one result for {scenario}")
    return int(values[0])


if ROOT.exists():
    shutil.rmtree(ROOT)
ROOT.mkdir(parents=True)

run(REPO / "scripts/bootstrap-apple-everest-host.sh")
run(DOTNET8, "run", "--project", REPO / "tools/AppleEverestBuilder/AppleEverestBuilder.csproj",
    "--", "acquire", "--profile", REPO / "apple-everest/profiles/stable-1.6458.0.json",
    "--output", UPSTREAM, cwd=pathlib.Path("/private/tmp"))
if run("git", "-C", UPSTREAM / "external/MonoMod", "rev-parse", "HEAD").stdout.strip() != MONOMOD_SHA:
    raise RuntimeError("pinned MonoMod checkout drifted")
run(DOTNET9, "build",
    UPSTREAM / "external/MonoMod/src/MonoMod.RuntimeDetour.HookGen/MonoMod.RuntimeDetour.HookGen.csproj",
    "-c", "Release", "-f", "net8.0", "-p:RestoreLockedMode=false", cwd=pathlib.Path("/private/tmp"))
MONOMOD = UPSTREAM / "external/MonoMod/artifacts/bin/MonoMod.Utils/release_net8.0/MonoMod.Utils.dll"
if not MONOMOD.is_file():
    raise RuntimeError("pinned MonoMod.Utils output is missing")

RUNTIME_DETOUR = (UPSTREAM / "external/MonoMod/artifacts/bin/MonoMod.RuntimeDetour.HookGen/"
                  "release_net8.0/MonoMod.RuntimeDetour.dll")
if not RUNTIME_DETOUR.is_file():
    raise RuntimeError("pinned MonoMod.RuntimeDetour output is missing")

run(DOTNET8, "restore", TEST / "Reference/Reference.csproj", "--locked-mode",
    "--artifacts-path", DOTNET_ARTIFACTS,
    f"-p:MonoModUtilsPath={MONOMOD}", cwd=pathlib.Path("/private/tmp"))
run(DOTNET8, "build", TEST / "Reference/Reference.csproj", "-c", "Release", "--no-restore",
    "--artifacts-path", DOTNET_ARTIFACTS,
    f"-p:MonoModUtilsPath={MONOMOD}", cwd=pathlib.Path("/private/tmp"))
run(DOTNET8, "restore", TEST / "RuntimeReference/RuntimeReference.csproj", "--locked-mode",
    "--artifacts-path", DOTNET_ARTIFACTS,
    f"-p:MonoModRuntimeDetourPath={RUNTIME_DETOUR}", f"-p:MonoModUtilsPath={MONOMOD}",
    cwd=pathlib.Path("/private/tmp"))
run(DOTNET8, "build", TEST / "RuntimeReference/RuntimeReference.csproj", "-c", "Release", "--no-restore",
    "--artifacts-path", DOTNET_ARTIFACTS,
    f"-p:MonoModRuntimeDetourPath={RUNTIME_DETOUR}", f"-p:MonoModUtilsPath={MONOMOD}",
    cwd=pathlib.Path("/private/tmp"))
run(DOTNET8, "build", TEST / "Target/Target.csproj", "-c", "Release",
    "--artifacts-path", DOTNET_ARTIFACTS, cwd=pathlib.Path("/private/tmp"))
run(DOTNET8, "build", TEST / "Runner/Runner.csproj", "-c", "Release",
    "--artifacts-path", DOTNET_ARTIFACTS, cwd=pathlib.Path("/private/tmp"))
run(DOTNET10, "restore", REPO / "tools/AppleEverestIlWorker/AppleEverestIlWorker.csproj",
    "--locked-mode", "--artifacts-path", DOTNET_ARTIFACTS,
    f"-p:MonoModUtilsPath={MONOMOD}")
run(DOTNET10, "build", REPO / "tools/AppleEverestIlWorker/AppleEverestIlWorker.csproj",
    "-c", "Release", "--no-restore", "--artifacts-path", DOTNET_ARTIFACTS,
    f"-p:MonoModUtilsPath={MONOMOD}")

BASELINE = DOTNET_ARTIFACTS / "bin/Target/release/AppleEverestIlComposeTarget.dll"
MANIP = DOTNET_ARTIFACTS / "bin/Manipulators/release/AppleEverestIlComposeManipulators.dll"
WORKER = DOTNET_ARTIFACTS / "bin/AppleEverestIlWorker/release/AppleEverestIlWorker.dll"
MANIP_SHA = sha(MANIP)

ref_a, data_a = reference("A", BASELINE, "A")
ref_b, data_b = reference("B", BASELINE, "B")
ref_ab, data_ab = reference("A,B", BASELINE, "AB")
ref_ba, data_ba = reference("B,A", BASELINE, "BA")
ref_abc, data_abc = reference("A,B,C", BASELINE, "ABC")
ref_lambda, data_lambda = reference("L", BASELINE, "Lambda")
if data_ab["finalSha256"] == data_ba["finalSha256"]:
    raise RuntimeError("project-owned A→B and B→A sequences did not differ")

step_a = data_a["steps"][0]
step_b = data_b["steps"][0]
steps_ab = data_ab["steps"]
steps_ba = data_ba["steps"]
plans = {
    "A": [transform("FixtureA", "AddThree", 0, step_a)],
    "B": [transform("FixtureB", "MultiplyFive", 0, step_b)],
    "AB": [transform("FixtureA", "AddThree", 0, steps_ab[0]),
           transform("FixtureB", "MultiplyFive", 1, steps_ab[1])],
    "BA": [transform("FixtureB", "MultiplyFive", 0, steps_ba[0]),
           transform("FixtureA", "AddThree", 1, steps_ba[1])],
    "ABC": [transform("FixtureA", "AddThree", 0, data_abc["steps"][0]),
            transform("FixtureB", "MultiplyFive", 1, data_abc["steps"][1]),
            transform("FixtureC", "SubtractSeven", 2, data_abc["steps"][2])],
}
expected = {"A": 11, "B": 40, "AB": 55, "BA": 43, "ABC": 48}
runner = DOTNET_ARTIFACTS / "bin/Runner/release/Runner.dll"
run(DOTNET8, runner, BASELINE, 8)
for name in ("A", "B", "AB", "BA", "ABC"):
    output, manifest, _ = worker(name, BASELINE, plans[name])
    run(DOTNET8, runner, output, expected[name])
    actual = json.loads(manifest.read_text())
    reference_data = {"A": data_a, "B": data_b, "AB": data_ab, "BA": data_ba,
                      "ABC": data_abc}[name]
    if actual["afterSha256"] != reference_data["finalSha256"]:
        raise RuntimeError(f"worker {name} did not match pinned desktop MonoMod")

# Exercise the actual pinned desktop RuntimeDetour implementation. HookGen IL
# events enter through HookEndpointManager.Modify, direct hooks use the exact
# ILHook(MethodBase, Manipulator) constructor, and the On-style wrapper enters
# through HookEndpointManager.Add. The Apple side freezes the observed order
# into the same worker plan and therefore needs no runtime detour backend.
runtime_results = {
    "eventThenDirect": runtime_reference("event-direct"),
    "directThenEvent": runtime_reference("direct-event"),
    "directThenOn": runtime_reference("direct-on"),
    "eventEventDirectThenOn": runtime_reference("event-event-direct-on"),
}
if runtime_results != {
    "eventThenDirect": expected["AB"],
    "directThenEvent": expected["BA"],
    "directThenOn": expected["B"] + 100,
    "eventEventDirectThenOn": expected["ABC"] + 100,
}:
    raise RuntimeError("pinned direct/event/On registration semantics drifted")

direct_composition_plans = {
    "event-direct": [
        transform("FixtureA", "AddThree", 0, steps_ab[0]),
        transform("FixtureB", "MultiplyFive", 1, steps_ab[1], mechanism="DIRECT_ILHOOK"),
    ],
    "direct-event": [
        transform("FixtureB", "MultiplyFive", 0, steps_ba[0], mechanism="DIRECT_ILHOOK"),
        transform("FixtureA", "AddThree", 1, steps_ba[1]),
    ],
    "direct-on-underlying": [
        transform("FixtureB", "MultiplyFive", 0, step_b, mechanism="DIRECT_ILHOOK"),
    ],
    "event-event-direct-on-underlying": [
        transform("FixtureA", "AddThree", 0, data_abc["steps"][0]),
        transform("FixtureB", "MultiplyFive", 1, data_abc["steps"][1]),
        transform("FixtureC", "SubtractSeven", 2, data_abc["steps"][2], mechanism="DIRECT_ILHOOK"),
    ],
}
direct_expected = {
    "event-direct": expected["AB"],
    "direct-event": expected["BA"],
    "direct-on-underlying": runtime_results["directThenOn"] - 100,
    "event-event-direct-on-underlying": runtime_results["eventEventDirectThenOn"] - 100,
}
for name, direct_plans in direct_composition_plans.items():
    output, manifest, _ = worker("direct-compose-" + name, BASELINE, direct_plans)
    run(DOTNET8, runner, output, direct_expected[name])
    evidence = json.loads(manifest.read_text())
    if evidence["schemaVersion"] != 3 or evidence["worker"] != "apple-everest-static-il-worker-v3":
        raise RuntimeError("mixed direct composition did not use the bounded v3 worker")

# Pinned desktop MonoMod executes a compiler-generated singleton lambda as a
# normal Func<int,int>. The production worker lowers the host dynamic cell to
# an explicit singleton load + direct virtual call, preserving result/stack
# semantics without carrying the host closure instance onto Apple devices.
lambda_plan = transform("FixtureLambda", "SingletonAddEleven", 0, data_lambda["steps"][0])
lambda_plan["afterSha256"] = "3f89ce71ed44776714821ba3b2148fb1cf9c0d20a88a99758d317a6df28fb542"
lambda_plan["diffSha256"] = "60afc4e8e9e7c4a5b448006a3ab65f4cc2cfa53c7cf6753394543166ff61df62"
lambda_plan["expectedDelegateTargets"] = [
    "AppleEverest.IlCompose.SequenceManipulators+<>c::<SingletonAddEleven>b__3_1"
]
lambda_output, lambda_manifest, _ = worker("compiler-singleton-lambda", BASELINE, [lambda_plan])
lambda_evidence = json.loads(lambda_manifest.read_text())
lowerings = lambda_evidence["steps"][0]["DelegateLowerings"]
desktop_lambda_il = data_lambda["steps"][0]["afterNormalizedIl"]
if "DynamicReferenceManager" not in desktop_lambda_il or lowerings != [{
    "Kind": "compiler-singleton-noncapturing",
    "Target": "AppleEverest.IlCompose.SequenceManipulators+<>c::<SingletonAddEleven>b__3_1",
    "ParameterCount": 1,
}]:
    raise RuntimeError("compiler-singleton EmitDelegate semantics/lowering drifted")

determinism: list[tuple[str, str]] = []
for index in range(3):
    output, manifest, _ = worker(f"AB-determinism-{index}", BASELINE, plans["AB"])
    evidence = json.loads(manifest.read_text())
    evidence.pop("outputSha256", None)
    determinism.append((sha(output), text_sha(json.dumps(evidence, sort_keys=True))))
if len(set(determinism)) != 1:
    raise RuntimeError("three-run A→B frozen output/evidence was nondeterministic")

bad_ordinal = [dict(plans["AB"][0]), dict(plans["AB"][1])]
bad_ordinal[1]["registrationOrdinal"] = 2
worker("reject-gap", BASELINE, bad_ordinal, expected=1)
duplicate = [dict(plans["AB"][0]), dict(plans["AB"][0])]
duplicate[1]["registrationOrdinal"] = 1
worker("reject-duplicate", BASELINE, duplicate, expected=1)
no_op_step = dict(step_a)
no_op = [transform("FixtureA", "NoOp", 0, no_op_step)]
no_op[0]["afterSha256"] = no_op[0]["beforeSha256"]
no_op[0]["diffSha256"] = text_sha("")
worker("reject-no-op", BASELINE, no_op, expected=1)
worker("reject-plan-hash", BASELINE, plans["AB"], expected=1, declared_sha="0" * 64)
worker("reject-altered-baseline", ref_b, plans["AB"], expected=1)
invalid = [transform("FixtureInvalid", "DanglingBranch", 0, step_a)]
invalid[0]["afterSha256"] = "1" * 64
invalid[0]["diffSha256"] = "2" * 64
worker("reject-invalid-intermediate", BASELINE, invalid, expected=1)

# A rejected worker invocation must not poison the subsequent fresh target worker.
isolated, _, _ = worker("post-rejection-isolation", BASELINE, plans["AB"])
run(DOTNET8, runner, isolated, expected["AB"])

if any(marker in isolated.read_bytes() for marker in
       (b"MonoMod.Cil", b"Mono.Cecil", b"ILContext", b"DynamicReferenceManager")):
    raise RuntimeError("frozen conformance target retained a host transformation dependency")

summary = {
    "schemaVersion": 1,
    "monoModCommit": MONOMOD_SHA,
    "targetMethod": TARGET_METHOD,
    "manipulatorAssemblySha256": MANIP_SHA,
    "baselineSha256": data_ab["baselineSha256"],
    "aSha256": steps_ab[0]["afterSha256"],
    "abSha256": data_ab["finalSha256"],
    "bSha256": steps_ba[0]["afterSha256"],
    "baSha256": data_ba["finalSha256"],
    "abcSha256": data_abc["finalSha256"],
    "aThenBResult": expected["AB"],
    "bThenAResult": expected["BA"],
    "aThenBThenCResult": expected["ABC"],
    "maximumSequenceLength": 3,
    "directIlHookComposition": {
        "desktopRegistrationResults": runtime_results,
        "eventThenDirectAppleResult": direct_expected["event-direct"],
        "directThenEventAppleResult": direct_expected["direct-event"],
        "directThenOnUnderlyingAppleResult": direct_expected["direct-on-underlying"],
        "eventEventDirectThenOnUnderlyingAppleResult":
            direct_expected["event-event-direct-on-underlying"],
        "onWrapperDelta": 100,
        "desktopEntryPoints": [
            "HookEndpointManager.Modify",
            "ILHook(MethodBase,ILContext.Manipulator)",
            "HookEndpointManager.Add",
        ],
        "appleRuntimeBackend": False,
    },
    "compilerSingletonLambda": {
        "desktopReference": "pinned MonoMod dynamic-cell shape",
        "expectedResultAfterProductionPublicization": 19,
        "executionCoverage": "real DisposableTheo managed compile and full-AOT product gate",
        "afterSha256": lambda_plan["afterSha256"],
        "diffSha256": lambda_plan["diffSha256"],
        "lowering": lowerings[0],
    },
    "threeRunDeterminism": True,
    "negativeCases": ["ordinal-gap", "duplicate", "no-op", "plan-hash", "altered-baseline",
                      "invalid-intermediate"],
}
(ROOT / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
print("PASS: pinned H-B sequence plus H-D direct/event/On composition, order, locks, rejection, and isolation")
