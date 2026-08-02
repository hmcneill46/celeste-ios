using System.Reflection;
using System.Runtime.InteropServices;
using Foundation;
using Microsoft.Xna.Framework;
using ObjCRuntime;
using SDL2;
using UIKit;

namespace CelesteTvOSHost;

internal static class Program
{
    private static readonly SDL.SDL_main_func SdlMainCallback = SdlMain;

    public static int Main(string[] args)
    {
        LogHostIdentity();
        Stage2Log.Info("lifecycle launch: entering SDL_UIKitRunApp");

        // SDL owns UIApplicationMain for this host. The static delegate root and
        // MonoPInvokeCallback annotation preserve the native callback for AOT.
        return SDL.SDL_UIKitRunApp(0, IntPtr.Zero, SdlMainCallback);
    }

    [MonoPInvokeCallback(typeof(SDL.SDL_main_func))]
    private static int SdlMain(int argc, IntPtr argv)
    {
        Stage2Log.Info($"managed startup: SDL main callback entered; argc={argc}");
        using var lifecycle = new LifecycleMonitor();

        try
        {
            string interop = NativeInteropSelfTest.Run();
            Stage2Log.Info($"native interoperability self-test PASS: {interop}");

            SDL.SDL_SetHint(SDL.SDL_HINT_TV_REMOTE_AS_JOYSTICK, "0");
            SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "Metal");

            FNALoggerEXT.LogInfo = message => Stage2Log.Info($"FNA: {message.TrimEnd()}");
            FNALoggerEXT.LogWarn = message => Stage2Log.Warning($"FNA: {message.TrimEnd()}");
            FNALoggerEXT.LogError = message => Stage2Log.Error($"FNA: {message.TrimEnd()}");

            using var game = new Stage2Game();
            Stage2Log.Info("FNA Game constructed; entering run loop");
            game.Run();
            Stage2Log.Info("FNA run loop returned cleanly");
            return 0;
        }
        catch (Exception exception)
        {
            Stage2Log.Error($"unhandled managed exception: {exception}");
            return 1;
        }
    }

    private static void LogHostIdentity()
    {
        Assembly assembly = typeof(Program).Assembly;
        string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
        string fnaVersion = typeof(Game).Assembly.GetName().Version?.ToString() ?? "unknown";
        bool simulator = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIMULATOR_DEVICE_NAME"));

        Stage2Log.Info($"host version={version}; repository baseline=5c59ba1b2cb353d241f6026caa8da1d8e1822e24");
        Stage2Log.Info($"runtime={RuntimeInformation.FrameworkDescription}; architecture={RuntimeInformation.ProcessArchitecture}");
        Stage2Log.Info(
            $"OS={NSProcessInfo.ProcessInfo.OperatingSystemVersionString}; UIKit={UIDevice.CurrentDevice.SystemName} " +
            $"{UIDevice.CurrentDevice.SystemVersion}; target={(simulator ? "simulator" : "physical-device")}"
        );
        Stage2Log.Info($"FNA assembly version={fnaVersion}; SDL platform={SafeSdlPlatform()}");
    }

    private static string SafeSdlPlatform()
    {
        try
        {
            return SDL.SDL_GetPlatform();
        }
        catch (Exception exception)
        {
            return $"unavailable-before-SDL-main ({exception.GetType().Name})";
        }
    }
}
