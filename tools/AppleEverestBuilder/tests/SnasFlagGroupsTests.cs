using AppleEverestBuilder;

internal static class SnasFlagGroupsTests
{
    internal static int Run()
    {
        int passed = 0;
        static MapElementRecord Touch() => new("entity", "MaxHelpingHand/FlagTouchSwitch", "3", 640, 64, 0, 0,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["id"] = "422", ["flag"] = "flag_snasberry_switch", ["persistent"] = "True" }, []);
        static MapElementRecord Gate() => new("entity", "MaxHelpingHand/FlagSwitchGate", "3", 248, 64, 32, 32,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["id"] = "423", ["flag"] = "flag_snasberry_switch", ["persistent"] = "False" }, [(288, 32)]);
        static MapElementRecord Attr(MapElementRecord row, string key, string value)
        {
            var attrs = new Dictionary<string, string>(row.Attributes, StringComparer.Ordinal) { [key] = value };
            return row with { Attributes = attrs };
        }
        void Reject(params MapElementRecord[] rows)
        {
            try { SnasFlagGroups.Inspect(rows); }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or FormatException)
            { passed++; return; }
            throw new Exception("unproved flag-group construction accepted");
        }
        var group = SnasFlagGroups.Inspect([Touch(), Gate()]);
        if (group.Sid != SnasFlagGroups.Sid || group.Mode != 0 || group.Room != "3" ||
            !group.LegacyMode || !group.GroupPersistence || !group.Members[0].Persistent || group.Members[1].Persistent)
            throw new Exception("source-derived mixed persistence was changed");
        passed++;
        Reject(); Reject(Touch()); Reject(Gate());
        Reject(Touch(), Gate(), Gate()); Reject(Touch(), Touch());
        Reject(Touch() with { Room = "wrong" }, Gate());
        Reject(Touch() with { X = 641 }, Gate());
        Reject(Attr(Touch(), "id", "424"), Gate());
        Reject(Attr(Touch(), "flag", "wrong"), Gate());
        Reject(Attr(Touch(), "legacyFlagMode", "False"), Gate());
        Reject(Attr(Touch(), "persistent", "False"), Gate());
        Reject(Attr(Touch(), "inverted", "True"), Gate());
        foreach (var pair in new[] { ("allowDisable", "True"), ("playerCanActivate", "False"),
                     ("hideIfFlag", "other"), ("borderTexture", "other"), ("icon", "other"), ("animationLength", "7") })
            Reject(Attr(Touch(), pair.Item1, pair.Item2), Gate());
        Reject(Touch(), Gate() with { Nodes = [] });
        Reject(Touch(), Gate() with { Nodes = [(288, 32), (300, 30)] });
        Reject(Touch(), Gate() with { Nodes = [(287, 32)] });
        Reject(Touch(), Gate() with { Width = 40 });
        Reject(Touch(), Attr(Gate(), "persistent", "True"));
        foreach (string key in new[] { "allowReturn", "speedMode", "isShatter", "moveImmediately" })
            Reject(Touch(), Attr(Gate(), key, "True"));
        Reject(Touch(), Attr(Gate(), "moveEased", "False"));
        foreach (string name in new[] { "eyebomb", "seeker", "seekerStatue", "MaxHelpingHand/MovingFlagTouchSwitch",
                     "MaxHelpingHand/FlagTouchSwitchWall", "ChroniaHelper/FlagTouchSwitch", "ChroniaHelper/FlagSwitchGate",
                     "MaxHelpingHand/ShatterFlagSwitchGate", "CommunalHelper/MaxHelpingHand/DreamFlagSwitchGate" })
            Reject(Touch(), Gate(), Touch() with { Id = name });
        if (SnasFlagGroups.Source([group]) != SnasFlagGroups.Source([SnasFlagGroups.Inspect([Gate(), Touch()])]))
            throw new Exception("flag group order depends on enumeration order");
        passed++;
        (string Sid, string SourceSha256) lobby = ("StrawberryJam2021/0-Lobbies/1-Beginner", "a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2");
        (string Sid, string SourceSha256) bing = ("StrawberryJam2021/1-Beginner/Bing_Over_Google", "e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347");
        (string Sid, string SourceSha256) snas = (SnasFlagGroups.Sid, SnasFlagGroups.SourceSha256);
        CollabManifestGenerator.ValidatePinnedBeginnerMaps([lobby, bing]); passed++;
        CollabManifestGenerator.ValidatePinnedBeginnerMaps([snas, bing, lobby]); passed++;
        foreach (var maps in new (string Sid, string SourceSha256)[][] {
            [], [lobby], [lobby, snas], [bing, snas], [lobby, bing, bing], [lobby, bing, snas, snas],
            [lobby, bing, (SnasFlagGroups.Sid, new string('0', 64))],
            [lobby, bing, ("StrawberryJam2021/1-Beginner/another", SnasFlagGroups.SourceSha256)] })
        {
            try { CollabManifestGenerator.ValidatePinnedBeginnerMaps(maps); }
            catch (InvalidDataException) { passed++; continue; }
            throw new Exception("unreviewed original map union accepted");
        }
        Console.WriteLine("PASS: " + passed + " finite flag-group construction controls (host metadata scope)");
        return passed;
    }
}
