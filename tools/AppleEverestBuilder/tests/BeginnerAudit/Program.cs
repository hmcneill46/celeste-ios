// HOST ONLY: inspect unchanged compiled production; never instantiate entities.
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

internal static class Program
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private sealed class Context(string directory) : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            string path = Path.Combine(directory, name.Name + ".dll");
            return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
        }
    }
    private static object Element(Type type, string name, JsonElement attrs, JsonElement nodes)
    {
        object value = Activator.CreateInstance(type)!;
        type.GetField("Name", Members)!.SetValue(value, name);
        Dictionary<string, object> values = new(StringComparer.Ordinal);
        foreach (JsonProperty property in attrs.EnumerateObject()) values.Add(property.Name, property.Value.ValueKind switch
        {
            JsonValueKind.String => property.Value.GetString()!,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => property.Value.TryGetInt32(out int n) ? (object)n : property.Value.GetSingle(),
            _ => throw new InvalidDataException("unsupported authored value")
        });
        type.GetField("Attributes", Members)!.SetValue(value, values);
        FieldInfo field = type.GetField("Children", Members)!;
        IList children = (IList)Activator.CreateInstance(field.FieldType)!;
        using JsonDocument empty = JsonDocument.Parse("[]");
        foreach (JsonElement node in nodes.EnumerateArray()) children.Add(Element(type, "node", node, empty.RootElement));
        field.SetValue(value, children);
        return value;
    }
    private static void Main(string[] args)
    {
        if (args.Length != 5) throw new ArgumentException("assembly manifest baseline-profiles candidate-profiles output");
        string assemblyPath = Path.GetFullPath(args[0]);
        // Run the existing full registration/caller/constructor-body/guard inspection
        // unchanged on the accepted controls before inspecting candidate profiles.
        Assembly builder = Assembly.Load("AppleEverestBuilder");
        Type closure = builder.GetType("AppleEverestBuilder.SelectedFactoryTypeClosure", true)!;
        object graph = closure.GetMethod("LoadAndValidate", Members)!.Invoke(null, [args[1], true])!;
        object manifest = graph.GetType().GetProperty("Manifest")!.GetValue(graph)!;
        object selected = manifest.GetType().GetProperty("Factories")!.GetValue(manifest)!;
        using JsonDocument baseline = JsonDocument.Parse(File.ReadAllBytes(args[2]));
        object proof = builder.GetType("AppleEverestBuilder.CompiledFactoryInspection", true)!.GetMethod("Inspect", Members)!
            .Invoke(null, [assemblyPath, baseline.RootElement, selected, false])!;
        using JsonDocument proofJson = JsonDocument.Parse(JsonSerializer.Serialize(proof));
        var verified = proofJson.RootElement.EnumerateArray().ToDictionary(
            r => r.GetProperty("Kind").GetString()! + ":" + r.GetProperty("CustomId").GetString()!, r => r.Clone());
        Context context = new(Path.GetDirectoryName(assemblyPath)!);
        try
        {
            Assembly runtime = context.LoadFromAssemblyPath(assemblyPath);
            Type registry = runtime.GetType("Celeste.Mod.GeneratedAppleEverestGameplayRegistry", true)!;
            Type guard = runtime.GetType("Celeste.Mod.AppleEverestSelectedProfileGuard", true)!;
            Type element = runtime.GetType("Celeste.BinaryPacker+Element", true)!;
            Type levelData = runtime.GetType("Celeste.LevelData", true)!;
            MethodInfo parser = levelData.GetMethod("CreateEntityData", Members)!;
            using JsonDocument candidates = JsonDocument.Parse(File.ReadAllBytes(args[3]));
            List<object> rows = [];
            int index = 0, negativeRejected = 0;
            foreach (JsonElement row in candidates.RootElement.GetProperty("occurrences").EnumerateArray())
            {
                string kind = row.GetProperty("kind").GetString()!, id = row.GetProperty("customId").GetString()!;
                if (kind is not ("entity" or "trigger" or "backdrop")) throw new InvalidDataException("unknown kind");
                string title = char.ToUpperInvariant(kind[0]) + kind[1..];
                MethodInfo selector = registry.GetMethod("Select" + title, Members)!;
                Delegate? dispatch = selector.Invoke(null, [id]) as Delegate;
                if (selector.Invoke(null, ["__stage25km_missing_registration"]) != null)
                    throw new InvalidDataException("unknown registration accepted");
                string status;
                bool profileAccepted = false;
                if (dispatch == null) status = "REGISTRATION_MISSING";
                else if (!verified.TryGetValue(kind + ":" + id, out JsonElement verifiedRow)) status = "REGISTRATION_UNREVIEWED";
                else
                {
                    if (row.GetProperty("provider").GetString() != verifiedRow.GetProperty("Provider").GetString())
                        throw new InvalidDataException("candidate provider differs from compiled registration owner");
                    MethodInfo check = guard.GetMethod(kind == "backdrop" ? "Backdrop" : "Entity", Members)!;
                    object data = Element(element, id, row.GetProperty("attributes"), row.GetProperty("nodes"));
                    if (kind != "backdrop")
                    {
                        object level = RuntimeHelpers.GetUninitializedObject(levelData);
                        levelData.GetField("Name", Members)!.SetValue(level, row.GetProperty("room").GetString());
                        data = parser.Invoke(level, [data]) ?? throw new InvalidDataException("null production parse");
                    }
                    try { check.Invoke(null, [id, data]); profileAccepted = true; }
                    catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { }
                    status = profileAccepted ? "EXACT_PROFILE_ACCEPTED" : "AUTHORED_PROFILE_REJECTED";
                    if (profileAccepted)
                    {
                        FieldInfo field = data.GetType().GetField(kind == "backdrop" ? "Attributes" : "Values", Members)!;
                        var values = (Dictionary<string, object>?)field.GetValue(data) ?? new(StringComparer.Ordinal);
                        field.SetValue(data, values);
                        values.Add("__stage25km_unsupported_authored_profile", true);
                        bool rejected = false;
                        try { check.Invoke(null, [id, data]); }
                        catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
                        if (!rejected) throw new InvalidDataException("unsupported authored profile accepted");
                        negativeRejected++;
                    }
                }
                rows.Add(new { index = index++, map = row.GetProperty("map").GetString(), kind, customId = id,
                    profileSha256 = row.GetProperty("profileSha256").GetString(), status });
            }
            File.WriteAllText(args[4], JsonSerializer.Serialize(new
            {
                schemaVersion = 1, productionAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath))).ToLowerInvariant(),
                companionSelectors = new[] { "MaxHelpingHand/MovingFlagTouchSwitch", "MaxHelpingHand/MovingTouchSwitch", "CommunalHelper/DreamSwitchGate", "CommunalHelper/DreamFlagSwitchGate" }
                    .ToDictionary(id => id, id => registry.GetMethod("SelectEntity", Members)!.Invoke(null, [id]) != null),
                candidateInputSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[3]))).ToLowerInvariant(),
                baselineProductionInspection = proofJson.RootElement, unsupportedProfileRejections = negativeRejected,
                unknownRegistrationRejected = true, occurrences = rows,
                scope = "ACTUAL_PRODUCTION_PARSER_SELECTOR_GUARD_ONLY_NO_CONSTRUCTOR_OR_LIFECYCLE_EXECUTION"
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        finally { context.Unload(); }
    }
}
