using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Diagnostics;

namespace AppleEverestBuilder;

internal static class RuntimeClosureScanner
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "MonoMod", "NLua", "KeraLua", "DiscordGameSDK", "Microsoft.CodeAnalysis"
    ];

    internal static IReadOnlyList<string> Inspect(string assemblyPath)
    {
        List<string> violations = [];
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(
            assemblyPath, new ReaderParameters { ReadSymbols = false });

        foreach (AssemblyNameReference reference in assembly.MainModule.AssemblyReferences)
        {
            if (ForbiddenAssemblyPrefixes.Any(prefix => reference.Name.StartsWith(prefix, StringComparison.Ordinal)))
                violations.Add("assembly-reference:" + reference.Name);
        }

        foreach (TypeDefinition type in assembly.MainModule.Types.SelectMany(AllTypes))
        {
            if (Forbidden(type.FullName) && !AllowedStaticFacadeType(assembly, type) && !AllowedEmbeddedCompilerMarker(type))
                violations.Add("type:" + type.FullName);
            foreach (MethodDefinition method in type.Methods.Where(value => value.HasBody))
            foreach (Instruction instruction in method.Body.Instructions)
            {
                if (instruction.Operand is not MethodReference called) continue;
                string full = called.DeclaringType.FullName + "::" + called.Name;
                if (Forbidden(full) && !AllowedStaticFacadeCall(called)) violations.Add("call:" + full);
            }
        }

        return violations.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    internal static void Verify(string assemblyPath)
    {
        IReadOnlyList<string> violations = Inspect(assemblyPath);
        if (violations.Count != 0)
            throw new InvalidDataException("linked device runtime contains forbidden dynamic/mod-loader closure: " +
                                           string.Join(", ", violations));
    }

    internal static void VerifyPreserved(string sourceAssemblyPath, string linkedAssemblyPath)
    {
        using AssemblyDefinition source = AssemblyDefinition.ReadAssembly(
            sourceAssemblyPath, new ReaderParameters { ReadSymbols = false });
        using AssemblyDefinition linked = AssemblyDefinition.ReadAssembly(
            linkedAssemblyPath, new ReaderParameters { ReadSymbols = false });
        if (source.Name.Name != linked.Name.Name)
            throw new InvalidDataException("linked external assembly identity changed");

        Dictionary<string, MethodDefinition> linkedMethods = linked.MainModule.Types.SelectMany(AllTypes)
            .SelectMany(type => type.Methods).ToDictionary(method => method.FullName, StringComparer.Ordinal);
        List<string> missingBodies = [];
        foreach (MethodDefinition method in source.MainModule.Types.SelectMany(AllTypes).SelectMany(type => type.Methods))
        {
            if (!linkedMethods.TryGetValue(method.FullName, out MethodDefinition? linkedMethod))
            {
                missingBodies.Add("missing:" + method.FullName);
                continue;
            }
            if (method.HasBody && method.Body.Instructions.Count != 0 &&
                (!linkedMethod.HasBody || linkedMethod.Body.Instructions.Count == 0))
                missingBodies.Add("body:" + method.FullName);
        }
        if (missingBodies.Count != 0)
            throw new InvalidDataException("linked external assembly lost executable methods: " +
                string.Join(", ", missingBodies.Take(8)));
    }

    internal static void VerifyReferencedApi(string sourceAssemblyPath, string targetAssemblyPath)
    {
        string targetDirectory = Path.GetDirectoryName(Path.GetFullPath(targetAssemblyPath))!;
        string sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceAssemblyPath))!;
        DefaultAssemblyResolver resolver = new();
        resolver.AddSearchDirectory(targetDirectory);
        resolver.AddSearchDirectory(sourceDirectory);
        using AssemblyDefinition target = AssemblyDefinition.ReadAssembly(targetAssemblyPath,
            new ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver });
        using AssemblyDefinition source = AssemblyDefinition.ReadAssembly(sourceAssemblyPath,
            new ReaderParameters { ReadSymbols = false, AssemblyResolver = resolver });

        string targetName = target.Name.Name;
        List<string> unresolved = [];
        foreach (TypeReference type in source.MainModule.GetTypeReferences()
                     .Where(type => ScopeName(type) == targetName))
        {
            try
            {
                if (type.Resolve() == null) unresolved.Add("type:" + type.FullName);
            }
            catch (AssemblyResolutionException)
            {
                unresolved.Add("type:" + type.FullName);
            }
        }
        foreach (MemberReference member in source.MainModule.GetMemberReferences()
                     .Where(member => ScopeName(member.DeclaringType) == targetName))
        {
            try
            {
                string? inaccessible = member switch
                {
                    MethodReference method when method.Resolve() is MethodDefinition definition &&
                        !ExternalAccess(definition) => "inaccessible-method:" + method.FullName,
                    FieldReference field when field.Resolve() is FieldDefinition definition &&
                        !ExternalAccess(definition) => "inaccessible-field:" + field.FullName,
                    _ => null
                };
                bool resolved = member switch
                {
                    MethodReference method => method.Resolve() != null,
                    FieldReference field => field.Resolve() != null,
                    _ => true
                };
                if (!resolved) unresolved.Add("member:" + member.FullName);
                else if (inaccessible != null) unresolved.Add(inaccessible);
            }
            catch (ResolutionException)
            {
                unresolved.Add("member:" + member.FullName);
            }
        }
        if (unresolved.Count != 0)
            throw new InvalidDataException("external assembly references APIs absent from the linked " + targetName +
                " contract or inaccessible: " + string.Join(", ", unresolved.Distinct(StringComparer.Ordinal).Take(64)));
    }

    internal static void VerifyAotObjects(string sourceAssemblyPath, IReadOnlyList<string> objectPaths)
    {
        if (objectPaths.Count == 0) throw new InvalidDataException("at least one AOT object is required");
        List<string> symbols = [];
        foreach (string objectPath in objectPaths)
        {
            ProcessStartInfo info = new("/usr/bin/nm")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            info.ArgumentList.Add("-j");
            info.ArgumentList.Add(objectPath);
            using Process process = Process.Start(info) ?? throw new InvalidOperationException("failed to start nm");
            symbols.AddRange(process.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries));
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidDataException("nm failed: " + error.Trim());
        }

        using AssemblyDefinition source = AssemblyDefinition.ReadAssembly(sourceAssemblyPath,
            new ReaderParameters { ReadSymbols = false });
        List<string> missing = [];
        foreach (TypeDefinition type in source.MainModule.Types.SelectMany(AllTypes)
                     .Where(type => !type.HasGenericParameters))
        foreach (MethodDefinition method in type.Methods.Where(method =>
                     method.HasBody && method.Body.Instructions.Count != 0 && !method.HasGenericParameters &&
                     !method.IsAbstract && !method.IsPInvokeImpl))
        {
            string methodName = AotName(type.FullName) + "_" + AotName(method.Name);
            string llvmPrefix = "_" + AotName(source.Name.Name) + "_" + methodName;
            bool present = symbols.Any(symbol => symbol.StartsWith(llvmPrefix, StringComparison.Ordinal) ||
                                                 symbol.StartsWith(methodName, StringComparison.Ordinal));
            // The full-AOT compiler may eliminate an individually preserved
            // but unreachable compiler-generated lambda while still emitting
            // the closure type and every reachable sibling. Require native
            // evidence for that exact generated type instead of inventing a
            // symbol the compiler intentionally did not emit.
            if (!present && CompilerGenerated(type, method))
            {
                string typePrefix = "_" + AotName(source.Name.Name) + "_" + AotName(type.FullName) + "_";
                present = symbols.Any(symbol => symbol.StartsWith(typePrefix, StringComparison.Ordinal));
            }
            if (!present)
                missing.Add(method.FullName);
        }
        if (missing.Count != 0)
            throw new InvalidDataException("full-AOT object omitted executable external methods: " +
                string.Join(", ", missing.Take(12)));
    }

    private static string ScopeName(TypeReference type) => type.GetElementType().Scope switch
    {
        AssemblyNameReference assembly => assembly.Name,
        ModuleDefinition module => module.Assembly.Name.Name,
        _ => string.Empty
    };

    private static bool ExternalAccess(MethodDefinition method) =>
        method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    private static bool ExternalAccess(FieldDefinition field) =>
        field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;

    private static bool CompilerGenerated(TypeDefinition type, MethodDefinition method) =>
        type.Name.Contains('<', StringComparison.Ordinal) || method.Name.Contains('<', StringComparison.Ordinal) ||
        type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName ==
            "System.Runtime.CompilerServices.CompilerGeneratedAttribute") ||
        method.CustomAttributes.Any(attribute => attribute.AttributeType.FullName ==
            "System.Runtime.CompilerServices.CompilerGeneratedAttribute");

    private static string AotName(string value)
    {
        // Mono's AOT symbol mangling maps the opening compiler-generated angle
        // bracket to an underscore, drops the closing bracket, and maps other
        // metadata punctuation to underscores.
        return new string(value.Where(character => character != '>')
            .Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
    }

    private static bool Forbidden(string value) =>
        ForbiddenAssemblyPrefixes.Any(prefix => value.Contains(prefix, StringComparison.Ordinal)) ||
        value.Contains("MonoMod.RuntimeDetour", StringComparison.Ordinal) ||
        value.Contains("System.Reflection.Emit", StringComparison.Ordinal) ||
        value.Contains("System.Reflection.Assembly::Load", StringComparison.Ordinal) ||
        value.Contains("System.Runtime.Loader.AssemblyLoadContext::Load", StringComparison.Ordinal) ||
        value.Contains("System.Runtime.InteropServices.NativeLibrary::Load", StringComparison.Ordinal) ||
        value.Contains("System.Diagnostics.Process::Start", StringComparison.Ordinal) ||
        value.Contains("System.IO.FileSystemWatcher::.ctor", StringComparison.Ordinal);

    private static bool AllowedStaticFacadeType(AssemblyDefinition assembly, TypeDefinition type) =>
        assembly.Name.Name == "Celeste" &&
        (type.Namespace == "MonoMod.RuntimeDetour" && type.Name is "Hook" or "DetourConfig" ||
         type.Namespace == "MonoMod.ModInterop" && type.Name is "ModInteropManager" or "ModExportNameAttribute" or "ModImportNameAttribute");

    private static bool AllowedEmbeddedCompilerMarker(TypeDefinition type) =>
        type.FullName == "Microsoft.CodeAnalysis.EmbeddedAttribute" &&
        type.BaseType?.FullName == "System.Attribute" &&
        type.Methods.All(method => method.IsConstructor && method.Parameters.Count == 0);

    private static bool AllowedStaticFacadeCall(MethodReference method)
    {
        if (ScopeName(method.DeclaringType) != "Celeste")
            return false;
        if (method.DeclaringType.Namespace == "MonoMod.ModInterop")
            return method.DeclaringType.Name switch
            {
                "ModInteropManager" => method.Name == "ModInterop",
                "ModExportNameAttribute" or "ModImportNameAttribute" => method.Name is ".ctor" or "get_Name",
                _ => false
            };
        if (method.DeclaringType.Namespace != "MonoMod.RuntimeDetour") return false;
        return method.DeclaringType.Name switch
        {
            "Hook" => method.Name is ".ctor" or "Apply" or "Undo" or "Dispose" or
                "get_IsApplied" or "get_IsValid",
            "DetourConfig" => method.Name is ".ctor" or "get_ID" or "set_ID" or
                "get_Priority" or "set_Priority" or "get_SubPriority" or "set_SubPriority" or
                "get_Before" or "set_Before" or "get_After" or "set_After",
            _ => false
        };
    }

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition root)
    {
        yield return root;
        foreach (TypeDefinition nested in root.NestedTypes.SelectMany(AllTypes)) yield return nested;
    }
}
