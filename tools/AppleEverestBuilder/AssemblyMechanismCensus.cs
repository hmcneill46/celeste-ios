using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;
using System.Text.Json;

namespace AppleEverestBuilder;

/// <summary>
/// Read-only assembly census used by compatibility audits.  It records metadata
/// and actual IL call sites without loading the reviewed assembly or executing
/// any of its module initializers.
/// </summary>
internal static class AssemblyMechanismCensus
{
    private sealed record Site(string Owner, string Target, int Offset);
    private sealed record CustomId(string Kind, string Id, string Type);

    internal static void Write(string dllPath, string outputPath)
    {
        dllPath = Path.GetFullPath(dllPath);
        outputPath = Path.GetFullPath(outputPath);
        if (!File.Exists(dllPath)) throw new FileNotFoundException("DLL input does not exist", dllPath);

        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(dllPath,
            new ReaderParameters { ReadingMode = ReadingMode.Deferred, ReadSymbols = false });
        TypeDefinition[] types = assembly.Modules.SelectMany(module => module.Types)
            .SelectMany(Flatten).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
        MethodDefinition[] methods = types.SelectMany(type => type.Methods)
            .OrderBy(method => method.FullName, StringComparer.Ordinal).ToArray();

        Dictionary<string, List<Site>> sites = Categories().ToDictionary(value => value,
            _ => new List<Site>(), StringComparer.Ordinal);
        List<object> configuredContexts = [];
        foreach (MethodDefinition method in methods.Where(method => method.HasBody))
        {
            Instruction[] instructions = method.Body.Instructions.ToArray();
            bool configured = false;
            foreach (Instruction instruction in instructions)
            {
                if (instruction.Operand is not MethodReference target) continue;
                string declaring = target.DeclaringType.FullName;
                string name = target.Name;
                string identity = target.FullName;
                void Add(string category) => sites[category].Add(new Site(method.FullName, identity,
                    instruction.Offset));

                if (declaring.StartsWith("On.", StringComparison.Ordinal) && name.StartsWith("add_", StringComparison.Ordinal)) Add("onHookSubscriptions");
                if (declaring.StartsWith("IL.", StringComparison.Ordinal) && name.StartsWith("add_", StringComparison.Ordinal)) Add("ilEventSubscriptions");
                if (declaring == "MonoMod.RuntimeDetour.Hook" && name == ".ctor") Add("directHookConstructors");
                if (declaring == "MonoMod.RuntimeDetour.ILHook" && name == ".ctor") Add("directIlHookConstructors");
                if (declaring.Contains("DetourConfig", StringComparison.Ordinal)) { Add("detourConfigReferences"); configured = true; }
                if (declaring.Contains("DetourContext", StringComparison.Ordinal)) { Add("detourContextReferences"); configured = true; }
                if (declaring.Contains("ILCursor", StringComparison.Ordinal) && name.Contains("EmitDelegate", StringComparison.Ordinal)) Add("emitDelegateCalls");
                if (declaring.Contains("MonoMod.Utils.DynamicData", StringComparison.Ordinal)) Add("dynamicDataCalls");
                if (declaring.Contains("MonoMod.Utils.DynData", StringComparison.Ordinal)) Add("dynDataCalls");
                if (declaring.Contains("FastReflection", StringComparison.Ordinal)) Add("fastReflectionCalls");
                if (declaring.Contains("ModInterop", StringComparison.Ordinal)) Add("modInteropCalls");
                if (declaring.StartsWith("Celeste.Mod.Everest+Content", StringComparison.Ordinal) || declaring == "Celeste.Mod.Everest.Content") Add("everestContentCalls");
                if (declaring.StartsWith("System.Reflection", StringComparison.Ordinal) || declaring == "System.Type") Add("reflectionCalls");
                if (declaring == "System.Reflection.Assembly" && name.StartsWith("Load", StringComparison.Ordinal)) Add("assemblyLoadCalls");
                if (declaring.Contains("DynamicMethod", StringComparison.Ordinal)) Add("dynamicMethodCalls");
                if (declaring.StartsWith("System.Reflection.Emit", StringComparison.Ordinal)) Add("reflectionEmitCalls");
                if (declaring.StartsWith("FMOD", StringComparison.Ordinal)) Add("fmodCalls");
                if (declaring == "System.Diagnostics.Process") Add("processCalls");
                if (declaring == "System.IO.FileSystemWatcher") Add("fileSystemWatcherCalls");
            }
            if (configured)
            {
                configuredContexts.Add(new
                {
                    owner = method.FullName,
                    stringConstants = instructions.Where(value => value.OpCode == OpCodes.Ldstr)
                        .Select(value => (string?)value.Operand).Where(value => value != null)
                        .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
                });
            }
        }

        List<CustomId> customIds = [];
        foreach (TypeDefinition type in types)
        {
            foreach (CustomAttribute attribute in type.CustomAttributes)
            {
                string kind = attribute.AttributeType.Name switch
                {
                    "CustomEntityAttribute" => "entity",
                    "CustomTriggerAttribute" => "trigger",
                    "CustomBackdropAttribute" => "backdrop",
                    _ => ""
                };
                if (kind.Length == 0) continue;
                IEnumerable<string> ids;
                try { ids = AttributeStrings(attribute).ToArray(); }
                catch (AssemblyResolutionException) { continue; }
                foreach (string id in ids)
                {
                    foreach (string value in id.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        // Everest permits "entity/id = namedLoader" aliases in a
                        // CustomEntity declaration.  The map stores only the left
                        // side, which is the stable ownership identity required by
                        // the content census.
                        string stableId = value.Split('=', 2, StringSplitOptions.TrimEntries)[0];
                        customIds.Add(new CustomId(kind, stableId, type.FullName));
                    }
                }
            }
        }

        object report = new
        {
            schemaVersion = 1,
            file = Path.GetFileName(dllPath),
            bytes = new FileInfo(dllPath).Length,
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dllPath))).ToLowerInvariant(),
            assembly = assembly.Name.Name,
            assemblyVersion = assembly.Name.Version.ToString(),
            moduleCount = assembly.Modules.Count,
            typeCount = types.Length,
            methodCount = methods.Length,
            assemblyReferences = assembly.MainModule.AssemblyReferences.Select(value => value.Name)
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            moduleClasses = types.Where(type => Inherits(type, "Celeste.Mod.EverestModule"))
                .Select(type => type.FullName).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            settingsClasses = types.Where(type => Inherits(type, "Celeste.Mod.EverestModuleSettings"))
                .Select(type => type.FullName).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            saveDataClasses = types.Where(type => Inherits(type, "Celeste.Mod.EverestModuleSaveData"))
                .Select(type => type.FullName).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            sessionClasses = types.Where(type => Inherits(type, "Celeste.Mod.EverestModuleSession"))
                .Select(type => type.FullName).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            pinvokeMethods = methods.Where(method => method.HasPInvokeInfo).Select(method => new
                { method = method.FullName, module = method.PInvokeInfo.Module.Name, entryPoint = method.PInvokeInfo.EntryPoint })
                .OrderBy(value => value.method, StringComparer.Ordinal).ToArray(),
            customIds = customIds.Distinct().OrderBy(value => value.Kind, StringComparer.Ordinal)
                .ThenBy(value => value.Id, StringComparer.Ordinal).ThenBy(value => value.Type, StringComparer.Ordinal).ToArray(),
            configuredContexts,
            mechanisms = sites.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key,
                pair => new
                {
                    count = pair.Value.Count,
                    targets = pair.Value.Select(value => value.Target).Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    owners = pair.Value.Select(value => value.Owner).Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    sites = pair.Value.OrderBy(value => value.Owner, StringComparer.Ordinal)
                        .ThenBy(value => value.Offset).ToArray()
                }, StringComparer.Ordinal)
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static IEnumerable<TypeDefinition> Flatten(TypeDefinition type)
    {
        yield return type;
        foreach (TypeDefinition nested in type.NestedTypes.SelectMany(Flatten)) yield return nested;
    }

    private static bool Inherits(TypeDefinition type, string fullName)
    {
        // Audit packages are deliberately inspected without a runtime or dependency
        // resolver. Everest module/settings/save/session classes derive directly from
        // their marker base, so resolving arbitrary transitive bases would only make
        // this metadata-only census depend on private game assemblies.
        return type.BaseType?.FullName == fullName;
    }

    private static IEnumerable<string> AttributeStrings(CustomAttribute attribute)
    {
        foreach (CustomAttributeArgument argument in attribute.ConstructorArguments)
        {
            if (argument.Value is string text) yield return text;
            else if (argument.Value is CustomAttributeArgument[] array)
                foreach (CustomAttributeArgument item in array)
                    if (item.Value is string value) yield return value;
        }
    }

    private static string[] Categories() =>
    [
        "assemblyLoadCalls", "detourConfigReferences", "detourContextReferences",
        "directHookConstructors", "directIlHookConstructors", "dynamicDataCalls",
        "dynamicMethodCalls", "dynDataCalls", "emitDelegateCalls", "everestContentCalls",
        "fastReflectionCalls", "fileSystemWatcherCalls", "fmodCalls", "ilEventSubscriptions",
        "modInteropCalls", "onHookSubscriptions", "processCalls", "reflectionCalls",
        "reflectionEmitCalls"
    ];
}
