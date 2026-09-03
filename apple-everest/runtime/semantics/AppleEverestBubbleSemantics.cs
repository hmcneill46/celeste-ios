#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

// YetAnotherHelper 1.2.5 BubblePushField, specialised to the exact Beginner
// attributes: horizontal, strength 1.5, Always, liftOffOfGround, makeWind.
// The distributed DLL is the behavioral authority; no reflection survives.
internal sealed class AppleEverestBubbleField : Entity
{
    internal readonly bool Rightward;
    internal const float Strength = 1.5f;
    private readonly Dictionary<WindMover, float> amounts = new();
    private int framesSinceSpawn;
    private int spawnFrame = 30;

    internal AppleEverestBubbleField(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        string direction = data.Attr("direction", "right");
        if (direction is not ("Left" or "Right") || data.Float("strength", 1f) != Strength ||
            data.Attr("activationMode", "Always") != "Always" ||
            !data.Bool("liftOffOfGround", false) || !data.Bool("makeWind", true))
            throw new InvalidOperationException("bubble field is outside the frozen Beginner semantics");
        Rightward = direction == "Right";
        Collider = new Hitbox(data.Width, data.Height);
    }

    public override void Update()
    {
        base.Update();
        Collidable = true;
        if (++framesSinceSpawn == spawnFrame)
        {
            framesSinceSpawn = 0;
            spawnFrame = Calc.Random.Next(2, 10);
            Add(new AppleEverestBubbleParticle());
        }
        foreach (WindMover mover in Scene.Tracker.GetComponents<WindMover>())
        {
            if (mover.Entity.CollideCheck(this))
                amounts[mover] = amounts.TryGetValue(mover, out float previous)
                    ? Calc.Approach(previous, Strength, Engine.DeltaTime / 0.6f) : 0f;
            else if (amounts.TryGetValue(mover, out float previous))
            {
                float next = Calc.Approach(previous, 0f, Engine.DeltaTime / 0.3f);
                if (next == 0f) amounts.Remove(mover); else amounts[mover] = next;
            }
        }
        foreach (WindMover mover in amounts.Keys.ToArray())
        {
            if (mover?.Entity?.Scene == null) continue;
            float x = Strength * 2f * Ease.CubeInOut(amounts[mover]) * (Rightward ? 1f : -1f);
            if (mover.Entity is Player player)
            {
                if (!MovePlayer(player, x, SceneAs<Level>().Bounds)) amounts[mover] = 0f;
            }
            else mover.Move(new Vector2(x, 0f));
        }
    }

    private static bool MovePlayer(Player player, float x, Rectangle bounds)
    {
        if (player.OnGround(1) && player.Ducking) return false;
        if (player.JustRespawned || player.noWindTimer > 0f || !player.InControl ||
            player.StateMachine.State is 2 or 4 or 10 || x == 0f) return true;
        player.windTimeout = 0.2f;
        player.windDirection.X = Math.Sign(x);
        x = x < 0f
            ? Math.Max(x, bounds.Left - (player.ExactPosition.X + player.Collider.Left))
            : Math.Min(x, bounds.Right - (player.ExactPosition.X + player.Collider.Right));
        player.MoveH(x);
        return true;
    }
}

internal sealed class AppleEverestBubbleParticle : Component
{
    private AppleEverestBubbleField field;
    private MTexture texture;
    private Vector2 position;
    private Vector2 end;
    private int framesAlive;
    private int framesMaxAlive;

    internal AppleEverestBubbleParticle() : base(true, true) { }
    public override void Added(Entity entity)
    {
        base.Added(entity);
        field = (AppleEverestBubbleField)entity;
        texture = GFX.Game["particles/YetAnotherHelper/bubble_" + (Calc.Random.Next(0, 2) == 0 ? "a" : "b")];
        position = new Vector2(field.Rightward ? field.Left : field.Right,
            Calc.Random.Range(field.Bottom, field.Top));
        end = new Vector2(field.Rightward ? field.Right : field.Left,
            Calc.Random.Range(field.Bottom, field.Top));
        framesMaxAlive = (int)Calc.Random.Range(20f, field.Width / AppleEverestBubbleField.Strength * 0.5f);
    }
    public override void Update()
    {
        base.Update();
        if (framesAlive == framesMaxAlive) RemoveSelf();
        framesAlive++;
        position.X = Calc.Approach(position.X, end.X, AppleEverestBubbleField.Strength * 2f);
        position.Y = Calc.Approach(position.Y, end.Y, AppleEverestBubbleField.Strength / 5f);
    }
    public override void Render() => texture.DrawCentered(position);
}
