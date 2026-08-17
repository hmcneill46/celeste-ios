using CelesteAppleInput;

namespace CelesteTvOSHost;

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

// Platform storage stays tvOS-owned; the family model and resolution policy
// are shared with modern iOS in CelesteAppleInput.
internal static class ControllerPromptPolicy
{
    internal const string PreferenceKey = "CelesteTvOS.ControllerPrompts.v1";

    internal static bool IsAllowed(ControllerPromptMode mode) => AppleControllerPromptPolicy.IsAllowed(mode);
    internal static ControllerPromptMode ParseStored(string? value) => AppleControllerPromptPolicy.ParseStored(value);
    internal static string StoredValue(ControllerPromptMode mode) => AppleControllerPromptPolicy.StoredValue(mode);
    internal static string ResolvePrefix(ControllerPromptMode mode, string automaticPrefix, AppleControllerFamily family) =>
        AppleControllerPromptPolicy.ResolvePrefix(mode, automaticPrefix, family);
    internal static string? ManualPrefix(ControllerPromptMode mode) => AppleControllerPromptPolicy.ManualPrefix(mode);
    internal static AppleControllerFamily SelectAppleFamily(IEnumerable<ControllerCandidate> controllers) =>
        AppleControllerPromptPolicy.SelectAppleFamily(controllers);
}
