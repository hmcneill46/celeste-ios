using System.Text.Json;

namespace AppleEverestBuilder;

// Hash of the complete private, reproducibly extracted authored data. The
// generating script rehashes the exact SJ archive, all three unchanged maps,
// and the immutable K-J profile control before deriving this document.
internal static class SnasProfileAuthority
{
    internal const string Sha256 = "1906bcec05cfb5ae5c16c471368716dd275aaf747f6b8e24eca8abd4741c6bfd";

    internal static JsonDocument Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (Hashing.BytesSha256(bytes) != Sha256)
            throw new InvalidDataException("K-N authored profile authority differs from exact three-map extraction");
        JsonDocument document = JsonDocument.Parse(bytes);
        if (document.RootElement.GetProperty("census").GetProperty("selectedFactories").GetInt32() != 77 ||
            document.RootElement.GetProperty("census").GetProperty("selectedOccurrences").GetInt32() != 973)
        {
            document.Dispose();
            throw new InvalidDataException("K-N authored profile census differs");
        }
        return document;
    }
}
