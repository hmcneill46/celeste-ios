using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AppleEverestBuilder;

internal static class CompatibilityAnalyzer
{
    private static readonly (string Needle, CompatibilityClass Classification)[] SourceRules =
    [
        ("IL.Celeste.", CompatibilityClass.IL_HOOK_DEFERRED),
        ("new ILHook", CompatibilityClass.IL_HOOK_DEFERRED),
        ("new Hook(", CompatibilityClass.DIRECT_HOOK_DEFERRED),
        ("NativeDetour", CompatibilityClass.NATIVE_UNSUPPORTED),
        ("DllImport", CompatibilityClass.NATIVE_UNSUPPORTED),
        ("LibraryImport", CompatibilityClass.NATIVE_UNSUPPORTED),
        ("NLua", CompatibilityClass.LUA_UNSUPPORTED),
        ("KeraLua", CompatibilityClass.LUA_UNSUPPORTED),
        ("Assembly.Load", CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED),
        ("AssemblyLoadContext", CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED),
        ("DynamicMethod", CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED),
        ("Reflection.Emit", CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED),
        ("System.Reflection.Emit", CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED),
        ("RuntimeDetour", CompatibilityClass.DIRECT_HOOK_DEFERRED),
        ("Process.Start", CompatibilityClass.PLATFORM_UNSUPPORTED),
        ("FileSystemWatcher", CompatibilityClass.PLATFORM_UNSUPPORTED)
    ];

    public static ResolvedMod Analyze(ModInput input, EverestYamlEntry metadata)
    {
        List<string> managed = input.Files.Where(file => IsManaged(file.Path)).Select(file => file.Path).ToList();
        List<string> content = input.Files.Where(file => IsContent(file.Path)).Select(file => file.Path).ToList();
        SortedSet<string> mechanisms = new(StringComparer.Ordinal);
        CompatibilityClass classification = string.IsNullOrWhiteSpace(metadata.DLL)
            ? CompatibilityClass.CONTENT_ONLY
            : CompatibilityClass.STATIC_MODULE;

        foreach (string relative in managed)
        {
            string path = Path.Combine(input.StagingRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (relative.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                string source = File.ReadAllText(path, new UTF8Encoding(false, true));
                foreach ((string needle, CompatibilityClass detected) in SourceRules)
                    if (source.Contains(needle, StringComparison.Ordinal)) Record($"{relative}:{needle}", detected);
                if (source.Contains("Everest.Events.", StringComparison.Ordinal)) Record($"{relative}:Everest.Events", CompatibilityClass.NORMAL_EVENT);
                if (source.Contains("On.Celeste.", StringComparison.Ordinal))
                {
                    string remaining = source
                        .Replace("On.Celeste.Dialog.Clean", "", StringComparison.Ordinal)
                        .Replace("On.Celeste.Dialog.orig_Clean", "", StringComparison.Ordinal);
                    if (remaining.Contains("On.Celeste.", StringComparison.Ordinal))
                        Record($"{relative}:unsupported On.Celeste target", CompatibilityClass.ON_HOOK_DEFERRED);
                    else
                        Record($"{relative}:On.Celeste.Dialog.Clean", CompatibilityClass.ON_HOOK_SUPPORTED);
                }
            }
            else if (relative.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                AnalyzeAssembly(path, (mechanism, detected) => Record($"{relative}:{mechanism}", detected));
            }
        }

        if (!string.IsNullOrWhiteSpace(metadata.DLL))
        {
            string normalized = metadata.DLL!.Replace('\\', '/');
            if (!input.Files.Any(file => string.Equals(file.Path, normalized, StringComparison.Ordinal)))
                throw new InvalidDataException($"declared DLL/source entry is missing for {metadata.Name}: {metadata.DLL}");
            if (!managed.Any(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"{metadata.Name} rejected before AOT: the initial profile requires reviewed source alongside its DLL");
        }

        if (classification is CompatibilityClass.ON_HOOK_DEFERRED or CompatibilityClass.IL_HOOK_DEFERRED or CompatibilityClass.DIRECT_HOOK_DEFERRED or
            CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED or CompatibilityClass.NATIVE_UNSUPPORTED or
            CompatibilityClass.LUA_UNSUPPORTED or CompatibilityClass.PLATFORM_UNSUPPORTED)
            throw new InvalidDataException($"{metadata.Name} rejected before AOT: {classification}; mechanisms={string.Join(',', mechanisms)}");

        return new ResolvedMod
        {
            Metadata = metadata,
            Input = input,
            Classification = classification,
            Mechanisms = mechanisms,
            ManagedFiles = managed,
            ContentFiles = content
        };

        void Record(string mechanism, CompatibilityClass detected)
        {
            mechanisms.Add(mechanism);
            classification = MoreRestrictive(classification, detected);
        }
    }

    private static void AnalyzeAssembly(string path, Action<string, CompatibilityClass> record)
    {
        try
        {
            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadSymbols = false });
            foreach (AssemblyNameReference reference in assembly.MainModule.AssemblyReferences)
            {
                string name = reference.Name;
                if (name is "NLua" or "KeraLua") record(name, CompatibilityClass.LUA_UNSUPPORTED);
                else if (name.StartsWith("MonoMod.RuntimeDetour", StringComparison.Ordinal)) record(name, CompatibilityClass.DIRECT_HOOK_DEFERRED);
            }
            foreach (TypeDefinition type in assembly.MainModule.Types.SelectMany(AllTypes))
            {
                foreach (PInvokeInfo? pinvoke in type.Methods.Select(method => method.PInvokeInfo).Where(value => value != null))
                    record($"PInvoke:{pinvoke!.Module.Name}", CompatibilityClass.NATIVE_UNSUPPORTED);
                foreach (MethodDefinition method in type.Methods.Where(method => method.HasBody))
                foreach (Instruction instruction in method.Body.Instructions)
                {
                    if (instruction.Operand is not MethodReference called) continue;
                    string full = called.DeclaringType.FullName + "::" + called.Name;
                    if (full.Contains("Assembly::Load", StringComparison.Ordinal) || full.Contains("AssemblyLoadContext", StringComparison.Ordinal) ||
                        full.Contains("DynamicMethod", StringComparison.Ordinal) || full.Contains("System.Reflection.Emit", StringComparison.Ordinal))
                        record(full, CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED);
                    else if (full.Contains("NativeDetour", StringComparison.Ordinal)) record(full, CompatibilityClass.NATIVE_UNSUPPORTED);
                    else if (full.Contains("ILHook", StringComparison.Ordinal)) record(full, CompatibilityClass.IL_HOOK_DEFERRED);
                    else if (full.Contains("MonoMod.RuntimeDetour.Hook", StringComparison.Ordinal)) record(full, CompatibilityClass.DIRECT_HOOK_DEFERRED);
                }
            }
        }
        catch (BadImageFormatException)
        {
            throw new InvalidDataException($"declared managed assembly is not valid CLI metadata: {Path.GetFileName(path)}");
        }
    }

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition root)
    {
        yield return root;
        foreach (TypeDefinition nested in root.NestedTypes.SelectMany(AllTypes)) yield return nested;
    }

    private static CompatibilityClass MoreRestrictive(CompatibilityClass current, CompatibilityClass detected)
    {
        int Rank(CompatibilityClass value) => value switch
        {
            CompatibilityClass.CONTENT_ONLY => 0,
            CompatibilityClass.STATIC_MODULE => 1,
            CompatibilityClass.NORMAL_EVENT => 2,
            CompatibilityClass.ON_HOOK_SUPPORTED => 3,
            CompatibilityClass.ON_HOOK_DEFERRED => 9,
            CompatibilityClass.IL_HOOK_DEFERRED => 10,
            CompatibilityClass.DIRECT_HOOK_DEFERRED => 11,
            CompatibilityClass.DYNAMIC_CODE_UNSUPPORTED => 12,
            CompatibilityClass.NATIVE_UNSUPPORTED => 13,
            CompatibilityClass.LUA_UNSUPPORTED => 14,
            CompatibilityClass.PLATFORM_UNSUPPORTED => 15,
            _ => 99
        };
        return Rank(detected) > Rank(current) ? detected : current;
    }

    private static bool IsManaged(string path) => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
    private static bool IsContent(string path) => path.StartsWith("Content/", StringComparison.Ordinal);
}
