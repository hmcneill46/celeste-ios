using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AppleEverestBuilder;
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
        ManagedFiles = [], ContentFiles = []
    };
}

try
{
    EverestVersion required = EverestVersion.Parse("1.2.3.4");
    Pass(EverestVersion.Satisfies(required, EverestVersion.Parse("1.2.3.4")), "exact version");
    Pass(EverestVersion.Satisfies(required, EverestVersion.Parse("1.3.0")), "newer minor");
    Pass(!EverestVersion.Satisfies(required, EverestVersion.Parse("2.2.3.4")), "major mismatch");
    Pass(!EverestVersion.Satisfies(required, EverestVersion.Parse("1.2.2.9")), "older build");
    Pass(EverestVersion.Satisfies(required, EverestVersion.Parse("0.0.1")), "Everest dev version");
    Throws(() => EverestVersion.Parse("1.bad"), "invalid Everest version", "invalid version rejected");

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
    Throws(() => AnalyzeSource("OtherOn", "// On.Celeste.Level.LoadLevel += handler\nclass Hook {}"), "ON_HOOK_DEFERRED", "unsupported On target");
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

    string BinaryFixture(string name, string hookNamespace, string hookType)
    {
        string root = NewDirectory("binary-" + name);
        Text(root, "everest.yaml", $"- Name: {name}\n  Version: 1.0.0\n  DLL: Code/{name}.dll\n  Dependencies:\n    - Name: Everest\n      Version: 1.6418.0\n");
        string path = Path.Combine(root, "Code", name + ".dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition(name, new Version(1, 0, 0, 0)), name, ModuleKind.Dll);
        AssemblyNameReference celeste = new("Celeste", new Version(1, 0, 0, 0));
        AssemblyNameReference hooks = new("MMHOOK_Celeste", new Version(0, 0, 0, 0));
        assembly.MainModule.AssemblyReferences.Add(celeste);
        assembly.MainModule.AssemblyReferences.Add(hooks);
        TypeDefinition module = new("Fixture", name + "Module", TypeAttributes.Public | TypeAttributes.Sealed,
            new TypeReference("Celeste.Mod", "EverestModule", assembly.MainModule, celeste));
        MethodDefinition constructor = new(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            assembly.MainModule.TypeSystem.Void);
        constructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        module.Methods.Add(constructor);
        module.Fields.Add(new FieldDefinition("HookRoot", FieldAttributes.Public | FieldAttributes.Static,
            new TypeReference(hookNamespace, hookType, assembly.MainModule, hooks)));
        assembly.MainModule.Types.Add(module);
        assembly.Write(path);
        return root;
    }

    string binaryRoot = BinaryFixture("BinarySupported", "On.Monocle", "ParticleSystem");
    ModInput binaryInput = SafeModIngestor.Ingest(binaryRoot, NewDirectory("stage-binary-supported"), 0);
    ResolvedMod binaryMod = CompatibilityAnalyzer.Analyze(binaryInput, binaryInput.Metadata[0]);
    Pass(binaryMod.Classification == CompatibilityClass.ON_HOOK_SUPPORTED, "precompiled typed hook accepted");
    Pass(binaryMod.Declaration?.ModuleType == "Fixture.BinarySupportedModule", "precompiled module factory inferred");
    Pass(binaryMod.ManagedFiles.SequenceEqual(["Code/BinarySupported.dll"]), "precompiled DLL accepted without source");
    string frozen = Path.Combine(NewDirectory("frozen"), "BinarySupported.dll");
    (string frozenAssemblyName, string originalHash, string frozenHash) = AssemblyFreezer.Freeze(
        Path.Combine(binaryInput.StagingRoot, "Code", "BinarySupported.dll"), frozen);
    using (AssemblyDefinition frozenAssembly = AssemblyDefinition.ReadAssembly(frozen))
    {
        Pass(frozenAssembly.MainModule.AssemblyReferences.All(reference => reference.Name != "MMHOOK_Celeste"), "frozen assembly removes HookGen runtime reference");
        Pass(frozenAssembly.MainModule.GetTypeReferences().Any(type => type.Namespace == "On.Monocle" && type.Name == "ParticleSystem" &&
             type.Scope is AssemblyNameReference reference && reference.Name == "Celeste"), "frozen typed hook binds to static Celeste facade");
    }
    Pass(frozenAssemblyName == "BinarySupported", "frozen assembly identity recorded for generic AOT rooting");
    Pass(originalHash.Length == 64 && frozenHash.Length == 64 && originalHash != frozenHash, "original and frozen assembly hashes recorded");
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

    string ApiContract(string directory, bool includeExpectedMethod)
    {
        string path = Path.Combine(NewDirectory(directory), "TargetApi.dll");
        using AssemblyDefinition assembly = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("TargetApi", new Version(1, 0, 0, 0)), "TargetApi", ModuleKind.Dll);
        TypeDefinition contract = new("Fixture", "Contract", TypeAttributes.Public | TypeAttributes.Abstract |
            TypeAttributes.Sealed, assembly.MainModule.TypeSystem.Object);
        if (includeExpectedMethod)
        {
            MethodDefinition expected = new("Expected", MethodAttributes.Public | MethodAttributes.Static,
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
    string unsupportedRoot = BinaryFixture("BinaryDeferred", "On.Celeste", "Player");
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
    Pass(File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/OnDialogStaticDispatch.cs")).Contains("orig_Clean chain", StringComparison.Ordinal), "typed production dispatcher");
    string staticRuntime = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/AppleEverestStaticRuntime.cs"));
    Pass(staticRuntime.Contains("SaveData.InitializeDebugMode(loadExisting: false)", StringComparison.Ordinal),
        "content canary provides an isolated save context before file selection");
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
    Pass(closureGenerator.Contains("typeof(global::", StringComparison.Ordinal),
        "generated AOT roots use namespace-unambiguous global type references");
    Pass(closureGenerator.Contains("AppleEverestExternalAssemblyRoots.props", StringComparison.Ordinal) &&
         closureGenerator.Contains("TrimmerRootAssembly", StringComparison.Ordinal),
        "external assembly identities generate complete trimmer roots");
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
         runtimeApi.Contains("public EverestModuleMetadata Mod;", StringComparison.Ordinal),
        "binary-compatible Everest content facade preserves nested types and fields");
    Pass(!Directory.EnumerateFiles(Path.Combine(repository, "apple-everest/runtime"), "*.cs").Select(File.ReadAllText)
        .Any(text => text.Contains("DynamicInvoke", StringComparison.Ordinal) || text.Contains("Assembly.Load", StringComparison.Ordinal) || text.Contains("RuntimeDetour", StringComparison.Ordinal)), "runtime forbidden APIs absent");
    Pass(RuntimeClosureScanner.Inspect(typeof(ResolvedMod).Assembly.Location)
        .Any(value => value.Contains("System.Diagnostics.Process::Start", StringComparison.Ordinal)),
        "linked-runtime scanner detects a real forbidden API in the host-only builder");
    Pass(RuntimeClosureScanner.Inspect(System.Reflection.Assembly.GetExecutingAssembly().Location).Count == 0,
        "linked-runtime scanner accepts the deterministic test closure");

    Pass(ProductPolicy.TransformerVersion == "apple-everest-static-v2", "real-ZIP transformer version");
    Pass(File.Exists(Path.Combine(repository, "tools/AppleEverestBuilder/AssemblyFreezer.cs")),
        "binary-first assembly freezer exists");
    string models = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/Models.cs"));
    Pass(models.Contains("OriginalSha256", StringComparison.Ordinal) && models.Contains("FrozenSha256", StringComparison.Ordinal),
        "original and transformed binary provenance model");
    string analyzerSource = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/CompatibilityAnalyzer.cs"));
    Pass(analyzerSource.Contains("On.Monocle.ParticleSystem", StringComparison.Ordinal) &&
         analyzerSource.Contains("On.Celeste.TrailManager", StringComparison.Ordinal),
        "bounded real HookGen target catalog");
    string particleDispatch = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/OnParticleStaticDispatch.cs"));
    Pass(particleDispatch.Contains("AppleEverestHookList.Version", StringComparison.Ordinal) &&
         particleDispatch.Contains("activeE", StringComparison.Ordinal), "hot particle hook chains are version-cached");
    string trailDispatch = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/OnTrailStaticDispatch.cs"));
    Pass(trailDispatch.Contains("activeVersion", StringComparison.Ordinal) &&
         trailDispatch.Contains("activeHandler", StringComparison.Ordinal), "trail hook chain is version-cached");
    string programSource = File.ReadAllText(Path.Combine(repository, "tools/AppleEverestBuilder/Program.cs"));
    Pass(programSource.Contains("case \"audit\"", StringComparison.Ordinal) &&
         programSource.Contains("transformerVersion", StringComparison.Ordinal), "deterministic compatibility-report command");
    Pass(programSource.Contains("verify-referenced-api", StringComparison.Ordinal) &&
         programSource.Contains("verify-aot-object", StringComparison.Ordinal),
        "external binary API and native AOT body verification commands");
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
