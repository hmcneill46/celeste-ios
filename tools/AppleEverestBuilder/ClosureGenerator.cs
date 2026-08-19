using System.Text;
using System.Text.Json;
using System.Security;

namespace AppleEverestBuilder;

internal static class ClosureGenerator
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

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
                foreach (string relative in mod.ManagedFiles.Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                {
                    string source = Path.Combine(mod.Input.StagingRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                    string destination = Path.Combine(managed, $"Mod{sourceIndex++:D3}_{Path.GetFileName(relative)}");
                    File.Copy(source, destination, overwrite: false);
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

        File.WriteAllText(Path.Combine(managed, "GeneratedAppleEverestModuleRegistry.cs"), RegistrySource(profile, codeModules), new UTF8Encoding(false));
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
        PatchStartup(Path.Combine(managedRoot, "Celeste", "Celeste.cs"));
        PatchContentReady(Path.Combine(managedRoot, "Celeste", "GameLoader.cs"));
        PatchMenu(Path.Combine(managedRoot, "Celeste", "MenuOptions.cs"));
        PatchNonPersistentSaveQuit(Path.Combine(managedRoot, "Celeste", "Level.cs"));
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

    private static void PatchStartup(string path) => ReplaceOnce(path,
        "\t\t\tceleste = new Celeste();",
        "\t\t\tglobal::Celeste.Mod.AppleEverestStaticRuntime.Startup();\n\t\t\tceleste = new Celeste();");

    private static void PatchContentReady(string path) => ReplaceOnce(path,
        "\t\tAreaData.Load();",
        "\t\tAreaData.Load();\n\t\tglobal::Celeste.Mod.AppleEverestStaticRuntime.ContentReady();");

    private static void PatchMenu(string path) => ReplaceOnce(path,
        "\t\tmenu.Add(new TextMenu.SubHeader(Dialog.Clean(\"options_gameplay\")));",
        "\t\tmenu.Add(new TextMenu.SubHeader(Dialog.Clean(\"options_gameplay\")));\n\t\tglobal::Celeste.Mod.AppleEverestLab.AddOptions(menu);");

    private static void PatchNonPersistentSaveQuit(string path) => ReplaceOnce(path,
        "\t\t\tif (SaveQuitDisabled || (player != null && player.StateMachine.State == 18))",
        "\t\t\tif (SaveQuitDisabled || global::Celeste.Mod.AppleEverestStaticRuntime.NonPersistentModSession || (player != null && player.StateMachine.State == 18))");

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

    private static string RegistrySource(AppleEverestProfile profile, IReadOnlyList<(ResolvedMod Mod, AppleStaticDeclaration Declaration)> modules)
    {
        StringBuilder result = new();
        result.AppendLine("using System;").AppendLine("namespace Celeste.Mod;").AppendLine()
            .AppendLine("internal static class GeneratedAppleEverestModuleRegistry")
            .AppendLine("{")
            .AppendLine($"    internal const string Profile = \"{Escape(profile.Profile)}\";")
            .AppendLine("    internal static readonly AppleEverestModuleDescriptor[] Modules =")
            .AppendLine("    {");
        foreach ((ResolvedMod mod, AppleStaticDeclaration declaration) in modules)
        {
            string dependencies = string.Join(", ", mod.Metadata.Dependencies.Select(dep => $"\"{Escape(dep.Name)}\""));
            result.Append("        new AppleEverestModuleDescriptor(\"").Append(Escape(mod.Metadata.Name)).Append("\", \"")
                .Append(Escape(mod.Metadata.Version)).Append("\", new[] { ").Append(dependencies).Append(" }, static () => new ")
                .Append("global::").Append(declaration.ModuleType).Append("(), ")
                .Append(SettingsFactory(declaration)).Append(", ")
                .Append(Factory(declaration.SaveDataType)).Append(", ")
                .Append(Factory(declaration.SessionType)).AppendLine("),");
        }
        return result.AppendLine("    };").AppendLine("}").ToString();
    }

    private static string ContentManifestSource(IReadOnlyList<ResolvedMod> mods, IReadOnlyList<ContentMountRecord> staged)
    {
        StringBuilder result = new("namespace Celeste.Mod;\n\ninternal static class GeneratedAppleEverestContentManifest\n{\n    internal static readonly string[] Entries =\n    {\n");
        foreach (string item in staged.Select(value => value.LogicalPath).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
            result.Append("        \"").Append(Escape(item)).AppendLine("\",");
        result.AppendLine("    };")
            .Append("    internal const string FirstMapPath = ")
            .Append(staged.Select(value => value.LogicalPath).FirstOrDefault(value => value.StartsWith("Maps/", StringComparison.Ordinal) && value.EndsWith(".bin", StringComparison.Ordinal)) is string map
                ? $"\"{Escape(map["Maps/".Length..^4])}\"" : "null")
            .AppendLine(";")
            .AppendLine("    internal static bool Has(string path) => System.Array.IndexOf(Entries, path) >= 0;")
            .Append("    internal const string ResolvedOrder = \"").Append(Escape(string.Join(",", mods.Select(mod => mod.Metadata.Name)))).AppendLine("\";")
            .AppendLine("}");
        return result.ToString();
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
            .AppendLine("    internal static void RegisterTrackerTypes()")
            .AppendLine("    {")
            .AppendLine("        foreach (Type type in TrackedEntities)")
            .AppendLine("        {")
            .AppendLine("            if (Tracker.TrackedEntityTypes.ContainsKey(type))")
            .AppendLine("                throw new InvalidOperationException($\"duplicate Apple Everest tracked entity registration: {type.FullName}\");")
            .AppendLine("            Tracker.TrackedEntityTypes.Add(type, new List<Type> { type });")
            .AppendLine("            Tracker.StoredEntityTypes.Add(type);")
            .AppendLine("        }")
            .AppendLine("    }")
            .AppendLine("}");
        return result.ToString();
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
        }
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
    }
    private static bool TypeName(string value) => value.Length is > 0 and < 256 && value.Split('.').All(part => part.Length > 0 && part.All(ch => char.IsLetterOrDigit(ch) || ch == '_'));
    private static void RequireMarker(string root)
    {
        if (!File.Exists(Path.Combine(root, ".apple-everest-static-closure"))) throw new InvalidDataException("not an Apple Everest static closure");
    }
}
