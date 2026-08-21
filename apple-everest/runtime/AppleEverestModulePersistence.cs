using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Foundation;

namespace Celeste.Mod;

internal static class AppleEverestModulePersistence
{
    private const string DefaultsPrefix = "CelesteAppleEverest.Slot";
    // CELESTE_IOS_STORAGE_ROOT is already the canonical
    // Library/Application Support/Celeste directory supplied by the iOS host.
    // The Foundation fallback starts one level higher at Application Support,
    // so it adds Celeste exactly once below.
    private const string RelativeDirectory = "Everest/Slots";
    private static readonly object Gate = new();
    private static readonly PlatformReplicaStore ReplicaStore = new();
    private static readonly AppleEverestModuleReplicaAuthority Authority = new(
        ReplicaStore, GeneratedAppleEverestModuleRegistry.DurabilityClosureSha256);
    private static readonly AppleEverestModuleReplicaState[] Slots = { new(), new(), new() };
    private static AppleEverestModulePreparedWrite pending;

    internal static void PreloadSlot(int slot, byte[] baseSave)
    {
        if (!Numbered(slot) || baseSave == null) return;
        try
        {
            byte[] hash = AppleEverestModuleSnapshotCodec.BaseHash(baseSave);
            lock (Gate)
            {
                Slots[slot] = Authority.Load(slot, hash);
                AppleEverestModuleReplicaState state = Slots[slot];
                AppleEverestStaticRuntime.Log($"module-data=preloaded slot={slot} generation={state.Selected?.Generation ?? 0} base-match={(state.Selected != null).ToString().ToLowerInvariant()}");
            }
        }
        catch (Exception exception)
        {
            lock (Gate) Slots[slot] = new AppleEverestModuleReplicaState
            {
                BaseHash = AppleEverestModuleSnapshotCodec.BaseHash(baseSave),
                Preloaded = true
            };
            AppleEverestStaticRuntime.Log($"module-data=preload-failed slot={slot} category={exception.GetType().Name} action=defaults");
        }
    }

    internal static void ActivateSaveData(int slot, byte[] baseSave) => Activate(slot, baseSave, includeSession: false);

    internal static void ActivateSlot(int slot, byte[] baseSave) => Activate(slot, baseSave, includeSession: true);

    private static void Activate(int slot, byte[] baseSave, bool includeSession)
    {
        if (!Numbered(slot) || baseSave == null)
        {
            AppleEverestStaticRuntime.ResetModuleData(slot, includeSession);
            return;
        }
        byte[] hash = AppleEverestModuleSnapshotCodec.BaseHash(baseSave);
        AppleEverestModuleSnapshot snapshot;
        lock (Gate)
        {
            AppleEverestModuleReplicaState state = Slots[slot];
            if (!state.Preloaded || state.BaseHash == null ||
                !CryptographicOperations.FixedTimeEquals(state.BaseHash, hash))
            {
                // This is a conservative cache-miss fallback for unusual direct
                // slot starts. The normal file-select path preloads on its
                // existing worker thread, keeping gameplay/main-menu IO-free.
                state = Authority.Load(slot, hash);
                Slots[slot] = state;
            }
            snapshot = state.Selected;
        }
        AppleEverestStaticRuntime.ApplyModuleSnapshot(slot, snapshot, includeSession);
    }

    internal static void CaptureSave(int slot, byte[] baseSave)
    {
        lock (Gate)
        {
            pending = null;
            if (!Numbered(slot) || baseSave == null || AppleEverestStaticRuntime.NonPersistentModSession)
            {
                AppleEverestStaticRuntime.Log($"module-data=capture-skipped slot={slot} nonpersistent={AppleEverestStaticRuntime.NonPersistentModSession.ToString().ToLowerInvariant()}");
                return;
            }
            AppleEverestModuleReplicaState state = Slots[slot];
            byte[] hash = AppleEverestModuleSnapshotCodec.BaseHash(baseSave);
            pending = Authority.Prepare(slot, state, hash,
                AppleEverestStaticRuntime.CaptureModuleSnapshotEntries(slot, state.Selected));
            AppleEverestStaticRuntime.Log($"module-data=captured slot={slot} generation={pending.Snapshot.Generation} modules={pending.Snapshot.Entries.Length} bytes={pending.Encoded.Length}");
        }
    }

    internal static bool CommitCapturedSave()
    {
        AppleEverestModulePreparedWrite write;
        lock (Gate)
        {
            write = pending;
            pending = null;
        }
        if (write == null) return true;
        try
        {
            lock (Gate)
            {
                if (!Authority.Commit(write, Slots[write.Slot]))
                    throw new IOException("Apple Everest module snapshot read-back failed");
            }
            AppleEverestStaticRuntime.Log($"module-data=committed slot={write.Slot} replica={write.Replica} generation={write.Snapshot.Generation}");
            return true;
        }
        catch (Exception exception)
        {
            AppleEverestStaticRuntime.Log($"module-data=commit-failed slot={write.Slot} category={exception.GetType().Name}");
            return false;
        }
    }

    internal static void DiscardCapturedSave()
    {
        lock (Gate) pending = null;
    }

    internal static bool DeleteSlot(int slot)
    {
        if (!Numbered(slot)) return true;
        try
        {
            Authority.Delete(slot);
            lock (Gate)
            {
                Slots[slot] = new AppleEverestModuleReplicaState { Preloaded = true };
                if (pending?.Slot == slot) pending = null;
            }
            AppleEverestStaticRuntime.Log($"module-data=deleted slot={slot}");
            return true;
        }
        catch (Exception exception)
        {
            AppleEverestStaticRuntime.Log($"module-data=delete-failed slot={slot} category={exception.GetType().Name}");
            return false;
        }
    }

    internal static void ResetSessionForNewVanillaSession(int slot)
    {
        if (Numbered(slot)) AppleEverestStaticRuntime.ResetModuleSessions(slot);
    }

    internal static AppleEverestModuleSnapshot SelectMatching(
        AppleEverestModuleSnapshot a, AppleEverestModuleSnapshot b, byte[] baseHash) =>
        AppleEverestModuleReplicaAuthority.SelectMatching(a, b, baseHash,
            GeneratedAppleEverestModuleRegistry.DurabilityClosureSha256);

    private sealed class PlatformReplicaStore : IAppleEverestModuleReplicaStore
    {
        public byte[] Read(int slot, string replica)
        {
#if TVOS
            using NSString key = new(Key(slot, replica));
            byte[] envelope = NSUserDefaults.StandardUserDefaults.DataForKey(key)?.ToArray();
            return AppleEverestModuleCompression.TryDecode(envelope, out byte[] logical) ? logical : null;
#else
            string path = FilePath(slot, replica, create: false);
            using NSData data = NSData.FromFile(path);
            return data?.ToArray();
#endif
        }

        public void Write(int slot, string replica, byte[] value)
        {
#if TVOS
            NSUserDefaults defaults = NSUserDefaults.StandardUserDefaults;
            using NSString key = new(Key(slot, replica));
            using NSData data = NSData.FromArray(AppleEverestModuleCompression.Encode(value));
            defaults.SetValueForKey(data, key);
            defaults.Synchronize();
#else
            string path = FilePath(slot, replica, create: true);
            using NSData data = NSData.FromArray(value);
            using NSUrl url = NSUrl.FromFilename(path);
            if (!data.Save(url, true)) throw new IOException("atomic module snapshot write failed");
#endif
        }

        public void Delete(int slot, string replica)
        {
#if TVOS
            NSUserDefaults defaults = NSUserDefaults.StandardUserDefaults;
            defaults.RemoveObject(Key(slot, replica));
            defaults.Synchronize();
#else
            string path = FilePath(slot, replica, create: false);
            if (File.Exists(path)) File.Delete(path);
#endif
        }
    }

    private static string Key(int slot, string replica) => DefaultsPrefix + slot + ".State." + replica + ".v1";

    private static string FilePath(int slot, string replica, bool create)
    {
        string root = Environment.GetEnvironmentVariable("CELESTE_IOS_STORAGE_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            NSUrl[] urls = NSFileManager.DefaultManager.GetUrls(
                NSSearchPathDirectory.ApplicationSupportDirectory, NSSearchPathDomain.User);
            root = urls.FirstOrDefault()?.Path;
            if (!string.IsNullOrWhiteSpace(root)) root = Path.Combine(root, "Celeste");
        }
        if (string.IsNullOrWhiteSpace(root)) throw new IOException("Application Support is unavailable");
        string directory = Path.Combine(root, RelativeDirectory,
            slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (create) Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"module-state-v1.{replica.ToLowerInvariant()}.snapshot");
    }

    private static bool Numbered(int slot) => slot is >= 0 and <= 2;
}
