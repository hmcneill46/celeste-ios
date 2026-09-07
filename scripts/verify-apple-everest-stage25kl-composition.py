#!/usr/bin/env python3
"""Gate real map composition and bind it to actual compiled production evidence."""
from __future__ import annotations
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
MAPS={
    "Maps/StrawberryJam2021/0-Lobbies/1-Beginner.bin":("a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2","3Plain",524807,"d76dba1bc5834d999f4d622afd008a702ffe6663b17fe7b0be3c5cc69afabe0d"),
    "Maps/StrawberryJam2021/1-Beginner/Bing_Over_Google.bin":("e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347","Bing_Over_Google",70024,"963d5f086279311c18561df8fb135e445ca1cf0bcc2d3f1ba0d8b21f8376da6d")}


def sha(path):
    h=hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda:f.read(1024*1024),b""):h.update(block)
    return h.hexdigest()


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);sys.modules[name]=value;spec.loader.exec_module(value);return value


def check(condition,message):
    if not condition:raise ValueError(message)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ["closure","runtime","production-preflight","content-plan","output"]:
        parser.add_argument("--"+name,required=True,type=Path)
    args=parser.parse_args()
    closure=args.closure.resolve();runtime=args.runtime.resolve();output=args.output.resolve()
    output.mkdir(parents=True,exist_ok=True)
    for name in ["readiness.json","READY_FOR_REAL_SJ_PRODUCT_BUILD"]:
        (output/name).unlink(missing_ok=True)
    m=json.loads((closure/"compatibility-manifest.json").read_text());plan=json.loads(args.content_plan.read_text())
    compiled=json.loads(args.production_preflight.read_text())
    check(compiled["sharedClosureSha256"]==m["sharedClosureSha256"],"compiled preflight belongs to a different closure")
    check(compiled["actualProductionRegenerated"] and compiled["compilation"]["FreshProductionCompilation"],"actual fresh compiled production proof is required")
    check(compiled["contentIdCensus"]=={"selectedOccurrences":920,"acceptedOrVanilla":920,"blocked":0,"unclassified":0},"gate A differs from selected 920 occurrences")
    check(compiled["registrationCensus"]=={"selected":73,"available":73,"unavailable":0,"providerRejected":0},"gate B is not 73 available")
    compiler=module("kl_content",ROOT/"scripts/generate-apple-everest-stage25kl-content.py")
    atlas=module("kl_atlas",ROOT/"scripts/inventory-celeste-controller-prompts.py")
    content=closure/"content/Content";canonical=ROOT/".build/celeste-ios/current/content/Content"
    mounts={(row["owner"],row["sourcePath"]):row for row in m["contentMounts"]}
    sources={row["sourcePath"]:row for row in m["contentMounts"]}
    selected_mods={row["name"]:row for row in m["selectedMods"]}
    for row in m["contentMounts"]:
        check(sha(content/row["logicalPath"])==row["sha256"],"staged content hash mismatch: "+row["sourcePath"])
    checked_files=0
    check(plan.get("everestContent")==[{"path":compiler.FALLBACK_PATH,"sha256":compiler.FALLBACK_SHA}],"pinned core fallback selection differs")
    for entry in plan["everestContent"]:
        mount=mounts[("Everest",entry["path"])]
        check(mount["sha256"]==entry["sha256"],"selected Everest core file differs")
        checked_files+=1
    for package in plan["packages"]:
        check(selected_mods[package["name"]]["sourceHash"]==package["sourceLogicalSha256"],"selected package source identity mismatch")
        for entry in package["includedFiles"]:
            mount=mounts[(package["name"],entry["path"])]
            if entry.get("preserveSourceBytes") or not entry["path"].endswith(".xml"):
                check(mount["sha256"]==entry["sha256"],"selected original file bytes differ: "+entry["path"])
            checked_files+=1
    actual_maps={row["sourcePath"] for row in m["contentMounts"] if row["owner"]=="StrawberryJam2021" and row["sourcePath"].startswith("Maps/") and row["sourcePath"].endswith(".bin")}
    check(actual_maps==set(MAPS) and plan["excludedMapCount"]==126,"original SJ map selection widened")
    trees={};map_reports=[]
    bindings={row["Sid"]:row for row in json.loads((closure/"map-bindings.json").read_text())}
    for path,(digest,label,root_bytes,appendix) in MAPS.items():
        mount=mounts[("StrawberryJam2021",path)];file=content/mount["logicalPath"]
        check(sha(file)==digest,"original map bytes changed")
        parsed=trees[path]=compiler.read_map(file.read_bytes());sid=path[5:-4]
        check(parsed["label"]==label and parsed["rootBytes"]==root_bytes and parsed["appendixSha256"]==appendix,"original source boundary differs")
        check(bindings[sid]["SourceLabel"]==label and bindings[sid]["TerrainSeed"]==sum(map(ord,sid)),"original label/seed binding differs")
        map_reports.append({"sid":sid,"sha256":digest,"sourcePackageLabel":label,"rootBytes":root_bytes,
                            "appendixBytes":file.stat().st_size-root_bytes,"appendixSha256":appendix,"terrainSeed":bindings[sid]["TerrainSeed"]})
    map_evidence=output/"original-map-trees.json";map_evidence.write_text(json.dumps(trees,ensure_ascii=False)+"\n")

    atlas_keys={};atlas_metadata={}
    for name in ["Gameplay","Gui","Misc","ColorGrades"]:
        file=canonical/"Graphics/Atlases"/(name+".meta")
        if name=="ColorGrades":file=canonical/"Graphics/ColorGrading.meta"
        if file.exists():
            atlas_keys[name]={key.lower():None for key in atlas.parse_paths(file.read_bytes())};atlas_metadata[name]=sha(file)
        else:atlas_keys[name]={}
    for file in (canonical/"Graphics/ColorGrading").rglob("*.png"):
        key=file.relative_to(canonical/"Graphics/ColorGrading").with_suffix("").as_posix()
        atlas_keys["ColorGrades"][key.lower()]=file
        atlas_metadata["ColorGrades/"+key]=sha(file)
    for mount in m["contentMounts"]:
        path=mount["sourcePath"]
        for name,prefix in [("Gameplay","Graphics/Atlases/Gameplay/"),("Gui","Graphics/Atlases/Gui/"),("ColorGrades","Graphics/ColorGrading/")]:
            if path.startswith(prefix) and path.endswith(".png"):
                atlas_keys[name][path[len(prefix):-4].lower()]=content/mount["logicalPath"]
    def require_texture(key,name="Gameplay"):
        check(key.lower() in atlas_keys[name],"unresolved "+name+" texture: "+key)
        return atlas_keys[name][key.lower()]
    def frames(key):
        keys=atlas_keys["Gameplay"];found=[];count=0
        while True:
            candidates=([key] if count==0 else [])+[key+str(count).zfill(w) for w in range(len(str(count)),len(str(count))+6)]
            selected=next((p for p in candidates if p.lower() in keys),None)
            if selected is None:break
            found.append(selected);count+=1
        check(found,"unresolved animation frames: "+key)
        return found
    def xml_path(source):
        return content/sources[source]["logicalPath"] if source in sources else canonical/source
    graphics="Graphics/SJ2021xmls/BeginnerLobby/"
    terrain={};terrain_reports=[];animations={};frame_counts={};missing_terrain={}
    expected_missing=[{"key":"tilesets/subfolder/betterTemplate","tileId":"y","sources":[graphics+"ForegroundTiles.xml",graphics+"BackgroundTiles.xml"],
                       "resolution":"PINNED_EVEREST_ATLAS_FALLBACK_AND_TILESET_MODULO","usedCellCount":0}]
    check(plan.get("originalMissingTerrainTextures")==expected_missing,"unreviewed original missing terrain profile")
    def terrain_texture(filename,tile):
        key="tilesets/"+tile.attrib["path"]
        if key.lower() in atlas_keys["Gameplay"]:return require_texture(key),False
        check(any(row["key"]==key and row["tileId"]==tile.attrib["id"] and filename in row["sources"] for row in expected_missing),"unreviewed missing terrain texture: "+key)
        return require_texture("__fallback"),True
    def used_terrain_complete(foreground,background,fg_xml,bg_xml):
        check(not foreground&missing_terrain[fg_xml] and not background&missing_terrain[bg_xml],"used real terrain cannot rely on a missing sheet")
    for filename in [graphics+"AnimatedTiles.xml","Graphics/AnimatedTiles.xml"]:
        file=xml_path(filename)
        if not file.exists():continue
        bank={}
        for node in ET.parse(file).getroot():
            check(node.attrib["name"] not in bank,"duplicate animation ID in source bank")
            bank[node.attrib["name"]]=len(frames(node.attrib["path"]))
            frame_counts[node.attrib["path"]]=bank[node.attrib["name"]]
        animations[filename]=bank
    sprite_reports=[]
    for sprite in ET.parse(xml_path(graphics+"Sprites.xml")).getroot():
        ids=[];selected_frames=[]
        check(set(sprite.attrib)<={"path","start","delay"},"unsupported selected sprite profile")
        for animation in sprite:
            if animation.tag=="Center":continue
            check(animation.tag in ("Anim","Loop"),"unsupported selected sprite node")
            ident=animation.attrib["id"];check(ident not in ids,"duplicate sprite animation");ids.append(ident)
            available=frames(sprite.attrib["path"]+animation.attrib.get("path",""))
            raw=animation.attrib.get("frames","");indices=[]
            if raw:
                for part in raw.split(","):
                    check(re.fullmatch(r"\s*\d+(?:-\d+)?\s*",part) is not None,"unsupported sprite frame syntax")
                    pair=list(map(int,part.strip().split("-")))
                    indices.extend(range(pair[0],pair[-1]+1))
                check(indices and all(0<=index<len(available) for index in indices),"selected sprite frame missing")
            else:indices=list(range(len(available)))
            selected_frames.append({"id":ident,"frames":[available[index] for index in indices]})
        check(sprite.attrib.get("start","") in ids,"sprite starting animation missing")
        sprite_reports.append({"sprite":sprite.tag,"animations":selected_frames})
    for filename in [graphics+"ForegroundTiles.xml",graphics+"BackgroundTiles.xml","Graphics/ForegroundTiles.xml","Graphics/BackgroundTiles.xml"]:
        definitions={};rule_count=0;wrapped_coordinates=0;missing_terrain[filename]=set()
        for tile in ET.parse(xml_path(filename)).getroot():
            ident=tile.attrib["id"];check(ident not in definitions,"duplicate terrain ID")
            width=int(tile.attrib.get("scanWidth",3));height=int(tile.attrib.get("scanHeight",3))
            check((width,height) in ((3,3),(5,5)),"unsupported terrain dimensions")
            for forbidden in ["ignoreExceptions","soundPath","soundParam","debrisImpactSfx"]:
                check(forbidden not in tile.attrib,"unsupported terrain profile")
            if "copy" in tile.attrib:
                check(tile.attrib["copy"] in definitions,"missing/forward terrain copy")
                base=definitions[tile.attrib["copy"]]
                check("copy" not in base.attrib and (int(base.attrib.get("scanWidth",3)),int(base.attrib.get("scanHeight",3)))==(width,height),"unsupported terrain copy dimensions/chain")
            texture,is_missing=terrain_texture(filename,tile)
            if is_missing:missing_terrain[filename].add(ident)
            dimensions=None
            if texture:
                data=texture.read_bytes();check(data[:8]==b'\x89PNG\r\n\x1a\n',"terrain sheet is not PNG")
                dimensions=struct.unpack(">II",data[16:24])
            rules=list(tile)+(list(definitions[tile.attrib["copy"]]) if "copy" in tile.attrib else [])
            for rule in rules:
                check(rule.tag=="set","unsupported terrain rule")
                mask=rule.attrib["mask"];cells=re.sub(r"[-\s]","",mask)
                check(mask in ("center","padding") or len(cells)==width*height and set(cells)<=set("01xX"),"invalid terrain mask")
                for coordinate in rule.attrib["tiles"].split(";"):
                    x,y=map(int,coordinate.split(","));check(x>=0 and y>=0,"negative terrain coordinates")
                    if dimensions:
                        check(dimensions[0]>=8 and dimensions[1]>=8,"terrain sheet has no complete tile")
                        if (x+1)*8>dimensions[0] or (y+1)*8>dimensions[1]:wrapped_coordinates+=1
                bank=animations[graphics+"AnimatedTiles.xml"] if filename.startswith(graphics) else animations["Graphics/AnimatedTiles.xml"]
                for sprite in rule.attrib.get("sprites","").split(","):
                    if sprite:check(sprite in bank,"terrain overlay missing from active bank: "+sprite)
                rule_count+=1
            if tile.attrib.get("debris"):frames("debris/"+tile.attrib["debris"])
            definitions[ident]=tile
        terrain[filename]=definitions
        terrain_reports.append({"source":filename,"sourceSha256":sha(xml_path(filename)),"definitions":len(definitions),"constructorRuleReads":rule_count,
                                "originalMissingTextureIds":sorted(missing_terrain[filename]),"wrappedConstructorCoordinates":wrapped_coordinates,
                                "dimensions":dict(Counter(tile.attrib.get("scanWidth","3")+"x"+tile.attrib.get("scanHeight","3") for tile in definitions.values()))})

    destinations=[];parallaxes=[];decal_occurrences=0;required_audio=set();used_terrain={}
    factory_ids={(row["kind"],row["customId"]) for row in json.loads((ROOT/"apple-everest/sj-factory-authored-profiles-stage25kj.json").read_text())["factories"]}
    selected_occurrences=0
    for path,parsed in trees.items():
        sid=path[5:-4];binding=bindings[sid];is_lobby="/0-Lobbies/" in path
        check(not binding["MergeAnimations"],"original maps must replace animated banks")
        fg_xml=graphics+"ForegroundTiles.xml" if is_lobby else "Graphics/ForegroundTiles.xml"
        bg_xml=graphics+"BackgroundTiles.xml" if is_lobby else "Graphics/BackgroundTiles.xml"
        if is_lobby:
            for field in ["ForegroundTiles","BackgroundTiles","AnimatedTiles","Sprites"]:
                check(binding[field]==sources[graphics+field+".xml"]["logicalPath"],"map graphics binding differs: "+field)
        else:check(all(not binding[field] for field in ["ForegroundTiles","BackgroundTiles","AnimatedTiles","Sprites"]),"Bing should use default graphics")
        used={"foreground":set(),"background":set()}
        for parent,node in compiler.walk(parsed["tree"]):
            a=node["attributes"];name=node["name"]
            kind="entity" if parent=="entities" else "trigger" if parent=="triggers" else "backdrop" if parent in ("Foregrounds","Backgrounds") else None
            if (kind,name) in factory_ids:selected_occurrences+=1
            if name in ("solids","bg"):
                ids=set(a.get("innerText",""))-set("\r\n0\0")
                layer="foreground" if name=="solids" else "background";used[layer]|=ids
                check(ids<=set(terrain[fg_xml if layer=="foreground" else bg_xml]),"used terrain lacks definition")
            if name in ("dashBlock","crumbleWallOnRumble","introCrusher"):
                tile=a.get("tiletype","3");used["foreground"].add(tile)
                check(tile in terrain[fg_xml],"entity terrain lacks definition")
            for attribute,value in a.items():
                if attribute.lower()=="tiletype":
                    check(isinstance(value,str) and len(value)==1 and value in terrain[fg_xml],"authored entity terrain lacks definition: "+name)
                    used["foreground"].add(value)
            if name=="FancyTileEntities/FancySolidTiles":
                raw=a.get("tileData","")
                ids=set("".join(row.strip() for row in raw.split("," if "," in raw else "\n")))-set("0\0")
                check(ids<=set(terrain[fg_xml]),"Fancy terrain lacks definition");used["foreground"]|=ids
            if parent in ("fgdecals","bgdecals"):
                texture=a["texture"].replace("\\","/");extension=Path(texture).suffix
                if extension:texture=texture.replace(extension,"")
                frames("decals/"+re.sub(r"\d+$","",texture));decal_occurrences+=1
            if is_lobby and name in ("SJ2021/StrawberryJamJar","CollabUtils2/ChapterPanelTrigger"):
                target=a["map"];status="AVAILABLE_SELECTED" if target in bindings else "UNAVAILABLE_EXCLUDED" if "Maps/"+target+".bin" in plan["excludedMaps"] else "UNAVAILABLE_UNKNOWN"
                check(status!="UNAVAILABLE_UNKNOWN","unknown authored destination")
                destinations.append({"sid":target,"status":status,"kind":name,"entityId":a["id"]})
            if name=="CollabUtils2/MiniHeartDoor":check(a["requires"]==21,"authored heart gate was lowered")
            if name=="CollabUtils2/LobbyMapController":check(a["totalMaps"]==21,"authored map count was lowered")
            if name.lower()=="parallax":
                texture=a.get("texture","");requested=a.get("atlas","game")
                chosen="Gameplay" if requested=="game" and texture.lower() in atlas_keys["Gameplay"] else "Gui" if requested=="gui" and texture.lower() in atlas_keys["Gui"] else "Misc"
                require_texture(texture,chosen)
                if texture.startswith("bgs/MaxHelpingHand/animatedParallax/"):
                    key=re.sub(r"\d+$","",texture);count=len(frames(key));frame_counts[key]=count
                    fps_match=re.search(r"[^0-9]((?:[0-9]+\.)?[0-9]+)fps$",key);fps=float(fps_match.group(1)) if fps_match else 12
                    check(1<=count<=256 and 0<fps<=120,"animated parallax outside finite bounds")
                    check(not any("Graphics/Atlases/Gameplay/"+key+ext in sources for ext in (".meta",".meta.yaml",".meta.yml")),"animated parallax metadata unsupported")
                    parallaxes.append({"key":key,"frames":count,"fps":fps})
                check("animatedHdParallax" not in texture and "/hdParallax/" not in texture,"HD parallax unsupported")
            for key,value in a.items():
                if isinstance(value,str) and value.startswith("event:/"):required_audio.add(value)
                if key.lower() in ("colorgrade","colorgradea","colorgradeb","colorgradefrom","colorgradeto") and isinstance(value,str) and value not in ("","(current)"):
                    require_texture(value,"ColorGrades")
        used_terrain_complete(used["foreground"],used["background"],fg_xml,bg_xml)
        used_terrain[sid]={key:sorted(value) for key,value in used.items()}
    check(selected_occurrences==920,"actual original selected factory occurrences differ from gate A")
    check(len(destinations)==23 and Counter(d["status"] for d in destinations)=={"AVAILABLE_SELECTED":1,"UNAVAILABLE_EXCLUDED":22},"authored destination availability differs")
    check(sorted((p["frames"],p["fps"]) for p in parallaxes)==[(4,3.0),(64,12)],"animated parallax selected scope changed")
    check(decal_occurrences==7342,"original decal occurrence census changed")
    negative_controls=[]
    def reject_control(action,label):
        try:action()
        except ValueError:negative_controls.append(label);return
        raise ValueError("negative composition control accepted: "+label)
    fg_source=graphics+"ForegroundTiles.xml"
    for key,tile,label in [("tilesets/sj2021/mosscairn/grayextended",terrain[fg_source]["J"],"OMITTED_USED_TERRAIN_SHEET"),
                           ("__fallback",terrain[fg_source]["y"],"OMITTED_PINNED_CORE_FALLBACK")]:
        saved=atlas_keys["Gameplay"].pop(key)
        try:reject_control(lambda:terrain_texture(fg_source,tile),label)
        finally:atlas_keys["Gameplay"][key]=saved
    reject_control(lambda:used_terrain_complete({"y"},set(),fg_source,graphics+"BackgroundTiles.xml"),"MISSING_TEMPLATE_BECOMES_VISIBLE")
    reject_control(lambda:terrain_texture(fg_source,ET.Element("Tileset",id="z",path="unreviewed/missing")),"UNREVIEWED_MISSING_TEMPLATE")
    required_audio.update(["event:/sj21_levelselect","event:/sj21_jamjar-blue","event:/SC2020_heartShard_get","event:/HonlyHelper/catsfx"])
    events={g["Path"] for bank in m["customAudioBanks"] for g in bank["guids"] if g["Kind"]=="event"}
    custom_required={event for event in required_audio if not event.startswith(("event:/game/","event:/music/","event:/env/","event:/char/","event:/ui/"))}
    check(custom_required<=events,"selected custom audio event missing bank: "+str(custom_required-events))
    check(m["customAudioBankCount"]==7 and m["frozenIlTransformCount"]==17 and m["managedDetourTargetCount"]==205 and m["appleApiSurfaceMemberCount"]==30,"accepted catalog/audio scope changed without review")
    map_data=(runtime/"Celeste/MapData.cs").read_text()
    for consumer in ["AppleEverestMapBinding.HeaderMatches(ModeData.Path, element.Package)","AppleEverestMapBinding.Find(ModeData.Path)?.TerrainSeed",
                     "AppleEverestAnimatedParallax.Create(mTexture)"]:
        check(consumer in map_data,"actual map consumer is missing: "+consumer)
    check("AppleEverestSelectedCanaryAssets.PrepareLevel(session)" in (runtime/"Celeste/LevelLoader.cs").read_text(),"actual graphics lifecycle consumer absent")
    generated_content=(closure/"managed/GeneratedAppleEverestContentManifest.cs").read_text()
    content_owners=set(re.findall(r'new AppleEverestModContentDescriptor\("([^"]+)"',generated_content))
    asset_owners=set(re.findall(r'new AppleEverestStaticAssetDescriptor\("([^"]+)"',generated_content))
    check(asset_owners<=content_owners,"static content owner unresolved before startup: "+str(asset_owners-content_owners))
    check('new AppleEverestAtlasMountDescriptor("Everest", "Gameplay", "__fallback",' in generated_content,"core fallback atlas mount absent")
    sprite_bank_section=generated_content.split("AppleEverestSpriteBankDescriptor[] SpriteBanks =",1)[1].split("};",1)[0]
    check(graphics+"Sprites.xml" not in sprite_bank_section,"map sprites leaked into global sprite bank")
    collab_rows=[line.split("\t") for line in (closure/"collab-manifest.txt").read_text().splitlines() if "\tStrawberryJam2021\t" in line]
    map_rows=[row for row in collab_rows if row[0]=="map"]
    check(len(map_rows)==1,"collab selected map list widened")
    bing=map_rows[0]
    check(bing[3]=="StrawberryJam2021/1-Beginner/Bing_Over_Google" and bing[4]=="If my 'driveway' almost did you in..." and
          bing[5]=="by Bing_Over_Google" and len(bing[10].split(","))==13 and bing[11:18]==["saving","SetReturnToHere","sj2021beginnerlobby","1520","552","3","heart"],"real Bing panel/progression descriptor differs")

    # Compile actual generated runtime binding/lifecycle/animation sources.
    probe=output/"runtime-probe";probe.mkdir(exist_ok=False)
    runtime_static=runtime/"Celeste/Mod/AppleEverestStatic"
    source_hashes={}
    for filename,repo_path in {
        "AppleEverestMapBinding.cs":"apple-everest/runtime/AppleEverestMapBinding.cs",
        "AppleEverestSelectedCanaryAssets.cs":"apple-everest/runtime/semantics/AppleEverestSelectedCanaryAssets.cs",
        "AppleEverestAnimatedParallax.cs":"apple-everest/runtime/semantics/AppleEverestAnimatedParallax.cs"}.items():
        check(sha(runtime_static/filename)==sha(ROOT/repo_path),"actual runtime source differs: "+filename)
        shutil.copyfile(runtime_static/filename,probe/filename);source_hashes[repo_path]=sha(ROOT/repo_path)
    static_source=ROOT/"apple-everest/runtime/AppleEverestStaticRuntime.cs"
    applied_static=(runtime_static/static_source.name).read_text()
    # The accepted Xaphan lowering inserts this single call after source copy.
    accepted_sprite_hook='        global::Celeste.Mod.AppleEverestXaphanSlopeHooks.InstallSpriteExtensions(StaticSpriteBank);\n'
    check(applied_static.count(accepted_sprite_hook)==1 and applied_static.replace(accepted_sprite_hook,"")==static_source.read_text(),
          "actual texture observation source differs beyond accepted Xaphan transform")
    source_hashes[static_source.relative_to(ROOT).as_posix()]=sha(static_source)
    observer=static_source.read_text().split("public static void ObserveTextureUsage(",1)[1].split("private static void MountStaticModContent",1)[0]
    observer=re.sub(r"//[^\n]*","",observer)
    check("var backing = texture.Texture" in observer and "backing == null" in observer and ".Texture_Safe" not in observer and "EnsureLoaded(" not in observer and
          "GC.GetTotalMemory(false)" in observer,"texture measurement must not force decode or collection")
    check('ObserveTextureUsage("content-ready")' in static_source.read_text() and
          'ObserveTextureUsage("level-loaded", this)' in (runtime/"Celeste/Level.cs").read_text(),
          "actual content/level texture observation consumers absent")
    shutil.copyfile(runtime_static/"GeneratedAppleEverestMapBindings.cs",probe/"Generated.cs")
    shutil.copyfile(ROOT/".build/celeste-ios/current/managed/Celeste/AnimatedTilesBank.cs",probe/"AnimatedTilesBank.cs")
    for filename in ["Stubs","Program"]:shutil.copyfile(ROOT/("tools/AppleEverestBuilder/tests/CompositionRuntime"+filename+".cs.txt"),probe/(filename+".cs"))
    (probe/"Probe.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup></Project>\n')
    (probe/"request.json").write_text(json.dumps({"contentRoot":str(content),"frames":frame_counts,"destinations":[d["sid"] for d in destinations],"parallaxes":parallaxes,
        "originalAnimationCount":len(animations[graphics+"AnimatedTiles.xml"]),"vanillaForeground":str(canonical/"Graphics/ForegroundTiles.xml"),"vanillaBackground":str(canonical/"Graphics/BackgroundTiles.xml")}))
    with (probe/"run.log").open("w") as log:
        run=subprocess.run(["dotnet","run","--project",str(probe/"Probe.csproj"),"-c","Release","--",str(probe/"request.json"),str(output/"runtime-composition.json")],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT)
    if run.returncode:raise ValueError("actual runtime composition probe failed:\n"+(probe/"run.log").read_text()[-7000:])
    subprocess.run([sys.executable,str(ROOT/"scripts/verify-apple-everest-autotiler-stage25kl.py"),"--runtime",str(runtime),"--closure",str(closure),
                    "--map-evidence",str(map_evidence),"--work-root",str(output/"autotiler-host"),"--output",str(output/"autotiler-conformance.json")],check=True,cwd=ROOT)

    baseline_path=ROOT/"apple-everest/sj-kj-accepted-semantic-stage25kl.json"
    check(sha(baseline_path)=="0097f41ed546502765849d86f951925596b8ba0d61671a98ddd446cbe0f6f652","accepted semantic ledger identity differs")
    baseline=json.loads(baseline_path.read_text());check(baseline["census"]=={"selected":73,"fullyClosed":73,"blocked":0,"unknown":0},"accepted semantic baseline is not closed")
    deltas=[]
    for factory in baseline["factories"]:
        for binding in factory["implementation"].get("sourceBindings",[]):
            for source in binding.get("sourceFiles",[]):
                current=sha(ROOT/source["path"])
                if current!=source["sha256"]:
                    check(source["path"]=="apple-everest/runtime/AppleEverestCollabRuntime.cs" and factory["customId"] in ("CollabUtils2/ChapterPanelTrigger","CollabUtils2/JournalTrigger"),"unreviewed semantic source delta: "+source["path"])
                    deltas.append({"factory":factory["customId"],"source":source["path"],"acceptedSha256":source["sha256"],"currentSha256":current,
                                   "review":"KL06 fail-closed destination checks and removal of area-zero fallback; available-destination behavior retained"})
    check(len(deltas)==2,"expected reviewed semantic delta census changed")
    collab=(ROOT/"apple-everest/runtime/AppleEverestCollabRuntime.cs").read_text()
    check("AreaData.Get(0)" not in collab and "talk.Enabled = AppleEverestCollabRuntime.DestinationAvailable(sid)" in collab and
          "!DestinationAvailable(forcedMapSid)" in collab and "!DestinationAvailable(sid)" in collab,"destination guards absent")
    report={"schemaVersion":1,"status":"PASS","marker":"READY_FOR_REAL_SJ_PRODUCT_BUILD","sharedClosureSha256":m["sharedClosureSha256"],
            "gateA":compiled["contentIdCensus"],"gateB":compiled["registrationCensus"],
            "gateC":{"selected":73,"closed":73,"blocked":0,"unknown":0,"authority":"ACCEPTED_KJ_SOURCE_LIFECYCLE_LEDGER_PLUS_CURRENT_COMPILED_GUARDS_AND_VERIFIED_BOUNDED_DELTAS","baselineSha256":sha(baseline_path),"deltas":deltas,"physicalKL":"PENDING_EXACT_PRODUCT_TESTS"},
            "gateD":{"blocked":0,"unknown":0,"maps":map_reports,"selectedPlanFileCount":checked_files,"totalMountedFilesIncludingRegressions":m["contentFileCount"],
                     "terrain":terrain_reports,"usedTerrain":used_terrain,"decalOccurrences":decal_occurrences,"parallaxes":parallaxes,"destinations":destinations,
                     "mapSprites":sprite_reports,"collabManifestSha256":sha(closure/"collab-manifest.txt"),"progressionManifestSha256":sha(closure/"levelset-progression-manifest.txt"),
                     "rejectedCompositionControls":negative_controls,
                     "textureObservation":{"checkpoints":["content-ready","level-loaded"],"scope":"static atlas mounts",
                                           "readsBackingFieldsWithoutDecode":True,"rgbaBytesAreEstimate":True,"managedLiveBytesAreNotProcessResidentBytes":True},
                     "requiredCustomAudio":sorted(custom_required),"atlasMetadataSha256":atlas_metadata,"runtimeSourceSha256":source_hashes,
                     "runtimeProbeSha256":sha(output/"runtime-composition.json"),"autotilerConformanceSha256":sha(output/"autotiler-conformance.json")},
            "compiledPreflightSha256":sha(args.production_preflight),"contentPlanSha256":sha(args.content_plan)}
    (output/"readiness.json").write_text(json.dumps(report,indent=2,ensure_ascii=False)+"\n")
    (output/"READY_FOR_REAL_SJ_PRODUCT_BUILD").write_text(m["sharedClosureSha256"]+"\n")
    print("PASS: four gates A920/920/0/0 B73/73/0 C73/73/0/0 D0/0; READY_FOR_REAL_SJ_PRODUCT_BUILD")


if __name__=="__main__":main()
