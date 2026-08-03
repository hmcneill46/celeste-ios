#if TVOS_STAGE3C
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace Celeste;

// Reflection-free XML for the locked Celeste 1.4.0.0 SaveData graph. This is
// temporary Stage 3C storage plumbing; durability and profile separation are
// deliberately outside this class.
public static class TvOSSaveDataSerializer
{
    public static byte[] SerializeToBytes(SaveData value)
    {
        using MemoryStream stream = new();
        Serialize(stream, value);
        return stream.ToArray();
    }

    public static void Serialize(Stream stream, SaveData value)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(value);
        XmlWriterSettings settings = new()
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            NewLineChars = "\n",
            CloseOutput = false,
            OmitXmlDeclaration = false
        };
        using XmlWriter writer = XmlWriter.Create(stream, settings);
        writer.WriteStartDocument();
        writer.WriteStartElement("SaveData");
        writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");
        WriteNullable(writer, "Version", value.Version);
        WriteNullable(writer, "Name", value.Name);
        Write(writer, "Time", value.Time);
        Write(writer, "LastSave", XmlConvert.ToString(value.LastSave, XmlDateTimeSerializationMode.RoundtripKind));
        Write(writer, "CheatMode", value.CheatMode);
        Write(writer, "AssistMode", value.AssistMode);
        Write(writer, "VariantMode", value.VariantMode);
        WriteAssists(writer, value.Assists);
        WriteNullable(writer, "TheoSisterName", value.TheoSisterName);
        Write(writer, "UnlockedAreas", value.UnlockedAreas);
        Write(writer, "TotalDeaths", value.TotalDeaths);
        Write(writer, "TotalStrawberries", value.TotalStrawberries);
        Write(writer, "TotalGoldenStrawberries", value.TotalGoldenStrawberries);
        Write(writer, "TotalJumps", value.TotalJumps);
        Write(writer, "TotalWallJumps", value.TotalWallJumps);
        Write(writer, "TotalDashes", value.TotalDashes);
        WriteStrings(writer, "Flags", value.Flags);
        WriteStrings(writer, "Poem", value.Poem);
        WriteBools(writer, "SummitGems", value.SummitGems);
        Write(writer, "RevealedChapter9", value.RevealedChapter9);
        WriteAreaKey(writer, "LastArea", value.LastArea);
        if (value.CurrentSession != null)
        {
            WriteSession(writer, value.CurrentSession);
        }
        WriteAreas(writer, "Areas", value.Areas);
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    public static SaveData Deserialize(Stream stream)
    {
        XElement root = LoadRoot(stream, "SaveData");
        SaveData result = new();
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (XElement element in root.Elements())
        {
            string name = UniqueElement(element, seen, "SaveData");
            switch (name)
            {
                case "Version": result.Version = Scalar(element); break;
                case "Name": result.Name = Scalar(element); break;
                case "Time": result.Time = ReadLong(element); break;
                case "LastSave": result.LastSave = ReadDateTime(element); break;
                case "CheatMode": result.CheatMode = ReadBool(element); break;
                case "AssistMode": result.AssistMode = ReadBool(element); break;
                case "VariantMode": result.VariantMode = ReadBool(element); break;
                case "Assists": result.Assists = ReadAssists(element); break;
                case "TheoSisterName": result.TheoSisterName = Scalar(element); break;
                case "UnlockedAreas": result.UnlockedAreas = ReadInt(element); break;
                case "TotalDeaths": result.TotalDeaths = ReadInt(element); break;
                case "TotalStrawberries": result.TotalStrawberries = ReadInt(element); break;
                case "TotalGoldenStrawberries": result.TotalGoldenStrawberries = ReadInt(element); break;
                case "TotalJumps": result.TotalJumps = ReadInt(element); break;
                case "TotalWallJumps": result.TotalWallJumps = ReadInt(element); break;
                case "TotalDashes": result.TotalDashes = ReadInt(element); break;
                case "Flags": result.Flags = ReadStrings(element); break;
                case "Poem": result.Poem = ReadStringList(element); break;
                case "SummitGems": result.SummitGems = ReadBools(element); break;
                case "RevealedChapter9": result.RevealedChapter9 = ReadBool(element); break;
                case "LastArea": result.LastArea = ReadAreaKey(element); break;
                case "CurrentSession": result.CurrentSession = ReadSession(element); break;
                case "Areas": result.Areas = ReadAreas(element); break;
                default: throw Unsupported(name, "SaveData");
            }
        }
        return result;
    }

    private static void WriteAssists(XmlWriter writer, Assists value)
    {
        writer.WriteStartElement("Assists");
        Write(writer, "GameSpeed", value.GameSpeed);
        Write(writer, "Invincible", value.Invincible);
        Write(writer, "DashMode", value.DashMode);
        Write(writer, "DashAssist", value.DashAssist);
        Write(writer, "InfiniteStamina", value.InfiniteStamina);
        Write(writer, "MirrorMode", value.MirrorMode);
        Write(writer, "ThreeSixtyDashing", value.ThreeSixtyDashing);
        Write(writer, "InvisibleMotion", value.InvisibleMotion);
        Write(writer, "NoGrabbing", value.NoGrabbing);
        Write(writer, "LowFriction", value.LowFriction);
        Write(writer, "SuperDashing", value.SuperDashing);
        Write(writer, "Hiccups", value.Hiccups);
        Write(writer, "PlayAsBadeline", value.PlayAsBadeline);
        writer.WriteEndElement();
    }

    private static Assists ReadAssists(XElement element)
    {
        RejectAttributes(element, Array.Empty<string>());
        Assists result = default;
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (XElement child in element.Elements())
        {
            string name = UniqueElement(child, seen, "Assists");
            switch (name)
            {
                case "GameSpeed": result.GameSpeed = ReadInt(child); break;
                case "Invincible": result.Invincible = ReadBool(child); break;
                case "DashMode": result.DashMode = ReadEnum<Assists.DashModes>(child); break;
                case "DashAssist": result.DashAssist = ReadBool(child); break;
                case "InfiniteStamina": result.InfiniteStamina = ReadBool(child); break;
                case "MirrorMode": result.MirrorMode = ReadBool(child); break;
                case "ThreeSixtyDashing": result.ThreeSixtyDashing = ReadBool(child); break;
                case "InvisibleMotion": result.InvisibleMotion = ReadBool(child); break;
                case "NoGrabbing": result.NoGrabbing = ReadBool(child); break;
                case "LowFriction": result.LowFriction = ReadBool(child); break;
                case "SuperDashing": result.SuperDashing = ReadBool(child); break;
                case "Hiccups": result.Hiccups = ReadBool(child); break;
                case "PlayAsBadeline": result.PlayAsBadeline = ReadBool(child); break;
                default: throw Unsupported(name, "Assists");
            }
        }
        return result;
    }

    private static void WriteAreaKey(XmlWriter writer, string name, AreaKey value)
    {
        writer.WriteStartElement(name);
        WriteAttribute(writer, "ID", value.ID);
        WriteAttribute(writer, "Mode", value.Mode);
        writer.WriteEndElement();
    }

    private static AreaKey ReadAreaKey(XElement element)
    {
        RejectChildElements(element);
        RejectAttributes(element, new[] { "ID", "Mode" });
        return new AreaKey(RequiredIntAttribute(element, "ID"), RequiredEnumAttribute<AreaMode>(element, "Mode"));
    }

    private static void WriteSession(XmlWriter writer, Session value)
    {
        writer.WriteStartElement("CurrentSession");
        WriteNullableAttribute(writer, "Level", value.Level);
        WriteAttribute(writer, "Time", value.Time);
        WriteAttribute(writer, "StartedFromBeginning", value.StartedFromBeginning);
        WriteAttribute(writer, "Deaths", value.Deaths);
        WriteAttribute(writer, "Dashes", value.Dashes);
        WriteAttribute(writer, "DashesAtLevelStart", value.DashesAtLevelStart);
        WriteAttribute(writer, "DeathsInCurrentLevel", value.DeathsInCurrentLevel);
        WriteAttribute(writer, "InArea", value.InArea);
        WriteNullableAttribute(writer, "StartCheckpoint", value.StartCheckpoint);
        WriteAttribute(writer, "FirstLevel", value.FirstLevel);
        WriteAttribute(writer, "Cassette", value.Cassette);
        WriteAttribute(writer, "HeartGem", value.HeartGem);
        WriteAttribute(writer, "Dreaming", value.Dreaming);
        WriteNullableAttribute(writer, "ColorGrade", value.ColorGrade);
        WriteAttribute(writer, "LightingAlphaAdd", value.LightingAlphaAdd);
        WriteAttribute(writer, "BloomBaseAdd", value.BloomBaseAdd);
        WriteAttribute(writer, "DarkRoomAlpha", value.DarkRoomAlpha);
        WriteAttribute(writer, "CoreMode", value.CoreMode);
        WriteAttribute(writer, "GrabbedGolden", value.GrabbedGolden);
        WriteAttribute(writer, "HitCheckpoint", value.HitCheckpoint);
        WriteAreaKey(writer, "Area", value.Area);
        if (value.RespawnPoint.HasValue)
        {
            writer.WriteStartElement("RespawnPoint");
            Write(writer, "X", value.RespawnPoint.Value.X);
            Write(writer, "Y", value.RespawnPoint.Value.Y);
            writer.WriteEndElement();
        }
        if (value.Audio != null) WriteAudioState(writer, value.Audio);
        WriteInventory(writer, value.Inventory);
        WriteStrings(writer, "Flags", value.Flags);
        WriteStrings(writer, "LevelFlags", value.LevelFlags);
        WriteEntityIds(writer, "Strawberries", value.Strawberries);
        WriteEntityIds(writer, "DoNotLoad", value.DoNotLoad);
        WriteEntityIds(writer, "Keys", value.Keys);
        WriteCounters(writer, value.Counters);
        WriteBools(writer, "SummitGems", value.SummitGems);
        if (value.OldStats != null) WriteAreaStats(writer, "OldStats", value.OldStats);
        Write(writer, "UnlockedCSide", value.UnlockedCSide);
        WriteNullable(writer, "FurthestSeenLevel", value.FurthestSeenLevel);
        Write(writer, "BeatBestTime", value.BeatBestTime);
        writer.WriteEndElement();
    }

    private static Session ReadSession(XElement element)
    {
        string[] allowed = { "Level", "Time", "StartedFromBeginning", "Deaths", "Dashes", "DashesAtLevelStart",
            "DeathsInCurrentLevel", "InArea", "StartCheckpoint", "FirstLevel", "Cassette", "HeartGem", "Dreaming",
            "ColorGrade", "LightingAlphaAdd", "BloomBaseAdd", "DarkRoomAlpha", "CoreMode", "GrabbedGolden", "HitCheckpoint" };
        RejectAttributes(element, allowed);
        Session result = new()
        {
            Level = OptionalAttribute(element, "Level"),
            Time = OptionalLongAttribute(element, "Time"),
            StartedFromBeginning = OptionalBoolAttribute(element, "StartedFromBeginning"),
            Deaths = OptionalIntAttribute(element, "Deaths"),
            Dashes = OptionalIntAttribute(element, "Dashes"),
            DashesAtLevelStart = OptionalIntAttribute(element, "DashesAtLevelStart"),
            DeathsInCurrentLevel = OptionalIntAttribute(element, "DeathsInCurrentLevel"),
            InArea = OptionalBoolAttribute(element, "InArea"),
            StartCheckpoint = OptionalAttribute(element, "StartCheckpoint"),
            FirstLevel = OptionalBoolAttribute(element, "FirstLevel", true),
            Cassette = OptionalBoolAttribute(element, "Cassette"),
            HeartGem = OptionalBoolAttribute(element, "HeartGem"),
            Dreaming = OptionalBoolAttribute(element, "Dreaming"),
            ColorGrade = OptionalAttribute(element, "ColorGrade"),
            LightingAlphaAdd = OptionalFloatAttribute(element, "LightingAlphaAdd"),
            BloomBaseAdd = OptionalFloatAttribute(element, "BloomBaseAdd"),
            DarkRoomAlpha = OptionalFloatAttribute(element, "DarkRoomAlpha", 0.75f),
            CoreMode = OptionalEnumAttribute(element, "CoreMode", Session.CoreModes.None),
            GrabbedGolden = OptionalBoolAttribute(element, "GrabbedGolden"),
            HitCheckpoint = OptionalBoolAttribute(element, "HitCheckpoint")
        };
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (XElement child in element.Elements())
        {
            string name = UniqueElement(child, seen, "CurrentSession");
            switch (name)
            {
                case "Area": result.Area = ReadAreaKey(child); break;
                case "RespawnPoint": result.RespawnPoint = ReadVector2(child); break;
                case "Audio": result.Audio = ReadAudioState(child); break;
                case "Inventory": result.Inventory = ReadInventory(child); break;
                case "Flags": result.Flags = ReadStrings(child); break;
                case "LevelFlags": result.LevelFlags = ReadStrings(child); break;
                case "Strawberries": result.Strawberries = ReadEntityIds(child); break;
                case "DoNotLoad": result.DoNotLoad = ReadEntityIds(child); break;
                case "Keys": result.Keys = ReadEntityIds(child); break;
                case "Counters": result.Counters = ReadCounters(child); break;
                case "SummitGems": result.SummitGems = ReadBools(child); break;
                case "OldStats": result.OldStats = ReadAreaStats(child); break;
                case "UnlockedCSide": result.UnlockedCSide = ReadBool(child); break;
                case "FurthestSeenLevel": result.FurthestSeenLevel = Scalar(child); break;
                case "BeatBestTime": result.BeatBestTime = ReadBool(child); break;
                default: throw Unsupported(name, "CurrentSession");
            }
        }
        return result;
    }

    private static void WriteAudioState(XmlWriter writer, AudioState value)
    {
        writer.WriteStartElement("Audio");
        if (value.Music != null) WriteAudioTrack(writer, "Music", value.Music);
        if (value.Ambience != null) WriteAudioTrack(writer, "Ambience", value.Ambience);
        writer.WriteEndElement();
    }

    private static AudioState ReadAudioState(XElement element)
    {
        RejectAttributes(element, Array.Empty<string>());
        AudioState result = new();
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (XElement child in element.Elements())
        {
            string name = UniqueElement(child, seen, "Audio");
            if (name == "Music") result.Music = ReadAudioTrack(child);
            else if (name == "Ambience") result.Ambience = ReadAudioTrack(child);
            else throw Unsupported(name, "Audio");
        }
        return result;
    }

    private static void WriteAudioTrack(XmlWriter writer, string name, AudioTrackState value)
    {
        writer.WriteStartElement(name);
        WriteNullableAttribute(writer, "Event", value.Event);
        if (value.Parameters != null)
        {
            writer.WriteStartElement("Parameters");
            foreach (MEP parameter in value.Parameters)
            {
                writer.WriteStartElement("MEP");
                WriteNullableAttribute(writer, "Key", parameter.Key);
                WriteAttribute(writer, "Value", parameter.Value);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static AudioTrackState ReadAudioTrack(XElement element)
    {
        RejectAttributes(element, new[] { "Event" });
        AudioTrackState result = new() { Event = OptionalAttribute(element, "Event") };
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (XElement child in element.Elements())
        {
            string name = UniqueElement(child, seen, element.Name.LocalName);
            if (name != "Parameters") throw Unsupported(name, element.Name.LocalName);
            result.Parameters = new List<MEP>();
            foreach (XElement parameter in child.Elements())
            {
                RequireName(parameter, "MEP");
                RejectAttributes(parameter, new[] { "Key", "Value" });
                RejectChildElements(parameter);
                result.Parameters.Add(new MEP(OptionalAttribute(parameter, "Key"), RequiredFloatAttribute(parameter, "Value")));
            }
        }
        return result;
    }

    private static void WriteInventory(XmlWriter writer, PlayerInventory value)
    {
        writer.WriteStartElement("Inventory");
        Write(writer, "Dashes", value.Dashes);
        Write(writer, "DreamDash", value.DreamDash);
        Write(writer, "Backpack", value.Backpack);
        Write(writer, "NoRefills", value.NoRefills);
        writer.WriteEndElement();
    }

    private static PlayerInventory ReadInventory(XElement element)
    {
        RejectAttributes(element, Array.Empty<string>());
        PlayerInventory result = default;
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (XElement child in element.Elements())
        {
            string name = UniqueElement(child, seen, "Inventory");
            switch (name)
            {
                case "Dashes": result.Dashes = ReadInt(child); break;
                case "DreamDash": result.DreamDash = ReadBool(child); break;
                case "Backpack": result.Backpack = ReadBool(child); break;
                case "NoRefills": result.NoRefills = ReadBool(child); break;
                default: throw Unsupported(name, "Inventory");
            }
        }
        return result;
    }

    private static void WriteAreas(XmlWriter writer, string name, IEnumerable<AreaStats> values)
    {
        if (values == null) return;
        writer.WriteStartElement(name);
        foreach (AreaStats value in values) WriteAreaStats(writer, "AreaStats", value);
        writer.WriteEndElement();
    }

    private static List<AreaStats> ReadAreas(XElement element)
    {
        RejectAttributes(element, Array.Empty<string>());
        List<AreaStats> result = new();
        foreach (XElement child in element.Elements())
        {
            RequireName(child, "AreaStats");
            result.Add(ReadAreaStats(child));
        }
        return result;
    }

    private static void WriteAreaStats(XmlWriter writer, string name, AreaStats value)
    {
        writer.WriteStartElement(name);
        WriteAttribute(writer, "ID", value.ID);
        WriteAttribute(writer, "Cassette", value.Cassette);
        if (value.Modes != null)
        {
            writer.WriteStartElement("Modes");
            foreach (AreaModeStats mode in value.Modes) WriteAreaModeStats(writer, mode);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static AreaStats ReadAreaStats(XElement element)
    {
        RejectAttributes(element, new[] { "ID", "Cassette" });
        AreaStats result = new(RequiredIntAttribute(element, "ID"))
        {
            Cassette = OptionalBoolAttribute(element, "Cassette")
        };
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (XElement child in element.Elements())
        {
            string name = UniqueElement(child, seen, element.Name.LocalName);
            if (name != "Modes") throw Unsupported(name, element.Name.LocalName);
            RejectAttributes(child, Array.Empty<string>());
            List<AreaModeStats> modes = new();
            foreach (XElement mode in child.Elements())
            {
                RequireName(mode, "AreaModeStats");
                modes.Add(ReadAreaModeStats(mode));
            }
            result.Modes = modes.ToArray();
        }
        return result;
    }

    private static void WriteAreaModeStats(XmlWriter writer, AreaModeStats value)
    {
        writer.WriteStartElement("AreaModeStats");
        WriteAttribute(writer, "TotalStrawberries", value.TotalStrawberries);
        WriteAttribute(writer, "Completed", value.Completed);
        WriteAttribute(writer, "SingleRunCompleted", value.SingleRunCompleted);
        WriteAttribute(writer, "FullClear", value.FullClear);
        WriteAttribute(writer, "Deaths", value.Deaths);
        WriteAttribute(writer, "TimePlayed", value.TimePlayed);
        WriteAttribute(writer, "BestTime", value.BestTime);
        WriteAttribute(writer, "BestFullClearTime", value.BestFullClearTime);
        WriteAttribute(writer, "BestDashes", value.BestDashes);
        WriteAttribute(writer, "BestDeaths", value.BestDeaths);
        WriteAttribute(writer, "HeartGem", value.HeartGem);
        WriteEntityIds(writer, "Strawberries", value.Strawberries);
        WriteStrings(writer, "Checkpoints", value.Checkpoints);
        writer.WriteEndElement();
    }

    private static AreaModeStats ReadAreaModeStats(XElement element)
    {
        string[] allowed = { "TotalStrawberries", "Completed", "SingleRunCompleted", "FullClear", "Deaths",
            "TimePlayed", "BestTime", "BestFullClearTime", "BestDashes", "BestDeaths", "HeartGem" };
        RejectAttributes(element, allowed);
        AreaModeStats result = new()
        {
            TotalStrawberries = OptionalIntAttribute(element, "TotalStrawberries"),
            Completed = OptionalBoolAttribute(element, "Completed"),
            SingleRunCompleted = OptionalBoolAttribute(element, "SingleRunCompleted"),
            FullClear = OptionalBoolAttribute(element, "FullClear"),
            Deaths = OptionalIntAttribute(element, "Deaths"),
            TimePlayed = OptionalLongAttribute(element, "TimePlayed"),
            BestTime = OptionalLongAttribute(element, "BestTime"),
            BestFullClearTime = OptionalLongAttribute(element, "BestFullClearTime"),
            BestDashes = OptionalIntAttribute(element, "BestDashes"),
            BestDeaths = OptionalIntAttribute(element, "BestDeaths"),
            HeartGem = OptionalBoolAttribute(element, "HeartGem")
        };
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (XElement child in element.Elements())
        {
            string name = UniqueElement(child, seen, "AreaModeStats");
            if (name == "Strawberries") result.Strawberries = ReadEntityIds(child);
            else if (name == "Checkpoints") result.Checkpoints = ReadStrings(child);
            else throw Unsupported(name, "AreaModeStats");
        }
        return result;
    }

    private static void WriteEntityIds(XmlWriter writer, string name, IEnumerable<EntityID> values)
    {
        if (values == null) return;
        writer.WriteStartElement(name);
        foreach (EntityID value in values.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            writer.WriteStartElement("EntityID");
            WriteNullableAttribute(writer, "Key", value.Key);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static HashSet<EntityID> ReadEntityIds(XElement element)
    {
        RejectAttributes(element, Array.Empty<string>());
        HashSet<EntityID> result = new();
        foreach (XElement child in element.Elements())
        {
            RequireName(child, "EntityID");
            RejectAttributes(child, new[] { "Key" });
            RejectChildElements(child);
            string key = RequiredAttribute(child, "Key");
            int separator = key.LastIndexOf(':');
            if (separator <= 0 || separator == key.Length - 1 ||
                !int.TryParse(key[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            {
                throw new InvalidDataException($"Invalid EntityID key '{key}'.");
            }
            if (!result.Add(new EntityID(key[..separator], id)))
            {
                throw new InvalidDataException($"Duplicate EntityID key '{key}'.");
            }
        }
        return result;
    }

    private static void WriteCounters(XmlWriter writer, IEnumerable<Session.Counter> values)
    {
        if (values == null) return;
        writer.WriteStartElement("Counters");
        foreach (Session.Counter value in values)
        {
            writer.WriteStartElement("Counter");
            WriteNullableAttribute(writer, "key", value.Key);
            WriteAttribute(writer, "value", value.Value);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static List<Session.Counter> ReadCounters(XElement element)
    {
        RejectAttributes(element, Array.Empty<string>());
        List<Session.Counter> result = new();
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (XElement child in element.Elements())
        {
            RequireName(child, "Counter");
            RejectAttributes(child, new[] { "key", "value" });
            RejectChildElements(child);
            string key = RequiredAttribute(child, "key");
            if (!keys.Add(key)) throw new InvalidDataException($"Duplicate Session counter '{key}'.");
            result.Add(new Session.Counter { Key = key, Value = RequiredIntAttribute(child, "value") });
        }
        return result;
    }

    private static void WriteStrings(XmlWriter writer, string name, IEnumerable<string> values)
    {
        if (values == null) return;
        writer.WriteStartElement(name);
        IEnumerable<string> ordered = values is HashSet<string> ? values.OrderBy(value => value, StringComparer.Ordinal) : values;
        foreach (string value in ordered) Write(writer, "string", value ?? "");
        writer.WriteEndElement();
    }

    private static HashSet<string> ReadStrings(XElement element)
    {
        RejectAttributes(element, Array.Empty<string>());
        HashSet<string> result = new(StringComparer.Ordinal);
        foreach (XElement child in element.Elements())
        {
            RequireName(child, "string");
            string value = Scalar(child);
            if (!result.Add(value)) throw new InvalidDataException($"Duplicate string '{value}' in '{element.Name.LocalName}'.");
        }
        return result;
    }

    private static List<string> ReadStringList(XElement element)
    {
        RejectAttributes(element, Array.Empty<string>());
        List<string> result = new();
        foreach (XElement child in element.Elements())
        {
            RequireName(child, "string");
            result.Add(Scalar(child));
        }
        return result;
    }

    private static void WriteBools(XmlWriter writer, string name, IEnumerable<bool> values)
    {
        if (values == null) return;
        writer.WriteStartElement(name);
        foreach (bool value in values) Write(writer, "boolean", value);
        writer.WriteEndElement();
    }

    private static bool[] ReadBools(XElement element)
    {
        RejectAttributes(element, Array.Empty<string>());
        List<bool> result = new();
        foreach (XElement child in element.Elements())
        {
            RequireName(child, "boolean");
            result.Add(ReadBool(child));
        }
        return result.ToArray();
    }

    private static Vector2 ReadVector2(XElement element)
    {
        RejectAttributes(element, Array.Empty<string>());
        float x = 0f;
        float y = 0f;
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (XElement child in element.Elements())
        {
            string name = UniqueElement(child, seen, "RespawnPoint");
            if (name == "X") x = ReadFloat(child);
            else if (name == "Y") y = ReadFloat(child);
            else throw Unsupported(name, "RespawnPoint");
        }
        return new Vector2(x, y);
    }

    private static XElement LoadRoot(Stream stream, string expected)
    {
        ArgumentNullException.ThrowIfNull(stream);
        XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Prohibit, CloseInput = false,
            IgnoreComments = true, IgnoreProcessingInstructions = true };
        try
        {
            using XmlReader reader = XmlReader.Create(stream, settings);
            XDocument document = XDocument.Load(reader, LoadOptions.None);
            XElement root = document.Root ?? throw new InvalidDataException("Celeste SaveData XML has no root element.");
            RequireName(root, expected);
            RejectAttributes(root, Array.Empty<string>());
            return root;
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("Malformed Celeste SaveData XML.", exception);
        }
    }

    private static string UniqueElement(XElement element, HashSet<string> seen, string context)
    {
        if (element.Name.NamespaceName.Length != 0 || !seen.Add(element.Name.LocalName))
            throw new InvalidDataException($"Unexpected or duplicate element '{element.Name}' in '{context}'.");
        return element.Name.LocalName;
    }

    private static void RequireName(XElement element, string name)
    {
        if (element.Name.LocalName != name || element.Name.NamespaceName.Length != 0)
            throw new InvalidDataException($"Expected element '{name}', found '{element.Name}'.");
    }

    private static void RejectAttributes(XElement element, IEnumerable<string> allowed)
    {
        HashSet<string> allowedSet = new(allowed, StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (XAttribute attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration) continue;
            if (attribute.Name.NamespaceName.Length != 0 || !allowedSet.Contains(attribute.Name.LocalName) || !seen.Add(attribute.Name.LocalName))
                throw new InvalidDataException($"Unsupported or duplicate attribute '{attribute.Name}' on '{element.Name.LocalName}'.");
        }
    }

    private static void RejectChildElements(XElement element)
    {
        if (element.HasElements) throw new InvalidDataException($"Element '{element.Name.LocalName}' must not contain child elements.");
    }

    private static string Scalar(XElement element)
    {
        RejectAttributes(element, Array.Empty<string>());
        RejectChildElements(element);
        return element.Value;
    }

    private static InvalidDataException Unsupported(string name, string context) =>
        new($"Unsupported Celeste SaveData element '{name}' in '{context}'.");

    private static bool ReadBool(XElement element) { try { return XmlConvert.ToBoolean(Scalar(element)); } catch (FormatException ex) { throw InvalidValue(element, ex); } }
    private static int ReadInt(XElement element) { try { return XmlConvert.ToInt32(Scalar(element)); } catch (FormatException ex) { throw InvalidValue(element, ex); } }
    private static long ReadLong(XElement element) { try { return XmlConvert.ToInt64(Scalar(element)); } catch (FormatException ex) { throw InvalidValue(element, ex); } }
    private static float ReadFloat(XElement element) { try { return XmlConvert.ToSingle(Scalar(element)); } catch (FormatException ex) { throw InvalidValue(element, ex); } }
    private static DateTime ReadDateTime(XElement element) { try { return XmlConvert.ToDateTime(Scalar(element), XmlDateTimeSerializationMode.RoundtripKind); } catch (FormatException ex) { throw InvalidValue(element, ex); } }
    private static T ReadEnum<T>(XElement element) where T : struct, Enum
    {
        string text = Scalar(element);
        if (Enum.TryParse(text, false, out T value) && Enum.IsDefined(value)) return value;
        throw new InvalidDataException($"Invalid {typeof(T).Name} value '{text}'.");
    }
    private static InvalidDataException InvalidValue(XElement element, Exception inner) => new($"Invalid value '{element.Value}' in '{element.Name.LocalName}'.", inner);

    private static string OptionalAttribute(XElement element, string name) => element.Attribute(name)?.Value;
    private static string RequiredAttribute(XElement element, string name) => OptionalAttribute(element, name) ?? throw new InvalidDataException($"Missing attribute '{name}' on '{element.Name.LocalName}'.");
    private static int RequiredIntAttribute(XElement e, string n) => ParseIntAttribute(e, n, true);
    private static float RequiredFloatAttribute(XElement e, string n) => ParseFloatAttribute(e, n, true);
    private static int OptionalIntAttribute(XElement e, string n, int d = 0) => e.Attribute(n) == null ? d : ParseIntAttribute(e, n, true);
    private static long OptionalLongAttribute(XElement e, string n, long d = 0) => e.Attribute(n) == null ? d : ParseLongAttribute(e, n);
    private static float OptionalFloatAttribute(XElement e, string n, float d = 0f) => e.Attribute(n) == null ? d : ParseFloatAttribute(e, n, true);
    private static bool OptionalBoolAttribute(XElement e, string n, bool d = false) => e.Attribute(n) == null ? d : ParseBoolAttribute(e, n);
    private static T RequiredEnumAttribute<T>(XElement e, string n) where T : struct, Enum => ParseEnumAttribute<T>(e, n);
    private static T OptionalEnumAttribute<T>(XElement e, string n, T d) where T : struct, Enum => e.Attribute(n) == null ? d : ParseEnumAttribute<T>(e, n);
    private static int ParseIntAttribute(XElement e, string n, bool required) { try { return XmlConvert.ToInt32(required ? RequiredAttribute(e, n) : OptionalAttribute(e, n)); } catch (FormatException ex) { throw new InvalidDataException($"Invalid integer attribute '{n}'.", ex); } }
    private static long ParseLongAttribute(XElement e, string n) { try { return XmlConvert.ToInt64(RequiredAttribute(e, n)); } catch (FormatException ex) { throw new InvalidDataException($"Invalid long attribute '{n}'.", ex); } }
    private static float ParseFloatAttribute(XElement e, string n, bool required) { try { return XmlConvert.ToSingle(required ? RequiredAttribute(e, n) : OptionalAttribute(e, n)); } catch (FormatException ex) { throw new InvalidDataException($"Invalid float attribute '{n}'.", ex); } }
    private static bool ParseBoolAttribute(XElement e, string n) { try { return XmlConvert.ToBoolean(RequiredAttribute(e, n)); } catch (FormatException ex) { throw new InvalidDataException($"Invalid boolean attribute '{n}'.", ex); } }
    private static T ParseEnumAttribute<T>(XElement e, string n) where T : struct, Enum { string text = RequiredAttribute(e, n); if (Enum.TryParse(text, false, out T value) && Enum.IsDefined(value)) return value; throw new InvalidDataException($"Invalid {typeof(T).Name} attribute '{n}' value '{text}'."); }

    private static void WriteNullable(XmlWriter writer, string name, string value) { if (value != null) Write(writer, name, value); }
    private static void WriteNullableAttribute(XmlWriter writer, string name, string value) { if (value != null) writer.WriteAttributeString(name, value); }
    private static void Write(XmlWriter writer, string name, string value) => writer.WriteElementString(name, value ?? "");
    private static void Write(XmlWriter writer, string name, bool value) => Write(writer, name, XmlConvert.ToString(value));
    private static void Write(XmlWriter writer, string name, int value) => Write(writer, name, XmlConvert.ToString(value));
    private static void Write(XmlWriter writer, string name, long value) => Write(writer, name, XmlConvert.ToString(value));
    private static void Write(XmlWriter writer, string name, float value) => Write(writer, name, XmlConvert.ToString(value));
    private static void Write<T>(XmlWriter writer, string name, T value) where T : struct, Enum => Write(writer, name, value.ToString());
    private static void WriteAttribute(XmlWriter writer, string name, bool value) => writer.WriteAttributeString(name, XmlConvert.ToString(value));
    private static void WriteAttribute(XmlWriter writer, string name, int value) => writer.WriteAttributeString(name, XmlConvert.ToString(value));
    private static void WriteAttribute(XmlWriter writer, string name, long value) => writer.WriteAttributeString(name, XmlConvert.ToString(value));
    private static void WriteAttribute(XmlWriter writer, string name, float value) => writer.WriteAttributeString(name, XmlConvert.ToString(value));
    private static void WriteAttribute<T>(XmlWriter writer, string name, T value) where T : struct, Enum => writer.WriteAttributeString(name, value.ToString());
}
#endif
