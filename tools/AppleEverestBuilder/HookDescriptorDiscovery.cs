using System.Security.Cryptography;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AppleEverestBuilder;

/// <summary>Host-only exact metadata join used to review HookGen breadth.</summary>
internal static class HookDescriptorDiscovery
{
    internal static void WriteApiBreadth(string gamePath, string canonicalPath, string applePath,
        IReadOnlyList<string> helperPaths, string outputPath)
    {
        if (helperPaths.Count == 0) throw new InvalidDataException("at least one helper DLL is required");
        using AssemblyDefinition game = AssemblyDefinition.ReadAssembly(Path.GetFullPath(gamePath));
        using AssemblyDefinition canonical = AssemblyDefinition.ReadAssembly(Path.GetFullPath(canonicalPath));
        using AssemblyDefinition apple = AssemblyDefinition.ReadAssembly(Path.GetFullPath(applePath));
        TypeDefinition[] gameTypes = game.Modules.SelectMany(module => module.Types).SelectMany(Flatten).ToArray();
        TypeDefinition[] canonicalTypes = canonical.Modules.SelectMany(module => module.Types).SelectMany(Flatten).ToArray();
        TypeDefinition[] appleTypes = apple.Modules.SelectMany(module => module.Types).SelectMany(Flatten).ToArray();
        Dictionary<string, MethodDefinition> gameMethods = gameTypes.SelectMany(type => type.Methods)
            .GroupBy(method => method.FullName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, FieldDefinition> gameFields = gameTypes.SelectMany(type => type.Fields)
            .GroupBy(field => field.FullName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, MethodDefinition> canonicalMethods = canonicalTypes.SelectMany(type => type.Methods)
            .GroupBy(method => method.FullName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, FieldDefinition> canonicalFields = canonicalTypes.SelectMany(type => type.Fields)
            .GroupBy(field => field.FullName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, MethodDefinition> appleMethods = appleTypes.SelectMany(type => type.Methods)
            .GroupBy(method => method.FullName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, FieldDefinition> appleFields = appleTypes.SelectMany(type => type.Fields)
            .GroupBy(field => field.FullName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        List<object> references = [];
        foreach (string helperPath in helperPaths.Order(StringComparer.Ordinal))
        {
            using AssemblyDefinition helper = AssemblyDefinition.ReadAssembly(Path.GetFullPath(helperPath));
            foreach (MethodDefinition owner in ReachableInitializationMethods(helper).Where(method => method.HasBody))
            foreach (Instruction instruction in owner.Body.Instructions)
            {
                if (instruction.Operand is MethodReference called &&
                    gameMethods.TryGetValue(called.FullName, out MethodDefinition? patchedMethod))
                {
                    canonicalMethods.TryGetValue(called.FullName, out MethodDefinition? canonicalMethod);
                    appleMethods.TryGetValue(called.FullName, out MethodDefinition? appleMethod);
                    references.Add(new
                    {
                        provider = helper.Name.Name,
                        helperSha256 = Sha(helperPath),
                        owner = owner.FullName,
                        offset = instruction.Offset,
                        kind = "method",
                        member = called.FullName,
                        patchedPublic = patchedMethod.IsPublic,
                        canonicalStatus = canonicalMethod == null ? "MISSING" :
                            canonicalMethod.IsPublic ? "PUBLIC" : "NONPUBLIC",
                        appleStatus = appleMethod == null ? "MISSING" :
                            appleMethod.IsPublic ? "PUBLIC" : "NONPUBLIC"
                    });
                }
                else if (instruction.Operand is FieldReference field &&
                         gameFields.TryGetValue(field.FullName, out FieldDefinition? patchedField))
                {
                    canonicalFields.TryGetValue(field.FullName, out FieldDefinition? canonicalField);
                    appleFields.TryGetValue(field.FullName, out FieldDefinition? appleField);
                    references.Add(new
                    {
                        provider = helper.Name.Name,
                        helperSha256 = Sha(helperPath),
                        owner = owner.FullName,
                        offset = instruction.Offset,
                        kind = "field",
                        member = field.FullName,
                        patchedPublic = patchedField.IsPublic,
                        canonicalStatus = canonicalField == null ? "MISSING" :
                            canonicalField.IsPublic ? "PUBLIC" : "NONPUBLIC",
                        appleStatus = appleField == null ? "MISSING" :
                            appleField.IsPublic ? "PUBLIC" : "NONPUBLIC"
                    });
                }
            }
        }
        var ordered = references.OrderBy(value => JsonSerializer.Serialize(value), StringComparer.Ordinal).ToArray();
        object report = new
        {
            schemaVersion = 1,
            patchedGameSha256 = Sha(gamePath),
            canonicalGameSha256 = Sha(canonicalPath),
            appleGameSha256 = Sha(applePath),
            providerCount = helperPaths.Count,
            referenceSiteCount = ordered.Length,
            references = ordered
        };
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    internal static void Write(string hookGenPath, string gamePath,
        IReadOnlyList<string> helperPaths, string outputPath)
    {
        if (helperPaths.Count == 0) throw new InvalidDataException("at least one helper DLL is required");
        using AssemblyDefinition hookGen = AssemblyDefinition.ReadAssembly(Path.GetFullPath(hookGenPath));
        using AssemblyDefinition game = AssemblyDefinition.ReadAssembly(Path.GetFullPath(gamePath));
        TypeDefinition[] hookTypes = hookGen.Modules.SelectMany(module => module.Types).SelectMany(Flatten).ToArray();
        TypeDefinition[] gameTypes = game.Modules.SelectMany(module => module.Types).SelectMany(Flatten).ToArray();
        List<object> requirements = [];

        foreach (string helperPath in helperPaths.Order(StringComparer.Ordinal))
        {
            using AssemblyDefinition helper = AssemblyDefinition.ReadAssembly(Path.GetFullPath(helperPath));
            MethodDefinition[] reachable = ReachableInitializationMethods(helper);
            var subscriptions = reachable.Where(method => method.HasBody)
                .SelectMany(method => method.Body.Instructions.Select(instruction =>
                    (Owner: method.FullName, Instruction: instruction)))
                .Where(site => site.Instruction.Operand is MethodReference called &&
                    called.DeclaringType.Namespace.StartsWith("On.", StringComparison.Ordinal) &&
                    called.Name.StartsWith("add_", StringComparison.Ordinal))
                .Select(site => new
                {
                    site.Owner,
                    Offset = site.Instruction.Offset,
                    Called = (MethodReference)site.Instruction.Operand
                }).ToArray();
            foreach (var group in subscriptions.GroupBy(site =>
                         site.Called.DeclaringType.FullName + "\0" + site.Called.Name[4..],
                         StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var first = group.First();
                string hookTypeName = first.Called.DeclaringType.FullName;
                string eventName = first.Called.Name[4..];
                object Unresolved(string reason) => new
                {
                    helper = helper.Name.Name,
                    helperSha256 = Sha(helperPath),
                    hookType = hookTypeName,
                    eventName,
                    status = "unresolved",
                    reason,
                    sites = group.OrderBy(site => site.Owner, StringComparer.Ordinal).ThenBy(site => site.Offset)
                        .Select(site => new { owner = site.Owner, offset = site.Offset }).ToArray()
                };
                TypeDefinition[] hookMatches = hookTypes.Where(type => type.FullName == hookTypeName).ToArray();
                if (hookMatches.Length != 1)
                {
                    requirements.Add(Unresolved($"HookGen type match count {hookMatches.Length}"));
                    continue;
                }
                TypeDefinition hookType = hookMatches[0];
                string hookDelegateFullName = first.Called.Parameters.Single().ParameterType.FullName;
                string hookDelegateName = first.Called.Parameters.Single().ParameterType.Name;
                TypeDefinition? hookDelegate = hookType.NestedTypes.SingleOrDefault(type =>
                    type.Name == hookDelegateName);
                if (hookDelegate is null)
                {
                    requirements.Add(Unresolved($"HookGen hook delegate absent: {hookDelegateFullName}"));
                    continue;
                }
                MethodDefinition hookInvoke = hookDelegate.Methods.Single(method => method.Name == "Invoke");
                string origDelegateFullName = hookInvoke.Parameters.First().ParameterType.FullName;
                string origDelegateName = hookInvoke.Parameters.First().ParameterType.Name;
                TypeDefinition? origType = hookType.NestedTypes.SingleOrDefault(type =>
                    type.Name == origDelegateName);
                if (origType is null)
                {
                    requirements.Add(Unresolved($"HookGen orig delegate absent: {origDelegateFullName}"));
                    continue;
                }
                MethodDefinition invoke = origType.Methods.Single(method => method.Name == "Invoke");

                string targetTypeName = hookTypeName[3..];
                TypeDefinition[] targetMatches = gameTypes.Where(type => type.FullName == targetTypeName).ToArray();
                if (targetMatches.Length != 1)
                {
                    requirements.Add(Unresolved($"game type match count {targetMatches.Length}: {targetTypeName}"));
                    continue;
                }
                TypeDefinition targetType = targetMatches[0];
                string targetName = eventName.StartsWith("ctor", StringComparison.Ordinal) ? ".ctor" :
                    eventName.StartsWith("get_", StringComparison.Ordinal) ? eventName :
                    targetType.Methods.Where(method => eventName == method.Name ||
                            eventName.StartsWith(method.Name + "_", StringComparison.Ordinal))
                        .OrderByDescending(method => method.Name.Length).Select(method => method.Name).FirstOrDefault()
                    ?? eventName;
                MethodDefinition[] candidates = targetType.Methods.Where(method => method.Name == targetName &&
                    SameType(method.ReturnType, invoke.ReturnType) &&
                    SameParameters(targetType, method, invoke)).ToArray();
                if (candidates.Length != 1)
                {
                    requirements.Add(Unresolved($"game method match count {candidates.Length}"));
                    continue;
                }
                MethodDefinition target = candidates[0];
                requirements.Add(new
                {
                    helper = helper.Name.Name,
                    helperSha256 = Sha(helperPath),
                    hookType = hookTypeName,
                    eventName,
                    status = "resolved",
                    origDelegate = origType.Name,
                    hookDelegate = hookDelegate.Name,
                    sites = group.OrderBy(site => site.Owner, StringComparer.Ordinal).ThenBy(site => site.Offset)
                        .Select(site => new { owner = site.Owner, offset = site.Offset }).ToArray(),
                    targetType = targetType.FullName,
                    targetMethod = target.FullName,
                    targetMetadataToken = target.MetadataToken.ToInt32(),
                    targetIsStatic = target.IsStatic,
                    sourceKind = target.IsGetter ? "property-getter" : "method",
                    returnType = target.ReturnType.FullName,
                    parameters = target.Parameters.Select(parameter => new
                    {
                        type = parameter.ParameterType.FullName,
                        parameter.Name,
                        hasDefault = parameter.HasConstant,
                        defaultValue = parameter.HasConstant ? parameter.Constant : null
                    }).ToArray()
                });
            }
        }
        object report = new
        {
            schemaVersion = 1,
            hookGenSha256 = Sha(hookGenPath),
            gameSha256 = Sha(gamePath),
            requirementCount = requirements.Count,
            requirements
        };
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static MethodDefinition[] ReachableInitializationMethods(AssemblyDefinition assembly)
    {
        TypeDefinition[] types = assembly.Modules.SelectMany(module => module.Types).SelectMany(Flatten).ToArray();
        Dictionary<string, TypeDefinition> typeByName = types.ToDictionary(type => type.FullName,
            StringComparer.Ordinal);
        Dictionary<string, MethodDefinition> methodByName = types.SelectMany(type => type.Methods)
            .GroupBy(method => method.FullName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(method =>
                method.MetadataToken.ToInt32()).First(), StringComparer.Ordinal);

        bool IsModule(TypeDefinition type)
        {
            TypeReference? current = type.BaseType;
            HashSet<string> seen = new(StringComparer.Ordinal);
            while (current != null && seen.Add(current.FullName))
            {
                if (current.FullName == "Celeste.Mod.EverestModule") return true;
                current = typeByName.TryGetValue(current.FullName, out TypeDefinition? local)
                    ? local.BaseType : null;
            }
            return false;
        }

        Queue<MethodDefinition> pending = new();
        HashSet<MethodDefinition> result = [];
        foreach (TypeDefinition module in types.Where(type => !type.IsAbstract && IsModule(type)))
        foreach (MethodDefinition method in module.Methods.Where(method =>
                     method.IsConstructor || method.Name is "Load" or "Initialize" or "LoadContent"))
            pending.Enqueue(method);

        void IncludeTypeInitializer(string typeName)
        {
            if (!typeByName.TryGetValue(typeName, out TypeDefinition? type)) return;
            MethodDefinition? initializer = type.Methods.SingleOrDefault(method => method.IsConstructor && method.IsStatic);
            if (initializer != null && !result.Contains(initializer)) pending.Enqueue(initializer);
        }

        while (pending.TryDequeue(out MethodDefinition? method))
        {
            if (!result.Add(method) || !method.HasBody) continue;
            foreach (Instruction instruction in method.Body.Instructions)
            {
                if (instruction.Operand is MethodReference called &&
                    instruction.OpCode.Code is Code.Call or Code.Callvirt or Code.Newobj)
                {
                    IncludeTypeInitializer(called.DeclaringType.FullName);
                    if (methodByName.TryGetValue(called.FullName, out MethodDefinition? local) &&
                        !result.Contains(local)) pending.Enqueue(local);
                }
                else if (instruction.Operand is FieldReference field)
                {
                    IncludeTypeInitializer(field.DeclaringType.FullName);
                }
            }
        }
        return result.OrderBy(method => method.FullName, StringComparer.Ordinal).ToArray();
    }

    private static bool SameParameters(TypeDefinition targetType, MethodDefinition target, MethodDefinition orig)
    {
        ParameterDefinition[] source = orig.Parameters.ToArray();
        if (!target.IsStatic)
        {
            if (source.Length == 0 || !SameType(source[0].ParameterType, targetType)) return false;
            source = source[1..];
        }
        return source.Length == target.Parameters.Count && source.Zip(target.Parameters)
            .All(pair => SameType(pair.First.ParameterType, pair.Second.ParameterType));
    }

    private static bool SameType(TypeReference left, TypeReference right) =>
        left.FullName == right.FullName ||
        left.FullName.Replace("&", "", StringComparison.Ordinal) ==
        right.FullName.Replace("&", "", StringComparison.Ordinal);

    private static string Sha(string path) => Convert.ToHexString(
        SHA256.HashData(File.ReadAllBytes(Path.GetFullPath(path)))).ToLowerInvariant();

    private static IEnumerable<TypeDefinition> Flatten(TypeDefinition type)
    {
        yield return type;
        foreach (TypeDefinition nested in type.NestedTypes.SelectMany(Flatten)) yield return nested;
    }
}
