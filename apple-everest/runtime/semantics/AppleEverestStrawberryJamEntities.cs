#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestSJMaskEntity : Entity
{
    internal enum FadeKind { None, LeftToRight, RightToLeft, BottomToTop }
    internal readonly FadeKind Fade;
    internal readonly string[] RenderTags;
    internal readonly float AlphaFrom, AlphaTo;
    internal readonly bool HasBloom;
    internal readonly bool DerivedMask;
    internal readonly bool BehindForeground;
    private static long nextAdditionOrder;
    internal long AdditionOrder;
    internal readonly float BaseFrom, BaseTo, StrengthFrom, StrengthTo;
    internal readonly record struct Slice(Vector2 Position, Rectangle Source, float Amount)
    {
        internal float Value(float from, float to) => Calc.LerpClamp(from, to, Amount);
    }

    internal AppleEverestSJMaskEntity(string kind, EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        Collider = new Hitbox(data.Width, data.Height);
        Depth = 2000000;
        Active = false;
        if (data.Attr("flag", "") != "" || data.Bool("notFlag", false) ||
            data.Float("scrollX", 0f) != 0f || data.Float("scrollY", 0f) != 0f ||
            data.Bool("entityRenderer", false))
            throw new InvalidOperationException("mask is outside the frozen Beginner semantics");
        Fade = data.Attr("fade", "None") switch
        {
            "None" => FadeKind.None,
            "LeftToRight" => FadeKind.LeftToRight,
            "RightToLeft" => FadeKind.RightToLeft,
            "BottomToTop" => FadeKind.BottomToTop,
            _ => throw new InvalidOperationException("mask fade is outside the frozen Beginner semantics")
        };
        bool all = kind == "all-in-one";
        DerivedMask = all;
        bool style = all || kind == "styleground";
        if (all && (data.Attr("colorGradeFrom", "(current)") != "(current)" ||
            data.Attr("colorGradeTo", "(current)") != "(current)" ||
            data.Float("lightingFrom", -1f) >= 0f || data.Float("lightingTo", -1f) >= 0f))
            throw new InvalidOperationException("colour-grade and lighting masks are outside the frozen slice");
        if (style && !data.Bool(all ? "styleBehindFg" : "behindFg", all))
            throw new InvalidOperationException("foreground-after masks are outside the frozen slice");
        BehindForeground = data.Bool(all ? "styleBehindFg" : "behindFg", all);
        RenderTags = style ? data.Attr(all ? "stylemaskTag" : "tag", "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
        AlphaFrom = data.Float(all ? "styleAlphaFrom" : "alphaFrom", 0f);
        AlphaTo = data.Float(all ? "styleAlphaTo" : "alphaTo", 1f);
        BaseFrom = data.Float(all ? "bloomBaseFrom" : "baseFrom", -1f);
        BaseTo = data.Float(all ? "bloomBaseTo" : "baseTo", -1f);
        StrengthFrom = data.Float(all ? "bloomStrengthFrom" : "strengthFrom", -1f);
        StrengthTo = data.Float(all ? "bloomStrengthTo" : "strengthTo", -1f);
        HasBloom = kind == "bloom" || all &&
            (BaseFrom >= 0f || BaseTo >= 0f || StrengthFrom >= 0f || StrengthTo >= 0f);
    }

    public override void Added(Scene scene)
    { base.Added(scene); AdditionOrder = nextAdditionOrder++; }

    internal Rectangle VisibleRect(Level level) => Rectangle.Intersect(new Rectangle(0, 0, 320, 180),
        new Rectangle((int)(X - level.Camera.X), (int)(Y - level.Camera.Y), (int)Width, (int)Height));

    internal IEnumerable<Slice> Slices(Level level)
    {
        Rectangle visible = VisibleRect(level);
        if (visible.Width <= 0 || visible.Height <= 0) yield break;
        Vector2 draw = new(Math.Max(X, level.Camera.X), Math.Max(Y, level.Camera.Y));
        Vector2 skipped = new(Math.Max(0f, level.Camera.X - X), Math.Max(0f, level.Camera.Y - Y));
        if (Fade == FadeKind.None) { yield return new Slice(draw, visible, 1f); yield break; }
        if (Fade == FadeKind.BottomToTop)
        {
            for (int y = (int)skipped.Y; y < Height && y - (int)skipped.Y < visible.Height; y++)
                yield return new Slice(Position + new Vector2(skipped.X, y),
                    new Rectangle(visible.X, visible.Y + y - (int)skipped.Y, visible.Width, 1), 1f - y / Height);
        }
        else
        {
            for (int x = (int)skipped.X; x < Width && x - (int)skipped.X < visible.Width; x++)
                yield return new Slice(Position + new Vector2(x, skipped.Y),
                    new Rectangle(visible.X + x - (int)skipped.X, visible.Y, 1, visible.Height),
                    Fade == FadeKind.LeftToRight ? x / Width : 1f - x / Width);
        }
    }
}

// The one authored controller has four empty name lists; no Entity type can
// match the empty string. The global orphan-light repair is a separate static
// source edit in LightingRenderer.BeforeRender, not a reflection scan.
internal sealed class AppleEverestSJGlowController : Entity
{
    internal AppleEverestSJGlowController(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        foreach (string name in new[] { "lightWhitelist", "lightBlacklist", "bloomWhitelist", "bloomBlacklist" })
            if (data.Attr(name, "") != "") throw new InvalidOperationException("nonempty glow lists are outside the frozen slice");
        Active = Visible = false;
    }
}

internal sealed class AppleEverestSJJamJar : Entity
{
    private readonly string map;
    private readonly string returnMode;
    private readonly bool allowSaving;
    internal static string SelectAnimation(AppleEverestStrawberryJamSaveData save, string sid, bool complete) =>
        !complete ? "empty" : save.FilledJamJarSIDs.Add(sid) ? "before_fill" : "full";

    internal AppleEverestSJJamJar(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        map = data.Attr("map", "");
        returnMode = data.Attr("returnToLobbyMode", "");
        allowSaving = data.Bool("allowSaving", false);
        if (data.Attr("sprite", "") != "beginner" || returnMode != "SetReturnToHere" || !allowSaving)
            throw new InvalidOperationException("jam jar is outside the frozen Beginner semantics");
        bool complete = AppleEverestProgressionRuntime.TryDescriptor(map, out var descriptor) &&
            AppleEverestProgressionRuntime.Completed(new AreaKey(descriptor.RuntimeAreaId), SaveData.Instance);
        string animation = SelectAnimation(AppleEverestStrawberryJamModule.Instance.Save, map, complete);
        Sprite sprite = AppleEverestStrawberryJamModule.Instance.SpriteBank.Create("jamJar_beginner");
        sprite.Play(animation);
        Add(sprite);
        if (animation == "before_fill") sprite.OnChange = (_, current) =>
        {
            if (current != "fill") return;
            SoundSource sound = new(new Vector2(0f, -20f), "event:/sj21_jamjar-blue") { RemoveOnOneshotEnd = true };
            sound.instance.setVolume(0.3f);
            Add(sound);
        };
        Depth = 1000;
    }
    public override void Added(Scene scene)
    {
        base.Added(scene);
        EntityData panel = new()
        {
            Position = Position - new Vector2(24f, 32f), Width = 48, Height = 32,
            Nodes = new[] { Position - new Vector2(0f, 32f) },
            Values = new Dictionary<string, object>
            {
                ["map"] = map, ["returnToLobbyMode"] = returnMode, ["allowSaving"] = allowSaving
            }
        };
        scene.Add(new AppleEverestChapterPanelTrigger(panel, Vector2.Zero));
    }
}
