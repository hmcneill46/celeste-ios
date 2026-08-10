#if CELESTE_RUNTIME && TVOS_STAGE6_HOST
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Celeste;
using Foundation;

namespace CelesteTvOSHost;

internal static class Stage6PersistenceDiagnostic
{
    private static int passed;

    internal static void Run(Stage6PersistenceStore store, string sessionRoot)
    {
        if (store.NamespaceCategory != "tests")
            throw new InvalidOperationException("Persistence diagnostics require the isolated tests namespace.");

        passed = 0;
        byte[] settingsA = SettingsBytes(3, 7);
        byte[] settingsB = SettingsBytes(8, 2);
        byte[] saveA = SaveBytes("Diagnostic A", deaths: 2);
        byte[] saveB = SaveBytes("Diagnostic B", deaths: 9);
        var emptyEntries = Entries(null, null, null, null);
        var oneSaveEntries = Entries(settingsA, saveA, null, null);
        var twoSaveEntries = Entries(settingsB, saveA, saveB, null);
        var allSaveEntries = Entries(settingsA, saveA, saveB, SaveBytes("Diagnostic C", deaths: 15));

        BasicV2(store, emptyEntries, oneSaveEntries, twoSaveEntries, allSaveEntries);
        CompressionAndCorruption(twoSaveEntries);
        Migration(store, sessionRoot, settingsA, settingsB, oneSaveEntries, twoSaveEntries, emptyEntries);
        LimitBoundaries(store, sessionRoot, settingsA);
        ExistingBehavior(store, sessionRoot, settingsA, saveA, oneSaveEntries, emptyEntries);
        SaveManagerMutationSuite(store, sessionRoot, settingsA, settingsB, saveA, saveB, oneSaveEntries);
        LargeFixtureSuite();
#if STAGE9B_RETAINED_FIXTURES
        RetainedPhysicalFixtureSuite();
#endif

        Assert(!store.SizeWarningObserved, "no-userdefaults-size-warning");
        Stage3BLog.Info($"STAGE9B_DIAGNOSTIC_PASS tests={passed}; namespace=tests; bridge-bytes={store.BridgeBytes()}");
        Stage3BLog.Info($"STAGE6_DIAGNOSTIC_PASS tests={passed}; namespace=tests; bridge-bytes={store.BridgeBytes()}");
    }

    private static void BasicV2(
        Stage6PersistenceStore store,
        IReadOnlyList<Stage6PersistenceStore.DiagnosticEntry> empty,
        IReadOnlyList<Stage6PersistenceStore.DiagnosticEntry> one,
        IReadOnlyList<Stage6PersistenceStore.DiagnosticEntry> two,
        IReadOnlyList<Stage6PersistenceStore.DiagnosticEntry> all
    )
    {
        store.ClearNamespaceForDiagnostics();
        Assert(!store.Restore().Materialized, "v2-empty-store");
        Assert(store.Flush("diagnostic-empty") && store.ReadRawForDiagnostics("A") == null && store.ReadRawForDiagnostics("B") == null,
            "empty-lifecycle-flush-does-not-create-generation");

        byte[] oneEncoded = Stage6PersistenceStore.EncodeForDiagnostics(1, one);
        Stage6PersistenceStore.DiagnosticDecoded oneDecoded = Stage6PersistenceStore.DecodeForDiagnostics(oneEncoded);
        Assert(oneDecoded.FormatVersion == 2 && oneDecoded.Generation == 1, "one-valid-v2-generation");
        Assert(oneDecoded.StoredPayloadBytes < oneDecoded.UncompressedPayloadBytes, "v2-entry-compression-effective");
        store.WriteRawForDiagnostics("A", oneEncoded);
        Assert(store.Restore().Generation == 1 && store.SelectedFormatVersion == 2, "one-valid-v2-slot-restore");

        byte[] twoEncoded = Stage6PersistenceStore.EncodeForDiagnostics(2, two);
        store.WriteRawForDiagnostics("B", twoEncoded);
        Assert(store.Restore().Generation == 2, "two-valid-v2-newest-wins");

        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(5, one));
        store.WriteRawForDiagnostics("B", Stage6PersistenceStore.EncodeForDiagnostics(4, two));
        Assert(store.Restore().Generation == 5, "highest-generation-selected-not-format-order");

        byte[] deleted = Stage6PersistenceStore.EncodeForDiagnostics(6, empty);
        Stage6PersistenceStore.DiagnosticDecoded deletedDecoded = Stage6PersistenceStore.DecodeForDiagnostics(deleted);
        Assert(deletedDecoded.Entries.All(entry => !entry.Present && entry.StoredBytes == 0), "deleted-entries-have-no-payload");

        byte[] allEncoded = Stage6PersistenceStore.EncodeForDiagnostics(7, all);
        Stage6PersistenceStore.DiagnosticDecoded allDecoded = Stage6PersistenceStore.DecodeForDiagnostics(allEncoded);
        Assert(allDecoded.Entries.Count(entry => entry.Present) == 4, "settings-and-three-save-slots");
        Assert(allDecoded.EnvelopeBytes < Stage6PersistenceStore.HardEnvelopeBudgetBytes, "basic-all-slot-envelope-budget");
    }

    private static void CompressionAndCorruption(IReadOnlyList<Stage6PersistenceStore.DiagnosticEntry> entries)
    {
        byte[] source = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Celeste-v2-deterministic\n", 4096)));
        byte[] compressedA = Stage6PersistenceStore.CompressForDiagnostics(source);
        byte[] compressedB = Stage6PersistenceStore.CompressForDiagnostics(source);
        Assert(compressedA.SequenceEqual(compressedB), "deterministic-zlib-level9");
        Assert(Stage6PersistenceStore.DecompressForDiagnostics(compressedA, source.Length, source.Length).SequenceEqual(source),
            "bounded-compression-roundtrip");

        byte[] encoded = Stage6PersistenceStore.EncodeForDiagnostics(20, entries);
        V2EntryLayout layout = Layout(encoded, "0");

        byte[] compressedHash = encoded.ToArray();
        compressedHash[layout.StoredOffset] ^= 0x40;
        RewriteEnvelopeChecksum(compressedHash);
        Assert(Category(compressedHash) == "compressed-hash-mismatch", "compressed-checksum-corruption");

        byte[] uncompressedHash = encoded.ToArray();
        uncompressedHash[layout.UncompressedHashOffset] ^= 0x20;
        RewriteEnvelopeChecksum(uncompressedHash);
        Assert(Category(uncompressedHash) == "uncompressed-hash-mismatch", "uncompressed-checksum-corruption");

        byte[] malformed = encoded.ToArray();
        Array.Fill(malformed, (byte)0xA5, layout.StoredOffset, layout.StoredLength);
        RewriteStoredHashAndEnvelope(malformed, Layout(malformed, "0"));
        Assert(Category(malformed) == "decompression-invalid", "malformed-compressed-stream");

        byte[] truncated = RemoveLastStoredByte(encoded, "0");
        Assert(Category(truncated) == "decompression-invalid", "truncated-compressed-stream");

        byte[] trailing = AddTrailingStoredByte(encoded, "0", 0x5A);
        Assert(Category(trailing) == "decompression-invalid", "trailing-compressed-data-rejected");

        byte[] tooLargeStored = encoded.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            tooLargeStored.AsSpan(layout.StoredLengthOffset),
            checked((uint)(Stage6PersistenceStore.HardCompressedEntryBudgetBytes + 1))
        );
        RewriteEnvelopeChecksum(tooLargeStored);
        Assert(Category(tooLargeStored) == "compressed-entry-limit", "declared-compressed-length-limit");

        byte[] tooLargeRaw = encoded.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            tooLargeRaw.AsSpan(layout.UncompressedLengthOffset),
            checked((uint)(Stage6PersistenceStore.MaximumSaveDataBytes + 1))
        );
        RewriteEnvelopeChecksum(tooLargeRaw);
        Assert(Category(tooLargeRaw) == "decompression-limit", "declared-uncompressed-length-limit");

        byte[] outputExceeds = encoded.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            outputExceeds.AsSpan(layout.UncompressedLengthOffset),
            checked((uint)(layout.UncompressedLength - 1))
        );
        RewriteEnvelopeChecksum(outputExceeds);
        Assert(Category(outputExceeds) == "decompression-limit", "decompressed-output-exceeds-declaration");

        byte[] unsupported = encoded.ToArray();
        unsupported[layout.AlgorithmOffset] = 99;
        RewriteEnvelopeChecksum(unsupported);
        Assert(Category(unsupported) == "unsupported-compression", "unsupported-compression-identifier");

        byte[] envelopeChecksum = encoded.ToArray();
        envelopeChecksum[^1] ^= 1;
        Assert(Category(envelopeChecksum) == "envelope-invalid", "outer-envelope-integrity");

        byte[] incompressible = SaveBytesWithRandomFlag(96 * 1024);
        byte[] incompressibleStored = Stage6PersistenceStore.CompressForDiagnostics(incompressible);
        Assert(incompressible.Length <= Stage6PersistenceStore.MaximumSaveDataBytes, "incompressible-valid-fixture-uncompressed-bounded");
        Assert(incompressibleStored.Length <= Stage6PersistenceStore.HardCompressedEntryBudgetBytes, "incompressible-valid-fixture-stored-bounded");
        Assert(Stage6PersistenceStore.DecompressForDiagnostics(incompressibleStored, incompressible.Length,
            Stage6PersistenceStore.MaximumSaveDataBytes).SequenceEqual(incompressible), "incompressible-valid-fixture-roundtrip");
    }

    private static void Migration(
        Stage6PersistenceStore store,
        string sessionRoot,
        byte[] settingsA,
        byte[] settingsB,
        IReadOnlyList<Stage6PersistenceStore.DiagnosticEntry> one,
        IReadOnlyList<Stage6PersistenceStore.DiagnosticEntry> two,
        IReadOnlyList<Stage6PersistenceStore.DiagnosticEntry> empty
    )
    {
        store.ClearNamespaceForDiagnostics();
        byte[] v1A = Stage6PersistenceStore.EncodeForDiagnostics(73, one, Stage6PersistenceStore.PreviousFormatVersion);
        byte[] v1B = Stage6PersistenceStore.EncodeForDiagnostics(74, two, Stage6PersistenceStore.PreviousFormatVersion);
        store.WriteRawForDiagnostics("A", v1A);
        store.WriteRawForDiagnostics("B", v1B);
        Stage6PersistenceStore.RestoreResult v1Restore = store.Restore();
        Assert(v1Restore.Generation == 74 && v1Restore.FormatVersion == 1, "valid-v1-read");
        Assert(store.Commit("v1-startup-unchanged") && store.ReadRawForDiagnostics("A")!.SequenceEqual(v1A) &&
            store.ReadRawForDiagnostics("B")!.SequenceEqual(v1B), "v1-startup-does-not-rewrite");

        WriteSessionFile(sessionRoot, "settings", settingsA);
        Assert(store.Commit("v1-intentional-change") && store.Generation == 75 && store.SelectedFormatVersion == 2,
            "v1-intentional-change-writes-v2");
        Assert(Stage6PersistenceStore.DecodeForDiagnostics(store.ReadRawForDiagnostics("A")!).FormatVersion == 2 &&
            store.ReadRawForDiagnostics("B")!.SequenceEqual(v1B), "first-v2-write-preserves-old-v1");
        Assert(store.Restore().Generation == 75, "mixed-v1-v2-restore");

        WriteSessionFile(sessionRoot, "settings", settingsB);
        Assert(store.Commit("next-v2-change") && store.Generation == 76, "next-v2-write-advances-generation");
        Assert(Stage6PersistenceStore.DecodeForDiagnostics(store.ReadRawForDiagnostics("A")!).FormatVersion == 2 &&
            Stage6PersistenceStore.DecodeForDiagnostics(store.ReadRawForDiagnostics("B")!).FormatVersion == 2,
            "second-v2-write-naturally-replaces-v1");

        byte[] newest = store.ReadRawForDiagnostics("B")!;
        newest[^1] ^= 1;
        store.WriteRawForDiagnostics("B", newest);
        Assert(store.Restore().Generation == 75, "corrupt-newest-v2-falls-back-to-older-v2");

        store.ClearNamespaceForDiagnostics();
        store.WriteRawForDiagnostics("A", v1A);
        byte[] corruptV2 = Stage6PersistenceStore.EncodeForDiagnostics(75, two);
        corruptV2[^1] ^= 1;
        store.WriteRawForDiagnostics("B", corruptV2);
        Assert(store.Restore().Generation == 73 && store.SelectedFormatVersion == 1, "corrupt-newest-v2-falls-back-to-v1");

        store.ClearNamespaceForDiagnostics();
        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(30, one));
        Assert(store.Restore().Generation == 30, "interrupted-migration-before-write-keeps-old");
        store.WriteRawForDiagnostics("B", Stage6PersistenceStore.EncodeForDiagnostics(31, two));
        Assert(store.Restore().Generation == 31, "interrupted-migration-after-complete-v2-selects-new");

        byte[] future = Stage6PersistenceStore.EncodeForDiagnostics(40, two);
        BinaryPrimitives.WriteUInt16LittleEndian(future.AsSpan(8), 99);
        RewriteEnvelopeChecksum(future);
        store.WriteRawForDiagnostics("B", future);
        Assert(store.Restore().Generation == 30 && store.ReadRawForDiagnostics("B")!.SequenceEqual(future), "future-format-preserved");
        WriteSessionFile(sessionRoot, "settings", settingsB);
        Assert(!store.Commit("future-format-conflict") && store.LastFailureCategory == "unsupported-format" &&
            store.ReadRawForDiagnostics("B")!.SequenceEqual(future), "future-format-fails-closed-on-write");

        store.ClearNamespaceForDiagnostics();
        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(50, empty, Stage6PersistenceStore.LegacyFormatVersion));
        Stage6PersistenceStore.RestoreResult v0 = store.Restore();
        Assert(v0.Generation == 50 && v0.FormatVersion == 0 && v0.SlotAStatus == "valid-v0", "valid-v0-read");
        Assert(store.Commit("v0-unchanged") && store.Generation == 50 && store.SelectedFormatVersion == 0,
            "v0-startup-does-not-rewrite");
        Assert(UserIO.Save<Settings>("settings", settingsA) && store.Generation == 51 && store.SelectedFormatVersion == 2,
            "v0-intentional-change-migrates-to-v2");
    }

    private static void LimitBoundaries(Stage6PersistenceStore store, string sessionRoot, byte[] settings)
    {
        byte[] saveBelow = ExactSaveBytes(Stage6PersistenceStore.MaximumSaveDataBytes - 1);
        byte[] saveAt = ExactSaveBytes(Stage6PersistenceStore.MaximumSaveDataBytes);
        byte[] saveAbove = ExactSaveBytes(Stage6PersistenceStore.MaximumSaveDataBytes + 1);
        Assert(Category(Stage6PersistenceStore.EncodeForDiagnostics(60, Entries(settings, saveBelow, null, null))) == "none",
            "save-uncompressed-limit-minus-one");
        Assert(Category(Stage6PersistenceStore.EncodeForDiagnostics(61, Entries(settings, saveAt, null, null))) == "none",
            "save-uncompressed-limit-inclusive");
        Assert(Category(Stage6PersistenceStore.EncodeForDiagnostics(62, Entries(settings, saveAbove, null, null))) == "decompression-limit",
            "save-uncompressed-limit-plus-one");

        byte[] settingsBelow = ExactSettingsBytes(Stage6PersistenceStore.MaximumSettingsBytes - 1);
        byte[] settingsAt = ExactSettingsBytes(Stage6PersistenceStore.MaximumSettingsBytes);
        byte[] settingsAbove = ExactSettingsBytes(Stage6PersistenceStore.MaximumSettingsBytes + 1);
        Assert(Category(Stage6PersistenceStore.EncodeForDiagnostics(63, Entries(settingsBelow, null, null, null))) == "none",
            "settings-uncompressed-limit-minus-one");
        Assert(Category(Stage6PersistenceStore.EncodeForDiagnostics(64, Entries(settingsAt, null, null, null))) == "none",
            "settings-uncompressed-limit-inclusive");
        Assert(Category(Stage6PersistenceStore.EncodeForDiagnostics(65, Entries(settingsAbove, null, null, null))) == "decompression-limit",
            "settings-uncompressed-limit-plus-one");

        Assert(Stage6PersistenceStore.PolicyFailureCategoryForDiagnostics("compressed-entry",
            Stage6PersistenceStore.HardCompressedEntryBudgetBytes) == "none", "compressed-entry-limit-inclusive");
        Assert(Stage6PersistenceStore.PolicyFailureCategoryForDiagnostics("compressed-entry",
            Stage6PersistenceStore.HardCompressedEntryBudgetBytes + 1) == "compressed-entry-limit", "compressed-entry-limit-plus-one");
        Assert(Stage6PersistenceStore.PolicyFailureCategoryForDiagnostics("envelope",
            Stage6PersistenceStore.HardEnvelopeBudgetBytes) == "none", "envelope-limit-inclusive");
        Assert(Stage6PersistenceStore.PolicyFailureCategoryForDiagnostics("envelope",
            Stage6PersistenceStore.HardEnvelopeBudgetBytes + 1) == "envelope-limit", "envelope-limit-plus-one");
        Assert(Stage6PersistenceStore.PolicyFailureCategoryForDiagnostics("bridge-total",
            Stage6PersistenceStore.HardTotalBudgetBytes - 1, 1) == "none", "bridge-total-limit-inclusive");
        Assert(Stage6PersistenceStore.PolicyFailureCategoryForDiagnostics("bridge-total",
            Stage6PersistenceStore.HardTotalBudgetBytes, 1) == "bridge-total-limit", "bridge-total-limit-plus-one");

        store.ClearNamespaceForDiagnostics();
        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(66, Entries(settings, null, null, null)));
        Assert(store.Restore().Generation == 66, "raw-capture-prior-generation-established");
        WriteSessionFile(sessionRoot, "0", saveAbove);
        Assert(!store.Commit("raw-limit-capture") && store.LastFailureCategory == "raw-uncompressed-limit" && store.Restore().Generation == 66,
            "raw-uncompressed-limit-retains-prior-generation");
    }

    private static void ExistingBehavior(
        Stage6PersistenceStore store,
        string sessionRoot,
        byte[] settings,
        byte[] save,
        IReadOnlyList<Stage6PersistenceStore.DiagnosticEntry> one,
        IReadOnlyList<Stage6PersistenceStore.DiagnosticEntry> empty
    )
    {
        store.ClearNamespaceForDiagnostics();
        _ = store.Restore();
        TvOSStage6PersistenceHooks.CommitRequested = store.Commit;
        Assert(UserIO.Save<Settings>("settings", settings), "actual-userio-settings-save");
        Assert(UserIO.Save<SaveData>("0", save), "actual-userio-save-slot-write");
        Assert(UserIO.Load<Settings>("settings")?.MusicVolume == 3, "actual-userio-settings-load");
        Assert(UserIO.Load<SaveData>("0")?.TotalDeaths == 2, "actual-userio-save-slot-load");
        Assert(UserIO.Delete("0") && !UserIO.Exists("0"), "actual-userio-deletion-entry");
        Assert(store.Flush("diagnostic-lifecycle"), "lifecycle-flush");

        Task<bool>[] concurrent = Enumerable.Range(0, 8).Select(index => Task.Run(() => store.Commit($"concurrent-{index}"))).ToArray();
        Task.WaitAll(concurrent);
        Assert(concurrent.All(task => task.Result), "eight-concurrent-commits-serialize");

        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(80, one));
        store.WriteRawForDiagnostics("B", Array.Empty<byte>());
        store.SimulateMaterializationFailure = true;
        bool materializationFailed = false;
        try { _ = store.Restore(); } catch (IOException) { materializationFailed = true; }
        finally { store.SimulateMaterializationFailure = false; }
        Assert(materializationFailed, "failed-materialization-is-fatal");

        byte[] malformedSettings = Encoding.UTF8.GetBytes("<Settings><MusicVolume>bad</MusicVolume></Settings>");
        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(81, Entries(malformedSettings, null, null, null)));
        Assert(!store.Restore().Materialized && store.Restore().SlotAStatus == "invalid-serializer-invalid",
            "malformed-settings-rejected");

        byte[] malformedSave = Encoding.UTF8.GetBytes("<SaveData><Deaths>bad</Deaths></SaveData>");
        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(82, Entries(settings, malformedSave, null, null)));
        Assert(!store.Restore().Materialized && store.Restore().SlotAStatus == "invalid-serializer-invalid",
            "malformed-save-rejected");

        byte[] duplicate = Stage6PersistenceStore.EncodeForDiagnostics(83, one);
        V2EntryLayout first = Layouts(duplicate)[0];
        V2EntryLayout second = Layouts(duplicate)[1];
        duplicate[second.NameOffset] = duplicate[first.NameOffset];
        RewriteEnvelopeChecksum(duplicate);
        store.WriteRawForDiagnostics("A", duplicate);
        Assert(!store.Restore().Materialized, "duplicate-entry-rejected");

        store.ClearNamespaceForDiagnostics();
        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(84, empty));
        Assert(store.Restore().Generation == 84, "size-warning-prior-generation");
        store.InjectSizeWarningForDiagnostics();
        WriteSessionFile(sessionRoot, "settings", settings);
        Assert(!store.Commit("injected-size-warning") && store.LastFailureCategory == "userdefaults-size-warning" &&
            store.ReadRawForDiagnostics("B") == null, "size-warning-injection-retains-prior");
        store.ClearNamespaceForDiagnostics();
    }

    private static void SaveManagerMutationSuite(
        Stage6PersistenceStore store,
        string sessionRoot,
        byte[] settingsA,
        byte[] settingsB,
        byte[] saveA,
        byte[] saveB,
        IReadOnlyList<Stage6PersistenceStore.DiagnosticEntry> initialEntries
    )
    {
        store.ClearNamespaceForDiagnostics();
        byte[] priorGeneration = Stage6PersistenceStore.EncodeForDiagnostics(200, initialEntries);
        store.WriteRawForDiagnostics("A", priorGeneration);
        Assert(store.Restore().Generation == 200, "save-manager-prior-generation-restored");
        Stage10AExportSnapshot before = store.CreateReadOnlySaveManagerSnapshot();

        Stage10BMutationResult replaced = store.MutateFromSaveManager(new Stage10BMutationCommand(
            "0", saveB, before.Generation, before.LogicalHash));
        Assert(replaced.Success && replaced.Changed && replaced.Snapshot.Generation == 201,
            "save-manager-replace-advances-once");
        Assert(replaced.Snapshot.Files["0"]!.SequenceEqual(saveB), "save-manager-replace-preserves-exact-bytes");
        Assert(store.ReadRawForDiagnostics("A")!.SequenceEqual(priorGeneration),
            "save-manager-replace-retains-prior-slot-byte-for-byte");
        Assert(store.ReadRawForDiagnostics("B") != null &&
            Stage6PersistenceStore.DecodeForDiagnostics(store.ReadRawForDiagnostics("B")!).Generation == 201,
            "save-manager-replace-readback-verified-v2");
        Assert(store.ExternalMutationRequiresRestart, "save-manager-replace-requires-restart");

        byte[] staleMaterialized = SettingsBytes(1, 1);
        WriteSessionFile(sessionRoot, "settings", staleMaterialized);
        Assert(store.Commit("stale-running-game") && store.Generation == 201 &&
            store.ExportLogicalPayloadForFutureSaveManager("settings")!.SequenceEqual(settingsA),
            "save-manager-stale-runtime-commit-suppressed");

        Stage10BMutationResult stale = store.MutateFromSaveManager(new Stage10BMutationCommand(
            "2", saveA, before.Generation, before.LogicalHash));
        Assert(stale.Conflict && store.Generation == 201, "save-manager-stale-revision-conflict");

        byte[] rawA = store.ReadRawForDiagnostics("A")!.ToArray();
        byte[] rawB = store.ReadRawForDiagnostics("B")!.ToArray();
        Stage10BMutationResult malformed = store.MutateFromSaveManager(new Stage10BMutationCommand(
            "settings", Encoding.UTF8.GetBytes("<Settings><MusicVolume>invalid</MusicVolume></Settings>"),
            replaced.Snapshot.Generation, replaced.Snapshot.LogicalHash));
        Assert(!malformed.Success && malformed.FailureCategory == "serializer-invalid" &&
            store.ReadRawForDiagnostics("A")!.SequenceEqual(rawA) && store.ReadRawForDiagnostics("B")!.SequenceEqual(rawB),
            "save-manager-malformed-upload-keeps-both-generations");

        byte[] oversized = ExactSaveBytes(Stage6PersistenceStore.MaximumSaveDataBytes + 1);
        Stage10BMutationResult tooLarge = store.MutateFromSaveManager(new Stage10BMutationCommand(
            "1", oversized, replaced.Snapshot.Generation, replaced.Snapshot.LogicalHash));
        Assert(!tooLarge.Success && tooLarge.FailureCategory == "raw-uncompressed-limit" && store.Generation == 201,
            "save-manager-oversized-upload-keeps-selected-generation");

        Stage10BMutationResult deleted = store.MutateFromSaveManager(new Stage10BMutationCommand(
            "0", null, replaced.Snapshot.Generation, replaced.Snapshot.LogicalHash));
        Assert(deleted.Success && deleted.Changed && deleted.Snapshot.Generation == 202 &&
            deleted.Snapshot.Files["0"] == null, "save-manager-delete-populated-slot");
        Stage10BMutationResult deleteAgain = store.MutateFromSaveManager(new Stage10BMutationCommand(
            "0", null, deleted.Snapshot.Generation, deleted.Snapshot.LogicalHash));
        Assert(deleteAgain.Success && !deleteAgain.Changed && deleteAgain.Snapshot.Generation == 202,
            "save-manager-delete-absent-is-noop");

        Stage10BMutationResult reset = store.MutateFromSaveManager(new Stage10BMutationCommand(
            "settings", null, deleted.Snapshot.Generation, deleted.Snapshot.LogicalHash));
        Assert(reset.Success && reset.Changed && reset.Snapshot.Generation == 203 &&
            reset.Snapshot.Files["settings"] == null, "save-manager-settings-reset-uses-absence");
        Assert(reset.Snapshot.Files["1"] == null && reset.Snapshot.Files["2"] == null,
            "save-manager-reset-does-not-create-save-slots");

        Stage10BMutationResult settingsReplace = store.MutateFromSaveManager(new Stage10BMutationCommand(
            "settings", settingsB, reset.Snapshot.Generation, reset.Snapshot.LogicalHash));
        Assert(settingsReplace.Success && settingsReplace.Snapshot.Files["settings"]!.SequenceEqual(settingsB),
            "save-manager-multiple-mutations-one-activation");

        byte[] newest = store.ReadRawForDiagnostics("A") != null &&
            Stage6PersistenceStore.DecodeFailureCategoryForDiagnostics(store.ReadRawForDiagnostics("A")!) == "none" &&
            Stage6PersistenceStore.DecodeForDiagnostics(store.ReadRawForDiagnostics("A")!).Generation == store.Generation
            ? store.ReadRawForDiagnostics("A")!.ToArray()
            : store.ReadRawForDiagnostics("B")!.ToArray();
        string newestSlot = store.ReadRawForDiagnostics("A") != null &&
            Stage6PersistenceStore.DecodeFailureCategoryForDiagnostics(store.ReadRawForDiagnostics("A")!) == "none" &&
            Stage6PersistenceStore.DecodeForDiagnostics(store.ReadRawForDiagnostics("A")!).Generation == store.Generation ? "A" : "B";
        newest[^1] ^= 1;
        store.WriteRawForDiagnostics(newestSlot, newest);
        Stage6PersistenceStore.RestoreResult fallback = store.Restore();
        Assert(fallback.Generation == settingsReplace.Snapshot.Generation - 1,
            "save-manager-corrupt-newest-falls-back-to-prior-generation");

        store.ClearNamespaceForDiagnostics();
        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(300, initialEntries));
        Assert(store.Restore().Generation == 300, "save-manager-concurrency-baseline");
        Stage10AExportSnapshot concurrentBefore = store.CreateReadOnlySaveManagerSnapshot();
        Stage10BMutationCommand first = new("1", saveA, concurrentBefore.Generation, concurrentBefore.LogicalHash);
        Stage10BMutationCommand second = new("2", saveB, concurrentBefore.Generation, concurrentBefore.LogicalHash);
        Task<Stage10BMutationResult>[] concurrent =
        {
            Task.Run(() => store.MutateFromSaveManager(first)),
            Task.Run(() => store.MutateFromSaveManager(second))
        };
        Task.WaitAll(concurrent);
        Assert(concurrent.Count(task => task.Result.Success && task.Result.Changed) == 1 &&
            concurrent.Count(task => task.Result.Conflict) == 1 && store.Generation == 301,
            "save-manager-concurrent-stale-mutation-conflict");
        store.ClearNamespaceForDiagnostics();
    }

    private static void LargeFixtureSuite()
    {
        var configurations = new[]
        {
            new FixtureConfig("late-chapter-5", 5, 360, 140, 160, 80, 24),
            new FixtureConfig("chapter-6", 6, 480, 190, 220, 96, 32),
            new FixtureConfig("chapter-7", 7, 640, 250, 320, 120, 40),
            new FixtureConfig("chapter-8", 8, 760, 300, 420, 150, 48),
            new FixtureConfig("chapter-9", 9, 920, 380, 560, 180, 56),
            new FixtureConfig("abc-sides-and-202-strawberries", 9, 1050, 430, 720, 202, 64),
            new FixtureConfig("near-complete-profile", 9, 1250, 520, 900, 202, 96)
        };

        Dictionary<string, byte[]> values = new(StringComparer.Ordinal);
        foreach (FixtureConfig configuration in configurations)
        {
            byte[] payload = TvOSSaveDataSerializer.SerializeToBytes(ProgressedSave(configuration));
            byte[] roundtrip = TvOSSaveDataSerializer.SerializeToBytes(DeserializeSave(payload));
            Assert(roundtrip.SequenceEqual(payload), $"{configuration.Name}-serializer-roundtrip");
            Assert(payload.Length <= Stage6PersistenceStore.MaximumSaveDataBytes, $"{configuration.Name}-uncompressed-budget");
            byte[] stored = Stage6PersistenceStore.CompressForDiagnostics(payload);
            Assert(stored.Length <= Stage6PersistenceStore.HardCompressedEntryBudgetBytes, $"{configuration.Name}-stored-budget");
            values.Add(configuration.Name, payload);
            Stage3BLog.Info(
                $"STAGE9B_FIXTURE name={configuration.Name}; raw-bytes={payload.Length}; compressed-bytes={stored.Length}; " +
                $"raw-budget-percent={Percent(payload.Length, Stage6PersistenceStore.MaximumSaveDataBytes)}; " +
                $"stored-budget-percent={Percent(stored.Length, Stage6PersistenceStore.HardCompressedEntryBudgetBytes)}"
            );
        }

        byte[] settings = SettingsBytes(9, 8);
        byte[] threeSlot = Stage6PersistenceStore.EncodeForDiagnostics(100, Entries(
            settings,
            values["near-complete-profile"],
            values["abc-sides-and-202-strawberries"],
            values["chapter-9"]
        ));
        Stage6PersistenceStore.DiagnosticDecoded decoded = Stage6PersistenceStore.DecodeForDiagnostics(threeSlot);
        Assert(decoded.Entries.Count(entry => entry.Present) == 4, "large-three-slot-generation-decodes");
        Assert(decoded.EnvelopeBytes <= Stage6PersistenceStore.HardEnvelopeBudgetBytes, "large-three-slot-envelope-headroom");
        Assert(decoded.EnvelopeBytes * 2 <= Stage6PersistenceStore.HardTotalBudgetBytes, "large-three-slot-two-generation-headroom");
        Stage3BLog.Info(
            $"STAGE9B_THREE_SLOT raw-payload-bytes={decoded.UncompressedPayloadBytes}; compressed-payload-bytes={decoded.StoredPayloadBytes}; " +
            $"envelope-bytes={decoded.EnvelopeBytes}; two-generation-bytes={decoded.EnvelopeBytes * 2}; " +
            $"envelope-budget-percent={Percent(decoded.EnvelopeBytes, Stage6PersistenceStore.HardEnvelopeBudgetBytes)}; " +
            $"bridge-budget-percent={Percent(decoded.EnvelopeBytes * 2, Stage6PersistenceStore.HardTotalBudgetBytes)}"
        );
    }

#if STAGE9B_RETAINED_FIXTURES
    private static void RetainedPhysicalFixtureSuite()
    {
        string root = Path.Combine(NSBundle.MainBundle.ResourcePath!, "Stage9BFixtures");
        var expected = new Dictionary<string, (int Bytes, string Hash)>(StringComparer.Ordinal)
        {
            ["first-over.celeste"] = (32839, "7684bbe9ba363c8071b6dfc7ee25a91d7a0905ff5a196d5a38b5a9763aaa4266"),
            ["latest-over.celeste"] = (33259, "33eb7e8e2a1e29bbd1de5a5d77457fb33ed56cb69751a5990caf5ac7de976afe"),
            ["max-over.celeste"] = (33985, "89c003ebfa51d09dd10c7509b3948d1789e6265108df93ffdac2f7e1695d31df")
        };
        foreach ((string name, (int bytes, string hash)) in expected)
        {
            byte[] payload = File.ReadAllBytes(Path.Combine(root, name));
            Assert(payload.Length == bytes && Hash(payload) == hash, $"retained-{name}-identity");
            _ = DeserializeSave(payload);
            byte[] encoded = Stage6PersistenceStore.EncodeForDiagnostics(110, Entries(SettingsBytes(10, 10), payload, null, null));
            Stage6PersistenceStore.DiagnosticDecoded decoded = Stage6PersistenceStore.DecodeForDiagnostics(encoded);
            Assert(decoded.Entries.Single(entry => entry.Name == "0").UncompressedBytes == bytes, $"retained-{name}-v2-roundtrip");
            Stage3BLog.Info($"STAGE9B_RETAINED_FIXTURE name={name}; raw-bytes={bytes}; compressed-bytes={Stage6PersistenceStore.CompressForDiagnostics(payload).Length}; sha256={hash}");
        }
    }
#endif

    private static IReadOnlyList<Stage6PersistenceStore.DiagnosticEntry> Entries(byte[]? settings, byte[]? slot0, byte[]? slot1, byte[]? slot2) =>
        new[]
        {
            Entry("settings", settings, Stage6PersistenceStore.SerializerKind.Settings),
            Entry("0", slot0, Stage6PersistenceStore.SerializerKind.SaveData),
            Entry("1", slot1, Stage6PersistenceStore.SerializerKind.SaveData),
            Entry("2", slot2, Stage6PersistenceStore.SerializerKind.SaveData)
        };

    private static Stage6PersistenceStore.DiagnosticEntry Entry(
        string name,
        byte[]? payload,
        Stage6PersistenceStore.SerializerKind serializer
    ) => new(name, payload != null, serializer, payload ?? Array.Empty<byte>());

    private static byte[] SettingsBytes(int music, int sfx)
    {
        Settings settings = new() { MusicVolume = music, SFXVolume = sfx, Rumble = RumbleAmount.Off, LastSaveFile = 1 };
        return TvOSSettingsSerializer.SerializeToBytes(settings);
    }

    private static byte[] SaveBytes(string name, int deaths)
    {
        SaveData save = new() { Name = name, TotalDeaths = deaths, LastArea = new AreaKey(0), UnlockedAreas = 1 };
        return TvOSSaveDataSerializer.SerializeToBytes(save);
    }

    private static byte[] ExactSaveBytes(int target)
    {
        SaveData save = new() { Name = "Diagnostic", LastArea = new AreaKey(0), UnlockedAreas = 1 };
        return ExactSerialized(target, length =>
        {
            save.Flags.Clear();
            if (length > 0) save.Flags.Add(new string('x', length));
            return TvOSSaveDataSerializer.SerializeToBytes(save);
        });
    }

    private static byte[] ExactSettingsBytes(int target)
    {
        Settings settings = new() { MusicVolume = 5, SFXVolume = 5 };
        return ExactSerialized(target, length =>
        {
            settings.DefaultFileName = new string('x', length);
            return TvOSSettingsSerializer.SerializeToBytes(settings);
        });
    }

    private static byte[] ExactSerialized(int target, Func<int, byte[]> serialize)
    {
        int low = 0;
        int high = target;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            byte[] value = serialize(middle);
            if (value.Length == target) return value;
            if (value.Length < target) low = middle + 1;
            else high = middle - 1;
        }
        throw new InvalidOperationException($"Could not construct exact valid serializer payload of {target} bytes.");
    }

    private static byte[] SaveBytesWithRandomFlag(int approximateRawBytes)
    {
        SaveData save = new() { Name = "Diagnostic", LastArea = new AreaKey(0), UnlockedAreas = 1 };
        int baseLength = TvOSSaveDataSerializer.SerializeToBytes(save).Length;
        save.Flags.Add(DeterministicText(Math.Max(1, approximateRawBytes - baseLength - 32)));
        byte[] payload = TvOSSaveDataSerializer.SerializeToBytes(save);
        _ = DeserializeSave(payload);
        return payload;
    }

    private static string DeterministicText(int length)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        char[] value = new char[length];
        uint state = 0x9E3779B9;
        for (int i = 0; i < value.Length; i++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            value[i] = alphabet[(int)(state & 63)];
        }
        return new string(value);
    }

    private static SaveData ProgressedSave(FixtureConfig configuration)
    {
        SaveData save = new()
        {
            Version = "1.4.0.0",
            Name = "Diagnostic",
            Time = 99_000_000,
            LastSave = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UnlockedAreas = 10,
            TotalDeaths = 2500,
            TotalStrawberries = configuration.Strawberries,
            TotalGoldenStrawberries = configuration.Strawberries >= 202 ? 25 : 0,
            TotalJumps = 150_000,
            TotalWallJumps = 45_000,
            TotalDashes = 90_000,
            LastArea = new AreaKey(configuration.Area),
            SummitGems = Enumerable.Repeat(true, 6).ToArray(),
            RevealedChapter9 = configuration.Area >= 9,
            TheoSisterName = "Diagnostic"
        };
        for (int i = 0; i < 11; i++)
        {
            AreaStats area = new(i) { Cassette = i is > 0 and < 9 };
            for (int mode = 0; mode < area.Modes.Length; mode++)
            {
                AreaModeStats stats = area.Modes[mode];
                stats.Completed = i <= configuration.Area;
                stats.SingleRunCompleted = stats.Completed;
                stats.FullClear = stats.Completed && mode == 0;
                stats.HeartGem = i is > 0 and < 10;
                stats.Deaths = 100 + i * 10 + mode;
                stats.TimePlayed = 10_000_000L + i * 1000 + mode;
                stats.BestTime = 1_000_000L + i * 100 + mode;
                stats.BestFullClearTime = 1_100_000L + i * 100 + mode;
                stats.BestDashes = 50 + i;
                stats.BestDeaths = i;
                for (int checkpoint = 0; checkpoint < 8; checkpoint++)
                    stats.Checkpoints.Add($"a{i:D2}-m{mode}-checkpoint-{checkpoint:D2}");
            }
            save.Areas.Add(area);
        }
        for (int i = 0; i < configuration.Strawberries; i++)
        {
            AreaModeStats stats = save.Areas[i % save.Areas.Count].Modes[(i / save.Areas.Count) % 3];
            stats.Strawberries.Add(new EntityID($"berry-level-{i / 16:D2}", i));
            stats.TotalStrawberries++;
        }
        for (int i = 0; i < 256; i++) save.Flags.Add($"profile-flag-{i:D4}");
        for (int i = 0; i < 24; i++) save.Poem.Add($"poem-entry-{i:D2}");

        save.CurrentSession = CreateSession(configuration.Area);
        Session session = save.CurrentSession;
        session.Level = $"chapter-{configuration.Area}-fixture";
        session.StartCheckpoint = "diagnostic-checkpoint";
        session.FurthestSeenLevel = "diagnostic-furthest";
        session.OldStats = save.Areas[configuration.Area].Clone();
        session.Inventory = new PlayerInventory { Dashes = 2, DreamDash = true, Backpack = true };
        session.Cassette = true;
        session.HeartGem = true;
        session.HitCheckpoint = true;
        for (int i = 0; i < configuration.Flags; i++) session.Flags.Add($"session-flag-{configuration.Area:D2}-{i:D5}");
        for (int i = 0; i < configuration.LevelFlags; i++) session.LevelFlags.Add($"level-flag-{i:D5}");
        for (int i = 0; i < configuration.DoNotLoad; i++) session.DoNotLoad.Add(new EntityID($"room-{i / 32:D3}", i));
        for (int i = 0; i < configuration.Strawberries; i++) session.Strawberries.Add(new EntityID($"berry-room-{i / 16:D2}", i));
        for (int i = 0; i < configuration.Keys; i++) session.Keys.Add(new EntityID($"key-room-{i / 8:D2}", i));
        for (int i = 0; i < 96; i++) session.Counters.Add(new Session.Counter { Key = $"counter-{i:D3}", Value = i * 3 });
        return save;
    }

    private static Session CreateSession(int area)
    {
        SaveData seed = new() { Name = "Diagnostic", LastArea = new AreaKey(area), UnlockedAreas = area + 1 };
        XDocument document;
        using (MemoryStream input = new(TvOSSaveDataSerializer.SerializeToBytes(seed), writable: false))
            document = XDocument.Load(input, LoadOptions.None);
        XElement areas = document.Root!.Element("Areas")!;
        areas.AddBeforeSelf(new XElement(
            "CurrentSession",
            new XAttribute("Level", "diagnostic"),
            new XAttribute("InArea", "true"),
            new XElement("Area", new XAttribute("ID", area), new XAttribute("Mode", AreaMode.Normal))
        ));
        using MemoryStream serialized = new();
        document.Save(serialized, SaveOptions.DisableFormatting);
        serialized.Position = 0;
        return TvOSSaveDataSerializer.Deserialize(serialized).CurrentSession;
    }

    private static SaveData DeserializeSave(byte[] payload)
    {
        using MemoryStream stream = new(payload, writable: false);
        SaveData value = TvOSSaveDataSerializer.Deserialize(stream);
        if (stream.Position != stream.Length) throw new InvalidDataException("Fixture serializer left trailing bytes.");
        return value;
    }

    private static void WriteSessionFile(string sessionRoot, string logicalName, byte[] payload)
    {
        string path = Path.Combine(sessionRoot, "Saves", logicalName == "settings" ? "settings.celeste" : $"{logicalName}.celeste");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Stage6PersistenceStore.WriteMaterializedPayload(path, payload);
    }

    private static string Category(byte[] encoded) => Stage6PersistenceStore.DecodeFailureCategoryForDiagnostics(encoded);

    private static List<V2EntryLayout> Layouts(byte[] value)
    {
        if (BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(8)) != Stage6PersistenceStore.FormatVersion)
            throw new InvalidOperationException("Layout parser requires v2.");
        int offset = 58;
        int count = BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(56));
        List<V2EntryLayout> result = new(count);
        for (int i = 0; i < count; i++)
        {
            int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(offset));
            int nameOffset = offset + 2;
            string name = Encoding.UTF8.GetString(value, nameOffset, nameLength);
            int stateOffset = nameOffset + nameLength;
            int algorithmOffset = stateOffset + 2;
            int uncompressedLengthOffset = algorithmOffset + 1;
            int storedLengthOffset = uncompressedLengthOffset + 4;
            int storedOffset = storedLengthOffset + 4;
            int uncompressedLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(value.AsSpan(uncompressedLengthOffset)));
            int storedLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(value.AsSpan(storedLengthOffset)));
            int storedHashOffset = storedOffset + storedLength;
            int rawHashOffset = storedHashOffset + 32;
            offset = rawHashOffset + 32;
            result.Add(new V2EntryLayout(name, nameOffset, algorithmOffset, uncompressedLengthOffset, storedLengthOffset,
                storedOffset, storedLength, uncompressedLength, storedHashOffset, rawHashOffset));
        }
        return result;
    }

    private static V2EntryLayout Layout(byte[] value, string name) => Layouts(value).Single(entry => entry.Name == name);

    private static byte[] RemoveLastStoredByte(byte[] value, string name)
    {
        V2EntryLayout old = Layout(value, name);
        List<byte> bytes = value.ToList();
        bytes.RemoveAt(old.StoredOffset + old.StoredLength - 1);
        byte[] result = bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(old.StoredLengthOffset), checked((uint)(old.StoredLength - 1)));
        RewriteStoredHashAndEnvelope(result, Layout(result, name));
        return result;
    }

    private static byte[] AddTrailingStoredByte(byte[] value, string name, byte extra)
    {
        V2EntryLayout old = Layout(value, name);
        List<byte> bytes = value.ToList();
        bytes.Insert(old.StoredOffset + old.StoredLength, extra);
        byte[] result = bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(old.StoredLengthOffset), checked((uint)(old.StoredLength + 1)));
        RewriteStoredHashAndEnvelope(result, Layout(result, name));
        return result;
    }

    private static void RewriteStoredHashAndEnvelope(byte[] value, V2EntryLayout layout)
    {
        SHA256.HashData(value.AsSpan(layout.StoredOffset, layout.StoredLength)).CopyTo(value.AsSpan(layout.StoredHashOffset));
        RewriteEnvelopeChecksum(value);
    }

    private static void RewriteEnvelopeChecksum(byte[] value) =>
        SHA256.HashData(value.AsSpan(0, value.Length - 32)).CopyTo(value.AsSpan(value.Length - 32));

    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static string Percent(int value, int total) => ((double)value / total * 100.0).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException($"Stage 9B diagnostic failed: {name}");
        passed++;
        Stage3BLog.Info($"STAGE6_TEST name={name}; result=PASS");
    }

    private sealed record V2EntryLayout(
        string Name,
        int NameOffset,
        int AlgorithmOffset,
        int UncompressedLengthOffset,
        int StoredLengthOffset,
        int StoredOffset,
        int StoredLength,
        int UncompressedLength,
        int StoredHashOffset,
        int UncompressedHashOffset
    );

    private sealed record FixtureConfig(
        string Name,
        int Area,
        int Flags,
        int LevelFlags,
        int DoNotLoad,
        int Strawberries,
        int Keys
    );
}
#endif
