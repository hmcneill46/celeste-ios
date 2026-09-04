using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestStage25KFAudioCanary : Entity
{
    private static readonly string[] Paths =
    {
        "event:/sj21_jamjar-blue",
        "event:/sj21_BegLobby",
        "event:/sj21_bingovergoogle",
        "event:/sj21_levelselect"
    };

    internal AppleEverestStage25KFAudioCanary(EntityData data, Vector2 offset)
        : base(data.Position + offset) { }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        foreach (string path in Paths)
            Audio.Play(path, Position);
        AppleEverestStaticRuntime.Log("stage25kf-audio-canary=requested=4 ordinary-audio-path=true");
        RemoveSelf();
    }
}
