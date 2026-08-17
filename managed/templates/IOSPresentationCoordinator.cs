using System;
using CelesteIOSFoundation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using ObjCRuntime;
using SDL2;
using UIKit;

namespace Celeste;

public static class IOSPresentationCoordinator
{
    private static bool dirty = true;
    private static IOSPresentation presentation;

    internal static IOSPresentation Current => presentation;

    public static void Invalidate(string reason)
    {
        dirty = true;
        IOSTouchControls.Reset(reason);
    }

    internal static bool Ensure()
    {
        if (!dirty && presentation.IsValid) return true;
        try
        {
            if (Celeste.Instance?.Window == null || Celeste.Instance.GraphicsDevice == null) return false;
            SDL.SDL_SysWMinfo info = default;
            SDL.SDL_VERSION(out info.version);
            if (SDL.SDL_GetWindowWMInfo(Celeste.Instance.Window.Handle, ref info) != SDL.SDL_bool.SDL_TRUE ||
                info.subsystem != SDL.SDL_SYSWM_TYPE.SDL_SYSWM_UIKIT || info.info.uikit.window == IntPtr.Zero)
                throw new InvalidOperationException("The SDL UIKit presentation window is unavailable.");
            UIWindow window = Runtime.GetNSObject<UIWindow>(info.info.uikit.window)
                ?? throw new InvalidOperationException("The SDL UIWindow wrapper is unavailable.");
            SDL.SDL_Metal_GetDrawableSize(Celeste.Instance.Window.Handle, out int drawableWidth, out int drawableHeight);
            double nativeScale = window.Screen.NativeScale;
            if (drawableWidth <= 0 || drawableHeight <= 0 ||
                drawableWidth != (int)Math.Round(window.Bounds.Width * nativeScale) ||
                drawableHeight != (int)Math.Round(window.Bounds.Height * nativeScale))
                throw new InvalidOperationException("The UIKit geometry and Metal drawable disagree.");

            PresentationParameters current = Celeste.Instance.GraphicsDevice.PresentationParameters;
            if (current.BackBufferWidth != drawableWidth || current.BackBufferHeight != drawableHeight)
            {
                Engine.Graphics.PreferredBackBufferWidth = drawableWidth;
                Engine.Graphics.PreferredBackBufferHeight = drawableHeight;
                PresentationParameters corrected = current.Clone();
                corrected.BackBufferWidth = drawableWidth;
                corrected.BackBufferHeight = drawableHeight;
                corrected.DisplayOrientation = DisplayOrientation.LandscapeLeft;
                Celeste.Instance.GraphicsDevice.Reset(corrected);
                current = Celeste.Instance.GraphicsDevice.PresentationParameters;
                if (current.BackBufferWidth != drawableWidth || current.BackBufferHeight != drawableHeight)
                    throw new InvalidOperationException("The iOS backbuffer could not be aligned to the Metal drawable.");
            }

            UIEdgeInsets safe = window.SafeAreaInsets;
            SafeAreaMetrics safeArea = new(safe.Top, safe.Left, safe.Bottom, safe.Right);
            if (!safeArea.IsValidFor(window.Bounds.Width, window.Bounds.Height))
                throw new InvalidOperationException("The iOS safe-area geometry is invalid.");
            presentation = new IOSPresentation(
                window.Bounds.Width,
                window.Bounds.Height,
                nativeScale,
                drawableWidth,
                drawableHeight,
                UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad,
                safeArea,
                true);
            dirty = false;
            return true;
        }
        catch (Exception exception)
        {
            dirty = true;
            presentation = default;
            Console.WriteLine($"iOS presentation unavailable: {exception.GetType().Name}; normal game continues");
            return false;
        }
    }

    internal readonly record struct IOSPresentation(
        double PointWidth,
        double PointHeight,
        double NativeScale,
        int PixelWidth,
        int PixelHeight,
        bool IsPad,
        SafeAreaMetrics SafeArea,
        bool IsValid);
}
