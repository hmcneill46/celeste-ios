using System.Runtime.CompilerServices;

namespace CelesteManagedAotClosure;

internal static class Program
{
    private static readonly Action<string[]> CelesteEntryBoundary = global::Celeste.Celeste.Run;

    public static int Main(string[] args)
    {
        // Root the Stage 3B callable boundary and its reachable managed graph.
        // The game entrypoint is deliberately never invoked in Stage 3A.
        GC.KeepAlive(CelesteEntryBoundary);
        GC.KeepAlive(typeof(global::Celeste.Content.Stage3AContentIdentity).Assembly);
        return args.Length == int.MinValue ? 1 : 0;
    }
}
