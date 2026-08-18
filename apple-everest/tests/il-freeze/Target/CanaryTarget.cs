using System.Runtime.CompilerServices;

namespace AppleEverest.IlFreeze;

public static class CanaryTarget
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Scale(int value) => value * 2;
}
