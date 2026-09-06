#!/usr/bin/env python3
"""Verify the actual linked/AOT/native/signed selected-factory app product."""
import argparse
import json
from pathlib import Path
import plistlib
import subprocess
from importlib.util import spec_from_file_location, module_from_spec

ROOT = Path(__file__).resolve().parents[1]
spec = spec_from_file_location('aot_provenance', ROOT / 'scripts/record-apple-everest-aot-provenance.py')
provenance = module_from_spec(spec)
spec.loader.exec_module(provenance)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=Path, required=True)
    parser.add_argument('--app', type=Path, required=True)
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--authored-profiles', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    build, app = args.build.resolve(), args.app.resolve()
    items = list(build.glob('obj/*/release_*-arm64/linker-items/_AssembliesToAOT.items'))
    if len(items) != 1:
        raise ValueError('expected one actual Release ARM64 linker/AOT item manifest')
    fields = provenance.selected_item(items[0])
    obj = items[0].parent.parent
    info = plistlib.loads((app / 'Info.plist').read_bytes())
    platform = 'tvos' if 'appletvos' in info.get('CFBundleSupportedPlatforms', [''])[0].lower() else 'ios'
    if platform == 'ios' and (sorted(info['UIDeviceFamily']) != [1, 2]
                              or info['MinimumOSVersion'] != '15.0'):
        raise ValueError('iOS product must preserve minimum15/universal iPhone-iPad family')
    if platform == 'tvos' and info['UIDeviceFamily'] != [3]:
        raise ValueError('tvOS product must retain the native TV device family')
    if ('--aot=mtriple=arm64-' + platform) not in fields['Arguments'].split():
        raise ValueError('AOT architecture/platform does not match app Info.plist')
    receipt = build / 'aot-provenance/receipt.json'
    captured = json.loads(receipt.read_text())
    if any(captured[key] != value for key, value in provenance.source_identity().items()):
        raise ValueError('AOT compiler receipt does not belong to the current source revision/worktree state')
    for key, value in fields.items():
        if captured['fields'].get(key) != value:
            raise ValueError('actual SDK item differs from captured compiler item: ' + key)
    for key, value in provenance.selected_item(items[0], 'DJMapHelper.dll').items():
        if captured['companions']['DJMapHelper']['fields'].get(key) != value:
            raise ValueError('actual companion SDK item differs from captured compiler item: ' + key)
    request = {'LinkedAssembly': fields['LinkedAssembly'], 'StrippedAssembly': str(obj / 'stripped/Celeste.dll'),
               'PackagedAssembly': str(app / 'Celeste.dll'), 'LlvmObject': fields['LLVMFile'],
               'MonoObject': fields['ObjectFile'], 'NativeImage': str(obj / 'nativelibraries' / info['CFBundleExecutable']),
               'PackagedNativeImage': str(app / info['CFBundleExecutable']), 'AotData': fields['AOTData'],
               'PackagedAotData': str(app / 'Celeste.aotdata.arm64'),
               'NativeMain': str(obj / 'linker-cache/main.arm64.mm'), 'AotProvenance': str(receipt)}
    for path in request.values():
        if not Path(path).is_file():
            raise ValueError('actual product evidence missing: ' + Path(path).name)
    request_path = build / 'selected-factory-product-request.json'
    request_path.write_text(json.dumps(request, indent=2, sort_keys=True) + '\n')
    subprocess.run(['codesign', '--verify', '--strict', str(app)], check=True)
    subprocess.run(['dotnet', 'exec', '--fx-version', '10.0.10',
                    str(ROOT / 'tools/AppleEverestBuilder/bin/Debug/net8.0/AppleEverestBuilder.dll'),
                    'verify-aot-factory-product', '--request', str(request_path),
                    '--manifest', str(args.manifest.resolve()), '--authored-profiles', str(args.authored_profiles.resolve()),
                    '--output', str(args.output.resolve())], cwd=ROOT, check=True)
    print('PASS: signed ' + platform + ' selected-factory AOT product')


if __name__ == '__main__':
    main()
