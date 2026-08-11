#if TVOS_STAGE16B
using System;
using System.Threading;

namespace Celeste;

// Narrow generated bridge: generated Celeste sees only the requested display
// preference and startup SDL handle, never UIKit or CAMetalLayer details.
public static class TvOSPerformanceHudHooks
{
    public static Func<bool> EnabledRequested { get; set; }
    public static Action<bool> EnabledChanged { get; set; }
    public static Func<bool> AvailableRequested { get; set; }
    public static Action<IntPtr> StartupRequested { get; set; }
    public static Action BeforeFirstRenderRequested { get; set; }

    private static int beforeFirstRenderApplied;

    public static bool Enabled => EnabledRequested?.Invoke() ?? false;
    public static bool Available => AvailableRequested?.Invoke() ?? false;

    public static void SetEnabled(bool enabled) => EnabledChanged?.Invoke(enabled);
    public static void ApplyStartup(IntPtr sdlWindow) => StartupRequested?.Invoke(sdlWindow);
    public static void ApplyBeforeFirstRender()
    {
        if (Interlocked.Exchange(ref beforeFirstRenderApplied, 1) == 0)
            BeforeFirstRenderRequested?.Invoke();
    }

    public static void Reset()
    {
        EnabledRequested = null;
        EnabledChanged = null;
        AvailableRequested = null;
        StartupRequested = null;
        BeforeFirstRenderRequested = null;
        Volatile.Write(ref beforeFirstRenderApplied, 0);
    }
}
#endif
