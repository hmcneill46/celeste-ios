#!/usr/bin/env python3
"""Extract presentation and collectible metadata without staging any original map."""
import argparse
import hashlib
import importlib.util
import json
import re
from pathlib import Path
import sys
import zipfile
ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('kj_profiles', ROOT / 'scripts/generate-apple-everest-stage25kj-profiles.py')
reader = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = reader
spec.loader.exec_module(reader)
def sha(data): return hashlib.sha256(data).hexdigest()
def main():
    p=argparse.ArgumentParser(); p.add_argument('--package', type=Path, required=True); p.add_argument('--assets', type=Path, required=True); p.add_argument('--output-root', type=Path, default=ROOT)
    a=p.parse_args(); authored=json.loads((ROOT/'apple-everest/sj-factory-authored-profiles-stage25kj.json').read_text())
    if sha(a.package.read_bytes()) != authored['sourceArchiveSha256']: raise ValueError('SJ archive identity mismatch')
    targets=sorted({o['attributes']['map'] for o in authored['occurrences'] if o['customId']=='SJ2021/StrawberryJamJar'})
    if len(targets)!=21: raise ValueError('selected jar target census changed')
    # Identity from the public dependency graph, never a locally recompressed ZIP.
    graph=json.loads((ROOT/'apple-everest/strawberry-jam-dependency-graph-stage25kc.json').read_text())
    assets_pin=next(n for n in graph['nodes'] if n['name']=='StrawberryJam2021Assets')
    assets_sha=assets_pin['zipSha256']
    if sha(a.assets.read_bytes()) != assets_sha: raise ValueError('SJ Assets archive identity mismatch')
    rows=[]
    with zipfile.ZipFile(a.package) as z:
        meta_path='Maps/StrawberryJam2021/0-Lobbies/1-Beginner.meta.yaml'
        meta_bytes=z.read(meta_path)
        if sha(meta_bytes) != 'd8110177870130c4a0003a310b0f2f06eb2836e5ecbd53f97658ee46a6db450f': raise ValueError('sticker metadata identity changed')
        block=meta_bytes.decode().split('Stickers:\n',1)[1]
        pattern=r'  - Path: ([^\n]+)\n    FinishedMaps:\n    - ([^\n]+)\n    X: ([-\d.]+)\n    Y: ([-\d.]+)\n    Scale: ([-\d.]+)\n    Rotation: ([-\d.]+)(?:\n|$)'
        matches=list(re.finditer(pattern,block))
        if len(matches)!=21 or ''.join(m.group(0) for m in matches)!=block: raise ValueError('selected sticker schema changed')
        stickers=[]
        with zipfile.ZipFile(a.assets) as assets:
            for match in matches:
                path,sid,x,y,scale,rotation=match.groups()
                png_path='Graphics/Atlases/Stickers/'+path+'.png';png=assets.read(png_path)
                stickers.append({'path':path,'finishedMaps':[sid],'x':float(x),'y':float(y),'scale':float(scale),'rotation':float(rotation),'pngSha256':sha(png),'pngBytes':len(png)})
        for sid in targets:
            data=z.read('Maps/'+sid+'.bin'); tree,_=reader.parse(data)
            meta=next(c for c in tree['children'] if c['name']=='meta')
            attrs=meta['attributes']; icon=attrs['Icon']
            berries=[]
            def walk(e,room=''):
                if e['name']=='level': room=e['attributes']['name'].split(':')[0].removeprefix('lvl_')
                if e['name']=='CollabUtils2/SilverBerry': berries.append({'room':room,'id':e['attributes']['id']})
                for c in e['children']: walk(c,room)
            walk(tree)
            if len(berries) != 1: raise ValueError("selected one-silver-per-SID census changed: " + sid)
            rows.append({'sid':sid,'mapSha256':sha(data),'icon':icon,'silverBerries':berries})
    target=a.output_root/'apple-everest';target.mkdir(parents=True,exist_ok=True)
    if sorted(s['finishedMaps'][0] for s in stickers)!=targets: raise ValueError('sticker conditions differ from selected catalog')
    (target/'sj-collab-presentation-metadata-stage25kj.json').write_text(json.dumps({'schemaVersion':1,'archiveSha256':authored['sourceArchiveSha256'],'metadataOnly':True,'maps':rows,'stickerMetadataPath':meta_path,'stickerMetadataSha256':sha(meta_bytes),'assetsArchiveSha256':assets_sha,'stickers':stickers},indent=2,sort_keys=True)+'\n')
    q=lambda s:json.dumps(s,ensure_ascii=False)
    source='''#nullable disable
using System;
using System.Linq;
namespace Celeste.Mod;

// Read-only original presentation/collectible metadata. These entries do not
// register areas, mount map bytes, or make their gameplay available.
internal static class AppleEverestCollabMapMetadata
{
    internal static string Icon(string sid) => sid switch
    {
'''
    source+=''.join(f'        {q(v["sid"])} => {q(v["icon"])},\n' for v in rows)
    source+='''        _ => null
    };
    internal static readonly (string Sid, EntityID Id)[] BeginnerSilverBerries =
    {
'''
    source+=''.join(f'        ({q(v["sid"])}, new EntityID({q(b["room"])}, {b["id"]})),\n' for v in rows for b in v['silverBerries'])
    source+='''    };
    internal static readonly AppleEverestCollabSticker[] BeginnerStickers =
    {
'''
    source+=''.join(f'        new({q(s["path"])}, {s["x"]}f, {s["y"]}f, {s["rotation"]}f, {s["scale"]}f, new[] {{ {q(s["finishedMaps"][0])} }}),\n' for s in stickers)
    source+='    };\n}\n'
    runtime=target/'runtime/semantics'; runtime.mkdir(parents=True,exist_ok=True)
    (runtime/'AppleEverestCollabMapMetadata.cs').write_text(source)
    print(f'metadata maps={len(rows)}; silver berries={sum(len(r["silverBerries"]) for r in rows)}; playable maps staged=0')
if __name__=='__main__':main()
