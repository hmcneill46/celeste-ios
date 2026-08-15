using Microsoft.Xna.Framework;
using ObjCRuntime;
using SDL2;
#if IOS_CELESTE_PRODUCT
using Celeste;
#endif

namespace CelesteIOSRuntimeHost;

internal static class Program
{
    private static readonly SDL.SDL_main_func SdlMainCallback = SdlMain;

    public static int Main(string[] args)
    {
        RuntimeLog.Info("process-start host=modern-dotnet-ios; handoff=SDL_UIKitRunApp; runtime-count=1");
        return SDL.SDL_UIKitRunApp(0, IntPtr.Zero, SdlMainCallback);
    }

    [MonoPInvokeCallback(typeof(SDL.SDL_main_func))]
    private static int SdlMain(int argc, IntPtr argv)
    {
        try
        {
            Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "Metal");
            Environment.SetEnvironmentVariable("FNA_GRAPHICS_ENABLE_HIGHDPI", "1");
            SDL.SDL_SetHint("FNA3D_FORCE_DRIVER", "Metal");
            SDL.SDL_SetHint(SDL.SDL_HINT_ORIENTATIONS, "LandscapeLeft LandscapeRight");
            SDL.SDL_SetHint(SDL.SDL_HINT_IOS_HIDE_HOME_INDICATOR, "2");
            SDL.SDL_SetHint(SDL.SDL_HINT_MOUSE_TOUCH_EVENTS, "0");
            SDL.SDL_SetHint(SDL.SDL_HINT_TOUCH_MOUSE_EVENTS, "0");
#if IOS_SIMULATOR_NO_FMOD
            Environment.SetEnvironmentVariable("FNA_AUDIO_DISABLE_SOUND", "1");
            RuntimeLog.Info("audio-lane=simulator-no-fmod; reason=FMOD-1.10.09-has-no-arm64-simulator-slice");
#endif
            FNALoggerEXT.LogInfo = message => RuntimeLog.Info($"FNA {message.TrimEnd()}");
            FNALoggerEXT.LogWarn = message => RuntimeLog.Warning($"FNA {message.TrimEnd()}");
            FNALoggerEXT.LogError = message => RuntimeLog.Error($"FNA {message.TrimEnd()}");
#if IOS_CELESTE_PRODUCT
            IOSCelesteRuntimeContext.Prepare();
            using IOSCelesteLifecycle lifecycle = new();
            RuntimeLog.Info("celeste-run-loop-enter; fna-game-count=1; celeste-runtime-count=1; fmod-owner=Celeste");
            global::Celeste.Celeste.Run(Array.Empty<string>());
            AppleRuntimeBridge.ThrowIfFatal();
            RuntimeLog.Info("celeste-run-loop-return; unexpected-product-exit=true");
#else
            using FoundationGame game = new();
            RuntimeLog.Info("run-loop-enter");
            game.Run();
            RuntimeLog.Info("run-loop-return");
#endif
            return 0;
        }
        catch (Exception exception)
        {
            RuntimeLog.Error($"fatal type={exception.GetType().Name}; message={exception.Message}");
            return 1;
        }
    }
}
