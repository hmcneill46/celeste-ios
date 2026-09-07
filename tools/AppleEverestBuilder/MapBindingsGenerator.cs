using System.Text;
using System.Text.Json;

namespace AppleEverestBuilder;

internal static class MapBindingsGenerator
{
    internal sealed record Binding(string Sid, string SourceLabel, string ForegroundTiles,
        string BackgroundTiles, string AnimatedTiles, string Sprites, bool MergeAnimations, int TerrainSeed);

    internal static Binding[] Bind(IReadOnlyList<MapProgressionRecord> maps, IReadOnlyList<ContentMountRecord> content,
        SelectedContentPlan? contentPlan)
    {
        return maps.Select(map =>
        {
            MapPresentationRecord view = map.Presentation ?? MapPresentationRecord.EverestDefault;
            ContentMountRecord mapMount = content.Single(mount => mount.LogicalPath == "Maps/" + map.Sid + ".bin");
            int terrainSeed = contentPlan?.Preserve(mapMount.Owner, mapMount.SourcePath) == true
                ? (view.Name.Length > 0 ? view.Name : map.Sid).Sum(character => (int)character) : -1;
            bool canary = map.Sid.StartsWith("AppleEverest/Stage25KJ", StringComparison.Ordinal) ||
                map.Sid.StartsWith("AppleEverestStage25KJ/", StringComparison.Ordinal);
            const string fixtureGraphics = "Graphics/SJ2021xmls/BeginnerLobby/";
            string Resolve(string path)
            {
                if (string.IsNullOrEmpty(path)) return "";
                if (path.StartsWith('/') || path.Split('/').Contains("..") || !path.EndsWith(".xml", StringComparison.Ordinal))
                    throw new InvalidDataException("invalid map graphics reference: " + path);
                ContentMountRecord[] matches = content.Where(mount => mount.SourcePath.Equals(path, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(mount => mount.Order).ToArray();
                return matches.Length > 0 ? matches[0].LogicalPath
                    : throw new InvalidDataException("map graphics resource not mounted: " + path);
            }
            return new Binding(map.Sid, map.SourcePackageLabel,
                Resolve(canary && view.ForegroundTiles.Length == 0 ? fixtureGraphics + "ForegroundTiles.xml" : view.ForegroundTiles),
                Resolve(view.BackgroundTiles),
                Resolve(canary && view.AnimatedTiles.Length == 0 ? fixtureGraphics + "AnimatedTiles.xml" : view.AnimatedTiles),
                Resolve(canary && view.Sprites.Length == 0 ? fixtureGraphics + "Sprites.xml" : view.Sprites), canary, terrainSeed);
        }).ToArray();
    }

    internal static string Source(IReadOnlyList<Binding> bindings, IReadOnlyList<ResolvedMod> mods)
    {
        static string Quote(string value) => JsonSerializer.Serialize(value);
        StringBuilder text = new("#nullable disable\nnamespace Celeste.Mod;\ninternal static class GeneratedAppleEverestMapBindings\n{\n    internal static readonly AppleEverestMapBinding[] Maps =\n    {\n");
        foreach (Binding map in bindings)
            text.Append("        new(").Append(string.Join(", ", new[] { map.Sid, map.SourceLabel, map.ForegroundTiles,
                map.BackgroundTiles, map.AnimatedTiles, map.Sprites }.Select(Quote)))
                .Append(", ").Append(map.MergeAnimations ? "true" : "false").Append(", ").Append(map.TerrainSeed).AppendLine("),");
        text.AppendLine("    };\n    internal static readonly string[] ExcludedMapSids =\n    {");
        HashSet<string> included = bindings.Select(map => map.Sid).ToHashSet(StringComparer.Ordinal);
        foreach (string sid in mods.SelectMany(mod => mod.Input.Files).Select(file => file.Path)
                     .Where(path => path.StartsWith("Maps/", StringComparison.Ordinal) && path.EndsWith(".bin", StringComparison.Ordinal))
                     .Select(path => path[5..^4]).Where(sid => !included.Contains(sid)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            text.Append("        ").Append(Quote(sid)).AppendLine(",");
        return text.AppendLine("    };\n}").ToString();
    }
}
