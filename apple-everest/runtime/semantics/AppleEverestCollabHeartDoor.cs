#nullable disable
using System;
using System.Collections;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;
namespace Celeste.Mod;

internal sealed class AppleEverestCollabMiniHeartDoor : AppleEverestMiniHeartDoor
{
    internal AppleEverestCollabMiniHeartDoor(EntityData data, Vector2 offset, EntityID id) : base(data, offset, id) { }
    internal string SaveId(Scene scene) => ((Level)scene).Session.Area.SID + (string.IsNullOrEmpty(DoorId) ? "" : ":" + DoorId);
    public override void Added(Scene scene)
    {
        Session session = ((Level)scene).Session;
        session.SetFlag("opened_heartgem_door_" + Requires,
            session.GetFlag("opened_mini_heart_door_" + EntityId) || AppleEverestCollabModule.Instance.SaveData.OpenedMiniHeartDoors.Contains(SaveId(scene)));
        base.Added(scene);
    }
}

internal sealed class AppleEverestCollabMiniHeartDoorUnlockTrigger : Trigger
{
    private readonly string doorId;
    internal AppleEverestCollabMiniHeartDoorUnlockTrigger(EntityData data, Vector2 offset) : base(data, offset) => doorId = data.Attr("doorID");
    private AppleEverestCollabMiniHeartDoor FindDoor() => Scene.Entities.OfType<AppleEverestCollabMiniHeartDoor>().FirstOrDefault(door => door.DoorId == doorId);
    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        var door = FindDoor();
        if (door == null || door.Opened) RemoveSelf();
    }
    public override void OnEnter(Player player) { base.OnEnter(player); Open(player); }
    private void Open(Player player)
    {
        var door = FindDoor();
        if (door != null && !door.Opened && door.Requires <= door.HeartGems && player != null)
        {
            Scene.Add(new AppleEverestCollabMiniHeartDoorUnlockCutscene(door, player));
            AppleEverestCollabModule.Instance.SaveData.OpenedMiniHeartDoors.Add(door.SaveId(Scene));
            RemoveSelf();
        }
    }
    internal static void AddAssistOption(Level level)
    {
        Player player = level.Tracker.GetEntity<Player>();
        var trigger = player?.CollideFirst<AppleEverestCollabMiniHeartDoorUnlockTrigger>();
        if (trigger == null) return;
        var door = trigger.FindDoor();
        TextMenu menu = level.Entities.AppleEverestCollabToAdd.OfType<TextMenu>().FirstOrDefault();
        if (menu == null || door == null || door.Opened || door.HeartGems >= door.Requires) return;
        menu.Add(new TextMenu.Button(Dialog.Clean("collabutils2_assist_skip")) { ConfirmSfx = "event:/ui/main/message_confirm" }.Pressed(() =>
        {
            menu.Focused = false;
            level.Add(new AppleEverestCollabAssistSkipConfirmUI(() =>
            {
                door.ForceAllHearts = true;
                trigger.Open(player);
                level.AppleEverestCollabUnpause();
                menu.Close();
            }, () => menu.Focused = true));
        }));
    }
}

internal sealed class AppleEverestCollabMiniHeartDoorUnlockCutscene : CutsceneEntity
{
    private readonly AppleEverestCollabMiniHeartDoor door;
    private readonly Player player;
    internal AppleEverestCollabMiniHeartDoorUnlockCutscene(AppleEverestCollabMiniHeartDoor door, Player player) { this.door = door; this.player = player; }
    public override void OnBegin(Level level) => Add(new Coroutine(Run(level)));
    private IEnumerator Run(Level level)
    {
        while (player.StateMachine.State != 0) yield return null;
        player.StateMachine.State = 11;
        yield return .5f;
        yield return CameraTo(door.Center - new Vector2(160f - door.Size / 2, 90f), 1f, Ease.CubeOut);
        door.ForceTrigger = true;
        while (door.AppleEverestCollabOpenPercent < 1f) yield return null;
        yield return 1f;
        yield return CameraTo(player.CameraTarget, 1f, Ease.CubeOut);
        EndCutscene(level);
    }
    public override void OnEnd(Level level)
    {
        player.StateMachine.State = 0;
        player.ForceCameraUpdate = false;
        if (!WasSkipped) return;
        level.Camera.Position = player.CameraTarget;
        door.AppleEverestCollabSkipOpening();
        foreach (Component component in door)
            if (component is Coroutine) { component.RemoveSelf(); break; }
    }
}
