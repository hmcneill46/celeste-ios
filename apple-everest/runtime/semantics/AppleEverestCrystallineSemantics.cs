using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestBloomStrengthTrigger : Trigger
{
    private readonly float from;
    private readonly float to;
    private readonly PositionModes mode;

    internal AppleEverestBloomStrengthTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        from = data.Float("bloomStrengthFrom", 1f);
        to = data.Float("bloomStrengthTo", 1f);
        if (from is not (0.5f or 1f or 1.3f or 2f) || to is not (0.5f or 1f or 1.3f or 2f) ||
            !Enum.TryParse(data.Attr("positionMode", "NoEffect"), out mode) ||
            mode is not (PositionModes.LeftToRight or PositionModes.RightToLeft or PositionModes.NoEffect))
            throw new InvalidOperationException("unreviewed CrystallineHelper bloom-strength profile");
    }

    public override void OnStay(Player player)
    {
        float progress = MathHelper.Clamp(GetPositionLerp(player, mode), 0f, 1f);
        SceneAs<Level>().Bloom.Strength = from + (to - from) * progress;
    }
}

internal sealed class AppleEverestEditDepthTrigger : Trigger
{
    private readonly string targetId;
    private readonly int targetDepth;

    internal AppleEverestEditDepthTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        targetId = data.Attr("entitiesToAffect", "") switch
        {
            "Celeste.Mod.FancyTileEntities.FancySolidTiles" => "FancyTileEntities/FancySolidTiles",
            "Celeste.Mod.MaxHelpingHand.Entities.FlagExitBlock" => "MaxHelpingHand/FlagExitBlock",
            string value => value
        };
        targetDepth = data.Int("depth", 0);
        if (data.Bool("debug", false) || data.Bool("updateOnEntry", false))
            throw new InvalidOperationException("unreviewed CrystallineHelper edit-depth update profile");
        bool reviewed = targetId == "FancyTileEntities/FancySolidTiles" && targetDepth is -12000 or -10011 ||
                        targetId == "MaxHelpingHand/FlagExitBlock" && targetDepth == -10010 ||
                        targetId == "appleEverest/stage25kfDepthTarget" && targetDepth == -20000;
        if (!reviewed)
            throw new InvalidOperationException("unreviewed CrystallineHelper edit-depth target profile");
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        int changed = 0;
        foreach (Entity entity in scene.Entities)
        {
            AppleEverestStaticIdentity identity = entity.Get<AppleEverestStaticIdentity>();
            if (identity?.CustomId == targetId && entity.CollideCheck(this))
            {
                entity.Depth = targetDepth;
                changed++;
            }
        }
        AppleEverestStaticRuntime.Log($"crystalline-edit-depth=PASS target={targetId} depth={targetDepth} changed={changed} typed-identity=true update=false");
    }
}

internal sealed class AppleEverestTriggerTrigger : Trigger
{
    private const int TriggerEntityIdOffset = 10000000;
    private readonly int sourceEntityId;
    private readonly int expectedRumbleId;
    private readonly int expectedFlagEntityId;
    private readonly Vector2[] nodes;
    private readonly List<Trigger> targets = new();
    private bool activating;

    internal AppleEverestTriggerTrigger(EntityData data, Vector2 offset, EntityID entityId) : base(data, offset)
    {
        sourceEntityId = data.ID;
        if (entityId.ID != TriggerEntityIdOffset + sourceEntityId)
            throw new InvalidOperationException("unreviewed Celeste trigger entity ID convention");
        nodes = data.NodesOffset(offset);
        if (sourceEntityId == 1140 && data.Position == new Vector2(456f, 2067f))
        {
            expectedRumbleId = 1142;
            expectedFlagEntityId = TriggerEntityIdOffset + 1141;
        }
        else if (sourceEntityId == 43)
        {
            expectedRumbleId = 44;
            expectedFlagEntityId = TriggerEntityIdOffset + 45;
        }
        else
        {
            throw new InvalidOperationException("unreviewed CrystallineHelper trigger-trigger entity identity");
        }

        if (nodes.Length != 2 || !data.Bool("oneUse", false) ||
            data.Attr("activationType", "Flag") != "OnHoldableEnter" ||
            Math.Abs(data.Float("delay", 0f) - 0.4f) > 0.0001f ||
            data.Bool("randomize", false) || data.Bool("matchPosition", true) ||
            data.Bool("activateOnTransition", false) || data.Bool("invertCondition", false) ||
            data.Bool("invertFlag", false) || data.Bool("onlyOnEnter", false))
            throw new InvalidOperationException("unreviewed CrystallineHelper trigger-trigger behavior profile");

        Add(new HoldableCollider(OnHoldable));
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        Trigger rumble = null;
        Trigger flag = null;
        foreach (Entity entity in scene.Entities)
        {
            if (entity is not Trigger trigger || ReferenceEquals(trigger, this)) continue;
            AppleEverestStaticIdentity identity = entity.Get<AppleEverestStaticIdentity>();
            if (identity?.EntityId.ID == expectedFlagEntityId && identity.CustomId == "everest/flagTrigger" &&
                Contains(trigger, nodes[1])) flag = trigger;
            if (entity is RumbleTrigger && Contains(trigger, nodes[0])) rumble = trigger;
        }
        if (rumble == null || flag == null)
            throw new InvalidOperationException("CrystallineHelper trigger-trigger exact targets were not found");
        targets.Add(rumble);
        targets.Add(flag);
        foreach (Trigger target in targets) target.Collidable = false;
        AppleEverestStaticRuntime.Log($"crystalline-trigger-targets=PASS source={sourceEntityId} rumble={expectedRumbleId} flag={expectedFlagEntityId - TriggerEntityIdOffset} typed=true");
    }

    private static bool Contains(Trigger trigger, Vector2 point) =>
        point.X >= trigger.Left && point.X <= trigger.Right &&
        point.Y >= trigger.Top && point.Y <= trigger.Bottom;

    private void OnHoldable(Holdable holdable)
    {
        if (activating || Scene == null) return;
        activating = true;
        Add(Alarm.Create(Alarm.AlarmMode.Oneshot, Activate, 0.4f, start: true));
    }

    private void Activate()
    {
        Player player = Scene?.Tracker.GetEntity<Player>();
        if (player == null) { activating = false; return; }
        foreach (Trigger target in targets)
        {
            if (target.PlayerIsInside) target.OnLeave(player);
            target.OnEnter(player);
        }
        AppleEverestStaticRuntime.Log($"crystalline-trigger-trigger=PASS source={sourceEntityId} activation=OnHoldableEnter delay=0.4 targets=2 one-use=true");
        RemoveSelf();
    }
}

internal sealed class AppleEverestStage25KFDepthTarget : Entity
{
    internal AppleEverestStage25KFDepthTarget(EntityData data, Vector2 offset)
        : base(data.Position + offset)
    {
        Collider = new Hitbox(Math.Max(8, data.Width), Math.Max(8, data.Height));
        Depth = -100;
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        if (Depth != -20000)
            throw new InvalidOperationException("Stage 25K-F edit-depth canary did not mutate its exact target");
        AppleEverestStaticRuntime.Log("stage25kf-depth-canary=PASS depth=-20000 typed-identity=true");
    }

    public override void Render()
    {
        Draw.Rect(X, Y, Width, Height, Color.LimeGreen * 0.8f);
        Draw.HollowRect(X, Y, Width, Height, Color.White);
    }
}
