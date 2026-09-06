#nullable disable
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;

internal sealed class AppleEverestFlagExitBlock : ExitBlock
{
    private readonly string flag;
    private readonly bool inverted, playSound, instant;
    internal AppleEverestFlagExitBlock(EntityData data, Vector2 offset) : base(data, offset)
    {
        flag = data.Attr("flag"); inverted = data.Bool("inverted");
        playSound = data.Bool("playSound"); instant = data.Bool("instant");
        Remove(Get<TransitionListener>());
    }
    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        if (SceneAs<Level>().Session.GetFlag(flag) == inverted)
        { AppleEverestAlpha = 0f; Collidable = false; }
    }
    public override void Update()
    {
        // The distributed MonoModLinkTo deliberately calls Solid.Update.
        AppleEverestSolidUpdate();
        bool previous = Collidable;
        Collidable = SceneAs<Level>().Session.GetFlag(flag) != inverted && !CollideCheck<Player>();
        if (playSound && !previous && Collidable) Audio.Play("event:/game/general/passage_closed_behind", Center);
        AppleEverestAlpha = Calc.Approach(AppleEverestAlpha, Collidable ? 1f : 0f, instant ? 1f : Engine.DeltaTime);
    }
}

internal sealed class AppleEverestHeatWaveNoColorGrade : HeatWave
{
    // Every selected profile has controlColorGradeWhenActive=false and
    // renderParticles=true. Preserve the inherited heat/particle lifecycle.
    public override void Update(Scene scene)
    {
        Level level = (Level)scene;
        bool active = IsVisible(level) && level.CoreMode != Session.CoreModes.None;
        if (!active && AppleEverestFade <= 0f) return;
        string last = level.lastColorGrade, grade = level.Session.ColorGrade;
        float ease = level.colorGradeEase, speed = level.colorGradeEaseSpeed;
        base.Update(scene);
        level.lastColorGrade = last; level.Session.ColorGrade = grade;
        level.colorGradeEase = ease; level.colorGradeEaseSpeed = speed;
        if (AppleEverestHeat <= 0f) Distort.WaterSineDirection = 1f;
    }
}

internal sealed class AppleEverestColorGradeFadeTrigger : Trigger
{
    private readonly string gradeA, gradeB;
    private readonly PositionModes direction;
    internal AppleEverestColorGradeFadeTrigger(EntityData data, Vector2 offset) : base(data, offset)
    { gradeA = data.Attr("colorGradeA"); gradeB = data.Attr("colorGradeB"); direction = data.Enum("direction", PositionModes.NoEffect); }
    public override void OnStay(Player player)
    {
        Level level = SceneAs<Level>();
        float amount = GetPositionLerp(player, direction);
        level.lastColorGrade = amount > 0.5f ? gradeA : gradeB;
        level.Session.ColorGrade = amount > 0.5f ? gradeB : gradeA;
        level.colorGradeEase = amount > 0.5f ? amount : 1f - amount;
        level.colorGradeEaseSpeed = 1f;
    }
    internal static bool SuppressGradeUpdate(Level level)
    {
        foreach (AppleEverestColorGradeFadeTrigger trigger in level.Tracker.GetEntities<AppleEverestColorGradeFadeTrigger>())
            if (trigger.Triggered) return true;
        return false;
    }
}
