using System;
using System.Collections.Generic;

namespace Celeste.Mod;

internal sealed class AppleEverestOwnedHook<T> where T : Delegate
{
    internal string Owner;
    internal T Handler;
}

internal static class AppleEverestHookList
{
    internal static int Version { get; private set; }

    internal static void Invalidate() => Version++;

    internal static void Add<T>(List<AppleEverestOwnedHook<T>> hooks, T handler) where T : Delegate
    {
        if (handler == null) return;
        hooks.Add(new AppleEverestOwnedHook<T> { Owner = AppleEverestStaticRuntime.CurrentOwner, Handler = handler });
        Invalidate();
    }

    internal static void Remove<T>(List<AppleEverestOwnedHook<T>> hooks, T handler) where T : Delegate
    {
        if (handler == null) return;
        string owner = AppleEverestStaticRuntime.CurrentOwner;
        for (int index = hooks.Count - 1; index >= 0; index--)
        {
            AppleEverestOwnedHook<T> candidate = hooks[index];
            if (candidate.Owner == owner && EqualityComparer<T>.Default.Equals(candidate.Handler, handler))
            {
                hooks.RemoveAt(index);
                Invalidate();
                return;
            }
        }
    }

    internal static IEnumerable<T> Active<T>(List<AppleEverestOwnedHook<T>> hooks) where T : Delegate
    {
        foreach (AppleEverestOwnedHook<T> hook in hooks)
            if (AppleEverestStaticRuntime.IsModuleEnabled(hook.Owner)) yield return hook.Handler;
    }
}
