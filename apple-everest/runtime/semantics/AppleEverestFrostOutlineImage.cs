#nullable disable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
namespace Celeste.Mod;

internal sealed class AppleEverestFrostOutlineImage : Component
{
    internal readonly Image Image;
    private readonly Color color;
    private AppleEverestFrostOutlineRenderer renderer;
    internal AppleEverestFrostOutlineImage(Image image, Color color) : base(false, false)
    { Image = image; this.color = color; }
    public override void EntityAwake()
    {
        base.EntityAwake();
        renderer = AppleEverestFrostOutlineRenderer.GetOrCreate(Scene, Entity.Depth, color);
        renderer.Images.Add(this);
    }
    public override void EntityRemoved(Scene scene)
    { base.EntityRemoved(scene); renderer?.Images.Remove(this); renderer = null; }
    public override void Removed(Entity entity)
    { base.Removed(entity); renderer?.Images.Remove(this); renderer = null; }
}

internal sealed class AppleEverestFrostOutlineRenderer : Entity
{
    private static readonly List<AppleEverestFrostOutlineRenderer> pending = new();
    internal readonly List<AppleEverestFrostOutlineImage> Images = new();
    private readonly Color color;
    private VirtualRenderTarget target;
    private AppleEverestFrostOutlineRenderer(int depth, Color color)
    { Depth = depth; this.color = color; Add(new BeforeRenderHook(BeforeRender)); }
    internal static AppleEverestFrostOutlineRenderer GetOrCreate(Scene scene, int depth, Color color)
    {
        // The exact source renderer is untracked. ControllerHelper reuses
        // only pending renderers until their Awake callback removes them.
        foreach (var renderer in pending)
            if (renderer.Depth == depth && renderer.color == color) return renderer;
        var created = new AppleEverestFrostOutlineRenderer(depth, color);
        pending.Add(created);
        scene.Add(created);
        return created;
    }
    public override void Awake(Scene scene) { base.Awake(scene); pending.Remove(this); }
    private void BeforeRender()
    {
        int width = GameplayBuffers.Gameplay.Width, height = GameplayBuffers.Gameplay.Height;
        if (target == null || target.IsDisposed || target.Width != width || target.Height != height)
        {
            target?.Dispose();
            target = VirtualContent.CreateRenderTarget("apple-everest-frost-debris", width, height);
        }
        GraphicsDevice graphics = Draw.SpriteBatch.GraphicsDevice;
        graphics.SetRenderTarget(target);
        graphics.Clear(Color.Transparent);
        GameplayRenderer.Begin();
        foreach (var item in Images) item.Image.Render();
        GameplayRenderer.End();
        graphics.SetRenderTarget(null);
    }
    public override void Render()
    {
        base.Render();
        if (target == null || target.IsDisposed) return;
        Vector2 position = ((Level)Scene).Camera.Position.Floor();
        Draw.SpriteBatch.Draw((RenderTarget2D)target, position - Vector2.UnitY, null, color);
        Draw.SpriteBatch.Draw((RenderTarget2D)target, position + Vector2.UnitY, null, color);
        Draw.SpriteBatch.Draw((RenderTarget2D)target, position - Vector2.UnitX, null, color);
        Draw.SpriteBatch.Draw((RenderTarget2D)target, position + Vector2.UnitX, null, color);
        Draw.SpriteBatch.Draw((RenderTarget2D)target, position, null, Color.White);
    }
    public override void Removed(Scene scene) { base.Removed(scene); DisposeTarget(); }
    public override void SceneEnd(Scene scene) { base.SceneEnd(scene); DisposeTarget(); }
    private void DisposeTarget() { target?.Dispose(); target = null; }
}
