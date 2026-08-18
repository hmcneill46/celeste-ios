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
    return On.Celeste.Dialog.Invoke(value, null, Original);
}

Check(Invoke() == "probe:O", "zero-hook return");
Check(Runtime.Trace.SequenceEqual(["original"]), "zero-hook original once");

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

Console.WriteLine($"PASS: production typed HookGen semantics ({passed})");
