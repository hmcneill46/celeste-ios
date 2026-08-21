#nullable disable
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Celeste.Mod;

// The generated adapters emit JSON flow mappings. JSON is a strict,
// deterministic subset of YAML 1.2 and is accepted by the pinned desktop
// Everest YamlDotNet reader.  This keeps module data human-inspectable while
// avoiding reflection, runtime type tags, and arbitrary object construction on
// fully trimmed Apple AOT builds.
internal static class AppleEverestModuleYaml
{
    internal const int MaximumModuleBytes = 512 * 1024;
    private const int MaximumDepth = 32;

    internal static byte[] Write(Action<Utf8JsonWriter> write)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions
               {
                   Indented = true,
                   MaxDepth = MaximumDepth,
                   SkipValidation = false
               }))
        {
            write(writer);
        }
        if (stream.Length > MaximumModuleBytes)
            throw new InvalidDataException("Apple Everest module YAML exceeds its bounded payload budget");
        byte[] body = stream.ToArray();
        byte[] result = new byte[body.Length + 1];
        Buffer.BlockCopy(body, 0, result, 0, body.Length);
        result[^1] = (byte)'\n';
        return result;
    }

    internal static JsonDocument Parse(byte[] payload)
    {
        if (payload == null || payload.Length == 0 || payload.Length > MaximumModuleBytes)
            throw new InvalidDataException("invalid Apple Everest module YAML length");
        return AppleEverestBoundedYaml.Parse(payload);
    }

    internal static bool TryProperty(JsonElement value, string name, out JsonElement property)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Apple Everest module YAML expected a mapping");
        return value.TryGetProperty(name, out property);
    }

    internal static string String(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Null => null,
        _ => throw new InvalidDataException("Apple Everest module YAML expected a string")
    };

    internal static bool Boolean(JsonElement value) => value.ValueKind is JsonValueKind.True or JsonValueKind.False
        ? value.GetBoolean()
        : throw new InvalidDataException("Apple Everest module YAML expected a boolean");

    internal static int Int32(JsonElement value) => value.TryGetInt32(out int result)
        ? result : throw new InvalidDataException("Apple Everest module YAML expected an Int32");

    internal static uint UInt32(JsonElement value) => value.TryGetUInt32(out uint result)
        ? result : throw new InvalidDataException("Apple Everest module YAML expected a UInt32");

    internal static long Int64(JsonElement value) => value.TryGetInt64(out long result)
        ? result : throw new InvalidDataException("Apple Everest module YAML expected an Int64");

    internal static ulong UInt64(JsonElement value) => value.TryGetUInt64(out ulong result)
        ? result : throw new InvalidDataException("Apple Everest module YAML expected a UInt64");

    internal static float Single(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out float result) ||
            float.IsNaN(result) || float.IsInfinity(result))
            throw new InvalidDataException("Apple Everest module YAML expected a finite Single");
        return result;
    }

    internal static double Double(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double result) ||
            double.IsNaN(result) || double.IsInfinity(result))
            throw new InvalidDataException("Apple Everest module YAML expected a finite Double");
        return result;
    }

    internal static decimal Decimal(JsonElement value) => value.TryGetDecimal(out decimal result)
        ? result : throw new InvalidDataException("Apple Everest module YAML expected a Decimal");

    internal static void WriteSingle(Utf8JsonWriter writer, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new InvalidDataException("non-finite module Single is unsupported");
        writer.WriteNumberValue(value);
    }

    internal static void WriteDouble(Utf8JsonWriter writer, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new InvalidDataException("non-finite module Double is unsupported");
        writer.WriteNumberValue(value);
    }

    internal static void RequireArray(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Apple Everest module YAML expected a sequence");
    }

    internal static void RequireObject(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Apple Everest module YAML expected a mapping");
    }

    internal static string Describe(byte[] payload) => Encoding.UTF8.GetString(payload);
}
