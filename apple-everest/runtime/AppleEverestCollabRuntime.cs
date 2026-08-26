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

    internal static bool IsSubordinate(AreaKey area) =>
        AppleEverestProgressionRuntime.Sid(area) is string sid && Maps.ContainsKey(sid);

    internal static void OnLevelLoaded(Level level)
    {
        string sid = AppleEverestProgressionRuntime.Sid(level.Session.Area);
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

    internal static void OpenChapterPanel(Player player, string sid)
    {
        if (!Maps.ContainsKey(sid)) return;
        OpenOverworld(player, sid, journalLevelSet: null, chapter: true);
    }

    internal static void OpenJournal(Player player, string levelSet)
    {
        AppleEverestCollabDescriptor collab = GeneratedAppleEverestCollabManifest.Collabs.SingleOrDefault(value =>
            value.Maps.Any(map => AppleEverestProgressionRuntime.TryDescriptor(map.Sid, out var descriptor) &&
                                  descriptor.LevelSet == levelSet));
        if (collab == null) return;
        string areaSid = collab.Maps.OrderBy(value => value.Order).First().Sid;
        OpenOverworld(player, areaSid, levelSet, chapter: false);
    }

    internal static string ChapterSubtitle(AreaKey area, string fallback)
    {
        string sid = AppleEverestProgressionRuntime.Sid(area);
        return sid != null && Maps.TryGetValue(sid, out AppleEverestCollabMapDescriptor map)
            ? map.Author : fallback;
    }

    internal static bool NeedsChapterCheckpointPage(OuiChapterPanel panel) =>
        overworldWrapper != null && forcedMapSid != null && panel?.Overworld == overworldWrapper.WrappedScene &&
        AppleEverestProgressionPersistence.HasSuspendedSession(forcedMapSid);

    internal static void ConfigureChapterCheckpoints(OuiChapterPanel panel)
    {
        if (overworldWrapper == null || forcedMapSid == null || panel?.Overworld != overworldWrapper.WrappedScene ||
            !AppleEverestProgressionPersistence.HasSuspendedSession(forcedMapSid)) return;
        Color startColor = panel.checkpoints.FirstOrDefault()?.BgColor ?? Calc.HexToColor("eabe26");
        Color continueColor = panel.checkpoints.Skip(1).FirstOrDefault()?.BgColor ?? Calc.HexToColor("3c6180");
        panel.checkpoints.Clear();
        panel.checkpoints.Add(new OuiChapterPanel.Option
        {
            Label = Dialog.Clean("collabutils2_chapterpanel_start"),
            BgColor = startColor,
            Bg = GFX.Gui["areaselect/tab"],
            Icon = GFX.Gui["areaselect/startpoint"],
            CheckpointRotation = Calc.Random.Choose(-1, 1) * Calc.Random.Range(0.05f, 0.2f),
            CheckpointOffset = new Vector2(Calc.Random.Range(-16, 16), Calc.Random.Range(-16, 16)),
            Large = false,
            Siblings = 2
        });
        panel.checkpoints.Add(new OuiChapterPanel.Option
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
        panel.option = 1;
        AppleEverestStaticRuntime.Log($"collab-chapter-bookmarks=ready sid={forcedMapSid} choices=start-over,continue selected=continue");
    }

    internal static bool ShouldDrawVanillaCheckpoint(OuiChapterPanel panel) =>
        overworldWrapper == null || forcedMapSid == null || panel?.Overworld != overworldWrapper.WrappedScene ||
        !AppleEverestProgressionPersistence.HasSuspendedSession(forcedMapSid);

    internal static bool TryStartChapterPanel(OuiChapterPanel panel, string checkpoint)
    {
        if (overworldWrapper == null || forcedMapSid == null || panel?.Overworld != overworldWrapper.WrappedScene ||
            Engine.Scene is not Level level)
            return false;
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
        if (!IsSubordinate(level.Session.Area)) return;
        TextMenu.Item item = null;
        menu.Add(item = new TextMenu.Button(Dialog.Clean("collabutils2_returntolobby")).Pressed(() =>
        {
            int returnIndex = menu.IndexOf(item);
            level.PauseMainMenuOpen = false;
            menu.RemoveSelf();
            OpenReturnToLobbyConfirmMenu(level, returnIndex);
        }));
        (item as TextMenu.Button).ConfirmSfx = "event:/ui/main/message_confirm";
    }

    internal static void CompleteMapAndReturn(Level level)
    {
        if (!IsSubordinate(level.Session.Area)) return;
        level.Session.HeartGem = true;
        SaveData.Instance?.RegisterHeartGem(level.Session.Area);
        level.RegisterAreaComplete();
        level.Paused = true;
        level.PauseLock = true;
        UserIO.SaveHandler(file: true, settings: false);
        level.Add(new AppleEverestCollabTransition(() => ReturnNow(level)));
    }

    private static void OpenReturnToLobbyConfirmMenu(Level level, int returnIndex)
    {
        level.Paused = true;
        TextMenu menu = new()
        {
            AutoScroll = false,
            Position = new Vector2(Engine.Width / 2f, Engine.Height / 2f - 100f)
        };
        menu.Add(new TextMenu.Header(Dialog.Clean("collabutils2_returntolobby_confirm_title")));
        menu.Add(new TextMenu.SubHeader(Dialog.Clean("collabutils2_returntolobby_confirm_note1")));
        menu.Add(new TextMenu.SubHeader(Dialog.Clean("collabutils2_returntolobby_confirm_note2")));
        menu.Add(new TextMenu.SubHeader(""));
        menu.Add(new TextMenu.Button(Dialog.Clean("collabutils2_returntolobby_confirm_save"))
            .Pressed(() => ReturnToLobby(level, menu, save: true)));
        menu.Add(new TextMenu.Button(Dialog.Clean("collabutils2_returntolobby_confirm_donotsave"))
            .Pressed(() => ReturnToLobby(level, menu, save: false)));
        menu.Add(new TextMenu.Button(Dialog.Clean("collabutils2_returntolobby_confirm_cancel"))
            .Pressed(() => menu.OnCancel()));
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
        menu.Focused = false;
        menu.RemoveSelf();
        level.PauseMainMenuOpen = false;
        level.Paused = true;
        level.PauseLock = true;
        if (save)
        {
            level.Session.InArea = true;
            level.Session.Deaths++;
            level.Session.DeathsInCurrentLevel++;
            SaveData.Instance?.AddDeath(level.Session.Area);
            AppleEverestProgressionPersistence.SuspendCurrentSession();
            UserIO.SaveHandler(file: true, settings: false);
        }
        level.Add(new AppleEverestCollabTransition(() => ReturnNow(level)));
    }

    private static void ReturnNow(Level level)
    {
        string sid = AppleEverestProgressionRuntime.Sid(level.Session.Area);
        if (sid == null || !Maps.TryGetValue(sid, out AppleEverestCollabMapDescriptor map)) return;
        level.EndPauseEffects();
        Audio.SetMusic(null);
        Audio.BusStopAll("bus:/gameplay_sfx", immediate: true);
        AppleEverestProgressionRuntime.LaunchPersistentAt(map.LobbySid, map.ReturnRoom,
            new Vector2(map.ReturnX, map.ReturnY));
    }

    private static void OpenOverworld(Player player, string areaSid, string journalLevelSet, bool chapter)
    {
        if (player?.Scene is not Level level || overworldWrapper != null || player.StateMachine.State == 11 ||
            SaveData.Instance == null || !AppleEverestProgressionRuntime.TryDescriptor(areaSid, out var descriptor))
            return;

        player.Drop();
        player.StateMachine.State = 11;
        previousArea = SaveData.Instance.LastArea;
        hasPreviousArea = true;
        SaveData.Instance.LastArea = new AreaKey(descriptor.RuntimeAreaId);
        SaveData.Instance.LastArea_Safe = SaveData.Instance.LastArea;
        forcedMapSid = chapter ? areaSid : null;
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
        if (Engine.Scene is Level current) CloseOverworld(current, resetPlayer: false);
        if (SaveData.Instance == null || SaveData.Instance.FileSlot is < 0 or > 2 ||
            !AppleEverestProgressionRuntime.TryDescriptor(sid, out AppleEverestMapProgressionDescriptor descriptor))
            yield break;
        bool continueSession = checkpoint == ContinueCheckpoint;
        Session session = null;
        if (continueSession)
            AppleEverestProgressionPersistence.TryTakeSuspendedSession(sid, out session);
        else
            AppleEverestProgressionPersistence.DiscardSuspendedSession(sid);
        session ??= new Session(new AreaKey(descriptor.RuntimeAreaId), checkpoint == ContinueCheckpoint ? null : checkpoint);
        AppleEverestStaticRuntime.Log($"collab-map=session-ready sid={sid} choice={(continueSession ? "continue" : "start-over")} room={session.Level}");
        SaveData.Instance.StartSession(session);
        AppleEverestStaticRuntime.Log($"collab-map=session-started sid={sid} choice={(continueSession ? "continue" : "start-over")}");
        UserIO.SaveHandler(file: true, settings: false);
        AppleEverestStaticRuntime.Log($"collab-map=save-started sid={sid} choice={(continueSession ? "continue" : "start-over")}");
        while (UserIO.Saving) yield return null;
        AppleEverestStaticRuntime.Log($"collab-map=handoff sid={sid} choice={(continueSession ? "continue" : "start-over")} room={session.Level}");
        EnterSelectedMap(session, descriptor, continueSession);
        AppleEverestStaticRuntime.Log($"collab-map=launch sid={sid} choice={(continueSession ? "continue" : "start-over")} room={session.Level}");
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
    private readonly TalkComponent talk;
    internal AppleEverestChapterPanelTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        sid = data.Attr("map");
        interactFlag = data.Attr("interactFlag");
        Vector2 drawAt = data.Nodes.Length > 0 ? data.Nodes[0] - data.Position : new Vector2(data.Width / 2f, data.Height / 2f);
        Add(talk = new TalkComponent(new Rectangle(0, 0, data.Width, data.Height), drawAt,
            player => AppleEverestCollabRuntime.OpenChapterPanel(player, sid)) { PlayerMustBeFacing = false });
    }
    public override void Update()
    {
        base.Update();
        talk.Enabled = string.IsNullOrWhiteSpace(interactFlag) || SceneAs<Level>().Session.GetFlag(interactFlag);
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
    private readonly Table table;

    internal AppleEverestCollabJournalProgress(OuiJournal journal, string levelSet) : base(journal)
    {
        PageTexture = "page";
        string minimumDeathsIcon = MTN.Journal.Has("CollabUtils2MinDeaths/SpringCollab2020/1-Beginner")
            ? "CollabUtils2MinDeaths/SpringCollab2020/1-Beginner" : "skullblue";
        table = new Table()
            .AddColumn(new TextCell(Dialog.Clean("journal_progress"), new Vector2(0f, 0.5f), 1f, Color.Black * 0.7f, 560f, true))
            .AddColumn(new IconCell("heartgem0", 90f))
            .AddColumn(new IconCell("strawberry", 120f))
            .AddColumn(new IconCell("skullblue", 100f))
            .AddColumn(new IconCell(minimumDeathsIcon, 100f))
            .AddColumn(new IconCell("time", 220f));

        foreach (AppleEverestCollabMapDescriptor map in GeneratedAppleEverestCollabManifest.Collabs
                     .SelectMany(collab => collab.Maps).Where(map => map.LevelSet == levelSet)
                     .OrderBy(map => map.Order))
        {
            if (!AppleEverestProgressionRuntime.TryDescriptor(map.Sid, out var descriptor) || SaveData.Instance == null ||
                SaveData.Instance.Areas.Count <= descriptor.RuntimeAreaId) continue;
            AreaStats stats = SaveData.Instance.Areas[descriptor.RuntimeAreaId];
            AreaModeStats mode = stats.Modes[0];
            string berries = AreaData.Areas[descriptor.RuntimeAreaId].Mode[0].TotalStrawberries > 0
                ? stats.TotalStrawberries + (mode.Completed ? "/" + AreaData.Areas[descriptor.RuntimeAreaId].Mode[0].TotalStrawberries : "")
                : "-";
            table.AddRow()
                .Add(new TextCell(map.DisplayName, new Vector2(1f, 0.5f), 0.6f, TextColor, 560f, true))
                .Add(new IconCell(mode.HeartGem ? "heartgem0" : "dot"))
                .Add(new TextCell(berries, TextJustify, 0.5f, TextColor))
                .Add(new TextCell(Dialog.Deaths(mode.Deaths), TextJustify, 0.5f, TextColor))
                .Add(mode.SingleRunCompleted
                    ? new TextCell(Dialog.Deaths(mode.BestDeaths), TextJustify, 0.5f, TextColor)
                    : new IconCell("dot"))
                .Add(mode.TimePlayed > 0
                    ? new TextCell(Dialog.Time(mode.TimePlayed), TextJustify, 0.5f, TextColor)
                    : new IconCell("dot"));
        }
    }

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
    private readonly Sprite sprite;
    private readonly bool requireDash;
    private readonly bool refillDash;
    private bool collected;
    internal AppleEverestMiniHeart(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        Depth = -100;
        Collider = new Hitbox(12f, 12f, -6f, -6f);
        requireDash = data.Bool("requireDashToBreak", true);
        refillDash = data.Bool("refillDash", true);
        Add(sprite = GFX.SpriteBank.Create("heartgem0"));
        sprite.Play("spin");
        Add(new PlayerCollider(OnPlayer));
        Add(new BloomPoint(0.75f, 16f));
        Add(new VertexLight(Color.Aqua, 1f, 32, 64));
    }
    private void OnPlayer(Player player)
    {
        if (collected || Scene is not Level level) return;
        if (requireDash && !player.DashAttacking)
        {
            player.PointBounce(Center);
            Audio.Play("event:/game/general/crystalheart_bounce", Position);
            return;
        }
        collected = true;
        Collidable = false;
        if (refillDash) player.RefillDash();
        Audio.Play("event:/game/general/crystalheart_blue_get", Position);
        AppleEverestCollabRuntime.CompleteMapAndReturn(level);
    }
}
