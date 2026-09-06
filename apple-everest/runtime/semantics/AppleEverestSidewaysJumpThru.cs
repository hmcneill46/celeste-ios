#nullable disable
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;

internal sealed class AppleEverestSidewaysJumpThru : Entity
{
    private sealed class FakeCollidingSolid : Solid
    { internal FakeCollidingSolid() : base(Vector2.Zero, 0f, 0f, false) { } }
    private static bool enabled;
    private readonly int lines;
    private readonly bool allowLeftToRight, allowClimbing, allowWallJumping, letSeekersThrough;

    internal AppleEverestSidewaysJumpThru(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        lines = data.Height / 8;
        allowLeftToRight = !data.Bool("left");
        allowClimbing = data.Bool("allowClimbing", true);
        allowWallJumping = data.Bool("allowWallJumping", true);
        letSeekersThrough = data.Bool("letSeekersThrough");
        Depth = -60;
        Collider = new Hitbox(5f, data.Height, allowLeftToRight ? 3f : 0f);
    }
    internal static void PrepareLevel(Session session) => enabled = session.MapData?.Levels?.Any(
        level => level.Entities?.Any(entity => entity.Name == "MaxHelpingHand/SidewaysJumpThru") ?? false) == true;
    internal static void EnterOverworld(Overworld.StartMode mode) { if ((int)mode != -1) enabled = false; }

    public override void Awake(Scene scene)
    {
        // Every selected profile has texture=wood and animationDelay=0.
        // Source intentionally omits base.Awake.
        MTexture texture = GFX.Game["objects/jumpthru/wood"];
        int columns = texture.Width / 8;
        for (int i = 0; i < lines; i++)
        {
            int column, row;
            if (i == 0) { column = 0; row = CollideCheck<Solid>(Position + new Vector2(0, -1)) ? 0 : 1; }
            else if (i == lines - 1) { column = columns - 1; row = CollideCheck<Solid>(Position + new Vector2(0, 1)) ? 0 : 1; }
            else { column = 1 + Calc.Random.Next(columns - 2); row = Calc.Random.Choose(0, 1); }
            Image image = new Image(texture.GetSubtexture(column * 8, row * 8, 8, 8));
            image.Y = i * 8;
            image.Rotation = (float)Math.PI / 2f;
            if (allowLeftToRight) image.X = 8f;
            else image.Scale.Y = -1f;
            Add(image);
        }
    }
    public override void Update()
    {
        base.Update();
        _ = CollideFirst<Player>();
        // pushPlayer=false and cornerCorrect=false in every selected profile.
    }

    internal static Solid CollideWithSolid(Solid original, Entity self, int moveH)
    {
        if (!enabled || original != null) return original;
        int direction = Math.Sign(moveH);
        bool movingLeftToRight = moveH > 0;
        var collision = self.CollideFirstOutside<AppleEverestSidewaysJumpThru>(self.Position + Vector2.UnitX * direction);
        if (collision != null && collision.allowLeftToRight != movingLeftToRight &&
            (!(self is Seeker) || !collision.letSeekersThrough)) return new FakeCollidingSolid();
        return null;
    }
    internal static bool EntityNeutral(bool original, Entity self, Vector2 at) => CheckEntity(original, self, at, false, false);
    internal static bool EntityClimb(bool original, Entity self, Vector2 at) => CheckEntity(original, self, at, true, false);
    internal static bool EntityWallJump(bool original, Entity self, Vector2 at) => CheckEntity(original, self, at, false, true);
    private static bool CheckEntity(bool original, Entity self, Vector2 at, bool climb, bool wallJump)
    {
        if (!enabled || original) return original;
        if (self.Position.X == at.X) return false;
        return EntityCollision(self, at, climb, wallJump);
    }
    private static bool EntityCollision(Entity self, Vector2 at, bool climb, bool wallJump)
    {
        bool rightToLeft = self.Position.X > at.X;
        var collision = self.CollideFirstOutside<AppleEverestSidewaysJumpThru>(at);
        if (collision != null && (self is Player || self is Seeker) && collision.allowLeftToRight == rightToLeft &&
            (!wallJump || collision.allowWallJumping) && (!climb || collision.allowClimbing))
            return collision.Bottom >= self.Top + at.Y - self.Position.Y + 3f;
        return false;
    }
    internal static bool SceneNeutral(bool original, Scene self, Vector2 at)
    {
        if (!enabled || original) return original;
        return self.CollideFirst<AppleEverestSidewaysJumpThru>(at) != null;
    }
    internal static bool AfterClimbHop(bool original, Player self) => original || enabled &&
        self.CollideCheckOutside<AppleEverestSidewaysJumpThru>(self.Position + Vector2.UnitX * (float)self.Facing);
    internal static bool AfterDuckFree(bool original, Player self, Vector2 at)
    {
        if (!enabled || !original) return original;
        Collider collider = self.Collider;
        self.Collider = new Hitbox(8f, 6f, -4f, -6f);
        bool result = !EntityCollision(self, at, false, false);
        self.Collider = collider;
        return result;
    }
    internal static int AfterNormal(int result, Player self)
    {
        if (enabled && self.Speed.X != 0f)
        {
            bool movingRight = self.Speed.X > 0f;
            var collision = self.CollideFirstOutside<AppleEverestSidewaysJumpThru>(self.Position + Vector2.UnitX * Math.Sign(self.Speed.X));
            if (collision != null && collision.allowLeftToRight != movingRight) self.Speed.X = 0f;
        }
        return result;
    }
}
