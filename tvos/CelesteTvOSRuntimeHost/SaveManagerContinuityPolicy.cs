#if CELESTE_RUNTIME && TVOS_CELESTE_RUNTIME_HOST
namespace CelesteTvOSHost;

internal enum SaveManagerBrowserContinuityState
{
    Connected,
    Checking,
    Disconnected,
    Reopened,
    SessionExpired
}

internal readonly record struct SaveManagerBrowserContinuityTransition(
    SaveManagerBrowserContinuityState State,
    int ConsecutiveFailures,
    bool ControlsEnabled
);

internal static class SaveManagerContinuityPolicy
{
    // Deterministic project port in the IANA dynamic/private range. If another
    // process already owns it, one ephemeral listener is allowed for this
    // activation instead of making Save Manager unavailable.
    internal const ushort PreferredPort = 49728;
    internal const string ServiceIdentifier = "celeste-save-manager";
    internal const int ProtocolVersion = 1;
    internal const int InstanceBytes = 16;
    internal const int InstanceHexCharacters = InstanceBytes * 2;
    internal const int PollIntervalMilliseconds = 2000;
    internal const int PollTimeoutMilliseconds = 1500;
    internal const int DisconnectFailureThreshold = 3;
    internal const long DarwinAddressInUse = 48;

    internal static bool ShouldUseEphemeralFallback(
        bool preferredAttempt,
        bool fallbackAlreadyAttempted,
        bool errorDomainIsPosix,
        long errorCode) =>
        preferredAttempt && !fallbackAlreadyAttempted && errorDomainIsPosix && errorCode == DarwinAddressInUse;

    internal static bool IsValidInstanceId(string? value) =>
        value?.Length == InstanceHexCharacters &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    // This mirrors the tiny browser policy so its security-sensitive state
    // transitions can be exercised without requiring a browser engine.
    internal static SaveManagerBrowserContinuityTransition Transition(
        int priorFailures,
        bool responseValid,
        bool sameInstance,
        bool authenticated,
        bool authenticationRequired)
    {
        if (!responseValid)
        {
            int failures = Math.Min(DisconnectFailureThreshold, Math.Max(0, priorFailures) + 1);
            SaveManagerBrowserContinuityState state = failures >= DisconnectFailureThreshold
                ? SaveManagerBrowserContinuityState.Disconnected
                : SaveManagerBrowserContinuityState.Checking;
            return new(state, failures, false);
        }
        if (!sameInstance)
            return new(SaveManagerBrowserContinuityState.Reopened, 0, false);
        if (authenticationRequired && !authenticated)
            return new(SaveManagerBrowserContinuityState.SessionExpired, 0, false);
        return new(SaveManagerBrowserContinuityState.Connected, 0, true);
    }
}
#endif
