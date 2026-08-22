using System.Text;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AppleEverestBuilder;

/// <summary>
/// Closed registry for real, source-audited HookGen IL manipulators which are
/// executed on the Mac and removed from the device assembly before Apple AOT.
/// This is intentionally not a generic arbitrary-mod execution surface.
/// </summary>
internal static class StaticIlFreeze
{
    internal const string FixtureName = "DashToggleHelper";
    internal const string FixtureVersion = "1.1.0";
    internal const string FixtureSourceSha256 = "a26ac163b4184cc0daccfd99f4ef11aeeeef7a2858b7d84938beb0dc6afd5d09";
    internal const string FixtureDllSha256 = "531eaa8a719cb81cc84adf2b9e930dcb3abae73c60406b8f44b823c9c4b4a083";
    internal const string FixtureZipSha256 = "677e8fbd067340d7b3133cc908e4ecafc0f5deab2c38b7eeb79a62eb5f61d523";
    internal const string FixtureUrl = "https://gamebanana.com/mmdl/1460721";
    internal const string FixtureSourceCommit = "9b140684c2ee80ddae3c9ef032de0c767a67530c";
    internal const string WorkerVersion = "apple-everest-static-il-worker-v1";

    private static readonly FrozenIlTransformPlan[] DashTogglePlans =
    [
        new(
            "DashToggleHelper:CrystalStaticSpinner.CreateSprites:CreateSpritesOverride",
            FixtureName, "bin/DashToggleHelper.dll", FixtureDllSha256,
            "IL.Celeste.CrystalStaticSpinner", "CreateSprites",
            "System.Void Celeste.CrystalStaticSpinner::AppleEverestOriginal_CreateSprites()",
            "System.Void Celeste.CrystalStaticSpinner::CreateSprites()",
            "Celeste.Mod.DashToggleHelper.DashToggleHelperModule", "CreateSpritesOverride",
            "60e4d178d19f1e70f21e9e88243354830db71155aa33de6b7b1f9b1e6abf6a94",
            "f03103f1f63b71351054d68b8fc6ed52a06dc1e690b616cf993885c93b3bd0d8",
            "da499a5a57b7ecb09f1d15ec20b61caa445252b8f631789d7723cbc02dbc81a9",
            ["DTSpinnerImage", "DTSpinnerColor", "isDTSpinner"]),
        new(
            "DashToggleHelper:CrystalStaticSpinner.AddSprite:AddSpriteOverride",
            FixtureName, "bin/DashToggleHelper.dll", FixtureDllSha256,
            "IL.Celeste.CrystalStaticSpinner", "AddSprite",
            "System.Void Celeste.CrystalStaticSpinner::AddSprite(Microsoft.Xna.Framework.Vector2)",
            "System.Void Celeste.CrystalStaticSpinner::AddSprite(Microsoft.Xna.Framework.Vector2)",
            "Celeste.Mod.DashToggleHelper.DashToggleHelperModule", "AddSpriteOverride",
            "0a10b7404b2394238548b3e32d89c8f15298c32f293a2bc23153e0c1a8ebd051",
            "91f955361cc5df3c643c8bd09b9cd211cf7ee09d6817ae2175d6569e2f9ec0aa",
            "17bf0d8a9050ef5f8372e08dc41367a800344a877a009096e20787dd64bb79e7",
            ["DTSpinnerImage", "tintIfDTSpinner"])
    ];

    internal static IReadOnlyList<FrozenIlTransformPlan> Resolve(ModInput input, EverestYamlEntry metadata)
    {
        if (metadata.Name != FixtureName || metadata.Version != FixtureVersion ||
            input.SourceSha256 != FixtureSourceSha256 || metadata.DLL != "bin/DashToggleHelper.dll")
            return [];
        string dll = Path.Combine(input.StagingRoot, "bin", "DashToggleHelper.dll");
        if (Hashing.FileSha256(dll) != FixtureDllSha256)
            throw new InvalidDataException("registered frozen-IL fixture DLL hash mismatch");
        if (metadata.Dependencies.Count != 1 || metadata.Dependencies[0].Name != "EverestCore" ||
            metadata.Dependencies[0].Version != "1.5421.0" || metadata.OptionalDependencies.Count != 1 ||
            metadata.OptionalDependencies[0].Name != "MoreDasheline" ||
            metadata.OptionalDependencies[0].Version != "1.7.1")
            throw new InvalidDataException("registered frozen-IL fixture metadata drifted");
        ValidateRegistrations(dll, DashTogglePlans);
        return DashTogglePlans;
    }

    private static void ValidateRegistrations(string dll, IReadOnlyList<FrozenIlTransformPlan> plans)
    {
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(dll,
            new ReaderParameters { ReadSymbols = false });
        List<(string Containing, string Operation, string EventType, string EventName, string Manipulator)> found = [];
        foreach (MethodDefinition method in assembly.MainModule.Types.SelectMany(AllTypes)
                     .SelectMany(type => type.Methods).Where(method => method.HasBody))
        {
            Instruction[] body = method.Body.Instructions.ToArray();
            for (int index = 0; index < body.Length; index++)
            {
                if (body[index].Operand is not MethodReference called ||
                    !called.DeclaringType.Namespace.StartsWith("IL.", StringComparison.Ordinal) ||
                    !(called.Name.StartsWith("add_", StringComparison.Ordinal) ||
                      called.Name.StartsWith("remove_", StringComparison.Ordinal))) continue;
                MethodReference? manipulator = body.Take(index).Reverse().Take(16)
                    .Where(instruction => instruction.OpCode == OpCodes.Ldftn)
                    .Select(instruction => instruction.Operand).OfType<MethodReference>().FirstOrDefault();
                if (manipulator == null)
                    throw new InvalidDataException("frozen-IL registration does not use a bounded static ldftn delegate: " +
                        string.Join(" | ", body.Skip(Math.Max(0, index - 16)).Take(17).Select(instruction =>
                            instruction.OpCode + ":" + (instruction.Operand is MemberReference member ? member.FullName : instruction.Operand))));
                found.Add((method.FullName,
                    called.Name.StartsWith("add_", StringComparison.Ordinal) ? "add" : "remove",
                    called.DeclaringType.FullName, called.Name[(called.Name[0] == 'a' ? 4 : 7)..],
                    manipulator.DeclaringType.FullName + "::" + manipulator.Name));
            }
        }
        string module = "Celeste.Mod.DashToggleHelper.DashToggleHelperModule";
        foreach (FrozenIlTransformPlan plan in plans)
        {
            string manipulator = plan.ManipulatorType + "::" + plan.ManipulatorMethod;
            if (found.Count(item => item.Operation == "add" && item.EventType == plan.EventType &&
                    item.EventName == plan.EventName && item.Manipulator == manipulator &&
                    item.Containing == "System.Void " + module + "::Load()") != 1 ||
                found.Count(item => item.Operation == "remove" && item.EventType == plan.EventType &&
                    item.EventName == plan.EventName && item.Manipulator == manipulator &&
                    item.Containing == "System.Void " + module + "::Unload()") != 1)
                throw new InvalidDataException("frozen-IL Load/Unload registration contract drifted: " + plan.PlanId);
        }
        if (found.Count != plans.Count * 2)
            throw new InvalidDataException("registered fixture contains an unreviewed IL event subscription");
        if (assembly.MainModule.GetTypeReferences().Any(type =>
                type.FullName == "MonoMod.RuntimeDetour.ILHook"))
            throw new InvalidDataException("registered fixture unexpectedly uses direct ILHook");
    }

    internal static void RewriteDeviceAssembly(AssemblyDefinition assembly,
        IReadOnlyList<FrozenIlTransformPlan> plans)
    {
        if (plans.Count == 0) return;
        if (assembly.Name.Name != FixtureName || plans.Any(plan => plan.Owner != FixtureName))
            throw new InvalidDataException("frozen-IL device rewrite received an unregistered assembly");

        TypeDefinition module = assembly.MainModule.Types.SelectMany(AllTypes)
            .Single(type => type.FullName == "Celeste.Mod.DashToggleHelper.DashToggleHelperModule");
        HashSet<string> manipulators = plans.Select(plan => plan.ManipulatorMethod).ToHashSet(StringComparer.Ordinal);
        RewriteLifecycleMethod(module.Methods.Single(method => method.Name == "Load"), "add_", plans.Count);
        RewriteLifecycleMethod(module.Methods.Single(method => method.Name == "Unload"), "remove_", plans.Count);

        foreach (string name in manipulators)
        {
            MethodDefinition method = module.Methods.Single(candidate => candidate.Name == name);
            module.Methods.Remove(method);
        }
        foreach (string name in plans.SelectMany(plan => plan.InjectedMethods).Distinct(StringComparer.Ordinal))
        {
            MethodDefinition method = module.Methods.Single(candidate => candidate.Name == name);
            method.IsPublic = true;
            method.IsPrivate = false;
        }

        // The fixture's public release declares MoreDasheline as optional. The
        // selected closure does not contain it, so freeze exactly the ordinary
        // fallback branch and remove its unreachable adapter/reference.
        RewriteDashColorFallback(module, assembly.MainModule);
        RewriteOptionalDependencyConstructor(module, assembly.MainModule);
        FieldDefinition optionalLoaded = module.Fields.Single(field => field.Name == "moreDashelineLoaded");
        module.Fields.Remove(optionalLoaded);
        TypeDefinition optional = assembly.MainModule.Types.Single(type =>
            type.FullName == "Celeste.Mod.DashToggleHelper.MoreDashelineIntegration");
        assembly.MainModule.Types.Remove(optional);

        TypeDefinition[] hostOnlyTypes = assembly.MainModule.Types.SelectMany(AllTypes).Where(type =>
            type.Name.StartsWith("<>", StringComparison.Ordinal) && TypeUsesHostIl(type)).ToArray();
        foreach (TypeDefinition type in hostOnlyTypes)
        {
            if (type.DeclaringType == null) assembly.MainModule.Types.Remove(type);
            else type.DeclaringType.NestedTypes.Remove(type);
        }

        foreach (TypeDefinition custom in assembly.MainModule.Types.SelectMany(AllTypes).Where(type =>
                     type.CustomAttributes.Any(attribute =>
                         attribute.AttributeType.FullName == "Celeste.Mod.Entities.CustomEntityAttribute")))
            MakePublic(custom);

        foreach (string referenceName in new[] { "MoreDasheline", "MonoMod.Utils", "Mono.Cecil" })
        {
            AssemblyNameReference? reference = assembly.MainModule.AssemblyReferences.SingleOrDefault(value =>
                value.Name == referenceName);
            if (reference == null) continue;
            string[] residual = ActiveReferenceIdentities(assembly.MainModule, referenceName).ToArray();
            if (residual.Length != 0)
                throw new InvalidDataException("frozen-IL host reference survived device rewrite: " +
                                               referenceName + ":" + string.Join(',', residual));
            // Cecil retains imported-but-now-unreferenced TypeRef rows in memory after their
            // owning methods/types are removed. Those rows are not part of the executable
            // assembly graph and are discarded when the module is written. Validate the live
            // graph above, then remove the now-unneeded AssemblyRef explicitly.
            assembly.MainModule.AssemblyReferences.Remove(reference);
        }
    }

    private static IEnumerable<string> ActiveReferenceIdentities(ModuleDefinition module, string assemblyName)
    {
        foreach (TypeDefinition type in module.Types.SelectMany(AllTypes))
        {
            if (UsesAssembly(type.BaseType, assemblyName)) yield return "base:" + type.FullName;
            foreach (InterfaceImplementation implementation in type.Interfaces)
                if (UsesAssembly(implementation.InterfaceType, assemblyName)) yield return "interface:" + type.FullName;
            foreach (FieldDefinition field in type.Fields)
                if (UsesAssembly(field.FieldType, assemblyName)) yield return "field:" + field.FullName;
            foreach (PropertyDefinition property in type.Properties)
                if (UsesAssembly(property.PropertyType, assemblyName) ||
                    property.Parameters.Any(parameter => UsesAssembly(parameter.ParameterType, assemblyName)))
                    yield return "property:" + property.FullName;
            foreach (EventDefinition @event in type.Events)
                if (UsesAssembly(@event.EventType, assemblyName)) yield return "event:" + @event.FullName;
            foreach (MethodDefinition method in type.Methods)
            {
                if (UsesAssembly(method.ReturnType, assemblyName) ||
                    method.Parameters.Any(parameter => UsesAssembly(parameter.ParameterType, assemblyName)) ||
                    method.Overrides.Any(@override => UsesAssembly(@override.DeclaringType, assemblyName)))
                    yield return "signature:" + method.FullName;
                if (!method.HasBody) continue;
                if (method.Body.Variables.Any(variable => UsesAssembly(variable.VariableType, assemblyName)) ||
                    method.Body.ExceptionHandlers.Any(handler => UsesAssembly(handler.CatchType, assemblyName)))
                    yield return "body-type:" + method.FullName;
                foreach (MemberReference member in method.Body.Instructions.Select(instruction => instruction.Operand)
                             .OfType<MemberReference>())
                    if (UsesAssembly(member.DeclaringType, assemblyName) ||
                        member is TypeReference referencedType && UsesAssembly(referencedType, assemblyName))
                        yield return "body-member:" + method.FullName + "->" + member.FullName;
            }
        }
    }

    private static bool UsesAssembly(TypeReference? type, string assemblyName) =>
        type != null && type.GetElementType().Scope is AssemblyNameReference reference &&
        string.Equals(reference.Name, assemblyName, StringComparison.Ordinal);

    private static void RewriteLifecycleMethod(MethodDefinition method, string operation, int expectedIlEvents)
    {
        Instruction[] body = method.Body.Instructions.ToArray();
        int ilEvents = body.Count(instruction => instruction.Operand is MethodReference called &&
            called.DeclaringType.Namespace.StartsWith("IL.", StringComparison.Ordinal) &&
            called.Name.StartsWith(operation, StringComparison.Ordinal));
        if (ilEvents != expectedIlEvents)
            throw new InvalidDataException("frozen-IL lifecycle event count drifted: " + method.FullName);

        List<(MethodReference Event, MethodReference Constructor, MethodReference Handler)> onEvents = [];
        for (int index = 0; index < body.Length; index++)
        {
            if (body[index].Operand is not MethodReference called ||
                !called.DeclaringType.Namespace.StartsWith("On.", StringComparison.Ordinal) ||
                !called.Name.StartsWith(operation, StringComparison.Ordinal)) continue;
            int functionIndex = Enumerable.Range(Math.Max(0, index - 20), Math.Min(20, index))
                .Reverse().FirstOrDefault(candidate => body[candidate].OpCode == OpCodes.Ldftn &&
                    body[candidate].Operand is MethodReference);
            if (body[functionIndex].Operand is not MethodReference handler)
                throw new InvalidDataException("static On lifecycle handler could not be resolved");
            MethodReference? constructor = body.Skip(functionIndex + 1).Take(index - functionIndex - 1)
                .Where(instruction => instruction.OpCode == OpCodes.Newobj)
                .Select(instruction => instruction.Operand).OfType<MethodReference>().LastOrDefault();
            if (constructor == null)
                throw new InvalidDataException("static On lifecycle delegate constructor could not be resolved");
            onEvents.Add((called, constructor, handler));
        }
        if (onEvents.Count != 6)
            throw new InvalidDataException("DashToggleHelper On lifecycle contract drifted");

        method.Body.Instructions.Clear();
        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.InitLocals = false;
        ILProcessor il = method.Body.GetILProcessor();
        foreach ((MethodReference eventMethod, MethodReference constructor, MethodReference handler) in onEvents)
        {
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Ldftn, handler));
            il.Append(il.Create(OpCodes.Newobj, constructor));
            il.Append(il.Create(OpCodes.Call, eventMethod));
        }
        il.Append(il.Create(OpCodes.Ret));
    }

    private static void RewriteDashColorFallback(TypeDefinition module, ModuleDefinition assembly)
    {
        MethodDefinition method = module.Methods.Single(candidate => candidate.Name == "getColor" &&
            candidate.IsStatic && candidate.Parameters.Count == 1);
        FieldReference colors = method.Body.Instructions.Select(instruction => instruction.Operand)
            .OfType<FieldReference>().First(field => field.Name == "dashColors");
        MethodReference contains = method.Body.Instructions.Select(instruction => instruction.Operand)
            .OfType<MethodReference>().First(reference => reference.Name == "ContainsKey");
        MethodReference item = method.Body.Instructions.Select(instruction => instruction.Operand)
            .OfType<MethodReference>().First(reference => reference.Name == "get_Item");
        MethodReference white = method.Body.Instructions.Select(instruction => instruction.Operand)
            .OfType<MethodReference>().First(reference => reference.Name == "get_White");
        method.Body.Instructions.Clear();
        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.InitLocals = false;
        ILProcessor il = method.Body.GetILProcessor();
        Instruction fallback = il.Create(OpCodes.Call, assembly.ImportReference(white));
        il.Append(il.Create(OpCodes.Ldsfld, assembly.ImportReference(colors)));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Callvirt, assembly.ImportReference(contains)));
        il.Append(il.Create(OpCodes.Brfalse, fallback));
        il.Append(il.Create(OpCodes.Ldsfld, assembly.ImportReference(colors)));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Callvirt, assembly.ImportReference(item)));
        il.Append(il.Create(OpCodes.Ret));
        il.Append(fallback);
        il.Append(il.Create(OpCodes.Ret));
    }

    private static void RewriteOptionalDependencyConstructor(TypeDefinition module, ModuleDefinition assembly)
    {
        MethodDefinition constructor = module.Methods.Single(method => method.IsConstructor && !method.IsStatic);
        MethodReference baseConstructor = constructor.Body.Instructions
            .Where(instruction => instruction.OpCode == OpCodes.Call)
            .Select(instruction => instruction.Operand).OfType<MethodReference>()
            .Single(method => method.Name == ".ctor" && method.DeclaringType.FullName == "Celeste.Mod.EverestModule");
        MethodReference instanceSetter = constructor.Body.Instructions
            .Select(instruction => instruction.Operand).OfType<MethodReference>()
            .Single(method => method.Name == "set_Instance" && method.DeclaringType.FullName == module.FullName);
        int dependencyCalls = constructor.Body.Instructions.Count(instruction =>
            instruction.Operand is MethodReference method && method.Name == "DependencyLoaded" &&
            method.DeclaringType.FullName == "Celeste.Mod.Everest/Loader");
        if (dependencyCalls != 1)
            throw new InvalidDataException("DashToggleHelper optional-dependency constructor contract drifted");

        constructor.Body.Instructions.Clear();
        constructor.Body.ExceptionHandlers.Clear();
        constructor.Body.Variables.Clear();
        constructor.Body.InitLocals = false;
        ILProcessor il = constructor.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Call, assembly.ImportReference(baseConstructor)));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Call, assembly.ImportReference(instanceSetter)));
        il.Append(il.Create(OpCodes.Ret));
    }

    private static bool MethodUsesHostIl(MethodDefinition method) => method.HasBody &&
        (method.ReturnType.Scope?.Name is "MonoMod.Utils" or "Mono.Cecil" ||
         method.Parameters.Any(parameter => parameter.ParameterType.Scope?.Name is "MonoMod.Utils" or "Mono.Cecil") ||
         method.Body.Instructions.Select(instruction => instruction.Operand).OfType<MemberReference>()
             .Any(reference => reference.DeclaringType.Scope?.Name is "MonoMod.Utils" or "Mono.Cecil"));

    private static bool TypeUsesHostIl(TypeDefinition type) =>
        type.Fields.Any(field => field.FieldType.Scope?.Name is "MonoMod.Utils" or "Mono.Cecil" ||
            field.FieldType.FullName.StartsWith("MonoMod.Cil.", StringComparison.Ordinal) ||
            field.FieldType.FullName.StartsWith("Mono.Cecil.", StringComparison.Ordinal)) ||
        type.Methods.Any(MethodUsesHostIl);

    private static void MakePublic(TypeDefinition type)
    {
        if (type.DeclaringType == null)
        {
            type.IsPublic = true;
            type.IsNotPublic = false;
        }
        else
        {
            MakePublic(type.DeclaringType);
            type.IsNestedPublic = true;
            type.IsNestedPrivate = type.IsNestedAssembly = type.IsNestedFamily = false;
            type.IsNestedFamilyAndAssembly = type.IsNestedFamilyOrAssembly = false;
        }
    }

    internal static string PlanSha256(IEnumerable<FrozenIlTransformPlan> plans) => Hashing.BytesSha256(
        Encoding.UTF8.GetBytes(string.Join("\n", plans.OrderBy(plan => plan.PlanId, StringComparer.Ordinal)
            .Select(plan => string.Join("\0", plan.PlanId, plan.AssemblySha256, plan.EventType, plan.EventName,
                plan.TargetMethod, plan.CanonicalTargetMethod, plan.ManipulatorType, plan.ManipulatorMethod, plan.BeforeSha256,
                plan.AfterSha256, plan.DiffSha256))) + "\n"));

    internal static string Targets(IReadOnlyList<FrozenIlTransformPlan> plans)
    {
        string host = "$(MSBuildProjectDirectory)/.AppleEverestStaticIlHost";
        StringBuilder xml = new StringBuilder("<Project>\n  <PropertyGroup>\n    <AppleEverestStaticIlHost>")
            .Append(host).AppendLine("</AppleEverestStaticIlHost>")
            .AppendLine("    <AppleEverestDotNetHost>$(DOTNET_HOST_PATH)</AppleEverestDotNetHost>")
            .AppendLine("  </PropertyGroup>")
            .AppendLine("  <Target Name=\"AppleEverestFreezeStaticIl\" AfterTargets=\"CoreCompile\" Condition=\"'$(DesignTimeBuild)' != 'true'\">")
            .AppendLine("    <PropertyGroup><AppleEverestIntermediateAssembly>$(IntermediateOutputPath)$(TargetFileName)</AppleEverestIntermediateAssembly></PropertyGroup>")
            .AppendLine("    <Error Condition=\"!Exists('$(AppleEverestDotNetHost)')\" Text=\"The active pinned .NET SDK host is unavailable for the Apple Everest static-IL freeze.\" />")
            .AppendLine("    <Error Condition=\"!Exists('$(AppleEverestIntermediateAssembly)')\" Text=\"The compiled Celeste intermediate assembly is missing at the static-IL freeze boundary.\" />")
            .AppendLine("    <MakeDir Directories=\"$(IntermediateOutputPath)apple-everest-static-il\" />");
        for (int index = 0; index < plans.Count; index++)
        {
            FrozenIlTransformPlan plan = plans[index];
            string temp = "$(IntermediateOutputPath)apple-everest-static-il/target-" + index + ".dll";
            string manifest = "$(IntermediateOutputPath)apple-everest-static-il/transform-" + index + ".json";
            xml.Append("    <Exec Command=\"&quot;$(AppleEverestDotNetHost)&quot; &quot;$(AppleEverestStaticIlHost)/AppleEverestIlWorker.dll&quot; --target &quot;$(AppleEverestIntermediateAssembly)&quot; --output &quot;")
                .Append(temp).Append("&quot; --mod &quot;$(AppleEverestStaticIlHost)/fixtures/DashToggleHelper.original.dll&quot; --target-method &quot;")
                .Append(Escape(plan.TargetMethod)).Append("&quot; --canonical-target-method &quot;")
                .Append(Escape(plan.CanonicalTargetMethod)).Append("&quot; --manipulator-type &quot;")
                .Append(Escape(plan.ManipulatorType)).Append("&quot; --manipulator-method &quot;")
                .Append(Escape(plan.ManipulatorMethod)).Append("&quot; --expected-before &quot;")
                .Append(plan.BeforeSha256).Append("&quot; --expected-after &quot;").Append(plan.AfterSha256)
                .Append("&quot; --expected-diff &quot;").Append(plan.DiffSha256)
                .Append("&quot; --manifest &quot;").Append(manifest)
                .Append("&quot; --runtime-dir &quot;$(IntermediateOutputPath)&quot; --runtime-dir &quot;$(TargetDir)&quot; --runtime-dir &quot;$(MSBuildProjectDirectory)/AppleEverestAssemblies&quot; --runtime-dir &quot;$(AppleEverestStaticIlHost)&quot; @(ReferenceCopyLocalPaths-&gt;'--runtime-dir &quot;%(RootDir)%(Directory)&quot;', ' ')\" />\n")
                .Append("    <Copy SourceFiles=\"").Append(temp).AppendLine("\" DestinationFiles=\"$(AppleEverestIntermediateAssembly)\" />");
        }
        xml.AppendLine("  </Target>\n</Project>");
        return xml.ToString();
    }

    private static string Escape(string value) => System.Security.SecurityElement.Escape(value) ?? "";

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition root)
    {
        yield return root;
        foreach (TypeDefinition nested in root.NestedTypes.SelectMany(AllTypes)) yield return nested;
    }
}
