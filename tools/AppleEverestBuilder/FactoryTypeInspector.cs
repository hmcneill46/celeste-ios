using System.Text.Json;
using Mono.Cecil;

namespace AppleEverestBuilder;

internal sealed class FactoryTypeInspectionRequest
{
    public string Id { get; set; } = "";
    public string Assembly { get; set; } = "";
    public string Type { get; set; } = "";
}

internal static class FactoryTypeInspector
{
    internal static void Write(string requestPath, string assemblyRoot, string outputPath)
    {
        JsonSerializerOptions readOptions = new() { PropertyNameCaseInsensitive = true };
        FactoryTypeInspectionRequest[] requests = JsonSerializer.Deserialize<FactoryTypeInspectionRequest[]>(
            File.ReadAllBytes(requestPath), readOptions) ?? throw new InvalidDataException("factory inspection request is empty");
        string root = Path.GetFullPath(assemblyRoot);
        using DefaultAssemblyResolver resolver = new();
        resolver.AddSearchDirectory(root);
        List<object> rows = [];
        foreach (FactoryTypeInspectionRequest request in requests.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            string assemblyPath = Path.Combine(root, request.Assembly);
            if (!File.Exists(assemblyPath)) throw new FileNotFoundException("factory assembly is missing", assemblyPath);
            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyPath,
                new ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver });
            TypeDefinition type = assembly.MainModule.Types.SelectMany(AllTypes).Single(candidate =>
                candidate.FullName.Replace('/', '.') == request.Type);
            List<string> baseChain = [type.FullName.Replace('/', '.')];
            TypeReference? current = type.BaseType;
            while (current != null)
            {
                string name = current.FullName.Replace('/', '.');
                if (baseChain.Contains(name, StringComparer.Ordinal))
                    throw new InvalidDataException("cyclic base chain for " + request.Id);
                baseChain.Add(name);
                try
                {
                    TypeDefinition resolved = current.Resolve() ?? throw new InvalidDataException(
                        $"unresolved base type '{name}' for {request.Id}");
                    current = resolved.BaseType;
                }
                catch (AssemblyResolutionException error)
                {
                    throw new InvalidDataException($"unresolved base type '{name}' for {request.Id}", error);
                }
            }
            string[] constructors = type.Methods.Where(method => method.IsConstructor && !method.IsStatic)
                .Select(method => method.FullName.Replace('/', '.')).Order(StringComparer.Ordinal).ToArray();
            string[] constructorParameterTypes = type.Methods.Where(method => method.IsConstructor && !method.IsStatic)
                .SelectMany(method => method.Parameters).Select(parameter => parameter.ParameterType.FullName.Replace('/', '.'))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            string[] lifecycle = type.Methods.Where(method => method.Name is
                    "Added" or "Awake" or "Update" or "Render" or "Removed" or "SceneEnd")
                .Select(method => method.FullName.Replace('/', '.')).Order(StringComparer.Ordinal).ToArray();
            string[] interfaces = type.Interfaces.Select(value => value.InterfaceType.FullName.Replace('/', '.'))
                .Order(StringComparer.Ordinal).ToArray();
            rows.Add(new
            {
                request.Id,
                assembly = Path.GetFileName(assemblyPath),
                assemblySha256 = Hashing.FileSha256(assemblyPath),
                type = type.FullName.Replace('/', '.'),
                baseChain,
                constructors,
                constructorParameterTypes,
                interfaces,
                lifecycle,
                typeInitializer = type.Methods.Where(method => method.IsConstructor && method.IsStatic)
                    .Select(method => method.FullName.Replace('/', '.')).SingleOrDefault(),
                fields = type.Fields.Select(field => new
                {
                    field.Name, type = field.FieldType.FullName.Replace('/', '.'), field.IsStatic
                }).OrderBy(field => field.Name, StringComparer.Ordinal).ToArray()
            });
        }
        string output = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { schemaVersion = 1, factories = rows },
            new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition type)
    {
        yield return type;
        foreach (TypeDefinition nested in type.NestedTypes.SelectMany(AllTypes)) yield return nested;
    }
}
