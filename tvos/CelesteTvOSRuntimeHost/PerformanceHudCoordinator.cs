#if CELESTE_RUNTIME && TVOS_CELESTE_RUNTIME_HOST
using Celeste;
using CoreAnimation;
using Foundation;
using ObjCRuntime;
using SDL2;
using UIKit;

namespace CelesteTvOSHost;

internal sealed class PerformanceHudCoordinator : IDisposable
{
    private static readonly NSString ModeKey = new("mode");
    private static readonly NSString LoggingKey = new("logging");
    private static readonly NSString DefaultValue = new("default");
    private static readonly NSString DisabledValue = new("disabled");
    private static readonly NSDictionary OnProperties = NSDictionary.FromObjectsAndKeys(
        new NSObject[] { DefaultValue, DisabledValue },
        new NSObject[] { ModeKey, LoggingKey });
    private static readonly NSDictionary OffProperties = NSDictionary.FromObjectsAndKeys(
        new NSObject[] { DisabledValue, DisabledValue },
        new NSObject[] { ModeKey, LoggingKey });

    private readonly object gate = new();
    private readonly PerformanceHudPreferenceState preference = new(new UserDefaultsStore());
    private readonly NSObject resignActiveObserver;
    private readonly NSObject foregroundObserver;
    private CAMetalLayer? presentationLayer;
    private IntPtr sdlWindow;
    private PerformanceHudMode? appliedMode;
    private bool available;
    private bool disposed;

    internal PerformanceHudCoordinator()
    {
        TvOSPerformanceHudHooks.EnabledRequested = IsEnabled;
        TvOSPerformanceHudHooks.EnabledChanged = SetEnabled;
        TvOSPerformanceHudHooks.AvailableRequested = IsAvailable;
        TvOSPerformanceHudHooks.StartupRequested = ApplyAtStartup;
        TvOSPerformanceHudHooks.BeforeFirstRenderRequested = ApplyBeforeFirstRender;
        resignActiveObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UIApplication.WillResignActiveNotification,
            _ => HideForInactiveLifecycle());
        foregroundObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            UIApplication.DidBecomeActiveNotification,
            _ => RevalidateOnForeground());
        RuntimeLog.Info($"STAGE16B_HUD coordinator-ready=true; requested={preference.Mode}; key-schema=v1; logging=disabled");
    }

    private void ApplyBeforeFirstRender()
    {
        lock (gate)
        {
            if (disposed || sdlWindow == IntPtr.Zero) return;
            if (!TryAcquireValidatedLayer(sdlWindow, out CAMetalLayer? current, out int candidates))
            {
                FailClosed("first-render-layer-validation", candidates);
                return;
            }
            bool replaced = presentationLayer == null || presentationLayer.Handle != current!.Handle;
            presentationLayer = current;
            available = true;
            if (replaced) appliedMode = null;
            Apply(preference.Mode, "before-first-render");
            RuntimeLog.Info($"STAGE16B_HUD before-first-render=true; layer-replaced={replaced.ToString().ToLowerInvariant()}; candidates={candidates}; draws={TvOSStage3Bridge.DrawCount}");
        }
    }

    private void HideForInactiveLifecycle()
    {
        lock (gate)
        {
            if (disposed || presentationLayer == null) return;
            PerformanceHudMode hidden =
                PerformanceHudPolicy.ModeWhileInactive(preference.Mode);
            Apply(hidden, "resign-active");
            RuntimeLog.Info($"STAGE16B_HUD lifecycle=resign-active; requested={preference.Mode}; visible=Off; logging=disabled");
        }
    }

    private bool IsEnabled()
    {
        lock (gate) return available && preference.Mode == PerformanceHudMode.On;
    }

    private bool IsAvailable()
    {
        lock (gate) return available;
    }

    private void ApplyAtStartup(IntPtr window)
    {
        lock (gate)
        {
            if (disposed || window == IntPtr.Zero) return;
            sdlWindow = window;
            if (!TryAcquireValidatedLayer(window, out CAMetalLayer? layer, out int candidates))
            {
                FailClosed("startup-layer-validation", candidates);
                return;
            }
            presentationLayer = layer;
            available = true;
            Apply(preference.Mode, "startup");
            RuntimeLog.Info($"STAGE16B_HUD real-presentation-layer=true; candidates={candidates}; drawable-match=true; device=present; first-draw=false");
        }
    }

    private void SetEnabled(bool enabled)
    {
        lock (gate)
        {
            if (disposed) return;
            PerformanceHudMode requested = enabled ? PerformanceHudMode.On : PerformanceHudMode.Off;
            if (!available || presentationLayer == null)
            {
                int candidates = 0;
                if (sdlWindow == IntPtr.Zero || !TryAcquireValidatedLayer(sdlWindow, out CAMetalLayer? reacquired, out candidates))
                {
                    FailClosed("options-layer-unavailable", candidates);
                    return;
                }
                presentationLayer = reacquired;
                available = true;
            }
            if (!Apply(requested, "options")) return;
            bool changed = preference.Set(requested);
            RuntimeLog.Info($"STAGE16B_HUD requested={preference.Mode}; persisted={(changed ? "changed" : "unchanged")}; live=true; logging=disabled; draws={TvOSStage3Bridge.DrawCount}");
        }
    }

    private void RevalidateOnForeground()
    {
        lock (gate)
        {
            if (disposed || sdlWindow == IntPtr.Zero) return;
            if (!TryAcquireValidatedLayer(sdlWindow, out CAMetalLayer? current, out int candidates))
            {
                FailClosed("foreground-layer-validation", candidates);
                return;
            }
            bool replaced = presentationLayer == null || presentationLayer.Handle != current!.Handle;
            presentationLayer = current;
            available = true;
            if (replaced) appliedMode = null;
            PerformanceHudMode active =
                PerformanceHudPolicy.ModeAfterForeground(preference.Mode);
            Apply(active, replaced ? "foreground-replacement" : "foreground-stable");
            RuntimeLog.Info($"STAGE16B_HUD foreground=true; layer-replaced={replaced.ToString().ToLowerInvariant()}; candidates={candidates}");
        }
    }

    private bool Apply(PerformanceHudMode mode, string reason)
    {
        if (presentationLayer == null || presentationLayer.Device == null) return false;
        if (appliedMode == mode) return true;
        try
        {
            presentationLayer.DeveloperHudProperties =
                mode == PerformanceHudMode.On ? OnProperties : OffProperties;
            appliedMode = mode;
            PerformanceHudProperties properties = PerformanceHudPolicy.Properties(mode);
            RuntimeLog.Info($"STAGE16B_HUD apply=true; mode={properties.Mode}; logging={properties.Logging}; reason={reason}; draws={TvOSStage3Bridge.DrawCount}");
            return true;
        }
        catch (Exception exception)
        {
            RuntimeLog.Warning($"STAGE16B_HUD apply=false; category={exception.GetType().Name}; reason={reason}; gameplay=continuing");
            available = false;
            appliedMode = null;
            return false;
        }
    }

    private void FailClosed(string category, int candidateCount)
    {
        if (presentationLayer != null)
        {
            try { presentationLayer.DeveloperHudProperties = OffProperties; }
            catch { }
        }
        available = false;
        appliedMode = null;
        RuntimeLog.Warning($"STAGE16B_HUD available=false; category={category}; candidates={candidateCount}; gameplay=continuing");
    }

    private static bool TryAcquireValidatedLayer(
        IntPtr windowHandle,
        out CAMetalLayer? selected,
        out int candidateCount)
    {
        selected = null;
        candidateCount = 0;
        if (!OperatingSystem.IsTvOSVersionAtLeast(16) || windowHandle == IntPtr.Zero) return false;

        SDL.SDL_SysWMinfo info = default;
        SDL.SDL_VERSION(out info.version);
        if (SDL.SDL_GetWindowWMInfo(windowHandle, ref info) != SDL.SDL_bool.SDL_TRUE ||
            info.subsystem != SDL.SDL_SYSWM_TYPE.SDL_SYSWM_UIKIT || info.info.uikit.window == IntPtr.Zero)
            return false;

        UIWindow? window = Runtime.GetNSObject<UIWindow>(info.info.uikit.window);
        UIView? presentationView = window?.RootViewController?.View;
        if (presentationView?.Layer is not CAMetalLayer directLayer || directLayer.Device == null) return false;

        SDL.SDL_Metal_GetDrawableSize(windowHandle, out int drawableWidth, out int drawableHeight);
        bool directSizeMatches = SizeMatches(directLayer, drawableWidth, drawableHeight);
        HashSet<IntPtr> metalIdentities = EnumerateMetalLayerIdentities(window);
        candidateCount = metalIdentities.Count;

        MetalLayerCandidate[] candidates = metalIdentities
            .Select(identity => new MetalLayerCandidate(
                identity,
                identity == (IntPtr)directLayer.Handle,
                true,
                identity == (IntPtr)directLayer.Handle && directLayer.Device != null,
                identity == (IntPtr)directLayer.Handle && directSizeMatches))
            .ToArray();
        IntPtr? selectedIdentity = PerformanceHudPolicy.SelectPresentationLayer(candidates);
        if (selectedIdentity != (IntPtr)directLayer.Handle) return false;
        selected = directLayer;
        return true;
    }

    private static HashSet<IntPtr> EnumerateMetalLayerIdentities(UIWindow? exactWindow)
    {
        HashSet<IntPtr> identities = new();
        if (exactWindow != null) CollectMetalLayers(exactWindow, identities);
        foreach (UIScene scene in UIApplication.SharedApplication.ConnectedScenes)
        {
            if (scene is not UIWindowScene windowScene) continue;
            foreach (UIWindow window in windowScene.Windows) CollectMetalLayers(window, identities);
        }
        return identities;
    }

    private static void CollectMetalLayers(UIView view, HashSet<IntPtr> identities)
    {
        if (view.Layer is CAMetalLayer layer) identities.Add((IntPtr)layer.Handle);
        foreach (UIView child in view.Subviews) CollectMetalLayers(child, identities);
    }

    private static bool SizeMatches(CAMetalLayer layer, int width, int height) =>
        width > 0 && height > 0 &&
        Math.Abs((double)layer.DrawableSize.Width - width) < 0.5 &&
        Math.Abs((double)layer.DrawableSize.Height - height) < 0.5;

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            if (presentationLayer != null)
            {
                try { presentationLayer.DeveloperHudProperties = OffProperties; }
                catch { }
            }
            TvOSPerformanceHudHooks.Reset();
            NSNotificationCenter.DefaultCenter.RemoveObserver(resignActiveObserver);
            NSNotificationCenter.DefaultCenter.RemoveObserver(foregroundObserver);
            resignActiveObserver.Dispose();
            foregroundObserver.Dispose();
            presentationLayer = null;
            disposed = true;
        }
    }

    private sealed class UserDefaultsStore : IPerformanceHudPreferenceStore
    {
        private readonly NSUserDefaults defaults = NSUserDefaults.StandardUserDefaults;

        public string? Read() => defaults.StringForKey(PerformanceHudPolicy.PreferenceKey);

        public void Write(string value)
        {
            defaults.SetString(value, PerformanceHudPolicy.PreferenceKey);
            defaults.Synchronize();
        }
    }
}
#endif
