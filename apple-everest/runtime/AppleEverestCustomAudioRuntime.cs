using System;
using System.Collections.Generic;
using System.IO;
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
    private static readonly HashSet<string> ObservedEvents = new(StringComparer.Ordinal);
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
        if (Banks.Count != 0 || Events.Count != 0)
            throw new InvalidOperationException("custom FMOD registry belongs to another live Studio System");
        try
        {
            foreach (AppleEverestCustomBankDescriptor descriptor in descriptors)
            {
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
                    Events.Add(entry.Path, entry.Id);
                }
                AppleEverestStaticRuntime.Log($"custom-bank=PASS owner={descriptor.Owner} ordinal={descriptor.LoadOrdinal} path={descriptor.BankPath} events={descriptor.Guids.Length - 1} identity=guid-manifest state=LOADED");
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

    internal static void BeforeSystemUnload(FMOD.Studio.System system)
    {
        if (!Lifecycle.Owns(system) && Banks.Count == 0) return;
        // Celeste immediately calls Studio.System.unloadAll(), which owns bank
        // destruction. Drop managed handles first; do not double-unload them.
        foreach (LoadedBank bank in Banks) bank.State = AppleEverestCustomBankState.Unloaded;
        int count = Lifecycle.BeforeSystemUnload(system);
        Banks.Clear();
        Events.Clear();
        ObservedEvents.Clear();
        AppleEverestStaticRuntime.Log($"custom-audio=teardown banks={count} policy=system-unloadAll");
    }

    private static void Require(RESULT result, string owner, string operation)
    {
        if (result != RESULT.OK)
            throw new InvalidOperationException($"custom FMOD validation failed: owner={owner}; operation={operation}; result={result}");
    }
}
