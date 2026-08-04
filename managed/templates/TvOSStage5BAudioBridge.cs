#if TVOS_REAL_AUDIO
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FMOD;
using FMOD.Studio;
using Monocle;

namespace Celeste;

// Bounded evidence and lifecycle support for the real Stage 5B Celeste audio
// lane. This class observes the original generated FMOD API; it is not a
// second wrapper and does not provide or emulate any native symbol.
public static class TvOSStage5BAudioBridge
{
    private const uint ExpectedNativeVersion = 0x00011009;
    private const uint ManagedHeaderVersion = 0x00011014;
    private static readonly long StartedAt = Stopwatch.GetTimestamp();
    private static readonly object Gate = new();
    private static readonly Dictionary<IntPtr, string> EventPaths = new();
    private static readonly Dictionary<string, long> Counters = new(StringComparer.Ordinal);
    private static long sequence;
    private static long audioUpdates;
    private static long listenerUpdates;
    private static bool initialized;
    private static bool banksReady;
    private static bool lifecyclePaused;
    private static bool shutdown;
    private static string scene = "<none>";

    public static bool Initialized => initialized;
    public static bool BanksReady => banksReady;

    public static void Checkpoint(string name, string detail = null)
    {
        long id = Interlocked.Increment(ref sequence);
        Console.WriteLine(detail == null
            ? $"STAGE5B_AUDIO seq={id}; t={Elapsed():0.000}; name={name}"
            : $"STAGE5B_AUDIO seq={id}; t={Elapsed():0.000}; name={name}; {detail}");
    }

    public static void NativeRuntimeVersion(uint version)
    {
        Checkpoint("native-version", $"native=0x{version:X8}; managed=0x{ManagedHeaderVersion:X8}");
        if (version != ExpectedNativeVersion)
            throw new InvalidOperationException($"Native FMOD runtime 0x{version:X8} is not exact 0x{ExpectedNativeVersion:X8}.");
    }

    public static void InitializedSystem()
    {
        initialized = true;
        shutdown = false;
        Checkpoint("audio-initialized", "studio=OK; low-level=OK; fmod-sdl=registered");
    }

    public static void BankLoaded(string relativeName, Bank bank, bool stringsBank)
    {
        Audio.CheckFmod(bank.getEventCount(out int events), $"bank-event-count:{relativeName}");
        Audio.CheckFmod(bank.getBusCount(out int buses), $"bank-bus-count:{relativeName}");
        Audio.CheckFmod(bank.getVCACount(out int vcas), $"bank-vca-count:{relativeName}");
        Checkpoint("bank-loaded", $"file={relativeName}; strings={stringsBank.ToString().ToLowerInvariant()}; events={events}; buses={buses}; vcas={vcas}");
    }

    public static void AllBanksLoaded(FMOD.Studio.System system)
    {
        Audio.CheckFmod(system.getBankList(out Bank[] banks), "studio-bank-list");
        int events = 0;
        int buses = 0;
        int vcas = 0;
        foreach (Bank bank in banks)
        {
            Audio.CheckFmod(bank.getEventCount(out int bankEvents), "bank-event-count:inventory");
            Audio.CheckFmod(bank.getBusCount(out int bankBuses), "bank-bus-count:inventory");
            Audio.CheckFmod(bank.getVCACount(out int bankVcas), "bank-vca-count:inventory");
            events += bankEvents;
            buses += bankBuses;
            vcas += bankVcas;
        }
        banksReady = true;
        Checkpoint("banks-ready", $"banks={banks.Length}; events={events}; buses={buses}; vcas={vcas}");
    }

    public static void RegisterEvent(EventInstance instance, string path, string kind)
    {
        if (instance == null) return;
        lock (Gate) EventPaths[instance.getRaw()] = path ?? "<null>";
        Increment($"created-{Category(path)}");
        LogBounded("event-created", $"kind={kind}; category={Category(path)}; path={path}", 24);
    }

    public static RESULT EventOperation(string operation, IntPtr handle, RESULT result, string detail = null)
    {
        string path;
        lock (Gate) path = EventPaths.TryGetValue(handle, out string known) ? known : "<unregistered>";
        Increment(operation);
        // Celeste applies a shared set of optional parameters to many event
        // variants. FMOD 1.10.09 returns ERR_EVENT_NOTFOUND when a particular
        // event does not define one of those parameters; the original game
        // deliberately ignores that result. Keep it visible and bounded, but
        // do not misclassify the established optional-parameter contract as a
        // native/runtime failure.
        if (operation == "parameter" && result == RESULT.ERR_EVENT_NOTFOUND)
        {
            Increment("parameter-not-applicable");
            LogBounded("event-parameter-not-applicable", $"category={Category(path)}; path={path}{(detail == null ? "" : "; " + detail)}; result={result}", 40);
        }
        else if (result != RESULT.OK)
        {
            Checkpoint("fmod-error", $"managed=FMOD.Studio.EventInstance.{operation}; result={result}; scene={scene}; path={path}");
        }
        else if (operation is "start" or "stop" or "trigger-cue" or "pause" or "parameter")
        {
            LogBounded($"event-{operation}", $"category={Category(path)}; path={path}{(detail == null ? "" : "; " + detail)}", 40);
        }
        if (operation == "release" && result == RESULT.OK)
        {
            lock (Gate) EventPaths.Remove(handle);
        }
        return result;
    }

    public static void BusState(string path, string property, string value, RESULT result)
    {
        if (result != RESULT.OK)
            Checkpoint("fmod-error", $"managed=FMOD.Studio.Bus.{property}; result={result}; scene={scene}; path={path}");
        else
            LogBounded("bus-state", $"path={path}; property={property}; value={value}", 32);
    }

    public static void VcaVolume(string path, float requested, float readback, float finalVolume)
    {
        Checkpoint("vca-volume", $"path={path}; requested={requested:0.00}; readback={readback:0.00}; final={finalVolume:0.00}");
    }

    public static void ListenerUpdated()
    {
        Interlocked.Increment(ref listenerUpdates);
    }

    public static void AudioUpdated()
    {
        long count = Interlocked.Increment(ref audioUpdates);
        if (count % 600 == 0)
        {
            string summary;
            lock (Gate)
                summary = string.Join(",", Counters.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}:{item.Value}"));
            Console.WriteLine($"STAGE5B_AUDIO_HEARTBEAT updates={count}; listeners={Interlocked.Read(ref listenerUpdates)}; scene={scene}; active-known={KnownEventCount()}; counters={summary}");
        }
    }

    public static void SceneUpdated(Scene current)
    {
        string next = current?.GetType().FullName ?? "<none>";
        if (!string.Equals(scene, next, StringComparison.Ordinal))
        {
            scene = next;
            Checkpoint("scene-audio-context", $"scene={scene}; current-music={SanitizePath(Audio.CurrentMusic)}");
        }
    }

    public static void LifecyclePause(string reason)
    {
        if (!initialized || shutdown || lifecyclePaused) return;
        lifecyclePaused = true;
        bool paused = Audio.BusPaused("bus:/", true);
        Checkpoint("lifecycle-audio-paused", $"reason={reason}; readback={paused.ToString().ToLowerInvariant()}");
    }

    public static void LifecycleResume(string reason)
    {
        if (!initialized || shutdown || !lifecyclePaused) return;
        lifecyclePaused = false;
        bool paused = Audio.BusPaused("bus:/", false);
        Checkpoint("lifecycle-audio-resumed", $"reason={reason}; readback={paused.ToString().ToLowerInvariant()}");
    }

    public static void ShutdownEntered(string reason)
    {
        if (shutdown) return;
        shutdown = true;
        Checkpoint("audio-shutdown-entered", $"reason={reason}; known-events={KnownEventCount()}");
    }

    public static void ShutdownCompleted(string reason)
    {
        initialized = false;
        banksReady = false;
        lifecyclePaused = false;
        lock (Gate) EventPaths.Clear();
        Checkpoint("audio-shutdown-completed", $"reason={reason}");
    }

    public static void ShutdownSafely(string reason)
    {
        if (!initialized || shutdown) return;
        try
        {
            Audio.Unload();
        }
        catch (Exception exception)
        {
            Checkpoint("audio-shutdown-failed", $"reason={reason}; type={exception.GetType().FullName}; message={exception.Message}");
        }
    }

    public static void Fatal(Exception exception, string source)
    {
        Checkpoint("audio-fatal", $"source={source}; type={exception.GetType().FullName}; message={exception.Message}");
    }

    private static void Increment(string category)
    {
        lock (Gate)
        {
            Counters.TryGetValue(category, out long count);
            Counters[category] = count + 1;
        }
    }

    private static void LogBounded(string name, string detail, long maximum)
    {
        long count;
        lock (Gate)
        {
            string key = "log-" + name;
            Counters.TryGetValue(key, out count);
            Counters[key] = count + 1;
        }
        if (count < maximum) Checkpoint(name, detail);
    }

    private static int KnownEventCount()
    {
        lock (Gate) return EventPaths.Count;
    }

    private static string Category(string path)
    {
        if (string.IsNullOrEmpty(path)) return "unknown";
        if (path.StartsWith("event:/music/", StringComparison.OrdinalIgnoreCase)) return "music";
        if (path.StartsWith("event:/env/", StringComparison.OrdinalIgnoreCase)) return "ambience";
        if (path.StartsWith("event:/ui/", StringComparison.OrdinalIgnoreCase)) return "ui";
        return "gameplay-sfx";
    }

    private static string SanitizePath(string path) => string.IsNullOrEmpty(path) ? "<none>" : path;
    private static double Elapsed() => (Stopwatch.GetTimestamp() - StartedAt) / (double)Stopwatch.Frequency;
}
#endif
