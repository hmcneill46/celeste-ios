using Runtime = Celeste.Mod.AppleEverestStaticRuntime;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    passed++;
}

string Original(string value, Celeste.Language language)
{
    Runtime.Trace.Add("original");
    return value + ":O";
}

string A(On.Celeste.Dialog.orig_Clean orig, string value, Celeste.Language language)
{
    Runtime.Trace.Add("A-before");
    string result = orig(value + ":argA", language);
    Runtime.Trace.Add("A-after");
    return result + ":A";
}

string B(On.Celeste.Dialog.orig_Clean orig, string value, Celeste.Language language)
{
    Runtime.Trace.Add("B-before");
    string result = orig(value, language);
    Runtime.Trace.Add("B-after");
    return result + ":B";
}

string Stop(On.Celeste.Dialog.orig_Clean orig, string value, Celeste.Language language)
{
    Runtime.Trace.Add("stop");
    return value + ":STOP";
}

string Invoke(string value = "probe")
{
    Runtime.Trace.Clear();
    return On.Celeste.Dialog.Invoke_Clean(value, null, Original);
}

Check(Invoke() == "probe:O", "zero-hook return");
Check(Runtime.Trace.SequenceEqual(["original"]), "zero-hook original once");

Check(!Celeste.Mod.AppleEverestLogPolicy.ShouldLog("Unconfigured", Celeste.Mod.LogLevel.Verbose),
    "default Everest log policy suppresses verbose");
Check(Celeste.Mod.AppleEverestLogPolicy.ShouldLog("Unconfigured", Celeste.Mod.LogLevel.Info),
    "default Everest log policy accepts info");
Celeste.Mod.AppleEverestLogPolicy.Set("Feather", Celeste.Mod.LogLevel.Warn);
Celeste.Mod.AppleEverestLogPolicy.Set("FeatherMaddy", Celeste.Mod.LogLevel.Info);
Check(Celeste.Mod.AppleEverestLogPolicy.ShouldLog("FeatherMaddyModule", Celeste.Mod.LogLevel.Info),
    "longest tag-prefix log rule wins");
Check(!Celeste.Mod.AppleEverestLogPolicy.ShouldLog("FeatherOther", Celeste.Mod.LogLevel.Info),
    "broader tag-prefix log rule remains enforced");
Celeste.Mod.AppleEverestLogPolicy.Set("DebugRelease", Celeste.Mod.LogLevel.Verbose);
Check(!Celeste.Mod.AppleEverestLogPolicy.ShouldLog("DebugReleaseModule", Celeste.Mod.LogLevel.Verbose) &&
      Celeste.Mod.AppleEverestLogPolicy.ShouldLog("DebugReleaseModule", Celeste.Mod.LogLevel.Info),
    "production log policy clamps third-party verbose requests to Info");

Runtime.CurrentOwner = "A";
On.Celeste.Dialog.Clean += A;
Check(Invoke() == "probe:argA:O:A", "one-hook argument and return modification");
Check(Runtime.Trace.SequenceEqual(["A-before", "original", "A-after"]), "one-hook orig trace");

Runtime.CurrentOwner = "B";
On.Celeste.Dialog.Clean += B;
Check(Invoke() == "probe:argA:O:A:B", "two-hook return nesting");
Check(Runtime.Trace.SequenceEqual(["B-before", "A-before", "original", "A-after", "B-after"]), "HookGen LIFO trace");

Runtime.SetEnabled("B", false);
Check(Invoke() == "probe:argA:O:A", "disable B return");
Check(Runtime.Trace.SequenceEqual(["A-before", "original", "A-after"]), "disable B ownership");
Runtime.SetEnabled("A", false);
Check(Invoke() == "probe:O", "disable all return");
Check(Runtime.Trace.SequenceEqual(["original"]), "disable all original only");
Runtime.SetEnabled("A", true);
Runtime.SetEnabled("B", true);
Check(Runtime.Trace.Count == 1 && Runtime.Trace[0] == "original", "enable changes no executable invocation");
Check(Invoke() == "probe:argA:O:A:B", "re-enable return");
Check(Runtime.Trace.SequenceEqual(["B-before", "A-before", "original", "A-after", "B-after"]), "re-enable exact chain");

Runtime.CurrentOwner = "Stop";
On.Celeste.Dialog.Clean += Stop;
Check(Invoke() == "probe:STOP", "no-orig return");
Check(Runtime.Trace.SequenceEqual(["stop"]), "no-orig suppresses inner chain");
On.Celeste.Dialog.Clean -= Stop;

On.Celeste.Dialog.hook_Clean explode = (_, _, _) => throw new InvalidOperationException("detour-exception");
On.Celeste.Dialog.Clean += explode;
bool propagated = false;
try { _ = Invoke(); }
catch (InvalidOperationException exception) when (exception.Message == "detour-exception") { propagated = true; }
Check(propagated, "handler exception propagates without compatibility wrapping");
On.Celeste.Dialog.Clean -= explode;

Runtime.CurrentOwner = "A";
On.Celeste.Dialog.Clean += A;
On.Celeste.Dialog.Clean += A;
Check(On.Celeste.Dialog.ActiveHandlerCount == 4, "duplicate subscriptions retained");
On.Celeste.Dialog.Clean -= A;
Check(On.Celeste.Dialog.ActiveHandlerCount == 3, "unsubscribe removes newest matching duplicate only");
On.Celeste.Dialog.Clean -= A;
On.Celeste.Dialog.Clean -= A;
Runtime.CurrentOwner = "B";
On.Celeste.Dialog.Clean -= B;
Check(On.Celeste.Dialog.ActiveHandlerCount == 0, "owner cleanup removes only owned handlers");
Check(Invoke() == "probe:O", "post-unload original");

Celeste.Player player = new();

Celeste.PlayerDeadBody DirectInvoke(int direction = 1)
{
    Runtime.Trace.Clear();
    return player.Die(direction, false, true);
}

Runtime.CurrentOwner = "DirectA";
using MonoMod.RuntimeDetour.Hook directA = new("A");
Check(directA.IsValid && directA.IsApplied, "direct Hook applies by default");
Check(DirectInvoke().Value == "2:False:True:O:A", "direct Hook argument and return mutation");
Check(Runtime.Trace.SequenceEqual(["A-before", "original", "A-after"]), "direct Hook orig trace");

directA.Undo();
Check(directA.IsValid && !directA.IsApplied, "direct Hook Undo state");
Check(DirectInvoke().Value == "1:False:True:O", "direct Hook Undo removes data registration");
directA.Apply();
Check(directA.IsApplied && DirectInvoke().Value.EndsWith(":A", StringComparison.Ordinal), "direct Hook Apply restores registration");

Runtime.CurrentOwner = "DirectB";
using MonoMod.RuntimeDetour.Hook directB = new("B");
Check(DirectInvoke().Value == "2:False:True:O:A:B", "two direct Hook return nesting");
Check(Runtime.Trace.SequenceEqual(["B-before", "A-before", "original", "A-after", "B-after"]), "two direct Hook LIFO order");

Runtime.CurrentOwner = "HookGen";
On.Celeste.Player.hook_Die eventHandler = (orig, self, direction, invincible, stats) =>
{
    Runtime.Trace.Add("event-before");
    Celeste.PlayerDeadBody body = orig(self, direction, invincible, stats);
    Runtime.Trace.Add("event-after");
    return new Celeste.PlayerDeadBody(body.Value + ":E");
};
On.Celeste.Player.Die += eventHandler;
Check(DirectInvoke().Value == "2:False:True:O:A:B:E", "HookGen and direct Hook share one chain");
Check(Runtime.Trace.SequenceEqual(["event-before", "B-before", "A-before", "original", "A-after", "B-after", "event-after"]),
    "mixed HookGen/direct insertion order");

Runtime.CurrentOwner = "Stop";
using MonoMod.RuntimeDetour.Hook directStop = new("Stop");
Check(DirectInvoke().Value == "STOP", "direct Hook may suppress orig");
Check(Runtime.Trace.SequenceEqual(["stop"]), "direct no-orig suppresses inner chain");
directStop.Dispose();
Check(!directStop.IsValid && !directStop.IsApplied, "disposed direct Hook state");
bool disposedApply = false;
try { directStop.Apply(); }
catch (ObjectDisposedException) { disposedApply = true; }
Check(disposedApply, "Apply after Dispose matches pinned exception behavior");

Runtime.CurrentOwner = "Deferred";
using MonoMod.RuntimeDetour.Hook deferred = new("Stop", applyByDefault: false);
Check(deferred.IsValid && !deferred.IsApplied, "applyByDefault false");
deferred.Apply();
Check(deferred.IsApplied && DirectInvoke().Value == "STOP", "deferred Apply");
deferred.Undo();

Runtime.CurrentOwner = "HookGen";
On.Celeste.Player.Die -= eventHandler;
directB.Dispose();
directA.Dispose();
Check(DirectInvoke().Value == "1:False:True:O", "all direct and HookGen registrations removed");

static void HotPassThrough(On.Celeste.HotUpdateTarget.orig_Update orig, Celeste.HotUpdateTarget self) => orig(self);
Celeste.HotUpdateTarget hotTarget = new();
Runtime.CurrentOwner = "HotUpdate";
On.Celeste.HotUpdateTarget.Update += HotPassThrough;
hotTarget.Update(); // Build and warm the cached delegate chain before measuring.
long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
for (int index = 0; index < 100_000; index++) hotTarget.Update();
long hotDispatchAllocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
Check(hotTarget.Ticks == 100_001 && hotDispatchAllocated <= 256,
    $"hot update cached dispatch allocation ({hotDispatchAllocated} bytes)");
On.Celeste.HotUpdateTarget.Update -= HotPassThrough;

Runtime.CurrentOwner = "OwnerCleanup";
using MonoMod.RuntimeDetour.Hook owned = new("A");
Celeste.Mod.GeneratedAppleEverestManagedDetourRegistry.RemoveOwner("OwnerCleanup");
Check(!owned.IsValid && !owned.IsApplied && DirectInvoke().Value == "1:False:True:O",
    "module owner cleanup invalidates only its direct registration");

Runtime.CurrentOwner = "Low";
using (Celeste.Mod.IAppleEverestManagedHookRegistration low =
       Celeste.Mod.GeneratedAppleEverestDirectHookRegistry.CreateConfiguredForTest(
           "A", new MonoMod.RuntimeDetour.DetourConfig("low", priority: -10)))
{
    Runtime.CurrentOwner = "High";
    using Celeste.Mod.IAppleEverestManagedHookRegistration high =
        Celeste.Mod.GeneratedAppleEverestDirectHookRegistry.CreateConfiguredForTest(
            "B", new MonoMod.RuntimeDetour.DetourConfig("high", priority: 10));
    Check(DirectInvoke().Value == "2:False:True:O:A:B", "configured priority return nesting");
    Check(Runtime.Trace.SequenceEqual(["B-before", "A-before", "original", "A-after", "B-after"]),
        "configured priority matches pinned desktop order");
}

Runtime.CurrentOwner = "Before";
using (Celeste.Mod.IAppleEverestManagedHookRegistration before =
       Celeste.Mod.GeneratedAppleEverestDirectHookRegistry.CreateConfiguredForTest(
           "A", new MonoMod.RuntimeDetour.DetourConfig("before", priority: -10, before: ["after"])))
{
    Runtime.CurrentOwner = "After";
    using Celeste.Mod.IAppleEverestManagedHookRegistration after =
        Celeste.Mod.GeneratedAppleEverestDirectHookRegistry.CreateConfiguredForTest(
            "B", new MonoMod.RuntimeDetour.DetourConfig("after", priority: 10));
    _ = DirectInvoke();
    Check(Runtime.Trace.SequenceEqual(["A-before", "B-before", "original", "B-after", "A-after"]),
        "Before constraint overrides priority");
}

Runtime.CurrentOwner = "CycleA";
using (Celeste.Mod.IAppleEverestManagedHookRegistration cycleA =
       Celeste.Mod.GeneratedAppleEverestDirectHookRegistry.CreateConfiguredForTest(
           "A", new MonoMod.RuntimeDetour.DetourConfig("cycle-a", before: ["cycle-b"])))
{
    Runtime.CurrentOwner = "CycleB";
    using Celeste.Mod.IAppleEverestManagedHookRegistration cycleB =
        Celeste.Mod.GeneratedAppleEverestDirectHookRegistry.CreateConfiguredForTest(
            "B", new MonoMod.RuntimeDetour.DetourConfig("cycle-b", before: ["cycle-a"]));
    bool cycleRejected = false;
    try { _ = DirectInvoke(); }
    catch (InvalidOperationException exception) when (exception.Message.Contains("cyclic", StringComparison.Ordinal))
    {
        cycleRejected = true;
    }
    Check(cycleRejected, "cyclic configured ordering fails closed");
}

Console.WriteLine($"PASS: production typed HookGen semantics ({passed})");
