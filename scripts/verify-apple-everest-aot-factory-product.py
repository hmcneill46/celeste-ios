#!/usr/bin/env python3
"""Verify the actual linked/AOT/native selected-factory app and explicit signing mode."""
import argparse
import copy
import json
from pathlib import Path
import plistlib
import shlex
import struct
import subprocess
import sys
from importlib.util import spec_from_file_location, module_from_spec

ROOT = Path(__file__).resolve().parents[1]
spec = spec_from_file_location('aot_provenance', ROOT / 'scripts/record-apple-everest-aot-provenance.py')
provenance = module_from_spec(spec)
spec.loader.exec_module(provenance)


def native_platform(data):
    if len(data) < 32:
        raise ValueError('native executable has no complete Mach-O header')
    magic, cpu, subtype, kind, count, size, flags, reserved = struct.unpack_from('<8I', data)
    if magic != 0xfeedfacf or cpu != 0x100000c or kind != 2 or size > len(data) - 32:
        raise ValueError('native product must be a thin ARM64 Mach-O executable')
    offset, versions = 32, []
    for _ in range(count):
        if offset + 8 > 32 + size:
            raise ValueError('native load-command header exceeds its table')
        command, length = struct.unpack_from('<2I', data, offset)
        if length < 8 or length % 8 or offset + length > 32 + size:
            raise ValueError('native load-command size is invalid')
        if command == 0x32:  # LC_BUILD_VERSION identifies the actual Apple OS.
            if length < 24:
                raise ValueError('native build-version command is truncated')
            platform, minimum, sdk, tools = struct.unpack_from('<4I', data, offset + 8)
            if length != 24 + tools * 8:
                raise ValueError('native build-version tool table is invalid')
            versions.append((platform, minimum, sdk, offset))
        offset += length
    if offset != 32 + size or len(versions) != 1:
        raise ValueError('native product needs one exact LC_BUILD_VERSION command')
    return versions[0]


def verify_platform(info, fields, captured, data):
    platforms = {('iPhoneOS',): ('ios', 2, [1, 2], 'iphoneos'),
                 ('AppleTVOS',): ('tvos', 3, [3], 'appletvos')}
    expected = platforms.get(tuple(info.get('CFBundleSupportedPlatforms', [])))
    if expected is None:
        raise ValueError('app must name one exact supported Apple device platform')
    platform, native_id, family, sdk_name = expected
    minimum_os = '15.0' if platform == 'ios' else '16.0'
    if (sorted(info.get('UIDeviceFamily', [])) != family or info.get('DTPlatformName') != sdk_name
            or info.get('MinimumOSVersion') != minimum_os or info.get('DTPlatformVersion') != '26.5'
            or info.get('DTSDKName') != sdk_name + '26.5'):
        raise ValueError('app platform, minimum OS or native device family differs')
    actual_id, minimum, sdk, _ = native_platform(data)
    expected_minimum = (15 if platform == 'ios' else 16) << 16
    if actual_id != native_id or minimum != expected_minimum or sdk != (26 << 16 | 5 << 8):
        raise ValueError('native LC_BUILD_VERSION differs from the app platform/minimum OS/SDK')
    arguments = shlex.split(fields['Arguments'])
    # Reviewed .NET 10 Apple SDK uses the iOS ABI triple for BOTH device OSes.
    # The actual native platform and platform-specific compiler pack are the
    # OS authority; the Mono triple alone cannot distinguish iOS from tvOS.
    triples = [value for value in arguments if value.startswith('--aot=mtriple=')]
    if fields['Abi'] != 'ARM64+LLVM' or fields['Arch'] != 'arm64' or triples != ['--aot=mtriple=arm64-ios']:
        raise ValueError('AOT compiler arguments differ from the reviewed ARM64 Apple ABI')
    provenance.verify_compiler(captured, fields, platform)
    return platform


def verify_signing(app, mode):
    if mode == 'development':
        subprocess.run(['codesign', '--verify', '--strict', str(app)], check=True)
        return 'SIGNED_STRICT_VERIFIED'
    if mode != 'unsigned':
        raise ValueError('unknown signing verification mode')
    for path in app.rglob('*'):
        if (path.name in ('_CodeSignature', 'CodeResources', 'embedded.mobileprovision')
                or path.suffix.lower() in ('.mobileprovision', '.provisionprofile', '.xcent', '.entitlements')):
            raise ValueError('signing/provisioning material found in unsigned app')
        if not path.is_file():
            continue
        with path.open('rb') as stream:
            magic = stream.read(4)
        if magic == b'\xcf\xfa\xed\xfe':
            data = path.read_bytes()
            if len(data) < 32:
                raise ValueError('truncated Mach-O in unsigned app')
            count, size = struct.unpack_from('<2I', data, 16)
            offset = 32
            if size > len(data) - offset:
                raise ValueError('invalid Mach-O command table in unsigned app')
            for _ in range(count):
                if offset + 8 > 32 + size:
                    raise ValueError('truncated Mach-O command in unsigned app')
                command, length = struct.unpack_from('<2I', data, offset)
                if length < 8 or length % 8 or offset + length > 32 + size:
                    raise ValueError('invalid Mach-O command in unsigned app')
                if command == 0x1d:  # LC_CODE_SIGNATURE, including ad-hoc signatures.
                    raise ValueError('signed Mach-O found in unsigned app')
                offset += length
            if offset != 32 + size:
                raise ValueError('incomplete Mach-O command table in unsigned app')
        elif magic in (b'\xfe\xed\xfa\xcf', b'\xce\xfa\xed\xfe', b'\xfe\xed\xfa\xce',
                       b'\xca\xfe\xba\xbe', b'\xbe\xba\xfe\xca', b'\xca\xfe\xba\xbf', b'\xbf\xba\xfe\xca'):
            raise ValueError('unsupported Mach-O format in unsigned device app')
    result = subprocess.run(['codesign', '-d', '--verbose=2', str(app)], text=True, capture_output=True)
    if result.stderr:
        print(result.stderr, file=sys.stderr, end='')
    if (result.returncode != 1 or result.stdout
            or result.stderr.strip() != str(app) + ': code object is not signed at all'):
        raise ValueError('codesign did not positively establish unsigned app status')
    return 'UNSIGNED_ABSENCE_VERIFIED'


def platform_controls(info, fields, captured, data):
    platform = verify_platform(info, fields, captured, data)
    rejected = []
    def reject(name, mutate):
        values = [copy.deepcopy(info), copy.deepcopy(fields), copy.deepcopy(captured), bytearray(data)]
        mutate(*values)
        try:
            verify_platform(*values)
        except (ValueError, KeyError):
            rejected.append(name)
        else:
            raise ValueError('platform control was incorrectly accepted: ' + name)
    command = native_platform(data)[3]
    other_id = 3 if platform == 'ios' else 2
    other_pack = 'Cross.tvos-arm64' if platform == 'ios' else 'Cross.ios-arm64'
    this_pack = 'Cross.' + platform + '-arm64'
    def duplicate(i, f, c, b):
        offset = 32
        while offset < len(b):
            kind, size = struct.unpack_from('<2I', b, offset)
            if kind != 0x32 and size >= 24:
                struct.pack_into('<6I', b, offset, 0x32, size, 2, 15 << 16, 26 << 16 | 5 << 8, (size - 24) // 8)
                return
            offset += size
        raise ValueError('positive native fixture has no load command for duplicate control')
    reject('wrong-native-os', lambda i, f, c, b: struct.pack_into('<I', b, command + 8, other_id))
    reject('simulator-native-os', lambda i, f, c, b: struct.pack_into('<I', b, command + 8, 7))
    reject('wrong-native-cpu', lambda i, f, c, b: struct.pack_into('<I', b, 4, 0x1000007))
    reject('missing-native-platform', lambda i, f, c, b: struct.pack_into('<I', b, command, 0x777))
    reject('malformed-native-platform', lambda i, f, c, b: struct.pack_into('<I', b, command + 4, 8))
    reject('duplicate-native-platform', duplicate)
    reject('truncated-native-header', lambda i, f, c, b: b.__delitem__(slice(31, None)))
    reject('wrong-minimum-os', lambda i, f, c, b: struct.pack_into('<I', b, command + 12, 1 << 16))
    reject('wrong-native-sdk', lambda i, f, c, b: struct.pack_into('<I', b, command + 16, 1 << 16))
    reject('unknown-plist-platform', lambda i, f, c, b: i.update(CFBundleSupportedPlatforms=['UnknownOS']))
    reject('ambiguous-plist-platform', lambda i, f, c, b: i.update(CFBundleSupportedPlatforms=['iPhoneOS', 'AppleTVOS']))
    reject('wrong-device-family', lambda i, f, c, b: i.update(UIDeviceFamily=[4]))
    reject('wrong-compiler-pack', lambda i, f, c, b: c.update(compiler=c['compiler'].replace(this_pack, other_pack)))
    reject('wrong-llvm-pack', lambda i, f, c, b: f.update(Arguments=f['Arguments'].replace(this_pack, other_pack)))
    reject('unreviewed-aot-triple', lambda i, f, c, b: f.update(Arguments=f['Arguments'].replace('mtriple=arm64-ios', 'mtriple=arm64-tvos')))
    return {'actualProductPositive': True, 'platform': platform, 'disposableMemoryCopiesOnly': True, 'rejectedControls': rejected}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=Path, required=True)
    parser.add_argument('--app', type=Path, required=True)
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--authored-profiles', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--platform-controls-output', type=Path)
    parser.add_argument('--signing', choices=['development', 'unsigned'], default='development')
    args = parser.parse_args()
    build, app = args.build.resolve(), args.app.resolve()
    items = list(build.glob('obj/*/release_*-arm64/linker-items/_AssembliesToAOT.items'))
    if len(items) != 1:
        raise ValueError('expected one actual Release ARM64 linker/AOT item manifest')
    fields = provenance.selected_item(items[0])
    obj = items[0].parent.parent
    info = plistlib.loads((app / 'Info.plist').read_bytes())
    receipt = build / 'aot-provenance/receipt.json'
    captured = json.loads(receipt.read_text())
    native = app / info['CFBundleExecutable']
    platform = verify_platform(info, fields, captured, native.read_bytes())
    if any(captured[key] != value for key, value in provenance.source_identity().items()):
        raise ValueError('AOT compiler receipt does not belong to the current source revision/worktree state')
    for key, value in fields.items():
        if captured['fields'].get(key) != value:
            raise ValueError('actual SDK item differs from captured compiler item: ' + key)
    companion_fields = provenance.selected_item(items[0], 'DJMapHelper.dll')
    verify_platform(info, companion_fields, captured, native.read_bytes())
    for key, value in companion_fields.items():
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
    signing_status = verify_signing(app, args.signing)
    subprocess.run(['dotnet', 'exec', '--fx-version', '10.0.10',
                    str(ROOT / 'tools/AppleEverestBuilder/bin/Debug/net8.0/AppleEverestBuilder.dll'),
                    'verify-aot-factory-product', '--request', str(request_path),
                    '--manifest', str(args.manifest.resolve()), '--authored-profiles', str(args.authored_profiles.resolve()),
                    '--output', str(args.output.resolve())], cwd=ROOT, check=True)
    if args.platform_controls_output:
        result = platform_controls(info, fields, captured, native.read_bytes())
        args.platform_controls_output.write_text(json.dumps(result, indent=2, sort_keys=True) + '\n')
    print('PASS: ' + platform + ' selected-factory AOT product; ' + signing_status)


if __name__ == '__main__':
    main()
