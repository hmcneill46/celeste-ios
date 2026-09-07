#!/usr/bin/env python3
"""Generate three independent complete K-L closures, compiler proofs and four gates."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import os

ROOT=Path(__file__).resolve().parents[1]
AE=ROOT/'apple-everest'

def read(path):return json.loads(path.read_text())
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest()
def inventory(root):return {p.relative_to(root).as_posix():sha(p) for p in sorted(root.rglob('*')) if p.is_file()}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['package-root','chrono-package','production-root','work-root']:parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args();work=args.work_root.resolve();product=args.production_root.resolve()
    if work.exists():raise ValueError('three independent generations require a fresh work root')
    work.mkdir(parents=True);(work/'.stage25kl-reproduction').touch()
    profiles=AE/'sj-factory-authored-profiles-stage25kj.json';plan=AE/'sj-beginner-content-stage25kl.json'
    providers=sorted({r['provider'] for r in read(profiles)['factories']}-{'EverestCore'})
    inputs=[args.package_root.resolve()/(name+'.zip') for name in providers+['StrawberryJam2021Assets','StrawberryJam2021AudioA','StrawberryJam2021AudioB']]
    inputs+=[args.chrono_package.resolve()]
    inputs+=[AE/'canaries'/name for name in ['stage25ke','stage25kf','stage25kh','stage25kj','stage25kj-interactions','custom-audio-content','dj-frozen-il-content']]
    mod_args=[argument for path in inputs for argument in ['--mod',str(path)]]
    assemblies=list((product/'preflight-runtime/bin/Release').glob('*/Celeste.dll'))
    if len(assemblies)!=1:raise ValueError('one actual compiled production assembly is required')
    runner=['dotnet','exec','--fx-version','10.0.10',str(ROOT/'tools/AppleEverestBuilder/bin/Debug/net8.0/AppleEverestBuilder.dll')]
    common=['--profile',str(AE/'profiles/stable-1.6458.0.json'),'--repo-root',str(ROOT),'--upstream',str(ROOT/'.build/apple-everest/upstream/Everest')]
    env=dict(os.environ,MSBUILDDISABLENODEREUSE='1',DOTNET_CLI_USE_MSBUILD_SERVER='0',UseSharedCompilation='false')
    snapshots=[]
    for number in range(1,4):
        current=work/('run'+str(number));current.mkdir();closure=current/'shared-closure'
        with (current/'commands.log').open('w') as log:
            def run(command):
                result=subprocess.run([str(v) for v in command],cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT)
                if result.returncode:raise RuntimeError('K-L reproduction failed; inspect run'+str(number)+'/commands.log')
            run(runner+['build',*common,'--output',closure,'--content-plan',plan,*mod_args])
            run(runner+['preflight-factory-closure',*common,'--manifest',AE/'selected-factory-type-closure-stage25kh.json',
                '--authored-profiles',profiles,'--assembly',assemblies[0],'--closure',closure,'--content-plan',plan,
                '--canonical-managed-root',ROOT/'.build/celeste-ios/current/managed','--dotnet',shutil.which('dotnet'),
                '--output',current/'production-preflight.json',*mod_args])
            run(['python3',ROOT/'scripts/inspect-apple-everest-stage25kj-canary-profiles.py','--closure',closure,'--output',current/'compiled-canary-profiles.json'])
            run(runner+['inspect-compiled-factories','--assembly',assemblies[0],'--manifest',AE/'selected-factory-type-closure-stage25kh.json',
                '--authored-profiles',current/'compiled-canary-profiles.json','--output',current/'compiled-canary-guards.json'])
            run(['python3',ROOT/'scripts/verify-apple-everest-stage25kl-composition.py','--closure',closure,'--runtime',product/'preflight-runtime',
                '--production-preflight',current/'production-preflight.json','--content-plan',plan,'--output',current/'real-composition'])
        manifest=read(closure/'compatibility-manifest.json');production=read(current/'production-preflight.json');ready=read(current/'real-composition/readiness.json')
        if manifest['sharedClosureSha256']!=read(product/'shared-closure/compatibility-manifest.json')['sharedClosureSha256']:
            raise ValueError('fresh reproduction differs from the actual product closure')
        snapshot={'completeTree':inventory(closure),'closure':manifest,'selectedFilePlanSha256':sha(plan),
            'gateA':ready['gateA'],'gateB':ready['gateB'],'gateC':ready['gateC'],'gateD':ready['gateD'],
            'actualCompiledFactories':production['factories'],'providers':production['providers'],
            'fullImplementationComparison':production['compilation']['CompiledDllLogicalSha256'],
            'autotilerConformance':read(current/'real-composition/autotiler-conformance.json'),
            'runtimeComposition':read(current/'real-composition/runtime-composition.json')}
        (current/'snapshot.json').write_text(json.dumps(snapshot,indent=2,sort_keys=True,ensure_ascii=False)+'\n')
        if snapshots and snapshot!=snapshots[0]:raise ValueError('complete closure, four-gate or pinned-reference determinism failed')
        snapshots.append(snapshot)
        if not (closure/'.apple-everest-static-closure').is_file():raise ValueError('refusing unmarked reproduction cleanup')
        shutil.rmtree(closure)
        for child in ['autotiler-host','runtime-probe']:
            shutil.rmtree(current/'real-composition'/child)
        # Keep hashes and finite conformance evidence, not an original BIN tree.
        (current/'real-composition/original-map-trees.json').unlink()
        print('PASS: independent complete closure, fresh compiler and four gates run '+str(number),flush=True)
    report={'schemaVersion':1,'stage':'25K-L','threeFreshGenerations':True,'freshProductionCompilations':3,
            'completeTreeCompared':True,'fourGatesCompared':True,'runsIdentical':True,
            'runSnapshotSha256':[sha(work/('run'+str(i))/'snapshot.json') for i in range(1,4)],
            'sharedClosureSha256':[s['closure']['sharedClosureSha256'] for s in snapshots],
            'sourceCommit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
            'sourceTreeDirty':bool(subprocess.check_output(['git','status','--porcelain'],cwd=ROOT,text=True).strip()),
            'physicalAcceptance':'SEPARATE_EXACT_PRODUCT_GATE'}
    (work/'reproduction.json').write_text(json.dumps(report,indent=2,sort_keys=True)+'\n')

if __name__=='__main__':main()
