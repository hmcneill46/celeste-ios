#!/usr/bin/env python3
"""Derive selected Decal registry rules from exact public ZIPs, in memory.

Writes metadata and typed generated code, never original map/decal layers.
"""
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('kj_factory_profiles', ROOT / 'scripts/generate-apple-everest-stage25kj-profiles.py')
profiles = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = profiles
spec.loader.exec_module(profiles)
PINS = {
    'JungleHelper': 'a140e21cbb5fd2dcaac70d4d5e36d49476e414861455ae25dedc0678164406cc',
    'StrawberryJam2021': '4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655',
}
PREFIX = 'Graphics/Atlases/Gameplay/decals/'
def sha(data): return hashlib.sha256(data).hexdigest()
def key(path): return re.sub('[0-9]+$', '', path.removesuffix('.png')).lower()
def cs(value): return json.dumps(value, ensure_ascii=False)
def num(value):
    value = float(value)
    if not (-1e9 < value < 1e9): raise ValueError('unbounded decal parameter')
    return format(value, '.9g') + 'f'
def apply(prop):
    a = prop['attributes']; name = prop['name']
    def f(n, d): return num(a.get(n, d))
    def i(n, d): return str(int(a.get(n, d)))
    def b(n, d):
        v = a.get(n, str(d)).lower()
        if v not in ('true', 'false'): raise ValueError('invalid boolean')
        return v
    if name == 'depth': return 'decal.Depth = ' + i('value', 0) + ';'
    if name == 'animationSpeed': return 'decal.AnimationSpeed = ' + f('value', 12) + ';'
    if name == 'randomizeFrame': return 'decal.AppleEverestRandomizeFrame();'
    if name == 'floaty': return 'decal.AppleEverestFloaty();'
    if name == 'parallax': return 'decal.AppleEverestSetParallax(' + f('amount', 0) + ');'
    if name == 'banner':
        args = [f('speed', 1), f('amplitude', 1), i('sliceSize', 1), f('sliceSinIncrement', 1),
                b('easeDown', False), f('offset', 0), b('onlyIfWindy', False)]
        return 'decal.AppleEverestBanner(' + ', '.join(args) + ');'
    if name in ('solid', 'staticMover'):
        args = [i('x', 0), i('y', 0), i('width', 16), i('height', 16)]
        if name == 'solid':
            if i('priority', 0) != '0' or b('blockWaterfalls', True) != 'true' or b('safe', True) != 'true':
                raise ValueError('unreviewed decal solid profile')
            args.append(i('index', 14))
        return 'decal.AppleEverest' + ('Solid' if name == 'solid' else 'StaticMover') + '(' + ', '.join(args) + ');'
    if name == 'scared':
        args = [i('hideRange', a.get('range', 32)), i('showRange', a.get('range', 48))]
        args += [cs(a.get(n, '0')) for n in ('idleFrames', 'hiddenFrames', 'showFrames', 'hideFrames')]
        return 'decal.AppleEverestScared(' + ', '.join(args) + ');'
    if name in ('bloom', 'light'):
        args = [f('offsetX', 0), f('offsetY', 0)]
        if name == 'light': args.append(cs(a.get('color', 'ffffff')))
        args.append(f('alpha', 1))
        args += [f('radius', 1)] if name == 'bloom' else [i('startFade', 16), i('endFade', 24)]
        return 'decal.AppleEverest' + name.title() + '(' + ', '.join(args) + ');'
    raise ValueError('unreviewed selected registry property: ' + name)

def generate(packages):
    counts = Counter(); scales = {}; representative = {}
    sj = packages / 'StrawberryJam2021.zip'
    if sha(sj.read_bytes()) != PINS['StrawberryJam2021']: raise ValueError('SJ archive pin differs')
    with zipfile.ZipFile(sj) as z:
        for sid, expected in profiles.MAPS.items():
            data = z.read('Maps/' + sid + '.bin')
            if sha(data) != expected: raise ValueError('map pin differs')
            tree, _ = profiles.parse(data)
            def walk(e, parent=''):
                if parent in ('fgdecals', 'bgdecals'):
                    a = e['attributes']
                    if set(a) != {'x', 'y', 'scaleX', 'scaleY', 'texture'}: raise ValueError('decal attributes changed')
                    name = key(a['texture']); counts[name] += 1
                    scales.setdefault(name, set()).add((a['scaleX'], a['scaleY']))
                    representative.setdefault(name, a['texture'])
                for c in e['children']: walk(c, e['name'])
            walk(tree)
    if sum(counts.values()) != 7342: raise ValueError('decal census differs')
    rules = []; assets = []; already = set()
    for owner, expected in PINS.items():
        package = packages / (owner + '.zip')
        if sha(package.read_bytes()) != expected: raise ValueError(owner + ' archive pin differs')
        with zipfile.ZipFile(package) as z:
            raw = z.read('DecalRegistry.xml'); root = ET.fromstring(raw)
            local = {}
            for n in z.namelist():
                if n.startswith(PREFIX) and n.endswith('.png'): local.setdefault(key(n[len(PREFIX):]), []).append(n)
            entries = [(index, e) for index, e in enumerate(root) if e.tag == 'decal']
            def priority(entry):
                p = entry[1].attrib['path'].lower()
                return (0 if p.endswith('/') else 1 if p.endswith('*') else 2, len(p) if p.endswith('*') else 0)
            for index, e in sorted(entries, key=priority):
                path = e.attrib['path'].lower()
                def matches(n):
                    if path.endswith(('*', '/')):
                        prefix = path.rstrip('*')
                        return len(n) > len(prefix) and n.startswith(prefix) and n.rfind('/') <= len(prefix) - 1
                    return n == path
                names = sorted(n for n in local if n in counts and matches(n))
                if not names: continue
                # No selected basename has overlapping winning rules. Reject
                # source changes rather than accidentally relying on stable sort.
                if already.intersection(names): raise ValueError('selected decal rule overlap changed')
                already.update(names)
                props = [{'name': c.tag, 'attributes': dict(c.attrib)} for c in e]
                for p in props: apply(p)
                rules.append({'provider': owner, 'sourceArchiveSha256': expected, 'registrySha256': sha(raw),
                    'xmlIndex': index, 'path': e.attrib['path'], 'properties': props,
                    'decals': [{'name': n, 'texture': representative[n], 'occurrences': counts[n],
                                'scales': [list(v) for v in sorted(scales[n])]} for n in names]})
                for name in names:
                    for path in sorted(local[name]):
                        if path[:-4] + '.meta.yaml' in z.namelist() or path[:-4] + '.meta.yml' in z.namelist():
                            raise ValueError('selected decal metadata framing changed')
                        assets.append({'provider': owner, 'path': path, 'sha256': sha(z.read(path))})
    if (len(rules), len(already), sum(counts[n] for n in already), len(assets)) != (58, 118, 1171, 298):
        raise ValueError('selected decal closure census differs')
    runtime = ['// Generated by generate-apple-everest-stage25kj-decals.py from exact public inputs.',
        'using System;', 'using System.Collections.Generic;', 'namespace Celeste.Mod;',
        'internal static class AppleEverestSelectedDecalRegistry', '{',
        '    internal static readonly HashSet<string> IgnoredNames = new(StringComparer.Ordinal);',
        '    internal static void Apply(Decal decal)', '    {',
        '        string name = decal.Name.ToLower();',
        '        if (name.StartsWith("decals/", StringComparison.Ordinal)) name = name.Substring(7);',
        '        switch (name)', '        {']
    for rule in rules:
        for decal in rule['decals']: runtime.append('            case ' + cs(decal['name']) + ':')
        runtime.append('                decal.AppleEverestBeginRegistry();')
        runtime += ['                ' + apply(prop) for prop in rule['properties']]
        runtime += ['                decal.AppleEverestEndRegistry();', '                break;']
    runtime += ['        }', '    }', '}']
    host = ['// Generated by generate-apple-everest-stage25kj-decals.py from exact public inputs.',
        'namespace AppleEverestBuilder;', 'internal static class SelectedDecalContent', '{',
        '    private static readonly HashSet<string> Paths = new(StringComparer.Ordinal)', '    {']
    host += ['        ' + cs(a['provider'] + '\0' + a['path']).replace('\\u0000', '\\0') + ',' for a in assets]
    host += ['    };', '    internal static bool Includes(string owner, string path) => Paths.Contains(owner + "\\0" + path);', '}']
    metadata = {'schemaVersion': 1, 'originalMapAnalysisOnly': True, 'originalLayersWritten': False,
        'selectedRules': 58, 'selectedDecalNames': 118, 'affectedOccurrences': 1171,
        'selectedPngFrames': 298, 'rules': rules, 'assets': assets}
    return {
        ROOT/'apple-everest/sj-selected-decal-profiles-stage25kj.json': json.dumps(metadata, indent=2, sort_keys=True) + '\n',
        ROOT/'apple-everest/runtime/semantics/AppleEverestSelectedDecalRegistry.cs': '\n'.join(runtime) + '\n',
        ROOT/'tools/AppleEverestBuilder/SelectedDecalContent.cs': '\n'.join(host) + '\n',
    }

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--packages', type=Path, required=True)
    p.add_argument('--verify', action='store_true')
    args = p.parse_args()
    for path, data in generate(args.packages).items():
        if args.verify:
            if not path.exists() or path.read_text() != data: raise ValueError('generated decal evidence differs: ' + path.name)
        else: path.write_text(data)
    print('PASS: 58 rules / 118 decal names / 1171 occurrences / 298 PNG frames; no original layers written')
if __name__ == '__main__': main()
