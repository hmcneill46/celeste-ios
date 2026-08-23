#nullable disable
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Celeste.Mod;

internal static class AppleEverestProgressionCompression
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("AEVPZV1\0");
    internal const int MaximumLogicalBytes = 1024 * 1024;
    internal const int MaximumReplicaBytes = 126976;
    internal const int MaximumTotalReplicaBytes = MaximumReplicaBytes * 6;
    private const int HashBytes = 32;

    internal static byte[] Encode(byte[] logical)
    {
        if (logical == null || logical.Length == 0 || logical.Length > MaximumLogicalBytes)
            throw new InvalidDataException("Apple Everest tvOS progression exceeds its logical budget");
        using MemoryStream compressed = new();
        using (DeflateStream deflate = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(logical, 0, logical.Length);
        byte[] body = compressed.ToArray();
        using MemoryStream envelope = new();
        using (BinaryWriter writer = new(envelope, new UTF8Encoding(false, true), leaveOpen: true))
        {
            writer.Write(Magic); writer.Write(1); writer.Write(logical.Length);
            writer.Write(SHA256.HashData(logical)); writer.Write(body.Length); writer.Write(body);
        }
        byte[] unsigned = envelope.ToArray();
        byte[] digest = SHA256.HashData(unsigned);
        if (unsigned.Length + digest.Length > MaximumReplicaBytes)
            throw new InvalidDataException("Apple Everest tvOS progression exceeds its compressed replica budget");
        return unsigned.Concat(digest).ToArray();
    }

    internal static bool TryDecode(byte[] envelope, out byte[] logical)
    {
        logical = null;
        if (envelope == null || envelope.Length < Magic.Length + 4 + 4 + HashBytes + 4 + HashBytes ||
            envelope.Length > MaximumReplicaBytes) return false;
        int unsignedLength = envelope.Length - HashBytes;
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(envelope.AsSpan(0, unsignedLength)),
                envelope.AsSpan(unsignedLength, HashBytes))) return false;
        try
        {
            using MemoryStream input = new(envelope, 0, unsignedLength, writable: false);
            using BinaryReader reader = new(input, new UTF8Encoding(false, true), leaveOpen: true);
            if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic) || reader.ReadInt32() != 1) return false;
            int logicalLength = reader.ReadInt32(); byte[] expected = reader.ReadBytes(HashBytes); int compressedLength = reader.ReadInt32();
            if (logicalLength is < 1 or > MaximumLogicalBytes || expected.Length != HashBytes ||
                compressedLength < 1 || compressedLength != input.Length - input.Position) return false;
            using DeflateStream deflate = new(input, CompressionMode.Decompress, leaveOpen: false);
            byte[] result = new byte[logicalLength]; int offset = 0;
            while (offset < result.Length) { int read = deflate.Read(result, offset, result.Length - offset); if (read == 0) return false; offset += read; }
            if (deflate.ReadByte() != -1 || !CryptographicOperations.FixedTimeEquals(expected, SHA256.HashData(result))) return false;
            logical = result; return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException) { return false; }
    }
}
