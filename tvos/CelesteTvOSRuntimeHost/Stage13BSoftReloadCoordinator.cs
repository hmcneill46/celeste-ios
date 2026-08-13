#if CELESTE_RUNTIME && TVOS_STAGE6_HOST
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Celeste;
using Foundation;
using Monocle;
using UIKit;

namespace CelesteTvOSHost;

internal sealed class Stage13BSoftReloadCoordinator : IDisposable
{
    private static readonly TimeSpan SaveWaitTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan SceneDetachTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MainMenuTimeout = TimeSpan.FromSeconds(20);

    private readonly object gate = new();
    private readonly Stage6PersistenceStore persistence;
    private readonly Stage10ASaveManager saveManager;
    private readonly List<NSObject> observers = new();
    private readonly int mainThreadId;
    private int gameIdentity;
    private int graphicsIdentity;
    private bool runtimeIdentityCaptured;
    private readonly Stage13BSoftReloadStateMachine stateMachine = new();
    private Stage6PersistenceStore.ExternalMutationReloadTicket? ticket;
    private DateTimeOffset saveDeadline;
    private Scene? detachmentScene;
    private Stopwatch? sceneDetachDeadline;
    private Stopwatch? mainMenuDeadline;
    private string failureCategory = "none";
    private bool disposed;
    private long firstManagedBytes;
    private long maximumManagedBytes;
    private int completedReloads;
    private TvOSControllerPromptMode promptMode;

    internal Stage13BSoftReloadCoordinator(Stage6PersistenceStore store, Stage10ASaveManager manager)
    {
        persistence = store ?? throw new ArgumentNullException(nameof(store));
        saveManager = manager ?? throw new ArgumentNullException(nameof(manager));
        mainThreadId = Environment.CurrentManagedThreadId;
        promptMode = TvOSControllerPromptHooks.Mode;
        TvOSSoftReloadHooks.ReloadRequested = RequestReload;
        TvOSSoftReloadHooks.StatusRequested = Status;
        TvOSSoftReloadHooks.UpdateRequested = Update;
        Observe(UIApplication.DidEnterBackgroundNotification, OnDidEnterBackground);
        Stage3BLog.Info(
            $"STAGE13B_ARMED state=inactive; process={Environment.ProcessId}; host-startup=one; " +
            "runtime-identity=pending-first-update; fna-runtime=one; fmod-runtime=one"
        );
    }

    internal TvOSSoftReloadPhase Phase { get { lock (gate) return EffectivePhaseUnsafe(); } }
    internal int CompletedReloads { get { lock (gate) return completedReloads; } }

    private bool RequestReload()
    {
        lock (gate)
        {
            AssertMainThread();
            if (disposed || !saveManager.RestartRequired || !persistence.ExternalMutationRequiresRestart)
            {
                Stage3BLog.Warning("STAGE13B_REQUEST result=rejected; reason=no-verified-mutation-or-disposed");
                return false;
            }
            if (!stateMachine.TryRequest(verifiedExternalMutation: true))
            {
                Stage3BLog.Info($"STAGE13B_REQUEST result=duplicate-suppressed; state={PhaseName(stateMachine.State)}");
                return false;
            }

            failureCategory = "none";
            saveDeadline = DateTimeOffset.UtcNow.Add(SaveWaitTimeout);
            promptMode = TvOSControllerPromptHooks.Mode;
            saveManager.StopForSoftReload();
            Stage3BLog.Info(
                $"STAGE13B_REQUEST result=accepted; userio-saving={UserIO.Saving.ToString().ToLowerInvariant()}; " +
                "listener=false; bonjour=false; sessions=cleared; stale-guard=true"
            );
            return true;
        }
    }

    private TvOSSoftReloadDisplayState Status()
    {
        lock (gate)
        {
            TvOSSoftReloadPhase effective = EffectivePhaseUnsafe();
            return new TvOSSoftReloadDisplayState { Phase = effective, FailureCategory = failureCategory };
        }
    }

    private TvOSSoftReloadPhase EffectivePhaseUnsafe() => stateMachine.EffectiveState(saveManager.RestartRequired);

    // Called once per existing Celeste update by the exact generated hook.
    // Every mutable Celeste operation therefore remains on the FNA main thread.
    private void Update()
    {
        lock (gate)
        {
            CaptureRuntimeIdentityOnFirstUpdate();
            TvOSSoftReloadPhase phase = stateMachine.State;
            if (disposed || phase is TvOSSoftReloadPhase.Inactive or TvOSSoftReloadPhase.RestartRequired or
                TvOSSoftReloadPhase.Complete or TvOSSoftReloadPhase.Failure)
                return;
            AssertMainThread();

            try
            {
                if (phase == TvOSSoftReloadPhase.PreparingReload)
                {
                    if (UserIO.Saving)
                    {
                        if (DateTimeOffset.UtcNow >= saveDeadline) Fail("userio-timeout", null);
                        return;
                    }
                    PrepareAndDetachStaleScene();
                    return;
                }

                if (phase == TvOSSoftReloadPhase.ReloadingSettings)
                {
                    if (!ReferenceEquals(Engine.Scene, detachmentScene))
                    {
                        if (sceneDetachDeadline?.Elapsed >= SceneDetachTimeout)
                            Fail("scene-detach-timeout", null);
                        return;
                    }
                    ReloadAndScheduleMainMenu();
                    return;
                }

                if (phase == TvOSSoftReloadPhase.WaitingForMainMenu)
                {
                    if (Engine.Scene is Overworld overworld && overworld.Current is OuiMainMenu && overworld.Next == null)
                    {
                        stateMachine.Verify();
                        VerifyAndComplete();
                    }
                    else if (mainMenuDeadline?.Elapsed >= MainMenuTimeout)
                    {
                        Fail("main-menu-timeout", null);
                    }
                }
            }
            catch (Exception exception)
            {
                Fail($"{PhaseName(stateMachine.State)}-{exception.GetType().Name}", exception);
            }
        }
    }

    private void PrepareAndDetachStaleScene()
    {
        if (UserIO.Saving) throw new InvalidOperationException("UserIO became busy during reload preparation.");
        if (saveManager.IsListening) throw new InvalidOperationException("Save Manager listener remained active during reload.");

        ticket = persistence.PrepareExternalMutationReload();
        RequireRuntimeIdentity();

        // TvOSSoftReloadHooks.Update runs after Engine.Update has already
        // processed scene transitions.  If this hook clears SaveData while a
        // Level is still current, that stale Level is rendered once more and
        // dereferences the cleared state with an active Metal encoder.  Keep
        // every old high-level object intact for that final draw, schedule an
        // inert scene, and continue only on the following update after Engine
        // has ended/detached the old scene.
        detachmentScene = new Scene();
        Engine.Scene = detachmentScene;
        sceneDetachDeadline = Stopwatch.StartNew();

        stateMachine.EnterSettings();
        Stage3BLog.Info(
            $"STAGE13B_RELOAD scheduled=stale-scene-detach; generation={ticket.Generation}; " +
            "old-save-instance=retained-through-final-draw; stale-guard=true"
        );
    }

    private void ReloadAndScheduleMainMenu()
    {
        if (!ReferenceEquals(Engine.Scene, detachmentScene))
            throw new InvalidOperationException("The stale scene was not detached before high-level state reload.");
        RequireRuntimeIdentity();
        sceneDetachDeadline = null;

        Settings.Reload();
        Input.Initialize();
        Input.ResetGrab();

        stateMachine.EnterGameState();
        SaveData.Instance = null;
        SaveData.NoFileAssistChecks();
        OuiFileSelect.Loaded = false;
        TvOSStage3CBridge.StopAllRumble("stage13b-soft-reload");
        Audio.SetMusic(null, allowFadeOut: false);
        Audio.SetAmbience(null);

        // These are the only transient engine fields Stage 13A proved could
        // otherwise carry a stale level's freeze/assist timing into the menu.
        Engine.TimeRate = 1f;
        Engine.TimeRateB = 1f;
        Engine.FreezeTimer = 0f;
        Engine.DashAssistFreeze = false;

        RequireRuntimeIdentity();
        Engine.Scene = new OverworldLoader(Overworld.StartMode.MainMenu);
        detachmentScene = null;
        mainMenuDeadline = Stopwatch.StartNew();
        stateMachine.WaitForMainMenu();
        Stage3BLog.Info(
            $"STAGE13B_RELOAD scheduled=overworld-main-menu; generation={ticket.Generation}; " +
            "old-save-instance=cleared; file-select-cache=cleared; fna-runloop-returned=false; game-disposed=false"
        );
    }

    private void VerifyAndComplete()
    {
        Stage6PersistenceStore.ExternalMutationReloadTicket current = ticket
            ?? throw new InvalidOperationException("Reload completion has no persistence ticket.");
        RequireRuntimeIdentity();
        if (saveManager.IsListening) throw new InvalidOperationException("Save Manager restarted during reload.");
        if (SaveData.Instance != null) throw new InvalidOperationException("A stale SaveData instance survived the main-menu reload.");
        if (OuiFileSelect.Loaded) throw new InvalidOperationException("The file-select inventory was populated before normal entry.");
        if (!ReferenceEquals(Input.Jump.Binding, Settings.Instance.Jump) ||
            !ReferenceEquals(Input.Dash.Binding, Settings.Instance.Dash) ||
            !ReferenceEquals(Input.Grab.Binding, Settings.Instance.Grab))
            throw new InvalidOperationException("Live virtual inputs do not reference the reloaded Settings graph.");
        if (TvOSControllerPromptHooks.Mode != promptMode)
            throw new InvalidOperationException("The independent Controller Prompts preference changed during Settings reload.");

        ValidateSettings(current);
        foreach (string logicalName in new[] { "0", "1", "2" }) ValidateSave(logicalName, current);
#if CELESTE_AUDIO
        if (!TvOSStage5BAudioBridge.Initialized || !TvOSStage5BAudioBridge.BanksReady)
            throw new InvalidOperationException("The existing FMOD runtime or seven-bank inventory is not ready.");
#endif

        persistence.CompleteExternalMutationReload(current);
        saveManager.CompleteSoftReload();
        stateMachine.Complete();
        completedReloads++;
        ObserveManagedMemory();
        Stage3BLog.Info(
            $"STAGE13B_RELOAD result=PASS; cycle={completedReloads}; generation={current.Generation}; " +
            $"logical={current.LogicalHash}; scene=Overworld; ui=OuiMainMenu; settings=verified; input=verified; " +
            $"save-presence={current.PresenceSummary}; stale-guard=false; fna-runtime=one; fmod-runtime=one; banks=seven"
        );
    }

    private void ValidateSettings(Stage6PersistenceStore.ExternalMutationReloadTicket current)
    {
        byte[]? raw = persistence.ExportLogicalPayloadForFutureSaveManager("settings");
        if ((raw != null) != current.SettingsPresent || UserIO.Exists("settings") != current.SettingsPresent)
            throw new InvalidOperationException("Reloaded Settings presence does not match the ticket.");
        if (raw == null)
        {
            if (Settings.Existed) throw new InvalidOperationException("Settings reset did not load Celeste defaults.");
            _ = TvOSSettingsSerializer.SerializeToBytes(Settings.Instance);
            return;
        }

        Settings imported;
        using (MemoryStream stream = new(raw, writable: false)) imported = TvOSSettingsSerializer.Deserialize(stream);
        byte[] expectedCanonical = TvOSSettingsSerializer.SerializeToBytes(imported);
        byte[] liveCanonical = TvOSSettingsSerializer.SerializeToBytes(Settings.Instance);
        if (!expectedCanonical.AsSpan().SequenceEqual(liveCanonical))
            throw new InvalidOperationException("The live Settings graph differs from the canonical imported graph.");
    }

    private void ValidateSave(string logicalName, Stage6PersistenceStore.ExternalMutationReloadTicket current)
    {
        bool expected = current.ExpectedPresent(logicalName);
        byte[]? raw = persistence.ExportLogicalPayloadForFutureSaveManager(logicalName);
        if ((raw != null) != expected || UserIO.Exists(logicalName) != expected)
            throw new InvalidOperationException($"Reloaded slot {logicalName} presence does not match the ticket.");
        if (raw == null) return;
        SaveData loaded = UserIO.Load<SaveData>(logicalName)
            ?? throw new InvalidOperationException($"Reloaded slot {logicalName} did not deserialize.");
        byte[] canonical = UserIO.Serialize(loaded);
        if (!raw.AsSpan().SequenceEqual(canonical))
            throw new InvalidOperationException($"Reloaded slot {logicalName} changed during serializer validation.");
    }

    private void RequireRuntimeIdentity()
    {
        if (!runtimeIdentityCaptured || Environment.CurrentManagedThreadId != mainThreadId ||
            RuntimeHelpers.GetHashCode(Engine.Instance) != gameIdentity ||
            RuntimeHelpers.GetHashCode(Engine.Graphics.GraphicsDevice) != graphicsIdentity)
        {
            throw new InvalidOperationException("FNA main-thread/game/graphics identity changed during soft reload.");
        }
    }

    private void CaptureRuntimeIdentityOnFirstUpdate()
    {
        if (runtimeIdentityCaptured) return;
        AssertMainThread();
        if (Engine.Instance == null || Engine.Graphics?.GraphicsDevice == null)
            throw new InvalidOperationException("The first Celeste update did not have an initialized FNA game and graphics device.");
        gameIdentity = RuntimeHelpers.GetHashCode(Engine.Instance);
        graphicsIdentity = RuntimeHelpers.GetHashCode(Engine.Graphics.GraphicsDevice);
        runtimeIdentityCaptured = true;
        Stage3BLog.Info(
            $"STAGE13B_READY state=inactive; process={Environment.ProcessId}; host-startup=one; " +
            $"game-identity={gameIdentity}; graphics-identity={graphicsIdentity}; fna-runtime=one; fmod-runtime=one"
        );
    }

    private void ObserveManagedMemory()
    {
        long bytes = GC.GetTotalMemory(forceFullCollection: false);
        if (firstManagedBytes == 0) firstManagedBytes = bytes;
        maximumManagedBytes = Math.Max(maximumManagedBytes, bytes);
        Stage3BLog.Info(
            $"STAGE13B_MEMORY cycle={completedReloads}; managed-bytes={bytes}; " +
            $"delta-from-first={bytes - firstManagedBytes}; maximum={maximumManagedBytes}"
        );
    }

    private void OnDidEnterBackground(NSNotification _)
    {
        lock (gate)
        {
            if (stateMachine.IsActive)
                Fail("background-during-reload", null);
        }
    }

    private void Fail(string category, Exception? exception)
    {
        if (stateMachine.State == TvOSSoftReloadPhase.Failure) return;
        failureCategory = Sanitize(category);
        stateMachine.Fail(failureCategory);
        saveManager.MarkSoftReloadFailed(failureCategory);
        TvOSStage3CBridge.StopAllRumble("stage13b-reload-failed");
        Stage3BLog.Error(
            $"STAGE13B_RELOAD result=failed; category={failureCategory}; type={exception?.GetType().Name ?? "none"}; " +
            $"generation={ticket?.Generation ?? persistence.Generation}; stale-guard={persistence.ExternalMutationRequiresRestart.ToString().ToLowerInvariant()}; " +
            "gameplay-blocked=true; fallback=app-switcher-termination"
        );
    }

    private void AssertMainThread()
    {
        if (Environment.CurrentManagedThreadId != mainThreadId)
            throw new InvalidOperationException("Soft reload was requested outside the FNA main thread.");
    }

    private void Observe(NSString notification, Action<NSNotification> callback) =>
        observers.Add(NSNotificationCenter.DefaultCenter.AddObserver(notification, callback));

    private static string PhaseName(TvOSSoftReloadPhase value) => value.ToString().ToLowerInvariant();
    private static string Sanitize(string value) => new((value ?? "unknown")
        .Where(character => char.IsAsciiLetterOrDigit(character) || character == '-').Take(64).ToArray());

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            TvOSSoftReloadHooks.ResetHostCallbacks();
            foreach (NSObject observer in observers)
            {
                NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
                observer.Dispose();
            }
            observers.Clear();
        }
    }
}
#endif
