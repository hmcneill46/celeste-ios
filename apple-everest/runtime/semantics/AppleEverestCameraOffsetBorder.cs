#nullable disable
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;
// MaxHelpingHand 1.40.9 selected spatial camera-border calculation.
internal sealed class AppleEverestCameraOffsetBorder : Trigger
{
    private readonly bool topLeft;

    private readonly bool topCenter;

    private readonly bool topRight;

    private readonly bool centerLeft;

    private readonly bool centerRight;

    private readonly bool bottomLeft;

    private readonly bool bottomCenter;

    private readonly bool bottomRight;

    private readonly bool inside;

    private readonly bool inverted;

    private readonly string flag;

    public AppleEverestCameraOffsetBorder(EntityData data, Vector2 offset)
        : base(data, offset)
    {
        topLeft = data.Bool("topLeft");
        topCenter = data.Bool("topCenter");
        topRight = data.Bool("topRight");
        centerLeft = data.Bool("centerLeft");
        centerRight = data.Bool("centerRight");
        bottomLeft = data.Bool("bottomLeft");
        bottomCenter = data.Bool("bottomCenter");
        bottomRight = data.Bool("bottomRight");
        inside = data.Bool("inside");
        flag = data.Attr("flag");
        inverted = data.Bool("inverted");
        AddTag(Tags.TransitionUpdate);
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        Update();
    }

    public override void Update()
    {
        base.Update();
        Player entity = base.Scene.Tracker.GetEntity<Player>();
        if (entity != null)
        {
            if (!string.IsNullOrEmpty(this.flag) && !(base.Scene as Level).Session.GetFlag(this.flag) && !inverted)
            {
                Collidable = false;
                return;
            }
            if (!string.IsNullOrEmpty(this.flag) && (base.Scene as Level).Session.GetFlag(this.flag) && inverted)
            {
                Collidable = false;
                return;
            }
            bool flag = entity.Bottom <= base.Top;
            bool flag2 = entity.Bottom > base.Top && entity.Top < base.Bottom;
            bool flag3 = entity.Top >= base.Bottom;
            bool flag4 = entity.Right <= base.Left;
            bool flag5 = entity.Right > base.Left && entity.Left < base.Right;
            bool flag6 = entity.Left >= base.Right;
            Collidable = (topLeft & flag & flag4) || (topCenter & flag & flag5) || (topRight & flag & flag6) || (centerLeft & flag2 & flag4) || (centerRight & flag2 & flag6) || (bottomLeft & flag3 & flag4) || (bottomCenter & flag3 & flag5) || (bottomRight & flag3 & flag6) || (inside & flag5 & flag2);
        }
    }

    internal static void BeginTransition(Level level)
    {
        foreach (AppleEverestCameraOffsetBorder border in level.Tracker.GetEntities<AppleEverestCameraOffsetBorder>())
            border.Active = border.Collidable = false;
    }

    internal static Vector2 CameraTarget(Player self, Vector2 result)
    {
        if (self.Scene == null)
        {
            return result;
        }
        foreach (AppleEverestCameraOffsetBorder entity in self.Scene.Tracker.GetEntities<AppleEverestCameraOffsetBorder>())
        {
            if (!entity.Collidable)
            {
                continue;
            }
            while (true)
            {
                float x = result.X;
                float num = result.X + (float)320;
                float y = result.Y;
                float num2 = result.Y + (float)180;
                if (!(x < entity.Right) || !(num > entity.Left) || !(y < entity.Bottom) || !(num2 > entity.Top))
                {
                    break;
                }
                if (self.Left <= entity.Right && num > entity.Left && (entity.topLeft || entity.centerLeft || entity.bottomLeft))
                {
                    result.X -= num - entity.Left;
                    continue;
                }
                if (self.Right >= entity.Left && x < entity.Right && (entity.topRight || entity.centerRight || entity.bottomRight))
                {
                    result.X += entity.Right - x;
                    continue;
                }
                if (self.Bottom >= entity.Top && y < entity.Bottom && (entity.bottomLeft || entity.bottomCenter || entity.bottomRight))
                {
                    result.Y += entity.Bottom - y;
                    continue;
                }
                if (self.Top <= entity.Bottom && num2 > entity.Top && (entity.topLeft || entity.topCenter || entity.topRight))
                {
                    result.Y -= num2 - entity.Top;
                    continue;
                }
                AppleEverestStaticRuntime.Log( "Camera offset border is on-screen but we didn't find any way to prevent that from happening!");
                break;
            }
        }
        return result;
    }
}
