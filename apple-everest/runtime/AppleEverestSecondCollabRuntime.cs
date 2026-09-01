#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

/// <summary>
/// Source-level, statically registered semantics for the bounded ordinary
/// CollabUtils2/EeveeHelper/LunaticHelper surface selected by Stage 25K-B.
/// These helpers deliberately contain no assembly scan, reflection, detour or
/// runtime IL path; ClosureGenerator patches the two vanilla hosts at build
/// time and every authored custom ID remains fail-closed in the registry.
/// </summary>
internal static class AppleEverestSecondCollabRuntime
{
    internal static int HeartGems(HeartGemDoor door, int vanilla)
    {
        if (door is not AppleEverestMiniHeartDoor mini) return vanilla;
        if (mini.ForceAllHearts) return door.Requires;
        return AppleEverestProgressionRuntime.TotalHearts(mini.LevelSet, SaveData.Instance);
    }

    internal static string DoorFlag(HeartGemDoor door, string vanilla) =>
        door is AppleEverestMiniHeartDoor mini
            ? "opened_mini_heart_door_" + mini.EntityId
            : vanilla;

    internal static bool CanApproachDoor(HeartGemDoor door, Player player, bool vanilla)
    {
        if (door is not AppleEverestMiniHeartDoor mini) return vanilla;
        return mini.ForceTrigger || player != null && player.Center.Y > door.Y - mini.DoorHalfHeight &&
            player.Center.Y < door.Y + mini.DoorHalfHeight;
    }

    internal static void ConfigureHeartDoor(HeartGemDoor door, Solid top, Solid bottom, float openDistance, bool opened)
    {
        if (door is not AppleEverestMiniHeartDoor mini) return;
        top.Collider.Height = mini.DoorHalfHeight;
        bottom.Collider.Height = mini.DoorHalfHeight;
        top.Top = door.Y - mini.DoorHalfHeight;
        bottom.Bottom = door.Y + mini.DoorHalfHeight;
        if (!opened) return;
        top.Collider.Height = Math.Max(0f, mini.DoorHalfHeight - openDistance);
        bottom.Collider.Height = Math.Max(0f, mini.DoorHalfHeight - openDistance);
        bottom.Top += openDistance;
    }

    internal static void ClampHeartDoor(HeartGemDoor door, Solid top, Solid bottom, bool opened)
    {
        if (!opened || door is not AppleEverestMiniHeartDoor mini) return;
        if (top.Top != door.Y - mini.DoorHalfHeight)
        {
            float displacement = door.Y - mini.DoorHalfHeight - top.Top;
            top.Collider.Height = Math.Max(0f, mini.DoorHalfHeight - displacement);
            top.Top = door.Y - mini.DoorHalfHeight;
        }
        if (bottom.Bottom != door.Y + mini.DoorHalfHeight)
        {
            float displacement = bottom.Top - door.Y;
            bottom.Collider.Height = Math.Max(0f, mini.DoorHalfHeight - displacement);
        }
    }

    internal static Color HeartDoorColor(HeartGemDoor door, Color vanilla) =>
        door is AppleEverestMiniHeartDoor mini ? mini.Color : vanilla;

    internal static void ConfigureStrawberry(Strawberry berry, Sprite sprite, BloomPoint bloom, VertexLight light)
    {
        string spriteId = berry switch
        {
            AppleEverestSilverBerry => "CollabUtils2_silverBerry",
            AppleEverestSpeedBerry => "CollabUtils2_speedBerry",
            AppleEverestRainbowBerry => "CollabUtils2_rainbowBerry",
            AppleEverestSecretBerry secret => secret.SpriteId,
            _ => null
        };
        if (!string.IsNullOrEmpty(spriteId))
        {
            AppleEverestStaticRuntime.CreateStaticModSpriteOn(sprite, spriteId);
            sprite.Play("idle");
        }
        if (berry is AppleEverestRainbowBerry) bloom.Alpha = 0.5f;
        if (berry is AppleEverestSpeedBerry speed) speed.ConfigureVisual(sprite);
    }

    internal static bool ShouldSetGrabbedGolden(Strawberry berry) => berry is not AppleEverestSpeedBerry;

    internal static bool ShouldCollectGolden(Strawberry berry, Player player, bool vanilla)
    {
        if (berry is AppleEverestSpeedBerry speed) return speed.ShouldCollect(player);
        return vanilla;
    }

    internal static void UpdateSpecialBerry(Strawberry berry)
    {
        if (berry is AppleEverestSpeedBerry speed) speed.UpdateTimer();
    }
}

internal sealed class AppleEverestGoldenBerryPlayerRespawnPoint : Entity
{
    public override void Added(Scene scene)
    {
        base.Added(scene);
        // The marker is metadata consumed by golden-restart logic; retaining a
        // scene entity would incorrectly add collision or visuals.
        RemoveSelf();
    }
}

internal sealed class AppleEverestMiniHeartDoor : HeartGemDoor
{
    private static readonly Dictionary<string, string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["beginner"] = "18668F", ["intermediate"] = "E0233D", ["advanced"] = "896900",
        ["expert"] = "824207", ["grandmaster"] = "650091"
    };

    internal readonly string LevelSet;
    internal readonly float DoorHalfHeight;
    internal readonly EntityID EntityId;
    internal readonly string DoorId;
    internal readonly Color Color;
    internal bool ForceTrigger;
    internal bool ForceAllHearts;

    internal AppleEverestMiniHeartDoor(EntityData data, Vector2 offset, EntityID entityId) : base(data, offset)
    {
        LevelSet = data.Attr("levelSet");
        DoorHalfHeight = data.Height;
        EntityId = entityId;
        DoorId = data.Attr("doorID");
        string color = data.Attr("color", "18668F");
        if (NamedColors.TryGetValue(color, out string named)) color = named;
        Color = Calc.HexToColor(color);
    }
}

internal sealed class AppleEverestMiniHeartDoorUnlockTrigger : Trigger
{
    private readonly string doorId;
    internal AppleEverestMiniHeartDoorUnlockTrigger(EntityData data, Vector2 offset) : base(data, offset) =>
        doorId = data.Attr("doorID");

    public override void OnEnter(Player player)
    {
        base.OnEnter(player);
        AppleEverestMiniHeartDoor door = Scene.Entities.FindAll<AppleEverestMiniHeartDoor>()
            .FirstOrDefault(value => value.DoorId == doorId);
        if (door != null && !door.Opened && door.HeartGems >= door.Requires)
        {
            door.ForceTrigger = true;
            RemoveSelf();
        }
    }
}

internal sealed class AppleEverestSilverBerry : Strawberry
{
    private readonly bool alwaysSpawn;
    internal AppleEverestSilverBerry(EntityData data, Vector2 offset, EntityID id) : base(data, offset, id)
    {
        Golden = true;
        alwaysSpawn = data.Bool("alwaysSpawn");
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        Level level = scene as Level;
        if (!alwaysSpawn && !SaveData.Instance.CheatMode &&
            !AppleEverestProgressionRuntime.Completed(level.Session.Area, SaveData.Instance)) RemoveSelf();
    }
}

internal sealed class AppleEverestSecretBerry : Strawberry
{
    internal readonly string SpriteId;
    internal AppleEverestSecretBerry(EntityData data, Vector2 offset, EntityID id) : base(data, offset, id) =>
        SpriteId = data.Attr("strawberrySprite", "strawberry");
}

internal sealed class AppleEverestSpeedBerry : Strawberry
{
    private readonly float bronzeTime;
    private readonly float silverTime;
    private readonly float goldTime;
    private float elapsed;
    private bool expired;
    private Sprite visual;

    internal AppleEverestSpeedBerry(EntityData data, Vector2 offset, EntityID id) : base(data, offset, id)
    {
        Golden = true;
        bronzeTime = data.Float("bronzeTime", 15f);
        silverTime = data.Float("silverTime", 10f);
        goldTime = data.Float("goldTime", 5f);
        Follower.PersistentFollow = true;
    }

    public override void Awake(Scene scene)
    {
        Level level = scene as Level;
        if (!SaveData.Instance.CheatMode && !AppleEverestProgressionRuntime.Completed(level.Session.Area, SaveData.Instance))
        {
            RemoveSelf();
            return;
        }
        base.Awake(scene);
    }

    internal void ConfigureVisual(Sprite sprite) => visual = sprite;

    internal bool ShouldCollect(Player player) => Follower.HasLeader &&
        player != null && player.CollideCheck<AppleEverestSpeedBerryCollectTrigger>();

    internal void UpdateTimer()
    {
        if (expired || collected || !Follower.HasLeader) return;
        elapsed += Engine.DeltaTime;
        string animation = elapsed >= silverTime ? "idle_bronze" : elapsed >= goldTime ? "idle_silver" : "idle_gold";
        if (visual != null && visual.CurrentAnimationID != animation && visual.Has(animation)) visual.Play(animation);
        if (elapsed <= bronzeTime) return;
        expired = true;
        SceneAs<Level>()?.Session.DoNotLoad.Remove(ID);
        Player player = Scene?.Tracker.GetEntity<Player>();
        if (player != null && !player.Dead) player.Die(Vector2.Zero, true, true);
        RemoveSelf();
    }
}

[Tracked(false)]
internal sealed class AppleEverestSpeedBerryCollectTrigger : Trigger
{
    internal AppleEverestSpeedBerryCollectTrigger(EntityData data, Vector2 offset) : base(data, offset) { }
}

internal sealed class AppleEverestRainbowBerry : Strawberry
{
    private readonly string levelSet;
    private readonly string[] maps;
    private readonly int required;
    internal bool Ready { get; private set; }

    internal AppleEverestRainbowBerry(EntityData data, Vector2 offset, EntityID id) : base(data, offset, id)
    {
        levelSet = data.Attr("levelSet");
        maps = string.IsNullOrEmpty(data.Attr("maps")) ? null : data.Attr("maps").Split(',')
            .Select(value => levelSet + "/" + value).ToArray();
        required = data.Int("requires", -1);
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        (int collected, int total) = AppleEverestProgressionRuntime.SilverBerries(levelSet, SaveData.Instance, maps);
        int need = required >= 0 ? required : total;
        Ready = collected >= need;
        if (Ready) return;
        scene.Add(new AppleEverestRainbowHologram(Position, collected, need));
        RemoveSelf();
    }
}

internal sealed class AppleEverestRainbowHologram : Entity
{
    private readonly int collected;
    private readonly int total;
    internal AppleEverestRainbowHologram(Vector2 position, int collected, int total) : base(position)
    {
        this.collected = collected;
        this.total = total;
        Depth = -100;
        Add(AppleEverestStaticRuntime.CreateStaticModSprite("CollabUtils2_holoRainbowBerry"));
    }

    public override void Render()
    {
        base.Render();
        ActiveFont.DrawOutline(collected + "/" + total, Position + new Vector2(0f, 18f),
            new Vector2(0.5f, 0f), Vector2.One * 0.25f, Color.White, 1f, Color.Black);
    }
}

internal sealed class AppleEverestRainbowBerryUnlockTrigger : Trigger
{
    internal AppleEverestRainbowBerryUnlockTrigger(EntityData data, Vector2 offset) : base(data, offset) { }
    public override void OnEnter(Player player)
    {
        base.OnEnter(player);
        RemoveSelf();
    }
}

internal sealed class AppleEverestFlagToggleModifier : Entity
{
    private readonly string flag;
    private readonly bool notFlag;
    private readonly bool toggleActive;
    private readonly bool toggleVisible;
    private readonly bool toggleCollidable;
    private readonly bool remember;
    private readonly List<(Entity Entity, bool Active, bool Visible, bool Collidable)> contained = new();

    internal AppleEverestFlagToggleModifier(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        Collider = new Hitbox(data.Width, data.Height);
        string raw = data.Attr("flag");
        if (raw.StartsWith("!", StringComparison.Ordinal)) { flag = raw.Substring(1); notFlag = true; }
        else { flag = raw; notFlag = data.Bool("notFlag"); }
        toggleActive = data.Bool("toggleActive", true);
        toggleVisible = data.Bool("toggleVisible", true);
        toggleCollidable = data.Bool("toggleCollidable", true);
        remember = data.Bool("rememberInitialState", true);
        Depth = Depths.Top - 9;
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        foreach (AppleEverestSecretBerry berry in scene.Entities.FindAll<AppleEverestSecretBerry>())
            if (CollideCheck(berry)) contained.Add((berry, berry.Active, berry.Visible, berry.Collidable));
        Apply();
    }

    public override void Update()
    {
        base.Update();
        Apply();
    }

    private void Apply()
    {
        bool enabled = (string.IsNullOrEmpty(flag) ? false : SceneAs<Level>().Session.GetFlag(flag)) != notFlag;
        foreach ((Entity entity, bool active, bool visible, bool collidable) in contained)
        {
            if (entity.Scene == null) continue;
            if (toggleActive) entity.Active = enabled ? (remember ? active : true) : false;
            if (toggleVisible) entity.Visible = enabled ? (remember ? visible : true) : false;
            if (toggleCollidable) entity.Collidable = enabled ? (remember ? collidable : true) : false;
        }
    }
}

internal sealed class AppleEverestStrawberryGate : Solid
{
    private readonly int requires;
    private readonly bool startHidden;
    private bool opened;
    private readonly MTexture edge;
    private readonly MTexture top;
    private readonly MTexture[] icons;

    internal AppleEverestStrawberryGate(EntityData data, Vector2 offset)
        : base(data.Position + offset - new Vector2(0f, 90f), data.Width, 180f, safe: true)
    {
        requires = data.Int("requires");
        startHidden = data.Bool("startHidden");
        Visible = !startHidden;
        Depth = -9000;
        edge = GFX.Game["objects/lunatichelper/strawberrygate/edge"];
        top = GFX.Game["objects/lunatichelper/strawberrygate/top"];
        icons = Enumerable.Range(0, 7).Select(index => GFX.Game[
            "objects/lunatichelper/strawberrygate/icon" + index.ToString("00")]).ToArray();
    }

    public override void Update()
    {
        base.Update();
        if (opened || Scene is not Level level) return;
        int berries = AppleEverestProgressionRuntime.TotalStrawberries(
            AppleEverestProgressionRuntime.LevelSet(level.Session.Area), SaveData.Instance);
        if (berries < requires) return;
        opened = true;
        Collidable = false;
        Visible = false;
        Audio.Play("event:/game/09_core/frontdoor_unlock", Center);
        level.Shake();
    }

    public override void Render()
    {
        if (!Visible) return;
        Draw.Rect(Collider.Bounds, Calc.HexToColor("29152f"));
        for (float x = Left; x < Right; x += edge.Width) edge.Draw(new Vector2(x, Top));
        top.Draw(TopCenter, new Vector2(top.Width / 2f, top.Height));
        MTexture icon = icons[(int)(Scene.TimeActive * 10f) % icons.Length];
        icon.DrawCentered(Center);
    }
}
