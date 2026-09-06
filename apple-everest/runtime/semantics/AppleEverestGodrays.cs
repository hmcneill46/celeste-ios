#nullable disable
// Selected static implementation reviewed against FlaglinesAndSuch 1.6.80.
// Exact ZIP/DLL identities and authored guard profiles are bound by the host registry.
using System;
using Celeste;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;


namespace Celeste.Mod;

internal sealed class AppleEverestGodrays : Backdrop
{
    internal static Backdrop Create(BinaryPacker.Element data) => new AppleEverestGodrays(data.AttrInt("min_width"), data.AttrInt("max_width"), data.AttrInt("min_length"), data.AttrInt("max_length"), data.AttrFloat("duration_base"), data.AttrFloat("duration_variance"), data.AttrFloat("ray_color_alpha"), data.Attr("ray_color", "f52b63"), data.Attr("fade_to_color"), data.AttrFloat("scroll_x"), data.AttrFloat("scroll_y"), data.AttrFloat("speed_x"), data.AttrFloat("speed_y"), data.AttrInt("ray_count"), data.AttrFloat("angle_x"), data.AttrFloat("angle_y"), data.AttrBool("extend_bounds"));

    private struct Ray
    {
        public float X;

        public float Y;

        public float Percent;

        public float Duration;

        public float Width;

        public float Length;

        public void Reset(int minwidth, int maxwidth, int minlength, int maxlength, float durationbase, float durationadd, bool extbound, float gpw, float gph)
        {
            Percent = 0f;
            Duration = Math.Abs(durationbase + Calc.Random.NextFloat() * durationadd);
            Width = Calc.Random.Next(minwidth, maxwidth);
            Length = Calc.Random.Next(minlength, maxlength);
            X = Calc.Random.NextFloat(gpw + (extbound ? (Width * 2f) : 0f));
            Y = Calc.Random.NextFloat(gph + (extbound ? (Length * 2f) : 0f));
        }
    }

    private float gpWidth = 384f;

    private float gpHeight = 244f;

    public int minWidth;

    public int maxWidth;

    public int minLength;

    public int maxLength;

    public float durationBase;

    public float durationAdd;

    public Color rayColor;

    public Color rayColorFade;

    public float scrollX;

    public float scrollY;

    public float speedX;

    public float speedY;

    public float angleX = -1.6707964f;

    public float angleY = 1f;

    public int RayCount = 6;

    private bool extendedBounds;

    private VertexPositionColor[] vertices;

    private int vertexCount;

    private Ray[] rays;

    private float fade;

    public AppleEverestGodrays(int minwidth, int maxwidth, int minlength, int maxlength, float durationbase, float durationadd, float raycoloralpha, string raycolor, string raycolorfade, float scrollx, float scrolly, float speedx, float speedy, int raycount, float anglex, float angley, bool extendbounds)
    {
        maxWidth = maxwidth;
        minWidth = Math.Min(minwidth, maxwidth);
        maxLength = maxlength;
        minLength = Math.Min(minlength, maxlength);
        durationBase = durationbase;
        durationAdd = durationadd;
        rayColor = Calc.HexToColor(raycolor) * raycoloralpha;
        rayColorFade = ((raycolorfade == "") ? rayColor : (Calc.HexToColor(raycolorfade) * raycoloralpha));
        scrollX = scrollx;
        scrollY = scrolly;
        speedX = speedx;
        speedY = speedy;
        RayCount = raycount;
        angleX = anglex;
        angleY = angley;
        rays = new Ray[RayCount];
        vertices = new VertexPositionColor[6 * RayCount];
        extendedBounds = extendbounds;
        UseSpritebatch = false;
        for (int i = 0; i < rays.Length; i++)
        {
            rays[i].Reset(minWidth, maxWidth, minLength, maxLength, durationBase, durationAdd, extendedBounds, 384f, 244f);
            rays[i].Percent = Calc.Random.NextFloat();
        }
    }

    public override void Update(Scene scene)
    {
        Level level = scene as Level;
        bool flag = IsVisible(level);
        fade = Calc.Approach(fade, flag ? 1 : 0, Engine.DeltaTime);
        Visible = fade > 0f;
        if (!Visible)
        {
            return;
        }
        gpWidth = GameplayBuffers.Gameplay.Width + 64;
        gpHeight = GameplayBuffers.Gameplay.Height + 64;
        Player entity = level.Tracker.GetEntity<Player>();
        Vector2 vector = Calc.AngleToVector(angleX, angleY);
        Vector2 vector2 = new Vector2(0f - vector.Y, vector.X);
        int num = 0;
        for (int i = 0; i < rays.Length; i++)
        {
            float width = rays[i].Width;
            float length = rays[i].Length;
            float m = gpWidth + (extendedBounds ? (width * 2f) : 0f);
            float m2 = gpHeight + (extendedBounds ? (length * 2f) : 0f);
            float num2 = (extendedBounds ? (0f - width) : (-32f));
            float num3 = (extendedBounds ? (0f - length) : (-32f));
            if (rays[i].Percent >= 1f)
            {
                rays[i].Reset(minWidth, maxWidth, minLength, maxLength, durationBase, durationAdd, extendedBounds, gpWidth, gpHeight);
            }
            rays[i].Percent += Engine.DeltaTime / rays[i].Duration;
            rays[i].Y += speedY * Engine.DeltaTime;
            rays[i].X += speedX * Engine.DeltaTime;
            float percent = rays[i].Percent;
            float x = num2 + Mod(rays[i].X - level.Camera.X * scrollX, m);
            float y = num3 + Mod(rays[i].Y - level.Camera.Y * scrollY, m2);
            Vector2 vector3 = new Vector2(x, y).Floor();
            Color color = Color.Lerp(rayColor, rayColorFade, percent) * Ease.CubeInOut(Calc.Clamp(((percent < 0.5f) ? percent : (1f - percent)) * 2f, 0f, 1f)) * fade;
            if (entity != null)
            {
                float num4 = (vector3 + level.Camera.Position - entity.Position).Length();
                if (num4 < 64f)
                {
                    color *= 0.25f + 0.75f * (num4 / 64f);
                }
            }
            VertexPositionColor vertexPositionColor = new VertexPositionColor(new Vector3(vector3 + vector2 * width + vector * length, 0f), color);
            VertexPositionColor vertexPositionColor2 = new VertexPositionColor(new Vector3(vector3 - vector2 * width, 0f), color);
            VertexPositionColor vertexPositionColor3 = new VertexPositionColor(new Vector3(vector3 + vector2 * width, 0f), color);
            VertexPositionColor vertexPositionColor4 = new VertexPositionColor(new Vector3(vector3 - vector2 * width - vector * length, 0f), color);
            vertices[num++] = vertexPositionColor;
            vertices[num++] = vertexPositionColor2;
            vertices[num++] = vertexPositionColor3;
            vertices[num++] = vertexPositionColor2;
            vertices[num++] = vertexPositionColor3;
            vertices[num++] = vertexPositionColor4;
        }
        vertexCount = num;
    }

    private float Mod(float x, float m)
    {
        return (x % m + m) % m;
    }

    public override void Render(Scene scene)
    {
        if (vertexCount > 0 && fade > 0f)
        {
            GFX.DrawVertices(Matrix.Identity, vertices, vertexCount);
        }
    }
}
