using System.Reflection;
using System.Text.Json;

namespace AppleEverestBuilder;

internal static class ManagedDetourCatalog
{
    private static readonly Lazy<IReadOnlyList<ManagedDetourTarget>> Loaded = new(Load);

    public static IReadOnlyList<ManagedDetourTarget> Targets => Loaded.Value;

    public static ManagedDetourTarget RequireByHookType(string hookType, string eventName)
    {
        ManagedDetourTarget[] matches = Targets.Where(target =>
            string.Equals(target.HookNamespace + "." + target.HookType, hookType, StringComparison.Ordinal) &&
            string.Equals(target.EventName, eventName, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException($"managed-detour target is not authorized: {hookType}.{eventName}");
    }

    public static ManagedDetourTarget RequireByDirectAlias(string declaringType, string methodName)
    {
        string alias = declaringType.Replace('/', '.') + "::" + methodName;
        ManagedDetourTarget[] matches = Targets.Where(target => target.DirectAliases.Contains(alias, StringComparer.Ordinal)).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException($"direct managed-hook target is not authorized: {alias}");
    }

    private static IReadOnlyList<ManagedDetourTarget> Load()
    {
        // AppleEverest.ManagedDetourTargets.json is embedded directly from
        // apple-everest/managed-detour-targets-v2.json by the project file.
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppleEverest.ManagedDetourTargets.json")
            ?? throw new InvalidDataException("embedded managed-detour target catalog is missing");
        ManagedDetourTargetCatalog catalog = JsonSerializer.Deserialize<ManagedDetourTargetCatalog>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("managed-detour target catalog is empty");
        if (catalog.SchemaVersion != 2 || catalog.Targets.Length == 0)
            throw new InvalidDataException("invalid managed-detour target catalog schema");
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> hooks = new(StringComparer.Ordinal);
        foreach (ManagedDetourTarget target in catalog.Targets)
        {
            string hook = target.HookNamespace + "." + target.HookType + "." + target.EventName;
            if (!SafeId(target.Id))
                throw new InvalidDataException($"invalid managed-detour target ID: {target.Id}");
            if (!ids.Add(target.Id))
                throw new InvalidDataException($"duplicate managed-detour target ID: {target.Id}");
            if (!hooks.Add(hook))
                throw new InvalidDataException($"duplicate managed-detour target hook: {hook}");
            if (
                string.IsNullOrWhiteSpace(target.SourceFile) || target.SourceFile.Contains("..", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(target.SourceDeclaration) || string.IsNullOrWhiteSpace(target.OriginalDeclaration) ||
                !SafeToken(target.OriginalAlias) || !SafeNamespace(target.HookNamespace) || !SafeToken(target.HookType) ||
                !SafeToken(target.EventName) || !SafeToken(target.OrigDelegate) || !SafeToken(target.HookDelegate) ||
                string.IsNullOrWhiteSpace(target.ReturnType) || (!target.IsStatic && string.IsNullOrWhiteSpace(target.ReceiverType)) ||
                target.Parameters.Any(parameter => string.IsNullOrWhiteSpace(parameter.Type) || !SafeToken(parameter.Name)))
                throw new InvalidDataException($"invalid managed-detour target catalog entry: {target.Id}");
        }
        return catalog.Targets.OrderBy(target => target.Id, StringComparer.Ordinal).ToArray();
    }

    private static bool SafeNamespace(string value) => value.Split('.').All(SafeToken);
    private static bool SafeId(string value) => value.Length is > 0 and < 160 &&
        value.All(character => char.IsLower(character) || char.IsDigit(character) || character == '-');
    private static bool SafeToken(string value) => value.Length is > 0 and < 160 &&
        (char.IsLetter(value[0]) || value[0] == '_') && value.All(character => char.IsLetterOrDigit(character) || character == '_');
}
