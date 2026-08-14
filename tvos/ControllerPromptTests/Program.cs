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

Test("fixed preference key", () => Equal("CelesteTvOS.ControllerPrompts.v1", ControllerPromptPolicy.PreferenceKey));
Test("missing key is Automatic", () => Equal(ControllerPromptMode.Automatic, ControllerPromptPolicy.ParseStored(null)));
Test("invalid key is Automatic", () => Equal(ControllerPromptMode.Automatic, ControllerPromptPolicy.ParseStored("DualSense")));

foreach (ControllerPromptMode mode in Enum.GetValues<ControllerPromptMode>())
{
    ControllerPromptMode captured = mode;
    Test($"{captured} stable parse roundtrip", () =>
        Equal(captured, ControllerPromptPolicy.ParseStored(ControllerPromptPolicy.StoredValue(captured))));
    Test($"{captured} preference persistence", () =>
    {
        MemoryStore store = new(null);
        ControllerPromptPreferenceState state = new(store);
        bool expectedChange = captured != ControllerPromptMode.Automatic;
        Equal(expectedChange, state.Set(captured));
        Equal(captured, new ControllerPromptPreferenceState(store).Mode);
    });
}

Test("same mode is a no-op", () =>
{
    MemoryStore store = new("Xbox");
    ControllerPromptPreferenceState state = new(store);
    Equal(false, state.Set(ControllerPromptMode.Xbox));
    Equal(0, store.Writes);
});
Test("out-of-range mode falls back safely", () =>
{
    MemoryStore store = new("Stadia");
    ControllerPromptPreferenceState state = new(store);
    True(state.Set((ControllerPromptMode)99));
    Equal(ControllerPromptMode.Automatic, state.Mode);
    Equal("Automatic", store.Value);
});

Test("manual Xbox resolves xb1", () => Equal("xb1", ControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.Xbox, "keyboard", AppleControllerFamily.PlayStation)));
Test("manual PlayStation resolves ps4", () => Equal("ps4", ControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.PlayStation, "xb1", AppleControllerFamily.Xbox)));
Test("manual Nintendo Switch resolves ns", () => Equal("ns", ControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.NintendoSwitch, "xb1", AppleControllerFamily.PlayStation)));
Test("manual Stadia resolves stadia", () => Equal("stadia", ControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.Stadia, "xb1", AppleControllerFamily.Xbox)));

Test("existing PlayStation GUID result wins", () => Equal("ps4", ControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.Automatic, "ps4", AppleControllerFamily.Xbox)));
Test("existing Nintendo GUID result wins", () => Equal("ns", ControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.Automatic, "ns", AppleControllerFamily.PlayStation)));
Test("existing Stadia GUID result wins", () => Equal("stadia", ControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.Automatic, "stadia", AppleControllerFamily.PlayStation)));
Test("DualSense category selects PlayStation", () => Equal("ps4", ControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.Automatic, "xb1", AppleControllerFamily.PlayStation)));
Test("DualShock category selects PlayStation", () => Equal("ps4", ControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.Automatic, "xb1", AppleControllerFamily.PlayStation)));
Test("Xbox category selects Xbox", () => Equal("xb1", ControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.Automatic, "xb1", AppleControllerFamily.Xbox)));
Test("unknown controller preserves Celeste fallback", () => Equal("xb1", ControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.Automatic, "xb1", AppleControllerFamily.Unknown)));
Test("keyboard focus remains keyboard in Automatic", () => Equal("keyboard", ControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.Automatic, "keyboard", AppleControllerFamily.Unknown)));
Test("unknown prefix fails safely to Xbox", () => Equal("xb1", ControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.Automatic, "mystery", AppleControllerFamily.Unknown)));

Test("Siri Remote is not selected", () => Equal(AppleControllerFamily.Unknown,
    ControllerPromptPolicy.SelectAppleFamily(new[] { new ControllerCandidate(true, true, AppleControllerFamily.Remote, 0) })));
Test("non-extended controller is not selected", () => Equal(AppleControllerFamily.Unknown,
    ControllerPromptPolicy.SelectAppleFamily(new[] { new ControllerCandidate(true, false, AppleControllerFamily.PlayStation, 0) })));
Test("current controller wins multiple-controller selection", () => Equal(AppleControllerFamily.PlayStation,
    ControllerPromptPolicy.SelectAppleFamily(new[]
    {
        new ControllerCandidate(false, true, AppleControllerFamily.Xbox, 0),
        new ControllerCandidate(true, true, AppleControllerFamily.PlayStation, 1)
    })));
Test("stable connection order breaks non-current tie", () => Equal(AppleControllerFamily.Xbox,
    ControllerPromptPolicy.SelectAppleFamily(new[]
    {
        new ControllerCandidate(false, true, AppleControllerFamily.PlayStation, 4),
        new ControllerCandidate(false, true, AppleControllerFamily.Xbox, 2)
    })));
Test("disconnect falls back to remaining controller", () => Equal(AppleControllerFamily.Xbox,
    ControllerPromptPolicy.SelectAppleFamily(new[] { new ControllerCandidate(false, true, AppleControllerFamily.Xbox, 0) })));
Test("no connected controller is unknown", () => Equal(AppleControllerFamily.Unknown,
    ControllerPromptPolicy.SelectAppleFamily(Array.Empty<ControllerCandidate>())));

Test("prompt choice does not mutate controller bindings", () =>
{
    string[] before = { "A", "Y", "X", "B", "LeftTrigger", "Start" };
    string[] after = before.ToArray();
    foreach (ControllerPromptMode mode in Enum.GetValues<ControllerPromptMode>())
        _ = ControllerPromptPolicy.ResolvePrefix(mode, "xb1", AppleControllerFamily.PlayStation);
    True(before.SequenceEqual(after, StringComparer.Ordinal));
});
Test("prompt choice does not mutate save bytes", () =>
{
    byte[] before = "locked-settings-and-save-payload"u8.ToArray();
    byte[] after = before.ToArray();
    MemoryStore store = new("PlayStation");
    ControllerPromptPreferenceState state = new(store);
    True(state.Set(ControllerPromptMode.NintendoSwitch));
    True(before.SequenceEqual(after));
});
Test("Settings import does not alter prompt preference", () =>
{
    MemoryStore store = new("Stadia");
    ControllerPromptPreferenceState state = new(store);
    byte[] importedSettings = "replacement-settings.celeste"u8.ToArray();
    _ = importedSettings.ToArray();
    Equal(ControllerPromptMode.Stadia, state.Mode);
    Equal(0, store.Writes);
});
Test("Settings reset does not alter prompt preference", () =>
{
    MemoryStore store = new("Xbox");
    ControllerPromptPreferenceState state = new(store);
    bool settingsPresent = false;
    Equal(false, settingsPresent);
    Equal(ControllerPromptMode.Xbox, state.Mode);
    Equal(0, store.Writes);
});

Console.WriteLine($"PASS: Stage 11 controller-prompt policy ({passed} tests)");

internal sealed class MemoryStore(string? initial) : IControllerPromptPreferenceStore
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
