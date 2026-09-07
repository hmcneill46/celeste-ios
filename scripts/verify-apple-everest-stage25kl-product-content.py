#!/usr/bin/env python3
"""Verify actual packaged original maps and every selected mounted asset."""
import argparse
import hashlib
import json
from pathlib import Path

def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['app','closure','readiness','output']:parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args()
    manifest=json.loads((args.closure/'compatibility-manifest.json').read_text());ready=json.loads(args.readiness.read_text())
    if ready['marker']!='READY_FOR_REAL_SJ_PRODUCT_BUILD' or ready['sharedClosureSha256']!=manifest['sharedClosureSha256']:
        raise ValueError('actual product requires bound four-gate readiness')
    if ready['status']!='PASS' or ready['gateD']['blocked'] or ready['gateD']['unknown']:
        raise ValueError('actual product requires zero blocked/unknown real composition')
    content=args.app/'Content'
    files={p.relative_to(content).as_posix():p for p in content.rglob('*') if p.is_file()}
    if len(files)!=len({p.casefold() for p in files}):raise ValueError('case-ambiguous packaged content')
    expected={'Maps/'+row['sid']+'.bin':row['sha256'] for row in ready['gateD']['maps']}
    actual={path:sha(file) for path,file in files.items() if path.lower().startswith('maps/strawberryjam2021/') and path.lower().endswith('.bin')}
    if len(expected)!=2 or actual!=expected:raise ValueError('actual product changed original maps or admitted excluded SJ maps')
    for relative in files:
        lower='/'+relative.lower()
        if '/maps/strawberryjam2021/' in lower and lower.endswith('.bin') and relative not in expected:
            raise ValueError('nested or excluded original SJ map entered packaged Content: '+relative)
        if files[relative].suffix.lower() in {'.zip','.7z','.rar'}:
            raise ValueError('source package archive entered packaged Content: '+relative)
    for path in args.app.rglob('*'):
        if not path.is_file():continue
        relative=path.relative_to(args.app).as_posix();lower='/'+relative.lower()
        if path.suffix.lower() in {'.zip','.7z','.rar'}:
            raise ValueError('source package archive entered device app: '+relative)
        if '/maps/strawberryjam2021/' in lower and lower.endswith('.bin') and relative not in {'Content/'+p for p in expected}:
            raise ValueError('nested or excluded original SJ map entered device app: '+relative)
    for mount in manifest['contentMounts']:
        path=mount['logicalPath']
        if path not in files or sha(files[path])!=mount['sha256']:raise ValueError('packaged selected content differs: '+path)
    artwork=ready['gateD']['chapterTitleLayout']['artwork']
    if artwork['sha256']!='0839135f2baafbf652d652177e69456c07bc85343a6c93491e4e2a906034518d' or sha(files[artwork['logicalPath']])!=artwork['sha256']:
        raise ValueError('actual package omitted or substituted the wider Everest title graphic')
    report={'schemaVersion':1,'status':'PASS','sharedClosureSha256':manifest['sharedClosureSha256'],'originalMaps':actual,
            'originalMapCount':2,'excludedSjGameplayMaps':126,'verifiedMountedFiles':len(manifest['contentMounts']),
            'caseExact':True,'allMountedContentBytesMatch':True,'sourceOriginalMapBytesPreserved':True,
            'chapterTitleArtwork':artwork}
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(report,indent=2)+'\n')
    print('PASS: actual app contains exactly two original SJ BINs and every hash-bound selected asset;126 excluded')

if __name__=='__main__':main()
