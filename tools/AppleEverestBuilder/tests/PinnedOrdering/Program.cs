using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

internal static class Program
{
    private delegate int Original(int value);
    private delegate int ManagedDetour(Original orig, int value);
    private static readonly List<string> Trace = [];

    private static int Main(string[] args)
    {
        if (args.Length != 1) throw new InvalidDataException("one output JSON path is required");
        Dictionary<string, object> cases = new(StringComparer.Ordinal)
        {
            ["priority"] = Managed([
                ("low", new DetourConfig("low", -10)),
                ("ordinary", null),
                ("high", new DetourConfig("high", 10))]),
            ["before"] = Managed([
                ("after", new DetourConfig("after", 10)),
                ("before", new DetourConfig("before", -10, before: ["after"]))]),
            ["after"] = Managed([
                ("first", new DetourConfig("first", 10, after: ["second"])),
                ("second", new DetourConfig("second", -10))]),
            ["beforeWildcard"] = Managed([
                ("ordinary", null),
                ("peer", new DetourConfig("peer", 0)),
                ("before-wildcard", new DetourConfig("before-wildcard", 0, before: ["*"]))]),
            ["afterWildcard"] = Managed([
                ("ordinary", null),
                ("peer", new DetourConfig("peer", 0)),
                ("after-wildcard", new DetourConfig("after-wildcard", 0, after: ["*"]))]),
            ["defaultAndTie"] = Managed([
                ("tie-a", new DetourConfig("tie-a", 0)),
                ("tie-b", new DetourConfig("tie-b", 0)),
                ("ordinary", null)]),
            ["beforeAllNormalized"] = Managed([
                ("ordinary", null),
                ("peer", new DetourConfig("peer", 0)),
                ("before-all", new DetourConfig("before-all", int.MinValue))]),
            ["afterAllNormalized"] = Managed([
                ("ordinary", null),
                ("peer", new DetourConfig("peer", 0)),
                ("after-all", new DetourConfig("after-all", int.MaxValue))]),
            ["multipleIds"] = Managed([
                ("middle", new DetourConfig("middle", 0, after: ["first"], before: ["last"])),
                ("last", new DetourConfig("last", 100)),
                ("first", new DetourConfig("first", -100))]),
            ["ambientContext"] = AmbientContext(),
            ["lifetime"] = Lifetime(),
            ["cycleRejected"] = CycleRejected(),
            ["ilAThenB"] = IlCase(aFirst: true),
            ["ilBThenA"] = IlCase(aFirst: false)
        };
        object result = new
        {
            schemaVersion = 1,
            monoModCommit = "dfc30a1506d37fb88a2c2be004f525205f46a24c",
            runtimeAssembly = typeof(Hook).Assembly.GetName().Name,
            runtimeVersion = typeof(Hook).Assembly.GetName().Version?.ToString(),
            cases
        };
        string output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(result,
            new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine("PASS: real pinned MonoMod configured-order reference");
        return 0;
    }

    private static object Managed((string Id, DetourConfig? Config)[] definitions)
    {
        Trace.Clear();
        MethodInfo target = typeof(Program).GetMethod(nameof(ManagedTarget),
            BindingFlags.Static | BindingFlags.NonPublic)!;
        List<Hook> hooks = [];
        try
        {
            foreach ((string id, DetourConfig? config) in definitions)
            {
                string captured = id;
                ManagedDetour detour = (orig, value) =>
                {
                    Trace.Add(captured + "-before");
                    int result = orig(value * 10 + captured.Length);
                    Trace.Add(captured + "-after");
                    return result + captured.Length;
                };
                hooks.Add(new Hook(target, detour, config));
            }
            int value = ManagedTarget(1);
            string[] chain = DetourManager.GetDetourInfo(target).Detours
                .Select(info => info.Config?.Id ?? "ordinary").ToArray();
            return new { registration = definitions.Select(value => value.Id), chain, trace = Trace.ToArray(), value };
        }
        finally
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
        }
    }

    private static object AmbientContext()
    {
        DetourConfig config = new("ambient", 17, before: ["other"]);
        using DataScope scope = new DetourConfigContext(config).Use();
        MethodInfo target = typeof(Program).GetMethod(nameof(AmbientTarget),
            BindingFlags.Static | BindingFlags.NonPublic)!;
        ManagedDetour detour = (orig, value) => orig(value) + 1;
        using Hook hook = new(target, detour);
        return new
        {
            id = hook.Config?.Id,
            priority = hook.Config?.Priority,
            before = hook.Config?.Before.ToArray(),
            active = hook.IsApplied,
            value = AmbientTarget(2)
        };
    }

    private static object Lifetime()
    {
        MethodInfo target = typeof(Program).GetMethod(nameof(LifetimeTarget),
            BindingFlags.Static | BindingFlags.NonPublic)!;
        ManagedDetour detour = (orig, value) => orig(value) + 10;
        Hook hook = new(target, detour, new DetourConfig("lifetime"), applyByDefault: false);
        int initial = LifetimeTarget(1);
        hook.Apply();
        int applied = LifetimeTarget(1);
        hook.Undo();
        int undone = LifetimeTarget(1);
        hook.Apply();
        hook.Dispose();
        int disposed = LifetimeTarget(1);
        return new { initial, applied, undone, disposed, hook.IsApplied, hook.IsValid };
    }

    private static object CycleRejected()
    {
        MethodInfo target = typeof(Program).GetMethod(nameof(CycleTarget),
            BindingFlags.Static | BindingFlags.NonPublic)!;
        ManagedDetour a = (orig, value) => orig(value) + 1;
        ManagedDetour b = (orig, value) => orig(value) + 2;
        ManagedDetour c = (orig, value) => orig(value) + 3;
        using Hook first = new(target, a, new DetourConfig("a", before: ["b"]));
        using Hook second = new(target, b, new DetourConfig("b", before: ["c"]));
        try
        {
            using Hook third = new(target, c, new DetourConfig("c", before: ["a"]));
            return new { rejected = false, exception = "" };
        }
        catch (InvalidOperationException exception)
        {
            return new { rejected = true, exception = exception.Message };
        }
    }

    private static object IlCase(bool aFirst)
    {
        MethodInfo target = typeof(Program).GetMethod(nameof(IlTarget),
            BindingFlags.Static | BindingFlags.NonPublic)!;
        List<string> composition = [];
        void A(ILContext context)
        {
            composition.Add("A");
            Instruction instruction = context.Body.Instructions.Single(value => value.OpCode == OpCodes.Ldc_I4_1);
            instruction.OpCode = OpCodes.Ldc_I4_2;
        }
        void B(ILContext context)
        {
            composition.Add("B");
            Instruction? instruction = context.Body.Instructions.SingleOrDefault(value => value.OpCode == OpCodes.Ldc_I4_2);
            if (instruction != null) instruction.OpCode = OpCodes.Ldc_I4_3;
        }
        List<ILHook> hooks = [];
        try
        {
            if (aFirst)
            {
                hooks.Add(new ILHook(target, A, new DetourConfig("A", 10)));
                hooks.Add(new ILHook(target, B, new DetourConfig("B", -10)));
            }
            else
            {
                hooks.Add(new ILHook(target, B, new DetourConfig("B", 10)));
                hooks.Add(new ILHook(target, A, new DetourConfig("A", -10)));
            }
            string[] graph = DetourManager.GetDetourInfo(target).ILHooks
                .Select(info => info.Config?.Id ?? "ordinary").ToArray();
            return new { graph, composition = composition.ToArray(), value = IlTarget() };
        }
        finally
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int ManagedTarget(int value) { Trace.Add("original"); return value; }
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int AmbientTarget(int value) => value;
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int LifetimeTarget(int value) => value;
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int CycleTarget(int value) => value;
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int IlTarget() => 1;
}
