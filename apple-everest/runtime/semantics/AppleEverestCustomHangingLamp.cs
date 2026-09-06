#nullable disable
// Selected static implementation reviewed against VivHelper 1.14.10.
// Package identity, authored guard and linked target patches are independently bound.
using System;
using System.Collections.Generic;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;


namespace Celeste.Mod;

internal sealed class AppleEverestCustomHangingLamp : Entity
{
    public readonly int Length;

    private List<Sprite> sprites = new List<Sprite>();

    private BloomPoint bloom;

    private VertexLight light;

    private float speed;

    private float rotation;

    private float soundDelay;

    private SoundSource sfx;

    private string AudioPath;

    private float InvWeight;

    private float AnimSpeed;

    private float lightDistance;

    private bool drawOutline;

    public AppleEverestCustomHangingLamp(EntityData e, Vector2 position)
    {
        Position = e.Position + position + Vector2.UnitX * 4f;
        Length = Math.Max(16, e.Height);
        base.Depth = 2000;
        string text = e.Attr("directory", "VivHelper/customHangingLamp/").Trim().TrimEnd(new char[1] { '/' }) + "/";
        AnimSpeed = Math.Max(e.Float("AnimationSpeed", 0.2f), 0f);
        string text2 = "";
        // Suffix is absent in every guarded profile; all three exact assets exist.
        MTexture mTexture = GFX.Game.GetAtlasSubtextures(text + "base" + text2)[0];
        if (mTexture == null)
        {
            throw new Exception("Missing file at Graphics/Atlases/Gameplay/" + text + "base" + text2 + "00");
        }
        int width = mTexture.Width;
        int height = mTexture.Height;
        MTexture mTexture2 = GFX.Game.GetAtlasSubtextures(text + "chain" + text2)[0];
        if (mTexture2 == null)
        {
            throw new Exception("Missing file at Graphics/Atlases/Gameplay/" + text + "chain" + text2 + "00");
        }
        int width2 = mTexture2.Width;
        int height2 = mTexture2.Height;
        MTexture mTexture3 = GFX.Game.GetAtlasSubtextures(text + "lamp" + text2)[0];
        if (mTexture3 == null)
        {
            throw new Exception("Missing file at Graphics/Atlases/Gameplay/" + text + "lamp" + text2 + "00");
        }
        int width3 = mTexture3.Width;
        int height3 = mTexture3.Height;
        Sprite sprite = new Sprite(GFX.Game, text + "base");
        sprite.Position = Position;
        sprite.AddLoop("main", text2, AnimSpeed);
        sprite.Origin.X = width / 2;
        sprite.Play("main");
        Sprite sprite2;
        for (int i = 0; i < Length - 8; i += height2)
        {
            sprite2 = new Sprite(GFX.Game, text + "chain");
            sprite2.Position = Position;
            sprite2.AddLoop("main", text2, AnimSpeed);
            sprite2.Origin = new Vector2((float)width2 / 2f, -i);
            sprite2.Play("main");
            sprites.Add(sprite2);
        }
        sprite2 = new Sprite(GFX.Game, text + "lamp");
        sprite2.Position = Position;
        sprite2.AddLoop("main", "", AnimSpeed);
        sprite2.Origin.X = width3 / 2;
        sprite2.Origin.Y = -(Length - height3);
        sprite2.Play("main");
        sprites.Add(sprite);
        sprites.Add(sprite2);
        Add(bloom = new BloomPoint(Vector2.UnitY * (Length - height3), Calc.Clamp(e.Float("BloomAlpha", 1f), 0f, 1f), Calc.Clamp(e.Float("BloomRadius", 48f), 0f, 128f)));
        Add(light = new VertexLight(Vector2.UnitY * (Length - height3), Color.White /* selected color; do not link dynamic Viv utility */, Calc.Clamp(e.Float("LightAlpha", 1f), 0f, 1f), Calc.Clamp(e.Int("LightFadeIn", 24), 0, 120), Calc.Clamp(e.Int("LightFadeOut", 48), 0, 120)));
        AudioPath = e.Attr("AudioPath", "event:/game/02_old_site/lantern_hit");
        InvWeight = 1f / Math.Max(e.Float("WeightMultiplier", 1f), 0.025f);
        Add(sfx = new SoundSource());
        if (height2 == height3)
        {
            base.Collider = new Hitbox(width2, Length, 0f - (float)width2 / 2f);
        }
        else
        {
            Hitbox hitbox = new Hitbox(width2, Length - height3, 0f - (float)width2 / 2f);
            Hitbox hitbox2 = new Hitbox(width3, height3, 0f - (float)width3 / 2f, Length - height3);
            base.Collider = new ColliderList(hitbox, hitbox2);
        }
        lightDistance = (float)Length - (float)height3 / 2f;
        light.Position = Vector2.UnitY * lightDistance;
        drawOutline = e.Bool("DrawOutline", defaultValue: true);
    }

    public override void Update()
    {
        base.Update();
        soundDelay -= Engine.DeltaTime;
        Player entity = base.Scene.Tracker.GetEntity<Player>();
        if (entity != null && base.Collider.Collide(entity))
        {
            speed = (0f - entity.Speed.X) * 0.005f * ((entity.Y - base.Y) / (float)Length) * InvWeight;
            if (Math.Abs(speed) < 0.1f)
            {
                speed = 0f;
            }
            else if (soundDelay <= 0f)
            {
                sfx.Play(AudioPath);
                soundDelay = 0.25f;
            }
        }
        float num = ((Math.Sign(rotation) == Math.Sign(speed)) ? 8f : 6f);
        if (Math.Abs(rotation) < 0.5f)
        {
            num *= 0.5f;
        }
        if (Math.Abs(rotation) < 0.25f)
        {
            num *= 0.5f;
        }
        float value = rotation;
        speed += (float)(-Math.Sign(rotation)) * num * Engine.DeltaTime;
        rotation += speed * Engine.DeltaTime;
        rotation = Calc.Clamp(rotation, -0.4f, 0.4f);
        if (Math.Abs(rotation) < 0.02f && Math.Abs(speed) < 0.2f)
        {
            rotation = (speed = 0f);
        }
        else if (Math.Sign(rotation) != Math.Sign(value) && soundDelay <= 0f && Math.Abs(speed) > 0.5f)
        {
            sfx.Play(AudioPath);
            soundDelay = 0.25f;
        }
        if (sprites.Count > 1)
        {
            for (int i = 1; i < sprites.Count; i++)
            {
                sprites[i].Rotation = rotation;
            }
        }
        Vector2 vector = Calc.AngleToVector(rotation + MathF.PI / 2f, lightDistance);
        BloomPoint bloomPoint = bloom;
        Vector2 position = (light.Position = vector + Position.Round() - Position);
        bloomPoint.Position = position;
        sfx.Position = vector;
    }

    public override void Render()
    {
        if (sprites.Count <= 0)
        {
            return;
        }
        if (drawOutline)
        {
            foreach (Sprite sprite in sprites)
            {
                sprite.DrawOutline();
            }
        }
        foreach (Sprite sprite2 in sprites)
        {
            sprite2.Render();
        }
    }
}
