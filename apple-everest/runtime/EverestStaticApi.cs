using System;
using System.Collections.Generic;

namespace Celeste.Mod;

public abstract class EverestModule
{
    public EverestModuleMetadata Metadata { get; internal set; }
    public virtual Type SettingsType => null;
    public virtual Type SaveDataType => null;
    public virtual Type SessionType => null;
    public virtual void Load() { }
    public virtual void Initialize() { }
    public virtual void LoadContent(bool firstLoad) { }
    public virtual void Unload() { }
}

public abstract class EverestModuleSettings { }
public abstract class EverestModuleSaveData { }
public abstract class EverestModuleSession { }

public sealed class EverestModuleMetadata
{
    public string Name { get; internal set; }
    public Version Version { get; internal set; }
}

public static partial class Everest
{
    public static IReadOnlyList<EverestModule> Modules => AppleEverestStaticRuntime.Modules;

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
