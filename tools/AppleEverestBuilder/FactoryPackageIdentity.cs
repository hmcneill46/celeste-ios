using System.Text.Json;

namespace AppleEverestBuilder;

internal static class FactoryPackageIdentity
{
    internal sealed record Dll(string Path, string Sha256);
    internal static Dll[] Verify(JsonElement pin, ModInput input, EverestYamlEntry metadata)
    {
        if (pin.ValueKind != JsonValueKind.Object || metadata.Name != pin.GetProperty("name").GetString())
            throw new InvalidDataException("exact provider ownership differs: " + metadata.Name);
        if (metadata.Version != pin.GetProperty("resolvedVersion").GetString())
            throw new InvalidDataException("exact provider version differs: " + metadata.Name);
        if (!File.Exists(input.SourcePath) || Hashing.FileSha256(input.SourcePath) != pin.GetProperty("zipSha256").GetString())
            throw new InvalidDataException("exact provider archive identity differs: " + metadata.Name);
        List<Dll> dlls = [];
        foreach (JsonElement dll in pin.GetProperty("distributedDlls").EnumerateArray())
        {
            string distributedName = dll.GetProperty("path").GetString()!;
            string[] matches = input.Files.Where(file => Path.GetFileName(file.Path) == distributedName).Select(file => file.Path).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("exact provider DLL path absent or ambiguous: " + metadata.Name + ":" + distributedName);
            string path = matches[0];
            string actual = Hashing.FileSha256(Path.Combine(input.StagingRoot, path));
            if (actual != dll.GetProperty("sha256").GetString())
                throw new InvalidDataException("exact provider DLL identity differs: " + metadata.Name + ":" + path);
            dlls.Add(new(path, actual));
        }
        return dlls.ToArray();
    }

    internal static void WriteControls(string repoRoot, string selectedArchive, string priorArchive, string output)
    {
        using JsonDocument graph = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(repoRoot,
            "apple-everest", "strawberry-jam-dependency-graph-stage25kc.json")));
        JsonElement pin = graph.RootElement.GetProperty("nodes").EnumerateArray().Single(node => node.GetProperty("name").GetString() == "FrostHelper");
        string temporary = Path.Combine(Path.GetTempPath(), "apple-everest-package-controls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            ModInput selected = SafeModIngestor.Ingest(selectedArchive, temporary, 0);
            EverestYamlEntry metadata = selected.Metadata.Single(entry => entry.Name == "FrostHelper");
            Dll[] exact = Verify(pin, selected, metadata);
            List<string> rejected = [];
            void Reject(string name, Action action, string reason)
            {
                bool failed = false;
                try { action(); }
                catch (InvalidDataException exception) when (exception.Message.Contains(reason, StringComparison.Ordinal)) { failed = true; }
                if (!failed) throw new InvalidDataException("package identity false-positive control: " + name);
                rejected.Add(name);
            }
            ModInput prior = SafeModIngestor.Ingest(priorArchive, temporary, 1);
            EverestYamlEntry old = prior.Metadata.Single(entry => entry.Name == "FrostHelper");
            if (old.Version != "1.79.1") throw new InvalidDataException("version control requires the actual separately accepted FrostHelper 1.79.1 archive");
            Reject("ACTUAL_FROST_1_79_1_CANNOT_SATISFY_SELECTED_1_80_1", () => Verify(pin, prior, old), "provider version differs");
            string version = metadata.Version;
            metadata.Version = "1.80.2";
            Reject("NEWER_METADATA_VERSION_CANNOT_INHERIT_ACCEPTANCE", () => Verify(pin, selected, metadata), "provider version differs");
            metadata.Version = version;
            string owner = metadata.Name;
            metadata.Name = "WrongOwner";
            Reject("WRONG_PROVIDER", () => Verify(pin, selected, metadata), "provider ownership differs");
            metadata.Name = owner;
            string archive = Path.Combine(temporary, "changed.zip");
            File.Copy(selectedArchive, archive);
            using (var zip = System.IO.Compression.ZipFile.Open(archive, System.IO.Compression.ZipArchiveMode.Update))
            using (StreamWriter writer = new(zip.CreateEntry("stage25kj-control.txt").Open())) writer.Write("changed archive identity");
            ModInput changed = SafeModIngestor.Ingest(archive, temporary, 2);
            Reject("CHANGED_ZIP_HASH", () => Verify(pin, changed, changed.Metadata.Single(entry => entry.Name == owner)), "provider archive identity differs");
            string dllPath = Path.Combine(selected.StagingRoot, exact[0].Path);
            byte[] dllBytes = File.ReadAllBytes(dllPath); dllBytes[^1] ^= 1; File.WriteAllBytes(dllPath, dllBytes);
            Reject("CHANGED_EXTRACTED_DLL_HASH", () => Verify(pin, selected, metadata), "provider DLL identity differs");
            File.WriteAllText(output, JsonSerializer.Serialize(new { schemaVersion = 1, exactSelectedVersion = metadata.Version,
                exactSelectedArchiveSha256 = Hashing.FileSha256(selectedArchive), exactSelectedDlls = exact,
                exactPublicInputAccepted = true, rejectedControls = rejected, sameValidatorAsProductionPreflight = true },
                new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }
}
