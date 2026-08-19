using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

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
        TypeDefinition? settingsDefinition = OverrideTypeDefinition(module, "get_SettingsType");
        AppleStaticDeclaration declaration = new()
        {
            SchemaVersion = 1,
            ModuleType = module.FullName.Replace('/', '.'),
            SettingsType = OverrideType(module, "get_SettingsType"),
            SaveDataType = OverrideType(module, "get_SaveDataType"),
            SessionType = OverrideType(module, "get_SessionType"),
            ButtonBindingProperties = settingsDefinition?.Properties
                .Where(property => property.PropertyType.FullName == "Celeste.Mod.ButtonBinding" &&
                                   property.SetMethod is { IsPublic: true })
                .Select(property => property.Name).OrderBy(value => value, StringComparer.Ordinal).ToArray() ?? [],
            TrackedEntityTypes = assembly.MainModule.Types.SelectMany(AllTypes)
                .Where(type => type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "Celeste.Mod.Entities.CustomEntityAttribute"))
                .Select(type => type.FullName.Replace('/', '.')).OrderBy(value => value, StringComparer.Ordinal).ToArray()
        };
        Validate(declaration, mod);
        return declaration;
    }

    public static (string AssemblyName, string OriginalSha256, string FrozenSha256) Freeze(
        string source,
        string destination,
        IReadOnlyList<DirectManagedHookPlan> directHooks)
    {
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(source, new ReaderParameters { ReadSymbols = false });
        string assemblyName = assembly.Name.Name;
        if (string.IsNullOrWhiteSpace(assemblyName) || assemblyName.Length > 255 ||
            assemblyName.Any(character => char.IsControl(character) || character is ';' or '<' or '>' or '"' or '\''))
            throw new InvalidDataException("external assembly has an unsafe identity");
        AssemblyNameReference? hook = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference => reference.Name == "MMHOOK_Celeste");
        AssemblyNameReference? runtimeDetour = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference => reference.Name == "MonoMod.RuntimeDetour");
        AssemblyNameReference celeste = assembly.MainModule.AssemblyReferences.Single(reference => reference.Name == "Celeste");
        foreach (AssemblyNameReference reference in new[] { hook, runtimeDetour }.Where(reference => reference != null).Cast<AssemblyNameReference>())
        {
            foreach (TypeReference type in assembly.MainModule.GetTypeReferences())
                if (ReferenceEquals(type.Scope, reference)) type.Scope = celeste;
            if (!assembly.MainModule.GetTypeReferences().Any(type => ReferenceEquals(type.Scope, reference)))
                assembly.MainModule.AssemblyReferences.Remove(reference);
        }

        // Older FNA-targeting mods were compiled against FNA's historical
        // strong-named Microsoft.Xna.Framework facade. Desktop Everest can
        // redirect that identity at runtime; static AOT cannot. Normalize the
        // reviewed facade identity to the exact FNA assembly shipped by the
        // canonical Celeste 1.4.0.0 Apple runtime before device compilation.
        AssemblyNameReference? xnaFacade = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference =>
            reference.Name == "Microsoft.Xna.Framework" && reference.Version == new Version(4, 0, 0, 0));
        if (xnaFacade != null)
        {
            AssemblyNameReference fna = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference => reference.Name == "FNA")
                ?? new AssemblyNameReference("FNA", new Version(21, 3, 5, 0));
            if (!assembly.MainModule.AssemblyReferences.Contains(fna))
                assembly.MainModule.AssemblyReferences.Add(fna);
            foreach (TypeReference type in assembly.MainModule.GetTypeReferences())
                if (ReferenceEquals(type.Scope, xnaFacade)) type.Scope = fna;
            if (!assembly.MainModule.GetTypeReferences().Any(type => ReferenceEquals(type.Scope, xnaFacade)))
                assembly.MainModule.AssemblyReferences.Remove(xnaFacade);
        }
        foreach (DirectManagedHookPlan plan in directHooks)
        {
            TypeDefinition type = assembly.MainModule.Types.SelectMany(AllTypes)
                .Single(candidate => candidate.FullName.Replace('/', '.') == plan.DetourType);
            MethodDefinition method = type.Methods.Single(candidate => candidate.Name == plan.DetourMethod);
            type.IsPublic = true;
            type.IsNotPublic = false;
            method.IsPublic = true;
            method.IsPrivate = false;

            MethodDefinition containing = assembly.MainModule.Types.SelectMany(AllTypes).SelectMany(candidate => candidate.Methods)
                .Single(candidate => candidate.FullName == plan.ContainingMethod);
            Instruction[] instructions = containing.Body.Instructions.ToArray();
            int constructorIndex = Array.FindIndex(instructions, instruction => instruction.Offset == plan.ConstructorOffset);
            if (constructorIndex < 9 || instructions[constructorIndex].OpCode != OpCodes.Newobj ||
                instructions[constructorIndex].Operand is not MethodReference originalConstructor ||
                originalConstructor.DeclaringType.FullName != "MonoMod.RuntimeDetour.Hook")
                throw new InvalidDataException($"direct managed Hook rewrite drifted: {plan.PlanId}");
            instructions[constructorIndex - 9].OpCode = OpCodes.Ldstr;
            instructions[constructorIndex - 9].Operand = plan.PlanId;
            for (int index = constructorIndex - 8; index < constructorIndex; index++)
            {
                instructions[index].OpCode = OpCodes.Nop;
                instructions[index].Operand = null;
            }
            TypeReference staticHook = new("MonoMod.RuntimeDetour", "Hook", assembly.MainModule, celeste);
            MethodReference staticConstructor = new(".ctor", assembly.MainModule.TypeSystem.Void, staticHook) { HasThis = true };
            staticConstructor.Parameters.Add(new ParameterDefinition(assembly.MainModule.TypeSystem.String));
            instructions[constructorIndex].Operand = staticConstructor;
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

    private static TypeDefinition? OverrideTypeDefinition(TypeDefinition module, string getter)
    {
        MethodDefinition? method = module.Methods.SingleOrDefault(candidate => candidate.Name == getter && candidate.HasBody);
        TypeReference? result = method?.Body.Instructions.Select(instruction => instruction.Operand)
            .OfType<TypeReference>().LastOrDefault();
        try { return result?.Resolve(); }
        catch (AssemblyResolutionException) { return null; }
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
        if (declaration.ButtonBindingProperties.Length > 64 || declaration.ButtonBindingProperties.Any(name => !MemberName(name)))
            throw new InvalidDataException($"invalid button-binding factory declaration for {mod}");
        if (declaration.TrackedEntityTypes.Length > 256 || declaration.TrackedEntityTypes.Any(type => !TypeName(type)))
            throw new InvalidDataException($"invalid tracked entity declaration for {mod}");
    }

    private static bool TypeName(string value) => value.Length is > 0 and < 256 &&
        value.Split('.').All(part => part.Length > 0 && part.All(ch => char.IsLetterOrDigit(ch) || ch == '_'));

    private static bool MemberName(string value) => value.Length is > 0 and < 128 &&
        value.All(character => char.IsLetterOrDigit(character) || character == '_');
}
