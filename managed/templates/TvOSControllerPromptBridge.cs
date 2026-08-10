#if TVOS_STAGE11
using System;

namespace Celeste;

// Stable host-owned display preference. These values deliberately do not
// belong to the locked Celeste 1.4.0.0 Settings XML graph.
public enum TvOSControllerPromptMode
{
    Automatic = 0,
    Xbox = 1,
    PlayStation = 2,
    NintendoSwitch = 3,
    Stadia = 4
}

public static class TvOSControllerPromptHooks
{
    public static Func<TvOSControllerPromptMode> ModeRequested { get; set; }
    public static Action<TvOSControllerPromptMode> ModeChanged { get; set; }
    public static Func<string, string> PrefixRequested { get; set; }

    public static TvOSControllerPromptMode Mode =>
        ModeRequested?.Invoke() ?? TvOSControllerPromptMode.Automatic;

    public static void SetMode(TvOSControllerPromptMode mode) => ModeChanged?.Invoke(mode);

    public static string ResolvePrefix(string automaticPrefix) =>
        PrefixRequested?.Invoke(automaticPrefix) ?? automaticPrefix;

    public static string DisplayName(int value) => (TvOSControllerPromptMode)value switch
    {
        TvOSControllerPromptMode.Automatic => "Automatic",
        TvOSControllerPromptMode.Xbox => "Xbox",
        TvOSControllerPromptMode.PlayStation => "PlayStation",
        TvOSControllerPromptMode.NintendoSwitch => "Nintendo Switch",
        TvOSControllerPromptMode.Stadia => "Stadia",
        _ => "Automatic"
    };

    public static void Reset()
    {
        ModeRequested = null;
        ModeChanged = null;
        PrefixRequested = null;
    }
}
#endif
