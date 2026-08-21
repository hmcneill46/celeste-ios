#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Celeste.Mod;

internal sealed record AppleEverestModuleSnapshotEntry(
    string Name,
    string Version,
    string Schema,
    byte[] SaveData,
    byte[] Session,
    bool SaveDataValid = true,
    bool SessionValid = true);

internal sealed record AppleEverestModuleSnapshot(
    int Slot,
    long Generation,
    byte[] BaseSaveSha256,
    string StaticClosureSha256,
    AppleEverestModuleSnapshotEntry[] Entries);

internal static class AppleEverestModuleSnapshotCodec
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("AEVMSV1\0");
    internal const int MaximumBytes = 2 * 1024 * 1024;
    internal const int MaximumModules = 128;
    private const int MaximumIdentityBytes = 256;
    private const int HashBytes = 32;

    internal static byte[] Encode(AppleEverestModuleSnapshot value)
    {
        ValidateSlot(value.Slot);
        if (value.Generation < 1 || value.BaseSaveSha256?.Length != HashBytes ||
            string.IsNullOrEmpty(value.StaticClosureSha256) ||
            value.Entries == null || value.Entries.Length > MaximumModules)
            throw new InvalidDataException("invalid Apple Everest module snapshot metadata");
        if (value.Entries.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != value.Entries.Length)
            throw new InvalidDataException("duplicate Apple Everest module snapshot identity");

        using MemoryStream body = new();
        using (BinaryWriter writer = new(body, new UTF8Encoding(false, true), leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(1);
            writer.Write(value.Slot);
            writer.Write(value.Generation);
            writer.Write(value.BaseSaveSha256);
            WriteText(writer, value.StaticClosureSha256);
            writer.Write(value.Entries.Length);
            foreach (AppleEverestModuleSnapshotEntry entry in value.Entries.OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                WriteText(writer, entry.Name);
                WriteText(writer, entry.Version);
                WriteText(writer, entry.Schema);
                if (!entry.SaveDataValid || !entry.SessionValid)
                    throw new InvalidDataException("cannot encode an invalid module payload");
                WritePayload(writer, entry.SaveData);
                WritePayload(writer, entry.Session);
            }
        }
        byte[] unsigned = body.ToArray();
        byte[] digest = SHA256.HashData(unsigned);
        if (unsigned.Length + digest.Length > MaximumBytes)
            throw new InvalidDataException("Apple Everest module snapshot exceeds its bounded storage budget");
        byte[] result = new byte[unsigned.Length + digest.Length];
        Buffer.BlockCopy(unsigned, 0, result, 0, unsigned.Length);
        Buffer.BlockCopy(digest, 0, result, unsigned.Length, digest.Length);
        return result;
    }

    internal static bool TryDecode(byte[] data, int expectedSlot, out AppleEverestModuleSnapshot value)
    {
        value = null;
        if (data == null || data.Length < Magic.Length + 4 + 4 + 8 + HashBytes + 4 + HashBytes ||
            data.Length > MaximumBytes || expectedSlot is < 0 or > 2)
            return false;
        int bodyLength = data.Length - HashBytes;
        if (!CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(data.AsSpan(0, bodyLength)), data.AsSpan(bodyLength, HashBytes)))
            return false;
        try
        {
            using MemoryStream stream = new(data, 0, bodyLength, writable: false);
            using BinaryReader reader = new(stream, new UTF8Encoding(false, true), leaveOpen: false);
            if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic) || reader.ReadInt32() != 1)
                return false;
            int slot = reader.ReadInt32();
            long generation = reader.ReadInt64();
            byte[] baseHash = reader.ReadBytes(HashBytes);
            string staticClosure = ReadText(reader);
            int count = reader.ReadInt32();
            if (slot != expectedSlot || generation < 1 || baseHash.Length != HashBytes ||
                count is < 0 or > MaximumModules)
                return false;
            AppleEverestModuleSnapshotEntry[] entries = new AppleEverestModuleSnapshotEntry[count];
            HashSet<string> names = new(StringComparer.Ordinal);
            for (int index = 0; index < count; index++)
            {
                string name = ReadText(reader);
                string version = ReadText(reader);
                string schema = ReadText(reader);
                if (!names.Add(name)) return false;
                (byte[] saveData, bool saveDataValid) = ReadPayload(reader);
                (byte[] session, bool sessionValid) = ReadPayload(reader);
                entries[index] = new(name, version, schema, saveData, session,
                    saveDataValid, sessionValid);
            }
            if (stream.Position != stream.Length) return false;
            value = new(slot, generation, baseHash, staticClosure, entries);
            return true;
        }
        catch (Exception exception) when (exception is EndOfStreamException or IOException or DecoderFallbackException or ArgumentException)
        {
            return false;
        }
    }

    internal static byte[] BaseHash(byte[] baseSave) => baseSave == null ? null : SHA256.HashData(baseSave);

    private static void WriteText(BinaryWriter writer, string value)
    {
        if (string.IsNullOrEmpty(value)) throw new InvalidDataException("empty module snapshot identity");
        byte[] encoded = new UTF8Encoding(false, true).GetBytes(value);
        if (encoded.Length > MaximumIdentityBytes) throw new InvalidDataException("oversized module snapshot identity");
        writer.Write(encoded.Length);
        writer.Write(encoded);
    }

    private static string ReadText(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length is < 1 or > MaximumIdentityBytes) throw new InvalidDataException("invalid module snapshot identity");
        byte[] encoded = reader.ReadBytes(length);
        if (encoded.Length != length) throw new EndOfStreamException();
        return new UTF8Encoding(false, true).GetString(encoded);
    }

    private static void WritePayload(BinaryWriter writer, byte[] value)
    {
        if (value == null)
        {
            writer.Write(-1);
            return;
        }
        if (value.Length > AppleEverestModuleYaml.MaximumModuleBytes)
            throw new InvalidDataException("oversized module YAML payload");
        writer.Write(value.Length);
        writer.Write(SHA256.HashData(value));
        writer.Write(value);
    }

    private static (byte[] Value, bool Valid) ReadPayload(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length == -1) return (null, true);
        if (length is < 0 or > AppleEverestModuleYaml.MaximumModuleBytes)
            throw new InvalidDataException("invalid module YAML payload length");
        byte[] expected = reader.ReadBytes(HashBytes);
        byte[] value = reader.ReadBytes(length);
        if (expected.Length != HashBytes || value.Length != length)
            throw new EndOfStreamException();
        return CryptographicOperations.FixedTimeEquals(expected, SHA256.HashData(value))
            ? (value, true)
            : (null, false);
    }

    private static void ValidateSlot(int slot)
    {
        if (slot is < 0 or > 2) throw new InvalidDataException("Apple Everest module durability only supports slots 0-2");
    }
}
