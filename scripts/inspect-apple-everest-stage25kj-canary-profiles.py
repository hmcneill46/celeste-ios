#!/usr/bin/env python3
"""Read compiled project-owned canary BINs for actual guard verification on host."""
import argparse
import importlib.util
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('profiles',ROOT/'scripts/generate-apple-everest-stage25kj-profiles.py')
p=importlib.util.module_from_spec(spec);sys.modules[spec.name]=p;spec.loader.exec_module(p)

def main():
 a=argparse.ArgumentParser(description=__doc__);a.add_argument('--closure',required=True,type=Path);a.add_argument('--output',required=True,type=Path);args=a.parse_args()
 authority=json.loads((ROOT/'apple-everest/sj-factory-authored-profiles-stage25kj.json').read_text())
 interactions=json.loads((ROOT/'apple-everest/sj-authored-interaction-profiles-stage25kj.json').read_text())
 known={(f['kind'],f['customId']):f['provider'] for f in authority['factories']}
 rows=[]
 def walk(e,sid,parent='',room=''):
  if e['name']=='level':room=e['attributes']['name'].removeprefix('lvl_')
  kind={'entities':'entity','triggers':'trigger','Backgrounds':'backdrop','Foregrounds':'backdrop'}.get(parent)
  if (kind,e['name']) in known:
   attrs=e['attributes'];nodes=[n['attributes'] for n in e['children'] if n['name']=='node']
   values={k:v for k,v in attrs.items() if k not in ('id','x','y','originX','originY')}
   rel=[{**n,'x':n['x']-attrs.get('x',0),'y':n['y']-attrs.get('y',0)} for n in nodes]
   rows.append({'kind':kind,'customId':e['name'],'provider':known[(kind,e['name'])], 'map':sid,'room':room,
    'entityId':attrs.get('id'),'attributes':attrs,'nodes':nodes,'profileSha256':p.sha(p.canonical({'attributes':values,'relativeNodes':rel}))})
  for c in e['children']:walk(c,sid,e['name'],room)
 root=args.closure/'content/Content/Maps/AppleEverest'
 for path in sorted(root.glob('Stage25KJ*.bin')):
  tree,_=p.parse(path.read_bytes());walk(tree,'AppleEverest/'+path.stem)
 for path in sorted((args.closure/'content/Content/Maps/AppleEverestStage25KJ/FactoryProfiles').glob('*.bin')):
  tree,_=p.parse(path.read_bytes());walk(tree,'AppleEverestStage25KJ/FactoryProfiles/'+path.stem)
 interaction_sids={r['map'] for r in interactions['occurrences']}
 for sid in sorted(interaction_sids):
  path=args.closure/'content/Content/Maps'/(sid+'.bin')
  if not path.is_file():raise ValueError('authored interaction graph is incomplete: '+sid)
  tree,_=p.parse(path.read_bytes());walk(tree,sid)
 selected={(r['kind'],r['customId']) for r in rows}
 if selected!=set(known):raise ValueError('actual compiled canary census differs: '+str(set(known)-selected))
 original_profiles={(r['kind'],r['customId'],r['profileSha256']) for r in authority['occurrences']}
 authored_profiles={(r['kind'],r['customId'],r['profileSha256']) for r in interactions['occurrences']}
 invalid=[r for r in rows if (r['kind'],r['customId'],r['profileSha256']) not in original_profiles and
   not (r['map'] in interaction_sids and (r['kind'],r['customId'],r['profileSha256']) in authored_profiles)]
 if invalid:raise ValueError('compiled canary authored profile differs: '+repr([(r['customId'],r['profileSha256']) for r in invalid]))
 plan=json.loads((ROOT/'apple-everest/sj-factory-canary-plan-stage25kj.json').read_text())
 for expected in plan['representatives']:
  matches=[r for r in rows if r['kind']==expected['kind'] and r['customId']==expected['customId'] and r['map']==expected['canaryMap'] and
    (r['kind']=='backdrop' or (r['room']==expected['canaryRoom'] and r['entityId']==expected['canaryEntityId']))]
  if len(matches)!=1 or matches[0]['profileSha256']!=expected['sourceProfileSha256']:
   raise ValueError('compiled canary does not bind planned representative: '+expected['customId'])
 for expected in interactions['occurrences']:
  matches=[r for r in rows if all(r[key]==expected[key] for key in ['map','room','kind','customId','entityId'])]
  if len(matches)!=1 or matches[0]['profileSha256']!=expected['profileSha256']:
   raise ValueError('compiled authored interaction does not bind reviewed fixture: '+expected['customId'])
 if sum(r['map'] in interaction_sids for r in rows)!=len(interactions['occurrences']):
  raise ValueError('compiled authored interaction occurrence census differs')
 p.save(args.output,{'schemaVersion':1,'actualCompiledCanaryBins':True,'originalGameplayMapsCopied':False,
  'plannedRepresentativesBoundToCompiledProfiles':len(plan['representatives']),
  'canaryPlanSha256':p.sha((ROOT/'apple-everest/sj-factory-canary-plan-stage25kj.json').read_bytes()),
  'authoredInteractionOccurrencesBound':len(interactions['occurrences']),
  'authoredAdditionalProfileOccurrences':sum((r['kind'],r['customId'],r['profileSha256']) not in original_profiles for r in rows),
  'census':{'selectedFactories':len(selected),'selectedOccurrences':len(rows)},'occurrences':rows})
 print('PASS:',len(plan['representatives']),'exact original representatives and',len(interactions['occurrences']),
  'reviewed interaction occurrences bound to compiled profiles;',len(rows),'total canary occurrences')
if __name__=='__main__':main()
