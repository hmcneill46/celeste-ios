#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod;

// Exact typed replacements for SJ21_StylegroundMaskRenderer,
// bloomMaskLastStrength and bloomMaskRects. Values belong to one Level or
// one invocation; no arbitrary object attachment or reflective access exists.
internal static class AppleEverestSJMaskRendering
{
    private sealed class Group
    {
        internal readonly string Tag;
        internal readonly bool Foreground;
        internal readonly BackdropRenderer Renderer = new();
        internal VirtualRenderTarget Buffer;
        internal Group(string tag, bool foreground) { Tag = tag; Foreground = foreground; }
    }
    private static readonly List<Group> groups = new();
    private static Level owner;
    private static VirtualRenderTarget bloomBuffer;

    internal static void OnLevelLoaded(Level level)
    {
        if (ReferenceEquals(owner, level)) return;
        UnloadBuffers();
        owner = level;
        Consume(level.Foreground.Backdrops, true);
        Consume(level.Background.Backdrops, false);
    }
    private static void Consume(List<Backdrop> source, bool foreground)
    {
        for (int i = source.Count - 1; i >= 0; i--)
        {
            Backdrop backdrop = source[i];
            string[] tags = backdrop.Tags.Where(tag => tag.StartsWith("sjstylemask_", StringComparison.Ordinal)).ToArray();
            if (tags.Length == 0) continue;
            if (tags.Length != 1) throw new InvalidOperationException("multiple mask tags are outside the frozen slice");
            string tag = tags[0][12..];
            Group group = groups.FirstOrDefault(value => value.Foreground == foreground && value.Tag == tag);
            if (group == null)
            {
                if (groups.Count == 128) throw new InvalidOperationException("styleground mask group bound exceeded");
                groups.Add(group = new Group(tag, foreground));
            }
            group.Renderer.Backdrops.Insert(0, backdrop);
            backdrop.Renderer = group.Renderer;
            source.RemoveAt(i);
        }
    }
    // Source AllInOne.Added enqueues derived masks after the authored entities.
    // Tracker order is independent of EntityList's rendering-depth sort.
    private static AppleEverestSJMaskEntity[] Masks(Level level) => level.Entities.OfType<AppleEverestSJMaskEntity>()
        .OrderBy(mask => mask.DerivedMask).ThenBy(mask => mask.AdditionOrder).ToArray();

    internal static void Render(Level level, bool foreground, bool behind = true)
    {
        AppleEverestSJMaskEntity[] masks = Masks(level);
        // Every selected foreground mask is behind the foreground. The later
        // pass must not update/render those same backdrops a second time.
        if (foreground && !masks.Any(mask => mask.BehindForeground == behind)) return;
        GraphicsDevice graphics = Engine.Graphics.GraphicsDevice;
        RenderTargetBinding[] targets = graphics.GetRenderTargets();
        foreach (Group group in groups.Where(group => group.Foreground == foreground))
        {
            if (!masks.Any(mask => mask.RenderTags.Contains(group.Tag) &&
                new Rectangle((int)mask.X, (int)mask.Y, (int)mask.Width, (int)mask.Height).Intersects(
                    new Rectangle((int)level.Camera.X, (int)level.Camera.Y, 320, 180)))) continue;
            if (!level.Paused) group.Renderer.Update(level);
            group.Renderer.BeforeRender(level);
            group.Buffer ??= VirtualContent.CreateRenderTarget("static-stylemask-" + group.Tag, 320, 180);
            graphics.SetRenderTarget(group.Buffer);
            graphics.Clear(Color.Transparent);
            group.Renderer.Render(level);
        }
        graphics.SetRenderTargets(targets);
        Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, level.Camera.Matrix);
        foreach (AppleEverestSJMaskEntity mask in masks.Where(mask => !foreground || mask.BehindForeground == behind))
            foreach (string tag in mask.RenderTags)
            {
                Group group = groups.FirstOrDefault(value => value.Tag == tag && value.Foreground == foreground);
                if (group?.Buffer == null) continue;
                foreach (var slice in mask.Slices(level))
                    Draw.SpriteBatch.Draw((RenderTarget2D)group.Buffer, slice.Position, slice.Source,
                        Color.White * slice.Value(mask.AlphaFrom, mask.AlphaTo));
            }
        Draw.SpriteBatch.End();
    }

    internal static float BeginBloom(BloomRenderer renderer, Scene scene)
    {
        float previous = renderer.Strength;
        if (scene is Level level && Masks(level).Any(mask => mask.HasBloom)) renderer.Strength = 1f;
        return previous;
    }

    internal static List<Rectangle> RenderBloom(BloomRenderer renderer, VirtualRenderTarget target,
        Scene scene, Texture2D blurred, float strength)
    {
        List<Rectangle> rectangles = new();
        if (scene is not Level level) return rectangles;
        GraphicsDevice graphics = Engine.Graphics.GraphicsDevice;
        RenderTargetBinding[] targets = graphics.GetRenderTargets();
        foreach (AppleEverestSJMaskEntity mask in Masks(level).Where(mask => mask.HasBloom))
        {
            bloomBuffer ??= VirtualContent.CreateRenderTarget("static-bloom-mask", 320, 180);
            var slices = mask.Slices(level).ToArray();
            graphics.SetRenderTarget(bloomBuffer);
            graphics.Clear(Color.Transparent);
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, level.Camera.Matrix);
            Draw.SpriteBatch.Draw((RenderTarget2D)GameplayBuffers.TempA,
                Vector2.Transform(Vector2.Zero, -level.Camera.Matrix), Color.White);
            foreach (var slice in slices)
                Draw.Rect(slice.Position.X, slice.Position.Y, slice.Source.Width, slice.Source.Height,
                    Color.White * slice.Value(mask.BaseFrom >= 0f ? mask.BaseFrom : renderer.Base,
                        mask.BaseTo >= 0f ? mask.BaseTo : renderer.Base));
            Draw.SpriteBatch.End();
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BloomRenderer.BlurredScreenToMask);
            Draw.SpriteBatch.Draw(blurred, Vector2.Zero, Color.White);
            Draw.SpriteBatch.End();
            graphics.SetRenderTarget(target);
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BloomRenderer.AdditiveMaskToScreen, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, level.Camera.Matrix);
            foreach (var slice in slices)
            {
                float amount = slice.Value(mask.StrengthFrom >= 0f ? mask.StrengthFrom : strength,
                    mask.StrengthTo >= 0f ? mask.StrengthTo : strength);
                for (int i = 0; i < amount; i++)
                    Draw.SpriteBatch.Draw((RenderTarget2D)bloomBuffer, slice.Position, slice.Source,
                        Color.White * (i < amount - 1f ? 1f : amount - i));
                rectangles.Add(new Rectangle((int)slice.Position.X, (int)slice.Position.Y, slice.Source.Width, slice.Source.Height));
            }
            Draw.SpriteBatch.End();
        }
        graphics.SetRenderTargets(targets);
        return rectangles;
    }

    internal static void ClearBloom(BloomRenderer renderer, Scene scene, float strength, List<Rectangle> rectangles)
    {
        if (rectangles.Count > 0)
        {
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, ((Level)scene).Camera.Matrix);
            foreach (Rectangle rectangle in rectangles) Draw.Rect(rectangle, Color.Transparent);
            Draw.SpriteBatch.End();
        }
        renderer.Strength = strength;
    }
    internal static void UnloadBuffers()
    {
        foreach (Group group in groups) group.Buffer?.Dispose();
        groups.Clear();
        bloomBuffer?.Dispose();
        bloomBuffer = null;
        owner = null;
    }
}
