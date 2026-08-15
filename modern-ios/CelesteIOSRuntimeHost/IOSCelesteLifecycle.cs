#if IOS_CELESTE_PRODUCT
using Celeste;
using Foundation;
using UIKit;

namespace CelesteIOSRuntimeHost;

internal sealed class IOSCelesteLifecycle : IDisposable
{
    private readonly IOSAudioSessionCoordinator audioSession = new();
    private readonly NSObject inactive;
    private readonly NSObject active;
    private readonly NSObject background;
    private bool disposed;

    internal IOSCelesteLifecycle()
    {
        NSNotificationCenter center = NSNotificationCenter.DefaultCenter;
        inactive = center.AddObserver(UIApplication.WillResignActiveNotification, _ => Pause("will-resign-active"));
        background = center.AddObserver(UIApplication.DidEnterBackgroundNotification, _ => Pause("did-enter-background"));
        active = center.AddObserver(UIApplication.DidBecomeActiveNotification, _ => Resume("did-become-active"));
    }

    private void Pause(string reason)
    {
        AppleRuntimeDiagnostics.StopAllRumble(reason);
        audioSession.Pause(reason);
        RuntimeLog.Info($"celeste-lifecycle={reason}; runtime-disposed=false");
    }

    private void Resume(string reason)
    {
        audioSession.Resume(reason);
        RuntimeLog.Info($"celeste-lifecycle={reason}; existing-runtime-resumed=true");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        NSNotificationCenter center = NSNotificationCenter.DefaultCenter;
        center.RemoveObserver(inactive);
        center.RemoveObserver(active);
        center.RemoveObserver(background);
        audioSession.Dispose();
    }
}
#endif
