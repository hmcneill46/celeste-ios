#!/usr/bin/env python3
"""Run current K-N host regressions without native stress or app AOT."""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work-root", type=Path, required=True)
    parser.add_argument("--require-clean", action="store_true")
    args = parser.parse_args();work = args.work_root.resolve()
    if work.exists() or not work.is_relative_to(ROOT / ".build/apple-everest"):
        raise ValueError("fresh owned regression root below .build/apple-everest required")
    for parent in (args.work_root.absolute(), *args.work_root.absolute().parents):
        if parent.is_symlink(): raise ValueError("symlink output ancestor")
    subprocess.run(["git", "check-ignore", "--no-index", "-q", "--", str((work / ".stage25kn-regressions").relative_to(ROOT))], cwd=ROOT, check=True)
    def git(*args): return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()
    revision = git("rev-parse", "HEAD");status = git("status", "--porcelain")
    if args.require_clean and status: raise ValueError("final regression requires a clean committed revision")
    work.mkdir(parents=True);(work / ".stage25kn-regressions").touch()
    env = dict(os.environ, MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_USE_MSBUILD_SERVER="0", UseSharedCompilation="false", PYTHONDONTWRITEBYTECODE="1")
    records = []
    def run(name, command, cwd=ROOT, sdk=None):
        print("K-N regression: " + name, flush=True)
        selected = dict(env)
        if sdk: selected["DOTNET_ROOT"] = str(sdk.parent)
        logpath = work / (name + ".private.log")
        with logpath.open("xb") as log:
            result = subprocess.run(list(map(str, command)), cwd=cwd, env=selected, stdout=log, stderr=subprocess.STDOUT)
        records.append({"name": name, "exitCode": result.returncode, "logSha256": hashlib.sha256(logpath.read_bytes()).hexdigest()})
        if result.returncode: raise ValueError("current regression failed: " + name)
    for name, pattern in [("host-b-portable-native", "test_native_*.py"),
                          ("host-c-aot-host-signing", "test_apple_aot_host_verification.py"),
                          ("historical-km-portable-audit", "test_stage25km_audit.py")]:
        run(name, ["python3", "-m", "unittest", "discover", "-s", "tests", "-p", pattern, "-v"])
    run("kn-contract-negative-controls", ["python3", ROOT / "scripts/test-apple-everest-stage25kn-controls.py"])
    d8 = ROOT / ".build/apple-everest/toolchain/dotnet8/dotnet"
    d10 = ROOT / ".build/apple-everest/toolchain/dotnet10/dotnet"
    def project(name, path, sdk, arguments=()):
        output = work / (name + "-artifacts")
        run(name + "-build", [sdk, "build", ROOT / path, "-c", "Release", "--nologo", "-m:1",
            "-p:BuildInParallel=false", "-p:UseSharedCompilation=false", "--artifacts-path", output], cwd=Path("/private/tmp"), sdk=sdk)
        dlls = list((output / "bin").rglob(name + ".dll"))
        if len(dlls) != 1: raise ValueError("one exact test assembly required: " + name)
        run(name, [sdk, dlls[0], *arguments], sdk=sdk)
    project("AppleEverestBuilder.Tests", "tools/AppleEverestBuilder/tests/AppleEverestBuilder.Tests.csproj", d8, [ROOT])
    project("AppleEverestHookSemantics.Tests", "tools/AppleEverestBuilder/tests/HookSemantics/AppleEverestHookSemantics.Tests.csproj", d8)
    run("desktop-hookgen", [ROOT / "scripts/test-apple-everest-desktop-hookgen.sh"])
    for name in ["SaveManagerPairingTests", "SaveManagerProtocolTests", "SaveManagerContinuityTests", "SoftReloadTests"]:
        project(name, "tvos/" + name + "/" + name + ".csproj", d10)
    run("canonical-input-profile-contracts", ["python3", ROOT / "scripts/verify-celeste-input-profiles.py", "--repo-root", ROOT])
    run("shell-syntax", ["bash", "-n", ROOT / "scripts/build-apple-everest-canary.sh"])
    run("diff-whitespace", ["git", "diff", "--check"])
    run("recursive-submodules", ["git", "submodule", "foreach", "--recursive", "--quiet", 'test -z "$(git status --porcelain)"'])
    if git("rev-parse", "HEAD") != revision or git("status", "--porcelain") != status:
        raise ValueError("source state changed during current regressions")
    report = {"schemaVersion": 1, "stage": "25K-N", "status": "PASS_CURRENT_HOST_REGRESSIONS",
        "sourceCommit": revision, "sourceTreeDirty": bool(status), "results": records,
        "nativeStressBuilds": False, "appAot": False, "signingOrDeviceOperations": False,
        "historicalAuthorities": "K-L/K-M literal-count authorities remain unchanged and do not certify expanded gameplay.",
        "physicalAcceptance": "SEPARATE_EXACT_PRODUCT_OBSERVATIONS_REQUIRED"}
    (work / "regression-evidence.json").write_text(json.dumps(report, indent=2, sort_keys=True)+"\n")
    print("PASS: current native-tooling/AOT-host/closure/hook/storage/soft-reload host regressions; no app AOT or physical claim")


if __name__ == "__main__":
    main()
