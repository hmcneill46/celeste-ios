#nullable disable
// Selected static implementation reviewed against LunaticHelper 1.1.1.
// Exact ZIP/DLL identities and authored guard profiles are bound by the host registry.
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;


namespace Celeste.Mod;

internal sealed class AppleEverestInvisibleLightSource : Entity
{
    private readonly float alpha;

    private BloomPoint bloom;

    private readonly Color color;

    private VertexLight light;

    private readonly float radius;

    public AppleEverestInvisibleLightSource(EntityData data, Vector2 offset)
        : base(data.Position + offset)
    {
        alpha = data.Float("alpha", 1f);
        radius = data.Float("radius", 48f);
        color = Color.White /* exact selected color; omit reflected named-color table */;
        Add(bloom = new BloomPoint(alpha, radius));
        Add(light = new VertexLight(color, alpha, data.Int("startFade", 24), data.Int("endFade", 48)));
    }
}
