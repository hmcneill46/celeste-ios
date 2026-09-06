#!/usr/bin/env python3
"""Extract exact authored factory metadata from verified host-only SJ inputs.

Retains complete attribute values and node coordinates, including compressed
strings. No gameplay BIN, tile/decal layer or appendix is written to the repo.
"""
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('kj_reader', ROOT / 'scripts/generate-apple-everest-stage25kc.py')
kc = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = kc
spec.loader.exec_module(kc)
MAPS = {
    'StrawberryJam2021/0-Lobbies/1-Beginner': 'a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2',
    'StrawberryJam2021/1-Beginner/Bing_Over_Google': 'e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347',
}

def sha(data): return hashlib.sha256(data).hexdigest()
def canonical(data): return json.dumps(data, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()
def save(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, sort_keys=True, ensure_ascii=False) + '\n')

def parse(data):
    r = kc.Reader(data)
    if r.string() != 'CELESTE MAP': raise ValueError('invalid magic')
    r.string()
    count = r.i16()
    if not 0 < count <= 8192: raise ValueError('invalid string table')
    strings = [r.string() for _ in range(count)]
    def element(depth=0):
        if depth > 64: raise ValueError('invalid nesting')
        name, attrs = strings[r.i16()], {}
        for _ in range(r.u8()):
            key, tag = strings[r.i16()], r.u8()
            if tag == 0: value = bool(r.u8())
            elif tag == 1: value = r.u8()
            elif tag == 2: value = r.i16()
            elif tag == 3: value = r.i32()
            elif tag == 4: value = r.f32()
            elif tag == 5: value = strings[r.i16()]
            elif tag == 6: value = r.string()
            elif tag == 7:
                encoded = r.take(r.i16())
                if len(encoded) % 2: raise ValueError('invalid RLE')
                value = ''.join(chr(encoded[i + 1]) * encoded[i] for i in range(0, len(encoded), 2))
            else: raise ValueError('invalid attribute tag')
            attrs[key] = value
        children = r.i16()
        if children < 0: raise ValueError('invalid children')
        return {'name': name, 'attributes': attrs, 'children': [element(depth + 1) for _ in range(children)]}
    tree = element()
    return tree, r.pos

def extract(package):
    graph = json.loads((ROOT / 'apple-everest/selected-factory-type-closure-stage25kh.json').read_text())
    known = {(f['kind'], f['customId']): f for f in graph['factories']}
    result, maps = [], []
    def walk(e, sid, parent='', room=''):
        if e['name'] == 'level': room = e['attributes']['name']
        kind = {'entities': 'entity', 'triggers': 'trigger', 'Backgrounds': 'backdrop', 'Foregrounds': 'backdrop'}.get(parent)
        key = kind, e['name']
        if key in known:
            attrs = e['attributes']
            nodes = [n['attributes'] for n in e['children'] if n['name'] == 'node']
            unexpected = [n['name'] for n in e['children'] if n['name'] != 'node']
            if unexpected: raise ValueError(f'unreviewed factory child {key}: {unexpected}')
            profile = {k: v for k, v in attrs.items() if k not in ('x', 'y', 'id', 'originX', 'originY')}
            # Shape relative to entity origin is part of the authored profile.
            relative_nodes = [{**n, 'x': n['x'] - attrs.get('x', 0), 'y': n['y'] - attrs.get('y', 0)} for n in nodes]
            result.append({'kind': kind, 'customId': e['name'], 'provider': known[key]['provider'],
                'map': sid, 'room': room, 'layer': parent if kind == 'backdrop' else None,
                'entityId': attrs.get('id'), 'position': {k: attrs[k] for k in ('x', 'y') if k in attrs},
                'attributes': attrs, 'nodes': nodes,
                'profileSha256': sha(canonical({'attributes': profile, 'relativeNodes': relative_nodes}))})
        for c in e['children']: walk(c, sid, e['name'], room)
    raw = package.read_bytes()
    if sha(raw) != '4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655': raise ValueError('SJ ZIP identity differs')
    with zipfile.ZipFile(package) as z:
        for sid, expected in MAPS.items():
            member = 'Maps/' + sid + '.bin'
            data = z.read(member)
            if sha(data) != expected: raise ValueError('original map identity differs')
            tree, boundary = parse(data)
            maps.append({'sid': sid, 'member': member, 'bytes': len(data), 'sha256': expected,
                         'rootBytes': boundary, 'appendixBytes': len(data) - boundary,
                         'appendixSha256': sha(data[boundary:]), 'hostAnalysisOnly': True})
            walk(tree, sid)
    counts = Counter((r['kind'], r['customId']) for r in result)
    if sum(counts.values()) != 920 or set(counts) != set(known): raise ValueError(f'selected census differs: {sum(counts.values())}/{len(counts)}')
    return {'schemaVersion': 1, 'sourceArchiveSha256': sha(raw), 'maps': maps,
        'census': {'selectedFactories': len(counts), 'selectedOccurrences': len(result)},
        'factories': [{'kind': k[0], 'customId': k[1], 'provider': known[k]['provider'], 'occurrences': counts[k],
            'distinctProfiles': len({r['profileSha256'] for r in result if (r['kind'], r['customId']) == k})} for k in sorted(counts)],
        'occurrences': result}

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--package', required=True, type=Path)
    p.add_argument('--output', required=True, type=Path)
    a = p.parse_args()
    doc = extract(a.package)
    save(a.output, doc)
    print(f"PASS: exact authored profiles: {doc['census']}")
if __name__ == '__main__': main()
