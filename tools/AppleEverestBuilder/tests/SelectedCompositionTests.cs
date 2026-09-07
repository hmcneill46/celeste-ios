using System.IO.Compression;
using System.Text.Json;
using System.Xml;
using AppleEverestBuilder;
using Celeste.Mod;

internal static class SelectedCompositionTests
{
    internal static int Run(string temporary)
    {
        int checks=0;
        void Check(bool condition,string label) { if(!condition)throw new Exception(label);checks++; }
        void Reject(Action action,string label)
        {
            try { action(); } catch(InvalidDataException) { checks++;return; }
            catch(InvalidOperationException) { checks++;return; }
            throw new Exception("composition control accepted: "+label);
        }
        string root=Path.Combine(temporary,"selected-composition");Directory.CreateDirectory(root);
        string xml=Path.Combine(root,"map.xml"),original=Path.Combine(root,"map.bin");
        File.WriteAllText(xml,"<Map><meta Name='Pinned map name' ForegroundTiles='Graphics/terrain.xml'/><levels><level name='room' x='0' y='0' width='320' height='184'><entities><player id='1' x='8' y='8'/></entities><triggers/><solids/><bg/></level></levels><Filler/><Style><Backgrounds/><Foregrounds/></Style></Map>");
        ContentCompiler.CompileMap(xml,original,"OriginalLabel");
        using(var append=new FileStream(original,FileMode.Append))append.Write(new byte[]{91,42,66,0,255});
        string outRoot=Path.Combine(root,"content"),logical="Maps/Selected/Map.bin";
        ContentCompiler.Stage(original,logical,outRoot,preserveOriginalMap:true);
        string staged=Path.Combine(outRoot,logical),sourceHash=Hashing.FileSha256(original);
        Check(Hashing.FileSha256(staged)==sourceHash,"original complete BIN bytes must survive selected staging");
        Check(ContentCompiler.InspectBoundary(staged).AppendixBytes==5,"original opaque appendix preserved");
        var inspected=ContentCompiler.InspectProgression(staged,logical,sourceHash);
        Check(inspected.SourcePackageLabel=="OriginalLabel"&&inspected.Sid=="Selected/Map","source label and canonical SID stay distinct");
        Check(inspected.Presentation!.ForegroundTiles=="Graphics/terrain.xml"&&inspected.Presentation.Name=="Pinned map name","embedded graphics/name metadata consumed");

        string archive=Path.Combine(root,"fixture.zip");
        using(var zip=ZipFile.Open(archive,ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(original,logical);
            using(var writer=new StreamWriter(zip.CreateEntry("Graphics/terrain.xml").Open()))writer.Write("<Data/>");
            using(var writer=new StreamWriter(zip.CreateEntry("Code/forbidden.dll").Open()))writer.Write("host-test-only");
            using(var writer=new StreamWriter(zip.CreateEntry("Audio/unknown.bank").Open()))writer.Write("host-test-only");
            using(var writer=new StreamWriter(zip.CreateEntry("everest.yaml").Open()))writer.Write("- Name: SelectedFixture\n  Version: 1.0.0\n");
        }
        ModInput input=SafeModIngestor.Ingest(archive,root,0);
        var mod=new ResolvedMod {Input=input,Metadata=input.Metadata.Single(),Classification=CompatibilityClass.CONTENT_ONLY,
            Mechanisms=new(StringComparer.Ordinal),ManagedFiles=[],ContentFiles=[],ManagedDetourTargets=new(StringComparer.Ordinal),
            DirectManagedHooks=[],ModInteropRegistrations=[],FrozenIlTransforms=[]};
        var package=new SelectedContentPlan.Package {Name=mod.Metadata.Name,Version=mod.Metadata.Version,
            SourceLogicalSha256=input.SourceSha256,ArchiveSha256=Hashing.FileSha256(archive),IncludedFiles=[
                new(){Path=logical,Sha256=sourceHash,PreserveSourceBytes=true},
                new(){Path="Graphics/terrain.xml",Sha256=input.Files.Single(f=>f.Path=="Graphics/terrain.xml").Sha256,PreserveSourceBytes=true}]};
        var plan=new SelectedContentPlan {SchemaVersion=1,Id="selected-test",Packages=[package]};
        string planFile=Path.Combine(root,"plan.json");
        void Write() => File.WriteAllText(planFile,JsonSerializer.Serialize(plan));
        Write();var loaded=SelectedContentPlan.Load(planFile,[mod]);Check(loaded.Files(mod).Count()==2,"enumerated content plan accepted");
        plan.EverestContent=[new(){Path=SelectedContentPlan.TerrainFallbackPath,Sha256=SelectedContentPlan.TerrainFallbackSha256}];
        Write();Check(SelectedContentPlan.Load(planFile,[mod]).EverestContent.Length==1,"exact pinned core fallback is allowed");
        plan.EverestContent[0].Path="Graphics/unreviewed.png";Write();Reject(()=>SelectedContentPlan.Load(planFile,[mod]),"unreviewed core asset");
        plan.EverestContent[0].Path=SelectedContentPlan.TerrainFallbackPath;plan.EverestContent[0].Sha256=new string('0',64);
        Write();Reject(()=>SelectedContentPlan.Load(planFile,[mod]),"core fallback source substitution");plan.EverestContent=[];
        var mounts=new[]{new ContentMountRecord(mod.Metadata.Name,0,logical,logical,sourceHash,sourceHash),
            new ContentMountRecord(mod.Metadata.Name,0,"Graphics/terrain.xml","AppleEverest/fixture/terrain.xml","","")};
        var binding=MapBindingsGenerator.Bind([inspected],mounts,loaded).Single();
        Check(binding.ForegroundTiles=="AppleEverest/fixture/terrain.xml"&&!binding.MergeAnimations,"original graphics use mounted paths and fresh banks");
        Check(binding.TerrainSeed=="Pinned map name".Sum(c=>(int)c),"original Name metadata determines exact terrain seed");
        var defaults=MapBindingsGenerator.Bind([inspected with {Presentation=inspected.Presentation with {Name=""}}],mounts,loaded).Single();
        Check(defaults.TerrainSeed=="Selected/Map".Sum(c=>(int)c),"raw SID supplies missing original Name seed");
        Check(MapBindingsGenerator.Bind([inspected],mounts,null).Single().TerrainSeed==-1,"ordinary accepted seed is unchanged");
        Reject(()=>MapBindingsGenerator.Bind([inspected],mounts.Take(1).ToArray(),loaded),"missing map graphics");
        package.ArchiveSha256=new string('0',64);Write();Reject(()=>SelectedContentPlan.Load(planFile,[mod]),"archive substitution");
        package.ArchiveSha256=Hashing.FileSha256(archive);package.IncludedFiles[0].Sha256=new string('0',64);Write();
        Reject(()=>SelectedContentPlan.Load(planFile,[mod]),"selected file substitution");
        package.IncludedFiles[0].Sha256=sourceHash;
        foreach(string forbidden in new[]{"Code/forbidden.dll","Audio/unknown.bank"})
        {
            var saved=package.IncludedFiles;package.IncludedFiles=[new(){Path=forbidden,Sha256=input.Files.Single(f=>f.Path==forbidden).Sha256}];
            Write();Reject(()=>SelectedContentPlan.Load(planFile,[mod]),"unregistered code/bank");package.IncludedFiles=saved;
        }
        var definition=new XmlDocument();definition.LoadXml("<Tileset id='1' scanWidth='5' scanHeight='5'><set mask='xxxxx-xxxxx-xx1xx-xxxxx-xxxxx' tiles='0,0'/></Tileset>");
        AppleEverestTileMaskRules.ValidateDefinition(definition.DocumentElement!,out int width,out int height);
        Check(width==5&&height==5,"generic bounded 5x5 definition");
        foreach(string value in new[]{"111","11111111111111111111111111","yyyyy-yyyyy-yyyyy-yyyyy-yyyyy"})
        {
            definition.DocumentElement!.FirstChild!.Attributes!["mask"]!.Value=value;
            Reject(()=>AppleEverestTileMaskRules.ValidateDefinition(definition.DocumentElement,out _,out _),"invalid mask");
        }
        return checks;
    }
}
