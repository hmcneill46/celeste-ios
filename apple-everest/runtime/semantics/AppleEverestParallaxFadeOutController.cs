#nullable disable
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;

internal sealed class AppleEverestParallaxFadeOutController : Entity
{
    private static bool enabled;
    internal AppleEverestParallaxFadeOutController(EntityData data, Vector2 offset)
        : base(data.Position + offset) { }
    public override void Awake(Scene scene) { base.Awake(scene); enabled = true; }
    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        if (scene.Tracker.CountEntities<AppleEverestParallaxFadeOutController>() <= 1) enabled = false;
    }
    public override void SceneEnd(Scene scene) { base.SceneEnd(scene); enabled = false; }
    internal static bool IsVisible(Backdrop backdrop) => backdrop.Visible ||
        (enabled && Engine.Scene.TimeActive > 1f && backdrop is Parallax parallax &&
         parallax.DoFadeIn && parallax.AppleEverestFadeIn > 0f);
}
