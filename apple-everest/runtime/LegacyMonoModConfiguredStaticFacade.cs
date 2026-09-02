using System;
using System.Collections.Generic;

namespace Celeste.Mod.Helpers.LegacyMonoMod;

// The host consumes this immutable legacy context when it creates the
// configured plan. The device keeps only the exact construction ABI.
public sealed class LegacyDetourContext : IDisposable
{
    public string ID;
    public int Priority;
    public readonly List<string> Before = new();
    public readonly List<string> After = new();
    public LegacyDetourContext(string id) { ID = id; }
    public void Dispose() { }
}
