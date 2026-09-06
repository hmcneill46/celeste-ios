using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AppleEverestBuilder;

// Mutations are confined to disposable HOST assemblies. Each omission removes
// real selector delegates, not rows in the structural graph or evidence JSON.
internal static class FactoryPreflightControls
{
    internal static void Write(string assemblyPath, string manifestPath, string profilesPath, string output)
    {
        var graph = SelectedFactoryTypeClosure.LoadAndValidate(manifestPath);
        using JsonDocument profiles = JsonDocument.Parse(File.ReadAllBytes(profilesPath));
        var baseline = CompiledFactoryInspection.Inspect(assemblyPath, profiles.RootElement, graph.Manifest.Factories);
        string[][] groups = [ ["MaxHelpingHand"], ["CollabUtils2"], ["FrostHelper"], ["FemtoHelper", "FlaglinesAndSuch"],
            ["CherryHelper", "FancyTileEntities", "BrokemiaHelper"], ["PandorasBox"], ["HonlyHelper", "LunaticHelper"], ["VivHelper", "XaphanHelper"] ];
        string temporary = Path.Combine(Path.GetTempPath(), "apple-everest-compiled-controls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            string originalRoot = Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!;
            foreach (string path in Directory.EnumerateFiles(originalRoot, "*.dll")) File.Copy(path, Path.Combine(temporary, Path.GetFileName(path)));
            string target = Path.Combine(temporary, "Celeste.dll");
            List<object> omissions = [];
            foreach (string[] group in groups)
            {
                target = Path.Combine(temporary, "Celeste.omit-" + omissions.Count + ".dll");
                var removed = graph.Manifest.Factories.Where(factory => group.Contains(factory.Provider, StringComparer.Ordinal)).ToArray();
                using (AssemblyDefinition assembly = Read(assemblyPath))
                {
                    TypeDefinition registry = assembly.MainModule.GetType("Celeste.Mod.GeneratedAppleEverestGameplayRegistry");
                    foreach (var factory in removed)
                    {
                        string entry = SelectedFactoryProfiles.EntryMethod(factory.Kind, factory.CustomId);
                        var matches = registry.Methods.Where(method => method.Name.StartsWith("Select", StringComparison.Ordinal))
                            .SelectMany(method => method.Body.Instructions).Where(instruction => instruction.OpCode == OpCodes.Ldftn &&
                                instruction.Operand is MethodReference reference && reference.Name == entry).ToArray();
                        if (matches.Length != 1 || matches[0].Next.OpCode != OpCodes.Newobj)
                            throw new InvalidDataException("compiled omission control no longer matches delegate emission: " + factory.CustomId);
                        matches[0].Next.OpCode = OpCodes.Nop; matches[0].Next.Operand = null;
                        matches[0].OpCode = OpCodes.Nop; matches[0].Operand = null;
                    }
                    assembly.Write(target);
                }
                var remaining = CompiledFactoryInspection.Inspect(target, profiles.RootElement, graph.Manifest.Factories, skipMissing: true);
                var expected = baseline.Where(factory => !group.Contains(factory.Provider, StringComparer.Ordinal)).ToArray();
                if (!remaining.Select(factory => factory.Kind + ":" + factory.CustomId).SequenceEqual(expected.Select(factory => factory.Kind + ":" + factory.CustomId)) ||
                    remaining.Where((factory, index) => factory.RegistrationSha256 != expected[index].RegistrationSha256).Any())
                    throw new InvalidDataException("omission changes unrelated compiled factory registrations: " + string.Join(",", group));
                Console.WriteLine("compiled omission PASS: " + string.Join(",", group) + " available=" + remaining.Length);
                omissions.Add(new { providers = group, removed = removed.Length, available = remaining.Length,
                    unchangedUnrelatedRegistrationHashes = true, actualCompiledSelectorOmission = true });
            }
            List<string> rejected = [];
            void Reject(string name, Action<AssemblyDefinition> mutate)
            {
                target = Path.Combine(temporary, "Celeste.negative-" + rejected.Count + ".dll");
                using (AssemblyDefinition assembly = Read(assemblyPath)) { mutate(assembly); assembly.Write(target); }
                bool failed = false;
                try { _ = CompiledFactoryInspection.Inspect(target, profiles.RootElement, graph.Manifest.Factories); }
                catch (Exception exception) when (exception is InvalidDataException or System.Reflection.TargetInvocationException or TypeLoadException or MissingMethodException) { failed = true; }
                if (!failed) throw new InvalidDataException("false-positive compiled factory control: " + name);
                rejected.Add(name);
            }
            Reject("WRONG_COMPILED_OWNER", assembly =>
            {
                TypeDefinition registry = assembly.MainModule.GetType("Celeste.Mod.GeneratedAppleEverestGameplayRegistry");
                string entry = SelectedFactoryProfiles.EntryMethod("entity", "FrostHelper/IceSpinner");
                var owner = registry.Methods.Single(method => method.Name == entry).Body.Instructions.Single(instruction => instruction.OpCode == OpCodes.Ldstr && (string)instruction.Operand == "FrostHelper");
                owner.Operand = "WrongOwner";
            });
            Reject("DECLARED_FACTORY_WITHOUT_LINKED_REGISTRY", assembly =>
                assembly.MainModule.GetType("Celeste.Mod.GeneratedAppleEverestGameplayRegistry").Name = "UnlinkedDeclaredRegistry");
            Reject("DECLARED_SEMANTIC_TYPE_NOT_LINKED", assembly =>
            {
                var entry = assembly.MainModule.GetType("Celeste.Mod.GeneratedAppleEverestGameplayRegistry").Methods
                    .Single(method => method.Name == SelectedFactoryProfiles.EntryMethod("entity", "FrostHelper/IceSpinner"));
                var call = entry.Body.Instructions.Single(instruction => instruction.OpCode == OpCodes.Newobj &&
                    instruction.Operand is MethodReference method && method.DeclaringType.FullName == "Celeste.Mod.AppleEverestFrostSpinner");
                var original = (MethodReference)call.Operand;
                var missing = new TypeReference("Celeste.Mod", "AppleEverestUnlinkedSelectedSemantics", assembly.MainModule, assembly.MainModule);
                var constructor = new MethodReference(original.Name, original.ReturnType, missing) { HasThis = true };
                foreach (var parameter in original.Parameters) constructor.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
                call.Operand = constructor;
            });
            Reject("LINKED_ENTRY_WITH_MISSING_CONSTRUCTOR_BODY", assembly =>
            {
                var type = assembly.MainModule.GetType("Celeste.Mod.AppleEverestFrostSpinner");
                foreach (var method in type.Methods.Where(method => method.IsConstructor && !method.IsStatic))
                { method.Body = null; method.ImplAttributes = MethodImplAttributes.Runtime; }
            });
            Reject("ACTUAL_GUARD_REJECTS_ORIGINAL_AUTHORED_PROFILE", assembly =>
            {
                var guard = assembly.MainModule.GetType("Celeste.Mod.AppleEverestSelectedProfileGuard");
                var method = guard.Methods.Single(method => method.Name == "Entity");
                method.Body.Instructions.Clear(); method.Body.ExceptionHandlers.Clear(); method.Body.Variables.Clear();
                var il = method.Body.GetILProcessor();
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, guard.Methods.Single(candidate => candidate.Name == "Outside")); il.Emit(OpCodes.Throw);
            });
            // Bind the complete implementation, including bodies the small
            // registration inspector intentionally does not traverse. These
            // controls retain the old inspector's complete 73/920 result.
            List<string> implementationRejected = [];
            FileRecord[] exactDlls = FactoryCompilationProof.DllInventory(originalRoot);
            string bindingRoot = Path.Combine(temporary, "binding");
            Directory.CreateDirectory(bindingRoot);
            foreach (string path in Directory.EnumerateFiles(originalRoot, "*.dll"))
                File.Copy(path, Path.Combine(bindingRoot, Path.GetFileName(path)));
            FactoryCompilationProof.VerifyExactDlls(exactDlls, bindingRoot);
            using (AssemblyDefinition roundTrip = Read(assemblyPath))
                roundTrip.Write(Path.Combine(bindingRoot, "Celeste.dll"));
            FactoryCompilationProof.VerifyExactDlls(exactDlls, bindingRoot);
            void RejectImplementation(string name, Action<AssemblyDefinition> mutate)
            {
                string probe = Path.Combine(temporary, "Celeste.binding-" + implementationRejected.Count + ".dll");
                using (AssemblyDefinition assembly = Read(assemblyPath)) { mutate(assembly); assembly.Write(probe); }
                var present = CompiledFactoryInspection.Inspect(probe, profiles.RootElement, graph.Manifest.Factories);
                if (present.Length != baseline.Length || present.Sum(factory => factory.AcceptedOccurrences) != baseline.Sum(factory => factory.AcceptedOccurrences))
                    throw new InvalidDataException("implementation control did not preserve registrations/guards: " + name);
                File.Copy(probe, Path.Combine(bindingRoot, "Celeste.dll"), overwrite: true);
                bool failed = false;
                try { FactoryCompilationProof.VerifyExactDlls(exactDlls, bindingRoot); }
                catch (InvalidDataException) { failed = true; }
                if (!failed) throw new InvalidDataException("false-positive compiled implementation control: " + name);
                implementationRejected.Add(name);
            }
            RejectImplementation("STALE_IMPLEMENTATION_COMPONENT_NOT_ATTACHED", assembly =>
            {
                var attach = assembly.MainModule.GetType("Celeste.Mod.AppleEverestMaxFlagCamera").Methods.Single(method => method.Name == "Attach");
                attach.Body.Instructions.Clear(); attach.Body.ExceptionHandlers.Clear(); attach.Body.Variables.Clear();
                var il = attach.Body.GetILProcessor(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ret);
            });
            RejectImplementation("WRONG_CONCRETE_TARGET", assembly =>
            {
                var entry = assembly.MainModule.GetType("Celeste.Mod.GeneratedAppleEverestGameplayRegistry").Methods
                    .Single(method => method.Name == SelectedFactoryProfiles.EntryMethod("entity", "FrostHelper/IceSpinner"));
                var constructor = entry.Body.Instructions.Single(instruction => instruction.OpCode == OpCodes.Newobj &&
                    instruction.Operand is MethodReference method && method.DeclaringType.FullName == "Celeste.Mod.AppleEverestFrostSpinner");
                constructor.Operand = assembly.MainModule.GetType("Celeste.Mod.AppleEverestFrostFireBarrier").Methods
                    .Single(method => method.IsConstructor && !method.IsStatic);
            });
            RejectImplementation("CREATED_VALUE_DISCARDED_RETURNS_NULL", assembly =>
            {
                var entry = assembly.MainModule.GetType("Celeste.Mod.GeneratedAppleEverestGameplayRegistry").Methods
                    .Single(method => method.Name == SelectedFactoryProfiles.EntryMethod("entity", "FrostHelper/IceSpinner"));
                var il = entry.Body.GetILProcessor();
                var returned = entry.Body.Instructions.Single(instruction => instruction.OpCode == OpCodes.Ret);
                il.InsertBefore(returned, il.Create(OpCodes.Pop)); il.InsertBefore(returned, il.Create(OpCodes.Ldnull));
            });
            File.Copy(assemblyPath, Path.Combine(bindingRoot, "Celeste.dll"), overwrite: true);
            string sibling = Path.Combine(bindingRoot, "CelesteAppleInput.dll");
            byte[] changed = File.ReadAllBytes(sibling); changed[^1] ^= 1; File.WriteAllBytes(sibling, changed);
            bool siblingFailed = false;
            try { FactoryCompilationProof.VerifyExactDlls(exactDlls, bindingRoot); }
            catch (InvalidDataException) { siblingFailed = true; }
            if (!siblingFailed) throw new InvalidDataException("stale sibling DLL escaped compiler binding");
            implementationRejected.Add("CHANGED_SIBLING_DLL_BYTES");
            File.WriteAllText(output, JsonSerializer.Serialize(new { schemaVersion = 2,
                positiveFactories = baseline.Length, positiveOccurrences = baseline.Sum(factory => factory.AcceptedOccurrences), omissions,
                rejectedCompiledControls = rejected, rejectedImplementationControls = implementationRejected,
                structuralGraphAloneCannotPass = true }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }
    private static AssemblyDefinition Read(string path) => AssemblyDefinition.ReadAssembly(path,
        new ReaderParameters { ReadSymbols = false, InMemory = true });
}
