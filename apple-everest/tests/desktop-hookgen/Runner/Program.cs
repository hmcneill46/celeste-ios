using AppleEverestDesktopHookGen;

static int HookA(On.AppleEverestDesktopHookGen.Target.orig_Compute orig, int value)
{
    Target.Trace.Add("A-before");
    int result = orig(value + 10);
    Target.Trace.Add("A-after");
    return result + 100;
}

static int HookB(On.AppleEverestDesktopHookGen.Target.orig_Compute orig, int value)
{
    Target.Trace.Add("B-before");
    int result = orig(value + 1000);
    Target.Trace.Add("B-after");
    return result + 10000;
}

On.AppleEverestDesktopHookGen.Target.hook_Compute a = HookA;
On.AppleEverestDesktopHookGen.Target.hook_Compute b = HookB;
On.AppleEverestDesktopHookGen.Target.Compute += a;
On.AppleEverestDesktopHookGen.Target.Compute += b;

int both = Target.Compute(1);
string bothTrace = string.Join(",", Target.Trace);
if (both != 11112 || bothTrace != "B-before,A-before,original,A-after,B-after")
    throw new InvalidOperationException($"HookGen chain mismatch: {both}; {bothTrace}");

On.AppleEverestDesktopHookGen.Target.Compute -= b;
Target.Trace.Clear();
int onlyA = Target.Compute(1);
string onlyATrace = string.Join(",", Target.Trace);
if (onlyA != 112 || onlyATrace != "A-before,original,A-after")
    throw new InvalidOperationException($"HookGen lifetime mismatch: {onlyA}; {onlyATrace}");

On.AppleEverestDesktopHookGen.Target.Compute -= a;

static int DirectA(Func<int, int> orig, int value)
{
    Target.Trace.Add("A-before");
    int result = orig(value + 10);
    Target.Trace.Add("A-after");
    return result + 100;
}

static int DirectB(Func<int, int> orig, int value)
{
    Target.Trace.Add("B-before");
    int result = orig(value + 1000);
    Target.Trace.Add("B-after");
    return result + 10000;
}

static int DirectStop(Func<int, int> orig, int value)
{
    Target.Trace.Add("stop");
    return value + 7;
}

System.Reflection.MethodInfo directSource = typeof(Target).GetMethod(nameof(Target.DirectCompute))!;
System.Reflection.MethodInfo directTargetA = ((Func<Func<int, int>, int, int>)DirectA).Method;
System.Reflection.MethodInfo directTargetB = ((Func<Func<int, int>, int, int>)DirectB).Method;
System.Reflection.MethodInfo directTargetStop = ((Func<Func<int, int>, int, int>)DirectStop).Method;
using MonoMod.RuntimeDetour.Hook directA = new(directSource, directTargetA);
using MonoMod.RuntimeDetour.Hook directB = new(directSource, directTargetB);
Target.Trace.Clear();
int directBoth = Target.DirectCompute(1);
string directBothTrace = string.Join(",", Target.Trace);
if (directBoth != 11112 || directBothTrace != "B-before,A-before,original,A-after,B-after")
    throw new InvalidOperationException($"direct Hook chain mismatch: {directBoth}; {directBothTrace}");

directB.Undo();
Target.Trace.Clear();
int directOnlyA = Target.DirectCompute(1);
if (directOnlyA != 112 || string.Join(",", Target.Trace) != "A-before,original,A-after" || directB.IsApplied || !directB.IsValid)
    throw new InvalidOperationException("direct Hook Undo/state mismatch");
directB.Apply();

using (MonoMod.RuntimeDetour.Hook stop = new(directSource, directTargetStop))
{
    Target.Trace.Clear();
    int stopped = Target.DirectCompute(1);
    if (stopped != 8 || string.Join(",", Target.Trace) != "stop")
        throw new InvalidOperationException("direct Hook no-orig mismatch");
}

MonoMod.RuntimeDetour.Hook deferred = new(directSource, directTargetStop, config: null, applyByDefault: false);
if (deferred.IsApplied || !deferred.IsValid) throw new InvalidOperationException("direct Hook deferred state mismatch");
deferred.Apply();
deferred.Undo();
deferred.Dispose();
if (deferred.IsApplied || deferred.IsValid) throw new InvalidOperationException("direct Hook disposed state mismatch");
bool applyAfterDispose = false;
try { deferred.Apply(); }
catch (ObjectDisposedException) { applyAfterDispose = true; }
if (!applyAfterDispose) throw new InvalidOperationException("direct Hook Apply-after-Dispose mismatch");

directB.Dispose();
directA.Dispose();

using MonoMod.RuntimeDetour.Hook low = new(directSource, directTargetA,
    new MonoMod.RuntimeDetour.DetourConfig("low", priority: -10));
using MonoMod.RuntimeDetour.Hook high = new(directSource, directTargetB,
    new MonoMod.RuntimeDetour.DetourConfig("high", priority: 10));
Target.Trace.Clear();
_ = Target.DirectCompute(1);
string priorityTrace = string.Join(",", Target.Trace);
if (priorityTrace != "B-before,A-before,original,A-after,B-after")
    throw new InvalidOperationException($"direct Hook priority mismatch: {priorityTrace}");

Console.WriteLine($"APPLE_EVEREST_DESKTOP_HOOKGEN_PASS both={both} trace={bothTrace} onlyA={onlyA} onlyA-trace={onlyATrace}");
Console.WriteLine($"APPLE_EVEREST_DESKTOP_DIRECT_HOOK_PASS both={directBoth} trace={directBothTrace} priority={priorityTrace} lifecycle=PASS");
