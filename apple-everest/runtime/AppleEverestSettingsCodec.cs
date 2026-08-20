using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Celeste.Mod;

internal readonly record struct AppleEverestSettingRecord(string Module, string Property, int Value);

internal static class AppleEverestSettingsCodec
{
    internal const string Header = "APPLE_EVEREST_SETTINGS_V1";
    internal const int MaximumBytes = 16 * 1024;

    internal static string Encode(IEnumerable<AppleEverestSettingRecord> values)
    {
        StringBuilder result = new StringBuilder(Header).Append('\n');
        foreach (AppleEverestSettingRecord value in values.OrderBy(item => item.Module, StringComparer.Ordinal)
                     .ThenBy(item => item.Property, StringComparer.Ordinal))
        {
            result.Append(Token(value.Module)).Append('\t').Append(Token(value.Property)).Append('\t')
                .Append(value.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        string encoded = result.ToString();
        if (Encoding.UTF8.GetByteCount(encoded) > MaximumBytes)
            throw new InvalidOperationException("Apple Everest settings exceed the bounded storage budget");
        return encoded;
    }

    internal static bool TryDecode(string text, out AppleEverestSettingRecord[] values)
    {
        values = Array.Empty<AppleEverestSettingRecord>();
        if (string.IsNullOrEmpty(text) || Encoding.UTF8.GetByteCount(text) > MaximumBytes) return false;
        string[] lines = text.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        if (lines.Length < 2 || lines[0] != Header) return false;
        List<AppleEverestSettingRecord> parsed = new List<AppleEverestSettingRecord>();
        HashSet<string> keys = new(StringComparer.Ordinal);
        for (int index = 1; index < lines.Length; index++)
        {
            if (lines[index].Length == 0) continue;
            string[] fields = lines[index].Split('\t');
            if (fields.Length != 3 || !TryToken(fields[0], out string module) || !TryToken(fields[1], out string property) ||
                !int.TryParse(fields[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value) ||
                module.Length is < 1 or > 192 || property.Length is < 1 or > 128 || !keys.Add(module + "\0" + property))
                return false;
            parsed.Add(new AppleEverestSettingRecord(module, property, value));
        }
        values = parsed.OrderBy(item => item.Module, StringComparer.Ordinal)
            .ThenBy(item => item.Property, StringComparer.Ordinal).ToArray();
        return true;
    }

    private static string Token(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryToken(string value, out string decoded)
    {
        decoded = "";
        if (value.Length is < 1 or > 512 || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')) return false;
        try
        {
            string base64 = value.Replace('-', '+').Replace('_', '/');
            base64 += new string('=', (4 - base64.Length % 4) % 4);
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            return Token(decoded) == value;
        }
        catch (FormatException) { return false; }
    }
}
