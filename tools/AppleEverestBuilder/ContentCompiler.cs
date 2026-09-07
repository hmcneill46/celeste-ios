using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using YamlDotNet.RepresentationModel;

namespace AppleEverestBuilder;

internal static class ContentCompiler
{
    // Stage 25K-C measured every distributed SJ 1.0.12 map: the largest
    // desktop-ignored appendix is 1,260,932 bytes. Keep the compatibility
    // rule deliberately bounded rather than treating arbitrary trailing data
    // as a valid map extension.
    internal const long MaxMapAppendixBytes = 2L * 1024 * 1024;

    internal static MapProgressionRecord InspectProgression(string path, string logicalPath, string sha256,
        IReadOnlySet<string>? staticallyLoweredStrawberryEntities = null)
    {
        if (!logicalPath.StartsWith("Maps/", StringComparison.Ordinal) ||
            !logicalPath.EndsWith(".bin", StringComparison.Ordinal))
            throw new InvalidDataException("progression inspection requires a mounted map");
        string mapPath = logicalPath["Maps/".Length..^4];
        int finalSlash = mapPath.LastIndexOf('/');
        string levelSet = finalSlash >= 0 ? mapPath[..finalSlash] : "";
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);
        if (reader.ReadString() != "CELESTE MAP") throw new InvalidDataException("map binary has an invalid Celeste header");
        string sourcePackageLabel = reader.ReadString();
        int count = reader.ReadInt16();
        if (count is < 1 or > 8192) throw new InvalidDataException("map string table is invalid");
        string[] table = new string[count];
        for (int index = 0; index < count; index++) table[index] = reader.ReadString();
        List<string> rooms = [];
        List<string> entities = [];
        List<string> triggers = [];
        List<(int Order, string Level)> checkpoints = [];
        MapPresentationBuilder presentation = new();
        // Everest first applies the adjacent .meta.yaml while AreaData is
        // discovered, then applies the map binary's embedded <meta> during
        // MapData.Load.  Preserve that order: embedded values are the final,
        // effective presentation seen by Session and LevelLoader.
        presentation.ApplySidecar(Path.ChangeExtension(path, ".meta.yaml"));
        int elements = 0;
        ReadProgressionElement(reader, table, null, null, 0, ref elements, rooms, entities, triggers,
            checkpoints, presentation);
        _ = InspectAppendix(stream);
        string[] entitySet = entities.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] triggerSet = triggers.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        int berries = entities.Count(value => value is "strawberry" or "goldenBerry" ||
            staticallyLoweredStrawberryEntities?.Contains(value) == true);
        bool heart = entities.Any(value => value is "blackGem" or "heartGem" or "CollabUtils2/MiniHeart");
        bool cassette = entities.Contains("cassette", StringComparer.Ordinal);
        string compatibility = Hashing.BytesSha256(Encoding.UTF8.GetBytes(
            "apple-everest-progression-map-v1\n" + mapPath + "\n" + sha256 + "\n" +
            string.Join("\n", rooms.OrderBy(value => value, StringComparer.Ordinal))));
        return new(mapPath, mapPath, levelSet, sha256, compatibility,
            rooms.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            berries, heart, cassette,
            checkpoints.GroupBy(value => value.Level, StringComparer.Ordinal)
                .Select(group => group.OrderBy(value => value.Order).First())
                .OrderBy(value => value.Order).ThenBy(value => value.Level, StringComparer.Ordinal)
                .Select(value => value.Level).ToArray(),
            entitySet, triggerSet, ["A"], !mapPath.Contains("/0-Lobbies/", StringComparison.Ordinal),
            presentation.Build(), sourcePackageLabel);
    }

    internal static IReadOnlyList<(string Kind, string Id)> InspectGameplayIds(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);
        try
        {
            if (reader.ReadString() != "CELESTE MAP") throw new InvalidDataException("map binary has an invalid Celeste header");
            string package = reader.ReadString();
            if (package.Length is < 1 or > 1024) throw new InvalidDataException("map binary package is invalid");
            int count = reader.ReadInt16();
            if (count is < 1 or > 8192) throw new InvalidDataException("map string table is invalid");
            string[] table = new string[count];
            for (int index = 0; index < count; index++)
            {
                table[index] = reader.ReadString();
                if (table[index].Length > 4096) throw new InvalidDataException("map string table value is too long");
            }
            List<(string Kind, string Id)> result = [];
            int elements = 0;
            ReadElement(reader, table, null, 0, ref elements, result);
            _ = InspectAppendix(stream);
            return result.Distinct().OrderBy(value => value.Kind, StringComparer.Ordinal)
                .ThenBy(value => value.Id, StringComparer.Ordinal).ToArray();
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("map binary body is truncated", exception);
        }
    }

    internal static IReadOnlyList<MapElementRecord> InspectElements(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);
        try
        {
            if (reader.ReadString() != "CELESTE MAP") throw new InvalidDataException("map binary has an invalid Celeste header");
            _ = reader.ReadString();
            int count = reader.ReadInt16();
            if (count is < 1 or > 8192) throw new InvalidDataException("map string table is invalid");
            string[] table = new string[count];
            for (int index = 0; index < count; index++) table[index] = reader.ReadString();
            List<MapElementRecord> result = [];
            int elements = 0;
            ReadDetailedElement(reader, table, null, "", 0, ref elements, result);
            _ = InspectAppendix(stream);
            return result;
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("map binary body is truncated", exception);
        }
    }

    internal static MapBinaryBoundaryRecord InspectBoundary(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
        try
        {
            if (reader.ReadString() != "CELESTE MAP")
                throw new InvalidDataException("map binary has an invalid Celeste header");
            string package = reader.ReadString();
            if (package.Length > 1024) throw new InvalidDataException("map binary package is invalid");
            int count = reader.ReadInt16();
            if (count is < 1 or > 8192) throw new InvalidDataException("map string table is invalid");
            string[] table = new string[count];
            for (int index = 0; index < count; index++)
            {
                table[index] = reader.ReadString();
                if (table[index].Length > 4096)
                    throw new InvalidDataException("map string table value is too long");
            }
            List<(string Kind, string Id)> ignored = [];
            int elements = 0;
            ReadElement(reader, table, null, 0, ref elements, ignored);
            return InspectAppendix(stream);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("map binary body is truncated", exception);
        }
    }

    private static MapBinaryBoundaryRecord InspectAppendix(Stream stream)
    {
        long rootBytes = stream.Position;
        long appendixBytes = stream.Length - rootBytes;
        if (appendixBytes < 0 || appendixBytes > MaxMapAppendixBytes)
            throw new InvalidDataException("map binary appendix exceeds the bounded compatibility limit");

        string appendixSha256;
        if (appendixBytes == 0)
        {
            appendixSha256 = Hashing.BytesSha256([]);
        }
        else
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[64 * 1024];
            long remaining = appendixBytes;
            while (remaining > 0)
            {
                int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read <= 0) throw new InvalidDataException("map binary appendix is truncated");
                hash.AppendData(buffer, 0, read);
                remaining -= read;
            }
            appendixSha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        return new(stream.Length, rootBytes, appendixBytes, appendixSha256);
    }

    public static string Stage(string source, string relative, string contentOutput, bool preserveOriginalMap = false)
    {
        string logical = relative.StartsWith("Content/", StringComparison.Ordinal)
            ? relative["Content/".Length..]
            : relative;
        if (logical.EndsWith(".asset.json", StringComparison.Ordinal))
        {
            logical = logical[..^".asset.json".Length] + ".png";
            string target = Target(contentOutput, logical);
            GeneratePng(source, target);
            return logical;
        }
        if (logical.StartsWith("Maps/", StringComparison.Ordinal) && logical.EndsWith(".xml", StringComparison.Ordinal))
        {
            logical = logical[..^4] + ".bin";
            string target = Target(contentOutput, logical);
            CompileMap(source, target, logical["Maps/".Length..^4]);
            return logical;
        }
        if (logical.StartsWith("Maps/", StringComparison.Ordinal) && logical.EndsWith(".bin", StringComparison.Ordinal))
        {
            string target = Target(contentOutput, logical);
            if (preserveOriginalMap)
            {
                _ = InspectBoundary(source);
                File.Copy(source, target, overwrite: true);
            }
            else NormalizeMapPackage(source, target, logical["Maps/".Length..^4]);
            return logical;
        }
        string destination = Target(contentOutput, logical);
        File.Copy(source, destination, overwrite: true);
        return logical;
    }

    private static string Target(string root, string relative)
    {
        string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(fullRoot, StringComparison.Ordinal)) throw new InvalidDataException("content output escaped closure root");
        Directory.CreateDirectory(Path.GetDirectoryName(result)!);
        return result;
    }

    private static void GeneratePng(string descriptorPath, string output)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(descriptorPath));
        JsonElement root = document.RootElement;
        int width = root.GetProperty("width").GetInt32();
        int height = root.GetProperty("height").GetInt32();
        if (width is < 1 or > 1024 || height is < 1 or > 1024) throw new InvalidDataException("generated asset dimensions are invalid");
        byte[] top = Hex(root.GetProperty("top").GetString()!);
        byte[] bottom = Hex(root.GetProperty("bottom").GetString()!);
        byte[] accent = Hex(root.GetProperty("accent").GetString()!);
        byte[] raw = new byte[(width * 4 + 1) * height];
        for (int y = 0; y < height; y++)
        {
            int row = y * (width * 4 + 1);
            raw[row] = 0;
            float amount = height == 1 ? 0f : (float)y / (height - 1);
            for (int x = 0; x < width; x++)
            {
                bool line = x < 4 || x >= width - 4 || y < 4 || y >= height - 4 || Math.Abs(x - width / 2) < 2;
                byte[] color = line ? accent : new[]
                {
                    (byte)Math.Round(top[0] + (bottom[0] - top[0]) * amount),
                    (byte)Math.Round(top[1] + (bottom[1] - top[1]) * amount),
                    (byte)Math.Round(top[2] + (bottom[2] - top[2]) * amount)
                };
                int offset = row + 1 + x * 4;
                raw[offset] = color[0]; raw[offset + 1] = color[1]; raw[offset + 2] = color[2]; raw[offset + 3] = 255;
            }
        }
        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true)) zlib.Write(raw);
        using FileStream stream = new(output, FileMode.Create, FileAccess.Write);
        stream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        WriteChunk(stream, "IHDR", UInt32(width).Concat(UInt32(height)).Concat(new byte[] { 8, 6, 0, 0, 0 }).ToArray());
        WriteChunk(stream, "IDAT", compressed.ToArray());
        WriteChunk(stream, "IEND", Array.Empty<byte>());
    }

    internal static void CompileMap(string xmlPath, string output, string package)
    {
        XmlDocument document = new() { PreserveWhitespace = false };
        document.Load(xmlPath);
        XmlElement root = document.DocumentElement ?? throw new InvalidDataException("map XML has no root element");
        List<string> table = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        void Add(string value) { if (seen.Add(value)) table.Add(value); }
        void Visit(XmlElement element)
        {
            Add(ElementName(element));
            foreach (XmlAttribute attribute in ElementAttributes(element))
            {
                Add(attribute.Name);
                if (AttributeValue(element, attribute).Type == 5) Add(attribute.Value);
            }
            foreach (XmlElement child in element.ChildNodes.OfType<XmlElement>()) Visit(child);
        }
        Visit(root); Add("innerText");
        Dictionary<string, short> lookup = table.Select((value, index) => (value, index)).ToDictionary(item => item.value, item => checked((short)item.index), StringComparer.Ordinal);
        using BinaryWriter writer = new(File.Open(output, FileMode.Create), Encoding.UTF8, leaveOpen: false);
        writer.Write("CELESTE MAP"); writer.Write(package); writer.Write(checked((short)table.Count));
        foreach (string value in table) writer.Write(value);
        WriteElement(writer, root, lookup);
    }

    private static void NormalizeMapPackage(string source, string output, string package)
    {
        // Everest accepts the generic package label emitted by common map
        // editors (normally "Contribution"), whereas vanilla Celeste insists
        // that this header equal the mounted ModeProperties path.  Normalize
        // only the two length-prefixed header strings at build time.  The
        // string table and complete element body remain the original pinned
        // mod bytes, and malformed/non-Celeste binaries fail closed.
        using FileStream input = File.OpenRead(source);
        using BinaryReader reader = new(input, Encoding.UTF8, leaveOpen: true);
        string magic;
        string sourcePackage;
        try
        {
            magic = reader.ReadString();
            sourcePackage = reader.ReadString();
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("map binary has an invalid Celeste header", exception);
        }
        if (magic != "CELESTE MAP") throw new InvalidDataException("map binary has an invalid Celeste header");
        // Older Everest sample maps may deliberately leave the package label
        // empty and rely on Everest to supply the mounted path. The Apple
        // closure already replaces that header with the exact logical map
        // path, so an empty source label is safe; only an oversized label is
        // rejected here.
        if (sourcePackage.Length > 1024) throw new InvalidDataException("map binary package is invalid");
        long bodyOffset = input.Position;
        byte[] body = new byte[checked((int)(input.Length - bodyOffset))];
        input.ReadExactly(body);
        if (body.Length < sizeof(short)) throw new InvalidDataException("map binary body is truncated");

        if (sourcePackage == package)
        {
            File.Copy(source, output, overwrite: true);
            return;
        }

        using BinaryWriter writer = new(File.Open(output, FileMode.Create), Encoding.UTF8, leaveOpen: false);
        writer.Write(magic);
        writer.Write(package);
        writer.Write(body);
    }

    private static void ReadElement(BinaryReader reader, string[] table, string? parent, int depth, ref int elements,
        List<(string Kind, string Id)> result)
    {
        if (depth > 128 || ++elements > 100000) throw new InvalidDataException("map element bounds exceeded");
        string name = Lookup(table, reader.ReadInt16());
        if (parent == "entities") result.Add(("entity", name));
        else if (parent == "triggers") result.Add(("trigger", name));
        else if (parent != null && (parent.Equals("backgrounds", StringComparison.OrdinalIgnoreCase) ||
                 parent.Equals("foregrounds", StringComparison.OrdinalIgnoreCase)))
            result.Add(("backdrop", name));
        int attributes = reader.ReadByte();
        for (int index = 0; index < attributes; index++)
        {
            _ = Lookup(table, reader.ReadInt16());
            switch (reader.ReadByte())
            {
                case 0: _ = reader.ReadBoolean(); break;
                case 1: _ = reader.ReadByte(); break;
                case 2: _ = reader.ReadInt16(); break;
                case 3: _ = reader.ReadInt32(); break;
                case 4: _ = reader.ReadSingle(); break;
                case 5: _ = Lookup(table, reader.ReadInt16()); break;
                case 6:
                    if (reader.ReadString().Length > 1024 * 1024) throw new InvalidDataException("map text value is too long");
                    break;
                case 7:
                    int length = reader.ReadInt16();
                    if (length < 0 || reader.ReadBytes(length).Length != length) throw new EndOfStreamException();
                    break;
                default: throw new InvalidDataException("map value type is invalid");
            }
        }
        int children = reader.ReadInt16();
        if (children < 0) throw new InvalidDataException("map child count is invalid");
        for (int index = 0; index < children; index++) ReadElement(reader, table, name, depth + 1, ref elements, result);
    }

    private static void ReadProgressionElement(BinaryReader reader, string[] table, string? parent,
        string? currentRoom, int depth, ref int elements, List<string> rooms, List<string> entities,
        List<string> triggers, List<(int Order, string Level)> checkpoints, MapPresentationBuilder presentation)
    {
        if (depth > 128 || ++elements > 100000) throw new InvalidDataException("map element bounds exceeded");
        string name = Lookup(table, reader.ReadInt16());
        Dictionary<string, object> attributes = new(StringComparer.Ordinal);
        int attributeCount = reader.ReadByte();
        for (int index = 0; index < attributeCount; index++)
        {
            string key = Lookup(table, reader.ReadInt16());
            byte type = reader.ReadByte();
            object value = type switch
            {
                0 => reader.ReadBoolean(),
                1 => reader.ReadByte(),
                2 => reader.ReadInt16(),
                3 => reader.ReadInt32(),
                4 => reader.ReadSingle(),
                5 => Lookup(table, reader.ReadInt16()),
                6 => reader.ReadString(),
                7 => reader.ReadBytes(CheckedRleLength(reader)),
                _ => throw new InvalidDataException("map value type is invalid")
            };
            attributes[key] = value;
        }
        string? childRoom = currentRoom;
        if (parent == "levels" && name == "level" && attributes.TryGetValue("name", out object? room) && room is string roomName)
        {
            // The pinned runtime's LevelData strips this optional legacy
            // prefix. Progression/session validation must use the same names.
            if (roomName.StartsWith("lvl_", StringComparison.Ordinal)) roomName = roomName[4..];
            rooms.Add(roomName);
            childRoom = roomName;
        }
        if (parent == "Map" && name == "meta") presentation.ApplyArea(attributes);
        else if (parent == "meta" && name == "mode") presentation.ApplyMode(attributes);
        else if (parent == "mode" && name == "audiostate") presentation.ApplyAudio(attributes);
        if (parent == "entities")
        {
            entities.Add(name);
            if (name == "checkpoint" && currentRoom != null)
            {
                int order = attributes.TryGetValue("checkpointID", out object? checkpointId)
                    ? Convert.ToInt32(checkpointId, System.Globalization.CultureInfo.InvariantCulture)
                    : int.MaxValue;
                checkpoints.Add((order, currentRoom));
            }
        }
        if (parent == "triggers")
        {
            triggers.Add(name);
            if (name == "changeRespawnTrigger" && attributes.TryGetValue("target", out object? checkpoint) && checkpoint is string checkpointName)
                checkpoints.Add((int.MaxValue, checkpointName));
        }
        int children = reader.ReadInt16();
        if (children < 0) throw new InvalidDataException("map child count is invalid");
        for (int index = 0; index < children; index++)
            ReadProgressionElement(reader, table, name, childRoom, depth + 1, ref elements, rooms, entities,
                triggers, checkpoints, presentation);

        static int CheckedRleLength(BinaryReader input)
        {
            int length = input.ReadInt16();
            if (length < 0) throw new InvalidDataException("map RLE value is invalid");
            return length;
        }
    }

    private sealed class MapPresentationBuilder
    {
        private MapPresentationRecord value = MapPresentationRecord.EverestDefault;

        internal void ApplySidecar(string path)
        {
            if (!File.Exists(path)) return;
            FileInfo info = new(path);
            if (info.Length <= 0 || info.Length > ProductPolicy.MaxYamlBytes)
                throw new InvalidDataException("map metadata sidecar size is invalid");

            YamlStream yaml = new();
            using (StreamReader reader = new(path, new UTF8Encoding(false, true))) yaml.Load(reader);
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                throw new InvalidDataException("map metadata sidecar root is invalid");

            Dictionary<string, object> attributes = new(StringComparer.Ordinal);
            foreach ((YamlNode keyNode, YamlNode valueNode) in root.Children)
            {
                if (keyNode is not YamlScalarNode { Value: { } key } || valueNode is not YamlScalarNode scalar)
                    continue;
                string raw = scalar.Value ?? "";
                attributes[key] = bool.TryParse(raw, out bool boolean) ? boolean
                    : float.TryParse(raw, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float number) ? number
                    : raw;
            }
            ApplyArea(attributes);
        }

        internal void ApplyArea(IReadOnlyDictionary<string, object> attributes)
        {
            value = value with
            {
                Icon = Text(attributes, "Icon", value.Icon),
                TitleBaseColor = ColorText(attributes, "TitleBaseColor", value.TitleBaseColor),
                TitleAccentColor = ColorText(attributes, "TitleAccentColor", value.TitleAccentColor),
                TitleTextColor = ColorText(attributes, "TitleTextColor", value.TitleTextColor),
                IntroType = Choice(attributes, "IntroType", value.IntroType,
                    "Transition", "Respawn", "WalkInRight", "WalkInLeft", "Jump", "WakeUp", "Fall",
                    "TempleMirrorVoid", "None", "ThinkForABit"),
                Dreaming = Boolean(attributes, "Dreaming", value.Dreaming),
                ColorGrade = Text(attributes, "ColorGrade", value.ColorGrade),
                Wipe = Wipe(attributes, value.Wipe),
                DarknessAlpha = Number(attributes, "DarknessAlpha", value.DarknessAlpha, 0f, 1f),
                BloomBase = Number(attributes, "BloomBase", value.BloomBase, 0f, 8f),
                BloomStrength = Number(attributes, "BloomStrength", value.BloomStrength, 0f, 8f),
                Jumpthru = Text(attributes, "Jumpthru", value.Jumpthru),
                CoreMode = Choice(attributes, "CoreMode", value.CoreMode, "None", "Hot", "Cold"),
                ForegroundTiles = Text(attributes, "ForegroundTiles", value.ForegroundTiles),
                BackgroundTiles = Text(attributes, "BackgroundTiles", value.BackgroundTiles),
                AnimatedTiles = Text(attributes, "AnimatedTiles", value.AnimatedTiles),
                Sprites = Text(attributes, "Sprites", value.Sprites),
                Name = Text(attributes, "Name", value.Name)
            };
        }

        internal void ApplyMode(IReadOnlyDictionary<string, object> attributes)
        {
            value = value with
            {
                Inventory = Choice(attributes, "Inventory", value.Inventory,
                    "Default", "CH6End", "Core", "OldSite", "Prologue", "TheSummit", "Farewell"),
                StartLevel = Text(attributes, "StartLevel", value.StartLevel),
                HeartIsEnd = Boolean(attributes, "HeartIsEnd", value.HeartIsEnd),
                IgnoreLevelAudioLayerData = Boolean(attributes, "IgnoreLevelAudioLayerData",
                    value.IgnoreLevelAudioLayerData)
            };
        }

        internal void ApplyAudio(IReadOnlyDictionary<string, object> attributes)
        {
            value = value with
            {
                Music = Text(attributes, "Music", value.Music),
                Ambience = Text(attributes, "Ambience", value.Ambience)
            };
        }

        internal MapPresentationRecord Build() => value;

        private static string Text(IReadOnlyDictionary<string, object> attributes, string key, string fallback)
        {
            if (!attributes.TryGetValue(key, out object? raw)) return fallback;
            string text = Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            if (text.Length > 1024) throw new InvalidDataException("map presentation text is too long: " + key);
            return text;
        }

        private static string ColorText(IReadOnlyDictionary<string, object> attributes, string key, string fallback)
        {
            string text = Text(attributes, key, fallback);
            if (text.Length != 6 || !text.All(Uri.IsHexDigit))
                throw new InvalidDataException("map presentation color is invalid: " + key);
            return text.ToLowerInvariant();
        }

        private static string Choice(IReadOnlyDictionary<string, object> attributes, string key, string fallback,
            params string[] choices)
        {
            string text = Text(attributes, key, fallback);
            string? selected = choices.FirstOrDefault(choice => choice.Equals(text, StringComparison.OrdinalIgnoreCase));
            return selected ?? throw new InvalidDataException("map presentation value is unsupported: " + key);
        }

        private static bool Boolean(IReadOnlyDictionary<string, object> attributes, string key, bool fallback) =>
            attributes.TryGetValue(key, out object? raw) && raw is bool value ? value : fallback;

        private static float Number(IReadOnlyDictionary<string, object> attributes, string key, float fallback,
            float minimum, float maximum)
        {
            if (!attributes.TryGetValue(key, out object? raw)) return fallback;
            float number = Convert.ToSingle(raw, System.Globalization.CultureInfo.InvariantCulture);
            if (!float.IsFinite(number) || number < minimum || number > maximum)
                throw new InvalidDataException("map presentation number is out of range: " + key);
            return number;
        }

        private static string Wipe(IReadOnlyDictionary<string, object> attributes, string fallback)
        {
            string text = Text(attributes, "Wipe", fallback);
            if (text.StartsWith("Celeste.", StringComparison.Ordinal) &&
                !text.EndsWith("Wipe", StringComparison.Ordinal)) text += "Wipe";
            string[] supported =
            [
                "Celeste.AngledWipe", "Celeste.CurtainWipe", "Celeste.DreamWipe", "Celeste.DropWipe",
                "Celeste.FadeWipe", "Celeste.FallWipe", "Celeste.HeartWipe", "Celeste.KeyDoorWipe",
                "Celeste.MountainWipe", "Celeste.SpotlightWipe", "Celeste.StarfieldWipe", "Celeste.WindWipe"
            ];
            return supported.Contains(text, StringComparer.Ordinal)
                ? text
                : throw new InvalidDataException("map presentation wipe is unsupported");
        }
    }

    private static void ReadDetailedElement(BinaryReader reader, string[] table, string? parent, string room,
        int depth, ref int elements, List<MapElementRecord> result, float roomX = 0f, float roomY = 0f)
    {
        if (depth > 128 || ++elements > 100000) throw new InvalidDataException("map element bounds exceeded");
        string name = Lookup(table, reader.ReadInt16());
        Dictionary<string, string> attributes = new(StringComparer.Ordinal);
        int count = reader.ReadByte();
        for (int index = 0; index < count; index++)
        {
            string key = Lookup(table, reader.ReadInt16());
            byte type = reader.ReadByte();
            object value = type switch
            {
                0 => reader.ReadBoolean(),
                1 => reader.ReadByte(),
                2 => reader.ReadInt16(),
                3 => reader.ReadInt32(),
                4 => reader.ReadSingle(),
                5 => Lookup(table, reader.ReadInt16()),
                6 => reader.ReadString(),
                7 => reader.ReadBytes(CheckedRleLength(reader)),
                _ => throw new InvalidDataException("map value type is invalid")
            };
            attributes[key] = value switch
            {
                float number => number.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                byte[] bytes => Convert.ToHexString(bytes),
                _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
            };
        }
        string currentRoom = parent == "levels" && name == "level" && attributes.TryGetValue("name", out string? roomName)
            ? roomName.StartsWith("lvl_", StringComparison.Ordinal) ? roomName[4..] : roomName
            : room;
        if (parent == "levels" && name == "level")
        {
            _ = TryFloat(attributes, "x", out roomX);
            _ = TryFloat(attributes, "y", out roomY);
        }
        int children = reader.ReadInt16();
        if (children < 0) throw new InvalidDataException("map child count is invalid");
        List<(float X, float Y)> nodes = [];
        for (int index = 0; index < children; index++)
        {
            long before = reader.BaseStream.Position;
            // Parse every child normally; node coordinates are also collected
            // as ordinary immutable map data and never interpreted on device.
            string childName = PeekElementName(reader, table);
            reader.BaseStream.Position = before;
            if (childName == "node")
            {
                Dictionary<string, string> nodeAttributes = ReadLeafElement(reader, table, depth + 1, ref elements);
                if (TryFloat(nodeAttributes, "x", out float x) && TryFloat(nodeAttributes, "y", out float y)) nodes.Add((x, y));
            }
            else
            {
                ReadDetailedElement(reader, table, name, currentRoom, depth + 1, ref elements, result, roomX, roomY);
            }
        }
        string? kind = parent == "entities" ? "entity" : parent == "triggers" ? "trigger" :
            parent != null && (parent.Equals("backgrounds", StringComparison.OrdinalIgnoreCase) ||
                parent.Equals("foregrounds", StringComparison.OrdinalIgnoreCase)) ? "backdrop" : null;
        if (kind != null)
        {
            _ = TryFloat(attributes, "x", out float x);
            _ = TryFloat(attributes, "y", out float y);
            _ = int.TryParse(attributes.GetValueOrDefault("width", "0"), out int width);
            _ = int.TryParse(attributes.GetValueOrDefault("height", "0"), out int height);
            result.Add(new(kind, name, room, x, y, width, height, attributes, nodes, roomX, roomY));
        }

        static int CheckedRleLength(BinaryReader input)
        {
            int length = input.ReadInt16();
            if (length < 0) throw new InvalidDataException("map RLE value is invalid");
            return length;
        }
    }

    private static string PeekElementName(BinaryReader reader, string[] table) => Lookup(table, reader.ReadInt16());

    private static Dictionary<string, string> ReadLeafElement(BinaryReader reader, string[] table, int depth, ref int elements)
    {
        if (depth > 128 || ++elements > 100000) throw new InvalidDataException("map element bounds exceeded");
        _ = Lookup(table, reader.ReadInt16());
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        int count = reader.ReadByte();
        for (int index = 0; index < count; index++)
        {
            string key = Lookup(table, reader.ReadInt16());
            byte type = reader.ReadByte();
            object value = type switch
            {
                0 => reader.ReadBoolean(), 1 => reader.ReadByte(), 2 => reader.ReadInt16(), 3 => reader.ReadInt32(),
                4 => reader.ReadSingle(), 5 => Lookup(table, reader.ReadInt16()), 6 => reader.ReadString(),
                7 => reader.ReadBytes(CheckedLength(reader)),
                _ => throw new InvalidDataException("map value type is invalid")
            };
            result[key] = value is float number
                ? number.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        }
        if (reader.ReadInt16() != 0) throw new InvalidDataException("map node unexpectedly contains children");
        return result;

        static int CheckedLength(BinaryReader input)
        {
            int length = input.ReadInt16();
            if (length < 0) throw new InvalidDataException("map RLE value is invalid");
            return length;
        }
    }

    private static bool TryFloat(IReadOnlyDictionary<string, string> values, string key, out float value) =>
        float.TryParse(values.GetValueOrDefault(key, "0"), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);

    private static string Lookup(string[] table, short index) => index >= 0 && index < table.Length
        ? table[index]
        : throw new InvalidDataException("map string table index is invalid");

    private static void WriteElement(BinaryWriter writer, XmlElement element, IReadOnlyDictionary<string, short> lookup)
    {
        XmlElement[] children = element.ChildNodes.OfType<XmlElement>().ToArray();
        string text = children.Length == 0 ? NormalizeText(element) : "";
        XmlAttribute[] attributes = ElementAttributes(element).ToArray();
        writer.Write(lookup[ElementName(element)]);
        writer.Write(checked((byte)(attributes.Length + (text.Length > 0 ? 1 : 0))));
        foreach (XmlAttribute attribute in attributes)
        {
            (byte type, object parsed) = AttributeValue(element, attribute);
            writer.Write(lookup[attribute.Name]); writer.Write(type); WriteValue(writer, type, parsed, lookup);
        }
        if (text.Length > 0)
        {
            writer.Write(lookup["innerText"]);
            if (element.Name is "solids" or "bg")
            {
                byte[] encoded = Rle(text); writer.Write((byte)7); writer.Write(checked((short)encoded.Length)); writer.Write(encoded);
            }
            else { writer.Write((byte)6); writer.Write(text); }
        }
        writer.Write(checked((short)children.Length));
        foreach (XmlElement child in children) WriteElement(writer, child, lookup);
    }

    private static bool FactoryWrapper(XmlElement element) => element.Name is
        "appleEverestEntity" or "appleEverestTrigger" or "appleEverestBackdrop";

    private static string FactoryIdAttribute(XmlElement element) => element.HasAttribute("appleEverestId") ? "appleEverestId" : "name";

    private static string ElementName(XmlElement element)
    {
        if (!FactoryWrapper(element)) return element.Name;
        string[] parents = element.Name switch
        {
            "appleEverestEntity" => ["entities"],
            "appleEverestTrigger" => ["triggers"],
            _ => ["Backgrounds", "Foregrounds"]
        };
        if (element.ParentNode is not XmlElement parent || !parents.Contains(parent.Name, StringComparer.Ordinal))
            throw new InvalidDataException($"{element.Name} is only valid directly below {string.Join("/", parents)}");
        string name = element.GetAttribute(FactoryIdAttribute(element));
        if (name.Length is < 1 or > 1024 || !name.Contains('/', StringComparison.Ordinal))
            throw new InvalidDataException($"{element.Name} requires a bounded namespaced name");
        string[] strings = element.GetAttribute("appleEverestStrings").Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (strings.Distinct(StringComparer.Ordinal).Count() != strings.Length || strings.Any(key =>
            !element.HasAttribute(key) || key == FactoryIdAttribute(element) || key == "appleEverestStrings"))
            throw new InvalidDataException("authored factory string-type metadata names an absent/duplicate/reserved attribute");
        return name;
    }

    private static IEnumerable<XmlAttribute> ElementAttributes(XmlElement element) =>
        element.Attributes.OfType<XmlAttribute>().Where(attribute => !FactoryWrapper(element) ||
            attribute.Name != FactoryIdAttribute(element) && attribute.Name != "appleEverestStrings");

    private static (byte Type, object Value) AttributeValue(XmlElement element, XmlAttribute attribute) =>
        FactoryWrapper(element) && element.GetAttribute("appleEverestStrings").Split(',').Contains(attribute.Name, StringComparer.Ordinal)
            ? (5, attribute.Value) : Value(attribute.Value);

    private static string NormalizeText(XmlElement element)
    {
        string value = element.InnerText;
        if (element.Name is "solids" or "bg" or "fgtiles" or "bgtiles" or "objtiles")
            return string.Join("\n", value.Replace("\r", "").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
        return value.Trim();
    }

    private static (byte Type, object Value) Value(string value)
    {
        if (bool.TryParse(value, out bool boolean)) return (0, boolean);
        if (byte.TryParse(value, out byte octet)) return (1, octet);
        if (short.TryParse(value, out short shortValue)) return (2, shortValue);
        if (int.TryParse(value, out int integer)) return (3, integer);
        if (float.TryParse(value, System.Globalization.NumberStyles.Integer | System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out float single)) return (4, single);
        return (5, value);
    }

    private static void WriteValue(BinaryWriter writer, byte type, object value, IReadOnlyDictionary<string, short> lookup)
    {
        switch (type)
        {
            case 0: writer.Write((bool)value); break;
            case 1: writer.Write((byte)value); break;
            case 2: writer.Write((short)value); break;
            case 3: writer.Write((int)value); break;
            case 4: writer.Write((float)value); break;
            case 5: writer.Write(lookup[(string)value]); break;
            default: throw new InvalidDataException("unknown map value type");
        }
    }

    private static byte[] Rle(string value)
    {
        List<byte> result = [];
        for (int index = 0; index < value.Length; index++)
        {
            byte count = 1; char current = value[index];
            while (index + 1 < value.Length && value[index + 1] == current && count < byte.MaxValue) { count++; index++; }
            result.Add(count); result.Add(checked((byte)current));
        }
        return result.ToArray();
    }

    private static byte[] Hex(string value)
    {
        if (value.Length != 6) throw new InvalidDataException("asset color must be six hexadecimal digits");
        return Convert.FromHexString(value);
    }

    private static byte[] UInt32(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static void WriteChunk(Stream output, string kind, byte[] data)
    {
        byte[] name = Encoding.ASCII.GetBytes(kind);
        output.Write(UInt32(data.Length)); output.Write(name); output.Write(data);
        output.Write(UInt32(unchecked((int)Crc32(name.Concat(data)))));
    }

    private static uint Crc32(IEnumerable<byte> bytes)
    {
        uint crc = 0xffffffff;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
    }
}
