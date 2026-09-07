#nullable disable
using System.Collections.Generic;
using System.Xml;
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;

// Generated bindings supply map metadata and the explicit fixture merge
// policy. Original maps replace banks as pinned Everest does.
internal static class AppleEverestSelectedCanaryAssets
{
    private static Autotiler originalForeground, originalBackground;
    private static AnimatedTilesBank originalAnimations;
    private static Dictionary<char, int> originalSounds;
    private static bool active;

    internal static void PrepareLevel(Session session)
    {
        AppleEverestStaticRuntime.SelectedMapSpriteBank = null;
        if (active)
        {
            GFX.FGAutotiler = originalForeground;
            GFX.BGAutotiler = originalBackground;
            GFX.AnimatedTilesBank = originalAnimations;
            SurfaceIndex.TileToIndex.Clear();
            foreach (var entry in originalSounds) SurfaceIndex.TileToIndex.Add(entry.Key, entry.Value);
            active = false;
        }
        var map = AppleEverestMapBinding.ForSession(session);
        if (map == null || map.ForegroundTiles.Length + map.BackgroundTiles.Length + map.AnimatedTiles.Length + map.Sprites.Length == 0)
            return;
        originalForeground = GFX.FGAutotiler;
        originalBackground = GFX.BGAutotiler;
        originalAnimations = GFX.AnimatedTilesBank;
        originalSounds = new Dictionary<char, int>(SurfaceIndex.TileToIndex);
        active = true;
        // Same reset, background, foreground sound registration order as Everest.
        if (map.BackgroundTiles.Length > 0) GFX.BGAutotiler = new Autotiler(map.BackgroundTiles);
        if (map.ForegroundTiles.Length > 0) GFX.FGAutotiler = new Autotiler(map.ForegroundTiles);
        if (map.AnimatedTiles.Length > 0)
        {
            AnimatedTilesBank animations = new();
            HashSet<string> existing = new();
            if (map.MergeAnimations)
                foreach (AnimatedTilesBank.Animation animation in originalAnimations.Animations)
                {
                    animations.Add(animation.Name, animation.Delay, animation.Offset, animation.Origin, new List<MTexture>(animation.Frames));
                    existing.Add(animation.Name);
                }
            foreach (XmlElement definition in Calc.LoadContentXML(map.AnimatedTiles)["Data"])
            {
                string name = definition.Attr("name");
                if (map.MergeAnimations && existing.Contains(name)) continue;
                animations.Add(name, definition.AttrFloat("delay", 0f),
                    definition.AttrVector2("posX", "posY", Vector2.Zero), definition.AttrVector2("origX", "origY", Vector2.Zero),
                    GFX.Game.GetAtlasSubtextures(definition.Attr("path")));
            }
            GFX.AnimatedTilesBank = animations;
        }
        if (map.Sprites.Length > 0)
            AppleEverestStaticRuntime.SelectedMapSpriteBank = new SpriteBank(GFX.Game, map.Sprites);
    }
}
