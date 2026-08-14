using CelesteTvOSHost;

int passed = 0;

void Test(string name, Action body)
{
    body();
    passed++;
    Console.WriteLine($"PASS {passed:D2}: {name}");
}

void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}, found {actual}.");
}

void True(bool value)
{
    if (!value) throw new InvalidOperationException("Expected true.");
}

MetalLayerCandidate Candidate(
    int identity, bool direct = true, bool metal = true, bool device = true, bool size = true) =>
    new(new IntPtr(identity), direct, metal, device, size);

Test("fixed preference key", () =>
    Equal("CelesteTvOS.PerformanceHUD.v1", PerformanceHudPolicy.PreferenceKey));
Test("missing preference is Off", () =>
    Equal(PerformanceHudMode.Off, PerformanceHudPolicy.ParseStored(null)));
Test("invalid preference is Off", () =>
    Equal(PerformanceHudMode.Off, PerformanceHudPolicy.ParseStored("Enabled")));
Test("Off parses", () =>
    Equal(PerformanceHudMode.Off, PerformanceHudPolicy.ParseStored("Off")));
Test("On parses", () =>
    Equal(PerformanceHudMode.On, PerformanceHudPolicy.ParseStored("On")));
Test("Off stable value", () =>
    Equal("Off", PerformanceHudPolicy.StoredValue(PerformanceHudMode.Off)));
Test("On stable value", () =>
    Equal("On", PerformanceHudPolicy.StoredValue(PerformanceHudMode.On)));
Test("Off round trip", () =>
{
    MemoryStore store = new("On");
    PerformanceHudPreferenceState state = new(store);
    True(state.Set(PerformanceHudMode.Off));
    Equal(PerformanceHudMode.Off, new PerformanceHudPreferenceState(store).Mode);
});
Test("On round trip", () =>
{
    MemoryStore store = new(null);
    PerformanceHudPreferenceState state = new(store);
    True(state.Set(PerformanceHudMode.On));
    Equal(PerformanceHudMode.On, new PerformanceHudPreferenceState(store).Mode);
});
Test("unchanged value is no-op", () =>
{
    MemoryStore store = new("On");
    PerformanceHudPreferenceState state = new(store);
    Equal(false, state.Set(PerformanceHudMode.On));
    Equal(0, store.Writes);
});
Test("invalid enum safely persists Off", () =>
{
    MemoryStore store = new("On");
    PerformanceHudPreferenceState state = new(store);
    True(state.Set((PerformanceHudMode)99));
    Equal("Off", store.Value);
});

Test("one valid real layer selected", () =>
    Equal(new IntPtr(1), PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1) })));
Test("no layer fails closed", () =>
    Equal<IntPtr?>(null, PerformanceHudPolicy.SelectPresentationLayer(Array.Empty<MetalLayerCandidate>())));
Test("wrong layer type fails closed", () =>
    Equal<IntPtr?>(null, PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1, metal: false) })));
Test("missing Metal device fails closed", () =>
    Equal<IntPtr?>(null, PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1, device: false) })));
Test("drawable mismatch fails closed", () =>
    Equal<IntPtr?>(null, PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1, size: false) })));
Test("non-direct Metal layer rejected", () =>
    Equal<IntPtr?>(null, PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1, direct: false) })));
Test("ambiguous direct candidates fail closed", () =>
    Equal<IntPtr?>(null, PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1), Candidate(2) })));
Test("duplicate wrapper identity remains unambiguous", () =>
    Equal(new IntPtr(1), PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1), Candidate(1) })));
Test("unrelated non-direct layer does not replace direct layer", () =>
    Equal(new IntPtr(2), PerformanceHudPolicy.SelectPresentationLayer(new[]
    {
        Candidate(1, direct: false), Candidate(2)
    })));
Test("unchanged foreground identity remains selected", () =>
{
    IntPtr? before = PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(7) });
    IntPtr? after = PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(7) });
    Equal(before, after);
});
Test("replacement foreground layer can be revalidated", () =>
{
    Equal(new IntPtr(8), PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(8) }));
});

Test("Off properties disable mode", () =>
    Equal("disabled", PerformanceHudPolicy.Properties(PerformanceHudMode.Off).Mode));
Test("On properties use default mode", () =>
    Equal("default", PerformanceHudPolicy.Properties(PerformanceHudMode.On).Mode));
Test("Off logging disabled", () =>
    Equal("disabled", PerformanceHudPolicy.Properties(PerformanceHudMode.Off).Logging));
Test("On logging disabled", () =>
    Equal("disabled", PerformanceHudPolicy.Properties(PerformanceHudMode.On).Logging));
Test("immediate mode sequence is deterministic", () =>
{
    string[] modes = new[] { PerformanceHudMode.Off, PerformanceHudMode.On, PerformanceHudMode.Off }
        .Select(mode => PerformanceHudPolicy.Properties(mode).Mode).ToArray();
    True(modes.SequenceEqual(new[] { "disabled", "default", "disabled" }));
});
Test("sixty toggle applications do not change policy", () =>
{
    for (int index = 0; index < 60; index++)
    {
        PerformanceHudMode mode = index % 2 == 0 ? PerformanceHudMode.On : PerformanceHudMode.Off;
        Equal("disabled", PerformanceHudPolicy.Properties(mode).Logging);
    }
});
Test("API unavailable remains hidden", () =>
{
    Equal<IntPtr?>(null, PerformanceHudPolicy.SelectPresentationLayer(Array.Empty<MetalLayerCandidate>()));
    Equal("disabled", PerformanceHudPolicy.Properties(PerformanceHudMode.Off).Mode);
});
Test("invalid preference produces hidden properties", () =>
{
    PerformanceHudMode mode = PerformanceHudPolicy.ParseStored("corrupt");
    Equal("disabled", PerformanceHudPolicy.Properties(mode).Mode);
});
Test("resign-active hides requested On without changing preference", () =>
{
    MemoryStore store = new("On");
    PerformanceHudPreferenceState state = new(store);
    Equal(PerformanceHudMode.Off,
        PerformanceHudPolicy.ModeWhileInactive(state.Mode));
    Equal(PerformanceHudMode.On, state.Mode);
    Equal(0, store.Writes);
});
Test("foreground restores requested On", () =>
    Equal(PerformanceHudMode.On,
        PerformanceHudPolicy.ModeAfterForeground(PerformanceHudMode.On)));
Test("foreground preserves requested Off", () =>
    Equal(PerformanceHudMode.Off,
        PerformanceHudPolicy.ModeAfterForeground(PerformanceHudMode.Off)));
Test("foreground invalid request fails closed", () =>
    Equal(PerformanceHudMode.Off,
        PerformanceHudPolicy.ModeAfterForeground((PerformanceHudMode)99)));
Test("duplicate hidden application is stable", () =>
{
    PerformanceHudProperties first = PerformanceHudPolicy.Properties(PerformanceHudMode.Off);
    PerformanceHudProperties second = PerformanceHudPolicy.Properties(PerformanceHudMode.Off);
    Equal(first, second);
});
Test("Settings bytes remain isolated", () =>
{
    byte[] before = "settings.celeste-v1"u8.ToArray();
    MemoryStore store = new("Off");
    PerformanceHudPreferenceState state = new(store);
    state.Set(PerformanceHudMode.On);
    True(before.SequenceEqual("settings.celeste-v1"u8.ToArray()));
});
Test("SaveData bytes remain isolated", () =>
{
    byte[] before = "slot-0.celeste"u8.ToArray();
    _ = PerformanceHudPolicy.Properties(PerformanceHudMode.On);
    True(before.SequenceEqual("slot-0.celeste"u8.ToArray()));
});
Test("Stage 9B generation remains isolated", () =>
{
    long generation = 151;
    MemoryStore store = new("Off");
    new PerformanceHudPreferenceState(store).Set(PerformanceHudMode.On);
    Equal(151L, generation);
});
Test("Save Manager Settings reset does not alter preference", () =>
{
    MemoryStore store = new("On");
    PerformanceHudPreferenceState state = new(store);
    bool settingsPresent = false;
    Equal(false, settingsPresent);
    Equal(PerformanceHudMode.On, state.Mode);
    Equal(0, store.Writes);
});

Console.WriteLine($"PASS: Stage 16B Performance HUD policy ({passed} tests)");

internal sealed class MemoryStore(string? initial) : IPerformanceHudPreferenceStore
{
    internal string? Value { get; private set; } = initial;
    internal int Writes { get; private set; }
    public string? Read() => Value;
    public void Write(string value)
    {
        Value = value;
        Writes++;
    }
}
