#!/usr/bin/env python3
"""Reproduce the unchanged slice's pre-product terrain stop from pinned inputs.

This generates host evidence, never a playable closure or an Apple product.
The accepted 73-factory gate remains distinct from real-content composition.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
AE = ROOT / 'apple-everest'
START = 'e69bfd6eeba3d36a5d745c24b3bc0f2f3f4ee92f'
SEMANTIC_SHA = '0097f41ed546502765849d86f951925596b8ba0d61671a98ddd446cbe0f6f652'
FG_PATH = 'Graphics/SJ2021xmls/BeginnerLobby/ForegroundTiles.xml'
FG_SHA = '4d13bd8fc7164ddd7b20015e17dde1b7aaa035d292ac59b058e40d22c6876f8f'
STATUS = 'YELLOW_NEW_AUTOTILER_COMPATIBILITY_MECHANISM'
IPAD = 'IPADOS_PHYSICAL_DEFERRED_LEGACY_COMPATIBILITY_POLICY'


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / 'scripts' / filename)
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value


content = module('kk_content', 'generate-apple-everest-stage25kk-content.py')
profiles = module('kk_profiles', 'generate-apple-everest-stage25kj-profiles.py')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read(path):
    return json.loads(path.read_text())


def save(path, value):
    text = json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + '\n'
    if re.search(r'/Users/|/private/|/Volumes/|BEGIN .*PRIVATE KEY', text):
        raise ValueError('private material in stage evidence')
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text)


def children(element, name):
    return [row for row in element['children'] if row['name'] == name]


def terrain_analysis(tree, xml_data):
    if sha(xml_data) != FG_SHA:
        raise ValueError('original foreground XML identity differs')
    definitions = ET.fromstring(xml_data)
    tile = next(row for row in definitions if row.attrib.get('id') == 'J')
    width, height = int(tile.attrib['scanWidth']), int(tile.attrib['scanHeight'])
    masks = [row.attrib for row in tile if row.attrib['mask'] not in ['center', 'padding']]
    cells = [''.join(c for c in row['mask'] if c in '01xX') for row in masks]
    if (width, height, len(cells)) != (5, 5, 62) or any(len(row) != 25 for row in cells):
        raise ValueError('reviewed variable-scan terrain profile changed')
    occurrences = []
    room_data = {}
    for level in children(tree, 'levels')[0]['children']:
        name = level['attributes']['name'].removeprefix('lvl_')
        layers = children(level, 'solids')
        rows = layers[0]['attributes'].get('innerText', '').splitlines() if layers else []
        room_data[name] = rows
        origin = {key: level['attributes'].get(key, 0) for key in ['x', 'y']}
        for y, row in enumerate(rows):
            for x, value in enumerate(row):
                if value == 'J':
                    occurrences.append({'room': name, 'tileX': x, 'tileY': y,
                        'worldX': origin['x'] + x * 8, 'worldY': origin['y'] + y * 8})
    if len(occurrences) != 26 or {row['room'] for row in occurrences} != {'sj2021beginnerlobby'}:
        raise ValueError('used custom terrain census changed')
    rows = room_data['sj2021beginnerlobby']
    ignores = set(tile.attrib.get('ignores', '').split(','))

    def present(x, y):
        if not (0 <= y < len(rows) and 0 <= x < len(rows[y])):
            raise ValueError('counterexample unexpectedly depends on out-of-level behavior')
        return rows[y][x] not in {'0', '\0'} and rows[y][x] not in ignores

    def neighborhood(x, y, size):
        half = size // 2
        return ''.join('1' if present(x + dx, y + dy) else '0'
            for dy in range(-half, half + 1)
            for dx in range(-half, half + 1))

    special = {row.attrib['mask']: row.attrib['tiles'] for row in tile
               if row.attrib['mask'] in ['center', 'padding']}
    pair = []
    for x, y in [(446, 263), (448, 263)]:
        n3, n5 = neighborhood(x, y, 3), neighborhood(x, y, 5)
        cross2 = ''.join('1' if present(x + dx, y + dy) else '0'
                        for dx, dy in [(-2, 0), (2, 0), (0, -2), (0, 2)])
        if set(n5) == {'1'}:
            full_cross = all(present(x + dx, y + dy) for dx, dy in [(-3, 0), (3, 0), (0, -3), (0, 3)])
            tiles = special['center' if full_cross else 'padding']
            match = 'center' if full_cross else 'padding'
        else:
            matching = [(mask.count('x') + mask.count('X'), index, row)
                for index, (row, mask) in enumerate(zip(masks, cells))
                if all(a in 'xX' or a == b for a, b in zip(mask, n5))]
            if not matching:
                raise ValueError('counterexample no longer matches an authored template')
            _, _, chosen = min(matching)
            tiles, match = chosen['tiles'], chosen['mask']
        pair.append({'tileX': x, 'tileY': y, 'canonicalNeighborhood3x3': n3,
            'canonicalCrossDistance2': cross2, 'everestNeighborhood5x5': n5,
            'everestMatchedRule': match, 'everestTextureCoordinates': tiles})
    if (pair[0]['canonicalNeighborhood3x3'] != pair[1]['canonicalNeighborhood3x3'] or
            pair[0]['canonicalCrossDistance2'] != pair[1]['canonicalCrossDistance2'] or
            pair[0]['everestTextureCoordinates'] == pair[1]['everestTextureCoordinates']):
        raise ValueError('3x3 reduction counterexample was not established')
    return {'classification': 'NEW_EVEREST_VARIABLE_SCAN_AUTOTILER',
        'sourceXml': FG_PATH, 'sourceXmlBytes': len(xml_data), 'sourceXmlSha256': FG_SHA,
        'tileset': dict(tile.attrib), 'maskCount': len(cells), 'maskCells': width * height,
        'acceptedMaskCapacity': 9, 'firstOutOfBoundsWriteIndex': 9,
        'firstAuthoredMask': masks[0]['mask'], 'usedTiles': occurrences,
        'usedTileCount': len(occurrences), 'counterexample': pair,
        'failureAuthority': 'EXACT_AUTHORED_CONTENT_PLUS_ACCEPTED_COMPILED_CONTRACT_AND_SOURCE_ANALYSIS',
        'physicalCrashClaimed': False,
        'cannotProjectToAccepted3x3': True,
        'neededBehavior': ['variable-size mask parsing', 'variable-size neighborhood matching',
            'matching fill/padding semantics', 'exact custom tile metadata loading'],
        'stopRequiredByBrief': ['46', '53']}


def generate(packages_root):
    ledger_path = AE / 'sj-kj-accepted-semantic-stage25kk.json'
    if sha(ledger_path.read_bytes()) != SEMANTIC_SHA:
        raise ValueError('accepted K-J semantic identity differs')
    ledger = read(ledger_path)
    if ledger['census'] != {'selected': 73, 'fullyClosed': 73, 'blocked': 0, 'unknown': 0}:
        raise ValueError('accepted K-J semantic census is not closed')
    for row in ledger['sharedSourceEffects']:
        if sha((ROOT / row['path']).read_bytes()) != row['sha256']:
            raise ValueError('accepted shared runtime changed: ' + row['path'])
    contract = read(AE / 'sj-autotiler-contract-stage25kk.json')
    for path, expected in contract['runtimeSourceFiles'].items():
        if sha((ROOT / path).read_bytes()) != expected:
            raise ValueError('accepted Autotiler contract changed: ' + path)
    if contract['compiledMaskConstructor']['arrayLength'] != 9:
        raise ValueError('accepted compiled mask capacity changed')
    plan = content.generate(packages_root)
    authored = profiles.extract(packages_root / 'StrawberryJam2021.zip')
    if authored != read(AE / 'sj-factory-authored-profiles-stage25kj.json'):
        raise ValueError('real authored factory profiles differ from accepted K-J')
    pins = {row['name']: row for row in read(AE / 'strawberry-jam-dependency-graph-stage25kc.json')['nodes']}
    providers = []
    names = {row['provider'] for row in authored['factories']} - {'EverestCore'}
    for name in sorted(names | {row['name'] for row in plan['packages']}):
        pin = pins[name]
        path = packages_root / (name + '.zip')
        with path.open('rb') as stream:
            digest = content.digest_stream(stream)
        if digest != pin['zipSha256'] or path.stat().st_size != pin['zipBytes']:
            raise ValueError('actual package identity differs: ' + name)
        verified_dlls = []
        with zipfile.ZipFile(path) as archive:
            for dll in pin.get('distributedDlls', []):
                matches = [member for member in archive.namelist() if Path(member).name == dll['path']]
                if len(matches) != 1 or sha(archive.read(matches[0])) != dll['sha256']:
                    raise ValueError('actual distributed DLL differs: ' + name)
                verified_dlls.append({'path': matches[0], 'sha256': dll['sha256']})
        providers.append({'name': name, 'version': pin['resolvedVersion'], 'zipSha256': digest,
            'distributedDlls': verified_dlls, 'wholePackageCompatible': False})
    map_records, trees = {}, {}
    with zipfile.ZipFile(packages_root / 'StrawberryJam2021.zip') as archive:
        for label, original in zip(['lobby', 'bing'], authored['maps']):
            data = archive.read(original['member'])
            tree, boundary = profiles.parse(data)
            trees[label] = tree
            reader = profiles.kc.Reader(data)
            reader.string()
            package_label = reader.string()
            rooms = children(tree, 'levels')[0]['children']
            room_names = sorted(level['attributes']['name'].removeprefix('lvl_') for level in rooms)
            spawns = [{'room': level['attributes']['name'].removeprefix('lvl_'),
                'localX': entity['attributes']['x'], 'localY': entity['attributes']['y'],
                'worldX': level['attributes'].get('x', 0) + entity['attributes']['x'],
                'worldY': level['attributes'].get('y', 0) + entity['attributes']['y']}
                for level in rooms for group in children(level, 'entities') for entity in group['children']
                if entity['name'] == 'player']
            compatibility = sha(('apple-everest-progression-map-v1\n' + original['sid'] + '\n' +
                                 original['sha256'] + '\n' + '\n'.join(room_names)).encode())
            map_records[label] = {**original, 'sourcePackageLabel': package_label, 'rooms': room_names,
                'authoredPlayerSpawns': spawns, 'sourceDerivedCompatibilityId': compatibility,
                'compatibilityIdRegisteredInKkProduct': False, 'physicalRoom': None, 'physicalSpawn': None,
                'originalBytesModified': False, 'packagedInKkProduct': False}
        terrain = terrain_analysis(trees['lobby'], archive.read(FG_PATH))
        terrain['bingUsedJTiles'] = sum(layer['attributes'].get('innerText', '').count('J')
            for level in children(trees['bing'], 'levels')[0]['children'] for layer in children(level, 'solids'))
        if terrain['bingUsedJTiles']:
            raise ValueError('Bing terrain finding changed')
        selected = {row['path'] for package in plan['packages'] if package['name'] == 'StrawberryJam2021'
                    for row in package['includedFiles']}
        inventory = set(archive.namelist())
        extras = {}
        for bank in ['ForegroundTiles', 'BackgroundTiles']:
            for tile in ET.fromstring(archive.read('Graphics/SJ2021xmls/BeginnerLobby/' + bank + '.xml')):
                path = 'Graphics/Atlases/Gameplay/tilesets/' + tile.attrib['path'] + '.png'
                if path in inventory and path not in selected:
                    extras[path] = 'Referenced by complete ' + bank + ' constructor'
        for path in ['Graphics/Atlases/Gui/areas/SJ2021/meters/2-med.png',
                     'Graphics/ColorGrading/SJ2021/Hanky/ForestNight.png']:
            if path not in inventory or path in selected:
                raise ValueError('reviewed metadata asset delta changed')
            extras[path] = 'Authored map presentation metadata'
        for owner, path in re.findall(r'"([^"\\]+)\\0([^"\\]+)"', (ROOT / 'tools/AppleEverestBuilder/SelectedDecalContent.cs').read_text()):
            if owner == 'StrawberryJam2021' and path not in selected:
                if path not in inventory:
                    raise ValueError('accepted animation file missing from original package')
                extras[path] = 'Animation frame already required by accepted K-J decal definitions'
        deltas = [{'path': path, 'sha256': sha(archive.read(path)), 'reason': reason}
                  for path, reason in sorted(extras.items())]
    census_a = {'selectedOccurrences': 920, 'acceptedOrVanilla': 920, 'blocked': 0, 'unclassified': 0}
    census_b = {'selected': 73, 'available': 73, 'unavailable': 0}
    census_c = ledger['census']
    factory = {'schemaVersion': 1, 'stage': '25K-K', 'status': 'ACCEPTED_KJ_SELECTED_FACTORY_SCOPE_UNCHANGED',
        'authoredProfilesRegeneratedFromExactMaps': True, 'authoredProfilesSha256': sha((AE / 'sj-factory-authored-profiles-stage25kj.json').read_bytes()),
        'providersReverifiedFromPublicZips': providers, 'selectedFactories': authored['factories'],
        'contentIdCensus': census_a, 'productionRegistrationCensus': census_b,
        'selectedFactorySemanticCensus': census_c, 'acceptedKjLedgerSha256': SEMANTIC_SHA,
        'acceptedKjCompleteRegistrySha256': contract['acceptedCompleteRegistrySha256'],
        'acceptedKjGameplayRegistrySourceSha256': read(AE / 'sj-selected-factory-production-stage25kj.json')['actualFactoryRegistrySha256'],
        'kkProductionRegistryGenerated': False, 'realSliceSemanticClosureEstablished': False,
        'registryAuthority': 'ACCEPTED_KJ_ACTUAL_COMPILED_PROFILE; NO_KK_REAL_SLICE_PRODUCT'}
    readiness = {'schemaVersion': 1, 'stage': '25K-K', 'status': STATUS,
        'acceptedKjReadinessMarker': 'READY_FOR_K_K_REAL_SJ_INTEGRATION_RETRY',
        'acceptedKjThreeGatesRetained': True, 'contentIdCensus': census_a,
        'productionRegistrationCensus': census_b, 'selectedFactorySemanticCensus': census_c,
        'realContentComposition': 'BLOCKED_NEW_VARIABLE_SCAN_AUTOTILER', 'aotAllowed': False,
        'developmentIntegrationReady': False, 'allPlatformReleaseReady': False,
        'fullBeginnerSupported': False, 'fullStrawberryJamSupported': False,
        'readinessMarker': None, 'stopRequiredByBrief': ['46', '53']}
    physical = {'schemaVersion': 1, 'stage': '25K-K', 'status': 'NOT_RUN_PREFLIGHT_STOP',
        'iphone': 'NOT_RUN_NO_KK_PRODUCT', 'appleTv': 'NOT_RUN_NO_KK_PRODUCT', 'ipad': IPAD,
        'iosIpaSha256': None, 'tvosIpaSha256': None, 'physicalRoomsTraversed': [],
        'lobby': 'NOT_RUN', 'bing': 'NOT_RUN', 'saveQuit': 'NOT_RUN', 'coldResume': 'NOT_RUN',
        'returnToLobby': 'NOT_RUN', 'reentry': 'NOT_RUN', 'journal': 'NOT_RUN',
        'strawberryWithReturn': 'NOT_RUN', 'completion': 'NOT_RUN', 'deviceDataTouched': False,
        'lastAcceptedPrimaryProducts': 'Stage 25K-J build 41',
        'lastAllThreePhysicalGreen': 'Stage 25K-B build 35'}
    result = {'schemaVersion': 1, 'stage': '25K-K', 'status': STATUS, 'startSha': START,
        'branch': 'feature/apple-everest-first-sj-slice-retry2', 'kgMerged': False, 'kiMerged': False,
        'strawberryJam': {'version': '1.0.12', 'zipSha256': authored['sourceArchiveSha256'],
            'dllSha256': '8d5b9184204e7e7728965bcf95af5f150f6220dafbfe52fdd6c23e50d47e5258'},
        'maps': map_records, 'contentPlan': {'classification': 'HISTORICAL_SELECTION_REPRODUCED_NOT_PRODUCT_READY',
            'packages': 14, 'files': 1212, 'sjRootFiles': 1178, 'sjAssetsFiles': 24,
            'selectedSjAudioBanks': 4, 'helperAssets': 7, 'selectedSjMapBins': 2,
            'excludedSjMapBins': 126, 'packagedSjMapBins': 0,
            'additionalRootFilesRequiredBeforeFutureProduct': deltas},
        'runtimeSourceChanged': False, 'buildNumberChanged': False, 'aevpsv1SchemaVersion': 1,
        'product': {'ios': 'NOT_BUILT_PREFLIGHT_STOP', 'tvos': 'NOT_BUILT_PREFLIGHT_STOP',
            'sharedClosureSha256': None, 'completeClosureThreeRun': 'NOT_RUN_PREFLIGHT_STOP',
            'signing': 'NOT_RUN', 'installation': 'NOT_RUN'},
        'developmentIntegrationReady': False, 'allPlatformReleaseReady': False,
        'firstPhysicallyRunningUnchangedSjSlice': False, 'fullBeginnerSupported': False,
        'fullStrawberryJamSupported': False, 'recommendedFastForwardSha': None,
        'nextWork': 'Close and accept the bounded Everest custom-autotiler mechanism before retrying unchanged lobby/Bing integration; defer expansion audit.'}
    return {'sj-beginner-slice-stage25kk.json': result, 'sj-beginner-content-stage25kk.json': plan,
        'sj-beginner-production-readiness-stage25kk.json': readiness,
        'sj-beginner-factory-closure-stage25kk.json': factory,
        'sj-beginner-physical-stage25kk.json': physical, 'sj-beginner-terrain-stage25kk.json': terrain}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packages-root', required=True, type=Path)
    parser.add_argument('--output-root', required=True, type=Path)
    args = parser.parse_args()
    for filename, value in generate(args.packages_root.resolve()).items():
        save(args.output_root / filename, value)
    print('PASS: reproduced K-K pre-product YELLOW; 26 used tiles need new 5x5 autotiling; no Apple product generated')


if __name__ == '__main__':
    main()
