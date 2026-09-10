#nullable disable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

// MaxHelpingHand 1.40.9's finite, generated legacy/persistent group. This
// entity deliberately has no vanilla Switch component or live hook backend.
internal sealed class AppleEverestFlagTouchSwitch : Entity
{
    private readonly EntityData source;
    private readonly AppleEverestFlagGroup group;
    private readonly int id;
    private readonly MTexture border;
    private readonly Sprite icon;
    private readonly BloomPoint bloom;
    private readonly Wiggler wiggler;
    private readonly SoundSource touchSound;
    private readonly ParticleType finishParticle;
    private readonly Color inactive, active, finish;
    private readonly bool persistent, smoke;
    private readonly string hitSound, sceneSound, switchSound;
    private readonly List<AppleEverestFlagTouchSwitch> localSwitches = new();
    private float timer, ease;
    private bool groupReady;
    private Vector2 pulse = Vector2.One;
    internal bool Activated { get; private set; }
    internal bool Finished { get; private set; }
    internal AppleEverestFlagGroup Group => group;
    internal int SourceId => id;

    internal AppleEverestFlagTouchSwitch(EntityData data, Vector2 offset, EntityID entityId)
        : base(data.Position + offset)
    {
        group = AppleEverestFlagGroup.Bind(data, offset, entityId, false);
        source = data; id = data.ID; persistent = data.Bool("persistent");
        Depth = 2000;
        inactive = Calc.HexToColor(data.Attr("inactiveColor", "5FCDE4"));
        active = Calc.HexToColor(data.Attr("activeColor", "FFFFFF"));
        finish = Calc.HexToColor(data.Attr("finishColor", "F141DF"));
        hitSound = data.Attr("hitSound", "event:/game/general/touchswitch_any");
        sceneSound = data.Attr("completeSoundFromScene", "event:/game/general/touchswitch_last_oneshot");
        switchSound = data.Attr("completeSoundFromSwitch", "event:/game/general/touchswitch_last_cutoff");
        smoke = data.Bool("smoke", true);
        border = GFX.Game["objects/touchswitch/container"];
        finishParticle = new ParticleType(TouchSwitch.P_Fire) { Color = finish };
        Collider = new Hitbox(16f, 16f, -8f, -8f);
        Add(new PlayerCollider(OnPlayer, null, new Hitbox(30f, 30f, -15f, -15f)));
        Add(new HoldableCollider(OnHoldable, new Hitbox(20f, 20f, -10f, -10f)));
        Add(new SeekerCollider(OnSeeker, new Hitbox(24f, 24f, -12f, -12f)));
        Add(icon = new Sprite(GFX.Game, "objects/touchswitch/icon"));
        icon.Add("idle", "", 0f, 0);
        icon.Add("spin", "", 0.1f, new Chooser<string>("spin", 1f), 0, 1, 2, 3, 4, 5);
        icon.Play("spin"); icon.Color = inactive; icon.CenterOrigin();
        Add(bloom = new BloomPoint(0f, 16f)); bloom.Alpha = 0f;
        Add(wiggler = Wiggler.Create(0.5f, 4f, value => pulse = Vector2.One * (1f + value * 0.25f)));
        Add(new VertexLight(Color.White, 0.8f, 16, 32));
        Add(touchSound = new SoundSource());
        // The generated group proves legacyMode=true. Its constructor reset
        // condition is false, independent of scene/loader initialization order.
    }

    internal void ValidateScene(Scene scene) => group.RequireCreation(scene, source);

    public override void Added(Scene scene)
    {
        Level level = group.RequireCreation(scene, source);
        base.Added(scene);
        if (level.Session.GetFlag(group.Flag))
        {
            Activated = Finished = true;
            icon.Rate = 0.1f; icon.Play("idle"); icon.Color = finish;
            ease = bloom.Alpha = 1f;
        }
        else if (level.Session.GetFlag(group.Flag + "_switch" + id))
        {
            Activated = true; icon.Rate = 4f; icon.Color = active;
            ease = bloom.Alpha = 1f;
        }
    }

    public override void Awake(Scene scene)
    {
        group.Require(scene, source);
        base.Awake(scene);
        group.ValidateMembers(scene);
        localSwitches.Clear();
        foreach (Entity entity in scene.Tracker.GetEntities<AppleEverestFlagTouchSwitch>())
            if (entity is AppleEverestFlagTouchSwitch touch && ReferenceEquals(touch.group, group))
                localSwitches.Add(touch);
        int expected = 0;
        foreach (AppleEverestFlagMember member in group.Members) if (!member.Gate) expected++;
        if (localSwitches.Count != expected)
            throw new InvalidOperationException("generated flag switch group is incomplete or duplicated");
        groupReady = true;
    }

    private void OnPlayer(Player player) => TurnOn();
    private void OnHoldable(Holdable holdable) => TurnOn();
    private void OnSeeker(Seeker seeker)
    {
        if (SceneAs<Level>().InsideCamera(Position, 10f)) TurnOn();
    }

    internal void TurnOn()
    {
        Level level = group.Require(Scene, source);
        if (!groupReady) throw new InvalidOperationException("flag switch activation precedes complete group Awake");
        if (Activated) return;
        touchSound.Play(hitSound);
        Activated = true;
        wiggler.Start();
        for (int i = 0; i < 32; i++)
        {
            float angle = Calc.Random.NextFloat((float)Math.PI * 2f);
            level.Particles.Emit(TouchSwitch.P_FireWhite, Position + Calc.AngleToVector(angle, 6f), angle);
        }
        icon.Rate = 4f;
        if (persistent) level.Session.SetFlag(group.Flag + "_switch" + id);
        foreach (AppleEverestFlagMember member in group.Members)
            if (!member.Gate && group.Room != level.Session.Level &&
                !level.Session.GetFlag(group.Flag + "_switch" + member.Id)) return;
        foreach (AppleEverestFlagTouchSwitch touch in localSwitches) if (!touch.Activated) return;
        foreach (AppleEverestFlagTouchSwitch touch in localSwitches) { touch.Finished = true; touch.ease = 0f; }
        SoundEmitter.Play(sceneSound);
        Add(new SoundSource(switchSound));
        foreach (Entity entity in level.Tracker.GetEntities<AppleEverestFlagSwitchGate>())
            if (entity is AppleEverestFlagSwitchGate gate && ReferenceEquals(gate.Group, group)) gate.Trigger();
        foreach (AppleEverestFlagMember member in group.Members)
            if (member.Gate && member.Persistent) level.Session.SetFlag(group.Flag + "_gate" + member.Id);
        // The processor's group-persistence value is true even though this
        // particular gate is nonpersistent. Do not manufacture _gate423.
        if (!group.LegacyMode || group.GroupPersistence) level.Session.SetFlag(group.Flag);
    }

    public override void Update()
    {
        Level level = group.Require(Scene, source);
        timer += Engine.DeltaTime * 8f;
        ease = Calc.Approach(ease, Finished || Activated ? 1f : 0f, Engine.DeltaTime * 2f);
        icon.Color = Color.Lerp(inactive, Finished ? finish : active, ease);
        icon.Color *= 0.5f + ((float)Math.Sin(timer) + 1f) / 2f * (1f - ease) * 0.5f + 0.5f * ease;
        bloom.Alpha = ease;
        if (Finished)
        {
            if (icon.Rate > 0.1f)
            {
                icon.Rate -= 2f * Engine.DeltaTime;
                if (icon.Rate <= 0.1f)
                {
                    icon.Rate = 0.1f; wiggler.Start(); icon.Play("idle");
                    level.Displacement.AddBurst(Position, 0.6f, 4f, 28f, 0.2f);
                }
            }
            else if (Scene.OnInterval(0.03f) && smoke)
                level.ParticlesBG.Emit(finishParticle,
                    Position + new Vector2(0f, 1f) + Calc.AngleToVector(Calc.Random.NextAngle(), 5f));
        }
        base.Update();
    }

    public override void Render()
    {
        border.DrawCentered(Position + new Vector2(0f, -1f), Color.Black);
        border.DrawCentered(Position, icon.Color, pulse);
        base.Render();
    }
}
