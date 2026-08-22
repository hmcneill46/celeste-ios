using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AppleEverestBuilder;

/// <summary>
/// Exact, hash-locked static replacements for small runtime surfaces in the
/// authoritative ChronoHelper/DJMapHelper binaries selected by Stage 25I-A.
/// This registry is deliberately closed: unknown releases retain the normal
/// compatibility rejection instead of inheriting a broad reflection facade.
/// </summary>
internal static class StaticAotCompatibility
{
    internal const string ChronoName = "ChronoHelper";
    internal const string ChronoVersion = "1.3.3";
    internal const string ChronoSourceSha256 = "3ada281adf3affd433bcd02eb9966abab662bf5ab23714095460f4de7eaff5f6";
    internal const string ChronoDllPath = "bin/Debug/net452/ChronoHelper.dll";
    internal const string ChronoDllSha256 = "214b26a5d7e3f17e93d3c388a3a6066dd137f9ce09b6203a4e1a482ba342a2dc";
    internal const string DJName = "DJMapHelper";
    internal const string DJVersion = "1.13.4";
    internal const string DJSourceSha256 = "bdfe229e6bc24e0addea3983b5f209a6d420a3d9924e3bafa609ee6ce4c2aa95";
    internal const string DJDllPath = "DJMapHelper.dll";
    internal const string DJDllSha256 = "0d73202b5e16f26a4c8a286dd6ad600c1601d1904d8f72d909f8acb8469ee446";

    internal static StaticAotCompatibilityPlan? Resolve(ModInput input, EverestYamlEntry metadata)
    {
        if (metadata.Name == ChronoName && metadata.Version == ChronoVersion &&
            input.SourceSha256 == ChronoSourceSha256 && metadata.DLL == ChronoDllPath)
        {
            Validate(metadata, input, ChronoDllPath, ChronoDllSha256, "Everest", "1.0.0");
            return new("chronohelper-1.3.3-static-aot-v1", ChronoName, ChronoVersion,
                ChronoSourceSha256, ChronoDllPath, ChronoDllSha256, true);
        }
        if (metadata.Name == DJName && metadata.Version == DJVersion &&
            input.SourceSha256 == DJSourceSha256 && metadata.DLL == DJDllPath)
        {
            Validate(metadata, input, DJDllPath, DJDllSha256, "Everest", "1.1963.0");
            return new("djmaphelper-1.13.4-static-aot-v1", DJName, DJVersion,
                DJSourceSha256, DJDllPath, DJDllSha256, false);
        }
        return null;
    }

    private static void Validate(EverestYamlEntry metadata, ModInput input, string dllPath, string dllSha,
        string dependency, string dependencyVersion)
    {
        string dll = Path.Combine(input.StagingRoot, dllPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(dll) || Hashing.FileSha256(dll) != dllSha)
            throw new InvalidDataException($"{metadata.Name} static-AOT DLL hash mismatch");
        if (metadata.Dependencies.Count != 1 || metadata.Dependencies[0].Name != dependency ||
            metadata.Dependencies[0].Version != dependencyVersion || metadata.OptionalDependencies.Count != 0 ||
            metadata.Conflicts.Count != 0)
            throw new InvalidDataException($"{metadata.Name} static-AOT metadata drifted");
    }

    internal static bool AllowsMonoModType(StaticAotCompatibilityPlan? plan, string fullName) => plan != null &&
        fullName is "MonoMod.Utils.DynData`1" or "MonoMod.Utils.GetDelegate`2" or "MonoMod.Utils.Extensions";

    internal static bool AllowsDynamicMethod(StaticAotCompatibilityPlan? plan) => plan?.Owner == DJName;

    internal static void RewriteDeviceAssembly(AssemblyDefinition assembly, StaticAotCompatibilityPlan? plan)
    {
        if (plan == null) return;
        if (assembly.Name.Name != plan.Owner)
            throw new InvalidDataException("static-AOT compatibility plan owner mismatch");

        foreach (TypeDefinition custom in assembly.MainModule.Types.SelectMany(AllTypes).Where(type =>
                     type.CustomAttributes.Any(attribute =>
                         attribute.AttributeType.FullName == "Celeste.Mod.Entities.CustomEntityAttribute")))
            MakePublic(custom);

        if (plan.Owner == DJName)
            RewriteDJFastReflection(assembly);

        AssemblyNameReference? monoMod = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference =>
            reference.Name == "MonoMod.Utils");
        AssemblyNameReference? celeste = assembly.MainModule.AssemblyReferences.SingleOrDefault(reference =>
            reference.Name == "Celeste");
        if (monoMod != null)
        {
            if (celeste == null) throw new InvalidDataException("static-AOT compatibility requires Celeste reference");
            TypeReference[] live = assembly.MainModule.GetTypeReferences().Where(type =>
                ReferenceEquals(type.Scope, monoMod) && AllowsMonoModType(plan, type.FullName)).ToArray();
            foreach (TypeReference type in live) type.Scope = celeste;
        }
    }

    private static void RewriteDJFastReflection(AssemblyDefinition assembly)
    {
        TypeDefinition fast = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
            type.FullName == "Celeste.Mod.DJMapHelper.Extensions.FastReflection");
        MethodDefinition[] publicFactories = fast.Methods.Where(method => method.Name == "CreateGetDelegate" &&
            method.GenericParameters.Count == 2).ToArray();
        if (publicFactories.Length != 2)
            throw new InvalidDataException("DJMapHelper FastReflection factory census drifted");
        foreach (MethodDefinition method in publicFactories)
        {
            method.Body.Instructions.Clear();
            method.Body.ExceptionHandlers.Clear();
            method.Body.Variables.Clear();
            method.Body.InitLocals = false;
            ILProcessor il = method.Body.GetILProcessor();
            il.Append(il.Create(method.Parameters.Count == 1 ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
            TypeReference access = new("Celeste.Mod", "AppleEverestStaticFieldAccess",
                assembly.MainModule, assembly.MainModule.AssemblyReferences.Single(reference => reference.Name == "Celeste"));
            MethodReference factory = new("CreateGetDelegate", method.ReturnType, access)
            {
                HasThis = false,
                CallingConvention = MethodCallingConvention.Default
            };
            factory.Parameters.Add(new ParameterDefinition(assembly.MainModule.TypeSystem.String));
            factory.GenericParameters.Add(new GenericParameter("TInstance", factory));
            factory.GenericParameters.Add(new GenericParameter("TResult", factory));
            GenericInstanceMethod call = new(factory);
            call.GenericArguments.Add(method.GenericParameters[0]);
            call.GenericArguments.Add(method.GenericParameters[1]);
            il.Append(il.Create(OpCodes.Call, call));
            il.Append(il.Create(OpCodes.Ret));
        }
        foreach (MethodDefinition method in fast.Methods.Where(method => method.Name == "CreateGetDelegateImpl").ToArray())
            fast.Methods.Remove(method);
        foreach (MethodDefinition method in fast.Methods.Where(method => method.IsConstructor && method.IsStatic).ToArray())
            fast.Methods.Remove(method);
        fast.Fields.Clear();
        fast.NestedTypes.Clear();
    }

    internal static string GeneratedSource(IReadOnlyList<ResolvedMod> mods)
    {
        bool chrono = mods.Any(mod => mod.StaticAotCompatibility?.Owner == ChronoName);
        bool dj = mods.Any(mod => mod.StaticAotCompatibility?.Owner == DJName);
        if (!chrono && !dj) return "";
        return $$"""
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Monocle;

namespace MonoMod.Utils
{
    public delegate TResult GetDelegate<in TInstance, out TResult>(TInstance instance);

    public sealed class DynData<T> where T : class
    {
        private readonly T target;
        public DynData(T target) => this.target = target ?? throw new ArgumentNullException(nameof(target));
        public object this[string key]
        {
            get => global::Celeste.Mod.AppleEverestStaticFieldAccess.Get(target, key);
            set => global::Celeste.Mod.AppleEverestStaticFieldAccess.Set(target, key, value);
        }
        public TValue Get<TValue>(string key) => (TValue)GetValue(key);
        public void Set<TValue>(string key, TValue value) =>
            global::Celeste.Mod.AppleEverestStaticFieldAccess.Set(target, key, value);
        private object GetValue(string key) => global::Celeste.Mod.AppleEverestStaticFieldAccess.Get(target, key);
    }
}

namespace Celeste.Mod
{
    public static class AppleEverestStaticFieldAccess
    {
        private sealed class ExtraData { internal readonly Dictionary<string, object> Values = new(StringComparer.Ordinal); }
        private static readonly ConditionalWeakTable<object, ExtraData> Extras = new();

        public static object Get(object target, string key) => (target, key) switch
        {
            (global::Celeste.Strawberry value, "rotateWiggler") => value.rotateWiggler,
            (global::Celeste.Strawberry value, "flapSpeed") => value.flapSpeed,
            (global::Celeste.Strawberry value, "Winged") => value.Winged,
            (global::Celeste.Strawberry value, "flyingAway") => value.flyingAway,
            (global::Celeste.LightningRenderer value, "list") => value.list,
            (global::Celeste.DashBlock value, "canDash") => value.canDash,
            (global::Celeste.Player value, "dashCooldownTimer") => value.dashCooldownTimer,
            (global::Celeste.Player value, "boostTarget") => value.boostTarget,
            _ when Extras.TryGetValue(target, out ExtraData extra) && extra.Values.TryGetValue(key, out object value) => value,
            _ => throw new MissingFieldException(target.GetType().FullName, key)
        };

        public static void Set(object target, string key, object value)
        {
            switch (target, key)
            {
                case (global::Celeste.Strawberry item, "flapSpeed"): item.flapSpeed = (float)value; return;
                case (global::Celeste.Strawberry item, "Winged"): item.Winged = (bool)value; return;
                case (global::Celeste.Strawberry item, "flyingAway"): item.flyingAway = (bool)value; return;
                case (global::Celeste.Player item, "dashCooldownTimer"): item.dashCooldownTimer = (float)value; return;
                case (global::Celeste.Player item, "boostTarget"): item.boostTarget = (Vector2)value; return;
                default: Extras.GetOrCreateValue(target).Values[key] = value; return;
            }
        }

        public static global::MonoMod.Utils.GetDelegate<TInstance, TResult> CreateGetDelegate<TInstance, TResult>(string fieldName)
        {
            if (typeof(TInstance) == typeof(global::Celeste.Player) && typeof(TResult) == typeof(float) && fieldName == "starFlyTimer")
                return (global::MonoMod.Utils.GetDelegate<TInstance, TResult>)(object)
                    (global::MonoMod.Utils.GetDelegate<global::Celeste.Player, float>)(value => value.starFlyTimer);
            if (typeof(TInstance) == typeof(global::Celeste.Player) && typeof(TResult) == typeof(Color) && fieldName == "starFlyColor")
                return (global::MonoMod.Utils.GetDelegate<TInstance, TResult>)(object)
                    (global::MonoMod.Utils.GetDelegate<global::Celeste.Player, Color>)(value => value.starFlyColor);
            if (typeof(TInstance) == typeof(global::Celeste.FlyFeather) && typeof(TResult) == typeof(bool) && fieldName == "shielded")
                return (global::MonoMod.Utils.GetDelegate<TInstance, TResult>)(object)
                    (global::MonoMod.Utils.GetDelegate<global::Celeste.FlyFeather, bool>)(value => value.shielded);
            throw new MissingFieldException(typeof(TInstance).FullName, fieldName);
        }

        internal static void RootReviewedReflectionMembers()
        {
            Action<global::Celeste.Player> player = value =>
            {
                GC.KeepAlive(value.beforeDashSpeed); GC.KeepAlive(value.varJumpSpeed);
                GC.KeepAlive(value.flingBird); GC.KeepAlive(value.forceMoveX);
                GC.KeepAlive(value.boostRed); GC.KeepAlive(value.starFlyColor);
            };
            Action<global::Celeste.FinalBoss> boss = value =>
            {
                GC.KeepAlive(value.patternIndex); GC.KeepAlive(value.normalHair);
                GC.KeepAlive(value.nodes); GC.KeepAlive(value.attackCoroutine);
            };
            Action<global::Celeste.Refill> refill = value => { GC.KeepAlive(value.sprite); GC.KeepAlive(value.flash); };
            Action<global::Celeste.CrystalStaticSpinner> spinner = value => GC.KeepAlive(value.color);
            Action<global::Celeste.Strawberry> berry = value => GC.KeepAlive(value.collected);
            Action<global::Celeste.Spring> spring = value => { GC.KeepAlive(value.sprite); value.BounceAnimate(); };
            Action solid = () => GC.KeepAlive(global::Celeste.Solid.riders);
            GC.KeepAlive(player); GC.KeepAlive(boss); GC.KeepAlive(refill); GC.KeepAlive(spinner);
            GC.KeepAlive(berry); GC.KeepAlive(spring); GC.KeepAlive(solid);
        }
    }
}
""";
    }

    internal static void PatchGameSources(string managedRoot)
    {
        Replace(Path.Combine(managedRoot, "Celeste", "Strawberry.cs"),
            "\tprivate Wiggler rotateWiggler;", "\tinternal Wiggler rotateWiggler;");
        Replace(Path.Combine(managedRoot, "Celeste", "Strawberry.cs"),
            "\tprivate bool flyingAway;", "\tinternal bool flyingAway;");
        Replace(Path.Combine(managedRoot, "Celeste", "Strawberry.cs"),
            "\tprivate float flapSpeed;", "\tinternal float flapSpeed;");
        Replace(Path.Combine(managedRoot, "Celeste", "Strawberry.cs"),
            "\tpublic bool Winged { get; private set; }", "\tpublic bool Winged { get; internal set; }");
        foreach ((string file, string declaration) in ReviewedInternalFields)
            Replace(Path.Combine(managedRoot, "Celeste", file), "\tprivate " + declaration,
                "\tinternal " + declaration);
        Replace(Path.Combine(managedRoot, "Celeste", "Solid.cs"),
            "\tprivate static HashSet<Actor> riders = new HashSet<Actor>();",
            "\tinternal static HashSet<Actor> riders = new HashSet<Actor>();");
        Replace(Path.Combine(managedRoot, "Celeste", "Spring.cs"),
            "\tprivate void BounceAnimate()", "\tinternal void BounceAnimate()");
    }

    private static readonly (string File, string Declaration)[] ReviewedInternalFields =
    [
        ("LightningRenderer.cs", "List<Lightning> list = new List<Lightning>();"),
        ("DashBlock.cs", "bool canDash;"),
        ("Player.cs", "float dashCooldownTimer;"), ("Player.cs", "Vector2 boostTarget;"),
        ("Player.cs", "Color starFlyColor = Calc.HexToColor(\"ffd65c\");"), ("Player.cs", "float starFlyTimer;"),
        ("Player.cs", "Vector2 beforeDashSpeed;"), ("Player.cs", "float varJumpSpeed;"),
        ("Player.cs", "FlingBird flingBird;"), ("Player.cs", "int forceMoveX;"), ("Player.cs", "bool boostRed;"),
        ("FlyFeather.cs", "bool shielded;"), ("FinalBoss.cs", "PlayerHair normalHair;"),
        ("FinalBoss.cs", "Vector2[] nodes;"), ("FinalBoss.cs", "int patternIndex;"),
        ("FinalBoss.cs", "Coroutine attackCoroutine;"), ("Refill.cs", "Sprite sprite;"),
        ("Refill.cs", "Sprite flash;"), ("CrystalStaticSpinner.cs", "CrystalColor color;"),
        ("Strawberry.cs", "bool collected;"), ("Spring.cs", "Sprite sprite;")
    ];

    private static void Replace(string path, string before, string after)
    {
        string text = File.ReadAllText(path);
        int first = text.IndexOf(before, StringComparison.Ordinal);
        if (first < 0 || first != text.LastIndexOf(before, StringComparison.Ordinal))
            throw new InvalidDataException("static-AOT reviewed game member drifted: " + Path.GetFileName(path) + ":" + before);
        File.WriteAllText(path, text[..first] + after + text[(first + before.Length)..], new UTF8Encoding(false));
    }

    private static void MakePublic(TypeDefinition type)
    {
        if (type.DeclaringType == null)
            type.Attributes = (type.Attributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.Public;
        else
        {
            MakePublic(type.DeclaringType);
            type.Attributes = (type.Attributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.NestedPublic;
        }
    }

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition type)
    {
        yield return type;
        foreach (TypeDefinition nested in type.NestedTypes)
            foreach (TypeDefinition value in AllTypes(nested)) yield return value;
    }
}
