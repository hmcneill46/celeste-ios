#nullable disable
// Selected static implementation reviewed against FemtoHelper 1.15.22.
// Exact ZIP/DLL identities and authored guard profiles are bound by the host registry.
using System;
using System.Linq;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestWindPetals : Backdrop
{
    internal static Backdrop Create(BinaryPacker.Element data) => new AppleEverestWindPetals(data.Attr("colors", "66cc33"), data.AttrFloat("fallingSpeedMin", 8f), data.AttrFloat("fallingSpeedMax", 16f), data.AttrInt("blurCount", 15), data.AttrFloat("blurDensity", 3f), data.Attr("texture", "particles/petal"), data.AttrInt("particleCount", 40), data.AttrFloat("parallax", 1f), data.AttrFloat("spinSpeedMultiplier", 1f), data.AttrFloat("spinAmountMultiplier", 1f), data.AttrFloat("alpha", 1f), data.AttrFloat("scale", 1f), data.AttrFloat("minXDriftSpeed"), data.AttrFloat("maxXDriftSpeed"), data.AttrFloat("windX_Multiplier", 1f), data.AttrFloat("windY_Multiplier", 1f), data.AttrFloat("extraLoopBorder", 32f));

    private struct Particle
    {
        public Vector2 Position;

        public float Speed;

        public float SpeedX;

        public float Spin;

        public float MaxRotate;

        public float RotationCounter;

        public Color Color;
    }

    public string PetalColor = "66cc22";

    public readonly Color[] Colors;

    public readonly float FallSpeedMin = 6f;

    public readonly float FallSpeedMax = 16f;

    public readonly float XDriftSpeedMin;

    public readonly float XDriftSpeedMax;

    public readonly int BlurCount = 15;

    public readonly float BlurDensity = 3f;

    public readonly string Sprite = "particles/petal";

    public readonly float Parallax = 1f;

    public readonly float SpinSpeedMultiplier = 1f;

    public readonly float SpinAmount = 8f;

    public readonly float Alpha;

    public readonly float Scale;

    public readonly float WindXMultiplier;

    public readonly float WindYMultiplier;

    public readonly float ExtraLoopBorder;

    private readonly Particle[] particles = new Particle[40];

    private float fade;

    public static int GameplayBufferWidth => GameplayBuffers.Gameplay?.Width ?? 320;

    public static int GameplayBufferHeight => GameplayBuffers.Gameplay?.Height ?? 180;

    public AppleEverestWindPetals(string colors, float fallingSpeedMin, float fallingSpeedMax, int blurCount, float blurDensity, string texture, int count, float scroll, float spinFrequency, float spinAmplitude, float transparency, float size, float xDriftingSpeedMin, float xDriftingSpeedMax, float windXMultiplier, float windYMultiplier, float extraLoopBorder)
    {
        Colors = (from str in colors.Split(',')
            select AppleEverestSelectedVisualHelpers.Rgb(str.Trim())).ToArray();
        FallSpeedMin = fallingSpeedMin;
        FallSpeedMax = fallingSpeedMax;
        XDriftSpeedMin = xDriftingSpeedMin;
        XDriftSpeedMax = xDriftingSpeedMax;
        BlurCount = blurCount;
        BlurDensity = blurDensity;
        Sprite = texture;
        particles = new Particle[count];
        Parallax = (float)Math.Max(scroll, 1E-05);
        SpinSpeedMultiplier = spinFrequency;
        SpinAmount = spinAmplitude;
        Alpha = transparency;
        Scale = size;
        WindXMultiplier = windXMultiplier;
        WindYMultiplier = windYMultiplier;
        ExtraLoopBorder = extraLoopBorder;
        for (int num = 0; num < particles.Length; num++)
        {
            Reset(num);
        }
    }

    private void Reset(int i)
    {
        particles[i].Position = new Vector2(Calc.Random.Range(0f, (float)GameplayBufferWidth + ExtraLoopBorder) / Parallax, Calc.Random.Range(0f, (float)GameplayBufferHeight + ExtraLoopBorder) / Parallax);
        particles[i].Speed = Calc.Random.Range(FallSpeedMin, FallSpeedMax);
        particles[i].SpeedX = Calc.Random.Range(XDriftSpeedMin, XDriftSpeedMax);
        particles[i].Spin = Calc.Random.Range(8f, 12f) * 0.2f;
        particles[i].RotationCounter = Calc.Random.NextAngle();
        particles[i].MaxRotate = Calc.Random.Range(0.3f, 0.6f) * ((float)Math.PI / 2f);
        particles[i].Color = Colors[Calc.Random.Next(Colors.Length)];
    }

    public override void Update(Scene scene)
    {
        base.Update(scene);
        Level level = scene as Level;
        for (int i = 0; i < particles.Length; i++)
        {
            _ = Vector2.Zero;
            particles[i].Position.Y += particles[i].Speed * Engine.DeltaTime;
            particles[i].Position.X += particles[i].SpeedX * Engine.DeltaTime;
            particles[i].RotationCounter += particles[i].Spin * Engine.DeltaTime;
            particles[i].Position.Y += level.Wind.Y * WindYMultiplier * Engine.DeltaTime;
            particles[i].Position.X += level.Wind.X * WindXMultiplier * Engine.DeltaTime;
        }
        fade = Calc.Approach(fade, Visible ? 1f : 0f, Engine.DeltaTime);
    }

    public override void Render(Scene level)
    {
        if (fade <= 0f)
        {
            return;
        }
        float num = Ease.SineInOut(fade);
        Camera camera = (level as Level).Camera;
        MTexture mTexture = GFX.Game[Sprite];
        for (int i = 0; i < particles.Length; i++)
        {
            Vector2 zero = Vector2.Zero;
            zero.X = -16f + Mod(particles[i].Position.X - camera.X, ((float)GameplayBufferWidth + ExtraLoopBorder * Parallax) / Parallax);
            zero.Y = -16f + Mod(particles[i].Position.Y - camera.Y, ((float)GameplayBufferHeight + ExtraLoopBorder * Parallax) / Parallax);
            float num2 = (float)Math.PI / 2f + MathF.Sin(particles[i].RotationCounter * SpinSpeedMultiplier * particles[i].MaxRotate) * 1f;
            zero += Calc.AngleToVector(num2, 4f);
            float num3 = (level as Level).Wind.X * WindXMultiplier;
            float num4 = (level as Level).Wind.Y * WindYMultiplier;
            for (int j = 1; j < BlurCount; j++)
            {
                mTexture.DrawCentered((zero - new Vector2(num3 / 300f * ((float)j / BlurDensity), num4 / 300f * ((float)j / BlurDensity))) * Parallax, particles[i].Color * Calc.Map(j, 1f, BlurCount - 1, 0.5f, 0f) * fade * Math.Max(Math.Min(Math.Abs(num3) / 300f, 1f), Math.Min(Math.Abs(num4) / 300f, 1f)) * Alpha, 1f * Scale, (num2 - 0.8f) * SpinAmount);
            }
            mTexture.DrawCentered(zero * Parallax, particles[i].Color * num * Alpha, 1f * Scale, (num2 - 0.8f) * SpinAmount);
        }
    }

    private float Mod(float x, float m)
    {
        return (x % m + m) % m;
    }
}
