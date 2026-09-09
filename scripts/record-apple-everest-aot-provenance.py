#!/usr/bin/env python3
"""Host-only hash receipts at the real Apple SDK AOT/native target boundaries."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shlex
import shutil
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def source_identity():
    def git(*args):
        return subprocess.check_output(['git', '-C', str(ROOT), *args], text=True).strip()
    return {'sourceCommit': git('rev-parse', 'HEAD'),
            'sourceTreeDirty': bool(git('status', '--porcelain'))}


def digest(path):
    with Path(path).open('rb') as stream:
        value = hashlib.sha256()
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
        return value.hexdigest()


def observe_toolchain():
    """Observe the executing host/SDK, never a caller-supplied host label."""
    machine = platform.machine()
    architecture = {'x86_64': 'x64', 'arm64': 'arm64'}.get(machine)
    if platform.system() != 'Darwin' or architecture is None:
        raise ValueError('AOT provenance requires a supported macOS host')
    dotnet = Path(shutil.which('dotnet') or '').resolve()
    env = dict(os.environ, DOTNET_CLI_UI_LANGUAGE='en-US')
    def run(*command):
        return subprocess.check_output(command, text=True, env=env).strip()
    info = run(str(dotnet), '--info')
    def field(section, key):
        match = re.search(r'^' + re.escape(section) + r':\n(.*?)(?=\n\S|\Z)', info, re.M | re.S)
        values = re.findall(r'^\s+' + re.escape(key) + r':\s+([^\n]+)', match[1] if match else '', re.M)
        if len(values) != 1:
            raise ValueError('independent .NET toolchain evidence missing: ' + key)
        return values[0].strip()
    observed = {'system': platform.system(), 'machine': machine, 'hostArchitecture': architecture,
                'dotnet': str(dotnet), 'dotnetSha256': digest(dotnet),
                'sdkVersion': field('.NET SDK', 'Version'),
                'workloadVersion': field('.NET SDK', 'Workload version'),
                'runtimeVersion': field('Host', 'Version'),
                'runtimeArchitecture': field('Host', 'Architecture'),
                'rid': field('Runtime Environment', 'RID'),
                'sdkBasePath': str(Path(field('Runtime Environment', 'Base Path')).resolve()),
                'xcode': run('xcodebuild', '-version'),
                'sdks': {sdk: run('xcrun', '--sdk', sdk, '--show-sdk-version')
                         for sdk in ('iphoneos', 'appletvos')}}
    if (observed['sdkVersion'] != '10.0.302' or observed['workloadVersion'] != '10.0.302.0'
            or observed['runtimeVersion'] != '10.0.10' or observed['runtimeArchitecture'] != architecture
            or observed['rid'] != 'osx-' + architecture
            or Path(observed['sdkBasePath']) != dotnet.parent / 'sdk/10.0.302'
            or observed['xcode'] != 'Xcode 26.6\nBuild version 17F113'
            or set(observed['sdks'].values()) != {'26.5'}):
        raise ValueError('independently observed host/toolchain differs from the pinned Apple AOT lane')
    return observed


def compiler_identity(compiler, fields, target_platform, observed):
    if target_platform not in ('ios', 'tvos'):
        raise ValueError('AOT target must be an Apple device platform')
    compiler = Path(compiler).resolve()
    pack = 'Microsoft.NETCore.App.Runtime.AOT.osx-' + observed['hostArchitecture'] + '.Cross.' + target_platform + '-arm64'
    expected = Path(observed['dotnet']).parent / 'packs' / pack / '10.0.10/tools/mono-aot-cross'
    if compiler != expected or not compiler.is_file():
        raise ValueError('AOT compiler pack differs from observed host, SDK, device platform or version')
    arguments = shlex.split(fields['Arguments'])
    llvm = [value.split('=', 1)[1] for value in arguments if value.startswith('llvm-path=')]
    if (fields['Abi'] != 'ARM64+LLVM' or fields['Arch'] != 'arm64'
            or [v for v in arguments if v.startswith('--aot=mtriple=')] != ['--aot=mtriple=arm64-ios']
            or len(llvm) != 1 or Path(llvm[0]).resolve() != compiler.parent):
        raise ValueError('AOT device ABI or LLVM directory differs from the actual compiler pack')
    # Bind LLVM executables too: a path alone does not establish unchanged tools.
    llvm_hashes = {name: digest(compiler.parent / name) for name in ('llc', 'opt')}
    architectures = subprocess.check_output(['xcrun', 'lipo', '-archs', str(compiler)], text=True).split()
    if architectures != [observed['machine']]:
        raise ValueError('actual compiler executable does not match the observed host architecture')
    version = subprocess.check_output([str(compiler), '--version'], text=True)
    if (not version.startswith('Mono JIT compiler version 10.0.10.0 (')
            or not re.search(r'^\s*Architecture:\s+arm64\s*$', version, re.M)):
        raise ValueError('actual AOT compiler version or device target differs')
    return {'targetPlatform': target_platform, 'targetArchitecture': 'arm64',
            'compilerSha256': digest(compiler), 'llvmSha256': llvm_hashes}


def verify_compiler(record, fields, target_platform):
    observed = observe_toolchain()
    if record['toolchain'] != observed:
        raise ValueError('AOT receipt toolchain differs from independent observation')
    identity = compiler_identity(record['compiler'], fields, target_platform, observed)
    if any(record[key] != value for key, value in identity.items()):
        raise ValueError('AOT compiler/LLVM receipt changed')


def selected_item(path, assembly='Celeste.dll'):
    rows = [row for row in ET.parse(path).getroot().iter()
            if row.tag.rsplit('}', 1)[-1] == '_AssembliesToAOT'
            and Path(row.attrib['Include']).name == assembly]
    if len(rows) != 1:
        raise ValueError('expected one actual AOT compiler input: ' + assembly)
    row = rows[0]
    fields = {child.tag.rsplit('}', 1)[-1]: child.text for child in row}
    fields['LinkedAssembly'] = row.attrib['Include']
    arguments = shlex.split(fields['Arguments'])
    if (fields['Abi'] != 'ARM64+LLVM' or fields['Arch'] != 'arm64'
            or not {'full', 'static', 'asmonly'}.issubset(arguments)
            or '--llvm' not in shlex.split(fields['ProcessArguments'])
            or not any(value in arguments for value in
                       ('--aot=mtriple=arm64-ios', '--aot=mtriple=arm64-tvos'))):
        raise ValueError('selected product is not full/static LLVM ARM64 AOT')
    for option, field in [('data-outfile', 'AOTData'), ('outfile', 'AOTAssembly'), ('llvm-outfile', 'LLVMFile')]:
        if arguments.count(option + '=' + fields[field]) != 1:
            raise ValueError('AOT argument/output mapping differs: ' + field)
    return fields


def inputs(fields):
    return {'LinkedAssembly': digest(fields['LinkedAssembly'])}


def outputs(fields):
    return {key: digest(fields[key]) for key in ('ObjectFile', 'LLVMFile', 'AOTData')}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('phase', choices=['before', 'after', 'native'])
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--compiler', type=Path)
    parser.add_argument('--platform', type=str.lower, choices=['ios', 'tvos'])
    parser.add_argument('--native', type=Path)
    parser.add_argument('--native-main', type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    items = root / 'compiler-items.xml'
    fields = selected_item(items)
    companions = {'DJMapHelper': selected_item(items, 'DJMapHelper.dll')}
    receipt = root / 'receipt.json'
    if args.phase == 'before':
        if receipt.exists() or any(Path(row[key]).exists() for row in [fields, *companions.values()]
                                   for key in ('ObjectFile', 'LLVMFile', 'AOTData')):
            raise ValueError('AOT provenance requires a fresh compiler output, not an incremental/stale object')
        observed = observe_toolchain()
        identity = compiler_identity(args.compiler, fields, args.platform, observed)
        if compiler_identity(args.compiler, companions['DJMapHelper'], args.platform, observed) != identity:
            raise ValueError('companion compiler identity differs')
        record = {'schemaVersion': 1, 'phase': 'BEFORE_AOT', 'boundary': 'AppleSDK._AOTCompile',
                  **source_identity(),
                  'toolchain': observed, **identity,
                  'compiler': str(args.compiler.resolve()), 'compilerSha256': digest(args.compiler),
                  'nativeMain': str(args.native_main.resolve()), 'nativeMainSha256': digest(args.native_main),
                  'itemsSha256': digest(items), 'fields': fields, 'inputHashes': inputs(fields),
                  'companions': {key: {'fields': row, 'inputHashes': inputs(row)} for key, row in companions.items()}}
    else:
        record = json.loads(receipt.read_text())
        verify_compiler(record, fields, record['targetPlatform'])
        if any(record[key] != value for key, value in source_identity().items()):
            raise ValueError('source revision/worktree state changed across actual compiler boundary')
        if (record['fields'] != fields or record['itemsSha256'] != digest(items)
                or record['inputHashes'] != inputs(fields)
                or record['compilerSha256'] != digest(record['compiler'])
                or record['nativeMainSha256'] != digest(record['nativeMain'])):
            raise ValueError('AOT input/compiler changed across actual compiler boundary')
        for key, row in companions.items():
            if record['companions'][key]['fields'] != row or record['companions'][key]['inputHashes'] != inputs(row):
                raise ValueError('companion AOT input changed across actual compiler boundary: ' + key)
        if args.phase == 'after':
            if record['phase'] != 'BEFORE_AOT':
                raise ValueError('AOT completion has no unique preceding input receipt')
            record.update(phase='AFTER_AOT', outputHashes=outputs(fields))
            for key, row in companions.items():
                record['companions'][key]['outputHashes'] = outputs(row)
        else:
            if record['phase'] != 'AFTER_AOT' or record['outputHashes'] != outputs(fields):
                raise ValueError('AOT objects changed before native link completion')
            for key, row in companions.items():
                if record['companions'][key]['outputHashes'] != outputs(row):
                    raise ValueError('companion AOT objects changed before native link completion: ' + key)
            record.update(phase='AFTER_NATIVE_LINK', nativeImage=str(args.native.resolve()),
                          nativeImageSha256=digest(args.native))
    receipt.write_text(json.dumps(record, indent=2, sort_keys=True) + '\n')
    print('PASS: selected-factory provenance ' + record['phase'])


if __name__ == '__main__':
    main()
