#!/usr/bin/env python3
"""Preserve exact profiles from previously accepted original collabs and authored canaries.

Original maps are parsed in memory from hash-verified public archives. No map
bytes, layers or progression payloads are emitted. This evidence is separate
from the frozen 920-occurrence Strawberry Jam selection.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('profiles', ROOT/'scripts/generate-apple-everest-stage25kj-profiles.py')
profiles = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = profiles
spec.loader.exec_module(profiles)

def extract(directory):
    selected = json.loads((ROOT/'apple-everest/sj-factory-authored-profiles-stage25kj.json').read_text())
    known = {(f['kind'], f['customId']): f['provider'] for f in selected['factories']}
    rows, sources = [], []
    def walk(e, sid, authority, parent='', room=''):
        if e['name'] == 'level': room = e['attributes']['name']
        kind = {'entities': 'entity', 'triggers': 'trigger', 'Backgrounds': 'backdrop', 'Foregrounds': 'backdrop'}.get(parent)
        key = (kind, e['name'])
        if key in known:
            attrs = e['attributes']
            nodes = [n['attributes'] for n in e['children'] if n['name'] == 'node']
            if any(n['name'] != 'node' for n in e['children']): raise ValueError('unreviewed legacy factory child')
            values = {k:v for k,v in attrs.items() if k not in ('id','x','y','originX','originY')}
            relative = [{**n,'x':n['x']-attrs.get('x',0),'y':n['y']-attrs.get('y',0)} for n in nodes]
            rows.append({'kind':kind,'customId':e['name'],'provider':known[key], 'authority':authority,
                'map':sid,'room':room,'entityId':attrs.get('id'),'attributes':attrs,'nodes':nodes,
                'profileSha256':profiles.sha(profiles.canonical({'attributes':values,'relativeNodes':relative}))})
        for child in e['children']: walk(child,sid,authority,e['name'],room)
    for stage, filename in [('ka','Hennyburgr-Comp-1.1.0.zip'),('kb','Kayonara Collection.zip')]:
        evidence = json.loads((ROOT/f'apple-everest/{"first" if stage == "ka" else "second"}-real-collab-stage25{stage}.json').read_text())['collab']
        archive = directory/filename
        if profiles.sha(archive.read_bytes()) != evidence['zipSha256']: raise ValueError('legacy archive identity differs: '+filename)
        sources.append({'archive':filename,'archiveSha256':evidence['zipSha256'],'version':evidence['version'],'url':evidence['url'], 'acceptedStage':'25K-'+stage[-1].upper()})
        with zipfile.ZipFile(archive) as z:
            for m in [evidence['lobby'], *evidence['maps']]:
                raw = z.read('Maps/'+m['sid']+'.bin')
                if profiles.sha(raw) != m['sourceMapSha256']: raise ValueError('legacy map identity differs')
                tree, _ = profiles.parse(raw)
                walk(tree,m['sid'],{'archiveSha256':evidence['zipSha256'],'mapSha256':m['sourceMapSha256'],'acceptedStage':'25K-'+stage[-1].upper()})
    # XML attribute types match ContentCompiler's boolean/integer/single/string
    # encoding; strings such as compressed tile data retain their exact value.
    import struct
    def value(raw):
        if raw.lower() in ('true','false'): return raw.lower() == 'true'
        try: return int(raw)
        except ValueError: pass
        try: return struct.unpack('<f',struct.pack('<f',float(raw)))[0]
        except ValueError: return raw
    def convert(e):
        name = e.attrib['name'] if e.tag in ('appleEverestEntity','appleEverestTrigger') else e.tag
        return {'name':name,'attributes':{k:value(v) for k,v in e.attrib.items() if not (k=='name' and e.tag in ('appleEverestEntity','appleEverestTrigger'))},'children':[convert(c) for c in e]}
    for path in sorted((ROOT/'apple-everest/canaries').rglob('*.xml')):
        if '/Maps/' not in path.as_posix() or '/stage25kj/' in path.as_posix(): continue
        sid = path.as_posix().split('/Maps/')[1][:-4]
        authority = {'projectOwnedCanary':path.relative_to(ROOT).as_posix(),'sourceSha256':profiles.sha(path.read_bytes())}
        walk(convert(ET.parse(path).getroot()),sid,authority)
    return {'schemaVersion':1,'purpose':'PRESERVE_PREVIOUSLY_ACCEPTED_PROFILES_SEPARATE_FROM_SJ_CENSUS','sources':sources,
        'census':{'occurrences':len(rows),'factories':len({(r['kind'],r['customId']) for r in rows})},'occurrences':rows}

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--packages',required=True,type=Path)
    p.add_argument('--output',type=Path,default=ROOT/'apple-everest/sj-legacy-factory-profiles-stage25kj.json')
    a=p.parse_args(); doc=extract(a.packages);profiles.save(a.output,doc);print('PASS:',doc['census'])
if __name__ == '__main__': main()
