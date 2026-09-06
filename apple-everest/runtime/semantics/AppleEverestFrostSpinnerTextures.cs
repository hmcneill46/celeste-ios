#nullable disable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
namespace Celeste.Mod;

internal static class AppleEverestFrostSpinnerTextures
{
    private static VirtualTexture packed;
    internal static MTexture[] Foreground, Background;
    internal static void Ensure()
    {
        if (packed != null && !packed.IsDisposed) return;
        const string prefix = "danger/FrostHelper/icecrystal/";
        // Exact four release metadata frames. The orphan bg00.meta.yaml does
        // not apply to bg.png, whose logical size remains 11 by 12.
        var offsets = new[] { new Vector2(3, 2), new Vector2(2, 3), new Vector2(4, 3), new Vector2(2, 2) };
        MTexture[] input = new MTexture[5];
        int width = 0, height = 0;
        for (int i = 0; i < 4; i++)
        {
            MTexture raw = GFX.Game[prefix + "fg0" + i];
            input[i] = new MTexture(raw, raw.AtlasPath, new Rectangle(0, 0, raw.Width, raw.Height), offsets[i], 24, 24);
        }
        input[4] = GFX.Game[prefix + "bg"];
        foreach (MTexture texture in input)
        {
            width += texture.Width + (int)Math.Abs(texture.DrawOffset.X);
            height = Math.Max(height, texture.Height);
        }
        GraphicsDevice graphics = Engine.Graphics.GraphicsDevice;
        RenderTarget2D target = new RenderTarget2D(graphics, width, height, false, SurfaceFormat.Color,
            DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        packed?.Dispose();
        packed = new VirtualTexture("apple-everest-frost-icecrystal", width, height, Color.Transparent);
        packed.Texture.Dispose();
        packed.Texture = target;
        MTexture parent = new MTexture(packed);
        MTexture[] result = new MTexture[input.Length];
        graphics.SetRenderTarget(target);
        graphics.Clear(Color.Transparent);
        Draw.SpriteBatch.Begin();
        int x = 0;
        for (int i = 0; i < input.Length; i++)
        {
            input[i].Draw(new Vector2(x, 0), Vector2.Zero);
            result[i] = new MTexture(parent, x, 0, input[i].Width, input[i].Height);
            x += input[i].Width + (int)Math.Abs(input[i].DrawOffset.X);
        }
        Draw.SpriteBatch.End();
        graphics.SetRenderTarget(null);
        Foreground = new[] { result[0], result[1], result[2], result[3] };
        Background = new[] { result[4] };
    }
    internal static void Unload()
    {
        packed?.Dispose();
        packed = null;
        Foreground = Background = null;
    }
}
