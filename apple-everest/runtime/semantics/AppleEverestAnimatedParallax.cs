#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Monocle;

namespace Celeste.Mod;

// Static constructor substitution for MaxHelpingHand's selected ordinary
// animated parallaxes. Metadata/HD profiles require a separate host closure.
internal sealed class AppleEverestAnimatedParallax : Parallax
{
    private readonly List<MTexture> frames;
    private readonly float frameDuration;
    private int currentFrame;
    private float timer;

    internal static Parallax Create(MTexture texture) => texture.AtlasPath?.StartsWith(
        "bgs/MaxHelpingHand/animatedParallax/", StringComparison.Ordinal) == true
        ? new AppleEverestAnimatedParallax(texture) : new Parallax(texture);

    private AppleEverestAnimatedParallax(MTexture texture) : base(texture)
    {
        string key = Regex.Replace(texture.AtlasPath, "[0-9]+$", "");
        frames = GFX.Game.GetAtlasSubtextures(key);
        if (frames.Count is < 1 or > 256)
            throw new InvalidOperationException("Animated parallax frames exceed the static profile.");
        Match match = Regex.Match(key, "[^0-9]((?:[0-9]+\\.)?[0-9]+)fps$");
        float fps = match.Success ? float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 12f;
        if (!float.IsFinite(fps) || fps <= 0 || fps > 120)
            throw new InvalidOperationException("Animated parallax FPS exceeds the static profile.");
        frameDuration = 1f / fps;
        timer = frameDuration;
        Texture = frames[0];
    }

    public override void Update(Scene scene)
    {
        base.Update(scene);
        if (!IsVisible(scene as Level)) return;
        timer -= Engine.DeltaTime;
        // Preserve the pinned single-step, strictly-less-than-zero behavior.
        if (timer < 0f)
        {
            timer += frameDuration;
            currentFrame = (currentFrame + 1) % frames.Count;
            Texture = frames[currentFrame];
        }
    }
}
