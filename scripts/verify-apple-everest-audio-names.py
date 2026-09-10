#!/usr/bin/env python3
"""Execute generated Audio/AudioState methods with the real custom-name registry.

Only FMOD and unused graphics/diagnostic boundaries use owned synthetic data.
No bank, proprietary source, generated assembly or physical PASS is tracked.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
METHODS = ("GetEventName", "SetMusic", "SetAmbience", "Stop", "SetParameter",
           "CreateInstance", "AppleEverestOriginal_CreateInstance", "GetEventDescription")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def extract(source, name):
    matches = list(re.finditer(r"^\t(?:public|private) static [^\n]+\b" + re.escape(name) + r"\([^\n]*\)\n\t\{\n", source, re.M))
    if len(matches) != 1:
        raise ValueError("one exact generated Audio method required: " + name)
    start = matches[0].start()
    end = source.find("\n\t}\n", matches[0].end())
    if end < 0:
        raise ValueError("unsupported generated Audio method shape: " + name)
    return source[start:end + 4]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runtime", type=Path, required=True)
    parser.add_argument("--work-root", type=Path, required=True)
    args = parser.parse_args();work = args.work_root.resolve()
    if work.exists() or work == ROOT:
        raise ValueError("fresh owned audio probe root required")
    if any(p.is_symlink() for p in (args.work_root.absolute(), *args.work_root.absolute().parents)):
        raise ValueError("symlink output ancestor")
    if ROOT in work.parents:
        subprocess.run(["git", "check-ignore", "--no-index", "-q", "--",
                        str((work / ".audio-name-probe").relative_to(ROOT))], cwd=ROOT, check=True)
    managed = args.runtime.resolve() / "Celeste"
    runtime = managed / "Mod/AppleEverestStatic"
    paths = {name: managed / name for name in ("Audio.cs", "AudioState.cs", "AudioTrackState.cs", "MEP.cs")}
    for name in ("AppleEverestCustomAudioRuntime.cs", "AppleEverestCustomAudioLifecycle.cs"):
        paths[name] = runtime / name
        if sha(paths[name]) != sha(ROOT / "apple-everest/runtime" / name):
            raise ValueError("generated custom audio source differs from current production source")
    before = {name: sha(path) for name, path in paths.items()}
    methods = {name: extract(paths["Audio.cs"].read_text(), name) for name in METHODS}
    work.mkdir(parents=True);(work / ".audio-name-probe").touch()
    for name, path in paths.items():
        if name != "Audio.cs": shutil.copyfile(path, work / name)
    tests = ROOT / "tools/AppleEverestBuilder/tests"
    for name in ("AudioNameRuntimeFixture", "AudioNameRuntimeProgram"):
        shutil.copyfile(tests / (name + ".cs.txt"), work / (name + ".cs"))
    (work / "Audio.cs").write_text("using System;\nusing FMOD;\nusing FMOD.Studio;\nusing Microsoft.Xna.Framework;\nnamespace Celeste;\npublic static partial class Audio\n{\n" +
        "\n".join(methods.values()) + "\n}\n")
    (work / "AudioProbe.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
        '<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>'
        '<Nullable>disable</Nullable><UseSharedCompilation>false</UseSharedCompilation><BuildInParallel>false</BuildInParallel>'
        '</PropertyGroup></Project>\n')
    env = dict(os.environ, MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_USE_MSBUILD_SERVER="0", UseSharedCompilation="false")
    with (work / "commands.private.log").open("xb") as log:
        subprocess.run(["dotnet", "build", str(work / "AudioProbe.csproj"), "-c", "Release", "-m:1",
                        "-p:BuildInParallel=false", "-p:UseSharedCompilation=false", "--nologo"],
                       cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
        subprocess.run(["dotnet", str(work / "bin/Release/net10.0/AudioProbe.dll"), str(work / "result.json")],
                       cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
    if before != {name: sha(path) for name, path in paths.items()}:
        raise ValueError("source changed during audio name probe")
    result = json.loads((work / "result.json").read_text())
    if result["status"] != "PASS" or result["reloads"] != 50:
        raise ValueError("audio execution proof incomplete")
    report = {"status": "PASS_SOURCE_BOUND_AUDIO_NAMES", "sourceSha256": before,
        "methodSha256": {name: hashlib.sha256(value.encode()).hexdigest() for name, value in methods.items()},
        "probeSourceSha256": {name: sha(tests / (name + ".cs.txt")) for name in ("AudioNameRuntimeFixture", "AudioNameRuntimeProgram")},
        "scriptSha256": sha(Path(__file__)), "result": result, "originalSourcesUnchanged": True}
    (work / "source-bound-result.json").write_text(json.dumps(report, sort_keys=True, indent=2) + "\n")
    print("PASS: actual generated Audio/AudioState and production name registry; 50 reloads; explicit FMOD fixture boundary")


if __name__ == "__main__":
    main()
