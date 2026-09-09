"""Project-owned synthetic audit controls; no third-party maps or binaries."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import types
import unittest
import zipfile

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('stage25km',ROOT/'scripts/audit-apple-everest-stage25km.py')
km=importlib.util.module_from_spec(spec);spec.loader.exec_module(km)

class AuditControls(unittest.TestCase):
    def test_unknown_provider_never_ready(self):
        gates=km.gate_counts([{'provider':'UNKNOWN','providerEvidence':'UNKNOWN_UNVALIDATED_PROVIDER_INPUT'}],['REGISTRATION_MISSING'],set(),set())
        self.assertEqual(gates['A']['status'],'UNKNOWN')
        self.assertEqual(gates['C']['unknownClosureOccurrences'],1)
        self.assertNotEqual(gates['B']['status'],'PASS')

    def test_new_provider_is_not_accepted(self):
        gates=km.gate_counts([{'provider':'OwnedCandidate','providerEvidence':'FRESH_PINNED_DLL_CUSTOM_ATTRIBUTE'}],['REGISTRATION_MISSING'],set(),{'OwnedCandidate'})
        self.assertEqual(gates['A']['acceptedProviderOccurrences'],0)
        self.assertEqual(gates['A']['newValidatedProviderOccurrences'],1)

    def test_registered_unreviewed_is_not_closed(self):
        gates=km.gate_counts([{'provider':'Owned','providerEvidence':'FRESH_PINNED_DLL_CUSTOM_ATTRIBUTE'}],['REGISTRATION_UNREVIEWED'],{'Owned'},set())
        self.assertEqual(gates['B']['status'],'BLOCKED')
        self.assertEqual(gates['C']['unknownClosureOccurrences'],1)

    def test_rejected_profile_cannot_inherit_id_acceptance(self):
        gates=km.gate_counts([{'provider':'Owned','providerEvidence':'FRESH_PINNED_DLL_CUSTOM_ATTRIBUTE'}],['AUTHORED_PROFILE_REJECTED'],{'Owned'},set())
        self.assertEqual(gates['C']['existingProfileClosureOccurrences'],0)
        self.assertNotEqual(gates['B']['status'],'PASS')

    def test_missing_provider_field_fails(self):
        with self.assertRaises(KeyError):km.gate_counts([{}],['EXACT_PROFILE_ACCEPTED'],set(),set())

    def test_omitted_or_unknown_disposition_rejected(self):
        for statuses in ([], ['UNREVIEWED_DEFAULT']):
            with self.subTest(statuses=statuses), self.assertRaises(ValueError):
                km.gate_counts([{'provider':'Owned','providerEvidence':'FRESH_PINNED_DLL_CUSTOM_ATTRIBUTE'}],statuses,{'Owned'},set())

    def test_missing_provider_evidence_cannot_be_ready(self):
        with self.assertRaises(KeyError):
            km.gate_counts([{'provider':'Owned'}],['EXACT_PROFILE_ACCEPTED'],{'Owned'},set())

    def test_wrong_package_and_dll_hash(self):
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'Owned.zip'
            with zipfile.ZipFile(path,'w') as z:z.writestr('Owned.dll',b'owned synthetic bytes')
            pin={'name':'Owned','zipSha256':'0'*64}
            with self.assertRaisesRegex(ValueError,'wrong exact package'):km.validate_zip(path,pin)
            pin.update(zipSha256=km.file_sha(path),distributedDlls=[{'path':'Owned.dll','bytes':21,'sha256':'0'*64}])
            with self.assertRaisesRegex(ValueError,'wrong pinned DLL'):km.validate_zip(path,pin)

    def test_duplicate_and_unsafe_archive_members(self):
        for names in [('Case.txt','case.txt'),('ok.txt','../escape')]:
            with self.subTest(names=names),tempfile.TemporaryDirectory() as tmp:
                path=Path(tmp)/'Owned.zip'
                with zipfile.ZipFile(path,'w') as z:
                    for n in names:z.writestr(n,b'owned')
                with self.assertRaises(ValueError):km.validate_zip(path,{'name':'Owned','zipSha256':km.file_sha(path)})

    def owned_zip(self, missing=False, duplicate=False, wrong_map=False):
        temp=tempfile.TemporaryDirectory();self.addCleanup(temp.cleanup)
        path=Path(temp.name)/'Owned.zip';sids=[km.LOBBY,km.BING,km.GYM,km.HEART,km.RECOMMENDED]
        with zipfile.ZipFile(path,'w') as z:
            for sid in sids:
                if missing and sid==km.RECOMMENDED:continue
                z.writestr('Maps/'+sid+'.bin',sid.encode())
            sticker='    - '+km.BING+'\n    - '+km.RECOMMENDED+'\n'
            if duplicate:sticker+='    - '+km.BING+'\n'
            z.writestr('Maps/'+km.LOBBY+'.meta.yaml',sticker)
        z=zipfile.ZipFile(path);self.addCleanup(z.close)
        tree={'children':[{'name':'SJ2021/StrawberryJamJar','attributes':{'map':sid}} for sid in [km.BING,km.RECOMMENDED]]+
            [{'name':'CollabUtils2/ChapterPanelTrigger','attributes':{'map':sid}} for sid in [km.GYM,km.HEART]]}
        reader=types.SimpleNamespace(read_map=lambda _: {'tree':tree},walk=lambda t:((None,x) for x in t['children']))
        old={sid:{'sha256':km.sha(sid.encode()),'bytes':len(sid.encode())} for sid in sids}
        if wrong_map:old[km.BING]['sha256']='0'*64
        return z,reader,old

    def test_complete_independent_census(self):
        sids,census=km.discover(*self.owned_zip())
        self.assertEqual(len(sids),5);self.assertEqual(census['remainingOrdinary'],1)

    def test_omitted_map_rejected(self):
        with self.assertRaises(ValueError):km.discover(*self.owned_zip(missing=True))

    def test_duplicate_metadata_rejected(self):
        with self.assertRaisesRegex(ValueError,'duplicate sticker'):km.discover(*self.owned_zip(duplicate=True))

    def test_wrong_map_hash_rejected(self):
        with self.assertRaisesRegex(ValueError,'hash/size'):km.discover(*self.owned_zip(wrong_map=True))

    def test_wrong_provider_rejected(self):
        node={'name':'Owned/Entity','attributes':{'id':1,'x':1,'y':2},'children':[]}
        tree={'name':'Map','attributes':{},'children':[{'name':'meta','attributes':{},'children':[]},{'name':'entities','attributes':{},'children':[node]}]}
        z=types.SimpleNamespace(read=lambda _:b'owned-map')
        reader=types.SimpleNamespace(read_map=lambda _: {'tree':tree,'rootBytes':9,'appendixSha256':km.sha(b''),'label':'Owned'})
        with self.assertRaisesRegex(ValueError,'provider authority'):
            km.read_candidate(z,'Owned/Map',reader,{('entity','Owned/Entity'):['Wrong']},set(),{('entity','Owned/Entity'):{'provider':'Right'}},{})

    def test_unprefixed_unknown_is_not_vanilla(self):
        node={'name':'UnprefixedUnknown','attributes':{'id':1,'x':1,'y':2},'children':[]}
        tree={'name':'Map','attributes':{},'children':[{'name':'meta','attributes':{},'children':[]},{'name':'triggers','attributes':{},'children':[node]}]}
        reader=types.SimpleNamespace(read_map=lambda _: {'tree':tree,'rootBytes':9,'appendixSha256':km.sha(b''),'label':'Owned'})
        row=km.read_candidate(types.SimpleNamespace(read=lambda _:b'owned-map'),'Owned/Map',reader,{},set(),{}, {})
        self.assertEqual(row['occurrences'][0]['provider'],'UNKNOWN');self.assertFalse(row['canonicalOccurrences'])

    def test_guid_text_without_embedded_guid_cannot_supply_event(self):
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'Owned.zip';event='event:/Owned/test';guid='{00000001-0002-0003-0004-000000000005}'
            with zipfile.ZipFile(path,'w') as z:
                z.writestr('Audio/Owned.guids.txt',guid+' '+event+'\n');z.writestr('Audio/Owned.bank',b'owned synthetic bank without GUID')
            with zipfile.ZipFile(path) as z:
                _,events=km.asset_index({'Owned':z},{event});self.assertNotIn(event,events)

    def test_unknown_probe_status_or_missing_evidence_rejected(self):
        with self.assertRaises((ValueError,KeyError)):
            km.validate_probe({'candidateInputSha256':'a','productionAssemblySha256':'b','unknownRegistrationRejected':False},[],'a',{'dlls':[{'Sha256':'b'}]})


class LedgerConsistency(unittest.TestCase):
    def owned_result(self):
        sids=[km.LOBBY,km.BING,km.GYM,km.HEART,km.RECOMMENDED]
        rows=[]
        for sid in sids:
            gates=km.gate_counts([{'provider':'Owned','providerEvidence':'FRESH_PINNED_DLL_CUSTOM_ATTRIBUTE'}],['REGISTRATION_MISSING'],{'Owned'},set())
            gates['D']={'status':'ACCEPTED_BASELINE_EXTERNAL_EVIDENCE' if sid in (km.LOBBY,km.BING) else 'BLOCKED'}
            rows.append({'sid':sid,'gates':gates,'unionGates':copy.deepcopy(gates),'candidateCounts':{'customOccurrences':1},'unionCounts':{'customOccurrences':1},'zeroIncrementalReady':False})
        return {'maps':rows,'census':{'ordinarySids':[km.BING,km.RECOMMENDED],'candidates':3,'remainingOrdinary':1},
            'productionProbe':{'baselineOccurrences':920,'baselineFactories':73,'regressionOccurrences':336,'selectedKjRegressionOccurrences':312,'legacyRegressionOccurrences':24,'totalCustomOccurrences':341,'actualStatuses':{'EXACT_PROFILE_ACCEPTED':0,'REGISTRATION_MISSING':341},'unsupportedProfileRejections':0,'unknownRegistrationRejected':True},
            'zeroIncrementalCandidates':[], 'recommendation':{'additionalSids':[km.RECOMMENDED],'gameplayAccepted':False,'combinedGates':copy.deepcopy(rows[-1]['unionGates']),'combinedCustomCounts':{'customOccurrences':1},'combinedComposition':copy.deepcopy(rows[-1]['gates']['D'])}}

    def test_owned_consistency_control(self):km.validate_result(self.owned_result())

    def test_omitted_duplicate_and_missing_gate(self):
        for mutation in ('omission','duplicate','gate'):
            result=self.owned_result()
            if mutation=='omission':result['maps'].pop()
            elif mutation=='duplicate':result['maps'].append(copy.deepcopy(result['maps'][0]))
            else:del result['maps'][-1]['unionGates']['D']
            with self.subTest(mutation=mutation),self.assertRaises((ValueError,KeyError)):km.validate_result(result)

    def test_union_and_recommendation_cannot_promote_unknowns(self):
        for target in ('candidateA','unionB','unionC','unionD','recommendation','counts','regressions','ready'):
            result=self.owned_result()
            if target=='candidateA':result['maps'][-1]['gates']['A']['status']='PASS'
            elif target.startswith('union'):result['maps'][-1]['unionGates'][target[-1]]['status']='PASS'
            elif target=='recommendation':result['recommendation']['combinedGates']={k:{'status':'PASS'} for k in 'ABCD'}
            elif target=='counts':result['recommendation']['combinedCustomCounts']['customOccurrences']=0
            elif target=='regressions':result['productionProbe']['regressionOccurrences']=0
            elif target=='ready':result['zeroIncrementalCandidates']=[km.RECOMMENDED]
            with self.subTest(target=target),self.assertRaises((ValueError,KeyError)):km.validate_result(result)

    def test_missing_candidate_assessment_cannot_use_baseline_default(self):
        decisions={'candidateOrder':[km.RECOMMENDED],'maps':{}}
        with self.assertRaisesRegex(ValueError,'candidate assessment'):
            km.validate_decisions(decisions,[km.LOBBY,km.BING,km.RECOMMENDED])

if __name__=='__main__':unittest.main()
