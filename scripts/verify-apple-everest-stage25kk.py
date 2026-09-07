#!/usr/bin/env python3
"""Verify the K-K fail-closed terrain diagnostic, not a gameplay PASS.

--require-ready intentionally fails for this frozen YELLOW stage. It must not
turn K-J's accepted factory census into support for the new tile mechanism.
"""
import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('kk_generator', ROOT / 'scripts/generate-apple-everest-stage25kk.py')
generator = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = generator
spec.loader.exec_module(generator)
START = generator.START
BRANCH = 'feature/apple-everest-first-sj-slice-retry2'


def read(path):
    return json.loads(path.read_text())


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args], text=True).strip()


def require_yellow(readiness, physical, terrain):
    if (readiness['status'] != generator.STATUS or readiness['aotAllowed'] or
            readiness['developmentIntegrationReady'] or readiness['readinessMarker'] is not None or
            readiness['allPlatformReleaseReady'] or readiness['realContentComposition'] != 'BLOCKED_NEW_VARIABLE_SCAN_AUTOTILER'):
        raise ValueError('unaccepted real-content mechanism cannot produce readiness')
    if (physical['iosIpaSha256'] is not None or physical['tvosIpaSha256'] is not None or
            physical['iphone'] != 'NOT_RUN_NO_KK_PRODUCT' or physical['appleTv'] != 'NOT_RUN_NO_KK_PRODUCT' or
            physical['ipad'] != generator.IPAD or physical['deviceDataTouched']):
        raise ValueError('blocked stage cannot claim physical acceptance')
    if (terrain['usedTileCount'] != 26 or terrain['maskCells'] != 25 or terrain['acceptedMaskCapacity'] != 9 or
            not terrain['cannotProjectToAccepted3x3'] or terrain['physicalCrashClaimed']):
        raise ValueError('unsupported terrain evidence changed')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packages-root', required=True, type=Path)
    parser.add_argument('--evidence-root', type=Path, default=ROOT / 'apple-everest')
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--baseline-preflight', type=Path)
    parser.add_argument('--compiled-mask-proof', type=Path)
    parser.add_argument('--accepted-autotiler-source', type=Path)
    parser.add_argument('--require-ready', action='store_true')
    parser.add_argument('--require-clean', action='store_true')
    args = parser.parse_args()
    output = args.output.resolve()
    if output.is_relative_to(ROOT) and subprocess.run(['git', '-C', str(ROOT), 'check-ignore', '-q', str(output)]).returncode:
        raise ValueError('machine output must be ignored or external')
    if git('ls-files', '--', str(output)):
        raise ValueError('machine verification cannot overwrite tracked evidence')
    output.parent.mkdir(parents=True, exist_ok=True)
    checks = []

    def require(ok, message):
        if not ok:
            raise ValueError(message)
        checks.append(message)

    require(git('branch', '--show-current') == BRANCH, 'authorized K-K branch')
    require(subprocess.run(['git', '-C', str(ROOT), 'merge-base', '--is-ancestor', START, 'HEAD']).returncode == 0,
            'accepted K-J ancestry')
    for revision in ['e0d7c1988a9e5a896001735e5fe21d2251446bd0', 'c28c38901fef2caafc18f2d35e17996a82e93966']:
        require(subprocess.run(['git', '-C', str(ROOT), 'merge-base', '--is-ancestor', revision, 'HEAD']).returncode == 1,
                'rejected diagnostic remains unmerged: ' + revision[:8])
    for ref, expected in {'tvos-port': START, 'origin/tvos-port': START,
            'ios-v0.1.1-rc.1^{}': '27e16b4724d94d3991b99c4795f680fcb0e5830c',
            'v1.0.0-rc.1^{}': 'ee52b0868df091746f134d95d4f020f94f23d4fb',
            'v1.0.0-rc.2^{}': '641e86e4ed164cdf93f602ce2f11436449654d6e',
            'origin/release/v1.0.0-rc.3': 'c8134c8ca7924cf12f48527e714b5242c6024927'}.items():
        require(git('rev-parse', ref) == expected, 'protected ref ' + ref)
    require(subprocess.run(['git', '-C', str(ROOT), 'rev-parse', '--verify', 'refs/tags/v1.0.0-rc.3'],
                          capture_output=True).returncode != 0, 'rc3 tag absent')
    for path in ['tools/AppleEverestBuilder', 'tools/AppleEverestIlWorker', 'apple-everest/runtime',
                 'apple-everest/canaries', 'modern-ios', 'tvos', 'managed', 'native', '.github/workflows',
                 'scripts/build-apple-everest-canary.sh']:
        require(not git('diff', START, '--', path), 'accepted implementation unchanged: ' + path)
    evidence = args.evidence_root.resolve()
    actual = generator.generate(args.packages_root.resolve())
    for name, value in actual.items():
        require(read(evidence / name) == value, 'independent public-input regeneration: ' + name)
    ready = actual['sj-beginner-production-readiness-stage25kk.json']
    physical = actual['sj-beginner-physical-stage25kk.json']
    terrain = actual['sj-beginner-terrain-stage25kk.json']
    require_yellow(ready, physical, terrain)
    checks.append('real terrain mechanism fails closed independently of 73 accepted factories')
    rejected = []
    controls = [
        ('factory-count-alone-is-not-real-slice-ready', 'ready', 'aotAllowed', True),
        ('false-development-green', 'ready', 'developmentIntegrationReady', True),
        ('false-readiness-marker', 'ready', 'readinessMarker', 'READY'),
        ('false-phone-pass', 'physical', 'iphone', 'PASS'),
        ('false-tv-pass', 'physical', 'appleTv', 'PASS'),
        ('deferred-ipad-is-not-pass', 'physical', 'ipad', 'PASS'),
        ('new-mask-size-is-not-accepted', 'terrain', 'acceptedMaskCapacity', 25),
        ('unused-template-substitution', 'terrain', 'usedTileCount', 0),
        ('source-analysis-is-not-device-crash', 'terrain', 'physicalCrashClaimed', True),
    ]
    for name, target, key, value in controls:
        mutated = copy.deepcopy({'ready': ready, 'physical': physical, 'terrain': terrain})
        mutated[target][key] = value
        try:
            require_yellow(mutated['ready'], mutated['physical'], mutated['terrain'])
        except ValueError:
            rejected.append(name)
        else:
            raise ValueError('corruption control accepted: ' + name)
    require(len(rejected) == 9, 'nine false-positive controls rejected')
    contract = read(ROOT / 'apple-everest/sj-autotiler-contract-stage25kk.json')
    receipt = read(ROOT / 'apple-everest/sj-kj-compiled-baseline-stage25kk.json')
    require(receipt['sourceCommitAtRun'] == START and contract['acceptedSourceCommit'] == START and
            receipt['scope'] == 'FRESH_REPRODUCTION_OF_ACCEPTED_KJ_CANARY_PROFILE_NOT_KK_REAL_CONTENT_PRODUCT' and
            receipt['realKkProductGenerated'] is False,
            'portable compiled-baseline receipt is explicitly scoped to accepted K-J')
    require(receipt['actualProductionRegenerated'] and receipt['exactPackageIdentityVerified'] and
            receipt['freshProductionCompilation'] and receipt['allImplementationBytesCompared'] and
            receipt['compiledDllCount'] == 6 and receipt['compiledMaskCapacity'] == 9,
            'portable receipt records actual fresh six-DLL baseline proof')
    require(receipt['compiledAssemblySha256'] == contract['compiledAssemblySha256'] and
            receipt['maskIlInspectionSha256'] == contract['compiledMaskConstructor']['ilInspectionSha256'] and
            receipt['sharedClosureSha256'] == contract['acceptedSharedClosureSha256'] and
            receipt['actualFactoryRegistrySha256'] == actual['sj-beginner-factory-closure-stage25kk.json']['acceptedKjGameplayRegistrySourceSha256'] and
            receipt['contentIdCensus'] == ready['contentIdCensus'] and
            receipt['registrationCensus'] == {'selected': 73, 'available': 73, 'unavailable': 0, 'providerRejected': 0},
            'portable compilation receipt binds exact baseline registries, gates and terrain IL')
    baseline = None
    if args.baseline_preflight:
        baseline = read(args.baseline_preflight)
        require(sha(args.baseline_preflight) == receipt['fullObservedPreflightSha256'],
                'observed full baseline preflight matches portable receipt')
        require(baseline['actualProductionRegenerated'] and baseline['exactPackageIdentityVerified'] and
                baseline['compilation']['FreshProductionCompilation'] and baseline['compilation']['AllImplementationBytesCompared'] and
                len(baseline['compilation']['CompiledDlls']) == 6,
                'fresh baseline package-backed production and all six compiled DLLs verified')
        require(baseline['sharedClosureSha256'] == contract['acceptedSharedClosureSha256'] and
                baseline['contentIdCensus'] == ready['contentIdCensus'] and
                baseline['registrationCensus'] == {'selected': 73, 'available': 73, 'unavailable': 0, 'providerRejected': 0},
                'actual compiled K-J baseline retains all selected gates')
        require(baseline['actualFactoryRegistrySha256'] == actual['sj-beginner-factory-closure-stage25kk.json']['acceptedKjGameplayRegistrySourceSha256'],
                'baseline factory proof matches actual generated selectors')
    if args.compiled_mask_proof:
        proof = read(args.compiled_mask_proof)
        require(sha(args.compiled_mask_proof) == receipt['maskIlInspectionSha256'] and
                proof['assemblySha256'] == receipt['compiledAssemblySha256'],
                'observed IL inspection matches portable compiled-baseline receipt')
        require(proof['method'] == 'System.Void Celeste.Autotiler/Masked::.ctor()', 'exact compiled mask constructor inspected')
        if baseline:
            require(proof['assemblySha256'] == baseline['compiledAssemblySha256'], 'mask IL bound to independently verified baseline assembly')
        instructions = proof['instructions']
        require(any(a['opcode'] == 'ldc.i4.s' and a['operand'] == '9' and
                    b['opcode'] == 'newarr' and b['operand'] == 'System.Byte' and
                    c['opcode'] == 'stfld' and c['operand'] == 'System.Byte[] Celeste.Autotiler/Masked::Mask'
                    for a, b, c in zip(instructions, instructions[1:], instructions[2:])), 'actual compiled mask allocates nine cells')
    if args.accepted_autotiler_source:
        require(sha(args.accepted_autotiler_source) == contract['acceptedAutotilerSourceSha256'], 'accepted runtime terrain source exact')
        source = args.accepted_autotiler_source.read_text()
        require('public byte[] Mask = new byte[9];' in source and 'private byte[] adjacent = new byte[9];' in source and
                'masked.Mask[num++] = 2;' in source and 'k < 9 && flag4' in source and 'ReadIntoCustomTemplate' not in source,
                'accepted parser and matcher retain fixed nine-cell behavior')
    require(not (ROOT / 'artifacts/apple-everest/stage25kk').exists(), 'no K-K Apple products created')
    require(not (ROOT / '.build/apple-everest/stage25kk/product').exists(), 'no K-K production closure or AOT tree created')
    changed = set(git('diff', START, '--name-only').splitlines()) | set(git('ls-files', '--others', '--exclude-standard').splitlines())
    allowed = {'apple-everest/' + name for name in actual} | {
        'apple-everest/sj-autotiler-contract-stage25kk.json',
        'apple-everest/sj-kj-accepted-semantic-stage25kk.json',
        'apple-everest/sj-kj-compiled-baseline-stage25kk.json',
        'scripts/fetch-apple-everest-stage25kk-inputs.py',
        'scripts/generate-apple-everest-stage25kk-content.py',
        'scripts/generate-apple-everest-stage25kk.py',
        'scripts/verify-apple-everest-stage25kk.py',
        'scripts/verify-apple-everest-stage25kk.sh',
        'docs/APPLE_EVEREST_COMPATIBILITY.md',
        'docs/history/stages/APPLE_EVEREST_FIRST_SJ_SLICE_STAGE25KK_REPORT.md',
    }
    require(changed <= allowed, 'only bounded diagnostic evidence, host scripts and documentation changed')
    for name in sorted(changed):
        path = ROOT / name
        if not path.is_file():
            continue
        require(path.suffix.lower() not in {'.zip', '.dll', '.bank', '.ipa', '.bin', '.p12', '.mobileprovision'}, 'no private input or product tracked: ' + name)
        text = path.read_text()
        require(not re.search(r'/Users/[A-Za-z0-9_-]+/|/private/tmp/[A-Za-z0-9_-]+|BEGIN (?:RSA |EC )?PRIVATE KEY', text), 'privacy: ' + name)
        if path.suffix == '.md':
            for target in re.findall(r'\]\(([^)]+)\)', text):
                target = target.strip('<>').split('#', 1)[0]
                if target and not re.match(r'^[a-z]+:', target) and not target.startswith('/'):
                    require((path.parent / target).exists(), 'local documentation link: ' + target)
    require(not any(line[0] in '-+U' for line in git('submodule', 'status', '--recursive').splitlines() if line), 'recursive submodule revisions exact')
    if args.require_clean:
        require(not git('status', '--porcelain'), 'worktree clean')
    result = {'schemaVersion': 1, 'stage': '25K-K', 'sourceCommit': git('rev-parse', 'HEAD'),
        'status': generator.STATUS, 'verificationResult': 'PASS_EXPECTED_YELLOW',
        'checksPassed': len(checks), 'checks': checks, 'rejectedControls': rejected,
        'acceptedKjGatesRetained': True, 'realSliceReady': False, 'aotAllowed': False,
        'readinessRequested': args.require_ready,
        'readinessRequestResult': 'REJECTED_UNACCEPTED_MECHANISM' if args.require_ready else 'NOT_REQUESTED',
        'freshBaselineCompilationVerified': baseline is not None,
        'actualCompiledMaskInspected': args.compiled_mask_proof is not None,
        'originalMapFilesModified': False, 'productsGenerated': False,
        'evidenceSha256': {name: sha(evidence / name) for name in actual},
        'ipad': generator.IPAD}
    output.write_text(json.dumps(result, indent=2, sort_keys=True) + '\n')
    if args.require_ready:
        raise ValueError('K-K is YELLOW: actual lobby requires unaccepted 5x5 autotiling; AOT is forbidden')
    print('PASS: K-K diagnostic (' + str(len(checks)) + ' checks; expected YELLOW; no product/readiness claim)')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, OSError) as error:
        print('FAIL: ' + str(error), file=sys.stderr)
        raise SystemExit(1)
