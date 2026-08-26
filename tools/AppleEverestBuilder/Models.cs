using System.Text.Json.Serialization;

namespace AppleEverestBuilder;

internal static class ProductPolicy
{
    public const int MaxFiles = 4096;
    public const long MaxExpandedBytes = 256L * 1024 * 1024;
    public const long MaxSingleFileBytes = 64L * 1024 * 1024;
    public const int MaxPathDepth = 24;
    public const int MaxYamlBytes = 1024 * 1024;
    public const string TransformerVersion = "apple-everest-static-v14";
    public const int LevelSetProgressionSchemaVersion = 1;
    public const string CanonicalClass = "celeste-1.4.0.0-a";
}

internal sealed class AppleEverestProfile
{
    public int SchemaVersion { get; set; }
    public string Profile { get; set; } = "";
    public EverestPin Everest { get; set; } = new();
    public DependencyPins Dependencies { get; set; } = new();
    public HostPin Host { get; set; } = new();
    public int AppleTransformationVersion { get; set; }
    public string CelesteCanonicalClass { get; set; } = "";
    public string[] SupportedMechanismClasses { get; set; } = [];
    public string[] UnsupportedMechanismClasses { get; set; } = [];
    public string[] DeviceRuntimeExcludes { get; set; } = [];
}

internal sealed class EverestPin
{
    public string Tag { get; set; } = "";
    public string Repository { get; set; } = "";
    public string Sha256Commit { get; set; } = "";
}

internal sealed class DependencyPins
{
    public string MonoModCommit { get; set; } = "";
    public string NLuaProvenanceOnlyCommit { get; set; } = "";
    public string YamlDotNetVersion { get; set; } = "";
}

internal sealed class HostPin
{
    public string DotnetSdk { get; set; } = "";
    public string MonoModBuildSdk { get; set; } = "";
}

internal sealed class EverestYamlEntry
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    [JsonPropertyName("DLL")]
    public string? DLL { get; set; }
    public List<EverestDependency> Dependencies { get; set; } = [];
    public List<EverestDependency> OptionalDependencies { get; set; } = [];
    public List<EverestDependency> Conflicts { get; set; } = [];
}

internal sealed class EverestDependency
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
}

internal sealed class AppleStaticDeclaration
{
    public int SchemaVersion { get; set; }
    public string ModuleType { get; set; } = "";
    public string? SettingsType { get; set; }
    public string? SaveDataType { get; set; }
    public string? SessionType { get; set; }
    public string[] ButtonBindingProperties { get; set; } = [];
    public string[] TrackedEntityTypes { get; set; } = [];
    public string[] PooledEntityTypes { get; set; } = [];
    public AppleCustomEntityFactory[] CustomEntityFactories { get; set; } = [];
    public AppleOmittedCustomEntityFactory[] OmittedCustomEntityFactories { get; set; } = [];
    public AppleCustomBackdropFactory[] CustomBackdropFactories { get; set; } = [];
    public AppleSettingProperty[] SettingsProperties { get; set; } = [];
    public string[] OmittedSettingsProperties { get; set; } = [];
    public AppleModuleDurabilityCompatibility Durability { get; set; } = new();
}

internal sealed class AppleModuleDurabilityCompatibility
{
    public string SaveDataClass { get; set; } = "NONE";
    public string SessionClass { get; set; } = "NONE";
    public string AsyncClass { get; set; } = "DEFAULT_ASYNC";
    public string[] RejectedOverrides { get; set; } = [];
}

internal sealed class AppleOmittedCustomEntityFactory
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Reason { get; set; } = "";
}

internal sealed class AppleCustomEntityFactory
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Constructor { get; set; } = "";
}

internal sealed class AppleCustomBackdropFactory
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Factory { get; set; } = "";
    public string Method { get; set; } = "";
}

internal sealed class AppleSettingProperty
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Type { get; set; } = "";
    public string[] EnumNames { get; set; } = [];
    public int[] EnumValues { get; set; } = [];
    public int Minimum { get; set; }
    public int Maximum { get; set; }
    public int Step { get; set; } = 1;
}

internal enum CompatibilityClass
{
    CONTENT_ONLY,
    STATIC_MODULE,
    NORMAL_EVENT,
    ON_HOOK_SUPPORTED,
    DIRECT_HOOK_SUPPORTED,
    MIXED_MANAGED_DETOURS_SUPPORTED,
    MODINTEROP_STATIC_SUPPORTED,
    STATIC_IL_EVENT_FREEZE,
    STATIC_IL_EVENT_SEQUENCE,
    STATIC_DIRECT_ILHOOK_FREEZE,
    HASH_LOCKED_STATIC_AOT_COMPATIBILITY,
    HASH_LOCKED_STATIC_SEMANTIC_LOWERING,
    STATIC_CUSTOM_FMOD_BANK,
    MODINTEROP_DEFERRED,
    ON_HOOK_DEFERRED,
    IL_HOOK_DEFERRED,
    DIRECT_HOOK_DEFERRED,
    DYNAMIC_TARGET_DEFERRED,
    DYNAMIC_DETOUR_DEFERRED,
    DETOUR_CONFIG_DEFERRED,
    DYNAMIC_CODE_UNSUPPORTED,
    CUSTOM_AUDIO_UNSUPPORTED,
    NATIVE_UNSUPPORTED,
    LUA_UNSUPPORTED,
    PLATFORM_UNSUPPORTED
}

internal sealed record FileRecord(string Path, long Bytes, string Sha256);
internal sealed record ContentMountRecord(string Owner, int Order, string SourcePath, string LogicalPath,
    string Sha256, string SourceSha256);
internal sealed record MapProgressionRecord(
    string Path,
    string Sid,
    string LevelSet,
    string MapSha256,
    string CompatibilityId,
    string[] Rooms,
    int Strawberries,
    bool Heart,
    bool Cassette,
    string[] Checkpoints,
    string[] ProgressionEntities,
    string[] ProgressionTriggers,
    string[] AreaModes,
    bool CompletionAvailable,
    MapPresentationRecord? Presentation = null);
internal sealed record MapPresentationRecord(
    string Icon,
    string TitleBaseColor,
    string TitleAccentColor,
    string TitleTextColor,
    string IntroType,
    bool Dreaming,
    string ColorGrade,
    string Wipe,
    float DarknessAlpha,
    float BloomBase,
    float BloomStrength,
    string Jumpthru,
    string CoreMode,
    string Inventory,
    string Music,
    string Ambience,
    string StartLevel,
    bool HeartIsEnd,
    bool IgnoreLevelAudioLayerData)
{
    internal static readonly MapPresentationRecord EverestDefault = new(
        "areas/null", "6c7c81", "2f344b", "ffffff", "WakeUp", false, "",
        "Celeste.AngledWipe", 0.05f, 0f, 1f, "wood", "None", "Default",
        "event:/music/lvl1/main", "event:/env/amb/00_prologue", "", false, false);
}
internal sealed record LevelSetProgressionRecord(
    string LevelSet,
    string Identity,
    string[] MapSids,
    int MaximumStrawberries,
    int MaximumHearts,
    int MaximumCassettes,
    int MaximumCompletions);
internal sealed record MapElementRecord(
    string Kind,
    string Id,
    string Room,
    float X,
    float Y,
    int Width,
    int Height,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<(float X, float Y)> Nodes);
internal sealed record CollabMapRecord(
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
    float ReturnY);
internal sealed record CollabDescriptorRecord(
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
    IReadOnlyList<CollabMapRecord> Maps);
internal sealed record FrozenAssemblyRecord(string Owner, string AssemblyName, string FileName, string OriginalSha256, string FrozenSha256);
internal sealed record CustomAudioGuidRecord(Guid Id, string Path, string Kind);
internal sealed record CustomAudioBankPlan(
    string Owner,
    string Version,
    string SourceArchiveSha256,
    string SourcePath,
    string BankSha256,
    string GuidSourcePath,
    string GuidSha256,
    Guid BankId,
    string BankPath,
    string StagedPath,
    IReadOnlyList<CustomAudioGuidRecord> Guids);

internal sealed class ManagedDetourTargetCatalog
{
    public int SchemaVersion { get; set; }
    public ManagedDetourTarget[] Targets { get; set; } = [];
}

internal sealed class ManagedDetourTarget
{
    public string SourceKind { get; set; } = "method";
    public string Id { get; set; } = "";
    public string TargetAssembly { get; set; } = "Celeste";
    public string SourceFile { get; set; } = "";
    public string SourceDeclaration { get; set; } = "";
    public string OriginalDeclaration { get; set; } = "";
    public string OriginalAlias { get; set; } = "";
    public string HookNamespace { get; set; } = "";
    public string HookType { get; set; } = "";
    public string EventName { get; set; } = "";
    public string OrigDelegate { get; set; } = "";
    public string HookDelegate { get; set; } = "";
    public bool IsStatic { get; set; }
    public string? ReceiverType { get; set; }
    public string ReturnType { get; set; } = "void";
    public ManagedDetourParameter[] Parameters { get; set; } = [];
    public string[] DirectAliases { get; set; } = [];
}

internal sealed class ManagedDetourParameter
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Default { get; set; }
}

internal sealed record DirectManagedHookPlan(
    string PlanId,
    string Owner,
    string AssemblyName,
    string ContainingMethod,
    int ConstructorOffset,
    string TargetId,
    string TargetLookupName,
    string DetourType,
    string DetourMethod,
    bool DetourIsStatic,
    string DetourReturnType,
    string[] DetourParameterTypes,
    string Capture,
    string ConstructorSignature,
    int ExpressionInstructionCount = 9,
    bool CustomOriginalDelegate = false);

internal sealed record FrozenIlTransformPlan(
    string PlanId,
    string Owner,
    string AssemblyPath,
    string AssemblySha256,
    string EventType,
    string EventName,
    string TargetMethod,
    string CanonicalTargetMethod,
    string ManipulatorType,
    string ManipulatorMethod,
    bool ManipulatorIsStatic,
    int RegistrationOrdinal,
    string BeforeSha256,
    string AfterSha256,
    string DiffSha256,
    string[] InjectedMethods,
    string[] ExpectedDelegateTargets,
    string Mechanism = "HOOKGEN_IL_EVENT",
    string ConstructorSignature = "",
    string TargetExpression = "",
    string ManipulatorExpression = "",
    string Config = "absent",
    string ApplyByDefault = "implicit-true",
    string Storage = "",
    string Lifetime = "MODULE_IMMUTABLE_ACTIVE");

internal sealed class ModInput
{
    public required string SourcePath { get; init; }
    public required string StagingRoot { get; init; }
    public required string SourceSha256 { get; init; }
    public required IReadOnlyList<FileRecord> Files { get; init; }
    public required IReadOnlyList<EverestYamlEntry> Metadata { get; init; }
}

internal sealed class ResolvedMod
{
    public required EverestYamlEntry Metadata { get; init; }
    public required ModInput Input { get; init; }
    public required CompatibilityClass Classification { get; set; }
    public required SortedSet<string> Mechanisms { get; init; }
    public required IReadOnlyList<string> ManagedFiles { get; init; }
    public required IReadOnlyList<string> ContentFiles { get; init; }
    public AppleStaticDeclaration? Declaration { get; init; }
    public string? DeclaredAssemblyPath { get; init; }
    public required SortedSet<string> ManagedDetourTargets { get; init; }
    public required IReadOnlyList<DirectManagedHookPlan> DirectManagedHooks { get; init; }
    public required IReadOnlyList<ModInteropRegistrationPlan> ModInteropRegistrations { get; init; }
    public required IReadOnlyList<FrozenIlTransformPlan> FrozenIlTransforms { get; init; }
    public StaticAotCompatibilityPlan? StaticAotCompatibility { get; init; }
    public StaticSemanticLoweringPlan? StaticSemanticLowering { get; init; }
    public IReadOnlyList<CustomAudioBankPlan> CustomAudioBanks { get; init; } = [];
}

internal sealed record StaticAotCompatibilityPlan(string Id, string Owner, string Version,
    string SourceSha256, string DllPath, string DllSha256, bool AllowNonPublicCustomFactories);

internal sealed record StaticSemanticFactory(string Kind, string Id, string RuntimeFactory);

internal sealed record StaticSemanticLoweringPlan(
    string Id,
    string Owner,
    string Version,
    string SourceSha256,
    string DllPath,
    string DllSha256,
    IReadOnlyList<StaticSemanticFactory> Factories);
