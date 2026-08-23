#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Celeste.Mod;

internal sealed record AppleEverestProgressionEntityId(string Level, int Id);

internal sealed record AppleEverestProgressionMode(
    int TotalStrawberries, bool Completed, bool SingleRunCompleted, bool FullClear,
    int Deaths, long TimePlayed, long BestTime, long BestFullClearTime,
    int BestDashes, int BestDeaths, bool HeartGem,
    AppleEverestProgressionEntityId[] Strawberries, string[] Checkpoints);

internal sealed record AppleEverestProgressionArea(
    string Sid, string LevelSet, string CompatibilityId, bool Cassette,
    AppleEverestProgressionMode[] Modes);

internal sealed record AppleEverestProgressionSession(
    string Sid, string CompatibilityId, int Mode, string Level,
    bool HasRespawnPoint, float RespawnX, float RespawnY,
    string StartCheckpoint, long Time, bool StartedFromBeginning,
    int Deaths, int Dashes, int DashesAtLevelStart, int DeathsInCurrentLevel,
    bool InArea, bool FirstLevel, bool Cassette, bool HeartGem, bool Dreaming,
    string ColorGrade, float LightingAlphaAdd, float BloomBaseAdd, float DarkRoomAlpha,
    int CoreMode, bool GrabbedGolden, bool HitCheckpoint,
    int InventoryDashes, bool InventoryDreamDash, bool InventoryBackpack, bool InventoryNoRefills,
    string MusicEvent, AppleEverestProgressionParameter[] MusicParameters,
    string AmbienceEvent, AppleEverestProgressionParameter[] AmbienceParameters,
    string[] Flags, string[] LevelFlags,
    AppleEverestProgressionEntityId[] Strawberries,
    AppleEverestProgressionEntityId[] DoNotLoad,
    AppleEverestProgressionEntityId[] Keys,
    AppleEverestProgressionCounter[] Counters,
    bool[] SummitGems, bool UnlockedCSide, string FurthestSeenLevel, bool BeatBestTime,
    bool OldStatsCassette, AppleEverestProgressionMode[] OldStatsModes);

internal sealed record AppleEverestProgressionParameter(string Key, float Value);
internal sealed record AppleEverestProgressionCounter(string Key, int Value);

internal sealed record AppleEverestProgressionSnapshot(
    int Slot, long Generation, byte[] BaseSaveSha256, byte[] Lineage,
    AppleEverestProgressionArea[] Areas, AppleEverestProgressionSession Session);

internal static class AppleEverestProgressionSnapshotCodec
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("AEVPSV1\0");
    // Keep the shared logical schema within the stricter tvOS adapter budget so
    // one closure cannot produce progression that only iOS can subsequently load.
    internal const int MaximumBytes = 1024 * 1024;
    internal const int MaximumAreas = 128;
    internal const int MaximumModes = 3;
    internal const int MaximumItems = 16384;
    internal const int MaximumStringBytes = 4096;
    internal const int HashBytes = 32;

    internal static byte[] Encode(AppleEverestProgressionSnapshot value)
    {
        Validate(value);
        using MemoryStream body = new();
        using (BinaryWriter writer = new(body, new UTF8Encoding(false, true), leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(1);
            writer.Write(value.Slot);
            writer.Write(value.Generation);
            writer.Write(value.BaseSaveSha256);
            writer.Write(value.Lineage);
            writer.Write(value.Areas.Length);
            foreach (AppleEverestProgressionArea area in value.Areas.OrderBy(item => item.Sid, StringComparer.Ordinal))
                WriteArea(writer, area);
            writer.Write(value.Session != null);
            if (value.Session != null) WriteSession(writer, value.Session);
        }
        byte[] unsigned = body.ToArray();
        byte[] digest = SHA256.HashData(unsigned);
        if (unsigned.Length + digest.Length > MaximumBytes)
            throw new InvalidDataException("Apple Everest progression exceeds its bounded storage budget");
        return unsigned.Concat(digest).ToArray();
    }

    internal static bool TryDecode(byte[] data, int slot, out AppleEverestProgressionSnapshot value)
    {
        value = null;
        if (slot is < 0 or > 2 || data == null || data.Length < Magic.Length + 4 + 4 + 8 + 64 + 4 + 1 + HashBytes ||
            data.Length > MaximumBytes) return false;
        int bodyLength = data.Length - HashBytes;
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(data.AsSpan(0, bodyLength)),
                data.AsSpan(bodyLength, HashBytes))) return false;
        try
        {
            using MemoryStream stream = new(data, 0, bodyLength, writable: false);
            using BinaryReader reader = new(stream, new UTF8Encoding(false, true), leaveOpen: false);
            if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic) || reader.ReadInt32() != 1 || reader.ReadInt32() != slot)
                return false;
            long generation = reader.ReadInt64();
            byte[] baseHash = reader.ReadBytes(HashBytes);
            byte[] lineage = reader.ReadBytes(HashBytes);
            int count = reader.ReadInt32();
            if (generation < 1 || baseHash.Length != HashBytes || lineage.Length != HashBytes || count is < 0 or > MaximumAreas)
                return false;
            AppleEverestProgressionArea[] areas = new AppleEverestProgressionArea[count];
            HashSet<string> identities = new(StringComparer.Ordinal);
            for (int index = 0; index < count; index++)
            {
                areas[index] = ReadArea(reader);
                if (!identities.Add(areas[index].Sid)) return false;
            }
            AppleEverestProgressionSession session = reader.ReadBoolean() ? ReadSession(reader) : null;
            if (stream.Position != stream.Length) return false;
            value = new(slot, generation, baseHash, lineage, areas, session);
            Validate(value);
            return true;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or DecoderFallbackException or OverflowException)
        {
            return false;
        }
    }

    internal static byte[] BaseHash(byte[] value) => value == null ? null : SHA256.HashData(value);

    private static void WriteArea(BinaryWriter writer, AppleEverestProgressionArea value)
    {
        WriteText(writer, value.Sid); WriteText(writer, value.LevelSet); WriteText(writer, value.CompatibilityId);
        writer.Write(value.Cassette); writer.Write(value.Modes.Length);
        foreach (AppleEverestProgressionMode mode in value.Modes) WriteMode(writer, mode);
    }

    private static AppleEverestProgressionArea ReadArea(BinaryReader reader)
    {
        string sid = ReadText(reader); string levelSet = ReadText(reader); string identity = ReadText(reader);
        bool cassette = reader.ReadBoolean(); int modes = reader.ReadInt32();
        if (modes is < 1 or > MaximumModes) throw new InvalidDataException("invalid progression mode count");
        AppleEverestProgressionMode[] values = new AppleEverestProgressionMode[modes];
        for (int index = 0; index < modes; index++) values[index] = ReadMode(reader);
        return new(sid, levelSet, identity, cassette, values);
    }

    private static void WriteMode(BinaryWriter writer, AppleEverestProgressionMode value)
    {
        writer.Write(value.TotalStrawberries); writer.Write(value.Completed); writer.Write(value.SingleRunCompleted);
        writer.Write(value.FullClear); writer.Write(value.Deaths); writer.Write(value.TimePlayed); writer.Write(value.BestTime);
        writer.Write(value.BestFullClearTime); writer.Write(value.BestDashes); writer.Write(value.BestDeaths); writer.Write(value.HeartGem);
        WriteEntityIds(writer, value.Strawberries); WriteStrings(writer, value.Checkpoints);
    }

    private static AppleEverestProgressionMode ReadMode(BinaryReader reader) => new(
        reader.ReadInt32(), reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadBoolean(),
        reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt64(),
        reader.ReadInt32(), reader.ReadInt32(), reader.ReadBoolean(), ReadEntityIds(reader), ReadStrings(reader));

    private static void WriteSession(BinaryWriter writer, AppleEverestProgressionSession value)
    {
        WriteText(writer, value.Sid); WriteText(writer, value.CompatibilityId); writer.Write(value.Mode); WriteNullableText(writer, value.Level);
        writer.Write(value.HasRespawnPoint); writer.Write(value.RespawnX); writer.Write(value.RespawnY); WriteNullableText(writer, value.StartCheckpoint);
        writer.Write(value.Time); writer.Write(value.StartedFromBeginning); writer.Write(value.Deaths); writer.Write(value.Dashes);
        writer.Write(value.DashesAtLevelStart); writer.Write(value.DeathsInCurrentLevel); writer.Write(value.InArea); writer.Write(value.FirstLevel);
        writer.Write(value.Cassette); writer.Write(value.HeartGem); writer.Write(value.Dreaming); WriteNullableText(writer, value.ColorGrade);
        writer.Write(value.LightingAlphaAdd); writer.Write(value.BloomBaseAdd); writer.Write(value.DarkRoomAlpha); writer.Write(value.CoreMode);
        writer.Write(value.GrabbedGolden); writer.Write(value.HitCheckpoint); writer.Write(value.InventoryDashes);
        writer.Write(value.InventoryDreamDash); writer.Write(value.InventoryBackpack); writer.Write(value.InventoryNoRefills);
        WriteNullableText(writer, value.MusicEvent); WriteParameters(writer, value.MusicParameters);
        WriteNullableText(writer, value.AmbienceEvent); WriteParameters(writer, value.AmbienceParameters);
        WriteStrings(writer, value.Flags); WriteStrings(writer, value.LevelFlags); WriteEntityIds(writer, value.Strawberries);
        WriteEntityIds(writer, value.DoNotLoad); WriteEntityIds(writer, value.Keys); WriteCounters(writer, value.Counters);
        WriteBools(writer, value.SummitGems); writer.Write(value.UnlockedCSide); WriteNullableText(writer, value.FurthestSeenLevel);
        writer.Write(value.BeatBestTime); writer.Write(value.OldStatsCassette); writer.Write(value.OldStatsModes.Length);
        foreach (AppleEverestProgressionMode mode in value.OldStatsModes) WriteMode(writer, mode);
    }

    private static AppleEverestProgressionSession ReadSession(BinaryReader reader) => new(
        ReadText(reader), ReadText(reader), reader.ReadInt32(), ReadNullableText(reader),
        reader.ReadBoolean(), reader.ReadSingle(), reader.ReadSingle(), ReadNullableText(reader),
        reader.ReadInt64(), reader.ReadBoolean(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
        reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadBoolean(), ReadNullableText(reader),
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadInt32(), reader.ReadBoolean(), reader.ReadBoolean(),
        reader.ReadInt32(), reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadBoolean(),
        ReadNullableText(reader), ReadParameters(reader), ReadNullableText(reader), ReadParameters(reader),
        ReadStrings(reader), ReadStrings(reader), ReadEntityIds(reader), ReadEntityIds(reader), ReadEntityIds(reader), ReadCounters(reader),
        ReadBools(reader), reader.ReadBoolean(), ReadNullableText(reader), reader.ReadBoolean(), reader.ReadBoolean(), ReadModes(reader));

    private static AppleEverestProgressionMode[] ReadModes(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count is < 1 or > MaximumModes) throw new InvalidDataException("invalid old-stats mode count");
        AppleEverestProgressionMode[] result = new AppleEverestProgressionMode[count];
        for (int index = 0; index < count; index++) result[index] = ReadMode(reader);
        return result;
    }

    private static void WriteEntityIds(BinaryWriter writer, IEnumerable<AppleEverestProgressionEntityId> values)
    {
        AppleEverestProgressionEntityId[] ordered = (values ?? Array.Empty<AppleEverestProgressionEntityId>()).OrderBy(value => value.Level, StringComparer.Ordinal).ThenBy(value => value.Id).ToArray();
        Count(ordered.Length); writer.Write(ordered.Length);
        foreach (AppleEverestProgressionEntityId value in ordered) { WriteText(writer, value.Level); writer.Write(value.Id); }
    }

    private static AppleEverestProgressionEntityId[] ReadEntityIds(BinaryReader reader)
    {
        int count = ReadCount(reader); AppleEverestProgressionEntityId[] result = new AppleEverestProgressionEntityId[count];
        for (int index = 0; index < count; index++) result[index] = new(ReadText(reader), reader.ReadInt32());
        return result;
    }

    private static void WriteStrings(BinaryWriter writer, IEnumerable<string> values)
    {
        string[] ordered = (values ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Count(ordered.Length); writer.Write(ordered.Length); foreach (string value in ordered) WriteText(writer, value);
    }
    private static string[] ReadStrings(BinaryReader reader)
    { int count = ReadCount(reader); string[] result = new string[count]; for (int i = 0; i < count; i++) result[i] = ReadText(reader); return result; }

    private static void WriteParameters(BinaryWriter writer, IEnumerable<AppleEverestProgressionParameter> values)
    {
        AppleEverestProgressionParameter[] ordered = (values ?? Array.Empty<AppleEverestProgressionParameter>()).OrderBy(value => value.Key, StringComparer.Ordinal).ToArray();
        Count(ordered.Length); writer.Write(ordered.Length); foreach (var value in ordered) { WriteText(writer, value.Key); writer.Write(value.Value); }
    }
    private static AppleEverestProgressionParameter[] ReadParameters(BinaryReader reader)
    { int count = ReadCount(reader); var result = new AppleEverestProgressionParameter[count]; for (int i = 0; i < count; i++) result[i] = new(ReadText(reader), reader.ReadSingle()); return result; }

    private static void WriteCounters(BinaryWriter writer, IEnumerable<AppleEverestProgressionCounter> values)
    {
        AppleEverestProgressionCounter[] ordered = (values ?? Array.Empty<AppleEverestProgressionCounter>()).OrderBy(value => value.Key, StringComparer.Ordinal).ToArray();
        Count(ordered.Length); writer.Write(ordered.Length); foreach (var value in ordered) { WriteText(writer, value.Key); writer.Write(value.Value); }
    }
    private static AppleEverestProgressionCounter[] ReadCounters(BinaryReader reader)
    { int count = ReadCount(reader); var result = new AppleEverestProgressionCounter[count]; for (int i = 0; i < count; i++) result[i] = new(ReadText(reader), reader.ReadInt32()); return result; }

    private static void WriteBools(BinaryWriter writer, IEnumerable<bool> values)
    { bool[] items = (values ?? Array.Empty<bool>()).ToArray(); Count(items.Length); writer.Write(items.Length); foreach (bool value in items) writer.Write(value); }
    private static bool[] ReadBools(BinaryReader reader)
    { int count = ReadCount(reader); bool[] result = new bool[count]; for (int i = 0; i < count; i++) result[i] = reader.ReadBoolean(); return result; }

    private static void WriteText(BinaryWriter writer, string value)
    {
        if (string.IsNullOrEmpty(value)) throw new InvalidDataException("empty progression identity");
        byte[] encoded = new UTF8Encoding(false, true).GetBytes(value);
        if (encoded.Length > MaximumStringBytes) throw new InvalidDataException("oversized progression string");
        writer.Write(encoded.Length); writer.Write(encoded);
    }
    private static string ReadText(BinaryReader reader)
    { int length = reader.ReadInt32(); if (length is < 1 or > MaximumStringBytes) throw new InvalidDataException("invalid progression string"); byte[] value = reader.ReadBytes(length); if (value.Length != length) throw new EndOfStreamException(); return new UTF8Encoding(false, true).GetString(value); }
    private static void WriteNullableText(BinaryWriter writer, string value) { writer.Write(value != null); if (value != null) WriteText(writer, value); }
    private static string ReadNullableText(BinaryReader reader) => reader.ReadBoolean() ? ReadText(reader) : null;
    private static int ReadCount(BinaryReader reader) { int value = reader.ReadInt32(); Count(value); return value; }
    private static void Count(int value) { if (value is < 0 or > MaximumItems) throw new InvalidDataException("progression collection exceeds bound"); }

    private static void Validate(AppleEverestProgressionSnapshot value)
    {
        if (value == null || value.Slot is < 0 or > 2 || value.Generation < 1 ||
            value.BaseSaveSha256?.Length != HashBytes || value.Lineage?.Length != HashBytes ||
            value.Areas == null || value.Areas.Length > MaximumAreas ||
            value.Areas.Select(area => area.Sid).Distinct(StringComparer.Ordinal).Count() != value.Areas.Length)
            throw new InvalidDataException("invalid Apple Everest progression snapshot metadata");
        foreach (AppleEverestProgressionArea area in value.Areas)
            if (string.IsNullOrEmpty(area.Sid) || string.IsNullOrEmpty(area.LevelSet) || string.IsNullOrEmpty(area.CompatibilityId) ||
                area.Modes == null || area.Modes.Length is < 1 or > MaximumModes)
                throw new InvalidDataException("invalid Apple Everest progression area");
        if (value.Session != null && (value.Session.OldStatsModes == null ||
            value.Session.OldStatsModes.Length is < 1 or > MaximumModes))
            throw new InvalidDataException("invalid Apple Everest progression session baseline");
    }
}
