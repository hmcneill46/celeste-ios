#!/usr/bin/env python3
"""Generate project-owned rooms from representative exact factory profiles.

No original gameplay layout, tile/decal layer or BIN is copied. Every selected
factory retains one exact authored attribute/node shape; only placements/IDs
are translated. Public helper assets remain separately acquired build inputs.
"""
import argparse
import copy
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
AE=ROOT/'apple-everest'
# Reviewed source occurrences chosen for useful, bounded visible behavior.
CHOICES={
 'CherryHelper/AssistRect':(246,'Bing',''), 'CherryHelper/ItemCrystal':(907,'0-Lobbies',''),
 'CherryHelper/ItemCrystalPedestal':(920,'0-Lobbies',''), 'FancyTileEntities/FancySolidTiles':(832,'0-Lobbies',''),
 'BrokemiaHelper/caveWall':(315,'0-Lobbies',''), 'HonlyHelper/PettableCat':(56,'0-Lobbies',''),
 'pandorasBox/coloredWater':(1359,'0-Lobbies',''), 'pandorasBox/coloredWaterfall':(111,'0-Lobbies',''),
 'pandorasBox/coloredBigWaterfall':(489,'0-Lobbies',''), 'VivHelper/CustomHangingLamp':(1053,'0-Lobbies',''),
 'VivHelper/CustomSpinner':(470,'0-Lobbies',''), 'HonlyHelper/CameraTargetCrossfadeTrigger':(1387,'0-Lobbies',''),
 'XaphanHelper/Slope':(1412,'0-Lobbies',''), 'pandorasBox/entityActivator':(197,'0-Lobbies',''),
 'MaxHelpingHand/FlagExitBlock':(953,'0-Lobbies',''), 'MaxHelpingHand/GroupedTriggerSpikesUp':(455,'Bing','05'),
 'MaxHelpingHand/SidewaysJumpThru':(45,'0-Lobbies',''), 'MaxHelpingHand/ColorGradeFadeTrigger':(82,'0-Lobbies',''),
 'MaxHelpingHand/CameraCatchupSpeedTrigger':(673,'0-Lobbies',''), 'MaxHelpingHand/CameraOffsetBorder':(296,'0-Lobbies',''),
 'MaxHelpingHand/FlagToggleCameraOffsetTrigger':(1264,'0-Lobbies',''),
 'MaxHelpingHand/FlagToggleCameraTargetTrigger':(14,'Bing','07B'),
 'MaxHelpingHand/SetFlagOnSpawnController':(11,'Bing','07B'),
 'MaxHelpingHand/FlagToggleSmoothCameraOffsetTrigger':(9,'Bing','02B'),
 'MaxHelpingHand/CustomTutorialWithNoBird':(893,'0-Lobbies',''), 'MaxHelpingHand/MoreCustomNPC':(1604,'0-Lobbies',''),
 'MaxHelpingHand/ParallaxFadeOutController':(194,'0-Lobbies',''), 'MaxHelpingHand/RainbowSpinnerColorAreaController':(294,'0-Lobbies',''),
 'MaxHelpingHand/StylegroundFadeController':(29,'0-Lobbies',''),
 'CollabUtils2/LobbyMapController':(103,'0-Lobbies',''), 'CollabUtils2/LobbyMapMarker':(1403,'0-Lobbies',''),
 'CollabUtils2/LobbyMapWarp':(623,'0-Lobbies',''), 'CollabUtils2/MiniHeart':(20,'Bing','09- Fin'),
 'CollabUtils2/MiniHeartDoor':(657,'0-Lobbies',''), 'CollabUtils2/MiniHeartDoorUnlockCutsceneTrigger':(1235,'0-Lobbies',''),
 'CollabUtils2/RainbowBerry':(137,'0-Lobbies',''), 'CollabUtils2/RainbowBerryUnlockCutsceneTrigger':(1234,'0-Lobbies',''),
 'CollabUtils2/SilverBerry':(581,'Bing','00- intro'), 'CollabUtils2/ChapterPanelTrigger':(1210,'0-Lobbies',''),
 'CollabUtils2/JournalTrigger':(1198,'0-Lobbies',''), 'LunaticHelper/InvisibleLightSource':(600,'0-Lobbies',''),
 'LunaticHelper/StrawberryWithReturn':(1459,'Bing','07B'),
}
HASHES={'FrostHelper/CustomFireBarrier':'4eb33c64b4','FrostHelper/CustomFlutterBird':'80227d79f7',
 'FrostHelper/DecalContainer':'8bb62efad2','FrostHelper/EntityMover':'6cdabc3461','FrostHelper/IceSpinner':'4edf04304d',
 'FrostHelper/WireLamps':'df33876513','FrostHelper/OnSpawnActivator':'4889cb65e0',
 'FemtoHelper/CustomParallaxBigWaterfall':'cb637d414a','FemtoHelper/ParticleEmitter':'ff709136a2',
 'FemtoHelper/WindPetals':'60dd21913b','LunaticHelper/CustomDust':'71281f'}
GROUPS={
 'CrystalCave':['BrokemiaHelper','CherryHelper','FancyTileEntities'],
 'WaterGarden':['PandorasBox','VivHelper'], 'Collab':['CollabUtils2'],
 'MaxMechanics':['MaxHelpingHand','LunaticHelper'], 'Frost':['FrostHelper'],
 'Atmosphere':['FemtoHelper','FlaglinesAndSuch'], 'CameraCorridor':['HonlyHelper','XaphanHelper'],
 'BaseHelpers':['JungleHelper','VortexHelper','YetAnotherHelper','ContortHelper','DJMapHelper','ExtendedVariantMode','EverestCore','CrystallineHelper'],
 'Masks':['StrawberryJam2021']}
SHARED_ROOMS={'CrystalCave':(640,288),'WaterGarden':(480,256),'CameraCorridor':(1280,480)}
PLACEMENTS={
 'BrokemiaHelper/caveWall':(280,48), 'CherryHelper/ItemCrystal':(104,272),
 'CherryHelper/ItemCrystalPedestal':(520,264), 'HonlyHelper/PettableCat':(400,256),
 'CherryHelper/AssistRect':(176,224), 'FancyTileEntities/FancySolidTiles':(184,264),
 'pandorasBox/coloredBigWaterfall':(64,80), 'pandorasBox/coloredWater':(144,208),
 'pandorasBox/coloredWaterfall':(160,128), 'VivHelper/CustomHangingLamp':(232,200),
 'VivHelper/CustomSpinner':(280,224), 'XaphanHelper/Slope':(128,448),
 'HonlyHelper/CameraTargetCrossfadeTrigger':(552,240), 'pandorasBox/entityActivator':(8,16),
 'FrostHelper/EntityMover':(968,-864), 'FrostHelper/WireLamps':(64,144),
 'FrostHelper/CustomFlutterBird':(160,239),
 'FrostHelper/OnSpawnActivator':(8,16), 'vitellary/triggertrigger':(160,220)}

def group_for(factory):
 if factory['customId']=='HonlyHelper/PettableCat':return 'CrystalCave'
 if factory['customId']=='pandorasBox/entityActivator':return 'CameraCorridor'
 return next(group for group,providers in GROUPS.items() if factory['provider'] in providers)

def fmt(v):
 if isinstance(v,bool):return str(v).lower()
 return str(v)

def representative(rows, factory):
 matches=[r for r in rows if r['kind']==factory['kind'] and r['customId']==factory['customId']]
 if factory['customId'] in CHOICES:
  entity, sid, room=CHOICES[factory['customId']]
  matches=[r for r in matches if r['entityId']==entity and sid in r['map'] and r['room'].startswith(room)]
 elif factory['customId'] in HASHES:
  matches=[r for r in matches if r['profileSha256'].startswith(HASHES[factory['customId']])]
 if not matches:raise ValueError('missing representative '+factory['customId'])
 return min(matches,key=lambda r:(r['attributes'].get('width',0)*r['attributes'].get('height',0),len(r['nodes']),r['profileSha256'],r['map'],r['room']))

def add_factory(parent,row,pos,entity_id):
 a=copy.deepcopy(row['attributes']);nodes=copy.deepcopy(row['nodes'])
 if row['kind']!='backdrop':
  dx,dy=pos[0]-a.get('x',0),pos[1]-a.get('y',0)
  a.update(id=entity_id,x=pos[0],y=pos[1])
  for node in nodes:node['x']+=dx;node['y']+=dy
 tag={'entity':'appleEverestEntity','trigger':'appleEverestTrigger','backdrop':'appleEverestBackdrop'}[row['kind']]
 # Explicit string metadata avoids silently converting authored "446666" or
 # "false" strings into integer/bool values in the XML convenience format.
 attrs={'appleEverestId':row['customId']}
 string_keys=sorted(k for k,v in a.items() if isinstance(v,str))
 if string_keys:attrs['appleEverestStrings']=','.join(string_keys)
 attrs.update((k,fmt(v)) for k,v in sorted(a.items()))
 element=ET.SubElement(parent,tag,attrs)
 for node in nodes:ET.SubElement(element,'node',{k:fmt(v) for k,v in sorted(node.items())})
 return a,nodes

def room(root,name,width=640,height=256,x=0,y=0):
 level=ET.SubElement(root,'level',{'name':'lvl_'+name,'x':str(x),'y':str(y),'width':str(width),'height':str(height),
   'music':'event:/music/lvl1/main','ambience':'event:/env/amb/01_main','windPattern':'None','dark':'false'})
 entities=ET.SubElement(level,'entities');ET.SubElement(entities,'player',{'id':'1','x':'24','y':str(height-16)})
 triggers=ET.SubElement(level,'triggers');ET.SubElement(level,'bgdecals');ET.SubElement(level,'fgdecals')
 grid=[list('0'*(width//8)) for _ in range(height//8)]
 for line in grid[-2:]:line[:]=['9']*len(line)
 ET.SubElement(level,'solids').text='\n'.join(''.join(line) for line in grid)
 for tag in ['bg','fgtiles','bgtiles','objtiles']:ET.SubElement(level,tag)
 return level,entities,triggers

def tiles(level,x,y,width,height,tile='9'):
 element=level.find('solids');grid=[list(line) for line in element.text.splitlines()]
 for row_index in range(y//8,(y+height+7)//8):
  for column in range(x//8,(x+width+7)//8):
   if 0<=row_index<len(grid) and 0<=column<len(grid[row_index]):grid[row_index][column]=tile
 element.text='\n'.join(''.join(line) for line in grid)

def companions(group,level,entities,triggers,rows,primary):
 additions=[]
 def exact(custom_id,pos,profile_prefix=None):
  factory=next(f for f in primary if f['customId']==custom_id)
  source=representative(rows,factory) if profile_prefix is None else next(r for r in rows if r['customId']==custom_id and r['profileSha256'].startswith(profile_prefix))
  entity_id=2000+len(additions)
  parent=triggers if source['kind']=='trigger' else entities
  attrs,nodes=add_factory(parent,source,pos,entity_id)
  additions.append({'customId':custom_id,'sourceProfileSha256':source['profileSha256'],'canaryEntityId':entity_id,'attributes':attrs,'nodes':nodes})
 def vanilla(name,**attrs):ET.SubElement(entities,name,{'id':str(3000+len(entities)),**{k:fmt(v) for k,v in attrs.items()}})
 if group=='CrystalCave':
  tiles(level,176,264,8,8,'E');tiles(level,200,264,8,8,'E');tiles(level,176,272,32,8,'E')
 elif group=='WaterGarden':
  exact('VivHelper/CustomSpinner',(300,224));tiles(level,216,192,40,8)
 elif group=='CameraCorridor':
  entities.find('player').set('y','448')
  tiles(level,0,448,128,16)
  for x,y,w in [(360,400,48),(416,352,48),(472,304,48),(520,240,32),(624,320,72)]:tiles(level,x,y,w,8)
  vanilla('spring',x=64,y=448);vanilla('spring',x=1120,y=464)
 return additions

def isolated_companions(custom_id,level,entities,triggers,rows,primary):
 additions=[]
 def exact(identifier,pos,prefix=None):
  f=next(f for f in primary if f['customId']==identifier)
  source=representative(rows,f) if prefix is None else next(r for r in rows if r['customId']==identifier and r['profileSha256'].startswith(prefix))
  entity_id=2000+len(additions);a,n=add_factory(triggers if source['kind']=='trigger' else entities,source,pos,entity_id)
  additions.append({'customId':identifier,'sourceProfileSha256':source['profileSha256'],'canaryEntityId':entity_id,'attributes':a,'nodes':n})
 h=int(level.get('height'));floor=h-16
 if custom_id in ['MaxHelpingHand/FlagToggleCameraOffsetTrigger','MaxHelpingHand/FlagToggleCameraTargetTrigger','MaxHelpingHand/CameraOffsetBorder']:
  floor-=80
  tiles(level,0,floor,int(level.get('width')),8)
  entities.find('player').set('y',str(floor))
 # These exact original flag profiles are also read by the desktop reference.
 # Walking from the spawn to x352 disables the visible effects; returning to
 # the spawn enables them again. No runtime-only flag setup is required.
 if custom_id.startswith(('FemtoHelper/','FlaglinesAndSuch/','MaxHelpingHand/','LunaticHelper/')):
  for enabled,disabled in [('455eb16d6bb1','370343140de4'),('e1fd0cfd3b2e','446741014216'),
                           ('7f52d5753fe8','6a304564756f'),('baf79299bd62','be0870e57f0c')]:
   for prefix,x in [(enabled,0),(disabled,352)]:
    source=next(r for r in rows if r['customId']=='everest/flagTrigger' and r['profileSha256'].startswith(prefix))
    exact('everest/flagTrigger',(x,floor-source['attributes']['height']),prefix)
 if custom_id.startswith(('FemtoHelper/','FlaglinesAndSuch/')):
  exact('SJ2021/StylegroundMask',(56,32),'896828f37e5d')
  if custom_id=='FemtoHelper/CustomParallaxBigWaterfall':
   exact(custom_id,(232,floor-146),'16feb374172e4f')
  if custom_id=='FemtoHelper/WindPetals':
   for index,(x,pattern) in enumerate([(112,'Right'),(272,'None')]):
    ET.SubElement(triggers,'windTrigger',{'id':str(3100+index),'x':str(x),'y':str(floor-64),'width':'32','height':'64','pattern':pattern})
 if custom_id=='FrostHelper/IceSpinner':exact(custom_id,(180,floor-16))
 if custom_id=='FrostHelper/DecalContainer':
  ET.SubElement(level.find('fgdecals'),'decal',{'texture':'JungleHelper/Jungle0-general/PlantGrassShort.png','x':'160','y':str(floor-8),'scaleX':'1','scaleY':'1'})
 if custom_id=='FrostHelper/EntityMover':
  exact('MaxHelpingHand/CustomTutorialWithNoBird',(88,120),'ef1c49ef93');tiles(level,80,120,24,8)
 if custom_id=='FrostHelper/OnSpawnActivator':exact('ExtendedVariantMode/ResetVariantsTrigger',(32,16),'48d4c31f9d')
 if custom_id=='MaxHelpingHand/CustomTutorialWithNoBird':
  mover=ET.SubElement(entities,'zipMover',{'id':'3200','x':'152','y':str(floor-32),'width':'24','height':'16'})
  ET.SubElement(mover,'node',{'x':'232','y':str(floor-32)})
 if custom_id=='MaxHelpingHand/MoreCustomNPC':tiles(level,128,floor-24,64,8)
 if custom_id=='MaxHelpingHand/FlagExitBlock':
  exact('everest/flagTrigger',(120,floor-8),'6f2c95a70014900b')
  exact('everest/flagTrigger',(192,floor-40),'e713ea4682d31ee0')
 if custom_id=='MaxHelpingHand/FlagToggleCameraTargetTrigger':
  exact('MaxHelpingHand/SetFlagOnSpawnController',(8,8),'d332bd3515a0f943')
  exact('everest/flagTrigger',(304,floor-16),'c23831cccbfcf658')
 if custom_id=='MaxHelpingHand/FlagToggleSmoothCameraOffsetTrigger':
  exact('MaxHelpingHand/SetFlagOnSpawnController',(8,8),'832d96a12ffe4d88')
  exact('everest/flagTrigger',(88,floor-80),'0c721ffaf7e86c2e')
 if custom_id=='MaxHelpingHand/FlagToggleCameraOffsetTrigger':
  exact('everest/flagTrigger',(112,floor-128),'70293f477267eb45')
  exact('everest/flagTrigger',(208,floor-144),'604f0f39f0e0dfd0')
 if custom_id=='MaxHelpingHand/GroupedTriggerSpikesUp':tiles(level,160,floor-8,40,8)
 if custom_id=='VortexHelper/AttachedJumpThru':tiles(level,152,floor-40,8,32)
 if custom_id=='MaxHelpingHand/SidewaysJumpThru':
  ET.SubElement(entities,'flyFeather',{'id':'3200','x':'96','y':str(floor-32)})
  source=next(r for r in rows if r['customId']==custom_id and r['entityId']==179 and '0-Lobbies' in r['map'])
  entity_id=2000+len(additions)
  a,n=add_factory(entities,source,(224,floor-source['attributes']['height']),entity_id)
  additions.append({'customId':custom_id,'sourceProfileSha256':source['profileSha256'],'canaryEntityId':entity_id,'attributes':a,'nodes':n})
 if custom_id=='MaxHelpingHand/RainbowSpinnerColorAreaController':
  ET.SubElement(entities,'spinner',{'id':'3001','x':'184','y':str(floor-24),'color':'Rainbow'})
  ET.SubElement(entities,'spinner',{'id':'3002','x':'280','y':str(floor-24),'color':'Rainbow'})
 if custom_id=='LunaticHelper/InvisibleLightSource':
  level.set('dark','true')
  tiles(level,192,floor-64,16,64)
 if custom_id=='CollabUtils2/MiniHeartDoorUnlockCutsceneTrigger':exact('CollabUtils2/MiniHeartDoor',(424,floor-68))
 if custom_id in ['CollabUtils2/LobbyMapController','CollabUtils2/LobbyMapMarker','CollabUtils2/LobbyMapWarp']:
  for identifier,pos in [('CollabUtils2/LobbyMapController',(8,8)),('CollabUtils2/LobbyMapMarker',(264,floor-24)),('CollabUtils2/LobbyMapWarp',(160,floor))]:
   if identifier!=custom_id:exact(identifier,pos)
 if custom_id=='CollabUtils2/RainbowBerryUnlockCutsceneTrigger':exact('CollabUtils2/RainbowBerry',(464,floor-24))
 if custom_id=='vitellary/triggertrigger':
  exact('everest/flagTrigger',(144,321))
  exact('CherryHelper/ItemCrystal',(104,220))
  ET.SubElement(triggers,'rumbleTrigger',{'id':'3001','x':'184','y':'321','width':'8','height':'8','manualTrigger':'true'})
  tiles(level,80,232,160,8)
 if custom_id=='vitellary/editdepthtrigger':exact('FancyTileEntities/FancySolidTiles',(184,floor-8))
 return additions

def generate():
 doc=json.loads((AE/'sj-factory-authored-profiles-stage25kj.json').read_text());rows=doc['occurrences']
 out=AE/'canaries/stage25kj/Content/Maps/AppleEverestStage25KJ/FactoryProfiles'
 out.mkdir(parents=True,exist_ok=True)
 manifest=[];companion_records=[]
 for group,providers in GROUPS.items():
  factories=[f for f in doc['factories'] if group_for(f)==group]
  tree=ET.Element('Map');levels=ET.SubElement(tree,'levels');ET.SubElement(tree,'Filler')
  style=ET.SubElement(tree,'Style',{'color':'10152c'});bg=ET.SubElement(style,'Backgrounds');fg=ET.SubElement(style,'Foregrounds')
  if group=='MaxMechanics':
   ET.SubElement(bg,'parallax',{'texture':'bgs/01/bg0','x':'0','y':'0','scrollx':'0','scrolly':'0',
    'loopx':'true','loopy':'true','color':'ffffff','alpha':'1','fadeIn':'true','flag':'waterfallfx'})
  shared=room(levels,'canary',*SHARED_ROOMS[group]) if group in SHARED_ROOMS else None
  ET.SubElement(ET.SubElement(tree,'meta',{'IntroType':'None',
    **({'CoreMode':'Hot'} if group=='MaxMechanics' else {}),
    'ForegroundTiles':'Graphics/SJ2021xmls/BeginnerLobby/ForegroundTiles.xml',
    'AnimatedTiles':'Graphics/SJ2021xmls/BeginnerLobby/AnimatedTiles.xml',
    'Sprites':'Graphics/SJ2021xmls/BeginnerLobby/Sprites.xml'}),'mode',
    {'StartLevel':'canary' if shared else 'factory_01','Inventory':'Default'})
  for index,factory in enumerate(factories):
   row=representative(rows,factory);a=row['attributes']
   name='canary' if shared else 'factory_'+str(index+1).zfill(2)
   width=max(640,int(math.ceil((a.get('width',0)+160)/8))*8);height=max(256,int(math.ceil((a.get('height',0)+64)/8))*8)
   if factory['customId']=='vitellary/triggertrigger':height=400
   if factory['customId'] in ['MaxHelpingHand/CameraCatchupSpeedTrigger','MaxHelpingHand/CameraOffsetBorder']:width=960
   if shared:
    level,entities,triggers=shared;width,height=SHARED_ROOMS[group]
   else:level,entities,triggers=room(levels,name,width,height,x=index*2048)
   pos=(160,height-16-a.get('height',0))
   if row['kind']=='entity' and not a.get('height',0):pos=(160,height-32)
   pos=PLACEMENTS.get(factory['customId'],pos)
   if factory['customId']=='CollabUtils2/LobbyMapWarp':pos=(160,height-16)
   if factory['customId']=='MaxHelpingHand/CustomTutorialWithNoBird':pos=(160,height-48)
   if factory['customId'] in ['MaxHelpingHand/FlagToggleCameraOffsetTrigger','MaxHelpingHand/FlagToggleCameraTargetTrigger','MaxHelpingHand/CameraOffsetBorder']:
    pos=(320 if factory['customId']=='MaxHelpingHand/CameraOffsetBorder' else pos[0],pos[1]-80)
   parent=(fg if row.get('layer')=='Foregrounds' else bg) if row['kind']=='backdrop' else triggers if row['kind']=='trigger' else entities
   attrs,nodes=add_factory(parent,row,pos,100+index)
   manifest.append({'kind':row['kind'],'customId':row['customId'],'provider':row['provider'],
     'sourceMap':row['map'],'sourceRoom':row['room'],'sourceEntityId':row['entityId'],'sourceProfileSha256':row['profileSha256'],
     'canaryMap':'AppleEverestStage25KJ/FactoryProfiles/'+group,'canaryRoom':name,'canaryEntityId':100+index,
     'attributes':attrs,'nodes':nodes,'status':'AUTHORED_NOT_YET_RUNTIME_VALIDATED'})
   if not shared:
    extra=isolated_companions(factory['customId'],level,entities,triggers,rows,doc['factories'])
    companion_records.extend({'canaryMap':'AppleEverestStage25KJ/FactoryProfiles/'+group,'canaryRoom':name,**r} for r in extra)
  if shared:
   extra=companions(group,*shared,rows,doc['factories'])
   companion_records.extend({'canaryMap':'AppleEverestStage25KJ/FactoryProfiles/'+group,'canaryRoom':'canary',**r} for r in extra)
  ET.indent(tree,space='  ')
  ET.ElementTree(tree).write(out/(group+'.xml'),encoding='utf-8',xml_declaration=True)
  # These nine files are solely owned by this generator. Preserve the earlier
  # manually authored Sideways map while retiring their previous logical paths.
  old=AE/'canaries/stage25kj/Content/Maps/AppleEverest'/('Stage25KJ'+group+'.xml')
  if old.exists():old.unlink()
 if len(manifest)!=73 or len({(r['kind'],r['customId']) for r in manifest})!=73:raise ValueError('canary factory census differs')
 (AE/'sj-factory-canary-plan-stage25kj.json').write_text(json.dumps({'schemaVersion':1,'originalGameplayMapsCopied':False,
   'selectedFactories':73,'maps':len(GROUPS),'representatives':manifest,'exactProfileCompanions':companion_records},indent=2,ensure_ascii=False)+'\n')
 source=['// Generated by scripts/generate-apple-everest-stage25kj-canaries.py.',
  'namespace Celeste.Mod;', 'internal static class AppleEverestFactoryCanaryPlan', '{',
  '    internal static readonly AppleEverestFactoryCanary.Definition[] Factories = new AppleEverestFactoryCanary.Definition[]', '    {']
 for row in manifest:
  values=[json.dumps(row[key]) for key in ['kind','customId','canaryMap','canaryRoom']]
  values.extend([str(row['canaryEntityId']),json.dumps(row['sourceProfileSha256'])])
  source.append('        new('+', '.join(values)+'),')
 source.extend(['    };','}'])
 (AE/'runtime/AppleEverestFactoryCanaryPlan.cs').write_text('\n'.join(source)+'\n')
 print('AUTHORED: 73 representative factories in',len(GROUPS),'provider-grouped maps; runtime validation pending')

if __name__=='__main__':generate()
