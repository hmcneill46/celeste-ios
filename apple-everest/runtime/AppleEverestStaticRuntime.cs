using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Foundation;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod;

public static class AppleEverestStaticRuntime
{
    private sealed class Loaded
    {
        public AppleEverestModuleDescriptor Descriptor;
        public EverestModule Module;
        public bool Enabled;
        public object Settings;
        public object SaveData;
        public object Session;
    }

    private static readonly List<Loaded> LoadedModules = new();
    private static readonly List<string> HookTrace = new();
    private static bool started;
    private static bool contentReady;
    private static string currentOwner = "AppleEverestCore";
    private static ModeProperties originalPrologueMode;

    internal static string CurrentOwner => currentOwner;
    public static IReadOnlyList<EverestModule> Modules => LoadedModules.Select(item => item.Module).ToArray();

    public static void Startup()
    {
        if (started) return;
        started = true;
        GeneratedAppleEverestAotRoots.Root();
        foreach (AppleEverestModuleDescriptor descriptor in GeneratedAppleEverestModuleRegistry.Modules)
        {
            EverestModule module = descriptor.ModuleFactory();
            module.Metadata = new EverestModuleMetadata { Name = descriptor.Name, Version = new Version(descriptor.Version) };
            Loaded loaded = new()
            {
                Descriptor = descriptor,
                Module = module,
                Enabled = true,
                Settings = descriptor.SettingsFactory?.Invoke(),
                SaveData = descriptor.SaveDataFactory?.Invoke(),
                Session = descriptor.SessionFactory?.Invoke()
            };
            LoadedModules.Add(loaded);
            InvokeOwned(loaded, module.Load);
        }
        Log($"startup=PASS profile={GeneratedAppleEverestModuleRegistry.Profile} modules={LoadedModules.Count} runtime-dll-load=false runtime-detour=false");
    }

    public static void ContentReady()
    {
        if (contentReady) return;
        contentReady = true;
        LoadCanaryDialog();
        VerifyContentPrecedence();
        foreach (Loaded loaded in LoadedModules)
        {
            InvokeOwned(loaded, loaded.Module.Initialize);
            InvokeOwned(loaded, () => loaded.Module.LoadContent(true));
        }
        Log($"content=PASS mounts={GeneratedAppleEverestContentManifest.Entries.Length}");
    }

    public static bool IsModuleEnabled(string name) =>
        LoadedModules.FirstOrDefault(item => item.Descriptor.Name == name)?.Enabled ?? name == "AppleEverestCore";

    public static bool ModuleEnabled(string name) => IsModuleEnabled(name);

    public static void SetModuleEnabled(string name, bool enabled)
    {
        Loaded loaded = LoadedModules.Single(item => item.Descriptor.Name == name);
        if (loaded.Enabled == enabled) return;
        if (enabled)
        {
            loaded.Enabled = true;
            InvokeOwned(loaded, loaded.Module.Load);
            if (contentReady)
            {
                InvokeOwned(loaded, loaded.Module.Initialize);
                InvokeOwned(loaded, () => loaded.Module.LoadContent(false));
            }
        }
        else
        {
            InvokeOwned(loaded, loaded.Module.Unload);
            loaded.Enabled = false;
        }
        On.Celeste.Dialog.RebuildActiveChain();
        ShowStatus($"{name}: {(enabled ? "ENABLED" : "DISABLED")}");
        Log($"module={name} enabled={enabled.ToString().ToLowerInvariant()} active-hooks={On.Celeste.Dialog.ActiveHandlerCount}");
    }

    public static void RecordHook(string value) => HookTrace.Add(value);

    public static void RunHookProbe()
    {
        HookTrace.Clear();
        string value = global::Celeste.Dialog.Clean("APPLE_EVEREST_HOOK_CANARY");
        string trace = string.Join(",", HookTrace);
        string expected = ExpectedHookTrace();
        bool pass = trace == expected;
        Log($"hook-probe={(pass ? "PASS" : "FAIL")} trace={trace} value={value}");
        ShowStatus((pass ? "HOOK CHAIN PASS" : "HOOK CHAIN FAIL") + "\n" + trace);
    }

    public static void LaunchCanaryRoom()
    {
        // Options is reachable before file selection, where vanilla Celeste
        // intentionally has no active SaveData. LevelLoader requires one, so
        // give the separate canary product Celeste's standard non-persistent
        // debug context instead of depending on a player-owned slot.
        bool createdDebugSave = SaveData.Instance == null;
        if (createdDebugSave) SaveData.InitializeDebugMode(loadExisting: false);
        Input.MenuConfirm.ConsumePress();
        Input.Jump.ConsumePress();
        originalPrologueMode ??= AreaData.Areas[0].Mode[0];
        ModeProperties source = originalPrologueMode;
        ModeProperties canary = new()
        {
            Path = "AppleEverest/Canary",
            Checkpoints = null,
            Inventory = PlayerInventory.Default,
            AudioState = source.AudioState.Clone()
        };
        AreaData.Areas[0].Mode[0] = canary;
        canary.MapData = new MapData(new AreaKey(0));
        Session session = new(new AreaKey(0));
        Engine.Scene = new LevelLoader(session) { PlayerIntroTypeOverride = Player.IntroTypes.None };
        Log($"content-room=launch path=AppleEverest/Canary debug-save-created={createdDebugSave.ToString().ToLowerInvariant()}");
    }

    public static void AttachCanaryBanner(global::Celeste.Level level, string source)
    {
        if (level.Tracker.GetEntity<AppleEverestCanaryBanner>() == null)
            level.Add(new AppleEverestCanaryBanner());
        Log($"ordinary-event=OnLoadLevel source={source} room={level.Session.Level}");
    }

    internal static void ShowStatus(string value)
    {
        if (Engine.Scene != null) Engine.Scene.Add(new AppleEverestStatus(value));
    }

    private static void InvokeOwned(Loaded loaded, Action action)
    {
        string previous = currentOwner;
        currentOwner = loaded.Descriptor.Name;
        try { action(); }
        finally { currentOwner = previous; }
    }

    private static string ExpectedHookTrace()
    {
        List<string> values = new List<string>();
        if (ModuleEnabled("AppleEverestCanaryHookB")) values.Add("B-before");
        if (ModuleEnabled("AppleEverestCanaryHookA")) values.Add("A-before");
        values.Add("original");
        if (ModuleEnabled("AppleEverestCanaryHookA")) values.Add("A-after");
        if (ModuleEnabled("AppleEverestCanaryHookB")) values.Add("B-after");
        return string.Join(",", values);
    }

    private static void LoadCanaryDialog()
    {
        string path = Path.Combine(Engine.ContentDirectory, "AppleEverest", "Dialog", "Canary.txt");
        foreach (string raw in ReadBundleText(path).Split('\n'))
        {
            int equals = raw.IndexOf('=');
            if (equals <= 0) continue;
            string key = raw[..equals].Trim();
            string value = raw[(equals + 1)..].Trim();
            foreach (Language language in global::Celeste.Dialog.Languages.Values)
            {
                language.Dialog[key] = value;
                language.Cleaned[key] = value;
            }
        }
    }

    private static void VerifyContentPrecedence()
    {
        string path = Path.Combine(Engine.ContentDirectory, "AppleEverest", "Canary", "precedence.txt");
        string value = ReadBundleText(path).Trim();
        if (value != "AppleEverestCanaryB")
            throw new InvalidDataException("Apple Everest content precedence did not select the newest dependent mount");
        Log("content-precedence=PASS winner=AppleEverestCanaryB");
    }

    private static string ReadBundleText(string path)
    {
        using Stream stream = TitleContainer.OpenStream(path);
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    internal static T GetSettings<T>(string moduleName) where T : class =>
        LoadedModules.Single(item => item.Descriptor.Name == moduleName).Settings as T;

    internal static void Log(string value)
    {
        string line = $"APPLE_EVEREST {value}";
        Console.WriteLine(line);
        AppleEverestPrivateLog.Write(line);
    }
}

internal static class AppleEverestPrivateLog
{
    private const long MaximumBytes = 64 * 1024;

    internal static void Write(string line)
    {
        try
        {
            string root = Environment.GetEnvironmentVariable("CELESTE_IOS_STORAGE_ROOT");
            if (string.IsNullOrWhiteSpace(root))
            {
                NSUrl[] urls = NSFileManager.DefaultManager.GetUrls(
                    NSSearchPathDirectory.ApplicationSupportDirectory, NSSearchPathDomain.User);
                root = urls.Length == 1 ? urls[0].Path : null;
            }
            if (string.IsNullOrWhiteSpace(root)) return;
            string directory = Path.Combine(root, "AppleEverest", "Logs");
            NSFileManager manager = NSFileManager.DefaultManager;
            NSError createError = null;
            if (!manager.FileExists(directory) &&
                !manager.CreateDirectory(directory, true, (NSFileAttributes)null, out createError))
            {
                createError?.Dispose();
                return;
            }
            createError?.Dispose();
            string path = Path.Combine(directory, "AppleEverestStatic.log");
            using NSData priorData = NSData.FromFile(path);
            byte[] prior = priorData?.Length <= MaximumBytes ? priorData.ToArray() : Encoding.UTF8.GetBytes("APPLE_EVEREST log rotated\n");
            byte[] addition = Encoding.UTF8.GetBytes(line + "\n");
            byte[] combined = new byte[prior.Length + addition.Length];
            Buffer.BlockCopy(prior, 0, combined, 0, prior.Length);
            Buffer.BlockCopy(addition, 0, combined, prior.Length, addition.Length);
            using NSData output = NSData.FromArray(combined);
            using NSUrl url = NSUrl.FromFilename(path);
            _ = output.Save(url, true);
        }
        catch
        {
            // Diagnostic logging must never affect Celeste gameplay.
        }
    }
}

internal sealed class AppleEverestCanaryBanner : Entity
{
    private readonly MTexture texture;

    public AppleEverestCanaryBanner()
    {
        Tag = Tags.HUD;
        Depth = -1000000;
        texture = new MTexture(VirtualContent.CreateTexture(Path.Combine("AppleEverest", "Canary", "banner.png")));
    }

    public override void Render()
    {
        texture.DrawJustified(new Vector2(960f, 112f), new Vector2(0.5f, 0.5f), Color.White);
        ActiveFont.DrawOutline("APPLE EVEREST STATIC CANARY", new Vector2(960f, 112f), new Vector2(0.5f, 0.5f), Vector2.One * 0.8f, Color.White, 2f, Color.Black);
    }
}

internal sealed class AppleEverestStatus : Entity
{
    private readonly string value;
    private float remaining = 4f;

    public AppleEverestStatus(string value)
    {
        this.value = value;
        Tag = Tags.HUD;
        Depth = -2000000;
    }

    public override void Update()
    {
        remaining -= Engine.RawDeltaTime;
        if (remaining <= 0f) RemoveSelf();
    }

    public override void Render()
    {
        Draw.Rect(360f, 420f, 1200f, 240f, Color.Black * 0.85f);
        ActiveFont.DrawOutline(value, new Vector2(960f, 540f), new Vector2(0.5f, 0.5f), Vector2.One * 0.75f, Color.White, 2f, Color.Black);
    }
}

internal static class AppleEverestLab
{
    public static void AddOptions(TextMenu menu)
    {
        menu.Add(new TextMenu.SubHeader("APPLE EVEREST STATIC LAB"));
        menu.Add(new TextMenu.Button("Play Content Canary").Pressed(AppleEverestStaticRuntime.LaunchCanaryRoom));
        menu.Add(new TextMenu.OnOff("Canary Core Module", AppleEverestStaticRuntime.ModuleEnabled("AppleEverestCanaryCore"))
            .Change(value => AppleEverestStaticRuntime.SetModuleEnabled("AppleEverestCanaryCore", value)));
        menu.Add(new TextMenu.OnOff("Canary Hook A", AppleEverestStaticRuntime.ModuleEnabled("AppleEverestCanaryHookA"))
            .Change(value => AppleEverestStaticRuntime.SetModuleEnabled("AppleEverestCanaryHookA", value)));
        menu.Add(new TextMenu.OnOff("Canary Hook B", AppleEverestStaticRuntime.ModuleEnabled("AppleEverestCanaryHookB"))
            .Change(value => AppleEverestStaticRuntime.SetModuleEnabled("AppleEverestCanaryHookB", value)));
        global::AppleEverest.Canaries.CanarySettings settings =
            AppleEverestStaticRuntime.GetSettings<global::AppleEverest.Canaries.CanarySettings>("AppleEverestCanaryCore");
        menu.Add(new TextMenu.OnOff("Canary Banner", settings.BannerEnabled)
            .Change(value => settings.BannerEnabled = value));
        menu.Add(new TextMenu.Button("Run Hook Chain Probe").Pressed(AppleEverestStaticRuntime.RunHookProbe));
    }
}
