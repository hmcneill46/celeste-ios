#if CELESTE_RUNTIME && TVOS_STAGE6_HOST
using Celeste;
using Foundation;
using UIKit;

namespace CelesteTvOSHost;

internal sealed class Stage12BQuitCoordinator : IDisposable
{
    private readonly object gate = new();
    private readonly Stage10ASaveManager saveManager;
    private readonly Stage12BQuitStateMachine stateMachine = new();
    private readonly List<NSObject> observers = new();
    private bool disposed;

    internal Stage12BQuitCoordinator(Stage10ASaveManager manager)
    {
        saveManager = manager ?? throw new ArgumentNullException(nameof(manager));
        TvOSQuitHooks.BeginRequested = BeginLeave;
        TvOSQuitHooks.PrepareRequested = PrepareLeave;
        TvOSQuitHooks.CancelRequested = CancelLeave;
        Observe(UIApplication.WillResignActiveNotification, OnWillResignActive);
        Observe(UIApplication.DidEnterBackgroundNotification, OnDidEnterBackground);
        Observe(UIApplication.DidBecomeActiveNotification, OnDidBecomeActive);
        Stage3BLog.Info("STAGE12B_QUIT state=inactive; engine-exit-intercept=main-menu-only");
    }

    internal Stage12BQuitState State { get { lock (gate) return stateMachine.State; } }

    private bool BeginLeave()
    {
        lock (gate)
        {
            if (disposed || saveManager.RestartRequired) return false;
            bool accepted = stateMachine.TryBegin();
            Stage3BLog.Info($"STAGE12B_QUIT event=request; result={(accepted ? "accepted" : "duplicate-suppressed")}; state={StateName(stateMachine.State)}");
            return accepted;
        }
    }

    private TvOSQuitPreparationResult PrepareLeave(bool waitedForSave, bool savingResult)
    {
        lock (gate)
        {
            if (disposed) return TvOSQuitPreparationResult.Failed;
            if (saveManager.RestartRequired)
            {
                FailPreparation("restart-required");
                return TvOSQuitPreparationResult.RestartRequired;
            }
            if (UserIO.Saving) return TvOSQuitPreparationResult.Busy;
            if (waitedForSave && !savingResult)
            {
                FailPreparation("userio-save-failed");
                return TvOSQuitPreparationResult.SaveFailed;
            }

            try
            {
                bool flushed = TvOSStage6PersistenceHooks.Flush("stage12b-user-leave");
                Stage3BLog.Info($"STAGE12B_QUIT event=durable-flush; result={(flushed ? "verified-or-unchanged" : "failed")}");
                if (!flushed)
                {
                    FailPreparation("durable-flush-failed");
                    return TvOSQuitPreparationResult.FlushFailed;
                }

                saveManager.StopForLeave();
                TvOSStage3CBridge.StopAllRumble("stage12b-leave-ready");
                stateMachine.PreparationSucceeded();
                Stage3BLog.Info("STAGE12B_QUIT event=ready; save-manager=stopped; haptics=zero; runtime=retained; state=awaiting-background");
                return TvOSQuitPreparationResult.Ready;
            }
            catch (Exception exception)
            {
                FailPreparation($"exception-{exception.GetType().Name}");
                Stage3BLog.Error($"STAGE12B_QUIT event=prepare-exception; type={exception.GetType().Name}; detail=redacted");
                return TvOSQuitPreparationResult.Failed;
            }
        }
    }

    private void FailPreparation(string category)
    {
        if (stateMachine.State == Stage12BQuitState.PreparingToLeave)
            stateMachine.PreparationFailed();
        Stage3BLog.Warning($"STAGE12B_QUIT event=prepare-failed; category={category}; runtime=retained; state=failure");
    }

    private void CancelLeave(string reason)
    {
        lock (gate)
        {
            if (disposed) return;
            bool changed = stateMachine.Cancel();
            Stage3BLog.Info($"STAGE12B_QUIT event=cancel; reason={Sanitize(reason)}; result={(changed ? "main-menu-restored" : "unchanged")}; state={StateName(stateMachine.State)}");
        }
    }

    private void OnWillResignActive(NSNotification _)
    {
        lock (gate)
        {
            stateMachine.WillResignActive();
            Stage3BLog.Info($"STAGE12B_QUIT lifecycle=resign-active; state={StateName(stateMachine.State)}; completion=false");
        }
    }

    private void OnDidEnterBackground(NSNotification _)
    {
        lock (gate)
        {
            bool completed = stateMachine.DidEnterBackground();
            Stage3BLog.Info($"STAGE12B_QUIT lifecycle=background; leave-completed={completed.ToString().ToLowerInvariant()}; state={StateName(stateMachine.State)}");
        }
    }

    private void OnDidBecomeActive(NSNotification _)
    {
        Stage12BForegroundAction action;
        lock (gate)
        {
            action = stateMachine.DidBecomeActive(saveManager.RestartRequired);
            Stage3BLog.Info($"STAGE12B_QUIT lifecycle=active; action={ActionName(action)}; restart-required={saveManager.RestartRequired.ToString().ToLowerInvariant()}; state={StateName(stateMachine.State)}");
        }
        if (action == Stage12BForegroundAction.RestoreMainMenu)
            TvOSQuitHooks.RequestForegroundMainMenu();
    }

    private void Observe(NSString notification, Action<NSNotification> callback) =>
        observers.Add(NSNotificationCenter.DefaultCenter.AddObserver(notification, callback));

    private static string StateName(Stage12BQuitState state) => state.ToString().ToLowerInvariant();
    private static string ActionName(Stage12BForegroundAction action) => action.ToString().ToLowerInvariant();
    private static string Sanitize(string value) => new((value ?? "unknown").Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').Take(48).ToArray());

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            TvOSQuitHooks.ResetHostCallbacks();
            foreach (NSObject observer in observers)
            {
                NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
                observer.Dispose();
            }
            observers.Clear();
        }
    }
}
#endif
