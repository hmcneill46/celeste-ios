#nullable disable
using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

// Typed source call sites replace the exact selected Xaphan hook closure.
// Remote-drone, Metroid and UI-controller producers are absent from this
// profile. MaxRunSpeed starts at zero and cannot change with every selected
// slope's affectPlayerSpeed=false, so its NormalUpdate multiplier remains one.
internal static class AppleEverestXaphanSlopeHooks
{
    internal static void BeforeSolidUpdate(Solid solid)
    {
        foreach (AppleEverestXaphanPlayerPlatform platform in solid.Scene.Tracker.GetEntities<AppleEverestXaphanPlayerPlatform>())
            if (platform.InView() && ((!platform.HasPlayerRider() &&
                platform.CollideFirst<Player>(platform.Position + Vector2.UnitY) == null && !platform.UpsideDown) || platform.preventCollision))
                platform.Collidable = false;
    }

    internal static float ActorMoveH(Actor actor, float move)
    {
        if (actor.CollideCheck<AppleEverestXaphanSlope>(actor.Position + Vector2.UnitY) && actor.GetType() != typeof(Player))
            return 0f;
        return move;
    }

    internal static bool TrySquish(Actor actor, CollisionData data)
    {
        if (data.Pusher.GetType() == typeof(AppleEverestXaphanPlayerPlatform) &&
            ((AppleEverestXaphanPlayerPlatform)data.Pusher).UpsideDown)
        {
            actor.Top = data.Pusher.Bottom;
            return true;
        }
        return false;
    }

    internal static void AfterActorUpdate(Actor actor, bool held = false)
    {
        if (!held)
            foreach (AppleEverestXaphanSlope slope in actor.Scene.Tracker.GetEntities<AppleEverestXaphanSlope>())
                if (slope.UpsideDown && actor.CollideCheck(slope)) actor.Position.Y++;
        AppleEverestXaphanSlope.SetCollisionAfterUpdate(actor);
    }

    internal static void AfterMoveBlockUpdate(MoveBlock block)
    {
        foreach (AppleEverestXaphanSlope slope in block.Scene.Tracker.GetEntities<AppleEverestXaphanSlope>())
            if (slope.UpsideDown && block.CollideCheck(slope)) block.Position.Y++;
        AppleEverestXaphanSlope.SetCollisionAfterUpdate(block);
    }

    internal static bool TheoCollideH(TheoCrystal crystal, CollisionData data)
    {
        if (data.Hit is not AppleEverestXaphanSlope) return false;
        crystal.Speed.X *= -0.4f;
        return true;
    }

    internal static bool GliderCollideH(Glider glider, CollisionData data)
    {
        if (data.Hit is not AppleEverestXaphanSlope) return false;
        if (glider.Speed.X < -50f)
            Audio.Play("event:/new_content/game/10_farewell/glider_wallbounce_left", glider.Position);
        else if (glider.Speed.X > 50f)
            Audio.Play("event:/new_content/game/10_farewell/glider_wallbounce_right", glider.Position);
        glider.Speed.X *= -1f;
        return true;
    }

    internal static string PlayerSpriteAnimation(Sprite sprite, string id)
    {
        if (sprite.Entity is Player player && player.Sprite == sprite && sprite.Scene is Level level && player.StateMachine.State != 2)
            foreach (AppleEverestXaphanPlayerPlatform platform in level.Tracker.GetEntities<AppleEverestXaphanPlayerPlatform>())
                if (platform.PlayerPose != "" && platform.Active) return platform.PlayerPose;
        return id;
    }

    internal static void InstallSpriteExtensions(SpriteBank selected)
    {
        const string prefix = "XaphanHelper_Extend_";
        foreach (string name in new[] { "player", "player_no_backpack" })
        {
            string extension = prefix + name;
            GFX.SpriteBank.SpriteData[extension] = selected.SpriteData[extension];
            PlayerSprite.CreateFramesMetadata(extension);
        }
        // The original metadata hook uses the player extension as its fallback
        // for other player modes, and never overwrites an existing animation.
        foreach (string name in new[] { "player", "player_no_backpack", "badeline", "player_badeline", "player_playback" })
        {
            string extension = name == "player_no_backpack" ? prefix + name : prefix + "player";
            GFX.SpriteBank.SpriteData[name].Sprite.AppleEverestCopyMissingAnimations(selected.SpriteData[extension].Sprite);
        }
    }
}
