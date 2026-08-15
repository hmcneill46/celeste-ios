#if IOS_CELESTE_PRODUCT
using AVFoundation;
using Celeste;
using Foundation;
using UIKit;

namespace CelesteIOSRuntimeHost;

/// <summary>
/// Owns the narrow iOS audio-session policy around Celeste's existing FMOD runtime.
/// FMOD remains the sole audio engine; this coordinator only establishes the public
/// playback category and restores the OS session before the existing FMOD root bus
/// is resumed after an interruption or application lifecycle transition.
/// </summary>
internal sealed class IOSAudioSessionCoordinator : IDisposable
{
    private readonly object gate = new();
    private readonly NSObject interruptionObserver;
    private readonly NSObject routeObserver;
    private bool activationScheduled;
    private bool resumeFmodPending;
    private string pendingReason = "unspecified";
    private bool disposed;

    internal IOSAudioSessionCoordinator()
    {
        interruptionObserver = AVAudioSession.Notifications.ObserveInterruption((_, args) =>
        {
            if (args.InterruptionType == AVAudioSessionInterruptionType.Began)
            {
                AppleAudioDiagnostics.LifecyclePause("audio-interruption-began");
                RuntimeLog.Info("ios-audio-session=interrupted; fmod-root-paused=true");
                return;
            }

            ScheduleActivation("audio-interruption-ended", resumeFmod: true);
        });

        routeObserver = AVAudioSession.Notifications.ObserveRouteChange((_, args) =>
            ScheduleActivation($"audio-route-change-{args.Reason}", resumeFmod: false));
    }

    internal static void PrepareBeforeRuntime()
    {
        AVAudioSession session = AVAudioSession.SharedInstance();
        NSError? error = session.SetCategory(
            AVAudioSessionCategory.Playback,
            AVAudioSessionCategoryOptions.DuckOthers);

        if (error is null)
        {
            RuntimeLog.Info("ios-audio-session-policy=playback; silent-switch-ignored=true; phase=pre-runtime");
        }
        else
        {
            RuntimeLog.Warning($"ios-audio-session-category-failed; phase=pre-runtime; code={error.Code}");
        }
    }

    internal void Pause(string reason)
    {
        AppleAudioDiagnostics.LifecyclePause(reason);
    }

    internal void Resume(string reason)
    {
        ScheduleActivation(reason, resumeFmod: true);
    }

    private void ScheduleActivation(string reason, bool resumeFmod)
    {
        lock (gate)
        {
            if (disposed)
                return;

            resumeFmodPending |= resumeFmod;
            pendingReason = reason;
            if (activationScheduled)
                return;

            activationScheduled = true;
        }

        // Run after every observer for the current UIKit notification has had a
        // chance to settle its own AudioQueue/FMOD state.
        UIApplication.SharedApplication.BeginInvokeOnMainThread(ApplyPendingActivation);
    }

    private void ApplyPendingActivation()
    {
        string reason;
        bool resumeFmod;
        lock (gate)
        {
            if (disposed)
                return;

            reason = pendingReason;
            resumeFmod = resumeFmodPending;
            activationScheduled = false;
            resumeFmodPending = false;
        }

        AVAudioSession session = AVAudioSession.SharedInstance();
        NSError? categoryError = session.SetCategory(
            AVAudioSessionCategory.Playback,
            AVAudioSessionCategoryOptions.DuckOthers);
        if (categoryError is not null)
        {
            RuntimeLog.Warning($"ios-audio-session-category-failed; phase=reactivation; reason={reason}; code={categoryError.Code}");
            return;
        }

        bool activated = session.SetActive(true, out NSError? activationError);
        if (!activated || activationError is not null)
        {
            long code = activationError?.Code ?? 0;
            RuntimeLog.Warning($"ios-audio-session-activation-failed; reason={reason}; code={code}; fmod-root-resume-blocked={resumeFmod.ToString().ToLowerInvariant()}");
            return;
        }

        if (resumeFmod)
            AppleAudioDiagnostics.LifecycleResume(reason);

        string outputs = string.Join(",", session.CurrentRoute.Outputs.Select(output => output.PortType.ToString()));
        RuntimeLog.Info($"ios-audio-session=active; category=playback; reason={reason}; outputs={outputs}; fmod-root-resume-requested={resumeFmod.ToString().ToLowerInvariant()}");
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
        }

        interruptionObserver.Dispose();
        routeObserver.Dispose();
    }
}
#endif
