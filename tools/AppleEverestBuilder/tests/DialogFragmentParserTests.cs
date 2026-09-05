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
        return passed;
    }
}
