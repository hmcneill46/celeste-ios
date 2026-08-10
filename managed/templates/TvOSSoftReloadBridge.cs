#if TVOS_STAGE13B
using System;

namespace Celeste;

public enum TvOSSoftReloadPhase
{
    Inactive,
    RestartRequired,
    PreparingReload,
    ReloadingSettings,
    ReloadingGameState,
    WaitingForMainMenu,
    Verifying,
    Complete,
    Failure
}

public sealed class TvOSSoftReloadDisplayState
{
    public TvOSSoftReloadPhase Phase { get; init; } = TvOSSoftReloadPhase.Inactive;
    public string FailureCategory { get; init; } = "none";
    public bool CanRequest => Phase == TvOSSoftReloadPhase.RestartRequired;
    public bool IsActive => Phase is TvOSSoftReloadPhase.PreparingReload or
        TvOSSoftReloadPhase.ReloadingSettings or TvOSSoftReloadPhase.ReloadingGameState or
        TvOSSoftReloadPhase.WaitingForMainMenu or TvOSSoftReloadPhase.Verifying;
}

// This exact bridge is the only generated-Celeste surface exposed to the
// tvOS host reload coordinator.  It owns no persistence or runtime objects.
public static class TvOSSoftReloadHooks
{
    public static Func<bool>? ReloadRequested { get; set; }
    public static Func<TvOSSoftReloadDisplayState>? StatusRequested { get; set; }
    public static Action? UpdateRequested { get; set; }

    public static bool RequestReload() => ReloadRequested?.Invoke() ?? false;
    public static TvOSSoftReloadDisplayState Status() => StatusRequested?.Invoke() ?? new TvOSSoftReloadDisplayState();
    public static void Update() => UpdateRequested?.Invoke();

    public static void ResetHostCallbacks()
    {
        ReloadRequested = null;
        StatusRequested = null;
        UpdateRequested = null;
    }
}
#endif
