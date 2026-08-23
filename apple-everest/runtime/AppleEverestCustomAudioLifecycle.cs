using System;

#nullable enable

namespace Celeste.Mod;

internal enum AppleEverestCustomBankState
{
    NotLoaded,
    Loading,
    Loaded,
    Unloaded
}

/// <summary>
/// Small deterministic state model shared by the production custom-audio
/// registry and host-side lifecycle tests. It deliberately knows nothing
/// about FMOD discovery or files; the caller supplies the one existing Studio
/// System identity and the exact generated bank count.
/// </summary>
internal sealed class AppleEverestCustomAudioLifecycle
{
    private object? activeSystem;

    internal AppleEverestCustomBankState State { get; private set; } = AppleEverestCustomBankState.NotLoaded;
    internal int LoadedBankCount { get; private set; }

    internal bool BeginLoad(object system, int expectedBankCount)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (expectedBankCount < 0) throw new ArgumentOutOfRangeException(nameof(expectedBankCount));
        if (expectedBankCount == 0) return false;
        if (State == AppleEverestCustomBankState.Loaded && ReferenceEquals(activeSystem, system) &&
            LoadedBankCount == expectedBankCount) return false;
        if (State is AppleEverestCustomBankState.Loading or AppleEverestCustomBankState.Loaded ||
            activeSystem != null || LoadedBankCount != 0)
            throw new InvalidOperationException("custom FMOD lifecycle already belongs to a live Studio System");
        activeSystem = system;
        State = AppleEverestCustomBankState.Loading;
        return true;
    }

    internal void CompleteLoad(object system, int loadedBankCount)
    {
        if (State != AppleEverestCustomBankState.Loading || !ReferenceEquals(activeSystem, system) || loadedBankCount <= 0)
            throw new InvalidOperationException("custom FMOD lifecycle completion is invalid");
        LoadedBankCount = loadedBankCount;
        State = AppleEverestCustomBankState.Loaded;
    }

    internal void FailLoad(object system)
    {
        if (activeSystem != null && !ReferenceEquals(activeSystem, system))
            throw new InvalidOperationException("custom FMOD lifecycle failure received the wrong Studio System");
        activeSystem = null;
        LoadedBankCount = 0;
        State = AppleEverestCustomBankState.NotLoaded;
    }

    internal bool Owns(object system) =>
        State == AppleEverestCustomBankState.Loaded && ReferenceEquals(activeSystem, system);

    internal void SoftReload(object system)
    {
        if (!Owns(system))
            throw new InvalidOperationException("custom FMOD soft reload received the wrong Studio System");
    }

    internal int BeforeSystemUnload(object system)
    {
        if (activeSystem == null && LoadedBankCount == 0) return 0;
        if (!ReferenceEquals(activeSystem, system))
            throw new InvalidOperationException("custom FMOD teardown received the wrong Studio System");
        int count = LoadedBankCount;
        activeSystem = null;
        LoadedBankCount = 0;
        State = AppleEverestCustomBankState.Unloaded;
        return count;
    }
}
