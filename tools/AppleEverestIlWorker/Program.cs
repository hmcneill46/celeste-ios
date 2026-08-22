using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

internal static class Program
{
    private sealed class Options
    {
        public string Target = "";
        public string Output = "";
        public string Mod = "";
        public string TargetMethod = "";
        public string CanonicalTargetMethod = "";
        public string ManipulatorType = "";
        public string ManipulatorMethod = "";
        public string ExpectedBefore = "";
        public string ExpectedAfter = "";
        public string ExpectedDiff = "";
        public string Manifest = "";
        public readonly List<string> RuntimeDirectories = new();
    }

    private static int Main(string[] args)
    {
        try
        {
            Options options = Parse(args);
            InstallResolver(options);

            DefaultAssemblyResolver cecilResolver = new();
            foreach (string directory in options.RuntimeDirectories.Concat(new[]
                     { Path.GetDirectoryName(options.Target)!, Path.GetDirectoryName(options.Mod)! }).Distinct())
                cecilResolver.AddSearchDirectory(directory);

            ReaderParameters reader = new() { AssemblyResolver = cecilResolver, ReadSymbols = false };
            using ModuleDefinition module = ModuleDefinition.ReadModule(options.Target, reader);
            MethodDefinition target = module.Types.SelectMany(AllTypes).SelectMany(type => type.Methods)
                .SingleOrDefault(method => method.FullName == options.TargetMethod)
                ?? throw new InvalidDataException("exact target method not found: " + options.TargetMethod);

            string beforeText = Normalize(target, options.CanonicalTargetMethod);
            string before = Sha256(beforeText);
            if (string.Equals(before, options.ExpectedAfter, StringComparison.OrdinalIgnoreCase))
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
                WriteManifest(options, target.FullName, alreadyFrozen: true,
                    options.ExpectedBefore, before, options.ExpectedDiff,
                    beforeNormalizedIl: null, afterNormalizedIl: beforeText,
                    normalizedDiff: null, alreadyFrozenReferences, alreadyFrozenForbidden);
                Console.WriteLine($"APPLE_EVEREST_STATIC_IL_ALREADY_FROZEN={before}");
                return 0;
            }
            if (!string.Equals(before, options.ExpectedBefore, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"target baseline mismatch: expected {options.ExpectedBefore}; actual {before}");

            Assembly mod = Assembly.LoadFrom(Path.GetFullPath(options.Mod));
            Type manipulatorType = mod.GetType(options.ManipulatorType, throwOnError: true, ignoreCase: false)!;
            MethodInfo manipulatorMethod = manipulatorType.GetMethod(options.ManipulatorMethod,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: new[] { typeof(ILContext) }, modifiers: null)
                ?? throw new InvalidDataException("exact static manipulator not found");
            if (!manipulatorMethod.IsStatic || manipulatorMethod.ReturnType != typeof(void))
                throw new InvalidDataException("only static void(ILContext) manipulators are accepted");

            if (Delegate.CreateDelegate(typeof(ILContext.Manipulator), manipulatorMethod,
                    throwOnBindFailure: true) is not ILContext.Manipulator manipulator)
                throw new InvalidDataException("exact manipulator delegate could not be created");
            using (ILContext context = new(target))
                context.Invoke(manipulator);

            ValidateBody(target);
            string afterText = Normalize(target, options.CanonicalTargetMethod);
            string after = Sha256(afterText);
            if (before == after)
                throw new InvalidDataException("manipulator produced no semantic target change");

            string diff = Diff(beforeText, afterText);
            string diffHash = Sha256(diff);
            if (!string.Equals(after, options.ExpectedAfter, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(diffHash, options.ExpectedDiff, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"transformed IL lock mismatch: after={after}; diff={diffHash}");
            string[] references = target.Body.Instructions.Select(ReferenceIdentity).Where(value => value != null)
                .Cast<string>().Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] forbidden = references.Where(IsForbidden).ToArray();
            if (forbidden.Length != 0)
                throw new InvalidDataException("forbidden final IL reference: " + string.Join(",", forbidden));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Output))!);
            module.Write(options.Output);
            WriteManifest(options, target.FullName, alreadyFrozen: false, before, after, diffHash,
                beforeText, afterText, diff, references, forbidden);
            Console.WriteLine($"APPLE_EVEREST_STATIC_IL_BEFORE={before}");
            Console.WriteLine($"APPLE_EVEREST_STATIC_IL_AFTER={after}");
            Console.WriteLine($"APPLE_EVEREST_STATIC_IL_DIFF={diffHash}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("APPLE_EVEREST_STATIC_IL_FAILURE=" + exception.GetType().Name + ": " + exception.Message);
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void WriteManifest(Options options, string target, bool alreadyFrozen,
        string before, string after, string diffHash, string? beforeNormalizedIl,
        string afterNormalizedIl, string? normalizedDiff, string[] references, string[] forbidden)
    {
        var manifest = new
        {
            schemaVersion = 1,
            target,
            manipulator = options.ManipulatorType + "::" + options.ManipulatorMethod,
            alreadyFrozen,
            beforeSha256 = before,
            afterSha256 = after,
            normalizedDiffSha256 = diffHash,
            beforeNormalizedIl,
            afterNormalizedIl,
            normalizedDiff,
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
                case "--mod": options.Mod = value; index++; break;
                case "--target-method": options.TargetMethod = value; index++; break;
                case "--canonical-target-method": options.CanonicalTargetMethod = value; index++; break;
                case "--manipulator-type": options.ManipulatorType = value; index++; break;
                case "--manipulator-method": options.ManipulatorMethod = value; index++; break;
                case "--expected-before": options.ExpectedBefore = value; index++; break;
                case "--expected-after": options.ExpectedAfter = value; index++; break;
                case "--expected-diff": options.ExpectedDiff = value; index++; break;
                case "--manifest": options.Manifest = value; index++; break;
                case "--runtime-dir": options.RuntimeDirectories.Add(value); index++; break;
                default: throw new ArgumentException("unknown argument: " + args[index]);
            }
        }
        if (new[] { options.Target, options.Output, options.Mod, options.TargetMethod, options.CanonicalTargetMethod,
                    options.ManipulatorType, options.ManipulatorMethod, options.ExpectedBefore, options.ExpectedAfter,
                    options.ExpectedDiff, options.Manifest }.Any(string.IsNullOrWhiteSpace) ||
            options.RuntimeDirectories.Count == 0)
            throw new ArgumentException("missing required static-IL worker argument");
        return options;
    }

    private static void InstallResolver(Options options)
    {
        string[] directories = options.RuntimeDirectories.Concat(new[] { Path.GetDirectoryName(options.Mod)! })
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
        return text.ToString();
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
    private static string FileSha256(string path) => Sha256(File.ReadAllBytes(path));
    private static string Sha256(byte[] bytes)
    {
        using SHA256 hash = SHA256.Create();
        return string.Concat(hash.ComputeHash(bytes).Select(value => value.ToString("x2")));
    }
}
