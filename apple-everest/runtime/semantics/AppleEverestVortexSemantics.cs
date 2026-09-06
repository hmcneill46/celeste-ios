using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

/// <summary>
/// Exact lowering for the two VortexHelper attached jumpthrus in the pinned
/// Beginner lobby. Both attach to ordinary Xaphan slope solids, so the helper's
/// MoveBlock dynamic-speed exception is unreachable for this slice.
/// </summary>
internal sealed class AppleEverestAttachedJumpThru : JumpThru
{
    private readonly int columns;
    private readonly string texture;
    private readonly int authoredSurfaceIndex;
    private readonly StaticMover triggerToken;
    private Platform attachedPlatform;
    private Vector2 imageOffset;
    private bool visibleWhenDisabled;

    internal AppleEverestAttachedJumpThru(EntityData data, Vector2 offset)
        : base(data.Position + offset, data.Width, safe: false)
    {
        columns = data.Width / 8;
        texture = data.Attr("texture", "default");
        authoredSurfaceIndex = data.Int("surfaceIndex", -1);
        if (columns != 1 || texture != "wood" || authoredSurfaceIndex != -1)
            throw new InvalidOperationException("unreviewed VortexHelper attached jumpthru shape");
        Depth = -60;
        triggerToken = new StaticMover { OnAttach = platform => Depth = platform.Depth + 1 };
        Add(new StaticMover
        {
            OnMove = MoveWithPlatform,
            OnShake = OnShake,
            SolidChecker = IsRiding,
            OnEnable = EnableWithPlatform,
            OnDisable = DisableWithPlatform
        });
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        AreaData area = AreaData.Get(scene);
        string previous = area.Jumpthru;
        if (!string.IsNullOrEmpty(texture) && texture != "default") area.Jumpthru = texture;
        SurfaceSoundIndex = authoredSurfaceIndex > 0 ? authoredSurfaceIndex : previous.ToLower() switch
            { "dream" => 32, "temple" or "templeb" => 8, "core" => 3, _ => 5 };
        MTexture tiles = GFX.Game["objects/jumpthru/" + area.Jumpthru];
        int tileColumns = tiles.Width / 8;
        for (int index = 0; index < columns; index++)
        {
            int column = index == 0 ? 0 : index == columns - 1 ? tileColumns - 1 : 1 + Calc.Random.Next(tileColumns - 2);
            int row = index == 0
                ? (CollideCheck<Solid>(Position - Vector2.UnitX) ? 0 : 1)
                : index == columns - 1
                    ? (CollideCheck<Solid>(Position + Vector2.UnitX) ? 0 : 1)
                    : Calc.Choose(Calc.Random, 0, 1);
            Add(new Image(tiles.GetSubtexture(column * 8, row * 8, 8, 8)) { X = index * 8 });
        }
        // The distributed subclass repeats the ordinary JumpThru attachment
        // pass after constructing its images; preserve that second pass.
        foreach (StaticMover mover in scene.Tracker.GetComponents<StaticMover>())
            if (mover.IsRiding(this) && mover.Platform == null)
            { staticMovers.Add(mover); mover.Platform = this; mover.OnAttach?.Invoke(this); }
    }

    private bool IsRiding(Solid solid)
    {
        if (!CollideCheck(solid, Position + Vector2.UnitX) &&
            !CollideCheck(solid, Position - Vector2.UnitX)) return false;
        attachedPlatform = solid;
        triggerToken.Platform = solid;
        visibleWhenDisabled = solid is CassetteBlock;
        return true;
    }

    private void MoveWithPlatform(Vector2 amount)
    {
        if (attachedPlatform != null) LiftSpeed = attachedPlatform.LiftSpeed;
        MoveH(amount.X, LiftSpeed.X);
        MoveV(amount.Y, LiftSpeed.Y);
    }

    public override void OnShake(Vector2 amount)
    {
        imageOffset += amount;
        ShakeStaticMovers(amount);
    }

    private void DisableWithPlatform()
    {
        Active = Collidable = false;
        DisableStaticMovers();
        if (visibleWhenDisabled)
            SetImageColor(Color.Lerp(Color.Black, Color.White, 0.4f));
        else
            Visible = false;
    }

    private void EnableWithPlatform()
    {
        Active = Visible = Collidable = true;
        EnableStaticMovers();
        SetImageColor(Color.White);
    }

    private void SetImageColor(Color color)
    {
        foreach (Component component in Components)
            if (component is Image image) image.Color = color;
    }

    public override void Render()
    {
        Vector2 position = Position;
        Position += imageOffset;
        base.Render();
        Position = position;
    }

    public override void OnStaticMoverTrigger(StaticMover mover) => TriggerPlatform();

    public override void Update()
    {
        base.Update();
        Player rider = GetPlayerRider();
        if (rider != null && rider.Speed.Y >= 0f) TriggerPlatform();
    }

    private void TriggerPlatform() => attachedPlatform?.OnStaticMoverTrigger(triggerToken);

    public override void MoveHExact(int move)
    {
        if (Collidable)
            foreach (Actor actor in Scene.Tracker.GetEntities<Actor>())
                if (actor.IsRiding(this))
                {
                    Collidable = false;
                    if (actor.TreatNaive) actor.NaiveMove(Vector2.UnitX * move);
                    else actor.MoveHExact(move);
                    actor.LiftSpeed = LiftSpeed;
                    Collidable = true;
                }
        X += move;
        MoveStaticMovers(Vector2.UnitX * move);
    }
}
