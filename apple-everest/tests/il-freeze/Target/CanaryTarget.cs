using System.Runtime.CompilerServices;

namespace AppleEverest.IlFreeze;

public static class CanaryTarget
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Scale(int value) => value * 2;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int StaticCall(int value) => value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Branch(int value) => value + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int AlterReturn(int value) => value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int LocalRoundTrip(int value) => value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int RemoveRoundTrip(int value) => value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Bump(int value) => value + 4;
}
