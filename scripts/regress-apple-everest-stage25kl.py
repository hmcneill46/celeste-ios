#!/usr/bin/env python3
"""Run current K-L regressions, separating immutable historical contracts."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
START = 'e69bfd6eeba3d36a5d745c24b3bc0f2f3f4ee92f'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work-root', type=Path, required=True)
    parser.add_argument('--product-root', type=Path, default=ROOT / '.build/apple-everest/stage25kl/product')
    args = parser.parse_args()
    work = args.work_root.resolve()
    if not work.is_relative_to(ROOT / '.build/apple-everest') or work.exists():
        raise ValueError('a fresh owned ignored work root is required')
    work.mkdir(parents=True)
    (work / '.stage25kl-regressions').touch()
    env = dict(os.environ, MSBUILDDISABLENODEREUSE='1',
               DOTNET_CLI_USE_MSBUILD_SERVER='0', UseSharedCompilation='false')

    def git(*arguments):
        return subprocess.check_output(['git', '-C', str(ROOT), *arguments], text=True).strip()

    revision = git('rev-parse', 'HEAD')
    if git('status', '--porcelain'):
        raise ValueError('current regression evidence requires a clean source revision')
    records = []

    def run(command, name, cwd=ROOT, required=True):
        print('RUN: ' + name, flush=True)
        logpath = work / (name + '.log')
        with logpath.open('w') as log:
            result = subprocess.run([str(v) for v in command], cwd=cwd, env=env,
                                    stdout=log, stderr=subprocess.STDOUT)
        records.append({'name': name, 'exitCode': result.returncode,
                        'classification': 'CURRENT_PASS' if result.returncode == 0 else
                        ('CURRENT_FAIL' if required else 'HISTORICAL_CONTRACT_INCOMPATIBLE'),
                        'requiredCurrent': required,
                        'logSha256': hashlib.sha256(logpath.read_bytes()).hexdigest()})
        if required and result.returncode:
            raise RuntimeError(name + ' failed; inspect its log')

    sdk = ROOT / '.build/apple-everest/toolchain/dotnet8/dotnet'

    def test(project, name, arguments=()):
        artifact = work / (name + '-artifacts')
        runtime = 'dotnet' if project.startswith('tvos/') else sdk
        run([runtime, 'build', ROOT / project, '--nologo', '--artifacts-path', artifact],
            name + '-build', Path('/private/tmp'))
        dlls = list((artifact / 'bin').rglob(name + '.dll'))
        if len(dlls) != 1:
            raise ValueError('one current test assembly is required: ' + name)
        run([runtime, dlls[0], *arguments], name)

    test('tools/AppleEverestBuilder/tests/AppleEverestBuilder.Tests.csproj',
         'AppleEverestBuilder.Tests', [ROOT])
    test('tools/AppleEverestBuilder/tests/HookSemantics/AppleEverestHookSemantics.Tests.csproj',
         'AppleEverestHookSemantics.Tests')
    # The desktop runner uses the default builder assets directory, independently
    # of the isolated test artifacts above.
    run([sdk, 'restore', ROOT / 'tools/AppleEverestBuilder/AppleEverestBuilder.csproj',
         '--locked-mode', '--nologo'], 'desktop-builder-restore', Path('/private/tmp'))
    run([ROOT / 'scripts/test-apple-everest-desktop-hookgen.sh'], 'desktop-hookgen')
    run(['python3', ROOT / 'scripts/test-apple-everest-stage25kl-evidence-controls.py',
         '--product-root', args.product_root, '--output', work / 'evidence-controls.json'],
        'current-evidence-rejection-controls')
    for name in ['SaveManagerPairingTests', 'SaveManagerProtocolTests',
                 'SaveManagerContinuityTests', 'SoftReloadTests']:
        test('tvos/' + name + '/' + name + '.csproj', name)
    run(['python3', ROOT / 'scripts/verify-celeste-input-profiles.py', '--repo-root', ROOT],
        'input-profiles')
    for stage in ['kd', 'ke', 'ib']:
        run(['python3', ROOT / ('scripts/verify-apple-everest-stage25' + stage + '.py')],
            'current-stage25' + stage)
    for stage in ['kc', 'kf', 'kh', 'ka', 'kb', 'hd', 'hc', 'hb', 'h', 'fb', 'f', 'e', 'd']:
        run(['python3', ROOT / ('scripts/verify-apple-everest-stage25' + stage + '.py')],
            'historical-contract-stage25' + stage, required=False)
    run(['git', 'diff', '--check'], 'diff-check')
    run(['git', 'submodule', 'foreach', '--recursive', '--quiet',
         'test -z "$(git status --porcelain)"'], 'recursive-submodule-cleanliness')

    protected_paths = ['native', 'tvos', 'FNA', 'scripts/prepare-celeste-ios-runtime.sh',
                       'modern-ios/patches', 'modern-ios/Info.plist']
    if git('diff', '--name-only', START, revision, '--', *protected_paths):
        raise ValueError('accepted canonical/native source or policy changed')
    locks = {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
             for name in git('ls-files', 'native/*lock.json', 'tvos/*lock.json').splitlines()}
    changed = git('diff', '--name-only', START, revision).splitlines()
    links = 0
    for name in changed:
        path = ROOT / name
        if not path.is_file():
            continue
        if path.suffix.lower() in {'.zip', '.dll', '.bin', '.bank', '.ipa', '.p12', '.mobileprovision'}:
            raise ValueError('private or third-party bytes entered source: ' + name)
        value = path.read_text(errors='replace')
        if re.search(r'/Users/[A-Za-z0-9_-]+/|BEGIN (?:RSA |EC )?PRIVATE KEY', value):
            raise ValueError('private data in changed tracked text: ' + name)
        if path.suffix == '.md':
            for target in re.findall(r'(?<!!)\[[^\]]+\]\(([^)]+)\)', value):
                parts = urlsplit(target.strip('<>'))
                if parts.scheme or not parts.path:
                    continue
                if not (path.parent / unquote(parts.path)).resolve().exists():
                    raise ValueError('broken local documentation link in ' + name + ': ' + target)
                links += 1
    if git('rev-parse', 'HEAD') != revision or git('status', '--porcelain'):
        raise ValueError('source changed during final regression')
    evidence = {'schemaVersion': 1, 'stage': '25K-L', 'sourceCommit': revision,
                'sourceTreeDirty': False, 'requiredCurrentPassed': True,
                'historicalResultsDistinguished': True, 'results': records,
                'historicalAuthority': 'Fresh attempts at immutable older contracts are distinguished from accepted historical PASS. Frozen literal counts are not edited.',
                'currentMechanismCoverage': 'Builder tests cover current K-J/K-H/K-F/K-E/K-D, frozen IL, progression and AEVPSV1. Actual factory/profile/native proof and physical interactions remain separate gates.',
                'canonicalNativeUnchangedFrom': START, 'nativeLockSha256': locks,
                'privacy': 'PASS', 'localDocumentationLinksChecked': links,
                'recursiveSubmodulesClean': True, 'githubActionsInvoked': 0}
    (work / 'regression-evidence.json').write_text(json.dumps(evidence, indent=2, sort_keys=True) + '\n')
    print('PASS: current exact-revision regressions; historical contracts reported separately', flush=True)


if __name__ == '__main__':
    main()
