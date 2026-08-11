namespace CelesteTvOSHost;

internal enum Stage16PerformanceHudMode
{
    Off,
    On
}

internal interface IStage16PerformanceHudPreferenceStore
{
    string? Read();
    void Write(string value);
}

internal sealed class Stage16PerformanceHudPreferenceState
{
    private readonly IStage16PerformanceHudPreferenceStore store;

    internal Stage16PerformanceHudPreferenceState(IStage16PerformanceHudPreferenceStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        Mode = Stage16PerformanceHudPolicy.ParseStored(store.Read());
    }

    internal Stage16PerformanceHudMode Mode { get; private set; }

    internal bool Set(Stage16PerformanceHudMode mode)
    {
        if (!Stage16PerformanceHudPolicy.IsAllowed(mode)) mode = Stage16PerformanceHudMode.Off;
        if (Mode == mode) return false;
        Mode = mode;
        store.Write(Stage16PerformanceHudPolicy.StoredValue(mode));
        return true;
    }
}

internal readonly record struct Stage16MetalLayerCandidate(
    IntPtr Identity,
    bool IsDirectPresentationLayer,
    bool IsMetalLayer,
    bool HasMetalDevice,
    bool DrawableSizeMatches
);

internal readonly record struct Stage16HudProperties(string Mode, string Logging);

internal static class Stage16PerformanceHudPolicy
{
    internal const string PreferenceKey = "CelesteTvOS.PerformanceHUD.v1";

    internal static bool IsAllowed(Stage16PerformanceHudMode mode) =>
        mode is Stage16PerformanceHudMode.Off or Stage16PerformanceHudMode.On;

    internal static Stage16PerformanceHudMode ParseStored(string? value) => value switch
    {
        "On" => Stage16PerformanceHudMode.On,
        _ => Stage16PerformanceHudMode.Off
    };

    internal static string StoredValue(Stage16PerformanceHudMode mode) =>
        mode == Stage16PerformanceHudMode.On ? "On" : "Off";

    internal static Stage16HudProperties Properties(Stage16PerformanceHudMode mode) =>
        new(mode == Stage16PerformanceHudMode.On ? "default" : "disabled", "disabled");

    // Apple's HUD renderer is foreground-only on tvOS. Keep the user's requested
    // mode in the preference, but always hide the native overlay before the app
    // resigns active and restore the request only after it becomes active again.
    internal static Stage16PerformanceHudMode ModeWhileInactive(
        Stage16PerformanceHudMode requested) => Stage16PerformanceHudMode.Off;

    internal static Stage16PerformanceHudMode ModeAfterForeground(
        Stage16PerformanceHudMode requested) =>
        IsAllowed(requested) ? requested : Stage16PerformanceHudMode.Off;

    internal static IntPtr? SelectPresentationLayer(IEnumerable<Stage16MetalLayerCandidate> candidates)
    {
        Stage16MetalLayerCandidate[] valid = candidates
            .Where(candidate => candidate.IsDirectPresentationLayer && candidate.IsMetalLayer &&
                candidate.HasMetalDevice && candidate.DrawableSizeMatches)
            .GroupBy(candidate => candidate.Identity)
            .Select(group => group.First())
            .ToArray();
        return valid.Length == 1 ? valid[0].Identity : null;
    }
}
