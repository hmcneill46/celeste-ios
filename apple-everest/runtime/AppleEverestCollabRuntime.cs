using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

internal static class AppleEverestCollabRuntime
{
    private const string ContinueCheckpoint = "collabutils_continue";
    private static readonly Dictionary<string, AppleEverestCollabMapDescriptor> Maps =
        GeneratedAppleEverestCollabManifest.Collabs.SelectMany(collab => collab.Maps)
            .ToDictionary(map => map.Sid, StringComparer.Ordinal);

    private static AppleEverestSceneWrappingEntity<Overworld> overworldWrapper;
    private static AreaKey previousArea;
    private static bool hasPreviousArea;
    private static string forcedMapSid;
    private static string forcedJournalLevelSet;
    private static string returnMode;
    private static bool allowSaving;
    private static readonly AppleEverestCollabChapterCredits chapterCredits = new();
    internal static AppleEverestCollabSession Route => null;
    internal static bool IsOpen => overworldWrapper != null;
    private static AppleEverestCollabSession restartRoute;
    private static int restartArea = -1;
    private static bool pauseTimerOnNextLobbyLoad;
    private static Level waitingForLobbyInput;

    internal static void RememberRestartRoute(Session session)
    {
        restartRoute = Route?.Copy();
        restartArea = session.Area.ID;
    }

    internal static AppleEverestCollabSession TakeRestartRoute(Session session)
    {
        AppleEverestCollabSession retained = restartArea == session.Area.ID ? restartRoute : null;
        restartRoute = null;
        restartArea = -1;
        // Exact same-area teleport preservation from the LevelLoader hook.
        if (Engine.Scene is Level previous && previous.Session != session && previous.Session.Area.ID == session.Area.ID)
            retained = Route?.Copy();
        return retained;
    }

    internal static void AfterPlayerUpdate(Player player)
    {
        if (waitingForLobbyInput != null && player.Scene == waitingForLobbyInput && player.InControl &&
            (Input.MoveX.Value != 0 || Input.MoveY.Value != 0 || Input.Grab.Check || Input.Jump.Check || Input.Dash.Check || Input.CrouchDash.Check))
        {
            waitingForLobbyInput.TimerStopped = false;
            waitingForLobbyInput = null;
        }
    }

    internal static bool IsSubordinate(AreaKey area) =>
        AppleEverestProgressionRuntime.Sid(area) is string sid && Maps.ContainsKey(sid);

    internal static void OnLevelLoaded(Level level)
    {
        restartRoute = null;
        restartArea = -1;
        waitingForLobbyInput = pauseTimerOnNextLobbyLoad ? level : null;
        pauseTimerOnNextLobbyLoad = false;
        if (waitingForLobbyInput != null) level.TimerStopped = true;
        string sid = AppleEverestProgressionRuntime.Sid(level.Session.Area);
        if (Route != null && Route.LobbySID == null && sid != null && Maps.TryGetValue(sid, out var direct))
        {
            Route.LobbySID = direct.LobbySid;
            Route.LobbyRoom = direct.ReturnRoom;
            Route.LobbySpawnPointX = direct.ReturnX;
            Route.LobbySpawnPointY = direct.ReturnY;
            Route.SaveAndReturnToLobbyAllowed = direct.AllowSaving;
        }
        AppleEverestCollabDescriptor collab = GeneratedAppleEverestCollabManifest.Collabs
            .SingleOrDefault(value => value.LobbySid == sid);
        if (collab == null || SaveData.Instance == null) return;
        foreach (AppleEverestCollabMapDescriptor map in collab.Maps)
        {
            if (!AppleEverestProgressionRuntime.TryDescriptor(map.Sid, out AppleEverestMapProgressionDescriptor descriptor) ||
                SaveData.Instance.Areas.Count <= descriptor.RuntimeAreaId) continue;
            bool complete = SaveData.Instance.Areas[descriptor.RuntimeAreaId].Modes.Any(mode => mode?.HeartGem == true);
            level.Session.SetFlag("CollabUtils2_MapCompleted_" + Basename(map.Sid), complete);
        }
    }

    internal static void OpenChapterPanel(Player player, string sid, string mode = "SetReturnToHere", bool saving = true)
    {
        if (player?.Scene is not Level || IsOpen || player.StateMachine.State == 11 || !DestinationAvailable(sid)) return;
        AreaData area = ResolveArea(sid);
        if (!Dialog.Has(area.Name + "_collabcredits") && area.Mode[0].Checkpoints?.Length > 0) saving = false;
        returnMode = mode;
        allowSaving = saving;
        OpenOverworld(player, sid, journalLevelSet: null, chapter: true);
    }

    internal static void OpenJournal(Player player, string levelSet)
    {
        AppleEverestCollabDescriptor collab = GeneratedAppleEverestCollabManifest.Collabs.SingleOrDefault(value =>
            value.Maps.Any(map => AppleEverestProgressionRuntime.TryDescriptor(map.Sid, out var descriptor) &&
                                  descriptor.LevelSet == levelSet));
        string areaSid = collab?.Maps.OrderBy(value => value.Order).FirstOrDefault()?.Sid;
        OpenOverworld(player, areaSid, levelSet, chapter: false);
    }

    internal static string ChapterSubtitle(AreaKey area, string fallback)
    {
        string sid = AppleEverestProgressionRuntime.Sid(area);
        return sid != null && Maps.TryGetValue(sid, out AppleEverestCollabMapDescriptor map)
            ? map.Author : fallback;
    }

    private static bool IsForcedChapterPanel(OuiChapterPanel panel) =>
        overworldWrapper != null && forcedMapSid != null && panel?.Overworld == overworldWrapper.WrappedScene;

    internal static void ConfigureChapterPanel(OuiChapterPanel panel)
    {
        if (!IsForcedChapterPanel(panel)) return;
        OuiChapterSelect select = panel.Overworld.GetUI<OuiChapterSelect>();
        OuiChapterSelectIcon icon = select?.AppleEverestIcon(panel.Area.ID);
        if (icon == null) return;
        icon.SnapToSelected();
        icon.Add(new Coroutine(UpdateChapterIcon(panel, icon)));
    }

    private static IEnumerator UpdateChapterIcon(OuiChapterPanel panel, OuiChapterSelectIcon icon)
    {
        Overworld overworld = overworldWrapper?.WrappedScene;
        if (overworld == null) yield break;
        while (overworld.Current == panel || overworld.Last == panel || overworld.Next == panel)
        {
            icon.Position = panel.Position + panel.IconOffset;
            yield return null;
        }
    }

    internal static float ChapterAuthorOffset(OuiChapterPanel panel, float fallback) =>
        IsForcedChapterPanel(panel) ? 43f : fallback;

    internal static float ChapterTitleOffset(OuiChapterPanel panel, float fallback) =>
        IsForcedChapterPanel(panel) ? -49f : fallback;

    internal static int ChapterSwapHeight(OuiChapterPanel panel, int fallback) =>
        IsForcedChapterPanel(panel) && panel.selectingMode
            ? AppleEverestCollabChapterCredits.HasCredits(panel.Area) ? 730 : UsesSyntheticBookmarks(forcedMapSid) ? 300 : fallback
            : fallback;

    internal static bool ShouldShowChapterDeaths(OuiChapterPanel panel) =>
        IsForcedChapterPanel(panel);

    internal static string ChapterCardTexture(OuiChapterPanel panel, string texture)
    {
        if (!IsForcedChapterPanel(panel) ||
            (texture != "areaselect/cardtop_golden" && texture != "areaselect/card_golden")) return texture;
        var berries = AreaData.Get(panel.Area).Mode[(int)panel.Area.Mode].MapData.Goldenberries;
        string kind = berries.Any(berry => berry.Name == "CollabUtils2/RainbowBerry") ? "rainbow" :
            berries.Any(berry => berry.Name == "CollabUtils2/SilverBerry") ? "silver" : null;
        return kind == null ? texture : "CollabUtils2/chapterCard/" +
            (texture == "areaselect/cardtop_golden" ? "cardtop_" : "card_") + kind;
    }

    internal static bool NeedsChapterCheckpointPage(OuiChapterPanel panel) =>
        IsForcedChapterPanel(panel) && (AppleEverestCollabChapterCredits.HasCredits(panel.Area) ||
            UsesSyntheticBookmarks(forcedMapSid) && AppleEverestProgressionPersistence.HasSuspendedSession(forcedMapSid));

    private static bool UsesSyntheticBookmarks(string sid) =>
        sid != null && (sid == forcedMapSid ? allowSaving : Maps.TryGetValue(sid, out AppleEverestCollabMapDescriptor map) && map.AllowSaving) &&
        AppleEverestProgressionRuntime.TryDescriptor(sid, out AppleEverestMapProgressionDescriptor descriptor) &&
        descriptor.Checkpoints.Length == 0;

    internal static void ConfigureChapterCheckpoints(OuiChapterPanel panel)
    {
        if (!NeedsChapterCheckpointPage(panel)) return;
        chapterCredits.Prepare(panel.Area);
        bool hasSaved = AppleEverestProgressionPersistence.HasSuspendedSession(forcedMapSid);
        Color startColor = panel.checkpoints.FirstOrDefault()?.BgColor ?? Calc.HexToColor("eabe26");
        Color continueColor = panel.checkpoints.Skip(1).FirstOrDefault()?.BgColor ?? Calc.HexToColor("3c6180");
        panel.checkpoints.Clear();
        panel.checkpoints.Add(new OuiChapterPanel.Option
        {
            Label = Dialog.Clean(hasSaved ? "collabutils2_chapterpanel_start" : "overworld_start"),
            BgColor = startColor,
            Bg = GFX.Gui["areaselect/tab"],
            Icon = GFX.Gui["areaselect/startpoint"],
            CheckpointRotation = Calc.Random.Choose(-1, 1) * Calc.Random.Range(0.05f, 0.2f),
            CheckpointOffset = new Vector2(Calc.Random.Range(-16, 16), Calc.Random.Range(-16, 16)),
            Large = false,
            Siblings = hasSaved ? 2 : 1
        });
        if (hasSaved) panel.checkpoints.Add(new OuiChapterPanel.Option
        {
            Label = Dialog.Clean("collabutils2_chapterpanel_continue"),
            BgColor = continueColor,
            Bg = GFX.Gui["areaselect/tab"],
            Icon = GFX.Gui["areaselect/checkpoint"],
            CheckpointRotation = Calc.Random.Choose(-1, 1) * Calc.Random.Range(0.05f, 0.2f),
            CheckpointOffset = new Vector2(Calc.Random.Range(-16, 16), Calc.Random.Range(-16, 16)),
            Large = false,
            Siblings = 2,
            CheckpointLevelName = ContinueCheckpoint
        });
        panel.option = hasSaved ? 1 : 0;
        AppleEverestStaticRuntime.Log($"collab-chapter-bookmarks=ready sid={forcedMapSid} choices={(hasSaved ? "start-over,continue" : "start")} selected={(hasSaved ? "continue" : "start")} credits={AppleEverestCollabChapterCredits.HasCredits(panel.Area)}");
    }

    internal static bool ShouldDrawVanillaCheckpoint(OuiChapterPanel panel) =>
        !NeedsChapterCheckpointPage(panel);

    internal static void DrawChapterCredits(OuiChapterPanel panel, Vector2 center, int checkpointIndex, float height)
    {
        if (IsForcedChapterPanel(panel)) chapterCredits.Draw(center, checkpointIndex, height);
    }

    internal static string CheckpointPreviewName(AreaKey area, string level)
    {
        string sid = AppleEverestProgressionRuntime.Sid(area);
        if (sid == null || !Maps.ContainsKey(sid)) return null;
        string mode = area.Mode switch
        {
            AreaMode.BSide => "B",
            AreaMode.CSide => "C",
            _ => "A"
        };
        string key = sid + "/" + mode + "/" + (level ?? "start");
        return MTN.Checkpoints.Has(key) ? key : null;
    }

    internal static bool TryStartChapterPanel(OuiChapterPanel panel, string checkpoint)
    {
        if (overworldWrapper == null || forcedMapSid == null || panel?.Overworld != overworldWrapper.WrappedScene ||
            Engine.Scene is not Level level)
            return false;
        if (!DestinationAvailable(forcedMapSid)) return true;
        panel.Focused = false;
        panel.EnteringChapter = true;
        Audio.Play("event:/ui/world_map/chapter/checkpoint_start");
        AppleEverestStaticRuntime.Log($"collab-map=transition-begin sid={forcedMapSid} choice={(checkpoint == ContinueCheckpoint ? "continue" : "start-over")}");
        level.Add(new AppleEverestCollabTransition(StartSelectedMap(panel, forcedMapSid, checkpoint)));
        return true;
    }

    internal static void ConfigureJournalPages(OuiJournal journal)
    {
        if (overworldWrapper == null || forcedJournalLevelSet == null || journal?.Overworld != overworldWrapper.WrappedScene)
            return;
        journal.Pages.Clear();
        journal.Pages.Add(new OuiJournalCover(journal));
        journal.Pages.Add(new AppleEverestCollabJournalProgress(journal, forcedJournalLevelSet));
    }

    internal static void AddPauseMenuItem(Level level, TextMenu menu)
    {
        string sid = AppleEverestProgressionRuntime.Sid(level.Session.Area);
        bool saving;
        if (Route?.LobbySID != null) saving = Route.SaveAndReturnToLobbyAllowed;
        else if (sid != null && Maps.TryGetValue(sid, out AppleEverestCollabMapDescriptor map)) saving = map.AllowSaving;
        else return;
        TextMenu.Item item = null;
        item = new TextMenu.Button(Dialog.Clean("collabutils2_returntolobby")).Pressed(() =>
        {
            int returnIndex = menu.IndexOf(item);
            level.PauseMainMenuOpen = false;
            menu.RemoveSelf();
            OpenReturnToLobbyConfirmMenu(level, returnIndex, saving);
        });
        menu.AppleEverestReplaceReturnButton(Dialog.Clean("menu_pause_return"), item);
        (item as TextMenu.Button).ConfirmSfx = "event:/ui/main/message_confirm";
    }

    internal static void CompleteMapAndReturn(Level level)
    {
        if (!IsSubordinate(level.Session.Area)) return;
        level.Session.HeartGem = true;
        SaveData.Instance?.RegisterHeartGem(level.Session.Area);
        level.TimerStopped = true;
        level.RegisterAreaComplete();
        level.PauseLock = true;
        BeginCompletedReturn(level);
    }

    internal static void BeginCompletedReturn(Level level)
    {
        // The authored wipe runs in the completing level. The destination
        // session and completion progress are committed together afterward.
        level.DoScreenWipe(false, () => ReturnNow(level), false);
    }

    private static void OpenReturnToLobbyConfirmMenu(Level level, int returnIndex, bool allowSaving)
    {
        level.Paused = true;
        TextMenu menu = new()
        {
            AutoScroll = false,
            Position = new Vector2(Engine.Width / 2f, Engine.Height / 2f - 100f)
        };
        menu.Add(new TextMenu.Header(Dialog.Clean("collabutils2_returntolobby_confirm_title")));
        if (allowSaving)
        {
            menu.Add(new TextMenu.SubHeader(Dialog.Clean("collabutils2_returntolobby_confirm_note1")));
            menu.Add(new TextMenu.SubHeader(Dialog.Clean("collabutils2_returntolobby_confirm_note2")));
            menu.Add(new TextMenu.SubHeader(""));
            menu.Add(new TextMenu.Button(Dialog.Clean("collabutils2_returntolobby_confirm_save"))
                .Pressed(() => ReturnToLobby(level, menu, save: true)));
            menu.Add(new TextMenu.Button(Dialog.Clean("collabutils2_returntolobby_confirm_donotsave"))
                .Pressed(() => ReturnToLobby(level, menu, save: false)));
            menu.Add(new TextMenu.Button(Dialog.Clean("collabutils2_returntolobby_confirm_cancel"))
                .Pressed(() => menu.OnCancel()));
        }
        else
        {
            // CollabUtils2 deliberately reuses Celeste's compact Yes/Cancel
            // return confirmation when the chapter-panel trigger disallows
            // suspension. The no-save choice must still be explicit.
            menu.Add(new TextMenu.Button(Dialog.Clean("menu_return_continue"))
                .Pressed(() => ReturnToLobby(level, menu, save: false)));
            menu.Add(new TextMenu.Button(Dialog.Clean("menu_return_cancel"))
                .Pressed(() => menu.OnCancel()));
        }
        menu.OnPause = menu.OnESC = () =>
        {
            menu.RemoveSelf();
            level.Paused = false;
            Engine.FreezeTimer = 0.15f;
            Audio.Play("event:/ui/game/unpause");
        };
        menu.OnCancel = () =>
        {
            Audio.Play("event:/ui/main/button_back");
            menu.RemoveSelf();
            level.Pause(returnIndex, minimal: false);
        };
        level.Add(menu);
    }

    private static void ReturnToLobby(Level level, TextMenu menu, bool save)
    {
        Engine.TimeRate = 1f;
        menu.Focused = false;
        Audio.SetMusic(null);
        Audio.BusStopAll("bus:/gameplay_sfx", immediate: true);
        if (save)
        {
            level.Session.InArea = true;
            level.Session.Deaths++;
            level.Session.DeathsInCurrentLevel++;
            SaveData.Instance?.AddDeath(level.Session.Area);
        }
        level.DoScreenWipe(false, () =>
        {
            if (save) AppleEverestProgressionPersistence.SuspendCurrentSession();
            ReturnNow(level);
        }, false);
        foreach (LevelEndingHook hook in level.Tracker.GetComponents<LevelEndingHook>())
            hook.OnEnd?.Invoke();
    }

    private static void ReturnNow(Level level)
    {
        string sid = AppleEverestProgressionRuntime.Sid(level.Session.Area);
        string lobby, room;
        Vector2 spawn;
        if (Route?.LobbySID != null)
        {
            lobby = Route.LobbySID; room = Route.LobbyRoom;
            spawn = new Vector2(Route.LobbySpawnPointX, Route.LobbySpawnPointY);
        }
        else if (sid != null && Maps.TryGetValue(sid, out AppleEverestCollabMapDescriptor map))
        {
            lobby = map.LobbySid; room = map.ReturnRoom;
            spawn = new Vector2(map.ReturnX, map.ReturnY);
        }
        else return;
        level.EndPauseEffects();
        Audio.SetMusic(null);
        Audio.BusStopAll("bus:/gameplay_sfx", immediate: true);
        Engine.Scene = new ReturnScene(lobby, room, spawn);
    }

    private sealed class ReturnScene : Scene
    {
        private readonly string sid, room;
        private readonly Vector2 spawn;
        internal ReturnScene(string sid, string room, Vector2 spawn)
        { this.sid = sid; this.room = room; this.spawn = spawn; }
        public override void Begin()
        {
            base.Begin();
            Add(new HudRenderer());
            SaveLoadIcon.Show(this);
            Entity routine = new();
            routine.Add(new Coroutine(Return()));
            Add(routine);
        }
        private IEnumerator Return()
        {
            Session next = new(new AreaKey(ResolveArea(sid).ID))
            {
                FirstLevel = false, StartedFromBeginning = false,
                Level = room, RespawnPoint = spawn
            };
            SaveData.Instance.StartSession(next);
            UserIO.SaveHandler(file: true, settings: true);
            while (UserIO.Saving) yield return null;
            while (SaveLoadIcon.OnScreen) yield return null;
            pauseTimerOnNextLobbyLoad = true;
            LevelEnter.Go(next, fromSaveData: false);
        }
    }

    internal static bool DestinationAvailable(string sid) => sid != null &&
        AppleEverestMapBinding.DestinationAvailability(sid) == "AVAILABLE_SELECTED" &&
        AppleEverestProgressionRuntime.TryDescriptor(sid, out _);

    private static AreaData ResolveArea(string sid) => DestinationAvailable(sid) && AppleEverestProgressionRuntime.TryDescriptor(sid, out var descriptor)
        ? AreaData.Get(descriptor.RuntimeAreaId) : throw new InvalidOperationException("Collab destination is unavailable: " + sid);

    private static void OpenOverworld(Player player, string areaSid, string journalLevelSet, bool chapter)
    {
        if (player?.Scene is not Level level || overworldWrapper != null || player.StateMachine.State == 11 ||
            SaveData.Instance == null || !DestinationAvailable(areaSid))
            return;

        player.Drop();
        player.StateMachine.State = 11;
        previousArea = SaveData.Instance.LastArea;
        hasPreviousArea = true;
        SaveData.Instance.LastArea = new AreaKey(ResolveArea(areaSid).ID);
        SaveData.Instance.LastArea_Safe = SaveData.Instance.LastArea;
        forcedMapSid = chapter ? areaSid : null;
        chapterCredits.Clear();
        forcedJournalLevelSet = journalLevelSet;

        if (chapter) AppleEverestOuiEnterChapterPanel.Start = true;
        else AppleEverestOuiEnterJournal.Start = true;
        HiresSnow snow = new(0.45f) { Alpha = 0f, ParticleAlpha = 0.25f };
        Overworld overworld = new(new OverworldLoader((Overworld.StartMode)(-1), snow));
        overworldWrapper = new AppleEverestSceneWrappingEntity<Overworld>(overworld);
        overworldWrapper.OnBegin += scene =>
        {
            Renderer mountain = scene.RendererList.Renderers.FirstOrDefault(renderer => renderer is MountainRenderer);
            Renderer wipe = scene.RendererList.Renderers.FirstOrDefault(renderer => renderer is ScreenWipe);
            if (mountain != null) scene.RendererList.Remove(mountain);
            if (wipe != null) scene.RendererList.Remove(wipe);
            scene.RendererList.UpdateLists();
            level.Session.Audio.Apply();
        };
        overworldWrapper.OnEnd += scene =>
        {
            if (overworldWrapper?.WrappedScene == scene) overworldWrapper = null;
        };
        level.Add(overworldWrapper);
        overworldWrapper.Add(new Coroutine(WrappedOverworldRoutine(level, overworldWrapper)));
    }

    internal static bool IsFactoryInteractionMap(string sid) => sid != null &&
        Maps.TryGetValue(sid, out AppleEverestCollabMapDescriptor map) &&
        map.LobbySid == "AppleEverestStage25KJ/0-Lobbies/1-Fixture";

    private static IEnumerator StartSelectedMap(OuiChapterPanel panel, string sid, string checkpoint)
    {
        panel.Add(new Coroutine(panel.EaseOut(removeChildren: false)));
        yield return 0.2f;
        if (Engine.Scene is Level level)
        {
            ScreenWipe.WipeColor = Color.Black;
            new FadeWipe(level, wipeIn: false);
        }
        Audio.SetMusic(null);
        Audio.SetAmbience(null);
        yield return 0.35f;
        AppleEverestCollabSession route = CaptureReturnRoute(Engine.Scene as Level);
        if (Engine.Scene is Level current) CloseOverworld(current, resetPlayer: false);
        bool authoredDebug = AppleEverestStaticRuntime.NonPersistentModSession && SaveData.Instance?.FileSlot == 4 &&
            IsFactoryInteractionMap(sid);
        if (SaveData.Instance == null || (SaveData.Instance.FileSlot is < 0 or > 2) && !authoredDebug)
            yield break;
        AppleEverestProgressionRuntime.TryDescriptor(sid, out AppleEverestMapProgressionDescriptor descriptor);
        bool continueSession = checkpoint == ContinueCheckpoint;
        Session session = null;
        if (continueSession)
            AppleEverestProgressionPersistence.TryTakeSuspendedSession(sid, out session);
        else
            AppleEverestProgressionPersistence.DiscardSuspendedSession(sid);
        // MapData.StartLevel is patched to consult the immutable presentation
        // descriptor, matching Everest before Session initializes its level,
        // intro, inventory and restart semantics.
        string sessionCheckpoint = ResolveSessionCheckpoint(checkpoint);
        session ??= new Session(new AreaKey(ResolveArea(sid).ID), sessionCheckpoint);
        AppleEverestStaticRuntime.Log($"collab-map=session-ready sid={sid} choice={(continueSession ? "continue" : "start-over")} room={session.Level} checkpoint={session.StartCheckpoint ?? "<none>"} beginning={session.StartedFromBeginning}");
        SaveData.Instance.StartSession(session);
        route.CopyTo(Route);
        AppleEverestStaticRuntime.Log($"collab-map=session-started sid={sid} choice={(continueSession ? "continue" : "start-over")}");
        UserIO.SaveHandler(file: true, settings: false);
        AppleEverestStaticRuntime.Log($"collab-map=save-started sid={sid} choice={(continueSession ? "continue" : "start-over")}");
        while (UserIO.Saving) yield return null;
        AppleEverestStaticRuntime.Log($"collab-map=handoff sid={sid} choice={(continueSession ? "continue" : "start-over")} room={session.Level}");
        if (descriptor != null) EnterSelectedMap(session, descriptor, continueSession);
        else LevelEnter.Go(session, fromSaveData: false);
        AppleEverestStaticRuntime.Log($"collab-map=launch sid={sid} choice={(continueSession ? "continue" : "start-over")} room={session.Level}");
    }

    private static AppleEverestCollabSession CaptureReturnRoute(Level level)
    {
        AppleEverestCollabSession route = Route?.Copy() ?? new AppleEverestCollabSession();
        route.SaveAndReturnToLobbyAllowed = allowSaving;
        if (returnMode == "SetReturnToHere")
        {
            route.LobbySID = level.Session.Area.SID;
            route.LobbyRoom = level.Session.LevelData.Name;
            Player player = level.Tracker.GetEntity<Player>();
            Vector2 spawn = level.GetSpawnPoint(player != null ? player.Position : level.Camera.Position + new Vector2(160f, 90f));
            route.LobbySpawnPointX = spawn.X; route.LobbySpawnPointY = spawn.Y;
        }
        else if (returnMode == "RemoveReturn")
        {
            route.LobbySID = route.LobbyRoom = null;
            route.LobbySpawnPointX = route.LobbySpawnPointY = 0f;
        }
        else if (returnMode != "DoNotChangeReturn") throw new InvalidOperationException("unsupported Collab return mode");
        return route;
    }

    private static string ResolveSessionCheckpoint(string checkpoint)
    {
        if (checkpoint == ContinueCheckpoint)
            return null;
        // A null Start selection is significant: Session marks the map as
        // beginning normally, so LevelLoader runs the map's effective authored
        // intro (for example WakeUp). Named chapter photos remain respawn-style
        // checkpoint entries, exactly as on desktop Everest.
        return string.IsNullOrEmpty(checkpoint) ? null : checkpoint;
    }

    private static void EnterSelectedMap(Session session, AppleEverestMapProgressionDescriptor descriptor,
        bool continueSession)
    {
        AreaData area = AreaData.Get(session.Area);
        string postcardKey = area?.Name + "_postcard";
        bool completed = SaveData.Instance.Areas.Count > descriptor.RuntimeAreaId &&
                         SaveData.Instance.Areas[descriptor.RuntimeAreaId].Modes[0]?.Completed == true;
        bool showPostcard = !continueSession && session.StartedFromBeginning &&
                            (!completed || SaveData.Instance.DebugMode) && Dialog.Has(postcardKey);
        if (showPostcard)
        {
            AppleEverestStaticRuntime.Log($"collab-map=postcard sid={descriptor.Sid} key={postcardKey}");
            Engine.Scene = new AppleEverestCollabLevelEnter(session, Dialog.Get(postcardKey));
            return;
        }
        LevelEnter.Go(session, fromSaveData: false);
    }

    private static IEnumerator WrappedOverworldRoutine(Level level, AppleEverestSceneWrappingEntity<Overworld> wrapper)
    {
        Overworld overworld = wrapper.WrappedScene;
        while (wrapper == overworldWrapper && wrapper.Scene == Engine.Scene)
        {
            if (overworld.Next is OuiChapterSelect)
            {
                overworld.Next.RemoveSelf();
                yield return null;
                CloseOverworld(level, resetPlayer: true);
                yield break;
            }
            overworld.Snow.ParticleAlpha = 0.25f;
            overworld.Snow.Alpha = Calc.Approach(overworld.Snow.Alpha, 1f, Engine.DeltaTime * 2f);
            yield return null;
        }
    }

    private static void CloseOverworld(Level level, bool resetPlayer)
    {
        AppleEverestSceneWrappingEntity<Overworld> wrapper = overworldWrapper;
        overworldWrapper = null;
        if (wrapper != null)
        {
            // CollabUtils2 removes and flushes the active chapter panel before
            // ending its wrapped Overworld. Without that ordering, the panel's
            // HUD render state can outlive the temporary render target and
            // Metal aborts while releasing the still-open command encoder.
            OuiChapterPanel chapterPanel = wrapper.WrappedScene.GetUI<OuiChapterPanel>();
            chapterPanel?.RemoveSelf();
            wrapper.WrappedScene.Entities.UpdateLists();
            wrapper.RemoveSelf();
        }
        if (hasPreviousArea && SaveData.Instance != null)
        {
            SaveData.Instance.LastArea = previousArea;
            SaveData.Instance.LastArea_Safe = previousArea;
        }
        hasPreviousArea = false;
        forcedMapSid = null;
        chapterCredits.Clear();
        forcedJournalLevelSet = null;
        level.Session.Audio.Apply();
        if (resetPlayer)
        {
            Player player = level.Tracker.GetEntity<Player>();
            if (player != null && player.StateMachine.State == 11)
                level.OnEndOfFrame += () => player.StateMachine.State = 0;
        }
    }

    private static string Basename(string sid)
    {
        int slash = sid.LastIndexOf('/');
        return slash < 0 ? sid : sid[(slash + 1)..];
    }
}

// Everest gives any custom map with an authored "<map dialog key>_postcard"
// entry the normal Celeste postcard before a fresh start. The stock decompiled
// LevelEnter only knows the six vanilla postcards, so preserve Everest's
// content-driven behavior here for collab chapter launches.
internal sealed class AppleEverestCollabLevelEnter : Scene
{
    private readonly Session session;
    private readonly string message;
    private Postcard postcard;

    internal AppleEverestCollabLevelEnter(Session session, string message)
    {
        this.session = session;
        this.message = message;
        Add(new Entity { new Coroutine(Routine()) });
        Add(new HudRenderer());
    }

    private IEnumerator Routine()
    {
        yield return 1f;
        Add(postcard = new Postcard(message,
            "event:/ui/main/postcard_csides_in", "event:/ui/main/postcard_csides_out"));
        yield return postcard.DisplayRoutine();
        Input.SetLightbarColor(AreaData.Get(session.Area).TitleBaseColor);
        Engine.Scene = new LevelLoader(session);
    }

    public override void BeforeRender()
    {
        base.BeforeRender();
        postcard?.BeforeRender();
    }
}

internal sealed class AppleEverestCollabTransition : Entity
{
    internal AppleEverestCollabTransition(IEnumerator routine)
    {
        Tag = (int)Tags.PauseUpdate | (int)Tags.FrozenUpdate;
        Add(new Coroutine(Run(routine)));
    }

    internal AppleEverestCollabTransition(Action complete)
    {
        // Return-to-lobby begins after the pause menu has deliberately frozen
        // the Level. Keep this tiny save barrier alive while paused (and during
        // any short engine freeze) so it can observe UserIO completion and hand
        // the existing runtime to the lobby LevelLoader.
        Tag = (int)Tags.PauseUpdate | (int)Tags.FrozenUpdate;
        Add(new Coroutine(Wait(complete)));
    }

    private IEnumerator Run(IEnumerator routine)
    {
        while (routine.MoveNext()) yield return routine.Current;
        RemoveSelf();
    }

    private static IEnumerator Wait(Action complete)
    {
        while (UserIO.Saving) yield return null;
        complete();
    }
}

internal sealed class AppleEverestChapterPanelTrigger : Trigger
{
    private readonly string sid;
    private readonly string interactFlag;
    private readonly string returnMode;
    private readonly bool allowSaving;
    private readonly TalkComponent talk;
    internal AppleEverestChapterPanelTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        sid = data.Attr("map");
        interactFlag = data.Attr("interactFlag");
        returnMode = data.Attr("returnToLobbyMode", "SetReturnToHere");
        allowSaving = data.Bool("allowSaving", true);
        Vector2 drawAt = data.Nodes.Length > 0 ? data.Nodes[0] - data.Position : new Vector2(data.Width / 2f, data.Height / 2f);
        Add(talk = new TalkComponent(new Rectangle(0, 0, data.Width, data.Height), drawAt,
            player => AppleEverestCollabRuntime.OpenChapterPanel(player, sid, returnMode, allowSaving)) { PlayerMustBeFacing = false });
    }
    public override void Update()
    {
        base.Update();
        talk.Enabled = AppleEverestCollabRuntime.DestinationAvailable(sid) && !AppleEverestCollabRuntime.IsOpen &&
            (string.IsNullOrEmpty(interactFlag) || SceneAs<Level>().Session.GetFlag(interactFlag));
    }
}

internal sealed class AppleEverestJournalTrigger : Trigger
{
    private readonly string levelSet;
    private readonly TalkComponent talk;
    internal AppleEverestJournalTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        levelSet = data.Attr("levelset");
        Vector2 drawAt = data.Nodes.Length > 0 ? data.Nodes[0] - data.Position : new Vector2(data.Width / 2f, data.Height / 2f);
        Add(talk = new TalkComponent(new Rectangle(0, 0, data.Width, data.Height), drawAt,
            player => AppleEverestCollabRuntime.OpenJournal(player, levelSet)) { PlayerMustBeFacing = false });
    }
    public override void Update()
    {
        base.Update();
        talk.Enabled = !AppleEverestCollabRuntime.IsOpen;
    }
}

internal sealed class AppleEverestOuiEnterChapterPanel : Oui
{
    internal static bool Start;
    public override bool IsStart(Overworld overworld, Overworld.StartMode start)
    {
        if (!Start) return false;
        Start = false;
        Add(new Coroutine(Enter(null)));
        return true;
    }
    public override IEnumerator Enter(Oui from)
    {
        Audio.Play("event:/ui/world_map/icon/select");
        Overworld.Goto<OuiChapterPanel>();
        yield break;
    }
    public override IEnumerator Leave(Oui next) { yield break; }
}

internal sealed class AppleEverestOuiEnterJournal : Oui
{
    internal static bool Start;
    public override bool IsStart(Overworld overworld, Overworld.StartMode start)
    {
        if (!Start) return false;
        Start = false;
        Add(new Coroutine(Enter(null)));
        return true;
    }
    public override IEnumerator Enter(Oui from)
    {
        Audio.Play("event:/ui/world_map/journal/select");
        Overworld.Goto<OuiJournal>();
        yield break;
    }
    public override IEnumerator Leave(Oui next) { yield break; }
}

internal sealed class AppleEverestCollabJournalProgress : OuiJournalPage
{
    private sealed class IconCellFromGui : Cell
    {
        private readonly string icon;
        private readonly float width;
        private readonly float height;

        internal IconCellFromGui(string icon, float width, float height)
        {
            this.icon = !string.IsNullOrEmpty(icon) && GFX.Gui.Has(icon) ? icon : "areas/null";
            this.width = width;
            this.height = height;
        }

        public override float Width() => width;

        public override void Render(Vector2 center, float columnWidth)
        {
            MTexture texture = GFX.Gui[icon];
            texture.DrawCentered(center, Color.White,
                Math.Min(width / texture.Width, height / texture.Height));
        }
    }

    private readonly Table table;

    internal AppleEverestCollabJournalProgress(OuiJournal journal, string levelSet) : base(journal)
    {
        PageTexture = "page";
        string skullTexture = MTN.Journal.Has("CollabUtils2Skulls/" + levelSet)
            ? "CollabUtils2Skulls/" + levelSet : "skullblue";
        string minimumDeathsTexture = MTN.Journal.Has("CollabUtils2MinDeaths/" + levelSet)
            ? "CollabUtils2MinDeaths/" + levelSet
            : MTN.Journal.Has("CollabUtils2MinDeaths/SpringCollab2020/1-Beginner")
                ? "CollabUtils2MinDeaths/SpringCollab2020/1-Beginner"
                : skullTexture;
        string heartTexture = MTN.Journal.Has("CollabUtils2Hearts/" + levelSet)
            ? "CollabUtils2Hearts/" + levelSet : "heartgem0";
        string speedBerryTexture = MTN.Journal.Has("CollabUtils2/speed_berry_pbs_heading")
            ? "CollabUtils2/speed_berry_pbs_heading" : "time";

        table = new Table()
            .AddColumn(new TextCell(Dialog.Clean("journal_progress"), new Vector2(0f, 0.5f),
                1f, Color.Black * 0.7f, 360f))
            .AddColumn(new EmptyCell(0f))
            .AddColumn(new EmptyCell(64f))
            .AddColumn(new EmptyCell(0f))
            .AddColumn(new EmptyCell(64f))
            .AddColumn(new IconCell("strawberry", 150f))
            .AddColumn(new IconCell(skullTexture, 100f))
            .AddColumn(new IconCell(minimumDeathsTexture, 100f))
            .AddColumn(new IconCell("time", 220f))
            .AddColumn(new IconCell(speedBerryTexture, 220f))
            .AddColumn(new EmptyCell(30f));

        int totalStrawberries = 0;
        int totalDeaths = 0;
        int totalBestDeaths = 0;
        long totalTime = 0L;
        long totalBestTime = 0L;
        bool allBestDeathsPresent = true;
        bool allBestTimesPresent = true;

        foreach (AppleEverestCollabMapDescriptor map in GeneratedAppleEverestCollabManifest.Collabs
                     .SelectMany(collab => collab.Maps)
                     .Where(map => map.LevelSet == levelSet && !IsHeartSide(map.Sid))
                     .OrderBy(map => map.Sid, StringComparer.Ordinal))
        {
            if (!AppleEverestProgressionRuntime.TryDescriptor(map.Sid, out var descriptor) || SaveData.Instance == null ||
                SaveData.Instance.Areas.Count <= descriptor.RuntimeAreaId) continue;
            AreaStats stats = SaveData.Instance.Areas[descriptor.RuntimeAreaId];
            AreaModeStats mode = stats.Modes[0];
            AreaData area = AreaData.Areas[descriptor.RuntimeAreaId];
            int collectedBerries = stats.TotalStrawberries;
            string berries = area.Mode[0].TotalStrawberries > 0 || collectedBerries > 0
                ? collectedBerries + (mode.Completed ? "/" + area.Mode[0].TotalStrawberries : "")
                : "-";

            string levelHeartTexture = MTN.Journal.Has("CollabUtils2LevelHearts/" + map.Sid)
                ? "CollabUtils2LevelHearts/" + map.Sid : heartTexture;
            Row row = table.AddRow()
                .Add(new TextCell(map.DisplayName, new Vector2(1f, 0.5f), 0.6f, TextColor))
                .Add(null)
                .Add(new IconCellFromGui(area.Icon, 60f, 50f))
                .Add(null)
                .Add(new IconCell(mode.HeartGem ? levelHeartTexture : "dot"))
                .Add(new TextCell(berries, TextJustify, 0.5f, TextColor))
                .Add(stats.TotalTimePlayed > 0
                    ? new TextCell(Dialog.Deaths(mode.Deaths), TextJustify, 0.5f, TextColor)
                    : new IconCell("dot"));

            EntityData collectedSilver = area.Mode[0].MapData.Goldenberries.FirstOrDefault(berry =>
                berry.Name == "CollabUtils2/SilverBerry" && mode.Strawberries.Contains(new EntityID(berry.Level.Name, berry.ID)));
            if (collectedSilver != null)
                row.Add(new IconCell("CollabUtils2/silver_strawberry"));
            else if (area.Mode[0].MapData.Goldenberries.Any(berry =>
                         mode.Strawberries.Contains(new EntityID(berry.Level.Name, berry.ID))))
                row.Add(new IconCell("CollabUtils2/golden_strawberry"));
            else if (mode.SingleRunCompleted)
            {
                row.Add(new TextCell(Dialog.Deaths(mode.BestDeaths), TextJustify, 0.5f, TextColor));
                totalBestDeaths += mode.BestDeaths;
            }
            else
            {
                row.Add(new IconCell("dot"));
                allBestDeathsPresent = false;
            }

            row.Add(stats.TotalTimePlayed > 0
                    ? new TextCell(Dialog.Time(stats.TotalTimePlayed), TextJustify, 0.5f, TextColor)
                    : new IconCell("dot"));
            if (mode.BestTime > 0L)
            {
                row.Add(new TextCell(Dialog.Time(mode.BestTime), TextJustify, 0.5f, TextColor)).Add(null);
                totalBestTime += mode.BestTime;
            }
            else
            {
                row.Add(new IconCell("dot")).Add(null);
                allBestTimesPresent = false;
            }

            totalStrawberries += collectedBerries;
            totalDeaths += mode.Deaths;
            totalTime += stats.TotalTimePlayed;
        }

        table.AddRow();
        table.AddRow()
            .Add(new TextCell(Dialog.Clean("journal_totals"), new Vector2(1f, 0.5f), 0.7f, TextColor))
            .Add(null)
            .Add(null)
            .Add(null)
            .Add(null)
            .Add(new TextCell(totalStrawberries.ToString(), TextJustify, 0.6f, TextColor))
            .Add(new TextCell(Dialog.Deaths(totalDeaths), TextJustify, 0.6f, TextColor))
            .Add(new TextCell(allBestDeathsPresent ? Dialog.Deaths(totalBestDeaths) : "-",
                TextJustify, 0.6f, TextColor))
            .Add(new TextCell(Dialog.Time(totalTime), TextJustify, 0.6f, TextColor))
            .Add(new TextCell(allBestTimesPresent ? Dialog.Time(totalBestTime) : "-",
                TextJustify, 0.6f, TextColor))
            .Add(null);
        table.AddRow();
    }

    // CollabUtils2's native lobby journal excludes the separately gated heart
    // side using this exact SID convention. Chapter-panel spatial order is a
    // lobby-navigation concern and must not determine journal row order.
    private static bool IsHeartSide(string sid) =>
        sid.EndsWith("/ZZ-HeartSide", StringComparison.Ordinal);

    public override void Redraw(VirtualRenderTarget buffer)
    {
        base.Redraw(buffer);
        Draw.SpriteBatch.Begin();
        table.Render(new Vector2(60f, 20f));
        Draw.SpriteBatch.End();
    }
}

internal sealed class AppleEverestMiniHeart : Entity
{
    private static readonly int[] AnimationFrames =
        { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };

    private Sprite sprite;
    private Sprite white;
    private readonly bool requireDash;
    private readonly bool refillDash;
    private readonly bool noGhostSprite;
    private readonly bool flash;
    private readonly string spriteName;
    private Wiggler scaleWiggler;
    private Wiggler moveWiggler;
    private Vector2 moveWiggleDirection;
    private BloomPoint bloom;
    private VertexLight light;
    private ParticleType shineParticle;
    private Coroutine collectRoutine;
    private float bounceSfxDelay;
    private bool collected;

    internal AppleEverestMiniHeart(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        Depth = -100;
        Collider = new Hitbox(12f, 12f, -6f, -6f);
        requireDash = data.Bool("requireDashToBreak", true);
        refillDash = data.Bool("refillDash", true);
        noGhostSprite = data.Bool("noGhostSprite", false);
        flash = data.Bool("flash", true);
        spriteName = data.Attr("sprite", "beginner");
        Add(scaleWiggler = Wiggler.Create(0.5f, 4f,
            value => sprite.Scale = Vector2.One * (1f + value * 0.3f)));
        moveWiggler = Wiggler.Create(0.8f, 2f);
        moveWiggler.StartZero = true;
        Add(moveWiggler);
        Add(new PlayerCollider(OnPlayer));
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        Level level = scene as Level;
        bool collectedBefore = level != null && SaveData.Instance != null &&
            SaveData.Instance.Areas.Count > level.Session.Area.ID &&
            SaveData.Instance.Areas[level.Session.Area.ID].Modes[(int)level.Session.Area.Mode].HeartGem;
        string path = "CollabUtils2/miniheart/" + spriteName + "/";
        if (collectedBefore && !noGhostSprite)
            path = GFX.Game.Has(path + "ghost00") ? path + "ghost" : "CollabUtils2/miniheart/ghost/ghost";
        Add(sprite = new Sprite(GFX.Game, path));
        sprite.AddLoop("idle", "", 0.1f, AnimationFrames);
        sprite.Play("idle");
        sprite.CenterOrigin();
        sprite.OnLoop = animation =>
        {
            if (!Visible) return;
            Audio.Play("event:/game/general/crystalheart_pulse", Position);
            scaleWiggler.Start();
            (Scene as Level)?.Displacement.AddBurst(Position + sprite.Position, 0.35f, 4f, 24f, 0.25f);
        };

        Color heartColor;
        switch (spriteName)
        {
            case "intermediate":
                heartColor = Color.Red;
                shineParticle = HeartGem.P_RedShine;
                break;
            case "advanced":
                heartColor = Color.Gold;
                shineParticle = HeartGem.P_GoldShine;
                break;
            case "expert":
                heartColor = Color.Orange;
                shineParticle = new ParticleType(HeartGem.P_BlueShine) { Color = Color.Orange };
                break;
            case "grandmaster":
                heartColor = Color.DarkViolet;
                shineParticle = new ParticleType(HeartGem.P_BlueShine) { Color = Color.DarkViolet };
                break;
            default:
                heartColor = Color.Aqua;
                shineParticle = HeartGem.P_BlueShine;
                break;
        }
        if (collectedBefore && !noGhostSprite)
        {
            heartColor = Color.White * 0.8f;
            shineParticle = new ParticleType(HeartGem.P_BlueShine) { Color = Calc.HexToColor("7589FF") };
        }
        Add(light = new VertexLight(Color.Lerp(heartColor, Color.White, 0.5f), 1f, 32, 64));
        Add(bloom = new BloomPoint(0.75f, 16f));
    }

    public override void Update()
    {
        base.Update();
        bounceSfxDelay -= Engine.DeltaTime;
        if (sprite != null)
            sprite.Position = moveWiggleDirection * moveWiggler.Value * -8f;
        if (white != null && sprite != null)
        {
            white.Position = sprite.Position;
            white.Scale = sprite.Scale;
            white.SetAnimationFrame(sprite.CurrentAnimationFrame);
        }
        if (Visible && shineParticle != null && Scene.OnInterval(0.1f))
            SceneAs<Level>().Particles.Emit(shineParticle, 1, Center + sprite.Position, Vector2.One * 4f);
    }

    private void OnPlayer(Player player)
    {
        if (collected || Scene is not Level level) return;
        if (requireDash && !player.DashAttacking)
        {
            int dashCount = player.Dashes;
            player.PointBounce(Center);
            if (!refillDash) player.Dashes = dashCount;
            moveWiggleDirection = (Center - player.Center).SafeNormalize(Vector2.UnitY);
            moveWiggler.Start();
            scaleWiggler.Start();
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            if (bounceSfxDelay <= 0f)
            {
                Audio.Play("event:/game/general/crystalheart_bounce", Position);
                bounceSfxDelay = 0.1f;
            }
            return;
        }
        collected = true;
        Collidable = false;
        if (refillDash) player.RefillDash();
        Add(collectRoutine = new Coroutine(Collect(player, level)));
    }

    private IEnumerator Collect(Player player, Level level)
    {
        level.CanRetry = false;
        Audio.SetMusic(null);
        Audio.SetAmbience(null);
        Audio.BusStopAll("bus:/gameplay_sfx", immediate: true);
        string collectSfx = spriteName == "intermediate"
            ? "event:/game/general/crystalheart_red_get"
            : spriteName is "advanced" or "expert" or "grandmaster"
                ? "event:/game/general/crystalheart_gold_get"
                : "event:/game/general/crystalheart_blue_get";
        SoundEmitter.Play(collectSfx, this);

        Add(white = new Sprite(GFX.Game, "CollabUtils2/miniheart/white/white"));
        white.AddLoop("idle", "", 0.1f, AnimationFrames);
        white.Play("idle");
        white.CenterOrigin();
        Depth = Depths.FormationSequences;
        yield return null;
        Celeste.Freeze(0.2f);
        yield return null;
        Engine.TimeRate = 0.5f;
        player.Depth = Depths.FormationSequences;
        for (int index = 0; index < 10; index++) Scene.Add(new AbsorbOrb(Position));
        level.Shake();
        Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
        if (flash) level.Flash(Color.White);
        light.Alpha = bloom.Alpha = 0f;
        level.FormationBackdrop.Display = true;
        level.FormationBackdrop.Alpha = 1f;
        Visible = false;
        for (float time = 0f; time < 2f; time += Engine.RawDeltaTime)
        {
            Engine.TimeRate = Calc.Approach(Engine.TimeRate, 0f, Engine.RawDeltaTime * 0.25f);
            yield return null;
        }
        Depth = 0;
        Depth = Depths.FormationSequences;
        yield return null;
        if (player.Dead)
        {
            RestoreCollectionState(level);
            yield break;
        }
        Engine.TimeRate = 1f;
        Tag = (int)Tags.FrozenUpdate;
        level.Frozen = true;
        AppleEverestCollabRuntime.CompleteMapAndReturn(level);
    }

    private static void RestoreCollectionState(Level level)
    {
        Engine.TimeRate = 1f;
        if (level == null) return;
        level.Frozen = false;
        level.CanRetry = true;
        level.FormationBackdrop.Display = false;
    }

    public override void Removed(Scene scene)
    {
        RestoreCollectionState(scene as Level);
        base.Removed(scene);
    }

    public override void SceneEnd(Scene scene)
    {
        RestoreCollectionState(scene as Level);
        base.SceneEnd(scene);
    }
}
