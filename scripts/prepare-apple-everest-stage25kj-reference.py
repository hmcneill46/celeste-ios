#!/usr/bin/env python3
"""Prepare authored K-J comparison maps with original desktop helper behavior."""
import argparse
import hashlib
import json
import os
import re
from pathlib import Path
import shutil
import shlex
import subprocess
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
HELPERS = sorted({row['provider'] for row in json.loads(
    (ROOT / 'apple-everest/sj-factory-authored-profiles-stage25kj.json').read_text())['factories']}
    - {'EverestCore', 'StrawberryJam2021'})
PREFIX = 'AppleEverestStage25KJ'
MAP_SOURCES = {}
for folder in ['stage25kj/Content', 'stage25kj-interactions', 'stage25ke/Content', 'stage25kf/Content', 'stage25kh/Content']:
    maps = ROOT / 'apple-everest/canaries' / folder / 'Maps'
    MAP_SOURCES.update({path.relative_to(maps).as_posix()[:-4]: path for path in maps.rglob('*.xml')})
SIDS = sorted(MAP_SOURCES)


def sha(value):
    return hashlib.sha256(value).hexdigest()


def file_sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package-root', required=True, type=Path)
    parser.add_argument('--closure', required=True, type=Path)
    parser.add_argument('--presentation-root', type=Path, default=ROOT / '.build/apple-everest/stage25kc/packages')
    parser.add_argument('--vanilla-app', type=Path, default=Path.home() / 'Downloads/Celeste.app')
    parser.add_argument('--refresh', action='store_true', help='refresh only the marked owned reference Mods; retain its saves/runtime')
    args = parser.parse_args()
    output = ROOT / '.build/apple-everest/stage25kj/macos-reference'
    existing = output.exists()
    if existing and not args.refresh:
        parser.error('use the existing reference or remove its owned disposable tree explicitly before preparing again')
    if existing and not (output / '.stage25kj-owned-reference').is_file():
        parser.error('refusing to refresh an unmarked reference')
    pins = {row['name']: row for row in json.loads((ROOT / 'apple-everest/strawberry-jam-dependency-graph-stage25kc.json').read_text())['nodes']}
    for name in HELPERS + ['StrawberryJam2021']:
        package = args.package_root / (name + '.zip')
        if file_sha(package) != pins[name]['zipSha256']:
            parser.error('exact public package identity differs: ' + name)
        if name != 'StrawberryJam2021':
            with zipfile.ZipFile(package) as archive:
                if any(path.lower().startswith('maps/') and path.lower().endswith('.bin') for path in archive.namelist()):
                    parser.error('unexpected gameplay map in reviewed helper: ' + name)
    audio_packages = {'StrawberryJam2021': args.package_root / 'StrawberryJam2021.zip',
        **{name: args.presentation_root / (name + '.zip') for name in ['StrawberryJam2021AudioA', 'StrawberryJam2021AudioB']}}
    for name, path in audio_packages.items():
        if file_sha(path) != pins[name]['zipSha256']:
            parser.error('exact reference audio package identity differs: ' + name)
    if not (args.closure / '.apple-everest-static-closure').is_file():
        parser.error('owned source content closure is absent')
    upstream = ROOT / '.build/apple-everest/upstream/Everest'
    installer = upstream / 'MiniInstaller/bin/Release/net8.0/publish'
    everest = upstream / 'Celeste.Mod.mm/bin/Release/net8.0/publish'
    dotnet = ROOT / '.build/apple-everest/toolchain/dotnet8'
    for path in [args.vanilla_app, installer, everest, dotnet / 'dotnet']:
        if not path.exists():
            parser.error('prepared engine input absent: ' + str(path))
    (output / 'game').mkdir(parents=True, exist_ok=True)
    (output / '.stage25kj-owned-reference').write_text('Disposable project-owned authored comparison environment\n')
    app = output / 'game/Celeste.app'
    if not existing:
        subprocess.run(['cp', '-cR', str(args.vanilla_app), str(app)], check=True)
    resources = app / 'Contents/Resources'
    env = dict(os.environ, DOTNET_ROOT=str(dotnet), PATH=str(dotnet) + ':' + os.environ['PATH'])
    if not existing:
        for source in [installer, everest]:
            subprocess.run(['ditto', str(source), str(resources)], check=True)
        with (output / 'prepare.log').open('w') as log:
            subprocess.run([str(dotnet / 'dotnet'), 'MiniInstaller.dll'], cwd=resources, env=env,
                           stdout=log, stderr=subprocess.STDOUT, check=True)
    subprocess.run(['dotnet', 'build', str(ROOT / 'tools/AppleEverestBuilder/AppleEverestBuilder.csproj'),
                    '-c', 'Release', '--no-restore', '--nologo'], cwd=ROOT, check=True)
    builder = [str(dotnet / 'dotnet'), str(ROOT / 'tools/AppleEverestBuilder/bin/Release/net8.0/AppleEverestBuilder.dll')]
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
        jar_source = 'Graphics/StrawberryJam2021/CustomEntitySprites.xml'
        jar = ET.fromstring(archive.read(jar_source)).find('jamJar_beginner')
        if jar is None:
            raise ValueError('original beginner jam jar sprite definition absent')
        jar_bank = ET.Element('Sprites')
        jar_bank.append(jar)
        jar_path = 'Graphics/AppleEverestStage25KJ/JarSprites.xml'
        jar_data = ET.tostring(jar_bank, encoding='utf-8')
        (projection / jar_path).parent.mkdir(parents=True, exist_ok=True)
        (projection / jar_path).write_bytes(jar_data)
        records.append({'path': jar_path, 'sha256': sha(jar_data), 'bytes': len(jar_data),
            'source': jar_source, 'sourceSha256': sha(archive.read(jar_source)),
            'projection': 'unchanged jamJar_beginner XML element only'})
        for animation in jar:
            if animation.tag in ['Loop', 'Anim']:
                prefix = 'Graphics/Atlases/Gameplay/' + jar.attrib['path'] + animation.attrib['path']
                pattern = re.compile(re.escape(prefix) + r'\d*\.png$')
                selected.update(name for name in names if pattern.fullmatch(name))
        dialog_source = archive.read('Dialog/English.txt')
        dialog_text = dialog_source.decode('utf-8-sig')
        dialog_keys = ['SJ2021_lobby_gym_tutorial_controls', 'SJ2021_lobby_gym_tutorial_info',
            'StrawberryJam2021_0_Lobbies_1_Beginner_Credits']
        dialog_entries = []
        for key in dialog_keys:
            match = re.search(r'^' + re.escape(key) + r'\s*=.*?(?=^[A-Za-z0-9_]+\s*=|\Z)',
                dialog_text, re.I | re.M | re.S)
            if match is None:
                raise ValueError('required original reference dialogue absent: ' + key)
            dialog_entries.append(match.group(0).rstrip())
        dialog_data = ('\n\n'.join(dialog_entries) + '\n').encode('utf-8')
        (projection / 'Dialog').mkdir()
        (projection / 'Dialog/English.txt').write_bytes(dialog_data)
        records.append({'path': 'Dialog/English.txt', 'sha256': sha(dialog_data), 'bytes': len(dialog_data),
            'sourceSha256': sha(dialog_source), 'selectedKeys': dialog_keys})
        # XML traversal cannot discover entity attributes such as warpSpritePath.
        # Also include the exact SJ assets already selected by production, with
        # bytes taken independently from the public source archive.
        manifest = json.loads((args.closure / 'compatibility-manifest.json').read_text())
        for mount in manifest['contentMounts']:
            name = mount['sourcePath']
            if mount['owner'] == 'StrawberryJam2021' and name.startswith('Graphics/') and name in names:
                selected.add(name)
        for name in sorted(selected):
            data = archive.read(name)
            path = projection / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            records.append({'path': name, 'sha256': sha(data), 'bytes': len(data)})
    for owner, banks in {'StrawberryJam2021': ['sj21_jamjars'],
            'StrawberryJam2021AudioA': ['sj21_bingovergoogle', 'sj21_shared'],
            'StrawberryJam2021AudioB': ['sj21_BegLobby']}.items():
        with zipfile.ZipFile(audio_packages[owner]) as archive:
            names = {name.lower(): name for name in archive.namelist()}
            for bank in banks:
                for suffix in ['.bank', '.guids.txt']:
                    name = names[('Audio/' + bank + suffix).lower()]
                    data = archive.read(name)
                    path = projection / name
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(data)
                    records.append({'path': name, 'owner': owner, 'sha256': sha(data), 'bytes': len(data)})
    fixture = mods / 'AppleEverestStage25KJReferenceFixtures'
    fixture.mkdir()
    capsule = mods / 'AppleEverestStage25KJSourceReference'
    capsule.mkdir()
    with zipfile.ZipFile(args.package_root / 'StrawberryJam2021.zip') as archive:
        source_dll = output / 'tmp/StrawberryJam2021.dll'
        source_dll.parent.mkdir(exist_ok=True)
        source_dll.write_bytes(archive.read('Code/StrawberryJam2021.dll'))
    subprocess.run(builder + ['project-desktop-reference', '--dll', str(source_dll),
        '--game', str(resources / 'Celeste.dll'), '--output', str(capsule / 'SelectedSource.dll'),
        '--evidence', str(output / 'selected-desktop-source-evidence.json')], check=True)
    utility = mods / 'AppleEverestStage25KJReferenceTools'
    utility.mkdir()
    utility_build = output / 'tmp/navigation'
    utility_build.mkdir(exist_ok=True)
    project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
    properties = ET.SubElement(project, 'PropertyGroup')
    for name, value in {'TargetFramework':'net8.0', 'EnableDefaultCompileItems':'false',
        'AssemblyName':'KJReferenceTools', 'Nullable':'disable', 'LangVersion':'12.0'}.items():
        ET.SubElement(properties, name).text = value
    items = ET.SubElement(project, 'ItemGroup')
    ET.SubElement(items, 'Compile', Include=str(ROOT / 'apple-everest/reference/Stage25KJReferenceTools.cs'))
    for name in ['Celeste', 'FNA']:
        reference = ET.SubElement(items, 'Reference', Include=name)
        ET.SubElement(reference, 'HintPath').text = str(resources / (name + '.dll'))
        ET.SubElement(reference, 'Private').text = 'false'
    project_path = utility_build / 'Navigation.csproj'
    ET.ElementTree(project).write(project_path, encoding='unicode')
    subprocess.run(['dotnet', 'build', str(project_path), '-c', 'Release', '--nologo'], cwd=ROOT, check=True)
    shutil.copy2(utility_build / 'bin/Release/net8.0/KJReferenceTools.dll', utility / 'KJReferenceTools.dll')
    (utility / 'everest.yaml').write_text('- Name: AppleEverestStage25KJReferenceTools\n  Version: 1.0.0\n'
        '  DLL: KJReferenceTools.dll\n  Dependencies:\n    - Name: Everest\n      Version: 1.6458.0\n')
    (capsule / 'everest.yaml').write_text('- Name: AppleEverestStage25KJSourceReference\n  Version: 1.0.0\n'
        '  DLL: SelectedSource.dll\n  Dependencies:\n    - Name: Everest\n      Version: 1.6458.0\n'
        '    - Name: MaxHelpingHand\n      Version: 1.40.9\n')
    dependencies = ['AppleEverestStage25KJReferenceAssets', 'AppleEverestStage25KJSourceReference'] + HELPERS
    yaml = ['- Name: AppleEverestStage25KJReferenceFixtures', '  Version: 1.0.0', '  Dependencies:']
    for name in dependencies:
        yaml += ['    - Name: ' + name, '      Version: ' + (pins[name]['resolvedVersion'] if name in pins else '1.0.0')]
    (fixture / 'everest.yaml').write_text('\n'.join(yaml) + '\n')
    map_differences = []
    for sid in SIDS:
        path = fixture / 'Maps' / (sid + '.bin')
        path.parent.mkdir(parents=True, exist_ok=True)
        source_xml = MAP_SOURCES[sid]
        if sid == 'AppleEverest/Stage25KE':
            # This marker introspects the Apple static-module state, which has
            # no desktop counterpart. Preserve every actual helper entity.
            tree = ET.parse(source_xml)
            for entities in tree.findall('./levels/level/entities'):
                for entity in list(entities):
                    if entity.get('name') == 'appleEverest/stage25keRootState': entities.remove(entity)
                    if entity.get('name') == 'SJ2021/GlowController':
                        # The vanilla HexColor accessor dereferences Values;
                        # this older fixture otherwise has no custom values.
                        # Explicitly spell the original constructor's default.
                        entity.set('lightWhitelist', '')
            source_xml = output / 'tmp/Stage25KE-reference.xml'
            tree.write(source_xml, encoding='unicode')
            map_differences.append({'sid': sid, 'omitted': 'appleEverest/stage25keRootState',
                'reason': 'Apple static-module state diagnostic; no desktop gameplay counterpart'})
            map_differences.append({'sid': sid, 'entity': 'SJ2021/GlowController',
                'explicitDefault': {'lightWhitelist': ''},
                'reason': 'Original default; prevents null EntityData.Values in vanilla HexColor'})
        if sid == 'AppleEverest/Stage25KF':
            tree = ET.parse(source_xml)
            for trigger in tree.findall('./levels/level/triggers/appleEverestTrigger'):
                if trigger.get('entitiesToAffect') == 'appleEverest/stage25kfDepthTarget':
                    trigger.set('entitiesToAffect', 'AppleEverest.Reference.ReferenceDepthTarget')
            source_xml = output / 'tmp/Stage25KF-reference.xml'
            tree.write(source_xml, encoding='unicode')
            map_differences.append({'sid': sid, 'entity': 'vitellary/editdepthtrigger',
                'targetType': 'AppleEverest.Reference.ReferenceDepthTarget',
                'reason': 'Original desktop helper resolves CLR type names; same project-owned depth probe'})
        subprocess.run(builder + ['compile-authored-reference-map', '--xml', str(source_xml),
            '--sid', sid, '--output', str(path)], check=True)
        data = path.read_bytes()
        records.append({'path': 'Maps/' + sid + '.bin', 'sha256': sha(data), 'bytes': len(data)})
    source = ROOT / 'apple-everest/canaries/stage25kj-interactions'
    for directory in ['Dialog', 'Graphics']:
        shutil.copytree(source / directory, fixture / directory)
    with (fixture / 'Dialog/English.txt').open('a') as dialog:
        dialog.write('\n' + (ROOT / 'apple-everest/canaries/stage25kh/Dialog/English.txt').read_text())
    shutil.copy2(source / 'CollabUtils2CollabID.txt', fixture / 'CollabUtils2CollabID.txt')
    metadata = Path('Maps') / (PREFIX + '/0-Lobbies/1-Fixture.meta.yaml')
    shutil.copy2(source / metadata, fixture / metadata)
    actual_maps = sorted(path.relative_to(fixture / 'Maps').as_posix()[:-4] for path in (fixture / 'Maps').rglob('*.bin'))
    if actual_maps != sorted(SIDS) or any((mods / name).exists() for name in ['StrawberryJam2021.zip', 'StrawberryJam2021']):
        raise ValueError('reference map/module boundary differs')
    for directory in ['state', 'tmp']:
        (output / directory).mkdir(exist_ok=True)
    launcher = output / 'Open Stage 25K-J Reference.command'
    launcher.write_text('#!/bin/zsh\nexec ' + shlex.quote(str(ROOT / 'scripts/run-apple-everest-stage25kj-reference.sh')) + '\n')
    launcher.chmod(0o755)
    (output / 'reference-evidence.json').write_text(json.dumps({'schemaVersion': 1,
        'engineProfile': 'stable-1.6458.0', 'authoredMapSids': SIDS, 'originalSjGameplayMaps': 0,
        'sjModuleMounted': False, 'selectedOriginalSjDesktopIl': True, 'thirdPartyYamlModified': False,
        'referenceMapDifferences': map_differences,
        'helperZipSha256': {name: pins[name]['zipSha256'] for name in HELPERS}, 'projectedSourceFiles': records,
        'knownSourceTemplateFallback': 'tilesets/subfolder/betterTemplate uses pinned Everest fallback; no alias invented'}, indent=2) + '\n')
    subprocess.run(['xattr', '-dr', 'com.apple.quarantine', str(app)], check=True)
    print(f'PASS: {len(SIDS)} authored reference maps, {len(HELPERS)} exact helpers, original selected SJ mask/glow/jar IL; no original gameplay maps')


if __name__ == '__main__':
    main()
