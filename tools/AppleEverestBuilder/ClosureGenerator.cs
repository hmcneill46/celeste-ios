using System.Text;
using System.Text.Json;
using System.Security;
using System.Xml;
using Mono.Cecil;

namespace AppleEverestBuilder;

internal static class ClosureGenerator
{
    private const string CoreSessionSchema = "ae9843c8fa67f56bc55f2bbfa877a2f83bb84847cf9c7b493491f5049fd950a2";
    private static bool NeedsSelectedCoreSession(IReadOnlyList<ResolvedMod> mods) =>
        mods.Any(mod => mod.StaticSemanticLowering?.RuntimeFiles?.Contains("AppleEverestCoreState.cs", StringComparer.Ordinal) == true);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    internal static readonly (string Kind, string Id, string Owner)[] CoreGameplayFactories =
    {
        ("entity", "everest/coreMessage", "EverestCore"),
        ("trigger", "everest/changeInventoryTrigger", "EverestCore"),
        ("trigger", "everest/coreModeTrigger", "EverestCore"),
        ("trigger", "everest/crystalShatterTrigger", "EverestCore"),
        ("trigger", "everest/flagTrigger", "EverestCore"),
        ("trigger", "everest/smoothCameraOffsetTrigger", "EverestCore")
    };

    public static void Generate(
        AppleEverestProfile profile,
        IReadOnlyList<ResolvedMod> ordered,
        string repositoryRoot,
        string outputRoot,
        SelectedContentPlan? contentPlan = null)
    {
        string managed = Path.Combine(outputRoot, "managed");
        string content = Path.Combine(outputRoot, "content", "Content");
        string assemblies = Path.Combine(outputRoot, "assemblies");
        Directory.CreateDirectory(managed);
        Directory.CreateDirectory(content);

        string runtimeRoot = Path.Combine(repositoryRoot, "apple-everest", "runtime");
        bool hasSemanticRuntime = ordered.Any(mod => mod.StaticSemanticLowering?.RuntimeFiles?.Count > 0);
        foreach (string source in Directory.EnumerateFiles(runtimeRoot, "*.cs").OrderBy(Path.GetFileName, StringComparer.Ordinal))
            File.Copy(source, Path.Combine(managed, Path.GetFileName(source)), overwrite: false);
        foreach (string name in ordered.SelectMany(mod => mod.StaticSemanticLowering?.RuntimeFiles ?? [])
                     .Append("AppleEverestSelectedProfileGuard.cs").Append("AppleEverestSnasProfileGuard.cs")
                     .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
        {
            if (Path.GetFileName(name) != name || !name.EndsWith(".cs", StringComparison.Ordinal))
                throw new InvalidDataException("invalid registered semantic runtime source");
            File.Copy(Path.Combine(runtimeRoot, "semantics", name), Path.Combine(managed, name), overwrite: false);
        }

        List<(ResolvedMod Mod, AppleStaticDeclaration Declaration)> codeModules = [];
        List<ContentMountRecord> stagedContent = [];
        foreach (SelectedContentPlan.Entry entry in contentPlan?.EverestContent ?? [])
        {
            string source = Path.Combine(repositoryRoot, ".build", "apple-everest", "upstream", "Everest", "Celeste.Mod.mm", "Content", entry.Path);
            if (profile.Everest.Sha256Commit != "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00" ||
                Hashing.FileSha256(source) != entry.Sha256)
                throw new InvalidDataException("selected Everest core content differs from pinned source");
            string logical = ContentCompiler.Stage(source, NormalizeContentPath("Everest", entry.Path), content);
            stagedContent.Add(new ContentMountRecord("Everest", -1, entry.Path, logical, entry.Sha256, entry.Sha256));
        }
        List<FrozenAssemblyRecord> frozenAssemblies = [];
        List<(CustomAudioBankPlan Plan, int ModuleOrdinal, int BankOrdinal)> unorderedCustomAudioBanks = [];
        IReadOnlyList<FrozenIlTransformPlan> frozenIlTransforms = ComposeFrozenIlTransforms(ordered);
        int sourceIndex = 0;
        for (int modOrder = 0; modOrder < ordered.Count; modOrder++)
        {
            ResolvedMod mod = ordered[modOrder];
            if (mod.StaticSemanticLowering?.Module is { } semanticModule)
            {
                ValidateDeclaration(semanticModule.Declaration, mod.Metadata.Name);
                codeModules.Add((mod, semanticModule.Declaration));
            }
            else if (!string.IsNullOrWhiteSpace(mod.Metadata.DLL) && mod.StaticSemanticLowering == null)
            {
                AppleStaticDeclaration declaration = mod.Declaration
                    ?? throw new InvalidDataException($"{mod.Metadata.Name} has no closed module declaration");
                ValidateDeclaration(declaration, mod.Metadata.Name);
                codeModules.Add((mod, declaration));
                if (mod.DeclaredAssemblyPath != null)
                {
                    FreezeManagedAssemblyClosure(mod, assemblies, frozenAssemblies);
                }
                // A binary module's declared DLL is the complete production input. Some ordinary
                // release ZIPs also contain incidental obj/Debug-generated C# files; compiling those
                // would create a source dependency and can duplicate assembly attributes. Source
                // modules deliberately have no declared assembly path and retain the existing closed
                // apple-static.json source path.
                if (mod.DeclaredAssemblyPath == null)
                {
                    foreach (string relative in mod.ManagedFiles.Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                    {
                        string source = Path.Combine(mod.Input.StagingRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                        string destination = Path.Combine(managed, $"Mod{sourceIndex++:D3}_{Path.GetFileName(relative)}");
                        File.Copy(source, destination, overwrite: false);
                    }
                }
            }
            foreach (string relative in contentPlan?.Files(mod) ?? mod.ContentFiles)
            {
                string source = Path.Combine(mod.Input.StagingRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                string stagedPath = NormalizeContentPath(mod.Metadata.Name, relative);
                string logical = contentPlan?.Preserve(mod.Metadata.Name, relative) == true
                    ? ContentCompiler.Stage(source, stagedPath, content, preserveOriginalMap: true)
                    : StaticSemanticLowering.StageContent(mod.StaticSemanticLowering, source, relative, stagedPath, content);
                stagedContent.Add(new ContentMountRecord(mod.Metadata.Name, modOrder, relative, logical,
                    Hashing.FileSha256(Path.Combine(content, logical.Replace('/', Path.DirectorySeparatorChar))),
                    Hashing.FileSha256(source)));
            }
            for (int bankOrdinal = 0; bankOrdinal < mod.CustomAudioBanks.Count; bankOrdinal++)
                unorderedCustomAudioBanks.Add((mod.CustomAudioBanks[bankOrdinal], modOrder, bankOrdinal));
        }

        IReadOnlyList<(CustomAudioBankPlan Plan, int Ordinal)> customAudioBanks =
            CustomAudioManifest.Order(unorderedCustomAudioBanks);
        CustomAudioManifest.ValidateGraph(customAudioBanks);
        foreach ((CustomAudioBankPlan bank, _) in customAudioBanks)
        {
            ContentMountRecord[] mounts = stagedContent.Where(mount => mount.Owner == bank.Owner &&
                mount.SourcePath == bank.SourcePath && mount.LogicalPath == bank.StagedPath).ToArray();
            if (mounts.Length != 1 || mounts[0].Sha256 != bank.BankSha256)
                throw new InvalidDataException($"custom FMOD bank was not staged exactly once: {bank.Owner}/{bank.SourcePath}");
        }
        string customAudioText = CustomAudioManifest.CanonicalManifest(customAudioBanks);
        string customAudioManifestSha256 = Hashing.BytesSha256(Encoding.UTF8.GetBytes(customAudioText));
        string customBankLogicalSet = CustomAudioManifest.LogicalSet(customAudioBanks);
        string customBankLogicalSetSha256 = Hashing.BytesSha256(Encoding.UTF8.GetBytes(customBankLogicalSet));
        File.WriteAllText(Path.Combine(outputRoot, "custom-audio-manifest.txt"), customAudioText, new UTF8Encoding(false));

        AppleCustomEntityFactory[] customFactories = codeModules.SelectMany(item => item.Declaration.CustomEntityFactories).ToArray();
        AppleCustomBackdropFactory[] backdropFactories = codeModules.SelectMany(item => item.Declaration.CustomBackdropFactories).ToArray();
        string[] duplicateFactoryIds = customFactories.GroupBy(value => value.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (duplicateFactoryIds.Length > 0)
            throw new InvalidDataException("duplicate custom entity IDs across resolved modules: " + string.Join(",", duplicateFactoryIds));
        string[] duplicateBackdropIds = backdropFactories.GroupBy(value => value.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (duplicateBackdropIds.Length > 0)
            throw new InvalidDataException("duplicate custom backdrop IDs across resolved modules: " + string.Join(",", duplicateBackdropIds));
        var factoryOwners = codeModules.SelectMany(item =>
                item.Declaration.CustomEntityFactories.Select(factory => new
                    { Id = factory.Id, Kind = factory.Kind, Owner = item.Mod.Metadata.Name })
                .Concat(item.Declaration.CustomBackdropFactories.Select(factory => new
                    { Id = factory.Id, Kind = "backdrop", Owner = item.Mod.Metadata.Name })))
            .Concat(ordered.Where(mod => mod.StaticSemanticLowering != null).SelectMany(mod =>
                mod.StaticSemanticLowering!.Factories.Select(factory => new
                    { factory.Id, factory.Kind, Owner = mod.Metadata.Name })))
            .Concat(CoreGameplayFactories.Select(factory => new
                { factory.Id, factory.Kind, factory.Owner }))
            .ToDictionary(value => value.Kind + "\0" + value.Id, StringComparer.Ordinal);
        var mapGameplayIds = stagedContent.Where(value => value.LogicalPath.StartsWith("Maps/", StringComparison.Ordinal) &&
                value.LogicalPath.EndsWith(".bin", StringComparison.Ordinal))
            .SelectMany(value => ContentCompiler.InspectGameplayIds(Path.Combine(content,
                value.LogicalPath.Replace('/', Path.DirectorySeparatorChar))).Select(item =>
                new { map = value.LogicalPath, kind = item.Kind, id = item.Id }))
            .OrderBy(value => value.map, StringComparer.Ordinal).ThenBy(value => value.kind, StringComparer.Ordinal)
            .ThenBy(value => value.id, StringComparer.Ordinal).ToArray();
        Dictionary<string, AppleOmittedCustomEntityFactory> omittedMapFactories = codeModules
            .SelectMany(item => item.Declaration.OmittedCustomEntityFactories)
            .ToDictionary(value => "entity\0" + value.Id, StringComparer.Ordinal);
        string[] unsupportedMapFactories = mapGameplayIds.Where(value =>
                omittedMapFactories.ContainsKey(value.kind + "\0" + value.id))
            .Select(value => value.map + ":" + value.kind + ":" + value.id)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (unsupportedMapFactories.Length > 0)
            throw new InvalidDataException("map references runtime-only custom entities without a static map factory: " +
                                           string.Join(",", unsupportedMapFactories));
        string[] unresolvedNamespacedFactories = mapGameplayIds.Where(value => value.id.Contains('/') &&
                !factoryOwners.ContainsKey(value.kind + "\0" + value.id))
            .Select(value => value.map + ":" + value.kind + ":" + value.id)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (unresolvedNamespacedFactories.Length > 0)
            throw new InvalidDataException("map references namespaced gameplay factories without an exact static implementation: " +
                                           string.Join(",", unresolvedNamespacedFactories));
        var resolvedMapFactories = mapGameplayIds.Where(value => factoryOwners.ContainsKey(value.kind + "\0" + value.id))
            .Select(value => new { value.map, value.kind, value.id,
                owner = factoryOwners[value.kind + "\0" + value.id].Owner })
            .ToArray();

        if (mapGameplayIds.Any(value => value.kind == "entity" && value.id == "MaxHelpingHand/SidewaysJumpThru"))
            frozenIlTransforms = SelectedSidewaysIlPlans.Append(ordered, frozenIlTransforms);

        (string durabilitySource, IReadOnlyDictionary<string, GeneratedDurabilityAdapter> durabilityAdapters) =
            DurabilityAdapterGenerator.Generate(codeModules);
        string durabilityClosureSha256 = Hashing.BytesSha256(Encoding.UTF8.GetBytes(string.Join("\n",
            codeModules.OrderBy(item => item.Mod.Metadata.Name, StringComparer.Ordinal).Select(item =>
                item.Mod.Metadata.Name + "\t" + item.Mod.Metadata.Version + "\t" + item.Mod.Input.SourceSha256 + "\t" +
                (durabilityAdapters.TryGetValue(item.Mod.Metadata.Name, out GeneratedDurabilityAdapter? adapter)
                    ? adapter.Schema : "none")))));
        if (NeedsSelectedCoreSession(ordered))
            durabilityClosureSha256 = Hashing.BytesSha256(Encoding.UTF8.GetBytes(durabilityClosureSha256 +
                "\nEverest\t" + profile.Everest.Tag + "\t" + profile.Everest.Sha256Commit + "\t" + CoreSessionSchema));
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestModuleDurabilityAdapters.cs"),
            durabilitySource, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestModuleRegistry.cs"),
            RegistrySource(profile, codeModules, ordered, durabilityAdapters, durabilityClosureSha256), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestGameplayRegistry.cs"),
            GameplayRegistrySource(codeModules, ordered), new UTF8Encoding(false));
        HashSet<string> staticallyLoweredStrawberryEntities = ordered
            .Where(mod => mod.StaticSemanticLowering != null)
            .SelectMany(mod => mod.StaticSemanticLowering!.Factories)
            .Where(StaticSemanticLowering.CountsAsStrawberry)
            .Select(factory => factory.Id)
            .ToHashSet(StringComparer.Ordinal);
        MapProgressionRecord[] progressionMaps = stagedContent
            .Where(value => value.LogicalPath.StartsWith("Maps/", StringComparison.Ordinal) &&
                            value.LogicalPath.EndsWith(".bin", StringComparison.Ordinal))
            .Select(value => ContentCompiler.InspectProgression(
                Path.Combine(content, value.LogicalPath.Replace('/', Path.DirectorySeparatorChar)),
                value.LogicalPath, value.SourceSha256, staticallyLoweredStrawberryEntities))
            .OrderBy(value => value.Sid, StringComparer.Ordinal).ToArray();
        MapBindingsGenerator.Binding[] mapBindings = MapBindingsGenerator.Bind(progressionMaps, stagedContent, contentPlan);
        if (ordered.Any(mod => mod.Metadata.Name == "MaxHelpingHand" && mod.StaticSemanticLowering != null))
        {
            SnasFlagGroups.Group[] groups = SnasFlagGroups.Create(stagedContent, content, ordered);
            File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestFlagGroups.cs"),
                SnasFlagGroups.Source(groups), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outputRoot, "flag-groups.json"),
                JsonSerializer.Serialize(groups, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestMapBindings.cs"),
            MapBindingsGenerator.Source(mapBindings, ordered), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputRoot, "map-bindings.json"),
            JsonSerializer.Serialize(mapBindings, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestContentManifest.cs"),
            ContentManifestSource(ordered, stagedContent, content, mapBindings), new UTF8Encoding(false));
        CollabGeneration collab = CollabManifestGenerator.Generate(ordered, stagedContent, content, progressionMaps);
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestCollabManifest.cs"),
            collab.Source, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputRoot, "collab-manifest.txt"),
            collab.ManifestText, new UTF8Encoding(false));
        LevelSetProgressionRecord[] progressionLevelSets = LevelSetProgressionManifest.Create(progressionMaps);
        string levelSetManifestText = LevelSetProgressionManifest.Text(progressionLevelSets);
        string progressionManifestSource = ProgressionManifestSource(progressionMaps, progressionLevelSets);
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestProgressionManifest.cs"),
            progressionManifestSource, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputRoot, "levelset-progression-manifest.txt"),
            ProgressionManifestText(progressionMaps), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputRoot, "levelset-manifest.txt"),
            levelSetManifestText, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestCustomAudioManifest.cs"),
            CustomAudioManifestSource(customAudioBanks, customAudioManifestSha256), new UTF8Encoding(false));
        StaticAssetGeneration staticAssets = StaticAssetGenerator.Generate(codeModules, stagedContent, content);
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestStaticAssets.cs"), staticAssets.Source, new UTF8Encoding(false));
        string staticAotSource = StaticAotCompatibility.GeneratedSource(ordered);
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestAotRoots.cs"),
            RootsSource(codeModules, staticAotSource.Length != 0), new UTF8Encoding(false));
        if (staticAotSource.Length != 0)
            File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestStaticAotCompatibility.cs"),
                staticAotSource, new UTF8Encoding(false));
        IReadOnlyList<ManagedDetourTarget> detourTargets = ManagedDetourCatalog.Targets;
        IReadOnlyList<DirectManagedHookPlan> directPlans = ordered.SelectMany(mod => mod.DirectManagedHooks).ToArray();
        IReadOnlyDictionary<string, ManagedDetourTarget> detourTargetsById = detourTargets.ToDictionary(target => target.Id, StringComparer.Ordinal);
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestManagedDetours.cs"),
            ManagedDetourGenerator.DispatcherSource(detourTargets), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestConfiguredOrdinals.cs"),
            StaticConfiguredDetourCompatibility.GeneratedOrdinalSource(ordered), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestDirectHooks.cs"),
            ManagedDetourGenerator.DirectRegistrySource(directPlans, detourTargetsById), new UTF8Encoding(false));
        GeneratedModInteropPlan modInterop = ModInteropPlanner.Generate(ordered);
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestModInterop.cs"),
            modInterop.Source, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(managed, "AppleEverestExternalAssemblyRoots.props"),
            ExternalAssemblyRootsSource(frozenAssemblies), new UTF8Encoding(false));
        string frozenIlPlanSha256 = StaticIlFreeze.PlanSha256(frozenIlTransforms);
        string configuredPlanSha256 = Hashing.BytesSha256(Encoding.UTF8.GetBytes(string.Join("\n",
            ordered.Where(mod => mod.StaticConfiguredDetours != null)
                .OrderBy(mod => mod.Metadata.Name, StringComparer.Ordinal)
                .Select(mod => mod.StaticConfiguredDetours!.PlanSha256))));
        if (frozenIlTransforms.Count > 0)
            PrepareStaticIlHost(repositoryRoot, ordered, frozenIlTransforms, outputRoot, managed);

        IReadOnlyList<FileRecord> managedInventory = Hashing.Inventory(managed);
        IReadOnlyList<FileRecord> contentInventory = Hashing.Inventory(content);
        string registryHash = Hashing.FileSha256(Path.Combine(managed, "GeneratedAppleEverestModuleRegistry.cs"));
        string apiSurfaceHash = AppleApiSurface.ContractSha256;
        string hookTransformHash = Hashing.BytesSha256(Encoding.UTF8.GetBytes(
            TargetPatchContract + "\nAppleApiSurface:" + apiSurfaceHash +
            (hasSemanticRuntime ? "\nSemanticRuntime:" + StaticSemanticRuntimePatches.ContractSha256 : "")));
        string managedHash = Hashing.LogicalHash(managedInventory);
        string contentHash = Hashing.LogicalHash(contentInventory);
        string frozenAssemblyLogicalSha256 = Hashing.BytesSha256(Encoding.UTF8.GetBytes(string.Join("\n",
            frozenAssemblies.OrderBy(value => value.AssemblyName, StringComparer.Ordinal)
                .Select(value => string.Join("\0", value.Owner, value.AssemblyName, value.FileName,
                    value.OriginalSha256, value.FrozenSha256)))));
        string sharedClosureHash = Hashing.BytesSha256(Encoding.UTF8.GetBytes(
            $"{ProductPolicy.TransformerVersion}\nmanaged:{managedHash}\ncontent:{contentHash}\n" +
            $"assemblies:{frozenAssemblyLogicalSha256}\nregistry:{registryHash}\nhooks:{hookTransformHash}\n" +
            $"api-surface:{apiSurfaceHash}\nstatic-il:{frozenIlPlanSha256}\n" +
            $"configured-order:{configuredPlanSha256}\n" +
            $"custom-audio:{customAudioManifestSha256}\ncustom-banks:{customBankLogicalSetSha256}\n" +
            $"mod-interop:{modInterop.PlanSha256}\n" +
            $"progression:{Hashing.BytesSha256(Encoding.UTF8.GetBytes(progressionManifestSource))}\n" +
            $"collab:{collab.Sha256}\n"));
        object manifest = new
        {
            schemaVersion = 1,
            profile = profile.Profile,
            transformerVersion = ProductPolicy.TransformerVersion,
            canonicalClass = ProductPolicy.CanonicalClass,
            everestSha = profile.Everest.Sha256Commit,
            monoModSha = profile.Dependencies.MonoModCommit,
            runtimeDllLoading = false,
            precompiledAssembliesAotLinked = frozenAssemblies.Count,
            runtimeDetour = directPlans.Count > 0 ? "static-data-only" : "absent",
            managedDetourCatalogSchema = 2,
            managedDetourTargetCount = detourTargets.Count,
            directManagedHookCount = directPlans.Count,
            configuredOrderingBehavior = ConfiguredDetourOrdering.SemanticVersion,
            configuredPlanCount = ordered.Count(mod => mod.StaticConfiguredDetours != null),
            configuredPlanSha256,
            runtimeConfiguredGraph = false,
            modInteropSchema = 1,
            modInteropBehavior = "monomod-dfc30a1506d37fb88a2c2be004f525205f46a24c-static-v1",
            modInteropPlanSha256 = modInterop.PlanSha256,
            modInteropRegistrationCount = modInterop.RegistrationCount,
            modInteropExportCount = modInterop.ExportCount,
            modInteropImportCount = modInterop.ImportCount,
            modInteropResolvedImportCount = modInterop.ResolvedImportCount,
            modInteropPlan = modInterop.Manifest,
            frozenIlSchema = frozenIlTransforms.Count == 0 ? 0 : StaticIlFreeze.SchemaVersionFor(frozenIlTransforms),
            frozenIlWorker = frozenIlTransforms.Count == 0 ? "absent" : StaticIlFreeze.WorkerVersionFor(frozenIlTransforms),
            frozenIlPlanSha256,
            frozenAssemblyLogicalSha256,
            customAudioSchema = CustomAudioManifest.Schema,
            customAudioManifestSha256,
            customBankLogicalSetSha256,
            customAudioBankCount = customAudioBanks.Count,
            customAudioEventCount = customAudioBanks.Sum(item => item.Plan.Guids.Count(value => value.Kind == "event")),
            customAudioLoadPolicy = CustomAudioManifest.LoadPolicy,
            customAudioSampleDataPolicy = CustomAudioManifest.SampleDataPolicy,
            customAudioLifecyclePolicy = CustomAudioManifest.LifecyclePolicy,
            customAudioBanks = customAudioBanks.Select(item => new
            {
                owner = item.Plan.Owner,
                version = item.Plan.Version,
                sourceArchiveSha256 = item.Plan.SourceArchiveSha256,
                sourcePath = item.Plan.SourcePath,
                bankSha256 = item.Plan.BankSha256,
                guidSourcePath = item.Plan.GuidSourcePath,
                guidSha256 = item.Plan.GuidSha256,
                loadOrdinal = item.Ordinal,
                bankId = item.Plan.BankId,
                bankPath = item.Plan.BankPath,
                stagedPath = item.Plan.StagedPath,
                guids = item.Plan.Guids.Select(value => new { value.Id, value.Path, value.Kind }).ToArray(),
                collisions = 0
            }).ToArray(),
            frozenIlTransformCount = frozenIlTransforms.Count,
            frozenIlTransforms = frozenIlTransforms.Select(plan => new
            {
                plan.PlanId,
                plan.Owner,
                plan.AssemblySha256,
                plan.EventType,
                plan.EventName,
                plan.TargetMethod,
                plan.CanonicalTargetMethod,
                plan.ManipulatorType,
                plan.ManipulatorMethod,
                plan.ManipulatorIsStatic,
                plan.RegistrationOrdinal,
                plan.BeforeSha256,
                plan.AfterSha256,
                plan.DiffSha256,
                plan.ExpectedDelegateTargets,
                plan.Mechanism,
                plan.Lifetime,
                runtimeUnload = plan.Mechanism == SelectedSidewaysIlPlans.Mechanism ? "typed-level-lifetime-predicate" : "unsupported-immutable-active"
            }).ToArray(),
            customEntityFactoryCount = customFactories.Count(value => value.Kind == "entity") +
                                       ordered.Where(mod => mod.StaticSemanticLowering != null)
                                           .SelectMany(mod => mod.StaticSemanticLowering!.Factories)
                                           .Count(value => value.Kind == "entity") +
                                       CoreGameplayFactories.Count(value => value.Kind == "entity"),
            customTriggerFactoryCount = customFactories.Count(value => value.Kind == "trigger") +
                                        ordered.Where(mod => mod.StaticSemanticLowering != null)
                                            .SelectMany(mod => mod.StaticSemanticLowering!.Factories)
                                            .Count(value => value.Kind == "trigger") +
                                        CoreGameplayFactories.Count(value => value.Kind == "trigger"),
            staticSemanticFactoryCount = ordered.Where(mod => mod.StaticSemanticLowering != null)
                .SelectMany(mod => mod.StaticSemanticLowering!.Factories).Count(),
            customBackdropFactoryCount = backdropFactories.Length + ordered.Sum(mod =>
                mod.StaticSemanticLowering?.Factories.Count(factory => factory.Kind == "backdrop") ?? 0),
            moduleSettingCount = codeModules.Sum(item => item.Declaration.SettingsProperties.Length),
            moduleDurabilityAdapterCount = durabilityAdapters.Count,
            moduleDurabilityFormat = "per-slot-aggregate-ab-v1",
            moduleDurabilityClosureSha256 = durabilityClosureSha256,
            moduleDurabilityLogicalMaximumBytes = 2 * 1024 * 1024,
            moduleDurabilityTvOSLogicalMaximumBytes = 512 * 1024,
            moduleDurabilityTvOSReplicaMaximumBytes = 126976,
            moduleDurabilityTvOSTotalMaximumBytes = 6 * 126976,
            levelSetProgressionSchema = ProductPolicy.LevelSetProgressionSchemaVersion,
            collabSchema = 1,
            collabManifestSha256 = collab.Sha256,
            collabCount = collab.Collabs.Count,
            collabMapCount = collab.Collabs.Sum(value => value.Maps.Count),
            levelSetProgressionFormat = "typed-sidecar-projection-lineage-ab-v1",
            levelSetProgressionManifestSha256 = Hashing.BytesSha256(Encoding.UTF8.GetBytes(ProgressionManifestText(progressionMaps))),
            levelSetProgressionMapCount = progressionMaps.Length,
            levelSetManifestSchema = 1,
            levelSetManifestSha256 = Hashing.BytesSha256(Encoding.UTF8.GetBytes(levelSetManifestText)),
            levelSetCount = progressionLevelSets.Length,
            levelSetIdentityPolicy = "ordered-member-map-compatibility-identities-v1",
            levelSetProgressionCompatibilityPolicy = "sid-plus-original-map-sha256-v1",
            levelSetProgressionSessionPolicy = "bounded-resumable-session-v1",
            interpreter = false,
            selectedMods = ordered.Select((mod, index) => new
            {
                order = index,
                name = mod.Metadata.Name,
                version = mod.Metadata.Version,
                sourceHash = mod.Input.SourceSha256,
                dependencies = mod.Metadata.Dependencies.Select(dep => new { dep.Name, dep.Version }).ToArray(),
                optionalDependencies = mod.Metadata.OptionalDependencies.Select(dep => new { dep.Name, dep.Version }).ToArray(),
                conflicts = mod.Metadata.Conflicts.Select(dep => new { dep.Name, dep.Version }).ToArray(),
                classification = mod.Classification.ToString(),
                durability = codeModules.FirstOrDefault(item => ReferenceEquals(item.Mod, mod)) is var code && code.Mod != null
                    ? new
                    {
                        code.Declaration.Durability.SaveDataClass,
                        code.Declaration.Durability.SessionClass,
                        code.Declaration.Durability.AsyncClass,
                        code.Declaration.SaveDataType,
                        code.Declaration.SessionType,
                        schemaSha256 = durabilityAdapters.TryGetValue(mod.Metadata.Name, out GeneratedDurabilityAdapter? durability)
                            ? durability.Schema : null
                    }
                    : null,
                mechanisms = mod.Mechanisms,
                staticAotCompatibility = mod.StaticAotCompatibility?.Id,
                staticSemanticLowering = mod.StaticSemanticLowering,
                staticConfiguredDetours = mod.StaticConfiguredDetours?.Id,
                staticConfiguredPlanSha256 = mod.StaticConfiguredDetours?.PlanSha256,
                modInteropRegistrations = mod.ModInteropRegistrations.Select(registration => registration.RegisteredType).ToArray(),
                frozenIlTransforms = frozenIlTransforms.Where(plan => plan.Owner == mod.Metadata.Name).Select(plan => plan.PlanId).ToArray(),
                customAudioBanks = mod.CustomAudioBanks.Select(bank => bank.BankPath).ToArray(),
                managedFiles = mod.ManagedFiles,
                contentFiles = (contentPlan?.Files(mod) ?? mod.ContentFiles).ToArray()
            }).ToArray(),
            resolvedOrder = ordered.Select(mod => mod.Metadata.Name).ToArray(),
            managedFileCount = managedInventory.Count,
            managedLogicalSha256 = managedHash,
            contentFileCount = contentInventory.Count,
            contentLogicalSha256 = contentHash,
            registrySha256 = registryHash,
            hookTransformSha256 = hookTransformHash,
            staticSemanticRuntimePatchSha256 = hasSemanticRuntime ? StaticSemanticRuntimePatches.ContractSha256 : null,
            appleApiSurfaceSha256 = apiSurfaceHash,
            appleApiSurfaceMemberCount = AppleApiSurface.Members.Count,
            contentMounts = stagedContent.Select(mount => new
            {
                owner = mount.Owner,
                order = mount.Order,
                sourcePath = mount.SourcePath,
                logicalPath = mount.LogicalPath,
                sha256 = mount.Sha256
            }).ToArray(),
            staticAssetDeserializerTypeCount = staticAssets.TypeCount,
            staticAssetFactoryCount = staticAssets.FactoryCount,
            frozenAssemblies = frozenAssemblies.Select(assembly => new
            {
                owner = assembly.Owner,
                assemblyName = assembly.AssemblyName,
                fileName = assembly.FileName,
                originalSha256 = assembly.OriginalSha256,
                frozenSha256 = assembly.FrozenSha256
            }).ToArray(),
            customEntityFactories = codeModules.SelectMany(item => item.Declaration.CustomEntityFactories.Select(factory => new
            {
                id = factory.Id,
                kind = factory.Kind,
                type = factory.Type,
                constructor = factory.Constructor,
                owner = item.Mod.Metadata.Name
            })).OrderBy(value => value.id, StringComparer.Ordinal).ToArray(),
            omittedCustomEntityFactories = codeModules.SelectMany(item => item.Declaration.OmittedCustomEntityFactories.Select(factory => new
            {
                id = factory.Id,
                type = factory.Type,
                reason = factory.Reason,
                owner = item.Mod.Metadata.Name
            })).OrderBy(value => value.id, StringComparer.Ordinal).ToArray(),
            coreGameplayFactories = CoreGameplayFactories.Select(factory => new
            {
                kind = factory.Kind,
                id = factory.Id,
                owner = factory.Owner
            }).ToArray(),
            customBackdropFactories = codeModules.SelectMany(item => item.Declaration.CustomBackdropFactories.Select(factory => new
            {
                id = factory.Id,
                type = factory.Type,
                factory = factory.Factory,
                method = factory.Method,
                owner = item.Mod.Metadata.Name
            })).OrderBy(value => value.id, StringComparer.Ordinal).ToArray(),
            moduleSettings = codeModules.SelectMany(item => item.Declaration.SettingsProperties.Select(property => new
            {
                owner = item.Mod.Metadata.Name,
                property.Name,
                property.Label,
                property.Kind,
                property.Type,
                property.Minimum,
                property.Maximum,
                property.Step,
                property.EnumNames,
                property.EnumValues
            })).ToArray(),
            omittedModuleSettings = codeModules.SelectMany(item => item.Declaration.OmittedSettingsProperties.Select(value => new
            {
                owner = item.Mod.Metadata.Name,
                property = value
            })).ToArray(),
            mapGameplayIds,
            resolvedMapFactories,
            sharedClosureSha256 = sharedClosureHash
        };
        File.WriteAllText(Path.Combine(outputRoot, "compatibility-manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputRoot, ".apple-everest-static-closure"), ProductPolicy.TransformerVersion + "\n", new UTF8Encoding(false));
    }

    internal static IReadOnlyList<FrozenIlTransformPlan> ComposeFrozenIlTransforms(
        IReadOnlyList<ResolvedMod> ordered)
    {
        // Pinned MonoMod appends ordinary unconfigured IL hooks in registration order and
        // rebuilds the target from its canonical baseline. The resolver's dependency order
        // establishes module Load order; the registry preserves registration order within a
        // module. Assign target-local ordinals only after combining those two sequences so
        // separate modules which target the same method cannot both claim ordinal zero.
        List<FrozenIlTransformPlan> result = [];
        foreach (ResolvedMod mod in ordered)
        {
            foreach (FrozenIlTransformPlan plan in mod.FrozenIlTransforms)
            {
                int registrationOrdinal = result.Count(existing =>
                    string.Equals(existing.TargetMethod, plan.TargetMethod, StringComparison.Ordinal));
                result.Add(plan with { RegistrationOrdinal = registrationOrdinal });
            }
        }
        return result;
    }

    public static void Apply(string closureRoot, string managedRoot)
    {
        RequireMarker(closureRoot);
        using (JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(closureRoot, "compatibility-manifest.json"))))
            if (manifest.RootElement.TryGetProperty("staticSemanticRuntimePatchSha256", out JsonElement semanticContract) &&
                semanticContract.ValueKind != JsonValueKind.Null && semanticContract.GetString() != StaticSemanticRuntimePatches.ContractSha256)
                throw new InvalidDataException("semantic runtime patch contract does not match this builder");
        if (!File.Exists(Path.Combine(managedRoot, "Celeste.Modern.csproj")))
            throw new InvalidDataException("managed target is not a generated Celeste tree");
        string destination = Path.Combine(managedRoot, "Celeste", "Mod", "AppleEverestStatic");
        if (Directory.Exists(destination)) throw new InvalidDataException("managed target already contains Apple Everest output");
        ValidateOnce(Path.Combine(managedRoot, "Celeste", "Level.cs"), "\t\tCalc.PopRandom();\n\t}\n\n\tpublic void UnloadLevel()");
        ValidateOnce(Path.Combine(managedRoot, "Celeste", "Level.cs"), "\t\t\tswitch (entity3.Name)\n\t\t\t{");
        ValidateOnce(Path.Combine(managedRoot, "Celeste", "Level.cs"), "\t\t\tswitch (trigger.Name)\n\t\t\t{");
        ValidateOnce(Path.Combine(managedRoot, "Celeste", "MapData.cs"),
            "\t\tBackdrop backdrop = null;\n\t\tif (child.Name.Equals(\"parallax\", StringComparison.OrdinalIgnoreCase))");
        ValidateOnce(Path.Combine(managedRoot, "Celeste", "MapData.cs"),
            MapDataCompatibilityPatch.LookupTarget);
        ValidateOnce(Path.Combine(managedRoot, "Celeste", "MapData.cs"),
            "\tpublic LevelData StartLevel()\n\t{\n\t\treturn GetAt(Vector2.Zero);\n\t}");
        ValidateOnce(Path.Combine(managedRoot, "Celeste", "Celeste.cs"), "\t\t\tceleste = new Celeste();");
        ValidateOnce(Path.Combine(managedRoot, "Celeste", "GameLoader.cs"), "\t\tAreaData.Load();");
        ValidateOnce(Path.Combine(managedRoot, "Celeste", "MenuOptions.cs"), "\t\tmenu.Add(new TextMenu.SubHeader(Dialog.Clean(\"options_gameplay\")));");
        ValidateOnce(Path.Combine(managedRoot, "Monocle", "Tracker.cs"), "\t\t}\n\t}\n\n\tprivate static List<Type> GetSubclasses(Type type)");
        ValidateOnce(Path.Combine(managedRoot, "Celeste.Modern.csproj"), "<DefineConstants>$(DefineConstants);");
        Directory.CreateDirectory(destination);
        foreach (string source in Directory.EnumerateFiles(Path.Combine(closureRoot, "managed"), "*.cs").OrderBy(Path.GetFileName, StringComparer.Ordinal))
            File.Copy(source, Path.Combine(destination, Path.GetFileName(source)), overwrite: false);
        File.Copy(Path.Combine(closureRoot, "managed", "AppleEverestExternalAssemblyRoots.props"),
            Path.Combine(managedRoot, "AppleEverestExternalAssemblyRoots.props"), overwrite: false);
        string frozenIlTargets = Path.Combine(closureRoot, "managed", "AppleEverestStaticIl.targets");
        if (File.Exists(frozenIlTargets))
        {
            File.Copy(frozenIlTargets, Path.Combine(managedRoot, "AppleEverestStaticIl.targets"), overwrite: false);
            CopyDirectory(Path.Combine(closureRoot, "host", "static-il"),
                Path.Combine(managedRoot, ".AppleEverestStaticIlHost"));
        }
        string closureAssemblies = Path.Combine(closureRoot, "assemblies");
        if (Directory.Exists(closureAssemblies))
        {
            string targetAssemblies = Path.Combine(managedRoot, "AppleEverestAssemblies");
            Directory.CreateDirectory(targetAssemblies);
            foreach (string source in Directory.EnumerateFiles(closureAssemblies, "*.dll").OrderBy(Path.GetFileName, StringComparer.Ordinal))
                File.Copy(source, Path.Combine(targetAssemblies, Path.GetFileName(source)), overwrite: false);
        }

        AppleApiSurface.Apply(managedRoot);
        PreparePinnedEverestManagedTargets(managedRoot);
        if (File.Exists(Path.Combine(destination, "GeneratedAppleEverestFlagGroups.cs")))
        {
            ReplaceOnce(Path.Combine(managedRoot, "Monocle", "EntityList.cs"),
                "\tpublic void Add(Entity entity)\n\t{",
                "\tpublic void Add(Entity entity)\n\t{\n\t\tglobal::Celeste.Mod.AppleEverestFlagGroup.ValidateCreation(Scene, entity);");
            ReplaceOnce(Path.Combine(managedRoot, "Monocle", "EntityList.cs"),
                "\t\t\t\tEntity entity = toAdd[i];",
                "\t\t\t\tEntity entity = toAdd[i];\n\t\t\t\tglobal::Celeste.Mod.AppleEverestFlagGroup.ValidateCreation(Scene, entity, beforeAdded: true);");
        }
        ManagedDetourGenerator.RewriteTargets(managedRoot, ManagedDetourCatalog.Targets);
        StaticAotCompatibility.PatchRuntimeRequiredGameSources(managedRoot);
        if (File.Exists(Path.Combine(destination, "GeneratedAppleEverestStaticAotCompatibility.cs")))
            StaticAotCompatibility.PatchGameSources(managedRoot);
        PatchDeferredAtlasTextureLoading(managedRoot);
        PatchLevel(Path.Combine(managedRoot, "Celeste", "Level.cs"));
        PatchLevelDataRoomNames(Path.Combine(managedRoot, "Celeste", "LevelData.cs"));
        PatchPlayerEvents(Path.Combine(managedRoot, "Celeste", "Player.cs"));
        PatchGameplayLoading(Path.Combine(managedRoot, "Celeste", "Level.cs"));
        PatchAuthoredSpinnerVariants(Path.Combine(managedRoot, "Celeste", "Level.cs"));
        PatchAuthoredSpinnerColours(Path.Combine(managedRoot, "Celeste", "Level.cs"),
            Path.Combine(managedRoot, "Celeste", "CrystalStaticSpinner.cs"));
        MapDataCompatibilityPatch.Apply(Path.Combine(managedRoot, "Celeste", "MapData.cs"));
        PatchMetadataStartLevel(Path.Combine(managedRoot, "Celeste", "MapData.cs"));
        PatchDefaultSpawn(managedRoot);
        PatchBackdropLoading(Path.Combine(managedRoot, "Celeste", "MapData.cs"));
        PatchStartup(Path.Combine(managedRoot, "Celeste", "Celeste.cs"));
        PatchContentReady(Path.Combine(managedRoot, "Celeste", "GameLoader.cs"));
        PatchCustomAudio(Path.Combine(managedRoot, "Celeste", "GameLoader.cs"),
            Path.Combine(managedRoot, "Celeste", "Audio.cs"));
        PatchMenu(Path.Combine(managedRoot, "Celeste", "MenuOptions.cs"));
        PatchCollabPauseMenu(Path.Combine(managedRoot, "Celeste", "Level.cs"));
        PatchCollabOverworldUi(Path.Combine(managedRoot, "Celeste", "Overworld.cs"),
            Path.Combine(managedRoot, "Celeste", "OuiChapterPanel.cs"),
            Path.Combine(managedRoot, "Celeste", "OuiChapterSelect.cs"),
            Path.Combine(managedRoot, "Celeste", "OuiJournal.cs"));
        PatchNonPersistentSaveQuit(Path.Combine(managedRoot, "Celeste", "Level.cs"));
        PatchNonPersistentSave(Path.Combine(managedRoot, "Celeste", "UserIO.cs"));
        PatchNonPersistentOverworldReturn(Path.Combine(managedRoot, "Celeste", "OverworldLoader.cs"));
        PatchPinnedEverestCompatibility(managedRoot);
        PatchModuleDurability(managedRoot);
        PatchLevelSetProgression(managedRoot);
        PatchSecondCollabSemantics(
            Path.Combine(managedRoot, "Celeste", "HeartGemDoor.cs"),
            Path.Combine(managedRoot, "Celeste", "Strawberry.cs"));
        StaticSemanticRuntimePatches.Apply(managedRoot);
        if (File.ReadAllText(Path.Combine(destination, "GeneratedAppleEverestContentManifest.cs"))
            .Contains("AppleEverestStage25KJ/FactoryProfiles/", StringComparison.Ordinal))
            FactoryCanaryInstrumentation.Apply(managedRoot);
        PatchTracker(Path.Combine(managedRoot, "Monocle", "Tracker.cs"));
        PatchPooler(Path.Combine(managedRoot, "Monocle", "Pooler.cs"));
        PatchProject(Path.Combine(managedRoot, "Celeste.Modern.csproj"), closureRoot);
    }

    public static string TargetPatchContract => string.Join("\n", new[]
    {
        "ManagedDetourCatalog:typed-static-dispatch:v4",
        "Level.LoadLevel:ordinary-event:v1",
        "LevelData:pre-1.2.5-optional-lvl-prefix:v1",
        "Player.Update:ordinary-after-update-event:v1",
        "Player.Update:max-helping-hand-camera-catchup-divisor:v1",
        "Celeste.Run:static-registry-startup:v1",
        "GameLoader:content-ready:v1",
        "GameLoader+Audio:static-custom-fmod-existing-system:v1",
        "MenuOptions:diagnostic-submenu:v2",
        "FactoryCanaries:registered-isolated-rooms-and-actual-lifecycle-dispatch:v1",
        "Tracker.Initialize:typed-gameplay-registry:v3",
        "Level.LoadLevel:typed-custom-factory-registry:v1",
        "MapData.ParseBackdrop:everest-event-and-typed-custom-backdrop-registry:v2",
        "MapData.Load:pinned-everest-checkpoint-attribution-and-grow-strawberry-tracker:v3",
        "MapData.StartLevel:pinned-everest-metadata-start-and-first-room-fallback:v1",
        "ModuleSettings:typed-menu-and-platform-storage:v1",
        "ModuleSaveData+Session:typed-yaml-aggregate-ab:v1",
        "UserIO.SaveRoutine:coherent-module-snapshot:v1",
        "SaveData.Start+StartSession:module-restore-boundaries:v1",
        "SaveData.TryDelete:module-slot-delete:v1",
        "OuiFileSelect:module-slot-preload:v1",
        "UserIO.SaveHandler:nonpersistent-mod-session-filter:v1",
        "OverworldLoader.Begin:nonpersistent-mod-session-restore:v1",
        "PinnedEverestABI:ConditionHelper+AchievementHelper-reviewed-members:v2",
        "LevelSetProgression:typed-sidecar-projection-lineage-ab:v1",
        "SecondRealCollab:source-lowered-heart-door-and-special-berries:v1",
        "CollabUtils2:real-ingame-overworld-ui-and-routing:v3",
        "PinnedEverestABI:DeathMarkers-reviewed-members:v1",
        "PinnedEverestABI:CaeruleaHelper-reviewed-members:v1",
        "HookGen+RuntimeDetour.Hook:shared-data-only-backend:v1",
        "MonoMod.ModInterop:host-cecil-static-typed-plan:v1",
        "HookGen.IL:hash-locked-host-freeze-immutable:v1",
        "AppleApiSurface:exact-reviewed-external-members:v1",
        "Celeste.Modern.csproj:EVEREST_APPLE_STATIC_AOT:v2",
        "ExternalAssembly:full-trimmer-root:v1"
    });

    private static void FreezeManagedAssemblyClosure(ResolvedMod mod, string destinationRoot,
        List<FrozenAssemblyRecord> frozenAssemblies)
    {
        string declared = mod.DeclaredAssemblyPath
            ?? throw new InvalidDataException($"{mod.Metadata.Name} has no declared assembly path");
        Dictionary<string, List<(string Path, string Sha)>> packagedCandidates = new(StringComparer.Ordinal);
        foreach (string relative in mod.ManagedFiles.Where(path =>
                     path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal))
        {
            string source = Path.Combine(mod.Input.StagingRoot,
                relative.Replace('/', Path.DirectorySeparatorChar));
            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(source,
                new ReaderParameters { ReadSymbols = false });
            string identity = assembly.Name.Name;
            if (!packagedCandidates.TryGetValue(identity, out List<(string Path, string Sha)>? candidates))
                packagedCandidates[identity] = candidates = [];
            candidates.Add((relative, Hashing.FileSha256(source)));
        }
        Dictionary<string, string> packagedByIdentity = new(StringComparer.Ordinal);
        string declaredSource = Path.Combine(mod.Input.StagingRoot,
            declared.Replace('/', Path.DirectorySeparatorChar));
        using AssemblyDefinition declaredAssembly = AssemblyDefinition.ReadAssembly(declaredSource,
            new ReaderParameters { ReadSymbols = false });
        string declaredIdentity = declaredAssembly.Name.Name;
        foreach ((string identity, List<(string Path, string Sha)> candidates) in packagedCandidates)
        {
            if (candidates.Select(candidate => candidate.Sha).Distinct(StringComparer.Ordinal).Count() != 1)
            {
                if (identity == declaredIdentity && candidates.Any(candidate => candidate.Path == declared))
                {
                    packagedByIdentity.Add(identity, declared);
                    continue;
                }
                throw new InvalidDataException($"conflicting managed dependency identity in {mod.Metadata.Name}: {identity}");
            }
            packagedByIdentity.Add(identity, candidates.Any(candidate => candidate.Path == declared)
                ? declared
                : candidates.OrderBy(candidate => candidate.Path.Count(character => character == '/'))
                    .ThenBy(candidate => candidate.Path, StringComparer.Ordinal).First().Path);
        }
        Queue<string> pending = new();
        HashSet<string> selected = new(StringComparer.Ordinal);
        pending.Enqueue(declared);
        while (pending.Count > 0)
        {
            string relative = pending.Dequeue();
            if (!selected.Add(relative)) continue;
            string source = Path.Combine(mod.Input.StagingRoot,
                relative.Replace('/', Path.DirectorySeparatorChar));
            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(source,
                new ReaderParameters { ReadSymbols = false });
            foreach (string dependency in assembly.MainModule.AssemblyReferences.Select(reference => reference.Name)
                         .Where(packagedByIdentity.ContainsKey).Order(StringComparer.Ordinal))
                pending.Enqueue(packagedByIdentity[dependency]);
        }

        foreach (string relative in selected.OrderBy(path => path == declared ? "" : path, StringComparer.Ordinal))
        {
            string source = Path.Combine(mod.Input.StagingRoot,
                relative.Replace('/', Path.DirectorySeparatorChar));
            string fileName = Path.GetFileName(relative);
            string destination = Path.Combine(destinationRoot, fileName);
            if (File.Exists(destination))
                throw new InvalidDataException($"duplicate frozen assembly filename: {fileName}");
            bool module = string.Equals(relative, declared, StringComparison.Ordinal);
            (string assemblyName, string original, string frozen) = AssemblyFreezer.Freeze(
                source, destination,
                module ? mod.DirectManagedHooks : Array.Empty<DirectManagedHookPlan>(),
                module ? mod.ModInteropRegistrations : Array.Empty<ModInteropRegistrationPlan>(),
                module ? mod.FrozenIlTransforms : Array.Empty<FrozenIlTransformPlan>(),
                module ? mod.StaticAotCompatibility : null,
                module ? mod.StaticConfiguredDetours : null);
            frozenAssemblies.Add(new FrozenAssemblyRecord(mod.Metadata.Name, assemblyName, fileName, original, frozen));
        }
    }

    private static string ExternalAssemblyRootsSource(IReadOnlyList<FrozenAssemblyRecord> assemblies)
    {
        StringBuilder source = new("<Project>\n  <!-- Third-party Everest DLLs are not trim-annotated. Root each accepted,\n       transformed assembly completely so every executable method receives native AOT code. -->\n  <ItemGroup>\n");
        foreach (FrozenAssemblyRecord assembly in assemblies.OrderBy(value => value.AssemblyName, StringComparer.Ordinal))
            source.Append("    <TrimmerRootAssembly Include=\"")
                .Append(SecurityElement.Escape(assembly.AssemblyName))
                .AppendLine("\" />");
        source.Append("  </ItemGroup>\n</Project>\n");
        return source.ToString();
    }

    private static void PrepareStaticIlHost(string repositoryRoot, IReadOnlyList<ResolvedMod> mods,
        IReadOnlyList<FrozenIlTransformPlan> plans, string outputRoot, string managedRoot)
    {
        string worker = Path.Combine(repositoryRoot, "tools", "AppleEverestIlWorker", "bin", "Release", "net10.0");
        string[] required =
        {
            "AppleEverestIlWorker.dll", "AppleEverestIlWorker.deps.json", "AppleEverestIlWorker.runtimeconfig.json",
            "Mono.Cecil.dll", "Mono.Cecil.Rocks.dll", "Mono.Cecil.Pdb.dll", "Mono.Cecil.Mdb.dll", "MonoMod.Utils.dll",
            "MonoMod.Backports.dll", "MonoMod.ILHelpers.dll"
        };
        if (plans.Any(plan => plan.Mechanism == "DIRECT_ILHOOK"))
            required = required.Concat(new[]
            {
                "MonoMod.RuntimeDetour.dll", "MonoMod.Core.dll", "MonoMod.Iced.dll"
            }).ToArray();
        if (required.Any(file => !File.Exists(Path.Combine(worker, file))))
            throw new InvalidDataException("build the exact pinned AppleEverestIlWorker before creating a frozen-IL closure");
        string host = Path.Combine(outputRoot, "host", "static-il");
        Directory.CreateDirectory(host);
        foreach (string file in required)
            File.Copy(Path.Combine(worker, file), Path.Combine(host, file), overwrite: false);
        string fixtures = Path.Combine(host, "fixtures");
        Directory.CreateDirectory(fixtures);
        foreach (string owner in plans.Select(plan => plan.Owner).Distinct(StringComparer.Ordinal))
        {
            ResolvedMod fixture = mods.Single(mod => mod.Metadata.Name == owner);
            FrozenIlTransformPlan[] ownerPlans = plans.Where(plan => plan.Owner == owner).ToArray();
            if (ownerPlans.Select(plan => plan.AssemblyPath + "\0" + plan.AssemblySha256).Distinct(StringComparer.Ordinal).Count() != 1)
                throw new InvalidDataException("static-IL owner provenance is ambiguous: " + owner);
            string source = Path.Combine(fixture.Input.StagingRoot,
                ownerPlans[0].AssemblyPath.Replace('/', Path.DirectorySeparatorChar));
            if (Hashing.FileSha256(source) != ownerPlans[0].AssemblySha256)
                throw new InvalidDataException("static-IL original package DLL hash mismatch: " + owner);
            File.Copy(source, Path.Combine(fixtures, owner + ".original.dll"), overwrite: false);
        }
        File.WriteAllText(Path.Combine(managedRoot, "AppleEverestStaticIl.targets"),
            StaticIlFreeze.Targets(plans), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(host, "frozen-il-plan.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = StaticIlFreeze.SchemaVersionFor(plans),
            worker = StaticIlFreeze.WorkerVersionFor(plans),
            planSha256 = StaticIlFreeze.PlanSha256(plans),
            transforms = plans
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        if (!Directory.Exists(sourceRoot))
            throw new InvalidDataException("static-IL host closure is missing");
        Directory.CreateDirectory(destinationRoot);
        foreach (string source in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                     .OrderBy(path => Path.GetRelativePath(sourceRoot, path), StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(sourceRoot, source);
            string destination = Path.Combine(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: false);
        }
    }

    private static void PatchLevel(string path) => ReplaceOnce(path,
        "\t\tCalc.PopRandom();\n\t}\n\n\tpublic void UnloadLevel()",
        "\t\tCalc.PopRandom();\n\t\tglobal::Celeste.Mod.Everest.Events.Level.RaiseOnLoadLevel(this, playerIntro, isFromLoader);\n\t\tglobal::Celeste.Mod.AppleEverestCollabRuntime.OnLevelLoaded(this);\n\t\tglobal::Celeste.Mod.AppleEverestStaticRuntime.ObserveTextureUsage(\"level-loaded\", this);\n\t}\n\n\tpublic void UnloadLevel()");

    private static void PatchLevelDataRoomNames(string path) => ReplaceOnce(path,
        "\t\t\tcase \"name\":\n\t\t\t\tName = attribute.Value.ToString().Substring(4);\n\t\t\t\tbreak;",
        "\t\t\tcase \"name\":\n\t\t\t\tstring appleEverestRoomName = attribute.Value.ToString();\n\t\t\t\tName = appleEverestRoomName.StartsWith(\"lvl_\", StringComparison.Ordinal) ? appleEverestRoomName.Substring(4) : appleEverestRoomName;\n\t\t\t\tbreak;");

    private static void PatchPlayerEvents(string path)
    {
        ReplaceOnce(path,
            "\t\t\t\tfloat num = ((StateMachine.State == 20) ? 8f : 1f);\n\t\t\t\tlevel.Camera.Position = position + (cameraTarget - position) * (1f - (float)Math.Pow(0.01f / num, Engine.DeltaTime));",
            "\t\t\t\tfloat num = ((StateMachine.State == 20) ? 8f : 1f);\n" +
            "\t\t\t\tnum = global::Celeste.Mod.AppleEverestCameraCatchupRuntime.ResolveDivisor(num, this);\n" +
            "\t\t\t\tlevel.Camera.Position = position + (cameraTarget - position) * (1f - (float)Math.Pow(0.01f / num, Engine.DeltaTime));");
        ReplaceOnce(path,
            "\t\t\t\tif (component2.Check(this) && Dead)\n\t\t\t\t{\n\t\t\t\t\tbase.Collider = collider;\n\t\t\t\t\treturn;\n\t\t\t\t}",
            "\t\t\t\tif (component2.Check(this) && Dead)\n\t\t\t\t{\n\t\t\t\t\tbase.Collider = collider;\n\t\t\t\t\tglobal::Celeste.Mod.Everest.Events.Player.RaiseOnAfterUpdate(this);\n\t\t\t\t\treturn;\n\t\t\t\t}");
        ReplaceOnce(path,
            "\t\twasOnGround = onGround;\n\t\twindMovedUp = false;\n\t}",
            "\t\twasOnGround = onGround;\n\t\twindMovedUp = false;\n\t\tglobal::Celeste.Mod.Everest.Events.Player.RaiseOnAfterUpdate(this);\n\t}");
    }

    private static void PatchGameplayLoading(string path)
    {
        ReplaceOnce(path,
            "\t\t\tswitch (entity3.Name)\n\t\t\t{",
            "\t\t\tif (LoadCustomEntity(entity3, this)) continue;\n" +
            "\t\t\tswitch (entity3.Name)\n\t\t\t{");
        ReplaceOnce(path,
            "\t\t\tswitch (trigger.Name)\n\t\t\t{",
            "\t\t\tif (global::Celeste.Mod.GeneratedAppleEverestGameplayRegistry.TryCreateTrigger(trigger.Name, trigger, vector, entityID3, out Entity appleEverestTrigger))\n\t\t\t{\n\t\t\t\tAdd(appleEverestTrigger);\n\t\t\t\tcontinue;\n\t\t\t}\n\t\t\tswitch (trigger.Name)\n\t\t\t{");
    }

    private static void PatchAuthoredSpinnerVariants(string path)
    {
        // Everest allows mod maps to select the otherwise chapter-bound star
        // and dust visuals on both moving spinner families. Preserve the
        // authored bool directly: false/missing values must keep Celeste's
        // canonical chapter selection and ultimately use the blade variant.
        ReplaceOnce(path,
            "\t\t\tcase \"rotateSpinner\":\n" +
            "\t\t\t\tif (Session.Area.ID == 10)\n",
            "\t\t\tcase \"rotateSpinner\":\n" +
            "\t\t\t\tif (Session.Area.ID == 10 || entity3.Bool(\"star\"))\n");
        ReplaceOnce(path,
            "\t\t\t\telse if (Session.Area.ID == 3 || (Session.Area.ID == 7 && Session.Level.StartsWith(\"d-\")))\n" +
            "\t\t\t\t{\n" +
            "\t\t\t\t\tAdd(new DustRotateSpinner(entity3, vector));",
            "\t\t\t\telse if (Session.Area.ID == 3 || (Session.Area.ID == 7 && Session.Level.StartsWith(\"d-\")) || entity3.Bool(\"dust\"))\n" +
            "\t\t\t\t{\n" +
            "\t\t\t\t\tAdd(new DustRotateSpinner(entity3, vector));");
        ReplaceOnce(path,
            "\t\t\tcase \"trackSpinner\":\n" +
            "\t\t\t\tif (Session.Area.ID == 10)\n",
            "\t\t\tcase \"trackSpinner\":\n" +
            "\t\t\t\tif (Session.Area.ID == 10 || entity3.Bool(\"star\"))\n");
        ReplaceOnce(path,
            "\t\t\t\telse if (Session.Area.ID == 3 || (Session.Area.ID == 7 && Session.Level.StartsWith(\"d-\")))\n" +
            "\t\t\t\t{\n" +
            "\t\t\t\t\tAdd(new DustTrackSpinner(entity3, vector));",
            "\t\t\t\telse if (Session.Area.ID == 3 || (Session.Area.ID == 7 && Session.Level.StartsWith(\"d-\")) || entity3.Bool(\"dust\"))\n" +
            "\t\t\t\t{\n" +
            "\t\t\t\t\tAdd(new DustTrackSpinner(entity3, vector));");
    }

    private static void PatchAuthoredSpinnerColours(string levelPath, string spinnerPath)
    {
        ReplaceOnce(levelPath,
            "\t\t\t\tAdd(new CrystalStaticSpinner(entity3, vector, color));",
            "\t\t\t\tif (global::Celeste.Mod.AppleEverestProgressionRuntime.IsCustom(Session.Area))\n" +
            "\t\t\t\t{\n" +
            "\t\t\t\t\tcolor = global::Celeste.Mod.AppleEverestSemanticFactories.ResolveSpinnerColor(entity3, color);\n" +
            "\t\t\t\t}\n" +
            "\t\t\t\tAdd(new CrystalStaticSpinner(entity3, vector, color));");
        ReplaceOnce(spinnerPath,
            "\tpublic override void Awake(Scene scene)\n\t{\n\t\tbase.Awake(scene);",
            "\tpublic override void Awake(Scene scene)\n\t{\n" +
            "\t\tbase.Awake(scene);\n" +
            "\t\tif ((int)color == -1)\n" +
            "\t\t{\n" +
            "\t\t\tAdd(new CoreModeListener(this));\n" +
            "\t\t\tcolor = ((scene as Level).CoreMode == Session.CoreModes.Cold) ? CrystalColor.Blue : CrystalColor.Red;\n" +
            "\t\t}");
    }

    private static void PatchDefaultSpawn(string managedRoot)
    {
        string data = Path.Combine(managedRoot, "Celeste", "LevelData.cs");
        ReplaceOnce(data, "\tpublic List<Vector2> Spawns;",
            "\tpublic List<Vector2> Spawns;\n\n\tpublic Vector2? DefaultSpawn;");
        const string add = "\t\t\t\t\t\tSpawns.Add(new Vector2((float)Bounds.X + Convert.ToSingle(child2.Attributes[\"x\"], CultureInfo.InvariantCulture), (float)Bounds.Y + Convert.ToSingle(child2.Attributes[\"y\"], CultureInfo.InvariantCulture)));";
        ReplaceOnce(data, add, add + "\n\t\t\t\t\t\tglobal::Celeste.Mod.AppleEverestDefaultSpawn.Record(this, child2.Attributes, Spawns[Spawns.Count - 1]);");
        ReplaceOnce(Path.Combine(managedRoot, "Celeste", "Level.cs"),
            "\tpublic Vector2 DefaultSpawnPoint => GetSpawnPoint(new Vector2(Bounds.Left, Bounds.Bottom));",
            "\tpublic Vector2 DefaultSpawnPoint => global::Celeste.Mod.AppleEverestDefaultSpawn.Get(this);");
    }

    private static void PatchMetadataStartLevel(string path) => ReplaceOnce(path,
        "\tpublic LevelData StartLevel()\n\t{\n\t\treturn GetAt(Vector2.Zero);\n\t}",
        "\tpublic LevelData StartLevel()\n\t{\n" +
        "\t\tstring appleEverestStartLevel = global::Celeste.Mod.AppleEverestProgressionRuntime.StartLevel(Area);\n" +
        "\t\tif (!string.IsNullOrEmpty(appleEverestStartLevel))\n" +
        "\t\t{\n" +
        "\t\t\tLevelData appleEverestLevel = Levels.FirstOrDefault(level => level.Name == appleEverestStartLevel);\n" +
        "\t\t\tif (appleEverestLevel != null)\n" +
        "\t\t\t{\n" +
        "\t\t\t\treturn appleEverestLevel;\n" +
        "\t\t\t}\n" +
        "\t\t}\n" +
        "\t\treturn GetAt(Vector2.Zero) ?? Levels.FirstOrDefault();\n" +
        "\t}");

    private static void PatchBackdropLoading(string path) => ReplaceOnce(path,
        "\t\tBackdrop backdrop = null;\n\t\tif (child.Name.Equals(\"parallax\", StringComparison.OrdinalIgnoreCase))",
        "\t\tBackdrop backdrop = global::Celeste.Mod.Everest.Events.Level.LoadBackdrop(this, child, above);\n\t\tif (backdrop != null)\n\t\t{\n\t\t}\n\t\telse if (global::Celeste.Mod.GeneratedAppleEverestGameplayRegistry.TryCreateBackdrop(child.Name, child, out backdrop))\n\t\t{\n\t\t}\n\t\telse if (child.Name.Equals(\"parallax\", StringComparison.OrdinalIgnoreCase))");

    private static void PatchStartup(string path)
    {
        ReplaceOnce(path,
            "\t\t\tceleste = new Celeste();",
            "\t\t\tglobal::Celeste.Mod.AppleEverestStaticRuntime.Startup();\n\t\t\tceleste = new Celeste();");
        ReplaceOnce(path,
            "\t\tAudio.Update();\n\t\tbase.Update(gameTime);",
            "\t\tAudio.Update();\n\t\tbase.Update(gameTime);\n\t\tglobal::Celeste.Mod.AppleEverestStaticRuntime.CompleteStartup();");
    }

    private static void PatchContentReady(string path) => ReplaceOnce(path,
        "\t\tAreaData.Load();",
        "\t\tAreaData.Load();\n\t\tglobal::Celeste.Mod.AppleEverestStaticRuntime.ContentReady();");

    private static void PatchDeferredAtlasTextureLoading(string managedRoot)
    {
        string virtualTexture = Path.Combine(managedRoot, "Monocle", "VirtualTexture.cs");
        string virtualTextureSource = File.ReadAllText(virtualTexture);
        const string iosBundleLoader = "global::Celeste.IOSStorageHooks.OpenBundleFile";
        const string tvosBundleLoader = "global::Celeste.TvOSStage6PersistenceHooks.OpenBundleFile";
        string bundleLoader = virtualTextureSource.Contains(iosBundleLoader, StringComparison.Ordinal)
            ? iosBundleLoader
            : virtualTextureSource.Contains(tvosBundleLoader, StringComparison.Ordinal)
                ? tvosBundleLoader
                : throw new InvalidDataException(
                    "VirtualTexture does not contain a reviewed Apple bundle-file loader");
        ReplaceOnce(virtualTexture,
            "\tprivate Color color;",
            "\tprivate Color color;\n\n" +
            "\tprivate readonly bool deferred;\n\n" +
            "\tprivate bool forceReload;\n\n" +
            "\tprivate bool restoreAfterReload;");
        ReplaceOnce(virtualTexture,
            "\tinternal VirtualTexture(string path)\n\t{\n\t\tbase.Name = (Path = path);\n\t\tReload();\n\t}",
            "\tinternal VirtualTexture(string path)\n\t{\n\t\tbase.Name = (Path = path);\n\t\tReload();\n\t}\n\n" +
            "\tinternal VirtualTexture(string path, bool deferred)\n\t{\n" +
            "\t\tbase.Name = (Path = path);\n\t\tthis.deferred = deferred;\n" +
            "\t\tif (!deferred)\n\t\t{\n\t\t\tReload();\n\t\t\treturn;\n\t\t}\n" +
            "\t\tReadDeferredPngDimensions();\n\t}\n\n" +
            "\tinternal void EnsureLoaded()\n\t{\n" +
            "\t\tif (Texture != null && !Texture.IsDisposed) return;\n" +
            "\t\tforceReload = true;\n\t\ttry\n\t\t{\n\t\t\tReload();\n\t\t}\n" +
            "\t\tfinally\n\t\t{\n\t\t\tforceReload = false;\n\t\t}\n\t}\n\n" +
            "\tpublic Texture2D Texture_Safe\n\t{\n" +
            "\t\tget\n\t\t{\n\t\t\tEnsureLoaded();\n\t\t\treturn Texture;\n\t\t}\n\t}\n\n" +
            "\tprivate void ReadDeferredPngDimensions()\n\t{\n" +
            "\t\tif (!string.Equals(System.IO.Path.GetExtension(Path), \".png\", StringComparison.OrdinalIgnoreCase))\n" +
            "\t\t\tthrow new InvalidDataException(\"deferred textures must be PNG files: \" + Path);\n" +
            "\t\tusing Stream stream = " + bundleLoader +
            "(System.IO.Path.Combine(Engine.ContentDirectory, Path));\n" +
            "\t\tbyte[] header = new byte[24];\n\t\tint offset = 0;\n" +
            "\t\twhile (offset < header.Length)\n\t\t{\n" +
            "\t\t\tint read = stream.Read(header, offset, header.Length - offset);\n" +
            "\t\t\tif (read <= 0) throw new InvalidDataException(\"truncated deferred PNG: \" + Path);\n" +
            "\t\t\toffset += read;\n\t\t}\n" +
            "\t\tif (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47 ||\n" +
            "\t\t\theader[12] != 0x49 || header[13] != 0x48 || header[14] != 0x44 || header[15] != 0x52)\n" +
            "\t\t\tthrow new InvalidDataException(\"invalid deferred PNG header: \" + Path);\n" +
            "\t\tbase.Width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];\n" +
            "\t\tbase.Height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];\n" +
            "\t\tif (base.Width <= 0 || base.Height <= 0) throw new InvalidDataException(\"invalid deferred PNG dimensions: \" + Path);\n" +
            "\t}");
        ReplaceOnce(virtualTexture,
            "\tinternal override void Unload()\n\t{\n\t\tif (Texture != null && !Texture.IsDisposed)",
            "\tinternal override void Unload()\n\t{\n" +
            "\t\trestoreAfterReload = deferred && Texture != null && !Texture.IsDisposed;\n" +
            "\t\tif (Texture != null && !Texture.IsDisposed)");
        ReplaceOnce(virtualTexture,
            "\tinternal unsafe override void Reload()\n\t{\n\t\tUnload();",
            "\tinternal unsafe override void Reload()\n\t{\n" +
            "\t\tif (deferred && !forceReload && !restoreAfterReload) return;\n" +
            "\t\tUnload();\n\t\trestoreAfterReload = false;");

        string virtualContent = Path.Combine(managedRoot, "Monocle", "VirtualContent.cs");
        ReplaceOnce(virtualContent,
            "\tpublic static VirtualTexture CreateTexture(string name, int width, int height, Color color)",
            "\tpublic static VirtualTexture CreateDeferredTexture(string path)\n\t{\n" +
            "\t\tVirtualTexture virtualTexture = new VirtualTexture(path, deferred: true);\n" +
            "\t\tassets.Add(virtualTexture);\n\t\treturn virtualTexture;\n\t}\n\n" +
            "\tpublic static VirtualTexture CreateTexture(string name, int width, int height, Color color)");

        string mTexture = Path.Combine(managedRoot, "Monocle", "MTexture.cs");
        ReplaceOnce(mTexture,
            "\tpublic VirtualTexture Texture { get; private set; }",
            "\tprivate VirtualTexture texture;\n\n" +
            "\tpublic VirtualTexture Texture\n\t{\n" +
            "\t\tget\n\t\t{\n\t\t\ttexture?.EnsureLoaded();\n\t\t\treturn texture;\n\t\t}\n" +
            "\t\tprivate set => texture = value;\n\t}");
        ReplaceOnce(mTexture,
            "\t\tClipRect = new Rectangle(0, 0, Texture.Width, Texture.Height);",
            "\t\tClipRect = new Rectangle(0, 0, this.texture.Width, this.texture.Height);");
        ReplaceExactOccurrences(mTexture, "\t\tTexture = parent.Texture;", "\t\tTexture = parent.texture;", 2);
        ReplaceOnce(mTexture,
            "\t\tLeftUV = (float)ClipRect.Left / (float)Texture.Width;\n" +
            "\t\tRightUV = (float)ClipRect.Right / (float)Texture.Width;\n" +
            "\t\tTopUV = (float)ClipRect.Top / (float)Texture.Height;\n" +
            "\t\tBottomUV = (float)ClipRect.Bottom / (float)Texture.Height;",
            "\t\tLeftUV = (float)ClipRect.Left / (float)texture.Width;\n" +
            "\t\tRightUV = (float)ClipRect.Right / (float)texture.Width;\n" +
            "\t\tTopUV = (float)ClipRect.Top / (float)texture.Height;\n" +
            "\t\tBottomUV = (float)ClipRect.Bottom / (float)texture.Height;");
        ReplaceOnce(mTexture,
            "\t\tTexture.Dispose();\n\t\tTexture = null;",
            "\t\ttexture.Dispose();\n\t\ttexture = null;");
        ReplaceOnce(mTexture, "\t\tapplyTo.Texture = Texture;", "\t\tapplyTo.texture = texture;");
        ReplaceOnce(mTexture,
            "\t\tif (Texture.Path != null)\n\t\t{\n\t\t\treturn Texture.Path;\n\t\t}\n" +
            "\t\treturn \"Texture [\" + Texture.Width + \", \" + Texture.Height + \"]\";",
            "\t\tif (texture.Path != null)\n\t\t{\n\t\t\treturn texture.Path;\n\t\t}\n" +
            "\t\treturn \"Texture [\" + texture.Width + \", \" + texture.Height + \"]\";");
    }

    private static void PatchCustomAudio(string gameLoader, string audio)
    {
        ReplaceOnce(gameLoader,
            "\t\tAudio.Stage5BAllBanksLoaded();",
            "\t\tAudio.Stage5BAllBanksLoaded();\n\t\tAudio.AppleEverestLoadCustomBanks();");
        ReplaceOneOf(audio,
            ["\tpublic static void Stage5BAllBanksLoaded() => AppleAudioDiagnostics.AllBanksLoaded(system);",
             "\tpublic static void Stage5BAllBanksLoaded() => TvOSStage5BAudioBridge.AllBanksLoaded(system);"],
            needle => needle + "\n\n\tinternal static void AppleEverestLoadCustomBanks() => global::Celeste.Mod.AppleEverestCustomAudioRuntime.Load(system);");
        ReplaceOneOf(audio,
            ["\t\t\tAppleAudioDiagnostics.ShutdownEntered(\"Celeste.Audio.Unload\");\n\t\t\tCheckFmod(system.unloadAll(), \"FMOD_Studio_System_UnloadAll\");",
             "\t\t\tTvOSStage5BAudioBridge.ShutdownEntered(\"Celeste.Audio.Unload\");\n\t\t\tCheckFmod(system.unloadAll(), \"FMOD_Studio_System_UnloadAll\");"],
            needle => needle.Replace("\n\t\t\tCheckFmod", "\n\t\t\tglobal::Celeste.Mod.AppleEverestCustomAudioRuntime.BeforeSystemUnload(system);\n\t\t\tCheckFmod", StringComparison.Ordinal));
        ReplaceOnce(audio,
            "\t\t\tRESULT @event = system.getEvent(path, out value);",
            "\t\t\tRESULT @event = system.getEvent(path, out value);\n\t\t\tif (@event == RESULT.ERR_EVENT_NOTFOUND && global::Celeste.Mod.AppleEverestCustomAudioRuntime.TryGetEventDescription(system, path, out value)) @event = RESULT.OK;");
        ReplaceOneOf(audio,
            ["\t\t\tAppleAudioDiagnostics.RegisterEvent(instance, path, \"event\");",
             "\t\t\tTvOSStage5BAudioBridge.RegisterEvent(instance, path, \"event\");"],
            needle => needle + "\n\t\t\tglobal::Celeste.Mod.AppleEverestCustomAudioRuntime.RecordEventRequest(path, instance);");
    }

    private static void PatchMenu(string path) => ReplaceOnce(path,
        "\t\tmenu.Add(new TextMenu.SubHeader(Dialog.Clean(\"options_gameplay\")));",
        "\t\tmenu.Add(new TextMenu.SubHeader(Dialog.Clean(\"options_gameplay\")));\n\t\tglobal::Celeste.Mod.AppleEverestLab.AddOptions(menu);");

    private static void PatchNonPersistentSaveQuit(string path) => ReplaceOnce(path,
        "\t\t\tif (SaveQuitDisabled || (player != null && player.StateMachine.State == 18))",
        "\t\t\tif (SaveQuitDisabled || global::Celeste.Mod.AppleEverestStaticRuntime.NonPersistentModSession || (player != null && player.StateMachine.State == 18))");

    private static void PatchCollabPauseMenu(string path) => ReplaceOnce(path,
        "\t\tif (startIndex > 0)\n\t\t{",
        "\t\tglobal::Celeste.Mod.AppleEverestCollabRuntime.AddPauseMenuItem(this, menu);\n\t\tif (startIndex > 0)\n\t\t{");

    private static void PatchCollabOverworldUi(
        string overworldPath, string chapterPanelPath, string chapterSelectPath, string journalPath)
    {
        ReplaceOnce(overworldPath,
            "\t\tOui[] menus = new Oui[10]\n\t\t{\n" +
            "\t\t\tnew OuiAssistMode(), new OuiChapterPanel(), new OuiChapterSelect(), new OuiCredits(),\n" +
            "\t\t\tnew OuiFileNaming(), new OuiFileSelect(), new OuiJournal(), new OuiMainMenu(),\n" +
            "\t\t\tnew OuiOptions(), new OuiTitleScreen()\n\t\t};",
            "\t\tOui[] menus = new Oui[12]\n\t\t{\n" +
            "\t\t\tnew OuiAssistMode(), new OuiChapterPanel(), new OuiChapterSelect(), new OuiCredits(),\n" +
            "\t\t\tnew OuiFileNaming(), new OuiFileSelect(), new OuiJournal(), new OuiMainMenu(),\n" +
            "\t\t\tnew OuiOptions(), new OuiTitleScreen(),\n" +
            "\t\t\tnew global::Celeste.Mod.AppleEverestOuiEnterChapterPanel(),\n" +
            "\t\t\tnew global::Celeste.Mod.AppleEverestOuiEnterJournal()\n\t\t};");
        ReplaceOnce(chapterPanelPath,
            "\tprivate class Option",
            "\tpublic class Option");
        ReplaceOnce(chapterPanelPath,
            "\tprivate int option\n",
            "\tinternal int option\n");
        ReplaceOnce(chapterPanelPath,
            "\tprivate bool selectingMode = true;",
            "\tinternal bool selectingMode = true;");
        ReplaceOnce(chapterPanelPath,
            "\tprivate List<Option> checkpoints = new List<Option>();",
            "\tinternal List<Option> checkpoints = new List<Option>();");
        ReplaceOnce(chapterPanelPath,
            "\t\tchapter = Dialog.Get(\"area_chapter\").Replace(\"{x}\", Area.ChapterIndex.ToString().PadLeft(2));",
            "\t\tchapter = Dialog.Get(\"area_chapter\").Replace(\"{x}\", Area.ChapterIndex.ToString().PadLeft(2));\n" +
            "\t\tchapter = global::Celeste.Mod.AppleEverestCollabRuntime.ChapterSubtitle(Area, chapter);");
        ReplaceOnce(chapterPanelPath,
            "\t\tcontentOffset = new Vector2(440f, 120f);\n\t\tinitialized = true;",
            "\t\tcontentOffset = new Vector2(440f, 120f);\n\t\tinitialized = true;\n" +
            "\t\tglobal::Celeste.Mod.AppleEverestCollabRuntime.ConfigureChapterPanel(this);");
        ReplaceOnce(chapterPanelPath,
            "\t\tint toHeight = (selectingMode ? 730 : GetModeHeight());",
            "\t\tint toHeight = (selectingMode ? 730 : GetModeHeight());\n" +
            "\t\ttoHeight = global::Celeste.Mod.AppleEverestCollabRuntime.ChapterSwapHeight(this, toHeight);");
        ReplaceOnce(chapterPanelPath,
            "\t\tif (!Data.Interlude && ((areaModeStats.Deaths > 0 && Area.Mode != 0) || areaModeStats.Completed || areaModeStats.HeartGem))",
            "\t\tif (!Data.Interlude && ((areaModeStats.Deaths > 0 && (Area.Mode != 0 || global::Celeste.Mod.AppleEverestCollabRuntime.ShouldShowChapterDeaths(this))) || areaModeStats.Completed || areaModeStats.HeartGem))");
        ReplaceOnce(chapterPanelPath,
            "\t\tdeaths.Visible = areaModeStats.Deaths > 0 && (Area.Mode != 0 || RealStats.Modes[(int)Area.Mode].Completed) && !AreaData.Get(Area).Interlude;",
            "\t\tdeaths.Visible = areaModeStats.Deaths > 0 && (global::Celeste.Mod.AppleEverestCollabRuntime.ShouldShowChapterDeaths(this) || Area.Mode != 0 || RealStats.Modes[(int)Area.Mode].Completed) && !AreaData.Get(Area).Interlude;");
        ReplaceOnce(chapterPanelPath,
            "Position + IconOffset + new Vector2(-100f, -2f)",
            "Position + IconOffset + new Vector2(-100f, global::Celeste.Mod.AppleEverestCollabRuntime.ChapterAuthorOffset(this, -2f))");
        ReplaceOnce(chapterPanelPath,
            "Position + IconOffset + new Vector2(-100f, -18f)",
            "Position + IconOffset + new Vector2(-100f, global::Celeste.Mod.AppleEverestCollabRuntime.ChapterTitleOffset(this, -18f))");
        foreach (string layer in new[] { "title", "accent" })
            ReplaceOnce(chapterPanelPath,
                "GFX.Gui[\"areaselect/" + layer + "\"].Draw(Position + new Vector2(-60f, 0f)",
                "GFX.Gui[\"areaselect/" + layer + "\"].Draw(Position + new Vector2(global::Celeste.Mod.AppleEverestChapterTitleLayout.BannerOffset(Area, -60f), 0f)");
        ReplaceOnce(chapterSelectPath,
            "\tpublic void AdvanceToNext()",
            "\tinternal OuiChapterSelectIcon AppleEverestIcon(int area) => area >= 0 && area < icons.Count ? icons[area] : null;\n\n" +
            "\tpublic void AdvanceToNext()");
        ReplaceOnce(chapterPanelPath,
            "\tpublic void Start(string checkpoint = null)\n\t{\n\t\tFocused = false;",
            "\tpublic void Start(string checkpoint = null)\n\t{\n" +
            "\t\tif (global::Celeste.Mod.AppleEverestCollabRuntime.TryStartChapterPanel(this, checkpoint)) return;\n" +
            "\t\tFocused = false;");
        ReplaceOnce(chapterPanelPath,
            "\t\tstring checkpointPreviewName = GetCheckpointPreviewName(area, level);",
            "\t\tstring checkpointPreviewName = global::Celeste.Mod.AppleEverestCollabRuntime.CheckpointPreviewName(area, level) ?? GetCheckpointPreviewName(area, level);");
        ReplaceOnce(chapterPanelPath,
            "\t\t\t\tif (!SaveData.Instance.FoundAnyCheckpoints(Area))",
            "\t\t\t\tif (!global::Celeste.Mod.AppleEverestCollabRuntime.NeedsChapterCheckpointPage(this) && !SaveData.Instance.FoundAnyCheckpoints(Area))");
        ReplaceOnce(chapterPanelPath,
            "\t\t\telse\n\t\t\t{\n\t\t\t\toption = 0;\n\t\t\t}\n\t\t\tfor (int j = 0; j < options.Count; j++)",
            "\t\t\telse\n\t\t\t{\n\t\t\t\toption = 0;\n\t\t\t}\n" +
            "\t\t\tglobal::Celeste.Mod.AppleEverestCollabRuntime.ConfigureChapterCheckpoints(this);\n" +
            "\t\t\tfor (int j = 0; j < options.Count; j++)");
        ReplaceOnce(chapterPanelPath,
            "\t\t\t\tDrawCheckpoint(center, options[num], num);",
            "\t\t\t\tif (global::Celeste.Mod.AppleEverestCollabRuntime.ShouldDrawVanillaCheckpoint(this))\n" +
            "\t\t\t\t{\n" +
            "\t\t\t\t\tDrawCheckpoint(center, options[num], num);\n" +
            "\t\t\t\t}\n" +
            "\t\t\t\telse global::Celeste.Mod.AppleEverestCollabRuntime.DrawChapterCredits(this, center, num, height);");
        ReplaceOnce(journalPath,
            "\t\tint num = 0;\n\t\tforeach (OuiJournalPage page in Pages)",
            "\t\tglobal::Celeste.Mod.AppleEverestCollabRuntime.ConfigureJournalPages(this);\n" +
            "\t\tint num = 0;\n\t\tforeach (OuiJournalPage page in Pages)");
    }

    private static void PatchNonPersistentSave(string path) => ReplaceOnce(path,
        "\tpublic static void SaveHandler(bool file, bool settings)\n\t{\n\t\tif (!Saving)",
        "\tpublic static void SaveHandler(bool file, bool settings)\n\t{\n\t\tfile = global::Celeste.Mod.AppleEverestStaticRuntime.FilterVanillaFileSave(file);\n\t\tif (!file && !settings)\n\t\t{\n\t\t\treturn;\n\t\t}\n\t\tif (!Saving)");

    private static void PatchNonPersistentOverworldReturn(string path) => ReplaceOnce(path,
        "\t\tif (SaveData.Instance != null)\n\t\t{\n\t\t\tsession = SaveData.Instance.CurrentSession;\n\t\t}\n\t\tEntity entity = new Entity();",
        "\t\tif (SaveData.Instance != null)\n\t\t{\n\t\t\tsession = SaveData.Instance.CurrentSession;\n\t\t}\n\t\tStartMode = global::Celeste.Mod.AppleEverestStaticRuntime.CompleteNonPersistentModSession(StartMode);\n\t\tEntity entity = new Entity();");

    private static void PatchPinnedEverestCompatibility(string managedRoot)
    {
        // Everest's desktop MonoMod output publicizes these two vanilla fields.
        // DeathMarkers' ordinary precompiled DLL references that exact ABI.
        // The changes live only in the closed Apple-Everest derived tree.
        string deadBody = Path.Combine(managedRoot, "Celeste", "PlayerDeadBody.cs");
        ReplaceOnce(deadBody, "\tprivate Vector2 bounce = Vector2.Zero;",
            "\tpublic Vector2 bounce = Vector2.Zero;");
        ReplaceOnce(deadBody, "\tprivate bool finished;", "\tpublic bool finished;");

        // The pinned Everest AreaKey patch exposes SID. For the canonical
        // Celeste class, derive its stable vanilla SID from the normal-mode
        // content path without expanding into general LevelSet support.
        string areaKey = Path.Combine(managedRoot, "Celeste", "AreaKey.cs");
        ReplaceOnce(areaKey,
            "\tpublic int ChapterIndex\n\t{",
            "\tpublic string SID\n\t{\n\t\tget\n\t\t{\n\t\t\tstring appleEverestSid = global::Celeste.Mod.AppleEverestProgressionRuntime.Sid(this);\n\t\t\tif (appleEverestSid != null) return appleEverestSid;\n\t\t\tif (AreaData.Areas == null || ID < 0 || ID >= AreaData.Areas.Count) return null;\n\t\t\tAreaData data = AreaData.Areas[ID];\n\t\t\tstring path = data?.Mode != null && data.Mode.Length > 0 ? data.Mode[0]?.Path : null;\n\t\t\treturn string.IsNullOrEmpty(path) ? data?.Name : \"Celeste/\" + path;\n\t\t}\n\t}\n\n\tpublic int ChapterIndex\n\t{");

        ReplaceOnce(areaKey,
            "\tpublic int ChapterIndex\n\t{",
            "\tpublic string LevelSet\n\t{\n\t\tget\n\t\t{\n\t\t\tstring appleEverestLevelSet = global::Celeste.Mod.AppleEverestProgressionRuntime.LevelSet(this);\n\t\t\tif (appleEverestLevelSet != null) return appleEverestLevelSet;\n\t\t\tstring sid = SID;\n\t\t\tif (string.IsNullOrEmpty(sid)) return null;\n\t\t\tint slash = sid.LastIndexOf('/');\n\t\t\treturn slash > 0 ? sid.Substring(0, slash) : \"Celeste\";\n\t\t}\n\t}\n\n\tpublic int ChapterIndex\n\t{");

        // ConditionHelper 1.0.0 is compiled against Everest's publicized
        // canonical-level-set ABI. Keep the ordinary serialized LastArea as
        // the source of truth and expose the reviewed nonserialized alias and
        // aggregate view used by that exact release.
        string saveData = Path.Combine(managedRoot, "Celeste", "SaveData.cs");
        ReplaceOnce(saveData,
            "\tpublic AreaKey LastArea;",
            "\tpublic AreaKey LastArea;\n\n\t[NonSerialized]\n\t[XmlIgnore]\n\tpublic AreaKey LastArea_Safe;");
        ReplaceOnce(saveData,
            "\tpublic List<AreaStats> Areas = new List<AreaStats>();",
            "\tpublic List<AreaStats> Areas = new List<AreaStats>();\n\n\tpublic List<AreaStats> Areas_Safe => Areas;");
        ReplaceOnce(saveData,
            "\tpublic int UnlockedModes\n\t{",
            "\tpublic LevelSetStats LevelSetStats => new LevelSetStats(this);\n\n\tpublic int UnlockedModes\n\t{");
        ReplaceOnce(saveData,
            "\tpublic void AfterInitialize()\n\t{",
            "\tpublic void AfterInitialize()\n\t{\n\t\tLastArea_Safe = LastArea;");
        ReplaceOnce(saveData,
            "\t\tLastArea = session.Area;",
            "\t\tLastArea = session.Area;\n\t\tLastArea_Safe = LastArea;");

        string levelSetStats = Path.Combine(managedRoot, "Celeste", "LevelSetStats.cs");
        if (File.Exists(levelSetStats))
            throw new InvalidDataException("pinned Everest LevelSetStats target already exists");
        File.WriteAllText(levelSetStats,
            "namespace Celeste;\n\n" +
            "public sealed class LevelSetStats\n{\n" +
            "\tprivate readonly SaveData saveData;\n\n" +
            "\tinternal LevelSetStats(SaveData saveData) { this.saveData = saveData; }\n\n" +
            "\tprivate string LevelSet => saveData?.LastArea_Safe.LevelSet ?? \"Celeste\";\n" +
            "\tpublic int TotalStrawberries => LevelSet == \"Celeste\" ? saveData?.TotalStrawberries ?? 0 : global::Celeste.Mod.AppleEverestProgressionRuntime.TotalStrawberries(LevelSet, saveData);\n" +
            "\tpublic int TotalGoldenStrawberries => LevelSet == \"Celeste\" ? saveData?.TotalGoldenStrawberries ?? 0 : 0;\n" +
            "\tpublic int TotalHeartGems => LevelSet == \"Celeste\" ? saveData?.TotalHeartGems ?? 0 : global::Celeste.Mod.AppleEverestProgressionRuntime.TotalHearts(LevelSet, saveData);\n" +
            "\tpublic int TotalCassettes => LevelSet == \"Celeste\" ? saveData?.TotalCassettes ?? 0 : global::Celeste.Mod.AppleEverestProgressionRuntime.TotalCassettes(LevelSet, saveData);\n" +
            "\tpublic long TotalTime => LevelSet == \"Celeste\" ? saveData?.Time ?? 0 : global::Celeste.Mod.AppleEverestProgressionRuntime.TotalTime(LevelSet, saveData);\n" +
            "\tpublic int TotalDeaths => LevelSet == \"Celeste\" ? saveData?.TotalDeaths ?? 0 : global::Celeste.Mod.AppleEverestProgressionRuntime.TotalDeaths(LevelSet, saveData);\n" +
            "\tpublic int TotalCompletions => LevelSet == \"Celeste\" ? saveData?.TotalCompletions ?? 0 : global::Celeste.Mod.AppleEverestProgressionRuntime.TotalCompletions(LevelSet, saveData);\n" +
            "\tpublic int MaxCompletions => LevelSet == \"Celeste\" ? 8 : global::Celeste.Mod.AppleEverestProgressionRuntime.MaximumCompletions(LevelSet);\n" +
            "}\n", new UTF8Encoding(false));

        // Desktop Everest publicizes Engine.scene. The accepted DLL contains a
        // direct field reference produced by that publicized contract.
        string engine = Path.Combine(managedRoot, "Monocle", "Engine.cs");
        ReplaceOnce(engine, "\tprivate Scene scene;", "\tpublic Scene scene;");

        // YetAnotherHelper 1.2.5 reflects three exact vanilla Player
        // fields for BubbleField.  The hash-locked semantic replacement is
        // compiled into the same assembly, so expose only that fixed ABI to
        // the generated code. The vertical-only climbNoMoveTimer lookup is omitted.
        // This is deliberately internal rather than a
        // broad/public member expansion and introduces no runtime reflection.
        if (File.Exists(Path.Combine(managedRoot, "Celeste", "Mod", "AppleEverestStatic", "AppleEverestBubbleSemantics.cs")))
        {
            string player = Path.Combine(managedRoot, "Celeste", "Player.cs");
            ReplaceOnce(player, "\tprivate float noWindTimer;", "\tinternal float noWindTimer;");
            ReplaceOnce(player, "\tprivate Vector2 windDirection;", "\tinternal Vector2 windDirection;");
            ReplaceOnce(player, "\tprivate float windTimeout;", "\tinternal float windTimeout;");
        }

        // CaeruleaHelper 1.11.1 calls Everest's public PointWrap backdrop
        // entry point. Keep the canonical renderer otherwise unchanged: the
        // helper explicitly ends this batch before returning to vanilla.
        string backdropRenderer = Path.Combine(managedRoot, "Celeste", "BackdropRenderer.cs");
        ReplaceOnce(backdropRenderer,
            "\tpublic void EndSpritebatch()",
            "\tpublic void StartSpritebatchLooping(BlendState blendState)\n\t{\n\t\tif (!usingSpritebatch)\n\t\t{\n\t\t\tDraw.SpriteBatch.Begin(SpriteSortMode.Deferred, blendState, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, Matrix);\n\t\t}\n\t\tusingSpritebatch = true;\n\t}\n\n\tpublic void EndSpritebatch()");

        // AchievementHelper 1.0.5 uses the exact TextMenu ABI exposed by its
        // pinned Everest build: the public Items view and the one-argument
        // SubHeader constructor. Keep the canonical list private and expose a
        // read-only property; the constructor delegates to vanilla behavior.
        string textMenu = Path.Combine(managedRoot, "Celeste", "TextMenu.cs");
        ReplaceOnce(textMenu,
            "\t\tpublic SubHeader(string title, bool topPadding = true)\n\t\t{",
            "\t\tpublic SubHeader(string title) : this(title, true) { }\n\n" +
            "\t\tpublic SubHeader(string title, bool topPadding)\n\t\t{");
        ReplaceOnce(textMenu,
            "\tprivate List<Item> items = new List<Item>();",
            "\tprivate List<Item> items = new List<Item>();\n\n\tpublic List<Item> Items => items;");
    }

    private static void PreparePinnedEverestManagedTargets(string managedRoot)
    {
        StaticAotCompatibility.PreserveFrozenCollisionRumbleCallers(managedRoot);
        // The pinned Everest Commands patch adds a level-set-aware overload.
        // The closed Apple product supports only the canonical Celeste class,
        // so its exact reviewed ABI delegates to vanilla for that class and
        // safely ignores unknown level-set names. This must exist before the
        // managed-detour catalog rewrites both overloads.
        string commands = Path.Combine(managedRoot, "Celeste", "Commands.cs");
        ReplaceOnce(commands,
            "\tprivate static void CmdHearts(int amount = 24)\n\t{",
            "\tprivate static void CmdHearts(int amount, string levelSet)\n\t{\n" +
            "\t\tif (string.IsNullOrEmpty(levelSet) || levelSet == \"Celeste\") CmdHearts(amount);\n" +
            "\t}\n\n" +
            "\tprivate static void CmdHearts(int amount = 24)\n\t{");

        // The pinned Everest PlayerHair patch factors the color expression
        // into this public method. Helpers hook the method itself, so it must
        // exist before the immutable HookGen dispatcher rewrite runs.
        string playerHair = Path.Combine(managedRoot, "Celeste", "PlayerHair.cs");
        ReplaceOnce(playerHair,
            "\tprivate Vector2 GetHairScale(int index)\n\t{",
            "\tpublic Color GetHairColor(int index)\n\t{\n" +
            "\t\treturn Color * Alpha;\n" +
            "\t}\n\n" +
            "\tprivate Vector2 GetHairScale(int index)\n\t{");
        ReplaceOnce(playerHair,
            ").Draw(Nodes[num], origin, color2, GetHairScale(num));",
            ").Draw(Nodes[num], origin, GetHairColor(num), GetHairScale(num));");

        // This is the exact pinned Everest interception point. The closed
        // product resolves only host-generated factories and typed events;
        // no runtime assembly scanning or attribute discovery is introduced.
        string level = Path.Combine(managedRoot, "Celeste", "Level.cs");
        ReplaceOnce(level,
            "\tpublic void LoadLevel(Player.IntroTypes playerIntro, bool isFromLoader = false)\n\t{",
            "\tpublic static bool LoadCustomEntity(EntityData entityData, Level level)\n" +
            "\t{\n" +
            "\t\tLevelData levelData = level.Session.LevelData;\n" +
            "\t\tVector2 offset = new Vector2(levelData.Bounds.Left, levelData.Bounds.Top);\n" +
            "\t\tEntityID entityId = new EntityID(levelData.Name, entityData.ID);\n" +
            "\t\tif (global::Celeste.Mod.Everest.Events.Level.LoadEntity(level, levelData, offset, entityData)) return true;\n" +
            "\t\tif (!global::Celeste.Mod.GeneratedAppleEverestGameplayRegistry.TryCreateEntity(entityData.Name, entityData, offset, entityId, out Entity entity)) return false;\n" +
            "\t\tlevel.Add(entity);\n" +
            "\t\treturn true;\n" +
            "\t}\n\n" +
            "\tpublic void LoadLevel(Player.IntroTypes playerIntro, bool isFromLoader = false)\n\t{");
    }

    private static void PatchModuleDurability(string managedRoot)
    {
        string userIo = Path.Combine(managedRoot, "Celeste", "UserIO.cs");
        ReplaceOnce(userIo,
            "\tprivate static byte[] savingSettingsData;",
            "\tprivate static byte[] savingSettingsData;\n\n\tprivate static bool appleEverestSaveQueued;\n\n\tprivate static bool appleEverestQueuedFile;\n\n\tprivate static bool appleEverestQueuedSettings;\n\n\tpublic static bool SaveQueued => appleEverestSaveQueued;");
        ReplaceOnce(userIo,
            "\tpublic static void SaveHandler(bool file, bool settings)\n\t{\n\t\tfile = global::Celeste.Mod.AppleEverestStaticRuntime.FilterVanillaFileSave(file);\n\t\tif (!file && !settings)\n\t\t{\n\t\t\treturn;\n\t\t}\n\t\tif (!Saving)",
            "\tpublic static void SaveHandler(bool file, bool settings)\n\t{\n\t\tfile = global::Celeste.Mod.AppleEverestStaticRuntime.FilterVanillaFileSave(file);\n\t\tif (!file && !settings)\n\t\t{\n\t\t\treturn;\n\t\t}\n\t\tif (Saving)\n\t\t{\n\t\t\tappleEverestSaveQueued = true;\n\t\t\tappleEverestQueuedFile |= file;\n\t\t\tappleEverestQueuedSettings |= settings;\n\t\t\treturn;\n\t\t}\n\t\tif (!Saving)");
        ReplaceOnce(userIo,
            "\t\t\t\tsavingFileData = Serialize(SaveData.Instance);",
            "\t\t\t\tsavingFileData = global::Celeste.Mod.AppleEverestProgressionPersistence.SerializeVanillaBase(SaveData.Instance);\n\t\t\t\tglobal::Celeste.Mod.AppleEverestProgressionPersistence.CaptureSave(SaveData.Instance.FileSlot, savingFileData);\n\t\t\t\tglobal::Celeste.Mod.AppleEverestModulePersistence.CaptureSave(SaveData.Instance.FileSlot, savingFileData);");
        ReplaceOnce(userIo,
            "\t\tSaving = false;\n\t\tCeleste.SaveRoutine = null;",
            "\t\tSaving = false;\n\t\tCeleste.SaveRoutine = null;\n\t\tif (appleEverestSaveQueued)\n\t\t{\n\t\t\tbool nextFile = appleEverestQueuedFile;\n\t\t\tbool nextSettings = appleEverestQueuedSettings;\n\t\t\tappleEverestSaveQueued = false;\n\t\t\tappleEverestQueuedFile = false;\n\t\t\tappleEverestQueuedSettings = false;\n\t\t\tSaveHandler(nextFile, nextSettings);\n\t\t}");
        ReplaceOnce(userIo,
            "\t\t\t\tSavingResult &= Save<SaveData>(SaveData.GetFilename(), savingFileData);",
            "\t\t\t\tSavingResult &= Save<SaveData>(SaveData.GetFilename(), savingFileData);\n\t\t\t\tif (SavingResult) SavingResult &= global::Celeste.Mod.AppleEverestProgressionPersistence.CommitCapturedSave();\n\t\t\t\telse global::Celeste.Mod.AppleEverestProgressionPersistence.DiscardCapturedSave();\n\t\t\t\tif (SavingResult) SavingResult &= global::Celeste.Mod.AppleEverestModulePersistence.CommitCapturedSave();\n\t\t\t\telse global::Celeste.Mod.AppleEverestModulePersistence.DiscardCapturedSave();");

        string saveData = Path.Combine(managedRoot, "Celeste", "SaveData.cs");
        ReplaceOnce(saveData,
            "\t\tInstance.FileSlot = slot;\n\t\tInstance.AfterInitialize();",
            "\t\tInstance.FileSlot = slot;\n\t\tInstance.AfterInitialize();\n\t\tbyte[] appleEverestBaseSave = global::Celeste.Mod.AppleEverestProgressionPersistence.SerializeVanillaBase(Instance);\n\t\tglobal::Celeste.Mod.AppleEverestProgressionPersistence.ActivateSlot(slot, appleEverestBaseSave);\n\t\tglobal::Celeste.Mod.AppleEverestModulePersistence.ActivateSlot(slot, appleEverestBaseSave);");
        ReplaceOnce(saveData,
            "\tpublic static bool TryDelete(int slot)\n\t{\n\t\treturn UserIO.Delete(GetFilename(slot));\n\t}",
            "\tpublic static bool TryDelete(int slot)\n\t{\n\t\tbool vanilla = UserIO.Delete(GetFilename(slot));\n\t\tbool progression = vanilla && global::Celeste.Mod.AppleEverestProgressionPersistence.DeleteSlot(slot);\n\t\tbool modules = vanilla && global::Celeste.Mod.AppleEverestModulePersistence.DeleteSlot(slot);\n\t\treturn vanilla && progression && modules;\n\t}");
        ReplaceOnce(saveData,
            "\tprivate void AppleEverestOriginal_StartSession(Session session)\n\t{\n\t\tLastArea = session.Area;\n\t\tLastArea_Safe = LastArea;\n\t\tCurrentSession = session;",
            "\tprivate void AppleEverestOriginal_StartSession(Session session)\n\t{\n\t\tSession appleEverestPreviousSession = CurrentSession;\n\t\tLastArea = session.Area;\n\t\tLastArea_Safe = LastArea;\n\t\tCurrentSession = session;\n\t\tif (!object.ReferenceEquals(appleEverestPreviousSession, session))\n\t\t{\n\t\t\tvar appleEverestRoute = global::Celeste.Mod.AppleEverestCollabRuntime.TakeRestartRoute(session);\n\t\t\tglobal::Celeste.Mod.AppleEverestModulePersistence.ResetSessionForNewVanillaSession(FileSlot);\n\t\t\tappleEverestRoute?.CopyTo(global::Celeste.Mod.AppleEverestCollabRuntime.Route);\n\t\t}");

        ReplaceOnce(Path.Combine(managedRoot, "Celeste", "TextMenu.cs"),
            "\tpublic TextMenu Add(Item item)\n\t{",
            "\tinternal void AppleEverestReplaceReturnButton(string label, Item replacement)\n\t{\n" +
            "\t\tint index = items.FindIndex(item => item is Button button && button.Label == label);\n" +
            "\t\tAdd(replacement);\n\t\titems.Remove(replacement);\n" +
            "\t\tif (index < 0) index = items.Count - 1;\n\t\tif (index >= 0)\n\t\t{\n\t\t\tItem previous = items[index];\n\t\t\titems.RemoveAt(index);\n" +
            "\t\t\tRemove(previous.ValueWiggler);\n\t\t\tRemove(previous.SelectWiggler);\n\t\t\tprevious.Container = null;\n\t\t}\n" +
            "\t\telse index = 0;\n" +
            "\t\titems.Insert(index, replacement);\n\t\tRecalculateSize();\n\t}\n\n\tpublic TextMenu Add(Item item)\n\t{");

        string fileSelect = Path.Combine(managedRoot, "Celeste", "OuiFileSelect.cs");
        ReplaceOnce(fileSelect,
            "\t\t\t\t\t\tsaveData.AfterInitialize();\n\t\t\t\t\t\touiFileSelectSlot = new OuiFileSelectSlot(i, this, saveData);",
            "\t\t\t\t\t\tsaveData.AfterInitialize();\n\t\t\t\t\t\tbyte[] appleEverestBaseSave = global::Celeste.Mod.AppleEverestProgressionPersistence.SerializeVanillaBase(saveData);\n\t\t\t\t\t\tglobal::Celeste.Mod.AppleEverestProgressionPersistence.PreloadSlot(i, appleEverestBaseSave);\n\t\t\t\t\t\tglobal::Celeste.Mod.AppleEverestModulePersistence.PreloadSlot(i, appleEverestBaseSave);\n\t\t\t\t\t\touiFileSelectSlot = new OuiFileSelectSlot(i, this, saveData);");

        string fileSelectSlot = Path.Combine(managedRoot, "Celeste", "OuiFileSelectSlot.cs");
        ReplaceOnce(fileSelectSlot,
            "\tpublic void CreateButtons()\n\t{\n\t\tbuttons.Clear();",
            "\tpublic void CreateButtons()\n\t{\n\t\tif (SaveData != null) global::Celeste.Mod.AppleEverestModulePersistence.ActivateSaveData(FileSlot, UserIO.Serialize(SaveData));\n\t\tbuttons.Clear();");
    }

    private static void PatchLevelSetProgression(string managedRoot)
    {
        string saveData = Path.Combine(managedRoot, "Celeste", "SaveData.cs");
        ReplaceOnce(saveData,
            "\t\twhile (Areas.Count < AreaData.Areas.Count)\n\t\t{\n\t\t\tAreas.Add(new AreaStats(Areas.Count));\n\t\t}\n\t\twhile (Areas.Count > AreaData.Areas.Count)",
            "\t\tint appleEverestVanillaAreaCount = global::Celeste.Mod.AppleEverestProgressionRuntime.VanillaAreaCount > 0 ? global::Celeste.Mod.AppleEverestProgressionRuntime.VanillaAreaCount : AreaData.Areas.Count;\n" +
            "\t\twhile (Areas.Count < appleEverestVanillaAreaCount)\n\t\t{\n\t\t\tAreas.Add(new AreaStats(Areas.Count));\n\t\t}\n\t\twhile (Areas.Count > appleEverestVanillaAreaCount)");
        ReplaceOnce(saveData,
            "\tpublic int MaxArea\n\t{\n\t\tget\n\t\t{\n\t\t\tif (Celeste.PlayMode == Celeste.PlayModes.Event)\n\t\t\t{\n\t\t\t\treturn 2;\n\t\t\t}\n\t\t\treturn AreaData.Areas.Count - 1;\n\t\t}\n\t}\n\n\tpublic int MaxAssistArea => AreaData.Areas.Count - 1;",
            "\tpublic int MaxArea\n\t{\n\t\tget\n\t\t{\n\t\t\tif (Celeste.PlayMode == Celeste.PlayModes.Event)\n\t\t\t{\n\t\t\t\treturn 2;\n\t\t\t}\n\t\t\treturn global::Celeste.Mod.AppleEverestProgressionRuntime.VanillaMaximumArea;\n\t\t}\n\t}\n\n\tpublic int MaxAssistArea => global::Celeste.Mod.AppleEverestProgressionRuntime.VanillaMaximumArea;");
        ReplaceOnce(saveData,
            "\tprivate void AppleEverestOriginal_AddDeath(AreaKey area)\n\t{\n\t\tTotalDeaths++;",
            "\tprivate void AppleEverestOriginal_AddDeath(AreaKey area)\n\t{\n\t\tif (global::Celeste.Mod.AppleEverestProgressionRuntime.IsCustom(area))\n\t\t{\n\t\t\tAreas[area.ID].Modes[(int)area.Mode].Deaths++;\n\t\t\treturn;\n\t\t}\n\t\tTotalDeaths++;");
        ReplaceOnce(saveData,
            "\t\tAreaModeStats areaModeStats = Areas[area.ID].Modes[(int)area.Mode];\n\t\tif (!areaModeStats.Strawberries.Contains(strawberry))",
            "\t\tAreaModeStats areaModeStats = Areas[area.ID].Modes[(int)area.Mode];\n\t\tif (global::Celeste.Mod.AppleEverestProgressionRuntime.IsCustom(area))\n\t\t{\n\t\t\tif (areaModeStats.Strawberries.Add(strawberry) && global::Celeste.Mod.AppleEverestProgressionRuntime.CountsAsCollectedStrawberry(area, strawberry)) areaModeStats.TotalStrawberries++;\n\t\t\treturn;\n\t\t}\n\t\tif (!areaModeStats.Strawberries.Contains(strawberry))");
        ReplaceOnce(saveData,
            "\tpublic void AddTime(AreaKey area, long time)\n\t{\n\t\tTime += time;\n\t\tAreas[area.ID].Modes[(int)area.Mode].TimePlayed += time;\n\t}",
            "\tpublic void AddTime(AreaKey area, long time)\n\t{\n\t\tif (!global::Celeste.Mod.AppleEverestProgressionRuntime.IsCustom(area)) Time += time;\n\t\tAreas[area.ID].Modes[(int)area.Mode].TimePlayed += time;\n\t}");
        ReplaceOnce(saveData,
            "\tprivate void AppleEverestOriginal_RegisterCassette(AreaKey area)\n\t{\n\t\tAreas[area.ID].Cassette = true;\n\t\tAchievements.Register(Achievement.CASS);",
            "\tprivate void AppleEverestOriginal_RegisterCassette(AreaKey area)\n\t{\n\t\tAreas[area.ID].Cassette = true;\n\t\tif (global::Celeste.Mod.AppleEverestProgressionRuntime.IsCustom(area)) return;\n\t\tAchievements.Register(Achievement.CASS);");
        ReplaceOnce(saveData,
            "\t\t\tforeach (AreaStats area in Areas)\n\t\t\t{\n\t\t\t\tfor (int i = 0; i < area.Modes.Length; i++)",
            "\t\t\tfor (int appleEverestAreaIndex = 0; appleEverestAreaIndex < global::Celeste.Mod.AppleEverestProgressionRuntime.VanillaAreaCount && appleEverestAreaIndex < Areas.Count; appleEverestAreaIndex++)\n\t\t\t{\n\t\t\t\tAreaStats area = Areas[appleEverestAreaIndex];\n\t\t\t\tfor (int i = 0; i < area.Modes.Length; i++)");

        string levelExit = Path.Combine(managedRoot, "Celeste", "LevelExit.cs");
        ReplaceOnce(levelExit,
            "\t\tthis.session = session;\n\t\tthis.mode = mode;\n\t\tthis.snow = snow;",
            "\t\tthis.session = session;\n\t\tif (mode is Mode.Restart or Mode.GoldenBerryRestart) global::Celeste.Mod.AppleEverestCollabRuntime.RememberRestartRoute(session);\n\t\tthis.mode = mode == Mode.Completed && global::Celeste.Mod.AppleEverestProgressionRuntime.IsCustom(session.Area) ? Mode.SaveAndQuit : mode;\n\t\tthis.snow = snow;");
    }

    private static void PatchSecondCollabSemantics(string heartDoor, string strawberry)
    {
        ReplaceOnce(heartDoor,
            "\tpublic int HeartGems\n\t{\n\t\tget\n\t\t{",
            "\tpublic int HeartGems\n\t{\n\t\tget\n\t\t{\n" +
            "\t\t\tif (this is global::Celeste.Mod.AppleEverestMiniHeartDoor) return global::Celeste.Mod.AppleEverestSecondCollabRuntime.HeartGems(this, 0);");
        ReplaceOnce(heartDoor,
            "\t\tlevel.Session.SetFlag(\"opened_heartgem_door_\" + Requires);",
            "\t\tlevel.Session.SetFlag(\"opened_heartgem_door_\" + Requires);\n" +
            "\t\tif (this is global::Celeste.Mod.AppleEverestMiniHeartDoor) level.Session.SetFlag(global::Celeste.Mod.AppleEverestSecondCollabRuntime.DoorFlag(this, \"opened_heartgem_door_\" + Requires));");
        ReplaceOnce(heartDoor,
            "\t\telse\n\t\t{\n\t\t\tAdd(new Coroutine(Routine()));\n\t\t}\n\t}\n\n\tpublic override void Awake(Scene scene)",
            "\t\telse\n\t\t{\n\t\t\tAdd(new Coroutine(Routine()));\n\t\t}\n" +
            "\t\tglobal::Celeste.Mod.AppleEverestSecondCollabRuntime.ConfigureHeartDoor(this, TopSolid, BotSolid, openDistance, Opened);\n\t}\n\n\tpublic override void Awake(Scene scene)");
        ReplaceOnce(heartDoor,
            "\t\t\tif (entity3 != null && Math.Abs(entity3.X - base.Center.X) < 80f && entity3.X < base.X)",
            "\t\t\tif (global::Celeste.Mod.AppleEverestSecondCollabRuntime.CanApproachDoor(this, entity3, entity3 != null && Math.Abs(entity3.X - base.Center.X) < 80f && entity3.X < base.X))");
        ReplaceOnce(heartDoor,
            "\t\t}\n\t}\n\n\tpublic void RenderBloom()",
            "\t\t}\n\t\tglobal::Celeste.Mod.AppleEverestSecondCollabRuntime.ClampHeartDoor(this, TopSolid, BotSolid, Opened);\n\t}\n\n\tpublic void RenderBloom()");
        ReplaceOnce(heartDoor,
            "\t\tDraw.Rect(bounds, Calc.HexToColor(\"18668f\"));",
            "\t\tDraw.Rect(bounds, global::Celeste.Mod.AppleEverestSecondCollabRuntime.HeartDoorColor(this, Calc.HexToColor(\"18668f\")));");

        ReplaceOnce(strawberry,
            "\t\tif ((scene as Level).Session.BloomBaseAdd > 0.1f)\n\t\t{\n\t\t\tbloom.Alpha *= 0.5f;\n\t\t}\n\t}",
            "\t\tif ((scene as Level).Session.BloomBaseAdd > 0.1f)\n\t\t{\n\t\t\tbloom.Alpha *= 0.5f;\n\t\t}\n" +
            "\t\tglobal::Celeste.Mod.AppleEverestSecondCollabRuntime.ConfigureStrawberry(this, sprite, bloom, light);\n\t}");
        ReplaceOnce(strawberry,
            "\tpublic override void Update()\n\t{\n\t\tif (WaitingOnSeeds)",
            "\tpublic override void Update()\n\t{\n\t\tglobal::Celeste.Mod.AppleEverestSecondCollabRuntime.UpdateSpecialBerry(this);\n\t\tif (WaitingOnSeeds)");
        ReplaceOnce(strawberry,
            "\t\t\t\t\t\tif (player.CollideCheck<GoldBerryCollectTrigger>() || (base.Scene as Level).Completed)\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\tflag = true;\n\t\t\t\t\t\t}",
            "\t\t\t\t\t\tif (global::Celeste.Mod.AppleEverestSecondCollabRuntime.ShouldCollectGolden(this, player, player.CollideCheck<GoldBerryCollectTrigger>() || (base.Scene as Level).Completed))\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\tflag = true;\n\t\t\t\t\t\t}");
        ReplaceOnce(strawberry,
            "\t\tif (Golden)\n\t\t{\n\t\t\t(base.Scene as Level).Session.GrabbedGolden = true;\n\t\t}",
            "\t\tif (Golden && global::Celeste.Mod.AppleEverestSecondCollabRuntime.ShouldSetGrabbedGolden(this))\n\t\t{\n\t\t\t(base.Scene as Level).Session.GrabbedGolden = true;\n\t\t}");
    }

    private static void PatchTracker(string path) => ReplaceOnce(path,
        "\t\t}\n\t}\n\n\tprivate static List<Type> GetSubclasses(Type type)",
        "\t\t}\n\t\tglobal::Celeste.Mod.GeneratedAppleEverestGameplayRegistry.RegisterTrackerTypes();\n\t}\n\n\tprivate static List<Type> GetSubclasses(Type type)");

    private static void PatchPooler(string path)
    {
        ReplaceOnce(path,
            "\t\t}\n\t}\n\n\tpublic T Create<T>() where T : Entity, new()",
            "\t\t}\n\t\tglobal::Celeste.Mod.GeneratedAppleEverestGameplayRegistry.RegisterPooledTypes(this);\n\t}\n\n\tpublic T Create<T>() where T : Entity, new()");
        ReplaceOnce(path,
            "\t\tthrow new InvalidOperationException(\"Missing AOT pooled factory for: \" + type.FullName);",
            "\t\tif (global::Celeste.Mod.GeneratedAppleEverestGameplayRegistry.TryCreatePooled(type, out Entity appleEverestEntity)) return appleEverestEntity;\n\t\tthrow new InvalidOperationException(\"Missing AOT pooled factory for: \" + type.FullName);");
    }

    private static void PatchProject(string path, string closureRoot)
    {
        ReplaceOnce(path, "<DefineConstants>$(DefineConstants);", "<DefineConstants>$(DefineConstants);EVEREST_APPLE_STATIC_AOT;");
        string assemblies = Path.Combine(closureRoot, "assemblies");
        if (!Directory.Exists(assemblies)) return;
        StringBuilder references = new("  <ItemGroup>\n");
        foreach (string assembly in Directory.EnumerateFiles(assemblies, "*.dll").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(assembly);
            references.Append("    <Reference Include=\"").Append(name).Append("\"><HintPath>AppleEverestAssemblies/")
                .Append(Path.GetFileName(assembly)).AppendLine("</HintPath><Private>true</Private></Reference>");
        }
        references.AppendLine("  </ItemGroup>");
        if (File.Exists(Path.Combine(closureRoot, "managed", "AppleEverestStaticIl.targets")))
            references.AppendLine("  <Import Project=\"AppleEverestStaticIl.targets\" />");
        ReplaceOnce(path, "</Project>", references + "</Project>");
    }

    private static void ReplaceOnce(string path, string needle, string replacement)
    {
        string text = File.ReadAllText(path);
        int first = text.IndexOf(needle, StringComparison.Ordinal);
        if (first < 0 || text.IndexOf(needle, first + needle.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidDataException($"locked target must occur exactly once: {Path.GetFileName(path)}");
        File.WriteAllText(path, text[..first] + replacement + text[(first + needle.Length)..], new UTF8Encoding(false));
    }

    private static void ReplaceExactOccurrences(string path, string needle, string replacement, int expected)
    {
        string text = File.ReadAllText(path);
        int count = 0;
        for (int offset = 0; (offset = text.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0;
             offset += needle.Length)
            count++;
        if (count != expected)
            throw new InvalidDataException($"locked target must occur exactly {expected} times: {Path.GetFileName(path)}");
        File.WriteAllText(path, text.Replace(needle, replacement, StringComparison.Ordinal), new UTF8Encoding(false));
    }

    private static void ReplaceOneOf(string path, IReadOnlyList<string> alternatives,
        Func<string, string> replacement)
    {
        string text = File.ReadAllText(path);
        string[] matches = alternatives.Where(needle => text.IndexOf(needle, StringComparison.Ordinal) >= 0).ToArray();
        if (matches.Length != 1 || text.IndexOf(matches[0], text.IndexOf(matches[0], StringComparison.Ordinal) + matches[0].Length,
                StringComparison.Ordinal) >= 0)
            throw new InvalidDataException($"one locked platform target must occur exactly once: {Path.GetFileName(path)}");
        string needle = matches[0];
        int first = text.IndexOf(needle, StringComparison.Ordinal);
        File.WriteAllText(path, text[..first] + replacement(needle) + text[(first + needle.Length)..], new UTF8Encoding(false));
    }

    private static void ValidateOnce(string path, string needle)
    {
        string text = File.ReadAllText(path);
        int first = text.IndexOf(needle, StringComparison.Ordinal);
        if (first < 0 || text.IndexOf(needle, first + needle.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidDataException($"locked target must occur exactly once: {Path.GetFileName(path)}");
    }

    private static string RegistrySource(AppleEverestProfile profile,
        IReadOnlyList<(ResolvedMod Mod, AppleStaticDeclaration Declaration)> modules,
        IReadOnlyList<ResolvedMod> resolved,
        IReadOnlyDictionary<string, GeneratedDurabilityAdapter> durabilityAdapters,
        string durabilityClosureSha256)
    {
        StringBuilder result = new();
        result.AppendLine("using System;").AppendLine("namespace Celeste.Mod;").AppendLine()
            .AppendLine("internal static class GeneratedAppleEverestModuleRegistry")
            .AppendLine("{")
            .AppendLine($"    internal const string Profile = \"{Escape(profile.Profile)}\";")
            .AppendLine($"    internal const string CoreSessionSchema = \"{CoreSessionSchema}\";")
            .AppendLine($"    internal const string DurabilityClosureSha256 = \"{Escape(durabilityClosureSha256)}\";")
            .AppendLine("    internal static readonly AppleEverestModuleDescriptor[] Modules =")
            .AppendLine("    {");
        if (NeedsSelectedCoreSession(resolved))
        {
            if (profile.Everest.Tag != "stable-1.6458.0")
                throw new InvalidDataException("selected CoreModule session requires the reviewed Everest identity");
            result.AppendLine("        new AppleEverestModuleDescriptor(\"Everest\", \"1.6458.0\", Array.Empty<string>(), new[] { \"FrostHelper\" },")
                .AppendLine("            static () => new AppleEverestCoreModule(), null, null, null,")
                .AppendLine("            static () => new AppleEverestCoreSession(), AppleEverestCoreDurability.Adapter),");
        }
        foreach ((ResolvedMod mod, AppleStaticDeclaration declaration) in modules)
        {
            string dependencies = StringArray(EverestGraphResolver.RuntimeRequirements(mod).Select(dep => dep.Name));
            string requiredBy = StringArray(resolved.Where(candidate => EverestGraphResolver.RuntimeRequirements(candidate).Any(dependency =>
                    dependency.Name == mod.Metadata.Name)).Select(candidate => candidate.Metadata.Name));
            result.Append("        new AppleEverestModuleDescriptor(\"").Append(Escape(mod.Metadata.Name)).Append("\", \"")
                .Append(Escape(mod.Metadata.Version)).Append("\", ").Append(dependencies).Append(", ")
                .Append(requiredBy).Append(", static () => new ")
                .Append("global::").Append(declaration.ModuleType).Append("(), ")
                .Append(SettingsFactory(declaration)).Append(", ")
                .Append(InputBindingInitializer(declaration)).Append(", ")
                .Append(Factory(declaration.SaveDataType)).Append(", ")
                .Append(Factory(declaration.SessionType)).Append(", ")
                .Append(durabilityAdapters.TryGetValue(mod.Metadata.Name, out GeneratedDurabilityAdapter? adapter)
                    ? "GeneratedAppleEverestModuleDurabilityAdapters." + adapter.Field : "null")
                .AppendLine("),");
        }
        result.AppendLine("    };")
            .AppendLine("    internal static readonly AppleEverestSettingDescriptor[] Settings =")
            .AppendLine("    {");
        foreach ((ResolvedMod mod, AppleStaticDeclaration declaration) in modules)
            foreach (AppleSettingProperty property in declaration.SettingsProperties)
                result.Append("        ").Append(SettingDescriptor(mod.Metadata.Name, declaration, property)).AppendLine(",");
        return result.AppendLine("    };").AppendLine("}").ToString();

        static string StringArray(IEnumerable<string> values)
        {
            string[] items = values.ToArray();
            return items.Length == 0 ? "System.Array.Empty<string>()" :
                "new[] { " + string.Join(", ", items.Select(value => $"\"{Escape(value)}\"")) + " }";
        }
    }

    private static string ContentManifestSource(IReadOnlyList<ResolvedMod> mods,
        IReadOnlyList<ContentMountRecord> staged, string contentRoot, IReadOnlyList<MapBindingsGenerator.Binding> mapBindings)
    {
        StringBuilder result = new("namespace Celeste.Mod;\n\ninternal static class GeneratedAppleEverestContentManifest\n{\n    internal static readonly string[] Entries =\n    {\n");
        foreach (string item in staged.Select(value => value.LogicalPath).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
            result.Append("        \"").Append(Escape(item)).AppendLine("\",");
        string[] maps = staged.Select(value => value.LogicalPath)
            .Where(value => value.StartsWith("Maps/", StringComparison.Ordinal) && value.EndsWith(".bin", StringComparison.Ordinal))
            .Select(value => value["Maps/".Length..^4])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        result.AppendLine("    };")
            .AppendLine("    internal static readonly AppleEverestModContentDescriptor[] ModContents =")
            .AppendLine("    {");
        foreach (ResolvedMod mod in mods)
            result.Append("        new AppleEverestModContentDescriptor(\"").Append(Escape(mod.Metadata.Name))
                .Append("\", \"").Append(Escape(mod.Metadata.Version)).AppendLine("\"),");
        result.AppendLine("    };")
            .AppendLine("    internal static readonly AppleEverestStaticAssetDescriptor[] StaticAssets =")
            .AppendLine("    {");
        // Core atlas content is mounted below, but has no ordinary mod owner.
        foreach (ContentMountRecord mount in staged.Where(value => value.Order >= 0).OrderBy(value => value.Order)
                     .ThenBy(value => value.SourcePath, StringComparer.Ordinal))
        {
            string extension = Path.GetExtension(mount.SourcePath);
            string virtualPath = StaticAssetGenerator.VirtualPath(mount.SourcePath);
            bool yaml = extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
            result.Append("        new AppleEverestStaticAssetDescriptor(\"").Append(Escape(mount.Owner))
                .Append("\", \"").Append(Escape(virtualPath)).Append("\", \"")
                .Append(Escape(mount.LogicalPath)).Append("\", ").Append(yaml ? "true" : "false")
                .Append(", \"").Append(Escape(extension.TrimStart('.').ToLowerInvariant())).AppendLine("\"),");
        }
        result.AppendLine("    };")
            .AppendLine("    internal static readonly AppleEverestSpriteBankDescriptor[] SpriteBanks =")
            .AppendLine("    {");
        foreach (ContentMountRecord mount in staged.OrderBy(value => value.Order)
                     .ThenBy(value => value.SourcePath, StringComparer.Ordinal))
        {
            if (!IsSpriteBankXml(contentRoot, mount)) continue;
            // Selected map metadata is applied when that map loads, after the
            // helper defaults. It must not become a process-wide sprite bank.
            if (mapBindings.Any(map => map.Sprites == mount.LogicalPath) ||
                mount.Owner == "StrawberryJam2021" && mount.SourcePath == "Graphics/SJ2021xmls/BeginnerLobby/Sprites.xml") continue;
            result.Append("        new AppleEverestSpriteBankDescriptor(\"").Append(Escape(mount.Owner))
                .Append("\", \"").Append(Escape(mount.LogicalPath)).AppendLine("\"),");
        }
        result.AppendLine("    };")
            .AppendLine("    internal static readonly AppleEverestAtlasMountDescriptor[] AtlasMounts =")
            .AppendLine("    {");
        foreach (ContentMountRecord mount in staged.OrderBy(value => value.Order)
                     .ThenBy(value => value.SourcePath, StringComparer.Ordinal))
        {
            if (!TryAtlasMount(mount.SourcePath, out string atlas, out string key)) continue;
            result.Append("        new AppleEverestAtlasMountDescriptor(\"").Append(Escape(mount.Owner)).Append("\", \"")
                .Append(Escape(atlas)).Append("\", \"").Append(Escape(key)).Append("\", \"")
                .Append(Escape(mount.LogicalPath)).AppendLine("\"),");
        }
        result.AppendLine("    };")
            .AppendLine("    internal static readonly string[] MapPaths =")
            .AppendLine("    {");
        foreach (string mapPath in maps)
            result.Append("        \"").Append(Escape(mapPath)).AppendLine("\",");
        result.AppendLine("    };")
            .Append("    internal const string FirstMapPath = ")
            .Append(maps.FirstOrDefault() is string map ? $"\"{Escape(map)}\"" : "null")
            .AppendLine(";")
            .AppendLine("    internal static bool Has(string path) => System.Array.IndexOf(Entries, path) >= 0;")
            .Append("    internal const string ResolvedOrder = \"").Append(Escape(string.Join(",", mods.Select(mod => mod.Metadata.Name)))).AppendLine("\";")
            .AppendLine("}");
        return result.ToString();

        static bool TryAtlasMount(string sourcePath, out string atlas, out string key)
        {
            const string gameplay = "Graphics/Atlases/Gameplay/";
            const string gui = "Graphics/Atlases/Gui/";
            const string journal = "Graphics/Atlases/Journal/";
            const string checkpoints = "Graphics/Atlases/Checkpoints/";
            const string grades = "Graphics/ColorGrading/";
            string prefix;
            if (sourcePath.StartsWith(gameplay, StringComparison.Ordinal))
            {
                atlas = "Gameplay";
                prefix = gameplay;
            }
            else if (sourcePath.StartsWith(gui, StringComparison.Ordinal))
            {
                atlas = "Gui";
                prefix = gui;
            }
            else if (sourcePath.StartsWith(journal, StringComparison.Ordinal))
            {
                atlas = "Journal";
                prefix = journal;
            }
            else if (sourcePath.StartsWith(checkpoints, StringComparison.Ordinal))
            {
                atlas = "Checkpoints";
                prefix = checkpoints;
            }
            else if (sourcePath.StartsWith(grades, StringComparison.Ordinal))
            {
                atlas = "ColorGrades";
                prefix = grades;
            }
            else
            {
                atlas = "";
                key = "";
                return false;
            }
            if (!sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                key = "";
                return false;
            }
            key = sourcePath[prefix.Length..^4].Replace('\\', '/');
            return key.Length > 0;
        }
    }

    private static bool IsSpriteBankXml(string contentRoot, ContentMountRecord mount)
    {
        if (!mount.SourcePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) return false;
        string path = Path.Combine(contentRoot, mount.LogicalPath.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreWhitespace = true
            });
            while (reader.Read())
                if (reader.NodeType == XmlNodeType.Element)
                    return reader.LocalName == "Sprites";
            return false;
        }
        catch (XmlException)
        {
            // Public mod packages can contain files with an .xml suffix that are
            // opaque game payloads. They are content, but not sprite-bank XML.
            return false;
        }
    }

    private static string ProgressionManifestSource(
        IReadOnlyList<MapProgressionRecord> maps,
        IReadOnlyList<LevelSetProgressionRecord> levelSets)
    {
        StringBuilder result = new("namespace Celeste.Mod;\n\ninternal static class GeneratedAppleEverestProgressionManifest\n{\n" +
            $"    internal const int SchemaVersion = {ProductPolicy.LevelSetProgressionSchemaVersion};\n" +
            "    internal static readonly AppleEverestMapProgressionDescriptor[] Maps =\n    {\n");
        foreach (MapProgressionRecord map in maps)
        {
            result.Append("        new AppleEverestMapProgressionDescriptor(\"").Append(Escape(map.Path))
                .Append("\", \"").Append(Escape(map.Sid)).Append("\", \"").Append(Escape(map.LevelSet))
                .Append("\", \"").Append(Escape(map.MapSha256)).Append("\", \"")
                .Append(Escape(map.CompatibilityId)).Append("\", ")
                .Append(StringArray(map.Rooms)).Append(", ").Append(map.Strawberries).Append(", ")
                .Append(map.Heart ? "true" : "false").Append(", ").Append(map.Cassette ? "true" : "false")
                .Append(", ").Append(StringArray(map.Checkpoints)).Append(", ")
                .Append(StringArray(map.AreaModes)).Append(", ")
                .Append(map.CompletionAvailable ? "true" : "false").Append(", ")
                .Append(Presentation(map.Presentation ?? MapPresentationRecord.EverestDefault)).AppendLine("),");
        }
        result.AppendLine("    };")
            .AppendLine("    internal static readonly AppleEverestLevelSetProgressionDescriptor[] LevelSets =")
            .AppendLine("    {");
        foreach (LevelSetProgressionRecord levelSet in levelSets)
            result.Append("        new AppleEverestLevelSetProgressionDescriptor(\"").Append(Escape(levelSet.LevelSet))
                .Append("\", \"").Append(levelSet.Identity).Append("\", ").Append(StringArray(levelSet.MapSids))
                .Append(", ").Append(levelSet.MaximumStrawberries).Append(", ").Append(levelSet.MaximumHearts)
                .Append(", ").Append(levelSet.MaximumCassettes).Append(", ")
                .Append(levelSet.MaximumCompletions).AppendLine("),");
        return result.AppendLine("    };").AppendLine("}").ToString();

        static string StringArray(IEnumerable<string> values)
        {
            string[] items = values.ToArray();
            return items.Length == 0 ? "System.Array.Empty<string>()" :
                "new[] { " + string.Join(", ", items.Select(value => $"\"{Escape(value)}\"")) + " }";
        }

        static string Presentation(MapPresentationRecord value)
        {
            string Text(string text) => "\"" + Escape(text) + "\"";
            string Float(float number) => number.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "f";
            string Bool(bool state) => state ? "true" : "false";
            return "new AppleEverestMapPresentationDescriptor(" + string.Join(", ", new[]
            {
                Text(value.Icon), Text(value.TitleBaseColor), Text(value.TitleAccentColor), Text(value.TitleTextColor),
                Text(value.IntroType), Bool(value.Dreaming), Text(value.ColorGrade), Text(value.Wipe),
                Float(value.DarknessAlpha), Float(value.BloomBase), Float(value.BloomStrength), Text(value.Jumpthru),
                Text(value.CoreMode), Text(value.Inventory), Text(value.Music), Text(value.Ambience),
                Text(value.StartLevel), Bool(value.HeartIsEnd), Bool(value.IgnoreLevelAudioLayerData)
            }) + ")";
        }
    }

    private static string ProgressionManifestText(IEnumerable<MapProgressionRecord> maps) =>
        "APPLE_EVEREST_LEVELSET_PROGRESSION_V1\n" + string.Join("\n", maps.Select(map => string.Join("\t",
            map.Sid, map.LevelSet, map.MapSha256, map.CompatibilityId, map.Strawberries,
            map.Heart ? "heart" : "no-heart", map.Cassette ? "cassette" : "no-cassette",
            string.Join(",", map.Rooms), string.Join(",", map.Checkpoints),
            string.Join(",", map.AreaModes), map.CompletionAvailable ? "completion" : "no-completion",
            PresentationText(map.Presentation ?? MapPresentationRecord.EverestDefault)))) + "\n";

    private static string PresentationText(MapPresentationRecord value) => string.Join("|", new[]
    {
        value.Icon, value.TitleBaseColor, value.TitleAccentColor, value.TitleTextColor, value.IntroType,
        value.Dreaming ? "dreaming" : "awake", value.ColorGrade, value.Wipe,
        value.DarknessAlpha.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        value.BloomBase.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        value.BloomStrength.ToString("R", System.Globalization.CultureInfo.InvariantCulture), value.Jumpthru,
        value.CoreMode, value.Inventory, value.Music, value.Ambience, value.StartLevel,
        value.HeartIsEnd ? "heart-end" : "heart-normal",
        value.IgnoreLevelAudioLayerData ? "ignore-audio-layers" : "level-audio-layers"
    });

    private static string CustomAudioManifestSource(
        IReadOnlyList<(CustomAudioBankPlan Plan, int Ordinal)> banks, string manifestSha256)
    {
        StringBuilder result = new("using System;\n\nnamespace Celeste.Mod;\n\ninternal static class GeneratedAppleEverestCustomAudioManifest\n{\n");
        result.Append("    internal const string Schema = \"").Append(CustomAudioManifest.Schema).AppendLine("\";")
            .Append("    internal const string ManifestSha256 = \"").Append(manifestSha256).AppendLine("\";")
            .AppendLine("    internal static readonly AppleEverestCustomBankDescriptor[] Banks =")
            .AppendLine("    {");
        foreach ((CustomAudioBankPlan bank, int ordinal) in banks.OrderBy(item => item.Ordinal))
        {
            result.Append("        new AppleEverestCustomBankDescriptor(\"").Append(Escape(bank.Owner)).Append("\", \"")
                .Append(Escape(bank.Version)).Append("\", ").Append(ordinal).Append(", \"")
                .Append(Escape(bank.StagedPath)).Append("\", \"").Append(bank.BankSha256).Append("\", new Guid(\"")
                .Append(bank.BankId.ToString("D")).Append("\"), \"").Append(Escape(bank.BankPath))
                .AppendLine("\", new AppleEverestCustomAudioGuidDescriptor[]")
                .AppendLine("        {");
            foreach (CustomAudioGuidRecord guid in bank.Guids)
                result.Append("            new AppleEverestCustomAudioGuidDescriptor(new Guid(\"")
                    .Append(guid.Id.ToString("D")).Append("\"), \"").Append(Escape(guid.Path)).Append("\", \"")
                    .Append(Escape(guid.Kind)).AppendLine("\"),");
            result.AppendLine("        }),");
        }
        return result.AppendLine("    };").AppendLine("}").ToString();
    }

    internal static string GameplayRegistrySource(IReadOnlyList<(ResolvedMod Mod, AppleStaticDeclaration Declaration)> modules,
        IReadOnlyList<ResolvedMod> ordered)
    {
        string[] entities = modules
            .SelectMany(item => item.Declaration.TrackedEntityTypes)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] pooled = modules
            .SelectMany(item => item.Declaration.PooledEntityTypes)
            .Concat(ordered.SelectMany(mod => mod.StaticSemanticLowering?.PooledEntityTypes ?? []))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        StringBuilder result = new("using System;\nusing System.Collections.Generic;\nusing Monocle;\n\nnamespace Celeste.Mod;\n\ninternal static class GeneratedAppleEverestGameplayRegistry\n{\n");
        result.Append("    internal static bool CountsAsMapStrawberry(string id) => id == \"strawberry\"");
        foreach (string id in ordered.SelectMany(mod => mod.StaticSemanticLowering?.Factories ?? [])
                     .Where(StaticSemanticLowering.CountsAsStrawberry).Select(factory => factory.Id)
                     .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal))
            result.Append(" || id == \"").Append(Escape(id)).Append('"');
        result.AppendLine(";");
        result.Append("    internal static bool IsSelectedMiniHeartFollower(Strawberry berry) => berry.GetType() == typeof(Strawberry)");
        var selectedBerryTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CollabUtils2/SilverBerry"] = "AppleEverestSilverBerry",
            ["CollabUtils2/RainbowBerry"] = "AppleEverestCollabRainbowBerry",
            ["MaxHelpingHand/SecretBerry"] = "AppleEverestSecretBerry",
            ["LunaticHelper/StrawberryWithReturn"] = "AppleEverestBubbleReturnBerry"
        };
        foreach (string id in ordered.SelectMany(mod => mod.StaticSemanticLowering?.Factories ?? []).Select(f => f.Id)
                     .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal))
            if (selectedBerryTypes.TryGetValue(id, out string? berryType))
                result.Append(" || berry.GetType() == typeof(").Append(berryType).Append(')');
        result.AppendLine(";");
        result.AppendLine("    private static readonly Type[] TrackedEntities =")
            .AppendLine("    {");
        foreach (string type in entities)
            result.Append("        typeof(global::").Append(type).AppendLine("),");
        result.AppendLine("    };")
            .AppendLine()
            .AppendLine("    // Canonical Celeste 1.4.0.0 marks these concrete/abstract bases")
            .AppendLine("    // [Tracked(true)]. External AOT-rooted subclasses must therefore")
            .AppendLine("    // also enter each assignable base bucket, exactly as Monocle's")
            .AppendLine("    // ordinary assembly scan does for game-owned subclasses.")
            .AppendLine("    private static readonly Type[] InheritedTrackedEntityTypes =")
            .AppendLine("    {")
            .AppendLine("        typeof(global::Celeste.Actor),")
            .AppendLine("        typeof(global::Celeste.Billboard),")
            .AppendLine("        typeof(global::Celeste.JumpThru),")
            .AppendLine("        typeof(global::Celeste.MenuButton),")
            .AppendLine("        typeof(global::Celeste.Platform),")
            .AppendLine("        typeof(global::Celeste.Solid),")
            .AppendLine("        typeof(global::Celeste.Trigger),")
            .AppendLine("    };")
            .AppendLine()
            .AppendLine("    private static readonly Type[] PooledEntities =")
            .AppendLine("    {");
        foreach (string type in pooled)
            result.Append("        typeof(global::").Append(type).AppendLine("),");
        result.AppendLine("    };")
            .AppendLine()
            .AppendLine("    internal static void RegisterTrackerTypes()")
            .AppendLine("    {")
            .AppendLine("        foreach (Type type in TrackedEntities)")
            .AppendLine("        {")
            .AppendLine("            if (Tracker.TrackedEntityTypes.ContainsKey(type))")
            .AppendLine("                throw new InvalidOperationException($\"duplicate Apple Everest tracked entity registration: {type.FullName}\");")
            .AppendLine("            List<Type> trackedAs = new() { type };")
            .AppendLine("            foreach (Type inheritedBase in InheritedTrackedEntityTypes)")
            .AppendLine("                if (inheritedBase != type && inheritedBase.IsAssignableFrom(type))")
            .AppendLine("                    trackedAs.Add(inheritedBase);")
            .AppendLine("            Tracker.TrackedEntityTypes.Add(type, trackedAs);")
            .AppendLine("            Tracker.StoredEntityTypes.Add(type);")
            .AppendLine("        }");
        foreach (StaticSemanticTracking tracking in ordered.SelectMany(mod => mod.StaticSemanticLowering?.Tracking ?? [])
                     .OrderBy(item => item.Type, StringComparer.Ordinal))
            result.Append("        RegisterSemanticTrackerType(typeof(global::").Append(tracking.Type)
                .Append("), new Type[] { ")
                .Append(string.Join(", ", tracking.Aliases.Select(alias => "typeof(global::" + alias + ")")))
                .Append(" }, ").Append(tracking.Component ? "true" : "false").AppendLine(");");
        result.AppendLine("        RegisterSemanticTrackerType(typeof(AppleEverestFlagTrigger), Array.Empty<Type>(), false);");
        result.AppendLine("    }")
            .AppendLine("    private static void RegisterSemanticTrackerType(Type type, Type[] aliases, bool component)")
            .AppendLine("    {")
            .AppendLine("        var registrations = component ? Tracker.TrackedComponentTypes : Tracker.TrackedEntityTypes;")
            .AppendLine("        var stored = component ? Tracker.StoredComponentTypes : Tracker.StoredEntityTypes;")
            .AppendLine("        if (!registrations.TryGetValue(type, out List<Type> trackedAs))")
            .AppendLine("            registrations.Add(type, trackedAs = new List<Type>());")
            .AppendLine("        if (!trackedAs.Contains(type)) trackedAs.Add(type);")
            .AppendLine("        stored.Add(type);")
            .AppendLine("        foreach (Type alias in aliases)")
            .AppendLine("        {")
            .AppendLine("            if (!trackedAs.Contains(alias)) trackedAs.Add(alias);")
            .AppendLine("            stored.Add(alias);")
            .AppendLine("        }")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    internal static void RegisterPooledTypes(Pooler pooler)")
            .AppendLine("    {")
            .AppendLine("        foreach (Type type in PooledEntities)")
            .AppendLine("        {")
            .AppendLine("            if (pooler.Pools.ContainsKey(type))")
            .AppendLine("                throw new InvalidOperationException($\"duplicate Apple Everest pooled entity registration: {type.FullName}\");")
            .AppendLine("            pooler.Pools.Add(type, new Queue<Entity>());")
            .AppendLine("        }")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    internal static bool TryCreatePooled(Type type, out Entity entity)")
            .AppendLine("    {");
        foreach (string type in pooled)
            result.Append("        if (type == typeof(global::").Append(type).Append(")) { entity = new global::")
                .Append(type).AppendLine("(); return true; }");
        result.AppendLine("        entity = null;")
            .AppendLine("        return false;")
            .AppendLine("    }")
            .AppendLine()
            ;
        AppendGameplayFactories(result, ordered, modules);
        result.AppendLine("}");
        return result.ToString();
    }

    private sealed record GeneratedFactory(string Owner, string Kind, string Id, string Expression, bool Guard);

    private static void AppendGameplayFactories(StringBuilder result, IReadOnlyList<ResolvedMod> ordered,
        IReadOnlyList<(ResolvedMod Mod, AppleStaticDeclaration Declaration)> modules)
    {
        List<GeneratedFactory> factories = [];
        foreach ((ResolvedMod mod, AppleStaticDeclaration declaration) in modules)
        {
            foreach (AppleCustomEntityFactory factory in declaration.CustomEntityFactories)
            {
                string arguments = factory.Constructor switch
                {
                    "entity-data-vector2" => "data, offset",
                    "entity-data-vector2-entity-id" => "data, offset, entityId",
                    "entity-id-entity-data-vector2" => "entityId, data, offset",
                    _ => throw new InvalidDataException("unsupported generated factory constructor: " + factory.Constructor)
                };
                factories.Add(new(mod.Metadata.Name, factory.Kind, factory.Id,
                    "new global::" + factory.Type + "(" + arguments + ")", SelectedFactoryProfiles.Contains(factory.Kind, factory.Id)));
            }
            foreach (AppleCustomBackdropFactory factory in declaration.CustomBackdropFactories)
                factories.Add(new(mod.Metadata.Name, "backdrop", factory.Id,
                    factory.Factory == "constructor" ? "new global::" + factory.Type + "(data)" :
                        "global::" + factory.Type + "." + factory.Method + "(data)", SelectedFactoryProfiles.Contains("backdrop", factory.Id)));
        }
        foreach (ResolvedMod mod in ordered.Where(value => value.StaticSemanticLowering != null))
            foreach (StaticSemanticFactory factory in mod.StaticSemanticLowering!.Factories)
            {
                string expression = factory.ConstructorExpression ?? SelectedFactoryProfiles.ExplicitConstructor(factory.Kind, factory.Id)
                    ?? $"AppleEverestSemanticFactories.Create{(factory.Kind == "entity" ? "Entity" : "Trigger")}(id, data, offset, entityId)";
                if (factory.Kind == "backdrop" && factory.ConstructorExpression == null)
                    throw new InvalidDataException("semantic backdrop requires an explicit production constructor: " + factory.Id);
                factories.Add(new(mod.Metadata.Name, factory.Kind, factory.Id, expression,
                    factory.GuardSelectedProfile || SelectedFactoryProfiles.Contains(factory.Kind, factory.Id)));
            }
        foreach (var factory in CoreGameplayFactories)
            factories.Add(new(factory.Owner, factory.Kind, factory.Id,
                SelectedFactoryProfiles.ExplicitConstructor(factory.Kind, factory.Id)
                    ?? throw new InvalidDataException("missing explicit built-in constructor: " + factory.Id),
                SelectedFactoryProfiles.Contains(factory.Kind, factory.Id)));
        if (factories.GroupBy(factory => factory.Kind + ":" + factory.Id).Any(group => group.Count() != 1))
            throw new InvalidDataException("duplicate generated gameplay factory");
        result.AppendLine("    internal delegate Entity EntityFactory(global::Celeste.EntityData data, global::Microsoft.Xna.Framework.Vector2 offset, global::Celeste.EntityID entityId);")
            .AppendLine("    internal delegate global::Celeste.Backdrop BackdropFactory(global::Celeste.BinaryPacker.Element data);");
        foreach (string kind in new[] { "entity", "trigger", "backdrop" })
        {
            string title = char.ToUpperInvariant(kind[0]) + kind[1..];
            bool backdrop = kind == "backdrop";
            string type = backdrop ? "global::Celeste.Backdrop" : "Entity";
            string signature = backdrop ? "global::Celeste.BinaryPacker.Element data" :
                "global::Celeste.EntityData data, global::Microsoft.Xna.Framework.Vector2 offset, global::Celeste.EntityID entityId";
            string arguments = backdrop ? "data" : "data, offset, entityId";
            result.Append("    internal static ").Append(backdrop ? "BackdropFactory" : "EntityFactory").Append(" Select").Append(title)
                .AppendLine("(string id) => id switch")
                .AppendLine("    {");
            foreach (GeneratedFactory factory in factories.Where(factory => factory.Kind == kind).OrderBy(factory => factory.Id, StringComparer.Ordinal))
                result.Append("        ").Append(JsonSerializer.Serialize(factory.Id)).Append(" => ").Append(SelectedFactoryProfiles.EntryMethod(kind, factory.Id)).AppendLine(",");
            result.AppendLine("        _ => null").AppendLine("    };");
            result.Append("    internal static bool TryCreate").Append(title).Append("(string id, ").Append(signature)
                .Append(", out ").Append(type).AppendLine(" value)")
                .AppendLine("    {")
                .Append("        var factory = Select").Append(title).AppendLine("(id);")
                .Append("        value = factory == null ? null : factory(").Append(arguments).AppendLine(");")
                .AppendLine("        return factory != null;").AppendLine("    }");
            foreach (GeneratedFactory factory in factories.Where(factory => factory.Kind == kind).OrderBy(factory => factory.Id, StringComparer.Ordinal))
            {
                result.Append("    private static ").Append(type).Append(' ').Append(SelectedFactoryProfiles.EntryMethod(kind, factory.Id))
                    .Append('(').Append(signature).AppendLine(")").AppendLine("    {")
                    .Append("        const string id = ").Append(JsonSerializer.Serialize(factory.Id)).AppendLine(";");
                if (factory.Guard)
                    result.Append("        AppleEverestSelectedProfileGuard.").Append(backdrop ? "Backdrop" : "Entity").AppendLine("(id, data);");
                if (SelectedFactoryProfiles.Contains(factory.Kind, factory.Id))
                    result.Append("        AppleEverestFactoryCanary.Constructing(id, ").Append(JsonSerializer.Serialize(kind))
                        .Append(", ").Append(backdrop ? "-1" : "data.ID").AppendLine(");");
                result.Append("        ").Append(type).Append(" value = ").Append(factory.Expression).AppendLine(";");
                if (!backdrop) result.AppendLine("        value.Add(new AppleEverestStaticIdentity(id, entityId));");
                if (SelectedFactoryProfiles.Contains(factory.Kind, factory.Id))
                    result.Append("        AppleEverestFactoryCanary.Created(value, id, ").Append(JsonSerializer.Serialize(kind))
                        .Append(", ").Append(backdrop ? "-1" : "data.ID").AppendLine(");");
                result.Append("        AppleEverestStaticRuntime.RecordCustomFactoryUse(").Append(JsonSerializer.Serialize(factory.Owner)).Append(", id, ")
                    .Append(JsonSerializer.Serialize(kind)).AppendLine(");").AppendLine("        return value;").AppendLine("    }");
            }
        }
    }

    private static string RootsSource(IReadOnlyList<(ResolvedMod Mod, AppleStaticDeclaration Declaration)> modules,
        bool includeStaticFieldAccessRoots)
    {
        StringBuilder result = new("using System;\nnamespace Celeste.Mod;\n\ninternal static class GeneratedAppleEverestAotRoots\n{\n    internal static void Root()\n    {\n");
        foreach ((_, AppleStaticDeclaration declaration) in modules)
        {
            result.Append("        _ = typeof(global::").Append(declaration.ModuleType).AppendLine(");");
            foreach (string? type in new[] { declaration.SettingsType, declaration.SaveDataType, declaration.SessionType })
                if (type != null) result.Append("        _ = typeof(global::").Append(type).AppendLine(");");
            foreach (string type in declaration.TrackedEntityTypes)
                result.Append("        _ = typeof(global::").Append(type).AppendLine(");");
            foreach (string type in declaration.PooledEntityTypes)
                result.Append("        _ = typeof(global::").Append(type).AppendLine(");");
            foreach (string type in declaration.CustomBackdropFactories.Select(value => value.Type).Distinct(StringComparer.Ordinal))
                result.Append("        _ = typeof(global::").Append(type).AppendLine(");");
        }
        if (includeStaticFieldAccessRoots)
            result.AppendLine("        global::Celeste.Mod.AppleEverestStaticFieldAccess.RootReviewedReflectionMembers();");
        result.AppendLine("        _ = typeof(global::Celeste.Mod.Entities.CustomCoreMessage);");
        return result.AppendLine("    }").AppendLine("}").ToString();
    }

    private static string Factory(string? type) => type == null ? "null" : $"static () => new global::{type}()";
    private static string SettingsFactory(AppleStaticDeclaration declaration)
    {
        if (declaration.SettingsType == null) return "null";
        if (declaration.ButtonBindingProperties.Length == 0)
            return $"static () => new global::{declaration.SettingsType}()";
        string assignments = string.Join(" ", declaration.ButtonBindingProperties.Select(property =>
            $"settings.{property} ??= new global::Celeste.Mod.ButtonBinding();"));
        return $"static () => {{ global::{declaration.SettingsType} settings = new global::{declaration.SettingsType}(); {assignments} return settings; }}";
    }

    private static string InputBindingInitializer(AppleStaticDeclaration declaration)
    {
        if (declaration.SettingsType == null || declaration.ButtonBindingProperties.Length == 0) return "null";
        string assignments = string.Join(" ", declaration.ButtonBindingProperties.Select(property =>
            $"settings.{property}?.InitializeCurrentInput();"));
        return $"static value => {{ global::{declaration.SettingsType} settings = (global::{declaration.SettingsType})value; {assignments} }}";
    }

    private static string SettingDescriptor(string module, AppleStaticDeclaration declaration, AppleSettingProperty property)
    {
        if (declaration.SettingsType == null) throw new InvalidDataException($"settings descriptor without settings type: {module}");
        string accessor = $"global::Celeste.Mod.AppleEverestStaticRuntime.GetSettings<global::{declaration.SettingsType}>(\"{Escape(module)}\").{property.Name}";
        string getter;
        string setter;
        string names = "System.Array.Empty<string>()";
        string values = "System.Array.Empty<int>()";
        int minimum = property.Minimum;
        int maximum = property.Maximum;
        int step = property.Step;
        string kind;
        switch (property.Kind)
        {
            case "bool":
                kind = "Boolean";
                getter = $"static () => {accessor} ? 1 : 0";
                setter = $"static value => {accessor} = value != 0";
                minimum = 0; maximum = 1; step = 1;
                break;
            case "enum":
                kind = "Enum";
                getter = $"static () => (int){accessor}";
                setter = $"static value => {accessor} = (global::{property.Type})value";
                if (module == "DeathMarkers" && property.Name == "Mode" &&
                    declaration.SettingsType == "Celeste.Mod.DeathMarkers.DeathMarkersSettings" &&
                    property.Type == "Celeste.Mod.DeathMarkers.DeathMarkersSettings.SaveMode")
                {
                    // DeathMarkers 2.0.0 indexes its per-area SaveData dictionary while changing
                    // Mode in a live Level, before the module has necessarily recorded a death in
                    // that area. Preserve the ordinary release binary and its transfer semantics,
                    // but establish the empty per-area bucket its setter requires. This exact,
                    // pinned compatibility guard is generated only for the reviewed property.
                    setter = "static value => { " +
                        "global::Celeste.Mod.DeathMarkers.DeathMarkersSettings settings = " +
                        "global::Celeste.Mod.AppleEverestStaticRuntime.GetSettings<global::Celeste.Mod.DeathMarkers.DeathMarkersSettings>(\"DeathMarkers\"); " +
                        "global::Celeste.Mod.DeathMarkers.DeathMarkersSettings.SaveMode next = " +
                        "(global::Celeste.Mod.DeathMarkers.DeathMarkersSettings.SaveMode)value; " +
                        "if (settings.Mode != next && global::Celeste.Celeste.Instance?.scene is global::Celeste.Level level) { " +
                        "string sid = level.Session.Area.SID; " +
                        "global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.List<global::Celeste.Mod.DeathMarkers.DeathMarkersSession.Death>> deaths = " +
                        "global::Celeste.Mod.DeathMarkers.DeathMarkersModule.SaveData.Deaths; " +
                        "if (!deaths.ContainsKey(sid)) deaths.Add(sid, new global::System.Collections.Generic.List<global::Celeste.Mod.DeathMarkers.DeathMarkersSession.Death>()); " +
                        "} settings.Mode = next; }";
                }
                names = "new[] { " + string.Join(", ", property.EnumNames.Select(value => $"\"{Escape(value)}\"")) + " }";
                values = "new[] { " + string.Join(", ", property.EnumValues) + " }";
                minimum = property.EnumValues.Min(); maximum = property.EnumValues.Max(); step = 1;
                break;
            case "int":
                kind = "Integer";
                getter = $"static () => {accessor}";
                setter = $"static value => {accessor} = value";
                break;
            default: throw new InvalidDataException($"unsupported generated setting kind: {property.Kind}");
        }
        return $"new AppleEverestSettingDescriptor(\"{Escape(module)}\", \"{Escape(property.Name)}\", \"{Escape(property.Label)}\", AppleEverestSettingKind.{kind}, {getter}, {setter}, {names}, {values}, {minimum}, {maximum}, {step})";
    }
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    private static string NormalizeContentPath(string owner, string relative)
    {
        if (relative.StartsWith("Content/", StringComparison.Ordinal) || relative.StartsWith("Maps/", StringComparison.Ordinal))
            return relative;
        string safeOwner = new(owner.Select(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' or '.' ? ch : '_').ToArray());
        return $"Content/AppleEverest/Mods/{safeOwner}/{relative}";
    }
    private static void ValidateDeclaration(AppleStaticDeclaration declaration, string mod)
    {
        if (declaration.SchemaVersion != 1 || !TypeName(declaration.ModuleType))
            throw new InvalidDataException($"invalid static module declaration for {mod}");
        foreach (string? type in new[] { declaration.SettingsType, declaration.SaveDataType, declaration.SessionType })
            if (type != null && !TypeName(type)) throw new InvalidDataException($"invalid factory type for {mod}");
        if (declaration.ButtonBindingProperties.Length > 64 || declaration.ButtonBindingProperties.Any(name =>
                name.Length is <= 0 or >= 128 || name.Any(character => !char.IsLetterOrDigit(character) && character != '_')))
            throw new InvalidDataException($"invalid button-binding factory declaration for {mod}");
        if (declaration.TrackedEntityTypes.Length > 256)
            throw new InvalidDataException($"too many tracked entity types for {mod}");
        foreach (string type in declaration.TrackedEntityTypes)
            if (!TypeName(type)) throw new InvalidDataException($"invalid tracked entity type for {mod}");
        if (declaration.PooledEntityTypes.Length > 256)
            throw new InvalidDataException($"too many pooled entity types for {mod}");
        foreach (string type in declaration.PooledEntityTypes)
            if (!TypeName(type)) throw new InvalidDataException($"invalid pooled entity type for {mod}");
        if (declaration.CustomEntityFactories.Length > 512 || declaration.CustomEntityFactories.Any(factory =>
                factory.Id.Length is < 1 or > 192 || !TypeName(factory.Type) || factory.Kind is not ("entity" or "trigger") ||
                factory.Constructor is not ("entity-data-vector2" or "entity-data-vector2-entity-id" or "entity-id-entity-data-vector2")))
            throw new InvalidDataException($"invalid custom entity factory for {mod}");
        if (declaration.OmittedCustomEntityFactories.Length > 512 || declaration.OmittedCustomEntityFactories.Any(factory =>
                factory.Id.Length is < 1 or > 192 || !TypeName(factory.Type) ||
                factory.Reason != "runtime-only-constructor"))
            throw new InvalidDataException($"invalid omitted custom entity factory for {mod}");
        if (declaration.SettingsProperties.Length > 128 || declaration.SettingsProperties.Any(property =>
                property.Name.Length is < 1 or > 128 || property.Label.Length is < 1 or > 192 ||
                property.Kind is not ("bool" or "enum" or "int") ||
                property.Kind == "enum" && (property.EnumNames.Length is < 1 or > 64 ||
                    property.EnumNames.Length != property.EnumValues.Length ||
                    property.EnumValues.Distinct().Count() != property.EnumValues.Length) ||
                property.Kind == "int" && (property.Maximum < property.Minimum || property.Maximum - property.Minimum > 255 || property.Step < 1)))
            throw new InvalidDataException($"invalid settings descriptor for {mod}");
    }
    private static bool TypeName(string value) => value.Length is > 0 and < 256 && value.Split('.').All(part => part.Length > 0 && part.All(ch => char.IsLetterOrDigit(ch) || ch == '_'));
    private static void RequireMarker(string root)
    {
        if (!File.Exists(Path.Combine(root, ".apple-everest-static-closure"))) throw new InvalidDataException("not an Apple Everest static closure");
    }
}
