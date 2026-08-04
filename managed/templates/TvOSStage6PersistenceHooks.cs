#if TVOS_STAGE6
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Foundation;

namespace Celeste;

public static class TvOSStage6PersistenceHooks
{
    private static readonly object Gate = new();
    private static int batchDepth;
    private static bool dirty;
    private static string lastReason = "none";

    public static Func<string, bool> CommitRequested { get; set; }

    public static void ValidateLogicalName(string logicalName)
    {
        if (logicalName is not ("settings" or "0" or "1" or "2"))
            throw new InvalidOperationException($"Stage 6 rejects non-durable Celeste file '{logicalName}'.");
    }

    public static void BeginBatch()
    {
        lock (Gate) batchDepth++;
    }

    public static void FileChanged(string logicalName, string operation)
    {
        ValidateLogicalName(logicalName);
        Func<string, bool> callback = null;
        string reason = $"{operation}:{logicalName}";
        lock (Gate)
        {
            dirty = true;
            lastReason = reason;
            if (batchDepth == 0)
            {
                dirty = false;
                callback = CommitRequested;
            }
        }
        if (callback != null && !callback(reason))
            throw new IOException("The Stage 6 durable commit failed.");
    }

    public static bool EndBatch(bool successful, string reason)
    {
        Func<string, bool> callback = null;
        string callbackReason = reason;
        lock (Gate)
        {
            if (batchDepth <= 0) throw new InvalidOperationException("Stage 6 persistence batch underflow.");
            batchDepth--;
            if (!successful) dirty = false;
            if (successful && dirty && batchDepth == 0)
            {
                dirty = false;
                callback = CommitRequested;
                callbackReason = $"{reason}:{lastReason}";
            }
        }
        return callback == null || callback(callbackReason);
    }

    public static bool Flush(string reason) => CommitRequested?.Invoke($"flush:{reason}") ?? true;

    public static void WriteApprovedFile(string path, byte[] payload)
    {
        using NSData data = NSData.FromArray(payload);
        using NSUrl url = NSUrl.FromFilename(path);
        if (!data.Save(url, atomically: true))
            throw new IOException("Foundation failed to write approved Celeste state.");
    }

    public static byte[] ReadApprovedFile(string path)
    {
        using NSData data = NSData.FromFile(path)
            ?? throw new IOException("Foundation failed to read approved Celeste state.");
        return data.ToArray();
    }

    public static Stream OpenApprovedFile(string path) => new MemoryStream(ReadApprovedFile(path), writable: false);

    public static void CopyApprovedFile(string source, string destination) =>
        WriteApprovedFile(destination, ReadApprovedFile(source));

    public static byte[] ReadBundleFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string contentRoot = Path.GetFullPath(Monocle.Engine.ContentDirectory)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(contentRoot, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Celeste content reads must remain inside the bundled Content directory.");
        return ReadFoundationFile(fullPath, "bundled Celeste content");
    }

    public static Stream OpenBundleFile(string path) =>
        new MemoryStream(ReadBundleFile(path), writable: false);

    public static IEnumerable<string> ReadBundleLines(string path, Encoding encoding)
    {
        List<string> lines = new();
        using StreamReader reader = new(OpenBundleFile(path), encoding, detectEncodingFromByteOrderMarks: true);
        string line;
        while ((line = reader.ReadLine()) != null) lines.Add(line);
        return lines;
    }

    public static string ReadSessionText(string path)
    {
        ValidateSessionIncidentalPath(path);
        return Encoding.UTF8.GetString(ReadFoundationFile(path, "session diagnostic"));
    }

    public static void WriteSessionText(string path, string value)
    {
        ValidateSessionIncidentalPath(path);
        WriteFoundationFile(path, Encoding.UTF8.GetBytes(value), "session diagnostic");
    }

    private static byte[] ReadFoundationFile(string path, string role)
    {
        using NSData data = NSData.FromFile(path)
            ?? throw new IOException($"Foundation failed to read {role}.");
        return data.ToArray();
    }

    private static void WriteFoundationFile(string path, byte[] payload, string role)
    {
        using NSData data = NSData.FromArray(payload);
        using NSUrl url = NSUrl.FromFilename(path);
        if (!data.Save(url, atomically: true))
            throw new IOException($"Foundation failed to write {role}.");
    }

    private static void ValidateSessionIncidentalPath(string path)
    {
        string root = Environment.GetEnvironmentVariable("CELESTE_TVOS_SESSION_ROOT");
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("The tvOS session root is unavailable.");
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(prefix, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Incidental output must remain inside the temporary session root.");
    }

    public static void Reset()
    {
        lock (Gate)
        {
            CommitRequested = null;
            batchDepth = 0;
            dirty = false;
            lastReason = "none";
        }
    }
}
#endif
