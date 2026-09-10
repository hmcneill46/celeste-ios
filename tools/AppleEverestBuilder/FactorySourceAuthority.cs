namespace AppleEverestBuilder;

// Binds K-N compilation receipts to the current owned transformation sources.
// This is provenance, not an exclusion or change to runtime logical hashing.
internal static class FactorySourceAuthority
{
    internal static FileRecord[] Inventory(string repoRoot)
    {
        List<string> files = [];
        foreach (string tree in new[] { "tools/AppleEverestBuilder", "tools/AppleEverestIlWorker", "apple-everest/runtime", "apple-everest/profiles" })
        foreach (string file in Directory.EnumerateFiles(Path.Combine(repoRoot, tree), "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(Path.Combine(repoRoot, tree), file).Replace('\\', '/');
            if (relative.Split('/').Any(part => part is "bin" or "obj" or "tests")) continue;
            if (Path.GetExtension(file) is ".cs" or ".csproj" or ".json" or ".props" or ".targets") files.Add(file);
        }
        files.AddRange(Directory.EnumerateFiles(Path.Combine(repoRoot, "apple-everest"), "*.json"));
        files.Add(Path.Combine(repoRoot, "modern-ios/IOSPortVersion.props"));
        return files.Select(file => new FileRecord(Path.GetRelativePath(repoRoot, file).Replace('\\', '/'),
            new FileInfo(file).Length, Hashing.FileSha256(file))).OrderBy(row => row.Path, StringComparer.Ordinal).ToArray();
    }
}
