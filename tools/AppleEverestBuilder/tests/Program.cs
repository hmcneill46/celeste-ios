using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AppleEverestBuilder;

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

    string contentOut = NewDirectory("compiled-content");
    string asset = Path.Combine(repository, "apple-everest/canaries/content/Content/AppleEverest/Canary/banner.asset.json");
    string map = Path.Combine(repository, "apple-everest/canaries/content/Content/Maps/AppleEverest/Canary.xml");
    Pass(ContentCompiler.Stage(asset, "Content/AppleEverest/banner.asset.json", contentOut) == "AppleEverest/banner.png", "asset logical path");
    Pass(File.ReadAllBytes(Path.Combine(contentOut, "AppleEverest/banner.png")).Take(8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }), "generated PNG");
    Pass(ContentCompiler.Stage(map, "Content/Maps/AppleEverest/Canary.xml", contentOut) == "Maps/AppleEverest/Canary.bin", "map logical path");
    Pass(File.ReadAllBytes(Path.Combine(contentOut, "Maps/AppleEverest/Canary.bin")).Length > 64, "compiled map");
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
    Pass(!Directory.EnumerateFiles(Path.Combine(repository, "apple-everest/runtime"), "*.cs").Select(File.ReadAllText)
        .Any(text => text.Contains("DynamicInvoke", StringComparison.Ordinal) || text.Contains("Assembly.Load", StringComparison.Ordinal) || text.Contains("RuntimeDetour", StringComparison.Ordinal)), "runtime forbidden APIs absent");
    Pass(RuntimeClosureScanner.Inspect(typeof(ResolvedMod).Assembly.Location)
        .Any(value => value.Contains("System.Diagnostics.Process::Start", StringComparison.Ordinal)),
        "linked-runtime scanner detects a real forbidden API in the host-only builder");
    Pass(RuntimeClosureScanner.Inspect(System.Reflection.Assembly.GetExecutingAssembly().Location).Count == 0,
        "linked-runtime scanner accepts the deterministic test closure");

    Console.WriteLine($"PASS: AppleEverestBuilder deterministic tests ({passed})");
}
finally
{
    if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
}
