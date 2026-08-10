using CelesteTvOSHost;

int count = 0;
void Test(string name, Action body)
{
    body();
    count++;
    Console.WriteLine($"PASS {count:D2}: {name}");
}
void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

Test("idle begins one Quit request", () =>
{
    Stage12BQuitStateMachine value = new();
    Require(value.TryBegin(), "first request rejected");
    Require(value.State == Stage12BQuitState.PreparingToLeave, "not preparing");
});
Test("duplicate Quit is suppressed", () =>
{
    Stage12BQuitStateMachine value = new();
    Require(value.TryBegin() && !value.TryBegin(), "duplicate accepted");
});
Test("active save leaves state preparing", () =>
{
    Stage12BQuitStateMachine value = new();
    value.TryBegin();
    Require(value.State == Stage12BQuitState.PreparingToLeave, "busy save advanced state");
});
Test("successful flush reaches Leave ready", () =>
{
    Stage12BQuitStateMachine value = new();
    value.TryBegin(); value.PreparationSucceeded();
    Require(value.State == Stage12BQuitState.AwaitingBackground, "not awaiting background");
});
Test("failed flush remains live in visible failure", () =>
{
    Stage12BQuitStateMachine value = new();
    value.TryBegin(); value.PreparationFailed();
    Require(value.State == Stage12BQuitState.Failure, "failure not retained");
});
Test("Back from Leave restores inactive main menu", () =>
{
    Stage12BQuitStateMachine value = new();
    value.TryBegin(); value.PreparationSucceeded();
    Require(value.Cancel() && value.State == Stage12BQuitState.Inactive, "cancel failed");
});
Test("resign-active alone does not complete Leave", () =>
{
    Stage12BQuitStateMachine value = new();
    value.TryBegin(); value.PreparationSucceeded(); value.WillResignActive();
    Require(value.State == Stage12BQuitState.AwaitingBackground, "resign completed leave");
});
Test("actual background completes Leave", () =>
{
    Stage12BQuitStateMachine value = new();
    value.TryBegin(); value.PreparationSucceeded();
    Require(value.DidEnterBackground() && value.State == Stage12BQuitState.LeftViaBackground, "background not recorded");
});
Test("foreground after completed Leave restores main menu", () =>
{
    Stage12BQuitStateMachine value = new();
    value.TryBegin(); value.PreparationSucceeded(); value.DidEnterBackground();
    Require(value.DidBecomeActive(false) == Stage12BForegroundAction.RestoreMainMenu, "main menu not requested");
    Require(value.State == Stage12BQuitState.Inactive, "leave state not cleared");
});
Test("ordinary background preserves ordinary runtime state", () =>
{
    Stage12BQuitStateMachine value = new();
    Require(!value.DidEnterBackground(), "ordinary background marked leave");
    Require(value.DidBecomeActive(false) == Stage12BForegroundAction.None, "ordinary foreground changed scene");
});
Test("restart-required foreground remains blocked", () =>
{
    Stage12BQuitStateMachine value = new();
    Require(value.DidBecomeActive(true) == Stage12BForegroundAction.KeepRestartRequired, "restart block cleared");
    Require(value.State == Stage12BQuitState.Inactive, "host leave state persisted");
});
Test("cold launch has no Leave state", () =>
{
    Require(new Stage12BQuitStateMachine().State == Stage12BQuitState.Inactive, "cold leave flag exists");
});
Test("completion requires background not repeated resign", () =>
{
    Stage12BQuitStateMachine value = new();
    value.TryBegin(); value.PreparationSucceeded();
    for (int i = 0; i < 5; i++) value.WillResignActive();
    Require(value.State == Stage12BQuitState.AwaitingBackground, "resign sequence completed leave");
});
Test("five Leave foreground cycles reuse one state machine", () =>
{
    Stage12BQuitStateMachine value = new();
    for (int i = 0; i < 5; i++)
    {
        Require(value.TryBegin(), "cycle request failed");
        value.PreparationSucceeded();
        Require(value.DidEnterBackground(), "cycle background failed");
        Require(value.DidBecomeActive(false) == Stage12BForegroundAction.RestoreMainMenu, "cycle foreground failed");
    }
});
Test("failure can be retried without another runtime", () =>
{
    Stage12BQuitStateMachine value = new();
    value.TryBegin(); value.PreparationFailed();
    Require(value.TryBegin(), "retry rejected");
    value.PreparationSucceeded();
    Require(value.State == Stage12BQuitState.AwaitingBackground, "retry did not become ready");
});
Test("Back from failure returns to live runtime", () =>
{
    Stage12BQuitStateMachine value = new();
    value.TryBegin(); value.PreparationFailed();
    Require(value.Cancel() && value.State == Stage12BQuitState.Inactive, "failure cancel failed");
});

Console.WriteLine($"STAGE12B_QUIT_TESTS PASS count={count}");
