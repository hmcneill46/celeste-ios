#nullable disable
// Selected static implementation reviewed against LunaticHelper 1.1.1.
// Exact ZIP/DLL identities and authored guard profiles are bound by the host registry.
using System;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;


namespace Celeste.Mod;

internal sealed class AppleEverestCustomDust : Backdrop
{
    internal static Backdrop Create(BinaryPacker.Element data) => new AppleEverestCustomDust(data.Attr("colors"), data.AttrInt("amount", 50));

    private struct Particle
    {
        public Vector2 Position;

        public Vector2 Direction;

        public float Percent;

        public float Duration;

        public float Speed;

        public float Spin;

        public Color Color;
    }

    private readonly Color[] colors;

    private float fade;

    private readonly Particle[] particles;

    private Vector2 scale;

    public AppleEverestCustomDust(string colors, int amount = 50)
    {
        particles = new Particle[amount];
        string[] array = colors.Split(new char[1] { ',' });
        this.colors = new Color[array.Length];
        for (int i = 0; i < array.Length; i++)
        {
            this.colors[i] = Calc.HexToColor(array[i]);
        }
        for (int j = 0; j < particles.Length; j++)
        {
            Reset(j, Calc.Random.NextFloat());
        }
    }

    private void Reset(int i, float f)
    {
        particles[i].Percent = f;
        particles[i].Position = new Vector2(Calc.Random.Range(0, 320), Calc.Random.Range(0, 180));
        particles[i].Speed = Calc.Random.Range(4, 14);
        particles[i].Spin = Calc.Random.Range(0.25f, MathF.PI * 6f);
        particles[i].Duration = Calc.Random.Range(1f, 4f);
        particles[i].Direction = Calc.AngleToVector(Calc.Random.NextFloat(MathF.PI * 2f), 1f);
        particles[i].Color = colors[Calc.Random.Next(colors.Length)];
    }

    private static float Mod(float x, float m)
    {
        return (x % m + m) % m;
    }

    public override void Update(Scene scene)
    {
        base.Update(scene);
        Level level = scene as Level;
        Vector2 zero = Vector2.Zero;
        if (level.Wind.Y == 0f)
        {
            scale.X = Math.Max(1f, Math.Abs(level.Wind.X) / 100f);
            scale.Y = 1f;
            zero = new Vector2(level.Wind.X, 0f);
        }
        else
        {
            scale.X = 1f;
            scale.Y = Math.Max(1f, Math.Abs(level.Wind.Y) / 40f);
            zero = new Vector2(0f, level.Wind.Y * 2f);
        }
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].Percent >= 1f)
            {
                Reset(i, 0f);
            }
            Particle[] array = particles;
            int num = i;
            array[num].Percent += Engine.DeltaTime / particles[i].Duration;
            Particle[] array2 = particles;
            int num2 = i;
            array2[num2].Position += (particles[i].Direction * particles[i].Speed + zero) * Engine.DeltaTime;
            particles[i].Direction.Rotate(particles[i].Spin * Engine.DeltaTime);
        }
        fade = Calc.Approach(fade, Visible ? 1f : 0f, Engine.DeltaTime);
    }

    public override void Render(Scene scene)
    {
        if (!(fade <= 0f))
        {
            Camera camera = (scene as Level).Camera;
            for (int i = 0; i < particles.Length; i++)
            {
                Vector2 position = new Vector2
                {
                    X = Mod(particles[i].Position.X - camera.X, 320f),
                    Y = Mod(particles[i].Position.Y - camera.Y, 180f)
                };
                float percent = particles[i].Percent;
                float num = ((percent < 0.7f) ? Calc.ClampedMap(percent, 0f, 0.3f) : Calc.ClampedMap(percent, 0.7f, 1f, 1f, 0f));
                num *= FadeAlphaMultiplier;
                Draw.Rect(position, scale.X, scale.Y, particles[i].Color * (fade * num));
            }
        }
    }
}
