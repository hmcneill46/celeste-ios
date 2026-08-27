#nullable disable
using System.Security.Cryptography;
using System.Text;
using AppleEverestBuilder;
using Celeste.Mod;

internal static class LevelSetProgressionTests
{
    internal static int Run(string repository, string temporary)
    {
        int passed = 0;
        void Pass(bool value, string name) { if (!value) throw new InvalidOperationException("FAIL: progression " + name); passed++; }
        byte[] hashA = SHA256.HashData(Encoding.UTF8.GetBytes("vanilla-a"));
        byte[] hashB = SHA256.HashData(Encoding.UTF8.GetBytes("vanilla-b"));
        byte[] lineage = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        string identity = new string('a', 64);
        AppleEverestProgressionMode mode = new(2, true, true, true, 7, 123456, 120000, 122000, 88, 4, true,
            [new("room-a", 4), new("room-b", 9)], ["room-b", "room-c"]);
        AppleEverestProgressionArea area = new("Fixture/Map", "Fixture", identity, true, [mode, EmptyMode(), EmptyMode()]);
        AppleEverestProgressionSession session = new("Fixture/Map", identity, 0, "room-c", true, 12.5f, 64,
            "room-b", 123456, true, 7, 88, 1, 2, true, false, true, true, false, "none", .1f, .2f, .75f,
            0, false, true, 2, true, false, false, "event:/music/test", [new("progress", 2)],
            "event:/env/test", [new("layer0", 1)], ["flag-b", "flag-a"], ["room-c"], [new("room-a", 4)],
            [new("room-b", 9)], [new("room-c", 1)], [new("counter", 3)], [true, false], false, "room-c", true,
            false, [mode, EmptyMode(), EmptyMode()]);
        AppleEverestProgressionSession suspendedSession = session with { Level = "room-suspended", RespawnX = 42f };
        AppleEverestProgressionSnapshot snapshot = new(0, 5, hashA, lineage, [area], session, [suspendedSession]);
        byte[] encoded = AppleEverestProgressionSnapshotCodec.Encode(snapshot);
        Pass(encoded.SequenceEqual(AppleEverestProgressionSnapshotCodec.Encode(snapshot)), "deterministic encoding");
        Pass(AppleEverestProgressionSnapshotCodec.TryDecode(encoded, 0, out AppleEverestProgressionSnapshot decoded) &&
             decoded.Generation == 5 && decoded.Areas[0].Modes[0].Deaths == 7 && decoded.Session.Level == "room-c" &&
             decoded.Session.MusicParameters[0].Key == "progress" && decoded.Session.OldStatsModes[0].Deaths == 7 &&
             decoded.SuspendedSessions.Length == 1 && decoded.SuspendedSessions[0].Level == "room-suspended" &&
             decoded.SuspendedSessions[0].RespawnX == 42f,
             "full AreaStats, active Session, and suspended collab Session round trip");
        Pass(!AppleEverestProgressionSnapshotCodec.TryDecode(encoded, 1, out _), "wrong slot rejected");
        Pass(!AppleEverestProgressionSnapshotCodec.TryDecode(encoded[..^1], 0, out _), "truncation rejected");
        byte[] wrongSchema = encoded.ToArray(); wrongSchema[0] ^= 1;
        Pass(!AppleEverestProgressionSnapshotCodec.TryDecode(wrongSchema, 0, out _), "schema mismatch rejected");
        byte[] corrupt = encoded.ToArray(); corrupt[20] ^= 1;
        Pass(!AppleEverestProgressionSnapshotCodec.TryDecode(corrupt, 0, out _), "aggregate corruption rejected");
        bool duplicate = false;
        try { _ = AppleEverestProgressionSnapshotCodec.Encode(snapshot with { Areas = [area, area] }); }
        catch (InvalidDataException) { duplicate = true; }
        Pass(duplicate, "duplicate SID rejected");
        bool wrongLineage = false;
        try { _ = AppleEverestProgressionSnapshotCodec.Encode(snapshot with { Lineage = [1] }); }
        catch (InvalidDataException) { wrongLineage = true; }
        Pass(wrongLineage, "invalid lineage rejected");
        bool duplicateSuspended = false;
        try { _ = AppleEverestProgressionSnapshotCodec.Encode(snapshot with { SuspendedSessions = [suspendedSession, suspendedSession] }); }
        catch (InvalidDataException) { duplicateSuspended = true; }
        Pass(duplicateSuspended, "duplicate suspended session SID rejected");

        byte[] compressed = AppleEverestProgressionCompression.Encode(encoded);
        Pass(AppleEverestProgressionCompression.TryDecode(compressed, out byte[] logical) && logical.SequenceEqual(encoded),
            "tvOS compression round trip");
        byte[] damagedCompression = compressed.ToArray(); damagedCompression[^1] ^= 1;
        Pass(!AppleEverestProgressionCompression.TryDecode(damagedCompression, out _), "tvOS compressed corruption rejected");
        Pass(!AppleEverestProgressionCompression.TryDecode(compressed[..^1], out _), "tvOS compressed truncation rejected");
        Pass(AppleEverestProgressionCompression.MaximumTotalReplicaBytes ==
             AppleEverestProgressionCompression.MaximumReplicaBytes * 6, "three-slot A/B budget explicit");

        Dictionary<string, string> maps = new(StringComparer.Ordinal) { [area.Sid] = identity };
        FakeStore store = new(); AppleEverestProgressionReplicaAuthority authority = new(store);
        AppleEverestProgressionReplicaState state = authority.Load(0, hashA, maps);
        Pass(state.Selected == null, "missing state defaults");
        var first = authority.Prepare(0, state, hashA, lineage, [area], session, [suspendedSession]);
        Pass(first.Replica == "A" && first.Snapshot.Generation == 1 && authority.Commit(first, state, maps), "first A commit");
        Pass(authority.Load(0, hashA, maps).Selected is { Generation: 1, SuspendedSessions.Length: 1 },
            "matching base restores active and suspended sessions");
        var second = authority.Prepare(0, state, hashB, lineage, [area], session with { Level = "room-d" });
        Pass(second.Replica == "B" && authority.Commit(second, state, maps), "second B commit");
        Pass(authority.Load(0, hashB, maps).Selected?.Session.Level == "room-d", "newest matching state restores");
        Pass(authority.Load(0, hashA, maps).Selected?.Generation == 1, "previous-good lineage restores with old base");
        Pass(authority.Load(0, SHA256.HashData(Encoding.UTF8.GetBytes("replacement")), maps).Selected == null,
            "replacement base cannot inherit progression");
        Pass(authority.Load(0, hashB, new Dictionary<string, string> { [area.Sid] = new string('b', 64) }).Selected == null,
            "changed map identity quarantines progression");
        Pass(authority.Load(0, hashB, new Dictionary<string, string>()).Selected == null,
            "removed map is fail-closed and remains recoverable on readdition");
        Pass(authority.Load(0, hashB, maps).Selected?.Generation == 2,
            "exact map readdition restores retained snapshot");

        string identityB = new string('c', 64);
        AppleEverestProgressionArea areaB = area with { Sid = "Second/Map", LevelSet = "Second", CompatibilityId = identityB };
        Dictionary<string, string> twoMaps = new(StringComparer.Ordinal) { [area.Sid] = identity, [areaB.Sid] = identityB };
        AppleEverestProgressionSnapshot twoMapSnapshot = snapshot with { Generation = 9, Areas = [area, areaB], Session = session };
        byte[] twoMapEncoded = AppleEverestProgressionSnapshotCodec.Encode(twoMapSnapshot);
        byte[] twoMapCompressed = AppleEverestProgressionCompression.Encode(twoMapEncoded);
        AppleEverestProgressionArea littleEpic = area with {
            Sid = "LittleEpic/precisionchallenge/precisionchallenge", LevelSet = "LittleEpic/precisionchallenge",
            CompatibilityId = "a341e6cbc2ff916c81b2717f24560b50bbb65b0a4326fb0ea31ed285841dc259"
        };
        AppleEverestProgressionArea fear = areaB with {
            Sid = "BevWeb/FearoftheDark/FearoftheDark", LevelSet = "BevWeb/FearoftheDark",
            CompatibilityId = "a7a63c0a036cda9dcbc16166afb06b1010ba714ef2b0f97b4054319dbf90ff5d"
        };
        AppleEverestProgressionArea torremolinosA = area with {
            Sid = "Xoa/Torremolinos Speedbuild/1", LevelSet = "Xoa/Torremolinos Speedbuild",
            CompatibilityId = "bb7febe30e78a2aad0f2b85709d624f37d63e9793dbbba89225d7193bc03c392"
        };
        AppleEverestProgressionArea torremolinosB = areaB with {
            Sid = "Xoa/Torremolinos Speedbuild/2", LevelSet = "Xoa/Torremolinos Speedbuild",
            CompatibilityId = "1166435f8b43fbd13843a84670733dc6f4bfb73e0095f8356b5457f87a6e3550"
        };
        AppleEverestProgressionSnapshot fourRealMapSnapshot = snapshot with {
            Generation = 10, Areas = [littleEpic, fear, torremolinosA, torremolinosB], Session = null
        };
        byte[] fourRealMapEncoded = AppleEverestProgressionSnapshotCodec.Encode(fourRealMapSnapshot);
        byte[] fourRealMapCompressed = AppleEverestProgressionCompression.Encode(fourRealMapEncoded);
        Dictionary<string, string> fourRealMaps = fourRealMapSnapshot.Areas.ToDictionary(
            value => value.Sid, value => value.CompatibilityId, StringComparer.Ordinal);
        Pass(AppleEverestProgressionReplicaAuthority.SelectMatching(
                 fourRealMapSnapshot, null, hashA, fourRealMaps)?.Areas.Length == 4,
            "LittleEpic Fear and both same-LevelSet maps share one bounded snapshot");
        Pass(fourRealMapCompressed.Length < AppleEverestProgressionCompression.MaximumReplicaBytes,
            "four-real-map tvOS snapshot remains within one replica");
        AppleEverestProgressionArea collabLobby = area with {
            Sid = "HennyburgrCompEntries/0-Lobbies/lobby", LevelSet = "HennyburgrCompEntries/0-Lobbies",
            CompatibilityId = "293cc93cdb340dd32eabcbaac4e96fb4504c57dac0b12334113f95e9712d012c"
        };
        AppleEverestProgressionArea collabMapA = areaB with {
            Sid = "HennyburgrCompEntries/1-Lobby/redboostercomp", LevelSet = "HennyburgrCompEntries/1-Lobby",
            CompatibilityId = "0a0a36f63a4294380c50ea94baee48191fddec96963f33f92685c80cd5cfb095"
        };
        AppleEverestProgressionArea collabMapB = area with {
            Sid = "HennyburgrCompEntries/1-Lobby/stationmovers", LevelSet = "HennyburgrCompEntries/1-Lobby",
            CompatibilityId = "419a5bae9425df172b32515079add4f89f0b1e81290d03f55f76cd6474bb077d"
        };
        AppleEverestProgressionSnapshot collabRealMapSnapshot = snapshot with {
            Generation = 11,
            Areas = [littleEpic, fear, torremolinosA, torremolinosB, collabLobby, collabMapA, collabMapB],
            Session = null
        };
        byte[] collabRealMapEncoded = AppleEverestProgressionSnapshotCodec.Encode(collabRealMapSnapshot);
        byte[] collabRealMapCompressed = AppleEverestProgressionCompression.Encode(collabRealMapEncoded);
        Dictionary<string, string> collabRealMaps = collabRealMapSnapshot.Areas.ToDictionary(
            value => value.Sid, value => value.CompatibilityId, StringComparer.Ordinal);
        Pass(AppleEverestProgressionReplicaAuthority.SelectMatching(
                 collabRealMapSnapshot, null, hashA, collabRealMaps)?.Areas.Length == 7,
            "four prior maps plus real collab lobby and both subordinate maps share one bounded snapshot");
        Pass(collabRealMapCompressed.Length < AppleEverestProgressionCompression.MaximumReplicaBytes,
            "real collab cumulative tvOS snapshot remains within one replica");
        Pass(AppleEverestProgressionReplicaAuthority.SelectMatching(snapshot, null, hashA, twoMaps)?.Areas.Length == 1,
            "old single-map sidecar remains valid after second map installation");
        Pass(AppleEverestProgressionReplicaAuthority.SelectMatching(twoMapSnapshot, null, hashA, twoMaps)?.Generation == 9,
            "two installed maps select one shared snapshot");
        Pass(AppleEverestProgressionReplicaAuthority.SelectMatching(twoMapSnapshot, null,
                 SHA256.HashData(Encoding.UTF8.GetBytes("two-map replacement")), twoMaps) == null,
            "imported replacement base cannot inherit two-map progression");
        Dictionary<string, string> firstMapOnly = new(StringComparer.Ordinal) { [area.Sid] = identity };
        AppleEverestProgressionSnapshot firstSelected = AppleEverestProgressionReplicaAuthority.SelectMatching(
            twoMapSnapshot, null, hashA, firstMapOnly);
        Pass(firstSelected?.Generation == 9, "removing second map does not hide first map state");
        AppleEverestProgressionArea refreshedA = area with { Modes = [mode with { Deaths = 11 }, EmptyMode(), EmptyMode()] };
        AppleEverestProgressionArea[] absentMerged = AppleEverestProgressionReplicaAuthority.MergeInstalledAreas(
            [refreshedA], firstSelected, firstMapOnly);
        Pass(absentMerged.Length == 2 && absentMerged.Single(value => value.Sid == areaB.Sid).CompatibilityId == identityB &&
             absentMerged.Single(value => value.Sid == area.Sid).Modes[0].Deaths == 11,
            "absent second-map state stays quarantined while installed map advances");
        Pass(AppleEverestProgressionReplicaAuthority.SelectMatching(
                 twoMapSnapshot with { Areas = absentMerged }, null, hashA, twoMaps)?.Areas.Any(value => value.Sid == areaB.Sid) == true,
            "exact second-map readdition restores its retained record");
        Dictionary<string, string> changedSecond = new(StringComparer.Ordinal) {
            [area.Sid] = identity, [areaB.Sid] = new string('d', 64)
        };
        AppleEverestProgressionArea changedAreaB = areaB with { CompatibilityId = changedSecond[areaB.Sid],
            Modes = [mode with { Deaths = 1 }, EmptyMode(), EmptyMode()] };
        AppleEverestProgressionArea[] changedMerged = AppleEverestProgressionReplicaAuthority.MergeInstalledAreas(
            [refreshedA, changedAreaB], twoMapSnapshot, changedSecond);
        Pass(changedMerged.Length == 2 && changedMerged.Single(value => value.Sid == areaB.Sid).CompatibilityId == changedSecond[areaB.Sid] &&
             changedMerged.Single(value => value.Sid == areaB.Sid).Modes[0].Deaths == 1,
            "same SID with changed content replaces rather than resurrects old state");
        Pass(AppleEverestProgressionReplicaAuthority.SelectMatching(
                 twoMapSnapshot, null, hashA, changedSecond)?.Areas.Single(value => value.Sid == area.Sid).CompatibilityId == identity,
            "changed second map cannot hide independent exact first-map state");

        MapProgressionRecord mapA = Map("Pack/MapA", "Pack", '1', identity, 2, true, false, true);
        MapProgressionRecord mapB = Map("Pack/MapB", "Pack", '2', identityB, 3, false, true, true);
        LevelSetProgressionRecord[] sameLevelSet = LevelSetProgressionManifest.Create([mapB, mapA]);
        Pass(sameLevelSet.Length == 1 && sameLevelSet[0].LevelSet == "Pack" &&
             sameLevelSet[0].MapSids.SequenceEqual(new[] { "Pack/MapA", "Pack/MapB" }, StringComparer.Ordinal),
            "same-LevelSet members are explicit and deterministically ordered");
        Pass(sameLevelSet[0].MaximumStrawberries == 5 && sameLevelSet[0].MaximumHearts == 1 &&
             sameLevelSet[0].MaximumCassettes == 1 && sameLevelSet[0].MaximumCompletions == 2,
            "same-LevelSet collectible and completion maxima aggregate once");
        Pass(LevelSetProgressionManifest.Create([mapA, mapB])[0].Identity == sameLevelSet[0].Identity,
            "runtime Area ordering cannot change LevelSet identity");
        MapProgressionRecord unrelated = Map("Elsewhere/Map", "Elsewhere", '3', new string('e', 64), 9, true, true, true);
        Pass(LevelSetProgressionManifest.Create([unrelated, mapB, mapA])
                 .Single(value => value.LevelSet == "Pack").Identity == sameLevelSet[0].Identity,
            "adding an unrelated LevelSet cannot invalidate existing identity");
        MapProgressionRecord changedMapB = mapB with { MapSha256 = new string('4', 64), CompatibilityId = new string('f', 64) };
        Pass(LevelSetProgressionManifest.Create([mapA, changedMapB])[0].Identity != sameLevelSet[0].Identity &&
             mapA.CompatibilityId == identity,
            "changing Map B changes its LevelSet aggregate but not Map A identity");
        Pass(LevelSetProgressionManifest.Create([mapA, mapB])[0].Identity == sameLevelSet[0].Identity,
            "exact Map B readdition restores the original LevelSet identity");
        Pass(LevelSetProgressionManifest.Text(sameLevelSet).StartsWith("APPLE_EVEREST_LEVELSET_MANIFEST_V1\nPack\t", StringComparison.Ordinal),
            "LevelSet manifest has a fixed schema and privacy-safe semantic identity");
        bool duplicateLevelSetSid = false;
        try { _ = LevelSetProgressionManifest.Create([mapA, mapA]); }
        catch (InvalidDataException) { duplicateLevelSetSid = true; }
        Pass(duplicateLevelSetSid, "duplicate map SID rejected by LevelSet authority");

        string mapDataFixture =
            "class MapData\n{\n\tvoid Load()\n\t{\n" + MapDataCompatibilityPatch.LookupTarget +
            "\n\t\t\t\t{\n\t\t\t\t}\n\t}\n\n\tpublic int[] GetStrawberries(out int total)\n\t{\n\t\ttotal = 0;\n\t\treturn null;\n\t}\n}";
        string patchedMapData = MapDataCompatibilityPatch.ApplySource(mapDataFixture);
        Pass(patchedMapData.Contains(
                 "AppleEverestNormalizeAndGet(ref ModeData.StrawberriesByCheckpoint, strawberry", StringComparison.Ordinal) &&
             patchedMapData.Contains("EntityData[,] expanded = new EntityData[y + 10, x + 25]", StringComparison.Ordinal),
            "MapData transform rewrites only the locked tracker block and emits bounded pinned-Everest normalization");
        bool duplicateMapDataTarget = false;
        try { _ = MapDataCompatibilityPatch.ApplySource(mapDataFixture + MapDataCompatibilityPatch.LookupTarget); }
        catch (InvalidDataException) { duplicateMapDataTarget = true; }
        Pass(duplicateMapDataTarget, "MapData transform fails closed on an ambiguous source shape");
        string[,] tracker = new string[10, 25];
        tracker[1, 2] = "preserved";
        string[,] originalTracker = tracker;
        var duplicateBerry = MapDataCompatibilityPatch.NormalizeAndGetForTest(ref tracker, 1, 2, 3);
        Pass(duplicateBerry.Checkpoint == 1 && duplicateBerry.Order == 0 && duplicateBerry.Existing == null &&
             ReferenceEquals(originalTracker, tracker), "duplicate explicit berry order is reassigned deterministically");
        var automatic = MapDataCompatibilityPatch.NormalizeAndGetForTest(ref tracker, -1, -1, 0);
        Pass(automatic.Checkpoint == 0 && automatic.Order == 0 && automatic.Existing == null,
            "negative automatic berry metadata is normalized to the start checkpoint");
        var grown = MapDataCompatibilityPatch.NormalizeAndGetForTest(ref tracker, 12, 30, 12);
        Pass(grown.Existing == null && tracker.GetLength(0) == 22 && tracker.GetLength(1) == 55 &&
             tracker[1, 2] == "preserved", "large valid tracker coordinates grow without losing existing entries");
        AppleEverestProgressionReplicaAuthority coldAuthority = new(store);
        Pass(coldAuthority.Load(0, hashB, maps).Selected?.Generation == 2,
            "cold initialization independently selects newest valid replica");
        Pass(new AppleEverestProgressionReplicaAuthority(store).Load(0, hashB, maps).Selected?.Session.Level == "room-d",
            "soft-reload model rehydrates session without process-global authority state");
        AppleEverestProgressionReplicaState slot1 = authority.Load(1, hashA, maps);
        Pass(authority.Commit(authority.Prepare(1, slot1, hashA, RandomNumberGenerator.GetBytes(32), [area], null), slot1, maps),
            "slot 1 independent commit");
        Pass(authority.Load(1, hashA, maps).Selected != null && authority.Load(2, hashA, maps).Selected == null,
            "three slots are isolated");
        authority.Delete(1);
        Pass(authority.Load(1, hashA, maps).Selected == null && authority.Load(0, hashB, maps).Selected != null,
            "delete removes only target slot and prevents recreation resurrection");
        AppleEverestProgressionReplicaState slot2 = authority.Load(2, hashA, twoMaps);
        Pass(authority.Commit(authority.Prepare(2, slot2, hashA, RandomNumberGenerator.GetBytes(32),
                 [area, areaB], session), slot2, twoMaps), "two-map slot commit before delete");
        authority.Delete(2);
        Pass(authority.Load(2, hashA, twoMaps).Selected == null,
            "delete removes both map records before slot recreation");

        FakeStore fault = new(); AppleEverestProgressionReplicaAuthority faultAuthority = new(fault);
        AppleEverestProgressionReplicaState faultState = faultAuthority.Load(0, hashA, maps);
        var good = faultAuthority.Prepare(0, faultState, hashA, lineage, [area], session);
        Pass(faultAuthority.Commit(good, faultState, maps), "fault baseline");
        foreach (Fault value in new[] { Fault.Before, Fault.Truncate, Fault.After })
        {
            var candidate = faultAuthority.Prepare(0, faultState, hashB, lineage, [area], session);
            fault.Mode = value; bool failed = false;
            try { failed = !faultAuthority.Commit(candidate, faultState, maps); } catch (IOException) { failed = true; }
            fault.Mode = Fault.None;
            Pass(failed, "fault rejected " + value);
            Pass(faultAuthority.Load(0, hashA, maps).Selected?.Generation == 1, "previous valid survives " + value);
        }
        fault.Corrupt(0, "A"); fault.Corrupt(0, "B");
        Pass(faultAuthority.Load(0, hashA, maps).Selected == null, "corrupt both replicas isolates progression");
        bool oversized = false;
        try { _ = AppleEverestProgressionSnapshotCodec.Encode(snapshot with {
            Areas = Enumerable.Range(0, AppleEverestProgressionSnapshotCodec.MaximumAreas + 1)
                .Select(index => area with { Sid = "Fixture/Map" + index }).ToArray() }); }
        catch (InvalidDataException) { oversized = true; }
        Pass(oversized, "oversized area census rejected");

        AppleEverestProgressionArea[] stressAreas = Enumerable.Range(0, 64).Select(index => new AppleEverestProgressionArea(
            (index < 32 ? "SetA/Map" : "SetB/Map") + index, index < 32 ? "SetA" : "SetB",
            SHA256.HashData(Encoding.UTF8.GetBytes("map" + index)).Aggregate("", (text, b) => text + b.ToString("x2")),
            index % 2 == 0, [new(128, index % 3 == 0, true, false, index * 10, index * 10000L, 0, 0, 100, 5,
                index % 4 == 0, Enumerable.Range(0, 128).Select(item => new AppleEverestProgressionEntityId("room" + item, item)).ToArray(),
                Enumerable.Range(0, 32).Select(item => "checkpoint" + item).ToArray()), EmptyMode(), EmptyMode()])).ToArray();
        Dictionary<string, string> stressMaps = stressAreas.ToDictionary(value => value.Sid, value => value.CompatibilityId, StringComparer.Ordinal);
        byte[] stress = AppleEverestProgressionSnapshotCodec.Encode(new(2, 1, hashA, lineage, stressAreas, null));
        byte[] stressCompressed = AppleEverestProgressionCompression.Encode(stress);
        Pass(stress.Length < AppleEverestProgressionSnapshotCodec.MaximumBytes, "realistic multi-map raw stress bounded");
        Pass(stressCompressed.Length < AppleEverestProgressionCompression.MaximumReplicaBytes, "tvOS multi-map stress compresses within one replica");
        Pass(AppleEverestProgressionReplicaAuthority.SelectMatching(
                 new(2, 1, hashA, lineage, stressAreas, null), null, hashA, stressMaps) != null,
            "two-LevelSet/multi-map generic identity model");
        Pass(stressAreas.Select(value => value.LevelSet).Distinct(StringComparer.Ordinal).Count() == 2,
            "stress fixture covers two explicit LevelSets");
        Console.WriteLine($"PROGRESSION_FIXTURE_RAW_BYTES={encoded.Length}");
        Console.WriteLine($"PROGRESSION_FIXTURE_TVOS_COMPRESSED_BYTES={compressed.Length}");
        Console.WriteLine($"PROGRESSION_TWO_MAP_RAW_BYTES={twoMapEncoded.Length}");
        Console.WriteLine($"PROGRESSION_TWO_MAP_TVOS_COMPRESSED_BYTES={twoMapCompressed.Length}");
        Console.WriteLine($"PROGRESSION_FOUR_REAL_MAP_RAW_BYTES={fourRealMapEncoded.Length}");
        Console.WriteLine($"PROGRESSION_FOUR_REAL_MAP_TVOS_COMPRESSED_BYTES={fourRealMapCompressed.Length}");
        Console.WriteLine($"PROGRESSION_COLLAB_CUMULATIVE_RAW_BYTES={collabRealMapEncoded.Length}");
        Console.WriteLine($"PROGRESSION_COLLAB_CUMULATIVE_TVOS_COMPRESSED_BYTES={collabRealMapCompressed.Length}");
        Console.WriteLine($"PROGRESSION_COLLAB_REPLICA_PERCENT={collabRealMapCompressed.Length * 100.0 / AppleEverestProgressionCompression.MaximumReplicaBytes:F2}");
        Console.WriteLine($"PROGRESSION_STRESS_RAW_BYTES={stress.Length}");
        Console.WriteLine($"PROGRESSION_STRESS_TVOS_COMPRESSED_BYTES={stressCompressed.Length}");
        Console.WriteLine($"PROGRESSION_TVOS_THREE_SLOT_AB_LIMIT_BYTES={AppleEverestProgressionCompression.MaximumTotalReplicaBytes}");

        string persistence = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/AppleEverestProgressionPersistence.cs"));
        string runtime = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/AppleEverestProgressionRuntime.cs"));
        string closure = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/ClosureGenerator.cs"));
        Pass(persistence.Contains("ApplicationSupportDirectory") && persistence.Contains("#if TVOS") &&
             persistence.Contains("Everest/Progression") && persistence.Contains(".Progression."), "shared model has narrow platform stores");
        Pass(runtime.Contains("SerializeVanillaBase") && runtime.Contains("save.Areas.RemoveRange") &&
             runtime.Contains("GeneratedAppleEverestProgressionManifest.Maps"), "vanilla serializer boundary strips projected state");
        int mapLoad = runtime.IndexOf("mode.MapData = new MapData(new AreaKey(id));", StringComparison.Ordinal);
        int authoredTotalRestore = runtime.IndexOf("mode.TotalStrawberries = descriptor.Strawberries;", StringComparison.Ordinal);
        Pass(mapLoad >= 0 && authoredTotalRestore > mapLoad,
            "compiler-authoritative authored berry totals survive the vanilla-only MapData scan");
        Pass(closure.Contains("AppleEverestProgressionPersistence.CaptureSave") &&
             closure.Contains("AppleEverestProgressionPersistence.CommitCapturedSave") &&
             closure.IndexOf("Save<SaveData>", StringComparison.Ordinal) < closure.IndexOf("AppleEverestProgressionPersistence.CommitCapturedSave", StringComparison.Ordinal),
            "progression commits only after vanilla save success");
        Pass(closure.Contains("AppleEverestProgressionPersistence.DeleteSlot") &&
             closure.Contains("AppleEverestProgressionPersistence.PreloadSlot"), "delete and preload are wired to numbered slots");
        string staticRuntime = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/AppleEverestStaticRuntime.cs"));
        Pass(runtime.Contains("Play Map:") == false && staticRuntime.Contains("LEVELSET: ") &&
             staticRuntime.Contains("Play Map: ") && staticRuntime.Contains("Play Static Mod Map (Debug):"),
            "grouped persistent LevelSet selector and explicit debug lane remain separate");
        Pass(!persistence.Contains("Documents", StringComparison.Ordinal) && !runtime.Contains("reflection", StringComparison.OrdinalIgnoreCase),
            "no Documents storage or runtime reflection serializer");
        string modules = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/AppleEverestModulePersistence.cs"));
        Pass(!modules.Contains("Progression", StringComparison.Ordinal) &&
             persistence.Contains("CelesteAppleEverest.Slot") && persistence.Contains("Everest/Progression"),
            "progression and module-state failure domains remain independent");
        return passed;
    }

    private static AppleEverestProgressionMode EmptyMode() => new(0, false, false, false, 0, 0, 0, 0, 0, 0, false,
        Array.Empty<AppleEverestProgressionEntityId>(), Array.Empty<string>());

    private static MapProgressionRecord Map(string sid, string levelSet, char source, string compatibility,
        int berries, bool heart, bool cassette, bool completion) =>
        new("Maps/" + sid + ".bin", sid, levelSet, new string(source, 64), compatibility,
            ["1", "2"], berries, heart, cassette, ["2"], ["strawberry"], [], ["A"], completion);

    private enum Fault { None, Before, Truncate, After }
    private sealed class FakeStore : IAppleEverestProgressionReplicaStore
    {
        private readonly Dictionary<string, byte[]> values = new(StringComparer.Ordinal);
        internal Fault Mode;
        public byte[] Read(int slot, string replica) => values.TryGetValue(slot + replica, out byte[] value) ? value.ToArray() : null;
        public void Write(int slot, string replica, byte[] logical)
        {
            if (Mode == Fault.Before) throw new IOException("before");
            values[slot + replica] = Mode == Fault.Truncate ? logical[..^1] : logical.ToArray();
            if (Mode == Fault.After) throw new IOException("after");
        }
        public void Delete(int slot, string replica) => values.Remove(slot + replica);
        internal void Corrupt(int slot, string replica)
        {
            if (values.TryGetValue(slot + replica, out byte[] value) && value.Length > 0) value[value.Length / 2] ^= 1;
        }
    }
}
