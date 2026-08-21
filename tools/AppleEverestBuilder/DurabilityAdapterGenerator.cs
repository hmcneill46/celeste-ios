using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;

namespace AppleEverestBuilder;

internal sealed record GeneratedDurabilityAdapter(string Module, string Field, string Schema);

internal static class DurabilityAdapterGenerator
{
    private const int MaximumGraphTypes = 256;
    private const int MaximumGraphDepth = 16;

    public static (string Source, IReadOnlyDictionary<string, GeneratedDurabilityAdapter> Adapters) Generate(
        IReadOnlyList<(ResolvedMod Mod, AppleStaticDeclaration Declaration)> modules)
    {
        StringBuilder fields = new();
        StringBuilder methods = new();
        Dictionary<string, GeneratedDurabilityAdapter> adapters = new(StringComparer.Ordinal);
        int index = 0;
        foreach ((ResolvedMod mod, AppleStaticDeclaration declaration) in modules)
        {
            if (mod.DeclaredAssemblyPath == null ||
                declaration.SaveDataType == null && declaration.SessionType == null)
                continue;
            string assemblyPath = Path.Combine(mod.Input.StagingRoot,
                mod.DeclaredAssemblyPath.Replace('/', Path.DirectorySeparatorChar));
            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyPath,
                new ReaderParameters { ReadSymbols = false });
            Emitter emitter = new(assembly.MainModule, mod.Metadata.Name, index++);
            string saveWrite = declaration.SaveDataType == null ? "null" : emitter.RootWriter(declaration.SaveDataType, "SaveData");
            string saveRead = declaration.SaveDataType == null ? "null" : emitter.RootReader(declaration.SaveDataType, "SaveData");
            string sessionWrite = declaration.SessionType == null ? "null" : emitter.RootWriter(declaration.SessionType, "Session");
            string sessionRead = declaration.SessionType == null ? "null" : emitter.RootReader(declaration.SessionType, "Session");
            string schema = Hashing.BytesSha256(Encoding.UTF8.GetBytes(emitter.Schema));
            string field = "Module" + emitter.Index.ToString("D3");
            fields.Append("    internal static readonly AppleEverestModuleDurabilityAdapter ").Append(field)
                .Append(" = new(\"").Append(schema).Append("\", ")
                .Append(saveWrite).Append(", ").Append(saveRead).Append(", ")
                .Append(sessionWrite).Append(", ").Append(sessionRead).AppendLine(");");
            methods.Append(emitter.Source);
            adapters.Add(mod.Metadata.Name, new GeneratedDurabilityAdapter(mod.Metadata.Name, field, schema));
        }
        string source = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Text.Json;\n\nnamespace Celeste.Mod;\n\n" +
                        "internal static class GeneratedAppleEverestModuleDurabilityAdapters\n{\n" +
                        fields + methods + "}\n";
        return (source, adapters);
    }

    private sealed class Emitter
    {
        private readonly ModuleDefinition module;
        private readonly string owner;
        private readonly Dictionary<string, TypeDefinition> localTypes;
        private readonly Dictionary<string, string> methodNames = new(StringComparer.Ordinal);
        private readonly HashSet<string> emitted = new(StringComparer.Ordinal);
        private readonly StringBuilder source = new();
        private readonly StringBuilder schema = new();
        private int nextMethod;
        private int nextValue;

        internal int Index { get; }
        internal string Source => source.ToString();
        internal string Schema => schema.ToString();

        internal Emitter(ModuleDefinition module, string owner, int index)
        {
            this.module = module;
            this.owner = owner;
            Index = index;
            localTypes = module.Types.SelectMany(AllTypes)
                .ToDictionary(type => Normalize(type.FullName), StringComparer.Ordinal);
            schema.Append("module:").Append(owner).Append('\n');
        }

        internal string RootWriter(string typeName, string kind)
        {
            TypeReference type = Find(typeName);
            Ensure(type, 0);
            string root = Method(type);
            string name = $"Serialize{kind}_{Index:D3}";
            source.Append("\n    private static byte[] ").Append(name)
                .Append("(EverestModule").Append(kind).AppendLine(" value) =>")
                .Append("        AppleEverestModuleYaml.Write(writer => ").Append(root)
                .Append("(writer, (global::").Append(Cs(type)).AppendLine(")value));");
            return name;
        }

        internal string RootReader(string typeName, string kind)
        {
            TypeReference type = Find(typeName);
            Ensure(type, 0);
            string root = Method(type);
            string name = $"Deserialize{kind}_{Index:D3}";
            source.Append("\n    private static EverestModule").Append(kind).Append(' ').Append(name)
                .AppendLine("(byte[] payload, int slot)")
                .AppendLine("    {")
                .AppendLine("        using JsonDocument document = AppleEverestModuleYaml.Parse(payload);")
                .Append("        global::").Append(Cs(type)).Append(" value = ").Append(root)
                .AppendLine("(document.RootElement);")
                .AppendLine("        if (value != null) value.Index = slot;")
                .AppendLine("        return value;")
                .AppendLine("    }");
            return name;
        }

        private void Ensure(TypeReference type, int depth)
        {
            if (depth > MaximumGraphDepth) Fail("property graph is too deep");
            string key = Key(type);
            if (Primitive(type) || Collection(type) || Nullable(type) || Vector(type) || Enum(type))
            {
                EnsureChildren(type, depth);
                return;
            }
            if (emitted.Contains(key)) return;
            if (emitted.Count >= MaximumGraphTypes) Fail("property graph contains too many object types");
            TypeDefinition definition = ResolveLocal(type);
            emitted.Add(key);
            List<PropertyDefinition> properties = Properties(definition);
            ConstructorPlan constructor = Constructor(definition, properties);
            foreach (PropertyDefinition property in properties) Ensure(property.PropertyType, depth + 1);
            string method = Method(type);
            schema.Append("type:").Append(Normalize(type.FullName)).Append(";ctor=")
                .Append(string.Join(',', constructor.Parameters.Select(value => value.Parameter.Name))).Append('\n');
            foreach (PropertyDefinition property in properties)
                schema.Append("property:").Append(property.Name).Append(':').Append(Key(property.PropertyType)).Append('\n');

            source.Append("\n    private static void ").Append(method).Append("(Utf8JsonWriter writer, global::")
                .Append(Cs(type)).AppendLine(" value)")
                .AppendLine("    {")
                .AppendLine("        if (value == null) { writer.WriteNullValue(); return; }")
                .AppendLine("        writer.WriteStartObject();");
            foreach (PropertyDefinition property in properties)
            {
                source.Append("        writer.WritePropertyName(\"").Append(Escape(property.Name)).AppendLine("\");");
                EmitWrite(source, property.PropertyType, "value." + property.Name, "        ");
            }
            source.AppendLine("        writer.WriteEndObject();")
                .AppendLine("    }");

            source.Append("\n    private static global::").Append(Cs(type)).Append(' ').Append(method)
                .AppendLine("(JsonElement element)")
                .AppendLine("    {")
                .AppendLine("        if (element.ValueKind == JsonValueKind.Null) return null;")
                .AppendLine("        AppleEverestModuleYaml.RequireObject(element);");
            foreach (PropertyDefinition property in properties)
                source.Append("        bool has_").Append(Safe(property.Name)).Append(" = AppleEverestModuleYaml.TryProperty(element, \"")
                    .Append(Escape(property.Name)).Append("\", out JsonElement p_").Append(Safe(property.Name)).AppendLine(");");
            source.Append("        global::").Append(Cs(type)).Append(" result = new global::").Append(Cs(type)).Append('(');
            for (int i = 0; i < constructor.Parameters.Count; i++)
            {
                if (i > 0) source.Append(", ");
                ConstructorParameter parameter = constructor.Parameters[i];
                source.Append("has_").Append(Safe(parameter.Property.Name)).Append(" ? ")
                    .Append(Read(parameter.Property.PropertyType, "p_" + Safe(parameter.Property.Name)))
                    .Append(" : ").Append(Default(parameter.Parameter));
            }
            source.AppendLine(");");
            foreach (PropertyDefinition property in properties.Where(property => !constructor.PropertyNames.Contains(property.Name)))
            {
                source.Append("        if (has_").Append(Safe(property.Name)).Append(") result.").Append(property.Name)
                    .Append(" = ").Append(Read(property.PropertyType, "p_" + Safe(property.Name))).AppendLine(";");
            }
            source.AppendLine("        return result;")
                .AppendLine("    }");
        }

        private void EnsureChildren(TypeReference type, int depth)
        {
            if (type is ArrayType array) Ensure(array.ElementType, depth + 1);
            else if (type is GenericInstanceType generic)
                foreach (TypeReference argument in generic.GenericArguments) Ensure(argument, depth + 1);
        }

        private void EmitWrite(StringBuilder output, TypeReference type, string value, string indent)
        {
            string full = Normalize(type.FullName);
            if (type is ArrayType array)
            {
                string itemName = "item_" + nextValue++;
                output.Append(indent).Append("if (").Append(value).AppendLine(" == null) writer.WriteNullValue(); else")
                    .Append(indent).AppendLine("{")
                    .Append(indent).AppendLine("    writer.WriteStartArray();")
                    .Append(indent).Append("    foreach (global::").Append(Cs(array.ElementType)).Append(' ').Append(itemName).Append(" in ").Append(value).AppendLine(")")
                    .Append(indent).AppendLine("    {");
                EmitWrite(output, array.ElementType, itemName, indent + "        ");
                output.Append(indent).AppendLine("    }").Append(indent).AppendLine("    writer.WriteEndArray();")
                    .Append(indent).AppendLine("}");
                return;
            }
            if (type is GenericInstanceType generic && IsList(generic))
            {
                TypeReference item = generic.GenericArguments[0];
                string itemName = "item_" + nextValue++;
                output.Append(indent).Append("if (").Append(value).AppendLine(" == null) writer.WriteNullValue(); else")
                    .Append(indent).AppendLine("{")
                    .Append(indent).AppendLine("    writer.WriteStartArray();")
                    .Append(indent).Append("    foreach (global::").Append(Cs(item)).Append(' ').Append(itemName).Append(" in ").Append(value).AppendLine(")")
                    .Append(indent).AppendLine("    {");
                EmitWrite(output, item, itemName, indent + "        ");
                output.Append(indent).AppendLine("    }").Append(indent).AppendLine("    writer.WriteEndArray();")
                    .Append(indent).AppendLine("}");
                return;
            }
            if (type is GenericInstanceType dictionary && IsDictionary(dictionary))
            {
                TypeReference item = dictionary.GenericArguments[1];
                string itemName = "item_" + nextValue++;
                output.Append(indent).Append("if (").Append(value).AppendLine(" == null) writer.WriteNullValue(); else")
                    .Append(indent).AppendLine("{")
                    .Append(indent).AppendLine("    writer.WriteStartObject();")
                    .Append(indent).Append("    foreach (global::System.Collections.Generic.KeyValuePair<string, global::")
                    .Append(Cs(item)).Append("> ").Append(itemName).Append(" in ").Append(value).AppendLine(".OrderBy(pair => pair.Key, StringComparer.Ordinal))")
                    .Append(indent).AppendLine("    {")
                    .Append(indent).Append("        writer.WritePropertyName(").Append(itemName).AppendLine(".Key);");
                EmitWrite(output, item, itemName + ".Value", indent + "        ");
                output.Append(indent).AppendLine("    }").Append(indent).AppendLine("    writer.WriteEndObject();")
                    .Append(indent).AppendLine("}");
                return;
            }
            if (type is GenericInstanceType nullable && IsNullable(nullable))
            {
                TypeReference item = nullable.GenericArguments[0];
                output.Append(indent).Append("if (!").Append(value).AppendLine(".HasValue) writer.WriteNullValue(); else")
                    .Append(indent).AppendLine("{");
                EmitWrite(output, item, value + ".Value", indent + "    ");
                output.Append(indent).AppendLine("}");
                return;
            }
            if (full == "System.String") output.Append(indent).Append("writer.WriteStringValue(").Append(value).AppendLine(");");
            else if (full == "System.Boolean") output.Append(indent).Append("writer.WriteBooleanValue(").Append(value).AppendLine(");");
            else if (full is "System.Single") output.Append(indent).Append("AppleEverestModuleYaml.WriteSingle(writer, ").Append(value).AppendLine(");");
            else if (full is "System.Double") output.Append(indent).Append("AppleEverestModuleYaml.WriteDouble(writer, ").Append(value).AppendLine(");");
            else if (full is "System.Decimal") output.Append(indent).Append("writer.WriteNumberValue(").Append(value).AppendLine(");");
            else if (Integer(type)) output.Append(indent).Append("writer.WriteNumberValue(").Append(value).AppendLine(");");
            else if (Enum(type)) output.Append(indent).Append("writer.WriteStringValue(").Append(value).AppendLine(".ToString());");
            else if (Vector(type))
            {
                output.Append(indent).AppendLine("writer.WriteStartObject();")
                    .Append(indent).AppendLine("writer.WritePropertyName(\"X\");")
                    .Append(indent).Append("AppleEverestModuleYaml.WriteSingle(writer, ").Append(value).AppendLine(".X);")
                    .Append(indent).AppendLine("writer.WritePropertyName(\"Y\");")
                    .Append(indent).Append("AppleEverestModuleYaml.WriteSingle(writer, ").Append(value).AppendLine(".Y);")
                    .Append(indent).AppendLine("writer.WriteEndObject();");
            }
            else output.Append(indent).Append(Method(type)).Append("(writer, ").Append(value).AppendLine(");");
        }

        private string Read(TypeReference type, string value)
        {
            string full = Normalize(type.FullName);
            if (type is ArrayType array)
            {
                string item = Read(array.ElementType, "item");
                return $"{value}.ValueKind == JsonValueKind.Null ? null : global::System.Linq.Enumerable.ToArray(global::System.Linq.Enumerable.Select({value}.EnumerateArray(), item => {item}))";
            }
            if (type is GenericInstanceType generic && IsList(generic))
            {
                string item = Read(generic.GenericArguments[0], "item");
                return $"{value}.ValueKind == JsonValueKind.Null ? null : global::System.Linq.Enumerable.ToList(global::System.Linq.Enumerable.Select({value}.EnumerateArray(), item => {item}))";
            }
            if (type is GenericInstanceType dictionary && IsDictionary(dictionary))
            {
                string read = Read(dictionary.GenericArguments[1], "item.Value");
                return $"{value}.ValueKind == JsonValueKind.Null ? null : global::System.Linq.Enumerable.ToDictionary({value}.EnumerateObject(), item => item.Name, item => {read}, StringComparer.Ordinal)";
            }
            if (type is GenericInstanceType nullable && IsNullable(nullable))
                return $"{value}.ValueKind == JsonValueKind.Null ? (global::{Cs(type)})null : {Read(nullable.GenericArguments[0], value)}";
            if (full == "System.String") return $"AppleEverestModuleYaml.String({value})";
            if (full == "System.Boolean") return $"AppleEverestModuleYaml.Boolean({value})";
            if (full == "System.Int32" || full == "System.Int16" || full == "System.SByte") return $"(global::{Cs(type)})AppleEverestModuleYaml.Int32({value})";
            if (full == "System.UInt32" || full == "System.UInt16" || full == "System.Byte") return $"(global::{Cs(type)})AppleEverestModuleYaml.UInt32({value})";
            if (full == "System.Int64") return $"AppleEverestModuleYaml.Int64({value})";
            if (full == "System.UInt64") return $"AppleEverestModuleYaml.UInt64({value})";
            if (full == "System.Single") return $"AppleEverestModuleYaml.Single({value})";
            if (full == "System.Double") return $"AppleEverestModuleYaml.Double({value})";
            if (full == "System.Decimal") return $"AppleEverestModuleYaml.Decimal({value})";
            if (Enum(type)) return EnumRead(type, value);
            if (Vector(type))
                return $"new global::Microsoft.Xna.Framework.Vector2(AppleEverestModuleYaml.Single({value}.GetProperty(\"X\")), AppleEverestModuleYaml.Single({value}.GetProperty(\"Y\")))";
            return Method(type) + "(" + value + ")";
        }

        private string EnumRead(TypeReference type, string value)
        {
            TypeDefinition definition = ResolveLocal(type);
            string expression = $"AppleEverestModuleYaml.String({value}) switch {{ ";
            foreach (FieldDefinition field in definition.Fields.Where(field => field.IsStatic && field.HasConstant))
                expression += $"\"{Escape(field.Name)}\" => global::{Cs(type)}.{field.Name}, ";
            return expression + $"_ => throw new global::System.IO.InvalidDataException(\"invalid {Escape(type.Name)} enum value\") }}";
        }

        private ConstructorPlan Constructor(TypeDefinition type, List<PropertyDefinition> properties)
        {
            MethodDefinition? parameterless = type.Methods.FirstOrDefault(method => method.IsConstructor && method.IsPublic &&
                !method.IsStatic && method.Parameters.Count == 0);
            if (parameterless != null) return new ConstructorPlan(parameterless, new List<ConstructorParameter>(), new HashSet<string>(StringComparer.Ordinal));
            foreach (MethodDefinition method in type.Methods.Where(method => method.IsConstructor && method.IsPublic && !method.IsStatic)
                         .OrderByDescending(method => method.Parameters.Count))
            {
                List<ConstructorParameter> parameters = new();
                bool valid = true;
                foreach (ParameterDefinition parameter in method.Parameters)
                {
                    PropertyDefinition? property = properties.FirstOrDefault(value =>
                        string.Equals(value.Name, parameter.Name, StringComparison.OrdinalIgnoreCase) &&
                        Key(value.PropertyType) == Key(parameter.ParameterType));
                    if (property == null) { valid = false; break; }
                    parameters.Add(new ConstructorParameter(parameter, property));
                }
                if (valid) return new ConstructorPlan(method, parameters,
                    parameters.Select(value => value.Property.Name).ToHashSet(StringComparer.Ordinal));
            }
            Fail("durable object has no supported public constructor: " + type.FullName);
            return null!;
        }

        private List<PropertyDefinition> Properties(TypeDefinition type)
        {
            List<PropertyDefinition> result = new();
            foreach (PropertyDefinition property in type.Properties.OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                if (property.Parameters.Count > 0 || property.GetMethod is not { IsPublic: true }) continue;
                bool ignored = property.CustomAttributes.Any(attribute =>
                    attribute.AttributeType.FullName == "YamlDotNet.Serialization.YamlIgnoreAttribute");
                if (ignored) continue;
                if (property.SetMethod is not { IsPublic: true })
                    Fail("durable public property is not writable and lacks YamlIgnore: " + type.FullName + "." + property.Name);
                result.Add(property);
            }
            if (result.Count > 128) Fail("durable object has too many properties: " + type.FullName);
            return result;
        }

        private TypeReference Find(string name)
        {
            if (!localTypes.TryGetValue(Normalize(name), out TypeDefinition? type))
                Fail("durability root type is outside its declared module assembly: " + name);
            return type!;
        }

        private TypeDefinition ResolveLocal(TypeReference type)
        {
            if (!localTypes.TryGetValue(Normalize(type.FullName), out TypeDefinition? definition))
                Fail("unsupported external durable object type: " + type.FullName);
            return definition!;
        }

        private string Method(TypeReference type)
        {
            string key = Key(type);
            if (!methodNames.TryGetValue(key, out string? method))
                methodNames.Add(key, method = $"Type_{Index:D3}_{nextMethod++:D3}");
            return method;
        }

        private static bool Primitive(TypeReference type) => Integer(type) || Normalize(type.FullName) is
            "System.String" or "System.Boolean" or "System.Single" or "System.Double" or "System.Decimal";
        private static bool Integer(TypeReference type) => Normalize(type.FullName) is
            "System.SByte" or "System.Byte" or "System.Int16" or "System.UInt16" or
            "System.Int32" or "System.UInt32" or "System.Int64" or "System.UInt64";
        private bool Enum(TypeReference type) => localTypes.TryGetValue(Normalize(type.FullName), out TypeDefinition? value) && value.IsEnum;
        private static bool Vector(TypeReference type) => Normalize(type.FullName) == "Microsoft.Xna.Framework.Vector2";
        private static bool Collection(TypeReference type) => type is ArrayType || type is GenericInstanceType generic &&
            (IsList(generic) || IsDictionary(generic));
        private static bool Nullable(TypeReference type) => type is GenericInstanceType generic && IsNullable(generic);
        private static bool IsList(GenericInstanceType type) => Normalize(type.ElementType.FullName) == "System.Collections.Generic.List`1" && type.GenericArguments.Count == 1;
        private static bool IsDictionary(GenericInstanceType type) => Normalize(type.ElementType.FullName) == "System.Collections.Generic.Dictionary`2" &&
            type.GenericArguments.Count == 2 && Normalize(type.GenericArguments[0].FullName) == "System.String";
        private static bool IsNullable(GenericInstanceType type) => Normalize(type.ElementType.FullName) == "System.Nullable`1" && type.GenericArguments.Count == 1;

        private static string Default(ParameterDefinition parameter)
        {
            if (parameter.HasConstant)
            {
                if (parameter.Constant == null) return "null";
                if (parameter.Constant is bool boolean) return boolean ? "true" : "false";
                if (parameter.Constant is string text) return "\"" + Escape(text) + "\"";
                return Convert.ToString(parameter.Constant, System.Globalization.CultureInfo.InvariantCulture) ?? "default";
            }
            return "default";
        }

        private static string Cs(TypeReference type)
        {
            if (type is ArrayType array) return Cs(array.ElementType) + "[]";
            if (type is GenericInstanceType generic)
            {
                string name = Normalize(generic.ElementType.FullName);
                int tick = name.IndexOf('`');
                if (tick >= 0) name = name[..tick];
                return name + "<" + string.Join(", ", generic.GenericArguments.Select(argument => "global::" + Cs(argument))) + ">";
            }
            // Generated type positions are deliberately emitted after a
            // global:: qualifier. Keep framework names fully qualified here;
            // C# aliases such as `string` are invalid after global::.
            return Normalize(type.FullName);
        }

        private static string Key(TypeReference type) => Normalize(type.FullName);
        private static string Normalize(string value) => value.Replace('/', '.');
        private static string Safe(string value) => new(value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        private void Fail(string message) => throw new InvalidDataException($"{owner} {message}");
        private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition type)
        {
            yield return type;
            foreach (TypeDefinition nested in type.NestedTypes.SelectMany(AllTypes)) yield return nested;
        }
        private sealed record ConstructorParameter(ParameterDefinition Parameter, PropertyDefinition Property);
        private sealed record ConstructorPlan(MethodDefinition Method, List<ConstructorParameter> Parameters,
            HashSet<string> PropertyNames);
    }
}
