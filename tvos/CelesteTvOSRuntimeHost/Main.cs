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
#elif CELESTE_PREFLIGHT
            PrepareCelesteRuntimeContext();
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
            PrepareCelesteRuntimeContext();
            Stage3BLog.Info(
                $"managed identity: Celeste={typeof(global::Celeste.Celeste).Assembly.GetName().Version}; " +
                $"Celeste.Content={typeof(global::Celeste.Content.Stage3AContentIdentity).Assembly.GetName().Version}"
            );
            TvOSStage3Bridge.Checkpoint("celeste-entry-invocation", "method=Celeste.Celeste.Run");
            global::Celeste.Celeste.Run(Array.Empty<string>());
            TvOSStage3Bridge.ThrowIfFatal();
            if (TvOSStage3Bridge.LowLevelFmodCallCount != 0)
            {
                throw new InvalidOperationException("Celeste reached an FMOD low-level guard.");
            }
            Stage3BLog.Info(
                $"Celeste run loop returned; updates={TvOSStage3Bridge.UpdateCount}; " +
                $"draws={TvOSStage3Bridge.DrawCount}; no-audio={TvOSStage3Bridge.NoAudioSummary()}"
            );
#endif
            return 0;
        }
        catch (Exception exception)
        {
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
    private static void PrepareCelesteRuntimeContext()
    {
        string resources = NSBundle.MainBundle.ResourcePath
            ?? throw new InvalidOperationException("The application resource path is unavailable.");
        string content = Path.Combine(resources, "Content");
        if (!File.Exists(Path.Combine(content, "Effects", "Border.xnb")) ||
            !File.Exists(Path.Combine(content, "Monocle", "MonocleDefault.xnb")))
        {
            throw new InvalidOperationException("Validated Stage 3B representative Content is not packaged.");
        }
        if (Directory.Exists(Path.Combine(content, "FMOD")))
        {
            throw new InvalidOperationException("Stage 3B package unexpectedly contains FMOD content.");
        }

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
        Environment.SetEnvironmentVariable("FNA_AUDIO_DISABLE_SOUND", "1");
        Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "Metal");
        Directory.SetCurrentDirectory(resources);
        Stage3BLog.Info("runtime context: validated bundled non-audio Content; ephemeral settings root configured");
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
            $"host version={version}; repository baseline=7a761643904ad2b4bcbc1fae85c19a21d3fc93cc; mode={LaunchMode}"
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
#elif CELESTE_GAME
            return "Celeste";
#else
            return "Stage2Diagnostic";
#endif
        }
    }
}
