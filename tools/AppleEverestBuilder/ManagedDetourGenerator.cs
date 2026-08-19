using System.Text;

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
                .Append(".RegisterDirect_").Append(target.EventName).AppendLine("(adapter, null, applyByDefault);\n        }");
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
            string needle = "\t" + target.SourceDeclaration + "\n\t{";
            int first = text.IndexOf(needle, StringComparison.Ordinal);
            if (first < 0 || first != text.LastIndexOf(needle, StringComparison.Ordinal))
                throw new InvalidDataException($"managed-detour target declaration drifted or is ambiguous: {target.Id}");
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
            .Append(", value); remove => global::Celeste.Mod.AppleEverestHookList.RemoveEvent(Hooks_").Append(target.EventName).AppendLine(", value); }");
        source.Append("    internal static global::Celeste.Mod.IAppleEverestManagedHookRegistration RegisterDirect_").Append(target.EventName)
            .Append('(').Append(target.HookDelegate).AppendLine(" handler, global::MonoMod.RuntimeDetour.DetourConfig config, bool applyByDefault) =>")
            .Append("        global::Celeste.Mod.AppleEverestHookList.AddDirect(Hooks_").Append(target.EventName).AppendLine(", handler, config, applyByDefault);");
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
            plan.DetourParameterTypes[0] != expectedOrig || !plan.DetourParameterTypes.Skip(1).SequenceEqual(arguments, StringComparer.Ordinal))
            throw new InvalidDataException($"DEFERRED_DIRECT_HOOK_SIGNATURE: {plan.Owner} {plan.DetourType}::{plan.DetourMethod}");
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
