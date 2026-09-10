using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AppleEverestBuilder;

// Finite host map-processor projection. No runtime map/provider discovery is
// introduced, and absence of a generated group never means an implicit true.
internal static class SnasFlagGroups
{
    internal const string Sid = "StrawberryJam2021/1-Beginner/snas";
    internal const string SourceSha256 = "6ad3172d496e8b5b4ce71f1128fe231b162d534419dc823d2af2cea27fc241d9";
    internal const string Flag = "flag_snasberry_switch";
    internal sealed record Member(int Id, bool Gate, bool Persistent, float X, float Y);
    internal sealed record Group(string Sid, int Mode, string Room, string Flag, string SourceSha256,
        bool LegacyMode, bool GroupPersistence, Member[] Members);
    private static readonly HashSet<string> Unsupported = new(StringComparer.Ordinal)
    {
        "eyebomb", "seeker", "seekerStatue", "MaxHelpingHand/MovingFlagTouchSwitch",
        "MaxHelpingHand/FlagTouchSwitchWall", "ChroniaHelper/FlagTouchSwitch", "ChroniaHelper/FlagSwitchGate",
        "MaxHelpingHand/ShatterFlagSwitchGate", "CommunalHelper/MaxHelpingHand/DreamFlagSwitchGate"
    };

    internal static Group Inspect(IReadOnlyList<MapElementRecord> elements)
    {
        if (elements.Any(row => Unsupported.Contains(row.Id)))
            throw new InvalidDataException("snas has an unproved authored creation branch");
        MapElementRecord[] members = elements.Where(row => row.Id is "MaxHelpingHand/FlagTouchSwitch" or "MaxHelpingHand/FlagSwitchGate").ToArray();
        if (members.Length != 2) throw new InvalidDataException("snas flag group census differs");
        var touch = members.SingleOrDefault(row => row.Id == "MaxHelpingHand/FlagTouchSwitch")
            ?? throw new InvalidDataException("snas flag switch is absent");
        var gate = members.SingleOrDefault(row => row.Id == "MaxHelpingHand/FlagSwitchGate")
            ?? throw new InvalidDataException("snas flag gate is absent");
        static bool Bool(MapElementRecord row, string key, bool missing = false) =>
            row.Attributes.TryGetValue(key, out string? value) ? bool.Parse(value) : missing;
        static int Id(MapElementRecord row) => int.Parse(row.Attributes["id"], CultureInfo.InvariantCulture);
        foreach (var row in members)
        {
            if (row.Kind != "entity" || row.Room != "3" || row.Attributes.GetValueOrDefault("flag") != Flag ||
                Bool(row, "inverted") || !Bool(row, "legacyFlagMode", true))
                throw new InvalidDataException("snas flag group room/flag/mode differs");
        }
        if (Id(touch) != 422 || touch.X != 640 || touch.Y != 64 || !Bool(touch, "persistent") ||
            touch.Nodes.Count != 0 || Bool(touch, "allowDisable") || !Bool(touch, "playerCanActivate", true) ||
            touch.Attributes.GetValueOrDefault("hideIfFlag", "") != "" ||
            touch.Attributes.GetValueOrDefault("borderTexture", "") != "" ||
            touch.Attributes.GetValueOrDefault("icon", "vanilla") != "vanilla" ||
            touch.Attributes.GetValueOrDefault("animationLength", "6") != "6")
            throw new InvalidDataException("snas flag switch processor profile differs");
        if (Id(gate) != 423 || gate.X != 248 || gate.Y != 64 || gate.Width != 32 || gate.Height != 32 ||
            Bool(gate, "persistent") || gate.Nodes.Count != 1 || gate.Nodes[0] != (288f, 32f) ||
            Bool(gate, "allowReturn") || Bool(gate, "speedMode") || Bool(gate, "isShatter") || Bool(gate, "moveImmediately") ||
            !Bool(gate, "moveEased", true) || gate.Attributes.GetValueOrDefault("sprite", "block") != "block" ||
            gate.Attributes.GetValueOrDefault("icon", "vanilla") != "vanilla")
            throw new InvalidDataException("snas flag gate processor profile differs");
        return new(Sid, 0, "3", Flag, SourceSha256, true, true,
            [new(422, false, true, touch.X, touch.Y), new(423, true, false, gate.X, gate.Y)]);
    }

    internal static Group[] Create(IReadOnlyList<ContentMountRecord> content, string contentRoot,
        IReadOnlyList<ResolvedMod> mods)
    {
        ContentMountRecord[] maps = content.Where(row => row.LogicalPath == "Maps/" + Sid + ".bin").ToArray();
        if (maps.Length == 0) return [];
        if (maps.Length != 1 || maps[0].Owner != "StrawberryJam2021" || maps[0].SourceSha256 != SourceSha256)
            throw new InvalidDataException("snas source mount identity differs");
        string path = Path.Combine(contentRoot, maps[0].LogicalPath);
        if (Hashing.FileSha256(path) != SourceSha256 || new FileInfo(path).Length != 67718)
            throw new InvalidDataException("snas source bytes were changed before group generation");
        if (mods.Any(mod => mod.Metadata.Name is "ExtendedCameraDynamics" or "ZoomOutHelperPrototype"))
            throw new InvalidDataException("selected Max camera getters cannot admit an optional zoom provider");
        var group = Inspect(ContentCompiler.InspectElements(path));
        foreach (var name in new[] { "CommunalHelper", "ContortHelper", "MaxHelpingHand" })
            if (mods.Count(mod => mod.Metadata.Name == name && mod.StaticSemanticLowering != null) != 1)
                throw new InvalidDataException("snas finite semantic provider is missing: " + name);
        return [group];
    }

    internal static string Source(Group[] groups)
    {
        static string Q(string value) => JsonSerializer.Serialize(value);
        static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture) + "f";
        var text = new StringBuilder("#nullable disable\nusing Microsoft.Xna.Framework;\nnamespace Celeste.Mod;\ninternal static class GeneratedAppleEverestFlagGroups\n{\n    internal static readonly AppleEverestFlagGroup[] Groups =\n    {\n");
        foreach (var group in groups.OrderBy(group => group.Sid, StringComparer.Ordinal))
        {
            text.Append("        new(").Append(Q(group.Sid)).Append(", ").Append(group.Mode).Append(", ")
                .Append(Q(group.Room)).Append(", ").Append(Q(group.Flag)).Append(", ").Append(Q(group.SourceSha256))
                .Append(", ").Append(group.LegacyMode ? "true" : "false").Append(", ").Append(group.GroupPersistence ? "true" : "false")
                .AppendLine(", new AppleEverestFlagMember[]\n        {");
            foreach (var member in group.Members)
                text.Append("            new(").Append(member.Id).Append(", ").Append(member.Gate ? "true" : "false")
                    .Append(", ").Append(member.Persistent ? "true" : "false").Append(", new Vector2(")
                    .Append(F(member.X)).Append(", ").Append(F(member.Y)).AppendLine(")),");
            text.AppendLine("        }),");
        }
        return text.AppendLine("    };\n}").ToString();
    }
}
