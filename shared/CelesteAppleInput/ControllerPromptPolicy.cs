namespace CelesteAppleInput;

public enum ControllerPromptMode
{
    Automatic = 0,
    Xbox = 1,
    PlayStation = 2,
    NintendoSwitch = 3,
    Stadia = 4
}

public enum AppleControllerFamily
{
    Unknown,
    PlayStation,
    Xbox,
    Remote
}

public readonly record struct ControllerCandidate(
    bool IsCurrent,
    bool HasExtendedGamepad,
    AppleControllerFamily Family,
    int StableOrder);

public static class AppleControllerPromptPolicy
{
    public static bool IsAllowed(ControllerPromptMode mode) => mode is
        ControllerPromptMode.Automatic or ControllerPromptMode.Xbox or
        ControllerPromptMode.PlayStation or ControllerPromptMode.NintendoSwitch or
        ControllerPromptMode.Stadia;

    public static ControllerPromptMode ParseStored(string? value) => value switch
    {
        "Automatic" => ControllerPromptMode.Automatic,
        "Xbox" => ControllerPromptMode.Xbox,
        "PlayStation" => ControllerPromptMode.PlayStation,
        "NintendoSwitch" => ControllerPromptMode.NintendoSwitch,
        "Stadia" => ControllerPromptMode.Stadia,
        _ => ControllerPromptMode.Automatic
    };

    public static string StoredValue(ControllerPromptMode mode) => mode switch
    {
        ControllerPromptMode.Xbox => "Xbox",
        ControllerPromptMode.PlayStation => "PlayStation",
        ControllerPromptMode.NintendoSwitch => "NintendoSwitch",
        ControllerPromptMode.Stadia => "Stadia",
        _ => "Automatic"
    };

    public static string DisplayName(ControllerPromptMode mode) => mode switch
    {
        ControllerPromptMode.Xbox => "Xbox",
        ControllerPromptMode.PlayStation => "PlayStation",
        ControllerPromptMode.NintendoSwitch => "Nintendo Switch",
        ControllerPromptMode.Stadia => "Stadia",
        _ => "Automatic"
    };

    public static string ResolvePrefix(
        ControllerPromptMode mode,
        string automaticPrefix,
        AppleControllerFamily appleFamily)
    {
        string? manual = ManualPrefix(mode);
        if (manual != null) return manual;

        // Preserve Celeste's exact GUID knowledge before using the narrower
        // Apple product-family signal for otherwise-generic controllers.
        if (automaticPrefix is "ps4" or "ns" or "stadia") return automaticPrefix;
        if (appleFamily == AppleControllerFamily.PlayStation) return "ps4";
        if (appleFamily == AppleControllerFamily.Xbox) return "xb1";
        return automaticPrefix is "keyboard" or "xb1" ? automaticPrefix : "xb1";
    }

    public static string? ManualPrefix(ControllerPromptMode mode) => mode switch
    {
        ControllerPromptMode.Xbox => "xb1",
        ControllerPromptMode.PlayStation => "ps4",
        ControllerPromptMode.NintendoSwitch => "ns",
        ControllerPromptMode.Stadia => "stadia",
        _ => null
    };

    public static AppleControllerFamily SelectAppleFamily(IEnumerable<ControllerCandidate> controllers)
    {
        ControllerCandidate[] eligible = controllers
            .Where(candidate => candidate.HasExtendedGamepad && candidate.Family != AppleControllerFamily.Remote)
            .OrderByDescending(candidate => candidate.IsCurrent)
            .ThenBy(candidate => candidate.StableOrder)
            .ToArray();
        return eligible.Length == 0 ? AppleControllerFamily.Unknown : eligible[0].Family;
    }
}
