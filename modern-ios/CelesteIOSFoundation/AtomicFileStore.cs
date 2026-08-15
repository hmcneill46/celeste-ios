namespace CelesteIOSFoundation;

public enum AtomicWriteFault { None, BeforeFlush, BeforeCommit }

public sealed class AtomicFileStore
{
    private static readonly HashSet<string> ApprovedNames = new(StringComparer.Ordinal)
    {
        "settings.celeste", "0.celeste", "1.celeste", "2.celeste"
    };

    public AtomicFileStore(string root)
    {
        Root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public static bool IsApprovedLogicalName(string name) => ApprovedNames.Contains(name);

    public void Write(string logicalName, ReadOnlySpan<byte> data, AtomicWriteFault fault = AtomicWriteFault.None)
    {
        if (!IsApprovedLogicalName(logicalName)) throw new ArgumentException("Unapproved Celeste logical filename.", nameof(logicalName));
        Directory.CreateDirectory(Root);
        string destination = Path.Combine(Root, logicalName);
        string temporary = Path.Combine(Root, $".{logicalName}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(data);
                if (fault == AtomicWriteFault.BeforeFlush) throw new IOException("Injected pre-flush failure.");
                stream.Flush(flushToDisk: true);
            }
            if (fault == AtomicWriteFault.BeforeCommit) throw new IOException("Injected pre-commit failure.");
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
