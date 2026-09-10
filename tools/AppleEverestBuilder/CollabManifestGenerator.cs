using System.Globalization;
using System.Text;

namespace AppleEverestBuilder;

internal sealed record CollabGeneration(string Source, string ManifestText,
    IReadOnlyList<CollabDescriptorRecord> Collabs, string Sha256);

internal static class CollabManifestGenerator
{
    private static readonly HashSet<string> SpecialBerryIds = new(StringComparer.Ordinal)
    {
        "CollabUtils2/SilverBerry", "CollabUtils2/SpeedBerry", "CollabUtils2/RainbowBerry",
        "MaxHelpingHand/SecretBerry", "goldenBerry"
    };

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

            ContentMountRecord[] mounts = content.Where(item => item.Owner == mod.Metadata.Name &&
                    item.LogicalPath.StartsWith("Maps/" + id + "/", StringComparison.Ordinal) &&
                    item.LogicalPath.EndsWith(".bin", StringComparison.Ordinal))
                .OrderBy(item => item.LogicalPath, StringComparer.Ordinal).ToArray();
            // A semantic module can deliberately contribute lifecycle/state only.
            // Its source archive may still carry a collab marker, but an empty
            // content allow-list means the playable collab is outside this
            // closure and must not be inferred from the unmounted source files.
            if (mounts.Length == 0 && mod.StaticSemanticLowering?.ContentPrefixes != null)
                continue;
            Dictionary<string, string> dialog = ReadDialog(mod.Input);
            MapProgressionRecord[] owned = mounts.Select(item =>
                    maps.Single(map => map.Sid == item.LogicalPath["Maps/".Length..^4]))
                .OrderBy(map => map.Sid, StringComparer.Ordinal).ToArray();
            MapProgressionRecord[] lobbies = owned.Where(map =>
                map.Sid.StartsWith(id + "/0-Lobbies/", StringComparison.Ordinal)).ToArray();
            if (lobbies.Length != 1)
                throw new InvalidDataException($"{mod.Metadata.Name} must expose exactly one bounded CollabUtils2 lobby");
            MapProgressionRecord lobby = lobbies[0];
            MapProgressionRecord[] subordinate = owned.Where(map => map != lobby).ToArray();
            bool selectedBeginner = IsPinnedBeginnerSelection(mod, owned, mounts);
            if (!selectedBeginner && subordinate.Length < 2)
                throw new InvalidDataException($"{mod.Metadata.Name} collab must expose at least two subordinate maps");

            ContentMountRecord lobbyMount = mounts.Single(item => item.LogicalPath == "Maps/" + lobby.Sid + ".bin");
            MapElementRecord[] elements = ContentCompiler.InspectElements(StagedPath(contentRoot, lobbyMount)).ToArray();
            MapElementRecord[] panels = elements.Where(item =>
                item.Kind == "trigger" && item.Id == "CollabUtils2/ChapterPanelTrigger" ||
                selectedBeginner && item.Kind == "entity" && item.Id == "SJ2021/StrawberryJamJar").ToArray();
            string[] targets = panels.Select(panel => panel.Attributes.GetValueOrDefault("map", ""))
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] expected = subordinate.Select(map => map.Sid).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (selectedBeginner) ValidateBeginnerEntrances(panels);
            else if (!targets.SequenceEqual(expected, StringComparer.Ordinal))
                throw new InvalidDataException($"{mod.Metadata.Name} lobby chapter-panel targets do not exactly match its subordinate maps");

            MapElementRecord[] journals = elements.Where(item =>
                item.Kind == "trigger" && item.Id == "CollabUtils2/JournalTrigger").ToArray();
            if (journals.Length == 0)
                throw new InvalidDataException($"{mod.Metadata.Name} lobby must expose at least one bounded CollabUtils2 journal");
            string commonLevelSet = subordinate.Select(map => map.LevelSet).Distinct(StringComparer.Ordinal).SingleOrDefault()
                ?? throw new InvalidDataException($"{mod.Metadata.Name} subordinate maps do not share one lobby LevelSet");
            if (journals.Any(item => item.Attributes.GetValueOrDefault("levelset", "") != commonLevelSet))
                throw new InvalidDataException($"{mod.Metadata.Name} lobby journal does not target its subordinate LevelSet");
            bool[] journalVanilla = journals.Select(item => ParseBool(item.Attributes, "vanillaJournal", false)).Distinct().ToArray();
            bool[] journalDiscovered = journals.Select(item => ParseBool(item.Attributes, "showOnlyDiscovered", false)).Distinct().ToArray();
            if (journalVanilla.Length != 1 || journalDiscovered.Length != 1)
                throw new InvalidDataException($"{mod.Metadata.Name} lobby journals disagree on bounded presentation semantics");
            MapElementRecord journal = journals.OrderBy(item => item.Room, StringComparer.Ordinal)
                .ThenBy(item => item.X).ThenBy(item => item.Y).First();
            string journalLevelSet = commonLevelSet;

            MapElementRecord[] players = elements.Where(item => item.Kind == "entity" && item.Id == "player").ToArray();
            CollabMapRecord[] mapRecords = panels.Where(panel => expected.Contains(panel.Attributes.GetValueOrDefault("map", ""), StringComparer.Ordinal))
                .OrderBy(panel => panel.X).Select((panel, index) =>
            {
                string sid = panel.Attributes["map"];
                MapProgressionRecord map = subordinate.Single(value => value.Sid == sid);
                ContentMountRecord mount = mounts.Single(item => item.LogicalPath == "Maps/" + sid + ".bin");
                MapElementRecord[] roomPlayers = players.Where(value => value.Room == panel.Room).ToArray();
                if (roomPlayers.Length == 0)
                    throw new InvalidDataException($"{mod.Metadata.Name} chapter panel has no lobby return spawn in room {panel.Room}");
                // This is only the direct-start recovery route. Live chapter
                // launches capture Level.GetSpawnPoint(player.Position). Match
                // ClosestTo's Single arithmetic and first-authored tie order.
                float centerX = panel.X + panel.Width / 2f;
                float centerY = panel.Y + panel.Height / 2f;
                var rankedPlayers = roomPlayers.Select(value => new
                    {
                        Player = value,
                        Distance = (value.X - centerX) * (value.X - centerX) + (value.Y - centerY) * (value.Y - centerY)
                    })
                    .OrderBy(value => value.Distance)
                    .ToArray();
                MapElementRecord player = rankedPlayers[0].Player;
                // Exact CollabUtils2 1.13.4 ChapterPanelTrigger default.
                bool allowSaving = ParseBool(panel.Attributes, "allowSaving", true);
                string returnMode = panel.Attributes.GetValueOrDefault("returnToLobbyMode", "SetReturnToHere");
                if (returnMode != "SetReturnToHere")
                    throw new InvalidDataException($"{mod.Metadata.Name} uses unsupported chapter-panel return semantics");
                MapElementRecord[] mapElements = ContentCompiler.InspectElements(StagedPath(contentRoot, mount)).ToArray();
                CollabSpecialBerryRecord[] specialBerries = SpecialBerries(mapElements, commonLevelSet);
                int miniHeartCount = mapElements.Count(item =>
                    item.Kind == "entity" && item.Id == "CollabUtils2/MiniHeart");
                return new CollabMapRecord(
                    sid, lobby.Sid, Dialog(dialog, DialogKey(sid), DisplayName(sid)),
                    Dialog(dialog, DialogKey(sid) + "_author", "Unknown author"), index,
                    mount.SourceSha256, mount.Sha256, map.CompatibilityId, map.LevelSet, map.Rooms,
                    allowSaving, returnMode, panel.Room, player.X + player.RoomX, player.Y + player.RoomY, map.Strawberries,
                    map.Heart, map.CompletionAvailable, miniHeartCount, specialBerries);
            }).ToArray();

            string[] contributingMaps = mapRecords.Where(map => map.MiniHeartCount > 0)
                .Select(map => map.Sid).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            CollabMiniHeartDoorRecord[] doors = elements.Where(item =>
                    item.Kind == "entity" && item.Id == "CollabUtils2/MiniHeartDoor")
                .OrderBy(item => item.Room, StringComparer.Ordinal).ThenBy(item => item.Y).ThenBy(item => item.X)
                .Select(item => new CollabMiniHeartDoorRecord(
                    Int(item.Attributes, "id", 0, int.MaxValue), item.Room, item.X, item.Y,
                    item.Width, item.Height, Int(item.Attributes, "requires", 0, 9999),
                    item.Attributes.GetValueOrDefault("levelSet", commonLevelSet),
                    item.Attributes.GetValueOrDefault("doorID", ""),
                    item.Attributes.GetValueOrDefault("color", "18668F"), contributingMaps)).ToArray();
            if (selectedBeginner && doors.Length != 1)
                throw new InvalidDataException("selected Beginner must retain its one authored mini-heart door");
            foreach (CollabMiniHeartDoorRecord door in doors)
            {
                if (door.LevelSet != commonLevelSet)
                    throw new InvalidDataException($"{mod.Metadata.Name} mini-heart door targets an unrelated LevelSet");
                if (selectedBeginner && (door.EntityId != 657 || door.Requires != 21))
                    throw new InvalidDataException("selected Beginner door metadata differs");
                if (!selectedBeginner && door.Requires > contributingMaps.Length)
                    throw new InvalidDataException($"{mod.Metadata.Name} mini-heart door threshold exceeds authored contributing maps");
            }
            CollabSpecialBerryRecord[] lobbySpecialBerries = SpecialBerries(elements, commonLevelSet);

            string archiveSha = File.Exists(mod.Input.SourcePath) ? Hashing.FileSha256(mod.Input.SourcePath) : "directory-input";
            collabs.Add(new CollabDescriptorRecord(
                id, Dialog(dialog, "modname_" + id, mod.Metadata.Name), mod.Metadata.Name, mod.Metadata.Version,
                mod.Input.SourceSha256, archiveSha, lobby.Sid,
                Dialog(dialog, DialogKey(lobby.Sid), DisplayName(lobby.Sid)),
                lobbyMount.SourceSha256, lobbyMount.Sha256, lobby.CompatibilityId, lobby.LevelSet, lobby.Rooms,
                journalLevelSet, ParseBool(journal.Attributes, "vanillaJournal", false),
                ParseBool(journal.Attributes, "showOnlyDiscovered", false), doors, lobbySpecialBerries, mapRecords));
        }

        string manifest = Manifest(collabs);
        string sha = Hashing.BytesSha256(Encoding.UTF8.GetBytes(manifest));
        return new(Source(collabs, sha), manifest, collabs, sha);
    }

    private static bool IsPinnedBeginnerSelection(ResolvedMod mod, MapProgressionRecord[] owned, ContentMountRecord[] mounts)
    {
        if (mod.Metadata.Name != "StrawberryJam2021") return false;
        if (mod.StaticSemanticLowering?.Id != "strawberryjam2021-1.0.12-beginner-root-v1")
            throw new InvalidDataException("partial collab selection requires the exact reviewed SJ provider identity");
        if (!owned.Select(map => map.Sid).Order(StringComparer.Ordinal).SequenceEqual(
            mounts.Select(mount => mount.LogicalPath["Maps/".Length..^4]).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("selected SJ progression/mount census differs");
        ValidatePinnedBeginnerMaps(mounts.Select(mount =>
            (mount.LogicalPath["Maps/".Length..^4], mount.SourceSha256)).ToArray());
        return true;
    }

    internal static void ValidatePinnedBeginnerMaps((string Sid, string SourceSha256)[] maps)
    {
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["StrawberryJam2021/0-Lobbies/1-Beginner"] = "a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2",
            ["StrawberryJam2021/1-Beginner/Bing_Over_Google"] = "e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347"
        };
        // Keep the original two-map product available as a historical control.
        // K-N adds exactly one unchanged source, never a general partial-collab
        // allowance. Production readiness remains a separate mandatory gate.
        if (maps.Any(map => map.Sid == SnasFlagGroups.Sid))
            expected.Add(SnasFlagGroups.Sid, SnasFlagGroups.SourceSha256);
        if (maps.Length != expected.Count || maps.Select(map => map.Sid).Distinct(StringComparer.Ordinal).Count() != maps.Length ||
            maps.Any(map => expected.GetValueOrDefault(map.Sid) != map.SourceSha256))
            throw new InvalidDataException("partial SJ collab selection differs from the exact K-L or K-N original identities");
    }

    private static void ValidateBeginnerEntrances(MapElementRecord[] entrances)
    {
        // Metadata-only source catalog: never create placeholder playable areas
        // for excluded jars, gym or heart-side. The available maps are selected
        // independently; this source catalog always retains all 23 entrances.
        using Stream stream = typeof(SelectedFactoryProfiles).Assembly.GetManifestResourceStream("AppleEverest.SelectedFactoryProfiles")!;
        using System.Text.Json.JsonDocument profiles = System.Text.Json.JsonDocument.Parse(stream);
        string[] expected = profiles.RootElement.GetProperty("occurrences").EnumerateArray()
            .Where(row => row.GetProperty("customId").GetString() is "SJ2021/StrawberryJamJar" or "CollabUtils2/ChapterPanelTrigger")
            .Select(row => row.GetProperty("attributes").GetProperty("map").GetString()!)
            .Order(StringComparer.Ordinal).ToArray();
        string[] actual = entrances.Select(row => row.Attributes.GetValueOrDefault("map", "")).Order(StringComparer.Ordinal).ToArray();
        if (expected.Length != 23 || expected.Distinct(StringComparer.Ordinal).Count() != 23 ||
            entrances.Count(row => row.Id == "SJ2021/StrawberryJamJar") != 21 || !actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException("selected Beginner entrance metadata must retain the exact 21 jars and two panels");
    }

    private static string Manifest(IEnumerable<CollabDescriptorRecord> collabs) =>
        "APPLE_EVEREST_STATIC_COLLAB_V2\n" + string.Join("\n", collabs
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
                }.Concat(collab.MiniHeartDoors.Select(door => string.Join('\t',
                    "mini-heart-door", collab.Id, door.EntityId, door.Room, Invariant(door.X), Invariant(door.Y),
                    door.Width, door.Height, door.Requires, door.LevelSet, door.DoorId, door.Color,
                    string.Join(',', door.ContributingMapSids))))
                .Concat(collab.LobbySpecialBerries.Select(berry => BerryManifest("lobby-special-berry", collab.Id, berry)))
                .Concat(collab.Maps.OrderBy(map => map.Order).SelectMany(map => new[] { string.Join('\t',
                    "map", collab.Id, map.Order, map.Sid, map.DisplayName, map.Author,
                    map.SourceMapSha256, map.MountedMapSha256, map.CompatibilityId, map.LevelSet,
                    string.Join(',', map.Rooms), map.AllowSaving ? "saving" : "no-saving", map.ReturnMode,
                    map.ReturnRoom, Invariant(map.ReturnX), Invariant(map.ReturnY), map.AuthoredStrawberries,
                    map.AuthoredHeart ? "heart" : "no-heart", map.CompletionAvailable ? "completion" : "no-completion",
                    map.MiniHeartCount) }.Concat(map.SpecialBerries.Select(berry =>
                        BerryManifest("map-special-berry", collab.Id + "\t" + map.Sid, berry))))))) + "\n";

    private static string BerryManifest(string kind, string owner, CollabSpecialBerryRecord berry) => string.Join('\t',
        kind, owner, berry.EntityType, berry.SemanticClass, berry.Durability, berry.EntityId, berry.Room,
        Invariant(berry.X), Invariant(berry.Y), berry.LevelSet, berry.Maps, berry.Requires,
        berry.AlwaysSpawn, berry.CountTowardsTotal, Invariant(berry.GoldTime), Invariant(berry.SilverTime),
        Invariant(berry.BronzeTime), berry.Sprite);

    private static CollabSpecialBerryRecord[] SpecialBerries(IEnumerable<MapElementRecord> elements, string defaultLevelSet) =>
        elements.Where(item => item.Kind == "entity" && SpecialBerryIds.Contains(item.Id))
            .OrderBy(item => item.Room, StringComparer.Ordinal).ThenBy(item => item.Y).ThenBy(item => item.X)
            .Select(item =>
            {
                string semanticClass = item.Id is "CollabUtils2/SilverBerry" or "CollabUtils2/RainbowBerry"
                    ? "REQUIRED_BY_GRAPH" : "SUPPORTED_UNUSED";
                string durability = item.Id switch
                {
                    "CollabUtils2/SilverBerry" => "completion-derived-run-local-golden",
                    "CollabUtils2/SpeedBerry" => "completion-derived-run-local-timer",
                    "CollabUtils2/RainbowBerry" => "silver-completion-derived",
                    "MaxHelpingHand/SecretBerry" => "vanilla-strawberry-save-data-if-counted",
                    "goldenBerry" => "vanilla-golden-run-local-until-completion",
                    _ => throw new InvalidDataException("unclassified collab special berry")
                };
                return new CollabSpecialBerryRecord(item.Id, semanticClass, durability,
                    Int(item.Attributes, "id", 0, int.MaxValue), item.Room, item.X, item.Y,
                    item.Attributes.GetValueOrDefault("levelSet", defaultLevelSet),
                    item.Attributes.GetValueOrDefault("maps", ""), Int(item.Attributes, "requires", -1, 9999),
                    Bool(item.Attributes, "alwaysSpawn", false), Bool(item.Attributes, "countTowardsTotal", true),
                    Float(item.Attributes, "goldTime", 0f, 86400f), Float(item.Attributes, "silverTime", 0f, 86400f),
                    Float(item.Attributes, "bronzeTime", 0f, 86400f),
                    item.Attributes.GetValueOrDefault("strawberrySprite", ""));
            }).ToArray();

    private static int Int(IReadOnlyDictionary<string, string> values, string key, int fallback, int maximum)
    {
        if (!values.TryGetValue(key, out string? raw)) return fallback;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) &&
               value >= fallback && value <= maximum ? value
            : throw new InvalidDataException($"invalid collab integer attribute: {key}");
    }

    private static float Float(IReadOnlyDictionary<string, string> values, string key, float fallback, float maximum)
    {
        if (!values.TryGetValue(key, out string? raw)) return fallback;
        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) &&
               float.IsFinite(value) && value >= 0f && value <= maximum ? value
            : throw new InvalidDataException($"invalid collab number attribute: {key}");
    }

    private static bool Bool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        !values.TryGetValue(key, out string? raw) ? fallback : bool.TryParse(raw, out bool value)
            ? value : throw new InvalidDataException($"invalid collab boolean attribute: {key}");

    private static Dictionary<string, string> ReadDialog(ModInput input)
    {
        FileRecord? record = input.Files.SingleOrDefault(value => value.Path == "Dialog/English.txt");
        if (record == null) return new(StringComparer.OrdinalIgnoreCase);
        if (record.Bytes is < 1 or > 1024 * 1024) throw new InvalidDataException("collab English dialog exceeds bounds");
        return global::Celeste.Mod.AppleEverestDialogFragmentParser.Parse(
            File.ReadAllText(Path.Combine(input.StagingRoot, record.Path), new UTF8Encoding(false, true)),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Cleaned, StringComparer.OrdinalIgnoreCase);
    }

    private static string Dialog(IReadOnlyDictionary<string, string> values, string key, string fallback)
    {
        string value = values.TryGetValue(key, out string? text) && !string.IsNullOrWhiteSpace(text) ? text : fallback;
        if (key.Length > 256 || value.Length > 1024) throw new InvalidDataException("collab presentation text exceeds bounds");
        return value;
    }
    private static string DialogKey(string sid) => sid.Replace('/', '_').Replace('-', '_').Replace('+', '_').Replace(' ', '_');
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
                .Append(", ").Append(DoorArray(collab.MiniHeartDoors))
                .Append(", ").Append(BerryArray(collab.LobbySpecialBerries))
                .Append(", new AppleEverestCollabMapDescriptor[] { ");
            foreach (CollabMapRecord map in collab.Maps.OrderBy(value => value.Order))
                source.Append("new(\"").Append(Escape(map.Sid)).Append("\", \"").Append(Escape(map.LobbySid))
                    .Append("\", \"").Append(Escape(map.DisplayName)).Append("\", \"").Append(Escape(map.Author)).Append("\", ")
                    .Append(map.Order).Append(", \"").Append(Escape(map.SourceMapSha256)).Append("\", \"")
                    .Append(Escape(map.MountedMapSha256)).Append("\", \"").Append(Escape(map.CompatibilityId)).Append("\", \"")
                    .Append(Escape(map.LevelSet)).Append("\", ").Append(StringArray(map.Rooms)).Append(", ")
                    .Append(map.AllowSaving ? "true" : "false").Append(", \"").Append(Escape(map.ReturnMode)).Append("\", \"")
                    .Append(Escape(map.ReturnRoom)).Append("\", ").Append(Invariant(map.ReturnX)).Append("f, ")
                    .Append(Invariant(map.ReturnY)).Append("f, ").Append(map.AuthoredStrawberries).Append(", ")
                    .Append(map.AuthoredHeart ? "true" : "false").Append(", ")
                    .Append(map.CompletionAvailable ? "true" : "false").Append(", ").Append(map.MiniHeartCount)
                    .Append(", ").Append(BerryArray(map.SpecialBerries)).Append("), ");
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

    private static string DoorArray(IEnumerable<CollabMiniHeartDoorRecord> values)
    {
        CollabMiniHeartDoorRecord[] items = values.ToArray();
        if (items.Length == 0) return "System.Array.Empty<AppleEverestCollabMiniHeartDoorDescriptor>()";
        return "new AppleEverestCollabMiniHeartDoorDescriptor[] { " + string.Join("", items.Select(value =>
            "new(" + value.EntityId + ", \"" + Escape(value.Room) + "\", " + Invariant(value.X) + "f, " +
            Invariant(value.Y) + "f, " + value.Width + ", " + value.Height + ", " + value.Requires + ", \"" +
            Escape(value.LevelSet) + "\", \"" + Escape(value.DoorId) + "\", \"" + Escape(value.Color) + "\", " +
            StringArray(value.ContributingMapSids) + "), ")) + "}";
    }

    private static string BerryArray(IEnumerable<CollabSpecialBerryRecord> values)
    {
        CollabSpecialBerryRecord[] items = values.ToArray();
        if (items.Length == 0) return "System.Array.Empty<AppleEverestCollabSpecialBerryDescriptor>()";
        return "new AppleEverestCollabSpecialBerryDescriptor[] { " + string.Join("", items.Select(value =>
            "new(\"" + Escape(value.EntityType) + "\", \"" + Escape(value.SemanticClass) + "\", \"" +
            Escape(value.Durability) + "\", " + value.EntityId + ", \"" + Escape(value.Room) + "\", " +
            Invariant(value.X) + "f, " + Invariant(value.Y) + "f, \"" + Escape(value.LevelSet) + "\", \"" +
            Escape(value.Maps) + "\", " + value.Requires + ", " + (value.AlwaysSpawn ? "true" : "false") +
            ", " + (value.CountTowardsTotal ? "true" : "false") + ", " + Invariant(value.GoldTime) + "f, " +
            Invariant(value.SilverTime) + "f, " + Invariant(value.BronzeTime) + "f, \"" + Escape(value.Sprite) +
            "\"), ")) + "}";
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);
}
