#!/usr/bin/env python3
"""Project-owned Collab graph for interaction proof, separate from original profiles."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import xml.etree.ElementTree as ET
import zlib

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('canaries',ROOT/'scripts/generate-apple-everest-stage25kj-canaries.py')
c=importlib.util.module_from_spec(spec);spec.loader.exec_module(c)
PREFIX='AppleEverestStage25KJ'
LOBBY=PREFIX+'/0-Lobbies/1-Fixture'
LEVELSET=PREFIX+'/1-Fixture'
FINISHES=[LEVELSET+'/1-FinishA',LEVELSET+'/2-FinishB']

def digest(value):return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()

def png(path,width,height,color,label):
 """Small deterministic test artwork; no third-party image is used."""
 path.parent.mkdir(parents=True,exist_ok=True)
 glyph={'A':['01110','11011','11011','11111','11011','11011','11011'],
        'B':['11110','11011','11011','11110','11011','11011','11110'],
        'M':['11011','11111','11111','11011','11011','11011','11011']}[label]
 scale=max(1,min(width//9,height//11));ox=(width-5*scale)//2;oy=(height-7*scale)//2
 raw=bytearray()
 for y in range(height):
  raw.append(0)
  for x in range(width):
   gx,gy=(x-ox)//scale,(y-oy)//scale
   ink=0<=gx<5 and 0<=gy<7 and glyph[gy][gx]=='1'
   border=min(x,y,width-1-x,height-1-y)<2
   raw.extend((255,245,222,255) if ink or border else (*color,255))
 def chunk(name,value):return struct.pack('>I',len(value))+name+value+struct.pack('>I',zlib.crc32(name+value)&0xffffffff)
 path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',width,height,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(bytes(raw),9))+chunk(b'IEND',b''))

def main():
 package=ROOT/'apple-everest/canaries/stage25kj-interactions'
 package.mkdir(parents=True,exist_ok=True)
 (package/'everest.yaml').write_text('- Name: AppleEverestStage25KJInteractions\n  Version: 1.0.0\n  Dependencies:\n    - Name: CollabUtils2\n      Version: 1.13.4\n    - Name: StrawberryJam2021\n      Version: 1.0.12\n')
 (package/'CollabUtils2CollabID.txt').write_text(PREFIX+'\n')
 authority=json.loads((ROOT/'apple-everest/sj-factory-authored-profiles-stage25kj.json').read_text())
 rows=authority['occurrences'];factories=authority['factories'];ledger=[];trees={}
 def map_tree(sid,width,height):
  tree=ET.Element('Map');levels=ET.SubElement(tree,'levels');ET.SubElement(tree,'Filler')
  style=ET.SubElement(tree,'Style',{'color':'172532'});ET.SubElement(style,'Backgrounds');ET.SubElement(style,'Foregrounds')
  level,entities,triggers=c.room(levels,'canary',width,height)
  ET.SubElement(ET.SubElement(tree,'meta',{'IntroType':'None','Icon':'areas/intro',
   'ForegroundTiles':'Graphics/SJ2021xmls/BeginnerLobby/ForegroundTiles.xml',
   'AnimatedTiles':'Graphics/SJ2021xmls/BeginnerLobby/AnimatedTiles.xml',
   'Sprites':'Graphics/SJ2021xmls/BeginnerLobby/Sprites.xml'}),'mode',{'StartLevel':'canary','Inventory':'Default'})
  trees[sid]=tree
  return level,entities,triggers
 def add(sid,parent,identifier,pos,changes=None,source_id=None):
  factory=next(f for f in factories if f['customId']==identifier)
  source=c.representative(rows,factory) if source_id is None else next(r for r in rows if r['customId']==identifier and r['entityId']==source_id)
  authored=copy.deepcopy(source);authored['attributes'].update(changes or {})
  entity_id=100+sum(r['map']==sid for r in ledger)
  attrs,nodes=c.add_factory(parent,authored,pos,entity_id)
  values={k:v for k,v in attrs.items() if k not in ('id','x','y','originX','originY')}
  relative=[{**n,'x':n['x']-attrs.get('x',0),'y':n['y']-attrs.get('y',0)} for n in nodes]
  sha=digest({'attributes':values,'relativeNodes':relative})
  ledger.append({'kind':factory['kind'],'customId':identifier,'provider':factory['provider'],'map':sid,'room':'canary',
   'entityId':entity_id,'attributes':attrs,'nodes':nodes,'profileSha256':sha,'basisSourceProfileSha256':source['profileSha256'],
   'disposition':'AUTHORED_MECHANISM_PROOF' if sha!=source['profileSha256'] else 'REUSED_EXACT_ORIGINAL_PROFILE',
   'reviewedChanges':changes or {}})
 level,entities,triggers=map_tree(LOBBY,640,256)
 spawn=entities.find('player');spawn.set('x','64')
 ET.SubElement(entities,'player',{'id':'2','x':'304','y':'240'})
 add(LOBBY,entities,'CollabUtils2/LobbyMapController',(8,8),{'mapTexture':PREFIX+'/map','totalMaps':2,'customMarkers':'','mapIcon':'CollabUtils2/lobbies/map'})
 add(LOBBY,entities,'CollabUtils2/LobbyMapMarker',(240,216))
 for i,x in enumerate([64,304]):
  add(LOBBY,entities,'CollabUtils2/LobbyMapWarp',(x,240),{'warpId':str(i+1),'dialogKey':'STAGE25KJ_WARP_'+str(i+1)})
 add(LOBBY,triggers,'CollabUtils2/ChapterPanelTrigger',(96,176),{'map':FINISHES[0]})
 add(LOBBY,triggers,'CollabUtils2/ChapterPanelTrigger',(160,104),{'map':FINISHES[1]},source_id=1582)
 add(LOBBY,triggers,'CollabUtils2/JournalTrigger',(224,180),{'levelset':LEVELSET})
 add(LOBBY,entities,'CollabUtils2/MiniHeartDoor',(384,172),{'levelSet':LEVELSET,'requires':2})
 add(LOBBY,triggers,'CollabUtils2/MiniHeartDoorUnlockCutsceneTrigger',(288,72))
 add(LOBBY,entities,'CollabUtils2/RainbowBerry',(528,216),{'levelSet':LEVELSET})
 add(LOBBY,triggers,'CollabUtils2/RainbowBerryUnlockCutsceneTrigger',(440,0))
 for sid in FINISHES:
  level,entities,triggers=map_tree(sid,320,184)
  add(sid,entities,'CollabUtils2/SilverBerry',(64,144))
  add(sid,entities,'CollabUtils2/MiniHeart',(256,128))
  if sid==FINISHES[1]:
   # A grounded red berry gives Save/Continue a visible session-state proof.
   ET.SubElement(entities,'strawberry',{'id':'900','x':'144','y':'152'})
 for sid,tree in trees.items():
  path=package/'Maps'/(sid+'.xml');path.parent.mkdir(parents=True,exist_ok=True)
  ET.indent(tree,space='  ');ET.ElementTree(tree).write(path,encoding='utf-8',xml_declaration=True)
 png(package/'Graphics/Atlases/Gui'/PREFIX/'map.png',160,64,(46,92,112),'M')
 stickers=[]
 for i,label in enumerate(['A','B']):
  logical=PREFIX+'/finish'+label
  path=package/'Graphics/Atlases/Stickers'/(logical+'.png')
  png(path,96,96,(176,54,91) if i==0 else (44,132,126),label)
  stickers.append({'Path':logical,'X':float(450+i*500),'Y':350.0,'Rotation':-10.0 if i==0 else 12.0,'Scale':2.0,
   'FinishedMaps':[FINISHES[i]],'assetSha256':hashlib.sha256(path.read_bytes()).hexdigest()})
 # This authored missing target must stay hidden. It creates no fake Area.
 stickers.append({**stickers[0],'X':800.0,'Y':700.0,'FinishedMaps':[PREFIX+'/1-Fixture/Absent']})
 metadata=package/'Maps'/(LOBBY+'.meta.yaml')
 text=['Stickers:']
 for item in stickers:
  text.extend(['  - Path: '+item['Path']]+['    '+k+': '+str(item[k]) for k in ['X','Y','Rotation','Scale']]+['    FinishedMaps:']+['      - '+sid for sid in item['FinishedMaps']])
 metadata.write_text('\n'.join(text)+'\n')
 dialog=package/'Dialog/English.txt';dialog.parent.mkdir(parents=True,exist_ok=True)
 lines=['modname_'+PREFIX+'=Stage 25K-J Interaction Fixture','STAGE25KJ_WARP_1=WEST BENCH','STAGE25KJ_WARP_2=EAST BENCH']
 for sid,label in [(LOBBY,'FACTORY LOBBY'),(FINISHES[0],'FINISH A'),(FINISHES[1],'FINISH B')]:
  key=sid.replace('/','_').replace('-','_');lines.extend([key+'='+label,key+'_author=Project-owned canary'])
 dialog.write_text('\n'.join(lines)+'\n')
 changed=[r for r in ledger if r['disposition']=='AUTHORED_MECHANISM_PROOF']
 if len(changed)!=8 or len({r['customId'] for r in changed})!=6:raise ValueError('authored profile review set changed')
 (ROOT/'apple-everest/sj-authored-interaction-profiles-stage25kj.json').write_text(json.dumps({'schemaVersion':1,
  'authority':'PROJECT_OWNED_AUTHORED_MECHANISM_FIXTURE_SEPARATE_FROM_ORIGINAL_920',
  'originalGameplayMapsCopied':False,'authoredAdditionalProfiles':8,'occurrences':ledger,
  'stickers':stickers,'stickerMetadataSha256':hashlib.sha256(metadata.read_bytes()).hexdigest()},indent=2)+'\n')
 code=['// Generated from project-owned Stage25KJ fixture metadata.','namespace Celeste.Mod;',
  'internal static class AppleEverestFactoryCanaryStickers','{','    internal const string LobbySid = '+json.dumps(LOBBY)+';',
  '    internal static readonly AppleEverestCollabSticker[] Entries = new AppleEverestCollabSticker[]','    {']
 for item in stickers:
  code.append('        new('+', '.join([json.dumps(item['Path'])]+[str(item[k])+'f' for k in ['X','Y','Rotation','Scale']])+', new[] { '+', '.join(json.dumps(x) for x in item['FinishedMaps'])+' }),')
 code.extend(['    };','}'])
 (ROOT/'apple-everest/runtime/semantics/AppleEverestFactoryCanaryStickers.cs').write_text('\n'.join(code)+'\n')
 print('AUTHORED: complete three-map Collab interaction graph; eight separate bounded profiles; runtime validation pending')

if __name__=='__main__':main()
