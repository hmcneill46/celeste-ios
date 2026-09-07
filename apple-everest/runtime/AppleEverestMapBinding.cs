#nullable disable
using System;

namespace Celeste.Mod;

internal sealed class AppleEverestMapBinding
{
    internal readonly string Sid, SourceLabel, ForegroundTiles, BackgroundTiles, AnimatedTiles, Sprites;
    internal readonly bool MergeAnimations;
    internal readonly int TerrainSeed;
    internal AppleEverestMapBinding(string sid, string sourceLabel, string foregroundTiles, string backgroundTiles,
        string animatedTiles, string sprites, bool mergeAnimations, int terrainSeed)
    {
        Sid = sid; SourceLabel = sourceLabel; ForegroundTiles = foregroundTiles; BackgroundTiles = backgroundTiles;
        AnimatedTiles = animatedTiles; Sprites = sprites; MergeAnimations = mergeAnimations;
        TerrainSeed = terrainSeed;
    }
    internal static AppleEverestMapBinding Find(string sid)
    {
        foreach (var map in GeneratedAppleEverestMapBindings.Maps) if (map.Sid == sid) return map;
        return null;
    }
    // The isolated debug route temporarily mounts a selected map in Area 0.
    // Its active ModeData.Path still identifies the actual content. Never use
    // that temporary area number as the identity of graphics or module rules.
    internal static AppleEverestMapBinding ForSession(Session session) =>
        Find(session?.MapData?.ModeData?.Path);
    // Pure generated lookup is available before AreaData constructs MapData.
    internal static bool HeaderMatches(string sid, string label)
    {
        var map = Find(sid);
        return String.Equals(map == null ? sid : map.SourceLabel, label, StringComparison.Ordinal);
    }
    internal static string DestinationAvailability(string sid)
    {
        if (Find(sid) != null) return "AVAILABLE_SELECTED";
        foreach (string excluded in GeneratedAppleEverestMapBindings.ExcludedMapSids)
            if (excluded == sid) return "UNAVAILABLE_EXCLUDED";
        return "UNAVAILABLE_UNKNOWN";
    }
}
