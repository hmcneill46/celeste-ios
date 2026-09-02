using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoMod.RuntimeDetour;

public sealed class DetourConfig
{
    public DetourConfig(string id, int? priority = null, IEnumerable<string> before = null, IEnumerable<string> after = null)
        : this(id, priority, before, after, 0) { }

    public DetourConfig(string id, int? priority, IEnumerable<string> before, IEnumerable<string> after, int subPriority)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("detour ID is required", nameof(id));
        Id = id;
        Priority = priority;
        Before = (before ?? Array.Empty<string>()).ToArray();
        After = (after ?? Array.Empty<string>()).ToArray();
        SubPriority = subPriority;
    }

    public string Id { get; }
    public int? Priority { get; }
    public IEnumerable<string> Before { get; }
    public IEnumerable<string> After { get; }
    public int SubPriority { get; }
    public DetourConfig WithPriority(int? value) => new(Id, value, Before, After, SubPriority);
    public DetourConfig WithBefore(IEnumerable<string> value) => new(Id, Priority, value, After, SubPriority);
    public DetourConfig WithBefore(params string[] value) => WithBefore((IEnumerable<string>)value);
    public DetourConfig WithAfter(IEnumerable<string> value) => new(Id, Priority, Before, value, SubPriority);
    public DetourConfig WithAfter(params string[] value) => WithAfter((IEnumerable<string>)value);
    public DetourConfig AddBefore(IEnumerable<string> value) => WithBefore(Before.Concat(value));
    public DetourConfig AddBefore(params string[] value) => AddBefore((IEnumerable<string>)value);
    public DetourConfig AddAfter(IEnumerable<string> value) => WithAfter(After.Concat(value));
    public DetourConfig AddAfter(params string[] value) => AddAfter((IEnumerable<string>)value);
}

public sealed class DetourConfigContext : IDisposable
{
    public DetourConfigContext(DetourConfig config) { Config = config; }
    public DetourConfig Config { get; }
    public void Dispose() { }
}

public sealed class Hook : IDisposable
{
    public const bool ApplyByDefault = true;
    private global::Celeste.Mod.IAppleEverestManagedHookRegistration registration;

    // AppleEverestBuilder replaces every statically resolved public Hook
    // construction with this plan-only constructor. The device never repeats
    // MethodBase discovery and never patches executable code.
    public Hook(string staticPlanId) : this(staticPlanId, ApplyByDefault) { }

    internal Hook(string staticPlanId, bool applyByDefault)
    {
        registration = global::Celeste.Mod.GeneratedAppleEverestDirectHookRegistry.CreateByPlan(staticPlanId, applyByDefault);
    }

    public bool IsApplied => registration?.IsApplied == true;
    public bool IsValid => registration?.IsValid == true;
    public void Apply()
    {
        if (registration == null) throw new ObjectDisposedException(nameof(Hook));
        registration.Apply();
    }
    public void Undo()
    {
        if (registration == null) throw new ObjectDisposedException(nameof(Hook));
        registration.Undo();
    }
    public void Dispose()
    {
        registration?.Dispose();
        registration = null;
    }
}
