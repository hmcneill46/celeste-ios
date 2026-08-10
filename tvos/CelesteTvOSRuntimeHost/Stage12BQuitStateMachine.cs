namespace CelesteTvOSHost;

internal enum Stage12BQuitState
{
    Inactive,
    PreparingToLeave,
    AwaitingBackground,
    LeftViaBackground,
    Failure
}

internal enum Stage12BForegroundAction
{
    None,
    RestoreMainMenu,
    KeepRestartRequired
}

// Pure state policy. UIKit, Celeste, persistence, networking, and haptics are
// orchestrated by Stage12BQuitCoordinator so this policy stays deterministic.
internal sealed class Stage12BQuitStateMachine
{
    internal Stage12BQuitState State { get; private set; }

    internal bool TryBegin()
    {
        if (State is not (Stage12BQuitState.Inactive or Stage12BQuitState.Failure)) return false;
        State = Stage12BQuitState.PreparingToLeave;
        return true;
    }

    internal void PreparationSucceeded()
    {
        Require(Stage12BQuitState.PreparingToLeave);
        State = Stage12BQuitState.AwaitingBackground;
    }

    internal void PreparationFailed()
    {
        Require(Stage12BQuitState.PreparingToLeave);
        State = Stage12BQuitState.Failure;
    }

    internal bool Cancel()
    {
        if (State is Stage12BQuitState.Inactive or Stage12BQuitState.LeftViaBackground) return false;
        State = Stage12BQuitState.Inactive;
        return true;
    }

    internal bool DidEnterBackground()
    {
        if (State != Stage12BQuitState.AwaitingBackground) return false;
        State = Stage12BQuitState.LeftViaBackground;
        return true;
    }

    // Resign-active alone is not evidence that the player reached Home.
    internal void WillResignActive() { }

    internal Stage12BForegroundAction DidBecomeActive(bool restartRequired)
    {
        if (restartRequired) return Stage12BForegroundAction.KeepRestartRequired;
        if (State != Stage12BQuitState.LeftViaBackground) return Stage12BForegroundAction.None;
        State = Stage12BQuitState.Inactive;
        return Stage12BForegroundAction.RestoreMainMenu;
    }

    private void Require(Stage12BQuitState expected)
    {
        if (State != expected)
            throw new InvalidOperationException($"Stage 12B invalid transition from {State}; expected {expected}.");
    }
}
