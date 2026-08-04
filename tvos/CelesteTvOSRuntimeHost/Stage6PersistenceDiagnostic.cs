#if CELESTE_RUNTIME && TVOS_STAGE6_HOST
using System.Buffers.Binary;
using System.Security.Cryptography;
using Celeste;

namespace CelesteTvOSHost;

internal static class Stage6PersistenceDiagnostic
{
    private static int passed;

    internal static void Run(Stage6PersistenceStore store, string sessionRoot)
    {
        if (store.NamespaceCategory != "tests") throw new InvalidOperationException("Persistence diagnostics require the isolated tests namespace.");
        passed = 0;
        byte[] settingsA = SettingsBytes(3, 7);
        byte[] settingsB = SettingsBytes(8, 2);
        byte[] saveA = SaveBytes("Stage 6 A", deaths: 2);
        byte[] saveB = SaveBytes("Stage 6 B", deaths: 9);
        var emptyEntries = Entries(null, null, null, null);
        var oneSaveEntries = Entries(settingsA, saveA, null, null);
        var twoSaveEntries = Entries(settingsB, saveA, saveB, null);

        store.ClearNamespaceForDiagnostics();
        Assert(!store.Restore().Materialized, "empty-store");
        Assert(store.Flush("diagnostic-empty") && store.ReadRawForDiagnostics("A") == null && store.ReadRawForDiagnostics("B") == null,
            "empty-lifecycle-flush-does-not-create-generation");

        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(1, oneSaveEntries));
        Assert(store.Restore().Generation == 1, "one-valid-slot");
        store.WriteRawForDiagnostics("B", Stage6PersistenceStore.EncodeForDiagnostics(2, twoSaveEntries));
        Assert(store.Restore().Generation == 2, "two-valid-slots-newest-wins");

        byte[] corruptEnvelope = store.ReadRawForDiagnostics("B")!;
        corruptEnvelope[^1] ^= 0x40;
        store.WriteRawForDiagnostics("B", corruptEnvelope);
        Assert(store.Restore().Generation == 1, "corrupted-newest-envelope-fallback");

        byte[] fileChecksum = Stage6PersistenceStore.EncodeForDiagnostics(3, twoSaveEntries);
        int firstChecksum = FirstPayloadChecksumOffset(fileChecksum);
        fileChecksum[firstChecksum] ^= 1;
        RewriteEnvelopeChecksum(fileChecksum);
        store.WriteRawForDiagnostics("B", fileChecksum);
        Assert(store.Restore().Generation == 1, "corrupted-file-checksum-fallback");

        byte[] truncated = Stage6PersistenceStore.EncodeForDiagnostics(4, twoSaveEntries)[..80];
        store.WriteRawForDiagnostics("B", truncated);
        Assert(store.Restore().Generation == 1, "truncated-newest-fallback");

        byte[] malformedLength = Stage6PersistenceStore.EncodeForDiagnostics(5, twoSaveEntries);
        BinaryPrimitives.WriteUInt32LittleEndian(malformedLength.AsSpan(FirstPayloadLengthOffset(malformedLength)), uint.MaxValue);
        RewriteEnvelopeChecksum(malformedLength);
        store.WriteRawForDiagnostics("B", malformedLength);
        Assert(store.Restore().Generation == 1, "malformed-envelope-length");

        byte[] duplicate = Stage6PersistenceStore.EncodeForDiagnostics(6, twoSaveEntries);
        int secondName = NextEntryNameOffset(duplicate, FirstEntryNameOffset());
        duplicate[secondName + 2] = (byte)'0';
        RewriteEnvelopeChecksum(duplicate);
        store.WriteRawForDiagnostics("B", duplicate);
        Assert(store.Restore().Generation == 1, "duplicate-entry-rejected");

        byte[] unknown = Stage6PersistenceStore.EncodeForDiagnostics(7, twoSaveEntries);
        unknown[FirstEntryNameOffset() + 2] = (byte)'x';
        RewriteEnvelopeChecksum(unknown);
        store.WriteRawForDiagnostics("B", unknown);
        Assert(store.Restore().Generation == 1, "unknown-entry-rejected");

        byte[] future = Stage6PersistenceStore.EncodeForDiagnostics(8, twoSaveEntries);
        BinaryPrimitives.WriteUInt16LittleEndian(future.AsSpan(8), 99);
        RewriteEnvelopeChecksum(future);
        store.WriteRawForDiagnostics("B", future);
        Assert(store.Restore().Generation == 1 && store.ReadRawForDiagnostics("B")!.SequenceEqual(future), "future-format-preserved");
        Directory.CreateDirectory(Path.Combine(sessionRoot, "Saves"));
        Stage6PersistenceStore.WriteMaterializedPayload(Path.Combine(sessionRoot, "Saves", "settings.celeste"), settingsB);
        Assert(!store.Commit("future-format-conflict") && store.ReadRawForDiagnostics("B")!.SequenceEqual(future),
            "future-format-and-supported-recovery-preserved-on-write");

        byte[] wrongIdentity = Stage6PersistenceStore.EncodeForDiagnostics(9, twoSaveEntries);
        wrongIdentity[20] ^= 1;
        RewriteEnvelopeChecksum(wrongIdentity);
        store.WriteRawForDiagnostics("B", wrongIdentity);
        Assert(store.Restore().Generation == 1, "wrong-input-identity");

        byte[] wrongSchema = Stage6PersistenceStore.EncodeForDiagnostics(10, twoSaveEntries);
        BinaryPrimitives.WriteUInt16LittleEndian(wrongSchema.AsSpan(52), 2);
        RewriteEnvelopeChecksum(wrongSchema);
        store.WriteRawForDiagnostics("B", wrongSchema);
        Assert(store.Restore().Generation == 1, "wrong-serializer-version");

        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(12, twoSaveEntries));
        store.WriteRawForDiagnostics("B", Stage6PersistenceStore.EncodeForDiagnostics(11, oneSaveEntries));
        Assert(store.Restore().Generation == 12, "lower-generation-stale-slot");
        store.WriteRawForDiagnostics("B", Array.Empty<byte>());
        Assert(store.Restore().Generation == 12, "one-missing-or-invalid-slot");
        store.WriteRawForDiagnostics("A", Array.Empty<byte>());
        Assert(!store.Restore().Materialized, "both-invalid-new-game");

        store.ClearNamespaceForDiagnostics();
        byte[] priorOversizeRecovery = Stage6PersistenceStore.EncodeForDiagnostics(13, oneSaveEntries);
        store.WriteRawForDiagnostics("A", priorOversizeRecovery);
        Assert(store.Restore().Generation == 13, "oversized-payload-prior-generation-established");
        Directory.CreateDirectory(Path.Combine(sessionRoot, "Saves"));
        Stage6PersistenceStore.WriteMaterializedPayload(Path.Combine(sessionRoot, "Saves", "settings.celeste"), new byte[25 * 1024]);
        Assert(
            !store.Commit("diagnostic-oversized-payload") &&
            store.ReadRawForDiagnostics("A")!.SequenceEqual(priorOversizeRecovery) &&
            store.ReadRawForDiagnostics("B") == null &&
            store.Restore().Generation == 13,
            "oversized-payload-retains-prior-generation");

        store.ClearNamespaceForDiagnostics();
        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(20, oneSaveEntries));
        Assert(store.Restore().Generation == 20, "interruption-before-write-keeps-old");
        store.WriteRawForDiagnostics("B", Stage6PersistenceStore.EncodeForDiagnostics(21, twoSaveEntries));
        Assert(store.Restore().Generation == 21, "interruption-after-one-complete-write-selects-new");

        Task<bool>[] concurrent = Enumerable.Range(0, 8).Select(index => Task.Run(() => store.Commit($"concurrent-{index}"))).ToArray();
        Task.WaitAll(concurrent);
        Assert(concurrent.All(task => task.Result), "concurrent-commit-serialization");

        store.ClearNamespaceForDiagnostics();
        _ = store.Restore();
        TvOSStage6PersistenceHooks.CommitRequested = store.Commit;
        Assert(UserIO.Save<Settings>("settings", settingsA), "actual-userio-settings-save");
        Assert(UserIO.Save<SaveData>("0", saveA), "actual-userio-save-slot-write");
        Assert(UserIO.Load<Settings>("settings")?.MusicVolume == 3, "actual-userio-settings-load");
        Assert(UserIO.Load<SaveData>("0")?.Name == "Stage 6 A", "actual-userio-save-slot-load");
        Assert(UserIO.Delete("0") && !UserIO.Exists("0"), "actual-userio-deletion-entry");

        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(30, oneSaveEntries));
        store.WriteRawForDiagnostics("B", Array.Empty<byte>());
        store.SimulateMaterializationFailure = true;
        bool materializeFailed = false;
        try { _ = store.Restore(); } catch (IOException) { materializeFailed = true; }
        finally { store.SimulateMaterializationFailure = false; }
        Assert(materializeFailed, "failed-materialization-is-fatal");

        byte[] malformedSettings = System.Text.Encoding.UTF8.GetBytes("<Settings><MusicVolume>bad</MusicVolume></Settings>");
        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(31, Entries(malformedSettings, null, null, null)));
        store.WriteRawForDiagnostics("B", Array.Empty<byte>());
        Assert(!store.Restore().Materialized, "malformed-settings-xml-rejected");

        byte[] malformedSave = System.Text.Encoding.UTF8.GetBytes("<SaveData><Deaths>bad</Deaths></SaveData>");
        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(32, Entries(settingsA, malformedSave, null, null)));
        Assert(!store.Restore().Materialized, "malformed-save-xml-rejected");

        store.WriteRawForDiagnostics("A", Stage6PersistenceStore.EncodeForDiagnostics(33, emptyEntries, Stage6PersistenceStore.LegacyFormatVersion));
        store.WriteRawForDiagnostics("B", Array.Empty<byte>());
        RestoreResultLegacy(store);

        Assert(!store.SizeWarningObserved, "no-userdefaults-size-warning");
        Stage3BLog.Info($"STAGE6_DIAGNOSTIC_PASS tests={passed}; namespace=tests; bridge-bytes={store.BridgeBytes()}");
    }

    private static void RestoreResultLegacy(Stage6PersistenceStore store)
    {
        Stage6PersistenceStore.RestoreResult result = store.Restore();
        Assert(result.Generation == 33 && result.SlotAStatus == "valid-v0", "v0-migration-read");
        Assert(store.Commit("v0-migration") && store.Generation == 33, "v0-unchanged-not-rewritten-until-intentional-change");
        Assert(UserIO.Save<Settings>("settings", SettingsBytes(6, 4)) && store.Generation == 34, "v0-intentional-change-migrates-to-v1");
    }

    private static IReadOnlyList<Stage6PersistenceStore.DiagnosticEntry> Entries(byte[]? settings, byte[]? slot0, byte[]? slot1, byte[]? slot2) =>
        new[]
        {
            Entry("settings", settings, Stage6PersistenceStore.SerializerKind.Settings),
            Entry("0", slot0, Stage6PersistenceStore.SerializerKind.SaveData),
            Entry("1", slot1, Stage6PersistenceStore.SerializerKind.SaveData),
            Entry("2", slot2, Stage6PersistenceStore.SerializerKind.SaveData)
        };

    private static Stage6PersistenceStore.DiagnosticEntry Entry(string name, byte[]? payload, Stage6PersistenceStore.SerializerKind serializer) =>
        new(name, payload != null, serializer, payload ?? Array.Empty<byte>());

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

    private static int FirstEntryNameOffset() => 58;
    private static int FirstPayloadLengthOffset(byte[] value) => FirstEntryNameOffset() + 2 + BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(FirstEntryNameOffset())) + 1 + 1;
    private static int FirstPayloadChecksumOffset(byte[] value)
    {
        int lengthOffset = FirstPayloadLengthOffset(value);
        return lengthOffset + 4 + checked((int)BinaryPrimitives.ReadUInt32LittleEndian(value.AsSpan(lengthOffset)));
    }
    private static int NextEntryNameOffset(byte[] value, int nameOffset)
    {
        int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(nameOffset));
        int payloadLengthOffset = nameOffset + 2 + nameLength + 2;
        int payloadLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(value.AsSpan(payloadLengthOffset)));
        return payloadLengthOffset + 4 + payloadLength + 32;
    }

    private static void RewriteEnvelopeChecksum(byte[] value)
    {
        SHA256.HashData(value.AsSpan(0, value.Length - 32)).CopyTo(value.AsSpan(value.Length - 32));
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException($"Stage 6 diagnostic failed: {name}");
        passed++;
        Stage3BLog.Info($"STAGE6_TEST name={name}; result=PASS");
    }
}
#endif
