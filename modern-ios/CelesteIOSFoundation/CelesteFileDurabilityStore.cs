namespace CelesteIOSFoundation;

/// <summary>
/// The two bounded physical copies owned for each ordinary Celeste logical
/// file. The platform adapter is solely responsible for mapping these values
/// to app-private storage; callers can never supply a filesystem path.
/// </summary>
public enum CelesteFileCopy
{
    Primary,
    PreviousGood,
}

public interface ICelesteFileDurabilityBackend
{
    byte[]? Read(string logicalName, CelesteFileCopy copy);
    void WriteAtomic(string logicalName, CelesteFileCopy copy, ReadOnlyMemory<byte> data);
    void Delete(string logicalName, CelesteFileCopy copy);
    bool CleanupOwnedTemporaryFiles(string logicalName);
}

public interface ICelesteLogicalFileValidator
{
    bool IsValid(string logicalName, ReadOnlyMemory<byte> data);
}

public readonly record struct CelesteFileLoadResult(
    byte[]? Data,
    bool RecoveredFromPreviousGood,
    bool PrimaryWasInvalid,
    bool PreviousGoodWasInvalid);

public readonly record struct CelesteFileCommitResult(
    bool Changed,
    bool PreviousGoodUpdated,
    bool CleanupSucceeded);

public readonly record struct CelesteFileRestoreResult(
    bool Restored,
    bool Reversible,
    bool CleanupSucceeded);

/// <summary>
/// Shared high-level durability policy for ordinary Celeste XML files.
/// Atomic replacement and path ownership remain responsibilities of the
/// platform backend. This policy keeps one primary and at most one previous
/// good copy, validates every candidate before mutation, and serializes all
/// reads/writes through a single process-local authority.
/// </summary>
public sealed class CelesteFileDurabilityStore
{
    public const int MaximumLogicalFileBytes = 64 * 1024 * 1024;

    private static readonly HashSet<string> ApprovedNames = new(StringComparer.Ordinal)
    {
        "settings", "0", "1", "2"
    };

    private readonly object gate = new();
    private readonly ICelesteFileDurabilityBackend backend;
    private readonly ICelesteLogicalFileValidator validator;

    public CelesteFileDurabilityStore(
        ICelesteFileDurabilityBackend backend,
        ICelesteLogicalFileValidator validator)
    {
        this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
        this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public static bool IsApprovedLogicalName(string logicalName) =>
        logicalName is not null && ApprovedNames.Contains(logicalName);

    public CelesteFileCommitResult Commit(string logicalName, ReadOnlyMemory<byte> candidate)
    {
        ValidateLogicalName(logicalName);
        ValidateSize(candidate.Length);
        if (!validator.IsValid(logicalName, candidate))
            throw new InvalidDataException("Celeste rejected invalid serialized state before storage mutation.");

        lock (gate)
        {
            bool cleanupSucceeded = TryCleanup(logicalName);
            byte[]? primary = ReadForMutation(logicalName, CelesteFileCopy.Primary);
            bool primaryValid = IsValid(logicalName, primary);
            byte[]? previousGood = ReadForMutation(logicalName, CelesteFileCopy.PreviousGood);
            bool previousGoodValid = IsValid(logicalName, previousGood);

            if (primaryValid && primary!.AsSpan().SequenceEqual(candidate.Span))
                return new CelesteFileCommitResult(false, false, cleanupSucceeded && TryCleanup(logicalName));

            bool previousGoodUpdated = false;
            if (primaryValid)
            {
                WriteAndVerify(logicalName, CelesteFileCopy.PreviousGood, primary!);
                previousGoodUpdated = true;
            }
            else if (!previousGoodValid)
            {
                backend.Delete(logicalName, CelesteFileCopy.PreviousGood);
            }

            try
            {
                backend.WriteAtomic(logicalName, CelesteFileCopy.Primary, candidate);
            }
            catch (Exception exception) when (IsStorageFailure(exception))
            {
                // Some native atomic writers can report a post-commit cleanup
                // error after the replacement is already durable. Accept the
                // write only when an independent read proves the exact valid
                // candidate; otherwise retain the guard by propagating failure.
                byte[]? uncertain = TryRead(logicalName, CelesteFileCopy.Primary);
                if (!IsValid(logicalName, uncertain) ||
                    !uncertain!.AsSpan().SequenceEqual(candidate.Span))
                    throw;
                cleanupSucceeded = false;
            }

            byte[] committed = RequiredValidRead(logicalName, CelesteFileCopy.Primary);
            if (!committed.AsSpan().SequenceEqual(candidate.Span))
                throw new IOException("Atomic Celeste replacement did not preserve the exact candidate bytes.");

            cleanupSucceeded &= TryCleanup(logicalName);
            return new CelesteFileCommitResult(true, previousGoodUpdated, cleanupSucceeded);
        }
    }

    public CelesteFileLoadResult Load(string logicalName)
    {
        ValidateLogicalName(logicalName);
        lock (gate)
        {
            _ = TryCleanup(logicalName);
            byte[]? primary = TryRead(logicalName, CelesteFileCopy.Primary);
            bool primaryValid = IsValid(logicalName, primary);
            if (primaryValid)
                return new CelesteFileLoadResult(primary, false, false, false);

            byte[]? previousGood = TryRead(logicalName, CelesteFileCopy.PreviousGood);
            bool previousGoodValid = IsValid(logicalName, previousGood);
            if (!previousGoodValid)
                return new CelesteFileLoadResult(null, false, primary is not null, previousGood is not null);

            WriteAndVerify(logicalName, CelesteFileCopy.Primary, previousGood!);
            _ = TryCleanup(logicalName);
            return new CelesteFileLoadResult(previousGood, true, primary is not null, false);
        }
    }

    public bool Exists(string logicalName) => Load(logicalName).Data is not null;

    public byte[]? LoadPreviousGood(string logicalName)
    {
        ValidateLogicalName(logicalName);
        lock (gate)
        {
            byte[]? previousGood = TryRead(logicalName, CelesteFileCopy.PreviousGood);
            return IsValid(logicalName, previousGood) ? previousGood : null;
        }
    }

    /// <summary>
    /// Atomically installs the validated previous-good bytes and verifies the
    /// restored primary before attempting to rotate a valid former primary
    /// into the undo position. The recovery point is therefore never destroyed
    /// before the restore itself is known durable. When the final rotation also
    /// succeeds, repeating Restore Previous naturally performs an undo.
    /// </summary>
    public CelesteFileRestoreResult RestorePreviousGood(string logicalName)
    {
        ValidateLogicalName(logicalName);
        lock (gate)
        {
            byte[]? previousGood = ReadForMutation(logicalName, CelesteFileCopy.PreviousGood);
            if (!IsValid(logicalName, previousGood))
                return new CelesteFileRestoreResult(false, false, TryCleanup(logicalName));

            byte[]? primary = ReadForMutation(logicalName, CelesteFileCopy.Primary);
            if (IsValid(logicalName, primary) && primary!.AsSpan().SequenceEqual(previousGood))
                return new CelesteFileRestoreResult(false, false, TryCleanup(logicalName));

            bool cleanupSucceeded = TryCleanup(logicalName);
            try
            {
                backend.WriteAtomic(logicalName, CelesteFileCopy.Primary, previousGood!);
            }
            catch (Exception exception) when (IsStorageFailure(exception))
            {
                byte[]? uncertain = TryRead(logicalName, CelesteFileCopy.Primary);
                if (!IsValid(logicalName, uncertain) ||
                    !uncertain!.AsSpan().SequenceEqual(previousGood))
                    throw;
                cleanupSucceeded = false;
            }

            byte[] restored = RequiredValidRead(logicalName, CelesteFileCopy.Primary);
            if (!restored.AsSpan().SequenceEqual(previousGood))
                throw new IOException("Restoring Celeste state did not preserve the exact previous-good bytes.");

            // Rotate the formerly current file only after the restore itself is
            // proven durable. Failure here cannot invalidate the restored
            // primary; it merely makes the operation non-reversible.
            bool reversible = IsValid(logicalName, primary);
            if (reversible)
            {
                try
                {
                    WriteAndVerify(logicalName, CelesteFileCopy.PreviousGood, primary!);
                }
                catch (Exception exception) when (IsStorageFailure(exception))
                {
                    reversible = false;
                    cleanupSucceeded = false;
                }
            }

            cleanupSucceeded &= TryCleanup(logicalName);
            return new CelesteFileRestoreResult(true, reversible, cleanupSucceeded);
        }
    }

    public bool Delete(string logicalName)
    {
        ValidateLogicalName(logicalName);
        lock (gate)
        {
            bool existed = ReadForMutation(logicalName, CelesteFileCopy.Primary) is not null ||
                           ReadForMutation(logicalName, CelesteFileCopy.PreviousGood) is not null;
            backend.Delete(logicalName, CelesteFileCopy.Primary);
            backend.Delete(logicalName, CelesteFileCopy.PreviousGood);
            _ = TryCleanup(logicalName);
            return existed;
        }
    }

    private void WriteAndVerify(string logicalName, CelesteFileCopy copy, ReadOnlyMemory<byte> data)
    {
        backend.WriteAtomic(logicalName, copy, data);
        byte[] committed = RequiredValidRead(logicalName, copy);
        if (!committed.AsSpan().SequenceEqual(data.Span))
            throw new IOException("Atomic Celeste copy verification did not preserve exact bytes.");
    }

    private byte[] RequiredValidRead(string logicalName, CelesteFileCopy copy)
    {
        byte[]? data = TryRead(logicalName, copy);
        if (!IsValid(logicalName, data))
            throw new IOException("Atomic Celeste copy did not validate after replacement.");
        return data!;
    }

    private byte[]? TryRead(string logicalName, CelesteFileCopy copy)
    {
        try
        {
            return backend.Read(logicalName, copy);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return null;
        }
    }

    private byte[]? ReadForMutation(string logicalName, CelesteFileCopy copy)
    {
        // A commit must distinguish a genuinely absent/invalid file from an
        // unreadable one. I/O failure aborts before any backup or primary is
        // mutated; only load is allowed to continue on to the other copy.
        return backend.Read(logicalName, copy);
    }

    private bool IsValid(string logicalName, byte[]? data) =>
        data is not null && data.Length <= MaximumLogicalFileBytes && validator.IsValid(logicalName, data);

    private bool TryCleanup(string logicalName)
    {
        try
        {
            return backend.CleanupOwnedTemporaryFiles(logicalName);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return false;
        }
    }

    private static bool IsStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

    private static void ValidateLogicalName(string logicalName)
    {
        if (!IsApprovedLogicalName(logicalName))
            throw new ArgumentException("Unapproved Celeste logical filename.", nameof(logicalName));
    }

    private static void ValidateSize(int length)
    {
        if (length < 1 || length > MaximumLogicalFileBytes)
            throw new InvalidDataException("Celeste logical file size is outside the bounded iOS sanity range.");
    }
}
