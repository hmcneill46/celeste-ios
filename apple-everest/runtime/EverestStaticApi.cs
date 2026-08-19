using System;
using System.Collections.Generic;

namespace Celeste.Mod;

public abstract class EverestModule
{
    // Everest exposes these as protected properties. Keeping the accessor
    // shape is important for precompiled mods: their IL calls get__Settings,
    // get__SaveData and get__Session rather than reading fields directly.
    protected EverestModuleSettings _Settings { get; private set; }
    protected EverestModuleSaveData _SaveData { get; private set; }
    protected EverestModuleSession _Session { get; private set; }
    public EverestModuleMetadata Metadata { get; internal set; }
    public virtual Type SettingsType => null;
    public virtual Type SaveDataType => null;
    public virtual Type SessionType => null;
    public virtual void Load() { }
    public virtual void Initialize() { }
    public virtual void LoadContent(bool firstLoad) { }
    public virtual void Unload() { }

    internal void SetStaticState(EverestModuleSettings settings, EverestModuleSaveData saveData, EverestModuleSession session)
    {
        _Settings = settings;
        _SaveData = saveData;
        _Session = session;
    }
}

public abstract class EverestModuleSettings { }
public abstract class EverestModuleSaveData { }
public abstract class EverestModuleSession { }

// Bounded settings ABI needed by supported precompiled modules. The normal
// Everest loader initializes these bindings reflectively; the static closure
// generator emits equivalent typed construction for every declared property.
public sealed class ButtonBinding
{
    public bool Pressed => false;
}

public static class Extensions
{
    public static global::Celeste.TextMenu.Item AddDescription(
        this global::Celeste.TextMenu.Item option,
        global::Celeste.TextMenu containingMenu,
        string description) => option;
}

public sealed class EverestModuleMetadata
{
    public string Name { get; internal set; }
    public Version Version { get; internal set; }
}

public static partial class Everest
{
    public static IReadOnlyList<EverestModule> Modules => AppleEverestStaticRuntime.Modules;

    // Preserve Everest's public binary contract. Mod DLLs refer to this as the
    // nested type Everest.Content and access Mods/Map as static fields.
    public static class Content
    {
        public static readonly List<ModContent> Mods = new();
        public static readonly Dictionary<string, ModAsset> Map = new(StringComparer.Ordinal);
    }

    public static partial class Events
    {
        public static partial class Level
        {
            public delegate void LoadLevelHandler(global::Celeste.Level level, global::Celeste.Player.IntroTypes playerIntro, bool isFromLoader);
            public static event LoadLevelHandler OnLoadLevel;
            internal static void RaiseOnLoadLevel(global::Celeste.Level level, global::Celeste.Player.IntroTypes intro, bool fromLoader) =>
                OnLoadLevel?.Invoke(level, intro, fromLoader);
        }
    }
}

public sealed class ModContent
{
    public string Name { get; set; }
    public EverestModuleMetadata Mod;
    public readonly Dictionary<string, ModAsset> Map = new(StringComparer.Ordinal);

    internal ModContent(string name) { Name = name; }
}

public sealed class ModAsset
{
    public bool TryDeserialize<T>(out T value)
    {
        value = default;
        return false;
    }
}

public static partial class Logger
{
    public static void SetLogLevel(string tag, LogLevel level) => AppleEverestLogPolicy.Set(tag, level);

    public static void Log(string tag, string value) => Write(LogLevel.Verbose, "mod-verbose", tag, value);
    public static void Info(string tag, string value) => Write(LogLevel.Info, "mod-info", tag, value);
    public static void Warn(string tag, string value) => Write(LogLevel.Warn, "mod-warning", tag, value);
    public static void Error(string tag, string value) => Write(LogLevel.Error, "mod-error", tag, value);

    internal static bool ShouldLog(string tag, LogLevel level) => AppleEverestLogPolicy.ShouldLog(tag, level);

    private static void Write(LogLevel level, string label, string tag, string value)
    {
        if (ShouldLog(tag, level)) AppleEverestStaticRuntime.Log($"{label} tag={tag} message={value}");
    }
}

internal sealed class AppleEverestModuleDescriptor
{
    public string Name { get; }
    public string Version { get; }
    public string[] Dependencies { get; }
    public Func<EverestModule> ModuleFactory { get; }
    public Func<EverestModuleSettings> SettingsFactory { get; }
    public Func<EverestModuleSaveData> SaveDataFactory { get; }
    public Func<EverestModuleSession> SessionFactory { get; }

    public AppleEverestModuleDescriptor(
        string name,
        string version,
        string[] dependencies,
        Func<EverestModule> moduleFactory,
        Func<EverestModuleSettings> settingsFactory,
        Func<EverestModuleSaveData> saveDataFactory,
        Func<EverestModuleSession> sessionFactory)
    {
        Name = name;
        Version = version;
        Dependencies = dependencies;
        ModuleFactory = moduleFactory;
        SettingsFactory = settingsFactory;
        SaveDataFactory = saveDataFactory;
        SessionFactory = sessionFactory;
    }
}
