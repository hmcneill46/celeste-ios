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
    internal const string ChronoSpriteBankSourcePath = "Graphics/ChronoHelper/CustomSprites.xml";
    internal const string ChronoSpriteBankApplePath =
        "AppleEverest/Mods/ChronoHelper/Graphics/ChronoHelper/CustomSprites.xml";
    internal const string DJSpriteBankSourcePath = "Graphics/DJMapHelperSprites.xml";
    internal const string DJSpriteBankApplePath =
        "AppleEverest/Mods/DJMapHelper/Graphics/DJMapHelperSprites.xml";

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
        if (metadata.Name == StaticConfiguredDetourCompatibility.LunaticName &&
            metadata.Version == StaticConfiguredDetourCompatibility.LunaticVersion &&
            input.SourceSha256 == StaticConfiguredDetourCompatibility.LunaticSourceSha256 &&
            metadata.DLL == StaticConfiguredDetourCompatibility.LunaticDllPath)
        {
            Validate(metadata, input, StaticConfiguredDetourCompatibility.LunaticDllPath,
                StaticConfiguredDetourCompatibility.LunaticDllSha256, "Everest", "1.1703.0");
            return new("lunatichelper-1.1.1-configured-fixture-aot-v1",
                StaticConfiguredDetourCompatibility.LunaticName,
                StaticConfiguredDetourCompatibility.LunaticVersion,
                StaticConfiguredDetourCompatibility.LunaticSourceSha256,
                StaticConfiguredDetourCompatibility.LunaticDllPath,
                StaticConfiguredDetourCompatibility.LunaticDllSha256, true);
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

        if (plan.Owner is ChronoName or DJName)
            RewritePinnedEverestOptionalParameterAbi(assembly, plan);

        if (plan.Owner == ChronoName)
            RewriteChronoNamespacedContentPath(assembly);
        else if (plan.Owner == DJName)
        {
            RewriteDJNamespacedContentPath(assembly);
            RewriteDJFastReflection(assembly);
        }

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

    /// <summary>
    /// ChronoHelper's desktop package loads its sprite bank through a
    /// TitleContainer-relative path. Static Apple closures deliberately mount
    /// every mod below its own namespaced root, so retain that isolation by
    /// rewriting the one hash-locked 1.3.3 call site instead of publishing a
    /// shared root alias which could collide with another mod.
    /// </summary>
    private static void RewriteChronoNamespacedContentPath(AssemblyDefinition assembly)
    {
        TypeDefinition module = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
            type.FullName == "Celeste.Mod.ChronoHelper.ChronoHelper");
        MethodDefinition loadContent = module.Methods.Single(method =>
            method.Name == "LoadContent" && method.Parameters.Count == 1 &&
            method.Parameters[0].ParameterType.FullName == "System.Boolean" && method.HasBody);
        Instruction[] sourcePaths = loadContent.Body.Instructions.Where(instruction =>
            instruction.OpCode == OpCodes.Ldstr &&
            string.Equals(instruction.Operand as string, ChronoSpriteBankSourcePath,
                StringComparison.Ordinal)).ToArray();
        if (sourcePaths.Length != 1 || loadContent.Body.Instructions.Any(instruction =>
                instruction.OpCode == OpCodes.Ldstr &&
                string.Equals(instruction.Operand as string, ChronoSpriteBankApplePath,
                    StringComparison.Ordinal)))
            throw new InvalidDataException("ChronoHelper sprite-bank content path census drifted");
        sourcePaths[0].Operand = ChronoSpriteBankApplePath;
    }

    private static void RewriteDJNamespacedContentPath(AssemblyDefinition assembly)
    {
        TypeDefinition feather = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
            type.FullName == "Celeste.Mod.DJMapHelper.Entities.ColorfulFlyFeather");
        MethodDefinition loadContent = feather.Methods.Single(method =>
            method.Name == "OnLoadContent" && method.Parameters.Count == 0 && method.HasBody);
        Instruction[] sourcePaths = loadContent.Body.Instructions.Where(instruction =>
            instruction.OpCode == OpCodes.Ldstr &&
            string.Equals(instruction.Operand as string, DJSpriteBankSourcePath,
                StringComparison.Ordinal)).ToArray();
        if (sourcePaths.Length != 1 || loadContent.Body.Instructions.Any(instruction =>
                instruction.OpCode == OpCodes.Ldstr &&
                string.Equals(instruction.Operand as string, DJSpriteBankApplePath,
                    StringComparison.Ordinal)))
            throw new InvalidDataException("DJMapHelper sprite-bank content path census drifted");
        sourcePaths[0].Operand = DJSpriteBankApplePath;
    }

    private static void RewriteDJFastReflection(AssemblyDefinition assembly)
    {
        TypeDefinition fast = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
            type.FullName == "Celeste.Mod.DJMapHelper.Extensions.FastReflection");
        MethodDefinition[] publicFactories = fast.Methods.Where(method => method.Name == "CreateGetDelegate" &&
            method.GenericParameters.Count == 2).ToArray();
        if (publicFactories.Length != 2)
            throw new InvalidDataException("DJMapHelper FastReflection factory census drifted");

        Dictionary<string, MethodReference> getters = new(StringComparer.Ordinal)
        {
            ["Celeste.Player|System.Single|starFlyTimer"] = ExactStaticFieldGetterReference(assembly,
                "PlayerStarFlyTimer", "Celeste", "Player",
                assembly.MainModule.TypeSystem.Single),
            ["Celeste.Player|Microsoft.Xna.Framework.Color|starFlyColor"] = ExactStaticFieldGetterReference(assembly,
                "PlayerStarFlyColor", "Celeste", "Player",
                assembly.MainModule.GetTypeReferences().Single(type => type.FullName == "Microsoft.Xna.Framework.Color")),
            ["Celeste.FlyFeather|System.Boolean|shielded"] = ExactStaticFieldGetterReference(assembly,
                "FlyFeatherShielded", "Celeste", "FlyFeather",
                assembly.MainModule.TypeSystem.Boolean)
        };

        int rewritten = 0;
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (MethodDefinition owner in assembly.MainModule.Types.SelectMany(AllTypes)
                     .SelectMany(type => type.Methods).Where(method => method.HasBody))
        {
            ILProcessor il = owner.Body.GetILProcessor();
            foreach (Instruction instruction in owner.Body.Instructions.ToArray())
            {
                if (instruction.OpCode != OpCodes.Call || instruction.Operand is not GenericInstanceMethod call ||
                    call.ElementMethod.DeclaringType.FullName != fast.FullName ||
                    call.ElementMethod.Name != "CreateGetDelegate" || call.GenericArguments.Count != 2 ||
                    call.Parameters.Count != 1)
                    continue;
                Instruction fieldName = instruction.Previous;
                if (fieldName?.OpCode != OpCodes.Ldstr || fieldName.Operand is not string name)
                    throw new InvalidDataException("DJMapHelper FastReflection call is not an exact constant field lookup");
                string key = $"{call.GenericArguments[0].FullName}|{call.GenericArguments[1].FullName}|{name}";
                if (!getters.TryGetValue(key, out MethodReference? getter) || !seen.Add(key))
                    throw new InvalidDataException("DJMapHelper FastReflection lookup census drifted: " + key);

                GenericInstanceType delegateType = new(assembly.MainModule.ImportReference(
                    call.ReturnType.GetElementType()));
                foreach (TypeReference argument in call.GenericArguments)
                    delegateType.GenericArguments.Add(assembly.MainModule.ImportReference(argument));
                MethodReference constructor = new(".ctor", assembly.MainModule.TypeSystem.Void, delegateType)
                {
                    HasThis = true,
                    CallingConvention = MethodCallingConvention.Default
                };
                constructor.Parameters.Add(new ParameterDefinition(assembly.MainModule.TypeSystem.Object));
                constructor.Parameters.Add(new ParameterDefinition(assembly.MainModule.TypeSystem.IntPtr));
                fieldName.OpCode = OpCodes.Ldnull;
                fieldName.Operand = null;
                instruction.OpCode = OpCodes.Ldftn;
                instruction.Operand = getter;
                il.InsertAfter(instruction, il.Create(OpCodes.Newobj, constructor));
                rewritten++;
            }
        }
        if (rewritten != 3 || seen.Count != getters.Count)
            throw new InvalidDataException($"DJMapHelper FastReflection exact lookup count drifted: {rewritten}");

        foreach (MethodDefinition method in publicFactories) fast.Methods.Remove(method);
        foreach (MethodDefinition method in fast.Methods.Where(method => method.Name == "CreateGetDelegateImpl").ToArray())
            fast.Methods.Remove(method);
        foreach (MethodDefinition method in fast.Methods.Where(method => method.IsConstructor && method.IsStatic).ToArray())
            fast.Methods.Remove(method);
        fast.Fields.Clear();
        fast.NestedTypes.Clear();
    }

    private static MethodReference ExactStaticFieldGetterReference(AssemblyDefinition assembly,
        string methodName, string targetNamespace, string targetName, TypeReference fieldType)
    {
        AssemblyNameReference celeste = assembly.MainModule.AssemblyReferences.Single(reference => reference.Name == "Celeste");
        TypeReference target = new(targetNamespace, targetName, assembly.MainModule, celeste);
        TypeReference resultType = assembly.MainModule.ImportReference(fieldType);
        TypeReference facade = new("Celeste.Mod", "AppleEverestStaticFieldAccess", assembly.MainModule, celeste);
        MethodReference method = new(methodName, resultType, facade) { HasThis = false };
        method.Parameters.Add(new ParameterDefinition(target));
        return method;
    }

    private static void RewritePinnedEverestOptionalParameterAbi(AssemblyDefinition assembly,
        StaticAotCompatibilityPlan plan)
    {
        int expectedRumble = plan.Owner == ChronoName ? 16 : 13;
        int expectedSine = plan.Owner == DJName ? 2 : 0;
        int expectedTrail = plan.Owner == DJName ? 1 : 0;
        int rumble = 0;
        int sine = 0;
        int trail = 0;
        foreach (MethodDefinition owner in assembly.MainModule.Types.SelectMany(AllTypes)
                     .SelectMany(type => type.Methods).Where(method => method.HasBody))
        {
            ILProcessor il = owner.Body.GetILProcessor();
            foreach (Instruction instruction in owner.Body.Instructions.ToArray())
            {
                if (instruction.Operand is not MethodReference called) continue;
                if (called.DeclaringType.FullName == "Celeste.Input" && called.Name == "Rumble" &&
                    called.Parameters.Count == 2)
                {
                    il.InsertBefore(instruction, il.Create(OpCodes.Ldnull));
                    instruction.Operand = AppendParameter(assembly, called, assembly.MainModule.TypeSystem.String);
                    rumble++;
                }
                else if (called.DeclaringType.FullName == "Monocle.SineWave" && called.Name == ".ctor" &&
                         called.Parameters.Count == 1)
                {
                    il.InsertBefore(instruction, il.Create(OpCodes.Ldc_R4, 0f));
                    instruction.Operand = AppendParameter(assembly, called, assembly.MainModule.TypeSystem.Single);
                    sine++;
                }
                else if (called.DeclaringType.FullName == "Celeste.TrailManager" && called.Name == "Add" &&
                         called.Parameters.Count == 3)
                {
                    il.InsertBefore(instruction, il.Create(OpCodes.Ldc_I4_0));
                    il.InsertBefore(instruction, il.Create(OpCodes.Ldc_I4_0));
                    MethodReference four = AppendParameter(assembly, called, assembly.MainModule.TypeSystem.Boolean);
                    instruction.Operand = AppendParameter(assembly, four, assembly.MainModule.TypeSystem.Boolean);
                    trail++;
                }
            }
        }
        if (rumble != expectedRumble || sine != expectedSine || trail != expectedTrail)
            throw new InvalidDataException($"{plan.Owner} pinned Everest optional-parameter ABI drifted: " +
                $"rumble={rumble}/{expectedRumble}; sine={sine}/{expectedSine}; trail={trail}/{expectedTrail}");
    }

    private static MethodReference AppendParameter(AssemblyDefinition assembly, MethodReference source,
        TypeReference parameterType)
    {
        MethodReference replacement = new(source.Name, assembly.MainModule.ImportReference(source.ReturnType),
            assembly.MainModule.ImportReference(source.DeclaringType))
        {
            HasThis = source.HasThis,
            ExplicitThis = source.ExplicitThis,
            CallingConvention = source.CallingConvention
        };
        foreach (ParameterDefinition parameter in source.Parameters)
            replacement.Parameters.Add(new ParameterDefinition(assembly.MainModule.ImportReference(parameter.ParameterType)));
        replacement.Parameters.Add(new ParameterDefinition(assembly.MainModule.ImportReference(parameterType)));
        return replacement;
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
        public Dictionary<string, object> Data => global::Celeste.Mod.AppleEverestStaticFieldAccess.Data(target);
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
        private static readonly ConditionalWeakTable<object, ExtraData>.CreateValueCallback ExtraDataFactory =
            static _ => new ExtraData();

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
                default: Extras.GetValue(target, ExtraDataFactory).Values[key] = value; return;
            }
        }

        public static Dictionary<string, object> Data(object target) =>
            Extras.GetValue(target, ExtraDataFactory).Values;

        public static float PlayerStarFlyTimer(global::Celeste.Player value) => value.starFlyTimer;
        public static Color PlayerStarFlyColor(global::Celeste.Player value) => value.starFlyColor;
        public static bool FlyFeatherShielded(global::Celeste.FlyFeather value) => value.shielded;

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
        PatchPinnedEverestHelperAbi(managedRoot);
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

    internal static void PatchRuntimeRequiredGameSources(string managedRoot)
    {
        Replace(Path.Combine(managedRoot, "Celeste", "Player.cs"),
            "\tprivate float dashCooldownTimer;", "\tinternal float dashCooldownTimer;");
        Replace(Path.Combine(managedRoot, "Celeste", "Strawberry.cs"),
            "\tprivate bool collected;", "\tinternal bool collected;");
        Replace(Path.Combine(managedRoot, "Celeste", "Input.cs"),
            "\tpublic static void Rumble(RumbleStrength strength, RumbleLength length, [CallerMemberName] string source = null)\n\t{",
            "\t// Preserve the two-parameter binary ABI used by pinned desktop Everest helpers.\n" +
            "\tpublic static void Rumble(RumbleStrength strength, RumbleLength length) =>\n" +
            "\t\tRumble(strength, length, null);\n\n" +
            "\tpublic static void Rumble(RumbleStrength strength, RumbleLength length, [CallerMemberName] string source = null)\n\t{");
    }

    /// <summary>
    /// Reproduces the small, public Everest ABI surface used by the exact
    /// ChronoHelper binary. These are ordinary source members in the static
    /// Apple product: no runtime patcher or reflection fallback is involved.
    /// </summary>
    private static void PatchPinnedEverestHelperAbi(string managedRoot)
    {
        Replace(Path.Combine(managedRoot, "Celeste", "DashListener.cs"),
            "\tpublic DashListener()\n\t\t: base(active: false, visible: false)\n\t{\n\t}",
            "\tpublic DashListener()\n\t\t: base(active: false, visible: false)\n\t{\n\t}\n\n" +
            "\tpublic DashListener(Action<Vector2> onDash) : this()\n\t{\n\t\tOnDash = onDash;\n\t}");

        Replace(Path.Combine(managedRoot, "Celeste", "Holdable.cs"),
            "\tpublic Func<Vector2> SpeedGetter;",
            "\tpublic Func<Vector2> SpeedGetter;\n\n\tpublic Action<Vector2> SpeedSetter;");
        Replace(Path.Combine(managedRoot, "Celeste", "Holdable.cs"),
            "\tpublic Vector2 GetSpeed()\n\t{",
            "\tpublic void SetSpeed(Vector2 speed)\n\t{\n\t\tSpeedSetter?.Invoke(speed);\n\t}\n\n" +
            "\tpublic Vector2 GetSpeed()\n\t{");

        string autotiler = Path.Combine(managedRoot, "Celeste", "Autotiler.cs");
        Replace(autotiler,
            "\t\tpublic Tiles Padded = new Tiles();",
            "\t\tpublic Tiles Padded = new Tiles();\n\n\t\tpublic string Debris;");
        Replace(autotiler,
            "\tprivate void ReadInto(TerrainType data, Tileset tileset, XmlElement xml)\n\t{",
            "\tprivate void ReadInto(TerrainType data, Tileset tileset, XmlElement xml)\n\t{\n" +
            "\t\tif (xml.HasAttr(\"debris\")) data.Debris = xml.Attr(\"debris\");");
        Replace(autotiler,
            "\tpublic Generated GenerateMap(VirtualMap<char> mapData, Behaviour behaviour)",
            "\tpublic bool TryGetCustomDebris(out string path, char tiletype)\n\t{\n" +
            "\t\tTerrainType terrain;\n\t\tpath = lookup.TryGetValue(tiletype, out terrain) ? terrain.Debris : null;\n" +
            "\t\treturn !string.IsNullOrEmpty(path);\n\t}\n\n" +
            "\tpublic Generated GenerateMap(VirtualMap<char> mapData, Behaviour behaviour)");
    }

    private static readonly (string File, string Declaration)[] ReviewedInternalFields =
    [
        ("LightningRenderer.cs", "List<Lightning> list = new List<Lightning>();"),
        ("DashBlock.cs", "bool canDash;"),
        ("Player.cs", "Vector2 boostTarget;"),
        ("Player.cs", "Color starFlyColor = Calc.HexToColor(\"ffd65c\");"), ("Player.cs", "float starFlyTimer;"),
        ("Player.cs", "Vector2 beforeDashSpeed;"), ("Player.cs", "float varJumpSpeed;"),
        ("Player.cs", "FlingBird flingBird;"), ("Player.cs", "int forceMoveX;"), ("Player.cs", "bool boostRed;"),
        ("FlyFeather.cs", "bool shielded;"), ("FinalBoss.cs", "PlayerHair normalHair;"),
        ("FinalBoss.cs", "Vector2[] nodes;"), ("FinalBoss.cs", "int patternIndex;"),
        ("FinalBoss.cs", "Coroutine attackCoroutine;"), ("Refill.cs", "Sprite sprite;"),
        ("Refill.cs", "Sprite flash;"), ("CrystalStaticSpinner.cs", "CrystalColor color;"),
        ("Spring.cs", "Sprite sprite;")
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
