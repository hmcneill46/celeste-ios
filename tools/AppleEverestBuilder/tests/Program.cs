using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AppleEverestBuilder;
using Celeste.Mod;
using Mono.Cecil;
using Mono.Cecil.Cil;

int passed = 0;
string repository = Path.GetFullPath(args.Length == 1 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../../"));
string temporary = Path.Combine(Path.GetTempPath(), "apple-everest-stage25b-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);

void Pass(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    passed++;
}

void Throws(Action action, string contains, string name)
{
    try { action(); }
    catch (Exception exception) when (exception.Message.Contains(contains, StringComparison.OrdinalIgnoreCase))
    {
        passed++;
        return;
    }
    throw new InvalidOperationException("FAIL: " + name);
}

string NewDirectory(string name)
{
    string result = Path.Combine(temporary, name);
    Directory.CreateDirectory(result);
    return result;
}

void Text(string root, string relative, string value)
{
    string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, value, new UTF8Encoding(false));
}

ResolvedMod Mod(string name, string version = "1.0.0", IEnumerable<(string Name, string Version)>? dependencies = null,
    IEnumerable<(string Name, string Version)>? optional = null, IEnumerable<(string Name, string Version)>? conflicts = null)
{
    EverestYamlEntry metadata = new() { Name = name, Version = version };
    foreach ((string item, string requirement) in dependencies ?? []) metadata.Dependencies.Add(new EverestDependency { Name = item, Version = requirement });
    foreach ((string item, string requirement) in optional ?? []) metadata.OptionalDependencies.Add(new EverestDependency { Name = item, Version = requirement });
    foreach ((string item, string requirement) in conflicts ?? []) metadata.Conflicts.Add(new EverestDependency { Name = item, Version = requirement });
    return new ResolvedMod
    {
        Metadata = metadata,
        Input = new ModInput { SourcePath = name, StagingRoot = temporary, SourceSha256 = name, Files = [], Metadata = [metadata] },
        Classification = CompatibilityClass.CONTENT_ONLY,
        Mechanisms = new SortedSet<string>(StringComparer.Ordinal),
        ManagedFiles = [], ContentFiles = [],
        ManagedDetourTargets = new SortedSet<string>(StringComparer.Ordinal),
        DirectManagedHooks = [],
        ModInteropRegistrations = []
    };
}

try
{
    passed += ModInteropTests.Run(repository, temporary);
    passed += ModuleDurabilityTests.Run(repository, temporary);

    EverestVersion required = EverestVersion.Parse("1.2.3.4");
    Pass(EverestVersion.Satisfies(required, EverestVersion.Parse("1.2.3.4")), "exact version");
    Pass(EverestVersion.Satisfies(required, EverestVersion.Parse("1.3.0")), "newer minor");
    Pass(!EverestVersion.Satisfies(required, EverestVersion.Parse("2.2.3.4")), "major mismatch");
    Pass(!EverestVersion.Satisfies(required, EverestVersion.Parse("1.2.2.9")), "older build");
    Pass(EverestVersion.Satisfies(required, EverestVersion.Parse("0.0.1")), "Everest dev version");
    Throws(() => EverestVersion.Parse("1.bad"), "invalid Everest version", "invalid version rejected");

    AppleEverestSettingRecord[] settingRecords =
    [
        new("ZetaHelper", "Enabled", 1),
        new("AlphaHelper", "Mode", 2)
    ];
    string encodedSettings = AppleEverestSettingsCodec.Encode(settingRecords);
    Pass(AppleEverestSettingsCodec.TryDecode(encodedSettings, out AppleEverestSettingRecord[] decodedSettings) &&
         decodedSettings.SequenceEqual(settingRecords.Reverse()), "module settings deterministic round trip");
    Pass(encodedSettings.StartsWith(AppleEverestSettingsCodec.Header + "\n", StringComparison.Ordinal) &&
         encodedSettings.IndexOf("AlphaHelper", StringComparison.Ordinal) < 0,
        "module settings use a versioned encoded namespace");
    Pass(!AppleEverestSettingsCodec.TryDecode(encodedSettings.Replace("\t2\n", "\tbad\n", StringComparison.Ordinal), out _),
        "module settings malformed value rejected");
    string duplicateSettings = AppleEverestSettingsCodec.Header + "\nQQ\tQg\t0\nQQ\tQg\t1\n";
    Pass(!AppleEverestSettingsCodec.TryDecode(duplicateSettings, out _), "module settings duplicate key rejected");
    Pass(!AppleEverestSettingsCodec.TryDecode(AppleEverestSettingsCodec.Header + "\n" +
         new string('A', AppleEverestSettingsCodec.MaximumBytes), out _), "module settings oversized payload rejected");

    ResolvedMod graphA = Mod("A");
    ResolvedMod graphB = Mod("B", dependencies: [("A", "1.0.0")]);
    Pass(EverestGraphResolver.Resolve([graphB, graphA]).Select(mod => mod.Metadata.Name).SequenceEqual(["A", "B"]), "dependency order");
    Pass(EverestGraphResolver.Resolve([Mod("C", optional: [("Missing", "1.0.0")])]).Count == 1, "missing optional dependency");
    Pass(EverestGraphResolver.Resolve([Mod("A"), Mod("B", optional: [("A", "1.0.0")])]).Select(mod => mod.Metadata.Name).SequenceEqual(["A", "B"]), "present optional order");
    Throws(() => EverestGraphResolver.Resolve([Mod("B", dependencies: [("Missing", "1.0.0")])]), "missing dependency", "missing required dependency");
    Throws(() => EverestGraphResolver.Resolve([Mod("A"), Mod("A")]), "duplicate", "duplicate identity");
    Throws(() => EverestGraphResolver.Resolve([Mod("A", dependencies: [("B", "1.0.0")]), Mod("B", dependencies: [("A", "1.0.0")])]), "cycle", "cycle");
    Throws(() => EverestGraphResolver.Resolve([Mod("A", "1.0.0"), Mod("B", dependencies: [("A", "2.0.0")])]), "incompatible", "version incompatibility");
    Throws(() => EverestGraphResolver.Resolve([Mod("A"), Mod("B", conflicts: [("A", "1.0.0")])]), "conflict", "conflict");
    Pass(EverestGraphResolver.Resolve([graphB, graphA]).Select(mod => mod.Metadata.Name).SequenceEqual(
        EverestGraphResolver.Resolve([graphA, graphB]).Select(mod => mod.Metadata.Name)), "stable graph order");
    ResolvedMod cpopGraph = Mod("CpopHelper", "1.3.0", [("Everest", "1.3471.0")]);
    ResolvedMod quizGraph = Mod("QuizSample", "0.0.1", [("CpopHelper", "1.0.0"), ("Everest", "1.3761.0")]);
    Pass(EverestGraphResolver.Resolve([quizGraph, cpopGraph]).Select(mod => mod.Metadata.Name)
        .SequenceEqual(["CpopHelper", "QuizSample"]), "real map-to-helper dependency order");
    Throws(() => EverestGraphResolver.Resolve([quizGraph]), "missing dependency", "real map missing helper rejected");
    Throws(() => EverestGraphResolver.Resolve([quizGraph, Mod("CpopHelper", "0.9.0")]), "incompatible",
        "real map wrong helper version rejected");

    string content = NewDirectory("content");
    Text(content, "everest.yaml", "- Name: MultiA\n  Version: 1.0.0\n- Name: MultiB\n  Version: 2.0.0\n");
    Text(content, "Content/test.txt", "owned\n");
    ModInput multi = SafeModIngestor.Ingest(content, NewDirectory("stage-multi"), 0);
    Pass(multi.Metadata.Count == 2, "multi-entry YAML");
    Pass(multi.Metadata[1].Name == "MultiB", "YAML fields");
    Pass(multi.Files.Any(file => file.Path == "Content/test.txt"), "directory inventory");

    string zipPath = Path.Combine(temporary, "safe.zip");
    using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
    {
        using (StreamWriter yaml = new(zip.CreateEntry("everest.yaml").Open()))
            yaml.Write("- Name: ZipSafe\n  Version: 1.0.0\n");
        using (StreamWriter data = new(zip.CreateEntry("Content/value.txt").Open()))
            data.Write("safe");
    }
    Pass(SafeModIngestor.Ingest(zipPath, NewDirectory("stage-safe-zip"), 0).Metadata[0].Name == "ZipSafe", "safe ZIP");

    string traversal = Path.Combine(temporary, "traversal.zip");
    using (ZipArchive zip = ZipFile.Open(traversal, ZipArchiveMode.Create)) zip.CreateEntry("../escape.txt");
    Throws(() => SafeModIngestor.Ingest(traversal, NewDirectory("stage-traversal"), 0), "unsafe", "ZIP traversal");
    string absolute = Path.Combine(temporary, "absolute.zip");
    using (ZipArchive zip = ZipFile.Open(absolute, ZipArchiveMode.Create)) zip.CreateEntry("/absolute.txt");
    Throws(() => SafeModIngestor.Ingest(absolute, NewDirectory("stage-absolute"), 0), "absolute", "ZIP absolute path");
    string duplicate = Path.Combine(temporary, "duplicate.zip");
    using (ZipArchive zip = ZipFile.Open(duplicate, ZipArchiveMode.Create)) { zip.CreateEntry("A.txt"); zip.CreateEntry("a.txt"); }
    Throws(() => SafeModIngestor.Ingest(duplicate, NewDirectory("stage-duplicate"), 0), "duplicate", "ZIP duplicate path");
    string linked = Path.Combine(temporary, "linked.zip");
    using (ZipArchive zip = ZipFile.Open(linked, ZipArchiveMode.Create)) { ZipArchiveEntry link = zip.CreateEntry("link"); link.ExternalAttributes = 0xA000; }
    Throws(() => SafeModIngestor.Ingest(linked, NewDirectory("stage-link"), 0), "links", "ZIP link");

    CompatibilityClass AnalyzeSource(string name, string source)
    {
        string root = NewDirectory("analyze-" + name);
        Text(root, "everest.yaml", $"- Name: {name}\n  Version: 1.0.0\n  DLL: Code.cs\n");
        Text(root, "Code.cs", source);
        Text(root, "apple-static.json", $"{{\"schemaVersion\":1,\"moduleType\":\"Tests.{name}Module\",\"trackedEntityTypes\":[]}}");
        ModInput input = SafeModIngestor.Ingest(root, NewDirectory("stage-" + name), 0);
        return CompatibilityAnalyzer.Analyze(input, input.Metadata[0]).Classification;
    }
    Pass(AnalyzeSource("Static", "class Static {}") == CompatibilityClass.STATIC_MODULE, "static source class");
    Pass(AnalyzeSource("Event", "// Everest.Events.Level\nclass Event {}") == CompatibilityClass.NORMAL_EVENT, "ordinary event class");
    Pass(AnalyzeSource("OnHook", "// On.Celeste.Dialog.Clean += handler; On.Celeste.Dialog.orig_Clean orig\nclass Hook {}") == CompatibilityClass.ON_HOOK_SUPPORTED, "supported On hook");
    Pass(AnalyzeSource("OtherOn", "// On.Celeste.Level.LoadLevel += handler\nclass Hook {}") == CompatibilityClass.ON_HOOK_SUPPORTED,
        "catalogued On target accepted without analyzer special case");
    foreach ((string name, string source, string expected) in new[]
    {
        ("IL", "// IL.Celeste.Player.Update", "IL_HOOK_DEFERRED"),
        ("Direct", "// new Hook(target, hook)", "DIRECT_HOOK_DEFERRED"),
        ("Native", "// NativeDetour", "NATIVE_UNSUPPORTED"),
        ("PInvoke", "// DllImport", "NATIVE_UNSUPPORTED"),
        ("Lua", "// NLua", "LUA_UNSUPPORTED"),
        ("Dynamic", "// Assembly.Load(bytes)", "DYNAMIC_CODE_UNSUPPORTED"),
        ("Emit", "// DynamicMethod", "DYNAMIC_CODE_UNSUPPORTED"),
        ("Process", "// Process.Start", "PLATFORM_UNSUPPORTED"),
        ("Watcher", "// FileSystemWatcher", "PLATFORM_UNSUPPORTED")
    }) Throws(() => AnalyzeSource(name, source + "\nclass Test {}"), expected, name + " analyzer rejection");

    string BinaryFixture(string name, string hookNamespace, string hookType, string? hookEvent = null)
    {
        string root = NewDirectory("binary-" + name);
        Text(root, "everest.yaml", $"- Name: {name}\n  Version: 1.0.0\n  DLL: Code/{name}.dll\n  Dependencies:\n    - Name: Everest\n      Version: 1.6418.0\n");
        string path = Path.Combine(root, "Code", name + ".dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition(name, new Version(1, 0, 0, 0)), name, ModuleKind.Dll);
        AssemblyNameReference celeste = new("Celeste", new Version(1, 0, 0, 0));
        AssemblyNameReference hooks = new("MMHOOK_Celeste", new Version(0, 0, 0, 0));
        AssemblyNameReference xnaFacade = new("Microsoft.Xna.Framework", new Version(4, 0, 0, 0))
        {
            PublicKeyToken = [0x84, 0x2c, 0xf8, 0xbe, 0x1d, 0xe5, 0x05, 0x53]
        };
        assembly.MainModule.AssemblyReferences.Add(celeste);
        assembly.MainModule.AssemblyReferences.Add(hooks);
        assembly.MainModule.AssemblyReferences.Add(xnaFacade);
        TypeDefinition module = new("Fixture", name + "Module", TypeAttributes.Public | TypeAttributes.Sealed,
            new TypeReference("Celeste.Mod", "EverestModule", assembly.MainModule, celeste));
        MethodDefinition constructor = new(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            assembly.MainModule.TypeSystem.Void);
        if (hookEvent != null)
        {
            TypeReference hookTypeReference = new(hookNamespace, hookType, assembly.MainModule, hooks);
            MethodReference add = new("add_" + hookEvent, assembly.MainModule.TypeSystem.Void, hookTypeReference)
            {
                HasThis = false
            };
            add.Parameters.Add(new ParameterDefinition(new TypeReference(hookNamespace + "." + hookType, "hook_" + hookEvent,
                assembly.MainModule, hooks)));
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, add));
        }
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        module.Methods.Add(constructor);
        module.Fields.Add(new FieldDefinition("HookRoot", FieldAttributes.Public | FieldAttributes.Static,
            new TypeReference(hookNamespace, hookType, assembly.MainModule, hooks)));
        module.Fields.Add(new FieldDefinition("LegacyFnaVector", FieldAttributes.Public,
            new TypeReference("Microsoft.Xna.Framework", "Vector2", assembly.MainModule, xnaFacade)));
        assembly.MainModule.Types.Add(module);
        assembly.Write(path);
        return root;
    }

    string GameplayFixture(string name, bool duplicateId = false, bool invalidConstructor = false)
    {
        string root = NewDirectory("gameplay-" + name);
        Text(root, "everest.yaml", $"- Name: {name}\n  Version: 1.0.0\n  DLL: Code/{name}.dll\n  Dependencies:\n    - Name: Everest\n      Version: 1.6418.0\n");
        string path = Path.Combine(root, "Code", name + ".dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition(name, new Version(1, 0, 0, 0)), name, ModuleKind.Dll);
        ModuleDefinition module = assembly.MainModule;
        AssemblyNameReference celeste = new("Celeste", new Version(1, 4, 0, 0));
        AssemblyNameReference fna = new("FNA", new Version(21, 3, 5, 0));
        module.AssemblyReferences.Add(celeste);
        module.AssemblyReferences.Add(fna);
        TypeReference vector2 = new("Microsoft.Xna.Framework", "Vector2", module, fna);
        TypeReference entityData = new("Celeste", "EntityData", module, celeste);
        TypeReference entityId = new("Celeste", "EntityID", module, celeste);

        TypeDefinition settings = new("Fixture", name + "Settings", TypeAttributes.Public | TypeAttributes.Sealed,
            module.TypeSystem.Object);
        void Property(string propertyName, TypeReference type, CustomAttribute? attribute = null)
        {
            FieldDefinition field = new("_" + propertyName, FieldAttributes.Private, type);
            settings.Fields.Add(field);
            MethodDefinition getter = new("get_" + propertyName, MethodAttributes.Public | MethodAttributes.HideBySig |
                MethodAttributes.SpecialName, type);
            getter.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            getter.Body.Instructions.Add(Instruction.Create(OpCodes.Ldfld, field));
            getter.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            MethodDefinition setter = new("set_" + propertyName, MethodAttributes.Public | MethodAttributes.HideBySig |
                MethodAttributes.SpecialName, module.TypeSystem.Void);
            setter.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, type));
            setter.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            setter.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
            setter.Body.Instructions.Add(Instruction.Create(OpCodes.Stfld, field));
            setter.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            settings.Methods.Add(getter);
            settings.Methods.Add(setter);
            PropertyDefinition property = new(propertyName, PropertyAttributes.None, type) { GetMethod = getter, SetMethod = setter };
            if (attribute != null) property.CustomAttributes.Add(attribute);
            settings.Properties.Add(property);
        }
        TypeDefinition mode = new("Fixture", name + "Mode", TypeAttributes.Public | TypeAttributes.Sealed,
            module.ImportReference(typeof(Enum)));
        mode.Fields.Add(new FieldDefinition("value__", FieldAttributes.Public | FieldAttributes.SpecialName |
            FieldAttributes.RTSpecialName, module.TypeSystem.Int32));
        mode.Fields.Add(new FieldDefinition("Quiet", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal,
            mode) { Constant = 0 });
        mode.Fields.Add(new FieldDefinition("Loud", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal,
            mode) { Constant = 2 });
        module.Types.Add(mode);
        TypeReference rangeAttribute = new("Celeste.Mod", "SettingRangeAttribute", module, celeste);
        MethodReference rangeConstructor = new(".ctor", module.TypeSystem.Void, rangeAttribute) { HasThis = true };
        rangeConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
        rangeConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
        CustomAttribute range = new(rangeConstructor);
        range.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.Int32, 1));
        range.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.Int32, 5));
        Property("Enabled", module.TypeSystem.Boolean);
        Property("DisplayMode", mode);
        Property("Amount", module.TypeSystem.Int32, range);
        Property("Unbounded", module.TypeSystem.Int32);
        module.Types.Add(settings);

        TypeDefinition everestModule = new("Fixture", name + "Module", TypeAttributes.Public | TypeAttributes.Sealed,
            new TypeReference("Celeste.Mod", "EverestModule", module, celeste));
        MethodDefinition moduleConstructor = new(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName, module.TypeSystem.Void);
        moduleConstructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        everestModule.Methods.Add(moduleConstructor);
        MethodDefinition settingsType = new("get_SettingsType", MethodAttributes.Public | MethodAttributes.Virtual |
            MethodAttributes.HideBySig | MethodAttributes.SpecialName, module.ImportReference(typeof(Type)));
        settingsType.Body.Instructions.Add(Instruction.Create(OpCodes.Ldtoken, settings));
        settingsType.Body.Instructions.Add(Instruction.Create(OpCodes.Call,
            module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!)));
        settingsType.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        everestModule.Methods.Add(settingsType);
        module.Types.Add(everestModule);

        void CustomType(string typeName, string baseName, string[] ids, TypeReference[] parameters)
        {
            TypeDefinition type = new("Fixture", typeName, TypeAttributes.Public | TypeAttributes.Sealed,
                new TypeReference("Celeste", baseName, module, celeste));
            MethodDefinition constructor = new(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            foreach (TypeReference parameter in parameters)
                constructor.Parameters.Add(new ParameterDefinition(parameter));
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            type.Methods.Add(constructor);
            TypeReference customAttributeType = new("Celeste.Mod.Entities", "CustomEntityAttribute", module, celeste);
            MethodReference customAttributeConstructor = new(".ctor", module.TypeSystem.Void, customAttributeType) { HasThis = true };
            customAttributeConstructor.Parameters.Add(new ParameterDefinition(new ArrayType(module.TypeSystem.String)));
            CustomAttribute custom = new(customAttributeConstructor);
            custom.ConstructorArguments.Add(new CustomAttributeArgument(new ArrayType(module.TypeSystem.String),
                ids.Select(id => new CustomAttributeArgument(module.TypeSystem.String, id)).ToArray()));
            type.CustomAttributes.Add(custom);
            module.Types.Add(type);
        }
        CustomType("CustomBlock", "Entity", duplicateId ? ["fixture/shared", "fixture/shared"] :
            ["fixture/block", "fixture/blockAlias"], invalidConstructor ? [entityData] : [entityData, vector2]);
        CustomType("CustomTrigger", "Trigger", ["fixture/trigger"], [entityData, vector2, entityId]);
        CustomType("CustomIdFirst", "Entity", ["fixture/idFirst"], [entityId, entityData, vector2]);

        TypeReference element = new("Celeste", "BinaryPacker/Element", module, celeste);
        TypeReference backdrop = new("Celeste", "Backdrop", module, celeste);
        TypeDefinition customBackdrop = new("Fixture", "CustomBackdrop", TypeAttributes.Public | TypeAttributes.Sealed, backdrop);
        MethodDefinition backdropFactory = new("Build", MethodAttributes.Public | MethodAttributes.Static, backdrop);
        backdropFactory.Parameters.Add(new ParameterDefinition(element));
        backdropFactory.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
        backdropFactory.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        customBackdrop.Methods.Add(backdropFactory);
        TypeReference backdropAttributeType = new("Celeste.Mod.Backdrops", "CustomBackdropAttribute", module, celeste);
        MethodReference backdropAttributeConstructor = new(".ctor", module.TypeSystem.Void, backdropAttributeType) { HasThis = true };
        backdropAttributeConstructor.Parameters.Add(new ParameterDefinition(new ArrayType(module.TypeSystem.String)));
        CustomAttribute backdropAttribute = new(backdropAttributeConstructor);
        backdropAttribute.ConstructorArguments.Add(new CustomAttributeArgument(new ArrayType(module.TypeSystem.String),
            new[] { new CustomAttributeArgument(module.TypeSystem.String, "fixture/backdrop=Build") }));
        customBackdrop.CustomAttributes.Add(backdropAttribute);
        module.Types.Add(customBackdrop);
        assembly.Write(path);
        return root;
    }

    string DirectBinaryFixture(string name, string mode)
    {
        string root = NewDirectory("direct-binary-" + name);
        Text(root, "everest.yaml", $"- Name: {name}\n  Version: 1.0.0\n  DLL: Code/{name}.dll\n  Dependencies:\n    - Name: Everest\n      Version: 1.6418.0\n");
        string path = Path.Combine(root, "Code", name + ".dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition(name, new Version(1, 0, 0, 0)), name, ModuleKind.Dll);
        ModuleDefinition module = assembly.MainModule;
        AssemblyNameReference celeste = new("Celeste", new Version(1, 0, 0, 0));
        AssemblyNameReference runtimeDetour = new("MonoMod.RuntimeDetour", new Version(25, 2, 3, 0));
        module.AssemblyReferences.Add(celeste);
        module.AssemblyReferences.Add(runtimeDetour);
        AssemblyNameReference? hooks = null;
        if (mode == "mixed")
        {
            hooks = new AssemblyNameReference("MMHOOK_Celeste", new Version(0, 0, 0, 0));
            module.AssemblyReferences.Add(hooks);
        }
        TypeReference player = new("Celeste", "Player", module, celeste);
        TypeReference playerDeadBody = new("Celeste", "PlayerDeadBody", module, celeste);
        TypeReference vector2 = new("Microsoft.Xna.Framework", "Vector2", module, celeste);
        TypeDefinition fixture = new("Fixture", name + "Module", TypeAttributes.Public | TypeAttributes.Sealed,
            new TypeReference("Celeste.Mod", "EverestModule", module, celeste));
        MethodDefinition detour = new("OnPlayerDie", MethodAttributes.Public | MethodAttributes.Static, playerDeadBody);
        GenericInstanceType orig = new(new TypeReference("System", "Func`5", module, module.TypeSystem.CoreLibrary));
        orig.GenericArguments.Add(player);
        orig.GenericArguments.Add(vector2);
        orig.GenericArguments.Add(module.TypeSystem.Boolean);
        orig.GenericArguments.Add(module.TypeSystem.Boolean);
        orig.GenericArguments.Add(playerDeadBody);
        detour.Parameters.Add(new ParameterDefinition(orig));
        detour.Parameters.Add(new ParameterDefinition(player));
        detour.Parameters.Add(new ParameterDefinition(vector2));
        detour.Parameters.Add(new ParameterDefinition(module.TypeSystem.Boolean));
        detour.Parameters.Add(new ParameterDefinition(module.TypeSystem.Boolean));
        detour.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
        detour.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        fixture.Methods.Add(detour);
        if (mode == "dynamic-detour")
        {
            MethodDefinition overload = new("OnPlayerDie", MethodAttributes.Public | MethodAttributes.Static, playerDeadBody);
            overload.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
            overload.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            fixture.Methods.Add(overload);
        }

        MethodDefinition constructor = new(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        MethodReference getType = module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
        MethodReference getTarget = module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetMethod),
            [typeof(string), typeof(System.Reflection.BindingFlags)])!);
        MethodReference getDetour = module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetMethod), [typeof(string)])!);
        if (hooks != null)
        {
            TypeReference engineHook = new("On.Monocle", "Engine", module, hooks);
            MethodReference addUpdate = new("add_Update", module.TypeSystem.Void, engineHook) { HasThis = false };
            addUpdate.Parameters.Add(new ParameterDefinition(new TypeReference("On.Monocle.Engine", "hook_Update", module, hooks)));
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, addUpdate));
        }
        constructor.Body.Instructions.Add(mode == "dynamic-target"
            ? Instruction.Create(OpCodes.Ldnull)
            : Instruction.Create(OpCodes.Ldtoken, player));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, getType));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "orig_Die"));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_S,
            mode == "wrong-binding-flags" ? (sbyte)16 : (sbyte)20));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, getTarget));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldtoken, fixture));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, getType));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "OnPlayerDie"));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, getDetour));
        TypeReference hook = new("MonoMod.RuntimeDetour", "Hook", module, runtimeDetour);
        MethodReference hookConstructor = new(".ctor", module.TypeSystem.Void, hook) { HasThis = true };
        hookConstructor.Parameters.Add(new ParameterDefinition(new TypeReference("System.Reflection", "MethodBase", module, module.TypeSystem.CoreLibrary)));
        hookConstructor.Parameters.Add(new ParameterDefinition(new TypeReference("System.Reflection", "MethodInfo", module, module.TypeSystem.CoreLibrary)));
        if (mode == "config")
        {
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
            hookConstructor.Parameters.Add(new ParameterDefinition(new TypeReference(
                "MonoMod.RuntimeDetour", "DetourConfig", module, runtimeDetour)));
        }
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Newobj, hookConstructor));
        if (mode == "unsupported-member")
        {
            MethodReference unsupported = new("get_Target", new TypeReference(
                "System.Reflection", "MethodBase", module, module.TypeSystem.CoreLibrary), hook) { HasThis = true };
            constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, unsupported));
        }
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Pop));
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        fixture.Methods.Add(constructor);
        assembly.MainModule.Types.Add(fixture);
        assembly.Write(path);
        return root;
    }

    string binaryRoot = BinaryFixture("BinarySupported", "On.Monocle", "ParticleSystem", "Emit_ParticleType_Vector2");
    ModInput binaryInput = SafeModIngestor.Ingest(binaryRoot, NewDirectory("stage-binary-supported"), 0);
    ResolvedMod binaryMod = CompatibilityAnalyzer.Analyze(binaryInput, binaryInput.Metadata[0]);
    Pass(binaryMod.Classification == CompatibilityClass.ON_HOOK_SUPPORTED, "precompiled typed hook accepted");
    Pass(binaryMod.Declaration?.ModuleType == "Fixture.BinarySupportedModule", "precompiled module factory inferred");
    Pass(binaryMod.ManagedFiles.SequenceEqual(["Code/BinarySupported.dll"]), "precompiled DLL accepted without source");
    string frozen = Path.Combine(NewDirectory("frozen"), "BinarySupported.dll");
    (string frozenAssemblyName, string originalHash, string frozenHash) = AssemblyFreezer.Freeze(
        Path.Combine(binaryInput.StagingRoot, "Code", "BinarySupported.dll"), frozen, []);
    using (AssemblyDefinition frozenAssembly = AssemblyDefinition.ReadAssembly(frozen))
    {
        Pass(frozenAssembly.MainModule.AssemblyReferences.All(reference => reference.Name != "MMHOOK_Celeste"), "frozen assembly removes HookGen runtime reference");
        Pass(frozenAssembly.MainModule.GetTypeReferences().Any(type => type.Namespace == "On.Monocle" && type.Name == "ParticleSystem" &&
             type.Scope is AssemblyNameReference reference && reference.Name == "Celeste"), "frozen typed hook binds to static Celeste facade");
        Pass(frozenAssembly.MainModule.AssemblyReferences.All(reference => reference.Name != "Microsoft.Xna.Framework") &&
             frozenAssembly.MainModule.GetTypeReferences().Any(type => type.Namespace == "Microsoft.Xna.Framework" && type.Name == "Vector2" &&
                 type.Scope is AssemblyNameReference reference && reference.Name == "FNA"),
            "legacy strong-named FNA facade binds to the canonical FNA assembly for static AOT");
    }
    Pass(frozenAssemblyName == "BinarySupported", "frozen assembly identity recorded for generic AOT rooting");
    Pass(originalHash.Length == 64 && frozenHash.Length == 64 && originalHash != frozenHash, "original and frozen assembly hashes recorded");

    string gameplayRoot = GameplayFixture("GameplayRegistry");
    ModInput gameplayInput = SafeModIngestor.Ingest(gameplayRoot, NewDirectory("stage-gameplay-registry"), 0);
    ResolvedMod gameplayMod = CompatibilityAnalyzer.Analyze(gameplayInput, gameplayInput.Metadata[0]);
    AppleStaticDeclaration gameplayDeclaration = gameplayMod.Declaration!;
    Pass(gameplayDeclaration.CustomEntityFactories.Length == 4 &&
         gameplayDeclaration.CustomEntityFactories.Count(value => value.Kind == "entity") == 3 &&
         gameplayDeclaration.CustomEntityFactories.Count(value => value.Kind == "trigger") == 1,
        "Cecil custom entity and trigger discovery");
    Pass(gameplayDeclaration.CustomEntityFactories.Any(value => value.Id == "fixture/blockAlias") &&
         gameplayDeclaration.CustomEntityFactories.Select(value => value.Constructor).Distinct(StringComparer.Ordinal)
             .OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(
                 ["entity-data-vector2", "entity-data-vector2-entity-id", "entity-id-entity-data-vector2"]),
        "multi-ID attributes and all bounded constructor shapes");
    Pass(gameplayDeclaration.CustomBackdropFactories.Length == 1 &&
         gameplayDeclaration.CustomBackdropFactories[0].Id == "fixture/backdrop" &&
         gameplayDeclaration.CustomBackdropFactories[0].Factory == "static-method" &&
         gameplayDeclaration.CustomBackdropFactories[0].Method == "Build",
        "Cecil custom backdrop discovery and static factory selection");
    Pass(gameplayDeclaration.SettingsProperties.Length == 3 &&
         gameplayDeclaration.SettingsProperties.Any(value => value.Name == "Enabled" && value.Kind == "bool") &&
         gameplayDeclaration.SettingsProperties.Any(value => value.Name == "DisplayMode" && value.Kind == "enum" &&
             value.EnumValues.SequenceEqual([0, 2])) &&
         gameplayDeclaration.SettingsProperties.Any(value => value.Name == "Amount" && value.Kind == "int" &&
             value.Minimum == 1 && value.Maximum == 5),
        "bounded bool, enum and ranged-int settings discovery");
    Pass(gameplayDeclaration.OmittedSettingsProperties.SequenceEqual(["Unbounded:System.Int32"]),
        "unsupported unbounded setting is explicitly omitted");
    string invalidGameplayRoot = GameplayFixture("InvalidGameplay", invalidConstructor: true);
    ModInput invalidGameplayInput = SafeModIngestor.Ingest(invalidGameplayRoot,
        NewDirectory("stage-invalid-gameplay"), 0);
    ResolvedMod invalidGameplayMod = CompatibilityAnalyzer.Analyze(
        invalidGameplayInput, invalidGameplayInput.Metadata[0]);
    Pass(invalidGameplayMod.Declaration?.OmittedCustomEntityFactories.Length == 2 &&
         invalidGameplayMod.Declaration.OmittedCustomEntityFactories.All(item =>
             item.Reason == "runtime-only-constructor"),
        "runtime-created entity without a map factory is explicitly omitted before AOT");
    string duplicateGameplayRoot = GameplayFixture("DuplicateGameplay", duplicateId: true);
    ModInput duplicateGameplayInput = SafeModIngestor.Ingest(duplicateGameplayRoot,
        NewDirectory("stage-duplicate-gameplay"), 0);
    Throws(() => CompatibilityAnalyzer.Analyze(duplicateGameplayInput, duplicateGameplayInput.Metadata[0]),
        "duplicate custom entity factory ID", "duplicate custom entity ID rejected before AOT");

    RuntimeClosureScanner.VerifyPreserved(frozen, frozen);
    Pass(true, "complete external assembly preservation accepts intact methods");
    string stripped = Path.Combine(NewDirectory("stripped"), "BinarySupported.dll");
    File.Copy(frozen, stripped);
    using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(stripped, new ReaderParameters { ReadWrite = true }))
    {
        MethodDefinition constructor = assembly.MainModule.Types.Single(type => type.Name == "BinarySupportedModule")
            .Methods.Single(method => method.IsConstructor);
        constructor.Body.Instructions.Clear();
        assembly.Write();
    }
    Throws(() => RuntimeClosureScanner.VerifyPreserved(frozen, stripped), "lost executable methods",
        "trimmed external method body rejected");

    string directRoot = DirectBinaryFixture("DirectSupported", "supported");
    ModInput directInput = SafeModIngestor.Ingest(directRoot, NewDirectory("stage-direct-supported"), 0);
    ResolvedMod directMod = CompatibilityAnalyzer.Analyze(directInput, directInput.Metadata[0]);
    Pass(directMod.Classification == CompatibilityClass.DIRECT_HOOK_SUPPORTED && directMod.DirectManagedHooks.Count == 1,
        "precompiled direct Hook constructor accepted");
    DirectManagedHookPlan directPlan = directMod.DirectManagedHooks.Single();
    Pass(directPlan.TargetId == "celeste-player-die" && directPlan.Capture == "STATIC" &&
         directPlan.DetourType == "Fixture.DirectSupportedModule" && directPlan.DetourMethod == "OnPlayerDie",
        "direct Hook target and detour statically resolved");
    string frozenDirect = Path.Combine(NewDirectory("frozen-direct"), "DirectSupported.dll");
    _ = AssemblyFreezer.Freeze(Path.Combine(directInput.StagingRoot, "Code", "DirectSupported.dll"), frozenDirect,
        directMod.DirectManagedHooks);
    using (AssemblyDefinition frozenDirectAssembly = AssemblyDefinition.ReadAssembly(frozenDirect))
    {
        Pass(frozenDirectAssembly.MainModule.AssemblyReferences.All(reference => reference.Name != "MonoMod.RuntimeDetour"),
            "frozen direct Hook assembly removes desktop RuntimeDetour reference");
        MethodDefinition frozenConstructor = frozenDirectAssembly.MainModule.Types.Single(type => type.Name == "DirectSupportedModule")
            .Methods.Single(method => method.IsConstructor);
        MethodReference rewrittenConstructor = frozenConstructor.Body.Instructions.Select(instruction => instruction.Operand)
            .OfType<MethodReference>().Single(method => method.DeclaringType.FullName == "MonoMod.RuntimeDetour.Hook" && method.Name == ".ctor");
        Pass(rewrittenConstructor.Parameters.Count == 1 && rewrittenConstructor.Parameters[0].ParameterType.FullName == "System.String" &&
             rewrittenConstructor.DeclaringType.Scope is AssemblyNameReference reference && reference.Name == "Celeste" &&
             frozenConstructor.Body.Instructions.Any(instruction => instruction.OpCode == OpCodes.Ldstr &&
                 Equals(instruction.Operand, directPlan.PlanId)),
            "direct Hook construction lowered to a fixed plan ID and Apple static facade");
    }
    string mixedRoot = DirectBinaryFixture("MixedSupported", "mixed");
    ModInput mixedInput = SafeModIngestor.Ingest(mixedRoot, NewDirectory("stage-mixed-supported"), 0);
    ResolvedMod mixedMod = CompatibilityAnalyzer.Analyze(mixedInput, mixedInput.Metadata[0]);
    Pass(mixedMod.Classification == CompatibilityClass.MIXED_MANAGED_DETOURS_SUPPORTED &&
         mixedMod.ManagedDetourTargets.SetEquals(["celeste-player-die", "monocle-engine-update"]) &&
         mixedMod.DirectManagedHooks.Count == 1,
        "precompiled mixed HookGen/direct Hook module shares one managed-detour classification");
    foreach ((string mode, CompatibilityClass expected, string message) in new[]
    {
        ("dynamic-target", CompatibilityClass.DYNAMIC_TARGET_DEFERRED, "DEFERRED_DYNAMIC_TARGET"),
        ("wrong-binding-flags", CompatibilityClass.DYNAMIC_TARGET_DEFERRED, "DEFERRED_DYNAMIC_TARGET"),
        ("dynamic-detour", CompatibilityClass.DYNAMIC_DETOUR_DEFERRED, "DEFERRED_DYNAMIC_DETOUR"),
        ("config", CompatibilityClass.DETOUR_CONFIG_DEFERRED, "DEFERRED_DETOUR_CONFIG"),
        ("unsupported-member", CompatibilityClass.DIRECT_HOOK_DEFERRED, "unsupported direct Hook member")
    })
    {
        string root = DirectBinaryFixture("Direct" + mode.Replace("-", "", StringComparison.Ordinal), mode);
        ModInput input = SafeModIngestor.Ingest(root, NewDirectory("stage-direct-" + mode), 0);
        ResolvedMod audit = CompatibilityAnalyzer.Audit(input, input.Metadata[0]);
        Pass(audit.Classification == expected && audit.Mechanisms.Any(value => value.Contains(message, StringComparison.Ordinal)),
            mode + " direct Hook audit classification");
        Throws(() => CompatibilityAnalyzer.Analyze(input, input.Metadata[0]), message,
            mode + " direct Hook rejected before AOT");
    }

    string ApiContract(string directory, bool includeExpectedMethod, bool publicExpectedMethod = true)
    {
        string path = Path.Combine(NewDirectory(directory), "TargetApi.dll");
        using AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("TargetApi", new Version(1, 0, 0, 0)), "TargetApi", ModuleKind.Dll);
        TypeDefinition contract = new("Fixture", "Contract", TypeAttributes.Public | TypeAttributes.Abstract |
            TypeAttributes.Sealed, assembly.MainModule.TypeSystem.Object);
        if (includeExpectedMethod)
        {
            MethodDefinition expected = new("Expected", (publicExpectedMethod ? MethodAttributes.Public :
                    MethodAttributes.Private) | MethodAttributes.Static,
                assembly.MainModule.TypeSystem.Void);
            expected.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            contract.Methods.Add(expected);
        }
        assembly.MainModule.Types.Add(contract);
        assembly.Write(path);
        return path;
    }

    string goodApi = ApiContract("api-good", includeExpectedMethod: true);
    string badApi = ApiContract("api-bad", includeExpectedMethod: false);
    string inaccessibleApi = ApiContract("api-inaccessible", includeExpectedMethod: true,
        publicExpectedMethod: false);
    string apiConsumer = Path.Combine(NewDirectory("api-consumer"), "ApiConsumer.dll");
    using (AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
        new AssemblyNameDefinition("ApiConsumer", new Version(1, 0, 0, 0)), "ApiConsumer", ModuleKind.Dll))
    {
        AssemblyNameReference targetReference = new("TargetApi", new Version(1, 0, 0, 0));
        assembly.MainModule.AssemblyReferences.Add(targetReference);
        TypeDefinition consumer = new("Fixture", "Consumer", TypeAttributes.Public | TypeAttributes.Abstract |
            TypeAttributes.Sealed, assembly.MainModule.TypeSystem.Object);
        MethodDefinition probe = new("Probe", MethodAttributes.Public | MethodAttributes.Static,
            assembly.MainModule.TypeSystem.Void);
        TypeReference targetType = new("Fixture", "Contract", assembly.MainModule, targetReference);
        MethodReference targetMethod = new("Expected", assembly.MainModule.TypeSystem.Void, targetType)
        {
            HasThis = false
        };
        probe.Body.Instructions.Add(Instruction.Create(OpCodes.Call, targetMethod));
        probe.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        consumer.Methods.Add(probe);
        assembly.MainModule.Types.Add(consumer);
        assembly.Write(apiConsumer);
    }
    RuntimeClosureScanner.VerifyReferencedApi(apiConsumer, goodApi);
    Pass(true, "external assembly API closure accepts the complete target contract");
    Throws(() => RuntimeClosureScanner.VerifyReferencedApi(apiConsumer, badApi), "absent from the linked TargetApi contract",
        "external assembly API closure rejects a missing target method before device AOT");
    Throws(() => RuntimeClosureScanner.VerifyReferencedApi(apiConsumer, inaccessibleApi), "inaccessible-method",
        "external assembly API closure rejects a private target method before device AOT");

    Pass(AppleApiSurface.Members.Count == 7 && AppleApiSurface.ContractSha256.Length == 64,
        "exact reviewed Apple external API surface contract");
    string apiSurfaceRoot = NewDirectory("apple-api-surface");
    Text(apiSurfaceRoot, "Celeste/Level.cs",
        "namespace Celeste;\npublic class Level\n{\n\tprivate float unpauseTimer;\n\tprivate void StartPauseEffects() {}\n\tprivate void EndPauseEffects() {}\n}\n");
    Text(apiSurfaceRoot, "Celeste/Actor.cs",
        "namespace Celeste;\npublic class Actor\n{\n\tprivate Vector2 movementCounter;\n}\n");
    Text(apiSurfaceRoot, "Celeste/Glider.cs",
        "namespace Celeste;\npublic class Glider\n{\n\tprivate bool destroyed;\n\tprivate Sprite sprite;\n\tprivate IEnumerator DestroyAnimationRoutine() {}\n}\n");
    AppleApiSurface.Apply(apiSurfaceRoot);
    string apiSurfaceLevel = File.ReadAllText(Path.Combine(apiSurfaceRoot, "Celeste", "Level.cs"));
    Pass(apiSurfaceLevel.Contains("public float unpauseTimer", StringComparison.Ordinal) &&
         apiSurfaceLevel.Contains("public void StartPauseEffects()", StringComparison.Ordinal) &&
         apiSurfaceLevel.Contains("public void EndPauseEffects()", StringComparison.Ordinal),
        "exact reviewed Apple API surface is applied");
    string apiSurfaceActor = File.ReadAllText(Path.Combine(apiSurfaceRoot, "Celeste", "Actor.cs"));
    string apiSurfaceGlider = File.ReadAllText(Path.Combine(apiSurfaceRoot, "Celeste", "Glider.cs"));
    Pass(apiSurfaceActor.Contains("public Vector2 movementCounter", StringComparison.Ordinal) &&
         apiSurfaceGlider.Contains("public bool destroyed", StringComparison.Ordinal) &&
         apiSurfaceGlider.Contains("public Sprite sprite", StringComparison.Ordinal) &&
         apiSurfaceGlider.Contains("public IEnumerator DestroyAnimationRoutine()", StringComparison.Ordinal),
        "Cpop's exact pinned-Everest publicized members are reviewed and applied");
    Throws(() => AppleApiSurface.Apply(apiSurfaceRoot), "must occur exactly once",
        "Apple API surface rejects duplicate application");
    string unsupportedRoot = BinaryFixture("BinaryDeferred", "On.Celeste", "Player", "UnknownMethod");
    ModInput unsupportedInput = SafeModIngestor.Ingest(unsupportedRoot, NewDirectory("stage-binary-deferred"), 0);
    Throws(() => CompatibilityAnalyzer.Analyze(unsupportedInput, unsupportedInput.Metadata[0]), "ON_HOOK_DEFERRED", "unknown precompiled hook target rejected");
    string binaryIlRoot = BinaryFixture("BinaryIlDeferred", "IL.Celeste", "SummitCheckpoint");
    ModInput binaryIlInput = SafeModIngestor.Ingest(binaryIlRoot, NewDirectory("stage-binary-il-deferred"), 0);
    ResolvedMod binaryIl = CompatibilityAnalyzer.Audit(binaryIlInput, binaryIlInput.Metadata[0]);
    Pass(binaryIl.Classification == CompatibilityClass.IL_HOOK_DEFERRED &&
         binaryIl.Mechanisms.Contains("Code/BinaryIlDeferred.dll:il-hook:IL.Celeste.SummitCheckpoint"),
         "precompiled IL.* type detected from CLI metadata");
    Pass(EverestGraphResolver.Resolve([binaryMod]).Single().Metadata.Name == "BinarySupported", "pinned Everest platform dependency satisfied");

    string realLayout = NewDirectory("real-content-layout");
    Text(realLayout, "everest.yaml", "- Name: RealContent\n  Version: 1.0.0\n  Dependencies:\n    - Name: Everest\n      Version: 1.519.0\n");
    Text(realLayout, "Maps/Author/Map.bin", "map");
    Text(realLayout, "Dialog/English.txt", "author_map=Map");
    ModInput realContentInput = SafeModIngestor.Ingest(realLayout, NewDirectory("stage-real-content"), 0);
    ResolvedMod realContent = CompatibilityAnalyzer.Analyze(realContentInput, realContentInput.Metadata[0]);
    Pass(realContent.ContentFiles.SequenceEqual(["Dialog/English.txt", "Maps/Author/Map.bin"]), "ordinary Everest content roots accepted");

    string contentOut = NewDirectory("compiled-content");
    string asset = Path.Combine(repository, "apple-everest/canaries/content/Content/AppleEverest/Canary/banner.asset.json");
    string map = Path.Combine(repository, "apple-everest/canaries/content/Content/Maps/AppleEverest/Canary.xml");
    Pass(ContentCompiler.Stage(asset, "Content/AppleEverest/banner.asset.json", contentOut) == "AppleEverest/banner.png", "asset logical path");
    Pass(File.ReadAllBytes(Path.Combine(contentOut, "AppleEverest/banner.png")).Take(8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }), "generated PNG");
    Pass(ContentCompiler.Stage(map, "Content/Maps/AppleEverest/Canary.xml", contentOut) == "Maps/AppleEverest/Canary.bin", "map logical path");
    Pass(File.ReadAllBytes(Path.Combine(contentOut, "Maps/AppleEverest/Canary.bin")).Length > 64, "compiled map");
    string gameplayMap = Path.Combine(temporary, "gameplay-registry-map.xml");
    File.WriteAllText(gameplayMap,
        "<Map><levels><level><entities><fixtureBlock /></entities><triggers><fixtureTrigger /></triggers></level></levels>" +
        "<Style><Backgrounds><fixtureBackdrop /></Backgrounds><Foregrounds /></Style></Map>");
    string gameplayMapOutput = NewDirectory("gameplay-map-content");
    ContentCompiler.Stage(gameplayMap, "Content/Maps/Fixture/Registry.xml", gameplayMapOutput);
    Pass(ContentCompiler.InspectGameplayIds(Path.Combine(gameplayMapOutput, "Maps/Fixture/Registry.bin"))
            .SequenceEqual(new[] { ("backdrop", "fixtureBackdrop"), ("entity", "fixtureBlock"), ("trigger", "fixtureTrigger") }),
        "map metadata discovers custom entity, trigger and backdrop identifiers without loading code");
    string genericMap = Path.Combine(contentOut, "Maps/AppleEverest/Canary.bin");
    string normalizedOut = NewDirectory("normalized-map-content");
    Pass(ContentCompiler.Stage(genericMap, "Content/Maps/Author/RealMap.bin", normalizedOut) == "Maps/Author/RealMap.bin",
        "precompiled Everest map logical path");
    static (string Magic, string Package, byte[] Body) ReadMap(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
        string magic = reader.ReadString();
        string package = reader.ReadString();
        byte[] body = new byte[checked((int)(stream.Length - stream.Position))];
        stream.ReadExactly(body);
        return (magic, package, body);
    }
    (string sourceMagic, _, byte[] sourceBody) = ReadMap(genericMap);
    (string targetMagic, string targetPackage, byte[] targetBody) = ReadMap(Path.Combine(normalizedOut, "Maps/Author/RealMap.bin"));
    Pass(sourceMagic == "CELESTE MAP" && targetMagic == sourceMagic && targetPackage == "Author/RealMap" &&
         targetBody.SequenceEqual(sourceBody), "Everest map package is normalized without changing its binary body");
    string invalidMap = Path.Combine(temporary, "invalid-map.bin");
    File.WriteAllText(invalidMap, "not a Celeste map");
    Throws(() => ContentCompiler.Stage(invalidMap, "Content/Maps/Author/Invalid.bin", normalizedOut),
        "invalid Celeste header", "malformed precompiled map fails closed");
    string first = Path.Combine(temporary, "first.txt"), second = Path.Combine(temporary, "second.txt");
    File.WriteAllText(first, "first"); File.WriteAllText(second, "second");
    ContentCompiler.Stage(first, "Content/precedence.txt", contentOut);
    ContentCompiler.Stage(second, "Content/precedence.txt", contentOut);
    Pass(File.ReadAllText(Path.Combine(contentOut, "precedence.txt")) == "second", "later mount precedence");

    string profilePath = Path.Combine(repository, "apple-everest/profiles/stable-1.6458.0.json");
    using JsonDocument profile = JsonDocument.Parse(File.ReadAllBytes(profilePath));
    Pass(profile.RootElement.GetProperty("everest").GetProperty("sha256Commit").GetString() == "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00", "Everest pin");
    Pass(profile.RootElement.GetProperty("dependencies").GetProperty("monoModCommit").GetString() == "dfc30a1506d37fb88a2c2be004f525205f46a24c", "MonoMod pin");
    Pass(profile.RootElement.GetProperty("host").GetProperty("dotnetSdk").GetString() == "8.0.424", "host SDK pin");
    using (JsonDocument ecosystem = JsonDocument.Parse(File.ReadAllBytes(
        Path.Combine(repository, "apple-everest/helper-ecosystem-compatibility-stage25e.json"))))
    {
        JsonElement selected = ecosystem.RootElement.GetProperty("selected");
        Pass(ecosystem.RootElement.GetProperty("helperCandidates").GetArrayLength() >= 8 &&
             ecosystem.RootElement.GetProperty("mapCandidates").GetArrayLength() >= 12,
            "Stage 25E candidate audit breadth");
        Pass(selected.GetProperty("helper").GetProperty("name").GetString() == "CpopHelper" &&
             selected.GetProperty("helper").GetProperty("zipSha256").GetString() ==
                 "7a807a8f9ce6ccb4d6ad0c664bb7791a60734533fb33cfcd6beccb202d846b63" &&
             selected.GetProperty("map").GetProperty("name").GetString() == "QuizSample" &&
             selected.GetProperty("map").GetProperty("zipSha256").GetString() ==
                 "5cb8351bb263aa316831d2b683cb8b04acd270587edfe7c9e3df3bb8e6b83b3e",
            "selected ordinary helper and dependent-map release pins");
        Pass(selected.GetProperty("sourceTreesRequiredForProduction").GetBoolean() == false &&
             selected.GetProperty("redistributedThirdPartyBytes").GetBoolean() == false,
            "selected helper ecosystem is source-free and not redistributed");
    }
    using (JsonDocument targetCatalog = JsonDocument.Parse(File.ReadAllBytes(
        Path.Combine(repository, "apple-everest/managed-detour-targets-v2.json"))))
    {
        JsonElement targets = targetCatalog.RootElement.GetProperty("targets");
        Pass(targetCatalog.RootElement.GetProperty("schemaVersion").GetInt32() == 2 &&
             targets.GetArrayLength() == 51,
            "signature-driven managed-detour target catalog v2");
        string[] ids = targets.EnumerateArray().Select(target => target.GetProperty("id").GetString()!).ToArray();
        Pass(ids.Distinct(StringComparer.Ordinal).Count() == 51 &&
             ids.Contains("celeste-commands-cmd-ow-complete", StringComparer.Ordinal) &&
             ids.Contains("celeste-oui-chapter-select-enter", StringComparer.Ordinal) &&
             ids.Contains("celeste-area-mode-stats-clone", StringComparer.Ordinal) &&
             ids.Contains("celeste-save-data-add-death", StringComparer.Ordinal) &&
             ids.Contains("celeste-player-added", StringComparer.Ordinal) &&
             ids.Contains("monocle-entity-added", StringComparer.Ordinal),
            "B2 target catalog covers exact high-arity, IEnumerator, reference-return and inherited target shapes");
    }
    ManagedDetourTarget SyntheticTarget(string id, string eventName, bool isStatic, string returnType,
        params (string Type, string Name)[] parameters) => new()
    {
        Id = id,
        SourceFile = id + ".cs",
        SourceDeclaration = "public " + (isStatic ? "static " : "") + returnType + " " + eventName + "(" +
            string.Join(", ", parameters.Select(value => value.Type + " " + value.Name)) + ")",
        OriginalDeclaration = "private " + (isStatic ? "static " : "") + returnType + " Original_" + eventName + "(" +
            string.Join(", ", parameters.Select(value => value.Type + " " + value.Name)) + ")",
        OriginalAlias = "Original_" + eventName,
        HookNamespace = "On.Fixture",
        HookType = "SignatureMatrix",
        EventName = eventName,
        OrigDelegate = "orig_" + eventName,
        HookDelegate = "hook_" + eventName,
        IsStatic = isStatic,
        ReceiverType = isStatic ? null : "global::Fixture.SignatureMatrix",
        ReturnType = returnType,
        Parameters = parameters.Select(value => new ManagedDetourParameter { Type = value.Type, Name = value.Name }).ToArray()
    };
    ManagedDetourTarget[] signatureMatrix =
    [
        SyntheticTarget("fixture-static-void", "StaticVoid", true, "void"),
        SyntheticTarget("fixture-static-int", "StaticInt", true, "int", ("int", "value")),
        SyntheticTarget("fixture-instance-void", "InstanceVoid", false, "void"),
        SyntheticTarget("fixture-instance-return", "InstanceReturn", false, "int",
            ("int", "value"), ("global::Microsoft.Xna.Framework.Vector2", "position"), ("string", "label")),
        SyntheticTarget("fixture-high-arity", "HighArity", true, "void",
            ("int", "a"), ("int", "b"), ("int", "c"), ("int", "d"),
            ("int", "e"), ("int", "f"), ("int", "g"), ("int", "h")),
        SyntheticTarget("fixture-ienumerator-return", "EnumeratorReturn", false,
            "global::System.Collections.IEnumerator", ("int", "from")),
        SyntheticTarget("fixture-reference-return", "ReferenceReturn", false,
            "global::Fixture.SignatureMatrix", ("string", "label"))
    ];
    string signatureSource = ManagedDetourGenerator.DispatcherSource(signatureMatrix);
    Pass(signatureSource.Contains("delegate void orig_StaticVoid()", StringComparison.Ordinal) &&
         signatureSource.Contains("delegate int orig_StaticInt(int value)", StringComparison.Ordinal) &&
         signatureSource.Contains("delegate void orig_InstanceVoid(global::Fixture.SignatureMatrix self)", StringComparison.Ordinal) &&
         signatureSource.Contains("delegate int orig_InstanceReturn(global::Fixture.SignatureMatrix self, int value, global::Microsoft.Xna.Framework.Vector2 position, string label)", StringComparison.Ordinal) &&
         signatureSource.Contains("delegate void orig_HighArity(int a, int b, int c, int d, int e, int f, int g, int h)", StringComparison.Ordinal) &&
         signatureSource.Contains("delegate global::System.Collections.IEnumerator orig_EnumeratorReturn(global::Fixture.SignatureMatrix self, int from)", StringComparison.Ordinal) &&
         signatureSource.Contains("delegate global::Fixture.SignatureMatrix orig_ReferenceReturn(global::Fixture.SignatureMatrix self, string label)", StringComparison.Ordinal),
        "signature generator covers static/instance, high arity, IEnumerator/reference return, struct/reference and multiple arguments");
    Pass(!signatureSource.Contains("DynamicInvoke", StringComparison.Ordinal) &&
         !signatureSource.Contains("object[]", StringComparison.Ordinal) &&
         signatureSource.Contains("AppleEverestHookList.Version", StringComparison.Ordinal),
        "generated signature matrix remains strongly typed and version-cached");
    string signatureRoot = NewDirectory("signature-rewrite");
    foreach (ManagedDetourTarget target in signatureMatrix)
        Text(signatureRoot, target.SourceFile,
            "namespace Fixture; public class SignatureMatrix\n{\n\t" + target.SourceDeclaration + "\n\t{\n\t\t" +
            (target.ReturnType == "void" ? "return;" : "return 0;") + "\n\t}\n}\n");
    ManagedDetourGenerator.RewriteTargets(signatureRoot, signatureMatrix);
    Pass(signatureMatrix.All(target => File.ReadAllText(Path.Combine(signatureRoot, target.SourceFile))
            .Contains(target.OriginalDeclaration, StringComparison.Ordinal)),
        "signature generator rewrites each synthetic target into one wrapper and one original body");
    DirectManagedHookPlan directEvidencePlan = new(
        "fixture:direct-evidence", "Fixture", "Fixture", "Fixture.DirectEvidence::.ctor", 0,
        "fixture-instance-return", "InstanceReturn", "Fixture.DirectEvidence", "Apply", true, "int",
        [
            "global::System.Func<global::Fixture.SignatureMatrix, int, global::Microsoft.Xna.Framework.Vector2, string, int>",
            "global::Fixture.SignatureMatrix", "int", "global::Microsoft.Xna.Framework.Vector2", "string"
        ], "STATIC", "System.Reflection.MethodBase,System.Reflection.MethodInfo");
    string directEvidenceSource = ManagedDetourGenerator.DirectRegistrySource(
        [directEvidencePlan], signatureMatrix.ToDictionary(target => target.Id, StringComparer.Ordinal));
    Pass(directEvidenceSource.Contains("RecordDirectHookInvocation(\"fixture:direct-evidence\")", StringComparison.Ordinal),
        "generated direct adapter records one bounded device invocation proof");
    string staticRuntime = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/AppleEverestStaticRuntime.cs"));
    Pass(staticRuntime.Contains("SaveData.InitializeDebugMode(loadExisting: false)", StringComparison.Ordinal) &&
         staticRuntime.Contains("saveDataBeforeModSession = SaveData.Instance", StringComparison.Ordinal) &&
         staticRuntime.Contains("SaveData.Instance = saveDataBeforeModSession", StringComparison.Ordinal),
        "content maps use an isolated debug context and restore any prior player save");
    Pass(staticRuntime.Contains("FilterVanillaFileSave", StringComparison.Ordinal) &&
         staticRuntime.Contains("mod-session-save=suppressed reason=nonpersistent", StringComparison.Ordinal) &&
         staticRuntime.Contains("CompleteNonPersistentModSession", StringComparison.Ordinal) &&
         staticRuntime.Contains("return Overworld.StartMode.MainMenu", StringComparison.Ordinal) &&
         staticRuntime.Contains("requested={requestedStartMode} applied={Overworld.StartMode.MainMenu}", StringComparison.Ordinal),
        "nonpersistent mod maps suppress temporary file writes and return through the null-safe main menu boundary");
    Pass(staticRuntime.Contains("Input.MenuConfirm.ConsumePress()", StringComparison.Ordinal) &&
         staticRuntime.Contains("Input.Jump.ConsumePress()", StringComparison.Ordinal),
        "content canary consumes its launch edge");
    using (JsonDocument declaration = JsonDocument.Parse(File.ReadAllBytes(
        Path.Combine(repository, "apple-everest/canaries/module-a/apple-static.json"))))
    {
        Pass(declaration.RootElement.GetProperty("trackedEntityTypes")[0].GetString() ==
             "Celeste.Mod.AppleEverestCanaryBanner", "canary tracked entity is explicitly declared");
    }
    string closureGenerator = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/ClosureGenerator.cs"));
    Pass(closureGenerator.Contains("GeneratedAppleEverestGameplayRegistry", StringComparison.Ordinal) &&
         closureGenerator.Contains("RegisterTrackerTypes", StringComparison.Ordinal),
        "typed static gameplay registry is generated and installed into Tracker initialization");
    Pass(closureGenerator.Contains("InheritedTrackedEntityTypes", StringComparison.Ordinal) &&
         closureGenerator.Contains("typeof(global::Celeste.Trigger)", StringComparison.Ordinal) &&
         closureGenerator.Contains("inheritedBase.IsAssignableFrom(type)", StringComparison.Ordinal) &&
         closureGenerator.Contains("Tracker.TrackedEntityTypes.Add(type, trackedAs)", StringComparison.Ordinal),
        "external custom entities preserve canonical inherited Monocle tracker buckets");
    Pass(closureGenerator.Contains("TryCreateEntity", StringComparison.Ordinal) &&
         closureGenerator.Contains("TryCreateTrigger", StringComparison.Ordinal) &&
         closureGenerator.Contains("TryCreateBackdrop", StringComparison.Ordinal) &&
         closureGenerator.Contains("if (mod.DeclaredAssemblyPath == null)", StringComparison.Ordinal),
        "generated entity, trigger and backdrop factories are reflection-free and binary modules remain source-free");
    Pass(closureGenerator.Contains("everest/coreMessage", StringComparison.Ordinal) &&
         closureGenerator.Contains("EverestCore", StringComparison.Ordinal) &&
         File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/EverestCoreEntitiesStaticApi.cs"))
             .Contains("class CustomCoreMessage", StringComparison.Ordinal),
        "pinned Everest core message entity has an explicit typed AOT factory");
    string tagsApi = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/EverestTagsStaticApi.cs"));
    Pass(tagsApi.Contains("readonly BitTag SubHUD = Tags.HUD", StringComparison.Ordinal),
        "static Apple SubHUD facade retains the helper's high-resolution coordinate space");
    Pass(closureGenerator.Contains("internal static readonly string[] MapPaths", StringComparison.Ordinal) &&
         staticRuntime.Contains("Play Static Mod Map:", StringComparison.Ordinal) &&
         staticRuntime.Contains("LaunchModMap(selectedMap)", StringComparison.Ordinal),
        "all staged maps are exposed through the generic static map launcher");
    Pass(closureGenerator.Contains("AppleEverestAtlasMountDescriptor", StringComparison.Ordinal) &&
         closureGenerator.Contains("Graphics/Atlases/Gameplay/", StringComparison.Ordinal) &&
         closureGenerator.Contains("Graphics/Atlases/Gui/", StringComparison.Ordinal) &&
         closureGenerator.Contains("ThenBy(value => value.SourcePath", StringComparison.Ordinal),
        "ordinary release PNGs generate dependency-ordered gameplay and GUI atlas mounts");
    Pass(closureGenerator.Contains("PatchNonPersistentSave(Path.Combine(managedRoot, \"Celeste\", \"UserIO.cs\"))", StringComparison.Ordinal) &&
         closureGenerator.Contains("FilterVanillaFileSave(file)", StringComparison.Ordinal) &&
         closureGenerator.Contains("PatchNonPersistentOverworldReturn", StringComparison.Ordinal) &&
         closureGenerator.Contains("StartMode = global::Celeste.Mod.AppleEverestStaticRuntime.CompleteNonPersistentModSession(StartMode);", StringComparison.Ordinal),
        "locked generated-source transforms keep debug SaveData out of durable storage and normalize its overworld return");
    Pass(staticRuntime.Contains("MountStaticAtlases();", StringComparison.Ordinal) &&
         staticRuntime.Contains("VirtualContent.CreateTexture(descriptor.LogicalPath)", StringComparison.Ordinal) &&
         staticRuntime.Contains("atlas[descriptor.Key] = mounted", StringComparison.Ordinal) &&
         staticRuntime.Contains("content-atlas=PASS", StringComparison.Ordinal),
        "static atlas mounts enter the live Celeste atlas without filesystem or type discovery");
    Pass(closureGenerator.Contains("typeof(global::", StringComparison.Ordinal),
        "generated AOT roots use namespace-unambiguous global type references");
    Pass(closureGenerator.Contains("AppleEverestExternalAssemblyRoots.props", StringComparison.Ordinal) &&
         closureGenerator.Contains("TrimmerRootAssembly", StringComparison.Ordinal),
        "external assembly identities generate complete trimmer roots");
    Pass(closureGenerator.Contains("ButtonBindingProperties", StringComparison.Ordinal) &&
         closureGenerator.Contains("new global::Celeste.Mod.ButtonBinding()", StringComparison.Ordinal) &&
         closureGenerator.Contains("InputBindingInitializer", StringComparison.Ordinal) &&
         closureGenerator.Contains("InitializeCurrentInput", StringComparison.Ordinal),
        "precompiled settings button bindings receive reflection-free post-input initialization");
    string iosHostProject = File.ReadAllText(Path.Combine(repository, "modern-ios/CelesteIOSRuntimeHost/CelesteIOSRuntimeHost.csproj"));
    string tvosHostProject = File.ReadAllText(Path.Combine(repository, "tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj"));
    Pass(iosHostProject.Contains("AppleEverestExternalAssemblyRoots.props", StringComparison.Ordinal) &&
         tvosHostProject.Contains("AppleEverestExternalAssemblyRoots.props", StringComparison.Ordinal),
        "both Apple hosts import generated external assembly roots");
    string loggerApi = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/EverestLoggerStaticApi.cs"));
    Pass(loggerApi.Contains("LogInterpolatedStringHandler<TLevel>", StringComparison.Ordinal) &&
         loggerApi.Contains("DefaultInterpolatedStringHandler", StringComparison.Ordinal) &&
         loggerApi.Contains("LogLevelConstTypes", StringComparison.Ordinal),
        "binary-compatible Everest interpolated logger API is AOT-safe");
    string runtimeApi = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/EverestStaticApi.cs"));
    Pass(runtimeApi.Contains("public static class Content", StringComparison.Ordinal) &&
         runtimeApi.Contains("public static readonly List<ModContent> Mods", StringComparison.Ordinal) &&
         runtimeApi.Contains("public EverestModuleMetadata Mod;", StringComparison.Ordinal) &&
         runtimeApi.Contains("public virtual EverestModuleSettings _Settings { get; set; }", StringComparison.Ordinal) &&
         runtimeApi.Contains("public virtual EverestModuleSaveData _SaveData { get; set; }", StringComparison.Ordinal) &&
         runtimeApi.Contains("public virtual EverestModuleSession _Session { get; set; }", StringComparison.Ordinal) &&
         runtimeApi.Contains("public virtual bool SaveDataAsync { get; set; } = true;", StringComparison.Ordinal) &&
         runtimeApi.Contains("public static void SetLogLevel(string tag, LogLevel level)", StringComparison.Ordinal) &&
         runtimeApi.Contains("public static void Log(string tag, string value)", StringComparison.Ordinal),
        "binary-compatible Everest content and module-state facades preserve compiled accessor shapes");
    Pass(runtimeApi.Contains("public sealed class ButtonBinding", StringComparison.Ordinal) &&
         runtimeApi.Contains("public VirtualButton Button", StringComparison.Ordinal) &&
         runtimeApi.Contains("internal void InitializeCurrentInput()", StringComparison.Ordinal) &&
         runtimeApi.Contains("public bool Pressed => Button?.Pressed ?? false", StringComparison.Ordinal),
        "bounded ButtonBinding facade preserves the real default shortcut binding ABI and lifecycle");
    string settingsPersistence = File.ReadAllText(Path.Combine(repository,
        "apple-everest/runtime/AppleEverestSettingsPersistence.cs"));
    Pass(settingsPersistence.Contains("CelesteAppleEverest.Settings.v1", StringComparison.Ordinal) &&
         settingsPersistence.Contains("AppleEverest/ModuleSettings.v1", StringComparison.Ordinal) &&
         settingsPersistence.Contains("ApplicationSupportDirectory", StringComparison.Ordinal) &&
         settingsPersistence.Contains("#if TVOS", StringComparison.Ordinal) &&
         !settingsPersistence.Contains("#if TVOS_CELESTE_RUNTIME_HOST", StringComparison.Ordinal) &&
         settingsPersistence.Contains("defaults.Synchronize()", StringComparison.Ordinal),
        "module settings use separate bounded iOS Application Support and tvOS defaults adapters");
    Pass(settingsPersistence.Contains("if (!descriptor.Accepts(value))", StringComparison.Ordinal) &&
         settingsPersistence.Contains("module-settings=corrupt action=defaults", StringComparison.Ordinal),
        "module settings fail safely on invalid values or corrupt storage");
    Pass(!settingsPersistence.Contains("SaveData", StringComparison.Ordinal) &&
         !settingsPersistence.Contains("settings.celeste", StringComparison.Ordinal),
        "module settings remain isolated from vanilla Settings and SaveData");
    string closureScanner = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/RuntimeClosureScanner.cs"));
    Pass(closureScanner.Contains("AllowedStaticFacadeType", StringComparison.Ordinal) &&
         closureScanner.Contains("AllowedStaticFacadeCall", StringComparison.Ordinal) &&
         closureScanner.Contains("type.Name is \"Hook\" or \"DetourConfig\"", StringComparison.Ordinal),
        "post-link scanner permits only the exact data-only RuntimeDetour facade types");
    Pass(!Directory.EnumerateFiles(Path.Combine(repository, "apple-everest/runtime"), "*.cs").Select(File.ReadAllText)
        .Any(text => text.Contains("DynamicInvoke", StringComparison.Ordinal) || text.Contains("Assembly.Load", StringComparison.Ordinal) ||
                     text.Contains("DynamicMethod", StringComparison.Ordinal) || text.Contains("Reflection.Emit", StringComparison.Ordinal) ||
                     text.Contains("NativeDetour", StringComparison.Ordinal)), "runtime forbidden executable mutation APIs absent");
    Pass(RuntimeClosureScanner.Inspect(typeof(ResolvedMod).Assembly.Location)
        .Any(value => value.Contains("System.Diagnostics.Process::Start", StringComparison.Ordinal)),
        "linked-runtime scanner detects a real forbidden API in the host-only builder");
    IReadOnlyList<string> testClosureViolations = RuntimeClosureScanner.Inspect(
        System.Reflection.Assembly.GetExecutingAssembly().Location);
    Pass(testClosureViolations.Count == 1 &&
         testClosureViolations[0].Contains("System.Diagnostics.Process::Start", StringComparison.Ordinal),
        "linked-runtime scanner isolates the intentional desktop static-plan test host spawn");

    Pass(ProductPolicy.TransformerVersion == "apple-everest-static-v7", "real-ZIP transformer version");
    Pass(File.Exists(Path.Combine(repository, "tools/AppleEverestBuilder/AssemblyFreezer.cs")),
        "binary-first assembly freezer exists");
    string models = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/Models.cs"));
    Pass(models.Contains("OriginalSha256", StringComparison.Ordinal) && models.Contains("FrozenSha256", StringComparison.Ordinal),
        "original and transformed binary provenance model");
    string analyzerSource = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs"));
    Pass(analyzerSource.Contains("ManagedDetourCatalog.RequireByHookType", StringComparison.Ordinal) &&
         analyzerSource.Contains("ResolveDirectHookPlan", StringComparison.Ordinal),
        "generic HookGen and direct-Hook analysis uses the reviewed target catalog");
    string detourGenerator = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/ManagedDetourGenerator.cs"));
    Pass(detourGenerator.Contains("DispatcherSource", StringComparison.Ordinal) &&
         detourGenerator.Contains("DirectRegistrySource", StringComparison.Ordinal) &&
         detourGenerator.Contains("AppleEverestHookList.Version", StringComparison.Ordinal),
        "signature-driven generated dispatchers share a version-cached backend");
    Pass(!File.Exists(Path.Combine(repository, "apple-everest/runtime/OnDialogStaticDispatch.cs")) &&
         !File.Exists(Path.Combine(repository, "apple-everest/runtime/OnParticleStaticDispatch.cs")) &&
         !File.Exists(Path.Combine(repository, "apple-everest/runtime/OnTrailStaticDispatch.cs")),
        "bespoke target dispatch files removed");
    string programSource = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/Program.cs"));
    Pass(programSource.Contains("case \"audit\"", StringComparison.Ordinal) &&
         programSource.Contains("transformerVersion", StringComparison.Ordinal), "deterministic compatibility-report command");
    Pass(programSource.Contains("verify-referenced-api", StringComparison.Ordinal) &&
         programSource.Contains("verify-aot-object", StringComparison.Ordinal),
        "external binary API and native AOT body verification commands");
    Pass(File.Exists(Path.Combine(repository, "tools/AppleEverestBuilder/StaticAssetGenerator.cs")) &&
         closureGenerator.Contains("GeneratedAppleEverestStaticAssets.cs", StringComparison.Ordinal) &&
         runtimeApi.Contains("GeneratedAppleEverestStaticAssets.TryDeserialize", StringComparison.Ordinal),
        "closed typed YAML factories replace runtime YamlDotNet/reflection");
    Pass(closureGenerator.Contains("public List<Item> Items => items", StringComparison.Ordinal) &&
         closureGenerator.Contains("public SubHeader(string title) : this(title, true)", StringComparison.Ordinal) &&
         closureGenerator.Contains("ConditionHelper+AchievementHelper-reviewed-members:v2", StringComparison.Ordinal),
        "pinned AchievementHelper TextMenu public ABI is explicit and bounded");
    Pass(runtimeApi.Contains("SettingSubTextAttribute", StringComparison.Ordinal) &&
         runtimeApi.Contains("SettingRangeAttribute", StringComparison.Ordinal) &&
         runtimeApi.Contains("DefaultButtonBindingAttribute", StringComparison.Ordinal),
        "metadata-only settings attributes keep accepted frozen helper DLLs linkable");
    string modInteropGenerator = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/ModInteropPlanner.cs"));
    Pass(modInteropGenerator.Contains("ReportStatus", StringComparison.Ordinal) &&
         staticRuntime.Contains("RecordModInteropExportInvocation", StringComparison.Ordinal) &&
         staticRuntime.Contains("RecordModInteropBinding", StringComparison.Ordinal),
        "bounded ModInterop diagnostics prove bindings and first real export invocations");
    string buildScript = File.ReadAllText(Path.Combine(repository, "scripts/build-apple-everest-canary.sh"));
    Pass(buildScript.Contains("verify-referenced-api", StringComparison.Ordinal) &&
         buildScript.Contains("verify-aot-object", StringComparison.Ordinal) &&
         buildScript.Contains(".dll.llvm.o", StringComparison.Ordinal) &&
         buildScript.Contains("mono_object", StringComparison.Ordinal),
        "product gate checks both LLVM and companion Mono AOT objects");
    Pass(File.Exists(Path.Combine(repository, "scripts/build-apple-everest-real-mods.sh")) &&
         File.Exists(Path.Combine(repository, "scripts/audit-apple-everest-mods.sh")),
        "ordinary-ZIP internal build and audit entry points");

    Console.WriteLine($"PASS: AppleEverestBuilder deterministic tests ({passed})");
}
finally
{
    if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
}
