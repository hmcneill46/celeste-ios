using System.Security.Cryptography;
using System.Text;

namespace AppleEverestBuilder;

internal static class Hashing
{
    public static string FileSha256(string path)
    {
        using FileStream input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    public static string BytesSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string LogicalHash(IEnumerable<FileRecord> records)
    {
        StringBuilder manifest = new();
        foreach (FileRecord record in records.OrderBy(item => item.Path, StringComparer.Ordinal))
            manifest.Append(record.Path).Append('\0').Append(record.Bytes).Append('\0').Append(record.Sha256).Append('\n');
        return BytesSha256(Encoding.UTF8.GetBytes(manifest.ToString()));
    }

    public static IReadOnlyList<FileRecord> Inventory(string root)
    {
        string fullRoot = Path.GetFullPath(root);
        List<FileRecord> result = [];
        foreach (string file in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories)
                     .OrderBy(path => Path.GetRelativePath(fullRoot, path).Replace('\\', '/'), StringComparer.Ordinal))
        {
            FileInfo info = new(file);
            string relative = Path.GetRelativePath(fullRoot, file).Replace('\\', '/');
            result.Add(new FileRecord(relative, info.Length, FileSha256(file)));
        }
        return result;
    }
}
