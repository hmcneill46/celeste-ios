#!/usr/bin/env python3
"""Verify independent K-J content, production and semantic gates without inferring readiness."""
import argparse
from collections import Counter
import copy
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
AE = ROOT / 'apple-everest'
START = 'fe46b3c97cfc7dd74a907da8871ff9ba2c57d6e3'


def read(path):
    return json.loads(path.read_text())


def sha(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
    return value.hexdigest()


def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args], text=True).strip()


def logical(root):
    rows = []
    for path in sorted(root.rglob('*'), key=lambda p: p.relative_to(root).as_posix()):
        if path.is_file():
            rows.append(path.relative_to(root).as_posix() + '\0' + str(path.stat().st_size) + '\0' + sha(path) + '\n')
    return hashlib.sha256(''.join(rows).encode()).hexdigest()


def key(row):
    return row.get('kind', row.get('Kind')), row.get('customId', row.get('CustomId'))


def portable_compilation(value):
    """Compare source evidence across builds, never normalize runtime DLL bytes."""
    result = copy.deepcopy(value)
    # Full observed records remain intact on disk. Host tools and project DLLs
    # carry checkout/commit-specific build metadata. Each build independently
    # compares every implementation byte against its own fresh compiler output.
    for field in ['AppliedManagedLogicalSha256', 'CompiledDllLogicalSha256']:
        result[field] = 'PRESERVED_IN_FULL_OBSERVED_BUILD_REPORT'
    if not re.fullmatch('[0-9a-f]{64}', result['AppliedManagedSourceLogicalSha256']):
        raise ValueError('portable source digest missing')
    project_dlls = {'Celeste.dll','FNA.dll','CelesteIOSFoundation.dll','CelesteAppleInput.dll'}
    expected = project_dlls | {'DJMapHelper.dll','ChronoHelper.dll'}
    for field in ['CompiledDlls','FreshRawDlls','InspectedRawDlls']:
        if len(result[field]) != 6 or {r['Path'] for r in result[field]} != expected:
            raise ValueError('portable compiler census is not the exact six DLLs')
        for row in result[field]:
            if row['Path'] in project_dlls:
                row['Bytes'] = row['Sha256'] = 'PRESERVED_IN_FULL_OBSERVED_BUILD_REPORT'
    return result


def portable_production(value):
    result = copy.deepcopy(value)
    result['compiledAssemblySha256'] = 'PRESERVED_IN_FULL_OBSERVED_BUILD_REPORT'
    result['compilation'] = portable_compilation(result['compilation'])
    return result


def semantic_source(value):
    result = copy.deepcopy(value)
    local_identity = result['fullProductionCompilation']['CompiledDllLogicalSha256']
    result['fullProductionCompilation'] = portable_compilation(result['fullProductionCompilation'])
    for factory in result['factories']:
        if factory['implementation']['fullImplementationByteComparison'] != local_identity:
            raise ValueError('semantic row does not bind its actual observed complete compiler proof')
        factory['implementation']['fullImplementationByteComparison'] = 'PRESERVED_IN_FULL_OBSERVED_BUILD_REPORT'
        factory['canary']['initialLifecycle'] = 'SEPARATE_EXACT_PRODUCT_PHYSICAL_GATE'
        factory['acceptance'] = 'SEPARATE_EXACT_PRODUCT_PHYSICAL_GATE'
    for field in ['physical','census','pendingReason']:
        result[field] = 'SEPARATE_EXACT_PRODUCT_PHYSICAL_GATE'
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--product-root', type=Path, required=True)
    parser.add_argument('--evidence-root', type=Path, default=AE,
                        help='Fresh observed semantic evidence; use an ignored directory for final acceptance at a frozen source SHA')
    parser.add_argument('--package-root', type=Path, required=True)
    parser.add_argument('--prior-frost-package', type=Path, required=True)
    parser.add_argument('--reproduction-root', type=Path, required=True)
    parser.add_argument('--products', type=Path)
    parser.add_argument('--acceptance-evidence', type=Path)
    parser.add_argument('--fresh-clone-evidence', type=Path)
    parser.add_argument('--regression-evidence', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--require-ready', action='store_true')
    parser.add_argument('--require-clean', action='store_true')
    parser.add_argument('--publish-readiness', action='store_true')
    args = parser.parse_args()
    def external_output(path):
        path = path.resolve()
        try:
            relative = path.relative_to(ROOT).as_posix()
        except ValueError:
            return
        if git('ls-files', '--', relative) or subprocess.run(
                ['git', '-C', str(ROOT), 'check-ignore', '--no-index', '-q', '--', relative],
                capture_output=True).returncode != 0:
            raise ValueError('verification outputs must be outside the checkout or ignored and untracked')
    external_output(args.output)
    external_output(args.output.resolve().parent / '.stage25kj-verifier-output')
    inputs = [args.evidence_root / name for name in
              ['sj-selected-factory-semantic-closure-stage25kj.json', 'sj-preintegration-readiness-stage25kj.json']]
    inputs += [path for path in [args.acceptance_evidence, args.fresh_clone_evidence,
                                args.regression_evidence, args.product_root / 'production-preflight.json'] if path]
    if args.output.resolve() in {path.resolve() for path in inputs}:
        raise ValueError('machine verification output must not overwrite an input evidence file')
    if args.publish_readiness:
        if not args.require_ready:
            raise ValueError('readiness publication requires --require-ready')
        destination = args.evidence_root.resolve() / 'sj-preintegration-readiness-stage25kj.json'
        external_output(destination)
        if args.output.resolve() == destination:
            raise ValueError('machine verification and published readiness must use distinct files')
    checks = []
    def require(value, label):
        if not value:
            raise ValueError(label)
        checks.append(label)
    out = args.output.resolve().parent
    out.mkdir(parents=True, exist_ok=True)
    def run(command, name):
        with (out / (name + '.log')).open('w') as log:
            result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
        require(result.returncode == 0, name + ' executable gate (see its log on failure)')
    product = args.product_root.resolve()
    closure = product / 'shared-closure'
    production = read(product / 'production-preflight.json')
    manifest = read(closure / 'compatibility-manifest.json')
    baseline = read(AE / 'sj-selected-factory-pre-fix-stage25kj.json')
    provider = read(AE / 'sj-provider-compatibility-stage25kj.json')
    evidence_root = args.evidence_root.resolve()
    semantic = read(evidence_root / 'sj-selected-factory-semantic-closure-stage25kj.json')
    readiness = read(evidence_root / 'sj-preintegration-readiness-stage25kj.json')
    profiles = read(AE / 'sj-factory-authored-profiles-stage25kj.json')
    plan = read(AE / 'sj-factory-canary-plan-stage25kj.json')
    require(baseline['baselineCommit'] == START and baseline['evidencePhase'] == 'PRE_FIX_IMMUTABLE'
        and baseline['registrationCensus'] == {'selected': 73, 'available': 30, 'unavailable': 43}
        and baseline['occurrenceCensus'] == {'selected': 920, 'withRegistrations': 511, 'missingRegistrations': 409},
        'independently reproduced frozen 30/43 and 511/409 baseline')
    require(provider['classificationCounts'] == {'PACKAGE_IDENTITY_VERSION_MISMATCH': 7,
        'PROVIDER_PRODUCTION_ANALYSIS_REJECTED': 17, 'PACKAGE_ACCEPTED_FACTORY_NOT_REGISTERED': 19}
        and provider['unknownPrimaryCauses'] == 0 and provider['newRuntimeArchitecturesRequired'] == 0,
        '43 individual primary causes classified without new-architecture overclaim')
    require(len(provider['formerlyMissingFactories']) == 43 and
        sum(r['selectedOccurrences'] for r in provider['formerlyMissingFactories']) == 409,
        'all 43 causes retain exact affected occurrence weight')
    require(production['contentIdCensus'] == {'selectedOccurrences': 920, 'acceptedOrVanilla': 920, 'blocked': 0, 'unclassified': 0}, 'independent content-ID gate A')
    require(production['registrationCensus'] == {'selected': 73, 'available': 73, 'unavailable': 0, 'providerRejected': 0}, 'independent production-registration gate B')
    require(not production['semanticClosureEstablished'] and production['exactPackageIdentityVerified']
        and production['actualProductionRegenerated'] and production['compilation']['FreshProductionCompilation']
        and production['compilation']['AllImplementationBytesCompared'] and len(production['compilation']['CompiledDlls']) == 6,
        'actual complete six-DLL compiler proof is separate from physical semantics')
    selected = {key(row) for row in production['factories']}
    require(len(selected) == 73 and selected == {key(r) for r in semantic['factories']} ==
        {key(r) for r in profiles['factories']} == {key(r) for r in plan['representatives']}, 'exact 73-factory joins')
    require(len(profiles['occurrences']) == 920 and sum(r['AcceptedOccurrences'] for r in production['factories']) == 920, 'all 920 selected authored occurrences covered')
    require(production['sharedClosureSha256'] == manifest['sharedClosureSha256'] == semantic['sharedClosureSha256'] == readiness['sharedClosureSha256'], 'four independent evidence records name one shared closure')
    require(portable_production(production) == portable_production(read(AE / 'sj-selected-factory-production-stage25kj.json'))
        and sha(AE / 'sj-selected-factory-production-stage25kj.json') == provider['actualProductionPreflightSha256'],
        'fresh complete compiler proof matches the tracked portable source/profile evidence')
    require(semantic_source(semantic) == semantic_source(read(AE/'sj-selected-factory-semantic-closure-stage25kj.json')),
        'fresh semantic source evidence matches tracked review without transferring physical acceptance')
    for row in production['compilation']['InspectedRawDlls']:
        file = product/'preflight-runtime/bin/Release/net10.0-ios26.5'/row['Path']
        require(file.stat().st_size == row['Bytes'] and sha(file) == row['Sha256'],
            'actual complete local compiler-inspected bytes '+row['Path'])
    require(sha(AE / 'sj-selected-factory-pre-fix-stage25kj.json') == provider['frozenPreFixSha256'], 'frozen diagnostic hash retained')
    require(sha(evidence_root / 'sj-selected-factory-semantic-closure-stage25kj.json') == readiness['semanticClosureSha256'], 'readiness binds semantic artifact')
    for folder, field in [('managed', 'managedLogicalSha256'), ('content/Content', 'contentLogicalSha256')]:
        require(logical(closure / folder) == manifest[field], 'actual ' + folder + ' tree matches closure manifest')
    require(sha(closure / 'managed/GeneratedAppleEverestGameplayRegistry.cs') == production['actualFactoryRegistrySha256'], 'actual generated gameplay registry hash')
    run(['python3', str(ROOT / 'scripts/verify-apple-everest-chapter-icons.py'),
        '--closure', str(closure), '--content-root', str(ROOT / '.build/celeste-ios/current/content/Content'),
        '--output', str(out / 'chapter-icons.json')], 'chapter-icons')
    for row in semantic['sharedSourceEffects']:
        require(sha(ROOT / row['path']) == row['sha256'], 'current shared source ' + row['path'])
    for factory in semantic['factories']:
        for binding in factory['implementation']['sourceBindings']:
            for row in binding['sourceFiles']:
                require(sha(ROOT / row['path']) == row['sha256'], 'current selected source ' + binding['type'])
    require(sha(AE / 'sj-factory-source-review-stage25kj.tsv') == semantic['auditedSourceNotesSha256']
        and semantic['sourceReviewed'] == 73 and not semantic['compiledBodyHashesAreExecutionEvidence']
        and not semantic['structuralGraphIsSemanticAuthority'], 'source review retains its authority boundaries')
    pins = {r['name']: r for r in read(AE / 'strawberry-jam-dependency-graph-stage25kc.json')['nodes']}
    for row in production['providers']:
        pin = pins[row['name']]
        path = args.package_root.resolve() / (row['name'] + '.zip')
        require(sha(path) == pin['zipSha256'] and row['version'] == pin['resolvedVersion'], 'actual exact public ZIP/version ' + row['name'])
        with zipfile.ZipFile(path) as archive:
            for dll in pin['distributedDlls']:
                paths = [n for n in archive.namelist() if Path(n).name == dll['path']]
                require(len(paths) == 1 and hashlib.sha256(archive.read(paths[0])).hexdigest() == dll['sha256'], 'actual distributed DLL ' + row['name'])
    require(not plan['originalGameplayMapsCopied'] and not readiness['originalSjGameplayMapsPackaged']
        and not any('/maps/strawberryjam2021/' in '/' + p.relative_to(closure).as_posix().lower() for p in closure.rglob('*')),
        'no original Strawberry Jam gameplay map in actual canary closure')
    compiled = read(product / 'compiled-canary-profiles.json')
    guards = read(product / 'compiled-canary-guards.json')
    require(compiled['actualCompiledCanaryBins'] and compiled['plannedRepresentativesBoundToCompiledProfiles'] == 73
        and compiled['canaryPlanSha256'] == sha(AE / 'sj-factory-canary-plan-stage25kj.json')
        and len(guards) == 73 and {key(r) for r in guards} == selected
        and semantic['compiledCanaryProfilesSha256'] == sha(product/'compiled-canary-profiles.json')
        and semantic['compiledCanaryGuardResultsSha256'] == sha(product/'compiled-canary-guards.json')
        and all(r['ActualProfileGuardInvoked'] and r['UnexpectedProfileRejected'] for r in guards),
        'actual generated BIN profiles execute exact compiled fail-closed guards')
    runner = ['dotnet', 'exec', '--fx-version', '10.0.10', str(ROOT / 'tools/AppleEverestBuilder/bin/Debug/net8.0/AppleEverestBuilder.dll')]
    assembly = product / 'preflight-runtime/bin/Release/net10.0-ios26.5/Celeste.dll'
    factory_args = ['--manifest', str(AE / 'selected-factory-type-closure-stage25kh.json'),
                    '--authored-profiles', str(AE / 'sj-factory-authored-profiles-stage25kj.json')]
    run(runner + ['verify-compiled-factory-controls', '--assembly', str(assembly), *factory_args,
        '--output', str(out / 'compiled-controls.json')], 'compiled-controls')
    controls = read(out / 'compiled-controls.json')
    require(controls['positiveFactories'] == 73 and controls['positiveOccurrences'] == 920 and
        [(r['removed'], r['available']) for r in controls['omissions']] == [(16,57),(11,62),(7,66),(5,68),(5,68),(4,69),(5,68),(3,70)]
        and all(r['unchangedUnrelatedRegistrationHashes'] and r['actualCompiledSelectorOmission'] for r in controls['omissions'])
        and len(controls['rejectedCompiledControls']) == 5 and len(controls['rejectedImplementationControls']) == 4,
        'eight compiled semantic-group omissions plus nine false-positive controls')
    run(runner + ['verify-factory-package-controls', '--repo-root', str(ROOT), '--selected-archive',
        str(args.package_root.resolve() / 'FrostHelper.zip'), '--prior-archive', str(args.prior_frost_package.resolve()),
        '--output', str(out / 'package-controls.json')], 'package-controls')
    pc = read(out / 'package-controls.json')
    require(pc['exactPublicInputAccepted'] and pc['exactSelectedVersion'] == '1.80.1' and len(pc['rejectedControls']) == 5,
        'Frost exact selected input passes while older/newer/owner/ZIP/DLL controls reject')
    run(['python3', str(ROOT / 'scripts/verify-apple-everest-stage25kj-sideways.py'), '--product-root', str(product),
        '--output', str(out / 'sideways-controls.json')], 'sideways-controls')
    reproduction = args.reproduction_root.resolve()
    snapshots = [read(reproduction / ('run' + str(i)) / 'snapshot.json') for i in range(1, 4)]
    rp = read(reproduction / 'reproduction.json')
    require(snapshots[0] == snapshots[1] == snapshots[2] and len(snapshots[0]['completeTree']) > 1000
        and rp['runSnapshotSha256'] == [sha(reproduction / ('run' + str(i)) / 'snapshot.json') for i in range(1, 4)]
        and rp['sharedClosureSha256'] == [manifest['sharedClosureSha256']] * 3
        and snapshots[0]['registrationCensus'] == production['registrationCensus'], 'three freshly generated complete closure snapshots and compiler proofs identical')
    require(snapshots[0]['completeTree'] == {p.relative_to(closure).as_posix(): sha(p) for p in sorted(closure.rglob('*')) if p.is_file()},
        'three-run inventory equals the actual product closure')
    require(manifest['managedDetourTargetCount'] == 205 and manifest['appleApiSurfaceMemberCount'] == 30
        and manifest['configuredPlanCount'] == 0 and manifest['directManagedHookCount'] == 0
        and not manifest['runtimeDllLoading'] and manifest['runtimeDetour'] == 'absent' and not manifest['interpreter'], 'unchanged 205-target / 30-API static runtime boundary')
    require(manifest['customAudioBankCount'] == 7 and manifest['frozenIlTransformCount'] == 17, 'seven custom banks and 17 bounded frozen transforms')
    for ref, expected in {'tvos-port':START, 'origin/tvos-port':START,
        'ios-v0.1.1-rc.1^{}':'27e16b4724d94d3991b99c4795f680fcb0e5830c',
        'v1.0.0-rc.1^{}':'ee52b0868df091746f134d95d4f020f94f23d4fb',
        'v1.0.0-rc.2^{}':'641e86e4ed164cdf93f602ce2f11436449654d6e',
        'origin/release/v1.0.0-rc.3':'c8134c8ca7924cf12f48527e714b5242c6024927'}.items():
        require(git('rev-parse', ref) == expected, 'protected ref ' + ref)
    require(subprocess.run(['git','-C',str(ROOT),'rev-parse','--verify','refs/tags/v1.0.0-rc.3'],capture_output=True).returncode != 0, 'rc3 tag absent')
    require(subprocess.run(['git','-C',str(ROOT),'merge-base','--is-ancestor',START,'HEAD']).returncode == 0, 'accepted K-H ancestry')
    for commit in ['c28c38901fef2caafc18f2d35e17996a82e93966', 'e0d7c1988a9e5a896001735e5fe21d2251446bd0']:
        require(subprocess.run(['git','-C',str(ROOT),'merge-base','--is-ancestor',commit,'HEAD']).returncode == 1, 'diagnostic/integration commit remains outside ancestry')
    require(not git('diff',START,'--name-only','--','.github/workflows'), 'zero workflow changes; verifier dispatches no Actions')
    changed = set(git('diff',START,'--name-only').splitlines()) | set(git('ls-files','--others','--exclude-standard').splitlines())
    for name in sorted(changed):
        path = ROOT / name
        if not path.is_file():
            continue
        require(path.suffix.lower() not in {'.zip','.dll','.bank','.ipa','.bin','.p12','.mobileprovision'}, 'no private binary input/product added: ' + name)
        if path.suffix.lower() in {'.md','.json','.tsv','.yaml','.xml','.cs','.py','.sh','.targets','.props','.csproj'}:
            text = path.read_text()
            require(not re.search(r'/Users/[A-Za-z0-9_-]+/|/private/tmp/[A-Za-z0-9_-]+|BEGIN (?:RSA |EC )?PRIVATE KEY', text), 'privacy ' + name)
            if path.suffix == '.md':
                for target in re.findall(r'\]\(([^)]+)\)', text):
                    target = target.strip('<>').split('#',1)[0]
                    if target and not re.match(r'^[a-z]+:',target) and not target.startswith('/'):
                        require((path.parent / target).exists(), 'local document link ' + target)
    require(not any(line[0] in '-+U' for line in git('submodule','status','--recursive').splitlines() if line), 'recursive submodule revisions exact')
    product_verified = False
    if args.products:
        for platform in ['ios','tvos']:
            folder = args.products.resolve() / platform
            apps = list((folder / 'Payload').glob('*.app')); ipas = list(folder.glob('*.ipa'))
            require(len(apps) == len(ipas) == 1, 'one exact '+platform+' app and IPA')
            run(['python3', str(ROOT / 'scripts/verify-apple-everest-chapter-icons.py'),
                '--closure', str(closure), '--content-root', str(apps[0] / 'Content'), '--packaged',
                '--output', str(out / (platform + '-chapter-icons.json'))], platform + '-chapter-icons')
            pm = read(folder / 'build-manifest.json')
            require(sha(ipas[0]) == pm['ipaSha256'] and ipas[0].stat().st_size == pm['ipaBytes']
                and pm['sharedClosureSha256'] == manifest['sharedClosureSha256'], 'exact '+platform+' IPA identity')
            with zipfile.ZipFile(ipas[0]) as archive:
                require(not any('/maps/strawberryjam2021/' in '/' + n.lower() for n in archive.namelist()), 'no original gameplay map in '+platform+' IPA')
                payload = {p.relative_to(folder).as_posix(): p for p in apps[0].rglob('*') if p.is_file()}
                archived = {p.filename for p in archive.infolist() if not p.is_dir()}
                require(set(payload) == archived and all(sha(path) == hashlib.sha256(archive.read(name)).hexdigest()
                    for name,path in payload.items()), 'exact '+platform+' IPA payload equals the independently inspected app')
            run(['python3',str(ROOT/'scripts/verify-apple-everest-aot-factory-product.py'),'--build',str(product/'build'/platform),
                '--app',str(apps[0]),*factory_args,'--output',str(out/(platform+'-aot-proof.json')),
                '--platform-controls-output',str(out/(platform+'-platform-controls.json'))],platform+'-aot-proof')
            platform_controls = read(out/(platform+'-platform-controls.json'))
            require(platform_controls['actualProductPositive'] and platform_controls['platform'] == platform
                and platform_controls['disposableMemoryCopiesOnly'] and len(platform_controls['rejectedControls']) == 15,
                'actual '+platform+' native platform and 15 isolated platform corruption controls')
        run(runner + ['verify-aot-factory-controls','--request',str(product/'build/ios/selected-factory-product-request.json'),
            *factory_args,'--output',str(out/'aot-controls.json')], 'aot-controls')
        product_verified = True
    physical_binding_verified = False
    if args.acceptance_evidence:
        require(args.products is not None, 'acceptance requires exact actual products')
        with tempfile.TemporaryDirectory(prefix='kj-semantic-', dir=out) as temp:
            run(['python3',str(ROOT/'scripts/generate-apple-everest-stage25kj-semantic-evidence.py'),
                '--product-root',str(product),'--products',str(args.products.resolve()),'--acceptance-evidence',str(args.acceptance_evidence.resolve()),
                '--evidence-output',temp], 'independent-physical-binding')
            regenerated = read(Path(temp)/'sj-selected-factory-semantic-closure-stage25kj.json')
            require(regenerated == semantic, 'tracked semantic evidence independently regenerates from exact physical receipt')
            physical_binding_verified = True
    physical_pass = semantic['census'] == {'selected':73,'fullyClosed':73,'blocked':0,'unknown':0}
    if physical_pass:
        require(physical_binding_verified and product_verified, 'closed semantic census requires independently verified exact-product physical evidence')
    if not physical_pass:
        require(semantic['census'] == {'selected':73,'fullyClosed':0,'blocked':0,'unknown':73}
            and readiness['status'] == 'IN_PROGRESS' and readiness['readinessMarker'] is None
            and not readiness['developmentIntegrationReady'] and all(f['acceptance']['status'] != 'CLOSED' for f in semantic['factories']),
            'pending physical acceptance cannot create GREEN or a closed semantic count')
    if args.require_clean or args.require_ready:
        require(not git('status','--porcelain'), 'clean worktree')
    ready = False
    if args.require_ready:
        require(physical_pass and product_verified and args.acceptance_evidence is not None, 'exact-product physical and native gates required for readiness')
        for platform in ['ios','tvos']:
            exact = read(args.products.resolve()/platform/'build-manifest.json')
            require(exact['sourceCommit'] == git('rev-parse','HEAD') and not exact['sourceTreeDirty'],
                'exact clean final-SHA '+platform+' product')
        require(args.fresh_clone_evidence is not None and args.regression_evidence is not None, 'fresh final-SHA clone and current regression evidence required')
        fresh = read(args.fresh_clone_evidence)
        require(fresh['sourceCommit'] == git('rev-parse','HEAD') and fresh['status'] == 'PASS'
            and fresh['independentPublicAcquisition'] and fresh['recursiveClone'] and fresh['worktreeClean']
            and fresh['sharedClosureSha256'] == manifest['sharedClosureSha256']
            and fresh['appliedManagedSourceLogicalSha256'] == production['compilation']['AppliedManagedSourceLogicalSha256']
            and fresh['compiledDllCount'] == 6 and fresh['allImplementationBytesCompared'],
            'exact final-SHA independent clean clone and complete compiler input binding')
        regressions = read(args.regression_evidence)
        require(regressions['sourceCommit'] == git('rev-parse','HEAD') and regressions['requiredCurrentPassed']
            and regressions['historicalResultsDistinguished'], 'current regression gates bound to final SHA')
        ready = True
    if args.publish_readiness:
        require(ready and evidence_root != AE, 'readiness publication requires complete gates and an external acceptance evidence directory')
    result = {'schemaVersion':1,'stage':'25K-J','sourceCommit':git('rev-parse','HEAD'),
        'status':'GREEN' if ready else 'IN_PROGRESS','checksPassed':len(checks),'checks':checks,
        'sharedClosureSha256':manifest['sharedClosureSha256'],'contentIdCensus':production['contentIdCensus'],
        'registrationCensus':production['registrationCensus'],'semanticClosureCensus':semantic['census'],
        'exactProductsVerified':product_verified,'readinessMarker':'READY_FOR_K_K_REAL_SJ_INTEGRATION_RETRY' if ready else None}
    args.output.write_text(json.dumps(result,indent=2,sort_keys=True)+'\n')
    if args.publish_readiness:
        readiness.update(status='GREEN', readinessMarker=result['readinessMarker'], developmentIntegrationReady=True,
            semanticClosureCensus=semantic['census'], sourceCommit=result['sourceCommit'],
            machineVerificationSha256=sha(args.output), physicalReceiptSha256=sha(args.acceptance_evidence),
            authority='EXACT_FROZEN_SOURCE_REVISION_PLUS_SEPARATE_VERIFIED_ACCEPTANCE_ARTIFACT')
        (evidence_root/'sj-preintegration-readiness-stage25kj.json').write_text(json.dumps(readiness,indent=2,sort_keys=True)+'\n')
    print('PASS: Stage 25K-J verifier ('+str(len(checks))+' checks; '+result['status']+')')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, KeyError, OSError) as error:
        print('FAIL: '+str(error),file=sys.stderr)
        raise SystemExit(1)
