#if (CELESTE_RUNTIME && TVOS_STAGE6_HOST) || STAGE13B_TESTS
using Celeste;

namespace CelesteTvOSHost;

internal sealed class Stage13BSoftReloadStateMachine
{
    internal TvOSSoftReloadPhase State { get; private set; } = TvOSSoftReloadPhase.Inactive;
    internal string FailureCategory { get; private set; } = "none";

    internal bool TryRequest(bool verifiedExternalMutation)
    {
        if (!verifiedExternalMutation || State is not (TvOSSoftReloadPhase.Inactive or TvOSSoftReloadPhase.Complete))
            return false;
        FailureCategory = "none";
        State = TvOSSoftReloadPhase.PreparingReload;
        return true;
    }

    internal void EnterSettings() => Move(TvOSSoftReloadPhase.PreparingReload, TvOSSoftReloadPhase.ReloadingSettings);
    internal void EnterGameState() => Move(TvOSSoftReloadPhase.ReloadingSettings, TvOSSoftReloadPhase.ReloadingGameState);
    internal void WaitForMainMenu() => Move(TvOSSoftReloadPhase.ReloadingGameState, TvOSSoftReloadPhase.WaitingForMainMenu);
    internal void Verify() => Move(TvOSSoftReloadPhase.WaitingForMainMenu, TvOSSoftReloadPhase.Verifying);
    internal void Complete() => Move(TvOSSoftReloadPhase.Verifying, TvOSSoftReloadPhase.Complete);

    internal bool DidEnterBackground()
    {
        if (!IsActive) return false;
        Fail("background-during-reload");
        return true;
    }

    internal void Fail(string category)
    {
        if (State == TvOSSoftReloadPhase.Failure) return;
        FailureCategory = string.IsNullOrWhiteSpace(category) ? "unknown" : category;
        State = TvOSSoftReloadPhase.Failure;
    }

    internal TvOSSoftReloadPhase EffectiveState(bool reloadRequired) =>
        reloadRequired && (State is TvOSSoftReloadPhase.Inactive or TvOSSoftReloadPhase.Complete)
            ? TvOSSoftReloadPhase.RestartRequired
            : State;

    internal bool IsActive => State is TvOSSoftReloadPhase.PreparingReload or
        TvOSSoftReloadPhase.ReloadingSettings or TvOSSoftReloadPhase.ReloadingGameState or
        TvOSSoftReloadPhase.WaitingForMainMenu or TvOSSoftReloadPhase.Verifying;

    private void Move(TvOSSoftReloadPhase expected, TvOSSoftReloadPhase next)
    {
        if (State != expected)
            throw new InvalidOperationException($"Invalid soft-reload transition {State} -> {next}; expected {expected}.");
        State = next;
    }
}
#endif
