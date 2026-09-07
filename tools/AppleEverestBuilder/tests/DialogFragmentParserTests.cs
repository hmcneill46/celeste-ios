using Celeste.Mod;

internal static class DialogFragmentParserTests
{
    internal static int Run()
    {
        int passed = 0;
        void Pass(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            passed++;
        }

        IReadOnlyDictionary<string, AppleEverestDialogFragmentEntry> exact =
            AppleEverestDialogFragmentParser.Parse(
                "StrawberryJam2021_0_Lobbies_1_Beginner_Credits=" +
                "[MADELINE left normal]STAGE 25K-H NPC TALK PASS\n",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        AppleEverestDialogFragmentEntry npc = exact["StrawberryJam2021_0_Lobbies_1_Beginner_Credits"];
        Pass(npc.Raw == "{portrait MADELINE left normal}STAGE 25K-H NPC TALK PASS" &&
             npc.Cleaned == "STAGE 25K-H NPC TALK PASS",
            "mod dialog portrait command matches Language.FromTxt");
        Pass(!npc.Raw.Contains("&#x20;", StringComparison.Ordinal) && !npc.Raw.Contains("[", StringComparison.Ordinal),
            "portrait dialog contains no printable command or encoded trailing space");

        IReadOnlyDictionary<string, AppleEverestDialogFragmentEntry> multiline =
            AppleEverestDialogFragmentParser.Parse("line=FIRST\nSECOND\n", new Dictionary<string, string>());
        Pass(multiline["line"] == new AppleEverestDialogFragmentEntry("FIRST{break}SECOND", "FIRST\nSECOND"),
            "mod dialog multiline break semantics");

        IReadOnlyDictionary<string, AppleEverestDialogFragmentEntry> inserts =
            AppleEverestDialogFragmentParser.Parse("base=HELLO\ncopy={+ base} WORLD\\#1\n",
                new Dictionary<string, string>());
        Pass(inserts["copy"] == new AppleEverestDialogFragmentEntry("HELLO WORLD#1", "HELLO WORLD#1"),
            "mod dialog insert and escaped hash semantics");

        const string credits = "StrawberryJam2021_0_Lobbies_1_Beginner_Credits";
        const string diagnosticMap = "AppleEverest/Stage25KH";
        Pass(AppleEverestDialogFragmentParser.KeyForMap(null, "AppleEverestStage25KJ/0-Lobbies/1-Fixture") ==
            "AppleEverestStage25KJ_0_Lobbies_1_Fixture", "original Everest SID key normalization resolves lobby title");
        Pass(AppleEverestDialogFragmentParser.KeyForMap(null, "a+b c/d-e") == "a_b_c_d_e",
            "all four original Everest dialog separators normalize");
        foreach (bool diagnosticFirst in new[] { true, false })
        {
            var dialog = new Dictionary<string, AppleEverestDialogFragmentEntry>(StringComparer.OrdinalIgnoreCase);
            var original = AppleEverestDialogFragmentParser.Parse(credits + "=Original credits\n", new Dictionary<string, string>());
            foreach (bool diagnostic in diagnosticFirst ? new[] { true, false } : new[] { false, true })
                foreach (var entry in diagnostic ? exact : original)
                    dialog[AppleEverestDialogFragmentParser.KeyForMap(diagnostic ? diagnosticMap : null, entry.Key)] = entry.Value;
            Pass(dialog[AppleEverestDialogFragmentParser.KeyForMap(diagnosticMap, credits)].Raw == npc.Raw &&
                 dialog[AppleEverestDialogFragmentParser.KeyForMap("AppleEverestStage25KJ/FactoryProfiles/MaxMechanics", credits)].Raw ==
                     "Original credits", "K-H portrait and original credits survive either mount order");
        }
        string scoped = AppleEverestDialogFragmentParser.KeyForMap(diagnosticMap, credits);
        Pass(AppleEverestDialogFragmentParser.KeyForMap(diagnosticMap, scoped) == scoped &&
             AppleEverestDialogFragmentParser.KeyForMap(diagnosticMap, "options_gameplay") == "options_gameplay",
            "diagnostic alias is idempotent and leaves unrelated UI keys alone");
        return passed;
    }
}
