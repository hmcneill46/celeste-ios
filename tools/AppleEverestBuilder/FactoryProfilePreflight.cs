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
    internal static void Write(string manifestPath, IReadOnlyList<string> modPaths, string output)
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
}
