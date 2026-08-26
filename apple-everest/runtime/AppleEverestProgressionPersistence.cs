#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Celeste;
using Foundation;

namespace Celeste.Mod;

internal static class AppleEverestProgressionPersistence
{
    private const string DefaultsPrefix = "CelesteAppleEverest.Slot";
    private const string RelativeDirectory = "Everest/Progression";
    private static readonly object Gate = new();
    private static readonly PlatformReplicaStore Store = new();
    private static readonly AppleEverestProgressionReplicaAuthority Authority = new(Store);
    private static readonly AppleEverestProgressionReplicaState[] Slots = { new(), new(), new() };
    private static readonly Dictionary<string, AppleEverestProgressionSession>[] SuspendedSessions =
        { new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal) };
    private static AppleEverestProgressionPreparedWrite pending;

    internal static byte[] SerializeVanillaBase(SaveData value) => AppleEverestProgressionRuntime.SerializeVanillaBase(value);

    internal static void PreloadSlot(int slot, byte[] baseSave)
    {
        if (!Numbered(slot) || baseSave == null) return;
        try
        {
            byte[] hash = AppleEverestProgressionSnapshotCodec.BaseHash(baseSave);
            lock (Gate)
            {
                Slots[slot] = Authority.Load(slot, hash, AppleEverestProgressionRuntime.CompatibleMaps);
                RestoreSuspendedLocked(slot, Slots[slot].Selected);
            }
            AppleEverestStaticRuntime.Log($"levelset-progression=preloaded slot={slot} generation={Slots[slot].Selected?.Generation ?? 0} base-match={(Slots[slot].Selected != null).ToString().ToLowerInvariant()}");
        }
        catch (Exception exception)
        {
            lock (Gate)
            {
                Slots[slot] = new() { BaseHash = AppleEverestProgressionSnapshotCodec.BaseHash(baseSave), Preloaded = true };
                SuspendedSessions[slot].Clear();
            }
            AppleEverestStaticRuntime.Log($"levelset-progression=preload-failed slot={slot} category={exception.GetType().Name} action=isolated-defaults");
        }
    }

    internal static void ActivateSlot(int slot, byte[] baseSave, bool includeSession = true)
    {
        if (!Numbered(slot) || baseSave == null)
        {
            AppleEverestProgressionRuntime.ApplySnapshot(SaveData.Instance, null, includeSession);
            return;
        }
        byte[] hash = AppleEverestProgressionSnapshotCodec.BaseHash(baseSave);
        AppleEverestProgressionSnapshot selected;
        lock (Gate)
        {
            AppleEverestProgressionReplicaState state = Slots[slot];
            if (!state.Preloaded || state.BaseHash == null || !CryptographicOperations.FixedTimeEquals(state.BaseHash, hash))
            {
                Slots[slot] = state = Authority.Load(slot, hash, AppleEverestProgressionRuntime.CompatibleMaps);
                RestoreSuspendedLocked(slot, state.Selected);
            }
            selected = state.Selected;
        }
        AppleEverestProgressionRuntime.ApplySnapshot(SaveData.Instance, selected, includeSession);
    }

    internal static void CaptureSave(int slot, byte[] baseSave)
    {
        lock (Gate)
        {
            pending = null;
            if (!Numbered(slot) || baseSave == null || AppleEverestStaticRuntime.NonPersistentModSession ||
                !AppleEverestProgressionRuntime.HasMeaningfulState(SaveData.Instance, Slots[slot].Selected)) return;
            AppleEverestProgressionReplicaState state = Slots[slot];
            byte[] lineage = state.Selected?.Lineage?.ToArray() ?? RandomNumberGenerator.GetBytes(32);
            AppleEverestProgressionSession session = AppleEverestProgressionRuntime.CaptureSession(SaveData.Instance);
            AppleEverestProgressionSession[] suspended = CaptureSuspendedLocked(slot, state.Selected);
            if (session != null && suspended.Any(value => value.Sid == session.Sid &&
                                                          value.CompatibilityId == session.CompatibilityId))
                session = null;
            if (session == null && state.Selected?.Session != null &&
                !AppleEverestProgressionRuntime.CompatibleMaps.ContainsKey(state.Selected.Session.Sid) &&
                !suspended.Any(value => value.Sid == state.Selected.Session.Sid))
                session = state.Selected.Session;
            pending = Authority.Prepare(slot, state, AppleEverestProgressionSnapshotCodec.BaseHash(baseSave), lineage,
                AppleEverestProgressionRuntime.CaptureAreas(SaveData.Instance, state.Selected), session, suspended);
            AppleEverestStaticRuntime.Log($"levelset-progression=captured slot={slot} generation={pending.Snapshot.Generation} areas={pending.Snapshot.Areas.Length} suspended={suspended.Length} bytes={pending.Encoded.Length}");
        }
    }

    internal static bool CommitCapturedSave()
    {
        AppleEverestProgressionPreparedWrite write;
        lock (Gate) { write = pending; pending = null; }
        if (write == null) return true;
        try
        {
            lock (Gate)
                if (!Authority.Commit(write, Slots[write.Slot], AppleEverestProgressionRuntime.CompatibleMaps))
                    throw new IOException("Apple Everest progression read-back failed");
            AppleEverestStaticRuntime.Log($"levelset-progression=committed slot={write.Slot} replica={write.Replica} generation={write.Snapshot.Generation}");
            return true;
        }
        catch (Exception exception)
        {
            AppleEverestStaticRuntime.Log($"levelset-progression=commit-failed slot={write.Slot} category={exception.GetType().Name}");
            return false;
        }
    }

    internal static void DiscardCapturedSave() { lock (Gate) pending = null; }

    internal static bool SuspendCurrentSession()
    {
        SaveData save = SaveData.Instance;
        if (save == null || !Numbered(save.FileSlot)) return false;
        AppleEverestProgressionSession session = AppleEverestProgressionRuntime.CaptureSession(save);
        if (session == null) return false;
        lock (Gate) SuspendedSessions[save.FileSlot][session.Sid] = session;
        AppleEverestStaticRuntime.Log($"collab-session=suspended slot={save.FileSlot} sid={session.Sid} room={session.Level}");
        return true;
    }

    internal static bool HasSuspendedSession(string sid)
    {
        SaveData save = SaveData.Instance;
        if (save == null || !Numbered(save.FileSlot) || string.IsNullOrEmpty(sid)) return false;
        lock (Gate) return SuspendedSessions[save.FileSlot].ContainsKey(sid);
    }

    internal static bool TryTakeSuspendedSession(string sid, out Session session)
    {
        session = null;
        SaveData save = SaveData.Instance;
        if (save == null || !Numbered(save.FileSlot) || string.IsNullOrEmpty(sid)) return false;
        AppleEverestProgressionSession stored;
        lock (Gate)
        {
            if (!SuspendedSessions[save.FileSlot].Remove(sid, out stored)) return false;
        }
        session = AppleEverestProgressionRuntime.RestoreSession(stored);
        if (session != null)
        {
            AppleEverestStaticRuntime.Log($"collab-session=continued slot={save.FileSlot} sid={sid} room={session.Level}");
            return true;
        }
        lock (Gate) SuspendedSessions[save.FileSlot][sid] = stored;
        return false;
    }

    internal static void DiscardSuspendedSession(string sid)
    {
        SaveData save = SaveData.Instance;
        if (save == null || !Numbered(save.FileSlot) || string.IsNullOrEmpty(sid)) return;
        bool removed;
        lock (Gate) removed = SuspendedSessions[save.FileSlot].Remove(sid);
        if (removed) AppleEverestStaticRuntime.Log($"collab-session=discarded slot={save.FileSlot} sid={sid}");
    }

    internal static bool DeleteSlot(int slot)
    {
        if (!Numbered(slot)) return true;
        try
        {
            Authority.Delete(slot);
            lock (Gate)
            {
                Slots[slot] = new() { Preloaded = true };
                SuspendedSessions[slot].Clear();
                if (pending?.Slot == slot) pending = null;
            }
            AppleEverestStaticRuntime.Log($"levelset-progression=deleted slot={slot}"); return true;
        }
        catch (Exception exception)
        {
            AppleEverestStaticRuntime.Log($"levelset-progression=delete-failed slot={slot} category={exception.GetType().Name}"); return false;
        }
    }

    private sealed class PlatformReplicaStore : IAppleEverestProgressionReplicaStore
    {
        public byte[] Read(int slot, string replica)
        {
#if TVOS
            using NSString key = new(Key(slot, replica));
            byte[] envelope = NSUserDefaults.StandardUserDefaults.DataForKey(key)?.ToArray();
            return AppleEverestProgressionCompression.TryDecode(envelope, out byte[] logical) ? logical : null;
#else
            using NSData data = NSData.FromFile(FilePath(slot, replica, false)); return data?.ToArray();
#endif
        }
        public void Write(int slot, string replica, byte[] value)
        {
#if TVOS
            NSUserDefaults defaults = NSUserDefaults.StandardUserDefaults;
            using NSString key = new(Key(slot, replica)); using NSData data = NSData.FromArray(AppleEverestProgressionCompression.Encode(value));
            defaults.SetValueForKey(data, key); defaults.Synchronize();
#else
            using NSData data = NSData.FromArray(value); using NSUrl url = NSUrl.FromFilename(FilePath(slot, replica, true));
            if (!data.Save(url, true)) throw new IOException("atomic progression snapshot write failed");
#endif
        }
        public void Delete(int slot, string replica)
        {
#if TVOS
            NSUserDefaults defaults = NSUserDefaults.StandardUserDefaults; defaults.RemoveObject(Key(slot, replica)); defaults.Synchronize();
#else
            string path = FilePath(slot, replica, false); if (File.Exists(path)) File.Delete(path);
#endif
        }
    }

    private static string Key(int slot, string replica) => DefaultsPrefix + slot + ".Progression." + replica + ".v1";

    private static void RestoreSuspendedLocked(int slot, AppleEverestProgressionSnapshot selected)
    {
        Dictionary<string, AppleEverestProgressionSession> target = SuspendedSessions[slot];
        target.Clear();
        foreach (AppleEverestProgressionSession session in selected?.SuspendedSessions ?? Array.Empty<AppleEverestProgressionSession>())
            if (AppleEverestProgressionRuntime.CompatibleMaps.TryGetValue(session.Sid, out string identity) &&
                identity == session.CompatibilityId)
                target[session.Sid] = session;
    }

    private static AppleEverestProgressionSession[] CaptureSuspendedLocked(int slot, AppleEverestProgressionSnapshot selected)
    {
        Dictionary<string, AppleEverestProgressionSession> result = new(StringComparer.Ordinal);
        foreach (AppleEverestProgressionSession session in selected?.SuspendedSessions ?? Array.Empty<AppleEverestProgressionSession>())
            if (!AppleEverestProgressionRuntime.CompatibleMaps.ContainsKey(session.Sid))
                result[session.Sid] = session;
        foreach ((string sid, AppleEverestProgressionSession session) in SuspendedSessions[slot]) result[sid] = session;
        return result.Values.OrderBy(value => value.Sid, StringComparer.Ordinal).ToArray();
    }

    private static string FilePath(int slot, string replica, bool create)
    {
        string root = Environment.GetEnvironmentVariable("CELESTE_IOS_STORAGE_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            NSUrl[] urls = NSFileManager.DefaultManager.GetUrls(NSSearchPathDirectory.ApplicationSupportDirectory, NSSearchPathDomain.User);
            root = urls.FirstOrDefault()?.Path; if (!string.IsNullOrWhiteSpace(root)) root = Path.Combine(root, "Celeste");
        }
        if (string.IsNullOrWhiteSpace(root)) throw new IOException("Application Support is unavailable");
        string directory = Path.Combine(root, RelativeDirectory, slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (create) Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"levelset-state-v1.{replica.ToLowerInvariant()}.snapshot");
    }
    private static bool Numbered(int slot) => slot is >= 0 and <= 2;
}
