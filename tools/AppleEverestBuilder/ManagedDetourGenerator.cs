using System.Text;
using System.Text.RegularExpressions;

namespace AppleEverestBuilder;

internal static class ManagedDetourGenerator
{
    public static string DispatcherSource(IReadOnlyList<ManagedDetourTarget> targets)
    {
        StringBuilder source = new("// Generated from the reviewed managed-detour target catalog.\n// Target bodies are rewritten once on the Mac; device operations change only typed registration data.\n\n");
        foreach (IGrouping<string, ManagedDetourTarget> namespaceGroup in targets
                     .GroupBy(target => target.HookNamespace).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            source.Append("namespace ").Append(namespaceGroup.Key).AppendLine("\n{");
            foreach (IGrouping<string, ManagedDetourTarget> typeGroup in namespaceGroup
                         .GroupBy(target => target.HookType).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                source.Append("public static class ").Append(typeGroup.Key).AppendLine("\n{");
                foreach (ManagedDetourTarget target in typeGroup.OrderBy(value => value.EventName, StringComparer.Ordinal))
                    EmitTarget(source, target);
                source.AppendLine("}\n");
            }
            source.AppendLine("}\n");
        }

        source.AppendLine("namespace Celeste.Mod\n{\ninternal static class GeneratedAppleEverestManagedDetourRegistry\n{");
        source.AppendLine("    internal static void RemoveOwner(string owner)\n    {");
        foreach (ManagedDetourTarget target in targets.OrderBy(target => target.Id, StringComparer.Ordinal))
            source.Append("        global::").Append(target.HookNamespace).Append('.').Append(target.HookType)
                .Append(".RemoveOwner_").Append(target.EventName).AppendLine("(owner);");
        source.AppendLine("    }\n}\n}");
        return source.ToString();
    }

    public static string DirectRegistrySource(
        IReadOnlyList<DirectManagedHookPlan> plans,
        IReadOnlyDictionary<string, ManagedDetourTarget> targets)
    {
        StringBuilder source = new("using System;\n\nnamespace Celeste.Mod\n{\ninternal static class GeneratedAppleEverestDirectHookRegistry\n{\n");
        source.AppendLine("    internal static IAppleEverestManagedHookRegistration CreateByPlan(string planId, bool applyByDefault)\n    {");
        foreach (DirectManagedHookPlan plan in plans.OrderBy(plan => plan.PlanId, StringComparer.Ordinal))
        {
            ManagedDetourTarget target = targets[plan.TargetId];
            ValidateDirectShape(plan, target);
            source.Append("        if (planId == \"").Append(Escape(plan.PlanId)).AppendLine("\")");
            source.AppendLine("        {");
            EmitAdapter(source, plan, target, "            ");
            source.Append("            return global::").Append(target.HookNamespace).Append('.').Append(target.HookType)
                .Append(".RegisterDirect_").Append(target.EventName).Append("(adapter, applyByDefault, ")
                .Append(plan.StaticDispatcherOrdinal?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")
                .AppendLine(");\n        }");
        }
        source.AppendLine("        throw new NotSupportedException($\"direct managed Hook plan was not statically authorized: {planId}\");\n    }");
        source.AppendLine("}\n}");
        return source.ToString();
    }

    private static void EmitAdapter(StringBuilder source, DirectManagedHookPlan plan, ManagedDetourTarget target, string indent)
    {
        source.Append(indent).Append("global::").Append(target.HookNamespace).Append('.').Append(target.HookType).Append('.')
            .Append(target.HookDelegate).Append(" adapter = (");
        List<string> invocationNames = InvocationNames(target);
        source.Append("orig");
        foreach (string name in invocationNames) source.Append(", ").Append(name);
        source.AppendLine(") =>");
        source.Append(indent).AppendLine("{");
        source.Append(indent).Append("    global::Celeste.Mod.AppleEverestStaticRuntime.RecordDirectHookInvocation(\"")
            .Append(Escape(plan.PlanId)).AppendLine("\");");
        source.Append(indent).Append("    return ");
        string detourReceiver = plan.DetourIsStatic
            ? "global::" + plan.DetourType
            : "global::Celeste.Mod.AppleEverestStaticRuntime.GetModule<global::" + plan.DetourType + ">(\"" + Escape(plan.Owner) + "\")";
        source.Append(detourReceiver).Append('.').Append(plan.DetourMethod).Append('(');
        source.Append('(').Append(string.Join(", ", invocationNames.Select((name, index) => "next" + index))).Append(") => orig(")
            .Append(string.Join(", ", invocationNames.Select((_, index) => "next" + index))).Append(')');
        foreach (string name in invocationNames) source.Append(", ").Append(name);
        source.AppendLine(");");
        source.Append(indent).AppendLine("};");
    }

    public static void RewriteTargets(string managedRoot, IReadOnlyList<ManagedDetourTarget> targets)
    {
        foreach (ManagedDetourTarget target in targets.OrderBy(target => target.Id, StringComparer.Ordinal))
        {
            string path = Path.Combine(managedRoot, target.SourceFile.Replace('/', Path.DirectorySeparatorChar));
            string text = File.ReadAllText(path);
            if (target.SourceKind == "property-getter")
            {
                text = RewritePropertyGetter(text, target);
                File.WriteAllText(path, text, new UTF8Encoding(false));
                continue;
            }
            string needle = "\t" + target.SourceDeclaration + "\n\t{";
            int first = text.IndexOf(needle, StringComparison.Ordinal);
            if (first < 0 || first != text.LastIndexOf(needle, StringComparison.Ordinal))
                throw new InvalidDataException($"managed-detour target declaration drifted or is ambiguous: {target.Id}");
            if (target.EventName.Equals("ctor", StringComparison.Ordinal) ||
                target.EventName.StartsWith("ctor_", StringComparison.Ordinal))
            {
                int constructorOpen = first + needle.Length - 1;
                int constructorClose = MatchingBrace(text, constructorOpen);
                text = RelaxMovedConstructorReadonlyFields(
                    text, text[(constructorOpen + 1)..constructorClose], target.Id);
                first = text.IndexOf(needle, StringComparison.Ordinal);
            }
            string wrapper = "\t" + target.SourceDeclaration + "\n\t{\n\t\t";
            if (target.ReturnType != "void") wrapper += "return ";
            wrapper += "global::" + target.HookNamespace + "." + target.HookType + ".Invoke_" + target.EventName + "(";
            List<string> arguments = WrapperInvocationNames(target);
            wrapper += string.Join(", ", arguments);
            if (arguments.Count > 0) wrapper += ", ";
            if (target.IsStatic)
            {
                wrapper += target.OriginalAlias;
            }
            else
            {
                List<string> delegateNames = ["appleSelf"];
                delegateNames.AddRange(target.Parameters.Select((_, index) => "appleArg" + index));
                wrapper += "(" + string.Join(", ", delegateNames) + ") => appleSelf." + target.OriginalAlias +
                    "(" + string.Join(", ", delegateNames.Skip(1)) + ")";
            }
            wrapper += ");\n\t}\n\n\t" + target.OriginalDeclaration + "\n\t{";
            text = text[..first] + wrapper + text[(first + needle.Length)..];
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }

    private static string RelaxMovedConstructorReadonlyFields(string text, string constructorBody, string targetId)
    {
        string[] assignedNames = Regex.Matches(constructorBody,
                @"(?m)^\s*(?:this\.)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=")
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        foreach (string name in assignedNames)
        {
            Regex declaration = new(
                @"(?m)^(?<prefix>\t(?:(?:public|private|protected|internal|static|unsafe|new|volatile)\s+)*)" +
                @"readonly\s+(?<suffix>[^;\r\n]*\b" + Regex.Escape(name) + @"\b[^;\r\n]*;)$");
            MatchCollection matches = declaration.Matches(text);
            if (matches.Count > 1)
                throw new InvalidDataException(
                    $"managed-detour constructor readonly field is ambiguous: {targetId}:{name}");
            if (matches.Count == 1)
                text = declaration.Replace(text, "${prefix}${suffix}", 1);
        }
        return text;
    }

    private static string RewritePropertyGetter(string text, ManagedDetourTarget target)
    {
        string declaration = "\t" + target.SourceDeclaration + "\n\t{";
        int first = text.IndexOf(declaration, StringComparison.Ordinal);
        if (first < 0 || first != text.LastIndexOf(declaration, StringComparison.Ordinal))
            throw new InvalidDataException($"managed-detour property target drifted or is ambiguous: {target.Id}");
        int propertyOpen = first + declaration.Length - 1;
        int propertyClose = MatchingBrace(text, propertyOpen);
        string getterNeedle = "\n\t\tget\n\t\t{";
        int getter = text.IndexOf(getterNeedle, propertyOpen, propertyClose - propertyOpen,
            StringComparison.Ordinal);
        if (getter < 0)
            throw new InvalidDataException($"managed-detour property getter is missing: {target.Id}");
        int getterOpen = getter + getterNeedle.Length - 1;
        int getterClose = MatchingBrace(text, getterOpen);
        string body = text[(getterOpen + 1)..getterClose];
        string wrapper = "\t" + target.SourceDeclaration + "\n\t{\n\t\tget\n\t\t{\n\t\t\treturn global::" +
            target.HookNamespace + "." + target.HookType + ".Invoke_" + target.EventName +
            "(this, appleSelf => appleSelf." + target.OriginalAlias + "());\n\t\t}\n\t}\n\n\t" +
            target.OriginalDeclaration + "\n\t{" + body + "\n\t}";
        return text[..first] + wrapper + text[(propertyClose + 1)..];
    }

    private static int MatchingBrace(string text, int open)
    {
        int depth = 0;
        for (int index = open; index < text.Length; index++)
        {
            if (text[index] == '{') depth++;
            else if (text[index] == '}' && --depth == 0) return index;
        }
        throw new InvalidDataException("managed-detour source contains an unterminated block");
    }

    private static void EmitTarget(StringBuilder source, ManagedDetourTarget target)
    {
        string origParameters = ParameterList(target, includeDefaults: false, includeReceiver: !target.IsStatic);
        string hookParameters = target.OrigDelegate + " orig" + (origParameters.Length == 0 ? "" : ", " + origParameters);
        source.Append("    public delegate ").Append(target.ReturnType).Append(' ').Append(target.OrigDelegate).Append('(').Append(origParameters).AppendLine(");");
        source.Append("    public delegate ").Append(target.ReturnType).Append(' ').Append(target.HookDelegate).Append('(').Append(hookParameters).AppendLine(");");
        source.Append("    private static readonly global::System.Collections.Generic.List<global::Celeste.Mod.AppleEverestManagedHook<")
            .Append(target.HookDelegate).Append(">> Hooks_").Append(target.EventName).AppendLine(" = new();");
        source.Append("    private static int Version_").Append(target.EventName).AppendLine(" = -1;");
        source.Append("    private static ").Append(target.OrigDelegate).Append(" Original_").Append(target.EventName).AppendLine(";");
        source.Append("    private static ").Append(target.OrigDelegate).Append(" Active_").Append(target.EventName).AppendLine(";");
        source.Append("    public static event ").Append(target.HookDelegate).Append(' ').Append(target.EventName)
            .Append(" { add => global::Celeste.Mod.AppleEverestHookList.AddEvent(Hooks_").Append(target.EventName)
            .Append(", value, \"").Append(Escape(target.Id)).Append("\"); remove => global::Celeste.Mod.AppleEverestHookList.RemoveEvent(Hooks_").Append(target.EventName).AppendLine(", value); }");
        source.Append("    internal static global::Celeste.Mod.IAppleEverestManagedHookRegistration RegisterDirect_").Append(target.EventName)
            .Append('(').Append(target.HookDelegate).AppendLine(" handler, bool applyByDefault, long? staticDispatcherOrdinal) =>")
            .Append("        global::Celeste.Mod.AppleEverestHookList.AddDirect(Hooks_").Append(target.EventName).AppendLine(", handler, applyByDefault, staticDispatcherOrdinal);");
        source.Append("    internal static void RemoveOwner_").Append(target.EventName).Append("(string owner) => global::Celeste.Mod.AppleEverestHookList.RemoveOwner(Hooks_")
            .Append(target.EventName).AppendLine(", owner);");

        string invokeParameters = ParameterList(target, includeDefaults: false, includeReceiver: !target.IsStatic);
        if (invokeParameters.Length > 0) invokeParameters += ", ";
        invokeParameters += target.OrigDelegate + " original";
        source.Append("    internal static ").Append(target.ReturnType).Append(" Invoke_").Append(target.EventName).Append('(').Append(invokeParameters).AppendLine(")\n    {");
        source.AppendLine("        int version = global::Celeste.Mod.AppleEverestHookList.Version;");
        source.Append("        if (Version_").Append(target.EventName).Append(" != version || Original_").Append(target.EventName).AppendLine(" != original)\n        {");
        source.Append("            Original_").Append(target.EventName).AppendLine(" = original;");
        source.Append("            Active_").Append(target.EventName).AppendLine(" = original;");
        source.Append("            foreach (").Append(target.HookDelegate).Append(" handler in global::Celeste.Mod.AppleEverestHookList.Active(Hooks_").Append(target.EventName).AppendLine("))\n            {");
        source.Append("                ").Append(target.OrigDelegate).Append(" nextHook = Active_").Append(target.EventName).AppendLine(";");
        List<string> names = InvocationNames(target);
        source.Append("                Active_").Append(target.EventName).Append(" = (").Append(string.Join(", ", names)).Append(") => handler(nextHook");
        foreach (string name in names) source.Append(", ").Append(name);
        source.AppendLine(");\n            }");
        source.Append("            Version_").Append(target.EventName).AppendLine(" = version;\n        }");
        source.Append("        ");
        if (target.ReturnType != "void") source.Append("return ");
        source.Append("Active_").Append(target.EventName).Append('(').Append(string.Join(", ", names)).AppendLine(");\n    }\n");
    }

    private static List<string> InvocationNames(ManagedDetourTarget target)
    {
        List<string> names = [];
        if (!target.IsStatic) names.Add("self");
        names.AddRange(target.Parameters.Select(parameter => parameter.Name));
        return names;
    }

    private static List<string> WrapperInvocationNames(ManagedDetourTarget target)
    {
        List<string> names = [];
        if (!target.IsStatic) names.Add("this");
        names.AddRange(target.Parameters.Select(parameter => parameter.Name));
        return names;
    }

    private static string ParameterList(ManagedDetourTarget target, bool includeDefaults, bool includeReceiver)
    {
        List<string> values = [];
        if (includeReceiver) values.Add(target.ReceiverType + " self");
        values.AddRange(target.Parameters.Select(parameter => parameter.Type + " " + parameter.Name +
            (includeDefaults && parameter.Default != null ? " = " + parameter.Default : "")));
        return string.Join(", ", values);
    }

    private static void ValidateDirectShape(DirectManagedHookPlan plan, ManagedDetourTarget target)
    {
        List<string> arguments = [];
        if (!target.IsStatic) arguments.Add(target.ReceiverType!);
        arguments.AddRange(target.Parameters.Select(parameter => parameter.Type));
        string expectedOrig = "global::System.Func<" + string.Join(", ", arguments.Append(target.ReturnType)) + ">";
        if (target.ReturnType == "void" || plan.DetourReturnType != target.ReturnType || plan.DetourParameterTypes.Length != arguments.Count + 1 ||
            (!plan.CustomOriginalDelegate && plan.DetourParameterTypes[0] != expectedOrig) ||
            !plan.DetourParameterTypes.Skip(1).SequenceEqual(arguments, StringComparer.Ordinal))
            throw new InvalidDataException($"DEFERRED_DIRECT_HOOK_SIGNATURE: {plan.Owner} {plan.DetourType}::{plan.DetourMethod}");
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
