#!/usr/bin/env python3
"""Audit original SJ Beginner maps with fresh host-only production guard inspection.

Never generates an expanded production closure or edits a map. Full parsed trees,
attributes, map bytes and probe logs stay in a new private output directory. Only
result.json is sanitized. Public inputs and retained production controls are
byte-validated on every invocation; no prior probe result is accepted as input.
"""
from __future__ import annotations
import argparse
from collections import Counter, defaultdict
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile
import uuid

ROOT = Path(__file__).resolve().parents[1]
PREFIX = 'StrawberryJam2021/'
LOBBY = PREFIX + '0-Lobbies/1-Beginner'
BING = PREFIX + '1-Beginner/Bing_Over_Google'
GYM = PREFIX + '0-Gyms/1-Beginner'
HEART = PREFIX + '1-Beginner/ZZ-HeartSide'
RECOMMENDED = PREFIX + '1-Beginner/snas'
KINDS = {'entities': 'entity', 'triggers': 'trigger', 'Backgrounds': 'backdrop', 'Foregrounds': 'backdrop'}
STATUSES = {'EXACT_PROFILE_ACCEPTED', 'AUTHORED_PROFILE_REJECTED', 'REGISTRATION_MISSING', 'REGISTRATION_UNREVIEWED'}

def canonical(value): return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()
def sha(data): return hashlib.sha256(data).hexdigest()
def file_sha(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as f:
        for b in iter(lambda: f.read(1024 * 1024), b''): h.update(b)
    return h.hexdigest()
def load(path): return json.loads(Path(path).read_text())
def save(path, value): Path(path).write_text(json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + '\n')
def require(value, reason):
    if not value: raise ValueError(reason)
def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / 'scripts' / filename)
    result = importlib.util.module_from_spec(spec); sys.modules[name] = result; spec.loader.exec_module(result)
    return result

def validate_zip(path, pin):
    require(file_sha(path) == pin['zipSha256'], 'wrong exact package: ' + pin['name'])
    if 'zipBytes' in pin: require(path.stat().st_size == pin['zipBytes'], 'wrong package size')
    z = zipfile.ZipFile(path)
    entries = [x for x in z.infolist() if not x.is_dir()]
    names = [x.filename for x in entries]
    require(len(names) == len(set(names)), 'duplicate archive member')
    require(len(names) == len({x.casefold() for x in names}), 'case-ambiguous archive member')
    require(all(not n.startswith('/') and '\\' not in n and '..' not in n.split('/') for n in names), 'unsafe archive path')
    for entry in entries:
        require((entry.external_attr >> 16) & 0o170000 != 0o120000, 'archive symlink')
    for dll in pin.get('distributedDlls', []):
        paths = [n for n in names if n == dll.get('archivePath', dll['path']) or
                 ('archivePath' not in dll and n.endswith('/' + dll['path']))]
        require(len(paths) == 1, 'missing or ambiguous pinned DLL')
        raw = z.read(paths[0]); require(len(raw) == dll['bytes'] and sha(raw) == dll['sha256'], 'wrong pinned DLL')
    return z

def discover(z, reader, old_maps):
    all_maps = sorted(n[5:-4] for n in z.namelist() if n.startswith('Maps/') and n.endswith('.bin'))
    require(len(all_maps) == len(set(all_maps)), 'duplicate map SID')
    ordinary_folder = {s for s in all_maps if s.startswith(PREFIX + '1-Beginner/')}
    lobby = reader.read_map(z.read('Maps/' + LOBBY + '.bin'))
    jar_targets = []
    panel_targets = []
    for _, n in reader.walk(lobby['tree']):
        if n['name'] == 'SJ2021/StrawberryJamJar': jar_targets.append(n['attributes']['map'])
        if n['name'] == 'CollabUtils2/ChapterPanelTrigger': panel_targets.append(n['attributes']['map'])
    require(len(jar_targets) == len(set(jar_targets)), 'duplicate ordinary lobby destination')
    sidecar = z.read('Maps/' + LOBBY + '.meta.yaml')
    sticker_targets = re.findall(r'^    - (StrawberryJam2021/1-Beginner/[^\r\n]+)$', sidecar.decode(), re.M)
    require(len(sticker_targets) == len(set(sticker_targets)), 'duplicate sticker destination')
    require(set(sticker_targets) == set(jar_targets), 'omitted or inconsistent collab metadata')
    require(ordinary_folder == set(jar_targets) | {HEART}, 'omitted/extra ordinary or special Beginner map')
    require(set(panel_targets) == {GYM, HEART} and len(panel_targets) == 2, 'unaccounted special/auxiliary destination')
    require(BING in jar_targets and LOBBY in all_maps, 'baseline control missing')
    sids = sorted({LOBBY, GYM, HEART, *jar_targets})
    require(set(all_maps) == set(old_maps), 'complete source map inventory differs from pinned historical census')
    for sid in all_maps:
        raw = z.read('Maps/' + sid + '.bin')
        require(sha(raw) == old_maps[sid]['sha256'] and len(raw) == old_maps[sid]['bytes'], 'source map hash/size mismatch')
    return sids, {'allSjMaps': len(all_maps), 'ordinaryBeginnerIncludingBing': len(jar_targets),
        'remainingOrdinary': len(jar_targets) - 1, 'heartside': 1, 'auxiliaryGym': 1,
        'baselineControls': 2, 'candidates': len(sids) - 2, 'lobbyEntrances': len(jar_targets) + len(panel_targets),
        'stickerMetadataSha256': sha(sidecar), 'ordinarySids': sorted(jar_targets), 'allSjSids': all_maps}

def read_candidate(z, sid, reader, providers, canonical_ids, selected, accepted_profiles):
    member = 'Maps/' + sid + '.bin'; raw = z.read(member); parsed = reader.read_map(raw)
    occurrences, rooms, canonical_rows, other_custom = [], [], [], []
    decals, events, destinations, terrain, meta = [], set(), [], defaultdict(Counter), []
    def walk(n, parent='', room=''):
        a = n['attributes']; name = n['name']; kind = KINDS.get(parent)
        if name == 'level':
            room = a['name']; spawns = [c['attributes'] for g in n['children'] if g['name'] == 'entities' for c in g['children'] if c['name'] == 'player']
            rooms.append({'name': room, 'attributes': a, 'spawns': spawns})
        if name == 'meta': meta.append(n)
        if parent in ('fgdecals', 'bgdecals'): decals.append(a)
        if name in ('solids', 'bg'): terrain[name].update(c for c in a.get('innerText', '') if c not in '0\r\n')
        for key, value in a.items():
            if isinstance(value, str) and value.startswith('event:/'): events.add(value)
            if isinstance(value, str) and value.startswith(PREFIX) and ('map' in key.lower() or 'sid' in key.lower() or 'level' in key.lower()):
                destinations.append({'id': name, 'key': key, 'sid': value})
        if kind:
            key = kind, name
            if key in canonical_ids:
                canonical_rows.append({'kind': kind, 'id': name})
            else:
                owners = providers.get(key, [])
                provider = owners[0] if len(owners) == 1 else 'UNKNOWN'
                if key in selected: require(provider == selected[key]['provider'], 'provider authority mismatch')
                nodes = [c['attributes'] for c in n['children'] if c['name'] == 'node']
                unexpected = [c['name'] for c in n['children'] if c['name'] != 'node']
                profile = {k: v for k, v in a.items() if k not in ('x', 'y', 'id', 'originX', 'originY')}
                relative = [{**v, 'x': v['x'] - a.get('x', 0), 'y': v['y'] - a.get('y', 0)} for v in nodes]
                ph = sha(canonical({'attributes': profile, 'relativeNodes': relative}))
                occurrences.append({'kind': kind, 'customId': name, 'provider': provider, 'map': sid, 'room': room,
                    'attributes': a, 'nodes': nodes, 'unexpectedChildren': unexpected, 'profileSha256': ph,
                    'acceptedSources': sorted(accepted_profiles.get((kind, name, ph), []))})
        elif '/' in name: other_custom.append(name)
        for child in n['children']: walk(child, name, room)
    walk(parsed['tree'])
    require(not other_custom, 'custom elements outside audited consumers; extend audit before declaring complete')
    require(all(not x['unexpectedChildren'] for x in occurrences), 'unsupported authored child profile')
    require(len(meta) == 1, 'missing or multiple metadata roots')
    return {'sid': sid, 'member': member, 'bytes': len(raw), 'sha256': sha(raw), 'rootBytes': parsed['rootBytes'],
        'appendixBytes': len(raw)-parsed['rootBytes'], 'appendixSha256': parsed['appendixSha256'],
        'sourceLabel': parsed['label'], 'tree': parsed['tree'], 'rooms': rooms, 'meta': meta[0],
        'occurrences': occurrences, 'canonicalOccurrences': canonical_rows, 'decals': decals,
        'audioEvents': sorted(events), 'destinations': destinations,
        'terrain': {k: dict(sorted(v.items())) for k, v in sorted(terrain.items())}}

def validate_probe(probe, candidates, input_sha, bindings):
    require(probe['candidateInputSha256'] == input_sha, 'probe does not bind current authored bytes')
    require(probe['productionAssemblySha256'] == bindings['dlls'][0]['Sha256'], 'probe used wrong production assembly')
    require(probe['unknownRegistrationRejected'] is True, 'missing registration negative control absent')
    observations = probe['occurrences']; expected = [o for row in candidates for o in row['occurrences']]
    require(len(observations) == len(expected), 'probe occurrence omission')
    for i, (a, b) in enumerate(zip(observations, expected)):
        require(a['index'] == i and all(a[k] == b[k] for k in ('map','kind','customId','profileSha256')), 'duplicate/rebound probe row')
        require(a['status'] in STATUSES, 'unknown probe disposition')
    require(probe['unsupportedProfileRejections'] == sum(x['status']=='EXACT_PROFILE_ACCEPTED' for x in observations), 'unsupported profile negatives incomplete')
    controls = [x for x in observations if x['map'] in (LOBBY, BING)]
    require(len(controls) == 920 and all(x['status']=='EXACT_PROFILE_ACCEPTED' for x in controls), 'accepted controls regressed')
    inspection = probe['baselineProductionInspection']
    require(len(inspection) == 73, 'compiled registration control census differs')
    require(all(x['Linked'] and x['ActualSelectorInvoked'] and x['ActualProfileGuardInvoked'] and x['UnexpectedProfileRejected'] for x in inspection), 'production linkage/guard control failed')

def asset_index(archives, wanted_events):
    index = defaultdict(list); events = defaultdict(list)
    for owner, z in sorted(archives.items()):
        names=set(z.namelist())
        for n in sorted(names):
            if n.endswith('/'): continue
            if n.startswith('Graphics/'): index[n.casefold()].append((owner,n))
            if n.lower().endswith('.guids.txt'):
                records=[line.split() for line in z.read(n).decode('utf-8-sig').splitlines()]
                wanted=[bits for bits in records if len(bits)==2 and bits[1] in wanted_events]
                bank=n[:-10]+'.bank'
                if not wanted or bank not in names: continue
                raw=z.read(bank);digest=sha(raw)
                for guid,event in wanted:
                    if uuid.UUID(guid).bytes_le in raw:
                        events[event].append({'provider':owner,'guidFile':n,'guid':guid,'bank':bank,
                            'bankPresent':True,'guidEmbeddedInExactBank':True,'bankSha256':digest})
    return index, events

def composition(row, archives, index, event_index, baseline_files):
    z = archives['StrawberryJam2021']; attrs = row['meta']['attributes']; sid = row['sid']
    metadata_files = []; terrain_defs = []; unknowns = set(); blockers = {'SELECTED_MAP_CONTENT_AND_SID_BINDING'}
    for key, value in attrs.items():
        if isinstance(value, str) and value.startswith('Graphics/'):
            matches = index.get(value.casefold(), [])
            if len(matches) != 1: unknowns.add('GRAPHICS_METADATA_RESOLUTION:' + key); continue
            owner, path = matches[0]; raw = archives[owner].read(path)
            metadata_files.append({'field': key, 'provider': owner, 'path': path, 'sha256': sha(raw), 'bytes': len(raw)})
            if key in ('ForegroundTiles', 'BackgroundTiles'):
                xml = ET.fromstring(raw); dimensions = Counter(); unusual = []; missing = []
                definitions = list(xml)
                for tile in definitions:
                    for node in tile:
                        mask = node.attrib.get('mask')
                        if mask and mask not in ('padding','center'):
                            lines = mask.split('-'); dimensions[str(len(lines)) + 'x' + str(len(lines[0]))] += 1
                    texture = 'Graphics/Atlases/Gameplay/tilesets/' + tile.attrib.get('path', '') + '.png'
                    if texture.casefold() not in index: missing.append(texture)
                    if set(tile.attrib)-{'id','path','copy','ignores','scanWidth','scanHeight','sound'}: unusual.append(tile.attrib['id'])
                terrain_defs.append({'field': key, 'definitions': len(definitions), 'maskDimensions': dict(sorted(dimensions.items())),
                    'unreviewedDefinitionCount': len(unusual), 'unresolvedOrCanonicalSheets': sorted(set(missing))})
                if unusual: unknowns.add('TERRAIN_ATTRIBUTES')
                unknowns.add('TERRAIN_ATLAS_COORDINATES_SOUND_AND_DEBRIS_REAL_COMPOSITION')
            else: unknowns.add('GRAPHICS_METADATA_CONSUMER:' + key)
    wipe = attrs.get('Wipe', 'Celeste.MountainWipe')
    supported = {'Celeste.'+x+'Wipe' for x in ['Angled','Curtain','Dream','Drop','Fall','Fade','Heart','KeyDoor','Mountain','Spotlight','Starfield','Wind']}
    if wipe not in supported: blockers.add('UNSUPPORTED_CUSTOM_WIPE')
    sidecar_path = row['member'][:-4] + '.meta.yaml'
    sidecar = None
    if sidecar_path in z.namelist():
        raw = z.read(sidecar_path); sidecar = {'path': sidecar_path, 'sha256': sha(raw), 'bytes': len(raw)}
        if sid != LOBBY: unknowns.add('EXTERNAL_METADATA_CASSETTE_OR_COMPLETE_SCREEN')
    for key in ('CassetteSong','PostcardSoundID','Portraits'):
        if key in attrs: unknowns.add('SPECIAL_METADATA:' + key)
    if attrs.get('Interlude'): blockers.add('INTERLUDE_SEMANTICS')
    actual_rooms = [r for r in row['rooms'] if r['spawns']]
    require(bool(actual_rooms), 'no authored gameplay spawn')
    modes = [x for x in row['meta']['children'] if x['name']=='mode']
    mode = modes[0]['attributes'] if modes else {}
    requested = mode.get('StartLevel')
    room_names = {r['name'] for r in actual_rooms}
    start = next((r for r in actual_rooms if r['name']==requested), actual_rooms[0])
    spawn = next((s for s in start['spawns'] if s.get('isDefaultSpawn') is True), start['spawns'][0])
    stale_start = bool(requested and requested not in room_names)
    # Counts/hashes only in tracked evidence; full positions/attributes stay private.
    start_evidence = {'room': start['name'], 'requestedRoom': requested, 'staleFallbackRequired': stale_start,
        'authoredSpawnCount': len(start['spawns']), 'defaultSpawnCount': sum(s.get('isDefaultSpawn') is True for r in actual_rooms for s in r['spawns']),
        'spawnProfileSha256': sha(canonical(spawn)), 'worldSpawnSha256': sha(canonical([spawn.get('x',0)+start['attributes'].get('x',0),spawn.get('y',0)+start['attributes'].get('y',0)]))}
    cachepath = row['member'][:-4]+'.texturecache.txt'; cache = z.read(cachepath) if cachepath in z.namelist() else b''
    cache_keys = sorted(set(cache.decode('utf-8-sig').splitlines())-{''})
    new_pngs = []; unresolved = []
    for key in cache_keys:
        matches = index.get((key+'.png').casefold(), [])
        if len(matches)==1:
            owner,path=matches[0]
            if (owner,path) not in baseline_files:
                raw=archives[owner].read(path); new_pngs.append({'provider':owner,'path':path,'sha256':sha(raw),'bytes':len(raw)})
        else: unresolved.append(key)
    audio=[]
    for event in row['audioEvents']:
        found=event_index.get(event, [])
        custom=not event.startswith(('event:/music/','event:/game/','event:/env/','event:/new_content/','event:/char/','event:/ui/'))
        if custom and not found: unknowns.add('AUTHORED_AUDIO_EVENT:' + event)
        audio.append({'event':event,'guidEvidence':found,'disposition':'EXACT_PUBLIC_GUID' if found else 'UNKNOWN_CUSTOM_EVENT' if custom else 'CANONICAL_EVENT_REQUIRES_PRODUCT_AUDIO_PROOF'})
    unknowns.update(['IMPLICIT_FACTORY_ASSETS_AND_DECAL_PROPERTIES','EXACT_GRAPHICS_RESTORATION_AND_RENDERED_TERRAIN','PANEL_LAYOUT_CREDITS_AND_INPUT','SESSION_RETURN_RESUME_COMPLETION_JOURNAL_AND_REGRESSION_UNION'])
    if any(d['sid'] not in (LOBBY, BING, sid) and d['sid'].endswith(('/0-Prologue','/1-Beginner')) for d in row['destinations']): unknowns.add('EXCLUDED_DESTINATION_BEHAVIOR')
    return {'status':'BLOCKED', 'blockers':sorted(blockers),'unknowns':sorted(unknowns), 'requirementClassifications':{b:('AUTO_FIX_COMPOSITION' if b=='SELECTED_MAP_CONTENT_AND_SID_BINDING' else 'UNKNOWN') for b in sorted(blockers)}, 'metadataSha256':sha(canonical(row['meta'])),
        'metadataAttributeKeys':sorted(attrs), 'presentation':{k:attrs[k] for k in ('IntroType','CoreMode','Interlude','Icon','CassetteSong','PostcardSoundID') if k in attrs}, 'modePresentation':{k:mode[k] for k in ('Inventory','HeartIsEnd','StartLevel') if k in mode}, 'metadataFiles':metadata_files, 'sidecar':sidecar, 'wipe':wipe,
        'roomCount':len(row['rooms']), 'gameplayRoomCount':len(actual_rooms),'start':start_evidence,
        'terrainSeed':sum(ord(c) for c in sid), 'terrainCells':row['terrain'],'terrainDefinitions':terrain_defs,
        'decalOccurrences':len(row['decals']),'distinctDecalProfiles':len({sha(canonical(x)) for x in row['decals']}),
        'decalProfilesSha256':sha(canonical(row['decals'])), 'textureCacheSha256':sha(cache), 'textureCacheKeys':len(cache_keys),
        'newExplicitCachedPngs':new_pngs,'unresolvedOrCanonicalCachedTextures':unresolved,
        'assetScope':'CACHE_LOWER_BOUND_ONLY_NOT_COMPLETE_CONTENT_CLOSURE','audio':audio,'destinations':row['destinations']}

def fresh_provider_evidence(config, archives, dotnet, output, env, candidates):
    owners=defaultdict(set); census_evidence=[]
    builder=ROOT/'tools/AppleEverestBuilder/bin/Release/net8.0/AppleEverestBuilder.dll'
    for pin in config['acceptedPackages']+[config['chrono']]+config['candidateOnlyPackages']:
        z=archives[pin['name']]
        for i,dll in enumerate(pin['distributedDlls']):
            names=[n for n in z.namelist() if n==dll.get('archivePath',dll['path']) or ('archivePath' not in dll and n.endswith('/'+dll['path']))]
            require(len(names)==1,'ambiguous audit DLL')
            path=output/(pin['name']+'-'+str(i)+'.dll');path.write_bytes(z.read(names[0]))
            result=output/(pin['name']+'-'+str(i)+'.census.json')
            with (output/'provider-census.log').open('a') as log:
                subprocess.run([str(dotnet),'--roll-forward','Major',str(builder),'census-dll','--dll',str(path),'--output',str(result)],env=env,cwd=ROOT,stdout=log,stderr=log,check=True)
            doc=load(result);require(doc['sha256']==dll['sha256'],'fresh DLL census identity differs')
            ids=sorted({x['Id'] for x in doc['customIds']})
            for id in ids:owners[id].add(pin['name'])
            census_evidence.append({'provider':pin['name'],'dllSha256':dll['sha256'],'customIdCount':len(ids),'customIdSetSha256':sha(canonical(ids))})
    proofs={}
    for row in candidates:
        for o in row['occurrences']:
            owner,id=o['provider'],o['customId'];key=o['kind']+':'+id
            if key not in proofs:
                found=owners.get(id,set());literal=[]
                if found=={owner}:status='FRESH_PINNED_DLL_CUSTOM_ATTRIBUTE'
                elif id in config['ownedRegressionProviders']:
                    require(owner==config['ownedRegressionProviders'][id], 'owned regression owner changed')
                    status='OWNED_STATIC_PRODUCER_REGISTRATION'
                elif found:raise ValueError('fresh provider differs from historical owner: '+id)
                elif owner=='EverestCore':status='PINNED_EVEREST_CORE_ALIAS_SOURCE_AUTHORITY'
                elif owner in archives:
                    z=archives[owner]
                    for entry in z.infolist():
                        if entry.filename.startswith('Maps/') or entry.file_size>8*1024*1024:continue
                        if Path(entry.filename).suffix.lower() in {'.lua','.jl','.yaml','.yml','.cs','.json','.xml','.txt'}:
                            raw=z.read(entry)
                            if id.encode() in raw:literal.append({'path':entry.filename,'sha256':sha(raw)})
                    status='EXACT_PINNED_DISTRIBUTED_LITERAL' if literal else 'UNKNOWN_ID_PROVIDER_BINDING'
                else:status='UNKNOWN_UNVALIDATED_PROVIDER_INPUT'
                proofs[key]={'kind':o['kind'],'customId':id,'provider':owner,'status':status,'literalEvidence':literal}
            o['providerEvidence']=proofs[key]['status']
    return {'dlls':census_evidence,'authoredIds':[proofs[k] for k in sorted(proofs)]}

def gate_counts(rows, statuses, accepted_packages, audit_packages):
    require(len(rows)==len(statuses) and all(s in STATUSES for s in statuses), 'incomplete or unknown probe dispositions')
    a=Counter();b=Counter(statuses);c=Counter()
    for o,status in zip(rows,statuses):
        owner=o['provider']
        require(o['providerEvidence'] in {'FRESH_PINNED_DLL_CUSTOM_ATTRIBUTE','PINNED_EVEREST_CORE_ALIAS_SOURCE_AUTHORITY','EXACT_PINNED_DISTRIBUTED_LITERAL','OWNED_STATIC_PRODUCER_REGISTRATION','UNKNOWN_ID_PROVIDER_BINDING','UNKNOWN_UNVALIDATED_PROVIDER_INPUT'}, 'unknown provider evidence disposition')
        a['blockedProviderOccurrences' if owner=='LuaCutscenes' else 'unknownProviderEvidenceOccurrences' if o['providerEvidence'].startswith('UNKNOWN') else 'acceptedProviderOccurrences' if owner in accepted_packages or owner=='EverestCore' else
          'newValidatedProviderOccurrences' if owner in audit_packages else 'unknownProviderEvidenceOccurrences']+=1
        c['existingProfileClosureOccurrences' if status=='EXACT_PROFILE_ACCEPTED' else
          'blockedUnimplementedOccurrences' if status=='REGISTRATION_MISSING' and owner!='UNKNOWN' else 'unknownClosureOccurrences']+=1
    for k in ('acceptedProviderOccurrences','newValidatedProviderOccurrences','blockedProviderOccurrences','unknownProviderEvidenceOccurrences'): a[k]+=0
    for k in STATUSES:b[k]+=0
    for k in ('existingProfileClosureOccurrences','blockedUnimplementedOccurrences','unknownClosureOccurrences'):c[k]+=0
    return {'A':{'status':'BLOCKED' if a['blockedProviderOccurrences'] else 'UNKNOWN' if a['unknownProviderEvidenceOccurrences'] else 'NEW_PROVIDER_REQUIRED' if a['newValidatedProviderOccurrences'] else 'PROVIDER_IDENTITIES_VALIDATED',**dict(sorted(a.items()))},
            'B':{'status':'PASS' if b['EXACT_PROFILE_ACCEPTED']==len(rows) else 'BLOCKED',**dict(sorted(b.items()))},
            'C':{'status':'PASS_FOR_EXISTING_AUTHORED_PROFILES' if c['existingProfileClosureOccurrences']==len(rows) else 'BLOCKED' if c['blockedUnimplementedOccurrences'] else 'UNKNOWN',**dict(sorted(c.items()))}}

def validate_decisions(decisions, sids):
    candidates=set(sids)-{LOBBY,BING}
    ranks=decisions['candidateOrder']
    require(set(ranks)==candidates and len(ranks)==len(set(ranks)), 'decision census omission or duplication')
    require(set(decisions['maps'])==candidates, 'missing or extra candidate assessment')
    for assessment in decisions['maps'].values():
        require(assessment['classification'] in {'EXISTING_CAPABILITY','AUTO_FIX_COMPOSITION','AUTO_IMPLEMENT_BOUNDED_COMPATIBILITY','STOP_MAJOR_ARCHITECTURE','UNKNOWN'} and bool(assessment['evidence']), 'missing or unknown candidate assessment')

def validate_result(result):
    maps=result['maps'];sids=[m['sid'] for m in maps];c=result['census']
    require(len(sids)==len(set(sids)), 'duplicate map ledger')
    require(set(sids)=={LOBBY,GYM,HEART,*c['ordinarySids']}, 'omitted map ledger')
    require(len(maps)==c['candidates']+2 and c['remainingOrdinary']==len(c['ordinarySids'])-1, 'census counts differ')
    for m in maps:
        require(set(m['gates'])=={'A','B','C','D'} and set(m['unionGates'])=={'A','B','C','D'}, 'missing gate')
        for counts_key in ('candidateCounts','unionCounts'):
            counts=m[counts_key]; gates=m['gates'] if counts_key=='candidateCounts' else m['unionGates']
            require(all(isinstance(v,int) and v>=0 for v in counts.values()), 'invalid occurrence counts')
            require(sum(gates['B'][x] for x in STATUSES)==counts['customOccurrences'], 'gate B counts differ')
            require(sum(gates['A'][x] for x in ('acceptedProviderOccurrences','newValidatedProviderOccurrences','blockedProviderOccurrences','unknownProviderEvidenceOccurrences'))==counts['customOccurrences'], 'gate A counts differ')
            require(sum(gates['C'][x] for x in ('existingProfileClosureOccurrences','blockedUnimplementedOccurrences','unknownClosureOccurrences'))==counts['customOccurrences'], 'gate C counts differ')
            a,b,cgate=gates['A'],gates['B'],gates['C']
            require(a['status']==('BLOCKED' if a['blockedProviderOccurrences'] else 'UNKNOWN' if a['unknownProviderEvidenceOccurrences'] else 'NEW_PROVIDER_REQUIRED' if a['newValidatedProviderOccurrences'] else 'PROVIDER_IDENTITIES_VALIDATED'), 'inconsistent A readiness')
            require(b['status']==('PASS' if b['EXACT_PROFILE_ACCEPTED']==counts['customOccurrences'] else 'BLOCKED'), 'inconsistent B readiness')
            require(cgate['status']==('PASS_FOR_EXISTING_AUTHORED_PROFILES' if cgate['existingProfileClosureOccurrences']==counts['customOccurrences'] else 'BLOCKED' if cgate['blockedUnimplementedOccurrences'] else 'UNKNOWN'), 'inconsistent C readiness')
            require(gates['D']['status']==('ACCEPTED_BASELINE_EXTERNAL_EVIDENCE' if m['sid'] in (LOBBY,BING) else 'BLOCKED'), 'unimplemented composition became ready')
        require(m['zeroIncrementalReady'] is False, 'no candidate has complete unchanged readiness evidence')
    probe=result['productionProbe']
    require((probe['baselineOccurrences'],probe['baselineFactories'],probe['regressionOccurrences'],probe['selectedKjRegressionOccurrences'],probe['legacyRegressionOccurrences'])==(920,73,336,312,24), 'production control census differs')
    require(probe['totalCustomOccurrences']==sum(m['candidateCounts']['customOccurrences'] for m in maps)+probe['regressionOccurrences'], 'production total differs')
    require(sum(probe['actualStatuses'].values())==probe['totalCustomOccurrences'] and set(probe['actualStatuses'])<=STATUSES, 'production dispositions differ')
    require(probe['unsupportedProfileRejections']==probe['actualStatuses']['EXACT_PROFILE_ACCEPTED'] and probe['unknownRegistrationRejected'] is True, 'production negatives differ')
    require(result['zeroIncrementalCandidates']==[], 'unproved candidate became ready')
    recommended=next(m for m in maps if m['sid']==RECOMMENDED)
    require(result['recommendation']['combinedGates']==recommended['unionGates'] and result['recommendation']['combinedCustomCounts']==recommended['unionCounts'], 'recommendation union differs')
    require(result['recommendation']['combinedComposition']==recommended['gates']['D'], 'recommendation composition differs')
    require(result['recommendation']['additionalSids']==[RECOMMENDED], 'unreviewed decision')
    require(result['recommendation']['gameplayAccepted'] is False, 'audit is not gameplay acceptance')

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('package-root','candidate-package-root','chrono-package','production-runtime','production-receipt','accepted-closure','dotnet','output'):
        p.add_argument('--'+name,type=Path,required=True)
    args=p.parse_args();out=args.output.resolve()
    require(not out.exists(), 'output must be new; preserve all previous audit attempts')
    if out.is_relative_to(ROOT):
        require(subprocess.run(['git','check-ignore','-q',str(out)],cwd=ROOT).returncode==0, 'in-tree output must be ignored')
    config=load(ROOT/'apple-everest/sj-beginner-expansion-inputs-stage25km.json')
    for path,digest in config['authorityFiles'].items(): require(file_sha(ROOT/path)==digest, 'accepted authority changed: '+path)
    require(file_sha(args.production_receipt)==config['productionEvidence']['receiptSha256'], 'wrong retained production receipt')
    for d in config['productionEvidence']['dlls']:
        f=args.production_runtime/d['Path'];require(file_sha(f)==d['Sha256'] and f.stat().st_size==d['Bytes'], 'wrong production DLL bytes')
    archives={}
    for group,directory in [('acceptedPackages',args.package_root),('candidateOnlyPackages',args.candidate_package_root)]:
        for pin in config[group]: archives[pin['name']]=validate_zip(directory/(pin['name']+'.zip'),pin)
    archives['ChronoHelper']=validate_zip(args.chrono_package,config['chrono'])
    usage=load(ROOT/'apple-everest/strawberry-jam-content-usage-stage25kc.json')
    providers={(x['kind'],x['id']):x['providers'] for x in usage['usage']}
    selected={(x['kind'],x['customId']):x for x in load(ROOT/'apple-everest/selected-factory-type-closure-stage25kh.json')['factories']}
    canonical_ids={(x['kind'],x['id']) for x in config['canonicalIds']}
    accepted_profiles=defaultdict(set)
    for filename in ['sj-factory-authored-profiles-stage25kj.json','sj-legacy-factory-profiles-stage25kj.json','sj-authored-interaction-profiles-stage25kj.json']:
        for o in load(ROOT/'apple-everest'/filename)['occurrences']:accepted_profiles[(o['kind'],o['customId'],o['profileSha256'])].add(filename)
    reader=module('km_content_reader','generate-apple-everest-stage25kl-content.py')
    sids,census=discover(archives['StrawberryJam2021'],reader,{x['sid']:x for x in usage['maps']})
    providers[('entity','ChronoHelper/ExplodingPinata')]=['ChronoHelper']
    # Legacy frozen-IL canaries predate the SJ usage census. Their exact owner
    # is independently checked below against the freshly censused pinned DLL.
    for id in ('DJMapHelper/colorfulFlyFeather','DJMapHelper/featherBarrier'):
        providers[('entity',id)]=['DJMapHelper']
    for id,owner in config['ownedRegressionProviders'].items():providers[('entity',id)]=[owner]
    candidates=[read_candidate(archives['StrawberryJam2021'],sid,reader,providers,canonical_ids,selected,accepted_profiles) for sid in sids]
    out.mkdir(parents=True);(out/'private').mkdir()
    env=dict(os.environ,MSBUILDDISABLENODEREUSE='1',DOTNET_CLI_USE_MSBUILD_SERVER='0',UseSharedCompilation='false',DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
    with (out/'private/host-tool-build.log').open('w') as log:
        subprocess.run([str(args.dotnet),'build',str(ROOT/'tools/AppleEverestBuilder/tests/BeginnerAudit/BeginnerAudit.csproj'),'-c','Release','-m:1','-p:BuildInParallel=false','-p:UseSharedCompilation=false'],cwd=ROOT,env=env,stdout=log,stderr=log,check=True)
    # Parse every owned regression BIN, not only the later KJ selected subset.
    regression_rows=[]
    for path,digest in config['regressionMaps'].items():
        source=args.accepted_closure/path;require(file_sha(source)==digest,'retained regression BIN changed')
        sid=path.removeprefix('content/Content/Maps/')[:-4]
        parsed=reader.read_map(source.read_bytes())
        def collect(n,parent='',room=''):
            a=n['attributes'];kind=KINDS.get(parent)
            if n['name']=='level':room=a['name'].removeprefix('lvl_')
            if kind and (kind,n['name']) not in canonical_ids:
                key=kind,n['name'];owner=providers.get(key,[])
                if key in selected:owner=[selected[key]['provider']]
                require(len(owner)==1,'unresolved owned regression provider: '+str(key))
                nodes=[c['attributes'] for c in n['children'] if c['name']=='node']
                require(all(c['name']=='node' for c in n['children']),'unsupported owned regression child')
                attrs={k:v for k,v in a.items() if k not in ('x','y','id','originX','originY')}
                relative=[{**v,'x':v['x']-a.get('x',0),'y':v['y']-a.get('y',0)} for v in nodes]
                ph=sha(canonical({'attributes':attrs,'relativeNodes':relative}))
                regression_rows.append({'kind':kind,'customId':n['name'],'provider':owner[0],'map':sid,'room':room,'attributes':a,'nodes':nodes,'profileSha256':ph})
            for c in n['children']:collect(c,n['name'],room)
        collect(parsed['tree'])
    require(len(regression_rows)==336 and len({o['map'] for o in regression_rows})==18,'complete legacy regression census changed')
    provider_evidence=fresh_provider_evidence(config,archives,args.dotnet,out/'private',env,candidates+[{'occurrences':regression_rows}])
    for row in candidates:
        key=row['sid'].replace('/','__');save(out/'private'/(key+'.json'),row)
        (out/'private'/(key+'.bin')).write_bytes(archives['StrawberryJam2021'].read(row['member']))
    for path,digest in config['regressionMaps'].items(): require(file_sha(args.accepted_closure/path)==digest, 'retained regression BIN changed')
    regression_path=out/'private/regression-profiles.json'
    subprocess.run([sys.executable,str(ROOT/'scripts/inspect-apple-everest-stage25kj-canary-profiles.py'),'--closure',str(args.accepted_closure),'--output',str(regression_path)],check=True,env=dict(os.environ,PYTHONDONTWRITEBYTECODE='1'))
    kj_rows=load(regression_path)['occurrences']
    require(len(kj_rows)==312,'selected KJ regression census changed')
    signature=lambda o:(o['map'],o['room'],o['kind'],o['customId'],o['profileSha256'])
    require(not (Counter(map(signature,kj_rows))-Counter(map(signature,regression_rows))), 'KJ subset missing from full regression census')
    profiles=out/'private/candidate-profiles.json';save(profiles,{'occurrences':[o for row in candidates for o in row['occurrences']]+regression_rows})
    probe_path=out/'private/compiled-guards.json';env=dict(os.environ,MSBUILDDISABLENODEREUSE='1',DOTNET_CLI_USE_MSBUILD_SERVER='0',UseSharedCompilation='false',DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
    require(subprocess.check_output([str(args.dotnet),'--version'],env=env,text=True,cwd=ROOT).strip()=='10.0.302', 'wrong host SDK')
    command=[str(args.dotnet),'run','--project',str(ROOT/'tools/AppleEverestBuilder/tests/BeginnerAudit/BeginnerAudit.csproj'),'-c','Release','--no-launch-profile','--',str(args.production_runtime/'Celeste.dll'),str(ROOT/'apple-everest/selected-factory-type-closure-stage25kh.json'),str(ROOT/'apple-everest/sj-factory-authored-profiles-stage25kj.json'),str(profiles),str(probe_path)]
    with (out/'private/compiled-guards.log').open('w') as log: subprocess.run(command,cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT,check=True)
    probe=load(probe_path);validate_probe(probe,candidates+[{'occurrences':regression_rows}],file_sha(profiles),config['productionEvidence'])
    require(all(o['status']=='EXACT_PROFILE_ACCEPTED' for o in probe['occurrences'] if o['map'] in {x['map'] for x in kj_rows}), 'selected KJ regression guard rejected')
    observations=defaultdict(list)
    for o in probe['occurrences']: observations[o['map']].append(o)
    combined_trees=[x['tree'] for x in candidates if x['sid'] in (LOBBY,BING,RECOMMENDED)]
    for path in config['regressionMaps']:combined_trees.append(reader.read_map((args.accepted_closure/path).read_bytes())['tree'])
    union_entities=Counter(n['name'] for tree in combined_trees for parent,n in reader.walk(tree) if parent=='entities')
    forbidden_companions=['MaxHelpingHand/MovingFlagTouchSwitch','MaxHelpingHand/MovingTouchSwitch','CommunalHelper/DreamSwitchGate','CommunalHelper/DreamFlagSwitchGate','seeker','puffer']
    require(all(union_entities[n]==0 for n in forbidden_companions), 'proposed finite companion exclusion no longer holds')
    require(union_entities['MaxHelpingHand/FlagTouchSwitch']==1 and union_entities['MaxHelpingHand/FlagSwitchGate']==1, 'combined flag group census differs')
    require(not any(probe['companionSelectors'].values()), 'unexpected compiled companion factory')
    snas=next(x for x in candidates if x['sid']==RECOMMENDED)
    flag_rows=[o for o in snas['occurrences'] if o['customId'] in ('MaxHelpingHand/FlagTouchSwitch','MaxHelpingHand/FlagSwitchGate')]
    require({o['room'] for o in flag_rows}=={'3'} and {o['attributes']['flag'] for o in flag_rows}=={'flag_snasberry_switch'}, 'reviewed snas group changed')
    combined_evidence={'totalMaps':len(combined_trees),'regressionMaps':len(config['regressionMaps']),
        'forbiddenAuthoredCompanionCounts':{n:union_entities[n] for n in forbidden_companions},
        'actualCompiledCompanionSelectors':probe['companionSelectors'],'flagProfilesSha256':sha(canonical(flag_rows)),
        'flagGroup':{'sid':RECOMMENDED,'mode':0,'room':'3','flag':'flag_snasberry_switch','inverted':False,'legacyMode':True,'groupPersistence':True,'switchId':422,'switchPersistent':True,'gateId':423,'gatePersistent':False},
        'scope':'Finite authored/type evidence; future generated closed-creation guards must establish runtime exclusion'}
    wanted_events={event for row in candidates for event in row['audioEvents'] if not event.startswith(('event:/music/','event:/game/','event:/env/','event:/new_content/','event:/char/','event:/ui/'))}
    wanted_events.add('event:/sj21_snas_special')
    index,event_index=asset_index(archives,wanted_events)
    baseline_files={(p['name'],f['path']) for p in load(ROOT/'apple-everest/sj-beginner-content-stage25kl.json')['packages'] for f in p['includedFiles']}
    accepted_packages={p['name'] for p in config['acceptedPackages']}|{'ChronoHelper'};audit_packages=set(archives)-accepted_packages
    baseline_rows=[o for row in candidates if row['sid'] in (LOBBY,BING) for o in row['occurrences']]+regression_rows
    baseline_status=['EXACT_PROFILE_ACCEPTED']*920+[o['status'] for o in probe['occurrences'] if not o['map'].startswith(PREFIX)]
    maps=[];requirements=defaultdict(lambda:{'maps':set(),'profiles':set(),'occurrences':0,'statuses':Counter()})
    for row in candidates:
        sid=row['sid'];obs=observations[sid];status=[o['status'] for o in obs];control=sid in (LOBBY,BING)
        union_rows=baseline_rows if control else baseline_rows+row['occurrences'];union_status=baseline_status if control else baseline_status+status
        gates=gate_counts(row['occurrences'],status,accepted_packages,audit_packages);comp=composition(row,archives,index,event_index,baseline_files)
        if control:comp['status']='ACCEPTED_BASELINE_EXTERNAL_EVIDENCE';comp['blockers']=[];comp['unknowns']=[]
        gates['D']={'status':comp['status'],'blockers':comp['blockers'],'unknowns':comp['unknowns']}
        union_gates=gate_counts(union_rows,union_status,accepted_packages,audit_packages)
        union_gates['D']={**gates['D'],'scope':'Accepted lobby+Bing+candidate+18regressions; new-map composition and cross-map restoration/session interactions remain blocked or unknown'}
        groups=defaultdict(list)
        for o,b in zip(row['occurrences'],obs):groups[(o['kind'],o['customId'],o['provider'])].append((o,b))
        factories=[]
        for (kind,id,provider),entries in sorted(groups.items()):
            counts=Counter(b['status'] for _,b in entries);profiles_set=sorted({o['profileSha256'] for o,_ in entries})
            factories.append({'kind':kind,'customId':id,'provider':provider,'occurrences':len(entries),'distinctProfiles':len(profiles_set),'profileSha256s':profiles_set,'actualProbeStatuses':dict(sorted(counts.items())),
                'exactProfileAuthorityReferences':sorted({s for o,_ in entries for s in o['acceptedSources']}),
                'semanticEvidence':'sj-kj-accepted-semantic-stage25kl.json#'+kind+':'+id if (kind,id) in selected else 'NO_ACCEPTED_SELECTED_SEMANTIC_CLOSURE'})
            if not control and any(b['status']!='EXACT_PROFILE_ACCEPTED' for _,b in entries):
                req=requirements[kind+':'+id];req['maps'].add(sid);req['profiles'].update(profiles_set);req['occurrences']+=len(entries);req['statuses'].update(counts)
        count=lambda rr:{'customOccurrences':len(rr),'distinctCustomIds':len({(o['kind'],o['customId']) for o in rr}),'distinctAuthoredProfiles':len({(o['kind'],o['customId'],o['profileSha256']) for o in rr})}
        maps.append({**{k:row[k] for k in ('sid','member','bytes','sha256','rootBytes','appendixBytes','appendixSha256','sourceLabel')},
            'role':'baseline-control' if control else 'auxiliary-gym' if sid==GYM else 'heartside' if sid==HEART else 'ordinary-candidate',
            'candidateCounts':count(row['occurrences']),'unionCounts':count(union_rows),'canonicalOccurrenceCount':len(row['canonicalOccurrences']),
            'gates':gates,'unionGates':union_gates,'factories':factories,'composition':comp,'zeroIncrementalReady':False,
            'proofReferences':['fresh-host-probe:'+sid,'exact-root-zip:'+row['member'],'sj-beginner-expansion-inputs-stage25km.json','sj-beginner-expansion-decisions-stage25km.json']})
    decisions=load(ROOT/'apple-everest/sj-beginner-expansion-decisions-stage25km.json')
    validate_decisions(decisions,sids);ranks=decisions['candidateOrder']
    for m in maps:
        m['rank']=None if m['role']=='baseline-control' else ranks.index(m['sid'])+1
        m['assessment']=({'classification':'EXISTING_CAPABILITY','scope':'Accepted baseline external evidence; newly rerun host guards only.'}
                         if m['sid'] in (LOBBY,BING) else decisions['maps'][m['sid']])
    clusters=[]
    for key,req in sorted(requirements.items()):
        reviewed=decisions['requirements'].get(key)
        clusters.append({'id':key,'maps':sorted(req['maps']),'mapCount':len(req['maps']),'distinctProfiles':len(req['profiles']),'occurrences':req['occurrences'],
            'actualProbeStatuses':dict(sorted(req['statuses'].items())), 'classification': reviewed['classification'] if reviewed else 'UNKNOWN',
            'evidence':reviewed['evidence'] if reviewed else 'No candidate-specific semantic/lifecycle closure proven; source audit required.'})
    recommended=next(m for m in maps if m['sid']==RECOMMENDED)
    addition=decisions['recommendation']['audioAddition']
    require(file_sha(ROOT/'tools/AppleEverestBuilder/CustomAudioManifest.cs')==config['authorityFiles']['tools/AppleEverestBuilder/CustomAudioManifest.cs'], 'accepted audio producer changed')
    for event in addition['events']:
        evidence=event_index.get(event,[])
        require(len(evidence)==1 and evidence[0]['provider']==addition['provider'] and evidence[0]['bank']==addition['path'] and evidence[0]['bankSha256']==addition['sha256'], 'recommended audio binding incomplete')
    require(sha(archives[addition['provider']].read(addition['guidPath']))==addition['guidSha256'], 'recommended GUID authority differs')
    result={'schemaVersion':1,'stage':'25K-M','status':'AUDIT_GREEN','meaning':'Complete reproducible audit and next decision; no new gameplay readiness',
        'startSha':config['startSha'],'acceptedGameSha':config['acceptedGameSha'],'baselineIdentities':config['baselineIdentities'],
        'inputBindingsSha256':file_sha(ROOT/'apple-everest/sj-beginner-expansion-inputs-stage25km.json'),'decisionBindingsSha256':file_sha(ROOT/'apple-everest/sj-beginner-expansion-decisions-stage25km.json'),
        'census':census,'maps':maps,'sharedRequirements':clusters,'zeroIncrementalCandidates':[],
        'freshProviderEvidence':provider_evidence,
        'productionProbe':{'baselineOccurrences':920,'regressionOccurrences':336,'selectedKjRegressionOccurrences':312,'legacyRegressionOccurrences':24,'baselineFactories':73,'totalCustomOccurrences':len(probe['occurrences']),
            'unsupportedProfileRejections':probe['unsupportedProfileRejections'],'unknownRegistrationRejected':True,
            'actualStatuses':dict(sorted(Counter(o['status'] for o in probe['occurrences']).items())),
            'productionAssemblySha256':probe['productionAssemblySha256'],'compiledGuardSha256s':sorted({x['GuardSha256'] for x in probe['baselineProductionInspection']}),
            'candidateInputSha256':probe['candidateInputSha256'],'scope':probe['scope']},
        'recommendation':{**decisions['recommendation'],'audioEmbeddedGuidEvidence':{k:event_index.get(k,[]) for k in decisions['recommendation']['audioAddition']['events']},'additionalSids':[RECOMMENDED],'selectedSjSids':[LOBBY,BING,RECOMMENDED],
            'combinedEvidence':combined_evidence,'combinedCustomCounts':recommended['unionCounts'],'combinedGates':recommended['unionGates'],'combinedComposition':recommended['gates']['D'],
            'excludedSjMapCount':census['allSjMaps']-3,'availableLobbyDestinations':2,'excludedLobbyDestinations':census['lobbyEntrances']-2,'gameplayAccepted':False},
        'boundaries':{'deviceAot':'NOT_RUN','nativeBuilds':'NOT_RUN','physicalTesting':'NOT_RUN','tuning':'NOT_RUN','signing':'NOT_RUN','actions':'NOT_RUN','ipadPolicy':'IPADOS_PHYSICAL_DEFERRED_LEGACY_COMPATIBILITY_POLICY',
            'regressionContent':'Rehashed 18 owned maps; freshly reparsed all336 custom regression occurrences (312 KJ plus24 legacy) and inspected actual selectors; selected guards rerun, six legacy registrations remain outside the73-profile proof. Included in union counts, not candidate counts. Combined gameplay lifecycle execution remains a later gate.'}}
    validate_result(result);save(out/'result.json',result)
    for z in archives.values():z.close()
    print('PASS: complete original Beginner audit; '+str(census['candidates'])+' candidates; no expanded gameplay acceptance')

if __name__=='__main__':main()
