using System.Runtime.CompilerServices;

namespace AppleEverest.IlCompose;

public static class ComposeTarget
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Compose(int value) => value * 2;
}
