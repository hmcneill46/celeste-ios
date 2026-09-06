#!/usr/bin/env python3
"""Generate three complete K-J closures and independently compile each preflight."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
AE = ROOT / 'apple-everest'


def read(path):
    return json.loads(path.read_text())


def sha(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
    return value.hexdigest()


def inventory(root):
    return {p.relative_to(root).as_posix(): sha(p) for p in sorted(root.rglob('*')) if p.is_file()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package-root', type=Path, required=True)
    parser.add_argument('--presentation-root', type=Path, required=True)
    parser.add_argument('--chrono-package', type=Path, required=True)
    parser.add_argument('--production-root', type=Path, required=True)
    parser.add_argument('--work-root', type=Path, required=True)
    args = parser.parse_args()
    work = args.work_root.resolve()
    if work.exists():
        raise ValueError('use a fresh reproduction work root')
    work.mkdir(parents=True)
    (work / '.stage25kj-reproduction').touch()
    profiles = AE / 'sj-factory-authored-profiles-stage25kj.json'
    providers = sorted({r['provider'] for r in read(profiles)['factories']} - {'EverestCore'})
    inputs = [args.package_root.resolve() / (p + '.zip') for p in providers]
    inputs += [args.presentation_root.resolve() / (p + '.zip') for p in
               ['StrawberryJam2021Assets', 'StrawberryJam2021AudioA', 'StrawberryJam2021AudioB']]
    inputs += [args.chrono_package.resolve()]
    inputs += [AE / 'canaries' / p for p in ['stage25ke', 'stage25kf', 'stage25kh', 'stage25kj',
                                          'stage25kj-interactions', 'custom-audio-content', 'dj-frozen-il-content']]
    mod_args = [arg for path in inputs for arg in ['--mod', str(path)]]
    runner = ['dotnet', 'exec', '--fx-version', '10.0.10',
              str(ROOT / 'tools/AppleEverestBuilder/bin/Debug/net8.0/AppleEverestBuilder.dll')]
    assembly = args.production_root.resolve() / 'preflight-runtime/bin/Release/net10.0-ios26.5/Celeste.dll'
    common = ['--profile', str(AE / 'profiles/stable-1.6458.0.json'), '--repo-root', str(ROOT),
              '--upstream', str(ROOT / '.build/apple-everest/upstream/Everest')]
    snapshots = []
    for number in range(1, 4):
        current = work / ('run' + str(number))
        current.mkdir()
        closure = current / 'shared-closure'
        with (current / 'commands.log').open('w') as log:
            def run(command):
                result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
                if result.returncode:
                    raise RuntimeError('reproduction command failed; inspect run' + str(number) + '/commands.log')
            run(runner + ['build', *common, '--output', str(closure), *mod_args])
            run(runner + ['preflight-factory-closure', *common, '--manifest', str(AE / 'selected-factory-type-closure-stage25kh.json'),
                '--authored-profiles', str(profiles), '--assembly', str(assembly), '--closure', str(closure),
                '--canonical-managed-root', str(ROOT / '.build/celeste-ios/current/managed'),
                '--dotnet', shutil.which('dotnet'), '--output', str(current / 'production-preflight.json'), *mod_args])
            run(['python3', str(ROOT / 'scripts/inspect-apple-everest-stage25kj-canary-profiles.py'),
                 '--closure', str(closure), '--output', str(current / 'compiled-canary-profiles.json')])
            run(runner + ['inspect-compiled-factories', '--assembly', str(assembly), '--manifest',
                str(AE / 'selected-factory-type-closure-stage25kh.json'), '--authored-profiles',
                str(current / 'compiled-canary-profiles.json'), '--output', str(current / 'compiled-canary-guards.json')])
            run(['python3', str(ROOT / 'scripts/generate-apple-everest-stage25kj-semantic-evidence.py'),
                 '--product-root', str(current), '--evidence-output', str(current)])
        manifest = read(closure / 'compatibility-manifest.json')
        production = read(current / 'production-preflight.json')
        semantic = read(current / 'sj-selected-factory-semantic-closure-stage25kj.json')
        snapshot = {'completeTree': inventory(closure), 'closure': manifest,
            'contentIdCensus': production['contentIdCensus'], 'registrationCensus': production['registrationCensus'],
            'semanticClosureCensus': semantic['census'], 'packages': production['providers'],
            'actualCompiledFactories': production['factories'],
            'fullImplementationComparison': production['compilation']['CompiledDllLogicalSha256']}
        (current / 'snapshot.json').write_text(json.dumps(snapshot, indent=2, sort_keys=True) + '\n')
        if snapshots and snapshot != snapshots[0]:
            raise ValueError('complete closure / actual registration / semantic census determinism failed')
        snapshots.append(snapshot)
        # Preserve complete inventories and compiler evidence; discard only this
        # freshly generated marked tree to avoid retaining three bank copies.
        if not (closure / '.apple-everest-static-closure').is_file():
            raise ValueError('refusing to remove unmarked reproduction closure')
        shutil.rmtree(closure)
        print('PASS: complete K-J closure and fresh compiler proof run ' + str(number), flush=True)
    report = {'schemaVersion': 1, 'stage': '25K-J', 'threeFreshGenerations': True,
        'freshProductionCompilations': 3, 'completeTreeCompared': True, 'runsIdentical': True,
        'runSnapshotSha256': [sha(work / ('run' + str(i)) / 'snapshot.json') for i in range(1, 4)],
        'sharedClosureSha256': [s['closure']['sharedClosureSha256'] for s in snapshots],
        'fileCount': len(snapshots[0]['completeTree']), 'semanticCensusAuthority': 'SEPARATE_PHYSICAL_GATE_REMAINS_PENDING'}
    (work / 'reproduction.json').write_text(json.dumps(report, indent=2, sort_keys=True) + '\n')


if __name__ == '__main__':
    main()
