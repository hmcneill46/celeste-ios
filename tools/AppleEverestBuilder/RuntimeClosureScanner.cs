using Mono.Cecil;
using Mono.Cecil.Cil;

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
            if (Forbidden(type.FullName)) violations.Add("type:" + type.FullName);
            foreach (MethodDefinition method in type.Methods.Where(value => value.HasBody))
            foreach (Instruction instruction in method.Body.Instructions)
            {
                if (instruction.Operand is not MethodReference called) continue;
                string full = called.DeclaringType.FullName + "::" + called.Name;
                if (Forbidden(full)) violations.Add("call:" + full);
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

    private static bool Forbidden(string value) =>
        ForbiddenAssemblyPrefixes.Any(prefix => value.Contains(prefix, StringComparison.Ordinal)) ||
        value.Contains("MonoMod.RuntimeDetour", StringComparison.Ordinal) ||
        value.Contains("System.Reflection.Emit", StringComparison.Ordinal) ||
        value.Contains("System.Reflection.Assembly::Load", StringComparison.Ordinal) ||
        value.Contains("System.Runtime.Loader.AssemblyLoadContext::Load", StringComparison.Ordinal) ||
        value.Contains("System.Runtime.InteropServices.NativeLibrary::Load", StringComparison.Ordinal) ||
        value.Contains("System.Diagnostics.Process::Start", StringComparison.Ordinal) ||
        value.Contains("System.IO.FileSystemWatcher::.ctor", StringComparison.Ordinal);

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition root)
    {
        yield return root;
        foreach (TypeDefinition nested in root.NestedTypes.SelectMany(AllTypes)) yield return nested;
    }
}
