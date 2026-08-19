#!/usr/bin/env python3
"""Verify the Stage 25C real Everest ZIP/static-AOT compatibility ladder."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess
import tempfile
import zipfile

BASE = "b033c76d73b93e4f9b6b37c4f10458561b44f83e"
EVEREST = "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00"
MONOMOD = "dfc30a1506d37fb88a2c2be004f525205f46a24c"
IOS_RC = "27e16b4724d94d3991b99c4795f680fcb0e5830c"
RC1 = "ee52b0868df091746f134d95d4f020f94f23d4fb"
RC2 = "641e86e4ed164cdf93f602ce2f11436449654d6e"
RC3 = "c8134c8ca7924cf12f48527e714b5242c6024927"
IOS_NATIVE = "9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2"
TVOS_NATIVE = "6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc"
CONTENT = "30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46"
RAW = "db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273"
PATCHED = "0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5"
STAGE6 = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"
VANILLA_IOS = "2f5d6fffaa151530f48deac7ff402c2c6d475b20eeef5579f1db784d57da2357"
CLOSURE = "9c971fe1b84e80092a8d9178bbae9cf0f9588e185f85c6c4c61996775f4ec532"
CONTENT_MOD_ZIP = "37eaa16b6b2d458a8ce27e08ea315b4cc3c2938d6b3c2292c61203c641ad7c95"
CODE_MOD_ZIP = "f9cf8874acbfaff87af22098caea36429c97941d0b13e1114d8749be61afbe19"
CODE_MOD_DLL = "eac2cb52112dafcb31f86cdad0f3373bb781a7f00e705394fb811c0f7ef08c17"
CODE_MOD_FROZEN = "3c8f459ec016c233f35a9ae723cd03035b745855867747dde53d5c26ed3157f0"
IL_MOD_ZIP = "ef5071ad28ed27ee73749623f4484a511f4793388f53417a4c0ee36c3d6e4a7b"
GOLDEN_TRAINER_ZIP = "2a39b5bb9524aeac510ff1e0835022a294081784982aca0bf3970fb8e867fc5b"


class Checks:
    def __init__(self) -> None:
        self.count = 0

    def require(self, condition: bool, message: str) -> None:
        if not condition:
            raise SystemExit("FAIL: " + message)
        self.count += 1


def read(root: pathlib.Path, relative: str) -> str:
    return (root / relative).read_text(errors="strict")


def git(root: pathlib.Path, *arguments: str) -> str:
    return subprocess.check_output(["git", "-C", str(root), *arguments], text=True).strip()


def sha(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def verify_package(ipa: pathlib.Path, platform: str, c: Checks) -> None:
    with tempfile.TemporaryDirectory() as temporary:
        with zipfile.ZipFile(ipa) as archive:
            c.require(archive.testzip() is None, f"{platform} ZIP integrity")
            archive.extractall(temporary)
        apps = list((pathlib.Path(temporary) / "Payload").glob("*.app"))
        c.require(len(apps) == 1, f"{platform} one app")
        app = apps[0]
        info = plistlib.loads((app / "Info.plist").read_bytes())
        c.require("everestcanary" in str(info.get("CFBundleIdentifier", "")).lower(), f"{platform} isolated identity")
        executable = app / str(info.get("CFBundleExecutable", ""))
        c.require(executable.is_file() and "arm64" in subprocess.check_output(["file", str(executable)], text=True), f"{platform} arm64")
        c.require((app / "ParticlePaletteHelper.dll").is_file(), f"{platform} transformed external assembly")
        c.require((app / "ParticlePaletteHelper.aotdata.arm64").is_file(), f"{platform} external assembly AOT data")
        c.require((app / "Celeste.aotdata.arm64").is_file(), f"{platform} Celeste AOT data")
        names = [path.name for path in app.rglob("*") if path.is_file()]
        forbidden = ("MMHOOK", "RuntimeDetour", "NativeDetour", "NLua", "KeraLua")
        c.require(not any(any(token.lower() in name.lower() for token in forbidden) for name in names), f"{platform} forbidden runtime members absent")
        build = json.loads((ipa.parent / "build-manifest.json").read_text())
        c.require(build["sharedClosureSha256"] == CLOSURE, f"{platform} exact shared closure")
        c.require(build["fullAOT"] and build["fullTrim"] and not build["useInterpreter"] and not build["jit"], f"{platform} full AOT contract")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--closure", type=pathlib.Path)
    parser.add_argument("--fixtures", type=pathlib.Path)
    parser.add_argument("--ios-ipa", type=pathlib.Path)
    parser.add_argument("--tvos-ipa", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    c = Checks()

    required = [
        "apple-everest/real-mod-compatibility-stage25c.json",
        "tools/AppleEverestBuilder/AssemblyFreezer.cs",
        "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs",
        "tools/AppleEverestBuilder/ClosureGenerator.cs",
        "tools/AppleEverestBuilder/ContentCompiler.cs",
        "apple-everest/runtime/AppleEverestHookList.cs",
        "apple-everest/runtime/EverestLoggerStaticApi.cs",
        "apple-everest/runtime/OnParticleStaticDispatch.cs",
        "apple-everest/runtime/OnTrailStaticDispatch.cs",
        "scripts/audit-apple-everest-mods.sh",
        "scripts/build-apple-everest-real-mods.sh",
        "scripts/fetch-apple-everest-stage25c-fixtures.sh",
        "docs/APPLE_EVEREST_STATIC_AOT.md",
        "docs/APPLE_EVEREST_COMPATIBILITY.md",
        "docs/history/stages/APPLE_EVEREST_REAL_MODS_STAGE25C_REPORT.md",
    ]
    for relative in required:
        c.require((root / relative).is_file(), f"required {relative}")

    fixture_fetch = read(root, "scripts/fetch-apple-everest-stage25c-fixtures.sh")
    c.require(
        'local name="$1" sha="$2" url="$3"\n  local destination="$OUTPUT/$name"' in fixture_fetch,
        "fixture downloader initializes derived paths after local names under set -u",
    )

    registry = json.loads(read(root, required[0]))
    c.require(registry["schemaVersion"] == 1 and registry["auditedCandidates"] >= 8, "candidate registry schema/count")
    c.require(registry["everestProfile"] == "apple-everest-stable-1.6458.0-v1", "pinned Everest profile")
    c.require(registry["transformerVersion"] == "apple-everest-static-v2", "binary transformer version")
    c.require(registry["selectedClosure"] == ["IAccidentallyFourCassetteBlocks@1.0.0", "ParticlePaletteHelper@1.0.0"], "exact selected real ZIPs")
    by_name = {item["name"]: item for item in registry["fixtures"]}
    c.require(len(by_name) == 14, "fourteen distinct audited candidates")
    c.require(by_name["IAccidentallyFourCassetteBlocks"]["zipSha256"] == CONTENT_MOD_ZIP and by_name["IAccidentallyFourCassetteBlocks"]["result"] == "SUPPORTED", "real content fixture pin")
    c.require(by_name["ParticlePaletteHelper"]["zipSha256"] == CODE_MOD_ZIP and by_name["ParticlePaletteHelper"]["result"] == "SUPPORTED_WITH_STATIC_TRANSFORM", "real code fixture pin")
    c.require(by_name["DashlessDreamBlocks"]["zipSha256"] == IL_MOD_ZIP and by_name["DashlessDreamBlocks"]["result"] == "DEFERRED_DIRECT_HOOK", "real direct-hook fixture")
    c.require(by_name["GoldenTrainer"]["zipSha256"] == GOLDEN_TRAINER_ZIP and by_name["GoldenTrainer"]["result"] == "DEFERRED_IL", "real IL.* event fixture")
    c.require(by_name["ExtendedVariantMode"]["result"] == "UNSUPPORTED_LUA", "real Lua negative")
    c.require(by_name["SmoothCeleste"]["result"] == "UNSUPPORTED_NATIVE", "real native negative")
    c.require(all(item.get("license") for item in registry["fixtures"]), "every fixture has a license finding")

    models = read(root, "tools/AppleEverestBuilder/Models.cs")
    c.require("apple-everest-static-v2" in models and "FrozenAssemblyRecord" in models, "v2 provenance model")
    freezer = read(root, "tools/AppleEverestBuilder/AssemblyFreezer.cs")
    for token in ("Mono.Cecil", "EverestModule", "MMHOOK_Celeste", "Celeste", "CustomEntityAttribute", "OriginalSha256", "FrozenSha256"):
        c.require(token in freezer, f"assembly freezer {token}")
    c.require("Assembly.Load" not in freezer and "DynamicMethod" not in freezer, "host transform avoids dynamic execution")

    analyzer = read(root, "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs")
    for target in ("On.Celeste.Dialog", "On.Celeste.TrailManager", "On.Monocle.ParticleSystem"):
        c.require(target in analyzer, f"bounded target {target}")
    for token in ("Assembly.Load", "AssemblyLoadContext", "DynamicMethod", "Reflection.Emit", "RuntimeDetour", "NativeDetour", "DllImport", "NLua", "KeraLua", "Process.Start", "FileSystemWatcher"):
        c.require(token in analyzer, f"binary analyzer rejects {token}")
    c.require("source module requires root apple-static.json" in analyzer and "DeclaredAssemblyPath" in analyzer, "source and binary contracts remain distinct")

    generator = read(root, "tools/AppleEverestBuilder/ClosureGenerator.cs")
    for token in ("AssemblyFreezer.Freeze", "frozenAssemblies", "OriginalSha256", "FrozenSha256", "AssemblyName", "AppleEverestAssemblies", "AppleEverestExternalAssemblyRoots.props", "TrimmerRootAssembly", "<Reference Include=", "PatchParticles", "PatchTrail"):
        c.require(token in generator, f"external closure generator {token}")
    c.require("ParticlePaletteHelper" not in generator and "IAccidentallyFourCassetteBlocks" not in generator, "no selected-mod special case")
    content_compiler = read(root, "tools/AppleEverestBuilder/ContentCompiler.cs")
    c.require("NormalizeMapPackage" in content_compiler and 'magic != "CELESTE MAP"' in content_compiler and
              "input.ReadExactly(body)" in content_compiler and "writer.Write(package)" in content_compiler,
              "precompiled Everest maps receive bounded package-header normalization")

    hooks = read(root, "apple-everest/runtime/AppleEverestHookList.cs")
    particles = read(root, "apple-everest/runtime/OnParticleStaticDispatch.cs")
    trail = read(root, "apple-everest/runtime/OnTrailStaticDispatch.cs")
    c.require("Version" in hooks and hooks.count("Invalidate()") >= 3, "hook chain invalidation")
    c.require(particles.count("public static event") == 5 and particles.count("AppleEverestHookList.Version") == 5, "five cached particle overloads")
    c.require("public static event" in trail and "activeVersion" in trail, "cached trail overload")
    c.require(all(token not in particles + trail for token in ("DynamicInvoke", "System.Reflection", "object[]")), "typed hook dispatch only")

    runtime = "\n".join(path.read_text() for path in (root / "apple-everest/runtime").glob("*.cs"))
    for token in ("Assembly.Load", "AssemblyLoadContext", "Reflection.Emit", "DynamicMethod", "RuntimeDetour", "NativeDetour", "Process.Start", "FileSystemWatcher", "NLua", "KeraLua", "DllImport"):
        c.require(token not in runtime, f"device runtime excludes {token}")
    build = read(root, "scripts/build-apple-everest-canary.sh")
    c.require("scan_product_runtime" in build and "CLOSURE/assemblies" in build and
              "verify-preserved-assembly" in build, "every frozen assembly receives runtime and preservation scans")
    c.require("verify-referenced-api" in build and "verify-aot-object" in build and
              ".dll.llvm.o" in build and "mono_object" in build,
              "every frozen assembly receives linked API and complete native AOT body verification")
    c.require("UseInterpreter=false" in build and "RunAOTCompilation=true" in build and "TrimMode=full" in build, "Apple full-AOT build")
    c.require("--mod ZIP_OR_DIR" in build and "MODS+=(" in build, "repeatable ordinary mod input")
    c.require("--mods DIRECTORY" in read(root, "scripts/build-apple-everest-real-mods.sh"), "internal directory UX")
    c.require("--mods" not in read(root, "build-ios.sh") and "--mods" not in read(root, "build-tvos.sh"), "vanilla builders remain mod-free")

    api = read(root, "apple-everest/runtime/EverestStaticApi.cs")
    logger = read(root, "apple-everest/runtime/EverestLoggerStaticApi.cs")
    c.require("public static class Content" in api and "public static readonly List<ModContent> Mods" in api and
              "public EverestModuleMetadata Mod;" in api, "external binary Everest content API compatibility")
    c.require("LogInterpolatedStringHandler<TLevel>" in logger and "DefaultInterpolatedStringHandler" in logger and
              "LogLevelConstTypes" in logger, "external binary Everest interpolated logging API compatibility")

    docs = read(root, "docs/APPLE_EVEREST_STATIC_AOT.md") + read(root, "docs/APPLE_EVEREST_COMPATIBILITY.md")
    for token in ("BINARY-COMPATIBLE", "SOURCE-PORTED", "I Accidentally Four Cassette Blocks", "Particle Palette Helper", "DEFERRED_IL", "not general Everest support"):
        c.require(token.lower() in docs.lower(), f"documentation {token}")
    report = read(root, "docs/history/stages/APPLE_EVEREST_REAL_MODS_STAGE25C_REPORT.md")
    c.require("PASS — GREEN" in report and BASE in report and CLOSURE in report,
              "Stage 25C report records accepted status, baseline, and closure")
    c.require(CODE_MOD_DLL in report and CODE_MOD_FROZEN in report,
              "Stage 25C report records original and transformed DLL provenance")
    c.require("APPLE_EVEREST_REAL_MODS_STAGE25C_REPORT.md" in read(root, "docs/history/README.md"),
              "history index links Stage 25C report")

    lock_text = "\n".join([
        read(root, "managed/celeste-input-profiles.json"), read(root, "scripts/celeste-managed.py"),
        read(root, "scripts/verify-celeste-ios-stage24e1.py"), read(root, "native/ios-native-output.lock.json"),
        read(root, "build-tvos.sh"),
    ])
    for value, label in ((CONTENT, "content"), (RAW, "raw"), (PATCHED, "patched"), (STAGE6, "stage6"),
                         (VANILLA_IOS, "vanilla iOS"), (IOS_NATIVE, "iOS native"), (TVOS_NATIVE, "tvOS native")):
        c.require(value in lock_text, f"accepted {label} lock")

    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == IOS_RC, "iOS recovery tag")
    c.require(git(root, "rev-parse", "v1.0.0-rc.1^{}") == RC1, "tvOS RC1")
    c.require(git(root, "rev-parse", "v1.0.0-rc.2^{}") == RC2, "tvOS RC2")
    c.require(not git(root, "tag", "-l", "v1.0.0-rc.3"), "RC3 tag absent")
    c.require(git(root, "rev-parse", "origin/release/v1.0.0-rc.3") == RC3, "deferred RC3 preserved")
    c.require(git(root, "rev-parse", "tvos-port") == BASE and git(root, "rev-parse", "origin/tvos-port") == BASE, "integration branch preserved")
    c.require(git(root, "diff", "--name-only", BASE, "--", ".github") == "", "GitHub Actions untouched")

    closure_path = (args.closure or root / ".build/apple-everest/production-canary/shared-closure")
    if closure_path.is_dir():
        manifest = json.loads((closure_path / "compatibility-manifest.json").read_text())
        c.require(manifest["sharedClosureSha256"] == CLOSURE, "exact reproducible shared closure")
        c.require(manifest["runtimeDllLoading"] is False and manifest["runtimeDetour"] is False and manifest["interpreter"] is False, "closed runtime policy")
        c.require(manifest["resolvedOrder"] == ["IAccidentallyFourCassetteBlocks", "ParticlePaletteHelper"], "exact real-mod graph")
        c.require(manifest["precompiledAssembliesAotLinked"] == 1, "one external assembly statically linked")
        frozen = manifest["frozenAssemblies"]
        c.require(len(frozen) == 1 and frozen[0]["assemblyName"] == "ParticlePaletteHelper" and
                  frozen[0]["originalSha256"] == CODE_MOD_DLL and frozen[0]["frozenSha256"] == CODE_MOD_FROZEN,
                  "external identity and original/transformed DLL hashes")
        c.require(sha(closure_path / "assemblies/ParticlePaletteHelper.dll") == CODE_MOD_FROZEN, "frozen DLL bytes")
        roots = (closure_path / "managed/AppleEverestExternalAssemblyRoots.props").read_text()
        c.require('<TrimmerRootAssembly Include="ParticlePaletteHelper" />' in roots,
                  "exact external assembly receives a complete trimmer root")
        map_path = closure_path / "content/Content/Maps/ncrecc/0/IAccidentallyFourCassetteBlocks.bin"
        with map_path.open("rb") as stream:
            def binary_string() -> str:
                length = 0
                shift = 0
                while True:
                    octet = stream.read(1)
                    if len(octet) != 1:
                        raise SystemExit("FAIL: normalized map header is truncated")
                    value = octet[0]
                    length |= (value & 0x7f) << shift
                    if value < 0x80:
                        break
                    shift += 7
                    if shift > 28:
                        raise SystemExit("FAIL: normalized map header length is not bounded")
                return stream.read(length).decode("utf-8")
            c.require(binary_string() == "CELESTE MAP", "normalized real map magic")
            c.require(binary_string() == "ncrecc/0/IAccidentallyFourCassetteBlocks", "normalized real map package")

    if args.fixtures:
        expected = {
            "IAccidentallyFourCassetteBlocks-v1.0.0.zip": CONTENT_MOD_ZIP,
            "ParticlePaletteHelper-v1.0.0.zip": CODE_MOD_ZIP,
            "DashlessDreamBlocks-v2.0.0.zip": IL_MOD_ZIP,
            "GoldenTrainer-v1.5.4.zip": GOLDEN_TRAINER_ZIP,
        }
        for name, expected_sha in expected.items():
            c.require(sha(args.fixtures / name) == expected_sha, f"fixture SHA {name}")

    if args.ios_ipa:
        verify_package(args.ios_ipa.resolve(), "iOS", c)
    if args.tvos_ipa:
        verify_package(args.tvos_ipa.resolve(), "tvOS", c)

    output = {"schemaVersion": 1, "stage": "25C", "status": "PASS", "checks": c.count,
              "sharedClosureSha256": CLOSURE}
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(output, indent=2, sort_keys=True) + "\n")
    print(f"PASS: Stage 25C verifier ({c.count} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
