#!/usr/bin/env python3
"""Reproduce frozen K-L source in a fresh recursive clone with fresh public inputs."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
BRANCH = 'feature/apple-everest-first-sj-slice-outcome'


def sha(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def read(path):
    return json.loads(path.read_text())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work-root', type=Path, required=True)
    args = parser.parse_args()
    work = args.work_root.resolve()
    if not work.is_relative_to(ROOT / '.build/apple-everest') or work.exists():
        raise ValueError('a fresh owned ignored clone root is required')
    work.mkdir(parents=True)
    (work / '.stage25kl-disposable-clone').touch()
    clone = work / 'repo'
    env = dict(os.environ, MSBUILDDISABLENODEREUSE='1',
               DOTNET_CLI_USE_MSBUILD_SERVER='0', UseSharedCompilation='false')
    env.update(GIT_CONFIG_COUNT='1', GIT_CONFIG_KEY_0='url.https://github.com/.insteadOf',
               GIT_CONFIG_VALUE_0='git://github.com/')

    def git(*arguments, cwd=ROOT):
        return subprocess.check_output(['git', '-C', str(cwd), *arguments], env=env, text=True).strip()

    expected = git('rev-parse', 'HEAD')
    if git('branch', '--show-current') != BRANCH or git('status', '--porcelain'):
        raise ValueError('clean frozen K-L source branch required')
    public_origin = git('remote', 'get-url', 'origin')
    if public_origin != 'https://github.com/hmcneill46/celeste-ios.git':
        raise ValueError('expected public repository origin')

    def run(command, name, cwd=None):
        print('RUN: ' + name, flush=True)
        with (work / (name + '.log')).open('w') as log:
            result = subprocess.run([str(v) for v in command], cwd=cwd or clone, env=env,
                                    stdout=log, stderr=subprocess.STDOUT)
        if result.returncode:
            raise RuntimeError(name + ' failed; inspect its private log')

    def disk(phase):
        with (work / 'disk.jsonl').open('a') as stream:
            stream.write(json.dumps({'phase': phase, 'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
                                     'freeBytes': shutil.disk_usage(ROOT).free}) + '\n')

    disk('before-clone')
    # --no-local transfers only committed Git objects without hardlink reuse.
    # This permits exact-final reproduction before the authorized feature push.
    run(['git', 'clone', '--no-local', '--recursive', '--branch', BRANCH, ROOT, clone],
        'fresh-recursive-source-clone', ROOT)
    if git('rev-parse', 'HEAD', cwd=clone) != expected or git('status', '--porcelain', cwd=clone):
        raise ValueError('fresh clone is not the exact clean frozen revision')
    run(['git', 'remote', 'set-url', 'origin', public_origin], 'public-origin')
    run(['git', 'fetch', '--no-tags', 'origin',
         'refs/heads/tvos-port:refs/remotes/origin/tvos-port',
         'refs/heads/release/v1.0.0-rc.3:refs/remotes/origin/release/v1.0.0-rc.3'], 'protected-public-refs')
    run(['git', 'branch', 'tvos-port', 'origin/tvos-port'], 'local-baseline-ref')
    for commit in ['e0d7c1988a9e5a896001735e5fe21d2251446bd0',
                   'c28c38901fef2caafc18f2d35e17996a82e93966',
                   '9c951a373395a01212ac44c302688fdf16b32382']:
        if subprocess.run(['git', '-C', str(clone), 'cat-file', '-e', commit],
                          capture_output=True, env=env).returncode:
            run(['git', 'fetch', 'origin', commit], 'diagnostic-object-' + commit[:8])
    toolchain = clone / '.build/apple-everest/toolchain'
    toolchain.mkdir(parents=True)
    for sdk in ['dotnet8', 'dotnet9']:
        (toolchain / sdk).symlink_to(ROOT / '.build/apple-everest/toolchain' / sdk, target_is_directory=True)

    original = Path(env['CELESTE_GAME_ROOT']).resolve()
    if original.is_relative_to(ROOT / '.build'):
        raise ValueError('authoritative external user game input required; no ignored fixture copy')
    game_input = original
    finder = original / 'Content/.DS_Store'
    if finder.is_file():
        h = hashlib.sha256()
        count = size = 0
        for path in sorted((p for p in (original / 'Content').rglob('*') if p.is_file() and p != finder),
                           key=lambda p: p.relative_to(original / 'Content').as_posix()):
            relative = path.relative_to(original / 'Content').as_posix()
            length = path.stat().st_size
            h.update((relative + '\0' + str(length) + '\0' + sha(path) + '\n').encode())
            count += 1
            size += length
        if (count, size, h.hexdigest()) != (1216, 1158665183,
                '30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46'):
            raise ValueError('authoritative game content identity changed')
        game_input = clone / '.build/apple-everest/stage25kl/user-owned-game-input'
        game_input.parent.mkdir(parents=True)
        run(['cp', '-cR', original, game_input], 'authoritative-game-copy')
        (game_input / 'Content/.DS_Store').unlink()
        (work / 'canonical-source-acquisition.json').write_text(json.dumps({
            'authoritativeExternalUserInput': True, 'copiedIgnoredFixtures': False,
            'removedOnlyObservedFinderMetadataFromDisposableCopy': True,
            'contentFiles': count, 'contentBytes': size, 'contentLogicalSha256': h.hexdigest()}, indent=2) + '\n')
    run([clone / 'scripts/prepare-celeste-ios-runtime.sh', '--game-root', game_input], 'canonical-regeneration')

    stage = clone / '.build/ios-host'
    (stage / 'managed').mkdir(parents=True)
    (stage / 'fna').mkdir()
    pins = read(clone / 'tvos/fna-managed-sources.lock.json')['sources']
    urls = {'SDL2.cs': ('flibitijibibo/SDL2-CS', 'src/SDL2.cs'),
            'FAudio.cs': ('FNA-XNA/FAudio', 'csharp/FAudio.cs'),
            'Theorafile.cs': ('FNA-XNA/Theorafile', 'csharp/Theorafile.cs')}
    for name, (repo, path) in urls.items():
        url = 'https://raw.githubusercontent.com/' + repo + '/' + pins[name]['revision'] + '/' + path
        with urllib.request.urlopen(url, timeout=60) as response:
            (stage / 'managed' / name).write_bytes(response.read())
    for name, path in [('FNA3D.cs', 'Graphics/FNA3D.cs'), ('FrameworkDispatcher.cs', 'FrameworkDispatcher.cs')]:
        shutil.copyfile(clone / 'FNA/src' / path, stage / 'managed' / name)
    for name, pin in pins.items():
        if sha(stage / 'managed' / name) != pin['sha256']:
            raise ValueError('fresh public managed binding differs: ' + name)
    shutil.copytree(clone / 'FNA/src', stage / 'fna/src')
    for number, (target, patch) in enumerate([
            ('managed', 'native/patches/FNA/0001-map-apple-static-imports-to-internal.patch'),
            ('managed', 'tvos/patches/FNA/0002-disambiguate-mediaplayer-alias.patch'),
            ('fna', 'modern-ios/patches/FNA/0001-preserve-stable-touch-finger-ids.patch')]):
        run(['patch', '--batch', '--forward', '-p1', '-d', stage / target, '-i', clone / patch],
            'fresh-managed-patch-' + str(number))
    packages = clone / '.build/apple-everest/stage25kl/packages'
    run(['python3', clone / 'scripts/fetch-apple-everest-stage25kj-inputs.py',
         '--output', packages, '--include-presentation'], 'public-package-acquisition')
    prior = clone / '.build/apple-everest/stage25kl/prior-public'
    prior.mkdir()
    chrono = prior / 'ChronoHelper.zip'
    for url in ['https://celestemodupdater.0x0a.de/banana-mirror/1778580.zip',
                'https://gamebanana.com/mmdl/1778580']:
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers={
                    'User-Agent': 'AppleEverest-stage25kl-reproducer/1'}), timeout=60) as response, chrono.open('wb') as output:
                shutil.copyfileobj(response, output)
            if sha(chrono) != 'af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18':
                raise ValueError('exact public Chrono identity differs')
            break
        except (OSError, ValueError):
            if url.startswith('https://gamebanana.com'):
                raise
    product = clone / '.build/apple-everest/stage25kl/product'
    run(['python3', clone / 'scripts/build-apple-everest-stage25kl.py', '--package-root', packages,
         '--chrono-package', chrono, '--platform', 'ios', '--prepare-only'], 'four-gate-preflight')
    reproduction = clone / '.build/apple-everest/stage25kl/reproduction'
    run(['python3', clone / 'scripts/reproduce-apple-everest-stage25kl.py', '--package-root', packages,
         '--chrono-package', chrono, '--production-root', product, '--work-root', reproduction], 'three-fresh-closures')
    regressions = clone / '.build/apple-everest/stage25kl/regressions'
    run(['python3', clone / 'scripts/regress-apple-everest-stage25kl.py', '--work-root', regressions], 'current-regressions')
    run(['python3', clone / 'scripts/verify-apple-everest-stage25kl.py', '--product-root', product,
         '--reproduction-root', reproduction, '--regression-evidence', regressions / 'regression-evidence.json',
         '--require-clean', '--output', work / 'verifier.json'], 'final-source-static-verifier')
    if git('status', '--porcelain', cwd=clone) or git('rev-parse', 'HEAD', cwd=clone) != expected:
        raise ValueError('disposable source changed during reproduction')
    if git('rev-parse', 'HEAD') != expected or git('status', '--porcelain'):
        raise ValueError('calling source changed during reproduction')
    for name in ['production-preflight.json', 'compiled-canary-profiles.json', 'compiled-canary-guards.json']:
        shutil.copyfile(product / name, work / name)
    for name in ['readiness.json', 'autotiler-conformance.json', 'runtime-composition.json']:
        shutil.copyfile(product / 'real-composition' / name, work / name)
    shutil.copyfile(packages / 'acquisition.json', work / 'public-acquisition.json')
    shutil.copyfile(regressions / 'regression-evidence.json', work / 'regression-evidence.json')
    shutil.copytree(reproduction, work / 'reproduction')
    compilation = read(product / 'production-preflight.json')['compilation']
    manifest = read(product / 'shared-closure/compatibility-manifest.json')
    record = {'sourceCommit': expected, 'status': 'PASS', 'recursiveClone': True, 'worktreeClean': True,
              'sourceTransfer': 'GIT_CLONE_NO_LOCAL_RECURSIVE_AT_FROZEN_FEATURE_SHA',
              'copiedIgnoredFixtures': False, 'hostSdksReusedAsTools': True,
              'independentPublicAcquisition': True, 'sharedClosureSha256': manifest['sharedClosureSha256'],
              'appliedManagedSourceLogicalSha256': compilation['AppliedManagedSourceLogicalSha256'],
              'compiledDllCount': len(compilation['CompiledDlls']),
              'allImplementationBytesCompared': compilation['AllImplementationBytesCompared'],
              'fourGatesPassed': True, 'threeIndependentClosuresPassed': True, 'currentRegressionsPassed': True,
              'machineVerificationSha256': sha(work / 'verifier.json'),
              'publicAcquisitionSha256': sha(work / 'public-acquisition.json'),
              'physicalAcceptance': 'SEPARATE_EXACT_PRODUCT_GATE', 'githubActionsInvoked': 0}
    (work / 'fresh-clone-evidence.json').write_text(json.dumps(record, indent=2, sort_keys=True) + '\n')
    disk('before-clone-removal')
    if not (work / '.stage25kl-disposable-clone').is_file() or clone.parent != work:
        raise ValueError('refusing unmarked disposable cleanup')
    shutil.rmtree(clone)
    disk('after-clone-removal')
    print('PASS: exact-source fresh recursive clone; disposable checkout removed', flush=True)


if __name__ == '__main__':
    main()
