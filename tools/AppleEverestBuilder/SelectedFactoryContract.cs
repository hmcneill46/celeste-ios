using System.Text.Json;

namespace AppleEverestBuilder;

// Selects only two reviewed verification contracts. The K-N contract binds the
// exact private extraction; it does not turn a registry into semantic evidence.
internal sealed class SelectedFactoryContract : IDisposable
{
    internal const string SnasManifestSha256 = "9552770401ef4d5d465c67dbbab5958f9eec07524b67d482fd5696ab751eeda2";
    internal JsonDocument Profiles { get; }
    internal SelectedFactoryClosureFactory[] Factories { get; }
    internal bool IsSnas { get; }

    private SelectedFactoryContract(JsonDocument profiles, SelectedFactoryClosureFactory[] factories, bool isSnas)
    { Profiles = profiles; Factories = factories; IsSnas = isSnas; }

    internal static SelectedFactoryContract Load(string manifestPath, string profilesPath)
    {
        if (Hashing.FileSha256(manifestPath) != SnasManifestSha256)
        {
            var old = SelectedFactoryTypeClosure.LoadAndValidate(manifestPath);
            return new(JsonDocument.Parse(File.ReadAllBytes(profilesPath)), old.Manifest.Factories, false);
        }
        JsonDocument profiles = SnasProfileAuthority.Load(profilesPath);
        var factories = profiles.RootElement.GetProperty("factories").EnumerateArray()
            .Select(row => new SelectedFactoryClosureFactory {
                Kind = row.GetProperty("kind").GetString()!, CustomId = row.GetProperty("customId").GetString()!,
                Provider = row.GetProperty("provider").GetString()! }).ToArray();
        return new(profiles, factories, true);
    }

    public void Dispose() => Profiles.Dispose();
}
