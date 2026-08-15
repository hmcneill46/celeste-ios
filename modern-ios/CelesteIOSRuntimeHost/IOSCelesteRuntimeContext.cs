#if IOS_CELESTE_PRODUCT
using Foundation;

namespace CelesteIOSRuntimeHost;

internal static class IOSCelesteRuntimeContext
{
    private static readonly string[] Banks =
    {
        "Master Bank.bank", "Master Bank.strings.bank", "music.bank", "sfx.bank",
        "ui.bank", "dlc_music.bank", "dlc_sfx.bank"
    };

    internal static void Prepare()
    {
        string resources = NSBundle.MainBundle.ResourcePath
            ?? throw new InvalidOperationException("The iOS application resource path is unavailable.");
        string content = Path.Combine(resources, "Content");
        if (!File.Exists(Path.Combine(content, "Effects", "Border.xnb")) ||
            !File.Exists(Path.Combine(content, "Monocle", "MonocleDefault.xnb")))
            throw new InvalidOperationException("Canonical Celeste Content is incomplete.");
        string bankRoot = Path.Combine(content, "FMOD", "Desktop");
        if (Banks.Any(name => !File.Exists(Path.Combine(bankRoot, name))))
            throw new InvalidOperationException("The accepted seven Celeste FMOD banks are incomplete.");

        NSUrl[] supportUrls = NSFileManager.DefaultManager.GetUrls(
            NSSearchPathDirectory.ApplicationSupportDirectory, NSSearchPathDomain.User);
        string support = supportUrls.FirstOrDefault()?.Path
            ?? throw new InvalidOperationException("Application Support is unavailable.");
        string stateRoot = Path.Combine(support, "Celeste");
        Directory.CreateDirectory(stateRoot);

        string incidentalRoot = Path.Combine(Path.GetTempPath(), "Celeste", Environment.ProcessId.ToString());
        Directory.CreateDirectory(incidentalRoot);
        Environment.SetEnvironmentVariable("CELESTE_IOS_STORAGE_ROOT", stateRoot);
        Environment.SetEnvironmentVariable("CELESTE_IOS_INCIDENTAL_ROOT", incidentalRoot);
        Environment.SetEnvironmentVariable("CELESTE_RUNTIME_PROLOGUE_SCENARIO", null);
        Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "Metal");
        Environment.SetEnvironmentVariable("FNA_AUDIO_DISABLE_SOUND", "1");
        Directory.SetCurrentDirectory(resources);
        Version? contentIdentity = typeof(global::Celeste.Content.Stage3AContentIdentity)
            .Assembly.GetName().Version;
        RuntimeLog.Info("celeste-product-context content=canonical-1.4.0.0-a; banks=7; " +
                        $"content-identity={contentIdentity}; storage=Library/Application Support/Celeste; " +
                        "fmod-owner=Celeste; probe-fmod=false");
    }
}
#endif
