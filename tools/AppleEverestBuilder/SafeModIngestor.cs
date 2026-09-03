using System.IO.Compression;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AppleEverestBuilder;

internal static class SafeModIngestor
{
    private const string StrawberryJam1012ArchiveSha256 = "4e1a2fc12baa3db27da433b93bf26b59f34b3e6b7f760c41d8d127636d020655";
    private const int StrawberryJam1012MaxFiles = 30000;
    private const long StrawberryJam1012MaxExpandedBytes = 256L * 1024 * 1024;
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static ModInput Ingest(string source, string stagingParent, int index)
    {
        source = Path.GetFullPath(source);
        if (!File.Exists(source) && !Directory.Exists(source))
            throw new InvalidDataException($"mod input does not exist: {source}");

        string staging = Path.Combine(stagingParent, $"mod-{index:D3}");
        Directory.CreateDirectory(staging);
        bool exactStrawberryJam = File.Exists(source) &&
            Hashing.FileSha256(source) == StrawberryJam1012ArchiveSha256;
        if (File.Exists(source)) ExtractZip(source, staging,
            exactStrawberryJam ? StrawberryJam1012MaxFiles : ProductPolicy.MaxFiles,
            exactStrawberryJam ? StrawberryJam1012MaxExpandedBytes : ProductPolicy.MaxExpandedBytes);
        else CopyDirectory(source, staging);

        IReadOnlyList<FileRecord> files = Hashing.Inventory(staging);
        int maxFiles = exactStrawberryJam ? StrawberryJam1012MaxFiles : ProductPolicy.MaxFiles;
        long maxExpandedBytes = exactStrawberryJam ? StrawberryJam1012MaxExpandedBytes : ProductPolicy.MaxExpandedBytes;
        if (files.Count == 0 || files.Count > maxFiles)
            throw new InvalidDataException("mod file-count budget exceeded or input is empty");
        long bytes = files.Sum(item => item.Bytes);
        if (bytes > maxExpandedBytes)
            throw new InvalidDataException("mod expanded-size budget exceeded");

        string yamlPath = new[] { "everest.yaml", "everest.yml" }
            .Select(name => Path.Combine(staging, name))
            .SingleOrDefault(File.Exists)
            ?? throw new InvalidDataException("mod must contain exactly one root everest.yaml or everest.yml");
        FileInfo yamlInfo = new(yamlPath);
        if (yamlInfo.Length <= 0 || yamlInfo.Length > ProductPolicy.MaxYamlBytes)
            throw new InvalidDataException("Everest metadata size is invalid");

        string yamlText = File.ReadAllText(yamlPath, new UTF8Encoding(false, true));
        List<EverestYamlEntry> metadata;
        try
        {
            metadata = Yaml.Deserialize<List<EverestYamlEntry>>(yamlText) ?? [];
        }
        catch
        {
            EverestYamlEntry? single = Yaml.Deserialize<EverestYamlEntry>(yamlText);
            metadata = single == null ? [] : [single];
        }
        if (metadata.Count == 0 || metadata.Count > 32)
            throw new InvalidDataException("Everest metadata must contain 1-32 entries");
        foreach (EverestYamlEntry entry in metadata) ValidateMetadata(entry);

        return new ModInput
        {
            SourcePath = source,
            StagingRoot = staging,
            SourceSha256 = Hashing.LogicalHash(files),
            Files = files,
            Metadata = metadata
        };
    }

    private static void ExtractZip(string archive, string destination, int maxFiles, long maxExpandedBytes)
    {
        if (!string.Equals(Path.GetExtension(archive), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("only ZIP archives or directories are accepted");
        using ZipArchive zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count == 0 || zip.Entries.Count > maxFiles)
            throw new InvalidDataException("ZIP file-count budget exceeded or archive is empty");
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            string path = NormalizeRelative(entry.FullName);
            bool directory = entry.FullName.EndsWith("/", StringComparison.Ordinal);
            if (!seen.Add(path.TrimEnd('/')))
                throw new InvalidDataException($"duplicate ZIP path: {path}");
            if ((entry.ExternalAttributes & 0xF000) is 0xA000 or 0x6000)
                throw new InvalidDataException($"ZIP links are forbidden: {path}");
            if (directory)
            {
                Directory.CreateDirectory(ResolveUnder(destination, path));
                continue;
            }
            if (entry.Length < 0 || entry.Length > ProductPolicy.MaxSingleFileBytes)
                throw new InvalidDataException($"ZIP member size budget exceeded: {path}");
            expanded = checked(expanded + entry.Length);
            if (expanded > maxExpandedBytes)
                throw new InvalidDataException("ZIP expanded-size budget exceeded");
            string target = ResolveUnder(destination, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using Stream input = entry.Open();
            using FileStream output = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
            if (output.Length != entry.Length)
                throw new InvalidDataException($"ZIP member length mismatch: {path}");
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        int count = 0;
        long bytes = 0;
        foreach (string path in Directory.EnumerateFileSystemEntries(source, "*", SearchOption.AllDirectories)
                     .OrderBy(value => Path.GetRelativePath(source, value).Replace('\\', '/'), StringComparer.Ordinal))
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            if (!string.IsNullOrEmpty(info.LinkTarget))
                throw new InvalidDataException($"links are forbidden: {Path.GetRelativePath(source, path)}");
            string relative = NormalizeRelative(Path.GetRelativePath(source, path).Replace('\\', '/'));
            string target = ResolveUnder(destination, relative);
            if (info is DirectoryInfo) Directory.CreateDirectory(target);
            else
            {
                FileInfo file = (FileInfo)info;
                count++;
                bytes = checked(bytes + file.Length);
                if (count > ProductPolicy.MaxFiles || file.Length > ProductPolicy.MaxSingleFileBytes || bytes > ProductPolicy.MaxExpandedBytes)
                    throw new InvalidDataException("directory input exceeds file/size budget");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(path, target, overwrite: false);
            }
        }
    }

    private static string NormalizeRelative(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOf('\0') >= 0 || value.Contains('\\'))
            throw new InvalidDataException("invalid archive path");
        if (value.StartsWith('/') || Path.IsPathRooted(value) || value.Contains(':'))
            throw new InvalidDataException($"absolute archive path is forbidden: {value}");
        string[] parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Length > ProductPolicy.MaxPathDepth || parts.Any(part => part is "." or ".."))
            throw new InvalidDataException($"unsafe or over-deep archive path: {value}");
        return string.Join('/', parts) + (value.EndsWith('/') ? "/" : "");
    }

    private static string ResolveUnder(string root, string relative)
    {
        string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(fullRoot, StringComparison.Ordinal))
            throw new InvalidDataException("archive path escaped staging root");
        return result;
    }

    private static void ValidateMetadata(EverestYamlEntry entry)
    {
        if (!IsIdentifier(entry.Name)) throw new InvalidDataException("invalid or missing Everest Name");
        _ = EverestVersion.Parse(entry.Version);
        if (entry.DLL != null)
        {
            string normalized = NormalizeRelative(entry.DLL);
            if (normalized.EndsWith('/')) throw new InvalidDataException("DLL metadata names a directory");
            entry.DLL = normalized;
        }
        foreach (EverestDependency dependency in entry.Dependencies.Concat(entry.OptionalDependencies).Concat(entry.Conflicts))
        {
            if (!IsIdentifier(dependency.Name)) throw new InvalidDataException("invalid dependency identity");
            _ = EverestVersion.Parse(dependency.Version);
        }
    }

    private static bool IsIdentifier(string value) =>
        value.Length is > 0 and <= 128 && char.IsAsciiLetterOrDigit(value[0]) &&
        char.IsAsciiLetterOrDigit(value[^1]) &&
        value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' or '.' or ' ' or '\'') &&
        !value.Contains("  ", StringComparison.Ordinal);
}
