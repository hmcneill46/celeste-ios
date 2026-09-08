#!/usr/bin/env python3
"""Verify K-L evidence without treating host readiness as physical acceptance."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import plistlib
import re
import subprocess
import zipfile
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
AE=ROOT/'apple-everest'
START='e69bfd6eeba3d36a5d745c24b3bc0f2f3f4ee92f'
DIAGNOSTIC=['e0d7c1988a9e5a896001735e5fe21d2251446bd0','c28c38901fef2caafc18f2d35e17996a82e93966','9c951a373395a01212ac44c302688fdf16b32382']
PROTECTED={'ios-v0.1.1-rc.1^{}':'27e16b4724d94d3991b99c4795f680fcb0e5830c','v1.0.0-rc.1^{}':'ee52b0868df091746f134d95d4f020f94f23d4fb',
           'v1.0.0-rc.2^{}':'641e86e4ed164cdf93f602ce2f11436449654d6e','origin/release/v1.0.0-rc.3':'c8134c8ca7924cf12f48527e714b5242c6024927'}
DEFERRED='IPADOS_PHYSICAL_DEFERRED_LEGACY_COMPATIBILITY_POLICY'
COMMON_PHYSICAL=['launchMenu','originalLobbyEntry','sourceRoomSpawn','jTerrainReferenceParity','realAssets','realAudio',
    'realTutorial','originalNpcCredits','khOrnateDiagnostic','representativeFactories','lobbyMapAndWarp','excludedDestinations',
    'bingPanelMetadata','lobbyToBing','bingSeveralRooms','deathRespawn','pauseResume','saveQuit','fullTerminationColdResume',
    'exactResumeRoomState','returnToLobby','reentryContinue','journal','previousProgression','slotIsolation','deleteRecreate',
    'importReplacement','corruptionFallback','coldAudioInitialization','softReload','deferredTexturesPerformance']
PHYSICAL={ 'iphone':COMMON_PHYSICAL+['touch','rotation','backgroundReopen'], 'tvos':COMMON_PHYSICAL+['controller','homeReopen'] }
def read(path):return json.loads(path.read_text())
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest()
def git(*args):return subprocess.check_output(['git','-C',str(ROOT),*args],text=True).strip()
def check(condition,label):
    if not condition:raise ValueError(label)
def write(path,value):
    text=json.dumps(value,indent=2,ensure_ascii=False)+'\n'
    check(not re.search(r'/Users/|/private/tmp/|BEGIN (?:RSA |EC )?PRIVATE KEY',text),'private data in portable evidence')
    path.parent.mkdir(parents=True,exist_ok=True);path.write_text(text)

def physical_identity(record, platform, manifest, info, revision):
    check(record.get('authority') in ['USER_REPORTED_EXACT_PRODUCT','CAPTURED_DEVICE_EVIDENCE'],
          'physical PASS needs explicit user/device evidence authority')
    check(record.get('sourceCommit')==manifest['sourceCommit']==revision and record.get('platform')==platform,
          'physical result belongs to another source revision or platform')
    check(record.get('appVersion')==info['CFBundleShortVersionString'] and
          record.get('appBuild')==info['CFBundleVersion'],'physical result belongs to another app version/build')
    check(record.get('ipaSha256')==manifest['ipaSha256'],'physical result belongs to another IPA')
    check(record.get('allFactoryLifecycleCount')==73,'actual final factory lifecycle sweep is incomplete')

def verify_products(args, product, closure, digest, revision, physical):
    out=product/'final-verification';out.mkdir(parents=True,exist_ok=True)
    factory_args=['--manifest',str(AE/'selected-factory-type-closure-stage25kh.json'),
                  '--authored-profiles',str(AE/'sj-factory-authored-profiles-stage25kj.json')]
    version=ET.parse(ROOT/'modern-ios/IOSPortVersion.props').getroot()
    for platform,field in [('ios','iphone'),('tvos','tvos')]:
        folder=args.artifact_root/platform
        apps=list((folder/'Payload').glob('*.app'));ipas=list(folder.glob('*.ipa'))
        check(len(apps)==len(ipas)==1,'one actual signed app and IPA required for '+platform)
        app=apps[0];ipa=ipas[0];record=read(folder/'build-manifest.json')
        check(record['sourceCommit']==revision and record['sourceTreeDirty'] is False,
              'actual product must come from exact clean final source revision')
        check(record['ipaSha256']==sha(ipa) and record['ipaBytes']==ipa.stat().st_size,
              'actual final IPA hash/size differs from manifest')
        check(record['sharedClosureSha256']==digest and record['platform']==platform and
              record['configuration']=='Release' and record['rid']==platform+'-arm64' and
              record['signing']=='development' and record['fullAOT'] is True and record['fullTrim'] is True and
              record['useInterpreter'] is False and record['jit'] is False,'actual final product policy differs')
        info=plistlib.loads((app/'Info.plist').read_bytes())
        check(info['CFBundleShortVersionString']==version.findtext('.//IOSPortSemanticVersion') and
              info['CFBundleVersion']==version.findtext('.//IOSPortBuildNumber'),
              'actual version/build differs from canonical authority')
        with zipfile.ZipFile(ipa) as archive:
            payload={p.relative_to(folder).as_posix():p for p in app.rglob('*') if p.is_file()}
            archived=[p.filename for p in archive.infolist() if not p.is_dir()]
            check(len(archived)==len(set(archived)) and set(payload)==set(archived),
                  'IPA file set differs from inspected signed app')
            for name,path in payload.items():
                h=hashlib.sha256()
                with archive.open(name) as stream:
                    for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
                check(h.hexdigest()==sha(path),'IPA payload differs from inspected signed app: '+name)
        subprocess.run(['python3',str(ROOT/'scripts/verify-apple-everest-stage25kl-product-content.py'),
            '--app',str(app),'--closure',str(closure),'--readiness',str(product/'real-composition/readiness.json'),
            '--output',str(out/(platform+'-content.json'))],cwd=ROOT,check=True)
        subprocess.run(['python3',str(ROOT/'scripts/verify-apple-everest-chapter-icons.py'),
            '--closure',str(closure),'--content-root',str(app/'Content'),'--packaged',
            '--output',str(out/(platform+'-chapter-icons.json'))],cwd=ROOT,check=True)
        subprocess.run(['python3',str(ROOT/'scripts/verify-apple-everest-aot-factory-product.py'),
            '--build',str(product/'build'/platform),'--app',str(app),*factory_args,
            '--output',str(out/(platform+'-aot.json')),
            '--platform-controls-output',str(out/(platform+'-platform-controls.json'))],cwd=ROOT,check=True)
        controls=read(out/(platform+'-platform-controls.json'))
        check(controls['actualProductPositive'] and controls['platform']==platform and
              controls['disposableMemoryCopiesOnly'] and len(controls['rejectedControls'])==15,
              'actual native platform or negative controls incomplete')
        check(read(out/(platform+'-aot.json'))['selectedFactories']==73,'actual native factory proof incomplete')
        if physical:
            physical_identity(physical[field],platform,record,info,revision)
            check(physical[field]['status']=='PASS' and set(physical[field]['checks'])==set(PHYSICAL[field]) and
                  all(v=='PASS' for v in physical[field]['checks'].values()),'exact final physical matrix incomplete')
            observations=physical[field].get('observations',{})
            rooms=observations.get('bingRoomsTraversed',[])
            check(isinstance(rooms,list) and all(isinstance(room,str) and room for room in rooms) and len(set(rooms))>=3 and
                  observations.get('coldResumeRoom') in rooms and observations.get('lobbyRoom')=='sj2021beginnerlobby' and
                  observations.get('returnLobbyRoom')=='sj2021beginnerlobby',
                  'physical acceptance needs observed real rooms, cold resume and return, not only checkbox counts')
        write(out/(platform+'-binding.json'),{'sourceCommit':revision,'sourceTreeDirty':False,
            'platform':platform,'appVersion':info['CFBundleShortVersionString'],'appBuild':info['CFBundleVersion'],
            'ipaSha256':record['ipaSha256'],'ipaBytes':record['ipaBytes'],'sharedClosureSha256':digest,
            'exactArchiveMatchesInspectedApp':True,'contentProofSha256':sha(out/(platform+'-content.json')),
            'nativeProofSha256':sha(out/(platform+'-aot.json'))})
    subprocess.run(['dotnet','exec','--fx-version','10.0.10',str(ROOT/'tools/AppleEverestBuilder/bin/Debug/net8.0/AppleEverestBuilder.dll'),
        'verify-aot-factory-controls','--request',str(product/'build/ios/selected-factory-product-request.json'),
        *factory_args,'--output',str(out/'aot-controls.json')],cwd=ROOT,check=True)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--product-root',type=Path,default=ROOT/'.build/apple-everest/stage25kl/product')
    parser.add_argument('--artifact-root',type=Path,default=ROOT/'artifacts/apple-everest/stage25kl')
    parser.add_argument('--reproduction-root',type=Path)
    parser.add_argument('--regression-evidence',type=Path)
    parser.add_argument('--clean-clone-evidence',type=Path)
    parser.add_argument('--physical-evidence',type=Path,help='Separate exact-product receipt for the frozen source revision')
    parser.add_argument('--verify-products',action='store_true')
    parser.add_argument('--publish-host-evidence',action='store_true')
    parser.add_argument('--require-clean',action='store_true')
    parser.add_argument('--require-ready',action='store_true')
    parser.add_argument('--output',type=Path)
    args=parser.parse_args();product=args.product_root.resolve();closure=product/'shared-closure';composition=product/'real-composition'
    if args.output:args.output.unlink(missing_ok=True)
    revision=git('rev-parse','HEAD')
    check(git('rev-parse','tvos-port')==START and git('rev-parse','origin/tvos-port')==START,'accepted K-J baseline moved')
    check(subprocess.run(['git','merge-base','--is-ancestor',START,'HEAD'],cwd=ROOT).returncode==0,'accepted K-J is not ancestor')
    for diagnostic_revision in DIAGNOSTIC:
        check(subprocess.run(['git','merge-base','--is-ancestor',diagnostic_revision,'HEAD'],cwd=ROOT).returncode==1,'rejected diagnostic is an ancestor or missing')
    for ref,protected_revision in PROTECTED.items():check(git('rev-parse',ref)==protected_revision,'protected reference changed: '+ref)
    check(subprocess.run(['git','show-ref','--verify','--quiet','refs/tags/v1.0.0-rc.3'],cwd=ROOT).returncode==1,'forbidden release tag exists')
    if args.require_clean or args.require_ready or args.verify_products:check(not git('status','--porcelain'),'worktree is dirty')
    manifest=read(closure/'compatibility-manifest.json');ready=read(composition/'readiness.json')
    compiled=read(product/'production-preflight.json');terrain=read(composition/'autotiler-conformance.json');runtime=read(composition/'runtime-composition.json')
    digest=manifest['sharedClosureSha256']
    check((composition/'READY_FOR_REAL_SJ_PRODUCT_BUILD').read_text().strip()==digest==ready['sharedClosureSha256']==compiled['sharedClosureSha256']==terrain['sharedClosureSha256'],'four-gate closure binding differs')
    check(ready['compiledPreflightSha256']==sha(product/'production-preflight.json'),'actual compiled proof changed')
    check(ready['contentPlanSha256']==sha(AE/'sj-beginner-content-stage25kl.json'),'selected plan changed')
    check(ready['gateA']=={'selectedOccurrences':920,'acceptedOrVanilla':920,'blocked':0,'unclassified':0},'gate A')
    check(ready['gateB']=={'selected':73,'available':73,'unavailable':0,'providerRejected':0},'gate B')
    check([ready['gateC'][key] for key in ['selected','closed','blocked','unknown']]==[73,73,0,0],'gate C')
    check(ready['gateD']['blocked']==ready['gateD']['unknown']==0 and ready['status']=='PASS','gate D')
    proof=compiled['compilation']
    check(proof['FreshProductionCompilation'] and proof['AllImplementationBytesCompared'] and len(proof['CompiledDlls'])==6,'actual six-DLL fresh compiler proof missing')
    guards=read(product/'compiled-canary-guards.json')
    check(len(guards)==73 and sum(row['AcceptedOccurrences'] for row in guards)==312 and all(
        row[key] for row in guards for key in ['Linked','ActualSelectorInvoked','ActualProfileGuardInvoked','UnexpectedProfileRejected']),'actual compiled canary guards incomplete')
    check(manifest['registrySha256']=='6e5b89f7d952aa98e72640abce0c75522567fb9d48f8b027e3db54f5cf9da72f','accepted registry changed')
    check([manifest[key] for key in ['managedDetourTargetCount','appleApiSurfaceMemberCount','frozenIlTransformCount','customAudioBankCount']]==[205,30,17,7],'accepted catalog scope differs')
    for tree,prefix in [('managed','managed'),('content/Content','content')]:
        files=sorted((p for p in (closure/tree).rglob('*') if p.is_file()),
                     key=lambda p:p.relative_to(closure/tree).as_posix())
        logical=''.join(p.relative_to(closure/tree).as_posix()+'\0'+str(p.stat().st_size)+'\0'+sha(p)+'\n' for p in files)
        check(len(files)==manifest[prefix+'FileCount'] and hashlib.sha256(logical.encode()).hexdigest()==manifest[prefix+'LogicalSha256'],
              'actual generated '+tree+' tree differs from closure authority')
    check(sha(closure/'collab-manifest.txt')==ready['gateD']['collabManifestSha256'] and
          sha(closure/'levelset-progression-manifest.txt')==ready['gateD']['progressionManifestSha256'],
          'actual collab/progression descriptors differ from composition proof')
    for mount in manifest['contentMounts']:check(sha(closure/'content/Content'/mount['logicalPath'])==mount['sha256'],'staged asset bytes differ')
    for source,digest_source in ready['gateD']['runtimeSourceSha256'].items():check(sha(ROOT/source)==digest_source,'current runtime source differs from proof')
    check(sha(AE/'runtime/semantics/AppleEverestAutotiler.cs')==terrain['actualProductionPartialSha256'] and
          sha(AE/'runtime/AppleEverestTileMaskRules.cs')==terrain['actualProductionMaskRulesSha256'],'current autotiler differs from proof')
    check(terrain['status']=='PASS' and terrain['realCellCount']==26 and terrain['assertions']>=89347 and len(terrain['rejectedControls'])==13,'pinned terrain conformance incomplete')
    check(all(row['apple']==row['desktop'] for row in terrain['realCells']),'actual J reference mismatch')
    cells={(row['x'],row['y']):row for row in terrain['realCells']}
    check(cells[(446,263)]['apple']['Textures']==['tilesets/SJ2021/mosscairn/grayExtended#5,14'] and
          cells[(448,263)]['apple']['Textures']==['tilesets/SJ2021/mosscairn/grayExtended#10,3'],'counterexample was lost')
    check(runtime['status']=='PASS' and runtime['assertions']>=290 and runtime['graphicsCycles']==3,'actual graphics/animation/debug/title/credits/spawn lifecycle proof incomplete')
    title=ready['gateD']['chapterTitleLayout']
    check(title['authority']=='PINNED_EVEREST_CHAPTER_TITLE_LENGTH' and title['sourceSha256']=='d7c4941d15f70ccf05c0f99d24549cb05a57f87ee77ab645057c541123318f6d' and
          title['renderConsumers']==['areaselect/title','areaselect/accent'] and title['caseCount']==8,'pinned bookmark title rendering proof missing')
    titles=runtime['chapterTitles']
    check(len(titles)==8 and all(t['bookmarkOffset']==t['referenceOffset'] for t in titles) and
          any(t['sid']=='StrawberryJam2021/1-Beginner/Bing_Over_Google' and t['measuredWidth']==835 and t['bookmarkOffset']==-345 for t in titles),
          'original long title bookmark does not match pinned Everest')
    artwork=title['artwork'];coverage=runtime['chapterBookmark']
    check(artwork['sha256']=='0839135f2baafbf652d652177e69456c07bc85343a6c93491e4e2a906034518d' and
          artwork['width']==1400 and artwork['height']==173 and artwork['generatedDescriptorVerified'] and artwork['canonicalOverrideVerified'],
          'pinned wider title graphic or actual override binding missing')
    check(coverage['leftEdge']==725 and coverage['textLeft']==825 and coverage['rightEdge']==2125 and
          coverage['viewportWidth']==1920 and coverage['omittedCoreRightEdge']==1641 and coverage['shortLeft']==1010,
          'both-edge title coverage and missing-core-asset rejection proof incomplete')
    check(len(runtime.get('creditMarkers',[]))==10 and all(row['normal']==row['debug']=='CONSUMED_MATCHES_PINNED_ROOT'
          for row in runtime['creditMarkers']),'actual lobby marker interception proof incomplete')
    check(ready['gateD'].get('creditsReference',{}).get('sourceDllSha256')=='8d5b9184204e7e7728965bcf95af5f150f6220dafbfe52fdd6c23e50d47e5258',
          'lobby loading proof lacks exact original root reference')
    panel_spawn=ready['gateD']['panelAndSpawnReference']
    check(panel_spawn['authority']=='PINNED_EVEREST_SPAWN_AND_COLLABUTILS2_TEXT_CREDITS' and
          panel_spawn['collabDllSha256']=='ce7d646252a2fc51f1f513071d97eaa1acedf7ddb0871276c688914e37319f60' and
          panel_spawn['selectedCreditTags']==0 and panel_spawn['unsupportedTagGateControlRejected'] and panel_spawn['savedRespawnBranchUnchanged'],
          'chapter credits/default spawn proof lacks exact pinned authority or saved-respawn preservation')
    lobby_spawns=[row for row in runtime['spawnReports'] if row['sid']=='StrawberryJam2021/0-Lobbies/1-Beginner']
    check(len(lobby_spawns)==1 and all(lobby_spawns[0][key]==value for key,value in
          {'spawnCount':52,'x':588,'y':40,'omittedX':376,'omittedY':1520}.items()),'original lobby spawn/omission conformance incomplete')
    credits=runtime['panelCredits']
    check(credits['referenceDrawComparisons']==40 and credits['reservedEmptyTagRow']==52 and
          credits['creditsCenterOffset']==14 and credits['pageHeight']==730 and credits['unchangedAcrossBookmarks'] and
          credits['lifecycleClear'] and credits['unsupportedTagsRejected'] and
          all(name in credits['originalText'] for name in ['Hyperlife','phant','Nano','Bissy']),
          'actual chapter credits layout/options/lifecycle conformance incomplete')
    check(sha(composition/'autotiler-conformance.json')==ready['gateD']['autotilerConformanceSha256'] and
          sha(composition/'runtime-composition.json')==ready['gateD']['runtimeProbeSha256'],'composition evidence changed')
    ledger=read(AE/'sj-bounded-fixes-stage25kl.json')
    classes=Counter(row['classification'] for row in ledger['issues'])
    check(set(classes)<={'AUTO_FIX_COMPOSITION','AUTO_IMPLEMENT_BOUNDED_COMPATIBILITY','STOP_MAJOR_ARCHITECTURE'},'invalid issue classification')
    check(not classes['STOP_MAJOR_ARCHITECTURE'],'major architecture issue remains')
    if args.publish_host_evidence:
        write(AE/'sj-beginner-autotiler-stage25kl.json',terrain)
        write(AE/'sj-beginner-composition-stage25kl.json',{'schemaVersion':1,'stage':'25K-L','sharedClosureSha256':digest,'status':'HOST_PASS','composition':ready['gateD'],'runtime':runtime})
        write(AE/'sj-beginner-production-readiness-stage25kl.json',ready)
        plan=read(AE/'sj-beginner-content-stage25kl.json')
        write(AE/'sj-beginner-slice-stage25kl.json',{'schemaVersion':1,'stage':'25K-L','status':'HOST_PREFLIGHT_PASS_PHYSICAL_PENDING','startSha':START,
            'branch':'feature/apple-everest-first-sj-slice-outcome','sharedClosureSha256':digest,'sjVersion':'1.0.12',
            'sjZipSha256':'4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655','originalMaps':ready['gateD']['maps'],
            'sourceMapCount':2,'excludedSjMapCount':126,'historicalTargetedPlanFiles':1212,'knownAdditionalRootFiles':117,
            'explicitSelectedPackageFiles':sum(len(p['includedFiles']) for p in plan['packages']),'selectedCoreAssets':len(plan['everestContent']),
            'finalMountedFilesIncludingRegressions':manifest['contentFileCount'],'pngAssetsIncludingRegressions':sum(m['sourcePath'].endswith('.png') for m in manifest['contentMounts']),
            'acceptedRegistrySha256':manifest['registrySha256'],'customAudioOrder':[{'owner':b['owner'],'sourcePath':b['sourcePath'],'loadOrdinal':b['loadOrdinal'],'sha256':b['bankSha256']} for b in manifest['customAudioBanks']],
            'singleFmodStudioSystem':True,'saveEnvelope':'AEVPSV1','iPadPhysicalStatus':DEFERRED,'historicalAllThreeDeviceBaseline':'Stage25K-B build35',
            'developmentIntegrationReady':False,'allPlatformReleaseReady':False,'boundedFixClassifications':dict(classes)})
        physical=AE/'sj-beginner-physical-stage25kl.json'
        if not physical.exists():write(physical,{'schemaVersion':1,'stage':'25K-L','status':'PENDING_EXACT_FINAL_PRODUCTS','sharedClosureSha256':digest,
            'intendedVersion':ET.parse(ROOT/'modern-ios/IOSPortVersion.props').findtext('.//IOSPortSemanticVersion'),
            'intendedBuild':ET.parse(ROOT/'modern-ios/IOSPortVersion.props').findtext('.//IOSPortBuildNumber'),
            **{platform:{'status':'PENDING','ipaSha256':None,'checks':{name:'PENDING' for name in names}} for platform,names in PHYSICAL.items()},
            'ipad':{'status':DEFERRED},'historicalAllThreeDeviceBaseline':'Stage25K-B build35','developmentIntegrationReady':False,'allPlatformReleaseReady':False})
    if args.reproduction_root:
        reproduction=read(args.reproduction_root/'reproduction.json')
        check(reproduction['sourceCommit']==revision and reproduction['sourceTreeDirty'] is False,
              'reproduction belongs to another or dirty source revision')
        check(reproduction['runsIdentical'] and reproduction['freshProductionCompilations']==3 and reproduction['fourGatesCompared'] and
              reproduction['sharedClosureSha256']==[digest]*3,'three independent final closures missing')
        check(reproduction['runSnapshotSha256']==[sha(args.reproduction_root/('run'+str(i))/'snapshot.json') for i in range(1,4)],'reproduction snapshot changed')
        snapshots=[read(args.reproduction_root/('run'+str(i))/'snapshot.json') for i in range(1,4)]
        check(snapshots[0]==snapshots[1]==snapshots[2] and snapshots[0]['completeTree']==
              {p.relative_to(closure).as_posix():sha(p) for p in sorted(closure.rglob('*')) if p.is_file()},
              'three independent snapshots differ from actual product closure')
    if args.regression_evidence:
        regressions=read(args.regression_evidence)
        check(regressions['sourceCommit']==revision and regressions['sourceTreeDirty'] is False and
              regressions['requiredCurrentPassed'] and regressions['historicalResultsDistinguished'],
              'required current regression failed or belongs to another source revision')
    if args.clean_clone_evidence:
        clone=read(args.clean_clone_evidence)
        check(clone['status']=='PASS' and clone['sourceCommit']==revision and clone['sharedClosureSha256']==digest and
              clone['copiedIgnoredFixtures'] is False and clone['independentPublicAcquisition'] and
              clone['recursiveClone'] and clone['worktreeClean'] and clone['compiledDllCount']==6 and
              clone['fourGatesPassed'] and clone['threeIndependentClosuresPassed'] and clone['currentRegressionsPassed'] and
              clone['allImplementationBytesCompared'] and clone['appliedManagedSourceLogicalSha256']==
              proof['AppliedManagedSourceLogicalSha256'],'exact final-SHA independent clean clone incomplete')
    physical=None
    if args.require_ready:
        check(args.reproduction_root and args.regression_evidence and args.clean_clone_evidence,'GREEN requires reproduction/regressions/final-SHA clone')
        check(args.physical_evidence,'GREEN requires a separate explicit exact-product physical receipt')
        physical=read(args.physical_evidence)
        check(physical['sharedClosureSha256']==digest and physical['ipad']['status']==DEFERRED,'physical closure/iPad status mismatch')
    if args.require_ready or args.verify_products:verify_products(args,product,closure,digest,revision,physical)
    names=git('ls-files').splitlines()+git('ls-files','--others','--exclude-standard').splitlines()
    for name in names:
        if name.endswith(('.md','.json','.py','.sh','.cs','.txt','.props')) and ('stage25kl' in name.lower() or 'stage25kl' in (ROOT/name).read_text(errors='replace').lower()):
            text=(ROOT/name).read_text(errors='replace')
            check(not re.search(r'/Users/[A-Za-z0-9_-]+/|BEGIN (?:RSA |EC )?PRIVATE KEY',text),'tracked private data: '+name)
    subprocess.run(['git','diff','--check'],cwd=ROOT,check=True)
    if args.require_clean or args.require_ready or args.verify_products:
        check(git('rev-parse','HEAD')==revision and not git('status','--porcelain'),'source changed during verification')
    result={'schemaVersion':1,'stage':'25K-L','status':'PASS — GREEN_IPHONE_TVOS / IPADOS_PHYSICAL_DEFERRED' if args.require_ready else 'HOST_PREFLIGHT_PASS_PHYSICAL_PENDING',
            'sourceCommit':git('rev-parse','HEAD'),'sharedClosureSha256':digest,'fourGates':'PASS','autotilerAssertions':terrain['assertions'],
            'compiledCanaryFactories':len(guards),'boundedFixClassifications':dict(classes),
            'actualProductsVerified':bool(args.require_ready or args.verify_products),
            'physicalReceiptSha256':sha(args.physical_evidence) if physical else None,
            'developmentIntegrationReady':bool(args.require_ready),'allPlatformReleaseReady':False}
    if args.output:write(args.output,result)
    print(result['status']+': four gates, original bytes,26 J cells,compiled registration/guards and source bindings verified')

if __name__=='__main__':main()
