#!/usr/bin/env python3
"""Derive Stage 1 native exports from the exact managed binding sources."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re


SOURCES = {
    "SDL2": "SDL2-CS-bindings/src/SDL2.cs",
    "FNA3D": "FNA-bindings/src/Graphics/FNA3D.cs",
    "FAudio": "FAudio/csharp/FAudio.cs",
    "Theorafile": "Theorafile/csharp/Theorafile.cs",
}

STUB_SYMBOLS = [
    "INTERNAL_SDL_AndroidGetExternalStoragePath",
    "SDL_AndroidBackButton",
    "SDL_AndroidGetActivity",
    "SDL_AndroidGetExternalStoragePath",
    "SDL_AndroidGetExternalStorageState",
    "SDL_AndroidGetInternalStoragePath",
    "SDL_AndroidGetJNIEnv",
    "SDL_AndroidRequestPermission",
    "SDL_AndroidShowToast",
    "SDL_DXGIGetOutputInfo",
    "SDL_Direct3D9GetAdapterIndex",
    "SDL_GetAndroidSDKVersion",
    "SDL_IsAndroidTV",
    "SDL_IsChromebook",
    "SDL_IsDeXMode",
    "SDL_LinuxSetThreadPriority",
    "SDL_RenderGetD3D11Device",
    "SDL_RenderGetD3D9Device",
    "SDL_SetWindowsMessageHook",
    "SDL_WinRTGetDeviceFamily",
    "SDL_WinRTGetFSPathUNICODE",
    "SDL_WinRTGetFSPathUTF8",
    "SDL_WinRTRunApp",
    "emscripten_cancel_main_loop",
    "emscripten_set_main_loop",
]


def pinvoke_symbols(text: str) -> list[str]:
    symbols: set[str] = set()
    # Binding declarations in these pinned sources place DllImport immediately
    # before an extern declaration. Match across formatting-only line breaks.
    pattern = re.compile(
        r"\[DllImport\((?P<attribute>.*?)\)\]"
        r"(?P<declaration>.{0,1200}?\bextern\b.{0,600}?\b(?P<method>[A-Za-z_]\w*)\s*\()",
        re.DOTALL,
    )
    for match in pattern.finditer(text):
        entry = re.search(r'EntryPoint\s*=\s*"([^"]+)"', match.group("attribute"))
        symbols.add(entry.group(1) if entry else match.group("method"))
    return sorted(symbols)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sources-dir", required=True, type=pathlib.Path)
    parser.add_argument("--hud-bootstrap-source", required=True, type=pathlib.Path)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    args = parser.parse_args()

    components: dict[str, dict[str, object]] = {}
    for component, relative in SOURCES.items():
        path = args.sources_dir / relative
        data = path.read_bytes()
        symbols = pinvoke_symbols(data.decode("utf-8-sig"))
        if not symbols:
            raise SystemExit(f"no DllImport symbols found in {path}")
        components[component] = {
            "bindingSource": relative,
            "bindingSha256": hashlib.sha256(data).hexdigest(),
            "symbols": symbols,
        }

    vulkan_loader = args.sources_dir / "FNA3D/src/FNA3D_Driver_Vulkan.c"
    components["MoltenVK"] = {
        "bindingSource": "FNA3D/src/FNA3D_Driver_Vulkan.c",
        "bindingSha256": hashlib.sha256(vulkan_loader.read_bytes()).hexdigest(),
        "note": "FNA3D dynamically resolves Vulkan entry points through this loader export.",
        "symbols": ["vkGetInstanceProcAddr"],
    }
    stubs_source = args.sources_dir / "NativeBuilder/tvStubs/stubs.c"
    stubs_data = stubs_source.read_bytes()
    derived_stubs = sorted(
        set(
            re.findall(
                r"^extern\s+[^\n]*?\b([A-Za-z_]\w*)\s*\(",
                stubs_data.decode("utf-8"),
                re.MULTILINE,
            )
        )
    )
    if derived_stubs != sorted(STUB_SYMBOLS):
        raise SystemExit("tracked tvStubs rationale list differs from the pinned stubs.c exports")
    components["tvStubs"] = {
        "bindingSource": "NativeBuilder/tvStubs/stubs.c",
        "bindingSha256": hashlib.sha256(stubs_data).hexdigest(),
        "repositoryBootstrapSource": "native/tvstubs/MetalPerformanceHudBootstrap.m",
        "repositoryBootstrapSha256": hashlib.sha256(args.hud_bootstrap_source.read_bytes()).hexdigest(),
        "note": "Non-tvOS SDL entry points retained for static Xamarin/FNA binding resolution plus the public-Foundation tvOS Metal HUD capability bootstrap.",
        "symbols": sorted(derived_stubs + ["CelesteTvOSMetalHudBootstrapForceLink"]),
    }

    output = {
        "schemaVersion": 1,
        "scope": "Pinned FNA, SDL2-CS, FAudio-CS, and Theorafile-CS native entry points; FMOD excluded.",
        "components": components,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
