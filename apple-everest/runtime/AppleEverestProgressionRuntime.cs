#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal static class AppleEverestProgressionRuntime
{
    private static readonly Dictionary<int, AppleEverestMapProgressionDescriptor> ByArea = new();
    private static readonly Dictionary<string, AppleEverestMapProgressionDescriptor> BySid = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, AppleEverestLevelSetProgressionDescriptor> ByLevelSet = new(StringComparer.Ordinal);
    private static AreaKey vanillaLastArea;
    internal static int VanillaAreaCount { get; private set; }
    internal static IReadOnlyDictionary<string, string> CompatibleMaps =>
        GeneratedAppleEverestProgressionManifest.Maps.ToDictionary(value => value.Sid, value => value.CompatibilityId, StringComparer.Ordinal);

    internal static void RegisterAreas()
    {
        if (VanillaAreaCount != 0) return;
        foreach (AppleEverestLevelSetProgressionDescriptor levelSet in GeneratedAppleEverestProgressionManifest.LevelSets)
        {
            if (string.IsNullOrEmpty(levelSet.LevelSet) || levelSet.Identity?.Length != 64 ||
                !ByLevelSet.TryAdd(levelSet.LevelSet, levelSet))
                throw new InvalidOperationException("invalid or duplicate static LevelSet: " + levelSet.LevelSet);
            string[] expected = GeneratedAppleEverestProgressionManifest.Maps
                .Where(map => map.LevelSet == levelSet.LevelSet).Select(map => map.Sid)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!expected.SequenceEqual(levelSet.MapSids ?? Array.Empty<string>(), StringComparer.Ordinal))
                throw new InvalidOperationException("static LevelSet membership mismatch: " + levelSet.LevelSet);
        }
        VanillaAreaCount = AreaData.Areas.Count;
        AreaData source = AreaData.Areas[0];
        foreach (AppleEverestMapProgressionDescriptor descriptor in GeneratedAppleEverestProgressionManifest.Maps)
        {
            if (BySid.ContainsKey(descriptor.Sid)) throw new InvalidOperationException("duplicate static map SID: " + descriptor.Sid);
            int id = AreaData.Areas.Count;
            descriptor.RuntimeAreaId = id;
            AppleEverestMapPresentationDescriptor presentation = descriptor.Presentation;
            ModeProperties mode = new()
            {
                Path = descriptor.Path,
                Checkpoints = descriptor.Checkpoints.Select(value =>
                    new CheckpointData(value, DialogKey(descriptor.Sid) + "_" + value)).ToArray(),
                Inventory = Inventory(presentation.Inventory),
                AudioState = new AudioState(presentation.Music, presentation.Ambience),
                IgnoreLevelAudioLayerData = presentation.IgnoreLevelAudioLayerData,
                TotalStrawberries = descriptor.Strawberries,
                StartStrawberries = 0
            };
            AreaData area = new()
            {
                // Everest dialog keys normalise map SIDs so authored entries
                // such as "Pack_1_Lobby_map" resolve through canonical
                // AreaData/UI consumers instead of rendering "XXX".
                ID = id, Name = DialogKey(descriptor.Sid),
                Icon = presentation.Icon == "areas/null" ? source.Icon : presentation.Icon,
                Interlude = false,
                CanFullClear = descriptor.Heart, CompleteScreenName = null,
                Mode = new ModeProperties[] { mode, null, null },
                TitleBaseColor = Calc.HexToColor(presentation.TitleBaseColor),
                TitleAccentColor = Calc.HexToColor(presentation.TitleAccentColor),
                TitleTextColor = Calc.HexToColor(presentation.TitleTextColor),
                IntroType = IntroType(presentation.IntroType), Dreaming = presentation.Dreaming,
                ColorGrade = string.IsNullOrEmpty(presentation.ColorGrade) ? null : presentation.ColorGrade,
                Wipe = Wipe(presentation.Wipe),
                DarknessAlpha = presentation.DarknessAlpha, BloomBase = presentation.BloomBase,
                BloomStrength = presentation.BloomStrength, Jumpthru = presentation.Jumpthru,
                Spike = source.Spike, CrumbleBlock = source.CrumbleBlock,
                WoodPlatform = source.WoodPlatform, CoreMode = CoreMode(presentation.CoreMode)
            };
            AreaData.Areas.Add(area);
            mode.MapData = new MapData(new AreaKey(id));
            // Pinned Celeste's MapData loader only classifies vanilla-named
            // strawberries. Static helper berries are lowered to the same
            // runtime collectible but retain their authored map entity name,
            // so Load() resets this field to its incomplete vanilla scan.
            // The closure manifest is the compiler-verified authority.
            mode.TotalStrawberries = descriptor.Strawberries;
            ByArea.Add(id, descriptor); BySid.Add(descriptor.Sid, descriptor);
        }
        AppleEverestStaticRuntime.Log($"levelset-registry=PASS vanilla={VanillaAreaCount} custom={ByArea.Count} levelsets={ByLevelSet.Count}");
    }

    private static PlayerInventory Inventory(string value) => value switch
    {
        "CH6End" => PlayerInventory.CH6End,
        "Core" => PlayerInventory.Core,
        "OldSite" => PlayerInventory.OldSite,
        "Prologue" => PlayerInventory.Prologue,
        "TheSummit" => PlayerInventory.TheSummit,
        "Farewell" => PlayerInventory.Farewell,
        _ => PlayerInventory.Default
    };

    private static string DialogKey(string sid) => sid.Replace('/', '_').Replace('-', '_');

    private static Player.IntroTypes IntroType(string value) => value switch
    {
        "Transition" => Player.IntroTypes.Transition,
        "Respawn" => Player.IntroTypes.Respawn,
        "WalkInRight" => Player.IntroTypes.WalkInRight,
        "WalkInLeft" => Player.IntroTypes.WalkInLeft,
        "Jump" => Player.IntroTypes.Jump,
        "WakeUp" => Player.IntroTypes.WakeUp,
        "Fall" => Player.IntroTypes.Fall,
        "TempleMirrorVoid" => Player.IntroTypes.TempleMirrorVoid,
        "ThinkForABit" => Player.IntroTypes.ThinkForABit,
        _ => Player.IntroTypes.None
    };

    private static Session.CoreModes CoreMode(string value) => value switch
    {
        "Hot" => Session.CoreModes.Hot,
        "Cold" => Session.CoreModes.Cold,
        _ => Session.CoreModes.None
    };

    private static Action<Scene, bool, Action> Wipe(string value) => value switch
    {
        "Celeste.CurtainWipe" => (scene, wipeIn, done) => new CurtainWipe(scene, wipeIn, done),
        "Celeste.DreamWipe" => (scene, wipeIn, done) => new DreamWipe(scene, wipeIn, done),
        "Celeste.DropWipe" => (scene, wipeIn, done) => new DropWipe(scene, wipeIn, done),
        "Celeste.FadeWipe" => (scene, wipeIn, done) => new FadeWipe(scene, wipeIn, done),
        "Celeste.FallWipe" => (scene, wipeIn, done) => new FallWipe(scene, wipeIn, done),
        "Celeste.HeartWipe" => (scene, wipeIn, done) => new HeartWipe(scene, wipeIn, done),
        "Celeste.KeyDoorWipe" => (scene, wipeIn, done) => new KeyDoorWipe(scene, wipeIn, done),
        "Celeste.MountainWipe" => (scene, wipeIn, done) => new MountainWipe(scene, wipeIn, done),
        "Celeste.SpotlightWipe" => (scene, wipeIn, done) => new SpotlightWipe(scene, wipeIn, done),
        "Celeste.StarfieldWipe" => (scene, wipeIn, done) => new StarfieldWipe(scene, wipeIn, done),
        "Celeste.WindWipe" => (scene, wipeIn, done) => new WindWipe(scene, wipeIn, done),
        _ => (scene, wipeIn, done) => new AngledWipe(scene, wipeIn, done)
    };

    internal static bool IsCustom(AreaKey area) => ByArea.ContainsKey(area.ID);
    internal static bool TryDescriptor(int areaId, out AppleEverestMapProgressionDescriptor descriptor) => ByArea.TryGetValue(areaId, out descriptor);
    internal static bool TryDescriptor(string sid, out AppleEverestMapProgressionDescriptor descriptor)
    {
        if (sid != null && BySid.TryGetValue(sid, out descriptor)) return true;
        descriptor = null;
        return false;
    }
    internal static string Sid(AreaKey area) => TryDescriptor(area.ID, out var descriptor) ? descriptor.Sid : null;
    internal static string LevelSet(AreaKey area) => TryDescriptor(area.ID, out var descriptor) ? descriptor.LevelSet : null;
    internal static string StartLevel(AreaKey area) =>
        TryDescriptor(area.ID, out var descriptor) ? descriptor.Presentation.StartLevel : null;
    internal static bool TryLevelSet(string levelSet, out AppleEverestLevelSetProgressionDescriptor descriptor)
    {
        if (levelSet != null && ByLevelSet.TryGetValue(levelSet, out descriptor)) return true;
        descriptor = null;
        return false;
    }
    internal static int VanillaMaximumArea => Math.Max(0, VanillaAreaCount - 1);

    internal static void RememberVanillaBoundary(SaveData save)
    {
        if (save != null && !IsCustom(save.LastArea)) vanillaLastArea = save.LastArea;
    }

    internal static byte[] SerializeVanillaBase(SaveData save)
    {
        if (save == null) return null;
        int keep = VanillaAreaCount > 0 ? VanillaAreaCount : save.Areas.Count;
        List<AreaStats> tail = save.Areas.Count > keep ? save.Areas.Skip(keep).ToList() : new List<AreaStats>();
        Session session = save.CurrentSession;
        AreaKey lastArea = save.LastArea;
        AreaKey safe = save.LastArea_Safe;
        try
        {
            if (tail.Count > 0) save.Areas.RemoveRange(keep, tail.Count);
            if (session != null && IsCustom(session.Area)) save.CurrentSession = null;
            if (IsCustom(save.LastArea)) save.LastArea = vanillaLastArea;
            if (IsCustom(save.LastArea_Safe)) save.LastArea_Safe = vanillaLastArea;
            return UserIO.Serialize(save);
        }
        finally
        {
            if (tail.Count > 0) save.Areas.AddRange(tail);
            save.CurrentSession = session; save.LastArea = lastArea; save.LastArea_Safe = safe;
        }
    }

    internal static void ApplySnapshot(SaveData save, AppleEverestProgressionSnapshot snapshot, bool includeSession)
    {
        if (save == null) return;
        RememberVanillaBoundary(save);
        while (save.Areas.Count > VanillaAreaCount) save.Areas.RemoveAt(save.Areas.Count - 1);
        foreach (AppleEverestMapProgressionDescriptor descriptor in GeneratedAppleEverestProgressionManifest.Maps)
        {
            AppleEverestProgressionArea stored = snapshot?.Areas.FirstOrDefault(value =>
                value.Sid == descriptor.Sid && value.CompatibilityId == descriptor.CompatibilityId);
            save.Areas.Add(stored == null ? new AreaStats(descriptor.RuntimeAreaId) : RestoreArea(descriptor, stored));
        }
        if (includeSession && snapshot?.Session != null &&
            TryDescriptor(snapshot.Session.Sid, out AppleEverestMapProgressionDescriptor sessionMap) &&
            sessionMap.CompatibilityId == snapshot.Session.CompatibilityId)
        {
            save.CurrentSession = RestoreSession(snapshot.Session, sessionMap, save.Areas[sessionMap.RuntimeAreaId]);
            save.LastArea_Safe = save.CurrentSession.Area;
        }
        else if (includeSession && save.CurrentSession != null && IsCustom(save.CurrentSession.Area))
            save.CurrentSession = null;
        AppleEverestStaticRuntime.Log($"levelset-progression=projected slot={save.FileSlot} generation={snapshot?.Generation ?? 0} session={(save.CurrentSession != null && IsCustom(save.CurrentSession.Area)).ToString().ToLowerInvariant()}");
    }

    internal static AppleEverestProgressionArea[] CaptureAreas(SaveData save, AppleEverestProgressionSnapshot selected) =>
        AppleEverestProgressionReplicaAuthority.MergeInstalledAreas(
            GeneratedAppleEverestProgressionManifest.Maps.Select(descriptor => CaptureArea(descriptor,
                save.Areas.Count > descriptor.RuntimeAreaId ? save.Areas[descriptor.RuntimeAreaId] : new AreaStats(descriptor.RuntimeAreaId))),
            selected, CompatibleMaps);

    internal static AppleEverestProgressionSession CaptureSession(SaveData save)
    {
        Session value = save?.CurrentSession;
        if (value == null || !TryDescriptor(value.Area.ID, out AppleEverestMapProgressionDescriptor descriptor)) return null;
        AudioTrackState music = value.Audio?.Music; AudioTrackState ambience = value.Audio?.Ambience;
        return new(descriptor.Sid, descriptor.CompatibilityId, (int)value.Area.Mode, value.Level,
            value.RespawnPoint.HasValue, value.RespawnPoint?.X ?? 0, value.RespawnPoint?.Y ?? 0,
            value.StartCheckpoint, value.Time, value.StartedFromBeginning, value.Deaths, value.Dashes,
            value.DashesAtLevelStart, value.DeathsInCurrentLevel, value.InArea, value.FirstLevel,
            value.Cassette, value.HeartGem, value.Dreaming, value.ColorGrade, value.LightingAlphaAdd,
            value.BloomBaseAdd, value.DarkRoomAlpha, (int)value.CoreMode, value.GrabbedGolden, value.HitCheckpoint,
            value.Inventory.Dashes, value.Inventory.DreamDash, value.Inventory.Backpack, value.Inventory.NoRefills,
            music?.Event, Parameters(music), ambience?.Event, Parameters(ambience),
            Ordered(value.Flags), Ordered(value.LevelFlags), EntityIds(value.Strawberries), EntityIds(value.DoNotLoad),
            EntityIds(value.Keys), value.Counters.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new AppleEverestProgressionCounter(item.Key, item.Value)).ToArray(),
            value.SummitGems?.ToArray() ?? Array.Empty<bool>(), value.UnlockedCSide, value.FurthestSeenLevel, value.BeatBestTime,
            value.OldStats?.Cassette ?? false, CaptureModes(value.OldStats ?? new AreaStats(descriptor.RuntimeAreaId)));
    }

    internal static Session RestoreSession(AppleEverestProgressionSession value)
    {
        if (value == null || SaveData.Instance == null ||
            !TryDescriptor(value.Sid, out AppleEverestMapProgressionDescriptor descriptor) ||
            descriptor.CompatibilityId != value.CompatibilityId ||
            SaveData.Instance.Areas.Count <= descriptor.RuntimeAreaId)
            return null;
        return RestoreSession(value, descriptor, SaveData.Instance.Areas[descriptor.RuntimeAreaId]);
    }

    internal static bool HasMeaningfulState(SaveData save, AppleEverestProgressionSnapshot selected) =>
        selected != null || save?.CurrentSession != null && IsCustom(save.CurrentSession.Area) ||
        save != null && GeneratedAppleEverestProgressionManifest.Maps.Any(map =>
            save.Areas.Count > map.RuntimeAreaId && Meaningful(save.Areas[map.RuntimeAreaId]));

    internal static void LaunchPersistent(string sid)
    {
        if (SaveData.Instance == null || SaveData.Instance.FileSlot is < 0 or > 2 || !TryDescriptor(sid, out var descriptor))
        {
            AppleEverestStaticRuntime.ShowStatus("SELECT A NUMBERED SAVE FIRST");
            return;
        }
        Input.MenuConfirm.ConsumePress(); Input.Jump.ConsumePress();
        Session session = SaveData.Instance.CurrentSession;
        if (session == null || session.Area.ID != descriptor.RuntimeAreaId || !session.InArea)
        {
            session = new Session(new AreaKey(descriptor.RuntimeAreaId));
            SaveData.Instance.StartSession(session);
        }
        Engine.Scene = new LevelLoader(session) { PlayerIntroTypeOverride = Player.IntroTypes.None };
        AppleEverestStaticRuntime.Log($"levelset-map=launch sid={descriptor.Sid} slot={SaveData.Instance.FileSlot} room={session.Level} persistent=true");
    }

    internal static void LaunchPersistentAt(string sid, string room, Vector2 spawn)
    {
        if (SaveData.Instance == null || SaveData.Instance.FileSlot is < 0 or > 2 ||
            !TryDescriptor(sid, out AppleEverestMapProgressionDescriptor descriptor) ||
            room == null || !descriptor.Rooms.Contains(room, StringComparer.Ordinal))
        {
            AppleEverestStaticRuntime.ShowStatus("SELECT A NUMBERED SAVE FIRST");
            return;
        }
        Input.MenuConfirm.ConsumePress(); Input.Jump.ConsumePress();
        Session session = new(new AreaKey(descriptor.RuntimeAreaId))
        {
            Level = room,
            RespawnPoint = spawn,
            StartedFromBeginning = true,
            FirstLevel = true
        };
        SaveData.Instance.StartSession(session);
        Engine.Scene = new LevelLoader(session) { PlayerIntroTypeOverride = Player.IntroTypes.None };
        AppleEverestStaticRuntime.Log($"levelset-map=launch-at sid={descriptor.Sid} slot={SaveData.Instance.FileSlot} room={room} persistent=true");
    }

    internal static int TotalStrawberries(string levelSet, SaveData save) => Areas(levelSet, save).Sum(value => value.TotalStrawberries);

    // Everest special berries retain normal durable EntityIDs but are
    // registered as untracked collectibles and therefore do not increase the
    // authored ordinary-berry count. Resolve the distinction from the closed
    // MapData graph; no runtime helper metadata or dynamic discovery is involved.
    internal static bool CountsAsOrdinaryStrawberry(AreaKey area, EntityID id)
    {
        if (!IsCustom(area)) return true;
        MapData map = AreaData.Get(area)?.Mode[(int)area.Mode]?.MapData;
        LevelData room = map?.Levels?.FirstOrDefault(value => value.Name == id.Level);
        EntityData entity = room?.Entities?.FirstOrDefault(value => value.ID == id.ID);
        return entity?.Name is "strawberry" or "LunaticHelper/StrawberryWithReturn";
    }
    internal static int TotalHearts(string levelSet, SaveData save) => Areas(levelSet, save).Sum(value => value.Modes.Count(mode => mode?.HeartGem == true));
    internal static bool Completed(AreaKey area, SaveData save)
    {
        if (save == null || area.ID < 0 || area.ID >= save.Areas.Count) return false;
        AreaStats stats = save.Areas[area.ID];
        int mode = (int)area.Mode;
        return mode >= 0 && mode < stats.Modes.Length && stats.Modes[mode]?.Completed == true;
    }

    internal static (int Collected, int Total) SilverBerries(string levelSet, SaveData save, string[] mapFilter = null)
    {
        if (save == null || string.IsNullOrEmpty(levelSet)) return (0, 0);
        HashSet<string> filter = mapFilter == null || mapFilter.Length == 0
            ? null : mapFilter.ToHashSet(StringComparer.Ordinal);
        int collected = 0;
        int total = 0;
        foreach (AppleEverestMapProgressionDescriptor descriptor in GeneratedAppleEverestProgressionManifest.Maps
                     .Where(value => value.LevelSet == levelSet && (filter == null || filter.Contains(value.Sid))))
        {
            AreaKey area = new(descriptor.RuntimeAreaId);
            MapData map = AreaData.Get(area)?.Mode[0]?.MapData;
            if (map == null) continue;
            HashSet<EntityID> saved = save.Areas.Count > descriptor.RuntimeAreaId
                ? save.Areas[descriptor.RuntimeAreaId].Modes[0].Strawberries : new HashSet<EntityID>();
            foreach (LevelData room in map.Levels)
                foreach (EntityData entity in room.Entities.Where(value => value.Name == "CollabUtils2/SilverBerry"))
                {
                    total++;
                    if (saved.Contains(new EntityID(room.Name, entity.ID))) collected++;
                }
        }
        return (collected, total);
    }
    internal static int TotalCassettes(string levelSet, SaveData save) => Areas(levelSet, save).Count(value => value.Cassette);
    internal static long TotalTime(string levelSet, SaveData save) => Areas(levelSet, save).Sum(value => value.TotalTimePlayed);
    internal static int TotalDeaths(string levelSet, SaveData save) => Areas(levelSet, save).Sum(value => value.TotalDeaths);
    internal static int TotalCompletions(string levelSet, SaveData save) => Areas(levelSet, save)
        .Sum(value => value.Modes.Count(mode => mode?.Completed == true));
    internal static int MaximumCompletions(string levelSet) => TryLevelSet(levelSet, out var descriptor)
        ? descriptor.MaximumCompletions : 0;

    private static IEnumerable<AreaStats> Areas(string levelSet, SaveData save) =>
        GeneratedAppleEverestProgressionManifest.Maps.Where(map => map.LevelSet == levelSet && save.Areas.Count > map.RuntimeAreaId)
            .Select(map => save.Areas[map.RuntimeAreaId]);

    private static bool Meaningful(AreaStats value) => value.Cassette || value.Modes.Any(mode => mode != null &&
        (mode.TotalStrawberries != 0 || mode.Completed || mode.SingleRunCompleted || mode.FullClear || mode.Deaths != 0 ||
         mode.TimePlayed != 0 || mode.BestTime != 0 || mode.BestFullClearTime != 0 || mode.BestDashes != 0 ||
         mode.BestDeaths != 0 || mode.HeartGem || mode.Strawberries.Count != 0 || mode.Checkpoints.Count != 0));

    private static AppleEverestProgressionArea CaptureArea(AppleEverestMapProgressionDescriptor descriptor, AreaStats value) =>
        new(descriptor.Sid, descriptor.LevelSet, descriptor.CompatibilityId, value.Cassette, CaptureModes(value));

    private static AppleEverestProgressionMode[] CaptureModes(AreaStats value) =>
        (value?.Modes ?? Array.Empty<AreaModeStats>()).Select(mode => new AppleEverestProgressionMode(mode.TotalStrawberries, mode.Completed,
                mode.SingleRunCompleted, mode.FullClear, mode.Deaths, mode.TimePlayed, mode.BestTime,
                mode.BestFullClearTime, mode.BestDashes, mode.BestDeaths, mode.HeartGem,
                EntityIds(mode.Strawberries), Ordered(mode.Checkpoints))).ToArray();

    private static AreaStats RestoreArea(AppleEverestMapProgressionDescriptor descriptor, AppleEverestProgressionArea value)
    {
        AreaStats result = new(descriptor.RuntimeAreaId) { Cassette = value.Cassette };
        for (int index = 0; index < Math.Min(result.Modes.Length, value.Modes.Length); index++)
        {
            AppleEverestProgressionMode source = value.Modes[index]; AreaModeStats target = result.Modes[index];
            target.TotalStrawberries = source.TotalStrawberries; target.Completed = source.Completed;
            target.SingleRunCompleted = source.SingleRunCompleted; target.FullClear = source.FullClear;
            target.Deaths = source.Deaths; target.TimePlayed = source.TimePlayed; target.BestTime = source.BestTime;
            target.BestFullClearTime = source.BestFullClearTime; target.BestDashes = source.BestDashes;
            target.BestDeaths = source.BestDeaths; target.HeartGem = source.HeartGem;
            target.Strawberries = source.Strawberries.Select(item => new EntityID(item.Level, item.Id)).ToHashSet();
            target.Checkpoints = source.Checkpoints.ToHashSet(StringComparer.Ordinal);
        }
        return result;
    }

    private static Session RestoreSession(AppleEverestProgressionSession value, AppleEverestMapProgressionDescriptor descriptor, AreaStats area)
    {
        AudioState audio = new(); audio.Music = Track(value.MusicEvent, value.MusicParameters); audio.Ambience = Track(value.AmbienceEvent, value.AmbienceParameters);
        Session result = new()
        {
            Area = new AreaKey(descriptor.RuntimeAreaId, (AreaMode)value.Mode), Level = value.Level,
            RespawnPoint = value.HasRespawnPoint ? new Vector2(value.RespawnX, value.RespawnY) : null,
            StartCheckpoint = value.StartCheckpoint, Time = value.Time, StartedFromBeginning = value.StartedFromBeginning,
            Deaths = value.Deaths, Dashes = value.Dashes, DashesAtLevelStart = value.DashesAtLevelStart,
            DeathsInCurrentLevel = value.DeathsInCurrentLevel, InArea = value.InArea, FirstLevel = value.FirstLevel,
            Cassette = value.Cassette, HeartGem = value.HeartGem, Dreaming = value.Dreaming,
            ColorGrade = value.ColorGrade, LightingAlphaAdd = value.LightingAlphaAdd,
            BloomBaseAdd = value.BloomBaseAdd, DarkRoomAlpha = value.DarkRoomAlpha,
            CoreMode = (Session.CoreModes)value.CoreMode, GrabbedGolden = value.GrabbedGolden,
            HitCheckpoint = value.HitCheckpoint, Inventory = new PlayerInventory(value.InventoryDashes,
                value.InventoryDreamDash, value.InventoryBackpack, value.InventoryNoRefills), Audio = audio,
            Flags = value.Flags.ToHashSet(StringComparer.Ordinal), LevelFlags = value.LevelFlags.ToHashSet(StringComparer.Ordinal),
            Strawberries = RestoreIds(value.Strawberries), DoNotLoad = RestoreIds(value.DoNotLoad), Keys = RestoreIds(value.Keys),
            Counters = value.Counters.Select(item => new Session.Counter { Key = item.Key, Value = item.Value }).ToList(),
            SummitGems = value.SummitGems.ToArray(), UnlockedCSide = value.UnlockedCSide,
            FurthestSeenLevel = value.FurthestSeenLevel, BeatBestTime = value.BeatBestTime,
            OldStats = RestoreArea(descriptor, new AppleEverestProgressionArea(descriptor.Sid, descriptor.LevelSet,
                descriptor.CompatibilityId, value.OldStatsCassette, value.OldStatsModes))
        };
        return result;
    }

    private static AudioTrackState Track(string ev, IEnumerable<AppleEverestProgressionParameter> values)
    { AudioTrackState result = new(ev); foreach (var value in values ?? Array.Empty<AppleEverestProgressionParameter>()) result.Parameters.Add(new MEP(value.Key, value.Value)); return result; }
    private static AppleEverestProgressionParameter[] Parameters(AudioTrackState value) =>
        value?.Parameters?.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new AppleEverestProgressionParameter(item.Key, item.Value)).ToArray() ?? Array.Empty<AppleEverestProgressionParameter>();
    private static AppleEverestProgressionEntityId[] EntityIds(IEnumerable<EntityID> values) =>
        (values ?? Array.Empty<EntityID>()).OrderBy(item => item.Level, StringComparer.Ordinal).ThenBy(item => item.ID)
            .Select(item => new AppleEverestProgressionEntityId(item.Level, item.ID)).ToArray();
    private static HashSet<EntityID> RestoreIds(IEnumerable<AppleEverestProgressionEntityId> values) =>
        (values ?? Array.Empty<AppleEverestProgressionEntityId>()).Select(item => new EntityID(item.Level, item.Id)).ToHashSet();
    private static string[] Ordered(IEnumerable<string> values) => (values ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
}
