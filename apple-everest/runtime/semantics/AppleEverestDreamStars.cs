#nullable disable
// Selected static implementation reviewed against FlaglinesAndSuch 1.6.80.
// Exact ZIP/DLL identities and authored guard profiles are bound by the host registry.
using System;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;


namespace Celeste.Mod;

internal sealed class AppleEverestDreamStars : Backdrop
{
    internal static Backdrop Create(BinaryPacker.Element data) => new AppleEverestDreamStars(data.AttrInt("count", 50), data.AttrFloat("minSpeed", 24f), data.AttrFloat("maxSpeed", 48f), data.AttrFloat("minSize", 2f), data.AttrFloat("maxSize", 8f), data.Attr("Color", "008080"), data.AttrInt("AngleX", -2), data.AttrInt("AngleY", -7), data.AttrFloat("Scroll"), data.Attr("shape"));

    public struct Stars
    {
        public Vector2 Position;

        public float Speed;

        public float Size;
    }

    private Stars[] stars;

    public Color StarColor;

    private Vector2 angle;

    private Vector2 lastCamera = Vector2.Zero;

    private float ParralaxScroll;

    private string ParticleShape;

    public AppleEverestDreamStars(int count, float minSpeed, float maxSpeed, float minSize, float maxSize, string Color, float AngleX, float AngleY, float scroll, string Shape)
    {
        stars = new Stars[count];
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].Position = new Vector2(Calc.Random.NextFloat(320f), Calc.Random.NextFloat(180f));
            stars[i].Speed = minSpeed + Calc.Random.NextFloat(maxSpeed - minSpeed);
            stars[i].Size = minSize + Calc.Random.NextFloat(maxSize - minSize);
        }
        StarColor = Calc.HexToColor(Color);
        angle = Vector2.Normalize(new Vector2(AngleX, AngleY));
        ParralaxScroll = scroll;
        ParticleShape = Shape;
    }

    public override void Update(Scene scene)
    {
        base.Update(scene);
        if (Visible)
        {
            Vector2 position = (scene as Level).Camera.Position;
            Vector2 vector = position - lastCamera;
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].Position += angle * stars[i].Speed * Engine.DeltaTime - vector * ParralaxScroll;
            }
            lastCamera = position;
        }
    }

    public override void Render(Scene scene)
    {
        for (int i = 0; i < stars.Length; i++)
        {
            Vector2 position = new Vector2(mod(stars[i].Position.X, 320f + 2f * stars[i].Size) - stars[i].Size, mod(stars[i].Position.Y, 180f + 2f * stars[i].Size) - stars[i].Size);
            switch (ParticleShape)
            {
            case "Diamond":
                Draw.Circle(position, stars[i].Size, StarColor, 1);
                break;
            case "Circle":
                Draw.Circle(position, stars[i].Size, StarColor, (int)Math.Log(stars[i].Size * 8f + 2f));
                break;
            case "FilledRect":
                Draw.Rect(position, stars[i].Size, stars[i].Size, StarColor);
                break;
            default:
                Draw.HollowRect(position, stars[i].Size, stars[i].Size, StarColor);
                break;
            }
        }
    }

    private float mod(float x, float m)
    {
        return (x % m + m) % m;
    }
}
