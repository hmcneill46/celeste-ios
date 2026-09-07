#!/usr/bin/env python3
"""Isolate a complete original desktop SJ reference beside the two-map Apple slice."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

ROOT=Path(__file__).resolve().parents[1]

def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package-root',type=Path,required=True)
    parser.add_argument('--extra-root',type=Path,default=ROOT/'.build/apple-everest/stage25kl/reference-packages')
    parser.add_argument('--base-reference',type=Path,default=ROOT/'.build/apple-everest/stage25kj/macos-reference')
    parser.add_argument('--output',type=Path,default=ROOT/'.build/apple-everest/stage25kl/macos-reference')
    parser.add_argument('--download-only',action='store_true')
    args=parser.parse_args()
    pins=json.loads((ROOT/'apple-everest/strawberry-jam-dependency-graph-stage25kc.json').read_text())['nodes']
    if len(pins)!=52:raise ValueError('original desktop graph changed')
    args.extra_root.mkdir(parents=True,exist_ok=True)
    def acquire(pin):
        path=args.package_root/(pin['name']+'.zip')
        if path.exists():
            if path.stat().st_size!=pin['zipBytes'] or sha(path)!=pin['zipSha256']:raise ValueError('existing exact public package differs')
            return path
        path=args.extra_root/(pin['name']+'.zip')
        if path.exists():
            if path.stat().st_size!=pin['zipBytes'] or sha(path)!=pin['zipSha256']:raise ValueError('existing desktop dependency differs')
            return path
        temporary=path.with_suffix('.download')
        mirror='https://celestemodupdater.0x0a.de/banana-mirror/'+pin['gameBananaFileId']+'.zip'
        for url in [mirror,pin['publicUrl']]:
            try:
                request=urllib.request.Request(url,headers={'User-Agent':'AppleEverest-stage25kl-reference/1'})
                count=0
                with urllib.request.urlopen(request,timeout=45) as source,temporary.open('wb') as target:
                    for block in iter(lambda:source.read(1024*1024),b''):
                        count+=len(block)
                        if count>pin['zipBytes']:raise ValueError('oversized public archive')
                        target.write(block)
                if count!=pin['zipBytes'] or sha(temporary)!=pin['zipSha256']:raise ValueError('public archive pin mismatch')
                temporary.replace(path);print('ACQUIRED exact desktop '+pin['name'],flush=True);return path
            except (OSError,ValueError):
                if url==pin['publicUrl']:raise
    with ThreadPoolExecutor(max_workers=3) as pool:packages=list(pool.map(acquire,pins))
    evidence={'schemaVersion':1,'stage':'25K-L','engineProfile':'stable-1.6458.0','fullOriginalDesktopGraph':True,
              'packages':[{'name':pin['name'],'version':pin['resolvedVersion'],'sha256':pin['zipSha256'],'publicUrl':pin['publicUrl']} for pin in pins]}
    (args.extra_root/'acquisition.json').write_text(json.dumps(evidence,indent=2)+'\n')
    if args.download_only:return
    base=args.base_reference
    if not (base/'.stage25kj-owned-reference').is_file() or json.loads((base/'reference-evidence.json').read_text())['engineProfile']!='stable-1.6458.0':
        raise ValueError('accepted pinned desktop engine reference is required')
    output=args.output
    if output.exists():raise ValueError('use a fresh owned K-L reference directory')
    (output/'game').mkdir(parents=True);(output/'.stage25kl-owned-reference').touch()
    app=output/'game/Celeste.app'
    subprocess.run(['cp','-cR',str(base/'game/Celeste.app'),str(app)],check=True)
    resources=app/'Contents/Resources';mods=resources/'Mods'
    for name in ['AppleEverestStage25KJSourceReference','AppleEverestStage25KJReferenceAssets']:
        shutil.rmtree(mods/name)
    for package in packages:
        destination=mods/package.name
        if destination.exists():destination.unlink()
        subprocess.run(['cp','-c',str(package),str(destination)],check=True)
        if sha(destination)!=sha(package):raise ValueError('desktop archive copy differs')
    fixture=mods/'AppleEverestStage25KJReferenceFixtures'
    yaml=fixture/'everest.yaml';value=yaml.read_text()
    for name,replacement in [('AppleEverestStage25KJReferenceAssets','    - Name: StrawberryJam2021\n      Version: 1.0.12\n'),
                             ('AppleEverestStage25KJSourceReference','')]:
        old='    - Name: '+name+'\n      Version: 1.0.0\n'
        if value.count(old)!=1:raise ValueError('owned reference fixture dependency differs')
        value=value.replace(old,replacement)
    yaml.write_text(value)
    utility=mods/'AppleEverestStage25KLReferenceTools';utility.mkdir()
    build=output/'tmp/navigation';build.mkdir(parents=True)
    project=ET.Element('Project',Sdk='Microsoft.NET.Sdk');properties=ET.SubElement(project,'PropertyGroup')
    for name,value in {'TargetFramework':'net8.0','EnableDefaultCompileItems':'false','AssemblyName':'KLReferenceTools','Nullable':'disable',
                       'UseSharedCompilation':'false','LangVersion':'12.0'}.items():ET.SubElement(properties,name).text=value
    items=ET.SubElement(project,'ItemGroup')
    ET.SubElement(items,'Compile',Include=str(ROOT/'apple-everest/reference/Stage25KLReferenceTools.cs'))
    for name in ['Celeste','FNA']:
        reference=ET.SubElement(items,'Reference',Include=name);ET.SubElement(reference,'HintPath').text=str(resources/(name+'.dll'));ET.SubElement(reference,'Private').text='false'
    path=build/'Navigation.csproj';ET.ElementTree(project).write(path,encoding='unicode')
    env=dict(os.environ,MSBUILDDISABLENODEREUSE='1',DOTNET_CLI_USE_MSBUILD_SERVER='0')
    with (output/'navigation-build.log').open('w') as log:
        subprocess.run(['dotnet','build',str(path),'-c','Release','--nologo'],cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT,check=True)
    shutil.copy2(build/'bin/Release/net8.0/KLReferenceTools.dll',utility/'KLReferenceTools.dll')
    (utility/'everest.yaml').write_text('- Name: AppleEverestStage25KLReferenceTools\n  Version: 1.0.0\n  DLL: KLReferenceTools.dll\n  Dependencies:\n    - Name: Everest\n      Version: 1.6458.0\n    - Name: StrawberryJam2021\n      Version: 1.0.12\n')
    for name in ['state','tmp']:(output/name).mkdir(exist_ok=True)
    with zipfile.ZipFile(mods/'StrawberryJam2021.zip') as archive:
        maps={name:hashlib.sha256(archive.read(name)).hexdigest() for name in archive.namelist() if name.startswith('Maps/') and name.endswith('.bin')}
    if len(maps)!=128:raise ValueError('full original desktop map set differs')
    evidence.update({'originalSjMapCount':128,'originalSjMaps':maps,'appleProductSjMapCount':2,'thirdPartyArchivesOrYamlModified':False,
                     'originalSjModuleMounted':True,'oldProjectedSourceCapsuleRemoved':True,'ownedDiagnosticMapCount':len(list((fixture/'Maps').rglob('*.bin'))),
                     'purpose':'Full original desktop comparison; physical Apple acceptance remains restricted to unchanged Beginner lobby and Bing.'})
    (output/'reference-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
    subprocess.run(['xattr','-dr','com.apple.quarantine',str(app)],check=True)
    print('PASS: full original 52-package desktop reference,128 original SJ maps; Apple product remains the two-map slice')

if __name__=='__main__':main()
