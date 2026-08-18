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
Console.WriteLine($"APPLE_EVEREST_DESKTOP_HOOKGEN_PASS both={both} trace={bothTrace} onlyA={onlyA} onlyA-trace={onlyATrace}");
