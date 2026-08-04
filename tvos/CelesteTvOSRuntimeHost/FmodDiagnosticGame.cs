#if FMOD_DIAGNOSTIC_DEVICE
using Foundation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIKit;

namespace CelesteTvOSHost;

internal sealed class FmodDiagnosticGame : Game
{
    private sealed record EventCandidate(string Path, int LengthMilliseconds, IntPtr Description);

    private readonly GraphicsDeviceManager graphics;
    private readonly List<NSObject> observers = new();
    private readonly object fmodGate = new();
    private SpriteBatch? spriteBatch;
    private Texture2D? pixel;
    private IntPtr studioSystem;
    private IntPtr masterBus;
    private IntPtr musicBus;
    private IntPtr sfxBus;
    private IntPtr currentInstance;
    private string currentCategory = "none";
    private EventCandidate? musicEvent;
    private EventCandidate? sfxEvent;
    private int phase;
    private double phaseStarted;
    private double diagnosticStarted;
    private long frameCount;
    private int lastPlaybackState = -1;
    private bool setupComplete;
    private bool musicPlayingObserved;
    private bool sfxPlayingObserved;
    private bool volumeTestPassed;
    private bool sfxVolumeTestPassed;
    private bool muteTestPassed;
    private bool pauseTestPassed;
    private bool backgroundObserved;
    private bool foregroundObserved;
    private bool lifecycleReady;
    private bool completed;
    private bool shutdown;
    private TimeSpan nextHeartbeat = TimeSpan.FromSeconds(5);

    public FmodDiagnosticGame()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1920,
            PreferredBackBufferHeight = 1080,
            IsFullScreen = true,
            PreferMultiSampling = false,
            SynchronizeWithVerticalRetrace = true
        };
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        InactiveSleepTime = TimeSpan.FromMilliseconds(20);

        Observe(UIApplication.WillResignActiveNotification, OnResignActive);
        Observe(UIApplication.DidEnterBackgroundNotification, OnBackground);
        Observe(UIApplication.WillEnterForegroundNotification, OnWillEnterForeground);
        Observe(UIApplication.DidBecomeActiveNotification, OnDidBecomeActive);
        Observe(UIApplication.WillTerminateNotification, _ => SafeStop("termination"));
        Deactivated += (_, _) => SafeStop("game-deactivated");
        Exiting += (_, _) => SafeStop("game-exiting");
    }

    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        pixel = new Texture2D(GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
        pixel.SetData(new[] { Color.White });
        Stage3BLog.Info(
            $"FMOD_DIAGNOSTIC_CHECKPOINT name=graphics-ready adapter={GraphicsDevice.Adapter.Description}; " +
            $"backbuffer={GraphicsDevice.PresentationParameters.BackBufferWidth}x{GraphicsDevice.PresentationParameters.BackBufferHeight}"
        );
    }

    protected override void Update(GameTime gameTime)
    {
        try
        {
            lock (fmodGate)
            {
                TickDiagnostic(gameTime.TotalGameTime.TotalSeconds);
            }
        }
        catch (Exception exception)
        {
            Stage3BLog.Error($"FMOD_DIAGNOSTIC_CHECKPOINT name=fatal phase={phase}; exception={exception}");
            SafeShutdown("exception");
            throw;
        }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        double seconds = gameTime.TotalGameTime.TotalSeconds;
        Color background = completed
            ? new Color(20, 105, 62)
            : setupComplete ? new Color(25, 68, 125) : new Color(95, 42, 45);
        GraphicsDevice.Clear(background);
        if (spriteBatch is not null && pixel is not null)
        {
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int x = (int)((seconds * 180) % (width + 180)) - 90;
            int meter = Math.Clamp(phase, 0, 13) * 100;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(pixel, new Rectangle(100, 860, meter, 38), new Color(255, 214, 80));
            spriteBatch.Draw(pixel, new Rectangle(x, 460, 120, 120), Color.White);
            spriteBatch.End();
        }
        frameCount += 1;
        if (gameTime.TotalGameTime >= nextHeartbeat)
        {
            Stage3BLog.Info(
                $"FMOD_DIAGNOSTIC_HEARTBEAT frame={frameCount}; elapsed={seconds:F1}; phase={phase}; " +
                $"music-playing={musicPlayingObserved}; sfx-playing={sfxPlayingObserved}; active={IsActive}"
            );
            nextHeartbeat += TimeSpan.FromSeconds(5);
        }
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        SafeShutdown("unload-content");
        pixel?.Dispose();
        spriteBatch?.Dispose();
        pixel = null;
        spriteBatch = null;
        foreach (NSObject observer in observers)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
            observer.Dispose();
        }
        observers.Clear();
        Stage3BLog.Info("FMOD_DIAGNOSTIC_CHECKPOINT name=graphics-disposed");
    }

    private void TickDiagnostic(double now)
    {
        if (!setupComplete)
        {
            diagnosticStarted = now;
            SetupFmod();
            StartEvent(musicEvent!, "music");
            Advance(1, now, "music-started");
            return;
        }

        FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_Studio_System_Update(studioSystem), "Studio update");
        PollPlaybackState();
        double elapsed = now - phaseStarted;
        if (now - diagnosticStarted > 90)
        {
            throw new TimeoutException("FMOD diagnostic exceeded its 90-second hard timeout.");
        }

        switch (phase)
        {
            case 1 when musicPlayingObserved && elapsed >= 3:
                SetVolume(masterBus, 0.45f, "master");
                volumeTestPassed = true;
                Advance(2, now, "master-volume-lowered");
                break;
            case 2 when elapsed >= 1:
                SetMute(masterBus, true, "master");
                Advance(3, now, "master-muted");
                break;
            case 3 when elapsed >= 1:
                SetMute(masterBus, false, "master");
                muteTestPassed = true;
                Advance(4, now, "master-unmuted");
                break;
            case 4 when elapsed >= 1:
                SetVolume(masterBus, 1.0f, "master");
                if (musicBus != IntPtr.Zero)
                {
                    SetVolume(musicBus, 0.5f, "music");
                }
                Advance(5, now, "music-bus-volume-tested");
                break;
            case 5 when elapsed >= 1:
                if (musicBus != IntPtr.Zero)
                {
                    SetVolume(musicBus, 1.0f, "music");
                }
                TestParameter(currentInstance, musicEvent!.Description);
                Advance(6, now, "event-parameter-tested");
                break;
            case 6 when elapsed >= 3:
                StopCurrent("music-sequence-complete");
                if (sfxBus != IntPtr.Zero)
                {
                    SetVolume(sfxBus, 0.5f, "sfx");
                    sfxVolumeTestPassed = true;
                }
                StartEvent(sfxEvent!, "sfx");
                Advance(7, now, "sfx-started");
                break;
            case 7 when sfxPlayingObserved && elapsed >= Math.Min(2.0, Math.Max(0.5, sfxEvent!.LengthMilliseconds / 1000.0)):
                if (sfxBus != IntPtr.Zero)
                {
                    SetVolume(sfxBus, 1.0f, "sfx");
                }
                StopCurrent("sfx-sequence-complete");
                StartEvent(musicEvent!, "music-lifecycle");
                Advance(8, now, "pause-test-music-started");
                break;
            case 8 when elapsed >= 1:
                SetPaused(masterBus, true, "master");
                Advance(9, now, "master-paused");
                break;
            case 9 when elapsed >= 1:
                SetPaused(masterBus, false, "master");
                pauseTestPassed = true;
                lifecycleReady = true;
                Advance(10, now, "lifecycle-ready");
                break;
            case 10 when foregroundObserved && elapsed >= 1:
                StartEvent(sfxEvent!, "sfx-post-foreground");
                Advance(11, now, "post-foreground-sfx-started");
                break;
            case 10 when elapsed >= 8:
                Stage3BLog.Warning("FMOD_DIAGNOSTIC_CHECKPOINT name=lifecycle-not-observed-in-this-launch");
                StopCurrent("lifecycle-wait-expired");
                StartEvent(sfxEvent!, "sfx-second-launch");
                Advance(11, now, "second-launch-sfx-started");
                break;
            case 11 when elapsed >= Math.Min(2.0, Math.Max(0.5, sfxEvent!.LengthMilliseconds / 1000.0)):
                StopCurrent("diagnostic-complete");
                RequireAcceptanceState();
                completed = true;
                Advance(12, now, "diagnostic-complete");
                break;
            case 12 when elapsed >= 3:
                SafeShutdown("normal-completion");
                Stage3BLog.Info("FMOD_DIAGNOSTIC_CHECKPOINT name=clean-process-exit");
                Exit();
                Advance(13, now, "exit-requested");
                break;
        }
    }

    private void SetupFmod()
    {
        Stage3BLog.Info("FMOD_DIAGNOSTIC_CHECKPOINT name=setup-start managed-header=0x00011014");
        IntPtr standalone = IntPtr.Zero;
        try
        {
            FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_System_Create(out standalone), "Low-level system creation");
            FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_System_GetVersion(standalone, out uint version), "Native version query");
            LogVersion(version, "standalone-low-level");
            FmodDiagnosticNative.FMOD_SDL_Register(standalone);
            Stage3BLog.Info("FMOD_DIAGNOSTIC_CHECKPOINT name=fmod-sdl-registered target=standalone-low-level");
            FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_System_Init(standalone, 64, 0, IntPtr.Zero), "Low-level initialization");
            FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_System_Update(standalone), "Low-level update");
            Stage3BLog.Info("FMOD_DIAGNOSTIC_CHECKPOINT name=low-level-initialized result=OK");
            FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_System_Close(standalone), "Low-level close");
            FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_System_Release(standalone), "Low-level release");
            standalone = IntPtr.Zero;
        }
        finally
        {
            if (standalone != IntPtr.Zero)
            {
                _ = FmodDiagnosticNative.FMOD_System_Close(standalone);
                _ = FmodDiagnosticNative.FMOD_System_Release(standalone);
            }
        }

        int createResult = FmodDiagnosticNative.FMOD_Studio_System_Create(
            out studioSystem,
            FmodDiagnosticNative.ManagedHeaderVersion
        );
        FmodDiagnosticNative.Check(createResult, "Studio system creation with managed 1.10.20 header");
        FmodDiagnosticNative.Check(
            FmodDiagnosticNative.FMOD_Studio_System_GetLowLevelSystem(studioSystem, out IntPtr studioLowLevel),
            "Studio low-level system lookup"
        );
        FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_System_GetVersion(studioLowLevel, out uint studioVersion), "Studio native version query");
        LogVersion(studioVersion, "studio-low-level");
        FmodDiagnosticNative.FMOD_SDL_Register(studioLowLevel);
        Stage3BLog.Info("FMOD_DIAGNOSTIC_CHECKPOINT name=fmod-sdl-registered target=studio-low-level");
        FmodDiagnosticNative.Check(
            FmodDiagnosticNative.FMOD_Studio_System_Initialize(studioSystem, 1024, 0, 0, IntPtr.Zero),
            "Studio initialization"
        );
        Stage3BLog.Info("FMOD_DIAGNOSTIC_CHECKPOINT name=studio-initialized result=OK");

        LoadAndEnumerateBanks();
        ResolveBuses();
        setupComplete = true;
        Stage3BLog.Info(
            $"FMOD_DIAGNOSTIC_CHECKPOINT name=setup-complete music-event={musicEvent!.Path}; " +
            $"sfx-event={sfxEvent!.Path}"
        );
    }

    private void LogVersion(uint version, string source)
    {
        Stage3BLog.Info(
            $"FMOD_DIAGNOSTIC_CHECKPOINT name=native-version source={source}; native=0x{version:X8}; " +
            $"managed=0x{FmodDiagnosticNative.ManagedHeaderVersion:X8}"
        );
        if (version != FmodDiagnosticNative.RequiredNativeVersion)
        {
            throw new InvalidOperationException($"Native FMOD runtime is 0x{version:X8}, expected exact 0x00011009.");
        }
    }

    private void LoadAndEnumerateBanks()
    {
        string resourceRoot = NSBundle.MainBundle.ResourcePath
            ?? throw new InvalidOperationException("Application resource path is unavailable.");
        string bankRoot = Path.Combine(resourceRoot, "Content", "FMOD", "Desktop");
        string[] files = Directory.GetFiles(bankRoot, "*.bank", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);
        string? master = files.SingleOrDefault(path => Path.GetFileName(path).Equals("Master Bank.bank", StringComparison.Ordinal));
        string? strings = files.SingleOrDefault(path => Path.GetFileName(path).Equals("Master Bank.strings.bank", StringComparison.Ordinal));
        if (master is null || strings is null || files.Length < 3)
        {
            throw new InvalidOperationException("Validated master, strings, and content banks are not packaged.");
        }
        IEnumerable<string> ordered = new[] { master, strings }.Concat(files.Where(path => path != master && path != strings));
        foreach (string file in ordered)
        {
            FmodDiagnosticNative.Check(
                FmodDiagnosticNative.FMOD_Studio_System_LoadBankFile(studioSystem, file, 0, out _),
                $"Bank load ({Path.GetFileName(file)})"
            );
            Stage3BLog.Info($"FMOD_DIAGNOSTIC_CHECKPOINT name=bank-loaded file={Path.GetFileName(file)}");
        }

        FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_Studio_System_GetBankCount(studioSystem, out int bankCount), "Bank count");
        IntPtr[] banks = FmodDiagnosticNative.ReadPointerList(
            bankCount,
            (IntPtr buffer, int capacity, out int actual) =>
                FmodDiagnosticNative.FMOD_Studio_System_GetBankList(studioSystem, buffer, capacity, out actual)
        );
        var candidates = new Dictionary<string, EventCandidate>(StringComparer.Ordinal);
        foreach (IntPtr bank in banks)
        {
            string bankPath = FmodDiagnosticNative.ReadUtf8Path(
                (IntPtr buffer, int size, out int retrieved) =>
                    FmodDiagnosticNative.FMOD_Studio_Bank_GetPath(bank, buffer, size, out retrieved)
            );
            FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_Studio_Bank_GetEventCount(bank, out int eventCount), "Bank event count");
            IntPtr[] descriptions = FmodDiagnosticNative.ReadPointerList(
                eventCount,
                (IntPtr buffer, int capacity, out int actual) =>
                    FmodDiagnosticNative.FMOD_Studio_Bank_GetEventList(bank, buffer, capacity, out actual)
            );
            foreach (IntPtr description in descriptions)
            {
                string eventPath = FmodDiagnosticNative.ReadUtf8Path(
                    (IntPtr buffer, int size, out int retrieved) =>
                        FmodDiagnosticNative.FMOD_Studio_EventDescription_GetPath(description, buffer, size, out retrieved)
                );
                FmodDiagnosticNative.Check(
                    FmodDiagnosticNative.FMOD_Studio_EventDescription_GetLength(description, out int length),
                    "Event length lookup"
                );
                candidates[eventPath] = new EventCandidate(eventPath, length, description);
            }
            Stage3BLog.Info($"FMOD_DIAGNOSTIC_CHECKPOINT name=bank-enumerated path={bankPath}; events={descriptions.Length}");
        }
        Stage3BLog.Info($"FMOD_DIAGNOSTIC_CHECKPOINT name=events-enumerated count={candidates.Count}");

        musicEvent = candidates.Values
            .Where(item => item.Path.StartsWith("event:/music/", StringComparison.OrdinalIgnoreCase) && item.LengthMilliseconds >= 3000)
            .OrderByDescending(item => item.Path.Contains("menu", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.Path.Contains("title", StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .FirstOrDefault();
        sfxEvent = candidates.Values
            .Where(item =>
                (item.Path.StartsWith("event:/ui/", StringComparison.OrdinalIgnoreCase) ||
                 item.Path.StartsWith("event:/game/", StringComparison.OrdinalIgnoreCase)) &&
                item.LengthMilliseconds >= 300)
            .OrderByDescending(item => item.Path.Contains("button", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.Path.Contains("select", StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.LengthMilliseconds)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .FirstOrDefault();
        if (musicEvent is null || sfxEvent is null)
        {
            throw new InvalidOperationException("Runtime bank enumeration did not discover suitable music and SFX/UI events.");
        }
        Stage3BLog.Info(
            $"FMOD_DIAGNOSTIC_CHECKPOINT name=events-selected music={musicEvent.Path}; music-ms={musicEvent.LengthMilliseconds}; " +
            $"sfx={sfxEvent.Path}; sfx-ms={sfxEvent.LengthMilliseconds}"
        );
    }

    private void ResolveBuses()
    {
        masterBus = GetBus("bus:/", required: true);
        musicBus = GetFirstBus("bus:/music", "bus:/Music");
        sfxBus = GetFirstBus("bus:/gameplay_sfx", "bus:/sfx", "bus:/ui");
        Stage3BLog.Info(
            $"FMOD_DIAGNOSTIC_CHECKPOINT name=buses-resolved master=true; music={musicBus != IntPtr.Zero}; " +
            $"sfx={sfxBus != IntPtr.Zero}"
        );
    }

    private IntPtr GetFirstBus(params string[] paths)
    {
        foreach (string path in paths)
        {
            IntPtr bus = GetBus(path, required: false);
            if (bus != IntPtr.Zero)
            {
                return bus;
            }
        }
        return IntPtr.Zero;
    }

    private IntPtr GetBus(string path, bool required)
    {
        int result = FmodDiagnosticNative.FMOD_Studio_System_GetBus(studioSystem, path, out IntPtr bus);
        if (result == 0)
        {
            return bus;
        }
        if (!required && result == 74)
        {
            return IntPtr.Zero;
        }
        FmodDiagnosticNative.Check(result, $"Bus lookup ({path})");
        return IntPtr.Zero;
    }

    private void StartEvent(EventCandidate candidate, string category)
    {
        StopCurrent("event-replaced");
        FmodDiagnosticNative.Check(
            FmodDiagnosticNative.FMOD_Studio_System_GetEvent(studioSystem, candidate.Path, out IntPtr description),
            $"Event lookup ({category})"
        );
        FmodDiagnosticNative.Check(
            FmodDiagnosticNative.FMOD_Studio_EventDescription_CreateInstance(description, out currentInstance),
            $"Event instance creation ({category})"
        );
        FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_Studio_EventInstance_Start(currentInstance), $"Event start ({category})");
        currentCategory = category;
        lastPlaybackState = -1;
        Stage3BLog.Info($"FMOD_DIAGNOSTIC_CHECKPOINT name=event-start category={category}; path={candidate.Path}");
    }

    private void PollPlaybackState()
    {
        if (currentInstance == IntPtr.Zero)
        {
            return;
        }
        FmodDiagnosticNative.Check(
            FmodDiagnosticNative.FMOD_Studio_EventInstance_GetPlaybackState(currentInstance, out int state),
            $"Playback-state poll ({currentCategory})"
        );
        if (state != lastPlaybackState)
        {
            Stage3BLog.Info(
                $"FMOD_DIAGNOSTIC_CHECKPOINT name=playback-state category={currentCategory}; state={PlaybackStateName(state)}"
            );
            lastPlaybackState = state;
        }
        if (state is 0 or 1)
        {
            if (currentCategory.StartsWith("music", StringComparison.Ordinal)) musicPlayingObserved = true;
            if (currentCategory.StartsWith("sfx", StringComparison.Ordinal)) sfxPlayingObserved = true;
        }
    }

    private void TestParameter(IntPtr instance, IntPtr description)
    {
        FmodDiagnosticNative.Check(
            FmodDiagnosticNative.FMOD_Studio_EventDescription_GetParameterCount(description, out int count),
            "Event parameter count"
        );
        for (int index = 0; index < count; index += 1)
        {
            FmodDiagnosticNative.Check(
                FmodDiagnosticNative.FMOD_Studio_EventDescription_GetParameterByIndex(description, index, out var parameter),
                "Event parameter lookup"
            );
            string name = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(parameter.Name) ?? string.Empty;
            if (!string.IsNullOrEmpty(name) && parameter.Maximum > parameter.Minimum)
            {
                float value = parameter.Minimum + (parameter.Maximum - parameter.Minimum) * 0.5f;
                FmodDiagnosticNative.Check(
                    FmodDiagnosticNative.FMOD_Studio_EventInstance_SetParameterValue(instance, name, value),
                    "Event parameter change"
                );
                Stage3BLog.Info(
                    $"FMOD_DIAGNOSTIC_CHECKPOINT name=parameter-changed parameter={name}; value={value:F3}"
                );
                return;
            }
        }
        Stage3BLog.Info("FMOD_DIAGNOSTIC_CHECKPOINT name=parameter-not-applicable selected-event-has-no-writable-parameter");
    }

    private void SetVolume(IntPtr bus, float value, string category)
    {
        FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_Studio_Bus_SetVolume(bus, value), $"{category} volume set");
        FlushControlCommands($"{category} volume");
        FmodDiagnosticNative.Check(
            FmodDiagnosticNative.FMOD_Studio_Bus_GetVolume(bus, out float volume, out float finalVolume),
            $"{category} volume get"
        );
        if (Math.Abs(volume - value) > 0.01f)
        {
            throw new InvalidOperationException($"{category} volume readback {volume:F3} did not match {value:F3}.");
        }
        Stage3BLog.Info(
            $"FMOD_DIAGNOSTIC_CHECKPOINT name=volume category={category}; requested={value:F2}; " +
            $"readback={volume:F2}; final={finalVolume:F2}"
        );
    }

    private void SetMute(IntPtr bus, bool value, string category)
    {
        FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_Studio_Bus_SetMute(bus, value ? 1 : 0), $"{category} mute set");
        FlushControlCommands($"{category} mute");
        FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_Studio_Bus_GetMute(bus, out int mute), $"{category} mute get");
        if ((mute != 0) != value) throw new InvalidOperationException($"{category} mute readback mismatch.");
        Stage3BLog.Info($"FMOD_DIAGNOSTIC_CHECKPOINT name=mute category={category}; value={value.ToString().ToLowerInvariant()}");
    }

    private void SetPaused(IntPtr bus, bool value, string category)
    {
        FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_Studio_Bus_SetPaused(bus, value ? 1 : 0), $"{category} pause set");
        FlushControlCommands($"{category} pause");
        FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_Studio_Bus_GetPaused(bus, out int paused), $"{category} pause get");
        if ((paused != 0) != value) throw new InvalidOperationException($"{category} pause readback mismatch.");
        Stage3BLog.Info($"FMOD_DIAGNOSTIC_CHECKPOINT name=pause category={category}; value={value.ToString().ToLowerInvariant()}");
    }

    private void FlushControlCommands(string operation)
    {
        FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_Studio_System_Update(studioSystem), $"{operation} update");
        FmodDiagnosticNative.Check(
            FmodDiagnosticNative.FMOD_Studio_System_FlushCommands(studioSystem),
            $"{operation} flush"
        );
    }

    private void StopCurrent(string reason)
    {
        if (currentInstance == IntPtr.Zero)
        {
            return;
        }
        int stop = FmodDiagnosticNative.FMOD_Studio_EventInstance_Stop(currentInstance, 1);
        int release = FmodDiagnosticNative.FMOD_Studio_EventInstance_Release(currentInstance);
        Stage3BLog.Info(
            $"FMOD_DIAGNOSTIC_CHECKPOINT name=event-stop category={currentCategory}; reason={reason}; " +
            $"stop={FmodDiagnosticNative.ResultName(stop)}; release={FmodDiagnosticNative.ResultName(release)}"
        );
        currentInstance = IntPtr.Zero;
        currentCategory = "none";
        FmodDiagnosticNative.Check(stop, "Event stop");
        FmodDiagnosticNative.Check(release, "Event release");
    }

    private void RequireAcceptanceState()
    {
        if (!musicPlayingObserved || !sfxPlayingObserved || !volumeTestPassed || !sfxVolumeTestPassed ||
            !muteTestPassed || !pauseTestPassed)
        {
            throw new InvalidOperationException(
                $"Incomplete FMOD diagnostic state: music={musicPlayingObserved}, sfx={sfxPlayingObserved}, " +
                $"volume={volumeTestPassed}, sfxVolume={sfxVolumeTestPassed}, mute={muteTestPassed}, " +
                $"pause={pauseTestPassed}."
            );
        }
        Stage3BLog.Info(
            $"FMOD_DIAGNOSTIC_SUMMARY low-level=PASS studio=PASS banks=PASS music-playing=PASS " +
            $"sfx-playing=PASS volume=PASS sfx-volume=PASS mute=PASS pause=PASS background={backgroundObserved}; " +
            $"foreground={foregroundObserved}"
        );
    }

    private void Advance(int nextPhase, double now, string checkpoint)
    {
        phase = nextPhase;
        phaseStarted = now;
        Stage3BLog.Info($"FMOD_DIAGNOSTIC_CHECKPOINT name={checkpoint}; phase={phase}");
    }

    private void Observe(NSString notification, Action<NSNotification> callback)
    {
        observers.Add(NSNotificationCenter.DefaultCenter.AddObserver(notification, callback));
    }

    private void OnResignActive(NSNotification notification)
    {
        lock (fmodGate)
        {
            SafeStop("resign-active");
            if (masterBus != IntPtr.Zero) _ = FmodDiagnosticNative.FMOD_Studio_Bus_SetPaused(masterBus, 1);
            Stage3BLog.Info("FMOD_DIAGNOSTIC_CHECKPOINT name=lifecycle-resign-active playback-stopped=true");
        }
    }

    private void OnBackground(NSNotification notification)
    {
        lock (fmodGate)
        {
            backgroundObserved = true;
            SafeStop("background");
            Stage3BLog.Info("FMOD_DIAGNOSTIC_CHECKPOINT name=lifecycle-background playback-stopped=true");
        }
    }

    private void OnWillEnterForeground(NSNotification notification)
    {
        Stage3BLog.Info("FMOD_DIAGNOSTIC_CHECKPOINT name=lifecycle-foreground-entering");
    }

    private void OnDidBecomeActive(NSNotification notification)
    {
        lock (fmodGate)
        {
            if (!lifecycleReady || studioSystem == IntPtr.Zero)
            {
                return;
            }
            if (masterBus != IntPtr.Zero) FmodDiagnosticNative.Check(FmodDiagnosticNative.FMOD_Studio_Bus_SetPaused(masterBus, 0), "Foreground unpause");
            foregroundObserved = true;
            if (phase == 10 && musicEvent is not null)
            {
                StartEvent(musicEvent, "music-post-foreground");
            }
            Stage3BLog.Info("FMOD_DIAGNOSTIC_CHECKPOINT name=lifecycle-active playback-restarted=true");
        }
    }

    private void SafeStop(string reason)
    {
        lock (fmodGate)
        {
            if (currentInstance != IntPtr.Zero)
            {
                _ = FmodDiagnosticNative.FMOD_Studio_EventInstance_Stop(currentInstance, 1);
                _ = FmodDiagnosticNative.FMOD_Studio_EventInstance_Release(currentInstance);
                currentInstance = IntPtr.Zero;
                Stage3BLog.Info($"FMOD_DIAGNOSTIC_CHECKPOINT name=event-stop category={currentCategory}; reason={reason}; safe=true");
                currentCategory = "none";
            }
        }
    }

    private void SafeShutdown(string reason)
    {
        lock (fmodGate)
        {
            if (shutdown)
            {
                return;
            }
            shutdown = true;
            SafeStop(reason);
            if (studioSystem != IntPtr.Zero)
            {
                int unload = FmodDiagnosticNative.FMOD_Studio_System_UnloadAll(studioSystem);
                int release = FmodDiagnosticNative.FMOD_Studio_System_Release(studioSystem);
                Stage3BLog.Info(
                    $"FMOD_DIAGNOSTIC_CHECKPOINT name=studio-shutdown reason={reason}; " +
                    $"unload={FmodDiagnosticNative.ResultName(unload)}; release={FmodDiagnosticNative.ResultName(release)}"
                );
                studioSystem = IntPtr.Zero;
            }
        }
    }

    private static string PlaybackStateName(int state) => state switch
    {
        0 => "PLAYING",
        1 => "SUSTAINING",
        2 => "STOPPED",
        3 => "STARTING",
        4 => "STOPPING",
        _ => $"UNKNOWN-{state}"
    };
}
#endif
