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
        bool applyByDefault, long sequence, long dispatcherOrdinal)
    {
        this.ownerList = ownerList;
        Owner = owner;
        Handler = handler;
        Sequence = sequence;
        DispatcherOrdinal = dispatcherOrdinal;
        ownerList.Add(this);
        if (applyByDefault) Apply();
    }

    internal string Owner { get; }
    internal T Handler { get; }
    internal long Sequence { get; }
    internal long DispatcherOrdinal { get; }
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

    internal static void AddEvent<T>(List<AppleEverestManagedHook<T>> hooks, T handler, string targetId) where T : Delegate
    {
        if (handler == null) return;
        long sequence = ++nextSequence;
        string owner = AppleEverestStaticRuntime.CurrentOwner;
        long ordinal = GeneratedAppleEverestConfiguredOrdinals.Resolve(owner, targetId) ?? sequence;
        _ = new AppleEverestManagedHook<T>(hooks, owner, handler, true, sequence, ordinal);
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
        bool applyByDefault, long? staticDispatcherOrdinal = null) where T : Delegate
    {
        long sequence = ++nextSequence;
        return new AppleEverestManagedHook<T>(hooks, AppleEverestStaticRuntime.CurrentOwner, handler,
            applyByDefault, sequence, staticDispatcherOrdinal ?? sequence);
    }

    internal static void RemoveOwner<T>(List<AppleEverestManagedHook<T>> hooks, string owner) where T : Delegate
    {
        foreach (AppleEverestManagedHook<T> hook in hooks.Where(candidate => candidate.Owner == owner).ToArray()) hook.Dispose();
    }

    internal static IEnumerable<T> Active<T>(List<AppleEverestManagedHook<T>> hooks) where T : Delegate
    {
        // The host-side planner has already resolved every configured graph.
        // Device dispatch is deliberately data-only: fixed inner-to-outer
        // ordinals followed by a stable registration tie-breaker.
        foreach (AppleEverestManagedHook<T> hook in hooks
                     .Where(hook => hook.IsApplied && AppleEverestStaticRuntime.IsModuleEnabled(hook.Owner))
                     .OrderBy(hook => hook.DispatcherOrdinal)
                     .ThenBy(hook => hook.Sequence))
            yield return hook.Handler;
    }
}
