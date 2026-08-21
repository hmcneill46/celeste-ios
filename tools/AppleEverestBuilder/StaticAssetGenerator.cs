using System.Globalization;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using YamlDotNet.RepresentationModel;

namespace AppleEverestBuilder;

internal sealed record StaticAssetGeneration(string Source, int TypeCount, int FactoryCount);

internal static class StaticAssetGenerator
{
    private sealed record Requirement(string ClosedType, string ElementType,
        IReadOnlyDictionary<string, PropertyDefinition> Properties);

    private sealed record Factory(string Owner, string PathVirtual, string ClosedType, string Expression);

    internal static StaticAssetGeneration Generate(
        IReadOnlyList<(ResolvedMod Mod, AppleStaticDeclaration Declaration)> modules,
        IReadOnlyList<ContentMountRecord> staged,
        string contentRoot)
    {
        Dictionary<string, Requirement> requirements = new(StringComparer.Ordinal);
        foreach ((ResolvedMod mod, _) in modules)
        {
            if (mod.DeclaredAssemblyPath == null) continue;
            string path = Path.Combine(mod.Input.StagingRoot,
                mod.DeclaredAssemblyPath.Replace('/', Path.DirectorySeparatorChar));
            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path,
                new ReaderParameters { ReadSymbols = false });
            foreach (MethodDefinition method in assembly.MainModule.Types.SelectMany(AllTypes)
                         .SelectMany(type => type.Methods).Where(method => method.HasBody))
            foreach (Instruction instruction in method.Body.Instructions)
            {
                if (instruction.Operand is not GenericInstanceMethod generic ||
                    generic.ElementMethod.DeclaringType.FullName != "Celeste.Mod.ModAsset" ||
                    generic.ElementMethod.Name is not ("Deserialize" or "TryDeserialize") ||
                    generic.GenericArguments.Count != 1) continue;
                TypeReference closed = generic.GenericArguments[0];
                if (closed is not GenericInstanceType list ||
                    list.ElementType.FullName != "System.Collections.Generic.List`1" ||
                    list.GenericArguments.Count != 1)
                    throw new InvalidDataException($"DEFERRED_STATIC_ASSET_TYPE:{mod.Metadata.Name}:{closed.FullName}");
                TypeDefinition element;
                try { element = list.GenericArguments[0].Resolve(); }
                catch (AssemblyResolutionException exception)
                {
                    throw new InvalidDataException($"static asset DTO is unresolved: {closed.FullName}", exception);
                }
                if (element == null || element.IsAbstract || element.HasGenericParameters ||
                    !element.Methods.Any(candidate => candidate.IsConstructor && !candidate.IsStatic &&
                        candidate.IsPublic && candidate.Parameters.Count == 0))
                    throw new InvalidDataException($"static asset DTO is not constructible: {closed.FullName}");
                Dictionary<string, PropertyDefinition> properties = element.Properties
                    .Where(property => property.SetMethod is { IsPublic: true } &&
                                       !property.CustomAttributes.Any(attribute => attribute.AttributeType.FullName ==
                                           "YamlDotNet.Serialization.YamlIgnoreAttribute"))
                    .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
                string closedType = Cs(closed);
                requirements.TryAdd(closedType, new Requirement(closedType, Cs(element), properties));
            }
        }

        List<Factory> factories = [];
        ContentMountRecord[] yamlAssets = staged.Where(asset =>
                asset.SourcePath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                asset.SourcePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            .GroupBy(asset => asset.Owner + "\0" + VirtualPath(asset.SourcePath), StringComparer.Ordinal)
            .Select(group => group.OrderBy(asset => asset.Order).Last())
            .OrderBy(asset => asset.Owner, StringComparer.Ordinal)
            .ThenBy(asset => asset.SourcePath, StringComparer.Ordinal)
            .ToArray();
        foreach (Requirement requirement in requirements.Values.OrderBy(value => value.ClosedType, StringComparer.Ordinal))
        foreach (ContentMountRecord asset in yamlAssets)
        {
            string path = Path.Combine(contentRoot,
                asset.LogicalPath.Replace('/', Path.DirectorySeparatorChar));
            if (TryFactory(path, requirement, out string expression))
                factories.Add(new Factory(asset.Owner, VirtualPath(asset.SourcePath),
                    requirement.ClosedType, expression));
        }
        foreach (Requirement requirement in requirements.Values)
            if (!factories.Any(factory => factory.ClosedType == requirement.ClosedType))
                throw new InvalidDataException($"STATIC_ASSET_DESERIALIZER_UNBOUND:{requirement.ClosedType}");

        StringBuilder source = new("// Generated closed typed static-asset factories. No runtime reflection or YamlDotNet.\n\nnamespace Celeste.Mod;\n\ninternal static class GeneratedAppleEverestStaticAssets\n{\n");
        source.Append("    internal const int FactoryCount = ").Append(factories.Count).AppendLine(";");
        source.AppendLine("    internal static bool TryDeserialize<T>(ModAsset asset, out T value)")
            .AppendLine("    {")
            .AppendLine("        if (asset?.Source != null)")
            .AppendLine("        {");
        foreach (Requirement requirement in requirements.Values.OrderBy(value => value.ClosedType, StringComparer.Ordinal))
        {
            source.Append("            if (typeof(T) == typeof(").Append(requirement.ClosedType).AppendLine("))")
                .AppendLine("            {")
                .AppendLine("                object result = (asset.Source.Name, asset.PathVirtual) switch")
                .AppendLine("                {");
            foreach (Factory factory in factories.Where(value => value.ClosedType == requirement.ClosedType)
                         .OrderBy(value => value.Owner, StringComparer.Ordinal)
                         .ThenBy(value => value.PathVirtual, StringComparer.Ordinal))
                source.Append("                    (\"").Append(Escape(factory.Owner)).Append("\", \"")
                    .Append(Escape(factory.PathVirtual)).Append("\") => ").Append(factory.Expression).AppendLine(",");
            source.AppendLine("                    _ => null")
                .AppendLine("                };")
                .AppendLine("                if (result != null)")
                .AppendLine("                {")
                .AppendLine("                    value = (T)result;")
                .AppendLine("                    return true;")
                .AppendLine("                }")
                .AppendLine("            }");
        }
        source.AppendLine("        }")
            .AppendLine("        value = default;")
            .AppendLine("        return false;")
            .AppendLine("    }")
            .AppendLine("}");
        return new StaticAssetGeneration(source.ToString(), requirements.Count, factories.Count);
    }

    internal static string VirtualPath(string sourcePath)
    {
        string value = sourcePath.Replace('\\', '/');
        if (value.StartsWith("Content/", StringComparison.Ordinal)) value = value["Content/".Length..];
        string extension = Path.GetExtension(value);
        return extension.Length == 0 ? value : value[..^extension.Length];
    }

    private static bool TryFactory(string path, Requirement requirement, out string expression)
    {
        YamlStream yaml = new();
        using (StreamReader reader = new(path, new UTF8Encoding(false, true))) yaml.Load(reader);
        if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlSequenceNode sequence)
        {
            expression = "";
            return false;
        }
        List<string> items = [];
        foreach (YamlNode node in sequence.Children)
        {
            if (node is not YamlMappingNode mapping)
            {
                expression = "";
                return false;
            }
            List<string> assignments = [];
            foreach ((YamlNode keyNode, YamlNode valueNode) in mapping.Children)
            {
                if (keyNode is not YamlScalarNode { Value: { } key } ||
                    !requirement.Properties.TryGetValue(key, out PropertyDefinition? property) ||
                    property == null || valueNode is not YamlScalarNode scalar ||
                    !TryScalar(property.PropertyType, scalar.Value, out string value))
                {
                    expression = "";
                    return false;
                }
                assignments.Add(property.Name + " = " + value);
            }
            items.Add("new " + requirement.ElementType + " { " + string.Join(", ", assignments) + " }");
        }
        expression = "new " + requirement.ClosedType + " { " + string.Join(", ", items) + " }";
        return true;
    }

    private static bool TryScalar(TypeReference type, string? raw, out string value)
    {
        string text = raw ?? "";
        switch (type.FullName)
        {
            case "System.String":
                value = raw == null ? "null" : "\"" + Escape(text) + "\"";
                return true;
            case "System.Boolean" when bool.TryParse(text, out bool boolean):
                value = boolean ? "true" : "false";
                return true;
            case "System.Int32" when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer):
                value = integer.ToString(CultureInfo.InvariantCulture);
                return true;
            case "System.Single" when float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float single) &&
                                      !float.IsNaN(single) && !float.IsInfinity(single):
                value = single.ToString("R", CultureInfo.InvariantCulture) + "f";
                return true;
            case "System.Double" when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) &&
                                      !double.IsNaN(number) && !double.IsInfinity(number):
                value = number.ToString("R", CultureInfo.InvariantCulture) + "d";
                return true;
            default:
                value = "";
                return false;
        }
    }

    private static string Cs(TypeReference type)
    {
        if (type is GenericInstanceType generic)
        {
            string element = generic.ElementType.FullName;
            int tick = element.IndexOf('`');
            if (tick >= 0) element = element[..tick];
            return "global::" + element.Replace('/', '.') + "<" +
                   string.Join(", ", generic.GenericArguments.Select(Cs)) + ">";
        }
        return type.FullName switch
        {
            "System.String" => "string",
            "System.Boolean" => "bool",
            "System.Int32" => "int",
            "System.Single" => "float",
            "System.Double" => "double",
            _ => "global::" + type.FullName.Replace('/', '.')
        };
    }

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition type)
    {
        yield return type;
        foreach (TypeDefinition nested in type.NestedTypes)
        foreach (TypeDefinition child in AllTypes(nested)) yield return child;
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);
}
