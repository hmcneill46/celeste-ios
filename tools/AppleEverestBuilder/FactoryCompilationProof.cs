using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace AppleEverestBuilder;

// HOST ONLY. Presence and reflection inspection alone cannot bind a compiled
// implementation to current source. Rebuild the actual regenerated closure,
// including the production static IL targets, and compare every DLL byte
// except the fresh compiler's COFF timestamp and module GUID. No semantic
// normalization may hide a changed component, hook,
// coroutine, static initializer, constructor, guard argument or return value.
internal static class FactoryCompilationProof
{
    internal sealed record Evidence(string CanonicalManagedLogicalSha256,
        string AppliedManagedLogicalSha256, string AppliedManagedSourceLogicalSha256, string CompiledDllLogicalSha256,
        FileRecord[] CompiledDlls, FileRecord[] FreshRawDlls, FileRecord[] InspectedRawDlls,
        bool FreshProductionCompilation, bool AllImplementationBytesCompared, string[] IgnoredBuildFields,
        string Scope);

    internal static Evidence CompileAndCompare(string repoRoot, string regenerated,
        string canonicalManaged, string assemblyPath, string dotnet, string temporary)
    {
        canonicalManaged = Path.GetFullPath(canonicalManaged);
        if (!File.Exists(Path.Combine(canonicalManaged, "Celeste.Modern.csproj")) ||
            Directory.Exists(Path.Combine(canonicalManaged, "Celeste", "Mod", "AppleEverestStatic")) ||
            Directory.Exists(Path.Combine(canonicalManaged, "bin")) || Directory.Exists(Path.Combine(canonicalManaged, "obj")))
            throw new InvalidDataException("factory compilation proof requires a pristine generated pre-Everest managed source tree");
        string sourceHash = Hashing.LogicalHash(Hashing.Inventory(canonicalManaged));
        string target = Path.Combine(temporary, "fresh-managed");
        Directory.CreateDirectory(target);
        foreach (string path in Directory.EnumerateFiles(canonicalManaged, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(target, Path.GetRelativePath(canonicalManaged, path));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(path, destination);
        }
        ClosureGenerator.Apply(regenerated, target);
        string appliedHash = Hashing.LogicalHash(Hashing.Inventory(target));
        // Keep the complete local input hash above. Only the portable source
        // digest excludes the separately bound host tool directory: its build
        // metadata can vary with checkout path/commit. Runtime DLL comparison
        // below still compares every implementation byte with the same narrow
        // timestamp/MVID exclusions as before.
        string appliedSourceHash = Hashing.LogicalHash(Hashing.Inventory(target)
            .Where(file => !file.Path.StartsWith(".AppleEverestStaticIlHost/", StringComparison.Ordinal)));
        ProcessStartInfo start = new(dotnet)
        {
            WorkingDirectory = repoRoot, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { "build", Path.Combine(target, "Celeste.Modern.csproj"),
            "-c", "Release", "--nologo", "--no-incremental", "-p:CelesteAppleRepoRoot=" + repoRoot,
            "-p:CelesteManagedGeneratedRoot=" + target }) start.ArgumentList.Add(argument);
        Console.WriteLine("factory preflight: fresh production compilation including static IL transformations");
        using Process process = Process.Start(start) ?? throw new InvalidDataException("could not start production compiler");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string buildOutput = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new InvalidDataException("fresh production compilation failed:\n" + buildOutput);
        string[] compiled = Directory.GetFiles(Path.Combine(target, "bin", "Release"), "Celeste.dll", SearchOption.AllDirectories);
        if (compiled.Length != 1) throw new InvalidDataException("fresh untrimmed production assembly is ambiguous or absent");
        string freshRoot = Path.GetDirectoryName(compiled[0])!;
        string suppliedRoot = Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!;
        if (Path.GetFileName(assemblyPath) != "Celeste.dll")
            throw new InvalidDataException("production preflight requires the untrimmed Celeste.dll and its original sibling DLL set");
        FileRecord[] expected = DllInventory(freshRoot);
        try { VerifyExactDlls(expected, suppliedRoot); }
        catch (InvalidDataException)
        {
            foreach (FileRecord file in expected.Where(file => File.Exists(Path.Combine(suppliedRoot, file.Path))))
            {
                byte[] freshBytes = File.ReadAllBytes(Path.Combine(freshRoot, file.Path));
                byte[] oldBytes = File.ReadAllBytes(Path.Combine(suppliedRoot, file.Path));
                if (freshBytes.SequenceEqual(oldBytes)) continue;
                int[] differences = Enumerable.Range(0, Math.Min(freshBytes.Length, oldBytes.Length))
                    .Where(index => freshBytes[index] != oldBytes[index]).ToArray();
                Console.WriteLine("compiled difference: " + file.Path + " bytes=" + freshBytes.Length + "/" + oldBytes.Length +
                    " changed=" + differences.Length + " first-offsets=" + string.Join(",", differences.Take(32)));
            }
            throw;
        }
        Console.WriteLine("factory preflight: fresh compiled implementation bytes equal inspected production DLLs (" + expected.Length + "; only compiler timestamp/MVID excluded)");
        return new(sourceHash, appliedHash, appliedSourceHash, Hashing.LogicalHash(expected), expected,
            RawInventory(freshRoot), RawInventory(suppliedRoot), true, true,
            ["project-produced DLL COFF TimeDateStamp (4 bytes)", "project-produced DLL Module.Mvid GUID heap entry (16 bytes)"],
            "UNTRIMMED_PRODUCTION_MANAGED_CLOSURE_FINAL_PLATFORM_TRIM_AND_NATIVE_AOT_ARE_SEPARATE_GATES");
    }

    private static string[] DllFiles(string root)
    {
        string[] files = Directory.EnumerateFiles(root).Where(path =>
            Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (files.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() != 1))
            throw new InvalidDataException("case-aliased production sibling DLLs are ambiguous");
        return files;
    }

    private static FileRecord[] RawInventory(string root) => DllFiles(root)
        .OrderBy(Path.GetFileName, StringComparer.Ordinal)
        .Select(path => new FileRecord(Path.GetFileName(path), new FileInfo(path).Length, Hashing.FileSha256(path))).ToArray();

    internal static FileRecord[] DllInventory(string root) => DllFiles(root)
        .OrderBy(Path.GetFileName, StringComparer.Ordinal)
        .Select(path => new FileRecord(Path.GetFileName(path), new FileInfo(path).Length,
            Hashing.BytesSha256(ImplementationBytes(path)))).ToArray();

    internal static byte[] ImplementationBytes(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        // Frozen package assemblies are copied artifacts and must remain
        // literally identical, including all build identifiers.
        if (Path.GetFileName(path) is not ("Celeste.dll" or "FNA.dll" or "CelesteIOSFoundation.dll" or "CelesteAppleInput.dll"))
            return bytes;
        using MemoryStream stream = new(bytes, writable: false);
        using PEReader pe = new(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        byte[] mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToByteArray();
        int root = pe.PEHeaders.MetadataStartOffset;
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
        stream.Position = root;
        if (reader.ReadUInt32() != 0x424a5342) throw new InvalidDataException("invalid managed metadata root");
        stream.Position = root + 12;
        uint versionLength = reader.ReadUInt32();
        if (versionLength > 1024) throw new InvalidDataException("invalid metadata version size");
        stream.Position = root + 16 + versionLength;
        _ = reader.ReadUInt16();
        int streams = reader.ReadUInt16();
        if (streams is < 1 or > 32) throw new InvalidDataException("invalid metadata stream count");
        int guidOffset = -1, guidSize = 0;
        for (int index = 0; index < streams; index++)
        {
            int offset = checked((int)reader.ReadUInt32());
            int size = checked((int)reader.ReadUInt32());
            List<byte> name = [];
            byte value;
            while ((value = reader.ReadByte()) != 0)
            {
                if (name.Count >= 32) throw new InvalidDataException("metadata stream name exceeds bound");
                name.Add(value);
            }
            stream.Position = (stream.Position + 3) & ~3L;
            if (Encoding.ASCII.GetString(name.ToArray()) != "#GUID") continue;
            if (guidOffset != -1 || size % 16 != 0 || offset < 0 || size < 16 || root + (long)offset + size > bytes.Length)
                throw new InvalidDataException("invalid GUID heap");
            guidOffset = root + offset; guidSize = size;
        }
        if (guidOffset == -1) throw new InvalidDataException("module GUID heap absent");
        int[] matches = Enumerable.Range(0, guidSize / 16).Select(index => guidOffset + index * 16)
            .Where(offset => bytes.AsSpan(offset, 16).SequenceEqual(mvid)).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("module GUID entry is absent or ambiguous");
        Array.Clear(bytes, matches[0], 16);
        Array.Clear(bytes, pe.PEHeaders.CoffHeaderStartOffset + 4, 4);
        return bytes;
    }

    internal static void VerifyExactDlls(FileRecord[] expected, string suppliedRoot)
    {
        FileRecord[] actual = DllInventory(suppliedRoot);
        if (!expected.Select(file => file.Path).SequenceEqual(actual.Select(file => file.Path)))
            throw new InvalidDataException("inspected production sibling DLL set differs from fresh compilation");
        for (int index = 0; index < expected.Length; index++)
            if (expected[index] != actual[index])
                throw new InvalidDataException("inspected production DLL bytes differ from fresh compilation: " + expected[index].Path);
    }
}
