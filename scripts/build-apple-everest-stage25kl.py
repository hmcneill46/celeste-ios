#!/usr/bin/env python3
"""Build the unchanged two-map SJ product with mandatory four-gate preflight."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[1]


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package-root",required=True,type=Path)
    parser.add_argument("--chrono-package",required=True,type=Path)
    args,product_args=parser.parse_known_args()
    valued={"--platform","--signing","--team-id","--ios-bundle-id","--tvos-bundle-id","--ios-device-id","--tvos-device-id","--work-root","--output"}
    flags={"--prepare-only","--clean"};index=0
    while index<len(product_args):
        option=product_args[index]
        if option in valued:
            if index+1==len(product_args) or product_args[index+1].startswith("--"):parser.error("missing product value: "+option)
            index+=2
        elif option in flags:index+=1
        else:parser.error("K-L does not permit input/gate overrides or reuse of an unbound AOT product: "+option)
    env=dict(os.environ,MSBUILDDISABLENODEREUSE="1",DOTNET_CLI_USE_MSBUILD_SERVER="0",UseSharedCompilation="false")
    env["APPLE_EVEREST_SJ_SOURCE_PACKAGE"]=str((args.package_root/"StrawberryJam2021.zip").resolve())
    # Content selection validates a pinned core asset before the common builder
    # runs. Acquire that same pinned source first, including on a fresh checkout.
    subprocess.run([str(ROOT/"scripts/bootstrap-apple-everest-host.sh")],cwd=ROOT,env=env,check=True)
    subprocess.run([str(ROOT/".build/apple-everest/toolchain/dotnet8/dotnet"),"run","--project",
        str(ROOT/"tools/AppleEverestBuilder/AppleEverestBuilder.csproj"),"--","acquire","--profile",
        str(ROOT/"apple-everest/profiles/stable-1.6458.0.json"),"--output",
        str(ROOT/".build/apple-everest/upstream/Everest")],cwd="/private/tmp",env=env,check=True)
    spec=importlib.util.spec_from_file_location("kl_content",ROOT/"scripts/generate-apple-everest-stage25kl-content.py")
    generator=importlib.util.module_from_spec(spec);sys.modules[spec.name]=generator;spec.loader.exec_module(generator)
    generated,_=generator.generate(args.package_root)
    plan=ROOT/"apple-everest/sj-beginner-content-stage25kl.json"
    if generated!=json.loads(plan.read_text()):parser.error("regenerated exact source content plan differs from tracked authority")
    profiles=ROOT/"apple-everest/sj-factory-authored-profiles-stage25kj.json"
    providers={row["provider"] for row in json.loads(profiles.read_text())["factories"]}-{"EverestCore"}
    inputs=[args.package_root/(name+".zip") for name in sorted(providers)]
    inputs += [args.package_root/(name+".zip") for name in ["StrawberryJam2021Assets","StrawberryJam2021AudioA","StrawberryJam2021AudioB"]]
    inputs += [args.chrono_package]
    inputs += [ROOT/"apple-everest/canaries"/name for name in ["stage25ke","stage25kf","stage25kh","stage25kj","stage25kj-interactions"]]
    for path in inputs:
        if not path.exists():parser.error("required exact source missing: "+str(path))
    command=[str(ROOT/"scripts/build-apple-everest-canary.sh"),"--work-root",str(ROOT/".build/apple-everest/stage25kl/product"),
             "--output",str(ROOT/"artifacts/apple-everest/stage25kl"),"--factory-preflight",str(ROOT/"apple-everest/selected-factory-type-closure-stage25kh.json"),
             "--authored-factory-profiles",str(profiles),"--content-plan",str(plan)]
    for path in inputs:command += ["--mod",str(path.resolve())]
    # Signing/device identities remain local and are not echoed in exceptions.
    raise SystemExit(subprocess.run(command+product_args,cwd=ROOT,env=env).returncode)


if __name__=="__main__":main()
