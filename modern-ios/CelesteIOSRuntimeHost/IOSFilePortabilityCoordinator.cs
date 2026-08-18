#if IOS_CELESTE_PRODUCT
using Celeste;
using CelesteIOSFoundation;
using CoreGraphics;
using System.Diagnostics;
using Foundation;
using UniformTypeIdentifiers;
using UIKit;

namespace CelesteIOSRuntimeHost;

/// <summary>
/// UIKit-only copy boundary for explicit Files import/export and sharing.
/// External URLs never become game storage and never cross into generated
/// Celeste code; coordinated bounded bytes are returned instead.
/// </summary>
internal sealed class IOSFilePortabilityCoordinator : IDisposable
{
    private const string CelesteSaveType = "io.github.roootthefox.celeste.save-data";
    private readonly object gate = new();
    private UIViewController? presented;
    private string? temporaryRoot;
    private bool disposed;
    private int presentationSequence;
    private long presentationStarted;

    internal IOSFilePortabilityCoordinator()
    {
        IOSFilePortabilityBridge.ImportRequested = PresentImport;
        IOSFilePortabilityBridge.ExportRequested = PresentExport;
        IOSFilePortabilityBridge.SystemPresentationChanged = active =>
        {
            IOSTouchControls.Reset(active ? "system-document-ui-open" : "system-document-ui-close");
            IOSPresentationCoordinator.Invalidate(active ? "system-document-ui-open" : "system-document-ui-close");
        };
        CleanupAbandonedExports();
    }

    private void PresentImport(
        IOSPortableDocumentKind kind,
        int maximumBytes,
        Action<IOSExternalReadResult> completed)
    {
        UIApplication.SharedApplication.BeginInvokeOnMainThread(() =>
        {
            if (!TryBegin(out UIViewController owner))
            {
                completed(IOSExternalReadResult.Failure("Files is already open."));
                return;
            }

            UTType type = UTType.CreateFromIdentifier(kind == IOSPortableDocumentKind.TouchLayout
                ? TouchLayoutShareDocument.TypeIdentifier : CelesteSaveType) ?? UTTypes.Data;
            UIDocumentPickerViewController picker = new(new[] { type }, asCopy: true)
            {
                AllowsMultipleSelection = false,
                ShouldShowFileExtensions = true,
            };
            presented = picker;
            picker.DidPickDocumentAtUrls += (_, args) =>
            {
                Trace("picker-selected");
                NSUrl? url = args.Urls.FirstOrDefault();
                if (url is null)
                {
                    FinishPresentation(() =>
                        completed(IOSExternalReadResult.Failure("The selected file could not be read.")));
                    return;
                }
                ReadExternal(url, maximumBytes, result =>
                    FinishPresentation(() => completed(result)));
            };
            picker.WasCancelled += (_, _) =>
            {
                Trace("picker-cancelled");
                FinishPresentation(() => completed(IOSExternalReadResult.Cancel()));
            };
            owner.PresentViewController(picker, true, null);
        });
    }

    private void PresentExport(
        IReadOnlyList<IOSPortableDocument> documents,
        bool share,
        Action<bool, string?> completed)
    {
        UIApplication.SharedApplication.BeginInvokeOnMainThread(() =>
        {
            if (!TryBegin(out UIViewController owner))
            {
                completed(false, "Files is already open.");
                return;
            }
            try
            {
                temporaryRoot = CreateTemporaryExports(documents, out NSUrl[] urls);
                if (share)
                {
                    UIActivityViewController sheet = new(urls.Cast<NSObject>().ToArray(), null);
                    sheet.CompletionWithItemsHandler = (_, _, _, error) =>
                    {
                        Trace("share-completed");
                        FinishPresentation(() => completed(
                            error is null, error is null ? null : "The share operation could not finish."));
                    };
                    ConfigurePopover(sheet, owner);
                    presented = sheet;
                    owner.PresentViewController(sheet, true, null);
                    return;
                }

                UIDocumentPickerViewController picker = new(urls, asCopy: true)
                {
                    ShouldShowFileExtensions = true,
                };
                presented = picker;
                picker.DidPickDocumentAtUrls += (_, _) =>
                {
                    Trace("export-selected");
                    FinishPresentation(() => completed(true, null));
                };
                picker.WasCancelled += (_, _) =>
                {
                    Trace("export-cancelled");
                    FinishPresentation(() => completed(false, null));
                };
                owner.PresentViewController(picker, true, null);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                FinishPresentation(() => completed(false, "The export copy could not be prepared."));
            }
        });
    }

    private static void ReadExternal(NSUrl url, int maximumBytes, Action<IOSExternalReadResult> completed)
    {
        bool accessing = url.StartAccessingSecurityScopedResource();
        Task.Run(() =>
        {
            IOSExternalReadResult result;
            try
            {
                byte[]? bytes = null;
                string? callbackFailure = null;
                using NSFileCoordinator coordinator = new();
                coordinator.CoordinateRead(url, NSFileCoordinatorReadingOptions.WithoutChanges,
                    out NSError coordinateError, coordinatedUrl =>
                    {
                        // Never let a managed exception cross this native block
                        // boundary. .NET for iOS marshals that as an uncaught
                        // Objective-C exception before the outer managed catch
                        // can observe it.
                        try
                        {
                            NSFileAttributes? attributes = NSFileManager.DefaultManager.GetAttributes(
                                coordinatedUrl.Path!, out NSError attributeError);
                            if (attributes is null || attributeError is not null || attributes.Type != NSFileType.Regular)
                            {
                                callbackFailure = "unreadable";
                                return;
                            }
                            if (attributes.Size == 0 || attributes.Size > (ulong)maximumBytes)
                            {
                                callbackFailure = "bounds";
                                return;
                            }
                            using NSData? data = NSData.FromUrl(coordinatedUrl);
                            if (data is null)
                            {
                                callbackFailure = "unreadable";
                                return;
                            }
                            if (data.Length == 0 || data.Length > (nuint)maximumBytes)
                            {
                                callbackFailure = "bounds";
                                return;
                            }
                            bytes = data.ToArray();
                        }
                        catch (Exception exception) when (
                            exception is IOException or InvalidDataException or UnauthorizedAccessException)
                        {
                            callbackFailure = exception is InvalidDataException ? "bounds" : "unreadable";
                        }
                    });
                if (coordinateError is not null)
                    throw new IOException("The file provider could not coordinate the selected file.");
                if (callbackFailure == "bounds")
                    throw new InvalidDataException("The selected file is empty or too large.");
                if (callbackFailure is not null)
                    throw new IOException("The selected file could not be read.");
                result = bytes is null
                    ? IOSExternalReadResult.Failure("The selected file could not be read.")
                    : IOSExternalReadResult.Success(bytes);
            }
            catch (InvalidDataException)
            {
                result = IOSExternalReadResult.Failure("The selected file is empty or too large.");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                result = IOSExternalReadResult.Failure("The selected file could not be read.");
            }
            finally
            {
                if (accessing) url.StopAccessingSecurityScopedResource();
            }
            UIApplication.SharedApplication.BeginInvokeOnMainThread(() => completed(result));
        });
    }

    private bool TryBegin(out UIViewController owner)
    {
        owner = null!;
        lock (gate)
        {
            if (disposed || presented is not null) return false;
            owner = FindPresentationOwner()!;
            if (owner is null) return false;
            presentationSequence++;
            presentationStarted = Stopwatch.GetTimestamp();
            Trace("begin");
            IOSFilePortabilityBridge.SystemPresentationChanged?.Invoke(true);
            return true;
        }
    }

    /// <summary>
    /// A document picker or share sheet returns directly to Celeste's own
    /// confirmation UI. Run that continuation only after the system controller
    /// has completely dismissed so touch ownership cannot cross the boundary.
    /// </summary>
    private void FinishPresentation(Action continuation)
    {
        Trace("finish-requested");
        UIViewController? controller;
        lock (gate)
        {
            controller = presented;
            presented = null;
        }

        void Finish()
        {
            Trace("finish-continuation");
            controller?.Dispose();
            lock (gate)
            {
                CleanupTemporaryRoot();
            }
            IOSFilePortabilityBridge.SystemPresentationChanged?.Invoke(false);
            continuation();
        }

        int checks = 0;
        void CheckDismissed()
        {
            bool detached = controller is null ||
                            controller.PresentingViewController is null ||
                            controller.View?.Window is null;
            if (detached)
            {
                Trace($"dismissed checks={checks}");
                Finish();
                return;
            }

            if (++checks <= 200)
            {
                CheckAgainAfterUIKitAdvances();
                return;
            }

            // Every picker and activity controller used here normally dismisses
            // itself. If an OS/controller defect leaves one attached
            // for five seconds, fail closed with one non-animated public UIKit
            // dismissal instead of triggering UIKit's ~30-second transition
            // timeout or presenting another controller over it.
            RuntimeLog.Warning($"files-ui seq={presentationSequence}; phase=dismiss-timeout; elapsed-ms={ElapsedMilliseconds()}");
            controller!.DismissViewController(false, () =>
                UIApplication.SharedApplication.BeginInvokeOnMainThread(Finish));
        }

        async void CheckAgainAfterUIKitAdvances()
        {
            await Task.Delay(25).ConfigureAwait(false);
            UIApplication.SharedApplication.BeginInvokeOnMainThread(CheckDismissed);
        }

        // Picker selection and share completion initiate their own UIKit
        // dismissal. Waiting for the exact controller to
        // detach avoids racing that automatic transition on both iOS 26 and
        // iPadOS 15 without scanning view hierarchies or polling every frame.
        CheckAgainAfterUIKitAdvances();
    }

    private void Trace(string phase) => RuntimeLog.Info(
        $"files-ui seq={presentationSequence}; phase={phase}; elapsed-ms={ElapsedMilliseconds()}; " +
        $"controller={(presented is null ? "none" : presented.GetType().Name)}");

    private long ElapsedMilliseconds() => presentationStarted == 0
        ? 0
        : (long)Stopwatch.GetElapsedTime(presentationStarted).TotalMilliseconds;

    private static UIViewController? FindPresentationOwner()
    {
        UIWindow? window = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(scene => scene.Windows)
            .FirstOrDefault(candidate => candidate.IsKeyWindow) ??
            UIApplication.SharedApplication.ConnectedScenes
                .OfType<UIWindowScene>()
                .SelectMany(scene => scene.Windows)
                .FirstOrDefault(candidate => !candidate.Hidden);
        UIViewController? owner = window?.RootViewController;
        while (owner?.PresentedViewController is not null) owner = owner.PresentedViewController;
        if (owner is UINavigationController navigation) owner = navigation.VisibleViewController ?? owner;
        if (owner is UITabBarController tabs) owner = tabs.SelectedViewController ?? owner;
        return owner;
    }

    private static void ConfigurePopover(UIActivityViewController sheet, UIViewController owner)
    {
        UIPopoverPresentationController? popover = sheet.PopoverPresentationController;
        if (popover is null) return;
        UIView sourceView = owner.View ?? throw new IOException("The share presentation view is unavailable.");
        popover.SourceView = sourceView;
        CGRect bounds = sourceView.Bounds;
        popover.SourceRect = new CGRect(bounds.GetMidX(), bounds.GetMidY(), 1, 1);
        popover.PermittedArrowDirections = (UIPopoverArrowDirection)0;
    }

    private static string CreateTemporaryExports(
        IReadOnlyList<IOSPortableDocument> documents,
        out NSUrl[] urls)
    {
        string root = Path.Combine(Path.GetTempPath(), "CelesteExports", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        List<NSUrl> created = new(documents.Count);
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (IOSPortableDocument document in documents)
        {
            string name = Path.GetFileName(document.FileName);
            if (name != document.FileName || string.IsNullOrWhiteSpace(name) || !names.Add(name))
                throw new InvalidDataException("An export filename is invalid.");
            string path = Path.Combine(root, name);
            using NSData data = NSData.FromArray(document.Data);
            using NSUrl url = NSUrl.FromFilename(path);
            if (!data.Save(url, atomically: true))
                throw new IOException("Foundation could not create the export copy.");
            created.Add(NSUrl.FromFilename(path));
        }
        urls = created.ToArray();
        return root;
    }

    private void CleanupTemporaryRoot()
    {
        string? root = temporaryRoot;
        temporaryRoot = null;
        if (root is null || !Directory.Exists(root)) return;
        try { Directory.Delete(root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void CleanupAbandonedExports()
    {
        string root = Path.Combine(Path.GetTempPath(), "CelesteExports");
        if (!Directory.Exists(root)) return;
        try { Directory.Delete(root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            presented?.DismissViewController(false, null);
            presented?.Dispose();
            presented = null;
            CleanupTemporaryRoot();
            IOSFilePortabilityBridge.Clear();
        }
    }
}
#endif
