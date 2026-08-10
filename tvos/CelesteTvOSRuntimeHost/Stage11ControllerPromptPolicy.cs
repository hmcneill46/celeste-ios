namespace CelesteTvOSHost;

internal enum Stage11PromptMode
{
    Automatic = 0,
    Xbox = 1,
    PlayStation = 2,
    NintendoSwitch = 3,
    Stadia = 4
}

internal enum Stage11AppleControllerFamily
{
    Unknown,
    PlayStation,
    Xbox,
    Remote
}

internal readonly record struct Stage11ControllerCandidate(
    bool IsCurrent,
    bool HasExtendedGamepad,
    Stage11AppleControllerFamily Family,
    int StableOrder
);

internal interface IStage11PromptPreferenceStore
{
    string? Read();
    void Write(string value);
}

internal sealed class Stage11PromptPreferenceState
{
    private readonly IStage11PromptPreferenceStore store;

    internal Stage11PromptPreferenceState(IStage11PromptPreferenceStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        Mode = Stage11ControllerPromptPolicy.ParseStored(store.Read());
    }

    internal Stage11PromptMode Mode { get; private set; }

    internal bool Set(Stage11PromptMode mode)
    {
        if (!Stage11ControllerPromptPolicy.IsAllowed(mode)) mode = Stage11PromptMode.Automatic;
        if (Mode == mode) return false;
        Mode = mode;
        store.Write(Stage11ControllerPromptPolicy.StoredValue(mode));
        return true;
    }
}

internal static class Stage11ControllerPromptPolicy
{
    internal const string PreferenceKey = "CelesteTvOS.ControllerPrompts.v1";

    internal static bool IsAllowed(Stage11PromptMode mode) => mode is
        Stage11PromptMode.Automatic or Stage11PromptMode.Xbox or
        Stage11PromptMode.PlayStation or Stage11PromptMode.NintendoSwitch or
        Stage11PromptMode.Stadia;

    internal static Stage11PromptMode ParseStored(string? value) => value switch
    {
        "Automatic" => Stage11PromptMode.Automatic,
        "Xbox" => Stage11PromptMode.Xbox,
        "PlayStation" => Stage11PromptMode.PlayStation,
        "NintendoSwitch" => Stage11PromptMode.NintendoSwitch,
        "Stadia" => Stage11PromptMode.Stadia,
        _ => Stage11PromptMode.Automatic
    };

    internal static string StoredValue(Stage11PromptMode mode) => mode switch
    {
        Stage11PromptMode.Xbox => "Xbox",
        Stage11PromptMode.PlayStation => "PlayStation",
        Stage11PromptMode.NintendoSwitch => "NintendoSwitch",
        Stage11PromptMode.Stadia => "Stadia",
        _ => "Automatic"
    };

    internal static string ResolvePrefix(
        Stage11PromptMode mode,
        string automaticPrefix,
        Stage11AppleControllerFamily appleFamily)
    {
        string? manual = ManualPrefix(mode);
        if (manual != null) return manual;

        // Preserve Celeste's exact controller GUID knowledge first. The locked
        // game recognizes specific PlayStation, Nintendo and Stadia devices.
        if (automaticPrefix is "ps4" or "ns" or "stadia") return automaticPrefix;

        // The original unknown-controller fallback is xb1. Apple product
        // categories can safely distinguish a DualSense/DualShock or Xbox
        // controller before accepting that generic fallback.
        if (appleFamily == Stage11AppleControllerFamily.PlayStation) return "ps4";
        if (appleFamily == Stage11AppleControllerFamily.Xbox) return "xb1";

        return automaticPrefix is "keyboard" or "xb1" ? automaticPrefix : "xb1";
    }

    internal static string? ManualPrefix(Stage11PromptMode mode) => mode switch
    {
        Stage11PromptMode.Xbox => "xb1",
        Stage11PromptMode.PlayStation => "ps4",
        Stage11PromptMode.NintendoSwitch => "ns",
        Stage11PromptMode.Stadia => "stadia",
        _ => null
    };

    internal static Stage11AppleControllerFamily SelectAppleFamily(
        IEnumerable<Stage11ControllerCandidate> controllers)
    {
        Stage11ControllerCandidate[] eligible = controllers
            .Where(candidate => candidate.HasExtendedGamepad && candidate.Family != Stage11AppleControllerFamily.Remote)
            .OrderByDescending(candidate => candidate.IsCurrent)
            .ThenBy(candidate => candidate.StableOrder)
            .ToArray();
        return eligible.Length == 0 ? Stage11AppleControllerFamily.Unknown : eligible[0].Family;
    }
}
