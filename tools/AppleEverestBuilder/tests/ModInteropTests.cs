using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Diagnostics;
using System.Text.Json;

namespace AppleEverestBuilder;

internal static class ModInteropTests
{
    internal static int Run(string repository, string temporary)
    {
        int passed = 0;
        void Pass(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            passed++;
        }
        void Throws(Action action, string text, string name)
        {
            try { action(); }
            catch (Exception exception) when (exception.Message.Contains(text, StringComparison.Ordinal))
            {
                passed++;
                return;
            }
            throw new InvalidOperationException("FAIL: " + name);
        }

        string fixture = Path.Combine(temporary, "ModInteropFixture.dll");
        CreateFixture(fixture, dynamicRegistration: false);
        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(fixture))
        {
            List<string> records = [];
            IReadOnlyList<ModInteropRegistrationPlan> plans = ModInteropPlanner.Analyze(assembly, "Fixture", true, records.Add);
            Pass(plans.Count == 4, "four statically registered ModInterop types");
            Pass(plans.Sum(plan => plan.CallSites.Count) == 5, "duplicate typeof callsites retained in plan");
            Pass(plans.Sum(plan => plan.Exports.Count) == 8, "public static exports and overloads inventoried");
            Pass(plans.Sum(plan => plan.Imports.Count) == 10, "qualified, explicit, unqualified, closed-generic, and missing imports inventoried");
            Pass(records.All(value => value.StartsWith("static-modinterop:", StringComparison.Ordinal)),
                "static registration mechanisms reported");

            ResolvedMod mod = Stub(plans);
            GeneratedModInteropPlan generated = ModInteropPlanner.Generate([mod]);
            Pass(generated.RegistrationCount == 4 && generated.ExportCount == 8 && generated.ImportCount == 10 &&
                 generated.ResolvedImportCount == 9, "typed plan resolves supported imports and leaves missing import null");
            Pass(generated.Source.Contains("new global::System.Func<string, string>(global::Fixture.Provider.Echo)", StringComparison.Ordinal) &&
                 generated.Source.Contains("new global::System.Func<string, object>(global::Fixture.Provider.Widen)", StringComparison.Ordinal) &&
                 generated.Source.Contains("new global::System.Func<object>(global::Fixture.Provider.Narrow)", StringComparison.Ordinal) &&
                 generated.Source.Contains("new global::Fixture.RefTransform(global::Fixture.Provider.Touch)", StringComparison.Ordinal) &&
                 generated.Source.Contains("new global::Fixture.Transform<string>(global::Fixture.Provider.Echo)", StringComparison.Ordinal) &&
                 generated.Source.Contains("new global::System.Func<System.Collections.Generic.List<string>, int>(global::Fixture.Provider.Count)", StringComparison.Ordinal) &&
                 generated.Source.Contains("global::Fixture.Provider.Choose", StringComparison.Ordinal) &&
                 generated.Source.Contains("bestOrdinal", StringComparison.Ordinal) &&
                 !generated.Source.Contains("GetMethods", StringComparison.Ordinal) &&
                 !generated.Source.Contains("CreateDelegate", StringComparison.Ordinal),
                "generated source uses direct typed assignment without reflection discovery");
            Pass(generated.PlanSha256.Length == 64, "interop plan has deterministic SHA-256");
            Pass(generated.PlanSha256 == ModInteropPlanner.Generate([mod]).PlanSha256,
                "interop plan SHA is deterministic for identical closed input");
            Pass(JsonSerializer.Serialize(generated.Manifest).Contains("OPTIONAL_INTEROP_PROVIDER_ABSENT", StringComparison.Ordinal),
                "undeclared missing provider is represented as optional null state");
            ResolvedMod required = Stub(plans);
            required.Metadata.Dependencies.Add(new EverestDependency { Name = "Api", Version = "1.0.0" });
            Throws(() => ModInteropPlanner.Generate([required]), "REQUIRED_INTEROP_PROVIDER_ABSENT",
                "required provider without a compatible export rejected before AOT");

            string frozen = Path.Combine(temporary, "ModInteropFixture.frozen.dll");
            _ = AssemblyFreezer.Freeze(fixture, frozen, [], plans);
            using AssemblyDefinition rewritten = AssemblyDefinition.ReadAssembly(frozen);
            Pass(rewritten.MainModule.AssemblyReferences.All(reference => reference.Name != "MonoMod.Utils") &&
                 rewritten.MainModule.GetTypeReferences().Where(type => type.Namespace == "MonoMod.ModInterop")
                     .All(type => type.Scope.Name == "Celeste"), "MonoMod.ModInterop ABI rebound narrowly to Celeste");

            RunStaticPlanHost(repository, temporary, frozen, generated.Source, "consumer-first");
            Pass(true, "generated static plan executes importer-first late refresh");
            RunStaticPlanHost(repository, temporary, frozen, generated.Source, "provider-first");
            Pass(true, "generated static plan executes provider-first binding");
            RunStaticPlanHost(repository, temporary, frozen, generated.Source, "provider-b-first");
            Pass(true, "generated static plan preserves first-compatible provider registration order");
        }

        string dynamic = Path.Combine(temporary, "ModInteropDynamic.dll");
        CreateFixture(dynamic, dynamicRegistration: true);
        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(dynamic))
            Throws(() => ModInteropPlanner.Analyze(assembly, "Dynamic", true, _ => { }),
                "DEFERRED_DYNAMIC_MODINTEROP_TYPE", "dynamic Type registration rejected before AOT");

        string openGeneric = Path.Combine(temporary, "ModInteropOpenGeneric.dll");
        CreateFixture(openGeneric, dynamicRegistration: false, openGenericExport: true);
        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(openGeneric))
            Throws(() => ModInteropPlanner.Analyze(assembly, "OpenGeneric", true, _ => { }),
                "DEFERRED_OPEN_GENERIC_MODINTEROP_METHOD", "open generic export rejected before AOT");

        string readOnly = Path.Combine(temporary, "ModInteropReadOnly.dll");
        CreateFixture(readOnly, dynamicRegistration: false, readOnlyImport: true);
        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(readOnly))
            Throws(() => ModInteropPlanner.Analyze(assembly, "ReadOnly", true, _ => { }),
                "DEFERRED_READONLY_MODINTEROP_IMPORT", "readonly delegate import rejected before AOT");

        return passed;
    }

    private static void RunStaticPlanHost(string repository, string temporary, string fixture, string generated, string order)
    {
        string root = Path.Combine(temporary, "static-host");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "global.json"), "{\"sdk\":{\"version\":\"8.0.424\",\"rollForward\":\"latestPatch\"}}\n");
        File.WriteAllText(Path.Combine(root, "StaticHost.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <AssemblyName>Celeste</AssemblyName>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>disable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(root, "GeneratedAppleEverestModInterop.cs"), generated);
        File.WriteAllText(Path.Combine(root, "Fixture.cs"), """
            using MonoMod.ModInterop;
            using System;
            using System.Collections.Generic;

            namespace Fixture
            {
                public delegate void RefTransform(ref int value);
                public delegate T Transform<T>(T value);

                [ModExportName("Api")]
                public static class Provider
                {
                    public static string Echo(string value) => value;
                    public static object Widen(object value) => value;
                    public static string Narrow() => "narrow";
                    public static void Touch(ref int value) { }
                    public static int Count(List<string> values) => values.Count;
                    public static string Choose(int value) => "wrong";
                    public static string Choose(string value) => value;
                }

                [ModExportName("Other")]
                public static class ProviderB
                {
                    public static string Echo(string value) => "B:" + value;
                }

                [ModImportName("Api")]
                public static class Consumer
                {
                    public static Func<string, string> Echo;
                    public static Func<string, object> Widen;
                    public static Func<object> Narrow;
                    public static RefTransform Touch;
                    [ModImportName("Api.Echo")] public static Transform<string> GenericEcho;
                    public static Func<List<string>, int> Count;
                    public static Func<string, string> Choose;
                    [ModImportName("Api.Echo")] public static Func<string, string> SpecificEcho;
                    public static Func<int> Missing = () => 7;
                }

                public static class UnqualifiedConsumer
                {
                    public static Func<string, string> Echo;
                }
            }
            """);
        File.WriteAllText(Path.Combine(root, "MonoModModInteropStaticFacade.cs"), """
            using System;
            namespace MonoMod.ModInterop
            {
                public static class ModInteropManager
                {
                    public static void ModInterop(this Type type)
                    {
                        if (type == null) throw new ArgumentNullException(nameof(type));
                        if (!global::Celeste.Mod.GeneratedAppleEverestModInterop.Register(type))
                            throw new InvalidOperationException("unknown type");
                    }
                }
                [AttributeUsage(AttributeTargets.Class)] public sealed class ModExportNameAttribute : Attribute
                { public string Name { get; } public ModExportNameAttribute(string name) => Name = name; }
                [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field)] public sealed class ModImportNameAttribute : Attribute
                { public string Name { get; } public ModImportNameAttribute(string name) => Name = name; }
            }
            namespace Celeste.Mod
            {
                public static class AppleEverestStaticRuntime
                {
                    public static void RecordModInteropBinding(string name, bool bound) { }
                }
            }
            """);
        File.WriteAllText(Path.Combine(root, "Program.cs"), """
            using MonoMod.ModInterop;
            using System.Collections.Generic;

            static void Require(bool value, string name) { if (!value) throw new Exception(name); }
            string order = args.Single();
            if (order == "consumer-first")
            {
                Require(Fixture.Consumer.Missing() == 7, "preinitialized field must survive before importer registration");
                typeof(Fixture.Consumer).ModInterop();
                Require(Fixture.Consumer.Echo == null && Fixture.Consumer.Missing == null, "imports must begin null");
                typeof(Fixture.Provider).ModInterop();
            }
            else if (order == "provider-b-first")
            {
                typeof(Fixture.Consumer).ModInterop();
                typeof(Fixture.UnqualifiedConsumer).ModInterop();
                typeof(Fixture.ProviderB).ModInterop();
                typeof(Fixture.Provider).ModInterop();
            }
            else
            {
                typeof(Fixture.Provider).ModInterop();
                typeof(Fixture.Consumer).ModInterop();
            }
            Require(Fixture.Consumer.Echo("echo") == "echo", "exact Func binding");
            Require((string) Fixture.Consumer.Widen("wide") == "wide", "contravariant input binding");
            Require((string) Fixture.Consumer.Narrow() == "narrow", "covariant return binding");
            Require(Fixture.Consumer.Count(new List<string> { "a", "b" }) == 2, "closed generic argument binding");
            Require(Fixture.Consumer.Choose("chosen") == "chosen", "wrong overload skipped");
            Require(Fixture.Consumer.GenericEcho("generic") == "generic", "closed custom generic delegate binding");
            Require(Fixture.Consumer.SpecificEcho("explicit") == "explicit", "field-level explicit name binding");
            Require(Fixture.Consumer.Touch != null && Fixture.Consumer.Missing == null, "ref delegate and missing import state");
            var first = Fixture.Consumer.Echo;
            typeof(Fixture.Provider).ModInterop();
            Require(ReferenceEquals(first, Fixture.Consumer.Echo), "duplicate registration is a no-op");
            typeof(Fixture.UnqualifiedConsumer).ModInterop();
            Require(Fixture.UnqualifiedConsumer.Echo("plain") == (order == "provider-b-first" ? "B:plain" : "plain"),
                "unqualified first-compatible binding");
            Require(Fixture.Consumer.Echo("qualified") == "qualified", "qualified provider binding ignores unqualified collision");
            Console.WriteLine("PASS: generated static ModInterop " + args[0]);
            """);

        string dotnet = Path.Combine(repository, ".build", "apple-everest", "toolchain", "dotnet8", "dotnet");
        if (!File.Exists(dotnet))
            throw new InvalidOperationException("pinned .NET 8 host path unavailable: " + dotnet);
        ProcessStartInfo info = new(dotnet)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        info.ArgumentList.Add("run");
        info.ArgumentList.Add("--project");
        info.ArgumentList.Add(Path.Combine(root, "StaticHost.csproj"));
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("Release");
        info.ArgumentList.Add("--");
        info.ArgumentList.Add(order);
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("failed to start static ModInterop host");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"static ModInterop host timed out ({order})");
        }
        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0 || !output.Contains("PASS: generated static ModInterop", StringComparison.Ordinal))
            throw new InvalidOperationException($"static ModInterop host failed ({order}): {output}\n{error}");
    }

    private static ResolvedMod Stub(IReadOnlyList<ModInteropRegistrationPlan> plans)
    {
        EverestYamlEntry metadata = new() { Name = "Fixture", Version = "1.0.0" };
        return new ResolvedMod
        {
            Metadata = metadata,
            Input = new ModInput { SourcePath = "fixture", StagingRoot = "fixture", SourceSha256 = "fixture", Files = [], Metadata = [metadata] },
            Classification = CompatibilityClass.MODINTEROP_STATIC_SUPPORTED,
            Mechanisms = new SortedSet<string>(StringComparer.Ordinal),
            ManagedFiles = [],
            ContentFiles = [],
            ManagedDetourTargets = new SortedSet<string>(StringComparer.Ordinal),
            DirectManagedHooks = [],
            ModInteropRegistrations = plans,
            FrozenIlTransforms = []
        };
    }

    private static void CreateFixture(string path, bool dynamicRegistration, bool openGenericExport = false,
        bool readOnlyImport = false)
    {
        using AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Fixture", new Version(1, 0, 0, 0)), "Fixture", ModuleKind.Dll);
        ModuleDefinition module = assembly.MainModule;
        AssemblyNameReference celeste = new("Celeste", new Version(1, 0, 0, 0));
        AssemblyNameReference monoMod = new("MonoMod.Utils", new Version(1, 0, 0, 0));
        module.AssemblyReferences.Add(celeste);
        module.AssemblyReferences.Add(monoMod);
        TypeReference ignoresAccess = new("System.Runtime.CompilerServices", "IgnoresAccessChecksToAttribute",
            module, monoMod);
        MethodReference ignoresAccessConstructor = new(".ctor", module.TypeSystem.Void, ignoresAccess) { HasThis = true };
        ignoresAccessConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        CustomAttribute ignoresAccessAttribute = new(ignoresAccessConstructor);
        ignoresAccessAttribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, "Celeste"));
        assembly.CustomAttributes.Add(ignoresAccessAttribute);
        TypeReference manager = new("MonoMod.ModInterop", "ModInteropManager", module, monoMod);
        MethodReference modInterop = new("ModInterop", module.TypeSystem.Void, manager) { HasThis = false };
        modInterop.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(Type))));
        MethodReference getType = module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);

        TypeDefinition provider = StaticType(module, "Provider");
        AddAttribute(provider, module, monoMod, "ModExportNameAttribute", "Api");
        MethodDefinition echo = new("Echo", MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.String);
        echo.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, module.TypeSystem.String));
        ILProcessor echoIl = echo.Body.GetILProcessor();
        echoIl.Emit(OpCodes.Ldarg_0);
        echoIl.Emit(OpCodes.Ret);
        provider.Methods.Add(echo);

        MethodDefinition widen = new("Widen", MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.Object);
        widen.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, module.TypeSystem.Object));
        ILProcessor widenIl = widen.Body.GetILProcessor();
        widenIl.Emit(OpCodes.Ldarg_0);
        widenIl.Emit(OpCodes.Ret);
        provider.Methods.Add(widen);

        MethodDefinition narrow = new("Narrow", MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.String);
        ILProcessor narrowIl = narrow.Body.GetILProcessor();
        narrowIl.Emit(OpCodes.Ldstr, "narrow");
        narrowIl.Emit(OpCodes.Ret);
        provider.Methods.Add(narrow);

        TypeDefinition refTransform = new("Fixture", "RefTransform",
            TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.NotPublic,
            module.ImportReference(typeof(MulticastDelegate)));
        MethodDefinition delegateConstructor = new(".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void) { ImplAttributes = MethodImplAttributes.Runtime };
        delegateConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));
        delegateConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.IntPtr));
        refTransform.Methods.Add(delegateConstructor);
        MethodDefinition invoke = new("Invoke", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            module.TypeSystem.Void) { ImplAttributes = MethodImplAttributes.Runtime };
        invoke.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, new ByReferenceType(module.TypeSystem.Int32)));
        refTransform.Methods.Add(invoke);
        module.Types.Add(refTransform);

        TypeDefinition transform = new("Fixture", "Transform`1",
            TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.NotPublic,
            module.ImportReference(typeof(MulticastDelegate)));
        GenericParameter transformParameter = new("T", transform);
        transform.GenericParameters.Add(transformParameter);
        MethodDefinition transformConstructor = new(".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void) { ImplAttributes = MethodImplAttributes.Runtime };
        transformConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));
        transformConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.IntPtr));
        transform.Methods.Add(transformConstructor);
        MethodDefinition transformInvoke = new("Invoke", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            transformParameter) { ImplAttributes = MethodImplAttributes.Runtime };
        transformInvoke.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, transformParameter));
        transform.Methods.Add(transformInvoke);
        module.Types.Add(transform);

        MethodDefinition touch = new("Touch", MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.Void);
        touch.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, new ByReferenceType(module.TypeSystem.Int32)));
        ILProcessor touchIl = touch.Body.GetILProcessor();
        touchIl.Emit(OpCodes.Ret);
        provider.Methods.Add(touch);

        TypeReference listOfString = new GenericInstanceType(module.ImportReference(typeof(List<>)))
        {
            GenericArguments = { module.TypeSystem.String }
        };
        MethodDefinition count = new("Count", MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.Int32);
        count.Parameters.Add(new ParameterDefinition("values", ParameterAttributes.None, listOfString));
        ILProcessor countIl = count.Body.GetILProcessor();
        countIl.Emit(OpCodes.Ldarg_0);
        countIl.Emit(OpCodes.Callvirt, module.ImportReference(typeof(List<string>).GetProperty(nameof(List<string>.Count))!.GetMethod!));
        countIl.Emit(OpCodes.Ret);
        provider.Methods.Add(count);

        MethodDefinition chooseWrong = new("Choose", MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.String);
        chooseWrong.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, module.TypeSystem.Int32));
        ILProcessor chooseWrongIl = chooseWrong.Body.GetILProcessor();
        chooseWrongIl.Emit(OpCodes.Ldstr, "wrong");
        chooseWrongIl.Emit(OpCodes.Ret);
        provider.Methods.Add(chooseWrong);
        MethodDefinition choose = new("Choose", MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.String);
        choose.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, module.TypeSystem.String));
        ILProcessor chooseIl = choose.Body.GetILProcessor();
        chooseIl.Emit(OpCodes.Ldarg_0);
        chooseIl.Emit(OpCodes.Ret);
        provider.Methods.Add(choose);
        if (openGenericExport)
        {
            MethodDefinition genericExport = new("Open", MethodAttributes.Public | MethodAttributes.Static,
                module.TypeSystem.Object);
            GenericParameter valueType = new("T", genericExport);
            genericExport.GenericParameters.Add(valueType);
            genericExport.ReturnType = valueType;
            genericExport.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, valueType));
            ILProcessor genericIl = genericExport.Body.GetILProcessor();
            genericIl.Emit(OpCodes.Ldarg_0);
            genericIl.Emit(OpCodes.Ret);
            provider.Methods.Add(genericExport);
        }

        TypeDefinition providerB = StaticType(module, "ProviderB");
        AddAttribute(providerB, module, monoMod, "ModExportNameAttribute", "Other");
        MethodDefinition echoB = new("Echo", MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.String);
        echoB.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, module.TypeSystem.String));
        ILProcessor echoBIl = echoB.Body.GetILProcessor();
        echoBIl.Emit(OpCodes.Ldarg_0);
        echoBIl.Emit(OpCodes.Ret);
        providerB.Methods.Add(echoB);

        TypeDefinition consumer = StaticType(module, "Consumer");
        AddAttribute(consumer, module, monoMod, "ModImportNameAttribute", "Api");
        TypeReference func = new GenericInstanceType(module.ImportReference(typeof(Func<,>)))
        {
            GenericArguments = { module.TypeSystem.String, module.TypeSystem.String }
        };
        consumer.Fields.Add(new FieldDefinition("Echo", FieldAttributes.Public | FieldAttributes.Static |
            (readOnlyImport ? FieldAttributes.InitOnly : 0), func));
        TypeReference wideFunc = new GenericInstanceType(module.ImportReference(typeof(Func<,>)))
        {
            GenericArguments = { module.TypeSystem.String, module.TypeSystem.Object }
        };
        consumer.Fields.Add(new FieldDefinition("Widen", FieldAttributes.Public | FieldAttributes.Static, wideFunc));
        TypeReference narrowFunc = new GenericInstanceType(module.ImportReference(typeof(Func<>)))
        {
            GenericArguments = { module.TypeSystem.Object }
        };
        consumer.Fields.Add(new FieldDefinition("Narrow", FieldAttributes.Public | FieldAttributes.Static, narrowFunc));
        consumer.Fields.Add(new FieldDefinition("Touch", FieldAttributes.Public | FieldAttributes.Static, refTransform));
        TypeReference closedTransform = new GenericInstanceType(transform)
        {
            GenericArguments = { module.TypeSystem.String }
        };
        FieldDefinition genericEcho = new("GenericEcho", FieldAttributes.Public | FieldAttributes.Static, closedTransform);
        AddAttribute(genericEcho, module, monoMod, "ModImportNameAttribute", "Api.Echo");
        consumer.Fields.Add(genericEcho);
        TypeReference countFunc = new GenericInstanceType(module.ImportReference(typeof(Func<,>)))
        {
            GenericArguments = { listOfString, module.TypeSystem.Int32 }
        };
        consumer.Fields.Add(new FieldDefinition("Count", FieldAttributes.Public | FieldAttributes.Static, countFunc));
        consumer.Fields.Add(new FieldDefinition("Choose", FieldAttributes.Public | FieldAttributes.Static, func));
        FieldDefinition explicitEcho = new("SpecificEcho", FieldAttributes.Public | FieldAttributes.Static, func);
        AddAttribute(explicitEcho, module, monoMod, "ModImportNameAttribute", "Api.Echo");
        consumer.Fields.Add(explicitEcho);
        TypeReference missingFunc = new GenericInstanceType(module.ImportReference(typeof(Func<>)))
        {
            GenericArguments = { module.TypeSystem.Int32 }
        };
        consumer.Fields.Add(new FieldDefinition("Missing", FieldAttributes.Public | FieldAttributes.Static, missingFunc));

        TypeDefinition unqualified = StaticType(module, "UnqualifiedConsumer");
        unqualified.Fields.Add(new FieldDefinition("Echo", FieldAttributes.Public | FieldAttributes.Static, func));

        TypeDefinition caller = StaticType(module, "Caller");
        MethodDefinition load = new("Load", MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.Void);
        if (dynamicRegistration) load.Parameters.Add(new ParameterDefinition("type", ParameterAttributes.None, module.ImportReference(typeof(Type))));
        ILProcessor il = load.Body.GetILProcessor();
        if (dynamicRegistration)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, modInterop);
        }
        else
        {
            Register(il, provider, getType, modInterop);
            Register(il, provider, getType, modInterop);
            Register(il, providerB, getType, modInterop);
            Register(il, consumer, getType, modInterop);
            Register(il, unqualified, getType, modInterop);
        }
        il.Emit(OpCodes.Ret);
        caller.Methods.Add(load);
        assembly.Write(path);
    }

    private static TypeDefinition StaticType(ModuleDefinition module, string name)
    {
        TypeDefinition type = new("Fixture", name,
            TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.NotPublic,
            module.TypeSystem.Object);
        module.Types.Add(type);
        return type;
    }

    private static void Register(ILProcessor il, TypeReference type, MethodReference getType, MethodReference modInterop)
    {
        il.Emit(OpCodes.Ldtoken, type);
        il.Emit(OpCodes.Call, getType);
        il.Emit(OpCodes.Call, modInterop);
    }

    private static void AddAttribute(ICustomAttributeProvider target, ModuleDefinition module, IMetadataScope scope, string name, string value)
    {
        TypeReference attribute = new("MonoMod.ModInterop", name, module, scope);
        MethodReference constructor = new(".ctor", module.TypeSystem.Void, attribute) { HasThis = true };
        constructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        CustomAttribute instance = new(constructor);
        instance.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, value));
        target.CustomAttributes.Add(instance);
    }
}
