#!/usr/bin/env python3
"""Resolve every generated chapter icon against the actual locked and mounted GUI assets."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('controller_atlas', ROOT / 'scripts/inventory-celeste-controller-prompts.py')
atlas = importlib.util.module_from_spec(spec)
spec.loader.exec_module(atlas)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def resolve(rows, assets):
    result = []
    for sid, icon in rows:
        # The pinned vanilla AreaData[0], used by RegisterAreas for this sentinel.
        resolved = 'areas/intro' if icon == 'areas/null' else icon
        if resolved.lower() not in assets:
            raise ValueError('missing chapter icon: ' + sid + ' -> ' + resolved)
        result.append({'sid': sid, 'authoredIcon': icon, 'resolvedIcon': resolved})
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--closure', required=True, type=Path)
    parser.add_argument('--content-root', required=True, type=Path)
    parser.add_argument('--packaged', action='store_true', help='Require mounted assets inside this actual product')
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    manifest = json.loads((args.closure / 'compatibility-manifest.json').read_text())
    progression = args.closure / 'levelset-progression-manifest.txt'
    if sha(progression) != manifest['levelSetProgressionManifestSha256']:
        raise ValueError('chapter icon manifest identity mismatch')
    lines = progression.read_text().splitlines()
    if lines[0] != 'APPLE_EVEREST_LEVELSET_PROGRESSION_V1':
        raise ValueError('unsupported chapter icon manifest schema')
    rows = []
    for line in lines[1:]:
        fields = line.split('\t')
        if len(fields) != 12:
            raise ValueError('invalid chapter icon manifest row')
        rows.append((fields[0], fields[11].split('|')[0]))
    generated = (args.closure / 'managed/GeneratedAppleEverestProgressionManifest.cs').read_text()
    compiled_rows = re.findall(r'new AppleEverestMapProgressionDescriptor\("([^"\\]*)"[^\n]*?new AppleEverestMapPresentationDescriptor\("([^"\\]*)"', generated)
    if rows != compiled_rows or len(rows) != manifest['levelSetProgressionMapCount']:
        raise ValueError('generated chapter icon descriptors differ from the manifest')
    lock = json.loads((ROOT / 'managed/celeste-controller-prompts.lock.json').read_text())['atlas']
    metadata = args.content_root / 'Graphics/Atlases/Gui.meta'
    if sha(metadata) != lock['sha256']:
        raise ValueError('chapter icon GUI atlas is not the locked canonical atlas')
    assets = {key.lower() for key in atlas.parse_paths(metadata.read_bytes())}
    prefix = 'Graphics/Atlases/Gui/'
    for mount in manifest['contentMounts']:
        source = mount['sourcePath']
        if source.startswith(prefix) and source.endswith('.png'):
            base = args.content_root if args.packaged else args.closure / 'content/Content'
            if sha(base / mount['logicalPath']) != mount['sha256']:
                raise ValueError('mounted chapter icon GUI asset identity mismatch')
            assets.add(source[len(prefix):-4].lower())
    resolved = resolve(rows, assets)
    controls = []
    for name, bad_rows, bad_assets in [
        ('build40-missing-areas-0', [('control', 'areas/0')], assets),
        ('missing-vanilla-fallback', [('control', 'areas/null')], assets - {'areas/intro'})
    ]:
        try:
            resolve(bad_rows, bad_assets)
        except ValueError as error:
            if not str(error).startswith('missing chapter icon:'):
                raise
            controls.append(name)
        else:
            raise ValueError('chapter icon corruption control was accepted: ' + name)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps({'status': 'PASS', 'sharedClosureSha256': manifest['sharedClosureSha256'],
        'guiAtlasSha256': sha(metadata), 'packagedAssetsVerified': args.packaged,
        'maps': resolved, 'rejectedControls': controls}, indent=2, sort_keys=True) + '\n')
    print('PASS: all ' + str(len(rows)) + ' generated chapter icons resolve; two missing-asset controls reject')


if __name__ == '__main__':
    main()
