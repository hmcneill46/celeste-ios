#if CELESTE_RUNTIME && TVOS_STAGE6_HOST
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Celeste;
using Foundation;

namespace CelesteTvOSHost;

internal sealed class Stage6PersistenceStore : IDisposable
{
    internal const ushort FormatVersion = 2;
    internal const ushort PreviousFormatVersion = 1;
    internal const ushort LegacyFormatVersion = 0;
    internal const ushort SettingsSchemaVersion = 1;
    internal const ushort SaveDataSchemaVersion = 1;
    internal const int MaximumSettingsBytes = 64 * 1024;
    internal const int MaximumSaveDataBytes = 256 * 1024;
    internal const int HardCompressedEntryBudgetBytes = 96 * 1024;
    internal const int HardTotalBudgetBytes = 256 * 1024;
    internal const int HardEnvelopeBudgetBytes = 124 * 1024;
    internal const byte CompressionNone = 0;
    internal const byte CompressionZlibLevel9 = 1;

    private const uint CompleteMarker = 0x434F4D50;
    // The magic and UserDefaults keys intentionally retain their v1 names so
    // an existing production install can decode and migrate its two slots.
    private const string Magic = "CTVOSPV1";
    private const string CelesteIdentity = "fd73f8a2311fa5737ded550cbad4b75c85b7686b36432f59185e940fcb65fcfe";
    private static readonly byte[] CelesteIdentityBytes = Convert.FromHexString(CelesteIdentity);
    private static readonly byte[] ZeroHash = new byte[32];
    private static readonly FilePolicy[] Files =
    {
        new("settings", "Saves/settings.celeste", "Backups/settings.celeste", SerializerKind.Settings, MaximumSettingsBytes),
        new("0", "Saves/0.celeste", "Backups/0.celeste", SerializerKind.SaveData, MaximumSaveDataBytes),
        new("1", "Saves/1.celeste", "Backups/1.celeste", SerializerKind.SaveData, MaximumSaveDataBytes),
        new("2", "Saves/2.celeste", "Backups/2.celeste", SerializerKind.SaveData, MaximumSaveDataBytes)
    };

    private readonly object gate = new();
    private readonly NSUserDefaults defaults;
    private readonly string root;
    private readonly string keyPrefix;
    private readonly NSObject sizeObserver;
    private Snapshot? selected;
    private bool disposed;
    private bool sizeWarningObserved;
    // A browser mutation changes durable state without changing the already
    // running Celeste object graph. Once that happens, lifecycle/UserIO flushes
    // must never recapture the stale materialized files over the import. The
    // next process restores the newly selected durable generation normally.
    private bool externalMutationRequiresRestart;
    internal bool SimulateMaterializationFailure { get; set; }
    internal string LastFailureCategory { get; private set; } = "none";

    internal Stage6PersistenceStore(string sessionRoot, string storageNamespace)
    {
        root = Path.GetFullPath(sessionRoot);
        keyPrefix = PrefixForNamespace(storageNamespace);
        defaults = NSUserDefaults.StandardUserDefaults;
        sizeObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            NSUserDefaults.SizeLimitExceededNotification,
            _ =>
            {
                lock (gate) sizeWarningObserved = true;
                Stage3BLog.Error("STAGE6_DEFAULTS_SIZE_LIMIT_WARNING category=userdefaults-size-warning; observed=true; acceptance=failure");
            });
        Directory.CreateDirectory(root);
    }

    internal ulong Generation { get { lock (gate) return selected?.Generation ?? 0; } }
    internal ushort SelectedFormatVersion { get { lock (gate) return selected?.SourceFormatVersion ?? FormatVersion; } }
    internal string LogicalHash { get { lock (gate) return selected?.LogicalHash ?? "none"; } }
    internal bool SizeWarningObserved { get { lock (gate) return sizeWarningObserved; } }
    internal bool ExternalMutationRequiresRestart { get { lock (gate) return externalMutationRequiresRestart; } }
    internal string NamespaceCategory => keyPrefix.Contains(".Tests.", StringComparison.Ordinal) ? "tests" :
        keyPrefix.Contains(".Acceptance.", StringComparison.Ordinal) ? "acceptance" :
        keyPrefix.Contains(".Restart.", StringComparison.Ordinal) ? "restart" : "production";

    internal RestoreResult Restore()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            Candidate a = ReadCandidate("A");
            Candidate b = ReadCandidate("B");
            selected = Select(a, b);
            externalMutationRequiresRestart = false;
            ClearMaterializedFiles();
            if (selected == null)
            {
                Stage3BLog.Warning($"STAGE6_RESTORE namespace={NamespaceCategory}; selected=none; A={a.Status}; B={b.Status}; action=new-game-no-write");
                return new RestoreResult(0, "none", a.Status, b.Status, false, FormatVersion);
            }
            try
            {
                Materialize(selected);
            }
            catch (Exception exception) when (exception is not PersistenceFailureException)
            {
                throw Failure("materialization", "Failed to materialize the selected durable generation.", exception);
            }
            Stage3BLog.Info(
                $"STAGE6_RESTORE namespace={NamespaceCategory}; generation={selected.Generation}; format=v{selected.SourceFormatVersion}; " +
                $"logical={selected.LogicalHash}; A={a.Status}; B={b.Status}; migrated-on-launch=false"
            );
            return new RestoreResult(selected.Generation, selected.LogicalHash, a.Status, b.Status, true, selected.SourceFormatVersion);
        }
    }

    internal bool Commit(string reason)
    {
        lock (gate)
        {
            try
            {
                ThrowIfDisposed();
                LastFailureCategory = "none";
                if (sizeWarningObserved)
                    throw Failure("userdefaults-size-warning", "A UserDefaults size-limit warning was observed.");
                if (externalMutationRequiresRestart)
                {
                    Stage3BLog.Info(
                        $"STAGE10B_STALE_COMMIT_SUPPRESSED reason={SanitizeReason(reason)}; generation={selected?.Generation ?? 0}; " +
                        "restart-required=true; materialized-runtime-capture=false"
                    );
                    return true;
                }

                Snapshot candidate = Capture((selected?.Generation ?? HighestStoredGeneration()) + 1);
                if (selected == null && candidate.Entries.All(entry => !entry.Present))
                {
                    Stage3BLog.Info($"STAGE6_COMMIT reason={SanitizeReason(reason)}; result=unchanged-new-game; generation=0; logical=none");
                    return true;
                }
                if (selected != null && candidate.LogicalHash == selected.LogicalHash)
                {
                    Stage3BLog.Info(
                        $"STAGE6_COMMIT reason={SanitizeReason(reason)}; result=unchanged; generation={selected.Generation}; " +
                        $"format=v{selected.SourceFormatVersion}; logical={selected.LogicalHash}"
                    );
                    return true;
                }

                byte[] encoded = Encode(candidate, FormatVersion);
                ValidateEnvelopeLength(encoded.Length);

                Candidate a = ReadCandidate("A");
                Candidate b = ReadCandidate("B");
                string target = ChooseTarget(a, b);
                int otherBytes = target == "A" ? b.RawSize : a.RawSize;
                ValidateBridgeLength(encoded.Length, otherBytes);

                using (NSString key = new(Key(target)))
                using (NSData data = NSData.FromArray(encoded))
                    defaults.SetValueForKey(data, key);
                defaults.Synchronize();

                Candidate readBack = ReadCandidate(target);
                if (readBack.Snapshot == null || readBack.Snapshot.SourceFormatVersion != FormatVersion ||
                    readBack.Snapshot.Generation != candidate.Generation || readBack.Snapshot.LogicalHash != candidate.LogicalHash)
                {
                    throw Failure("userdefaults-readback", "UserDefaults read-back verification failed.");
                }

                selected = readBack.Snapshot;
                DiagnosticDecoded decoded = Describe(selected, encoded.Length);
                Stage3BLog.Info(
                    $"STAGE6_COMMIT reason={SanitizeReason(reason)}; result=committed; slot={target}; generation={selected.Generation}; " +
                    $"format=v{selected.SourceFormatVersion}; logical={selected.LogicalHash}; raw-logical-bytes={decoded.UncompressedPayloadBytes}; " +
                    $"compressed-payload-bytes={decoded.StoredPayloadBytes}; slot-bytes={encoded.Length}; bridge-bytes={BridgeBytes()}; " +
                    $"app-domain-keys={defaults.ToDictionary().Count}"
                );
                return true;
            }
            catch (Exception exception)
            {
                LastFailureCategory = Category(exception);
                Stage3BLog.Error(
                    $"STAGE6_COMMIT reason={SanitizeReason(reason)}; result=failed; category={LastFailureCategory}; " +
                    $"type={exception.GetType().Name}; message={exception.Message}; prior-generation={selected?.Generation ?? 0}"
                );
                return false;
            }
        }
    }

    internal bool Flush(string reason) => Commit($"lifecycle-{reason}");

    private Snapshot Capture(ulong generation)
    {
        List<Entry> entries = new(Files.Length);
        foreach (FilePolicy policy in Files)
        {
            string path = SafePath(policy.PrimaryPath);
            if (!File.Exists(path))
            {
                entries.Add(new Entry(policy.LogicalName, false, policy.Serializer, Array.Empty<byte>()));
                continue;
            }
            FileInfo info = new(path);
            if (info.LinkTarget != null)
                throw Failure("materialization", $"Symlinks are not allowed for {policy.LogicalName}.");
            if (info.Length > policy.MaximumBytes)
                throw Failure("raw-uncompressed-limit", $"{policy.LogicalName} is {info.Length} bytes; maximum is {policy.MaximumBytes}.");
            using NSData? data = NSData.FromFile(path);
            if (data == null)
                throw Failure("materialization", $"Foundation failed to read approved state file {policy.LogicalName}.");
            byte[] payload = data.ToArray();
            ValidatePayload(policy, payload);
            entries.Add(new Entry(policy.LogicalName, true, policy.Serializer, payload));
            Stage3BLog.Info(
                $"STAGE9B_CAPTURE file={policy.LogicalName}; raw-bytes={payload.Length}; raw-maximum={policy.MaximumBytes}; " +
                $"sha256={Hash(payload)}"
            );
        }
        return Snapshot.Create(generation, entries, FormatVersion);
    }

    private void Materialize(Snapshot snapshot)
    {
        if (SimulateMaterializationFailure && NamespaceCategory != "production")
            throw Failure("materialization", "Simulated isolated materialization failure.");
        foreach (Entry entry in snapshot.Entries)
        {
            FilePolicy policy = Policy(entry.Name);
            string primary = SafePath(policy.PrimaryPath);
            string backup = SafePath(policy.BackupPath);
            if (!entry.Present)
            {
                DeleteIfPresent(primary);
                DeleteIfPresent(backup);
                continue;
            }
            ValidatePayload(policy, entry.Payload);
            Directory.CreateDirectory(Path.GetDirectoryName(primary)!);
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            WriteMaterializedPayload(primary, entry.Payload);
            WriteMaterializedPayload(backup, entry.Payload);
        }
    }

    private void ClearMaterializedFiles()
    {
        foreach (FilePolicy policy in Files)
        {
            DeleteIfPresent(SafePath(policy.PrimaryPath));
            DeleteIfPresent(SafePath(policy.BackupPath));
        }
    }

    private static void ValidatePayload(FilePolicy policy, byte[] payload)
    {
        if (payload.Length > policy.MaximumBytes)
            throw Failure("raw-uncompressed-limit", $"{policy.LogicalName} is {payload.Length} bytes; maximum is {policy.MaximumBytes}.");
        try
        {
            using MemoryStream stream = new(payload, writable: false);
            if (policy.Serializer == SerializerKind.Settings)
                _ = TvOSSettingsSerializer.Deserialize(stream);
            else
                _ = TvOSSaveDataSerializer.Deserialize(stream);
            if (stream.Position != stream.Length)
                throw new InvalidDataException("Serializer did not consume the complete payload.");
        }
        catch (Exception exception) when (exception is not PersistenceFailureException)
        {
            throw Failure("serializer-invalid", $"{policy.LogicalName} failed strict serializer validation.", exception);
        }
    }

    private Candidate ReadCandidate(string slot)
    {
        NSData? data = defaults.DataForKey(Key(slot));
        if (data == null) return new Candidate(slot, "missing", 0, null);
        byte[] bytes = data.ToArray();
        try
        {
            Snapshot decoded = Decode(bytes);
            return new Candidate(slot, $"valid-v{decoded.SourceFormatVersion}", bytes.Length, decoded);
        }
        catch (UnsupportedFormatException exception)
        {
            return new Candidate(slot, $"unsupported-v{exception.Version}", bytes.Length, null);
        }
        catch (PersistenceFailureException exception)
        {
            return new Candidate(slot, $"invalid-{exception.Category}", bytes.Length, null);
        }
        catch (Exception exception)
        {
            return new Candidate(slot, $"invalid-{exception.GetType().Name}", bytes.Length, null);
        }
    }

    private static Snapshot? Select(Candidate a, Candidate b)
    {
        if (a.Snapshot == null) return b.Snapshot;
        if (b.Snapshot == null) return a.Snapshot;
        if (a.Snapshot.Generation == b.Snapshot.Generation)
            return string.CompareOrdinal(a.Snapshot.LogicalHash, b.Snapshot.LogicalHash) >= 0 ? a.Snapshot : b.Snapshot;
        return a.Snapshot.Generation > b.Snapshot.Generation ? a.Snapshot : b.Snapshot;
    }

    private static string ChooseTarget(Candidate a, Candidate b)
    {
        bool aFuture = a.Status.StartsWith("unsupported-v", StringComparison.Ordinal);
        bool bFuture = b.Status.StartsWith("unsupported-v", StringComparison.Ordinal);
        if ((aFuture && b.Snapshot != null) || (bFuture && a.Snapshot != null) || (aFuture && bFuture))
        {
            throw Failure(
                "unsupported-format",
                "No slot can be replaced without destroying either a supported recovery generation or an unsupported future-format generation."
            );
        }
        if (aFuture) return "B";
        if (bFuture) return "A";
        if (a.Snapshot == null) return "A";
        if (b.Snapshot == null) return "B";
        return a.Snapshot.Generation <= b.Snapshot.Generation ? "A" : "B";
    }

    private ulong HighestStoredGeneration()
    {
        Candidate a = ReadCandidate("A");
        Candidate b = ReadCandidate("B");
        return Math.Max(a.Snapshot?.Generation ?? 0, b.Snapshot?.Generation ?? 0);
    }

    internal int BridgeBytes()
    {
        NSData? a = defaults.DataForKey(Key("A"));
        NSData? b = defaults.DataForKey(Key("B"));
        return checked((int)((a?.Length ?? 0) + (b?.Length ?? 0)));
    }

    internal byte[]? ExportLogicalPayloadForFutureSaveManager(string logicalName)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            _ = Policy(logicalName);
            Entry? entry = selected?.Entries.Single(value => value.Name == logicalName);
            return entry is { Present: true } ? entry.Payload.ToArray() : null;
        }
    }

    internal Stage10AExportSnapshot CreateReadOnlySaveManagerSnapshot()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (!Commit("save-manager-read-snapshot"))
                throw Failure("save-manager-flush", "The durable generation could not be verified before export.");

            Dictionary<string, byte[]?> files = new(StringComparer.Ordinal);
            foreach (string logicalName in new[] { "settings", "0", "1", "2" })
                files.Add(logicalName, ExportLogicalPayloadForFutureSaveManager(logicalName));

            return new Stage10AExportSnapshot(
                selected?.Generation ?? 0,
                selected?.LogicalHash ?? "none",
                files
            );
        }
    }

    internal Stage10BMutationResult MutateFromSaveManager(Stage10BMutationCommand command)
    {
        lock (gate)
        {
            string operation = command.Payload == null ? "delete" : "replace";
            int rawBytes = command.Payload?.Length ?? 0;
            try
            {
                ThrowIfDisposed();
                LastFailureCategory = "none";
                if (sizeWarningObserved)
                    throw Failure("userdefaults-size-warning", "A UserDefaults size-limit warning was observed.");

                FilePolicy policy = Policy(command.LogicalName);
                ulong currentGeneration = selected?.Generation ?? 0;
                string currentHash = selected?.LogicalHash ?? "none";
                if (command.ExpectedGeneration != currentGeneration ||
                    !CryptographicOperations.FixedTimeEquals(
                        Encoding.ASCII.GetBytes(command.ExpectedLogicalHash),
                        Encoding.ASCII.GetBytes(currentHash)))
                {
                    Stage3BLog.Warning(
                        $"STAGE10B_MUTATION operation={operation}; target={command.LogicalName}; result=conflict; " +
                        $"expected-generation={command.ExpectedGeneration}; current-generation={currentGeneration}; raw-bytes={rawBytes}"
                    );
                    return Stage10BMutationResult.ConflictResult(CreateExportSnapshotUnsafe());
                }

                byte[]? acceptedPayload = command.Payload?.ToArray();
                if (acceptedPayload != null) ValidatePayload(policy, acceptedPayload);

                List<Entry> entries = Files.Select(file =>
                {
                    Entry? existing = selected?.Entries.Single(value => value.Name == file.LogicalName);
                    return existing == null
                        ? new Entry(file.LogicalName, false, file.Serializer, Array.Empty<byte>())
                        : new Entry(existing.Name, existing.Present, existing.Serializer, existing.Payload.ToArray());
                }).ToList();
                int index = entries.FindIndex(entry => entry.Name == command.LogicalName);
                Entry prior = entries[index];
                bool changed = acceptedPayload == null
                    ? prior.Present
                    : !prior.Present || !CryptographicOperations.FixedTimeEquals(prior.Payload, acceptedPayload);
                if (!changed)
                {
                    Stage3BLog.Info(
                        $"STAGE10B_MUTATION operation={operation}; target={command.LogicalName}; result=unchanged; " +
                        $"generation={currentGeneration}; raw-bytes={rawBytes}; restart-required={externalMutationRequiresRestart.ToString().ToLowerInvariant()}"
                    );
                    return Stage10BMutationResult.Unchanged(CreateExportSnapshotUnsafe(), externalMutationRequiresRestart);
                }

                entries[index] = acceptedPayload == null
                    ? new Entry(command.LogicalName, false, policy.Serializer, Array.Empty<byte>())
                    : new Entry(command.LogicalName, true, policy.Serializer, acceptedPayload);
                Snapshot candidate = Snapshot.Create(
                    checked(Math.Max(currentGeneration, HighestStoredGeneration()) + 1),
                    entries,
                    FormatVersion
                );
                byte[] encoded = Encode(candidate, FormatVersion);
                ValidateEnvelopeLength(encoded.Length);

                Candidate a = ReadCandidate("A");
                Candidate b = ReadCandidate("B");
                string target = ChooseTarget(a, b);
                int otherBytes = target == "A" ? b.RawSize : a.RawSize;
                ValidateBridgeLength(encoded.Length, otherBytes);

                using (NSString key = new(Key(target)))
                using (NSData data = NSData.FromArray(encoded))
                    defaults.SetValueForKey(data, key);
                defaults.Synchronize();

                Candidate readBack = ReadCandidate(target);
                if (readBack.Snapshot == null || readBack.Snapshot.SourceFormatVersion != FormatVersion ||
                    readBack.Snapshot.Generation != candidate.Generation ||
                    readBack.Snapshot.LogicalHash != candidate.LogicalHash)
                {
                    throw Failure("userdefaults-readback", "The imported generation did not pass durable read-back verification.");
                }

                // Do not materialize into the active process. Its in-memory
                // Settings/SaveData are stale and may only be reconciled by a
                // clean launch. Commit() is suppressed from this point onward.
                selected = readBack.Snapshot;
                externalMutationRequiresRestart = true;
                Stage10AExportSnapshot exported = CreateExportSnapshotUnsafe();
                Stage3BLog.Info(
                    $"STAGE10B_MUTATION operation={operation}; target={command.LogicalName}; result=committed; " +
                    $"raw-bytes={rawBytes}; generation={selected.Generation}; envelope-bytes={encoded.Length}; " +
                    $"bridge-bytes={BridgeBytes()}; logical={selected.LogicalHash}; restart-required=true"
                );
                return Stage10BMutationResult.Committed(exported, encoded.Length, BridgeBytes());
            }
            catch (Exception exception)
            {
                LastFailureCategory = Category(exception);
                Stage3BLog.Error(
                    $"STAGE10B_MUTATION operation={operation}; target={SanitizeReason(command.LogicalName)}; result=failed; " +
                    $"category={LastFailureCategory}; type={exception.GetType().Name}; raw-bytes={rawBytes}; " +
                    $"prior-generation={selected?.Generation ?? 0}; restart-required={externalMutationRequiresRestart.ToString().ToLowerInvariant()}"
                );
                return Stage10BMutationResult.Failed(LastFailureCategory, CreateExportSnapshotUnsafe(), externalMutationRequiresRestart);
            }
        }
    }

    // Stage 13B keeps the external-mutation guard set while it rebuilds only
    // Celeste's high-level state.  This ticket is deliberately made from the
    // durable A/B authority, not from the stale in-process materialisation.
    internal ExternalMutationReloadTicket PrepareExternalMutationReload()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (!externalMutationRequiresRestart || selected == null)
                throw Failure("reload-not-required", "No verified external mutation is waiting to be reloaded.");

            Candidate a = ReadCandidate("A");
            Candidate b = ReadCandidate("B");
            Snapshot durable = Select(a, b)
                ?? throw Failure("reload-generation-missing", "Neither durable slot contains a supported reload generation.");
            if (durable.Generation != selected.Generation || durable.LogicalHash != selected.LogicalHash)
                throw Failure("reload-generation-mismatch", "The selected durable generation changed before reload preparation.");

            ClearMaterializedFiles();
            Materialize(durable);
            Snapshot recaptured = Capture(durable.Generation);
            if (recaptured.Generation != durable.Generation || recaptured.LogicalHash != durable.LogicalHash)
                throw Failure("reload-materialization-mismatch", "Re-materialised files do not match the selected durable generation.");

            ExternalMutationReloadTicket ticket = new(
                durable.Generation,
                durable.LogicalHash,
                durable.Entries.Single(value => value.Name == "settings").Present,
                durable.Entries.Single(value => value.Name == "0").Present,
                durable.Entries.Single(value => value.Name == "1").Present,
                durable.Entries.Single(value => value.Name == "2").Present
            );
            Stage3BLog.Info(
                $"STAGE13B_PREPARE result=PASS; generation={ticket.Generation}; logical={ticket.LogicalHash}; " +
                $"presence={ticket.PresenceSummary}; generation-advanced=false; stale-guard=true"
            );
            return ticket;
        }
    }

    // The host calls this only after the new Settings/Input graph and a normal
    // Overworld/OuiMainMenu have independently passed validation.  Re-read A/B
    // and re-capture the materialised files one final time before permitting
    // UserIO writes again.
    internal void CompleteExternalMutationReload(ExternalMutationReloadTicket ticket)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (!externalMutationRequiresRestart || selected == null)
                throw Failure("reload-guard-missing", "The external-mutation guard was cleared before reload completion.");

            Candidate a = ReadCandidate("A");
            Candidate b = ReadCandidate("B");
            Snapshot durable = Select(a, b)
                ?? throw Failure("reload-generation-missing", "The durable reload generation disappeared before completion.");
            if (durable.Generation != ticket.Generation || durable.LogicalHash != ticket.LogicalHash ||
                selected.Generation != ticket.Generation || selected.LogicalHash != ticket.LogicalHash)
            {
                throw Failure("reload-completion-ticket-mismatch", "The durable reload ticket no longer identifies the selected generation.");
            }

            Snapshot recaptured = Capture(ticket.Generation);
            if (recaptured.LogicalHash != ticket.LogicalHash)
                throw Failure("reload-completion-hash-mismatch", "Materialised files changed before reload completion.");

            externalMutationRequiresRestart = false;
            Stage3BLog.Info(
                $"STAGE13B_COMPLETE result=PASS; generation={ticket.Generation}; logical={ticket.LogicalHash}; " +
                "generation-advanced=false; stale-guard=false"
            );
        }
    }

    private Stage10AExportSnapshot CreateExportSnapshotUnsafe()
    {
        Dictionary<string, byte[]?> files = new(StringComparer.Ordinal);
        foreach (string logicalName in new[] { "settings", "0", "1", "2" })
        {
            Entry? entry = selected?.Entries.Single(value => value.Name == logicalName);
            files.Add(logicalName, entry is { Present: true } ? entry.Payload.ToArray() : null);
        }
        return new Stage10AExportSnapshot(selected?.Generation ?? 0, selected?.LogicalHash ?? "none", files);
    }

    internal void ClearNamespaceForDiagnostics()
    {
        EnsureNonProductionDiagnosticNamespace();
        lock (gate)
        {
            defaults.RemoveObject(Key("A"));
            defaults.RemoveObject(Key("B"));
            defaults.Synchronize();
            selected = null;
            externalMutationRequiresRestart = false;
            ClearMaterializedFiles();
            LastFailureCategory = "none";
            sizeWarningObserved = false;
        }
    }

    internal void WriteRawForDiagnostics(string slot, byte[] value)
    {
        EnsureNonProductionDiagnosticNamespace();
        if (slot is not ("A" or "B")) throw new ArgumentOutOfRangeException(nameof(slot));
        using (NSString key = new(Key(slot)))
        using (NSData data = NSData.FromArray(value))
            defaults.SetValueForKey(data, key);
        defaults.Synchronize();
    }

    internal byte[]? ReadRawForDiagnostics(string slot)
    {
        EnsureNonProductionDiagnosticNamespace();
        return defaults.DataForKey(Key(slot))?.ToArray();
    }

    internal void InjectSizeWarningForDiagnostics()
    {
        EnsureNonProductionDiagnosticNamespace();
        lock (gate) sizeWarningObserved = true;
    }

    internal static byte[] EncodeForDiagnostics(
        ulong generation,
        IReadOnlyList<DiagnosticEntry> entries,
        ushort version = FormatVersion
    )
    {
        List<Entry> converted = entries.Select(item => new Entry(item.Name, item.Present, item.Serializer, item.Payload)).ToList();
        return Encode(Snapshot.Create(generation, converted, version), version);
    }

    internal static DiagnosticDecoded DecodeForDiagnostics(byte[] encoded)
    {
        Snapshot snapshot = Decode(encoded);
        return Describe(snapshot, encoded.Length);
    }

    internal static byte[] CompressForDiagnostics(byte[] payload) => Compress(payload);
    internal static byte[] DecompressForDiagnostics(byte[] stored, int declaredLength, int maximumBytes) =>
        DecompressBounded(stored, declaredLength, maximumBytes);
    internal static string DecodeFailureCategoryForDiagnostics(byte[] encoded)
    {
        try { _ = Decode(encoded); return "none"; }
        catch (UnsupportedFormatException) { return "unsupported-format"; }
        catch (Exception exception) { return Category(exception); }
    }
    internal static string PolicyFailureCategoryForDiagnostics(string policy, int first, int second = 0)
    {
        try
        {
            switch (policy)
            {
                case "compressed-entry": ValidateCompressedLength(first); break;
                case "envelope": ValidateEnvelopeLength(first); break;
                case "bridge-total": ValidateBridgeLength(first, second); break;
                default: throw new ArgumentOutOfRangeException(nameof(policy));
            }
            return "none";
        }
        catch (Exception exception) { return Category(exception); }
    }

    private static byte[] Encode(Snapshot snapshot, ushort version)
    {
        if (version is not (LegacyFormatVersion or PreviousFormatVersion or FormatVersion))
            throw new UnsupportedFormatException(version);

        using MemoryStream body = new();
        using (BinaryWriter writer = new(body, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes(Magic));
            WriteUInt16(writer, version);
            WriteUInt16(writer, 1);
            WriteUInt64(writer, snapshot.Generation);
            writer.Write(CelesteIdentityBytes);
            if (version >= PreviousFormatVersion)
            {
                WriteUInt16(writer, SettingsSchemaVersion);
                WriteUInt16(writer, SaveDataSchemaVersion);
            }
            WriteUInt16(writer, checked((ushort)snapshot.Entries.Count));
            foreach (Entry entry in snapshot.Entries.OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                byte[] name = Encoding.UTF8.GetBytes(entry.Name);
                WriteUInt16(writer, checked((ushort)name.Length));
                writer.Write(name);
                writer.Write(entry.Present ? (byte)1 : (byte)0);
                writer.Write((byte)entry.Serializer);
                if (version < FormatVersion)
                {
                    WriteUInt32(writer, checked((uint)entry.Payload.Length));
                    writer.Write(entry.Payload);
                    writer.Write(entry.Present ? SHA256.HashData(entry.Payload) : ZeroHash);
                    continue;
                }

                if (!entry.Present)
                {
                    writer.Write(CompressionNone);
                    WriteUInt32(writer, 0);
                    WriteUInt32(writer, 0);
                    writer.Write(ZeroHash);
                    writer.Write(ZeroHash);
                    continue;
                }

                byte[] stored = Compress(entry.Payload);
                ValidateCompressedLength(stored.Length, entry.Name);
                writer.Write(CompressionZlibLevel9);
                WriteUInt32(writer, checked((uint)entry.Payload.Length));
                WriteUInt32(writer, checked((uint)stored.Length));
                writer.Write(stored);
                writer.Write(SHA256.HashData(stored));
                writer.Write(SHA256.HashData(entry.Payload));
            }
            WriteUInt32(writer, CompleteMarker);
        }
        byte[] withoutChecksum = body.ToArray();
        body.Write(SHA256.HashData(withoutChecksum));
        return body.ToArray();
    }

    private static Snapshot Decode(byte[] encoded)
    {
        ValidateEnvelopeLength(encoded.Length);
        if (encoded.Length < 8 + 2 + 2 + 8 + 32 + 2 + 4 + 32)
            throw Failure("envelope-invalid", "Envelope is truncated.");
        ReadOnlySpan<byte> body = encoded.AsSpan(0, encoded.Length - 32);
        ReadOnlySpan<byte> checksum = encoded.AsSpan(encoded.Length - 32);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(body), checksum))
            throw Failure("envelope-invalid", "Envelope checksum mismatch.");

        SpanReader reader = new(body);
        if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != Magic)
            throw Failure("envelope-invalid", "Envelope magic mismatch.");
        ushort version = reader.ReadUInt16();
        if (version > FormatVersion) throw new UnsupportedFormatException(version);
        if (version is not (LegacyFormatVersion or PreviousFormatVersion or FormatVersion))
            throw Failure("unsupported-format", "Unsupported legacy envelope version.");
        if (reader.ReadUInt16() != 1)
            throw Failure("envelope-invalid", "Commit-complete flag is absent.");
        ulong generation = reader.ReadUInt64();
        if (generation == 0) throw Failure("envelope-invalid", "Generation zero is invalid.");
        if (!CryptographicOperations.FixedTimeEquals(reader.ReadBytes(32), CelesteIdentityBytes))
            throw Failure("envelope-invalid", "Celeste input identity mismatch.");
        if (version >= PreviousFormatVersion &&
            (reader.ReadUInt16() != SettingsSchemaVersion || reader.ReadUInt16() != SaveDataSchemaVersion))
        {
            throw Failure("serializer-invalid", "Serializer schema mismatch.");
        }
        int count = reader.ReadUInt16();
        if (count != Files.Length) throw Failure("envelope-invalid", "Envelope file count mismatch.");

        List<Entry> entries = new(count);
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            string name = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadUInt16()));
            if (!seen.Add(name)) throw Failure("envelope-invalid", "Duplicate file entry.");
            FilePolicy policy = Policy(name);
            byte state = reader.ReadByte();
            if (state > 1) throw Failure("envelope-invalid", "Invalid entry state.");
            SerializerKind serializer = (SerializerKind)reader.ReadByte();
            if (serializer != policy.Serializer) throw Failure("serializer-invalid", "Entry serializer mismatch.");
            bool present = state == 1;
            Entry entry = version < FormatVersion
                ? DecodeLegacyEntry(ref reader, policy, name, serializer, present)
                : DecodeCompressedEntry(ref reader, policy, name, serializer, present);
            entries.Add(entry);
        }
        if (reader.ReadUInt32() != CompleteMarker || !reader.AtEnd)
            throw Failure("envelope-invalid", "Envelope completion marker mismatch.");
        return Snapshot.Create(generation, entries, version);
    }

    private static Entry DecodeLegacyEntry(
        ref SpanReader reader,
        FilePolicy policy,
        string name,
        SerializerKind serializer,
        bool present
    )
    {
        int length = checked((int)reader.ReadUInt32());
        if (length > policy.MaximumBytes)
            throw Failure("raw-uncompressed-limit", $"{name} legacy payload exceeds its v2 safety limit.");
        byte[] payload = reader.ReadBytes(length).ToArray();
        ReadOnlySpan<byte> payloadHash = reader.ReadBytes(32);
        if (present)
        {
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(payload), payloadHash))
                throw Failure("uncompressed-hash-mismatch", $"{name} legacy payload checksum mismatch.");
            ValidatePayload(policy, payload);
        }
        else if (length != 0 || !payloadHash.SequenceEqual(ZeroHash))
        {
            throw Failure("envelope-invalid", "Deleted legacy entry contains payload data.");
        }
        return new Entry(name, present, serializer, payload);
    }

    private static Entry DecodeCompressedEntry(
        ref SpanReader reader,
        FilePolicy policy,
        string name,
        SerializerKind serializer,
        bool present
    )
    {
        byte algorithm = reader.ReadByte();
        int uncompressedLength = checked((int)reader.ReadUInt32());
        int storedLength = checked((int)reader.ReadUInt32());
        if (uncompressedLength > policy.MaximumBytes)
            throw Failure("decompression-limit", $"{name} declares {uncompressedLength} uncompressed bytes; maximum is {policy.MaximumBytes}.");
        ValidateCompressedLength(storedLength, name);
        byte[] stored = reader.ReadBytes(storedLength).ToArray();
        ReadOnlySpan<byte> storedHash = reader.ReadBytes(32);
        ReadOnlySpan<byte> payloadHash = reader.ReadBytes(32);

        if (!present)
        {
            if (algorithm != CompressionNone || uncompressedLength != 0 || storedLength != 0 ||
                !storedHash.SequenceEqual(ZeroHash) || !payloadHash.SequenceEqual(ZeroHash))
            {
                throw Failure("envelope-invalid", "Deleted compressed entry contains payload metadata.");
            }
            return new Entry(name, false, serializer, Array.Empty<byte>());
        }
        if (algorithm != CompressionZlibLevel9)
            throw Failure("unsupported-compression", $"{name} uses unsupported compression algorithm {algorithm}.");
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(stored), storedHash))
            throw Failure("compressed-hash-mismatch", $"{name} compressed payload checksum mismatch.");

        byte[] payload = DecompressBounded(stored, uncompressedLength, policy.MaximumBytes);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(payload), payloadHash))
            throw Failure("uncompressed-hash-mismatch", $"{name} uncompressed payload checksum mismatch.");
        ValidatePayload(policy, payload);
        return new Entry(name, true, serializer, payload);
    }

    private static byte[] Compress(byte[] payload)
    {
        using MemoryStream output = new();
        using (ZLibStream zlib = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(payload);
        return output.ToArray();
    }

    private static byte[] DecompressBounded(byte[] stored, int declaredLength, int maximumBytes)
    {
        if (declaredLength < 0 || declaredLength > maximumBytes)
            throw Failure("decompression-limit", "Declared uncompressed length is outside the allowed range.");
        try
        {
            using MemoryStream input = new(stored, writable: false);
            using ZLibStream zlib = new(input, CompressionMode.Decompress, leaveOpen: true);
            using MemoryStream output = new(Math.Min(declaredLength, 64 * 1024));
            byte[] buffer = new byte[16 * 1024];
            while (true)
            {
                int count = zlib.Read(buffer, 0, buffer.Length);
                if (count == 0) break;
                if (output.Length + count > declaredLength || output.Length + count > maximumBytes)
                    throw Failure("decompression-limit", "Decompressed output exceeded its declared or policy limit.");
                output.Write(buffer, 0, count);
            }
            byte[] payload = output.ToArray();
            if (payload.Length != declaredLength)
                throw Failure("decompression-invalid", $"Decompressed length {payload.Length} does not match declared length {declaredLength}.");

            // v2 defines one canonical zlib-level-9 representation. Re-encoding
            // rejects trailing data and alternative/noncanonical streams that a
            // buffered decompressor could otherwise accept silently.
            if (!CryptographicOperations.FixedTimeEquals(Compress(payload), stored))
                throw Failure("decompression-invalid", "Compressed stream is noncanonical or contains trailing data.");
            return payload;
        }
        catch (PersistenceFailureException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Failure("decompression-invalid", "Compressed stream is malformed or truncated.", exception);
        }
    }

    private static DiagnosticDecoded Describe(Snapshot snapshot, int envelopeBytes)
    {
        List<DiagnosticEntrySize> sizes = new();
        int raw = 0;
        int stored = 0;
        foreach (Entry entry in snapshot.Entries)
        {
            int rawBytes = entry.Present ? entry.Payload.Length : 0;
            int storedBytes = entry.Present
                ? snapshot.SourceFormatVersion == FormatVersion ? Compress(entry.Payload).Length : rawBytes
                : 0;
            raw += rawBytes;
            stored += storedBytes;
            sizes.Add(new DiagnosticEntrySize(entry.Name, entry.Present, rawBytes, storedBytes, Hash(entry.Payload)));
        }
        return new DiagnosticDecoded(snapshot.SourceFormatVersion, snapshot.Generation, snapshot.LogicalHash, raw, stored, envelopeBytes, sizes);
    }

    private static FilePolicy Policy(string logicalName) => Files.SingleOrDefault(value => value.LogicalName == logicalName)
        ?? throw Failure("envelope-invalid", $"Unknown durable file entry '{logicalName}'.");

    private string SafePath(string relative)
    {
        string combined = Path.GetFullPath(Path.Combine(root, relative));
        string requiredPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(requiredPrefix, StringComparison.Ordinal))
            throw Failure("materialization", "Materialized path escaped the session root.");
        return combined;
    }

    private static string PrefixForNamespace(string value) => value switch
    {
        "production" => "CelesteTvOS.Persistence.v1",
        "acceptance" => "CelesteTvOS.Persistence.Acceptance.v1",
        "restart" => "CelesteTvOS.Persistence.Restart.v1",
        "tests" => "CelesteTvOS.Persistence.Tests.v1",
        _ => throw new InvalidOperationException("Stage 6 storage namespace must be a committed fixed category.")
    };

    private string Key(string slot) => $"{keyPrefix}.{slot}";

    private void EnsureNonProductionDiagnosticNamespace()
    {
        if (NamespaceCategory == "production")
            throw new InvalidOperationException("Diagnostics may not mutate production persistence keys.");
    }

    private static string SanitizeReason(string reason) =>
        new(reason.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or ':' or '.').Take(96).ToArray());

    internal static void WriteMaterializedPayload(string path, byte[] payload)
    {
        using NSData data = NSData.FromArray(payload);
        using NSUrl url = NSUrl.FromFilename(path);
        if (!data.Save(url, atomically: true))
            throw Failure("materialization", "Foundation failed to materialize an approved Celeste state file.");
    }

    private static void DeleteIfPresent(string path) { if (File.Exists(path)) File.Delete(path); }
    private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(Stage6PersistenceStore)); }
    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static void ValidateCompressedLength(int length, string logicalName = "entry")
    {
        if (length < 0 || length > HardCompressedEntryBudgetBytes)
            throw Failure("compressed-entry-limit", $"{logicalName} stores {length} compressed bytes; maximum is {HardCompressedEntryBudgetBytes}.");
    }
    private static void ValidateEnvelopeLength(int length)
    {
        if (length < 0 || length > HardEnvelopeBudgetBytes)
            throw Failure("envelope-limit", $"Encoded slot {length} exceeds {HardEnvelopeBudgetBytes} bytes.");
    }
    private static void ValidateBridgeLength(int targetBytes, int recoveryBytes)
    {
        if (targetBytes < 0 || recoveryBytes < 0 || (long)targetBytes + recoveryBytes > HardTotalBudgetBytes)
            throw Failure("bridge-total-limit", $"Bridge usage {targetBytes + (long)recoveryBytes} exceeds {HardTotalBudgetBytes} bytes.");
    }
    private static PersistenceFailureException Failure(string category, string message, Exception? inner = null) => new(category, message, inner);
    private static string Category(Exception exception) => exception is PersistenceFailureException failure ? failure.Category : "unexpected";
    private static void WriteUInt16(BinaryWriter writer, ushort value) { Span<byte> bytes = stackalloc byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, value); writer.Write(bytes); }
    private static void WriteUInt32(BinaryWriter writer, uint value) { Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, value); writer.Write(bytes); }
    private static void WriteUInt64(BinaryWriter writer, ulong value) { Span<byte> bytes = stackalloc byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(bytes, value); writer.Write(bytes); }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            _ = Commit("host-disposal");
            TvOSStage6PersistenceHooks.Reset();
            NSNotificationCenter.DefaultCenter.RemoveObserver(sizeObserver);
            sizeObserver.Dispose();
            disposed = true;
        }
    }

    internal readonly record struct RestoreResult(
        ulong Generation,
        string LogicalHash,
        string SlotAStatus,
        string SlotBStatus,
        bool Materialized,
        ushort FormatVersion
    );
    internal readonly record struct DiagnosticEntry(string Name, bool Present, SerializerKind Serializer, byte[] Payload);
    internal readonly record struct DiagnosticEntrySize(string Name, bool Present, int UncompressedBytes, int StoredBytes, string Sha256);
    internal readonly record struct DiagnosticDecoded(
        ushort FormatVersion,
        ulong Generation,
        string LogicalHash,
        int UncompressedPayloadBytes,
        int StoredPayloadBytes,
        int EnvelopeBytes,
        IReadOnlyList<DiagnosticEntrySize> Entries
    );
    internal enum SerializerKind : byte { Settings = 1, SaveData = 2 }
    internal sealed record ExternalMutationReloadTicket(
        ulong Generation,
        string LogicalHash,
        bool SettingsPresent,
        bool Slot0Present,
        bool Slot1Present,
        bool Slot2Present)
    {
        internal bool ExpectedPresent(string logicalName) => logicalName switch
        {
            "settings" => SettingsPresent,
            "0" => Slot0Present,
            "1" => Slot1Present,
            "2" => Slot2Present,
            _ => throw new ArgumentOutOfRangeException(nameof(logicalName))
        };

        internal string PresenceSummary =>
            $"settings:{Bit(SettingsPresent)},0:{Bit(Slot0Present)},1:{Bit(Slot1Present)},2:{Bit(Slot2Present)}";

        private static int Bit(bool value) => value ? 1 : 0;
    }
    private sealed record FilePolicy(string LogicalName, string PrimaryPath, string BackupPath, SerializerKind Serializer, int MaximumBytes);
    private sealed record Entry(string Name, bool Present, SerializerKind Serializer, byte[] Payload);
    private sealed record Snapshot(ulong Generation, IReadOnlyList<Entry> Entries, string LogicalHash, ushort SourceFormatVersion)
    {
        internal static Snapshot Create(ulong generation, IReadOnlyList<Entry> entries, ushort sourceFormatVersion)
        {
            if (entries.Count != Files.Length) throw Failure("envelope-invalid", "Snapshot must contain the complete file set.");
            using MemoryStream logical = new();
            foreach (Entry entry in entries.OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                logical.Write(Encoding.UTF8.GetBytes(entry.Name));
                logical.WriteByte(0);
                logical.WriteByte(entry.Present ? (byte)1 : (byte)0);
                logical.WriteByte((byte)entry.Serializer);
                logical.Write(SHA256.HashData(entry.Payload));
            }
            return new Snapshot(
                generation,
                entries.OrderBy(value => value.Name, StringComparer.Ordinal).ToArray(),
                Convert.ToHexString(SHA256.HashData(logical.ToArray())).ToLowerInvariant(),
                sourceFormatVersion
            );
        }
    }
    private sealed record Candidate(string Slot, string Status, int RawSize, Snapshot? Snapshot);
    private sealed class UnsupportedFormatException(ushort version) : Exception { internal ushort Version { get; } = version; }
    private sealed class PersistenceFailureException(string category, string message, Exception? inner = null) : IOException(message, inner)
    {
        internal string Category { get; } = category;
    }
    private ref struct SpanReader
    {
        private ReadOnlySpan<byte> remaining;
        internal SpanReader(ReadOnlySpan<byte> source) => remaining = source;
        internal bool AtEnd => remaining.IsEmpty;
        internal byte ReadByte() { ReadOnlySpan<byte> value = ReadBytes(1); return value[0]; }
        internal ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(2));
        internal uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(4));
        internal ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(8));
        internal ReadOnlySpan<byte> ReadBytes(int count)
        {
            if (count < 0 || count > remaining.Length)
                throw Failure("envelope-invalid", "Envelope length exceeds remaining data.");
            ReadOnlySpan<byte> result = remaining[..count];
            remaining = remaining[count..];
            return result;
        }
    }
}
#endif
