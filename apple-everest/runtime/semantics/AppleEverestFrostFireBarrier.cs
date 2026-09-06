#nullable disable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;

internal sealed class AppleEverestFrostFireBarrier : Entity
{
    internal AppleEverestFrostFireBarrier(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        Tag = Tags.TransitionUpdate;
        Collider = new Hitbox(data.Width, data.Height);
        Add(new PlayerCollider(player => player.Die((player.Center - Center).SafeNormalize())));
        // Exact guard: isIce=false, ignoreCoreMode=true, silent=true, All
        // surfaces, default two waves and default bubbles. No listener/SFX.
        Add(new AppleEverestFrostLavaRect(data.Width, data.Height, 4, "1")
        {
            SurfaceColor = AppleEverestSelectedVisualHelpers.Rgb(data.Attr("surfaceColor")),
            EdgeColor = AppleEverestSelectedVisualHelpers.Rgb(data.Attr("edgeColor")),
            CenterColor = Color.White,
            Waves = new List<AppleEverestFrostLavaRect.WaveData>
                { new(2f, .25f, 4f), new(1f, .05f, .5f) },
            CurveAmplitude = 1f,
            Fade = 16f
        });
        Depth = -8500;
    }
    public override void Added(Scene scene)
    {
        base.Added(scene);
        scene.Add(new Solid(Position + new Vector2(2f, 3f), Width - 4f, Height - 5f, false));
        Collidable = true;
    }
    public override void Update()
    {
        Visible = Collidable;
        if (!SceneAs<Level>().Transitioning) base.Update();
    }
}

internal static class AppleEverestFrostLavaGeometry
{
    private static VirtualRenderTarget buffer;
    internal static VirtualRenderTarget Buffer()
    {
        int width = GameplayBuffers.Gameplay.Width, height = GameplayBuffers.Gameplay.Height;
        if (buffer == null || buffer.Width != width || buffer.Height != height)
        {
            buffer?.Dispose();
            buffer = VirtualContent.CreateRenderTarget("apple-everest-frost-lava", width, height);
        }
        return buffer;
    }
    internal static void UnloadBuffer() { buffer?.Dispose(); buffer = null; }
    internal static System.Numerics.Vector2 Normal(System.Numerics.Vector2 vector)
    {
        if (vector != System.Numerics.Vector2.Zero) vector = System.Numerics.Vector2.Normalize(vector);
        return new System.Numerics.Vector2(-vector.Y, vector.X);
    }
    internal static bool Visible(Rectangle rectangle, Camera camera)
    {
        float x = rectangle.X - camera.Left, y = rectangle.Y - camera.Top;
        return x + rectangle.Width >= -4f && y + rectangle.Height >= -4f &&
            x <= 4f + camera.Viewport.Width * camera.Zoom && y <= 4f + camera.Viewport.Height * camera.Zoom;
    }
    internal static Rectangle VisibleSection(Rectangle rectangle, Camera camera)
    {
        int left = Math.Max(rectangle.X, (int)camera.Left - 4), top = Math.Max(rectangle.Y, (int)camera.Top - 4);
        int right = Math.Min(rectangle.Right, (int)camera.Right + 4), bottom = Math.Min(rectangle.Bottom, (int)camera.Bottom + 4);
        return new Rectangle(left, top, right - left, bottom - top);
    }
}
