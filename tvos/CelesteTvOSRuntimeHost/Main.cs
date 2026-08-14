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
        RuntimeLog.Info($"lifecycle launch: mode={LaunchMode}; entering SDL_UIKitRunApp");
        return SDL.SDL_UIKitRunApp(0, IntPtr.Zero, SdlMainCallback);
    }

    [MonoPInvokeCallback(typeof(SDL.SDL_main_func))]
    private static int SdlMain(int argc, IntPtr argv)
    {
        RuntimeLog.Info($"managed startup: SDL main callback entered; mode={LaunchMode}; argc={argc}");
        using var lifecycle = new LifecycleMonitor();
#if CELESTE_RUNTIME
        using var hapticLifecycle = new HapticLifecycle();
#endif
#if CELESTE_AUDIO
        using var audioLifecycle = new AudioLifecycle();
#endif

        try
        {
            string interop = NativeInteropSelfTest.Run();
            RuntimeLog.Info($"native interoperability self-test PASS: {interop}");

            SDL.SDL_SetHint(SDL.SDL_HINT_TV_REMOTE_AS_JOYSTICK, "0");
            SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "Metal");
            ConfigureFnaLogging();

#if STAGE2_DIAGNOSTIC
            using var diagnostic = new Stage2Game();
            RuntimeLog.Info("Stage2Diagnostic: proven geometric FNA scene constructed; entering run loop");
            diagnostic.Run();
            RuntimeLog.Info("Stage2Diagnostic: run loop returned cleanly");
#elif FMOD_DIAGNOSTIC_DEVICE
            using var fmodDiagnostic = new FmodDiagnosticGame();
            RuntimeLog.Info("FmodDiagnostic: real FMOD 1.10.09 device lane constructed; entering run loop");
            fmodDiagnostic.Run();
            RuntimeLog.Info("FmodDiagnostic: run loop returned cleanly");
#elif FMOD_DIAGNOSTIC_UNAVAILABLE
            RuntimeLog.Warning(
                "FmodDiagnostic: native FMOD is device-only for arm64; supplied tvOS simulator archives are x86_64-only"
            );
            using var unavailableDiagnostic = new Stage2Game();
            unavailableDiagnostic.Run();
#elif CELESTE_PREFLIGHT
            _ = PrepareCelesteRuntimeContext(enablePersistence: false);
            using var preflight = new CelestePreflightGame();
            RuntimeLog.Info("CelestePreflight: FNA preflight game constructed; entering run loop");
            preflight.Run();
            RuntimeLog.Info("CelestePreflight: run loop returned cleanly");
            TvOSStage3Bridge.ThrowIfFatal();
            if (TvOSStage3Bridge.LowLevelFmodCallCount != 0)
            {
                throw new InvalidOperationException("CelestePreflight reached an FMOD low-level guard.");
            }
#elif CELESTE_GAME
#if TVOS_CELESTE_RUNTIME_HOST
            using PersistenceStore? persistence = PrepareCelesteRuntimeContext(enablePersistence: true);
            using PersistenceLifecycle? persistenceLifecycle = persistence == null ? null : new PersistenceLifecycle(persistence);
            using SaveManagerService? saveManager = persistence == null ? null : new SaveManagerService(persistence);
            using ControllerPromptPreferences? controllerPrompts = persistence == null ? null : new ControllerPromptPreferences();
            using PerformanceHudCoordinator performanceHud = new();
            using SoftReloadCoordinator? softReloadCoordinator = saveManager == null || persistence == null
                ? null : new SoftReloadCoordinator(persistence, saveManager);
            using QuitCoordinator? quitCoordinator = saveManager == null ? null : new QuitCoordinator(saveManager);
#else
            PrepareCelesteRuntimeContext(enablePersistence: false);
#endif
            RuntimeLog.Info(
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
            RuntimeLog.Info(
                $"Celeste run loop returned; updates={TvOSStage3Bridge.UpdateCount}; " +
#if CELESTE_AUDIO
                $"draws={TvOSStage3Bridge.DrawCount}; audio=real-fmod"
#else
                $"draws={TvOSStage3Bridge.DrawCount}; no-audio={TvOSStage3Bridge.NoAudioSummary()}"
#endif
            );
#elif CELESTE_PERSISTENCE_DIAGNOSTIC
            using (PersistenceStore persistence = PrepareCelesteRuntimeContext(enablePersistence: true)
                ?? throw new InvalidOperationException("tvOS persistence was not enabled."))
            using (PersistenceLifecycle persistenceLifecycle = new(persistence))
            {
                PersistenceDiagnostic.Run(persistence, Environment.GetEnvironmentVariable("CELESTE_TVOS_SESSION_ROOT")!);
            }
#elif CELESTE_PERSISTENCE_INSPECT
            using (PersistenceStore persistence = PrepareCelesteRuntimeContext(enablePersistence: true)
                ?? throw new InvalidOperationException("Stage 9B production persistence inspection was not enabled."))
            {
                RuntimeLog.Info(
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
            RuntimeLog.Error($"unhandled managed exception: {exception}");
            return 1;
        }
    }

    private static void ConfigureFnaLogging()
    {
        FNALoggerEXT.LogInfo = message => RuntimeLog.Info($"FNA: {message.TrimEnd()}");
        FNALoggerEXT.LogWarn = message => RuntimeLog.Warning($"FNA: {message.TrimEnd()}");
        FNALoggerEXT.LogError = message => RuntimeLog.Error($"FNA: {message.TrimEnd()}");
    }

#if CELESTE_RUNTIME
#if TVOS_CELESTE_RUNTIME_HOST
    private static PersistenceStore? PrepareCelesteRuntimeContext(bool enablePersistence)
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
            throw new InvalidOperationException("Validated representative Celeste Content is not packaged.");
        }
#if CELESTE_AUDIO
        string bankRoot = Path.Combine(resources, "Content", "FMOD", "Desktop");
        string[] requiredBanks =
        {
            "Master Bank.bank", "Master Bank.strings.bank", "music.bank", "sfx.bank", "ui.bank",
            "dlc_music.bank", "dlc_sfx.bank"
        };
        if (requiredBanks.Any(name => !File.Exists(Path.Combine(bankRoot, name))))
            throw new InvalidOperationException("The accepted seven FMOD banks are not packaged.");
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
#if TVOS_CELESTE_RUNTIME_HOST
        PersistenceStore? persistence = null;
        if (enablePersistence)
        {
            persistence = new PersistenceStore(sessionRoot, PersistenceNamespace);
            PersistenceStore.RestoreResult restored = persistence.Restore();
            TvOSStage6PersistenceHooks.CommitRequested = persistence.Commit;
            RuntimeLog.Info($"STAGE6_READY namespace={persistence.NamespaceCategory}; generation={restored.Generation}; materialized={restored.Materialized.ToString().ToLowerInvariant()}; bridge-bytes={persistence.BridgeBytes()}");
        }
#endif
#if CELESTE_AUDIO
        RuntimeLog.Info(enablePersistence
            ? "runtime context: durable UserDefaults bridge restored into a private materialized session; seven accepted device-only FMOD banks; real Celeste audio enabled"
            : "runtime context: validated non-audio Content plus seven accepted device-only FMOD banks; real Celeste audio enabled");
#else
        RuntimeLog.Info(enablePersistence
            ? "runtime context: durable UserDefaults bridge restored into a private materialized session; validated bundled non-audio Content"
            : "runtime context: validated bundled non-audio Content; ephemeral settings root configured");
#endif
#if TVOS_CELESTE_RUNTIME_HOST
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
        RuntimeLog.Info(
            $"host version={version}; repository baseline=6118e5e2f6fd13657e55ebeeab52dee313d5306d; mode={LaunchMode}"
        );
        RuntimeLog.Info($"runtime={RuntimeInformation.FrameworkDescription}; architecture={RuntimeInformation.ProcessArchitecture}");
        RuntimeLog.Info(
            $"OS={NSProcessInfo.ProcessInfo.OperatingSystemVersionString}; UIKit={UIDevice.CurrentDevice.SystemName} " +
            $"{UIDevice.CurrentDevice.SystemVersion}; target={(simulator ? "simulator" : "physical-device")}"
        );
        RuntimeLog.Info($"FNA assembly version={typeof(Game).Assembly.GetName().Version}; SDL platform={SafeSdlPlatform()}");
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

#if TVOS_CELESTE_RUNTIME_HOST
    private static string PersistenceNamespace =>
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
