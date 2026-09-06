#nullable disable
// Selected static implementation reviewed against FemtoHelper 1.15.22.
// Exact ZIP/DLL identities and authored guard profiles are bound by the host registry.
using System;
using System.Collections.Generic;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestFemtoWaterfall : Entity
{
    private enum Layers
    {
        Fg,
        Bg
    }

    private enum DisplacementType
    {
        None,
        Vanilla,
        Custom
    }

    private readonly Layers layer;

    private readonly DisplacementType displacementType;

    private readonly float width;

    private readonly float height;

    private readonly float parallax;

    private readonly float fallSpeedMultiplier;

    private readonly List<float> lines = new List<float>();

    private readonly Color surfaceColor;

    private readonly Color fillColor;

    private float sine;

    private readonly SoundSource loopingSfx;

    private float fade;

    private readonly bool smooth;

    public bool UseDisplacement => displacementType != DisplacementType.None;

    private Vector2 RenderPosition => RenderPositionAtCamera((base.Scene as Level).Camera.Position + new Vector2(160f, 90f));

    public AppleEverestFemtoWaterfall(EntityData data, Vector2 offset)
        : base(data.Position + offset)
    {
        base.Tag = Tags.TransitionUpdate;
        layer = data.Enum("layer", Layers.Bg);
        width = data.Width;
        height = data.Height;
        fallSpeedMultiplier = data.Float("fallSpeedMultiplier");
        float num = data.Float("surfaceOpacity");
        float num2 = data.Float("fillOpacity");
        base.Depth = data.Int("depth", (layer == Layers.Fg) ? (-49900) : 10010);
        smooth = data.Bool("smooth", layer == Layers.Fg);
        if (!data.Bool("silent", layer == Layers.Bg))
        {
            Add(loopingSfx = new SoundSource());
            loopingSfx.Play("event:/env/local/waterfall_big_main");
        }
        displacementType = data.Enum("displacementType", (layer == Layers.Fg) ? DisplacementType.Vanilla : DisplacementType.None);
        if (UseDisplacement)
        {
            Add(new DisplacementRenderHook(RenderDisplacement));
        }
        if (displacementType == DisplacementType.Vanilla)
        {
            lines.Add(3f);
            lines.Add(width - 4f);
        }
        else
        {
            lines.Add(6f);
            lines.Add(width - 7f);
        }
        parallax = data.Float("parallax");
        surfaceColor = Calc.HexToColor(data.Attr("surfaceColor")) * num;
        fillColor = Calc.HexToColor(data.Attr("fillColor")) * num2;
        fade = 1f;
        Add(new TransitionListener
        {
            OnIn = delegate(float f)
            {
                fade = f;
            },
            OnOut = delegate(float f)
            {
                fade = 1f - f;
            }
        });
        if (width > 16f)
        {
            int num3 = Calc.Random.Next((int)(width / 16f));
            for (int num4 = 0; num4 < num3; num4++)
            {
                lines.Add(8f + Calc.Random.NextFloat(width - 16f));
            }
        }
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        if ((base.Scene as Level).Transitioning)
        {
            fade = 0f;
        }
    }

    public Vector2 RenderPositionAtCamera(Vector2 camera)
    {
        Vector2 vector = Position + new Vector2(width, height) / 2f - camera;
        Vector2 zero = Vector2.Zero;
        zero -= vector * (1f - parallax);
        return Position + zero;
    }

    public void RenderDisplacement()
    {
        float x = RenderPosition.X;
        if (!AppleEverestSelectedVisualHelpers.IsRectangleVisible(x, base.Y, width, height))
        {
            return;
        }
        if (displacementType == DisplacementType.Vanilla)
        {
            Draw.Rect(x, base.Y, width, height, new Color(0.5f, 0.5f, 1f, 1f));
        }
        else if (displacementType != DisplacementType.None)
        {
            Vector2 position = (base.Scene as Level).Camera.Position;
            int num = (smooth ? 1 : 3);
            float num2 = Math.Max(base.Y, (float)Math.Floor(position.Y / (float)num) * (float)num);
            float num3 = Math.Min(base.Y + height, position.Y + 180f);
            for (float num4 = num2; num4 < num3; num4 += (float)num)
            {
                int num5 = (int)(Math.Sin(num4 / 6f - sine * 8f) * 2.0);
                Draw.Rect(x + 1f, num4, width - 2f, num, new Color(0.5f + (float)num5 / 32f, 0.5f, 0f, 1f));
            }
        }
    }

    public override void Update()
    {
        sine += Engine.DeltaTime * fallSpeedMultiplier;
        if (loopingSfx != null)
        {
            Vector2 position = (base.Scene as Level).Camera.Position;
            loopingSfx.Position = new Vector2(RenderPosition.X - base.X, Calc.Clamp(position.Y + 90f, base.Y, height) - base.Y);
        }
        base.Update();
    }

    public override void Render()
    {
        float x = RenderPosition.X;
        if (!AppleEverestSelectedVisualHelpers.IsRectangleVisible(x, base.Y, width, height))
        {
            return;
        }
        Color color = fillColor * fade;
        Color color2 = surfaceColor * fade;
        Draw.Rect(x, base.Y, width, height, color);
        if (UseDisplacement)
        {
            float num = ((displacementType == DisplacementType.Custom) ? 0f : 1f);
            Draw.Rect(x - num, base.Y, 3f, height, color2);
            Draw.Rect(x + (width - 3f) + num, base.Y, 3f, height, color2);
            {
                foreach (float line in lines)
                {
                    Draw.Rect(x + line, base.Y, 1f, height, color2);
                }
                return;
            }
        }
        Vector2 position = (base.Scene as Level).Camera.Position;
        int num2 = (smooth ? 1 : 3);
        float num3 = Math.Max(base.Y, (float)Math.Floor(position.Y / (float)num2) * (float)num2);
        float num4 = Math.Min(base.Y + height, position.Y + 180f);
        for (float num5 = num3; num5 < num4; num5 += (float)num2)
        {
            int num6 = (int)(Math.Sin(num5 / 6f - sine * 8f) * 2.0);
            Draw.Rect(x, num5, 4 + num6, num2, color2);
            Draw.Rect(x + width - 4f + (float)num6, num5, 4 - num6, num2, color2);
            foreach (float line2 in lines)
            {
                Draw.Rect(x + (float)num6 + line2, num5, 1f, num2, color2);
            }
        }
    }
}
