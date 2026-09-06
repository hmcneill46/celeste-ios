#nullable disable
// Selected static implementation reviewed against FemtoHelper 1.15.22.
// Exact ZIP/DLL identities and authored guard profiles are bound by the host registry.
using System;
using Microsoft.Xna.Framework;
using Monocle;


namespace Celeste.Mod;

internal sealed class AppleEverestParticleEmitter : Entity
{
    public readonly ParticleType EmitterParticle;

    public readonly float ParticleSpawnSpread;

    public float SpawnTimer;

    public readonly int ParticleCount;

    public readonly float SpawnInterval;

    public readonly float SpawnChance;

    public readonly float ParticleAngle;

    public readonly float ParticleAngleRange;

    public readonly bool IsFg;

    public readonly string Flag;

    public new readonly string Tag;

    public readonly bool AttachToPlayer;

    public readonly Vector2 AttachToPlayerOffset;

    public AppleEverestParticleEmitter(EntityData data, Vector2 offset)
        : base(data.Position + offset)
    {
        Add(new BloomPoint(data.Float("BloomAlpha"), data.Float("BloomRadius", 6f)));
        ParticleSpawnSpread = data.Float("particleSpawnSpread");
        ParticleCount = data.Int("particleCount");
        SpawnInterval = data.Float("spawnInterval");
        SpawnChance = data.Float("spawnChance");
        ParticleAngle = data.Float("particleAngle");
        ParticleAngleRange = data.Float("particleAngleRange");
        AttachToPlayer = data.Bool("attachToPlayer");
        AttachToPlayerOffset = new Vector2(data.Float("attachToPlayerOffsetX"), data.Float("attachToPlayerOffsetY"));
        IsFg = data.Bool("foreground");
        Flag = data.Attr("flag");
        Tag = data.Attr("tag");
        string[] array = data.Attr("particleTexture").Split(',');
        Chooser<MTexture> chooser = new Chooser<MTexture>();
        string[] array2 = array;
        foreach (string id in array2)
        {
            chooser.Add(GFX.Game[id], 1f);
        }
        if (!data.Bool("noTexture"))
        {
            EmitterParticle = new ParticleType
            {
                SourceChooser = chooser,
                Color = AppleEverestSelectedVisualHelpers.Rgb(data.Attr("particleColor", "40FF9080")) * data.Float("particleAlpha"),
                Color2 = AppleEverestSelectedVisualHelpers.Rgb(data.Attr("particleColor2", "20408000")) * data.Float("particleAlpha"),
                FadeMode = ((data.Int("particleFadeMode") != 0) ? ((data.Int("particleFadeMode") == 1) ? ParticleType.FadeModes.Linear : ((data.Int("particleFadeMode") == 2) ? ParticleType.FadeModes.Late : ParticleType.FadeModes.InAndOut)) : ParticleType.FadeModes.None),
                ColorMode = ((data.Int("particleColorMode") != 0) ? ((data.Int("particleColorMode") == 1) ? ParticleType.ColorModes.Choose : ((data.Int("particleColorMode") == 2) ? ParticleType.ColorModes.Blink : ParticleType.ColorModes.Fade)) : ParticleType.ColorModes.Static),
                RotationMode = ((data.Int("particleRotationMode") != 0) ? ((data.Int("particleRotationMode") == 1) ? ParticleType.RotationModes.Random : ParticleType.RotationModes.SameAsDirection) : ParticleType.RotationModes.None),
                LifeMin = data.Float("particleLifespanMin"),
                LifeMax = data.Float("particleLifespanMax"),
                Size = data.Float("particleSize"),
                SizeRange = data.Float("particleSizeRange"),
                ScaleOut = data.Bool("particleScaleOut"),
                SpeedMin = data.Float("particleSpeedMin"),
                SpeedMax = data.Float("particleSpeedMax"),
                Friction = data.Float("particleFriction"),
                Acceleration = new Vector2(data.Float("particleAccelX"), data.Float("particleAccelY")),
                SpinFlippedChance = data.Bool("particleFlipChance"),
                SpinMin = data.Float("particleSpinSpeedMin"),
                SpinMax = data.Float("particleSpinSpeedMax")
            };
        }
        else
        {
            EmitterParticle = new ParticleType
            {
                Color = AppleEverestSelectedVisualHelpers.Rgb(data.Attr("particleColor", "40FF9080")) * data.Float("particleAlpha"),
                Color2 = AppleEverestSelectedVisualHelpers.Rgb(data.Attr("particleColor2", "20408000")) * data.Float("particleAlpha"),
                FadeMode = ((data.Int("particleFadeMode") != 0) ? ((data.Int("particleFadeMode") == 1) ? ParticleType.FadeModes.Linear : ((data.Int("particleFadeMode") == 2) ? ParticleType.FadeModes.Late : ParticleType.FadeModes.InAndOut)) : ParticleType.FadeModes.None),
                ColorMode = ((data.Int("particleColorMode") != 0) ? ((data.Int("particleColorMode") == 1) ? ParticleType.ColorModes.Choose : ((data.Int("particleColorMode") == 2) ? ParticleType.ColorModes.Blink : ParticleType.ColorModes.Fade)) : ParticleType.ColorModes.Static),
                RotationMode = ((data.Int("particleRotationMode") != 0) ? ((data.Int("particleRotationMode") == 1) ? ParticleType.RotationModes.Random : ParticleType.RotationModes.SameAsDirection) : ParticleType.RotationModes.None),
                LifeMin = data.Float("particleLifespanMin"),
                LifeMax = data.Float("particleLifespanMax"),
                Size = data.Float("particleSize"),
                SizeRange = data.Float("particleSizeRange"),
                ScaleOut = data.Bool("particleScaleOut"),
                SpeedMin = data.Float("particleSpeedMin"),
                SpeedMax = data.Float("particleSpeedMax"),
                Friction = data.Float("particleFriction"),
                Acceleration = new Vector2(data.Float("particleAccelX"), data.Float("particleAccelY")),
                SpinFlippedChance = data.Bool("particleFlipChance"),
                SpinMin = data.Float("particleSpinSpeedMin"),
                SpinMax = data.Float("particleSpinSpeedMax")
            };
        }
    }

    public override void Update()
    {
        _ = base.Scene;
        base.Update();
        if (AttachToPlayer)
        {
            Player entity = base.Scene.Tracker.GetEntity<Player>();
            if (entity != null)
            {
                Position = entity.Position + AttachToPlayerOffset;
            }
        }
        SpawnTimer++;
        if (!Visible || !base.Scene.OnInterval(SpawnInterval) || false /* selected empty flag is unconditionally enabled */)
        {
            return;
        }
        for (int i = 0; i < ParticleCount; i++)
        {
            if (Calc.Random.NextFloat() < SpawnChance / 100f)
            {
                float num = ParticleAngleRange / 360f * ((float)Math.PI * 2f);
                if (IsFg)
                {
                    SceneAs<Level>().ParticlesFG.Emit(EmitterParticle, 1, base.Center, Vector2.One * ParticleSpawnSpread, ParticleAngle / 360f * ((float)Math.PI * 2f) + (Calc.Random.NextFloat() * num - num / 2f));
                }
                else
                {
                    SceneAs<Level>().ParticlesBG.Emit(EmitterParticle, 1, base.Center, Vector2.One * ParticleSpawnSpread, ParticleAngle / 360f * ((float)Math.PI * 2f) + (Calc.Random.NextFloat() * num - num / 2f));
                }
            }
        }
    }

    public override void DebugRender(Camera camera)
    {
        base.DebugRender(camera);
        Draw.HollowRect(Position, 4f, 4f, Color.Purple);
    }
}
