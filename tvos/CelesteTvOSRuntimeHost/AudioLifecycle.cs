#if CELESTE_AUDIO
using Celeste;
using Foundation;
using UIKit;

namespace CelesteTvOSHost;

internal sealed class AudioLifecycle : IDisposable
{
    private readonly List<NSObject> observers = new();

    public AudioLifecycle()
    {
        Observe(UIApplication.WillResignActiveNotification, _ => TvOSStage5BAudioBridge.LifecyclePause("resign-active"));
        Observe(UIApplication.DidEnterBackgroundNotification, _ => TvOSStage5BAudioBridge.LifecyclePause("background"));
        Observe(UIApplication.WillEnterForegroundNotification, _ => RuntimeLog.Info("audio lifecycle: foreground entering"));
        Observe(UIApplication.DidBecomeActiveNotification, _ => TvOSStage5BAudioBridge.LifecycleResume("active"));
        Observe(UIApplication.WillTerminateNotification, _ => TvOSStage5BAudioBridge.ShutdownSafely("termination"));
    }

    private void Observe(NSString notification, Action<NSNotification> callback) =>
        observers.Add(NSNotificationCenter.DefaultCenter.AddObserver(notification, callback));

    public void Dispose()
    {
        foreach (NSObject observer in observers)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
            observer.Dispose();
        }
        observers.Clear();
    }
}
#endif
