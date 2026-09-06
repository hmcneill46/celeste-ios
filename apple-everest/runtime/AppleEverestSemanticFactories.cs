using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal static class AppleEverestSemanticFactories
{
    internal static CrystalColor ResolveSpinnerColor(EntityData data, CrystalColor fallback)
    {
        // Everest permits custom maps to author the otherwise chapter-bound
        // vanilla crystal palette. Keep the accepted set explicit so malformed
        // or future values fail closed to canonical Celeste's chosen colour.
        string authored = data.Attr("color");
        if (authored.Equals("Blue", StringComparison.OrdinalIgnoreCase))
            return CrystalColor.Blue;
        if (authored.Equals("Red", StringComparison.OrdinalIgnoreCase))
            return CrystalColor.Red;
        if (authored.Equals("Purple", StringComparison.OrdinalIgnoreCase))
            return CrystalColor.Purple;
        if (authored.Equals("Rainbow", StringComparison.OrdinalIgnoreCase))
            return CrystalColor.Rainbow;
        if (authored.Equals("Core", StringComparison.OrdinalIgnoreCase))
            return (CrystalColor)(-1);
        return fallback;
    }

    internal static Entity CreateEntity(string id, EntityData data, Vector2 offset, EntityID entityId) => id switch
    {
        "CollabUtils2/MiniHeart" => new AppleEverestMiniHeart(data, offset),
        "CollabUtils2/GoldenBerryPlayerRespawnPoint" => new AppleEverestGoldenBerryPlayerRespawnPoint(),
        "CollabUtils2/MiniHeartDoor" => new AppleEverestMiniHeartDoor(data, offset, entityId),
        "CollabUtils2/RainbowBerry" => new AppleEverestRainbowBerry(data, offset, entityId),
        "CollabUtils2/SilverBerry" => new AppleEverestSilverBerry(data, offset, entityId),
        "CollabUtils2/SpeedBerry" => new AppleEverestSpeedBerry(data, offset, entityId),
        "CommunalHelper/DreamMoveBlock" => CreateDreamMoveBlock(data, offset),
        "CommunalHelper/StationBlock" => new AppleEverestStationBlock(data, offset),
        "CommunalHelper/StationBlockTrack" => new AppleEverestStationTrack(data, offset),
        "LunaticHelper/StrawberryGate" => new AppleEverestStrawberryGate(data, offset),
        "MaxHelpingHand/GroupedTriggerSpikesUp" => new AppleEverestGroupedTriggerSpikesUp(data, offset),
        "MaxHelpingHand/CustomSummitCheckpoint" => new SummitCheckpoint(data, offset),
        "MaxHelpingHand/FlagSwitchGate" => new SwitchGate(data, offset),
        "MaxHelpingHand/FlagTouchSwitch" => new TouchSwitch(data, offset),
        "MaxHelpingHand/SecretBerry" => new AppleEverestSecretBerry(data, offset, entityId),
        "EeveeHelper/FlagToggleModifier" => new AppleEverestFlagToggleModifier(data, offset),
        "FancyTileEntities/FancyFakeWall" => new AppleEverestFancyFakeWall(entityId, data, offset),
        "FrostHelper/NoDashArea" => new AppleEverestNoDashArea(data, offset),
        "ShroomHelper/AttachedIceWall" => new AppleEverestAttachedIceWall(data, offset),
        "ShroomHelper/CrumbleBlockOnTouch" => new AppleEverestCrumbleBlockOnTouch(data, offset, entityId),
        _ => throw new InvalidOperationException("unregistered static semantic entity: " + id)
    };

    private static DreamBlock CreateDreamMoveBlock(EntityData data, Vector2 offset)
    {
        // Canonical Celeste deliberately tracks DreamBlock with inherited
        // tracking disabled.  Keep the gameplay entity an exact DreamBlock so
        // Player.DreamDashCheck sees it, and attach the frozen CommunalHelper
        // movement semantics as a component instead of creating a subclass.
        DreamBlock block = new(data.Position + offset, data.Width, data.Height, null,
            data.Bool("fastMoving", false), data.Bool("oneUse", false), data.Bool("below", false));
        block.StopPlayerRunIntoAnimation = false;
        block.Add(new AppleEverestDreamMoveBlockController(data));
        return block;
    }

    internal static Entity CreateTrigger(string id, EntityData data, Vector2 offset, EntityID entityId) => id switch
    {
        "CollabUtils2/ChapterPanelTrigger" => new AppleEverestChapterPanelTrigger(data, offset),
        "CollabUtils2/JournalTrigger" => new AppleEverestJournalTrigger(data, offset),
        "CollabUtils2/MiniHeartDoorUnlockCutsceneTrigger" => new AppleEverestMiniHeartDoorUnlockTrigger(data, offset),
        "CollabUtils2/RainbowBerryUnlockCutsceneTrigger" => new AppleEverestRainbowBerryUnlockTrigger(data, offset),
        "CollabUtils2/SpeedBerryCollectTrigger" => new AppleEverestSpeedBerryCollectTrigger(data, offset),
        "MaxHelpingHand/CameraCatchupSpeedTrigger" => new AppleEverestCameraCatchupTrigger(data, offset),
        "everest/changeInventoryTrigger" => new AppleEverestChangeInventoryTrigger(data, offset),
        "everest/coreModeTrigger" => new AppleEverestCoreModeTrigger(data, offset),
        "everest/crystalShatterTrigger" => new AppleEverestCrystalShatterTrigger(data, offset),
        "everest/flagTrigger" => new AppleEverestFlagTrigger(data, offset),
        "everest/smoothCameraOffsetTrigger" => new AppleEverestSmoothCameraOffsetTrigger(data, offset),
        _ => throw new InvalidOperationException("unregistered static semantic trigger: " + id)
    };
}

/// <summary>
/// Frozen ShroomHelper AttachedIceWall semantics.  This is a two-pixel climb
/// blocker carried by its neighbouring Solid; a vanilla IceBlock is a full
/// core-mode hazard and cannot represent either its collision or attachment.
/// </summary>
internal sealed class AppleEverestAttachedIceWall : Entity
{
    private readonly StaticMover staticMover;
    private readonly ClimbBlocker climbBlocker;
    private readonly List<Sprite> tiles = new();
    private Vector2 imageOffset;

    internal AppleEverestAttachedIceWall(EntityData data, Vector2 offset)
        : base(data.Position + offset)
    {
        bool left = data.Bool("left", true);
        int spriteOffset = data.Int("spriteOffset", 0);
        float height = data.Height;
        Tag = (int)Tags.TransitionUpdate;
        Depth = 1999;
        Collider = left ? new Hitbox(2f, height) : new Hitbox(2f, height, 6f);

        Add(staticMover = new StaticMover
        {
            OnShake = amount => imageOffset += amount,
            OnAttach = platform => Depth = platform.Depth + 1,
            SolidChecker = solid => CollideCheck(solid,
                Position + (left ? -Vector2.UnitX : Vector2.UnitX)),
            OnEnable = () => Visible = Collidable = true,
            OnDisable = () => Visible = Collidable = false
        });
        Add(climbBlocker = new ClimbBlocker(edge: false));

        int tileCount = Math.Max(1, (int)Math.Ceiling(height / 8f));
        for (int index = 0; index < tileCount; index++)
        {
            string spriteId = index == 0 ? "WallBoosterTop"
                : index == tileCount - 1 ? "WallBoosterBottom" : "WallBoosterMid";
            Sprite sprite = AppleEverestStaticRuntime.CreateStaticModSprite(spriteId);
            sprite.FlipX = !left;
            sprite.Position = new Vector2(left ? -spriteOffset : 4 + spriteOffset, index * 8f);
            tiles.Add(sprite);
            Add(sprite);
        }
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        // ShroomHelper intentionally waits until the entity belongs to the
        // room before enabling both the climb blocker and the ice animation.
        // Doing this in the constructor leaves the visual present but can
        // leave the carried no-grab strip inactive on static-mover attach.
        climbBlocker.Blocking = true;
        foreach (Sprite tile in tiles) tile.Play("ice");
    }

    public override void Update()
    {
        if (Scene is not Level level || !level.Transitioning) base.Update();
    }

    public override void Render()
    {
        Position += imageOffset;
        base.Render();
        Position -= imageOffset;
    }
}

/// <summary>
/// Frozen ShroomHelper touch-crumble semantics.  This must remain a tiled
/// solid: substituting Celeste's one-tile-high CrumblePlatform changes both
/// the authored appearance and collision shape of ordinary helper maps.
/// </summary>
internal sealed class AppleEverestCrumbleBlockOnTouch : Solid
{
    private readonly EntityID entityId;
    private readonly char tileType;
    private readonly bool blendIn;
    private readonly bool persistent;
    private readonly bool destroyStaticMovers;
    private float delay;
    private bool triggered;

    internal AppleEverestCrumbleBlockOnTouch(EntityData data, Vector2 offset, EntityID entityId)
        : base(data.Position + offset, data.Width, data.Height, safe: true)
    {
        this.entityId = entityId;
        tileType = data.Char("tiletype", 'm');
        blendIn = data.Bool("blendin", true);
        persistent = data.Bool("persistent", false);
        delay = data.Float("delay", 0f);
        destroyStaticMovers = data.Bool("destroyStaticMovers", false);
        Depth = -12999;
        if (SurfaceIndex.TileToIndex.TryGetValue(tileType, out int surface)) SurfaceSoundIndex = surface;
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        TileGrid tiles;
        if (!blendIn)
        {
            tiles = GFX.FGAutotiler.GenerateBox(tileType, (int)Width / 8, (int)Height / 8).TileGrid;
        }
        else
        {
            Level level = SceneAs<Level>();
            Rectangle bounds = level.Session.MapData.TileBounds;
            int x = (int)X / 8 - bounds.Left;
            int y = (int)Y / 8 - bounds.Top;
            tiles = GFX.FGAutotiler.GenerateOverlay(tileType, x, y, (int)Width / 8, (int)Height / 8,
                level.SolidsData).TileGrid;
            Depth = -10501;
        }
        Add(tiles);
        Add(new Coroutine(Sequence()));
        Add(new TileInterceptor(tiles, highPriority: true));
        Add(new LightOcclude(1f));
        if (CollideCheck<Player>()) RemoveSelf();
    }

    public override void OnStaticMoverTrigger(StaticMover mover) => triggered = true;

    private bool PlayerBreakCheck() => HasPlayerRider() || HasPlayerLeaning();

    private bool HasPlayerLeaning()
    {
        Player player = Scene?.Tracker.GetEntity<Player>();
        if (player == null) return false;
        return player.Facing == Facings.Left && CollideCheck(player, Position + Vector2.UnitX) ||
               player.Facing == Facings.Right && CollideCheck(player, Position - Vector2.UnitX);
    }

    private IEnumerator Sequence()
    {
        while (!triggered && !PlayerBreakCheck()) yield return null;
        while (delay > 0f)
        {
            delay -= Engine.DeltaTime;
            yield return null;
        }
        Break();
    }

    private void Break()
    {
        if (!Collidable || Scene == null) return;
        Audio.Play("event:/new_content/game/10_farewell/quake_rockbreak", Position);
        Collidable = false;
        for (int x = 0; x < Width / 8f; x++)
            for (int y = 0; y < Height / 8f; y++)
            {
                Rectangle tile = new((int)X + x * 8, (int)Y + y * 8, 8, 8);
                if (!Scene.CollideCheck<Solid>(tile))
                    Scene.Add(Engine.Pooler.Create<Debris>()
                        .Init(Position + new Vector2(4 + x * 8, 4 + y * 8), tileType)
                        .BlastFrom(TopCenter));
            }
        if (persistent) SceneAs<Level>().Session.DoNotLoad.Add(entityId);
        if (destroyStaticMovers) DestroyStaticMovers();
        RemoveSelf();
    }
}

internal sealed class AppleEverestGroupedTriggerSpikesUp : Entity
{
    private const float DelayTime = 0.4f;

    private readonly int size;
    private readonly string overrideType;
    private readonly bool triggerIfSameDirection;
    private readonly bool killIfSameDirection;
    private readonly Vector2 outwards = -Vector2.UnitY;
    private Vector2 shakeOffset;
    private Vector2[] spikePositions = Array.Empty<Vector2>();
    private List<MTexture> spikeTextures = new List<MTexture>();
    private bool triggered;
    private float delayTimer;
    private float lerp;
    private bool blockingLedge;

    internal AppleEverestGroupedTriggerSpikesUp(EntityData data, Vector2 offset)
        : base(data.Position + offset)
    {
        size = data.Width;
        overrideType = data.Attr("type", "default");
        triggerIfSameDirection = data.Bool("triggerIfSameDirection", false);
        killIfSameDirection = data.Bool("killIfSameDirection", triggerIfSameDirection);
        Collider = new Hitbox(size, 3f, 0f, -3f);
        Add(new SafeGroundBlocker());
        Add(new LedgeBlocker(UpSafeBlockCheck));
        Add(new PlayerCollider(OnCollide));
        Add(new StaticMover
        {
            OnShake = amount => shakeOffset += amount,
            SolidChecker = solid => CollideCheckOutside(solid, Position + Vector2.UnitY),
            JumpThruChecker = jumpThru => CollideCheck(jumpThru, Position + Vector2.UnitY),
            OnEnable = () => Collidable = Visible = true,
            OnDisable = () => Collidable = Visible = false
        });
        Depth = data.Bool("behindMoveBlocks", false) ? 0 : -50;
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        string spikeType = AreaData.Get(scene).Spike;
        if (!string.IsNullOrEmpty(overrideType) && overrideType != "default")
            spikeType = overrideType;
        if (spikeType == "tentacles")
            throw new InvalidOperationException("static grouped trigger tentacles are not supported");
        spikeTextures = GFX.Game.GetAtlasSubtextures("danger/spikes/" + spikeType + "_up");
        if (spikeTextures.Count == 0)
            throw new InvalidOperationException("static grouped trigger spike texture is unavailable: " + spikeType);
        spikePositions = new Vector2[size / 8];
        for (int index = 0; index < spikePositions.Length; index++)
            spikePositions[index] = Vector2.UnitX * (index + 0.5f) * 8f + Vector2.UnitY;
    }

    public override void Update()
    {
        base.Update();
        if (triggered && delayTimer > 0f)
        {
            delayTimer -= Engine.DeltaTime;
            if (delayTimer <= 0f)
            {
                if (CollideCheck<Player>())
                    delayTimer = 0.05f;
                else
                    Audio.Play("event:/game/03_resort/fluff_tendril_emerge", Position + spikePositions[spikePositions.Length / 2]);
            }
        }
        else if (triggered)
        {
            lerp = Calc.Approach(lerp, 1f, 8f * Engine.DeltaTime);
        }
        else
        {
            lerp = Calc.Approach(lerp, 0f, 4f * Engine.DeltaTime);
            if (lerp <= 0f) triggered = false;
        }
        if (blockingLedge == (lerp >= 1f)) return;
        blockingLedge = !blockingLedge;
        if (blockingLedge)
        {
            Add(new LedgeBlocker());
            return;
        }
        foreach (Component component in this)
            if (component is LedgeBlocker)
            {
                Remove(component);
                break;
            }
    }

    public override void Render()
    {
        base.Render();
        for (int index = 0; index < spikePositions.Length; index++)
        {
            Vector2 position = Position + shakeOffset + spikePositions[index] + outwards * (-4f + lerp * 4f);
            spikeTextures[0].DrawJustified(position, new Vector2(0.5f, 1f), Color.White);
        }
    }

    private void OnCollide(Player player)
    {
        if (player.Speed.Y < 0f && !(triggered ? killIfSameDirection : triggerIfSameDirection)) return;
        int index = (int)((player.Left - Left) / 8f);
        if (index < 0 || index >= spikePositions.Length) return;
        if (!triggered)
        {
            Audio.Play("event:/game/03_resort/fluff_tendril_touch", Position + spikePositions[index]);
            triggered = true;
            delayTimer = DelayTime;
        }
        else if (lerp >= 1f)
        {
            player.Die(outwards, evenIfInvincible: false, registerDeathInStats: true);
        }
    }

    private bool UpSafeBlockCheck(Player player)
    {
        int facingOffset = 8 * (int)player.Facing;
        int minimum = (int)((player.Left + facingOffset - Left) / 4f);
        int maximum = (int)((player.Right + facingOffset - Left) / 4f);
        if (maximum < 0 || minimum >= spikePositions.Length) return false;
        return lerp >= 1f;
    }
}

internal sealed class AppleEverestNoDashArea : Entity
{
    private static readonly float[] ParticleSpeeds = { 12f, 20f, 40f };

    private readonly PlayerCollider playerCollider;
    private readonly List<Vector2> particles = new();
    private bool colliding;
    private float flash;

    internal AppleEverestNoDashArea(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        Collider = new Hitbox(data.Width, data.Height);
        playerCollider = new PlayerCollider(OnPlayer);
        Add(playerCollider);
        Add(new DisplacementRenderHook(RenderDisplacement));

        int particleCount = (int)(Width * Height / 16f);
        for (int index = 0; index < particleCount; index++)
            particles.Add(new Vector2(Calc.Random.NextFloat(Width - 1f), Calc.Random.NextFloat(Height - 1f)));

        Vector2? node = data.FirstNodeNullable(offset);
        if (node.HasValue)
        {
            Vector2 start = Position;
            Vector2 end = node.Value;
            float duration = Vector2.Distance(start, end) / 12f;
            if (data.Bool("fastMoving", false))
                duration /= 3f;
            Tween movement = Tween.Create(Tween.TweenMode.YoyoLooping, Ease.SineInOut, duration, start: true);
            movement.OnUpdate = tween => Position = Vector2.Lerp(start, end, tween.Eased);
            Add(movement);
        }
    }

    public override void Update()
    {
        base.Update();
        Player player = Scene?.Tracker.GetEntity<Player>();
        colliding = player != null && playerCollider.Check(player);
        if (colliding && Input.Dash.Pressed)
            flash = 1f;
        if (flash > 0f)
            flash = Calc.Approach(flash, 0f, Engine.DeltaTime * 4f);

        float wrapHeight = Math.Max(1f, Height - 1f);
        for (int index = 0; index < particles.Count; index++)
        {
            Vector2 particle = particles[index] + Vector2.UnitY * ParticleSpeeds[index % ParticleSpeeds.Length] * Engine.DeltaTime;
            particle.Y %= wrapHeight;
            particles[index] = particle;
        }
    }

    public override void Render()
    {
        Draw.Rect(Collider, Color.Red * 0.25f);
        Color particleColor = Color.White * 0.5f;
        foreach (Vector2 particle in particles)
            Draw.Pixel.Draw(Position + particle, Vector2.Zero, particleColor);
        if (flash > 0f)
            Draw.Rect(Collider, Color.White * flash * 0.25f);
    }

    private void OnPlayer(Player player)
    {
        // FrostHelper suppresses a dash by continuously holding the canonical
        // cooldown just above zero. Preserve the player's dash count so the
        // ability becomes available immediately after leaving the area.
        player.dashCooldownTimer = Engine.DeltaTime + 0.000001f;
    }

    private void RenderDisplacement()
    {
        Draw.Rect(X, Y, Width, Height, new Color(0.5f, 0.5f, 0.8f, 1f));
    }
}

internal sealed class AppleEverestStationTrack : Entity
{
    private const string TextureRoot = "objects/CommunalHelper/stationBlock/tracks/";

    internal readonly bool Horizontal;
    internal Vector2 NodeA;
    internal Vector2 NodeB;
    private MTexture trackTexture;
    private List<MTexture> nodeTextures;

    internal AppleEverestStationTrack(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        Horizontal = data.Bool("horizontal", false);
        Collider = new Hitbox(Horizontal ? Math.Max(8, data.Width) : 8,
            Horizontal ? 8 : Math.Max(8, data.Height));
        NodeA = Position + new Vector2(4f, 4f);
        NodeB = Position + new Vector2(Horizontal ? Width - 4f : 4f, Horizontal ? 4f : Height - 4f);
        SetTheme(reversedControls: false);
        Depth = 5000;
    }

    internal void SetTheme(bool reversedControls)
    {
        string theme = reversedControls ? "altMoonTrack/" : "moonTrack/";
        trackTexture = GFX.Game[TextureRoot + theme + (Horizontal ? "trackH" : "trackV")];
        nodeTextures = GFX.Game.GetAtlasSubtextures(TextureRoot + theme + "node");
        if (nodeTextures.Count != 4)
            throw new InvalidOperationException("static StationBlock track atlas is incomplete");
    }

    internal void OffsetBy(Vector2 amount)
    {
        Position += amount;
        NodeA += amount;
        NodeB += amount;
    }

    public override void Render()
    {
        // Moon tracks are an animated, constantly scrolling eight-pixel pipe.
        // Preserve the original phase so a frozen phone frame agrees with the
        // JIT reference instead of looking like a static outlined rectangle.
        float length = (Horizontal ? Width : Height) - 8f;
        int phase = (int)((Scene.TimeActive * 14f) % 8f);
        for (int along = phase; along <= length; along += 8)
            trackTexture.Draw(Position + new Vector2(Horizontal ? along : 0, Horizontal ? 0 : along));
        nodeTextures[0].DrawCentered(NodeA);
        nodeTextures[0].DrawCentered(NodeB);
    }
}

internal sealed class AppleEverestStationBlock : Solid
{
    private const string TextureRoot = "objects/CommunalHelper/stationBlock/";

    private readonly bool reverseControls;
    private readonly float speedFactor;
    private readonly MTexture[,] blockTiles;
    private readonly List<MTexture> arrowFrames;
    private Vector2 movementStart;
    private Vector2 target;
    private float movementProgress;
    private float movementDelay;
    private int arrowFrame;
    private bool attached;
    private bool moving;
    private Vector2 impactScale = Vector2.One;
    private Vector2 hitOffset;

    internal AppleEverestStationBlock(EntityData data, Vector2 offset)
        : base(data.Position + offset, data.Width, data.Height, safe: true)
    {
        reverseControls = data.Attr("behavior", "Pulling") == "Pushing";
        speedFactor = Math.Max(0.1f, data.Float("speedFactor", 1f));
        target = Position;
        OnDashCollide = OnDashed;
        SurfaceSoundIndex = 7;
        Add(new LightOcclude(1f));

        MTexture blockTexture = GFX.Game[TextureRoot + "blocks/" +
            (reverseControls ? "alt_moon_block" : "moon_block")];
        MTexture[,] slices = new MTexture[3, 3];
        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                slices[x, y] = blockTexture.GetSubtexture(x * 8, y * 8, 8, 8);

        int columns = (int)Width / 8;
        int rows = (int)Height / 8;
        blockTiles = new MTexture[columns, rows];
        for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
                blockTiles[x, y] = slices[x == 0 ? 0 : (x == columns - 1 ? 2 : 1),
                    y == 0 ? 0 : (y == rows - 1 ? 2 : 1)];

        int minimum = (int)Math.Min(Width, Height);
        string arrowSize = minimum <= 16 ? "small" : (minimum <= 24 ? "med" : "big");
        string arrowTheme = reverseControls ? "altMoonArrow/" : "moonArrow/";
        arrowFrames = GFX.Game.GetAtlasSubtextures(TextureRoot + arrowTheme + arrowSize);
        if (arrowFrames.Count != 16)
            throw new InvalidOperationException("static StationBlock arrow atlas is incomplete");
        Depth = -9999;
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        AppleEverestStationTrack selectedTrack = null;
        bool selectedNodeA = false;
        float bestDistance = float.MaxValue;
        Vector2 center = Center;
        foreach (AppleEverestStationTrack track in scene.Entities.FindAll<AppleEverestStationTrack>())
        {
            float distanceA = Vector2.DistanceSquared(center, track.NodeA);
            float distanceB = Vector2.DistanceSquared(center, track.NodeB);
            float distance = Math.Min(distanceA, distanceB);
            if (distance > 32f || distance >= bestDistance) continue;
            bestDistance = distance;
            selectedTrack = track;
            selectedNodeA = distanceA <= distanceB;
        }

        if (selectedTrack == null) return;

        // CommunalHelper treats the authored StationBlock position as
        // authoritative and offsets the connected track graph onto it. Moving
        // the Solid here instead happens before later spike entities Awake,
        // so their StaticMovers never attach and are left floating in place.
        Vector2 selectedNode = selectedNodeA ? selectedTrack.NodeA : selectedTrack.NodeB;
        Vector2 trackOffset = center - selectedNode;
        List<AppleEverestStationTrack> group = FindConnectedTracks(scene, selectedTrack);
        foreach (AppleEverestStationTrack track in group)
        {
            track.OffsetBy(trackOffset);
            track.SetTheme(reverseControls);
        }

        attached = true;
        target = Position;
        selectedNode = selectedNodeA ? selectedTrack.NodeA : selectedTrack.NodeB;
        Vector2 otherNode = selectedNodeA ? selectedTrack.NodeB : selectedTrack.NodeA;
        SetArrowToward(selectedNode, otherNode);
    }

    private static List<AppleEverestStationTrack> FindConnectedTracks(
        Scene scene, AppleEverestStationTrack seed)
    {
        List<AppleEverestStationTrack> all = scene.Entities.FindAll<AppleEverestStationTrack>();
        List<AppleEverestStationTrack> group = new() { seed };
        for (int index = 0; index < group.Count; index++)
        {
            AppleEverestStationTrack current = group[index];
            foreach (AppleEverestStationTrack candidate in all)
            {
                if (group.Contains(candidate) || !TracksConnect(current, candidate)) continue;
                group.Add(candidate);
            }
        }
        return group;
    }

    private static bool TracksConnect(AppleEverestStationTrack left, AppleEverestStationTrack right) =>
        Vector2.DistanceSquared(left.NodeA, right.NodeA) <= 0.01f ||
        Vector2.DistanceSquared(left.NodeA, right.NodeB) <= 0.01f ||
        Vector2.DistanceSquared(left.NodeB, right.NodeA) <= 0.01f ||
        Vector2.DistanceSquared(left.NodeB, right.NodeB) <= 0.01f;

    public override void Update()
    {
        base.Update();
        impactScale.X = Calc.Approach(impactScale.X, 1f, 4f * Engine.DeltaTime);
        impactScale.Y = Calc.Approach(impactScale.Y, 1f, 4f * Engine.DeltaTime);
        hitOffset.X = Calc.Approach(hitOffset.X, 0f, 15f * Engine.DeltaTime);
        hitOffset.Y = Calc.Approach(hitOffset.Y, 0f, 15f * Engine.DeltaTime);
        if (!moving) return;
        if (movementDelay > 0f)
        {
            movementDelay -= Engine.DeltaTime;
            return;
        }
        float nextProgress = movementProgress + speedFactor * 2f * Engine.DeltaTime;
        movementProgress = Calc.Approach(movementProgress, 1f,
            speedFactor * 2f * Engine.DeltaTime);
        Vector2 next = Vector2.Lerp(movementStart, target, Ease.SineIn(movementProgress));
        Vector2 nextUnclamped = Vector2.Lerp(movementStart, target, Ease.SineIn(nextProgress));
        Vector2 liftSpeed = Engine.DeltaTime == 0f
            ? Vector2.Zero
            : (nextUnclamped - ExactPosition) / Engine.DeltaTime;
        MoveToX(next.X, liftSpeed.X);
        MoveToY(next.Y, liftSpeed.Y);
        if (movementProgress >= 1f)
        {
            MoveToX(target.X);
            MoveToY(target.Y);
            Safe = true;
            moving = false;
            SetArrowAtNode(Center);
        }
    }
    private DashCollisionResults OnDashed(Player player, Vector2 direction)
    {
        if (!attached || moving) return DashCollisionResults.NormalCollision;
        ReactToDashImpact(direction);
        Vector2 requested = reverseControls ? direction : -direction;
        Vector2 center = Center;
        AppleEverestStationTrack selected = null;
        Vector2 selectedTarget = center;
        float best = float.MaxValue;
        foreach (AppleEverestStationTrack track in Scene.Entities.FindAll<AppleEverestStationTrack>())
        {
            if (track.Horizontal != (requested.X != 0f)) continue;
            Vector2 from;
            Vector2 to;
            if (Vector2.DistanceSquared(center, track.NodeA) <= 36f)
            {
                from = track.NodeA; to = track.NodeB;
            }
            else if (Vector2.DistanceSquared(center, track.NodeB) <= 36f)
            {
                from = track.NodeB; to = track.NodeA;
            }
            else continue;
            Vector2 path = to - from;
            if (Vector2.Dot(path, requested) <= 0f) continue;
            float distance = Vector2.DistanceSquared(center, from);
            if (distance >= best) continue;
            best = distance; selected = track; selectedTarget = to;
        }
        if (selected == null) return DashCollisionResults.NormalCollision;
        movementStart = Position;
        movementProgress = 0f;
        target = selectedTarget - new Vector2(Width / 2f, Height / 2f);
        SetArrowToward(center, selectedTarget);
        movementDelay = 0.2f;
        Safe = false;
        moving = true;
        Audio.Play("event:/game/03_resort/forcefield_bump", Center);
        return DashCollisionResults.NormalCollision;
    }

    private void ReactToDashImpact(Vector2 direction)
    {
        impactScale = new Vector2(
            1f + Math.Abs(direction.Y) * 0.35f - Math.Abs(direction.X) * 0.35f,
            1f + Math.Abs(direction.X) * 0.35f - Math.Abs(direction.Y) * 0.35f);
        hitOffset = direction * 5f;
        StartShaking(0.2f);
        Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);
    }

    private void SetArrowAtNode(Vector2 node)
    {
        foreach (AppleEverestStationTrack track in Scene.Entities.FindAll<AppleEverestStationTrack>())
        {
            if (Vector2.DistanceSquared(node, track.NodeA) <= 36f)
            {
                SetArrowToward(track.NodeA, track.NodeB);
                return;
            }
            if (Vector2.DistanceSquared(node, track.NodeB) <= 36f)
            {
                SetArrowToward(track.NodeB, track.NodeA);
                return;
            }
        }
    }

    private void SetArrowToward(Vector2 from, Vector2 to)
    {
        Vector2 direction = to - from;
        arrowFrame = Math.Abs(direction.X) > Math.Abs(direction.Y)
            ? (direction.X > 0f ? 4 : 12)
            : (direction.Y > 0f ? 8 : 0);
    }

    public override void Render()
    {
        Vector2 shake = Shake;
        for (int x = 0; x < blockTiles.GetLength(0); x++)
            for (int y = 0; y < blockTiles.GetLength(1); y++)
            {
                Vector2 tileCenter = Position + new Vector2(x * 8 + 4, y * 8 + 4);
                Vector2 visualCenter = Center + (tileCenter - Center) * impactScale + hitOffset + shake;
                blockTiles[x, y].DrawCentered(visualCenter, Color.White, impactScale);
            }
        arrowFrames[arrowFrame].DrawCentered(Center + shake + hitOffset, Color.White, impactScale);
    }
}

internal sealed class AppleEverestFancyFakeWall : Entity
{
    private readonly EntityID entityId;
    private readonly VirtualMap<char> tileMap;
    private readonly EffectCutout cutout;
    private TileGrid tiles;
    private bool fading;

    internal AppleEverestFancyFakeWall(EntityID entityId, EntityData data, Vector2 offset)
        : base(data.Position + offset)
    {
        this.entityId = entityId;
        int columns = Math.Max(1, data.Width / 8);
        int rows = Math.Max(1, data.Height / 8);
        tileMap = new VirtualMap<char>(columns, rows, '0');
        string[] authoredRows = data.Attr("tileData", "").Replace("\r", "")
            .Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Grid collision = new(columns, rows, 8f, 8f);
        for (int y = 0; y < rows; y++)
        {
            string row = y < authoredRows.Length ? authoredRows[y] : string.Empty;
            for (int x = 0; x < columns; x++)
            {
                char tile = x < row.Length ? row[x] : '0';
                tileMap[x, y] = tile;
                collision[x, y] = tile != '0' && tile != '\0';
            }
        }
        Collider = collision;
        Depth = -13000;
        Add(cutout = new EffectCutout());
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        Level level = SceneAs<Level>();
        int columns = tileMap.Columns;
        int rows = tileMap.Rows;
        VirtualMap<char> padded = new(columns + 2, rows + 2, '0');
        Rectangle bounds = level.Session.MapData.TileBounds;
        int worldX = (int)X / 8 - bounds.Left;
        int worldY = (int)Y / 8 - bounds.Top;
        for (int x = -1; x <= columns; x++)
            for (int y = -1; y <= rows; y++)
            {
                int sourceX = worldX + x;
                int sourceY = worldY + y;
                if (sourceX >= 0 && sourceY >= 0 && sourceX < level.SolidsData.Columns && sourceY < level.SolidsData.Rows)
                    padded[x + 1, y + 1] = level.SolidsData[sourceX, sourceY];
            }
        for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
                padded[x + 1, y + 1] = tileMap[x, y];

        TileGrid generated = GFX.FGAutotiler.GenerateMap(padded, new Autotiler.Behaviour
        {
            EdgesExtend = true,
            EdgesIgnoreOutOfLevel = false,
            PaddingIgnoreOutOfLevel = false
        }).TileGrid;
        tiles = new TileGrid(8, 8, columns, rows);
        for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
                tiles.Tiles[x, y] = generated.Tiles[x + 1, y + 1];
        Add(tiles);
        Add(new TileInterceptor(tiles, highPriority: false));
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
        if (CollideCheck<Player>())
        {
            tiles.Alpha = 0f;
            cutout.Visible = false;
            fading = true;
            SceneAs<Level>().Session.DoNotLoad.Add(entityId);
        }
    }

    public override void Update()
    {
        base.Update();
        if (!fading)
        {
            Player player = CollideFirst<Player>();
            if (player != null && player.StateMachine.State != 9)
            {
                SceneAs<Level>().Session.DoNotLoad.Add(entityId);
                fading = true;
                Audio.Play("event:/game/general/secret_revealed", Center);
            }
        }
        if (fading)
        {
            tiles.Alpha = Calc.Approach(tiles.Alpha, 0f, 2f * Engine.DeltaTime);
            cutout.Alpha = tiles.Alpha;
            if (tiles.Alpha <= 0f)
                RemoveSelf();
        }
    }
}

internal sealed class AppleEverestDreamMoveBlockController : Component
{
    private readonly Vector2 moveDirection;
    private readonly int arrowIndex;
    private readonly List<MTexture> arrows;
    private readonly float moveSpeed;
    private readonly bool noCollide;
    private AppleEverestDreamMoveBlockArrow arrow;
    private bool activated;
    private float speed;

    internal AppleEverestDreamMoveBlockController(EntityData data)
        : base(active: true, visible: true)
    {
        string direction = data.Attr("direction", "Right");
        moveDirection = direction switch
        {
            "Left" => -Vector2.UnitX,
            "Up" => -Vector2.UnitY,
            "Down" => Vector2.UnitY,
            _ => Vector2.UnitX
        };
        // CommunalHelper's frozen renderer selects the eight-frame arrow
        // atlas from the movement angle. Cardinal maps therefore use the
        // exact even frames 0/2/4/6 for right/up/left/down.
        arrowIndex = direction switch { "Up" => 2, "Left" => 4, "Down" => 6, _ => 0 };
        arrows = GFX.Game.GetAtlasSubtextures("objects/CommunalHelper/dreamMoveBlock/arrow");
        if (arrows.Count != 8)
            throw new InvalidOperationException("static DreamMoveBlock arrow atlas is incomplete");
        moveSpeed = data.Bool("fast", false) ? 75f : Math.Max(1f, data.Float("moveSpeed", 60f));
        noCollide = data.Bool("noCollide", false) || data.Bool("noCollideSteer", false);
    }

    public override void EntityAdded(Scene scene)
    {
        base.EntityAdded(scene);
        if (Entity is DreamBlock block && arrow == null)
        {
            // Canonical DreamBlock.Render deliberately does not call
            // Entity.Render/base.Render, so a visible Component attached to
            // the tracked gameplay block is never rendered.  Keep the exact
            // canonical DreamBlock for gameplay/tracker compatibility and
            // compose its authored CommunalHelper arrow as one companion
            // entity with the same scene lifetime.
            arrow = new AppleEverestDreamMoveBlockArrow(block, arrows[arrowIndex]);
            scene.Add(arrow);
        }
    }

    public override void EntityRemoved(Scene scene)
    {
        if (arrow != null)
        {
            scene.Remove(arrow);
            arrow = null;
        }
        base.EntityRemoved(scene);
    }

    public override void Update()
    {
        base.Update();
        if (Entity is not DreamBlock block)
            return;
        if (!activated)
        {
            if (!block.HasPlayerRider()) return;
            activated = true;
            block.StartShaking(0.2f);
        }
        speed = Calc.Approach(speed, moveSpeed, 300f * Engine.DeltaTime);
        Vector2 amount = moveDirection * speed * Engine.DeltaTime;
        if ((!noCollide && block.CollideCheck<Solid>(block.Position + amount)) ||
            block.CollideCheck<DreamBlock>(block.Position + amount))
        {
            speed = 0f;
            activated = false;
            return;
        }
        block.MoveH(amount.X);
        block.MoveV(amount.Y);
    }
}

internal sealed class AppleEverestDreamMoveBlockArrow : Entity
{
    private readonly DreamBlock block;
    private readonly MTexture texture;

    internal AppleEverestDreamMoveBlockArrow(DreamBlock block, MTexture texture)
    {
        this.block = block ?? throw new ArgumentNullException(nameof(block));
        this.texture = texture ?? throw new ArgumentNullException(nameof(texture));
        Active = false;
        Depth = block.Depth - 1;
    }

    public override void Render()
    {
        if (block.Visible && block.Scene == Scene)
            texture.DrawCentered(block.Center, Color.White);
    }
}

internal sealed class AppleEverestChangeInventoryTrigger : Trigger
{
    internal AppleEverestChangeInventoryTrigger(EntityData data, Vector2 offset) : base(data, offset) { }
    public override void OnEnter(Player player) { base.OnEnter(player); SceneAs<Level>().Session.Inventory = PlayerInventory.TheSummit; }
}

/// <summary>
/// Static equivalent of Everest's built-in core-mode trigger.  This is an
/// Everest map-data alias rather than a helper factory, so it is part of the
/// closed core registry and never requires an Everest runtime assembly.
/// </summary>
internal sealed class AppleEverestCoreModeTrigger : Trigger
{
    private enum Modes { None, Hot, Cold, Toggle }

    private readonly Modes mode;
    private readonly bool playEffects;

    internal AppleEverestCoreModeTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        _ = Enum.TryParse(data.Attr("mode", "None"), ignoreCase: true, out mode);
        playEffects = data.Bool("playEffects", true);
    }

    public override void OnEnter(Player player)
    {
        base.OnEnter(player);
        if (Scene is not Level level)
            return;

        Session.CoreModes next = mode switch
        {
            Modes.Hot => Session.CoreModes.Hot,
            Modes.Cold => Session.CoreModes.Cold,
            Modes.Toggle when level.CoreMode == Session.CoreModes.Hot => Session.CoreModes.Cold,
            Modes.Toggle when level.CoreMode == Session.CoreModes.Cold => Session.CoreModes.Hot,
            _ => Session.CoreModes.None
        };
        if (level.CoreMode == next)
            return;

        level.CoreMode = next;
        if (playEffects)
        {
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            level.Flash(Color.White * 0.15f, drawPlayerOver: true);
            Celeste.Freeze(0.05f);
        }
    }
}

/// <summary>
/// Static equivalent of Everest's built-in crystal-shatter trigger.  Kayonara
/// uses the modern Contained/All mode attribute; the legacy boolean spelling
/// is retained because it is still part of Everest's ordinary-map contract.
/// </summary>
internal sealed class AppleEverestCrystalShatterTrigger : Trigger
{
    private enum Modes { Contained, All }
    private readonly Modes mode;

    internal AppleEverestCrystalShatterTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        if (data.Has("mode"))
            _ = Enum.TryParse(data.Attr("mode", "Contained"), ignoreCase: true, out mode);
        else
            mode = data.Bool("destroyEveryCrystal", false) ? Modes.All : Modes.Contained;
    }

    public override void OnEnter(Player player)
    {
        base.OnEnter(player);
        if (Scene == null)
            return;

        List<Entity> spinners = Scene.Tracker.GetEntities<CrystalStaticSpinner>();
        if (mode == Modes.All && spinners.Count != 0)
            Audio.Play("event:/game/06_reflection/boss_spikes_burst");
        foreach (Entity entity in spinners)
        {
            if (entity is not CrystalStaticSpinner spinner)
                continue;
            bool wasCollidable = spinner.Collidable;
            spinner.Collidable = true;
            if (mode == Modes.All || CollideCheck(spinner))
                spinner.Destroy();
            spinner.Collidable = wasCollidable;
        }
        RemoveSelf();
    }
}

internal sealed class AppleEverestFlagTrigger : Trigger
{
    private enum Modes { OnPlayerEnter, OnPlayerLeave, OnLevelStart }
    private readonly string flag;
    private readonly bool state;
    private readonly Modes mode;
    private readonly bool onlyOnce;
    private readonly int deathCount;
    private bool triggered;
    internal AppleEverestFlagTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        flag = data.Attr("flag"); state = data.Bool("state");
        mode = data.Enum("mode", Modes.OnPlayerEnter);
        onlyOnce = data.Bool("only_once", false);
        deathCount = data.Int("death_count", -1);
    }
    public override void Awake(Scene scene)
    { base.Awake(scene); if (mode == Modes.OnLevelStart) SetFlag(); }
    // The source overrides intentionally do not change PlayerIsInside.
    public override void OnEnter(Player player) { if (mode == Modes.OnPlayerEnter) SetFlag(); }
    public override void OnLeave(Player player) { if (mode == Modes.OnPlayerLeave) SetFlag(); }
    private void SetFlag()
    {
        if (triggered) return;
        Session session = SceneAs<Level>().Session;
        if (deathCount >= 0 && session.DeathsInCurrentLevel != deathCount) return;
        session.SetFlag(flag, state);
        if (onlyOnce) triggered = true;
    }
}

internal sealed class AppleEverestSmoothCameraOffsetTrigger : Trigger
{
    private readonly Vector2 from;
    private readonly Vector2 to;
    private readonly PositionModes mode;
    private readonly bool onlyOnce, xOnly, yOnly;
    internal AppleEverestSmoothCameraOffsetTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        from = new Vector2(data.Float("offsetXFrom") * 48f, data.Float("offsetYFrom") * 32f);
        to = new Vector2(data.Float("offsetXTo") * 48f, data.Float("offsetYTo") * 32f);
        _ = Enum.TryParse(data.Attr("positionMode", "NoEffect"), out mode);
        onlyOnce = data.Bool("onlyOnce");
        xOnly = data.Bool("xOnly");
        yOnly = data.Bool("yOnly");
    }
    public override void OnStay(Player player)
    {
        base.OnStay(player);
        if (!yOnly) SceneAs<Level>().CameraOffset.X = MathHelper.Lerp(from.X, to.X, GetPositionLerp(player, mode));
        if (!xOnly) SceneAs<Level>().CameraOffset.Y = MathHelper.Lerp(from.Y, to.Y, GetPositionLerp(player, mode));
    }
    public override void OnLeave(Player player) { base.OnLeave(player); if (onlyOnce) RemoveSelf(); }
}

internal sealed class AppleEverestCameraCatchupTrigger : Trigger
{
    internal readonly float CatchupSpeed;
    private readonly bool revertOnLeave;

    internal AppleEverestCameraCatchupTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        CatchupSpeed = Math.Max(0.000001f, data.Float("catchupSpeed", 1f));
        revertOnLeave = data.Bool("revertOnLeave", true);
    }

    public override void OnEnter(Player player)
    {
        base.OnEnter(player);
        if (!revertOnLeave && Scene is Level level)
            AppleEverestCameraCatchupRuntime.SetPersistent(level.Session, CatchupSpeed);
    }
}

/// <summary>
/// Static equivalent of MaxHelpingHand's CameraCatchupSpeedTrigger IL patch.
/// The original helper replaces the divisor in Celeste.Player's camera
/// interpolation expression; it never writes Camera.Position from OnEnter.
/// Keeping the session value weakly keyed also preserves revertOnLeave=false
/// without leaking completed custom-map sessions.
/// </summary>
internal static class AppleEverestCameraCatchupRuntime
{
    private sealed class SessionState
    {
        internal float Speed;
    }

    private static readonly ConditionalWeakTable<Session, SessionState> SessionStates = new();

    internal static void SetPersistent(Session session, float speed) =>
        SessionStates.GetValue(session, static _ => new SessionState()).Speed = speed;

    internal static float ResolveDivisor(float original, Player player)
    {
        if (player?.Scene != null)
        {
            foreach (AppleEverestCameraCatchupTrigger trigger in
                     player.Scene.Entities.FindAll<AppleEverestCameraCatchupTrigger>())
            {
                if (player.CollideCheck(trigger))
                    return trigger.CatchupSpeed;
            }
        }

        if (player?.Scene is Level level &&
            SessionStates.TryGetValue(level.Session, out SessionState state))
            return state.Speed;

        return original;
    }
}
