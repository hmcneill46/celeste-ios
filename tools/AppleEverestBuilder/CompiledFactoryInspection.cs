using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AppleEverestBuilder;

// HOST ONLY. Reflection reads the exact compiled selector and invokes only the
// production data parser/profile guard. Entity constructors are exercised by
// authored runtime canaries, never simulated by this graphics-free inspection.
internal static class CompiledFactoryInspection
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    internal sealed record Factory(string Kind, string CustomId, string Provider, string EntryMethod,
        string RegistrationSha256, string GuardSha256, string[] ConcreteTypes, object[] TypeClosure,
        int AcceptedOccurrences, bool Linked, bool ActualSelectorInvoked, bool ActualProfileGuardInvoked,
        bool UnexpectedProfileRejected, string[] ProductionCallers);

    private sealed class ProbeContext(string root) : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            string path = Path.Combine(root, name.Name + ".dll");
            return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
        }
    }

    internal static Factory[] Inspect(string assemblyPath, JsonElement profiles, IEnumerable<SelectedFactoryClosureFactory> selected, bool skipMissing = false)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        string root = Path.GetDirectoryName(assemblyPath)!;
        using DefaultAssemblyResolver resolver = new();
        resolver.AddSearchDirectory(root);
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        using AssemblyDefinition compiled = AssemblyDefinition.ReadAssembly(assemblyPath,
            new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = false });
        TypeDefinition registry = compiled.MainModule.GetType("Celeste.Mod.GeneratedAppleEverestGameplayRegistry")
            ?? throw new InvalidDataException("actual linked factory registry is absent");
        TypeDefinition guard = compiled.MainModule.GetType("Celeste.Mod.AppleEverestSelectedProfileGuard")
            ?? throw new InvalidDataException("actual linked profile guard is absent");
        TypeDefinition? snasGuard = compiled.MainModule.GetType("Celeste.Mod.AppleEverestSnasProfileGuard");
        IEnumerable<TypeDefinition> guards = snasGuard == null ? [guard] : [guard, snasGuard];
        string guardHash = Hashing.BytesSha256(Encoding.UTF8.GetBytes(string.Join("\n", guards.SelectMany(type => type.Methods).Where(method => method.HasBody)
            .OrderBy(method => method.FullName, StringComparer.Ordinal).Select(Normalize))));
        ProbeContext context = new(root);
        try
        {
            Assembly runtime = context.LoadFromAssemblyPath(assemblyPath);
            Type runtimeRegistry = NeedType(runtime, registry.FullName);
            Type runtimeGuard = NeedType(runtime, guard.FullName);
            Type element = NeedType(runtime, "Celeste.BinaryPacker+Element");
            Type levelData = NeedType(runtime, "Celeste.LevelData");
            MethodInfo parser = levelData.GetMethod("CreateEntityData", Members)
                ?? throw new InvalidDataException("actual production EntityData parser is absent");
            List<Factory> result = [];
            foreach (SelectedFactoryClosureFactory factory in selected.OrderBy(row => row.Kind + ":" + row.CustomId, StringComparer.Ordinal))
            {
                string title = char.ToUpperInvariant(factory.Kind[0]) + factory.Kind[1..];
                MethodDefinition creator = registry.Methods.Single(method => method.Name == "TryCreate" + title && method.HasBody);
                if (!creator.Body.Instructions.Any(instruction => instruction.Operand is MethodReference reference &&
                    reference.DeclaringType.FullName == registry.FullName && reference.Name == "Select" + title) ||
                    !creator.Body.Instructions.Any(instruction => instruction.Operand is MethodReference reference && reference.Name == "Invoke"))
                    throw new InvalidDataException("production creation path does not invoke the inspected selector: " + title);
                string callerType = factory.Kind == "backdrop" ? "Celeste.MapData" : "Celeste.Level";
                string[] callers = compiled.MainModule.GetType(callerType).Methods.Where(method => method.HasBody &&
                    method.Body.Instructions.Any(instruction => instruction.Operand is MethodReference reference && reference.FullName == creator.FullName))
                    .Select(method => method.FullName).Order(StringComparer.Ordinal).ToArray();
                if (callers.Length == 0) throw new InvalidDataException("factory is not reachable from the actual production level/map loader: " + title);
                MethodInfo selector = runtimeRegistry.GetMethod("Select" + title, Members)
                    ?? throw new InvalidDataException("actual production selector absent: " + title);
                Delegate? dispatch = selector.Invoke(null, [factory.CustomId]) as Delegate;
                if (dispatch == null)
                {
                    if (skipMissing) continue;
                    throw new InvalidDataException("actual production registration absent: " + factory.CustomId);
                }
                string entryName = SelectedFactoryProfiles.EntryMethod(factory.Kind, factory.CustomId);
                if (dispatch.Method.DeclaringType != runtimeRegistry || dispatch.Method.Name != entryName)
                    throw new InvalidDataException("actual production selector reaches an unexpected entry: " + factory.CustomId);
                MethodDefinition entry = registry.Methods.SingleOrDefault(method => method.Name == entryName && method.HasBody)
                    ?? throw new InvalidDataException("linked entry body absent: " + factory.CustomId);
                // All selected entries are deliberately straight-line: guard,
                // explicit constructor/typed creator, identity, usage record.
                if (entry.Body.ExceptionHandlers.Count != 0 || entry.Body.Instructions.Any(instruction =>
                    instruction.OpCode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch or FlowControl.Throw))
                    throw new InvalidDataException("unreviewed factory entry control flow: " + factory.CustomId);
                Instruction firstCall = entry.Body.Instructions.First(instruction => instruction.Operand is MethodReference);
                string expectedGuard = factory.Kind == "backdrop" ? "Backdrop" : "Entity";
                if (firstCall.Operand is not MethodReference guardCall || guardCall.DeclaringType.FullName != guard.FullName || guardCall.Name != expectedGuard)
                    throw new InvalidDataException("actual selected guard is not before construction: " + factory.CustomId);
                Instruction[] instructions = entry.Body.Instructions.ToArray();
                int recordIndex = Array.FindIndex(instructions, instruction => instruction.Operand is MethodReference method &&
                    method.DeclaringType.FullName == "Celeste.Mod.AppleEverestStaticRuntime" && method.Name == "RecordCustomFactoryUse");
                if (recordIndex < 3 || instructions[recordIndex - 3].Operand as string != factory.Provider ||
                    instructions[recordIndex - 2].Operand as string != factory.CustomId || instructions[recordIndex - 1].Operand as string != factory.Kind)
                    throw new InvalidDataException("actual factory owner/id/kind differs: " + factory.CustomId);
                Dictionary<string, TypeDefinition> concrete = [];
                FindConcrete(entry, new HashSet<string>(StringComparer.Ordinal), concrete);
                if (concrete.Count == 0) throw new InvalidDataException("factory has no linked concrete implementation: " + factory.CustomId);
                int count = 0;
                MethodInfo check = runtimeGuard.GetMethod(expectedGuard, Members)
                    ?? throw new InvalidDataException("actual guard method absent");
                foreach (JsonElement row in profiles.GetProperty("occurrences").EnumerateArray().Where(row =>
                    row.GetProperty("kind").GetString() == factory.Kind && row.GetProperty("customId").GetString() == factory.CustomId))
                {
                    object data = MakeElement(element, factory.CustomId, row.GetProperty("attributes"), row.GetProperty("nodes"));
                    if (factory.Kind != "backdrop")
                    {
                        object level = RuntimeHelpers.GetUninitializedObject(levelData);
                        levelData.GetField("Name", Members)!.SetValue(level, row.GetProperty("room").GetString());
                        data = parser.Invoke(level, [data]) ?? throw new InvalidDataException("production parser returned null");
                    }
                    try { check.Invoke(null, [factory.CustomId, data]); }
                    catch (TargetInvocationException error) { throw new InvalidDataException("actual guard rejects selected occurrence: " + factory.CustomId +
                        " room=" + row.GetProperty("room").GetString() + " profile=" + row.GetProperty("profileSha256").GetString(), error.InnerException); }
                    if (count == 0)
                    {
                        FieldInfo valuesField = data.GetType().GetField(factory.Kind == "backdrop" ? "Attributes" : "Values", Members)!;
                        var values = (Dictionary<string, object>?)valuesField.GetValue(data) ?? new(StringComparer.Ordinal);
                        valuesField.SetValue(data, values);
                        values.Add("__stage25kj_unreviewed_attribute", true);
                        bool rejected = false;
                        try { check.Invoke(null, [factory.CustomId, data]); }
                        catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
                        values.Remove("__stage25kj_unreviewed_attribute");
                        if (!rejected) throw new InvalidDataException("actual profile guard accepts unreviewed attributes: " + factory.CustomId);
                    }
                    count++;
                }
                if (count == 0) throw new InvalidDataException("selected factory has no package-backed occurrences: " + factory.CustomId);
                result.Add(new(factory.Kind, factory.CustomId, factory.Provider, entry.FullName,
                    Hashing.BytesSha256(Encoding.UTF8.GetBytes(Normalize(entry))), guardHash,
                    concrete.Keys.Order(StringComparer.Ordinal).ToArray(), concrete.Values.OrderBy(type => type.FullName, StringComparer.Ordinal)
                        .Select(TypeClosure).ToArray(), count, true, true, true, true, callers));
            }
            return result.ToArray();
        }
        finally { context.Unload(); }
    }

    private static Type NeedType(Assembly assembly, string name) => assembly.GetType(name, throwOnError: true)!;

    // These six controls predate the selected-profile lane. Their exact map
    // bytes, package/frozen-IL and stage-owned runtime proofs are separate
    // authorities. This inspection must never report a selected guard PASS.
    internal static object[] InspectLegacyRegressionEntries(string assemblyPath)
    {
        (string Id, string Owner)[] expected = [
            ("ChronoHelper/ExplodingPinata", "ChronoHelper"),
            ("DJMapHelper/colorfulFlyFeather", "DJMapHelper"),
            ("DJMapHelper/featherBarrier", "DJMapHelper"),
            ("appleEverest/stage25keRootState", "StrawberryJam2021"),
            ("appleEverest/stage25kfAudio", "StrawberryJam2021"),
            ("appleEverest/stage25kfDepthTarget", "CrystallineHelper") ];
        assemblyPath = Path.GetFullPath(assemblyPath);
        string root = Path.GetDirectoryName(assemblyPath)!;
        using DefaultAssemblyResolver resolver = new();
        resolver.AddSearchDirectory(root);
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        using AssemblyDefinition compiled = AssemblyDefinition.ReadAssembly(assemblyPath,
            new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = false });
        TypeDefinition registry = compiled.MainModule.GetType("Celeste.Mod.GeneratedAppleEverestGameplayRegistry")
            ?? throw new InvalidDataException("legacy compiled factory registry absent");
        MethodDefinition creator = registry.Methods.Single(method => method.Name == "TryCreateEntity" && method.HasBody);
        if (!creator.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call &&
            call.DeclaringType.FullName == registry.FullName && call.Name == "SelectEntity") ||
            !creator.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.Name == "Invoke"))
            throw new InvalidDataException("legacy production creation caller differs");
        string[] callers = compiled.MainModule.GetType("Celeste.Level").Methods.Where(method => method.HasBody &&
            method.Body.Instructions.Any(instruction => instruction.Operand is MethodReference call && call.FullName == creator.FullName))
            .Select(method => method.FullName).Order(StringComparer.Ordinal).ToArray();
        if (callers.Length == 0) throw new InvalidDataException("legacy production Level caller absent");
        ProbeContext context = new(root);
        try
        {
            Assembly runtime = context.LoadFromAssemblyPath(assemblyPath);
            Type runtimeRegistry = NeedType(runtime, registry.FullName);
            MethodInfo selector = runtimeRegistry.GetMethod("SelectEntity", Members)!;
            if (selector.Invoke(null, ["__stage25kn_missing_legacy"]) != null)
                throw new InvalidDataException("unknown legacy factory accepted");
            List<object> rows = [];
            foreach (var item in expected)
            {
                Delegate dispatch = selector.Invoke(null, [item.Id]) as Delegate
                    ?? throw new InvalidDataException("actual legacy registration absent: " + item.Id);
                if (dispatch.Method.DeclaringType != runtimeRegistry)
                    throw new InvalidDataException("legacy selector escaped generated registry");
                MethodDefinition entry = registry.Methods.Single(method => method.Name == dispatch.Method.Name && method.HasBody);
                if (entry.Body.ExceptionHandlers.Count != 0 || entry.Body.Instructions.Any(instruction =>
                    instruction.OpCode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch or FlowControl.Throw))
                    throw new InvalidDataException("unreviewed legacy entry control flow");
                Instruction[] instructions = entry.Body.Instructions.ToArray();
                int record = Array.FindIndex(instructions, instruction => instruction.Operand is MethodReference method &&
                    method.DeclaringType.FullName == "Celeste.Mod.AppleEverestStaticRuntime" && method.Name == "RecordCustomFactoryUse");
                if (record < 3 || instructions[record-3].Operand as string != item.Owner ||
                    instructions[record-2].Operand as string != item.Id || instructions[record-1].Operand as string != "entity")
                    throw new InvalidDataException("legacy compiled provider/ID differs");
                Dictionary<string, TypeDefinition> concrete = [];
                FindConcrete(entry, new(StringComparer.Ordinal), concrete);
                if (concrete.Count == 0) throw new InvalidDataException("legacy linked concrete constructor missing");
                rows.Add(new { kind = "entity", customId = item.Id, provider = item.Owner,
                    actualSelectorInvoked = true, actualProductionCallers = callers,
                    selectedProfileGuardProof = false, constructorLifecycleExecution = false,
                    entrySha256 = Hashing.BytesSha256(Encoding.UTF8.GetBytes(Normalize(entry))),
                    linkedTypeClosure = concrete.Values.OrderBy(type => type.FullName, StringComparer.Ordinal).Select(TypeClosure).ToArray(),
                    disposition = "SEPARATE_LEGACY_PACKAGE_FROZEN_IL_BEHAVIOR_AND_EXACT_MAP_PROOFS_REQUIRED" });
            }
            return rows.ToArray();
        }
        finally { context.Unload(); }
    }
    private static object MakeElement(Type type, string name, JsonElement attributes, JsonElement nodes)
    {
        object value = Activator.CreateInstance(type)!;
        type.GetField("Name", Members)!.SetValue(value, name);
        Dictionary<string, object> values = new(StringComparer.Ordinal);
        foreach (JsonProperty attribute in attributes.EnumerateObject()) values.Add(attribute.Name, attribute.Value.ValueKind switch
        {
            JsonValueKind.String => attribute.Value.GetString()!,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => attribute.Value.TryGetInt32(out int integer) ? (object)integer : attribute.Value.GetSingle(),
            _ => throw new InvalidDataException("unreviewed authored attribute type")
        });
        type.GetField("Attributes", Members)!.SetValue(value, values);
        FieldInfo childrenField = type.GetField("Children", Members)!;
        IList children = (IList)Activator.CreateInstance(childrenField.FieldType)!;
        using JsonDocument empty = JsonDocument.Parse("[]");
        foreach (JsonElement node in nodes.EnumerateArray()) children.Add(MakeElement(type, "node", node, empty.RootElement));
        childrenField.SetValue(value, children);
        return value;
    }

    private static bool IsGameplay(TypeDefinition type)
    {
        for (TypeDefinition? current = type; current != null; current = current.BaseType?.Resolve())
            if (current.FullName is "Monocle.Entity" or "Celeste.Backdrop") return true;
        return false;
    }
    private static void FindConcrete(MethodDefinition method, HashSet<string> visited, Dictionary<string, TypeDefinition> found)
    {
        if (!visited.Add(method.FullName) || !method.HasBody) return;
        foreach (Instruction instruction in method.Body.Instructions)
        {
            if (instruction.Operand is not MethodReference called) continue;
            if (instruction.OpCode == OpCodes.Newobj)
            {
                TypeDefinition type = called.DeclaringType.Resolve() ?? throw new InvalidDataException("unresolved factory constructor");
                if (IsGameplay(type))
                {
                    if (type.IsAbstract || called.Resolve()?.HasBody != true) throw new InvalidDataException("missing concrete constructor body");
                    found[type.FullName] = type;
                }
            }
            else if (called.DeclaringType.Namespace == "Celeste.Mod" && called.Name is "Create" or "Offset" or "Target" or "Smooth")
                FindConcrete(called.Resolve() ?? throw new InvalidDataException("missing linked typed creator"), visited, found);
        }
    }
    private static object TypeClosure(TypeDefinition type)
    {
        List<string> bases = [];
        List<object> lifecycle = [];
        HashSet<string> names = [];
        for (TypeDefinition? current = type; current != null; current = current.BaseType?.Resolve())
        {
            bases.Add(current.FullName);
            foreach (MethodDefinition method in current.Methods.Where(method => method.Name is "Added" or "Awake" or "Update" or "Render" or "Removed" or "SceneEnd"))
                if (names.Add(method.Name)) lifecycle.Add(new { method = method.FullName, bodySha256 = method.HasBody ?
                    Hashing.BytesSha256(Encoding.UTF8.GetBytes(Normalize(method))) : null, inherited = current != type });
        }
        return new { type = type.FullName, baseChain = bases,
            constructors = type.Methods.Where(method => method.IsConstructor).Select(method => new { method = method.FullName,
                bodySha256 = method.HasBody ? Hashing.BytesSha256(Encoding.UTF8.GetBytes(Normalize(method))) : null }).ToArray(), lifecycle };
    }
    internal static string Normalize(MethodDefinition method)
    {
        var instructions = method.Body.Instructions;
        return method.FullName + "\n" + string.Join("\n", instructions.Select(instruction => instruction.OpCode.Name + " " + (instruction.Operand switch
        {
            Instruction target => "@" + instructions.IndexOf(target),
            Instruction[] targets => string.Join(",", targets.Select(target => "@" + instructions.IndexOf(target))),
            MemberReference member => member.FullName,
            ParameterDefinition parameter => "arg:" + parameter.Index,
            VariableDefinition variable => "local:" + variable.Index + ":" + variable.VariableType.FullName,
            _ => Convert.ToString(instruction.Operand, System.Globalization.CultureInfo.InvariantCulture)
        })));
    }
}
