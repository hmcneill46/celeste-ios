using AppleEverestBuilder;
using Mono.Cecil;

internal static class AotNativeRootSignatureTests
{
    internal static int Run()
    {
        using var assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("OwnedFixture", new Version(1, 0)), "OwnedFixture", ModuleKind.Dll);
        ModuleDefinition module = assembly.MainModule;
        var owner = new TypeDefinition("Fixture", "Audio", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(owner);
        var description = new TypeReference("FMOD.Studio", "EventDescription", module, module);
        int checks = 0;
        string Symbol(TypeReference parameter, ParameterAttributes attributes = ParameterAttributes.None)
        {
            var method = new MethodDefinition("Resolve", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
            method.Parameters.Add(new ParameterDefinition("value", attributes, parameter));
            owner.Methods.Add(method);
            return AotFactoryProductInspection.Symbol(method);
        }
        void Equal(string actual, string expected)
        { if (actual != expected) throw new InvalidOperationException("native signature mismatch: " + actual); checks++; }
        void Reject(TypeReference parameter)
        {
            try { Symbol(parameter); }
            catch (InvalidDataException error) when (error.Message.Contains("unreviewed native root signature shape", StringComparison.Ordinal))
            { checks++; return; }
            throw new InvalidOperationException("unsupported native signature accepted: " + parameter.FullName);
        }
        const string prefix = "_OwnedFixture_Fixture_Audio_Resolve_";
        Equal(Symbol(description), prefix + "FMOD_Studio_EventDescription");
        Equal(Symbol(new ByReferenceType(description)), prefix + "FMOD_Studio_EventDescription_");
        Equal(Symbol(new ByReferenceType(description), ParameterAttributes.Out), prefix + "FMOD_Studio_EventDescription_");
        Equal(Symbol(new ByReferenceType(module.TypeSystem.Int32)), prefix + "int_");
        Equal(Symbol(new ByReferenceType(module.TypeSystem.String)), prefix + "string_");
        Equal(Symbol(module.TypeSystem.Int32), prefix + "int");
        Equal(Symbol(new ArrayType(description)), prefix + "FMOD_Studio_EventDescription__");
        Reject(new PointerType(description));
        Reject(new ByReferenceType(new PointerType(description)));
        Reject(new ByReferenceType(new ByReferenceType(description)));
        Reject(new ByReferenceType(new ArrayType(description)));
        Reject(new ByReferenceType(new GenericParameter("T", owner)));
        Reject(new ArrayType(description, 2));
        var open = new TypeReference("Fixture", "Open`1", module, module);
        open.GenericParameters.Add(new GenericParameter("T", open));
        Reject(new ByReferenceType(open));
        var closed = new GenericInstanceType(open);
        closed.GenericArguments.Add(description);
        Reject(new ByReferenceType(closed));
        Reject(new ByReferenceType(new OptionalModifierType(description, module.TypeSystem.Int32)));
        Reject(new ByReferenceType(new RequiredModifierType(description, module.TypeSystem.Int32)));
        Reject(new ByReferenceType(new PinnedType(description)));
        Console.WriteLine("PASS: " + checks + " owned native root signature controls; no third-party binaries");
        return checks;
    }
}
