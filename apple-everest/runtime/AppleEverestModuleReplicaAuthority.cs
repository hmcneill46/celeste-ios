#nullable disable
using System;
using System.Linq;
using System.Security.Cryptography;

namespace Celeste.Mod;

internal interface IAppleEverestModuleReplicaStore
{
    byte[] Read(int slot, string replica);
    void Write(int slot, string replica, byte[] logical);
    void Delete(int slot, string replica);
}

internal sealed class AppleEverestModuleReplicaState
{
    internal byte[] BaseHash;
    internal AppleEverestModuleSnapshot Selected;
    internal long GenerationA;
    internal long GenerationB;
    internal bool Preloaded;
}

internal sealed record AppleEverestModulePreparedWrite(
    int Slot,
    string Replica,
    byte[] Encoded,
    AppleEverestModuleSnapshot Snapshot);

// Shared transaction/recovery policy. Platform adapters only provide bounded
// replica bytes; selection, base-save binding and verification stay identical
// on iOS and tvOS and are deterministically fault-injectable on the host.
internal sealed class AppleEverestModuleReplicaAuthority
{
    private readonly IAppleEverestModuleReplicaStore store;
    private readonly string staticClosureSha256;

    internal AppleEverestModuleReplicaAuthority(IAppleEverestModuleReplicaStore store,
        string staticClosureSha256)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.staticClosureSha256 = string.IsNullOrEmpty(staticClosureSha256)
            ? throw new ArgumentException("static closure identity is required", nameof(staticClosureSha256))
            : staticClosureSha256;
    }

    internal AppleEverestModuleReplicaState Load(int slot, byte[] baseHash)
    {
        AppleEverestModuleSnapshot a = Decode(store.Read(slot, "A"), slot);
        AppleEverestModuleSnapshot b = Decode(store.Read(slot, "B"), slot);
        return new AppleEverestModuleReplicaState
        {
            GenerationA = a?.Generation ?? 0,
            GenerationB = b?.Generation ?? 0,
            BaseHash = baseHash,
            Selected = SelectMatching(a, b, baseHash, staticClosureSha256),
            Preloaded = true
        };
    }

    internal AppleEverestModulePreparedWrite Prepare(int slot,
        AppleEverestModuleReplicaState state, byte[] baseHash,
        AppleEverestModuleSnapshotEntry[] entries)
    {
        long generation = Math.Max(state?.GenerationA ?? 0, state?.GenerationB ?? 0) + 1;
        AppleEverestModuleSnapshot snapshot = new(slot, generation, baseHash,
            staticClosureSha256, entries);
        string replica = (state?.GenerationA ?? 0) <= (state?.GenerationB ?? 0) ? "A" : "B";
        return new AppleEverestModulePreparedWrite(slot, replica,
            AppleEverestModuleSnapshotCodec.Encode(snapshot), snapshot);
    }

    internal bool Commit(AppleEverestModulePreparedWrite write,
        AppleEverestModuleReplicaState state)
    {
        store.Write(write.Slot, write.Replica, write.Encoded);
        AppleEverestModuleSnapshot decoded = Decode(store.Read(write.Slot, write.Replica), write.Slot);
        if (decoded == null || decoded.Generation != write.Snapshot.Generation ||
            decoded.StaticClosureSha256 != staticClosureSha256 ||
            !CryptographicOperations.FixedTimeEquals(decoded.BaseSaveSha256,
                write.Snapshot.BaseSaveSha256) ||
            !CryptographicOperations.FixedTimeEquals(
                System.Security.Cryptography.SHA256.HashData(write.Encoded),
                System.Security.Cryptography.SHA256.HashData(
                    AppleEverestModuleSnapshotCodec.Encode(decoded))))
            return false;
        if (write.Replica == "A") state.GenerationA = decoded.Generation;
        else state.GenerationB = decoded.Generation;
        state.BaseHash = decoded.BaseSaveSha256;
        state.Selected = decoded;
        state.Preloaded = true;
        return true;
    }

    internal void Delete(int slot)
    {
        store.Delete(slot, "A");
        store.Delete(slot, "B");
    }

    internal static AppleEverestModuleSnapshot SelectMatching(
        AppleEverestModuleSnapshot a,
        AppleEverestModuleSnapshot b,
        byte[] baseHash,
        string staticClosureSha256)
    {
        return new[] { a, b }.Where(item => item != null && baseHash != null &&
                item.StaticClosureSha256 == staticClosureSha256 &&
                CryptographicOperations.FixedTimeEquals(item.BaseSaveSha256, baseHash))
            .OrderByDescending(item => item.Generation).FirstOrDefault();
    }

    private static AppleEverestModuleSnapshot Decode(byte[] value, int slot) =>
        AppleEverestModuleSnapshotCodec.TryDecode(value, slot, out AppleEverestModuleSnapshot snapshot)
            ? snapshot
            : null;
}
