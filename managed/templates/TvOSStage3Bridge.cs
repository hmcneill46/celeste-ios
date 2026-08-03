#if TVOS_STAGE3B
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste;

public static class TvOSStage3Bridge
{
    private static readonly object AudioGate = new();
    private static readonly Dictionary<string, long> NoAudioCalls = new(StringComparer.Ordinal);
    private static long noAudioUpdates;
    private static long updateCount;
    private static long drawCount;
    private static int lowLevelFmodCalls;
    private static Exception fatalException;

    public static long UpdateCount => Interlocked.Read(ref updateCount);
    public static long DrawCount => Interlocked.Read(ref drawCount);
    public static int LowLevelFmodCallCount => Volatile.Read(ref lowLevelFmodCalls);

    public static void Checkpoint(string name, string detail = null)
    {
        Console.WriteLine(detail == null
            ? $"STAGE3B_CHECKPOINT name={name}"
            : $"STAGE3B_CHECKPOINT name={name}; {detail}");
    }

    public static void RecordUpdate(Scene scene)
    {
        long count = Interlocked.Increment(ref updateCount);
        if (count == 1)
        {
            Checkpoint("first-celeste-update", DescribeScene(scene));
        }
    }

    public static void RecordDraw(Scene scene, GraphicsDevice graphicsDevice)
    {
        long count = Interlocked.Increment(ref drawCount);
        if (count == 1)
        {
            Checkpoint(
                "first-celeste-draw",
                $"{DescribeScene(scene)}; backbuffer={graphicsDevice.PresentationParameters.BackBufferWidth}x" +
                graphicsDevice.PresentationParameters.BackBufferHeight
            );
        }
        if (count % 300 == 0)
        {
            Console.WriteLine(
                $"STAGE3B_CELESTE_HEARTBEAT draws={count}; updates={UpdateCount}; {DescribeScene(scene)}; " +
                $"fmod-low-level={LowLevelFmodCallCount}"
            );
        }
    }

    public static void RecordNoAudioCall(string category)
    {
        lock (AudioGate)
        {
            NoAudioCalls.TryGetValue(category, out long count);
            NoAudioCalls[category] = count + 1;
        }
    }

    public static void RecordNoAudioUpdate()
    {
        long count = Interlocked.Increment(ref noAudioUpdates);
        if (count % 600 == 0)
        {
            Console.WriteLine($"STAGE3B_NO_AUDIO summary={NoAudioSummary()}; low-level={LowLevelFmodCallCount}");
        }
    }

    public static string NoAudioSummary()
    {
        lock (AudioGate)
        {
            return string.Join(",", NoAudioCalls.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}:{pair.Value}"));
        }
    }

    public static Exception FmodLowLevelReached(string symbol)
    {
        Interlocked.Increment(ref lowLevelFmodCalls);
        Console.WriteLine($"STAGE3B_FMOD_LOW_LEVEL_REACHED symbol={symbol}");
        return new PlatformNotSupportedException(
            $"FMOD native call {symbol} reached the Stage 3B fail-fast guard while audio is disabled."
        );
    }

    public static void RecordFatal(Exception exception, string source)
    {
        Interlocked.CompareExchange(ref fatalException, exception, null);
        Console.WriteLine($"STAGE3B_FATAL source={source}; type={exception.GetType().FullName}; message={exception.Message}");
    }

    public static void ThrowIfFatal()
    {
        Exception exception = Volatile.Read(ref fatalException);
        if (exception != null)
        {
            throw new InvalidOperationException("A Celeste background operation failed.", exception);
        }
    }

    public static string RunSettingsPreflight(string sessionRoot)
    {
        if (string.IsNullOrWhiteSpace(sessionRoot))
        {
            throw new ArgumentException("A private ephemeral session root is required.", nameof(sessionRoot));
        }
        Directory.CreateDirectory(sessionRoot);

        Settings.Instance = null;
        Settings.Existed = false;
        Settings.Initialize();
        if (Settings.Existed || Settings.Instance == null || Settings.Instance.Language != Settings.EnglishLanguage)
        {
            throw new InvalidOperationException("Settings first-run defaults failed.");
        }

        byte[] defaultXml = UserIO.Serialize(Settings.Instance);
        using (MemoryStream stream = new(defaultXml, writable: false))
        {
            Settings defaultsRoundTrip = TvOSSettingsSerializer.Deserialize(stream);
            if (defaultsRoundTrip.Language != Settings.EnglishLanguage || defaultsRoundTrip.WindowScale != 6)
            {
                throw new InvalidOperationException("Settings default round-trip failed.");
            }
        }

        Settings representative = new()
        {
            Version = "1.4.0.0",
            DefaultFileName = "TV Test",
            Fullscreen = false,
            WindowScale = 4,
            ViewportPadding = 12,
            VSync = false,
            DisableFlashes = true,
            ScreenShake = ScreenshakeAmount.Off,
            Rumble = RumbleAmount.Half,
            GrabMode = GrabModes.Toggle,
            CrouchDashMode = CrouchDashModes.Press,
            MusicVolume = 3,
            SFXVolume = 4,
            SpeedrunClock = SpeedrunType.File,
            LastSaveFile = 2,
            Language = "french",
            Pico8OnMainMenu = true,
            SetViewportOnce = true,
            VariantsUnlocked = true
        };
        representative.Left.Keyboard.Add(Keys.A);
        representative.Left.Controller.Add(Buttons.DPadLeft);

        byte[] representativeXml = UserIO.Serialize(representative);
        using (MemoryStream stream = new(representativeXml, writable: false))
        {
            Settings roundTrip = TvOSSettingsSerializer.Deserialize(stream);
            if (roundTrip.DefaultFileName != "TV Test" || roundTrip.WindowScale != 4 ||
                roundTrip.ScreenShake != ScreenshakeAmount.Off || roundTrip.Rumble != RumbleAmount.Half ||
                roundTrip.SpeedrunClock != SpeedrunType.File || !roundTrip.Left.Keyboard.SequenceEqual(new[] { Keys.A }) ||
                !roundTrip.Left.Controller.SequenceEqual(new[] { Buttons.DPadLeft }))
            {
                throw new InvalidOperationException("Settings representative non-default round-trip failed.");
            }
        }

        if (!UserIO.Save<Settings>(Settings.Filename, representativeXml))
        {
            throw new InvalidOperationException("Settings UserIO write/read verification failed.");
        }
        Settings loaded = UserIO.Load<Settings>(Settings.Filename);
        if (loaded == null || loaded.DefaultFileName != "TV Test")
        {
            throw new InvalidOperationException("Settings UserIO persisted round-trip failed.");
        }

        ExpectInvalidSettings("<Settings><Fullscreen>not-a-boolean</Fullscreen></Settings>", "malformed-value");
        ExpectInvalidSettings("<Settings><UnknownSetting>1</UnknownSetting></Settings>", "unknown-element");

        const string legacy = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<Settings xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" " +
            "xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
            "<Fullscreen>false</Fullscreen><WindowScale>5</WindowScale>" +
            "<ScreenShake>true</ScreenShake><Rumble>true</Rumble><SpeedrunClock>false</SpeedrunClock>" +
            "<Left><Keyboard><Keys>Left</Keys></Keyboard><Controller><Buttons>DPadLeft</Buttons></Controller></Left>" +
            "</Settings>";
        using (MemoryStream stream = new(Encoding.UTF8.GetBytes(legacy), writable: false))
        {
            Settings legacySettings = TvOSSettingsSerializer.Deserialize(stream);
            if (legacySettings.Fullscreen || legacySettings.WindowScale != 5 ||
                legacySettings.ScreenShake != ScreenshakeAmount.Half || legacySettings.Rumble != RumbleAmount.On ||
                legacySettings.SpeedrunClock != SpeedrunType.Off || legacySettings.Left.Keyboard.Single() != Keys.Left)
            {
                throw new InvalidOperationException("Representative legacy settings XML compatibility failed.");
            }
        }

        return "first-run-defaults=PASS; default-round-trip=PASS; non-default-round-trip=PASS; " +
            "UserIO-round-trip=PASS; malformed-and-unknown=PASS; legacy-XML=PASS";
    }

    public static string[] DiscoveryManifestLines()
    {
        Assembly assembly = typeof(Celeste).Assembly;
        Type[] types = assembly.GetTypes();

        Type[] trackedRoots = types.Where(type => type.GetCustomAttribute<Tracked>(inherit: false) != null).ToArray();
        SortedSet<string> trackedExpanded = new(StringComparer.Ordinal);
        foreach (Type root in trackedRoots)
        {
            trackedExpanded.Add(TypeName(root));
            Tracked attribute = root.GetCustomAttribute<Tracked>(inherit: false);
            if (attribute.Inherited)
            {
                foreach (Type candidate in types.Where(candidate => candidate != root && root.IsAssignableFrom(candidate)))
                {
                    trackedExpanded.Add(TypeName(candidate));
                }
            }
        }

        string[] pooled = Sorted(types.Where(type => type.GetCustomAttribute<Pooled>(inherit: false) != null).Select(TypeName));
        string[] ouis = Sorted(types.Where(type => typeof(Oui).IsAssignableFrom(type) && !type.IsAbstract).Select(TypeName));
        Type[] spawnableTypes = types.Where(type => type.GetCustomAttribute<SpawnableAttribute>(inherit: false) != null).ToArray();
        string[] spawnables = Sorted(spawnableTypes.Select(TypeName));
        string[] spawners = Sorted(spawnableTypes.SelectMany(type => type.GetMethods())
            .Where(method => method.IsStatic && method.GetCustomAttribute<SpawnerAttribute>(inherit: false) != null)
            .Select(MethodName));
        string[] commands = Sorted(types.SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(method => method.GetCustomAttribute<Command>(inherit: false) != null)
            .Select(MethodName));

        Tracker.Initialize();
        string[] trackedActual = Sorted(Tracker.TrackedEntityTypes.Keys.Concat(Tracker.TrackedComponentTypes.Keys).Select(TypeName));
        Pooler pooler = new();
        string[] pooledActual = Sorted(pooler.Pools.Keys.Select(TypeName));
        SpawnManager.SpawnActions.Clear();
        SpawnManager.Init();
        string[] spawnActionsActual = Sorted(SpawnManager.SpawnActions.Keys);
        _ = new Monocle.Commands();

        return new[]
        {
            ManifestLine("oui-types", ouis),
            ManifestLine("tracked-roots", Sorted(trackedRoots.Select(TypeName))),
            ManifestLine("tracked-expanded", trackedExpanded.ToArray()),
            ManifestLine("tracked-actual", trackedActual),
            ManifestLine("pooled-roots", pooled),
            ManifestLine("pooled-actual", pooledActual),
            ManifestLine("spawnable-types", spawnables),
            ManifestLine("spawner-methods", spawners),
            ManifestLine("spawn-actions-actual", spawnActionsActual),
            ManifestLine("command-methods", commands)
        };
    }

    private static void ExpectInvalidSettings(string xml, string scenario)
    {
        try
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(xml), writable: false);
            _ = TvOSSettingsSerializer.Deserialize(stream);
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException($"Settings {scenario} input was not rejected.");
    }

    private static string DescribeScene(Scene scene)
    {
        if (scene == null)
        {
            return "scene=<none>; entities=0; renderers=0";
        }
        return $"scene={TypeName(scene.GetType())}; entities={scene.Entities.Count}; renderers={scene.RendererList.Renderers.Count}";
    }

    private static string ManifestLine(string category, string[] values)
    {
        string joined = string.Join("\n", values);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined))).ToLowerInvariant();
        return $"category={category}; count={values.Length}; sha256={hash}; values={string.Join(",", values)}";
    }

    private static string[] Sorted(IEnumerable<string> values) =>
        values.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static string TypeName(Type type) => type.FullName ?? type.Name;
    private static string MethodName(MethodInfo method) => $"{TypeName(method.DeclaringType)}::{method.Name}";
}
#endif
