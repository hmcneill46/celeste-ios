using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
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
    private static readonly HashSet<string> ObservedDirectHooks = new(StringComparer.Ordinal);
    private static readonly HashSet<string> ObservedCustomFactories = new(StringComparer.Ordinal);
    private static readonly HashSet<string> ObservedModInteropRegistrations = new(StringComparer.Ordinal);
    private static readonly HashSet<string> ObservedModInteropExports = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> StaticSpriteOwners = new(StringComparer.OrdinalIgnoreCase);
    private static SpriteBank StaticSpriteBank;
    private static bool started;
    private static bool startupCompleted;
    private static bool contentReady;
    private static bool contentLifecycleCompleted;
    private static string currentOwner = "AppleEverestCore";
    private static ModeProperties originalPrologueMode;
    private static SaveData saveDataBeforeModSession;
    private static bool suppressedModSessionSaveLogged;
    internal static bool NonPersistentModSession { get; private set; }

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
            // Establish the exact Everest module state ABI immediately. Some
            // ordinary module setting setters refer back through their owning
            // module's static Settings/SaveData/Session properties.
            module.SetStaticState(loaded.Settings as EverestModuleSettings,
                loaded.SaveData as EverestModuleSaveData, loaded.Session as EverestModuleSession);
        }
    }

    // Persisted settings can have ordinary module-defined side effects which
    // inspect Celeste.Instance.scene directly. Assigning Engine.Scene only
    // queues nextScene; Engine's first update is the point at which GameLoader
    // becomes the live scene. Complete startup immediately after that update,
    // before GameLoader's first update/draw, without a module-specific bypass.
    public static void CompleteStartup()
    {
        if (startupCompleted) return;
        if (global::Celeste.Celeste.Instance?.scene == null) return;
        startupCompleted = true;
        AppleEverestSettingsPersistence.LoadAndApply(GeneratedAppleEverestModuleRegistry.Settings);
        foreach (Loaded loaded in LoadedModules)
        {
            loaded.Descriptor.InputBindingInitializer?.Invoke((EverestModuleSettings)loaded.Settings);
            EverestModule module = loaded.Module;
            InvokeOwned(loaded, module.Load);
        }
        GeneratedAppleEverestModInterop.ReportStatus();
        CompleteContentLifecycle();
        Log($"startup=PASS profile={GeneratedAppleEverestModuleRegistry.Profile} modules={LoadedModules.Count} runtime-dll-load=false runtime-detour=false");
    }

    public static void ContentReady()
    {
        if (contentReady) return;
        contentReady = true;
        MountStaticAtlases();
        MountStaticModContent();
        MountStaticSpriteBanks();
        AppleEverestProgressionRuntime.RegisterAreas();
        if (GeneratedAppleEverestContentManifest.Has("AppleEverest/Dialog/Canary.txt")) LoadCanaryDialog();
        LoadStaticDialogFragments();
        if (GeneratedAppleEverestContentManifest.Has("AppleEverest/Canary/precedence.txt")) VerifyContentPrecedence();
        CompleteContentLifecycle();
        Log($"content=PASS mounts={GeneratedAppleEverestContentManifest.Entries.Length}");
    }

    private static void CompleteContentLifecycle()
    {
        if (contentLifecycleCompleted || !contentReady || !startupCompleted) return;
        contentLifecycleCompleted = true;
        foreach (Loaded loaded in LoadedModules)
        {
            InvokeOwned(loaded, loaded.Module.Initialize);
            InvokeOwned(loaded, () => loaded.Module.LoadContent(true));
        }
    }

    private static void MountStaticAtlases()
    {
        int game = 0;
        int gui = 0;
        int journal = 0;
        int checkpoints = 0;
        foreach (AppleEverestAtlasMountDescriptor descriptor in GeneratedAppleEverestContentManifest.AtlasMounts)
        {
            Atlas atlas;
            if (descriptor.Atlas == "Gameplay")
            {
                atlas = GFX.Game;
                game++;
            }
            else if (descriptor.Atlas == "Gui")
            {
                atlas = GFX.Gui;
                gui++;
            }
            else if (descriptor.Atlas == "Journal")
            {
                atlas = MTN.Journal;
                journal++;
            }
            else if (descriptor.Atlas == "Checkpoints")
            {
                atlas = MTN.Checkpoints;
                checkpoints++;
            }
            else
            {
                throw new InvalidOperationException($"unsupported static Everest atlas: {descriptor.Atlas}");
            }

            // The closure contains the exact release PNG as a private,
            // deterministic content mount.  Register it under the ordinary
            // Everest atlas key before any module Initialize/LoadContent call.
            // Later dependency-order entries intentionally win duplicate keys.
            // The closure can contain thousands of ordinary loose PNGs from
            // transitive helpers. Preserve every atlas key and its authored
            // dimensions at startup, but decode the backing image only if
            // gameplay or UI actually uses it.
            VirtualTexture texture = VirtualContent.CreateDeferredTexture(descriptor.LogicalPath);
            MTexture mounted = new(texture) { AtlasPath = descriptor.Key };
            atlas.Sources.Add(texture);
            atlas[descriptor.Key] = mounted;
        }
        Log($"content-atlas=PASS gameplay={game} gui={gui} journal={journal} checkpoints={checkpoints} precedence=dependency-order");
    }

    private static void MountStaticModContent()
    {
        Everest.Content.Mods.Clear();
        Everest.Content.Map.Clear();
        Dictionary<string, ModContent> owners = new(StringComparer.Ordinal);
        foreach (AppleEverestModContentDescriptor descriptor in GeneratedAppleEverestContentManifest.ModContents)
        {
            ModContent content = new(descriptor.Name)
            {
                Mod = new EverestModuleMetadata
                {
                    Name = descriptor.Name,
                    Version = new Version(descriptor.Version)
                }
            };
            owners.Add(descriptor.Name, content);
            Everest.Content.Mods.Add(content);
        }
        foreach (AppleEverestStaticAssetDescriptor descriptor in GeneratedAppleEverestContentManifest.StaticAssets)
        {
            if (!owners.TryGetValue(descriptor.Owner, out ModContent owner))
                throw new InvalidDataException($"static content owner is unresolved: {descriptor.Owner}");
            Type type = descriptor.IsYaml ? typeof(AssetTypeYaml) : null;
            ModAsset asset = new(owner, descriptor.PathVirtual, descriptor.LogicalPath, type, descriptor.Format);
            owner.Map[descriptor.PathVirtual] = asset;
            Everest.Content.Map[descriptor.PathVirtual] = asset;
        }
        Log($"content-assets=PASS owners={owners.Count} assets={GeneratedAppleEverestContentManifest.StaticAssets.Length} typed-yaml={GeneratedAppleEverestStaticAssets.FactoryCount}");
    }

    private static void MountStaticSpriteBanks()
    {
        XmlDocument composite = (XmlDocument)GFX.SpriteBank.XML.CloneNode(true);
        XmlElement sprites = composite["Sprites"] ??
            throw new InvalidDataException("vanilla sprite bank has no Sprites root");
        StaticSpriteOwners.Clear();
        foreach (AppleEverestSpriteBankDescriptor descriptor in GeneratedAppleEverestContentManifest.SpriteBanks)
        {
            XmlDocument mod = Calc.LoadContentXML(descriptor.LogicalPath);
            XmlElement modSprites = mod["Sprites"] ??
                throw new InvalidDataException($"static sprite bank has no Sprites root: {descriptor.LogicalPath}");
            foreach (XmlElement source in modSprites.ChildNodes.OfType<XmlElement>())
            {
                XmlElement existing = sprites.ChildNodes.OfType<XmlElement>()
                    .FirstOrDefault(value => string.Equals(value.Name, source.Name, StringComparison.OrdinalIgnoreCase));
                XmlNode imported = composite.ImportNode(source, true);
                if (existing == null) sprites.AppendChild(imported);
                else sprites.ReplaceChild(imported, existing);
                StaticSpriteOwners[source.Name] = descriptor.Owner;
            }
        }
        // Construct once after dependency-ordered composition. This preserves
        // ordinary Everest copy="player" inheritance and cross-bank overrides
        // without runtime assembly discovery or filesystem scanning.
        StaticSpriteBank = new SpriteBank(GFX.Game, composite);
        Log($"content-sprite-banks=PASS banks={GeneratedAppleEverestContentManifest.SpriteBanks.Length} sprites={StaticSpriteOwners.Count} precedence=dependency-order");
    }

    internal static void CreateStaticModSpriteOn(Sprite sprite, string spriteId)
    {
        if (StaticSpriteBank != null && StaticSpriteBank.Has(spriteId))
        {
            StaticSpriteBank.CreateOn(sprite, spriteId);
            StaticSpriteOwners.TryGetValue(spriteId, out string owner);
            Log($"content-sprite=PASS id={spriteId} owner={owner ?? "vanilla"}");
            return;
        }
        if (GFX.SpriteBank.Has(spriteId))
        {
            GFX.SpriteBank.CreateOn(sprite, spriteId);
            return;
        }
        throw new InvalidDataException($"unresolved static Everest sprite id: {spriteId}");
    }

    internal static Sprite CreateStaticModSprite(string spriteId)
    {
        Sprite sprite = new(GFX.Game, "");
        CreateStaticModSpriteOn(sprite, spriteId);
        return sprite;
    }

    public static bool IsModuleEnabled(string name) =>
        LoadedModules.FirstOrDefault(item => item.Descriptor.Name == name)?.Enabled ?? name == "AppleEverestCore";

    public static bool ModuleEnabled(string name) => IsModuleEnabled(name);

    public static void SetModuleEnabled(string name, bool enabled)
    {
        Loaded loaded = LoadedModules.Single(item => item.Descriptor.Name == name);
        if (loaded.Enabled == enabled) return;
        if (!enabled && loaded.Descriptor.RequiredBy.Length > 0)
        {
            ShowStatus($"{name} IS REQUIRED BY\n{string.Join(", ", loaded.Descriptor.RequiredBy)}");
            Log($"module={name} disable=blocked required-by={string.Join(",", loaded.Descriptor.RequiredBy)}");
            return;
        }
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
            GeneratedAppleEverestManagedDetourRegistry.RemoveOwner(loaded.Descriptor.Name);
            loaded.Enabled = false;
        }
        AppleEverestHookList.Invalidate();
        ShowStatus($"{name}: {(enabled ? "ENABLED" : "DISABLED")}");
        Log($"module={name} enabled={enabled.ToString().ToLowerInvariant()} detour-generation={AppleEverestHookList.Version}");
    }

    public static void RecordHook(string value) => HookTrace.Add(value);

    internal static void RecordDirectHookInvocation(string planId)
    {
        if (ObservedDirectHooks.Add(planId)) Log($"direct-hook=PASS plan={planId}");
    }

    internal static void RecordCustomFactoryUse(string owner, string id, string kind)
    {
        string key = owner + "\0" + kind + "\0" + id;
        if (ObservedCustomFactories.Add(key)) Log($"custom-factory=PASS owner={owner} kind={kind} id={id}");
    }

    public static void RecordModInteropRegistration(string type)
    {
        if (ObservedModInteropRegistrations.Add(type ?? "<null>"))
            Log($"modinterop-registration=PASS type={type}");
    }

    public static void RecordModInteropExportInvocation(string export)
    {
        if (ObservedModInteropExports.Add(export ?? "<null>"))
            Log($"modinterop-export-invoked=PASS export={export}");
    }

    internal static void RecordModInteropBinding(string import, bool bound) =>
        Log($"modinterop-binding={(bound ? "PASS" : "FAIL")} import={import}");

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
        bool createdDebugSave = BeginNonPersistentModSession();
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

    public static void LaunchFirstModMap()
    {
        LaunchModMap(GeneratedAppleEverestContentManifest.FirstMapPath);
    }

    public static void LaunchModMap(string path)
    {
        LaunchModMapRoom(path, null);
    }

    public static void LaunchModMapRoom(string path, string room)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        bool createdDebugSave = BeginNonPersistentModSession();
        Input.MenuConfirm.ConsumePress();
        Input.Jump.ConsumePress();
        originalPrologueMode ??= AreaData.Areas[0].Mode[0];
        ModeProperties source = originalPrologueMode;
        ModeProperties mod = new()
        {
            Path = path,
            Checkpoints = null,
            Inventory = PlayerInventory.Default,
            AudioState = source.AudioState.Clone()
        };
        AreaData.Areas[0].Mode[0] = mod;
        mod.MapData = new MapData(new AreaKey(0));
        Session session = new(new AreaKey(0));
        if (!string.IsNullOrEmpty(room))
        {
            if (!mod.MapData.Levels.Any(level => level.Name == room))
                throw new InvalidOperationException("static mod acceptance room is absent: " + room);
            session.Level = room;
            session.FirstLevel = false;
            session.StartedFromBeginning = false;
            session.RespawnPoint = null;
        }
        Engine.Scene = new LevelLoader(session) { PlayerIntroTypeOverride = Player.IntroTypes.None };
        Log($"content-map=launch path={path} room={(room ?? session.Level)} debug-save-created={createdDebugSave.ToString().ToLowerInvariant()}");
    }

    private static bool BeginNonPersistentModSession()
    {
        if (NonPersistentModSession) return false;

        saveDataBeforeModSession = SaveData.Instance;
        bool createdWithoutExistingSave = saveDataBeforeModSession == null;

        // A static mod map is an explicitly bounded compatibility surface, not
        // a vanilla file-select slot. Always give it its own debug SaveData so
        // map/session mutations cannot leak into a selected player save.
        SaveData.InitializeDebugMode(loadExisting: false);
        NonPersistentModSession = true;
        suppressedModSessionSaveLogged = false;
        Log($"mod-session=begin isolated=true prior-save={(createdWithoutExistingSave ? "none" : "preserved")}");
        return createdWithoutExistingSave;
    }

    internal static bool FilterVanillaFileSave(bool requested)
    {
        if (!requested || !NonPersistentModSession) return requested;
        if (!suppressedModSessionSaveLogged)
        {
            suppressedModSessionSaveLogged = true;
            Log("mod-session-save=suppressed reason=nonpersistent");
        }
        return false;
    }

    internal static Overworld.StartMode CompleteNonPersistentModSession(Overworld.StartMode requestedStartMode)
    {
        if (!NonPersistentModSession) return requestedStartMode;

        SaveData.Instance = saveDataBeforeModSession;
        saveDataBeforeModSession = null;
        NonPersistentModSession = false;
        suppressedModSessionSaveLogged = false;
        if (originalPrologueMode != null) AreaData.Areas[0].Mode[0] = originalPrologueMode;

        // LevelExit normally requests AreaQuit for Return to Map. That mode
        // constructs chapter-select UI and requires a live file-slot SaveData.
        // A mod map can instead have been launched from main-menu Options,
        // where the preserved vanilla state is intentionally null. Return to
        // the ordinary main menu after every bounded static-mod session: this
        // is safe both with and without a previously selected vanilla slot and
        // never exposes the isolated debug SaveData to normal menu gameplay.
        Log($"mod-session=complete prior-save-restored=true requested={requestedStartMode} applied={Overworld.StartMode.MainMenu}");
        return Overworld.StartMode.MainMenu;
    }

    internal static AppleEverestModuleSnapshotEntry[] CaptureModuleSnapshotEntries(
        int slot, AppleEverestModuleSnapshot previous)
    {
        Dictionary<string, AppleEverestModuleSnapshotEntry> previousEntries = previous?.Entries?
            .ToDictionary(value => value.Name, StringComparer.Ordinal) ?? new(StringComparer.Ordinal);
        List<AppleEverestModuleSnapshotEntry> entries = new();
        foreach (Loaded loaded in LoadedModules)
        {
            AppleEverestModuleDurabilityAdapter adapter = loaded.Descriptor.Durability;
            if (adapter == null) continue;
            byte[] saveData = null;
            byte[] session = null;
            bool previousCompatible = previousEntries.TryGetValue(loaded.Descriptor.Name,
                out AppleEverestModuleSnapshotEntry previousEntry) &&
                previousEntry.Version == loaded.Descriptor.Version &&
                previousEntry.Schema == adapter.Schema;
            try
            {
                if (adapter.SerializeSaveData != null)
                {
                    if (loaded.SaveData is EverestModuleSaveData indexed) indexed.Index = slot;
                    saveData = adapter.SerializeSaveData(loaded.SaveData as EverestModuleSaveData);
                }
            }
            catch (Exception exception)
            {
                saveData = previousCompatible && previousEntry.SaveDataValid ? previousEntry.SaveData : null;
                Log($"module-data=serialize-failed module={loaded.Descriptor.Name} kind=savedata category={exception.GetType().Name} action={(saveData != null ? "preserve-previous" : "default")}");
            }
            try
            {
                if (adapter.SerializeSession != null)
                {
                    if (loaded.Session is EverestModuleSession indexed) indexed.Index = slot;
                    session = adapter.SerializeSession(loaded.Session as EverestModuleSession);
                }
            }
            catch (Exception exception)
            {
                session = previousCompatible && previousEntry.SessionValid ? previousEntry.Session : null;
                Log($"module-data=serialize-failed module={loaded.Descriptor.Name} kind=session category={exception.GetType().Name} action={(session != null ? "preserve-previous" : "default")}");
            }
            entries.Add(new AppleEverestModuleSnapshotEntry(loaded.Descriptor.Name,
                loaded.Descriptor.Version, adapter.Schema, saveData, session));
            Log($"module-data=serialized module={loaded.Descriptor.Name} save-bytes={saveData?.Length ?? -1} session-bytes={session?.Length ?? -1}");
        }
        return entries.ToArray();
    }

    internal static void ApplyModuleSnapshot(int slot, AppleEverestModuleSnapshot snapshot, bool includeSession)
    {
        Dictionary<string, AppleEverestModuleSnapshotEntry> entries = snapshot?.Entries?
            .ToDictionary(value => value.Name, StringComparer.Ordinal) ?? new(StringComparer.Ordinal);
        foreach (Loaded loaded in LoadedModules)
        {
            AppleEverestModuleDurabilityAdapter adapter = loaded.Descriptor.Durability;
            if (adapter == null) continue;
            bool compatible = entries.TryGetValue(loaded.Descriptor.Name, out AppleEverestModuleSnapshotEntry entry) &&
                              entry.Version == loaded.Descriptor.Version && entry.Schema == adapter.Schema;
            loaded.SaveData = DeserializeSaveData(loaded, adapter,
                compatible && entry.SaveDataValid ? entry.SaveData : null, slot);
            if (includeSession)
                loaded.Session = DeserializeSession(loaded, adapter,
                    compatible && entry.SessionValid ? entry.Session : null, slot);
            loaded.Module.SetStaticState(loaded.Settings as EverestModuleSettings,
                loaded.SaveData as EverestModuleSaveData, loaded.Session as EverestModuleSession);
        }
        Log($"module-data=activated slot={slot} generation={snapshot?.Generation ?? 0} session={includeSession.ToString().ToLowerInvariant()}");
    }

    internal static void ResetModuleData(int slot, bool includeSession)
    {
        foreach (Loaded loaded in LoadedModules)
        {
            loaded.SaveData = NewSaveData(loaded, slot);
            if (includeSession) loaded.Session = NewSession(loaded, slot);
            loaded.Module.SetStaticState(loaded.Settings as EverestModuleSettings,
                loaded.SaveData as EverestModuleSaveData, loaded.Session as EverestModuleSession);
        }
    }

    internal static void ResetModuleSessions(int slot)
    {
        foreach (Loaded loaded in LoadedModules)
        {
            loaded.Session = NewSession(loaded, slot);
            loaded.Module.SetStaticState(loaded.Settings as EverestModuleSettings,
                loaded.SaveData as EverestModuleSaveData, loaded.Session as EverestModuleSession);
        }
        Log($"module-session=reset slot={slot} reason=new-vanilla-session");
    }

    private static EverestModuleSaveData DeserializeSaveData(Loaded loaded,
        AppleEverestModuleDurabilityAdapter adapter, byte[] payload, int slot)
    {
        try
        {
            EverestModuleSaveData value = payload == null || adapter.DeserializeSaveData == null
                ? NewSaveData(loaded, slot)
                : adapter.DeserializeSaveData(payload, slot);
            value ??= NewSaveData(loaded, slot);
            if (value != null) value.Index = slot;
            return value;
        }
        catch (Exception exception)
        {
            Log($"module-data=deserialize-failed module={loaded.Descriptor.Name} kind=savedata category={exception.GetType().Name} action=module-default");
            return NewSaveData(loaded, slot);
        }
    }

    private static EverestModuleSession DeserializeSession(Loaded loaded,
        AppleEverestModuleDurabilityAdapter adapter, byte[] payload, int slot)
    {
        try
        {
            EverestModuleSession value = payload == null || adapter.DeserializeSession == null
                ? NewSession(loaded, slot)
                : adapter.DeserializeSession(payload, slot);
            value ??= NewSession(loaded, slot);
            if (value != null) value.Index = slot;
            return value;
        }
        catch (Exception exception)
        {
            Log($"module-data=deserialize-failed module={loaded.Descriptor.Name} kind=session category={exception.GetType().Name} action=module-default");
            return NewSession(loaded, slot);
        }
    }

    private static EverestModuleSaveData NewSaveData(Loaded loaded, int slot)
    {
        EverestModuleSaveData value = loaded.Descriptor.SaveDataFactory?.Invoke();
        if (value != null) value.Index = slot;
        return value;
    }

    private static EverestModuleSession NewSession(Loaded loaded, int slot)
    {
        EverestModuleSession value = loaded.Descriptor.SessionFactory?.Invoke();
        if (value != null) value.Index = slot;
        return value;
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

    private static void LoadStaticDialogFragments()
    {
        foreach (string logical in GeneratedAppleEverestContentManifest.Entries)
        {
            if (!logical.StartsWith("AppleEverest/Mods/", StringComparison.Ordinal) ||
                !logical.Contains("/Dialog/", StringComparison.Ordinal) ||
                !logical.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) continue;
            string languageId = Path.GetFileNameWithoutExtension(logical).ToLowerInvariant();
            if (!global::Celeste.Dialog.Languages.TryGetValue(languageId, out Language language)) continue;
            string key = null;
            StringBuilder value = new();
            void Commit()
            {
                if (string.IsNullOrWhiteSpace(key)) return;
                string raw = value.ToString();
                language.Dialog[key] = raw;
                language.Cleaned[key] = raw;
            }
            foreach (string rawLine in ReadBundleText(Path.Combine(Engine.ContentDirectory, logical)).Replace("\r", "").Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                int equals = line.IndexOf('=');
                if (equals > 0)
                {
                    Commit();
                    key = line[..equals].Trim();
                    value.Clear();
                    value.Append(line[(equals + 1)..].Trim());
                }
                else if (key != null)
                {
                    if (value.Length > 0) value.Append("{break}");
                    value.Append(line);
                }
            }
            Commit();
            Log($"content-dialog=loaded path={logical}");
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

    internal static T GetModule<T>(string moduleName) where T : EverestModule =>
        LoadedModules.Single(item => item.Descriptor.Name == moduleName).Module as T;

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
        menu.Add(new TextMenu.Button("EVEREST / PORT OPTIONS").Pressed(() => OpenOptions(menu)));
    }

    private static void OpenOptions(TextMenu parent)
    {
        parent.Visible = false;
        parent.Focused = false;
        if (Engine.Scene is Overworld overworld) overworld.ShowConfirmUI = false;
        TextMenu options = new();
        options.Add(new TextMenu.Header("EVEREST / PORT OPTIONS"));
        string version = NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleShortVersionString")?.ToString() ?? "?";
        string build = NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleVersion")?.ToString() ?? "?";
        options.Add(new TextMenu.SubHeader("PORT v" + version + "  •  BUILD " + build, false));
        PopulateOptions(options);
        options.OnClose = () =>
        {
            parent.Visible = true;
            parent.Focused = true;
            if (Engine.Scene is Overworld current) current.ShowConfirmUI = true;
        };
        options.OnESC = options.OnPause = options.OnCancel = () =>
        {
            Audio.Play("event:/ui/main/button_back");
            options.Focused = false;
            options.Close();
        };
        Engine.Scene.Add(options);
        Engine.Scene.OnEndOfFrame += () => Engine.Scene.Entities.UpdateLists();
    }

    private static void PopulateOptions(TextMenu menu)
    {
        menu.Add(new TextMenu.SubHeader("APPLE EVEREST STATIC LAB"));
        foreach (AppleEverestCollabDescriptor collab in GeneratedAppleEverestCollabManifest.Collabs)
        {
            AppleEverestCollabDescriptor selectedCollab = collab;
            menu.Add(new TextMenu.Button("Play Real Collab Lobby: " + collab.DisplayName)
                .Pressed(() => AppleEverestProgressionRuntime.LaunchPersistent(selectedCollab.LobbySid)));
        }
        foreach (AppleEverestLevelSetProgressionDescriptor levelSet in GeneratedAppleEverestProgressionManifest.LevelSets)
        {
            menu.Add(new TextMenu.SubHeader("LEVELSET: " + levelSet.LevelSet));
            foreach (string mapSid in levelSet.MapSids)
            {
                string selectedSid = mapSid;
                string label = Path.GetFileName(selectedSid);
                menu.Add(new TextMenu.Button("Play Map: " + label)
                    .Pressed(() => AppleEverestProgressionRuntime.LaunchPersistent(selectedSid)));
            }
        }
        menu.Add(new TextMenu.SubHeader("STATIC DEBUG MAPS"));
        foreach (string mapPath in GeneratedAppleEverestContentManifest.MapPaths)
        {
            string selectedMap = mapPath;
            string label = Path.GetFileName(selectedMap);
            menu.Add(new TextMenu.Button("Play Static Mod Map (Debug): " + label)
                .Pressed(() => AppleEverestStaticRuntime.LaunchModMap(selectedMap)));
            if (selectedMap == "LittleEpic/precisionchallenge/precisionchallenge")
                menu.Add(new TextMenu.Button("Play LittleEpic Room 5 (Acceptance)")
                    .Pressed(() => AppleEverestStaticRuntime.LaunchModMapRoom(selectedMap, "5")));
        }
        foreach (EverestModule module in AppleEverestStaticRuntime.Modules)
        {
            string name = module.Metadata.Name;
            AppleEverestModuleDescriptor descriptor = GeneratedAppleEverestModuleRegistry.Modules.Single(value => value.Name == name);
            if (descriptor.RequiredBy.Length > 0)
                menu.Add(new TextMenu.SubHeader(name + " (REQUIRED BY " + string.Join(", ", descriptor.RequiredBy) + ")"));
            else
                menu.Add(new TextMenu.OnOff(name, AppleEverestStaticRuntime.ModuleEnabled(name))
                    .Change(value => AppleEverestStaticRuntime.SetModuleEnabled(name, value)));
        }
        foreach (IGrouping<string, AppleEverestSettingDescriptor> group in GeneratedAppleEverestModuleRegistry.Settings
                     .GroupBy(value => value.Module, StringComparer.Ordinal))
        {
            menu.Add(new TextMenu.SubHeader(group.Key + " MOD OPTIONS"));
            foreach (AppleEverestSettingDescriptor descriptor in group)
            {
                int current = descriptor.Get();
                if (descriptor.Kind == AppleEverestSettingKind.Boolean)
                {
                    menu.Add(new TextMenu.OnOff(descriptor.Label, current != 0)
                        .Change(value => AppleEverestSettingsPersistence.Set(descriptor, value ? 1 : 0)));
                }
                else if (descriptor.Kind == AppleEverestSettingKind.Enum)
                {
                    TextMenu.Option<int> option = new(descriptor.Label);
                    for (int index = 0; index < descriptor.EnumValues.Length; index++)
                        option.Add(descriptor.EnumNames[index], descriptor.EnumValues[index], descriptor.EnumValues[index] == current);
                    menu.Add(option.Change(value => AppleEverestSettingsPersistence.Set(descriptor, value)));
                }
                else
                {
                    int stepCount = (descriptor.Maximum - descriptor.Minimum) / descriptor.Step;
                    int currentStep = (current - descriptor.Minimum) / descriptor.Step;
                    menu.Add(new TextMenu.Slider(descriptor.Label,
                        index => (descriptor.Minimum + index * descriptor.Step).ToString(),
                        0, stepCount, currentStep)
                        .Change(index => AppleEverestSettingsPersistence.Set(descriptor,
                            descriptor.Minimum + index * descriptor.Step)));
                }
            }
        }
        if (AppleEverestStaticRuntime.ModuleEnabled("AppleEverestCanaryHookA") || AppleEverestStaticRuntime.ModuleEnabled("AppleEverestCanaryHookB"))
            menu.Add(new TextMenu.Button("Run Hook Chain Probe").Pressed(AppleEverestStaticRuntime.RunHookProbe));
    }
}
