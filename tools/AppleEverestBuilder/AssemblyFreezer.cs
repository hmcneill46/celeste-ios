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

    public static AppleStaticDeclaration InspectDeclaration(string path, string mod,
        bool allowNonPublicCustomFactories = false)
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
        TypeDefinition[] pooledTypes = assembly.MainModule.Types.SelectMany(AllTypes)
            .Where(type => type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "Monocle.Pooled"))
            // The generated shared registry is deliberately reflection-free and can therefore
            // construct only types exposed by the exact external assembly. Private nested pools
            // remain owned by their declaring module and are not silently made public.
            .Where(IsPubliclyConstructiblePooledType)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        (AppleSettingProperty[] settings, string[] omittedSettings) = InspectSettings(settingsDefinition);
        (AppleCustomEntityFactory[] customEntityFactories,
            AppleOmittedCustomEntityFactory[] omittedCustomEntityFactories) = InspectCustomEntities(customTypes,
                allowNonPublicCustomFactories);
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
            PooledEntityTypes = pooledTypes.Select(type => type.FullName.Replace('/', '.')).ToArray(),
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
        IReadOnlyList<ModInteropRegistrationPlan> modInteropRegistrations,
        IReadOnlyList<FrozenIlTransformPlan>? frozenIlTransforms = null,
        StaticAotCompatibilityPlan? staticAotCompatibility = null)
    {
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(source, new ReaderParameters { ReadSymbols = false });
        frozenIlTransforms ??= [];
        StaticIlFreeze.RewriteDeviceAssembly(assembly, frozenIlTransforms);
        StaticAotCompatibility.RewriteDeviceAssembly(assembly, staticAotCompatibility);
        string assemblyName = assembly.Name.Name;
        if (string.IsNullOrWhiteSpace(assemblyName) || assemblyName.Length > 255 ||
            assemblyName.Any(character => char.IsControl(character) || character is ';' or '<' or '>' or '"' or '\''))
            throw new InvalidDataException("external assembly has an unsafe identity");
        AssemblyNameReference? hook = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference => reference.Name == "MMHOOK_Celeste");
        AssemblyNameReference? runtimeDetour = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference => reference.Name == "MonoMod.RuntimeDetour");
        AssemblyNameReference? monoModUtils = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference => reference.Name == "MonoMod.Utils");
        AssemblyNameReference? celeste = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference => reference.Name == "Celeste");
        foreach (AssemblyNameReference reference in new[] { hook, runtimeDetour }.Where(reference => reference != null).Cast<AssemblyNameReference>())
        {
            if (celeste == null)
                throw new InvalidDataException("managed detour facade requires a Celeste assembly reference");
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
            {
                if (celeste == null)
                    throw new InvalidDataException("ModInterop facade requires a Celeste assembly reference");
                type.Scope = celeste;
            }
            foreach (TypeReference type in assembly.MainModule.GetTypeReferences().Where(type =>
                         ReferenceEquals(type.Scope, monoModUtils) &&
                         StaticAotCompatibility.AllowsMonoModType(staticAotCompatibility, type.FullName)))
            {
                if (celeste == null)
                    throw new InvalidDataException("static-AOT MonoMod facade requires a Celeste assembly reference");
                type.Scope = celeste;
            }
            string[] residual = ActiveAssemblyReferenceTypes(assembly.MainModule, monoModUtils.Name)
                .Where(type => type != "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute")
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (residual.Length != 0)
                throw new InvalidDataException("DEFERRED_MONOMOD_UTILS_SURFACE:" + string.Join(',',
                    residual.Take(12)));
            assembly.MainModule.AssemblyReferences.Remove(monoModUtils);
        }

        // Static YAML factories are generated and type-checked on the Mac.
        // The device never reflects over YamlDotNet attributes or carries the
        // desktop serializer, so remove the marker after it has informed the
        // host-side DTO audit and drop the now-unused package reference.
        AssemblyNameReference? yamlDotNet = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference =>
            reference.Name == "YamlDotNet");
        if (yamlDotNet != null)
        {
            foreach (TypeDefinition type in assembly.MainModule.Types.SelectMany(AllTypes))
            foreach (PropertyDefinition property in type.Properties)
                for (int index = property.CustomAttributes.Count - 1; index >= 0; index--)
                    if (property.CustomAttributes[index].AttributeType.FullName ==
                        "YamlDotNet.Serialization.YamlIgnoreAttribute")
                        property.CustomAttributes.RemoveAt(index);
            TypeReference[] residualYaml = assembly.MainModule.GetTypeReferences().Where(type =>
                ReferenceEquals(type.Scope, yamlDotNet)).ToArray();
            TypeReference[] unsupportedYaml = residualYaml.Where(type =>
                type.FullName != "YamlDotNet.Serialization.YamlIgnoreAttribute").ToArray();
            if (unsupportedYaml.Length != 0)
                throw new InvalidDataException("DEFERRED_YAMLDOTNET_SURFACE:" + string.Join(',', unsupportedYaml
                    .Select(type => type.FullName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(12)));
            if (residualYaml.Length != 0)
            {
                if (celeste == null)
                    throw new InvalidDataException("static YAML marker normalization requires a Celeste assembly reference");
                foreach (TypeReference marker in residualYaml) marker.Scope = celeste;
            }
            assembly.MainModule.AssemblyReferences.Remove(yamlDotNet);
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
            if (celeste == null)
                throw new InvalidDataException("direct managed Hook requires a Celeste assembly reference");
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
            int expressionStart = constructorIndex - plan.ExpressionInstructionCount;
            if (expressionStart < 0 || instructions[constructorIndex].OpCode != OpCodes.Newobj ||
                instructions[constructorIndex].Operand is not MethodReference originalConstructor ||
                originalConstructor.DeclaringType.FullName != "MonoMod.RuntimeDetour.Hook")
                throw new InvalidDataException($"direct managed Hook rewrite drifted: {plan.PlanId}");
            instructions[expressionStart].OpCode = OpCodes.Ldstr;
            instructions[expressionStart].Operand = plan.PlanId;
            for (int index = expressionStart + 1; index < constructorIndex; index++)
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
        InstrumentModInteropExports(assembly, celeste, modInteropRegistrations);
        ImportForeignDefinitionOperands(assembly.MainModule);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        assembly.Write(destination, new WriterParameters { WriteSymbols = false });
        return (assemblyName, Hashing.FileSha256(source), Hashing.FileSha256(destination));
    }

    private static void ImportForeignDefinitionOperands(ModuleDefinition module)
    {
        foreach (MethodDefinition method in module.Types.SelectMany(AllTypes).SelectMany(type => type.Methods)
                     .Where(method => method.HasBody))
        foreach (Instruction instruction in method.Body.Instructions)
        {
            try
            {
                instruction.Operand = instruction.Operand switch
                {
                    MethodDefinition definition when definition.Module != module => module.ImportReference(definition),
                    FieldDefinition definition when definition.Module != module => module.ImportReference(definition),
                    TypeDefinition definition when definition.Module != module => module.ImportReference(definition),
                    _ => instruction.Operand
                };
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("foreign definition import failed in " + method.FullName +
                    " at " + instruction.Offset + ": " + instruction.Operand, exception);
            }
        }
    }

    private static void MakePublic(TypeDefinition type)
    {
        if (type.DeclaringType == null)
        {
            type.Attributes = (type.Attributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.Public;
        }
        else
        {
            MakePublic(type.DeclaringType);
            type.Attributes = (type.Attributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.NestedPublic;
        }
    }

    private static void InstrumentModInteropExports(AssemblyDefinition assembly, AssemblyNameReference? celeste,
        IReadOnlyList<ModInteropRegistrationPlan> registrations)
    {
        if (registrations.Count == 0) return;
        if (celeste == null)
            throw new InvalidDataException("static ModInterop diagnostics require a Celeste assembly reference");
        TypeReference runtime = new("Celeste.Mod", "AppleEverestStaticRuntime", assembly.MainModule, celeste);
        MethodReference record = new("RecordModInteropExportInvocation", assembly.MainModule.TypeSystem.Void, runtime)
        {
            HasThis = false
        };
        record.Parameters.Add(new ParameterDefinition(assembly.MainModule.TypeSystem.String));
        foreach (ModInteropRegistrationPlan registration in registrations)
        {
            TypeDefinition type = assembly.MainModule.Types.SelectMany(AllTypes)
                .Single(candidate => candidate.FullName.Replace('/', '.') == registration.RegisteredType);
            MethodDefinition[] methods = type.Methods.Where(method => method.IsPublic && method.IsStatic && !method.IsConstructor)
                .ToArray();
            foreach (ModInteropExportPlan export in registration.Exports)
            {
                if (export.MethodOrder < 0 || export.MethodOrder >= methods.Length ||
                    methods[export.MethodOrder].Name != export.Method)
                    throw new InvalidDataException($"static ModInterop export order drifted: {registration.RegisteredType}::{export.Method}");
                MethodDefinition method = methods[export.MethodOrder];
                if (!method.HasBody)
                    throw new InvalidDataException($"static ModInterop export has no body: {registration.RegisteredType}::{export.Method}");
                ILProcessor il = method.Body.GetILProcessor();
                Instruction first = method.Body.Instructions.First();
                il.InsertBefore(first, il.Create(OpCodes.Ldstr, export.Names.Last()));
                il.InsertBefore(first, il.Create(OpCodes.Call, record));
            }
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
        InspectCustomEntities(IEnumerable<TypeDefinition> types, bool allowNonPublic)
    {
        List<AppleCustomEntityFactory> supported = [];
        List<AppleOmittedCustomEntityFactory> omitted = [];
        foreach (TypeDefinition type in types)
        {
            string kind = Inherits(type, "Celeste.Trigger") ? "trigger" : "entity";
            if ((!PublicType(type) && !allowNonPublic) || type.IsAbstract)
                throw new InvalidDataException($"custom entity type must be public and concrete: {type.FullName}");
            string? constructor = ConstructorKind(type);
            foreach (CustomAttribute attribute in type.CustomAttributes.Where(value =>
                         value.AttributeType.FullName == "Celeste.Mod.Entities.CustomEntityAttribute"))
            {
                foreach (string full in AttributeStrings(attribute))
                {
                    string[] alias = full.Split('=', 2, StringSplitOptions.TrimEntries);
                    string id = alias[0];
                    if (id.Length is < 1 or > 192 || id.Any(char.IsControl) || id.Contains('=') || id.Contains(','))
                        throw new InvalidDataException($"unsupported custom entity ID on {type.FullName}");
                    if (alias.Length == 2 || constructor == null)
                    {
                        omitted.Add(new AppleOmittedCustomEntityFactory
                        {
                            Id = id,
                            Type = type.FullName.Replace('/', '.'),
                            Reason = alias.Length == 2 ? "static-method-factory" : "runtime-only-constructor"
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
        // An unused helper factory with no deterministic constructor is safe to
        // omit. Closure generation still rejects the product if any mounted
        // map actually references that ID, so ambiguity never becomes a
        // runtime constructor guess.
        if (constructors.Length != 1) return null;
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

    // Cecil retains harmless orphan TypeRef rows after the exact host-only IL
    // scaffolding has been removed. Gate the device closure on references that
    // are still reachable from live metadata/IL rather than those stale rows.
    private static IEnumerable<string> ActiveAssemblyReferenceTypes(ModuleDefinition module, string assemblyName)
    {
        foreach (TypeDefinition type in module.Types.SelectMany(AllTypes))
        {
            foreach (TypeReference reference in SignatureTypes(type.BaseType))
                if (UsesAssembly(reference, assemblyName)) yield return reference.FullName;
            foreach (InterfaceImplementation implementation in type.Interfaces)
            foreach (TypeReference reference in SignatureTypes(implementation.InterfaceType))
                if (UsesAssembly(reference, assemblyName)) yield return reference.FullName;
            foreach (FieldDefinition field in type.Fields)
            foreach (TypeReference reference in SignatureTypes(field.FieldType))
                if (UsesAssembly(reference, assemblyName)) yield return reference.FullName;
            foreach (PropertyDefinition property in type.Properties)
            foreach (TypeReference reference in SignatureTypes(property.PropertyType)
                         .Concat(property.Parameters.SelectMany(parameter => SignatureTypes(parameter.ParameterType))))
                if (UsesAssembly(reference, assemblyName)) yield return reference.FullName;
            foreach (EventDefinition @event in type.Events)
            foreach (TypeReference reference in SignatureTypes(@event.EventType))
                if (UsesAssembly(reference, assemblyName)) yield return reference.FullName;
            foreach (MethodDefinition method in type.Methods)
            {
                IEnumerable<TypeReference> signature = SignatureTypes(method.ReturnType)
                    .Concat(method.Parameters.SelectMany(parameter => SignatureTypes(parameter.ParameterType)))
                    .Concat(method.GenericParameters.SelectMany(parameter => parameter.Constraints)
                        .SelectMany(constraint => SignatureTypes(constraint.ConstraintType)));
                foreach (TypeReference reference in signature)
                    if (UsesAssembly(reference, assemblyName)) yield return reference.FullName;
                if (!method.HasBody) continue;
                foreach (TypeReference reference in method.Body.Variables
                             .SelectMany(variable => SignatureTypes(variable.VariableType))
                             .Concat(method.Body.ExceptionHandlers.SelectMany(handler => SignatureTypes(handler.CatchType))))
                    if (UsesAssembly(reference, assemblyName)) yield return reference.FullName;
                foreach (MemberReference member in method.Body.Instructions.Select(instruction => instruction.Operand)
                             .OfType<MemberReference>())
                {
                    IEnumerable<TypeReference> referenced = SignatureTypes(member.DeclaringType);
                    if (member is MethodReference called)
                        referenced = referenced.Concat(SignatureTypes(called.ReturnType))
                            .Concat(called.Parameters.SelectMany(parameter => SignatureTypes(parameter.ParameterType)));
                    else if (member is FieldReference accessed)
                        referenced = referenced.Concat(SignatureTypes(accessed.FieldType));
                    else if (member is TypeReference operandType)
                        referenced = referenced.Concat(SignatureTypes(operandType));
                    foreach (TypeReference reference in referenced)
                        if (UsesAssembly(reference, assemblyName)) yield return reference.FullName;
                }
            }
        }
    }

    private static IEnumerable<TypeReference> SignatureTypes(TypeReference? type)
    {
        if (type == null) yield break;
        yield return type;
        if (type is GenericInstanceType generic)
            foreach (TypeReference argument in generic.GenericArguments.SelectMany(SignatureTypes)) yield return argument;
        if (type is TypeSpecification specification)
            foreach (TypeReference element in SignatureTypes(specification.ElementType)) yield return element;
    }

    private static bool UsesAssembly(TypeReference type, string assemblyName)
    {
        TypeReference element = type.GetElementType();
        // A generic parameter belongs to its declaring type or method, not an
        // external assembly. Cecil's Scope accessor also assumes that owner is
        // still attached; exact rewrites may intentionally remove the owning
        // generic factory before this live-metadata census runs.
        if (element is GenericParameter) return false;
        return element.Scope is AssemblyNameReference reference &&
               string.Equals(reference.Name, assemblyName, StringComparison.Ordinal);
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
        if (declaration.PooledEntityTypes.Length > 256 || declaration.PooledEntityTypes.Any(type => !TypeName(type)))
            throw new InvalidDataException($"invalid pooled entity declaration for {mod}");
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
                factory.Reason is not ("runtime-only-constructor" or "static-method-factory")))
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

    private static bool IsPubliclyConstructiblePooledType(TypeDefinition type)
    {
        for (TypeDefinition? current = type; current != null; current = current.DeclaringType)
        {
            if (current.DeclaringType == null ? !current.IsPublic : !current.IsNestedPublic)
                return false;
        }
        return !type.IsAbstract && type.Methods.Any(method => method.IsConstructor && !method.IsStatic &&
            method.IsPublic && method.Parameters.Count == 0);
    }

    private static bool TypeName(string value) => value.Length is > 0 and < 256 &&
        value.Split('.').All(part => part.Length > 0 && part.All(ch => char.IsLetterOrDigit(ch) || ch == '_'));

    private static bool MemberName(string value) => value.Length is > 0 and < 128 &&
        value.All(character => char.IsLetterOrDigit(character) || character == '_');
}
