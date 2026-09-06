using System.Text.Json;

namespace AppleEverestBuilder;

internal sealed class SelectedFactoryClosureManifest
{
    public int SchemaVersion { get; set; }
    public string Profile { get; set; } = "";
    public SelectedFactoryClosureFactory[] Factories { get; set; } = [];
    public SelectedFactoryClosureNode[] Nodes { get; set; } = [];
}

internal sealed class SelectedFactoryClosureFactory
{
    public string Kind { get; set; } = "";
    public string CustomId { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ProviderAssembly { get; set; } = "";
    public string ConcreteSourceType { get; set; } = "";
    public string[] BaseChain { get; set; } = [];
    public string Constructor { get; set; } = "";
    public string[] ConstructorParameterTypes { get; set; } = [];
    public string[] ReachableLifecycleMethods { get; set; } = [];
    public string[] RequiredModuleLoadBehavior { get; set; } = [];
    public string[] RequiredHookSites { get; set; } = [];
    public string[] RequiredReflectionSites { get; set; } = [];
    public string[] ContentRequirements { get; set; } = [];
    public string Classification { get; set; } = "";
    public string[] RootNodeIds { get; set; } = [];
}

internal sealed class SelectedFactoryClosureNode
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public bool Required { get; set; }
    public string Classification { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string[] Dependencies { get; set; } = [];
}

internal sealed record SelectedFactoryClosureResult(
    int SelectedFactories,
    int FullyClosed,
    int Blocked,
    int Unknown,
    IReadOnlyList<string> Violations,
    SelectedFactoryClosureManifest Manifest);

/// <summary>
/// Validates the complete, profile-selected observable closure attached to a
/// custom factory. The graph format is deliberately independent of any mod or
/// map name: callers provide evidence nodes and this gate follows every edge,
/// requires every closure dimension, and rejects missing or unsupported state.
/// </summary>
internal static class SelectedFactoryTypeClosure
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    internal static readonly string[] RequiredDimensions =
    [
        "PROVIDER_PACKAGE", "PROVIDER_ASSEMBLY", "CONCRETE_TYPE_OR_LOWERING", "BASE_CHAIN",
        "CONSTRUCTOR", "CONSTRUCTOR_PARAMETER_TYPES", "BASE_CONSTRUCTOR", "FIELD_PROPERTY_TYPES",
        "TYPE_INITIALIZER", "CONSTRUCTOR_CALL_GRAPH", "LIFECYCLE", "INTERACTION",
        "COROUTINE_STATE_MACHINE", "MODULE_LOAD", "HOOKS", "REFLECTION", "CONTENT"
    ];

    private static readonly HashSet<string> AcceptedClassifications = new(StringComparer.Ordinal)
    {
        "ACCEPTED_VANILLA", "ACCEPTED_STATIC_RUNTIME", "ACCEPTED_STATIC_SEMANTIC_LOWERING"
    };

    private static readonly HashSet<string> AllClassifications = new(AcceptedClassifications,
        StringComparer.Ordinal)
    {
        "PRESENT_BUT_UNREACHABLE", "UNSUPPORTED_REQUIRED", "UNKNOWN"
    };

    internal static SelectedFactoryClosureResult LoadAndValidate(string path, bool requireGreen = true)
    {
        string full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new FileNotFoundException("selected-factory closure manifest does not exist", full);
        SelectedFactoryClosureManifest manifest = JsonSerializer.Deserialize<SelectedFactoryClosureManifest>(
            File.ReadAllBytes(full), Json) ?? throw new InvalidDataException("selected-factory closure manifest is empty");
        SelectedFactoryClosureResult result = Validate(manifest);
        if (requireGreen && result.Violations.Count != 0)
            throw new InvalidDataException("selected-factory closure is incomplete: " +
                string.Join("; ", result.Violations.Take(12)));
        return result;
    }

    internal static SelectedFactoryClosureResult Validate(SelectedFactoryClosureManifest manifest)
    {
        List<string> global = [];
        if (manifest.SchemaVersion != 1) global.Add("manifest: unsupported schemaVersion");
        if (string.IsNullOrWhiteSpace(manifest.Profile)) global.Add("manifest: profile is required");
        if (manifest.Factories.Length == 0) global.Add("manifest: at least one selected factory is required");

        Dictionary<string, SelectedFactoryClosureNode> nodes = new(StringComparer.Ordinal);
        foreach (SelectedFactoryClosureNode node in manifest.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id) || !nodes.TryAdd(node.Id, node))
                global.Add($"node: missing or duplicate id '{node.Id}'");
            if (!RequiredDimensions.Contains(node.Kind, StringComparer.Ordinal))
                global.Add($"node {node.Id}: unknown closure dimension '{node.Kind}'");
            if (!AllClassifications.Contains(node.Classification))
                global.Add($"node {node.Id}: unknown classification '{node.Classification}'");
            if (string.IsNullOrWhiteSpace(node.Evidence))
                global.Add($"node {node.Id}: evidence is required");
        }

        HashSet<string> factoryKeys = new(StringComparer.Ordinal);
        List<string> violations = [.. global];
        int closed = 0;
        int blocked = 0;
        int unknown = 0;
        foreach (SelectedFactoryClosureFactory factory in manifest.Factories)
        {
            string key = factory.Kind + ":" + factory.CustomId;
            List<string> issues = [];
            if (!factoryKeys.Add(key)) issues.Add("duplicate selected factory");
            if (factory.Kind is not ("entity" or "trigger" or "backdrop")) issues.Add("invalid factory kind");
            if (string.IsNullOrWhiteSpace(factory.CustomId)) issues.Add("custom ID is required");
            if (string.IsNullOrWhiteSpace(factory.Provider)) issues.Add("provider is required");
            if (string.IsNullOrWhiteSpace(factory.ProviderAssembly)) issues.Add("provider assembly is required");
            if (string.IsNullOrWhiteSpace(factory.ConcreteSourceType)) issues.Add("concrete source type is required");
            if (factory.BaseChain.Length == 0 || factory.BaseChain.Any(string.IsNullOrWhiteSpace))
                issues.Add("complete base chain is required");
            else if (!string.Equals(factory.BaseChain[0], factory.ConcreteSourceType, StringComparison.Ordinal))
                issues.Add("base chain must begin with concrete source type");
            if (string.IsNullOrWhiteSpace(factory.Constructor)) issues.Add("selected constructor is required");
            if (factory.ConstructorParameterTypes.Any(string.IsNullOrWhiteSpace))
                issues.Add("constructor parameter types must be resolved");
            if (factory.ReachableLifecycleMethods.Length == 0 ||
                factory.ReachableLifecycleMethods.Any(string.IsNullOrWhiteSpace))
                issues.Add("reachable lifecycle methods are required");
            if (factory.RequiredModuleLoadBehavior.Length == 0 ||
                factory.RequiredModuleLoadBehavior.Any(string.IsNullOrWhiteSpace))
                issues.Add("module-load behavior is required");
            if (factory.RequiredHookSites.Length == 0 || factory.RequiredHookSites.Any(string.IsNullOrWhiteSpace))
                issues.Add("hook-site disposition is required");
            if (factory.RequiredReflectionSites.Length == 0 ||
                factory.RequiredReflectionSites.Any(string.IsNullOrWhiteSpace))
                issues.Add("reflection-site disposition is required");
            if (factory.ContentRequirements.Length == 0 || factory.ContentRequirements.Any(string.IsNullOrWhiteSpace))
                issues.Add("content requirements are required");
            if (factory.RootNodeIds.Length == 0 || factory.RootNodeIds.Any(string.IsNullOrWhiteSpace))
                issues.Add("closure root node is required");
            if (!AcceptedClassifications.Contains(factory.Classification))
                issues.Add("selected factory classification is not accepted");

            HashSet<string> reached = new(StringComparer.Ordinal);
            HashSet<string> active = new(StringComparer.Ordinal);
            bool hasUnsupported = false;
            bool hasUnknown = issues.Count != 0;
            foreach (string root in factory.RootNodeIds)
                Visit(root, nodes, reached, active, issues, ref hasUnsupported, ref hasUnknown);
            foreach (string dimension in RequiredDimensions)
            {
                SelectedFactoryClosureNode[] requiredNodes = reached
                    .Where(id => nodes.TryGetValue(id, out SelectedFactoryClosureNode? node) &&
                                 node.Kind == dimension && node.Required)
                    .Select(id => nodes[id])
                    .ToArray();
                if (requiredNodes.Length == 0)
                {
                    issues.Add($"missing accepted required closure dimension {dimension}");
                    hasUnknown = true;
                }
                else if (!requiredNodes.Any(node => AcceptedClassifications.Contains(node.Classification)) &&
                         !requiredNodes.All(node => node.Classification == "UNSUPPORTED_REQUIRED"))
                {
                    issues.Add($"missing accepted required closure dimension {dimension}");
                    hasUnknown = true;
                }
            }

            if (hasUnknown)
            {
                unknown++;
                violations.AddRange(issues.Select(issue => key + ": " + issue));
            }
            else if (hasUnsupported)
            {
                blocked++;
                violations.AddRange(issues.Select(issue => key + ": " + issue));
                if (issues.Count == 0) violations.Add(key + ": unsupported required closure node");
            }
            else closed++;
        }
        return new(manifest.Factories.Length, closed, blocked, unknown, violations, manifest);
    }

    internal static void ValidateAvailableFactories(SelectedFactoryClosureResult closure,
        IReadOnlyList<ResolvedMod> ordered)
    {
        string[] missing = UnavailableFactories(closure, ordered)
            .Select(factory => factory.Kind + ":" + factory.CustomId).ToArray();
        if (missing.Length != 0)
            throw new InvalidDataException("selected-factory closure references unavailable factories: " +
                string.Join(", ", missing));
    }

    internal static SelectedFactoryClosureFactory[] UnavailableFactories(
        SelectedFactoryClosureResult closure, IReadOnlyList<ResolvedMod> ordered)
    {
        HashSet<(string Owner, string Kind, string Id)> available =
            ClosureGenerator.CoreGameplayFactories.Select(factory =>
                (factory.Owner, factory.Kind, factory.Id)).ToHashSet();
        foreach (ResolvedMod mod in ordered)
        {
            foreach (StaticSemanticFactory factory in mod.StaticSemanticLowering?.Factories ?? [])
                available.Add((mod.Metadata.Name, factory.Kind, factory.Id));
            foreach (AppleCustomEntityFactory factory in mod.Declaration?.CustomEntityFactories ?? [])
                available.Add((mod.Metadata.Name, factory.Kind, factory.Id));
            foreach (AppleCustomBackdropFactory factory in mod.Declaration?.CustomBackdropFactories ?? [])
                available.Add((mod.Metadata.Name, "backdrop", factory.Id));
        }
        return closure.Manifest.Factories
            .Where(factory => !available.Contains((factory.Provider, factory.Kind, factory.CustomId)))
            .OrderBy(factory => factory.Kind + ":" + factory.CustomId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Visit(string id, IReadOnlyDictionary<string, SelectedFactoryClosureNode> nodes,
        HashSet<string> reached, HashSet<string> active, List<string> issues,
        ref bool hasUnsupported, ref bool hasUnknown)
    {
        if (string.IsNullOrWhiteSpace(id) || !nodes.TryGetValue(id, out SelectedFactoryClosureNode? node))
        {
            issues.Add($"unresolved closure edge '{id}'");
            hasUnknown = true;
            return;
        }
        if (!active.Add(id))
        {
            issues.Add($"cyclic closure edge '{id}'");
            hasUnknown = true;
            return;
        }
        if (!reached.Add(id)) { active.Remove(id); return; }
        if (node.Classification == "UNKNOWN")
        {
            issues.Add($"{node.Kind} node '{id}' is UNKNOWN");
            hasUnknown = true;
        }
        else if (node.Classification == "UNSUPPORTED_REQUIRED")
        {
            issues.Add($"{node.Kind} node '{id}' is UNSUPPORTED_REQUIRED");
            hasUnsupported = true;
        }
        else if (node.Classification == "PRESENT_BUT_UNREACHABLE" && node.Required)
        {
            issues.Add($"{node.Kind} node '{id}' is marked unreachable but required");
            hasUnknown = true;
        }
        else if (!AcceptedClassifications.Contains(node.Classification) &&
                 node.Classification != "PRESENT_BUT_UNREACHABLE")
        {
            issues.Add($"{node.Kind} node '{id}' has invalid classification '{node.Classification}'");
            hasUnknown = true;
        }
        foreach (string dependency in node.Dependencies)
            Visit(dependency, nodes, reached, active, issues, ref hasUnsupported, ref hasUnknown);
        active.Remove(id);
    }
}
