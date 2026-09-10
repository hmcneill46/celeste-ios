#!/usr/bin/env python3
"""Build exactly lobby + Bing + unchanged snas with mandatory expanded gates."""
from __future__ import annotations
import argparse
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / filename)
    value = importlib.util.module_from_spec(spec);sys.modules[name] = value;spec.loader.exec_module(value)
    return value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package-root", type=Path, required=True)
    parser.add_argument("--chrono-package", type=Path, required=True)
    parser.add_argument("--work-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args, product_args = parser.parse_known_args()
    valued = {"--platform", "--signing", "--team-id", "--ios-bundle-id", "--tvos-bundle-id", "--ios-device-id", "--tvos-device-id"}
    index = 0;seen = set()
    while index < len(product_args):
        option = product_args[index]
        if option in seen: parser.error("duplicate product option: " + option)
        seen.add(option)
        if option in valued:
            if index + 1 == len(product_args) or product_args[index+1].startswith("--"): parser.error("missing product option value")
            index += 2
        elif option == "--prepare-only": index += 1
        else: parser.error("K-N requires fresh products and permits no input or gate override: " + option)
    if "--prepare-only" not in product_args:
        if subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True).strip():
            parser.error("final K-N AOT products require the clean committed tooling/game revision")
        if int(ET.parse(ROOT / "modern-ios/IOSPortVersion.props").findtext(".//IOSPortBuildNumber")) <= 46:
            parser.error("expanded K-N products require a new canonical build identity")
        module("kn_frozen_authority", "verify-apple-everest-stage25kn-product-content.py").frozen_authority()
    for value, base in ((args.work_root, ROOT / ".build/apple-everest"), (args.output, ROOT / "artifacts/apple-everest")):
        if value.resolve() == base or not value.resolve().is_relative_to(base) or value.exists():
            parser.error("K-N requires a new isolated output under the supported owned root")
        if any(parent.is_symlink() for parent in (value.absolute(), *value.absolute().parents)):
            parser.error("symlink output ancestor")
        subprocess.run(["git", "check-ignore", "--no-index", "-q", "--", str((value.resolve()/".stage25kn-inputs").relative_to(ROOT))], cwd=ROOT, check=True)
    env = dict(os.environ, MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_USE_MSBUILD_SERVER="0", UseSharedCompilation="false", PYTHONDONTWRITEBYTECODE="1")
    env["APPLE_EVEREST_SJ_SOURCE_PACKAGE"] = str((args.package_root / "StrawberryJam2021.zip").resolve())
    subprocess.run([str(ROOT / "scripts/bootstrap-apple-everest-host.sh")], cwd=ROOT, env=env, check=True)
    subprocess.run([str(ROOT / ".build/apple-everest/toolchain/dotnet8/dotnet"), "run", "--project",
        str(ROOT / "tools/AppleEverestBuilder/AppleEverestBuilder.csproj"), "--", "acquire", "--profile",
        str(ROOT / "apple-everest/profiles/stable-1.6458.0.json"), "--output", str(ROOT / ".build/apple-everest/upstream/Everest")], cwd="/private/tmp", env=env, check=True)
    profiles_module = module("kn_wrapper_profiles", "generate-apple-everest-stage25kn-profiles.py")
    content_module = module("kn_wrapper_content", "generate-apple-everest-stage25kn-content.py")
    profiles = profiles_module.extract(args.package_root / "StrawberryJam2021.zip")
    content, _ = content_module.generate(args.package_root)
    if profiles_module.guard(profiles) != (ROOT / "apple-everest/runtime/semantics/AppleEverestSnasProfileGuard.cs").read_text():
        parser.error("production guard differs from exact original map extraction")
    args.work_root.mkdir(parents=True);(args.work_root / ".stage25kn-inputs").touch()
    profile_path = args.work_root / "authored-profiles.private.json"
    plan_path = args.work_root / "content-plan.json"
    profile_path.write_bytes(profiles_module.serialized(profiles))
    plan_path.write_text(json.dumps(content, indent=2, ensure_ascii=False)+"\n")
    providers = {row["provider"] for row in profiles["factories"]} - {"EverestCore"}
    inputs = [args.package_root / (name + ".zip") for name in sorted(providers)]
    inputs += [args.package_root / (name + ".zip") for name in ("StrawberryJam2021Assets", "StrawberryJam2021AudioA", "StrawberryJam2021AudioB")]
    inputs += [args.chrono_package]
    inputs += [ROOT / "apple-everest/canaries" / name for name in ("stage25ke", "stage25kf", "stage25kh", "stage25kj", "stage25kj-interactions")]
    if any(not path.exists() for path in inputs): parser.error("exact required source input missing")
    command = [str(ROOT / "scripts/build-apple-everest-canary.sh"), "--work-root", str(args.work_root.resolve()),
        "--output", str(args.output.resolve()), "--factory-preflight", str(ROOT / "apple-everest/sj-snas-factory-contract-stage25kn.json"),
        "--authored-factory-profiles", str(profile_path.resolve()), "--content-plan", str(plan_path.resolve()),
        "--stage25kn-package-root", str(args.package_root.resolve())]
    for path in inputs: command += ["--mod", str(path.resolve())]
    # Local signing/device values remain private command arguments, never a
    # tracked input authority or an exception containing the full command.
    raise SystemExit(subprocess.run(command + product_args, cwd=ROOT, env=env).returncode)


if __name__ == "__main__":
    main()
