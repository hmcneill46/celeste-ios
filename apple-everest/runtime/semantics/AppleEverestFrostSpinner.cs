#nullable disable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
namespace Celeste.Mod;

internal sealed class AppleEverestFrostSpinner : Entity
{
    internal readonly struct Fill
    {
        internal readonly MTexture Texture;
        internal readonly Color Color;
        internal readonly Vector2 Position;
        internal readonly float Rotation;
        internal Fill(MTexture texture, Color color, Vector2 position, float rotation)
        { Texture = texture; Color = color; Position = position; Rotation = rotation; }
    }
    internal readonly List<Image> Images = new();
    internal readonly List<Fill> Fills = new();
    private readonly int id, randomSeed;
    private readonly float intervalOffset;
    private readonly Color tint, destroyColor;
    private bool expanded, registered;
    private int lastDepth;

    internal AppleEverestFrostSpinner(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        id = data.ID;
        tint = Calc.HexToColor(data.Attr("tint", "ffffff"));
        destroyColor = Calc.HexToColor(data.Attr("destroyColor", "639bff"));
        Calc.Random.Next(); // The source deliberately consumes this unused value.
        intervalOffset = Calc.Random.NextFloat();
        Tag = Tags.TransitionUpdate;
        Collider = new AppleEverestFrostSpinnerCollider();
        Add(new PlayerCollider(player => player.Die((player.Position - Position).SafeNormalize())));
        Add(new HoldableCollider(holdable => holdable.HitSpinner(this)));
        Add(new LedgeBlocker());
        Depth = lastDepth = data.Int("depth", -8500);
        randomSeed = Calc.Random.Next();
        Visible = false;
        AppleEverestFrostSpinnerTextures.Ensure();
    }
    public override void Added(Scene scene)
    {
        base.Added(scene);
        AppleEverestFactoryCanary.Successor(this, AppleEverestFrostSpinnerRenderer.GetOrCreate(scene, Depth));
    }
    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        if (InView()) CreateSprites();
    }
    private bool InView()
    {
        Camera camera = ((Level)Scene).Camera;
        // Retain the source's float operation order and strict comparisons.
        Vector2 relative = Position - camera.Position;
        relative += new Vector2(16f);
        if ((relative.X <= 0f) | (relative.Y <= 0f)) return false;
        relative -= new Vector2(camera.Viewport.Width, camera.Viewport.Height);
        relative -= new Vector2(16f);
        relative -= new Vector2(16f);
        return (relative.X < 0f) & (relative.Y < 0f);
    }
    public override void Update()
    {
        // Source intentionally skips Entity.Update; collision components are
        // consumed by their ordinary Player/Holdable tracker paths.
        if (!Visible)
        {
            Collidable = false;
            if (InView())
            {
                Register();
                Visible = true;
                if (!expanded) CreateSprites();
            }
        }
        else
        {
            if (lastDepth != Depth)
            {
                lastDepth = Depth;
                Unregister();
                AppleEverestFrostSpinnerRenderer.GetOrCreate(Scene, Depth);
                Register();
                Scene.OnEndOfFrame += () => Scene.Entities.UpdateLists();
            }
            if (Scene.OnInterval(0.25f, intervalOffset) && !InView())
            {
                Visible = false;
                Unregister();
            }
            if (Scene.OnInterval(0.05f, intervalOffset))
            {
                Player player = Scene.Tracker.GetEntity<Player>();
                if (player != null)
                    Collidable = Math.Abs(player.X - X) < 128f && Math.Abs(player.Y - Y) < 128f;
            }
        }
    }
    // All selected outlines are black; the group renderer draws both the
    // foreground and connectors. The original optimized Render is empty.
    public override void Render() { }
    private void Register()
    {
        if (registered) return;
        AppleEverestFrostSpinnerRenderer.Find(Scene, Depth)?.Spinners.Add(this);
        registered = true;
    }
    private void Unregister(Scene scene = null)
    {
        if (!registered) return;
        AppleEverestFrostSpinnerRenderer.Find(scene ?? Scene, Depth)?.Spinners.Remove(this);
        registered = false;
    }
    private void AddImage(Image image)
    {
        Images.Add(image);
        image.AppleEverestBindEntity(this);
        image.Color = tint;
        image.Scale = Vector2.One;
        image.Active = false;
    }
    private void CreateSprites()
    {
        if (expanded) return;
        Unregister();
        Register();
        Calc.PushRandom(randomSeed);
        MTexture texture = Calc.Random.Choose(AppleEverestFrostSpinnerTextures.Foreground);
        foreach (AppleEverestFrostSpinner other in Scene.Tracker.GetEntities<AppleEverestFrostSpinner>())
            if (other.id > id && (other.Position - Position).LengthSquared() < 576f)
                AddFill((Position + other.Position) / 2f - Position);
        bool topLeft = !SolidCheck(new Vector2(X - 4f, Y - 4f));
        bool topRight = !SolidCheck(new Vector2(X + 4f, Y - 4f));
        bool bottomLeft = !SolidCheck(new Vector2(X + 4f, Y + 4f));
        bool bottomRight = !SolidCheck(new Vector2(X - 4f, Y + 4f));
        if (topLeft && topRight && bottomLeft && bottomRight) AddImage(new Image(texture).CenterOrigin());
        else
        {
            int halfWidth = texture.Width / 2, halfHeight = texture.Height / 2;
            if (topLeft & topRight) Add(0, 0, texture.Width + 4, halfHeight + 2, halfWidth, halfHeight);
            else
            {
                if (topLeft) Add(0, 0, halfWidth + 2, halfHeight + 2, halfWidth, halfHeight);
                if (topRight) Add(halfWidth - 2, 0, halfWidth + 2, halfHeight + 2, 2f, halfHeight);
            }
            if (bottomLeft & bottomRight) Add(0, halfHeight - 2, texture.Width + 4, halfHeight + 2, halfWidth, 2f);
            else
            {
                if (bottomLeft) Add(halfWidth - 2, halfHeight - 2, halfWidth + 2, halfHeight + 2, 2f, 2f);
                if (bottomRight) Add(0, halfHeight - 2, halfWidth + 2, halfHeight + 2, halfWidth, 2f);
            }
            void Add(int x, int y, int width, int height, float originX, float originY) =>
                AddImage(new Image(texture.GetSubtexture(x, y, width, height)).SetOrigin(originX, originY));
        }
        expanded = true;
        Calc.PopRandom();
    }
    private bool SolidCheck(Vector2 position)
    {
        foreach (SolidTiles tiles in Scene.CollideAll<SolidTiles>(position))
            if (tiles.Depth <= Depth) return true;
        return false;
    }
    private void AddFill(Vector2 offset)
    {
        offset = offset.Floor();
        MTexture texture = Calc.Random.Choose(AppleEverestFrostSpinnerTextures.Background);
        float rotation = Calc.Random.Choose(0, 1, 2, 3) * ((float)Math.PI / 2f);
        // Source converts the already-radian rotation to radians again.
        Vector2 rotated = texture.Center.Rotate(rotation * ((float)Math.PI / 180f));
        Vector2 position = offset.Round() + rotated - rotated.Round();
        Fills.Add(new Fill(texture, tint, position, rotation));
    }
    public override void Removed(Scene scene) { Unregister(scene); base.Removed(scene); }
    public override void SceneEnd(Scene scene) { base.SceneEnd(scene); Unregister(scene); }
    internal void Destroy(bool boss = false)
    {
        if (InView())
        {
            Audio.Play("event:/game/06_reflection/fall_spike_smash", Position);
            AppleEverestFrostCrystalDebris.Burst(Position, destroyColor, boss, 8);
        }
        RemoveSelf();
    }
    internal static void BeforeSummitReturn(Player player)
    {
        var spinner = player.Scene.CollideFirst<AppleEverestFrostSpinner>(new Rectangle((int)(player.X - 4f), (int)(player.Y - 40f), 8, 12));
        if (spinner == null) return;
        spinner.Destroy();
        ((Level)player.Scene).Shake();
        Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);
        global::Celeste.Celeste.Freeze(0.01f);
    }
    internal static void BeforeCrystalShatter(Trigger trigger, bool all)
    {
        List<Entity> spinners = trigger.Scene.Tracker.GetEntities<AppleEverestFrostSpinner>();
        if (spinners.Count == 0) return;
        if (all) Audio.Play("event:/game/06_reflection/boss_spikes_burst");
        foreach (AppleEverestFrostSpinner spinner in spinners)
            if (all || trigger.CollideCheck(spinner)) spinner.Destroy();
    }
}

internal sealed class AppleEverestFrostSpinnerRenderer : Entity
{
    private static readonly List<AppleEverestFrostSpinnerRenderer> pending = new();
    internal readonly List<AppleEverestFrostSpinner> Spinners = new();
    private readonly int baseDepth;
    private AppleEverestFrostSpinnerRenderer(int depth) { baseDepth = depth; Depth = depth + 2; Tag = Tags.Persistent; }
    internal static AppleEverestFrostSpinnerRenderer Find(Scene scene, int depth)
    {
        foreach (AppleEverestFrostSpinnerRenderer renderer in scene.Tracker.GetEntities<AppleEverestFrostSpinnerRenderer>())
            if (renderer.baseDepth == depth) return renderer;
        foreach (var renderer in pending) if (renderer.baseDepth == depth) return renderer;
        return null;
    }
    internal static AppleEverestFrostSpinnerRenderer GetOrCreate(Scene scene, int depth)
    {
        var renderer = Find(scene, depth);
        if (renderer != null) return renderer;
        renderer = new(depth);
        pending.Add(renderer);
        scene.Add(renderer);
        return renderer;
    }
    public override void Awake(Scene scene) { base.Awake(scene); pending.Remove(this); }
    public override void Render()
    {
        if (Spinners.Count == 0) return;
        GameplayRenderer.End();
        VirtualRenderTarget target = AppleEverestFrostLavaGeometry.Buffer();
        GraphicsDevice graphics = Engine.Instance.GraphicsDevice;
        graphics.SetRenderTarget(target);
        graphics.Clear(Color.Transparent);
        GameplayRenderer.Begin();
        // Connector and border registrations have identical insertion/removal
        // order in the selected black-outline profile; preserve both passes.
        foreach (var spinner in Spinners)
            if (spinner.Visible && spinner.Fills.Count != 0)
            {
                MTexture texture = spinner.Fills[0].Texture;
                foreach (var fill in spinner.Fills)
                    Draw.SpriteBatch.Draw(texture.Texture.Texture, fill.Position + spinner.Position,
                        texture.ClipRect, fill.Color, fill.Rotation, texture.Center - texture.DrawOffset,
                        1f, SpriteEffects.None, 0f);
            }
        foreach (var spinner in Spinners)
            foreach (Image image in spinner.Images) image.Render();
        GameplayRenderer.End();
        graphics.SetRenderTarget(GameplayBuffers.Gameplay);
        GameplayRenderer.Begin();
        Vector2 position = ((Level)Scene).Camera.Position.Floor();
        Draw.SpriteBatch.Draw((RenderTarget2D)target, position - Vector2.UnitY, null, Color.Black);
        Draw.SpriteBatch.Draw((RenderTarget2D)target, position + Vector2.UnitY, null, Color.Black);
        Draw.SpriteBatch.Draw((RenderTarget2D)target, position - Vector2.UnitX, null, Color.Black);
        Draw.SpriteBatch.Draw((RenderTarget2D)target, position + Vector2.UnitX, null, Color.Black);
        Draw.SpriteBatch.Draw((RenderTarget2D)target, position, null, Color.White);
        GameplayRenderer.End();
        GameplayRenderer.Begin();
    }
}
