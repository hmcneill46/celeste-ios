#nullable disable
using System;
using System.Collections;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;

// Concrete lowering of the selected StrawberryHooks paths. The source's speed
// berry hook is outside the selected SJ factory set and keeps its earlier plan.
internal static class AppleEverestCollabStrawberryHooks
{
    private static bool hasSilver;
    internal static Sprite ReplaceSprite(Sprite original, Strawberry berry)
    {
        string id = berry is AppleEverestSilverBerry ? "SilverBerry" :
            berry is AppleEverestCollabRainbowBerry ? "RainbowBerry" : null;
        if (id == null) return original;
        return AppleEverestStaticRuntime.CreateStaticModSprite("CollabUtils2_" +
            (SaveData.Instance.CheckStrawberry(berry.ID) ? "ghost" + id : char.ToLowerInvariant(id[0]) + id.Substring(1)));
    }
    internal static string CollectionSound(string original, Strawberry berry) =>
        berry is AppleEverestSilverBerry ? "event:/SC2020_silverBerry_get" :
        berry is AppleEverestCollabRainbowBerry ? "event:/SC2020_rainbowBerry_get" : original;
    internal static IEnumerator Collect(Strawberry berry, IEnumerator original)
    {
        Scene scene = berry.Scene;
        while (original.MoveNext()) yield return original.Current;
        if (berry is AppleEverestCollabRainbowBerry)
        {
            scene.Entities.AppleEverestRemoveCollabPoints();
            scene.Add(new AppleEverestCollabRainbowBerryPerfectEffect(berry.Position));
        }
    }
    internal static PlayerDeadBody Die(Player player, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats,
        Func<Vector2, bool, bool, PlayerDeadBody> original)
    {
        bool silver = !player.Dead && (evenIfInvincible || !SaveData.Instance.Assists.Invincible) && player.StateMachine.State != 18 &&
            player.Leader.Followers.Any(follower => follower.Entity is AppleEverestSilverBerry);
        PlayerDeadBody result = original(direction, evenIfInvincible, registerDeathInStats);
        hasSilver = result != null && silver;
        return result;
    }
    internal static string GoldenDeathSound(string original) => hasSilver ? "event:/SC2020_silverBerry_death" : original;
}
