#if IOS_CELESTE_RUNTIME_HOST
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CelesteIOSFoundation;
using Foundation;

namespace Celeste;

/// <summary>
/// Narrow iOS file boundary for Celeste's four ordinary logical files and
/// read-only bundled Content. Durable writes use Foundation's native atomic
/// replacement path so full-AOT iOS never enters System.IO's JIT-only
/// SafeFileHandle construction path. The tvOS UserDefaults persistence system
/// is intentionally not part of the iOS product.
/// </summary>
public static class IOSStorageHooks
{
    private static readonly HashSet<string> LogicalNames = new(StringComparer.Ordinal)
    {
        "settings", "0", "1", "2"
    };

    private static readonly CelesteFileDurabilityStore DurableStore = new(
        new FoundationDurabilityBackend(), new CanonicalCelesteValidator());

    public static void ValidateLogicalName(string logicalName)
    {
        if (!LogicalNames.Contains(logicalName))
            throw new InvalidOperationException($"iOS rejects unsupported Celeste file '{logicalName}'.");
    }

    public static void BeginBatch() { }
    public static void FileChanged(string logicalName, string operation) => ValidateLogicalName(logicalName);
    public static bool EndBatch(bool successful, string reason) => successful;
    public static bool Flush(string reason) => true;

    public static bool SaveLogicalFile(string logicalName, byte[] payload)
    {
        ValidateLogicalName(logicalName);
        CelesteFileCommitResult result = DurableStore.Commit(logicalName, payload);
        Console.WriteLine($"IOS_STORAGE write={logicalName}.celeste; bytes={payload.Length}; " +
                          $"atomic=Foundation; changed={result.Changed}; previous-good={result.PreviousGoodUpdated}; " +
                          $"cleanup={result.CleanupSucceeded}");
        return true;
    }

    public static byte[] ReadLogicalFile(string logicalName, bool previousGoodOnly = false)
    {
        ValidateLogicalName(logicalName);
        if (previousGoodOnly) return DurableStore.LoadPreviousGood(logicalName);
        CelesteFileLoadResult result = DurableStore.Load(logicalName);
        if (result.RecoveredFromPreviousGood)
            Console.WriteLine($"IOS_STORAGE recovery={logicalName}.celeste; source=previous-good; primary-repaired=true");
        return result.Data;
    }

    public static bool LogicalFileExists(string logicalName)
    {
        ValidateLogicalName(logicalName);
        return DurableStore.Exists(logicalName);
    }

    public static bool DeleteLogicalFile(string logicalName)
    {
        ValidateLogicalName(logicalName);
        return DurableStore.Delete(logicalName);
    }

    public static byte[] ReadBundleFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string contentRoot = Path.GetFullPath(Monocle.Engine.ContentDirectory)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(contentRoot, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Celeste Content reads must remain inside the app bundle.");
        return ReadFoundationFile(fullPath, "bundled Celeste Content");
    }

    public static Stream OpenBundleFile(string path) =>
        new MemoryStream(ReadBundleFile(path), writable: false);

    public static IEnumerable<string> ReadBundleLines(string path, Encoding encoding)
    {
        List<string> lines = new();
        using StreamReader reader = new(OpenBundleFile(path), encoding, true);
        string line;
        while ((line = reader.ReadLine()) != null) lines.Add(line);
        return lines;
    }

    public static string ReadSessionText(string path)
    {
        ValidateIncidentalPath(path);
        return Encoding.UTF8.GetString(ReadFoundationFile(path, "iOS diagnostic"));
    }

    public static void WriteSessionText(string path, string value)
    {
        ValidateIncidentalPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? throw new IOException("The iOS diagnostic path has no parent."));
        using NSData data = NSData.FromArray(Encoding.UTF8.GetBytes(value));
        using NSUrl url = NSUrl.FromFilename(path);
        if (!data.Save(url, atomically: true))
            throw new IOException("Foundation failed to write the iOS diagnostic.");
    }

    private sealed class CanonicalCelesteValidator : ICelesteLogicalFileValidator
    {
        public bool IsValid(string logicalName, ReadOnlyMemory<byte> data)
        {
            try
            {
                using MemoryStream stream = new(data.ToArray(), writable: false);
                if (logicalName == "settings")
                    return AppleSettingsSerializer.Deserialize(stream) != null;
                return AppleSaveDataSerializer.Deserialize(stream) != null;
            }
            catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or FormatException)
            {
                return false;
            }
        }
    }

    private sealed class FoundationDurabilityBackend : ICelesteFileDurabilityBackend
    {
        private readonly string root = RequiredEnvironmentRoot("CELESTE_IOS_STORAGE_ROOT");
        private readonly NSFileManager files = NSFileManager.DefaultManager;

        public byte[] Read(string logicalName, CelesteFileCopy copy)
        {
            string path = Resolve(logicalName, copy);
            NSFileAttributes attributes = SafeAttributes(path);
            if (attributes == null) return null;
            if (attributes.Type == NSFileType.SymbolicLink)
                throw new UnauthorizedAccessException("Celeste state symlinks are not permitted.");
            if (attributes.Size == 0 || attributes.Size > CelesteFileDurabilityStore.MaximumLogicalFileBytes)
                return Array.Empty<byte>();
            return ReadFoundationFile(path, "approved Celeste state");
        }

        public void WriteAtomic(string logicalName, CelesteFileCopy copy, ReadOnlyMemory<byte> payload)
        {
            string path = Resolve(logicalName, copy);
            string parent = Path.GetDirectoryName(path)
                ?? throw new IOException("The approved Celeste state path has no parent.");
            EnsureDirectory(parent);
            NSFileAttributes attributes = SafeAttributes(path);
            if (attributes?.Type == NSFileType.SymbolicLink)
                throw new UnauthorizedAccessException("Celeste state symlinks are not permitted.");
            using NSData data = NSData.FromArray(payload.ToArray());
            using NSUrl url = NSUrl.FromFilename(path);
            if (!data.Save(url, atomically: true))
                throw new IOException("Foundation failed to atomically replace approved Celeste state.");
        }

        public void Delete(string logicalName, CelesteFileCopy copy)
        {
            string path = Resolve(logicalName, copy);
            if (!files.FileExists(path)) return;
            if (!files.Remove(path, out NSError error))
                throw new IOException($"Foundation failed to delete approved Celeste state ({error?.Code ?? -1}).");
        }

        public bool CleanupOwnedTemporaryFiles(string logicalName)
        {
            ValidateLogicalName(logicalName);
            // NSData's atomic save owns its private temporary name and cleanup.
            // The product intentionally creates no project-visible temp files.
            return true;
        }

        private string Resolve(string logicalName, CelesteFileCopy copy)
        {
            ValidateLogicalName(logicalName);
            RejectSymlink(root, allowMissing: false);
            string directory = Path.Combine(root, copy == CelesteFileCopy.Primary ? "Saves" : "Backups");
            EnsureDirectory(directory);
            string path = Path.GetFullPath(Path.Combine(directory, logicalName + ".celeste"));
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Celeste state escaped Application Support.");
            return path;
        }

        private void EnsureDirectory(string path)
        {
            NSFileAttributes existing = SafeAttributes(path);
            if (existing != null)
            {
                if (existing.Type == NSFileType.SymbolicLink)
                    throw new UnauthorizedAccessException("Celeste storage directories cannot be symlinks.");
                if (existing.Type != NSFileType.Directory)
                    throw new IOException("Celeste storage path is not a directory.");
                return;
            }
            if (!files.CreateDirectory(path, true, (NSFileAttributes)null, out NSError error))
                throw new IOException($"Foundation failed to create Celeste storage ({error?.Code ?? -1}).");
            RejectSymlink(path, allowMissing: false);
        }

        private void RejectSymlink(string path, bool allowMissing)
        {
            NSFileAttributes attributes = SafeAttributes(path);
            if (attributes == null)
            {
                if (allowMissing) return;
                throw new IOException("Required Celeste storage directory is unavailable.");
            }
            if (attributes.Type == NSFileType.SymbolicLink)
                throw new UnauthorizedAccessException("Celeste storage symlinks are not permitted.");
        }

        private NSFileAttributes SafeAttributes(string path)
        {
            NSFileAttributes attributes = files.GetAttributes(path, out NSError error);
            if (attributes != null) return attributes;
            if (!files.FileExists(path)) return null;
            throw new IOException($"Foundation failed to inspect approved Celeste state ({error?.Code ?? -1}).");
        }
    }

    private static void ValidateIncidentalPath(string path)
    {
        string root = RequiredEnvironmentRoot("CELESTE_IOS_INCIDENTAL_ROOT");
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(prefix, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Diagnostic output escaped the temporary app container.");
    }

    private static string RequiredEnvironmentRoot(string name)
    {
        string value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Required iOS runtime root {name} is unavailable.");
        return Path.GetFullPath(value);
    }

    private static byte[] ReadFoundationFile(string path, string role)
    {
        using NSData data = NSData.FromFile(path)
            ?? throw new IOException($"Foundation failed to read {role}.");
        return data.ToArray();
    }

}
#endif
