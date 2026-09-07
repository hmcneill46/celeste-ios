using System.Text.Json;

namespace AppleEverestBuilder;

/// <summary>
/// Binds a reviewed graph to actual accepted package registrations without
/// generating content or a product. Availability is a necessary condition for
/// semantic closure; this report never turns an available ID into a new proof
/// of its constructor or lifecycle behavior.
/// </summary>
internal static class FactoryProfilePreflight
{
    internal static void WriteDeclarationDiagnostic(string manifestPath, IReadOnlyList<string> modPaths, string output)
    {
        SelectedFactoryClosureResult closure = SelectedFactoryTypeClosure.LoadAndValidate(manifestPath);
        if (modPaths.Count == 0)
            throw new InvalidDataException("factory preflight requires explicit package inputs");
        string staging = Path.Combine(Path.GetTempPath(), "apple-everest-preflight-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        List<ResolvedMod> accepted = [];
        List<object> providers = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        HashSet<string> rejected = new(StringComparer.Ordinal);
        try
        {
            for (int index = 0; index < modPaths.Count; index++)
            {
                ModInput input = SafeModIngestor.Ingest(modPaths[index], staging, index);
                string archiveSha = File.Exists(input.SourcePath) ? Hashing.FileSha256(input.SourcePath) : "directory-input";
                foreach (EverestYamlEntry metadata in input.Metadata)
                {
                    if (!names.Add(metadata.Name))
                        throw new InvalidDataException("duplicate factory preflight provider: " + metadata.Name);
                    string status;
                    string? reason = null;
                    try
                    {
                        accepted.Add(CompatibilityAnalyzer.Analyze(input, metadata));
                        status = "ACCEPTED_PROVIDER";
                    }
                    catch (InvalidDataException exception)
                    {
                        status = "REJECTED_PROVIDER";
                        reason = exception.Message.Replace(input.StagingRoot, "<package>", StringComparison.Ordinal);
                        rejected.Add(metadata.Name);
                    }
                    providers.Add(new { name = metadata.Name, version = metadata.Version,
                        archiveSha256 = archiveSha, sourceLogicalSha256 = input.SourceSha256, status, reason });
                }
            }
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
        SelectedFactoryClosureFactory[] missing = SelectedFactoryTypeClosure.UnavailableFactories(closure, accepted);
        HashSet<string> unavailable = missing.Select(factory => factory.Kind + ":" + factory.CustomId).ToHashSet();
        object report = new
        {
            schemaVersion = 1,
            claimedGraphSha256 = Hashing.FileSha256(manifestPath),
            claimedGraphCensus = new { selected = closure.SelectedFactories, closed = closure.FullyClosed,
                blocked = closure.Blocked, unknown = closure.Unknown },
            registrationCensus = new { selected = closure.SelectedFactories,
                available = closure.SelectedFactories - missing.Length, unavailable = missing.Length },
            semanticClosureEstablished = false,
            semanticClosureDisposition = "REGISTRATION_PREFLIGHT_ONLY_NOT_A_NEW_BEHAVIORAL_CLOSURE_PROOF",
            productGenerated = false,
            providers,
            factories = closure.Manifest.Factories.OrderBy(factory => factory.Kind + ":" + factory.CustomId,
                StringComparer.Ordinal).Select(factory => new
            {
                kind = factory.Kind, customId = factory.CustomId, provider = factory.Provider,
                status = !unavailable.Contains(factory.Kind + ":" + factory.CustomId)
                    ? "AVAILABLE_ACCEPTED_REGISTRATION"
                    : rejected.Contains(factory.Provider) ? "PROVIDER_REJECTED"
                    : names.Contains(factory.Provider) || factory.Provider == "EverestCore"
                        ? "MISSING_ACCEPTED_REGISTRATION" : "PROVIDER_NOT_SUPPLIED"
            })
        };
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        if (missing.Length != 0)
            throw new InvalidDataException($"factory profile preflight blocked: {missing.Length} of {closure.SelectedFactories} selected factories unavailable; no product generated");
    }

    internal static void Write(string manifestPath, IReadOnlyList<string> modPaths, string output,
        string repoRoot, string profilePath, string upstream, string suppliedClosure, string assemblyPath, string authoredProfilesPath,
        string canonicalManaged, string dotnet, string? contentPlanPath = null)
    {
        SelectedFactoryClosureResult graph = SelectedFactoryTypeClosure.LoadAndValidate(manifestPath);
        using JsonDocument authored = JsonDocument.Parse(File.ReadAllBytes(authoredProfilesPath));
        using Stream expectedStream = typeof(SelectedFactoryProfiles).Assembly.GetManifestResourceStream("AppleEverest.SelectedFactoryProfiles")!;
        using MemoryStream expectedBytes = new();
        expectedStream.CopyTo(expectedBytes);
        if (Hashing.FileSha256(authoredProfilesPath) != Hashing.BytesSha256(expectedBytes.ToArray()))
            throw new InvalidDataException("authored profile authority differs from reviewed package extraction");
        using JsonDocument dependencyGraph = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(repoRoot,
            "apple-everest", "strawberry-jam-dependency-graph-stage25kc.json")));
        Dictionary<string, JsonElement> pins = dependencyGraph.RootElement.GetProperty("nodes").EnumerateArray()
            .ToDictionary(node => node.GetProperty("name").GetString()!, StringComparer.Ordinal);
        HashSet<string> providers = graph.Manifest.Factories.Where(factory => factory.Provider != "EverestCore")
            .Select(factory => factory.Provider).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> expectedOwners = authored.RootElement.GetProperty("factories").EnumerateArray()
            .ToDictionary(factory => factory.GetProperty("kind").GetString() + ":" + factory.GetProperty("customId").GetString(),
                factory => factory.GetProperty("provider").GetString()!, StringComparer.Ordinal);
        if (graph.Manifest.Factories.Length != expectedOwners.Count || graph.Manifest.Factories.Any(factory =>
            expectedOwners.GetValueOrDefault(factory.Kind + ":" + factory.CustomId) != factory.Provider))
            throw new InvalidDataException("selected factory ownership/census differs from package-backed authority");
        string temporary = Path.Combine(Path.GetTempPath(), "apple-everest-production-preflight-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            List<object> identities = [];
            HashSet<string> supplied = new(StringComparer.Ordinal);
            for (int index = 0; index < modPaths.Count; index++)
            {
                ModInput input = SafeModIngestor.Ingest(modPaths[index], temporary, index);
                foreach (EverestYamlEntry metadata in input.Metadata)
                {
                    if (!supplied.Add(metadata.Name)) throw new InvalidDataException("duplicate preflight provider: " + metadata.Name);
                    if (!providers.Contains(metadata.Name)) continue;
                    JsonElement pin = pins.GetValueOrDefault(metadata.Name);
                    FactoryPackageIdentity.Dll[] dlls = FactoryPackageIdentity.Verify(pin, input, metadata);
                    // Exercise the production analyzer now; the regeneration
                    // below additionally resolves dependencies and transforms.
                    ResolvedMod analyzed = CompatibilityAnalyzer.Analyze(input, metadata);
                    identities.Add(new { name = metadata.Name, version = metadata.Version,
                        archiveSha256 = Hashing.FileSha256(input.SourcePath), sourceLogicalSha256 = input.SourceSha256,
                        dlls, dependencies = metadata.Dependencies, optionalDependencies = metadata.OptionalDependencies,
                        selectedImplementation = analyzed.StaticSemanticLowering?.Id, wholePackageCompatible = false });
                }
            }
            if (!providers.IsSubsetOf(supplied)) throw new InvalidDataException("exact selected providers missing: " + string.Join(",", providers.Except(supplied).Order()));
            string regenerated = Path.Combine(temporary, "regenerated");
            Program.Build(profilePath, repoRoot, upstream, regenerated, modPaths, factoryClosurePath: null, contentPlanPath);
            using JsonDocument production = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(regenerated, "compatibility-manifest.json")));
            using JsonDocument previous = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(suppliedClosure, "compatibility-manifest.json")));
            foreach (string field in new[] { "sharedClosureSha256", "managedLogicalSha256", "contentLogicalSha256", "registrySha256",
                "frozenAssemblyLogicalSha256", "frozenIlPlanSha256", "appleApiSurfaceSha256", "hookTransformSha256" })
                if (production.RootElement.GetProperty(field).GetString() != previous.RootElement.GetProperty(field).GetString())
                    throw new InvalidDataException("supplied closure differs from actual production transformation: " + field);
            foreach (string tree in new[] { "managed", "content", "assemblies" })
                if (Hashing.LogicalHash(Hashing.Inventory(Path.Combine(regenerated, tree))) !=
                    Hashing.LogicalHash(Hashing.Inventory(Path.Combine(suppliedClosure, tree))))
                    throw new InvalidDataException("supplied closure tree differs from production: " + tree);
            FactoryCompilationProof.Evidence compilation = FactoryCompilationProof.CompileAndCompare(repoRoot,
                regenerated, canonicalManaged, assemblyPath, dotnet, temporary);
            CompiledFactoryInspection.Factory[] factories = CompiledFactoryInspection.Inspect(assemblyPath, authored.RootElement, graph.Manifest.Factories);
            int occurrences = factories.Sum(factory => factory.AcceptedOccurrences);
            object report = new
            {
                schemaVersion = 3, authority = "EXACT_PUBLIC_PACKAGES_PRODUCTION_REGENERATION_FRESH_COMPILATION_EXACT_DLL_BYTES_COMPILED_SELECTORS_ACTUAL_PROFILE_GUARDS",
                contentIdCensus = new { selectedOccurrences = occurrences, acceptedOrVanilla = occurrences, blocked = 0, unclassified = 0 },
                registrationCensus = new { selected = factories.Length, available = factories.Length, unavailable = 0, providerRejected = 0 },
                semanticClosureEstablished = false,
                semanticClosureDisposition = "SEPARATE_SOURCE_LIFECYCLE_CANARY_AND_PHYSICAL_EVIDENCE_REQUIRED",
                exactPackageIdentityVerified = true, actualProductionRegenerated = true,
                sharedClosureSha256 = production.RootElement.GetProperty("sharedClosureSha256").GetString(),
                actualFactoryRegistrySha256 = Hashing.FileSha256(Path.Combine(regenerated, "managed", "GeneratedAppleEverestGameplayRegistry.cs")),
                compiledAssemblySha256 = Hashing.FileSha256(assemblyPath), compilation,
                authoredProfilesSha256 = Hashing.FileSha256(authoredProfilesPath),
                productGenerated = false, providers = identities, factories
            };
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

}
