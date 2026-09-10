using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AppleEverestBuilder;
using Celeste.Mod;
using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length == 3 && args[0] == "--stage25kc-progression")
{
    ProgressionScaleAudit.Write(args[1], args[2]);
    Console.WriteLine("PASS: Stage 25K-C progression scale audit");
    return;
}

int passed = 0;
string repository = Path.GetFullPath(args.Length == 1 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../../"));
string temporary = Path.Combine(Path.GetTempPath(), "apple-everest-stage25b-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);

void Pass(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    passed++;
}

void Throws(Action action, string contains, string name)
{
    try { action(); }
    catch (Exception exception) when (exception.Message.Contains(contains, StringComparison.OrdinalIgnoreCase))
    {
        passed++;
        return;
    }
    throw new InvalidOperationException("FAIL: " + name);
}

string NewDirectory(string name)
{
    string result = Path.Combine(temporary, name);
    Directory.CreateDirectory(result);
    return result;
}

void Text(string root, string relative, string value)
{
    string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, value, new UTF8Encoding(false));
}

ResolvedMod Mod(string name, string version = "1.0.0", IEnumerable<(string Name, string Version)>? dependencies = null,
    IEnumerable<(string Name, string Version)>? optional = null, IEnumerable<(string Name, string Version)>? conflicts = null)
{
    EverestYamlEntry metadata = new() { Name = name, Version = version };
    foreach ((string item, string requirement) in dependencies ?? []) metadata.Dependencies.Add(new EverestDependency { Name = item, Version = requirement });
    foreach ((string item, string requirement) in optional ?? []) metadata.OptionalDependencies.Add(new EverestDependency { Name = item, Version = requirement });
    foreach ((string item, string requirement) in conflicts ?? []) metadata.Conflicts.Add(new EverestDependency { Name = item, Version = requirement });
    return new ResolvedMod
    {
        Metadata = metadata,
        Input = new ModInput { SourcePath = name, StagingRoot = temporary, SourceSha256 = name, Files = [], Metadata = [metadata] },
        Classification = CompatibilityClass.CONTENT_ONLY,
        Mechanisms = new SortedSet<string>(StringComparer.Ordinal),
        ManagedFiles = [], ContentFiles = [],
        ManagedDetourTargets = new SortedSet<string>(StringComparer.Ordinal),
        DirectManagedHooks = [],
        ModInteropRegistrations = [],
        FrozenIlTransforms = []
    };
}

FrozenIlTransformPlan FrozenPlan(string owner, string id, string target, int localOrdinal = 0) => new(
    id, owner, owner + ".dll", new string('a', 64), "IL.Fixture.Target", "Run", target, target,
    owner + ".Module", "Manipulate", true, localOrdinal, new string('b', 64), new string('c', 64),
    new string('d', 64), [], []);

ResolvedMod FrozenMod(string name, IReadOnlyList<FrozenIlTransformPlan> plans,
    IEnumerable<(string Name, string Version)>? dependencies = null)
{
    ResolvedMod ordinary = Mod(name, dependencies: dependencies);
    return new ResolvedMod
    {
        Metadata = ordinary.Metadata,
        Input = ordinary.Input,
        Classification = CompatibilityClass.STATIC_IL_EVENT_FREEZE,
        Mechanisms = new SortedSet<string>(["HookGen IL.*"], StringComparer.Ordinal),
        ManagedFiles = [], ContentFiles = [], Declaration = null, DeclaredAssemblyPath = null,
        ManagedDetourTargets = new SortedSet<string>(StringComparer.Ordinal), DirectManagedHooks = [],
        ModInteropRegistrations = [], FrozenIlTransforms = plans
    };
}

try
{
    passed += ModInteropTests.Run(repository, temporary);
    passed += ModuleDurabilityTests.Run(repository, temporary);
    passed += StrawberryJamStateTests.Run();
    passed += CoreSessionStateTests.Run();
    passed += CollabSaveStateTests.Run();
    passed += LevelSetProgressionTests.Run(repository, temporary);
    passed += CollabStaticTests.Run(repository, temporary);
    passed += SelectedCanaryContentTests.Run(repository, temporary);
    passed += SelectedCompositionTests.Run(temporary);
    passed += SelectedProfileGuardTests.Run(repository, temporary);
    passed += SelectedFactoryTypeClosureTests.Run();
    passed += SnasFlagGroupsTests.Run();
    passed += SnasVerificationControlsTests.Run(temporary, repository);
    passed += DialogFragmentParserTests.Run();

    EverestVersion required = EverestVersion.Parse("1.2.3.4");
    Pass(EverestVersion.Satisfies(required, EverestVersion.Parse("1.2.3.4")), "exact version");
    Pass(EverestVersion.Satisfies(required, EverestVersion.Parse("1.3.0")), "newer minor");
    Pass(!EverestVersion.Satisfies(required, EverestVersion.Parse("2.2.3.4")), "major mismatch");
    Pass(!EverestVersion.Satisfies(required, EverestVersion.Parse("1.2.2.9")), "older build");
    Pass(EverestVersion.Satisfies(required, EverestVersion.Parse("0.0.1")), "Everest dev version");
    Throws(() => EverestVersion.Parse("1.bad"), "invalid Everest version", "invalid version rejected");

    AppleEverestSettingRecord[] settingRecords =
    [
        new("ZetaHelper", "Enabled", 1),
        new("AlphaHelper", "Mode", 2)
    ];
    string encodedSettings = AppleEverestSettingsCodec.Encode(settingRecords);
    Pass(AppleEverestSettingsCodec.TryDecode(encodedSettings, out AppleEverestSettingRecord[] decodedSettings) &&
         decodedSettings.SequenceEqual(settingRecords.Reverse()), "module settings deterministic round trip");
    Pass(encodedSettings.StartsWith(AppleEverestSettingsCodec.Header + "\n", StringComparison.Ordinal) &&
         encodedSettings.IndexOf("AlphaHelper", StringComparison.Ordinal) < 0,
        "module settings use a versioned encoded namespace");
    Pass(!AppleEverestSettingsCodec.TryDecode(encodedSettings.Replace("\t2\n", "\tbad\n", StringComparison.Ordinal), out _),
        "module settings malformed value rejected");
    string duplicateSettings = AppleEverestSettingsCodec.Header + "\nQQ\tQg\t0\nQQ\tQg\t1\n";
    Pass(!AppleEverestSettingsCodec.TryDecode(duplicateSettings, out _), "module settings duplicate key rejected");
    Pass(!AppleEverestSettingsCodec.TryDecode(AppleEverestSettingsCodec.Header + "\n" +
         new string('A', AppleEverestSettingsCodec.MaximumBytes), out _), "module settings oversized payload rejected");

    ResolvedMod graphA = Mod("A");
    ResolvedMod graphB = Mod("B", dependencies: [("A", "1.0.0")]);
    Pass(EverestGraphResolver.Resolve([graphB, graphA]).Select(mod => mod.Metadata.Name).SequenceEqual(["A", "B"]), "dependency order");
    Pass(EverestGraphResolver.Resolve([Mod("C", optional: [("Missing", "1.0.0")])]).Count == 1, "missing optional dependency");
    Pass(EverestGraphResolver.Resolve([Mod("A"), Mod("B", optional: [("A", "1.0.0")])]).Select(mod => mod.Metadata.Name).SequenceEqual(["A", "B"]), "present optional order");
    Throws(() => EverestGraphResolver.Resolve([Mod("B", dependencies: [("Missing", "1.0.0")])]), "missing dependency", "missing required dependency");
    Throws(() => EverestGraphResolver.Resolve([Mod("A"), Mod("A")]), "duplicate", "duplicate identity");
    Throws(() => EverestGraphResolver.Resolve([Mod("A", dependencies: [("B", "1.0.0")]), Mod("B", dependencies: [("A", "1.0.0")])]), "cycle", "cycle");
    Throws(() => EverestGraphResolver.Resolve([Mod("A", "1.0.0"), Mod("B", dependencies: [("A", "2.0.0")])]), "incompatible", "version incompatibility");
    Throws(() => EverestGraphResolver.Resolve([Mod("A"), Mod("B", conflicts: [("A", "1.0.0")])]), "conflict", "conflict");
    Pass(EverestGraphResolver.Resolve([graphB, graphA]).Select(mod => mod.Metadata.Name).SequenceEqual(
        EverestGraphResolver.Resolve([graphA, graphB]).Select(mod => mod.Metadata.Name)), "stable graph order");
    const string sharedFrozenTarget = "System.Int32 Fixture.Target::Run(System.Int32)";
    ResolvedMod frozenA = FrozenMod("FrozenA", [FrozenPlan("FrozenA", "A", sharedFrozenTarget)]);
    ResolvedMod frozenB = FrozenMod("FrozenB", [FrozenPlan("FrozenB", "B", sharedFrozenTarget)],
        [("FrozenA", "1.0.0")]);
    FrozenIlTransformPlan[] composed = ClosureGenerator.ComposeFrozenIlTransforms(
        EverestGraphResolver.Resolve([frozenB, frozenA])).ToArray();
    Pass(composed.Select(plan => plan.Owner).SequenceEqual(["FrozenA", "FrozenB"]) &&
         composed.Select(plan => plan.RegistrationOrdinal).SequenceEqual([0, 1]),
        "frozen IL sequence follows resolved module registration order");
    FrozenIlTransformPlan[] independent = ClosureGenerator.ComposeFrozenIlTransforms([
        FrozenMod("FrozenA", [FrozenPlan("FrozenA", "A", sharedFrozenTarget),
            FrozenPlan("FrozenA", "Other", "System.Void Fixture.Other::Run()")]),
        FrozenMod("FrozenB", [FrozenPlan("FrozenB", "B", sharedFrozenTarget)])]).ToArray();
    Pass(independent.Select(plan => plan.RegistrationOrdinal).SequenceEqual([0, 0, 1]),
        "frozen IL registration ordinals are target-local");

    const string orderingTarget = "System.Int32 Fixture.Target::Run(System.Int32)";
    StaticConfiguredDetourNode[] priorityNodes =
    [
        new("low", orderingTarget, "MANAGED_HOOK", new("low", -10, [], []), 0, 0),
        new("ordinary", orderingTarget, "MANAGED_HOOK", null, 1, 0),
        new("high", orderingTarget, "MANAGED_HOOK", new("high", 10, [], []), 2, 0)
    ];
    StaticConfiguredDetourSequence prioritySequence = ConfiguredDetourOrdering.Resolve(orderingTarget, priorityNodes);
    Pass(prioritySequence.ConfiguredExecutionOrder.SequenceEqual(["high", "low"]) &&
         prioritySequence.ManagedDispatcherOrder.SequenceEqual(["ordinary", "low", "high"]) &&
         prioritySequence.IlCompositionOrder.SequenceEqual(["high", "low", "ordinary"]),
        "configured priority, managed wrapping and IL composition remain distinct");
    Pass(StaticConfiguredDetourCompatibility.ConfiguredOrdinalBase > 1_000_000_000L,
        "configured managed ordinals occupy a disjoint fixed range");
    Pass(ConfiguredDetourOrdering.Resolve(orderingTarget, priorityNodes.Reverse()).PlanSha256 ==
         prioritySequence.PlanSha256,
        "configured plan is deterministic across reversed input enumeration");

    StaticConfiguredDetourSequence beforeSequence = ConfiguredDetourOrdering.Resolve(orderingTarget,
    [
        new("before", orderingTarget, "MANAGED_HOOK", new("before", -10, ["after"], []), 0, 0),
        new("after", orderingTarget, "MANAGED_HOOK", new("after", 10, [], []), 1, 0)
    ]);
    Pass(beforeSequence.ConfiguredExecutionOrder.SequenceEqual(["before", "after"]) &&
         beforeSequence.ManagedDispatcherOrder.SequenceEqual(["after", "before"]),
        "Before constraint overrides priority with exact wrapper reversal");

    StaticConfiguredDetourSequence afterSequence = ConfiguredDetourOrdering.Resolve(orderingTarget,
    [
        new("first", orderingTarget, "IL_EVENT", new("first", 10, [], ["second"]), 0, 0),
        new("second", orderingTarget, "IL_EVENT", new("second", -10, [], []), 1, 0)
    ]);
    Pass(afterSequence.ConfiguredExecutionOrder.SequenceEqual(["second", "first"]),
        "After constraint overrides priority");

    StaticDetourConfig beforeAll = ConfiguredDetourOrdering.NormalizeLegacy("legacy-before-all", 42,
        ["*"], [], 7, forIlHook: false);
    StaticDetourConfig afterAll = ConfiguredDetourOrdering.NormalizeLegacy("legacy-after-all", 42,
        [], ["*"], 8, forIlHook: false);
    StaticDetourConfig beforeAllIl = ConfiguredDetourOrdering.NormalizeLegacy("legacy-il-before-all", 42,
        ["*"], [], 9, forIlHook: true);
    Pass(beforeAll.Priority == int.MinValue && afterAll.Priority == int.MaxValue &&
         beforeAll.Before.Length == 0 && beforeAll.After.Length == 0,
        "legacy wildcard priorities match pinned Everest adapter");
    Pass(beforeAllIl.Priority == int.MaxValue && beforeAllIl.SubPriority == int.MaxValue - 9,
        "legacy IL wildcard and global registration order are reversed exactly");

    Throws(() => ConfiguredDetourOrdering.Resolve(orderingTarget,
    [
        new("cycle-a", orderingTarget, "IL_EVENT", new("cycle-a", null, ["cycle-b"], []), 0, 0),
        new("cycle-b", orderingTarget, "IL_EVENT", new("cycle-b", null, ["cycle-a"], []), 1, 0)
    ]), "CYCLE", "configured ordering cycle rejected before AOT");
    Throws(() => ConfiguredDetourOrdering.Resolve(orderingTarget,
    [
        new("dynamic", orderingTarget, "IL_EVENT", new("dynamic", null, [], []), 0, 0,
            "GAMEPLAY_SCOPED_MUTABLE")
    ]), "DYNAMIC_CONFIG_LIFETIME_DEFERRED", "dynamic configured lifetime remains rejected");
    Throws(() => ConfiguredDetourOrdering.Resolve(orderingTarget,
    [
        new("same-a", orderingTarget, "IL_EVENT", new("same", 1, [], []), 0, 0),
        new("same-b", orderingTarget, "IL_EVENT", new("same", 2, [], []), 1, 0)
    ]), "DUPLICATE_INCOMPATIBLE_ID", "incompatible duplicate config identity rejected");
    ResolvedMod cpopGraph = Mod("CpopHelper", "1.3.0", [("Everest", "1.3471.0")]);
    ResolvedMod quizGraph = Mod("QuizSample", "0.0.1", [("CpopHelper", "1.0.0"), ("Everest", "1.3761.0")]);
    Pass(EverestGraphResolver.Resolve([quizGraph, cpopGraph]).Select(mod => mod.Metadata.Name)
        .SequenceEqual(["CpopHelper", "QuizSample"]), "real map-to-helper dependency order");
    ResolvedMod sharedDj = Mod("DJMapHelper", "1.13.4");
    ResolvedMod littleEpicGraph = Mod("LittleEpic", dependencies: [("DJMapHelper", "1.13.4")]);
    ResolvedMod spaceJamGraph = Mod("SpaceJam", dependencies: [("DJMapHelper", "1.8.23")]);
    Pass(EverestGraphResolver.Resolve([spaceJamGraph, sharedDj, littleEpicGraph]).Select(mod => mod.Metadata.Name)
        .SequenceEqual(["DJMapHelper", "LittleEpic", "SpaceJam"]),
        "two real maps share one helper satisfying compatible minimums");
    Throws(() => EverestGraphResolver.Resolve([spaceJamGraph, Mod("DJMapHelper", "1.8.23"),
            Mod("DJMapHelper", "1.13.4"), littleEpicGraph]), "duplicate",
        "two exact helper archives cannot win by input order");
    Throws(() => EverestGraphResolver.Resolve([quizGraph]), "missing dependency", "real map missing helper rejected");
    Throws(() => EverestGraphResolver.Resolve([quizGraph, Mod("CpopHelper", "0.9.0")]), "incompatible",
        "real map wrong helper version rejected");

    string content = NewDirectory("content");
    Text(content, "everest.yaml", "- Name: MultiA\n  Version: 1.0.0\n- Name: MultiB\n  Version: 2.0.0\n");
    Text(content, "Content/test.txt", "owned\n");
    ModInput multi = SafeModIngestor.Ingest(content, NewDirectory("stage-multi"), 0);
    Pass(multi.Metadata.Count == 2, "multi-entry YAML");
    Pass(multi.Metadata[1].Name == "MultiB", "YAML fields");
    Pass(multi.Files.Any(file => file.Path == "Content/test.txt"), "directory inventory");

    string zipPath = Path.Combine(temporary, "safe.zip");
    using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
    {
        using (StreamWriter yaml = new(zip.CreateEntry("everest.yaml").Open()))
            yaml.Write("- Name: ZipSafe\n  Version: 1.0.0\n");
        using (StreamWriter data = new(zip.CreateEntry("Content/value.txt").Open()))
            data.Write("safe");
    }
    Pass(SafeModIngestor.Ingest(zipPath, NewDirectory("stage-safe-zip"), 0).Metadata[0].Name == "ZipSafe", "safe ZIP");

    string customAudio = NewDirectory("custom-audio");
    Text(customAudio, "everest.yaml", "- Name: CustomAudio\n  Version: 1.0.0\n");
    Text(customAudio, "Audio/custom.bank", "not-a-real-bank");
    ModInput customAudioInput = SafeModIngestor.Ingest(customAudio, NewDirectory("stage-custom-audio"), 0);
    Pass(CompatibilityAnalyzer.Audit(customAudioInput, customAudioInput.Metadata[0]).Classification ==
         CompatibilityClass.CUSTOM_AUDIO_UNSUPPORTED, "custom FMOD bank audit is explicit");
    Throws(() => CompatibilityAnalyzer.Analyze(customAudioInput, customAudioInput.Metadata[0]),
        "CUSTOM_AUDIO_UNSUPPORTED", "custom FMOD bank fails closed before AOT");

    const string hornGuid = "{33eab85e-7e13-417e-ab3b-a0b7c8caa6b2} event:/ricky06/EC2023/horn";
    const string moverGuid = "{22f6b410-423f-4c15-a6b5-0523b16ab5fd} event:/ricky06/zip_mover 2";
    const string bankGuid = "{f12a5c05-a79b-4ed0-bea9-81a1d2ecb986} bank:/ExpertContestHelper";
    IReadOnlyList<CustomAudioGuidRecord> exactGuids = CustomAudioManifest.ParseGuidTable(
        Encoding.UTF8.GetBytes(hornGuid + "\n" + moverGuid + "\n" + bankGuid + "\n"));
    Pass(exactGuids.Count == 3 && exactGuids.Count(record => record.Kind == "event") == 2 &&
         exactGuids.Single(record => record.Kind == "bank").Id == CustomAudioManifest.ExpectedBankId,
        "custom FMOD GUID table parses completely");
    Pass(exactGuids.Any(record => record.Path == "event:/ricky06/EC2023/horn") &&
         exactGuids.Any(record => record.Path == "event:/ricky06/zip_mover 2"),
        "custom FMOD event paths are exact");
    Throws(() => CustomAudioManifest.ParseGuidTable(Encoding.UTF8.GetBytes("malformed\n")),
        "malformed", "malformed custom FMOD GUID rejected");
    Throws(() => CustomAudioManifest.ParseGuidTable(Encoding.UTF8.GetBytes(hornGuid + "\n" + hornGuid + "\n")),
        "duplicate", "duplicate custom FMOD GUID rejected");
    Throws(() => CustomAudioManifest.ParseGuidTable(Encoding.UTF8.GetBytes(
        hornGuid + "\n{43eab85e-7e13-417e-ab3b-a0b7c8caa6b2} event:/ricky06/EC2023/horn\n")),
        "collision", "duplicate custom FMOD event path rejected");
    Throws(() => CustomAudioManifest.ParseGuidTable(Encoding.UTF8.GetBytes(
        "{43eab85e-7e13-417e-ab3b-a0b7c8caa6b2} parameter:/unsupported\n")),
        "unsupported", "unsupported custom FMOD GUID class rejected");
    CustomAudioBankPlan exactBank = new("ChronoHelper", "1.3.3", CustomAudioManifest.ChronoArchiveSha256,
        CustomAudioManifest.BankSourcePath, CustomAudioManifest.BankSha256, CustomAudioManifest.GuidSourcePath,
        CustomAudioManifest.GuidSha256, CustomAudioManifest.ExpectedBankId, CustomAudioManifest.ExpectedBankPath,
        "AppleEverest/Mods/ChronoHelper/Audio/ExpertContestHelper.bank", exactGuids);
    string manifestA = CustomAudioManifest.CanonicalManifest([(exactBank, 0)]);
    string manifestB = CustomAudioManifest.CanonicalManifest([(exactBank, 0)]);
    Pass(manifestA == manifestB && manifestA.Contains(CustomAudioManifest.BankSha256, StringComparison.Ordinal) &&
         manifestA.Contains("event:/ricky06/EC2023/horn", StringComparison.Ordinal),
        "custom FMOD manifest is deterministic and hash complete");
    Pass(CustomAudioManifest.LogicalSet([(exactBank, 0)]).Contains(CustomAudioManifest.BankSha256, StringComparison.Ordinal),
        "custom FMOD bank bytes participate in closure identity");
    CustomAudioBankPlan conflictingBank = exactBank with
    {
        StagedPath = "AppleEverest/Mods/Other/Audio/different.bank",
        BankPath = "bank:/Different",
        BankSha256 = new string('f', 64)
    };
    Throws(() => CustomAudioManifest.ValidateGraph([(exactBank, 0), (conflictingBank, 1)]),
        "bank identity collision", "incompatible custom FMOD bank identity rejected");
    CustomAudioBankPlan conflictingGuid = exactBank with
    {
        StagedPath = "AppleEverest/Mods/Other/Audio/other.bank",
        BankId = Guid.NewGuid(),
        BankPath = "bank:/Other",
        Guids = [new CustomAudioGuidRecord(exactGuids[0].Id, "event:/other", "event")]
    };
    Throws(() => CustomAudioManifest.ValidateGraph([(exactBank, 0), (conflictingGuid, 1)]),
        "GUID collision", "cross-module custom FMOD GUID collision rejected");
    CustomAudioBankPlan conflictingLogicalPath = exactBank with
    {
        StagedPath = "AppleEverest/Mods/Other/Audio/logical.bank",
        BankId = Guid.NewGuid(),
        BankSha256 = new string('e', 64),
        Guids = [new CustomAudioGuidRecord(Guid.NewGuid(), exactBank.BankPath, "bank")]
    };
    Throws(() => CustomAudioManifest.ValidateGraph([(exactBank, 8), (conflictingLogicalPath, 9)]),
        "logical bank path collision", "same logical bank path with different identity is rejected");
    CustomAudioBankPlan conflictingEventPath = exactBank with
    {
        StagedPath = "AppleEverest/Mods/Other/Audio/event-path.bank",
        BankId = Guid.NewGuid(),
        BankPath = "bank:/EventPath",
        BankSha256 = new string('d', 64),
        Guids = [new CustomAudioGuidRecord(Guid.NewGuid(), exactGuids[0].Path, "event")]
    };
    Throws(() => CustomAudioManifest.ValidateGraph([(exactBank, 8), (conflictingEventPath, 9)]),
        "path collision", "same event path with different GUID is rejected");
    CustomAudioBankPlan compatibleSharedBus = exactBank with
    {
        StagedPath = "AppleEverest/Mods/Other/Audio/shared-bus.bank",
        BankId = Guid.NewGuid(),
        BankPath = "bank:/SharedBus",
        BankSha256 = new string('c', 64),
        Guids = [new CustomAudioGuidRecord(Guid.Parse("7429d822-1e68-4251-9907-6d4e8d14a82e"),
            "bus:/music/tunes/mains", "bus")]
    };
    CustomAudioBankPlan compatibleSharedBus2 = compatibleSharedBus with
    {
        StagedPath = "AppleEverest/Mods/Other2/Audio/shared-bus-2.bank",
        BankId = Guid.NewGuid(),
        BankPath = "bank:/SharedBus2",
        BankSha256 = new string('b', 64)
    };
    CustomAudioManifest.ValidateGraph([(compatibleSharedBus, 8), (compatibleSharedBus2, 9)]);
    Pass(true, "compatible shared bus GUID/path is accepted");
    IReadOnlyList<(CustomAudioBankPlan Plan, int Ordinal)> exactOrder = CustomAudioManifest.Order([
        (exactBank with { Owner = "StrawberryJam2021", SourcePath = "Audio/sj21_jamjars.bank" }, 0, 0),
        (exactBank with { Owner = "StrawberryJam2021AudioB", SourcePath = "Audio/sj21_BegLobby.bank" }, 1, 0),
        (exactBank with { Owner = "StrawberryJam2021AudioA", SourcePath = "Audio/sj21_shared.bank" }, 2, 1),
        (exactBank with { Owner = "StrawberryJam2021AudioA", SourcePath = "Audio/sj21_bingovergoogle.bank" }, 2, 0),
        (exactBank, 3, 0)
    ]);
    Pass(exactOrder.Select(item => (item.Plan.Owner, item.Plan.SourcePath, item.Ordinal)).SequenceEqual([
        ("ChronoHelper", "Audio/ExpertContestHelper.bank", 8),
        ("StrawberryJam2021AudioA", "Audio/sj21_bingovergoogle.bank", 9),
        ("StrawberryJam2021AudioA", "Audio/sj21_shared.bank", 10),
        ("StrawberryJam2021AudioB", "Audio/sj21_BegLobby.bank", 11),
        ("StrawberryJam2021", "Audio/sj21_jamjars.bank", 12)
    ]), "custom FMOD order matches pinned desktop module and archive registration order");
    var expandedOrder = CustomAudioManifest.Order([
        (exactBank with { Owner = "HonlyHelper", SourcePath = "Audio/HonlyHelper.bank" }, 7, 14),
        (exactBank with { Owner = "StrawberryJam2021AudioB", SourcePath = "Audio/sj21_BegLobby.bank" }, 2, 77),
        (exactBank with { Owner = "StrawberryJam2021", SourcePath = "Audio/sj21_jamjars.bank" }, 0, 27792),
        (exactBank with { Owner = "StrawberryJam2021AudioA", SourcePath = "Audio/sj21_shared.bank" }, 3, 64),
        (exactBank with { Owner = "CollabUtils2", SourcePath = "Audio/SC2020_global_collectibles.bank" }, 1, 656),
        (exactBank with { Owner = "StrawberryJam2021AudioB", SourcePath = "Audio/sj21_snas.bank" }, 2, 63),
        (exactBank with { Owner = "StrawberryJam2021AudioA", SourcePath = "Audio/sj21_bingovergoogle.bank" }, 3, 18),
        (exactBank, 5, 14)
    ]);
    Pass(expandedOrder.Select(item => (item.Plan.SourcePath, item.Ordinal)).SequenceEqual([
        ("Audio/ExpertContestHelper.bank", 8), ("Audio/sj21_bingovergoogle.bank", 9),
        ("Audio/sj21_shared.bank", 10), ("Audio/sj21_snas.bank", 11),
        ("Audio/sj21_BegLobby.bank", 12), ("Audio/sj21_jamjars.bank", 13),
        ("Audio/SC2020_global_collectibles.bank", 14), ("Audio/HonlyHelper.bank", 15)
    ]), "K-N inserts only snas at ordinal 11 and preserves all old relative bank order");
    Pass(CustomAudioManifest.Schema == "apple-everest-custom-audio-v1" &&
         CustomAudioManifest.LoadPolicy.EndsWith("loadBankFile", StringComparison.Ordinal),
        "custom FMOD compatibility policy is locked");
    AppleEverestCustomAudioLifecycle audioLifecycle = new();
    object studioSystemA = new();
    object studioSystemB = new();
    Pass(audioLifecycle.BeginLoad(studioSystemA, 1), "custom FMOD lifecycle initializes once");
    audioLifecycle.CompleteLoad(studioSystemA, 1);
    Pass(!audioLifecycle.BeginLoad(studioSystemA, 1) && audioLifecycle.LoadedBankCount == 1,
        "custom FMOD lifecycle duplicate initialize is idempotent");
    audioLifecycle.SoftReload(studioSystemA);
    Pass(audioLifecycle.State == AppleEverestCustomBankState.Loaded,
        "custom FMOD lifecycle survives soft reload without duplicate state");
    Throws(() => audioLifecycle.BeginLoad(studioSystemB, 1), "live Studio System",
        "custom FMOD lifecycle rejects a second Studio System");
    Pass(audioLifecycle.BeforeSystemUnload(studioSystemA) == 1 &&
         audioLifecycle.State == AppleEverestCustomBankState.Unloaded,
        "custom FMOD lifecycle shuts down once");
    Pass(audioLifecycle.BeginLoad(studioSystemB, 1), "custom FMOD lifecycle reinitializes after shutdown");
    audioLifecycle.FailLoad(studioSystemB);
    Pass(audioLifecycle.State == AppleEverestCustomBankState.NotLoaded && audioLifecycle.LoadedBankCount == 0,
        "custom FMOD lifecycle failed load leaves no duplicate state");

    string traversal = Path.Combine(temporary, "traversal.zip");
    using (ZipArchive zip = ZipFile.Open(traversal, ZipArchiveMode.Create)) zip.CreateEntry("../escape.txt");
    Throws(() => SafeModIngestor.Ingest(traversal, NewDirectory("stage-traversal"), 0), "unsafe", "ZIP traversal");
    string absolute = Path.Combine(temporary, "absolute.zip");
    using (ZipArchive zip = ZipFile.Open(absolute, ZipArchiveMode.Create)) zip.CreateEntry("/absolute.txt");
    Throws(() => SafeModIngestor.Ingest(absolute, NewDirectory("stage-absolute"), 0), "absolute", "ZIP absolute path");
    string duplicate = Path.Combine(temporary, "duplicate.zip");
    using (ZipArchive zip = ZipFile.Open(duplicate, ZipArchiveMode.Create)) { zip.CreateEntry("A.txt"); zip.CreateEntry("a.txt"); }
    Throws(() => SafeModIngestor.Ingest(duplicate, NewDirectory("stage-duplicate"), 0), "duplicate", "ZIP duplicate path");
    string linked = Path.Combine(temporary, "linked.zip");
    using (ZipArchive zip = ZipFile.Open(linked, ZipArchiveMode.Create)) { ZipArchiveEntry link = zip.CreateEntry("link"); link.ExternalAttributes = 0xA000; }
    Throws(() => SafeModIngestor.Ingest(linked, NewDirectory("stage-link"), 0), "links", "ZIP link");

    CompatibilityClass AnalyzeSource(string name, string source)
    {
        string root = NewDirectory("analyze-" + name);
        Text(root, "everest.yaml", $"- Name: {name}\n  Version: 1.0.0\n  DLL: Code.cs\n");
        Text(root, "Code.cs", source);
        Text(root, "apple-static.json", $"{{\"schemaVersion\":1,\"moduleType\":\"Tests.{name}Module\",\"trackedEntityTypes\":[]}}");
        ModInput input = SafeModIngestor.Ingest(root, NewDirectory("stage-" + name), 0);
        return CompatibilityAnalyzer.Analyze(input, input.Metadata[0]).Classification;
    }
    Pass(AnalyzeSource("Static", "class Static {}") == CompatibilityClass.STATIC_MODULE, "static source class");
    Pass(AnalyzeSource("Event", "// Everest.Events.Level\nclass Event {}") == CompatibilityClass.NORMAL_EVENT, "ordinary event class");
    Pass(AnalyzeSource("OnHook", "// On.Celeste.Dialog.Clean += handler; On.Celeste.Dialog.orig_Clean orig\nclass Hook {}") == CompatibilityClass.ON_HOOK_SUPPORTED, "supported On hook");
    Pass(AnalyzeSource("OtherOn", "// On.Celeste.Level.LoadLevel += handler\nclass Hook {}") == CompatibilityClass.ON_HOOK_SUPPORTED,
        "catalogued On target accepted without analyzer special case");
    foreach ((string name, string source, string expected) in new[]
    {
        ("IL", "// IL.Celeste.Player.Update", "IL_HOOK_DEFERRED"),
        ("Direct", "// new Hook(target, hook)", "DIRECT_HOOK_DEFERRED"),
        ("Native", "// NativeDetour", "NATIVE_UNSUPPORTED"),
        ("PInvoke", "// DllImport", "NATIVE_UNSUPPORTED"),
        ("Lua", "// NLua", "LUA_UNSUPPORTED"),
        ("Dynamic", "// Assembly.Load(bytes)", "DYNAMIC_CODE_UNSUPPORTED"),
        ("Emit", "// DynamicMethod", "DYNAMIC_CODE_UNSUPPORTED"),
        ("Process", "// Process.Start", "PLATFORM_UNSUPPORTED"),
        ("Watcher", "// FileSystemWatcher", "PLATFORM_UNSUPPORTED")
    }) Throws(() => AnalyzeSource(name, source + "\nclass Test {}"), expected, name + " analyzer rejection");

    string BinaryFixture(string name, string hookNamespace, string hookType, string? hookEvent = null)
    {
        string root = NewDirectory("binary-" + name);
        Text(root, "everest.yaml", $"- Name: {name}\n  Version: 1.0.0\n  DLL: Code/{name}.dll\n  Dependencies:\n    - Name: Everest\n      Version: 1.6418.0\n");
        string path = Path.Combine(root, "Code", name + ".dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition(name, new Version(1, 0, 0, 0)), name, ModuleKind.Dll);
        AssemblyNameReference celeste = new("Celeste", new Version(1, 0, 0, 0));
        AssemblyNameReference hooks = new("MMHOOK_Celeste", new Version(0, 0, 0, 0));
        AssemblyNameReference xnaFacade = new("Microsoft.Xna.Framework", new Version(4, 0, 0, 0))
        {
            PublicKeyToken = [0x84, 0x2c, 0xf8, 0xbe, 0x1d, 0xe5, 0x05, 0x53]
        };
        assembly.MainModule.AssemblyReferences.Add(celeste);
        assembly.MainModule.AssemblyReferences.Add(hooks);
        assembly.MainModule.AssemblyReferences.Add(xnaFacade);
        TypeDefinition module = new("Fixture", name + "Module", TypeAttributes.Public | TypeAttributes.Sealed,
            new TypeReference("Celeste.Mod", "EverestModule", assembly.MainModule, celeste));
        MethodDefinition constructor = new(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            assembly.MainModule.TypeSystem.Void);
        if (hookEvent != null)
        {
            TypeReference hookTypeReference = new(hookNamespace, hookType, assembly.MainModule, hooks);
            MethodReference add = new("add_" + hookEvent, assembly.MainModule.TypeSystem.Void, hookTypeReference)
            {
                HasThis = false
            };
            add.Parameters.Add(new ParameterDefinition(new TypeReference(hookNamespace + "." + hookType, "hook_" + hookEvent,
                assembly.MainModule, hooks)));
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, add));
        }
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        module.Methods.Add(constructor);
        module.Fields.Add(new FieldDefinition("HookRoot", FieldAttributes.Public | FieldAttributes.Static,
            new TypeReference(hookNamespace, hookType, assembly.MainModule, hooks)));
        module.Fields.Add(new FieldDefinition("LegacyFnaVector", FieldAttributes.Public,
            new TypeReference("Microsoft.Xna.Framework", "Vector2", assembly.MainModule, xnaFacade)));
        assembly.MainModule.Types.Add(module);
        assembly.Write(path);
        return root;
    }

    string GameplayFixture(string name, bool duplicateId = false, bool invalidConstructor = false)
    {
        string root = NewDirectory("gameplay-" + name);
        Text(root, "everest.yaml", $"- Name: {name}\n  Version: 1.0.0\n  DLL: Code/{name}.dll\n  Dependencies:\n    - Name: Everest\n      Version: 1.6418.0\n");
        string path = Path.Combine(root, "Code", name + ".dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition(name, new Version(1, 0, 0, 0)), name, ModuleKind.Dll);
        ModuleDefinition module = assembly.MainModule;
        AssemblyNameReference celeste = new("Celeste", new Version(1, 4, 0, 0));
        AssemblyNameReference fna = new("FNA", new Version(21, 3, 5, 0));
        module.AssemblyReferences.Add(celeste);
        module.AssemblyReferences.Add(fna);
        TypeReference vector2 = new("Microsoft.Xna.Framework", "Vector2", module, fna);
        TypeReference entityData = new("Celeste", "EntityData", module, celeste);
        TypeReference entityId = new("Celeste", "EntityID", module, celeste);

        TypeDefinition settings = new("Fixture", name + "Settings", TypeAttributes.Public | TypeAttributes.Sealed,
            module.TypeSystem.Object);
        void Property(string propertyName, TypeReference type, CustomAttribute? attribute = null)
        {
            FieldDefinition field = new("_" + propertyName, FieldAttributes.Private, type);
            settings.Fields.Add(field);
            MethodDefinition getter = new("get_" + propertyName, MethodAttributes.Public | MethodAttributes.HideBySig |
                MethodAttributes.SpecialName, type);
            getter.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            getter.Body.Instructions.Add(Instruction.Create(OpCodes.Ldfld, field));
            getter.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            MethodDefinition setter = new("set_" + propertyName, MethodAttributes.Public | MethodAttributes.HideBySig |
                MethodAttributes.SpecialName, module.TypeSystem.Void);
            setter.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, type));
            setter.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            setter.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
            setter.Body.Instructions.Add(Instruction.Create(OpCodes.Stfld, field));
            setter.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            settings.Methods.Add(getter);
            settings.Methods.Add(setter);
            PropertyDefinition property = new(propertyName, PropertyAttributes.None, type) { GetMethod = getter, SetMethod = setter };
            if (attribute != null) property.CustomAttributes.Add(attribute);
            settings.Properties.Add(property);
        }
        TypeDefinition mode = new("Fixture", name + "Mode", TypeAttributes.Public | TypeAttributes.Sealed,
            module.ImportReference(typeof(Enum)));
        mode.Fields.Add(new FieldDefinition("value__", FieldAttributes.Public | FieldAttributes.SpecialName |
            FieldAttributes.RTSpecialName, module.TypeSystem.Int32));
        mode.Fields.Add(new FieldDefinition("Quiet", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal,
            mode) { Constant = 0 });
        mode.Fields.Add(new FieldDefinition("Loud", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal,
            mode) { Constant = 2 });
        module.Types.Add(mode);
        TypeReference rangeAttribute = new("Celeste.Mod", "SettingRangeAttribute", module, celeste);
        MethodReference rangeConstructor = new(".ctor", module.TypeSystem.Void, rangeAttribute) { HasThis = true };
        rangeConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
        rangeConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
        CustomAttribute range = new(rangeConstructor);
        range.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.Int32, 1));
        range.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.Int32, 5));
        Property("Enabled", module.TypeSystem.Boolean);
        Property("DisplayMode", mode);
        Property("Amount", module.TypeSystem.Int32, range);
        Property("Unbounded", module.TypeSystem.Int32);
        module.Types.Add(settings);

        TypeDefinition everestModule = new("Fixture", name + "Module", TypeAttributes.Public | TypeAttributes.Sealed,
            new TypeReference("Celeste.Mod", "EverestModule", module, celeste));
        MethodDefinition moduleConstructor = new(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName, module.TypeSystem.Void);
        moduleConstructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        everestModule.Methods.Add(moduleConstructor);
        MethodDefinition settingsType = new("get_SettingsType", MethodAttributes.Public | MethodAttributes.Virtual |
            MethodAttributes.HideBySig | MethodAttributes.SpecialName, module.ImportReference(typeof(Type)));
        settingsType.Body.Instructions.Add(Instruction.Create(OpCodes.Ldtoken, settings));
        settingsType.Body.Instructions.Add(Instruction.Create(OpCodes.Call,
            module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!)));
        settingsType.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        everestModule.Methods.Add(settingsType);
        module.Types.Add(everestModule);

        void CustomType(string typeName, string baseName, string[] ids, TypeReference[] parameters)
        {
            TypeDefinition type = new("Fixture", typeName, TypeAttributes.Public | TypeAttributes.Sealed,
                new TypeReference("Celeste", baseName, module, celeste));
            MethodDefinition constructor = new(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            foreach (TypeReference parameter in parameters)
                constructor.Parameters.Add(new ParameterDefinition(parameter));
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            type.Methods.Add(constructor);
            TypeReference customAttributeType = new("Celeste.Mod.Entities", "CustomEntityAttribute", module, celeste);
            MethodReference customAttributeConstructor = new(".ctor", module.TypeSystem.Void, customAttributeType) { HasThis = true };
            customAttributeConstructor.Parameters.Add(new ParameterDefinition(new ArrayType(module.TypeSystem.String)));
            CustomAttribute custom = new(customAttributeConstructor);
            custom.ConstructorArguments.Add(new CustomAttributeArgument(new ArrayType(module.TypeSystem.String),
                ids.Select(id => new CustomAttributeArgument(module.TypeSystem.String, id)).ToArray()));
            type.CustomAttributes.Add(custom);
            module.Types.Add(type);
        }
        CustomType("CustomBlock", "Entity", duplicateId ? ["fixture/shared", "fixture/shared"] :
            ["fixture/block", "fixture/blockAlias"], invalidConstructor ? [entityData] : [entityData, vector2]);
        CustomType("CustomTrigger", "Trigger", ["fixture/trigger"], [entityData, vector2, entityId]);
        CustomType("CustomIdFirst", "Entity", ["fixture/idFirst"], [entityId, entityData, vector2]);

        void PooledType(string typeName, TypeAttributes visibility)
        {
            TypeDefinition type = new("Fixture", typeName, visibility | TypeAttributes.Sealed,
                new TypeReference("Celeste", "Entity", module, celeste));
            MethodDefinition constructor = new(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            type.Methods.Add(constructor);
            TypeReference pooledAttribute = new("Monocle", "Pooled", module, celeste);
            MethodReference pooledConstructor = new(".ctor", module.TypeSystem.Void, pooledAttribute) { HasThis = true };
            type.CustomAttributes.Add(new CustomAttribute(pooledConstructor));
            module.Types.Add(type);
        }
        PooledType("PublicDebris", TypeAttributes.Public);
        PooledType("PrivateDebris", TypeAttributes.NotPublic);

        TypeReference element = new("Celeste", "BinaryPacker/Element", module, celeste);
        TypeReference backdrop = new("Celeste", "Backdrop", module, celeste);
        TypeDefinition customBackdrop = new("Fixture", "CustomBackdrop", TypeAttributes.Public | TypeAttributes.Sealed, backdrop);
        MethodDefinition backdropFactory = new("Build", MethodAttributes.Public | MethodAttributes.Static, backdrop);
        backdropFactory.Parameters.Add(new ParameterDefinition(element));
        backdropFactory.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
        backdropFactory.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        customBackdrop.Methods.Add(backdropFactory);
        TypeReference backdropAttributeType = new("Celeste.Mod.Backdrops", "CustomBackdropAttribute", module, celeste);
        MethodReference backdropAttributeConstructor = new(".ctor", module.TypeSystem.Void, backdropAttributeType) { HasThis = true };
        backdropAttributeConstructor.Parameters.Add(new ParameterDefinition(new ArrayType(module.TypeSystem.String)));
        CustomAttribute backdropAttribute = new(backdropAttributeConstructor);
        backdropAttribute.ConstructorArguments.Add(new CustomAttributeArgument(new ArrayType(module.TypeSystem.String),
            new[] { new CustomAttributeArgument(module.TypeSystem.String, "fixture/backdrop=Build") }));
        customBackdrop.CustomAttributes.Add(backdropAttribute);
        module.Types.Add(customBackdrop);
        assembly.Write(path);
        return root;
    }

    string DirectBinaryFixture(string name, string mode)
    {
        string root = NewDirectory("direct-binary-" + name);
        Text(root, "everest.yaml", $"- Name: {name}\n  Version: 1.0.0\n  DLL: Code/{name}.dll\n  Dependencies:\n    - Name: Everest\n      Version: 1.6418.0\n");
        string path = Path.Combine(root, "Code", name + ".dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition(name, new Version(1, 0, 0, 0)), name, ModuleKind.Dll);
        ModuleDefinition module = assembly.MainModule;
        AssemblyNameReference celeste = new("Celeste", new Version(1, 0, 0, 0));
        AssemblyNameReference runtimeDetour = new("MonoMod.RuntimeDetour", new Version(25, 2, 3, 0));
        module.AssemblyReferences.Add(celeste);
        module.AssemblyReferences.Add(runtimeDetour);
        AssemblyNameReference? hooks = null;
        if (mode == "mixed")
        {
            hooks = new AssemblyNameReference("MMHOOK_Celeste", new Version(0, 0, 0, 0));
            module.AssemblyReferences.Add(hooks);
        }
        TypeReference player = new("Celeste", "Player", module, celeste);
        TypeReference playerDeadBody = new("Celeste", "PlayerDeadBody", module, celeste);
        TypeReference vector2 = new("Microsoft.Xna.Framework", "Vector2", module, celeste);
        TypeDefinition fixture = new("Fixture", name + "Module", TypeAttributes.Public | TypeAttributes.Sealed,
            new TypeReference("Celeste.Mod", "EverestModule", module, celeste));
        MethodDefinition detour = new("OnPlayerDie", MethodAttributes.Public | MethodAttributes.Static, playerDeadBody);
        GenericInstanceType orig = new(new TypeReference("System", "Func`5", module, module.TypeSystem.CoreLibrary));
        orig.GenericArguments.Add(player);
        orig.GenericArguments.Add(vector2);
        orig.GenericArguments.Add(module.TypeSystem.Boolean);
        orig.GenericArguments.Add(module.TypeSystem.Boolean);
        orig.GenericArguments.Add(playerDeadBody);
        detour.Parameters.Add(new ParameterDefinition(orig));
        detour.Parameters.Add(new ParameterDefinition(player));
        detour.Parameters.Add(new ParameterDefinition(vector2));
        detour.Parameters.Add(new ParameterDefinition(module.TypeSystem.Boolean));
        detour.Parameters.Add(new ParameterDefinition(module.TypeSystem.Boolean));
        detour.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
        detour.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        fixture.Methods.Add(detour);
        if (mode == "dynamic-detour")
        {
            MethodDefinition overload = new("OnPlayerDie", MethodAttributes.Public | MethodAttributes.Static, playerDeadBody);
            overload.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
            overload.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            fixture.Methods.Add(overload);
        }

        MethodDefinition constructor = new(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        MethodReference getType = module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
        MethodReference getTarget = module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetMethod),
            [typeof(string), typeof(System.Reflection.BindingFlags)])!);
        MethodReference getDetour = module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetMethod), [typeof(string)])!);
        if (hooks != null)
        {
            TypeReference engineHook = new("On.Monocle", "Engine", module, hooks);
            MethodReference addUpdate = new("add_Update", module.TypeSystem.Void, engineHook) { HasThis = false };
            addUpdate.Parameters.Add(new ParameterDefinition(new TypeReference("On.Monocle.Engine", "hook_Update", module, hooks)));
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, addUpdate));
        }
        constructor.Body.Instructions.Add(mode == "dynamic-target"
            ? Instruction.Create(OpCodes.Ldnull)
            : Instruction.Create(OpCodes.Ldtoken, player));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, getType));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "orig_Die"));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_S,
            mode == "wrong-binding-flags" ? (sbyte)16 : (sbyte)20));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, getTarget));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldtoken, fixture));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, getType));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "OnPlayerDie"));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, getDetour));
        TypeReference hook = new("MonoMod.RuntimeDetour", "Hook", module, runtimeDetour);
        MethodReference hookConstructor = new(".ctor", module.TypeSystem.Void, hook) { HasThis = true };
        hookConstructor.Parameters.Add(new ParameterDefinition(new TypeReference("System.Reflection", "MethodBase", module, module.TypeSystem.CoreLibrary)));
        hookConstructor.Parameters.Add(new ParameterDefinition(new TypeReference("System.Reflection", "MethodInfo", module, module.TypeSystem.CoreLibrary)));
        if (mode == "config")
        {
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
            hookConstructor.Parameters.Add(new ParameterDefinition(new TypeReference(
                "MonoMod.RuntimeDetour", "DetourConfig", module, runtimeDetour)));
        }
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Newobj, hookConstructor));
        if (mode == "unsupported-member")
        {
            MethodReference unsupported = new("get_Target", new TypeReference(
                "System.Reflection", "MethodBase", module, module.TypeSystem.CoreLibrary), hook) { HasThis = true };
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, unsupported));
        }
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Pop));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        fixture.Methods.Add(constructor);
        assembly.MainModule.Types.Add(fixture);
        assembly.Write(path);
        return root;
    }

    string binaryRoot = BinaryFixture("BinarySupported", "On.Monocle", "ParticleSystem", "Emit_ParticleType_Vector2");
    ModInput binaryInput = SafeModIngestor.Ingest(binaryRoot, NewDirectory("stage-binary-supported"), 0);
    ResolvedMod binaryMod = CompatibilityAnalyzer.Analyze(binaryInput, binaryInput.Metadata[0]);
    Pass(binaryMod.Classification == CompatibilityClass.ON_HOOK_SUPPORTED, "precompiled typed hook accepted");
    Pass(binaryMod.Declaration?.ModuleType == "Fixture.BinarySupportedModule", "precompiled module factory inferred");
    Pass(binaryMod.ManagedFiles.SequenceEqual(["Code/BinarySupported.dll"]), "precompiled DLL accepted without source");
    string frozen = Path.Combine(NewDirectory("frozen"), "BinarySupported.dll");
    (string frozenAssemblyName, string originalHash, string frozenHash) = AssemblyFreezer.Freeze(
        Path.Combine(binaryInput.StagingRoot, "Code", "BinarySupported.dll"), frozen, []);
    using (AssemblyDefinition frozenAssembly = AssemblyDefinition.ReadAssembly(frozen))
    {
        Pass(frozenAssembly.MainModule.AssemblyReferences.All(reference => reference.Name != "MMHOOK_Celeste"), "frozen assembly removes HookGen runtime reference");
        Pass(frozenAssembly.MainModule.GetTypeReferences().Any(type => type.Namespace == "On.Monocle" && type.Name == "ParticleSystem" &&
             type.Scope is AssemblyNameReference reference && reference.Name == "Celeste"), "frozen typed hook binds to static Celeste facade");
        Pass(frozenAssembly.MainModule.AssemblyReferences.All(reference => reference.Name != "Microsoft.Xna.Framework") &&
             frozenAssembly.MainModule.GetTypeReferences().Any(type => type.Namespace == "Microsoft.Xna.Framework" && type.Name == "Vector2" &&
                 type.Scope is AssemblyNameReference reference && reference.Name == "FNA"),
            "legacy strong-named FNA facade binds to the canonical FNA assembly for static AOT");
    }
    Pass(frozenAssemblyName == "BinarySupported", "frozen assembly identity recorded for generic AOT rooting");
    Pass(originalHash.Length == 64 && frozenHash.Length == 64 && originalHash != frozenHash, "original and frozen assembly hashes recorded");

    string gameplayRoot = GameplayFixture("GameplayRegistry");
    ModInput gameplayInput = SafeModIngestor.Ingest(gameplayRoot, NewDirectory("stage-gameplay-registry"), 0);
    ResolvedMod gameplayMod = CompatibilityAnalyzer.Analyze(gameplayInput, gameplayInput.Metadata[0]);
    AppleStaticDeclaration gameplayDeclaration = gameplayMod.Declaration!;
    Pass(gameplayDeclaration.CustomEntityFactories.Length == 4 &&
         gameplayDeclaration.CustomEntityFactories.Count(value => value.Kind == "entity") == 3 &&
         gameplayDeclaration.CustomEntityFactories.Count(value => value.Kind == "trigger") == 1,
        "Cecil custom entity and trigger discovery");
    Pass(gameplayDeclaration.CustomEntityFactories.Any(value => value.Id == "fixture/blockAlias") &&
         gameplayDeclaration.CustomEntityFactories.Select(value => value.Constructor).Distinct(StringComparer.Ordinal)
             .OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(
                 ["entity-data-vector2", "entity-data-vector2-entity-id", "entity-id-entity-data-vector2"]),
        "multi-ID attributes and all bounded constructor shapes");
    Pass(gameplayDeclaration.CustomBackdropFactories.Length == 1 &&
         gameplayDeclaration.CustomBackdropFactories[0].Id == "fixture/backdrop" &&
         gameplayDeclaration.CustomBackdropFactories[0].Factory == "static-method" &&
         gameplayDeclaration.CustomBackdropFactories[0].Method == "Build",
        "Cecil custom backdrop discovery and static factory selection");
    Pass(gameplayDeclaration.PooledEntityTypes.SequenceEqual(["Fixture.PublicDebris"]),
        "public parameterless pooled entities are discovered while inaccessible pools remain module-owned");
    Pass(gameplayDeclaration.SettingsProperties.Length == 3 &&
         gameplayDeclaration.SettingsProperties.Any(value => value.Name == "Enabled" && value.Kind == "bool") &&
         gameplayDeclaration.SettingsProperties.Any(value => value.Name == "DisplayMode" && value.Kind == "enum" &&
             value.EnumValues.SequenceEqual([0, 2])) &&
         gameplayDeclaration.SettingsProperties.Any(value => value.Name == "Amount" && value.Kind == "int" &&
             value.Minimum == 1 && value.Maximum == 5),
        "bounded bool, enum and ranged-int settings discovery");
    Pass(gameplayDeclaration.OmittedSettingsProperties.SequenceEqual(["Unbounded:System.Int32"]),
        "unsupported unbounded setting is explicitly omitted");
    string invalidGameplayRoot = GameplayFixture("InvalidGameplay", invalidConstructor: true);
    ModInput invalidGameplayInput = SafeModIngestor.Ingest(invalidGameplayRoot,
        NewDirectory("stage-invalid-gameplay"), 0);
    ResolvedMod invalidGameplayMod = CompatibilityAnalyzer.Analyze(
        invalidGameplayInput, invalidGameplayInput.Metadata[0]);
    Pass(invalidGameplayMod.Declaration?.OmittedCustomEntityFactories.Length == 2 &&
         invalidGameplayMod.Declaration.OmittedCustomEntityFactories.All(item =>
             item.Reason == "runtime-only-constructor"),
        "runtime-created entity without a map factory is explicitly omitted before AOT");
    string duplicateGameplayRoot = GameplayFixture("DuplicateGameplay", duplicateId: true);
    ModInput duplicateGameplayInput = SafeModIngestor.Ingest(duplicateGameplayRoot,
        NewDirectory("stage-duplicate-gameplay"), 0);
    Throws(() => CompatibilityAnalyzer.Analyze(duplicateGameplayInput, duplicateGameplayInput.Metadata[0]),
        "duplicate custom entity factory ID", "duplicate custom entity ID rejected before AOT");

    RuntimeClosureScanner.VerifyPreserved(frozen, frozen);
    Pass(true, "complete external assembly preservation accepts intact methods");
    string stripped = Path.Combine(NewDirectory("stripped"), "BinarySupported.dll");
    File.Copy(frozen, stripped);
    using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(stripped, new ReaderParameters { ReadWrite = true }))
    {
        MethodDefinition constructor = assembly.MainModule.Types.Single(type => type.Name == "BinarySupportedModule")
            .Methods.Single(method => method.IsConstructor);
        constructor.Body.Instructions.Clear();
        assembly.Write();
    }
    Throws(() => RuntimeClosureScanner.VerifyPreserved(frozen, stripped), "lost executable methods",
        "trimmed external method body rejected");

    string directRoot = DirectBinaryFixture("DirectSupported", "supported");
    ModInput directInput = SafeModIngestor.Ingest(directRoot, NewDirectory("stage-direct-supported"), 0);
    ResolvedMod directMod = CompatibilityAnalyzer.Analyze(directInput, directInput.Metadata[0]);
    Pass(directMod.Classification == CompatibilityClass.DIRECT_HOOK_SUPPORTED && directMod.DirectManagedHooks.Count == 1,
        "precompiled direct Hook constructor accepted");
    DirectManagedHookPlan directPlan = directMod.DirectManagedHooks.Single();
    Pass(directPlan.TargetId == "celeste-player-die" && directPlan.Capture == "STATIC" &&
         directPlan.DetourType == "Fixture.DirectSupportedModule" && directPlan.DetourMethod == "OnPlayerDie",
        "direct Hook target and detour statically resolved");
    string frozenDirect = Path.Combine(NewDirectory("frozen-direct"), "DirectSupported.dll");
    _ = AssemblyFreezer.Freeze(Path.Combine(directInput.StagingRoot, "Code", "DirectSupported.dll"), frozenDirect,
        directMod.DirectManagedHooks);
    using (AssemblyDefinition frozenDirectAssembly = AssemblyDefinition.ReadAssembly(frozenDirect))
    {
        Pass(frozenDirectAssembly.MainModule.AssemblyReferences.All(reference => reference.Name != "MonoMod.RuntimeDetour"),
            "frozen direct Hook assembly removes desktop RuntimeDetour reference");
        MethodDefinition frozenConstructor = frozenDirectAssembly.MainModule.Types.Single(type => type.Name == "DirectSupportedModule")
            .Methods.Single(method => method.IsConstructor);
        MethodReference rewrittenConstructor = frozenConstructor.Body.Instructions.Select(instruction => instruction.Operand)
            .OfType<MethodReference>().Single(method => method.DeclaringType.FullName == "MonoMod.RuntimeDetour.Hook" && method.Name == ".ctor");
        Pass(rewrittenConstructor.Parameters.Count == 1 && rewrittenConstructor.Parameters[0].ParameterType.FullName == "System.String" &&
             rewrittenConstructor.DeclaringType.Scope is AssemblyNameReference reference && reference.Name == "Celeste" &&
             frozenConstructor.Body.Instructions.Any(instruction => instruction.OpCode == OpCodes.Ldstr &&
                 Equals(instruction.Operand, directPlan.PlanId)),
            "direct Hook construction lowered to a fixed plan ID and Apple static facade");
    }
    string mixedRoot = DirectBinaryFixture("MixedSupported", "mixed");
    ModInput mixedInput = SafeModIngestor.Ingest(mixedRoot, NewDirectory("stage-mixed-supported"), 0);
    ResolvedMod mixedMod = CompatibilityAnalyzer.Analyze(mixedInput, mixedInput.Metadata[0]);
    Pass(mixedMod.Classification == CompatibilityClass.MIXED_MANAGED_DETOURS_SUPPORTED &&
         mixedMod.ManagedDetourTargets.SetEquals(["celeste-player-die", "monocle-engine-update"]) &&
         mixedMod.DirectManagedHooks.Count == 1,
        "precompiled mixed HookGen/direct Hook module shares one managed-detour classification");
    foreach ((string mode, CompatibilityClass expected, string message) in new[]
    {
        ("dynamic-target", CompatibilityClass.DYNAMIC_TARGET_DEFERRED, "DEFERRED_DYNAMIC_TARGET"),
        ("wrong-binding-flags", CompatibilityClass.DYNAMIC_TARGET_DEFERRED, "DEFERRED_DYNAMIC_TARGET"),
        ("dynamic-detour", CompatibilityClass.DYNAMIC_DETOUR_DEFERRED, "DEFERRED_DYNAMIC_DETOUR"),
        ("config", CompatibilityClass.DETOUR_CONFIG_DEFERRED, "DEFERRED_DETOUR_CONFIG"),
        ("unsupported-member", CompatibilityClass.DIRECT_HOOK_DEFERRED, "unsupported direct Hook member")
    })
    {
        string root = DirectBinaryFixture("Direct" + mode.Replace("-", "", StringComparison.Ordinal), mode);
        ModInput input = SafeModIngestor.Ingest(root, NewDirectory("stage-direct-" + mode), 0);
        ResolvedMod audit = CompatibilityAnalyzer.Audit(input, input.Metadata[0]);
        Pass(audit.Classification == expected && audit.Mechanisms.Any(value => value.Contains(message, StringComparison.Ordinal)),
            mode + " direct Hook audit classification");
        Throws(() => CompatibilityAnalyzer.Analyze(input, input.Metadata[0]), message,
            mode + " direct Hook rejected before AOT");
    }

    string ApiContract(string directory, bool includeExpectedMethod, bool publicExpectedMethod = true)
    {
        string path = Path.Combine(NewDirectory(directory), "TargetApi.dll");
        using AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("TargetApi", new Version(1, 0, 0, 0)), "TargetApi", ModuleKind.Dll);
        TypeDefinition contract = new("Fixture", "Contract", TypeAttributes.Public | TypeAttributes.Abstract |
            TypeAttributes.Sealed, assembly.MainModule.TypeSystem.Object);
        if (includeExpectedMethod)
        {
            MethodDefinition expected = new("Expected", (publicExpectedMethod ? MethodAttributes.Public :
                    MethodAttributes.Private) | MethodAttributes.Static,
                assembly.MainModule.TypeSystem.Void);
            expected.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            contract.Methods.Add(expected);
        }
        assembly.MainModule.Types.Add(contract);
        assembly.Write(path);
        return path;
    }

    string goodApi = ApiContract("api-good", includeExpectedMethod: true);
    string badApi = ApiContract("api-bad", includeExpectedMethod: false);
    string inaccessibleApi = ApiContract("api-inaccessible", includeExpectedMethod: true,
        publicExpectedMethod: false);
    string apiConsumer = Path.Combine(NewDirectory("api-consumer"), "ApiConsumer.dll");
    using (AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
        new AssemblyNameDefinition("ApiConsumer", new Version(1, 0, 0, 0)), "ApiConsumer", ModuleKind.Dll))
    {
        AssemblyNameReference targetReference = new("TargetApi", new Version(1, 0, 0, 0));
        assembly.MainModule.AssemblyReferences.Add(targetReference);
        TypeDefinition consumer = new("Fixture", "Consumer", TypeAttributes.Public | TypeAttributes.Abstract |
            TypeAttributes.Sealed, assembly.MainModule.TypeSystem.Object);
        MethodDefinition probe = new("Probe", MethodAttributes.Public | MethodAttributes.Static,
            assembly.MainModule.TypeSystem.Void);
        TypeReference targetType = new("Fixture", "Contract", assembly.MainModule, targetReference);
        MethodReference targetMethod = new("Expected", assembly.MainModule.TypeSystem.Void, targetType)
        {
            HasThis = false
        };
        probe.Body.Instructions.Add(Instruction.Create(OpCodes.Call, targetMethod));
        ArrayType targetGrid = new(targetType, 2);
        MethodReference targetGridConstructor = new(".ctor", assembly.MainModule.TypeSystem.Void, targetGrid)
        {
            HasThis = true
        };
        targetGridConstructor.Parameters.Add(new ParameterDefinition(assembly.MainModule.TypeSystem.Int32));
        targetGridConstructor.Parameters.Add(new ParameterDefinition(assembly.MainModule.TypeSystem.Int32));
        probe.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_1));
        probe.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_1));
        probe.Body.Instructions.Add(Instruction.Create(OpCodes.Newobj, targetGridConstructor));
        probe.Body.Instructions.Add(Instruction.Create(OpCodes.Pop));
        probe.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        consumer.Methods.Add(probe);
        assembly.MainModule.Types.Add(consumer);
        assembly.Write(apiConsumer);
    }
    RuntimeClosureScanner.VerifyReferencedApi(apiConsumer, goodApi);
    Pass(true, "external assembly API closure accepts the complete target contract and CLR array intrinsics");
    Throws(() => RuntimeClosureScanner.VerifyReferencedApi(apiConsumer, badApi), "absent from the linked TargetApi contract",
        "external assembly API closure rejects a missing target method before device AOT");
    Throws(() => RuntimeClosureScanner.VerifyReferencedApi(apiConsumer, inaccessibleApi), "inaccessible-method",
        "external assembly API closure rejects a private target method before device AOT");

    Pass(AppleApiSurface.Members.Count == 30 && AppleApiSurface.ContractSha256.Length == 64,
        "exact reviewed Apple external API surface contract");
    string apiSurfaceRoot = NewDirectory("apple-api-surface");
    Text(apiSurfaceRoot, "Celeste/Level.cs",
        "namespace Celeste;\npublic class Level\n{\n\tprivate float unpauseTimer;\n\tprivate void StartPauseEffects() {}\n\tprivate void EndPauseEffects() {}\n}\n");
    Text(apiSurfaceRoot, "Celeste/Actor.cs",
        "namespace Celeste;\npublic class Actor\n{\n\tprivate Vector2 movementCounter;\n}\n");
    Text(apiSurfaceRoot, "Celeste/Glider.cs",
        "namespace Celeste;\npublic class Glider\n{\n\tprivate bool destroyed;\n\tprivate Sprite sprite;\n\tprivate IEnumerator DestroyAnimationRoutine() {}\n}\n");
    Text(apiSurfaceRoot, "Celeste/CassetteBlock.cs",
        "namespace Celeste;\npublic class CassetteBlock\n{\n\tprivate List<CassetteBlock> group;\n\tprivate Vector2 groupOrigin;\n\n\tprivate Color color;\n\tprivate List<Image> pressed = new List<Image>();\n\tprivate List<Image> solid = new List<Image>();\n\tprivate Image CreateImage(float x, float y, int tx, int ty, MTexture tex) => null;\n\tprivate void ShiftSize(int amount) {}\n}\n");
    Text(apiSurfaceRoot, "Celeste/CrystalStaticSpinner.cs",
        "namespace Celeste;\npublic class CrystalStaticSpinner\n{\n\tprivate class Border : Entity\n\t{\n\t\tprivate Entity[] drawing = new Entity[2];\n\t}\n\tprivate Entity filler;\n\tprivate Border border;\n\tprivate int randomSeed;\n\tprivate void AddSprite(Vector2 offset) {}\n\tprivate bool SolidCheck(Vector2 position) => false;\n}\n");
    Text(apiSurfaceRoot, "Celeste/EntityData.cs",
        "namespace Celeste;\npublic class EntityData\n{\n\tpublic string Attr(string key, string defaultValue = \"\") => defaultValue;\n}\n");
    Text(apiSurfaceRoot, "Celeste/NorthernLights.cs",
        "namespace Celeste;\npublic class NorthernLights\n{\n\tprivate class Node { }\n\tprivate class Strand { public List<Node> Nodes = new List<Node>(); }\n\tprivate List<Strand> strands = new List<Strand>();\n\tprivate float timer;\n}\n");
    Text(apiSurfaceRoot, "Celeste/FloatySpaceBlock.cs",
        "namespace Celeste;\npublic class FloatySpaceBlock\n{\n\tprivate float sineWave;\n}\n");
    Text(apiSurfaceRoot, "Celeste/Strawberry.cs",
        "namespace Celeste;\npublic class Strawberry\n{\n\tpublic bool Golden { get; private set; }\n}\n");
    Text(apiSurfaceRoot, "Celeste/Booster.cs",
        "namespace Celeste;\npublic class Booster\n{\n\tprivate Sprite sprite;\n\tprivate ParticleType particleType;\n\tprivate bool red;\n}\n");
    AppleApiSurface.Apply(apiSurfaceRoot);
    string apiSurfaceLevel = File.ReadAllText(Path.Combine(apiSurfaceRoot, "Celeste", "Level.cs"));
    Pass(apiSurfaceLevel.Contains("public float unpauseTimer", StringComparison.Ordinal) &&
         apiSurfaceLevel.Contains("public void StartPauseEffects()", StringComparison.Ordinal) &&
         apiSurfaceLevel.Contains("public void EndPauseEffects()", StringComparison.Ordinal),
        "exact reviewed Apple API surface is applied");
    string apiSurfaceActor = File.ReadAllText(Path.Combine(apiSurfaceRoot, "Celeste", "Actor.cs"));
    string apiSurfaceGlider = File.ReadAllText(Path.Combine(apiSurfaceRoot, "Celeste", "Glider.cs"));
    Pass(apiSurfaceActor.Contains("public Vector2 movementCounter", StringComparison.Ordinal) &&
         apiSurfaceGlider.Contains("public bool destroyed", StringComparison.Ordinal) &&
         apiSurfaceGlider.Contains("public Sprite sprite", StringComparison.Ordinal) &&
         apiSurfaceGlider.Contains("public IEnumerator DestroyAnimationRoutine()", StringComparison.Ordinal),
        "Cpop's exact pinned-Everest publicized members are reviewed and applied");
    string apiSurfaceCassette = File.ReadAllText(Path.Combine(apiSurfaceRoot, "Celeste", "CassetteBlock.cs"));
    string apiSurfaceSpinner = File.ReadAllText(Path.Combine(apiSurfaceRoot, "Celeste", "CrystalStaticSpinner.cs"));
    string apiSurfaceEntityData = File.ReadAllText(Path.Combine(apiSurfaceRoot, "Celeste", "EntityData.cs"));
    Pass(apiSurfaceCassette.Contains("public List<CassetteBlock> group", StringComparison.Ordinal) &&
         apiSurfaceCassette.Contains("public Color color", StringComparison.Ordinal) &&
         apiSurfaceCassette.Contains("public List<Image> pressed", StringComparison.Ordinal) &&
         apiSurfaceCassette.Contains("public List<Image> solid", StringComparison.Ordinal) &&
         apiSurfaceCassette.Contains("public Image CreateImage", StringComparison.Ordinal) &&
         apiSurfaceCassette.Contains("public void ShiftSize", StringComparison.Ordinal) &&
         apiSurfaceSpinner.Contains("public class Border", StringComparison.Ordinal) &&
         apiSurfaceSpinner.Contains("public Entity[] drawing", StringComparison.Ordinal) &&
         apiSurfaceSpinner.Contains("public Entity filler", StringComparison.Ordinal) &&
         apiSurfaceSpinner.Contains("public Border border", StringComparison.Ordinal) &&
         apiSurfaceSpinner.Contains("public int randomSeed", StringComparison.Ordinal) &&
         apiSurfaceSpinner.Contains("public void AddSprite", StringComparison.Ordinal) &&
         apiSurfaceSpinner.Contains("public bool SolidCheck", StringComparison.Ordinal) &&
         apiSurfaceEntityData.Contains("public string String", StringComparison.Ordinal),
        "DashToggle's exact pinned-Everest publicized members are reviewed and applied");
    string apiSurfaceNorthernLights = File.ReadAllText(Path.Combine(apiSurfaceRoot, "Celeste", "NorthernLights.cs"));
    string apiSurfaceFloaty = File.ReadAllText(Path.Combine(apiSurfaceRoot, "Celeste", "FloatySpaceBlock.cs"));
    string apiSurfaceStrawberry = File.ReadAllText(Path.Combine(apiSurfaceRoot, "Celeste", "Strawberry.cs"));
    string apiSurfaceBooster = File.ReadAllText(Path.Combine(apiSurfaceRoot, "Celeste", "Booster.cs"));
    Pass(apiSurfaceNorthernLights.Contains("public class Node", StringComparison.Ordinal) &&
         apiSurfaceNorthernLights.Contains("public class Strand", StringComparison.Ordinal) &&
         apiSurfaceNorthernLights.Contains("public List<Strand> strands", StringComparison.Ordinal) &&
         apiSurfaceNorthernLights.Contains("public float timer", StringComparison.Ordinal) &&
         apiSurfaceFloaty.Contains("public float sineWave", StringComparison.Ordinal) &&
         apiSurfaceStrawberry.Contains("public bool Golden { get; set; }", StringComparison.Ordinal) &&
         apiSurfaceBooster.Contains("public Sprite sprite", StringComparison.Ordinal) &&
         apiSurfaceBooster.Contains("public ParticleType particleType", StringComparison.Ordinal) &&
         apiSurfaceBooster.Contains("public bool red", StringComparison.Ordinal),
        "CaeruleaHelper's exact pinned-Everest publicized members are reviewed and applied");
    Throws(() => AppleApiSurface.Apply(apiSurfaceRoot), "must occur exactly once",
        "Apple API surface rejects duplicate application");
    string unsupportedRoot = BinaryFixture("BinaryDeferred", "On.Celeste", "Player", "UnknownMethod");
    ModInput unsupportedInput = SafeModIngestor.Ingest(unsupportedRoot, NewDirectory("stage-binary-deferred"), 0);
    Throws(() => CompatibilityAnalyzer.Analyze(unsupportedInput, unsupportedInput.Metadata[0]), "ON_HOOK_DEFERRED", "unknown precompiled hook target rejected");
    string binaryIlRoot = BinaryFixture("BinaryIlDeferred", "IL.Celeste", "SummitCheckpoint");
    ModInput binaryIlInput = SafeModIngestor.Ingest(binaryIlRoot, NewDirectory("stage-binary-il-deferred"), 0);
    ResolvedMod binaryIl = CompatibilityAnalyzer.Audit(binaryIlInput, binaryIlInput.Metadata[0]);
    Pass(binaryIl.Classification == CompatibilityClass.IL_HOOK_DEFERRED &&
         binaryIl.Mechanisms.Contains("Code/BinaryIlDeferred.dll:il-hook:IL.Celeste.SummitCheckpoint"),
         "precompiled IL.* type detected from CLI metadata");
    Pass(EverestGraphResolver.Resolve([binaryMod]).Single().Metadata.Name == "BinarySupported", "pinned Everest platform dependency satisfied");

    string realLayout = NewDirectory("real-content-layout");
    Text(realLayout, "everest.yaml", "- Name: RealContent\n  Version: 1.0.0\n  Dependencies:\n    - Name: Everest\n      Version: 1.519.0\n");
    Text(realLayout, "Maps/Author/Map.bin", "map");
    Text(realLayout, "Dialog/English.txt", "author_map=Map");
    ModInput realContentInput = SafeModIngestor.Ingest(realLayout, NewDirectory("stage-real-content"), 0);
    ResolvedMod realContent = CompatibilityAnalyzer.Analyze(realContentInput, realContentInput.Metadata[0]);
    Pass(realContent.ContentFiles.SequenceEqual(["Dialog/English.txt", "Maps/Author/Map.bin"]), "ordinary Everest content roots accepted");

    string contentOut = NewDirectory("compiled-content");
    string asset = Path.Combine(repository, "apple-everest/canaries/content/Content/AppleEverest/Canary/banner.asset.json");
    string map = Path.Combine(repository, "apple-everest/canaries/content/Content/Maps/AppleEverest/Canary.xml");
    Pass(ContentCompiler.Stage(asset, "Content/AppleEverest/banner.asset.json", contentOut) == "AppleEverest/banner.png", "asset logical path");
    Pass(File.ReadAllBytes(Path.Combine(contentOut, "AppleEverest/banner.png")).Take(8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }), "generated PNG");
    Pass(ContentCompiler.Stage(map, "Content/Maps/AppleEverest/Canary.xml", contentOut) == "Maps/AppleEverest/Canary.bin", "map logical path");
    Pass(File.ReadAllBytes(Path.Combine(contentOut, "Maps/AppleEverest/Canary.bin")).Length > 64, "compiled map");
    string gameplayMap = Path.Combine(temporary, "gameplay-registry-map.xml");
    File.WriteAllText(gameplayMap,
        "<Map><levels><level><entities><fixtureBlock /></entities><triggers><fixtureTrigger /></triggers></level></levels>" +
        "<Style><Backgrounds><fixtureBackdrop /></Backgrounds><Foregrounds /></Style></Map>");
    string gameplayMapOutput = NewDirectory("gameplay-map-content");
    ContentCompiler.Stage(gameplayMap, "Content/Maps/Fixture/Registry.xml", gameplayMapOutput);
    Pass(ContentCompiler.InspectGameplayIds(Path.Combine(gameplayMapOutput, "Maps/Fixture/Registry.bin"))
            .SequenceEqual(new[] { ("backdrop", "fixtureBackdrop"), ("entity", "fixtureBlock"), ("trigger", "fixtureTrigger") }),
        "map metadata discovers custom entity, trigger and backdrop identifiers without loading code");
    string genericMap = Path.Combine(contentOut, "Maps/AppleEverest/Canary.bin");
    string normalizedOut = NewDirectory("normalized-map-content");
    Pass(ContentCompiler.Stage(genericMap, "Content/Maps/Author/RealMap.bin", normalizedOut) == "Maps/Author/RealMap.bin",
        "precompiled Everest map logical path");
    static (string Magic, string Package, byte[] Body) ReadMap(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
        string magic = reader.ReadString();
        string package = reader.ReadString();
        byte[] body = new byte[checked((int)(stream.Length - stream.Position))];
        stream.ReadExactly(body);
        return (magic, package, body);
    }
    (string sourceMagic, _, byte[] sourceBody) = ReadMap(genericMap);
    (string targetMagic, string targetPackage, byte[] targetBody) = ReadMap(Path.Combine(normalizedOut, "Maps/Author/RealMap.bin"));
    Pass(sourceMagic == "CELESTE MAP" && targetMagic == sourceMagic && targetPackage == "Author/RealMap" &&
         targetBody.SequenceEqual(sourceBody), "Everest map package is normalized without changing its binary body");

    MapBinaryBoundaryRecord plainBoundary = ContentCompiler.InspectBoundary(genericMap);
    Pass(plainBoundary.ConsumedRootBytes == plainBoundary.FileBytes && plainBoundary.AppendixBytes == 0 &&
         plainBoundary.AppendixSha256 == Hashing.BytesSha256([]),
        "no-appendix map boundary and bytes remain unchanged");
    string appendedMap = Path.Combine(temporary, "map-with-appendix.bin");
    byte[] appendix = Encoding.UTF8.GetBytes("desktop-owned-trailing-appendix\0not-a-second-root");
    using (FileStream output = File.Create(appendedMap))
    {
        output.Write(File.ReadAllBytes(genericMap));
        output.Write(appendix);
    }
    MapBinaryBoundaryRecord appendedBoundary = ContentCompiler.InspectBoundary(appendedMap);
    Pass(appendedBoundary.ConsumedRootBytes == plainBoundary.FileBytes &&
         appendedBoundary.AppendixBytes == appendix.Length &&
         appendedBoundary.AppendixSha256 == Hashing.BytesSha256(appendix),
        "valid primary root records bounded appendix length and SHA-256 without interpreting it");
    Pass(JsonSerializer.Serialize(ContentCompiler.InspectElements(appendedMap)) ==
         JsonSerializer.Serialize(ContentCompiler.InspectElements(genericMap)),
        "appended map root semantics equal desktop stop-after-root behavior");
    string plainMapHash = Hashing.FileSha256(genericMap);
    string appendedMapHash = Hashing.FileSha256(appendedMap);
    MapProgressionRecord plainProgression = ContentCompiler.InspectProgression(genericMap,
        "Maps/AppleEverest/Canary.bin", plainMapHash);
    MapProgressionRecord appendedProgression = ContentCompiler.InspectProgression(appendedMap,
        "Maps/AppleEverest/Canary.bin", appendedMapHash);
    Pass(plainMapHash != appendedMapHash && appendedProgression.MapSha256 == appendedMapHash &&
         appendedProgression.Rooms.SequenceEqual(plainProgression.Rooms) &&
         appendedProgression.ProgressionEntities.SequenceEqual(plainProgression.ProgressionEntities) &&
         appendedProgression.CompatibilityId != plainProgression.CompatibilityId,
        "complete source hash remains authoritative while parsed root semantics are unchanged");
    string oversizedAppendixMap = Path.Combine(temporary, "oversized-map-appendix.bin");
    using (FileStream output = File.Create(oversizedAppendixMap))
    {
        output.Write(File.ReadAllBytes(genericMap));
        output.SetLength(output.Length + ContentCompiler.MaxMapAppendixBytes + 1);
    }
    Throws(() => ContentCompiler.InspectBoundary(oversizedAppendixMap), "bounded compatibility limit",
        "pathological map appendix rejected");
    string truncatedRootMap = Path.Combine(temporary, "truncated-map-root.bin");
    byte[] validRootBytes = File.ReadAllBytes(genericMap);
    File.WriteAllBytes(truncatedRootMap, validRootBytes[..^1]);
    Throws(() => ContentCompiler.InspectBoundary(truncatedRootMap), "truncated",
        "truncated primary map root rejected rather than treated as appendix");
    string invalidMap = Path.Combine(temporary, "invalid-map.bin");
    File.WriteAllText(invalidMap, "not a Celeste map");
    Throws(() => ContentCompiler.Stage(invalidMap, "Content/Maps/Author/Invalid.bin", normalizedOut),
        "invalid Celeste header", "malformed precompiled map fails closed");
    string first = Path.Combine(temporary, "first.txt"), second = Path.Combine(temporary, "second.txt");
    File.WriteAllText(first, "first"); File.WriteAllText(second, "second");
    ContentCompiler.Stage(first, "Content/precedence.txt", contentOut);
    ContentCompiler.Stage(second, "Content/precedence.txt", contentOut);
    Pass(File.ReadAllText(Path.Combine(contentOut, "precedence.txt")) == "second", "later mount precedence");

    string profilePath = Path.Combine(repository, "apple-everest/profiles/stable-1.6458.0.json");
    using JsonDocument profile = JsonDocument.Parse(File.ReadAllBytes(profilePath));
    Pass(profile.RootElement.GetProperty("everest").GetProperty("sha256Commit").GetString() == "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00", "Everest pin");
    Pass(profile.RootElement.GetProperty("dependencies").GetProperty("monoModCommit").GetString() == "dfc30a1506d37fb88a2c2be004f525205f46a24c", "MonoMod pin");
    Pass(profile.RootElement.GetProperty("host").GetProperty("dotnetSdk").GetString() == "8.0.424", "host SDK pin");
    using (JsonDocument ecosystem = JsonDocument.Parse(File.ReadAllBytes(
        Path.Combine(repository, "apple-everest/helper-ecosystem-compatibility-stage25e.json"))))
    {
        JsonElement selected = ecosystem.RootElement.GetProperty("selected");
        Pass(ecosystem.RootElement.GetProperty("helperCandidates").GetArrayLength() >= 8 &&
             ecosystem.RootElement.GetProperty("mapCandidates").GetArrayLength() >= 12,
            "Stage 25E candidate audit breadth");
        Pass(selected.GetProperty("helper").GetProperty("name").GetString() == "CpopHelper" &&
             selected.GetProperty("helper").GetProperty("zipSha256").GetString() ==
                 "7a807a8f9ce6ccb4d6ad0c664bb7791a60734533fb33cfcd6beccb202d846b63" &&
             selected.GetProperty("map").GetProperty("name").GetString() == "QuizSample" &&
             selected.GetProperty("map").GetProperty("zipSha256").GetString() ==
                 "5cb8351bb263aa316831d2b683cb8b04acd270587edfe7c9e3df3bb8e6b83b3e",
            "selected ordinary helper and dependent-map release pins");
        Pass(selected.GetProperty("sourceTreesRequiredForProduction").GetBoolean() == false &&
             selected.GetProperty("redistributedThirdPartyBytes").GetBoolean() == false,
            "selected helper ecosystem is source-free and not redistributed");
    }
    using (JsonDocument targetCatalog = JsonDocument.Parse(File.ReadAllBytes(
        Path.Combine(repository, "apple-everest/managed-detour-targets-v2.json"))))
    {
        JsonElement targets = targetCatalog.RootElement.GetProperty("targets");
        Pass(targetCatalog.RootElement.GetProperty("schemaVersion").GetInt32() == 2 &&
             targets.GetArrayLength() == 205,
            "signature-driven managed-detour target catalog v2");
        string[] ids = targets.EnumerateArray().Select(target => target.GetProperty("id").GetString()!).ToArray();
        Pass(ids.Distinct(StringComparer.Ordinal).Count() == 205 &&
             ids.Contains("celeste-commands-cmd-ow-complete", StringComparer.Ordinal) &&
             ids.Contains("celeste-oui-chapter-select-enter", StringComparer.Ordinal) &&
             ids.Contains("celeste-area-mode-stats-clone", StringComparer.Ordinal) &&
             ids.Contains("celeste-save-data-add-death", StringComparer.Ordinal) &&
             ids.Contains("celeste-player-added", StringComparer.Ordinal) &&
             ids.Contains("monocle-entity-added", StringComparer.Ordinal) &&
             ids.Contains("celeste-player-super-jump", StringComparer.Ordinal) &&
             ids.Contains("celeste-player-super-wall-jump-angle-check", StringComparer.Ordinal) &&
             ids.Contains("celeste-star-jump-block-open", StringComparer.Ordinal) &&
             ids.Contains("celeste-player-dash-end", StringComparer.Ordinal) &&
             ids.Contains("celeste-level-loader-loading-thread", StringComparer.Ordinal) &&
             ids.Contains("celeste-crystal-static-spinner-removed", StringComparer.Ordinal) &&
             ids.Contains("celeste-player-star-fly-return-to-normal-hitbox", StringComparer.Ordinal) &&
             ids.Contains("celeste-theo-crystal-update", StringComparer.Ordinal),
            "B2/I-A target catalog covers exact high-arity, IEnumerator, constructor, inherited and helper graph target shapes");
    }
    ManagedDetourTarget SyntheticTarget(string id, string eventName, bool isStatic, string returnType,
        params (string Type, string Name)[] parameters) => new()
    {
        Id = id,
        SourceFile = id + ".cs",
        SourceDeclaration = "public " + (isStatic ? "static " : "") + returnType + " " + eventName + "(" +
            string.Join(", ", parameters.Select(value => value.Type + " " + value.Name)) + ")",
        OriginalDeclaration = "private " + (isStatic ? "static " : "") + returnType + " Original_" + eventName + "(" +
            string.Join(", ", parameters.Select(value => value.Type + " " + value.Name)) + ")",
        OriginalAlias = "Original_" + eventName,
        HookNamespace = "On.Fixture",
        HookType = "SignatureMatrix",
        EventName = eventName,
        OrigDelegate = "orig_" + eventName,
        HookDelegate = "hook_" + eventName,
        IsStatic = isStatic,
        ReceiverType = isStatic ? null : "global::Fixture.SignatureMatrix",
        ReturnType = returnType,
        Parameters = parameters.Select(value => new ManagedDetourParameter { Type = value.Type, Name = value.Name }).ToArray()
    };
    ManagedDetourTarget[] signatureMatrix =
    [
        SyntheticTarget("fixture-static-void", "StaticVoid", true, "void"),
        SyntheticTarget("fixture-static-int", "StaticInt", true, "int", ("int", "value")),
        SyntheticTarget("fixture-instance-void", "InstanceVoid", false, "void"),
        SyntheticTarget("fixture-instance-return", "InstanceReturn", false, "int",
            ("int", "value"), ("global::Microsoft.Xna.Framework.Vector2", "position"), ("string", "label")),
        SyntheticTarget("fixture-high-arity", "HighArity", true, "void",
            ("int", "a"), ("int", "b"), ("int", "c"), ("int", "d"),
            ("int", "e"), ("int", "f"), ("int", "g"), ("int", "h")),
        SyntheticTarget("fixture-ienumerator-return", "EnumeratorReturn", false,
            "global::System.Collections.IEnumerator", ("int", "from")),
        SyntheticTarget("fixture-reference-return", "ReferenceReturn", false,
            "global::Fixture.SignatureMatrix", ("string", "label"))
    ];
    string signatureSource = ManagedDetourGenerator.DispatcherSource(signatureMatrix);
    Pass(signatureSource.Contains("delegate void orig_StaticVoid()", StringComparison.Ordinal) &&
         signatureSource.Contains("delegate int orig_StaticInt(int value)", StringComparison.Ordinal) &&
         signatureSource.Contains("delegate void orig_InstanceVoid(global::Fixture.SignatureMatrix self)", StringComparison.Ordinal) &&
         signatureSource.Contains("delegate int orig_InstanceReturn(global::Fixture.SignatureMatrix self, int value, global::Microsoft.Xna.Framework.Vector2 position, string label)", StringComparison.Ordinal) &&
         signatureSource.Contains("delegate void orig_HighArity(int a, int b, int c, int d, int e, int f, int g, int h)", StringComparison.Ordinal) &&
         signatureSource.Contains("delegate global::System.Collections.IEnumerator orig_EnumeratorReturn(global::Fixture.SignatureMatrix self, int from)", StringComparison.Ordinal) &&
         signatureSource.Contains("delegate global::Fixture.SignatureMatrix orig_ReferenceReturn(global::Fixture.SignatureMatrix self, string label)", StringComparison.Ordinal),
        "signature generator covers static/instance, high arity, IEnumerator/reference return, struct/reference and multiple arguments");
    Pass(!signatureSource.Contains("DynamicInvoke", StringComparison.Ordinal) &&
         !signatureSource.Contains("object[]", StringComparison.Ordinal) &&
         signatureSource.Contains("AppleEverestHookList.Version", StringComparison.Ordinal),
        "generated signature matrix remains strongly typed and version-cached");
    string signatureRoot = NewDirectory("signature-rewrite");
    foreach (ManagedDetourTarget target in signatureMatrix)
        Text(signatureRoot, target.SourceFile,
            "namespace Fixture; public class SignatureMatrix\n{\n\t" + target.SourceDeclaration + "\n\t{\n\t\t" +
            (target.ReturnType == "void" ? "return;" : "return 0;") + "\n\t}\n}\n");
    ManagedDetourGenerator.RewriteTargets(signatureRoot, signatureMatrix);
    Pass(signatureMatrix.All(target => File.ReadAllText(Path.Combine(signatureRoot, target.SourceFile))
            .Contains(target.OriginalDeclaration, StringComparison.Ordinal)),
        "signature generator rewrites each synthetic target into one wrapper and one original body");
    ManagedDetourTarget readonlyConstructor = new()
    {
        Id = "fixture-readonly-constructor",
        SourceFile = "ReadonlyConstructor.cs",
        SourceDeclaration = "public ReadonlyConstructor(int value)",
        OriginalDeclaration = "private void Original_ctor_int(int value)",
        OriginalAlias = "Original_ctor_int",
        HookNamespace = "On.Fixture",
        HookType = "ReadonlyConstructor",
        EventName = "ctor_int",
        OrigDelegate = "orig_ctor_int",
        HookDelegate = "hook_ctor_int",
        IsStatic = false,
        ReceiverType = "global::Fixture.ReadonlyConstructor",
        ReturnType = "void",
        Parameters = [new ManagedDetourParameter { Type = "int", Name = "value" }]
    };
    Text(signatureRoot, readonlyConstructor.SourceFile,
        "namespace Fixture; public class ReadonlyConstructor\n{\n\tprivate readonly int value;\n" +
        "\tpublic ReadonlyConstructor(int value)\n\t{\n\t\tthis.value = value;\n\t}\n}\n");
    ManagedDetourGenerator.RewriteTargets(signatureRoot, [readonlyConstructor]);
    string readonlyConstructorSource = File.ReadAllText(Path.Combine(signatureRoot, readonlyConstructor.SourceFile));
    Pass(readonlyConstructorSource.Contains("private int value;", StringComparison.Ordinal) &&
         !readonlyConstructorSource.Contains("private readonly int value;", StringComparison.Ordinal) &&
         readonlyConstructorSource.Contains("private void Original_ctor_int", StringComparison.Ordinal),
        "constructor detour source move relaxes only readonly fields assigned by the moved body");
    DirectManagedHookPlan directEvidencePlan = new(
        "fixture:direct-evidence", "Fixture", "Fixture", "Fixture.DirectEvidence::.ctor", 0,
        "fixture-instance-return", "InstanceReturn", "Fixture.DirectEvidence", "Apply", true, "int",
        [
            "global::System.Func<global::Fixture.SignatureMatrix, int, global::Microsoft.Xna.Framework.Vector2, string, int>",
            "global::Fixture.SignatureMatrix", "int", "global::Microsoft.Xna.Framework.Vector2", "string"
        ], "STATIC", "System.Reflection.MethodBase,System.Reflection.MethodInfo");
    string directEvidenceSource = ManagedDetourGenerator.DirectRegistrySource(
        [directEvidencePlan], signatureMatrix.ToDictionary(target => target.Id, StringComparer.Ordinal));
    Pass(directEvidenceSource.Contains("RecordDirectHookInvocation(\"fixture:direct-evidence\")", StringComparison.Ordinal),
        "generated direct adapter records one bounded device invocation proof");
    string staticRuntime = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/AppleEverestStaticRuntime.cs"));
    string customAudioRuntime = File.ReadAllText(Path.Combine(repository,
        "apple-everest/runtime/AppleEverestCustomAudioRuntime.cs"));
    Pass(customAudioRuntime.Contains("identity=guid-manifest", StringComparison.Ordinal) &&
         customAudioRuntime.Contains("getEventByID", StringComparison.Ordinal) &&
         !customAudioRuntime.Contains("bank.getPath", StringComparison.Ordinal),
        "stringless custom bank identity is validated by the exact GUID manifest, not reverse path lookup");
    string areaKeyApi = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/EverestAreaKeyStaticApi.cs"));
    Pass(staticRuntime.Contains("SaveData.InitializeDebugMode(loadExisting: false)", StringComparison.Ordinal) &&
         staticRuntime.Contains("saveDataBeforeModSession = SaveData.Instance", StringComparison.Ordinal) &&
         staticRuntime.Contains("SaveData.Instance = saveDataBeforeModSession", StringComparison.Ordinal),
        "content maps use an isolated debug context and restore any prior player save");
    Pass(staticRuntime.Contains("FilterVanillaFileSave", StringComparison.Ordinal) &&
         staticRuntime.Contains("mod-session-save=suppressed reason=nonpersistent", StringComparison.Ordinal) &&
         staticRuntime.Contains("CompleteNonPersistentModSession", StringComparison.Ordinal) &&
         staticRuntime.Contains("return Overworld.StartMode.MainMenu", StringComparison.Ordinal) &&
         staticRuntime.Contains("requested={requestedStartMode} applied={Overworld.StartMode.MainMenu}", StringComparison.Ordinal),
        "nonpersistent mod maps suppress temporary file writes and return through the null-safe main menu boundary");
    Pass(staticRuntime.Contains("Input.MenuConfirm.ConsumePress()", StringComparison.Ordinal) &&
         staticRuntime.Contains("Input.Jump.ConsumePress()", StringComparison.Ordinal),
        "content canary consumes its launch edge");
    using (JsonDocument declaration = JsonDocument.Parse(File.ReadAllBytes(
        Path.Combine(repository, "apple-everest/canaries/module-a/apple-static.json"))))
    {
        Pass(declaration.RootElement.GetProperty("trackedEntityTypes")[0].GetString() ==
             "Celeste.Mod.AppleEverestCanaryBanner", "canary tracked entity is explicitly declared");
    }
    string closureGenerator = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/ClosureGenerator.cs"));
    string assemblyFreezer = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/AssemblyFreezer.cs"));
    Pass(assemblyFreezer.Contains("if (element is GenericParameter) return false;", StringComparison.Ordinal),
        "live assembly-reference census handles detached rewritten generic parameters deterministically");
    Pass(closureGenerator.Contains("GeneratedAppleEverestGameplayRegistry", StringComparison.Ordinal) &&
         closureGenerator.Contains("RegisterTrackerTypes", StringComparison.Ordinal),
        "typed static gameplay registry is generated and installed into Tracker initialization");
    Pass(closureGenerator.Contains("RegisterPooledTypes", StringComparison.Ordinal) &&
         closureGenerator.Contains("TryCreatePooled", StringComparison.Ordinal) &&
         closureGenerator.Contains("new Queue<Entity>()", StringComparison.Ordinal) &&
         closureGenerator.Contains("PatchPooler", StringComparison.Ordinal),
        "external public pooled entities receive deterministic registration and typed AOT factories");
    Pass(closureGenerator.Contains("PatchLevelDataRoomNames", StringComparison.Ordinal) &&
         closureGenerator.Contains("pre-1.2.5-optional-lvl-prefix", StringComparison.Ordinal) &&
         closureGenerator.Contains("appleEverestRoomName.StartsWith", StringComparison.Ordinal),
        "pre-1.2.5 Everest maps retain exact bytes while optional lvl_ room prefixes normalize safely");
    string mapDataCompatibility = File.ReadAllText(Path.Combine(repository,
        "tools/AppleEverestBuilder/MapDataCompatibilityPatch.cs"));
    Pass(closureGenerator.Contains("MapDataCompatibilityPatch.Apply", StringComparison.Ordinal) &&
         closureGenerator.Contains("MapData.Load:pinned-everest-checkpoint-attribution-and-grow-strawberry-tracker:v3", StringComparison.Ordinal) &&
         mapDataCompatibility.Contains("AppleEverestNormalizeAndGet", StringComparison.Ordinal) &&
         mapDataCompatibility.Contains("AppleEverestCheckpointForLevel", StringComparison.Ordinal) &&
         mapDataCompatibility.Contains("Array.Copy", StringComparison.Ordinal),
        "pinned Everest strawberry checkpoint attribution and tracker growth are frozen as an exact generated-source transform");
    Pass(closureGenerator.Contains("InheritedTrackedEntityTypes", StringComparison.Ordinal) &&
         closureGenerator.Contains("typeof(global::Celeste.Trigger)", StringComparison.Ordinal) &&
         closureGenerator.Contains("inheritedBase.IsAssignableFrom(type)", StringComparison.Ordinal) &&
         closureGenerator.Contains("Tracker.TrackedEntityTypes.Add(type, trackedAs)", StringComparison.Ordinal),
        "external custom entities preserve canonical inherited Monocle tracker buckets");
    string secondCollabRuntime = File.ReadAllText(Path.Combine(repository,
        "apple-everest/runtime/AppleEverestSecondCollabRuntime.cs"));
    Pass(secondCollabRuntime.Contains("[Tracked(false)]\ninternal sealed class AppleEverestSpeedBerryCollectTrigger", StringComparison.Ordinal) &&
         !closureGenerator.Contains("builtInTrackedEntities", StringComparison.Ordinal),
        "repository-owned speed-berry trigger uses Monocle's canonical concrete tracking without duplicate generated registration");
    Pass(closureGenerator.Contains("TryCreateEntity", StringComparison.Ordinal) &&
         closureGenerator.Contains("TryCreateTrigger", StringComparison.Ordinal) &&
         closureGenerator.Contains("TryCreateBackdrop", StringComparison.Ordinal) &&
         closureGenerator.Contains("if (mod.DeclaredAssemblyPath == null)", StringComparison.Ordinal),
        "generated entity, trigger and backdrop factories are reflection-free and binary modules remain source-free");
    Pass(closureGenerator.Contains("everest/coreMessage", StringComparison.Ordinal) &&
         closureGenerator.Contains("EverestCore", StringComparison.Ordinal) &&
         File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/EverestCoreEntitiesStaticApi.cs"))
             .Contains("class CustomCoreMessage", StringComparison.Ordinal),
        "pinned Everest core message entity has an explicit typed AOT factory");
    string tagsApi = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/EverestTagsStaticApi.cs"));
    Pass(tagsApi.Contains("readonly BitTag SubHUD = Tags.HUD", StringComparison.Ordinal),
        "static Apple SubHUD facade retains the helper's high-resolution coordinate space");
    Pass(closureGenerator.Contains("internal static readonly string[] MapPaths", StringComparison.Ordinal) &&
         staticRuntime.Contains("LEVELSET: ", StringComparison.Ordinal) &&
         staticRuntime.Contains("Play Map: ", StringComparison.Ordinal) &&
         staticRuntime.Contains("AppleEverestProgressionRuntime.LaunchPersistent(selectedSid)", StringComparison.Ordinal) &&
         staticRuntime.Contains("Play Static Mod Map (Debug):", StringComparison.Ordinal) &&
         staticRuntime.Contains("LaunchModMap(selectedMap)", StringComparison.Ordinal),
        "all staged maps have distinct persistent and explicitly nonpersistent launch routes");
    Pass(staticRuntime.Contains("Play LittleEpic Room 5 (Acceptance)", StringComparison.Ordinal) &&
         staticRuntime.Contains("LaunchModMapRoom(selectedMap, \"5\")", StringComparison.Ordinal) &&
         staticRuntime.Contains("static mod acceptance room is absent", StringComparison.Ordinal),
        "unchanged LittleEpic room 5 has a bounded ordinary-session acceptance route");
    Pass(areaKeyApi.Contains("public static string GetLevelSet(this AreaKey area) => area.LevelSet;", StringComparison.Ordinal),
        "AreaKey level-set compatibility preserves registered custom LevelSets");
    string staticCompatibility = File.ReadAllText(Path.Combine(repository,
        "tools/AppleEverestBuilder/StaticAotCompatibility.cs"));
    Pass(staticCompatibility.Contains("foreach (TypeReference argument in call.GenericArguments)", StringComparison.Ordinal) &&
         staticCompatibility.Contains("delegateType.GenericArguments.Add", StringComparison.Ordinal),
        "DJ exact delegate rewrite closes factory generic arguments before device IL is emitted");
    Pass(staticCompatibility.Contains("ConditionalWeakTable<object, ExtraData>.CreateValueCallback ExtraDataFactory", StringComparison.Ordinal) &&
         staticCompatibility.Contains("Extras.GetValue(target, ExtraDataFactory)", StringComparison.Ordinal) &&
         !staticCompatibility.Contains("GetOrCreateValue", StringComparison.Ordinal),
        "static DynData storage uses an explicit AOT-compiled value factory instead of reflective generic construction");
    Pass(staticCompatibility.Contains("PatchPinnedEverestHelperAbi", StringComparison.Ordinal) &&
         staticCompatibility.Contains("public DashListener(Action<Vector2> onDash)", StringComparison.Ordinal) &&
         staticCompatibility.Contains("public Action<Vector2> SpeedSetter", StringComparison.Ordinal) &&
         closureGenerator.Contains("public Texture2D Texture_Safe", StringComparison.Ordinal) &&
         staticCompatibility.Contains("TryGetCustomDebris(out string path, char tiletype)", StringComparison.Ordinal),
        "exact ChronoHelper Everest ABI is reproduced as ordinary static Apple source");
    Pass(staticCompatibility.Contains("RewriteChronoNamespacedContentPath", StringComparison.Ordinal) &&
         staticCompatibility.Contains("Graphics/ChronoHelper/CustomSprites.xml", StringComparison.Ordinal) &&
         staticCompatibility.Contains("AppleEverest/Mods/ChronoHelper/Graphics/ChronoHelper/CustomSprites.xml",
             StringComparison.Ordinal) &&
         staticCompatibility.Contains("sourcePaths.Length != 1", StringComparison.Ordinal),
        "exact ChronoHelper sprite-bank load is pinned to its namespaced static Apple content");
    Pass(staticCompatibility.Contains("RewriteDJNamespacedContentPath", StringComparison.Ordinal) &&
         staticCompatibility.Contains("Graphics/DJMapHelperSprites.xml", StringComparison.Ordinal) &&
         staticCompatibility.Contains("AppleEverest/Mods/DJMapHelper/Graphics/DJMapHelperSprites.xml",
             StringComparison.Ordinal) &&
         staticCompatibility.Contains("DJMapHelper sprite-bank content path census drifted",
             StringComparison.Ordinal),
        "exact DJMapHelper sprite-bank load is pinned to its namespaced static Apple content");
    Pass(closureGenerator.Contains("AppleEverestAtlasMountDescriptor", StringComparison.Ordinal) &&
         closureGenerator.Contains("Graphics/Atlases/Gameplay/", StringComparison.Ordinal) &&
         closureGenerator.Contains("Graphics/Atlases/Gui/", StringComparison.Ordinal) &&
         closureGenerator.Contains("Graphics/Atlases/Journal/", StringComparison.Ordinal) &&
         closureGenerator.Contains("ThenBy(value => value.SourcePath", StringComparison.Ordinal),
        "ordinary release PNGs generate dependency-ordered gameplay, GUI, and journal atlas mounts");
    Pass(closureGenerator.Contains("PatchDeferredAtlasTextureLoading(managedRoot)", StringComparison.Ordinal) &&
         closureGenerator.Contains("CreateDeferredTexture", StringComparison.Ordinal) &&
         closureGenerator.Contains("ReadDeferredPngDimensions", StringComparison.Ordinal) &&
         closureGenerator.Contains("texture?.EnsureLoaded()", StringComparison.Ordinal) &&
         closureGenerator.Contains("IOSStorageHooks.OpenBundleFile", StringComparison.Ordinal) &&
         closureGenerator.Contains("TvOSStage6PersistenceHooks.OpenBundleFile", StringComparison.Ordinal),
        "static loose atlases preserve keys and dimensions while deferring PNG decode until first use");
    Pass(closureGenerator.Contains("PatchNonPersistentSave(Path.Combine(managedRoot, \"Celeste\", \"UserIO.cs\"))", StringComparison.Ordinal) &&
         closureGenerator.Contains("FilterVanillaFileSave(file)", StringComparison.Ordinal) &&
         closureGenerator.Contains("PatchNonPersistentOverworldReturn", StringComparison.Ordinal) &&
         closureGenerator.Contains("StartMode = global::Celeste.Mod.AppleEverestStaticRuntime.CompleteNonPersistentModSession(StartMode);", StringComparison.Ordinal),
        "locked generated-source transforms keep debug SaveData out of durable storage and normalize its overworld return");
    Pass(staticRuntime.Contains("MountStaticAtlases();", StringComparison.Ordinal) &&
         staticRuntime.Contains("VirtualContent.CreateDeferredTexture(descriptor.LogicalPath)", StringComparison.Ordinal) &&
         staticRuntime.Contains("atlas[descriptor.Key] = mounted", StringComparison.Ordinal) &&
         staticRuntime.Contains("atlas = MTN.Journal", StringComparison.Ordinal) &&
         staticRuntime.Contains("content-atlas=PASS", StringComparison.Ordinal),
        "static atlas mounts enter the live Celeste gameplay, GUI, and journal atlases without filesystem or type discovery");
    Pass(closureGenerator.Contains("typeof(global::", StringComparison.Ordinal),
        "generated AOT roots use namespace-unambiguous global type references");
    Pass(closureGenerator.Contains("AppleEverestExternalAssemblyRoots.props", StringComparison.Ordinal) &&
         closureGenerator.Contains("TrimmerRootAssembly", StringComparison.Ordinal),
        "external assembly identities generate complete trimmer roots");
    Pass(closureGenerator.Contains("ButtonBindingProperties", StringComparison.Ordinal) &&
         closureGenerator.Contains("new global::Celeste.Mod.ButtonBinding()", StringComparison.Ordinal) &&
         closureGenerator.Contains("InputBindingInitializer", StringComparison.Ordinal) &&
         closureGenerator.Contains("InitializeCurrentInput", StringComparison.Ordinal),
        "precompiled settings button bindings receive reflection-free post-input initialization");
    string iosHostProject = File.ReadAllText(Path.Combine(repository, "modern-ios/CelesteIOSRuntimeHost/CelesteIOSRuntimeHost.csproj"));
    string tvosHostProject = File.ReadAllText(Path.Combine(repository, "tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj"));
    Pass(iosHostProject.Contains("AppleEverestExternalAssemblyRoots.props", StringComparison.Ordinal) &&
         tvosHostProject.Contains("AppleEverestExternalAssemblyRoots.props", StringComparison.Ordinal),
        "both Apple hosts import generated external assembly roots");
    string loggerApi = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/EverestLoggerStaticApi.cs"));
    Pass(loggerApi.Contains("LogInterpolatedStringHandler<TLevel>", StringComparison.Ordinal) &&
         loggerApi.Contains("DefaultInterpolatedStringHandler", StringComparison.Ordinal) &&
         loggerApi.Contains("LogLevelConstTypes", StringComparison.Ordinal),
        "binary-compatible Everest interpolated logger API is AOT-safe");
    string runtimeApi = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/EverestStaticApi.cs"));
    Pass(runtimeApi.Contains("public static class Content", StringComparison.Ordinal) &&
         runtimeApi.Contains("public static readonly List<ModContent> Mods", StringComparison.Ordinal) &&
         runtimeApi.Contains("public EverestModuleMetadata Mod;", StringComparison.Ordinal) &&
         runtimeApi.Contains("public virtual EverestModuleSettings _Settings { get; set; }", StringComparison.Ordinal) &&
         runtimeApi.Contains("public virtual EverestModuleSaveData _SaveData { get; set; }", StringComparison.Ordinal) &&
         runtimeApi.Contains("public virtual EverestModuleSession _Session { get; set; }", StringComparison.Ordinal) &&
         runtimeApi.Contains("public virtual bool SaveDataAsync { get; set; } = true;", StringComparison.Ordinal) &&
         runtimeApi.Contains("public static void SetLogLevel(string tag, LogLevel level)", StringComparison.Ordinal) &&
         runtimeApi.Contains("public static void Log(string tag, string value)", StringComparison.Ordinal),
        "binary-compatible Everest content and module-state facades preserve compiled accessor shapes");
    Pass(runtimeApi.Contains("public static int AttrInt(this global::Celeste.BinaryPacker.Element element,", StringComparison.Ordinal) &&
         runtimeApi.Contains("CultureInfo.InvariantCulture", StringComparison.Ordinal) &&
         staticCompatibility.Contains("public static void Rumble(RumbleStrength strength, RumbleLength length) =>", StringComparison.Ordinal) &&
         staticCompatibility.Contains("Rumble(strength, length, null);", StringComparison.Ordinal),
        "pinned LunaticHelper keeps its exact public Everest AttrInt and two-argument rumble ABI");
    Pass(runtimeApi.Contains("public sealed class ButtonBinding", StringComparison.Ordinal) &&
         runtimeApi.Contains("public VirtualButton Button", StringComparison.Ordinal) &&
         runtimeApi.Contains("internal void InitializeCurrentInput()", StringComparison.Ordinal) &&
         runtimeApi.Contains("public bool Pressed => Button?.Pressed ?? false", StringComparison.Ordinal),
        "bounded ButtonBinding facade preserves the real default shortcut binding ABI and lifecycle");
    string settingsPersistence = File.ReadAllText(Path.Combine(repository,
        "apple-everest/runtime/AppleEverestSettingsPersistence.cs"));
    Pass(settingsPersistence.Contains("CelesteAppleEverest.Settings.v1", StringComparison.Ordinal) &&
         settingsPersistence.Contains("AppleEverest/ModuleSettings.v1", StringComparison.Ordinal) &&
         settingsPersistence.Contains("ApplicationSupportDirectory", StringComparison.Ordinal) &&
         settingsPersistence.Contains("#if TVOS", StringComparison.Ordinal) &&
         !settingsPersistence.Contains("#if TVOS_CELESTE_RUNTIME_HOST", StringComparison.Ordinal) &&
         settingsPersistence.Contains("defaults.Synchronize()", StringComparison.Ordinal),
        "module settings use separate bounded iOS Application Support and tvOS defaults adapters");
    Pass(settingsPersistence.Contains("if (!descriptor.Accepts(value))", StringComparison.Ordinal) &&
         settingsPersistence.Contains("module-settings=corrupt action=defaults", StringComparison.Ordinal),
        "module settings fail safely on invalid values or corrupt storage");
    Pass(!settingsPersistence.Contains("SaveData", StringComparison.Ordinal) &&
         !settingsPersistence.Contains("settings.celeste", StringComparison.Ordinal),
        "module settings remain isolated from vanilla Settings and SaveData");
    string closureScanner = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/RuntimeClosureScanner.cs"));
    Pass(closureScanner.Contains("AllowedStaticFacadeType", StringComparison.Ordinal) &&
         closureScanner.Contains("AllowedStaticFacadeCall", StringComparison.Ordinal) &&
         closureScanner.Contains("type.Name is \"Hook\" or \"DetourConfig\"", StringComparison.Ordinal) &&
         closureScanner.Contains("type.Name is \"DynData`1\" or \"GetDelegate`2\"", StringComparison.Ordinal) &&
         closureScanner.Contains("\"DynData`1\" => method.Name is \".ctor\" or \"get_Data\" or \"get_Item\" or \"set_Item\" or \"Get\" or \"Set\"", StringComparison.Ordinal),
        "post-link scanner permits only the exact reviewed static-AOT facade types and members");
    Pass(closureScanner.Contains("method.DeclaringType is ArrayType", StringComparison.Ordinal) &&
         closureScanner.Contains("method.Name is \".ctor\" or \"Get\" or \"Set\" or \"Address\"", StringComparison.Ordinal),
        "post-link API verifier distinguishes CLR multidimensional-array intrinsics from target APIs");
    Pass(closureScanner.Contains("AotName(source.Name.Name) + \"__\" + methodName", StringComparison.Ordinal),
        "native AOT verifier matches Mono's exact Mach-O assembly/type symbol separator");
    Pass(!Directory.EnumerateFiles(Path.Combine(repository, "apple-everest/runtime"), "*.cs").Select(File.ReadAllText)
        .Any(text => text.Contains("DynamicInvoke", StringComparison.Ordinal) || text.Contains("Assembly.Load", StringComparison.Ordinal) ||
                     text.Contains("DynamicMethod", StringComparison.Ordinal) || text.Contains("Reflection.Emit", StringComparison.Ordinal) ||
                     text.Contains("NativeDetour", StringComparison.Ordinal)), "runtime forbidden executable mutation APIs absent");
    Pass(RuntimeClosureScanner.Inspect(typeof(ResolvedMod).Assembly.Location)
        .Any(value => value.Contains("System.Diagnostics.Process::Start", StringComparison.Ordinal)),
        "linked-runtime scanner detects a real forbidden API in the host-only builder");
    IReadOnlyList<string> testClosureViolations = RuntimeClosureScanner.Inspect(
        System.Reflection.Assembly.GetExecutingAssembly().Location);
    Pass(testClosureViolations.Count == 1 &&
         testClosureViolations[0].Contains("System.Diagnostics.Process::Start", StringComparison.Ordinal),
        "linked-runtime scanner isolates the intentional desktop static-plan test host spawn");

    Pass(ProductPolicy.TransformerVersion == "apple-everest-static-v19", "real-ZIP transformer version");
    Pass(File.Exists(Path.Combine(repository, "tools/AppleEverestBuilder/AssemblyFreezer.cs")),
        "binary-first assembly freezer exists");
    string models = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/Models.cs"));
    Pass(models.Contains("OriginalSha256", StringComparison.Ordinal) && models.Contains("FrozenSha256", StringComparison.Ordinal),
        "original and transformed binary provenance model");
    string analyzerSource = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs"));
    Pass(analyzerSource.Contains("ManagedDetourCatalog.RequireByHookType", StringComparison.Ordinal) &&
         analyzerSource.Contains("ResolveDirectHookPlan", StringComparison.Ordinal),
        "generic HookGen and direct-Hook analysis uses the reviewed target catalog");
    string detourGenerator = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/ManagedDetourGenerator.cs"));
    Pass(detourGenerator.Contains("DispatcherSource", StringComparison.Ordinal) &&
         detourGenerator.Contains("DirectRegistrySource", StringComparison.Ordinal) &&
         detourGenerator.Contains("AppleEverestHookList.Version", StringComparison.Ordinal),
        "signature-driven generated dispatchers share a version-cached backend");
    Pass(!File.Exists(Path.Combine(repository, "apple-everest/runtime/OnDialogStaticDispatch.cs")) &&
         !File.Exists(Path.Combine(repository, "apple-everest/runtime/OnParticleStaticDispatch.cs")) &&
         !File.Exists(Path.Combine(repository, "apple-everest/runtime/OnTrailStaticDispatch.cs")),
        "bespoke target dispatch files removed");
    string programSource = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/Program.cs"));
    Pass(programSource.Contains("case \"audit\"", StringComparison.Ordinal) &&
         programSource.Contains("transformerVersion", StringComparison.Ordinal), "deterministic compatibility-report command");
    Pass(programSource.Contains("verify-referenced-api", StringComparison.Ordinal) &&
         programSource.Contains("verify-aot-object", StringComparison.Ordinal),
        "external binary API and native AOT body verification commands");
    Pass(File.Exists(Path.Combine(repository, "tools/AppleEverestBuilder/StaticAssetGenerator.cs")) &&
         closureGenerator.Contains("GeneratedAppleEverestStaticAssets.cs", StringComparison.Ordinal) &&
         runtimeApi.Contains("GeneratedAppleEverestStaticAssets.TryDeserialize", StringComparison.Ordinal),
        "closed typed YAML factories replace runtime YamlDotNet/reflection");
    Pass(closureGenerator.Contains("public List<Item> Items => items", StringComparison.Ordinal) &&
         closureGenerator.Contains("public SubHeader(string title) : this(title, true)", StringComparison.Ordinal) &&
         closureGenerator.Contains("ConditionHelper+AchievementHelper-reviewed-members:v2", StringComparison.Ordinal),
        "pinned AchievementHelper TextMenu public ABI is explicit and bounded");
    Pass(runtimeApi.Contains("SettingSubTextAttribute", StringComparison.Ordinal) &&
         runtimeApi.Contains("SettingRangeAttribute", StringComparison.Ordinal) &&
         runtimeApi.Contains("DefaultButtonBindingAttribute", StringComparison.Ordinal),
        "metadata-only settings attributes keep accepted frozen helper DLLs linkable");
    string modInteropGenerator = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/ModInteropPlanner.cs"));
    Pass(modInteropGenerator.Contains("ReportStatus", StringComparison.Ordinal) &&
         staticRuntime.Contains("RecordModInteropExportInvocation", StringComparison.Ordinal) &&
         staticRuntime.Contains("RecordModInteropBinding", StringComparison.Ordinal),
        "bounded ModInterop diagnostics prove bindings and first real export invocations");
    string buildScript = File.ReadAllText(Path.Combine(repository, "scripts/build-apple-everest-canary.sh"));
    Pass(buildScript.Contains("verify-referenced-api", StringComparison.Ordinal) &&
         buildScript.Contains("verify-aot-object", StringComparison.Ordinal) &&
         buildScript.Contains(".dll.llvm.o", StringComparison.Ordinal) &&
         buildScript.Contains("mono_object", StringComparison.Ordinal),
        "product gate checks both LLVM and companion Mono AOT objects");
    Pass(buildScript.Contains("--configured-fixture", StringComparison.Ordinal) &&
         buildScript.Contains("build-configured-fixture", StringComparison.Ordinal),
        "canary builder can select the exact real configured-detour production lane");
    Pass(File.Exists(Path.Combine(repository, "scripts/build-apple-everest-real-mods.sh")) &&
         File.Exists(Path.Combine(repository, "scripts/audit-apple-everest-mods.sh")),
        "ordinary-ZIP internal build and audit entry points");

    string staticIlFreeze = File.ReadAllText(Path.Combine(repository,
        "tools/AppleEverestBuilder/StaticIlFreeze.cs"));
    string staticIlWorker = File.ReadAllText(Path.Combine(repository,
        "tools/AppleEverestIlWorker/Program.cs"));
    string staticIlProject = File.ReadAllText(Path.Combine(repository,
        "tools/AppleEverestIlWorker/AppleEverestIlWorker.csproj"));
    Pass(StaticIlFreeze.FixtureName == "DashToggleHelper" && StaticIlFreeze.FixtureVersion == "1.1.0",
        "exact real frozen-IL fixture identity");
    Pass(StaticIlFreeze.FixtureZipSha256 ==
         "677e8fbd067340d7b3133cc908e4ecafc0f5deab2c38b7eeb79a62eb5f61d523",
        "real release ZIP pin");
    Pass(StaticIlFreeze.FixtureDllSha256 ==
         "531eaa8a719cb81cc84adf2b9e930dcb3abae73c60406b8f44b823c9c4b4a083",
        "authoritative precompiled DLL pin");
    Pass(StaticIlFreeze.FixtureSourceSha256 ==
         "a26ac163b4184cc0daccfd99f4ef11aeeeef7a2858b7d84938beb0dc6afd5d09",
        "source-free logical input pin");
    Pass(StaticIlFreeze.FixtureSourceCommit == "9b140684c2ee80ddae3c9ef032de0c767a67530c",
        "public source provenance pin");
    Pass(StaticIlFreeze.VortexHelperName == "VortexHelper" &&
         StaticIlFreeze.VortexHelperVersion == "1.2.19",
        "Stage 25H-C exact VortexHelper identity");
    Pass(StaticIlFreeze.VortexHelperZipSha256 ==
         "b6280fe2e3c05d355c32a51049854137c41be72d24ec4aec9d997cc7c4394db2" &&
         StaticIlFreeze.VortexHelperDllSha256 ==
         "f5a32f02c2699dcdf41bb1af808ead631491571c3d259429761953693bd37f73" &&
         StaticIlFreeze.VortexHelperSourceSha256 ==
         "c071d33bb1cc4f0387ea204834e212bd020f3143aea68c9f4e56b9bfa35def1b",
        "Stage 25H-C VortexHelper release, DLL, and logical-source locks");
    Pass(StaticIlFreeze.VortexHelperSourceCommit ==
         "b37b67b9365d769ba0fd19a67a1327260988fd6e" &&
         StaticIlFreeze.VortexHelperLicenseSha256 ==
         "051f92453f04ec0a8a9dff60882264ca949ea8e86de5f6aa96e04acdee90d359",
        "Stage 25H-C source and MIT-license provenance locks");
    Pass(StaticIlFreeze.CaeruleaName == "CaeruleaHelper" &&
         StaticIlFreeze.CaeruleaVersion == "1.11.1" &&
         StaticIlFreeze.CaeruleaZipSha256 ==
         "6a0649518d49cd0d17b84da3be53929cdd602d89d922e2d3ab87c524345e3807" &&
         StaticIlFreeze.CaeruleaDllSha256 ==
         "3c5b79a57ce03b6c98e8ae12d781ec6baddce944995b2b4928f067a5ad7973ae",
        "Stage 25H-D exact CaeruleaHelper release and DLL locks");
    Pass(StaticIlFreeze.CaeruleaSourceSha256 ==
         "036bc9adbc5471931ca6cfb1aa574d0bbcfe3a6dce9025a09b56a0c522054d07" &&
         StaticIlFreeze.CaeruleaSourceCommit ==
         "ce2ad0694feb28cd3dff0a5d7501f6e60d620fd5",
        "Stage 25H-D source-audit provenance is exact but not a production input");
    FrozenIlTransformPlan directPolicyPlan = FrozenPlan("Direct", "Direct", sharedFrozenTarget) with
    {
        Mechanism = "DIRECT_ILHOOK",
        ConstructorSignature =
            "System.Void MonoMod.RuntimeDetour.ILHook::.ctor(System.Reflection.MethodBase,MonoMod.Cil.ILContext/Manipulator)",
        TargetExpression = "typeof(Target).GetMethod(\"Run\")",
        ManipulatorExpression = "Fixture.Manipulator.Run",
        Config = "absent",
        ApplyByDefault = "implicit-true",
        Storage = "static field",
        Lifetime = "MODULE_IMMUTABLE_ACTIVE"
    };
    Pass(StaticIlFreeze.SchemaVersionFor([directPolicyPlan]) == 3 &&
         StaticIlFreeze.WorkerVersionFor([directPolicyPlan]) ==
         "apple-everest-static-il-worker-v3" &&
         StaticIlFreeze.SchemaVersionFor([FrozenPlan("Event", "Event", sharedFrozenTarget)]) == 2,
        "direct ILHook plans advance only their own deterministic plan schema");
    Pass(staticIlFreeze.Contains("VortexHelper:Player.NormalUpdate:Player_FrictionNormalUpdate",
             StringComparison.Ordinal) &&
         staticIlFreeze.Contains("VortexHelper:Player.WallJumpCheck:Player_WallJumpCheck",
             StringComparison.Ordinal) &&
         staticIlFreeze.Contains("Entities.FloorBooster+Hooks", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("Entities.PurpleBooster+Hooks", StringComparison.Ordinal),
        "Stage 25H-C freezes exactly the two selected entity-local IL registrations");
    Pass(staticIlFreeze.Contains("metadata.Dependencies.Count != 1", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("metadata.OptionalDependencies.Count != 1", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("registered VortexHelper metadata drifted", StringComparison.Ordinal),
        "Stage 25H-C VortexHelper metadata contract fails closed");
    Pass(staticIlFreeze.Contains("RewriteVortexHelper", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("RewriteLifecycleMethod(floor.Methods.Single", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("RewriteLifecycleMethod(purple.Methods.Single", StringComparison.Ordinal),
        "Stage 25H-C removes the exact nested runtime IL registrations before device AOT");
    Pass(staticIlFreeze.Contains("MonoMod.Utils intentionally remains blocked", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("live DynamicData usage", StringComparison.Ordinal),
        "Stage 25H-C does not hide VortexHelper's unrelated DynamicData blocker");
    Pass(staticIlFreeze.Contains("ValidateRegistrations", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("found.Count != plans.Count * 2", StringComparison.Ordinal),
        "exact binary IL add/remove discovery is closed");
    Pass(staticIlFreeze.Contains("registered fixture contains an unreviewed IL event subscription", StringComparison.Ordinal),
        "unexpected additional manipulator rejected");
    Pass(staticIlFreeze.Contains("registered fixture unexpectedly uses direct ILHook", StringComparison.Ordinal),
        "direct ILHook remains rejected");
    Pass(staticIlFreeze.Contains("otherwise-unreachable TypeRef row for Instruction", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("orphan.Name = assembly.MainModule.TypeSystem.Object.Name", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("orphan.Scope = assembly.MainModule.TypeSystem.Object.Scope", StringComparison.Ordinal),
        "DJMapHelper host-only orphan TypeRef is normalized away before the Mono.Cecil AssemblyRef is removed");
    Pass(closureGenerator.Contains("frozenAssemblyLogicalSha256", StringComparison.Ordinal) &&
         closureGenerator.Contains("assemblies:{frozenAssemblyLogicalSha256}", StringComparison.Ordinal) &&
         closureGenerator.Contains("custom-audio:{customAudioManifestSha256}", StringComparison.Ordinal),
        "complete closure identity transitively locks frozen DLL bytes and static custom audio");
    Pass(staticIlFreeze.Contains("CaeruleaHelper direct ILHook constructor contract drifted", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("CaeruleaHelper direct ILHook lifetime contract drifted", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("MODULE_IMMUTABLE_ACTIVE", StringComparison.Ordinal),
        "exact direct constructor, target, manipulator, storage and lifetime fail closed");
    Pass(staticIlFreeze.Contains("RewriteCaerulea", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("DashCoroutineHook", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("dashSpeed.Fields.Remove", StringComparison.Ordinal),
        "Caerulea runtime constructor, field and unload disposal are removed source-free");
    Pass(staticIlFreeze.Contains("RewriteLifecycleMethod", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("method.Body.Instructions.Clear", StringComparison.Ordinal),
        "runtime subscriptions are removed from the frozen module");
    Pass(staticIlFreeze.Contains("~TypeAttributes.VisibilityMask) | TypeAttributes.NestedPublic",
             StringComparison.Ordinal),
        "cross-assembly compiler singleton uses one atomic valid nested-public visibility value");
    Pass(staticIlFreeze.Contains("RewriteDisposableTheoEverestAbi", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("DisposableTheo pinned integer-axis ABI contract drifted", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("DisposableTheo pinned rumble ABI contract drifted", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("DisposableTheo pinned desktop settings-menu ABI contract drifted", StringComparison.Ordinal),
        "exact DisposableTheo desktop-Everest ABI normalization is bounded and fails closed");
    Pass(staticIlFreeze.Contains("frozen-IL lifecycle event count drifted", StringComparison.Ordinal) &&
         staticIlFreeze.Contains("frozen-IL On lifecycle contract drifted", StringComparison.Ordinal),
        "Load and Unload rewrites fail closed");
    Pass(staticIlFreeze.Contains("On.Celeste.CassetteBlock", StringComparison.Ordinal) == false &&
         ManagedDetourCatalog.Targets.Count == 205,
        "ordinary same-target On hooks remain catalog-driven");
    Pass(ManagedDetourCatalog.Targets.Any(target => target.Id == "celeste-crystal-static-spinner-create-sprites"),
        "same-target CrystalStaticSpinner On target registered");
    Pass(staticIlWorker.Contains("context.Invoke(manipulator)", StringComparison.Ordinal),
        "real pinned MonoMod manipulator is executed rather than reproduced");
    Pass(staticIlWorker.Contains("ResolveTarget", StringComparison.Ordinal) &&
         staticIlWorker.Contains("exact DashCoroutine iterator target is missing or ambiguous", StringComparison.Ordinal) &&
         staticIlWorker.Contains("DashCoroutine", StringComparison.Ordinal),
        "direct target resolution is exact, iterator-aware and ambiguity rejecting");
    Pass(staticIlWorker.Contains("static-noncapturing-direct", StringComparison.Ordinal) &&
         staticIlWorker.Contains("ExpectedDirectCallCounts", StringComparison.Ordinal) &&
         staticIlWorker.Contains("EmitDelegate lowering count/targets drifted", StringComparison.Ordinal),
        "modern direct EmitDelegate calls are statically lowered and counted");
    Pass(staticIlWorker.Contains("target baseline mismatch", StringComparison.Ordinal) &&
         staticIlWorker.Contains("transformed IL lock mismatch", StringComparison.Ordinal),
        "before/after/diff locks fail closed");
    Pass(staticIlWorker.Contains("dangling branch target", StringComparison.Ordinal) &&
         staticIlWorker.Contains("dangling exception-handler endpoint", StringComparison.Ordinal),
        "branch and exception-region validation");
    Pass(staticIlWorker.Contains("forbidden final IL reference", StringComparison.Ordinal) &&
         staticIlWorker.Contains("DynamicReferenceManager", StringComparison.Ordinal) &&
         staticIlWorker.Contains("Reflection.Emit", StringComparison.Ordinal),
        "dynamic injected references fail before product build");
    Pass(staticIlWorker.Contains("BeforeNormalizedIl", StringComparison.Ordinal) &&
         staticIlWorker.Contains("NormalizedDiffSha256", StringComparison.Ordinal),
        "normalized IL and semantic diff evidence emitted");
    Pass(staticIlWorker.Contains("APPLE_EVEREST_STATIC_IL_ALREADY_FROZEN", StringComparison.Ordinal) &&
         staticIlWorker.Contains("string.Equals(before, transforms[^1].AfterSha256", StringComparison.Ordinal),
        "incremental rebuild accepts only the exact locked already-frozen target body");
    Pass(staticIlProject.Contains("<TargetFramework>net10.0</TargetFramework>", StringComparison.Ordinal) &&
         !staticIlProject.Contains("MonoMod.RuntimeDetour", StringComparison.Ordinal),
        "host worker matches compiled target runtime without device detour backend");
    Pass(closureGenerator.Contains("AppleEverestStaticIl.targets", StringComparison.Ordinal) &&
         closureGenerator.Contains(".AppleEverestStaticIlHost", StringComparison.Ordinal),
        "host-only transform closure is applied at compile boundary");
    Pass(buildScript.Contains("AppleEverestIlWorker.csproj", StringComparison.Ordinal) &&
         buildScript.Contains("MonoMod.Utils.csproj", StringComparison.Ordinal),
        "public canary build prepares exact pinned host worker");
    Pass(buildScript.Contains("MonoMod.RuntimeDetour.dll", StringComparison.Ordinal) &&
         buildScript.Contains("MonoMod.Core.dll", StringComparison.Ordinal) &&
         buildScript.Contains("MonoMod.Iced.dll", StringComparison.Ordinal),
        "pinned direct manipulator receives only the exact host-side detour dependency closure");
    Pass(buildScript.Contains("djmaphelper_littleepic_fixture_sha=\"95ab02d657213031b70c3079738b21be399e567ca8a32b1fe8e8effc0778eedb\"",
             StringComparison.Ordinal) &&
         buildScript.Contains("MODS+=(\"$REPO_ROOT/apple-everest/canaries/dj-frozen-il-content\")",
             StringComparison.Ordinal),
        "exact LittleEpic DJMapHelper fixture mounts the project-owned frozen-IL behavior room");
    string djFrozenIlContent = Path.Combine(repository,
        "apple-everest/canaries/dj-frozen-il-content/Content/Maps/AppleEverest/DJFrozenIl.xml");
    Pass(File.Exists(djFrozenIlContent) &&
         File.ReadAllText(djFrozenIlContent).Contains("DJMapHelper/featherBarrier", StringComparison.Ordinal) &&
         File.ReadAllText(djFrozenIlContent).Contains("DJMapHelper/colorfulFlyFeather", StringComparison.Ordinal),
        "project-owned DJ behavior room pairs the real colored feather with the real frozen-IL barrier");
    string compiledDjFrozenIlMap = Path.Combine(NewDirectory("dj-frozen-il-map"), "Content");
    string djFrozenIlLogical = ContentCompiler.Stage(djFrozenIlContent,
        "Content/Maps/AppleEverest/DJFrozenIl.xml", compiledDjFrozenIlMap);
    IReadOnlyList<(string Kind, string Id)> djFrozenIlIds = ContentCompiler.InspectGameplayIds(
        Path.Combine(compiledDjFrozenIlMap, djFrozenIlLogical.Replace('/', Path.DirectorySeparatorChar)));
    Pass(djFrozenIlIds.Contains(("entity", "DJMapHelper/colorfulFlyFeather")) &&
         djFrozenIlIds.Contains(("entity", "DJMapHelper/featherBarrier")),
        "DJ frozen-IL canary compiles to the exact registered real helper entity IDs");
    Pass(!Directory.EnumerateFiles(Path.Combine(repository,
                 "apple-everest/canaries/dj-frozen-il-content"), "*", SearchOption.AllDirectories)
            .Any(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                         path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)),
        "tracked DJ frozen-IL canary is data-only and redistributes no third-party code");
    Pass(buildScript.Contains("static_il_fixture_sha=\"677e8fbd067340d7b3133cc908e4ecafc0f5deab2c38b7eeb79a62eb5f61d523\"",
             StringComparison.Ordinal) &&
         buildScript.Contains("MODS+=(\"$REPO_ROOT/apple-everest/canaries/static-il-content\")",
             StringComparison.Ordinal),
        "exact Stage 25H Canary product mounts the project-owned behavior room");
    string staticIlContent = Path.Combine(repository,
        "apple-everest/canaries/static-il-content/Content/Maps/AppleEverest/StaticIl.xml");
    Pass(File.Exists(staticIlContent) && File.ReadAllText(staticIlContent)
        .Contains("DashToggleHelper/DashToggleStaticSpinner", StringComparison.Ordinal),
        "project-owned real-behavior map uses the selected custom entity");
    string compiledStaticIlMap = Path.Combine(NewDirectory("static-il-map"), "Content");
    string staticIlLogical = ContentCompiler.Stage(staticIlContent,
        "Content/Maps/AppleEverest/StaticIl.xml", compiledStaticIlMap);
    IReadOnlyList<(string Kind, string Id)> staticIlIds = ContentCompiler.InspectGameplayIds(
        Path.Combine(compiledStaticIlMap, staticIlLogical.Replace('/', Path.DirectorySeparatorChar)));
    Pass(staticIlIds.Contains(("entity", "DashToggleHelper/DashToggleStaticSpinner")) &&
         staticIlIds.Contains(("entity", "DashToggleHelper/DashToggleBlock")),
        "custom namespaced XML canary compiles to exact Celeste map IDs");
    Pass(!Directory.EnumerateFiles(Path.Combine(repository, "apple-everest/canaries/static-il-content"), "*",
             SearchOption.AllDirectories).Any(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                                                       path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)),
        "tracked behavior canary contains no third-party binary or source fixture");

    Pass(buildScript.Contains("static_il_compose_fixture_sha=\"df291c0175df46682791fb6373c47eb557c47483eca3db96895eba9b5bbe85b5\"",
             StringComparison.Ordinal) &&
         buildScript.Contains("MODS+=(\"$REPO_ROOT/apple-everest/canaries/static-il-compose-content\")",
             StringComparison.Ordinal),
        "exact Stage 25H-B fixture mounts the project-owned Disposable Theo behavior room");
    string staticIlComposeContent = Path.Combine(repository,
        "apple-everest/canaries/static-il-compose-content/Content/Maps/AppleEverest/StaticIlCompose.xml");
    Pass(File.Exists(staticIlComposeContent) && File.ReadAllText(staticIlComposeContent)
        .Contains("<theoCrystal", StringComparison.Ordinal),
        "project-owned H-B behavior map uses Celeste's built-in Theo crystal");
    string compiledStaticIlComposeMap = Path.Combine(NewDirectory("static-il-compose-map"), "Content");
    string staticIlComposeLogical = ContentCompiler.Stage(staticIlComposeContent,
        "Content/Maps/AppleEverest/StaticIlCompose.xml", compiledStaticIlComposeMap);
    IReadOnlyList<(string Kind, string Id)> staticIlComposeIds = ContentCompiler.InspectGameplayIds(
        Path.Combine(compiledStaticIlComposeMap,
            staticIlComposeLogical.Replace('/', Path.DirectorySeparatorChar)));
    Pass(staticIlComposeIds.Contains(("entity", "theoCrystal")) &&
         staticIlComposeIds.Contains(("trigger", "DisposableTheoTrigger")),
        "H-B behavior map compiles the built-in Theo and exact registered Disposable Theo trigger");
    Pass(!Directory.EnumerateFiles(Path.Combine(repository,
                 "apple-everest/canaries/static-il-compose-content"), "*", SearchOption.AllDirectories)
            .Any(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                         path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)),
        "tracked H-B behavior canary contains no third-party binary or source fixture");

    string directAuditPath = Path.Combine(repository,
        "apple-everest/direct-ilhook-audit-stage25hd.json");
    using JsonDocument directAudit = JsonDocument.Parse(File.ReadAllText(directAuditPath));
    JsonElement directAuditRoot = directAudit.RootElement;
    JsonElement directSummary = directAuditRoot.GetProperty("summary");
    JsonElement directSites = directAuditRoot.GetProperty("sites");
    Pass(directSites.GetArrayLength() == 53 &&
         directAuditRoot.GetProperty("highPrioritySource").GetProperty("constructionSites").GetInt32() == 53,
        "all 53 high-priority direct construction sites are present exactly once");
    Pass(directSites.EnumerateArray().Select(site => site.GetProperty("id").GetString()).Distinct().Count() == 53 &&
         directSites.EnumerateArray().All(site =>
             site.TryGetProperty("target", out _) && site.TryGetProperty("manipulator", out _) &&
             site.TryGetProperty("config", out _) && site.TryGetProperty("dispose", out _)),
        "direct audit site identities and required semantic records are complete");
    Pass(directSummary.GetProperty("eligibleStaticUnconfiguredImmutable").GetInt32() == 12 &&
         directSummary.GetProperty("configuredSites").GetInt32() == 32 &&
         directSummary.GetProperty("dynamicGameplayLifetimeSites").GetInt32() == 3 &&
         directSummary.GetProperty("dynamicTargetSites").GetInt32() == 12 &&
         directSummary.GetProperty("dynamicManipulatorSites").GetInt32() == 0,
        "direct audit exact eligibility/config/lifetime/target/manipulator counts are locked");
    Pass(directSummary.GetProperty("multipleDirectSameTargetSites").GetInt32() == 13 &&
         directSummary.GetProperty("multipleDirectSameTargetGroups").GetInt32() == 5 &&
         directAuditRoot.GetProperty("graphPayoff").GetProperty("directIlHookSitesBefore").GetInt32() == 43 &&
         directAuditRoot.GetProperty("graphPayoff").GetProperty("directIlHookSitesAfterSelectedFixture").GetInt32() == 42,
        "same-target risk and exact QLetterAurora graph payoff are explicit");
    Pass(directSites.EnumerateArray().Single(site =>
             site.GetProperty("id").GetString() == "caerulea-dash-coroutine")
         .GetProperty("primary").GetString() == "A_STATIC_UNCONFIGURED_IMMUTABLE",
        "selected distributed Caerulea direct hook is in the bounded eligible class");
    Pass(directSites.EnumerateArray().Where(site =>
             site.GetProperty("primary").GetString() == "C_STATIC_CONFIGURED")
         .All(site => site.GetProperty("package").GetString() == "MaxHelpingHand"),
        "ambient MaxHelpingHand detour contexts are never mistaken for plain hooks");
    string directContent = Path.Combine(repository,
        "apple-everest/canaries/static-direct-ilhook-content/Content/Maps/AppleEverest/StaticDirectIlHook.xml");
    Pass(File.Exists(directContent) && File.ReadAllText(directContent)
        .Contains("CaeruleaHelper/NoDashSpeedResetTrigger", StringComparison.Ordinal),
        "project-owned H-D map exposes the real helper trigger backed by the direct transform");
    string compiledDirectMap = Path.Combine(NewDirectory("static-direct-ilhook-map"), "Content");
    string directLogical = ContentCompiler.Stage(directContent,
        "Content/Maps/AppleEverest/StaticDirectIlHook.xml", compiledDirectMap);
    IReadOnlyList<(string Kind, string Id)> directIds = ContentCompiler.InspectGameplayIds(
        Path.Combine(compiledDirectMap, directLogical.Replace('/', Path.DirectorySeparatorChar)));
    Pass(directIds.Contains(("trigger", "CaeruleaHelper/NoDashSpeedResetTrigger")),
        "H-D namespaced trigger wrapper compiles to the exact real custom trigger ID");
    Pass(!Directory.EnumerateFiles(Path.Combine(repository,
                 "apple-everest/canaries/static-direct-ilhook-content"), "*", SearchOption.AllDirectories)
            .Any(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                         path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)),
        "tracked H-D behavior canary contains no third-party binary or source fixture");

    Console.WriteLine($"PASS: AppleEverestBuilder deterministic tests ({passed})");
}
catch (Exception exception)
{
    // Return a normal failing exit status; avoid macOS crash-report hangs for
    // an ordinary assertion failure, while still running temporary cleanup.
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}
finally
{
    if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
}
