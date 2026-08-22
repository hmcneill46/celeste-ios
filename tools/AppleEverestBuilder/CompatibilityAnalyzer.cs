using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AppleEverestBuilder;

internal static class CompatibilityAnalyzer
{
    private static readonly (string Needle, CompatibilityClass Classification)[] SourceRules =
    [
        ("IL.Celeste.", CompatibilityClass.IL_HOOK_DEFERRED),
        ("IL.Monocle.", CompatibilityClass.IL_HOOK_DEFERRED),
        ("new ILHook", CompatibilityClass.IL_HOOK_DEFERRED),
        ("new Hook(", CompatibilityClass.DIRECT_HOOK_DEFERRED),
        ("NativeDetour", CompatibilityClass.NATIVE_UNSUPPORTED),
        ("DllImport", CompatibilityClass.NATIVE_UNSUPPORTED),
        ("LibraryImport", CompatibilityClass.NATIVE_UNSUPPORTED),
        ("NLua", CompatibilityClass.LUA_UNSUPPORTED),
        ("KeraLua", CompatibilityClass.LUA_UNSUPPORTED),
        ("Assembly.Load", CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED),
        ("AssemblyLoadContext", CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED),
        ("DynamicMethod", CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED),
        ("Reflection.Emit", CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED),
        ("System.Reflection.Emit", CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED),
        ("RuntimeDetour", CompatibilityClass.DIRECT_HOOK_DEFERRED),
        ("Process.Start", CompatibilityClass.PLATFORM_UNSUPPORTED),
        ("FileSystemWatcher", CompatibilityClass.PLATFORM_UNSUPPORTED),
        ("ModInterop(", CompatibilityClass.MODINTEROP_DEFERRED)
    ];

    public static ResolvedMod Analyze(ModInput input, EverestYamlEntry metadata) => AnalyzeCore(input, metadata, rejectUnsupported: true);
    public static ResolvedMod Audit(ModInput input, EverestYamlEntry metadata) => AnalyzeCore(input, metadata, rejectUnsupported: false);

    private static ResolvedMod AnalyzeCore(ModInput input, EverestYamlEntry metadata, bool rejectUnsupported)
    {
        IReadOnlyList<FrozenIlTransformPlan> frozenIl = StaticIlFreeze.Resolve(input, metadata);
        StaticAotCompatibilityPlan? staticAot = StaticAotCompatibility.Resolve(input, metadata);
        List<string> managed = input.Files.Where(file => IsManaged(file.Path)).Select(file => file.Path).ToList();
        List<string> content = input.Files.Where(file => IsContent(file.Path)).Select(file => file.Path).ToList();
        SortedSet<string> mechanisms = new(StringComparer.Ordinal);
        bool hookGenRegistration = false;
        CompatibilityClass classification = string.IsNullOrWhiteSpace(metadata.DLL)
            ? CompatibilityClass.CONTENT_ONLY
            : CompatibilityClass.STATIC_MODULE;

        // Mod-supplied FMOD banks require a separate deterministic bank-loading
        // architecture.  Treat their mere presence as a product boundary: an
        // otherwise compatible helper must not silently ship a partial feature
        // set whose custom events can never be resolved on Apple devices.
        foreach (string bank in input.Files.Where(file =>
                     file.Path.EndsWith(".bank", StringComparison.OrdinalIgnoreCase))
                 .Select(file => file.Path))
            Record("custom-fmod-bank:" + bank, CompatibilityClass.CUSTOM_AUDIO_UNSUPPORTED);

        AppleStaticDeclaration? declaration = null;
        string? declaredAssembly = null;
        SortedSet<string> managedDetourTargets = new(StringComparer.Ordinal);
        List<DirectManagedHookPlan> directManagedHooks = [];
        List<ModInteropRegistrationPlan> modInteropRegistrations = [];
        string? normalizedDeclaredEntry = metadata.DLL?.Replace('\\', '/');
        bool declaredBinaryModule = normalizedDeclaredEntry?.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) == true;
        foreach (string relative in managed)
        {
            string path = Path.Combine(input.StagingRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (relative.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                if (declaredBinaryModule)
                {
                    mechanisms.Add(relative + ":ignored-incidental-managed-source");
                    continue;
                }
                string source = File.ReadAllText(path, new UTF8Encoding(false, true));
                foreach ((string needle, CompatibilityClass detected) in SourceRules)
                    if (source.Contains(needle, StringComparison.Ordinal)) Record($"{relative}:{needle}", detected);
                if (source.Contains("Everest.Events.", StringComparison.Ordinal)) Record($"{relative}:Everest.Events", CompatibilityClass.NORMAL_EVENT);
                AnalyzeSourceHookReferences(relative, source, managedDetourTargets,
                    (mechanism, detected) => Record(mechanism, detected));
            }
            else if (relative.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                if (declaredBinaryModule && !string.Equals(relative, normalizedDeclaredEntry, StringComparison.Ordinal))
                {
                    mechanisms.Add(relative + ":ignored-nonauthoritative-managed-assembly");
                    continue;
                }
                List<ModInteropRegistrationPlan> assemblyModInterop = [];
                AnalyzeAssembly(path, metadata.Name, managedDetourTargets, directManagedHooks, assemblyModInterop,
                    (mechanism, detected) => Record($"{relative}:{mechanism}", detected), rejectUnsupported,
                    frozenIl.Count > 0 && string.Equals(relative, normalizedDeclaredEntry, StringComparison.Ordinal),
                    frozenIl.Any(plan => plan.Mechanism == "DIRECT_ILHOOK") &&
                    string.Equals(relative, normalizedDeclaredEntry, StringComparison.Ordinal),
                    string.Equals(relative, normalizedDeclaredEntry, StringComparison.Ordinal) ? staticAot : null);
                if (assemblyModInterop.Count > 0 && !string.Equals(relative, normalizedDeclaredEntry, StringComparison.Ordinal))
                {
                    Record($"{relative}:DEFERRED_UNLINKED_MODINTEROP_ASSEMBLY", CompatibilityClass.MODINTEROP_DEFERRED);
                }
                else
                {
                    modInteropRegistrations.AddRange(assemblyModInterop);
                }
            }
        }

        if (directManagedHooks.Count > 0 && hookGenRegistration &&
            (classification == CompatibilityClass.DIRECT_HOOK_SUPPORTED || classification == CompatibilityClass.ON_HOOK_SUPPORTED))
            Record("HookGen+direct-Hook:shared-static-managed-detour-chain", CompatibilityClass.MIXED_MANAGED_DETOURS_SUPPORTED);

        if (!string.IsNullOrWhiteSpace(metadata.DLL))
        {
            string normalized = metadata.DLL!.Replace('\\', '/');
            if (!input.Files.Any(file => string.Equals(file.Path, normalized, StringComparison.Ordinal)))
                throw new InvalidDataException($"declared DLL/source entry is missing for {metadata.Name}: {metadata.DLL}");
            if (normalized.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                declaredAssembly = normalized;
                declaration = AssemblyFreezer.InspectDeclaration(Path.Combine(input.StagingRoot,
                    normalized.Replace('/', Path.DirectorySeparatorChar)), metadata.Name,
                    allowNonPublicCustomFactories: frozenIl.Count > 0 ||
                    staticAot?.AllowNonPublicCustomFactories == true);
            }
            else
            {
                string declarationPath = Path.Combine(input.StagingRoot, "apple-static.json");
                if (!File.Exists(declarationPath))
                    throw new InvalidDataException($"{metadata.Name} source module requires root apple-static.json for a closed module factory");
                declaration = AssemblyFreezer.ReadSourceDeclaration(declarationPath, metadata.Name);
            }
        }

        if (frozenIl.Count > 0)
            Record("hash-locked-static-il-event-freeze:" + StaticIlFreeze.PlanSha256(frozenIl),
                frozenIl.Any(plan => plan.Mechanism == "DIRECT_ILHOOK")
                    ? CompatibilityClass.STATIC_DIRECT_ILHOOK_FREEZE
                    : frozenIl.GroupBy(plan => plan.TargetMethod, StringComparer.Ordinal).Any(group => group.Count() > 1)
                    ? CompatibilityClass.STATIC_IL_EVENT_SEQUENCE
                    : CompatibilityClass.STATIC_IL_EVENT_FREEZE);

        if (staticAot != null)
            Record("hash-locked-static-aot-compatibility:" + staticAot.Id,
                CompatibilityClass.HASH_LOCKED_STATIC_AOT_COMPATIBILITY);

        if (rejectUnsupported && classification is (CompatibilityClass.MODINTEROP_DEFERRED or CompatibilityClass.ON_HOOK_DEFERRED or CompatibilityClass.IL_HOOK_DEFERRED or CompatibilityClass.DIRECT_HOOK_DEFERRED or
            CompatibilityClass.DYNAMIC_TARGET_DEFERRED or CompatibilityClass.DYNAMIC_DETOUR_DEFERRED or CompatibilityClass.DETOUR_CONFIG_DEFERRED or
            CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED or CompatibilityClass.CUSTOM_AUDIO_UNSUPPORTED or
            CompatibilityClass.NATIVE_UNSUPPORTED or
            CompatibilityClass.LUA_UNSUPPORTED or CompatibilityClass.PLATFORM_UNSUPPORTED))
            throw new InvalidDataException($"{metadata.Name} rejected before AOT: {classification}; mechanisms={string.Join(',', mechanisms)}");

        return new ResolvedMod
        {
            Metadata = metadata,
            Input = input,
            Classification = classification,
            Mechanisms = mechanisms,
            ManagedFiles = managed,
            ContentFiles = content,
            Declaration = declaration,
            DeclaredAssemblyPath = declaredAssembly,
            ManagedDetourTargets = managedDetourTargets,
            DirectManagedHooks = directManagedHooks,
            ModInteropRegistrations = modInteropRegistrations,
            FrozenIlTransforms = frozenIl,
            StaticAotCompatibility = staticAot
        };

        void Record(string mechanism, CompatibilityClass detected)
        {
            mechanisms.Add(mechanism);
            if (mechanism.Contains("typed-hook:", StringComparison.Ordinal)) hookGenRegistration = true;
            classification = MoreRestrictive(classification, detected);
        }
    }

    private static void AnalyzeSourceHookReferences(
        string relative,
        string source,
        SortedSet<string> targets,
        Action<string, CompatibilityClass> record)
    {
        foreach (Match match in Regex.Matches(source,
                     @"\b(On\.(?:Celeste|Monocle)\.[A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)",
                     RegexOptions.CultureInvariant))
        {
            string hookType = match.Groups[1].Value;
            string member = match.Groups[2].Value;
            string eventName = member.StartsWith("orig_", StringComparison.Ordinal) ? member[5..] :
                member.StartsWith("hook_", StringComparison.Ordinal) ? member[5..] : member;
            try
            {
                ManagedDetourTarget target = ManagedDetourCatalog.RequireByHookType(hookType, eventName);
                targets.Add(target.Id);
                record($"{relative}:typed-hook:{target.Id}", CompatibilityClass.ON_HOOK_SUPPORTED);
            }
            catch (InvalidDataException)
            {
                record($"{relative}:unsupported typed hook:{hookType}.{member}", CompatibilityClass.ON_HOOK_DEFERRED);
            }
        }
    }

    private static void AnalyzeAssembly(
        string path,
        string owner,
        SortedSet<string> targets,
        List<DirectManagedHookPlan> directHooks,
        List<ModInteropRegistrationPlan> modInteropRegistrations,
        Action<string, CompatibilityClass> record,
        bool rejectUnsupported,
        bool registeredStaticIl,
        bool registeredDirectIl,
        StaticAotCompatibilityPlan? staticAot)
    {
        try
        {
            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadSymbols = false });
            IReadOnlyList<ModInteropRegistrationPlan> interop = ModInteropPlanner.Analyze(assembly, owner,
                rejectUnsupported, mechanism => record(mechanism,
                    mechanism.StartsWith("DEFERRED_", StringComparison.Ordinal)
                        ? CompatibilityClass.MODINTEROP_DEFERRED
                        : CompatibilityClass.MODINTEROP_STATIC_SUPPORTED));
            if (interop.Count > 0)
            {
                modInteropRegistrations.AddRange(interop);
                foreach (ModInteropRegistrationPlan registration in interop)
                    record($"static-modinterop:{registration.RegisteredType}", CompatibilityClass.MODINTEROP_STATIC_SUPPORTED);
            }
            TypeReference[] residualMonoModUtils = assembly.MainModule.GetTypeReferences().Where(type =>
                type.Scope is AssemblyNameReference reference && reference.Name == "MonoMod.Utils" &&
                type.Namespace != "MonoMod.ModInterop" &&
                !(registeredStaticIl && type.FullName.StartsWith("MonoMod.Cil.", StringComparison.Ordinal)) &&
                !(registeredDirectIl && type.FullName == "MonoMod.Utils.Extensions") &&
                !StaticAotCompatibility.AllowsMonoModType(staticAot, type.FullName) &&
                type.FullName != "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute").ToArray();
            if (residualMonoModUtils.Length != 0)
                record("DEFERRED_MONOMOD_UTILS_SURFACE:" + string.Join(',', residualMonoModUtils
                    .Select(type => type.FullName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(12)),
                    CompatibilityClass.MODINTEROP_DEFERRED);
            foreach (TypeReference type in assembly.MainModule.GetTypeReferences()
                         .Where(type => type.FullName.StartsWith("IL.", StringComparison.Ordinal)))
            {
                string hookType = type.FullName;
                record($"il-hook:{hookType}", registeredStaticIl
                    ? CompatibilityClass.STATIC_IL_EVENT_FREEZE
                    : CompatibilityClass.IL_HOOK_DEFERRED);
            }
            foreach (TypeReference type in assembly.MainModule.GetTypeReferences()
                         .Where(type => type.Namespace == "MonoMod.RuntimeDetour"))
            {
                switch (type.Name)
                {
                    case "Hook":
                        break;
                    case "ILHook":
                        record($"runtime-detour-type:{type.FullName}", registeredDirectIl
                            ? CompatibilityClass.STATIC_DIRECT_ILHOOK_FREEZE
                            : CompatibilityClass.IL_HOOK_DEFERRED);
                        break;
                    case "NativeDetour":
                        record($"runtime-detour-type:{type.FullName}", CompatibilityClass.NATIVE_UNSUPPORTED);
                        break;
                    case "DetourConfig":
                    case "DetourContext":
                        record($"DEFERRED_DETOUR_CONFIG:{type.FullName}", CompatibilityClass.DETOUR_CONFIG_DEFERRED);
                        break;
                    default:
                        record($"unsupported RuntimeDetour type:{type.FullName}", CompatibilityClass.DIRECT_HOOK_DEFERRED);
                        break;
                }
            }
            bool runtimeDetourReference = false;
            foreach (AssemblyNameReference reference in assembly.MainModule.AssemblyReferences)
            {
                string name = reference.Name;
                if (name is "NLua" or "KeraLua") record(name, CompatibilityClass.LUA_UNSUPPORTED);
                else if (name == "MonoMod.RuntimeDetour") runtimeDetourReference = true;
            }
            int directConstructorCount = 0;
            foreach (TypeDefinition type in assembly.MainModule.Types.SelectMany(AllTypes))
            {
                foreach (PInvokeInfo? pinvoke in type.Methods.Select(method => method.PInvokeInfo).Where(value => value != null))
                    record($"PInvoke:{pinvoke!.Module.Name}", CompatibilityClass.NATIVE_UNSUPPORTED);
                foreach (MethodDefinition method in type.Methods.Where(method => method.HasBody))
                foreach (Instruction instruction in method.Body.Instructions)
                {
                    if (instruction.Operand is not MethodReference called) continue;
                    string full = called.DeclaringType.FullName + "::" + called.Name;
                    if (called.DeclaringType.Namespace.StartsWith("On.", StringComparison.Ordinal) &&
                        (called.Name.StartsWith("add_", StringComparison.Ordinal) || called.Name.StartsWith("remove_", StringComparison.Ordinal)))
                    {
                        string eventName = called.Name[(called.Name.StartsWith("add_", StringComparison.Ordinal) ? 4 : 7)..];
                        try
                        {
                            ManagedDetourTarget target = ManagedDetourCatalog.RequireByHookType(
                                called.DeclaringType.Namespace + "." + called.DeclaringType.Name, eventName);
                            targets.Add(target.Id);
                            record($"typed-hook:{target.Id}", CompatibilityClass.ON_HOOK_SUPPORTED);
                        }
                        catch (InvalidDataException)
                        {
                            record($"unsupported typed hook:{called.DeclaringType.FullName}.{eventName}", CompatibilityClass.ON_HOOK_DEFERRED);
                        }
                        continue;
                    }
                    if (full.Contains("Assembly::Load", StringComparison.Ordinal) || full.Contains("AssemblyLoadContext", StringComparison.Ordinal) ||
                        full.Contains("DynamicMethod", StringComparison.Ordinal) || full.Contains("System.Reflection.Emit", StringComparison.Ordinal))
                        record(full, StaticAotCompatibility.AllowsDynamicMethod(staticAot)
                            ? CompatibilityClass.HASH_LOCKED_STATIC_AOT_COMPATIBILITY
                            : CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED);
                    else if (full.Contains("NativeDetour", StringComparison.Ordinal)) record(full, CompatibilityClass.NATIVE_UNSUPPORTED);
                    else if (full.Contains("ILHook", StringComparison.Ordinal)) record(full, registeredDirectIl
                        ? CompatibilityClass.STATIC_DIRECT_ILHOOK_FREEZE
                        : CompatibilityClass.IL_HOOK_DEFERRED);
                    else if (called.DeclaringType.FullName == "MonoMod.RuntimeDetour.Hook" && called.Name == ".ctor")
                    {
                        directConstructorCount++;
                        if (registeredDirectIl && owner == StaticIlFreeze.CaeruleaName &&
                            method.FullName == "System.Void Celeste.Mod.CaeruleaHelper.Entities.CustomStarJumpBlock::Load()")
                        {
                            record("static-optional-DashlessHelper-fallback:celeste-star-jump-block-open",
                                CompatibilityClass.ON_HOOK_SUPPORTED);
                            continue;
                        }
                        try
                        {
                            DirectManagedHookPlan plan = ResolveDirectHookPlan(assembly, method, instruction, owner);
                            directHooks.Add(plan);
                            targets.Add(plan.TargetId);
                            record($"direct-managed-hook:{plan.TargetId}:{plan.DetourType}::{plan.DetourMethod}", CompatibilityClass.DIRECT_HOOK_SUPPORTED);
                        }
                        catch (InvalidDataException exception)
                        {
                            if (rejectUnsupported) throw;
                            record(exception.Message, DirectFailureClass(exception.Message));
                        }
                    }
                    else if (called.DeclaringType.FullName == "MonoMod.RuntimeDetour.ILHook")
                        record(full, registeredDirectIl ? CompatibilityClass.STATIC_DIRECT_ILHOOK_FREEZE :
                            CompatibilityClass.IL_HOOK_DEFERRED);
                    else if (called.DeclaringType.FullName.Contains("NativeDetour", StringComparison.Ordinal))
                        record(full, CompatibilityClass.NATIVE_UNSUPPORTED);
                    else if (called.DeclaringType.FullName == "MonoMod.RuntimeDetour.Hook" &&
                             called.Name is not ("Dispose" or "Apply" or "Undo" or "get_IsApplied" or "get_IsValid"))
                        record($"unsupported direct Hook member:{called.FullName}", CompatibilityClass.DIRECT_HOOK_DEFERRED);
                }
            }
            if (runtimeDetourReference && directConstructorCount == 0)
                record("MonoMod.RuntimeDetour:unresolved-or-unsupported-surface", CompatibilityClass.DIRECT_HOOK_DEFERRED);
        }
        catch (BadImageFormatException)
        {
            throw new InvalidDataException($"declared managed assembly is not valid CLI metadata: {Path.GetFileName(path)}");
        }
    }

    private static DirectManagedHookPlan ResolveDirectHookPlan(
        AssemblyDefinition assembly,
        MethodDefinition containingMethod,
        Instruction constructor,
        string owner)
    {
        MethodReference called = (MethodReference)constructor.Operand;
        string signature = called.FullName;
        if (called.Parameters.Count != 2)
            throw new InvalidDataException($"DEFERRED_DETOUR_CONFIG: {owner} {containingMethod.FullName} uses {signature}");
        if (called.Parameters[0].ParameterType.FullName != "System.Reflection.MethodBase" ||
            called.Parameters[1].ParameterType.FullName != "System.Reflection.MethodInfo")
            throw new InvalidDataException($"DEFERRED_DIRECT_HOOK_CONSTRUCTOR: {owner} {containingMethod.FullName} uses {signature}");

        Instruction[] body = containingMethod.Body.Instructions.ToArray();
        int index = Array.IndexOf(body, constructor);
        TypeReference targetType;
        TypeReference detourType;
        string targetName;
        string detourName;
        int expressionInstructionCount;
        bool propertyGetter = index >= 12 && body[index - 12].OpCode == OpCodes.Ldtoken &&
            body[index - 12].Operand is TypeReference && body[index - 10].OpCode == OpCodes.Ldstr &&
            body[index - 10].Operand is string && body[index - 8].Operand is MethodReference getProperty &&
            getProperty.DeclaringType.FullName == "System.Type" && getProperty.Name == "GetProperty" &&
            body[index - 6].Operand is MethodReference getGetter && getGetter.DeclaringType.FullName ==
            "System.Reflection.PropertyInfo" && getGetter.Name == "GetGetMethod" &&
            body[index - 5].OpCode == OpCodes.Ldtoken && body[index - 5].Operand is TypeReference &&
            body[index - 3].OpCode == OpCodes.Ldstr && body[index - 3].Operand is string &&
            body[index - 1].Operand is MethodReference propertyDetourLookup &&
            IsGetMethod(propertyDetourLookup, "System.String", "System.Reflection.BindingFlags");
        if (propertyGetter)
        {
            targetType = (TypeReference)body[index - 12].Operand;
            targetName = "get_" + (string)body[index - 10].Operand;
            detourType = (TypeReference)body[index - 5].Operand;
            detourName = (string)body[index - 3].Operand;
            expressionInstructionCount = 12;
        }
        else
        {
            if (index < 9 || body[index - 9].OpCode != OpCodes.Ldtoken || body[index - 9].Operand is not TypeReference methodTargetType ||
                body[index - 7].OpCode != OpCodes.Ldstr || body[index - 7].Operand is not string methodTargetName ||
                body[index - 6].OpCode != OpCodes.Ldc_I4_S || Convert.ToInt32(body[index - 6].Operand) !=
                    (int)(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public) ||
                body[index - 4].OpCode != OpCodes.Ldtoken || body[index - 4].Operand is not TypeReference methodDetourType ||
                body[index - 2].OpCode != OpCodes.Ldstr || body[index - 2].Operand is not string methodDetourName ||
                body[index - 8].Operand is not MethodReference targetGetType || targetGetType.Name != "GetTypeFromHandle" ||
                body[index - 5].Operand is not MethodReference targetGetMethod ||
                    !IsGetMethod(targetGetMethod, "System.String", "System.Reflection.BindingFlags") ||
                body[index - 3].Operand is not MethodReference detourGetType || detourGetType.Name != "GetTypeFromHandle" ||
                body[index - 1].Operand is not MethodReference detourGetMethod ||
                    !IsGetMethod(detourGetMethod, "System.String"))
                throw new InvalidDataException($"DEFERRED_DYNAMIC_TARGET: {owner} {containingMethod.FullName} has an unresolved direct Hook expression");
            targetType = methodTargetType;
            targetName = methodTargetName;
            detourType = methodDetourType;
            detourName = methodDetourName;
            expressionInstructionCount = 9;
        }

        ManagedDetourTarget target;
        try { target = ManagedDetourCatalog.RequireByDirectAlias(targetType.FullName, targetName); }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException(
                $"DEFERRED_DYNAMIC_TARGET: {owner} {containingMethod.FullName} target {targetType.FullName}::{targetName} is not authorized", exception);
        }
        TypeDefinition? detourDefinition = assembly.MainModule.Types.SelectMany(AllTypes)
            .SingleOrDefault(type => type.FullName == detourType.FullName);
        MethodDefinition[] methods = detourDefinition?.Methods.Where(method => method.Name == detourName).ToArray() ?? [];
        if (methods.Length != 1)
            throw new InvalidDataException($"DEFERRED_DYNAMIC_DETOUR: {owner} {detourType.FullName}::{detourName} resolved {methods.Length} methods");
        MethodDefinition detour = methods[0];
        bool customOriginalDelegate = false;
        if (propertyGetter && detour.Parameters.Count > 0)
        {
            TypeDefinition? originalDelegate = detour.Parameters[0].ParameterType.Resolve();
            MethodDefinition? invoke = originalDelegate?.Methods.SingleOrDefault(method => method.Name == "Invoke");
            customOriginalDelegate = originalDelegate?.BaseType?.FullName == "System.MulticastDelegate" &&
                invoke != null && invoke.ReturnType.FullName == "System.Boolean" && invoke.Parameters.Count == 1 &&
                invoke.Parameters[0].ParameterType.FullName == "Celeste.Player";
            if (!customOriginalDelegate)
                throw new InvalidDataException($"DEFERRED_DIRECT_HOOK_SIGNATURE: {owner} {detour.FullName}");
        }
        string capture = detour.IsStatic ? "STATIC" :
            Inherits(detourDefinition!, "Celeste.Mod.EverestModule") ? "MODULE_INSTANCE" : "RUNTIME_DYNAMIC";
        if (capture == "RUNTIME_DYNAMIC")
            throw new InvalidDataException($"DEFERRED_DYNAMIC_CAPTURE: {owner} {detour.FullName}");
        string planId = owner + ":" + target.Id + ":" + detourType.FullName.Replace('/', '.') + "::" + detourName;
        return new DirectManagedHookPlan(
            planId,
            owner,
            assembly.Name.Name,
            containingMethod.FullName,
            constructor.Offset,
            target.Id,
            targetType.FullName.Replace('/', '.') + "::" + targetName,
            detourType.FullName.Replace('/', '.'),
            detourName,
            detour.IsStatic,
            CSharpType(detour.ReturnType),
            detour.Parameters.Select(parameter => CSharpType(parameter.ParameterType)).ToArray(),
            capture,
            signature,
            expressionInstructionCount,
            customOriginalDelegate);
    }

    private static bool IsGetMethod(MethodReference method, params string[] parameterTypes) =>
        method.DeclaringType.FullName == "System.Type" && method.Name == "GetMethod" &&
        method.Parameters.Select(parameter => parameter.ParameterType.FullName).SequenceEqual(parameterTypes);

    private static string CSharpType(TypeReference type)
    {
        if (type is GenericInstanceType generic)
        {
            string root = generic.ElementType.FullName.Split('`')[0].Replace('/', '.');
            return "global::" + root + "<" + string.Join(", ", generic.GenericArguments.Select(CSharpType)) + ">";
        }
        if (type is ByReferenceType byReference) return CSharpType(byReference.ElementType);
        return type.FullName switch
        {
            "System.Void" => "void",
            "System.Boolean" => "bool",
            "System.Int32" => "int",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.String" => "string",
            _ => "global::" + type.FullName.Replace('/', '.')
        };
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

    private static CompatibilityClass DirectFailureClass(string message) =>
        message.StartsWith("DEFERRED_DYNAMIC_TARGET", StringComparison.Ordinal) ? CompatibilityClass.DYNAMIC_TARGET_DEFERRED :
        message.StartsWith("DEFERRED_DYNAMIC_DETOUR", StringComparison.Ordinal) ||
        message.StartsWith("DEFERRED_DYNAMIC_CAPTURE", StringComparison.Ordinal) ? CompatibilityClass.DYNAMIC_DETOUR_DEFERRED :
        message.StartsWith("DEFERRED_DETOUR_CONFIG", StringComparison.Ordinal) ? CompatibilityClass.DETOUR_CONFIG_DEFERRED :
        CompatibilityClass.DIRECT_HOOK_DEFERRED;

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition root)
    {
        yield return root;
        foreach (TypeDefinition nested in root.NestedTypes.SelectMany(AllTypes)) yield return nested;
    }

    private static CompatibilityClass MoreRestrictive(CompatibilityClass current, CompatibilityClass detected)
    {
        int Rank(CompatibilityClass value) => value switch
        {
            CompatibilityClass.CONTENT_ONLY => 0,
            CompatibilityClass.STATIC_MODULE => 1,
            CompatibilityClass.NORMAL_EVENT => 2,
            CompatibilityClass.ON_HOOK_SUPPORTED => 3,
            CompatibilityClass.DIRECT_HOOK_SUPPORTED => 4,
            CompatibilityClass.MIXED_MANAGED_DETOURS_SUPPORTED => 5,
            CompatibilityClass.MODINTEROP_STATIC_SUPPORTED => 6,
            CompatibilityClass.STATIC_IL_EVENT_FREEZE => 7,
            CompatibilityClass.STATIC_IL_EVENT_SEQUENCE => 8,
            CompatibilityClass.STATIC_DIRECT_ILHOOK_FREEZE => 9,
            CompatibilityClass.HASH_LOCKED_STATIC_AOT_COMPATIBILITY => 10,
            CompatibilityClass.MODINTEROP_DEFERRED => 11,
            CompatibilityClass.ON_HOOK_DEFERRED => 12,
            CompatibilityClass.IL_HOOK_DEFERRED => 13,
            CompatibilityClass.DIRECT_HOOK_DEFERRED => 14,
            CompatibilityClass.DYNAMIC_TARGET_DEFERRED => 15,
            CompatibilityClass.DYNAMIC_DETOUR_DEFERRED => 16,
            CompatibilityClass.DETOUR_CONFIG_DEFERRED => 17,
            CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED => 18,
            CompatibilityClass.CUSTOM_AUDIO_UNSUPPORTED => 19,
            CompatibilityClass.NATIVE_UNSUPPORTED => 20,
            CompatibilityClass.LUA_UNSUPPORTED => 21,
            CompatibilityClass.PLATFORM_UNSUPPORTED => 22,
            _ => 99
        };
        return Rank(detected) > Rank(current) ? detected : current;
    }

    private static bool IsManaged(string path) => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
    private static bool IsContent(string path)
    {
        string[] roots = ["Content/", "Maps/", "Dialog/", "Graphics/", "Tutorials/", "Audio/", "Effects/", "Atlases/", "Decals/", "Characters/", "Config/"];
        if (roots.Any(root => path.StartsWith(root, StringComparison.Ordinal))) return true;
        // Everest exposes ordinary root YAML files as virtual content assets.
        // The metadata file itself is control data, not game content.
        return !path.Contains('/') &&
               (path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)) &&
               !path.Equals("everest.yaml", StringComparison.OrdinalIgnoreCase) &&
               !path.Equals("everest.yml", StringComparison.OrdinalIgnoreCase);
    }
}
