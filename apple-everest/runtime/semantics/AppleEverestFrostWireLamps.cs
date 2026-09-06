#nullable disable
using System;
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;
// FrostHelper 1.80.1; attached=false is enforced before construction.
internal sealed class AppleEverestFrostWireLamps : Entity
{
    internal MTexture LampTexture;

    internal Sprite[] Sprites;

    public float Wobbliness;

    public VertexLight[] lights;

    public Color Color;

    public SimpleCurve Curve;

    private float sineX;

    private float sineY;

    internal AppleEverestFrostWireLamps(EntityData data, Vector2 offset)
        : base(data.Position + offset)
    {
        Vector2 end = data.Nodes[0] + offset;
        Curve = new SimpleCurve(Position, end, Vector2.Zero);
        base.Depth = (data.Bool("above") ? (-8500) : 2000) - 1;
        Random random = new Random((int)Math.Min(Position.X, end.X));
        Color[] colors = new[] { Microsoft.Xna.Framework.Color.White };
        Color = AppleEverestSelectedVisualHelpers.Rgb(data.Attr("wireColor", "595866"));
        sineX = random.NextFloat(4f);
        sineY = random.NextFloat(4f);
        Wobbliness = data.Float("wobbliness", 1f);
        lights = new VertexLight[data.Int("lightCount", 3)];
        float alpha = data.Float("lightAlpha", 1f);
        int startFade = data.Int("lightStartFade", 8);
        int endFade = data.Int("lightEndFade", 16);
        string text = data.Attr("lampSprite", "objects/FrostHelper/wireLamp");
        bool flag = !GFX.Game.Has(text);
        if (flag)
        {
            Sprites = new Sprite[lights.Length];
        }
        else
        {
            LampTexture = GFX.Game[text];
        }
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i] = new VertexLight(colors[random.Next(0, colors.Length)], alpha, startFade, endFade);
            Add(lights[i]);
            if (flag)
            {
                Sprite sprite = new Sprite(GFX.Game, text)
                {
                    Color = lights[i].Color
                };
                sprite.AddLoop("i", "", data.Float("frameDelay", 0.5f));
                sprite.Play("i", restart: false, randomizeFrame: true);
                sprite.CenterOrigin();
                Add(Sprites[i] = sprite);
            }
        }
    }

    public override void Render()
    {
        Level level = base.Scene as Level;
        if (level == null)
        {
            return;
        }
        Vector2 vector = new Vector2((float)Math.Sin(sineX + level.WindSineTimer * 2f), (float)Math.Sin(sineY + level.WindSineTimer * 2.8f)) * 8f;
        Curve.Control = (Curve.Begin + Curve.End) / 2f + new Vector2(0f, 24f) + vector * Wobbliness;
        if (!AppleEverestFrostWireLamps.CurveVisible(Curve, level.Camera))
        {
            return;
        }
        Vector2 start = Curve.Begin;
        for (int i = 1; i <= 16; i++)
        {
            float percent = (float)i / 16f;
            Vector2 point = Curve.GetPoint(percent);
            Draw.Line(start, point, Color);
            start = point;
        }
        for (int j = 1; j <= lights.Length; j++)
        {
            float percent2 = (float)j / ((float)lights.Length + 1f);
            Vector2 point2 = Curve.GetPoint(percent2);
            lights[j - 1].Position = point2 - Position;
            if (LampTexture != null)
            {
                LampTexture.DrawCentered(point2, getColor(j));
            }
            else
            {
                Sprites[j - 1].Position = lights[j - 1].Position;
            }
        }
        base.Render();
    }

    private Color getColor(int i)
    {
        return lights[(i - 1) % lights.Length].Color;
    }
    private static bool CurveVisible(SimpleCurve curve, Camera camera)
    {
        float left = Math.Min(curve.Begin.X, Math.Min(curve.End.X, curve.Control.X));
        float top = Math.Min(curve.Begin.Y, Math.Min(curve.End.Y, curve.Control.Y));
        float right = Math.Max(curve.Begin.X, Math.Max(curve.End.X, curve.Control.X));
        float bottom = Math.Max(curve.Begin.Y, Math.Max(curve.End.Y, curve.Control.Y));
        return right - camera.Left >= -4f && bottom - camera.Top >= -4f &&
            left - camera.Left <= 4f + camera.Viewport.Width * camera.Zoom &&
            top - camera.Top <= 4f + camera.Viewport.Height * camera.Zoom;
    }
}