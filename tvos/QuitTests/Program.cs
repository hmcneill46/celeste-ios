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
    QuitStateMachine value = new();
    Require(value.TryBegin(), "first request rejected");
    Require(value.State == QuitState.PreparingToLeave, "not preparing");
});
Test("duplicate Quit is suppressed", () =>
{
    QuitStateMachine value = new();
    Require(value.TryBegin() && !value.TryBegin(), "duplicate accepted");
});
Test("active save leaves state preparing", () =>
{
    QuitStateMachine value = new();
    value.TryBegin();
    Require(value.State == QuitState.PreparingToLeave, "busy save advanced state");
});
Test("successful flush reaches Leave ready", () =>
{
    QuitStateMachine value = new();
    value.TryBegin(); value.PreparationSucceeded();
    Require(value.State == QuitState.AwaitingBackground, "not awaiting background");
});
Test("failed flush remains live in visible failure", () =>
{
    QuitStateMachine value = new();
    value.TryBegin(); value.PreparationFailed();
    Require(value.State == QuitState.Failure, "failure not retained");
});
Test("Back from Leave restores inactive main menu", () =>
{
    QuitStateMachine value = new();
    value.TryBegin(); value.PreparationSucceeded();
    Require(value.Cancel() && value.State == QuitState.Inactive, "cancel failed");
});
Test("resign-active alone does not complete Leave", () =>
{
    QuitStateMachine value = new();
    value.TryBegin(); value.PreparationSucceeded(); value.WillResignActive();
    Require(value.State == QuitState.AwaitingBackground, "resign completed leave");
});
Test("actual background completes Leave", () =>
{
    QuitStateMachine value = new();
    value.TryBegin(); value.PreparationSucceeded();
    Require(value.DidEnterBackground() && value.State == QuitState.LeftViaBackground, "background not recorded");
});
Test("foreground after completed Leave restores main menu", () =>
{
    QuitStateMachine value = new();
    value.TryBegin(); value.PreparationSucceeded(); value.DidEnterBackground();
    Require(value.DidBecomeActive(false) == QuitForegroundAction.RestoreMainMenu, "main menu not requested");
    Require(value.State == QuitState.Inactive, "leave state not cleared");
});
Test("ordinary background preserves ordinary runtime state", () =>
{
    QuitStateMachine value = new();
    Require(!value.DidEnterBackground(), "ordinary background marked leave");
    Require(value.DidBecomeActive(false) == QuitForegroundAction.None, "ordinary foreground changed scene");
});
Test("restart-required foreground remains blocked", () =>
{
    QuitStateMachine value = new();
    Require(value.DidBecomeActive(true) == QuitForegroundAction.KeepRestartRequired, "restart block cleared");
    Require(value.State == QuitState.Inactive, "host leave state persisted");
});
Test("cold launch has no Leave state", () =>
{
    Require(new QuitStateMachine().State == QuitState.Inactive, "cold leave flag exists");
});
Test("completion requires background not repeated resign", () =>
{
    QuitStateMachine value = new();
    value.TryBegin(); value.PreparationSucceeded();
    for (int i = 0; i < 5; i++) value.WillResignActive();
    Require(value.State == QuitState.AwaitingBackground, "resign sequence completed leave");
});
Test("five Leave foreground cycles reuse one state machine", () =>
{
    QuitStateMachine value = new();
    for (int i = 0; i < 5; i++)
    {
        Require(value.TryBegin(), "cycle request failed");
        value.PreparationSucceeded();
        Require(value.DidEnterBackground(), "cycle background failed");
        Require(value.DidBecomeActive(false) == QuitForegroundAction.RestoreMainMenu, "cycle foreground failed");
    }
});
Test("failure can be retried without another runtime", () =>
{
    QuitStateMachine value = new();
    value.TryBegin(); value.PreparationFailed();
    Require(value.TryBegin(), "retry rejected");
    value.PreparationSucceeded();
    Require(value.State == QuitState.AwaitingBackground, "retry did not become ready");
});
Test("Back from failure returns to live runtime", () =>
{
    QuitStateMachine value = new();
    value.TryBegin(); value.PreparationFailed();
    Require(value.Cancel() && value.State == QuitState.Inactive, "failure cancel failed");
});

Console.WriteLine($"STAGE12B_QUIT_TESTS PASS count={count}");
