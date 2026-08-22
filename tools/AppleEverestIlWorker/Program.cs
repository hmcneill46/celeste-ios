using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

internal static class Program
{
    private sealed class Options
    {
        public string Target = "";
        public string Output = "";
        public string Plan = "";
        public string TargetMethod = "";
        public string Manifest = "";
        public readonly List<string> RuntimeDirectories = new();
    }

    private sealed class PlanDocument
    {
        public int SchemaVersion { get; set; }
        public string Worker { get; set; } = "";
        public string PlanSha256 { get; set; } = "";
        public Transform[] Transforms { get; set; } = [];
    }

    private sealed class Transform
    {
        public string PlanId { get; set; } = "";
        public string Owner { get; set; } = "";
        public string AssemblySha256 { get; set; } = "";
        public string EventType { get; set; } = "";
        public string EventName { get; set; } = "";
        public string TargetMethod { get; set; } = "";
        public string CanonicalTargetMethod { get; set; } = "";
        public string ManipulatorType { get; set; } = "";
        public string ManipulatorMethod { get; set; } = "";
        public bool ManipulatorIsStatic { get; set; }
        public int RegistrationOrdinal { get; set; }
        public string BeforeSha256 { get; set; } = "";
        public string AfterSha256 { get; set; } = "";
        public string DiffSha256 { get; set; } = "";
        public string[] ExpectedDelegateTargets { get; set; } = [];
        public string Mechanism { get; set; } = "HOOKGEN_IL_EVENT";
        public string ConstructorSignature { get; set; } = "";
        public string TargetExpression { get; set; } = "";
        public string ManipulatorExpression { get; set; } = "";
        public string Config { get; set; } = "absent";
        public string ApplyByDefault { get; set; } = "implicit-true";
        public string Storage { get; set; } = "";
        public string Lifetime { get; set; } = "MODULE_IMMUTABLE_ACTIVE";
    }

    private sealed record DelegateLowering(string Kind, string Target, int ParameterCount);
    private sealed record StepEvidence(string PlanId, string Owner, int RegistrationOrdinal,
        string BeforeSha256, string AfterSha256, string NormalizedDiffSha256,
        string BeforeNormalizedIl, string AfterNormalizedIl, string NormalizedDiff,
        DelegateLowering[] DelegateLowerings);

    private static int Main(string[] args)
    {
        try
        {
            Options options = Parse(args);
            PlanDocument document = JsonSerializer.Deserialize<PlanDocument>(File.ReadAllText(options.Plan),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("frozen-IL plan is empty");
            bool legacy = document.SchemaVersion == 2 && document.Worker == "apple-everest-static-il-worker-v2";
            bool direct = document.SchemaVersion == 3 && document.Worker == "apple-everest-static-il-worker-v3";
            if (!legacy && !direct)
                throw new InvalidDataException("unsupported frozen-IL plan schema/worker");
            if (document.Transforms.Any(transform => transform.Mechanism == "DIRECT_ILHOOK") != direct)
                throw new InvalidDataException("direct-ILHook plans require the exact v3 worker schema");
            string actualPlanSha256 = PlanSha256(document.Transforms);
            if (!string.Equals(document.PlanSha256, actualPlanSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"frozen-IL plan hash mismatch: expected {document.PlanSha256}; actual {actualPlanSha256}");
            Transform[] transforms = document.Transforms.Where(transform =>
                transform.TargetMethod == options.TargetMethod).ToArray();
            if (transforms.Length == 0)
                throw new InvalidDataException("target has no registered transforms");
            if (transforms.Select(transform => transform.CanonicalTargetMethod).Distinct(StringComparer.Ordinal).Count() != 1 ||
                transforms.Select((transform, ordinal) => transform.RegistrationOrdinal == ordinal).Any(value => !value))
                throw new InvalidDataException("same-target registration order is not closed and contiguous");
            if (transforms.Any(transform => string.Equals(transform.BeforeSha256, transform.AfterSha256,
                    StringComparison.OrdinalIgnoreCase)) ||
                transforms.Skip(1).Select((transform, index) => string.Equals(transform.BeforeSha256,
                    transforms[index].AfterSha256, StringComparison.OrdinalIgnoreCase)).Any(matches => !matches))
                throw new InvalidDataException("same-target intermediate hash chain is not closed or contains a no-op");
            string[] duplicateManipulators = transforms.GroupBy(transform => string.Join('\0',
                    transform.AssemblySha256, transform.ManipulatorType, transform.ManipulatorMethod),
                    StringComparer.Ordinal)
                .Where(group => group.Count() > 1).Select(group => group.First().ManipulatorType + "::" +
                    group.First().ManipulatorMethod).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (duplicateManipulators.Length != 0)
                throw new InvalidDataException("duplicate frozen-IL manipulator registration: " +
                    string.Join(',', duplicateManipulators));
            InstallResolver(options, transforms);

            DefaultAssemblyResolver cecilResolver = new();
            foreach (string directory in options.RuntimeDirectories.Concat(new[]
                     { Path.GetDirectoryName(options.Target)!, Path.Combine(Path.GetDirectoryName(options.Plan)!, "fixtures") }).Distinct())
                cecilResolver.AddSearchDirectory(directory);

            ReaderParameters reader = new() { AssemblyResolver = cecilResolver, ReadSymbols = false };
            using ModuleDefinition module = ModuleDefinition.ReadModule(options.Target, reader);
            MethodDefinition target = ResolveTarget(module, options.TargetMethod);

            string canonicalTarget = transforms[0].CanonicalTargetMethod;
            string beforeText = Normalize(target, canonicalTarget);
            string before = Sha256(beforeText);
            if (string.Equals(before, transforms[^1].AfterSha256, StringComparison.OrdinalIgnoreCase))
            {
                ValidateBody(target);
                string[] alreadyFrozenReferences = target.Body.Instructions.Select(ReferenceIdentity)
                    .Where(value => value != null).Cast<string>().Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                string[] alreadyFrozenForbidden = alreadyFrozenReferences.Where(IsForbidden).ToArray();
                if (alreadyFrozenForbidden.Length != 0)
                    throw new InvalidDataException("forbidden final IL reference: " +
                                                   string.Join(",", alreadyFrozenForbidden));

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Output))!);
                module.Write(options.Output);
                WriteManifest(options, document, target.FullName, alreadyFrozen: true,
                    transforms[0].BeforeSha256, before, [], beforeText,
                    alreadyFrozenReferences, alreadyFrozenForbidden);
                Console.WriteLine($"APPLE_EVEREST_STATIC_IL_ALREADY_FROZEN={before}");
                return 0;
            }
            if (!string.Equals(before, transforms[0].BeforeSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"target baseline mismatch: expected {transforms[0].BeforeSha256}; actual {before}");

            List<StepEvidence> steps = [];
            foreach (Transform transform in transforms)
            {
                string currentText = Normalize(target, canonicalTarget);
                string current = Sha256(currentText);
                if (!string.Equals(current, transform.BeforeSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"intermediate chain mismatch before {transform.PlanId}: expected {transform.BeforeSha256}; actual {current}");
                string modPath = Path.Combine(Path.GetDirectoryName(options.Plan)!, "fixtures", transform.Owner + ".original.dll");
                if (FileSha256(modPath) != transform.AssemblySha256)
                    throw new InvalidDataException("manipulator fixture hash mismatch: " + transform.Owner);
                Assembly mod = Assembly.LoadFrom(Path.GetFullPath(modPath));
                Type manipulatorType = mod.GetType(transform.ManipulatorType, throwOnError: true, ignoreCase: false)!;
                MethodInfo manipulatorMethod = manipulatorType.GetMethod(transform.ManipulatorMethod,
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null, types: new[] { typeof(ILContext) }, modifiers: null)
                    ?? throw new InvalidDataException("exact manipulator not found");
                if (manipulatorMethod.IsStatic != transform.ManipulatorIsStatic || manipulatorMethod.ReturnType != typeof(void))
                    throw new InvalidDataException("manipulator static/instance signature drifted");
                object? instance = manipulatorMethod.IsStatic ? null : Activator.CreateInstance(manipulatorType)
                    ?? throw new InvalidDataException("instance manipulator owner could not be constructed");
                if (Delegate.CreateDelegate(typeof(ILContext.Manipulator), instance, manipulatorMethod,
                        throwOnBindFailure: true) is not ILContext.Manipulator manipulator)
                    throw new InvalidDataException("exact manipulator delegate could not be created");
                Dictionary<string, int> directCallsBefore = ExpectedDirectCallCounts(target,
                    transform.ExpectedDelegateTargets);
                DelegateLowering[] lowerings;
                using (ILContext context = new(target))
                {
                    context.Invoke(manipulator);
                    lowerings = LowerEmitDelegates(target, module, transform.ExpectedDelegateTargets,
                        directCallsBefore);
                }

                ValidateBody(target);
                string afterText = Normalize(target, canonicalTarget);
                string after = Sha256(afterText);
                if (current == after)
                    throw new InvalidDataException("manipulator produced no semantic target change: " + transform.PlanId);
                string diff = Diff(currentText, afterText);
                string diffHash = Sha256(diff);
                if (!string.Equals(after, transform.AfterSha256, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(diffHash, transform.DiffSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"transformed IL lock mismatch for {transform.PlanId}: after={after}; diff={diffHash}");
                steps.Add(new StepEvidence(transform.PlanId, transform.Owner, transform.RegistrationOrdinal,
                    current, after, diffHash, currentText, afterText, diff, lowerings));
            }
            string finalText = Normalize(target, canonicalTarget);
            string final = Sha256(finalText);
            string[] references = target.Body.Instructions.Select(ReferenceIdentity).Where(value => value != null)
                .Cast<string>().Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] forbidden = references.Where(IsForbidden).ToArray();
            if (forbidden.Length != 0)
                throw new InvalidDataException("forbidden final IL reference: " + string.Join(",", forbidden));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Output))!);
            module.Write(options.Output);
            WriteManifest(options, document, target.FullName, alreadyFrozen: false, before, final,
                steps.ToArray(), finalText, references, forbidden);
            Console.WriteLine($"APPLE_EVEREST_STATIC_IL_BEFORE={before}");
            Console.WriteLine($"APPLE_EVEREST_STATIC_IL_AFTER={final}");
            Console.WriteLine($"APPLE_EVEREST_STATIC_IL_STEPS={steps.Count}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("APPLE_EVEREST_STATIC_IL_FAILURE=" + exception.GetType().Name + ": " + exception.Message);
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static DelegateLowering[] LowerEmitDelegates(MethodDefinition method, ModuleDefinition module,
        IReadOnlyList<string> expectedTargets, IReadOnlyDictionary<string, int> directCallsBefore)
    {
        List<DelegateLowering> lowered = [];
        Dictionary<string, int> directCallsAfter = ExpectedDirectCallCounts(method, expectedTargets);
        foreach ((string identity, int count) in directCallsAfter.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            directCallsBefore.TryGetValue(identity, out int before);
            for (int index = before; index < count; index++)
            {
                MethodReference called = method.Body.Instructions.Select(instruction => instruction.Operand)
                    .OfType<MethodReference>().First(reference => RuntimeMethodIdentity(reference) == identity);
                lowered.Add(new DelegateLowering("static-noncapturing-direct", identity, called.Parameters.Count));
            }
        }
        Mono.Collections.Generic.Collection<Instruction> body = method.Body.Instructions;
        for (int index = 0; index + 3 < body.Count; index++)
        {
            if (!TryReadInt32(body[index], out int cellIndex) ||
                !TryReadInt32(body[index + 1], out int cellHash) ||
                body[index + 2].Operand is not MethodReference get ||
                get.DeclaringType.FullName != "MonoMod.Utils.DynamicReferenceManager" ||
                body[index + 3].Operand is not GenericInstanceMethod invoke ||
                !invoke.ElementMethod.DeclaringType.FullName.StartsWith("MonoMod.Cil.FastDelegateInvokers", StringComparison.Ordinal))
                continue;

            Delegate emitted = DynamicReferenceManager.GetValue<Delegate>(new DynamicReferenceCell(cellIndex, cellHash))
                ?? throw new InvalidDataException("EmitDelegate dynamic cell did not contain a delegate");
            string identity = emitted.Method.DeclaringType!.FullName + "::" + emitted.Method.Name;
            if (!expectedTargets.Contains(identity, StringComparer.Ordinal))
                throw new InvalidDataException("unreviewed EmitDelegate target: " + identity);
            ILProcessor processor = method.Body.GetILProcessor();
            MethodReference importedMethod = module.ImportReference(emitted.Method);
            ParameterInfo[] parameters = emitted.Method.GetParameters();
            List<Instruction> replacement = [];
            string kind;
            if (emitted.Target == null)
            {
                kind = "static-noncapturing";
                replacement.Add(Instruction.Create(OpCodes.Call, importedMethod));
            }
            else
            {
                Type targetType = emitted.Target.GetType();
                if (targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length != 0)
                    throw new InvalidDataException("capturing EmitDelegate closure is deferred: " + identity);
                FieldInfo[] matching = targetType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(field => object.ReferenceEquals(field.GetValue(null), emitted.Target)).ToArray();
                if (matching.Length != 1)
                    throw new InvalidDataException("noncapturing EmitDelegate singleton is not unambiguous: " + identity);
                kind = "compiler-singleton-noncapturing";
                VariableDefinition[] arguments = parameters.Select(parameter =>
                    new VariableDefinition(module.ImportReference(parameter.ParameterType))).ToArray();
                foreach (VariableDefinition argument in arguments)
                    method.Body.Variables.Add(argument);
                method.Body.InitLocals = true;
                for (int parameter = arguments.Length - 1; parameter >= 0; parameter--)
                    replacement.Add(Instruction.Create(OpCodes.Stloc, arguments[parameter]));
                replacement.Add(Instruction.Create(OpCodes.Ldsfld, module.ImportReference(matching[0])));
                foreach (VariableDefinition argument in arguments)
                    replacement.Add(Instruction.Create(OpCodes.Ldloc, argument));
                replacement.Add(Instruction.Create(OpCodes.Callvirt, importedMethod));
            }

            Instruction first = body[index];
            Instruction second = body[index + 1];
            Instruction third = body[index + 2];
            Instruction fourth = body[index + 3];
            first.OpCode = replacement[0].OpCode;
            first.Operand = replacement[0].Operand;
            Instruction cursor = first;
            foreach (Instruction instruction in replacement.Skip(1))
            {
                processor.InsertAfter(cursor, instruction);
                cursor = instruction;
            }
            processor.Remove(second);
            processor.Remove(third);
            processor.Remove(fourth);
            lowered.Add(new DelegateLowering(kind, identity, parameters.Length));
            index--;
        }
        string[] actual = lowered.Select(value => value.Target).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] expected = expectedTargets.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException("EmitDelegate lowering count/targets drifted: expected=" +
                string.Join(',', expected) + "; actual=" + string.Join(',', actual));
        return lowered.ToArray();
    }

    private static Dictionary<string, int> ExpectedDirectCallCounts(MethodDefinition method,
        IReadOnlyList<string> expectedTargets)
    {
        HashSet<string> expected = expectedTargets.ToHashSet(StringComparer.Ordinal);
        return method.Body.Instructions.Select(instruction => instruction.Operand).OfType<MethodReference>()
            .Select(RuntimeMethodIdentity).Where(expected.Contains)
            .GroupBy(identity => identity, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private static string RuntimeMethodIdentity(MethodReference method) =>
        method.DeclaringType.FullName.Replace('/', '+') + "::" + method.Name;

    private static bool TryReadInt32(Instruction instruction, out int value)
    {
        if (instruction.OpCode == OpCodes.Ldc_I4) { value = (int)instruction.Operand; return true; }
        if (instruction.OpCode == OpCodes.Ldc_I4_S) { value = (sbyte)instruction.Operand; return true; }
        value = instruction.OpCode.Code switch
        {
            Code.Ldc_I4_M1 => -1, Code.Ldc_I4_0 => 0, Code.Ldc_I4_1 => 1,
            Code.Ldc_I4_2 => 2, Code.Ldc_I4_3 => 3, Code.Ldc_I4_4 => 4,
            Code.Ldc_I4_5 => 5, Code.Ldc_I4_6 => 6, Code.Ldc_I4_7 => 7,
            Code.Ldc_I4_8 => 8, _ => 0
        };
        return instruction.OpCode.Code >= Code.Ldc_I4_M1 && instruction.OpCode.Code <= Code.Ldc_I4_8;
    }

    private static void WriteManifest(Options options, PlanDocument document, string target, bool alreadyFrozen,
        string before, string after, StepEvidence[] steps, string afterNormalizedIl,
        string[] references, string[] forbidden)
    {
        var manifest = new
        {
            schemaVersion = document.SchemaVersion,
            worker = document.Worker,
            planSha256 = document.PlanSha256,
            target,
            alreadyFrozen,
            beforeSha256 = before,
            afterSha256 = after,
            steps,
            afterNormalizedIl,
            injectedReferences = references,
            forbiddenReferences = forbidden,
            outputSha256 = FileSha256(options.Output)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Manifest))!);
        File.WriteAllText(options.Manifest, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        }) + "\n", new UTF8Encoding(false));
    }

    private static Options Parse(string[] args)
    {
        Options options = new();
        for (int index = 0; index < args.Length; index++)
        {
            string value = index + 1 < args.Length ? args[index + 1] : "";
            switch (args[index])
            {
                case "--target": options.Target = value; index++; break;
                case "--output": options.Output = value; index++; break;
                case "--plan": options.Plan = value; index++; break;
                case "--target-method": options.TargetMethod = value; index++; break;
                case "--manifest": options.Manifest = value; index++; break;
                case "--runtime-dir": options.RuntimeDirectories.Add(value); index++; break;
                default: throw new ArgumentException("unknown argument: " + args[index]);
            }
        }
        if (new[] { options.Target, options.Output, options.Plan, options.TargetMethod,
                    options.Manifest }.Any(string.IsNullOrWhiteSpace) ||
            options.RuntimeDirectories.Count == 0)
            throw new ArgumentException("missing required static-IL worker argument");
        return options;
    }

    private static void InstallResolver(Options options, IEnumerable<Transform> transforms)
    {
        string[] directories = options.RuntimeDirectories.Concat(new[]
            { Path.Combine(Path.GetDirectoryName(options.Plan)!, "fixtures") })
            .Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).ToArray();
        AppDomain.CurrentDomain.AssemblyResolve += (_, eventArgs) =>
        {
            string simpleName = new AssemblyName(eventArgs.Name).Name
                ?? throw new InvalidDataException("assembly resolution request has no simple name");
            string? path = directories.SelectMany(directory => new[]
                { Path.Combine(directory, simpleName + ".dll"), Path.Combine(directory, simpleName + ".exe") })
                .FirstOrDefault(File.Exists);
            return path == null ? null : Assembly.LoadFrom(path);
        };
    }

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition type)
    {
        yield return type;
        foreach (TypeDefinition nested in type.NestedTypes)
            foreach (TypeDefinition descendant in AllTypes(nested))
                yield return descendant;
    }

    private static MethodDefinition ResolveTarget(ModuleDefinition module, string identity)
    {
        MethodDefinition[] methods = module.Types.SelectMany(AllTypes).SelectMany(type => type.Methods).ToArray();
        MethodDefinition[] exact = methods.Where(method => method.FullName == identity).ToArray();
        if (exact.Length == 1) return exact[0];
        if (exact.Length != 0)
            throw new InvalidDataException("exact target method is ambiguous: " + identity);
        const string dashIterator = "System.Boolean Celeste.Player/<DashCoroutine>d__*::MoveNext()";
        if (identity != dashIterator)
            throw new InvalidDataException("exact target method not found: " + identity);
        MethodDefinition[] iterator = methods.Where(method =>
            method.Name == "MoveNext" && method.ReturnType.FullName == "System.Boolean" && !method.HasParameters &&
            method.DeclaringType.DeclaringType?.FullName == "Celeste.Player" &&
            method.DeclaringType.Name.StartsWith("<DashCoroutine>d__", StringComparison.Ordinal) &&
            method.DeclaringType.Name["<DashCoroutine>d__".Length..].All(char.IsAsciiDigit)).ToArray();
        if (iterator.Length != 1)
            throw new InvalidDataException("exact DashCoroutine iterator target is missing or ambiguous");
        return iterator[0];
    }

    private static string Normalize(MethodDefinition method, string semanticIdentity)
    {
        StringBuilder text = new();
        text.AppendLine("method " + semanticIdentity);
        text.AppendLine("initlocals " + method.Body.InitLocals.ToString().ToLowerInvariant());
        for (int index = 0; index < method.Body.Variables.Count; index++)
            text.AppendLine($"local V_{index} {TypeIdentity(method.Body.Variables[index].VariableType)}");
        Dictionary<Instruction, int> positions = new();
        for (int index = 0; index < method.Body.Instructions.Count; index++)
            positions.Add(method.Body.Instructions[index], index);
        for (int index = 0; index < method.Body.Instructions.Count; index++)
        {
            Instruction instruction = method.Body.Instructions[index];
            text.Append("IL_").Append(index.ToString("D4")).Append(' ').Append(instruction.OpCode.Code.ToString());
            string operand = OperandIdentity(instruction.Operand, positions);
            if (operand.Length != 0)
                text.Append(' ').Append(operand);
            text.AppendLine();
        }
        foreach (ExceptionHandler handler in method.Body.ExceptionHandlers)
            text.Append("eh ").Append(handler.HandlerType).Append(' ')
                .Append(Label(handler.TryStart, positions)).Append(' ').Append(Label(handler.TryEnd, positions)).Append(' ')
                .Append(Label(handler.HandlerStart, positions)).Append(' ').Append(Label(handler.HandlerEnd, positions)).Append(' ')
                .Append(Label(handler.FilterStart, positions)).Append(' ')
                .Append(handler.CatchType == null ? "-" : TypeIdentity(handler.CatchType)).AppendLine();
        string result = text.ToString();
        return semanticIdentity.Contains("<DashCoroutine>d__::", StringComparison.Ordinal)
            ? Regex.Replace(result, @"(?<=<DashCoroutine>d__)\d+", "", RegexOptions.CultureInvariant)
            : result;
    }

    private static string OperandIdentity(object? operand, IReadOnlyDictionary<Instruction, int> positions) => operand switch
    {
        null => "",
        Instruction instruction => Label(instruction, positions),
        Instruction[] instructions => "[" + string.Join(",", instructions.Select(value => Label(value, positions))) + "]",
        VariableDefinition variable => "V_" + variable.Index,
        ParameterDefinition parameter => "A_" + parameter.Index + ":" + TypeIdentity(parameter.ParameterType),
        MethodReference method => "method:" + method.FullName + "@" + Scope(method.DeclaringType),
        FieldReference field => "field:" + field.FullName + "@" + Scope(field.DeclaringType),
        TypeReference type => "type:" + TypeIdentity(type),
        CallSite site => "callsite:" + site.FullName,
        string value => "string:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)),
        float value => "r4:" + BitConverter.ToInt32(BitConverter.GetBytes(value), 0).ToString("x8"),
        double value => "r8:" + BitConverter.DoubleToInt64Bits(value).ToString("x16"),
        _ => operand.GetType().FullName + ":" + Convert.ToString(operand, System.Globalization.CultureInfo.InvariantCulture)
    };

    private static string Label(Instruction? instruction, IReadOnlyDictionary<Instruction, int> positions) =>
        instruction == null ? "-" : positions.TryGetValue(instruction, out int index) ? "IL_" + index.ToString("D4") : "INVALID";
    private static string TypeIdentity(TypeReference type) => type.FullName + "@" + Scope(type);
    private static string Scope(TypeReference type) => type.Scope?.Name ?? type.Module?.Assembly?.Name?.Name ?? "?";

    private static string? ReferenceIdentity(Instruction instruction) => instruction.Operand switch
    {
        MethodReference method => "method:" + method.FullName + "@" + Scope(method.DeclaringType),
        FieldReference field => "field:" + field.FullName + "@" + Scope(field.DeclaringType),
        TypeReference type => "type:" + TypeIdentity(type),
        _ => null
    };

    private static bool IsForbidden(string reference) => new[]
    {
        "MonoMod.Cil", "Mono.Cecil", "ILContext", "ILCursor", "ILLabel", "ILHook", "DynamicMethod",
        "DynamicMethodDefinition", "Reflection.Emit", "DynamicReferenceManager", "Assembly::Load"
    }.Any(reference.Contains);

    private static void ValidateBody(MethodDefinition method)
    {
        HashSet<Instruction> instructions = new(method.Body.Instructions);
        foreach (Instruction instruction in method.Body.Instructions)
        {
            if (instruction.Operand is Instruction target && !instructions.Contains(target))
                throw new InvalidDataException("dangling branch target");
            if (instruction.Operand is Instruction[] targets && targets.Any(target => !instructions.Contains(target)))
                throw new InvalidDataException("dangling switch target");
        }
        foreach (ExceptionHandler handler in method.Body.ExceptionHandlers)
            foreach (Instruction? endpoint in new[] { handler.TryStart, handler.TryEnd, handler.HandlerStart,
                         handler.HandlerEnd, handler.FilterStart })
                if (endpoint != null && !instructions.Contains(endpoint))
                    throw new InvalidDataException("dangling exception-handler endpoint");
    }

    private static string Diff(string before, string after)
    {
        string[] left = before.Split('\n');
        string[] right = after.Split('\n');
        StringBuilder diff = new();
        int maximum = Math.Max(left.Length, right.Length);
        for (int index = 0; index < maximum; index++)
        {
            string oldLine = index < left.Length ? left[index] : "<missing>";
            string newLine = index < right.Length ? right[index] : "<missing>";
            if (oldLine != newLine)
                diff.Append(index).Append('\t').Append(oldLine).Append("\t=>\t").Append(newLine).AppendLine();
        }
        return diff.ToString();
    }

    private static string Sha256(string value) => Sha256(Encoding.UTF8.GetBytes(value));

    private static string PlanSha256(IEnumerable<Transform> transforms) => Sha256(
        string.Join("\n", transforms.Select(transform => string.Join("\0", transform.PlanId,
            transform.AssemblySha256, transform.EventType, transform.EventName, transform.TargetMethod,
            transform.CanonicalTargetMethod, transform.ManipulatorType,
            transform.ManipulatorMethod, transform.ManipulatorIsStatic, transform.RegistrationOrdinal,
            transform.BeforeSha256, transform.AfterSha256, transform.DiffSha256,
            string.Join(',', transform.ExpectedDelegateTargets)) +
            (transform.Mechanism == "DIRECT_ILHOOK" ? "\0" + string.Join("\0", transform.Mechanism,
                transform.ConstructorSignature, transform.TargetExpression, transform.ManipulatorExpression,
                transform.Config, transform.ApplyByDefault, transform.Storage, transform.Lifetime) : ""))) + "\n");
    private static string FileSha256(string path) => Sha256(File.ReadAllBytes(path));
    private static string Sha256(byte[] bytes)
    {
        using SHA256 hash = SHA256.Create();
        return string.Concat(hash.ComputeHash(bytes).Select(value => value.ToString("x2")));
    }
}
