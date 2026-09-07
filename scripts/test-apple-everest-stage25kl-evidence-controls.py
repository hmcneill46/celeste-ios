#!/usr/bin/env python3
"""Exercise K-L evidence rejection controls on disposable host content copies."""
import argparse
import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--product-root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    spec = importlib.util.spec_from_file_location('kl_verify', ROOT / 'scripts/verify-apple-everest-stage25kl.py')
    verifier = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(verifier)
    revision = 'a' * 40
    manifest = {'sourceCommit': revision, 'ipaSha256': 'b' * 64}
    info = {'CFBundleShortVersionString': '0.1.1', 'CFBundleVersion': '42'}
    physical = {'authority': 'USER_REPORTED_EXACT_PRODUCT', 'sourceCommit': revision,
                'platform': 'ios', 'ipaSha256': 'b' * 64, 'appVersion': '0.1.1',
                'appBuild': '42', 'allFactoryLifecycleCount': 73}
    verifier.physical_identity(physical, 'ios', manifest, info, revision)
    rejected = []
    for field, value in [('authority', 'HOST_ASSERTION'), ('sourceCommit', 'c' * 40),
                         ('platform', 'tvos'), ('ipaSha256', 'd' * 64), ('appVersion', '0.1.0'),
                         ('appBuild', '41'), ('allFactoryLifecycleCount', 72)]:
        altered = copy.deepcopy(physical)
        altered[field] = value
        try:
            verifier.physical_identity(altered, 'ios', manifest, info, revision)
        except ValueError:
            rejected.append('PHYSICAL_' + field)
        else:
            raise ValueError('invalid physical identity accepted: ' + field)
    product = args.product_root.resolve()
    closure = product / 'shared-closure'
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='content-controls-', dir=args.output.parent) as temporary:
        app = Path(temporary) / 'HostContent.app'
        subprocess.run(['cp', '-cR', str(product / 'ios-runtime/content'), str(app)], check=True)
        command = ['python3', str(ROOT / 'scripts/verify-apple-everest-stage25kl-product-content.py'),
                   '--app', str(app), '--closure', str(closure), '--readiness',
                   str(product / 'real-composition/readiness.json'), '--output', str(Path(temporary) / 'result.json')]
        subprocess.run(command, stdout=subprocess.DEVNULL, check=True)
        controls = [('NESTED_EXCLUDED_BIN', 'Content/AppleEverest/Mods/Unexpected/Maps/StrawberryJam2021/1-Beginner/Excluded.bin'),
                    ('ROOT_EXCLUDED_BIN', 'Content/Maps/StrawberryJam2021/1-Beginner/Excluded.bin'),
                    ('SOURCE_PACKAGE_ARCHIVE', 'StrawberryJam2021.zip')]
        for label, name in controls:
            target = app / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(b'isolated negative control')
            result = subprocess.run(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            target.unlink()
            if result.returncode == 0:
                raise ValueError('invalid packaged content accepted: ' + label)
            rejected.append(label)
        mounts = json.loads((closure / 'compatibility-manifest.json').read_text())['contentMounts']
        target = app / 'Content' / next(m['logicalPath'] for m in mounts if m['sourcePath'].endswith('.png'))
        target.write_bytes(b'isolated changed texture bytes')
        result = subprocess.run(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if result.returncode == 0:
            raise ValueError('changed actual mounted content accepted')
        rejected.append('CHANGED_SELECTED_CONTENT_BYTES')
    report = {'stage': '25K-L', 'status': 'PASS', 'hostContentPositive': True,
              'physicalIdentityFunctionPositive': True, 'rejectedControls': rejected,
              'disposableCopiesOnly': True, 'actualSignedProductClaimed': False}
    args.output.write_text(json.dumps(report, indent=2) + '\n')
    print('PASS: host content and physical identity guards; ' + str(len(rejected)) + ' rejection controls')


if __name__ == '__main__':
    main()
