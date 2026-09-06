#nullable disable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;

internal sealed class AppleEverestFrostEntityMover : Entity
{
    private readonly Vector2 destination;
    private readonly float duration, pauseTime;
    private float pauseTimer;
    private readonly List<(Entity Entity, Vector2 Start)> targets = new();
    private Tween tween;
    private bool moveBack;
    internal AppleEverestFrostEntityMover(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        Collider = new Hitbox(data.Width, data.Height);
        destination = data.Nodes[0] + offset;
        duration = data.Float("moveDuration", 1f);
        pauseTime = data.Float("pauseTimeLength");
        pauseTimer = data.Float("startPauseTimeLength");
    }
    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        foreach (Entity entity in scene.Entities)
            if (entity is AppleEverestCustomTutorialWithNoBird) targets.Add((entity, entity.Position));
        tween = Tween.Create(Tween.TweenMode.Looping, Ease.Linear, duration, start: true);
        tween.OnUpdate = current =>
        {
            foreach (var target in targets)
                target.Entity.Position = moveBack ? Vector2.Lerp(destination, target.Start, current.Eased) :
                    Vector2.Lerp(target.Start, destination, current.Eased);
        };
        tween.OnComplete = _ => { moveBack = !moveBack; pauseTimer = pauseTime; };
        Add(tween);
    }
    public override void Update()
    {
        // The original manually advances the tween without Entity.Update.
        if (pauseTimer > 0f) pauseTimer -= Engine.DeltaTime;
        else tween.Update();
    }
}

internal sealed class AppleEverestFrostFlutterBird : FlutterBird
{
    internal AppleEverestFrostFlutterBird(EntityData data, Vector2 offset) : base(data, offset)
    {
        // Keep the second RNG choice, even for an authored one-color list.
        Get<Sprite>().Color = Calc.Random.Choose(new[] { Calc.HexToColor(data.Attr("colors")) });
    }
}

internal sealed class AppleEverestFrostOnSpawnActivator : Trigger
{
    private readonly Vector2 node;
    private readonly List<AppleEverestResetSliceVariantsTrigger> targets = new();
    private bool activated;
    internal AppleEverestFrostOnSpawnActivator(EntityData data, Vector2 offset) : base(data, offset)
    { node = data.Nodes[0] + offset; Collidable = false; }
    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        // BaseActivator caches during Awake. Exact selected node closure has
        // only ResetVariantsTrigger; scanning is independent of Collidable.
        foreach (Entity entity in scene.Tracker.GetEntities<Trigger>())
            if (entity is AppleEverestResetSliceVariantsTrigger reset && reset.Collider.Collide(node)) targets.Add(reset);
    }
    internal static void AfterUpdateLists(Scene scene)
    {
        foreach (Entity entity in scene.Entities)
            if (entity is AppleEverestFrostOnSpawnActivator activator && !activator.activated)
                activator.Activate();
    }
    private void Activate()
    {
        activated = true;
        Player player = Scene.Tracker.GetEntity<Player>();
        if (player?.Scene != null)
            foreach (AppleEverestResetSliceVariantsTrigger target in targets)
                if (target.Scene != null)
                {
                    if (target.PlayerIsInside) target.OnLeave(player);
                    target.OnEnter(player);
                    target.OnStay(player);
                }
        AppleEverestFactoryCanary.Completion(this, "reset-variants-dispatched", player?.Scene != null && targets.Exists(target => target.Scene != null));
        RemoveSelf();
    }
}
