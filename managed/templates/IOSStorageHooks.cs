#if IOS_CELESTE_RUNTIME_HOST
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

    public static void ValidateLogicalName(string logicalName)
    {
        if (!LogicalNames.Contains(logicalName))
            throw new InvalidOperationException($"iOS rejects unsupported Celeste file '{logicalName}'.");
    }

    public static void BeginBatch() { }
    public static void FileChanged(string logicalName, string operation) => ValidateLogicalName(logicalName);
    public static bool EndBatch(bool successful, string reason) => successful;
    public static bool Flush(string reason) => true;

    public static void WriteApprovedFile(string path, byte[] payload)
    {
        ValidateApprovedStatePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? throw new IOException("The approved Celeste state path has no parent."));
        using NSData data = NSData.FromArray(payload);
        using NSUrl url = NSUrl.FromFilename(path);
        if (!data.Save(url, atomically: true))
            throw new IOException("Foundation failed to atomically write approved Celeste state.");
        Console.WriteLine($"IOS_STORAGE write={Path.GetFileName(path)}; bytes={payload.Length}; atomic=Foundation");
    }

    public static byte[] ReadApprovedFile(string path)
    {
        ValidateApprovedStatePath(path);
        return ReadFoundationFile(path, "approved Celeste state");
    }

    public static Stream OpenApprovedFile(string path) =>
        new MemoryStream(ReadApprovedFile(path), writable: false);

    public static void CopyApprovedFile(string source, string destination) =>
        WriteApprovedFile(destination, ReadApprovedFile(source));

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

    private static void ValidateApprovedStatePath(string path)
    {
        string root = RequiredEnvironmentRoot("CELESTE_IOS_STORAGE_ROOT");
        string full = Path.GetFullPath(path);
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Celeste state escaped Application Support.");

        string relative = Path.GetRelativePath(root, full).Replace('\\', '/');
        string[] parts = relative.Split('/');
        if (parts.Length != 2 || parts[0] is not ("Saves" or "Backups") ||
            !parts[1].EndsWith(".celeste", StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Celeste state path is not approved.");
        ValidateLogicalName(parts[1][..^".celeste".Length]);
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
