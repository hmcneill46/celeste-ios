using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AppleEverestBuilder;

/// <summary>
/// Closed registry for ordinary distributed DLLs whose configured detours can
/// be reduced to immutable, target-local ordinals on the host.  The product
/// receives only the resolved ordinals; it never receives this graph.
/// </summary>
internal static class StaticConfiguredDetourCompatibility
{
    // Ordinary registrations use the monotonically increasing device sequence.
    // Host-resolved configured registrations live in a disjoint range so that
    // an ordinary hook can never tie or overtake a configured outer wrapper.
    internal const long ConfiguredOrdinalBase = 1L << 60;
    internal const string LunaticName = "LunaticHelper";
    internal const string LunaticVersion = "1.1.1";
    internal const string LunaticSourceSha256 =
        "e7cef501937fc1bc07d1ff13e753fe920b4ccbbd4e4db4c0b2c4312de89fdd78";
    internal const string LunaticDllPath = "LunaticHelper.dll";
    internal const string LunaticDllSha256 =
        "fc08f00296551a6025c5e31422c6edd5e8136e6861459f44ffea909129c6a925";
    internal const string LunaticMethod = "System.Void LunaticHelper.BubbleReturnBerry::Load()";
    internal const string LunaticPlanId =
        "LunaticHelper:celeste-player-ctor:BubbleReturnBerry.onPlayerConstructor";

    internal static StaticConfiguredCompatibilityPlan? Resolve(ModInput input,
        EverestYamlEntry metadata)
    {
        if (metadata.Name != LunaticName || metadata.Version != LunaticVersion ||
            input.SourceSha256 != LunaticSourceSha256 || metadata.DLL != LunaticDllPath)
            return null;
        if (metadata.Dependencies.Count != 1 || metadata.Dependencies[0].Name != "Everest" ||
            metadata.Dependencies[0].Version != "1.1703.0" ||
            metadata.OptionalDependencies.Count != 0 || metadata.Conflicts.Count != 0)
            throw new InvalidDataException("LunaticHelper configured-fixture metadata drifted");

        string dll = Path.Combine(input.StagingRoot, LunaticDllPath);
        if (!File.Exists(dll) || Hashing.FileSha256(dll) != LunaticDllSha256)
            throw new InvalidDataException("LunaticHelper configured-fixture DLL identity drifted");
        ValidateLunaticSite(dll);

        StaticDetourConfig config = ConfiguredDetourOrdering.NormalizeLegacy(
            LunaticName, 0, [], ["*"], 0, forIlHook: false);
        StaticConfiguredDetourNode node = new(LunaticPlanId, "celeste-player-ctor",
            "HOOKGEN_ON", config, 0, 0);
        StaticConfiguredDetourSequence sequence = ConfiguredDetourOrdering.Resolve(
            node.TargetId, [node]);
        string hash = Hashing.BytesSha256(JsonSerializer.SerializeToUtf8Bytes(new
        {
            semanticVersion = ConfiguredDetourOrdering.SemanticVersion,
            fixture = "lunatichelper-1.1.1-player-ctor-after-all-v1",
            source = LunaticSourceSha256,
            dll = LunaticDllSha256,
            method = LunaticMethod,
            sequence
        }));
        return new("lunatichelper-1.1.1-player-ctor-after-all-v1", LunaticName,
            LunaticVersion, LunaticSourceSha256, LunaticDllPath, LunaticDllSha256,
            [LunaticMethod], [node], [sequence], hash);
    }

    private static void ValidateLunaticSite(string path)
    {
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path,
            new ReaderParameters { ReadSymbols = false });
        MethodDefinition method = assembly.MainModule.Types.SelectMany(AllTypes)
            .SelectMany(type => type.Methods).Single(candidate => candidate.FullName == LunaticMethod);
        Instruction[] body = method.Body.Instructions.ToArray();
        int contexts = body.Count(instruction => instruction.OpCode == OpCodes.Newobj &&
            instruction.Operand is MethodReference called &&
            called.DeclaringType.FullName == "MonoMod.RuntimeDetour.DetourContext" &&
            called.Name == ".ctor");
        int wildcards = body.Count(instruction => instruction.OpCode == OpCodes.Ldstr &&
            string.Equals(instruction.Operand as string, "*", StringComparison.Ordinal));
        int registrations = body.Count(instruction => instruction.OpCode == OpCodes.Call &&
            instruction.Operand is MethodReference called &&
            called.DeclaringType.FullName == "On.Celeste.Player" && called.Name == "add_ctor");
        int disposals = body.Count(instruction => instruction.Operand is MethodReference called &&
            called.DeclaringType.FullName == "System.IDisposable" && called.Name == "Dispose");
        if (contexts != 1 || wildcards != 1 || registrations != 1 || disposals != 1)
            throw new InvalidDataException("LunaticHelper configured site census drifted");
    }

    internal static void RewriteDeviceAssembly(AssemblyDefinition assembly,
        StaticConfiguredCompatibilityPlan? plan)
    {
        if (plan == null) return;
        if (plan.Owner != LunaticName || assembly.Name.Name != LunaticName ||
            plan.Id != "lunatichelper-1.1.1-player-ctor-after-all-v1")
            throw new InvalidDataException("configured device rewrite plan drifted");

        MethodDefinition method = assembly.MainModule.Types.SelectMany(AllTypes)
            .SelectMany(type => type.Methods).Single(candidate => candidate.FullName == LunaticMethod);
        Instruction[] body = method.Body.Instructions.ToArray();
        MethodReference callback = (MethodReference)body.Single(instruction => instruction.OpCode == OpCodes.Ldftn).Operand;
        MethodReference hookConstructor = (MethodReference)body.Single(instruction =>
            instruction.OpCode == OpCodes.Newobj && instruction.Operand is MethodReference called &&
            called.DeclaringType.FullName == "On.Celeste.Player/hook_ctor").Operand;
        MethodReference registration = (MethodReference)body.Single(instruction =>
            instruction.OpCode == OpCodes.Call && instruction.Operand is MethodReference called &&
            called.DeclaringType.FullName == "On.Celeste.Player" && called.Name == "add_ctor").Operand;
        if (method.Body.Variables.Count != 1 || method.Body.ExceptionHandlers.Count != 1 ||
            method.Body.ExceptionHandlers[0].HandlerType != ExceptionHandlerType.Finally)
            throw new InvalidDataException("LunaticHelper configured method shape drifted");

        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.InitLocals = false;
        method.Body.Instructions.Clear();
        ILProcessor il = method.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ldnull));
        il.Append(il.Create(OpCodes.Ldftn, callback));
        il.Append(il.Create(OpCodes.Newobj, hookConstructor));
        il.Append(il.Create(OpCodes.Call, registration));
        il.Append(il.Create(OpCodes.Ret));
        method.Body.MaxStackSize = 2;

        TypeReference[] contexts = assembly.MainModule.GetTypeReferences()
            .Where(type => type.FullName == "MonoMod.RuntimeDetour.DetourContext").ToArray();
        if (contexts.Length != 1)
            throw new InvalidDataException($"LunaticHelper DetourContext type-reference census drifted: {contexts.Length}");
        TypeReference replacement = assembly.MainModule.TypeSystem.Object;
        contexts[0].Namespace = replacement.Namespace;
        contexts[0].Name = replacement.Name;
        contexts[0].Scope = replacement.Scope;
    }

    internal static string GeneratedOrdinalSource(IReadOnlyList<ResolvedMod> mods)
    {
        var entries = mods.Where(mod => mod.StaticConfiguredDetours != null)
            .SelectMany(mod => mod.StaticConfiguredDetours!.Sequences.SelectMany(sequence =>
                sequence.ManagedDispatcherOrder.Select((planId, ordinal) => new
                {
                    mod.Metadata.Name,
                    sequence.TargetId,
                    PlanId = planId,
                    Ordinal = ConfiguredOrdinalBase + ordinal
                }))).OrderBy(value => value.Name, StringComparer.Ordinal)
            .ThenBy(value => value.TargetId, StringComparer.Ordinal)
            .ThenBy(value => value.Ordinal).ToArray();
        List<string> lines =
        [
            "namespace Celeste.Mod;",
            "internal static class GeneratedAppleEverestConfiguredOrdinals",
            "{",
            "    internal static long? Resolve(string owner, string targetId)",
            "    {"
        ];
        foreach (var entry in entries)
            lines.Add($"        if (owner == \"{Escape(entry.Name)}\" && targetId == \"{Escape(entry.TargetId)}\") return {entry.Ordinal}L;");
        lines.Add("        return null;");
        lines.Add("    }");
        lines.Add("}");
        return string.Join("\n", lines) + "\n";
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\",
        StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition type)
    {
        yield return type;
        foreach (TypeDefinition nested in type.NestedTypes.SelectMany(AllTypes)) yield return nested;
    }
}
