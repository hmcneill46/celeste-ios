using System.Text;

namespace AppleEverestBuilder;

/// <summary>
/// Closed, hash-pinned build-time support for ordinary Everest FMOD banks.
/// This is intentionally not a general bank scanner: only the exact reviewed
/// ChronoHelper 1.3.3 release is accepted by the initial compatibility class.
/// </summary>
internal static class CustomAudioManifest
{
    internal const string CompatibilityClass = "STATIC_CUSTOM_FMOD_BANK";
    internal const string Schema = "apple-everest-custom-audio-v1";
    internal const string LoadPolicy = "existing-celeste-studio-system-loadBankFile";
    internal const string SampleDataPolicy = "event-description-lazy";
    internal const string LifecyclePolicy = "system-owned-unloadAll;registry-invalidated-before-system-release";
    internal const string ChronoArchiveSha256 = "af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18";
    internal const string BankSourcePath = "Audio/ExpertContestHelper.bank";
    internal const string BankSha256 = "2607c358e66f4bce4e81fa3748eaffe1205dcfb57010be2e90ce40fabeb8d3ec";
    internal const string GuidSourcePath = "Audio/ExpertContestHelper.guids.txt";
    internal const string GuidSha256 = "db7f44d7ee1d79eb5efe734e595a6fb99937fd78e5cf9af91b0db6cabc4272a6";
    internal const string ExpectedBankPath = "bank:/ExpertContestHelper";
    internal static readonly Guid ExpectedBankId = new("f12a5c05-a79b-4ed0-bea9-81a1d2ecb986");

    internal static IReadOnlyList<CustomAudioBankPlan> Resolve(ModInput input, EverestYamlEntry metadata)
    {
        bool knownChronoBinary = metadata.Name == StaticAotCompatibility.ChronoName &&
            metadata.Version == StaticAotCompatibility.ChronoVersion &&
            metadata.DLL == StaticAotCompatibility.ChronoDllPath &&
            input.Files.Any(file => file.Path == StaticAotCompatibility.ChronoDllPath &&
                                    file.Sha256 == StaticAotCompatibility.ChronoDllSha256);
        string[] bankPaths = input.Files.Where(file => file.Path.EndsWith(".bank", StringComparison.OrdinalIgnoreCase))
            .Select(file => file.Path).Order(StringComparer.Ordinal).ToArray();

        if (!knownChronoBinary) return [];
        if (!File.Exists(input.SourcePath))
            throw new InvalidDataException("ChronoHelper STATIC_CUSTOM_FMOD_BANK requires the exact public ZIP input");
        string archiveSha = Hashing.FileSha256(input.SourcePath);
        if (archiveSha != ChronoArchiveSha256)
            throw new InvalidDataException($"ChronoHelper STATIC_CUSTOM_FMOD_BANK archive hash mismatch: {archiveSha}");
        if (input.SourceSha256 != StaticAotCompatibility.ChronoSourceSha256)
            throw new InvalidDataException("ChronoHelper STATIC_CUSTOM_FMOD_BANK logical source hash mismatch");
        if (!bankPaths.SequenceEqual([BankSourcePath], StringComparer.Ordinal))
            throw new InvalidDataException("ChronoHelper STATIC_CUSTOM_FMOD_BANK bank inventory mismatch");

        FileRecord bank = RequireFile(input, BankSourcePath,
            "ChronoHelper STATIC_CUSTOM_FMOD_BANK required bank missing");
        if (bank.Sha256 != BankSha256)
            throw new InvalidDataException($"ChronoHelper STATIC_CUSTOM_FMOD_BANK bank hash mismatch: {bank.Sha256}");
        FileRecord guids = RequireFile(input, GuidSourcePath,
            "ChronoHelper STATIC_CUSTOM_FMOD_BANK required GUID manifest missing");
        if (guids.Sha256 != GuidSha256)
            throw new InvalidDataException($"ChronoHelper STATIC_CUSTOM_FMOD_BANK GUID manifest hash mismatch: {guids.Sha256}");

        string guidPath = Path.Combine(input.StagingRoot, GuidSourcePath.Replace('/', Path.DirectorySeparatorChar));
        IReadOnlyList<CustomAudioGuidRecord> records = ParseGuidTable(File.ReadAllBytes(guidPath));
        CustomAudioGuidRecord[] banks = records.Where(record => record.Kind == "bank").ToArray();
        if (banks.Length != 1 || banks[0].Id != ExpectedBankId || banks[0].Path != ExpectedBankPath)
            throw new InvalidDataException("ChronoHelper STATIC_CUSTOM_FMOD_BANK bank identity mismatch");
        string safeOwner = SafeOwner(metadata.Name);
        return [new CustomAudioBankPlan(metadata.Name, metadata.Version, archiveSha, BankSourcePath,
            BankSha256, GuidSourcePath, GuidSha256, ExpectedBankId, ExpectedBankPath,
            $"AppleEverest/Mods/{safeOwner}/{BankSourcePath}", records)];
    }

    internal static IReadOnlyList<CustomAudioGuidRecord> ParseGuidTable(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is <= 0 or > 64 * 1024)
            throw new InvalidDataException("custom FMOD GUID manifest size is invalid");
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("custom FMOD GUID manifest is not valid UTF-8", exception);
        }
        if (text.IndexOf('\0') >= 0)
            throw new InvalidDataException("custom FMOD GUID manifest contains a NUL byte");

        List<CustomAudioGuidRecord> result = [];
        Dictionary<Guid, string> byId = [];
        Dictionary<string, Guid> byPath = new(StringComparer.Ordinal);
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (string raw in lines)
        {
            if (raw.Length == 0) continue;
            if (raw != raw.Trim())
                throw new InvalidDataException("custom FMOD GUID manifest has surrounding whitespace");
            int separator = raw.IndexOf(' ');
            if (separator <= 0 || separator == raw.Length - 1 || raw.IndexOf(' ', separator + 1) == separator + 1)
                throw new InvalidDataException("custom FMOD GUID manifest record is malformed");
            string guidText = raw[..separator];
            string path = raw[(separator + 1)..];
            if (guidText.Length != 38 || guidText[0] != '{' || guidText[^1] != '}' ||
                !Guid.TryParseExact(guidText, "B", out Guid id))
                throw new InvalidDataException("custom FMOD GUID manifest contains an invalid GUID");
            string kind = Kind(path);
            if (path.Length > 512 || path.IndexOfAny(['\r', '\n', '\0']) >= 0 || path.Contains("..", StringComparison.Ordinal))
                throw new InvalidDataException("custom FMOD GUID manifest path is invalid");
            if (byId.TryGetValue(id, out string? priorPath))
                throw new InvalidDataException(priorPath == path
                    ? "custom FMOD GUID manifest contains a duplicate GUID record"
                    : "custom FMOD GUID collision maps one GUID to incompatible paths");
            if (byPath.TryGetValue(path, out Guid priorId))
                throw new InvalidDataException(priorId == id
                    ? "custom FMOD GUID manifest contains a duplicate path record"
                    : "custom FMOD event collision maps one path to incompatible GUIDs");
            byId.Add(id, path);
            byPath.Add(path, id);
            result.Add(new CustomAudioGuidRecord(id, path, kind));
        }
        if (result.Count is <= 0 or > 1024)
            throw new InvalidDataException("custom FMOD GUID manifest entry count is invalid");
        return result;
    }

    internal static void ValidateGraph(IReadOnlyList<(CustomAudioBankPlan Plan, int Ordinal)> plans)
    {
        Dictionary<Guid, string> ids = [];
        Dictionary<string, Guid> paths = new(StringComparer.Ordinal);
        Dictionary<string, string> staged = new(StringComparer.Ordinal);
        Dictionary<Guid, string> banks = [];
        foreach ((CustomAudioBankPlan plan, _) in plans)
        {
            if (staged.TryGetValue(plan.StagedPath, out string? hash) && hash != plan.BankSha256)
                throw new InvalidDataException("custom FMOD logical bank path collision");
            staged[plan.StagedPath] = plan.BankSha256;
            if (banks.TryGetValue(plan.BankId, out string? bankPath) && bankPath != plan.BankPath)
                throw new InvalidDataException("custom FMOD bank identity collision");
            banks[plan.BankId] = plan.BankPath;
            foreach (CustomAudioGuidRecord record in plan.Guids)
            {
                if (ids.TryGetValue(record.Id, out string? existingPath) && existingPath != record.Path)
                    throw new InvalidDataException("custom FMOD GUID collision across selected modules");
                if (paths.TryGetValue(record.Path, out Guid existingId) && existingId != record.Id)
                    throw new InvalidDataException("custom FMOD path collision across selected modules");
                ids[record.Id] = record.Path;
                paths[record.Path] = record.Id;
            }
        }
    }

    internal static string CanonicalManifest(IReadOnlyList<(CustomAudioBankPlan Plan, int Ordinal)> plans)
    {
        StringBuilder text = new();
        text.AppendLine(Schema).Append("load-policy\t").AppendLine(LoadPolicy)
            .Append("sample-policy\t").AppendLine(SampleDataPolicy)
            .Append("lifecycle-policy\t").AppendLine(LifecyclePolicy);
        foreach ((CustomAudioBankPlan plan, int ordinal) in plans.OrderBy(item => item.Ordinal))
        {
            text.Append("bank\t").Append(ordinal).Append('\t').Append(plan.Owner).Append('\t').Append(plan.Version)
                .Append('\t').Append(plan.SourceArchiveSha256).Append('\t').Append(plan.SourcePath).Append('\t')
                .Append(plan.BankSha256).Append('\t').Append(plan.GuidSourcePath).Append('\t').Append(plan.GuidSha256)
                .Append('\t').Append(plan.BankId.ToString("D")).Append('\t').Append(plan.BankPath).Append('\t')
                .Append(plan.StagedPath).AppendLine();
            foreach (CustomAudioGuidRecord record in plan.Guids)
                text.Append("guid\t").Append(ordinal).Append('\t').Append(record.Kind).Append('\t')
                    .Append(record.Id.ToString("D")).Append('\t').Append(record.Path).AppendLine();
        }
        return text.ToString();
    }

    internal static string LogicalSet(IReadOnlyList<(CustomAudioBankPlan Plan, int Ordinal)> plans) =>
        string.Join("\n", plans.OrderBy(item => item.Ordinal).Select(item =>
            $"{item.Ordinal}\t{item.Plan.Owner}\t{item.Plan.StagedPath}\t{item.Plan.BankSha256}")) + "\n";

    private static string Kind(string path) => path.StartsWith("bank:/", StringComparison.Ordinal) ? "bank" :
        path.StartsWith("event:/", StringComparison.Ordinal) ? "event" :
        path.StartsWith("bus:/", StringComparison.Ordinal) ? "bus" :
        path.StartsWith("snapshot:/", StringComparison.Ordinal) ? "snapshot" :
        path.StartsWith("vca:/", StringComparison.Ordinal) ? "vca" :
        throw new InvalidDataException("custom FMOD GUID manifest contains an unsupported path class");

    private static FileRecord RequireFile(ModInput input, string path, string message) =>
        input.Files.SingleOrDefault(file => file.Path == path) ?? throw new InvalidDataException(message);

    private static string SafeOwner(string owner) =>
        new(owner.Select(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' or '.' ? ch : '_').ToArray());
}
