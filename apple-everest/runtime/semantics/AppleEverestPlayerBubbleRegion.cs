#nullable disable
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

// Selected CommunalHelper 1.25.5 behavior. The generated factory validates the
// exact source profile before this constructor; canonical CassetteFly owns the
// player state, movement, and cleanup.
internal sealed class AppleEverestPlayerBubbleRegion : Entity
{
    private readonly Vector2 control;
    private readonly Vector2 destination;

    internal AppleEverestPlayerBubbleRegion(EntityData data, Vector2 offset)
        : base(data.Position + offset)
    {
        if (data.Nodes == null || data.Nodes.Length != 2)
            throw new System.InvalidOperationException("cassette-flight region requires its two authored nodes");
        Depth = -11001;
        Collider = new Hitbox(14f, 14f);
        control = data.Nodes[0] + offset;
        destination = data.Nodes[1] + offset;
        Add(new PlayerCollider(OnPlayer));
    }

    private void OnPlayer(Player player)
    {
        if (player.Dead || player.StateMachine.State == 21) return;
        Audio.Play("event:/game/general/cassette_bubblereturn",
            SceneAs<Level>().Camera.Position + new Vector2(160f, 90f));
        player.StartCassetteFly(destination, control);
    }
}
