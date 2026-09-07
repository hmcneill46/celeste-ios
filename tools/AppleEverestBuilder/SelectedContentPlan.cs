using System.Text.Json;

namespace AppleEverestBuilder;

// Adds an enumerated, hash-bound content slice to each accepted semantic
// provider's regression assets. It cannot admit code or a new FMOD bank.
internal sealed class SelectedContentPlan
{
    public int SchemaVersion { get; set; }
    public string Id { get; set; } = "";
    public Package[] Packages { get; set; } = [];
    public Entry[] EverestContent { get; set; } = [];
    internal const string TerrainFallbackPath = "Graphics/Atlases/Gameplay/__fallback.png";
    internal const string TerrainFallbackSha256 = "f28617c37d0760b999caed3cde18935841017c29d7dab622fb668c224fca2ca8";
    internal const string ChapterTitlePath = "Graphics/Atlases/Gui/areaselect/title.png";
    internal const string ChapterTitleSha256 = "0839135f2baafbf652d652177e69456c07bc85343a6c93491e4e2a906034518d";
    internal sealed class Package
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string SourceLogicalSha256 { get; set; } = "";
        public string ArchiveSha256 { get; set; } = "";
        public Entry[] IncludedFiles { get; set; } = [];
    }
    internal sealed class Entry
    {
        public string Path { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public bool PreserveSourceBytes { get; set; }
    }

    internal static SelectedContentPlan Load(string path, IReadOnlyList<ResolvedMod> mods)
    {
        var plan = JsonSerializer.Deserialize<SelectedContentPlan>(File.ReadAllBytes(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("empty selected content plan");
        if (plan.SchemaVersion != 1 || string.IsNullOrWhiteSpace(plan.Id) || plan.Packages.Length == 0 ||
            plan.Packages.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != plan.Packages.Length)
            throw new InvalidDataException("invalid selected content plan");
        foreach (Package package in plan.Packages)
        {
            ResolvedMod mod = mods.SingleOrDefault(m => m.Metadata.Name == package.Name)
                ?? throw new InvalidDataException("selected content provider missing: " + package.Name);
            if (mod.Metadata.Version != package.Version || mod.Input.SourceSha256 != package.SourceLogicalSha256 ||
                !File.Exists(mod.Input.SourcePath) || Hashing.FileSha256(mod.Input.SourcePath) != package.ArchiveSha256)
                throw new InvalidDataException("selected content package identity mismatch: " + package.Name);
            if (package.IncludedFiles.Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != package.IncludedFiles.Length)
                throw new InvalidDataException("ambiguous selected content paths: " + package.Name);
            foreach (Entry entry in package.IncludedFiles)
            {
                FileRecord source = mod.Input.Files.SingleOrDefault(f => f.Path == entry.Path)
                    ?? throw new InvalidDataException("selected content file missing: " + entry.Path);
                if (source.Sha256 != entry.Sha256 || !IsContent(entry.Path))
                    throw new InvalidDataException("selected content file identity/type mismatch: " + entry.Path);
                if (entry.Path.EndsWith(".bank", StringComparison.OrdinalIgnoreCase) &&
                    !mod.CustomAudioBanks.Any(bank => bank.SourcePath == entry.Path && bank.BankSha256 == entry.Sha256))
                    throw new InvalidDataException("selected content cannot introduce an unregistered bank: " + entry.Path);
                if (entry.PreserveSourceBytes && !(entry.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                    entry.Path.StartsWith("Maps/", StringComparison.Ordinal) && entry.Path.EndsWith(".bin", StringComparison.Ordinal)))
                    throw new InvalidDataException("invalid original-byte content selection: " + entry.Path);
            }
        }
        // Only these two exact stable-1.6458.0 core assets are admitted.
        // The wider title graphic is the companion to _FixTitleLength.
        if (plan.EverestContent.Length > 2 ||
            plan.EverestContent.Select(entry => entry.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != plan.EverestContent.Length ||
            plan.EverestContent.Any(entry => entry.PreserveSourceBytes || !((
                entry.Path == TerrainFallbackPath && entry.Sha256 == TerrainFallbackSha256) || (
                entry.Path == ChapterTitlePath && entry.Sha256 == ChapterTitleSha256))))
            throw new InvalidDataException("unsupported selected Everest core content");
        return plan;
    }

    private static bool IsContent(string path) =>
        (path.StartsWith("Graphics/", StringComparison.Ordinal) || path.StartsWith("Dialog/", StringComparison.Ordinal) ||
         path.StartsWith("Maps/", StringComparison.Ordinal) || path.StartsWith("Audio/", StringComparison.Ordinal)) &&
        new[] { ".png", ".xml", ".txt", ".yaml", ".yml", ".bin", ".bank" }.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) &&
        !path.EndsWith(".guids.txt", StringComparison.OrdinalIgnoreCase);

    internal IEnumerable<string> Files(ResolvedMod mod) => mod.ContentFiles.Concat(
        Packages.SingleOrDefault(p => p.Name == mod.Metadata.Name)?.IncludedFiles.Select(f => f.Path) ?? [])
        .Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal);

    internal bool Preserve(string owner, string path) => Packages.SingleOrDefault(p => p.Name == owner)?
        .IncludedFiles.Any(f => f.Path == path && f.PreserveSourceBytes) == true;
}
