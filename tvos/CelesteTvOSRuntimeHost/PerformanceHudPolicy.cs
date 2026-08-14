namespace CelesteTvOSHost;

internal enum PerformanceHudMode
{
    Off,
    On
}

internal interface IPerformanceHudPreferenceStore
{
    string? Read();
    void Write(string value);
}

internal sealed class PerformanceHudPreferenceState
{
    private readonly IPerformanceHudPreferenceStore store;

    internal PerformanceHudPreferenceState(IPerformanceHudPreferenceStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        Mode = PerformanceHudPolicy.ParseStored(store.Read());
    }

    internal PerformanceHudMode Mode { get; private set; }

    internal bool Set(PerformanceHudMode mode)
    {
        if (!PerformanceHudPolicy.IsAllowed(mode)) mode = PerformanceHudMode.Off;
        if (Mode == mode) return false;
        Mode = mode;
        store.Write(PerformanceHudPolicy.StoredValue(mode));
        return true;
    }
}

internal readonly record struct MetalLayerCandidate(
    IntPtr Identity,
    bool IsDirectPresentationLayer,
    bool IsMetalLayer,
    bool HasMetalDevice,
    bool DrawableSizeMatches
);

internal readonly record struct PerformanceHudProperties(string Mode, string Logging);

internal static class PerformanceHudPolicy
{
    internal const string PreferenceKey = "CelesteTvOS.PerformanceHUD.v1";

    internal static bool IsAllowed(PerformanceHudMode mode) =>
        mode is PerformanceHudMode.Off or PerformanceHudMode.On;

    internal static PerformanceHudMode ParseStored(string? value) => value switch
    {
        "On" => PerformanceHudMode.On,
        _ => PerformanceHudMode.Off
    };

    internal static string StoredValue(PerformanceHudMode mode) =>
        mode == PerformanceHudMode.On ? "On" : "Off";

    internal static PerformanceHudProperties Properties(PerformanceHudMode mode) =>
        new(mode == PerformanceHudMode.On ? "default" : "disabled", "disabled");

    // Apple's HUD renderer is foreground-only on tvOS. Keep the user's requested
    // mode in the preference, but always hide the native overlay before the app
    // resigns active and restore the request only after it becomes active again.
    internal static PerformanceHudMode ModeWhileInactive(
        PerformanceHudMode requested) => PerformanceHudMode.Off;

    internal static PerformanceHudMode ModeAfterForeground(
        PerformanceHudMode requested) =>
        IsAllowed(requested) ? requested : PerformanceHudMode.Off;

    internal static IntPtr? SelectPresentationLayer(IEnumerable<MetalLayerCandidate> candidates)
    {
        MetalLayerCandidate[] valid = candidates
            .Where(candidate => candidate.IsDirectPresentationLayer && candidate.IsMetalLayer &&
                candidate.HasMetalDevice && candidate.DrawableSizeMatches)
            .GroupBy(candidate => candidate.Identity)
            .Select(group => group.First())
            .ToArray();
        return valid.Length == 1 ? valid[0].Identity : null;
    }
}
