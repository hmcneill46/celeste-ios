#nullable disable
using System.IO.Compression;
using System.Text.Json;
using AppleEverestBuilder;
using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;

// Owned synthetic package bytes; no distributed DLL or map is a test fixture.
internal static class SnasVerificationControlsTests
{
    internal static int Run(string temporary, string repository)
    {
        int passed = 0;
        void Reject(Action action, string expected)
        {
            try { action(); }
            catch (Exception e) when (e is InvalidDataException or InvalidOperationException &&
                e.Message.Contains(expected, StringComparison.Ordinal)) { passed++; return; }
            throw new Exception("K-N negative control did not fail at " + expected);
        }
        string work = Path.Combine(temporary, "snas-verification-controls");
        Directory.CreateDirectory(work);
        string archive = Path.Combine(work, "owned-fixture.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("everest.yaml").Open()))
                writer.Write("- Name: OwnedIdentityFixture\n  Version: 1.0.0\n  DLL: Owned.dll\n");
            using (var writer = new StreamWriter(zip.CreateEntry("Owned.dll").Open()))
                writer.Write("project-owned identity fixture, deliberately not executable\n");
        }
        ModInput input = SafeModIngestor.Ingest(archive, work, 0);
        EverestYamlEntry metadata = input.Metadata.Single();
        string dll = Path.Combine(input.StagingRoot, "Owned.dll");
        byte[] originalDll = File.ReadAllBytes(dll);
        JsonElement pin = JsonSerializer.SerializeToElement(new {
            name = metadata.Name, resolvedVersion = metadata.Version, zipSha256 = Hashing.FileSha256(archive),
            distributedDlls = new[] { new { path = "Owned.dll", sha256 = Hashing.FileSha256(dll) } } });
        _ = FactoryPackageIdentity.Verify(pin, input, metadata); passed++;
        metadata.Name = "WrongOwner";
        Reject(() => FactoryPackageIdentity.Verify(pin, input, metadata), "provider ownership differs");
        metadata.Name = "OwnedIdentityFixture";
        metadata.Version = "1.0.1";
        Reject(() => FactoryPackageIdentity.Verify(pin, input, metadata), "provider version differs");
        metadata.Version = "1.0.0";
        File.WriteAllBytes(dll, [..originalDll, 0]);
        Reject(() => FactoryPackageIdentity.Verify(pin, input, metadata), "provider DLL identity differs");
        File.WriteAllBytes(dll, originalDll);
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        using (var writer = new StreamWriter(zip.CreateEntry("extra.txt").Open())) writer.Write("changed ZIP");
        Reject(() => FactoryPackageIdentity.Verify(pin, input, metadata), "provider archive identity differs");

        // A plausible census must not substitute for the exact extraction.
        string authored = Path.Combine(work, "forged-authored.json");
        foreach (string value in new[] { "{}", "{\"census\":{\"selectedFactories\":77,\"selectedOccurrences\":973}}",
                     "{\"census\":{\"selectedFactories\":73,\"selectedOccurrences\":920}}" })
        {
            File.WriteAllText(authored, value);
            Reject(() => { using var ignored = SnasProfileAuthority.Load(authored); }, "authority differs");
        }
        string contract = Path.Combine(repository, "apple-everest/sj-snas-factory-contract-stage25kn.json");
        if (Hashing.FileSha256(contract) != SelectedFactoryContract.SnasManifestSha256)
            throw new Exception("K-N product contract hash differs");
        passed++;
        Reject(() => { using var ignored = SelectedFactoryContract.Load(contract, authored); }, "authority differs");
        string changedContract = Path.Combine(work, "changed-contract.json");
        File.WriteAllText(changedContract, File.ReadAllText(contract) + " ");
        bool contractRejected = false;
        try { using var ignored = SelectedFactoryContract.Load(changedContract, authored); }
        catch (InvalidDataException) { contractRejected = true; }
        if (!contractRejected) throw new Exception("mutated K-N contract escaped exact contract selection");
        passed++;

        EntityData Bubble() => new() { Name = "CommunalHelper/PlayerBubbleRegion", Position = new(10, 20),
            Width = 14, Height = 14, Values = new(), Nodes = [new(213, 107), new(421, 159)] };
        EntityData Sound() => new() { Name = "ContortHelper/RandomSoundTrigger", Width = 24, Height = 8,
            Nodes = [], Values = new() { ["audioEvents"] = "event:/sj21_snas_flourish", ["delay"] = 0,
                ["flagsAfterInvoke"] = "", ["neededFlags"] = "", ["occurOnEnter"] = true,
                ["oneUse"] = false, ["persistent"] = false } };
        void Accept(EntityData data) { AppleEverestSelectedProfileGuard.Entity(data.Name, data); passed++; }
        void Bad(EntityData data) => Reject(() => AppleEverestSelectedProfileGuard.Entity(data.Name, data), "outside the reviewed");
        Accept(Bubble());
        foreach (Action<EntityData> change in new Action<EntityData>[] {
            data => data.Width++, data => data.Height++, data => data.Nodes = [],
            data => data.Nodes = [data.Nodes[0]], data => data.Nodes = [data.Nodes[1], data.Nodes[0]],
            data => data.Nodes = [..data.Nodes, new(0, 0)], data => data.Nodes[1] = new(422, 159),
            data => data.Origin = new(1, 0), data => data.Values["once"] = true,
            data => data.Name = "CommunalHelper/UnsupportedBubble" })
        { var data = Bubble(); change(data); Bad(data); }
        foreach (bool once in new[] { false, true })
        { var sound = Sound(); sound.Values["oneUse"] = once; Accept(sound); }
        foreach (var pair in new (string Key, object Value)[] {
            ("persistent", true), ("delay", 0.1f), ("neededFlags", "gate"), ("flagsAfterInvoke", "gate"),
            ("audioEvents", "event:/unproved"), ("oneUse", "true"), ("occurOnEnter", false), ("unknown", false) })
        { var data = Sound(); data.Values[pair.Key] = pair.Value; Bad(data); }
        var missing = Sound(); missing.Values.Remove("persistent"); Bad(missing);
        var node = Sound(); node.Nodes = [new(0, 0)]; Bad(node);
        Console.WriteLine("PASS: " + passed + " K-N package, extraction and actual guard controls");
        return passed;
    }
}
