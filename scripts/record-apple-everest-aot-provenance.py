#!/usr/bin/env python3
"""Host-only hash receipts at the real Apple SDK AOT/native target boundaries."""
import argparse
import hashlib
import json
from pathlib import Path
import shlex
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
        record = {'schemaVersion': 1, 'phase': 'BEFORE_AOT', 'boundary': 'AppleSDK._AOTCompile',
                  **source_identity(),
                  'compiler': str(args.compiler.resolve()), 'compilerSha256': digest(args.compiler),
                  'nativeMain': str(args.native_main.resolve()), 'nativeMainSha256': digest(args.native_main),
                  'itemsSha256': digest(items), 'fields': fields, 'inputHashes': inputs(fields),
                  'companions': {key: {'fields': row, 'inputHashes': inputs(row)} for key, row in companions.items()}}
    else:
        record = json.loads(receipt.read_text())
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
