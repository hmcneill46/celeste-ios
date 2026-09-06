#nullable disable
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

// MaxHelpingHand 1.40.9's three static Load factories add the same component
// to vanilla/Everest camera triggers. The component leaves Active enabled so
// a disabled trigger can observe a subsequent session-flag change.
internal static class AppleEverestMaxFlagCamera
{
    internal static Entity Offset(EntityData data, Vector2 offset) =>
        Attach(new CameraOffsetTrigger(data, offset), data);
    internal static Entity Target(EntityData data, Vector2 offset) =>
        Attach(new CameraTargetTrigger(data, offset), data);
    internal static Entity Smooth(EntityData data, Vector2 offset) =>
        Attach(new AppleEverestSmoothCameraOffsetTrigger(data, offset), data);

    private static Entity Attach(Entity entity, EntityData data)
    {
        entity.Add(new AppleEverestFlagToggleComponent(data.Attr("flag"), data.Bool("inverted")));
        return entity;
    }
}

internal sealed class AppleEverestFlagToggleComponent : Component
{
    private readonly string flag;
    private readonly bool inverted;
    private bool enabled = true;
    internal AppleEverestFlagToggleComponent(string flag, bool inverted) : base(true, false)
    { this.flag = flag; this.inverted = inverted; }
    public override void EntityAdded(Scene scene) { base.EntityAdded(scene); UpdateFlag(); }
    public override void Update() { base.Update(); UpdateFlag(); }
    private void UpdateFlag()
    {
        bool desired = SceneAs<Level>().Session.GetFlag(flag) != inverted;
        if (desired == enabled) return;
        Entity.Visible = Entity.Collidable = enabled = desired;
    }
}

internal sealed class AppleEverestSetFlagOnSpawn : Entity
{
    internal AppleEverestSetFlagOnSpawn(EntityData data, Vector2 offset)
    {
        // Both exact profiles have onlyOnRespawn=false and no ifFlag; their
        // constructor path never consults the module's respawn hook state.
        Level level = Engine.Scene as Level ?? (Engine.Scene as LevelLoader)?.Level;
        foreach (string flag in data.Attr("flag").Split(','))
            level?.Session.SetFlag(flag, data.Bool("enable"));
    }
    public override void Added(Scene scene) { base.Added(scene); RemoveSelf(); }
}
