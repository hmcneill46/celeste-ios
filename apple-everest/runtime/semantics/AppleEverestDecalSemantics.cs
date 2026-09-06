#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod;

namespace Celeste;

// The pinned Decal declaration is made partial only in the derived target.
// These methods have typed access to the exact private fields they replace.
public partial class Decal
{
    private readonly List<Solid> appleEverestSolids = new();
    private StaticMover appleEverestStaticMover;
    private float appleEverestHideRange = 32f, appleEverestShowRange = 48f;
    internal MTexture AppleEverestFirstTexture => textures[0];
    internal Vector2 AppleEverestScale => scale;
    internal bool AppleEverestParallax => parallax;
    internal float AppleEverestParallaxAmount => parallaxAmount;

    internal void AppleEverestBeginRegistry() { Remove(image); image = null; }
    internal void AppleEverestEndRegistry() { if (image == null) Add(image = new DecalImage()); }
    internal void AppleEverestRandomizeFrame() => frame = Calc.Random.NextFloat(textures.Count);
    internal void AppleEverestFloaty() => MakeFloaty();
    internal void AppleEverestSetParallax(float amount) { parallax = amount != 0f; parallaxAmount = amount; }
    internal void AppleEverestBanner(float speed, float amplitude, int sliceSize, float increment,
        bool easeDown, float offset, bool onlyIfWindy) =>
        MakeBanner(speed, amplitude * scale.X, sliceSize, increment, easeDown,
            offset * Math.Sign(scale.X) * Math.Abs(scale.Y), onlyIfWindy);
    private float AppleEverestScaledRadius(float radius) => radius * ((Math.Abs(scale.X) + Math.Abs(scale.Y)) / 2f);
    private Vector2 AppleEverestScaledOffset(float x, float y) => new(x * scale.X, y * scale.Y);
    internal void AppleEverestBloom(float x, float y, float alpha, float radius) =>
        Add(new BloomPoint(AppleEverestScaledOffset(x, y), alpha, AppleEverestScaledRadius(radius)));
    internal void AppleEverestLight(float x, float y, string color, float alpha, int startFade, int endFade) =>
        Add(new VertexLight(AppleEverestScaledOffset(x, y), Calc.HexToColor(color), alpha,
            (int)AppleEverestScaledRadius(startFade), (int)AppleEverestScaledRadius(endFade)));
    private void AppleEverestScaleRectangle(ref int x, ref int y, ref int width, ref int height)
    {
        x = (int)(x * Math.Abs(scale.X)); y = (int)(y * Math.Abs(scale.Y));
        width = (int)(width * Math.Abs(scale.X)); height = (int)(height * Math.Abs(scale.Y));
        x = scale.X < 0 ? -x - width : x;
        y = scale.Y < 0 ? -y - height : y;
    }
    internal void AppleEverestSolid(int x, int y, int width, int height, int index)
    {
        AppleEverestScaleRectangle(ref x, ref y, ref width, ref height);
        // All selected solid rules use safe=true, waterfalls=true, priority=0.
        MakeSolid(x, y, width, height, index, true);
    }
    internal void AppleEverestScared(int hideRange, int showRange, string idle, string hidden, string show, string hide)
    {
        Sprite sprite = (Sprite)(image = new Sprite(null, null));
        MTexture[] Frames(string csv) => Calc.ReadCSVIntWithTricks(csv).Select(index => textures[index]).ToArray();
        sprite.AddLoop("hidden", 0.1f, Frames(hidden));
        sprite.Add("return", 0.1f, "idle", Frames(show));
        sprite.AddLoop("idle", 0.1f, Frames(idle));
        sprite.Add("hide", 0.1f, "hidden", Frames(hide));
        sprite.Play("idle", restart: true);
        sprite.Scale = scale;
        sprite.CenterOrigin();
        Add(sprite);
        appleEverestHideRange = (int)AppleEverestScaledRadius(hideRange);
        appleEverestShowRange = (int)AppleEverestScaledRadius(showRange);
        scaredAnimal = true;
    }
    internal void AppleEverestStaticMover(int x, int y, int width, int height)
    {
        AppleEverestScaleRectangle(ref x, ref y, ref width, ref height);
        appleEverestStaticMover = new StaticMover
        {
            SolidChecker = solid => !appleEverestSolids.Contains(solid) &&
                solid.CollideRect(new Rectangle((int)X + x, (int)Y + y, width, height)),
            OnDestroy = () => { RemoveSelf(); appleEverestSolids.ForEach(solid => solid.RemoveSelf()); },
            OnDisable = () =>
            { Active = Visible = Collidable = false; appleEverestSolids.ForEach(solid => solid.Collidable = false); },
            OnEnable = () =>
            { Active = Visible = Collidable = true; appleEverestSolids.ForEach(solid => solid.Collidable = true); },
            OnMove = movement =>
            {
                Position += movement;
                Vector2 liftSpeed = appleEverestStaticMover.Platform.LiftSpeed;
                appleEverestSolids.ForEach(solid =>
                { solid.MoveH(movement.X, liftSpeed.X); solid.MoveV(movement.Y, liftSpeed.Y); });
            },
            OnShake = movement => Position += movement,
            OnAttach = platform =>
            {
                platform.Add(new AppleEverestDecalRemovedListener(() =>
                { RemoveSelf(); appleEverestSolids.ForEach(solid => solid.RemoveSelf()); }));
                AppleEverestCoreModule.Session.AttachedDecals.Add(AppleEverestAttachmentKey());
            }
        };
        // No selected registry StaticMover requests a JumpThru checker.
        Add(appleEverestStaticMover);
    }
    private string AppleEverestAttachmentKey() => string.Format("{0}||{1}||{2}", Name, Position.X, Position.Y);
    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        if (appleEverestStaticMover?.Platform == null &&
            AppleEverestCoreModule.Session.AttachedDecals.Contains(AppleEverestAttachmentKey())) RemoveSelf();
    }
}

internal sealed class AppleEverestDecalRemovedListener : Component
{
    private readonly Action removed;
    internal AppleEverestDecalRemovedListener(Action removed) : base(false, false) { this.removed = removed; }
    public override void EntityRemoved(Scene scene) { base.EntityRemoved(scene); removed?.Invoke(); }
}
