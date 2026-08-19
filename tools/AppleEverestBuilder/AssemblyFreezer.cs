using System.Text.Json;
using Mono.Cecil;

namespace AppleEverestBuilder;

internal static class AssemblyFreezer
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static AppleStaticDeclaration ReadSourceDeclaration(string path, string mod)
    {
        AppleStaticDeclaration declaration = JsonSerializer.Deserialize<AppleStaticDeclaration>(File.ReadAllBytes(path), Json)
            ?? throw new InvalidDataException("invalid apple-static.json");
        Validate(declaration, mod);
        return declaration;
    }

    public static AppleStaticDeclaration InspectDeclaration(string path, string mod)
    {
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadSymbols = false });
        TypeDefinition[] modules = assembly.MainModule.Types.SelectMany(AllTypes)
            .Where(type => !type.IsAbstract && Inherits(type, "Celeste.Mod.EverestModule"))
            .ToArray();
        if (modules.Length != 1)
            throw new InvalidDataException($"{mod} must contain exactly one concrete EverestModule; found {modules.Length}");
        TypeDefinition module = modules[0];
        if (!module.Methods.Any(method => method.IsConstructor && !method.IsStatic && method.Parameters.Count == 0))
            throw new InvalidDataException($"{mod} module has no parameterless constructor");
        AppleStaticDeclaration declaration = new()
        {
            SchemaVersion = 1,
            ModuleType = module.FullName.Replace('/', '.'),
            SettingsType = OverrideType(module, "get_SettingsType"),
            SaveDataType = OverrideType(module, "get_SaveDataType"),
            SessionType = OverrideType(module, "get_SessionType"),
            TrackedEntityTypes = assembly.MainModule.Types.SelectMany(AllTypes)
                .Where(type => type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "Celeste.Mod.Entities.CustomEntityAttribute"))
                .Select(type => type.FullName.Replace('/', '.')).OrderBy(value => value, StringComparer.Ordinal).ToArray()
        };
        Validate(declaration, mod);
        return declaration;
    }

    public static (string AssemblyName, string OriginalSha256, string FrozenSha256) Freeze(string source, string destination)
    {
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(source, new ReaderParameters { ReadSymbols = false });
        string assemblyName = assembly.Name.Name;
        if (string.IsNullOrWhiteSpace(assemblyName) || assemblyName.Length > 255 ||
            assemblyName.Any(character => char.IsControl(character) || character is ';' or '<' or '>' or '"' or '\''))
            throw new InvalidDataException("external assembly has an unsafe identity");
        AssemblyNameReference? hook = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference => reference.Name == "MMHOOK_Celeste");
        AssemblyNameReference celeste = assembly.MainModule.AssemblyReferences.Single(reference => reference.Name == "Celeste");
        if (hook != null)
        {
            foreach (TypeReference type in assembly.MainModule.GetTypeReferences())
                if (ReferenceEquals(type.Scope, hook)) type.Scope = celeste;
            if (!assembly.MainModule.GetTypeReferences().Any(type => ReferenceEquals(type.Scope, hook)))
                assembly.MainModule.AssemblyReferences.Remove(hook);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        assembly.Write(destination, new WriterParameters { WriteSymbols = false });
        return (assemblyName, Hashing.FileSha256(source), Hashing.FileSha256(destination));
    }

    private static string? OverrideType(TypeDefinition module, string getter)
    {
        MethodDefinition? method = module.Methods.SingleOrDefault(candidate => candidate.Name == getter && candidate.HasBody);
        if (method == null) return null;
        TypeReference? result = method.Body.Instructions.Select(instruction => instruction.Operand).OfType<TypeReference>().LastOrDefault();
        return result?.FullName.Replace('/', '.');
    }

    private static bool Inherits(TypeDefinition type, string expected)
    {
        TypeReference? current = type.BaseType;
        while (current != null)
        {
            if (current.FullName == expected) return true;
            try { current = current.Resolve()?.BaseType; }
            catch (AssemblyResolutionException) { return false; }
        }
        return false;
    }

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition root)
    {
        yield return root;
        foreach (TypeDefinition nested in root.NestedTypes.SelectMany(AllTypes)) yield return nested;
    }

    private static void Validate(AppleStaticDeclaration declaration, string mod)
    {
        if (declaration.SchemaVersion != 1 || !TypeName(declaration.ModuleType))
            throw new InvalidDataException($"invalid static module declaration for {mod}");
        foreach (string? type in new[] { declaration.SettingsType, declaration.SaveDataType, declaration.SessionType })
            if (type != null && !TypeName(type)) throw new InvalidDataException($"invalid factory type for {mod}");
        if (declaration.TrackedEntityTypes.Length > 256 || declaration.TrackedEntityTypes.Any(type => !TypeName(type)))
            throw new InvalidDataException($"invalid tracked entity declaration for {mod}");
    }

    private static bool TypeName(string value) => value.Length is > 0 and < 256 &&
        value.Split('.').All(part => part.Length > 0 && part.All(ch => char.IsLetterOrDigit(ch) || ch == '_'));
}
