using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Celeste.Mod;

internal sealed record AppleEverestDialogFragmentEntry(string Raw, string Cleaned);

/// <summary>
/// Parses statically mounted mod dialog fragments with the observable text
/// transformations performed by Celeste Language.FromTxt. Mod fragments are
/// merged into an already loaded language, so language metadata is ignored.
/// </summary>
internal static class AppleEverestDialogFragmentParser
{
    // Everest's DialogKeyify, plus an isolated namespace for the owned K-H
    // diagnostic. The selected source profiles deliberately share these keys.
    internal static string KeyForMap(string? sid, string key)
    {
        string normalized = key.Replace('/', '_').Replace('-', '_').Replace('+', '_').Replace(' ', '_');
        if (sid == "AppleEverest/Stage25KH" &&
            (normalized.Equals("SJ2021_lobby_gym_tutorial_info", StringComparison.OrdinalIgnoreCase) ||
             normalized.Equals("SJ2021_lobby_gym_tutorial_controls", StringComparison.OrdinalIgnoreCase) ||
             normalized.Equals("StrawberryJam2021_0_Lobbies_1_Beginner_Credits", StringComparison.OrdinalIgnoreCase)))
            return "APPLE_EVEREST_STAGE25KH_" + normalized;
        return normalized;
    }

    private static readonly Regex Command = new("\\{(.*?)\\}", RegexOptions.RightToLeft);
    private static readonly Regex Insert = new("\\{\\+\\s*(.*?)\\}");
    private static readonly Regex Variable = new("^\\w+\\=.*");
    private static readonly Regex Portrait = new(
        "\\[(?<content>[^\\[\\\\]*(?:\\\\.[^\\]\\\\]*)*)\\]", RegexOptions.IgnoreCase);

    private static readonly HashSet<string> MetadataKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "language", "icon", "order", "split_regex", "commas", "periods", "font"
    };

    internal static IReadOnlyDictionary<string, AppleEverestDialogFragmentEntry> Parse(
        string content, IReadOnlyDictionary<string, string> inherited)
    {
        Dictionary<string, string> raw = new(StringComparer.OrdinalIgnoreCase);
        string? key = null;
        string previousLine = "";
        StringBuilder value = new();

        void Commit()
        {
            if (!string.IsNullOrWhiteSpace(key)) raw[key] = value.ToString();
        }

        foreach (string sourceLine in content.Replace("\r", "").Split('\n'))
        {
            string line = sourceLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
            if (line.IndexOf('[') >= 0)
                line = Portrait.Replace(line, match => "{portrait " + match.Groups["content"].Value + "}");
            line = line.Replace("\\#", "#", StringComparison.Ordinal);

            if (Variable.IsMatch(line))
            {
                Commit();
                int equals = line.IndexOf('=');
                string nextKey = line[..equals].Trim();
                key = MetadataKeys.Contains(nextKey) ? null : nextKey;
                value.Clear();
                if (key != null) value.Append(line[(equals + 1)..].Trim());
            }
            else if (key != null)
            {
                if (value.Length > 0 && !value.ToString().EndsWith("{break}", StringComparison.Ordinal) &&
                    !value.ToString().EndsWith("{n}", StringComparison.Ordinal) &&
                    Command.Replace(previousLine, "").Length > 0)
                    value.Append("{break}");
                value.Append(line);
            }
            previousLine = line;
        }
        Commit();

        Dictionary<string, AppleEverestDialogFragmentEntry> result =
            new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string source) in raw)
        {
            string resolved = source;
            for (int pass = 0; ; pass++)
            {
                MatchCollection matches = Insert.Matches(resolved);
                if (matches.Count == 0) break;
                if (pass >= 128) throw new InvalidDataException("cyclic or oversized dialog insert chain: " + name);
                foreach (Match match in matches)
                {
                    string target = match.Groups[1].Value;
                    string replacement;
                    if (raw.TryGetValue(target, out string? local) && local != null)
                        replacement = local;
                    else if (inherited.TryGetValue(target, out string? existing) && existing != null)
                        replacement = existing;
                    else
                        replacement = "[XXX]";
                    resolved = resolved.Replace(match.Value, replacement, StringComparison.Ordinal);
                }
            }
            string cleaned = resolved.Replace("{n}", "\n", StringComparison.Ordinal)
                .Replace("{break}", "\n", StringComparison.Ordinal);
            if (cleaned.IndexOf('{') >= 0) cleaned = Command.Replace(cleaned, "");
            result[name] = new AppleEverestDialogFragmentEntry(resolved, cleaned);
        }
        return result;
    }
}
