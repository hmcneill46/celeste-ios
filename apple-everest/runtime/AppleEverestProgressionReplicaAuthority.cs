#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Celeste.Mod;

internal interface IAppleEverestProgressionReplicaStore
{
    byte[] Read(int slot, string replica);
    void Write(int slot, string replica, byte[] logical);
    void Delete(int slot, string replica);
}

internal sealed class AppleEverestProgressionReplicaState
{
    internal byte[] BaseHash;
    internal AppleEverestProgressionSnapshot Selected;
    internal long GenerationA;
    internal long GenerationB;
    internal bool Preloaded;
}

internal sealed record AppleEverestProgressionPreparedWrite(
    int Slot, string Replica, byte[] Encoded, AppleEverestProgressionSnapshot Snapshot);

internal sealed class AppleEverestProgressionReplicaAuthority
{
    private readonly IAppleEverestProgressionReplicaStore store;

    internal AppleEverestProgressionReplicaAuthority(IAppleEverestProgressionReplicaStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    internal AppleEverestProgressionReplicaState Load(int slot, byte[] baseHash,
        IReadOnlyDictionary<string, string> compatibleMaps)
    {
        AppleEverestProgressionSnapshot a = Decode(store.Read(slot, "A"), slot);
        AppleEverestProgressionSnapshot b = Decode(store.Read(slot, "B"), slot);
        return new()
        {
            GenerationA = a?.Generation ?? 0, GenerationB = b?.Generation ?? 0,
            BaseHash = baseHash, Selected = SelectMatching(a, b, baseHash, compatibleMaps), Preloaded = true
        };
    }

    internal AppleEverestProgressionPreparedWrite Prepare(int slot, AppleEverestProgressionReplicaState state,
        byte[] baseHash, byte[] lineage, AppleEverestProgressionArea[] areas, AppleEverestProgressionSession session,
        AppleEverestProgressionSession[] suspendedSessions = null)
    {
        long generation = Math.Max(state?.GenerationA ?? 0, state?.GenerationB ?? 0) + 1;
        AppleEverestProgressionSnapshot snapshot = new(slot, generation, baseHash, lineage, areas, session, suspendedSessions);
        string replica = (state?.GenerationA ?? 0) <= (state?.GenerationB ?? 0) ? "A" : "B";
        return new(slot, replica, AppleEverestProgressionSnapshotCodec.Encode(snapshot), snapshot);
    }

    internal bool Commit(AppleEverestProgressionPreparedWrite write, AppleEverestProgressionReplicaState state,
        IReadOnlyDictionary<string, string> compatibleMaps)
    {
        store.Write(write.Slot, write.Replica, write.Encoded);
        AppleEverestProgressionSnapshot decoded = Decode(store.Read(write.Slot, write.Replica), write.Slot);
        if (decoded == null || decoded.Generation != write.Snapshot.Generation ||
            !CryptographicOperations.FixedTimeEquals(decoded.BaseSaveSha256, write.Snapshot.BaseSaveSha256) ||
            SelectMatching(decoded, null, decoded.BaseSaveSha256, compatibleMaps) == null ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(write.Encoded),
                SHA256.HashData(AppleEverestProgressionSnapshotCodec.Encode(decoded)))) return false;
        if (write.Replica == "A") state.GenerationA = decoded.Generation; else state.GenerationB = decoded.Generation;
        state.BaseHash = decoded.BaseSaveSha256; state.Selected = decoded; state.Preloaded = true;
        return true;
    }

    internal void Delete(int slot) { store.Delete(slot, "A"); store.Delete(slot, "B"); }

    internal static AppleEverestProgressionSnapshot SelectMatching(AppleEverestProgressionSnapshot a,
        AppleEverestProgressionSnapshot b, byte[] baseHash, IReadOnlyDictionary<string, string> compatibleMaps) =>
        new[] { a, b }.Where(item => item != null && baseHash != null &&
                CryptographicOperations.FixedTimeEquals(item.BaseSaveSha256, baseHash) && Compatible(item, compatibleMaps))
            .OrderByDescending(item => item.Generation).FirstOrDefault();

    // A snapshot is selected when at least one currently installed map has an
    // exact identity match. Records for absent maps remain quarantined inside
    // the validated snapshot; projection applies only exact installed records.
    // This lets independently installed LevelSets survive removal/re-addition
    // without allowing changed content at the same SID to consume old state.
    private static bool Compatible(AppleEverestProgressionSnapshot value, IReadOnlyDictionary<string, string> maps) =>
        maps != null && (value.Areas.Any(area => maps.TryGetValue(area.Sid, out string identity) &&
                                                identity == area.CompatibilityId) ||
                         value.Session != null && maps.TryGetValue(value.Session.Sid, out string sessionIdentity) &&
                                                  sessionIdentity == value.Session.CompatibilityId ||
                         (value.SuspendedSessions ?? Array.Empty<AppleEverestProgressionSession>()).Any(session =>
                             maps.TryGetValue(session.Sid, out string suspendedIdentity) &&
                             suspendedIdentity == session.CompatibilityId));

    internal static AppleEverestProgressionArea[] MergeInstalledAreas(
        IEnumerable<AppleEverestProgressionArea> installed,
        AppleEverestProgressionSnapshot selected,
        IReadOnlyDictionary<string, string> compatibleMaps)
    {
        Dictionary<string, AppleEverestProgressionArea> merged = (installed ?? Array.Empty<AppleEverestProgressionArea>())
            .ToDictionary(area => area.Sid, StringComparer.Ordinal);
        if (selected?.Areas != null)
            foreach (AppleEverestProgressionArea area in selected.Areas)
                if (compatibleMaps != null && !compatibleMaps.ContainsKey(area.Sid) && !merged.ContainsKey(area.Sid))
                    merged.Add(area.Sid, area);
        return merged.Values.OrderBy(area => area.Sid, StringComparer.Ordinal).ToArray();
    }

    private static AppleEverestProgressionSnapshot Decode(byte[] bytes, int slot) =>
        AppleEverestProgressionSnapshotCodec.TryDecode(bytes, slot, out AppleEverestProgressionSnapshot value) ? value : null;
}
