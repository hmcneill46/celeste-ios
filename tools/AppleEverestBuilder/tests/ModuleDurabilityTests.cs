#nullable disable
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Celeste.Mod;
using YamlDotNet.Serialization;

internal static class ModuleDurabilityTests
{
    private const string Closure = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    internal static int Run(string repository, string temporary)
    {
        int passed = 0;
        void Pass(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: module durability " + name);
            passed++;
        }

        byte[] AppleYaml(DesktopState state) => AppleEverestModuleYaml.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Deaths");
            writer.WriteStartObject();
            foreach ((string room, List<DesktopDeath> deaths) in state.Deaths.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(room);
                writer.WriteStartArray();
                foreach (DesktopDeath death in deaths)
                {
                    writer.WriteStartObject();
                    writer.WriteString("Room", death.Room);
                    writer.WritePropertyName("Position");
                    writer.WriteStartObject();
                    writer.WriteNumber("X", death.Position.X);
                    writer.WriteNumber("Y", death.Position.Y);
                    writer.WriteEndObject();
                    writer.WriteNumber("Amount", death.Amount);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        });

        DesktopState expected = new()
        {
            Deaths = new Dictionary<string, List<DesktopDeath>>(StringComparer.Ordinal)
            {
                ["lvl_a-01"] =
                [
                    new DesktopDeath { Room = "lvl_a-01", Position = new DesktopVector { X = 12.5f, Y = -3 }, Amount = 2 },
                    new DesktopDeath { Room = "lvl_a-01", Position = new DesktopVector { X = 30, Y = 44.25f }, Amount = 1 }
                ]
            }
        };

        byte[] appleBytes = AppleYaml(expected);
        IDeserializer desktopReader = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        DesktopState desktopFromApple = desktopReader.Deserialize<DesktopState>(Encoding.UTF8.GetString(appleBytes));
        Pass(Same(expected, desktopFromApple), "Apple JSON/YAML is accepted by pinned desktop YamlDotNet");

        ISerializer desktopWriter = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
            .Build();
        string desktopYaml = desktopWriter.Serialize(expected);
        using JsonDocument appleFromDesktop = AppleEverestModuleYaml.Parse(Encoding.UTF8.GetBytes(desktopYaml));
        Pass(appleFromDesktop.RootElement.GetProperty("Deaths").GetProperty("lvl_a-01")[0]
            .GetProperty("Position").GetProperty("X").GetSingle() == 12.5f,
            "pinned desktop YAML is accepted by bounded Apple reader");
        Pass(desktopReader.Deserialize<DesktopState>(desktopYaml).Deaths["lvl_a-01"][0].Amount == 2,
            "desktop YAML semantic round trip");
        Pass(appleBytes.SequenceEqual(AppleYaml(expected)), "Apple payload is deterministic");
        Pass(appleBytes[^1] == (byte)'\n', "Apple payload has stable YAML text ending");

        foreach (string malicious in new[]
        {
            "!System.Type value\n", "&anchor value\n", "value: *anchor\n", "%TAG ! tag:evil\n",
            "value: |\n  unbounded\n"
        })
        {
            bool rejected = false;
            try { using JsonDocument _ = AppleEverestModuleYaml.Parse(Encoding.UTF8.GetBytes(malicious)); }
            catch (InvalidDataException) { rejected = true; }
            Pass(rejected, "unsafe YAML construct rejected: " + malicious[0]);
        }
        bool emptyRejected = false;
        try { using JsonDocument _ = AppleEverestModuleYaml.Parse([]); }
        catch (InvalidDataException) { emptyRejected = true; }
        Pass(emptyRejected, "empty YAML rejected");

        byte[] baseA = SHA256.HashData(Encoding.UTF8.GetBytes("base-a"));
        byte[] baseB = SHA256.HashData(Encoding.UTF8.GetBytes("base-b"));
        AppleEverestModuleSnapshotEntry entryA = new("DeathMarkers", "2.0.0", "schema-a", appleBytes, null);
        AppleEverestModuleSnapshotEntry entryB = new("Other", "1.0.0", "schema-b", Encoding.UTF8.GetBytes("{}\n"), Encoding.UTF8.GetBytes("null\n"));
        AppleEverestModuleSnapshot snapshot = new(0, 7, baseA, Closure, [entryA, entryB]);
        byte[] encoded = AppleEverestModuleSnapshotCodec.Encode(snapshot);
        Pass(AppleEverestModuleSnapshotCodec.TryDecode(encoded, 0, out AppleEverestModuleSnapshot decoded) &&
             decoded.Generation == 7 && decoded.Entries.Length == 2 && decoded.Entries[0].Name == "DeathMarkers" &&
             decoded.Entries[0].Session == null, "aggregate round trip and explicit null session");
        Pass(!AppleEverestModuleSnapshotCodec.TryDecode(encoded, 1, out _), "wrong slot rejected");
        Pass(!AppleEverestModuleSnapshotCodec.TryDecode(encoded[..^1], 0, out _), "truncated aggregate rejected");
        byte[] badAggregate = (byte[])encoded.Clone();
        badAggregate[0] ^= 1;
        Pass(!AppleEverestModuleSnapshotCodec.TryDecode(badAggregate, 0, out _), "bad aggregate checksum rejected");
        bool duplicateRejected = false;
        try { AppleEverestModuleSnapshotCodec.Encode(new(0, 1, baseA, Closure, [entryA, entryA])); }
        catch (InvalidDataException) { duplicateRejected = true; }
        Pass(duplicateRejected, "duplicate module identity rejected");
        bool debugRejected = false;
        try { AppleEverestModuleSnapshotCodec.Encode(new(-1, 1, baseA, Closure, [entryA])); }
        catch (InvalidDataException) { debugRejected = true; }
        Pass(debugRejected, "debug slot excluded");
        bool oversizedRejected = false;
        try
        {
            AppleEverestModuleSnapshotCodec.Encode(new(0, 1, baseA, Closure,
                [new("TooLarge", "1", "s", new byte[AppleEverestModuleYaml.MaximumModuleBytes + 1], null)]));
        }
        catch (InvalidDataException) { oversizedRejected = true; }
        Pass(oversizedRejected, "oversized module payload rejected before storage");

        byte[] isolated = CorruptPayloadAndResign(encoded, appleBytes);
        Pass(AppleEverestModuleSnapshotCodec.TryDecode(isolated, 0, out AppleEverestModuleSnapshot isolatedDecoded) &&
             !isolatedDecoded.Entries.Single(item => item.Name == "DeathMarkers").SaveDataValid &&
             isolatedDecoded.Entries.Single(item => item.Name == "Other").SaveDataValid,
            "one bad module payload is isolated under a structurally valid aggregate");

        byte[] compressed = AppleEverestModuleCompression.Encode(encoded);
        Pass(AppleEverestModuleCompression.TryDecode(compressed, out byte[] expanded) && expanded.SequenceEqual(encoded),
            "tvOS compressed envelope round trip");
        byte[] badCompressed = (byte[])compressed.Clone();
        badCompressed[^1] ^= 1;
        Pass(!AppleEverestModuleCompression.TryDecode(badCompressed, out _), "tvOS envelope checksum rejected");
        Pass(!AppleEverestModuleCompression.TryDecode(compressed[..^1], out _), "tvOS truncated envelope rejected");
        bool incompressibleRejected = false;
        try
        {
            byte[] noise = new byte[AppleEverestModuleCompression.MaximumLogicalBytes];
            RandomNumberGenerator.Fill(noise);
            AppleEverestModuleCompression.Encode(noise);
        }
        catch (InvalidDataException) { incompressibleRejected = true; }
        Pass(incompressibleRejected, "tvOS compressed replica budget enforced");
        Pass(AppleEverestModuleCompression.MaximumTotalReplicaBytes ==
             AppleEverestModuleCompression.MaximumReplicaBytes * 6, "tvOS total A/B budget is explicit");

        FakeReplicaStore store = new();
        AppleEverestModuleReplicaAuthority authority = new(store, Closure);
        AppleEverestModuleReplicaState state = authority.Load(0, baseA);
        Pass(state.Selected == null && state.GenerationA == 0 && state.GenerationB == 0, "missing state defaults");
        AppleEverestModulePreparedWrite first = authority.Prepare(0, state, baseA, [entryA]);
        Pass(first.Replica == "A" && first.Snapshot.Generation == 1, "first generation selects A");
        Pass(authority.Commit(first, state), "first generation commits with exact readback");
        AppleEverestModuleReplicaState loaded = authority.Load(0, baseA);
        Pass(loaded.Selected?.Generation == 1, "primary matching generation loads");

        AppleEverestModulePreparedWrite second = authority.Prepare(0, state, baseB, [entryB]);
        Pass(second.Replica == "B" && authority.Commit(second, state), "second generation alternates to B");
        Pass(authority.Load(0, baseB).Selected?.Generation == 2, "newest matching generation loads");
        Pass(authority.Load(0, baseA).Selected?.Generation == 1, "previous-good matching generation recovers");
        Pass(authority.Load(0, SHA256.HashData(Encoding.UTF8.GetBytes("unrelated"))).Selected == null,
            "no base match resets rather than attaching stale state");
        Pass(new AppleEverestModuleReplicaAuthority(store, new string('f', 64)).Load(0, baseA).Selected == null,
            "closure mismatch resets only incompatible module aggregate");

        FakeReplicaStore slotStore = new();
        AppleEverestModuleReplicaAuthority slotAuthority = new(slotStore, Closure);
        for (int slot = 0; slot <= 2; slot++)
        {
            AppleEverestModuleReplicaState slotState = slotAuthority.Load(slot, baseA);
            Pass(slotAuthority.Commit(slotAuthority.Prepare(slot, slotState, baseA,
                 [new("Slot" + slot, "1", "s", Encoding.UTF8.GetBytes("{}\n"), null)]), slotState),
                "slot " + slot + " commit");
        }
        Pass(Enumerable.Range(0, 3).All(slot => slotAuthority.Load(slot, baseA).Selected?.Entries[0].Name == "Slot" + slot),
            "slots 0/1/2 remain isolated");
        slotAuthority.Delete(1);
        Pass(slotAuthority.Load(1, baseA).Selected == null && slotAuthority.Load(0, baseA).Selected != null,
            "slot delete removes only its A/B state");

        FakeReplicaStore faultStore = new();
        AppleEverestModuleReplicaAuthority faultAuthority = new(faultStore, Closure);
        AppleEverestModuleReplicaState faultState = faultAuthority.Load(0, baseA);
        AppleEverestModulePreparedWrite good = faultAuthority.Prepare(0, faultState, baseA, [entryA]);
        Pass(faultAuthority.Commit(good, faultState), "fault baseline commits");
        foreach (FaultMode mode in new[] { FaultMode.ThrowBeforeWrite, FaultMode.Truncate, FaultMode.ThrowAfterWrite })
        {
            AppleEverestModulePreparedWrite candidate = faultAuthority.Prepare(0, faultState, baseB, [entryB]);
            faultStore.Mode = mode;
            bool failed = false;
            try { failed = !faultAuthority.Commit(candidate, faultState); }
            catch (IOException) { failed = true; }
            faultStore.Mode = FaultMode.None;
            Pass(failed, "fault rejected: " + mode);
            Pass(faultAuthority.Load(0, baseA).Selected?.Generation == 1,
                "previous-good preserved after " + mode);
        }

        string persistence = File.ReadAllText(Path.Combine(repository,
            "apple-everest/runtime/AppleEverestModulePersistence.cs"));
        Pass(persistence.Contains("ApplicationSupportDirectory", StringComparison.Ordinal) &&
             persistence.Contains("CELESTE_IOS_STORAGE_ROOT", StringComparison.Ordinal) &&
             persistence.Contains("Path.Combine(root, \"Celeste\")", StringComparison.Ordinal) &&
             persistence.Contains("Everest/Slots", StringComparison.Ordinal) &&
             !persistence.Contains("Celeste/Everest/Slots", StringComparison.Ordinal) &&
             persistence.Contains("#if TVOS", StringComparison.Ordinal),
            "platform adapters share one logical authority");
        Pass(persistence.Contains("CelesteAppleEverest.Slot", StringComparison.Ordinal) &&
             !persistence.Contains("AppleEverest/ModuleSettings.v1", StringComparison.Ordinal),
            "slot state remains separate from global module settings");
        string closure = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/ClosureGenerator.cs"));
        Pass(closure.Contains("CaptureSave(SaveData.Instance.FileSlot, savingFileData)", StringComparison.Ordinal) &&
             closure.Contains("CommitCapturedSave()", StringComparison.Ordinal) &&
             closure.Contains("DiscardCapturedSave()", StringComparison.Ordinal),
            "module objects are snapshotted within the real vanilla save request");
        Pass(closure.Contains("appleEverestQueuedFile |= file", StringComparison.Ordinal) &&
             closure.Contains("appleEverestQueuedSettings |= settings", StringComparison.Ordinal) &&
             !closure.Contains("Queue<Tuple<bool, bool>>", StringComparison.Ordinal),
            "repeated save requests coalesce into one bounded latest-state follow-up");
        Pass(closure.Contains("byte[] appleEverestBaseSave = global::Celeste.Mod.AppleEverestProgressionPersistence.SerializeVanillaBase(saveData)", StringComparison.Ordinal) &&
             closure.Contains("AppleEverestProgressionPersistence.PreloadSlot(i, appleEverestBaseSave)", StringComparison.Ordinal) &&
             closure.Contains("AppleEverestModulePersistence.PreloadSlot(i, appleEverestBaseSave)", StringComparison.Ordinal) &&
             closure.Contains("AppleEverestProgressionPersistence.ActivateSlot(slot, appleEverestBaseSave)", StringComparison.Ordinal) &&
             closure.Contains("AppleEverestModulePersistence.ActivateSlot(slot, appleEverestBaseSave)", StringComparison.Ordinal) &&
             closure.Contains("ResetSessionForNewVanillaSession", StringComparison.Ordinal),
            "file-select, continuation and new-session lifecycle are distinct");
        Pass(closure.Contains("DeleteSlot(slot)", StringComparison.Ordinal) &&
             closure.Contains("NonPersistentModSession", StringComparison.Ordinal),
            "slot deletion is coupled while debug-map durability remains excluded");
        Pass(closure.Contains("PatchPinnedEverestCompatibility", StringComparison.Ordinal) &&
             closure.Contains("public Vector2 bounce", StringComparison.Ordinal) &&
             closure.Contains("public bool finished", StringComparison.Ordinal) &&
             closure.Contains("public string SID", StringComparison.Ordinal) &&
             closure.Contains("public Scene scene", StringComparison.Ordinal),
            "ordinary DeathMarkers binary receives only its reviewed pinned-Everest vanilla ABI");
        Pass(closure.Contains("module == \"DeathMarkers\" && property.Name == \"Mode\"", StringComparison.Ordinal) &&
             closure.Contains("if (!deaths.ContainsKey(sid)) deaths.Add(sid", StringComparison.Ordinal) &&
             closure.Contains("settings.Mode = next", StringComparison.Ordinal),
            "DeathMarkers live Mode changes establish the pinned setter's required empty area bucket");
        string staticRuntime = File.ReadAllText(Path.Combine(repository,
            "apple-everest/runtime/AppleEverestStaticRuntime.cs"));
        int staticState = staticRuntime.IndexOf("module.SetStaticState(loaded.Settings", StringComparison.Ordinal);
        int settingsRestore = staticRuntime.IndexOf(
            "AppleEverestSettingsPersistence.LoadAndApply", StringComparison.Ordinal);
        Pass(staticState >= 0 && settingsRestore > staticState &&
             staticRuntime.Contains("public static void CompleteStartup()", StringComparison.Ordinal) &&
             staticRuntime.Contains("Celeste.Instance?.scene == null", StringComparison.Ordinal) &&
             staticRuntime.Contains("!contentReady || !startupCompleted", StringComparison.Ordinal) &&
             closure.Contains("base.Update(gameTime);\\n\\t\\tglobal::Celeste.Mod.AppleEverestStaticRuntime.CompleteStartup();", StringComparison.Ordinal),
            "module static state exists before side-effectful settings restore, which waits for a live non-null scene");
        string api = File.ReadAllText(Path.Combine(repository,
            "apple-everest/runtime/EverestStaticApi.cs"));
        Pass(api.Contains("public virtual void CreateModMenuSection", StringComparison.Ordinal),
            "pinned Everest module menu override ABI remains linkable");

        using (JsonDocument audit = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(repository,
                   "apple-everest/module-durability-audit-stage25f.json"))))
        {
            JsonElement selected = audit.RootElement.GetProperty("selected");
            Pass(audit.RootElement.GetProperty("candidates").GetArrayLength() >= 10,
                "real public SaveData and Session audit covers at least ten releases");
            Pass(selected.GetProperty("name").GetString() == "DeathMarkers" &&
                 selected.GetProperty("version").GetString() == "2.0.0" &&
                 selected.GetProperty("zipSha256").GetString() ==
                    "94ad7d14fec6fb500f811ef09f46f008f444b45aafcdd8c2e8b86ce5d3ee6fc7" &&
                 selected.GetProperty("dllSha256").GetString() ==
                    "620e5b639b057a46890e7f0ed8a828fadb422a7b4adcd6b1c53bbb6568acdff8",
                "ordinary DeathMarkers release ZIP and precompiled DLL are pinned");
            Pass(selected.GetProperty("saveDataClass").GetString() == "DEFAULT_YAML_SAVEDATA_SUPPORTED" &&
                 selected.GetProperty("sessionClass").GetString() == "DEFAULT_YAML_SESSION_SUPPORTED" &&
                 selected.GetProperty("saveDataAsync").GetString() == "default-true",
                "selected fixture is the bounded pinned default-YAML async class");
            Pass(audit.RootElement.GetProperty("policy").GetProperty("sourceRequiredForProduction").GetBoolean() == false &&
                 audit.RootElement.GetProperty("policy").GetProperty("runtimeReflection").GetBoolean() == false,
                "production closure remains precompiled-binary-first and reflection-free");
        }

        return passed;
    }

    private static byte[] CorruptPayloadAndResign(byte[] encoded, byte[] payload)
    {
        byte[] result = (byte[])encoded.Clone();
        int index = IndexOf(result, payload);
        if (index < 0) throw new InvalidOperationException("test payload missing");
        result[index] ^= 1;
        int bodyLength = result.Length - 32;
        SHA256.HashData(result.AsSpan(0, bodyLength)).CopyTo(result, bodyLength);
        return result;
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int index = 0; index <= haystack.Length - needle.Length; index++)
            if (haystack.AsSpan(index, needle.Length).SequenceEqual(needle)) return index;
        return -1;
    }

    private static bool Same(DesktopState left, DesktopState right) =>
        right?.Deaths != null && left.Deaths.Count == right.Deaths.Count &&
        left.Deaths.All(pair => right.Deaths.TryGetValue(pair.Key, out List<DesktopDeath> values) &&
            pair.Value.Count == values.Count && pair.Value.Zip(values).All(items =>
                items.First.Room == items.Second.Room && items.First.Amount == items.Second.Amount &&
                items.First.Position.X == items.Second.Position.X && items.First.Position.Y == items.Second.Position.Y));

    public sealed class DesktopState
    {
        public Dictionary<string, List<DesktopDeath>> Deaths { get; set; } = new();
    }

    public sealed class DesktopDeath
    {
        public string Room { get; set; } = "";
        public DesktopVector Position { get; set; } = new();
        public int Amount { get; set; } = 1;
    }

    public sealed class DesktopVector
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    private enum FaultMode { None, ThrowBeforeWrite, Truncate, ThrowAfterWrite }

    private sealed class FakeReplicaStore : IAppleEverestModuleReplicaStore
    {
        private readonly Dictionary<string, byte[]> values = new(StringComparer.Ordinal);
        internal FaultMode Mode { get; set; }

        public byte[] Read(int slot, string replica) =>
            values.TryGetValue(slot + replica, out byte[] value) ? (byte[])value.Clone() : null;

        public void Write(int slot, string replica, byte[] logical)
        {
            if (Mode == FaultMode.ThrowBeforeWrite) throw new IOException("injected before write");
            byte[] stored = Mode == FaultMode.Truncate ? logical[..Math.Max(1, logical.Length / 2)] : logical;
            values[slot + replica] = (byte[])stored.Clone();
            if (Mode == FaultMode.ThrowAfterWrite) throw new IOException("injected after write");
        }

        public void Delete(int slot, string replica) => values.Remove(slot + replica);
    }
}
