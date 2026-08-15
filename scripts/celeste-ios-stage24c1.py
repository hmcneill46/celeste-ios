#!/usr/bin/env python3
"""Derive the narrow modern-iOS Celeste tree from the locked shared Stage 6 tree."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import shutil

SHARED_COUNT = 934
SHARED_HASH = "1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9"


def digest(path: pathlib.Path) -> str:
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def manifest(root: pathlib.Path) -> dict[str, object]:
    records: list[dict[str, object]] = []
    logical = hashlib.sha256()
    for path in sorted(root.rglob("*"), key=lambda item: item.relative_to(root).as_posix()):
        if not path.is_file() or any(part in {"bin", "obj"} for part in path.relative_to(root).parts):
            continue
        relative = path.relative_to(root).as_posix()
        sha = digest(path)
        records.append({"path": relative, "size": path.stat().st_size, "sha256": sha})
        logical.update(relative.encode() + b"\0" + sha.encode() + b"\n")
    return {"fileCount": len(records), "logicalSha256": logical.hexdigest(), "files": records}


def replace_exact(path: pathlib.Path, old: str, new: str, label: str, count: int = 1) -> None:
    text = path.read_text()
    actual = text.count(old)
    if actual != count:
        raise SystemExit(f"error: {label} expected {count} matches in {path.name}, found {actual}")
    path.write_text(text.replace(old, new))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=pathlib.Path, required=True)
    parser.add_argument("--templates", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve()
    before = manifest(root)
    if before["fileCount"] != SHARED_COUNT or before["logicalSha256"] != SHARED_HASH:
        raise SystemExit("error: iOS derivation input is not the locked Stage 6 real-audio tree")

    # These later tvOS product features remain in the canonical Stage 6 tree but
    # are not part of the modern iOS product. Their guarded source is removed
    # before compilation rather than activated under misleading tvOS symbols.
    removed = [
        "Audio.TvOSDisabled.cs", "TvOSControllerPromptBridge.cs",
        "TvOSPerformanceHudBridge.cs", "TvOSQuitBridge.cs",
        "TvOSSaveManagerBridge.cs", "TvOSSoftReloadBridge.cs",
    ]
    for name in removed:
        (root / "Celeste" / name).unlink()

    identifier_map = {
        "TvOSStage3Bridge": "AppleRuntimeBridge",
        "TvOSStage3CBridge": "AppleRuntimeDiagnostics",
        "TvOSStage5BAudioBridge": "AppleAudioDiagnostics",
        "TvOSStage6PersistenceHooks": "IOSStorageHooks",
        "TvOSSettingsSerializer": "AppleSettingsSerializer",
        "TvOSSaveDataSerializer": "AppleSaveDataSerializer",
        "CELESTE_TVOS_SESSION_ROOT": "CELESTE_IOS_STORAGE_ROOT",
        "CELESTE_TVOS_PROLOGUE_SCENARIO": "CELESTE_RUNTIME_PROLOGUE_SCENARIO",
        "TVOS_STAGE3B": "CELESTE_RUNTIME",
        "TVOS_STAGE3C": "CELESTE_RUNTIME",
        "TVOS_STAGE5B": "CELESTE_RUNTIME",
        "TVOS_REAL_AUDIO": "CELESTE_RUNTIME",
        "TVOS_STAGE6": "IOS_CELESTE_RUNTIME_HOST",
    }
    for path in sorted(root.rglob("*")):
        if not path.is_file() or path.suffix not in {".cs", ".csproj", ".xml"}:
            continue
        text = path.read_text()
        for old, new in identifier_map.items():
            text = text.replace(old, new)
        text = text.replace("unavailable on tvOS", "unavailable on modern iOS")
        text = text.replace("tvOS session root", "iOS app-container root")
        path.write_text(text)

    renames = {
        "TvOSStage3Bridge.cs": "AppleRuntimeBridge.cs",
        "TvOSStage3CBridge.cs": "AppleRuntimeDiagnostics.cs",
        "TvOSStage5BAudioBridge.cs": "AppleAudioDiagnostics.cs",
        "TvOSStage6PersistenceHooks.cs": "IOSStorageHooks.cs",
        "TvOSSettingsSerializer.cs": "AppleSettingsSerializer.cs",
        "TvOSSaveDataSerializer.cs": "AppleSaveDataSerializer.cs",
    }
    for old, new in renames.items():
        (root / "Celeste" / old).rename(root / "Celeste" / new)
    shutil.copyfile(args.templates / "IOSStorageHooks.cs", root / "Celeste" / "IOSStorageHooks.cs")

    # Fatal/error diagnostics are incidental runtime evidence, never durable
    # Celeste settings or SaveData. Keep them outside the strict four-file
    # Application Support allowlist used by IOSStorageHooks.
    error_log = root / "Monocle" / "ErrorLog.cs"
    replace_exact(error_log,
                  'Environment.GetEnvironmentVariable("CELESTE_IOS_STORAGE_ROOT")',
                  'Environment.GetEnvironmentVariable("CELESTE_IOS_INCIDENTAL_ROOT")',
                  "iOS incidental error-log root")

    project = root / "Celeste.Modern.csproj"
    text = project.read_text()
    constants_start = "<DefineConstants>$(DefineConstants);TVOS;CELESTE_RUNTIME;CELESTE_RUNTIME;CELESTE_RUNTIME;IOS_CELESTE_RUNTIME_HOST;TVOS_STAGE10A;TVOS_STAGE11;TVOS_STAGE12B;TVOS_STAGE13B;TVOS_STAGE15;TVOS_STAGE16B;CELESTE_RUNTIME</DefineConstants>"
    if text.count(constants_start) != 1:
        raise SystemExit("error: generated project constants are not the expected transformed Stage 6 form")
    text = text.replace(constants_start, "<DefineConstants>$(DefineConstants);CELESTE_RUNTIME;IOS_CELESTE_RUNTIME_HOST</DefineConstants>")
    text = text.replace("net10.0-tvos26.5", "net10.0-ios26.5")
    text = text.replace("<SupportedOSPlatformVersion>16.0</SupportedOSPlatformVersion>", "<SupportedOSPlatformVersion>15.0</SupportedOSPlatformVersion>")
    old_reference = (
        '<ProjectReference Include="$(CelesteTvOSRepoRoot)/tvos/FNA.TvOS/FNA.TvOS.csproj"\n'
        '                      AdditionalProperties="CustomAfterMicrosoftCommonTargets=$(CelesteTvOSRepoRoot)/tvos/CelesteTvOSRuntimeHost/Stage3BFnaExtension.targets;Stage6PersistenceEnabled=$(Stage6PersistenceEnabled);CelesteTvOSRepoRoot=$(CelesteTvOSRepoRoot)" />'
    )
    new_reference = (
        '<ProjectReference Include="$(CelesteAppleRepoRoot)/modern-ios/FNA.iOS/FNA.iOS.csproj"\n'
        '                      AdditionalProperties="CustomAfterMicrosoftCommonTargets=$(CelesteAppleRepoRoot)/modern-ios/CelesteIOSRuntimeHost/IOSFnaExtension.targets;IOSCelesteRuntimeEnabled=true;CelesteAppleRepoRoot=$(CelesteAppleRepoRoot)" />'
    )
    if text.count(old_reference) != 1:
        raise SystemExit("error: generated FNA project reference changed")
    text = text.replace(old_reference, new_reference)
    text = text.replace("ValidateCelesteTvOSRepoRoot", "ValidateCelesteAppleRepoRoot")
    text = text.replace("CelesteTvOSRepoRoot", "CelesteAppleRepoRoot")
    text = text.replace("CelesteAppleRepoRoot must point to the repository root.", "CelesteAppleRepoRoot must point to the repository root.")
    project.write_text(text)

    content_project = root / "Celeste.Content.Modern.csproj"
    replace_exact(content_project, "net10.0-tvos26.5", "net10.0-ios26.5", "Content iOS target")

    # FMOD's SDL output bridge is useful on tvOS. On iOS the validated FMOD
    # device library owns its native CoreAudio output directly.
    audio = root / "Celeste" / "Audio.cs"
    replace_exact(audio,
                  '\t[DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]\n\tprivate static extern void FMOD_SDL_Register(IntPtr system);\n\n',
                  "", "remove iOS-inapplicable FMOD SDL import")
    replace_exact(audio, "\t\tFMOD_SDL_Register(system.getRaw());\n\t\tAppleAudioDiagnostics.Checkpoint(\"fmod-sdl-registered\", \"target=studio-low-level\");",
                  "\t\tAppleAudioDiagnostics.Checkpoint(\"ios-native-output\", \"target=studio-low-level; driver=CoreAudio\");",
                  "iOS native FMOD output")

    # The game's managed FMOD 1.10.20 wrapper names one DSP diagnostic added
    # after the accepted 1.10.09 runtime. Celeste never calls it; keep a
    # deterministic managed unsupported result so AOT does not require a
    # nonexistent native symbol.
    dsp = root / "FMOD" / "DSP.cs"
    replace_exact(dsp,
                  "\tpublic RESULT getCPUUsage(out uint exclusive, out uint inclusive)\n\t{\n\t\treturn FMOD_DSP_GetCPUUsage(rawPtr, out exclusive, out inclusive);\n\t}",
                  "\tpublic RESULT getCPUUsage(out uint exclusive, out uint inclusive)\n\t{\n\t\texclusive = 0;\n\t\tinclusive = 0;\n\t\treturn RESULT.ERR_UNSUPPORTED;\n\t}",
                  "FMOD 1.10.09 DSP CPU diagnostic compatibility")
    replace_exact(dsp,
                  '\n\t[DllImport("__Internal", ExactSpelling = true)]\n\tpublic static extern RESULT FMOD_DSP_GetCPUUsage(IntPtr dsp, out uint exclusive, out uint inclusive);\n',
                  "\n", "remove unavailable FMOD 1.10.20 DSP import")

    # A mobile app must never expose Celeste's desktop process-exit route.
    menu = root / "Celeste" / "OuiMainMenu.cs"
    exit_button = (
        "\t\tif (!Celeste.IsGGP)\n\t\t{\n"
        "\t\t\tMainMenuSmallButton mainMenuSmallButton5 = new MainMenuSmallButton(\"menu_exit\", \"menu/exit\", this, vector, vector + vector2, OnExit);\n"
        "\t\t\tbuttons.Add(mainMenuSmallButton5);\n"
        "\t\t\tvector += Vector2.UnitY * mainMenuSmallButton5.ButtonHeight;\n\t\t}\n"
    )
    replace_exact(menu, exit_button, "", "iOS desktop Quit removal")

    after = manifest(root)
    report = {
        "schemaVersion": 1,
        "platformTransform": "modern-ios-stage24c1-v1",
        "input": {"fileCount": before["fileCount"], "logicalSha256": before["logicalSha256"]},
        "output": after,
        "removedTvOSOnlyFiles": removed,
        "quitPolicy": "desktop-main-menu-exit-hidden",
        "storagePolicy": "Application Support/Celeste; settings and slots 0-2 only",
        "incidentalPolicy": "temporary-directory diagnostics only",
        "fmodOutput": "native iOS/CoreAudio; no FMOD SDL output bridge",
        "generatedSourceTracked": False,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    print(f"PASS: modern iOS generated tree {after['fileCount']} files {after['logicalSha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
