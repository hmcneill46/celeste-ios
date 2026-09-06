#!/usr/bin/env python3
"""Join reviewed source notes to actual implementation evidence; keep C independent."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import plistlib
import re
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
AE = ROOT / 'apple-everest'


def read(path):
    return json.loads(path.read_text())


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(path, value):
    text = json.dumps(value, indent=2, sort_keys=True) + '\n'
    if any(prefix in text for prefix in ['/Users/', '/private/', '/Volumes/']):
        raise ValueError('private path in tracked semantic evidence')
    path.write_text(text)


def key(row):
    return row.get('kind', row.get('Kind')), row.get('customId', row.get('CustomId'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--product-root', required=True, type=Path)
    parser.add_argument('--acceptance-evidence', type=Path)
    parser.add_argument('--products', type=Path)
    parser.add_argument('--evidence-output', type=Path, default=AE)
    args = parser.parse_args()
    destination = args.evidence_output.resolve()
    destination.mkdir(parents=True, exist_ok=True)
    root = args.product_root.resolve()
    production = read(root / 'production-preflight.json')
    closure = read(root / 'shared-closure/compatibility-manifest.json')
    compiled = read(root / 'compiled-canary-profiles.json')
    guards = {key(row): row for row in read(root / 'compiled-canary-guards.json')}
    profiles = read(AE / 'sj-factory-authored-profiles-stage25kj.json')
    plan = read(AE / 'sj-factory-canary-plan-stage25kj.json')
    representatives = {key(row): row for row in plan['representatives']}
    review_path = AE / 'sj-factory-source-review-stage25kj.tsv'
    with review_path.open() as stream:
        rows = list(csv.DictReader(stream, delimiter='\t'))
    notes = {row['customId']: row for row in rows}
    selected = {key(row) for row in production['factories']}
    if (len(selected) != 73 or len(rows) != 73 or len(notes) != 73
            or {row[1] for row in selected} != set(notes)
            or selected != set(representatives) or selected != set(guards)):
        raise ValueError('source review, actual factory and authored representative sets differ')
    if (production['registrationCensus'] != {'selected': 73, 'available': 73, 'unavailable': 0, 'providerRejected': 0}
            or not production['compilation']['FreshProductionCompilation']
            or not production['compilation']['AllImplementationBytesCompared']
            or production['sharedClosureSha256'] != closure['sharedClosureSha256']
            or not compiled['actualCompiledCanaryBins'] or compiled['originalGameplayMapsCopied']
            or compiled['plannedRepresentativesBoundToCompiledProfiles'] != 73
            or compiled['canaryPlanSha256'] != sha(AE / 'sj-factory-canary-plan-stage25kj.json')):
        raise ValueError('source ledger cannot substitute for actual production/profile proof')
    acceptance = read(args.acceptance_evidence) if args.acceptance_evidence else None
    if acceptance and acceptance['sharedClosureSha256'] != closure['sharedClosureSha256']:
        raise ValueError('physical acceptance belongs to a different closure')
    # Every acceptance fact comes from a separate explicit report. The source
    # inventory and nonempty compiled methods cannot manufacture those facts.
    physical = {platform: (acceptance or {}).get('physical', {}).get(platform, {'status': 'PENDING'})
                for platform in ['ios', 'tvos', 'ipados']}
    physical['ipados'] = (acceptance or {}).get('physical', {}).get('ipados',
        {'status': 'IPADOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE'})
    semantic_pass = all(physical[p].get('status') == 'PASS' and physical[p].get('allFactoryLifecycleCount') == 73
                        and physical[p].get('representativeSemanticChecks') == 'PASS' for p in ['ios', 'tvos'])
    if semantic_pass and not all(physical[p].get('authority') in ['USER_REPORTED_EXACT_PRODUCT', 'CAPTURED_DEVICE_EVIDENCE']
                                 and re.fullmatch(r'[0-9a-f]{64}', physical[p].get('ipaSha256', '')) for p in ['ios', 'tvos']):
        raise ValueError('physical pass requires explicit source and exact product identity')
    if semantic_pass:
        if not args.products:
            raise ValueError('physical pass requires the actual signed products directory')
        for platform in ['ios', 'tvos']:
            folder = args.products.resolve() / platform
            manifest = read(folder / 'build-manifest.json')
            ipas = list(folder.glob('*.ipa'))
            apps = list((folder / 'Payload').glob('*.app'))
            if len(ipas) != 1 or len(apps) != 1:
                raise ValueError('physical product must have one exact IPA and app')
            digest = sha(ipas[0])
            app_info = plistlib.loads((apps[0] / 'Info.plist').read_bytes())
            if (physical[platform]['ipaSha256'] != digest or manifest['ipaSha256'] != digest
                    or manifest['ipaBytes'] != ipas[0].stat().st_size
                    or manifest['sharedClosureSha256'] != closure['sharedClosureSha256']
                    or manifest['platform'] != platform or not manifest['fullAOT'] or not manifest['fullTrim']
                    or manifest['useInterpreter'] or manifest['jit']
                    or physical[platform].get('sourceCommit') != manifest['sourceCommit']
                    or physical[platform].get('appVersion') != app_info['CFBundleShortVersionString']
                    or physical[platform].get('appBuild') != app_info['CFBundleVersion']):
                raise ValueError('physical evidence does not match the actual exact signed product')
            with zipfile.ZipFile(ipas[0]) as archive:
                actual = {p.relative_to(folder).as_posix(): p for p in apps[0].rglob('*') if p.is_file()}
                archived = {p.filename for p in archive.infolist() if not p.is_dir()}
                if set(actual) != archived or any(sha(path) != hashlib.sha256(archive.read(name)).hexdigest()
                                                  for name, path in actual.items()):
                    raise ValueError('IPA payload differs from independently checked signed app')
            subprocess.run(['python3', str(ROOT / 'scripts/verify-apple-everest-aot-factory-product.py'),
                '--build', str(root / 'build' / platform), '--app', str(apps[0]),
                '--manifest', str(AE / 'selected-factory-type-closure-stage25kh.json'),
                '--authored-profiles', str(AE / 'sj-factory-authored-profiles-stage25kj.json'),
                '--output', str(root / 'build' / platform / 'linked-selected-factories.json')], check=True)
    mods = {row['name']: row for row in closure['selectedMods']}
    runtime_files = list((AE / 'runtime').rglob('*.cs'))
    runtime_sources = {path: path.read_text() for path in runtime_files}
    providers = {}
    for owner in {row['Provider'] for row in production['factories']}:
        mod = mods.get(owner, {})
        lowering = mod.get('staticSemanticLowering')
        providers[owner] = {'exactPackage': next((row for row in production['providers'] if row['name'] == owner), None),
            'modulePlan': (lowering or {}).get('Module'), 'selectedLoweringPlan': lowering,
            'wholeDesktopModuleEnabled': owner == 'DJMapHelper',
            'wholePackageCompatibilityEstablished': False,
            'moduleDisposition': ('SELECTED_ORIGINAL_ASSEMBLY_STATICALLY_TRANSFORMED' if owner == 'DJMapHelper' else
                'TYPED_MODULE_STATE_AND_EXPLICIT_STATIC_HOOK_EFFECTS' if (lowering or {}).get('Module') else
                'DESKTOP_MODULE_OMITTED_REQUIRED_SELECTED_EFFECTS_EXPLICIT'),
            'assetProjection': [row for row in closure['contentMounts'] if row['owner'] == owner]}
    factories = []
    for factory in sorted(production['factories'], key=key):
        k = key(factory)
        if not guards[k]['ActualProfileGuardInvoked'] or not guards[k]['UnexpectedProfileRejected']:
            raise ValueError('actual canary profile guard was not exercised')
        bindings = []
        for name in factory['ConcreteTypes']:
            short = name.rsplit('.', 1)[-1].split('/')[0]
            matches = [path for path, text in runtime_sources.items()
                       if re.search(r'\b(?:class|struct)\s+' + re.escape(short) + r'\b', text)]
            bindings.append({'type': name, 'sourceFiles': [{'path': path.relative_to(ROOT).as_posix(), 'sha256': sha(path)}
                                                          for path in matches],
                             'externalAssemblyAuthority': 'DJMapHelper' if factory['Provider'] == 'DJMapHelper' else None})
        factories.append({'kind': k[0], 'customId': k[1], 'provider': factory['Provider'],
            'selectedOriginalOccurrences': factory['AcceptedOccurrences'],
            'selectedProfileSha256': sorted({row['profileSha256'] for row in profiles['occurrences'] if key(row) == k}),
            'sourceReview': {**notes[k[1]], 'status': 'ROOT_REVIEWED_SELECTED_SOURCE',
                            'authority': 'PINNED_DISTRIBUTED_DLL_OR_PINNED_EVEREST_SOURCE',
                            'reviewLedgerSha256': sha(review_path)},
            'implementation': {'entryMethod': factory['EntryMethod'], 'registrationSha256': factory['RegistrationSha256'],
                'guardSha256': factory['GuardSha256'], 'productionCallers': factory['ProductionCallers'],
                'sourceBindings': bindings, 'compiledTypeClosure': factory['TypeClosure'],
                'fullImplementationByteComparison': production['compilation']['CompiledDllLogicalSha256']},
            'canary': {'representative': representatives[k], 'compiledProfileGuardAccepted': True,
                       'unexpectedProfileRejected': True,
                       'compiledOccurrences': sum(key(row) == k for row in compiled['occurrences']),
                       'initialLifecycle': 'PASS' if semantic_pass else 'PENDING_DEVICE_EXECUTION'},
            'acceptance': {'status': 'CLOSED' if semantic_pass else 'PENDING_PHYSICAL_ACCEPTANCE',
                           'ios': physical['ios']['status'], 'tvos': physical['tvos']['status']}})
    common = ['tools/AppleEverestBuilder/ClosureGenerator.cs', 'tools/AppleEverestBuilder/StaticSemanticRuntimePatches.cs',
              'tools/AppleEverestBuilder/StaticSemanticLowering.cs', 'tools/AppleEverestBuilder/SelectedSidewaysIlPlans.cs',
              'tools/AppleEverestIlWorker/SelectedSidewaysIlLowering.cs', 'apple-everest/runtime/AppleEverestCollabRuntime.cs',
              'apple-everest/runtime/AppleEverestCollabSession.cs', 'apple-everest/runtime/AppleEverestProgressionPersistence.cs']
    semantic = {'schemaVersion': 1, 'stage': '25K-J', 'authority': 'SOURCE_REVIEW_PLUS_ACTUAL_IMPLEMENTATION_PLUS_SEPARATE_PHYSICAL_GATE',
        'sharedClosureSha256': closure['sharedClosureSha256'], 'sourceReviewed': 73,
        'census': {'selected': 73, 'fullyClosed': 73 if semantic_pass else 0, 'blocked': 0, 'unknown': 0 if semantic_pass else 73},
        'pendingReason': None if semantic_pass else 'EXACT_KJ_DEVICE_LIFECYCLE_AND_REPRESENTATIVE_SEMANTICS_NOT_YET_ACCEPTED',
        'compiledBodyHashesAreExecutionEvidence': False, 'structuralGraphIsSemanticAuthority': False,
        'providers': providers, 'factories': factories, 'physical': physical,
        'sharedSourceEffects': [{'path': path, 'sha256': sha(ROOT / path)} for path in common],
        'fullProductionCompilation': production['compilation'],
        'compiledCanaryProfilesSha256': sha(root / 'compiled-canary-profiles.json'),
        'compiledCanaryGuardResultsSha256': sha(root / 'compiled-canary-guards.json'),
        'auditedSourceNotesSha256': sha(review_path)}
    semantic_path = destination / 'sj-selected-factory-semantic-closure-stage25kj.json'
    write(semantic_path, semantic)
    # The permanent verifier owns GREEN after recomputing the machine gates.
    # Strings supplied in a physical acceptance report cannot establish them.
    complete = False
    write(destination / 'sj-preintegration-readiness-stage25kj.json', {'schemaVersion': 1, 'stage': '25K-J',
        'status': 'GREEN' if complete else 'IN_PROGRESS', 'contentIdCensus': production['contentIdCensus'],
        'productionRegistrationCensus': production['registrationCensus'], 'semanticClosureCensus': semantic['census'],
        'semanticClosureSha256': sha(semantic_path), 'sharedClosureSha256': closure['sharedClosureSha256'],
        'physical': physical, 'gates': {},
        'readinessMarker': 'READY_FOR_K_K_REAL_SJ_INTEGRATION_RETRY' if complete else None,
        'originalSjGameplayMapsPackaged': False, 'kiMerged': False, 'kgMerged': False,
        'lastAllThreeDevicePhysicalGreen': 'Stage 25K-B build 35',
        'developmentIntegrationReady': complete, 'allPlatformReleaseReady': False,
        'fullBeginnerSupported': False, 'fullStrawberryJamSupported': False})
    print('PASS:73 source reviews bound to actual production; semantic acceptance=' + ('PASS' if semantic_pass else 'PENDING'))


if __name__ == '__main__':
    main()
