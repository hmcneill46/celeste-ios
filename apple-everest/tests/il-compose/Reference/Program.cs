using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

if (args.Length != 4 || args[0] != "--sequence")
    throw new ArgumentException("usage: Reference --sequence A,B input.dll output-directory");

string[] sequence = args[1].Length == 0 ? [] : args[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
string input = Path.GetFullPath(args[2]);
string output = Path.GetFullPath(args[3]);
Directory.CreateDirectory(output);

using ModuleDefinition module = ModuleDefinition.ReadModule(input);
MethodDefinition target = module.Types.Single(type => type.FullName == "AppleEverest.IlCompose.ComposeTarget")
    .Methods.Single(method => method.Name == "Compose");
string semantic = target.FullName;
List<object> steps = [];
foreach (string item in sequence)
{
    string beforeText = Normalize(target, semantic);
    string method = item switch
    {
        "A" => "AddThree",
        "B" => "MultiplyFive",
        "C" => "SubtractSeven",
        "L" => "SingletonAddEleven",
        "N" => "NoOp",
        _ => throw new InvalidDataException("unknown manipulator " + item)
    };
    MethodInfo info = typeof(AppleEverest.IlCompose.SequenceManipulators).GetMethod(method,
        BindingFlags.Public | BindingFlags.Static)!;
    ILContext.Manipulator manipulator = (ILContext.Manipulator)Delegate.CreateDelegate(
        typeof(ILContext.Manipulator), info);
    using (ILContext context = new(target)) context.Invoke(manipulator);
    string afterText = Normalize(target, semantic);
    string diff = Diff(beforeText, afterText);
    steps.Add(new
    {
        id = item,
        beforeSha256 = Sha256(beforeText),
        afterSha256 = Sha256(afterText),
        diffSha256 = Sha256(diff),
        beforeNormalizedIl = beforeText,
        afterNormalizedIl = afterText,
        normalizedDiff = diff
    });
}

string assembly = Path.Combine(output, "AppleEverestIlComposeTarget.dll");
module.Write(assembly);
File.WriteAllText(Path.Combine(output, "reference.json"), JsonSerializer.Serialize(new
{
    sequence,
    baselineSha256 = steps.Count == 0 ? Sha256(Normalize(target, semantic)) :
        (string)steps[0].GetType().GetProperty("beforeSha256")!.GetValue(steps[0])!,
    finalSha256 = Sha256(Normalize(target, semantic)),
    steps
}, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));

static string Normalize(MethodDefinition method, string semanticIdentity)
{
    StringBuilder text = new();
    text.AppendLine("method " + semanticIdentity);
    text.AppendLine("initlocals " + method.Body.InitLocals.ToString().ToLowerInvariant());
    for (int index = 0; index < method.Body.Variables.Count; index++)
        text.AppendLine($"local V_{index} {TypeIdentity(method.Body.Variables[index].VariableType)}");
    Dictionary<Instruction, int> positions = method.Body.Instructions.Select((instruction, index) =>
        (instruction, index)).ToDictionary(item => item.instruction, item => item.index);
    for (int index = 0; index < method.Body.Instructions.Count; index++)
    {
        Instruction instruction = method.Body.Instructions[index];
        text.Append("IL_").Append(index.ToString("D4")).Append(' ').Append(instruction.OpCode.Code);
        string operand = OperandIdentity(instruction.Operand, positions);
        if (operand.Length != 0) text.Append(' ').Append(operand);
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

static string OperandIdentity(object? operand, IReadOnlyDictionary<Instruction, int> positions) => operand switch
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
    _ => operand.GetType().FullName + ":" + Convert.ToString(operand,
        System.Globalization.CultureInfo.InvariantCulture)
};

static string Label(Instruction? instruction, IReadOnlyDictionary<Instruction, int> positions) =>
    instruction == null ? "-" : positions.TryGetValue(instruction, out int index) ? "IL_" + index.ToString("D4") : "INVALID";
static string TypeIdentity(TypeReference type) => type.FullName + "@" + Scope(type);
static string Scope(TypeReference type) => type.Scope?.Name ?? type.Module?.Assembly?.Name?.Name ?? "?";

static string Diff(string before, string after)
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

static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
    .ToLowerInvariant();
