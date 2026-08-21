using System.Text;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AppleEverestBuilder;

internal sealed record ModInteropParameterPlan(
    string TypeKey,
    string CSharpType,
    string Modifier,
    IReadOnlyList<string> AssignableBaseTypeKeys);

internal sealed record ModInteropSignaturePlan(
    string ReturnTypeKey,
    string ReturnCSharpType,
    IReadOnlyList<string> ReturnAssignableBaseTypeKeys,
    IReadOnlyList<ModInteropParameterPlan> Parameters,
    bool Supported,
    string? DeferredReason)
{
    internal string StableIdentity => ReturnTypeKey + "(" + string.Join(",",
        Parameters.Select(parameter => parameter.Modifier + ":" + parameter.TypeKey)) + ")";
}

internal sealed record ModInteropExportPlan(
    int MethodOrder,
    string DeclaringType,
    string Method,
    IReadOnlyList<string> Names,
    ModInteropSignaturePlan Signature);

internal sealed record ModInteropImportPlan(
    int FieldOrder,
    string DeclaringType,
    string Field,
    string ImportName,
    string DelegateType,
    string DelegateDefinitionType,
    ModInteropSignaturePlan Signature);

internal sealed record ModInteropCallSitePlan(string ContainingMethod, int Offset);

internal sealed record ModInteropRegistrationPlan(
    string Owner,
    string AssemblyName,
    string RegisteredType,
    IReadOnlyList<ModInteropCallSitePlan> CallSites,
    IReadOnlyList<ModInteropExportPlan> Exports,
    IReadOnlyList<ModInteropImportPlan> Imports);

internal sealed record GeneratedModInteropPlan(
    string Source,
    string PlanSha256,
    int RegistrationCount,
    int ExportCount,
    int ImportCount,
    int ResolvedImportCount,
    object Manifest);

/// <summary>
/// Host-only Cecil analysis and source generation for the exact pinned
/// MonoMod.ModInterop contract. Device code receives only direct type tests,
/// registration ordinals, and statically typed delegate assignments.
/// </summary>
internal static class ModInteropPlanner
{
    private const string ManagerType = "MonoMod.ModInterop.ModInteropManager";
    private const string ExportAttribute = "MonoMod.ModInterop.ModExportNameAttribute";
    private const string ImportAttribute = "MonoMod.ModInterop.ModImportNameAttribute";

    internal static IReadOnlyList<ModInteropRegistrationPlan> Analyze(
        AssemblyDefinition assembly,
        string owner,
        bool rejectUnsupported,
        Action<string> record)
    {
        Dictionary<string, List<ModInteropCallSitePlan>> callSites = new(StringComparer.Ordinal);
        foreach (MethodDefinition method in assembly.MainModule.Types.SelectMany(AllTypes)
                     .SelectMany(type => type.Methods).Where(method => method.HasBody))
        {
            Instruction[] body = method.Body.Instructions.ToArray();
            for (int index = 0; index < body.Length; index++)
            {
                if (body[index].Operand is not MethodReference called || called.DeclaringType.FullName != ManagerType ||
                    called.Name != "ModInterop") continue;
                TypeReference? registered = StaticTypeArgument(body, index);
                if (registered == null)
                {
                    string reason = $"DEFERRED_DYNAMIC_MODINTEROP_TYPE:{owner}:{method.FullName}@{body[index].Offset}";
                    if (rejectUnsupported) throw new InvalidDataException(reason);
                    record(reason);
                    continue;
                }
                TypeDefinition? definition = assembly.MainModule.Types.SelectMany(AllTypes)
                    .SingleOrDefault(type => type.FullName == registered.FullName);
                if (definition == null)
                {
                    string reason = $"DEFERRED_EXTERNAL_MODINTEROP_TYPE:{owner}:{registered.FullName}";
                    if (rejectUnsupported) throw new InvalidDataException(reason);
                    record(reason);
                    continue;
                }
                if (definition.HasGenericParameters)
                {
                    string reason = $"DEFERRED_OPEN_GENERIC_MODINTEROP_TYPE:{owner}:{registered.FullName}";
                    if (rejectUnsupported) throw new InvalidDataException(reason);
                    record(reason);
                    continue;
                }
                if (!callSites.TryGetValue(definition.FullName, out List<ModInteropCallSitePlan>? sites))
                    callSites[definition.FullName] = sites = [];
                sites.Add(new ModInteropCallSitePlan(method.FullName, body[index].Offset));
            }
        }

        List<ModInteropRegistrationPlan> plans = [];
        foreach ((string typeName, List<ModInteropCallSitePlan> sites) in callSites.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            TypeDefinition type = assembly.MainModule.Types.SelectMany(AllTypes).Single(value => value.FullName == typeName);
            string prefix = assembly.Name.Name;
            foreach (CustomAttribute attribute in type.CustomAttributes.Where(value => value.AttributeType.FullName == ExportAttribute))
                prefix = AttributeName(attribute, ExportAttribute, type.FullName);

            List<ModInteropImportPlan> imports = [];
            int fieldOrder = 0;
            foreach (FieldDefinition field in type.Fields.Where(field => field.IsPublic && field.IsStatic))
            {
                if (!TryDelegateSignature(field.FieldType, out ModInteropSignaturePlan? signature)) continue;
                if (field.IsInitOnly)
                {
                    string reason = $"DEFERRED_READONLY_MODINTEROP_IMPORT:{owner}:{type.FullName}::{field.Name}";
                    if (rejectUnsupported) throw new InvalidDataException(reason);
                    record(reason);
                    continue;
                }
                string importName = field.Name;
                CustomAttribute? fieldName = field.CustomAttributes.FirstOrDefault(value => value.AttributeType.FullName == ImportAttribute);
                if (fieldName != null) importName = AttributeName(fieldName, ImportAttribute, field.FullName);
                else
                {
                    CustomAttribute? typeNameAttribute = type.CustomAttributes.FirstOrDefault(value => value.AttributeType.FullName == ImportAttribute);
                    if (typeNameAttribute != null)
                        importName = AttributeName(typeNameAttribute, ImportAttribute, type.FullName) + "." + field.Name;
                }
                TypeReference delegateDefinition = field.FieldType is GenericInstanceType genericDelegate
                    ? genericDelegate.ElementType
                    : field.FieldType;
                ModInteropImportPlan import = new(fieldOrder++, CSharpTypeName(type), field.Name, importName,
                    CSharpType(field.FieldType), CSharpTypeName(delegateDefinition), signature!);
                if (!signature!.Supported)
                {
                    string reason = $"{signature.DeferredReason}:{owner}:{type.FullName}::{field.Name}";
                    if (rejectUnsupported) throw new InvalidDataException(reason);
                    record(reason);
                }
                imports.Add(import);
            }

            List<ModInteropExportPlan> exports = [];
            int methodOrder = 0;
            foreach (MethodDefinition method in type.Methods.Where(method => method.IsPublic && method.IsStatic && !method.IsConstructor))
            {
                ModInteropSignaturePlan signature = MethodSignature(method);
                if (!signature.Supported)
                {
                    string reason = $"{signature.DeferredReason}:{owner}:{method.FullName}";
                    if (rejectUnsupported) throw new InvalidDataException(reason);
                    record(reason);
                }
                string[] names = string.IsNullOrEmpty(prefix) || prefix + "." + method.Name == method.Name
                    ? [method.Name]
                    : [method.Name, prefix + "." + method.Name];
                exports.Add(new ModInteropExportPlan(methodOrder++, CSharpTypeName(method.DeclaringType), method.Name,
                    names.Distinct(StringComparer.Ordinal).ToArray(), signature));
            }
            plans.Add(new ModInteropRegistrationPlan(owner, assembly.Name.Name, CSharpTypeName(type),
                sites.OrderBy(value => value.ContainingMethod, StringComparer.Ordinal).ThenBy(value => value.Offset).ToArray(),
                exports, imports));
            record($"static-modinterop:{type.FullName}:calls={sites.Count}:exports={exports.Count}:imports={imports.Count}");
        }
        return plans;
    }

    internal static GeneratedModInteropPlan Generate(IReadOnlyList<ResolvedMod> ordered, bool rejectRequiredMissing = true)
    {
        var registrationEntries = ordered.SelectMany(mod => mod.ModInteropRegistrations
            .Select(plan => (Mod: mod, Plan: plan))).ToArray();
        ModInteropRegistrationPlan[] registrations = registrationEntries.Select(entry => entry.Plan).ToArray();
        List<(int Registration, ModInteropExportPlan Export)> exports = registrations.SelectMany((registration, index) =>
            registration.Exports.Select(export => (index, export))).ToList();
        List<(int Registration, ModInteropImportPlan Import)> imports = registrations.SelectMany((registration, index) =>
            registration.Imports.Select(import => (index, import))).ToList();

        var bindings = imports.Select(item =>
        {
            var candidates = exports.Where(export => export.Export.Names.Contains(item.Import.ImportName, StringComparer.Ordinal) &&
                                                       Compatible(item.Import.Signature, export.Export.Signature))
                .GroupBy(export => export.Registration)
                .Select(group => group.OrderBy(value => value.Export.MethodOrder).First())
                .OrderBy(value => value.Registration).ThenBy(value => value.Export.MethodOrder).ToArray();
            string status = candidates.Length != 0 ? "BOUND_CANDIDATE" :
                RequiredProvider(item.Import.ImportName, registrationEntries[item.Registration].Mod)
                    ? "REQUIRED_INTEROP_PROVIDER_ABSENT"
                    : "OPTIONAL_INTEROP_PROVIDER_ABSENT";
            return new { item.Registration, item.Import, Candidates = candidates, Status = status };
        }).ToArray();
        var requiredMissing = bindings.Where(binding => binding.Status == "REQUIRED_INTEROP_PROVIDER_ABSENT").ToArray();
        if (rejectRequiredMissing && requiredMissing.Length != 0)
            throw new InvalidDataException("REQUIRED_INTEROP_PROVIDER_ABSENT:" + string.Join(',', requiredMissing
                .Select(binding => binding.Import.DeclaringType + "::" + binding.Import.Field + "=" + binding.Import.ImportName)));

        StringBuilder source = new("using System;\n\nnamespace Celeste.Mod;\n\ninternal static class GeneratedAppleEverestModInterop\n{\n    private static int nextOrdinal;\n");
        for (int index = 0; index < registrations.Length; index++)
            source.Append("    private static int registration").Append(index).AppendLine(" = -1;");
        source.AppendLine("\n    internal static bool Register(Type type)\n    {");
        for (int index = 0; index < registrations.Length; index++)
        {
            source.Append("        if (type == typeof(global::").Append(registrations[index].RegisteredType).AppendLine("))")
                .AppendLine("        {")
                .Append("            if (registration").Append(index).AppendLine(" >= 0) return true;")
                .Append("            registration").Append(index).AppendLine(" = nextOrdinal++;")
                .AppendLine("            Refresh();")
                .AppendLine("            return true;")
                .AppendLine("        }");
        }
        source.AppendLine("        return false;\n    }\n\n    private static void Refresh()\n    {");
        foreach (var binding in bindings)
        {
            string field = "global::" + binding.Import.DeclaringType + "." + binding.Import.Field;
            source.Append("        if (registration").Append(binding.Registration).AppendLine(" >= 0)")
                .AppendLine("        {")
                .Append("            ").Append(field).AppendLine(" = null;")
                .AppendLine("            int bestOrdinal = int.MaxValue;");
            foreach (var candidate in binding.Candidates)
            {
                source.Append("            if (registration").Append(candidate.Registration)
                    .Append(" >= 0 && registration").Append(candidate.Registration).AppendLine(" < bestOrdinal)")
                    .AppendLine("            {")
                    .Append("                ").Append(field).Append(" = new global::").Append(binding.Import.DelegateType)
                    .Append("(global::").Append(candidate.Export.DeclaringType).Append('.').Append(candidate.Export.Method).AppendLine(");")
                    .Append("                bestOrdinal = registration").Append(candidate.Registration).AppendLine(";")
                    .AppendLine("            }");
            }
            source.AppendLine("        }");
        }
        source.AppendLine("    }")
            .AppendLine()
            .AppendLine("    internal static void ReportStatus()")
            .AppendLine("    {");
        foreach (var binding in bindings)
        {
            string field = "global::" + binding.Import.DeclaringType + "." + binding.Import.Field;
            source.Append("        global::Celeste.Mod.AppleEverestStaticRuntime.RecordModInteropBinding(\"")
                .Append(Escape(binding.Import.ImportName)).Append("\", ").Append(field).AppendLine(" != null);");
        }
        source.AppendLine("    }")
            .AppendLine("}");

        object stable = new
        {
            schemaVersion = 1,
            behavior = "monomod-dfc30a1506d37fb88a2c2be004f525205f46a24c-static-v1",
            registrations = registrations.Select((registration, index) => new
            {
                index,
                registration.Owner,
                registration.AssemblyName,
                registration.RegisteredType,
                callSites = registration.CallSites,
                exports = registration.Exports.Select(export => new
                {
                    export.MethodOrder,
                    export.DeclaringType,
                    export.Method,
                    export.Names,
                    signature = export.Signature.StableIdentity,
                    export.Signature.Supported,
                    export.Signature.DeferredReason
                }),
                imports = registration.Imports.Select(import => new
                {
                    import.FieldOrder,
                    import.DeclaringType,
                    import.Field,
                    import.ImportName,
                    import.DelegateType,
                    import.DelegateDefinitionType,
                    signature = import.Signature.StableIdentity,
                    import.Signature.Supported,
                    import.Signature.DeferredReason
                })
            }),
            bindings = bindings.Select(binding => new
            {
                importerRegistration = binding.Registration,
                field = binding.Import.DeclaringType + "::" + binding.Import.Field,
                binding.Import.ImportName,
                providerStatus = binding.Status,
                candidates = binding.Candidates.Select(candidate => new
                {
                    providerRegistration = candidate.Registration,
                    methodOrder = candidate.Export.MethodOrder,
                    method = candidate.Export.DeclaringType + "::" + candidate.Export.Method
                })
            })
        };
        string stableJson = JsonSerializer.Serialize(stable, new JsonSerializerOptions { WriteIndented = true });
        string sha = Hashing.BytesSha256(Encoding.UTF8.GetBytes(stableJson + "\n"));
        int resolved = bindings.Count(binding => binding.Candidates.Length != 0);
        return new GeneratedModInteropPlan(source.ToString(), sha, registrations.Length, exports.Count, imports.Count,
            resolved, stable);
    }

    private static bool RequiredProvider(string importName, ResolvedMod consumer)
    {
        int separator = importName.IndexOf('.');
        if (separator <= 0) return false;
        string prefix = importName[..separator];
        return consumer.Metadata.Dependencies.Any(dependency =>
            dependency.Name is not ("Everest" or "EverestCore") &&
            string.Equals(dependency.Name, prefix, StringComparison.Ordinal));
    }

    internal static IEnumerable<string> RequiredPublicTypes(IReadOnlyList<ModInteropRegistrationPlan> plans)
    {
        foreach (ModInteropRegistrationPlan plan in plans)
        {
            yield return plan.RegisteredType;
            foreach (ModInteropExportPlan export in plan.Exports) yield return export.DeclaringType;
            foreach (ModInteropImportPlan import in plan.Imports)
            {
                yield return import.DeclaringType;
                if (!import.DelegateDefinitionType.StartsWith("System.", StringComparison.Ordinal))
                    yield return import.DelegateDefinitionType;
            }
        }
    }

    private static TypeReference? StaticTypeArgument(Instruction[] body, int callIndex)
    {
        int getType = PreviousMeaningful(body, callIndex - 1);
        int token = PreviousMeaningful(body, getType - 1);
        if (getType < 0 || token < 0 || body[token].OpCode != OpCodes.Ldtoken || body[token].Operand is not TypeReference type ||
            body[getType].OpCode is not { Code: Code.Call or Code.Callvirt } ||
            body[getType].Operand is not MethodReference method || method.DeclaringType.FullName != "System.Type" ||
            method.Name != "GetTypeFromHandle") return null;
        return type;
    }

    private static int PreviousMeaningful(Instruction[] body, int index)
    {
        while (index >= 0 && body[index].OpCode == OpCodes.Nop) index--;
        return index;
    }

    private static bool TryDelegateSignature(TypeReference type, out ModInteropSignaturePlan? signature)
    {
        if (type is GenericInstanceType generic && generic.ElementType.Namespace == "System")
        {
            if (generic.ElementType.Name.StartsWith("Action`", StringComparison.Ordinal))
            {
                signature = Signature(type.Module.TypeSystem.Void, generic.GenericArguments.Select(PlainParameter));
                return true;
            }
            if (generic.ElementType.Name.StartsWith("Func`", StringComparison.Ordinal) && generic.GenericArguments.Count >= 1)
            {
                signature = Signature(generic.GenericArguments[^1], generic.GenericArguments.Take(generic.GenericArguments.Count - 1).Select(PlainParameter));
                return true;
            }
        }
        if (type.FullName == "System.Action")
        {
            signature = Signature(type.Module.TypeSystem.Void, []);
            return true;
        }
        TypeDefinition? definition;
        try { definition = type.Resolve(); }
        catch (ResolutionException) { definition = null; }
        if (definition?.BaseType?.FullName != "System.MulticastDelegate")
        {
            signature = null;
            return false;
        }
        MethodDefinition? invoke = definition.Methods.SingleOrDefault(method => method.Name == "Invoke");
        if (invoke == null)
        {
            signature = new ModInteropSignaturePlan("", "", [], [], false, "DEFERRED_DELEGATE_WITHOUT_INVOKE");
            return true;
        }
        GenericInstanceType? closedDelegate = type as GenericInstanceType;
        signature = Signature(CloseDelegateType(invoke.ReturnType, closedDelegate),
            invoke.Parameters.Select(parameter => Parameter(parameter, closedDelegate)));
        return true;
    }

    private static ModInteropSignaturePlan MethodSignature(MethodDefinition method)
    {
        if (method.HasGenericParameters)
            return new ModInteropSignaturePlan(TypeKey(method.ReturnType), CSharpType(method.ReturnType),
                AssignableBaseTypeKeys(method.ReturnType), method.Parameters.Select(Parameter).ToArray(), false,
                "DEFERRED_OPEN_GENERIC_MODINTEROP_METHOD");
        return Signature(method.ReturnType, method.Parameters.Select(Parameter));
    }

    private static ModInteropSignaturePlan Signature(TypeReference returnType, IEnumerable<ModInteropParameterPlan> parameters)
    {
        ModInteropParameterPlan[] values = parameters.ToArray();
        string? deferred = UnsupportedType(returnType) ?? values.Select(value => value.TypeKey)
            .FirstOrDefault(value => value.Contains("!open", StringComparison.Ordinal));
        return new ModInteropSignaturePlan(TypeKey(returnType), CSharpType(returnType),
            AssignableBaseTypeKeys(returnType), values, deferred == null,
            deferred == null ? null : deferred.StartsWith("DEFERRED_", StringComparison.Ordinal) ? deferred : "DEFERRED_OPEN_GENERIC_MODINTEROP_SIGNATURE");
    }

    private static ModInteropParameterPlan PlainParameter(TypeReference type) =>
        new(TypeKey(type), CSharpType(type), "value", AssignableBaseTypeKeys(type));

    private static ModInteropParameterPlan Parameter(ParameterDefinition parameter) => Parameter(parameter, null);

    private static ModInteropParameterPlan Parameter(ParameterDefinition parameter, GenericInstanceType? closedDelegate)
    {
        TypeReference type = CloseDelegateType(parameter.ParameterType, closedDelegate);
        string modifier = "value";
        if (type is ByReferenceType reference)
        {
            type = reference.ElementType;
            modifier = parameter.IsOut ? "out" : parameter.IsIn ? "in" : "ref";
        }
        return new ModInteropParameterPlan(TypeKey(type), CSharpType(type), modifier, AssignableBaseTypeKeys(type));
    }

    private static TypeReference CloseDelegateType(TypeReference type, GenericInstanceType? closedDelegate)
    {
        if (closedDelegate == null) return type;
        if (type is GenericParameter parameter && parameter.Type == GenericParameterType.Type &&
            parameter.Position >= 0 && parameter.Position < closedDelegate.GenericArguments.Count)
            return closedDelegate.GenericArguments[parameter.Position];
        if (type is ByReferenceType reference)
            return new ByReferenceType(CloseDelegateType(reference.ElementType, closedDelegate));
        if (type is ArrayType array)
            return new ArrayType(CloseDelegateType(array.ElementType, closedDelegate), array.Rank);
        if (type is PointerType pointer)
            return new PointerType(CloseDelegateType(pointer.ElementType, closedDelegate));
        if (type is GenericInstanceType generic)
        {
            GenericInstanceType closed = new(CloseDelegateType(generic.ElementType, closedDelegate));
            foreach (TypeReference argument in generic.GenericArguments)
                closed.GenericArguments.Add(CloseDelegateType(argument, closedDelegate));
            return closed;
        }
        return type;
    }

    private static bool Compatible(ModInteropSignaturePlan import, ModInteropSignaturePlan export) =>
        import.Supported && export.Supported &&
        (import.ReturnTypeKey == export.ReturnTypeKey ||
         export.ReturnAssignableBaseTypeKeys.Contains(import.ReturnTypeKey, StringComparer.Ordinal)) &&
        import.Parameters.Count == export.Parameters.Count && import.Parameters.Zip(export.Parameters)
            .All(pair => pair.First.Modifier == pair.Second.Modifier &&
                         (pair.First.TypeKey == pair.Second.TypeKey || pair.First.Modifier == "value" &&
                          pair.First.AssignableBaseTypeKeys.Contains(pair.Second.TypeKey, StringComparer.Ordinal)));

    private static string[] AssignableBaseTypeKeys(TypeReference type)
    {
        string own = TypeKey(type);
        List<string> values = [own];
        if (type is ByReferenceType or GenericParameter or PointerType or FunctionPointerType ||
            type.MetadataType == MetadataType.Void) return values.ToArray();
        if (type.FullName == "System.String") values.Add(TypeKey(type.Module.TypeSystem.Object));
        if (type is GenericInstanceType) return values.Distinct(StringComparer.Ordinal).ToArray();
        try
        {
            TypeDefinition? definition = type.Resolve();
            TypeReference? current = definition?.BaseType;
            while (current != null)
            {
                values.Add(TypeKey(current));
                current = current.Resolve()?.BaseType;
            }
            if (definition != null)
                values.AddRange(definition.Interfaces.Select(value => TypeKey(value.InterfaceType)));
        }
        catch (ResolutionException)
        {
            // Exact matching remains available when an unrelated metadata
            // dependency is absent from the bounded host resolver.
        }
        return values.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string? UnsupportedType(TypeReference type)
    {
        TypeReference element = type is ByReferenceType byReference ? byReference.ElementType : type;
        if (element is GenericParameter) return "DEFERRED_OPEN_GENERIC_MODINTEROP_SIGNATURE";
        if (element is PointerType or FunctionPointerType) return "DEFERRED_UNSAFE_MODINTEROP_SIGNATURE";
        if (element is GenericInstanceType generic)
            return generic.GenericArguments.Select(UnsupportedType).FirstOrDefault(value => value != null);
        if (element is ArrayType array) return UnsupportedType(array.ElementType);
        return null;
    }

    private static string TypeKey(TypeReference type)
    {
        if (type is ByReferenceType reference) return TypeKey(reference.ElementType);
        if (type is ArrayType array) return TypeKey(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]";
        if (type is GenericParameter) return "!open:" + type.Name;
        if (type is GenericInstanceType generic)
            return TypeKey(generic.ElementType) + "<" + string.Join(",", generic.GenericArguments.Select(TypeKey)) + ">";
        string scope = type.Namespace == "System" || type.Namespace.StartsWith("System.", StringComparison.Ordinal)
            ? "system"
            : type.GetElementType().Scope switch
        {
            AssemblyNameReference assemblyReference => assemblyReference.Name,
            ModuleDefinition module => module.Assembly.Name.Name,
            _ => ""
        };
        return type.FullName.Replace('/', '.') + "@" + scope;
    }

    internal static string CSharpType(TypeReference type)
    {
        if (type is ByReferenceType reference) return CSharpType(reference.ElementType);
        if (type is ArrayType array) return CSharpType(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]";
        if (type is GenericInstanceType generic)
            return CSharpTypeName(generic.ElementType).Split('`')[0] + "<" + string.Join(", ", generic.GenericArguments.Select(CSharpType)) + ">";
        return type.FullName switch
        {
            "System.Void" => "void",
            "System.Boolean" => "bool",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.Int32" => "int",
            "System.UInt32" => "uint",
            "System.Int64" => "long",
            "System.UInt64" => "ulong",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Decimal" => "decimal",
            "System.Char" => "char",
            "System.String" => "string",
            "System.Object" => "object",
            _ => CSharpTypeName(type)
        };
    }

    private static string CSharpTypeName(TypeReference type) => type.FullName.Replace('/', '.');

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string AttributeName(CustomAttribute attribute, string expected, string owner)
    {
        if (attribute.ConstructorArguments.Count != 1 || attribute.ConstructorArguments[0].Value is not string value ||
            value.Length > 512 || value.Any(char.IsControl))
            throw new InvalidDataException($"invalid {expected} on {owner}");
        return value;
    }

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition root)
    {
        yield return root;
        foreach (TypeDefinition nested in root.NestedTypes.SelectMany(AllTypes)) yield return nested;
    }
}
