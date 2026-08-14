namespace CelesteTvOSHost;

internal enum ControllerPromptMode
{
    Automatic = 0,
    Xbox = 1,
    PlayStation = 2,
    NintendoSwitch = 3,
    Stadia = 4
}

internal enum AppleControllerFamily
{
    Unknown,
    PlayStation,
    Xbox,
    Remote
}

internal readonly record struct ControllerCandidate(
    bool IsCurrent,
    bool HasExtendedGamepad,
    AppleControllerFamily Family,
    int StableOrder
);

internal interface IControllerPromptPreferenceStore
{
    string? Read();
    void Write(string value);
}

internal sealed class ControllerPromptPreferenceState
{
    private readonly IControllerPromptPreferenceStore store;

    internal ControllerPromptPreferenceState(IControllerPromptPreferenceStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        Mode = ControllerPromptPolicy.ParseStored(store.Read());
    }

    internal ControllerPromptMode Mode { get; private set; }

    internal bool Set(ControllerPromptMode mode)
    {
        if (!ControllerPromptPolicy.IsAllowed(mode)) mode = ControllerPromptMode.Automatic;
        if (Mode == mode) return false;
        Mode = mode;
        store.Write(ControllerPromptPolicy.StoredValue(mode));
        return true;
    }
}

internal static class ControllerPromptPolicy
{
    internal const string PreferenceKey = "CelesteTvOS.ControllerPrompts.v1";

    internal static bool IsAllowed(ControllerPromptMode mode) => mode is
        ControllerPromptMode.Automatic or ControllerPromptMode.Xbox or
        ControllerPromptMode.PlayStation or ControllerPromptMode.NintendoSwitch or
        ControllerPromptMode.Stadia;

    internal static ControllerPromptMode ParseStored(string? value) => value switch
    {
        "Automatic" => ControllerPromptMode.Automatic,
        "Xbox" => ControllerPromptMode.Xbox,
        "PlayStation" => ControllerPromptMode.PlayStation,
        "NintendoSwitch" => ControllerPromptMode.NintendoSwitch,
        "Stadia" => ControllerPromptMode.Stadia,
        _ => ControllerPromptMode.Automatic
    };

    internal static string StoredValue(ControllerPromptMode mode) => mode switch
    {
        ControllerPromptMode.Xbox => "Xbox",
        ControllerPromptMode.PlayStation => "PlayStation",
        ControllerPromptMode.NintendoSwitch => "NintendoSwitch",
        ControllerPromptMode.Stadia => "Stadia",
        _ => "Automatic"
    };

    internal static string ResolvePrefix(
        ControllerPromptMode mode,
        string automaticPrefix,
        AppleControllerFamily appleFamily)
    {
        string? manual = ManualPrefix(mode);
        if (manual != null) return manual;

        // Preserve Celeste's exact controller GUID knowledge first. The locked
        // game recognizes specific PlayStation, Nintendo and Stadia devices.
        if (automaticPrefix is "ps4" or "ns" or "stadia") return automaticPrefix;

        // The original unknown-controller fallback is xb1. Apple product
        // categories can safely distinguish a DualSense/DualShock or Xbox
        // controller before accepting that generic fallback.
        if (appleFamily == AppleControllerFamily.PlayStation) return "ps4";
        if (appleFamily == AppleControllerFamily.Xbox) return "xb1";

        return automaticPrefix is "keyboard" or "xb1" ? automaticPrefix : "xb1";
    }

    internal static string? ManualPrefix(ControllerPromptMode mode) => mode switch
    {
        ControllerPromptMode.Xbox => "xb1",
        ControllerPromptMode.PlayStation => "ps4",
        ControllerPromptMode.NintendoSwitch => "ns",
        ControllerPromptMode.Stadia => "stadia",
        _ => null
    };

    internal static AppleControllerFamily SelectAppleFamily(
        IEnumerable<ControllerCandidate> controllers)
    {
        ControllerCandidate[] eligible = controllers
            .Where(candidate => candidate.HasExtendedGamepad && candidate.Family != AppleControllerFamily.Remote)
            .OrderByDescending(candidate => candidate.IsCurrent)
            .ThenBy(candidate => candidate.StableOrder)
            .ToArray();
        return eligible.Length == 0 ? AppleControllerFamily.Unknown : eligible[0].Family;
    }
}
