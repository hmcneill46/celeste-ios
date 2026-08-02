using Foundation;
using UIKit;

namespace CelesteTvOSHost;

internal sealed class LifecycleMonitor : IDisposable
{
    private readonly List<NSObject> observers = new();
    private bool disposed;

    public LifecycleMonitor()
    {
        Observe(UIApplication.DidBecomeActiveNotification, "active");
        Observe(UIApplication.WillResignActiveNotification, "resign-active");
        Observe(UIApplication.DidEnterBackgroundNotification, "background");
        Observe(UIApplication.WillEnterForegroundNotification, "foreground");
        Observe(UIApplication.WillTerminateNotification, "termination");
        Stage2Log.Info($"lifecycle launch: SDL main callback; state={UIApplication.SharedApplication.ApplicationState}");
    }

    private void Observe(NSString notification, string eventName)
    {
        observers.Add(
            NSNotificationCenter.DefaultCenter.AddObserver(
                notification,
                _ => Stage2Log.Info(
                    $"lifecycle {eventName}; state={UIApplication.SharedApplication.ApplicationState}"
                )
            )
        );
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        foreach (NSObject observer in observers)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
            observer.Dispose();
        }

        observers.Clear();
        disposed = true;
        Stage2Log.Info("lifecycle monitor disposed");
    }
}
