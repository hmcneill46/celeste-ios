#if CELESTE_RUNTIME && TVOS_CELESTE_RUNTIME_HOST
using Celeste;
using Foundation;
using UIKit;

namespace CelesteTvOSHost;

internal sealed class PersistenceLifecycle : IDisposable
{
    private readonly List<NSObject> observers = new();
    private bool disposed;

    internal PersistenceLifecycle(PersistenceStore persistenceStore)
    {
        _ = persistenceStore ?? throw new ArgumentNullException(nameof(persistenceStore));
        Observe(UIApplication.WillResignActiveNotification, "resign-active");
        Observe(UIApplication.DidEnterBackgroundNotification, "background");
        Observe(UIApplication.WillTerminateNotification, "termination");
    }

    private void Observe(NSString notification, string reason)
    {
        observers.Add(NSNotificationCenter.DefaultCenter.AddObserver(notification, _ =>
        {
            bool result = TvOSStage6PersistenceHooks.Flush(reason);
            RuntimeLog.Info($"STAGE6_LIFECYCLE_FLUSH reason={reason}; result={(result ? "completed-or-unchanged" : "failed")}");
        }));
    }

    public void Dispose()
    {
        if (disposed) return;
        bool result = TvOSStage6PersistenceHooks.Flush("lifecycle-disposal");
        RuntimeLog.Info($"STAGE6_LIFECYCLE_FLUSH reason=disposal; result={(result ? "completed-or-unchanged" : "failed")}");
        foreach (NSObject observer in observers)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
            observer.Dispose();
        }
        observers.Clear();
        disposed = true;
    }
}
#endif
