#if CELESTE_RUNTIME && TVOS_STAGE6_HOST
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Celeste;
using Foundation;

namespace CelesteTvOSHost;

internal sealed class Stage6PersistenceStore : IDisposable
{
    internal const ushort FormatVersion = 1;
    internal const ushort LegacyFormatVersion = 0;
    internal const ushort SettingsSchemaVersion = 1;
    internal const ushort SaveDataSchemaVersion = 1;
    internal const int HardTotalBudgetBytes = 256 * 1024;
    internal const int HardEnvelopeBudgetBytes = 124 * 1024;
    private const uint CompleteMarker = 0x434F4D50;
    private const string Magic = "CTVOSPV1";
    private const string CelesteIdentity = "fd73f8a2311fa5737ded550cbad4b75c85b7686b36432f59185e940fcb65fcfe";
    private static readonly byte[] CelesteIdentityBytes = Convert.FromHexString(CelesteIdentity);
    private static readonly FilePolicy[] Files =
    {
        new("settings", "Saves/settings.celeste", "Backups/settings.celeste", SerializerKind.Settings, 24 * 1024),
        new("0", "Saves/0.celeste", "Backups/0.celeste", SerializerKind.SaveData, 32 * 1024),
        new("1", "Saves/1.celeste", "Backups/1.celeste", SerializerKind.SaveData, 32 * 1024),
        new("2", "Saves/2.celeste", "Backups/2.celeste", SerializerKind.SaveData, 32 * 1024)
    };

    private readonly object gate = new();
    private readonly NSUserDefaults defaults;
    private readonly string root;
    private readonly string keyPrefix;
    private readonly NSObject sizeObserver;
    private Snapshot? selected;
    private bool disposed;
    private bool sizeWarningObserved;
    internal bool SimulateMaterializationFailure { get; set; }

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
                Stage3BLog.Error("STAGE6_DEFAULTS_SIZE_LIMIT_WARNING observed=true; acceptance=failure");
            });
        Directory.CreateDirectory(root);
    }

    internal ulong Generation { get { lock (gate) return selected?.Generation ?? 0; } }
    internal string LogicalHash { get { lock (gate) return selected?.LogicalHash ?? "none"; } }
    internal bool SizeWarningObserved { get { lock (gate) return sizeWarningObserved; } }
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
            ClearMaterializedFiles();
            if (selected == null)
            {
                Stage3BLog.Warning($"STAGE6_RESTORE namespace={NamespaceCategory}; selected=none; A={a.Status}; B={b.Status}; action=new-game-no-write");
                return new RestoreResult(0, "none", a.Status, b.Status, false);
            }
            Materialize(selected);
            Stage3BLog.Info($"STAGE6_RESTORE namespace={NamespaceCategory}; generation={selected.Generation}; logical={selected.LogicalHash}; A={a.Status}; B={b.Status}; migrated={selected.MigratedFromV0.ToString().ToLowerInvariant()}");
            return new RestoreResult(selected.Generation, selected.LogicalHash, a.Status, b.Status, true);
        }
    }

    internal bool Commit(string reason)
    {
        lock (gate)
        {
            try
            {
                ThrowIfDisposed();
                if (sizeWarningObserved) throw new InvalidOperationException("A UserDefaults size-limit warning was observed.");
                Snapshot candidate = Capture((selected?.Generation ?? HighestStoredGeneration()) + 1);
                if (selected == null && candidate.Entries.All(entry => !entry.Present))
                {
                    Stage3BLog.Info($"STAGE6_COMMIT reason={SanitizeReason(reason)}; result=unchanged-new-game; generation=0; logical=none");
                    return true;
                }
                if (selected != null && candidate.LogicalHash == selected.LogicalHash)
                {
                    Stage3BLog.Info($"STAGE6_COMMIT reason={SanitizeReason(reason)}; result=unchanged; generation={selected.Generation}; logical={selected.LogicalHash}");
                    return true;
                }

                byte[] encoded = Encode(candidate, FormatVersion);
                if (encoded.Length > HardEnvelopeBudgetBytes)
                    throw new InvalidDataException($"Encoded slot {encoded.Length} exceeds {HardEnvelopeBudgetBytes} bytes.");
                Candidate a = ReadCandidate("A");
                Candidate b = ReadCandidate("B");
                string target = ChooseTarget(a, b);
                int otherBytes = target == "A" ? b.RawSize : a.RawSize;
                if (encoded.Length + otherBytes > HardTotalBudgetBytes)
                    throw new InvalidDataException($"Bridge usage would exceed {HardTotalBudgetBytes} bytes.");

                using (NSString key = new(Key(target)))
                using (NSData data = NSData.FromArray(encoded))
                    defaults.SetValueForKey(data, key);
                defaults.Synchronize();
                Candidate readBack = ReadCandidate(target);
                if (readBack.Snapshot == null || readBack.Snapshot.Generation != candidate.Generation ||
                    readBack.Snapshot.LogicalHash != candidate.LogicalHash)
                    throw new InvalidDataException("UserDefaults read-back verification failed.");

                selected = readBack.Snapshot;
                Stage3BLog.Info($"STAGE6_COMMIT reason={SanitizeReason(reason)}; result=committed; slot={target}; generation={selected.Generation}; logical={selected.LogicalHash}; slot-bytes={encoded.Length}; bridge-bytes={BridgeBytes()}; app-domain-keys={defaults.ToDictionary().Count}");
                return true;
            }
            catch (Exception exception)
            {
                Stage3BLog.Error($"STAGE6_COMMIT reason={SanitizeReason(reason)}; result=failed; type={exception.GetType().Name}; message={exception.Message}; prior-generation={selected?.Generation ?? 0}");
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
            if (info.LinkTarget != null) throw new InvalidDataException($"Symlinks are not allowed for {policy.LogicalName}.");
            if (info.Length > policy.MaximumBytes) throw new InvalidDataException($"{policy.LogicalName} exceeds its payload limit.");
            using NSData? data = NSData.FromFile(path);
            if (data == null) throw new IOException($"Foundation failed to read approved state file {policy.LogicalName}.");
            byte[] payload = data.ToArray();
            ValidatePayload(policy, payload);
            entries.Add(new Entry(policy.LogicalName, true, policy.Serializer, payload));
        }
        return Snapshot.Create(generation, entries, migratedFromV0: false);
    }

    private void Materialize(Snapshot snapshot)
    {
        if (SimulateMaterializationFailure && NamespaceCategory != "production")
            throw new IOException("Simulated isolated materialization failure.");
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
        if (payload.Length > policy.MaximumBytes) throw new InvalidDataException($"{policy.LogicalName} exceeds its payload limit.");
        using MemoryStream stream = new(payload, writable: false);
        if (policy.Serializer == SerializerKind.Settings)
            _ = TvOSSettingsSerializer.Deserialize(stream);
        else
            _ = TvOSSaveDataSerializer.Deserialize(stream);
        if (stream.Position != stream.Length)
            throw new InvalidDataException($"{policy.LogicalName} serializer did not consume its complete payload.");
    }

    private Candidate ReadCandidate(string slot)
    {
        NSData? data = defaults.DataForKey(Key(slot));
        if (data == null) return new Candidate(slot, "missing", 0, null);
        byte[] bytes = data.ToArray();
        try
        {
            Snapshot decoded = Decode(bytes);
            return new Candidate(slot, decoded.MigratedFromV0 ? "valid-v0" : "valid", bytes.Length, decoded);
        }
        catch (UnsupportedFormatException exception)
        {
            return new Candidate(slot, $"unsupported-v{exception.Version}", bytes.Length, null);
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
            throw new InvalidOperationException(
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

    internal void ClearNamespaceForDiagnostics()
    {
        EnsureNonProductionDiagnosticNamespace();
        lock (gate)
        {
            defaults.RemoveObject(Key("A"));
            defaults.RemoveObject(Key("B"));
            defaults.Synchronize();
            selected = null;
            ClearMaterializedFiles();
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

    internal static byte[] EncodeForDiagnostics(ulong generation, IReadOnlyList<DiagnosticEntry> entries, ushort version = FormatVersion)
    {
        List<Entry> converted = entries.Select(item => new Entry(item.Name, item.Present, item.Serializer, item.Payload)).ToList();
        return Encode(Snapshot.Create(generation, converted, version == LegacyFormatVersion), version);
    }

    private static byte[] Encode(Snapshot snapshot, ushort version)
    {
        using MemoryStream body = new();
        using (BinaryWriter writer = new(body, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes(Magic));
            WriteUInt16(writer, version);
            WriteUInt16(writer, 1);
            WriteUInt64(writer, snapshot.Generation);
            writer.Write(CelesteIdentityBytes);
            if (version == FormatVersion)
            {
                WriteUInt16(writer, SettingsSchemaVersion);
                WriteUInt16(writer, SaveDataSchemaVersion);
            }
            else if (version != LegacyFormatVersion)
                throw new UnsupportedFormatException(version);
            WriteUInt16(writer, checked((ushort)snapshot.Entries.Count));
            foreach (Entry entry in snapshot.Entries.OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                byte[] name = Encoding.UTF8.GetBytes(entry.Name);
                WriteUInt16(writer, checked((ushort)name.Length));
                writer.Write(name);
                writer.Write(entry.Present ? (byte)1 : (byte)0);
                writer.Write((byte)entry.Serializer);
                WriteUInt32(writer, checked((uint)entry.Payload.Length));
                writer.Write(entry.Payload);
                writer.Write(entry.Present ? SHA256.HashData(entry.Payload) : new byte[32]);
            }
            WriteUInt32(writer, CompleteMarker);
        }
        byte[] withoutChecksum = body.ToArray();
        body.Write(SHA256.HashData(withoutChecksum));
        return body.ToArray();
    }

    private static Snapshot Decode(byte[] encoded)
    {
        if (encoded.Length < 8 + 2 + 2 + 8 + 32 + 2 + 4 + 32) throw new InvalidDataException("Envelope is truncated.");
        ReadOnlySpan<byte> body = encoded.AsSpan(0, encoded.Length - 32);
        ReadOnlySpan<byte> checksum = encoded.AsSpan(encoded.Length - 32);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(body), checksum))
            throw new InvalidDataException("Envelope checksum mismatch.");
        SpanReader reader = new(body);
        if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != Magic) throw new InvalidDataException("Envelope magic mismatch.");
        ushort version = reader.ReadUInt16();
        if (version > FormatVersion) throw new UnsupportedFormatException(version);
        if (version is not (LegacyFormatVersion or FormatVersion)) throw new InvalidDataException("Unsupported legacy envelope version.");
        if (reader.ReadUInt16() != 1) throw new InvalidDataException("Commit-complete flag is absent.");
        ulong generation = reader.ReadUInt64();
        if (generation == 0) throw new InvalidDataException("Generation zero is invalid.");
        if (!CryptographicOperations.FixedTimeEquals(reader.ReadBytes(32), CelesteIdentityBytes))
            throw new InvalidDataException("Celeste input identity mismatch.");
        if (version == FormatVersion && (reader.ReadUInt16() != SettingsSchemaVersion || reader.ReadUInt16() != SaveDataSchemaVersion))
            throw new InvalidDataException("Serializer schema mismatch.");
        int count = reader.ReadUInt16();
        if (count != Files.Length) throw new InvalidDataException("Envelope file count mismatch.");
        List<Entry> entries = new(count);
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            string name = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadUInt16()));
            if (!seen.Add(name)) throw new InvalidDataException("Duplicate file entry.");
            FilePolicy policy = Policy(name);
            byte state = reader.ReadByte();
            if (state > 1) throw new InvalidDataException("Invalid entry state.");
            SerializerKind serializer = (SerializerKind)reader.ReadByte();
            if (serializer != policy.Serializer) throw new InvalidDataException("Entry serializer mismatch.");
            int length = checked((int)reader.ReadUInt32());
            if (length > policy.MaximumBytes) throw new InvalidDataException("Entry payload exceeds its limit.");
            byte[] payload = reader.ReadBytes(length).ToArray();
            ReadOnlySpan<byte> payloadHash = reader.ReadBytes(32);
            bool present = state == 1;
            if (present)
            {
                if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(payload), payloadHash))
                    throw new InvalidDataException("Entry checksum mismatch.");
                ValidatePayload(policy, payload);
            }
            else if (length != 0 || !payloadHash.SequenceEqual(new byte[32]))
                throw new InvalidDataException("Deleted entry contains payload data.");
            entries.Add(new Entry(name, present, serializer, payload));
        }
        if (reader.ReadUInt32() != CompleteMarker || !reader.AtEnd) throw new InvalidDataException("Envelope completion marker mismatch.");
        return Snapshot.Create(generation, entries, version == LegacyFormatVersion);
    }

    private static FilePolicy Policy(string logicalName) => Files.SingleOrDefault(value => value.LogicalName == logicalName)
        ?? throw new InvalidDataException($"Unknown durable file entry '{logicalName}'.");

    private string SafePath(string relative)
    {
        string combined = Path.GetFullPath(Path.Combine(root, relative));
        string requiredPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(requiredPrefix, StringComparison.Ordinal)) throw new InvalidDataException("Materialized path escaped the session root.");
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
        if (NamespaceCategory == "production") throw new InvalidOperationException("Diagnostics may not mutate production persistence keys.");
    }

    private static string SanitizeReason(string reason) => new(reason.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or ':' or '.').Take(96).ToArray());
    internal static void WriteMaterializedPayload(string path, byte[] payload)
    {
        using NSData data = NSData.FromArray(payload);
        using NSUrl url = NSUrl.FromFilename(path);
        if (!data.Save(url, atomically: true))
            throw new IOException("Foundation failed to materialize an approved Celeste state file.");
    }
    private static void DeleteIfPresent(string path) { if (File.Exists(path)) File.Delete(path); }
    private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(Stage6PersistenceStore)); }
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

    internal readonly record struct RestoreResult(ulong Generation, string LogicalHash, string SlotAStatus, string SlotBStatus, bool Materialized);
    internal readonly record struct DiagnosticEntry(string Name, bool Present, SerializerKind Serializer, byte[] Payload);
    internal enum SerializerKind : byte { Settings = 1, SaveData = 2 }
    private sealed record FilePolicy(string LogicalName, string PrimaryPath, string BackupPath, SerializerKind Serializer, int MaximumBytes);
    private sealed record Entry(string Name, bool Present, SerializerKind Serializer, byte[] Payload);
    private sealed record Snapshot(ulong Generation, IReadOnlyList<Entry> Entries, string LogicalHash, bool MigratedFromV0)
    {
        internal static Snapshot Create(ulong generation, IReadOnlyList<Entry> entries, bool migratedFromV0)
        {
            if (entries.Count != Files.Length) throw new InvalidDataException("Snapshot must contain the complete file set.");
            using MemoryStream logical = new();
            foreach (Entry entry in entries.OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                logical.Write(Encoding.UTF8.GetBytes(entry.Name)); logical.WriteByte(0);
                logical.WriteByte(entry.Present ? (byte)1 : (byte)0);
                logical.WriteByte((byte)entry.Serializer);
                logical.Write(SHA256.HashData(entry.Payload));
            }
            return new Snapshot(generation, entries.OrderBy(value => value.Name, StringComparer.Ordinal).ToArray(), Convert.ToHexString(SHA256.HashData(logical.ToArray())).ToLowerInvariant(), migratedFromV0);
        }
    }
    private sealed record Candidate(string Slot, string Status, int RawSize, Snapshot? Snapshot);
    private sealed class UnsupportedFormatException(ushort version) : Exception { internal ushort Version { get; } = version; }
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
            if (count < 0 || count > remaining.Length) throw new InvalidDataException("Envelope length exceeds remaining data.");
            ReadOnlySpan<byte> result = remaining[..count];
            remaining = remaining[count..];
            return result;
        }
    }
}
#endif
