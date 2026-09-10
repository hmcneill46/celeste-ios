using System.Text.Encodings.Web;
using System.Text.Json;

namespace AppleEverestBuilder;

// Hashes the already-existing canonical metadata/IL description, not emitted
// native bytes. The actual linked method bodies must match the accepted six.
internal static class LegacyFactoryAuthority
{
    internal static void Verify(object[] rows, string authorityPath)
    {
        if (Hashing.FileSha256(authorityPath) != "83ed99658075ec1f3da44706529b4c9008aea3759dcd393e711d2637c26c0ca5")
            throw new InvalidDataException("legacy factory reference authority differs");
        using JsonDocument authority = JsonDocument.Parse(File.ReadAllBytes(authorityPath));
        JsonElement[] expected = authority.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        JsonElement[] actual = JsonSerializer.SerializeToElement(rows).EnumerateArray().ToArray();
        if (actual.Length != 6 || expected.Length != actual.Length)
            throw new InvalidDataException("legacy factory reference census differs");
        for (int i = 0; i < actual.Length; i++)
        {
            JsonElement a = actual[i], b = expected[i];
            if (new[] { "kind", "customId", "provider", "entrySha256" }.Any(key => a.GetProperty(key).GetString() != b.GetProperty(key).GetString()) ||
                Hashing.BytesSha256(Canonical(a.GetProperty("linkedTypeClosure"))) != b.GetProperty("linkedTypeClosureSha256").GetString() ||
                !Canonical(a.GetProperty("actualProductionCallers")).SequenceEqual(Canonical(b.GetProperty("productionCallers"))) ||
                !a.GetProperty("actualSelectorInvoked").GetBoolean() || a.GetProperty("selectedProfileGuardProof").GetBoolean() ||
                a.GetProperty("constructorLifecycleExecution").GetBoolean())
                throw new InvalidDataException("legacy factory entry/type reference differs: " + b.GetProperty("customId").GetString());
        }
    }

    private static byte[] Canonical(JsonElement value)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            void Write(JsonElement node)
            {
                switch (node.ValueKind)
                {
                    case JsonValueKind.Object:
                        writer.WriteStartObject();
                        foreach (JsonProperty property in node.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                        { writer.WritePropertyName(property.Name); Write(property.Value); }
                        writer.WriteEndObject(); break;
                    case JsonValueKind.Array:
                        writer.WriteStartArray(); foreach (JsonElement child in node.EnumerateArray()) Write(child); writer.WriteEndArray(); break;
                    case JsonValueKind.String: writer.WriteStringValue(node.GetString()); break;
                    case JsonValueKind.True: writer.WriteBooleanValue(true); break;
                    case JsonValueKind.False: writer.WriteBooleanValue(false); break;
                    case JsonValueKind.Null: writer.WriteNullValue(); break;
                    default: throw new InvalidDataException("unsupported legacy reference field shape");
                }
            }
            Write(value);
        }
        return stream.ToArray();
    }
}
