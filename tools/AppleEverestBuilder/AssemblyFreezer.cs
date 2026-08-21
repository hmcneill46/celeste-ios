using System.Text.Json;
using System.Text;
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
        TypeDefinition? saveDataDefinition = OverrideTypeDefinition(module, "get_SaveDataType");
        TypeDefinition? sessionDefinition = OverrideTypeDefinition(module, "get_SessionType");
        AppleModuleDurabilityCompatibility durability = InspectDurability(module,
            saveDataDefinition, sessionDefinition, mod);
        TypeDefinition[] customTypes = assembly.MainModule.Types.SelectMany(AllTypes)
            .Where(type => type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "Celeste.Mod.Entities.CustomEntityAttribute"))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        TypeDefinition[] backdropTypes = assembly.MainModule.Types.SelectMany(AllTypes)
            .Where(type => type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "Celeste.Mod.Backdrops.CustomBackdropAttribute"))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        (AppleSettingProperty[] settings, string[] omittedSettings) = InspectSettings(settingsDefinition);
        (AppleCustomEntityFactory[] customEntityFactories,
            AppleOmittedCustomEntityFactory[] omittedCustomEntityFactories) = InspectCustomEntities(customTypes);
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
            TrackedEntityTypes = customTypes.Select(type => type.FullName.Replace('/', '.')).ToArray(),
            CustomEntityFactories = customEntityFactories,
            OmittedCustomEntityFactories = omittedCustomEntityFactories,
            CustomBackdropFactories = backdropTypes.SelectMany(CustomBackdropFactories)
                .OrderBy(value => value.Id, StringComparer.Ordinal).ToArray(),
            SettingsProperties = settings,
            OmittedSettingsProperties = omittedSettings,
            Durability = durability
        };
        Validate(declaration, mod);
        return declaration;
    }

    public static (string AssemblyName, string OriginalSha256, string FrozenSha256) Freeze(
        string source,
        string destination,
        IReadOnlyList<DirectManagedHookPlan> directHooks) => Freeze(source, destination, directHooks, []);

    public static (string AssemblyName, string OriginalSha256, string FrozenSha256) Freeze(
        string source,
        string destination,
        IReadOnlyList<DirectManagedHookPlan> directHooks,
        IReadOnlyList<ModInteropRegistrationPlan> modInteropRegistrations)
    {
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(source, new ReaderParameters { ReadSymbols = false });
        string assemblyName = assembly.Name.Name;
        if (string.IsNullOrWhiteSpace(assemblyName) || assemblyName.Length > 255 ||
            assemblyName.Any(character => char.IsControl(character) || character is ';' or '<' or '>' or '"' or '\''))
            throw new InvalidDataException("external assembly has an unsafe identity");
        AssemblyNameReference? hook = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference => reference.Name == "MMHOOK_Celeste");
        AssemblyNameReference? runtimeDetour = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference => reference.Name == "MonoMod.RuntimeDetour");
        AssemblyNameReference? monoModUtils = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference => reference.Name == "MonoMod.Utils");
        AssemblyNameReference celeste = assembly.MainModule.AssemblyReferences.Single(reference => reference.Name == "Celeste");
        foreach (AssemblyNameReference reference in new[] { hook, runtimeDetour }.Where(reference => reference != null).Cast<AssemblyNameReference>())
        {
            foreach (TypeReference type in assembly.MainModule.GetTypeReferences())
                if (ReferenceEquals(type.Scope, reference)) type.Scope = celeste;
            if (!assembly.MainModule.GetTypeReferences().Any(type => ReferenceEquals(type.Scope, reference)))
                assembly.MainModule.AssemblyReferences.Remove(reference);
        }
        if (monoModUtils != null)
        {
            // Publicized desktop mod builds commonly carry MonoMod's compiler
            // access-bypass marker. The closed Apple product exposes only the
            // reviewed ABI surface, so discard that assembly-level request
            // rather than preserving a broad private-access capability.
            for (int index = assembly.CustomAttributes.Count - 1; index >= 0; index--)
                if (assembly.CustomAttributes[index].AttributeType.FullName ==
                    "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute")
                    assembly.CustomAttributes.RemoveAt(index);
            foreach (TypeReference type in assembly.MainModule.GetTypeReferences().Where(type =>
                         ReferenceEquals(type.Scope, monoModUtils) && type.Namespace == "MonoMod.ModInterop"))
                type.Scope = celeste;
            TypeReference[] residual = assembly.MainModule.GetTypeReferences()
                .Where(type => ReferenceEquals(type.Scope, monoModUtils) &&
                               type.FullName != "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute").ToArray();
            if (residual.Length != 0)
                throw new InvalidDataException("DEFERRED_MONOMOD_UTILS_SURFACE:" + string.Join(',',
                    residual.Select(type => type.FullName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(12)));
            assembly.MainModule.AssemblyReferences.Remove(monoModUtils);
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
        foreach (string name in ModInteropPlanner.RequiredPublicTypes(modInteropRegistrations).Distinct(StringComparer.Ordinal))
        {
            TypeDefinition? type = assembly.MainModule.Types.SelectMany(AllTypes)
                .SingleOrDefault(candidate => candidate.FullName.Replace('/', '.') == name);
            if (type == null) continue;
            MakePublic(type);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        assembly.Write(destination, new WriterParameters { WriteSymbols = false });
        return (assemblyName, Hashing.FileSha256(source), Hashing.FileSha256(destination));
    }

    private static void MakePublic(TypeDefinition type)
    {
        if (type.DeclaringType == null)
        {
            type.IsPublic = true;
            type.IsNotPublic = false;
        }
        else
        {
            MakePublic(type.DeclaringType);
            type.IsNestedPublic = true;
            type.IsNestedPrivate = false;
            type.IsNestedFamily = false;
            type.IsNestedAssembly = false;
            type.IsNestedFamilyAndAssembly = false;
            type.IsNestedFamilyOrAssembly = false;
        }
    }

    private static string? OverrideType(TypeDefinition module, string getter)
    {
        MethodDefinition? method = FindModuleMethod(module, getter);
        if (method == null) return null;
        TypeReference? result = method.Body.Instructions.Select(instruction => instruction.Operand).OfType<TypeReference>().LastOrDefault();
        return result?.FullName.Replace('/', '.');
    }

    private static TypeDefinition? OverrideTypeDefinition(TypeDefinition module, string getter)
    {
        MethodDefinition? method = FindModuleMethod(module, getter);
        TypeReference? result = method?.Body.Instructions.Select(instruction => instruction.Operand)
            .OfType<TypeReference>().LastOrDefault();
        try { return result?.Resolve(); }
        catch (AssemblyResolutionException) { return null; }
    }

    private static AppleModuleDurabilityCompatibility InspectDurability(TypeDefinition module,
        TypeDefinition? saveData, TypeDefinition? session, string mod)
    {
        string[] customSerializers =
        {
            "SerializeSaveData", "DeserializeSaveData", "SerializeSession", "DeserializeSession"
        };
        string[] customIo = { "ReadSaveData", "WriteSaveData", "ReadSession", "WriteSession" };
        string[] legacy =
        {
            "LoadSaveData", "SaveSaveData", "DeleteSaveData", "LoadSession", "SaveSession", "DeleteSession"
        };
        TypeDefinition[] moduleHierarchy = ModuleHierarchy(module).ToArray();
        HashSet<string> declared = moduleHierarchy.SelectMany(type => type.Methods)
            .Select(method => method.Name).ToHashSet(StringComparer.Ordinal);
        List<string> rejected = customSerializers.Concat(customIo).Concat(legacy)
            .Where(declared.Contains).OrderBy(value => value, StringComparer.Ordinal).ToList();
        bool asyncCustomized = declared.Contains("get_SaveDataAsync") || declared.Contains("set_SaveDataAsync") ||
            moduleHierarchy.SelectMany(type => type.Methods).Where(method => method.IsConstructor && method.HasBody)
                .SelectMany(method => method.Body.Instructions)
                .Select(instruction => instruction.Operand).OfType<MethodReference>()
                .Any(method => method.Name == "set_SaveDataAsync" && method.DeclaringType.FullName == "Celeste.Mod.EverestModule");
        if (asyncCustomized) rejected.Add("SaveDataAsync");

        string saveClass = saveData == null ? "NONE" : "DEFAULT_YAML_SAVEDATA_SUPPORTED";
        string sessionClass = session == null ? "NONE" : "DEFAULT_YAML_SESSION_SUPPORTED";
        if (saveData != null && Inherits(saveData, "Celeste.Mod.EverestModuleBinarySaveData"))
            saveClass = "BINARY_SAVEDATA_DEFERRED";
        if (session != null && Inherits(session, "Celeste.Mod.EverestModuleBinarySession"))
            sessionClass = "BINARY_SESSION_DEFERRED";
        if (rejected.Any(customSerializers.Contains))
        {
            if (saveData != null) saveClass = "CUSTOM_SERIALIZER_DEFERRED";
            if (session != null) sessionClass = "CUSTOM_SERIALIZER_DEFERRED";
        }
        else if (rejected.Any(customIo.Contains))
        {
            if (saveData != null) saveClass = "CUSTOM_IO_DEFERRED";
            if (session != null) sessionClass = "CUSTOM_IO_DEFERRED";
        }
        else if (rejected.Any(legacy.Contains))
        {
            if (saveData != null) saveClass = "LEGACY_SYNC_SAVEDATA_DEFERRED";
            if (session != null) sessionClass = "LEGACY_SYNC_SESSION_DEFERRED";
        }

        ValidateDurableRoot(saveData, "SaveData", mod);
        ValidateDurableRoot(session, "Session", mod);
        AppleModuleDurabilityCompatibility result = new()
        {
            SaveDataClass = saveClass,
            SessionClass = sessionClass,
            AsyncClass = asyncCustomized ? "CUSTOM_ASYNC_POLICY_DEFERRED" : "DEFAULT_ASYNC",
            RejectedOverrides = rejected.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
        };
        if (saveClass.EndsWith("DEFERRED", StringComparison.Ordinal) ||
            sessionClass.EndsWith("DEFERRED", StringComparison.Ordinal) || asyncCustomized)
            throw new InvalidDataException($"{mod} module durability is outside the bounded static profile: " +
                $"save={saveClass} session={sessionClass} async={result.AsyncClass} overrides={string.Join(',', result.RejectedOverrides)}");
        return result;
    }

    private static MethodDefinition? FindModuleMethod(TypeDefinition module, string name) =>
        ModuleHierarchy(module).SelectMany(type => type.Methods)
            .FirstOrDefault(method => method.Name == name && method.HasBody);

    private static IEnumerable<TypeDefinition> ModuleHierarchy(TypeDefinition module)
    {
        TypeDefinition? current = module;
        while (current != null && current.FullName != "Celeste.Mod.EverestModule")
        {
            yield return current;
            try { current = current.BaseType?.Resolve(); }
            catch (AssemblyResolutionException) { yield break; }
        }
    }

    private static void ValidateDurableRoot(TypeDefinition? type, string kind, string mod)
    {
        if (type == null) return;
        if (!PublicType(type) || type.IsAbstract || !type.Methods.Any(method => method.IsConstructor &&
                method.IsPublic && !method.IsStatic && method.Parameters.Count == 0))
            throw new InvalidDataException($"{mod} {kind} type requires a public parameterless constructor: {type.FullName}");
    }

    private static (AppleCustomEntityFactory[] Supported, AppleOmittedCustomEntityFactory[] Omitted)
        InspectCustomEntities(IEnumerable<TypeDefinition> types)
    {
        List<AppleCustomEntityFactory> supported = [];
        List<AppleOmittedCustomEntityFactory> omitted = [];
        foreach (TypeDefinition type in types)
        {
            string kind = Inherits(type, "Celeste.Trigger") ? "trigger" : "entity";
            if (!PublicType(type) || type.IsAbstract)
                throw new InvalidDataException($"custom entity type must be public and concrete: {type.FullName}");
            string? constructor = ConstructorKind(type);
            foreach (CustomAttribute attribute in type.CustomAttributes.Where(value =>
                         value.AttributeType.FullName == "Celeste.Mod.Entities.CustomEntityAttribute"))
            {
                foreach (string id in AttributeStrings(attribute))
                {
                    if (id.Length is < 1 or > 192 || id.Any(char.IsControl) || id.Contains('=') || id.Contains(','))
                        throw new InvalidDataException($"unsupported custom entity ID on {type.FullName}");
                    if (constructor == null)
                    {
                        omitted.Add(new AppleOmittedCustomEntityFactory
                        {
                            Id = id,
                            Type = type.FullName.Replace('/', '.'),
                            Reason = "runtime-only-constructor"
                        });
                    }
                    else
                    {
                        supported.Add(new AppleCustomEntityFactory
                        {
                            Id = id,
                            Type = type.FullName.Replace('/', '.'),
                            Kind = kind,
                            Constructor = constructor
                        });
                    }
                }
            }
        }
        return (supported.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray(),
            omitted.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray());
    }

    private static IEnumerable<string> AttributeStrings(CustomAttribute attribute)
    {
        foreach (CustomAttributeArgument argument in attribute.ConstructorArguments)
        {
            if (argument.Value is string text) yield return text;
            else if (argument.Value is CustomAttributeArgument[] values)
                foreach (CustomAttributeArgument item in values)
                    if (item.Value is string value) yield return value;
        }
    }

    private static IEnumerable<AppleCustomBackdropFactory> CustomBackdropFactories(TypeDefinition type)
    {
        if (!PublicType(type) || type.IsAbstract || !Inherits(type, "Celeste.Backdrop"))
            throw new InvalidDataException($"custom backdrop factory type must be public and concrete: {type.FullName}");
        foreach (CustomAttribute attribute in type.CustomAttributes.Where(value =>
                     value.AttributeType.FullName == "Celeste.Mod.Backdrops.CustomBackdropAttribute"))
        {
            foreach (string full in AttributeStrings(attribute))
            {
                string[] parts = full.Split('=');
                if (parts.Length is < 1 or > 2)
                    throw new InvalidDataException($"unsupported custom backdrop ID on {type.FullName}");
                string id = parts[0].Trim();
                string method = parts.Length == 2 ? parts[1].Trim() : "Load";
                if (id.Length is < 1 or > 192 || id.Any(char.IsControl) || !MemberName(method))
                    throw new InvalidDataException($"unsupported custom backdrop ID on {type.FullName}");
                MethodDefinition? generator = type.Methods.SingleOrDefault(candidate => candidate.Name == method &&
                    candidate.IsPublic && candidate.IsStatic && candidate.Parameters.Count == 1 &&
                    candidate.Parameters[0].ParameterType.FullName == "Celeste.BinaryPacker/Element" &&
                    (candidate.ReturnType.FullName is "Celeste.Backdrop" ||
                     candidate.ReturnType.FullName == type.FullName));
                MethodDefinition? constructor = type.Methods.SingleOrDefault(candidate => candidate.IsConstructor &&
                    candidate.IsPublic && !candidate.IsStatic && candidate.Parameters.Count == 1 &&
                    candidate.Parameters[0].ParameterType.FullName == "Celeste.BinaryPacker/Element");
                if (generator == null && constructor == null)
                    throw new InvalidDataException($"custom backdrop type has no supported public factory: {type.FullName}");
                yield return new AppleCustomBackdropFactory
                {
                    Id = id,
                    Type = type.FullName.Replace('/', '.'),
                    Factory = generator != null ? "static-method" : "constructor",
                    Method = generator != null ? method : ""
                };
            }
        }
    }

    private static string? ConstructorKind(TypeDefinition type)
    {
        static string Signature(MethodDefinition method) => string.Join(",", method.Parameters.Select(value => value.ParameterType.FullName));
        string[] supported =
        [
            "Celeste.EntityData,Microsoft.Xna.Framework.Vector2",
            "Celeste.EntityData,Microsoft.Xna.Framework.Vector2,Celeste.EntityID",
            "Celeste.EntityID,Celeste.EntityData,Microsoft.Xna.Framework.Vector2"
        ];
        MethodDefinition[] constructors = type.Methods.Where(method => method.IsConstructor && !method.IsStatic && method.IsPublic &&
            supported.Contains(Signature(method), StringComparer.Ordinal)).ToArray();
        if (constructors.Length == 0) return null;
        if (constructors.Length != 1)
            throw new InvalidDataException($"custom entity type has ambiguous supported constructors: {type.FullName}");
        return Signature(constructors[0]) switch
        {
            "Celeste.EntityData,Microsoft.Xna.Framework.Vector2" => "entity-data-vector2",
            "Celeste.EntityData,Microsoft.Xna.Framework.Vector2,Celeste.EntityID" => "entity-data-vector2-entity-id",
            "Celeste.EntityID,Celeste.EntityData,Microsoft.Xna.Framework.Vector2" => "entity-id-entity-data-vector2",
            _ => throw new InvalidDataException("unreachable custom entity constructor")
        };
    }

    private static (AppleSettingProperty[] Supported, string[] Omitted) InspectSettings(TypeDefinition? settings)
    {
        if (settings == null) return ([], []);
        List<AppleSettingProperty> supported = [];
        List<string> omitted = [];
        foreach (PropertyDefinition property in settings.Properties.OrderBy(value => value.Name, StringComparer.Ordinal))
        {
            if (property.GetMethod is not { IsPublic: true, IsStatic: false } ||
                property.SetMethod is not { IsPublic: true, IsStatic: false })
            {
                omitted.Add(property.Name + ":not-public-read-write");
                continue;
            }
            string typeName = property.PropertyType.FullName.Replace('/', '.');
            AppleSettingProperty? descriptor = null;
            if (typeName == "System.Boolean")
            {
                descriptor = new AppleSettingProperty { Name = property.Name, Label = Humanize(property.Name), Kind = "bool", Type = typeName };
            }
            else
            {
                TypeDefinition? definition = Resolve(property.PropertyType);
                if (definition?.IsEnum == true)
                {
                    (string Name, int Value)[] values = definition.Fields.Where(field => field.IsStatic && field.HasConstant)
                        .Select(field => (Name: field.Name, Value: Convert.ToInt32(field.Constant, System.Globalization.CultureInfo.InvariantCulture)))
                        .OrderBy(value => value.Value).ThenBy(value => value.Name, StringComparer.Ordinal).ToArray();
                    if (values.Length is > 0 and <= 64 && values.Select(value => value.Value).Distinct().Count() == values.Length)
                    {
                        descriptor = new AppleSettingProperty
                        {
                            Name = property.Name,
                            Label = Humanize(property.Name),
                            Kind = "enum",
                            Type = typeName,
                            EnumNames = values.Select(value => Humanize(value.Name)).ToArray(),
                            EnumValues = values.Select(value => value.Value).ToArray()
                        };
                    }
                }
                else if (typeName == "System.Int32" && TryRange(property, out int minimum, out int maximum) &&
                         maximum >= minimum && maximum - minimum <= 255)
                {
                    descriptor = new AppleSettingProperty
                    {
                        Name = property.Name,
                        Label = Humanize(property.Name),
                        Kind = "int",
                        Type = typeName,
                        Minimum = minimum,
                        Maximum = maximum,
                        Step = 1
                    };
                }
            }
            if (descriptor == null) omitted.Add(property.Name + ":" + typeName);
            else supported.Add(descriptor);
        }
        return (supported.ToArray(), omitted.ToArray());
    }

    private static bool TryRange(PropertyDefinition property, out int minimum, out int maximum)
    {
        minimum = maximum = 0;
        CustomAttribute? range = property.CustomAttributes.FirstOrDefault(attribute =>
            (attribute.AttributeType.Name is "SettingRangeAttribute" or "SettingNumberInputAttribute") &&
            attribute.ConstructorArguments.Count >= 2);
        if (range == null) return false;
        try
        {
            minimum = Convert.ToInt32(range.ConstructorArguments[0].Value, System.Globalization.CultureInfo.InvariantCulture);
            maximum = Convert.ToInt32(range.ConstructorArguments[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception) { return false; }
    }

    private static TypeDefinition? Resolve(TypeReference type)
    {
        try { return type.Resolve(); }
        catch (AssemblyResolutionException) { return null; }
    }

    private static bool PublicType(TypeDefinition type) => type.IsPublic ||
        type.IsNestedPublic && type.DeclaringType != null && PublicType(type.DeclaringType);

    private static string Humanize(string value)
    {
        StringBuilder result = new();
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (index > 0 && char.IsUpper(current) && (char.IsLower(value[index - 1]) ||
                index + 1 < value.Length && char.IsLower(value[index + 1]))) result.Append(' ');
            result.Append(current);
        }
        return result.ToString();
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
        if (declaration.Durability.SaveDataClass is not ("NONE" or "DEFAULT_YAML_SAVEDATA_SUPPORTED") ||
            declaration.Durability.SessionClass is not ("NONE" or "DEFAULT_YAML_SESSION_SUPPORTED") ||
            declaration.Durability.AsyncClass != "DEFAULT_ASYNC" || declaration.Durability.RejectedOverrides.Length != 0)
            throw new InvalidDataException($"unsupported durability declaration for {mod}");
        if (declaration.ButtonBindingProperties.Length > 64 || declaration.ButtonBindingProperties.Any(name => !MemberName(name)))
            throw new InvalidDataException($"invalid button-binding factory declaration for {mod}");
        if (declaration.TrackedEntityTypes.Length > 256 || declaration.TrackedEntityTypes.Any(type => !TypeName(type)))
            throw new InvalidDataException($"invalid tracked entity declaration for {mod}");
        if (declaration.CustomEntityFactories.Length > 512 || declaration.CustomEntityFactories.Any(factory =>
                factory.Id.Length is < 1 or > 192 || !TypeName(factory.Type) ||
                factory.Kind is not ("entity" or "trigger") ||
                factory.Constructor is not ("entity-data-vector2" or "entity-data-vector2-entity-id" or "entity-id-entity-data-vector2")))
            throw new InvalidDataException($"invalid custom entity factory declaration for {mod}");
        if (declaration.CustomEntityFactories.Select(factory => factory.Id).Distinct(StringComparer.Ordinal).Count() !=
            declaration.CustomEntityFactories.Length)
            throw new InvalidDataException($"duplicate custom entity factory ID for {mod}");
        if (declaration.OmittedCustomEntityFactories.Length > 512 || declaration.OmittedCustomEntityFactories.Any(factory =>
                factory.Id.Length is < 1 or > 192 || !TypeName(factory.Type) ||
                factory.Reason != "runtime-only-constructor"))
            throw new InvalidDataException($"invalid omitted custom entity factory declaration for {mod}");
        if (declaration.CustomBackdropFactories.Length > 256 || declaration.CustomBackdropFactories.Any(factory =>
                factory.Id.Length is < 1 or > 192 || !TypeName(factory.Type) ||
                factory.Factory is not ("constructor" or "static-method") ||
                factory.Factory == "static-method" && !MemberName(factory.Method)))
            throw new InvalidDataException($"invalid custom backdrop factory declaration for {mod}");
        if (declaration.CustomBackdropFactories.Select(factory => factory.Id).Distinct(StringComparer.Ordinal).Count() !=
            declaration.CustomBackdropFactories.Length)
            throw new InvalidDataException($"duplicate custom backdrop factory ID for {mod}");
        if (declaration.SettingsProperties.Length > 128 || declaration.SettingsProperties.Any(property =>
                !MemberName(property.Name) || property.Label.Length is < 1 or > 192 ||
                property.Kind is not ("bool" or "enum" or "int") || !TypeName(property.Type)))
            throw new InvalidDataException($"invalid settings property declaration for {mod}");
        if (declaration.OmittedSettingsProperties.Length > 128 || declaration.OmittedSettingsProperties.Any(value => value.Length is < 1 or > 256))
            throw new InvalidDataException($"invalid omitted settings declaration for {mod}");
    }

    private static bool TypeName(string value) => value.Length is > 0 and < 256 &&
        value.Split('.').All(part => part.Length > 0 && part.All(ch => char.IsLetterOrDigit(ch) || ch == '_'));

    private static bool MemberName(string value) => value.Length is > 0 and < 128 &&
        value.All(character => char.IsLetterOrDigit(character) || character == '_');
}
