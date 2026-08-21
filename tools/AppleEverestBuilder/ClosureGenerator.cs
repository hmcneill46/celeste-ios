using System.Text;
using System.Text.Json;
using System.Security;

namespace AppleEverestBuilder;

internal static class ClosureGenerator
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly (string Kind, string Id, string Owner)[] CoreGameplayFactories =
    {
        ("entity", "everest/coreMessage", "EverestCore")
    };

    public static void Generate(
        AppleEverestProfile profile,
        IReadOnlyList<ResolvedMod> ordered,
        string repositoryRoot,
        string outputRoot)
    {
        string managed = Path.Combine(outputRoot, "managed");
        string content = Path.Combine(outputRoot, "content", "Content");
        string assemblies = Path.Combine(outputRoot, "assemblies");
        Directory.CreateDirectory(managed);
        Directory.CreateDirectory(content);

        string runtimeRoot = Path.Combine(repositoryRoot, "apple-everest", "runtime");
        foreach (string source in Directory.EnumerateFiles(runtimeRoot, "*.cs").OrderBy(Path.GetFileName, StringComparer.Ordinal))
            File.Copy(source, Path.Combine(managed, Path.GetFileName(source)), overwrite: false);

        List<(ResolvedMod Mod, AppleStaticDeclaration Declaration)> codeModules = [];
        List<ContentMountRecord> stagedContent = [];
        List<FrozenAssemblyRecord> frozenAssemblies = [];
        int sourceIndex = 0;
        for (int modOrder = 0; modOrder < ordered.Count; modOrder++)
        {
            ResolvedMod mod = ordered[modOrder];
            if (!string.IsNullOrWhiteSpace(mod.Metadata.DLL))
            {
                AppleStaticDeclaration declaration = mod.Declaration
                    ?? throw new InvalidDataException($"{mod.Metadata.Name} has no closed module declaration");
                ValidateDeclaration(declaration, mod.Metadata.Name);
                codeModules.Add((mod, declaration));
                if (mod.DeclaredAssemblyPath != null)
                {
                    string sourceAssembly = Path.Combine(mod.Input.StagingRoot,
                        mod.DeclaredAssemblyPath.Replace('/', Path.DirectorySeparatorChar));
                    string fileName = Path.GetFileName(mod.DeclaredAssemblyPath);
                    string destinationAssembly = Path.Combine(assemblies, fileName);
                    if (File.Exists(destinationAssembly))
                        throw new InvalidDataException($"duplicate frozen assembly filename: {fileName}");
                    (string assemblyName, string original, string frozen) = AssemblyFreezer.Freeze(
                        sourceAssembly, destinationAssembly, mod.DirectManagedHooks);
                    frozenAssemblies.Add(new FrozenAssemblyRecord(mod.Metadata.Name, assemblyName, fileName, original, frozen));
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
            foreach (string relative in mod.ContentFiles)
            {
                string source = Path.Combine(mod.Input.StagingRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                string stagedPath = NormalizeContentPath(mod.Metadata.Name, relative);
                string logical = ContentCompiler.Stage(source, stagedPath, content);
                stagedContent.Add(new ContentMountRecord(mod.Metadata.Name, modOrder, relative, logical,
                    Hashing.FileSha256(Path.Combine(content, logical.Replace('/', Path.DirectorySeparatorChar)))));
            }
        }

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
        var resolvedMapFactories = mapGameplayIds.Where(value => factoryOwners.ContainsKey(value.kind + "\0" + value.id))
            .Select(value => new { value.map, value.kind, value.id,
                owner = factoryOwners[value.kind + "\0" + value.id].Owner })
            .ToArray();

        (string durabilitySource, IReadOnlyDictionary<string, GeneratedDurabilityAdapter> durabilityAdapters) =
            DurabilityAdapterGenerator.Generate(codeModules);
        string durabilityClosureSha256 = Hashing.BytesSha256(Encoding.UTF8.GetBytes(string.Join("\n",
            codeModules.OrderBy(item => item.Mod.Metadata.Name, StringComparer.Ordinal).Select(item =>
                item.Mod.Metadata.Name + "\t" + item.Mod.Metadata.Version + "\t" + item.Mod.Input.SourceSha256 + "\t" +
                (durabilityAdapters.TryGetValue(item.Mod.Metadata.Name, out GeneratedDurabilityAdapter? adapter)
                    ? adapter.Schema : "none")))));
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestModuleDurabilityAdapters.cs"),
            durabilitySource, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestModuleRegistry.cs"),
            RegistrySource(profile, codeModules, ordered, durabilityAdapters, durabilityClosureSha256), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestGameplayRegistry.cs"), GameplayRegistrySource(codeModules), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestContentManifest.cs"), ContentManifestSource(ordered, stagedContent), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestAotRoots.cs"), RootsSource(codeModules), new UTF8Encoding(false));
        IReadOnlyList<ManagedDetourTarget> detourTargets = ManagedDetourCatalog.Targets;
        IReadOnlyList<DirectManagedHookPlan> directPlans = ordered.SelectMany(mod => mod.DirectManagedHooks).ToArray();
        IReadOnlyDictionary<string, ManagedDetourTarget> detourTargetsById = detourTargets.ToDictionary(target => target.Id, StringComparer.Ordinal);
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestManagedDetours.cs"),
            ManagedDetourGenerator.DispatcherSource(detourTargets), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestDirectHooks.cs"),
            ManagedDetourGenerator.DirectRegistrySource(directPlans, detourTargetsById), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(managed, "AppleEverestExternalAssemblyRoots.props"),
            ExternalAssemblyRootsSource(frozenAssemblies), new UTF8Encoding(false));

        IReadOnlyList<FileRecord> managedInventory = Hashing.Inventory(managed);
        IReadOnlyList<FileRecord> contentInventory = Hashing.Inventory(content);
        string registryHash = Hashing.FileSha256(Path.Combine(managed, "GeneratedAppleEverestModuleRegistry.cs"));
        string apiSurfaceHash = AppleApiSurface.ContractSha256;
        string hookTransformHash = Hashing.BytesSha256(Encoding.UTF8.GetBytes(
            TargetPatchContract + "\nAppleApiSurface:" + apiSurfaceHash));
        string managedHash = Hashing.LogicalHash(managedInventory);
        string contentHash = Hashing.LogicalHash(contentInventory);
        string sharedClosureHash = Hashing.BytesSha256(Encoding.UTF8.GetBytes(
            $"{ProductPolicy.TransformerVersion}\nmanaged:{managedHash}\ncontent:{contentHash}\napi-surface:{apiSurfaceHash}\n"));
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
            managedDetourCatalogSchema = 1,
            managedDetourTargetCount = detourTargets.Count,
            directManagedHookCount = directPlans.Count,
            customEntityFactoryCount = customFactories.Length + CoreGameplayFactories.Count(value => value.Kind == "entity"),
            customBackdropFactoryCount = backdropFactories.Length,
            moduleSettingCount = codeModules.Sum(item => item.Declaration.SettingsProperties.Length),
            moduleDurabilityAdapterCount = durabilityAdapters.Count,
            moduleDurabilityFormat = "per-slot-aggregate-ab-v1",
            moduleDurabilityClosureSha256 = durabilityClosureSha256,
            moduleDurabilityLogicalMaximumBytes = 2 * 1024 * 1024,
            moduleDurabilityTvOSLogicalMaximumBytes = 512 * 1024,
            moduleDurabilityTvOSReplicaMaximumBytes = 126976,
            moduleDurabilityTvOSTotalMaximumBytes = 6 * 126976,
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
                managedFiles = mod.ManagedFiles,
                contentFiles = mod.ContentFiles
            }).ToArray(),
            resolvedOrder = ordered.Select(mod => mod.Metadata.Name).ToArray(),
            managedFileCount = managedInventory.Count,
            managedLogicalSha256 = managedHash,
            contentFileCount = contentInventory.Count,
            contentLogicalSha256 = contentHash,
            registrySha256 = registryHash,
            hookTransformSha256 = hookTransformHash,
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

    public static void Apply(string closureRoot, string managedRoot)
    {
        RequireMarker(closureRoot);
        if (!File.Exists(Path.Combine(managedRoot, "Celeste.Modern.csproj")))
            throw new InvalidDataException("managed target is not a generated Celeste tree");
        string destination = Path.Combine(managedRoot, "Celeste", "Mod", "AppleEverestStatic");
        if (Directory.Exists(destination)) throw new InvalidDataException("managed target already contains Apple Everest output");
        ValidateOnce(Path.Combine(managedRoot, "Celeste", "Level.cs"), "\t\tCalc.PopRandom();\n\t}\n\n\tpublic void UnloadLevel()");
        ValidateOnce(Path.Combine(managedRoot, "Celeste", "Level.cs"), "\t\t\tswitch (entity3.Name)\n\t\t\t{");
        ValidateOnce(Path.Combine(managedRoot, "Celeste", "Level.cs"), "\t\t\tswitch (trigger.Name)\n\t\t\t{");
        ValidateOnce(Path.Combine(managedRoot, "Celeste", "MapData.cs"),
            "\t\tBackdrop backdrop = null;\n\t\tif (child.Name.Equals(\"parallax\", StringComparison.OrdinalIgnoreCase))");
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
        string closureAssemblies = Path.Combine(closureRoot, "assemblies");
        if (Directory.Exists(closureAssemblies))
        {
            string targetAssemblies = Path.Combine(managedRoot, "AppleEverestAssemblies");
            Directory.CreateDirectory(targetAssemblies);
            foreach (string source in Directory.EnumerateFiles(closureAssemblies, "*.dll").OrderBy(Path.GetFileName, StringComparer.Ordinal))
                File.Copy(source, Path.Combine(targetAssemblies, Path.GetFileName(source)), overwrite: false);
        }

        AppleApiSurface.Apply(managedRoot);
        ManagedDetourGenerator.RewriteTargets(managedRoot, ManagedDetourCatalog.Targets);
        PatchLevel(Path.Combine(managedRoot, "Celeste", "Level.cs"));
        PatchGameplayLoading(Path.Combine(managedRoot, "Celeste", "Level.cs"));
        PatchBackdropLoading(Path.Combine(managedRoot, "Celeste", "MapData.cs"));
        PatchStartup(Path.Combine(managedRoot, "Celeste", "Celeste.cs"));
        PatchContentReady(Path.Combine(managedRoot, "Celeste", "GameLoader.cs"));
        PatchMenu(Path.Combine(managedRoot, "Celeste", "MenuOptions.cs"));
        PatchNonPersistentSaveQuit(Path.Combine(managedRoot, "Celeste", "Level.cs"));
        PatchNonPersistentSave(Path.Combine(managedRoot, "Celeste", "UserIO.cs"));
        PatchNonPersistentOverworldReturn(Path.Combine(managedRoot, "Celeste", "OverworldLoader.cs"));
        PatchPinnedEverestCompatibility(managedRoot);
        PatchModuleDurability(managedRoot);
        PatchTracker(Path.Combine(managedRoot, "Monocle", "Tracker.cs"));
        PatchProject(Path.Combine(managedRoot, "Celeste.Modern.csproj"), closureRoot);
    }

    public static string TargetPatchContract => string.Join("\n", new[]
    {
        "ManagedDetourCatalog:typed-static-dispatch:v3",
        "Level.LoadLevel:ordinary-event:v1",
        "Celeste.Run:static-registry-startup:v1",
        "GameLoader:content-ready:v1",
        "MenuOptions:diagnostic-panel:v1",
        "Tracker.Initialize:typed-gameplay-registry:v1",
        "Level.LoadLevel:typed-custom-factory-registry:v1",
        "MapData.ParseBackdrop:typed-custom-backdrop-registry:v1",
        "ModuleSettings:typed-menu-and-platform-storage:v1",
        "ModuleSaveData+Session:typed-yaml-aggregate-ab:v1",
        "UserIO.SaveRoutine:coherent-module-snapshot:v1",
        "SaveData.Start+StartSession:module-restore-boundaries:v1",
        "SaveData.TryDelete:module-slot-delete:v1",
        "OuiFileSelect:module-slot-preload:v1",
        "UserIO.SaveHandler:nonpersistent-mod-session-filter:v1",
        "OverworldLoader.Begin:nonpersistent-mod-session-restore:v1",
        "PinnedEverestABI:DeathMarkers-reviewed-members:v1",
        "HookGen+RuntimeDetour.Hook:shared-data-only-backend:v1",
        "AppleApiSurface:exact-reviewed-external-members:v1",
        "Celeste.Modern.csproj:EVEREST_APPLE_STATIC_AOT:v2",
        "ExternalAssembly:full-trimmer-root:v1"
    });

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

    private static void PatchLevel(string path) => ReplaceOnce(path,
        "\t\tCalc.PopRandom();\n\t}\n\n\tpublic void UnloadLevel()",
        "\t\tCalc.PopRandom();\n\t\tglobal::Celeste.Mod.Everest.Events.Level.RaiseOnLoadLevel(this, playerIntro, isFromLoader);\n\t}\n\n\tpublic void UnloadLevel()");

    private static void PatchGameplayLoading(string path)
    {
        ReplaceOnce(path,
            "\t\t\tswitch (entity3.Name)\n\t\t\t{",
            "\t\t\tif (global::Celeste.Mod.GeneratedAppleEverestGameplayRegistry.TryCreateEntity(entity3.Name, entity3, vector, entityID, out Entity appleEverestEntity))\n\t\t\t{\n\t\t\t\tAdd(appleEverestEntity);\n\t\t\t\tcontinue;\n\t\t\t}\n\t\t\tswitch (entity3.Name)\n\t\t\t{");
        ReplaceOnce(path,
            "\t\t\tswitch (trigger.Name)\n\t\t\t{",
            "\t\t\tif (global::Celeste.Mod.GeneratedAppleEverestGameplayRegistry.TryCreateTrigger(trigger.Name, trigger, vector, entityID3, out Entity appleEverestTrigger))\n\t\t\t{\n\t\t\t\tAdd(appleEverestTrigger);\n\t\t\t\tcontinue;\n\t\t\t}\n\t\t\tswitch (trigger.Name)\n\t\t\t{");
    }

    private static void PatchBackdropLoading(string path) => ReplaceOnce(path,
        "\t\tBackdrop backdrop = null;\n\t\tif (child.Name.Equals(\"parallax\", StringComparison.OrdinalIgnoreCase))",
        "\t\tBackdrop backdrop = null;\n\t\tif (global::Celeste.Mod.GeneratedAppleEverestGameplayRegistry.TryCreateBackdrop(child.Name, child, out backdrop))\n\t\t{\n\t\t}\n\t\telse if (child.Name.Equals(\"parallax\", StringComparison.OrdinalIgnoreCase))");

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

    private static void PatchMenu(string path) => ReplaceOnce(path,
        "\t\tmenu.Add(new TextMenu.SubHeader(Dialog.Clean(\"options_gameplay\")));",
        "\t\tmenu.Add(new TextMenu.SubHeader(Dialog.Clean(\"options_gameplay\")));\n\t\tglobal::Celeste.Mod.AppleEverestLab.AddOptions(menu);");

    private static void PatchNonPersistentSaveQuit(string path) => ReplaceOnce(path,
        "\t\t\tif (SaveQuitDisabled || (player != null && player.StateMachine.State == 18))",
        "\t\t\tif (SaveQuitDisabled || global::Celeste.Mod.AppleEverestStaticRuntime.NonPersistentModSession || (player != null && player.StateMachine.State == 18))");

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
            "\tpublic string SID\n\t{\n\t\tget\n\t\t{\n\t\t\tif (AreaData.Areas == null || ID < 0 || ID >= AreaData.Areas.Count) return null;\n\t\t\tAreaData data = AreaData.Areas[ID];\n\t\t\tstring path = data?.Mode != null && data.Mode.Length > 0 ? data.Mode[0]?.Path : null;\n\t\t\treturn string.IsNullOrEmpty(path) ? data?.Name : \"Celeste/\" + path;\n\t\t}\n\t}\n\n\tpublic int ChapterIndex\n\t{");

        // Desktop Everest publicizes Engine.scene. The accepted DLL contains a
        // direct field reference produced by that publicized contract.
        string engine = Path.Combine(managedRoot, "Monocle", "Engine.cs");
        ReplaceOnce(engine, "\tprivate Scene scene;", "\tpublic Scene scene;");
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
            "\t\t\t\tsavingFileData = Serialize(SaveData.Instance);\n\t\t\t\tglobal::Celeste.Mod.AppleEverestModulePersistence.CaptureSave(SaveData.Instance.FileSlot, savingFileData);");
        ReplaceOnce(userIo,
            "\t\tSaving = false;\n\t\tCeleste.SaveRoutine = null;",
            "\t\tSaving = false;\n\t\tCeleste.SaveRoutine = null;\n\t\tif (appleEverestSaveQueued)\n\t\t{\n\t\t\tbool nextFile = appleEverestQueuedFile;\n\t\t\tbool nextSettings = appleEverestQueuedSettings;\n\t\t\tappleEverestSaveQueued = false;\n\t\t\tappleEverestQueuedFile = false;\n\t\t\tappleEverestQueuedSettings = false;\n\t\t\tSaveHandler(nextFile, nextSettings);\n\t\t}");
        ReplaceOnce(userIo,
            "\t\t\t\tSavingResult &= Save<SaveData>(SaveData.GetFilename(), savingFileData);",
            "\t\t\t\tSavingResult &= Save<SaveData>(SaveData.GetFilename(), savingFileData);\n\t\t\t\tif (SavingResult) SavingResult &= global::Celeste.Mod.AppleEverestModulePersistence.CommitCapturedSave();\n\t\t\t\telse global::Celeste.Mod.AppleEverestModulePersistence.DiscardCapturedSave();");

        string saveData = Path.Combine(managedRoot, "Celeste", "SaveData.cs");
        ReplaceOnce(saveData,
            "\t\tInstance.FileSlot = slot;\n\t\tInstance.AfterInitialize();",
            "\t\tInstance.FileSlot = slot;\n\t\tInstance.AfterInitialize();\n\t\tglobal::Celeste.Mod.AppleEverestModulePersistence.ActivateSlot(slot, UserIO.Serialize(Instance));");
        ReplaceOnce(saveData,
            "\tpublic static bool TryDelete(int slot)\n\t{\n\t\treturn UserIO.Delete(GetFilename(slot));\n\t}",
            "\tpublic static bool TryDelete(int slot)\n\t{\n\t\tbool vanilla = UserIO.Delete(GetFilename(slot));\n\t\treturn vanilla && global::Celeste.Mod.AppleEverestModulePersistence.DeleteSlot(slot);\n\t}");
        ReplaceOnce(saveData,
            "\tpublic void StartSession(Session session)\n\t{\n\t\tLastArea = session.Area;\n\t\tCurrentSession = session;",
            "\tpublic void StartSession(Session session)\n\t{\n\t\tSession appleEverestPreviousSession = CurrentSession;\n\t\tLastArea = session.Area;\n\t\tCurrentSession = session;\n\t\tif (!object.ReferenceEquals(appleEverestPreviousSession, session))\n\t\t\tglobal::Celeste.Mod.AppleEverestModulePersistence.ResetSessionForNewVanillaSession(FileSlot);");

        string fileSelect = Path.Combine(managedRoot, "Celeste", "OuiFileSelect.cs");
        ReplaceOnce(fileSelect,
            "\t\t\t\t\t\tsaveData.AfterInitialize();\n\t\t\t\t\t\touiFileSelectSlot = new OuiFileSelectSlot(i, this, saveData);",
            "\t\t\t\t\t\tsaveData.AfterInitialize();\n\t\t\t\t\t\tglobal::Celeste.Mod.AppleEverestModulePersistence.PreloadSlot(i, UserIO.Serialize(saveData));\n\t\t\t\t\t\touiFileSelectSlot = new OuiFileSelectSlot(i, this, saveData);");

        string fileSelectSlot = Path.Combine(managedRoot, "Celeste", "OuiFileSelectSlot.cs");
        ReplaceOnce(fileSelectSlot,
            "\tpublic void CreateButtons()\n\t{\n\t\tbuttons.Clear();",
            "\tpublic void CreateButtons()\n\t{\n\t\tif (SaveData != null) global::Celeste.Mod.AppleEverestModulePersistence.ActivateSaveData(FileSlot, UserIO.Serialize(SaveData));\n\t\tbuttons.Clear();");
    }

    private static void PatchTracker(string path) => ReplaceOnce(path,
        "\t\t}\n\t}\n\n\tprivate static List<Type> GetSubclasses(Type type)",
        "\t\t}\n\t\tglobal::Celeste.Mod.GeneratedAppleEverestGameplayRegistry.RegisterTrackerTypes();\n\t}\n\n\tprivate static List<Type> GetSubclasses(Type type)");

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
            .AppendLine($"    internal const string DurabilityClosureSha256 = \"{Escape(durabilityClosureSha256)}\";")
            .AppendLine("    internal static readonly AppleEverestModuleDescriptor[] Modules =")
            .AppendLine("    {");
        foreach ((ResolvedMod mod, AppleStaticDeclaration declaration) in modules)
        {
            string dependencies = StringArray(mod.Metadata.Dependencies.Select(dep => dep.Name));
            string requiredBy = StringArray(resolved.Where(candidate => candidate.Metadata.Dependencies.Any(dependency =>
                    dependency.Name == mod.Metadata.Name)).Select(candidate => candidate.Metadata.Name));
            result.Append("        new AppleEverestModuleDescriptor(\"").Append(Escape(mod.Metadata.Name)).Append("\", \"")
                .Append(Escape(mod.Metadata.Version)).Append("\", ").Append(dependencies).Append(", ")
                .Append(requiredBy).Append(", static () => new ")
                .Append("global::").Append(declaration.ModuleType).Append("(), ")
                .Append(SettingsFactory(declaration)).Append(", ")
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

    private static string ContentManifestSource(IReadOnlyList<ResolvedMod> mods, IReadOnlyList<ContentMountRecord> staged)
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

    private static string GameplayRegistrySource(IReadOnlyList<(ResolvedMod Mod, AppleStaticDeclaration Declaration)> modules)
    {
        string[] entities = modules
            .SelectMany(item => item.Declaration.TrackedEntityTypes)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        StringBuilder result = new("using System;\nusing System.Collections.Generic;\nusing Monocle;\n\nnamespace Celeste.Mod;\n\ninternal static class GeneratedAppleEverestGameplayRegistry\n{\n");
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
            .AppendLine("        }")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    internal static bool TryCreateEntity(string id, global::Celeste.EntityData data, global::Microsoft.Xna.Framework.Vector2 offset, global::Celeste.EntityID entityId, out Entity entity)")
            .AppendLine("    {")
            .AppendLine("        switch (id)")
            .AppendLine("        {");
        foreach ((ResolvedMod mod, AppleStaticDeclaration declaration) in modules)
            foreach (AppleCustomEntityFactory factory in declaration.CustomEntityFactories.Where(value => value.Kind == "entity"))
                AppendFactoryCase(result, mod.Metadata.Name, factory);
        result.AppendLine("            case \"everest/coreMessage\":")
            .AppendLine("                entity = new global::Celeste.Mod.Entities.CustomCoreMessage(data, offset);")
            .AppendLine("                AppleEverestStaticRuntime.RecordCustomFactoryUse(\"EverestCore\", \"everest/coreMessage\", \"entity\");")
            .AppendLine("                return true;");
        result.AppendLine("        }")
            .AppendLine("        entity = null;")
            .AppendLine("        return false;")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    internal static bool TryCreateTrigger(string id, global::Celeste.EntityData data, global::Microsoft.Xna.Framework.Vector2 offset, global::Celeste.EntityID entityId, out Entity entity)")
            .AppendLine("    {")
            .AppendLine("        switch (id)")
            .AppendLine("        {");
        foreach ((ResolvedMod mod, AppleStaticDeclaration declaration) in modules)
            foreach (AppleCustomEntityFactory factory in declaration.CustomEntityFactories.Where(value => value.Kind == "trigger"))
                AppendFactoryCase(result, mod.Metadata.Name, factory);
        result.AppendLine("        }")
            .AppendLine("        entity = null;")
            .AppendLine("        return false;")
            .AppendLine("    }")
            .AppendLine()
            .AppendLine("    internal static bool TryCreateBackdrop(string id, global::Celeste.BinaryPacker.Element data, out global::Celeste.Backdrop backdrop)")
            .AppendLine("    {")
            .AppendLine("        switch (id)")
            .AppendLine("        {");
        foreach ((ResolvedMod mod, AppleStaticDeclaration declaration) in modules)
        {
            foreach (AppleCustomBackdropFactory factory in declaration.CustomBackdropFactories)
            {
                string expression = factory.Factory == "constructor"
                    ? $"new global::{factory.Type}(data)"
                    : $"global::{factory.Type}.{factory.Method}(data)";
                result.Append("            case \"").Append(Escape(factory.Id)).AppendLine("\":")
                    .Append("                backdrop = ").Append(expression).AppendLine(";")
                    .Append("                AppleEverestStaticRuntime.RecordCustomFactoryUse(\"").Append(Escape(mod.Metadata.Name))
                    .Append("\", \"").Append(Escape(factory.Id)).AppendLine("\", \"backdrop\");")
                    .AppendLine("                return true;");
            }
        }
        result.AppendLine("        }")
            .AppendLine("        backdrop = null;")
            .AppendLine("        return false;")
            .AppendLine("    }")
            .AppendLine("}");
        return result.ToString();
    }

    private static void AppendFactoryCase(StringBuilder result, string owner, AppleCustomEntityFactory factory)
    {
        string arguments = factory.Constructor switch
        {
            "entity-data-vector2" => "data, offset",
            "entity-data-vector2-entity-id" => "data, offset, entityId",
            "entity-id-entity-data-vector2" => "entityId, data, offset",
            _ => throw new InvalidDataException($"unsupported generated custom factory constructor: {factory.Constructor}")
        };
        result.Append("            case \"").Append(Escape(factory.Id)).AppendLine("\":")
            .Append("                entity = new global::").Append(factory.Type).Append('(').Append(arguments).AppendLine(");")
            .Append("                AppleEverestStaticRuntime.RecordCustomFactoryUse(\"").Append(Escape(owner)).Append("\", \"")
            .Append(Escape(factory.Id)).Append("\", \"").Append(factory.Kind).AppendLine("\");")
            .AppendLine("                return true;");
    }

    private static string RootsSource(IReadOnlyList<(ResolvedMod Mod, AppleStaticDeclaration Declaration)> modules)
    {
        StringBuilder result = new("using System;\nnamespace Celeste.Mod;\n\ninternal static class GeneratedAppleEverestAotRoots\n{\n    internal static void Root()\n    {\n");
        foreach ((_, AppleStaticDeclaration declaration) in modules)
        {
            result.Append("        _ = typeof(global::").Append(declaration.ModuleType).AppendLine(");");
            foreach (string? type in new[] { declaration.SettingsType, declaration.SaveDataType, declaration.SessionType })
                if (type != null) result.Append("        _ = typeof(global::").Append(type).AppendLine(");");
            foreach (string type in declaration.TrackedEntityTypes)
                result.Append("        _ = typeof(global::").Append(type).AppendLine(");");
            foreach (string type in declaration.CustomBackdropFactories.Select(value => value.Type).Distinct(StringComparer.Ordinal))
                result.Append("        _ = typeof(global::").Append(type).AppendLine(");");
        }
        result.AppendLine("        _ = typeof(global::Celeste.Mod.Entities.CustomCoreMessage);");
        return result.AppendLine("    }").AppendLine("}").ToString();
    }

    private static string Factory(string? type) => type == null ? "null" : $"static () => new global::{type}()";
    private static string SettingsFactory(AppleStaticDeclaration declaration)
    {
        if (declaration.SettingsType == null) return "null";
        string assignments = string.Join(", ", declaration.ButtonBindingProperties.Select(property =>
            property + " = new global::Celeste.Mod.ButtonBinding()"));
        return assignments.Length == 0
            ? $"static () => new global::{declaration.SettingsType}()"
            : $"static () => new global::{declaration.SettingsType} {{ {assignments} }}";
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
