#nullable disable
using System.Collections;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

// Closed behavior of the pinned Lunatic 1.1.1 return berry. The original
// constructor adds a second PlayerCollider after Strawberry's own collider.
internal sealed class AppleEverestBubbleReturnBerry : Strawberry
{
    internal AppleEverestBubbleReturnBerry(EntityData data, Vector2 offset, EntityID id) : base(data, offset, id)
    {
        Add(new PlayerCollider(OnReturnPlayer));
    }

    private void OnReturnPlayer(Player player)
    {
        OnPlayer(player);
        if (WaitingOnSeeds) return;
        Add(new Coroutine(Return(player)));
        Collidable = false;
    }

    private IEnumerator Return(Player player)
    {
        yield return 0.3f;
        if (player.Dead) yield break;
        Level level = SceneAs<Level>();
        Vector2 respawn = level.Session.RespawnPoint.Value;
        Audio.Play("event:/game/general/cassette_bubblereturn", level.Camera.Position + new Vector2(160f, 90f));
        player.StartCassetteFly(respawn, respawn);
    }
}
