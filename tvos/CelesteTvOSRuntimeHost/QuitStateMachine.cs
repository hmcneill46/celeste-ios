namespace CelesteTvOSHost;

internal enum QuitState
{
    Inactive,
    PreparingToLeave,
    AwaitingBackground,
    LeftViaBackground,
    Failure
}

internal enum QuitForegroundAction
{
    None,
    RestoreMainMenu,
    KeepRestartRequired
}

// Pure state policy. UIKit, Celeste, persistence, networking, and haptics are
// orchestrated by QuitCoordinator so this policy stays deterministic.
internal sealed class QuitStateMachine
{
    internal QuitState State { get; private set; }

    internal bool TryBegin()
    {
        if (State is not (QuitState.Inactive or QuitState.Failure)) return false;
        State = QuitState.PreparingToLeave;
        return true;
    }

    internal void PreparationSucceeded()
    {
        Require(QuitState.PreparingToLeave);
        State = QuitState.AwaitingBackground;
    }

    internal void PreparationFailed()
    {
        Require(QuitState.PreparingToLeave);
        State = QuitState.Failure;
    }

    internal bool Cancel()
    {
        if (State is QuitState.Inactive or QuitState.LeftViaBackground) return false;
        State = QuitState.Inactive;
        return true;
    }

    internal bool DidEnterBackground()
    {
        if (State != QuitState.AwaitingBackground) return false;
        State = QuitState.LeftViaBackground;
        return true;
    }

    // Resign-active alone is not evidence that the player reached Home.
    internal void WillResignActive() { }

    internal QuitForegroundAction DidBecomeActive(bool restartRequired)
    {
        if (restartRequired) return QuitForegroundAction.KeepRestartRequired;
        if (State != QuitState.LeftViaBackground) return QuitForegroundAction.None;
        State = QuitState.Inactive;
        return QuitForegroundAction.RestoreMainMenu;
    }

    private void Require(QuitState expected)
    {
        if (State != expected)
            throw new InvalidOperationException($"Quit state machine invalid transition from {State}; expected {expected}.");
    }
}
