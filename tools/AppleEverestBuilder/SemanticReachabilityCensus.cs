using System.Text;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AppleEverestBuilder;

// Host-only evidence. Read metadata/IL without activating the distributed
// assembly; target products never receive this analyser or its input DLLs.
internal static class SemanticReachabilityCensus
{
    internal static void Write(string dllPath, string outputPath)
    {
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(dllPath,
            new ReaderParameters { ReadSymbols = false });
        object report = new
        {
            schemaVersion = 1,
            dllSha256 = Hashing.FileSha256(dllPath),
            assembly = assembly.Name.Name,
            types = assembly.Modules.SelectMany(module => module.Types).SelectMany(Flatten)
                .OrderBy(type => type.FullName, StringComparer.Ordinal).Select(type => new
                {
                    name = type.FullName,
                    baseType = type.BaseType?.FullName,
                    fields = type.Fields.OrderBy(field => field.FullName, StringComparer.Ordinal).Select(field => new
                    {
                        name = field.FullName, isStatic = field.IsStatic,
                        isLiteral = field.IsLiteral, constant = field.HasConstant ? field.Constant : null
                    }).ToArray(),
                    properties = type.Properties.OrderBy(property => property.FullName, StringComparer.Ordinal)
                        .Select(property => new { name = property.FullName,
                            getter = property.GetMethod?.FullName, setter = property.SetMethod?.FullName }).ToArray(),
                    methods = type.Methods.OrderBy(method => method.FullName, StringComparer.Ordinal).Select(method => new
                    {
                        name = method.FullName, isStatic = method.IsStatic, isVirtual = method.IsVirtual,
                        bodySha256 = method.HasBody ? BodyHash(method) : null,
                        calls = method.HasBody ? method.Body.Instructions
                            .Where(instruction => instruction.Operand is MethodReference)
                            .Select(instruction => new { offset = instruction.Offset,
                                opcode = instruction.OpCode.Name,
                                target = ((MethodReference)instruction.Operand).FullName }).ToArray() : [],
                        fields = method.HasBody ? method.Body.Instructions
                            .Where(instruction => instruction.Operand is FieldReference)
                            .Select(instruction => new { offset = instruction.Offset,
                                opcode = instruction.OpCode.Name,
                                target = ((FieldReference)instruction.Operand).FullName }).ToArray() : [],
                        strings = method.HasBody ? method.Body.Instructions
                            .Where(instruction => instruction.OpCode == OpCodes.Ldstr)
                            .Select(instruction => (string)instruction.Operand).Distinct(StringComparer.Ordinal)
                            .OrderBy(value => value, StringComparer.Ordinal).ToArray() : []
                    }).ToArray()
                }).ToArray()
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static IEnumerable<TypeDefinition> Flatten(TypeDefinition type)
    {
        yield return type;
        foreach (TypeDefinition nested in type.NestedTypes)
            foreach (TypeDefinition value in Flatten(nested)) yield return value;
    }

    private static string BodyHash(MethodDefinition method)
    {
        string Operand(object? value) => value switch
        {
            null => "",
            Instruction target => target.Offset.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Instruction[] targets => string.Join(",", targets.Select(target => Operand(target))),
            MemberReference member => member.FullName,
            VariableDefinition variable => "local:" + variable.Index + ":" + variable.VariableType.FullName,
            ParameterDefinition parameter => "arg:" + parameter.Index + ":" + parameter.ParameterType.FullName,
            IFormattable number => number.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
        StringBuilder body = new();
        body.AppendLine(method.FullName);
        foreach (VariableDefinition variable in method.Body.Variables)
            body.AppendLine(Operand(variable));
        foreach (Instruction instruction in method.Body.Instructions)
            body.Append(instruction.Offset).Append(':').Append(instruction.OpCode.Name)
                .Append(':').AppendLine(Operand(instruction.Operand));
        foreach (ExceptionHandler handler in method.Body.ExceptionHandlers)
            body.Append(handler.HandlerType).Append(':').Append(Operand(handler.TryStart))
                .Append(':').Append(Operand(handler.TryEnd)).Append(':').Append(Operand(handler.HandlerStart))
                .Append(':').Append(Operand(handler.HandlerEnd)).Append(':').Append(Operand(handler.FilterStart))
                .Append(':').AppendLine(handler.CatchType?.FullName ?? "");
        return Hashing.BytesSha256(Encoding.UTF8.GetBytes(body.ToString().Replace("\r\n", "\n")));
    }
}
