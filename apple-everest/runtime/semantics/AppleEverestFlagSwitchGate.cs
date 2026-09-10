#nullable disable
using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod;

// Selected MaxHelpingHand gate: ordinary Solid, no return/shatter/dream path.
// Movement and particles retain the pinned coroutine/tween ordering.
internal sealed class AppleEverestFlagSwitchGate : Solid
{
    private readonly EntityData source;
    internal readonly AppleEverestFlagGroup Group;
    private readonly int id;
    private readonly Vector2 node, iconOffset;
    private readonly Color inactive, active, finish;
    private readonly ParticleType fire;
    private readonly Sprite icon;
    private readonly MTexture texture;
    private readonly Wiggler wiggler;
    private readonly SoundSource opening;
    private readonly float shakeTime, moveTime;
    private readonly string moveSound, finishedSound;
    private readonly bool particles, smoke;
    internal bool Triggered { get; private set; }
    internal int SourceId => id;

    internal AppleEverestFlagSwitchGate(EntityData data, Vector2 offset, EntityID entityId)
        : base(data.Position + offset, data.Width, data.Height, false)
    {
        Group = AppleEverestFlagGroup.Bind(data, offset, entityId, true);
        source = data; id = data.ID; node = data.Nodes[0] + offset;
        inactive = Calc.HexToColor(data.Attr("inactiveColor", "5FCDE4"));
        active = Calc.HexToColor(data.Attr("activeColor", "FFFFFF"));
        finish = Calc.HexToColor(data.Attr("finishColor", "F141DF"));
        shakeTime = data.Float("shakeTime", 0.5f); moveTime = data.Float("moveTime", 1.8f);
        moveSound = data.Attr("moveSound", "event:/game/general/touchswitch_gate_open");
        finishedSound = data.Attr("finishedSound", "event:/game/general/touchswitch_gate_finish");
        particles = data.Bool("particles", true); smoke = data.Bool("smoke", true);
        SurfaceSoundIndex = data.Int("surfaceIndex", SurfaceSoundIndex);
        fire = new ParticleType(TouchSwitch.P_Fire) { Color = finish };
        Add(icon = new Sprite(GFX.Game, "objects/switchgate/icon"));
        icon.Add("spin", "", 0.1f, "spin"); icon.Play("spin");
        icon.Rate = 0f; icon.Color = inactive;
        icon.Position = iconOffset = new Vector2(data.Width / 2f, data.Height / 2f);
        icon.CenterOrigin();
        Add(wiggler = Wiggler.Create(0.5f, 4f, value => icon.Scale = Vector2.One * (1f + value)));
        texture = GFX.Game["objects/switchgate/block"];
        Add(opening = new SoundSource());
        Add(new LightOcclude(0.5f));
    }

    internal void ValidateScene(Scene scene) => Group.RequireCreation(scene, source);

    public override void Added(Scene scene)
    {
        Group.RequireCreation(scene, source);
        base.Added(scene);
    }

    public override void Awake(Scene scene)
    {
        Level level = Group.Require(scene, source);
        base.Awake(scene); // Solid attaches its static movers here.
        Group.ValidateMembers(scene);
        if (level.Session.GetFlag(Group.Flag + "_gate" + id) || level.Session.GetFlag(Group.Flag))
        {
            MoveTo(node);
            icon.Rate = 0f; icon.SetAnimationFrame(0); icon.Color = finish;
        }
        else Add(new Coroutine(MoveSequence()));
    }

    internal void Trigger()
    {
        Group.Require(Scene, source);
        Triggered = true;
    }

    private IEnumerator MoveSequence()
    {
        Vector2 start = Position;
        while (!Triggered && !SceneAs<Level>().Session.GetFlag(Group.Flag)) yield return null;
        yield return 0.1f;
        opening.Play(moveSound);
        StartShaking(shakeTime);
        while (icon.Rate < 1f)
        {
            icon.Color = Color.Lerp(inactive, active, icon.Rate);
            icon.Rate += Engine.DeltaTime / shakeTime;
            yield return null;
        }
        yield return 0.1f;
        int particleAt = 0;
        Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeOut, moveTime + 0.2f, true);
        tween.OnUpdate = value =>
        {
            MoveTo(Vector2.Lerp(start, node, value.Eased));
            if (!particles || !Scene.OnInterval(0.1f)) return;
            particleAt = (particleAt + 1) % 2;
            for (int x = 0; x < Width / 8f; x++)
                for (int y = 0; y < Height / 8f; y++)
                    if ((x + y) % 2 == particleAt)
                        SceneAs<Level>().ParticlesBG.Emit(SwitchGate.P_Behind,
                            Position + new Vector2(x * 8, y * 8) + Calc.Random.Range(Vector2.One * 2f, Vector2.One * 6f));
        };
        Add(tween);
        // The original starts finishing after 1.8 s while its eased 2.0 s
        // Solid.MoveTo tween is still active. Do not await tween completion.
        for (float remaining = moveTime; remaining > 0f; remaining -= Engine.DeltaTime) yield return null;
        EmitArrivalDust(start);
        Audio.Play(finishedSound, Position);
        StartShaking(0.2f);
        while (icon.Rate > 0f)
        {
            icon.Color = Color.Lerp(active, finish, 1f - icon.Rate);
            icon.Rate -= Engine.DeltaTime * 4f;
            yield return null;
        }
        icon.Rate = 0f; icon.SetAnimationFrame(0); wiggler.Start();
        bool collidable = Collidable;
        Collidable = false;
        if (!Scene.CollideCheck<Solid>(Center) && smoke)
            for (int i = 0; i < 32; i++)
            {
                float angle = Calc.Random.NextFloat((float)Math.PI * 2f);
                SceneAs<Level>().ParticlesFG.Emit(fire, Position + iconOffset + Calc.AngleToVector(angle, 4f), angle);
            }
        Collidable = collidable;
    }

    private void Dust(Vector2 outside, Vector2 inside, Vector2 spread, float angle)
    {
        if (Scene.CollideCheck<Solid>(outside) && !Scene.CollideCheck<Solid>(inside))
        {
            SceneAs<Level>().ParticlesFG.Emit(SwitchGate.P_Dust, outside + spread, angle);
            SceneAs<Level>().ParticlesFG.Emit(SwitchGate.P_Dust, outside - spread, angle);
        }
    }

    private void EmitArrivalDust(Vector2 start)
    {
        bool collidable = Collidable;
        Collidable = false;
        if (node.X <= start.X)
            for (int y = 0; y < Height / 8f; y++)
            {
                Vector2 point = new(Left - 1f, Top + 4f + y * 8);
                Dust(point, point + Vector2.UnitX, new Vector2(0, 2), (float)Math.PI);
            }
        if (node.X >= start.X)
            for (int y = 0; y < Height / 8f; y++)
            {
                Vector2 point = new(Right + 1f, Top + 4f + y * 8);
                Dust(point, point - Vector2.UnitX * 2f, new Vector2(0, 2), 0f);
            }
        if (node.Y <= start.Y)
            for (int x = 0; x < Width / 8f; x++)
            {
                Vector2 point = new(Left + 4f + x * 8, Top - 1f);
                Dust(point, point + Vector2.UnitY, new Vector2(2, 0), -(float)Math.PI / 2f);
            }
        if (node.Y >= start.Y)
            for (int x = 0; x < Width / 8f; x++)
            {
                Vector2 point = new(Left + 4f + x * 8, Bottom + 1f);
                Dust(point, point - Vector2.UnitY * 2f, new Vector2(2, 0), (float)Math.PI / 2f);
            }
        Collidable = collidable;
    }

    internal bool InView()
    {
        Camera camera = SceneAs<Level>().Camera;
        // The selected resolved closure excludes both optional zoom providers;
        // the pinned Max getters then return 320/180, also during cutscene zoom.
        return Position.X + Width > camera.X - 16f && Position.Y + Height > camera.Y - 16f &&
            Position.X < camera.X + 320f && Position.Y < camera.Y + 180f;
    }

    public override void Render()
    {
        if (!InView()) return;
        int lastX = (int)Collider.Width / 8 - 1, lastY = (int)Collider.Height / 8 - 1;
        Vector2 drawPosition = Position + Shake;
        Texture2D image = texture.Texture.Texture;
        Rectangle clip = new(texture.ClipRect.X, texture.ClipRect.Y, 8, 8);
        for (int x = 0; x <= lastX; x++)
        {
            clip.X = texture.ClipRect.X + (x >= lastX ? 16 : x != 0 ? 8 : 0);
            for (int y = 0; y <= lastY; y++)
            {
                clip.Y = texture.ClipRect.Y + (y >= lastY ? 16 : y != 0 ? 8 : 0);
                Draw.SpriteBatch.Draw(image, drawPosition, clip, Color.White);
                drawPosition.Y += 8f;
            }
            drawPosition.X += 8f;
            drawPosition.Y = Position.Y + Shake.Y;
        }
        icon.Position = iconOffset + Shake;
        icon.DrawOutline();
        base.Render();
    }
}
