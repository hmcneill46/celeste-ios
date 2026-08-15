using CelesteIOSFoundation;
using Foundation;

namespace CelesteIOSRuntimeHost;

internal static class ApplicationSupportStorage
{
    internal static AtomicFileStore Create()
    {
        NSUrl[] urls = NSFileManager.DefaultManager.GetUrls(
            NSSearchPathDirectory.ApplicationSupportDirectory,
            NSSearchPathDomain.User);
        string basePath = urls.FirstOrDefault()?.Path
            ?? throw new InvalidOperationException("Application Support is unavailable.");
        string root = Path.Combine(basePath, "Celeste");
        AtomicFileStore store = new(root);
        RuntimeLog.Info("storage-root=Library/Application Support/Celeste; created=true; save-files-created=false");
        return store;
    }
}
