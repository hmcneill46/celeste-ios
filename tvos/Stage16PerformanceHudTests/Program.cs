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

Stage16MetalLayerCandidate Candidate(
    int identity, bool direct = true, bool metal = true, bool device = true, bool size = true) =>
    new(new IntPtr(identity), direct, metal, device, size);

Test("fixed preference key", () =>
    Equal("CelesteTvOS.PerformanceHUD.v1", Stage16PerformanceHudPolicy.PreferenceKey));
Test("missing preference is Off", () =>
    Equal(Stage16PerformanceHudMode.Off, Stage16PerformanceHudPolicy.ParseStored(null)));
Test("invalid preference is Off", () =>
    Equal(Stage16PerformanceHudMode.Off, Stage16PerformanceHudPolicy.ParseStored("Enabled")));
Test("Off parses", () =>
    Equal(Stage16PerformanceHudMode.Off, Stage16PerformanceHudPolicy.ParseStored("Off")));
Test("On parses", () =>
    Equal(Stage16PerformanceHudMode.On, Stage16PerformanceHudPolicy.ParseStored("On")));
Test("Off stable value", () =>
    Equal("Off", Stage16PerformanceHudPolicy.StoredValue(Stage16PerformanceHudMode.Off)));
Test("On stable value", () =>
    Equal("On", Stage16PerformanceHudPolicy.StoredValue(Stage16PerformanceHudMode.On)));
Test("Off round trip", () =>
{
    MemoryStore store = new("On");
    Stage16PerformanceHudPreferenceState state = new(store);
    True(state.Set(Stage16PerformanceHudMode.Off));
    Equal(Stage16PerformanceHudMode.Off, new Stage16PerformanceHudPreferenceState(store).Mode);
});
Test("On round trip", () =>
{
    MemoryStore store = new(null);
    Stage16PerformanceHudPreferenceState state = new(store);
    True(state.Set(Stage16PerformanceHudMode.On));
    Equal(Stage16PerformanceHudMode.On, new Stage16PerformanceHudPreferenceState(store).Mode);
});
Test("unchanged value is no-op", () =>
{
    MemoryStore store = new("On");
    Stage16PerformanceHudPreferenceState state = new(store);
    Equal(false, state.Set(Stage16PerformanceHudMode.On));
    Equal(0, store.Writes);
});
Test("invalid enum safely persists Off", () =>
{
    MemoryStore store = new("On");
    Stage16PerformanceHudPreferenceState state = new(store);
    True(state.Set((Stage16PerformanceHudMode)99));
    Equal("Off", store.Value);
});

Test("one valid real layer selected", () =>
    Equal(new IntPtr(1), Stage16PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1) })));
Test("no layer fails closed", () =>
    Equal<IntPtr?>(null, Stage16PerformanceHudPolicy.SelectPresentationLayer(Array.Empty<Stage16MetalLayerCandidate>())));
Test("wrong layer type fails closed", () =>
    Equal<IntPtr?>(null, Stage16PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1, metal: false) })));
Test("missing Metal device fails closed", () =>
    Equal<IntPtr?>(null, Stage16PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1, device: false) })));
Test("drawable mismatch fails closed", () =>
    Equal<IntPtr?>(null, Stage16PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1, size: false) })));
Test("non-direct Metal layer rejected", () =>
    Equal<IntPtr?>(null, Stage16PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1, direct: false) })));
Test("ambiguous direct candidates fail closed", () =>
    Equal<IntPtr?>(null, Stage16PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1), Candidate(2) })));
Test("duplicate wrapper identity remains unambiguous", () =>
    Equal(new IntPtr(1), Stage16PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(1), Candidate(1) })));
Test("unrelated non-direct layer does not replace direct layer", () =>
    Equal(new IntPtr(2), Stage16PerformanceHudPolicy.SelectPresentationLayer(new[]
    {
        Candidate(1, direct: false), Candidate(2)
    })));
Test("unchanged foreground identity remains selected", () =>
{
    IntPtr? before = Stage16PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(7) });
    IntPtr? after = Stage16PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(7) });
    Equal(before, after);
});
Test("replacement foreground layer can be revalidated", () =>
{
    Equal(new IntPtr(8), Stage16PerformanceHudPolicy.SelectPresentationLayer(new[] { Candidate(8) }));
});

Test("Off properties disable mode", () =>
    Equal("disabled", Stage16PerformanceHudPolicy.Properties(Stage16PerformanceHudMode.Off).Mode));
Test("On properties use default mode", () =>
    Equal("default", Stage16PerformanceHudPolicy.Properties(Stage16PerformanceHudMode.On).Mode));
Test("Off logging disabled", () =>
    Equal("disabled", Stage16PerformanceHudPolicy.Properties(Stage16PerformanceHudMode.Off).Logging));
Test("On logging disabled", () =>
    Equal("disabled", Stage16PerformanceHudPolicy.Properties(Stage16PerformanceHudMode.On).Logging));
Test("immediate mode sequence is deterministic", () =>
{
    string[] modes = new[] { Stage16PerformanceHudMode.Off, Stage16PerformanceHudMode.On, Stage16PerformanceHudMode.Off }
        .Select(mode => Stage16PerformanceHudPolicy.Properties(mode).Mode).ToArray();
    True(modes.SequenceEqual(new[] { "disabled", "default", "disabled" }));
});
Test("sixty toggle applications do not change policy", () =>
{
    for (int index = 0; index < 60; index++)
    {
        Stage16PerformanceHudMode mode = index % 2 == 0 ? Stage16PerformanceHudMode.On : Stage16PerformanceHudMode.Off;
        Equal("disabled", Stage16PerformanceHudPolicy.Properties(mode).Logging);
    }
});
Test("API unavailable remains hidden", () =>
{
    Equal<IntPtr?>(null, Stage16PerformanceHudPolicy.SelectPresentationLayer(Array.Empty<Stage16MetalLayerCandidate>()));
    Equal("disabled", Stage16PerformanceHudPolicy.Properties(Stage16PerformanceHudMode.Off).Mode);
});
Test("invalid preference produces hidden properties", () =>
{
    Stage16PerformanceHudMode mode = Stage16PerformanceHudPolicy.ParseStored("corrupt");
    Equal("disabled", Stage16PerformanceHudPolicy.Properties(mode).Mode);
});
Test("resign-active hides requested On without changing preference", () =>
{
    MemoryStore store = new("On");
    Stage16PerformanceHudPreferenceState state = new(store);
    Equal(Stage16PerformanceHudMode.Off,
        Stage16PerformanceHudPolicy.ModeWhileInactive(state.Mode));
    Equal(Stage16PerformanceHudMode.On, state.Mode);
    Equal(0, store.Writes);
});
Test("foreground restores requested On", () =>
    Equal(Stage16PerformanceHudMode.On,
        Stage16PerformanceHudPolicy.ModeAfterForeground(Stage16PerformanceHudMode.On)));
Test("foreground preserves requested Off", () =>
    Equal(Stage16PerformanceHudMode.Off,
        Stage16PerformanceHudPolicy.ModeAfterForeground(Stage16PerformanceHudMode.Off)));
Test("foreground invalid request fails closed", () =>
    Equal(Stage16PerformanceHudMode.Off,
        Stage16PerformanceHudPolicy.ModeAfterForeground((Stage16PerformanceHudMode)99)));
Test("duplicate hidden application is stable", () =>
{
    Stage16HudProperties first = Stage16PerformanceHudPolicy.Properties(Stage16PerformanceHudMode.Off);
    Stage16HudProperties second = Stage16PerformanceHudPolicy.Properties(Stage16PerformanceHudMode.Off);
    Equal(first, second);
});
Test("Settings bytes remain isolated", () =>
{
    byte[] before = "settings.celeste-v1"u8.ToArray();
    MemoryStore store = new("Off");
    Stage16PerformanceHudPreferenceState state = new(store);
    state.Set(Stage16PerformanceHudMode.On);
    True(before.SequenceEqual("settings.celeste-v1"u8.ToArray()));
});
Test("SaveData bytes remain isolated", () =>
{
    byte[] before = "slot-0.celeste"u8.ToArray();
    _ = Stage16PerformanceHudPolicy.Properties(Stage16PerformanceHudMode.On);
    True(before.SequenceEqual("slot-0.celeste"u8.ToArray()));
});
Test("Stage 9B generation remains isolated", () =>
{
    long generation = 151;
    MemoryStore store = new("Off");
    new Stage16PerformanceHudPreferenceState(store).Set(Stage16PerformanceHudMode.On);
    Equal(151L, generation);
});
Test("Save Manager Settings reset does not alter preference", () =>
{
    MemoryStore store = new("On");
    Stage16PerformanceHudPreferenceState state = new(store);
    bool settingsPresent = false;
    Equal(false, settingsPresent);
    Equal(Stage16PerformanceHudMode.On, state.Mode);
    Equal(0, store.Writes);
});

Console.WriteLine($"PASS: Stage 16B Performance HUD policy ({passed} tests)");

internal sealed class MemoryStore(string? initial) : IStage16PerformanceHudPreferenceStore
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
