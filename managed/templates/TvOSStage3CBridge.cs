#if TVOS_STAGE3C
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste;

public static class TvOSStage3CBridge
{
    private const double RumbleWatchdogSeconds = 3.0;
    private static readonly long ProcessStartedAt = Stopwatch.GetTimestamp();
    private static readonly object Gate = new();
    private static readonly long[] RumbleStartedAt = new long[4];
    private static readonly double[] RumbleIntendedSeconds = new double[4];
    private static readonly bool[] RumbleActive = new bool[4];
    private static long sequence;
    private static string lastCheckpoint = "stage3c-not-started";
    private static bool diagnosticPositioned;
    private static bool diagnosticActionIssued;
    private static long tutorialEnteredAt;
    private static long postTransitionUpdates;
    private static long postTransitionDraws;
    private static string postTransitionScene;
    private static bool diagnosticExitIssued;
    private static bool manualHapticProbeStarted;
    private static bool controllerDisconnectedObserved;
    private static bool controllerReconnectedObserved;
    private static bool postReconnectInputObserved;

    public static string LastCheckpoint => lastCheckpoint;
    public static string DiagnosticScenario => Environment.GetEnvironmentVariable("CELESTE_TVOS_PROLOGUE_SCENARIO") ?? "";
    public static bool PrologueDiagnosticEnabled => DiagnosticScenario is "normal" or "skip" or "manual";
    public static long PostTransitionUpdates => postTransitionUpdates;
    public static long PostTransitionDraws => postTransitionDraws;

    public static void Checkpoint(string name, string detail = null)
    {
        lastCheckpoint = name;
        long id = System.Threading.Interlocked.Increment(ref sequence);
        Console.WriteLine(detail == null
            ? $"STAGE3C_CHECKPOINT seq={id}; t={ElapsedSeconds(ProcessStartedAt):0.000}; name={name}"
            : $"STAGE3C_CHECKPOINT seq={id}; t={ElapsedSeconds(ProcessStartedAt):0.000}; name={name}; {detail}");
        if (name == "bird-tutorial-entered") tutorialEnteredAt = Stopwatch.GetTimestamp();
    }

    public static void PreparePrologueDiagnostic(HiresSnow snow)
    {
        if (!PrologueDiagnosticEnabled) return;
        SaveData data = new() { Name = "Stage 3C Diagnostic", LastArea = new AreaKey(0) };
        SaveData.Start(data, 0);
        Session session = new(new AreaKey(0))
        {
            Level = "3",
            StartedFromBeginning = false,
            FirstLevel = false,
            JustStarted = true
        };
        Checkpoint("prologue-diagnostic-route", $"area=0; mode=Normal; room=3; scenario={DiagnosticScenario}");
        Engine.Scene = new LevelLoader(session);
    }

    public static void OnCelesteUpdate(Scene scene)
    {
        CheckWatchdog();
        if (PrologueDiagnosticEnabled && scene is Overworld && postTransitionScene == null)
        {
            MarkPostTransitionScene(scene);
        }
        if (PrologueDiagnosticEnabled && scene is Level level && level.Session?.Area.ID == 0 && level.Session.Level == "3")
        {
            // EntityList lookup avoids introducing a new Tracker reflection root;
            // the game itself uses this path for the Prologue bird and bridge.
            Player player = level.Entities.FindFirst<Player>();
            BirdNPC bird = level.Entities.FindFirst<BirdNPC>();
            CS00_Ending ending = level.Entities.FindFirst<CS00_Ending>();
            Bridge bridge = level.Entities.FindFirst<Bridge>();
            if (!diagnosticPositioned && ending == null && player != null && bird != null && bridge != null)
            {
                player.Position = bird.StartPosition + new Vector2(-80f, -15f);
                player.Speed = Vector2.Zero;
                // Start the map's real ending cutscene directly so automated
                // diagnosis does not depend on replaying the preceding bridge.
                level.Add(new CS00_Ending(player, bird, bridge));
                diagnosticPositioned = true;
                Checkpoint("prologue-diagnostic-positioned", "room-entities=real; cutscene=CS00_Ending; remaining-action=dash-or-skip");
            }
            if (!diagnosticActionIssued && TutorialDelayElapsed() && DiagnosticScenario == "skip" && level.InCutscene)
            {
                diagnosticActionIssued = true;
                Rumble(Input.Gamepad, 0.4f, 0.4f, 0.5f, "prologue-diagnostic-skip");
                Checkpoint("cutscene-skip-requested", "source=diagnostic-edge");
                level.SkipCutscene();
            }
        }

        if (postTransitionScene != null && SceneName(scene) == postTransitionScene)
        {
            postTransitionUpdates++;
            if (postTransitionUpdates == 1)
            {
                Checkpoint("next-scene-first-update", $"scene={postTransitionScene}");
                if (DiagnosticScenario == "manual")
                    Checkpoint("manual-haptic-probe-ready", "action=press-logical-dash-then-disconnect-controller");
            }
            if (DiagnosticScenario == "manual" && !manualHapticProbeStarted && Input.Dash.Pressed)
            {
                manualHapticProbeStarted = true;
                // Holding the DualSense PS button to power it down takes about
                // ten seconds. Keep this dedicated effect bounded but long
                // enough to exercise the real disconnect callback.
                Rumble(Input.Gamepad, 0.7f, 0.7f, 12f, "manual-disconnect-probe");
                Checkpoint("manual-haptic-probe-started", "intended-duration=12");
            }
            if (controllerReconnectedObserved && !postReconnectInputObserved && Input.MenuConfirm.Pressed)
            {
                postReconnectInputObserved = true;
                Checkpoint("controller-input-after-reconnect", "action=menu-confirm");
            }
            if (postTransitionUpdates % 300 == 0)
            {
                Console.WriteLine($"STAGE3C_POST_TRANSITION_HEARTBEAT t={ElapsedSeconds(ProcessStartedAt):0.000}; scene={postTransitionScene}; updates={postTransitionUpdates}; draws={postTransitionDraws}; rumble-active={ActiveRumbleCount()}");
            }
            if (!diagnosticExitIssued && postTransitionUpdates >= 4200)
            {
                diagnosticExitIssued = true;
                StopAllRumble("shutdown");
                Checkpoint("diagnostic-clean-exit-requested", $"updates={postTransitionUpdates}; draws={postTransitionDraws}");
                Engine.Instance.Exit();
            }
        }
    }

    public static void OnCelesteDraw(Scene scene)
    {
        if (postTransitionScene != null && SceneName(scene) == postTransitionScene)
        {
            postTransitionDraws++;
            if (postTransitionDraws == 1) Checkpoint("next-scene-first-draw", $"scene={postTransitionScene}");
        }
    }

    public static bool ConsumeDiagnosticDash()
    {
        if (diagnosticActionIssued || DiagnosticScenario != "normal" || !TutorialDelayElapsed()) return false;
        diagnosticActionIssued = true;
        Rumble(Input.Gamepad, 1f, 1f, 0.25f, "prologue-diagnostic-dash");
        return true;
    }

    public static void MarkPostTransitionScene(Scene scene)
    {
        string name = SceneName(scene);
        if (name is "Celeste.LevelExit" or "Celeste.AreaComplete" or "Celeste.OverworldLoader") return;
        if (postTransitionScene == null)
        {
            postTransitionScene = name;
            Checkpoint("next-scene-constructed", $"scene={name}");
        }
    }

    public static void Rumble(int controllerIndex, float low, float high, float duration, string source)
    {
        if ((uint)controllerIndex >= 4u) throw new ArgumentOutOfRangeException(nameof(controllerIndex));
        if (low <= 0f && high <= 0f)
        {
            StopRumble(controllerIndex, "explicit-zero", source);
            return;
        }
        GamePad.SetVibration((PlayerIndex)controllerIndex, low, high);
        lock (Gate)
        {
            RumbleActive[controllerIndex] = true;
            RumbleStartedAt[controllerIndex] = Stopwatch.GetTimestamp();
            RumbleIntendedSeconds[controllerIndex] = duration;
        }
        RumbleEvent("start", controllerIndex, low, high, duration, source, null, 0.0);
    }

    public static void StopRumble(int controllerIndex, string reason, string source = null)
    {
        if ((uint)controllerIndex >= 4u) return;
        bool wasActive;
        long startedAt;
        lock (Gate)
        {
            wasActive = RumbleActive[controllerIndex];
            startedAt = RumbleStartedAt[controllerIndex];
            RumbleActive[controllerIndex] = false;
            RumbleStartedAt[controllerIndex] = 0;
            RumbleIntendedSeconds[controllerIndex] = 0;
        }
        GamePad.SetVibration((PlayerIndex)controllerIndex, 0f, 0f);
        if (wasActive) RumbleEvent("stop", controllerIndex, 0f, 0f, 0f, source, reason,
            startedAt == 0 ? 0.0 : ElapsedSeconds(startedAt));
    }

    public static void StopAllRumble(string reason)
    {
        for (int i = 0; i < 4; i++) StopRumble(i, reason);
    }

    public static int ActiveRumbleCount()
    {
        lock (Gate) return RumbleActive.Count(active => active);
    }

    public static void ControllerConnectionChanged(int controllerIndex, bool connected)
    {
        if (!connected)
        {
            bool hapticWasActive;
            lock (Gate) hapticWasActive = (uint)controllerIndex < 4u && RumbleActive[controllerIndex];
            controllerDisconnectedObserved = true;
            StopRumble(controllerIndex, "controller-disconnect");
            Checkpoint("controller-disconnected", $"controller-index={controllerIndex}; haptic-active-before-stop={hapticWasActive}");
            return;
        }
        if (controllerDisconnectedObserved) controllerReconnectedObserved = true;
        Checkpoint(controllerDisconnectedObserved ? "controller-reconnected" : "controller-connected",
            $"controller-index={controllerIndex}");
    }

    public static void SceneTransition(Scene from, Scene to)
    {
        StopAllRumble("scene-transition");
        Checkpoint("scene-transition", $"from={SceneName(from)}; to={SceneName(to)}");
        if (from is Level && to is not Level && PrologueDiagnosticEnabled) MarkPostTransitionScene(to);
    }

    public static void Fatal(Exception exception, string source)
    {
        StopAllRumble("unhandled-exception");
        Checkpoint("fatal", $"source={source}; type={exception.GetType().FullName}; message={exception.Message}; last={lastCheckpoint}");
    }

    public static string RunSaveDataPreflight(string sessionRoot)
    {
        if (string.IsNullOrWhiteSpace(sessionRoot)) throw new ArgumentException("Temporary session root is required.", nameof(sessionRoot));
        Directory.CreateDirectory(sessionRoot);
        if (UserIO.Load<SaveData>("stage3c-missing") != null) throw new InvalidOperationException("Missing SaveData did not produce new-game input state.");

        SaveData empty = new();
        if (empty.Name != "Madeline" || empty.Flags.Count != 0 || empty.Areas.Count != 0) throw new InvalidOperationException("New-game SaveData defaults drifted.");
        AssertEquivalent(empty, RoundTrip(empty), "default");

        SaveData representative = RepresentativeSaveData();
        SaveData roundTrip = RoundTrip(representative);
        AssertEquivalent(representative, roundTrip, "representative");
        if (!roundTrip.Areas[0].Modes[0].Completed || roundTrip.UnlockedAreas != 1 || roundTrip.CurrentSession.Inventory.Dashes != 1)
            throw new InvalidOperationException("Prologue completion/dash-unlock state did not round-trip.");
        if (!roundTrip.Areas[0].Modes[0].Strawberries.Contains(new EntityID("3", 17)) || !roundTrip.Areas[0].Modes[0].Checkpoints.Contains("3"))
            throw new InvalidOperationException("Area statistics/collectibles did not round-trip.");

        ExpectInvalid("<SaveData><Time>not-a-long</Time></SaveData>", "malformed");
        ExpectInvalid("<SaveData><Unknown>1</Unknown></SaveData>", "unknown");
        ExpectInvalid("<SaveData><Name>A</Name><Name>B</Name></SaveData>", "duplicate");

        byte[] first = UserIO.Serialize(representative);
        if (!UserIO.Save<SaveData>("stage3c-roundtrip", first)) throw new InvalidOperationException("UserIO.Save first replacement failed.");
        SaveData loaded = UserIO.Load<SaveData>("stage3c-roundtrip");
        AssertEquivalent(representative, loaded, "UserIO-first");
        representative.TotalDashes += 11;
        byte[] second = UserIO.Serialize(representative);
        if (!UserIO.Save<SaveData>("stage3c-roundtrip", second)) throw new InvalidOperationException("UserIO.Save repeated replacement failed.");
        loaded = UserIO.Load<SaveData>("stage3c-roundtrip");
        AssertEquivalent(representative, loaded, "UserIO-replacement");

        return "new-game=PASS; default-round-trip=PASS; representative=PASS; prologue-state=PASS; area-stats=PASS; " +
            "malformed=PASS; unknown=PASS; duplicate=PASS; UserIO-save-load=PASS; repeated-replacement=PASS";
    }

    private static SaveData RepresentativeSaveData()
    {
        AreaStats area = new(0);
        area.Modes[0].Completed = true;
        area.Modes[0].SingleRunCompleted = true;
        area.Modes[0].TimePlayed = 987654321;
        area.Modes[0].Deaths = 2;
        area.Modes[0].FullClear = true;
        area.Modes[0].BestTime = 87654321;
        area.Modes[0].BestFullClearTime = 88776655;
        area.Modes[0].BestDashes = 4;
        area.Modes[0].BestDeaths = 2;
        area.Modes[0].HeartGem = true;
        area.Modes[0].Strawberries.Add(new EntityID("3", 17));
        area.Modes[0].TotalStrawberries = 1;
        area.Modes[0].Checkpoints.Add("3");
        Session session = new()
        {
            Area = new AreaKey(0), Level = "3", Time = 1234567, StartedFromBeginning = true, InArea = false,
            FirstLevel = false, Deaths = 2, DeathsInCurrentLevel = 1, Dashes = 1, DashesAtLevelStart = 0,
            StartCheckpoint = "3", Cassette = true, HeartGem = true, Dreaming = true, ColorGrade = "cold",
            LightingAlphaAdd = 0.125f, BloomBaseAdd = 0.25f, DarkRoomAlpha = 0.5f,
            CoreMode = Session.CoreModes.Cold, GrabbedGolden = true, HitCheckpoint = true,
            Inventory = new PlayerInventory(1, dreamDash: true, backpack: false, noRefills: true),
            RespawnPoint = new Vector2(12.5f, 24.25f), OldStats = area.Clone(),
            Audio = new AudioState("event:/music/test", "event:/env/test")
        };
        session.Flags.Add("bird-finished");
        session.LevelFlags.Add("room-complete");
        session.Strawberries.Add(new EntityID("3", 17));
        session.DoNotLoad.Add(new EntityID("3", 18));
        session.Keys.Add(new EntityID("3", 19));
        session.Counters.Add(new Session.Counter { Key = "tutorial", Value = 1 });
        session.SummitGems = new[] { false, true, false, true, false, true };
        session.UnlockedCSide = true;
        session.FurthestSeenLevel = "4";
        session.BeatBestTime = true;
        session.Audio.Music.Param("progress", 3f);
        session.Audio.Ambience.Param("wind", 0.75f);
        return new SaveData
        {
            Version = "1.4.0.0", Name = "Stage 3C", Time = 555, LastSave = new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc),
            CheatMode = true, AssistMode = true, VariantMode = true,
            Assists = new Assists { GameSpeed = 80, Invincible = true, DashMode = Assists.DashModes.Two,
                DashAssist = true, InfiniteStamina = true, MirrorMode = true, ThreeSixtyDashing = true,
                InvisibleMotion = true, NoGrabbing = true, LowFriction = true, SuperDashing = true,
                Hiccups = true, PlayAsBadeline = true },
            TheoSisterName = "Alex", UnlockedAreas = 1, TotalDeaths = 2, TotalStrawberries = 1,
            TotalGoldenStrawberries = 1, TotalJumps = 20, TotalWallJumps = 5, TotalDashes = 4,
            Flags = new HashSet<string> { "prologue-complete", "dash-unlocked" },
            Poem = new List<string> { "first-line", "first-line", "third-line" },
            SummitGems = new[] { true, false, true, false, true, false }, LastArea = new AreaKey(0),
            RevealedChapter9 = true,
            CurrentSession = session, Areas = new List<AreaStats> { area }
        };
    }

    private static SaveData RoundTrip(SaveData value)
    {
        byte[] xml = TvOSSaveDataSerializer.SerializeToBytes(value);
        using MemoryStream stream = new(xml, writable: false);
        return TvOSSaveDataSerializer.Deserialize(stream);
    }

    private static void AssertEquivalent(SaveData expected, SaveData actual, string scenario)
    {
        if (actual == null || expected.Version != actual.Version || expected.Name != actual.Name || expected.Time != actual.Time ||
            expected.LastSave != actual.LastSave || expected.CheatMode != actual.CheatMode || expected.AssistMode != actual.AssistMode ||
            expected.VariantMode != actual.VariantMode || !AssistsEqual(expected.Assists, actual.Assists) ||
            expected.TheoSisterName != actual.TheoSisterName ||
            expected.UnlockedAreas != actual.UnlockedAreas || expected.TotalDeaths != actual.TotalDeaths ||
            expected.TotalStrawberries != actual.TotalStrawberries ||
            expected.TotalGoldenStrawberries != actual.TotalGoldenStrawberries ||
            expected.TotalJumps != actual.TotalJumps || expected.TotalWallJumps != actual.TotalWallJumps ||
            expected.TotalDashes != actual.TotalDashes ||
            !expected.Flags.SetEquals(actual.Flags) || !expected.Poem.SequenceEqual(actual.Poem) ||
            !SequenceEqual(expected.SummitGems, actual.SummitGems) || expected.RevealedChapter9 != actual.RevealedChapter9 ||
            expected.LastArea != actual.LastArea ||
            expected.Areas.Count != actual.Areas.Count)
            throw new InvalidOperationException($"SaveData {scenario} scalar/root field comparison failed.");
        for (int i = 0; i < expected.Areas.Count; i++)
        {
            AreaStats a = expected.Areas[i];
            AreaStats b = actual.Areas[i];
            if (a.ID != b.ID || a.Cassette != b.Cassette || a.Modes.Length != b.Modes.Length) throw new InvalidOperationException($"SaveData {scenario} AreaStats comparison failed.");
            for (int m = 0; m < a.Modes.Length; m++)
            {
                AreaModeStats x = a.Modes[m];
                AreaModeStats y = b.Modes[m];
                if (x.TotalStrawberries != y.TotalStrawberries || x.Completed != y.Completed || x.SingleRunCompleted != y.SingleRunCompleted ||
                    x.FullClear != y.FullClear || x.Deaths != y.Deaths || x.TimePlayed != y.TimePlayed || x.BestTime != y.BestTime ||
                    x.BestFullClearTime != y.BestFullClearTime || x.BestDashes != y.BestDashes ||
                    x.BestDeaths != y.BestDeaths || x.HeartGem != y.HeartGem ||
                    !x.Strawberries.SetEquals(y.Strawberries) || !x.Checkpoints.SetEquals(y.Checkpoints))
                    throw new InvalidOperationException($"SaveData {scenario} AreaModeStats comparison failed at {i}/{m}.");
            }
        }
        if ((expected.CurrentSession == null) != (actual.CurrentSession == null)) throw new InvalidOperationException($"SaveData {scenario} Session presence failed.");
        if (expected.CurrentSession != null)
        {
            Session a = expected.CurrentSession;
            Session b = actual.CurrentSession;
            if (a.Area != b.Area || a.Level != b.Level || a.Time != b.Time ||
                a.StartedFromBeginning != b.StartedFromBeginning || a.Deaths != b.Deaths || a.Dashes != b.Dashes ||
                a.DashesAtLevelStart != b.DashesAtLevelStart || a.DeathsInCurrentLevel != b.DeathsInCurrentLevel ||
                a.InArea != b.InArea || a.StartCheckpoint != b.StartCheckpoint || a.FirstLevel != b.FirstLevel ||
                a.Cassette != b.Cassette || a.HeartGem != b.HeartGem || a.Dreaming != b.Dreaming ||
                a.ColorGrade != b.ColorGrade || a.LightingAlphaAdd != b.LightingAlphaAdd ||
                a.BloomBaseAdd != b.BloomBaseAdd || a.DarkRoomAlpha != b.DarkRoomAlpha || a.CoreMode != b.CoreMode ||
                a.GrabbedGolden != b.GrabbedGolden || a.HitCheckpoint != b.HitCheckpoint ||
                !InventoryEqual(a.Inventory, b.Inventory) || a.RespawnPoint != b.RespawnPoint ||
                !a.Flags.SetEquals(b.Flags) || !a.LevelFlags.SetEquals(b.LevelFlags) ||
                !a.Strawberries.SetEquals(b.Strawberries) || !a.DoNotLoad.SetEquals(b.DoNotLoad) || !a.Keys.SetEquals(b.Keys) ||
                !CountersEqual(a.Counters, b.Counters) || !SequenceEqual(a.SummitGems, b.SummitGems) ||
                !AreaStatsEqual(a.OldStats, b.OldStats) || a.UnlockedCSide != b.UnlockedCSide ||
                a.FurthestSeenLevel != b.FurthestSeenLevel || a.BeatBestTime != b.BeatBestTime ||
                !AudioStateEqual(a.Audio, b.Audio))
                throw new InvalidOperationException($"SaveData {scenario} Session comparison failed.");
        }
    }

    private static bool AssistsEqual(Assists a, Assists b) =>
        a.GameSpeed == b.GameSpeed && a.Invincible == b.Invincible && a.DashMode == b.DashMode &&
        a.DashAssist == b.DashAssist && a.InfiniteStamina == b.InfiniteStamina && a.MirrorMode == b.MirrorMode &&
        a.ThreeSixtyDashing == b.ThreeSixtyDashing && a.InvisibleMotion == b.InvisibleMotion &&
        a.NoGrabbing == b.NoGrabbing && a.LowFriction == b.LowFriction && a.SuperDashing == b.SuperDashing &&
        a.Hiccups == b.Hiccups && a.PlayAsBadeline == b.PlayAsBadeline;

    private static bool InventoryEqual(PlayerInventory a, PlayerInventory b) =>
        a.Dashes == b.Dashes && a.DreamDash == b.DreamDash && a.Backpack == b.Backpack && a.NoRefills == b.NoRefills;

    private static bool CountersEqual(List<Session.Counter> a, List<Session.Counter> b) =>
        a == null ? b == null : b != null && a.Count == b.Count &&
        a.Zip(b, (left, right) => left.Key == right.Key && left.Value == right.Value).All(equal => equal);

    private static bool AreaStatsEqual(AreaStats a, AreaStats b)
    {
        if (a == null || b == null) return a == b;
        if (a.ID != b.ID || a.Cassette != b.Cassette || a.Modes.Length != b.Modes.Length) return false;
        for (int i = 0; i < a.Modes.Length; i++)
        {
            AreaModeStats x = a.Modes[i];
            AreaModeStats y = b.Modes[i];
            if (x.TotalStrawberries != y.TotalStrawberries || x.Completed != y.Completed ||
                x.SingleRunCompleted != y.SingleRunCompleted || x.FullClear != y.FullClear || x.Deaths != y.Deaths ||
                x.TimePlayed != y.TimePlayed || x.BestTime != y.BestTime || x.BestFullClearTime != y.BestFullClearTime ||
                x.BestDashes != y.BestDashes || x.BestDeaths != y.BestDeaths || x.HeartGem != y.HeartGem ||
                !x.Strawberries.SetEquals(y.Strawberries) || !x.Checkpoints.SetEquals(y.Checkpoints)) return false;
        }
        return true;
    }

    private static bool AudioStateEqual(AudioState a, AudioState b) =>
        a == null ? b == null : b != null && AudioTrackEqual(a.Music, b.Music) && AudioTrackEqual(a.Ambience, b.Ambience);

    private static bool AudioTrackEqual(AudioTrackState a, AudioTrackState b) =>
        a == null ? b == null : b != null && a.Event == b.Event && a.Parameters.Count == b.Parameters.Count &&
        a.Parameters.Zip(b.Parameters, (left, right) => left.Key == right.Key && left.Value == right.Value).All(equal => equal);

    private static bool SequenceEqual(bool[] left, bool[] right) =>
        left == null ? right == null : right != null && left.SequenceEqual(right);

    private static void ExpectInvalid(string xml, string scenario)
    {
        try
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml), writable: false);
            _ = TvOSSaveDataSerializer.Deserialize(stream);
        }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException($"SaveData {scenario} input was not rejected.");
    }

    private static bool TutorialDelayElapsed() => tutorialEnteredAt != 0 && Stopwatch.GetElapsedTime(tutorialEnteredAt).TotalSeconds >= 0.5;

    private static void CheckWatchdog()
    {
        for (int i = 0; i < 4; i++)
        {
            long started;
            bool active;
            double intended;
            lock (Gate)
            {
                started = RumbleStartedAt[i];
                intended = RumbleIntendedSeconds[i];
                active = RumbleActive[i];
            }
            // Every Celeste 1.4.0.0 rumble request is duration-bounded. Give
            // the normal update timer a small grace period; the longer manual
            // disconnect probe remains bounded by its explicit duration too.
            double limit = Math.Max(RumbleWatchdogSeconds, intended + 0.5);
            if (active && started != 0 && intended > 0 && Stopwatch.GetElapsedTime(started).TotalSeconds > limit)
            {
                Console.WriteLine($"STAGE3C_RUMBLE_WATCHDOG controller={i}; limit={limit.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}");
                StopRumble(i, "watchdog");
            }
        }
    }

    private static void RumbleEvent(string action, int controller, float low, float high, float duration, string source, string reason, double elapsed)
    {
        long id = System.Threading.Interlocked.Increment(ref sequence);
        string scene = SceneName(Engine.Scene);
        Console.WriteLine($"STAGE3C_RUMBLE seq={id}; t={ElapsedSeconds(ProcessStartedAt):0.000}; action={action}; controller-index={controller}; low={low:0.###}; high={high:0.###}; intended-duration={duration:0.###}; elapsed={elapsed:0.###}; source={source ?? "unknown"}; reason={reason ?? "none"}; scene={scene}");
    }

    private static double ElapsedSeconds(long startedAt) => Stopwatch.GetElapsedTime(startedAt).TotalSeconds;

    private static string SceneName(Scene scene) => scene?.GetType().FullName ?? "<none>";
}
#endif
