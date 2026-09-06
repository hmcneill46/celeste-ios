#nullable disable
using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;

// Stage 25K-J canaries use the selected original tile definitions, sprite
// overrides and animations. This does not admit or mount an original SJ map.
internal static class AppleEverestSelectedCanaryAssets
{
    private const string Prefix = "AppleEverest/Mods/StrawberryJam2021/Graphics/SJ2021xmls/BeginnerLobby/";
    private static Autotiler originalForeground;
    private static AnimatedTilesBank originalAnimations;
    private static Dictionary<char, int> originalSounds;
    private static bool active;

    internal static void PrepareLevel(Session session)
    {
        string sid = AppleEverestProgressionRuntime.Sid(session.Area);
        bool selected = sid != null && (sid.StartsWith("AppleEverest/Stage25KJ", StringComparison.Ordinal) ||
            sid.StartsWith("AppleEverestStage25KJ/", StringComparison.Ordinal));
        if (active)
        {
            GFX.FGAutotiler = originalForeground;
            GFX.AnimatedTilesBank = originalAnimations;
            SurfaceIndex.TileToIndex.Clear();
            foreach (KeyValuePair<char, int> entry in originalSounds) SurfaceIndex.TileToIndex.Add(entry.Key, entry.Value);
            active = false;
        }
        if (!selected) return;
        originalForeground = GFX.FGAutotiler;
        originalAnimations = GFX.AnimatedTilesBank;
        originalSounds = new Dictionary<char, int>(SurfaceIndex.TileToIndex);
        XmlDocument definitions = Calc.LoadContentXML(Prefix + "ForegroundTiles.xml");
        foreach (XmlElement definition in definitions.GetElementsByTagName("Tileset"))
            if (definition.HasAttr("sound")) SurfaceIndex.TileToIndex[definition.AttrChar("id")] = definition.AttrInt("sound");
        GFX.FGAutotiler = new Autotiler(Prefix + "ForegroundTiles.xml");
        AnimatedTilesBank animations = new AnimatedTilesBank();
        foreach (AnimatedTilesBank.Animation animation in originalAnimations.Animations)
            animations.Add(animation.Name, animation.Delay, animation.Offset, animation.Origin, new List<MTexture>(animation.Frames));
        foreach (XmlElement definition in Calc.LoadContentXML(Prefix + "AnimatedTiles.xml")["Data"])
            animations.Add(definition.Attr("name"), definition.AttrFloat("delay", 0f),
                definition.AttrVector2("posX", "posY", Vector2.Zero), definition.AttrVector2("origX", "origY", Vector2.Zero),
                GFX.Game.GetAtlasSubtextures(definition.Attr("path")));
        GFX.AnimatedTilesBank = animations;
        active = true;
    }
}
