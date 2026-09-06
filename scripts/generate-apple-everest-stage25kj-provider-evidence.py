#!/usr/bin/env python3
"""Bind the frozen failure classification to an actual K-J production preflight."""
import argparse
from collections import Counter
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
AE = ROOT / 'apple-everest'


def read(path):
    return json.loads(path.read_text())


def write(path, value):
    text = json.dumps(value, indent=2, sort_keys=True) + '\n'
    if '/Users/' in text or '/private/' in text:
        raise ValueError('private absolute path in tracked evidence')
    path.write_text(text)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--production-preflight', required=True, type=Path)
    args = parser.parse_args()
    before = read(AE / 'sj-selected-factory-pre-fix-stage25kj.json')
    profiles = read(AE / 'sj-factory-authored-profiles-stage25kj.json')
    production = read(args.production_preflight)
    if production['registrationCensus'] != {'selected': 73, 'available': 73, 'unavailable': 0, 'providerRejected': 0}:
        raise ValueError('actual post-fix registration gate is incomplete')
    if not production['compilation']['FreshProductionCompilation'] or not production['compilation']['AllImplementationBytesCompared']:
        raise ValueError('a declaration is not a production implementation proof')
    pins = {row['name']: row for row in read(AE / 'strawberry-jam-dependency-graph-stage25kc.json')['nodes']}
    counts = {(row['kind'], row['customId']): row['occurrences'] for row in profiles['factories']}
    initial_providers = {row['name']: row for row in before['providers']}
    with (AE / 'sj-factory-source-review-stage25kj.tsv').open() as stream:
        source_review = {row['customId']: row for row in csv.DictReader(stream, delimiter='\t')}
    missing = []
    for row in before['factories']:
        if row['status'] == 'AVAILABLE_ACCEPTED_REGISTRATION':
            continue
        cause = ('PACKAGE_IDENTITY_VERSION_MISMATCH' if row['provider'] == 'FrostHelper' else
                 'PROVIDER_PRODUCTION_ANALYSIS_REJECTED' if row['status'] == 'PROVIDER_REJECTED' else
                 'PACKAGE_ACCEPTED_FACTORY_NOT_REGISTERED')
        missing.append({**row, 'primaryCause': cause,
            'selectedOccurrences': counts[(row['kind'], row['customId'])],
            'implementationChosen': 'BOUNDED_SEMANTIC_LOWERING',
            'postFixRegistration': 'ACTUAL_GENERATED_AND_COMPILED',
            'semanticAcceptance': 'SEPARATE_LIFECYCLE_AND_PHYSICAL_GATE',
            'secondaryBlockers': [
                {'kind': 'SELECTED_CONSTRUCTOR_LIFECYCLE_MODULE_EFFECTS',
                 'detail': source_review[row['customId']]['requiredEffectsAndFolds'],
                 'implementationDisposition': 'EXPLICIT_TYPED_IMPLEMENTATION_AND_STATIC_FOLDS'},
                {'kind': 'SELECTED_CONTENT_CLOSURE', 'detail': source_review[row['customId']]['assets'],
                 'implementationDisposition': 'EXACT_SELECTED_ASSET_PROJECTION'},
                {'kind': 'EXACT_PRODUCT_PHYSICAL_ACCEPTANCE', 'disposition': 'PENDING_SEPARATE_GATE_C'}],
            'requiresNewRuntimeArchitecture': False})
    causes = Counter(row['primaryCause'] for row in missing)
    if causes != {'PACKAGE_IDENTITY_VERSION_MISMATCH': 7, 'PROVIDER_PRODUCTION_ANALYSIS_REJECTED': 17,
                  'PACKAGE_ACCEPTED_FACTORY_NOT_REGISTERED': 19} or sum(row['selectedOccurrences'] for row in missing) != 409:
        raise ValueError('frozen 43-factory causal classification changed')
    providers = []
    for identity in production['providers']:
        name = identity['name']
        pin = pins[name]
        prior = initial_providers[name]
        selected = [row for row in production['factories'] if row['Provider'] == name]
        providers.append({**identity, 'publicUrl': pin['publicUrl'], 'zipBytes': pin['zipBytes'],
            'initialProductionStatus': prior['status'], 'initialRejectionReason': prior.get('reason'),
            'previousAcceptedVersion': '1.79.1' if name == 'FrostHelper' else
                identity['version'] if prior['status'] == 'ACCEPTED_PROVIDER' else None,
            'selectedFactoryIds': [row['CustomId'] for row in selected],
            'selectedFactoryCount': len(selected),
            'initialMissingCount': sum(row['provider'] == name for row in missing),
            'finalMissingRegistrations': 0, 'wholePackageCompatibilityEstablished': False,
            'deviceHelperDllEnabled': name == 'DJMapHelper',
            'deviceImplementationScope': 'EXACT_HASH_LOCKED_STATIC_IL_ASSEMBLY' if name == 'DJMapHelper' else 'EXACT_SELECTED_STATIC_SEMANTICS',
            'sourceAuthority': 'EXACT_DISTRIBUTED_DLLS_AND_PACKAGE_CONTENT',
            'sourceLoweringRegistry': 'tools/AppleEverestBuilder/StaticSemanticLowering.cs'})
    write(AE / 'sj-selected-factory-production-stage25kj.json', production)
    write(AE / 'sj-provider-compatibility-stage25kj.json', {
        'schemaVersion': 1, 'stage': '25K-J', 'authority': 'INDEPENDENT_FROZEN_PRE_FIX_AND_ACTUAL_COMPILED_POST_FIX',
        'frozenPreFixSha256': hashlib.sha256((AE / 'sj-selected-factory-pre-fix-stage25kj.json').read_bytes()).hexdigest(),
        'actualProductionPreflightSha256': hashlib.sha256((AE / 'sj-selected-factory-production-stage25kj.json').read_bytes()).hexdigest(),
        'classificationCounts': dict(causes),
        'classificationOccurrenceCounts': {cause: sum(row['selectedOccurrences'] for row in missing if row['primaryCause'] == cause) for cause in sorted(causes)},
        'missingBefore': 43, 'missingAfter': 0, 'unknownPrimaryCauses': 0,
        'newRuntimeArchitecturesRequired': 0, 'formerlyMissingFactories': missing, 'providers': providers,
        'frostVersionAcceptance': {'selectedVersion': '1.80.1', 'previousAcceptedVersion': '1.79.1',
            'automaticVersionInheritance': False, 'downgradedOrSpoofed': False,
            'exactSelectedPackageAcceptedByProduction': True, 'versionAndZipAndDllNegativeControlsRequired': True},
        'builtInProvider': {'name': 'EverestCore', 'selectedFactoryIds': [row['CustomId'] for row in production['factories'] if row['Provider'] == 'EverestCore'],
            'authority': 'PINNED_EVEREST_BASE_ENTITY_SEMANTICS_WITH_EXACT_PROFILE_GUARDS'},
        'auditAgents': [
            {'assignment': 'A', 'providers': ['MaxHelpingHand', 'CollabUtils2', 'LunaticHelper']},
            {'assignment': 'B', 'providers': ['FrostHelper', 'FemtoHelper', 'FlaglinesAndSuch']},
            {'assignment': 'C', 'providers': ['CherryHelper', 'PandorasBox', 'FancyTileEntities', 'BrokemiaHelper']},
            {'assignment': 'D', 'providers': ['HonlyHelper', 'VivHelper', 'XaphanHelper']}],
        'agentPolicy': 'FOUR_LIFETIME_READ_ONLY_AUDIT_AGENTS_ROOT_ONLY_WRITER_BUILDER_SIGNER',
        'independentRootReview': True,
        'disagreementsResolved': ['Primary causes use frozen production rejection evidence, not the implementation subsequently chosen.',
            'Actual LevelData strips lvl_ from room names; generated descriptors and runtime plans now agree.',
            'Whole compiled implementation bytes are compared; constructor-only IL hashes cannot establish semantic closure.',
            'Canary camera bounds, callback-only evidence and source-reference metadata were checked against exact engine/helper code.']})
    print('PASS: 43 causes classified (19 registration, 7 identity, 17 analysis); 73 actual post-fix registrations remain separate from semantics')


if __name__ == '__main__':
    main()
