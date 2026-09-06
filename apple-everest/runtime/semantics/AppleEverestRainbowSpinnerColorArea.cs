#nullable disable
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;

// MaxHelpingHand 1.40.9, selected unflagged area. The authored guard excludes
// the flag-dependent palette and its spinner refresh branch.
internal sealed class AppleEverestRainbowSpinnerColorArea : Entity
{
    private static bool enabled;
    private readonly Color[] colors;
    private readonly float gradientSize, gradientSpeed;
    private readonly Vector2 center;
    private readonly bool loopColors;

    internal AppleEverestRainbowSpinnerColorArea(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        gradientSize = data.Float("gradientSize", 280f);
        gradientSpeed = data.Float("gradientSpeed", 50f);
        center = new Vector2(data.Float("centerX"), data.Float("centerY"));
        loopColors = data.Bool("loopColors");
        Color[] parsed = data.Attr("colors", "89E5AE,88E0E0,87A9DD,9887DB,D088E2").Split(',')
            .Select(value => Calc.HexToColor(value)).ToArray();
        colors = loopColors ? parsed.Concat(new[] { parsed[0] }).ToArray() : parsed;
        Collider = new Hitbox(data.Width, data.Height);
    }
    public override void Awake(Scene scene) { base.Awake(scene); enabled = true; }
    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        if (scene.Tracker.CountEntities<AppleEverestRainbowSpinnerColorArea>() <= 1) enabled = false;
    }
    public override void SceneEnd(Scene scene) { base.SceneEnd(scene); enabled = false; }

    internal static bool TryHue(CrystalStaticSpinner spinner, Vector2 position, out Color color)
    {
        color = default(Color);
        if (!enabled) return false;
        AppleEverestRainbowSpinnerColorArea area = spinner.CollideFirst<AppleEverestRainbowSpinnerColorArea>(position);
        if (area == null) return false;
        color = area.Hue(spinner.Scene, position);
        return true;
    }

    private Color Hue(Scene scene, Vector2 position)
    {
        if (colors.Length == 1) return colors[0];
        float amount = (position - center).Length() + scene.TimeActive * gradientSpeed;
        while (amount < 0f) amount += gradientSize;
        amount = amount % gradientSize / gradientSize;
        if (!loopColors) amount = Calc.YoYo(amount);
        if (amount == 1f) return colors[colors.Length - 1];
        float index = (colors.Length - 1) * amount;
        int first = (int)index;
        return Color.Lerp(colors[first], colors[first + 1], index - first);
    }
}
