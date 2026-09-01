using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod;

public abstract class EverestModule
{
    // Keep the exact public virtual property ABI used by pinned desktop
    // Everest. Precompiled modules call these accessors directly.
    public virtual EverestModuleSettings _Settings { get; set; }
    public virtual EverestModuleSaveData _SaveData { get; set; }
    public virtual EverestModuleSession _Session { get; set; }
    public virtual EverestModuleMetadata Metadata { get; set; }
    public virtual Type SettingsType => null;
    public virtual Type SaveDataType => null;
    public virtual Type SessionType => null;
    public virtual bool SaveDataAsync { get; set; } = true;
    public virtual void Load() { }
    public virtual void Initialize() { }
    public virtual void LoadContent(bool firstLoad) { }
    public virtual void Unload() { }
    // Exact pinned public override point. The static product builds supported
    // settings controls from its closed descriptor graph; accepted modules may
    // still call the base implementation from their own optional menu code.
    public virtual void CreateModMenuSection(global::Celeste.TextMenu menu, bool inGame,
        global::FMOD.Studio.EventInstance snapshot) { }

    internal void SetStaticState(EverestModuleSettings settings, EverestModuleSaveData saveData, EverestModuleSession session)
    {
        _Settings = settings;
        _SaveData = saveData;
        _Session = session;
    }
}

public abstract class EverestModuleSettings { }
public abstract class EverestModuleSaveData
{
    // Desktop Everest assigns the selected numbered slot before and after
    // deserializing a module payload.  Keep the same public ABI and semantics
    // without allowing the index to enter the YAML property graph.
    public int Index { get; set; }
}

public abstract class EverestModuleSession
{
    public int Index { get; set; }
}

internal sealed record AppleEverestCollabMapDescriptor(
    string Sid,
    string LobbySid,
    string DisplayName,
    string Author,
    int Order,
    string SourceMapSha256,
    string MountedMapSha256,
    string CompatibilityId,
    string LevelSet,
    string[] Rooms,
    bool AllowSaving,
    string ReturnMode,
    string ReturnRoom,
    float ReturnX,
    float ReturnY,
    int AuthoredStrawberries,
    bool AuthoredHeart,
    bool CompletionAvailable,
    int MiniHeartCount,
    AppleEverestCollabSpecialBerryDescriptor[] SpecialBerries);
internal sealed record AppleEverestCollabMiniHeartDoorDescriptor(
    int EntityId,
    string Room,
    float X,
    float Y,
    int Width,
    int Height,
    int Requires,
    string LevelSet,
    string DoorId,
    string Color,
    string[] ContributingMapSids);
internal sealed record AppleEverestCollabSpecialBerryDescriptor(
    string EntityType,
    string SemanticClass,
    string Durability,
    int EntityId,
    string Room,
    float X,
    float Y,
    string LevelSet,
    string Maps,
    int Requires,
    bool AlwaysSpawn,
    bool CountTowardsTotal,
    float GoldTime,
    float SilverTime,
    float BronzeTime,
    string Sprite);
internal sealed record AppleEverestCollabDescriptor(
    string Id,
    string DisplayName,
    string Owner,
    string Version,
    string SourceLogicalSha256,
    string ArchiveSha256,
    string LobbySid,
    string LobbyDisplayName,
    string LobbySourceMapSha256,
    string LobbyMountedMapSha256,
    string LobbyCompatibilityId,
    string LobbyLevelSet,
    string[] LobbyRooms,
    string JournalLevelSet,
    bool JournalVanilla,
    bool JournalShowOnlyDiscovered,
    AppleEverestCollabMiniHeartDoorDescriptor[] MiniHeartDoors,
    AppleEverestCollabSpecialBerryDescriptor[] LobbySpecialBerries,
    AppleEverestCollabMapDescriptor[] Maps);

// Bounded settings ABI needed by supported precompiled modules. The normal
// Everest loader initializes these bindings reflectively; the static closure
// generator emits equivalent typed construction for every declared property.
public sealed class ButtonBinding
{
    public List<Buttons> Buttons
    {
        get => Binding.Controller;
        set => Binding.Controller = value ?? new List<Buttons>();
    }

    public List<Keys> Keys
    {
        get => Binding.Keyboard;
        set => Binding.Keyboard = value ?? new List<Keys>();
    }

    public Binding Binding { get; private set; }
    public VirtualButton Button;

    public bool Check => Button?.Check ?? false;
    public bool Pressed => Button?.Pressed ?? false;
    public bool Released => Button?.Released ?? false;
    public bool Repeating => Button?.Repeating ?? false;

    public ButtonBinding() : this(0) { }

    public ButtonBinding(Buttons buttons, params Keys[] keys)
    {
        Binding = new Binding
        {
            Controller = Enum.GetValues<Buttons>()
                .Where(button => button != 0 && (buttons & button) == button).ToList(),
            Keyboard = new List<Keys>(keys ?? Array.Empty<Keys>())
        };
    }

    // Desktop Everest deliberately constructs settings before Celeste input
    // exists, then attaches each ButtonBinding from OnInputInitialize. The
    // static product emits that same typed second phase without reflection.
    internal void InitializeCurrentInput()
    {
        if (Button != null) return;
        if (global::Celeste.Input.Gamepad == null)
            throw new InvalidOperationException("Celeste input is not initialized");
        Button = new VirtualButton(Binding, global::Celeste.Input.Gamepad, 0.08f, 0.2f);
    }

    public void ConsumeBuffer() => Button?.ConsumeBuffer();
    public void ConsumePress() => Button?.ConsumePress();
    public void SetRepeat(float repeatTime) => Button?.SetRepeat(repeatTime);
    public void SetRepeat(float repeatTime, float multiRepeatTime) => Button?.SetRepeat(repeatTime, multiRepeatTime);
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingNameAttribute : Attribute
{
    public string Name { get; }
    public SettingNameAttribute(string name) { Name = name; }
}

// Metadata-only Everest settings ABI referenced by accepted precompiled
// modules. The host generator consumes these attributes before AOT and emits
// typed menu/binding descriptors; the device runtime keeps only their exact
// harmless constructors so the frozen assemblies remain linkable.
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingSubTextAttribute : Attribute
{
    public string Description { get; }
    public SettingSubTextAttribute(string description) { Description = description; }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingRangeAttribute : Attribute
{
    public int Min { get; }
    public int Max { get; }
    public bool LargeRange { get; }
    public SettingRangeAttribute(int min, int max, bool largeRange = false)
    {
        Min = min;
        Max = max;
        LargeRange = largeRange;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class DefaultButtonBindingAttribute : Attribute
{
    public Buttons Button { get; }
    public Keys Key { get; }
    public DefaultButtonBindingAttribute(Buttons button, Keys key)
    {
        Button = button;
        Key = key;
    }
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
        public static event Action<ModAsset, ModAsset> OnUpdate;

        internal static void RaiseUpdate(ModAsset oldAsset, ModAsset newAsset) => OnUpdate?.Invoke(oldAsset, newAsset);
    }

    public static partial class Events
    {
        public static partial class Player
        {
            public static event Action<global::Celeste.Player> OnAfterUpdate;
            internal static void RaiseOnAfterUpdate(global::Celeste.Player player) =>
                OnAfterUpdate?.Invoke(player);
        }

        public static partial class Level
        {
            public delegate void LoadLevelHandler(global::Celeste.Level level, global::Celeste.Player.IntroTypes playerIntro, bool isFromLoader);
            public static event LoadLevelHandler OnLoadLevel;
            internal static void RaiseOnLoadLevel(global::Celeste.Level level, global::Celeste.Player.IntroTypes intro, bool fromLoader) =>
                OnLoadLevel?.Invoke(level, intro, fromLoader);

            public delegate global::Celeste.Backdrop LoadBackdropHandler(global::Celeste.MapData map,
                global::Celeste.BinaryPacker.Element child, global::Celeste.BinaryPacker.Element above);
            public static event LoadBackdropHandler OnLoadBackdrop;
            internal static global::Celeste.Backdrop LoadBackdrop(global::Celeste.MapData map,
                global::Celeste.BinaryPacker.Element child, global::Celeste.BinaryPacker.Element above)
            {
                if (OnLoadBackdrop == null) return null;
                foreach (LoadBackdropHandler handler in OnLoadBackdrop.GetInvocationList())
                {
                    global::Celeste.Backdrop result = handler(map, child, above);
                    if (result != null) return result;
                }
                return null;
            }
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
    public ModContent Source;
    public Type Type;
    public string Format;
    public string PathVirtual;
    internal string LogicalPath;

    internal ModAsset(ModContent source, string pathVirtual, string logicalPath, Type type, string format)
    {
        Source = source;
        PathVirtual = pathVirtual;
        LogicalPath = logicalPath;
        Type = type;
        Format = format;
    }

    public bool TryDeserialize<T>(out T value)
    {
        return GeneratedAppleEverestStaticAssets.TryDeserialize(this, out value);
    }

    public T Deserialize<T>() => TryDeserialize(out T value) ? value : default;
}

public sealed class AssetTypeYaml { private AssetTypeYaml() { } }

public static partial class Logger
{
    public static void SetLogLevel(string tag, LogLevel level) => AppleEverestLogPolicy.Set(tag, level);

    public static void Log(string tag, string value) => Write(LogLevel.Verbose, "mod-verbose", tag, value);
    public static void Log(LogLevel level, string tag, string value) => Write(level, "mod-" + level.ToString().ToLowerInvariant(), tag, value);
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
    public string[] RequiredBy { get; }
    public Func<EverestModule> ModuleFactory { get; }
    public Func<EverestModuleSettings> SettingsFactory { get; }
    public Action<EverestModuleSettings> InputBindingInitializer { get; }
    public Func<EverestModuleSaveData> SaveDataFactory { get; }
    public Func<EverestModuleSession> SessionFactory { get; }
    public AppleEverestModuleDurabilityAdapter Durability { get; }

    public AppleEverestModuleDescriptor(
        string name,
        string version,
        string[] dependencies,
        string[] requiredBy,
        Func<EverestModule> moduleFactory,
        Func<EverestModuleSettings> settingsFactory,
        Action<EverestModuleSettings> inputBindingInitializer,
        Func<EverestModuleSaveData> saveDataFactory,
        Func<EverestModuleSession> sessionFactory,
        AppleEverestModuleDurabilityAdapter durability)
    {
        Name = name;
        Version = version;
        Dependencies = dependencies;
        RequiredBy = requiredBy;
        ModuleFactory = moduleFactory;
        SettingsFactory = settingsFactory;
        InputBindingInitializer = inputBindingInitializer;
        SaveDataFactory = saveDataFactory;
        SessionFactory = sessionFactory;
        Durability = durability;
    }
}

internal sealed class AppleEverestMapProgressionDescriptor
{
    public string Path { get; }
    public string Sid { get; }
    public string LevelSet { get; }
    public string MapSha256 { get; }
    public string CompatibilityId { get; }
    public string[] Rooms { get; }
    public int Strawberries { get; }
    public bool Heart { get; }
    public bool Cassette { get; }
    public string[] Checkpoints { get; }
    public string[] AreaModes { get; }
    public bool CompletionAvailable { get; }
    public AppleEverestMapPresentationDescriptor Presentation { get; }
    public int RuntimeAreaId { get; internal set; } = -1;

    public AppleEverestMapProgressionDescriptor(string path, string sid, string levelSet,
        string mapSha256, string compatibilityId, string[] rooms, int strawberries,
        bool heart, bool cassette, string[] checkpoints, string[] areaModes,
        bool completionAvailable, AppleEverestMapPresentationDescriptor presentation = null)
    {
        Path = path;
        Sid = sid;
        LevelSet = levelSet;
        MapSha256 = mapSha256;
        CompatibilityId = compatibilityId;
        Rooms = rooms;
        Strawberries = strawberries;
        Heart = heart;
        Cassette = cassette;
        Checkpoints = checkpoints;
        AreaModes = areaModes;
        CompletionAvailable = completionAvailable;
        Presentation = presentation ?? AppleEverestMapPresentationDescriptor.EverestDefault;
    }
}

internal sealed class AppleEverestMapPresentationDescriptor
{
    internal static readonly AppleEverestMapPresentationDescriptor EverestDefault = new(
        "areas/null", "6c7c81", "2f344b", "ffffff", "WakeUp", false, "",
        "Celeste.AngledWipe", 0.05f, 0f, 1f, "wood", "None", "Default",
        "event:/music/lvl1/main", "event:/env/amb/00_prologue", "", false, false);

    public string Icon { get; }
    public string TitleBaseColor { get; }
    public string TitleAccentColor { get; }
    public string TitleTextColor { get; }
    public string IntroType { get; }
    public bool Dreaming { get; }
    public string ColorGrade { get; }
    public string Wipe { get; }
    public float DarknessAlpha { get; }
    public float BloomBase { get; }
    public float BloomStrength { get; }
    public string Jumpthru { get; }
    public string CoreMode { get; }
    public string Inventory { get; }
    public string Music { get; }
    public string Ambience { get; }
    public string StartLevel { get; }
    public bool HeartIsEnd { get; }
    public bool IgnoreLevelAudioLayerData { get; }

    public AppleEverestMapPresentationDescriptor(string icon, string titleBaseColor, string titleAccentColor,
        string titleTextColor, string introType, bool dreaming, string colorGrade, string wipe,
        float darknessAlpha, float bloomBase, float bloomStrength, string jumpthru, string coreMode,
        string inventory, string music, string ambience, string startLevel, bool heartIsEnd,
        bool ignoreLevelAudioLayerData)
    {
        Icon = icon; TitleBaseColor = titleBaseColor; TitleAccentColor = titleAccentColor;
        TitleTextColor = titleTextColor; IntroType = introType; Dreaming = dreaming;
        ColorGrade = colorGrade; Wipe = wipe; DarknessAlpha = darknessAlpha; BloomBase = bloomBase;
        BloomStrength = bloomStrength; Jumpthru = jumpthru; CoreMode = coreMode; Inventory = inventory;
        Music = music; Ambience = ambience; StartLevel = startLevel; HeartIsEnd = heartIsEnd;
        IgnoreLevelAudioLayerData = ignoreLevelAudioLayerData;
    }
}

internal sealed class AppleEverestLevelSetProgressionDescriptor
{
    public string LevelSet { get; }
    public string Identity { get; }
    public string[] MapSids { get; }
    public int MaximumStrawberries { get; }
    public int MaximumHearts { get; }
    public int MaximumCassettes { get; }
    public int MaximumCompletions { get; }

    public AppleEverestLevelSetProgressionDescriptor(string levelSet, string identity,
        string[] mapSids, int maximumStrawberries, int maximumHearts,
        int maximumCassettes, int maximumCompletions)
    {
        LevelSet = levelSet;
        Identity = identity;
        MapSids = mapSids;
        MaximumStrawberries = maximumStrawberries;
        MaximumHearts = maximumHearts;
        MaximumCassettes = maximumCassettes;
        MaximumCompletions = maximumCompletions;
    }
}

internal sealed class AppleEverestModuleDurabilityAdapter
{
    public string Schema { get; }
    public Func<EverestModuleSaveData, byte[]> SerializeSaveData { get; }
    public Func<byte[], int, EverestModuleSaveData> DeserializeSaveData { get; }
    public Func<EverestModuleSession, byte[]> SerializeSession { get; }
    public Func<byte[], int, EverestModuleSession> DeserializeSession { get; }

    public AppleEverestModuleDurabilityAdapter(
        string schema,
        Func<EverestModuleSaveData, byte[]> serializeSaveData,
        Func<byte[], int, EverestModuleSaveData> deserializeSaveData,
        Func<EverestModuleSession, byte[]> serializeSession,
        Func<byte[], int, EverestModuleSession> deserializeSession)
    {
        Schema = schema;
        SerializeSaveData = serializeSaveData;
        DeserializeSaveData = deserializeSaveData;
        SerializeSession = serializeSession;
        DeserializeSession = deserializeSession;
    }
}

internal enum AppleEverestSettingKind
{
    Boolean,
    Enum,
    Integer
}

internal sealed class AppleEverestSettingDescriptor
{
    public string Module { get; }
    public string Property { get; }
    public string Label { get; }
    public AppleEverestSettingKind Kind { get; }
    public Func<int> Get { get; }
    public Action<int> Set { get; }
    public string[] EnumNames { get; }
    public int[] EnumValues { get; }
    public int Minimum { get; }
    public int Maximum { get; }
    public int Step { get; }

    public AppleEverestSettingDescriptor(
        string module,
        string property,
        string label,
        AppleEverestSettingKind kind,
        Func<int> get,
        Action<int> set,
        string[] enumNames,
        int[] enumValues,
        int minimum,
        int maximum,
        int step)
    {
        Module = module;
        Property = property;
        Label = label;
        Kind = kind;
        Get = get;
        Set = set;
        EnumNames = enumNames;
        EnumValues = enumValues;
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
    }

    public bool Accepts(int value) => Kind switch
    {
        AppleEverestSettingKind.Boolean => value is 0 or 1,
        AppleEverestSettingKind.Enum => Array.IndexOf(EnumValues, value) >= 0,
        AppleEverestSettingKind.Integer => value >= Minimum && value <= Maximum &&
            (value - Minimum) % Math.Max(1, Step) == 0,
        _ => false
    };
}

internal sealed class AppleEverestAtlasMountDescriptor
{
    internal string Owner { get; }
    internal string Atlas { get; }
    internal string Key { get; }
    internal string LogicalPath { get; }

    internal AppleEverestAtlasMountDescriptor(string owner, string atlas, string key, string logicalPath)
    {
        Owner = owner;
        Atlas = atlas;
        Key = key;
        LogicalPath = logicalPath;
    }
}

internal sealed class AppleEverestModContentDescriptor
{
    internal string Name { get; }
    internal string Version { get; }

    internal AppleEverestModContentDescriptor(string name, string version)
    {
        Name = name;
        Version = version;
    }
}

internal sealed class AppleEverestSpriteBankDescriptor
{
    internal string Owner { get; }
    internal string LogicalPath { get; }

    internal AppleEverestSpriteBankDescriptor(string owner, string logicalPath)
    {
        Owner = owner;
        LogicalPath = logicalPath;
    }
}

internal sealed class AppleEverestStaticAssetDescriptor
{
    internal string Owner { get; }
    internal string PathVirtual { get; }
    internal string LogicalPath { get; }
    internal bool IsYaml { get; }
    internal string Format { get; }

    internal AppleEverestStaticAssetDescriptor(string owner, string pathVirtual,
        string logicalPath, bool isYaml, string format)
    {
        Owner = owner;
        PathVirtual = pathVirtual;
        LogicalPath = logicalPath;
        IsYaml = isYaml;
        Format = format;
    }
}
