using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text;
using System.Text.Json;

namespace AppleEverestBuilder;

// Desktop comparison only. Keep the original package IL for the selected
// masks and glow controller, including their real desktop hooks/reflection.
// No Apple semantic implementation is used as its own reference.
internal static class DesktopReferenceProjection
{
    internal static void Write(string source, string game, string output, string evidence)
    {
        using ModuleDefinition module = ModuleDefinition.ReadModule(source);
        using ModuleDefinition engine = ModuleDefinition.ReadModule(game);
        TypeDefinition[] retained = module.Types.Where(t =>
            t.Namespace == "Celeste.Mod.StrawberryJam2021.StylegroundMasks" ||
            t.FullName is "Celeste.Mod.StrawberryJam2021.Entities.GlowController" or
                "Celeste.Mod.StrawberryJam2021.Entities.StrawberryJamJar" or
                "Celeste.Mod.StrawberryJam2021.StrawberryJam2021SaveData").ToArray();
        if (retained.Length != 11) throw new InvalidDataException("reference selected desktop type census changed");
        TypeDefinition entry = module.Types.Single(t => t.FullName == "Celeste.Mod.StrawberryJam2021.StrawberryJam2021Module");
        var before = Methods(retained);
        foreach (TypeDefinition type in module.Types.ToArray())
            if (type.Name != "<Module>" && !retained.Contains(type) && type != entry) module.Types.Remove(type);

        // Keep the jar's original typed SaveData/SpriteBank accessors and
        // module identity. Replace only broad module initialization with its
        // selected hooks and bounded sprite-bank initialization.
        foreach (MethodDefinition method in entry.Methods.ToArray())
            if (method.Name is not (".ctor" or "get_SaveDataType" or "get_SaveData" or "get_SpriteBank" or "LoadContent"))
                entry.Methods.Remove(method);
        foreach (PropertyDefinition property in entry.Properties.ToArray())
            if (property.Name is not ("SaveDataType" or "SaveData" or "SpriteBank")) entry.Properties.Remove(property);
        foreach (FieldDefinition field in entry.Fields.ToArray())
            if (field.Name is not ("Instance" or "_CustomEntitySpriteBank")) entry.Fields.Remove(field);
        entry.NestedTypes.Clear();
        MethodDefinition content = entry.Methods.Single(m => m.Name == "LoadContent");
        int end = content.Body.Instructions.ToList().FindIndex(i => i.OpCode == OpCodes.Stfld &&
            i.Operand is FieldReference field && field.Name == "_CustomEntitySpriteBank");
        if (end < 0) throw new InvalidDataException("original module sprite-bank initializer absent");
        while (content.Body.Instructions.Count > end + 1) content.Body.Instructions.RemoveAt(end + 1);
        foreach (Instruction instruction in content.Body.Instructions)
            if (instruction.Operand is string path && path == "Graphics/StrawberryJam2021/CustomEntitySprites.xml")
                instruction.Operand = "Graphics/AppleEverestStage25KJ/JarSprites.xml";
        content.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        content.Body.ExceptionHandlers.Clear();
        content.Body.Variables.Clear();
        foreach (string phase in new[] { "Load", "Unload" })
        {
            MethodDefinition method = new(phase, MethodAttributes.Public | MethodAttributes.Virtual |
                MethodAttributes.HideBySig, module.TypeSystem.Void);
            entry.Methods.Add(method);
            foreach (string typeName in new[] { "MaskHooks", "GlowController" })
            {
                MethodDefinition target = retained.Single(t => t.Name == typeName).Methods
                    .Single(m => m.Name == phase && m.IsStatic && m.Parameters.Count == 0);
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, target));
            }
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        module.Write(output);
        using ModuleDefinition reread = ModuleDefinition.ReadModule(output);
        var after = Methods(reread.Types.Where(t => retained.Any(r => r.FullName == t.FullName)));
        if (before.Count != after.Count || before.Any(p => !after.TryGetValue(p.Key, out string? hash) || hash != p.Value))
            throw new InvalidDataException("selected original reference method bodies changed");
        File.WriteAllText(evidence, JsonSerializer.Serialize(new {
            schemaVersion = 1, authority = "EXACT_PACKAGE_SELECTED_DESKTOP_IL",
            sourceDllSha256 = Hashing.FileSha256(source), outputDllSha256 = Hashing.FileSha256(output),
            selectedTypes = retained.Select(t => t.FullName).Order().ToArray(),
            originalMethodBodies = before, originalMethodBodiesUnchanged = true,
            generatedGlue = "Selected module members; MaskHooks.Load/Unload; GlowController.Load/Unload; bounded original jamJar_beginner sprite-bank initializer",
            wholeSjModuleLoaded = false, appleLoweringsUsed = false
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static Dictionary<string, string> Methods(IEnumerable<TypeDefinition> types)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        void Visit(TypeDefinition type)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                string body = method.HasBody ? string.Join("\n", method.Body.Instructions.Select(i => i.ToString())) +
                    "\nlocals=" + string.Join(",", method.Body.Variables.Select(v => v.VariableType.FullName)) +
                    "\ninit=" + method.Body.InitLocals + "\nstack=" + method.Body.MaxStackSize +
                    "\nhandlers=" + string.Join(";", method.Body.ExceptionHandlers.Select(h =>
                        $"{h.HandlerType}:{h.TryStart?.Offset}:{h.TryEnd?.Offset}:{h.HandlerStart?.Offset}:{h.HandlerEnd?.Offset}:{h.FilterStart?.Offset}:{h.CatchType?.FullName}")) : "no-body";
                result.Add(method.FullName, Hashing.BytesSha256(Encoding.UTF8.GetBytes(body)));
            }
            foreach (TypeDefinition child in type.NestedTypes) Visit(child);
        }
        foreach (TypeDefinition type in types) Visit(type);
        return result;
    }
}
