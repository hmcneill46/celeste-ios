using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FMOD;
using FMOD.Studio;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestCustomAudioGuidDescriptor
{
    internal Guid Id { get; }
    internal string Path { get; }
    internal string Kind { get; }

    internal AppleEverestCustomAudioGuidDescriptor(Guid id, string path, string kind)
    {
        Id = id;
        Path = path;
        Kind = kind;
    }
}

internal sealed class AppleEverestCustomBankDescriptor
{
    internal string Owner { get; }
    internal string Version { get; }
    internal int LoadOrdinal { get; }
    internal string ResourcePath { get; }
    internal string BankSha256 { get; }
    internal Guid BankId { get; }
    internal string BankPath { get; }
    internal AppleEverestCustomAudioGuidDescriptor[] Guids { get; }

    internal AppleEverestCustomBankDescriptor(string owner, string version, int loadOrdinal,
        string resourcePath, string bankSha256, Guid bankId, string bankPath,
        AppleEverestCustomAudioGuidDescriptor[] guids)
    {
        Owner = owner;
        Version = version;
        LoadOrdinal = loadOrdinal;
        ResourcePath = resourcePath;
        BankSha256 = bankSha256;
        BankId = bankId;
        BankPath = bankPath;
        Guids = guids;
    }
}

/// <summary>
/// Loads the generated, graph-owned FMOD bank set into Celeste's one existing
/// Studio System. Discovery and GUID parsing happen on the Mac; device code
/// consumes only this bounded typed manifest and exact bundle paths.
/// </summary>
internal static class AppleEverestCustomAudioRuntime
{
    private sealed class LoadedBank
    {
        internal AppleEverestCustomBankDescriptor Descriptor;
        internal Bank Handle;
        internal AppleEverestCustomBankState State;
    }

    private static readonly List<LoadedBank> Banks = new();
    private static readonly Dictionary<string, Guid> Events = new(StringComparer.Ordinal);
    private static readonly Dictionary<Guid, string> EventNames = new();
    private static readonly HashSet<string> ObservedEvents = new(StringComparer.Ordinal);
    private static readonly HashSet<Guid> ObservedNames = new();
    private static readonly AppleEverestCustomAudioLifecycle Lifecycle = new();

    internal static void Load(FMOD.Studio.System system)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        AppleEverestCustomBankDescriptor[] descriptors = GeneratedAppleEverestCustomAudioManifest.Banks;
        if (descriptors.Length == 0) return;
        if (!Lifecycle.BeginLoad(system, descriptors.Length))
        {
            AppleEverestStaticRuntime.Log($"custom-audio=already-loaded banks={Banks.Count} duplicate-load=false");
            return;
        }
        if (Banks.Count != 0 || Events.Count != 0 || EventNames.Count != 0)
            throw new InvalidOperationException("custom FMOD registry belongs to another live Studio System");
        try
        {
            for (int descriptorIndex = 0; descriptorIndex < descriptors.Length; descriptorIndex++)
            {
                AppleEverestCustomBankDescriptor descriptor = descriptors[descriptorIndex];
                int expectedOrdinal = 8 + descriptorIndex;
                if (descriptor.LoadOrdinal != expectedOrdinal)
                    throw new InvalidOperationException($"custom FMOD load ordinal mismatch: expected={expectedOrdinal}; actual={descriptor.LoadOrdinal}");
                string path = Path.Combine(Engine.ContentDirectory,
                    descriptor.ResourcePath.Replace('/', Path.DirectorySeparatorChar));
                RESULT result = system.loadBankFile(path, LOAD_BANK_FLAGS.NORMAL, out Bank bank);
                if (result != RESULT.OK)
                    throw new InvalidOperationException($"custom FMOD loadBankFile failed: owner={descriptor.Owner}; result={result}");
                LoadedBank loaded = new() { Descriptor = descriptor, Handle = bank,
                    State = AppleEverestCustomBankState.Loaded };
                Banks.Add(loaded);
                Require(bank.getID(out Guid bankId), descriptor.Owner, "getID");
                if (bankId != descriptor.BankId)
                    throw new InvalidOperationException($"custom FMOD bank GUID mismatch: owner={descriptor.Owner}");
                if (descriptor.Owner == "CollabUtils2")
                {
                    // The export also contains vanilla bus/snapshot references.
                    // Enumerate actual bank members rather than treating every
                    // embedded GUID byte sequence as an event definition.
                    Require(bank.getEventList(out EventDescription[] members), descriptor.Owner, "bank.getEventList");
                    HashSet<Guid> actualEvents = new();
                    int actualSnapshots = 0;
                    foreach (EventDescription member in members)
                    {
                        Require(member.isSnapshot(out bool snapshot), descriptor.Owner, "event.isSnapshot");
                        Require(member.getID(out Guid id), descriptor.Owner, "event.getID");
                        if (snapshot) actualSnapshots++;
                        else if (!actualEvents.Add(id)) throw new InvalidOperationException("duplicate Collab bank event");
                    }
                    if (!actualEvents.SetEquals(descriptor.Guids.Where(g => g.Kind == "event").Select(g => g.Id)))
                        throw new InvalidOperationException("Collab bank actual event membership differs from its pinned event set");
                    Require(bank.getBusCount(out int actualBuses), descriptor.Owner, "bank.getBusCount");
                    Require(bank.getVCACount(out int actualVcas), descriptor.Owner, "bank.getVCACount");
                    AppleEverestStaticRuntime.Log($"custom-bank-members=PASS owner=CollabUtils2 events={actualEvents.Count} snapshots={actualSnapshots} buses={actualBuses} vcas={actualVcas} authority=native-bank-enumeration");
                }

                // This exact ChronoHelper release ships a bank plus the FMOD
                // Studio GUID export, but no companion strings bank. FMOD can
                // therefore validate the loaded bank/event GUIDs while reverse
                // path lookup (Bank.getPath) correctly returns
                // ERR_EVENT_NOTFOUND. The Mac-side exact manifest binds each
                // path to its GUID; device validation proves those GUIDs are
                // present in the bytes actually loaded into the existing
                // Studio System.

                foreach (AppleEverestCustomAudioGuidDescriptor entry in descriptor.Guids)
                {
                    if (entry.Kind != "event") continue;
                    Require(system.getEventByID(entry.Id, out EventDescription description), descriptor.Owner,
                        "getEventByID:" + entry.Path);
                    Require(description.getID(out Guid eventId), descriptor.Owner, "event.getID:" + entry.Path);
                    if (eventId != entry.Id)
                        throw new InvalidOperationException($"custom FMOD event GUID mismatch: path={entry.Path}");
                    if (Events.TryGetValue(entry.Path, out Guid existing) && existing != entry.Id)
                        throw new InvalidOperationException($"custom FMOD event path collision: path={entry.Path}");
                    if (EventNames.TryGetValue(entry.Id, out string existingName) && existingName != entry.Path)
                        throw new InvalidOperationException("custom FMOD event GUID has conflicting paths");
                    Events[entry.Path] = entry.Id;
                    EventNames[entry.Id] = entry.Path;
                }
                int events = descriptor.Guids.Count(entry => entry.Kind == "event");
                int buses = descriptor.Guids.Count(entry => entry.Kind == "bus");
                int vcas = descriptor.Guids.Count(entry => entry.Kind == "vca");
                int snapshots = descriptor.Guids.Count(entry => entry.Kind == "snapshot");
                AppleEverestStaticRuntime.Log($"custom-bank=PASS owner={descriptor.Owner} ordinal={descriptor.LoadOrdinal} path={descriptor.BankPath} events={events} buses={buses} vcas={vcas} snapshots={snapshots} identity=guid-manifest state=LOADED");
            }
            Lifecycle.CompleteLoad(system, Banks.Count);
            AppleEverestStaticRuntime.Log($"custom-audio=PASS schema={GeneratedAppleEverestCustomAudioManifest.Schema} banks={Banks.Count} events={Events.Count} existing-studio-system=true");
        }
        catch
        {
            for (int index = Banks.Count - 1; index >= 0; index--)
            {
                if (Banks[index].State != AppleEverestCustomBankState.Loaded) continue;
                _ = Banks[index].Handle.unload();
                Banks[index].State = AppleEverestCustomBankState.Unloaded;
            }
            Banks.Clear();
            Events.Clear();
            EventNames.Clear();
            ObservedNames.Clear();
            Lifecycle.FailLoad(system);
            throw;
        }
    }

    internal static bool TryGetEventDescription(FMOD.Studio.System system, string path,
        out EventDescription description)
    {
        description = null;
        if (!Lifecycle.Owns(system) || !Events.TryGetValue(path, out Guid id)) return false;
        RESULT result = system.getEventByID(id, out description);
        if (result != RESULT.OK)
            throw new InvalidOperationException($"custom FMOD event lookup failed: path={path}; result={result}");
        return true;
    }

    internal static void RecordEventRequest(string path, EventInstance instance)
    {
        if (path == null || !Events.ContainsKey(path)) return;
        if (instance == null) throw new InvalidOperationException("custom FMOD event produced no instance: " + path);
        if (ObservedEvents.Add(path))
            AppleEverestStaticRuntime.Log($"custom-event=PASS path={path} instance-created=true ordinary-audio-path=true");
    }

    internal static string GetEventName(FMOD.Studio.System system, EventInstance instance)
    {
        if (instance == null) return "";
        Require(instance.getDescription(out EventDescription description), "event-name", "getDescription");
        if (description == null) throw new InvalidOperationException("FMOD event has no description");
        Require(description.getID(out Guid id), "event-name", "getID");
        string registered = null;
        bool known = Lifecycle.Owns(system) && EventNames.TryGetValue(id, out registered);
        RESULT result = description.getPath(out string path);
        if (result == RESULT.OK)
        {
            if (string.IsNullOrEmpty(path) || (known && path != registered))
                throw new InvalidOperationException("FMOD event name differs from its validated identity");
            return path;
        }
        // Pinned stringless banks provide an event GUID but no native reverse
        // path. Use only the exact path validated while loading those banks
        // into this Studio System, as the pinned Everest GUID cache does.
        if (result == RESULT.ERR_EVENT_NOTFOUND && known)
        {
            if (ObservedNames.Add(id))
                AppleEverestStaticRuntime.Log($"custom-event-name=PASS path={registered} identity=guid-manifest native-result={result}");
            return registered;
        }
        Require(result, "event-name", "getPath");
        throw new InvalidOperationException("FMOD event name could not be resolved");
    }

    internal static void BeforeSystemUnload(FMOD.Studio.System system)
    {
        if (!Lifecycle.Owns(system) && Banks.Count == 0) return;
        // Celeste immediately calls Studio.System.unloadAll(), which owns bank
        // destruction. Drop managed handles first; do not double-unload them.
        foreach (LoadedBank bank in Banks) bank.State = AppleEverestCustomBankState.Unloaded;
        int count = Lifecycle.BeforeSystemUnload(system);
        Banks.Clear();
        Events.Clear();
        EventNames.Clear();
        ObservedEvents.Clear();
        ObservedNames.Clear();
        AppleEverestStaticRuntime.Log($"custom-audio=teardown banks={count} policy=system-unloadAll");
    }

    private static void Require(RESULT result, string owner, string operation)
    {
        if (result != RESULT.OK)
            throw new InvalidOperationException($"custom FMOD validation failed: owner={owner}; operation={operation}; result={result}");
    }
}
