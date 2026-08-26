using System.Globalization;
using System.Text;

namespace AppleEverestBuilder;

internal sealed record CollabGeneration(string Source, string ManifestText,
    IReadOnlyList<CollabDescriptorRecord> Collabs, string Sha256);

internal static class CollabManifestGenerator
{
    internal static CollabGeneration Generate(IReadOnlyList<ResolvedMod> mods,
        IReadOnlyList<ContentMountRecord> content, string contentRoot, IReadOnlyList<MapProgressionRecord> maps)
    {
        List<CollabDescriptorRecord> collabs = [];
        foreach (ResolvedMod mod in mods)
        {
            FileRecord[] markers = mod.Input.Files.Where(file => file.Path == "CollabUtils2CollabID.txt").ToArray();
            if (markers.Length == 0) continue;
            if (markers.Length != 1 || markers[0].Bytes is < 1 or > 128)
                throw new InvalidDataException($"invalid CollabUtils2 collab ID marker for {mod.Metadata.Name}");
            string id = File.ReadAllText(Path.Combine(mod.Input.StagingRoot, markers[0].Path), Encoding.UTF8).Trim();
            if (id.Length is < 1 or > 96 || id.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-')))
                throw new InvalidDataException($"invalid CollabUtils2 collab ID for {mod.Metadata.Name}");

            Dictionary<string, string> dialog = ReadDialog(mod.Input);
            ContentMountRecord[] mounts = content.Where(item => item.Owner == mod.Metadata.Name &&
                    item.LogicalPath.StartsWith("Maps/" + id + "/", StringComparison.Ordinal) &&
                    item.LogicalPath.EndsWith(".bin", StringComparison.Ordinal))
                .OrderBy(item => item.LogicalPath, StringComparer.Ordinal).ToArray();
            MapProgressionRecord[] owned = mounts.Select(item =>
                    maps.Single(map => map.Sid == item.LogicalPath["Maps/".Length..^4]))
                .OrderBy(map => map.Sid, StringComparer.Ordinal).ToArray();
            MapProgressionRecord[] lobbies = owned.Where(map =>
                map.Sid.StartsWith(id + "/0-Lobbies/", StringComparison.Ordinal)).ToArray();
            if (lobbies.Length != 1)
                throw new InvalidDataException($"{mod.Metadata.Name} must expose exactly one bounded CollabUtils2 lobby");
            MapProgressionRecord lobby = lobbies[0];
            MapProgressionRecord[] subordinate = owned.Where(map => map != lobby).ToArray();
            if (subordinate.Length < 2)
                throw new InvalidDataException($"{mod.Metadata.Name} collab must expose at least two subordinate maps");

            ContentMountRecord lobbyMount = mounts.Single(item => item.LogicalPath == "Maps/" + lobby.Sid + ".bin");
            MapElementRecord[] elements = ContentCompiler.InspectElements(StagedPath(contentRoot, lobbyMount)).ToArray();
            MapElementRecord[] panels = elements.Where(item =>
                item.Kind == "trigger" && item.Id == "CollabUtils2/ChapterPanelTrigger").ToArray();
            string[] targets = panels.Select(panel => panel.Attributes.GetValueOrDefault("map", ""))
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] expected = subordinate.Select(map => map.Sid).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!targets.SequenceEqual(expected, StringComparer.Ordinal))
                throw new InvalidDataException($"{mod.Metadata.Name} lobby chapter-panel targets do not exactly match its subordinate maps");

            MapElementRecord[] journals = elements.Where(item =>
                item.Kind == "trigger" && item.Id == "CollabUtils2/JournalTrigger").ToArray();
            if (journals.Length != 1)
                throw new InvalidDataException($"{mod.Metadata.Name} lobby must expose exactly one bounded CollabUtils2 journal");
            MapElementRecord journal = journals[0];
            string journalLevelSet = journal.Attributes.GetValueOrDefault("levelset", "");
            string commonLevelSet = subordinate.Select(map => map.LevelSet).Distinct(StringComparer.Ordinal).SingleOrDefault()
                ?? throw new InvalidDataException($"{mod.Metadata.Name} subordinate maps do not share one lobby LevelSet");
            if (journalLevelSet != commonLevelSet)
                throw new InvalidDataException($"{mod.Metadata.Name} lobby journal does not target its subordinate LevelSet");

            MapElementRecord[] players = elements.Where(item => item.Kind == "entity" && item.Id == "player").ToArray();
            CollabMapRecord[] mapRecords = panels.OrderBy(panel => panel.X).Select((panel, index) =>
            {
                string sid = panel.Attributes["map"];
                MapProgressionRecord map = subordinate.Single(value => value.Sid == sid);
                ContentMountRecord mount = mounts.Single(item => item.LogicalPath == "Maps/" + sid + ".bin");
                MapElementRecord[] roomPlayers = players.Where(value => value.Room == panel.Room).ToArray();
                if (roomPlayers.Length != 1)
                    throw new InvalidDataException($"{mod.Metadata.Name} chapter panel requires one unambiguous lobby return spawn in room {panel.Room}");
                MapElementRecord player = roomPlayers[0];
                bool allowSaving = ParseBool(panel.Attributes, "allowSaving", true);
                string returnMode = panel.Attributes.GetValueOrDefault("returnToLobbyMode", "SetReturnToHere");
                if (!allowSaving || returnMode != "SetReturnToHere")
                    throw new InvalidDataException($"{mod.Metadata.Name} uses unsupported chapter-panel return semantics");
                return new CollabMapRecord(
                    sid, lobby.Sid, Dialog(dialog, DialogKey(sid), DisplayName(sid)),
                    Dialog(dialog, DialogKey(sid) + "_author", "Unknown author"), index,
                    mount.SourceSha256, mount.Sha256, map.CompatibilityId, map.LevelSet, map.Rooms,
                    allowSaving, returnMode, panel.Room, player.X, player.Y);
            }).ToArray();

            string archiveSha = File.Exists(mod.Input.SourcePath) ? Hashing.FileSha256(mod.Input.SourcePath) : "directory-input";
            collabs.Add(new CollabDescriptorRecord(
                id, Dialog(dialog, "modname_" + id, mod.Metadata.Name), mod.Metadata.Name, mod.Metadata.Version,
                mod.Input.SourceSha256, archiveSha, lobby.Sid,
                Dialog(dialog, DialogKey(lobby.Sid), DisplayName(lobby.Sid)),
                lobbyMount.SourceSha256, lobbyMount.Sha256, lobby.CompatibilityId, lobby.LevelSet, lobby.Rooms,
                journalLevelSet, ParseBool(journal.Attributes, "vanillaJournal", false),
                ParseBool(journal.Attributes, "showOnlyDiscovered", false), mapRecords));
        }

        string manifest = Manifest(collabs);
        string sha = Hashing.BytesSha256(Encoding.UTF8.GetBytes(manifest));
        return new(Source(collabs, sha), manifest, collabs, sha);
    }

    private static string Manifest(IEnumerable<CollabDescriptorRecord> collabs) =>
        "APPLE_EVEREST_STATIC_COLLAB_V1\n" + string.Join("\n", collabs
            .OrderBy(value => value.Id, StringComparer.Ordinal).SelectMany(collab =>
                new[]
                {
                    string.Join('\t', "collab", collab.Id, collab.DisplayName, collab.Owner, collab.Version,
                        collab.SourceLogicalSha256, collab.ArchiveSha256),
                    string.Join('\t', "lobby", collab.Id, collab.LobbySid, collab.LobbyDisplayName,
                        collab.LobbySourceMapSha256, collab.LobbyMountedMapSha256, collab.LobbyCompatibilityId,
                        collab.LobbyLevelSet, string.Join(',', collab.LobbyRooms), collab.JournalLevelSet,
                        collab.JournalVanilla ? "vanilla-journal" : "collab-journal",
                        collab.JournalShowOnlyDiscovered ? "discovered-only" : "all-maps")
                }.Concat(collab.Maps.OrderBy(map => map.Order).Select(map => string.Join('\t',
                    "map", collab.Id, map.Order, map.Sid, map.DisplayName, map.Author,
                    map.SourceMapSha256, map.MountedMapSha256, map.CompatibilityId, map.LevelSet,
                    string.Join(',', map.Rooms), map.AllowSaving ? "saving" : "no-saving", map.ReturnMode,
                    map.ReturnRoom, Invariant(map.ReturnX), Invariant(map.ReturnY)))))) + "\n";

    private static Dictionary<string, string> ReadDialog(ModInput input)
    {
        FileRecord? record = input.Files.SingleOrDefault(value => value.Path == "Dialog/English.txt");
        if (record == null) return new(StringComparer.Ordinal);
        if (record.Bytes is < 1 or > 1024 * 1024) throw new InvalidDataException("collab English dialog exceeds bounds");
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (string raw in File.ReadAllLines(Path.Combine(input.StagingRoot, record.Path), new UTF8Encoding(false, true)))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int equals = line.IndexOf('=');
            if (equals <= 0) continue;
            string key = line[..equals].Trim();
            string value = line[(equals + 1)..].Trim();
            if (key.Length > 256 || value.Length > 1024 || !result.TryAdd(key, value))
                throw new InvalidDataException("invalid or duplicate collab English dialog key");
        }
        return result;
    }

    private static string Dialog(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out string? value) && value.Length != 0 ? value : fallback;
    private static string DialogKey(string sid) => sid.Replace('/', '_').Replace('-', '_');
    private static bool ParseBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        !values.TryGetValue(key, out string? raw) ? fallback : bool.TryParse(raw, out bool value)
            ? value : throw new InvalidDataException($"invalid collab boolean attribute: {key}");

    private static string DisplayName(string sid)
    {
        string value = sid[(sid.LastIndexOf('/') + 1)..];
        StringBuilder result = new();
        for (int index = 0; index < value.Length; index++)
        {
            char ch = value[index];
            if (index > 0 && char.IsUpper(ch) && char.IsLower(value[index - 1])) result.Append(' ');
            result.Append(index == 0 ? char.ToUpperInvariant(ch) : ch);
        }
        return result.ToString();
    }

    private static string StagedPath(string root, ContentMountRecord mount) =>
        Path.Combine(root, mount.LogicalPath.Replace('/', Path.DirectorySeparatorChar));
    private static string Invariant(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Source(IReadOnlyList<CollabDescriptorRecord> collabs, string sha)
    {
        StringBuilder source = new("namespace Celeste.Mod;\n\ninternal static class GeneratedAppleEverestCollabManifest\n{\n");
        source.Append("    internal const string Sha256 = \"").Append(sha).AppendLine("\";")
            .AppendLine("    internal static readonly AppleEverestCollabDescriptor[] Collabs =")
            .AppendLine("    {");
        foreach (CollabDescriptorRecord collab in collabs.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            source.Append("        new(\"").Append(Escape(collab.Id)).Append("\", \"")
                .Append(Escape(collab.DisplayName)).Append("\", \"").Append(Escape(collab.Owner)).Append("\", \"")
                .Append(Escape(collab.Version)).Append("\", \"").Append(Escape(collab.SourceLogicalSha256)).Append("\", \"")
                .Append(Escape(collab.ArchiveSha256)).Append("\", \"").Append(Escape(collab.LobbySid)).Append("\", \"")
                .Append(Escape(collab.LobbyDisplayName)).Append("\", \"").Append(Escape(collab.LobbySourceMapSha256)).Append("\", \"")
                .Append(Escape(collab.LobbyMountedMapSha256)).Append("\", \"").Append(Escape(collab.LobbyCompatibilityId)).Append("\", \"")
                .Append(Escape(collab.LobbyLevelSet)).Append("\", ").Append(StringArray(collab.LobbyRooms)).Append(", \"")
                .Append(Escape(collab.JournalLevelSet)).Append("\", ").Append(collab.JournalVanilla ? "true" : "false")
                .Append(", ").Append(collab.JournalShowOnlyDiscovered ? "true" : "false")
                .Append(", new AppleEverestCollabMapDescriptor[] { ");
            foreach (CollabMapRecord map in collab.Maps.OrderBy(value => value.Order))
                source.Append("new(\"").Append(Escape(map.Sid)).Append("\", \"").Append(Escape(map.LobbySid))
                    .Append("\", \"").Append(Escape(map.DisplayName)).Append("\", \"").Append(Escape(map.Author)).Append("\", ")
                    .Append(map.Order).Append(", \"").Append(Escape(map.SourceMapSha256)).Append("\", \"")
                    .Append(Escape(map.MountedMapSha256)).Append("\", \"").Append(Escape(map.CompatibilityId)).Append("\", \"")
                    .Append(Escape(map.LevelSet)).Append("\", ").Append(StringArray(map.Rooms)).Append(", ")
                    .Append(map.AllowSaving ? "true" : "false").Append(", \"").Append(Escape(map.ReturnMode)).Append("\", \"")
                    .Append(Escape(map.ReturnRoom)).Append("\", ").Append(Invariant(map.ReturnX)).Append("f, ")
                    .Append(Invariant(map.ReturnY)).Append("f), ");
            source.AppendLine("}),");
        }
        return source.AppendLine("    };").AppendLine("}").ToString();
    }

    private static string StringArray(IEnumerable<string> values)
    {
        string[] items = values.ToArray();
        return items.Length == 0 ? "System.Array.Empty<string>()" :
            "new string[] { " + string.Join(", ", items.Select(value => "\"" + Escape(value) + "\"")) + " }";
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);
}
