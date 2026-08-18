using System.Diagnostics;
using System.Text.Json;

namespace AppleEverestBuilder;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help") { Help(); return 0; }
            Dictionary<string, List<string>> options = Parse(args.Skip(1).ToArray());
            switch (args[0])
            {
                case "acquire": Acquire(One(options, "--profile"), One(options, "--output")); break;
                case "build": Build(One(options, "--profile"), One(options, "--repo-root"), One(options, "--upstream"), One(options, "--output"), Many(options, "--mod")); break;
                case "apply": ClosureGenerator.Apply(One(options, "--closure"), One(options, "--managed-root")); break;
                case "scan-runtime": RuntimeClosureScanner.Verify(One(options, "--assembly")); break;
                case "verify-profile": _ = LoadProfile(One(options, "--profile")); break;
                default: throw new InvalidDataException($"unknown command: {args[0]}");
            }
            Console.WriteLine($"PASS: AppleEverestBuilder {args[0]}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }

    private static void Build(string profilePath, string repoRoot, string upstream, string output, IReadOnlyList<string> modPaths)
    {
        AppleEverestProfile profile = LoadProfile(profilePath);
        VerifyUpstream(profile, upstream);
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new InvalidDataException("closure output already exists");
        if (modPaths.Count == 0) throw new InvalidDataException("at least one explicit mod input is required");
        Directory.CreateDirectory(output);
        string staging = Path.Combine(output, ".staging");
        Directory.CreateDirectory(staging);
        List<ResolvedMod> analyzed = [];
        for (int index = 0; index < modPaths.Count; index++)
        {
            ModInput input = SafeModIngestor.Ingest(modPaths[index], staging, index);
            analyzed.AddRange(input.Metadata.Select(metadata => CompatibilityAnalyzer.Analyze(input, metadata)));
        }
        IReadOnlyList<ResolvedMod> ordered = EverestGraphResolver.Resolve(analyzed);
        ClosureGenerator.Generate(profile, ordered, Path.GetFullPath(repoRoot), output);
        Directory.Delete(staging, recursive: true);
    }

    private static void Acquire(string profilePath, string output)
    {
        AppleEverestProfile profile = LoadProfile(profilePath);
        output = Path.GetFullPath(output);
        if (!Directory.Exists(Path.Combine(output, ".git")))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            Run("git", "clone", "--filter=blob:none", "--no-checkout", profile.Everest.Repository, output);
        }
        Run("git", "-C", output, "fetch", "--force", "origin", profile.Everest.Sha256Commit);
        Run("git", "-C", output, "checkout", "--detach", profile.Everest.Sha256Commit);
        Run("git", "-C", output, "submodule", "update", "--init", "--recursive", "external/MonoMod");
        VerifyUpstream(profile, output);
    }

    private static AppleEverestProfile LoadProfile(string path)
    {
        AppleEverestProfile profile = JsonSerializer.Deserialize<AppleEverestProfile>(File.ReadAllBytes(path), Json)
            ?? throw new InvalidDataException("profile JSON is empty");
        if (profile.SchemaVersion != 1 || profile.AppleTransformationVersion != 1 || profile.CelesteCanonicalClass != ProductPolicy.CanonicalClass ||
            profile.Everest.Tag != "stable-1.6458.0" || profile.Everest.Sha256Commit != "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00" ||
            profile.Dependencies.MonoModCommit != "dfc30a1506d37fb88a2c2be004f525205f46a24c" ||
            profile.Dependencies.NLuaProvenanceOnlyCommit != "b3524288712743fb2394dcf615d14d0dac3276e2" ||
            profile.Dependencies.YamlDotNetVersion != "16.1.3" || profile.Host.DotnetSdk != "8.0.424" ||
            profile.Host.MonoModBuildSdk != "9.0.317")
            throw new InvalidDataException("profile does not match the accepted Stage 25B pins");
        return profile;
    }

    private static void VerifyUpstream(AppleEverestProfile profile, string upstream)
    {
        string head = Capture("git", "-C", upstream, "rev-parse", "HEAD");
        string monoMod = Capture("git", "-C", Path.Combine(upstream, "external", "MonoMod"), "rev-parse", "HEAD");
        if (head != profile.Everest.Sha256Commit) throw new InvalidDataException($"Everest pin mismatch: {head}");
        if (monoMod != profile.Dependencies.MonoModCommit) throw new InvalidDataException($"MonoMod pin mismatch: {monoMod}");
    }

    private static Dictionary<string, List<string>> Parse(string[] args)
    {
        Dictionary<string, List<string>> result = new(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
                throw new InvalidDataException("options must be --name value pairs");
            if (!result.TryGetValue(args[index], out List<string>? values)) result[args[index]] = values = [];
            values.Add(args[index + 1]);
        }
        return result;
    }

    private static string One(Dictionary<string, List<string>> options, string name) =>
        options.TryGetValue(name, out List<string>? values) && values.Count == 1 ? values[0] : throw new InvalidDataException($"exactly one {name} is required");
    private static IReadOnlyList<string> Many(Dictionary<string, List<string>> options, string name) => options.TryGetValue(name, out List<string>? values) ? values : [];

    private static string Capture(string command, params string[] args)
    {
        ProcessStartInfo info = new(command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in args) info.ArgumentList.Add(arg);
        using Process process = Process.Start(info) ?? throw new InvalidOperationException($"failed to start {command}");
        string stdout = process.StandardOutput.ReadToEnd(); string stderr = process.StandardError.ReadToEnd(); process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidDataException($"{command} failed: {stderr.Trim()}");
        return stdout.Trim();
    }

    private static void Run(string command, params string[] args) { _ = Capture(command, args); }

    private static void Help() => Console.WriteLine("AppleEverestBuilder acquire|build|apply|scan-runtime|verify-profile (Stage 25B closed static-AOT foundation)");
}
