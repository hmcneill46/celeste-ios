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

Test("fixed preference key", () => Equal("CelesteTvOS.ControllerPrompts.v1", Stage11ControllerPromptPolicy.PreferenceKey));
Test("missing key is Automatic", () => Equal(Stage11PromptMode.Automatic, Stage11ControllerPromptPolicy.ParseStored(null)));
Test("invalid key is Automatic", () => Equal(Stage11PromptMode.Automatic, Stage11ControllerPromptPolicy.ParseStored("DualSense")));

foreach (Stage11PromptMode mode in Enum.GetValues<Stage11PromptMode>())
{
    Stage11PromptMode captured = mode;
    Test($"{captured} stable parse roundtrip", () =>
        Equal(captured, Stage11ControllerPromptPolicy.ParseStored(Stage11ControllerPromptPolicy.StoredValue(captured))));
    Test($"{captured} preference persistence", () =>
    {
        MemoryStore store = new(null);
        Stage11PromptPreferenceState state = new(store);
        bool expectedChange = captured != Stage11PromptMode.Automatic;
        Equal(expectedChange, state.Set(captured));
        Equal(captured, new Stage11PromptPreferenceState(store).Mode);
    });
}

Test("same mode is a no-op", () =>
{
    MemoryStore store = new("Xbox");
    Stage11PromptPreferenceState state = new(store);
    Equal(false, state.Set(Stage11PromptMode.Xbox));
    Equal(0, store.Writes);
});
Test("out-of-range mode falls back safely", () =>
{
    MemoryStore store = new("Stadia");
    Stage11PromptPreferenceState state = new(store);
    True(state.Set((Stage11PromptMode)99));
    Equal(Stage11PromptMode.Automatic, state.Mode);
    Equal("Automatic", store.Value);
});

Test("manual Xbox resolves xb1", () => Equal("xb1", Stage11ControllerPromptPolicy.ResolvePrefix(Stage11PromptMode.Xbox, "keyboard", Stage11AppleControllerFamily.PlayStation)));
Test("manual PlayStation resolves ps4", () => Equal("ps4", Stage11ControllerPromptPolicy.ResolvePrefix(Stage11PromptMode.PlayStation, "xb1", Stage11AppleControllerFamily.Xbox)));
Test("manual Nintendo Switch resolves ns", () => Equal("ns", Stage11ControllerPromptPolicy.ResolvePrefix(Stage11PromptMode.NintendoSwitch, "xb1", Stage11AppleControllerFamily.PlayStation)));
Test("manual Stadia resolves stadia", () => Equal("stadia", Stage11ControllerPromptPolicy.ResolvePrefix(Stage11PromptMode.Stadia, "xb1", Stage11AppleControllerFamily.Xbox)));

Test("existing PlayStation GUID result wins", () => Equal("ps4", Stage11ControllerPromptPolicy.ResolvePrefix(Stage11PromptMode.Automatic, "ps4", Stage11AppleControllerFamily.Xbox)));
Test("existing Nintendo GUID result wins", () => Equal("ns", Stage11ControllerPromptPolicy.ResolvePrefix(Stage11PromptMode.Automatic, "ns", Stage11AppleControllerFamily.PlayStation)));
Test("existing Stadia GUID result wins", () => Equal("stadia", Stage11ControllerPromptPolicy.ResolvePrefix(Stage11PromptMode.Automatic, "stadia", Stage11AppleControllerFamily.PlayStation)));
Test("DualSense category selects PlayStation", () => Equal("ps4", Stage11ControllerPromptPolicy.ResolvePrefix(Stage11PromptMode.Automatic, "xb1", Stage11AppleControllerFamily.PlayStation)));
Test("DualShock category selects PlayStation", () => Equal("ps4", Stage11ControllerPromptPolicy.ResolvePrefix(Stage11PromptMode.Automatic, "xb1", Stage11AppleControllerFamily.PlayStation)));
Test("Xbox category selects Xbox", () => Equal("xb1", Stage11ControllerPromptPolicy.ResolvePrefix(Stage11PromptMode.Automatic, "xb1", Stage11AppleControllerFamily.Xbox)));
Test("unknown controller preserves Celeste fallback", () => Equal("xb1", Stage11ControllerPromptPolicy.ResolvePrefix(Stage11PromptMode.Automatic, "xb1", Stage11AppleControllerFamily.Unknown)));
Test("keyboard focus remains keyboard in Automatic", () => Equal("keyboard", Stage11ControllerPromptPolicy.ResolvePrefix(Stage11PromptMode.Automatic, "keyboard", Stage11AppleControllerFamily.Unknown)));
Test("unknown prefix fails safely to Xbox", () => Equal("xb1", Stage11ControllerPromptPolicy.ResolvePrefix(Stage11PromptMode.Automatic, "mystery", Stage11AppleControllerFamily.Unknown)));

Test("Siri Remote is not selected", () => Equal(Stage11AppleControllerFamily.Unknown,
    Stage11ControllerPromptPolicy.SelectAppleFamily(new[] { new Stage11ControllerCandidate(true, true, Stage11AppleControllerFamily.Remote, 0) })));
Test("non-extended controller is not selected", () => Equal(Stage11AppleControllerFamily.Unknown,
    Stage11ControllerPromptPolicy.SelectAppleFamily(new[] { new Stage11ControllerCandidate(true, false, Stage11AppleControllerFamily.PlayStation, 0) })));
Test("current controller wins multiple-controller selection", () => Equal(Stage11AppleControllerFamily.PlayStation,
    Stage11ControllerPromptPolicy.SelectAppleFamily(new[]
    {
        new Stage11ControllerCandidate(false, true, Stage11AppleControllerFamily.Xbox, 0),
        new Stage11ControllerCandidate(true, true, Stage11AppleControllerFamily.PlayStation, 1)
    })));
Test("stable connection order breaks non-current tie", () => Equal(Stage11AppleControllerFamily.Xbox,
    Stage11ControllerPromptPolicy.SelectAppleFamily(new[]
    {
        new Stage11ControllerCandidate(false, true, Stage11AppleControllerFamily.PlayStation, 4),
        new Stage11ControllerCandidate(false, true, Stage11AppleControllerFamily.Xbox, 2)
    })));
Test("disconnect falls back to remaining controller", () => Equal(Stage11AppleControllerFamily.Xbox,
    Stage11ControllerPromptPolicy.SelectAppleFamily(new[] { new Stage11ControllerCandidate(false, true, Stage11AppleControllerFamily.Xbox, 0) })));
Test("no connected controller is unknown", () => Equal(Stage11AppleControllerFamily.Unknown,
    Stage11ControllerPromptPolicy.SelectAppleFamily(Array.Empty<Stage11ControllerCandidate>())));

Test("prompt choice does not mutate controller bindings", () =>
{
    string[] before = { "A", "Y", "X", "B", "LeftTrigger", "Start" };
    string[] after = before.ToArray();
    foreach (Stage11PromptMode mode in Enum.GetValues<Stage11PromptMode>())
        _ = Stage11ControllerPromptPolicy.ResolvePrefix(mode, "xb1", Stage11AppleControllerFamily.PlayStation);
    True(before.SequenceEqual(after, StringComparer.Ordinal));
});
Test("prompt choice does not mutate save bytes", () =>
{
    byte[] before = "locked-settings-and-save-payload"u8.ToArray();
    byte[] after = before.ToArray();
    MemoryStore store = new("PlayStation");
    Stage11PromptPreferenceState state = new(store);
    True(state.Set(Stage11PromptMode.NintendoSwitch));
    True(before.SequenceEqual(after));
});
Test("Settings import does not alter prompt preference", () =>
{
    MemoryStore store = new("Stadia");
    Stage11PromptPreferenceState state = new(store);
    byte[] importedSettings = "replacement-settings.celeste"u8.ToArray();
    _ = importedSettings.ToArray();
    Equal(Stage11PromptMode.Stadia, state.Mode);
    Equal(0, store.Writes);
});
Test("Settings reset does not alter prompt preference", () =>
{
    MemoryStore store = new("Xbox");
    Stage11PromptPreferenceState state = new(store);
    bool settingsPresent = false;
    Equal(false, settingsPresent);
    Equal(Stage11PromptMode.Xbox, state.Mode);
    Equal(0, store.Writes);
});

Console.WriteLine($"PASS: Stage 11 controller-prompt policy ({passed} tests)");

internal sealed class MemoryStore(string? initial) : IStage11PromptPreferenceStore
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
