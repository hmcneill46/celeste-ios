#if CELESTE_RUNTIME && TVOS_CELESTE_RUNTIME_HOST
using Celeste;
using Foundation;
using GameController;

namespace CelesteTvOSHost;

internal sealed class ControllerPromptPreferences : IDisposable
{
    private readonly object gate = new();
    private readonly ControllerPromptPreferenceState preference;
    private readonly List<NSObject> observers = new();
    private AppleControllerFamily appleFamily;
    private bool disposed;

    internal ControllerPromptPreferences()
    {
        preference = new ControllerPromptPreferenceState(new UserDefaultsStore());
        RefreshControllers();
        Observe(GCController.DidConnectNotification);
        Observe(GCController.DidDisconnectNotification);
        Observe(GCController.DidBecomeCurrentNotification);
        Observe(GCController.DidStopBeingCurrentNotification);
        TvOSControllerPromptHooks.ModeRequested = GetMode;
        TvOSControllerPromptHooks.ModeChanged = SetMode;
        TvOSControllerPromptHooks.PrefixRequested = ResolvePrefix;
        RuntimeLog.Info($"STAGE11_PROMPTS ready=true; requested={preference.Mode}; effective={EffectiveFamilyForLog()}; key-schema=v1");
    }

    private TvOSControllerPromptMode GetMode()
    {
        lock (gate) return (TvOSControllerPromptMode)(int)preference.Mode;
    }

    private void SetMode(TvOSControllerPromptMode requested)
    {
        lock (gate)
        {
            ControllerPromptMode mode = (ControllerPromptMode)(int)requested;
            bool changed = preference.Set(mode);
            RuntimeLog.Info($"STAGE11_PROMPTS requested={preference.Mode}; effective={EffectiveFamilyForLog()}; persisted={(changed ? "changed" : "unchanged")}; bindings=untouched");
        }
    }

    private string ResolvePrefix(string automaticPrefix)
    {
        lock (gate)
            return ControllerPromptPolicy.ResolvePrefix(preference.Mode, automaticPrefix, appleFamily);
    }

    private void Observe(NSString notification) =>
        observers.Add(NSNotificationCenter.DefaultCenter.AddObserver(notification, _ => RefreshControllers()));

    private void RefreshControllers()
    {
        lock (gate)
        {
            GCController? current = GCController.Current;
            GCController[] connected = GCController.Controllers ?? Array.Empty<GCController>();
            List<ControllerCandidate> candidates = new(connected.Length + 1);
            for (int index = 0; index < connected.Length; index++)
            {
                GCController controller = connected[index];
                candidates.Add(new ControllerCandidate(
                    SameController(controller, current),
                    controller.ExtendedGamepad != null,
                    Classify(controller.ProductCategory),
                    index
                ));
            }
            if (current != null && !connected.Any(controller => SameController(controller, current)))
            {
                candidates.Add(new ControllerCandidate(
                    true,
                    current.ExtendedGamepad != null,
                    Classify(current.ProductCategory),
                    connected.Length
                ));
            }
            AppleControllerFamily previous = appleFamily;
            appleFamily = ControllerPromptPolicy.SelectAppleFamily(candidates);
            if (previous != appleFamily)
                RuntimeLog.Info($"STAGE11_CONTROLLER_CHANGE apple-family={appleFamily}; connected-extended={candidates.Count(candidate => candidate.HasExtendedGamepad)}; private-identity=not-logged");
        }
    }

    private static AppleControllerFamily Classify(string? productCategory)
    {
        if (EqualsCategory(productCategory, GCProductCategory.DualSense) ||
            EqualsCategory(productCategory, GCProductCategory.DualShock4))
            return AppleControllerFamily.PlayStation;
        if (EqualsCategory(productCategory, GCProductCategory.XboxOne))
            return AppleControllerFamily.Xbox;
        if (EqualsCategory(productCategory, GCProductCategory.SiriRemote1stGen) ||
            EqualsCategory(productCategory, GCProductCategory.SiriRemote2ndGen) ||
            EqualsCategory(productCategory, GCProductCategory.ControlCenterRemote) ||
            EqualsCategory(productCategory, GCProductCategory.UniversalElectronicsRemote) ||
            EqualsCategory(productCategory, GCProductCategory.CoalescedRemote))
            return AppleControllerFamily.Remote;
        return AppleControllerFamily.Unknown;
    }

    private static bool EqualsCategory(string? value, NSString category) =>
        string.Equals(value, category.ToString(), StringComparison.Ordinal);

    private static bool SameController(GCController controller, GCController? other) =>
        other != null && controller.Handle == other.Handle;

    private string EffectiveFamilyForLog() =>
        ControllerPromptPolicy.ResolvePrefix(preference.Mode, "xb1", appleFamily) switch
        {
            "ps4" => "PlayStation",
            "ns" => "NintendoSwitch",
            "stadia" => "Stadia",
            _ => "Xbox"
        };

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            TvOSControllerPromptHooks.Reset();
            foreach (NSObject observer in observers)
            {
                NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
                observer.Dispose();
            }
            observers.Clear();
            disposed = true;
        }
    }

    private sealed class UserDefaultsStore : IControllerPromptPreferenceStore
    {
        private readonly NSUserDefaults defaults = NSUserDefaults.StandardUserDefaults;

        public string? Read() => defaults.StringForKey(ControllerPromptPolicy.PreferenceKey);

        public void Write(string value)
        {
            defaults.SetString(value, ControllerPromptPolicy.PreferenceKey);
            defaults.Synchronize();
        }
    }
}
#endif
