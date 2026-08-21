#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Celeste.Mod;

// A deliberately small YAML 1.2 reader for the default, reflection-free
// EverestModule SaveData/Session compatibility class. It accepts the ordinary
// block mappings/sequences and scalar forms emitted by pinned YamlDotNet, plus
// JSON flow documents. It rejects tags, anchors, aliases, directives, block
// scalars and arbitrary object construction.
internal static class AppleEverestBoundedYaml
{
    private const int MaximumLines = 65536;
    private const int MaximumNodes = 131072;
    private const int MaximumScalarCharacters = 65536;
    private const int MaximumDepth = 32;

    private sealed record Line(int Indent, string Text);

    internal static JsonDocument Parse(byte[] payload)
    {
        string source;
        try { source = new UTF8Encoding(false, true).GetString(payload); }
        catch (DecoderFallbackException exception) { throw new InvalidDataException("module YAML is not valid UTF-8", exception); }
        string trimmed = source.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            return JsonDocument.Parse(payload, JsonOptions());

        List<Line> lines = Tokenize(source);
        if (lines.Count == 0) throw new InvalidDataException("module YAML is empty");
        int index = 0;
        int nodes = 0;
        JsonNode node = ParseBlock(lines, ref index, lines[0].Indent, 0, ref nodes);
        if (index != lines.Count) throw new InvalidDataException("module YAML contains an invalid indentation transition");
        return JsonDocument.Parse(node.ToJsonString(), JsonOptions());
    }

    private static JsonDocumentOptions JsonOptions() => new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = MaximumDepth
    };

    private static List<Line> Tokenize(string source)
    {
        string[] raw = source.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n').Split('\n');
        if (raw.Length > MaximumLines) throw new InvalidDataException("module YAML has too many lines");
        List<Line> result = new List<Line>();
        foreach (string original in raw)
        {
            if (original.IndexOf('\t') >= 0) throw new InvalidDataException("module YAML tabs are unsupported");
            int indent = 0;
            while (indent < original.Length && original[indent] == ' ') indent++;
            string text = original[indent..].TrimEnd();
            if (text.Length == 0 || text is "---" or "...") continue;
            if (text.StartsWith('%') || text.StartsWith('!') || text.StartsWith('&') || text.StartsWith('*'))
                throw new InvalidDataException("module YAML dynamic features are unsupported");
            result.Add(new Line(indent, text));
        }
        return result;
    }

    private static JsonNode ParseBlock(List<Line> lines, ref int index, int indent, int depth, ref int nodes)
    {
        Bound(depth, ref nodes);
        if (index >= lines.Count || lines[index].Indent < indent)
            throw new InvalidDataException("module YAML expected a nested value");
        return lines[index].Text == "-" || lines[index].Text.StartsWith("- ", StringComparison.Ordinal)
            ? ParseSequence(lines, ref index, indent, depth, ref nodes)
            : ParseMapping(lines, ref index, indent, depth, ref nodes);
    }

    private static JsonArray ParseSequence(List<Line> lines, ref int index, int indent, int depth, ref int nodes)
    {
        JsonArray result = new JsonArray();
        while (index < lines.Count && lines[index].Indent == indent &&
               (lines[index].Text == "-" || lines[index].Text.StartsWith("- ", StringComparison.Ordinal)))
        {
            Bound(depth, ref nodes);
            string item = lines[index++].Text[1..].TrimStart();
            if (item.Length == 0)
            {
                if (index >= lines.Count || lines[index].Indent <= indent)
                    result.Add(null);
                else
                    result.Add(ParseBlock(lines, ref index, lines[index].Indent, depth + 1, ref nodes));
                continue;
            }
            if (TryPair(item, out string key, out string value))
            {
                JsonObject objectValue = new JsonObject();
                AddPair(objectValue, key, value, lines, ref index, indent, depth + 1, ref nodes,
                    allowIndentlessSequence: false);
                if (index < lines.Count && lines[index].Indent > indent)
                {
                    int childIndent = lines[index].Indent;
                    ParseMappingInto(objectValue, lines, ref index, childIndent, depth + 1, ref nodes);
                }
                result.Add((JsonNode)objectValue);
            }
            else
            {
                result.Add(ParseScalar(item, ref nodes));
            }
        }
        return result;
    }

    private static JsonObject ParseMapping(List<Line> lines, ref int index, int indent, int depth, ref int nodes)
    {
        JsonObject result = new JsonObject();
        ParseMappingInto(result, lines, ref index, indent, depth, ref nodes);
        return result;
    }

    private static void ParseMappingInto(JsonObject result, List<Line> lines, ref int index,
        int indent, int depth, ref int nodes)
    {
        while (index < lines.Count && lines[index].Indent == indent &&
               lines[index].Text != "-" && !lines[index].Text.StartsWith("- ", StringComparison.Ordinal))
        {
            Bound(depth, ref nodes);
            Line line = lines[index++];
            if (!TryPair(line.Text, out string key, out string value))
                throw new InvalidDataException("module YAML expected a mapping entry");
            if (result.ContainsKey(key)) throw new InvalidDataException("module YAML contains a duplicate key");
            AddPair(result, key, value, lines, ref index, indent, depth, ref nodes,
                allowIndentlessSequence: true);
        }
    }

    private static void AddPair(JsonObject target, string key, string value, List<Line> lines,
        ref int index, int indent, int depth, ref int nodes, bool allowIndentlessSequence)
    {
        key = ParseKey(key);
        if (key.Length == 0 || key.Length > MaximumScalarCharacters)
            throw new InvalidDataException("module YAML has an invalid key");
        if (value.Length > 0)
        {
            target[key] = ParseScalar(value, ref nodes);
            return;
        }
        if (index >= lines.Count || lines[index].Indent < indent ||
            lines[index].Indent == indent && !(allowIndentlessSequence &&
                (lines[index].Text == "-" || lines[index].Text.StartsWith("- ", StringComparison.Ordinal))))
        {
            target[key] = null;
            return;
        }
        int nestedIndent = lines[index].Indent;
        target[key] = ParseBlock(lines, ref index, nestedIndent, depth + 1, ref nodes);
    }

    private static bool TryPair(string text, out string key, out string value)
    {
        bool single = false;
        bool quoted = false;
        bool escaped = false;
        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (escaped) { escaped = false; continue; }
            if (quoted && current == '\\') { escaped = true; continue; }
            if (!single && current == '"') { quoted = !quoted; continue; }
            if (!quoted && current == '\'') { single = !single; continue; }
            if (!quoted && !single && current == ':' &&
                (index + 1 == text.Length || char.IsWhiteSpace(text[index + 1])))
            {
                key = text[..index].Trim();
                value = text[(index + 1)..].TrimStart();
                return true;
            }
        }
        key = value = "";
        return false;
    }

    private static string ParseKey(string value)
    {
        int ignored = 0;
        JsonNode scalar = ParseScalar(value, ref ignored);
        if (scalar is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string text)) return text;
        return value;
    }

    private static JsonNode ParseScalar(string value, ref int nodes)
    {
        Bound(0, ref nodes);
        if (value.Length > MaximumScalarCharacters) throw new InvalidDataException("module YAML scalar is too long");
        if (value is "{}") return new JsonObject();
        if (value is "[]") return new JsonArray();
        if (value is "|" or ">" || value.StartsWith('!') || value.StartsWith('&') || value.StartsWith('*'))
            throw new InvalidDataException("module YAML dynamic or block scalar features are unsupported");
        if (value.StartsWith('{') || value.StartsWith('['))
            return JsonNode.Parse(value, documentOptions: JsonOptions())
                   ?? throw new InvalidDataException("module YAML flow value is empty");
        if (value.StartsWith('"'))
        {
            JsonNode quoted = JsonNode.Parse(value, documentOptions: JsonOptions());
            return quoted ?? JsonValue.Create((string)null);
        }
        if (value.StartsWith('\''))
        {
            if (value.Length < 2 || value[^1] != '\'') throw new InvalidDataException("module YAML has an unterminated quoted scalar");
            return JsonValue.Create(value[1..^1].Replace("''", "'", StringComparison.Ordinal));
        }
        if (value is "~" or "null" or "Null" or "NULL") return null;
        if (value is "true" or "True" or "TRUE") return JsonValue.Create(true);
        if (value is "false" or "False" or "FALSE") return JsonValue.Create(false);
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long signed))
            return JsonValue.Create(signed);
        if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong unsigned))
            return JsonValue.Create(unsigned);
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) &&
            !double.IsNaN(number) && !double.IsInfinity(number))
            return JsonValue.Create(number);
        if (value.Contains(" #", StringComparison.Ordinal))
            throw new InvalidDataException("module YAML comments on plain scalars are unsupported");
        return JsonValue.Create(value);
    }

    private static void Bound(int depth, ref int nodes)
    {
        if (depth > MaximumDepth) throw new InvalidDataException("module YAML nesting is too deep");
        if (++nodes > MaximumNodes) throw new InvalidDataException("module YAML has too many values");
    }
}
