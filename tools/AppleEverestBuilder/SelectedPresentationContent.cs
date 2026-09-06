namespace AppleEverestBuilder;

// Content-only release projection. The original package remains the input and
// its exact ZIP is verified before selecting any presentation files.
internal static class SelectedPresentationContent
{
    internal static Func<string, bool>? Resolve(ModInput input, EverestYamlEntry metadata)
    {
        if (metadata.Name != "StrawberryJam2021Assets") return null;
        if (metadata.Version != "1.0.1" || !string.IsNullOrEmpty(metadata.DLL) || !File.Exists(input.SourcePath) ||
            Hashing.FileSha256(input.SourcePath) != "26fab85f20fff89d1adcba2ef926c3447d9996057c7dfc78e5d97f9d96ed7e45")
            throw new InvalidDataException("unregistered SJ presentation asset package identity");
        bool Include(string path) => path.StartsWith("Graphics/Atlases/Stickers/SJ2021/1-Beginner/", StringComparison.Ordinal) &&
            path.EndsWith(".png", StringComparison.Ordinal);
        if (input.Files.Count(f => Include(f.Path)) != 21)
            throw new InvalidDataException("selected SJ sticker file census changed");
        return Include;
    }
}
