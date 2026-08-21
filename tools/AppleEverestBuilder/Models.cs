using System.Text.Json.Serialization;

namespace AppleEverestBuilder;

internal static class ProductPolicy
{
    public const int MaxFiles = 4096;
    public const long MaxExpandedBytes = 256L * 1024 * 1024;
    public const long MaxSingleFileBytes = 64L * 1024 * 1024;
    public const int MaxPathDepth = 24;
    public const int MaxYamlBytes = 1024 * 1024;
    public const string TransformerVersion = "apple-everest-static-v5";
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
    ON_HOOK_DEFERRED,
    IL_HOOK_DEFERRED,
    DIRECT_HOOK_DEFERRED,
    DYNAMIC_TARGET_DEFERRED,
    DYNAMIC_DETOUR_DEFERRED,
    DETOUR_CONFIG_DEFERRED,
    DYNAMIC_CODE_UNSUPPORTED,
    NATIVE_UNSUPPORTED,
    LUA_UNSUPPORTED,
    PLATFORM_UNSUPPORTED
}

internal sealed record FileRecord(string Path, long Bytes, string Sha256);
internal sealed record ContentMountRecord(string Owner, int Order, string SourcePath, string LogicalPath, string Sha256);
internal sealed record FrozenAssemblyRecord(string Owner, string AssemblyName, string FileName, string OriginalSha256, string FrozenSha256);

internal sealed class ManagedDetourTargetCatalog
{
    public int SchemaVersion { get; set; }
    public ManagedDetourTarget[] Targets { get; set; } = [];
}

internal sealed class ManagedDetourTarget
{
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
    string ConstructorSignature);

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
}
