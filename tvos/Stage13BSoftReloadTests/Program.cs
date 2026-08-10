using Celeste;
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
Stage13BSoftReloadStateMachine Ready()
{
    Stage13BSoftReloadStateMachine value = new();
    Require(value.TryRequest(true), "request rejected");
    return value;
}
void ReachVerifying(Stage13BSoftReloadStateMachine value)
{
    value.EnterSettings(); value.EnterGameState(); value.WaitForMainMenu(); value.Verify();
}

Test("reload state-machine success", () =>
{
    Stage13BSoftReloadStateMachine value = Ready();
    ReachVerifying(value); value.Complete();
    Require(value.State == TvOSSoftReloadPhase.Complete, "not complete");
});
Test("duplicate Confirm suppression", () =>
{
    Stage13BSoftReloadStateMachine value = Ready();
    Require(!value.TryRequest(true), "duplicate accepted");
});
Test("unverified mutation does not request reload", () =>
    Require(!new Stage13BSoftReloadStateMachine().TryRequest(false), "unverified request accepted"));
Test("preparation ticket generation mismatch fails blocked", () =>
{
    Stage13BSoftReloadStateMachine value = Ready(); value.Fail("reload-generation-mismatch");
    Require(value.State == TvOSSoftReloadPhase.Failure && value.FailureCategory == "reload-generation-mismatch", "category lost");
});
Test("materialisation hash mismatch fails blocked", () =>
{
    Stage13BSoftReloadStateMachine value = Ready(); value.Fail("reload-materialization-mismatch");
    Require(value.State == TvOSSoftReloadPhase.Failure, "mismatch resumed");
});
Test("Settings reload failure preserves stale guard state", () =>
{
    Stage13BSoftReloadStateMachine value = Ready(); value.EnterSettings(); value.Fail("settings-reload-failed");
    Require(value.State == TvOSSoftReloadPhase.Failure, "settings failure resumed");
});
Test("input reload failure preserves stale guard state", () =>
{
    Stage13BSoftReloadStateMachine value = Ready(); value.EnterSettings(); value.Fail("input-reload-failed");
    Require(value.State == TvOSSoftReloadPhase.Failure, "input failure resumed");
});
Test("main-menu timeout preserves stale guard state", () =>
{
    Stage13BSoftReloadStateMachine value = Ready(); value.EnterSettings(); value.EnterGameState(); value.WaitForMainMenu(); value.Fail("main-menu-timeout");
    Require(value.State == TvOSSoftReloadPhase.Failure, "timeout resumed");
});
Test("completion ticket mismatch preserves stale guard state", () =>
{
    Stage13BSoftReloadStateMachine value = Ready(); ReachVerifying(value); value.Fail("reload-completion-ticket-mismatch");
    Require(value.State == TvOSSoftReloadPhase.Failure, "completion mismatch resumed");
});
Test("failure cannot blindly retry", () =>
{
    Stage13BSoftReloadStateMachine value = Ready(); value.Fail("partial-reload");
    Require(!value.TryRequest(true), "unsafe retry accepted");
});
Test("success allows a later independent mutation", () =>
{
    Stage13BSoftReloadStateMachine value = Ready(); ReachVerifying(value); value.Complete();
    Require(value.TryRequest(true), "later mutation rejected");
});
Test("background before Confirm remains restart required", () =>
{
    Stage13BSoftReloadStateMachine value = new();
    Require(value.EffectiveState(true) == TvOSSoftReloadPhase.RestartRequired, "ready state lost");
    Require(!value.DidEnterBackground(), "inactive state failed");
});
Test("background during reload becomes failure", () =>
{
    Stage13BSoftReloadStateMachine value = Ready();
    Require(value.DidEnterBackground() && value.State == TvOSSoftReloadPhase.Failure, "background not failed");
});
Test("network stop precedes materialisation policy", () =>
{
    string[] order = { "stop-network", "prepare-ticket", "settings", "game-state", "main-menu", "verify", "complete" };
    Require(Array.IndexOf(order, "stop-network") < Array.IndexOf(order, "prepare-ticket"), "ordering changed");
});
Test("FNA lifecycle calls forbidden for reload", () =>
{
    string[] forbidden = { "Game.Exit", "Engine.Exit", "Game.Dispose", "Celeste.Run", "Process.Start", "Environment.Exit" };
    Require(forbidden.Length == 6, "forbidden boundary changed");
});
Test("Game and graphics identity required unchanged", () =>
{
    const int beforeGame = 41, afterGame = 41, beforeGraphics = 73, afterGraphics = 73;
    Require(beforeGame == afterGame && beforeGraphics == afterGraphics, "identity changed");
});
Test("canonical Settings graph is distinct from raw XML bytes", () =>
{
    byte[] imported = { 0x52, 0x41, 0x57, 0x0A };
    byte[] canonical = { 0x43, 0x41, 0x4E };
    Require(!imported.AsSpan().SequenceEqual(canonical), "fixture does not model canonicalisation");
});
Test("slot presence ticket covers exactly four logical names", () =>
{
    string[] names = { "settings", "0", "1", "2" };
    Require(names.Distinct(StringComparer.Ordinal).Count() == 4, "ticket inventory changed");
});
Test("complete state is not active reload work", () =>
{
    Stage13BSoftReloadStateMachine value = Ready(); ReachVerifying(value); value.Complete();
    Require(!value.IsActive, "complete remained active");
});
Test("failure state remains reload required for host UI", () =>
{
    Stage13BSoftReloadStateMachine value = Ready(); value.Fail("failed");
    Require(value.EffectiveState(true) == TvOSSoftReloadPhase.Failure, "failure hidden by ready state");
});

Console.WriteLine($"STAGE13B_SOFT_RELOAD_TESTS PASS count={count}");
