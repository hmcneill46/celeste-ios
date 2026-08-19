using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AppleEverestBuilder;

internal static class AppleApiSurface
{
    private const string ResourceName = "AppleEverest.AppleApiSurface.json";
    private static readonly Lazy<(string Text, IReadOnlyList<ApiSurfaceMember> Members)> Contract =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static string ContractSha256 => Hashing.BytesSha256(
        Encoding.UTF8.GetBytes(Contract.Value.Text));

    internal static IReadOnlyList<ApiSurfaceMember> Members => Contract.Value.Members;

    internal static void Apply(string managedRoot)
    {
        foreach (IGrouping<string, ApiSurfaceMember> group in Members.GroupBy(member => member.SourceFile,
                     StringComparer.Ordinal))
        {
            string path = Path.GetFullPath(Path.Combine(managedRoot,
                group.Key.Replace('/', Path.DirectorySeparatorChar)));
            string expectedRoot = Path.GetFullPath(managedRoot) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(expectedRoot, StringComparison.Ordinal) || !File.Exists(path))
                throw new InvalidDataException($"Apple API surface source is absent: {group.Key}");

            string text = File.ReadAllText(path);
            foreach (ApiSurfaceMember member in group)
            {
                int first = text.IndexOf(member.OriginalDeclaration, StringComparison.Ordinal);
                if (first < 0 || text.IndexOf(member.OriginalDeclaration,
                        first + member.OriginalDeclaration.Length, StringComparison.Ordinal) >= 0)
                    throw new InvalidDataException(
                        $"Apple API surface target must occur exactly once: {member.Id}");
                text = text[..first] + member.ReplacementDeclaration +
                       text[(first + member.OriginalDeclaration.Length)..];
            }
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }

    private static (string Text, IReadOnlyList<ApiSurfaceMember> Members) Load()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException("embedded Apple API surface contract is missing");
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096, leaveOpen: false);
        string text = reader.ReadToEnd();
        ApiSurfaceContract contract = JsonSerializer.Deserialize<ApiSurfaceContract>(text,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Apple API surface contract is empty");
        if (contract.SchemaVersion != 1 || contract.Members.Length == 0 ||
            contract.Members.Select(member => member.Id).Distinct(StringComparer.Ordinal).Count() !=
            contract.Members.Length)
            throw new InvalidDataException("invalid Apple API surface contract");
        foreach (ApiSurfaceMember member in contract.Members)
        {
            if (string.IsNullOrWhiteSpace(member.Id) || string.IsNullOrWhiteSpace(member.SourceFile) ||
                member.SourceFile.StartsWith("/", StringComparison.Ordinal) ||
                member.SourceFile.Split('/').Any(part => part is "" or "." or "..") ||
                string.IsNullOrEmpty(member.OriginalDeclaration) ||
                string.IsNullOrEmpty(member.ReplacementDeclaration) ||
                member.OriginalDeclaration == member.ReplacementDeclaration)
                throw new InvalidDataException($"invalid Apple API surface member: {member.Id}");
        }
        return (text, contract.Members.OrderBy(member => member.Id, StringComparer.Ordinal).ToArray());
    }

    private sealed class ApiSurfaceContract
    {
        public int SchemaVersion { get; init; }
        public ApiSurfaceMember[] Members { get; init; } = [];
    }
}

internal sealed class ApiSurfaceMember
{
    public string Id { get; init; } = "";
    public string SourceFile { get; init; } = "";
    public string OriginalDeclaration { get; init; } = "";
    public string ReplacementDeclaration { get; init; } = "";
}
