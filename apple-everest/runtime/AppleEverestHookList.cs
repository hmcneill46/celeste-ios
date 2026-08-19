using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod;

internal interface IAppleEverestManagedHookRegistration : IDisposable
{
    bool IsApplied { get; }
    bool IsValid { get; }
    void Apply();
    void Undo();
}

internal sealed class AppleEverestManagedHook<T> : IAppleEverestManagedHookRegistration where T : Delegate
{
    private readonly List<AppleEverestManagedHook<T>> ownerList;
    private bool valid = true;
    private bool applied;

    internal AppleEverestManagedHook(List<AppleEverestManagedHook<T>> ownerList, string owner, T handler,
        global::MonoMod.RuntimeDetour.DetourConfig config, bool applyByDefault, long sequence)
    {
        this.ownerList = ownerList;
        Owner = owner;
        Handler = handler;
        Config = config;
        Sequence = sequence;
        ownerList.Add(this);
        if (applyByDefault) Apply();
    }

    internal string Owner { get; }
    internal T Handler { get; }
    internal global::MonoMod.RuntimeDetour.DetourConfig Config { get; }
    internal long Sequence { get; }
    public bool IsApplied => valid && applied;
    public bool IsValid => valid;

    public void Apply()
    {
        if (!valid) throw new ObjectDisposedException(nameof(AppleEverestManagedHook<T>));
        if (applied) return;
        applied = true;
        AppleEverestHookList.Invalidate();
    }

    public void Undo()
    {
        if (!valid) throw new ObjectDisposedException(nameof(AppleEverestManagedHook<T>));
        if (!applied) return;
        applied = false;
        AppleEverestHookList.Invalidate();
    }

    public void Dispose()
    {
        if (!valid) return;
        if (applied) AppleEverestHookList.Invalidate();
        applied = false;
        valid = false;
        ownerList.Remove(this);
    }
}

internal static class AppleEverestHookList
{
    private static long nextSequence;
    internal static int Version { get; private set; }

    internal static void Invalidate() => Version++;

    internal static void AddEvent<T>(List<AppleEverestManagedHook<T>> hooks, T handler) where T : Delegate
    {
        if (handler == null) return;
        _ = new AppleEverestManagedHook<T>(hooks, AppleEverestStaticRuntime.CurrentOwner, handler, null, true, ++nextSequence);
    }

    internal static void RemoveEvent<T>(List<AppleEverestManagedHook<T>> hooks, T handler) where T : Delegate
    {
        if (handler == null) return;
        string owner = AppleEverestStaticRuntime.CurrentOwner;
        AppleEverestManagedHook<T> match = hooks.LastOrDefault(candidate =>
            candidate.Owner == owner && EqualityComparer<T>.Default.Equals(candidate.Handler, handler));
        match?.Dispose();
    }

    internal static IAppleEverestManagedHookRegistration AddDirect<T>(List<AppleEverestManagedHook<T>> hooks, T handler,
        global::MonoMod.RuntimeDetour.DetourConfig config, bool applyByDefault) where T : Delegate =>
        new AppleEverestManagedHook<T>(hooks, AppleEverestStaticRuntime.CurrentOwner, handler, config, applyByDefault, ++nextSequence);

    internal static void RemoveOwner<T>(List<AppleEverestManagedHook<T>> hooks, string owner) where T : Delegate
    {
        foreach (AppleEverestManagedHook<T> hook in hooks.Where(candidate => candidate.Owner == owner).ToArray()) hook.Dispose();
    }

    internal static IEnumerable<T> Active<T>(List<AppleEverestManagedHook<T>> hooks) where T : Delegate
    {
        AppleEverestManagedHook<T>[] active = hooks.Where(hook => hook.IsApplied && AppleEverestStaticRuntime.IsModuleEnabled(hook.Owner)).ToArray();
        foreach (AppleEverestManagedHook<T> hook in Order(active)) yield return hook.Handler;
    }

    private static IReadOnlyList<AppleEverestManagedHook<T>> Order<T>(AppleEverestManagedHook<T>[] hooks) where T : Delegate
    {
        // MonoMod runs configured detours before the no-config LIFO chain. The
        // generated dispatcher builds delegates from inner to outer, hence the
        // deliberate reversal of the configured execution order below.
        AppleEverestManagedHook<T>[] noConfig = hooks.Where(hook => hook.Config == null)
            .OrderBy(hook => hook.Sequence).ToArray();
        AppleEverestManagedHook<T>[] configured = hooks.Where(hook => hook.Config != null).ToArray();
        Dictionary<AppleEverestManagedHook<T>, HashSet<AppleEverestManagedHook<T>>> edges = configured.ToDictionary(
            hook => hook, _ => new HashSet<AppleEverestManagedHook<T>>());
        foreach (AppleEverestManagedHook<T> left in configured)
        foreach (AppleEverestManagedHook<T> right in configured)
        {
            if (ReferenceEquals(left, right)) continue;
            string leftId = left.Config?.Id;
            string rightId = right.Config?.Id;
            if (left.Config?.Before?.Contains(rightId) == true || right.Config?.After?.Contains(leftId) == true) edges[left].Add(right);
            else if (left.Config?.After?.Contains(rightId) == true || right.Config?.Before?.Contains(leftId) == true) edges[right].Add(left);
        }
        List<AppleEverestManagedHook<T>> remaining = configured.ToList();
        List<AppleEverestManagedHook<T>> executionOrder = new();
        while (remaining.Count > 0)
        {
            AppleEverestManagedHook<T>[] ready = remaining.Where(candidate => !remaining.Any(other => edges[other].Contains(candidate)))
                .OrderBy(candidate => candidate.Config?.Priority.HasValue == true ? 0 : 1)
                .ThenByDescending(candidate => candidate.Config?.Priority ?? 0)
                .ThenByDescending(candidate => candidate.Config?.SubPriority ?? 0)
                .ThenBy(candidate => candidate.Config?.Priority.HasValue == true ? -candidate.Sequence : candidate.Sequence)
                .ToArray();
            if (ready.Length == 0) throw new InvalidOperationException("cyclic Apple static managed-detour ordering constraints");
            AppleEverestManagedHook<T> selected = ready[0];
            executionOrder.Add(selected);
            remaining.Remove(selected);
        }
        return noConfig.Concat(executionOrder.AsEnumerable().Reverse()).ToArray();
    }
}
