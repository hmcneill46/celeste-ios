#nullable disable
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;

// Pandora 1.0.49 replaces these two private colors in Awake without calling
// base.Awake. Keep that exact ordering through a typed vanilla accessor.
internal sealed class AppleEverestColoredBigWaterfall : BigWaterfall
{
    private readonly Color baseColor;
    internal AppleEverestColoredBigWaterfall(EntityData data, Vector2 offset) : base(data, offset)
    { baseColor = AppleEverestPandoraColors.Get(data.Attr("color", "#87CEFA")); }
    public override void Awake(Scene scene) => AppleEverestSetColors(baseColor * 0.8f, baseColor * 0.3f);
}

internal static class AppleEverestPandoraColors
{
    internal static Color Get(string value) => value switch
    {
        "LightCyan" => Color.LightCyan,
        "LightSkyBlue" => Color.LightSkyBlue,
        "SteelBlue" => Color.SteelBlue,
        "GhostWhite" => Color.GhostWhite,
        _ => Calc.HexToColor(value)
    };
}
