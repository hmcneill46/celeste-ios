#!/usr/bin/env python3
"""Prepare six authored K-J comparison maps with unmodified desktop helpers."""
import argparse
import hashlib
import json
import os
import re
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
HELPERS = ['CherryHelper', 'PandorasBox', 'FancyTileEntities', 'BrokemiaHelper',
           'HonlyHelper', 'VivHelper', 'XaphanHelper', 'CollabUtils2']
PREFIX = 'AppleEverestStage25KJ'
SIDS = [PREFIX + '/FactoryProfiles/' + name for name in ['CrystalCave', 'WaterGarden', 'CameraCorridor']]
SIDS += [PREFIX + suffix for suffix in ['/0-Lobbies/1-Fixture', '/1-Fixture/1-FinishA', '/1-Fixture/2-FinishB']]


def sha(value):
    return hashlib.sha256(value).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package-root', required=True, type=Path)
    parser.add_argument('--closure', required=True, type=Path)
    parser.add_argument('--vanilla-app', type=Path, default=Path.home() / 'Downloads/Celeste.app')
    args = parser.parse_args()
    output = ROOT / '.build/apple-everest/stage25kj/macos-reference'
    if output.exists():
        parser.error('use the existing reference or remove its owned disposable tree explicitly before preparing again')
    pins = {row['name']: row for row in json.loads((ROOT / 'apple-everest/strawberry-jam-dependency-graph-stage25kc.json').read_text())['nodes']}
    for name in HELPERS + ['StrawberryJam2021']:
        package = args.package_root / (name + '.zip')
        if sha(package.read_bytes()) != pins[name]['zipSha256']:
            parser.error('exact public package identity differs: ' + name)
        if name != 'StrawberryJam2021':
            with zipfile.ZipFile(package) as archive:
                if any(path.lower().startswith('maps/') and path.lower().endswith('.bin') for path in archive.namelist()):
                    parser.error('unexpected gameplay map in reviewed helper: ' + name)
    for sid in SIDS:
        if not (args.closure / 'content/Content/Maps' / (sid + '.bin')).is_file():
            parser.error('compiled authored comparison map absent: ' + sid)
    upstream = ROOT / '.build/apple-everest/upstream/Everest'
    installer = upstream / 'MiniInstaller/bin/Release/net8.0/publish'
    everest = upstream / 'Celeste.Mod.mm/bin/Release/net8.0/publish'
    dotnet = ROOT / '.build/apple-everest/toolchain/dotnet8'
    for path in [args.vanilla_app, installer, everest, dotnet / 'dotnet']:
        if not path.exists():
            parser.error('prepared engine input absent: ' + str(path))
    (output / 'game').mkdir(parents=True)
    (output / '.stage25kj-owned-reference').write_text('Disposable project-owned six-map comparison environment\n')
    app = output / 'game/Celeste.app'
    subprocess.run(['cp', '-cR', str(args.vanilla_app), str(app)], check=True)
    resources = app / 'Contents/Resources'
    for source in [installer, everest]:
        subprocess.run(['ditto', str(source), str(resources)], check=True)
    env = dict(os.environ, DOTNET_ROOT=str(dotnet), PATH=str(dotnet) + ':' + os.environ['PATH'])
    with (output / 'prepare.log').open('w') as log:
        subprocess.run([str(dotnet / 'dotnet'), 'MiniInstaller.dll'], cwd=resources, env=env,
                       stdout=log, stderr=subprocess.STDOUT, check=True)
    mods = resources / 'Mods'
    if mods.exists():
        shutil.rmtree(mods)
    mods.mkdir()
    for name in HELPERS:
        shutil.copy2(args.package_root / (name + '.zip'), mods / (name + '.zip'))
    projection = mods / 'AppleEverestStage25KJReferenceAssets'
    projection.mkdir()
    (projection / 'everest.yaml').write_text('- Name: AppleEverestStage25KJReferenceAssets\n  Version: 1.0.0\n')
    records = []
    with zipfile.ZipFile(args.package_root / 'StrawberryJam2021.zip') as archive:
        names = set(archive.namelist())
        selected = set()
        for bank in ['ForegroundTiles', 'AnimatedTiles', 'Sprites']:
            name = 'Graphics/SJ2021xmls/BeginnerLobby/' + bank + '.xml'
            selected.add(name)
            tree = ET.fromstring(archive.read(name))
            if bank == 'ForegroundTiles':
                for tile in tree:
                    candidate = 'Graphics/Atlases/Gameplay/tilesets/' + tile.attrib['path'] + '.png'
                    if candidate in names:
                        selected.add(candidate)
            else:
                prefixes = [sprite.attrib['path'] for sprite in tree] if bank == 'AnimatedTiles' else [
                    sprite.attrib['path'] + animation.attrib['path'] for sprite in tree for animation in sprite
                    if animation.tag in ['Loop', 'Anim']]
                for prefix in prefixes:
                    pattern = re.compile(re.escape('Graphics/Atlases/Gameplay/' + prefix) + r'\d*\.png$')
                    selected.update(name for name in names if pattern.fullmatch(name))
        if len([name for name in selected if name.endswith('.png')]) != 88:
            raise ValueError('reviewed source XML asset projection differs from 88 exact PNGs')
        for name in sorted(selected):
            data = archive.read(name)
            path = projection / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            records.append({'path': name, 'sha256': sha(data), 'bytes': len(data)})
    fixture = mods / 'AppleEverestStage25KJReferenceFixtures'
    fixture.mkdir()
    dependencies = ['AppleEverestStage25KJReferenceAssets'] + HELPERS
    yaml = ['- Name: AppleEverestStage25KJReferenceFixtures', '  Version: 1.0.0', '  Dependencies:']
    for name in dependencies:
        yaml += ['    - Name: ' + name, '      Version: ' + (pins[name]['resolvedVersion'] if name in pins else '1.0.0')]
    (fixture / 'everest.yaml').write_text('\n'.join(yaml) + '\n')
    for sid in SIDS:
        data = (args.closure / 'content/Content/Maps' / (sid + '.bin')).read_bytes()
        path = fixture / 'Maps' / (sid + '.bin')
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        records.append({'path': 'Maps/' + sid + '.bin', 'sha256': sha(data), 'bytes': len(data)})
    source = ROOT / 'apple-everest/canaries/stage25kj-interactions'
    for directory in ['Dialog', 'Graphics']:
        shutil.copytree(source / directory, fixture / directory)
    shutil.copy2(source / 'CollabUtils2CollabID.txt', fixture / 'CollabUtils2CollabID.txt')
    metadata = Path('Maps') / (PREFIX + '/0-Lobbies/1-Fixture.meta.yaml')
    shutil.copy2(source / metadata, fixture / metadata)
    actual_maps = sorted(path.relative_to(fixture / 'Maps').as_posix()[:-4] for path in (fixture / 'Maps').rglob('*.bin'))
    if actual_maps != sorted(SIDS) or any((mods / name).exists() for name in ['StrawberryJam2021.zip', 'StrawberryJam2021']):
        raise ValueError('reference map/module boundary differs')
    for directory in ['state', 'tmp']:
        (output / directory).mkdir()
    (output / 'reference-evidence.json').write_text(json.dumps({'schemaVersion': 1,
        'engineProfile': 'stable-1.6458.0', 'authoredMapSids': SIDS, 'originalSjGameplayMaps': 0,
        'sjModuleMounted': False, 'thirdPartyYamlModified': False,
        'helperZipSha256': {name: pins[name]['zipSha256'] for name in HELPERS}, 'projectedSourceFiles': records,
        'knownSourceTemplateFallback': 'tilesets/subfolder/betterTemplate uses pinned Everest fallback; no alias invented'}, indent=2) + '\n')
    subprocess.run(['xattr', '-dr', 'com.apple.quarantine', str(app)], check=True)
    print('PASS: six authored reference maps, eight exact helpers, 88 exact source PNGs; no original SJ gameplay maps or SJ module')


if __name__ == '__main__':
    main()
