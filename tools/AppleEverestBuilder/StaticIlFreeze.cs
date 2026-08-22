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
    internal const string DisposableTheoName = "DisposableTheo";
    internal const string DisposableTheoVersion = "1.0.6";
    internal const string DisposableTheoSourceSha256 = "fc6aa15ee69311eac205af76e382d8a09dfb16ebe73eb05163ba90da7c21d597";
    internal const string DisposableTheoDllSha256 = "1d47c08238fd0dd29eaa5c6e53a36e7d72942fdb7b7abc2a3870f09fbc952dcc";
    internal const string DisposableTheoZipSha256 = "df291c0175df46682791fb6373c47eb557c47483eca3db96895eba9b5bbe85b5";
    internal const string DisposableTheoUrl = "https://gamebanana.com/mmdl/929736";
    internal const string WorkerVersion = "apple-everest-static-il-worker-v2";

    private static readonly FrozenIlTransformPlan[] DashTogglePlans =
    [
        new(
            "DashToggleHelper:CrystalStaticSpinner.CreateSprites:CreateSpritesOverride",
            FixtureName, "bin/DashToggleHelper.dll", FixtureDllSha256,
            "IL.Celeste.CrystalStaticSpinner", "CreateSprites",
            "System.Void Celeste.CrystalStaticSpinner::AppleEverestOriginal_CreateSprites()",
            "System.Void Celeste.CrystalStaticSpinner::CreateSprites()",
            "Celeste.Mod.DashToggleHelper.DashToggleHelperModule", "CreateSpritesOverride",
            true, 0,
            "60e4d178d19f1e70f21e9e88243354830db71155aa33de6b7b1f9b1e6abf6a94",
            "f03103f1f63b71351054d68b8fc6ed52a06dc1e690b616cf993885c93b3bd0d8",
            "da499a5a57b7ecb09f1d15ec20b61caa445252b8f631789d7723cbc02dbc81a9",
            ["DTSpinnerImage", "DTSpinnerColor", "isDTSpinner"], []),
        new(
            "DashToggleHelper:CrystalStaticSpinner.AddSprite:AddSpriteOverride",
            FixtureName, "bin/DashToggleHelper.dll", FixtureDllSha256,
            "IL.Celeste.CrystalStaticSpinner", "AddSprite",
            "System.Void Celeste.CrystalStaticSpinner::AddSprite(Microsoft.Xna.Framework.Vector2)",
            "System.Void Celeste.CrystalStaticSpinner::AddSprite(Microsoft.Xna.Framework.Vector2)",
            "Celeste.Mod.DashToggleHelper.DashToggleHelperModule", "AddSpriteOverride",
            true, 0,
            "0a10b7404b2394238548b3e32d89c8f15298c32f293a2bc23153e0c1a8ebd051",
            "91f955361cc5df3c643c8bd09b9cd211cf7ee09d6817ae2175d6569e2f9ec0aa",
            "17bf0d8a9050ef5f8372e08dc41367a800344a877a009096e20787dd64bb79e7",
            ["DTSpinnerImage", "tintIfDTSpinner"], [])
    ];

    private static readonly FrozenIlTransformPlan[] DisposableTheoPlans =
    [
        new(
            "DisposableTheo:TheoCrystal.Die:TheoCrystal_Die",
            DisposableTheoName, "DisposableTheo.dll", DisposableTheoDllSha256,
            "IL.Celeste.TheoCrystal", "Die",
            "System.Void Celeste.TheoCrystal::Die()",
            "System.Void Celeste.TheoCrystal::Die()",
            "Celeste.Mod.DisposableTheo.DisposableTheoModule", "TheoCrystal_Die",
            false, 0,
            "ec6294022668396295da4d81b61192b01bcbc9e898399d94a8a74251d3c87911",
            "c538112f327281bfd4fa0af488a3ee175ff8662a63bfd3fced969e1d8e52ba55",
            "107ea6b57c066477eda086f303ca7331c86adbe40964fe30a266d27691c02987",
            [], ["Celeste.Mod.DisposableTheo.DisposableTheoModule+<>c::<TheoCrystal_Die>b__9_0"]),
        new(
            "DisposableTheo:Level.EnforceBounds:Level_EnforceBounds",
            DisposableTheoName, "DisposableTheo.dll", DisposableTheoDllSha256,
            "IL.Celeste.Level", "EnforceBounds",
            "System.Void Celeste.Level::EnforceBounds(Celeste.Player)",
            "System.Void Celeste.Level::EnforceBounds(Celeste.Player)",
            "Celeste.Mod.DisposableTheo.DisposableTheoModule", "Level_EnforceBounds",
            false, 0,
            "90f8c7928bd3cf8fff7db66aaebc122dc4f8082a947e41b78d5bba7d0021c1ac",
            "025cf84ebf83868acd00f5bab4bc7c028f6190159e89c4199ec08d03db5ecd78",
            "480bd4cdc59a703a66658d048a57067eebc55bde531c52acf344b6a3d3694fc1",
            [], ["Celeste.Mod.DisposableTheo.DisposableTheoModule+<>c::<Level_EnforceBounds>b__10_0"])
    ];

    internal static IReadOnlyList<FrozenIlTransformPlan> Resolve(ModInput input, EverestYamlEntry metadata)
    {
        if (metadata.Name == FixtureName && metadata.Version == FixtureVersion &&
            input.SourceSha256 == FixtureSourceSha256 && metadata.DLL == "bin/DashToggleHelper.dll")
        {
            string dll = Path.Combine(input.StagingRoot, "bin", "DashToggleHelper.dll");
            if (Hashing.FileSha256(dll) != FixtureDllSha256)
                throw new InvalidDataException("registered frozen-IL fixture DLL hash mismatch");
            if (metadata.Dependencies.Count != 1 || metadata.Dependencies[0].Name != "EverestCore" ||
                metadata.Dependencies[0].Version != "1.5421.0" || metadata.OptionalDependencies.Count != 1 ||
                metadata.OptionalDependencies[0].Name != "MoreDasheline" ||
                metadata.OptionalDependencies[0].Version != "1.7.1")
                throw new InvalidDataException("registered frozen-IL fixture metadata drifted");
            ValidateRegistrations(dll, DashTogglePlans, "Celeste.Mod.DashToggleHelper.DashToggleHelperModule");
            return DashTogglePlans;
        }
        if (metadata.Name == DisposableTheoName && metadata.Version == DisposableTheoVersion &&
            input.SourceSha256 == DisposableTheoSourceSha256 && metadata.DLL == "DisposableTheo.dll")
        {
            string dll = Path.Combine(input.StagingRoot, "DisposableTheo.dll");
            if (Hashing.FileSha256(dll) != DisposableTheoDllSha256)
                throw new InvalidDataException("registered DisposableTheo DLL hash mismatch");
            if (metadata.Dependencies.Count != 1 || metadata.Dependencies[0].Name != "Everest" ||
                metadata.Dependencies[0].Version != "1.0.0" || metadata.OptionalDependencies.Count != 0)
                throw new InvalidDataException("registered DisposableTheo metadata drifted");
            ValidateRegistrations(dll, DisposableTheoPlans, "Celeste.Mod.DisposableTheo.DisposableTheoModule");
            return DisposableTheoPlans;
        }
        return [];
    }

    private static void ValidateRegistrations(string dll, IReadOnlyList<FrozenIlTransformPlan> plans, string module)
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
        if (plans.Any(plan => plan.Owner != assembly.Name.Name) ||
            assembly.Name.Name is not (FixtureName or DisposableTheoName))
            throw new InvalidDataException("frozen-IL device rewrite received an unregistered assembly");

        if (assembly.Name.Name == DisposableTheoName)
        {
            RewriteDisposableTheo(assembly, plans);
            return;
        }

        TypeDefinition module = assembly.MainModule.Types.SelectMany(AllTypes)
            .Single(type => type.FullName == "Celeste.Mod.DashToggleHelper.DashToggleHelperModule");
        HashSet<string> manipulators = plans.Select(plan => plan.ManipulatorMethod).ToHashSet(StringComparer.Ordinal);
        RewriteLifecycleMethod(module.Methods.Single(method => method.Name == "Load"), "add_", plans.Count, 6);
        RewriteLifecycleMethod(module.Methods.Single(method => method.Name == "Unload"), "remove_", plans.Count, 6);

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

    private static void RewriteDisposableTheoEverestAbi(AssemblyDefinition assembly, TypeDefinition module)
    {
        // DisposableTheo 1.0.6 was compiled against three small APIs from its
        // pinned desktop Everest Celeste contract. Keep the shared canonical
        // Apple game tree unchanged and normalize this one exact, hash-locked
        // binary instead:
        //
        //  * Everest's integer VirtualIntegerAxis conversion is the public
        //    Value field in canonical Celeste.
        //  * Everest's two-argument Input.Rumble overload maps to canonical
        //    Celeste's source-aware three-argument overload with a null source.
        //  * CreateEnabledEntry is an Everest reflection-menu hook. The closed
        //    Apple runtime owns settings through generated descriptors, so the
        //    unreachable desktop menu body is deliberately reduced to a no-op.
        //
        // Every count below is part of the exact pinned-DLL contract. Drift
        // fails closed rather than expanding this into a generic ABI shim.
        MethodDefinition throwMethod = module.Methods.SingleOrDefault(method => method.Name == "Player_Throw")
            ?? throw new InvalidDataException("DisposableTheo pinned Player_Throw ABI method missing: " +
                string.Join(',', module.Methods.Select(method => method.Name)));
        Instruction[] original = throwMethod.Body.Instructions.ToArray();
        Instruction[] axisConversions = original.Where(instruction =>
            instruction.Operand is MethodReference called &&
            called.DeclaringType.FullName == "Monocle.VirtualIntegerAxis" &&
            called.Name == "op_Implicit" && called.Parameters.Count == 1 &&
            called.ReturnType.MetadataType == MetadataType.Int32).ToArray();
        if (axisConversions.Length != 4)
            throw new InvalidDataException("DisposableTheo pinned integer-axis ABI contract drifted");
        foreach (Instruction instruction in axisConversions)
        {
            MethodReference conversion = (MethodReference)instruction.Operand;
            instruction.OpCode = OpCodes.Ldfld;
            instruction.Operand = assembly.MainModule.ImportReference(new FieldReference(
                "Value", assembly.MainModule.TypeSystem.Int32, conversion.DeclaringType));
        }

        Instruction[] rumbleCalls = original.Where(instruction =>
            instruction.Operand is MethodReference called &&
            called.DeclaringType.FullName == "Celeste.Input" && called.Name == "Rumble" &&
            called.Parameters.Count == 2).ToArray();
        if (rumbleCalls.Length != 2)
            throw new InvalidDataException("DisposableTheo pinned rumble ABI contract drifted");
        ILProcessor processor = throwMethod.Body.GetILProcessor();
        foreach (Instruction instruction in rumbleCalls)
        {
            MethodReference old = (MethodReference)instruction.Operand;
            MethodReference replacement = new("Rumble", assembly.MainModule.TypeSystem.Void,
                assembly.MainModule.ImportReference(old.DeclaringType))
            {
                HasThis = false,
                CallingConvention = old.CallingConvention
            };
            replacement.Parameters.Add(new ParameterDefinition(assembly.MainModule.ImportReference(
                old.Parameters[0].ParameterType)));
            replacement.Parameters.Add(new ParameterDefinition(assembly.MainModule.ImportReference(
                old.Parameters[1].ParameterType)));
            replacement.Parameters.Add(new ParameterDefinition(assembly.MainModule.TypeSystem.String));
            processor.InsertBefore(instruction, processor.Create(OpCodes.Ldnull));
            instruction.Operand = assembly.MainModule.ImportReference(replacement);
        }

        TypeDefinition settings = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
            type.FullName == "Celeste.Mod.DisposableTheo.DisposableTheoSettings");
        CustomAttribute[] desktopSettingAttributes = settings.Properties
            .SelectMany(property => property.CustomAttributes)
            .Where(attribute => attribute.AttributeType.FullName == "Celeste.Mod.SettingIgnoreAttribute")
            .ToArray();
        if (desktopSettingAttributes.Length != 3)
            throw new InvalidDataException("DisposableTheo pinned settings metadata contract drifted");
        foreach (PropertyDefinition property in settings.Properties)
            for (int index = property.CustomAttributes.Count - 1; index >= 0; index--)
                if (property.CustomAttributes[index].AttributeType.FullName == "Celeste.Mod.SettingIgnoreAttribute")
                    property.CustomAttributes.RemoveAt(index);
        MethodDefinition menu = settings.Methods.Single(method => method.Name == "CreateEnabledEntry" &&
            method.Parameters.Count == 2);
        int desktopMenuReferences = menu.Body.Instructions.Count(instruction =>
            instruction.Operand is MemberReference member &&
            (member.DeclaringType.FullName == "Celeste.TextMenuExt/OptionSubMenu" ||
             member.DeclaringType.FullName.StartsWith("Celeste.TextMenuExt", StringComparison.Ordinal)));
        if (desktopMenuReferences != 5)
            throw new InvalidDataException("DisposableTheo pinned desktop settings-menu ABI contract drifted: " +
                desktopMenuReferences);
        menu.Body.Instructions.Clear();
        menu.Body.ExceptionHandlers.Clear();
        menu.Body.Variables.Clear();
        menu.Body.InitLocals = false;
        menu.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
    }

    private static void RewriteDisposableTheo(AssemblyDefinition assembly,
        IReadOnlyList<FrozenIlTransformPlan> plans)
    {
        TypeDefinition module = assembly.MainModule.Types.SelectMany(AllTypes).Single(type =>
            type.FullName == "Celeste.Mod.DisposableTheo.DisposableTheoModule");
        RewriteLifecycleMethod(module.Methods.Single(method => method.Name == "Load"), "add_", plans.Count, 2);
        RewriteLifecycleMethod(module.Methods.Single(method => method.Name == "Unload"), "remove_", plans.Count, 1);
        foreach (string name in plans.Select(plan => plan.ManipulatorMethod).Distinct(StringComparer.Ordinal))
            module.Methods.Remove(module.Methods.Single(method => method.Name == name));

        TypeDefinition singleton = module.NestedTypes.Single(type => type.Name == "<>c");
        HashSet<string> runtimeTargets = plans.SelectMany(plan => plan.ExpectedDelegateTargets)
            .Select(value => value[(value.LastIndexOf("::", StringComparison.Ordinal) + 2)..])
            .ToHashSet(StringComparer.Ordinal);
        foreach (MethodDefinition method in singleton.Methods.Where(method =>
                     MethodUsesHostIl(method) || (method.Name.StartsWith("<", StringComparison.Ordinal) &&
                     !runtimeTargets.Contains(method.Name))).ToArray())
            singleton.Methods.Remove(method);
        foreach (FieldDefinition field in singleton.Fields.Where(field =>
                     field.FieldType.Scope?.Name is "MonoMod.Utils" or "Mono.Cecil" ||
                     field.FieldType.FullName.Contains("Mono.Cecil", StringComparison.Ordinal) ||
                     field.FieldType.FullName.Contains("MonoMod.Cil", StringComparison.Ordinal)).ToArray())
            singleton.Fields.Remove(field);
        MakePublic(singleton);
        foreach (FieldDefinition field in singleton.Fields.Where(field => field.Name == "<>9"))
        {
            field.IsPublic = true;
            field.IsPrivate = false;
        }
        foreach (MethodDefinition method in singleton.Methods.Where(method => runtimeTargets.Contains(method.Name)))
        {
            method.IsPublic = true;
            method.IsPrivate = false;
        }

        foreach (TypeDefinition custom in assembly.MainModule.Types.SelectMany(AllTypes).Where(type =>
                     type.CustomAttributes.Any(attribute =>
                         attribute.AttributeType.FullName == "Celeste.Mod.Entities.CustomEntityAttribute")))
            MakePublic(custom);

        RewriteDisposableTheoEverestAbi(assembly, module);

        foreach (string referenceName in new[] { "MonoMod.Utils", "Mono.Cecil" })
        {
            AssemblyNameReference? reference = assembly.MainModule.AssemblyReferences.SingleOrDefault(value =>
                value.Name == referenceName);
            if (reference == null) continue;
            string[] residual = ActiveReferenceIdentities(assembly.MainModule, referenceName).ToArray();
            if (residual.Length != 0)
                throw new InvalidDataException("frozen-IL host reference survived DisposableTheo rewrite: " +
                                               referenceName + ":" + string.Join(',', residual));
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

    private static void RewriteLifecycleMethod(MethodDefinition method, string operation, int expectedIlEvents,
        int expectedOnEvents)
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
        if (onEvents.Count != expectedOnEvents)
            throw new InvalidDataException("frozen-IL On lifecycle contract drifted: " + method.FullName);

        method.Body.Instructions.Clear();
        method.Body.ExceptionHandlers.Clear();
        method.Body.Variables.Clear();
        method.Body.InitLocals = false;
        ILProcessor il = method.Body.GetILProcessor();
        foreach ((MethodReference eventMethod, MethodReference constructor, MethodReference handler) in onEvents)
        {
            il.Append(handler.HasThis ? il.Create(OpCodes.Ldarg_0) : il.Create(OpCodes.Ldnull));
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
            type.Attributes = (type.Attributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.Public;
        }
        else
        {
            MakePublic(type.DeclaringType);
            type.Attributes = (type.Attributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.NestedPublic;
        }
    }

    internal static string PlanSha256(IEnumerable<FrozenIlTransformPlan> plans) => Hashing.BytesSha256(
        Encoding.UTF8.GetBytes(string.Join("\n", plans
            .Select(plan => string.Join("\0", plan.PlanId, plan.AssemblySha256, plan.EventType, plan.EventName,
                plan.TargetMethod, plan.CanonicalTargetMethod, plan.ManipulatorType, plan.ManipulatorMethod,
                plan.ManipulatorIsStatic, plan.RegistrationOrdinal, plan.BeforeSha256,
                plan.AfterSha256, plan.DiffSha256, string.Join(',', plan.ExpectedDelegateTargets)))) + "\n"));

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
        IGrouping<string, FrozenIlTransformPlan>[] groups = plans.GroupBy(plan => plan.TargetMethod,
            StringComparer.Ordinal).ToArray();
        for (int index = 0; index < groups.Length; index++)
        {
            IGrouping<string, FrozenIlTransformPlan> group = groups[index];
            FrozenIlTransformPlan plan = group.First();
            string temp = "$(IntermediateOutputPath)apple-everest-static-il/target-" + index + ".dll";
            string manifest = "$(IntermediateOutputPath)apple-everest-static-il/transform-" + index + ".json";
            xml.Append("    <Exec Command=\"&quot;$(AppleEverestDotNetHost)&quot; &quot;$(AppleEverestStaticIlHost)/AppleEverestIlWorker.dll&quot; --target &quot;$(AppleEverestIntermediateAssembly)&quot; --output &quot;")
                .Append(temp).Append("&quot; --plan &quot;$(AppleEverestStaticIlHost)/frozen-il-plan.json&quot; --target-method &quot;")
                .Append(Escape(plan.TargetMethod))
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
