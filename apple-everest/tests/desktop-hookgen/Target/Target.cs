namespace AppleEverestDesktopHookGen;

public static class Target
{
    public static readonly List<string> Trace = new();

    public static int Compute(int value)
    {
        Trace.Add("original");
        return value + 1;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static int DirectCompute(int value)
    {
        Trace.Add("original");
        return value + 1;
    }
}
