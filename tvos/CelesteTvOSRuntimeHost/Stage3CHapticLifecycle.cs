#if CELESTE_RUNTIME
using Celeste;
using Foundation;
using UIKit;

namespace CelesteTvOSHost;

internal sealed class Stage3CHapticLifecycle : IDisposable
{
    private readonly List<NSObject> observers = new();

    public Stage3CHapticLifecycle()
    {
        Observe(UIApplication.WillResignActiveNotification, "resign-active");
        Observe(UIApplication.DidEnterBackgroundNotification, "background");
        Observe(UIApplication.WillTerminateNotification, "shutdown");
    }

    private void Observe(NSString notification, string reason)
    {
        observers.Add(NSNotificationCenter.DefaultCenter.AddObserver(notification, _ =>
        {
            TvOSStage3CBridge.StopAllRumble(reason);
            TvOSStage3CBridge.Checkpoint("haptic-lifecycle-stop", $"reason={reason}");
        }));
    }

    public void Dispose()
    {
        TvOSStage3CBridge.StopAllRumble("shutdown");
        foreach (NSObject observer in observers)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
            observer.Dispose();
        }
        observers.Clear();
    }
}
#endif
