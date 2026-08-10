using System.Reflection;
using System.Runtime.InteropServices;
using Foundation;
using Microsoft.Xna.Framework;
using ObjCRuntime;
using SDL2;
using UIKit;
#if CELESTE_RUNTIME
using Celeste;
#endif

namespace CelesteTvOSHost;

internal static class Program
{
    private static readonly SDL.SDL_main_func SdlMainCallback = SdlMain;

    public static int Main(string[] args)
    {
        LogHostIdentity();
        Stage3BLog.Info($"lifecycle launch: mode={LaunchMode}; entering SDL_UIKitRunApp");
        return SDL.SDL_UIKitRunApp(0, IntPtr.Zero, SdlMainCallback);
    }

    [MonoPInvokeCallback(typeof(SDL.SDL_main_func))]
    private static int SdlMain(int argc, IntPtr argv)
    {
        Stage3BLog.Info($"managed startup: SDL main callback entered; mode={LaunchMode}; argc={argc}");
        using var lifecycle = new LifecycleMonitor();
#if CELESTE_RUNTIME
        using var hapticLifecycle = new Stage3CHapticLifecycle();
#endif
#if CELESTE_AUDIO
        using var audioLifecycle = new Stage5BAudioLifecycle();
#endif

        try
        {
            string interop = NativeInteropSelfTest.Run();
            Stage3BLog.Info($"native interoperability self-test PASS: {interop}");

            SDL.SDL_SetHint(SDL.SDL_HINT_TV_REMOTE_AS_JOYSTICK, "0");
            SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "Metal");
            ConfigureFnaLogging();

#if STAGE2_DIAGNOSTIC
            using var diagnostic = new Stage2Game();
            Stage3BLog.Info("Stage2Diagnostic: proven geometric FNA scene constructed; entering run loop");
            diagnostic.Run();
            Stage3BLog.Info("Stage2Diagnostic: run loop returned cleanly");
#elif FMOD_DIAGNOSTIC_DEVICE
            using var fmodDiagnostic = new FmodDiagnosticGame();
            Stage3BLog.Info("FmodDiagnostic: real FMOD 1.10.09 device lane constructed; entering run loop");
            fmodDiagnostic.Run();
            Stage3BLog.Info("FmodDiagnostic: run loop returned cleanly");
#elif FMOD_DIAGNOSTIC_UNAVAILABLE
            Stage3BLog.Warning(
                "FmodDiagnostic: native FMOD is device-only for arm64; supplied tvOS simulator archives are x86_64-only"
            );
            using var unavailableDiagnostic = new Stage2Game();
            unavailableDiagnostic.Run();
#elif CELESTE_PREFLIGHT
            _ = PrepareCelesteRuntimeContext(enablePersistence: false);
            using var preflight = new CelestePreflightGame();
            Stage3BLog.Info("CelestePreflight: FNA preflight game constructed; entering run loop");
            preflight.Run();
            Stage3BLog.Info("CelestePreflight: run loop returned cleanly");
            TvOSStage3Bridge.ThrowIfFatal();
            if (TvOSStage3Bridge.LowLevelFmodCallCount != 0)
            {
                throw new InvalidOperationException("CelestePreflight reached an FMOD low-level guard.");
            }
#elif CELESTE_GAME
#if TVOS_STAGE6_HOST
            using Stage6PersistenceStore? persistence = PrepareCelesteRuntimeContext(enablePersistence: true);
            using Stage6PersistenceLifecycle? persistenceLifecycle = persistence == null ? null : new Stage6PersistenceLifecycle(persistence);
            using Stage10ASaveManager? saveManager = persistence == null ? null : new Stage10ASaveManager(persistence);
            using Stage12BQuitCoordinator? quitCoordinator = saveManager == null ? null : new Stage12BQuitCoordinator(saveManager);
            using Stage11ControllerPromptPreferences? controllerPrompts = persistence == null ? null : new Stage11ControllerPromptPreferences();
#else
            PrepareCelesteRuntimeContext(enablePersistence: false);
#endif
            Stage3BLog.Info(
                $"managed identity: Celeste={typeof(global::Celeste.Celeste).Assembly.GetName().Version}; " +
                $"Celeste.Content={typeof(global::Celeste.Content.Stage3AContentIdentity).Assembly.GetName().Version}"
            );
            TvOSStage3Bridge.Checkpoint("celeste-entry-invocation", "method=Celeste.Celeste.Run");
            global::Celeste.Celeste.Run(Array.Empty<string>());
            TvOSStage3Bridge.ThrowIfFatal();
#if !CELESTE_AUDIO
            if (TvOSStage3Bridge.LowLevelFmodCallCount != 0)
            {
                throw new InvalidOperationException("Celeste reached an FMOD low-level guard.");
            }
#endif
            Stage3BLog.Info(
                $"Celeste run loop returned; updates={TvOSStage3Bridge.UpdateCount}; " +
#if CELESTE_AUDIO
                $"draws={TvOSStage3Bridge.DrawCount}; audio=real-fmod"
#else
                $"draws={TvOSStage3Bridge.DrawCount}; no-audio={TvOSStage3Bridge.NoAudioSummary()}"
#endif
            );
#elif CELESTE_PERSISTENCE_DIAGNOSTIC
            using (Stage6PersistenceStore persistence = PrepareCelesteRuntimeContext(enablePersistence: true)
                ?? throw new InvalidOperationException("Stage 6 persistence was not enabled."))
            using (Stage6PersistenceLifecycle persistenceLifecycle = new(persistence))
            {
                Stage6PersistenceDiagnostic.Run(persistence, Environment.GetEnvironmentVariable("CELESTE_TVOS_SESSION_ROOT")!);
            }
#elif CELESTE_PERSISTENCE_INSPECT
            using (Stage6PersistenceStore persistence = PrepareCelesteRuntimeContext(enablePersistence: true)
                ?? throw new InvalidOperationException("Stage 9B production persistence inspection was not enabled."))
            {
                Stage3BLog.Info(
                    $"STAGE9B_PRODUCTION_INSPECT result=PASS; generation={persistence.Generation}; " +
                    $"format=v{persistence.SelectedFormatVersion}; logical={persistence.LogicalHash}; " +
                    $"bridge-bytes={persistence.BridgeBytes()}; mutation=none"
                );
            }
#endif
            return 0;
        }
        catch (Exception exception)
        {
#if CELESTE_RUNTIME
            TvOSStage3CBridge.Fatal(exception, "tvOS host");
#endif
#if CELESTE_AUDIO
            TvOSStage5BAudioBridge.Fatal(exception, "tvOS host");
            TvOSStage5BAudioBridge.ShutdownSafely("host-exception");
#endif
            Stage3BLog.Error($"unhandled managed exception: {exception}");
            return 1;
        }
    }

    private static void ConfigureFnaLogging()
    {
        FNALoggerEXT.LogInfo = message => Stage3BLog.Info($"FNA: {message.TrimEnd()}");
        FNALoggerEXT.LogWarn = message => Stage3BLog.Warning($"FNA: {message.TrimEnd()}");
        FNALoggerEXT.LogError = message => Stage3BLog.Error($"FNA: {message.TrimEnd()}");
    }

#if CELESTE_RUNTIME
#if TVOS_STAGE6_HOST
    private static Stage6PersistenceStore? PrepareCelesteRuntimeContext(bool enablePersistence)
#else
    private static object? PrepareCelesteRuntimeContext(bool enablePersistence)
#endif
    {
        string resources = NSBundle.MainBundle.ResourcePath
            ?? throw new InvalidOperationException("The application resource path is unavailable.");
        string content = Path.Combine(resources, "Content");
        if (!File.Exists(Path.Combine(content, "Effects", "Border.xnb")) ||
            !File.Exists(Path.Combine(content, "Monocle", "MonocleDefault.xnb")))
        {
            throw new InvalidOperationException("Validated Stage 3B representative Content is not packaged.");
        }
#if CELESTE_AUDIO
        string bankRoot = Path.Combine(resources, "Content", "FMOD", "Desktop");
        string[] requiredBanks =
        {
            "Master Bank.bank", "Master Bank.strings.bank", "music.bank", "sfx.bank", "ui.bank",
            "dlc_music.bank", "dlc_sfx.bank"
        };
        if (requiredBanks.Any(name => !File.Exists(Path.Combine(bankRoot, name))))
            throw new InvalidOperationException("The accepted seven Stage 5A FMOD banks are not packaged.");
#else
        if (Directory.Exists(Path.Combine(content, "FMOD")))
        {
            throw new InvalidOperationException("A no-audio Celeste package unexpectedly contains FMOD material.");
        }
#endif

        string temporaryBase = Path.GetFullPath(Path.GetTempPath());
        string sessionRoot = Path.GetFullPath(Path.Combine(temporaryBase, $"celeste-stage3b-{Environment.ProcessId}"));
        if (!sessionRoot.StartsWith(temporaryBase, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Ephemeral session root escaped the application temporary directory.");
        }
        if (Directory.Exists(sessionRoot))
        {
            Directory.Delete(sessionRoot, recursive: true);
        }
        Directory.CreateDirectory(sessionRoot);

        Environment.SetEnvironmentVariable("CELESTE_TVOS_SESSION_ROOT", sessionRoot);
#if CELESTE_PROLOGUE_DIAGNOSTIC
        Environment.SetEnvironmentVariable("CELESTE_TVOS_PROLOGUE_SCENARIO", PrologueScenario);
#else
        Environment.SetEnvironmentVariable("CELESTE_TVOS_PROLOGUE_SCENARIO", null);
#endif
        Environment.SetEnvironmentVariable("FNA_AUDIO_DISABLE_SOUND", "1");
        Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "Metal");
        Directory.SetCurrentDirectory(resources);
#if TVOS_STAGE6_HOST
        Stage6PersistenceStore? persistence = null;
        if (enablePersistence)
        {
            persistence = new Stage6PersistenceStore(sessionRoot, Stage6Namespace);
            Stage6PersistenceStore.RestoreResult restored = persistence.Restore();
            TvOSStage6PersistenceHooks.CommitRequested = persistence.Commit;
            Stage3BLog.Info($"STAGE6_READY namespace={persistence.NamespaceCategory}; generation={restored.Generation}; materialized={restored.Materialized.ToString().ToLowerInvariant()}; bridge-bytes={persistence.BridgeBytes()}");
        }
#endif
#if CELESTE_AUDIO
        Stage3BLog.Info(enablePersistence
            ? "runtime context: durable UserDefaults bridge restored into a private materialized session; seven accepted device-only FMOD banks; real Celeste audio enabled"
            : "runtime context: validated non-audio Content plus seven accepted device-only FMOD banks; real Celeste audio enabled");
#else
        Stage3BLog.Info(enablePersistence
            ? "runtime context: durable UserDefaults bridge restored into a private materialized session; validated bundled non-audio Content"
            : "runtime context: validated bundled non-audio Content; ephemeral settings root configured");
#endif
#if TVOS_STAGE6_HOST
        return persistence;
#else
        return null;
#endif
    }
#endif

    private static void LogHostIdentity()
    {
        Assembly assembly = typeof(Program).Assembly;
        string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
        bool simulator = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIMULATOR_DEVICE_NAME"));
        Stage3BLog.Info(
            $"host version={version}; repository baseline=6118e5e2f6fd13657e55ebeeab52dee313d5306d; mode={LaunchMode}"
        );
        Stage3BLog.Info($"runtime={RuntimeInformation.FrameworkDescription}; architecture={RuntimeInformation.ProcessArchitecture}");
        Stage3BLog.Info(
            $"OS={NSProcessInfo.ProcessInfo.OperatingSystemVersionString}; UIKit={UIDevice.CurrentDevice.SystemName} " +
            $"{UIDevice.CurrentDevice.SystemVersion}; target={(simulator ? "simulator" : "physical-device")}"
        );
        Stage3BLog.Info($"FNA assembly version={typeof(Game).Assembly.GetName().Version}; SDL platform={SafeSdlPlatform()}");
    }

    private static string SafeSdlPlatform()
    {
        try { return SDL.SDL_GetPlatform(); }
        catch (Exception exception) { return $"unavailable-before-SDL-main ({exception.GetType().Name})"; }
    }

    private static string LaunchMode
    {
        get
        {
#if CELESTE_PREFLIGHT
            return "CelestePreflight";
#elif CELESTE_PERSISTENCE_DIAGNOSTIC
            return "CelestePersistenceDiagnostic";
#elif CELESTE_PERSISTENCE_INSPECT
            return "CelestePersistenceInspect";
#elif FMOD_DIAGNOSTIC_DEVICE || FMOD_DIAGNOSTIC_UNAVAILABLE
            return "FmodDiagnostic";
#elif CELESTE_AUDIO
            return "CelesteAudio";
#elif CELESTE_PROLOGUE_DIAGNOSTIC
            return "CelestePrologueDiagnostic";
#elif CELESTE_GAME
            return "Celeste";
#else
            return "Stage2Diagnostic";
#endif
        }
    }

#if TVOS_STAGE6_HOST
    private static string Stage6Namespace =>
#if STAGE6_NAMESPACE_TESTS
        "tests";
#elif STAGE6_NAMESPACE_ACCEPTANCE
        "acceptance";
#elif STAGE6_NAMESPACE_RESTART
        "restart";
#else
        "production";
#endif
#endif

#if CELESTE_PROLOGUE_DIAGNOSTIC
    private static string PrologueScenario
    {
        get
        {
#if STAGE3C_SCENARIO_SKIP
            return "skip";
#elif STAGE3C_SCENARIO_MANUAL
            return "manual";
#else
            return "normal";
#endif
        }
    }
#endif
}
