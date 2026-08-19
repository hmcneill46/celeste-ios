using System;
using System.Collections.Generic;

namespace Celeste.Mod;

// A small reflection-free subset of Everest's tag-prefix log policy. Ordinary
// mod logging must not become unbounded device I/O merely because the static
// runtime implements a binary-compatible Logger facade.
internal static class AppleEverestLogPolicy
{
    private static readonly Dictionary<string, LogLevel> MinimumLevels = new(StringComparer.Ordinal);

    internal static void Set(string tag, LogLevel level)
    {
        lock (MinimumLevels) MinimumLevels[tag ?? string.Empty] = level;
    }

    internal static bool ShouldLog(string tag, LogLevel level)
    {
        tag ??= string.Empty;
        lock (MinimumLevels)
        {
            string best = null;
            LogLevel minimum = LogLevel.Info;
            foreach (KeyValuePair<string, LogLevel> candidate in MinimumLevels)
            {
                if (tag.StartsWith(candidate.Key, StringComparison.Ordinal) &&
                    (best == null || candidate.Key.Length > best.Length ||
                     candidate.Key.Length == best.Length && string.CompareOrdinal(candidate.Key, best) > 0))
                {
                    best = candidate.Key;
                    minimum = candidate.Value;
                }
            }
            return level >= minimum;
        }
    }
}
