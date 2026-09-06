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
    private readonly int sourceEntityId;
    private readonly Vector2[] nodes;
    private readonly List<Trigger> targets = new();
    private readonly List<Entity> entitiesInside = new();
    private bool activating, deactivating, activated;

    internal AppleEverestTriggerTrigger(EntityData data, Vector2 offset, EntityID entityId) : base(data, offset)
    {
        sourceEntityId = data.ID;
        nodes = data.NodesOffset(offset);
        if (nodes.Length != 2 || !data.Bool("oneUse", false) ||
            data.Attr("activationType", "Flag") != "OnHoldableEnter" ||
            data.Float("delay", 0f) != 0.4f || data.Bool("randomize", false) ||
            data.Bool("matchPosition", true) || data.Bool("activateOnTransition", false) ||
            data.Bool("invertCondition", false) || data.Bool("invertFlag", false) || data.Bool("onlyOnEnter", false))
            throw new InvalidOperationException("unreviewed CrystallineHelper trigger-trigger behavior profile");
        Add(new HoldableCollider(holdable => entitiesInside.Add(holdable.Entity)));
        Add(new TransitionListener { OnOut = _ => DeactivateTriggers(Scene?.Tracker.GetEntity<Player>()) });
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        foreach (Vector2 node in nodes)
        {
            Dictionary<Trigger, bool> previous = new();
            foreach (Trigger trigger in scene.Tracker.GetEntities<Trigger>())
            { previous.Add(trigger, trigger.Collidable); trigger.Collidable = true; }
            Trigger target = scene.CollideFirst<Trigger>(node);
            foreach (Trigger trigger in scene.Tracker.GetEntities<Trigger>()) trigger.Collidable = previous[trigger];
            target ??= scene.Tracker.GetNearestEntity<Trigger>(node);
            if (target != this && target != null) { targets.Add(target); target.Collidable = false; }
        }
        // The selected graph contains these two target classes in this order.
        // Perform the source's unfiltered selection before checking the bound;
        // entity IDs and absolute positions are not selection criteria.
        if (targets.Count != 2 || targets[0].GetType() != typeof(RumbleTrigger) ||
            targets[1].GetType() != typeof(AppleEverestFlagTrigger))
            throw new InvalidOperationException("CrystallineHelper trigger-trigger targets are outside the selected closure");
        AppleEverestStaticRuntime.Log($"crystalline-trigger-targets=PASS source={sourceEntityId} targets=2 typed=true");
    }

    public override void Update()
    {
        base.Update();
        Player player = Scene.Tracker.GetEntity<Player>();
        if (player == null) return;
        // Retain duplicates and removal order from the original overlap list.
        List<Entity> outside = new();
        foreach (Entity entity in entitiesInside) if (!entity.CollideCheck(this)) outside.Add(entity);
        foreach (Entity entity in outside) entitiesInside.Remove(entity);
        // OnHoldableEnter forces the source's Global condition-check boolean
        // true. It is unrelated to Tags.Global or transition persistence.
        TryActivate(player);
        TryDeactivate(player);
        if (activated) RemoveSelf();
    }

    private void TryActivate(Player player)
    {
        if (activating || (activated && !deactivating) || entitiesInside.Count == 0) return;
        activating = true;
        Add(Alarm.Create(Alarm.AlarmMode.Oneshot, () =>
        { activating = false; ActivateTriggers(player); }, 0.4f, start: true));
    }
    private void TryDeactivate(Player player)
    {
        if (deactivating || (!activated && !activating) || entitiesInside.Count > 0) return;
        deactivating = true;
        Add(Alarm.Create(Alarm.AlarmMode.Oneshot, () =>
        { deactivating = false; DeactivateTriggers(player); }, 0.4f, start: true));
    }
    private void CleanTriggers() => targets.RemoveAll(trigger => trigger.Scene == null);
    private void ActivateTriggers(Player player)
    {
        DeactivateTriggers(player);
        CleanTriggers();
        activated = true;
        foreach (Trigger target in targets)
        {
            if (target.PlayerIsInside) target.OnLeave(player);
            target.OnEnter(player);
        }
        AppleEverestStaticRuntime.Log($"crystalline-trigger-trigger=PASS source={sourceEntityId} activation=OnHoldableEnter delay=0.4 targets=2 one-use=true");
    }
    private void DeactivateTriggers(Player player)
    {
        CleanTriggers();
        activated = false;
        foreach (Trigger target in targets) if (target.PlayerIsInside) target.OnLeave(player);
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
