#!/usr/bin/env python3
"""Exercise the actual Sideways static worker, its locks and its idempotence."""
import argparse
from collections import Counter
import copy
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return json.loads(path.read_text())


def sha(text):
    return hashlib.sha256(text.encode()).hexdigest()


def plan_hash(transforms):
    rows = []
    for row in transforms:
        values = [row[k] for k in ['PlanId', 'AssemblySha256', 'EventType', 'EventName', 'TargetMethod',
            'CanonicalTargetMethod', 'ManipulatorType', 'ManipulatorMethod', 'ManipulatorIsStatic',
            'RegistrationOrdinal', 'BeforeSha256', 'AfterSha256', 'DiffSha256']]
        values.append(','.join(row['ExpectedDelegateTargets']))
        mechanism = row.get('Mechanism', 'HOOKGEN_IL_EVENT')
        if mechanism == 'HASH_LOCKED_STATIC_SEMANTIC_LOWERING':
            values += [mechanism, row['Lifetime']]
        elif mechanism == 'DIRECT_ILHOOK':
            values += [mechanism] + [row[k] for k in ['ConstructorSignature', 'TargetExpression',
                'ManipulatorExpression', 'Config', 'ApplyByDefault', 'Storage', 'Lifetime']]
        rows.append('\0'.join(str(v) for v in values))
    return sha('\n'.join(rows) + '\n')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--product-root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    product = args.product_root.resolve()
    host = product / 'shared-closure/host/static-il'
    plan = read(host / 'frozen-il-plan.json')
    if plan_hash(plan['transforms']) != plan['planSha256']:
        raise ValueError('independent plan serialization does not match actual worker plan')
    plans = {p['PlanId']: p for p in plan['transforms']}
    max_rows = [p for p in plans.values() if p['Owner'] == 'MaxHelpingHand']
    if len(plans) != 17 or len(max_rows) != 12 or sum(len(p['ExpectedDelegateTargets']) for p in max_rows) != 25:
        raise ValueError('Sideways/DJ transform census differs')
    runtime = product / 'preflight-runtime'
    evidence_roots = list(runtime.glob('obj/Release/*/apple-everest-static-il'))
    if len(evidence_roots) != 1:
        raise ValueError('expected one actual preflight freeze evidence directory')
    evidence_root = evidence_roots[0]
    evidence = [read(p) for p in sorted(evidence_root.glob('transform-*.json'))]
    seen = []
    for item in evidence:
        if item['alreadyFrozen'] or item['forbiddenReferences'] or item['planSha256'] != plan['planSha256']:
            raise ValueError('initial compile did not execute the actual locked freeze')
        for step in item['steps']:
            p = plans[step['PlanId']]
            if (step['Owner'] != p['Owner'] or step['RegistrationOrdinal'] != p['RegistrationOrdinal']
                    or step['BeforeSha256'] != p['BeforeSha256'] or step['AfterSha256'] != p['AfterSha256']
                    or step['NormalizedDiffSha256'] != p['DiffSha256']
                    or sha(step['BeforeNormalizedIl']) != p['BeforeSha256']
                    or sha(step['AfterNormalizedIl']) != p['AfterSha256'] or sha(step['NormalizedDiff']) != p['DiffSha256']
                    or Counter(d['Target'] for d in step['DelegateLowerings']) != Counter(p['ExpectedDelegateTargets'])):
                raise ValueError('actual freeze step differs from pinned plan: ' + p['PlanId'])
            seen.append(p['PlanId'])
    if Counter(seen) != Counter(plans.keys()):
        raise ValueError('actual freeze steps absent or duplicated')
    assemblies = list(runtime.glob('bin/Release/*/Celeste.dll'))
    if len(assemblies) != 1:
        raise ValueError('expected one actual compiled assembly')
    frozen = assemblies[0]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='kj-sideways-', dir=args.output.parent) as scratch:
        tmp = Path(scratch)
        shutil.copytree(host, tmp / 'host')
        control_host = tmp / 'host'
        control_plan = control_host / 'frozen-il-plan.json'
        worker = ['dotnet', str(host / 'AppleEverestIlWorker.dll')]
        def run(target, method, expect=None):
            command = worker + ['--target', str(target), '--output', str(tmp / 'out.dll'),
                '--plan', str(control_plan), '--target-method', method, '--manifest', str(tmp / 'evidence.json'),
                '--runtime-dir', str(frozen.parent), '--runtime-dir', str(host),
                '--runtime-dir', str(product / 'shared-closure/assemblies')]
            result = subprocess.run(command, capture_output=True, text=True)
            if expect:
                if result.returncode == 0 or expect not in result.stderr + result.stdout:
                    raise ValueError('worker did not reject intended control: ' + expect)
                return None
            if result.returncode:
                raise ValueError('actual worker positive failed: ' + result.stderr[-1500:])
            return read(tmp / 'evidence.json')
        idempotence = []
        for p in max_rows:
            item = run(frozen, p['TargetMethod'])
            if not item['alreadyFrozen'] or item['afterSha256'] != p['AfterSha256']:
                raise ValueError('Sideways idempotence failed')
            idempotence.append(p['PlanId'])
        p = next(row for row in max_rows if row['EventName'] == 'WallJumpCheck')
        # target-2 is the real compiler output after the retained DJ steps and
        # before WallJumpCheck. Do not bypass the composed DJ/Max target.
        baseline = evidence_root / 'target-2.dll'
        fresh = run(baseline, p['TargetMethod'])
        if fresh['alreadyFrozen'] or fresh['afterSha256'] != p['AfterSha256']:
            raise ValueError('fresh Sideways replay did not execute')
        rejected = []
        def changed(name, edit, reason):
            mutated = copy.deepcopy(plan)
            row = next(r for r in mutated['transforms'] if r['PlanId'] == p['PlanId'])
            edit(row)
            mutated['planSha256'] = plan_hash(mutated['transforms'])
            control_plan.write_text(json.dumps(mutated))
            run(baseline, row['TargetMethod'], reason)
            rejected.append(name)
            control_plan.write_text(json.dumps(plan))
        changed('WRONG_LIFETIME', lambda r: r.update(Lifetime='WRONG'), 'provenance/lifetime/helper')
        changed('WRONG_SOURCE_MANIPULATOR', lambda r: r.update(ManipulatorMethod='Wrong'), 'provenance/lifetime/helper')
        changed('MISSING_HELPER', lambda r: r.update(ExpectedDelegateTargets=[]), 'provenance/lifetime/helper')
        changed('EXTRA_HELPER', lambda r: r['ExpectedDelegateTargets'].append(r['ExpectedDelegateTargets'][0]), 'provenance/lifetime/helper')
        changed('WRONG_BEFORE_LOCK', lambda r: r.update(BeforeSha256='0' * 64), 'target baseline mismatch')
        changed('WRONG_AFTER_LOCK', lambda r: r.update(AfterSha256='0' * 64), 'transformed IL lock mismatch')
        changed('WRONG_DIFF_LOCK', lambda r: r.update(DiffSha256='0' * 64), 'transformed IL lock mismatch')
        changed('WRONG_TARGET_SIGNATURE', lambda r: r.update(TargetMethod=r['TargetMethod'].replace('Int32', 'Int64')), 'no matching element')
        fixture = control_host / 'fixtures/MaxHelpingHand.original.dll'
        data = fixture.read_bytes(); fixture.write_bytes(data[:-1] + bytes([data[-1] ^ 1]))
        run(baseline, p['TargetMethod'], 'manipulator fixture hash mismatch')
        rejected.append('WRONG_PACKAGE_BYTES'); fixture.write_bytes(data)
        subprocess.run(['dotnet', 'exec', '--fx-version', '10.0.10', str(ROOT / 'tools/AppleEverestBuilder/bin/Debug/net8.0/AppleEverestBuilder.dll'),
            'write-sideways-control-inputs', '--source', str(baseline), '--output', str(tmp)], check=True,
            stdout=subprocess.DEVNULL)
        for kind in ['collision-site', 'branch-boundary']:
            run(tmp / (kind + '.dll'), p['TargetMethod'], 'target baseline mismatch')
            rejected.append('CHANGED_' + kind.upper().replace('-', '_'))
    report = {'schemaVersion': 1, 'actualInitialTransforms': 17, 'selectedSidewaysMethods': 12,
        'selectedSidewaysSites': 25, 'exactInitialBeforeAfterDiffAndCalls': True,
        'composedDjChainPreserved': True, 'freshReplayExecuted': True,
        'alreadyFrozenMethods': idempotence, 'negativeControls': rejected,
        'mutatedPlanHashesRecomputed': True, 'actualTargetBytesMutated': ['collision-site', 'branch-boundary']}
    args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + '\n')
    print('PASS: Sideways 12 methods / 25 sites, fresh replay, 12 idempotent methods and 11 negatives')


if __name__ == '__main__':
    main()
