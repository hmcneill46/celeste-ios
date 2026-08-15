using CoreAnimation;
using CelesteIOSFoundation;
using Microsoft.Xna.Framework;
using ObjCRuntime;
using SDL2;
using UIKit;

namespace CelesteIOSRuntimeHost;

internal readonly record struct IOSSceneMetrics(
    double PointWidth, double PointHeight, double Scale,
    double SafeTop, double SafeLeft, double SafeBottom, double SafeRight,
    int DrawableWidth, int DrawableHeight, bool HasWindowScene, bool HasMetalLayer);

internal sealed class SceneMetricsCoordinator : IDisposable
{
    private readonly GameWindow gameWindow;
    private bool disposed;
    private IOSSceneMetrics? previous;

    internal SceneMetricsCoordinator(GameWindow gameWindow)
    {
        this.gameWindow = gameWindow;
        gameWindow.ClientSizeChanged += OnGeometryChanged;
    }

    internal IOSSceneMetrics Capture(string reason)
    {
        SDL.SDL_SysWMinfo info = default;
        SDL.SDL_VERSION(out info.version);
        if (SDL.SDL_GetWindowWMInfo(gameWindow.Handle, ref info) != SDL.SDL_bool.SDL_TRUE ||
            info.subsystem != SDL.SDL_SYSWM_TYPE.SDL_SYSWM_UIKIT || info.info.uikit.window == IntPtr.Zero)
            throw new InvalidOperationException("The FNA window does not resolve to the expected UIKit window.");
        UIWindow window = Runtime.GetNSObject<UIWindow>(info.info.uikit.window)
            ?? throw new InvalidOperationException("The UIKit window wrapper is unavailable.");
        SDL.SDL_Metal_GetDrawableSize(gameWindow.Handle, out int drawableWidth, out int drawableHeight);
        UIEdgeInsets safe = window.SafeAreaInsets;
        SafeAreaMetrics safeArea = new(safe.Top, safe.Left, safe.Bottom, safe.Right);
        if (!safeArea.IsValidFor(window.Bounds.Width, window.Bounds.Height))
            throw new InvalidOperationException("The UIKit safe-area metrics are invalid.");
        int expectedDrawableWidth = (int)Math.Round(window.Bounds.Width * window.Screen.NativeScale);
        int expectedDrawableHeight = (int)Math.Round(window.Bounds.Height * window.Screen.NativeScale);
        if (drawableWidth != expectedDrawableWidth || drawableHeight != expectedDrawableHeight)
            throw new InvalidOperationException("The Metal drawable is not using the native Retina scale.");
        IOSSceneMetrics metrics = new(
            window.Bounds.Width, window.Bounds.Height, window.Screen.NativeScale,
            safe.Top, safe.Left, safe.Bottom, safe.Right,
            drawableWidth, drawableHeight, window.WindowScene != null,
            window.RootViewController?.View?.Layer is CAMetalLayer);
        if (!metrics.HasWindowScene || !metrics.HasMetalLayer)
            throw new InvalidOperationException("The FNA presentation is not attached to one UIScene CAMetalLayer.");
        if (metrics.PointWidth <= metrics.PointHeight || drawableWidth <= drawableHeight)
            throw new InvalidOperationException("The real UIKit/Metal presentation is not landscape.");
        if (previous != metrics)
        {
            RuntimeLog.Info(
                $"scene-metrics reason={reason}; points={metrics.PointWidth:F0}x{metrics.PointHeight:F0}; " +
                $"native-scale={metrics.Scale:F2}; drawable-pixels={drawableWidth}x{drawableHeight}; " +
                $"safe={metrics.SafeTop:F1},{metrics.SafeLeft:F1},{metrics.SafeBottom:F1},{metrics.SafeRight:F1}; " +
                "scene=UIWindowScene; layer=CAMetalLayer");
            previous = metrics;
        }
        return metrics;
    }

    private void OnGeometryChanged(object? sender, EventArgs args) => Capture("client-size-changed");

    public void Dispose()
    {
        if (disposed) return;
        gameWindow.ClientSizeChanged -= OnGeometryChanged;
        disposed = true;
    }
}
