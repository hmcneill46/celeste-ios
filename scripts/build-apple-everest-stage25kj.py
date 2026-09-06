#!/usr/bin/env python3
"""Build the isolated K-J factory product after actual package/compile preflight."""
import argparse
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package-root', required=True, type=Path)
    parser.add_argument('--presentation-root', type=Path)
    parser.add_argument('--chrono-package', required=True, type=Path)
    args, product_args = parser.parse_known_args()
    valued = {'--platform', '--signing', '--team-id', '--ios-bundle-id', '--tvos-bundle-id',
              '--ios-device-id', '--tvos-device-id', '--work-root', '--output'}
    flags = {'--prepare-only', '--clean'}
    index = 0
    while index < len(product_args):
        option = product_args[index]
        if option in valued:
            if index + 1 == len(product_args) or product_args[index + 1].startswith('--'):
                parser.error('missing product option value: ' + option)
            index += 2
        elif option in flags:
            index += 1
        else:
            parser.error('K-J does not allow overriding inputs/gates or reusing an unbound AOT build: ' + option)
    profiles = ROOT / 'apple-everest/sj-factory-authored-profiles-stage25kj.json'
    providers = {row['provider'] for row in json.loads(profiles.read_text())['factories']} - {'EverestCore'}
    inputs = [args.package_root / (name + '.zip') for name in sorted(providers)]
    presentation = args.presentation_root or args.package_root
    inputs += [presentation / (name + '.zip') for name in
               ['StrawberryJam2021Assets', 'StrawberryJam2021AudioA', 'StrawberryJam2021AudioB']]
    inputs.append(args.chrono_package)
    inputs += [ROOT / 'apple-everest/canaries' / name for name in
               ['stage25ke', 'stage25kf', 'stage25kh', 'stage25kj', 'stage25kj-interactions']]
    for source in inputs:
        if not source.exists():
            parser.error('required exact input missing: ' + str(source))
    command = [str(ROOT / 'scripts/build-apple-everest-canary.sh'),
               '--work-root', str(ROOT / '.build/apple-everest/stage25kj/product'),
               '--output', str(ROOT / 'artifacts/apple-everest/stage25kj'),
               '--factory-preflight', str(ROOT / 'apple-everest/selected-factory-type-closure-stage25kh.json'),
               '--authored-factory-profiles', str(profiles)]
    for source in inputs:
        command += ['--mod', str(source.resolve())]
    # Do not echo signing/device arguments in a CalledProcessError traceback.
    raise SystemExit(subprocess.run(command + product_args, cwd=ROOT).returncode)


if __name__ == '__main__':
    main()
