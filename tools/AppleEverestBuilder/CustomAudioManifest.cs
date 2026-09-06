using System.Text;

namespace AppleEverestBuilder;

/// <summary>
/// Closed, hash-pinned build-time support for ordinary Everest FMOD banks.
/// Discovery is restricted to reviewed releases. The generated product sees
/// only selected immutable banks and GUID records proven to occur in them.
/// </summary>
internal static class CustomAudioManifest
{
    private sealed record RegisteredBank(
        string Owner, string Version, string ArchiveSha256, string SourceSha256,
        string SourcePath, string BankSha256, string GuidSourcePath, string GuidSha256,
        Guid BankId, string BankPath, int SourceOrdinal, int ExpectedRecordCount,
        string[] RequiredEvents, bool LegacySingleBank = false);

    internal const string CompatibilityClass = "STATIC_CUSTOM_FMOD_BANK";
    internal const string BankSetCompatibilityClass = "STATIC_CUSTOM_FMOD_BANK_SET";
    internal const string Schema = "apple-everest-custom-audio-v1";
    internal const string LoadPolicy = "existing-celeste-studio-system-loadBankFile";
    internal const string SampleDataPolicy = "event-description-lazy";
    internal const string LifecyclePolicy = "system-owned-unloadAll;registry-invalidated-before-system-release";
    internal const int VanillaBankCount = 7;
    internal const int FirstCustomLoadOrdinal = 8;

    internal const string ChronoArchiveSha256 = "af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18";
    internal const string BankSourcePath = "Audio/ExpertContestHelper.bank";
    internal const string BankSha256 = "2607c358e66f4bce4e81fa3748eaffe1205dcfb57010be2e90ce40fabeb8d3ec";
    internal const string GuidSourcePath = "Audio/ExpertContestHelper.guids.txt";
    internal const string GuidSha256 = "db7f44d7ee1d79eb5efe734e595a6fb99937fd78e5cf9af91b0db6cabc4272a6";
    internal const string ExpectedBankPath = "bank:/ExpertContestHelper";
    internal const string SjBingBankSha256 = "772e3d4a41b463edcf529882f6d2f4de12384e891ae62a23220aa1379d89e159";
    internal static readonly Guid ExpectedBankId = new("f12a5c05-a79b-4ed0-bea9-81a1d2ecb986");

    private static readonly RegisteredBank[] Registry =
    [
        new("CollabUtils2", "1.13.4",
            "4bcea8a9011edb8b7d27b433c47f1f4a871b8f7a4dd7f2bffac67bb99c7f6ad5",
            "b987d25608874623453e2c75c441661b906d6fd907948699983c2f12ba14c88e",
            "Audio/SC2020_global_collectibles.bank", "81c19f256382718b1b8ea7bcab0944b6ed204e1eb7cf048fd699ba4f0d2a5777",
            "Audio/SC2020_global_collectibles.guids.txt", "33b184e9eff8213689ebd6f10d34b12926ecd6df3c68e45f58edfabede559293",
            new Guid("e0fed2a5-965a-4c2f-93dc-bef8d92874ba"), "bank:/SC2020_global_collectibles", 656, 35,
            ["event:/SC2020_heartShard_get", "event:/SC2020_heartShard_pulse", "event:/SC2020_silverBerry_get",
             "event:/SC2020_silverBerry_death", "event:/SC2020_rainbowBerry_get"]),
        new("HonlyHelper", "1.7.5",
            "6a2d0f04a5be3a9c9c3bf66ec7e93701398a64d5a0e72add5df682e860e7d08d",
            "cb8524306f28c0d04081dd63b5a3ccad4d7fecd5bc71970e06376606439298e3",
            "Audio/HonlyHelper.bank", "6164aa671dda3c9bee425545b7d556c74b9a0245e88f8d8c9acd652b90b62172",
            "Audio/HonlyHelper.guids.txt", "148c994feb915ce8d419030b5bb17aa068ae5681a0824a650726e5c885d520d7",
            new Guid("1b9737d7-462d-4ee3-8697-91f1ba21e925"), "bank:/HonlyHelper", 14, 9,
            ["event:/HonlyHelper/catsfx"]),
        new(StaticAotCompatibility.ChronoName, StaticAotCompatibility.ChronoVersion,
            ChronoArchiveSha256, StaticAotCompatibility.ChronoSourceSha256,
            BankSourcePath, BankSha256, GuidSourcePath, GuidSha256,
            ExpectedBankId, ExpectedBankPath, 14, 3,
            ["event:/ricky06/EC2023/horn", "event:/ricky06/zip_mover 2"], true),
        new("StrawberryJam2021AudioA", "1.0.4",
            "81e9cbc39b3a5525c93dfc5b24b675a833a8fb616e8a01cfc0838296f5e37e1d",
            "d4fc6c79afdb1a986317af52f331a7d1a404eedee79c2f2fa1a32664cc4d7330",
            "Audio/sj21_bingovergoogle.bank", SjBingBankSha256,
            "Audio/sj21_bingovergoogle.guids.txt",
            "88c1e1c6d54ffcfc9fbb1c4527cb9108e531ea3bc303dbfd73b8c7eeba0cf1ba",
            new Guid("f023b527-acd0-40a9-a9b3-87e9f686b81c"), "bank:/sj21_bingovergoogle", 18, 2,
            ["event:/sj21_bingovergoogle"]),
        new("StrawberryJam2021AudioA", "1.0.4",
            "81e9cbc39b3a5525c93dfc5b24b675a833a8fb616e8a01cfc0838296f5e37e1d",
            "d4fc6c79afdb1a986317af52f331a7d1a404eedee79c2f2fa1a32664cc4d7330",
            "Audio/sj21_shared.bank",
            "7620d1de4c32f1564b1206ac252af806b5b33c6a9f2b4624df582ece0d7f62e8",
            "Audio/sj21_shared.guids.txt",
            "500b8c536468e0ce4bab7c44c0f8c8fe932a5ef41ab68d2793683f8a21420555",
            new Guid("1068df52-9f57-4e6e-887c-c1d5a961d61d"), "bank:/sj21_shared", 64, 10,
            ["event:/sj21_levelselect"]),
        new("StrawberryJam2021AudioB", "1.0.0",
            "70b90f45709956a4d18bfbb5941836c534344a1cf0ab859a50430daa3b76ef42",
            "cc73019ea246b2742ac0cbfa639e6cda5a566fe05b91631bb70ff71669425751",
            "Audio/sj21_BegLobby.bank",
            "a5c45fe0ed77d048c4c9520bb2e1e58f9dbfd1d05dc2ccc19d5310fac0ac0ceb",
            "Audio/sj21_BegLobby.GUIDs.txt",
            "3a8ea6f4a1f7f3ec0ae16b19b7cec11c5a1479974e830d812896351fa3cac740",
            new Guid("827873b5-86b7-4e1b-9848-e04c83fb7ddf"), "bank:/sj21_BegLobby", 77, 3,
            ["event:/sj21_BegLobby"]),
        new("StrawberryJam2021", "1.0.12",
            "4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655",
            "d5e68237d8371fa26ed5d804578ce5b1841673ba380d7438fb4fafcd73899063",
            "Audio/sj21_jamjars.bank",
            "9e6bf27fc7f607e2ef5695b6b32ccf34abf1b7380ba13d66f3b49bc5b1613c1b",
            "Audio/sj21_jamjars.guids.txt",
            "034b1e3a27419c8332e4a81af1a773971858daca97609ea9855ddeb5b7bf34f7",
            new Guid("a8371196-9461-4ff6-8994-7032718615a7"), "bank:/sj21_jamjars", 27792, 6,
            ["event:/sj21_jamjar-blue"])
    ];

    internal static IReadOnlyList<CustomAudioBankPlan> Resolve(ModInput input, EverestYamlEntry metadata)
    {
        RegisteredBank[] selected = Registry.Where(item => item.Owner == metadata.Name).ToArray();
        if (selected.Length == 0) return [];
        if (selected.Any(item => item.Version != metadata.Version || item.SourceSha256 != input.SourceSha256))
            throw new InvalidDataException($"unregistered custom FMOD identity for {metadata.Name}: version={metadata.Version}; source={input.SourceSha256}");
        if (!File.Exists(input.SourcePath))
            throw new InvalidDataException($"{metadata.Name} custom FMOD support requires the exact public ZIP input");
        string archiveSha = Hashing.FileSha256(input.SourcePath);
        if (selected.Any(item => item.ArchiveSha256 != archiveSha))
            throw new InvalidDataException($"{metadata.Name} custom FMOD archive hash mismatch: {archiveSha}");

        List<CustomAudioBankPlan> result = [];
        foreach (RegisteredBank registered in selected.OrderBy(item => item.SourceOrdinal))
        {
            FileRecord bank = RequireFile(input, registered.SourcePath,
                $"{metadata.Name} required custom FMOD bank missing");
            if (bank.Sha256 != registered.BankSha256)
                throw new InvalidDataException($"{metadata.Name} custom FMOD bank hash mismatch: {bank.Sha256}");
            FileRecord guids = RequireFile(input, registered.GuidSourcePath,
                $"{metadata.Name} required custom FMOD GUID export missing");
            if (guids.Sha256 != registered.GuidSha256)
                throw new InvalidDataException($"{metadata.Name} custom FMOD GUID export hash mismatch: {guids.Sha256}");

            byte[] bankBytes = File.ReadAllBytes(Path.Combine(input.StagingRoot,
                registered.SourcePath.Replace('/', Path.DirectorySeparatorChar)));
            IReadOnlyList<CustomAudioGuidRecord> exported = ParseGuidTable(File.ReadAllBytes(Path.Combine(
                input.StagingRoot, registered.GuidSourcePath.Replace('/', Path.DirectorySeparatorChar))));
            CustomAudioGuidRecord[] records = exported.Where(record =>
                bankBytes.AsSpan().IndexOf(record.Id.ToByteArray()) >= 0).ToArray();
            if (records.Length != registered.ExpectedRecordCount)
                throw new InvalidDataException($"{metadata.Name} custom FMOD embedded GUID census drifted");
            CustomAudioGuidRecord[] banks = records.Where(record => record.Kind == "bank").ToArray();
            if (banks.Length != 1 || banks[0].Id != registered.BankId || banks[0].Path != registered.BankPath)
                throw new InvalidDataException($"{metadata.Name} custom FMOD bank identity mismatch");
            foreach (string requiredEvent in registered.RequiredEvents)
                if (records.SingleOrDefault(record => record.Kind == "event" && record.Path == requiredEvent) == null)
                    throw new InvalidDataException($"{metadata.Name} required custom FMOD event is not embedded: {requiredEvent}");

            string safeOwner = SafeOwner(metadata.Name);
            result.Add(new CustomAudioBankPlan(metadata.Name, metadata.Version, archiveSha, registered.SourcePath,
                registered.BankSha256, registered.GuidSourcePath, registered.GuidSha256, registered.BankId,
                registered.BankPath, $"AppleEverest/Mods/{safeOwner}/{registered.SourcePath}", records));
        }
        return result;
    }

    internal static bool IsLegacySingleBank(CustomAudioBankPlan plan) =>
        plan.Owner == StaticAotCompatibility.ChronoName && plan.BankId == ExpectedBankId;

    internal static IReadOnlyList<(CustomAudioBankPlan Plan, int Ordinal)> Order(
        IReadOnlyList<(CustomAudioBankPlan Plan, int ModuleOrdinal, int BankOrdinal)> plans)
    {
        // Chrono predates the SJ extension and stays first. The SJ root's
        // reduced static dependency plan intentionally omits its audio-only
        // packages, so restore the pinned desktop module sequence explicitly;
        // banks within a module retain ZipArchive entry order.
        int DesktopModuleOrder(CustomAudioBankPlan plan) => plan.Owner switch
        {
            StaticAotCompatibility.ChronoName => 0,
            "StrawberryJam2021AudioA" => 1,
            "StrawberryJam2021AudioB" => 2,
            "StrawberryJam2021" => 3,
            _ => 4
        };
        return plans.OrderBy(item => DesktopModuleOrder(item.Plan))
            .ThenBy(item => item.ModuleOrdinal)
            .ThenBy(item => item.BankOrdinal)
            .Select((item, index) => (item.Plan, FirstCustomLoadOrdinal + index)).ToArray();
    }

    internal static IReadOnlyList<CustomAudioGuidRecord> ParseGuidTable(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is <= 0 or > 256 * 1024)
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
        if (result.Count is <= 0 or > 4096)
            throw new InvalidDataException("custom FMOD GUID manifest entry count is invalid");
        return result;
    }

    internal static void ValidateGraph(IReadOnlyList<(CustomAudioBankPlan Plan, int Ordinal)> plans)
    {
        Dictionary<Guid, string> ids = [];
        Dictionary<string, Guid> paths = new(StringComparer.Ordinal);
        Dictionary<string, (Guid Id, string Hash)> logicalBanks = new(StringComparer.Ordinal);
        Dictionary<Guid, (string Path, string Hash)> bankIdentities = [];
        HashSet<int> ordinals = [];
        foreach ((CustomAudioBankPlan plan, int ordinal) in plans)
        {
            if (!ordinals.Add(ordinal))
                throw new InvalidDataException("custom FMOD load ordinal collision");
            if (logicalBanks.TryGetValue(plan.BankPath, out var logical) &&
                (logical.Id != plan.BankId || logical.Hash != plan.BankSha256))
                throw new InvalidDataException("custom FMOD logical bank path collision");
            logicalBanks[plan.BankPath] = (plan.BankId, plan.BankSha256);
            if (bankIdentities.TryGetValue(plan.BankId, out var identity) &&
                (identity.Path != plan.BankPath || identity.Hash != plan.BankSha256))
                throw new InvalidDataException("custom FMOD bank identity collision");
            bankIdentities[plan.BankId] = (plan.BankPath, plan.BankSha256);
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
        text.AppendLine(Schema).Append("class\t").AppendLine(plans.Count <= 1 ? CompatibilityClass : BankSetCompatibilityClass)
            .Append("load-policy\t").AppendLine(LoadPolicy)
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
